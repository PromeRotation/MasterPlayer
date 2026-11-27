using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using MasterPlayer.Replay;

namespace MasterPlayer;

internal static class PlayerPerspective
{
    public static bool IsDutyReplay => DutyReplayState.Active;
    public static bool IsReplay => MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true || IsDutyReplay;

    public static unsafe IPlayerCharacter? LivePlayer
    {
        get
        {
            if (!IsDutyReplay) return Svc.Objects?.LocalPlayer;
            // In spectator mode, selecting a player updates FocusObjectId instead
            // of the hard target. The viewed actor's combat target is independent.
            var control = Control.Instance();
            var spectator = control != null ? control->SpectatorSystem : null;
            var id = spectator != null ? spectator->FocusObjectId : 0;
            return id is 0 or 0xE0000000 ? null : Svc.Objects.SearchByEntityId(id) as IPlayerCharacter;
        }
    }

    public static ulong GetId(ReplayWorldState? world = null)
        => world != null ? world.LocalPlayer?.Id ?? 0 : LivePlayer?.EntityId ?? 0;

    public static ReplayActorState? Capture(ReplayWorldState? world, DateTime now)
        => world != null ? world.LocalPlayer : LivePlayer is { } player
            ? ReplayWorldCapture.CaptureActor(player, now) : null;
}
