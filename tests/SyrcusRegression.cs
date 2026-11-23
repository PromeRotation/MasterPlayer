using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class SyrcusRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Syrcus four native bosses territory ownership arenas and lifecycle", () =>
        {
            Check(ArrDungeonCatalog.IsScript("masterplayer.alliance.syrcus")
                && SyrcusCatalog.Create().Length == 4, "Catalog/entry.");
            foreach (var definition in SyrcusCatalog.Create())
            {
                using var f = Scene(definition.BossOid);
                Check(f.Engine.Active?.BossOid == definition.BossOid, "Boss selection.");
                var arena = f.Engine.Arena!;
                var radius = definition.BossOid == 0xBE0 ? 30 : 35;
                Check(arena.Contains(arena.Position + Vector3.UnitX * (radius - .01f))
                    && !arena.Contains(arena.Position + Vector3.UnitX * (radius + .01f)), "Arena radius.");
                f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
                Check(f.Engine.Active != null && f.Engine.PreserveFightAcrossPlayerDeath, "Personal death reset.");
                f.Engine.DirectorUpdate(0x40000005); f.Scan();
                Check(f.Engine.Active == null && f.Snapshot.DangerAreas.Count == 0, "Wipe repull.");
                f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
                f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan();
                Check(f.Engine.Active == null, "HP0 end.");
            }
            using var wrong = new Fight(0xBC1, 174);
            Check(wrong.Engine.Active == null, "Cross-territory activation.");
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0xBC1, 2321, new BossMod.AOEShapeCircle(6.75f)),
            (0xBC1, 2330, new BossMod.AOEShapeRect(61, 2)),
            (0xBC1, 2331, new BossMod.AOEShapeDonut(6, 12)),
            (0xBD7, 2348, new BossMod.AOEShapeCircle(8)),
            (0xBD7, 2353, new BossMod.AOEShapeCone(30, new(MathF.PI / 8))),
            (0xBE0, 2382, new BossMod.AOEShapeCircle(10)),
            (0xBE0, 1875, new BossMod.AOEShapeCone(6, new(MathF.PI / 8))),
            (0xBE0, 2384, new BossMod.AOEShapeCircle(6)),
            (0xBE0, 2383, new BossMod.AOEShapeCircle(6)),
            (0xBDB, 2355, new BossMod.AOEShapeCircle(10)),
            (0xBDB, 2357, new BossMod.AOEShapeCircle(8)),
            (0xBDB, 2356, new BossMod.AOEShapeCircle(8)),
            (0xBDB, 2358, new BossMod.AOEShapeRect(60, 5)),
        })
            run($"Syrcus {boss:X} action{action} exact geometry locked cast independent dual view and finish", () =>
            {
                using var f = Scene(boss); Start(f, 10, action, 3, .7f);
                var cast = f.Engine.Casts[10];
                var shape = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (var x = -65f; x <= 65; x += .83f)
                    for (var z = -65f; z <= 65; z += .89f)
                        Check(shape.Contains(cast.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(cast.Location.X + x, cast.Location.Z + z),
                                new(cast.Location.X, cast.Location.Z), new(.7f)), "Source geometry mismatch.");
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Drawing/navigation missing.");
                f.Actors[10] = f.Actors[10] with { Position = cast.Location + Vector3.UnitX * 20, Rotation = 2 };
                f.Scan();
                Check(f.Engine.Casts[10].Location == cast.Location && f.Engine.Casts[10].Rotation == cast.Rotation, "Locked cast moved.");
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Nominal timeout removed cast.");
                f.Engine.Finish(10, action, f.Now, false); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancellation cleanup.");
            });
        run("Syrcus Scylla concurrent staff clocks and repeated source casts remain independent", () =>
        {
            using var f = Scene(0xBC1);
            Start(f, 10, 2330, 3); Start(f, 11, 2331, 5); Start(f, 12, 2330, 7, MathF.PI);
            Check(f.Engine.Casts.Count == 3 && f.Draw.Previews.Count == 3, "Concurrent staffs.");
            f.Engine.Finish(11, 2331, f.Now, true); f.Render();
            Check(f.Engine.Casts.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Wrong source removed.");
            f.Now = f.Now.AddSeconds(1); Start(f, 11, 2331, 4);
            Check(f.Engine.Casts.Count == 3, "Repeated cast suppressed by prior finish.");
        });
        run("Syrcus raidwide cast predicts party on exact finish without a fabricated danger circle", () =>
        {
            foreach (var (boss, action) in new (uint, uint)[] { (0xBC1, 2317), (0xBDB, 2359), (0xBDB, 2361) })
            {
                using var f = Scene(boss);
                f.Actors[101] = new(101, 0, f.Actors[100].Position, PartySlot: 1, Hostile: false); f.Scan();
                Start(f, 10, action, 5);
                var hits = f.Engine.PredictedDamage.ToArray();
                Check(hits.Length == 2 && hits.All(p => p.Activation == f.Now.AddSeconds(5.3))
                    && f.Snapshot.DangerAreas.Count == 0, "Raidwide targets/clock/geometry.");
                f.Engine.Finish(10, action, f.Now, true); f.Render();
                Check(!f.Engine.PredictedDamage.Any(), "Raidwide finish.");
            }
        });
        run("Syrcus Amon shelter freezes living ice and permits hitboxes with plus0.2 inner radius", () =>
        {
            using var f = Scene(0xBE0); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0xBE1, c + Vector3.UnitX * 10, HitboxRadius: 1.8f);
            f.Actors[21] = new(21, 0xBE1, c - Vector3.UnitX * 10, Dead: true, HitboxRadius: 1.8f); f.Scan();
            Start(f, 1, 2441, 15);
            var safe = Group(f).Single(a => a.Safe);
            var shape = safe.Shape.Resolve(c, 0, null);
            Check(safe.Activation == f.Now.AddSeconds(15.3) && shape.Contains(c + Vector3.UnitX * 15)
                && !shape.Contains(c - Vector3.UnitX * 15), "Ice filter/clock.");
            Check(!shape.Contains(c + Vector3.UnitX * 10.19f) && shape.Contains(c + Vector3.UnitX * 10.21f), "Ice hitbox/inner radius.");
            f.Actors[20] = f.Actors[20] with { Dead = true, Position = c + Vector3.UnitZ * 10 }; f.Scan();
            Check(Group(f).Single(a => a.Safe).Shape.Resolve(c, 0, null).Contains(c + Vector3.UnitX * 15), "Shelter followed ice.");
            Check(f.Engine.Hints(c + Vector3.UnitZ * 10).Contains("躲在冰块后")
                && !f.Engine.Hints(c + Vector3.UnitX * 15).Contains("躲在冰块后"), "Personal shelter hint.");
        });
        run("Syrcus Amon FIFO shows first shelter and snapshots earliest caster origin", () =>
        {
            using var f = Scene(0xBE0); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0xBE1, c + Vector3.UnitX * 10, HitboxRadius: 1.8f); f.Scan();
            Start(f, 10, 2441, 10);
            f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitZ * 10 }; f.Scan();
            f.Actors[11] = new(11, 0x1B2, c + Vector3.UnitX * 20); f.Scan();
            Start(f, 11, 2441, 15);
            Check(Group(f).Length == 2 && Group(f).Single(a => a.Safe).Shape.Resolve(c, 0, null).Contains(c + Vector3.UnitX * 15),
                "First shelter replaced by later cast.");
            f.Engine.Finish(10, 2441, f.Now, true); f.Render();
            var next = Group(f).Single(a => a.Safe);
            Check(next.Activation == f.Now.AddSeconds(15.3)
                && next.Shape.Resolve(c, 0, null).Contains(c + Vector3.UnitZ * 15)
                && !next.Shape.Resolve(c, 0, null).Contains(c + Vector3.UnitX * 15), "FIFO/earliest origin snapshot.");
            f.Engine.RemoveActor(11, f.Now); f.Actors.Remove(11); f.Render();
            Check(Group(f).Length == 0 && f.Draw.Previews.Count == 0, "Destroyed caster cleanup.");
        });
        run("Syrcus Amon no living ice has no fabricated shelter and solver reaches real shadow", () =>
        {
            using var f = Scene(0xBE0); var c = f.Engine.Arena!.Position;
            Start(f, 10, 2441, 15); Check(Group(f).Length == 0, "Invented empty shelter.");
            f.Engine.Finish(10, 2441, f.Now, true); f.Now = f.Now.AddSeconds(1);
            f.Actors[100] = f.Actors[100] with { Position = c + new Vector3(10, 0, 4) };
            f.Actors[20] = new(20, 0xBE1, c + Vector3.UnitX * 10, HitboxRadius: 1.8f); f.Scan();
            Start(f, 10, 2441, 5);
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 1
                && Group(f).Single(a => a.Safe).Shape.Resolve(c, 0, null).Contains(path[^1].Position), "Actual shelter route.");
            f.Output.Clear(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Owner cleanup.");
        });
    }

    private static Fight Scene(uint boss)
    {
        var f = new Fight(boss, SyrcusCatalog.TERRITORY);
        var c = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = c, Hp = 1000, MaxHp = 1000 };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 8, PartySlot: 0, PartyRole: "Tank", Hostile: false);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0)
    {
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x1B2, f.Engine.Arena!.Position); f.Scan(); }
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("syrcus/")).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
