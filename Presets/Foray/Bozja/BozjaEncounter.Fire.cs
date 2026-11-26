using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaCast> Countdowns { get; } = [];
    internal List<BozjaCast> PublishedCountdowns { get; } = [];
    internal int CountdownCasts { get; private set; }
    internal string? FireNavigationError { get; private set; }
    internal string? FireDrawingError { get; private set; }
    private bool IsFire => Definition.PrimaryOid == 0x2E75;
    private readonly Dictionary<uint, List<BozjaActor>> fireEnemies = [];
    private readonly List<BozjaActor> fireParticipants = [];
    private BozjaActor? fireMater;
    private Vector3? fireMaterPosition;
    private int countdownLastCount;
    private int countdownLastVersion;
    private Vector3? fireFurthest;
    private bool fireMissingMater;
    private static readonly GeometryTemplate[] countdownShapes =
        [new DonutTemplate(11, 18), new DonutTemplate(5, 12), new CircleTemplate(6)];
    private static readonly uint[] fireTrashOids = [0x2E75, 0x2E72, 0x2E73, 0x2E77];

    private List<BozjaActor> FireEnemies(uint oid)
    {
        if (!fireEnemies.TryGetValue(oid, out var list))
            fireEnemies[oid] = list = Actors.Values.Where(a => a.Actor.Oid == oid).ToList();
        return list;
    }

    private void FireCreated(BozjaActor actor)
    {
        if (fireEnemies.TryGetValue(actor.Actor.Oid, out var list)) list.Add(actor);
    }

    private void FireDestroyed(BozjaActor actor)
    {
        if (fireEnemies.TryGetValue(actor.Actor.Oid, out var list)) list.Remove(actor);
        if (actor.Actor.Oid == 0x2E78) Countdowns.Clear();
    }

    private void ClearFire()
    {
        Countdowns.Clear();
        PublishedCountdowns.Clear();
        fireEnemies.Clear();
        fireParticipants.Clear();
        fireMater = null;
        fireMaterPosition = fireFurthest = null;
        FireNavigationError = FireDrawingError = null;
        fireMissingMater = false;
        CountdownCasts = countdownLastCount = countdownLastVersion = 0;
    }

    private bool FireComplete()
        => fireMater?.Actor.Dead == true
            || fireTrashOids.All(oid => FireEnemies(oid).All(a => a.Destroyed || a.Actor.Dead));

    internal void ObserveFireDrawing()
    {
        if (!IsFire) return;
        foreach (var oid in fireTrashOids) _ = FireEnemies(oid);
    }

    private void UpdateFire()
    {
        fireMater ??= FireEnemies(0x2E74).FirstOrDefault();
        if (countdownLastCount == Countdowns.Count && countdownLastVersion == CountdownCasts) return;
        countdownLastCount = Countdowns.Count;
        countdownLastVersion = CountdownCasts;
        PublishedCountdowns.Clear();
        PublishedCountdowns.AddRange(Countdowns);
    }

    private void StartFire(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.Countdown)
        {
            if (actor.Cast!.ActionId == 20665)
                Countdowns.Add(new(++serial, rule, actor, actor.Cast.Location, 0, Activation(actor))
                    { ShapeOverride = countdownShapes[0] });
        }
        else if (rule.Kind == BozjaRuleKind.TooHot)
            Casts.Add(new(++serial, rule, actor, actor.Cast!.Location, 0, Activation(actor)));
    }

    private void FinishCountdown(ReplayCast spell)
    {
        if (Countdowns.Count == 0) return;
        var order = spell.ActionId switch { 20665 => 0, 20664 => 1, 20663 => 2, _ => -1 };
        if (order < 0) return;
        ++CountdownCasts;
        var index = Countdowns.FindIndex(s => s.Action == order
            && MathF.Abs(s.Origin.X - spell.Location.X) <= 1 && MathF.Abs(s.Origin.Z - spell.Location.Z) <= 1);
        if (index < 0) return;
        if (order == 2) Countdowns.RemoveAt(index);
        else Countdowns[index] = Countdowns[index] with
        {
            Action = (uint)(order + 1),
            Activation = Now.AddSeconds(4d),
            ShapeOverride = countdownShapes[order + 1],
        };
    }

    private void FireStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!gain || status.Id != 2056
            || Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.BombStack) is not { } rule) return;
        Casts.Add(new(++serial, rule, actor, actor.Actor.Position, 0, Now.AddSeconds(16d)) { Target = actor });
    }

    private void FireEffect(BozjaActor actor, uint action)
    {
        if (action is not (20661 or 20662)) return;
        var index = Casts.FindIndex(s => s.Rule.Kind == BozjaRuleKind.BombStack && ReferenceEquals(s.Target, actor));
        if (index >= 0) Casts.RemoveAt(index);
    }

    // A missing Mater and the source participant-index bug are failed output
    // calculations, not legitimate zero-AOE states. Keep history and retry next
    // frame; match the old host's whole-navigation withdrawal on those frames.
    internal bool PrepareFireNavigation(BozjaActor viewer)
    {
        if (!IsFire) return true;
        FireNavigationError = null;
        var count = fireParticipants.Count;
        if (count == 0)
            fireParticipants.AddRange(Actors.Values.Where(a => a.Actor.Oid == 0 && DistanceSquared(a.Actor.Position, Center) <= 400));
        var stacks = Casts.Where(c => c.Rule.Kind == BozjaRuleKind.BombStack).ToArray();
        for (var i = 0; i < stacks.Length; ++i)
        {
            var stack = stacks[i];
            if (DistanceSquared(stack.Target!.Actor.Position, StackCenter(stack)) > 36) continue;
            if (viewer.Actor.Hp > 30000) break;
            if (count == 0) continue;
            if (i >= count)
            {
                FireNavigationError = $"CantTouchThis参与者索引异常：viewer={viewer.Actor.Id:X} HP={viewer.Actor.Hp} stack={i}/{stacks.Length} participants={count}";
                return false;
            }
            // Source deliberately repeats participants[i], rather than [j].
            // Its weak 0/5 goal remains diagnostic-only under the bridge's 100
            // conversion threshold; do not submit a new movement goal here.
            if (count > 1 && DistanceSquared(fireParticipants[i].Actor.Position, stack.Target.Actor.Position) <= 36) break;
        }
        _ = FireHotViews().ToArray();
        if (fireMissingMater)
            FireNavigationError = FireDrawingError;
        return FireNavigationError == null;
    }

    internal IEnumerable<BozjaCast> FireHotViews()
    {
        var list = Casts.Where(c => c.Rule.Kind == BozjaRuleKind.TooHot).ToArray();
        FireDrawingError = null;
        fireMissingMater = false;
        fireFurthest = null;
        if (list.Length != 9)
        {
            foreach (var cast in list) yield return cast;
            yield break;
        }
        fireMaterPosition ??= FireEnemies(0x2E74).FirstOrDefault()?.Actor.Position;
        if (fireMaterPosition is not { } mater)
        {
            FireDrawingError = "TooHotToHandle缺少Mater，保留九项并等待恢复";
            fireMissingMater = true;
            yield break;
        }
        var distance = float.MinValue;
        foreach (var cast in list)
            if (DistanceSquared(cast.Origin, mater) > distance)
            {
                distance = DistanceSquared(cast.Origin, mater);
                fireFurthest = cast.Origin;
            }
        var filtered = list.Where(c => c.Origin.X != fireFurthest!.Value.X || c.Origin.Z != fireFurthest.Value.Z).ToArray();
        if (filtered.Length < 8)
            FireDrawingError = $"TooHotToHandle重复最远坐标：保留{filtered.Length}个有效圆，源默认尾项绘图失败";
        foreach (var cast in filtered) yield return cast;
    }

    internal IEnumerable<BozjaCast> FireCleaves()
    {
        if (!Active || !IsFire) yield break;
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Cleave);
        foreach (var source in FireEnemies(Definition.PrimaryOid))
        {
            if (source.Actor.Dead || !source.Actor.Targetable
                || Actors.GetValueOrDefault(source.Actor.TargetId) is not { } target) continue;
            var delta = target.Actor.Position - source.Actor.Position;
            yield return new(-source.BirthOrder, rule, source, source.Actor.Position,
                MathF.Atan2(delta.X, delta.Z), default) { Target = target };
        }
    }

    private IEnumerable<BozjaZone> FireCleaveNavigation(BozjaActor viewer)
    {
        foreach (var cleave in FireCleaves())
        {
            var origin = DungeonEncounter.QuantizedDungeonPosition(cleave.Origin);
            if (!ReferenceEquals(cleave.Target, viewer))
                yield return new($"cleave/{cleave.Generation}/avoid", cleave.Rule.Component, default,
                    cleave.Shape, origin, cleave.Rotation);
            else
                foreach (var actor in Members().Where(a => !ReferenceEquals(a, viewer)))
                {
                    var delta = actor.Actor.Position - cleave.Origin;
                    yield return new($"cleave/{cleave.Generation}/party/{actor.Actor.Id}", cleave.Rule.Component, default,
                        new FanTemplate(100, 2 * (60f * (MathF.PI / 180f))), origin, MathF.Atan2(delta.X, delta.Z));
                }
        }
    }

    private int? FireTargetPriority(ReplayActorState actor)
    {
        if (!IsFire || !Casts.Any(c => c.Rule.Kind == BozjaRuleKind.TooHot)) return null;
        _ = FireHotViews().ToArray();
        if (fireFurthest is not { } position) return -1;
        return MathF.Abs(actor.Position.X - position.X) <= 1 && MathF.Abs(actor.Position.Z - position.Z) <= 1 ? 1 : -1;
    }
}
