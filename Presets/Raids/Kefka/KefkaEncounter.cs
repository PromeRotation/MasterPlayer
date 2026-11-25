using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// The source's fourteen components share a single trivial combat phase. Actor
// objects outlive removal so targeted markers keep the original reference.
internal sealed partial class KefkaEncounter
{
    internal readonly Dictionary<ulong, KefkaActor> Actors = [];
    internal readonly List<KefkaMechanic> Mechanics = [];
    internal KefkaActor? Primary { get; private set; }
    internal KefkaMechanic? Ruin { get; private set; }
    internal bool Loaded { get; private set; }
    internal bool Active { get; private set; }
    internal bool Completed { get; private set; }
    internal ulong PlayerId { get; private set; }
    private IReadOnlyList<ReplayPartyMember> party = [];
    private readonly DateTime[,] immunities = new DateTime[64, 3];
    private DateTime now;
    private long generation;

    internal int Slot => party.FirstOrDefault(p => p.EntityId == PlayerId && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal IEnumerable<KefkaActor> Party => party.Where(p => p.Slot is >= 0 and < 64)
        .Select(p => Actors.GetValueOrDefault(p.EntityId)).OfType<KefkaActor>();

    internal void Synchronize(IEnumerable<DungeonActor> input, IReadOnlyList<ReplayPartyMember> roster,
        ulong viewer, DateTime time, bool enabled, bool update)
    {
        if (!enabled) { Clear(); return; }
        now = time;
        PlayerId = viewer;
        var values = input.ToArray();
        var present = values.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values)
            AdvanceClock(old, time);
        foreach (var old in Actors.Values.Where(a => !present.TryGetValue(a.Actor.Id, out var next)
            || next.Oid != a.Actor.Oid).ToArray())
        {
            if (old.Cast is { } cast) Finish(old, cast);
            foreach (var status in old.Statuses.OfType<ReplayStatus>()) Status(old, status, false, true);
            old.Cast = null;
            Array.Clear(old.Statuses);
            old.Destroyed = true;
            Actors.Remove(old.Actor.Id);
        }
        foreach (var value in values)
        {
            var created = !Actors.TryGetValue(value.Id, out var actor);
            if (created)
            {
                actor = new(value with { Cast = null, Statuses = [], StatusSlots = [] });
                Actors.Add(value.Id, actor);
                if (Primary is { Destroyed: true } previous && !Completed
                    && previous.Actor.Id == value.Id && previous.Actor.Oid == value.Oid)
                    Primary = actor;
                else if (value.Oid == 0x2153 && (Primary == null || Completed))
                {
                    Primary = actor;
                    Loaded = Active = Completed = false;
                    Mechanics.Clear();
                    Ruin = null;
                    Array.Clear(immunities);
                }
            }
            actor!.Actor = value;
            var nextCast = NativeNpc(value) ? value.Cast : null;
            if (!SameCast(actor.Cast, nextCast))
            {
                if (actor.Cast is { } oldCast) Finish(actor, oldCast);
                actor.Cast = nextCast;
                if (nextCast != null)
                {
                    actor.Elapsed = Math.Clamp((float)(time - nextCast.StartedAt).TotalSeconds, 0, Total(nextCast));
                    actor.ClockAt = time;
                    if (Active) Start(actor);
                }
            }
            ObserveStatuses(actor);
        }
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
            // Component activation replays each actor's Cast and real status slots.
            foreach (var actor in Actors.Values)
            {
                if (actor.Cast != null && NativeNpc(actor.Actor)) Start(actor);
                foreach (var status in actor.Statuses.OfType<ReplayStatus>()) Status(actor, status, true, false);
            }
        }
        if (Active && (Primary.Destroyed || Primary.Actor.Dead || Primary.Actor.Hp == 0)) End();
    }

    private void Start(KefkaActor actor)
    {
        var cast = actor.Cast!;
        var activation = Activation(actor);
        var rotation = cast.Rotation ?? 0;
        if (cast.Action == 10530 && Primary != null
            && !AlmostEqual(actor.Actor.Position, Primary.Actor.Position, 1))
            Ruin = new(++generation, actor, cast.Action, actor.Actor.Position, rotation,
                activation.AddSeconds(3.1d), new FanTemplate(102.7f, 2 * MathF.PI / 3));
        if (cast.Action == 10532) Ruin = null;
        if (!KefkaCatalog.ACTIONS.Contains(cast.Action) || cast.Action == 10530) return;
        var targetId = Sanitize(cast.TargetId);
        var target = Actors.GetValueOrDefault(targetId);
        if (cast.Action is 10527 or 10528 && target == null) return;
        GeometryTemplate? shape = cast.Action switch
        {
            10516 or 10521 => new DonutTemplate(10, 40),
            10518 or 10520 => new CircleTemplate(10),
            10524 or 10525 => RayRectangleTemplate.Symmetric(10, 40),
            10536 => RayRectangleTemplate.Symmetric(6, 70),
            10537 or 10538 => new FanTemplate(100, MathF.PI),
            _ => null,
        };
        var mechanic = new KefkaMechanic(++generation, actor, cast.Action, cast.Location,
            rotation, activation, shape, target,
            cast.Action is 10532 or 10535
                ? new(cast.Location, cast.Action == 10532 ? 10 : 15, rotation, activation, cast.Action == 10535)
                : null, targetId);
        Mechanics.Add(mechanic);
        // SimpleAOEGroups sorts its own component list at and beyond 99.
        var group = Group(cast.Action);
        if (shape != null && group is 10516 or 10518 or 10524 or 10537)
        {
            var grouped = Mechanics.Where(m => Group(m.Action) == group).ToList();
            if (grouped.Count >= 99)
            {
                DungeonAoeOrder.Sort(grouped, m => m.Activation);
                Mechanics.RemoveAll(m => Group(m.Action) == group);
                Mechanics.AddRange(grouped);
            }
        }
    }

    internal void Finish(KefkaActor actor, DungeonCastObservation cast)
    {
        if (!Active) return;
        foreach (var group in new uint[] { 10516, 10518, 10524, 10537 })
        {
            var grouped = Mechanics.FindIndex(m => m.Source.Actor.Id == actor.Actor.Id && Group(m.Action) == group);
            if (grouped >= 0) Mechanics.RemoveAt(grouped);
        }
        if (cast.Action is 10527 or 10528)
        {
            var mark = Mechanics.FindIndex(m => m.Action == cast.Action && m.TargetId == Sanitize(cast.TargetId));
            if (mark >= 0) Mechanics.RemoveAt(mark);
        }
        else if (cast.Action is not (10516 or 10521 or 10518 or 10520 or 10524 or 10525 or 10537 or 10538))
        {
            var index = Mechanics.FindIndex(m => m.Source.Actor.Id == actor.Actor.Id && m.Action == cast.Action);
            if (index >= 0) Mechanics.RemoveAt(index);
        }
        // RevoltingRuin is intentionally not cleared by CastFinished.
    }

    internal void Effect(ulong source, uint action)
    {
        if (Active && action == 10531 && Actors.TryGetValue(source, out var actor) && NativeNpc(actor.Actor))
            Ruin = null;
    }

    internal bool Immune(DateTime activation)
        => Slot is >= 0 and < 64 && Enumerable.Range(0, 3).Any(i => immunities[Slot, i] > activation);

    private void Status(KefkaActor actor, ReplayStatus status, bool gained, bool hot)
    {
        if (!Active || hot && actor.Actor.NativeType == 0x202) return;
        var category = status.Id switch
        {
            160 or 1209 or 3054 => 0,
            1722 or 2663 => 1,
            2345 => 2,
            _ => -1,
        };
        var slot = party.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
        if (category >= 0 && slot is >= 0 and < 64)
            immunities[slot, category] = gained ? status.ExpiresAt : default;
    }

    private void ObserveStatuses(KefkaActor actor)
    {
        var next = new ReplayStatus?[60];
        var statuses = actor.Actor.StatusSlots ?? (actor.Actor.Statuses ?? []).Select((id, i) => new ReplayStatus(id,
            actor.Actor.StatusParams?.GetValueOrDefault(id) ?? 0, actor.Actor.StatusSources?.GetValueOrDefault(id) ?? 0,
            actor.Actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = i }).ToArray();
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

    private static uint Group(uint action) => action switch
    {
        10521 => 10516, 10520 => 10518, 10525 => 10524, 10538 => 10537, _ => action,
    };

    private static bool SameCast(DungeonCastObservation? a, DungeonCastObservation? b)
        => a == null ? b == null : b != null && a.Action == b.Action && Sanitize(a.TargetId) == Sanitize(b.TargetId)
            && a.Location == b.Location && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static float Total(DungeonCastObservation cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private DateTime Activation(KefkaActor actor)
        => actor.Cast == null ? now : now.AddSeconds((Total(actor.Cast) + .3f) - actor.Elapsed);
    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;
    private static bool NativeNpc(DungeonActor actor)
        => !actor.Player && !actor.Buddy && actor.NativeType is not (0x104 or 0x202 or 0x203 or 0x209);
    private static bool AlmostEqual(Vector3 a, Vector3 b, float tolerance)
        => MathF.Abs(a.X - b.X) <= tolerance && MathF.Abs(a.Z - b.Z) <= tolerance;
    private static void AdvanceClock(KefkaActor actor, DateTime time)
    {
        if (actor.Cast != null && time > actor.ClockAt)
            actor.Elapsed = MathF.Min(actor.Elapsed + (float)(time - actor.ClockAt).TotalSeconds, Total(actor.Cast));
        actor.ClockAt = time;
    }

    internal void End()
    {
        Active = Loaded = false;
        Completed = true;
        Mechanics.Clear();
        Ruin = null;
        Array.Clear(immunities);
    }

    internal void Clear()
    {
        End();
        Actors.Clear();
        Primary = null;
        Completed = false;
        party = [];
    }
}
