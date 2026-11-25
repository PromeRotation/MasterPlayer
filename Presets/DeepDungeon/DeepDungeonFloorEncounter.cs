using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.DeepDungeon;

// AutoClear's combat lists and AIHintsBuilder's ordinary casts have independent
// lifetimes. In particular, a non-null replacement expires only the latter.
internal abstract class DeepDungeonFloorEncounter
{
    private readonly Dictionary<ulong, PalaceTrashCaster> actors = [];
    private readonly Dictionary<PalaceTrashCaster, PalaceCast> ordinary = [];
    private readonly Dictionary<PalaceTrashCaster, PalaceTrashGaze> genericGazes = [];
    private readonly HashSet<PalaceTrashCaster> clearedPending = [];
    private readonly HashSet<ulong> retiredIds = [];
    protected readonly List<PalaceTrashGaze> gazes = [];
    protected readonly List<(PalaceTrashCaster Source, float Inner, float Outer)> donuts = [];
    protected readonly HashSet<PalaceTrashCaster> interrupts = [];
    protected readonly HashSet<PalaceTrashCaster> stuns = [];
    protected readonly HashSet<PalaceTrashCaster> hintDisabled = [];
    private static readonly LocalPolygonTemplate eye = DungeonEncounter.CreateQueenEyeSymbol();
    private static readonly LocalPolygonTemplate viewArc = DungeonEncounter.CreateQueenGazeArc();
    private bool initialized;

    internal bool ZoneEnabled { get; set; } = true;
    internal bool BossActive { get; set; }
    internal IEnumerable<ReplayActorState> Actors => actors.Values.Select(s => s.Actor);
    protected PalaceTrashCaster? Source(ulong id) => actors.GetValueOrDefault(id);
    internal bool RequiresEffectIdentity(ulong id) => retiredIds.Contains(id);
    internal virtual bool PreservesGenericDodge(uint action) => false;
    internal virtual void Effect(ulong actorId, uint action) { }
    internal virtual ulong[] SpikedTargets(DateTime now) => [];
    internal virtual void SynchronizeFloor(IEnumerable<ReplayActorState> observations, DateTime now, bool observe,
        Func<ReplayActorState, PalaceCast?> resolve, Action<ReplayActorState>? created,
        Func<ReplayActorState, uint, bool>? ownsCast, ReplayPartyMember[] nextParty)
        => Synchronize(observations, now, observe, resolve, created, ownsCast);
    private static ulong Target(ulong id) => id == 0xE0000000 ? 0 : id;
    private static float Total(ReplayCast cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private static bool Same(ReplayCast a, ReplayCast b)
        => a.ActionId == b.ActionId && a.IsSpell == b.IsSpell && Target(a.TargetId) == Target(b.TargetId)
            && a.Location == b.Location && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static bool NewActor(ReplayActorState a, ReplayActorState b)
        => a.Oid != b.Oid || a.GameObjectId != 0 && b.GameObjectId != 0 && a.GameObjectId != b.GameObjectId
            || a.ObjectIndex != null && b.ObjectIndex != null && a.ObjectIndex != b.ObjectIndex;

    internal void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now, bool observeCasts,
        Func<ReplayActorState, PalaceCast?> resolve, Action<ReplayActorState>? created = null,
        Func<ReplayActorState, uint, bool>? ownsCast = null,
        Action<PalaceTrashCaster, bool>? observeStatuses = null)
    {
        var incoming = observations.Where(a => a.Id != 0).ToDictionary(a => a.Id);
        foreach (var id in actors.Keys.Except(incoming.Keys).ToArray()) Remove(id, now);
        foreach (var (id, actor) in incoming)
        {
            if (actors.TryGetValue(id, out var previous) && NewActor(previous.Actor, actor)) Remove(id, now);
            if (!actors.TryGetValue(id, out var source))
            {
                actors[id] = source = new(actor);
                if (initialized) created?.Invoke(actor);
            }
            source.Actor = actor;
            Advance(source, now);
            if (!observeCasts) continue;
            if (actor.Cast is not { } next)
            {
                Finish(source, now);
                observeStatuses?.Invoke(source, initialized);
                continue;
            }
            if (source.Observed && source.Cast is { } old && Same(old, next))
            {
                source.Observed = true;
                observeStatuses?.Invoke(source, initialized);
                continue;
            }
            var packetConfirmed = source.Action == next.ActionId && source.Responded && !source.Observed
                && Math.Abs((next.StartedAt - source.Start).TotalSeconds) < .25;
            if (!packetConfirmed && source.Cast != null && source.Observed) OnFinished(source, now);
            ordinary.Remove(source);
            genericGazes.Remove(source);
            source.Cast = next;
            source.Action = next.ActionId;
            source.Start = next.StartedAt;
            source.Clock = now;
            source.Observed = true;
            var value = resolve(actor);
            source.Extra = value?.CastExtra ?? 0;
            source.Elapsed = Math.Clamp((float)(now - next.StartedAt).TotalSeconds, 0, Total(next));
            // Zone constructors subscribe to an already filled world; they do
            // not replay current CastInfo or ActorAdded. Keep a receipt so a
            // delayed packet cannot manufacture these historical callbacks.
            var replayStart = clearedPending.Remove(source);
            if (initialized && (!packetConfirmed || replayStart) && (ownsCast?.Invoke(actor, next.ActionId) ?? true))
                AddZone(source, now);
            source.Responded = true;
            if (!BossActive && value is { Shape: not null })
            {
                if (value.GenericGaze)
                    genericGazes[source] = new(source, value.Action, value.Shape,
                        actors.GetValueOrDefault(Target(next.TargetId)), Generic: true);
                else ordinary[source] = value;
            }
            observeStatuses?.Invoke(source, initialized);
        }
        initialized = true;
    }

