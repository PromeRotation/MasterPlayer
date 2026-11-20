using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class LocalTerrainController(SafeZoneManager manager, ITerrainProbe probe, ITerrainSceneSource? sceneSource = null)
{
    private readonly LocalTerrainScanner scanner = new(probe);
    private readonly SceneTerrainModel? sceneModel = sceneSource == null ? null : new(sceneSource);
    private readonly TerrainCollisionCorrections corrections = new();
    private readonly TerrainSceneExporter exporter = new();
    private TerrainObstruction? pendingObstruction;
    private uint territory;
    private (nint Director, byte Floor, byte Layout) sceneIdentity;
    private DateTime retryAt;
    private Vector3? groundPosition;
    private bool airborne;
    private bool awaitingGround;
    internal LocalTerrainBoundary? Current { get; private set; }
    internal LocalTerrainBoundary? Display { get; private set; }
    internal float ViewRadius { get; private set; } = 20;
    internal string StatusText { get; private set; } = "未启用";
    internal string? LastError { get; private set; }
    internal double LastScanMilliseconds => sceneModel?.LastUpdateMilliseconds ?? scanner.LastScanMilliseconds;
    internal TerrainCaptureDiagnostics? CaptureDiagnostics => sceneModel?.CaptureDiagnostics;
    internal TerrainSceneCapture? LatestCapture => sceneModel?.LatestCapture;
    internal bool Exporting => exporter.IsRunning;
    internal string ExportStatus => exporter.Status;

    internal bool ExportScene(string directory)
    {
        if (LatestCapture is not { } capture || Current == null) return false;
        return exporter.Start(directory, new(capture, territory, sceneIdentity.Floor, sceneIdentity.Layout,
            Current, corrections.Snapshot()));
    }

    internal void Update(Vector3 player, uint currentTerritory, bool enabled, float radius, DateTime now, bool airborne = false,
        (nint Director, byte Floor, byte Layout) scene = default, bool deferPublication = false)
    {
        radius = float.IsFinite(radius) ? Math.Clamp(radius, 8, 30) : 20;
        ViewRadius = radius;
        if (!enabled || manager.HasRegisteredArenaBoundary)
        {
            Clear();
            StatusText = manager.HasRegisteredArenaBoundary ? "使用已注册的机制场地" : "未启用";
            return;
        }
        if (territory != currentTerritory || sceneIdentity != scene) Clear();
        territory = currentTerritory;
        sceneIdentity = scene;
        this.airborne = airborne;
        if (now < retryAt) return;
        try
        {
            if (!probe.IsAvailable && Current is { WalkableCount: > 0 })
            {
                scanner.Clear(); sceneModel?.Clear(); Current = null;
            }
            if (probe.IsAvailable && airborne)
            {
                if (Current == null)
                {
                    if (sceneModel != null) sceneModel.Invalidate(player, radius);
                    else scanner.Reset(player, radius);
                    Current = sceneModel?.Current ?? scanner.Current;
                }
                awaitingGround = Current is { WalkableCount: 0 };
                Publish();
                StatusText = "跳跃中，保留已采样地形，等待落地后继续扫描";
                return;
            }
            var origin = player;
            if (probe.IsAvailable && TryLocateGround(player, out var floor))
            {
                origin = floor;
                groundPosition = floor;
                if (awaitingGround) { scanner.Clear(); sceneModel?.Clear(); Current = null; awaitingGround = false; }
            }
            else if (probe.IsAvailable && Current is { WalkableCount: > 0 })
            {
                StatusText = "当前离地，保留已采样地形，等待确认支撑地面";
                return;
            }
            if (sceneModel != null)
            {
                if (probe.IsAvailable)
                {
                    sceneModel.Update(origin, radius, now, deferPublication);
                    corrections.Refresh(probe, origin, radius, now);
                }
                else sceneModel.Invalidate(origin, radius);
                Current = corrections.Apply(sceneModel.Current);
            }
            else
            {
                scanner.Update(origin, radius, now);
                Current = scanner.Current;
            }
            Publish();
            LastError = null;
            if (!probe.IsAvailable)
                StatusText = "地形碰撞接口尚未就绪，暂停无场地移动";
            else if (sceneModel != null)
                StatusText = sceneModel.StatusText + (corrections.Count > 0 ? $"；实时碰撞修正 {corrections.Count} 处" : "");
            else if (scanner.Complete && Current is { WalkableCount: 0 })
                StatusText = $"未找到可通行地面，暂停无场地移动：{scanner.SampledCount} 个已检测格";
            else
                StatusText = $"{(scanner.Complete ? "已就绪" : "扫描中")}：{Current?.WalkableCount ?? 0} 个可通行格，累计采样 {scanner.SampledCount} 次";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            scanner.Reset(groundPosition ?? player, radius);
            sceneModel?.Invalidate(groundPosition ?? player, radius);
            Current = sceneModel?.Current ?? scanner.Current;
            Publish();
            retryAt = now.AddSeconds(2);
            StatusText = $"地形扫描失败，暂停无场地移动：{LastError}";
        }
    }

    private void Publish()
    {
        manager.SetFallbackBoundary(Current);
        if (sceneModel == null) Display = Current;
        else if (sceneModel.Display == null) Display = null;
        else if (ReferenceEquals(sceneModel.Display, sceneModel.Current)) Display = Current;
    }

    internal bool ValidateStep(Vector3 player, Vector3 destination)
    {
        pendingObstruction = null;
        if (Current == null) return true;
        if (LastError != null) return false;
        try
        {
            if (!probe.IsAvailable) return false;
            if (!TryLocateGround(player, out var floor)) return false;
            var delta = destination - player;
            delta.Y = 0;
            var distance = new Vector2(delta.X, delta.Z).Length();
            var end = floor + delta;
            if (distance > 2) end = floor + delta * (2 / distance);
            return probe.CanTraverse(floor, end, out pendingObstruction);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private bool TryLocateGround(Vector3 player, out Vector3 floor)
    {
        if (!airborne && probe.TryGetGround(player, out floor)) return true;
        if (Current != null && Current.TryGetFloorPosition(player, out var reference)
            && probe.TryGetGround(reference, out floor)) return true;
        floor = default;
        return false;
    }

    internal void RejectRoute(Vector3 player, float radius, DateTime now)
    {
        if (sceneModel != null && pendingObstruction is { } obstruction)
        {
            corrections.Record(obstruction, now);
            pendingObstruction = null;
            Current = corrections.Apply(sceneModel.Current);
            Publish();
            StatusText = $"实时碰撞已修正地形 {corrections.Count} 处，重新规划路线";
            return;
        }
        scanner.Reset(groundPosition ?? player, radius);
        sceneModel?.Invalidate(groundPosition ?? player, radius);
        Current = sceneModel?.Current ?? scanner.Current;
        Publish();
        StatusText = LastError == null ? "前方地形变化，已停止旧路线并重新扫描" : $"地形复查失败，已停止旧路线：{LastError}";
    }

    internal void Clear()
    {
        scanner.Clear();
        sceneModel?.Clear();
        corrections.Clear();
        pendingObstruction = null;
        Current = Display = null;
        manager.SetFallbackBoundary(null);
        territory = 0;
        sceneIdentity = default;
        retryAt = default;
        groundPosition = null;
        airborne = awaitingGround = false;
        LastError = null;
        StatusText = "未启用";
    }
}
