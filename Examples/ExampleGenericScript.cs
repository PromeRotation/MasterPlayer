using System.Collections.Generic;
using System.Numerics;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Examples;

/// <summary>
/// 通用机制示例：任意区域生效。被点名（TargetIcon）玩家脚下画一个跟随圈，
/// 同时注册一个 3 码圆圈的临时危险区（到期单次命中判定）。
/// 演示 [UserSetting] 每脚本设置、GetMechanisms 机制开关、Draw / SafeZone 门面。
/// </summary>
[MechanicScript(
    name: "示例-通用点名圈",
    territorys: [],
    guid: "masterplayer.example.generic",
    version: "0.1.0",
    author: "XSZ",
    note: "演示用：点名画跟随圈 + 临时危险区")]
public sealed class ExampleGenericScript : MasterPlayerScript
{
    [UserSetting("圈半径")]
    public float Radius { get; set; } = 1.5f;

    [UserSetting("判定倒计时（秒）")]
    public float CountdownSeconds { get; set; } = 3f;

    public override IEnumerable<string> GetMechanisms() => ["画圈提示"];

    protected override void OnStart()
    {
        OnTargetIcon(OnTargetIconMarked);
    }

    private void OnTargetIconMarked(LogSystemTargetIconEvent e)
    {
        if (!IsMechanismEnabled("画圈提示"))
            return;

        // [UserSetting] 属性不会被面板回写，运行值一律从 Settings 读取（键 = 属性名）
        var radius = Settings.GetFloat(nameof(Radius), Radius);
        var countdownSeconds = Settings.GetFloat(nameof(CountdownSeconds), CountdownSeconds);
        var targetId = (uint)e.TargetId;

        // 跟随被点名玩家的圈，倒计时结束自动消失
        Draw.AddBound(
            "target_icon_circle",
            new CircleGeometry(Vector3.Zero, radius),
            DrawBinding.Follow(targetId),
            DrawStyle.Danger,
            durationMs: countdownSeconds * 1000f);

        // 倒计时结束瞬间的单次命中危险区（3 码圆圈，跟随目标位置）
        SafeZone.AddDelayedHit(
            new CircleTemplate(3f),
            new FollowEntityPosition(targetId, Vector2.Zero, 0f, MissingEntityPolicy.DisableArea),
            delaySeconds: countdownSeconds);

        Log($"点名圈：目标 {targetId}，半径 {radius:0.0}，{countdownSeconds:0.0} 秒后判定");
    }
}
