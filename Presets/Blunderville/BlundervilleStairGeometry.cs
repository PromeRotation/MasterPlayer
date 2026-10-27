using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

namespace MasterPlayer.Mechanics.Blunderville;

// The saved graph chooses an entrance. It does not turn the wide staircase
// into a single X coordinate: each grounded tread can choose its next hop.
internal static class BlundervilleStairGeometry
{
    internal static int Step(Vector3 p) => p.Z > 149.5f && p.Y < 32.2f ? 0
        : p.Z > 145.5f && p.Y < 33.1f ? 1 : p.Z > 141.5f && p.Y < 34 ? 2 : 3;

    internal static bool OnStairs(Vector3 p) => float.IsFinite(p.X) && p.Y >= 30
        && MathF.Abs(p.X) is >= 1.5f and <= 15.4f && p.Z is >= 136.5f and <= 155.5f;

    internal static BlundervilleJumpCorridor? Choose(Vector3 player, DateTime now,
        IReadOnlyList<BlundervilleHazard> hazards, float speed = 6)
    {
        if (!OnStairs(player)) return null;
        var step = Step(player); var takeoffZ = 153 - 4 * step; var landingZ = takeoffZ - 4;
        var takeoffY = new[] { 31.5f, 32.6f, 33.5f, 34.4f }[step];
        var landingY = new[] { 32.6f, 33.5f, 34.4f, 35.8f }[step];
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
        BlundervilleJumpCorridor? best = null; var cost = float.PositiveInfinity;
        // One supported tread at a time, with an actual takeoff anywhere in
        // its launch band. Wider lateral travel happens on the tread first.
        var z = Math.Min(takeoffZ, player.Z);
        if (z < takeoffZ - BlundervilleJumpExecution.StairBand) return null;
        // The walking mesh trims the outer lip of these treads. The 20:21 ARR
        // records grounded support at X=-14.45/Z=140.4/Y=34.41. Permit inward
        // travel across that same tread, never across a riser or the centre gap.
        var tread = new RectangleGeometry(new(MathF.Sign(player.X) * 9, takeoffY, takeoffZ + .1f), 0, 11.5f, 2.8f);
        var xs = Enumerable.Range(3, 28).Select(i => MathF.Sign(player.X) * i * .5f)
            .Append(player.X).Distinct().OrderBy(x => MathF.Abs(x - player.X)).ToArray();
        foreach (var x in xs)
        {
            var start = new Vector3(x, takeoffY, z);
            var walk = Distance(player, start);
            var acrossTread = player.Z >= takeoffZ - 1.25f && player.Z <= takeoffZ + 1.4f
                && tread.Contains(player) && tread.Contains(start);
            if (walk > 6 || walk > .05f && !Link(player, start) && !acrossTread) continue;
            foreach (var endX in xs.Where(v => MathF.Abs(v-x) <= 2))
            foreach (var offset in new[] { 0f, .4f, -.4f })
            {
                var end = new Vector3(endX, landingY, landingZ + offset);
                var direction = Vector3.Normalize((end - start) with { Y = 0 });
                // A short overrun keeps forward input until physical landing,
                // when the next tread is selected. It never aims through all
                // remaining risers along one frozen axis.
                var run = end + direction * .65f;
                var flight = Distance(start, end);
                var score = walk + flight + MathF.Abs(offset) * .5f;
                if (score >= cost) continue;
                if (flight is < 2.2f or > 4.6f || !Supported(end) || !Link(end, run)) continue;
                if (step == 3 && !Link(run, new(MathF.Sign(player.X) * 6, landingY, 136.6f))) continue;
                var arrival = walk / speed; var until = arrival + Distance(start, run) / speed;
                if (!index.Segment(player, start, 0, arrival) || !index.Segment(start, run, arrival, until)
                    || !index.Stand(run, until, until + .3)) continue;
                cost = score;
                best = new(start, end, width: .7f, padding: .2f, stairs: true, movementTarget: run, supportedTread: tread) { FlexibleStairs = true };
            }
        }
        return best;
    }

    private static bool Supported(Vector3 p) => MathF.Abs(p.X) is >= 1.5f and <= 15.4f
        && Link(p - Vector3.UnitX * .1f, p + Vector3.UnitX * .1f);
}
