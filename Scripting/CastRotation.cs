using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace MasterPlayer.Scripting;

public static class CastRotation
{
    // PR keeps the ActorCast packet snapshot in a private capture record.
    private static readonly PropertyInfo? PacketFacing = typeof(LogSystemCastStartEvent).Assembly
        .GetType("PromeRotation.LogSystem.Capture.StartCastCapture+StartCastCaptureRaw")
        ?.GetProperty("PacketFacing", BindingFlags.Instance | BindingFlags.Public);

    /// <summary>仅主线程：优先读取事件包的锁定朝向，缺失时读取匹配技能的当前 CastRotation；不使用实体普通朝向。</summary>
    public static unsafe bool TryGet(LogSystemCastStartEvent e, out float rotation)
    {
        rotation = default;
        if (e.Raw is { } raw && PacketFacing is { PropertyType: var propertyType, DeclaringType: { } rawType }
            && propertyType == typeof(float) && rawType.IsInstanceOfType(raw)
            && PacketFacing.GetValue(raw) is float packetRotation && float.IsFinite(packetRotation))
        {
            rotation = packetRotation;
            return true;
        }

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
