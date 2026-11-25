using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonStackBuddyTemplate(IGeometryShape Arena, Vector3[] Buddies, float Radius) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new SohrKhaiWallHintTemplate(Arena, Buddies.Select(p => (IGeometryShape)new CircleGeometry(p, Radius)).ToArray())
            .Resolve(position, rotation, lengthOverride);

    internal float Distance(float x, float z)
    {
        var distance = float.NegativeInfinity;
        foreach (var buddy in Buddies)
        {
            var dx = x - buddy.X;
            var dz = z - buddy.Z;
            distance = MathF.Max(distance, Radius - MathF.Sqrt(dx * dx + dz * dz));
        }
        return distance;
    }
}
