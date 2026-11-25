using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// Native Q1 model. The incomplete upstream branches stay explicit and receive
// no extra mechanics inferred from action names.
internal sealed partial class QuantumEncounter
{
    internal static readonly Vector3 CENTER = new(-600, 0, -300);
    internal readonly Dictionary<ulong, QuantumActor> Actors = [];
    internal QuantumActor? Primary { get; private set; }
    internal QuantumActor? Eater { get; private set; }
    internal QuantumTimeline Timeline { get; private set; } = new();
    internal QuantumScourgingBlaze? Blaze { get; private set; }
    internal IGeometryShape Arena { get; private set; } = QuantumGeometry.RestoredFloor;
    internal bool Enabled { get; private set; }
    private bool moduleLoaded;
    private bool Constructed => Primary != null && Timeline.Stage < 13 && Enabled;
    internal bool Active => Loaded && Timeline.Stage >= 0;
    internal bool Loaded => Constructed && moduleLoaded;
    internal uint Level { get; private set; }
    internal ulong PlayerId { get; private set; }
    private IReadOnlyList<ReplayPartyMember> party = [];
    private readonly List<QuantumAoe> ordinary = [];
    private readonly List<QuantumAoe> small = [];
    private bool pullActive;
    private bool smallActive;
    private bool endActive;
    private bool stackActive;
    private bool towerActive;
    private int pullCasts;
    private int smallCasts;
    private int endCasts;
    private int stackCasts;
    private int towerCasts;
    private long generation;
    private DateTime now;

