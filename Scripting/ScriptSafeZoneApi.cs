using ECommons.DalamudServices;
using MasterPlayer.Dodge;
using MasterPlayer.Settings;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Scripting;

/// <summary>
/// SafeZone 注册门面：包装 PR SafeZoneRegistry（相对秒数语义，仅主线程）。
/// 本脚本注册的全部句柄都会被追踪，脚本停止时由 Cleanup 统一移除。
/// 不提供 SetArenaBoundary（场地边界由本体/其他系统管理）。
/// 危险区注册统一按 DodgeDangerMargin 外扩（框架级余量，DangerMarginInflator）；
/// 脚本自身再加的余量（如斗兽棋脚本的 +0.35）会与之叠加。SafeArea / Obstacle 不外扩。
/// </summary>
public sealed class ScriptSafeZoneApi
{
    private readonly List<SafeZoneAreaHandle> _handles = new();

    internal ScriptSafeZoneApi()
    {
    }

    private static SafeZoneRegistry Registry => PromeRotation.Plugin.Instance.SafeZoneRegistry;

    // ── 危险区 ─────────────────────────────────────────

    /// <summary>延迟 delaySeconds 秒后判定、判定持续 hitDurationSeconds 秒的单次命中危险区（典型：AOE 读条结束那一下）。</summary>
    public SafeZoneAreaHandle AddDelayedHit(
        GeometryTemplate template,
        TransformBinding binding,
        float delaySeconds,
        float hitDurationSeconds = SafeZoneRegistry.DefaultHitDurationSeconds)
        => Track(Registry.AddDelayedHit(WithDangerMargin(template), binding, delaySeconds, hitDurationSeconds));

    /// <summary>从现在开始、持续 durationSeconds 秒的危险区。</summary>
    public SafeZoneAreaHandle AddTimedDanger(GeometryTemplate template, TransformBinding binding, float durationSeconds)
        => Track(Registry.AddTimedDanger(WithDangerMargin(template), binding, durationSeconds));

    /// <summary>从现在开始、永不结束的持续危险区。</summary>
    public SafeZoneAreaHandle AddPersistentDanger(GeometryTemplate template, TransformBinding binding)
        => Track(Registry.AddPersistentDanger(WithDangerMargin(template), binding));

    // ── 目标区（安全点）─────────────────────────────────

    /// <summary>从现在开始、永不结束的持续目标区。</summary>
    public SafeZoneAreaHandle AddPersistentSafeArea(
        GeometryTemplate template,
        TransformBinding binding,
        int priority = 0,
        PositionPreference preference = PositionPreference.Center)
        => Track(Registry.AddPersistentSafeArea(template, binding, priority, preference));

    /// <summary>从现在开始、持续 durationSeconds 秒的目标区。</summary>
    public SafeZoneAreaHandle AddTimedSafeArea(
        GeometryTemplate template,
        TransformBinding binding,
        float durationSeconds,
        int priority = 0,
        PositionPreference preference = PositionPreference.Center)
        => Track(Registry.AddTimedSafeArea(template, binding, durationSeconds, priority, preference));

    // ── 障碍 ───────────────────────────────────────────

    /// <summary>注册一个硬障碍（无时间窗口，Solver 路径全程不得穿越）。</summary>
    public SafeZoneAreaHandle AddObstacle(GeometryTemplate template, TransformBinding binding)
        => Track(Registry.AddObstacle(template, binding));

    // ── 移除 / 清理 ────────────────────────────────────

    /// <summary>按句柄移除本脚本注册的区域。返回是否实际移除。</summary>
    public bool Remove(SafeZoneAreaHandle handle)
    {
        _handles.Remove(handle);
        return Registry.Remove(handle);
    }

    internal void Cleanup()
    {
        foreach (var handle in _handles)
        {
            try
            {
                Registry.Remove(handle);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[MasterPlayer] 脚本安全区句柄移除失败：{ex.Message}");
            }
        }

        _handles.Clear();
    }

    private SafeZoneAreaHandle Track(SafeZoneAreaHandle handle)
    {
        if (handle.IsValid)
            _handles.Add(handle);
        return handle;
    }

    private static GeometryTemplate WithDangerMargin(GeometryTemplate template)
        => DangerMarginInflator.Inflate(template, MasterPlayerSettings.Instance.DodgeDangerMargin);
}
