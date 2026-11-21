namespace MasterPlayer.Mechanics.Dungeons;

internal static class CastrumAbaniaCatalog
{
    internal const uint Territory = 1145;

    internal const uint MagnaRoader = 0x1AA9;
    internal const uint SubjectNumberXXIV = 0x3F3B;
    internal const uint Inferno = 0x1AAE;
    internal const uint MagnaHelper = 0x18D6;
    internal const uint SubjectHelper = 0x233C;
    internal const uint InfernoHelper = 0x18D6;
    internal const uint Cannon = 0x1AAC;
    internal const uint DeathClaw = 0x1AB0;

    internal const uint TargetIconSpread = 74;
    internal const uint ClawTether = 1;

    internal static readonly uint[] Actions =
    [
        7957, 7958, 7959, 7960, 8318, 8184, 8325, 8336,
        33448, 33449, 33450, 33451, 33452, 33453, 33454, 33455, 33456,
        33457, 33458, 33460, 33461, 33462, 33463, 33464, 33465, 33466, 33467,
        33747, 33748, 33749, 33750,
        7973, 7974, 7975, 7976, 7977, 7978, 7979, 8328, 8329, 8331, 8332, 8334, 8335,
    ];

    internal static bool IsBoss(uint oid)
        => oid is MagnaRoader or SubjectNumberXXIV or Inferno;

    internal static bool IsHelper(uint oid)
        => oid is MagnaHelper or SubjectHelper or InfernoHelper;

    internal static bool Owns(uint oid, uint territory)
        => territory == Territory && IsBoss(oid);

    internal static bool IsScript(string id)
        => string.Equals(id, "masterplayer.dungeon.castrum-abania", StringComparison.OrdinalIgnoreCase);

    internal static string Name(uint oid) => oid switch
    {
        MagnaRoader => "Magna Roader",
        SubjectNumberXXIV => "Subject Number XXIV",
        Inferno => "Inferno",
        _ => "白山堡 Boss",
    };
}
