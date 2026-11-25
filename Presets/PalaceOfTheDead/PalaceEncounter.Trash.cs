using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed partial class PalaceEncounter
{
    private readonly Dictionary<ulong, PalaceTrashCaster> trashCasters = [];
    private readonly List<PalaceTrashGaze> trashGazes = [];
    private readonly Dictionary<ulong, PalaceTrashGaze> genericGazes = [];
    private readonly HashSet<PalaceTrashCaster> trashInterrupts = [];
    private readonly HashSet<PalaceTrashCaster> trashHintDisabled = [];
    private static readonly LocalPolygonTemplate trashEye = DungeonEncounter.CreateQueenEyeSymbol();
    private static readonly LocalPolygonTemplate trashViewArc = DungeonEncounter.CreateQueenGazeArc();

    private static float TrashTotal(ReplayCast cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);

    private static bool SameTrashCast(ReplayCast a, ReplayCast b)
        => a.ActionId == b.ActionId && a.IsSpell == b.IsSpell && SanitizeTrashTarget(a.TargetId) == SanitizeTrashTarget(b.TargetId) && a.Location == b.Location
            && a.Rotation == b.Rotation && TrashTotal(a) == TrashTotal(b);

    private static ulong SanitizeTrashTarget(ulong id) => id == 0xE0000000 ? 0 : id;

    private static void AdvanceTrashClock(PalaceTrashCaster source, DateTime now)
    {
        if (source.Cast is { } cast && now > source.Clock)
            source.Elapsed = MathF.Min(source.Elapsed + (float)(now - source.Clock).TotalSeconds, TrashTotal(cast) + source.Extra);
        source.Clock = now;
    }

    private void SynchronizeTrash(ReplayActorState actor, DateTime now, bool observeCasts,
        Func<ReplayActorState, PalaceCast?>? resolve)
    {
        if (!trashCasters.TryGetValue(actor.Id, out var source))
            trashCasters[actor.Id] = source = new(actor);
        source.Actor = actor;
        if (actor.Kind == 1 || PalaceCatalog.IsBoss(actor.Oid)) return;
        AdvanceTrashClock(source, now);
        if (!observeCasts) return;
        var next = actor.Cast;
        if (next == null)
        {
            // The source lists expire on CastInfo == null, not the estimated hit,
            // death, Effect, or a Finished callback immediately followed by Start.
            if (source.Observed || now >= source.Start.AddSeconds(.2))
            {
                ClearTrashCast(source, clearZone: false);
                if (casts.TryGetValue(actor.Id, out var old) && !PalaceCatalog.IsBossAction(old.Action))
                    casts.Remove(actor.Id);
            }
            return;
        }
        if (source.Cast is { } previous && SameTrashCast(previous, next))
        {
            source.Observed = true;
            return;
        }
        var confirmedPacket = source.Cast == null && source.Action == next.ActionId
            && source.Responded && Math.Abs((next.StartedAt - source.Start).TotalSeconds) < .25;
        if (!confirmedPacket) source.Responded = false;
        source.Cast = next;
        source.Clock = now;
        source.Elapsed = Math.Clamp((float)(now - next.StartedAt).TotalSeconds, 0, TrashTotal(next));
        source.Observed = true;
        source.Action = next.ActionId;
        source.Start = next.StartedAt;
        // Like an empty MechanicWorldFeed, create each native actor before its
        // first CastInfo diff. This is not component-style replay on a filled world.
        if (!confirmedPacket) AddTrashResponse(source, next.ActionId);
        if (!PalaceCatalog.IsBossAction(next.ActionId))
        {
            var value = resolve?.Invoke(actor) ?? new PalaceCast(actor.Id, next.ActionId, next.TargetId,
                next.Location, actor.Position, next.Rotation, next.StartedAt, next.FinishAt);
            if (BossActive) value = value with { Shape = null, GenericGaze = false };
            source.Extra = value.CastExtra;
            casts[actor.Id] = value;
            // The generic builder finishes its old entry on every CastInfo
            // replacement. The Zone's separate lists persist until raw null.
            genericGazes.Remove(actor.Id);
            if (value is { GenericGaze: true, Shape: { } gazeShape })
                genericGazes[actor.Id] = new(source, value.Action, gazeShape,
                    trashCasters.GetValueOrDefault(SanitizeTrashTarget(next.TargetId)), Generic: true);
        }
    }

    private void StartTrashResponse(PalaceCast cast)
    {
        if (!trashCasters.TryGetValue(cast.Source, out var source)) return;
        if (source.FinishedAction == cast.Action && cast.Start <= source.FinishedStart.AddSeconds(.25)) return;
        if (source.Responded && source.Action == cast.Action && Math.Abs((source.Start - cast.Start).TotalSeconds) < .25) return;
        // A delayed packet cannot replace a newer observed cast or its identity.
        if (source.Start > cast.Start.AddSeconds(.25)) return;
        if (source.Cast is { } observed && observed.ActionId == cast.Action
            && Math.Abs((observed.StartedAt - cast.Start).TotalSeconds) < .25)
        {
            AddTrashResponse(source, cast.Action);
            return;
        }
        source.Action = cast.Action;
        source.Start = cast.Start;
        source.Observed = false;
        source.Cast = null;
        source.Clock = cast.Start;
        source.Elapsed = 0;
        AddTrashResponse(source, cast.Action);
    }

    private void AddTrashResponse(PalaceTrashCaster source, uint action)
    {
        source.Responded = true;
        GeometryTemplate? shape = action switch
        {
            6351 => new CircleTemplate(100),
            6356 => new FanTemplate(8.2f, MathF.PI / 2),
            6953 => new CircleTemplate(30),
            _ => null,
        };
        if (shape != null) trashGazes.Add(new(source, action, shape));
        if (PalaceCatalog.Interrupts.Contains(action)) trashInterrupts.Add(source);
        if (action is 6356 or 6365) trashHintDisabled.Add(source);
    }

    private void ClearTrashCast(PalaceTrashCaster source, bool clearZone = true)
    {
        if (source.Action != 0)
        {
            source.FinishedAction = source.Action;
            source.FinishedStart = source.Start;
        }
        if (clearZone)
        {
            trashGazes.RemoveAll(g => ReferenceEquals(g.Source, source));
            trashInterrupts.Remove(source);
            trashHintDisabled.Remove(source);
        }
        genericGazes.Remove(source.Actor.Id);
        source.Cast = null;
        source.Action = 0;
        source.Observed = false;
        source.Responded = false;
    }

    private DateTime TrashActivation(PalaceTrashCaster source, DateTime now)
    {
        AdvanceTrashClock(source, now);
        if (source.Cast is { } cast)
            return now.AddSeconds((TrashTotal(cast) + .3f) - source.Elapsed);
        return casts.GetValueOrDefault(source.Actor.Id)?.Hit ?? now;
    }

    private void PruneZoneResponses()
    {
        if (!ZoneMechanicsEnabled || BossActive) return;
        trashGazes.RemoveAll(g => g.Source.Cast == null && g.Source.Action == 0);
        trashInterrupts.RemoveWhere(s => s.Cast == null && s.Action == 0);
        trashHintDisabled.RemoveWhere(s => s.Cast == null && s.Action == 0);
    }

    internal IEnumerable<ReplayActorState> InterruptCandidates
    {
        get
        {
            PruneZoneResponses();
            return ZoneMechanicsEnabled && !BossActive
                ? trashInterrupts.Select(s => s.Actor).Where(a => a.Cast is { Interruptible: true }) : [];
        }
    }

    private IEnumerable<(PalaceTrashGaze Gaze, Vector3 Origin, DateTime Hit)> TrashEyes(DateTime now, ReplayActorState player)
    {
        if (BossActive) yield break;
        PruneZoneResponses();
        // AutoClear iterates its lists in reverse insertion order.
        for (var i = ZoneMechanicsEnabled ? trashGazes.Count - 1 : -1; i >= 0; --i)
        {
            var gaze = trashGazes[i];
            var actor = gaze.Source.Actor;
            if (DungeonSourceGeometry.Contains(gaze.Shape, actor.Position, actor.Rotation, player.Position))
                yield return (gaze, actor.Position, TrashActivation(gaze.Source, now));
        }
        foreach (var gaze in genericGazes.Values)
        {
            if (gaze.Source.Cast is not { } cast) continue;
            var origin = gaze.Target?.Actor.Position ?? cast.Location;
            var rotation = cast.Rotation ?? gaze.Source.Actor.Rotation;
            if (DungeonSourceGeometry.Contains(gaze.Shape, origin, rotation, player.Position))
                yield return (gaze, origin, TrashActivation(gaze.Source, now));
        }
    }

    private IEnumerable<PalaceArea> TrashGazeMarkers(DateTime now, ReplayActorState player)
    {
        var shown = false;
        foreach (var (gaze, origin, hit) in TrashEyes(now, player))
        {
            var actor = gaze.Source.Actor;
            var delta = origin - player.Position;
            var direction = MathF.Atan2(delta.X, delta.Z);
            var danger = MathF.Abs(MathF.Atan2(MathF.Sin(player.Rotation - direction),
                MathF.Cos(player.Rotation - direction))) <= MathF.PI / 4;
            var color = danger ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1);
            var key = $"trash/gaze/{(gaze.Generic ? "generic/" : "")}{actor.Id}/{gaze.Action}";
            yield return new(key + "/eye", "背对视线", trashEye, origin, 0, hit, DateTime.MaxValue,
                actor.Id, gaze.Action, PreviewOnly: true, Color: color, Unclipped: true);
            yield return new(key + "/pupil", "视线瞳孔", new CircleTemplate(.28f), origin, 0, hit, DateTime.MaxValue,
                actor.Id, gaze.Action, PreviewOnly: true, Color: new Vector4(.04f, .04f, .04f, 1), Unclipped: true);
            shown = true;
        }
        if (shown)
            yield return new("trash/gaze/view", "视线前方九十度判定弧", trashViewArc, player.Position, player.Rotation,
                now, DateTime.MaxValue, PreviewOnly: true, Color: new Vector4(1, 0, 0, 1), Unclipped: true);
    }
}
