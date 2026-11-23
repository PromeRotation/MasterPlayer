using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// Multiple eligible stacks require participating in exactly one, including when their ranges overlap.
internal sealed record DungeonExclusiveJoinTemplate(IGeometryShape Arena, IGeometryShape[] Candidates) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new DungeonExclusiveJoinGeometry(Arena, Candidates);
}
