using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonCrossTemplate(float Length, float HalfWidth) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.LocalPolygon;

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new LocalPolygonGeometry(position, rotation,
        [
            new(-HalfWidth, -Length), new(HalfWidth, -Length), new(HalfWidth, -HalfWidth),
            new(Length, -HalfWidth), new(Length, HalfWidth), new(HalfWidth, HalfWidth),
            new(HalfWidth, Length), new(-HalfWidth, Length), new(-HalfWidth, HalfWidth),
            new(-Length, HalfWidth), new(-Length, -HalfWidth), new(-HalfWidth, -HalfWidth),
        ]);
}
