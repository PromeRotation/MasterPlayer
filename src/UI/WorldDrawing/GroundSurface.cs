using System.Numerics;
using MasterPlayer.Spatial.Terrain;

namespace MasterPlayer.Drawing;

/// <summary>绘制专用的原始支撑面，不经过寻路腐蚀，也不修改规划边界。</summary>
internal sealed class GroundSurface
{
    private const float BIN_SIZE = 4f;
    private const float STEP_HEIGHT = 0.65f;
    private readonly GroundTriangle[] triangles;
    private readonly Dictionary<(int X, int Z), int[]> bins;
    internal Vector3 Origin { get; }
    internal float Radius { get; }
    internal int Count => triangles.Length;
    internal GroundTriangle this[int index] => triangles[index];

    private GroundSurface(Vector3 origin, float radius, GroundTriangle[] triangles)
    {
        Origin = origin;
        Radius = radius;
        this.triangles = triangles;
        var index = new Dictionary<(int, int), List<int>>();
        for (var i = 0; i < triangles.Length; ++i)
        {
            var min = triangles[i].Min;
            var max = triangles[i].Max;
            for (var z = Bin(min.Y); z <= Bin(max.Y); ++z)
                for (var x = Bin(min.X); x <= Bin(max.X); ++x)
                {
                    if (!index.TryGetValue((x, z), out var entries)) index[(x, z)] = entries = [];
                    entries.Add(i);
                }
        }
        bins = index.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    internal static GroundSurface Build(TerrainSceneSnapshot scene, Vector3 origin, float radius, CancellationToken cancellation)
    {
        var faces = new List<GroundTriangle>();
        var minimum = new Vector2(origin.X - radius, origin.Z - radius);
        var maximum = new Vector2(origin.X + radius, origin.Z + radius);
        foreach (var instance in scene.Instances)
        {
            cancellation.ThrowIfCancellationRequested();
            var mesh = instance.Asset.Mesh.Value;
            var vertices = mesh.Vertices.Select(vertex => Vector3.Transform(vertex, instance.World)).ToArray();
            var mirrored = instance.World.GetDeterminant() < 0;
            foreach (var triangle in mesh.Triangles)
            {
                var material = TerrainSceneSnapshot.ResolveMaterial(triangle.Material, instance.Material, instance.MaterialMask);
                if (TerrainSceneSnapshot.IsUnwalkableMaterial(material)) continue;
                var a = vertices[triangle.A];
                var b = vertices[triangle.B];
                var c = vertices[triangle.C];
                var normal = Vector3.Cross(b - a, c - a);
                if (mirrored) normal = -normal;
                // 只保留朝上的支撑面；墙、天花板和退化面不承接范围。
                if (!TerrainMesh.Finite(normal) || normal.LengthSquared() < 1e-10f || normal.Y < normal.Length() * 0.35f)
                    continue;
                // 将巨大的场景三角面裁到采集窗口，限制后续空间索引规模。
                var polygon = GroundProjection.ClipRectangle(a, b, c, minimum, maximum);
                for (var i = 1; i + 1 < polygon.Count; ++i)
                {
                    var ab = polygon[i] - polygon[0];
                    var ac = polygon[i + 1] - polygon[0];
                    if (MathF.Abs(ab.X * ac.Z - ab.Z * ac.X) > 1e-8f)
                        faces.Add(new(polygon[0], polygon[i], polygon[i + 1], faces.Count));
                }
            }
        }

        var parents = Enumerable.Range(0, faces.Count).ToArray();
        var contacts = new Dictionary<(int X, int Z), List<(float Y, int Face)>>();
        int Root(int i)
        {
            while (parents[i] != i)
            {
                parents[i] = parents[parents[i]];
                i = parents[i];
            }
            return i;
        }
        for (var i = 0; i < faces.Count; ++i)
        {
            cancellation.ThrowIfCancellationRequested();
            var face = faces[i];
            foreach (var vertex in (ReadOnlySpan<Vector3>)[face.A, face.B, face.C])
            {
                var key = ((int)MathF.Round(vertex.X * 100), (int)MathF.Round(vertex.Z * 100));
                if (!contacts.TryGetValue(key, out var adjacent)) contacts[key] = adjacent = [];
                foreach (var contact in adjacent)
                    if (MathF.Abs(contact.Y - vertex.Y) <= STEP_HEIGHT)
                        parents[Root(i)] = Root(contact.Face);
                adjacent.Add((vertex.Y, i));
            }
        }
        var result = new GroundSurface(origin, radius, faces.ToArray());
        // 不同碰撞模型可能在同一条边上使用不同细分；补齐顶点落在长边内部的 T 接缝。
        for (var i = 0; i < faces.Count; ++i)
        {
            cancellation.ThrowIfCancellationRequested();
            var face = faces[i];
            foreach (var vertex in (ReadOnlySpan<Vector3>)[face.A, face.B, face.C])
            {
                var point = new Vector2(vertex.X, vertex.Z);
                foreach (var other in result.Query(point - new Vector2(0.01f), point + new Vector2(0.01f)))
                {
                    if (Root(i) == Root(other)) continue;
                    var adjacent = faces[other];
                    foreach (var (a, b) in (ReadOnlySpan<(Vector3, Vector3)>)
                        [(adjacent.A, adjacent.B), (adjacent.B, adjacent.C), (adjacent.C, adjacent.A)])
                    {
                        var start = new Vector2(a.X, a.Z);
                        var delta = new Vector2(b.X - a.X, b.Z - a.Z);
                        var length = delta.LengthSquared();
                        if (length < 1e-8f) continue;
                        var t = Vector2.Dot(point - start, delta) / length;
                        if (t < 0 || t > 1 || Vector2.DistanceSquared(point, start + t * delta) > 0.0001f) continue;
                        if (MathF.Abs(vertex.Y - (a.Y + t * (b.Y - a.Y))) <= STEP_HEIGHT)
                            parents[Root(i)] = Root(other);
                    }
                }
            }
        }
        for (var i = 0; i < faces.Count; ++i) result.triangles[i] = faces[i] with { Layer = Root(i) };
        return result;
    }

    internal IEnumerable<int> Query(Vector2 min, Vector2 max)
    {
        min = Vector2.Max(min, new(Origin.X - Radius, Origin.Z - Radius));
        max = Vector2.Min(max, new(Origin.X + Radius, Origin.Z + Radius));
        var seen = new HashSet<int>();
        for (var z = Bin(min.Y); z <= Bin(max.Y); ++z)
            for (var x = Bin(min.X); x <= Bin(max.X); ++x)
                if (bins.TryGetValue((x, z), out var entries))
                    foreach (var i in entries)
                        if (seen.Add(i) && triangles[i].Min.X <= max.X && triangles[i].Max.X >= min.X
                            && triangles[i].Min.Y <= max.Y && triangles[i].Max.Y >= min.Y)
                            yield return i;
    }

    internal int FindSupport(Vector3 anchor)
    {
        var point = new Vector2(anchor.X, anchor.Z);
        var best = -1;
        var distance = 4f;
        if (!bins.TryGetValue((Bin(point.X), Bin(point.Y)), out var candidates)) return -1;
        foreach (var i in candidates)
            if (triangles[i].TryHeight(point, out var height) && MathF.Abs(height - anchor.Y) < distance)
            {
                best = i;
                distance = MathF.Abs(height - anchor.Y);
            }
        return best;
    }

    internal bool IsSelected(int index, Vector3 point, float anchorHeight)
    {
        var face = triangles[index];
        var xz = new Vector2(point.X, point.Z);
        var distance = MathF.Abs(point.Y - anchorHeight);
        if (!bins.TryGetValue((Bin(xz.X), Bin(xz.Y)), out var candidates)) return false;
        foreach (var other in candidates)
        {
            if (other == index || triangles[other].Layer != face.Layer) continue;
            if (triangles[other].TryHeight(xz, out var height) && MathF.Abs(height - anchorHeight) + 0.01f < distance)
                return false;
        }
        return true;
    }

    private static int Bin(float value) => (int)MathF.Floor(value / BIN_SIZE);
}
