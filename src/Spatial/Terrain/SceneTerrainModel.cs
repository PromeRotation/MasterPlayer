using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class SceneTerrainModel(ITerrainSceneSource source)
{
    private sealed record Completion(int Generation, TerrainSceneCapture? Capture, Exception? Error);
    private readonly ConcurrentQueue<Completion> completed = new();
    private CancellationTokenSource? cancellation;
    private TerrainSceneSnapshot? previousScene;
    private TerrainCaptureDiagnostics? pendingCaptureDiagnostics;
    private Vector3 anchor;
    private float range;
    private int generation, stage;
    private bool building;
    private DateTime nextCapture;
    internal LocalTerrainBoundary? Current { get; private set; }
    internal LocalTerrainBoundary? Display { get; private set; }
    internal string StatusText { get; private set; } = "等待场景碰撞数据";
    internal double LastUpdateMilliseconds { get; private set; }
    internal double BuildMilliseconds { get; private set; }
    internal int TriangleCount { get; private set; }
    internal bool Ready => stage == 2 && !building;
    internal TerrainCaptureDiagnostics? CaptureDiagnostics => source.Diagnostics;
    internal TerrainSceneCapture? LatestCapture { get; private set; }

    internal void Update(Vector3 origin, float radius, DateTime now)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            radius = float.IsFinite(radius) ? Math.Clamp(radius, 8, 30) : 20;
            var changedLayer = Current != null && Current.TryGetFloorPosition(origin, out var floor) && MathF.Abs(origin.Y - floor.Y) > 1;
            if (changedLayer) Display = null;
            if (Current == null || range != radius || changedLayer
                || new Vector2(origin.X - anchor.X, origin.Z - anchor.Z).LengthSquared() > 16)
                Restart(origin, radius, !changedLayer && Current?.Contains(origin) == true);
            while (completed.TryDequeue(out var completion))
            {
                if (completion.Generation != generation) continue;
                building = false;
                cancellation?.Dispose(); cancellation = null;
                if (completion.Error != null) throw new InvalidOperationException($"场景地形后台建模失败：{completion.Error.Message}", completion.Error);
                LatestCapture = completion.Capture!;
                Current = LatestCapture.Result.Boundary;
                BuildMilliseconds = LatestCapture.Result.Milliseconds;
                TriangleCount = LatestCapture.Result.Triangles;
                stage = stage == 0 ? 1 : 2;
                // Keep the last complete view while navigation rebuilds its near/full maps.
                if (Display == null || stage == 2) Display = Current;
                nextCapture = stage == 2 ? now.AddMilliseconds(500) : default;
            }
            if (building)
            {
                StatusText = stage == 0 ? "后台构建近身地形" : "近身地形已发布，后台构建周边地形";
                return;
            }
            if (now < nextCapture) { DescribeReady(); return; }
            var captureRange = stage == 0 ? Math.Min(8, radius) : radius;
            var captured = source.TryCapture(origin, captureRange, out var snapshot);
            if (source.Diagnostics is { } attempt)
                pendingCaptureDiagnostics = pendingCaptureDiagnostics == null ? attempt : attempt.Accumulate(pendingCaptureDiagnostics);
            if (!captured || snapshot == null)
            {
                StatusText = "正在提取有效场景碰撞数据，保留已确认地形";
                return;
            }
            var captureDiagnostics = pendingCaptureDiagnostics;
            pendingCaptureDiagnostics = null;
            if (stage == 2 && previousScene != null && snapshot.SameGeometry(previousScene))
            {
                nextCapture = now.AddMilliseconds(500);
                DescribeReady();
                return;
            }
            if (stage == 2)
            {
                Restart(origin, radius, false);
                captureRange = Math.Min(8, radius);
            }
            previousScene = snapshot;
            cancellation = new();
            var token = cancellation.Token;
            var currentGeneration = generation;
            building = true;
            _ = Task.Run(() =>
            {
                try
                {
                    var result = SceneTerrainBuilder.Build(snapshot, origin, captureRange, token);
                    completed.Enqueue(new(currentGeneration, new(snapshot, origin, captureRange, now, captureDiagnostics, result), null));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // A scene/window change owns the replacement task; obsolete results are discarded.
                }
                catch (Exception ex) { completed.Enqueue(new(currentGeneration, null, ex)); }
            });
            StatusText = stage == 0 ? "后台构建近身地形" : "近身地形已发布，后台构建周边地形";
        }
        finally { LastUpdateMilliseconds = clock.Elapsed.TotalMilliseconds; }
    }

    private void DescribeReady()
    {
        StatusText = Current is { WalkableCount: > 0 }
            ? $"场景地形已就绪：{Current.WalkableCount} 个通行单元，{TriangleCount} 个三角面，后台构建 {BuildMilliseconds:F1}ms"
            : "场景模型未找到可通行地面，暂停无场地移动";
    }

    private void Restart(Vector3 origin, float radius, bool preserve)
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        ++generation; building = false; stage = 0;
        anchor = origin; range = radius; previousScene = null; nextCapture = default;
        pendingCaptureDiagnostics = null;
        if (!preserve)
        {
            LatestCapture = null;
            Current = new(origin, 1, [TerrainCellKind.Unknown], [float.NaN], radius * 2);
        }
    }

    internal void Invalidate(Vector3 origin, float radius) => Restart(origin, Math.Clamp(radius, 8, 30), false);

    internal void Clear()
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        ++generation; building = false; stage = 0; previousScene = null; Current = Display = null;
        nextCapture = default;
        LatestCapture = null;
        pendingCaptureDiagnostics = null;
        while (completed.TryDequeue(out _)) { }
        StatusText = "等待场景碰撞数据";
        LastUpdateMilliseconds = BuildMilliseconds = 0; TriangleCount = 0;
    }
}
