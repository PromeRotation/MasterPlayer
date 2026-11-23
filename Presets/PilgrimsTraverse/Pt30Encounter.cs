// Mechanic dimensions and arena derived from BossModReborn DD30ForgivenTreachery (MIT).
using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt30Encounter
{
    public const uint BOSS_OID = 0x4609;
    public const uint BALL_OID = 0x460A;
    public const int BALL_PREDICTION_SLICES = 20;
    // Keep the original footprints. Grid dilation would close the narrow passages
    // between the balls and the floor hole.
    public const float NAVIGATION_GRID_MARGIN = 0;
    public static readonly uint[] Actions = [39642, 39643, 39674, 39743, 40551, 44927, 40552, 40553, 39876, 44917, 44918, 44919];
    public sealed record Actor(ulong Id, uint Oid, Vector3 Position, float Rotation, bool Dead, bool InCombat);
    public sealed record Hazard(string Key, string Name, GeometryTemplate Shape, Vector3 Position, float Rotation, DateTime From, DateTime Until)
    {
        public bool HasMotionEnvelope { get; init; }
        public bool HasExplicitTiming { get; init; }
        public Vector3? BallPreviewPosition { get; init; }
        public IGeometryShape? BallPreviewGeometry { get; init; }
        public DangerWindow Window(DateTime now) => new(From, Until == DateTime.MaxValue ? null : Until, now)
        {
            HasMotionEnvelope = HasMotionEnvelope,
            HasExplicitTiming = HasExplicitTiming,
        };
    }
    private readonly Dictionary<string, Hazard> casts = [];
    private readonly Dictionary<string, DateTime> castFinishTimes = [];
    private readonly Dictionary<ulong, BallMotion> balls = [];
    private readonly HashSet<ulong> activeBalls = [];
    private readonly HashSet<ulong> inactiveBalls = [];
    private readonly HashSet<(ulong, uint)> seenEffects = [];
    private readonly Dictionary<ulong, Actor> observedActors = [];
    private ulong markedPlayer;
    private ulong chasingPlayer;
    private ulong chaseSource;
    private Vector3 lastChaseHit;
    private DateTime nextChaseHit;
    private DateTime chaseExpires;
    private int direction;
    private bool firstChaseCast;
    public int Remaining { get; private set; }
    public bool Active { get; private set; }
    public Vector3 BossPosition { get; private set; } = new(-300, 0, -300);
    public Vector3? Goal { get; private set; }
    public Hazard? ReachGoal { get; private set; }
    public (Vector3 Start, Vector3 End)? ChaseLine => Remaining > 0
        && observedActors.GetValueOrDefault(chasingPlayer) is { Dead: false } target ? (lastChaseHit, target.Position) : null;
    public int CastCount => casts.Count;
    public int BallCount => balls.Count;
    public string Status => $"casts={casts.Count}, balls={balls.Count}, chase={Remaining}, direction={direction}, goal={Goal}, reachGoal={ReachGoal?.Rotation}";

    public void Synchronize(IEnumerable<Actor> actors, DateTime now)
    {
        var list = actors.ToArray();
        observedActors.Clear();
        foreach (var actor in list) observedActors[actor.Id] = actor;
        var boss = list.FirstOrDefault(a => a.Oid == BOSS_OID && !a.Dead);
        Active = boss?.InCombat == true;
        if (boss == null || !Active) { Reset(); return; }
        BossPosition = boss.Position;
        var seen = new HashSet<ulong>();
        foreach (var ball in list.Where(a => a.Oid == BALL_OID && !a.Dead && !inactiveBalls.Contains(a.Id)))
        {
            seen.Add(ball.Id);
            if (balls.TryGetValue(ball.Id, out var motion))
                motion.Observe(ball.Position, now);
            else
                balls.Add(ball.Id, new(ball.Position, now));
            balls[ball.Id].Rotation = ball.Rotation;
        }
        foreach (var id in balls.Keys.Where(id => !seen.Contains(id)).ToArray()) balls.Remove(id);
        if (Remaining > 0 && now > chaseExpires) ClearChase();
        // 首击仍按已锁定的位置结算；之后死亡或消失的目标不再产生追踪圈。
        if (Remaining is > 0 and < 8 && (!observedActors.TryGetValue(chasingPlayer, out var target) || target.Dead))
            ClearChase();
    }

    public void Timeline(ulong actor, ushort timeline)
    {
        if (timeline == 0x1E46) { activeBalls.Add(actor); inactiveBalls.Remove(actor); }
        if (timeline == 0x1E3C) { activeBalls.Remove(actor); inactiveBalls.Add(actor); balls.Remove(actor); }
    }

    public void Icon(ulong target, uint icon, DateTime now)
    {
        if (icon != 197) return;
        markedPlayer = target;
        // The icon only marks the target. Start moving when the first circle is telegraphed.
        chasingPlayer = 0;
        chaseSource = 0;
        Remaining = 8;
        firstChaseCast = false;
        nextChaseHit = now.AddSeconds(8);
        chaseExpires = now.AddSeconds(20);
        direction = 0;
        Goal = null;
    }

    public void Cast(ulong source, uint action, Vector3 location, float rotation, DateTime start, DateTime finish)
    {
        GeometryTemplate? shape = action switch
        {
            39642 => new DonutTemplate(9, 14),
            39643 => new DonutTemplate(14, 19),
            39674 => new DonutTemplate(19, 24),
            39743 => new DonutTemplate(24, 29),
            40551 or 44927 => RayRectangleTemplate.Symmetric(30, 60),
            40552 or 40553 => new FanTemplate(30, 220 * MathF.PI / 180),
            44918 => new CircleTemplate(4),
            _ => null
        };
        if (shape == null) return;
        var key = $"cast/{source}/{action}";
        // Sampling and packet events describe the same cast; retain its first locked pose/time.
        if (castFinishTimes.TryGetValue(key, out var previousFinish) && Math.Abs((previousFinish - finish).TotalSeconds) < 1) return;
        castFinishTimes[key] = finish;
        // ARR: the helper effects resolve ~0.3s after cast end. The caller has
        // already included that delay. Do not add another 0.9s occupation tail.
        key += $"/{finish.Ticks}";
        casts[key] = new(key, Name(action), shape, location, rotation, finish.AddSeconds(-.2), finish.AddSeconds(.15))
            { HasExplicitTiming = action != 44918 };
        if (action is 40551 or 44927)
        {
            var side = action == 40551 ? 1 : -1;
            var followKey = $"reach/{source}";
            casts[followKey] = new(followKey, "Salvation's Reach 预站位", new FanTemplate(30, 220 * MathF.PI / 180),
                BossPosition, rotation + side * 70 * MathF.PI / 180, finish.AddSeconds(7.3), finish.AddSeconds(7.65))
                { HasExplicitTiming = true };
        }
        if (action is 40552 or 40553)
            foreach (var item in casts.Keys.Where(k => k.StartsWith("reach/", StringComparison.Ordinal)).ToArray()) casts.Remove(item);
        if (action == 44918)
        {
            chaseSource = source;
            chasingPlayer = markedPlayer;
            Remaining = 8;
            lastChaseHit = location;
            firstChaseCast = true;
            nextChaseHit = finish;
            chaseExpires = finish.AddSeconds(8);
        }
    }

    public void Cancel(ulong source)
    {
        foreach (var key in casts.Keys.Where(k => k.StartsWith($"cast/{source}/", StringComparison.Ordinal) || k == $"reach/{source}").ToArray()) casts.Remove(key);
        if (chaseSource == source) ClearChase();
    }

    public void Effect(ulong source, uint sequence, uint action, Vector3? location, DateTime now, int targetIndex = 0)
    {
        if (!Actions.Contains(action) || sequence == 0 && targetIndex > 0
            || sequence != 0 && !seenEffects.Add((source, sequence))) return;
        if (seenEffects.Count > 512) seenEffects.Remove(seenEffects.First());
        var resolved = casts.Values.Where(h => h.Key.StartsWith($"cast/{source}/{action}/", StringComparison.Ordinal)).MinBy(h => h.From);
        if (resolved != null) casts.Remove(resolved.Key);
        if (action is 40552 or 40553)
            foreach (var key in casts.Keys.Where(k => k.StartsWith("reach/", StringComparison.Ordinal)).ToArray()) casts.Remove(key);
        if (action is not (44918 or 44919)) return;
        if (location is { } p) lastChaseHit = p;
        Remaining = Math.Max(0, Remaining - 1);
        firstChaseCast = false;
        nextChaseHit = now.AddSeconds(.6);
        if (Remaining == 0) ClearChase();
    }

    public IReadOnlyList<Hazard> Hazards(DateTime now, ulong playerId, Vector3 player)
    {
        var result = casts.Values.Where(h => h.Until >= now).ToList();
        ReachGoal = null;
        // Do not pull into the later safe side while Grip's rectangle is still pending.
        // After Grip resolves there are 7.5 seconds left to preposition for Reach.
        if (Remaining == 0 && !result.Any(h => h.Shape is RayRectangleTemplate))
        {
            var reach = result.FirstOrDefault(h => h.Key.StartsWith("reach/", StringComparison.Ordinal) || h.Shape is FanTemplate);
            if (reach != null)
                ReachGoal = new("reach-goal", "Salvation's Reach 提前站位", new FanTemplate(29, 128 * MathF.PI / 180),
                    BossPosition, reach.Rotation + MathF.PI, now, reach.Until);
        }
        foreach (var (id, ball) in balls)
        {
            if (MathF.Abs(ball.Velocity) < .02f)
            {
                result.Add(new($"ball/{id}/0", "Bounds of Indulgence", new CircleTemplate(4), ball.Position, 0, now, DateTime.MaxValue)
                    { HasMotionEnvelope = true });
                continue;
            }
            // A capsule follows the chord of one short arc. Only the tiny chord
            // sagitta expands its width; travel distance never enlarges the ball.
            const float slot = .5f, timeReserve = .1f;
            // Cover the solver's entire ten-second horizon: ending after two
            // seconds lets SIPP repeatedly wait for a fictitious disappearance.
            var angle = Angle(ball.Position);
            var radius = Radius(ball.Position);
            var sagitta = radius * (1 - MathF.Cos(MathF.Abs(ball.Velocity) * (slot * .5f + timeReserve)));
            for (var i = 0; i < BALL_PREDICTION_SLICES; i++)
            {
                var from = Point(angle + ball.Velocity * (i * slot - timeReserve), radius, ball.Position.Y);
                var to = Point(angle + ball.Velocity * ((i + 1) * slot + timeReserve), radius, ball.Position.Y);
                result.Add(new($"ball/{id}/{i}", "Bounds of Indulgence 移动预测", new Pt30BallSweepTemplate(from, to, 4f + sagitta), ball.Position, 0,
                    now.AddSeconds(i * slot - timeReserve), now.AddSeconds((i + 1) * slot + timeReserve))
                    { HasMotionEnvelope = true, BallPreviewPosition = i == 0 ? ball.Position : null,
                        BallPreviewGeometry = i == 0 ? BallArcPreview(ball.Position, ball.Rotation) : null });
            }
        }
        if (Remaining > 0 && chasingPlayer != 0)
        {
            if (firstChaseCast)
                result.RemoveAll(h => h.Key.StartsWith($"cast/{chaseSource}/44918/", StringComparison.Ordinal));
            var predicted = lastChaseHit;
            if (!firstChaseCast)
            {
                var targetPosition = lastChaseHit;
                if (chasingPlayer == playerId)
                    targetPosition = player;
                else if (observedActors.GetValueOrDefault(chasingPlayer) is { Dead: false } target)
                    targetPosition = target.Position;
                var delta = targetPosition - lastChaseHit;
                delta.Y = 0;
                if (delta.LengthSquared() > .001f) predicted += Vector3.Normalize(delta) * Math.Min(3.5f, delta.Length());
            }
            if (firstChaseCast || nextChaseHit < now.AddSeconds(2))
                result.Add(new("chase", "Divine Favor", new CircleTemplate(4), predicted, 0, nextChaseHit, nextChaseHit.AddSeconds(.4)));
            if (chasingPlayer == playerId)
            {
                if (direction == 0)
                    direction = DirectionCost(1, player, result, now) <= DirectionCost(-1, player, result, now) ? 1 : -1;
                // Keep a waypoint stable until reached: replanning latency must not move the goal away every frame.
                if (Goal is not { } goal || Vector3.DistanceSquared(player, goal) < 9)
                    Goal = Point(Angle(player) + direction * 25 * MathF.PI / 180, 16.7f, player.Y);
            }
            else Goal = null;
        }
        else Goal = null;
        return result;
    }

    private int DirectionCost(int sign, Vector3 player, List<Hazard> hazards, DateTime now)
    {
        var score = 0;
        for (var degrees = 5; degrees <= 60; degrees += 5)
        {
            var point = Point(Angle(player) + sign * degrees * MathF.PI / 180, 16.7f, player.Y);
            score += hazards.Count(h => h.From < now.AddSeconds(3) && h.Shape.Resolve(h.Position, h.Rotation, null).Contains(point));
        }
        return score;
    }

    public void Reset()
    {
        Active = false;
        ReachGoal = null;
        casts.Clear(); castFinishTimes.Clear(); balls.Clear(); activeBalls.Clear(); inactiveBalls.Clear(); seenEffects.Clear();
        observedActors.Clear();
        ClearChase();
    }

    private void ClearChase()
    {
        markedPlayer = chasingPlayer = chaseSource = 0;
        Remaining = direction = 0;
        Goal = null;
        firstChaseCast = false;
    }

    private static string Name(uint action) => action switch
    {
        39642 or 39643 or 39674 or 39743 => "Brutal Halo",
        40551 or 44927 => "Grip of Salvation",
        40552 or 40553 => "Salvation's Reach",
        _ => "Divine Favor"
    };
    private static float Angle(Vector3 p) => MathF.Atan2(p.X + 300, p.Z + 300);
    private static float Radius(Vector3 p) => new Vector2(p.X + 300, p.Z + 300).Length();
    private static Vector3 Point(float angle, float radius, float y) => new(-300 + MathF.Sin(angle) * radius, y, -300 + MathF.Cos(angle) * radius);

    // Display the source's thirty-degree arc capsule; navigation keeps its timed short sweeps.
    internal static IGeometryShape BallArcPreview(Vector3 position, float rotation)
    {
        var radius = Radius(position);
        if (radius <= 4) return new CircleGeometry(position, 4);
        var radial = position - new Vector3(-300, position.Y, -300);
        var right = new Vector3(-MathF.Cos(rotation), 0, MathF.Sin(rotation));
        var sign = Vector3.Dot(right, radial) < 0 ? -1 : 1;
        var start = Angle(position);
        var sweep = sign * MathF.PI / 6;
        var end = start + sweep;
        var endpoint = Point(end, radius, position.Y);
        var points = new List<Vector3>();
        const int arcSegments = 24, capSegments = 24;
        for (var i = 0; i <= arcSegments; ++i) points.Add(Point(start + sweep * i / arcSegments, radius + 4, position.Y));
        for (var i = 1; i <= capSegments; ++i)
            points.Add(GeometryHelper.PointFromRotationDistanceXZ(endpoint, end + sign * MathF.PI * i / capSegments, 4));
        for (var i = arcSegments - 1; i >= 0; --i) points.Add(Point(start + sweep * i / arcSegments, radius - 4, position.Y));
        for (var i = 1; i < capSegments; ++i)
            points.Add(GeometryHelper.PointFromRotationDistanceXZ(position, start + sign * MathF.PI + sign * MathF.PI * i / capSegments, 4));
        return new WorldPolygonGeometry(points);
    }

    private sealed class BallMotion(Vector3 position, DateTime at)
    {
        private const double SAMPLE_WINDOW_SECONDS = .8;
        private const double STOPPED_SECONDS = .45;
        private readonly Queue<(DateTime At, float Angle)> samples = new([(at, Angle(position))]);
        private DateTime lastMovement = at;

        public Vector3 Position { get; private set; } = position;
        public float Velocity { get; private set; }
        public float Rotation { get; set; }

        public void Observe(Vector3 position, DateTime now)
        {
            var relocated = MathF.Abs(Radius(position) - Radius(Position)) > 1;
            if (Vector3.DistanceSquared(position, Position) > .0025f) lastMovement = now;
            Position = position;
            var angle = Angle(position);
            if (relocated || (now - lastMovement).TotalSeconds >= STOPPED_SECONDS)
            {
                samples.Clear();
                Velocity = 0;
            }

            // At long range single-update position corrections exaggerate a ball's future displacement.
            while (samples.TryPeek(out var oldest) && (now - oldest.At).TotalSeconds > SAMPLE_WINDOW_SECONDS)
                samples.Dequeue();
            if (samples.TryPeek(out var first) && (now - first.At).TotalSeconds > .05)
            {
                var delta = MathF.IEEERemainder(angle - first.Angle, MathF.Tau);
                Velocity = Math.Clamp(delta / (float)(now - first.At).TotalSeconds, -.8f, .8f);
            }
            samples.Enqueue((now, angle));
        }
    }
}
