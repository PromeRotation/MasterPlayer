using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleRaceDecision(Vector3? Target, bool Hold, bool Jump, float? Facing, string Hint, bool DirectTraversal = false)
{
    public IReadOnlyList<Vector3>? ContinuousPath { get; init; }
    public float HeadingTolerance { get; init; }
    public Spatial.DirectTraversalMotion? TraversalMotion { get; init; }
    // The spin bridge planner checks the 34775 blades itself, at their impact
    // instants; the tracker's wide windows must not veto its route.
    public bool SpinRowsOwned { get; init; }
    // Likewise the slime ramp planner and the 34774 pushers on the final ramp.
    public bool RampOwned { get; init; }
    public Spatial.Geometry.IGeometryShape? RecoveryFloor { get; init; }
    public bool BelowDeliveryMechanisms { get; init; }
}

internal sealed class BlundervilleRacePlanner
{
    // The hub pickup is an EventRange that fires 3.15–3.90m from its centre
    // (21 Delivery rounds); 1.3m walked an extra ~2m every pickup.
    internal const float PickupRadius = 2.6f;
    private IReadOnlyList<BlundervilleRouteNode> _nodes = [];
    private long _version = -1;
    private int _cursor;
    private DateTime _arrival;
    private Vector3? _arrivalTarget;
    private Vector3? _previous;
    private Vector3? _deliveryTarget;
    private bool _recovering;
    private int _recoveryNodeCount;
    private DateTime _forcedExpiry;
    private Vector3 _forcedPosition;
    private (float Heading, bool Safe) _forcedHeading;
    private float? _forcedFacing;
    private bool _rejoinCircles;
    private readonly BlundervilleDeliveryRecovery _deliveryRecovery = new();
    public int Cursor => _cursor;
    public bool Recovering => _recovering;
    public BlundervilleRecoveryCorridor? RecoveryCorridor { get; private set; }
    private BlundervilleFinalRouteSettings? _finalRoutes;
    // First/second round lane graphs. The script refreshes the source and the
    // traversal forecast every scan; a fork node re-picks its branch on arrival.
    internal Func<BlundervilleCourse, BlundervilleCourseGraph.Compiled?>? CourseGraphs { get; set; } = course => BlundervilleCourseGraphs.Assigned(null, course);
    internal IReadOnlyList<BlundervilleHazard> Hazards { get; set; } = [];
    private BlundervilleCourseGraph.Compiled? _graph;
    private List<int>? _routeIds;
    private int _forkCursor = -1;
    internal string RouteDescription => _routeIds == null ? "" : string.Join("→", _routeIds);

    internal void ConfigureFinalRoutes(BlundervilleFinalRouteSettings settings)
    {
        if (ReferenceEquals(_finalRoutes, settings)) return;
        _finalRoutes = settings; Reset();
    }

    private IReadOnlyList<BlundervilleRouteNode> CourseNodes(BlundervilleCourse course, Vector3 position, DateTime now = default, float speed = 6)
    {
        if (course == BlundervilleCourse.Final && _finalRoutes != null) return _finalRoutes.Nodes(position);
        _graph = course is BlundervilleCourse.FirstA or BlundervilleCourse.FirstB or BlundervilleCourse.Ring ? CourseGraphs?.Invoke(course) : null;
        _routeIds = null; _forkCursor = -1;
        if (_graph == null) return BlundervilleRoutes.Nodes(course, position);
        var from = course == BlundervilleCourse.Ring ? _graph.Start : BlundervilleCourseRouter.Nearest(_graph, position);
        // A shared start-line node only offers the lanes; the spawn slot fixes
        // the side (as the fixed routes did), so start on the nearer lane.
        if (course != BlundervilleCourse.Ring && from == _graph.Start && _graph.Next[from].Length > 0)
            from = _graph.Next[from].MinBy(id => Vector2.DistanceSquared(new(position.X, position.Z),
                new(_graph.Nodes[id].Position.X, _graph.Nodes[id].Position.Z)));
        _routeIds = BlundervilleCourseRouter.Route(_graph, from, Hazards, now, speed);
        return _routeIds.Select(id => _graph.Nodes[id]).ToArray();
    }

