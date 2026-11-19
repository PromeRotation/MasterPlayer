namespace MasterPlayer.Spatial.Terrain;

internal sealed record TerrainCaptureDiagnostics
{
    public bool Complete { get; init; }
    public int Attempts { get; init; } = 1;
    public double TotalMilliseconds { get; init; }
    public double LockMilliseconds { get; init; }
    public double CopyMilliseconds { get; init; }
    public double HashMilliseconds { get; init; }
    public int VisitedColliders { get; init; }
    public int Instances { get; init; }
    public int CacheHits { get; init; }
    public int CopiedModels { get; init; }
    public int HashedModels { get; init; }
    public long CopiedBytes { get; init; }
    public long CachedBytes { get; init; }

    internal TerrainCaptureDiagnostics Accumulate(TerrainCaptureDiagnostics previous) => this with
    {
        Attempts = previous.Attempts + Attempts,
        TotalMilliseconds = previous.TotalMilliseconds + TotalMilliseconds,
        LockMilliseconds = previous.LockMilliseconds + LockMilliseconds,
        CopyMilliseconds = previous.CopyMilliseconds + CopyMilliseconds,
        HashMilliseconds = previous.HashMilliseconds + HashMilliseconds,
        VisitedColliders = previous.VisitedColliders + VisitedColliders,
        CacheHits = previous.CacheHits + CacheHits,
        CopiedModels = previous.CopiedModels + CopiedModels,
        HashedModels = previous.HashedModels + HashedModels,
        CopiedBytes = previous.CopiedBytes + CopiedBytes,
    };
}
