using System.Numerics;
using MasterPlayer.Replay;
using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;

namespace MasterPlayer.Mechanics.Dungeons;

// Owns source Actor references and component lifetimes. Event input is flushed
// before Update; changing the view or party never recreates global mechanics.
internal sealed partial class ThordanEncounter
{
    internal readonly Dictionary<ulong, ThordanActor> Actors = [];
    internal readonly List<ThordanAoe> Aoes = [];
    internal readonly List<ThordanBait> Baits = [];
    internal readonly List<ThordanHeavySequence> Heavy = [];
    internal readonly List<ThordanDisplacement> Light = [];
    internal readonly List<(ThordanActor Actor, uint Buff)> Swords = [];
    internal readonly ThordanActor?[] ChainPartners = new ThordanActor?[64];
    internal readonly ThordanWildRole[] WildRoles = new ThordanWildRole[64];
    internal ThordanActor? Primary { get; private set; }
    internal ThordanActor? Janlenoux { get; private set; }
    internal ThordanActor? Adelphel { get; private set; }
    internal ThordanActor? WildChargeSource { get; private set; }
    internal ThordanDisplacement? Faith { get; private set; }
    internal DateTime FaithNavigationAt { get; private set; }
    internal Vector3 EyePositionHint { get; private set; }
    internal bool ReducedArena { get; set; }
    internal bool Loaded { get; private set; }
    internal bool Active { get; private set; }
    internal bool Completed { get; private set; }
    internal ulong PlayerId { get; private set; }
    internal ThordanTimeline Timeline { get; }
    internal string? LastTimelineDiagnostic { get; private set; }
    private readonly Dictionary<C, int> components = [];
    private readonly List<C> componentOrder = [];
    private IReadOnlyList<ReplayPartyMember> party = [];
    private readonly Dictionary<C, float> thrustDelay = [];
    private DateTime now;
    private DateTime pierceActivation;
    private long generation;
    private readonly Action<string>? report;

    internal ThordanEncounter(Action<string>? report = null)
    {
        this.report = report;
        Timeline = new(this);
    }

