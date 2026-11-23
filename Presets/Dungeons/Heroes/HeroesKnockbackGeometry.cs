using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class HeroesKnockbackGeometry(HeroesKnockbackTemplate template) : GeometryShape(template.LandingArena.Position)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => template.Unsafe(point, epsilon);
    public override GeometryBounds GetBounds() => template.LandingArena.GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
