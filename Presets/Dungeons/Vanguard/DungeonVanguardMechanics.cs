using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Mechanics.PilgrimsTraverse;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long vanguardSerial;
    private bool vanguardSmall;
    private DungeonAmbientAoe? vanguardShrink;
    private int vanguardFence = -1;
    private readonly List<DungeonPendingCast> vanguardSimple = [];
    private readonly List<DungeonAmbientAoe> vanguardEnhanced = [];
    private readonly List<DungeonAmbientAoe> vanguardRotary = [];
    private readonly List<DungeonAmbientAoe> vanguardCombo = [];
    private readonly HashSet<(ulong Source, DateTime Start)> vanguardRotaryStarts = [];
    private readonly List<(ulong Source, DateTime Start, Vector3 Origin, float Rotation, DateTime At, int Remaining, long Serial)> vanguardBattery = [];
    private readonly List<DungeonAmbientAoe> vanguardBatteryCache = [];
    private bool vanguardBatteryDirty;
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> vanguardLines = [];
    private readonly Dictionary<ulong, DateTime> vanguardBombs = [];
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> vanguardBombSnapshots = [];
    private readonly List<(ulong Source, DateTime Start, DungeonActor Target, DateTime At)> vanguardBusters = [];

    private bool IsVanguardTrash => territory == VanguardCatalog.TERRITORY && Active != null && VanguardCatalog.IsTrash(Active.BossOid);
    private bool VanguardRequireAllStop => territory == VanguardCatalog.TERRITORY
        && _actors.TryGetValue(PlayerId, out var player) && !player.Dead
        && vanguardBombs.TryGetValue(PlayerId, out var at) && at < _now.AddSeconds(1);

    private DungeonDefinition? VanguardDefinition(DungeonActor actor)
        => Definitions.FirstOrDefault(d => (d.BossOid == actor.Oid || d.PrimaryAliases?.Contains(actor.Oid) == true)
            && (!VanguardCatalog.IsTrash(d.BossOid) || AetherfontInBounds(d.Arena(actor.Position), actor.Position, AetherfontRadius(d.Arena(actor.Position)))));

    private bool VanguardCanStart(DungeonActor actor) => actor.InCombat && VanguardDefinition(actor) != null;

    private void VanguardSelectPrimary()
    {
        if (!IsVanguardTrash || Active == null || Arena == null) return;
        // The arena disambiguates initial discovery; an active group ends only when every member is dead or destroyed.
        var alive = _actors.Values.Where(a => !a.Dead && (a.Oid == Active.BossOid || Active.PrimaryAliases?.Contains(a.Oid) == true)).ToArray();
        if (alive.Length == 0) { ResetFight(); return; }
        if (!alive.Any(a => a.Id == PrimaryId)) PrimaryId = alive[0].Id;
    }

    private void VanguardCast(DungeonPendingCast cast)
    {
        if (territory != VanguardCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        var id = $"vanguard/cast/{cast.Source}/{cast.StartedAt.Ticks}";
        if (VanguardCatalog.Enhanced(action) || VanguardCatalog.Combo(action))
        {
            var list = VanguardCatalog.Enhanced(action) ? vanguardEnhanced : vanguardCombo;
            var index = list.FindIndex(a => a.Id == id);
            var aoe = new DungeonAmbientAoe(id, cast.Rule.Name, cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
            if (index >= 0) list[index] = aoe with { Color = list[index].Color, Navigate = list[index].Navigate };
            else
            {
                list.Add(aoe);
                if (list == vanguardCombo || list.Count == 2)
                    list.Sort((a, b) => a.Activation!.Value.CompareTo(b.Activation));
            }
        }
        else if (action == 37351)
        {
            if (!vanguardBattery.Any(s => s.Source == cast.Source && s.Start == cast.StartedAt))
            {
                vanguardBattery.Add((cast.Source, cast.StartedAt, cast.Location, cast.Rotation, cast.FinishAt, 34, ++vanguardSerial));
                vanguardBatteryDirty = true;
            }
        }
        else if (cast.Rule.Shape != null && cast.Rule.PartyAoe == null)
        {
            var snapshot = cast;
            if (action is 38051 or 36569 && _actors.TryGetValue(cast.Source, out var source))
            {
                var delta = cast.Location - source.Position; delta.Y = 0;
                snapshot = cast with { Rule = cast.Rule with { Shape = RayRectangleTemplate.Symmetric(5, delta.Length()) },
                    Location = QuantizedDungeonPosition(source.Position), Rotation = MathF.Atan2(delta.X, delta.Z) };
            }
            var index = vanguardSimple.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index < 0) vanguardSimple.Add(snapshot); else vanguardSimple[index] = snapshot;
            var group = VanguardCatalog.SimpleGroup(action);
            if (group != action || group is 37160 or 36574 or 37198)
            {
                var indices = Enumerable.Range(0, vanguardSimple.Count).Where(i => VanguardCatalog.SimpleGroup(vanguardSimple[i].Rule.Action) == group).ToArray();
                if (indices.Length >= 99)
                {
                    var ordered = indices.Select(i => vanguardSimple[i]).OrderBy(c => c.FinishAt).ToArray();
                    for (var i = 0; i < indices.Length; ++i) vanguardSimple[indices[i]] = ordered[i];
                }
            }
        }
        if (action is 39140 or 39141 or 36559 or 36560 && vanguardRotaryStarts.Add((cast.Source, cast.StartedAt)))
        {
            var angle = action is 39140 or 36559 ? -MathF.PI / 3 : MathF.PI / 3;
            GeometryTemplate other = action is 39140 or 36560
                ? new PtDonutSectorTemplate(17, 28, MathF.PI * 2 / 3) : new FanTemplate(14, MathF.PI * 2 / 3);
            for (var i = 0; i < 3; ++i)
                foreach (var shape in new GeometryTemplate[] { new PtDonutSectorTemplate(11, 17, MathF.PI * 2 / 3), other })
                    vanguardRotary.Add(new($"vanguard/rotary/{++vanguardSerial}", "连续回旋", shape,
                        QuantizedDungeonPosition(Arena.Position), angle - i * MathF.PI * 2 / 3, Activation: cast.FinishAt.AddSeconds(1.8 + i * .3)));
        }
        if (action == 36595)
        {
            var target = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
            if (_actors.TryGetValue(target, out var actor))
            {
                var index = vanguardBusters.FindIndex(b => b.Source == cast.Source && b.Start == cast.StartedAt);
                var prediction = (cast.Source, cast.StartedAt, actor, cast.FinishAt);
                if (index < 0) vanguardBusters.Add(prediction); else vanguardBusters[index] = prediction;
            }
        }
        if (!vanguardSmall && action is 36571 or 37161)
        {
            var outer = action == 36571 ? new Vector2(20, 20) : new Vector2(15, 23);
            var inner = action == 36571 ? new Vector2(17, 17) : new Vector2(12, 20);
            vanguardShrink = new("vanguard/shrink", "缩场预告", VanguardRing(Arena.Position, outer, inner),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(.4));
        }
        if (!vanguardSmall && action == 36594)
            vanguardShrink = new("vanguard/shrink", "缩场预告", new DonutTemplate(17, 20), Arena.Position,
                Activation: cast.FinishAt.AddSeconds(.5));
    }

    private static GeometryTemplate VanguardRing(Vector3 center, Vector2 outer, Vector2 inner)
    {
        Vector2[][] strips =
        [
            [new(-outer.X, -outer.Y), new(outer.X, -outer.Y), new(outer.X, -inner.Y), new(-outer.X, -inner.Y)],
            [new(-outer.X, inner.Y), new(outer.X, inner.Y), new(outer.X, outer.Y), new(-outer.X, outer.Y)],
            [new(-outer.X, -inner.Y), new(-inner.X, -inner.Y), new(-inner.X, inner.Y), new(-outer.X, inner.Y)],
            [new(inner.X, -inner.Y), new(outer.X, -inner.Y), new(outer.X, inner.Y), new(inner.X, inner.Y)],
        ];
        var fill = strips.SelectMany(p => new[] { new[] { p[0], p[1], p[2] }, new[] { p[0], p[2], p[3] } }).ToArray();
        return new DungeonShapeTemplate(new PolygonRegionGeometry(center, strips, [], fill));
    }

    private void VanguardFinish(DungeonPendingCast cast)
    {
        if (territory != VanguardCatalog.TERRITORY) return;
        if (VanguardCatalog.Enhanced(cast.Rule.Action) && vanguardEnhanced.Count > 0) vanguardEnhanced.RemoveAt(0);
        if (VanguardCatalog.Combo(cast.Rule.Action) && vanguardCombo.Count > 0) vanguardCombo.RemoveAt(0);
        var index = vanguardSimple.FindIndex(c => c.Source == cast.Source);
        if (index >= 0) vanguardSimple.RemoveAt(index);
    }

    private void VanguardEffect(ulong source, uint action, ulong target, DateTime now)
    {
        if (territory != VanguardCatalog.TERRITORY) return;
        if (action is 36565 or 36566 or 36567 && vanguardRotary.Count > 0) vanguardRotary.RemoveAt(0);
        if (action == 36576) vanguardBusters.Clear();
        if (action == 36595)
        {
            var index = vanguardBusters.FindIndex(b => b.Target.Id == target);
            if (index >= 0) vanguardBusters.RemoveAt(index);
        }
        if (action is 37351 or 37344 && _actors.TryGetValue(source, out var caster))
        {
            var index = vanguardBattery.FindIndex(s => MathF.Abs(s.Origin.X - caster.Position.X) <= 1
                && MathF.Abs(s.Origin.Z - caster.Position.Z) <= 1
                && MathF.Abs(MathF.Atan2(MathF.Sin(s.Rotation - caster.Rotation), MathF.Cos(s.Rotation - caster.Rotation))) <= .05f);
            if (index >= 0)
            {
                var s = vanguardBattery[index];
                if (s.Remaining == 1) vanguardBattery.RemoveAt(index);
                else vanguardBattery[index] = (s.Source, s.Start, s.Origin, s.Rotation - 11 * MathF.PI / 180, now.AddSeconds(.5), s.Remaining - 1, s.Serial);
                vanguardBatteryDirty = true;
            }
        }
        if (action == 37347 && _actors.TryGetValue(source, out var a) && _actors.TryGetValue(target, out var b))
            vanguardLines.Add((a, b, now.AddSeconds(8), ++vanguardSerial));
        if (action == 37345)
        {
            var index = vanguardLines.FindIndex(l => l.Target.Id == target);
            if (index >= 0) vanguardLines.RemoveAt(index);
            else if (vanguardLines.Count == 1 && _actors.ContainsKey(target)) vanguardLines.Clear();
        }
    }

    private void VanguardMap(uint index, uint state, DateTime now)
    {
        if (territory != VanguardCatalog.TERRITORY || Arena == null) return;
        if (state == 0x00020001 && (Active?.BossOid == 0x411D && index == 0x0A
            || Active?.BossOid == 0x4237 && index == 0x0C || Active?.BossOid == 0x411E && index == 0))
        {
            vanguardSmall = true; vanguardShrink = null; vanguardFence = -1;
            Arena = Active.BossOid switch
            {
                0x411D => new RectangleGeometry(Arena.Position, 0, 34, 34),
                0x4237 => new RectangleGeometry(new(0, Arena.Position.Y, -100), 0, 24, 40),
                _ => VanguardArenas.ZanderSmall(Arena.Position.Y),
            };
        }
        if (Active?.BossOid != 0x4237 || index != 0x0D) return;
        var fence = state switch { 0x01000080 => 0, 0x08000400 => 1, 0x00020001 => 2, 0x00200010 => 3, _ => -1 };
        if (fence >= 0)
        {
            vanguardFence = fence;
            var shape = VanguardFenceAoe(fence, Arena.Position.Y);
            vanguardShrink = new("vanguard/fence", "电网预告", new DungeonShapeTemplate(shape), shape.Position, Activation: now.AddSeconds(3));
        }
        if (state is 0x02000004 or 0x10000004 or 0x00080004 or 0x00400004)
            Arena = new RectangleGeometry(new(0, Arena.Position.Y, -100), 0, 24, 40);
    }

    private static IGeometryShape VanguardFenceAoe(int index, float y) => index switch
    {
        0 => VanguardArenas.Fence0Aoe(y), 1 => VanguardArenas.Fence1Aoe(y),
        2 => VanguardArenas.Fence2Aoe(y), _ => VanguardArenas.Fence3Aoe(y),
    };

    private void VanguardStatus(ulong actor, uint status, bool present, DateTime? expires)
    {
        if (territory != VanguardCatalog.TERRITORY || status is not (3802 or 4144)
            || !_actors.TryGetValue(actor, out var a) || a.PartySlot < 0) return;
        if (present && expires is { } at) vanguardBombs[actor] = at;
        else if (!present) vanguardBombs.Remove(actor);
    }

    private void SynchronizeVanguard()
    {
        if (territory != VanguardCatalog.TERRITORY || Arena == null) return;
        if (vanguardFence >= 0 && vanguardShrink?.Activation <= _now)
        {
            Arena = vanguardFence switch
            {
                0 => VanguardArenas.Fence0Arena(Arena.Position.Y), 1 => VanguardArenas.Fence1Arena(Arena.Position.Y),
                2 => VanguardArenas.Fence2Arena(Arena.Position.Y), _ => VanguardArenas.Fence3Arena(Arena.Position.Y),
            };
            vanguardShrink = null; vanguardFence = -1;
        }
        foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0))
        {
            if (a.Dead)
            {
                vanguardBombs.Remove(a.Id);
                foreach (var key in vanguardBombSnapshots.Keys.Where(k => k.Actor == a.Id).ToArray()) vanguardBombSnapshots.Remove(key);
                continue;
            }
            if (a.Statuses == null && a.StatusExpirations == null) continue;
            foreach (var status in new uint[] { 3802, 4144 })
            {
                var key = (a.Id, status);
                var recentEvent = statusChanges.TryGetValue(key, out var change) && _now < change.ChangedAt.AddSeconds(.5);
                var present = a.Statuses?.Contains(status) == true || a.StatusExpirations?.ContainsKey(status) == true;
                if (present)
                {
                    var expires = a.StatusExpirations?.GetValueOrDefault(status) ?? default;
                    var changed = !vanguardBombSnapshots.TryGetValue(key, out var old) || old != expires;
                    vanguardBombSnapshots[key] = expires;
                    if (changed && expires != default && (!recentEvent || change.Present && !vanguardBombs.ContainsKey(a.Id)))
                        VanguardStatus(a.Id, status, true, expires);
                }
                else if (vanguardBombSnapshots.Remove(key) && (!recentEvent || !change.Present))
                    VanguardStatus(a.Id, status, false, null);
            }
            var gainPending = statusChanges.Any(s => s.Key.Actor == a.Id && s.Key.Status is 3802 or 4144
                && s.Value.Present && _now < s.Value.ChangedAt.AddSeconds(.5));
            if (!vanguardBombSnapshots.Keys.Any(k => k.Actor == a.Id) && !gainPending) vanguardBombs.Remove(a.Id);
        }
        for (var i = 0; i < vanguardLines.Count; ++i)
        {
            var l = vanguardLines[i];
            vanguardLines[i] = (_actors.GetValueOrDefault(l.Source.Id) ?? l.Source, _actors.GetValueOrDefault(l.Target.Id) ?? l.Target, l.At, l.Serial);
        }
    }

    private void ResetVanguard()
    {
        vanguardSerial = 0; vanguardSmall = false; vanguardShrink = null; vanguardFence = -1;
        vanguardSimple.Clear(); vanguardEnhanced.Clear(); vanguardRotary.Clear(); vanguardCombo.Clear(); vanguardRotaryStarts.Clear();
        vanguardBattery.Clear(); vanguardBatteryCache.Clear(); vanguardBatteryDirty = false;
        vanguardLines.Clear(); vanguardBombs.Clear(); vanguardBombSnapshots.Clear(); vanguardBusters.Clear();
    }
}
