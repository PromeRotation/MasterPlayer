using System.Collections.Concurrent;
using System.Numerics;
using MasterPlayer.Spatial.Terrain;

namespace MasterPlayer.Drawing;

/// <summary>Framework 采集、后台解码；绘制帧只读取已发布的托管地表。</summary>
internal sealed class GroundSceneController(ITerrainSceneSource source) : IDisposable
{
    private sealed record Completion(int Generation, GroundSurface? Surface, Exception? Error);
    private const float CAPTURE_RADIUS = 40f;
    private readonly ConcurrentQueue<Completion> completed = new();
    private CancellationTokenSource? cancellation;
    private TerrainSceneSnapshot? previous;
    private TerrainSceneSnapshot? publishedScene;
    private Vector3 anchor;
    private (ushort Territory, nint Director, byte Floor, byte Layout)? identity;
    private int generation;
    private bool building;
    private bool disposed;
    private DateTime nextCapture;
    internal GroundSurface? Current { get; private set; }
    internal string Status { get; private set; } = "等待贴地范围";

    internal void Update(Vector3? position, ushort territory, (nint Director, byte Floor, byte Layout) layout, DateTime now)
    {
        if (disposed) return;
        if (position is not { } origin)
        {
            Clear();
            return;
        }
        var key = (territory, layout.Director, layout.Floor, layout.Layout);
        if (identity != key || MathF.Abs(origin.Y - anchor.Y) > 3
            || Vector2.DistanceSquared(new(origin.X, origin.Z), new(anchor.X, anchor.Z)) > 16)
        {
            var retain = identity == key && Current != null && MathF.Abs(origin.Y - anchor.Y) <= 3
                && MathF.Abs(origin.X - Current.Origin.X) <= Current.Radius
                && MathF.Abs(origin.Z - Current.Origin.Z) <= Current.Radius;
            Reset();
            if (!retain)
            {
                Current = null;
                publishedScene = null;
            }
            identity = key;
            anchor = origin;
        }
        while (completed.TryDequeue(out var completion))
        {
            if (completion.Generation != generation) continue;
            building = false;
            cancellation?.Dispose();
            cancellation = null;
            if (completion.Error != null)
            {
                previous = null;
                Current = null;
                publishedScene = null;
                Status = "贴地构建失败：" + completion.Error.Message;
                nextCapture = now.AddSeconds(2);
                return;
            }
            Current = completion.Surface;
            publishedScene = previous;
            Status = $"贴地地表就绪：{Current!.Count} 个三角面，采集半径 {CAPTURE_RADIUS:F0} 码";
        }
        if (building || now < nextCapture) return;
        try
        {
            if (!source.TryCapture(anchor, CAPTURE_RADIUS, out var scene) || scene == null)
            {
                Status = "正在采集贴地地表";
                return;
            }
            nextCapture = now.AddMilliseconds(500);
            if (previous != null && previous.SameGeometry(scene)) return;
            // 活动平台/地形改变后停止使用旧面，不等待后台任务完成才失效。
            if (Current == null || publishedScene == null || HasOverlappingChanges(publishedScene, scene, Current, anchor))
            {
                Current = null;
                publishedScene = null;
            }
            previous = scene;
            var capturedOrigin = anchor;
            var capturedGeneration = generation;
            cancellation = new();
            var token = cancellation.Token;
            building = true;
            Status = "正在后台构建贴地地表";
            _ = Task.Run(() =>
            {
                try
                {
                    var surface = GroundSurface.Build(scene, capturedOrigin, CAPTURE_RADIUS, token);
                    completed.Enqueue(new(capturedGeneration, surface, null));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 换区、换层或关闭绘制已撤销该代结果。
                }
                catch (Exception ex)
                {
                    completed.Enqueue(new(capturedGeneration, null, ex));
                }
            });
        }
        catch (Exception ex)
        {
            Reset();
            Current = null;
            publishedScene = null;
            nextCapture = now.AddSeconds(2);
            Status = "贴地采集失败：" + ex.Message;
        }
    }

    private static bool HasOverlappingChanges(TerrainSceneSnapshot oldScene, TerrainSceneSnapshot newScene,
        GroundSurface surface, Vector3 newOrigin)
    {
        bool IntersectsWindow(TerrainSceneSnapshot.Instance instance)
        {
            // 原生采集自带世界包围盒；未知时保守失效，不在 Framework 中解码模型。
            if (instance.GetBounds(false) is not { } bounds) return true;
            var minX = MathF.Max(surface.Origin.X - surface.Radius, newOrigin.X - CAPTURE_RADIUS);
            var maxX = MathF.Min(surface.Origin.X + surface.Radius, newOrigin.X + CAPTURE_RADIUS);
            var minZ = MathF.Max(surface.Origin.Z - surface.Radius, newOrigin.Z - CAPTURE_RADIUS);
            var maxZ = MathF.Min(surface.Origin.Z + surface.Radius, newOrigin.Z + CAPTURE_RADIUS);
            return bounds.Min.X <= maxX && bounds.Max.X >= minX && bounds.Min.Z <= maxZ && bounds.Max.Z >= minZ;
        }
        var oldById = oldScene.Instances.ToLookup(instance => instance.Id);
        var newById = newScene.Instances.ToLookup(instance => instance.Id);
        return oldScene.Instances.Any(instance => !newById[instance.Id].Any(instance.SameGeometry) && IntersectsWindow(instance))
            || newScene.Instances.Any(instance => !oldById[instance.Id].Any(instance.SameGeometry) && IntersectsWindow(instance));
    }

    private void Reset()
    {
        if (source is NativeTerrainSceneSource native) native.ResetCapture();
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        ++generation;
        previous = null;
        building = false;
        nextCapture = default;
    }

    internal void Clear()
    {
        if (identity == null && !building && Current == null) return;
        Reset();
        identity = null;
        Current = null;
        publishedScene = null;
        while (completed.TryDequeue(out _)) { }
        Status = "等待贴地范围";
    }

    public void Dispose()
    {
        if (disposed) return;
        Clear();
        disposed = true;
    }
}
