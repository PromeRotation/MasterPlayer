using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Scripting.Visual;

internal sealed record RotationOffsetTemplate(GeometryTemplate Inner, float OffsetRadians) : GeometryTemplate
{
    public override GeometryKind Kind => Inner.Kind;

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => Inner.Resolve(position, rotation + OffsetRadians, lengthOverride);
}