    internal void ReportTimelineDiagnostic(string message)
    {
        LastTimelineDiagnostic = message;
        report?.Invoke(message);
    }
    internal int Slot => party.FirstOrDefault(p => p.EntityId == PlayerId && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal IEnumerable<(int Slot, ThordanActor Actor)> Party => party.Where(p => p.Slot is >= 0 and < 64)
        .Select(p => (p.Slot, Actor: Actors.GetValueOrDefault(p.EntityId))).Where(p => p.Actor != null)
        .Select(p => (p.Slot, p.Actor!));
    internal bool AllCometsDead => Actors.Values.Where(a => a.Actor.Oid == 0x1462).All(KnightDead);
    internal bool KnightDead(ThordanActor? actor) => actor != null && (actor.Destroyed || actor.Actor.Dead);
    internal bool Has(C component) => components.ContainsKey(component);
    internal int Count(C component) => components.GetValueOrDefault(component);
    internal IEnumerable<C> ActiveComponents => componentOrder;

    internal bool ComponentActive(C component) => component switch
    {
        C.SerZephirin => Actors.Values.Any(a => a.Actor.Oid == 0x1454 && a.Actor.Targetable && !a.Actor.Dead),
        C.CometCircle => Actors.Values.Any(a => a.Actor.Oid == 0x1462 && a.Actor.Targetable && !a.Actor.Dead),
        C.MeteorCircle => Actors.Values.Any(a => a.Actor.Oid == 0x1461 && a.Actor.Targetable && !a.Actor.Dead),
        C.SwordShieldOfTheHeavens => Swords.Any(a => !a.Actor.Destroyed && a.Actor.Actor.Targetable),
        C.HeavyImpact => Heavy.Count > 0,
        _ => Aoes.Any(a => a.Component == component) || Baits.Any(b => b.Component == component),
    };

    internal void Synchronize(IEnumerable<DungeonActor> input, IReadOnlyList<ReplayPartyMember> roster,
        ulong viewer, DateTime time, bool enabled, bool update)
    {
        if (!enabled) { Clear(); return; }
        now = time;
        PlayerId = viewer;
        var values = input.ToArray();
        var incoming = values.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values) AdvanceClock(old, time);
        foreach (var old in Actors.Values.Where(a => !incoming.TryGetValue(a.Actor.Id, out var next)
            || next.Oid != a.Actor.Oid
            || a.Actor.GameObjectId != 0 && next.GameObjectId != 0 && a.Actor.GameObjectId != next.GameObjectId
            || a.Actor.ObjectIndex != null && next.ObjectIndex != null && a.Actor.ObjectIndex != next.ObjectIndex).ToArray())
        {
            if (old.Cast is { } cast) Finish(old, cast);
            if (old.Tether is { Id: not 0 } tether) Tether(old, tether, false);
            foreach (var status in old.Statuses.OfType<ReplayStatus>()) Status(old, status, false, true);
            old.Cast = null;
            old.Tether = null;
            Array.Clear(old.Statuses);
            old.Destroyed = true;
            Actors.Remove(old.Actor.Id);
        }
        foreach (var value in values)
        {
            if (!Actors.TryGetValue(value.Id, out var actor))
            {
                actor = new(value with { Cast = null, StatusSlots = [] });
                Actors.Add(value.Id, actor);
                if (value.Oid == 0x1453 && (Primary == null || Completed))
                {
                    Timeline.Stop();
                    Primary = actor;
                    Janlenoux = Adelphel = null;
                    Loaded = Active = Completed = ReducedArena = false;
                }
                else if (Primary is { Destroyed: true } old && !Completed && old.Actor.Id == value.Id
                    && old.Actor.Oid == value.Oid)
                    Primary = actor;
                if (Active && NativeNpc(value))
                    foreach (var component in componentOrder.ToArray()) Created(component, actor);
            }
            actor.Actor = value;
            // The timeline observes every raw cast. Component callbacks only
            // receive NPC spells, including Spell↔Ability replacements.
            var nextCast = value.Cast;
            if (!SameCast(actor.Cast, nextCast))
            {
                if (actor.Cast is { } oldCast) Finish(actor, oldCast);
                actor.Cast = nextCast;
                if (nextCast != null)
                {
                    actor.Elapsed = Math.Clamp((float)(time - nextCast.StartedAt).TotalSeconds, 0, Total(nextCast));
                    actor.Clock = time;
                    if (NativeNpc(value) && nextCast.IsSpell == true)
                        foreach (var component in componentOrder.ToArray()) Start(component, actor, nextCast);
                }
            }
            var nextTether = value.FirstTether is { Id: not 0 } t ? t with { TargetId = Sanitize(t.TargetId) } : null;
            if (actor.Tether?.Id != nextTether?.Id || actor.Tether?.TargetId != nextTether?.TargetId)
            {
                if (actor.Tether is { Id: not 0 } oldTether) Tether(actor, oldTether, false);
                actor.Tether = nextTether;
                if (nextTether != null) Tether(actor, nextTether, true);
            }
            ObserveStatuses(actor);
        }
        // Input callbacks above intentionally use the previous Party slot table.
        party = roster.ToArray();
        if (!Loaded && !Completed && Primary is { Destroyed: false } primary)
        {
            if (primary.Actor.Dead) Completed = true;
            else if (Actors.GetValueOrDefault(party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0) is { } source
                && Vector3.DistanceSquared(source.Actor.Position, primary.Actor.Position) <= 500 * 500)
                Loaded = true;
        }
        if (update) Update(time);
    }

    internal void Update(DateTime time)
    {
        now = time;
        foreach (var actor in Actors.Values) AdvanceClock(actor, time);
        if (!Loaded || Completed || Primary == null) return;
        if (!Active && Primary.Actor.Targetable && Primary.Actor.InCombat)
        {
            Active = true;
            Timeline.Start(time);
        }
        if (!Active) return;
        Timeline.Update(time);
        if (Timeline.Phase >= 1)
        {
            Janlenoux ??= Actors.Values.FirstOrDefault(a => a.Actor.Oid == 0x1456);
            Adelphel ??= Actors.Values.FirstOrDefault(a => a.Actor.Oid == 0x1455);
        }
    }

    internal void Activate(C component)
    {
        Deactivate(component);
        components.Add(component, 0);
        componentOrder.Add(component);
        if (component is C.SpiralThrust1 or C.SpiralThrust2)
            thrustDelay[component] = component == C.SpiralThrust1 ? 10 : 12.1f;
        if (component == C.FaithUnmoving)
        {
            FaithNavigationAt = now.AddSeconds(4.5d);
            Faith = Actors.Values.FirstOrDefault(a => a.Actor.Oid == 0x145A) is { } grinnaux
                ? new(grinnaux.Actor.Position, 16, default) : null;
        }
        // Source component activation replays Created, spell Cast, tether and
        // status slots per actor. Historical icons/effects/animations are absent.
        foreach (var actor in Actors.Values)
        {
            if (NativeNpc(actor.Actor))
            {
                Created(component, actor);
                if (actor.Cast is { IsSpell: true } cast) Start(component, actor, cast);
            }
            if (actor.Tether is { Id: not 0 } tether) Tether(component, actor, tether, true);
            foreach (var status in actor.Statuses.OfType<ReplayStatus>()) Status(component, actor, status, true);
        }
    }

