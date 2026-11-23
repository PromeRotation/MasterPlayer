using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class DarknessRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Darkness four bosses exact territories lifecycle arena geometry and wipe gates", () =>
        {
            Check(ArrDungeonCatalog.IsScript("masterplayer.alliance.darkness") && DarknessCatalog.Create().Length == 4, "Catalog.");
            foreach (var boss in new uint[] { 0xE00, 0xDFC, 0xDF6, 0xCFA })
            {
                using var f = Scene(boss); var c = f.Engine.Arena!.Position;
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PreserveFightAcrossPlayerDeath, "Native activation.");
                if (boss == 0xDF6)
                    Check(f.Engine.Arena.Contains(c + new Vector3(19.9f, 0, 39.9f))
                        && !f.Engine.Arena.Contains(c + new Vector3(20.1f, 0, 0))
                        && !f.Engine.Arena.Contains(c + new Vector3(0, 0, 40.1f)), "Cerberus rectangular arena.");
                else Check(f.Engine.Arena.Contains(c + Vector3.UnitX * 29.9f) && !f.Engine.Arena.Contains(c + Vector3.UnitX * 30.1f), "Circle arena.");
                f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(f.Engine.Active != null, "Personal death.");
                f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe repull.");
                f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
                f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0.");
            }
            using var wrong = new Fight(0xE00, 372); Check(wrong.Engine.Active == null, "Wrong territory.");
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0xE00, 3275, new BossMod.AOEShapeCircle(10)),
            (0xE00, 3277, new BossMod.AOEShapeCircle(10)),
            (0xDFC, 3295, new BossMod.AOEShapeCone(30, new(MathF.PI / 3))),
            (0xDFC, 3285, new BossMod.AOEShapeCircle(6)),
            (0xDFC, 3284, new BossMod.AOEShapeCircle(6)),
            (0xDFC, 3288, new BossMod.AOEShapeCircle(6)),
            (0xDFC, 3286, new BossMod.AOEShapeCircle(6)),
            (0xDF6, 3246, new BossMod.AOEShapeCone(19, new(MathF.PI / 4))),
            (0xDF6, 3241, new BossMod.AOEShapeCircle(8)),
            (0xDF6, 3249, new BossMod.AOEShapeCircle(9)),
            (0xDF6, 3250, new BossMod.AOEShapeRect(35, 3)),
            (0xDF6, 3251, new BossMod.AOEShapeRect(45, 3)),
            (0xCFA, 3297, new BossMod.AOEShapeRect(74, 12)),
        })
            run($"Darkness {boss:X} action{action} source geometry actual finish and dual views", () =>
            {
                using var f = Scene(boss); Start(f, 10, action, 3, rotation: .7f);
                var cast = f.Engine.Casts[10]; var shape = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (var x = -80f; x <= 80; x += .91f)
                    for (var z = -80f; z <= 80; z += .97f)
                        Check(shape.Contains(cast.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(cast.Location.X + x, cast.Location.Z + z), new(cast.Location.X, cast.Location.Z), new(.7f)), "Source geometry.");
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Views/navigation.");
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 1, "Nominal timeout.");
                f.Engine.Finish(10, action, f.Now, false); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Actual cancel.");
            });
        run("Darkness Cerberus charge freezes source-to-destination width length and computed rotation", () =>
        {
            foreach (var action in new uint[] { 3253, 3247 })
            {
                using var f = Scene(0xDF6); var c = f.Engine.Arena!.Position;
                var origin = c + new Vector3(-8.3f, 0, -12.7f); var end = c + new Vector3(9.3f, 0, 18.1f);
                f.Actors[10] = new(10, 0xDFB, origin); f.Scan(); Start(f, 10, action, 3, end, rotation: null);
                var cast = f.Engine.Casts[10]; var shape = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                var delta = end - origin; var width = action == 3253 ? 2 : 7;
                var expected = new BossMod.AOEShapeRect(delta.Length(), width);
                var source = new BossMod.WPos(origin.X, origin.Z).Quantized();
                for (var x = -30f; x <= 30; x += .71f)
                    for (var z = -40f; z <= 40; z += .73f)
                        Check(shape.Contains(c + new Vector3(x, 0, z), .00001f) == expected.Check(new(c.X + x, c.Z + z),
                            source, new(MathF.Atan2(delta.X, delta.Z))), "Charge source geometry.");
                f.Actors[10] = f.Actors[10] with { Position = end, Rotation = 2 }; f.Scan();
                Check(f.Engine.Casts[10].Location == cast.Location && f.Engine.Casts[10].Rotation == cast.Rotation, "Charge drift.");
            }
        });
        run("Darkness raidwide party predictions retain source cast clocks", () =>
        {
            foreach (var (boss, action) in new (uint, uint)[] { (0xE00, 3272), (0xDFC, 3289), (0xDFC, 3294), (0xDF6, 3254), (0xCFA, 3302) })
            {
                using var f = Scene(boss); Start(f, 10, action, 5);
                Check(f.Engine.PredictedDamage.Single().Activation == f.Now.AddSeconds(5.3) && f.Snapshot.DangerAreas.Count == 0, "Raidwide.");
            }
        });
        run("Darkness gaze locks eye snapshot joins forbidden directions respects source default range and cleanup", () =>
        {
            using var f = Scene(0xE00); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c, Rotation = MathF.PI / 2 }; f.Scan();
            Start(f, 10, 3281, 3, c + Vector3.UnitX * 10);
            Check(f.Engine.Gazes.Count() == 1 && f.Engine.GazeFacing(c, MathF.PI / 2) == null, "Facing too early.");
            Check(Group(f, "darkness/gaze").Length == 3 && f.Engine.Hints(c).Contains("背对死亡视线"), "Eye/arc/hint.");
            f.Now = f.Now.AddSeconds(2.6); f.Engine.Tick(f.Now);
            var facing = f.Engine.GazeFacing(c, MathF.PI / 2);
            Check(facing.HasValue && MathF.Cos(facing.Value - MathF.PI / 2) < .707107f, "Lookaway.");
            Check(f.Engine.GazeFacing(c + Vector3.UnitX * 20000, MathF.PI / 2) == null, "Source 10000 range.");
            f.Actors[100] = f.Actors[100] with { Rotation = -MathF.PI / 2 }; f.Scan();
            Check(!f.Engine.Hints(c).Contains("背对死亡视线")
                && Group(f, "darkness/gaze").Single(a => a.Id.EndsWith("/eye")).Safe, "Safe facing still warned.");
            f.Engine.Finish(10, 3281, f.Now, false); f.Render();
            Check(!f.Engine.Gazes.Any() && Group(f, "darkness/gaze").Length == 0 && f.Draw.Previews.Count == 0, "Gaze cancel owner.");
        });
        run("Darkness chase requires icon target and starts at locked location with radius10 rather than enum8", () =>
        {
            using var f = Scene(0xCFA); var c = f.Engine.Arena!.Position;
            Start(f, 10, 3298, 3, c); Check(Chases(f).Length == 0, "Invented nearest party fallback.");
            f.Engine.Finish(10, 3298, f.Now, true); f.Now = f.Now.AddSeconds(1);
            f.Engine.Icon(100, 197, f.Now); Start(f, 10, 3298, 3, c);
            var display = Chases(f).Single(a => a.Id.EndsWith("/display"));
            Check(display.Position == Quantized(c) && display.Shape is CircleTemplate { Radius: 10 }
                && display.Activation == f.Now.AddSeconds(3.3), "First hit geometry/clock.");
            var avoid = Chases(f).Single(a => a.Id.EndsWith("/avoid"));
            Check(avoid.Shape is CircleTemplate { Radius: 14 } && avoid.Position == display.Position - Vector3.UnitZ * 10, "Own chase AI hack.");
            var cast = f.Engine.Casts[10];
            f.Engine.StartCast(10, new(3298, c + Vector3.UnitX, 0, cast.StartedAt.AddSeconds(.05), cast.FinishAt), f.Now);
            Check(Chases(f).Count(a => a.Id.EndsWith("/display")) == 1, "Packet correction duplicated chaser.");
        });
        run("Darkness chase exact five hits nearest event matching move4 interval1.5 and no elapsed expiry", () =>
        {
            using var f = Chase(); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 20 }; f.Scan();
            f.Engine.Effect(10, 3298, 1, f.Now, location: c); f.Render();
            Check(Display(f).Name.Contains("4") && Display(f).Position == Quantized(c + Vector3.UnitX * 4)
                && Display(f).Activation == f.Now.AddSeconds(1.5), "After first hit.");
            for (var i = 1; i <= 3; ++i)
            {
                var previous = Display(f).Position;
                var from = new BossMod.WPos(previous.X, previous.Z);
                var offset = new BossMod.WPos(f.Actors[100].Position.X, f.Actors[100].Position.Z) - from;
                var expected = (from + 4 * offset / offset.Length()).Quantized();
                f.Now = f.Now.AddSeconds(1.5);
                f.Engine.Effect(11, 3299, (uint)(i + 1), f.Now, location: previous); f.Render();
                Check(Display(f).Name.Contains((4 - i).ToString())
                    && Display(f).Position == new Vector3(expected.X, previous.Y, expected.Z),
                    $"Followup {i}: {Display(f).Name}, position {Display(f).Position}, expected {expected}.");
            }
            f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now); f.Render(); Check(Chases(f).Length > 0, "Elapsed timeout.");
            f.Engine.Effect(11, 3299, 10, f.Now, location: Display(f).Position); f.Render();
            Check(Chases(f).Length == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Fifth hit cleanup.");
        });
        run("Darkness chase picks nearest marked target preserves concurrent sequences and switches personal avoidance", () =>
        {
            using var f = Scene(0xCFA); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 15 };
            f.Actors[101] = new(101, 0, c - Vector3.UnitX * 15, PartySlot: 1, Hostile: false); f.Scan();
            f.Engine.Icon(100, 197, f.Now); f.Engine.Icon(101, 197, f.Now);
            Start(f, 10, 3298, 3, c + Vector3.UnitX * 15); Start(f, 11, 3298, 5, c - Vector3.UnitX * 15);
            Check(Chases(f).Count(a => a.Id.EndsWith("/display")) == 2, "Concurrent chasers.");
            f.Engine.Effect(12, 3299, 1, f.Now, location: c - Vector3.UnitX * 15); f.Render();
            Check(Chases(f).Single(a => a.Id.EndsWith("/display") && a.Position.X < c.X).Name.Contains("4")
                && Chases(f).Single(a => a.Id.EndsWith("/display") && a.Position.X > c.X).Name.Contains("5"), "Nearest event selection.");
            f.PlayerId = 101; f.Scan();
            Check(Chases(f).Single(a => a.Id.EndsWith("/avoid") && a.Position.X < c.X).Shape is CircleTemplate { Radius: 14 }
                && Chases(f).Single(a => a.Id.EndsWith("/avoid") && a.Position.X > c.X).Shape is CircleTemplate { Radius: 11 }, "Perspective radius.");
        });
        run("Darkness chase keeps dead target first hit cancels after hit and owner cleanup releases movement", () =>
        {
            using var f = Chase();
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(Chases(f).Length > 0, "Dead target initial hit removed.");
            f.Engine.Effect(10, 3298, 1, f.Now, location: Display(f).Position);
            f.Engine.Tick(f.Now); f.Render(); Check(Chases(f).Length == 0, "Dead target followups retained.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; f.Scan();
            f.Now = f.Now.AddSeconds(1); f.Engine.Icon(100, 197, f.Now); Start(f, 11, 3298, 3);
            f.Output.Clear(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Owner cleanup.");
        });
        run("Darkness chase real solver exits predicted own circle", () =>
        {
            using var f = Chase(); var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            var avoid = Chases(f).Single(a => a.Id.EndsWith("/avoid"));
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 1
                && !avoid.Shape.Resolve(avoid.Position, avoid.Rotation, null).Contains(path[^1].Position), "Chase route.");
        });
    }

    private static Fight Chase()
    {
        var f = Scene(0xCFA);
        f.Actors[100] = f.Actors[100] with { Position = f.Engine.Arena!.Position }; f.Scan();
        f.Engine.Icon(100, 197, f.Now); Start(f, 10, 3298, 5); return f;
    }
    private static Fight Scene(uint boss)
    {
        var f = new Fight(boss, DarknessCatalog.TERRITORY); var c = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = c, Hp = 1000, MaxHp = 1000 };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 8, PartySlot: 0, PartyRole: "Tank", Hostile: false);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, double seconds, Vector3? location = null, float? rotation = 0)
    {
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x1B2, f.Engine.Arena!.Position); f.Scan(); }
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static Vector3 Quantized(Vector3 p)
    {
        var source = new BossMod.WPos(p.X, p.Z).Quantized();
        return new(source.X, p.Y, source.Z);
    }
    private static DungeonAmbientAoe Display(Fight f) => Chases(f).Single(a => a.Id.EndsWith("/display"));
    private static DungeonAmbientAoe[] Chases(Fight f) => Group(f, "darkness/chase/");
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
