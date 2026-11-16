namespace MasterPlayer.Mechanics.Enuo;

internal static class EnuoExecutionGate
{
    public static bool Allows(bool smartMovement, bool mechanismEnabled, bool replay, bool alive, bool assigned)
        => smartMovement && mechanismEnabled && !replay && alive && assigned;
}
