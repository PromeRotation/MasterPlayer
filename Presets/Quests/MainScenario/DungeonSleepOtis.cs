using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsSleep => territory == MainScenarioQuestCatalog.SLEEP_DISTURBED;
    private bool IsOtis => territory == MainScenarioQuestCatalog.OTIS_OATHBROKEN;
    private readonly HashSet<ulong> sleepOtisCompleted = [];
    private readonly List<DungeonPendingCast> otisCircles = [];
    private readonly List<DungeonPendingCast> otisRushes = [];
    private readonly List<SleepBeam> sleepBeams = [];
    private readonly Dictionary<uint, DateTime> sleepBeamActivations = [];
    private readonly Dictionary<(ulong Source, uint Type), ulong> sleepLiveTethers = [];
    private readonly Dictionary<(ulong Source, uint Type), ulong> sleepObservedTethers = [];
    private readonly HashSet<(ulong Source, uint Type, ulong Target, bool Present)> sleepRawReceipts = [];
    private readonly Dictionary<ulong, DungeonActor> sleepTrackedActors = [];
    private ulong sleepBeamId;

    private bool SleepOtisPull(DungeonActor actor) => !sleepOtisCompleted.Contains(actor.Id) && actor.Hp != 0
        && (IsSleep ? actor.Targetable : _actors.GetValueOrDefault(PlayerId)?.InCombat == true);

    private void SleepOtisCast(DungeonPendingCast cast, DungeonPendingCast? previous)
    {
        if (!IsOtis) return;
        var group = cast.Rule.Action switch
        {
            38197 or 38205 => otisCircles,
            38209 => otisRushes,
            _ => null,
        };
        if (group == null) return;
        if (previous == null)
        {
            group.Add(cast);
            if (cast.Rule.Action == 38209 ? group.Count == 99 : group.Count >= 99)
                DungeonAoeOrder.Sort(group, c => c.FinishAt);
        }
        else
        {
            // A corrected packet cannot resurrect a record already consumed by another action's Finish.
            var index = group.FindIndex(c => ReferenceEquals(c, previous));
            if (index >= 0) group[index] = cast;
        }
    }

    private void SleepOtisFinish(DungeonPendingCast cast)
    {
        if (!IsOtis) return;
        foreach (var group in new[] { otisCircles, otisRushes })
        {
            var index = group.FindIndex(c => c.Source == cast.Source);
            if (index >= 0) group.RemoveAt(index);
        }
    }

    private void SleepTether(ulong source, ulong target, uint type, bool present, DateTime now, bool observed = false)
    {
        if (!IsSleep || Active == null || type is not (1 or 84)) return;
        var key = (source, type);
        var receipt = (source, type, target, present);
        if (!observed && sleepRawReceipts.Remove(receipt)) return;
        if (observed && !present && sleepLiveTethers.GetValueOrDefault(key) != target) return;
        if (!observed) sleepRawReceipts.RemoveWhere(r => r.Source == source);
        if (present)
        {
            if (observed && sleepLiveTethers.GetValueOrDefault(key) == target) return;
            // OpTether replaces the actor's single tether, including an identical repeated value.
            foreach (var old in sleepLiveTethers.Where(p => p.Key.Source == source).ToArray())
            {
                SleepUntether(source, old.Value, old.Key.Type);
                sleepLiveTethers.Remove(old.Key);
            }
            sleepLiveTethers[key] = target;
        }
        else
        {
            sleepLiveTethers.Remove(key);
            SleepUntether(source, target, type);
        }
        if (observed) sleepRawReceipts.Add(receipt);
        if (!present) return;
        var from = _actors.GetValueOrDefault(source);
        var to = _actors.GetValueOrDefault(target);
        if (from == null || to == null) return;
        var (enemy, player) = from.Player || from.Buddy ? (to, from) : (from, to);
        if (!player.Player && !player.Buddy || enemy.Player || enemy.Buddy) return;
        if (!sleepBeamActivations.ContainsKey(type)) sleepBeamActivations[type] = now;
        sleepBeams.Add(new(++sleepBeamId, type, source, target, enemy, player, sleepBeamActivations[type]));
    }

    private void SleepUntether(ulong source, ulong target, uint type, IReadOnlyDictionary<ulong, DungeonActor>? actors = null)
    {
        actors ??= _actors;
        var from = actors.GetValueOrDefault(source);
        var to = actors.GetValueOrDefault(target);
        var old = sleepBeams.FirstOrDefault(b => b.Type == type && b.RawSource == source && b.RawTarget == target);
        from ??= old?.Source.Id == source ? old.Source : old?.Target;
        // Upstream cannot resolve the incoming target once that actor has been removed.
        if (from == null || to == null) return;
        var (enemy, player) = from.Player || from.Buddy ? (to, from) : (from, to);
        if (!player.Player && !player.Buddy || enemy.Player || enemy.Buddy) return;
        var index = sleepBeams.FindIndex(b => b.Type == type && b.Source.Id == enemy.Id && b.Target.Id == player.Id);
        if (index >= 0) sleepBeams.RemoveAt(index);
    }

    private void SleepSynchronizeActors()
    {
        if (!IsSleep) return;
        // Feed destroys missing actors in persistent dictionary order before updating any survivors.
        foreach (var id in sleepTrackedActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            SleepOtisRemoved(id, sleepTrackedActors);
            sleepTrackedActors.Remove(id);
        }
        foreach (var actor in _actors.Values) sleepTrackedActors[actor.Id] = actor;
    }

    private void SleepOtisObserve()
    {
        if (!IsSleep || Active == null) return;
        foreach (var id in sleepLiveTethers.Keys.Select(k => k.Source).Distinct()
            .Where(id => !_actors.ContainsKey(id)).ToArray())
            SleepOtisRemoved(id);
        for (var i = 0; i < sleepBeams.Count; ++i)
        {
            var b = sleepBeams[i];
            var source = !b.SourceRemoved ? _actors.GetValueOrDefault(b.Source.Id) : null;
            var target = !b.TargetRemoved ? _actors.GetValueOrDefault(b.Target.Id) : null;
            sleepBeams[i] = b with { Source = source ?? b.Source, Target = target ?? b.Target,
                SourceRemoved = source == null, TargetRemoved = target == null };
        }
        var observed = _actors.Values.SelectMany(a => (a.Tethers ?? new Dictionary<uint, ulong>())
            .Where(t => t.Key is 1 or 84 && t.Value != 0).Select(t => (Key: (Source: a.Id, Type: t.Key), t.Value)))
            .ToDictionary(t => t.Key, t => t.Value);
        // One raw replacement produces both Lose and Gain; keep both receipts until their typed echoes arrive.
        foreach (var source in sleepObservedTethers.Keys.Concat(observed.Keys).Distinct()
            .Where(k => sleepObservedTethers.GetValueOrDefault(k) != observed.GetValueOrDefault(k))
            .Select(k => k.Source).Distinct())
            sleepRawReceipts.RemoveWhere(r => r.Source == source);
        foreach (var old in sleepObservedTethers.ToArray())
            if (observed.GetValueOrDefault(old.Key) != old.Value)
                SleepTether(old.Key.Source, old.Value, old.Key.Type, false, _now, true);
        foreach (var pair in observed)
            if (sleepObservedTethers.GetValueOrDefault(pair.Key) != pair.Value)
                SleepTether(pair.Key.Source, pair.Value, pair.Key.Type, true, _now, true);
        sleepObservedTethers.Clear();
        foreach (var pair in observed) sleepObservedTethers[pair.Key] = pair.Value;
    }

    private void SleepOtisRemoved(ulong id, IReadOnlyDictionary<ulong, DungeonActor>? actors = null)
    {
        if (!IsSleep && !IsOtis) return;
        sleepOtisCompleted.Remove(id);
        foreach (var pair in sleepLiveTethers.Where(p => p.Key.Source == id).ToArray())
        {
            SleepUntether(id, pair.Value, pair.Key.Type, actors);
            sleepLiveTethers.Remove(pair.Key);
        }
        sleepRawReceipts.RemoveWhere(r => r.Source == id);
        sleepObservedTethers.Remove((id, 1));
        sleepObservedTethers.Remove((id, 84));
        for (var i = 0; i < sleepBeams.Count; ++i)
        {
            var b = sleepBeams[i];
            sleepBeams[i] = b with { SourceRemoved = b.SourceRemoved || b.Source.Id == id,
                TargetRemoved = b.TargetRemoved || b.Target.Id == id };
        }
    }

    private IEnumerable<DungeonAmbientAoe> SleepOtisAoes()
    {
        if (Active == null) yield break;
        if (IsOtis)
        {
            foreach (var (cast, index) in otisCircles.Select((c, i) => (c, i)))
                yield return new($"otis/circle/{index}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                    cast.Rotation, Activation: cast.FinishAt);
            foreach (var (cast, index) in otisRushes.Take(4).Select((c, i) => (c, i)))
                yield return new($"otis/rush/{index}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                    cast.Rotation, Navigate: index < 2, Activation: cast.FinishAt,
                    Color: otisRushes.Count > 2 && index < 2 ? new Vector4(1, 1, 0, 1) : null);
        }
        if (!IsSleep) yield break;
        foreach (var b in sleepBeams.Where(b => !b.Source.Dead))
        {
            var delta = b.Target.Position - b.Source.Position;
            var angle = MathF.Atan2(delta.X, delta.Z);
            var own = !b.TargetRemoved && b.Target.Id == PlayerId;
            yield return new($"sleep/beam/{b.Id}/display", "Burning Beam", RayRectangleTemplate.Symmetric(4, 40),
                b.Source.Position, angle, Navigate: !own, OutlineOnly: own, Activation: b.Activation);
            if (!own) continue;
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
            {
                var direction = member.Position - b.Source.Position;
                var distance = new Vector2(direction.X, direction.Z).Length();
                if (distance < 2) continue;
                yield return new($"sleep/beam/{b.Id}/away/{member.Id}", "诱导直线远离队伍",
                    new FanTemplate(100, 2 * MathF.Asin(2 / distance)), b.Source.Position,
                    MathF.Atan2(direction.X, direction.Z), Display: false, Activation: b.Activation);
            }
        }
    }

    internal IEnumerable<(string Id, Vector3 Start, Vector3 End)> SleepBeamLines()
    {
        if (!IsSleep || Active == null) yield break;
        foreach (var b in sleepBeams.Where(b => !b.Source.Dead))
            yield return ($"sleep/beam/{b.Id}/line", b.Source.Position, b.Target.Position);
    }

    private IEnumerable<DungeonDamagePrediction> SleepBeamDamage()
    {
        if (!IsSleep || Active == null) yield break;
        foreach (var group in sleepBeams.GroupBy(b => b.Type))
        foreach (var b in group.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
        {
            var member = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == b.Target.Id);
            var actor = _actors.GetValueOrDefault(b.Target.Id);
            var slot = member?.Slot ?? actor?.PartySlot ?? -1;
            if (slot >= 0)
                yield return new(b.Target.Id, slot, member?.Role ?? b.Target.PartyRole, b.Target.Name,
                    group.First().Activation, "Tankbuster");
        }
    }

    private IEnumerable<string> SleepOtisHints(Vector3? player)
    {
        if (player is not { } p || Active == null) yield break;
        if (IsOtis && SleepOtisAoes().Where(a => a.Navigate).Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
        if (!IsSleep) yield break;
        foreach (var b in sleepBeams.Where(b => !b.Source.Dead))
        {
            var d = b.Target.Position - b.Source.Position;
            var shape = RayRectangleTemplate.Symmetric(4, 40).Resolve(b.Source.Position, MathF.Atan2(d.X, d.Z), null);
            if (!b.TargetRemoved && b.Target.Id == PlayerId)
            {
                if (_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position)))
                    yield return "Bait away from raid!";
            }
            else if (shape.Contains(p)) yield return "GTFO from baited aoe!";
        }
    }

    private void ResetSleepOtis()
    {
        otisCircles.Clear();
        otisRushes.Clear();
        sleepBeams.Clear();
        sleepBeamActivations.Clear();
        sleepLiveTethers.Clear();
        sleepObservedTethers.Clear();
        sleepRawReceipts.Clear();
        sleepBeamId = 0;
    }
}
