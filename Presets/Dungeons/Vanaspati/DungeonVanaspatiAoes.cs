using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> VanaspatiAoes()
    {
        if (territory != VanaspatiCatalog.TERRITORY || Arena == null) yield break;
        if (vanaspatiRing != null) yield return vanaspatiRing;
        if (Active?.BossOid == 0x33E9)
        {
            var fire = _casts.Values.FirstOrDefault(c => c.Rule.Action == 25147);
            var bubbles = vanaspatiBubbles.Select(id => _actors.GetValueOrDefault(id)).OfType<DungeonActor>().ToArray();
            foreach (var bubble in bubbles.Where(a => !a.Dead))
                yield return new($"vanaspati/bubble/{bubble.Id}", "水泡", new CircleTemplate(2.5f), bubble.Position,
                    Navigate: fire == null, Safe: fire != null);
            if (fire != null && bubbles.Length > 0)
            {
                // The source uses every unconsumed bubble, including one that has just died.
                var regions = bubbles.Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2.5f)).ToArray();
                yield return new("vanaspati/fire/enter", "进入水泡躲避火喷", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: fire.FinishAt);
            }
            if (_actors.TryGetValue(PlayerId, out var player))
                foreach (var water in _casts.Values.Where(c => c.Rule.Action == 25148 && !KnockbackImmune(player, c.FinishAt)))
                {
                    var landing = new VanaspatiKnockbackTemplate(Arena, water.Location,
                        bubbles.Where(a => !a.Dead).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2.5f)).ToArray());
                    foreach (var preview in DungeonMovementPreview("vanaspati/water", "水喷击退落点", player.Position, landing.Landing(player.Position))) yield return preview;
                    if (bubbles.Length == 0) continue;
                    yield return new("vanaspati/water/center", "击退前靠近来源七码", new SohrKhaiWallHintTemplate(Arena,
                        [new CircleGeometry(water.Location, 7)]), Arena.Position, Display: false, Activation: water.FinishAt);
                    foreach (var bubble in _actors.Values.Where(a => a.Oid == 0x3731))
                    {
                        var delta = bubble.Position - water.Location; delta.Y = 0;
                        var distance = delta.Length();
                        if (distance <= .001f) continue;
                        yield return new($"vanaspati/water/cone/{bubble.Id}", "避开水泡方向",
                            new FanTemplate(100, 2 * MathF.Asin(Math.Clamp(2.5f / distance, 0, 1))), water.Location,
                            MathF.Atan2(delta.X, delta.Z), Display: false, Activation: water.FinishAt);
                    }
                    yield return new("vanaspati/water/avoid-landing", "水喷落点避开水泡和场外", landing, Arena.Position, Display: false, Activation: water.FinishAt);
                }
            foreach (var stack in _casts.Values.Where(c => c.Rule.Action == 27851))
            {
                var own = stack.TargetId == PlayerId;
                IGeometryShape[] regions = [];
                if (own)
                    regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                else if (_actors.TryGetValue(stack.TargetId, out var stackTarget))
                    regions = [new CircleGeometry(stackTarget.Position, 6)];
                if (regions.Length > 0) yield return new("vanaspati/stack/join", "四人集合分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: stack.FinishAt);
            }
        }
        if (Active?.BossOid != 0x33EB) yield break;
        var knockback = _casts.Values.FirstOrDefault(c => c.Rule.Action == 25169);
        var immune = knockback != null && _actors.TryGetValue(PlayerId, out var actor) && KnockbackImmune(actor, knockback.FinishAt);
        foreach (var stripe in vanaspatiStripes) yield return stripe with { Navigate = knockback == null || immune };
        foreach (var rect in vanaspatiCosmic.Take(3)) yield return rect;
        if (knockback != null && !immune && _actors.TryGetValue(PlayerId, out var target))
        {
            // Upstream's AI uses a 19y half-width, even before the arena shrinks to 20y.
            var landing = new VanaspatiKnockbackTemplate(new RectangleGeometry(Arena.Position, 0, 38, 38), knockback.Location,
                vanaspatiStripes.Select(s => s.Shape.Resolve(s.Position, s.Rotation, null)).ToArray());
            if (vanaspatiStripes.Count > 0)
                yield return new("vanaspati/cosmic/landing", "击退后避开红蓝条带", landing, Arena.Position, Display: false, Activation: knockback.FinishAt);
            foreach (var preview in DungeonMovementPreview("vanaspati/knockback", "天体击退落点", target.Position, landing.Landing(target.Position))) yield return preview;
        }
    }
}