    internal void Deactivate(C component)
    {
        components.Remove(component);
        componentOrder.Remove(component);
        Aoes.RemoveAll(a => a.Component == component);
        Baits.RemoveAll(a => a.Component == component);
        thrustDelay.Remove(component);
        switch (component)
        {
            case C.HeavyImpact: Heavy.Clear(); break;
            case C.DragonsGaze: EyePositionHint = default; break;
            case C.SwordShieldOfTheHeavens: Swords.Clear(); break;
            case C.BurningChains: Array.Clear(ChainPartners); break;
            case C.HolyShieldBash: WildChargeSource = null; Array.Clear(WildRoles); break;
            case C.SpiralPierce: pierceActivation = default; break;
            case C.FaithUnmoving: Faith = null; FaithNavigationAt = default; break;
            case C.LightOfAscalon: Light.Clear(); break;
        }
    }

    internal void ClearComponents()
    {
        foreach (var component in componentOrder.ToArray()) Deactivate(component);
    }

    internal void FinishTimeline()
    {
        Active = Loaded = false;
        Completed = true;
    }

    internal void End()
    {
        Timeline.Stop();
        FinishTimeline();
    }

    internal void Clear()
    {
        End();
        Actors.Clear();
        Primary = Janlenoux = Adelphel = null;
        party = [];
        Completed = ReducedArena = false;
    }

    private void Created(C component, ThordanActor actor)
    {
        if (component == C.SwordShieldOfTheHeavens && actor.Actor.Oid is 0x1455 or 0x1456)
            Swords.Add((actor, 0));
    }

    internal DateTime Activation(ThordanActor actor)
        => actor.Cast == null ? now : now.AddSeconds((Total(actor.Cast) + .3f) - actor.Elapsed);
    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;
    private static float Total(DungeonCastObservation cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private static bool SameCast(DungeonCastObservation? a, DungeonCastObservation? b)
        => a == null ? b == null : b != null && a.Action == b.Action && a.IsSpell == b.IsSpell
            && Sanitize(a.TargetId) == Sanitize(b.TargetId) && a.Location == b.Location
            && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static bool NativeNpc(DungeonActor actor)
        => !actor.Player && !actor.Buddy && actor.NativeType is not (0x104 or 0x202 or 0x203 or 0x209);
    private static void AdvanceClock(ThordanActor actor, DateTime time)
    {
        if (actor.Cast != null && time > actor.Clock)
            actor.Elapsed = MathF.Min(actor.Elapsed + (float)(time - actor.Clock).TotalSeconds, Total(actor.Cast));
        actor.Clock = time;
    }

    private void ObserveStatuses(ThordanActor actor)
    {
        var next = new ReplayStatus?[60];
        var statuses = actor.Actor.StatusSlots ?? [];
        for (var i = 0; i < statuses.Count; ++i)
        {
            var slot = statuses[i].SlotIndex ?? i;
            if (slot is >= 0 and < 60 && statuses[i].Id != 0)
                next[slot] = statuses[i] with { SourceId = Sanitize(statuses[i].SourceId) };
        }
        for (var i = 0; i < 60; ++i)
        {
            var old = actor.Statuses[i];
            var value = next[i];
            if (old != null && value != null && old.Id == value.Id && old.Param == value.Param
                && old.SourceId == value.SourceId && (value.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1d)
            { actor.Statuses[i] = value; continue; }
            if (old != null && (old.Id != value?.Id || old.SourceId != value?.SourceId))
                Status(actor, old, false, true);
            actor.Statuses[i] = value;
            if (value != null) Status(actor, value, true, true);
        }
    }

    private void Status(ThordanActor actor, ReplayStatus status, bool gained, bool hot)
    {
        if (hot && actor.Actor.NativeType == 0x202) return;
        foreach (var component in componentOrder.ToArray()) Status(component, actor, status, gained);
    }

    private void Status(C component, ThordanActor actor, ReplayStatus status, bool gained)
    {
        if (component != C.SwordShieldOfTheHeavens || status.Id is not (944 or 945)) return;
        var index = Swords.FindIndex(a => ReferenceEquals(a.Actor, actor));
        if (index >= 0 && (gained || Swords[index].Buff == status.Id))
            Swords[index] = (actor, gained ? status.Id : 0);
    }
}
