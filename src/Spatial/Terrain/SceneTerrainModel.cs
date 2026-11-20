using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class SceneTerrainModel(ITerrainSceneSource source)
{
    private sealed record Completion(int Generation, TerrainSceneCapture? Capture, Exception? Error);
    private readonly ConcurrentQueue<Completion> completed = new();
    private readonly TerrainNavigationCache navigation = new();
    private CancellationTokenSource? cancellation;
    private TerrainSceneSnapshot? previousScene;
    private TerrainCaptureDiagnostics? pendingCaptureDiagnostics;
    private Vector3 anchor;
    private float range;
    private int generation, stage;
    private bool building;
    private bool stagingWindow;
    private TerrainSceneCapture? deferredCapture;
    private TerrainSceneCapture? publishedAudit;
    private DateTime nextCapture;
    private DateTime nextPublishedCheck;
    internal LocalTerrainBoundary? Current { get; private set; }
    internal LocalTerrainBoundary? Display { get; private set; }
    internal string StatusText { get; private set; } = "等待场景碰撞数据";
    internal double LastUpdateMilliseconds { get; private set; }
    internal double BuildMilliseconds { get; private set; }
    internal int TriangleCount { get; private set; }
    internal bool Ready => stage == 2 && !building;
    internal TerrainCaptureDiagnostics? CaptureDiagnostics => source.Diagnostics;
    internal TerrainSceneCapture? LatestCapture { get; private set; }

    internal void Update(Vector3 origin, float radius, DateTime now, bool deferPublication = false)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            radius = float.IsFinite(radius) ? Math.Clamp(radius, 8, 30) : 20;
            var changedLayer = Current != null && Current.TryGetFloorPosition(origin, out var floor) && MathF.Abs(origin.Y - floor.Y) > 1;
            if (changedLayer) { Display = null; navigation.Clear(); }
            if (Current == null || range != radius || changedLayer
                || new Vector2(origin.X - anchor.X, origin.Z - anchor.Z).LengthSquared() > 16)
                Restart(origin, radius, !changedLayer && range == radius && Current?.Contains(origin) == true);
            // A staged window is not the map currently guiding the character.
            // Continue checking the published window, including geometry outside
            // the replacement's capture radius, while movement retains it.
            if ((stagingWindow || deferredCapture != null || deferPublication && building) && publishedAudit is { } published
                && Current is { WalkableCount: > 0 } && now >= nextPublishedCheck)
            {
                if (!source.TryCapture(published.Origin, TerrainNavigationCache.CaptureRadius(published.Radius), out var live) || live == null)
                {
                    StatusText = "正在复查使用中的地形，保留已确认通路";
                    return;
                }
                nextPublishedCheck = now.AddMilliseconds(500);
                if (!live.SameGeometry(published.Scene))
                {
                    InvalidateGeometry(origin, radius, published.Scene, live);
                    publishedAudit = published with { Scene = live };
                }
            }
            while (completed.TryDequeue(out var completion))
            {
                if (completion.Generation != generation) continue;
                building = false;
                cancellation?.Dispose(); cancellation = null;
                if (completion.Error != null) throw new InvalidOperationException($"场景地形后台建模失败：{completion.Error.Message}", completion.Error);
                var capture = completion.Capture!;
                BuildMilliseconds = capture.Result.Milliseconds;
                TriangleCount = capture.Result.Triangles;
                stage = stage == 0 ? 1 : 2;
                if ((stagingWindow || deferPublication || deferredCapture != null)
                    && Current is { WalkableCount: > 0 } && Current.Contains(origin))
                    deferredCapture = capture;
                else
                    Publish(capture);
                nextCapture = stage == 2 ? now.AddMilliseconds(500) : default;
            }
            if (stage == 2 && deferredCapture is { } ready)
            {
                if (deferPublication && Current?.Contains(origin) == true)
                {
                    StatusText = "新地形已就绪，躲避到达安全点后替换";
                    return;
                }
                // The candidate may have waited through a door change outside
                // the old window. Never publish its stale collision snapshot.
                if (!source.TryCapture(ready.Origin, TerrainNavigationCache.CaptureRadius(ready.Radius), out var live) || live == null)
                {
                    StatusText = "正在确认待发布地形的碰撞状态";
                    return;
                }
                if (!live.SameGeometry(ready.Scene)) InvalidateGeometry(origin, radius, ready.Scene, live);
                else
                {
                    Publish(ready);
                    nextCapture = now.AddMilliseconds(500);
                    DescribeReady();
                    return;
                }
            }
            if (building)
            {
                StatusText = stage == 0 ? "后台构建近身地形" : "近身地形已发布，后台构建周边地形";
                return;
            }
            if (now < nextCapture) { DescribeReady(); return; }
            var captureRange = stage == 0 ? Math.Min(8, radius) : radius;
            // Compare the same spatial window until movement restarts its anchor.
            // Following every player step changes the selected collider subset and
            // falsely invalidates otherwise unchanged, traversable terrain.
            var captureOrigin = anchor;
            var captured = source.TryCapture(captureOrigin, TerrainNavigationCache.CaptureRadius(captureRange), out var snapshot);
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
                InvalidateGeometry(captureOrigin, radius, previousScene!, snapshot);
                if (publishedAudit is { } audit && audit.Origin == captureOrigin && audit.Radius == radius)
                    publishedAudit = audit with { Scene = snapshot };
                // Unaffected tiles are already usable. Rebuild only the dirty portion of the full window.
                stage = 1;
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
                    var result = navigation.Build(snapshot, captureOrigin, captureRange, token);
                    completed.Enqueue(new(currentGeneration, new(snapshot, captureOrigin, captureRange, now, captureDiagnostics, result), null));
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
            ? $"场景地形已就绪：{Current.WalkableCount} 个通行单元，同层缓存 {navigation.Count} 块，后台构建 {BuildMilliseconds:F1}ms"
            : "场景模型未找到可通行地面，暂停无场地移动";
    }

    private void Publish(TerrainSceneCapture capture)
    {
        LatestCapture = capture;
        publishedAudit = capture;
        Current = capture.Result.Boundary;
        deferredCapture = null;
        stagingWindow = false;
        // Retain the last complete radar view through intermediate near maps.
        if (Display == null || stage == 2) Display = Current;
    }

    private void Restart(Vector3 origin, float radius, bool preserve)
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        ++generation; building = false; stage = 0;
        anchor = origin; range = radius; previousScene = null; nextCapture = default;
        pendingCaptureDiagnostics = null;
        deferredCapture = null;
        stagingWindow = preserve;
        nextPublishedCheck = default;
        if (!preserve)
        {
            LatestCapture = null;
            publishedAudit = null;
            Current = new(origin, 1, [TerrainCellKind.Unknown], [float.NaN], radius * 2);
        }
    }

    private void InvalidateGeometry(Vector3 origin, float radius, TerrainSceneSnapshot previous, TerrainSceneSnapshot current)
    {
        var regions = navigation.Invalidate(previous, current);
        var retained = Current?.WithUnknownRegions(regions);
        Restart(origin, radius, true);
        Current = retained;
        // A partially invalidated map has no reproducible scene archive until the new snapshot is published.
        LatestCapture = null;
    }

    internal void Invalidate(Vector3 origin, float radius)
    {
        navigation.Clear();
        Restart(origin, Math.Clamp(radius, 8, 30), false);
    }

    internal void Clear()
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        ++generation; building = false; stage = 0; previousScene = null; Current = Display = null;
        navigation.Clear();
        nextCapture = default;
        nextPublishedCheck = default;
        deferredCapture = null;
        stagingWindow = false;
        LatestCapture = null;
        publishedAudit = null;
        pendingCaptureDiagnostics = null;
        while (completed.TryDequeue(out _)) { }
        StatusText = "等待场景碰撞数据";
        LastUpdateMilliseconds = BuildMilliseconds = 0; TriangleCount = 0;
    }
}
