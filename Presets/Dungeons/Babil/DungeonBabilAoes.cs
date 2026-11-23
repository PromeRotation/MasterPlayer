using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> BabilAoes()
    {
        if (territory != BabilCatalog.TERRITORY || Arena == null) yield break;
        if (babilRing != null) yield return babilRing;
        if (Active?.BossOid == 0x33F7 && babilMagnetAt is { } activation && _actors.TryGetValue(PlayerId, out var player))
        {
            var pull = !babilBossPositive && babilPositive.Contains(PlayerId) || babilBossPositive && babilNegative.Contains(PlayerId);
            var push = babilBossPositive && babilPositive.Contains(PlayerId) || !babilBossPositive && babilNegative.Contains(PlayerId);
            var immune = KnockbackImmune(player, activation);
            // Circular magnetism ignores immunity. Keep its navigation active too.
            if ((!babilMagnetRectangle || !immune) && (pull || push))
            {
                GeometryTemplate shape;
                if (babilMagnetRectangle && pull) shape = new RectangleTemplate(24, 30);
                else if (pull) shape = new CircleTemplate(13);
                else
                {
                    var safe = new CircleGeometry(Arena.Position, babilMagnetRectangle ? 6 : 10);
                    shape = new SohrKhaiWallHintTemplate(Arena, [safe]);
                }
                yield return new("babil/magnet/avoid", "磁力位移前站位", shape, Arena.Position, Display: false, Activation: activation);
            }
            if (!babilMagnetRectangle || !immune)
                foreach (var preview in DungeonMovementPreview("babil/magnet", "磁力预计落点", player.Position,
                    BabilCatalog.MagneticLanding(Arena.Position, player.Position, babilMagnetRectangle, pull)))
                    yield return preview;
        }
        if (Active?.BossOid == 0x33FA && babilFloorMode != 0 && _actors.TryGetValue(PlayerId, out var floorPlayer))
        {
            var toad = new Vector3(213, Arena.Position.Y, 306);
            var mini = new Vector3(229, Arena.Position.Y, 306);
            // Query statuses separately: losing Minimum must not erase Breathless.
            var enterToad = babilFloorMode == 1 && HasStatus(floorPlayer, 2672);
            var enterMini = babilFloorMode == 2 && !babilAvoidFloors && !HasStatus(floorPlayer, 2504);
            yield return new("babil/floor/toad", "绿色变蛙地板", new RectangleTemplate(8, 8), toad, Safe: enterToad);
            yield return new("babil/floor/mini", "紫色缩小地板", new RectangleTemplate(8, 8), mini, Safe: enterMini);
            if (enterToad || enterMini)
                yield return new("babil/floor/enter", "进入对应变身地板", new SohrKhaiWallHintTemplate(Arena,
                    [new RectangleGeometry(enterToad ? toad : mini, 0, 8, 8)]), Arena.Position, Display: false);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25324 && c.TargetId == PlayerId))
        {
            var regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            if (regions.Length > 0)
                yield return new($"babil/stack/{cast.Source}/join", "四人集合分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
        }
        if (!IsBabilAnima) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25357).OrderBy(c => babilClawOrder.GetValueOrDefault(c.Source)).Take(5))
            yield return new($"babil/claw/{cast.Source}", "连续爪击", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        foreach (var graviton in _actors.Values.Where(a => a.Oid == 0x33FF && !a.Dead))
            yield return new($"babil/graviton/{graviton.Id}", "重力球", new CircleTemplate(1), graviton.Position);
        if (babilPain != null)
        {
            yield return babilPain;
            yield return new("babil/pain/avoid-south", "向北脱离无尽痛苦", RayRectangleTemplate.Symmetric(40, 20), babilPain.Position, Display: false);
        }
        foreach (var (pair, tether) in babilTethers)
        {
            if (pair.Target != PlayerId || !_actors.TryGetValue(pair.Source, out var source)
                || !_actors.TryGetValue(pair.Target, out var target) || KnockbackImmune(target, tether.Activation)) continue;
            yield return new($"babil/tether/{pair.Source}/avoid", "拉长重力连线33码", new CircleTemplate(33), source.Position, Display: false, Activation: tether.Activation);
            var delta = target.Position - source.Position; delta.Y = 0; var length = delta.Length();
            if (length > .001f)
                yield return new($"babil/tether/{pair.Source}/line", "重力连线", new RectangleTemplate(.12f, length),
                    (target.Position + source.Position) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: tether.Id == 17);
        }
        if (babilChaseTargets.ContainsKey(PlayerId))
            yield return new("babil/chase/edge", "追踪点名先靠左右场边", new RectangleTemplate(37, 40), Arena.Position, Display: false, Activation: babilChaseAt);
        foreach (var chase in babilChasers)
        {
            var position = BabilChasePosition(chase);
            yield return new($"babil/chase/{chase.Source}/display", $"追踪爆炸：剩余{chase.Remaining}次", new CircleTemplate(3), position, Navigate: false, Activation: chase.Activation);
            var self = chase.Target.Id == PlayerId;
            var avoid = self ? position - new Vector3(MathF.Sin(chase.Target.Rotation), 0, MathF.Cos(chase.Target.Rotation)) * 3 : position;
            yield return new($"babil/chase/{chase.Source}/avoid", "连续追踪避让", new CircleTemplate(self ? 9 : 4), avoid, Display: false, Activation: chase.Activation);
            if (self && _actors.TryGetValue(PlayerId, out var current))
                yield return new($"babil/chase/{chase.Source}/horizontal", "沿水平方向避让追踪", new SohrKhaiWallHintTemplate(Arena,
                    [new RectangleGeometry(current.Position, MathF.PI / 2, 6, 80)]), Arena.Position, Display: false);
            var delta = chase.Target.Position - chase.Previous; delta.Y = 0; var length = delta.Length();
            if (length > .001f)
                yield return new($"babil/chase/{chase.Source}/line", "追踪方向", new RectangleTemplate(.12f, length),
                    (chase.Target.Position + chase.Previous) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false);
        }
    }

    private IEnumerable<DungeonDamagePrediction> BabilDamage()
    {
        if (!IsBabilAnima) yield break;
        foreach (var (pair, tether) in babilTethers)
            if (_actors.TryGetValue(pair.Target, out var target) && !target.Dead && target.PartySlot >= 0)
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, tether.Activation, "Tankbuster");
    }

    private IEnumerable<string> BabilHints()
    {
        if (territory != BabilCatalog.TERRITORY || Arena == null) yield break;
        if (babilFloorMode != 0 && _actors.TryGetValue(PlayerId, out var p))
        {
            if (babilFloorMode == 1 && HasStatus(p, 2672)) yield return "进入绿色地板变蛙";
            if (babilFloorMode == 2 && !babilAvoidFloors && !HasStatus(p, 2504)) yield return "进入紫色地板缩小";
        }
        if (_casts.Values.Any(c => c.Rule.Action == 25359)) yield return "连续16次全屏伤害";
        if (babilTethers.Keys.Any(t => t.Target == PlayerId)) yield return "拉长重力连线，或使用防击退";
        if (babilMagnetAt is { } activation && _actors.TryGetValue(PlayerId, out var player) && (!babilMagnetRectangle || !KnockbackImmune(player, activation)))
        {
            var pull = !babilBossPositive && babilPositive.Contains(PlayerId) || babilBossPositive && babilNegative.Contains(PlayerId);
            var landing = BabilCatalog.MagneticLanding(Arena.Position, player.Position, babilMagnetRectangle, pull);
            if (!Arena.Contains(landing, 0) || _casts.Values.Any(c => c.Rule.Shape != null && c.Rule.PartyAoe == null
                && c.Rule.Shape.Resolve(c.Location, c.Rotation, null).Contains(landing, 0)))
                yield return "磁力落点位于危险区域";
        }
    }
}
