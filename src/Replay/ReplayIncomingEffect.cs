namespace MasterPlayer.Replay;

public sealed record ReplayIncomingEffect(int Slot, uint GlobalSequence, byte TargetIndex, bool TargetConfirmed, bool Knockback)
{
    // Optional in v2 recordings; older files retain the observed HP without a prediction.
    public int HpDelta { get; init; }

    internal static bool IsKnockback(ulong effect) => (byte)effect is >= 31 and <= 36;

    internal static int PendingHpDelta(ulong effect, ulong actor, ulong source, ulong target,
        bool sourceConfirmed, bool targetConfirmed)
    {
        var flags = (byte)(effect >> 40);
        var atSource = (flags & 0x80) != 0;
        if (actor != (atSource ? source : target) || (atSource ? sourceConfirmed : targetConfirmed)) return 0;
        var value = (int)(effect >> 48);
        if ((flags & 0x40) != 0) value += (byte)(effect >> 32) << 16;
        return (byte)effect switch
        {
            3 or 5 or 6 => -value,
            4 => value,
            _ => 0,
        };
    }
}
