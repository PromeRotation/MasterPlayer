using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long alzadaalSerial;
    private readonly List<DungeonAmbientAoe> alzadaalTentacles = [];
    private readonly List<ulong> alzadaalFountains = [];
    private readonly List<ulong> alzadaalDrudges = [];
    private readonly Dictionary<ulong, (byte Model, byte Animation1, byte Animation2)> alzadaalModels = [];
    private readonly Dictionary<ulong, byte> alzadaalStates = [];
    private readonly Dictionary<ulong, ushort> alzadaalOrders = [];
    private readonly List<DungeonAmbientAoe> alzadaalCones = [];
    private readonly List<DungeonAmbientAoe> alzadaalRects = [];
    private int alzadaalWaves;
    private int alzadaalConeHits;
    private int alzadaalRectHits;
    private DungeonAmbientAoe? alzadaalVoid;
    private int alzadaalPattern;
    private ulong alzadaalManaTarget;
    private DateTime alzadaalManaAt;
    private readonly List<DungeonAmbientAoe> alzadaalMana = [];
    private readonly Dictionary<(ulong Source, DateTime Start), DungeonAmbientAoe> alzadaalTraps = [];
    private readonly HashSet<ulong> alzadaalSpinning = [];
    private int alzadaalBlades;

    private void AlzadaalCast(DungeonPendingCast cast)
    {
        if (territory != AlzadaalCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action is 29556 or 29467 && !alzadaalFountains.Contains(cast.Source))
        {
            alzadaalFountains.Add(cast.Source);
            if (alzadaalFountains.Count >= 13)
                alzadaalFountains.Sort((a, b) => _casts[a].FinishAt.CompareTo(_casts[b].FinishAt));
        }
        if (cast.Rule.Action == 28441)
            alzadaalVoid = new("alzadaal/void", "战车下方持续危险", new CircleTemplate(6), Arena.Position, Activation: cast.FinishAt.AddSeconds(.8));
        if (cast.Rule.Action == 28519)
            alzadaalTraps[(cast.Source, cast.StartedAt)] = new($"alzadaal/trap/{cast.Source}/{cast.StartedAt.Ticks}", "持续尖刺陷阱",
                cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (cast.Rule.Action == 28530)
            delayedDamage[cast.Source] = cast.TargetId == cast.Source
                ? cast with { TargetId = _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 }
                : cast;
    }

    private void AlzadaalFinish(DungeonPendingCast cast)
    {
        if (territory != AlzadaalCatalog.TERRITORY || Arena == null) return;
        alzadaalFountains.Remove(cast.Source);
        if (cast.Rule.Action is 29158 or 28508 && alzadaalTentacles.Count > 0) alzadaalTentacles.RemoveAt(0);
        if (cast.Rule.Action == 28523) { alzadaalMana.Clear(); alzadaalPattern = 0; }
        if (cast.Rule.Action == 28520) ++alzadaalBlades;
        if (cast.Rule.Action == 28528 && Math.Max(Arena.GetBounds().Width, Arena.GetBounds().Height) / 2 < 24)
            Arena = new RectangleGeometry(Arena.Position, 0, 30, 40);
    }

    internal void ActionTimeline(ulong source, uint timeline)
    {
        if (territory != AlzadaalCatalog.TERRITORY || Active?.BossOid != 0x386C) return;
        if (timeline == 0x1E43) alzadaalDrudges.Add(source);
        else if (timeline == 0x1E39) alzadaalDrudges.Remove(source);
    }

    private void AlzadaalStatus(ulong target, uint status, bool present, ushort param, DateTime time)
    {
        if (territory != AlzadaalCatalog.TERRITORY) return;
        if (status == 2973)
        {
            if (present) { alzadaalSpinning.Add(target); alzadaalBlades = 0; }
            else alzadaalSpinning.Remove(target);
        }
        if (status != 2552 || Active?.BossOid != 0x386C) return;
        if (!present) alzadaalOrders.Remove(target);
        else if (alzadaalOrders.GetValueOrDefault(target, ushort.MaxValue) != param && _actors.TryGetValue(target, out var actor))
        {
            alzadaalOrders[target] = param;
            alzadaalRects.Add(new($"alzadaal/cannon/{++alzadaalSerial}", "顺序短炮", RayRectangleTemplate.Symmetric(8, 28),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: time.AddSeconds(param == 0x180 ? 6.9 : 15)));
        }
    }

    private void AlzadaalEffect(uint action)
    {
        if (territory != AlzadaalCatalog.TERRITORY || Active?.BossOid != 0x386C) return;
        if (action == 28456) alzadaalWaves = 1;
        else if (action == 28457) alzadaalWaves = 2;
        else if (alzadaalWaves == 2)
        {
            if (action == 28455 && alzadaalCones.Count > 0 && ++alzadaalConeHits == 14)
            {
                alzadaalCones.RemoveRange(0, Math.Min(2, alzadaalCones.Count)); alzadaalConeHits = 0;
                if (alzadaalCones.Count == 0) alzadaalWaves = 0;
            }
            else if (action == 28445 && alzadaalRects.Count > 0 && ++alzadaalRectHits == 14)
            {
                alzadaalRects.RemoveRange(0, Math.Min(2, alzadaalRects.Count)); alzadaalRectHits = 0;
            }
        }
        else if (alzadaalWaves == 1 && action == 28455 && ++alzadaalConeHits == 14)
        {
            alzadaalCones.Clear(); alzadaalRects.Clear(); alzadaalConeHits = 0; alzadaalWaves = 0;
        }
    }

    private void AlzadaalModel(byte model)
    {
        if (alzadaalCones.Count > 0 || Arena == null || alzadaalWaves == 0) return;
        if (model is 4 or 5)
        {
            AddCones(model, _now.AddSeconds(alzadaalWaves == 2 ? 7.1 : 6.9));
            if (alzadaalWaves == 2) AddCones(model == 4 ? (byte)5 : (byte)4, _now.AddSeconds(15));
        }
        if (alzadaalWaves != 1) return;
        foreach (var id in alzadaalDrudges)
        {
            if (!_actors.TryGetValue(id, out var actor)) continue;
            var corner = actor.Position.X is -20.5f or 20.5f && actor.Position.Z is -202.5f or -161.5f;
            alzadaalRects.Add(new($"alzadaal/cannon/{++alzadaalSerial}", "反射炮", RayRectangleTemplate.Symmetric(8, corner ? 28 : 40),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: _now.AddSeconds(6.9)));
        }
        void AddCones(byte state, DateTime time)
        {
            foreach (var degrees in state == 4 ? new[] { 134.999f, -45.003f } : new[] { -135.005f, 44.998f })
                alzadaalCones.Add(new($"alzadaal/reflection/{++alzadaalSerial}", "连续反射扇", new FanTemplate(30, MathF.PI / 2),
                    QuantizedDungeonPosition(Arena.Position), degrees * MathF.PI / 180, Activation: time));
        }
    }

    private void SynchronizeAlzadaal()
    {
        if (territory != AlzadaalCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x386C)
            foreach (var actor in _actors.Values)
            {
                var model = (actor.ModelState, actor.AnimationState1, actor.AnimationState2);
                var modelChanged = alzadaalModels.TryGetValue(actor.Id, out var old) ? old != model : model != default;
                alzadaalModels[actor.Id] = model;
                if (modelChanged) AlzadaalModel(actor.ModelState);
                if (!alzadaalStates.TryGetValue(actor.Id, out var state) || state != actor.EventState)
                { alzadaalStates[actor.Id] = actor.EventState; AlzadaalEventState(actor.Id, actor.EventState); }
                if (HasStatus(actor, 2552) && actor.StatusParams?.TryGetValue(2552, out var param) == true)
                    AlzadaalStatus(actor.Id, 2552, true, param, _now);
                else if (!HasStatus(actor, 2552)) alzadaalOrders.Remove(actor.Id);
            }
        if (Active?.BossOid != 0x36C1) return;
        alzadaalSpinning.RemoveWhere(id => !_actors.TryGetValue(id, out var actor) || !HasStatus(actor, 2973));
        foreach (var actor in _actors.Values)
            if (HasStatus(actor, 2973) && alzadaalSpinning.Add(actor.Id)) alzadaalBlades = 0;
        if (alzadaalManaTarget == 0 || !_actors.TryGetValue(alzadaalManaTarget, out var target)) return;
        if (target.Position.Z is not (-45.5f or -90.5f)) return;
        var first = (target.Position.Z == -45.5f) == (alzadaalPattern == 1);
        Vector2[] points = first ? [new(119, -68), new(101, -86), new(101, -50)] : [new(119, -50), new(101, -68), new(119, -86)];
        foreach (var point in points)
            alzadaalMana.Add(new($"alzadaal/mana/{++alzadaalSerial}", "连线布料爆炸", new CircleTemplate(15),
                QuantizedDungeonPosition(new(point.X, target.Position.Y, point.Y)), Activation: alzadaalManaAt));
        alzadaalManaTarget = 0;
    }

    private void AlzadaalEventState(ulong source, byte state)
    {
        if (territory == AlzadaalCatalog.TERRITORY && state == 4 && _actors.GetValueOrDefault(source)?.Oid == 0x1EB69C) alzadaalVoid = null;
    }

    private void AlzadaalMap(uint index, uint state, DateTime time)
    {
        if (territory != AlzadaalCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x3879)
        {
            Vector2? point = (index, state) switch
            {
                (0x11, 0x00080004) => new(117, -97), (0x11, 0x00800004) => new(131, -83),
                (0x11, 0x00200004) => new(131, -97), (0x11, 0x02000004) => new(117, -83),
                (0x10, 0x00200004) => new(109, -90), (0x10, 0x02000004) => new(139, -90),
                (0x10, 0x00080004) => new(124, -75), (0x10, 0x00800004) => new(124, -105), _ => null,
            };
            if (point is { } p) alzadaalTentacles.Add(new($"alzadaal/tentacle/{++alzadaalSerial}", "触手落点预告", new CircleTemplate(21),
                QuantizedDungeonPosition(new(p.X, Arena.Position.Y, p.Y)), Activation: time.AddSeconds(10.5)));
        }
        if (Active?.BossOid == 0x36C1)
        {
            if (state == 0x00020001 && index is 0x0D or 0x09) alzadaalPattern = index == 0x0D ? 1 : 2;
            if (index == 1 && state is 0x00400004 or 0x00800004 or 0x00080004) alzadaalTraps.Clear();
        }
    }

    private void AlzadaalTether(ulong target, uint tether, bool present, DateTime time)
    {
        if (territory != AlzadaalCatalog.TERRITORY || Active?.BossOid != 0x36C1 || tether != 188 || !present) return;
        alzadaalManaTarget = target; alzadaalManaAt = time.AddSeconds(11.5);
        SynchronizeAlzadaal();
    }

    private void ResetAlzadaal()
    {
        alzadaalSerial = 0; alzadaalTentacles.Clear(); alzadaalFountains.Clear(); alzadaalDrudges.Clear();
        alzadaalModels.Clear(); alzadaalStates.Clear(); alzadaalOrders.Clear(); alzadaalCones.Clear(); alzadaalRects.Clear();
        alzadaalWaves = 0; alzadaalConeHits = 0; alzadaalRectHits = 0; alzadaalVoid = null;
        alzadaalPattern = 0; alzadaalManaTarget = 0; alzadaalManaAt = default; alzadaalMana.Clear(); alzadaalTraps.Clear(); alzadaalBlades = 0;
        alzadaalSpinning.Clear();
    }
}
