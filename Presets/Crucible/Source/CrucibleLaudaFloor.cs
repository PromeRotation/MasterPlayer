using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Crucible;

static class CrucibleLaudaFloor
{
    public static readonly WPos Center = new(520, -420);
    // Client b4228yuka1_v2 fire-mesh holes. Both f1x3 Gatler and f1x5 Lauda
    // use sgbg_f1x3_a1_gim05 at (520,-420), without rotation or scaling.
    private static readonly Shape[] Platforms =
    [
        new Rectangle(Center, 10, 20),
        new Rectangle(new(520, -442.5f), 2.5f, 2.5f),
        new Rectangle(new(520, -397.5f), 2.5f, 2.5f),
        new Rectangle(new(532.5f, -427.5f), 2.5f, 2.5f),
        new Rectangle(new(532.5f, -417.5f), 2.5f, 2.5f),
        new Rectangle(new(507.5f, -422.5f), 2.5f, 2.5f),
        new Rectangle(new(507.5f, -412.5f), 2.5f, 2.5f)
    ];
    public static readonly ArenaBoundsCustom Display = new(Platforms, CenterOverride: Center);
    public static readonly ArenaBoundsCustom Safe = new(Platforms, AdjustForHitboxInwards: true, CenterOverride: Center);

    public static bool Contains(WPos pos) => Safe.Contains(pos - Center);

    public static bool OnPlatform(WPos pos) => Display.Contains(pos - Center);

    // Nearest standing point at least 1y inside a platform.
    public static WPos Nearest(WPos pos)
    {
        var best = Center;
        var bestDistance = float.MaxValue;
        for (var x = 504f; x <= 536; x += .5f)
            for (var z = -446f; z <= -394; z += .5f)
            {
                var p = new WPos(x, z);
                var distance = (p - pos).LengthSq();
                if (distance < bestDistance && Contains(p) && Contains(p + new WDir(.5f, 0)) && Contains(p - new WDir(.5f, 0))
                    && Contains(p + new WDir(0, .5f)) && Contains(p - new WDir(0, .5f)))
                {
                    best = p;
                    bestDistance = distance;
                }
            }
        return best;
    }

    public static bool SafeTravel(WPos start, WDir displacement)
    {
        // Electric floor can be crossed during flight, but neither endpoint may touch it.
        return Contains(start) && Contains(start + displacement);
    }
}
