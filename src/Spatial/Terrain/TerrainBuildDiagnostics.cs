namespace MasterPlayer.Spatial.Terrain;

internal sealed record TerrainBuildDiagnostics
{
    public double TotalMilliseconds { get; init; }
    public double DecodeMilliseconds { get; init; }
    public double TransformMilliseconds { get; init; }
    public double ClassifyMilliseconds { get; init; }
    public double RasterizeMilliseconds { get; init; }
    public double FilterMilliseconds { get; init; }
    public double CompactMilliseconds { get; init; }
    public double ForbiddenMilliseconds { get; init; }
    public double ErodeMilliseconds { get; init; }
    public double ReachabilityMilliseconds { get; init; }
    public double BoundaryMilliseconds { get; init; }
    public int Instances { get; init; }
    public int DecodedModels { get; init; }
    public int InputVertices { get; init; }
    public int InputTriangles { get; init; }
    public int RasterizedTriangles { get; init; }
    public int ForbiddenTriangles { get; init; }
    public int CompactSpans { get; init; }
    public int ReachableSpans { get; init; }
    public int WalkableCells { get; init; }
    public int BuiltTiles { get; init; }
    public int CachedTiles { get; init; }
    public int StoredTiles { get; init; }
}
