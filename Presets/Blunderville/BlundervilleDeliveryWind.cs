using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// North delivery wind moves the player after its effect packet. The two
// west-side barriers stop that displacement; open floor does not.
internal static class BlundervilleDeliveryWind
{
    internal const double First = 3.664, Period = 5.6157, LaunchFrom = .55, LaunchUntil = 1.45;
    internal readonly record struct Shelter(float MinX, float MaxX, float MinZ, float WallZ);
    internal static readonly Shelter[] Shelters =
    [
        new(-204.35f, -202.15f, 229.4f, 230.45f),
        new(-205.25f, -202.9f, 223.65f, 224.7f),
    ];

    internal static bool Protected(Vector3 p)
        => Shelters.Any(s => p.X >= s.MinX && p.X <= s.MaxX && p.Z >= s.MinZ && p.Z <= s.WallZ);

    // Rectangles leave the confirmed barrier pockets open. They are planning
    // constraints only; the normal wind telegraph still covers the whole lane.
    internal static readonly RectangleGeometry[] Exposed = Build();
    private static RectangleGeometry[] Build()
    {
        float[] xs = [-206, .. Shelters.SelectMany(s => new[] { s.MinX, s.MaxX }).Order(), -194];
        float[] zs = [212, .. Shelters.SelectMany(s => new[] { s.MinZ, s.WallZ }).Order(), 243];
        var result = new List<RectangleGeometry>();
        for (var x = 1; x < xs.Length; ++x)
            for (var z = 1; z < zs.Length; ++z)
            {
                var center = new Vector3((xs[x - 1] + xs[x]) / 2, 6, (zs[z - 1] + zs[z]) / 2);
                if (!Protected(center)) result.Add(new(center, 0, xs[x] - xs[x - 1], zs[z] - zs[z - 1]));
            }
        return result.ToArray();
    }
}
