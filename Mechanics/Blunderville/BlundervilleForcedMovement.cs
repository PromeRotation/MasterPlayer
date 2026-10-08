using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleForcedMovement
{
    // 01:42 ARR: status 3737 lasts ~3s and ground displacement is 6m/s.
    // Test the complete travel, including .5m of endpoint margin, not just the
    // next waypoint. In particular, the FirstB 151-155 gap cannot be walked.
    public const float Travel = 18.5f;
    public static (float Heading, bool Safe) Heading(IGeometryShape floor, Vector3 position, Vector3 target)
    {
        var desired = MathF.Atan2(target.X - position.X, target.Z - position.Z);
        var best = desired; var longest = -1f;
        for (var i = 0; i < 72; ++i)
        {
            var angle = desired + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * MathF.Tau / 72;
            var direction = new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
            var end = position + direction * Travel;
            if (Supported(floor, position, end)) return (angle, true);
            if (longest > 0 && !Supported(floor, position, position + direction * (longest + .1f))) continue;
            // Too late to reach a full-length runway: face the longest remaining
            // supported direction instead of deliberately walking over a gap.
            var low = MathF.Max(0, longest); var high = Travel;
            for (var step = 0; step < 7; ++step)
            {
                var middle = (low + high) / 2;
                if (Supported(floor, position, position + direction * middle)) low = middle; else high = middle;
            }
            if (low > longest) { longest = low; best = angle; }
        }
        return (best, false);
    }

    private static bool Supported(IGeometryShape floor, Vector3 from, Vector3 to)
    {
        var side = Vector3.Normalize(new Vector3(to.Z - from.Z, 0, from.X - to.X)) * .15f;
        return ArenaBoundaryTraversal.ContainsSegment(floor, from, to)
            && ArenaBoundaryTraversal.ContainsSegment(floor, from + side, to + side)
            && ArenaBoundaryTraversal.ContainsSegment(floor, from - side, to - side);
    }
}
