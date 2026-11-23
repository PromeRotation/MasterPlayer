using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class LapisRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Lapis four modules ownership exact source arenas both views", () =>
        {
            var definitions = ArrDungeonCatalog.Create(1097);
            Check(definitions.Length == 4 && definitions.All(d => ArrDungeonCatalog.Owns(d.BossOid, 1097)
                && !ArrDungeonCatalog.Owns(d.BossOid, 1070)) && ArrDungeonCatalog.IsScript("masterplayer.dungeon.lapis"), "Ownership.");
            var type = typeof(BossMod.Endwalker.Dungeon.D11LapisManalis.D112GalateaMagna.D112GalateaMagna);
            var custom = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            foreach (var definition in definitions)
            {
                var native = definition.Arena(Vector3.Zero); var c = native.Position;
                for (var x = -31.137f; x < 31; x += .41f)
                    for (var z = -31.231f; z < 31; z += .41f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = definition.BossOid switch
                        {
                            0x3D56 => MathF.Abs(x) <= 8.5f && MathF.Abs(z) <= 11.5f,
                            0x3971 => custom.Contains(new(p.X - custom.Center.X, p.Z - custom.Center.Z)),
                            _ => MathF.Max(MathF.Abs(x), MathF.Abs(z)) <= (definition.BossOid == 0x3AE2 ? 29.5f : 19.5f),
                        };
                        Check(native.Contains(p, 0) == expected && HostDrawingGeometry.Convert(native).Contains(p, 0) == expected, "Arena differs.");
                    }
            }
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x3D56, 32535, new BossMod.AOEShapeCone(9, new(MathF.PI / 4))),
            (0x3D56, 32786, new BossMod.AOEShapeCircle(8)),
            (0x3D56, 32787, new BossMod.AOEShapeCone(8, new(MathF.PI / 4))),
            (0x3CFE, 32813, new BossMod.AOEShapeRect(80, 10)),
            (0x3CFE, 32814, new BossMod.AOEShapeRect(80, 10)),
            (0x3CFE, 31359, new BossMod.AOEShapeCircle(5)),
            (0x3CFE, 31361, new BossMod.AOEShapeCircle(17)),
            (0x3CFE, 32783, new BossMod.AOEShapeCircle(6)),
            (0x3CFE, 32697, new BossMod.AOEShapeCircle(6)),
            (0x3971, 31386, new BossMod.AOEShapeCircle(18)),
            (0x3AE2, 31120, new BossMod.AOEShapeCircle(15)),
            (0x3AE2, 31123, new BossMod.AOEShapeCircle(8)),
            (0x3AE2, 31136, new BossMod.AOEShapeCircle(6)),
            (0x3AE2, 32691, new BossMod.AOEShapeCone(50, new(MathF.PI / 12))),
            (0x3AE2, 31113, new BossMod.AOEShapeCone(50, new(MathF.PI / 12))),
        })
            run($"Lapis {action} source shape radar world and observed ending", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                if (action is 32786 or 32787) Griffin(f);
                f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, action);
                var cast = f.Engine.Casts[10]; var native = cast.Rule.Shape!.Resolve(c, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -51.137f; x < 51; x += .91f)
                    for (var z = -51.231f; z < 51; z += .91f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = source.Check(new(p.X, p.Z), new(c.X, c.Z), new(cast.Rotation));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Shape differs.");
                    }
                Check(f.Draw.Previews.Count > 0 && f.Snapshot.DangerAreas.Count > 0, "Missing outputs.");
                Finish(f, 10); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Ending leaked.");
            });
        run("Lapis carriage phase transition clears first component retains dead actors until destruction", () =>
        {
            using var f = Prepare(0x3D56); var c = f.Engine.Arena!.Position;
            f.Actors[2] = new(2, 0x3CE2, c, InCombat: true);
            f.Actors[3] = new(3, 0x3E9F, c, InCombat: true); Scan(f);
            Cast(f, 2, 32535); Cast(f, 3, 32786);
            Check(f.Engine.Casts.ContainsKey(2) && !f.Engine.Casts.ContainsKey(3), "Second stage active early.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
            Check(f.Engine.Active != null && f.Engine.Casts.ContainsKey(2), "One first-stage death ended phase.");
            f.Actors[2] = f.Actors[2] with { Dead = true }; Scan(f);
            Check(f.Engine.Active != null && !f.Engine.Casts.ContainsKey(2) && f.Engine.Casts.ContainsKey(3), "Phase did not replace components.");
            f.Actors[3] = f.Actors[3] with { Dead = true }; Scan(f);
            Check(f.Engine.Active != null, "Dead first-stage bodies treated as destroyed.");
            f.Actors.Remove(1); f.Actors.Remove(2); Scan(f);
            Check(f.Engine.Active == null, "Destroyed first stage and dead griffin did not finish.");
        });
        run("Lapis WindsOfWinter stun role range damage and cancellation", () =>
        {
            using var f = Prepare(0x3D56); Griffin(f); Cast(f, 3, 32785);
            Check(LapisCatalog.StunAction(19) == 7540 && LapisCatalog.StunAction(41) == 7863 && LapisCatalog.StunAction(23) == 0,
                "Client action categories differ.");
            Check(f.Engine.LapisStunTarget(19)?.Id == 3 && f.Engine.LapisStunTarget(24) == null
                && f.Engine.PredictedDamage.Count() == 2, "Stun/damage output.");
            f.Actors[100] = f.Actors[100] with { Position = f.Actors[3].Position + Vector3.UnitX * 20 }; Scan(f);
            Check(f.Engine.LapisStunTarget(19) == null, "Out of range stun.");
            Finish(f, 3, false); Check(!f.Engine.PredictedDamage.Any(), "Cancelled raidwide leaked.");
        });
        foreach (uint index in Enumerable.Range(0x21, 8).Select(i => (uint)i))
            run($"Lapis stampede map {index:X} full initial corridor and thirty real effects strict half second", () =>
            {
                using var f = Prepare(0x3CFE);
                f.Engine.MapEffect(index, 0x00020001, f.Now); f.Render();
                var lane = Aoes(f, "stampede").Single();
                var left = index < 0x25;
                var p = new Vector3(left ? 4 : 44, 0, -759 + (index - (left ? 0x21u : 0x25u)) * 10);
                var shape = lane.Shape.Resolve(lane.Position, lane.Rotation, null);
                Check(shape.Contains(p + Vector3.UnitX * 19, 0) && shape.Contains(p - Vector3.UnitX * 19, 0)
                    && !shape.Contains(p + Vector3.UnitZ * 6, 0) && f.Snapshot.DangerAreas.Count == 1, "Initial corridor geometry.");
                f.Actors[10] = new(10, 0x3D03, p); Scan(f);
                for (uint i = 0; i < 29; ++i) f.Engine.Effect(10, 31357, i, f.Now);
                f.Engine.Effect(10, 31357, 28, f.Now, 101);
                f.Now = f.Start.AddSeconds(10); Scan(f); Check(Aoes(f, "stampede").Length == 1, "Duplicate/clock consumed lane early.");
                f.Engine.Effect(10, 31357, 29, f.Now);
                f.Now = f.Now.AddSeconds(.5); Scan(f); Check(Aoes(f, "stampede").Length == 1, "Reset used >= instead of >.");
                f.Now = f.Now.AddTicks(1); Scan(f); Check(Aoes(f, "stampede").Length == 0, "Lane did not end.");
            });
        run("Lapis stampede animal entry collection dynamic positions retained destroyed members and independent lanes", () =>
        {
            using var f = Prepare(0x3CFE);
            f.Engine.MapEffect(0x21, 0x00020001); f.Engine.MapEffect(0x22, 0x00020001);
            f.Actors[10] = new(10, 0x3CFF, new(0, 0, -759));
            f.Actors[11] = new(11, 0x3D02, new(-4, 0, -759)); Scan(f);
            var lane = Aoes(f, "stampede").First();
            Check(lane.Position.X == -4 && lane.Shape is RayRectangleTemplate { Length: 34 }, "Animals not collected in source order.");
            f.Actors[10] = f.Actors[10] with { Position = new(10, 0, -759) };
            f.Actors[11] = f.Actors[11] with { Position = new(6, 0, -759) }; Scan(f);
            Check(Aoes(f, "stampede").First().Position.X == 6 && Aoes(f, "stampede").Length == 2, "Moving channel frozen.");
            f.Actors.Remove(11); Scan(f); Check(Aoes(f, "stampede").First().Position.X == 6, "Destroyed cached beast erased.");
        });
        run("Lapis Albion ice LOS snapshots all crystals including dead untargetable both views and real finish", () =>
        {
            using var f = Prepare(0x3CFE); var c = f.Engine.Arena!.Position;
            f.Actors[2] = new(2, 0x3D04, c + Vector3.UnitX * 8, Dead: true, Targetable: false, HitboxRadius: 2); Scan(f);
            Cast(f, 1, 31364); var shadow = Aoes(f, "roar").Single(a => a.Safe).Shape.Resolve(c, 0, null);
            Check(shadow.Contains(c + Vector3.UnitX * 15, 0) && !shadow.Contains(c + Vector3.UnitZ * 15, 0), "LOS shape omitted dead crystal.");
            var world = HostDrawingGeometry.Convert(shadow);
            Check(world.Contains(c + Vector3.UnitX * 15, 0) && !world.Contains(c + Vector3.UnitZ * 15, 0), "World shadow.");
            f.Actors[2] = f.Actors[2] with { Position = c - Vector3.UnitX * 8 }; Scan(f);
            Check(Aoes(f, "roar").Single(a => a.Safe).Shape.Resolve(c, 0, null).Contains(c + Vector3.UnitX * 15, 0), "LOS moved after lock.");
            Finish(f, 1); Check(Aoes(f, "roar").Length == 0, "LOS finish leaked.");
        });
        foreach (var first in new uint[] { 32622, 31377 })
            run($"Lapis {first} concentric sequence actual first ending second prediction independent helper ending", () =>
            {
                using var f = Prepare(0x3971); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 1, first, duration: 4);
                var a = Aoes(f, "cycle").Single();
                Check((a.Shape is DonutTemplate) == (first == 32622), "Wrong first shape.");
                f.Now = f.Start.AddSeconds(4); Finish(f, 1);
                a = Aoes(f, "cycle").Single();
                Check((a.Shape is CircleTemplate) == (first == 32622)
                    && a.Activation == f.Now.AddSeconds(first == 32622 ? 2 : 2.7), "Second stage timing/shape.");
                f.Now = f.Start.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
                Check(Aoes(f, "cycle").Length == 1 && f.Snapshot.DangerAreas.Count > 0, "Prediction timeout consumed sequence.");
                Cast(f, 10, first == 32622 ? 32624u : 31379u, location: c + new Vector3(.8f, 0, .8f)); Finish(f, 10);
                Check(Aoes(f, "cycle").Length == 0, "Actual second ending did not clear.");
            });
        run("Lapis scarecrow icon actor position quantized sorting first two only first risky and true FIFO finish", () =>
        {
            using var f = Prepare(0x3971); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 15; ++i) f.Actors[i] = new(i, 0x3D06, c + Vector3.UnitX * (i - 10)); Scan(f);
            f.Engine.Icon(100, 337, f.Now, 11); f.Engine.Icon(100, 336, f.Now, 10); f.Render();
            var outputs = Aoes(f, "cross");
            Check(outputs.Length == 2 && outputs[0].Position == Quantized(f.Actors[10].Position) && outputs[0].Navigate && !outputs[1].Navigate,
                "Icon used target or cap/risk/order differs.");
            Check(!outputs[1].OutlineOnly && outputs[0].Color == new Vector4(1, 1, 0, 1)
                && outputs[1].Color == null, "Both telegraphs must fill; only imminent cross is emphasized.");
            var colors = f.Draw.Previews.Values.Where(a => a.Id.StartsWith("lapis/cross")).ToArray();
            foreach (var settings in new[] { new MasterPlayer.Settings.AoeViewSettings(), MasterPlayer.Settings.AoeViewSettings.CreateRadarDefaults() })
                Check(colors[0].Style(settings).FillColor == new Vector4(1, 1, 0, 1)
                    && colors[1].Style(settings).FillColor!.Value.W > 0, "Shared radar/world preview lost color or fill.");
            f.Engine.Icon(100, 339, f.Now, 13); f.Engine.Icon(100, 338, f.Now, 12); f.Render();
            Check(Aoes(f, "cross").Length == 2, "Four crosses shown at once.");
            var sourceShape = new BossMod.AOEShapeCross(40, 5); var cross = outputs[0];
            var native = cross.Shape.Resolve(cross.Position, cross.Rotation, null);
            for (var x = -42.137f; x < 42; x += 1.1f)
                for (var z = -42.231f; z < 42; z += 1.1f)
                {
                    var p = cross.Position + new Vector3(x, 0, z);
                    Check(native.Contains(p, 0) == sourceShape.Check(new(p.X, p.Z), new(cross.Position.X, cross.Position.Z), new(cross.Rotation)), "Cross size.");
                }
            f.Now = f.Start.AddSeconds(40); f.Engine.Tick(f.Now); f.Render(); Check(Aoes(f, "cross").Length == 2, "Cross clock consumed queue.");
            Cast(f, 14, 32703); Finish(f, 14);
            Check(Aoes(f, "cross").First().Position == Quantized(f.Actors[11].Position), "Finish did not promote FIFO.");
            f.Engine.Effect(14, 32703, 1, f.Now); Check(Aoes(f, "cross").Length == 2, "Effect advanced finish-only queue.");
            f.Now = f.Now.AddSeconds(1);
            Cast(f, 14, 32703); f.Engine.RemoveActor(14, f.Now); f.Actors.Remove(14); f.Render();
            Check(Aoes(f, "cross").First().Position == Quantized(f.Actors[12].Position),
                "Destroyed casting helper did not dispatch its final cast ending.");
        });
        run("Lapis tower map single soaker hard constraint real success position and failed effect retained", () =>
        {
            using var f = Prepare(0x3971);
            for (uint i = 7; i <= 10; ++i) f.Engine.MapEffect(i, 0x00010008, f.Now);
            f.Render(); Check(Aoes(f, "tower/").Count(a => a.Display) == 4 && Aoes(f, "towers/join").Length == 1, "Four tower outputs.");
            var tower = Aoes(f, "tower/").First(a => a.Display); f.Actors[101] = f.Actors[101] with { Position = tower.Position }; Scan(f);
            Check(Aoes(f, "tower/").Any(a => a.Id.EndsWith("/avoid")) && f.Engine.PredictedDamage.Any(p => p.TargetId == 101), "Occupied tower/damage.");
            f.Actors[10] = new(10, 0x233C, new(350, 0, -404)); Scan(f);
            f.Engine.Effect(10, 31384, 1, f.Now); Check(Aoes(f, "tower/").Count(a => a.Display) == 4, "Failure invented cleanup.");
            f.Engine.Effect(10, 31383, 1, f.Now); f.Render();
            Check(Aoes(f, "tower/").Count(a => a.Display) == 3, "Success failed precise position cleanup.");
        });
        run("Lapis GlassyEyed status dynamic eyes shared expiry ten second gate gain stale scan loss recovery facing", () =>
        {
            using var f = Prepare(0x3971); var c = f.Engine.Arena!.Position;
            f.Engine.StatusChanged(101, 3511, true, f.Now, expiresAt: f.Start.AddSeconds(20)); Scan(f);
            Check(!f.Engine.Gazes.Any(), "Gaze more than ten seconds early.");
            f.Now = f.Start.AddSeconds(10); Scan(f); Check(f.Engine.Gazes.Single().FinishAt == f.Start.AddSeconds(20), "Event swallowed by old snapshot.");
            f.Actors[101] = f.Actors[101] with { Statuses = [3511], StatusExpirations = new Dictionary<uint, DateTime> { [3511] = f.Start.AddSeconds(20) },
                Position = c + Vector3.UnitZ * 10 }; Scan(f);
            Check(f.Engine.Gazes.Single().Location == f.Actors[101].Position, "Eye frozen.");
            f.Now = f.Start.AddSeconds(19.5); Scan(f);
            Check(f.Engine.GazeFacing(c, 0) is { } facing && MathF.Abs(facing) > MathF.PI / 4, "PR facing not planned.");
            f.Engine.StatusChanged(101, 3511, false, f.Now); Scan(f);
            Check(!f.Engine.Gazes.Any(), "Stale status restored lost eye.");
            f.Engine.StatusChanged(100, 3364, true, f.Now); Scan(f);
            Check(f.Engine.LapisCleanseTarget(24)?.Id == 100 && f.Engine.LapisCleanseTarget(23)?.Id == 100, "Doom lost.");
        });
        run("Lapis shrinking square ring exact source geometry and map only confirmation", () =>
        {
            using var f = Prepare(0x3AE2); var c = f.Engine.Arena!.Position;
            Cast(f, 1, 31139);
            var ring = Aoes(f, "shrink").Single();
            Check(ring.Activation == f.Engine.Casts[1].FinishAt.AddSeconds(.7), "Shrink prediction.");
            var native = ring.Shape.Resolve(c, 0, null); var world = HostDrawingGeometry.Convert(native);
            for (var x = -31.137f; x < 31; x += .41f)
                for (var z = -31.231f; z < 31; z += .41f)
                {
                    var p = c + new Vector3(x, 0, z); var size = MathF.Max(MathF.Abs(x), MathF.Abs(z));
                    Check(native.Contains(p, 0) == (size <= 30 && size >= 20) && world.Contains(p, 0) == native.Contains(p, 0), "Shrink ring differs.");
                }
            Finish(f, 1); Check(Aoes(f, "shrink").Length == 1 && f.Engine.Arena.Contains(c + Vector3.UnitX * 25, 0), "Finish changed arena.");
            f.Engine.MapEffect(0, 0x00020001); f.Render();
            Check(Aoes(f, "shrink").Length == 0 && !f.Engine.Arena.Contains(c + Vector3.UnitX * 25, 0), "Map failed shrink.");
        });
        run("Lapis ram and bomb telegraphs actual length delay packet correction FIFO effects duplicate targets and no timeout", () =>
        {
            using var f = Prepare(0x3AE2); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 14; ++i) f.Actors[i] = new(i, 0x233C, c + Vector3.UnitX * (i - 10)); Scan(f);
            for (ulong i = 10; i < 12; ++i)
            {
                Cast(f, i, 32693, duration: 2, location: f.Actors[i].Position + Vector3.UnitZ * 16);
                Finish(f, i);
            }
            for (ulong i = 12; i < 14; ++i) { Cast(f, i, 32695, duration: 2); Finish(f, i); }
            Check(Aoes(f, "ram").Length == 0, "Unexpected named output.");
            var ram = f.Engine.AmbientAoes().Where(a => a.Name == "冲锋连续预告").ToArray();
            var bombs = f.Engine.AmbientAoes().Where(a => a.Name == "水圈连续预告").ToArray();
            Check(ram.Length == 2 && ram.All(a => a.Shape is RayRectangleTemplate { Length: 16 }) && bombs.Length == 2
                && ram[0].Activation == f.Start.AddSeconds(7.7) && bombs[0].Activation == f.Start.AddSeconds(8.1), "FIFO shape/timing.");
            f.Now = f.Start.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Name is "冲锋连续预告" or "水圈连续预告") == 4, "Clock consumed FIFO.");
            f.Engine.Effect(1, 32694, 1, f.Now); f.Engine.Effect(1, 32694, 1, f.Now, 101);
            Check(f.Engine.AmbientAoes().Count(a => a.Name == "冲锋连续预告") == 1, "Duplicate advanced FIFO.");
            f.Engine.Effect(1, 32696, 1, f.Now); Check(f.Engine.AmbientAoes().Count(a => a.Name == "水圈连续预告") == 1, "Bomb effect failed.");
        });
        run("Lapis knockback ignores immunity source effect count navigation after four and reset on sixth finish", () =>
        {
            using var f = Prepare(0x3AE2); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 5, Statuses = [160] };
            f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c); Scan(f);
            Cast(f, 10, 31122); Check(Aoes(f, "knockback").Single(a => a.Id.EndsWith("landing")).Position == c + Vector3.UnitX * 15,
                "Immunity suppressed mandatory knockback.");
            Check(!Aoes(f, "knockback/inside").Any(), "Four effect constraint early.");
            for (uint i = 0; i < 4; ++i) f.Engine.Effect(11, 31120, i, f.Now);
            Check(Aoes(f, "knockback/inside").Length == 1, "Four effect constraint absent.");
            Cast(f, 11, 31120); f.Engine.Effect(11, 31120, 4, f.Now); f.Engine.Effect(11, 31120, 5, f.Now);
            Finish(f, 11); Check(Aoes(f, "knockback/inside").Length == 0, "Six effect finish did not reset.");
            Finish(f, 10); Check(!Aoes(f, "knockback").Any(), "Knockback finish leaked.");
        });
        run("Lapis icons dynamic spread hard stack tether either endpoint snapshot cancellation and torrent bait", () =>
        {
            using var f = Prepare(0x3AE2); var c = f.Engine.Arena!.Position;
            f.Engine.Icon(101, 139); f.Engine.Icon(100, 161); f.Render();
            Check(Aoes(f, "spread").Any() && Aoes(f, "stack").Any(a => a.Id.EndsWith("gather")), "Icon output.");
            f.Engine.Effect(1, 31376, 1, f.Now); Check(!Aoes(f, "stack").Any(), "Hydrofall real effect failed clear.");
            f.Engine.Effect(1, 31134, 1, f.Now, 101); Check(!Aoes(f, "spread").Any(), "Spread target cleanup.");
            f.Actors[10] = new(10, 0x3E97, c); Scan(f);
            f.Engine.Tether(10, 1, true, f.Now, 101); f.Render();
            Check(Aoes(f, "tether").Count(a => a.Display) == 2 && Aoes(f, "tether").Any(a => a.Navigate), "Reversed tether failed.");
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [1] = 101 } }; Scan(f);
            f.Engine.Tether(101, 1, false, f.Now, 10); Scan(f); Check(!Aoes(f, "tether").Any(), "Stale snapshot restored cancelled tether.");
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong>() }; Scan(f);
            Cast(f, 1, 31118, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitZ * 10 }; Scan(f);
            Check(Math.Abs(Aoes(f, "torrent").Single(a => a.Display).Rotation) < .001f && f.Engine.PredictedDamage.Any(p => p.TargetId == 101), "Torrent bait froze.");
        });
        run("Lapis NPC yell tsunami exact delay real effect only reset and native solver", () =>
        {
            using var f = Prepare(0x3AE2); f.Engine.NpcYell(1, 15175, f.Now);
            Check(f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(4.5)) && f.Engine.PredictedDamage.Count() == 2, "Yell timing.");
            f.Now = f.Start.AddSeconds(20); f.Engine.Tick(f.Now);
            f.Engine.Effect(1, 31138, 1, f.Now); Check(f.Engine.PredictedDamage.Count() == 2, "Enrage/clock cleared source.");
            f.Engine.Effect(1, 31137, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Tsunami failed clear.");
            f.Actors[10] = new(10, 0x233C, f.Actors[100].Position); Scan(f); Cast(f, 10, 31123, duration: 3);
            var s = f.Snapshot; var p = f.Actors[100].Position;
            var solution = new SafeZoneSolver().Solve(s, new(p, 0, 6, false, false, 0, true, s.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
                && solution.ExecutablePath.Count + solution.ContinuationPath.Count > 0, "Solver failed.");
            f.Engine.ResetFight(); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Reset leaked.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1097); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Griffin(Fight f)
    {
        f.Actors[3] = new(3, 0x3E9F, f.Actors[100].Position, InCombat: true);
        f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, double duration = 5, Vector3? location = null)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, .7f, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong source, bool completed = true)
    {
        if (f.Actors.TryGetValue(source, out var actor)) f.Actors[source] = actor with { Cast = null };
        f.Engine.Finish(source, endedAt: f.Now, completed: completed); f.Render();
    }
    private static Vector3 Quantized(Vector3 p)
    {
        var source = new BossMod.WPos(p.X, p.Z).Quantized();
        return new(source.X, p.Y, source.Z);
    }
    private static DungeonAmbientAoe[] Aoes(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("lapis/" + key)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
