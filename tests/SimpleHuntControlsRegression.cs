using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

internal static class SimpleHuntControlsRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts charge exact widths quantized origins locked route and changed cast inputs", Charge);
        run("SimpleHunts gaze actual range shared safe facing lead eye arc and view changes", Gaze);
        run("SimpleHunts knockback shapes distances all immunities expiry landing and damage independence", Knockback);
        run("SimpleHunts next four source rules exact dimensions pure hints and unavoidable predictions", Rules);
        run("SimpleHunts LunatenderQueen ordinary inverted shared gaze directions arcs and eight source rules", InvertedGaze);
    }

    private static void Charge()
    {
        foreach (var (oid, action, halfWidth) in new[] { (0x35DDu, 27075u, 4f), (0x1AB5u, 7921u, 4f), (0x1AB5u, 7922u, 6f), (0x283Au, 17862u, 3f) })
        {
            var encounter = new SimpleHuntEncounter();
            var origin = new Vector3(566.6437377929688f, 0, 2.5f);
            var cast = Cast(action, origin + Vector3.UnitX * 10);
            var boss = Boss(1, oid, cast) with { Position = origin };
            encounter.Synchronize([boss], _ => true);
            var pending = encounter.Casts.Single();
            const float grid = 2000f / 65535f;
            const float gridInv = 1f / grid;
            var expectedOrigin = new Vector3(((int)MathF.Round(origin.X * gridInv) - .5f) * grid, 0, ((int)MathF.Round(origin.Z * gridInv) - .5f) * grid);
            Check(pending.Location == expectedOrigin && pending.Rotation == MathF.PI / 2, "Charge used a target actor, spell rotation or unquantized origin.");
            var shape = pending.Shape!.Resolve(pending.Location, pending.Rotation, null);
            Check(shape.Contains(expectedOrigin + new Vector3(5, 0, halfWidth - .01f), 0)
                && !shape.Contains(expectedOrigin + new Vector3(5, 0, halfWidth + .01f), 0)
                && !shape.Contains(expectedOrigin + new Vector3(10.01f, 0, 0), 0)
                && !shape.Contains(expectedOrigin - Vector3.UnitX * .01f, 0), "Charge dimensions added hitbox or changed half-width/front/back.");
            boss = boss with { Position = new(50, 0, 50), Rotation = 3 };
            encounter.Synchronize([boss], _ => true);
            Check(encounter.Casts.Single() == pending, "Caster movement alone changed a charge route.");
            cast = cast with { FinishAt = cast.FinishAt.AddMilliseconds(40) };
            boss = boss with { Cast = cast };
            encounter.Synchronize([boss], _ => true);
            var corrected = encounter.Casts.Single();
            var expectedDirection = Vector3.Normalize(cast.Location - boss.Position);
            Check(corrected.Activation == cast.FinishAt && Vector3.Distance(corrected.Location, boss.Position) < .05f
                && MathF.Abs(corrected.Rotation - MathF.Atan2(expectedDirection.X, expectedDirection.Z)) < .0001f,
                "A 40ms duration correction did not restart the charge geometry from the observed caster.");
            boss = boss with { Cast = cast with { Location = new(60, 0, 50) } };
            encounter.Synchronize([boss], _ => true);
            Check(encounter.Casts.Single().Location != pending.Location, "A real changed cast location failed to rebuild the charge from the new origin.");
            encounter.Finish(1, action);
            Check(encounter.Casts.Count == 0, "Charge cast end retained its route.");
        }
    }

    private static void Gaze()
    {
        var encounter = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100, Position = Vector3.Zero, Rotation = 0 };
        var first = Boss(1, 0x2A03, Cast(18041, new(0, 0, 60)));
        var second = Boss(2, 0x2A03, Cast(18041, new(0, 0, -60)));
        encounter.Synchronize([first, second], _ => true);
        Check(encounter.Casts.All(c => c.Shape == null) && encounter.Hints(player).Contains("Turn away from gaze!"),
            "CastGaze became an R40 movement danger or lost its early facing hint.");
        Check(encounter.Facing(player, Start.AddSeconds(2.54)) == null, "PR gaze turned before the .75-second bridge lead.");
        var heading = encounter.Facing(player, Start.AddSeconds(2.56));
        Check(heading != null && encounter.Casts.All(c => !SimpleHuntEncounter.HitByGaze(c, player with { Rotation = heading.Value })),
            "Opposing gazes did not share a safe direction.");
        var previews = HuntDrawing.Collect(encounter, player).ToArray();
        Check(previews.Length == 7 && previews.Count(p => p.Id.Contains("/eye")) == 2
            && previews.Count(p => p.Id.Contains("/pupil")) == 2, "Gaze eye/pupil/view arc or enemy drawings were omitted.");
        var arc = previews.Single(p => p.Id == "gaze/view").Geometry;
        Check(arc.Contains(new(0, 0, 1), 0) && !arc.Contains(new(0, 0, -1), 0), "Gaze view arc was reversed.");
        encounter.Synchronize([first with { Position = new(100, 0, 100) }, second], _ => true);
        Check(encounter.Casts.Single(c => c.Source == 1).Location == first.Cast!.Location, "Actor motion moved a locked eye.");
        var outside = player with { Position = new(10001, 0, 0) };
        Check(encounter.Facing(outside, Start.AddSeconds(3)) == null
            && !HuntDrawing.Collect(encounter, outside).Any(p => p.Id == "gaze/view"), "Default source gaze range was not 10000.");
        encounter.Finish(1);
        encounter.Finish(2);
        Check(encounter.Facing(player, Start.AddSeconds(3)) == null, "Gaze finish retained a facing request.");
        encounter.Reset();
        encounter.Synchronize([first with { Cast = Cast(18041, Vector3.Zero) }], _ => true);
        Check(encounter.Facing(player, Start.AddSeconds(3)) == null && !encounter.Hints(player).Contains("Turn away from gaze!"),
            "Coincident eye/player invented a forbidden direction.");
    }

    private static void Knockback()
    {
        foreach (var (oid, action, radius, distance) in new[] { (0x1AB5u, 7919u, 10f, 20f), (0x1AB1u, 7995u, 14f, 30f), (0x1AB1u, 7993u, 10f, 20f) })
        {
            var encounter = new SimpleHuntEncounter();
            var origin = new Vector3(10, 0, 20);
            encounter.Synchronize([Boss(1, oid, Cast(action, origin)) with { Position = new(500, 0, 500) }], _ => true);
            var player = new ReplayActorState { Id = 100, Position = origin + Vector3.UnitX * 5 };
            var movement = encounter.Knockbacks(player).Single();
            Check(movement.From == player.Position && movement.To == player.Position + Vector3.UnitX * distance,
                "Knockback used boss position, froze player position, changed distance or stopped at a display boundary.");
            Check(encounter.Knockbacks(player with { Position = origin }).Count() == 0
                && encounter.Knockbacks(player with { Position = origin + Vector3.UnitX * (radius + .01f) }).Count() == 0,
                "Knockback affected an out-of-range point or invented direction at its origin.");
            Check(encounter.Knockbacks(player with { Position = origin + Vector3.UnitX * .0005f }).Count() == 1,
                "A nonzero near-origin point incorrectly lost its knockback.");
            foreach (var status in new uint[] { 3054, 160, 1209, 1722, 2663, 2345 })
            {
                var immune = player with { Statuses = [new(status, 0, 100, Start.AddSeconds(4))] };
                Check(!encounter.Knockbacks(immune).Any() && encounter.Casts.Single().Shape != null,
                    "Immunity failed to skip only knockback while preserving damage.");
                Check(encounter.Knockbacks(immune with { Statuses = [new(status, 0, 100, Start.AddSeconds(3.3))] }).Count() == 1,
                    "Immunity expiry equal to activation incorrectly protected the player.");
                Check(HuntDrawing.Collect(encounter, immune).Count() == 1, "Immune player retained a projected landing.");
            }
            var drawings = HuntDrawing.Collect(encounter, player).ToArray();
            Check(drawings.Count(p => p.Id.StartsWith("knockback/")) == 2, "Knockback omitted its dual-view route or landing actor.");
            Check(encounter.Knockbacks(player with { Position = origin + Vector3.UnitZ * 2 }).Single().To == origin + Vector3.UnitZ * (2 + distance),
                "Knockback did not recompute from the current view position.");
        }
        var mixed = new SimpleHuntEncounter();
        var viewed = new ReplayActorState { Id = 100, Position = Vector3.UnitX };
        mixed.Synchronize([
            Boss(1, 0x1AB1, Cast(7993, Vector3.Zero)),
            Boss(2, 0x1AB1, Cast(7995, Vector3.UnitX * 10))
        ], _ => true);
        var projections = mixed.Knockbacks(viewed).ToArray();
        Check(projections.Length == 2 && projections[0].From == viewed.Position && projections[0].To.X == 21
            && projections[1].From == viewed.Position && projections[1].To.X == -29,
            "Distinct knockback components incorrectly shared a chained origin.");
        mixed.Reset();
        mixed.Synchronize([
            Boss(1, 0x1AB1, Cast(7995, Vector3.Zero)),
            Boss(2, 0x1AB1, Cast(7995, Vector3.UnitX * 20))
        ], _ => true);
        projections = mixed.Knockbacks(viewed).ToArray();
        Check(projections.Length == 2 && projections[1].From.X == 31 && projections[1].To.X == 61,
            "Multiple sources in one knockback component stopped chaining.");
        mixed.Synchronize([
            Boss(1, 0x1AB1, Cast(7995, Vector3.Zero) with { FinishAt = Start.AddSeconds(3.34) }),
            Boss(2, 0x1AB1, Cast(7995, Vector3.UnitX * 20))
        ], _ => true);
        projections = mixed.Knockbacks(viewed).ToArray();
        Check(projections.Length == 1 && projections[0].Source == 1 && projections[0].To.X == 31,
            "A restarted same-component source did not move to the source-list tail.");
    }

    private static void Rules()
    {
        var cases = new (uint Oid, uint Action, Vector3 Inside, Vector3 Outside)[]
        {
            (0x35DD, 27073, new(0, 0, 29.99f), new(0, 0, 30.01f)),
            (0x35DD, 27076, new(0, 0, 5.01f), new(0, 0, 4.99f)),
            (0x2A03, 17043, new(0, 0, 4.99f), new(0, 0, 5.01f)),
            (0x1AB5, 7924, new(0, 0, 14.19f), new(0, 0, 14.21f)),
            (0x1AB5, 7920, new(0, 0, 11.19f), new(0, 0, 11.21f)),
            (0x1AB5, 7918, new(0, 0, 18.19f), new(0, 0, 18.21f)),
            (0x1AB1, 7990, new(2.99f, 0, 29.99f), new(3.01f, 0, 29.99f)),
            (0x1AB1, 7991, new(0, 0, 24.99f), new(0, 0, 25.01f)),
            (0x1AB1, 7996, new(0, 0, 8.01f), new(0, 0, 7.99f)),
        };
        foreach (var (oid, action, inside, outside) in cases)
        {
            var encounter = new SimpleHuntEncounter();
            encounter.Synchronize([Boss(1, oid, Cast(action, Vector3.Zero))], _ => true);
            var cast = encounter.Casts.Single();
            var shape = cast.Shape!.Resolve(cast.Location, cast.Rotation, null);
            Check(shape.Contains(inside, 0) && !shape.Contains(outside, 0), $"Source shape {action} changed.");
        }
        foreach (var action in new uint[] { 27077, 27078, 7994, 7992 })
        {
            var definition = SimpleHuntCatalog.Definitions.Single(d => d.Casts.Any(c => c.Action == action));
            var encounter = new SimpleHuntEncounter();
            encounter.Synchronize([Boss(1, definition.Oid, Cast(action, Vector3.Zero) with { TargetId = 100 }), new() { Id = 100 }], _ => true);
            var damage = encounter.PredictedDamage([new(0, "Tank", 100)]).ToArray();
            Check(encounter.Casts.Single().Shape == null && damage.Length == (action == 7992 ? 0 : 1), "CastHint or unavoidable damage became a navigable circle.");
            if (action == 7992) Check(encounter.Hints(null).Single() == "Applies damage buff to self", "RubyTide lost its pure global hint.");
        }
    }

    private static void InvertedGaze()
    {
        var encounter = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100, Rotation = MathF.PI };
        var inverse = Boss(1, 0x35DF, Cast(27369, Vector3.UnitZ * 20));
        encounter.Synchronize([inverse], _ => true);
        Check(encounter.Hints(player).Contains("Face the eye!") && !encounter.Hints(player).Contains("Turn away from gaze!"),
            "Inverted gaze retained the ordinary warning.");
        var heading = encounter.Facing(player, Start.AddSeconds(3));
        Check(heading != null && SimpleHuntEncounter.HitByGaze(encounter.Casts.Single(), player with { Rotation = heading.Value }),
            "Inverted gaze did not face the eye.");
        Check(encounter.Facing(player with { Rotation = 0 }, Start.AddSeconds(3)) == 0,
            "Already safe inverted gaze changed the heading.");
        var drawings = HuntDrawing.Collect(encounter, player).ToArray();
        var arc = drawings.Single(p => p.Id == "gaze/view/inverted").Geometry;
        Check(arc.Contains(Vector3.UnitZ, 0) && !arc.Contains(-Vector3.UnitZ, 0),
            "Inverted gaze did not draw the rear 270 degree arc for a player facing -Z.");
        Check(drawings.Single(p => p.Id.EndsWith("/eye")).Color == new Vector4(1, 0, 0, 1),
            "Inverted gaze eye failed to reverse its dangerous color.");
        encounter.Synchronize([inverse, Boss(2, 0x35DF, Cast(27363, -Vector3.UnitZ * 20))], _ => true);
        heading = encounter.Facing(player, Start.AddSeconds(3));
        Check(heading != null && encounter.Casts.All(c => SimpleHuntEncounter.HitByGaze(c, player with { Rotation = heading.Value }) == c.Rule.InvertedGaze),
            "Mixed ordinary/inverted eyes did not share a safe direction.");
        Check(HuntDrawing.Collect(encounter, player).Count(p => p.Id.StartsWith("gaze/view")) == 2,
            "Mixed eyes did not retain both source arc shapes.");
        encounter.Reset();
        encounter.Synchronize([
            Boss(1, 0x35DF, Cast(27369, new(0, 0, 20))),
            Boss(2, 0x35DF, Cast(27363, new(.19999667f, 0, 19.999f)))
        ], _ => true);
        heading = encounter.Facing(player with { Rotation = 0 }, Start.AddSeconds(3));
        Check(heading != null && encounter.Casts.All(c => SimpleHuntEncounter.HitByGaze(c, player with { Rotation = heading.Value }) == c.Rule.InvertedGaze),
            "A narrow but valid common gaze direction was erased by an extra angular margin.");
        encounter.Reset();
        foreach (var (action, inside, outside) in new[]
        {
            (27364u, 6.01f, 5.99f), (27365u, 14.99f, 15.01f), (27366u, 5.99f, 6.01f),
            (27370u, 14.99f, 15.01f), (27371u, 39.99f, 40.01f)
        })
        {
            encounter.Synchronize([Boss(1, 0x35DF, Cast(action, Vector3.Zero))], _ => true);
            var shape = encounter.Areas.Single().Shape!.Resolve(Vector3.Zero, 0, null);
            Check(shape.Contains(Vector3.UnitZ * inside, 0) && !shape.Contains(Vector3.UnitZ * outside, 0),
                $"LunatenderQueen {action} changed its circle/donut boundary.");
            encounter.Reset();
        }
        encounter.Synchronize([Boss(1, 0x35DF, Cast(27367, Vector3.Zero))], _ => true);
        Check(encounter.Hints(null).Single() == "Invert next cast" && encounter.Casts.Single().Shape == null,
            "WickedWhim was converted from a pure hint into speculative state or geometry.");
    }

    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast cast) => new() { Hp = 1000, Id = id, Oid = oid, InCombat = true, Targetable = true, Cast = cast };
    private static ReplayCast Cast(uint action, Vector3 location) => new(action, 0, location, 0, Start, Start.AddSeconds(3.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
