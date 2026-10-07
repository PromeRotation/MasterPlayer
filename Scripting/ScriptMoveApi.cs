using ECommons.DalamudServices;
using MasterPlayer.Spatial;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer.Scripting;

/// <summary>
/// 移动门面：包装 PR GreenMoveSystem（last-write-wins，仅主线程）。
/// 内置移动纪律：
/// - MoveTo / Append / CatmullRomMoveTo 视为本脚本接管移动（_hasControl = true）；
/// - Stop 只在本脚本接管过时才真正下发，绝不停掉本体或其他插件发起的移动；
/// - 插件或宿主安全区开启自动移动或持有控制权时让位，
///   指令不下发，并以 5 秒节流写一条日志提示。
/// </summary>
public sealed class ScriptMoveApi
{
    private static readonly TimeSpan AutoMovementBlockLogInterval = TimeSpan.FromSeconds(5);

    private readonly Action<string>? _log;
    private bool _hasControl;
    private DateTime _lastAutoMovementBlockLogUtc;

    internal ScriptMoveApi(Action<string>? log = null)
    {
        _log = log;
    }

    private static IGreenMoveSystem System => PromeRotation.Plugin.Instance.GreenMoveSystem;
    private static bool SafeZoneHasControl => HostMovement.HasControl ||
        MasterPlayerPlugin.Instance!.SafeZone.MovementStatus.HasControl;

    /// <summary>是否正在执行移动。</summary>
    public bool IsRunning => System.IsRunning;

    /// <summary>获取当前移动状态。</summary>
    public GreenMoveStatus GetStatus() => System.GetStatus();

    /// <summary>替换当前路线并移动到单点。</summary>
    public void MoveTo(GreenMovePoint point)
    {
        if (!TryTakeControl())
            return;
        System.MoveTo(point);
        _hasControl = true;
    }

    /// <summary>替换当前路线并从第一个点开始移动。</summary>
    public void MoveTo(IReadOnlyList<GreenMovePoint> points)
    {
        if (!TryTakeControl())
            return;
        System.MoveTo(points);
        _hasControl = true;
    }

    /// <summary>追加单点到当前路线尾部。</summary>
    public void Append(GreenMovePoint point)
    {
        if (!TryTakeControl())
            return;
        System.Append(point);
        _hasControl = true;
    }

    /// <summary>Catmull-Rom 平滑移动（单点），最终仍走 MoveTo。</summary>
    public void CatmullRomMoveTo(GreenMovePoint point, CatmullRomMoveOptions? options = null)
    {
        if (!TryTakeControl())
            return;
        System.CatmullRomMoveTo(point, options);
        _hasControl = true;
    }

    /// <summary>Catmull-Rom 平滑移动（路线），最终仍走 MoveTo。</summary>
    public void CatmullRomMoveTo(IReadOnlyList<GreenMovePoint> route, CatmullRomMoveOptions? options = null)
    {
        if (!TryTakeControl())
            return;
        System.CatmullRomMoveTo(route, options);
        _hasControl = true;
    }

    /// <summary>停止移动并清空路线。仅本脚本接管过移动时才真正下发。</summary>
    public void Stop()
    {
        if (!_hasControl)
            return;
        _hasControl = false;
        if (!SafeZoneHasControl)
            System.Stop();
    }

    /// <summary>清空后续点，保留当前点。</summary>
    public void ClearQueue()
    {
        if (_hasControl && TryTakeControl())
            System.ClearQueue();
    }

    /// <summary>跳过当前点。</summary>
    public void SkipCurrent()
    {
        if (_hasControl && TryTakeControl())
            System.SkipCurrent();
    }

    internal void Cleanup()
    {
        if (!_hasControl)
            return;
        try
        {
            Stop();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[MasterPlayer] 脚本移动停止失败：{ex.Message}");
        }
    }

    private bool TryTakeControl()
    {
        var safeZoneHasControl = SafeZoneHasControl;
        if (MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true)
            return false;
        if (!HostMovement.AutomaticMovementEnabled && !MasterPlayerPlugin.Instance!.SafeZone.Setting.EffectiveEnableAutoMovement &&
            !MasterPlayerPlugin.Instance.SafeZone.Setting.RuntimeMovementHeld &&
            !safeZoneHasControl)
            return true;

        if (safeZoneHasControl)
            _hasControl = false;
        var now = DateTime.UtcNow;
        if (now - _lastAutoMovementBlockLogUtc >= AutoMovementBlockLogInterval)
        {
            _lastAutoMovementBlockLogUtc = now;
            _log?.Invoke("安全区自动移动已启用或接管，脚本移动指令已让位");
        }

        return false;
    }
}