    internal void StartPacket(PalaceCast cast)
    {
        if (!initialized || !actors.TryGetValue(cast.Source, out var source)) return;
        // CastStart packets have no actor generation. Once an ID was reused,
        // only authoritative raw CastInfo can associate a new start with it.
        // A scan timestamp is not proof of when the replacement was born.
        if (retiredIds.Contains(cast.Source)) return;
        if (source.Start > cast.Start.AddSeconds(.25)
            || source.FinishedAction == cast.Action && cast.Start <= source.FinishedStart.AddSeconds(.25)) return;
        if (source.Responded && source.Action == cast.Action && Math.Abs((source.Start - cast.Start).TotalSeconds) < .25) return;
        // Packets stage Zone callbacks only; ordinary geometry comes from the
        // authoritative observed CastInfo and captures its target reference.
        if (source.Cast != null) return;
        source.Cast = new(cast.Action, Target(cast.Target), cast.Position, cast.Rotation,
            cast.Start, cast.Hit, false, false)
        {
            TotalTime = (float)(cast.Hit - cast.Start).TotalSeconds - .3f,
        };
        source.Action = cast.Action;
        source.Start = cast.Start;
        source.Clock = cast.Start;
        source.Extra = cast.CastExtra;
        source.Elapsed = 0;
        source.Observed = false;
        source.Responded = true;
        clearedPending.Remove(source);
        AddZone(source, cast.Start);
    }

    protected abstract void AddZone(PalaceTrashCaster source, DateTime now);
    protected virtual void OnFinished(PalaceTrashCaster source, DateTime now) { }

    private static void Advance(PalaceTrashCaster source, DateTime now)
    {
        if (source.Cast is { } cast && now > source.Clock)
            source.Elapsed = MathF.Min(source.Elapsed + (float)(now - source.Clock).TotalSeconds, Total(cast) + source.Extra);
        source.Clock = now;
    }

    protected static DateTime Activation(PalaceTrashCaster source, DateTime now)
    {
        Advance(source, now);
        return source.Cast is { } cast ? now.AddSeconds(Total(cast) + .3f - source.Elapsed) : now;
    }

