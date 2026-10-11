using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleBridgeDecision(Vector3 Target, bool Hold, bool Jump, IGeometryShape? CrossingCorridor, string Hint);

internal sealed class BlundervilleBridgePlanner
{
    private static readonly float[] Centers = [-210.523f, -203.5f, -196.5f, -189.5f];
    private const float Floor = 13.565f;
    private const float StartZ = -322.928f;
    private const float EndZ = -353.072f;
    // 1.6m was tried for more run-up margin but lands short from the outer
    // lane 14 (01:01 replay); keep the measured 2.0m edge takeoff.
    internal const float SwitchTakeoffOffset = 2.0f;
    private Vector3? _landing;
    private IGeometryShape? _crossing;
    private int _landingLane = -1;
    private bool _airborne;
    private DateTime _lastTime;
    private Vector3? _previous;
    private int _switchLane = -1;
    private int _switchOriginLane = -1;
    private Vector3? _takeoff;
    private Vector3? _preparedLanding;

    public BlundervilleBridgeDecision? Plan(BlundervilleBridgeTracker tracker, DateTime now, Vector3 player,
        Vector3 forwardTarget, bool grounded, bool jumping)
    {
        if (!Finite(player) || !Finite(forwardTarget)) { Reset(); return null; }
        if (_lastTime != default && now < _lastTime || _previous is { } old && Vector3.DistanceSquared(old, player) > 20 * 20) Reset();
        _lastTime = now;
        _previous = player;
        if (player.Y < 12.8f || player.X is < -215 or > -185 || player.Z is > -317 or < -357)
        {
            Reset();
            return null;
        }
        if (_landing is { } landing)
        {
            _airborne |= !grounded || jumping;
            var onDestination = _landingLane < 0 ? player.Z < EndZ - 0.25f
                : MathF.Abs(player.X - Centers[_landingLane]) < 2.1f && DistanceXZ(player, landing) < 1.6f;
            if (_airborne && grounded && !jumping && onDestination)
            {
                _landing = null; _airborne = false; _landingLane = -1;
                ClearPreparation();
            }
            else return new(landing, false, grounded && !jumping && !_airborne, _crossing, "保持跳跃落点，等待着地");
        }
        if (player.Z < EndZ - 0.25f) { Reset(); return null; }
        var lane = NearestLane(player.X);
        if (player.Z >= StartZ + 0.2f)
        {
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < Centers.Length; i++)
            {
                var index = (byte)(14 + i);
                if (!tracker.BridgeEnabled(index, now) || tracker.BridgeNextDisappearAt(index, now) is not { } closes) continue;
                var approach = DistanceXZ(player, new(Centers[i], Floor, StartZ - 0.8f)) / 6;
                var remaining = (float)(closes - now).TotalSeconds;
                if (remaining < approach + 1.3f) continue;
                var score = remaining - approach;
                if (score > bestScore) { best = i; bestScore = score; }
            }
            if (best < 0)
                return new(new(player.X, Floor, Math.Max(player.Z, StartZ + 1)), true, false, null, "等待桥面出现并校准周期");
            if (MathF.Abs(player.X - Centers[best]) > 0.6f)
                return new(new(Centers[best], Floor, Math.Max(player.Z, StartZ + 1.2f)), false, false, null, "在入口平台对齐通行桥面");
            return new(new(Centers[best], Floor, StartZ - 1), false, false, null, "选择通行时间充足的桥面");
        }
        var currentIndex = (byte)(14 + lane);
        var deadline = tracker.BridgeNextDisappearAt(currentIndex, now);
        var remainingSeconds = deadline is { } close ? (float)(close - now).TotalSeconds : 0;
        var exitSeconds = Math.Max(0, player.Z - EndZ) / 6;
        // Prepare the straight finish jump four metres early: the run-up is the
        // walk itself, and 1.5m left no time for one scan plus route dispatch.
        if (player.Z <= -347.5f && grounded && !jumping)
            return BeginJump(new(player.X, Floor, -351f), new(player.X, Floor, -355.2f), -1, "助跑后跳上终点平台");
        if (_switchLane >= 0 && !tracker.BridgeEnabled((byte)(14 + _switchLane), now)) ClearPreparation();
        if (_switchLane < 0 && (remainingSeconds < 2.2f && exitSeconds + 0.2f > remainingSeconds || !tracker.BridgeEnabled(currentIndex, now)))
        {
            var bestRemaining = 0.8f;
            foreach (var adjacent in new[] { lane - 1, lane + 1 })
            {
                if (adjacent is < 0 or > 3 || !tracker.BridgeEnabled((byte)(14 + adjacent), now)
                    || tracker.BridgeNextDisappearAt((byte)(14 + adjacent), now) is not { } adjacentClose) continue;
                var candidate = (float)(adjacentClose - now).TotalSeconds;
                if (candidate > bestRemaining) { _switchLane = adjacent; bestRemaining = candidate; }
            }
            if (_switchLane >= 0)
            {
                _switchOriginLane = lane;
                var direction = MathF.Sign(Centers[_switchLane] - Centers[lane]);
                _takeoff = new(Centers[lane] + direction * SwitchTakeoffOffset, Floor, Math.Max(EndZ - 0.2f, player.Z - 0.8f));
                _preparedLanding = new(Centers[_switchLane] - direction * 1.65f, Floor, Math.Max(EndZ - 0.4f, player.Z - 2.2f));
                _crossing = new BlundervilleJumpCorridor(_takeoff.Value, _preparedLanding.Value, 3f, 1.2f) { LaneSwitch = true };
            }
        }
        // The executor launches a lane switch from wherever its run-up crosses
        // the takeoff band, which can precede this planner's own edge check.
        if (_switchLane >= 0 && _preparedLanding is { } launched && (!grounded || jumping))
        {
            var flight = BeginJump(player, launched, _switchLane, "保持跳跃落点，等待着地");
            _airborne = true;
            return flight with { Jump = false };
        }
        if (_switchLane >= 0 && grounded && !jumping && lane == _switchLane) ClearPreparation();
        if (_switchLane >= 0 && _takeoff is { } takeoff && _preparedLanding is { } preparedLanding)
        {
            var direction = MathF.Sign(Centers[_switchLane] - Centers[_switchOriginLane]);
            if (grounded && !jumping && direction * (player.X - takeoff.X) >= -0.3f)
            {
                return BeginJump(player, preparedLanding, _switchLane, "跳过间隙，切换到相邻桥面");
            }
            return new(takeoff, false, false, _crossing, "桥面即将消失，靠近换道边缘");
        }
        if (!tracker.BridgeEnabled(currentIndex, now))
            return new(player, true, false, null, "桥面已消失，等待有效落点");
        return new(new(Centers[lane], Floor, EndZ + 1.2f), false, false, null, "沿当前桥面前进");
    }

    public void Reset()
    {
        _landing = null; _landingLane = -1; _airborne = false;
        _lastTime = default; _previous = null;
        ClearPreparation();
    }

    private BlundervilleBridgeDecision BeginJump(Vector3 from, Vector3 landing, int lane, string hint)
    {
        _landing = landing;
        _landingLane = lane;
        _airborne = false;
        if (_crossing == null || _preparedLanding != landing) _crossing = Crossing(from, landing);
        return new(landing, false, true, _crossing, hint);
    }

    private void ClearPreparation()
    {
        _crossing = null; _switchLane = -1; _switchOriginLane = -1; _takeoff = null; _preparedLanding = null;
    }

    private static IGeometryShape Crossing(Vector3 from, Vector3 landing)
        => new BlundervilleJumpCorridor(from, landing, 2.2f, 1.2f);

    private static int NearestLane(float x)
    {
        var best = 0;
        for (var i = 1; i < Centers.Length; i++) if (MathF.Abs(x - Centers[i]) < MathF.Abs(x - Centers[best])) best = i;
        return best;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
