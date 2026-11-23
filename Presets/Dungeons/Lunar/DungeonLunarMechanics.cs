using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int lunarConfluences;
    private int lunarLandslips;
    private bool lunarTowerDanger;
    private bool lunarAntlionSmall;
    private long lunarSerial;
    private bool lunarModelsInitialized;
    private DungeonAmbientAoe? lunarShrink;
    private readonly Dictionary<ulong, (byte Model, byte Anim1, byte Anim2)> lunarModels = [];
    private readonly List<DungeonAmbientAoe> lunarCrosses = [];
    private readonly List<DungeonAmbientAoe> lunarMarches = [];
    private readonly List<DungeonAmbientAoe> lunarTowers = [];
    private readonly List<DungeonAmbientAoe> lunarBatteries = [];
    private readonly List<DungeonPendingCast> lunarKnockbacks = [];
    private readonly List<ulong> lunarQuicksandOrder = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> lunarLines = [];
    private readonly HashSet<ulong> lunarDoom = [];
    private readonly HashSet<ulong> lunarDoomUnconfirmed = [];
    private readonly HashSet<ulong> lunarDoomLosses = [];

    private void SynchronizeLunar()
    {
        if (territory != LunarCatalog.TERRITORY) return;
        lunarQuicksandOrder.RemoveAll(id => !_actors.TryGetValue(id, out var a) || a.Oid != 0x1EB90E);
        foreach (var a in _actors.Values.Where(a => a.Oid == 0x1EB90E))
            if (!lunarQuicksandOrder.Contains(a.Id)) lunarQuicksandOrder.Add(a.Id);
        if (Active?.BossOid == 0x3FE2)
            foreach (var actor in _actors.Values)
            {
                var state = (actor.ModelState, actor.AnimationState1, actor.AnimationState2);
                var changed = lunarModels.TryGetValue(actor.Id, out var old) ? old != state : state != default;
                lunarModels[actor.Id] = state;
                if (lunarModelsInitialized && changed && actor.Oid == 0x3FE3 && actor.AnimationState2 == 1)
                {
                    var delay = lunarConfluences == 0 ? 8.1 : 25.9;
                    if (lunarConfluences > 1) delay = 32;
                    lunarCrosses.Add(new($"lunar/cross/{++lunarSerial}", "魔杖连续十字", new DungeonCrossTemplate(40, 4),
                        QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: _now.AddSeconds(delay)));
                }
                if (actor.Statuses?.Contains(3364u) == true) { lunarDoom.Add(actor.Id); lunarDoomUnconfirmed.Remove(actor.Id); }
                else if (lunarDoomLosses.Remove(actor.Id) || !lunarDoomUnconfirmed.Contains(actor.Id)) lunarDoom.Remove(actor.Id);
            }
        lunarModelsInitialized = true;
        lunarDoom.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || a.Dead);
    }

    private void SynchronizeLunarLines()
    {
        for (var i = 0; i < lunarLines.Count; ++i)
        {
            var line = lunarLines[i];
            lunarLines[i] = (_actors.GetValueOrDefault(line.Source.Id) ?? line.Source,
                _actors.GetValueOrDefault(line.Target.Id) ?? line.Target, line.At, line.Serial);
        }
    }

    private void LunarCast(DungeonPendingCast cast, DateTime now)
    {
        if (territory != LunarCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action == 34813 && !lunarAntlionSmall)
        {
            Vector2[][] strips = [[new(-19.5f, -25), new(19.5f, -25), new(19.5f, -20), new(-19.5f, -20)],
                [new(-19.5f, 20), new(19.5f, 20), new(19.5f, 25), new(-19.5f, 25)]];
            var fill = strips.SelectMany(p => new[] { new[] { p[0], p[1], p[2] }, new[] { p[0], p[2], p[3] } }).ToArray();
            lunarShrink = new("lunar/shrink", "缩场预告", new DungeonShapeTemplate(new PolygonRegionGeometry(Arena.Position, strips, [], fill)),
                Arena.Position, Activation: cast.FinishAt);
        }
        if (cast.Rule.Action == 35011 && Arena is CircleGeometry { Radius: > 20 })
            lunarShrink = new("lunar/shrink", "缩场预告", new DonutTemplate(20, 23), Arena.Position, Activation: cast.FinishAt);
        if (cast.Rule.Action == 34819)
        {
            var index = lunarKnockbacks.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index >= 0) lunarKnockbacks[index] = cast; else lunarKnockbacks.Add(cast);
        }
        if (cast.Rule.Action == 35871 && _actors.TryGetValue(cast.Source, out var caster))
        {
            var delta = cast.Location - caster.Position;
            var id = $"lunar/march/{cast.Source}/{cast.StartedAt.Ticks}";
            var aoe = new DungeonAmbientAoe(id, "连续冲锋", RayRectangleTemplate.Symmetric(9, new Vector2(delta.X, delta.Z).Length()),
                QuantizedDungeonPosition(caster.Position), MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt.AddSeconds(4.2));
            var index = lunarMarches.FindIndex(a => a.Id == id);
            if (index >= 0) lunarMarches[index] = aoe; else lunarMarches.Add(aoe);
        }
        if (cast.Rule.Action == 36058)
        {
            var id = $"lunar/battery/{cast.Source}/{cast.StartedAt.Ticks}";
            var index = lunarBatteries.FindIndex(a => a.Id == id);
            if (index >= 0) lunarBatteries[index] = lunarBatteries[index] with { Position = cast.Location };
            else lunarBatteries.Add(new(id, "连续电池圈", new CircleTemplate(5), cast.Location,
                Navigate: false, Activation: now.AddSeconds(6.5)));
        }
    }

    private void LunarFinish(DungeonPendingCast cast)
    {
        if (territory != LunarCatalog.TERRITORY) return;
        if (cast.Rule.Action == 35205) ++lunarConfluences;
        if (cast.Rule.Action is 35254 or 34789) lunarCrosses.Clear();
        if (cast.Rule.Action == 34819)
        {
            lunarKnockbacks.Clear();
            if (++lunarLandslips > 4) lunarTowerDanger = true;
        }
    }

    private void LunarMap(uint index, uint state, DateTime now)
    {
        if (territory != LunarCatalog.TERRITORY || Arena == null || state != 0x00020001) return;
        if (Active?.BossOid == 0x4022)
        {
            if (index == 0) { lunarAntlionSmall = true; Arena = new RectangleGeometry(Arena.Position, 0, 39, 40); lunarShrink = null; }
            else
            {
                var left = index < 5;
                lunarTowers.Add(new($"lunar/tower/{++lunarSerial}", "倒塔", RayRectangleTemplate.Symmetric(10, 40),
                    QuantizedDungeonPosition(new(left ? -20 : 20, Arena.Position.Y, left ? 35 + index * 10 : -5 + index * 10)),
                    (left ? 89.999f : -90.004f) * MathF.PI / 180, Activation: now.AddSeconds(13 - lunarTowers.Count)));
            }
        }
        if (Active?.BossOid == 0x4042 && index == 10) { Arena = new CircleGeometry(Arena.Position, 20); lunarShrink = null; }
    }

    private void LunarEffect(ulong source, uint action, ulong target, DateTime now)
    {
        if (territory != LunarCatalog.TERRITORY) return;
        if (action == 34817 && lunarMarches.Count > 0) lunarMarches.RemoveAt(0);
        if (action == 34820) { lunarTowers.Clear(); lunarTowerDanger = false; }
        if (action == 34994 && lunarBatteries.Count > 0) lunarBatteries.RemoveAt(0);
        if (action == 14588 && _actors.TryGetValue(source, out var a) && _actors.TryGetValue(target, out var b))
            lunarLines.Add((a, b, now.AddSeconds(5.4), ++lunarSerial));
        if (action == 35008 && lunarLines.Count > 0) lunarLines.RemoveAt(0);
    }

    private void LunarStatus(ulong actor, uint status, bool present)
    {
        if (territory != LunarCatalog.TERRITORY || Active?.BossOid != 0x3FE2 || status != 3364) return;
        if (present) { lunarDoom.Add(actor); lunarDoomUnconfirmed.Add(actor); lunarDoomLosses.Remove(actor); }
        else { lunarDoomUnconfirmed.Remove(actor); lunarDoomLosses.Add(actor); }
    }

    internal DungeonActor? LunarCleanseTarget(uint job)
        => territory == LunarCatalog.TERRITORY && Active?.BossOid == 0x3FE2 && job is 6 or 24 or 28 or 33 or 40 or 23
            ? lunarDoom.Select(id => _actors.GetValueOrDefault(id)).FirstOrDefault(a => a is { Dead: false, PartySlot: >= 0 }) : null;

    private void ResetLunar()
    {
        lunarConfluences = 0; lunarLandslips = 0; lunarTowerDanger = false; lunarAntlionSmall = false; lunarSerial = 0; lunarShrink = null;
        lunarModelsInitialized = false;
        lunarModels.Clear(); lunarCrosses.Clear(); lunarMarches.Clear(); lunarTowers.Clear(); lunarBatteries.Clear(); lunarKnockbacks.Clear(); lunarLines.Clear();
        lunarQuicksandOrder.Clear();
        lunarDoom.Clear(); lunarDoomUnconfirmed.Clear(); lunarDoomLosses.Clear();
    }
}
