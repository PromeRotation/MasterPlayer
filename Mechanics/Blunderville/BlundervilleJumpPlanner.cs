using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleJumpDecision(Vector3 Target, bool Hold, bool Jump, BlundervilleJumpCorridor Corridor, string Hint);

internal sealed class BlundervilleJumpPlanner
{
    private Vector3? _takeoff, _landing, _previous;
    private BlundervilleJumpCorridor? _corridor;
    private bool _airborne;
    private BlundervilleCourse _course;
    private DateTime _lastTime;
    private DateTime _nextJumpRetry;

    public BlundervilleJumpDecision? Plan(BlundervilleCourse course, DateTime now, Vector3 player,
        Vector3 forwardTarget, bool grounded, bool jumping)
    {
        if (!BlundervilleEncounter.Finite(player) || !BlundervilleEncounter.Finite(forwardTarget)) { Reset(); return null; }
        if (_course != course || _lastTime != default && now < _lastTime
            || _previous is { } old && Vector3.DistanceSquared(old, player) > 20 * 20) Reset();
        _course = course; _lastTime = now; _previous = player;
        if (course == BlundervilleCourse.FirstB && player.Y < 8.5f
            || course == BlundervilleCourse.Final && player.Y < 30
            || course == BlundervilleCourse.Ring && player.Y < 9)
        { ClearJump(); return null; }
        if (_landing is { } landing)
        {
            _airborne |= !grounded || jumping;
            var direction = Direction(_takeoff!.Value, landing);
            var delta = new Vector2(player.X - landing.X, player.Z - landing.Z);
            if (_airborne && grounded && !jumping && Vector2.Dot(delta, direction) >= -0.7f
                && MathF.Abs(delta.X * direction.Y - delta.Y * direction.X) < 1.5f)
                ClearJump();
            else
            {
                var retry = grounded && !jumping && !_airborne && now >= _nextJumpRetry;
                if (retry) _nextJumpRetry = now.AddSeconds(0.7);
                return new(landing, false, retry, _corridor!, "保持短跳落点，等待着地");
            }
        }
        // Keep the selected step while the executor backs up for its run-up.
        // Otherwise the final stairs can select the previous step by Z alone.
        var link = _corridor is { } prepared
            ? (prepared.Takeoff, prepared.Landing) : Link(course, player, forwardTarget);
        if (link == null) { ClearJump(); return null; }
        var (start, end) = link.Value;
        var forward = Direction(start, end);
        // A nearby gap does not supersede a corner still ahead of the base
        // route. In FirstB the second takeoff is visible before the turn onto
        // its board; selecting it there strands the base cursor at that turn.
        if (_corridor == null && Vector2.Dot(new(forwardTarget.X - start.X, forwardTarget.Z - start.Z), forward) < -.3f
            && !(course == BlundervilleCourse.Final && forwardTarget.Z <= 154.1f))
            return null;
        var offset = new Vector2(player.X - start.X, player.Z - start.Z);
        var progress = Vector2.Dot(offset, forward);
        if (progress > (course == BlundervilleCourse.Final ? 1.4f : .45f)) { ClearJump(); return null; }
        if (_takeoff != start)
        {
            _takeoff = start;
            _corridor = new(start, end, stairs: course == BlundervilleCourse.Final);
        }
        if (progress >= -0.25f && MathF.Abs(offset.X * forward.Y - offset.Y * forward.X) <= 0.4f)
        {
            if (!grounded || jumping)
                return new(start, !jumping, false, _corridor!, "等待可靠桥面后起跳");
            _landing = end; _airborne = false; _nextJumpRetry = now.AddSeconds(0.7);
            return new(end, false, true, _corridor!, "从实录跳点跳向下一段落点");
        }
        return new(start, false, false, _corridor!, "靠近实录跳点");
    }

    public void Reset()
    {
        ClearJump(); _previous = null; _lastTime = default; _course = BlundervilleCourse.None;
    }

    public void ConfirmLaunch(BlundervilleJumpCorridor corridor)
    {
        if (!ReferenceEquals(_corridor, corridor)) return;
        // The per-frame gate can launch between 10Hz planner scans. Bind the
        // flight to that exact step now; an airborne height must not select
        // the following stair before the current landing is observed.
        _takeoff = corridor.Takeoff; _landing = corridor.Landing; _airborne = false;
    }

