using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hydatos;

internal sealed record HydatosNavigation(string Key, string Component, GeometryTemplate Shape,
    Vector3 Origin, float Rotation, DateTime Activation, DateTime StartedAt)
{
    internal bool SameOutput(HydatosNavigation other)
        => Key == other.Key && Component == other.Component && Origin == other.Origin
            && Rotation == other.Rotation && Activation == other.Activation && StartedAt == other.StartedAt
            && SameShape(Shape, other.Shape);

    private static bool SameShape(GeometryTemplate a, GeometryTemplate b)
        => (a, b) switch
        {
            (DungeonStackBuddyTemplate x, DungeonStackBuddyTemplate y)
                => x.Radius == y.Radius && SameBounds(x.Arena, y.Arena) && x.Buddies.SequenceEqual(y.Buddies),
            (SohrKhaiWallHintTemplate x, SohrKhaiWallHintTemplate y)
                => SameBounds(x.Arena, y.Arena) && SameCircles(x.Shelters, y.Shelters),
            (DungeonExclusiveJoinTemplate x, DungeonExclusiveJoinTemplate y)
                => SameBounds(x.Arena, y.Arena) && SameCircles(x.Candidates, y.Candidates),
            _ => a == b,
        };

    private static bool SameCircles(IGeometryShape[] a, IGeometryShape[] b)
        => a.Length == b.Length && a.Zip(b).All(p => SameBounds(p.First, p.Second));

    private static bool SameBounds(IGeometryShape a, IGeometryShape b)
        => (a, b) switch
        {
            (CircleGeometry x, CircleGeometry y) => x.Position == y.Position && x.Radius == y.Radius,
            (RectangleGeometry x, RectangleGeometry y) => x.Position == y.Position && x.Rotation == y.Rotation
                && x.Width == y.Width && x.Length == y.Length,
            _ => ReferenceEquals(a, b),
        };
}