    private IReadOnlyList<BlundervilleRouteNode> DeliveryRoute(Vector3 position, Vector3 destination, DateTime now, float speed, bool pickup)
    {
        if (CourseGraphs?.Invoke(BlundervilleCourse.Delivery) is not { } graph)
            return pickup ? BlundervilleRoutes.DeliveryNodes(position, destination).Reverse().ToArray() : BlundervilleRoutes.DeliveryNodes(position, destination);
        var ids = BlundervilleCourseRouter.Route(graph, BlundervilleCourseRouter.Nearest(graph, position), Hazards, now, speed, destination);
        var nodes = ids.Select(id => graph.Nodes[id]).ToList();
        if (!pickup && Vector3.Distance(nodes[^1].Position, destination) > .05f) nodes.Add(new(destination, 1.4f));
        return nodes;
    }

    public BlundervilleRaceDecision Update(BlundervilleRaceState state, DateTime now, Vector3 position,
        bool grounded, bool jumping, bool gapAhead, float? arrowDirection, bool nativeMisdirection = false, Vector3? executionTarget = null)
    {
        if (!state.IsRacing || state.Qualified || !BlundervilleEncounter.Finite(position))
        {
            Reset();
            return new(null, true, false, null, state.Qualified ? "已晋级，等待下一关" : "等待起跑");
        }
        if (_version != state.RoundVersion || _previous is { } previous && Vector3.DistanceSquared(previous, position) > 40 * 40)
        {
            _deliveryRecovery.Reset();
            _nodes = CourseNodes(state.Course, position, now, state.NavigationBaseSpeed(now));
            _version = state.RoundVersion;
            _cursor = NearestSegment(position, BlundervilleRoutes.IsLoop(state.Course));
            _arrival = default; _arrivalTarget = null;
            _deliveryTarget = null; _recovering = false; RecoveryCorridor = null;
            _forcedExpiry = default;
            _forcedFacing = null;
            _rejoinCircles = false;
        }
        _previous = position;
        var onCircles = state.Course == BlundervilleCourse.FirstB && position.Y is >= 9.8f and <= 10.8f
            && position.Z is < -194 and > -250;
        if (onCircles && state.HasStatus(149, now)) _rejoinCircles = true;
        if (state.Course == BlundervilleCourse.FirstA && position.Y < 12
            && position.X is > -224 and < -176 && position.Z is > -355 and < -322)
            return new(null, true, false, null, "已跌落消失桥，等待回到桥面或手动恢复");
        if (_recovering && grounded && !jumping && position.Y >= 9.8f
            && MathF.Abs(position.X) <= 8 && position.Z is >= -270 and <= -266)
            _cursor = Math.Max(_cursor, _recoveryNodeCount - 2);
        if (_recovering && grounded && !jumping && position.Y >= 9.8f
            && MathF.Abs(position.X) <= 5.8f && position.Z is >= -270 and <= -266)
        {
            _recovering = false; RecoveryCorridor = null;
            _cursor = _recoveryNodeCount - 1;
        }
        Vector3? target;
        if (state.Course == BlundervilleCourse.Delivery)
        {
            BlundervilleDeliverySearch.Prepare();
            if (_deliveryRecovery.Apply(state, position, now, grounded, jumping) is { } recovery)
            {
                _deliveryTarget = null;
                return recovery;
            }
            var finalTarget = state.Score >= 3 ? null : state.CarryCrystalParam == 0
                ? (Vector3?)BlundervilleRaceState.PickupCenter : state.DeliveryTarget;
            if (finalTarget is { } delivery)
            {
                if (_deliveryTarget != delivery)
                {
                    _nodes = state.CarryCrystalParam == 0 && _deliveryTarget is { } previousDelivery
                        ? DeliveryRoute(position, previousDelivery, now, state.NavigationBaseSpeed(now), true)
                        : state.CarryCrystalParam == 0 ? [new(delivery, PickupRadius)] : DeliveryRoute(position, delivery, now, state.NavigationBaseSpeed(now), false);
                    _deliveryTarget = delivery; _cursor = NearestSegment(position);
                }
                while (_cursor + 1 < _nodes.Count && Reached(position, _cursor)) _cursor++;
                target = _cursor + 1 < _nodes.Count ? _nodes[_cursor].Position : delivery;
            }
            else target = null;
        }
        else
        {
            if (state.Course == BlundervilleCourse.FirstB && (position.Y < 9.8f
                    || position.Y < 10.1f && MathF.Abs(position.X) > 6.5f && position.Z is > -270 and < -266)
                && position.Z is < -263 and > -337 && !_recovering)
            {
                if (!grounded || jumping)
                    return new(null, true, false, null, "等待击飞落地，重新确认回坡起点");
                var normal = CourseNodes(state.Course, position);
                var recovery = BlundervilleRoutes.FirstBRecovery(position);
                _recoveryNodeCount = recovery.Count;
                _nodes = recovery.Concat(normal.Where(n => n.Position.Z < -271)).ToArray();
                RecoveryCorridor = new([position, .. recovery.Select(n => n.Position)], 1);
                _cursor = 0; _recovering = true;
            }
            // 20:32 ARR: circle knockbacks crossed the centre and later pushed
            // backwards around the diamond. Rejoin the actual landing, including
            // its side and progress, once stun/airborne movement has ended. The
            // normal 2.5m forward-only rejoin cannot reach the opposite lane.
            if (onCircles && !_recovering && grounded && !jumping && state.CanMove(now)
                && (_rejoinCircles || MathF.Abs(position.X) > 2 && _nodes.Count > 10
                    && MathF.Sign(position.X) != MathF.Sign(_nodes[10].Position.X)))
            {
                _nodes = CourseNodes(state.Course, position);
                _cursor = NearestSegment(position);
                _rejoinCircles = false;
            }
            if (!onCircles && grounded && !jumping && state.CanMove(now)) _rejoinCircles = false;
            // The Final route side is fixed by the start x, but the Final lane
            // planner picks either stair (x=±4) by its hazard timing. 01:39 ARR:
            // start x=0.47 (right route), hand-over at the left run-up
            // (-3.9,155.3); the 2.5m rejoin never matched, the cursor stayed at
            // node 1 (z=278), the jump planner saw a target behind it and the
            // player shuttled across the planner boundary for 25s. Follow the
            // side the player committed to (mirrored route, nearest segment).
            if (state.Course == BlundervilleCourse.Final && _finalRoutes == null && !_recovering && _nodes.Count > 1
                && position.Z <= BlundervilleFinalPlanner.Bottom + 1
                && MathF.Abs(position.X) > 2 && MathF.Sign(position.X) != MathF.Sign(_nodes[^1].Position.X))
            {
                _nodes = CourseNodes(state.Course, position);
                _cursor = NearestSegment(position);
            }
            if (!_recovering && grounded && !jumping && !BlundervilleRoutes.IsLoop(state.Course))
                RejoinForwardSegment(position);
            while (_cursor < _nodes.Count && (!_recovering || grounded && !jumping) && Reached(position, _cursor))
            {
                if (_cursor == _nodes.Count - 1 && BlundervilleRoutes.IsLoop(state.Course))
                {
                    if (_graph != null && _routeIds != null)
                    {
                        _routeIds = BlundervilleCourseRouter.Route(_graph, _graph.Start, Hazards, now, state.NavigationBaseSpeed(now));
                        _nodes = _routeIds.Select(id => _graph.Nodes[id]).ToArray(); _forkCursor = -1;
                    }
                    _cursor = 0; break;
                }
                if (_cursor == _nodes.Count - 1) break;
                _cursor++;
            }
            // Reaching a fork re-picks the branch with the current forecast.
            if (_graph != null && _routeIds != null && !_recovering && _cursor > 0 && _cursor < _routeIds.Count
                && _forkCursor != _cursor && _graph.Fork(_routeIds[_cursor - 1]))
            {
                _forkCursor = _cursor;
                var fork = _routeIds[_cursor - 1];
                var branch = BlundervilleCourseRouter.Route(_graph, fork, Hazards, now, state.NavigationBaseSpeed(now));
                if (branch.Count > 1 && branch[1] != _routeIds[_cursor])
                {
                    _routeIds = [.. _routeIds.Take(_cursor - 1), .. branch];
                    _nodes = _routeIds.Select(id => _graph.Nodes[id]).ToArray();
                }
            }
            target = _cursor < _nodes.Count ? _nodes[_cursor].Position : null;
        }
        if (target is not { } destination)
            return new(null, true, false, null, state.Course == BlundervilleCourse.Delivery && state.CarryCrystalParam != 0
                ? $"未识别的水晶参数 {state.CarryCrystalParam}" : "等待通关确认");

        var distance = DistanceXZ(position, destination);
        if (state.Course == BlundervilleCourse.Delivery && _cursor == _nodes.Count - 1
            && distance <= (state.CarryCrystalParam == 0 ? PickupRadius : .25f))
        {
            if (_arrivalTarget != destination) { _arrivalTarget = destination; _arrival = now; }
            // Pickup and delivery are automatic EventRange triggers; status/score confirms success.
            return new(destination, true, false, null, state.CarryCrystalParam == 0 ? "中央领取水晶，等待状态确认" : "对应投放区，等待得分确认");
        }
        _arrival = default; _arrivalTarget = null;
        var desired = MathF.Atan2(destination.X - position.X, destination.Z - position.Z);
        if (state.IsForcedMoving) _forcedFacing = null;
        else if (state.ForcedMoveExpiresAt is { } expires && state.ForcedDirectionOffset is { } offset
            && expires != DateTime.MaxValue && expires - now <= TimeSpan.FromSeconds(0.8) && expires >= now)
        {
            if (_forcedExpiry != expires || Vector3.DistanceSquared(_forcedPosition, position) > .25f * .25f)
            {
                _forcedExpiry = expires; _forcedPosition = position;
                _forcedHeading = BlundervilleForcedMovement.Heading(BlundervilleRoutes.Boundary(state.Course), position, destination);
            }
            _forcedFacing = MathF.IEEERemainder(_forcedHeading.Heading - offset, MathF.Tau);
        }
        // The local countdown can end before the server replaces it with 3737.
        // Keep the committed facing through that gap; release on confirmation or
        // a bounded timeout if the command was cancelled or its signal was lost.
        if (_forcedFacing is { } facing && now <= _forcedExpiry.AddSeconds(.75))
            return new(destination, true, false, facing,
                _forcedHeading.Safe ? "沿完整安全直线对齐强制移动" : "强制移动准备过晚：朝最长地板方向，仍有跌落风险");
        _forcedFacing = null;
        var canMove = state.CanMoveToward(position, destination, arrowDirection, now);
        var hint = state.MustStopForAccelerationBomb(now) ? "加速度炸弹：停步"
            : state.IsForcedMoving ? "等待强制移动结束"
            : nativeMisdirection && state.HasMisdirection ? "箭头模式：按安全路线持续前进"
            : state.HasMisdirection && !canMove ? "等待头顶箭头朝向路线"
            : state.IsSlidingSlope ? "湿滑坡面：沿路线前进"
            : _recovering ? "掉桥恢复：沿外侧到坡口，横向上坡"
            : state.IsSpinning ? "旋转移动：沿桥面转向" : "沿路线竞速";
        // A missing floor probe is also a cliff, a pillar or a rising slope.
        // Only JumpPlanner/BridgePlanner may authorize a measured takeoff/landing.
        const bool jump = false;
        var hold = nativeMisdirection && state.HasMisdirection ? !state.CanMove(now) : !canMove;
        return new(destination, hold, jump, null, hint);
    }

