using System.Numerics;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleNavigation(SafeZoneRuntime runtime, ScriptSafeZoneApi safeZone, ScriptDrawApi draw)
{
    public bool HasMovementControl => runtime.MovementStatus.HasControl;
    public DirectTraversalExecution GetDirectTraversalExecution(DateTime now) => runtime.GetDirectTraversalExecution(this, now);
    public bool CommitJump(Vector3 landing, Vector3 player, float speed, DateTime now) => runtime.CommitJumpMovement(this, landing, player, speed, now);
    public void ReleaseJump() => runtime.ReleaseCommittedMovement(this);
    public void StopForRunup() => runtime.StopOwnedMovement(this);
    private sealed record Registered(BlundervilleHazard Hazard, SafeZoneAreaHandle Handle);
    private readonly Dictionary<string, Registered> _dangers = [];
    private SafeZoneAreaHandle _goal;
    private Vector3? _target;
    private IGeometryShape? _boundary;
    private IGeometryShape? _sourceBoundary;
    private BlundervilleCourse _course;
    private Vector3 _regionCenter;
    private IGeometryShape? _crossing;
    private float _goalRadius;
    private bool _preciseRoute;

    public void Apply(BlundervilleRaceState state, BlundervilleRaceDecision decision, BlundervilleEncounter encounter,
        DateTime now, Vector3 player, bool automatic, bool replay, bool showRoute, IGeometryShape? crossing = null)
    {
        if (!state.IsRacing || state.Qualified || state.Course == BlundervilleCourse.None)
        {
            Clear(); return;
        }
        var recovery = crossing is BlundervilleRecoveryCorridor;
        var preciseRoute = decision.DirectTraversal || decision.RecoveryFloor != null || state.Course == BlundervilleCourse.FirstB && player.Y >= 8.5f
                && (player.Z is < -118 and > -158 || decision.Target is { Z: < -118 and > -158 })
            || state.Course == BlundervilleCourse.Ring && (player.Z < -137 || decision.Target is { Z: < -137 })
            || state.Course == BlundervilleCourse.Final && player.Y >= 35 && (player.Z < 129 || decision.Target is { Z: < 129 });
        var delivery = state.Course == BlundervilleCourse.Delivery;
        // Ring uses the same .3m player footprint as Bean's physical hazards;
        // the course-edge clearance remains independent.
        float? hardMargin = state.Course switch
        {
            BlundervilleCourse.Delivery => .1f,
            BlundervilleCourse.Ring => .3f,
            _ => null
        };
        runtime.RequestAutomaticMovement(this, automatic && !replay, decision.Hold, state.NavigationBaseSpeed(now),
            preciseRoute || delivery ? 0.1f : null, preferStraightPaths: true, hardSafetyMargin: hardMargin);
        using var group = safeZone.BeginDrawingGroup("竞速路线与机制处理");
        if (decision.Target is { } target && showRoute)
        {
            draw.AddLinePreview("race/route", player, target, true);
            draw.AddAoePreview("race/goal", new CircleGeometry(target, 0.8f), true, true, 1);
        }
        else { draw.Remove("race/route"); draw.Remove("race/goal"); }
        if (!automatic && !replay) { ClearAreas(); return; }

        var middle = decision.Target is { } destinationPoint ? (destinationPoint + player) * 0.5f : player;
        var center = new Vector3(MathF.Round(middle.X / 10) * 10, player.Y, MathF.Round(middle.Z / 10) * 10);
        var tightCrossing = crossing != null && state.Course == BlundervilleCourse.FirstA || crossing is BlundervilleJumpCorridor;
        var source = decision.RecoveryFloor ?? BlundervilleRoutes.NavigationBoundary(state.Course, player);
        if (_boundary == null || !ReferenceEquals(_sourceBoundary, source) || _course != state.Course || DistanceXZ(center, _regionCenter) > 0.1f || !ReferenceEquals(_crossing, crossing) || _preciseRoute != preciseRoute)
        {
            _course = state.Course; _regionCenter = center; _crossing = crossing; _preciseRoute = preciseRoute;
            _sourceBoundary = source;
            // Lower-floor recovery must not union in the high bridge: in X/Z that
            // would authorize shortcuts through its wall before reaching the ramp.
            _boundary = new LocalCorridor(recovery ? crossing! : crossing == null ? source : new CrossingCorridor(source, crossing), center,
                crossing is BlundervilleJumpCorridor { Stairs: true } ? 16 : tightCrossing ? 8 : preciseRoute ? 16 : 28);
            runtime.Manager.SetArenaBoundary(_boundary);
        }
        var closeWaypoints = state.Course == BlundervilleCourse.FirstB && player.Z is < -194 and > -336;
        var goalRadius = tightCrossing || preciseRoute || recovery || closeWaypoints || decision.DirectTraversal ? 0.2f : 0.8f;
        if (decision.Target != _target || _goalRadius != goalRadius || _goal.IsValid && !runtime.Manager.ContainsArea(_goal))
        {
            _target = decision.Target; _goalRadius = goalRadius;
            if (_target is { } destination)
            {
                if (!_goal.IsValid || !runtime.Manager.UpdateArea(_goal, new CircleTemplate(goalRadius), new StaticTransform(destination, 0)))
                {
                    _goal = safeZone.AddPersistentSafeArea(new CircleTemplate(goalRadius), new StaticTransform(destination, 0), 100, PositionPreference.Center);
                    safeZone.SetNavigationOnly(_goal, true);
                }
            }
            else { if (_goal.IsValid) safeZone.Remove(_goal); _goal = default; }
        }
        // Charger body zones only steer the Delivery corridor plan (soft).
        var desired = encounter.TraversalHazards(now, !decision.SpinRowsOwned, !decision.RampOwned)
            .Select(h => CircleImmunityWindow(h, state, player, now))
            .Where(h => h.From <= h.Until)
            .Where(h => !(delivery && decision.BelowDeliveryMechanisms && h.Geometry.Position.Y > player.Y + 1.5f)
                && !h.Id.StartsWith(BlundervilleDeliverySchedule.BodyPrefix, StringComparison.Ordinal)
                && Radar.RadarLocalGeometry.Intersects(h.Geometry.GetBounds(), player, 48)).ToDictionary(h => h.Id);
        foreach (var id in _dangers.Keys.Where(id => !desired.ContainsKey(id)).ToArray())
        { safeZone.Remove(_dangers[id].Handle); _dangers.Remove(id); }
        foreach (var (id, hazard) in desired)
        {
            if (_dangers.TryGetValue(id, out var existing) && existing.Hazard == hazard) continue;
            var template = Template(hazard.Geometry);
            if (template == null) continue;
            var binding = new StaticTransform(hazard.Geometry.Position, hazard.Geometry.Rotation);
            DangerWindow[] windows = [new(hazard.From, hazard.Until == DateTime.MaxValue ? null : hazard.Until, hazard.Telegraph)
                { HasMotionEnvelope = hazard.HasMotionEnvelope || hazard.Id.StartsWith("bumper/", StringComparison.Ordinal)
                    || hazard.Id.StartsWith("delivery/", StringComparison.Ordinal)
                    || hazard.Id.StartsWith("charge/", StringComparison.Ordinal)
                    || hazard.Id.StartsWith("rhythm/", StringComparison.Ordinal)
                        && (hazard.Id.Contains("/34717/", StringComparison.Ordinal) || hazard.Id.Contains("/34773/", StringComparison.Ordinal)
                            || hazard.Id.Contains("/34799/", StringComparison.Ordinal)) }];
            if (existing != null)
            {
                var dynamic = id.StartsWith("ball/", StringComparison.Ordinal) || id.StartsWith("beam/", StringComparison.Ordinal)
                    || id.StartsWith("bumper/", StringComparison.Ordinal) || id.StartsWith("bumper-unknown/", StringComparison.Ordinal);
                if (runtime.Manager.UpdateArea(existing.Handle, template, binding, windows, incrementVersion: !dynamic))
                { _dangers[id] = new(hazard, existing.Handle); continue; }
                safeZone.Remove(existing.Handle); _dangers.Remove(id);
            }
            // Forecast geometry already includes body uncertainty; the solver
            // applies its own clearance. Add/update must use identical geometry.
            var handle = safeZone.AddExactDangerSnapshot(template, binding, windows);
            safeZone.SetNavigationOnly(handle, true);
            _dangers[id] = new(hazard, handle);
        }
        // A running jump needs its exact landing route to acquire takeoff.
        // A generic escape goal can strand Ring jumps on an island while the
        // beam approaches. Validate the full straight run with the existing
        // timed traversal checks; the native takeoff/landing gate still owns flight.
        var jumpRun = crossing is BlundervilleJumpCorridor jump && decision.Target == jump.MovementTarget
            && (jump.Stairs || state.Course == BlundervilleCourse.Ring);
        runtime.RequestDirectTraversal(this, automatic && !replay && (decision.DirectTraversal || jumpRun) ? decision.Target : null, now,
            decision.ContinuousPath, decision.HeadingTolerance, decision.TraversalMotion);
    }

    public void Clear()
    {
        SuspendMovement();
        ClearAreas();
        draw.Remove("race/route"); draw.Remove("race/goal");
    }

    public void SuspendMovement()
    {
        runtime.RequestDirectTraversal(this, null, runtime.CurrentTime);
        runtime.RequestAutomaticMovement(this, false);
    }

    private static BlundervilleHazard CircleImmunityWindow(BlundervilleHazard hazard, BlundervilleRaceState state, Vector3 player, DateTime now)
    {
        // 20:32 ARR: 149 wears off ~2s before 529. During that remaining
        // protection, 34773 contacts contain only a miss (0x01), with no stun
        // or knockback. Plan the exit now, retaining the obstacle's real window
        // after protection ends. Other actions and all floor/wall checks stay active.
        if (state.Course == BlundervilleCourse.FirstB && player.Y is >= 9.8f and <= 10.8f && player.Z is < -194 and > -250
            && hazard.Id.StartsWith("rhythm/", StringComparison.Ordinal) && hazard.Id.Contains("/34773/", StringComparison.Ordinal)
            && state.ObstacleImmunityExpiresAt(now) is { } until && until > hazard.From)
            return hazard with { From = until };
        return hazard;
    }

    public void ApplyDirectFrame(BlundervilleRaceState state, BlundervilleRaceDecision decision, DateTime now)
    {
        runtime.RequestAutomaticMovement(this, true, decision.Hold, state.NavigationBaseSpeed(now), arenaSafetyMargin: .1f, preferStraightPaths: true);
        runtime.RequestDirectTraversal(this, decision.DirectTraversal ? decision.Target : null, now,
            decision.ContinuousPath, decision.HeadingTolerance, decision.TraversalMotion);
    }

    private void ClearAreas()
    {
        foreach (var danger in _dangers.Values) safeZone.Remove(danger.Handle);
        _dangers.Clear();
        if (_goal.IsValid) safeZone.Remove(_goal);
        _goal = default; _target = null;
        if (_boundary != null && ReferenceEquals(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, _boundary))
            runtime.Manager.ClearArenaBoundary();
        _boundary = _sourceBoundary = null; _course = BlundervilleCourse.None; _crossing = null; _preciseRoute = false;
    }

    private static GeometryTemplate? Template(IGeometryShape geometry) => geometry switch
    {
        BlundervilleBumperSweep sweep => new BlundervilleBumperSweep.SnapshotTemplate(sweep),
        CircleGeometry c => new CircleTemplate(c.Radius),
        FanGeometry f => new FanTemplate(f.Radius, f.AngleRadians),
        RayRectangleGeometry r => new RayRectangleTemplate(r.MinLocalX, r.MaxLocalX, r.Length),
        RectangleGeometry r => new RectangleTemplate(r.Width, r.Length),
        _ => null
    };
    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    private sealed class LocalCorridor(IGeometryShape source, Vector3 center, float radius) : ILocalViewBoundary
    {
        public float ViewRadius => 25;
        public ArenaBoundaryIndex TraversalIndex { get; } = new(source.GetContours());
        public GeometryKind Kind => source.Kind;
        public Vector3 Position => center;
        public float Rotation => 0;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => source.Contains(point, epsilon);
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => source.IntersectsSegment(start, end, epsilon);
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => source.GetContours(segments);
        public GeometryBounds GetBounds()
        {
            var b = source.GetBounds();
            return new(MathF.Max(b.MinX, center.X - radius), MathF.Max(b.MinZ, center.Z - radius),
                MathF.Min(b.MaxX, center.X + radius), MathF.Min(b.MaxZ, center.Z + radius));
        }
    }

    internal sealed class CrossingCorridor(IGeometryShape source, IGeometryShape crossing) : IGeometryShape
    {
        public GeometryKind Kind => source.Kind;
        public Vector3 Position => source.Position;
        public float Rotation => 0;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => source.Contains(point, epsilon) || crossing.Contains(point, epsilon);
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => source.IntersectsSegment(start, end, epsilon) || crossing.IntersectsSegment(start, end, epsilon);
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => source.GetContours(segments).Concat(crossing.GetContours(segments)).ToArray();
        public GeometryBounds GetBounds()
        {
            var a = source.GetBounds(); var b = crossing.GetBounds();
            return new(MathF.Min(a.MinX, b.MinX), MathF.Min(a.MinZ, b.MinZ), MathF.Max(a.MaxX, b.MaxX), MathF.Max(a.MaxZ, b.MaxZ));
        }
    }
}
