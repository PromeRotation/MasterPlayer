using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace MasterPlayer.Scripting;

public static class CastRotation
{
    /// <summary>仅主线程：读取与事件技能匹配的当前 CastRotation；失效读条返回 false，不使用实体普通朝向代替。</summary>
    public static unsafe bool TryGet(LogSystemCastStartEvent e, out float rotation)
    {
        rotation = default;
        if (Svc.Objects.SearchByEntityId((uint)e.SourceId) is not IBattleChara caster
            || caster.Address == nint.Zero || !caster.IsCasting || caster.CastActionId != e.ActionId)
            return false;

        var value = ((NativeBattleChara*)caster.Address)->CastRotation;
        if (!float.IsFinite(value))
            return false;

        rotation = value;
        return true;
    }
}