    public void Reset()
    {
        _deliveryRecovery.Reset();
        _forcedExpiry = default;
        _forcedFacing = null;
        _rejoinCircles = false;
        _nodes = []; _version = -1; _cursor = 0; _arrival = default; _arrivalTarget = null; _previous = null;
        _graph = null; _routeIds = null; _forkCursor = -1;
        _deliveryTarget = null; _recovering = false; _recoveryNodeCount = 0; RecoveryCorridor = null;
    }

    private int NearestSegment(Vector3 position, bool loop = false)
    {
        if (_nodes.Count < 2) return 0;
        var closest = float.MaxValue;
        var result = 0;
        for (var i = 0; i < (loop ? _nodes.Count : _nodes.Count - 1); i++)
        {
            var next = (i + 1) % _nodes.Count;
            var a = _nodes[i].Position; var b = _nodes[next].Position;
            var ab = new Vector2(b.X - a.X, b.Z - a.Z);
            var ap = new Vector2(position.X - a.X, position.Z - a.Z);
            var t = ab.LengthSquared() > 0.0001f ? Math.Clamp(Vector2.Dot(ap, ab) / ab.LengthSquared(), 0, 1) : 0;
            var p = Vector3.Lerp(a, b, t);
            var score = Vector3.DistanceSquared(position, p);
            if (score < closest) { closest = score; result = t <= 0.02f ? i : next; }
        }
        return result;
    }

