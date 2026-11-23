using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> StigmaAoes()
    {
        if (territory != StigmaCatalog.TERRITORY || Arena == null) yield break;
        foreach (var charge in stigmaCharges.Values) yield return charge;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25386))
        {
            IGeometryShape[] regions = [];
            if (cast.TargetId == PlayerId)
                regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            else if (_actors.TryGetValue(cast.TargetId, out var target)) regions = [new CircleGeometry(target.Position, 6)];
            if (regions.Length > 0)
                yield return new($"stigma/stack/{cast.Source}", "四人分摊后击退", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
            if (!_actors.TryGetValue(PlayerId, out var player) || KnockbackImmune(player, cast.FinishAt)) continue;
            var delta = player.Position - cast.Location; delta.Y = 0;
            if (delta.LengthSquared() < .000001f) continue;
            var to = DungeonSquareKnockbackLanding(Arena, player.Position, MathF.Atan2(delta.X, delta.Z), player.HitboxRadius, 15);
            foreach (var preview in DungeonMovementPreview($"stigma/knockback/{cast.Source}", "径向击退15码，遇墙停止", player.Position, to))
                yield return preview;
        }
        if (_actors.TryGetValue(PlayerId, out var current) && StigmaMarch(current, current.Rotation) is { } march)
        {
            if (!RequireStigmaMovementHold)
                yield return new("stigma/march/avoid", "强制行进沿途及落点安全", march, Arena.Position, Display: false);
            var from = current.Position;
            for (var i = 0; i < march.Offsets.Length; ++i)
            {
                var to = current.Position + march.Offsets[i];
                foreach (var preview in DungeonMovementPreview($"stigma/march/{i}", "强制行进路径", from, to)) yield return preview;
                from = to;
            }
        }
    }
}
