using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class ValigarmandaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Valigarmanda native identity fixed rectangle and primary death cleanup", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.valigarmanda") && f.Engine.Arena!.Contains(new(119.9f, 0, 114.9f))
                && !f.Engine.Arena.Contains(new(120.1f, 0, 100)), "Identity/rectangle.");
            Start(f, 10, 36191); f.Engine.RemoveActor(1); f.Render();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.ArenaBoundary == null, "Primary removal cleanup.");
        });
        run("Valigarmanda arcane sphere creation snapshots quantized position and double delay until any cast finish", () =>
        {
            using var f = Scene(); f.Actors[30] = new(30, 0x4181, new(103.004f, 0, 101.001f), Rotation: .31f); f.Scan();
            var expected = new BossMod.WPos(103.004f, 101.001f).Quantized(); var aoe = Group(f, "lightning").Single();
            Check(aoe.Position == new Vector3(expected.X, 0, expected.Z) && aoe.Rotation == .31f
                && aoe.Activation == f.Start.AddSeconds(8.6) && aoe.Shape is RayRectangleTemplate { Length: 50 }, "Creation snapshot.");
            f.Actors[30] = f.Actors[30] with { Position = new(110, 0, 110), Dead = true }; At(f, 100);
            f.Engine.RemoveActor(30); f.Actors.Remove(30); f.Scan();
            Check(Group(f, "lightning").Single() == aoe, "Object lifetime consumed prediction.");
            Hit(f, 10, 39001, 1); Check(Group(f, "lightning").Any(), "Effect cleared instead of finish.");
            Start(f, 10, 39001); End(f, 10, 39001); Check(!Group(f, "lightning").Any(), "Finish did not clear.");
        });
        run("Valigarmanda coil single slot suppresses both views and navigation while lightning exists", () =>
        {
            using var f = Scene(); Start(f, 10, 36160);
            Check(Group(f, "coil").Single().Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 30 }, "Coil.");
            f.Actors[30] = new(30, 0x4181, new(100, 0, 100)); f.Scan(); Start(f, 11, 36156);
            Check(!Group(f, "coil").Any() && !f.Draw.Previews.ContainsKey("valig/coil"), "Suppressed coil still output.");
            Start(f, 12, 39001); End(f, 12, 39001);
            Check(Group(f, "coil").Single().Shape is FanTemplate { Radius: 50 }, "Single-slot overwrite lost while hidden.");
            End(f, 10, 36160); Check(!Group(f, "coil").Any(), "Any watched finish failed clear current slot.");
        });
        run("Valigarmanda chilling sphere creates exact slightly offset cross angles with independent clock", () =>
        {
            using var f = Scene(); f.Actors[30] = new(30, 0x4493, new(100, 0, 100), Rotation: 1); f.Scan();
            var list = Group(f, "chilling");
            Check(list.Length == 2 && list[0].Rotation == BossMod.Angle.AnglesCardinals[1].Rad
                && list[1].Rotation == BossMod.Angle.AnglesIntercardinals[1].Rad
                && list.All(a => a.Activation == f.Start.AddSeconds(7.1)), "Cross angles/time.");
            Hit(f, 10, 39265, 1); Check(Group(f, "chilling").Length == 2, "Cross effect consumed.");
            Start(f, 10, 39265); End(f, 10, 39265); Check(!Group(f, "chilling").Any(), "Cross finish.");
        });
        run("Valigarmanda northern cross map clock exact coordinates overwrite and effect only cleanup", () =>
        {
            using var f = Scene(); f.Engine.MapEffect(2, 0x00200010, f.Now); f.Render();
            Check(Group(f, "northern").Single() is { Position.X: 131.487f, Position.Z: 107.988f } a
                && a.Activation == f.Start.AddSeconds(9.2), "Map first branch.");
            f.Engine.MapEffect(2, 0x00020001, f.Now); f.Render();
            Check(Group(f, "northern").Single() is { Position.X: 116.472f, Position.Z: 127.977f }, "Map overwrite.");
            f.Engine.MapEffect(2, 99, f.Now); At(f, 50); Check(Group(f, "northern").Any(), "Unknown map/time cleared.");
            Hit(f, 10, 36168, 1); Check(!Group(f, "northern").Any(), "Northern effect clear.");
        });
        run("Valigarmanda freezing activation includes finish plus one with indefinite movement and central constraint", () =>
        {
            using var f = Scene(); Start(f, 1, 36177);
            Check(Group(f, "freezing/center").Single().Activation == f.Start.AddSeconds(6.3) && !f.Engine.RequireValigMovement, "Freeze activation.");
            At(f, 3.2); Check(!f.Engine.Hints(f.Actors[100].Position).Contains("冻结粉尘：持续移动"), "Hint appeared more than three seconds early.");
            At(f, 3.4); Check(f.Engine.Hints(f.Actors[100].Position).Contains("冻结粉尘：持续移动"), "Hint window.");
            End(f, 1, 36177); At(f, 6.4);
            Check(f.Engine.RequireValigMovement && Group(f, "freezing/move").Any()
                && Group(f, "freezing").All(a => !a.Display), "Move execution/hidden constraint.");
            At(f, 100); Check(f.Engine.RequireValigMovement, "Freeze timed out without status.");
        });
        run("Valigarmanda freezing status loss and deep freeze gain clear only corresponding slot", () =>
        {
            using var f = Scene(); Start(f, 1, 36177); At(f, 10);
            f.Engine.StatusChanged(101, 3523, false, f.Now); Check(f.Engine.RequireValigMovement, "Other slot cleared current.");
            f.Engine.StatusChanged(100, 3523, true, f.Now); Check(f.Engine.RequireValigMovement, "Gain cleared state.");
            f.Engine.StatusChanged(100, 3523, false, f.Now); f.Render();
            Check(!f.Engine.RequireValigMovement && !Group(f, "freezing").Any(), "Lose failed clear.");
            At(f, 11); Start(f, 1, 36177); At(f, 20);
            f.Engine.StatusChanged(100, 4150, true, f.Now); f.Render(); Check(!f.Engine.RequireValigMovement, "Deep freeze gain.");
        });
        run("Valigarmanda raw snapshot status transitions and personal death clear slot while retaining fight", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Statuses = [3523] }; f.Scan();
            Start(f, 1, 36177); At(f, 10);
            f.Actors[100] = f.Actors[100] with { Statuses = [] }; f.Scan(); Check(!f.Engine.RequireValigMovement, "Raw loss.");
            Start(f, 1, 36177); At(f, 20); f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null && !f.Engine.RequireValigMovement, "Personal death contract.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; f.Scan(); Check(!f.Engine.RequireValigMovement, "Revival resurrected move.");
        });
        run("Valigarmanda ground platform has six fixed cells and dynamic buddy circles only during visual", () =>
        {
            using var f = Scene();
            for (ulong i = 102; i < 108; ++i) f.Actors[i] = f.Actors[i] with { Position = new(100, 0, 90) };
            f.Actors[101] = f.Actors[101] with { Position = new(95, 0, 100) }; f.Scan();
            Start(f, 1, 36172);
            Check(Group(f, "platform").Length == 6 && Group(f, "bolt").Length == 1
                && Group(f, "bolt").Single().Activation == null, "Platform/buddy geometry.");
            End(f, 1, 36172); Check(Group(f, "platform").Length == 6 && !Group(f, "bolt").Any(), "Visual end cleared platform or retained circles.");
            Start(f, 10, 36174); End(f, 10, 36174); Check(!Group(f, "platform").Any(), "Actual finish failed platform clear.");
            At(f, 10); Start(f, 1, 36176); Check(!Group(f, "platform").Any(), "Invented missing requireLevitating branch.");
        });
        run("Valigarmanda tank towers use only allowed current soakers for prediction and role dependent navigation", () =>
        {
            using var f = Scene(); Start(f, 10, 36187);
            Check(Group(f, "tower/join").Any() && Group(f, "tower").Single(a => a.Display).Safe
                && f.Engine.PredictedDamage.Count() == 1 && f.Engine.PredictedDamage.Single().DamageType == "Raidwide", "Tank tower.");
            f.Actors[100] = f.Actors[100] with { PartyRole = "Melee" }; f.Scan();
            Check(!Group(f, "tower/join").Any() && Group(f, "tower").Any(a => a.Id.EndsWith("/avoid"))
                && !Group(f, "tower").Single(a => a.Display).Safe, "NonTank not forbidden.");
            End(f, 10, 36187); Check(!Group(f, "tower").Any(), "Tower finish.");
        });
        run("Valigarmanda ruinfall non tank preimage uses full distance inset while preview stops after wall", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { PartyRole = "Melee" }; f.Scan(); Start(f, 10, 36189);
            var join = Group(f, "ruinfall/join").Single().Shape.Resolve(new(100, 0, 100), 0, null);
            Check(join.Contains(new(100, 0, 100)) && !join.Contains(new(100, 0, 86)), "Expected outside-of-allowed shape.");
            Check(Group(f, "ruinfall").Any(a => a.Display), "Missing wall-clipped preview.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(5.3) } }; f.Scan();
            Check(Group(f, "ruinfall").Any(), "Immunity expiry equal activation counted.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(5.4) } }; f.Scan();
            Check(!Group(f, "ruinfall").Any(), "Valid immunity ignored.");
        });
        run("Valigarmanda tank in first tower requests arms length only under five seconds", () =>
        {
            using var f = Scene(); Start(f, 10, 36187); Start(f, 11, 36189, duration: 8);
            Check(!f.Engine.WantsValigKnockbackImmunity, "Too early action.");
            At(f, 3.4); Check(f.Engine.WantsValigKnockbackImmunity && !Group(f, "ruinfall/join").Any(), "Tank action/AI override.");
            f.Actors[100] = f.Actors[100] with { Position = new(112, 0, 100) }; f.Scan(); Check(!f.Engine.WantsValigKnockbackImmunity, "Outside tower requested.");
        });
        run("Valigarmanda independent line markers track geometry allow dead target and both consume corrected single target", () =>
        {
            using var f = Scene(); Hit(f, 10, 26708, 1, 101); Hit(f, 11, 34722, 2, 102);
            Check(Group(f, "line").Count(a => a.Display) == 2 && f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 16, "Independent line lists.");
            f.Actors[101] = f.Actors[101] with { Dead = true, Position = new(110, 0, 105) }; f.Scan();
            Check(Group(f, "line").Count(a => a.Display) == 2, "Dead target hidden despite allowDeadTargets.");
            Hit(f, 12, 36194, 3, 103); Check(!Group(f, "line").Any(), "Each component failed single-target correction/consume.");
        });
        run("Valigarmanda line limit break effect clears both lists but no ordinary time expiry", () =>
        {
            using var f = Scene(); Hit(f, 10, 26708, 1, 101); Hit(f, 11, 34722, 2, 102); At(f, 100);
            Check(Group(f, "line").Count(a => a.Display) == 2, "Line expired.");
            Hit(f, 1, 38323, 3); Check(!Group(f, "line").Any(), "Limit break clear.");
        });
        run("Valigarmanda ice talon resolves icon target fallback FIFO and permanently drops dead target", () =>
        {
            using var f = Scene(); f.Engine.Icon(102, 344, f.Now, actorId: 101);
            Check(Group(f, "talon").Single(a => a.Display).Position == f.Actors[102].Position, "Icon target precedence.");
            f.Engine.Icon(999, 344, f.Now, actorId: 103); f.Render();
            Hit(f, 12, 36185, 1, 103); Check(Group(f, "talon").Single(a => a.Display).Position == f.Actors[103].Position, "Not FIFO.");
            f.Actors[103] = f.Actors[103] with { Dead = true }; f.Scan();
            f.Actors[103] = f.Actors[103] with { Dead = false }; f.Scan(); Check(!Group(f, "talon").Any(), "Dead marker revived.");
        });
        run("Valigarmanda tulidisaster three independent predicted clocks survive visual finish until respective effects", () =>
        {
            using var f = Scene(); Start(f, 1, 36197);
            var times = f.Engine.PredictedDamage.Select(p => Math.Round((p.Activation - f.Start).TotalSeconds, 3)).Distinct().ToArray();
            Check(times.SequenceEqual([8.4, 16.9, 24.9]), "Three delay clocks.");
            End(f, 1, 36197); At(f, 100); Check(f.Engine.PredictedDamage.Count() == 24, "Visual finish/time cleared.");
            Hit(f, 10, 36200, 1); Check(f.Engine.PredictedDamage.Count() == 16, "Middle hit cleared other predictions.");
            Hit(f, 10, 36199, 2); Hit(f, 10, 36198, 3); Check(!f.Engine.PredictedDamage.Any(), "Final independent clears.");
        });
        run("Valigarmanda direct shape sizes ordinary raidwides and no invented facing shield or hold controls", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 36158u, 36195u, 36191u, 39129u, 36174u })
            {
                Start(f, 10, action); Check(f.Draw.Previews.ContainsKey("cast/10") && f.Snapshot.DangerAreas.Count > 0, "Direct shape output.");
                End(f, 10, action);
            }
            foreach (var action in new[] { 36162u, 36163u, 36361u, 36165u, 36167u, 38545u })
            {
                Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 8, "Ordinary raidwide.");
                End(f, 10, action);
            }
            Check(!f.Engine.RequireAllStop && !f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Invented restrictions.");
        });
        run("Valigarmanda actual event adapter forwards freezing loss gain and immunity packets ahead of raw snapshots", () =>
        {
            using var f = Scene(); Start(f, 1, 36177); At(f, 10);
            var adapter = new DungeonEventAdapter(f.Engine, f.Scan, _ => 0, _ => { });
            adapter.Handle(JsonSerializer.Deserialize<LogSystemStatusRemoveEvent>(JsonSerializer.Serialize(new
                { TargetId = 100UL, StatusId = 3523u, Timestamp = f.Now }))!, f.Now);
            Check(!f.Engine.RequireValigMovement, "StatusRemove not forwarded.");
            At(f, 11); Start(f, 1, 36177); At(f, 20);
            adapter.Handle(JsonSerializer.Deserialize<LogSystemStatusAddEvent>(JsonSerializer.Serialize(new
                { SourceId = 1UL, TargetId = 100UL, StatusId = 4150u, Duration = 30f, Timestamp = f.Now }))!, f.Now);
            Check(!f.Engine.RequireValigMovement, "StatusAdd not forwarded.");
            Start(f, 10, 36189);
            adapter.Handle(JsonSerializer.Deserialize<LogSystemStatusAddEvent>(JsonSerializer.Serialize(new
                { TargetId = 100UL, StatusId = 1209u, Duration = 30f, Timestamp = f.Now }))!, f.Now); f.Render();
            Check(!Group(f, "ruinfall").Any(), "Immunity gain packet ignored.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Now.AddSeconds(30) } };
            adapter.Handle(JsonSerializer.Deserialize<LogSystemStatusRemoveEvent>(JsonSerializer.Serialize(new
                { TargetId = 100UL, StatusId = 1209u, Timestamp = f.Now }))!, f.Now); f.Scan();
            Check(Group(f, "ruinfall").Any(), "Immunity loss packet failed override old raw.");
        });
        run("Valigarmanda freezing raw source change and refreshed deep freeze preserve upstream gain lose semantics", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Statuses = [3523], StatusSources = new Dictionary<uint, ulong> { [3523] = 1 } }; f.Scan();
            Start(f, 1, 36177); At(f, 10);
            f.Actors[100] = f.Actors[100] with { StatusSources = new Dictionary<uint, ulong> { [3523] = 2 } }; f.Scan();
            Check(!f.Engine.RequireValigMovement, "Source-only Lose did not clear.");
            f.Actors[100] = f.Actors[100] with { Statuses = [4150], StatusExpirations = new Dictionary<uint, DateTime> { [4150] = f.Now.AddSeconds(20) } }; f.Scan();
            Start(f, 1, 36177); At(f, 20); Check(f.Engine.RequireValigMovement, "Existing deep freeze unexpectedly retriggered.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [4150] = f.Now.AddSeconds(30) } }; f.Scan();
            Check(!f.Engine.RequireValigMovement, "Expiry extension did not emit gain.");
        });
        run("Valigarmanda actor destruction emits freezing up loss before slot reuse", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Statuses = [3523] }; f.Scan();
            Start(f, 1, 36177); At(f, 10);
            f.Engine.RemoveActor(100); f.Actors.Remove(100);
            f.Actors[100] = new(100, 0, new(100, 0, 100), PartySlot: 0, Hostile: false); f.Scan();
            Check(!f.Engine.RequireValigMovement && !Group(f, "freezing").Any(), "New generation inherited old slot state.");
        });
        run("Valigarmanda source changing gain packet emits loss before raw acknowledgement", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Statuses = [3523], StatusSources = new Dictionary<uint, ulong> { [3523] = 1 } }; f.Scan();
            Start(f, 1, 36177); At(f, 10); Check(f.Engine.RequireValigMovement, "Move not armed.");
            f.Engine.StatusChanged(100, 3523, true, f.Now, sourceId: 2);
            Check(!f.Engine.RequireValigMovement, "Gain packet omitted old-source Lose.");
            f.Actors[100] = f.Actors[100] with { StatusSources = new Dictionary<uint, ulong> { [3523] = 2 } }; f.Scan();
            Check(!f.Engine.RequireValigMovement, "Raw acknowledgement restored stale movement.");
        });
    }
    private static Fight Scene()
    {
        var f = new Fight(0x4115, 1195); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i,
            PartySlot: i, PartyRole: i < 2 ? "Tank" : "Melee", Hostile: false);
        f.Actors[101] = f.Actors[101] with { Position = new(110, 0, 100) };
        for (ulong i = 10; i < 30; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Scan(); return f;
    }
    private static void At(Fight f, double seconds) { f.Now = f.Start.AddSeconds(seconds); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, double duration = 5)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static void Hit(Fight f, ulong source, uint action, uint seq, ulong target = 0)
    { f.Engine.Effect(source, action, seq, f.Now, target); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("valig/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
