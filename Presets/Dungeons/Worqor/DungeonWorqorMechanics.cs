using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long worqorSerial;
    private int worqorTutorial;
    private int worqorFreezeCount;
    private bool worqorSmall;
    private bool worqorFirstFire = true;
    private bool worqorWEWE;
    private DungeonAmbientAoe? worqorShrink;
    private DungeonAmbientAoe? worqorWind;
    private uint worqorWindIndex;
    private bool worqorWindInverted;
    private readonly List<(ulong Source, DateTime Start, DungeonAmbientAoe Aoe)> worqorFrozen = [];
    private readonly List<DungeonAmbientAoe> worqorFire = [];
    private readonly List<ulong> worqorSnowOrder = [];
    private readonly HashSet<ulong> worqorCreated = [];
    private readonly HashSet<(ulong Source, ulong Target)> worqorFreezePairs = [];
    private readonly HashSet<(ulong Source, ulong Target)> worqorFreezeObserved = [];
    private readonly List<(DungeonActor Target, DateTime At, long Serial)> worqorStacks = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> worqorLines = [];
    private readonly List<DungeonPendingCast> worqorKnockbacks = [];

    internal bool WantsWorqorSprint => territory == WorqorCatalog.TERRITORY && Active?.BossOid == 0x415F
        && _actors.Values.Any(a => a.Oid == 0x4162 && !a.Dead);

    private bool WorqorCastVisible(DungeonPendingCast cast)
    {
        if (territory != WorqorCatalog.TERRITORY) return true;
        if (cast.Rule.Action is 36270 or 36271 or 36303 or 36304 or 36305) return false;
        return cast.Rule.Action != 36278 || worqorSnowOrder.Take(6).Contains(cast.Source);
    }

    private void WorqorCast(DungeonPendingCast cast)
    {
        if (territory != WorqorCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        if (action == 36278 && !worqorSnowOrder.Contains(cast.Source)) worqorSnowOrder.Add(cast.Source);
        if (action is 36270 or 36271)
        {
            var index = worqorFrozen.FindIndex(a => a.Source == cast.Source && a.Start == cast.StartedAt);
            var aoe = new DungeonAmbientAoe($"worqor/frozen/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name,
                cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
            if (index < 0) worqorFrozen.Add((cast.Source, cast.StartedAt, aoe));
            else worqorFrozen[index] = (cast.Source, cast.StartedAt, aoe with { Activation = worqorFrozen[index].Aoe.Activation });
        }
        if (action == 36280 && !worqorSmall)
            worqorShrink = new("worqor/shrink", "缩场预告", new DonutTemplate(20, 23), Arena.Position,
                Activation: cast.FinishAt.AddSeconds(.6));
        if (action == 36375 && !worqorSmall)
        {
            Vector2[][] exterior = [[new(-25, -25), new(25, -25), new(25, 25), new(-25, 25)]];
            Vector2[][] hole = [[new(-20, -20), new(20, -20), new(20, 20), new(-20, 20)]];
            Vector2[][] strips =
            [
                [new(-25, -25), new(25, -25), new(25, -20), new(-25, -20)],
                [new(-25, 20), new(25, 20), new(25, 25), new(-25, 25)],
                [new(-25, -20), new(-20, -20), new(-20, 20), new(-25, 20)],
                [new(20, -20), new(25, -20), new(25, 20), new(20, 20)],
            ];
            var fill = strips.SelectMany(p => new[] { new[] { p[0], p[1], p[2] }, new[] { p[0], p[2], p[3] } }).ToArray();
            worqorShrink = new("worqor/shrink", "缩场预告", new DungeonShapeTemplate(new PolygonRegionGeometry(Arena.Position, exterior, hole, fill)),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(.6));
        }
        if (action is 36307 or 36310 or 39074)
        {
            var index = worqorKnockbacks.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index < 0) worqorKnockbacks.Add(cast); else worqorKnockbacks[index] = cast;
        }
        if (worqorFirstFire && action is 36303 or 36304 or 36305 || !worqorFirstFire && action == 36307)
        {
            var id = $"worqor/fire/{cast.Source}/{cast.StartedAt.Ticks}";
            var index = worqorFire.FindIndex(a => a.Id == id);
            var safe = !worqorFirstFire;
            var aoe = new DungeonAmbientAoe(id, safe ? "站安全区等击退" : "连续火格",
                safe ? RayRectangleTemplate.Symmetric(20, 15) : cast.Rule.Shape!, cast.Location, cast.Rotation,
                Navigate: !safe, Activation: cast.FinishAt, Color: safe ? new Vector4(0, 128f / 255, 0, 128f / 255) : null);
            if (index < 0)
            {
                worqorFire.Add(aoe);
                if (worqorFirstFire && worqorFire.Count == 16) worqorFire.Sort((a, b) => a.Activation!.Value.CompareTo(b.Activation));
            }
            else worqorFire[index] = aoe with { Color = worqorFire[index].Color };
        }
    }

    private void WorqorFinish(DungeonPendingCast cast)
    {
        if (territory != WorqorCatalog.TERRITORY) return;
        worqorSnowOrder.Remove(cast.Source);
        if (cast.Rule.Action is 36270 or 36271)
        {
            worqorFreezePairs.RemoveWhere(p => p.Source == cast.Source);
            worqorFreezeObserved.RemoveWhere(p => p.Source == cast.Source);
            var index = worqorFrozen.FindIndex(a => a.Source == cast.Source);
            if (index >= 0)
            {
                worqorFrozen.RemoveAt(index);
                if (worqorFrozen.Count == 0) { ++worqorTutorial; worqorFreezeCount = 0; }
            }
        }
        if (worqorFire.Count > 0 && cast.Rule.Action is 36303 or 36304 or 36305 or 36307)
        {
            worqorFire.RemoveAt(0);
            if (worqorFire.Count == 0) worqorFirstFire = false;
        }
        worqorKnockbacks.RemoveAll(c => c.Source == cast.Source);
    }

    private void WorqorTether(ulong source, ulong target, uint tether, bool present, DateTime now)
    {
        if (territory != WorqorCatalog.TERRITORY || tether != 272) return;
        if (!present)
        {
            worqorFreezePairs.Remove((source, target));
            worqorFreezeObserved.Remove((source, target));
            return;
        }
        var index = worqorFrozen.FindIndex(a => a.Source == source);
        if (index < 0 || !worqorFreezePairs.Add((source, target))) return;
        var item = worqorFrozen[index];
        worqorFrozen[index] = item with { Aoe = item.Aoe with { Activation = now.AddSeconds(14.9) } };
        if (++worqorFreezeCount == (worqorTutorial < 2 ? 2 : 4))
            worqorFrozen.Sort((a, b) => a.Aoe.Activation!.Value.CompareTo(b.Aoe.Activation));
    }

    private void WorqorIcon(ulong actor, uint icon, DateTime now)
    {
        if (territory == WorqorCatalog.TERRITORY && Active?.BossOid == 0x415D && icon == 511
            && _actors.TryGetValue(actor, out var a))
            worqorStacks.Add((a, now.AddSeconds(6), ++worqorSerial));
    }

    private void WorqorEffect(ulong source, uint action, ulong target, DateTime now)
    {
        if (territory != WorqorCatalog.TERRITORY) return;
        if (action == 36296)
        {
            var index = worqorStacks.FindIndex(s => s.Target.Id == target);
            if (index < 0) worqorStacks.Clear(); else worqorStacks.RemoveAt(index);
        }
        if (action == 36315 && _actors.TryGetValue(source, out var a) && _actors.TryGetValue(target, out var b))
            worqorLines.Add((a, b, now.AddSeconds(4.9), ++worqorSerial));
        if (action == 39260)
        {
            var index = worqorLines.FindIndex(l => l.Target.Id == target);
            if (index >= 0) worqorLines.RemoveAt(index);
            else if (worqorLines.Count == 1 && _actors.ContainsKey(target)) worqorLines.Clear();
        }
    }

    private void WorqorMap(uint index, uint state, DateTime now)
    {
        if (territory != WorqorCatalog.TERRITORY || Arena == null) return;
        if (state == 0x00020001 && (Active?.BossOid == 0x4159 && index == 0x17 || Active?.BossOid == 0x415F && index == 0x18))
        {
            worqorSmall = true; worqorShrink = null;
            // MiniArena.Bounds changes only the shape; its center remains the initial resolved center.
            Arena = Active.BossOid == 0x4159
                ? new LocalPolygonGeometry(Arena.Position, 0, ((LocalPolygonGeometry)WorqorArenas.RyoqorSmall(Arena.Position.Y)).LocalPoints)
                : new RectangleGeometry(Arena.Position, 0, 40, 40);
        }
        if (Active?.BossOid != 0x415D) return;
        if (state is 0x02000001 or 0x04000004 or 0x08000004 or 0x01000001) { worqorWind = null; return; }
        if (state is not (0x00800040 or 0x00200010) || index is < 0x1E or > 0x21) return;
        var inverted = state == 0x00800040;
        var position = new Vector3(index == 0x1F ? -63 : -43, Arena.Position.Y, -57);
        GeometryTemplate shape;
        if (index < 0x20) shape = inverted ? new DonutTemplate(8, 50) : new CircleTemplate(15);
        else
        {
            IGeometryShape polygon;
            if (index == 0x20) polygon = inverted ? WorqorArenas.Wind20Narrow(Arena.Position.Y) : WorqorArenas.Wind20Wide(Arena.Position.Y);
            else polygon = inverted ? WorqorArenas.Wind21Narrow(Arena.Position.Y) : WorqorArenas.Wind21Wide(Arena.Position.Y);
            shape = new DungeonShapeTemplate(polygon); position = polygon.Position;
        }
        worqorWindIndex = index; worqorWindInverted = index >= 0x20 && inverted;
        worqorWind = new("worqor/wind/display", "风土晶体", shape, position, Navigate: !worqorWindInverted,
            Activation: now.AddSeconds(5.9), Color: worqorWindInverted ? new Vector4(0, 128f / 255, 0, 128f / 255) : null);
    }

    private void WorqorObserveActors()
    {
        if (territory != WorqorCatalog.TERRITORY) return;
        for (var i = 0; i < worqorLines.Count; ++i)
        {
            var l = worqorLines[i];
            worqorLines[i] = (_actors.GetValueOrDefault(l.Source.Id) ?? l.Source, _actors.GetValueOrDefault(l.Target.Id) ?? l.Target, l.At, l.Serial);
        }
        for (var i = 0; i < worqorStacks.Count; ++i)
        {
            var s = worqorStacks[i];
            worqorStacks[i] = (_actors.GetValueOrDefault(s.Target.Id) ?? s.Target, s.At, s.Serial);
        }
        worqorStacks.RemoveAll(s => s.Target.Dead);
    }

    private void SynchronizeWorqor()
    {
        if (territory != WorqorCatalog.TERRITORY) return;
        foreach (var actor in _actors.Values)
        {
            if (actor.Tethers?.TryGetValue(272, out var target) == true)
            {
                WorqorTether(actor.Id, target, 272, true, _now);
                if (worqorFreezePairs.Contains((actor.Id, target))) worqorFreezeObserved.Add((actor.Id, target));
            }
            if (worqorCreated.Add(actor.Id) && actor.Oid == 0x4160)
            {
                if (actor.Position.Z == -180) worqorWEWE = false;
                else if (actor.Position.Z == -210) worqorWEWE = true;
            }
        }
        worqorCreated.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var pair in worqorFreezeObserved.Where(p => !_actors.TryGetValue(p.Source, out var a)
            || a.Tethers?.GetValueOrDefault(272u) != p.Target).ToArray())
        {
            worqorFreezePairs.Remove(pair);
            worqorFreezeObserved.Remove(pair);
        }
    }

    private void ResetWorqor()
    {
        worqorSerial = 0; worqorTutorial = 0; worqorFreezeCount = 0; worqorSmall = false; worqorFirstFire = true;
        worqorWEWE = false; worqorShrink = null; worqorWind = null; worqorWindIndex = 0; worqorWindInverted = false;
        worqorFrozen.Clear(); worqorFire.Clear(); worqorSnowOrder.Clear(); worqorCreated.Clear(); worqorFreezePairs.Clear();
        worqorFreezeObserved.Clear(); worqorStacks.Clear(); worqorLines.Clear(); worqorKnockbacks.Clear();
    }
}
