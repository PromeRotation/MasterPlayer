using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Headsmen = BossMod.Dawntrail.Dungeon.D11MesoTerminal.D112FourHeadsmen.D112FourHeadsmen;

internal static class MesoRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Meso native discovery all bosses and no invented gaze shield or stop", () =>
        {
            Check(ArrDungeonCatalog.Create(1292).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.meso"), "Discovery.");
            foreach (var boss in new uint[] { 0x488F, 0x4891, 0x4890, 0x4892, 0x4893, 0x48BE }) Check(MesoCatalog.Owns(boss), "Ownership.");
            using var f = Prepare(0x4891);
            Check(!f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Invented controls.");
            f.Engine.StatusChanged(1, 2552, true, f.Now, 0x3A3);
            Check(f.Engine.ForbiddenTargets.Length == 0, "Invented shield restriction.");
        });
        foreach (var prisons in new Vector3[][]
        {
            [], [new(60, 0, -258)], [new(42.5f, 0, -258), new(77.5f, 0, -258)],
            [new(60, 0, -240)], [new(30.5f, 0, -258)], [new(60, 0, -258), new(68, 0, -258)],
        })
            run($"Meso exact prison difference source radar world {string.Join(";", prisons)}", () =>
            {
                var native = MesoPrisonArena.Build(0, prisons);
                var source = new BossMod.ArenaBoundsCustom(Headsmen.DefaultRect,
                    [.. Headsmen.Differences, .. prisons.Select(p => new BossMod.DonutV(new(p.X, p.Z), 8, 8.25f, 40))]);
                var host = HostDrawingGeometry.Convert(native);
                Vector2 Project(Vector3 v) => new Vector2(v.X - 60, v.Z + 258) * 10;
                var outlines = native.GetContours().Select(p => (IReadOnlyList<Vector2>)p.Select(Project).ToArray()).ToArray();
                var fill = native is PolygonRegionGeometry region
                    ? region.FillPolygons.Select(p => (IReadOnlyList<Vector2>)p.Select(Project).ToArray()).ToArray() : outlines;
                var radar = new RadarClipRegion(outlines[0], Vector2.Zero, 400, outlines, fill);
                for (var x = 30.137f; x < 90; x += .231f)
                    for (var z = -280.173f; z < -235; z += .233f)
                    {
                        var p = new Vector3(x, 0, z);
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(p, 0) == expected && host.Contains(p, 0) == expected, $"Prison geometry {p}: source={expected} native={native.Contains(p, 0)}.");
                        // Radar's existing 0.001-pixel edge tolerance intentionally includes
                        // boundary pixels. At 10 px/y that is at most 0.0001 y.
                        if (radar.Contains(Project(p)) != expected)
                            Check(NearBoundary(native, p, .0002f), $"Radar geometry {p}.");
                    }
                // Every contour segment must separate playable and forbidden space.
                foreach (var contour in native.GetContours())
                    for (var i = 0; i < contour.Count; ++i)
                    {
                        var a = contour[i]; var b = contour[(i + 1) % contour.Count]; var d = b - a;
                        // The source's 89.98-degree notch creates subpixel edges near
                        // a tangent ring; world-float probes cannot resolve both sides.
                        if (d.Length() < .01f) continue;
                        var n = Vector3.Normalize(new(-d.Z, 0, d.X)) * .002f; var p = (a + b) * .5f;
                        Check(native.Contains(p + n, 0) != native.Contains(p - n, 0), "Internal triangulation seam displayed.");
                    }
            });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x488F, 43802, new BossMod.AOEShapeRect(40, 32.5f)),
            (0x4891, 43587, new BossMod.AOEShapeRect(16, 2)),
            (0x4891, 43596, new BossMod.AOEShapeDonut(4, 9)),
            (0x4891, 43592, new BossMod.AOEShapeCircle(5)),
            (0x4891, 43595, new BossMod.AOEShapeCircle(6)),
            (0x48BE, 43810, new BossMod.AOEShapeRect(45, 4)),
        })
            run($"Meso {action} exact source shape packet correction both outputs finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var c = f.Actors[10].Cast! with { Location = f.Actors[10].Position + Vector3.UnitX * 2, Rotation = 1.2f };
                f.Engine.StartCast(10, c, f.Now); f.Render();
                var a = Group(f, "simple").Single(); var geometry = a.Shape.Resolve(a.Position, a.Rotation, null);
                Check(a.Position == c.Location, "Packet correction.");
                for (var x = -45.13f; x < 46; x += .91f)
                    for (var z = -45.27f; z < 46; z += .97f)
                    {
                        var p = a.Position + new Vector3(x, 0, z);
                        Check(geometry.Contains(p, 0) == source.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)), "Shape.");
                    }
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Dual output.");
                f.Now = f.Now.AddMinutes(1); f.Engine.Tick(f.Now); Check(Group(f, "simple").Length == 1, "Timed consumption.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Finish.");
            });
        run("Meso map shrink not cast finish and exactly first four sphere risk", () =>
        {
            using var f = Prepare(0x488F); var p = f.Engine.Arena!.Position;
            Cast(f, 1, 43798); Check(Near(Group(f, "shrink").Single().Activation, f.Now, 5.8), "Shrink clock.");
            Finish(f, 1); Check(f.Engine.Arena.Contains(p + Vector3.UnitX * 21, 0), "Premature shrink.");
            f.Engine.MapEffect(4, 0x00020001);
            Check(!f.Engine.Arena!.Contains(p + Vector3.UnitX * 21, 0) && Group(f, "shrink").Length == 0, "Map shrink.");
            foreach (var index in new uint[] { 0x0B, 0x10, 0x0D, 0x12, 0x0F }) f.Engine.MapEffect(index, 0x00020001);
            Check(Group(f, "sphere").Length == 5 && Group(f, "sphere").All(a => !a.Navigate), "Early spheres.");
            Cast(f, 10, 43805); Check(Group(f, "sphere").All(a => !a.Navigate), "Big activated spheres.");
            Finish(f, 10); Check(Group(f, "sphere").Length == 5, "Big consumed spheres.");
            Cast(f, 11, 43806);
            Check(Group(f, "sphere").Take(4).All(a => a.Navigate && Near(a.Activation, f.Now, 5))
                && !Group(f, "sphere")[4].Navigate, "First four only.");
            Finish(f, 11); Check(Group(f, "sphere").Length == 0, "Small finish clear.");
        });
        run("Meso Pungent knockback navigation excludes circle avoidance but unsafe preview checks circles", () =>
        {
            using var f = Prepare(0x488F); var p = f.Engine.Arena!.Position;
            f.Engine.MapEffect(0x0B, 0x00020001); Cast(f, 10, 43807);
            var kb = (VanaspatiKnockbackTemplate)Group(f, "knockback/avoid").Single().Shape;
            Check(kb.Distance == 24 && kb.Dangers.Length == 0, "Source navigation differs.");
            var target = new Vector3(260, 0, 2);
            var start = p + Vector3.Normalize(target - p);
            Check(!kb.Resolve(p, 0, null).Contains(start, 0), "Valid square landing rejected.");
            f.Actors[100] = f.Actors[100] with { Position = start }; Scan(f);
            Check(Group(f, "knockback/landing").Single().Color == new Vector4(1, .1f, .1f, 1), "Unsafe preview circles.");
        });
        run("Meso personal assignment tethers status fallback exact forbidden set and target priority", () =>
        {
            using var f = Prepare(0x4891); var p = f.Actors[100].Position;
            f.Actors[30] = new(30, 0x48D2, p + Vector3.UnitX * 17.99f);
            f.Actors[31] = new(31, 0x48D2, p + Vector3.UnitX * 18);
            f.Actors[32] = new(32, 0x9999, p);
            Scan(f); f.Engine.Tether(100, 249, true, f.Now, 2);
            Check(f.Engine.ForbiddenTargets.Order().SequenceEqual(new ulong[] { 1, 3, 4, 31, 32 }), "Forbidden all hostile / strict 18.");
            Check(f.Engine.PriorityTarget(1, p) == 30, "Hellmaker priority.");
            f.Engine.Tether(100, 249, false, f.Now, 2); Check(f.Engine.ForbiddenTargets.Length == 5, "249 cancellation cleared.");
            f.Engine.Tether(100, 341, true, f.Now, 2); f.Engine.Tether(100, 341, false, f.Now, 2);
            Check(f.Engine.ForbiddenTargets.Length == 0, "341 cancellation retained.");
            f.Engine.StatusChanged(100, 4543, true, f.Now);
            Check(!f.Engine.ForbiddenTargets.Contains(1UL) && f.Engine.ForbiddenTargets.Contains(2UL), "Beta original primary.");
            f.Engine.StatusChanged(100, 4543, false, f.Now); Check(f.Engine.ForbiddenTargets.Contains(2UL), "Lose fallback cleared.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.ForbiddenTargets.Length == 0, "Perspective leaked assignment.");
            f.PlayerId = 100; Scan(f); Check(f.Engine.ForbiddenTargets.Contains(2UL), "Perspective lost assignment.");
            f.Engine.StatusChanged(100, 4550, true, f.Now); Check(f.Engine.ForbiddenTargets.Length == 0, "Fugitive clear.");
        });
        run("Meso prison snapshot event acknowledgement current-position loss and personal map visibility", () =>
        {
            using var f = Prepare(0x4891);
            f.Actors[1] = f.Actors[1] with { Statuses = [2552], StatusParams = new Dictionary<uint, ushort> { [2552] = 0x394 } };
            Scan(f); f.Engine.StatusChanged(1, 2552, true, f.Now, 0x394);
            f.Engine.MapEffect(0x19, 0x00020001);
            Check(Group(f, "hellmaker").Length == 1, "Unassigned square hidden.");
            f.Engine.Tether(100, 249, true, f.Now, 1);
            Check(Group(f, "hellmaker").Length == 0 && f.Engine.Arena!.GetContours().Count == 3, "Assigned prison / duplicate snapshot.");
            var prison = f.Actors[1].Position;
            Check(f.Engine.Arena.Contains(prison, 0) && !f.Engine.Arena.Contains(prison + Vector3.UnitX * 8.1f, 0), "Ring interior.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.Arena!.GetContours().Count == 1 && Group(f, "hellmaker").Length == 1, "Unassigned view.");
            f.PlayerId = 100; Scan(f);
            f.Actors[1] = f.Actors[1] with { Position = prison + Vector3.UnitX };
            Scan(f); f.Engine.StatusChanged(1, 2552, false, f.Now);
            Check(f.Engine.Arena!.GetContours().Count == 3, "Loss used original rather than current position.");
        });
        run("Meso headsmen group survives original primary death and ends on all deaths", () =>
        {
            using var f = Prepare(0x4891);
            f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
            Check(f.Engine.Active != null && f.Engine.PrimaryId == 1, "Primary death ended group.");
            f.Engine.StatusChanged(100, 4543, true, f.Now); Check(f.Engine.ForbiddenTargets.Contains(2UL), "Beta replaced.");
            foreach (var id in new ulong[] { 2, 3, 4 }) f.Actors[id] = f.Actors[id] with { Dead = true, InCombat = false };
            Scan(f); Check(f.Engine.Active == null, "All dead did not finish.");
        });
        run("Meso removed prison owner emits status loss before disappearance with late event idempotence", () =>
        {
            using var f = Prepare(0x4891);
            f.Actors[30] = new(30, 0x233C, f.Engine.Arena!.Position, Statuses: [2552],
                StatusParams: new Dictionary<uint, ushort> { [2552] = 0x394 });
            Scan(f); f.Engine.Tether(100, 249, true, f.Now, 1);
            Check(f.Engine.Arena!.GetContours().Count == 3, "Prison missing.");
            f.Actors.Remove(30); Scan(f);
            Check(f.Engine.Arena!.GetContours().Count == 1, "Destroyed prison retained.");
            f.Engine.RemoveActor(30); Check(f.Engine.Arena.GetContours().Count == 1, "Late remove duplicated.");
            f.Engine.StatusChanged(30, 2552, true, f.Now.AddSeconds(1), 0x394);
            Scan(f); Check(f.Engine.Arena!.GetContours().Count == 1, "Late gain resurrected destroyed prison.");
        });
        run("Meso chains direct replacement releases previous player's assignment", () =>
        {
            using var f = Prepare(0x4891); f.Engine.Tether(100, 341, true, f.Now, 2);
            Check(f.Engine.ForbiddenTargets.Contains(1UL), "Initial assignment.");
            f.Engine.Tether(101, 341, true, f.Now, 2);
            Check(f.Engine.ForbiddenTargets.Length == 0, "Previous assignment retained.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.ForbiddenTargets.Contains(1UL), "Replacement assignment lost.");
        });
        run("Meso unacknowledged chains source removal clears assignment and never resurrects pending tether", () =>
        {
            using var f = Prepare(0x4891); f.Engine.Tether(100, 341, true, f.Now, 2);
            Check(f.Engine.ForbiddenTargets.Contains(1UL), "Not assigned.");
            f.Actors.Remove(2); Scan(f);
            Check(f.Engine.ForbiddenTargets.Length == 0, "Removed chains retained restriction.");
            Scan(f); f.Engine.RemoveActor(2); Check(f.Engine.ForbiddenTargets.Length == 0, "Pending resurrected.");
        });
        run("Meso sword snapshot disappears before remove consumes matching line once", () =>
        {
            using var f = Prepare(0x4891); f.Actors[20] = new(20, 0x4894, f.Engine.Arena!.Position); Scan(f);
            Check(Group(f, "sword").Length == 12, "Sword missing.");
            f.Actors.Remove(20); Scan(f); Check(Group(f, "sword").Length == 0, "Missing sword retained.");
            f.Engine.RemoveActor(20); f.Engine.Tick(f.Now); Check(Group(f, "sword").Length == 0, "Late remove restored.");
        });
        run("Meso continuous group strict unsorted prefix and caster FIFO finish", () =>
        {
            using var f = Prepare(0x4891);
            Cast(f, 10, 43587, 5); Cast(f, 11, 43592, 7); Cast(f, 12, 43596, 4);
            Check(Group(f, "simple").Length == 1, "Strict unsorted prefix.");
            Finish(f, 10); Check(Group(f, "simple").Length == 2, "FIFO release.");
        });
        run("Meso three hit buster retains original target after finish and clears only third unique effect", () =>
        {
            using var f = Prepare(0x4891); f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            Cast(f, 1, 43589, target: 1); Finish(f, 1);
            Check(f.Engine.PredictedDamage.Single(d => d.DamageType == "Tankbuster").TargetId == 100, "Buster delayed target.");
            f.Engine.Effect(10, 43590, 1, f.Now); f.Engine.Effect(10, 43590, 1, f.Now);
            f.Engine.Effect(10, 43590, 2, f.Now); Check(f.Engine.PredictedDamage.Any(d => d.DamageType == "Tankbuster"), "Cleared before third.");
            f.Engine.Effect(10, 43590, 3, f.Now); Check(!f.Engine.PredictedDamage.Any(d => d.DamageType == "Tankbuster"), "Third retained.");
        });
        run("Meso sword creation clock 51 hits first step second rotation actual targets cache and obstacle", () =>
        {
            using var f = Prepare(0x4891); var p = f.Engine.Arena!.Position;
            f.Engine.ObjectCreated(20, f.Now.AddSeconds(-1)); f.Actors[20] = new(20, 0x4894, p); Scan(f);
            var aoes = Group(f, "sword"); Check(aoes.Length == 12 && Near(aoes[^1].Activation, f.Now, 4.5), "Create clock/current order.");
            Check(Group(f, "obstacle").Single().Obstacle, "Sword obstacle.");
            var time = aoes[0].Activation; f.Now = f.Now.AddSeconds(10); f.Engine.Tick(f.Now);
            Check(Group(f, "sword")[0].Activation == time, "Tick shifted cache.");
            f.Engine.Effect(20, 43594, 1, f.Now, target: 999, hasTargets: false, sourcePosition: p);
            f.Engine.Tick(f.Now);
            Check(Vector3.Distance(Group(f, "sword")[^1].Position, Q(p + Vector3.UnitZ * .24118f)) < .0001, "First advance.");
            var next = p + Vector3.UnitZ * .24118f;
            f.Engine.Effect(20, 43594, 2, f.Now, sourceRotation: MathF.PI / 2, hasTargets: false, sourcePosition: next);
            f.Engine.Tick(f.Now);
            Check(Vector3.Distance(Group(f, "sword")[^1].Position, Q(next + Vector3.UnitX * .3222f)) < .0001, "Second advance current rotation.");
            f.Engine.Effect(20, 43594, 3, f.Now, hasTargets: true, sourcePosition: next + Vector3.UnitX * .3222f);
            f.Engine.Tick(f.Now); Check(Group(f, "sword").Length == 0, "Actual targets didn't stop sword.");
        });
        run("Meso sword targeted packet TargetIndex controls termination even TargetId zero", () =>
        {
            using var f = Prepare(0x4891); f.Actors[20] = new(20, 0x4894, f.Engine.Arena!.Position); Scan(f);
            f.Events.Handle(new LogSystemActionEffectEvent { SourceId = 20, ActionId = 43594, GlobalSequence = 1, TargetId = 0, TargetIndex = 0, Timestamp = f.Now }, f.Now);
            f.Engine.Tick(f.Now); Check(Group(f, "sword").Length == 0, "TargetIndex ignored.");
        });
        run("Meso sword death directional strict middle band and destruction current next matching", () =>
        {
            using var f = Prepare(0x4891); var p = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x4894, p, Rotation: MathF.PI / 2);
            f.Actors[21] = new(21, 0x4894, p);
            Scan(f); f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
            Check(Group(f, "sword").Length == 12 && Group(f, "sword").All(a => MathF.Abs(a.Rotation) < .01), "Death direction.");
            f.Engine.RemoveActor(21); f.Engine.Tick(f.Now); Check(Group(f, "sword").Length == 0, "Destroy next match.");
        });
        run("Meso terror timeline duplicates nonmatching timeline removes first and yell uses shared clock rounded degrees", () =>
        {
            using var f = Prepare(0x48BE); Terrors(f);
            f.Actors[40] = new(40, 0x9999, new(1, 0, 1), Rotation: 1.4f); Scan(f);
            var start = f.Now; f.Engine.NpcYell(40, 18705, start);
            var a = Group(f, "bomb").Single();
            var angle = MathF.Round(1.4f * 180 / MathF.PI) * MathF.PI / 180;
            Check(((CircleTemplate)a.Shape).Radius == 14 && a.Position == Q(new(1 + 3.5f * MathF.Sin(angle), 0, 1 + 3.5f * MathF.Cos(angle)))
                && Near(a.Activation, start, 9.8), "Yell source / radians / clock.");
            f.Engine.NpcYell(26, 18708, start.AddSeconds(2));
            Check(Group(f, "bomb").Length == 2 && Group(f, "bomb").All(b => b.Activation == a.Activation), "Shared activation.");
            Cast(f, 10, 43812); Finish(f, 10); Check(Group(f, "bomb").Length == 2, "Big cleared.");
            Cast(f, 11, 43811); Finish(f, 11); Check(Group(f, "bomb").Length == 0, "Small finish.");
        });
        run("Meso Impression exact eight risk toggling preserves display and source action 43818", () =>
        {
            using var f = Prepare(0x48BE); Terrors(f);
            for (var i = 0; i < 7; ++i) f.Engine.NpcYell(20, 18705, f.Now);
            Cast(f, 10, 43819); Check(Group(f, "knockback").Length == 0, "Wrong knockback AID."); Finish(f, 10);
            Cast(f, 11, 43818); f.Engine.Tick(f.Now);
            Check(Group(f, "bomb").All(a => a.Navigate), "Seven changed risk.");
            f.Engine.NpcYell(20, 18705, f.Now); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "bomb").All(a => !a.Navigate) && f.Draw.Previews.Count >= 9, "Eight risk hid drawings.");
            var avoid = (VanaspatiKnockbackTemplate)Group(f, "knockback/avoid").Single().Shape;
            Check(avoid.Distance == 11 && avoid.Dangers.All(a => a is CircleGeometry { Radius: 4 }), "KB inflated circles.");
            Check(Group(f, "knockback/center").Length == 1, "Center start exclusion.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; Scan(f);
            Check(Group(f, "knockback/avoid").Length == 0 && Group(f, "knockback/center").Length == 1, "Immune center exclusion.");
            Finish(f, 11); f.Engine.Tick(f.Now); Check(Group(f, "bomb").All(a => a.Navigate), "Risk restoration.");
        });
        run("Meso Turmoil packet origin and Keraunography visual finish plus three future until actual finish", () =>
        {
            using var f = Prepare(0x48BE);
            f.Engine.Effect(1, 43814, 1, f.Now, occurredAt: f.Now.AddSeconds(-1), sourcePosition: new Vector3(2, 0, 3));
            var a = Group(f, "turmoil").Single();
            Check(a.Position == Q(new(-8, 0, 3)) && Near(a.Activation, f.Now, 3.4), "Turmoil origin/time.");
            f.Engine.Effect(1, 43816, 2, f.Now); Check(Group(f, "turmoil").Length == 0, "Turmoil finish.");
            Cast(f, 10, 43813, 4); Check(Near(Group(f, "keraunos").Single().Activation, f.Now, 7), "Visual +3.");
            Finish(f, 10); Check(Group(f, "keraunos").Length == 1, "Visual consumed.");
            Cast(f, 11, 45176); Finish(f, 11); Check(Group(f, "keraunos").Length == 0, "Actual finish retained.");
        });
        run("Meso line icon exact half width three source actor dead target retained FIFO", () =>
        {
            using var f = Prepare(0x48BE); f.Engine.Icon(100, 525, f.Now, 1);
            var a = Group(f, "line").First(a => a.Display);
            Check(a.Shape is RayRectangleTemplate { Length: 60, MinLocalX: -3, MaxLocalX: 3 }
                && Near(a.Activation, f.Now, 5.2), "Line shape/clock.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(Group(f, "line").Length > 0, "Dead target discarded.");
            f.Engine.Effect(99, 43822, 1, f.Now); Check(Group(f, "line").Length == 0, "Effect FIFO.");
        });
        run("Meso poison doom and interrupt queries obey roles range and status", () =>
        {
            using var f = Prepare(0x488F); f.Engine.StatusChanged(101, 2104, true, f.Now);
            Check(f.Engine.MesoCleanseTarget(24)?.Id == 101 && f.Engine.MesoCleanseTarget(19) == null, "Poison query.");
            using var g = Prepare(0x4891); g.Engine.StatusChanged(101, 4594, true, g.Now);
            Check(g.Engine.MesoCleanseTarget(40)?.Id == 101, "Doom query.");
            Cast(g, 1, 44856, target: 100); Check(g.Engine.MesoInterruptTarget(19)?.Id == 1, "Interrupt query.");
            g.Actors[100] = g.Actors[100] with { Position = g.Actors[1].Position + Vector3.UnitX * 30 }; Scan(g);
            Check(g.Engine.MesoInterruptTarget(23) == null, "Interrupt range.");
        });
        run("Meso native temporal solver avoids continuous rectangle inside arena", () =>
        {
            using var f = Prepare(0x48BE); Cast(f, 10, 43810, 7, 0);
            var p = f.Engine.Arena!.Position + Vector3.UnitZ * 3;
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot, new(p, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback, "No solution.");
            var route = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(route.Count >= 2 && route.All(n => f.Engine.Arena.Contains(n.Position))
                && !Group(f, "simple").Single().Shape.Resolve(f.Actors[10].Position, 0, null).Contains(route[^1].Position, 0), "Unsafe route.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1292); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true, Name: "Primary");
        if (boss == 0x4891)
            for (var i = 2; i < 5; ++i) f.Actors[(ulong)i] = new((ulong)i, i == 2 ? 0x4890u : i == 3 ? 0x4892u : 0x4893u,
                c + Vector3.UnitX * ((i - 2) * 5 + 2), InCombat: true);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i * 3,
            PartySlot: i, Hostile: false);
        Scan(f); return f;
    }
    private static void Terrors(Fight f)
    {
        for (ulong id = 20; id < 27; ++id) f.Actors[id] = new(id, 0x48C0, new((id - 20) * 8, 0, 0));
        Scan(f); foreach (var id in Enumerable.Range(20, 7)) f.Engine.Timeline((ulong)id, 0x11D3, f.Now);
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double duration = 5, float rotation = .7f, ulong target = 0)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        var c = new DungeonCastObservation(action, f.Actors[id].Position, rotation, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[id] = f.Actors[id] with { Cast = c }; Scan(f); f.Engine.StartCast(id, c, f.Now); f.Engine.Tick(f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id) { f.Actors[id] = f.Actors[id] with { Cast = null }; f.Engine.Finish(id, endedAt: f.Now); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("meso/" + key)).ToArray();
    private static Vector3 Q(Vector3 p) { var q = new BossMod.WPos(p.X, p.Z).Quantized(); return new(q.X, p.Y, q.Z); }
    private static bool NearBoundary(IGeometryShape geometry, Vector3 p, float tolerance)
    {
        foreach (var contour in geometry.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = contour[i]; var d = contour[(i + 1) % contour.Count] - a;
                var t = Math.Clamp(Vector3.Dot(p - a, d) / d.LengthSquared(), 0, 1);
                if (Vector3.DistanceSquared(p, a + t * d) <= tolerance * tolerance) return true;
            }
        return false;
    }
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } d && Math.Abs((d - start).TotalSeconds - seconds) < .00001;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
