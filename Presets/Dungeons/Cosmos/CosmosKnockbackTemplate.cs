using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CosmosKnockbackTemplate(Vector3 Center, float Rotation, IGeometryShape[] Dangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new CosmosKnockbackGeometry(this);
    internal Vector3 Landing(Vector3 point)
    {
        var direction = new Vector3(MathF.Sin(Rotation), 0, MathF.Cos(Rotation)) * 10;
        var fraction = 1f;
        if (direction.X > 0) fraction = Math.Min(fraction, (Center.X + 19.5f - point.X) / direction.X);
        if (direction.X < 0) fraction = Math.Min(fraction, (Center.X - 19.5f - point.X) / direction.X);
        if (direction.Z > 0) fraction = Math.Min(fraction, (Center.Z + 19.5f - point.Z) / direction.Z);
        if (direction.Z < 0) fraction = Math.Min(fraction, (Center.Z - 19.5f - point.Z) / direction.Z);
        return point + direction * Math.Clamp(fraction - .00001f, 0, 1);
    }
}
