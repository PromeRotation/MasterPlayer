using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CastrumAbaniaArena
{
    internal static IGeometryShape For(uint bossOid, float y)
        => bossOid switch
        {
            CastrumAbaniaCatalog.MagnaRoader => AbaniaArenas.Magna(y),
            CastrumAbaniaCatalog.SubjectNumberXXIV => AbaniaArenas.Subject(y),
            CastrumAbaniaCatalog.Inferno => AbaniaArenas.Inferno(y),
            _ => throw new ArgumentOutOfRangeException(nameof(bossOid)),
        };
}
