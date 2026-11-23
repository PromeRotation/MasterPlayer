using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> SmiletonAoes()
    {
        if (territory != SmiletonCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x34CF && _actors.TryGetValue(PlayerId, out var player))
        {
            var from = player.Position;
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 27742))
            {
                if (KnockbackImmune(player, cast.FinishAt)) continue;
                var to = DungeonSquareKnockbackLanding(Arena, from, cast.Rotation, player.HitboxRadius);
                foreach (var preview in DungeonMovementPreview($"smileton/knockback/{cast.Source}", "击退8码，遇墙停止", from, to))
                    yield return preview;
                from = to;
            }
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 26451))
        {
            IGeometryShape[] regions = [];
            if (cast.TargetId == PlayerId)
                regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            else if (_actors.TryGetValue(cast.TargetId, out var target)) regions = [new CircleGeometry(target.Position, 6)];
            if (regions.Length > 0)
                yield return new($"smileton/stack/{cast.Source}", "四人分摊电弧", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
        }
    }

    internal static Vector3 DungeonSquareKnockbackLanding(IGeometryShape arena, Vector3 from, float rotation, float hitboxRadius, float distance = 8)
    {
        var dir = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
        var bounds = arena.GetBounds();
        if (Math.Abs(dir.X) > .00001f)
            distance = Math.Min(distance, ((dir.X > 0 ? bounds.MaxX : bounds.MinX) - from.X) / dir.X
                - Math.Max(hitboxRadius - .499f, .001f));
        if (Math.Abs(dir.Z) > .00001f)
            distance = Math.Min(distance, ((dir.Z > 0 ? bounds.MaxZ : bounds.MinZ) - from.Z) / dir.Z
                - Math.Max(hitboxRadius - .499f, .001f));
        return from + dir * Math.Max(distance, 0);
    }
}
