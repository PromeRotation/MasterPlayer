using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// Keep uphill input while making shallow corrections around the rolling R2
// snowballs. A straight lane cannot clear both alternating pairs: the gap
// between adjacent lanes is only 3.67m. Targets stay ahead, never on a row.
internal static class BlundervilleRampAscent
{
    // 2026-10-14 23:45 ARR: two stuns at 2.49m from the ball centre; the
    // widest recorded stun (16:40 ARR) was 2.63m. 2.2 walked into both.
    internal const float BallRadius = 2.8f;
    internal const double Segment = .5, Sample = .05;
    private const int Width = 64, Samples = 10;
    private const double Envelope = .6;
    // Lateral metres per metre uphill: 1 is W+A / W+D, the human climb (camera
    // up the ramp, W held, strafe to dodge). 0.267 (15°) could not clear an
    // R2.8 ball within its lane gap and waited at the foot (01:45, 02:10 ARRs).
    // 0.6 (31°) climbed ~1s faster but touched balls in 6 of 177 replays.
    internal const float Strafe = 1;
    private static readonly float[] Directions = [0, -1, 1];
    private static readonly float[] EntryLanes = [-3.65f, 0, 3.65f];
    private readonly record struct Body(float X, float MinZ, float MaxZ);
    private readonly record struct PusherBody(float Z, float MinX, float MaxX);
    private sealed record Frame(Body[] Balls, BlundervilleRampStep[] Pushers, PusherBody[] Bodies);
    private sealed record Node(Vector2 Position, Vector2 FirstAim, float Cost, float Lane, double Depart, bool Started = false,
        Node? Parent = null, Vector2 SegmentAim = default);
    internal sealed record Result(Vector3 Aim, float Lane, double Depart, bool Complete, IReadOnlyList<Vector3> Aims, IReadOnlyList<Vector2> Starts);

