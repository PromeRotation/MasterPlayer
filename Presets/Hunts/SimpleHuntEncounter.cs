using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class SimpleHuntEncounter
{
    private readonly Dictionary<ulong, ReplayActorState> actors = [];
    private readonly Dictionary<ulong, HuntPendingCast> casts = [];
    private readonly Dictionary<ulong, HuntPendingCast> rips = [];
    private readonly Dictionary<ulong, HuntComboState> combos = [];
    private readonly Dictionary<ulong, HuntChestThumpState> thumps = [];
    private readonly Dictionary<ulong, HuntPredictionState> predictions = [];
    private readonly Dictionary<ulong, HuntStoredSequenceState> storedSequences = [];
    private readonly Dictionary<ulong, HuntRotatingSequenceState> rotations = [];
    private readonly Dictionary<ulong, HuntSneezeState> sneezes = [];
    private readonly Dictionary<ulong, HuntMissileState> missiles = [];
    private readonly Dictionary<ulong, HuntCompressorState> compressors = [];
    private readonly Dictionary<ulong, HuntTargetState> targets = [];
    private readonly Dictionary<ulong, HuntAegeirosState> aegeiros = [];
    private readonly Dictionary<ulong, HuntStackState> stacks = [];
    private readonly Dictionary<ulong, HuntNarrowRiftState> narrowRifts = [];
    private readonly Dictionary<ulong, HuntExecutionState> executions = [];
    private readonly HuntMarchState march = new();
    private readonly Dictionary<ulong, bool> stackStatusEvents = [];
    private readonly Dictionary<ulong, DateTime> stackStatusWatermarks = [];
    private ReplayPartyMember[] partyMembers = [];
    private readonly Dictionary<ulong, List<HuntPendingCast>> delayedDamage = [];
    private readonly HashSet<ulong> hasted = [];
    private readonly HashSet<(ulong Source, uint Action, uint Sequence, DateTime At)> effects = [];
    private readonly Dictionary<(ulong Target, ulong Source), ReplayStatus?> pyreticEvents = [];
    private readonly Dictionary<(ulong Target, ulong Source), DateTime> pyreticWatermarks = [];
    private readonly Dictionary<(ulong Target, uint Status), ReplayStatus?> bossStatusEvents = [];
    private readonly Dictionary<(ulong Target, uint Status), DateTime> bossStatusWatermarks = [];
    private readonly Dictionary<ulong, (uint Action, DateTime StartedAt)> ended = [];
    private readonly HashSet<ulong> engaged = [];
    private readonly HashSet<ulong> active = [];
    private long castOrder;
    private DateTime? observedTime;
    private readonly HuntMisdirectionState misdirection = new();
    internal IReadOnlyCollection<HuntPendingCast> Casts => casts.Values;
    internal IEnumerable<HuntPendingCast> Areas => casts.Values.Concat(rips.Values).Concat(combos.Values.SelectMany(c => c.Areas))
        .Concat(predictions.Values.SelectMany(p => p.Areas)).Concat(storedSequences.Values.SelectMany(s => s.Areas))
        .Concat(rotations.Values.SelectMany(r => r.Areas)).Concat(sneezes.Values.SelectMany(s => s.Areas))
        .Concat(missiles.Values.SelectMany(m => m.Areas)).Concat(compressors.Values.SelectMany(c => c.Areas))
        .Concat(aegeiros.SelectMany(p => p.Value.Areas(actors[p.Key], casts.GetValueOrDefault(p.Key))))
        .Concat(narrowRifts.SelectMany(p => p.Value.Areas(actors[p.Key], casts.GetValueOrDefault(p.Key))))
        .Concat(executions.Values.SelectMany(e => e.Areas));
    internal IEnumerable<ReplayActorState> ActiveActors => active.Select(id => actors[id]);
    internal bool Active => active.Count > 0;

    internal void Synchronize(IEnumerable<ReplayActorState> observations, Func<string, bool> enabled, DateTime? now = null,
        IEnumerable<ReplayPartyMember>? party = null)
    {
        observedTime = now;
        if (party != null) partyMembers = party.ToArray();
        var current = observations.ToArray();
        foreach (var actor in current)
            if (actors.TryGetValue(actor.Id, out var old) && old.Oid != actor.Oid) Remove(actor.Id);
        actors.Clear();
        foreach (var actor in current)
        {
            var merged = actor;
            foreach (var (key, pending) in bossStatusEvents.Where(p => p.Key.Target == actor.Id).ToArray())
            {
                var present = merged.Statuses.FirstOrDefault(s => s.Id == key.Status);
                if (pending == null && present == null || pending != null && (present == pending || pending.ExpiresAt <= now))
                    bossStatusEvents.Remove(key);
                else merged = WithStatus(merged, key.Status, pending);
            }
            actors[actor.Id] = merged;
        }
        engaged.RemoveWhere(id => !actors.TryGetValue(id, out var actor) || !Eligible(actor));
        foreach (var actor in actors.Values.Where(a => Eligible(a) && a.Targetable)) engaged.Add(actor.Id);
        active.Clear();
        active.UnionWith(engaged.Where(id => enabled(SimpleHuntCatalog.Find(actors[id].Oid)!.Name)));
        if (!ActiveActors.Any(a => a.Oid == 0x41FC)) misdirection.Clear();
        foreach (var key in bossStatusEvents.Keys.Where(k => !active.Contains(k.Target)).ToArray()) bossStatusEvents.Remove(key);
        foreach (var key in bossStatusWatermarks.Keys.Where(k => !active.Contains(k.Target)).ToArray()) bossStatusWatermarks.Remove(key);
        foreach (var id in rips.Keys.Where(id => !active.Contains(id)).ToArray()) rips.Remove(id);
        foreach (var id in combos.Keys.Where(id => !active.Contains(id)).ToArray()) combos.Remove(id);
        foreach (var id in thumps.Keys.Where(id => !active.Contains(id)).ToArray()) thumps.Remove(id);
        foreach (var id in predictions.Keys.Where(id => !active.Contains(id)).ToArray()) predictions.Remove(id);
        foreach (var id in storedSequences.Keys.Where(id => !active.Contains(id)).ToArray()) storedSequences.Remove(id);
        foreach (var id in rotations.Keys.Where(id => !active.Contains(id)).ToArray()) rotations.Remove(id);
        foreach (var id in sneezes.Keys.Where(id => !active.Contains(id)).ToArray()) sneezes.Remove(id);
        foreach (var id in missiles.Keys.Where(id => !active.Contains(id)).ToArray()) missiles.Remove(id);
        foreach (var id in compressors.Keys.Where(id => !active.Contains(id)).ToArray()) compressors.Remove(id);
        foreach (var id in targets.Keys.Where(id => !active.Contains(id)).ToArray()) targets.Remove(id);
        foreach (var id in aegeiros.Keys.Where(id => !active.Contains(id)).ToArray()) aegeiros.Remove(id);
        foreach (var id in stacks.Keys.Where(id => !active.Contains(id)).ToArray()) stacks.Remove(id);
        foreach (var id in narrowRifts.Keys.Where(id => !active.Contains(id)).ToArray()) narrowRifts.Remove(id);
        foreach (var id in executions.Keys.Where(id => !active.Contains(id)).ToArray()) executions.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4395)) executions.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x35DB)) narrowRifts.TryAdd(actor.Id, new());
        if (ActiveActors.Any(a => a.Oid is 0x35BF or 0x35DB)) march.Synchronize(actors);
        else march.Clear();
        if (!ActiveActors.Any(a => a.Oid == 0x2890))
        {
            stackStatusEvents.Clear();
            stackStatusWatermarks.Clear();
        }
        foreach (var (id, present) in stackStatusEvents.ToArray())
            if (actors.TryGetValue(id, out var actor) && actor.Statuses.Any(s => s.Id == 1435) == present)
                stackStatusEvents.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x3671)) aegeiros.TryAdd(actor.Id, new());
        foreach (var id in delayedDamage.Keys.Where(id => !active.Contains(id)).ToArray()) delayedDamage.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x35BE)) compressors.TryAdd(actor.Id, new());
        hasted.RemoveWhere(id => !active.Contains(id));
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x283A)) thumps.TryAdd(actor.Id, new());
        if (!ActiveActors.Any(a => a.Oid == 0x1AB2))
        {
            pyreticEvents.Clear();
            pyreticWatermarks.Clear();
        }
        foreach (var (key, pending) in pyreticEvents.ToArray())
        {
            if (!actors.TryGetValue(key.Target, out var player) || player.Dead)
            {
                pyreticEvents.Remove(key);
                continue;
            }
            var observed = player.Statuses.FirstOrDefault(s => s.Id == 960 && s.SourceId == key.Source);
            // Log duration and object sampling can differ by a frame; an older pre-refresh expiry must not confirm a new gain.
            if (pending == null && observed == null || pending != null && observed != null
                && observed.ExpiresAt >= pending.ExpiresAt.AddMilliseconds(-100))
                pyreticEvents.Remove(key);
        }
        foreach (var id in casts.Keys.Where(id => !actors.TryGetValue(id, out var actor)
            || !active.Contains(id)).ToArray())
            casts.Remove(id);
        foreach (var id in ended.Keys.Where(id => !actors.ContainsKey(id)).ToArray()) ended.Remove(id);
        foreach (var actor in actors.Values.Where(a => active.Contains(a.Id)))
        {
            if (!enabled(SimpleHuntCatalog.Find(actor.Oid)!.Name)) continue;
            if (actor.Cast is { } cast)
            {
                Start(actor.Id, cast, false, observedAt: now);
            }
            else if (casts.TryGetValue(actor.Id, out var previous) && previous.Observed) Finish(actor.Id);
            if (casts.TryGetValue(actor.Id, out var currentCast) && currentCast.Rule.FollowCasterRotation && currentCast.Rotation != actor.Rotation)
                casts[actor.Id] = currentCast with { Rotation = actor.Rotation };
            if (combos.TryGetValue(actor.Id, out var combo)) combo.Update(actor);
            if (predictions.TryGetValue(actor.Id, out var prediction)) prediction.Update(actor);
            if (storedSequences.TryGetValue(actor.Id, out var stored)) stored.Update(actor);
            if (sneezes.TryGetValue(actor.Id, out var sneeze)) sneeze.Update(actor);
            if (missiles.TryGetValue(actor.Id, out var missile)) missile.Update(actors);
            if (targets.TryGetValue(actor.Id, out var target)) target.Update(actors);
            if (stacks.TryGetValue(actor.Id, out var stack)) stack.Update(actors);
        }
    }

    internal bool Handles(ulong source, uint action, Func<string, bool> enabled)
        => actors.TryGetValue(source, out var actor) && active.Contains(source)
            && SimpleHuntCatalog.Find(actor.Oid) is { } definition && enabled(definition.Name)
            && definition.Casts.Any(c => c.Action == action);

    internal void Start(ulong source, ReplayCast cast, bool packetPose, Vector3? packetSourcePosition = null, DateTime? observedAt = null)
    {
        if (!actors.TryGetValue(source, out var actor) || !active.Contains(source)
            || SimpleHuntCatalog.Find(actor.Oid) is not { } definition
            || cast.FinishAt < cast.StartedAt) return;
        if (ended.TryGetValue(source, out var old) && (cast.StartedAt < old.StartedAt
            || old.Action == cast.ActionId && (cast.StartedAt - old.StartedAt).TotalSeconds < .5)) return;
        if (casts.TryGetValue(source, out var previous))
        {
            if ((previous.StartedAt - cast.StartedAt).TotalSeconds >= .5) return;
            if (previous.Rule.Action == cast.ActionId && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .5)
            {
                if (cast.Rotation == null && previous.Input.Rotation is { } knownRotation)
                    cast = cast with { Rotation = knownRotation };
                var observationChanged = !packetPose && previous.Observation is { } observation
                    && Changed(observation, cast) && Changed(previous.Input, cast);
                var updated = previous;
                if (packetPose && (!previous.PacketPose || Changed(previous.Input, cast))
                    || observationChanged)
                    updated = Create(source, definition, previous.Rule, actor, cast, packetPose, packetSourcePosition, observedAt)
                        with { Observed = previous.Observed || !packetPose, Observation = packetPose ? previous.Observation : cast };
                else if (!packetPose && updated.Observation != cast)
                    updated = updated with { Observed = true, Observation = cast };
                if (updated != previous)
                {
                    casts[source] = updated;
                    if (observationChanged) AddDelayedDamage(updated);
                    else if (previous.Input != updated.Input) CorrectDelayedDamage(previous, updated);
                    UpdateRip(updated);
                    if (combos.TryGetValue(source, out var combo) && (observationChanged || previous.Input != updated.Input))
                    {
                        if (observationChanged)
                        {
                            combo.Finish(previous.Definition.Oid, previous.Rule.Action);
                            combo.Start(updated, actor);
                        }
                        else combo.Correct(previous, updated, actor);
                    }
                    if (thumps.TryGetValue(source, out var thump) && cast.ActionId == 17859
                        && (observationChanged || previous.Input != updated.Input))
                    {
                        if (observationChanged) thump.Finish();
                        thump.Start(updated.Activation);
                    }
                    if (predictions.TryGetValue(source, out var prediction))
                    {
                        if (observationChanged)
                        {
                            prediction.Finish(actor.Oid, previous.Rule.Action);
                            prediction.Start(updated, actor);
                        }
                        else if (previous.Input != updated.Input) prediction.Correct(previous, updated, actor);
                    }
                    if (aegeiros.TryGetValue(source, out var tree))
                    {
                        if (observationChanged)
                        {
                            tree.Finish(previous.Rule.Action);
                            tree.Start(updated);
                        }
                        else if (previous.Input != updated.Input) tree.Correct(previous, updated);
                    }
                    if (storedSequences.TryGetValue(source, out var stored))
                    {
                        if (observationChanged)
                        {
                            stored.Finish(actor.Oid, previous.Rule.Action);
                            stored.Start(updated, actor);
                        }
                        else if (previous.Input != updated.Input) stored.Correct(previous, updated);
                    }
                    if (!packetPose && previous.Observation == null)
                    {
                        if (predictions.TryGetValue(source, out var initialPrediction)) initialPrediction.FirstObservation(updated, actor);
                        if (storedSequences.TryGetValue(source, out var initialStored)) initialStored.FirstObservation(updated, actor);
                        if (targets.TryGetValue(source, out var initialTarget)) initialTarget.FirstObservation(updated, actors);
                    }
                    if (rotations.TryGetValue(source, out var rotation))
                    {
                        if (observationChanged)
                        {
                            rotation.Finish(previous.Rule.Action, observedAt ?? cast.StartedAt);
                            rotation.Start(updated);
                        }
                        else if (previous.Input != updated.Input) rotation.Correct(previous, updated);
                    }
                    if (sneezes.TryGetValue(source, out var sneeze))
                    {
                        if (observationChanged)
                        {
                            sneeze.Finish(previous.Rule.Action, observedAt ?? cast.StartedAt, actor);
                            sneeze.Start(updated, actor);
                        }
                        else if (previous.Input != updated.Input) sneeze.Correct(previous, updated, actor);
                    }
                    if (missiles.TryGetValue(source, out var missile))
                    {
                        if (observationChanged)
                        {
                            missile.Finish(previous.Rule.Action, actors);
                            missile.Start(updated, actors);
                        }
                        else if (previous.Input != updated.Input) missile.Correct(previous, updated, actors);
                    }
                    if (compressors.TryGetValue(source, out var compressor))
                    {
                        if (observationChanged) compressor.Start(updated);
                        else if (previous.Input != updated.Input) compressor.Correct(previous, updated);
                    }
                    if (targets.TryGetValue(source, out var target))
                    {
                        if (observationChanged)
                        {
                            target.Finish(previous.Rule.Action);
                            target.Start(updated, actors);
                        }
                        else if (previous.Input != updated.Input) target.Correct(previous, updated, actors);
                    }
                    if (stacks.TryGetValue(source, out var stack))
                    {
                        if (observationChanged) stack.Start(updated, actors, FrozenForbidden());
                        else if (previous.Input != updated.Input) stack.Correct(previous, updated, actors);
                        if (!packetPose && previous.Observation == null) stack.FirstObservation(updated, actors);
                    }
                    if (observationChanged && narrowRifts.TryGetValue(source, out var narrow))
                    {
                        narrow.Finish();
                        narrow.Start(updated);
                    }
                    if (executions.TryGetValue(source, out var execution))
                    {
                        if (observationChanged) execution.Start(updated);
                        else if (previous.Input != updated.Input) execution.Correct(previous, updated);
                    }
                }
                return;
            }
            Finish(source);
        }
        var rule = definition.Casts.FirstOrDefault(c => c.Action == cast.ActionId);
        // EmptyPromise consumes every primary cast finish, including casts without a dedicated rule.
        if (rule == null && actor.Oid == 0x35DB) rule = new(cast.ActionId, "OtherCast", null);
        if (rule == null
            || cast.Rotation == null && (rule.Shape is not (null or CircleTemplate or DonutTemplate) && !rule.FollowCasterRotation
                || actor.Oid == 0x2854 && cast.ActionId is 17423 or 17424
                || actor.Oid == 0x28B5 && cast.ActionId is 17495 or 17497 or 17498
                || actor.Oid == 0x360B && cast.ActionId == 27318
                || actor.Oid == 0x3670 && cast.ActionId is 27624 or 27625
                || actor.Oid == 0x361B && cast.ActionId is 27493 or 27494 or 27497 or 27498 or 27521 or 27522
                || actor.Oid == 0x360A && cast.ActionId is 27307 or 27308 or 27692 or 27309
                || actor.Oid == 0x35BE && cast.ActionId == 27472
                || actor.Oid == 0x4395 && cast.ActionId is >= 38457 and <= 38460
                || actor.Oid == 0x2875 && cast.ActionId is 18100 or 18101)) return;
        var pending = Create(source, definition, rule, actor, cast, packetPose, packetSourcePosition, observedAt);
        casts[source] = pending;
        AddDelayedDamage(pending);
        UpdateRip(pending, true);
        if (actor.Oid == 0x2874 && cast.ActionId == 18020) hasted.Add(source);
        if (actor.Oid == 0x283A && cast.ActionId == 17859) thumps[source].Start(pending.Activation);
        if (actor.Oid is 0x2854 or 0x28B5)
        {
            if (!combos.TryGetValue(source, out var combo)) combos[source] = combo = new();
            combo.Start(pending, actor);
        }
        if (actor.Oid is 0x35DE or 0x360B or 0x35DC or 0x28B4 or 0x288F or 0x2853 or 0x35BF)
        {
            if (!predictions.TryGetValue(source, out var prediction)) predictions[source] = prediction = new();
            prediction.Start(pending, actor);
        }
        if (actor.Oid is 0x35BD or 0x3670)
        {
            if (!storedSequences.TryGetValue(source, out var stored)) storedSequences[source] = stored = new();
            stored.Start(pending, actor);
        }
        if (actor.Oid is 0x361B or 0x2875)
        {
            if (!rotations.TryGetValue(source, out var rotation)) rotations[source] = rotation = new();
            rotation.Start(pending);
        }
        if (actor.Oid == 0x360A)
        {
            if (!sneezes.TryGetValue(source, out var sneeze)) sneezes[source] = sneeze = new();
            sneeze.Start(pending, actor);
        }
        if (actor.Oid == 0x3609)
        {
            if (!missiles.TryGetValue(source, out var missile)) missiles[source] = missile = new();
            missile.Start(pending, actors);
        }
        if (compressors.TryGetValue(source, out var compressorState)) compressorState.Start(pending);
        if (aegeiros.TryGetValue(source, out var aegeirosState)) aegeirosState.Start(pending);
        if (narrowRifts.TryGetValue(source, out var narrowState)) narrowState.Start(pending);
        if (executions.TryGetValue(source, out var executionState)) executionState.Start(pending);
        if (actor.Oid == 0x2890 && cast.ActionId == 16971)
        {
            var stack = new HuntStackState();
            stack.Start(pending, actors, FrozenForbidden());
            stacks[source] = stack;
        }
        if (rule.BaitRadius > 0 || rule.SpreadRadius > 0)
        {
            if (!targets.TryGetValue(source, out var target)) targets[source] = target = new();
            target.Start(pending, actors);
        }
    }

    private void UpdateRip(HuntPendingCast cast, bool starting = false)
    {
        if (cast.Definition.Oid == 0x1AC0 && cast.Rule.Action == 8191 && (starting || rips.ContainsKey(cast.Source)))
            rips[cast.Source] = cast with { Rule = new(8192, "Rip", cast.Shape), Followup = true };
    }

    private IEnumerable<ulong> FrozenForbidden()
        => partyMembers.Where(p => p.Slot is >= 0 and < 64 && p.EntityId is not (0 or 0xE0000000))
            .Select(p => p.EntityId).Distinct().Where(id => stackStatusEvents.TryGetValue(id, out var present)
                ? present : actors.TryGetValue(id, out var actor) && actor.Statuses.Any(s => s.Id == 1435));

    internal void StackStatus(ulong target, DateTime at, bool present)
    {
        if (!ActiveActors.Any(a => a.Oid == 0x2890) || stackStatusWatermarks.TryGetValue(target, out var previous) && at < previous) return;
        stackStatusWatermarks[target] = at;
        stackStatusEvents[target] = present;
    }

    internal void MarchStatus(ulong target, uint status, DateTime at, ReplayStatus? value)
    {
        if (ActiveActors.Any(a => a.Oid is 0x35BF or 0x35DB)) march.Status(target, status, at, value);
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> MarchMovements(ReplayActorState player)
        => march.Movements(player, observedTime ?? default);

    private void AddDelayedDamage(HuntPendingCast cast)
    {
        if (cast.Definition.Oid != 0x288F || cast.Rule.Action != 16964) return;
        var target = cast.TargetId == cast.Source ? actors[cast.Source].TargetId : cast.TargetId;
        if (!actors.ContainsKey(target)) return;
        if (!delayedDamage.TryGetValue(cast.Source, out var damage)) delayedDamage[cast.Source] = damage = [];
        damage.Add(cast with { TargetId = target });
    }

    private void CorrectDelayedDamage(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (!delayedDamage.TryGetValue(previous.Source, out var damage)) return;
        var index = damage.FindIndex(c => c.Input == previous.Input);
        if (index < 0) return;
        var target = damage[index].TargetId;
        if (previous.Input.TargetId != updated.Input.TargetId)
            target = updated.TargetId == updated.Source ? actors[updated.Source].TargetId : updated.TargetId;
        if (previous.Input.TargetId == updated.Input.TargetId || actors.ContainsKey(target))
            damage[index] = updated with { TargetId = target };
        else damage.RemoveAt(index);
    }

    internal void Effect(ulong source, uint action, DateTime at, uint sequence = 0, ulong target = 0)
    {
        if (action == 8192 && rips.TryGetValue(source, out var rip) && at >= rip.StartedAt)
            rips.Remove(source);
        if (!active.Contains(source) || action is not (17421 or 17422 or 18042 or 17495 or 17497 or 17498 or 17863
                or 26878 or 26879 or 26880 or 26881 or 26915 or 26916 or 26167 or 26168
                or 27470 or 27471 or 27472 or 27473 or 27474 or 17489 or 16964 or 18037 or 17369
                or 27333 or 27516 or 38457 or 38458 or 38459 or 38460 or 38461 or 38462 or 38463)
            || !actors.TryGetValue(source, out var actor) || !effects.Add((source, action, sequence, sequence == 0 ? at : default))) return;
        if (combos.TryGetValue(source, out var combo)) combo.Effect(actor.Oid, action, at);
        if (action == 17863 && thumps.TryGetValue(source, out var thump)) thump.Effect();
        if (storedSequences.TryGetValue(source, out var stored)) stored.Effect(actor.Oid, action, at);
        if (compressors.TryGetValue(source, out var compressor)) compressor.Effect(action, at);
        if (predictions.TryGetValue(source, out var prediction)) prediction.Effect(actor.Oid, action, at);
        if (narrowRifts.TryGetValue(source, out var narrow)) narrow.Effect(actor, action, at);
        if (executions.TryGetValue(source, out var execution)) execution.Effect(action, at);
        if (actor.Oid == 0x288F && action == 16964 && delayedDamage.TryGetValue(source, out var damage))
        {
            var index = damage.FindIndex(c => c.TargetId == target && c.StartedAt <= at);
            if (index >= 0) damage.RemoveAt(index);
        }
    }

    internal void Pyretic(ulong target, DateTime at, ReplayStatus? status, ulong source = 0)
    {
        var key = (target, status?.SourceId ?? source);
        if (!ActiveActors.Any(a => a.Oid == 0x1AB2) || actors.TryGetValue(target, out var actor) && actor.Dead
            || pyreticWatermarks.TryGetValue(key, out var old) && at < old) return;
        pyreticWatermarks[key] = at;
        pyreticEvents[key] = status;
    }

    internal void BossStatus(ulong target, uint status, DateTime at, ReplayStatus? value)
    {
        if (!actors.TryGetValue(target, out var actor) || !active.Contains(target)
            || !(actor.Oid == 0x360B && status is 2840 or 2841 or 2842 or 2958
                || actor.Oid == 0x3670 && status is >= 2835 and <= 2838)) return;
        var key = (target, status);
        if (bossStatusWatermarks.TryGetValue(key, out var old) && at < old) return;
        bossStatusWatermarks[key] = at;
        bossStatusEvents[key] = value;
        actors[target] = WithStatus(actor, status, value);
    }

    private static ReplayActorState WithStatus(ReplayActorState actor, uint status, ReplayStatus? value)
    {
        var statuses = actor.Statuses.ToList();
        var index = statuses.FindIndex(s => s.Id == status);
        if (index >= 0)
        {
            if (value == null) statuses.RemoveAt(index);
            else statuses[index] = value;
        }
        else if (value != null) statuses.Add(value);
        return actor with { Statuses = statuses.ToArray() };
    }

    internal bool StopEverything(ReplayActorState? player, IEnumerable<ReplayPartyMember> party, DateTime now)
        => player is { Dead: false } && ActiveActors.Any(a => a.Oid == 0x1AB2)
            && party.Any(p => p.EntityId == player.Id)
            && (pyreticEvents.Any(p => p.Key.Target == player.Id && p.Value?.ExpiresAt > now)
                || player.Statuses.Any(s => s.Id == 960 && s.ExpiresAt > now && !pyreticEvents.ContainsKey((player.Id, s.SourceId))));

    internal void ObserveMisdirection(IReadOnlyList<ReplayPartyMember> party)
    {
        if (ActiveActors.Any(a => a.Oid == 0x41FC)) misdirection.Observe(actors, party);
    }

    internal void Misdirection(ulong target, uint status, bool present, IReadOnlyList<ReplayPartyMember> party)
    {
        if (ActiveActors.Any(a => a.Oid == 0x41FC)) misdirection.Status(target, status, present, party);
    }

    internal bool Misdirected(ReplayActorState? player, IReadOnlyList<ReplayPartyMember> party)
        => player != null && ActiveActors.Any(a => a.Oid == 0x41FC) && misdirection.Active(player.Id, party);

    internal void Finish(ulong source, uint action = 0, DateTime? at = null)
    {
        if (!casts.TryGetValue(source, out var cast) || action != 0 && action != cast.Rule.Action
            || at < cast.StartedAt) return;
        ended[source] = (cast.Rule.Action, cast.StartedAt);
        casts.Remove(source);
        if (combos.TryGetValue(source, out var combo)) combo.Finish(cast.Definition.Oid, cast.Rule.Action);
        if (cast.Rule.Action == 17859 && thumps.TryGetValue(source, out var thump)) thump.Finish();
        if (predictions.TryGetValue(source, out var prediction)) prediction.Finish(cast.Definition.Oid, cast.Rule.Action);
        if (storedSequences.TryGetValue(source, out var stored)) stored.Finish(cast.Definition.Oid, cast.Rule.Action);
        if (rotations.TryGetValue(source, out var rotation)) rotation.Finish(cast.Rule.Action, at ?? observedTime ?? cast.Activation);
        if (sneezes.TryGetValue(source, out var sneeze)) sneeze.Finish(cast.Rule.Action, at ?? observedTime ?? cast.Activation, actors[source]);
        if (missiles.TryGetValue(source, out var missile)) missile.Finish(cast.Rule.Action, actors);
        if (targets.TryGetValue(source, out var target)) target.Finish(cast.Rule.Action);
        if (aegeiros.TryGetValue(source, out var tree)) tree.Finish(cast.Rule.Action);
        if (narrowRifts.TryGetValue(source, out var narrow)) narrow.Finish();
        if (cast.Rule.Action == 16971) stacks.Remove(source);
    }

    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IEnumerable<ReplayPartyMember> party)
    {
        var members = party.ToArray();
        var ids = members.Where(p => p.EntityId is not (0 or 0xE0000000)).Select(p => p.EntityId).Distinct().ToArray();
        foreach (var cast in casts.Values.Where(c => c.Rule.DamageType != null))
            foreach (var target in cast.Rule.DamageType == "Raidwide"
                ? ids.Where(id => actors.TryGetValue(id, out var actor) && !actor.Dead)
                : ids.Where(id => id == (cast.TargetId == cast.Source ? actors[cast.Source].TargetId : cast.TargetId)))
                yield return new(cast.Source, target, cast.Activation, cast.Rule.DamageType!);
        foreach (var (source, thump) in thumps)
            foreach (var target in members.Where(p => p.Slot is >= 0 and < 24).Select(p => p.EntityId).Distinct()
                .Where(id => actors.TryGetValue(id, out var actor) && !actor.Dead))
                yield return new(source, target, thump.Activation, "Raidwide");
        foreach (var target in targets.Values.Where(t => t.Active && t.Cast?.Rule.BaitRadius > 0 && ids.Contains(t.TargetId)))
            yield return new(target.Cast!.Source, target.TargetId, target.Cast.Activation, "Raidwide");
        foreach (var cast in delayedDamage.Values.SelectMany(d => d).Where(c => ids.Contains(c.TargetId)))
            yield return new(cast.Source, cast.TargetId, cast.Activation, "Tankbuster");
        foreach (var stack in stacks.Values.Where(s => s.Active && s.Forbidden.Count != 0))
        {
            // Each boss owns a separate upstream component and full-mask prediction guard.
            var cast = stack.Cast!;
            foreach (var id in members.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).Distinct()
                .Where(id => id is not (0 or 0xE0000000) && !stack.Forbidden.Contains(id)))
                yield return new(cast.Source, id, cast.Activation, "Shared");
        }
    }

    internal IEnumerable<HuntPendingCast> TargetPreviews(ReplayActorState? player)
        => targets.Values.Select(t => t.Preview(player)).OfType<HuntPendingCast>().Concat(Cleaves())
            .Concat(stacks.Values.Select(s => s.Preview(player, PartyActors(partyMembers), StackTargets(s))).OfType<HuntPendingCast>());

    internal IEnumerable<HuntPendingCast> TargetDangers(ReplayActorState? player, IEnumerable<ReplayPartyMember> party)
    {
        if (player == null) return [];
        var members = PartyActors(party);
        return targets.Values.SelectMany(t => t.Dangers(player, members, targets.Values
            .Where(other => other.Active && other.Cast?.Definition.Oid == t.Cast?.Definition.Oid
                && other.Cast?.Rule.Action == t.Cast?.Rule.Action && other.Cast?.Rule.SpreadRadius > 0
                && other.AttachedTargetId != 0)
            .Select(other => other.AttachedTargetId).ToHashSet()))
            .Concat(Cleaves().SelectMany(c => HuntAegeirosState.Dangers(c, actors[c.Source], player, members)))
            .Concat(stacks.Values.SelectMany(s => s.Navigation(player, members, StackTargets(s))));
    }

    internal IEnumerable<string> TargetHints(ReplayActorState? player, IEnumerable<ReplayPartyMember> party)
        => player == null ? [] : targets.Values.SelectMany(t => t.Hints(player, PartyActors(party)))
            .Concat(Cleaves().Any(c => HuntAegeirosState.Hit(c, player)) ? ["GTFO from cleave!"] : [])
            .Concat(stacks.Values.SelectMany(s => s.Hints(player, PartyActors(party)))).Distinct();

    private static HashSet<ulong> StackTargets(HuntStackState stack)
        => stack.AttachedTargetId is var id && id != 0 ? [id] : [];

    private IEnumerable<HuntPendingCast> Cleaves()
    {
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x3671))
        {
            var casting = casts.ContainsKey(actor.Id) || actor.Cast is { } observed
                && !(ended.TryGetValue(actor.Id, out var finish) && finish.Action == observed.ActionId
                    && Math.Abs((finish.StartedAt - observed.StartedAt).TotalSeconds) < .5);
            if (HuntAegeirosState.Cleave(actor, actors.GetValueOrDefault(actor.TargetId), casting) is { } cleave)
                yield return cleave;
        }
    }

    private ReplayActorState[] PartyActors(IEnumerable<ReplayPartyMember> party)
        => party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).Distinct()
            .Where(id => actors.TryGetValue(id, out var actor) && !actor.Dead).Select(id => actors[id]).ToArray();

    internal IEnumerable<HuntMovementProjection> Knockbacks(ReplayActorState player)
    {
        foreach (var component in casts.Values.Where(c => c.Rule.KnockbackDistance > 0 || c.Rule.PullDistance > 0).OrderBy(c => c.Order)
            .GroupBy(c => (c.Definition.Oid, c.Rule.Action, Instance: c.Rule.PullFromCaster ? c.Source : 0)))
        {
            // Each upstream SimpleKnockbacks component has its own source list.
            var from = player.Position;
            foreach (var cast in component)
            {
                if (player.Statuses.Any(s => s.ExpiresAt > cast.Activation && s.Id is 3054 or 160 or 1209 or 1722 or 2663 or 2345)) continue;
                var origin = cast.Rule.PullFromCaster ? actors[cast.Source].Position : cast.Input.Location;
                var shape = cast.Rule.PullDistance > 0 ? new CircleTemplate(30) : cast.Shape;
                if (shape?.Resolve(origin, cast.Input.Rotation ?? 0, null).Contains(from, 0) != true) continue;
                var direction = from - origin;
                direction.Y = 0;
                if (direction.LengthSquared() == 0) continue;
                var distance = cast.Rule.KnockbackDistance;
                if (cast.Rule.PullDistance > 0)
                {
                    var minimum = cast.Rule.PullFromCaster ? actors[cast.Source].HitboxRadius + player.HitboxRadius : 0;
                    distance = -MathF.Min(cast.Rule.PullDistance, direction.Length() - minimum);
                    if (distance >= 0) continue;
                }
                var to = from + Vector3.Normalize(direction) * distance;
                yield return new(cast.Source, from, to);
                from = to;
            }
        }
    }

    internal float? Facing(ReplayActorState player, DateTime now)
    {
        var directions = casts.Values.Where(c => c.Rule.GazeRange > 0 && c.Activation <= now.AddSeconds(.75)
            && InGazeRange(c, player) && (c.Input.Location.X != player.Position.X || c.Input.Location.Z != player.Position.Z))
            .Select(c => (Direction: Direction(c, player) + (c.Rule.InvertedGaze ? MathF.PI : 0),
                HalfAngle: c.Rule.InvertedGaze ? 3 * MathF.PI / 4 : MathF.PI / 4)).ToArray();
        if (directions.Length == 0) return null;
        var boundaries = directions.SelectMany(d => new[] { d.Direction - d.HalfAngle, d.Direction + d.HalfAngle })
            .Select(a => (a % MathF.Tau + MathF.Tau) % MathF.Tau).Order().ToArray();
        var candidates = new List<float> { player.Rotation };
        for (var i = 0; i < boundaries.Length; ++i)
        {
            var end = i + 1 < boundaries.Length ? boundaries[i + 1] : boundaries[0] + MathF.Tau;
            candidates.Add((boundaries[i] + end) * .5f);
        }
        return candidates.Cast<float?>().FirstOrDefault(candidate => directions.All(d =>
            MathF.Abs(MathF.Atan2(MathF.Sin(candidate!.Value - d.Direction), MathF.Cos(candidate.Value - d.Direction))) > d.HalfAngle));
    }

    internal IEnumerable<string> Hints(ReplayActorState? player)
    {
        foreach (var hint in casts.Values.Select(c => c.Rule.Hint).Where(h => !string.IsNullOrEmpty(h)).Distinct()) yield return hint!;
        foreach (var hint in thumps.Values.Select(t => t.Hint).Where(h => h != null).Distinct()) yield return hint!;
        if (hasted.Count != 0) yield return "Getting hit by the needle attack will instantly kill you from now on!";
        if (delayedDamage.Values.Any(d => d.Count != 0)) yield return "Tankbuster";
        if (casts.Values.Any(c => c.Definition.Oid == 0x1AB2 && c.Rule.Action == 7913))
            yield return "Applies Pyretic - STOP everything until it runs out!";
        if (player == null) yield break;
        if (Areas.Any(c => c.Risky && c.Shape?.Resolve(c.Location, c.Rotation, null).Contains(player.Position, 0) == true))
            yield return "GTFO from aoe!";
        if (casts.Values.Any(c => c.Rule.GazeRange > 0 && !c.Rule.InvertedGaze && InGazeRange(c, player) && HitByGaze(c, player)))
            yield return "Turn away from gaze!";
        if (casts.Values.Any(c => c.Rule.GazeRange > 0 && c.Rule.InvertedGaze && InGazeRange(c, player) && !HitByGaze(c, player)))
            yield return "Face the eye!";
        // SimpleBossModule's R30 is a local display/search view, not a physical wall.
        if (Knockbacks(player).Any(m => UnsafeLanding(m, player)))
            yield return "About to be knocked into danger!";
        var lastMarch = MarchMovements(player).LastOrDefault();
        if (lastMarch.From != lastMarch.To && ActiveActors.Any(a => a.Oid == 0x35BF
                ? Vector2.DistanceSquared(new(lastMarch.To.X, lastMarch.To.Z), new(a.Position.X, a.Position.Z)) <= 324
                : a.Oid == 0x35DB && Vector2.DistanceSquared(new(lastMarch.To.X, lastMarch.To.Z), new(player.Position.X, player.Position.Z)) > 900))
            yield return "Aim for safe spot!";
    }

    private bool UnsafeLanding(HuntMovementProjection movement, ReplayActorState player)
    {
        IEnumerable<HuntPendingCast> aoes;
        if (actors[movement.Source].Oid == 0x2853)
            aoes = predictions.TryGetValue(movement.Source, out var flood) ? flood.Areas : [];
        else if (actors[movement.Source].Oid == 0x2875)
            aoes = rotations.TryGetValue(movement.Source, out var rotation) ? rotation.Areas : [];
        else
            return Vector2.DistanceSquared(new(movement.To.X, movement.To.Z), new(player.Position.X, player.Position.Z)) > 900;
        return aoes.Any(a => a.Shape?.Resolve(a.Location, a.Rotation, null).Contains(movement.To, 0) == true);
    }

    internal static bool InGazeRange(HuntPendingCast gaze, ReplayActorState player)
        => Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(gaze.Input.Location.X, gaze.Input.Location.Z))
            <= gaze.Rule.GazeRange * gaze.Rule.GazeRange;

    internal static bool HitByGaze(HuntPendingCast gaze, ReplayActorState player)
    {
        var delta = new Vector2(gaze.Input.Location.X - player.Position.X, gaze.Input.Location.Z - player.Position.Z);
        return delta.LengthSquared() > 0 && Vector2.Dot(new(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation)),
            Vector2.Normalize(delta)) >= .707107f;
    }

    private static float Direction(HuntPendingCast gaze, ReplayActorState player)
        => MathF.Atan2(gaze.Input.Location.X - player.Position.X, gaze.Input.Location.Z - player.Position.Z);

    internal void Remove(ulong source)
    {
        foreach (var missile in missiles.Values) missile.Remove(source);
        foreach (var target in targets.Values) target.Remove(source);
        foreach (var stack in stacks.Values) stack.Remove(source);
        actors.Remove(source);
        casts.Remove(source);
        rips.Remove(source);
        combos.Remove(source);
        thumps.Remove(source);
        predictions.Remove(source);
        storedSequences.Remove(source);
        rotations.Remove(source);
        sneezes.Remove(source);
        missiles.Remove(source);
        compressors.Remove(source);
        targets.Remove(source);
        aegeiros.Remove(source);
        stacks.Remove(source);
        narrowRifts.Remove(source);
        executions.Remove(source);
        march.Remove(source);
        stackStatusEvents.Remove(source);
        stackStatusWatermarks.Remove(source);
        delayedDamage.Remove(source);
        hasted.Remove(source);
        effects.RemoveWhere(e => e.Source == source);
        foreach (var key in pyreticEvents.Keys.Where(k => k.Target == source).ToArray()) pyreticEvents.Remove(key);
        foreach (var key in pyreticWatermarks.Keys.Where(k => k.Target == source).ToArray()) pyreticWatermarks.Remove(key);
        foreach (var key in bossStatusEvents.Keys.Where(k => k.Target == source).ToArray()) bossStatusEvents.Remove(key);
        foreach (var key in bossStatusWatermarks.Keys.Where(k => k.Target == source).ToArray()) bossStatusWatermarks.Remove(key);
        ended.Remove(source);
        engaged.Remove(source);
        active.Remove(source);
    }

    internal void Reset()
    {
        actors.Clear();
        casts.Clear();
        rips.Clear();
        combos.Clear();
        thumps.Clear();
        predictions.Clear();
        storedSequences.Clear();
        rotations.Clear();
        sneezes.Clear();
        missiles.Clear();
        compressors.Clear();
        targets.Clear();
        aegeiros.Clear();
        stacks.Clear();
        narrowRifts.Clear();
        executions.Clear();
        march.Clear();
        stackStatusEvents.Clear();
        stackStatusWatermarks.Clear();
        partyMembers = [];
        delayedDamage.Clear();
        hasted.Clear();
        effects.Clear();
        pyreticEvents.Clear();
        pyreticWatermarks.Clear();
        bossStatusEvents.Clear();
        bossStatusWatermarks.Clear();
        ended.Clear();
        engaged.Clear();
        active.Clear();
        castOrder = 0;
        observedTime = null;
        misdirection.Clear();
    }

    private static bool Eligible(ReplayActorState actor)
        => actor.InCombat && !actor.Dead && SimpleHuntCatalog.Owns(actor.Oid);

    private static bool Changed(ReplayCast before, ReplayCast after)
    {
        // Preserve the original float TotalTime. Older recordings only have reconstructed DateTimes,
        // whose elapsed-time subtraction can vary by a few 100ns ticks between otherwise identical samples.
        var durationChanged = before.TotalTime is { } previousTotal && after.TotalTime is { } currentTotal
            ? previousTotal != currentTotal
            : Math.Abs(((before.FinishAt - before.StartedAt) - (after.FinishAt - after.StartedAt)).TotalSeconds) > .000001;
        return before.TargetId != after.TargetId || before.Location != after.Location || before.Rotation != after.Rotation || durationChanged;
    }

    private HuntPendingCast Create(ulong source, HuntDefinition definition, HuntCastRule rule,
        ReplayActorState actor, ReplayCast cast, bool packetPose, Vector3? sourcePosition, DateTime? observedAt)
    {
        var location = cast.Location;
        var rotation = rule.FollowCasterRotation ? actor.Rotation : cast.Rotation ?? 0;
        var shape = rule.Shape;
        if (rule.ChargeHalfWidth > 0)
        {
            var origin = sourcePosition ?? actor.Position;
            var direction = cast.Location - origin;
            direction.Y = 0;
            const float gridSize = 2000f / 65535f;
            const float gridSizeInv = 1f / gridSize;
            float Quantize(float value) => ((int)MathF.Round(value * gridSizeInv) - .5f) * gridSize;
            location = new(Quantize(origin.X), origin.Y, Quantize(origin.Z));
            rotation = MathF.Atan2(direction.X, direction.Z);
            shape = direction.LengthSquared() > 0
                ? RayRectangleTemplate.Symmetric(rule.ChargeHalfWidth * 2, direction.Length()) : null;
        }
        return new(source, definition, rule, location, rotation, cast.StartedAt, cast.FinishAt,
            cast.TargetId, packetPose, !packetPose, shape, cast, packetPose ? null : cast, Order: ++castOrder, ObservedAt: observedAt);
    }
}
