using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class LunarRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Lunar ownership three exact arenas and no invented shield", () =>
        {
            Check(ArrDungeonCatalog.Create(1164).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.lunar"), "Discovery.");
            foreach (var boss in new uint[] { 0x3FE2, 0x4022, 0x4042 })
            {
                using var f = Prepare(boss);
                Check(ArrDungeonCatalog.Owns(boss, 1164) && !ArrDungeonCatalog.Owns(boss, 1126), "Ownership scope.");
                Check(!f.Engine.ForbiddenTargets.Any(), "Invented shield target ban.");
                var center = f.Engine.Arena!.Position;
                Check(f.Engine.Arena.Contains(center, 0) && !f.Engine.Arena.Contains(center + Vector3.UnitX * 24, 0), "Arena bounds.");
            }
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x3FE2, 34787, new BossMod.AOEShapeRect(8, 4)),
            (0x4022, 34443, new BossMod.AOEShapeCircle(12)),
            (0x4042, 35006, new BossMod.AOEShapeCircle(11)),
            (0x4042, 35005, new BossMod.AOEShapeCircle(9)),
            (0x4042, 35009, new BossMod.AOEShapeCone(50, new(MathF.PI / 4))),
            (0x4042, 36056, new BossMod.AOEShapeRect(60, 10)),
            (0x4042, 35002, new BossMod.AOEShapeCircle(25)),
            (0x4042, 34995, new BossMod.AOEShapeCircle(8)),
            (0x4042, 34996, new BossMod.AOEShapeCone(30, new(MathF.PI / 12))),
        })
            run($"Lunar {action} source shape both views real end survives clock", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                Cast(f, 10, action);
                var cast = f.Engine.OutputCasts.Single().Value;
                var native = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -51.137f; x < 51; x += 1.17f)
                    for (var z = -51.231f; z < 51; z += 1.17f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = shape.Check(new(p.X, p.Z), new(c.X, c.Z), new(cast.Rotation));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Shape parity.");
                    }
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Timeout erased real cast.");
                Finish(f, 10); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Finish leaked.");
            });
        foreach (var (count, delay) in new (int, double)[] { (0, 8.1), (1, 25.9), (2, 32), (3, 32) })
            run($"Lunar staff confluence {count} model transition delay {delay} strict risk hidden explosion and real clear", () =>
            {
                using var f = Prepare(0x3FE2);
                for (var i = 0; i < count; ++i) { f.Now = f.Now.AddSeconds(1); Cast(f, 1, 35205); Finish(f, 1); }
                f.Actors[11] = new(11, 0x3FE3, f.Engine.Arena!.Position, AnimationState2: 1); Scan(f);
                var cross = Aoes(f, "cross").Single();
                Check(cross.Activation == f.Now.AddSeconds(delay) && !cross.Navigate && !cross.OutlineOnly, "Timing/display.");
                Scan(f); Check(Aoes(f, "cross").Length == 1, "Unchanged model repeated.");
                f.Now = cross.Activation!.Value.AddSeconds(-5); Scan(f);
                Check(!Aoes(f, "cross").Single().Navigate && f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Strict boundary.");
                f.Now = f.Now.AddTicks(1); Scan(f); Check(Aoes(f, "cross").Single().Navigate, "Risk not activated.");
                Cast(f, 10, 34787); Check(Aoes(f, "cross").Length == 0 && f.Draw.Previews.Count == 1, "Explosion did not hide group.");
                Finish(f, 10); Check(Aoes(f, "cross").Length == 1, "Hidden cache was cleared.");
                Cast(f, 11, 34783); Finish(f, 11); Check(Aoes(f, "cross").Length == 1, "Visual cleared cross.");
                Cast(f, 11, 35254); f.Engine.RemoveActor(11, f.Now); f.Actors.Remove(11); f.Render();
                Check(Aoes(f, "cross").Length == 0, "Destroyed real hex caster did not clear.");
            });
        foreach (var (boss, action, type) in new (uint, uint, string)[]
        {
            (0x3FE2, 35204, "Tankbuster"), (0x3FE2, 34782, "Raidwide"),
            (0x4042, 35010, "Raidwide"), (0x4042, 35011, "Raidwide"), (0x4042, 35905, "Raidwide"),
        })
            run($"Lunar {action} source damage classification {type} real finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 1, action, 101);
                var damage = f.Engine.PredictedDamage.ToArray();
                Check(damage.Length == (type == "Tankbuster" ? 1 : 2) && damage.All(p => p.DamageType == type), "Damage source classification.");
                Finish(f, 1); Check(!f.Engine.PredictedDamage.Any(), "Damage finish leaked.");
            });
        run("Lunar initial model baseline does not invent transition and changed model reappends", () =>
        {
            using var f = Prepare(0x3FE2); f.Engine.ResetFight();
            f.Actors[11] = new(11, 0x3FE3, f.Actors[1].Position, AnimationState2: 1); Scan(f);
            Check(Aoes(f, "cross").Length == 0, "Activation replayed model event.");
            f.Actors[11] = f.Actors[11] with { ModelState = 1 }; Scan(f);
            Check(Aoes(f, "cross").Length == 1, "Model change with anim2=1 lost.");
            f.Actors[11] = f.Actors[11] with { AnimationState2 = 0 }; Scan(f);
            f.Actors[11] = f.Actors[11] with { AnimationState2 = 1 }; Scan(f);
            Check(Aoes(f, "cross").Length == 2, "Second transition was deduplicated.");
        });
        foreach (var boss in new uint[] { 0x4022, 0x4042 })
            run($"Lunar {boss:X} arena forecast retained until exact map confirmed both views", () =>
            {
                using var f = Prepare(boss); var center = f.Engine.Arena!.Position;
                Cast(f, 1, boss == 0x4022 ? 34813u : 35011u);
                Check(Aoes(f, "shrink").Length == 1 && Aoes(f, "shrink").Single().Activation == f.Now.AddSeconds(5), "Missing shrink.");
                Finish(f, 1); Check(Aoes(f, "shrink").Length == 1, "Finish cleared forecast.");
                f.Engine.MapEffect(boss == 0x4022 ? 0u : 10u, 0x00080004, f.Now); f.Render();
                Check(Aoes(f, "shrink").Length == 1, "Wrong map confirmed.");
                f.Engine.MapEffect(boss == 0x4022 ? 0u : 10u, 0x00020001, f.Now); f.Render();
                Check(Aoes(f, "shrink").Length == 0 && !f.Engine.Arena!.Contains(center + Vector3.UnitZ * 21, 0), "Map bounds.");
            });
        foreach (var (index1, index2, enter) in new (uint, uint, bool)[] { (1, 2, true), (1, 3, false), (1, 4, true), (1, 5, false), (2, 7, true) })
            run($"Lunar two towers {index1}/{index2} exact combination navigation ignores risk immunity", () =>
            {
                using var f = Prepare(0x4022);
                f.Engine.MapEffect(index1, 0x00020001, f.Now); f.Engine.MapEffect(index2, 0x00020001, f.Now); f.Render();
                Check(Aoes(f, "tower").Count(a => a.Display) == 2 && Aoes(f, "tower").All(a => !a.Navigate), "Initial towers risky.");
                var towers = Aoes(f, "tower").Where(a => a.Display).ToArray();
                Check(towers[0].Activation == f.Now.AddSeconds(13) && towers[1].Activation == f.Now.AddSeconds(12), "Append clocks.");
                Cast(f, 10, 34819, location: new(0, 0, 35), rotation: 0);
                Check(Aoes(f, "tower/join").Length == (enter ? 1 : 0) && Aoes(f, "tower/avoid").Length == (enter ? 0 : 2), "Wrong tower combination.");
                var nav = Aoes(f, enter ? "tower/join" : "tower/avoid").First();
                var world = HostDrawingGeometry.Convert(towers[0].Shape.Resolve(towers[0].Position, towers[0].Rotation, null));
                Check(world.Contains(towers[0].Position + new Vector3(MathF.Sin(towers[0].Rotation), 0, MathF.Cos(towers[0].Rotation)) * 10, 0), "Tower world.");
                Check(nav.Activation == f.Now.AddSeconds(5), "Combination uses tower instead of KB clock.");
                f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
                Check(Aoes(f, "knockback").All(a => !a.Display) && Aoes(f, enter ? "tower/join" : "tower/avoid").Any(),
                    "Immunity erased custom tower navigation or still drew knockback.");
            });
        run("Lunar knockback fifth real finish tower risk all clear and tower effect reset only flag", () =>
        {
            using var f = Prepare(0x4022); f.Engine.MapEffect(1, 0x00020001, f.Now);
            for (var i = 0; i < 5; ++i)
            {
                f.Now = f.Now.AddSeconds(1); Cast(f, 10, 34819);
                if (i == 4) Cast(f, 11, 34819);
                Finish(f, 10);
                Check(!Aoes(f, "knockback").Any() && Aoes(f, "tower").Single().Navigate == (i == 4), "KB finish count/all clear.");
            }
            f.Engine.Effect(1, 34820, 1, f.Now); f.Render(); Check(Aoes(f, "tower").Length == 0, "Tower effect did not clear.");
            f.Now = f.Now.AddSeconds(1); f.Engine.MapEffect(2, 0x00020001, f.Now); Cast(f, 10, 34819); Finish(f, 10);
            Check(Aoes(f, "tower").Single().Navigate, "Tower resolution incorrectly reset cumulative KB count.");
        });
        foreach (var small in new[] { false, true })
            run($"Lunar knockback strip small={small} correct negative back length projection landing unsafe", () =>
            {
                using var f = Prepare(0x4022);
                if (small) f.Engine.MapEffect(0, 0x00020001, f.Now);
                var origin = new Vector3(0, 0, small ? 40 : 35);
                f.Actors[100] = f.Actors[100] with { Position = origin + Vector3.UnitZ * 35 }; Scan(f);
                Cast(f, 10, 34819, location: origin, rotation: 0);
                var a = Aoes(f, "knockback").Single(a => a.Navigate); var shape = a.Shape.Resolve(a.Position, a.Rotation, null);
                var length = small ? 40 : 50;
                Check(!shape.Contains(origin + Vector3.UnitZ * (length - 21), 0) && shape.Contains(origin + Vector3.UnitZ * (length - 10), 0)
                    && !shape.Contains(origin + Vector3.UnitZ * (length + 1), 0), "Negative-back strip translation.");
                Check(f.Engine.Hints(f.Actors[100].Position).Contains("击退落点危险") && Aoes(f, "knockback").Count(a => a.Display) == 2, "Landing prediction.");
            });
        run("Lunar directional knockback preserves packet heading", () =>
        {
            using var f = Prepare(0x4022); var origin = new Vector3(-19.5f, 0, 60);
            Cast(f, 10, 34819, location: origin, rotation: MathF.PI / 2);
            var a = Aoes(f, "knockback").Single(a => a.Navigate);
            Check(MathF.Abs(a.Rotation - MathF.PI / 2) < .00001f && Vector3.Distance(a.Position, origin + Vector3.UnitX * 30) < .00001f,
                "Null-shape cast discarded directional packet heading.");
        });
        run("Lunar quicksand first event state gates whole list including dead objects", () =>
        {
            using var f = Prepare(0x4022); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x1EB90E, c, Dead: true, EventState: 0);
            f.Actors[21] = new(21, 0x1EB90E, c + Vector3.UnitX * 12, EventState: 7); Scan(f);
            Check(Aoes(f, "quicksand").Length == 2 && Aoes(f, "quicksand").All(a => a.Obstacle), "Per-object filter or dead removed.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(Aoes(f, "quicksand").Length == 0, "First gate ignored.");
            f.Engine.RemoveActor(20, f.Now); f.Actors.Remove(20); f.Actors[21] = f.Actors[21] with { EventState = 0 }; Scan(f);
            Check(Aoes(f, "quicksand").Length == 1, "First removal not promoted.");
        });
        run("Lunar quicksand creation order stable across reordered scan inputs", () =>
        {
            using var f = Prepare(0x4022); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x1EB90E, c); Scan(f);
            f.Actors[21] = new(21, 0x1EB90E, c + Vector3.UnitX * 12, EventState: 7);
            f.Engine.Synchronize(f.Actors.Values.Reverse(), f.Actors[100].Position, f.Now, playerId: 100); f.Render();
            Check(Aoes(f, "quicksand").Length == 2, "New first enumerated object replaced creation-order leader.");
        });
        run("Lunar multiple stack candidates choose exactly one disjoint or overlapping actual solver", () =>
        {
            using var f = Prepare(0x4022); var c = f.Engine.Arena!.Position;
            f.Actors[101] = f.Actors[101] with { Position = c - Vector3.UnitX * 6 };
            f.Actors[102] = new(102, 0, c + Vector3.UnitX * 6, PartySlot: 2, Hostile: false); Scan(f);
            Cast(f, 10, 34822, 101); Cast(f, 11, 34822, 102);
            var a = Aoes(f, "stack/join").Single(); var zone = a.Shape.Resolve(a.Position, a.Rotation, null);
            Check(zone.Contains(c, 0) && !zone.Contains(c - Vector3.UnitX * 12, 0), "Overlapping stacks allowed or single candidate blocked.");
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot, new(c, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6),
                new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 0 && !zone.Contains(path[^1].Position, 0),
                "Actual solver failed to leave overlapping candidates.");
        });
        run("Lunar marches snapshot width nine delayed real effect FIFO persistent first color", () =>
        {
            using var f = Prepare(0x4022); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 35871, location: c + Vector3.UnitZ * 20); Cast(f, 11, 35871, location: c - Vector3.UnitZ * 20);
            var a = Aoes(f, "march");
            Check(a.Length == 2 && a[0].Color == new Vector4(1, 1, 0, 1) && a.All(x => x.Navigate && !x.OutlineOnly)
                && a[0].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -4.5f, MaxLocalX: 4.5f }
                && Math.Abs((a[0].Activation!.Value - f.Now).TotalSeconds - 9.2) < .000001,
                $"March source geometry/timing: {a.Length}, {a[0].Shape}, {a[0].Color}, {(a[0].Activation - f.Now)?.TotalSeconds}.");
            Finish(f, 10); Finish(f, 11); Check(Aoes(f, "march").Length == 2, "Telegraph finish consumed march.");
            f.Engine.Effect(1, 34817, 1, f.Now); f.Render(); Check(Aoes(f, "march").Length == 1, "March effect not FIFO.");
            f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now); f.Render(); Check(Aoes(f, "march").Length == 1, "Clock consumed march.");
        });
        run("Lunar battery event clock all displayed first sixteen risk FIFO append persistent color", () =>
        {
            using var f = Prepare(0x4042); var c = f.Engine.Arena!.Position;
            for (ulong id = 20; id < 40; ++id) Cast(f, id, 36058, location: c + Vector3.UnitX * (id - 20), start: f.Now.AddSeconds(-2));
            var a = Aoes(f, "battery");
            Check(a.Length == 20 && a.Count(x => x.Navigate) == 16 && f.Draw.Previews.Count == 20 && f.Snapshot.DangerAreas.Count == 16
                && a.All(x => x.Activation == f.Now.AddSeconds(6.5)), "Battery output or packet/event clock.");
            Check(a.Take(16).All(x => x.Color == new Vector4(1, 1, 0, 1)) && a.Skip(16).All(x => x.Color == null), "Batch colors.");
            for (var i = 0; i < 5; ++i) f.Engine.Effect(1, 34994, (uint)i + 1, f.Now);
            f.Render(); a = Aoes(f, "battery");
            Check(a.Length == 15 && a.All(x => x.Navigate) && a.Take(11).All(x => x.Color != null) && a.Skip(11).All(x => x.Color == null), "Colors not persistent/next batch not promoted.");
            f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now); f.Render(); Check(Aoes(f, "battery").Length == 15, "Battery time-cleared.");
        });
        run("Lunar event line dynamic source target disappearance death FIFO shared damage perspective", () =>
        {
            using var f = Prepare(0x4042); var c = f.Engine.Arena!.Position;
            f.Engine.Effect(10, 14588, 1, f.Now, 101); f.Render();
            Check(Aoes(f, "line").Count(a => a.Display) == 1 && Aoes(f, "line/join").Length == 1
                && f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 2, "Event line/Shared prediction.");
            f.Actors[10] = f.Actors[10] with { Position = c - Vector3.UnitZ * 10 };
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 10, Dead = true }; Scan(f);
            var a = Aoes(f, "line").Single(x => x.Display);
            Check(a.Position == f.Actors[10].Position && MathF.Abs(a.Rotation - MathF.PI / 4) < .0001f, "Dynamic line or dead target excluded.");
            f.Engine.RemoveActor(10, f.Now); f.Actors.Remove(10); Scan(f);
            Check(Aoes(f, "line").Count(x => x.Display) == 1, "Source destroy lost retained reference.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.PlayerId = 101; Scan(f);
            Check(Aoes(f, "line").Count(a => a.Id.EndsWith("/gather")) == 1 && Aoes(f, "line/join").Length == 0, "Marked perspective.");
            f.Now = f.Now.AddSeconds(1); f.Engine.Effect(11, 14588, 2, f.Now, 100); f.Render();
            Check(Aoes(f, "line/").Count(x => x.Display && !x.Safe) == 1 && Aoes(f, "line").Any(x => x.Id.EndsWith("/avoid")), "Other marker not excluded.");
            Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 2
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(5.4)), "Two lines duplicated Shared batch or lost earliest clock.");
            f.Engine.Effect(1, 35008, 3, f.Now, 100); f.Render();
            Check(Aoes(f, "line").Single(x => x.Display).Position == f.Actors[11].Position, "Resolve matched target instead of FIFO.");
            Check(f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(6.4)), "Shared did not promote next line clock.");
            f.Actors[11] = f.Actors[11] with { Dead = true }; Scan(f);
            Check(!Aoes(f, "line").Any(), "Dead source line active.");
        });
        foreach (var (boss, action) in new (uint, uint)[] { (0x3FE2, 34788), (0x4042, 35882) })
            run($"Lunar {action} spread radius seven movement six outline perspective", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action, 101);
                Check(Aoes(f, "spread").Single(a => a.Display) is { Shape: CircleTemplate { Radius: 6 }, OutlineOnly: true }
                    && Aoes(f, "spread").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 7 }, "Spread radii.");
                f.PlayerId = 101; Scan(f);
                Check(Vector3.Distance(Aoes(f, "spread").Single(a => a.Navigate).Position, f.Actors[100].Position) < .05f, "Personal spread.");
                Finish(f, 10); Check(!Aoes(f, "spread").Any(), "Real spread finish.");
            });
        run("Lunar hard stack actual solver capacity and multiple markers", () =>
        {
            using var f = Prepare(0x4022); var c = f.Engine.Arena!.Position;
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 14 }; Scan(f); Cast(f, 10, 34822, 101);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(c, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6),
                new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0
                && Vector3.Distance(path[^1].Position, f.Actors[101].Position) <= 10, "Stack movement unsolved.");
            Cast(f, 11, 34822, 100); f.PlayerId = 100; Scan(f);
            Check(Aoes(f, "stack").Count(a => a.Display) == 2 && Aoes(f, "stack").Count(a => a.Display && a.Safe) == 1
                && Aoes(f, "stack").Any(a => a.Shape is CircleTemplate { Radius: 20 } && !a.Display), "Multiple stack marker overlap.");
        });
        run("Lunar Doom status event stale snapshot class gates loss and reset", () =>
        {
            using var f = Prepare(0x3FE2); f.Engine.StatusChanged(101, 3364, true, f.Now);
            Scan(f); Check(f.Engine.LunarCleanseTarget(24)?.Id == 101 && f.Engine.LunarCleanseTarget(19) == null, "Doom event/class.");
            f.Actors[101] = f.Actors[101] with { Statuses = [3364] }; Scan(f);
            f.Engine.StatusChanged(101, 3364, false, f.Now);
            f.Actors[101] = f.Actors[101] with { Statuses = [] }; Scan(f);
            Check(f.Engine.LunarCleanseTarget(24) == null, "Doom loss.");
            f.Engine.ResetFight(); f.Render(); Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0, "Reset leaked.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1164); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, Vector3? location = null, float rotation = .7f, DateTime? start = null)
    {
        if (!f.Actors.ContainsKey(source)) f.Actors[source] = new(source, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, start ?? f.Now, f.Now.AddSeconds(5), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; Scan(f); f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong source)
    {
        f.Actors[source] = f.Actors[source] with { Cast = null }; f.Engine.Finish(source, endedAt: f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Aoes(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("lunar/" + key)).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
