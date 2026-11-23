using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class AlaMhigoRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var boss in new uint[] { 0x1BA4, 0x1BA6 })
            run($"AlaMhigo {boss:X} exported arena matches source and world geometry", () =>
            {
                using var f = Prepare(boss);
                var type = boss == 0x1BA4
                    ? typeof(BossMod.Stormblood.Dungeon.D06AlaMhigo.D061MagitekScorpion.D061MagitekScorpion)
                    : typeof(BossMod.Stormblood.Dungeon.D06AlaMhigo.D062AulusMalAsina.D062AulusMalAsina);
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var native = f.Engine.Arena!; var host = HostDrawingGeometry.Convert(native); var bounds = native.GetBounds();
                for (var x = bounds.MinX - .7f; x < bounds.MaxX + .7f; x += .41f)
                    for (var z = bounds.MinZ - .7f; z < bounds.MaxZ + .7f; z += .41f)
                    {
                        var p = new Vector3(x, 0, z); var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 1146) && !ArrDungeonCatalog.Owns(boss, 1145), "Ownership differs.");
            });
        run("AlaMhigo Scorpion twelve rest hits preserve first cast and packet heading", () =>
        {
            using var f = Prepare(0x1BA4); var center = Center(0x1BA4);
            f.Actors[10] = new(10, 0x18D6, center); f.Actors[11] = new(11, 0x18D6, center); Scan(f);
            Cast(f, 10, 8265, 3, MathF.PI / 2); Cast(f, 11, 8266, 3, -MathF.PI / 2);
            var first = f.Engine.AmbientAoes().First();
            Check(first.Activation == f.Start.AddSeconds(4.3), "Laser activation shifted.");
            Check(f.Draw.Previews.Values.Any(p => p.Geometry.Contains(center + Vector3.UnitX * 20))
                && f.Draw.Previews.Values.All(p => !p.Geometry.Contains(center + Vector3.UnitZ * 5.1f)), "Laser dimensions/heading lost.");
            f.Engine.Finish(10, completed: true); f.Engine.Finish(11, completed: true);
            f.Engine.Effect(10, 8265, 1, f.Now); f.Engine.Effect(11, 8266, 2, f.Now);
            for (uint hit = 1; hit <= 12; ++hit)
            {
                f.Engine.Effect(10, 8267, hit + 10, f.Now); f.Engine.Effect(10, 8267, hit + 10, f.Now); f.Render();
                Check(f.Draw.Previews.Count == (hit == 12 ? 0 : 2), $"Laser cleared at wrong rest hit {hit}.");
            }
            f.Now = f.Start.AddSeconds(8); Cast(f, 10, 8265, 3); f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled laser retained.");
        });
        run("AlaMhigo target search and fire prediction hand off to live voidzone", () =>
        {
            using var f = Prepare(0x1BA4); var center = Center(0x1BA4);
            f.Actors[10] = new(10, 0x1BA5, center); Scan(f);
            f.Engine.Effect(20, 8263, 1, f.Now, 100, occurredAt: f.Start.AddSeconds(-.2)); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id.Contains("predicted")).Activation == f.Start.AddSeconds(.5), "Fire event clock shifted.");
            f.Actors[10] = f.Actors[10] with { EventState = 1 };
            f.Actors[11] = new(11, 0x1EA66D, center); Scan(f);
            Check(f.Draw.Previews.Count == 1 && !f.Engine.AmbientAoes().Any(a => a.Id.Contains("predicted")), "Prediction did not hand off.");
            f.Actors[11] = f.Actors[11] with { EventState = 7 }; Scan(f);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Inactive voidzone retained.");
        });
        run("AlaMhigo body tethers lock each origin and remove matching source only", () =>
        {
            using var f = Prepare(0x1BA6); var center = Center(0x1BA6);
            f.Actors[101] = new(101, 0, center, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x1BAF, center + Vector3.UnitX * 10);
            f.Actors[11] = new(11, 0x1BAF, center - Vector3.UnitX * 10); Scan(f);
            f.Engine.Tether(100, 45, true, f.Now, 10); f.Engine.Tether(101, 45, true, f.Now, 11); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 2, "Body entry/line not drawn.");
            f.PlayerId = 101; Scan(f);
            f.Engine.Tether(101, 45, false, f.Now, 11); f.Render();
            Check(f.Draw.Previews.Count == 0, "Second body untether removed wrong entry.");
            f.PlayerId = 100; f.Actors[10] = f.Actors[10] with { Position = center }; Scan(f);
            Check(f.Draw.Previews.Values.Any(a => a.Geometry.Contains(center + Vector3.UnitX * 10)), "Body origin moved with actor.");
            SolveInto(f, center + Vector3.UnitX * 10, 2);
        });
        run("AlaMhigo body cancellation blocks stale tethers until raw snapshot clears", () =>
        {
            using var f = Prepare(0x1BA6); var center = Center(0x1BA6);
            f.Actors[10] = new(10, 0x1BAF, center + Vector3.UnitX * 8, Tethers: new Dictionary<uint, ulong> { [45] = 100 }); Scan(f);
            f.Engine.Tether(100, 45, false, f.Now, 10);
            f.Now = f.Start.AddSeconds(2); Scan(f); Check(f.Draw.Previews.Count == 0, "Stale body resurrected.");
            f.Actors[10] = f.Actors[10] with { Tethers = null }; Scan(f);
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [45] = 100 } }; Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 1, "New body failed to restore.");
        });
        foreach (var (oid, action, inside, outside) in new (uint, uint, Vector3, Vector3)[]
        {
            (0x1BA7, 8282, new(7.9f, 0, 0), new(8.1f, 0, 0)),
            (0x1BA7, 8283, new(12, 0, 0), new(11, 0, 0)),
            (0x1BA8, 8276, new(.9f, 0, 45.5f), new(1.1f, 0, 20)),
        })
            run($"AlaMhigo Aulus {action} exact cast shape in radar and world", () =>
            {
                using var f = Prepare(0x1BA6); var center = Center(0x1BA6);
                f.Actors[10] = new(10, oid, center); Scan(f); Cast(f, 10, action, 3);
                var geometry = f.Draw.Previews.Single().Value.Geometry; var world = HostDrawingGeometry.Convert(geometry);
                Check(geometry.Contains(center + inside) && !geometry.Contains(center + outside)
                    && world.Contains(center + inside) && !world.Contains(center + outside), "Cast shape differs.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled cast leaked.");
            });
        run("AlaMhigo Aulus spread follows other party target and clears on finish", () =>
        {
            using var f = Prepare(0x1BA6); var center = Center(0x1BA6);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
            var cast = new DungeonCastObservation(8286, center, 0, f.Now, f.Now.AddSeconds(5.3), 101);
            f.Actors[10] = new(10, 0x18D6, center, Cast: cast); Scan(f);
            Check(f.Draw.Previews.Values.Any(p => p.Geometry.Contains(f.Actors[101].Position))
                && f.Snapshot.DangerAreas.Count == 1, "Spread target not used.");
            f.Actors[101] = f.Actors[101] with { Position = center - Vector3.UnitX * 8 }; Scan(f);
            Check(f.Draw.Previews.Values.Any(p => p.Geometry.Contains(f.Actors[101].Position)), "Spread stopped following target.");
            f.Engine.Finish(10, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "Spread finish leaked.");
        });
        foreach (var (oid, action, radius) in new (uint, uint, float)[]
        {
            (0x1BAA, 8294, 8), (0x1BAB, 9607, 8), (0x1BAA, 9398, 10), (0x1BAB, 8300, 10),
        })
            run($"AlaMhigo Zenos {action} real radius and cancellation", () =>
            {
                using var f = Prepare(0x1BAA); Shrink(f); var center = Center(0x1BAA);
                f.Actors[11] = new(11, oid, center); Scan(f); Cast(f, 11, action, 6);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(center + Vector3.UnitX * (radius - .1f))
                    && !geometry.Contains(center + Vector3.UnitX * (radius + .1f)), "AOE radius differs.");
                f.Engine.Finish(11); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled AOE retained.");
            });
        run("AlaMhigo Zenos starts square restores shrink event before cast and resets", () =>
        {
            using var f = Prepare(0x1BAA); var center = Center(0x1BAA);
            Check(f.Engine.Arena!.Contains(center + new Vector3(22, 0, 22))
                && f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(10), "Initial shrink prediction differs.");
            f.Actors[10] = new(10, 0x1EA1A1, center, EventAnimation: 0x00040008); Scan(f);
            Check(!f.Engine.Arena!.Contains(center + Vector3.UnitX * 21) && f.Draw.Previews.Count == 0, "Shrink restore failed.");
            var arena = f.Engine.Arena; Scan(f); Check(ReferenceEquals(arena, f.Engine.Arena), "Restored animation rebuilt arena each frame.");
            f.Engine.ResetFight(); f.Actors.Remove(10); Scan(f);
            Check(f.Engine.Arena!.Contains(center + new Vector3(22, 0, 22)), "Reset retained shrink.");
        });
        run("AlaMhigo Troika yell starts first cleave actual effect ends then second and third casts", () =>
        {
            using var f = Prepare(0x1BAA); var center = Center(0x1BAA); Shrink(f);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 8 };
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(), "First cleave started before yell.");
            f.Engine.NpcYell(1, 5372, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/party/101")), "Tank bait missing.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id == "ala/troika/avoid"), "Other viewer cleave missing.");
            f.Engine.Effect(1, 8288, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "First hit retained.");
            f.Actors[11] = new(11, 0x18D6, center); Scan(f);
            foreach (var action in new uint[] { 8289, 8290 })
            {
                f.Now = f.Now.AddSeconds(3); Cast(f, 11, action, 2, MathF.PI / 2);
                Check(f.Draw.Previews.Single().Value.Geometry.Contains(center + Vector3.UnitX * 9.9f), "Follow-up cone wrong.");
                f.Engine.Finish(11, completed: true); f.Render();
            }
        });
        foreach (var action in new uint[] { 8293, 9606 })
            run($"AlaMhigo {action} knockback solver reaches center and immunity changes landing only", () =>
            {
                using var f = Prepare(0x1BAA); var center = Center(0x1BAA); Shrink(f);
                f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 10 };
                var source = action == 8293 ? 1UL : 11UL;
                if (source == 11) f.Actors[11] = new(11, 0x1BAB, center);
                Scan(f); Cast(f, source, action, 6); SolveInto(f, center, 5);
                Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/landing")), "Landing missing.");
                f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(10) } }; Scan(f);
                Check(!f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/landing"))
                    && f.Snapshot.DangerAreas.Count == 1, "Immunity removed center constraint or retained displacement.");
            });
        run("AlaMhigo sword baits share original six point three clock and persist past natural finish", () =>
        {
            using var f = Prepare(0x1BAA); var center = Center(0x1BAA); Shrink(f);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 8 };
            f.Actors[11] = new(11, 0x1BAB, center - Vector3.UnitX * 8); Scan(f);
            Cast(f, 1, 8296, 5.5); Cast(f, 11, 9608, 5.5);
            Check(f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ala/sword")).All(a => a.Activation == f.Start.AddSeconds(6.3))
                && f.Draw.Previews.Count == 4, "Sword clock or clone baits differ.");
            f.Engine.Finish(1, completed: true); f.Engine.Finish(11, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 4, "Sword removed at natural finish.");
            f.Engine.Effect(20, 9609, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Sword hit retained baits.");
        });
        run("AlaMhigo spark original clock main knockback suppresses navigation only", () =>
        {
            using var f = Prepare(0x1BAA); var center = Center(0x1BAA); Shrink(f);
            f.Actors[1] = f.Actors[1] with { Position = center + Vector3.UnitX * 10 };
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 15 };
            f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false);
            f.Actors[11] = new(11, 0x1BAB, center); Scan(f);
            f.Engine.Tether(100, 41, true, f.Start.AddSeconds(-.2), 1); f.Render();
            var spark = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(Math.Abs((spark.Activation!.Value - f.Start.AddSeconds(7.8)).TotalMilliseconds) < 1
                && f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/outward"))
                && f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/party/101")), "Spark bait or clock missing.");
            Cast(f, 1, 8293, 6);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("ala/spark") && a.Navigate)
                && f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/display")), "Main knockback suppression differs.");
            f.Engine.Finish(1); Cast(f, 11, 9606, 6);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/outward")), "Clone incorrectly suppressed spark.");
            f.Engine.Effect(1, 8299, 1, f.Now); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("ala/spark")), "Spark hit retained tether.");
        });
        run("AlaMhigo dead target and reset remove body sword and all previews", () =>
        {
            using var f = Prepare(0x1BAA); Shrink(f); Cast(f, 1, 8296, 5.5);
            f.Engine.RemoveActor(100); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed sword target leaked.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset leaked.");
        });
        run("AlaMhigo spark cancellation survives stale raw tether and other viewer avoids cone", () =>
        {
            using var f = Prepare(0x1BAA); Shrink(f);
            f.Actors[101] = new(101, 0, Center(0x1BAA) + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false);
            f.Actors[1] = f.Actors[1] with { Tethers = new Dictionary<uint, ulong> { [41] = 101 } }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "ala/spark/1/avoid"), "Other viewer cone absent.");
            f.Engine.Tether(101, 41, false, f.Now, 1);
            f.Now = f.Start.AddSeconds(2); Scan(f); Check(f.Draw.Previews.Count == 0, "Stale spark resurrected.");
            f.Actors[1] = f.Actors[1] with { Tethers = null }; Scan(f);
            f.Actors[1] = f.Actors[1] with { Tethers = new Dictionary<uint, ulong> { [41] = 101 } }; Scan(f);
            Check(f.Draw.Previews.Count == 2, "Fresh spark failed to restore.");
        });
    }

    private static Vector3 Center(uint boss) => boss switch { 0x1BA4 => new(-191, 0, 72), 0x1BA6 => new(250, 0, -70), _ => new(250, 0, -353) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1146); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Shrink(Fight f) { f.Actors[50] = new(50, 0x1EA1A1, Center(0x1BAA), EventAnimation: 0x00040008); Scan(f); }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void SolveInto(Fight f, Vector3 target, float radius)
    {
        var snapshot = f.Snapshot; var start = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(start, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
            && Vector3.Distance(path[^1].Position, target) <= radius, $"Solver failed to enter R{radius}: {solution.Status}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
