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
    public int Cursor => _cursor;
    public bool Recovering => _recovering;
    public BlundervilleRecoveryCorridor? RecoveryCorridor { get; private set; }

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
            _nodes = BlundervilleRoutes.Nodes(state.Course, position);
            _version = state.RoundVersion;
            _cursor = NearestSegment(position);
            _arrival = default; _arrivalTarget = null;
            _deliveryTarget = null; _recovering = false; RecoveryCorridor = null;
            _forcedExpiry = default;
            _forcedFacing = null;
        }
        _previous = position;
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
            var finalTarget = state.Score >= 3 ? null : state.CarryCrystalParam == 0
                ? (Vector3?)BlundervilleRaceState.PickupCenter : state.DeliveryTarget;
            if (finalTarget is { } delivery)
            {
                if (_deliveryTarget != delivery)
                {
                    _nodes = state.CarryCrystalParam == 0 && _deliveryTarget is { } previousDelivery
                        ? BlundervilleRoutes.DeliveryNodes(position, previousDelivery).Reverse().ToArray()
                        : state.CarryCrystalParam == 0 ? [new(delivery, PickupRadius)] : BlundervilleRoutes.DeliveryNodes(position, delivery);
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
                var normal = BlundervilleRoutes.Nodes(state.Course, position);
                var recovery = BlundervilleRoutes.FirstBRecovery(position);
                _recoveryNodeCount = recovery.Count;
                _nodes = recovery.Concat(normal.Where(n => n.Position.Z < -271)).ToArray();
                RecoveryCorridor = new([position, .. recovery.Select(n => n.Position)], 1);
                _cursor = 0; _recovering = true;
            }
            // The Final route side is fixed by the start x, but the Final lane
            // planner picks either stair (x=±4) by its hazard timing. 01:39 ARR:
            // start x=0.47 (right route), hand-over at the left run-up
            // (-3.9,155.3); the 2.5m rejoin never matched, the cursor stayed at
            // node 1 (z=278), the jump planner saw a target behind it and the
            // player shuttled across the planner boundary for 25s. Follow the
            // side the player committed to (mirrored route, nearest segment).
            if (state.Course == BlundervilleCourse.Final && !_recovering && _nodes.Count > 1
                && MathF.Abs(position.X) > 2 && MathF.Sign(position.X) != MathF.Sign(_nodes[1].Position.X))
            {
                _nodes = BlundervilleRoutes.Nodes(state.Course, position);
                _cursor = NearestSegment(position);
            }
            if (!_recovering && grounded && !jumping && !BlundervilleRoutes.IsLoop(state.Course))
                RejoinForwardSegment(position);
            while (_cursor < _nodes.Count && (!_recovering || grounded && !jumping) && Reached(position, _cursor))
            {
                if (_cursor == _nodes.Count - 1 && BlundervilleRoutes.IsLoop(state.Course)) { _cursor = 0; break; }
                if (_cursor == _nodes.Count - 1) break;
                _cursor++;
            }
            target = _cursor < _nodes.Count ? _nodes[_cursor].Position : null;
        }
        if (target is not { } destination)
            return new(null, true, false, null, state.Course == BlundervilleCourse.Delivery && state.CarryCrystalParam != 0
                ? $"未识别的水晶参数 {state.CarryCrystalParam}" : "等待通关确认");

        var distance = DistanceXZ(position, destination);
        if (state.Course == BlundervilleCourse.Delivery && _cursor == _nodes.Count - 1
            && distance <= (state.CarryCrystalParam == 0 ? PickupRadius : 1.3f))
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
        _forcedExpiry = default;
        _forcedFacing = null;
        _nodes = []; _version = -1; _cursor = 0; _arrival = default; _arrivalTarget = null; _previous = null;
        _deliveryTarget = null; _recovering = false; _recoveryNodeCount = 0; RecoveryCorridor = null;
    }

    private int NearestSegment(Vector3 position)
    {
        if (_nodes.Count < 2) return 0;
        var closest = float.MaxValue;
        var result = 0;
        for (var i = 0; i + 1 < _nodes.Count; i++)
        {
            var a = _nodes[i].Position; var b = _nodes[i + 1].Position;
            var ab = new Vector2(b.X - a.X, b.Z - a.Z);
            var ap = new Vector2(position.X - a.X, position.Z - a.Z);
            var t = ab.LengthSquared() > 0.0001f ? Math.Clamp(Vector2.Dot(ap, ab) / ab.LengthSquared(), 0, 1) : 0;
            var p = Vector3.Lerp(a, b, t);
            var score = Vector3.DistanceSquared(position, p);
            if (score < closest) { closest = score; result = t <= 0.02f ? i : i + 1; }
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
