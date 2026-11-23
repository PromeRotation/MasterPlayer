using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class IfritExtremeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("IfritExtreme identity arena personal death wipe HP0 and no HP stage shortcut", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.ifrit-extreme")
                && ArrDungeonCatalog.Create(295).Length == 1 && ArrDungeonCatalog.Create(295)[0].BossOid == 0xD3, "Identity.");
            var original = BossMod.RealmReborn.Trial.T01IfritN.T01IfritN.IfritArena;
            var world = HostDrawingGeometry.Convert(f.Engine.Arena!);
            for (float x = -21; x <= 21; x += .43f)
                for (float z = -21; z <= 21; z += .47f)
                {
                    var expected = original.Contains(new(x, z));
                    Check(f.Engine.Arena!.Contains(new(x, 0, z), 0) == expected
                        && world.Contains(new(x, 0, z), 0) == expected, "Polygon parity in both views.");
                }
            f.Actors[1] = f.Actors[1] with { Hp = 1, MaxHp = 1000 }; f.Scan();
            Check(Stage(f) == "BeforeNails", "HP thresholds are comments, not stage predicates.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(f.Engine.Active != null, "Personal death.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
            f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0.");
        });
        run("IfritExtreme wind strict3.4 gate three real hits six second cadence replacement and perspective", () =>
        {
            using var f = Scene();
            Start(f, 1, 1529, 2, target: 101);
            var began = f.Now;
            Check(Group(f, "wind/display").Single().Activation == began.AddSeconds(5.4), "First5.4.");
            Check(((CircleGeometry)Group(f, "wind/display").Single().Shape.Resolve(Vector3.Zero, 0, null)).Radius == 14, "Display14.");
            At(f, began.AddSeconds(3.4)); Check(Group(f, "wind/avoid").Length == 0, "Strict3.4.");
            At(f, began.AddSeconds(3.401));
            Check(((CircleGeometry)Group(f, "wind/avoid").Single().Shape.Resolve(Vector3.Zero, 0, null)).Radius == 15, "Navigation15.");
            f.PlayerId = 101; f.Scan();
            Check(Group(f, "wind/avoid").Length == 0 && Group(f, "wind/party").Length == 2, "Perspective spread targets.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(Group(f, "wind/party").Length == 1, "Dead party spread.");
            for (var i = 0; i < 3; ++i)
            {
                Effect(f, 1530);
                Check(Group(f, "wind/display").Length == (i < 2 ? 1 : 0), "Three actual hits.");
                if (i < 2) Check(Group(f, "wind/display").Single().Activation == f.Now.AddSeconds(6), "Next6.");
                At(f, f.Now.AddSeconds(6));
            }
            End(f, 1, 1529); Start(f, 1, 1529, 2, target: 101); End(f, 1, 1529);
            Start(f, 1, 1529, 2, target: 100);
            Check(Group(f, "wind/display").Single().Position == f.Actors[100].Position, "Old target replaced.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(Group(f, "wind").Length == 0, "Dead wind target.");
        });
        run("IfritExtreme eruption strict one second alive closest baiter and zero caster reset", () =>
        {
            using var f = Scene();
            f.Actors[102] = f.Actors[102] with { Position = new(8, 0, 1), Dead = true }; f.Scan();
            Start(f, 1, 1676, 2);
            var began = f.Now;
            Start(f, 20, 1677, 3, position: new(8, 0, 1));
            Check(Group(f, "stand").Length == 1, "Alive closest melee baiter should use predefined second bait.");
            f.PlayerId = 101; f.Scan(); Check(Group(f, "stand").Length == 0, "Nonbaiter moved.");
            End(f, 20, 1677); f.Engine.Tick(f.Now); f.Render();
            f.PlayerId = 100; f.Scan();
            Check(Group(f, "stand").Length == 1, "Zero casters must clear baiters and restore normal positioning.");
            f.PlayerId = 101; f.Scan();
            At(f, began.AddSeconds(1));
            Start(f, 21, 1677, 3, position: f.Actors[101].Position, advance: false);
            Check(Group(f, "stand").Length == 0, "Deadline equality must not add late baiter.");
            End(f, 21, 1677);
            End(f, 1, 1676);
            f.PlayerId = 100; f.Scan(); Start(f, 1, 1676, 2);
            Start(f, 22, 1677, 3, position: f.Actors[100].Position);
            End(f, 1, 1676);
            Check(Group(f, "bait").Length == 1 && Group(f, "stand").Length == 0, "Postvisual baiter avoids live nonbaiters.");
        });
        run("IfritExtreme nail snapshot clockwise stable order dead removal no late additions and big threshold", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0xD4, new(15, 0, 0), Dead: true);
            f.Actors[21] = new(21, 0xD4, new(0, 0, 15));
            f.Actors[22] = new(22, 0xD4, new(-15, 0, 0));
            f.Actors[23] = new(23, 0xD4, new(0, 0, -15));
            f.Actors[24] = new(24, 0x91B, new(2, 0, 2), Hp: 50, MaxHp: 100); f.Scan();
            Check(Stage(f) == "Nails" && f.Engine.PriorityTarget(1, Vector3.Zero) == 21, "Dead first removed after snapshot.");
            f.Actors[25] = new(25, 0xD4, new(15, 0, 0)); f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 21, "Late nail changed snapshot.");
            f.Actors[21] = f.Actors[21] with { Targetable = false }; f.Scan();
            Check(Group(f, "next-nail").Single().Position == f.Actors[21].Position, "Untargetable nail removed.");
            f.Actors[21] = f.Actors[21] with { Dead = true }; f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 22, "Clockwise order.");
            f.Actors.Remove(22); f.Actors.Remove(23); f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 24 && !f.Engine.PredictedDamage.Any(), "Big nail last, strict half HP.");
            f.Actors[24] = f.Actors[24] with { Hp = 49 }; f.Scan();
            Check(f.Engine.PredictedDamage.Count() == 3 && f.Engine.Hints(null).Any(h => h.Contains("减伤")), "Large nail damage and visible hint.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(f.Engine.PredictedDamage.Count() == 2, "Dead party member in nail damage prediction.");
            Check(!f.Engine.ForbiddenTargets.Contains(24ul), "Soft priority must not become hard attack prohibition.");
        });
        run("IfritExtreme three rounds source predicates invincibility helper restoration Cyclone and terminal", () =>
        {
            using var f = Scene();
            for (var wave = 0; wave < 3; ++wave)
            {
                AddNail(f);
                f.Actors[1] = f.Actors[1] with { Statuses = [775] };
                f.Engine.StatusChanged(1, 775, true, f.Now); f.Scan();
                Check(Stage(f) == "Nails", "775 must not end nails.");
                Start(f, 1, 1536, 2);
                var finish = f.Engine.Casts[1].FinishAt;
                Targetable(f, false);
                Check(Stage(f) == "HellfireEnd" && f.Engine.PredictedDamage.All(p => p.Activation == finish), "Existing Hellfire restored on activation.");
                f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan();
                End(f, 1, 1536);
                Check(Stage(f) == "InvincibleEnd", "Wait for real status loss.");
                At(f, f.Now.AddSeconds(10)); Check(Stage(f) == "InvincibleEnd", "No early timeout.");
                f.Actors[1] = f.Actors[1] with { Statuses = [] };
                f.Engine.StatusChanged(1, 775, false, f.Now); Targetable(f, true);
                Check(Stage(f) == "PlumeStart", "Status loss.");
                Start(f, 21, 1359, 3, position: new(6, 0, 0));
                Check(!f.Engine.Casts.ContainsKey(21), "Plume helper premature activation.");
                Start(f, 1, 1356, 2);
                Check(f.Engine.Casts.ContainsKey(21) && Stage(f) == "PlumeEnd", "Existing helper restored.");
                End(f, 1, 1356);
                Check(Stage(f) == "PlumeResolve", "Wait helpers.");
                End(f, 21, 1359); Check(Stage(f) == "Disappear", "Actual plume ending.");
                Targetable(f, false);
                Check(Stage(f) == "Cyclone", "Actual disappearance.");
                Start(f, 22, 1532, 3, .71f);
                Check(f.Engine.Casts[22].Rotation == .71f, "Cyclone packet rotation.");
                var cast = f.Engine.OutputCasts.Single(c => c.Value.Source == 22).Value;
                var original = new BossMod.AOEShapeRect(49, 9);
                var geometry = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (float x = -15; x < 52; x += .61f)
                    for (float z = -15; z < 52; z += .59f)
                        Check(geometry.Contains(new(x, 0, z), .00001f)
                            == original.Check(new(x, z), new(cast.Location.X, cast.Location.Z), new(.71f)), "Cyclone source shape.");
                At(f, f.Now.AddSeconds(60)); Check(f.Engine.Casts.ContainsKey(22), "No invented caster timeout.");
                Targetable(f, true);
                Check(!f.Engine.Casts.ContainsKey(22), "Cyclone disabled on reappearance.");
            }
            Check(Stage(f) == "Enrage", "Three rounds.");
            At(f, f.Now.AddSeconds(5000)); Check(Stage(f) == "Enrage", "Terminal cannot auto advance.");
            Start(f, 23, 1677, 3); Check(f.Engine.Casts.ContainsKey(23), "Enrage eruption.");
        });
        run("IfritExtreme high DPS skips Cyclone and restores preexisting second wave fetters", () =>
        {
            using var f = Scene(); AddNail(f); ThroughPlumes(f);
            f.Actors[100] = f.Actors[100] with { Statuses = [377], StatusParams = new Dictionary<uint, ushort> { [377] = 1 } };
            f.Actors[101] = f.Actors[101] with { Statuses = [377] };
            AddNail(f);
            Check(Stage(f) == "Nails" && Group(f, "fetters/line").Single().Safe, "Same-frame skip and status snapshot restoration.");
            Check(Group(f, "fetters/near").Length == 1, "Non-tank within10.");
            f.Engine.StatusChanged(101, 377, true, f.Now, 3); f.Render();
            Check(!Group(f, "fetters/line").Single().Safe, "Strength danger line.");
            f.Engine.StatusChanged(101, 377, false, f.Now); f.Render();
            Check(Group(f, "fetters/line").Length == 0 && Group(f, "fetters/near").Length == 0, "Status loss.");
        });
        run("IfritExtreme incinerate counter mask reset and safe conditional provoke", () =>
        {
            using var f = Scene(); AddNail(f);
            f.Actors[100] = f.Actors[100] with { ClassJob = 21, PartyRole = "Tank", Position = new(0, 0, -18) };
            f.Actors[101] = f.Actors[101] with { Position = new(10, 0, 10) };
            f.Actors[102] = f.Actors[102] with { Position = new(-10, 0, 10) };
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -13) }; f.Scan();
            f.Engine.CurrentAssignment = "OT";
            for (var i = 0; i < 3; ++i)
            {
                Check(!f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533), "OT count before3.");
                Effect(f, 1528);
            }
            Check(f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533 && a.Target == 1), "OT at count3.");
            f.Actors[102] = f.Actors[102] with { Position = new(0, 0, -17) }; f.Scan();
            Check(!f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533), "Unsafe taunt cleaves non-tank.");
            f.Actors[102] = f.Actors[102] with { Position = new(-10, 0, 10) }; f.Scan();
            Effect(f, 1528); Check(!f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533), "MT at count4.");
            ThroughPlumes(f); AddNail(f);
            Check(f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533), "Wave2 count0 OT mask.");
            for (var i = 0; i < 3; ++i) Effect(f, 1528);
            Check(!f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533), "Wave2 count3 MT.");
        });
        run("IfritExtreme three incinerate masks use count not vulnerability stacks", () =>
        {
            using var f = Scene();
            f.Engine.CurrentAssignment = "OT";
            f.Actors[100] = f.Actors[100] with { ClassJob = 21, PartyRole = "Tank", Position = new(0, 0, -18) };
            f.Actors[101] = f.Actors[101] with { Position = new(10, 0, 10) };
            f.Actors[102] = f.Actors[102] with { Position = new(-10, 0, 10) }; f.Scan();
            for (var wave = 0; wave < 3; ++wave)
            {
                AddNail(f);
                var mask = new[] { 0x8, 0x7, 0x3C70 }[wave];
                for (var count = 0; count < 16; ++count)
                {
                    f.Engine.StatusChanged(101, 375, true, f.Now, 9);
                    Check(f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533) == ((mask & (1 << count)) != 0), $"Wave{wave} count{count}.");
                    Effect(f, 1528);
                }
                ThroughPlumes(f);
            }
        });
        run("IfritExtreme first wave fetters disabled and caster category excludes limited job", () =>
        {
            using var f = Scene();
            f.Engine.StatusChanged(100, 377, true, f.Now, 3);
            f.Engine.StatusChanged(101, 377, true, f.Now, 3); f.Render();
            Check(Group(f, "fetters").Length == 0, "Fetters enabled in first wave.");
            f.Actors[100] = f.Actors[100] with { ClassJob = 36, PartyRole = "Ranged" }; f.Scan();
            var limited = Group(f, "stand").Single().Position;
            f.Actors[100] = f.Actors[100] with { ClassJob = 25 }; f.Scan();
            Check(Vector3.Distance(Group(f, "stand").Single().Position, limited + Vector3.UnitZ * 15) < .001f, "Caster category.");
        });
        run("IfritExtreme independent eruption helpers actual cancellation destruction and source circle", () =>
        {
            using var f = Scene();
            Start(f, 20, 1677, 3, position: new(4, 0, 3));
            Start(f, 21, 1677, 5, position: new(-4, 0, 3));
            var cast = f.Engine.Casts[20];
            var original = new BossMod.AOEShapeCircle(8);
            var geometry = cast.Rule.Shape!.Resolve(cast.Location, 0, null);
            var world = HostDrawingGeometry.Convert(geometry);
            for (float x = -14; x < 14; x += .57f)
                for (float z = -14; z < 14; z += .59f)
                {
                    var expected = original.Check(new(x, z), new(4, 3), default);
                    Check(geometry.Contains(new(x, 0, z), .00001f) == expected
                        && world.Contains(new(x, 0, z), .00001f) == expected, "Circle source in both views.");
                }
            At(f, f.Now.AddSeconds(30));
            Check(f.Engine.Casts.ContainsKey(20) && f.Engine.Casts.ContainsKey(21), "Nominal deadline cleared helpers.");
            End(f, 20, 1677);
            Check(!f.Engine.Casts.ContainsKey(20) && f.Engine.Casts.ContainsKey(21), "Independent finish.");
            f.Actors.Remove(21); f.Scan();
            Check(!f.Engine.Casts.ContainsKey(21), "Destroyed helper retained.");
        });
        run("IfritExtreme mitigation half open windows all waves and third wave repeated windows", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { ClassJob = 21, PartyRole = "Tank", Position = new(0, 0, -12) };
            f.Actors[1] = f.Actors[1] with { TargetId = 100, Position = new(0, 0, -13) }; f.Scan();
            for (var wave = 0; wave < 3; ++wave)
            {
                AddNail(f); var began = f.Now;
                var windows = wave switch
                {
                    0 => new (uint Action, double From, double To)[] { (7531, 10, 20) },
                    1 => [(7531, 8, 13)],
                    _ => [(7531, 9, 13), (7535, 20, 25), (44, 35, 40), (44, 57, 63), (7531, 91, 97), (7535, 102, 109)],
                };
                foreach (var window in windows)
                {
                    At(f, began.AddSeconds(window.From - .001));
                    Check(!f.Engine.IfritExActions().Any(a => a.Action == window.Action), "Before window.");
                    At(f, began.AddSeconds(window.From));
                    Check(f.Engine.IfritExActions().Any(a => a.Action == window.Action && a.Target == 100
                        && a.Until == began.AddSeconds(window.To)), "Inclusive start and exact expiry.");
                    At(f, began.AddSeconds(window.To));
                    Check(!f.Engine.IfritExActions().Any(a => a.Action == window.Action), "Exclusive end.");
                }
                ThroughPlumes(f);
            }
        });
        run("IfritExtreme real solver follows immediate healer stand display sizes remain independent", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { PartyRole = "Healer", ClassJob = 24, Position = new(10, 0, 0) }; f.Scan();
            var stand = Group(f, "stand").Single();
            Check(stand.Position == Vector3.Zero && stand.Activation == null && !stand.Display && stand.Navigate, "Immediate position hint.");
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(new(10, 0, 0), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
                && solution.ExecutablePath.Concat(solution.ContinuationPath).Any(p => Vector3.Distance(p.Position, Vector3.Zero) <= 2), "Healer stand route.");
            Start(f, 20, 1677, 3, position: new(8, 0, 8));
            var display = f.Draw.Previews.Values.Single(p => p.Geometry is CircleGeometry { Radius: 8 });
            Check(display.Geometry.Contains(new(15.9f, 0, 8))
                && f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(new(16.4f, 0, 8))), "Display8 navigation margin.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0xD3, 295);
        f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -13), TargetId = 101 };
        f.Actors[100] = new(100, 0, new(8, 0, 0), PartySlot: 0, PartyRole: "Melee", ClassJob: 20, Hostile: false);
        f.Actors[101] = new(101, 0, new(0, 0, -18), PartySlot: 1, PartyRole: "Tank", ClassJob: 19, Hostile: false);
        f.Actors[102] = new(102, 0, new(-8, 0, 0), PartySlot: 2, PartyRole: "Healer", ClassJob: 24, Hostile: false);
        f.Scan(); return f;
    }

    private static void AddNail(Fight f)
    {
        f.Actors[20] = new(20, 0xD4, new(15, 0, 0)); f.Scan();
    }
    private static void ThroughPlumes(Fight f)
    {
        f.Actors[20] = f.Actors[20] with { Dead = true };
        Targetable(f, false); Start(f, 1, 1536, 2); End(f, 1, 1536);
        f.Engine.StatusChanged(1, 775, false, f.Now); Targetable(f, true);
        Start(f, 1, 1356, 2); End(f, 1, 1356);
        Check(Stage(f) == "Disappear", "ThroughPlumes.");
    }
    private static void At(Fight f, DateTime at) { f.Now = at; f.Engine.Tick(at); f.Render(); }
    private static void Targetable(Fight f, bool value) { f.Actors[1] = f.Actors[1] with { Targetable = value }; f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0,
        ulong target = 0, Vector3? position = null, bool advance = true)
    {
        if (advance) f.Now = f.Now.AddSeconds(.01);
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x908, position ?? Vector3.Zero); f.Scan(); }
        var cast = new DungeonCastObservation(action, position ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now); f.Scan(); f.Engine.Tick(f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Now = f.Now.AddSeconds(.21); f.Actors[source] = f.Actors[source] with { Cast = null };
        f.Engine.Finish(source, action, f.Now); f.Scan(); f.Engine.Tick(f.Now); f.Render();
    }
    private static uint sequence = 70000;
    private static void Effect(Fight f, uint action) { f.Engine.Effect(1, action, sequence++, f.Now); f.Engine.Tick(f.Now); f.Render(); }
    private static string Stage(Fight f) => f.Engine.GetType().GetField("ifritExStep",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f.Engine)!.ToString()!;
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ifrit-ex/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
