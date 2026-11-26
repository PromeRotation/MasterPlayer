using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class AlteRoiteEncounter
{
    internal Dictionary<ulong, AlteRoiteActor> Actors { get; } = [];
    internal List<AlteRoiteMechanic> Ordinary { get; } = [];
    internal List<AlteRoiteMechanic> Knockbacks { get; } = [];
    internal Dictionary<ulong, AlteRoiteOrb> Orbs { get; } = [];
    internal List<AlteRoiteMarker> Stacks { get; } = [];
    internal List<AlteRoiteMarker> Spreads { get; } = [];
    internal AlteRoiteActor? Primary { get; private set; }
    internal bool Loaded => Primary != null;
    internal bool Active { get; private set; }
    internal ulong PlayerId { get; private set; }
    internal int Slot => party.FirstOrDefault(p => p.EntityId == PlayerId && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal AlteRoiteActor? SourcePlayer => Actors.GetValueOrDefault(party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0);
    internal IEnumerable<AlteRoiteActor> Party => party.Where(p => p.Slot is >= 0 and < 64)
        .Select(p => Actors.GetValueOrDefault(p.EntityId)).OfType<AlteRoiteActor>();
    internal ulong IceMask { get; private set; }
    internal int StackCounter { get; private set; }
    private ReplayPartyMember[] party = [];
    private readonly List<AlteRoiteActor> pending = [];
    private readonly HashSet<AlteRoiteActor> retired = [];
    private readonly DateTime[,] immunities = new DateTime[64, 3];
    private DateTime now;
    private long generation;
    private bool wipeInProgress;
    private float clampLength;
    private DateTime firstRing;
    private DateTime breath;
    private DateTime downburst;
    private List<AlteRoiteActor>? cachedOrbs;
    private List<AlteRoiteActor>? cachedTornadoes;

    internal void Synchronize(IEnumerable<DungeonActor> input, IReadOnlyList<ReplayPartyMember> roster,
        ulong viewer, DateTime time, bool enabled, bool update)
    {
        if (!enabled) { Clear(); return; }
        now = time;
        PlayerId = viewer;
        var values = input.ToArray();
        var present = values.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values) Advance(old, time);
        foreach (var old in Actors.Values.Where(a => !present.TryGetValue(a.Actor.Id, out var next)
            || NewActor(a.Actor, next)).ToArray())
        {
            if (old.Cast is { } cast) Finish(old, cast);
            foreach (var status in old.Statuses.OfType<ReplayStatus>()) Status(old, status, false, true);
            if (Active && old.NativeNpc && old.Actor.Oid == AlteRoiteCatalog.ORB) Orbs.Remove(old.Actor.Id);
            old.Cast = null;
            Array.Clear(old.Statuses);
            old.Destroyed = true;
            cachedOrbs?.Remove(old);
            cachedTornadoes?.Remove(old);
            Actors.Remove(old.Actor.Id);
        }
        foreach (var value in values)
        {
            if (!Actors.TryGetValue(value.Id, out var actor))
            {
                actor = new(value);
                Actors.Add(value.Id, actor);
                if (value.Oid == AlteRoiteCatalog.ORB) cachedOrbs?.Add(actor);
                if (value.Oid == 0x18D6) cachedTornadoes?.Add(actor);
                if (value.Oid == AlteRoiteCatalog.BOSS)
                {
                    if (Primary is { Destroyed: true } old && old.Actor.Id == value.Id
                        && old.Actor.Oid == value.Oid)
                        Primary = actor;
                    else if (Primary == null && CanLoad(actor))
                        Primary = actor;
                    else
                        pending.Add(actor);
                }
                if (Active && actor.NativeNpc) CreatedOrb(actor);
            }
            actor.Actor = value;
            var nextCast = actor.NativeNpc && value.Cast?.IsSpell != false ? value.Cast : null;
            if (!SameCast(actor.Cast, nextCast))
            {
                if (actor.Cast is { } previous) Finish(actor, previous);
                actor.Cast = nextCast;
                if (nextCast != null)
                {
                    actor.Elapsed = Math.Clamp((float)(time - nextCast.StartedAt).TotalSeconds, 0, Total(nextCast));
                    actor.Clock = time;
                    if (Active) Start(actor);
                }
            }
            ObserveStatuses(actor);
        }
        party = roster.ToArray();
        if (update) Update(time);
    }

    internal void Update(DateTime time)
    {
        now = time;
        foreach (var actor in Actors.Values) Advance(actor, time);
        if (SourcePlayer == null) return;
        for (var i = pending.Count - 1; i >= 0; --i)
        {
            var actor = pending[i];
            if (actor.Destroyed || actor.Actor.Dead || retired.Contains(actor))
                pending.RemoveAt(i);
            else if (Primary == null && CanLoad(actor))
            {
                Primary = actor;
                pending.RemoveAt(i);
            }
        }
        if (Primary == null) return;
        if (!wipeInProgress && !Active && Primary.Actor.Targetable && Primary.Actor.InCombat)
        {
            Active = true;
            clampLength = 9 + Primary.Actor.HitboxRadius;
            // Activate in source order. Each component receives Created then Cast
            // and the sixty real status slots for each actor, before the next actor.
            for (var component = 0; component < 12; ++component)
                foreach (var actor in Actors.Values)
                {
                    if (actor.NativeNpc)
                    {
                        if (component == 9) CreatedOrb(actor);
                        if (actor.Cast != null) StartComponent(actor, component);
                    }
                    if (component is 1 or 4 or 6 or 7)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                            StatusComponent(actor, status, true, component);
                }
        }
        if (!Active && Vector3.DistanceSquared(SourcePlayer.Actor.Position, Primary.Actor.Position) > 500 * 500)
        {
            if (!Primary.Destroyed) pending.Add(Primary);
            Primary = null;
            return;
        }
        // IconStackSpread.Update permanently removes dead spreads, but retains
        // dead stack markers. A global dead-marker cleanup changes this contract.
        if (Active && !wipeInProgress) Spreads.RemoveAll(s => s.Target.Actor.Dead);
        if (Primary.Destroyed || Active && !wipeInProgress && (Primary.Actor.Dead || Primary.Actor.Hp == 0)) End();
    }

    private bool CanLoad(AlteRoiteActor actor)
        => !actor.Destroyed && !retired.Contains(actor) && SourcePlayer is { } local
            && Vector3.DistanceSquared(local.Actor.Position, actor.Actor.Position) <= 500 * 500;

    internal void Start(AlteRoiteActor actor)
    {
        if (!Active || !actor.NativeNpc || actor.Cast == null) return;
        for (var component = 0; component < 12; ++component) StartComponent(actor, component);
    }

    private void StartComponent(AlteRoiteActor actor, int component)
    {
        var cast = actor.Cast!;
        var action = cast.Action;
        GeometryTemplate? shape = component switch
        {
            5 when action == 9186 => RayRectangleTemplate.Symmetric(10, clampLength),
            10 when action == 9176 => new CircleTemplate(5),
            _ => null,
        };
        if (component == 0 && action == 9180 || component == 2 && action == 9179
            || component == 3 && action == 9175 || shape != null)
            Ordinary.Add(new(++generation, actor, action, cast.Location, cast.Rotation ?? 0, Activation(actor), shape));
        if (component == 4 && action == 9182 || component == 6 && action == 9186 || component == 7 && action == 7896)
            Knockbacks.Add(new(++generation, actor, action, cast.Location, cast.Rotation ?? 0, Activation(actor),
                action == 9186 ? RayRectangleTemplate.Symmetric(10, clampLength) : null));
        if (component != 9) return;
        if (ReferenceEquals(actor, Primary))
        {
            if (action == 9182) breath = now;
            else if (action == 7896) downburst = now;
            if (action is 9182 or 7896)
                foreach (var (id, orb) in Orbs.ToArray())
                    if (orb.Outer && orb.Actual == null) Orbs[id] = orb with { Predicted = PredictOuter(orb.Spawn) };
        }
        else if (actor.Actor.Oid == AlteRoiteCatalog.ORB && action is 9173 or 9188
            && Orbs.TryGetValue(actor.Actor.Id, out var orb))
            Orbs[actor.Actor.Id] = orb with { Actual = Activation(actor) };
    }

    private void CreatedOrb(AlteRoiteActor actor)
    {
        if (actor.Actor.Oid != AlteRoiteCatalog.ORB) return;
        if (Orbs.Count == 0)
        {
            firstRing = now;
            breath = downburst = default;
        }
        if (firstRing == default) firstRing = now;
        var outer = (now - firstRing).TotalSeconds >= 2d;
        Orbs[actor.Actor.Id] = new(++generation, now, outer, outer ? PredictOuter(now) : now.AddSeconds(16.84f));
    }

    private DateTime PredictOuter(DateTime spawn)
    {
        if (downburst != default) return downburst.AddSeconds(12.18f);
        if (breath != default) return breath.AddSeconds(9.08f);
        return spawn.AddSeconds(16.84f);
    }

    internal void Finish(AlteRoiteActor actor, DungeonCastObservation cast)
    {
        if (!Active) return;
        if (cast.Action is 9180 or 9179 or 9175 or 9186 or 9176)
        {
            var index = Ordinary.FindIndex(m => m.Action == cast.Action && (m.Shape == null
                ? ReferenceEquals(m.Source, actor) : m.Source.Actor.Id == actor.Actor.Id));
            if (index >= 0) Ordinary.RemoveAt(index);
        }
        if (cast.Action is 9182 or 9186 or 7896)
        {
            var index = Knockbacks.FindIndex(m => m.Action == cast.Action && m.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Knockbacks.RemoveAt(index);
        }
        if (actor.Actor.Oid == AlteRoiteCatalog.ORB && cast.Action is 9173 or 9188)
            Orbs.Remove(actor.Actor.Id);
    }

    internal void Icon(ulong source, uint icon)
    {
        if (!Active || icon is not (62 or 108) || !Actors.TryGetValue(source, out var actor)) return;
        (icon == 62 ? Stacks : Spreads).Add(new(++generation, actor, now.AddSeconds(5d)));
    }

    internal void Effect(ulong source, uint action, ulong target)
    {
        if (!Active || !Actors.TryGetValue(source, out var actor) || !actor.NativeNpc) return;
        if (action == 9185)
        {
            if (++StackCounter != 1 || Stacks.Count == 0) return;
            var index = Stacks.FindIndex(m => m.Target.Actor.Id == target);
            Stacks.RemoveAt(index >= 0 ? index : 0);
            StackCounter = 0;
        }
        else if (action == 9177 && Spreads.Count > 0)
        {
            var index = Spreads.FindIndex(m => m.Target.Actor.Id == target);
            Spreads.RemoveAt(index >= 0 ? index : 0);
        }
    }

    internal void Director(uint command)
    {
        if (command == 0x40000005) { wipeInProgress = true; End(); }
        else if (command == 0x40000011) wipeInProgress = false;
    }

    internal void End()
    {
        if (Primary != null) retired.Add(Primary);
        Primary = null;
        Active = false;
        Ordinary.Clear(); Knockbacks.Clear(); Orbs.Clear(); Stacks.Clear(); Spreads.Clear();
        IceMask = 0; StackCounter = 0;
        Array.Clear(immunities);
        firstRing = breath = downburst = default;
        cachedOrbs = cachedTornadoes = null;
    }

    internal void Clear()
    {
        End();
        Actors.Clear(); pending.Clear(); retired.Clear(); party = [];
        wipeInProgress = false;
    }

    private static float Total(DungeonCastObservation cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    internal DateTime Activation(AlteRoiteActor actor)
        => actor.Cast == null ? default : now.AddSeconds(Total(actor.Cast) + .3f - actor.Elapsed);
    private static bool SameCast(DungeonCastObservation? a, DungeonCastObservation? b)
        => a == null ? b == null : b != null && a.Action == b.Action
            && Sanitize(a.TargetId) == Sanitize(b.TargetId) && a.Location == b.Location
            && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;
    private static bool NewActor(DungeonActor a, DungeonActor b)
        => a.Oid != b.Oid || a.GameObjectId != 0 && b.GameObjectId != 0 && a.GameObjectId != b.GameObjectId
            || a.ObjectIndex != null && b.ObjectIndex != null && a.ObjectIndex != b.ObjectIndex;
    private static void Advance(AlteRoiteActor actor, DateTime time)
    {
        if (actor.Cast is { } cast && time > actor.Clock)
            actor.Elapsed = MathF.Min(actor.Elapsed + (float)(time - actor.Clock).TotalSeconds, Total(cast));
        actor.Clock = time;
    }
}
