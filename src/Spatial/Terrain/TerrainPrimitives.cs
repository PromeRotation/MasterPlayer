using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal static class TerrainPrimitives
{
    internal static readonly TerrainMeshAsset Box = new("<box>", BuildBox());
    internal static readonly TerrainMeshAsset Cylinder = new("<cylinder>", BuildRound(false));
    internal static readonly TerrainMeshAsset Sphere = new("<sphere>", BuildRound(true));
    internal static readonly TerrainMeshAsset Plane = new("<plane>", new TerrainMesh(
        [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
        [new(0, 1, 2, 0), new(0, 2, 3, 0)]));
    internal static readonly TerrainMeshAsset TwoSidedPlane = new("<plane-two-sided>", new TerrainMesh(
        Plane.Mesh.Value.Vertices, [new(0, 1, 2, 0), new(0, 2, 3, 0), new(2, 1, 0, 0), new(3, 2, 0, 0)]));

    private static TerrainMesh BuildBox()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<TerrainMesh.Triangle>();
        foreach (var normal in (ReadOnlySpan<Vector3>)[Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ])
        {
            var u = MathF.Abs(normal.Y) > .5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(normal, u);
            var first = vertices.Count;
            vertices.AddRange([normal - u - v, normal + u - v, normal + u + v, normal - u + v]);
            triangles.Add(new(first, first + 1, first + 2, 0));
            triangles.Add(new(first, first + 2, first + 3, 0));
        }
        return new(vertices.ToArray(), triangles.ToArray());
    }

    private static TerrainMesh BuildRound(bool sphere)
    {
        const int SEGMENTS = 32;
        var bands = sphere ? 16 : 1;
        var vertices = new List<Vector3>();
        var triangles = new List<TerrainMesh.Triangle>();
        var expansion = 1 / MathF.Cos(MathF.PI / SEGMENTS);
        if (sphere) expansion *= expansion;
        for (var band = 0; band <= bands; ++band)
        {
            var angle = -MathF.PI / 2 + MathF.PI * band / bands;
            var y = sphere ? MathF.Sin(angle) * expansion : band * 2 - 1;
            var radius = sphere ? MathF.Cos(angle) * expansion : expansion;
            for (var i = 0; i < SEGMENTS; ++i)
                vertices.Add(new(MathF.Cos(i * MathF.Tau / SEGMENTS) * radius, y, MathF.Sin(i * MathF.Tau / SEGMENTS) * radius));
        }
        for (var band = 0; band < bands; ++band)
            for (var i = 0; i < SEGMENTS; ++i)
            {
                var a = band * SEGMENTS + i;
                var b = band * SEGMENTS + (i + 1) % SEGMENTS;
                triangles.Add(new(a, a + SEGMENTS, b, 0));
                triangles.Add(new(b, a + SEGMENTS, b + SEGMENTS, 0));
            }
        if (!sphere)
        {
            var bottom = vertices.Count;
            vertices.Add(-Vector3.UnitY); vertices.Add(Vector3.UnitY);
            for (var i = 0; i < SEGMENTS; ++i)
            {
                var next = (i + 1) % SEGMENTS;
                triangles.Add(new(bottom, i, next, 0));
                triangles.Add(new(bottom + 1, next + SEGMENTS, i + SEGMENTS, 0));
            }
        }
        return new(vertices.ToArray(), triangles.ToArray());
    }
}