    private void Finish(PalaceTrashCaster source, DateTime now)
    {
        // Packet-only staging is not proof that a real cast ever existed.
        // Raw-null can reject it, but cannot manufacture an OnCastFinished.
        if (source.Cast != null && source.Observed) OnFinished(source, now);
        if (source.Action != 0)
        {
            source.FinishedAction = source.Action;
            source.FinishedStart = source.Start;
        }
        ordinary.Remove(source);
        genericGazes.Remove(source);
        clearedPending.Remove(source);
        source.Cast = null;
        source.Action = 0;
        source.Observed = false;
        source.Responded = false;
    }

    private void Remove(ulong id, DateTime now)
    {
        if (actors.Remove(id, out var source))
        {
            retiredIds.Add(id);
            Finish(source, now);
            OnRemoved(source);
        }
        // Old target shells deliberately freeze; a same-ID birth cannot rebind.
    }
    protected virtual void OnRemoved(PalaceTrashCaster source) { }

    private void Prune()
    {
        if (!ZoneEnabled || BossActive) return;
        gazes.RemoveAll(g => g.Source.Cast == null);
        donuts.RemoveAll(g => g.Source.Cast == null);
        interrupts.RemoveWhere(g => g.Cast == null);
        stuns.RemoveWhere(g => g.Cast == null);
        hintDisabled.RemoveWhere(g => g.Cast == null);
    }

    internal IEnumerable<(PalaceTrashGaze Gaze, Vector3 Origin, DateTime Hit)> Eyes(DateTime now, ReplayActorState player)
    {
        Prune();
        if (BossActive) yield break;
        for (var i = ZoneEnabled ? gazes.Count - 1 : -1; i >= 0; --i)
        {
            var gaze = gazes[i];
            var actor = gaze.Source.Actor;
            if (DungeonSourceGeometry.Contains(gaze.Shape, actor.Position, actor.Rotation, player.Position))
                yield return (gaze, actor.Position, Activation(gaze.Source, now));
        }
        foreach (var gaze in genericGazes.Values)
        {
            if (gaze.Source.Cast is not { } cast) continue;
            var origin = gaze.Target?.Actor.Position ?? cast.Location;
            if (DungeonSourceGeometry.Contains(gaze.Shape, origin, cast.Rotation ?? gaze.Source.Actor.Rotation, player.Position))
                yield return (gaze, origin, Activation(gaze.Source, now));
        }
    }

    internal float? Facing(DateTime now, ReplayActorState player)
        => DungeonFacingDirections.Solve(Eyes(now, player).Where(e => e.Hit <= now.AddSeconds(.75))
            .Select(e => (MathF.Atan2(e.Origin.X - player.Position.X, e.Origin.Z - player.Position.Z), MathF.PI / 4)).ToArray(),
            player.Rotation);

    internal IEnumerable<string> Hints(DateTime now, ReplayActorState player)
    {
        Prune();
        if (BossActive) yield break;
        if (Eyes(now, player).Any(e => MathF.Abs(MathF.Atan2(
            MathF.Sin(player.Rotation - MathF.Atan2(e.Origin.X - player.Position.X, e.Origin.Z - player.Position.Z)),
            MathF.Cos(player.Rotation - MathF.Atan2(e.Origin.X - player.Position.X, e.Origin.Z - player.Position.Z)))) <= MathF.PI / 4))
            yield return "背对视线";
        if (!ZoneEnabled) yield break;
        foreach (var source in interrupts) yield return $"打断 {source.Actor.Name}（{source.Action}）";
        foreach (var source in stuns) yield return $"眩晕 {source.Actor.Name}（{source.Action}）";
    }

