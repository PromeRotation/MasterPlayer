namespace MasterPlayer.Scripting;

/// <summary>
/// 机制提示门面：脚本侧的 AIHints 式能力出口（朝向引导），与机制导航桥共用宿主 MechanicControl 可选接口。
/// 内置纪律：
/// - 回放期间 SetFacing 为空操作（回放禁止宿主控制）；
/// - 宿主缺少 MechanicControl 接口时一次性警告并跳过，走位与绘制不受影响；
/// - 每脚本独立 owner，脚本停止时 Cleanup 撤销自己的朝向请求，绝不清除其他来源。
/// </summary>
public sealed class ScriptHintsApi
{
    private readonly MechanicHost.MechanicFacingControl _facing;
    private readonly Func<bool> _isReplay;

    internal ScriptHintsApi(Func<bool> isReplay)
    {
        _isReplay = isReplay;
        _facing = new MechanicHost.MechanicFacingControl("ScriptHints",
            "[MasterPlayer] 当前 PR 本体未提供机制控制接口：脚本朝向引导不可用；走位与绘制继续工作。");
    }

    /// <summary>请求宿主把角色面向转到 <paramref name="headingRadians"/>（FFXIV 弧度约定：0=+Z，π/2=+X）；null 撤销请求。</summary>
    public void SetFacing(float? headingRadians)
    {
        if (_isReplay() || !Settings.MasterPlayerSettings.Instance.EnableSmartMovement)
        {
            _facing.Clear();
            return;
        }
        _facing.Apply(headingRadians);
    }

    /// <summary>撤销当前朝向请求，等同 SetFacing(null)。</summary>
    public void ClearFacing() => SetFacing(null);

    internal void Cleanup() => _facing.Clear();
}
