using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record QueenEternalKnockbackTemplate(IGeometryShape Polygon, Vector3 Center, Vector3 Origin, float? Direction) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new QueenEternalKnockbackGeometry(this);
    internal Vector3 Landing(Vector3 point)
    {
        if (Direction is { } angle) return point + 20 * new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
        var offset = point - Origin; offset.Y = 0;
        return point + (offset.LengthSquared() > 0 ? Vector3.Normalize(offset) * 10 : Vector3.Zero);
    }
    internal bool Unsafe(Vector3 point)
    {
        var localStart = point - Center + Polygon.Position;
        var localEnd = Landing(point) - Center + Polygon.Position;
        if (!Polygon.Contains(localEnd, 0)) return true;
        return Direction == null && QueenEternalKnockbackGeometry.FirstBoundaryDistance(Polygon, localStart, localEnd) < Vector3.Distance(localStart, localEnd);
    }
}
