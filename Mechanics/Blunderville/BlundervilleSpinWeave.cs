using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// FirstB spin bridge (z -272..-334, 3702 at 3.6m/s): the 34775 rows sweep
// 3x3m boxes on one side only — right rows z -281/-301/-321 at x 0.5..6.5,
// left rows z -288/-308/-328 at x -6.5..-0.5. MP weaved out to |x| 5–7
// around each row with the solver and took ~28s (15:10, 18:27 ARRs); the
// route research's timing-free line keeps |x| >= 1.75 on the free side of
// each row and switches sides in the gaps (21.6s). Every point of this line
// stays a metre outside every row box at any time, so it is published as one
// direct traversal; a player off the line keeps the ordinary route.
internal static class BlundervilleSpinWeave
{
    internal const float Side = 2.5f, Body = .5f, Margin = .3f;
    private static readonly (float Z, float From, float To)[] Rows =
        [(-281, -1, 8), (-301, -1, 8), (-321, -1, 8), (-288, -8, 1), (-308, -8, 1), (-328, -8, 1)];
    private static readonly Vector3[] Line =
    [
        new(-Side, 10.03f, -278), new(-Side, 10.03f, -283.3f), new(Side, 10.03f, -285.7f), new(Side, 10.03f, -290.3f),
        new(-Side, 10.03f, -298.7f), new(-Side, 10.03f, -303.3f), new(Side, 10.03f, -305.7f), new(Side, 10.03f, -310.3f),
        new(-Side, 10.03f, -318.7f), new(-Side, 10.03f, -323.3f), new(Side, 10.03f, -325.7f), new(Side, 10.03f, -330.3f),
        new(0, 10.03f, -336),
    ];

    public static BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state, Vector3 player, bool recovering)
    {
        if (state.Course != BlundervilleCourse.FirstB || !state.IsRacing || state.Qualified || normal.Hold || recovering
            || player.Y < 9.8f || player.Z > -271 || player.Z < -335.5f || MathF.Abs(player.X) > 8) return normal;
        var next = Array.FindIndex(Line, v => v.Z < player.Z - .3f);
        if (next < 0 || !Clear(player, Line[next])) return normal;
        var path = Line[next..];
        return normal with { Target = path[^1], Hint = "旋转桥：沿免时序的左右交替路线", DirectTraversal = true, ContinuousPath = path };
    }

    // Static: the segment never enters a row box grown by the body radius.
    internal static bool Clear(Vector3 from, Vector3 to)
    {
        foreach (var (z, minX, maxX) in Rows)
        {
            var box = new RectangleGeometry(new((minX + maxX) / 2, 10.03f, z), 0, maxX - minX, 3);
            if (box.Contains(from, Body + Margin) || box.IntersectsSegment(from, to, Body + Margin)) return false;
        }
        return true;
    }

    internal static IReadOnlyList<Vector3> Route => Line;
}
