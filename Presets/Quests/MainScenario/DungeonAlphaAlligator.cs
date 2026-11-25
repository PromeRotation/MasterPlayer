using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool UsesInterceptTethers => IsAlphaAlligator || IsBarPassage;
    private bool InterceptActive => IsAlphaAlligator || BarPassageStageStarted(0x465D);
    private bool InterceptOutputActive => IsAlphaAlligator || IsBarPassageP1;
    private uint InterceptLizardOid => IsBarPassage ? 0x4664u : 0x4635u;
    private List<AlphaActorState>? interceptTargets;
    internal bool IsAlphaAlligator => territory == MainScenarioQuestCatalog.WARMTH_OF_THE_FAMILY;
    private readonly Dictionary<ulong, AlphaActorState> alphaActors = [];
    private readonly List<(long Id, AlphaActorState Player, AlphaActorState Enemy)> alphaTethers = [];
    private readonly HashSet<ulong> alphaCompleted = [];
    private readonly Dictionary<ulong, DungeonPendingCast> alphaStarted = [];
    private List<AlphaActorState>? alphaLizards;
    private AlphaActorState? alphaBoss;
    private DateTime alphaActivation;
    private ulong alphaTetheredSlots;
    private long alphaTetherGeneration;
    private bool alphaWiping;
    internal DateTime AlphaActivation => alphaActivation;
    internal int AlphaTetherCount => alphaTethers.Count;
    private static bool AlphaEnemy(uint oid) => oid is 0x4633 or 0x4637 or 0x4635 or 0x4634 or 0x4636;

    private void AlphaInput()
    {
        if (!UsesInterceptTethers) return;
        // Feed destruction precedes all survivor updates, in historical world order.
        foreach (var id in alphaActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            var removed = alphaActors[id];
            AlphaReplaceTether(removed, default);
            Finish(id);
            removed.Removed = true;
            alphaLizards?.Remove(removed);
            barP3Mycotenders?.Remove(removed);
            interceptTargets?.Remove(removed);
            alphaActors.Remove(id);
            treasureObservedCasts.Remove(id);
            alphaCompleted.Remove(id);
        }
        foreach (var input in _actors.Values.ToArray())
        {
            var created = !alphaActors.TryGetValue(input.Id, out var state);
            if (created)
            {
                alphaCompleted.Remove(input.Id);
                alphaActors[input.Id] = state = new(input);
                if (input.Oid == InterceptLizardOid) alphaLizards?.Add(state);
                if (input.Oid == 0x465D) interceptTargets?.Add(state);
                if (input.Oid == 0x4670) barP3Mycotenders?.Add(state);
            }
            else
                _actors[input.Id] = input with { Oid = state!.Actor.Oid };
            state!.Actor = _actors[input.Id];
            var tether = state.Actor.Tethers?.FirstOrDefault(t => t.Value != 0) ?? default;
            // Live Feed creates a new actor's tether before CastInfo; an existing actor's
            // shared diffs finish/start CastInfo before replacing its tether.
            if (created) AlphaReplaceTether(state, (tether.Key, tether.Value));
            if ((Active != null || IsBarPassage) && InterceptActive) AlphaObserveCast(state.Actor);
            if (!created && state.Tether != (tether.Key, tether.Value))
                AlphaReplaceTether(state, (tether.Key, tether.Value));
        }
    }

    private void AlphaObserveCast(DungeonActor actor)
    {
        if (actor.Cast is { } cast) StartCast(actor.Id, cast, _now, fromPacket: false);
        else
        {
            questWindObserved.Remove(actor.Id);
            if (_casts.ContainsKey(actor.Id)) Finish(actor.Id);
        }
    }

    private void AlphaStarted()
    {
        if (!InterceptActive) return;
        // Component activation replays each actor's cast, then tether.
        foreach (var state in alphaActors.Values)
        {
            AlphaObserveCast(state.Actor);
            AlphaGainTether(state, state.Tether);
        }
    }

    private void AlphaObserve()
    {
        if (!IsAlphaAlligator || Active == null) return;
        // The phase predicate runs before UpdateModule first caches the 4637 reference.
        if (alphaBoss?.Actor.Dead == true || !alphaActors.Values.Any(a => AlphaEnemy(a.Actor.Oid)))
        {
            alphaCompleted.Add(PrimaryId);
            ResetFight();
            return;
        }
        alphaBoss ??= alphaActors.Values.FirstOrDefault(a => a.Actor.Oid == 0x4637);
    }

    private (AlphaActorState Player, AlphaActorState Enemy, int Slot)? AlphaSides(
        AlphaActorState source, (uint Type, ulong Target) tether)
    {
        if (tether.Type is not (84 or 17) || !alphaActors.TryGetValue(tether.Target, out var target)) return null;
        var sourceInRaid = !source.Removed && !source.Actor.Dead && source.Actor.PartySlot is >= 0 and < 64;
        var player = sourceInRaid ? source : target;
        var enemy = sourceInRaid ? target : source;
        return (player, enemy, player.Actor.PartySlot);
    }

    private void AlphaGainTether(AlphaActorState source, (uint Type, ulong Target) tether)
    {
        if (!InterceptActive || !IsBarPassage && Active == null || AlphaSides(source, tether) is not { } sides) return;
        alphaTethers.Add((++alphaTetherGeneration, sides.Player, sides.Enemy));
        if (sides.Slot is >= 0 and < 64) alphaTetheredSlots |= 1ul << sides.Slot;
    }

    private void AlphaReplaceTether(AlphaActorState source, (uint Type, ulong Target) tether)
    {
        if (InterceptActive && (IsBarPassage || Active != null) && AlphaSides(source, source.Tether) is { } old)
        {
            var index = alphaTethers.FindIndex(t => ReferenceEquals(t.Player, old.Player) && ReferenceEquals(t.Enemy, old.Enemy));
            if (index >= 0) alphaTethers.RemoveAt(index);
            // The source clears a bit directly, even if another tether still uses the slot.
            if (old.Slot is >= 0 and < 64) alphaTetheredSlots &= ~(1ul << old.Slot);
        }
        source.Tether = tether;
        AlphaGainTether(source, tether);
    }

    private void AlphaTether(ulong source, ulong target, uint type, bool present)
    {
        if (!UsesInterceptTethers || !alphaActors.TryGetValue(source, out var actor)) return;
        // Typed events execute OpTether too; equal values still produce Lose then Gain.
        AlphaReplaceTether(actor, present ? (type, target) : default);
    }

    private void AlphaCast(DungeonPendingCast cast)
    {
        if (!InterceptActive) return;
        var correction = alphaStarted.TryGetValue(cast.Source, out var previous)
            && previous.Rule.Action == cast.Rule.Action && previous.StartedAt == cast.StartedAt;
        if ((IsBarPassage ? cast.Rule.Action == 41125 : cast.Rule.Action == 40562) && !correction)
            alphaActivation = cast.FinishAt.AddSeconds(IsBarPassage ? 1.1 : 1.2);
        alphaStarted[cast.Source] = cast;
    }

    private void AlphaFinish(DungeonPendingCast cast)
    {
        if (InterceptActive) alphaStarted.Remove(cast.Source);
    }

    private IEnumerable<DungeonAmbientAoe> AlphaAoes()
    {
        if (!InterceptOutputActive || Active == null || Arena == null || alphaTethers.Count == 0) yield break;
        // Module.Enemies creates a lazy list, then appends new actors after surviving entries.
        alphaLizards ??= alphaActors.Values.Where(a => a.Actor.Oid == InterceptLizardOid).ToList();
        var middle = IsBarPassageP1 ? new Vector2(13, -302) : new Vector2(403, -105);
        var source = alphaLizards.FirstOrDefault(a =>
            MathF.Abs(a.Actor.Position.X - middle.X) <= 1 && MathF.Abs(a.Actor.Position.Z - middle.Y) <= 1);
        if (source == null) yield break;
        AlphaActorState? target;
        if (IsBarPassageP1)
        {
            interceptTargets ??= alphaActors.Values.Where(a => a.Actor.Oid == 0x465D).ToList();
            target = interceptTargets.FirstOrDefault();
        }
        else
            alphaActors.TryGetValue(source.Tether.Target, out target);
        // The source indexes [0] without a missing-target branch. Do not invent
        // a new corridor when that required actor cannot be resolved.
        if (target == null) yield break;
        var delta = source.Actor.Position - target.Actor.Position;
        delta.Y = 0;
        var length = delta.Length();
        var direction = length > 0 ? delta / length : Vector3.Zero;
        var from = target.Actor.Position + (target.Actor.HitboxRadius + .1f) * direction;
        var to = source.Actor.Position;
        var segmentLength = Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
        // Upstream divides by zero here and produces NaN distances. Keep the actor/tether
        // model and display, but do not invent a finite navigation corridor or throw.
        if (!(segmentLength > 0)) yield break;
        var shape = RayRectangleTemplate.Symmetric(1, segmentLength);
        yield return new("alpha/intercept", IsBarPassageP1 ? "Toxic Spit" : "Feeding Time", shape,
            from, MathF.Atan2(to.X - from.X, to.Z - from.Z), Safe: true, Display: false, Activation: alphaActivation, GoalPriority: 1);
    }

    internal IEnumerable<(string Id, Vector3 Start, Vector3 End, bool Safe)> AlphaLines()
    {
        if (!InterceptOutputActive || Active == null) yield break;
        if (IsBarPassageP1 && alphaTethers.Count > 0)
            interceptTargets ??= alphaActors.Values.Where(a => a.Actor.Oid == 0x465D).ToList();
        foreach (var tether in alphaTethers)
        {
            var player = tether.Player;
            var safe = !player.Removed && !player.Actor.Dead && player.Actor.PartySlot is >= 0 and < 64
                && player.Actor.Oid != (IsBarPassageP1 ? 0x465Du : 0x4632u);
            yield return ($"alpha/tether/{tether.Id}", tether.Enemy.Actor.Position, player.Actor.Position, safe);
        }
    }

    private IEnumerable<string> AlphaHints(Vector3? player)
    {
        if (!InterceptOutputActive || Active == null) yield break;
        var slot = _actors.GetValueOrDefault(PlayerId)?.PartySlot ?? -1;
        if (alphaTethers.Count > 0 && (slot is < 0 or >= 64 || (alphaTetheredSlots & (1ul << slot)) == 0))
            yield return "Grab the tether!";
        if (player is { } p && _casts.Values.Any(c => BarPassageCastVisible(c) && (IsBarPassageP1 ? c.Rule.Shape != null : c.Rule.Action == 40563) && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
    }

    private void AlphaDirector(uint update)
    {
        if (!IsAlphaAlligator || update is not (0x40000005 or 0x40000011)) return;
        alphaWiping = update == 0x40000005;
        if (!alphaWiping) return;
        ResetFight();
        alphaCompleted.Clear();
    }

    private void ResetAlpha()
    {
        alphaTethers.Clear();
        alphaTetheredSlots = 0;
        alphaActivation = default;
        alphaBoss = null;
        alphaStarted.Clear();
        alphaLizards = null;
        interceptTargets = null;
    }
}