    private bool Reached(Vector3 position, int index)
    {
        var node = _nodes[index];
        // Recovery corners must actually be reached; the ordinary 4m pass-through
        // rule cuts across the ramp's vertical side, especially under the speed buff.
        if (_recovering)
            return MathF.Abs(position.Y - node.Position.Y) <= .7f && DistanceXZ(position, node.Position) <= node.ArrivalRadius;
        if (MathF.Abs(position.Y - node.Position.Y) > 3) return false;
        if (node.Position.Z is <= -319 and >= -354 && node.Position.X is < -176 and > -224 && position.Z <= node.Position.Z) return true;
        if (DistanceXZ(position, node.Position) <= node.ArrivalRadius) return true;
        if (index == 0) return false;
        var ab = node.Position - _nodes[index - 1].Position; ab.Y = 0;
        var bp = position - node.Position; bp.Y = 0;
        return Vector3.Dot(bp, ab) >= 0 && DistanceXZ(position, node.Position) < (node.Jump ? 6 : 4);
    }

    private void RejoinForwardSegment(Vector3 position)
    {
        // A jump, forced movement or another traversal controller can pass
        // several nodes without entering their small arrival circles. Use the
        // floor the player actually reached, never a desired future target.
        var closest = 2.5f * 2.5f;
        for (var i = _cursor; i + 1 < _nodes.Count; ++i)
        {
            var a = _nodes[i].Position; var b = _nodes[i + 1].Position;
            var ab = new Vector2(b.X - a.X, b.Z - a.Z);
            if (ab.LengthSquared() < .001f) continue;
            var t = Vector2.Dot(new(position.X - a.X, position.Z - a.Z), ab) / ab.LengthSquared();
            if (t < .05f || t > 1.05f) continue;
            var projected = Vector3.Lerp(a, b, Math.Clamp(t, 0, 1));
            if (MathF.Abs(projected.Y - position.Y) > .8f) continue;
            var distance = Vector2.DistanceSquared(new(position.X, position.Z), new(projected.X, projected.Z));
            if (distance >= closest) continue;
            closest = distance; _cursor = i + 1;
        }
    }

    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
