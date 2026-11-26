using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class WickedThunderEncounter
{
    internal Dictionary<ulong, WickedThunderActor> Actors { get; } = [];
    internal List<WickedThunderAoe> Ordinary { get; } = [];
    internal List<WickedThunderAoe> Cannons { get; } = [];
    internal List<WickedThunderAoe> Sides { get; } = [];
    internal List<WickedThunderAoe> Hunts { get; } = [];
    internal List<WickedThunderAoe> Baits { get; } = [];
    internal List<WickedThunderAoe> Stacks { get; } = [];
    internal WickedThunderAoe? Stampede { get; private set; }
    internal WickedThunderAoe? Hyper { get; private set; }
    internal WickedThunderActor? Primary { get; private set; }
    internal IGeometryShape Arena { get; private set; } = WickedThunderCatalog.ARENA;
    internal Vector3 Center { get; private set; } = WickedThunderCatalog.CENTER;
    internal bool Loaded { get; private set; }
    internal bool Active { get; private set; }
    internal bool Completed { get; private set; }
    internal ulong PlayerId { get; private set; }
    internal string? InputDiagnostic { get; private set; }
    private ReplayPartyMember[] party = [];
    private float[] cannonDelays = [];
    private int hyperCasts;
    private int boltCasts;
    private int soulCasts;
    private long generation;
    private DateTime now;

    internal int Slot => party.FirstOrDefault(p => p.EntityId == PlayerId && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal WickedThunderActor[] Party => party.Where(p => p.Slot is >= 0 and < 64)
        .Select(p => Actors.GetValueOrDefault(p.EntityId)).OfType<WickedThunderActor>().ToArray();

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
                if (Primary is { Destroyed: true } previous && !Completed
                    && previous.Actor.Id == value.Id && previous.Actor.Oid == value.Oid)
                    Primary = actor;
                else if (value.Oid == WickedThunderCatalog.BOSS && (Primary == null || Completed))
                {
                    ResetMechanics();
                    Primary = actor;
                    Loaded = Active = Completed = false;
                }
            }
            actor.Actor = value;
            var nextCast = NativeNpc(value) && value.Cast?.IsSpell != false ? value.Cast : null;
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
        if (!Loaded && !Completed && Primary is { Destroyed: false } primary)
        {
            if (primary.Actor.Dead) Completed = true;
            else if (Actors.GetValueOrDefault(party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0) is { } local
                && Vector3.DistanceSquared(local.Actor.Position, primary.Actor.Position) <= 500 * 500)
                Loaded = true;
        }
        if (update) Update(time);
    }

    internal void Update(DateTime time)
    {
        now = time;
        foreach (var actor in Actors.Values) Advance(actor, time);
        if (!Loaded || Completed || Primary == null) return;
        if (!Active && Primary.Actor.Targetable && Primary.Actor.InCombat)
        {
            Active = true;
            // Each actor's current cast precedes its real status slots during
            // component activation; hot status changes exclude pets separately.
            foreach (var actor in Actors.Values)
            {
                if (actor.Cast != null && NativeNpc(actor.Actor)) Start(actor);
                foreach (var status in actor.Statuses.OfType<ReplayStatus>()) Gain(status);
            }
        }
        Baits.RemoveAll(b => b.Source.Destroyed || b.Source.Actor.Dead || b.Target?.Actor.Dead == true);
        if (Active && (Primary.Destroyed || Primary.Actor.Dead || Primary.Actor.Hp == 0)) End();
    }

    private void Start(WickedThunderActor actor)
    {
        var cast = actor.Cast!;
        // Every spell start resets delays, including unrelated helper casts.
        cannonDelays = cast.Action switch
        {
            37549 or 37552 => [9.5f, 8.3f, 7.7f],
            39759 or 39765 => [12, 10.9f, 10.2f, 9.5f],
            39766 or 39767 => [14.5f, 13.4f, 12.8f, 12.2f, 11.6f],
            _ => [],
        };
        var hit = Activation(actor);
        var rotation = cast.Rotation ?? 0;
        var targetId = Sanitize(cast.TargetId);
        var target = Actors.GetValueOrDefault(targetId);
        WickedThunderAoe Make(GeometryTemplate? shape, DateTime activation)
            => new(++generation, actor, cast.Action, shape, cast.Location, rotation, activation, target, targetId);
        if (cast.Action is >= 37564 and <= 37567)
            Sides.Add(Make(new FanTemplate(60, MathF.PI), hit));
        else if (cast.Action == 37561)
            Sides.Add(Make(RayRectangleTemplate.Symmetric(16, 40), hit));
        else if (cast.Action == 37557)
            Hunts.Add(Make(new CircleTemplate(6), Activation(actor, 6.3d)) with { Rotation = 0, Risky = false });
        else if (cast.Action is 37553 or 37554 && Center.X is 85 or 115)
            Hyper = Make(RayRectangleTemplate.Symmetric(20, 40), Activation(actor, .6d)) with
            { Origin = new(Center.X, 0, 80), Rotation = 0, Risky = false };
        else if (cast.Action == 37576 && target != null)
            Baits.Add(Make(RayRectangleTemplate.Symmetric(5, 60), hit));
        else if (cast.Action is 37560 or 37574 or 37575 || cast.Action == 37573 && target != null)
        {
            GeometryTemplate? shape = cast.Action switch
            {
                37560 => RayRectangleTemplate.Symmetric(5, 40),
                37574 => new CircleTemplate(5),
                _ => null,
            };
            Ordinary.Add(Make(shape, hit));
        }
    }

    internal void Finish(WickedThunderActor actor, DungeonCastObservation cast)
    {
        if (!Active) return;
        // Each SimpleAOEs component consumes only its watched action.
        if (cast.Action is 37560 or 37574 or 37575)
        {
            var index = Ordinary.FindIndex(m => m.Action == cast.Action
                && ReferenceEquals(m.Source, actor));
            if (index >= 0) Ordinary.RemoveAt(index);
        }
        if (cast.Action == 37573)
        {
            var index = Ordinary.FindIndex(m => m.Action == 37573 && m.TargetId == Sanitize(cast.TargetId));
            if (index >= 0) Ordinary.RemoveAt(index);
        }
        if (cast.Action is >= 37564 and <= 37567 or 39429 or 39439 or 37561 && Sides.Count > 0)
            Sides.RemoveAt(0);
        // Baits, telegraphs, cannons and Hyper have independent effect endings.
    }

    internal void Icon(ulong source, uint icon)
    {
        if (!Active || !Actors.TryGetValue(source, out var actor) || icon is not (316 or 161)) return;
        Stacks.Add(new(++generation, actor, icon == 316 ? 37571u : 37569u,
            new CircleTemplate(icon == 316 ? 5 : 6), default, 0, now.AddSeconds(icon == 316 ? 5d : 5.4d),
            actor, source));
    }

    internal void Timeline(ulong source, uint timeline)
    {
        if (!Active || !Actors.TryGetValue(source, out var actor) || actor.Actor.Oid != 0x4264
            || timeline is not (0x11D8 or 0x11D6)) return;
        Sides.Add(new(++generation, actor, 0, new FanTemplate(60, MathF.PI), actor.Actor.Position,
            actor.Actor.Rotation + (timeline == 0x11D8 ? MathF.PI / 2 : -MathF.PI / 2), now.AddSeconds(8d)));
    }

    internal void Map(byte index, uint state)
    {
        if (!Active || index != 0) return;
        if (state is 0x00020001 or 0x00200010)
        {
            Center = new(state == 0x00020001 ? 85 : 115, 0, 100);
            Arena = new RectangleGeometry(Center, 0, 10, 40);
            Stampede = null;
        }
        else if (state is 0x00400004 or 0x00800004)
        {
            Center = WickedThunderCatalog.CENTER;
            Arena = WickedThunderCatalog.ARENA;
        }
    }

    internal void Effect(ulong source, uint action, ulong targetId)
    {
        if (!Active || !Actors.TryGetValue(source, out var actor) || !NativeNpc(actor.Actor)) return;
        if (action is 37547 or 37548)
            Stampede = new(++generation, actor, action, RayRectangleTemplate.Symmetric(30, 40),
                DungeonEncounter.QuantizedDungeonPosition(new(action == 37547 ? 95 : 105, 0, 80)),
                actor.Actor.Rotation, now.AddSeconds(9.4d));
        if (action is 20032 or 37550 or 37551 or 39852 or 39870 && Cannons.Count > 0)
            Cannons.RemoveAt(0);
        if (action == 37558 && Hunts.Count > 0) Hunts.RemoveAt(0);
        if (action == 37555 && ++hyperCasts == 10) { Hyper = null; hyperCasts = 0; }
        if (action == 37576)
        {
            var index = Baits.FindIndex(b => b.Source.Actor.Id == source);
            if (index >= 0) Baits.RemoveAt(index);
        }
        if (action is not (37571 or 37569)) return;
        var stack = Stacks.Where(s => s.Action == action).ToArray();
        targetId = Sanitize(targetId);
        if (action == 37571 && stack.Length == 1 && stack[0].TargetId != targetId)
        {
            if (Actors.TryGetValue(targetId, out var target))
            {
                var index = Stacks.IndexOf(stack[0]);
                Stacks[index] = stack[0] with { Target = target, TargetId = targetId };
            }
            else InputDiagnostic = "多段分摊的新主目标缺少Actor，保留原目标和判定钟";
        }
        var count = action == 37571 ? ++boltCasts : ++soulCasts;
        if (count != (action == 37571 ? 5 : 1)) return;
        var found = Stacks.FindIndex(s => s.Action == action && s.TargetId == targetId);
        if (found < 0) found = Stacks.FindIndex(s => s.Action == action);
        if (found < 0) return;
        Stacks.RemoveAt(found);
        if (action == 37571) boltCasts = 0;
        else soulCasts = 0;
    }

    private void ObserveStatuses(WickedThunderActor actor)
    {
        var next = new ReplayStatus?[60];
        var slots = actor.Actor.StatusSlots ?? (actor.Actor.Statuses ?? []).Select((id, i) => new ReplayStatus(id,
            actor.Actor.StatusParams?.GetValueOrDefault(id) ?? 0, actor.Actor.StatusSources?.GetValueOrDefault(id) ?? 0,
            actor.Actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = i }).ToArray();
        for (var i = 0; i < slots.Count; ++i)
        {
            var slot = slots[i].SlotIndex ?? i;
            if (slot is >= 0 and < 60 && slots[i].Id != 0) next[slot] = slots[i] with { SourceId = Sanitize(slots[i].SourceId) };
        }
        for (var i = 0; i < 60; ++i)
        {
            var old = actor.Statuses[i];
            var value = next[i];
            actor.Statuses[i] = value;
            if (value == null || old != null && old.Id == value.Id && old.Param == value.Param
                && old.SourceId == value.SourceId && (value.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1d) continue;
            if (Active && actor.Actor.NativeType != 0x202) Gain(value);
        }
    }

    private void Gain(ReplayStatus status)
    {
        if (status.Id != 2970 || cannonDelays.Length <= Cannons.Count || Primary == null) return;
        var rotation = status.Param switch
        {
            0x2D3 => MathF.PI,
            // Upstream AnglesCardinals[1] deliberately retains this offset.
            0x2D4 => -.003f * (MathF.PI / 180f),
            _ => 0,
        };
        Cannons.Add(new(++generation, Primary, 20032, RayRectangleTemplate.Symmetric(10, 40),
            Primary.Actor.Position, rotation,
            now.AddSeconds(cannonDelays[Cannons.Count]), Risky: false));
    }

    internal DateTime Activation(WickedThunderActor actor, double extraDelay = 0)
        => actor.Cast == null ? now : now.AddSeconds(Total(actor.Cast) + .3f - actor.Elapsed + extraDelay);
    private static float Total(DungeonCastObservation cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private static void Advance(WickedThunderActor actor, DateTime time)
    {
        if (actor.Cast != null && time > actor.Clock)
            actor.Elapsed = MathF.Min(actor.Elapsed + (float)(time - actor.Clock).TotalSeconds, Total(actor.Cast));
        actor.Clock = time;
    }
    private static ulong Sanitize(ulong id) => id == 0xE0000000 ? 0 : id;
    private static bool NativeNpc(DungeonActor actor)
        => !actor.Player && !actor.Buddy && actor.NativeType is not (0x104 or 0x202 or 0x203 or 0x209);
    private static bool NewActor(DungeonActor old, DungeonActor next)
        => old.Oid != next.Oid || old.GameObjectId != 0 && next.GameObjectId != 0 && old.GameObjectId != next.GameObjectId
            || old.ObjectIndex != null && next.ObjectIndex != null && old.ObjectIndex != next.ObjectIndex;
    private static bool SameCast(DungeonCastObservation? a, DungeonCastObservation? b)
        => a == null ? b == null : b != null && a.Action == b.Action && Sanitize(a.TargetId) == Sanitize(b.TargetId)
            && a.Location == b.Location && a.Rotation == b.Rotation && Total(a) == Total(b);
    private void ResetMechanics()
    {
        Ordinary.Clear(); Cannons.Clear(); Sides.Clear(); Hunts.Clear(); Baits.Clear(); Stacks.Clear();
        Stampede = Hyper = null;
        cannonDelays = [];
        hyperCasts = boltCasts = soulCasts = 0;
        Center = WickedThunderCatalog.CENTER;
        Arena = WickedThunderCatalog.ARENA;
        InputDiagnostic = null;
    }
    internal void End()
    {
        ResetMechanics();
        Active = Loaded = false;
        Completed = true;
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
