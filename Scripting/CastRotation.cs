using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace MasterPlayer.Scripting;

public static class CastRotation
{
    // PR keeps the ActorCast packet snapshot in a private capture record.
    private static readonly PropertyInfo? PacketFacing = typeof(LogSystemCastStartEvent).Assembly
        .GetType("PromeRotation.LogSystem.Capture.StartCastCapture+StartCastCaptureRaw")
        ?.GetProperty("PacketFacing", BindingFlags.Instance | BindingFlags.Public);

    public static bool TryGetPacket(LogSystemCastStartEvent e, out float rotation)
    {
        rotation = default;
        if (e.Raw is ReplayCastData replay)
        {
            if (replay.PacketFacing is not { } facing || !float.IsFinite(facing)) return false;
            rotation = facing;
            return true;
        }
        if (e.Raw is { } raw && PacketFacing is { PropertyType: var propertyType, DeclaringType: { } rawType }
            && propertyType == typeof(float) && rawType.IsInstanceOfType(raw)
            && PacketFacing.GetValue(raw) is float packetRotation && float.IsFinite(packetRotation))
        {
            rotation = packetRotation;
            return true;
        }
        return false;
    }

    /// <summary>仅主线程。回放事件绝不回退到实时对象表；普通朝向不能替代锁定施法朝向。</summary>
    public static unsafe bool TryGet(LogSystemCastStartEvent e, out float rotation)
    {
        if (TryGetPacket(e, out rotation)) return true;
        if (e.Raw is ReplayCastData) return false;

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
