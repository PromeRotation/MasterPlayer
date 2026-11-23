using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class WarriorOfLightRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("WarriorOfLight both independent primaries checkpoint restart and square boundary", () =>
        {
            using var f = Scene();
            Check(f.Engine.Active?.BossOid == 0x2DDD && f.Engine.Arena!.Contains(new(119.9f, 0, 119.9f))
                && !f.Engine.Arena.Contains(new(120.1f, 0, 100)), "Initial primary or square differs.");
            f.Engine.MapEffect(0, 0x00020001, f.Now); f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null, "P1 death retained separate P2 module history.");
            f.Actors[2] = new(2, 0x2DDE, new(100, 0, 100), InCombat: true); f.Scan();
            Check(f.Engine.Active?.BossOid == 0x2DDE && f.Engine.PrimaryId == 2 && !Group(f, "tower").Any()
                && ArrDungeonCatalog.Owns(0x2DDE, 922) && ArrDungeonCatalog.IsScript("masterplayer.trial.warrioroflight"),
                "Checkpoint failed to start independently or retained P1 towers.");
        });
        run("WarriorOfLight icon freeze window uses actor slot and expires all modes only by perspective slot", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 225, f.Now, 100);
            At(f, 3.99); Check(!f.Engine.RequireWolMovement, "Freeze movement began early.");
            At(f, 4); Check(f.Engine.RequireWolMovement && Group(f, "element/move").Length == 1, "Actor icon window missing.");
            f.Engine.Icon(101, 225, f.Now); At(f, 6);
            Check(!f.Engine.RequireWolMovement, "Perspective finish failed to clear all states.");
            f.PlayerId = 101; f.Scan(); Check(!f.Engine.RequireWolMovement, "Other slot survived full-state clear.");
            f.Engine.Icon(101, 227, f.Now); f.Scan(); Check(!f.Engine.RequireAllStop, "Pause icon invented Pyretic.");
        });
        run("WarriorOfLight imbued ice callback windows retain source party-only saber-loss condition", () =>
        {
            using var f = Scene(); Start(f, 10, 20243); End(f, 10, 20243);
            Start(f, 10, 20299); At(f, 5.5);
            Check(f.Engine.RequireWolMovement, "Imbued callback +5.5 movement missing.");
            f.Engine.StatusChanged(1, 2377, false, f.Now); End(f, 10, 20299);
            At(f, 8); Start(f, 10, 20300); At(f, 13.5);
            Check(f.Engine.RequireWolMovement, "Boss loss incorrectly cleared source ice flag.");
            f.Engine.StatusChanged(100, 2377, false, f.Now); End(f, 10, 20300);
            At(f, 16); Start(f, 10, 20299); At(f, 22);
            Check(!f.Engine.RequireWolMovement, "Party saber loss failed to clear ice flag.");
        });
        run("WarriorOfLight pyretic explicit and snapshot expiry stop actions immediately and clear on loss", () =>
        {
            using var f = Scene(); f.Engine.StatusChanged(100, 960, true, f.Now, expiresAt: f.Now.AddSeconds(5));
            Check(f.Engine.RequireWolStop && f.Engine.RequireAllStop && !f.Engine.RequireWolMovement, "Pyretic all-stop missing.");
            f.Scan(); Check(f.Engine.RequireAllStop, "Stale raw snapshot erased fresh Pyretic.");
            f.Engine.StatusChanged(100, 960, false, f.Now); f.Scan(); Check(!f.Engine.RequireAllStop, "Pyretic loss leaked.");
            f.Actors[100] = f.Actors[100] with { Statuses = [960], StatusExpirations = new Dictionary<uint, DateTime> { [960] = f.Start.AddSeconds(7) } };
            At(f, 1); Check(f.Engine.RequireAllStop, "Raw status did not restore state.");
            At(f, 7); Check(!f.Engine.RequireAllStop, "Perspective expiry failed exact end cleanup.");
            At(f, 8); Check(!f.Engine.RequireAllStop, "Unchanged raw status recreated expired mode.");
        });
        run("WarriorOfLight movement anchor advances while preserving new-position escape pressure", () =>
        {
            using var f = Scene(); f.Engine.Icon(100, 225, f.Now); At(f, 4);
            var old = Group(f, "element/move").Single().Position;
            f.Actors[100] = f.Actors[100] with { Position = old + Vector3.UnitX * 2 }; f.Scan();
            Check(Group(f, "element/move").Single().Position == f.Actors[100].Position, "Movement pressure remained on stale anchor.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(!f.Engine.RequireWolMovement && f.Engine.Active != null, "Death retained personal movement or erased encounter.");
        });
        run("WarriorOfLight towers expose one earliest activation with stable ties and map removal all matches", () =>
        {
            using var f = Scene();
            f.Engine.MapEffect(2, 0x00020001, f.Start.AddSeconds(1));
            f.Engine.MapEffect(0, 0x00020001, f.Start);
            f.Engine.MapEffect(4, 0x00020001, f.Start); f.Render();
            var north = Group(f, "tower").Single(a => a.Display);
            Check(north.Shape is CircleTemplate { Radius: 3 } && north.Position.Z < 90 && north.Position.X > 99.9f,
                "Earliest tower or map radius differs.");
            At(f, 30); Check(Group(f, "tower").Single(a => a.Display).Position == north.Position, "Estimated time removed map tower.");
            f.Engine.Effect(10, 20258, 1, f.Now); f.Render(); Check(Group(f, "tower").Single(a => a.Display).Position == north.Position, "Effect consumed map tower.");
            f.Engine.MapEffect(0, 0x00080004, f.Now); f.Render();
            Check(Group(f, "tower").Single(a => a.Display).Position.Z > 110, "Same-time stable insertion order lost.");
            f.Engine.MapEffect(4, 0x00080004, f.Now); f.Render();
            Check(Group(f, "tower").Single(a => a.Display).Position.X > 110, "Next activation tower unavailable.");
        });
        run("WarriorOfLight cast tower radius four capacity five coexists with map tower exact capacity two", () =>
        {
            using var f = Scene(); Start(f, 10, 20258);
            f.Engine.MapEffect(8, 0x00020001, f.Now); f.Render();
            Check(Group(f, "tower").Single(a => a.Display).Shape is CircleTemplate { Radius: 4 }, "Cast path inherited map radius.");
            End(f, 10, 20258);
            Check(Group(f, "tower").Single(a => a.Display).Shape is CircleTemplate { Radius: 3 }, "Cast finish erased map tower.");
            f.Engine.MapEffect(8, 0x00020001, f.Now); f.Engine.MapEffect(8, 0x00080004, f.Now); f.Render();
            Check(!Group(f, "tower").Any(), "Position removal did not clear duplicate towers.");
        });
        run("WarriorOfLight brimstone keeps sixteen radius zero onward but exposes first four plus normal cast", () =>
        {
            using var f = Scene(); Start(f, 10, 20254);
            var growing = Group(f, "brimstone");
            Check(growing.Length == 4 && growing[0].Shape is WarriorOfLightZeroCircleTemplate
                && growing.Skip(1).Select(a => ((CircleTemplate)a.Shape).Radius).SequenceEqual([1f, 2, 3])
                && growing[0].Activation == f.Start.AddSeconds(5) && growing[3].Activation == f.Start.AddSeconds(8)
                && f.Draw.Previews["cast/10"].Geometry.Contains(new(100, 0, 105.9f)), "Growth initial queue differs.");
            At(f, 7); End(f, 10, 20254);
            Check(((CircleTemplate)Group(f, "brimstone")[0].Shape).Radius == 3, "Finish failed <=now pruning.");
            f.Engine.Effect(10, 20255, 1, f.Start.AddSeconds(9)); f.Render();
            Check(((CircleTemplate)Group(f, "brimstone")[0].Shape).Radius == 5, "Pulse did not prune all due entries.");
            At(f, 100); Check(Group(f, "brimstone").Length == 4, "Time alone consumed growth.");
        });
        run("WarriorOfLight brimstone interleaves callbacks and only BrimstoneActor destruction clears all", () =>
        {
            using var f = Scene(); Start(f, 10, 20254); At(f, 1); Start(f, 11, 20254);
            Check(Group(f, "brimstone").Select(a => a.Activation).SequenceEqual(Group(f, "brimstone").Select(a => a.Activation).Order()), "Growth not sorted globally.");
            f.Actors[20] = new(20, 0x1EAFFE, new(110, 0, 110), Dead: true); f.Scan();
            Check(Group(f, "brimstone").Any(), "Brimstone death cleared growth.");
            f.Engine.RemoveActor(20); f.Scan();
            Check(!Group(f, "brimstone").Any(), "Brimstone destruction did not clear global queue.");
        });
        run("WarriorOfLight sword derives inward cones from maps and consumes only due effects", () =>
        {
            using var f = Scene(); f.Engine.Effect(1, 20290, 1, f.Now);
            Check(!Group(f, "sword").Any(), "Sword visual created range.");
            f.Engine.MapEffect(0x14, 0x00020001, f.Now);
            f.Engine.MapEffect(0x15, 0x00020001, f.Start.AddSeconds(5)); f.Render();
            var north = Group(f, "sword").First();
            var geometry = north.Shape.Resolve(north.Position, north.Rotation, null);
            Check(north.Activation == f.Start.AddSeconds((double)2.7f) && geometry.Contains(new(100, 0, 100))
                && HostDrawingGeometry.Convert(geometry).Contains(new(100, 0, 100)), "Map sword shape/clock/world conversion differs.");
            f.Engine.Effect(10, 20262, 2, f.Start.AddSeconds(3)); f.Render();
            Check(Group(f, "sword").Length == 1 && Group(f, "sword")[0].Position.X > 119.9f, "Sword erased not-yet-due range.");
            At(f, 100); Check(Group(f, "sword").Length == 1, "Sword timeout invented.");
        });
        run("WarriorOfLight holy uses actor target two seconds min six and only existing first stack excludes proximity player", () =>
        {
            using var f = Scene(); f.Engine.Icon(100, 87, f.Now); f.Engine.Icon(101, 161, f.Now, 102); f.Render();
            Check(Group(f, "stack/20237").Single(a => a.Display).Position.Z < 90.1f
                && !f.Engine.PredictedDamage.Any(p => p.DamageType == "Shared"), "Holy target actor or default mask differs.");
            f.Engine.Icon(100, 87, f.Now); f.Render();
            Check(Group(f, "stack/20237").Any(a => a.Id.EndsWith("/avoid") && a.Activation == f.Start.AddSeconds(2))
                && f.Engine.PredictedDamage.Any(p => p.DamageType == "Shared")
                && !f.Engine.PredictedDamage.Any(p => p.TargetId == 100 && p.DamageType == "Shared"), "Holy exclusion not applied to existing first stack.");
            f.Engine.Icon(101, 161, f.Now); f.Engine.Effect(10, 20237, 1, f.Now, 999); f.Render();
            Check(Group(f, "stack/20237").Count(a => a.Display) == 1, "Unknown effect target did not consume first holy.");
            Start(f, 10, 20253, target: 101); f.Engine.Effect(10, 20253, 2, f.Now); f.Render();
            Check(!Group(f, "stack/20237").Any() && Group(f, "stack/20253").Any(), "Katon effect failed independent holy clear.");
            End(f, 10, 20253); Check(!Group(f, "stack/20253").Any(), "Katon finish leaked.");
        });
        run("WarriorOfLight braver uses arena center dummy source and three FIFO resolving actions", () =>
        {
            using var f = Scene(); f.Actors[1] = f.Actors[1] with { Position = new(80, 0, 100) }; f.Scan();
            f.Engine.Icon(101, 234, f.Now); f.Engine.Icon(102, 234, f.Now); f.Engine.Icon(103, 234, f.Now); Start(f, 10, 21076);
            Check(Group(f, "bait/21076").Where(a => a.Display).All(a => a.Position.X == 100 && a.Activation == null)
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(6))
                && f.Draw.Previews.ContainsKey("cast/10"), "Braver dummy center or independent cleave missing.");
            foreach (var action in new[] { 21076u, 20246u, 20247u }) f.Engine.Effect(10, action, action, f.Now, 107);
            f.Render(); Check(!Group(f, "bait/21076").Any() && f.Draw.Previews.ContainsKey("cast/10"), "Braver FIFO affects cast or leaks.");
        });
        run("WarriorOfLight desperado resolves selected TargetID of dynamic marked actors instead of marker IDs", () =>
        {
            using var f = Scene(); f.Actors[101] = f.Actors[101] with { TargetId = 777 }; f.Actors[102] = f.Actors[102] with { TargetId = 778 }; f.Scan();
            f.Engine.Effect(10, 20294, 1, f.Now, 101); f.Engine.Effect(11, 20294, 2, f.Now, 102); f.Render();
            var line = Group(f, "line").First(a => a.Display);
            Check(line.Shape is RayRectangleTemplate && f.Engine.PredictedDamage.All(p => p.DamageType == "Shared"), "Line geometry or prediction missing.");
            f.Engine.Effect(10, 20248, 3, f.Now, 101); f.Render();
            Check(Group(f, "line").Count(a => a.Display) == 2, "Source quirk replaced with marker-ID matching.");
            f.Engine.Effect(10, 20249, 4, f.Now, 777); f.Render(); Check(Group(f, "line").Count(a => a.Display) == 1, "Selected TargetID did not consume line.");
            f.Engine.Effect(10, 20248, 5, f.Now, 999); f.Render(); Check(!Group(f, "line").Any(), "Last line did not clear unconditionally.");
        });
        run("WarriorOfLight flare tethers both directions share initial clock remain through effect and draw separate cast", () =>
        {
            using var f = Scene(); f.Engine.Tether(100, 17, true, f.Now, 20);
            At(f, 2); f.Engine.Tether(21, 17, true, f.Now, 101); Start(f, 10, 20260);
            Check(Group(f, "bait/20260").Count(a => a.Id.EndsWith("/tether")) == 2
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start && p.DamageType == "Tankbuster")
                && f.Draw.Previews.ContainsKey("cast/10"), "Flare batch clock, sides, drawing or cast differs.");
            f.Engine.Effect(20, 20260, 1, f.Now); f.Render(); Check(Group(f, "bait/20260").Count(a => a.Id.EndsWith("/display")) == 2, "Hit consumed healthy tether.");
            f.Engine.Tether(100, 17, false, f.Now, 20); f.Engine.Tether(21, 17, false, f.Now, 101);
            f.Engine.Effect(20, 20260, 2, f.Now); At(f, 10); f.Engine.Tether(100, 17, true, f.Now, 20); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(10), "Empty effect failed batch reset.");
        });
        run("WarriorOfLight bitter end awaits hit with default raidwide type and death removes permanently", () =>
        {
            using var f = Scene(); Start(f, 10, 20264, target: 101); End(f, 10, 20264);
            Check(Group(f, "bait/20264").Any() && f.Engine.PredictedDamage.Single().DamageType == "Raidwide", "BitterEnd source parameter mapping differs.");
            f.Engine.Effect(10, 20264, 1, f.Now); f.Render(); Check(!Group(f, "bait/20264").Any(), "BitterEnd actual hit leaked.");
            At(f, 1); Start(f, 10, 20264, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(!Group(f, "bait/20264").Any(), "EndsOnCastEvent Update did not permanently prune dead target.");
        });
        run("WarriorOfLight deluge and meteor use radius twenty corners with rolling time independent consumption", () =>
        {
            using var f = Scene(); f.Engine.Icon(100, 87, f.Now); Start(f, 10, 20251, target: 100);
            Check(Group(f, "bait/20256").Single(a => a.Display).Shape is CircleTemplate { Radius: 20 }
                && Group(f, "spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 20 }
                && Group(f, "corners").Single().Activation == f.Start.AddSeconds(5), "Proximity/spread radius or corners missing.");
            At(f, 2); Check(Group(f, "corners").Single().Activation == f.Start.AddSeconds(7), "Corner time is not rolling.");
            f.Engine.Effect(10, 20256, 1, f.Now); f.Render();
            Check(!Group(f, "bait/20256").Any() && Group(f, "spread").Any(), "Deluge consumed meteor.");
            End(f, 10, 20251); Check(!Group(f, "spread").Any() && !Group(f, "corners").Any(), "Meteor finish leaked.");
        });
        run("WarriorOfLight suiton directional thirty FIFO first cast strip and strict expiry immunity", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 85) }; f.Scan(); Start(f, 10, 20252);
            var path = Group(f, "knockback").Single(a => a.Id.EndsWith("/landing"));
            Check(path.Position == new Vector3(100, 0, 115) && Group(f, "suiton/join").Single().Activation == null,
                "Suiton direction, distance or immediate strip differs.");
            Check(!Group(f, "suiton/landing").Any(), "Source has no added landing AI constraint.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(5.3) } }; f.Scan();
            Check(Group(f, "suiton").Any(), "Equal-expiry immunity incorrectly applied.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(6) } }; f.Scan();
            Check(!Group(f, "suiton").Any() && !Group(f, "knockback").Any(), "Valid immunity retained displacement.");
        });
        run("WarriorOfLight decimation exposes four FIFO casters and ordinary shapes end independently", () =>
        {
            using var f = Scene();
            for (ulong i = 30; i < 36; ++i) { f.Actors[i] = new(i, 0x233C, new(100, 0, 100)); f.Scan(); Start(f, i, 20259); }
            Check(Group(f, "decimation").Length == 4 && !f.Draw.Previews.Keys.Any(k => k.StartsWith("cast/3")), "Decimation visibility limit duplicated or lost.");
            End(f, 30, 20259); Check(Group(f, "decimation").Length == 4, "Next decimation did not enter after finish.");
            Start(f, 10, 20261); Start(f, 11, 20241);
            Check(f.Draw.Previews["cast/10"].Geometry.Contains(new(109.9f, 0, 139))
                && !f.Draw.Previews["cast/10"].Geometry.Contains(new(110.1f, 0, 139))
                && !f.Draw.Previews["cast/11"].Geometry.Contains(new(100, 0, 103)), "Normal rectangle/donut differs.");
            At(f, 100); Check(f.Draw.Previews.ContainsKey("cast/10"), "Normal cast ended on timeout.");
            End(f, 10, 20261); Check(f.Draw.Previews.ContainsKey("cast/11"), "Independent cast was cleared.");
        });
        run("WarriorOfLight watched raidwides are cast-only and no source TODO becomes execution", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 20263u, 21628u, 21297u, 20265u })
            { Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 8, "Raidwide missing."); End(f, 10, action); }
            f.Engine.Icon(100, 227, f.Now); f.Engine.Icon(100, 233, f.Now);
            f.Engine.StatusChanged(100, 2090, true, f.Now); f.Engine.StatusChanged(100, 2303, true, f.Now);
            Check(!f.Engine.RequireAllStop && f.Engine.ForbiddenTargets.Length == 0 && !Group(f, "spread").Any(), "Comment-only mechanic invented execution.");
        });
        run("WarriorOfLight low surviving party retains forbidden stack navigation and source outline styles", () =>
        {
            using var f = Scene();
            for (ulong i = 105; i < 108; ++i) f.Actors[i] = f.Actors[i] with { Dead = true };
            f.Scan(); f.Engine.Icon(100, 161, f.Now); f.Engine.Icon(100, 87, f.Now); Start(f, 10, 20251, target: 101);
            Check(Group(f, "stack/20237").Single(a => a.Display) is { Safe: false, OutlineOnly: true }
                && Group(f, "spread").Single(a => a.Display).OutlineOnly, "Forbidden own stack or other spread outline differs.");
            f.Engine.Effect(10, 20237, 1, f.Now, 100);
            f.Engine.Icon(101, 161, f.Now); f.Engine.Icon(100, 87, f.Now); f.Render();
            Check(Group(f, "stack/20237").Any(a => a.Id.EndsWith("/avoid")), "Five-person forbidden stack lost avoidance.");
            End(f, 10, 20251); f.Engine.MapEffect(8, 0x00020001, f.Now);
            for (ulong i = 100; i < 103; ++i) f.Actors[i] = f.Actors[i] with { Position = new(104, 0, 100) };
            f.Scan();
            Check(Group(f, "tower").Single(a => a.Display) is { Safe: false, OutlineOnly: true }, "Inside overfull tower lost danger outline.");
        });
        run("WarriorOfLight corrected cast follows runtime finish-start semantics for growth and effect-ended FIFO", () =>
        {
            using var f = Scene(); Start(f, 10, 20254); var cast = f.Actors[10].Cast!;
            f.Now = f.Start.AddSeconds(.2); f.Engine.StartCast(10, cast with { Location = cast.Location + Vector3.UnitX }, f.Now);
            f.Now = f.Start.AddSeconds(5); f.Engine.Effect(10, 20255, 1, f.Now); f.Render();
            Check(Group(f, "brimstone").Count(a => a.Shape is WarriorOfLightZeroCircleTemplate) == 1,
                "Correction failed to append second callback clock.");
            End(f, 10, 20254); Start(f, 11, 20264, target: 101); var bitter = f.Actors[11].Cast!;
            f.Engine.StartCast(11, bitter with { TargetId = 102 }, f.Now); f.Engine.Effect(11, 20264, 2, f.Now); f.Render();
            Check(Group(f, "bait/20264").Single(a => a.Display).Position == f.Actors[102].Position,
                "Effect-ended finish-start FIFO did not retain corrected target.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x2DDD, 922); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i)
            f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + new Vector3((i % 4 - 1) * 10, 0, (i / 4 - 1) * 10), PartySlot: i, Hostile: false);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c);
        f.Actors[20] = new(20, 0x2DEB, c - Vector3.UnitX * 15);
        f.Actors[21] = new(21, 0x2DEB, c + Vector3.UnitX * 15); f.Scan(); return f;
    }
    private static void At(Fight f, double time) { f.Now = f.Start.AddSeconds(time); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes()
        .Where(a => a.Id.StartsWith("wol/" + prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
