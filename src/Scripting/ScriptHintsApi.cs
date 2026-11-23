using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace MasterPlayer.Scripting;

/// <summary>
/// 机制控制门面：面向、选敌和 ACR 动作限制通过 PR MechanicControl 发布，移动暂停使用 MP owner 请求。
/// 内置纪律：
/// - 回放期间只清理 owner，不执行宿主控制或原生自动攻击控制；
/// - 宿主缺少 MechanicControl 接口时一次性警告并跳过，走位与绘制不受影响；
/// - 每脚本独立 owner，脚本停止时 Cleanup 撤销自己的控制请求，绝不清除其他来源；
/// - 有效请求须在 OnUpdate 持续刷新，PR 的租约为 500ms。
/// </summary>
public sealed class ScriptHintsApi
{
    private readonly MechanicHost.MechanicFacingControl _facing;
    private readonly Func<bool> _isReplay;
    private readonly ScriptMoveApi? move;
    private readonly Action<bool, ulong[]> stopAutoAttack;
    private float? heading;
    private bool holdActions;
    private bool blockCasts;
    private ulong[] forbiddenTargets = [];
    private ulong target;

    internal ScriptHintsApi(Func<bool> isReplay, ScriptMoveApi? move = null,
        MechanicHost.MechanicFacingControl? control = null, Action<bool, ulong[]>? stopAutoAttack = null)
    {
        _isReplay = isReplay;
        this.move = move;
        this.stopAutoAttack = stopAutoAttack ?? StopForbiddenAutoAttacks;
        _facing = control ?? new MechanicHost.MechanicFacingControl("ScriptHints",
            "[MasterPlayer] 当前 PR 本体未提供机制控制接口：脚本选敌、朝向、停手和禁读条请求不可用；走位与绘制继续工作。");
    }

    /// <summary>请求宿主把角色面向转到 <paramref name="headingRadians"/>（FFXIV 弧度约定：0=+Z，π/2=+X）；null 撤销请求。</summary>
    public void SetFacing(float? headingRadians)
    {
        heading = headingRadians;
        Publish();
    }

    /// <summary>
    /// 设置 PR ACR 停手、禁止新读条及禁攻目标。cancelCast 只作用于本次调用；
    /// 全停手会阻止治疗和自保，反击类机制应使用 forbiddenTargets 精确禁攻。
    /// 同时停止当前禁攻目标的原生自动攻击；全停手停止当前自动攻击。
    /// 结束时调用 ClearActionRestrictions；有效期间每帧刷新。
    /// </summary>
    public void SetActionRestrictions(bool holdActions = false, bool blockCasts = false,
        bool cancelCast = false, params ulong[] forbiddenTargets)
    {
        this.holdActions = holdActions;
        this.blockCasts = blockCasts;
        this.forbiddenTargets = forbiddenTargets.ToArray();
        Publish(cancelCast);
    }

    /// <summary>撤销本脚本的 ACR 动作限制，保留朝向请求。</summary>
    public void ClearActionRestrictions()
    {
        SetActionRestrictions();
    }

    /// <summary>请求 PR 优先攻击指定目标；0 撤销本脚本的选敌请求，不改变其他 owner。</summary>
    public void SetTarget(ulong targetId)
    {
        target = targetId;
        Publish();
    }

    /// <summary>暂停 MP 自动走位及本脚本直接移动；保留规划和绘制，不接管 PR 本体路线。</summary>
    public void SetMovementHold(bool hold)
    {
        if (!CanControl)
        {
            Cleanup();
            return;
        }
        if (hold)
            move?.Stop();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, hold, hold: hold);
    }

    /// <summary>撤销当前朝向请求，等同 SetFacing(null)。</summary>
    public void ClearFacing() => SetFacing(null);

    internal void Cleanup()
    {
        heading = null;
        holdActions = false;
        blockCasts = false;
        forbiddenTargets = [];
        target = 0;
        try
        {
            _facing.Clear();
        }
        finally
        {
            MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        }
    }

    private bool CanControl => !_isReplay() && !PlayerPerspective.IsReplay
        && Settings.MasterPlayerSettings.Instance.EnableSmartMovement;

    private void Publish(bool cancelCast = false)
    {
        if (!CanControl)
        {
            Cleanup();
            return;
        }
        _facing.Apply(heading, holdActions, blockCasts, cancelCast, forbiddenTargets, target);
        if (holdActions || forbiddenTargets.Length != 0)
            stopAutoAttack(holdActions, forbiddenTargets);
    }

    private static unsafe void StopForbiddenAutoAttacks(bool hold, ulong[] forbidden)
    {
        if (Svc.Targets?.Target is not { } target || (!hold && !forbidden.Contains(target.EntityId))) return;
        var ui = UIState.Instance();
        if (ui != null && ui->WeaponState.AutoAttackState.Get())
            ui->WeaponState.AutoAttackState.Set(false);
    }
}
