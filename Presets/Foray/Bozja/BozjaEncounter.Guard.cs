using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsGuard => Definition.Key == "DRN3QueensGuard";
    internal Dictionary<ulong, int> GuardParries { get; } = [];
    private List<BozjaActor>? guardFeatures;
    private List<BozjaActor>? guardBursts;
    private List<BozjaActor>? guardTurrets;
    private List<BozjaActor>? guardWards;
    private bool guardReduced;
    internal float GuardSampleRadius => guardReduced ? DelubrumArenas.GuardDefaultRadius : DelubrumArenas.GuardStartingRadius;

    private void ClearGuard()
    {
        GuardParries.Clear();
        guardFeatures = guardBursts = guardTurrets = guardWards = null;
        guardReduced = false;
    }

    private List<BozjaActor> GuardEnemies(ref List<BozjaActor>? cache, uint oid)
        => cache ??= Actors.Values.Where(a => a.Actor.Oid == oid).ToList();

    private void GuardCreated(BozjaActor actor)
    {
        if (!IsGuard && !IsQueen) return;
        switch (actor.Actor.Oid)
        {
            case 0x1EA1A1: guardFeatures?.Add(actor); break;
            case 0x30BE when IsGuard:
            case 0x310F when IsQueen: guardBursts?.Add(actor); break;
            case 0x30C2 when IsGuard:
            case 0x3113 when IsQueen: guardTurrets?.Add(actor); break;
            case 0x30BB: guardWards?.Add(actor); break;
        }
        if (Active && actor.NativeNpc) GuardBoardCreated(actor);
    }

    private void GuardBoardCreated(BozjaActor actor)
    {
        if (actor.Actor.Oid != (IsQueen ? 0x310E : 0x30BD)) return;
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.GuardAboveBoard);
        Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            0, Now.AddSeconds(IsQueen ? 21d : 14d)));
    }

    private void GuardRemoved(BozjaActor actor)
    {
        guardFeatures?.Remove(actor);
        guardBursts?.Remove(actor);
        guardTurrets?.Remove(actor);
        guardWards?.Remove(actor);
    }

    private void GuardUntargetable(BozjaActor actor, BozjaRule? rule = null)
    {
        if (!Active || !IsGuard) return;
        if (actor.Actor.Oid == 0x30BC && (rule == null || rule.Kind == BozjaRuleKind.GuardAboveBoard))
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GuardAboveBoard);
        if (actor.Actor.Oid == 0x30C1 && (rule == null || rule.Kind == BozjaRuleKind.GuardTurrets))
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GuardTurrets);
    }

    private void UpdateGuard()
    {
        if (!IsGuard || guardReduced || Casts.Any(c => c.Rule.Kind == BozjaRuleKind.GuardArena)) return;
        foreach (var feature in GuardEnemies(ref guardFeatures, 0x1EA1A1))
            if (feature.Actor.EventState == 0 && MathF.Abs(feature.Actor.Position.X - 244) <= 1
                && MathF.Abs(feature.Actor.Position.Z + 129) <= 1)
            {
                var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.GuardArena);
                Casts.Add(new(++serial, rule, feature, Center, 0, Now.AddSeconds(5d)));
                return;
            }
    }

    private void GuardMap(byte index, uint state)
    {
        if (!Active || !IsGuard || index != 0x18 || state != 0x00020001) return;
        Arena = DelubrumArenas.GuardDefault(Center.Y);
        Center = Arena.Position;
        guardReduced = true;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GuardArena);
    }

    private void StartGuard(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.GuardParry)
        {
            GuardParries[actor.Actor.Id] = (actor.Cast!.ActionId == 22518 ? 3 : 12) << 4
                | GuardParries.GetValueOrDefault(actor.Actor.Id) & 15;
        }
        if (rule.Kind == BozjaRuleKind.GuardAboveBoard)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            var activation = Activation(actor, 15.1f);
            foreach (var bomb in GuardEnemies(ref guardBursts, IsQueen ? 0x310Fu : 0x30BEu))
                Casts.Add(new(++serial, rule, bomb, DungeonEncounter.QuantizedDungeonPosition(bomb.Actor.Position),
                    0, activation));
        }
        if (rule.Kind != BozjaRuleKind.GuardTurrets) return;
        var delta = actor.Cast!.Location - actor.Actor.Position;
        var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        var activationTime = Activation(actor);
        Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            MathF.Atan2(delta.X, delta.Z), activationTime)
            { ShapeOverride = RayRectangleTemplate.Symmetric(6, length) });
        var turrets = GuardEnemies(ref guardTurrets, IsQueen ? 0x3113u : 0x30C2u);
        if (Casts.Count(c => c.Rule == rule) != turrets.Count / 2) return;
        foreach (var turret in turrets)
        {
            var (sin, cos) = Math.SinCos(turret.Actor.Rotation);
            var dx = (float)sin;
            var dz = (float)cos;
            var minDistance = float.MaxValue;
            BozjaActor? closest = null;
            foreach (var candidate in turrets)
            {
                if (ReferenceEquals(candidate, turret)) continue;
                var offset = candidate.Actor.Position - turret.Actor.Position;
                var forward = offset.X * dx + offset.Z * dz;
                var side = offset.X * dz - offset.Z * dx;
                if (!(forward >= 0 && forward <= 50 && MathF.Abs(side) <= .1f)) continue;
                var distance = offset.X * offset.X + offset.Z * offset.Z;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = candidate;
                }
            }
            Casts.Add(new(++serial, rule, turret, turret.Actor.Position, turret.Actor.Rotation, activationTime)
                { ShapeOverride = RayRectangleTemplate.Symmetric(6, closest != null ? MathF.Sqrt(minDistance) : 50) });
        }
    }

    private void GuardEffect(BozjaActor actor, uint action)
    {
        if (!IsGuard && !IsQueen) return;
        if (IsQueen ? action is 22994 or 23430 : action is 22525 or 23402)
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GuardAboveBoard);
        if (!(IsQueen ? action is 23008 or 23009 or 23010 : action is 22541 or 22542 or 22543)) return;
        var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.GuardTurrets && c.Source.Actor.Id == actor.Actor.Id);
        if (index >= 0) Casts.RemoveAt(index);
    }

    private void GuardStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!Active || !IsGuard || status.Id != 680) return;
        var state = gain ? status.Param & 15 : GuardParries.GetValueOrDefault(actor.Actor.Id) & ~15;
        // Source Gain keeps a zero-valued entry; Lose/Predict use UpdateState.
        if (!gain && state == 0) GuardParries.Remove(actor.Actor.Id);
        else GuardParries[actor.Actor.Id] = state;
    }

    private IEnumerable<BozjaActor> GuardActiveWards
        => GuardEnemies(ref guardWards, 0x30BB).Where(a => a.Actor.Targetable && !a.Actor.Dead);

    internal static int GuardAttackSide(BozjaActor viewer, BozjaActor target)
    {
        var offset = viewer.Actor.Position - target.Actor.Position;
        var length = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        var inverse = length > 0 ? 1f / length : 0;
        var x = offset.X * inverse;
        var z = offset.Z * inverse;
        var (sin, cos) = Math.SinCos(target.Actor.Rotation);
        var dx = (float)sin;
        var dz = (float)cos;
        var forward = x * dx + z * dz;
        return forward > .7071067f ? 1 : forward < -.7071067f ? 2 : x * dz - z * dx > 0 ? 4 : 8;
    }

    private IEnumerable<BozjaHint> GuardHints(BozjaActor viewer)
    {
        if (GuardParries.Count == 0) yield break;
        var target = GuardActiveWards.FirstOrDefault(a => a.Actor.Id == viewer.Actor.TargetId);
        if (target == null || !GuardParries.TryGetValue(target.Actor.Id, out var state)) yield break;
        if (((state & 15 | state >> 4 & 15) & GuardAttackSide(viewer, target)) != 0)
            yield return new("Attack target from unshielded side!");
    }

    // Old navigation host ignores AI target priority. Preserve the intent for
    // diagnostics without introducing new PR execution or whole-target holds.
    internal IEnumerable<ulong> GuardForbiddenTargets(BozjaActor viewer)
    {
        if (!Active || !IsGuard) yield break;
        foreach (var (id, state) in GuardParries)
            if ((state & 15) != 0 && Actors.GetValueOrDefault(id) is { } target
                && ((state & 15 | state >> 4 & 15) & GuardAttackSide(viewer, target)) != 0)
                yield return id;
    }

    private IEnumerable<BozjaZone> GuardNavigation(BozjaActor viewer)
    {
        foreach (var (id, state) in GuardParries)
        {
            if (Actors.GetValueOrDefault(id) is not { } target || viewer.Actor.TargetId != id) continue;
            var forbidden = state & 15 | state >> 4 & 15;
            if ((forbidden & GuardAttackSide(viewer, target)) == 0) continue;
            foreach (var (bit, degrees) in new (int, float)[] { (1, 0), (4, 90), (2, 180), (8, 270) })
                if ((forbidden & bit) != 0)
                    yield return new($"guard/parry/{id}/{bit}", "CoatOfArms", DateTime.MaxValue,
                        new FanTemplate(100, 2 * (45f * (MathF.PI / 180f))), target.Actor.Position,
                        target.Actor.Rotation + degrees * (MathF.PI / 180f));
        }
    }

    internal IEnumerable<(string Key, Vector3[] Points, bool Active)> GuardArcs()
    {
        if (!Active || !IsGuard) yield break;
        foreach (var actor in GuardActiveWards)
        {
            if (!GuardParries.TryGetValue(actor.Actor.Id, out var state)) continue;
            foreach (var (bit, degrees) in new (int, float)[] { (1, 0), (2, 180), (4, 90), (8, 270) })
            {
                if (((state & 15 | state >> 4 & 15) & bit) == 0) continue;
                var direction = actor.Actor.Rotation + degrees * (MathF.PI / 180f);
                var min = direction - 45f * (MathF.PI / 180f);
                var max = direction + 45f * (MathF.PI / 180f);
                var segments = Math.Clamp((int)MathF.Ceiling(MathF.Abs(max - min) * 24f / MathF.PI), 2, 64);
                var points = new Vector3[segments + 1];
                for (var i = 0; i <= segments; ++i)
                {
                    var (sin, cos) = Math.SinCos(min + (max - min) * i / segments);
                    points[i] = actor.Actor.Position + new Vector3(1.5f * (float)sin, 0, 1.5f * (float)cos);
                }
                yield return ($"guard/parry/{actor.BirthOrder}/{bit}", points, (state & bit) != 0);
            }
        }
    }
}
