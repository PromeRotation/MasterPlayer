namespace MasterPlayer.Combat.Sequences;

internal static class NinjaSequence
{
    public static uint[] Expand(uint id) => id switch
    {
        2265 => [2259, id],
        2266 or 16491 => [2261, 2259, id],
        2267 => [2259, 2261, id],
        2268 or 16492 => [2259, 2263, id],
        2269 => [2263, 2261, 2259, id],
        2270 => [2261, 2263, 2259, id],
        2271 => [2259, 2261, 2263, id],
        _ => [id],
    };

    public static bool IsMudra(uint id) => id is 2259 or 2261 or 2263 or 18805 or 18806 or 18807;
}
