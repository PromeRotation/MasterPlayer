using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class HalicarnassusEncounter
{
    internal Dictionary<ulong, HalicarnassusActor> Actors { get; } = [];
    internal List<HalicarnassusAoe> Ordinary { get; } = [];
    internal List<HalicarnassusAoe> Waltz { get; } = [];
    internal List<HalicarnassusAoe> Game { get; } = [];
    internal HalicarnassusActor? Primary { get; private set; }
    internal bool Loaded => Primary != null;
    internal bool Active { get; private set; }
    internal ulong PlayerId { get; private set; }
    internal int Slot => party.FirstOrDefault(p => p.EntityId == PlayerId && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal HalicarnassusActor? SourcePlayer => Actors.GetValueOrDefault(party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0);
    private ReplayPartyMember[] party = [];
    private readonly List<HalicarnassusActor> pending = [];
    private readonly HashSet<HalicarnassusActor> retired = [];
    private DateTime now;
    private long generation;
    private bool wipeInProgress;

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
            old.Cast = null;
            old.Destroyed = true;
            Actors.Remove(old.Actor.Id);
        }
        foreach (var value in values)
        {
            if (!Actors.TryGetValue(value.Id, out var actor))
            {
                actor = new(value);
                Actors.Add(value.Id, actor);
                if (value.Oid == HalicarnassusCatalog.BOSS)
                {
                    if (Primary is { Destroyed: true } old && old.Actor.Id == value.Id
                        && old.Actor.Oid == value.Oid)
                        Primary = actor;
                    else if (Primary == null && CanLoad(actor))
                        Primary = actor;
                    else
                        pending.Add(actor);
                }
            }
            actor.Actor = value;
            var nextCast = actor.NativeNpc && value.Cast?.IsSpell != false ? value.Cast : null;
            if (SameCast(actor.Cast, nextCast)) continue;
            if (actor.Cast is { } previous) Finish(actor, previous);
            actor.Cast = nextCast;
            if (nextCast == null) continue;
            actor.Elapsed = Math.Clamp((float)(time - nextCast.StartedAt).TotalSeconds, 0, Total(nextCast));
            actor.Clock = time;
            if (Active) Start(actor);
        }
        party = roster.ToArray();
        if (update) Update(time);
    }

    internal void Update(DateTime time)
    {
        now = time;
        foreach (var actor in Actors.Values) Advance(actor, time);
        if (SourcePlayer == null) return;
        // Manager checks pending before retiring loaded modules. Multiple event
        // scans must not advance this sequence before the next framework update.
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
            // Activate one component at a time, replaying actors in world order.
            foreach (var action in HalicarnassusCatalog.ACTIONS)
                foreach (var actor in Actors.Values)
                    if (actor.Cast?.Action == action) Start(actor);
        }
        if (!Active && Vector3.DistanceSquared(SourcePlayer.Actor.Position, Primary.Actor.Position) > 500 * 500)
        {
            if (!Primary.Destroyed) pending.Add(Primary);
            Primary = null;
            return;
        }
        if (Primary.Destroyed || Active && !wipeInProgress && (Primary.Actor.Dead || Primary.Actor.Hp == 0)) End();
    }

    private bool CanLoad(HalicarnassusActor actor)
        => !actor.Destroyed && !retired.Contains(actor)
            && Actors.GetValueOrDefault(party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0) is { } local
            && Vector3.DistanceSquared(local.Actor.Position, actor.Actor.Position) <= 500 * 500;

    internal void Start(HalicarnassusActor actor)
    {
        if (!Active || !actor.NativeNpc || actor.Cast is not { } cast) return;
        if (HalicarnassusCatalog.Shape(cast.Action) is { } shape)
            Ordinary.Add(new(++generation, actor.Actor.Id, cast.Action, shape, cast.Location,
                cast.Rotation ?? 0, now.AddSeconds(Total(cast) + .3f - actor.Elapsed)));
        else if (cast.Action is 9329 or 9325)
            for (var z = -15; z <= 15; z += 10)
                for (var x = -15; x <= 15; x += 10)
                    if (cast.Action == 9325 || x != z)
                        (cast.Action == 9325 ? Game : Waltz).Add(new(++generation, actor.Actor.Id,
                            cast.Action, new RectangleTemplate(10, 10), new(x, 0, z), 0, default));
    }

    internal void Finish(HalicarnassusActor actor, DungeonCastObservation cast)
    {
        if (!Active || HalicarnassusCatalog.Shape(cast.Action) == null) return;
        var index = Ordinary.FindIndex(a => a.Action == cast.Action && a.Source == actor.Actor.Id);
        if (index >= 0) Ordinary.RemoveAt(index);
    }

    internal void Effect(ulong source, uint action)
    {
        if (!Active || !Actors.TryGetValue(source, out var actor) || !actor.NativeNpc) return;
        if (action == 9329) Waltz.Clear();
        if (action == 9325) Game.Clear();
    }

    internal IEnumerable<HalicarnassusAoe> Visible()
    {
        if (!Active || !Actors.TryGetValue(PlayerId, out var viewer)) yield break;
        foreach (var action in HalicarnassusCatalog.ACTIONS.Take(6))
            foreach (var aoe in Ordinary.Where(a => a.Action == action)) yield return aoe;
        foreach (var aoe in Waltz) yield return aoe;
        for (var i = 0; i < Math.Min(16, Game.Count); ++i)
        {
            var safe = viewer.Actor.ClassJob switch
            {
                1 or 3 or 19 or 21 or 32 or 37 => i == 6,
                6 or 24 or 28 or 33 or 40 => i == 9,
                2 or 4 or 5 or 7 or 20 or 22 or 23 or 25 or 26 or 27 or 29 or 30 or 31
                    or 34 or 35 or 36 or 38 or 39 or 41 or 42 or 43 => i is 5 or 10,
                _ => false,
            };
            if (!safe) yield return Game[i];
        }
    }

    private static float Total(DungeonCastObservation cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private static bool SameCast(DungeonCastObservation? a, DungeonCastObservation? b)
        => a == null ? b == null : b != null && a.Action == b.Action
            && Sanitize(a.TargetId) == Sanitize(b.TargetId) && a.Location == b.Location
            && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;
    private static bool NewActor(DungeonActor a, DungeonActor b)
        => a.Oid != b.Oid || a.GameObjectId != 0 && b.GameObjectId != 0 && a.GameObjectId != b.GameObjectId
            || a.ObjectIndex != null && b.ObjectIndex != null && a.ObjectIndex != b.ObjectIndex;
    private static void Advance(HalicarnassusActor actor, DateTime time)
    {
        if (actor.Cast is { } cast && time > actor.Clock)
            actor.Elapsed = MathF.Min(actor.Elapsed + (float)(time - actor.Clock).TotalSeconds, Total(cast));
        actor.Clock = time;
    }
    internal void End()
    {
        if (Primary != null) retired.Add(Primary);
        Primary = null;
        Active = false;
        Ordinary.Clear(); Waltz.Clear(); Game.Clear();
    }
    internal void Director(uint command)
    {
        if (command == 0x40000005)
        {
            wipeInProgress = true;
            End();
        }
        else if (command == 0x40000011)
            wipeInProgress = false;
    }
    internal void Clear()
    {
        End();
        Actors.Clear(); pending.Clear(); retired.Clear();
        party = [];
        wipeInProgress = false;
    }
}
