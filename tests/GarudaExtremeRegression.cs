using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class GarudaExtremeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("GarudaExtreme native identity death wipe recommence and HP0", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.Owns(0xF5, 297) && !ArrDungeonCatalog.Owns(0xF5, 65)
                && ArrDungeonCatalog.IsScript("masterplayer.trial.garuda-extreme"), "Identity.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null, "Personal death.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
            f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0.");
        });
        foreach (var (action, source) in new (uint, BossMod.AOEShape)[]
        {
            (1382, new BossMod.AOEShapeCone(11.7f, new(MathF.PI / 4))),
            (1549, new BossMod.AOEShapeCircle(5)), (1550, new BossMod.AOEShapeCircle(3)),
            (1555, new BossMod.AOEShapeCircle(6.5f)), (1777, new BossMod.AOEShapeCircle(8)),
        })
            run($"GarudaExtreme action{action} source geometry actual observed ending and independent display", () =>
            {
                using var f = Scene();
                if (action is 1549 or 1555) EnterAdds(f);
                if (action == 1777) EnterWhirlwind(f);
                Start(f, 20, action, 3, .7f);
                Check(f.Engine.Casts.TryGetValue(20, out var cast), $"Action{action} rejected.");
                var geometry = cast!.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (var x = -15f; x < 15; x += .47f)
                    for (var z = -15f; z < 15; z += .51f)
                        Check(geometry.Contains(cast.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(cast.Location.X + x, cast.Location.Z + z), new(cast.Location.X, cast.Location.Z), new(cast.Rotation)), "Source shape.");
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
                Check(f.Engine.Casts.ContainsKey(20) && f.Draw.Previews.Count > 0, "Nominal timeout.");
                End(f, 20, action, false); Check(!f.Engine.Casts.ContainsKey(20), "Observed cancel.");
            });
        run("GarudaExtreme component gating follows disappearance and phase2 adds", () =>
        {
            using var f = Scene(); Start(f, 20, 1549, 3);
            Check(!f.Engine.Casts.ContainsKey(20), "Adds friction active in plumes phase.");
            Start(f, 21, 1382, 3); Check(f.Engine.Casts.ContainsKey(21), "Initial slipstream.");
            Targetable(f, false);
            Check(!f.Engine.Casts.ContainsKey(21) && Group(f, "song/shelter").Length == 1, "Disappear disables combat.");
            Start(f, 22, 1382, 3); Check(!f.Engine.Casts.ContainsKey(22), "Slipstream re-enabled while hidden.");
            EnterAdds(f); Start(f, 20, 1549, 3);
            Check(f.Engine.Casts.ContainsKey(20) && Group(f, "song").Length == 0, "Phase transition clears old song.");
        });
        run("GarudaExtreme both predicted songs actual origin frozen live blockers and timed navigation", () =>
        {
            using var f = Scene();
            f.Actors[30] = new(30, 0xF3, Vector3.Zero, HitboxRadius: 2.3f);
            f.Actors[31] = new(31, 0xF3, new(10, 0, 0), Dead: true, HitboxRadius: 2.3f); f.Scan();
            Targetable(f, false);
            Check(Group(f, "song/shelter").Single().Position == new Vector3(0, 0, -13)
                && Group(f, "song/avoid").Length == 0, "First predicted origin.");
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -12) };
            Targetable(f, true); Start(f, 1, 1761, 1);
            var shelter = Group(f, "song/shelter").Single();
            Check(shelter.Position == new Vector3(0, 0, -12) && Group(f, "song/avoid").Single().Activation == f.Engine.Casts[1].FinishAt,
                "Actual cast origin and finish.");
            var safe = shelter.Shape.Resolve(shelter.Position, 0, null);
            Check(safe.Contains(new(0, 0, 5)) && !safe.Contains(Vector3.Zero) && !safe.Contains(new(10, 0, 5)), "Pillar shadow or hitbox.");
            f.Actors[30] = f.Actors[30] with { Dead = true }; f.Scan();
            Check(Group(f, "song/shelter").Single().Shape.Resolve(shelter.Position, 0, null).Contains(new(0, 0, 5)), "Blockers changed mid cast.");
            End(f, 1, 1761); Check(Group(f, "song").Length == 0, "First actual finish.");
            Targetable(f, false);
            Check(Group(f, "song/shelter").Single().Position == new Vector3(13, 0, 0), "Second predicted origin.");
            Targetable(f, true); Start(f, 1, 1761, 1); End(f, 1, 1761);
            Targetable(f, false);
            Check(Group(f, "song").Length == 0, "Terminal state fabricated third song.");
        });
        run("GarudaExtreme real solver reaches pillar shelter for Mistral", () =>
        {
            using var f = Scene();
            f.Actors[30] = new(30, 0xF3, Vector3.Zero, HitboxRadius: 2.3f); f.Scan();
            Targetable(f, false);
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -13) };
            Targetable(f, true); Start(f, 1, 1761, 5);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(new(5, 0, 5), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var safe = Group(f, "song/shelter").Single();
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
                && solution.ExecutablePath.Concat(solution.ContinuationPath).Any(p => safe.Shape.Resolve(safe.Position, 0, null).Contains(p.Position)),
                $"Shelter path {solution.Status},fallback={solution.IsLeastDangerFallback}.");
        });
        run("GarudaExtreme Eye finish plus4.2 persists and phase change clears old caster", () =>
        {
            using var f = Scene(); Start(f, 20, 1671, 2);
            Check(Group(f, "eye").Single().Activation == f.Engine.Casts[20].FinishAt, "Eye initial activation.");
            f.Now = f.Now.AddSeconds(1); End(f, 20, 1671);
            Check(Group(f, "eye").Single().Activation == f.Now.AddSeconds(4.2), "Observed finish plus4.2.");
            f.Actors[20] = f.Actors[20] with { Position = new(2, 0, 3) }; f.Now = f.Now.AddSeconds(20); f.Scan();
            Check(Group(f, "eye").Single().Position == Quantized(new(2, 0, 3)), "Persistent eye live origin.");
            EnterAdds(f); Check(Group(f, "eye").Length == 0, "Phase2 recreated Eye component.");
            Start(f, 20, 1671, 2); Check(!f.Engine.Casts.ContainsKey(20), "Eye accepted in adds phase.");
            EnterEye(f); Start(f, 20, 1671, 2); Check(Group(f, "eye").Length == 1, "Next Eye subphase missing.");
        });
        run("GarudaExtreme first adds hide has60 check delay and later states have no invented timeout", () =>
        {
            using var f = Scene(); EnterAdds(f);
            Targetable(f, false); f.Now = f.Now.AddSeconds(59.9); f.Scan();
            Check(f.Engine.PriorityTarget(0, Vector3.Zero) == 10, "AI disappeared before60 gate.");
            Start(f, 20, 1549, 2); Check(f.Engine.Casts.ContainsKey(20), "Adds disabled before60.");
            f.Now = f.Now.AddSeconds(.1); f.Scan(); Check(!f.Engine.Casts.ContainsKey(20), "Actual hide after60.");
            f.Now = f.Now.AddSeconds(300); f.Scan();
            Start(f, 21, 1382, 2); Check(!f.Engine.Casts.ContainsKey(21), "Reappear fabricated by elapsed time.");
            Targetable(f, true); Start(f, 1, 1554, 4); End(f, 1, 1554);
            Start(f, 22, 1382, 2); Check(f.Engine.Casts.ContainsKey(22), "Next combat did not start on actual cast end.");
        });
        run("GarudaExtreme WickedWheel strict final3 activation and effect resets only with alive Suparna", () =>
        {
            using var f = Scene(); EnterAdds(f); var began = f.Now;
            f.Now = began.AddSeconds(22); f.Scan(); Check(Group(f, "wheel").Length == 0, "Strict <3.");
            f.Now = began.AddSeconds(22.01); f.Scan();
            Check(Group(f, "wheel").Single().Activation == began.AddSeconds(25) && Group(f, "wheel").Single().Navigate, "Initial25.");
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
            Check(!Group(f, "wheel").Single().Navigate, "MT navigation exclusion.");
            f.Engine.Effect(10, 1552, 1, f.Now); f.Render(); Check(Group(f, "wheel").Length == 0, "Effect reset25.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            f.Engine.Effect(1, 1552, 2, f.Now); f.Now = f.Now.AddSeconds(100); f.Scan();
            Check(Group(f, "wheel").Length == 0, "Wheel rearmed without alive Suparna.");
        });
        run("GarudaExtreme shield first active entity state7 replacement and forced solver entry", () =>
        {
            using var f = Scene(); EnterAdds(f);
            f.Actors[30] = new(30, 0x1E8F68, new(10, 0, 0), EventState: 7);
            f.Actors[31] = new(31, 0x1E8F68, new(-10, 0, 0));
            f.Actors[100] = f.Actors[100] with { Position = new(-10, 0, 0) }; f.Scan();
            Check(Group(f, "shield/display").Single().Position == new Vector3(-10, 0, 0)
                && !f.Engine.Hints(new(-10, 0, 0)).Any(h => h == "进入刺羽护罩"), "First non7 shield.");
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success
                && solution.ExecutablePath.Concat(solution.ContinuationPath).Any(p => Vector3.Distance(p.Position, new(-10, 0, 0)) <= 6), "Shield entry path.");
            f.Actors[31] = f.Actors[31] with { EventState = 7 }; f.Scan(); Check(Group(f, "shield").Length == 0, "Expired shield.");
        });
        run("GarudaExtreme spiny pointless priority stays soft during two-stack tank provoke", () =>
        {
            using var f = Scene(); EnterAdds(f);
            f.Actors[100] = f.Actors[100] with { ClassJob = 21 };
            f.Actors[101] = new(101, 0, new(0, 0, 10), PartySlot: 1, Statuses: [379], StatusParams: new Dictionary<uint, ushort> { [379] = 1 });
            f.Actors[30] = new(30, 0x600, new(0, 0, 12), TargetId: 101); f.Scan();
            Check(!f.Engine.ForbiddenTargets.Contains(30ul) && f.Engine.EnemyProvokeActions().Count == 0
                && f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Soft priority at1.");
            f.Engine.StatusChanged(101, 379, true, f.Now, 2); f.Render();
            Check(!f.Engine.ForbiddenTargets.Contains(30ul) && f.Engine.EnemyProvokeActions().Any(a => a.Action == 7533 && a.Target == 30)
                && f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Provoke without selecting spiny for damage.");
            f.Engine.StatusChanged(101, 379, false, f.Now); f.Render();
            Check(f.Engine.EnemyProvokeActions().Count == 0, "Status lose packet ignored.");
            f.Actors[100] = f.Actors[100] with { ClassJob = 25 }; f.Engine.StatusChanged(101, 379, true, f.Now, 2); f.Scan();
            Check(f.Engine.EnemyProvokeActions().Count == 0, "Non-tank provoke.");
            f.Now = f.Now.AddSeconds(60); Targetable(f, false);
            Check(!f.Engine.ForbiddenTargets.Contains(30ul) && f.Engine.PriorityTarget(0, Vector3.Zero) == 30, "Kill spiny during disappear.");
        });
        run("GarudaExtreme MT priority and nonMT Satin Razor Suparna Chirada order", () =>
        {
            using var f = Scene(); EnterAdds(f);
            f.Actors[11] = new(11, 0x620, new(10, 0, 0));
            f.Actors[12] = new(12, 0xF4, new(10, 0, 0)); f.Actors[13] = new(13, 0x601, new(10, 0, 0)); f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 13, "Satin first.");
            f.Engine.CurrentAssignment = "MT"; Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 11, "MT must prioritize Chirada.");
            f.Engine.CurrentAssignment = "OT"; f.Actors[13] = f.Actors[13] with { Dead = true }; f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 12, "Razor next.");
            f.Actors[12] = f.Actors[12] with { Dead = true }; f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Suparna next.");
        });
        run("GarudaExtreme Eye stays through Shriek then clears on next observed hide before whirlwind", () =>
        {
            using var f = Scene(); EnterEye(f); Start(f, 20, 1671, 2); End(f, 20, 1671);
            Targetable(f, false); Targetable(f, true); Start(f, 1, 1384, 3); End(f, 1, 1384);
            Check(Group(f, "eye").Length == 1, "Eye cleared at Shriek finish.");
            Targetable(f, false); Check(Group(f, "eye").Length == 0, "Eye retained beyond second hide.");
            Start(f, 21, 1777, 3); Check(!f.Engine.Casts.ContainsKey(21), "Whirlwind activated before reappear.");
            Targetable(f, true); Start(f, 21, 1777, 3); Check(f.Engine.Casts.ContainsKey(21), "Whirlwind next combat.");
        });
        run("GarudaExtreme MistralShriek publishes source raidwide prediction", () =>
        {
            using var f = Scene(); EnterEye(f);
            Targetable(f, false); Targetable(f, true); Start(f, 1, 1384, 3);
            Check(f.Engine.PredictedDamage.Any(d => d.DamageType == "Raidwide" && d.Activation == f.Engine.Casts[1].FinishAt),
                "Shriek raidwide prediction.");
        });
        run("GarudaExtreme dead spiny deathcast remains until actual Gigastorm finish", () =>
        {
            using var f = Scene(); EnterAdds(f);
            f.Actors[30] = new(30, 0x600, new(0, 0, 10), Dead: true, Hp: 0); f.Scan();
            Start(f, 30, 1555, 3); f.Scan();
            Check(f.Engine.Casts.ContainsKey(30) && f.Engine.OutputCasts.Any(c => c.Value.Source == 30), "Deathcast rejected.");
            End(f, 30, 1555); Check(!f.Engine.Casts.ContainsKey(30), "Deathcast finish.");
        });
        run("GarudaExtreme all three Eye Shriek Whirlwind Blast cycles stop at source terminal", () =>
        {
            using var f = Scene(); EnterEye(f);
            for (var cycle = 0; cycle < 3; ++cycle)
            {
                Start(f, 20, 1671, 2); End(f, 20, 1671);
                Targetable(f, false); Targetable(f, true); Start(f, 1, 1384, 3); End(f, 1, 1384);
                Check(Group(f, "eye").Length == 1, $"Cycle{cycle} Eye lost after Shriek.");
                Targetable(f, false); Check(Group(f, "eye").Length == 0, $"Cycle{cycle} Eye retained.");
                Targetable(f, true); Start(f, 21, 1777, 10);
                Check(f.Engine.Casts.ContainsKey(21), $"Cycle{cycle} Whirlwind missing.");
                Targetable(f, false); Check(f.Engine.Casts.ContainsKey(21), $"Cycle{cycle} Whirlwind cleared at hide.");
                Targetable(f, true); Check(!f.Engine.Casts.ContainsKey(21), $"Cycle{cycle} Whirlwind retained after reappear.");
                Start(f, 1, 1554, 4); End(f, 1, 1554);
            }
            Start(f, 22, 1382, 2); Start(f, 23, 1671, 2); Start(f, 24, 1777, 2);
            Check(!f.Engine.Casts.ContainsKey(22) && !f.Engine.Casts.ContainsKey(23) && !f.Engine.Casts.ContainsKey(24),
                "Source terminal fabricated fourth cycle.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0xF5, 297);
        f.Actors[1] = f.Actors[1] with { Hp = 1000, MaxHp = 1000 };
        f.Actors[100] = new(100, 0, new(10, 0, 0), PartySlot: 0, Hostile: false);
        f.Scan(); return f;
    }
    private static void EnterAdds(Fight f)
    {
        f.Actors[10] = new(10, 0x621, new(0, 0, 15)); f.Scan();
    }
    private static void EnterEye(Fight f)
    {
        EnterAdds(f); f.Now = f.Now.AddSeconds(60);
        Targetable(f, false); Targetable(f, true); Start(f, 1, 1554, 4); End(f, 1, 1554);
    }
    private static void EnterWhirlwind(Fight f)
    {
        EnterEye(f);
        Targetable(f, false); Targetable(f, true); Start(f, 1, 1384, 3); End(f, 1, 1384);
        Targetable(f, false); Targetable(f, true);
    }
    private static void Targetable(Fight f, bool value)
    {
        f.Actors[1] = f.Actors[1] with { Targetable = value }; f.Scan();
    }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0)
    {
        f.Now = f.Now.AddSeconds(.001);
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x1B2, Vector3.Zero); f.Scan(); }
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), 100);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action, bool completed = true)
    {
        f.Actors[source] = f.Actors[source] with { Cast = null };
        f.Engine.Finish(source, action, f.Now, completed); f.Render();
    }
    private static Vector3 Quantized(Vector3 position)
    {
        var source = new BossMod.WPos(position.X, position.Z).Quantized();
        return new(source.X, position.Y, source.Z);
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("garuda-ex/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
