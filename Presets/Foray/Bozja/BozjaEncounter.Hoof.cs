using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaEye> Eyes { get; } = [];
    internal List<BozjaBurst> Bursts { get; } = [];
    internal List<BozjaDelayedHit> DelayedHits { get; } = [];
    internal int NumDelayedHits { get; private set; }
    private bool eyesInverted;

    private void ClearHoof()
    {
        Eyes.Clear();
        Bursts.Clear();
        DelayedHits.Clear();
        NumDelayedHits = 0;
        eyesInverted = false;
    }

    private void HoofCreated(BozjaActor actor)
    {
        if (!Active || !actor.NativeNpc) return;
        foreach (var rule in Definition.Rules)
            HoofCreatedForRule(actor, rule);
    }

    private void HoofCreatedForRule(BozjaActor actor, BozjaRule rule)
    {
        if (!actor.NativeNpc) return;
        if (rule.Kind == BozjaRuleKind.ArenaOnFinish && actor.Actor.Oid == rule.EnemyOid)
        {
            Center = DungeonEncounter.QuantizedDungeonPosition(Center);
            Arena = new CircleGeometry(Center, 20);
            Casts.RemoveAll(c => c.Rule == rule);
        }
        if (rule.Kind == BozjaRuleKind.Glimmer && actor.Actor.Oid == rule.EnemyOid)
            Bursts.Add(new(new(++serial, rule, actor,
                DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position), 0, Now.AddSeconds(4.9d))));
    }

    private void StartHoof(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.DelayedTankbuster)
        {
            var id = spell.TargetId == actor.Actor.Id ? actor.Actor.TargetId : spell.TargetId;
            if (Actors.GetValueOrDefault(id) is { } target)
                DelayedHits.Add(new(actor, target, Party.Where(p => p.Slot is >= 0 and < 64)
                    .OrderBy(p => p.Slot).FirstOrDefault(p => p.EntityId == id)?.Slot ?? -1, Activation(actor)));
        }
        else if (rule.Kind == BozjaRuleKind.CastGaze)
            Eyes.Add(new(++serial, spell.Location with { Y = 0 }, Activation(actor), false, actor.Actor.Id, Precise: IsAlbeleo, Range: rule.GazeRange));
        else if (spell.ActionId == 20962)
            Eyes.Add(new(++serial, spell.Location with { Y = 0 }, Activation(actor), eyesInverted));
        else if (spell.ActionId == 20961)
        {
            eyesInverted = true;
            for (var i = 0; i < Eyes.Count; ++i) Eyes[i] = Eyes[i] with { Inverted = true };
        }
    }

    private void FinishHoof(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.CastGaze)
        {
            var index = Eyes.FindIndex(e => e.SourceId == actor.Actor.Id);
            if (index >= 0) Eyes.RemoveAt(index);
            return;
        }
        if (rule.Kind == BozjaRuleKind.Gaze)
        {
            if (spell.ActionId == 20962)
            {
                Eyes.Clear();
                eyesInverted = false;
            }
            return;
        }
        Casts.RemoveAll(c => c.Rule == rule);
        Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor).AddSeconds(6d)));
    }

    private void HoofEffect(BozjaActor source, uint action, ulong target)
    {
        if (Definition.Rules.Any(r => r.Kind == BozjaRuleKind.DelayedTankbuster && r.Actions.Contains(action)))
        {
            ++NumDelayedHits;
            var hit = DelayedHits.FindIndex(h => h.Target.Actor.Id == target);
            if (hit >= 0) DelayedHits.RemoveAt(hit);
        }
        if (action != 20963) return;
        var p = source.Actor.Position;
        foreach (var burst in Bursts)
        {
            var origin = burst.Cast.Origin;
            if (MathF.Abs(origin.X - p.X) > .1f || MathF.Abs(origin.Z - p.Z) > .1f) continue;
            if (++burst.NumCasts == 10) Bursts.Remove(burst);
            break;
        }
    }

    internal static bool EyeDanger(BozjaEye eye, ReplayActorState viewer)
    {
        var delta = new Vector2(eye.Position.X - viewer.Position.X, eye.Position.Z - viewer.Position.Z);
        if (eye.Precise)
        {
            var (sin, cos) = Math.SinCos(viewer.Rotation + eye.Forward);
            var reciprocal = 1f / MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            return ((float)sin * (delta.X * reciprocal) + (float)cos * (delta.Y * reciprocal) >= .707107f) != eye.Inverted;
        }
        var facing = new Vector2(MathF.Sin(viewer.Rotation), MathF.Cos(viewer.Rotation));
        return (Vector2.Dot(facing, Vector2.Normalize(delta)) >= .707107f) != eye.Inverted;
    }

    internal IEnumerable<(float Center, float HalfWidth, DateTime Activation)> GazeDirections(ReplayActorState viewer)
    {
        if (!Active) yield break;
        foreach (var eye in EyesFor(Actors.GetValueOrDefault(viewer.Id)).Where(e => DistanceSquared(e.Position, viewer.Position) <= e.Range * e.Range))
        {
            var delta = eye.Inverted ? viewer.Position - eye.Position : eye.Position - viewer.Position;
            yield return (MathF.Atan2(delta.X, delta.Z) - eye.Forward,
                eye.Inverted ? 135f * (MathF.PI / 180f) : 45f * (MathF.PI / 180f), eye.Activation);
        }
    }

    internal float? Facing(ReplayActorState viewer)
    {
        if (IsCuchulainn)
        {
            if (!Actors.TryGetValue(viewer.Id, out var current)) return null;
            var directions = CuchDirections(current).ToArray();
            SortActivations(directions, 0, directions.Length - 1, d => d.Activation);
            var last = directions.Where(d => d.Activation <= Now.AddSeconds(.75)).ToArray();
            return last.Length > 0 ? last[^1].Center + MathF.PI : null;
        }
        // CE32's wide forbidden-direction tuple is consumed by the old bridge
        // as center+PI in its lead window; it does not use the gaze solver.
        if (IsRobots)
            return Actors.GetValueOrDefault(viewer.Id) is { } actor && RobotDirection(actor) is { } direction
                && direction.Activation <= Now.AddSeconds(.75) ? direction.Center + MathF.PI : null;
        return DungeonFacingDirections.Solve(GazeDirections(viewer).Where(d => d.Activation <= Now.AddSeconds(.75))
            .Select(d => (d.Center, d.HalfWidth)).ToArray(), viewer.Rotation);
    }

    internal static Vector3[] EyeArc(BozjaEye eye, ReplayActorState viewer)
    {
        var min = viewer.Rotation + eye.Forward + (eye.Inverted ? 45f : -45f) * (MathF.PI / 180f);
        var max = viewer.Rotation + eye.Forward + (eye.Inverted ? 315f : 45f) * (MathF.PI / 180f);
        var segments = Math.Clamp((int)MathF.Ceiling(MathF.Abs(max - min) * 24f / MathF.PI), 2, 64);
        return Enumerable.Range(0, segments + 1).Select(i =>
        {
            var (sin, cos) = Math.SinCos(min + (max - min) * i / segments);
            return viewer.Position + new Vector3((float)sin, 0, (float)cos);
        }).ToArray();
    }

    internal int? TargetPriority(ReplayActorState actor)
    {
        if (!Active) return null;
        if (IsFire) return FireTargetPriority(actor);
        if (IsSkirmish) return actor.Oid == Definition.PrimaryOid ? 1 : actor.InCombat ? 0 : -3;
        if (!IsAlbeleo && !Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Gaze)) return null;
        return actor.Oid == Definition.PrimaryOid ? 0 : 1;
    }
}