    internal virtual IEnumerable<PalaceArea> Collect(DateTime now, ReplayActorState player)
    {
        Prune();
        if (BossActive) yield break;
        foreach (var source in actors.Values)
        {
            var actor = source.Actor;
            if (ZoneEnabled && !actor.OpenedTreasure
                && actor.Oid is >= 0x1EA08E and <= 0x1EA092 or 0x1EA9A0 or 0x1EB864 or 0x1EBEDB)
                yield return new($"trap/{actor.Id}", "已揭示陷阱", new CircleTemplate(2), actor.Position, 0,
                    now, DateTime.MaxValue, actor.Id);
        }
        foreach (var (source, value) in ordinary)
        {
            if (source.Cast is not { } cast || value.Shape is not { } shape) continue;
            // HintDisabled removes a zone by Source ID. Ordinary non-charge
            // hints use Source=0 and therefore survive (notably 12415's 3..41).
            if (ZoneEnabled && value.Charge && hintDisabled.Contains(source)) continue;
            var position = cast.Location;
            var rotation = cast.Rotation ?? source.Actor.Rotation;
            if (value.Charge && shape is RayRectangleTemplate rect)
            {
                position = Quantized(source.Actor.Position);
                var delta = cast.Location - position;
                delta.Y = 0;
                if (delta.LengthSquared() == 0) continue;
                shape = RayRectangleTemplate.Symmetric(rect.MaxLocalX - rect.MinLocalX, delta.Length());
                rotation = MathF.Atan2(delta.X, delta.Z);
            }
            yield return new($"cast/{source.Actor.Id}/{value.Action}", value.Name, shape, position, rotation,
                Activation(source, now), DateTime.MaxValue, source.Actor.Id, value.Action, Telegraph: value.Start);
        }
        for (var i = ZoneEnabled ? donuts.Count - 1 : -1; i >= 0; --i)
        {
            var donut = donuts[i];
            if (donut.Source.Cast is not { } cast) continue;
            yield return new($"zone/donut/{i}", "环形范围", new DonutTemplate(donut.Inner, donut.Outer), cast.Location,
                cast.Rotation ?? 0, Activation(donut.Source, now), DateTime.MaxValue, donut.Source.Actor.Id, donut.Source.Action);
        }
        var index = 0;
        foreach (var (_, origin, hit) in Eyes(now, player))
        {
            var direction = MathF.Atan2(origin.X - player.Position.X, origin.Z - player.Position.Z);
            var danger = MathF.Abs(MathF.Atan2(MathF.Sin(player.Rotation - direction),
                MathF.Cos(player.Rotation - direction))) <= MathF.PI / 4;
            var color = danger ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1);
            yield return new($"gaze/{index}/eye", "背对视线", eye, origin, 0, hit, DateTime.MaxValue,
                PreviewOnly: true, Color: color, Unclipped: true);
            yield return new($"gaze/{index++}/pupil", "视线瞳孔", new CircleTemplate(.28f), origin, 0, hit,
                DateTime.MaxValue, PreviewOnly: true, Color: new Vector4(.04f, .04f, .04f, 1), Unclipped: true);
        }
        if (index > 0)
            yield return new("gaze/view", "视线前方九十度判定弧", viewArc, player.Position, player.Rotation,
                now, DateTime.MaxValue, PreviewOnly: true, Color: new Vector4(1, 0, 0, 1), Unclipped: true);
    }

    protected static Vector3 Quantized(Vector3 p)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(p.X * inverse) - .5f) * grid, p.Y,
            ((int)MathF.Round(p.Z * inverse) - .5f) * grid);
    }

    internal virtual void ClearFloor()
    {
        // ClearState does not reconstruct the Zone or replay its existing world.
        // Keep actor/cast receipts while allowing genuinely new hot births.
        gazes.Clear();
        donuts.Clear();
        interrupts.Clear();
        stuns.Clear();
        hintDisabled.Clear();
        foreach (var source in actors.Values)
            if (source.Responded && !source.Observed) clearedPending.Add(source);
    }

    internal virtual void Reset()
    {
        actors.Clear();
        ordinary.Clear();
        genericGazes.Clear();
        clearedPending.Clear();
        retiredIds.Clear();
        gazes.Clear();
        donuts.Clear();
        interrupts.Clear();
        stuns.Clear();
        hintDisabled.Clear();
        initialized = false;
    }
}
