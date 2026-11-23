using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class UltimaExtremeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("UltimaExtreme native identity personal death wipe and HP0", () =>
        {
            using var f = Scene();
            Check(f.Engine.Active?.BossOid == 0x90F && ArrDungeonCatalog.Owns(0x90F, 348)
                && !ArrDungeonCatalog.Owns(0x90F, 68) && ArrDungeonCatalog.IsScript("masterplayer.trial.ultima-extreme"), "Identity.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(f.Engine.Active != null, "Personal death.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
            f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0.");
        });
        foreach (var (action, source) in new (uint, BossMod.AOEShape)[]
        {
            (1521, new BossMod.AOEShapeCircle(8)), (1522, new BossMod.AOEShapeCircle(6)),
            (1524, new BossMod.AOEShapeCircle(8)), (1507, new BossMod.AOEShapeRect(40, 3)),
            (1508, new BossMod.AOEShapeRect(40, 3)), (1509, new BossMod.AOEShapeRect(40, 3)),
            (1526, new BossMod.AOEShapeRect(45, 1)),
        })
            run($"UltimaExtreme action{action} source shape helper clock and observed end", () =>
            {
                using var f = Scene(); Start(f, 10, action, 3, .7f);
                var cast = f.Engine.Casts[10]; var actual = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (var x = -55f; x < 55; x += .87f)
                    for (var z = -55f; z < 55; z += .91f)
                        Check(actual.Contains(cast.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(cast.Location.X + x, cast.Location.Z + z), new(cast.Location.X, cast.Location.Z), new(.7f)), "Geometry.");
                int CastPreviews() => f.Draw.Previews.Keys.Count(k => !k.StartsWith("enemy/marker/", StringComparison.Ordinal));
                Check(CastPreviews() == 1 && f.Snapshot.DangerAreas.Count == 1, "Output.");
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render(); Check(CastPreviews() == 1, "Nominal timeout.");
                f.Engine.Finish(10, action, f.Now, false); f.Render(); Check(CastPreviews() == 0, "Observed cancel.");
            });
        run("UltimaExtreme Mistral timeline6.6 only final6 live turning and actual burst changes navigation", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x912, new(0, 0, -15), Rotation: .7f); f.Scan();
            var origin = f.Now; f.Engine.Timeline(20, 0x588, f.Now); f.Render(); Check(Group(f, "mistral").Length == 0, "Timeline shown too early.");
            f.Now = f.Now.AddSeconds(.7); f.Scan();
            var display = Group(f, "mistral/display").Single(); var nav = Group(f, "mistral/avoid").Single();
            Check(display.Activation == origin.AddSeconds(6.6) && display.Rotation == .7f && nav.Position == f.Actors[1].Position
                && Group(f, "mistral/knockback").Length > 0, "Timeline clock/burst route.");
            f.Actors[20] = f.Actors[20] with { Rotation = 1.3f, Position = new(2, 0, -14) }; f.Scan();
            Check(Group(f, "mistral/display").Single().Rotation == 1.3f, "Garuda turning locked early.");
            f.Engine.Effect(1, 1518, 1, f.Now); f.Render();
            Check(Group(f, "mistral/knockback").Length == 0 && Group(f, "mistral/avoid").Single().Position == f.Actors[20].Position, "Burst phase change.");
            f.Now = f.Now.AddSeconds(.1); Start(f, 20, 1517, 3);
            f.Engine.Finish(20, 1517, f.Now, false); f.Render(); Check(Group(f, "mistral").Length == 0, "Cancel Mistral.");
        });
        run("UltimaExtreme Mistral pre-knockback cone independently matches source two-edge construction", () =>
        {
            using var f = Scene(); f.Actors[1] = f.Actors[1] with { Position = new(3, 0, -7) };
            f.Actors[20] = new(20, 0x912, new(0, 0, -15), Rotation: .7f); f.Scan(); Start(f, 20, 1517, 3);
            var boss = new BossMod.WPos(3, -7);
            var p1 = 20 * new BossMod.Angle(.7f + MathF.PI * 5 / 12).ToDirection();
            var p2 = 20 * new BossMod.Angle(.7f - MathF.PI * 5 / 12).ToDirection();
            var a1 = BossMod.Angle.FromDirection(new BossMod.WPos(p1.X, p1.Z) - boss);
            var a2 = BossMod.Angle.FromDirection(new BossMod.WPos(p2.X, p2.Z) - boss);
            if (a2.Rad > a1.Rad) a2 -= new BossMod.Angle(MathF.Tau);
            var source = new BossMod.SDCone(boss, 40, (a1 + a2) / 2, (a1 - a2) / 2);
            var nav = Group(f, "mistral/avoid").Single(); var shape = nav.Shape.Resolve(nav.Position, nav.Rotation, null);
            for (var x = -19f; x < 19; x += .71f)
                for (var z = -19f; z < 19; z += .73f)
                    Check(shape.Contains(new(x, 0, z), 0) == (source.Distance(new(x, z)) <= 0), "Source knockback cone.");
        });
        run("UltimaExtreme Eye overrides Geocrush with separate display donut and narrow navigation ring", () =>
        {
            using var f = Scene(); Start(f, 20, 1520, 4); Start(f, 21, 1519, 3);
            Check(Group(f, "geocrush").Length == 0 && Group(f, "eye").Length == 3, "EOTS priority.");
            Check(Group(f, "eye").All(a => a.Activation == f.Engine.Casts[21].FinishAt), "Packet-first eye activation.");
            var nav = Group(f, "eye").Where(a => a.Navigate).Select(a => a.Shape.Resolve(a.Position, 0, null)).ToArray();
            Check(nav.Any(s => s.Contains(new(9, 0, 0))) && nav.All(s => !s.Contains(new(11, 0, 0)))
                && nav.Any(s => s.Contains(new(13, 0, 0))), "Ring10-12.");
            f.Engine.Finish(21, 1519, f.Now, false); f.Render(); Check(Group(f, "geocrush").Length == 1, "Geocrush should survive EOTS.");
            var s = f.Snapshot;
            var path = new SafeZoneSolver().Solve(s, new(new(11, 0, 0), 0, 6, false, false, 0, true, s.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(path.Status == SolutionStatus.Success && !path.IsLeastDangerFallback
                && path.ExecutablePath.Concat(path.ContinuationPath).Any(p => p.Position.Length() >= 18),
                $"Actual Geocrush escape: {path.Status}, fallback={path.IsLeastDangerFallback}, path={string.Join(',', path.ExecutablePath.Concat(path.ContinuationPath).Select(p => p.Position))}; aoe={string.Join(';', s.DangerAreas.Select(d => $"{d.Geometry.GetType().Name}@{d.Geometry.Position},window={string.Join(',', d.DangerWindows.Select(w => (w.DangerFrom-f.Now).TotalSeconds))}"))}.");
        });
        run("UltimaExtreme Crimson timeline5 live actor front43 back5 and finish", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x910, new(0, 0, -10), Rotation: .7f); f.Scan();
            f.Engine.Timeline(20, 0x8D, f.Now); f.Render();
            var aoe = Group(f, "cyclone").Single(); Check(aoe.Activation == f.Now.AddSeconds(5), "Timeline5.");
            var source = new BossMod.AOEShapeRect(43, 6, 5);
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            for (var x = -55f; x < 55; x += .91f)
                for (var z = -55f; z < 55; z += .93f)
                    Check(shape.Contains(new(x, 0, z), .00001f) == source.Check(new(x, z), new(0, -10), new(.7f)), "Front/back geometry.");
            f.Actors[20] = f.Actors[20] with { Position = new(10, 0, 0), Rotation = 1 }; f.Scan();
            Check(Group(f, "cyclone").Single().Rotation == 1, "Ifrit must keep tracking.");
            Start(f, 20, 1525, 3); f.Engine.Finish(20, 1525, f.Now, false); f.Render(); Check(Group(f, "cyclone").Length == 0, "Actual finish.");
        });
        run("UltimaExtreme Freefire global last timeline plus6 per-caster effects second MT invuln", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { ClassJob = 21 };
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[20] = new(20, 0x1B2, new(5, 0, 0)); f.Actors[21] = new(21, 0x1B2, new(-5, 0, 0)); f.Scan();
            f.Engine.Timeline(20, 0x449, f.Now); f.Now = f.Now.AddSeconds(1); f.Engine.Tick(f.Now);
            f.Engine.Timeline(21, 0x449, f.Now); f.Render();
            Check(Group(f, "freefire").Length == 2 && Group(f, "freefire").All(a => a.Activation == f.Now.AddSeconds(6) && a.Navigate), "Global timer.");
            f.Engine.Effect(20, 1515, 1, f.Now); f.Render();
            Check(Group(f, "freefire").Length == 1 && !Group(f, "freefire").Single().Navigate
                && f.Engine.UltimaActions().Any(a => a.Action == 43), "Second freefire MT Holmgang.");
            f.Engine.Effect(21, 1515, 2, f.Now); f.Render(); Check(Group(f, "freefire").Length == 0, "Per caster effect.");
        });
        run("UltimaExtreme pursuit fixate angular kiter mapping corridor and explosion-count reset", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(10, 0, 0) };
            f.Actors[101] = new(101, 0, new(0, 0, 10), PartySlot: 1, Hostile: false);
            f.Actors[20] = new(20, 0x913, Vector3.Zero, Rotation: MathF.PI / 2); f.Actors[21] = new(21, 0x913, new(-5, 0, 0)); f.Scan();
            f.Engine.Effect(10, 1650, 1, f.Now, 100); f.Engine.Effect(10, 1650, 2, f.Now, 101); f.Render();
            Check(Group(f, "kite/20/corridor").Length == 0 && Group(f, "kite/21/corridor").Length == 1, "Angular kiter.");
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("引导追踪球")), "Kiter hint.");
            f.Engine.Effect(20, 1510, 3, f.Now); f.Render(); Check(Group(f, "kite/20").Length == 0, "Exploded orb hidden.");
            f.Engine.Effect(21, 1510, 4, f.Now); f.Render();
            Check(Group(f, "kite/20").Length > 0 && !f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("引导追踪球")),
                "Exact equal count reset must clear both masks, not permanently remember old orb IDs.");
        });
        run("UltimaExtreme boom first orb queue exact-count gate pop quorum and tank exclusion", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { ClassJob = 20 }; f.Scan(); Start(f, 1, 1511, 4);
            Check(Group(f, "boom/south").Length == 1, "Pre-knockback south.");
            f.Engine.Effect(1, 1511, 1, f.Now); f.Engine.Finish(1, 1511, f.Now, true);
            AddOrbs(f, [new(0, 0, 18), new(18, 0, 0), new(0, 0, -18)]);
            Check(Group(f, "boom/preposition").Length == 1, "Accepted incomplete orb set.");
            f.Actors[23] = new(23, 0x914, new(-18, 0, 0)); f.Scan();
            var pop = Group(f, "boom/pop").Single();
            Check(pop.Position == new Vector3(0, 0, 19) && pop.Shape is DonutTemplate { InnerRadius: 6 }, "South orb order or follow offset.");
            for (ulong i = 101; i < 106; ++i) f.Actors[i] = new(i, 0, new(0, 0, 18), PartySlot: (int)(i - 100));
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, 18) }; f.Scan();
            Check(Group(f, "boom/pop").Single().Shape is DonutTemplate { InnerRadius: 1.5f }, "Six-person quorum.");
            f.Engine.Effect(20, 1512, 2, f.Now); f.Render(); Check(Group(f, "boom/pop").Single().Position == new Vector3(18, 0, 1), "E orb next.");
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
            Check(Group(f, "boom/pop").Length == 0 && Group(f, "boom/tank").Length == 3, "MT should avoid all active orbs.");
        });
        run("UltimaExtreme third boom assignment sides melee sacrifice and immunity actions", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { ClassJob = 20 }; f.Scan();
            f.Engine.Effect(1, 1511, 1, f.Now); f.Engine.Effect(1, 1511, 2, f.Now);
            Start(f, 1, 1511, 4); f.Scan();
            foreach (var (role, expected) in new (string, Vector3)[]
            {
                ("M1", new(10, 0, -2)), ("M2", new(-10, 0, -2)), ("H1", new(2, 0, 10)), ("H2", new(-2, 0, 10)),
            })
            {
                f.Engine.CurrentAssignment = role; f.Render();
                Check(Group(f, "boom/preposition").Single().Position == expected, "Third-group assignment.");
            }
            Check(f.Engine.UltimaActions().Any(a => a.Action == 7548), "Third knockback immunity.");
            f.Actors[100] = f.Actors[100] with { ClassJob = 25 }; f.Scan();
            Check(f.Engine.UltimaActions().Any(a => a.Action == 7559), "Surecast job route.");
        });
        run("UltimaExtreme TankPurge alternating source roles and four-stack tank pass", () =>
        {
            using var f = Scene(); Start(f, 1, 1514, 3); f.Engine.CurrentAssignment = "M1";
            f.Actors[100] = f.Actors[100] with { ClassJob = 20 }; f.Scan();
            Check(f.Engine.UltimaActions().Any(a => a.Action == 7549 && a.Target == 1), "First Feint.");
            f.Engine.CurrentAssignment = "M2"; Check(f.Engine.UltimaActions().Count == 0, "Even-role first mitigation.");
            f.Engine.Effect(1, 1514, 1, f.Now);
            Check(f.Engine.UltimaActions().Any(a => a.Action == 7549), "Second Feint.");
            f.Actors[100] = f.Actors[100] with { ClassJob = 21, PartyRole = "Tank" };
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan(); f.Engine.StatusChanged(100, 369, true, f.Now, 4);
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("交接仇恨")), "Stack4 swap.");
            f.Engine.StatusChanged(100, 369, false, f.Now); Check(!f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("交接仇恨")), "Status loss.");
            f.Actors[101] = new(101, 0, new(0, 0, -10), PartySlot: 1, PartyRole: "Tank", Statuses: [369],
                StatusParams: new Dictionary<uint, ushort> { [369] = 4 });
            f.Actors[1] = f.Actors[1] with { TargetId = 101 };
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, -2) }; f.Scan();
            Check(f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533 && a.Target == 1), "Tank swap provoke at north side.");
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, 2) }; f.Scan();
            Check(!f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533), "Must preposition before provoke.");
        });
        run("UltimaExtreme default role goal yields to mechanics and bits win target priority", () =>
        {
            using var f = Scene();
            f.Actors[101] = new(101, 0, new(0, 0, -10), PartySlot: 1);
            f.Actors[1] = f.Actors[1] with { TargetId = 101 }; f.Engine.CurrentAssignment = "R1"; f.Scan();
            var goal = Group(f, "idle").Single();
            Check(goal.Safe && MathF.Abs(goal.Position.X - 7.5f) < .0001f, "Default ranged offset.");
            Start(f, 10, 1521, 3); Check(Group(f, "idle").Length == 0, "Idle overrides live mechanic.");
            f.Engine.Finish(10, 1521, f.Now, false); f.Actors[20] = new(20, 0x915, new(10, 0, 0)); f.Scan();
            Check(Group(f, "idle").Length == 0 && f.Engine.PriorityTarget(1, f.Actors[100].Position) == 20, "Bit priority/range.");
            f.Output.Clear(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Owner leak.");
        });
        run("UltimaExtreme actor destruction ends actual cast but preserves effect-driven Freefire", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x912, new(0, 0, -15));
            f.Actors[21] = new(21, 0x1B2, new(10, 0, 0)); f.Scan();
            Start(f, 20, 1517, 3);
            f.Engine.Timeline(21, 0x449, f.Now); f.Render();
            Check(Group(f, "mistral").Length > 0 && Group(f, "freefire").Length == 1, "Initial independent components.");
            f.Actors.Remove(20); f.Engine.RemoveActor(20, f.Now); f.Render();
            Check(Group(f, "mistral").Length == 0, "Destroy must deliver cast finish.");
            f.Actors.Remove(21); f.Engine.RemoveActor(21, f.Now); f.Scan();
            Check(Group(f, "freefire").Length == 1, "Timeline Freefire is removed by effect, not destruction.");
            f.Engine.Effect(21, 1515, 1, f.Now); f.Render();
            Check(Group(f, "freefire").Length == 0, "Destroyed caster's late effect.");
        });
        run("UltimaExtreme Reprisal requires enemy coverage including enemy hitbox", () =>
        {
            using var f = Scene();
            f.Engine.CurrentAssignment = "MT";
            f.Actors[100] = f.Actors[100] with { ClassJob = 21, Position = new(15, 0, 0) };
            f.Actors[1] = f.Actors[1] with { HitboxRadius = 5 }; f.Scan(); Start(f, 1, 1514, 3);
            Check(!f.Engine.UltimaActions().Any(a => a.Action == 7535), "Out-of-range empty Reprisal.");
            f.Actors[100] = f.Actors[100] with { Position = new(10, 0, 0) }; f.Scan();
            Check(f.Engine.UltimaActions().Any(a => a.Action == 7535), "Enemy hitbox at exact coverage.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(!f.Engine.UltimaActions().Any(a => a.Action == 7535), "Untargetable enemy coverage.");
        });
        run("UltimaExtreme Cleave excludes dead party and untargetable boss", () =>
        {
            using var f = Scene();
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[101] = new(101, 0, new(0, 0, 10), PartySlot: 1, Dead: true); f.Scan();
            Check(Group(f, "viscous/party").Length == 0, "Dead party navigation.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(Group(f, "viscous/party").Length == 1, "Live party navigation.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(Group(f, "viscous").Length == 0, "Untargetable cleave.");
        });
        run("UltimaExtreme MT orb avoidance ends with pop queue while display retains remaining orbs", () =>
        {
            using var f = Scene(); Start(f, 1, 1511, 4);
            f.Engine.Effect(1, 1511, 1, f.Now); f.Engine.Finish(1, 1511, f.Now, true);
            AddOrbs(f, [new(0, 0, 18), new(18, 0, 0), new(0, 0, -18), new(-18, 0, 0)]);
            f.Engine.Effect(20, 1512, 2, f.Now); f.Engine.Effect(21, 1512, 3, f.Now);
            f.Actors[1] = f.Actors[1] with { TargetId = 100, Cast = null }; f.Scan();
            Check(Group(f, "boom/orb").Length == 2 && Group(f, "boom/tank").Length == 0, "Queue exhaustion must disable MT navigation only.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x90F, 348);
        f.Actors[1] = f.Actors[1] with { Hp = 1000, MaxHp = 1000 };
        f.Actors[100] = new(100, 0, new(10, 0, 0), PartySlot: 0, Hostile: false);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0)
    {
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x1B2, Vector3.Zero); f.Scan(); }
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), 100);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void AddOrbs(Fight f, Vector3[] positions)
    {
        for (var i = 0; i < positions.Length; ++i) f.Actors[(ulong)(20 + i)] = new((ulong)(20 + i), 0x914, positions[i]);
        f.Scan();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ultima/" + prefix)).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
