using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureAcheloios => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_ACHELOIOS;
    private bool IsTreasureHippomenes => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.HIPPOMENES;
    private readonly List<TreasureQueuedCone> treasureDoubleHammers = [];
    private readonly List<TreasureQueuedCone> treasureBlasters = [];
    private DungeonPendingCast? treasureHammerDirectionCast;
    private DungeonPendingCast? treasureHammerRotationCast;
    private float treasureHammerIncrement;
    private float treasureHammerRotation;
    private bool treasureHammerDirectionPending;
    private bool treasureHammerRotationPending;
    private long treasureGymnasionId;
    internal IReadOnlyList<TreasureQueuedCone> TreasureDoubleHammers => treasureDoubleHammers;
    internal IReadOnlyList<TreasureQueuedCone> TreasureBlasters => treasureBlasters;

    private void TreasureGymnasionStart(DungeonPendingCast cast, bool correction)
    {
        if (IsTreasureAcheloios)
        {
            if (cast.Rule.Action is 32284 or 32281)
            {
                var positive = 89.999f * (MathF.PI / 180f);
                var negative = -90.004f * (MathF.PI / 180f);
                var angles = cast.Rule.Action == 32284 ? new[] { negative, positive } : [positive, negative];
                TreasureQueueCones(treasureDoubleHammers, cast, correction, angles, false);
            }
            if (cast.Rule.Action is 32280 or 32283)
            {
                if (correction && (!treasureHammerDirectionPending || treasureHammerDirectionCast is not { } previous
                    || previous.Source != cast.Source || previous.StartedAt != cast.StartedAt)) return;
                treasureHammerDirectionCast = cast;
                treasureHammerDirectionPending = true;
                treasureHammerIncrement = cast.Rule.Action == 32283 ? MathF.PI / 2 : -MathF.PI / 2;
            }
            else if (cast.Rule.Action == 32858)
            {
                if (correction && (!treasureHammerRotationPending || treasureHammerRotationCast is not { } previous
                    || previous.Source != cast.Source || previous.StartedAt != cast.StartedAt)) return;
                treasureHammerRotationCast = cast;
                treasureHammerRotationPending = true;
                treasureHammerRotation = cast.Rotation;
            }
            // Both slots are consumed together; unrelated starts cannot make a previously incomplete pair ready.
            if (treasureHammerRotation == 0 || treasureHammerIncrement == 0
                || _actors.GetValueOrDefault(cast.Source) is not { } actor) return;
            treasureRotations.Add(new(++treasureRotationId, cast.Source, actor.Oid, cast.StartedAt, actor.Position,
                treasureHammerRotation, treasureHammerIncrement, cast.FinishAt.AddSeconds(.8d),
                (double)3.3f, 4, 2, 30, MathF.PI));
            treasureHammerRotation = 0;
            treasureHammerIncrement = 0;
            treasureHammerDirectionPending = false;
            treasureHammerRotationPending = false;
            RebuildTreasureRotationAoes(_now);
        }
        else if (IsTreasureHippomenes)
        {
            float[] offsets = cast.Rule.Action switch
            {
                33164 => [0, MathF.PI],
                32342 => [0, MathF.PI, MathF.PI / 2, -MathF.PI / 2],
                32343 => [0, -MathF.PI / 2, MathF.PI, MathF.PI / 2],
                32344 => [0, MathF.PI / 2, -MathF.PI / 2, MathF.PI],
                _ => [],
            };
            if (offsets.Length > 0) TreasureQueueCones(treasureBlasters, cast, correction, offsets, true);
        }
    }

    private void TreasureQueueCones(List<TreasureQueuedCone> queue, DungeonPendingCast cast, bool correction,
        float[] angles, bool blaster)
    {
        TreasureQueuedCone Cone(long id, int order)
        {
            var delay = order == 0 ? .8f : 3.7f;
            if (blaster) delay = 6f + 3f * order;
            return new(id, cast, order, cast.Location, angles[order] + (blaster ? cast.Rotation : 0),
                cast.FinishAt.AddSeconds(delay));
        }
        if (!correction)
        {
            for (var i = 0; i < angles.Length; ++i) queue.Add(Cone(++treasureGymnasionId, i));
            return;
        }
        for (var i = 0; i < queue.Count; ++i)
            if (queue[i].Cast.Source == cast.Source && queue[i].Cast.StartedAt == cast.StartedAt
                && queue[i].Cast.Rule.Action == cast.Rule.Action)
                queue[i] = Cone(queue[i].Id, queue[i].Order);
    }

    private void TreasureGymnasionFinish(DungeonPendingCast cast, DateTime time)
    {
        if (!IsTreasureAcheloios) return;
        if (cast.Rule.Action is 32859 or 32860 or 32861 && treasureDoubleHammers.Count > 0)
            treasureDoubleHammers.RemoveAt(0);
        if (cast.Rule.Action is 32858 or 32860 or 32861 && treasureRotations.Count > 0)
            AdvanceTreasureRotation(0, time);
    }

    private void TreasureBlasterEffect(uint action)
    {
        if (IsTreasureHippomenes && action is 32345 or 33165 or 32346 or 32348 or 32349 or 32347
            && treasureBlasters.Count > 0)
            treasureBlasters.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureGymnasionSequenceAoes()
    {
        for (var i = 0; i < treasureDoubleHammers.Count; ++i)
        {
            var cone = treasureDoubleHammers[i];
            yield return new($"treasure/double-hammer/{cone.Id}", "Double Hammer", new FanTemplate(30, MathF.PI),
                cone.Origin, cone.Rotation, Navigate: i == 0, Activation: cone.Activation,
                Color: i == 0 && treasureDoubleHammers.Count > 1 ? new Vector4(1, 0, 0, 1) : null);
        }
        if (treasureBlasters.Count == 0) yield break;
        var first = treasureBlasters[0];
        yield return new($"treasure/blaster/{first.Id}", "Dibrid / Tetrabrid Blaster",
            new FanTemplate(30, 3 * MathF.PI / 2), first.Origin, first.Rotation, Activation: first.Activation);
        if (treasureBlasters.Count > 1 && Arena != null)
            yield return new("treasure/blaster/center", "连续扇形 / 保持5码内",
                new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(first.Origin, 5)]), Arena.Position,
                Display: false, Activation: first.Activation);
    }

    private void ResetTreasureGymnasionSequences()
    {
        treasureDoubleHammers.Clear();
        treasureBlasters.Clear();
        treasureHammerDirectionCast = null;
        treasureHammerRotationCast = null;
        treasureHammerIncrement = 0;
        treasureHammerRotation = 0;
        treasureHammerDirectionPending = false;
        treasureHammerRotationPending = false;
        treasureGymnasionId = 0;
    }
}
