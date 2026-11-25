using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTactical => NoviceTacticalCatalog.IsTerritory(territory);
    private readonly List<TacticalAoe> tacticalRings = [];
    private readonly List<TacticalAoe> tacticalSurges = [];
    private readonly List<TacticalAoe> tacticalFans = [];
    private readonly List<TacticalAoe> tacticalTowers = [];
    private readonly List<TacticalMark> tacticalMarks = [];
    private readonly List<TacticalChaser> tacticalChasers = [];
    private readonly Dictionary<uint, List<(DungeonActor Actor, bool Removed)>> tacticalChaserTargets = [];
    private readonly List<TacticalKnockback> tacticalKnockbacks = [];
    private readonly List<TacticalTether> tacticalTethers = [];
    private readonly List<(DungeonActor Actor, bool Removed)> tacticalIntercepts = [];
    private readonly HashSet<(ulong Source, uint Action, uint Sequence, ulong Target, DateTime At)> tacticalEffects = [];
    private Vector3? tacticalReadyPosition;
    private float tacticalReadyRadius = 1;
    private bool tacticalInteractReady;
    private bool tacticalTrialComplete;
    private DateTime? tacticalTetherActivation;
    private long tacticalId;
    private DungeonActor? tacticalBaitSource;
    private bool tacticalBaitSourceRemoved;
    private readonly Dictionary<(ulong Source, uint Type), ulong> tacticalLiveTethers = [];
    private readonly Dictionary<(ulong Source, uint Type), ulong> tacticalObservedTethers = [];
    private readonly HashSet<(ulong Source, uint Type, ulong Target, bool Present)> tacticalRawTetherReceipts = [];

    internal ulong TacticalInteractionTarget => IsTactical && Active != null && (tacticalInteractReady || tacticalTrialComplete)
        && NoviceActors.FirstOrDefault(a => a.Oid == 0x100B76) is { Targetable: true } npc ? npc.Id : 0;

    private void TacticalMap(uint index, uint state)
    {
        if (!IsTactical || Active == null) return;
        if (state == 0x00080004)
        {
            tacticalReadyPosition = null;
            tacticalReadyRadius = 1;
            return;
        }
        if (state != 0x00020001) return;
        Vector2? point = territory switch
        {
            1127 => index switch
            {
                0 => new(-3.3f, 0), 1 => new(-9.9f, 9.5f), 2 => new(-7.7f, -.1f),
                3 => new(-14.8f, 0), 4 => new(-3.2f, -.1f), _ => null,
            },
            1128 => index switch
            {
                0 => new(-10.070f, 10.198f), 1 => new(-2.152f, 2.652f), 2 => new(-3.050f, .260f),
                3 => new(-10.248f, 9.636f), 4 => new(-11.719f, 2.587f), 5 => new(-2.815f, 2.137f),
                0x10 => new(-1.511f, 4.501f), 0x18 => new(1.999f, 5.020f), _ => null,
            },
            _ => index switch
            {
                0 => new(-5, 0), 1 => new(-4.709f, -7.366f), 2 => new(-5.637f, -5.024f),
                3 => new(-12.223f, .224f), 4 => new(-4.729f, .106f), _ => null,
            },
        };
        tacticalReadyPosition = point is { } p ? new(p.X, Arena?.Position.Y ?? 0, p.Y) : null;
        if (territory == 1128 && index == 0x18) tacticalReadyRadius = 4;
    }

    private void TacticalDirector(uint update, uint p1, uint p2, uint p3, uint p4)
    {
        if (!IsTactical || Active == null || p3 != 0 || p4 != 0) return;
        var readyParam = territory == 1128 ? 5u : 4u;
        if (update == 0x80000015 && p1 == readyParam && p2 == 0) tacticalInteractReady = true;
        if (update == 0x80000016 && p1 == readyParam + 1 && p2 == 1) tacticalInteractReady = false;
        if (update == 1 && p1 == 0 && p2 == 0) tacticalTrialComplete = true;
    }

    private void TacticalCast(DungeonPendingCast cast, bool correction, DungeonPendingCast? previous)
    {
        if (!IsTactical) return;
        var action = cast.Rule.Action;
        List<TacticalAoe>? list = null;
        GeometryTemplate? shape = null;
        var activation = cast.FinishAt;
        if (action is 40694 or 40760 or 40693 or 40759)
        {
            list = tacticalRings;
            shape = action is 40694 or 40760 ? new DonutTemplate(10, 20) : new CircleTemplate(10);
            var delay = action switch { 40694 => 7, 40760 => 13.5, 40693 => 11, _ => 10 };
            var old = correction ? list.FirstOrDefault(a => ReferenceEquals(a.Cast, previous)) : null;
            activation = (old?.PreparedAt ?? _now).AddSeconds(delay);
        }
        if (action is 40732 or 40733) { list = tacticalSurges; shape = new FanTemplate(20, MathF.PI); }
        if (action is 40752 or 40753) { list = tacticalFans; shape = new FanTemplate(60, 46 * MathF.PI / 180); }
        if (action is 40716 or 40727) { list = tacticalTowers; shape = new CircleTemplate(4); }
        if (list != null)
        {
            var index = correction ? list.FindIndex(a => ReferenceEquals(a.Cast, previous)) : -1;
            if (!correction || index >= 0)
            {
                var old = index >= 0 ? list[index] : null;
                var next = new TacticalAoe(old?.Id ?? ++tacticalId, cast, shape!, cast.Location, cast.Rotation,
                    activation, old?.PreparedAt ?? _now);
                if (index >= 0) list[index] = next;
                else list.Add(next);
                if (!correction && (list == tacticalRings || list == tacticalFans && list.Count >= 4))
                    DungeonAoeOrder.Sort(list, a => a.Activation);
            }
        }
        if (action is 40706 or 40722 or 40709 or 40726)
        {
            var index = correction ? tacticalKnockbacks.FindIndex(a => ReferenceEquals(a.Cast, previous)) : -1;
            if (!correction || index >= 0)
            {
                var kb = new TacticalKnockback(cast, action switch { 40709 => 10, 40726 => 20, _ => 15 },
                    action is 40706 or 40722 ? 25 : null);
                if (index >= 0) tacticalKnockbacks[index] = kb;
                else tacticalKnockbacks.Add(kb);
            }
        }
        if (action is not (40689 or 40702)) return;
        var chaserIndex = correction ? tacticalChasers.FindIndex(a => ReferenceEquals(a.Cast, previous)) : -1;
        if (correction)
        {
            if (chaserIndex >= 0)
            {
                var old = tacticalChasers[chaserIndex];
                tacticalChasers[chaserIndex] = old with { Cast = cast,
                    Previous = old.Remaining == 5 ? cast.Location : old.Previous,
                    Activation = old.Remaining == 5 ? cast.FinishAt : old.Activation };
            }
            return;
        }
        if (!tacticalChaserTargets.TryGetValue(action, out var targets) || targets.Count == 0) return;
        var target = targets.OrderBy(a => NativeEnemyDistanceSquared(a.Actor.Position, cast.Location)).First();
        targets.Remove(target);
        tacticalChasers.Add(new(++tacticalId, cast, target.Actor, cast.Location, cast.FinishAt) { Removed = target.Removed });
    }

    private void TacticalFinish(DungeonPendingCast cast)
    {
        if (!IsTactical) return;
        foreach (var list in new[] { tacticalSurges, tacticalFans })
        {
            var index = list.FindIndex(a => a.Cast.Source == cast.Source);
            if (index >= 0) list.RemoveAt(index);
        }
        if (cast.Rule.Action is 40716 or 40727)
        {
            var index = tacticalTowers.FindIndex(a => a.Cast.Source == cast.Source && a.Cast.Rule.Action == cast.Rule.Action);
            if (index >= 0) tacticalTowers.RemoveAt(index);
        }
        if (cast.Rule.Action is 40706 or 40722 or 40709 or 40726)
        {
            var index = tacticalKnockbacks.FindIndex(a => a.Cast.Source == cast.Source && a.Cast.Rule.Action == cast.Rule.Action);
            if (index >= 0) tacticalKnockbacks.RemoveAt(index);
        }
    }

    private void TacticalIcon(ulong target, uint icon, DateTime at, ulong source)
    {
        if (!IsTactical || Active == null || !_actors.TryGetValue(source, out var actor)) return;
        if (icon == 197)
        {
            foreach (var action in new uint[] { 40689, 40702 })
            {
                if (!tacticalChaserTargets.TryGetValue(action, out var targets)) tacticalChaserTargets[action] = targets = [];
                targets.Add((actor, false));
            }
            return;
        }
        if (icon is not (139 or 375 or 318 or 317 or 412)) return;
        if (icon == 412)
        {
            actor = _actors.GetValueOrDefault(target) ?? actor;
            tacticalBaitSource ??= _actors.GetValueOrDefault(PrimaryId);
        }
        var delay = icon switch { 412 => 5.1, 318 or 317 => 0, _ => 5 };
        tacticalMarks.Add(new(++tacticalId, icon, actor, at.AddSeconds(delay)));
    }

    internal bool UsesTacticalNativeEffect(uint action) => IsTactical && NoviceTacticalCatalog.NativeEffect(action);

    private void TacticalEffect(ulong source, uint action, uint sequence, ulong target, DateTime at, Vector3? targetPosition)
    {
        if (!UsesTacticalNativeEffect(action) || Active == null
            || !tacticalEffects.Add((source, action, sequence, target, sequence == 0 ? at : default))) return;
        if (action is 40680 or 40692 or 40678) tacticalMarks.RemoveAll(m => m.Icon == 139);
        if (action == 40692)
        {
            var index = tacticalMarks.FindIndex(m => m.Icon == 375 && m.Target.Id == target);
            if (index < 0) index = tacticalMarks.FindIndex(m => m.Icon == 375);
            if (index >= 0) tacticalMarks.RemoveAt(index);
        }
        if (action is 40682 or 40695 or 40683 or 40696)
            tacticalMarks.RemoveAll(m => m.Icon is 318 or 317 && m.Target.Id == target);
        if (action is 40685 or 40698 or 40686 or 40699)
            tacticalMarks.RemoveAll(m => m.Icon == 412 && m.Target.Id == target);
        if (action is 40694 or 40693 or 40760 or 40759 && tacticalRings.Count > 0) tacticalRings.RemoveAt(0);
        if (action is not (40689 or 40690 or 40702 or 40703)) return;
        var group = action is 40689 or 40690 ? 40689u : 40702u;
        var position = target == source && _actors.GetValueOrDefault(source) is { } caster
            ? QuantizedDungeonPosition(caster.Position) : _actors.GetValueOrDefault(target)?.Position ?? targetPosition;
        if (position == null) return;
        var chaser = tacticalChasers.Where(c => c.Cast.Rule.Action == group)
            .OrderBy(c => NativeEnemyDistanceSquared(TacticalChaserPosition(c), position.Value)).FirstOrDefault();
        if (chaser == null) return;
        var indexChaser = tacticalChasers.IndexOf(chaser);
        if (chaser.Remaining == 1) tacticalChasers.RemoveAt(indexChaser);
        else tacticalChasers[indexChaser] = chaser with { Previous = position.Value, MoveDistance = 5,
            Remaining = chaser.Remaining - 1, Activation = _now.AddSeconds(2.5) };
    }

    private void TacticalObserve()
    {
        if (!IsTactical || Active == null) return;
        DungeonActor Refresh(DungeonActor actor, ref bool removed)
        {
            if (!removed && _actors.TryGetValue(actor.Id, out var current) && current.Oid == actor.Oid) return current;
            removed = true;
            return actor;
        }
        for (var i = tacticalMarks.Count - 1; i >= 0; --i)
        {
            var mark = tacticalMarks[i];
            var removed = mark.Removed;
            mark = mark with { Target = Refresh(mark.Target, ref removed), Removed = removed };
            if (mark.Icon is 139 or 375 or 412 && mark.Target.Dead) tacticalMarks.RemoveAt(i);
            else tacticalMarks[i] = mark;
        }
        for (var i = tacticalChasers.Count - 1; i >= 0; --i)
        {
            var chaser = tacticalChasers[i];
            var removed = chaser.Removed;
            chaser = chaser with { Target = Refresh(chaser.Target, ref removed), Removed = removed };
            if (chaser.Remaining < 5 && (removed || chaser.Target.Dead)) tacticalChasers.RemoveAt(i);
            else tacticalChasers[i] = chaser;
        }
        foreach (var targets in tacticalChaserTargets.Values)
            for (var i = 0; i < targets.Count; ++i)
            {
                var (actor, removed) = targets[i];
                targets[i] = (Refresh(actor, ref removed), removed);
            }
        if (tacticalBaitSource != null)
            tacticalBaitSource = Refresh(tacticalBaitSource, ref tacticalBaitSourceRemoved);
        for (var i = 0; i < tacticalTethers.Count; ++i)
        {
            var tether = tacticalTethers[i];
            var sourceRemoved = tether.SourceRemoved;
            var targetRemoved = tether.TargetRemoved;
            var sourceActor = Refresh(tether.Source, ref sourceRemoved);
            var targetActor = Refresh(tether.Target, ref targetRemoved);
            tacticalTethers[i] = tether with { Source = sourceActor, Target = targetActor,
                SourceRemoved = sourceRemoved, TargetRemoved = targetRemoved };
        }
        for (var i = 0; i < tacticalIntercepts.Count; ++i)
        {
            var (actor, removed) = tacticalIntercepts[i];
            tacticalIntercepts[i] = (Refresh(actor, ref removed), removed);
        }
        var observed = _actors.Values.SelectMany(a => (a.Tethers ?? new Dictionary<uint, ulong>())
            .Where(t => t.Key is 57 or 84 && t.Value != 0).Select(t => (Key: (Source: a.Id, Type: t.Key), t.Value)))
            .ToDictionary(t => t.Key, t => t.Value);
        foreach (var old in tacticalObservedTethers.ToArray())
            if (observed.GetValueOrDefault(old.Key) != old.Value)
                TacticalTetherChanged(old.Key.Source, old.Value, old.Key.Type, false, _now, observed: true);
        foreach (var pair in observed)
            if (tacticalObservedTethers.GetValueOrDefault(pair.Key) != pair.Value)
                TacticalTetherChanged(pair.Key.Source, pair.Value, pair.Key.Type, true, _now, observed: true);
        tacticalObservedTethers.Clear();
        foreach (var pair in observed) tacticalObservedTethers[pair.Key] = pair.Value;
    }

    private void TacticalTetherChanged(ulong source, ulong target, uint type, bool present, DateTime now, bool observed = false)
    {
        if (!IsTactical || Active == null || type is not (57 or 84)) return;
        var key = (source, type);
        var receipt = (source, type, target, present);
        if (!observed && tacticalRawTetherReceipts.Remove(receipt)) return;
        if (observed && !present && tacticalLiveTethers.GetValueOrDefault(key) != target) return;
        if (present)
        {
            if (tacticalLiveTethers.TryGetValue(key, out var old))
            {
                if (observed && old == target) return;
            }
            tacticalLiveTethers[key] = target;
        }
        else tacticalLiveTethers.Remove(key);
        if (observed) tacticalRawTetherReceipts.Add(receipt);
        if (type == 84)
        {
            if (_actors.GetValueOrDefault(target) is not { Oid: 0x4640 } bomb) return;
            if (present) tacticalIntercepts.Add((bomb, false));
            else
            {
                var index = tacticalIntercepts.FindIndex(b => b.Actor.Id == bomb.Id && !b.Removed);
                if (index >= 0) tacticalIntercepts.RemoveAt(index);
            }
            return;
        }
        var from = _actors.GetValueOrDefault(source);
        var to = _actors.GetValueOrDefault(target);
        if (!present && from == null)
        {
            var previous = tacticalTethers.FirstOrDefault(t => t.Source.Id == source || t.Target.Id == source);
            from = previous?.Source.Id == source ? previous.Source : previous?.Target;
        }
        if (from == null || to == null) return;
        var (enemy, player) = from.Player || from.Buddy ? (to, from) : (from, to);
        if (!player.Player && !player.Buddy || enemy.Player || enemy.Buddy) return;
        if (present)
        {
            tacticalTetherActivation ??= now;
            tacticalTethers.Add(new(++tacticalId, enemy, player, tacticalTetherActivation.Value));
        }
        else
        {
            var index = tacticalTethers.FindIndex(t => t.Source.Id == enemy.Id && t.Target.Id == player.Id);
            if (index >= 0) tacticalTethers.RemoveAt(index);
        }
    }

    private void TacticalRemoved(ulong id)
    {
        if (!IsTactical) return;
        foreach (var pair in tacticalLiveTethers.Where(p => p.Key.Source == id).ToArray())
            TacticalTetherChanged(id, pair.Value, pair.Key.Type, false, _now);
        tacticalObservedTethers.Remove((id, 57));
        tacticalObservedTethers.Remove((id, 84));
        tacticalBaitSourceRemoved |= tacticalBaitSource?.Id == id;
        foreach (var targets in tacticalChaserTargets.Values)
            for (var i = 0; i < targets.Count; ++i)
                if (targets[i].Actor.Id == id) targets[i] = (targets[i].Actor, true);
        for (var i = 0; i < tacticalMarks.Count; ++i)
            if (tacticalMarks[i].Target.Id == id) tacticalMarks[i] = tacticalMarks[i] with { Removed = true };
        for (var i = 0; i < tacticalChasers.Count; ++i)
            if (tacticalChasers[i].Target.Id == id) tacticalChasers[i] = tacticalChasers[i] with { Removed = true };
        for (var i = 0; i < tacticalTethers.Count; ++i)
        {
            var tether = tacticalTethers[i];
            tacticalTethers[i] = tether with { SourceRemoved = tether.SourceRemoved || tether.Source.Id == id,
                TargetRemoved = tether.TargetRemoved || tether.Target.Id == id };
        }
        for (var i = 0; i < tacticalIntercepts.Count; ++i)
            if (tacticalIntercepts[i].Actor.Id == id) tacticalIntercepts[i] = (tacticalIntercepts[i].Actor, true);
    }

    private void ResetTactical()
    {
        tacticalRings.Clear();
        tacticalSurges.Clear();
        tacticalFans.Clear();
        tacticalTowers.Clear();
        tacticalMarks.Clear();
        tacticalChasers.Clear();
        tacticalChaserTargets.Clear();
        tacticalLiveTethers.Clear();
        tacticalObservedTethers.Clear();
        tacticalRawTetherReceipts.Clear();
        tacticalBaitSource = null;
        tacticalBaitSourceRemoved = false;
        tacticalKnockbacks.Clear();
        tacticalTethers.Clear();
        tacticalIntercepts.Clear();
        tacticalEffects.Clear();
        tacticalReadyPosition = null;
        tacticalReadyRadius = 1;
        tacticalInteractReady = false;
        tacticalTrialComplete = false;
        tacticalTetherActivation = null;
        tacticalId = 0;
    }
}
