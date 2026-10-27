using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

public sealed class BlundervilleRouteLibrary
{
    public int Version { get; set; } = 1;
    public bool UsePresets { get; set; }
    public List<BlundervilleRoutePreset> Routes { get; set; } = [];

    public BlundervilleRouteLibrary Copy() => new()
    {
        Version = Version, UsePresets = UsePresets,
        Routes = (Routes ?? []).Where(r => r != null).Take(BlundervillePresetGeometry.MaxRoutes)
            .Select(r => r.Copy()).ToList()
    };
}

public sealed class BlundervilleRoutePreset
{
    public string Name { get; set; } = "新路线";
    public bool Enabled { get; set; } = true;
    public List<BlundervilleRoutePoint> Points { get; set; } = [];
    public BlundervilleRoutePreset Copy() => new()
    {
        Name = Name, Enabled = Enabled,
        Points = (Points ?? []).Select(p => new BlundervilleRoutePoint { X = p?.X ?? float.NaN, Z = p?.Z ?? float.NaN }).ToList()
    };
}

public sealed class BlundervilleRoutePoint
{
    public float X { get; set; }
    public float Z { get; set; }
    internal Vector3 Position => new(X, BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, Z), Z);
    internal static BlundervilleRoutePoint From(Vector3 p) => new() { X = p.X, Z = p.Z };
}

internal static class BlundervillePresetGeometry
{
    internal const int MaxRoutes = 24, MaxPoints = 128;
    internal const float StartZ = 323, EndZ = BlundervilleFinalPlanner.RunupZ;
    internal static readonly float[] Stops = [275, 260, 236, 184];
    internal static readonly IGeometryShape Floor = BlundervilleRoutes.Boundary(BlundervilleCourse.Final);

    internal static bool Link(Vector3 a, Vector3 b) => ArenaBoundaryTraversal.ContainsTube(Floor, a, b, BlundervilleFinalPlanner.FloorClearance);

    internal static string? Validate(BlundervilleRoutePreset route)
    {
        if (string.IsNullOrWhiteSpace(route.Name) || route.Name.Length > 60) return "路线名须为 1–60 个字符。";
        if (route.Points is not { Count: >= 2 and <= MaxPoints }) return $"路线须有 2–{MaxPoints} 个路点。";
        var points = route.Points;
        foreach (var p in points)
            if (p == null || !float.IsFinite(p.X) || !float.IsFinite(p.Z) || MathF.Abs(p.X) > 18 || p.Z < EndZ - .01f || p.Z > StartZ + .01f)
                return "路点超出决赛坡道范围。";
        if (MathF.Abs(points[0].Z - StartZ) > .01f) return "首点须在起跑线 Z=323（点击起跑线附近会吸附）。";
        var end = points[^1];
        if (MathF.Abs(end.Z - EndZ) > .01f || MathF.Abs(MathF.Abs(end.X) - BlundervilleRoutes.FinalStairLane) > .01f)
            return "末点须接左／右台阶入口（X=±4，Z=155.2）。";
        for (var i = 1; i < points.Count; ++i)
        {
            var a = points[i - 1].Position; var b = points[i].Position;
            if (b.Z > a.Z + .01f || Distance(a, b) < .1f) return $"第 {i + 1} 点须向终点前进或横移，不能回头或重复。";
            if (!Link(a, b)) return $"第 {i} → {i + 1} 点穿过缺口／墙体，或离边缘不足 0.25 米。";
        }
        return null;
    }

    internal static Vector3 Snap(Vector3 p)
    {
        if (MathF.Abs(p.Z - StartZ) < 2) p.Z = StartZ;
        if (MathF.Abs(p.Z - EndZ) < 2) { p.Z = EndZ; p.X = p.X < 0 ? -4 : 4; }
        p.X = MathF.Round(p.X * 10) / 10; p.Z = MathF.Round(p.Z * 10) / 10;
        return p;
    }

    internal static BlundervilleRoutePreset Reference(int side) => new()
    {
        Name = side < 0 ? "左侧参考线" : "右侧参考线",
        Points = new[] { new Vector3(0, 0, StartZ) }
            .Concat(BlundervilleRoutes.Nodes(BlundervilleCourse.Final, new(side, 0, StartZ))
                .Where(n => n.Position.Z > EndZ).Select(n => n.Position))
            .Append(new(side * 4, 0, EndZ)).Select(BlundervilleRoutePoint.From).ToList()
    };

    internal static Vector3[] Compile(BlundervilleRoutePreset route)
    {
        var result = new List<Vector3> { route.Points[0].Position };
        foreach (var point in route.Points.Skip(1))
        {
            var a = result[^1]; var b = point.Position;
            foreach (var z in Stops.Where(z => z < a.Z && z > b.Z))
                result.Add(Vector3.Lerp(a, b, (a.Z - z) / (a.Z - b.Z)));
            result.Add(b);
        }
        return result.ToArray();
    }

    internal static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
