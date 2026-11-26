using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Fates;

internal sealed partial class CrescentFateEncounter(CrescentFateDefinition definition)
{
    internal CrescentFateDefinition Definition { get; } = definition;
    internal Dictionary<ulong, CrescentFateActor> Actors { get; } = [];
    internal List<CrescentFateCast> Casts { get; } = [];
    internal CrescentFateActor? Primary { get; private set; }
    internal bool Active { get; private set; }
    internal bool Completed { get; private set; }
    internal Vector3 Center { get; private set; }
    internal float Radius { get; private set; } = 30;
    internal string? Diagnostic { get; private set; }
    private uint cachedFateId;
    private bool bigFanSeen;
    private long nextId;
    private DateTime now;
    private ulong previousSourcePlayerId;
    private readonly List<CrescentFateActor> pending = [];

    internal void Synchronize(IEnumerable<ReplayActorState> input, ReplayActiveFate? fate,
        ulong sourcePlayerId, DateTime time, bool updateModules = true)
    {
        Advance(time);
        var promotePending = Primary == null || Completed;
        // Feed creates actors before submitting Party[0]. Only a player from the
        // previous world frame can make LoadModule load immediately on creation.
        var sourceSlotKnown = sourcePlayerId != 0 && previousSourcePlayerId == sourcePlayerId;
        previousSourcePlayerId = sourcePlayerId;
        var observations = input.ToArray();
        var present = observations.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values.Where(a => !present.TryGetValue(a.Actor.Id, out var next)
            || NewActor(a.Actor, next)).ToArray())
        {
            if (old.Cast is { } previous) Finish(old, previous);
            old.Cast = null;
            old.Destroyed = true;
            Actors.Remove(old.Actor.Id);
        }
        foreach (var actor in observations)
        {
            if (!Actors.TryGetValue(actor.Id, out var shell))
            {
                shell = new(actor);
                Actors.Add(actor.Id, shell);
                if (Active && NativeNpc(actor)) Created(shell);
                if (actor.Oid == Definition.PrimaryOid)
                {
                    if (Primary is { Destroyed: true } previous && !Completed && previous.Actor.Id == actor.Id)
                    {
                        // Manager rebinds a loaded same-ID/OID primary on creation;
                        // components retain their own old actor references.
                        Primary = shell;
                    }
                    else if (promotePending && sourceSlotKnown
                        && Actors.GetValueOrDefault(sourcePlayerId) is { Destroyed: false } loadObserver
                        && Vector3.DistanceSquared(loadObserver.Actor.Position, actor.Position) <= 500 * 500)
                    {
                        Reset();
                        Primary = shell;
                        Completed = false;
                        promotePending = false;
                    }
                    else pending.Add(shell);
                }
            }
            shell.Actor = actor with { TargetId = Sanitize(actor.TargetId) };
            var cast = NativeNpc(actor) && actor.Cast?.IsSpell == true ? actor.Cast : null;
            if (cast != null) cast = cast with { TargetId = Sanitize(cast.TargetId) };
            if (!SameCast(shell.Cast, cast))
            {
                if (shell.Cast is { } previous) Finish(shell, previous);
                shell.Cast = cast;
                shell.EventHappened = cast?.EffectSeen == true;
                if (cast != null)
                {
                    shell.Elapsed = Math.Clamp((float)(time - cast.StartedAt).TotalSeconds, 0, AdjustedTotal(cast));
                    shell.Clock = time;
                    if (Active) Start(shell);
                }
            }
            else if (cast?.EffectSeen == true)
            {
                // The sampled response may arrive after the native effect callback.
                // Keep history on this original cast without restarting its clock.
                shell.EventHappened = true;
                shell.Cast = shell.Cast! with { EffectSeen = true };
            }
            ObserveStatuses(shell);
        }
        // Raw callbacks may arrive several times between Framework updates. They
        // update actors/components, while manager retirement/promotion runs once.
        if (!updateModules) return;
        pending.RemoveAll(a => a.Destroyed || a.Actor.Dead);
        if (promotePending && Actors.GetValueOrDefault(sourcePlayerId) is { } observer)
            for (var i = pending.Count - 1; i >= 0; --i)
            {
                var candidate = pending[i];
                if (Vector3.DistanceSquared(observer.Actor.Position, candidate.Actor.Position) > 500 * 500) continue;
                Reset();
                Primary = candidate;
                Completed = false;
                pending.RemoveAt(i);
                break;
            }
        Diagnostic = fate == null ? "录像缺少当前FATE原始身份、中心和半径，未猜测场地" : null;
        if (Primary is not { } primary) return;
        var player = Actors.GetValueOrDefault(sourcePlayerId)?.Actor;
        if (primary.Destroyed || primary.Actor.Dead || primary.Actor.Hp == 0)
        {
            Reset();
            Completed = true;
            return;
        }
        if (Completed || fate == null)
        {
            Reset();
            return;
        }
        if (!Definition.ProtectedPot) UpdateArena(fate);
        if (!Active)
        {
            if (player != null && Vector3.DistanceSquared(player.Position, primary.Actor.Position) > 500 * 500)
            {
                ReturnToPending(primary);
                return;
            }
            var pull = Definition.ProtectedPot
                ? fate.Id == Definition.FateId && fate.Radius > 0 && InCircle(player, fate.Center, fate.Radius)
                    && Actors.Values.Any(a => a.Actor.Oid == Definition.PrimaryOid)
                : primary.Actor.Targetable && primary.Actor.InCombat && InCircle(player, Center, Radius);
            if (!pull) return;
            Active = true;
            // BossModule.ActivateComponent cold-broadcasts all native Spell casts,
            // in world insertion order, independently for each component.
            foreach (var rule in Definition.Rules)
                foreach (var shell in Actors.Values)
                {
                    if (NativeNpc(shell.Actor)) Created(shell, rule);
                    if (shell.Cast != null) Start(shell, rule);
                    foreach (var status in shell.Statuses.OfType<ReplayStatus>()) Gain(shell, status, rule);
                }
        }
        UpdateArena(fate);
        var reset = false;
        if (Definition.ProtectedPot)
        {
            var bigExists = Actors.Values.Any(a => a.Actor.Oid == 0x4D8E);
            bigFanSeen |= bigExists;
            reset = bigFanSeen && !bigExists;
            if (!reset && fate.Id == Definition.FateId && fate.Radius > 0)
                reset = !InCircle(player, fate.Center, fate.Radius + 10);
            else if (!reset)
                reset = BaseReset(primary, player, fate);
        }
        else reset = BaseReset(primary, player, fate);
        if (reset)
        {
            Reset();
            if (player != null && Vector3.DistanceSquared(player.Position, primary.Actor.Position) > 500 * 500)
                ReturnToPending(primary);
        }
        if (Active) Update();
    }

    private void ReturnToPending(CrescentFateActor primary)
    {
        Reset();
        pending.Add(primary);
        Primary = null;
    }

    private bool BaseReset(CrescentFateActor primary, ReplayActorState? player, ReplayActiveFate fate)
        => !primary.Actor.InCombat || cachedFateId != 0 && fate.Id != cachedFateId
            || !InCircle(player, Center, Radius + 10);

    private void UpdateArena(ReplayActiveFate fate)
    {
        var primary = Primary!.Actor;
        if (fate.Id != 0 && fate.Radius > 0
            && (primary.FateId is > 0 ? primary.FateId == fate.Id : Definition.FateId == fate.Id))
        {
            if (cachedFateId != fate.Id || MathF.Abs(Center.X - fate.Center.X) > .01f
                || MathF.Abs(Center.Z - fate.Center.Z) > .01f
                || MathF.Abs(Radius - fate.Radius) > .01f)
            {
                cachedFateId = fate.Id;
                Center = fate.Center;
                Radius = fate.Radius;
            }
        }
        if (cachedFateId == 0) Center = primary.Position;
    }

    private void Start(CrescentFateActor source, CrescentFateRule? only = null)
    {
        if (source.Cast is not { } cast) return;
        foreach (var rule in Definition.Rules)
            if ((only == null || ReferenceEquals(rule, only)) && rule.Actions.Contains(cast.ActionId))
            {
                if (StartVariant(source, rule)) continue;
                Casts.Add(new(++nextId, source, rule, cast.ActionId, cast.Location,
                    cast.Rotation ?? source.Actor.Rotation, Activation(source), cast.StartedAt));
                var group = Casts.Where(c => ReferenceEquals(c.Rule, rule)).ToList();
                if (rule.Actions.Length == 1 || group.Count < 99) continue;
                // Reuse the MP source-order adapter: equal clocks are intentionally
                // right-pivot ordered, and only this component's Start triggers sorting.
                DungeonAoeOrder.Sort(group, c => c.Activation);
                var next = 0;
                for (var i = 0; i < Casts.Count; ++i)
                    if (ReferenceEquals(Casts[i].Rule, rule)) Casts[i] = group[next++];
            }
    }

    internal void Finish(CrescentFateActor source, ReplayCast cast)
    {
        foreach (var rule in Definition.Rules)
        {
            if (FinishVariant(source, cast, rule)) continue;
            if (rule.Actions.Length == 1 && !rule.Actions.Contains(cast.ActionId)) continue;
            var index = Casts.FindIndex(c => ReferenceEquals(c.Rule, rule)
                && (rule.Kind == CrescentFateRuleKind.Raidwide
                    ? ReferenceEquals(c.Source, source) : c.Source.Actor.Id == source.Actor.Id));
            if (index >= 0) Casts.RemoveAt(index);
        }
    }

    internal DateTime Activation(CrescentFateActor source, double extraDelay = 0)
        => source.Cast == null ? default : now.AddSeconds((Total(source.Cast) + .3f - source.Elapsed) + extraDelay);

    internal void Advance(DateTime time)
    {
        now = time;
        foreach (var old in Actors.Values)
        {
            if (old.Cast != null && time > old.Clock)
                old.Elapsed = MathF.Min(old.Elapsed + (float)(time - old.Clock).TotalSeconds, AdjustedTotal(old.Cast));
            old.Clock = time;
        }
    }

    internal CrescentFateCast[] Gazes(ReplayActorState player)
        => Casts.Where(c => c.Rule.Kind == CrescentFateRuleKind.Gaze
            && Vector2.DistanceSquared(Xz(player.Position), Xz(c.Origin)) <= c.Rule.GazeRange * c.Rule.GazeRange).ToArray();

    internal float? Facing(ReplayActorState player)
        => DungeonFacingDirections.Solve(Gazes(player).Where(c => c.Activation <= now.AddSeconds(.75))
            .Select(c => (MathF.Atan2(c.Origin.X - player.Position.X, c.Origin.Z - player.Position.Z), MathF.PI / 4)).ToArray(),
            player.Rotation);

    internal void Reset()
    {
        Active = false;
        Casts.Clear();
        concentricDisplay.Clear();
        freefallSequences.Clear();
        puddleSequences.Clear();
        cachedFateId = 0;
        Radius = 30;
        Center = Primary?.Actor.Position ?? default;
        bigFanSeen = false;
    }

    internal void Clear()
    {
        Reset();
        Actors.Clear();
        pending.Clear();
        Primary = null;
        Completed = false;
        Diagnostic = null;
        previousSourcePlayerId = 0;
    }

    private static Vector2 Xz(Vector3 point) => new(point.X, point.Z);
    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;
    private static bool InCircle(ReplayActorState? player, Vector3 center, float radius)
        => player != null && Vector2.DistanceSquared(Xz(player.Position), Xz(center)) <= radius * radius;
    private static float Total(ReplayCast cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private static float AdjustedTotal(ReplayCast cast) => Total(cast) + CrescentFateCatalog.ExtraCastTime(cast.ActionId);
    private static bool SameCast(ReplayCast? a, ReplayCast? b)
        => a == null ? b == null : b != null && a.ActionId == b.ActionId && a.TargetId == b.TargetId
            && a.Location == b.Location && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static bool NewActor(ReplayActorState a, ReplayActorState b)
        => a.Oid != b.Oid || a.GameObjectId != 0 && b.GameObjectId != 0 && a.GameObjectId != b.GameObjectId
            || a.ObjectIndex != null && b.ObjectIndex != null && a.ObjectIndex != b.ObjectIndex;
    internal static bool NativeNpc(ReplayActorState actor)
        => actor.Kind != 1 && !(actor.Kind == 2 && actor.SubKind is 2 or 3 or 9);
}
