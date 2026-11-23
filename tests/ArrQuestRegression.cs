using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class ArrQuestRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("ArrQuest native quest territories separate field maps and preserve personal death", () =>
        {
            foreach (var (territory, boss, radius) in new (uint, uint, float)[] { (1049, 0x38F5, 14.5f), (1052, 0x3933, 20) })
            {
                using var f = Scene(territory, boss); var c = f.Engine.Arena!.Position;
                Check(f.Engine.Active?.BossOid == boss && f.Engine.Arena.Contains(c + Vector3.UnitX * (radius - .01f))
                    && !f.Engine.Arena.Contains(c + Vector3.UnitX * (radius + .01f)), "Arena/activation.");
                f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
                Check(f.Engine.PreserveFightAcrossPlayerDeath && f.Engine.Active != null, "Personal death.");
                f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe repull.");
                f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
                f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0.");
            }
            using var field = new Fight(0x38F5, 140); Check(field.Engine.Active == null, "Field map claimed.");
            using var cutscene = new Fight(0x3933, 1053); Check(cutscene.Engine.Active == null, "Cutscene map claimed.");
        });
        foreach (var (territory, boss, action, source) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (1049, 0x38F5, 28871, new BossMod.AOEShapeCircle(7)),
            (1049, 0x38F5, 28865, new BossMod.AOEShapeCircle(7)),
            (1049, 0x38F5, 28869, new BossMod.AOEShapeCircle(11)),
            (1049, 0x38F5, 28870, new BossMod.AOEShapeRect(30, 2.5f)),
            (1049, 0x38F5, 28874, new BossMod.AOEShapeRect(30, 2.5f)),
            (1049, 0x38F5, 28877, new BossMod.AOEShapeCircle(14)),
            (1049, 0x38F5, 28876, new BossMod.AOEShapeRect(30, 15)),
            (1052, 0x3933, 29329, new BossMod.AOEShapeCircle(1)),
            (1052, 0x3933, 29331, new BossMod.AOEShapeRect(60, 4)),
            (1052, 0x3933, 29762, new BossMod.AOEShapeRect(60, 4)),
            (1052, 0x3933, 29335, new BossMod.AOEShapeCircle(6)),
            (1052, 0x3933, 29760, new BossMod.AOEShapeCone(40, new(MathF.PI / 6))),
            (1052, 0x3933, 29337, new BossMod.AOEShapeCone(40, new(MathF.PI * 5 / 12))),
            (1068, 0x3A5F, 31262, new BossMod.AOEShapeCone(9, new(MathF.PI / 4))),
            (1068, 0x3A5F, 30880, new BossMod.AOEShapeCircle(31)),
            (1068, 0x3A5F, 26410, new BossMod.AOEShapeCircle(31)),
            (1068, 0x3A5F, 30879, new BossMod.AOEShapeCone(50, new(MathF.PI / 3))),
            (1068, 0x3A5F, 30894, new BossMod.AOEShapeCircle(6)),
            (1068, 0x3A5F, 30883, new BossMod.AOEShapeCircle(10)),
        })
            run($"ArrQuest {territory} action{action} matches exact source geometry and observed ending", () =>
            {
                using var f = Scene(territory, boss); Start(f, 10, action, 3, .7f);
                var cast = f.Engine.Casts[10]; var shape = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (var x = -64f; x <= 64; x += .83f)
                    for (var z = -64f; z <= 64; z += .89f)
                        Check(shape.Contains(cast.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(cast.Location.X + x, cast.Location.Z + z), new(cast.Location.X, cast.Location.Z), new(.7f)), "Source shape.");
                var background = territory == 1068 ? 1 : 0;
                Check(f.Draw.Previews.Count == 1 + background && f.Snapshot.DangerAreas.Count == 1, "Views/navigation.");
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render(); Check(f.Draw.Previews.Count == 1 + background, "Nominal expiry.");
                f.Engine.Finish(10, action, f.Now, false); f.Render();
                Check(f.Draw.Previews.Count == background && f.Snapshot.DangerAreas.Count == 0, "Cancel.");
            });
        run("ArrQuest Archon parry all sixteen masks honor source cardinal sides target-only navigation and status packets", () =>
        {
            using var f = Scene(1049, 0x38F5); var c = f.Engine.Arena!.Position;
            foreach (var (side, offset) in new[] { (1, Vector3.UnitZ), (2, -Vector3.UnitZ), (4, Vector3.UnitX), (8, -Vector3.UnitX) })
                for (ushort mask = 0; mask < 16; ++mask)
                {
                    f.Actors[100] = f.Actors[100] with { Position = c + offset * 5, TargetId = 1 };
                    f.Engine.StatusChanged(1, 680, mask != 0, f.Now, mask); f.Scan();
                    Check(f.Engine.ForbiddenTargets.Contains(1ul) == ((mask & side) != 0), $"Mask{mask}/side{side}.");
                    var moves = Group(f, "archon/parry/move");
                    Check(moves.Length == ((mask & side) == 0 ? 0 : System.Numerics.BitOperations.PopCount(mask)), "Parry navigation.");
                    f.Actors[100] = f.Actors[100] with { TargetId = 0 }; f.Scan();
                    Check(Group(f, "archon/parry/move").Length == 0, "No target still forced parry navigation.");
                }
            f.Engine.StatusChanged(1, 680, false, f.Now); f.Render();
            Check(f.Engine.ForbiddenTargets.Length == 0 && Group(f, "archon/parry").Length == 0, "Status loss.");
        });
        run("ArrQuest Archon parry tracks rotation and real solver reaches unguarded side", () =>
        {
            using var f = Scene(1049, 0x38F5); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitZ * 5, TargetId = 1 };
            f.Engine.StatusChanged(1, 680, true, f.Now, 1); f.Scan();
            Check(f.Engine.ForbiddenTargets.SequenceEqual(new ulong[] { 1 }), "Front ban.");
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 1
                && !Group(f, "archon/parry/move").Single().Shape.Resolve(c, 0, null).Contains(path[^1].Position), "Parry actual path.");
            f.Actors[1] = f.Actors[1] with { Rotation = MathF.PI }; f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 0 && Group(f, "archon/parry/move").Length == 0, "Rotation ban stale.");
        });
        run("ArrQuest Lahabrea arena telegraph plus0.7 map commits smaller boundary without premature finish", () =>
        {
            using var f = Scene(1052, 0x3933); var c = f.Engine.Arena!.Position;
            Start(f, 1, 29327, 4);
            Check(Math.Abs((Group(f, "lahabrea/arena").Single().Activation!.Value - f.Now.AddSeconds(5)).Ticks) <= 1
                && Math.Abs((f.Engine.PredictedDamage.Single().Activation - f.Now.AddSeconds(4.3)).Ticks) <= 1,
                "Arena/raidwide clock.");
            f.Engine.Finish(1, 29327, f.Now.AddSeconds(4.3), true); f.Render();
            Check(f.Engine.Arena.Contains(c + Vector3.UnitX * 18), "Shrank at cast finish.");
            f.Engine.MapEffect(1, 0x00020001); f.Render(); Check(f.Engine.Arena.Contains(c + Vector3.UnitX * 18), "Wrong map index.");
            f.Engine.MapEffect(0, 0x00020001); f.Render();
            Check(!f.Engine.Arena.Contains(c + Vector3.UnitX * 18) && f.Engine.Arena.Contains(c + Vector3.UnitX * 14.9f), "Map shrink.");
            var first = Group(f, "lahabrea/arena").Single();
            f.Now = f.Now.AddSeconds(10); Start(f, 1, 29327, 4);
            Check(Group(f, "lahabrea/arena").Single().Activation == first.Activation, "Repeated fire overwrote completed telegraph.");
        });
        run("ArrQuest Lahabrea sphere summon actual finish plus12 overrides with Burst clocks expires strictly and gates knockback hints", () =>
        {
            using var f = Scene(1052, 0x3933); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x3934, c + Vector3.UnitX * 10); f.Scan();
            Start(f, 1, 29332, 4); Check(Group(f, "lahabrea/sphere").Length == 0, "Summon start predicted too early.");
            f.Engine.Finish(1, 29332, f.Now, true); f.Render();
            Check(Group(f, "lahabrea/sphere").Single().Activation == f.Now.AddSeconds(12), "Summon observed clock.");
            Start(f, 10, 29758, 15);
            Check(Group(f, "lahabrea/knockback/avoid").Length == 0, "Knockback hint before sphere expiry.");
            Start(f, 20, 29333, 3);
            var at = f.Engine.Casts[20].FinishAt;
            Check(Group(f, "lahabrea/sphere").Single().Activation == at, "Burst override.");
            f.Now = at; f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "lahabrea/sphere").Length == 0 && Group(f, "lahabrea/knockback/avoid").Length == 1, "Strict time gate.");
            f.Engine.Finish(10, 29758, f.Now, true); f.Render(); Check(Group(f, "lahabrea/knockback").Length == 0, "Knockback finish.");
        });
        run("ArrQuest Lahabrea Cross FIFO maximum8 preserves hidden ninth and live pitch State7", () =>
        {
            using var f = Scene(1052, 0x3933); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i <= 18; ++i) Start(f, i, 29756, 3 + i);
            Check(Group(f, "lahabrea/cross").Length == 8 && f.Engine.Casts.Count == 9, "Cross limit dropped hidden cast.");
            f.Engine.Finish(10, 29756, f.Now, false); f.Render();
            Check(Group(f, "lahabrea/cross").Length == 8 && Group(f, "lahabrea/cross").Any(a => a.Id.EndsWith("/18")), "Cross FIFO shift.");
            f.Actors[20] = new(20, 0x1EB738, c); f.Scan(); Check(Group(f, "lahabrea/pitch").Length == 1, "Live pitch.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; f.Scan(); Check(Group(f, "lahabrea/pitch").Length == 0, "Retired pitch.");
            f.Output.Clear(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Owner cleanup.");
        });
        RunSteps(run);
    }

    private static void RunSteps(Action<string, Action> run)
    {
        run("ArrQuest Vishap accepts untargetable primary and six scrolling map phases preserve bounds", () =>
        {
            using var f = Scene(1068, 0x3A5F);
            f.Engine.ResetFight(); f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(f.Engine.Active != null && Group(f, "steps/boss").Length == 1, "Untargetable primary.");
            foreach (var (index, state, min, max) in new (uint, uint, float, float)[]
            {
                (99, 0u, 160, 260), (3, 0x00020001, 160, 160), (0, 0x00800040, 0, 160),
                (4, 0x00020001, 0, 0), (1, 0x00800040, -160, 0), (6, 0x00020001, -160, -160),
            })
            {
                f.Engine.MapEffect(index, state);
                foreach (var z in new[] { -400f, -100f, 30f, 175f, 400f })
                {
                    f.Actors[100] = f.Actors[100] with { Position = new(0, 0, z) }; f.Scan();
                    var c = f.Engine.Arena!.Position;
                    Check(c.Z == Math.Clamp(z, min, max) && f.Engine.Arena.Contains(c + new Vector3(21.99f, 0, 39.99f))
                        && !f.Engine.Arena.Contains(c + new Vector3(22.01f, 0, 0)), "Scroll clamp/size.");
                    var boundary = f.Engine.Arena; f.Scan(); Check(ReferenceEquals(boundary, f.Engine.Arena), "Unchanged center rebuilt boundary.");
                }
            }
            f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Arena!.Position.Z == 260, "Reset bounds.");
        });
        run("ArrQuest Vishap advance SDRect exact source order combat and InEvent gate plus real path", () =>
        {
            using var f = Scene(1068, 0x3A5F);
            f.Actors[100] = f.Actors[100] with { InCombat = false }; f.Scan();
            var a = Group(f, "steps/advance").Single(); var c = f.Engine.Arena!.Position;
            var source = new BossMod.SDRect(new(c.X, c.Z), new BossMod.WDir(0, 1), 38, 22, 40);
            for (float x = -45; x < 45; x += 1.7f)
                for (float z = -45; z < 45; z += 1.9f)
                    Check(a.Shape.Resolve(a.Position, a.Rotation, null).Contains(c + new Vector3(x, 0, z), 0)
                        == (source.Distance(new(c.X + x, c.Z + z)) <= 0), "SDRect parameter order.");
            var s = f.Snapshot;
            var path = new SafeZoneSolver().Solve(s, new(f.Actors[100].Position, 0, 6, false, false, 0, true, s.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(path.Status == SolutionStatus.Success && !path.IsLeastDangerFallback
                && path.ExecutablePath.Concat(path.ContinuationPath).Any(p => p.Position.Z < c.Z - 22), "Advance actual path.");
            f.Engine.StatusChanged(100, 1268, true, f.Now); f.Render(); Check(Group(f, "steps/advance").Length == 0, "InEvent.");
            f.Engine.StatusChanged(100, 1268, false, f.Now);
            foreach (var (index, threshold) in new (uint, float)[] { (0, 25), (1, -135) })
            {
                f.Engine.MapEffect(index, 0x00800040);
                f.Actors[100] = f.Actors[100] with { Position = new(0, 0, threshold + .1f), InCombat = true }; f.Scan();
                Check(Group(f, "steps/advance").Length == 1, "Phase advance.");
                f.Actors[100] = f.Actors[100] with { Position = new(0, 0, threshold) }; f.Scan();
                Check(Group(f, "steps/advance").Length == 0, "Threshold must be strict.");
            }
        });
        run("ArrQuest Vishap Earthrising two independent five-hit lines follow caster effects not finishes", () =>
        {
            using var f = Scene(1068, 0x3A5F); var c = f.Engine.Arena!.Position;
            Start(f, 10, 30888, 7); f.Actors[11] = new(11, 0x233C, c + Vector3.UnitX * 12); f.Scan(); Start(f, 11, 30888, 9);
            var first = Group(f, "steps/earthrising").Where(a => a.Id.Contains("/1/")).ToArray();
            Check(first.Length == 2 && first[1].Activation == first[0].Activation!.Value.AddSeconds(1), "Preview clock/count.");
            f.Engine.Finish(10, 30888, f.Now, false); f.Render(); Check(Group(f, "steps/earthrising").Length == 4, "Finish cleared sequence.");
            for (var i = 0; i < 5; ++i)
            {
                f.Now = f.Now.AddSeconds(1.3);
                f.Actors[10] = f.Actors[10] with { Position = c - Vector3.UnitZ * (7.5f * i) }; f.Scan();
                f.Engine.Effect(10, i == 0 ? 30888u : 26412u, (uint)i, f.Now, location: c + Vector3.UnitX * 80); f.Render();
                var remaining = Group(f, "steps/earthrising").Where(a => a.Id.Contains("/1/")).ToArray();
                Check(remaining.Length == Math.Min(4 - i, 2), "Effect count.");
                if (remaining.Length > 0)
                    Check(remaining[0].Activation == f.Now.AddSeconds(1), "Effect plus1 clock.");
                Check(Group(f, "steps/earthrising").Count(a => a.Id.Contains("/2/")) == 2, "Other line consumed.");
            }
            var second = Group(f, "steps/earthrising");
            f.Engine.Effect(11, 26412, 99, f.Now); f.Render();
            Check(Group(f, "steps/earthrising").Length == 2 && Group(f, "steps/earthrising")[0].Position != second[0].Position, "Second line effect.");
        });
        run("ArrQuest Vishap FlameBreath frozen primary plus1 and independent14 versus35 observed hits", () =>
        {
            using var f = Scene(1068, 0x3A5F); var c = f.Engine.Arena!.Position;
            f.Actors[1] = f.Actors[1] with { Rotation = .7f }; f.Scan(); Start(f, 10, 30185, 5, 1.2f);
            var first = Group(f, "steps/flame").Single();
            Check(first.Position == c && first.Rotation == .7f && first.Activation == f.Engine.Casts[10].FinishAt.AddSeconds(1), "Primary snapshot/clock.");
            f.Engine.Finish(10, 30185, f.Now, false); f.Actors[1] = f.Actors[1] with { Position = c + Vector3.UnitX * 10, Rotation = 2 }; f.Scan();
            Check(Group(f, "steps/flame").Single() == first, "Source finish/movement cleared frozen flame.");
            Start(f, 11, 26411, 4, 1.2f);
            for (uint i = 1; i <= 35; ++i)
            {
                f.Engine.Effect(20, 30884, i, f.Now); f.Render();
                Check(Group(f, "steps/flame2").Length == (i < 14 ? 1 : 0), "14-hit breath counter.");
                Check(Group(f, "steps/flame").Count(a => a.Id == "steps/flame") == (i < 35 ? 1 : 0), "35-hit breath counter.");
            }
        });
        run("ArrQuest Vishap Earthshaker FIFO2 retains hidden third and spread follows live targets", () =>
        {
            using var f = Scene(1068, 0x3A5F); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i <= 12; ++i) Start(f, i, 30887, 5 + i, .5f);
            Check(Group(f, "steps/earthshaker").Length == 2 && f.Engine.Casts.Count == 3, "FIFO2.");
            f.Engine.Finish(10, 30887, f.Now, false); f.Render();
            Check(Group(f, "steps/earthshaker").Any(a => a.Id.EndsWith("/12")), "Hidden third lost.");
            f.Actors[101] = new(101, 0, c + Vector3.UnitX * 3, PartySlot: 1, Hostile: false); f.Scan();
            Start(f, 20, 30875, 6);
            Check(Group(f, "party/display/20").Single().Position == f.Actors[100].Position && Group(f, "party/separate/20").Length == 1, "Self spread.");
            f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 6 }; f.Scan();
            Check(Group(f, "party/display/20").Single().Position == f.Actors[100].Position, "Live target.");
        });
        run("ArrQuest Vishap Levinshower exact five OIDs dynamic aim and target-specific avoid", () =>
        {
            using var f = Scene(1068, 0x3A5F); var c = f.Engine.Arena!.Position;
            foreach (var oid in new uint[] { 0x3AB3, 0x3AAB, 0x3AB6, 0x3AC2, 0x3AB8, 0x3AB2 })
            {
                f.Actors[20] = new(20, oid, c, TargetId: 100); f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 3, PartySlot: 1); f.Scan();
                var count = oid == 0x3AB2 ? 0 : 1;
                Check(Group(f, "steps/cleave/display").Length == count && Group(f, "steps/cleave/party").Length == count, "OID scope/self.");
                f.Actors[20] = f.Actors[20] with { TargetId = 101 }; f.Scan();
                Check(Group(f, "steps/cleave/avoid").Length == count && Group(f, "steps/cleave/party").Length == 0, "Other target.");
                f.Actors[20] = f.Actors[20] with { Targetable = false }; f.Scan(); Check(Group(f, "steps/cleave").Length == 0, "Untargetable add.");
            }
        });
        run("ArrQuest Vishap Cauterize center218 branch actual finish and Scorching persists live primary", () =>
        {
            using var f = Scene(1068, 0x3A5F); var c = f.Engine.Arena!.Position;
            Start(f, 10, 30885, 35);
            var a = Group(f, "steps/cauterize").Single();
            Check(a.Shape.Resolve(a.Position, 0, null).Contains(c - Vector3.UnitZ * 37)
                && !a.Shape.Resolve(a.Position, 0, null).Contains(c - Vector3.UnitZ * 39), "Initial MoveIt shape.");
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, 218) }; f.Scan();
            a = Group(f, "steps/cauterize").Single();
            Check(a.Position == f.Actors[1].Position && a.Rotation == MathF.PI, "Strict center218.");
            f.Engine.Finish(10, 30885, f.Now, false); f.Render(); Check(Group(f, "steps/cauterize").Length == 0, "Cauterize finish.");
            Start(f, 11, 29789, 20); f.Engine.Finish(11, 29789, f.Now, false);
            f.Actors[1] = f.Actors[1] with { Rotation = 1.3f, Position = c + Vector3.UnitX * 10 }; f.Scan();
            a = Group(f, "steps/scorching").Single();
            Check(a.Position == f.Actors[1].Position && a.Rotation == 1.3f, "Persistent Scorching follows primary.");
        });
        run("ArrQuest Vishap radial10 and directional20 stop before wall immunity and no radial origin vector", () =>
        {
            using var f = Scene(1068, 0x3A5F); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 20 }; f.Scan();
            Start(f, 10, 26401, 6, MathF.PI / 2);
            var landing = Group(f, "steps/knockback").Single(a => a.Id.EndsWith("/landing")).Position;
            Check(MathF.Abs(landing.X - (c.X + 22 - (.5f - .499f))) < .00001f, "Directional stopAtWall.");
            f.Engine.Finish(10, 26401, f.Now, false); Start(f, 11, 26408, 6);
            landing = Group(f, "steps/knockback").Single(a => a.Id.EndsWith("/landing")).Position;
            Check(landing.X < c.X + 22 && landing.X > c.X + 21.99f, "Radial stopAtWall.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; f.Scan();
            Check(Group(f, "steps/knockback").Length == 0, "Immunity.");
            f.Actors[100] = f.Actors[100] with { Statuses = [], Position = c }; f.Scan(); Check(Group(f, "steps/knockback").Length == 0, "Radial exact origin.");
        });
    }

    private static Fight Scene(uint territory, uint boss)
    {
        var f = new Fight(boss, territory); var c = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = c, Hp = 1000, MaxHp = 1000 };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 8, PartySlot: 0, Hostile: false, InCombat: territory == 1068);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0)
    {
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x233C, f.Engine.Arena!.Position); f.Scan(); }
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), 100);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
