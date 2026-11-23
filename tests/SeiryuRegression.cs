using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class SeiryuRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Seiryu phase arena remains polygon19.5 until actual Strength finish then polygon20", () =>
        {
            using var f = Scene();
            Check(f.Engine.Arena!.GetContours().Single().Count == 48 && !f.Engine.Arena.Contains(new(119.8f, 0, 100)), "Initial48gon differs.");
            Start(f, 1, 14281); f.Engine.Effect(1, 14281, 1, f.Now); f.Render();
            Check(!f.Engine.Arena.Contains(new(119.8f, 0, 100)), "Phase changed at start/effect.");
            End(f, 1, 14281);
            Check(f.Engine.Arena.Contains(new(119.8f, 0, 100)) && !f.Engine.Arena.Contains(new(121, 0, 100)), "Phase2 boundary missing.");
        });
        run("Seiryu new model triple callback transition fixed quantized snapshot and component-wide actual finish", () =>
        {
            using var f = Scene();
            f.Actors[30] = new(30, 0x2672, new(104, 3, 100), ModelState: 32); f.Scan();
            Check(Group(f, "seiryu/sigil").Single().Activation == f.Now.AddSeconds(5.6),
                "Active component missed a new actor's initial nonzero model callback.");
            f.Now = f.Start.AddSeconds(1); f.Actors[30] = f.Actors[30] with { AnimationState1 = 1 }; f.Scan();
            var first = Group(f, "seiryu/sigil").Single();
            Check(first.Shape is CircleTemplate { Radius: 12 } && first.Activation == f.Now.AddSeconds(5.6)
                && first.Position != f.Actors[30].Position, "Model callback clock/quantization differs.");
            f.Now = f.Start.AddSeconds(2); f.Actors[30] = f.Actors[30] with { Position = new(110, 0, 100) }; f.Scan();
            Check(Group(f, "seiryu/sigil").Single() == first, "Unchanged model moved/refreshed stored prediction.");
            f.Actors[31] = new(31, 0x233C, new(100, 0, 110)); f.Scan();
            f.Actors[31] = f.Actors[31] with { ModelState = 33 }; f.Scan();
            Check(Group(f, "seiryu/sigil").Single().Shape is DonutTemplate { InnerRadius: 7, OuterRadius: 30 }, "Later model failed to replace global prediction.");
            f.Actors[31] = f.Actors[31] with { ModelState = 0 }; f.Scan();
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(Group(f, "seiryu/sigil").Any(), "Unknown model/clock cleared prediction.");
            Start(f, 10, 14855); End(f, 10, 14855); Check(!Group(f, "seiryu/sigil").Any(), "Actual finish did not clear global prediction.");
        });
        run("Seiryu tower birth static clock lifetime survives destroy until success or failure effect clears all", () =>
        {
            using var f = Scene(); f.Now = f.Start.AddSeconds(2);
            f.Engine.ObjectCreated(30, f.Start); f.Actors[30] = new(30, 0x1EAA3C, new(85, 3, 100)); f.Scan();
            var first = Group(f, "seiryu/tower").Single(a => a.Display);
            Check(Group(f, "seiryu/tower/join").Single().Activation == f.Start.AddSeconds(7.8)
                && first.Shape is CircleTemplate { Radius: 3 }, "Tower birth clock differs.");
            f.Actors.Remove(30); f.Now = f.Start.AddSeconds(100); f.Scan();
            Check(Group(f, "seiryu/tower").Any(), "Destroy/timeout erased static tower.");
            f.Actors[31] = new(31, 0x1EAA3C, new(110, 0, 100)); f.Scan();
            f.Engine.Effect(10, 14989, 1, f.Now); f.Render();
            Check(!Group(f, "seiryu/tower").Any(), "Failed tower did not clear whole component.");
            f.Scan(); Check(!Group(f, "seiryu/tower").Any(), "Consumed tower recreated from persistent object.");
        });
        run("Seiryu tower singleton occupancy display navigation and predicted damage use first activation", () =>
        {
            using var f = Scene();
            f.Actors[30] = new(30, 0x1EAA3C, f.Actors[100].Position); f.Scan();
            Check(Group(f, "seiryu/tower").Single(a => a.Display) is { Safe: true, OutlineOnly: true }
                && f.Engine.PredictedDamage.Single() is { TargetId: 100, DamageType: "Raidwide" }, "Correct tower state differs.");
            f.PlayerId = 101; f.Scan();
            Check(Group(f, "seiryu/tower").Single(a => a.Display) is { Safe: false, OutlineOnly: false }
                && Group(f, "seiryu/tower").Any(a => a.Id.EndsWith("/avoid")), "Full foreign tower is not forbidden.");
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position }; f.Scan();
            Check(f.Engine.Hints(f.Actors[101].Position).Contains("单人塔超员"), "Overfull hint missing.");
            Check(Group(f, "seiryu/tower").Single(a => a.Display) is { Safe: false, OutlineOnly: true },
                "Own overfull tower was filled instead of outlined.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(!f.Engine.PredictedDamage.Any(p => p.TargetId == 100), "Tower predicted damage on a corpse.");
        });
        run("Seiryu tower callbacks correct later clocks and queue order after early discovery", () =>
        {
            using var f = Scene();
            f.Actors[30] = new(30, 0x1EAA3C, new(85, 0, 100));
            f.Actors[31] = new(31, 0x1EAA3C, new(100, 0, 85)); f.Scan();
            f.Engine.ObjectCreated(31, f.Start.AddSeconds(.2));
            f.Engine.ObjectCreated(30, f.Start.AddSeconds(.1)); f.Render();
            var towers = Group(f, "seiryu/tower").Where(a => a.Display).ToArray();
            Check(towers[0].Position.Z < 86 && Group(f, "seiryu/tower/join").Single().Activation == f.Start.AddSeconds(.2).AddSeconds(7.8),
                "Tower callback order or a later actual creation clock was lost.");
        });
        run("Seiryu separate tether batches stay sticky across untether until watched effect when empty", () =>
        {
            using var f = Scene();
            f.Engine.Tether(100, 17, true, f.Now, 20); f.Render();
            var red = Group(f, "seiryu/bait/15389").Single(a => a.Id.EndsWith("/position")).Activation;
            f.Now = f.Start.AddSeconds(1); f.Scan();
            f.Engine.Tether(101, 17, true, f.Now, 21); f.Render();
            Check(Group(f, "seiryu/bait/15392").Single(a => a.Id.EndsWith("/avoid")).Activation == f.Start.AddSeconds(6.9),
                "Independent Kanabo batch reused red clock.");
            f.Engine.Tether(100, 17, false, f.Now, 20);
            f.Now = f.Start.AddSeconds(2); f.Scan(); f.Engine.Tether(100, 17, true, f.Now, 20); f.Render();
            Check(Group(f, "seiryu/bait/15389").Single(a => a.Id.EndsWith("/buddy/101")).Activation == f.Start.AddSeconds(6)
                && Group(f, "seiryu/bait/15389").Single(a => a.Id.EndsWith("/position")).Activation == red!.Value.AddSeconds(2),
                "Sticky batch and rolling position clocks merged.");
            f.Engine.Tether(100, 17, false, f.Now, 20); f.Engine.Effect(20, 15389, 1, f.Now);
            f.Now = f.Start.AddSeconds(3); f.Scan(); f.Engine.Tether(100, 17, true, f.Now, 20); f.Render();
            Check(Group(f, "seiryu/bait/15389").Single(a => a.Id.EndsWith("/buddy/101")).Activation == f.Start.AddSeconds(9),
                "Empty watched effect did not reset red batch.");
        });
        run("Seiryu red forward-only tether direction and blue forbidden applies only first marked line", () =>
        {
            using var f = Scene(); f.Engine.Tether(20, 17, true, f.Now, 100); f.Render();
            Check(!Group(f, "seiryu/bait/15389").Any(), "Reverse red tether accepted.");
            f.Engine.Tether(100, 17, true, f.Now, 20);
            f.Engine.Effect(10, 14319, 1, f.Now, 101); f.Scan();
            Check(Group(f, "seiryu/line/15388").Single(a => a.Display).Safe == false
                && Group(f, "seiryu/line/15388").Any(a => a.Id.EndsWith("/forbidden"))
                && !f.Engine.PredictedDamage.Any(p => p.TargetId == 100 && p.DamageType == "Shared")
                && f.Engine.ForbiddenTargets.Length == 0, "Forbidden soaker confused with ACR restriction.");
            f.Engine.Effect(11, 14319, 2, f.Now, 102); f.Scan();
            Check(Group(f, "seiryu/line/15388").Count(a => a.Display && a.Safe) == 1
                && f.Engine.PredictedDamage.Any(p => p.TargetId == 100 && p.DamageType == "Shared"),
                "Forbidden propagated to all lines rather than first only.");
        });
        run("Seiryu line stacks use marker effect dynamic targets actual second only and final-target correction", () =>
        {
            using var f = Scene(); f.Engine.Effect(10, 14277, 1, f.Now, 101); f.Render();
            var line = Group(f, "seiryu/line/15474").Single(a => a.Display);
            Check(line.Shape is RayRectangleTemplate { Length: 84.4f, MinLocalX: -4, MaxLocalX: 4 }
                && Group(f, "seiryu/line/15474/join").Single().Activation == f.Start.AddSeconds((double)5.2f),
                "ForbiddenArts marker delay/geometry differs.");
            f.Actors[101] = f.Actors[101] with { Position = new(115, 0, 100), Dead = true }; f.Scan();
            Check(Group(f, "seiryu/line/15474").Single(a => a.Display).Rotation > 1.5f, "Dead target stopped dynamic line.");
            f.Engine.Effect(1, 15490, 2, f.Now, 101); f.Render(); Check(Group(f, "seiryu/line/15474").Any(), "First effect consumed source second-only line.");
            f.Engine.Effect(1, 15474, 3, f.Now, 9999); f.Render(); Check(Group(f, "seiryu/line/15474").Any(), "Unknown target consumed line.");
            f.Engine.Effect(1, 15474, 4, f.Now, 102); f.Render(); Check(!Group(f, "seiryu/line/15474").Any(), "Single-line corrected target failed to resolve.");
        });
        run("Seiryu red phase-dependent position and Kanabo reverse tether input preserve distinct geometry", () =>
        {
            using var f = Scene();
            f.Engine.Tether(100, 17, true, f.Now, 20); f.Render();
            var pre = Group(f, "seiryu/bait/15389").Single(a => a.Id.EndsWith("/position"));
            Check(pre.Shape is CircleTemplate { Radius: 18.5f }, "Pre phase red edge requirement differs.");
            Start(f, 1, 14281); End(f, 1, 14281);
            var post = Group(f, "seiryu/bait/15389").Single(a => a.Id.EndsWith("/position"));
            Check(!post.Shape.Resolve(post.Position, 0, null).Contains(new(100, 0, 100))
                && post.Shape.Resolve(post.Position, 0, null).Contains(new(110, 0, 100)), "Phase2 red did not require center5.");
            f.Engine.Tether(21, 17, true, f.Now, 101); f.Render();
            Check(Group(f, "seiryu/bait/15392").Single(a => a.Id.EndsWith("/display")).Shape is FanTemplate { Radius: 45 },
                "Kanabo reverse input lost bait.");
        });
        run("Seiryu directional river ignores immunity but handprint suppresses only custom navigation", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(30) } }; f.Scan();
            Start(f, 10, 14349, MathF.PI / 2);
            Check(Group(f, "seiryu/knockback/14349").Any() && Group(f, "seiryu/river/join").Any(), "River obeyed immunity.");
            var hint = Group(f, "seiryu/river/join").Single();
            Check(!hint.Shape.Resolve(hint.Position, 0, null).Contains(new(90, 0, 100))
                && hint.Shape.Resolve(hint.Position, 0, null).Contains(new(110, 0, 100)), "Fixed displacement R19 preimage differs.");
            Start(f, 11, 14344);
            Check(Group(f, "seiryu/knockback/14349").Any() && !Group(f, "seiryu/river/join").Any(),
                "Handprint removed trajectory or left custom AI.");
            End(f, 11, 14344); Check(Group(f, "seiryu/river/join").Any(), "River AI did not resume.");
        });
        run("Seiryu radial force honors immunity for trajectory yet keeps center10 navigation", () =>
        {
            using var f = Scene(); Start(f, 10, 14346);
            Check(Group(f, "seiryu/knockback/14346").Any() && Group(f, "seiryu/force/join").Any(), "Force output missing.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(30) } }; f.Scan();
            Check(!Group(f, "seiryu/knockback/14346").Any() && Group(f, "seiryu/force/join").Any(), "Immune force incorrectly removed custom AI.");
        });
        run("Seiryu icon spread delay actual effect clear and death revival cannot resurrect marker", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 169, f.Now); f.Render();
            Check(Group(f, "seiryu/spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 5 }
                && Group(f, "seiryu/spread").Single(a => a.Navigate) is { Shape: CircleTemplate { Radius: 6 }, Activation: var at }
                && at == f.Start.AddSeconds(6), "Spread shape/clock differs.");
            f.Now = f.Start.AddSeconds(20); f.Scan(); Check(Group(f, "seiryu/spread").Any(), "Estimated end removed icon spread.");
            f.Engine.Effect(10, 14340, 1, f.Now); f.Render(); Check(!Group(f, "seiryu/spread").Any(), "Resolve effect did not consume spread.");
            f.Engine.Icon(101, 169, f.Now); f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan(); Check(!Group(f, "seiryu/spread").Any(), "Revival resurrected spread.");
        });
        run("Seiryu icon effects consume only one matching or fallback spread and predict earliest damage", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 169, f.Now);
            f.Now = f.Start.AddSeconds(1); f.Engine.Icon(100, 169, f.Now); f.Render();
            Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Raidwide" && p.Activation == f.Start.AddSeconds(6)) == 2,
                "Spread earliest damage mask missing.");
            f.Engine.Effect(10, 14340, 1, f.Now, 100); f.Render();
            Check(Group(f, "seiryu/spread").Count(a => a.Display) == 1
                && f.Engine.PredictedDamage.Single().TargetId == 101, "One matching hit consumed another marker.");
            f.Engine.Effect(10, 14340, 2, f.Now, 9999); f.Render();
            Check(!Group(f, "seiryu/spread").Any(), "Unknown main target failed to consume one fallback marker.");
        });
        run("Seiryu radial knockbacks retain insertion order after a source finishes and a new source starts", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 100) };
            f.Actors[11] = f.Actors[11] with { Position = new(110, 0, 100) };
            f.Actors[12] = new(12, 0x233C, new(100, 0, 110)); f.Scan();
            Start(f, 10, 14346); Start(f, 11, 14346); End(f, 10, 14346); Start(f, 12, 14346);
            var end = Group(f, "seiryu/knockback/14346/12/landing").Single().Position;
            Check(Vector3.Distance(end, new(82.92893f, 0, 92.92893f)) < .02f,
                "Dictionary slot reuse reordered consecutive knockbacks.");
        });
        run("Seiryu infirm target-centered bait dynamic dead target tankbuster and true finish", () =>
        {
            using var f = Scene(); Start(f, 1, 14333, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = new(112, 0, 100), Dead = true }; f.Scan();
            var aoe = Group(f, "seiryu/bait/14333").Single(a => a.Display);
            Check(aoe.Position == f.Actors[101].Position && aoe.Shape is CircleTemplate { Radius: 4 }
                && f.Engine.PredictedDamage.Single().TargetId == 101, "Infirm dead target/bait prediction omitted.");
            End(f, 1, 14333); Check(!Group(f, "seiryu/bait/14333").Any(), "Infirm actual finish leaked.");
        });
        run("Seiryu corrected radial cast restarts at FIFO tail", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 100) }; f.Scan();
            var a = new DungeonCastObservation(14346, new(109, 0, 100), 0, f.Now, f.Now.AddSeconds(5));
            var b = a with { Location = new(100, 0, 110) };
            f.Engine.StartCast(10, a, f.Now); f.Engine.StartCast(11, b, f.Now);
            f.Engine.StartCast(10, a with { Location = new(110, 0, 100) }, f.Now); f.Render();
            var end = Group(f, "seiryu/knockback/14346/10/landing").Single().Position;
            Check(Vector3.Distance(end, new(92.92893f, 0, 82.92893f)) < .02f,
                "Same-cast location correction retained the stale FIFO position.");
        });
        run("Seiryu corrected infirm cast with unresolved target removes old bait and damage", () =>
        {
            using var f = Scene(); Start(f, 1, 14333, target: 101);
            var cast = f.Actors[1].Cast! with { TargetId = 9999 };
            f.Actors[1] = f.Actors[1] with { Cast = cast };
            f.Engine.StartCast(1, cast, f.Now); f.Render();
            Check(!Group(f, "seiryu/bait/14333").Any() && !f.Engine.PredictedDamage.Any(),
                "Unresolved correction retained the original target.");
        });
        run("Seiryu ordinary casts dual drawing true independent endings hidden phase death and ownership", () =>
        {
            using var f = Scene(); Start(f, 10, 14342); Start(f, 11, 14355);
            var a = f.Draw.Previews["cast/10"].Geometry;
            Check(a.Contains(new(101.9f, 0, 150)) && HostDrawingGeometry.Convert(a).Contains(new(101.9f, 0, 150)), "Ray dual view differs.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(f.Draw.Previews.ContainsKey("cast/10"), "Clock erased active cast.");
            End(f, 10, 14342); Check(f.Draw.Previews.ContainsKey("cast/11"), "Independent ending erased other line.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan(); Check(f.Engine.Active != null, "Hidden primary ended trivial phase.");
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.seiryu") && f.Engine.ForbiddenTargets.Length == 0, "Ownership/restriction differs.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); Check(f.Draw.Previews.Count == 0, "Death leaked graphics.");
        });
    }
    private static Fight Scene()
    {
        var f = new Fight(0x25F4, 824); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, c - Vector3.UnitX * 10, PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c);
        f.Actors[20] = new(20, 0x2786, c - Vector3.UnitZ * 15);
        f.Actors[21] = new(21, 0x2788, c - Vector3.UnitZ * 10); f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