    private void ClearJump() { _takeoff = _landing = null; _corridor = null; _airborne = false; _nextJumpRetry = default; }

    private static (Vector3 Start, Vector3 End)? Link(BlundervilleCourse course, Vector3 player, Vector3 target)
    {
        var side = (MathF.Abs(target.X) > 1 ? target.X : player.X) < 0 ? -1f : 1f;
        if (course == BlundervilleCourse.FirstB && player.Y >= 8.5f)
        {
            if (player.Z is <= -130.5f and > -137.7f)
                return (new(side * 10.25f, 10.03f, -134.2f), new(side * 11.9f, 10.03f, -138.4f));
            if (player.Z is <= -145f and > -155.2f)
                return (new(side * 8, 10.03f, -151.5f), new(side * 8, 10.03f, -155.9f));
        }
        if (course == BlundervilleCourse.Final && player.Z <= BlundervilleFinalPlanner.Bottom && player.Z > 137.7f)
        {
            var x = side * BlundervilleRoutes.FinalStairLane;
            if (player.Z > 149.5f && player.Y < 32.2f) return (new(x, 31.5f, 153), new(x, 32.6f, 149));
            if (player.Z > 145.5f && player.Y < 33.1f) return (new(x, 32.6f, 149), new(x, 33.5f, 145));
            if (player.Z > 141.5f && player.Y < 34f) return (new(x, 33.5f, 145), new(x, 34.4f, 141));
            return (new(x, 34.4f, 141), new(x, 35.8f, 137));
        }
        if (course == BlundervilleCourse.Ring && player.X < 187 && player.Z is <= -134f and > -142.1f)
            return (new(183.8f, 10, -138.6f), new(185.6f, 10, -142.8f));
        if (course == BlundervilleCourse.Ring && player.X > 208 && player.Z is >= -152f and < -144f)
            return (new(209.5f, 10, -147.7f), new(210.3f, 10, -143.3f));
        if (course == BlundervilleCourse.Ring && player.X > 208 && player.Z is >= -144f and < -139.4f)
            return (new(210.8f, 10, -142.6f), new(213.2f, 10, -138.7f));
        return null;
    }
    private static Vector2 Direction(Vector3 start, Vector3 end) => Vector2.Normalize(new(end.X - start.X, end.Z - start.Z));
}

internal sealed class BlundervilleJumpCorridor : IGeometryShape
{
    private readonly RectangleGeometry _rectangle;
    public Vector3 Takeoff { get; }
    public Vector3 Landing { get; }
    // Continuous input through all four treads. Each physical landing still
    // ends the flight lease; it is not the movement goal of the next hop.
    // Mesh risers are z~151/147/143/139: the nominal takeoffs have 1.8m of
    // supported tread, so a grounded hop up to 1.2m later retains clearance.
    public bool Stairs { get; }
    public Vector3 MovementTarget => Stairs ? new(Landing.X, 35.8f, 137) : Landing;
    // A vanishing-bridge lane switch lands anywhere on a 5m-wide lane, so the
    // run-up need not sit on the exact axis (see BlundervilleJumpExecution).
    public bool LaneSwitch { get; init; }
    public BlundervilleJumpCorridor(Vector3 takeoff, Vector3 landing, float width = 3, float padding = 2, bool stairs = false)
    {
        Takeoff = takeoff; Landing = landing; Stairs = stairs;
        var end = MovementTarget;
        var delta = end - takeoff; delta.Y = 0;
        _rectangle = new((takeoff + end) / 2, MathF.Atan2(delta.X, delta.Z), width, delta.Length() + padding);
    }
    public GeometryKind Kind => _rectangle.Kind;
    public Vector3 Position => _rectangle.Position;
    public float Rotation => _rectangle.Rotation;
    public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => _rectangle.Contains(point, epsilon);
    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => _rectangle.IntersectsSegment(start, end, epsilon);
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => _rectangle.GetContours(segments);
    public GeometryBounds GetBounds() => _rectangle.GetBounds();
}
