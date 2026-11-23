using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsArchon => territory == ArrQuestCatalog.ARCHON;
    private bool IsLahabrea => territory == ArrQuestCatalog.ULTIMATE_WEAPON;
    private bool IsStepsOfFaith => territory == ArrQuestCatalog.STEPS_OF_FAITH;
    private bool IsArrQuest => IsArchon || IsLahabrea || IsStepsOfFaith;
    private bool arrQuestWiping;
    private bool lahabreaArenaChanged;
    private DateTime? lahabreaArenaAt;
    private DateTime? lahabreaSpheresAt;

    private void ArrQuestDirector(uint command)
    {
        if (!IsArrQuest) return;
        if (command == 0x40000005) { arrQuestWiping = true; ResetFight(); }
        if (command == 0x40000011) arrQuestWiping = false;
    }

    private void ArrQuestCast(DungeonPendingCast cast)
    {
        StepsCast(cast);
        if (!IsLahabrea) return;
        if (cast.Rule.Action == 29327 && !lahabreaArenaChanged) lahabreaArenaAt = cast.FinishAt.AddSeconds(.7);
        if (cast.Rule.Action == 29333) lahabreaSpheresAt = cast.FinishAt;
    }

    private void ArrQuestFinish(DungeonPendingCast cast, DateTime at)
    {
        if (IsStepsOfFaith && cast.Rule.Action == 30885) stepsCauterize = false;
        // FireSphere watches the actual cast finish, including an interrupted summon.
        if (IsLahabrea && cast.Rule.Action == 29332) lahabreaSpheresAt = at.AddSeconds(12);
    }

    private void ArrQuestMap(uint index, uint state)
    {
        StepsMap(index, state);
        if (!IsLahabrea || index != 0 || state != 0x00020001) return;
        lahabreaArenaChanged = true;
        Arena = new CircleGeometry(new(-704, Arena!.Position.Y, 480), 15);
    }

    private int ArchonParrySides(DungeonActor actor)
    {
        if (!IsArchon || actor.Oid != 0x38F5 || !HasStatus(actor, 680)) return 0;
        if (statusChanges.TryGetValue((actor.Id, 680), out var change) && _now < change.ChangedAt.AddSeconds(.5))
            return change.Param & 15;
        return actor.StatusParams?.GetValueOrDefault(680u) & 15 ?? 0;
    }

    private bool ArchonParryForbidden(DungeonActor target)
    {
        if (!IsArchon || !_actors.TryGetValue(PlayerId, out var player)) return false;
        var delta = player.Position - target.Position; delta.Y = 0;
        if (delta.LengthSquared() > 0) delta = Vector3.Normalize(delta);
        var forward = new Vector3(MathF.Sin(target.Rotation), 0, MathF.Cos(target.Rotation));
        var dot = Vector3.Dot(delta, forward);
        var leftDot = Vector3.Dot(delta, new(forward.Z, 0, -forward.X));
        var side = leftDot > 0 ? 4 : 8;
        if (dot > .7071067f) side = 1;
        else if (dot < -.7071067f) side = 2;
        return (ArchonParrySides(target) & side) != 0;
    }

    private IEnumerable<DungeonAmbientAoe> ArrQuestAoes()
    {
        if (!IsArrQuest || Active == null || Arena == null) yield break;
        if (IsStepsOfFaith)
        {
            foreach (var aoe in StepsAoes()) yield return aoe;
            yield break;
        }
        if (IsArchon)
        {
            foreach (var add in _actors.Values.Where(a => !a.Dead && a.Targetable && a.Oid is 0x38F7 or 0x38F6))
                yield return new($"archon/add/{add.Id}", "帝国援军", new CircleTemplate(add.HitboxRadius), add.Position,
                    Navigate: false, OutlineOnly: true);
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x38F5 && !a.Dead))
                foreach (var (side, offset) in new[] { (1, 0f), (2, MathF.PI), (4, MathF.PI / 2), (8, -MathF.PI / 2) })
                {
                    if ((ArchonParrySides(actor) & side) == 0) continue;
                    yield return new($"archon/parry/{actor.Id}/{side}", "方向格挡", new PtDonutSectorTemplate(1.45f, 1.55f, MathF.PI / 2),
                        actor.Position, actor.Rotation + offset, Navigate: false, OutlineOnly: true);
                    if (_actors.GetValueOrDefault(PlayerId)?.TargetId == actor.Id && ArchonParryForbidden(actor))
                        yield return new($"archon/parry/move/{actor.Id}/{side}", "前往未格挡方向", new FanTemplate(100, MathF.PI / 2),
                            actor.Position, actor.Rotation + offset, Display: false);
                }
            yield break;
        }
        if (lahabreaArenaAt is { } changeAt)
            yield return new("lahabrea/arena", "场地缩小", new DonutTemplate(15, 20), Arena.Position, Activation: changeAt);
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1EB738 && a.EventState != 7))
            yield return new($"lahabrea/pitch/{actor.Id}", "沥青海", new CircleTemplate(4), actor.Position);
        if (lahabreaSpheresAt > _now)
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x3934))
                yield return new($"lahabrea/sphere/{actor.Id}", "火球爆发", new CircleTemplate(8), QuantizedDungeonPosition(actor.Position),
                    Activation: lahabreaSpheresAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 29756).Take(8))
            yield return new($"lahabrea/cross/{cast.Source}", "古代十字", new CircleTemplate(6), cast.Location, Activation: cast.FinishAt);
        var knockback = _casts.Values.FirstOrDefault(c => c.Rule.Action == 29758);
        if (knockback != null && !(lahabreaSpheresAt > _now && _actors.Values.Any(a => a.Oid == 0x3934)))
            yield return new("lahabrea/knockback/avoid", "击退前靠近中央", new DonutTemplate(5, 100),
                Arena.Position, Display: false, Activation: knockback.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is { } player)
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 29758))
            {
                var delta = player.Position - cast.Location; delta.Y = 0;
                var landing = player.Position + (delta.LengthSquared() > 0 ? Vector3.Normalize(delta) * 10 : Vector3.Zero);
                foreach (var arrow in DungeonMovementPreview($"lahabrea/knockback/{cast.Source}", "击退10码", player.Position, landing))
                    yield return arrow;
            }
    }
}
