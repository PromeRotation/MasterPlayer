using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class TwintaniaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Twintania identity pull HP0 death wipe and personal death lifecycle", () =>
        {
            using var f = Scene();
            Check(f.Engine.Active?.BossOid == 0x7E5 && f.Engine.Arena is CircleGeometry
                && f.Engine.Arena.Position == new Vector3(-3, 0, -6.5f), "Native identity.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null && f.Engine.PreserveFightAcrossPlayerDeath, "Personal death reset.");
            f.Actors[1] = f.Actors[1] with { InCombat = false }; f.Scan();
            Check(f.Engine.Active != null, "Combat loss reset.");
            f.Actors[1] = f.Actors[1] with { Hp = 0, InCombat = true }; f.Scan();
            Check(f.Engine.Active == null, "HP0 did not end phase.");
            f.Actors[1] = f.Actors[1] with { Hp = 1000 }; f.Scan();
            f.Engine.DirectorUpdate(0x40000005); f.Scan();
            Check(f.Engine.Active == null, "Wipe immediately repulled.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan();
            Check(Stage(f) == 0, "Recommence failed.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Death leaked output.");
        });
        run("Twintania neurolink counts are current objects and targetable waits have no timeout", () =>
        {
            using var f = Scene();
            Link(f, 10); Check(Stage(f) == 1, "First link did not enter P2.");
            f.Actors.Remove(10); f.Scan(); Link(f, 11);
            Check(Stage(f) == 1, "Historical count used.");
            Link(f, 12); Check(Stage(f) == 2, "Second link did not enter disappear.");
            f.Now = f.Now.AddSeconds(200); f.Scan(); Check(Stage(f) == 2, "Disappear nominal duration became timeout.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(Stage(f) == 3, "Untargetable transition.");
        });
        run("Twintania all six Divebomb marker effect pairs and adds activate in exact order", () =>
        {
            using var f = Scene(); Dive(f);
            for (var i = 0; i < 6; ++i)
            {
                if (i == 3)
                {
                    Check(Stage(f) == 9, "Adds state absent.");
                    f.Actors[30] = new(30, 0x7E9, new(1, 0, 1), Dead: true); f.Scan();
                    Check(Stage(f) == 10, "Targetable dead Asclepius should satisfy source condition.");
                }
                var expected = i < 3 ? 3 + i * 2 : 10 + (i - 3) * 2;
                Check(Stage(f) == expected, "Marker state.");
                Effect(f, 1456, 101);
                Check(Stage(f) == expected + 1 && Group(f, "divebomb").Length == 1, "Marker did not advance.");
                f.Now = f.Now.AddSeconds(1); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "divebomb").Length == 1, "Nominal deadline erased target without effect.");
                // A resolve timeout may advance the chain; the actual effect still clears only its own marker.
                Effect(f, 1247);
                Check(Group(f, "divebomb").Length == 0, "Damage effect failed to clear marker.");
            }
            Check(Stage(f) == 16, "Reappear state absent.");
            f.Now = f.Now.AddSeconds(200); f.Scan(); Check(Stage(f) == 16, "Reappear timed out.");
            f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
            Check(Stage(f) == 17, "Reappear condition.");
            Effect(f, 1248); Check(Stage(f) == 18, "Profusion effect.");
            Link(f, 13); Check(Stage(f) == 19, "P5 link condition.");
            f.Now = f.Now.AddSeconds(2000); f.Scan(); Check(Stage(f) == 19 && f.Engine.Active != null, "Terminal state timed out.");
        });
        run("Twintania state conditions can finish early and overdue uses expected plus one", () =>
        {
            using var f = Scene(); Dive(f);
            f.Now = f.Now.AddSeconds(6.19); f.Engine.Tick(f.Now); Check(Stage(f) == 3, "Early marker timeout.");
            f.Now = f.Now.AddSeconds(.011); f.Engine.Tick(f.Now); Check(Stage(f) == 5, "Overdue or immediate empty resolve.");
            Effect(f, 1456, 101); Check(Stage(f) == 6, "Early marker blocked by expected delay.");
            Effect(f, 1247); Check(Stage(f) == 7, "Early hit blocked.");
        });
        run("Twintania P1 and P2 preserve Plummet timer and P4 recreates common components", () =>
        {
            using var f = Scene();
            Check(Group(f, "plummet/outline").Single().Activation == f.Start.AddSeconds(6.5), "Initial Plummet.");
            Effect(f, 1240); var expected = f.Now.AddSeconds(12.5);
            Link(f, 10);
            Check(Group(f, "plummet/outline").Single().Activation == expected, "P2 reset common timer.");
            Profusion(f); Effect(f, 1248);
            Check(Group(f, "plummet/outline").Single().Activation == f.Now.AddSeconds(6.5), "P4 did not reset timer.");
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("当前坦克 MT")), "P4 tank role reset.");
        });
        run("Twintania DeathSentence raw start drift toggles once and counts only1458 effects", () =>
        {
            using var f = Scene(); Role(f, "MT", 21);
            Cast(f, 1458, 4);
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("当前坦克 OT"))
                && f.Engine.TwintaniaActions().Select(a => a.Action).SequenceEqual([44u]), "Old tank Vengeance.");
            var cast = f.Actors[1].Cast!;
            f.Actors[1] = f.Actors[1] with { Cast = cast with { StartedAt = cast.StartedAt.AddSeconds(.03) } }; f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("当前坦克 OT")), "Raw clock drift toggled tank twice.");
            Effect(f, 1241); Effect(f, 1242);
            Check(Field<int>(f, "twinSentenceHits") == 0, "Damage effect counted as visual.");
            Effect(f, 1458); Check(Field<int>(f, "twinSentenceHits") == 1, "Visual effect not counted.");
        });
        run("Twintania DeathSentence WAR PLD mitigation rotation Feint Addle availability and deadline", () =>
        {
            using var f = Scene(); Role(f, "MT", 19); Cast(f, 1458, 4);
            Check(Actions(f).SequenceEqual([3542u, 17u]), "PLD first rotation.");
            Role(f, "M1", 22); Check(Actions(f).SequenceEqual([7549u]), "M1 Feint.");
            Effect(f, 1458); Role(f, "M2", 30); Check(Actions(f).SequenceEqual([7549u]), "M2 Feint.");
            Effect(f, 1458); Role(f, "R1", 25); Check(Actions(f).SequenceEqual([7560u]), "R1 Addle.");
            Role(f, "R1", 23); Check(Actions(f).Length == 0, "Physical ranged asked to Addle.");
            Role(f, "MT", 21); Check(Actions(f).SequenceEqual([7531u, 7535u, 40u]), "WAR second rotation.");
            Role(f, "MT", 19); Check(Actions(f).SequenceEqual([3542u, 7531u, 7535u]), "PLD second rotation.");
            f.Now = f.Start.AddSeconds(4); f.Engine.Tick(f.Now);
            Check(Actions(f).Length == 0, "Action after true cast finish.");
        });
        run("Twintania P1 Scourge priority gather and LiquidHell effect live consumption", () =>
        {
            using var f = Scene(); Role(f, "M1", 22);
            Check(Group(f, "p1/gather").Length == 1, "Non tank gather.");
            f.Actors[20] = new(20, 0x7E7, new(2, 0, 2)); f.Scan();
            Check(Group(f, "p1/gather").Length == 0 && f.Engine.PriorityTarget(1, Vector3.Zero) == 20, "Scourge priority.");
            Cast(f, 1243, 2, 101); Effect(f, 1243, 101);
            var puddle = Group(f, "liquid/event").Single();
            Check(puddle.Activation == f.Now && puddle.Position == f.Actors[101].Position, "P1 puddle clock.");
            f.Actors[21] = new(21, 0x1E88FE, puddle.Position, EventState: 7); f.Scan();
            Check(Group(f, "liquid/event").Length == 1, "Retired puddle consumed prediction.");
            f.Actors[21] = f.Actors[21] with { EventState = 0 }; f.Scan();
            Check(Group(f, "liquid/event").Length == 0 && Group(f, "liquid/live").Length == 1, "Live puddle reconciliation.");
            Link(f, 10); Check(!Group(f, "liquid").Any(), "P1 puddle survived component exit.");
        });
        run("Twintania fireball actual combat roles drive grouped damage and independent4.5s clock", () =>
        {
            using var f = Scene(); Link(f, 10); Role(f, "M1", 22);
            Effect(f, 1452, 102);
            Check(Group(f, "fireball/outline").Single().Shape is CircleTemplate { Radius: 4 }
                && Group(f, "fireball/position").Single().Activation == f.Now.AddSeconds(4.5), "Fireball outputs.");
            var victims = f.Engine.PredictedDamage.Select(p => p.TargetId).ToArray();
            Check(victims.Contains(102UL) && victims.Contains(103UL) && !victims.Contains(106UL) && !victims.Contains(101UL), "Healer group damage.");
            var position = Group(f, "fireball/position").Single().Position;
            Effect(f, 1452, 106);
            Check(Group(f, "fireball/position").Single().Position != position, "Melee did not follow fireball group.");
            victims = f.Engine.PredictedDamage.Select(p => p.TargetId).ToArray();
            Check(victims.Contains(106UL) && !victims.Contains(102UL), "Ranged group damage.");
            Effect(f, 1246); Check(Group(f, "fireball/outline").Length == 0, "Fireball clear.");
        });
        run("Twintania conflag personal clears all common forbidden navigation while retaining display", () =>
        {
            using var f = Scene(); Link(f, 10); Role(f, "M1", 22);
            f.Actors[20] = new(20, 0x7F2, Vector3.Zero); f.Scan();
            Effect(f, 1452, 102); Effect(f, 1451, 100);
            Check(Group(f, "conflag/personal").Single().Activation == f.Now.AddSeconds(2.5), "Fetters deadline.");
            Check(Group(f, "plummet/outline").Length == 1 && !Group(f, "plummet/outline").Single().Navigate
                && Group(f, "conflag/").Length == 1 && Group(f, "fireball/position").Length == 0, "Conflag forbidden clear parity.");
            Check(Group(f, "fireball/outline").Length == 1 && f.Draw.Previews.Count >= 2, "Personal navigation removed common drawings.");
            f.Now = f.Now.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "conflag/personal").Length == 1, "Conflag target timer cleared without status.");
            f.Engine.StatusChanged(100, 292, true, f.Now, expiresAt: f.Now.AddSeconds(10)); f.Render();
            Check(Group(f, "conflag/personal").Length == 0 && Group(f, "conflag/20").Length == 1, "292 gain failed to release target.");
        });
        run("Twintania raw Fetters new gain clears target but unchanged old status cannot erase new marker", () =>
        {
            using var f = Scene(); Link(f, 10); Effect(f, 1451, 100);
            f.Actors[100] = f.Actors[100] with { Statuses = [292] }; f.Scan();
            Check(Group(f, "conflag/personal").Length == 0, "Raw status gain.");
            Effect(f, 1451, 100); f.Scan();
            Check(Group(f, "conflag/personal").Length == 1, "Old status erased later marker.");
        });
        run("Twintania Divebomb locks marker position follows boss origin uses cast direction and effect clear", () =>
        {
            using var f = Scene(); Dive(f);
            Effect(f, 1456, 101);
            var first = Group(f, "divebomb").Single();
            f.Actors[101] = f.Actors[101] with { Position = new(-20, 0, -20) }; f.Scan();
            Check(Group(f, "divebomb").Single().Rotation == first.Rotation, "Marker tracked moving target.");
            Cast(f, 1247, 3, rotation: .9f);
            Check(Group(f, "divebomb").Single() is { Rotation: .9f, Shape: RayRectangleTemplate { Length: 35, MaxLocalX: 6 } }, "Locked cast rectangle.");
            f.Engine.Finish(1, 1247, f.Now, completed: true); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Render();
            Check(Group(f, "divebomb").Length == 1, "Cast finish cleared effect-owned target.");
            Effect(f, 1247); Check(Group(f, "divebomb").Length == 0, "Effect clear.");
        });
        run("Twintania Hygieia chooses minimum identity and uses pending HP and348 ten second boundary", () =>
        {
            using var f = Scene(); Adds(f);
            f.Actors[31] = new(31, 0x7E8, new(5, 0, 5), Hp: 900, MaxHp: 1000);
            f.Actors[32] = new(32, 0x7E8, new(7, 0, 7), Hp: 100, MaxHp: 1000); f.Scan();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 30, "MT must retain ownership of Asclepius over the damage priority.");
            Role(f, "M1", 22);
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 31, "Used lowest HP instead of lowest ID.");
            f.Engine.StatusChanged(30, 348, true, f.Now, expiresAt: f.Now.AddSeconds(10)); f.Render();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 30, "Exactly10s must not kill snake.");
            f.Now = f.Now.AddSeconds(.01); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 31, "Below10s should kill snake.");
            f.Engine.StatusChanged(30, 348, true, f.Now, expiresAt: f.Now.AddSeconds(20));
            f.Actors[31] = f.Actors[31] with { Hp = 400, IncomingEffects = [new(0, 2, 0, false, false) { HpDelta = -101 }] }; f.Scan();
            Check(Priority(f, 31) == -1, "Pending HP30% threshold.");
            f.Actors[31] = f.Actors[31] with { Hp = 1, IncomingEffects = [] }; f.Scan();
            Check(Priority(f, 31) == 0, "HP1 priority0.");
        });
        run("Twintania small snake danger uses R9 predicted thresholds display usesR8 and adds persist toP5", () =>
        {
            using var f = Scene(); Adds(f);
            f.Actors[31] = new(31, 0x7E8, new(5, 0, 5), Hp: 101, MaxHp: 1000,
                IncomingEffects: [new(0, 2, 0, false, false) { HpDelta = -1 }]); Role(f, "M1", 22); f.Scan();
            Check(Group(f, "hygieia/avoid").Single().Shape is CircleTemplate { Radius: 9 }
                && Group(f, "hygieia/outline").Single().Shape is CircleTemplate { Radius: 8 }, "R8 display/R9 navigation.");
            Role(f, "OT", 21); Check(Group(f, "hygieia/avoid").Length == 0, "OT uses1HP threshold.");
            f.Actors[31] = f.Actors[31] with { IncomingEffects = [new(0, 2, 0, false, false) { HpDelta = -100 }] }; f.Scan();
            Check(Group(f, "hygieia/avoid").Length == 1, "OT pending lethal.");
            Profusion(f); Effect(f, 1248); Link(f, 12);
            Check(Stage(f) == 19 && Group(f, "hygieia/avoid").Length == 1, "Adds component ended before fight.");
        });
        run("Twintania Profusion MT and others use distinct links at6.7s and raidwide prediction", () =>
        {
            using var f = Scene(); Role(f, "MT", 19); Profusion(f);
            var personal = Group(f, "profusion/position").Single();
            Check(personal.Position == f.Actors[11].Position && personal.Activation == f.Now.AddSeconds(6.7), "MT link.");
            Role(f, "R1", 25);
            Check(Group(f, "profusion/position").Single().Position == f.Actors[10].Position
                && Actions(f).Contains(7560u) && f.Engine.PredictedDamage.Count() == 8, "Other link/Addle/raidwide.");
            f.Now = f.Now.AddSeconds(7.71); f.Engine.Tick(f.Now); f.Render();
            Check(Stage(f) == 18 && Group(f, "profusion").Length == 0, "Profusion overdue.");
        });
        run("Twintania Twister prediction starts at true finish and any object clears while live useshitbox", () =>
        {
            using var f = Scene(); P4(f); Cast(f, 1249, 3);
            f.Now = f.Now.AddSeconds(2.99); f.Scan();
            Check(Group(f, "twister/prediction").Length == 0 && f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("保持移动")), "Early twister prediction.");
            f.Now = f.Now.AddSeconds(.02); f.Scan();
            Check(Group(f, "twister/prediction").Length == 0, "The NPC action delay was removed a second time.");
            f.Actors[100] = f.Actors[100] with { Position = new(9, 0, 9) };
            f.Now = f.Now.AddSeconds(.3); f.Scan();
            Check(Group(f, "twister/prediction").Length == 8, "True finish missing party predictions.");
            Check(Group(f, "twister/prediction").Any(a => a.Position == new Vector3(9, 0, 9)),
                "Twister prediction froze a party position before the true cast finish.");
            f.Actors[40] = new(40, 0x1E8910, new(5, 0, 5), EventState: 7, HitboxRadius: .7f); f.Scan();
            Check(Group(f, "twister").Length == 0, "Retired object should clear predictions.");
            f.Actors[40] = f.Actors[40] with { EventState = 0 }; f.Scan();
            Check(Group(f, "twister/outline").Single().Shape is CircleTemplate { Radius: .7f }
                && Group(f, "twister/avoid").Single().Shape is CircleTemplate { Radius: 1.7f }, "Hitbox and cushion.");
        });
        run("Twintania Dreadknight pretarget avoidance posttarget tether and job specific crowd control", () =>
        {
            using var f = Scene(); P4(f);
            f.Actors[41] = new(41, 0x7EA, new(4, 0, 4)); f.Scan();
            Check(Group(f, "dreadknight/avoid").Single().Shape is CircleTemplate { Radius: 15 }, "Pretarget avoid.");
            Effect(f, 1251, 101);
            Role(f, "R1", 23); Check(Group(f, "dreadknight/line").Length == 1 && Group(f, "dreadknight/avoid").Length == 0
                && Actions(f).SequenceEqual([7554u]), "Leg Graze and tether.");
            Role(f, "M1", 22); Check(Actions(f).SequenceEqual([7863u]), "Leg Sweep.");
            Role(f, "MT", 19); Check(Actions(f).SequenceEqual([7540u]) && Priority(f, 41) == 0, "Tank Low Blow and priority0.");
            f.Actors[41] = f.Actors[41] with { Dead = true }; f.Scan();
            Check(Actions(f).Length == 0 && Group(f, "dreadknight/line").Length == 0, "Dead knight cleanup.");
        });
        run("Twintania P5 LiquidHell count persists across markers and effect prediction1.5s", () =>
        {
            using var f = Scene(); P5(f);
            Effect(f, 1457, 100);
            for (var i = 0; i < 4; ++i) Effect(f, 670, 101);
            Check(Group(f, "liquid/bait").Length == 7 && Group(f, "liquid/event").Length == 4
                && Group(f, "liquid/event").All(a => a.Activation == f.Now.AddSeconds(1.5)), "Four hits.");
            Effect(f, 1457, 100); Effect(f, 670, 101);
            Check(Group(f, "liquid/bait").Length == 0, "New marker reset global five-hit counter.");
            f.Now = f.Now.AddSeconds(50); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "liquid/event").Length == 5, "Prediction gained invented timeout.");
        });
        run("Twintania Hatch target uses union without underboss link default link dangerR5", () =>
        {
            using var f = Scene(); P5(f);
            Check(Group(f, "hatch/avoid").Length == 3, "Default link avoidance.");
            Effect(f, 1453, 100);
            var target = Group(f, "hatch/target").Single();
            var geometry = target.Shape.Resolve(target.Position, 0, null);
            Check(geometry.Contains(f.Actors[10].Position) && !geometry.Contains(f.Actors[11].Position)
                && !geometry.Contains(f.Actors[12].Position) && Group(f, "hatch/avoid").Length == 0, "Hatch safe union.");
            Check(Group(f, "hatch/outline").All(a => a.Safe), "Personal green link drawings.");
            Effect(f, 1256); Check(Group(f, "hatch/avoid").Length == 3, "Hatch clear restores default.");
        });
        run("Twintania Hatch OT unsafe link skips general paths while other members avoid width4paths", () =>
        {
            using var f = Scene(); P5(f);
            f.Actors[42] = new(42, 0x7F3, new(-10, 0, 0), Dead: true); f.Scan();
            Effect(f, 1453, 106); Role(f, "OT", 21);
            Check(Group(f, "hatch/intercept").Length == 1 && Group(f, "hatch/avoid").Length == 0, "OT intercept.");
            f.Actors[43] = new(43, 0x1E88FE, Vector3.Zero); f.Scan();
            Check(Group(f, "hatch/intercept").Length == 0 && Group(f, "hatch/avoid").Length == 3
                && Group(f, "hatch/orb-path").Length == 0, "Unsafe OT branch incorrectly fell through.");
            Role(f, "M1", 22);
            Check(Group(f, "hatch/orb-path").Single().Shape is RayRectangleTemplate { MinLocalX: -2, MaxLocalX: 2 }
                && Group(f, "hatch/target-path").Length == 1 && Group(f, "hatch/line").Length == 1, "Other member paths/all orbs.");
        });
        run("Twintania action execution throttle expiry movement replay and death gates", () =>
        {
            var control = new DungeonActionControl();
            var now = DateTime.UtcNow;
            var player = new ReplayActorState { EntityId = 100 };
            DungeonActionRequest[] requests = [new(44, 100, now.AddSeconds(1)), new(7531, 100, now.AddSeconds(1))];
            var calls = 0;
            bool Execute(DungeonActionRequest action) { ++calls; return true; }
            control.Update(requests, false, player, now, Execute); Check(calls == 0, "Replay/movement gate bypass.");
            control.Update(requests, true, player, now, Execute);
            control.Update(requests, true, player, now.AddSeconds(.099), Execute); Check(calls == 1, "Throttle.");
            control.Update(requests, true, player, now.AddSeconds(.1), Execute); Check(calls == 2, "Renew.");
            control.Update(requests, true, player, now.AddSeconds(1), Execute); Check(calls == 2, "Expired action.");
            control.Update(requests, true, player with { Dead = true }, now, Execute); Check(calls == 2, "Dead action.");
        });
        run("Twintania personal Conflag reaches boss and actual solver output owner cleans", () =>
        {
            using var f = Scene(); Link(f, 10); Role(f, "M1", 22);
            f.Actors[100] = f.Actors[100] with { Position = new(8, 0, 0) }; f.Scan();
            Effect(f, 1451, 100);
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && path.Count > 1
                && Vector3.Distance(path[^1].Position, Vector3.Zero) < 1.5f, "Conflag actual route.");
            f.Output.Clear(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Owner cleanup.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x7E5, 245);
        f.Actors[1] = f.Actors[1] with { TargetId = 101, Hp = 1000, MaxHp = 1000 };
        var roles = new[] { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };
        uint[] jobs = [19, 21, 24, 28, 22, 30, 25, 23];
        for (var i = 0; i < 8; ++i)
            f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, new(i + 2, 0, i + 1),
                PartySlot: i, PartyRole: i < 2 ? "Tank" : i < 4 ? "Healer" : i < 6 ? "Melee" : "Ranged",
                ClassJob: jobs[i], Hp: 1000, MaxHp: 1000);
        f.Engine.CurrentPartyAssignments = roles;
        f.Scan();
        return f;
    }
    private static void Role(Fight f, string role, uint job)
    {
        f.Engine.CurrentPartyAssignments = [role, "OT", "H1", "H2", "M1", "M2", "R1", "R2"];
        f.Actors[100] = f.Actors[100] with { ClassJob = job }; f.Scan();
    }
    private static void Link(Fight f, ulong id)
    {
        f.Actors[id] = new(id, 0x7E6, id == 10 ? Vector3.Zero : new((float)(id - 10) * 10, 0, 0),
            Targetable: false, Hostile: false); f.Scan();
    }
    private static void Dive(Fight f)
    {
        if (Stage(f) == 0) Link(f, 10);
        if (Stage(f) == 1) Link(f, 11);
        f.Actors[1] = f.Actors[1] with { Targetable = false, Cast = null }; f.Scan();
    }
    private static void Adds(Fight f)
    {
        Dive(f);
        for (var i = 0; i < 3; ++i) { Effect(f, 1456, 101); Effect(f, 1247); }
        f.Actors[30] = new(30, 0x7E9, new(2, 0, 2), Hp: 1000, MaxHp: 1000); f.Scan();
    }
    private static void Profusion(Fight f)
    {
        if (Stage(f) < 9) Adds(f);
        for (var i = 0; i < 3; ++i) { Effect(f, 1456, 101); Effect(f, 1247); }
        f.Actors[1] = f.Actors[1] with { Targetable = true, Cast = null }; f.Scan();
    }
    private static void P4(Fight f) { Profusion(f); Effect(f, 1248); }
    private static void P5(Fight f) { P4(f); Link(f, 12); }
    private static void Cast(Fight f, uint action, double duration, ulong target = 0, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, Vector3.Zero, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
    }
    private static uint sequence;
    private static void Effect(Fight f, uint action, ulong target = 0)
    {
        f.Engine.Effect(1, action, ++sequence, f.Now, target); f.Engine.Tick(f.Now); f.Render();
    }
    private static T Field<T>(Fight f, string name) => (T)typeof(DungeonEncounter).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Engine)!;
    private static int Stage(Fight f) => Field<int>(f, "twinStage");
    private static int Priority(Fight f, ulong id) => (int)typeof(DungeonEncounter).GetMethod("TwinPriority", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(f.Engine, [f.Actors[id]])!;
    private static uint[] Actions(Fight f) => f.Engine.TwintaniaActions().Select(a => a.Action).ToArray();
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("twin/" + prefix)).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
