using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsRobots => Definition.PrimaryOid == 0x2E8C;
    internal BozjaAoe?[] RobotOrders { get; } = new BozjaAoe?[8];
    internal List<uint>?[] RobotNumbers { get; } = new List<uint>?[8];
    private readonly Dictionary<ulong, BozjaMarchState> robotMarch = [];
    internal int RobotActiveMarches { get; private set; }
    internal float RobotRandomOdd { get; set; } = Random.Shared.Next(1, 51) * 2 - 1;
    private readonly List<float> robotRotations = [];
    private readonly List<(BozjaCast Cast, float Increment, int Remaining)> robotSequences = [];
    private readonly List<BozjaAoe> robotPublished = [];
    private float robotIncrement;
    private DateTime robotActivation;
    private int robotCasts;
    private int robotVersion;
    private int robotCount;
    internal int RobotNumCasts { get; private set; }
    internal string? RobotNavigationError { get; private set; }
    internal string? RobotHintError { get; private set; }
    internal static readonly Vector3 RobotCenter = new(104, 0, 237);
    internal static Vector3 RobotTower(uint number)
    {
        var (sin, cos) = Math.SinCos((number - 1) * (-90f * (MathF.PI / 180f)));
        return RobotCenter + new Vector3(-20f * (float)sin, 0, -20f * (float)cos);
    }

    private void ClearRobots()
    {
        Array.Clear(RobotOrders);
        Array.Clear(RobotNumbers);
        robotMarch.Clear();
        robotRotations.Clear();
        robotSequences.Clear();
        robotPublished.Clear();
        robotIncrement = 0;
        robotActivation = default;
        RobotNavigationError = RobotHintError = null;
        robotCasts = robotVersion = robotCount = RobotNumCasts = RobotActiveMarches = 0;
        if (IsRobots) RobotRandomOdd = Random.Shared.Next(1, 51) * 2 - 1;
    }

    private void RobotCreated(BozjaActor actor)
    {
        if (!Active || !IsRobots || !actor.NativeNpc || actor.Actor.Oid != 0x1EB032) return;
        Center = DungeonEncounter.QuantizedDungeonPosition(Center);
        Arena = new CircleGeometry(Center, 25);
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.RobotArena);
    }

    private void StartRobot(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.RobotTrain)
        {
            robotRotations.Add(spell.Rotation ?? 0);
            robotCasts = spell.ActionId >= 20683 ? 12 : 6;
            InitRobotTrain(actor, rule);
        }
        if (rule.Kind != BozjaRuleKind.RobotOrder) return;
        var divisor = spell.ActionId switch { 20677 => 3u, 20678 => 4u, 20679 => 5u, _ => 0u };
        for (var i = 0; i < 8; ++i) RobotNumbers[i] = [];
        foreach (var member in Party.Where(p => p.Slot is >= 0 and < 8).OrderBy(p => p.Slot))
        {
            if (Actors.GetValueOrDefault(member.EntityId) is not { } target) continue;
            var hp = target.Actor.MaxHp;
            if (hp > 9) continue;
            var numbers = RobotNumbers[member.Slot]!;
            var outsideSafe = false;
            var towers = new List<Vector3>();
            for (var j = 0u; j < 5u; ++j)
            {
                var value = hp + j;
                var divisible = divisor == 0 ? IsPrime(value) : value % divisor == 0;
                if (!outsideSafe && divisible || outsideSafe && !divisible)
                {
                    numbers.Add(j);
                    if (j == 0) outsideSafe = true;
                    else towers.Add(RobotTower(j));
                }
            }
            RobotOrders[member.Slot] = new(new(++serial, rule, actor, Center, 0, Activation(actor))
            { ShapeOverride = new BozjaOrderTemplate(Center, towers, !outsideSafe) }, false, SafeColor: !outsideSafe);
        }
    }

    private static bool IsPrime(uint value)
    {
        if (value < 2) return false;
        for (var divisor = 2u; divisor <= value / divisor; ++divisor)
            if (value % divisor == 0) return false;
        return true;
    }

    private void FinishRobot(BozjaActor actor, ReplayCast spell, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.RobotArena)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, 4.3d)));
        }
        if (rule.Kind == BozjaRuleKind.RobotOrder)
        {
            Array.Clear(RobotOrders);
            Array.Clear(RobotNumbers);
        }
    }

    private void RobotIcon(BozjaActor actor, uint icon)
    {
        if (!Active || !IsRobots) return;
        robotIncrement = icon switch { 235 => -60f * (MathF.PI / 180f), 236 => 60f * (MathF.PI / 180f), _ => 0 };
        robotActivation = Now.AddSeconds(8d);
        InitRobotTrain(actor, Definition.Rules.Single(r => r.Kind == BozjaRuleKind.RobotTrain));
    }

    private void InitRobotTrain(BozjaActor actor, BozjaRule rule)
    {
        if (robotRotations.Count != 2 || robotIncrement == 0) return;
        foreach (var rotation in robotRotations)
            robotSequences.Add((new(++serial, rule, actor, Center, rotation, robotActivation), robotIncrement, robotCasts));
        robotRotations.Clear();
        robotIncrement = 0;
        robotCasts = 0;
    }

    private void RobotEffect(uint action)
    {
        if (!IsRobots || action is not (20672 or 20673 or 20683 or 20684)) return;
        for (var i = robotSequences.Count - 1; i >= 0; --i)
        {
            ++RobotNumCasts;
            var s = robotSequences[i];
            if (s.Remaining <= 1) robotSequences.RemoveAt(i);
            else robotSequences[i] = (s.Cast with
            {
                Rotation = s.Cast.Rotation + s.Increment,
                Activation = Now.AddSeconds(1.2d),
            }, s.Increment, s.Remaining - 1);
        }
    }

    private void UpdateRobots()
    {
        if (robotVersion == RobotNumCasts && robotCount == robotSequences.Count) return;
        robotVersion = RobotNumCasts;
        robotCount = robotSequences.Count;
        PublishRotations(robotSequences, robotPublished, 1.2d);
    }

    private void RobotStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsRobots) return;
        var kind = status.Id switch { 1293 => 0, 1295 => 1, 1294 => 2, 1296 => 3, 1257 or 3629 => 4, _ => -1 };
        if (kind < 0) return;
        if (!robotMarch.TryGetValue(actor.Actor.Id, out var state))
            robotMarch.Add(actor.Actor.Id, state = new());
        state.Apply(kind, gain, status.ExpiresAt);
        if (kind == 4) RobotActiveMarches += gain ? 1 : -1;
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Direction)> RobotMovements(BozjaActor viewer)
        => Active && IsRobots && robotMarch.TryGetValue(viewer.Actor.Id, out var state)
            ? state.Movements(viewer.Actor.Position, viewer.Actor.Rotation, Now) : [];

    internal void PrepareRobotOutputs(BozjaActor viewer)
    {
        if (!IsRobots) return;
        RobotNavigationError = RobotHintError = null;
        var slot = ViewerSlot(viewer);
        var state = robotMarch.GetValueOrDefault(viewer.Actor.Id);
        var pendingMarch = state?.Pending.Count > 0;
        if (slot is < 0 or > 7)
        {
            if (pendingMarch) RobotNavigationError = $"OrderForcedMarch 队伍槽越界：slot={slot}";
            var moves = RobotMovements(viewer).ToArray();
            if (moves.Length > 0 && moves[^1].From != moves[^1].To)
                RobotHintError = $"OrderForcedMarch 落点队伍槽越界：slot={slot}";
            return;
        }
        var empty = RobotNumbers[slot] is { Count: 0 };
        if (pendingMarch && empty) RobotNavigationError = $"OrderForcedMarch 缺少数学候选：slot={slot} MaxHP={viewer.Actor.MaxHp}";
        if (RobotOrders[slot] != null && empty) RobotHintError = $"OrderTowers 旧区域缺少数学候选：slot={slot} MaxHP={viewer.Actor.MaxHp}";
    }

    private IEnumerable<BozjaAoe> RobotAoes(BozjaActor? viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.RobotArena)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false);
        if (rule.Kind == BozjaRuleKind.RobotOrder && viewer != null && ViewerSlot(viewer) is >= 0 and < 8
            && RobotOrders[ViewerSlot(viewer)] is { } order) yield return order;
        if (rule.Kind == BozjaRuleKind.RobotTrain)
            foreach (var aoe in robotPublished) yield return aoe;
    }

    private IEnumerable<BozjaHint> RobotHints(BozjaActor viewer, BozjaRule rule)
    {
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or > 7) yield break;
        if (rule.Kind == BozjaRuleKind.RobotOrder && RobotOrders[slot] is { } order)
        {
            var outsideSafe = RobotNumbers[slot]![0] == 0;
            var inside = order.Cast.Contains(viewer.Actor.Position);
            yield return new(outsideSafe ? "Avoid marked towers!" : "Move into a marked tower!", outsideSafe ? inside : !inside);
        }
        if (rule.Kind != BozjaRuleKind.RobotMarch) yield break;
        var moves = RobotMovements(viewer).ToArray();
        if (moves.Length == 0 || moves[^1].From == moves[^1].To) yield break;
        var unsafeDestination = RobotOrders[slot] is { } math
            ? math.Cast.Contains(moves[^1].To) == (RobotNumbers[slot]![0] == 0)
            : !Arena.Contains(moves[^1].To, 0);
        if (unsafeDestination) yield return new("Aim for safe spot!");
    }

    internal (float Center, float HalfWidth, DateTime Activation)? RobotDirection(BozjaActor viewer)
    {
        var slot = ViewerSlot(viewer);
        if (!Active || !IsRobots || slot is < 0 or > 7 || RobotNumbers[slot] is not { } numbers
            || !robotMarch.TryGetValue(viewer.Actor.Id, out var state) || state.Pending.Count == 0) return null;
        var first = state.Pending[0];
        var direction = numbers[0] == 0 ? 45f * (MathF.PI / 180f) * RobotRandomOdd
            : (numbers[0] - 1) * (-90f * (MathF.PI / 180f)) - first.Direction;
        return (direction, 175f * (MathF.PI / 180f), first.Activation);
    }

    private IEnumerable<BozjaZone> RobotNavigation(BozjaActor viewer, BozjaRule rule)
    {
        foreach (var aoe in RobotAoes(viewer, rule))
        {
            var cast = aoe.Cast;
            yield return new($"robots/aoe/{cast.Generation}", rule.Component, cast.Activation,
                cast.Shape, cast.Origin, cast.Rotation);
        }
        if (rule.Kind == BozjaRuleKind.RobotTrain && robotSequences.Count > 0)
            yield return new("robots/train/near", rule.Component, robotSequences[0].Cast.Activation,
                new CircleTemplate(3), RobotCenter, 0, Goal: true);
        if (rule.Kind != BozjaRuleKind.RobotMarch || RobotDirection(viewer) is not { } direction) yield break;
        var numbers = RobotNumbers[ViewerSlot(viewer)]!;
        var angle = numbers[0] == 0 ? 45f * (MathF.PI / 180f) * RobotRandomOdd
            : 180f * (MathF.PI / 180f) + (numbers[0] - 1) * (-90f * (MathF.PI / 180f));
        yield return new("robots/march/spot", rule.Component, direction.Activation,
            new FanTemplate(7, 2 * (22.5f * (MathF.PI / 180f))), RobotCenter, angle, Goal: true);
    }
}
