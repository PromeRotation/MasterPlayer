using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, AlphaActorState> eurekaActors = [];
    private readonly List<AlphaActorState> eurekaWheiTargets = [];
    private readonly List<EurekaChaser> eurekaChasers = [];
    private readonly Dictionary<ulong, DungeonPendingCast> eurekaTiamatHits = [];
    private readonly Dictionary<ulong, (AlphaActorState Source, Vector3 Origin, float Rotation, DateTime Start)> eurekaHeadTracks = [];
    private readonly Dictionary<AlphaActorState, DateTime> eurekaRecoveries = [];
    private DateTime? eurekaGridCastStartedAt;
    private DateTime eurekaGridUntil;
    private bool eurekaChaseMissingPosition;
    internal IReadOnlyList<EurekaChaser> EurekaChasers => eurekaChasers;

    private void EurekaActorsObserve()
    {
        if (!IsEurekaOrthos) return;
        if (EurekaUsesOrderedInput)
            eurekaApplyingActorInput = eurekaChimeraBootstrapped;
        foreach (var id in eurekaActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            EurekaChimeraRemoved(eurekaActors[id]);
            eurekaActors[id].Removed = true;
            eurekaTwisterCreated.Remove(eurekaActors[id]);
            eurekaShockCreated.Remove(eurekaActors[id]);
            eurekaActors.Remove(id);
        }
        foreach (var actor in _actors.Values)
        {
            // Active Chimera inputs are applied in source order, so a tether
            // cannot resolve an actor that has not yet been created this frame.
            if (EurekaUsesOrderedInput && Active != null) continue;
            if (eurekaActors.TryGetValue(actor.Id, out var state)) state.Actor = actor;
            else
            {
                eurekaActors[actor.Id] = new(actor);
            }
        }
    }

    private void EurekaUpdate()
    {
        if (EurekaInputScan) return;
        EurekaTwintaniaUpdate();
        if (IsEurekaKaliya)
            eurekaResonances.RemoveAll(b => b.Source.Actor.Dead || b.Source.Removed || b.Target.Actor.Dead);
        if (EurekaUsesOrderedInput && Active != null && !eurekaWiping)
        {
            foreach (var (component, _) in EurekaTetherComponents()) component.Update(_now);
        }
        if (territory != EurekaOrthosCatalog.TIAMAT || Active == null || eurekaWiping) return;
        foreach (var source in eurekaTiamatHits.Keys.Where(id =>
            !_actors.TryGetValue(id, out var actor) || actor.Dead
            || _now > eurekaTiamatHits[id].FinishAt.AddSeconds(.6)).ToArray())
            eurekaTiamatHits.Remove(source);
        eurekaChasers.RemoveAll(c => c.Remaining < 5 && (c.Target.Removed || c.Target.Actor.Dead));
        foreach (var target in eurekaRecoveries.Keys.Where(t => t.Removed || t.Actor.Dead || eurekaRecoveries[t] < _now).ToArray())
            eurekaRecoveries.Remove(target);
        EurekaTiamatHeadsUpdate();
    }

    private void EurekaTiamatHeadsUpdate()
    {
        // The visual selects tighter sweep windows; it is not an independent AOE.
        if (_actors.GetValueOrDefault(PrimaryId)?.Cast is { Action: 31849 } grid
            && eurekaGridCastStartedAt != grid.StartedAt)
        {
            eurekaGridCastStartedAt = grid.StartedAt;
            eurekaGridUntil = _now.AddSeconds(26);
        }
        foreach (var id in eurekaHeadTracks.Keys.Where(id =>
            !eurekaActors.TryGetValue(id, out var source) || source.Actor.Oid != 0x3D9B
            || source.Actor.AnimationState1 != 1 || !ReferenceEquals(source, eurekaHeadTracks[id].Source)).ToArray())
            eurekaHeadTracks.Remove(id);
        foreach (var source in eurekaActors.Values.Where(a => a.Actor.Oid == 0x3D9B && a.Actor.AnimationState1 == 1))
        {
            var head = source.Actor;
            if (!eurekaHeadTracks.TryGetValue(head.Id, out var track))
            {
                eurekaHeadTracks[head.Id] = (source, head.Position, head.Rotation, _now.AddSeconds(1));
                continue;
            }
            var elapsed = Math.Max(0, (_now - track.Start).TotalSeconds);
            var predicted = track.Origin + EurekaHeadDirection(track.Rotation) * (3f * (float)elapsed);
            var delta = head.Rotation - track.Rotation;
            var angle = MathF.Atan2(MathF.Sin(delta), MathF.Cos(delta));
            if (MathF.Abs(angle) > .05f || Vector2.DistanceSquared(
                new(predicted.X, predicted.Z), new(head.Position.X, head.Position.Z)) > .9f * .9f)
                eurekaHeadTracks[head.Id] = (source, head.Position, head.Rotation, _now);
        }
    }

    private static Vector3 EurekaHeadDirection(float rotation) => new(MathF.Sin(rotation), 0, MathF.Cos(rotation));

    private void EurekaTiamatCast(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.TIAMAT) return;
        eurekaTiamatHits.Remove(cast.Source);
        if (cast.Rule.Action is 31844 or 31846 or 31850)
            eurekaTiamatHits[cast.Source] = cast;
    }

    private void EurekaIcon(ulong actorId, uint icon)
    {
        EurekaAdministratorIcon(actorId, icon);
        if (territory == EurekaOrthosCatalog.TIAMAT && Active != null && eurekaEnabled
            && icon == 197 && eurekaActors.TryGetValue(actorId, out var actor))
            eurekaWheiTargets.Add(actor);
    }

    private void EurekaChaseStart(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.TIAMAT || cast.Rule.Action != 31847 || !cast.Observed) return;
        AlphaActorState? target = null;
        var bestDistance = float.MaxValue;
        foreach (var candidate in eurekaWheiTargets)
        {
            var distance = new Vector2(candidate.Actor.Position.X - cast.Location.X,
                candidate.Actor.Position.Z - cast.Location.Z).LengthSquared();
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            target = candidate;
        }
        if (target == null) return;
        eurekaWheiTargets.Remove(target);
        eurekaRecoveries.Remove(target);
        eurekaChasers.Add(new(++eurekaGeneration, target, cast.Location, cast)
        {
            Destination = EurekaBaitDestination(target.Actor.Position, cast.Location),
        });
    }

    private Vector3? EurekaBaitDestination(Vector3 target, Vector3 origin)
    {
        var boundary = Arena ?? EurekaOrthosCatalog.Arena(territory, target.Y);
        Vector3? best = null;
        var bestScore = float.MinValue;
        for (var x = -17f; x <= 17; x += 2)
            for (var z = -17f; z <= 17; z += 2)
            {
                var candidate = new Vector3(boundary.Position.X + x, target.Y, boundary.Position.Z + z);
                if (!boundary.Contains(candidate, 0)) continue;
                var clearance = Vector2.Distance(new(candidate.X, candidate.Z), new(origin.X, origin.Z));
                if (clearance < 14) continue;
                var score = Math.Min(clearance, 33) * 4
                    - Vector2.Distance(new(candidate.X, candidate.Z), new(target.X, target.Z));
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
        return best;
    }

    private void EurekaChaseEffect(ulong source, ulong target, Vector3? location, DateTime now)
    {
        Vector3 position;
        if (target == source && _actors.GetValueOrDefault(source) is { } caster)
            position = QuantizedDungeonPosition(caster.Position);
        else if (_actors.GetValueOrDefault(target) is { } mainTarget)
            position = mainTarget.Position;
        else if (location is { } packetPosition)
            position = packetPosition;
        else
        {
            eurekaChaseMissingPosition = true;
            return;
        }
        eurekaChaseMissingPosition = false;
        EurekaChaser? nearest = null;
        var bestDistance = float.MaxValue;
        foreach (var chaser in eurekaChasers)
        {
            var predicted = chaser.Predicted();
            var distance = new Vector2(predicted.X - position.X, predicted.Z - position.Z).LengthSquared();
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = chaser;
        }
        if (nearest != null)
        {
            if (--nearest.Remaining <= 0)
            {
                eurekaChasers.Remove(nearest);
                eurekaRecoveries[nearest.Target] = now.AddSeconds(10);
            }
            else
            {
                nearest.Previous = position;
                nearest.MoveDistance = 6;
                nearest.Activation = now.AddSeconds(2d);
            }
        }
        if (eurekaChasers.Count == 0) eurekaWheiTargets.Clear();
    }

    private IEnumerable<DungeonAmbientAoe> EurekaTiamatAoes()
    {
        if (territory != EurekaOrthosCatalog.TIAMAT) yield break;
        // Natural CastInfo completion precedes the hit. The mechanic retains its
        // locked footprint; an actual cancellation removes it in EurekaFinish.
        foreach (var cast in eurekaTiamatHits.Values)
            if (_actors.GetValueOrDefault(cast.Source) is { Dead: false }
                && _now <= cast.FinishAt.AddSeconds(.6))
                yield return new($"eureka/hit/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!,
                    cast.Location, cast.Rotation, Activation: cast.FinishAt, ExpiresAt: cast.FinishAt.AddSeconds(.6));
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x3D9B && a.AnimationState1 == 1))
        {
            var id = $"eureka/head/{actor.Id}";
            yield return new(id, "Creature of Darkness", new DungeonCapsuleTemplate(2, 6),
                actor.Position, actor.Rotation, Navigate: false);
            // A moving body must not remain a hard wall for the full route.
            // Keep the observed contact occupied briefly alongside its sweeps.
            yield return new(id + "/body", "Creature of Darkness", new CircleTemplate(2),
                actor.Position, Display: false, Activation: _now, ExpiresAt: _now.AddSeconds(.2),
                HasMotionEnvelope: true);
        }
        var grid = _now < eurekaGridUntil;
        var step = grid ? .15 : .5;
        var lead = grid ? .15 : .8;
        var tail = grid ? .25 : .9;
        var count = grid ? 100 : 30;
        var length = (float)(3 * step);
        foreach (var (id, track) in eurekaHeadTracks)
        {
            // Extend from the current slice so long-lived heads retain a full horizon.
            var elapsed = (_now - track.Start).TotalSeconds;
            var first = (int)Math.Floor(Math.Max(0, elapsed - tail) / step);
            var last = (int)Math.Floor(Math.Max(0, elapsed) / step) + count;
            for (var i = first; i < last; ++i)
            {
                var activation = track.Start.AddSeconds(i * step - lead);
                var expiry = track.Start.AddSeconds((i + 1) * step + tail);
                if (expiry < _now) continue;
                var origin = track.Origin + EurekaHeadDirection(track.Rotation) * (length * i);
                yield return new($"eureka/head/{id}/future/{i}", "Creature of Darkness",
                    new DungeonCapsuleTemplate(2.5f, length), origin, track.Rotation, Display: false,
                    Activation: activation, ExpiresAt: expiry, HasMotionEnvelope: true);
            }
        }
        var viewer = eurekaActors.GetValueOrDefault(PlayerId);
        foreach (var chaser in eurekaChasers)
        {
            var position = chaser.Predicted();
            var own = ReferenceEquals(viewer, chaser.Target);
            var navigation = own ? chaser.Previous : position;
            var radius = 7f;
            if (own) radius = chaser.Remaining == 5 ? 6 : 12.5f;
            yield return new($"eureka/whei/{chaser.Generation}", "Whei Morn", new CircleTemplate(6),
                position, Navigate: false, Activation: chaser.Activation);
            yield return new($"eureka/whei/{chaser.Generation}/navigation", "Whei Morn", new CircleTemplate(radius),
                navigation, Display: false, Activation: chaser.Activation, ExpiresAt: chaser.Activation.AddSeconds(.8),
                RequireOutwardExit: own);
            if (own && !chaser.Target.Removed && !chaser.Target.Actor.Dead && chaser.Destination is { } destination
                && (chaser.Remaining < 5 || _now >= chaser.Cast.FinishAt.AddSeconds(-2.5)))
                yield return new($"eureka/whei/{chaser.Generation}/bait", "Whei Morn bait", new CircleTemplate(1),
                    destination, Display: false, Safe: true, Activation: chaser.Cast.FinishAt.AddSeconds(-2.5),
                    ExpiresAt: chaser.Activation.AddSeconds((chaser.Remaining - 1) * 2 + .8), OverrideDangerMargin: false);
        }
        if (viewer != null && eurekaRecoveries.TryGetValue(viewer, out var recoverUntil)
            && !viewer.Removed && !viewer.Actor.Dead && _now <= recoverUntil
            && !eurekaChasers.Any(c => ReferenceEquals(c.Target, viewer)))
            yield return new("eureka/recovery", "Whei Morn recovery", new CircleTemplate(8),
                (Arena ?? EurekaOrthosCatalog.Arena(territory, viewer.Actor.Position.Y)).Position,
                Display: false, Safe: true, Activation: recoverUntil.AddSeconds(-10),
                ExpiresAt: recoverUntil, OverrideDangerMargin: false);
    }

    internal IEnumerable<(string Id, Vector3 Start, Vector3 End)> EurekaLines()
    {
        if (territory != EurekaOrthosCatalog.TIAMAT || Active == null || !eurekaEnabled
            || !_actors.ContainsKey(PlayerId)) yield break;
        foreach (var chaser in eurekaChasers)
            yield return ($"eureka/whei/{chaser.Generation}/line", chaser.Previous, chaser.Target.Actor.Position);
    }

    private IEnumerable<string> EurekaTiamatHints(Vector3 player)
    {
        if (territory != EurekaOrthosCatalog.TIAMAT || Active == null || !eurekaEnabled) yield break;
        if (eurekaChaseMissingPosition) yield return "Whei Morn：录像缺少主目标及效果区域坐标，追踪判定待确认";
        if (eurekaChasers.Any(c => Vector2.DistanceSquared(new(player.X, player.Z), new(c.Predicted().X, c.Predicted().Z)) <= 36))
            yield return "GTFO from chasing aoe!";
        if (_actors.Values.Any(a => a.Oid == 0x3D9B && a.AnimationState1 == 1
            && new DungeonCapsuleTemplate(2, 6).Resolve(a.Position, a.Rotation, null).Contains(player, 0)))
            yield return "GTFO from voidzone!";
    }
}
