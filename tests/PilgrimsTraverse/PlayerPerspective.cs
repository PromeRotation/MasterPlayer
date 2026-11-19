using Dalamud.Game.ClientState.Objects.SubKinds;

namespace MasterPlayer;

// These tests construct immutable PlayerState values and never sample live game memory.
internal static class PlayerPerspective
{
    public static IPlayerCharacter? LivePlayer => null;
}
