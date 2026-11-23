using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class ZoraalJaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("ZoraalJa identity rotated arena first phase untargetable handoff and checkpoint restart", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.zoraal-ja") && ArrDungeonCatalog.Owns(0x42B4, 1200)
                && f.Engine.Arena!.Contains(new(126, 0, 100)) && !f.Engine.Arena.Contains(new(119, 0, 119)), "Identity/rotated square.");
            Start(f, 10, 37714); Check(Group(f, "sword").Any(), "P1 missing.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "P1 must finish on untargetable.");
            f.Actors[2] = new(2, 0x42B4, new(100, 0, 100), InCombat: true, Hp: 100); f.Scan();
            Check(f.Engine.Active?.BossOid == 0x42B4, "P2 handoff.");
            f.Engine.MapEffect(0x20, 0x00080004);
            f.Actors[2] = f.Actors[2] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null, "P2 death.");
            f.Actors.Remove(1); f.Actors.Remove(2);
            f.Actors[3] = new(3, 0x42B4, new(100, 0, 100), InCombat: true, Hp: 100); f.Scan();
            Check(f.Engine.Active?.BossOid == 0x42B4 && f.Engine.Arena!.Contains(new(126, 0, 100)), "Checkpoint needs fresh default arena.");
        });
        run("ZoraalJa checkpoint loads independently personal death persists and zero HP ends phase", () =>
        {
            using var f = Scene(true); Start(f, 10, 37741);
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null && f.Engine.OutputCasts.Any(), "Personal death ended phase.");
            f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Zero HP did not end phase.");
        });
        run("ZoraalJa double edged swords all previews only head navigates and finish is FIFO", () =>
        {
            using var f = Scene(); Start(f, 10, 37714, rotation: 0); Start(f, 11, 37714, rotation: MathF.PI);
            var items = Group(f, "sword");
            Check(items.Length == 2 && items.Count(a => a.Navigate) == 1 && items[0].Color?.X == 1
                && items[1].Shape is FanTemplate { Radius: 30, AngleRadians: MathF.PI }, "Visual/risky distinction.");
            f.Now = f.Start.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "sword").Length == 2, "Timeout consumed swords.");
            End(f, 11, 37714);
            Check(Group(f, "sword").Single() is { Rotation: MathF.PI, Color: null }, "Finish must consume first without recoloring the next.");
            End(f, 10, 37714); Check(!Group(f, "sword").Any(), "Last sword.");
        });
        run("ZoraalJa double edged and chasm packet correction retains list position without duplicate", () =>
        {
            using var f = Scene(); var raw = Start(f, 10, 37714);
            f.Engine.StartCast(10, raw with { Rotation = .7f }, f.Now);
            Check(Group(f, "sword") is [{ Rotation: .7f }], "Sword correction duplicated queue.");
            using var p = Scene(true); raw = Start(p, 10, 37720, location: new(121.2132f, 0, 121.2132f));
            p.Engine.StartCast(10, raw with { Rotation = .8f }, p.Now);
            Check(Group(p, "chasm") is [{ Rotation: .8f }], "Chasm correction duplicated queue.");
        });
        run("ZoraalJa charge uses quantized caster and actual destination distance not packet direction", () =>
        {
            using var f = Scene(); f.Actors[10] = f.Actors[10] with { Position = new(90, 0, 100) }; f.Scan();
            Start(f, 10, 38184, rotation: 0, location: new(110, 0, 100));
            var charge = Group(f, "charge").Single();
            Check(charge.Shape is RayRectangleTemplate { Length: 20, MinLocalX: -2, MaxLocalX: 2 }
                && MathF.Abs(charge.Position.X - 90) < .04f && charge.Rotation == MathF.PI / 2, "Charge geometry.");
            End(f, 10, 38184); Check(!Group(f, "charge").Any(), "Charge finish.");
        });
        run("ZoraalJa arena shrink waits real map completion transition square hole and restore retains pending warning", () =>
        {
            using var f = Scene(true); f.Engine.MapEffect(0x20, 0x00020001); f.Render();
            var aoe = Group(f, "transition").Single(); var shape = aoe.Shape.Resolve(aoe.Position, 0, null);
            Check(Time(f, aoe.Activation, 8) && shape.Contains(new(120, 0, 100)) && !shape.Contains(new(100, 0, 100)), "Square ring hole/activation.");
            f.Now = f.Start.AddSeconds(40); f.Engine.Tick(f.Now);
            Check(f.Engine.Arena!.Contains(new(126, 0, 100)), "Prediction incorrectly changed boundary.");
            f.Engine.MapEffect(0x20, 0x00080004); f.Render();
            Check(!Group(f, "transition").Any() && !f.Engine.Arena.Contains(new(120, 0, 100)), "Actual shrink.");
            f.Engine.MapEffect(0x20, 0x00020001); f.Engine.MapEffect(0x1B, 0x00080004);
            Check(Group(f, "transition").Any() && f.Engine.Arena.Contains(new(126, 0, 100)), "Restore source pending warning semantics.");
        });
        run("ZoraalJa chasm projection tests caster bounds half full hides all outputs until any resolving finish", () =>
        {
            using var f = Scene(true); Start(f, 11, 37738); Start(f, 12, 37738, rotation: 1);
            Check(Group(f, "half").Length == 1, "HalfFull only first.");
            f.Actors[10] = f.Actors[10] with { Position = new(130, 0, 130) }; f.Scan();
            Start(f, 10, 37720, location: new(121.2132f, 0, 121.2132f));
            Check(!Group(f, "half").Any() && Group(f, "chasm").Single().Position is { X: > 99.96f and < 100.04f, Z: > 99.96f and < 100.04f }
                && !f.Engine.OutputCasts.Any(c => c.Value.Rule.Action is 37720 or 37738), "Projection/hide.");
            Start(f, 13, 37722); End(f, 13, 37722);
            Check(!Group(f, "chasm").Any() && Group(f, "half").Length == 1, "Helper finish must clear every chasm.");
            End(f, 11, 37738); Check(Group(f, "half").Single().Rotation == 1, "Second HalfFull reveal.");
        });
        run("ZoraalJa chasm in bounds caster keeps raw location even when destination outside", () =>
        {
            using var f = Scene(true); Start(f, 10, 37720, location: new(130, 0, 130));
            Check(Group(f, "chasm").Single().Position == new Vector3(130, 0, 130), "Projection used target bounds instead of caster.");
        });
        run("ZoraalJa forged tracks arbitrary tethers first four only real effects consume FIFO with no timeout", () =>
        {
            using var f = Scene(true);
            for (var i = 0; i < 5; ++i) f.Engine.Tether(100, (uint)(900 + i), true, f.Now, (ulong)(10 + i));
            Check(Group(f, "track").Length == 4 && Time(f, Group(f, "track")[0].Activation, 13.3), "Track cap/clock.");
            f.Engine.Tether(100, 900, false, f.Now, 10); f.Engine.Tick(f.Now.AddSeconds(40));
            Check(Group(f, "track").Length == 4, "Untether/timeout consumed track.");
            Hit(f, 10, 37730, 1); Check(Group(f, "track").Length == 4, "Fifth track did not enter visible prefix.");
            Hit(f, 10, 37730, 2); Check(Group(f, "track").Length == 3, "Track second effect.");
        });
        run("ZoraalJa forged track intercardinal lane adjustment and initial raw tether confirmation", () =>
        {
            using var f = Scene(true);
            var rotation = -45.003f * MathF.PI / 180; var right = new Vector2(-MathF.Cos(rotation), MathF.Sin(rotation));
            f.Actors[10] = f.Actors[10] with { Position = new(100 + right.X * -7.5f, 0, 100 + right.Y * -7.5f),
                Rotation = rotation, Tethers = new Dictionary<uint, ulong> { [999] = 100 } }; f.Scan();
            var track = Group(f, "track").Single();
            var lane = Vector2.Dot(new(track.Position.X - 100, track.Position.Z - 100), right);
            Check(MathF.Abs(lane + 2.5f) < .05f && track.Shape is RectangleTemplate { Width: 5, Length: 20 }, "X lane shift/centered rect.");
            f.Scan(); Check(Group(f, "track").Length == 1, "Raw repeated.");
            f.Engine.Tether(100, 999, true, f.Now, 10);
            Check(Group(f, "track").Length == 1, "Raw-first packet duplicated the same state transition.");
            f.Engine.Tether(101, 998, true, f.Now, 11);
            f.Actors[11] = f.Actors[11] with { Tethers = new Dictionary<uint, ulong> { [998] = 101 } }; f.Scan();
            Check(Group(f, "track").Length == 2, "Raw packet confirmation duplicated track.");
        });
        run("ZoraalJa FireIII dynamic six display seven AI exact target removal fallback death and prediction", () =>
        {
            using var f = Scene(true); f.Engine.Icon(100, 376, f.Now, 100); f.Engine.Icon(101, 376, f.Now, 101); f.Render();
            Check(Group(f, "spread").Count(a => a.Display) == 2 && Group(f, "spread").Any(a => a.Shape is CircleTemplate { Radius: 7 })
                && f.Engine.PredictedDamage.Count() == 2 && f.Engine.PredictedDamage.All(d => Time(f, d.Activation, 5.1)), "Spread display/AI/prediction.");
            Hit(f, 10, 37752, 1, 101); Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Target match.");
            f.Actors[100] = f.Actors[100] with { Position = new(110, 0, 100) }; f.Scan();
            Check(MathF.Abs(Group(f, "spread").First().Position.X - 110) < .04f, "Spread not dynamic.");
            Hit(f, 10, 37752, 2, 999); Check(!Group(f, "spread").Any(), "Fallback first.");
            f.Engine.Icon(102, 376, f.Now, 102); f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(!Group(f, "spread").Any(), "Dead target not removed.");
        });
        run("ZoraalJa duties edge actual marker four hit count unchanged activation dynamic source target and dead target", () =>
        {
            using var f = Scene(true); Hit(f, 10, 35567, 1, 101);
            Check(Group(f, "stack").Any() && Group(f, "stack/join").Any() && Time(f, Group(f, "stack/join").Single().Activation, 5.4)
                && f.Engine.PredictedDamage.Count(d => d.DamageType == "Shared") == 8, "Marker/join/shared prediction.");
            for (uint i = 1; i <= 3; ++i) Hit(f, 11, 37750, i, 102);
            Check(Group(f, "stack").Any() && Time(f, Group(f, "stack/join").Single().Activation, 5.4), "Intermediate hit erased or rescheduled.");
            f.Actors[101] = f.Actors[101] with { Position = new(100, 0, 110), Dead = true }; f.Scan();
            Check(Group(f, "stack").First().Rotation == 0, "Dead target was replaced or did not follow.");
            Hit(f, 11, 37750, 4, 102); Check(!Group(f, "stack").Any(), "Fourth hit.");
        });
        run("ZoraalJa line marked players approach two yard buddy union avoid other stacks and preserve premarker count", () =>
        {
            using var f = Scene(true); Hit(f, 10, 37750, 1);
            Hit(f, 10, 35567, 1, 100); Hit(f, 11, 35567, 1, 101);
            Check(Group(f, "stack").Any(a => a.Id.EndsWith("/buddy"))
                && Group(f, "stack").Any(a => a.Shape is RayRectangleTemplate { MinLocalX: -8, MaxLocalX: 8 } && !a.Display), "Marked player movement constraints.");
            for (uint i = 2; i <= 4; ++i) Hit(f, 10, 37750, i);
            Check(Group(f, "stack").Count(a => a.Display) == 1, "Premarker source castCounter not retained.");
        });
        run("ZoraalJa raidwide duplicate source registration and visuals do not invent missing mechanisms", () =>
        {
            using var f = Scene(); Start(f, 1, 37707); Check(f.Engine.PredictedDamage.Count() == 16, "Source duplicate raidwide registration.");
            End(f, 1, 37707); Start(f, 1, 37744);
            Check(!f.Engine.PredictedDamage.Any() && !f.Engine.OutputCasts.Any(), "Unconsumed visual was upgraded.");
        });
        run("ZoraalJa event adapter callbacks use processing frame for icon map tether and marker", () =>
        {
            using var f = Scene(true); f.Now = f.Start.AddSeconds(10);
            f.Events.Handle(new LogSystemTargetIconEvent { SourceId = 101, TargetId = 101, MarkerId = 376, Timestamp = f.Start }, f.Now);
            f.Events.Handle(new LogSystemMapEffectEvent { Position = 0x20, Param1 = 1, Param2 = 2, Timestamp = f.Start }, f.Now);
            f.Events.Handle(new LogSystemTetherEvent { SourceId = 10, TargetId = 100, TetherId = 999, Timestamp = f.Start }, f.Now);
            Check(Time(f, f.Engine.PredictedDamage.Single().Activation, 15.1) && Time(f, Group(f, "transition").Single().Activation, 18)
                && Time(f, Group(f, "track").Single().Activation, 23.3), "Current callback time lost.");
        });
        run("ZoraalJa real solver escapes current half while displayed next half remains traversable", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 110) }; f.Scan();
            Start(f, 10, 37714); Start(f, 11, 37714, rotation: MathF.PI);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && path.All(p => f.Engine.Arena!.Contains(p.Position))
                && !new FanGeometry(new(100, 0, 100), 0, 30, MathF.PI).Contains(path[^1].Position),
                "Solver failed to navigate the first half or treated the displayed next half as dangerous.");
        });
        run("ZoraalJa wipe director clears alive checkpoint state and prevents resurrection until recommence", () =>
        {
            using var f = Scene(true); f.Engine.MapEffect(0x20, 0x00080004);
            f.Engine.Tether(100, 999, true, f.Now, 10); Hit(f, 11, 35567, 1, 101); Hit(f, 11, 37750, 1);
            f.Engine.DirectorUpdate(0x40000005); f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Alive primary reactivated during wipe.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan();
            Check(f.Engine.Active?.BossOid == 0x42B4 && f.Engine.Arena!.Contains(new(126, 0, 100))
                && !Group(f, "track").Any() && !Group(f, "stack").Any(), "Recommence retained prior arena/queues.");
            Hit(f, 11, 35567, 2, 101);
            for (uint i = 2; i <= 4; ++i) Hit(f, 11, 37750, i);
            Check(Group(f, "stack").Any(), "Prior partial hit counter survived wipe.");
        });
        run("ZoraalJa dead line source hides output and shared prediction while dead target remains supported", () =>
        {
            using var f = Scene(true); Hit(f, 10, 35567, 1, 101);
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            Check(Group(f, "stack").Any(), "Dead target incorrectly hid stack.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(!Group(f, "stack").Any() && !f.Engine.PredictedDamage.Any(), "Dead source still produced stack/Shared.");
            f.Actors[10] = f.Actors[10] with { Dead = false }; f.Scan();
            Check(Group(f, "stack").Any(), "Hidden source state was consumed before actual effects.");
        });
        run("ZoraalJa position hints include risky half spreads and line stack participation", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 110) }; f.Scan();
            Start(f, 10, 37714); Check(f.Engine.Hints(f.Actors[100].Position).Contains("离开机制危险区"), "Ambient risky half lost warning.");
            using var p = Scene(true); p.Engine.Icon(101, 376, p.Now, 101);
            Check(p.Engine.Hints(p.Actors[100].Position).Contains("离开分散范围"), "Spread warning.");
            Hit(p, 10, 35567, 1, 101); Check(p.Engine.Hints(p.Actors[100].Position).Any(h => h.Contains("四连直线分摊")), "Stack warning.");
        });
        run("ZoraalJa marked spreads warn each other and hidden stack targets remain excluded from buddies", () =>
        {
            using var f = Scene(true);
            for (ulong i = 102; i < 108; ++i) f.Actors[i] = f.Actors[i] with { Position = new(120, 0, 100) };
            f.Scan(); f.Engine.Icon(100, 376, f.Now, 100); f.Engine.Icon(101, 376, f.Now, 101);
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("分散远离队友"), "Marked spread pair lost warning.");
            Hit(f, 10, 35567, 1, 100); Hit(f, 11, 35567, 1, 101);
            f.Actors[11] = f.Actors[11] with { Dead = true }; f.Scan();
            var buddy = Group(f, "stack").Single(a => a.Id.EndsWith("/buddy"));
            Check(buddy.Shape.Resolve(buddy.Position, 0, null).Contains(f.Actors[101].Position), "Inactive bait target was accepted as buddy.");
        });
        run("ZoraalJa pending tether accepts a later raw transition after convergence window without replaying confirmation", () =>
        {
            using var f = Scene(true); f.Engine.Tether(100, 999, true, f.Now, 10);
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [999] = 101 } };
            f.Scan(); Check(Group(f, "track").Length == 1, "Stale raw bypassed convergence window.");
            f.Now = f.Now.AddSeconds(.6); f.Scan(); Check(Group(f, "track").Length == 2, "Later raw transition swallowed by old pending.");
            f.Engine.Tether(101, 999, true, f.Now, 10); f.Scan();
            Check(Group(f, "track").Length == 2, "Raw transition packet confirmation duplicated track.");
        });
    }

    private static Fight Scene(bool second = false)
    {
        var f = new Fight(second ? 0x42B4u : 0x42A9u, 1200); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c, Hp = 100 };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i,
            PartySlot: i, Hostile: false);
        for (ulong i = 10; i < 20; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Scan(); return f;
    }
    private static DungeonCastObservation Start(Fight f, ulong source, uint action, float rotation = 0, Vector3? location = null)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render(); return cast;
    }
    private static void End(Fight f, ulong source, uint action)
    { f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render(); }
    private static void Hit(Fight f, ulong source, uint action, uint seq, ulong target = 0)
    { f.Engine.Effect(source, action, seq, f.Now, target); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("zoraal/" + prefix)).ToArray();
    private static bool Time(Fight f, DateTime? actual, double seconds) => actual is { } at && Math.Abs((at - f.Start.AddSeconds(seconds)).Ticks) <= 10;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