    internal void Synchronize(IEnumerable<DungeonActor> input, IReadOnlyList<ReplayPartyMember> roster,
        ulong playerId, DateTime time, bool enabled, bool update = true)
    {
        now = time;
        PlayerId = playerId;
        if (!enabled)
        {
            Clear();
            Enabled = false;
            return;
        }
        Enabled = true;
        var values = input.ToArray();
        var present = values.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values.Where(a => !present.TryGetValue(a.Actor.Id, out var next) || next.Oid != a.Actor.Oid).ToArray())
        {
            if (old.Cast != null) CastFinished(old, old.Cast);
            for (var i = 0; i < old.Statuses.Length; ++i)
                if (old.Statuses[i] is { } status) StatusChanged(old, status, false, true);
            Array.Clear(old.Statuses);
            old.Cast = null;
            old.Destroyed = true;
            Actors.Remove(old.Actor.Id);
        }
        foreach (var value in values)
        {
            var created = !Actors.TryGetValue(value.Id, out var state);
            if (created)
            {
                state = new(value with { Cast = null, StatusSlots = [], Statuses = null });
                Actors.Add(value.Id, state);
                // ActorAdded rebinds an existing loaded module for the same
                // InstanceID/OID before its next state-machine update.
                if (Primary is { Destroyed: true } previous && Timeline.Stage < 13
                    && previous.Actor.Id == value.Id && previous.Actor.Oid == value.Oid)
                    Primary = state;
                else if (value.Oid == 0x48EE && (Primary == null || Timeline.Stage == 13))
                    ConstructPrimary(state);
            }
            var actor = state!;
            actor.Actor = value;
            AdvanceClock(actor, time);
            if (created && Active && NativeNpc(value)) Blaze?.Created(value);
            var nextCast = NativeNpc(value) ? value.Cast : null;
            if (!SameCast(actor.Cast, nextCast))
            {
                if (actor.Cast is { } old) CastFinished(actor, old);
                actor.Cast = nextCast;
                if (nextCast != null)
                {
                    actor.CastElapsed = Math.Clamp((float)(time - nextCast.StartedAt).TotalSeconds, 0, CastTotal(nextCast));
                    actor.CastClockAt = time;
                    CastStarted(actor, nextCast);
                }
            }
            ObserveStatuses(actor);
        }
        party = roster.ToArray();
        if (!moduleLoaded && Constructed)
        {
            if (Primary!.Destroyed || Primary.Actor.Dead)
            {
                Timeline.Finish(ExitStage);
            }
            else if (Actors.GetValueOrDefault(roster.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0) is { } source
                && Vector3.DistanceSquared(Primary.Actor.Position, source.Actor.Position) <= 500 * 500)
                moduleLoaded = true;
        }
        if (update) Update(time);
    }

    private void ConstructPrimary(QuantumActor actor)
    {
        Primary = actor;
        moduleLoaded = false;
        Timeline = new();
        Level = 0;
        Eater = null;
        ordinary.Clear();
        small.Clear();
        pillars.Clear();
        towers.Clear();
        stacks.Clear();
        Blaze = null;
        endAoe = null;
        pullActive = smallActive = endActive = stackActive = towerActive = false;
        Arena = QuantumGeometry.RestoredFloor;
        // Construction is inside ActorAdded: bootstrap only the world already
        // committed at that point, with the previous Party table.
        BootstrapLight();
    }

    internal void Update(DateTime time)
    {
        now = time;
        foreach (var actor in Actors.Values) AdvanceClock(actor, time);
        if (!Loaded) return;
        if (Timeline.Stage == -1)
        {
            // This reference is intentionally cached only during CheckPull.
            Eater ??= Actors.Values.FirstOrDefault(a => a.Actor.Oid == 0x48EF);
            if (Primary!.Actor.Targetable && Primary.Actor.InCombat)
            {
                Timeline.Begin(now);
                BootstrapOrdinary();
            }
        }
        if (Active && (Primary!.Destroyed || Primary.Actor.Dead || Primary.Actor.Hp == 0))
        {
            Timeline.Finish(ExitStage);
            ordinary.Clear();
            small.Clear();
            Blaze = null;
            endAoe = null;
            stacks.Clear();
            towers.Clear();
            return;
        }
        if (!Active)
        {
            UpdateHp();
            return;
        }
        Timeline.Advance(now, Level, Condition, EnterStage, ExitStage);
        // Match BossModule: state transitions run before component Update.
        if (Timeline.Stage == 0 && Eater != null && Level != 40)
            Level = Extra(Eater, 4572) + Extra(Primary!, 4571) + Extra(Primary!, 1018)
                + Extra(Primary!, 4570) + Extra(Primary!, 586);
        UpdateHp();
        Blaze?.Update(now);
    }

    private bool Condition(int stage) => stage switch
    {
        1 => Blaze?.Direction != 0,
        2 => Blaze?.Direction == 2,
        3 => pullCasts != 0,
        4 => smallCasts != 0,
        5 => smallCasts == 12,
        6 => endCasts != 0 && pillars.Count == 0,
        7 => stackCasts != 0,
        8 => towerCasts != 0,
        9 => Blaze?.NumCasts != 0,
        10 => Blaze?.Lines.Count == 0,
        _ => false,
    };

    private void EnterStage(int stage)
    {
        switch (stage)
        {
            case 1:
                Blaze = new();
                foreach (var actor in Actors.Values)
                {
                    if (!NativeNpc(actor.Actor)) continue;
                    Blaze.Created(actor.Actor);
                    if (actor.Cast != null) Blaze.Start(actor.Actor, CaptureCast(actor));
                }
                break;
            case 3:
                pullCasts = towerCasts = endCasts = 0;
                pullActive = towerActive = endActive = true;
                forbidden = ulong.MaxValue;
                towers.Clear();
                pillars.Clear();
                endAoe = null;
                foreach (var actor in Actors.Values)
                    foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                        TowerStatus(actor, status.Id, true);
                break;
            case 4:
                stackActive = smallActive = true;
                stackCasts = smallCasts = 0;
                stacks.Clear();
                small.Clear();
                foreach (var actor in Actors.Values)
                    if (actor.Cast is { Action: 44122 }) AddOrdinary(small, actor, CaptureCast(actor), 3);
                break;
        }
    }

    private void ExitStage(int stage)
    {
        switch (stage)
        {
            case 3: pullActive = false; break;
            case 5: smallActive = false; small.Clear(); break;
            case 6: endActive = false; endAoe = null; break;
            case 7: stackActive = false; stacks.Clear(); break;
            case 8:
                Blaze?.ShowAoes();
                towerActive = false;
                towers.Clear();
                break;
            case 10: Blaze = null; break;
        }
    }

    private void BootstrapOrdinary()
    {
        foreach (var actor in Actors.Values)
            if (actor.Cast is { Action: 45117 or 44098 or 45200 })
                AddOrdinary(ordinary, actor, CaptureCast(actor), 6);
    }

    private void CastStarted(QuantumActor actor, DungeonCastObservation cast)
    {
        if (!Constructed) return;
        cast = CaptureCast(actor);
        LightCast(cast);
        if (!Active) return;
        if (cast.Action is 45117 or 44098 or 45200) AddOrdinary(ordinary, actor, cast, 6);
        if (smallActive && cast.Action == 44122) AddOrdinary(small, actor, cast, 3);
        Blaze?.Start(actor.Actor, cast);
    }

    private void CastFinished(QuantumActor actor, DungeonCastObservation cast)
    {
        if (!Constructed) return;
        LightFinish(cast.Action);
        if (!Active) return;
        RemoveFirst(ordinary, actor);
        if (smallActive && cast.Action == 44122) RemoveFirst(small, actor);
        if (endActive && endAoe == null && cast.Action == 44122)
        {
            var center = DungeonEncounter.QuantizedDungeonPosition(Arena.Position);
            var offset = center - actor.Actor.Position;
            var (sin, cos) = Math.SinCos(actor.Actor.Rotation);
            var circle = offset.X * (float)sin + offset.Z * (float)cos > 0;
            endAoe = new(++generation, actor, cast.Action,
                circle ? new CircleTemplate(8) : new DonutTemplate(8, 30), center, 0, now.AddSeconds(6d));
        }
    }

    internal void Effect(ulong source, uint action, Vector3? target, DateTime time)
    {
        now = time;
        if (!Active || !Actors.TryGetValue(source, out var actor) || !NativeNpc(actor.Actor)) return;
        if (action != 44119 || target != null)
            Blaze?.Effect(actor.Actor, action, target ?? default, now);
        if (pullActive && action == 44121) ++pullCasts;
        if (smallActive && action == 44122) ++smallCasts;
        if (endActive && action is 44123 or 44124)
        {
            endAoe = null;
            ++endCasts;
        }
    }

    internal void Icon(ulong source, uint icon, DateTime time)
    {
        if (stackActive && icon == 78 && Actors.TryGetValue(source, out var actor))
            stacks.Add((++generation, actor, time.AddSeconds(5.1d)));
    }

    internal void Vfx(uint id)
    {
        if (stackActive && id == 40) ++stackCasts;
    }

    private void AddOrdinary(List<QuantumAoe> list, QuantumActor actor, DungeonCastObservation cast, float radius)
    {
        list.Add(new(++generation, actor, cast.Action, new CircleTemplate(radius), cast.Location, cast.Rotation ?? 0, cast.FinishAt));
        if (list.Count >= 99) DungeonAoeOrder.Sort(list, a => a.Activation);
    }

    private static void RemoveFirst(List<QuantumAoe> list, QuantumActor actor)
    {
        var index = list.FindIndex(a => ReferenceEquals(a.Source, actor));
        if (index >= 0) list.RemoveAt(index);
    }

    private static bool SameCast(DungeonCastObservation? first, DungeonCastObservation? second)
        => first == null ? second == null : second != null && first.Action == second.Action
            && Sanitize(first.TargetId) == Sanitize(second.TargetId) && first.Rotation == second.Rotation
            && CastTotal(first) == CastTotal(second) && first.Location == second.Location;

    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;

    private static float CastTotal(DungeonCastObservation cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);

    private static void AdvanceClock(QuantumActor actor, DateTime time)
    {
        if (actor.Cast != null && time > actor.CastClockAt)
        {
            // The matching client's nine Q1 input actions have no extra cast
            // time; see the action-table evidence in the encounter specification.
            actor.CastElapsed = MathF.Min(actor.CastElapsed + (float)(time - actor.CastClockAt).TotalSeconds,
                CastTotal(actor.Cast));
        }
        actor.CastClockAt = time;
    }

    private DungeonCastObservation CaptureCast(QuantumActor actor)
        => actor.Cast! with { FinishAt = now.AddSeconds((CastTotal(actor.Cast!) + .3f) - actor.CastElapsed) };

    private static bool NativeNpc(DungeonActor actor) => !actor.Player && !actor.Buddy
        && actor.NativeType is not (0x104 or 0x202 or 0x203 or 0x209);

    private static uint Extra(QuantumActor actor, uint id)
        => actor.Statuses.FirstOrDefault(s => s?.Id == id)?.Param ?? (uint)0;

    internal void EndModule()
    {
        Timeline.Finish(ExitStage);
        ordinary.Clear();
        small.Clear();
        Blaze = null;
        towers.Clear();
        stacks.Clear();
        endAoe = null;
    }

    private void ObserveStatuses(QuantumActor actor)
    {
        var next = new ReplayStatus?[60];
        var statuses = actor.Actor.StatusSlots ?? (actor.Actor.Statuses ?? []).Select((id, i) => new ReplayStatus(id,
            actor.Actor.StatusParams?.GetValueOrDefault(id) ?? 0,
            actor.Actor.StatusSources?.GetValueOrDefault(id) ?? 0,
            actor.Actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = i }).ToArray();
        for (var i = 0; i < statuses.Count; ++i)
        {
            var status = statuses[i];
            var slot = status.SlotIndex ?? i;
            if (slot is >= 0 and < 60 && status.Id != 0)
                next[slot] = status with { SourceId = Sanitize(status.SourceId) };
        }
        for (var i = 0; i < next.Length; ++i)
        {
            var old = actor.Statuses[i];
            var value = next[i];
            if (old == null && value == null) continue;
            if (old != null && value != null && old.Id == value.Id && old.SourceId == value.SourceId
                && old.Param == value.Param && (value.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1d)
            {
                actor.Statuses[i] = value;
                continue;
            }
            if (old != null && (old.Id != value?.Id || old.SourceId != value?.SourceId))
                StatusChanged(actor, old, false, true);
            actor.Statuses[i] = value;
            if (value != null) StatusChanged(actor, value, true, true);
        }
    }

    internal void Clear()
    {
        Actors.Clear();
        Primary = Eater = null;
        moduleLoaded = false;
        Timeline = new();
        Blaze = null;
        ordinary.Clear();
        small.Clear();
        pillars.Clear();
        towers.Clear();
        stacks.Clear();
        pullActive = smallActive = endActive = stackActive = towerActive = false;
        endAoe = null;
        Level = 0;
        party = [];
        ResetLight();
        Arena = QuantumGeometry.RestoredFloor;
    }
}
