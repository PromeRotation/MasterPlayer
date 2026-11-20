using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

internal static class NativePt30Tests
{
    public static void Run()
    {
        var start = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var center = new Vector3(-300, 0, -300);
        var boss = new Pt30Encounter.Actor(1, Pt30Encounter.BOSS_OID, center, 0, false, true);
        var e = new Pt30Encounter();
        e.Synchronize([boss], start);
        var arena = Pt30Arena.Boundary(0);
        var holeTemplate = (WorldPolygonTemplate)Pt30Arena.Hole(0);
        var hole = holeTemplate.Resolve(Vector3.Zero, 0, null);
        Check(ReferenceEquals(hole, holeTemplate.Resolve(center, MathF.PI, null)), "static world polygon reuses validated geometry across snapshots");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; ++i) _ = new MasterPlayer.Spatial.Geometry.WorldPolygonGeometry(holeTemplate.WorldPoints);
        var repeatedValidation = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        for (var i = 0; i < 100; ++i) _ = holeTemplate.Resolve(Vector3.Zero, 0, null);
        Console.WriteLine($"PT30 hole: 100 repeated validations {repeatedValidation:F2}ms; 100 cached resolves {timer.Elapsed.TotalMilliseconds:F3}ms.");
        Check(arena.Contains(new(-284, 0, -300)) && !hole.Contains(new(-288, 0, -300)), "inner east platform remains walkable");
        Check(hole.Contains(center) && !arena.Contains(new(-270, 0, -300)), "central hole and outer bounds");
        var rings = new[] { (39642u, 9f, 14f), (39643u, 14f, 19f), (39674u, 19f, 24f), (39743u, 24f, 29f) };
        foreach (var (action, inner, outer) in rings)
        {
            e.Cast(10, action, center, 0, start, start.AddSeconds(12));
            var h = e.Hazards(start, 100, new(-283.3f, 0, -300)).Single();
            Check(h.Shape is DonutTemplate d && d.InnerRadius == inner && d.OuterRadius == outer, "exact halo radii");
            Check(e.Hazards(start, 100, center).Count == 1, "one whole donut, no triangle expansion");
            e.Effect(10, action, action, center, start.AddSeconds(12));
        }
        foreach (var action in new uint[] { 40551, 44927 })
        {
            e.Cast(10, action, center, 0, start, start.AddSeconds(7.7));
            Check(e.Hazards(start, 100, center).Count == 2, "rectangle plus predicted cone");
            Check(e.ReachGoal == null, "Reach goal must not pull into an unresolved Grip rectangle");
            e.Effect(10, action, action, center, start.AddSeconds(7.7));
            e.Hazards(start.AddSeconds(7.7), 100, center);
            Check(e.ReachGoal is { } preposition && !e.Hazards(start, 100, center).Single(h => h.Shape is FanTemplate).Shape
                .Resolve(center, action == 40551 ? 70 * MathF.PI / 180 : -70 * MathF.PI / 180, null)
                .Contains(center + new Vector3(MathF.Sin(preposition.Rotation) * 16.7f, 0, MathF.Cos(preposition.Rotation) * 16.7f)), "Grip resolution publishes safe-sector prepositioning 7.5 seconds before Reach");
            var coneAction = action == 40551 ? 40552u : 40553u;
            e.Cast(11, coneAction, center, MathF.PI, start, start.AddSeconds(1));
            var cone = e.Hazards(start, 100, center).Single(h => h.Shape is FanTemplate);
            Check(Math.Abs(cone.Rotation - MathF.PI) < .001f && cone.Shape is FanTemplate f && Math.Abs(f.AngleRadians - 220 * MathF.PI / 180) < .001f, "actual cone replaces prediction with locked facing");
            e.Cancel(10); e.Cancel(11);
        }
        // Actual 2026-11-17 packet: helper's native TargetLocation was zero, but the
        // network origin is offset behind the arena and contains the observed hit point.
        e.Reset();
        e.Synchronize([boss], start);
        var packetOrigin = new Vector3(-285.26746f, -.015258789f, -330.12897f);
        e.Cast(10, 40551, packetOrigin, -.9187247f, start, start.AddSeconds(7.7));
        var grip = e.Hazards(start, 100, center).Single(h => h.Shape is RayRectangleTemplate);
        Check(grip.Position == packetOrigin && grip.Shape.Resolve(grip.Position, grip.Rotation, null).Contains(new(-314.28424f, .04997635f, -292.16037f)), "recorded Grip packet origin identifies actual hit point");
        e.Cancel(10);
        var ball = new Pt30Encounter.Actor(50, Pt30Encounter.BALL_OID, new(-283.3f, 0, -300), 0, false, true);
        e.Synchronize([boss, ball], start);
        e.Timeline(50, 0x1E46);
        for (var i = 1; i <= 1000; ++i)
        {
            var angle = i * .01f;
            e.Synchronize([boss, ball with { Position = center + new Vector3(MathF.Cos(angle) * 16.7f, 0, MathF.Sin(angle) * 16.7f) }], start.AddSeconds(i * .1));
            var hazards = e.Hazards(start.AddSeconds(i * .1), 100, center);
            Check(hazards.Count <= Pt30Encounter.BALL_PREDICTION_SLICES && hazards.All(h => h.From.Year < 9999 && h.Until > h.From), "1000 moving frames bounded and finite");
        }
        e.Timeline(50, 0x1E3C);
        Check(e.Hazards(start, 100, center).Count == 0, "ball removed by timeline");
        e.Icon(100, 197, start);
        var player = new Vector3(-283.3f, 0, -300);
        for (var i = 0; i <= 90; ++i)
        {
            var waiting = e.Hazards(start.AddSeconds(i * .1), 100, player);
            Check(e.Goal == null && !waiting.Any(h => h.Name == "Divine Favor"),
                "icon alone must not start movement or invent a chase circle, even after the estimated first-hit time");
            Check(e.Remaining == 8, "waiting for the actual circle retains the marked sequence");
        }
        var castAt = start.AddSeconds(9);
        var firstHitAt = castAt.AddSeconds(3.3);
        e.Cast(10, 44918, player, 0, castAt, firstHitAt);
        var firstCircle = e.Hazards(castAt, 100, player).Single();
        var goal = e.Goal;
        Check(goal != null && !hole.Contains(goal.Value) && firstCircle.From == firstHitAt && firstCircle.Position == player,
            "first circle telegraph starts chase movement with its actual locked position and hit time");
        Check(e.Hazards(castAt.AddSeconds(.1), 100, player).Count == 1 && e.Goal == goal, "stable chase waypoint");
        var lastHit = player;
        for (uint pulse = 0; pulse < 8; ++pulse)
        {
            var now = firstHitAt.AddSeconds(pulse * .6);
            e.Hazards(now, 100, player);
            var delta = e.Goal!.Value - player;
            if (delta.Length() > .01f) player += Vector3.Normalize(delta) * Math.Min(3.6f, delta.Length());
            var action = pulse == 0 ? 44918u : 44919u;
            if (pulse > 0) lastHit += Vector3.Normalize(player - lastHit) * Math.Min(3.5f, Vector3.Distance(player, lastHit));
            e.Effect(10, 1000 + pulse, action, lastHit, now);
            var remaining = e.Remaining;
            e.Effect(10, 1000 + pulse, action, lastHit, now);
            Check(e.Remaining == remaining, "AOE per-target effects deduplicated");
        }
        Check(e.Remaining == 0 && e.Goal == null, "eight hits clear chase");
        e.Icon(100, 197, start);
        e.Cast(20, 44918, player, 0, start, start.AddSeconds(3));
        e.Cancel(99);
        Check(e.Remaining == 8, "other source cancellation must preserve the chase");
        e.Cancel(20);
        Check(e.Remaining == 0 && !e.Hazards(start, 100, player).Any(h => h.Key == "chase") && e.Goal == null,
            "cancelled first chase cast must clear its goal and sequence");
        e.Cast(30, 39642, center, 0, start, start.AddSeconds(5));
        e.Cast(30, 39642, center, 0, start.AddSeconds(2), start.AddSeconds(7));
        Check(e.CastCount == 2, "same-source halo observations must remain separate");
        e.Synchronize([boss], start.AddSeconds(5.5));
        e.Effect(30, 2000, 39642, Vector3.Zero, start.AddSeconds(5.6));
        Check(e.Hazards(start.AddSeconds(5.6), 100, player).Count == 1,
            "expired first halo effect must not delete the second cast");
        e.Effect(30, 2000, 39642, Vector3.Zero, start.AddSeconds(5.7));
        Check(e.CastCount == 1, "duplicate old halo effect must not consume the next cast");
        e.Cancel(30);
        Console.WriteLine("PASS MP native PT30 identity: cancellation clears chase; late same-source effects preserve future casts.");
        e.Reset();
        Check(!e.Active && e.CastCount == 0 && e.BallCount == 0, "wipe and stop cleanup");
        foreach (var velocity in new[] { -.3f, .16f, .3f })
        {
            e.Reset();
            Vector3 Orbit(float angle) => center + new Vector3(MathF.Sin(angle) * 22, 0, MathF.Cos(angle) * 22);
            var moving = ball with { Position = Orbit(0) };
            e.Synchronize([boss, moving], start.AddSeconds(-.1));
            e.Synchronize([boss, moving with { Position = Orbit(velocity * .1f) }], start);
            var predictions = e.Hazards(start, 100, center);
            Check(predictions.All(h => h.Shape is Pt30BallSweepTemplate { Radius: < 4.15f }), "R4 sweep adds only its mathematical chord sagitta");
            for (var step = 0; step <= 1000; ++step)
            {
                var seconds = step * .01f;
                var at = start.AddSeconds(seconds);
                var position = Orbit(velocity * (.1f + seconds));
                var active = predictions.Where(h => h.Window(start).IsActiveAt(at)).ToArray();
                for (var angle = 0; angle < 16; ++angle)
                {
                    var edge = position + new Vector3(MathF.Sin(angle * MathF.PI / 8) * 4, 0, MathF.Cos(angle * MathF.PI / 8) * 4);
                    Check(active.Any(h => h.Shape.Resolve(h.Position, h.Rotation, null).Contains(edge)), "unexpanded sweep must contain the full actual moving R4 circle");
                }
            }
        }
        Console.WriteLine("PASS MP native PT30: four donuts, both cones, arena/hole, 1000-frame bounded ball output, eight chase effects and cleanup.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
