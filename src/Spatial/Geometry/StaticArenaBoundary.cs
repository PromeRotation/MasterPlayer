using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

// Implement only for immutable X/Z containment. A changed corridor needs a new instance.
internal interface IStaticArenaBoundary : IGeometryShape
{
    ArenaBoundaryIndex TraversalIndex { get; }
}

internal sealed class ArenaBoundaryIndex
{
    private const float BinSize = 4;
    private readonly Edge[][] _bins;
    private readonly float _minZ;
    internal readonly record struct Edge(Vector2 A, Vector2 B)
    {
        public float MinX => MathF.Min(A.X, B.X);
        public float MaxX => MathF.Max(A.X, B.X);
        public float MinZ => MathF.Min(A.Y, B.Y);
        public float MaxZ => MathF.Max(A.Y, B.Y);
    }

    public ArenaBoundaryIndex(IReadOnlyList<IReadOnlyList<Vector3>> contours)
    {
        var edges = new List<Edge>();
        foreach (var contour in contours)
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = contour[i]; var b = contour[(i + 1) % contour.Count];
                edges.Add(new(new(a.X, a.Z), new(b.X, b.Z)));
            }
        _minZ = edges.Count == 0 ? 0 : edges.Min(e => e.MinZ);
        var maxZ = edges.Count == 0 ? 0 : edges.Max(e => e.MaxZ);
        var bins = Enumerable.Range(0, Math.Clamp((int)MathF.Ceiling((maxZ - _minZ) / BinSize) + 1, 1, 2048))
            .Select(_ => new List<Edge>()).ToArray();
        _bins = new Edge[bins.Length][];
        foreach (var edge in edges)
            for (var i = Bin(edge.MinZ); i <= Bin(edge.MaxZ); ++i) bins[i].Add(edge);
        for (var i = 0; i < bins.Length; ++i) _bins[i] = bins[i].ToArray();
    }

    internal int Bin(float z) => (int)Math.Clamp(MathF.Floor((z - _minZ) / BinSize), 0, _bins.Length - 1);
    internal ReadOnlySpan<Edge> Edges(int bin) => _bins[bin];
}
