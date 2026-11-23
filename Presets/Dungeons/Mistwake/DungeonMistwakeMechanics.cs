using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long mistSerial;
    private bool mistSmall;
    private bool mistRocksInitialized;
    private readonly HashSet<int> mistRemovedRocks = [];
    private readonly List<DungeonActor> mistRocks = [];
    private readonly HashSet<(ulong Source, DateTime Start)> mistStarts = [];
    private readonly List<DungeonPendingCast> mistSimple = [];
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast)> mistParty = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DungeonPendingCast Cast)> mistBusters = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> mistLines = [];
    private readonly List<(DungeonPendingCast Cast, DungeonAmbientAoe Aoe)> mistCharges = [];
    private readonly List<DungeonAmbientAoe> mistBursts = [];
    private readonly List<(DungeonPendingCast Cast, IGeometryShape[] Shelters)> mistLos = [];
    private readonly List<DungeonPendingCast> mistKbs = [];
    private readonly Dictionary<ulong, DateTime> mistBirths = [];
    private readonly HashSet<ulong> mistStorms = [];
    private readonly HashSet<ulong> mistBurstObserved = [];
    private readonly HashSet<ulong> mistBurstPending = [];
    private readonly Dictionary<ulong, int> mistInferredBursts = [];
    private DungeonAmbientAoe? mistStorm;
    private DungeonAmbientAoe? mistShrink;
    private int mistStackHits;
    private bool mistAlongZ;

    private void MistCast(DungeonPendingCast c)
    {
        if (territory != MistwakeCatalog.TERRITORY || Arena == null) return;
        var fresh = mistStarts.Add((c.Source, c.StartedAt));
        var action = c.Rule.Action;
        if (c.Rule.PartyAoe != null && _actors.TryGetValue(c.TargetId, out var target))
        {
            var i = mistParty.FindIndex(v => v.Cast.Source == c.Source && v.Cast.StartedAt == c.StartedAt);
            if (i >= 0) mistParty[i] = (target, c);
            else if (fresh) mistParty.Add((target, c));
        }
        else if (c.Rule.Shape != null && action != 47022)
        {
            var i = mistSimple.FindIndex(v => v.Source == c.Source && v.StartedAt == c.StartedAt);
            if (i >= 0) mistSimple[i] = c;
            else if (fresh) mistSimple.Add(c);
            var group = MistSimpleGroup(action);
            if (group != 0 && mistSimple.Count(v => MistSimpleGroup(v.Rule.Action) == group) >= 99)
            {
                var indices = Enumerable.Range(0, mistSimple.Count).Where(n => MistSimpleGroup(mistSimple[n].Rule.Action) == group).ToArray();
                var sorted = indices.Select(n => mistSimple[n]).OrderBy(v => v.FinishAt).ToArray();
                for (var n = 0; n < indices.Length; ++n) mistSimple[indices[n]] = sorted[n];
            }
        }
        if (action == 43329 && _actors.TryGetValue(c.Source, out var caster))
        {
            var id = c.TargetId == c.Source ? caster.TargetId : c.TargetId;
            if (_actors.TryGetValue(id, out var actor))
            {
                var i = mistBusters.FindIndex(v => v.Cast.Source == c.Source && v.Cast.StartedAt == c.StartedAt);
                if (i >= 0) mistBusters[i] = (caster, actor, c);
                else if (fresh) mistBusters.Add((caster, actor, c));
            }
        }
        if (action == 45348 && _actors.TryGetValue(c.Source, out var source))
        {
            var i = mistCharges.FindIndex(v => v.Cast.Source == c.Source && v.Cast.StartedAt == c.StartedAt);
            var delta = c.Location - source.Position;
            var aoe = new DungeonAmbientAoe($"mist/charge/{c.Source}/{c.StartedAt.Ticks}", "GallopingThunder",
                RayRectangleTemplate.Symmetric(5, new Vector2(delta.X, delta.Z).Length()), source.Position, MathF.Atan2(delta.X, delta.Z),
                Activation: c.FinishAt.AddSeconds(8.7 + .5 * (i >= 0 ? i : mistCharges.Count)),
                Color: i == 0 || i < 0 && mistCharges.Count == 0 ? new Vector4(1, .1f, .1f, 1) : null);
            if (i >= 0) mistCharges[i] = (c, aoe with { Activation = mistCharges[i].Aoe.Activation, Color = mistCharges[i].Aoe.Color });
            else if (fresh) mistCharges.Add((c, aoe));
        }
        if (action == 43330)
        {
            var i = mistLos.FindIndex(v => v.Cast.Source == c.Source && v.Cast.StartedAt == c.StartedAt);
            if (i >= 0)
            {
                // A packet can correct the cast's locked origin without sampling later rock movement.
                var old = mistLos[i];
                if (old.Cast.Location != c.Location) mistLos[i] = (c, MistLosShelters(c.Location));
            }
            else if (fresh)
            {
                var active = _casts.Values.Where(v => v.Rule.Action == 43330).MinBy(v => v.FinishAt)!;
                mistLos.Add((c, MistLosShelters(active.Location)));
            }
        }
        if (action == 45291 && !mistSmall)
            mistShrink = new("mist/shrink", "场地收缩", new DonutTemplate(20, 30), new(281, Arena.Position.Y, -620),
                Activation: c.FinishAt.AddSeconds(.7));
        if (action == 47022)
        {
            var i = mistKbs.FindIndex(v => v.Source == c.Source && v.StartedAt == c.StartedAt);
            if (i >= 0) mistKbs[i] = c;
            else if (fresh) mistKbs.Add(c);
            var angle = c.Rotation + MathF.PI / 2;
            mistAlongZ = MathF.Abs(MathF.Sin(angle)) <= MathF.Sin(MathF.PI / 180);
        }
    }

    private static int MistSimpleGroup(uint action) => action switch
    {
        45337 or 45342 => 1,
        45296 or 45297 or 45298 => 2,
        _ => 0,
    };

    private void MistFinish(DungeonPendingCast c)
    {
        if (territory != MistwakeCatalog.TERRITORY) return;
        mistSimple.RemoveAll(v => v.Source == c.Source && (MistSimpleGroup(v.Rule.Action) != 0 || v.Rule.Action == c.Rule.Action));
        if (c.Rule.Action != 45353)
        {
            var i = mistParty.FindIndex(v => v.Cast.Rule.Action == c.Rule.Action && v.Target.Id == c.TargetId);
            if (i >= 0) mistParty.RemoveAt(i);
        }
        mistBusters.RemoveAll(v => v.Source.Id == c.Source && v.Cast.Rule.Action == c.Rule.Action);
        if (c.Rule.Action == 45349 && mistBursts.Count > 0) mistBursts.RemoveAt(0);
        if (c.Rule.Action == 45299) mistStorm = null;
        if (c.Rule.Action == 47022) mistKbs.Clear();
        if (c.Rule.Action == 43330 && mistLos.Count > 0) mistLos.RemoveAt(0);
    }

    private void MistEffect(uint action, ulong target)
    {
        if (territory != MistwakeCatalog.TERRITORY) return;
        if (action == 45347 && mistCharges.Count > 0)
        {
            mistCharges.RemoveAt(0);
            if (mistCharges.Count > 1)
            {
                var first = mistCharges[0];
                mistCharges[0] = (first.Cast, first.Aoe with { Color = new Vector4(1, .1f, .1f, 1) });
            }
        }
        if (action == 45354 && ++mistStackHits == 2)
        {
            mistParty.RemoveAll(v => v.Cast.Rule.Action == 45353);
            mistStackHits = 0;
        }
        if (action == 43334)
        {
            if (mistLines.Count == 1 && _actors.ContainsKey(target)) mistLines.Clear();
            else
            {
                var i = mistLines.FindIndex(v => v.Target.Id == target);
                if (i >= 0) mistLines.RemoveAt(i);
            }
        }
    }

    private void MistIcon(ulong target, uint icon, DateTime now, ulong source)
    {
        if (territory != MistwakeCatalog.TERRITORY || icon != 524 || Active?.BossOid != 0x4841) return;
        if (_actors.TryGetValue(source is 0 or 0xE0000000 ? target : source, out var a)
            && _actors.TryGetValue(target, out var b)) mistLines.Add((a, b, now.AddSeconds(6.2), ++mistSerial));
    }

    private void MistMap(uint index, uint state)
    {
        if (territory != MistwakeCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x4841 && index is >= 7 and <= 0x13)
        {
            var query = MistwakeRocks.Rocks[(int)index - 7];
            var x = index == 0x13 ? 97.5f : query.X;
            var i = Enumerable.Range(0, 13).FirstOrDefault(n => !mistRemovedRocks.Contains(n)
                && MathF.Abs(MistwakeRocks.Rocks[n].X - x) <= 1 && MathF.Abs(MistwakeRocks.Rocks[n].Z - query.Z) <= 1, -1);
            if (i >= 0) mistRemovedRocks.Add(i);
            Arena = MistwakeRocks.Build(Arena.Position.Y, mistRemovedRocks);
        }
        if (Active?.BossOid == 0x4A65 && index == 0x17 && state == 0x00020001)
        {
            Arena = MistwakeArenas.GriffinSmall(Arena.Position.Y); mistSmall = true; mistShrink = null;
        }
    }

    private void MistCreated(ulong id, DateTime now)
    {
        if (territory != MistwakeCatalog.TERRITORY) return;
        mistBirths[id] = now;
        if (_actors.GetValueOrDefault(id) is { Oid: 0x4A67 } actor && mistStorms.Contains(id) && mistStorm?.Id == $"mist/storm/{id}")
            mistStorm = mistStorm with { Activation = now.AddSeconds(6), Position = QuantizedDungeonPosition(actor.Position), Rotation = actor.Rotation };
    }

    private void MistStatus(ulong id, uint status, bool present, DateTime now)
    {
        if (territory != MistwakeCatalog.TERRITORY || Active?.BossOid != 0x4A77 || status != 2536) return;
        if (!present)
        {
            mistBurstPending.Remove(id);
            mistInferredBursts.Remove(id);
            return;
        }
        if (mistInferredBursts.GetValueOrDefault(id) > 0) { mistInferredBursts[id]--; return; }
        if (_actors.TryGetValue(id, out var a))
        {
            MistAddBurst(a, now);
            if (!mistBurstObserved.Contains(id)) mistBurstPending.Add(id);
        }
    }

    private void MistAddBurst(DungeonActor a, DateTime now)
        => mistBursts.Add(new($"mist/burst/{++mistSerial}", "Burst", new CircleTemplate(9),
            QuantizedDungeonPosition(a.Position), Activation: now.AddSeconds(4.5)));

    private void MistObserve()
    {
        if (territory != MistwakeCatalog.TERRITORY || Arena == null) return;
        if (!mistRocksInitialized && Active?.BossOid == 0x4841)
        {
            mistRocks.AddRange(_actors.Values.Where(a => a.Oid is 0x4851 or 0x4843 or 0x4842));
            mistRocksInitialized = true;
        }
        DungeonActor Latest(DungeonActor a) => _actors.GetValueOrDefault(a.Id) ?? a;
        for (var i = mistRocks.Count - 1; i >= 0; --i)
            if (!_actors.ContainsKey(mistRocks[i].Id)) mistRocks.RemoveAt(i);
            else mistRocks[i] = Latest(mistRocks[i]);
        for (var i = 0; i < mistParty.Count; ++i) mistParty[i] = (Latest(mistParty[i].Target), mistParty[i].Cast);
        for (var i = 0; i < mistBusters.Count; ++i)
        {
            var b = mistBusters[i]; mistBusters[i] = (Latest(b.Source), Latest(b.Target), b.Cast);
        }
        for (var i = 0; i < mistLines.Count; ++i)
        {
            var b = mistLines[i]; mistLines[i] = (Latest(b.Source), Latest(b.Target), b.At, b.Serial);
        }
        foreach (var a in _actors.Values)
        {
            if (Active?.BossOid == 0x4A65 && a.Oid == 0x4A67 && mistStorms.Add(a.Id))
                mistStorm = new($"mist/storm/{a.Id}", "StormSurge", RayRectangleTemplate.Symmetric(10, 50),
                    QuantizedDungeonPosition(a.Position), a.Rotation, Activation: mistBirths.GetValueOrDefault(a.Id, _now).AddSeconds(6));
            if (Active?.BossOid != 0x4A77) continue;
            var present = a.Statuses?.Contains(2536u) == true;
            if (present && mistBurstObserved.Add(a.Id) && !mistBurstPending.Remove(a.Id))
            {
                MistAddBurst(a, _now);
                mistInferredBursts[a.Id] = mistInferredBursts.GetValueOrDefault(a.Id) + 1;
            }
            if (!present && mistBurstObserved.Remove(a.Id)) mistInferredBursts.Remove(a.Id);
        }
    }

    private void MistTick()
    {
        if (territory != MistwakeCatalog.TERRITORY) return;
        mistParty.RemoveAll(v => v.Target.Dead && v.Cast.Rule.Action == 45353);
        mistBusters.RemoveAll(v => v.Source.Dead);
        mistLines.RemoveAll(v => v.Source.Dead);
        if (mistBursts.Count > 0 && mistBursts[^1].Activation > mistBursts[0].Activation!.Value.AddSeconds(1))
        {
            var deadline = mistBursts[0].Activation!.Value.AddSeconds(1);
            for (var i = 0; i < mistBursts.Count; ++i)
                if (mistBursts[i].Activation < deadline) mistBursts[i] = mistBursts[i] with { Color = new Vector4(1, .1f, .1f, 1) };
        }
    }

    private void ResetMist()
    {
        mistSerial = 0; mistSmall = false; mistRocksInitialized = false; mistRemovedRocks.Clear(); mistRocks.Clear(); mistStarts.Clear();
        mistSimple.Clear(); mistParty.Clear(); mistBusters.Clear(); mistLines.Clear(); mistCharges.Clear(); mistBursts.Clear();
        mistLos.Clear(); mistKbs.Clear(); mistBirths.Clear(); mistStorms.Clear(); mistBurstObserved.Clear(); mistBurstPending.Clear();
        mistInferredBursts.Clear(); mistStorm = null; mistShrink = null; mistStackHits = 0; mistAlongZ = false;
    }
}
