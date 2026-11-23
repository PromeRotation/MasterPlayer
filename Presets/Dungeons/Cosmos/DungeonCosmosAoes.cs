using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> CosmosAoes()
    {
        if (territory != CosmosCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x2C1A)
        {
            foreach (var pool in cosmosPools.Values) yield return pool;
            foreach (var pool in _actors.Values.Where(a => a.Oid == 0x1EAEAE && a.EventState != 7))
                yield return new($"cosmos/pool/{pool.Id}", "扫除污渍", new CircleTemplate(6), QuantizedDungeonPosition(pool.Position));
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 18852))
                yield return new($"cosmos/tribulation/{cast.Source}", "污渍落点", new CircleTemplate(6),
                    _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 18282))
                if (_actors.TryGetValue(cast.TargetId, out var target))
                {
                    yield return new($"cosmos/stack/{cast.Source}", "四人分摊", new CircleTemplate(6), target.Position,
                        Navigate: false, Safe: true, Activation: cast.FinishAt);
                    if (target.Id != PlayerId) yield return new($"cosmos/stack/enter/{cast.Source}", "进入四人分摊",
                        new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 6)]), Arena.Position, Display: false, Activation: cast.FinishAt);
                    else
                    {
                        var partners = _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead)
                            .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                        if (partners.Length > 0)
                            yield return new($"cosmos/stack/enter/{cast.Source}", "分摊点名靠近队友",
                                new SohrKhaiWallHintTemplate(Arena, partners), Arena.Position, Display: false, Activation: cast.FinishAt);
                    }
                }
            foreach (var broom in _actors.Values.Where(a => a.Oid == 0x2C1B && a.AnimationState1 == 1))
            {
                yield return new($"cosmos/broom/{broom.Id}", "扫帚移动预告", new DungeonCapsuleTemplate(3.125f, 10), broom.Position, broom.Rotation, Navigate: false);
                yield return new($"cosmos/broom/{broom.Id}/now", "扫帚当前位置", new CircleTemplate(3.125f), broom.Position, Display: false);
                foreach (var (length, delay) in new (float, double)[] { (5, 1.1), (10, 3), (20, 10), (30, double.PositiveInfinity) })
                    yield return new($"cosmos/broom/{broom.Id}/{length}", "扫帚后续移动", new DungeonCapsuleTemplate(3.125f, length),
                        broom.Position, broom.Rotation, Display: false, Activation: double.IsPositiveInfinity(delay) ? DateTime.MaxValue : _now.AddSeconds(delay));
            }
        }
        if (Active?.BossOid == 0x2C04)
        {
            if (CosmosTilesActive)
            {
                for (var i = 0; i < cosmosTiles.Length; ++i)
                    yield return new($"cosmos/tile/{i}", "种子易发芽绿地", new RectangleTemplate(10, 10), cosmosTiles[i],
                        Navigate: CosmosTransporting, OutlineOnly: !CosmosTransporting);
                if (!CosmosTransporting)
                    foreach (var seed in _actors.Values.Where(a => CosmosCatalog.IsSeed(a.Oid) && !a.Dead && a.Targetable
                        && CosmosTileShapes().Any(t => t.Contains(a.Position))))
                        yield return new($"cosmos/seed/{seed.Id}", "靠近并搬运种子", new CircleTemplate(2.5f), seed.Position, Safe: true, OutlineOnly: true);
            }
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 18209))
                if (_actors.TryGetValue(PlayerId, out var player) && !KnockbackImmune(player, cast.FinishAt))
                {
                    var hazards = _casts.Values.Where(c => c.Source != cast.Source && c.Rule.Shape != null && c.Rule.PartyAoe == null)
                        .Select(c => c.Rule.Shape!.Resolve(c.Location, c.Rotation, null)).ToArray();
                    var knockback = new CosmosKnockbackTemplate(Arena.Position, cast.Rotation, hazards);
                    yield return new($"cosmos/wind/{cast.Source}", "顺风击退安全落点", knockback, Arena.Position, Display: false, Activation: cast.FinishAt);
                    var landing = knockback.Landing(player.Position);
                    yield return new($"cosmos/wind/{cast.Source}/landing", "预计击退落点", new CircleTemplate(.5f), landing, Navigate: false, Safe: true, OutlineOnly: true);
                }
        }
        if (Active?.BossOid != 0x2C13 || !_actors.TryGetValue(PrimaryId, out var boss)) yield break;
        var furniture = CosmosFurniture().ToArray();
        foreach (var (id, activation) in cosmosBlackFlames)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            var own = id == PlayerId;
            yield return new($"cosmos/black/{id}/circle", "黑火圆形诱导", new CircleTemplate(6), target.Position,
                Navigate: !own, OutlineOnly: own, Activation: activation);
            yield return new($"cosmos/black/{id}/cross", "黑火十字诱导", new DungeonCrossTemplate(10, 2), target.Position,
                CosmosCatalog.BLACK_FLAME_ROTATION, Navigate: !own, OutlineOnly: own, Activation: activation);
            if (!own) continue;
            foreach (var actor in furniture)
            {
                yield return new($"cosmos/black/{id}/circle/avoid/{actor.Id}", "黑火避开家具", new CircleTemplate(6 + actor.HitboxRadius), actor.Position, Display: false, Activation: activation);
                yield return new($"cosmos/black/{id}/cross/avoid/{actor.Id}", "黑火十字避开家具", new DungeonCrossTemplate(10 + actor.HitboxRadius, 2 + actor.HitboxRadius),
                    actor.Position, CosmosCatalog.BLACK_FLAME_ROTATION, Display: false, Activation: activation);
            }
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                yield return new($"cosmos/black/{id}/party/{actor.Id}", "黑火远离队友", new CircleTemplate(6), actor.Position, Display: false, Activation: activation);
                yield return new($"cosmos/black/{id}/party-cross/{actor.Id}", "黑火十字避开队友", new DungeonCrossTemplate(10, 2),
                    actor.Position, CosmosCatalog.BLACK_FLAME_ROTATION, Display: false, Activation: activation);
            }
        }
        for (var i = 0; i < cosmosCharges.Count; ++i)
        {
            if (!_actors.TryGetValue(cosmosCharges[i], out var target)) continue;
            var delta = target.Position - boss.Position; delta.Y = 0;
            var length = delta.Length(); var rotation = MathF.Atan2(delta.X, delta.Z);
            var own = target.Id == PlayerId;
            if (length > .001f)
                yield return new($"cosmos/charge/{i}", "四连冲锋", RayRectangleTemplate.Symmetric(4, length),
                    boss.Position, rotation, Navigate: !own, OutlineOnly: own);
            var coneOrigin = target.Position - (length > .001f ? delta / length : Vector3.Zero) * (target.HitboxRadius + boss.HitboxRadius);
            yield return new($"cosmos/charge/{i}/cone", "冲锋后扇形预告", new FanTemplate(20, MathF.PI / 2), coneOrigin, rotation, Navigate: !own, OutlineOnly: own);
            if (!own) continue;
            foreach (var actor in furniture.Concat(_actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead)))
                yield return new($"cosmos/charge/{i}/avoid/{actor.Id}", "冲锋远离家具及队友", new CircleTemplate(10), actor.Position, Display: false);
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var offset = actor.Position - boss.Position; offset.Y = 0;
                var distance = offset.Length();
                // A player inside the charge half-width cannot define a finite tangent:
                // every departure direction intersects that player's clearance circle.
                if (distance <= 2)
                    yield return new($"cosmos/charge/{i}/line-avoid/{actor.Id}", "冲锋路径避开队友", new CircleTemplate(100), boss.Position, Display: false);
                else
                    yield return new($"cosmos/charge/{i}/line-avoid/{actor.Id}", "冲锋路径避开队友",
                        new FanTemplate(100, 2 * MathF.Asin(2 / distance)), boss.Position, MathF.Atan2(offset.X, offset.Z), Display: false);
            }
        }
        foreach (var (source, tether) in cosmosTethers.Where(t => t.Value.Present && (t.Key == PlayerId || t.Value.Target == PlayerId)))
            if (_actors.TryGetValue(source, out var from) && _actors.TryGetValue(tether.Target, out var to))
            {
                var offset = to.Position - from.Position; offset.Y = 0;
                if (offset.Length() > .001f)
                    yield return new($"cosmos/tether/{source}", "冲锋距离连线", RayRectangleTemplate.Symmetric(.08f, offset.Length()),
                        from.Position, MathF.Atan2(offset.X, offset.Z), Navigate: false, Safe: tether.Id == 1);
            }
        if (cosmosTethers.Any(t => t.Value.Present && (t.Key == PlayerId || t.Value.Target == PlayerId)))
            yield return new("cosmos/tether/stretch", "拉长冲锋连线", new RectangleTemplate(47, 47), Arena.Position, Display: false);
        var burning = _actors.GetValueOrDefault(PlayerId);
        if (burning != null && HasStatus(burning, 2136) && furniture.Length > 0)
        {
            foreach (var actor in furniture)
                yield return new($"cosmos/flame/furniture/{actor.Id}", "可传火家具", new CircleTemplate(actor.HitboxRadius), actor.Position,
                    Navigate: false, Safe: true, OutlineOnly: true);
            yield return new("cosmos/flame/enter", "将火传给家具", new SohrKhaiWallHintTemplate(Arena,
                furniture.Select(a => (IGeometryShape)new CircleGeometry(a.Position, .8f)).ToArray()), Arena.Position,
                Display: false, Activation: burning.StatusExpirations?.GetValueOrDefault(2136u, _now) ?? _now);
        }
        if (cosmosBlackFlames.ContainsKey(PlayerId) || cosmosCharges.Contains(PlayerId))
            foreach (var actor in furniture)
                yield return new($"cosmos/furniture/{actor.Id}", "需保护家具", new CircleTemplate(actor.HitboxRadius), actor.Position, Navigate: false, OutlineOnly: true);
    }
}
