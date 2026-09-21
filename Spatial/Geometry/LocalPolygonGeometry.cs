using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

public sealed class LocalPolygonGeometry : GeometryShape
{
    public LocalPolygonGeometry(Vector3 position, float rotation, IEnumerable<Vector2> localPoints)
        : base(position, rotation)
    {
        if (!float.IsFinite(position.X) ||
            !float.IsFinite(position.Y) ||
            !float.IsFinite(position.Z))
        {
            throw new ArgumentException("多边形位置必须是有限值。", nameof(position));
        }

        if (!float.IsFinite(rotation))
        {
            throw new ArgumentException("多边形朝向必须是有限值。", nameof(rotation));
        }

        LocalPoints = PolygonGeometryHelpers.NormalizeAndValidate(localPoints, nameof(localPoints));
    }

    public override GeometryKind Kind => GeometryKind.LocalPolygon;

    public IReadOnlyList<Vector2> LocalPoints { get; }

    public IReadOnlyList<Vector3> WorldPoints => LocalPoints.Select(LocalToWorld).ToList();

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => GeometryHelper.PointInPolygon2D(WorldToLocal(point), LocalPoints, epsilon);

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
        => SingleContour(WorldPoints);
}