    internal static Result Search(Vector3 player, DateTime now, IReadOnlyList<BlundervilleRampStep> steps,
        IReadOnlyList<BlundervilleRampBalls.Ball> balls, bool floor = false)
    {
        var depthLimit = floor ? 60 : 36;
        // The 3703 onset is observed between z=-338 and -340.5. The faster
        // entry can put the ascent up to 0.85s ahead of a floor prediction.
        var early = floor ? .85 : 0;
        var frames = new Frame[depthLimit * Samples + 1];
        for (var i = 0; i < frames.Length; ++i)
        {
            var at = now.AddSeconds(i * Sample);
            var bodies = new List<Body>();
            foreach (var ball in balls)
            {
                var age = (at - ball.Spawn).TotalSeconds;
                if (age < -Envelope || age > BlundervilleRampBalls.Despawn + Envelope + early) continue;
                bodies.Add(new(ball.X, BlundervilleRampBalls.Z(Math.Clamp(age - Envelope - early, 0, BlundervilleRampBalls.Despawn))!.Value,
                    BlundervilleRampBalls.Z(Math.Clamp(age + Envelope, 0, BlundervilleRampBalls.Despawn))!.Value));
            }
            var pusherBodies = new List<PusherBody>();
            foreach (var step in steps)
            {
                var age = (at - step.At).TotalSeconds;
                if (age < -.1 || age > BlundervilleRampPushers.StepSeconds + .1 + early) continue;
                // Actor moves arrive about 0.7s after the step. Cover that
                // traversal, then its stationary R1 body until the next step.
                // 20:57 ARR: environmental stun at (+0.519,-0.854) from the
                // resting body, 1.029s after its effect; three older ARRs agree.
                pusherBodies.Add(age <= .8 + early
                    ? new(step.Z, MathF.Min(step.FromX, step.ToX), MathF.Max(step.FromX, step.ToX))
                    : new(step.Z, step.ToX, step.ToX));
            }
            frames[i] = new([.. bodies], steps.Where(s => (s.At - at).TotalSeconds >= -.2 - early && (s.At - at).TotalSeconds <= .4).ToArray(), [.. pusherBodies]);
        }
        var start = new Vector2(player.X, player.Z);
        var best = new Node(start, start + new Vector2(0, -6), 0, player.X, 0);
        var nodes = new List<Node>();
        if (!floor) nodes.Add(best);
        for (var depth = 0; depth < depthLimit; ++depth)
        {
            if (floor && depth * Segment <= 12)
                foreach (var lane in EntryLanes)
                    if (Vector2.Distance(start, new(lane, BlundervilleSlimeRamp.Wait)) / 6
                        + (BlundervilleSlimeRamp.Wait - BlundervilleSlimeRamp.Bottom) / 6 + .15 <= depth * Segment)
                        nodes.Add(new(new(lane, BlundervilleSlimeRamp.Bottom), default, 0, lane, depth * Segment));
            var next = new Dictionary<(int X, int Z), Node>();
            foreach (var node in nodes)
                foreach (var direction in Directions)
                {
                    var aim = Aim(node.Position, direction);
                    var p = node.Position;
                    var safe = true;
                    var penetration = Penetration(p, frames[depth * Samples]);
                    for (var k = 1; k <= Samples; ++k)
                    {
                        p += (Vector2.Normalize(aim - p) * 6 + new Vector2(0, 4)) * (float)Sample;
                        var nextPenetration = Penetration(p, frames[depth * Samples + k]);
                        // An updated animation sample can put us inside the
                        // uncertainty envelope. Keep steering out of it instead
                        // of rejecting every escape and reverting to straight.
                        if (nextPenetration > 0 && (penetration <= 0 || nextPenetration >= penetration - .0001f)) { safe = false; break; }
                        penetration = nextPenetration;
                        if (p.Y < BlundervilleSlimeRamp.Top) break;
                    }
                    if (!safe) continue;
                    var cost = node.Cost + MathF.Abs(aim.X - node.Position.X) * .015f;
                    var candidate = new Node(p, node.Started ? node.FirstAim : aim, cost, node.Lane, node.Depart, true, node, aim);
                    if (p.Y < BlundervilleSlimeRamp.Top) return ResultOf(candidate, true);
                    var key = ((int)MathF.Round(p.X * 5), (int)MathF.Round(p.Y * 10));
                    if (!next.TryGetValue(key, out var old) || Rank(candidate) < Rank(old)) next[key] = candidate;
                }
            if (next.Count == 0)
            {
                if (floor) { nodes.Clear(); continue; }
                if (depth == 0)
                {
                    var score = float.PositiveInfinity;
                    foreach (var direction in Directions)
                    {
                        var aim = Aim(start, direction);
                        var p = start; var risk = 0f;
                        for (var k = 1; k <= Samples * 2; ++k)
                        {
                            p += (Vector2.Normalize(aim - p) * 6 + new Vector2(0, 4)) * (float)Sample;
                            risk += Penetration(p, frames[k]);
                        }
                        if (risk < score) { score = risk; best = best with { FirstAim = aim }; }
                    }
                }
                break;
            }
            nodes = next.Values.OrderBy(Rank).Take(Width).ToList();
            best = nodes[0];
        }
        return ResultOf(best, false);

        Result ResultOf(Node node, bool complete)
        {
            var aims = new List<Vector3>(); var starts = new List<Vector2>();
            for (var n = node; n.Parent != null; n = n.Parent)
            { aims.Add(new(n.SegmentAim.X, player.Y, n.SegmentAim.Y)); starts.Add(n.Parent.Position); }
            aims.Reverse(); starts.Reverse();
            return new(new(node.FirstAim.X, player.Y, node.FirstAim.Y), node.Lane, node.Depart, complete, aims, starts);
        }
    }

    private static float Rank(Node node) => node.Position.Y + node.Cost;

    private static Vector2 Aim(Vector2 position, float direction)
    {
        var z = MathF.Max(BlundervilleSlimeRamp.Top - 2, position.Y - 6);
        var side = (position.Y - z) * Strafe;
        var x = Math.Clamp(position.X + direction * side, -5.6f, 5.6f);
        return new(position.X + Math.Clamp(x - position.X, -side, side), z);
    }

    private static float Penetration(Vector2 p, Frame frame)
    {
        if (MathF.Abs(p.X) > 5.8f) return 100;
        var penetration = 0f;
        foreach (var ball in frame.Balls)
        {
            var dx = p.X - ball.X;
            if (MathF.Abs(dx) >= BallRadius) continue;
            var dz = p.Y - Math.Clamp(p.Y, ball.MinZ, ball.MaxZ);
            if (MathF.Abs(dz) >= BallRadius) continue;
            penetration += MathF.Max(0, BallRadius - MathF.Sqrt(dx * dx + dz * dz));
        }
        foreach (var step in frame.Pushers)
            if (MathF.Abs(p.Y - step.Z) < 1.7f && p.X >= MathF.Min(step.FromX, step.ToX) - .3f && p.X <= MathF.Max(step.FromX, step.ToX) + .3f)
                return 100;
        foreach (var body in frame.Bodies)
        {
            var dx = p.X - Math.Clamp(p.X, body.MinX, body.MaxX);
            var dz = p.Y - body.Z;
            penetration += MathF.Max(0, 1.2f - MathF.Sqrt(dx * dx + dz * dz));
        }
        return penetration;
    }
}
