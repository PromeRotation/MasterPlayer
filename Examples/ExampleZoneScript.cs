using System.Collections.Generic;
using System.Numerics;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Examples;

/// <summary>
/// 区域限定示例：仅在 territorys 列出的副本（示例用 1234）内自动启动。
/// 按读条 ActionId 注册不同形状的危险区（圆形 / 扇形），判定时刻 = 读条结束。
/// 演示 OnStartCast、GeometryTemplate + TransformBinding、机制开关。
/// </summary>
[MechanicScript(
    name: "示例-区域读条躲避",
    territorys: [1234],
    guid: "masterplayer.example.zone",
    version: "0.1.0",
    author: "XSZ",
    note: "演示用：按读条 ActionId 注册圆形/扇形危险区")]
public sealed class ExampleZoneScript : MasterPlayerScript
{
    // 示例 ActionId，实际使用时换成真实技能 ID
    private const ushort ActionCircleAoe = 1001;
    private const ushort ActionFanAoe = 1002;

    public override IEnumerable<string> GetMechanisms() => ["危险区注册"];

    protected override void OnStart()
    {
        OnStartCast(OnCastStarted);
    }

    private void OnCastStarted(LogSystemCastStartEvent e)
    {
        if (!IsMechanismEnabled("危险区注册"))
            return;

        switch (e.ActionId)
        {
            case ActionCircleAoe:
                // 以施法者为圆心的 8 码圆形，读条结束瞬间判定
                SafeZone.AddDelayedHit(
                    new CircleTemplate(8f),
                    new FollowEntityPositionAndRotation((uint)e.SourceId, Vector2.Zero, MissingEntityPolicy.RemoveArea),
                    delaySeconds: e.CastTime);
                break;

            case ActionFanAoe:
                // 施法者前方 20 码 120° 扇形（跟随施法者位置与朝向）
                SafeZone.AddDelayedHit(
                    new FanTemplate(20f, float.DegreesToRadians(120f)),
                    new FollowEntityPositionAndRotation((uint)e.SourceId, Vector2.Zero, MissingEntityPolicy.RemoveArea),
                    delaySeconds: e.CastTime);
                break;
        }
    }

    // Move 门面用法示例（注释保留，启用前先确认本体 SafeZone 自动移动未接管，见铁律 3）：
    //
    // protected override void OnUpdate(float deltaTime)
    // {
    //     // MoveTo 是 last-write-wins：只在目标点变化时调用，不要每帧重发
    //     if (!Move.IsRunning)
    //         Move.MoveTo(new GreenMovePoint(new Vector3(100f, 0f, 100f), Tolerance: 0.3f));
    // }
    //
    // // 只有自己接管过移动才 Stop：
    // protected override void OnStop()
    // {
    //     if (Move.IsRunning)
    //         Move.Stop();
    // }
}
