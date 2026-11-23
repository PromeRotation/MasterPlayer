using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class ClyteumRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Clyteum native discovery arena boundaries no invented shield gaze or raidwide", () =>
        {
            Check(ArrDungeonCatalog.Create(1345).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.clyteum"), "Discovery.");
            foreach (var oid in new uint[] { 0x4C2C, 0x4C3F, 0x4C28 })
            {
                using var f = Prepare(oid); var p = f.Engine.Arena!.Position;
                Check(ClyteumCatalog.Owns(oid) && !f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Controls.");
                var radius = oid == 0x4C3F ? 15 : 20;
                var host = HostDrawingGeometry.Convert(f.Engine.Arena);
                Check(f.Engine.Arena.Contains(p + Vector3.UnitX * (radius - .01f), 0)
                    && !host.Contains(p + Vector3.UnitX * (radius + .01f), 0), "Arena.");
            }
            using var chort = Prepare(0x4C3F);
            Cast(chort, 1, 48884); Cast(chort, 10, 50408);
            Check(!chort.Engine.PredictedDamage.Any() && !Group(chort, "").Any(), "Invented inactive raidwide.");
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x4C2C, 50177, new BossMod.AOEShapeCone(70, new(MathF.PI * 50 / 180))),
            (0x4C2C, 50178, new BossMod.AOEShapeCone(70, new(MathF.PI * 50 / 180))),
            (0x4C2C, 48898, new BossMod.AOEShapeCircle(5)),
            (0x4C3F, 48871, new BossMod.AOEShapeRect(40, 4)),
            (0x4C3F, 50400, new BossMod.AOEShapeRect(40, 8)),
            (0x4C3F, 48876, new BossMod.AOEShapeRect(40, 8)),
            (0x4C3F, 50361, new BossMod.AOEShapeCone(50, new(MathF.PI / 3))),
            (0x4C28, 50313, new BossMod.AOEShapeCone(30, new(MathF.PI / 2))),
            (0x4C28, 48933, new BossMod.AOEShapeRect(80, 2)),
            (0x4C28, 48937, new BossMod.AOEShapeCone(60, new(MathF.PI / 12))),
            (0x4C28, 48943, new BossMod.AOEShapeCone(60, new(MathF.PI / 12))),
            (0x4C28, 48935, new BossMod.AOEShapeCircle(7)),
            (0x4C28, 48940, new BossMod.AOEShapeCircle(7)),
        })
            run($"Clyteum {action} source shape locked packet correction both outputs observed end", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action, 14.5);
                var c = f.Actors[10].Cast! with { Rotation = 1.1f, Location = f.Engine.Arena!.Position + Vector3.UnitX * 2 };
                f.Engine.StartCast(10, c, f.Now); f.Actors[10] = f.Actors[10] with { Cast = c }; f.Render();
                var a = Group(f, "simple").Single(); var g = a.Shape.Resolve(a.Position, a.Rotation, null);
                Check(a.Position == c.Location && a.Activation == c.FinishAt, "Packet/timing.");
                for (var x = -40.17f; x < 40; x += .71f)
                    for (var z = -40.13f; z < 40; z += .73f)
                    {
                        var p = a.Position + new Vector3(x, 0, z);
                        Check(g.Contains(p, 0) == source.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)), "Source geometry.");
                    }
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Drawing/navigation.");
                f.Now = f.Now.AddMinutes(1); f.Engine.Tick(f.Now);
                Check(Group(f, "simple").Length == 1, "Clock consumed unfinished cast.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "End retained.");
            });
        run("Clyteum first two beams advance in insertion order and all long Malphas casts risky immediately", () =>
        {
            using var f = Prepare(0x4C2C);
            Cast(f, 10, 50177, 9); Cast(f, 11, 50178, 5); Cast(f, 12, 50177, 7);
            Check(Group(f, "simple").Length == 2 && f.Snapshot.DangerAreas.Count == 2, "Beam max.");
            Finish(f, 10); Check(Group(f, "simple").Length == 2 && Group(f, "simple").Any(a => a.Id.Contains("/12/")), "Next beam.");
            using var m = Prepare(0x4C28);
            Cast(m, 10, 48943, 14.5); Cast(m, 11, 48937, 6.5); Cast(m, 12, 48940, 14.5);
            Check(Group(m, "simple").Length == 3 && m.Snapshot.DangerAreas.Count == 3, "Long casts delayed.");
        });
        run("Clyteum MotionTracker event expiry one second threshold remains until status loss", () =>
        {
            using var f = Prepare(0x4C2C);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 5191, Duration = 5, Timestamp = f.Now }, f.Now);
            Check(f.Engine.ClyteumStopActivation == f.Now.AddSeconds(5) && !f.Engine.RequireAllStop, "Status adapter.");
            f.Now = f.Now.AddSeconds(4); Scan(f); Check(!f.Engine.RequireAllStop, "Strict threshold.");
            f.Now = f.Now.AddMilliseconds(1); f.Engine.Tick(f.Now); Check(f.Engine.RequireAllStop, "Threshold missed.");
            f.Now = f.Now.AddSeconds(10); f.Engine.Tick(f.Now); Check(f.Engine.RequireAllStop, "Clock expired stay.");
            f.Engine.StatusChanged(100, 5191, false, f.Now); Check(!f.Engine.RequireAllStop, "Lose did not release.");
        });
        run("Clyteum scanner display half nine control half ten no dodge and renderflag overwrite", () =>
        {
            using var f = Prepare(0x4C2C); var p = f.Engine.Arena!.Position;
            f.Actors[30] = new(30, 0x4C2D, p, RenderFlags: 0);
            f.Actors[100] = f.Actors[100] with { Position = p + Vector3.UnitZ * 9.5f }; Scan(f);
            Check(f.Engine.RequireAllStop && f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 1, "Beam controls independent from dodge.");
            var a = Group(f, "beam").Single(); var g = a.Shape.Resolve(a.Position, a.Rotation, null);
            Check(!g.Contains(f.Actors[100].Position, 0) && g.Contains(p + Vector3.UnitZ * 8.9f, 0), "Display extents.");
            f.Actors[100] = f.Actors[100] with { Position = p + Vector3.UnitZ * 10.1f }; Scan(f);
            Check(!f.Engine.RequireAllStop, "Control extents.");
            f.Actors[30] = f.Actors[30] with { RenderFlags = 42 }; Scan(f); Check(Group(f, "beam").Length == 1, "Other flags cleared.");
            f.Actors[31] = new(31, 0x4C2D, p, RenderFlags: 16384); Scan(f); Check(Group(f, "beam").Length == 0, "Other scanner clear.");
        });
        run("Clyteum scanner overwrites status and personal beam uses changed perspective without drawings", () =>
        {
            using var f = Prepare(0x4C2C); var p = f.Engine.Arena!.Position;
            f.Engine.StatusChanged(100, 5191, true, f.Now, expiresAt: f.Now.AddSeconds(20));
            f.Actors[30] = new(30, 0x4C2D, p, RenderFlags: 0); Scan(f); Check(f.Engine.RequireAllStop, "Inside beam.");
            f.PlayerId = 101; f.Actors[101] = f.Actors[101] with { Position = p + Vector3.UnitX * 21 };
            f.Engine.Synchronize(f.Actors.Values, f.Actors[101].Position, f.Now, playerId: 101);
            Check(!f.Engine.RequireAllStop, "Perspective leaked stop.");
            f.PlayerId = 100; f.Actors[100] = f.Actors[100] with { Position = p + Vector3.UnitX * 21 }; Scan(f);
            Check(f.Engine.ClyteumStopActivation == null, "Beam did not override status.");
        });
        run("Clyteum earliest tower stable ties next after caster finish double output", () =>
        {
            using var f = Prepare(0x4C3F); var p = f.Engine.Arena!.Position;
            Cast(f, 10, 48882, 9, position: p + Vector3.UnitX * 5);
            Cast(f, 11, 48882, 6, position: p - Vector3.UnitX * 5);
            Cast(f, 12, 48882, 6, position: p + Vector3.UnitZ * 5);
            Check(Group(f, "tower").Single(a => a.Display).Position == p - Vector3.UnitX * 5
                && f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Earliest tower.");
            Finish(f, 11); Check(Group(f, "tower").Single(a => a.Display).Position == p + Vector3.UnitZ * 5, "Tie order.");
            Finish(f, 12); Check(Group(f, "tower").Single(a => a.Display).Position == p + Vector3.UnitX * 5, "Final tower.");
            Finish(f, 10); Check(!Group(f, "tower").Any(), "Final clear.");
        });
        run("Clyteum icon spread source fixed five seconds not helper cast target correction death", () =>
        {
            using var f = Prepare(0x4C3F);
            Cast(f, 10, 48885, target: 100); Check(!Group(f, "spread").Any(), "Cast created icon spread.");
            f.Engine.Icon(101, 558, f.Now.AddSeconds(-1), 100);
            var a = Group(f, "spread").Single(a => a.Display);
            Check(((CircleTemplate)a.Shape).Radius == 4 && a.Activation == f.Now.AddSeconds(4), "Icon target/activation.");
            f.Engine.Effect(10, 48885, 1, f.Now, target: 101); Check(!Group(f, "spread").Any(), "Fallback consumption.");
            f.Engine.Icon(100, 558, f.Now, 100);
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f); Check(!Group(f, "spread").Any(), "Dead icon.");
        });
        foreach (var (boss, action, radius) in new (uint, uint, float)[] { (0x4C2C, 48899, 5), (0x4C28, 50314, 6), (0x4C28, 48928, 5) })
            run($"Clyteum {action} exact spread radius AI extra one and resurrection", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action, target: 101);
                Check(((CircleTemplate)Group(f, "spread").Single(a => a.Display).Shape).Radius == radius
                    && ((CircleTemplate)Group(f, "spread").Single(a => a.Navigate).Shape).Radius == radius + 1, "Radius.");
                f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Check(!Group(f, "spread").Any(), "Dead spread.");
                f.Actors[101] = f.Actors[101] with { Dead = false }; Scan(f); Check(Group(f, "spread").Any(), "Resurrection.");
                Finish(f, 10); Check(!Group(f, "spread").Any(), "Finish.");
            });
        run("Clyteum eight yard knockback inverted seven nav immune and sequential display", () =>
        {
            using var f = Prepare(0x4C3F); var p = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = p + Vector3.UnitX * 10 }; Scan(f); Cast(f, 10, 48878);
            var a = Group(f, "kb/avoid").Single(); var g = a.Shape.Resolve(a.Position, 0, null);
            Check(!g.Contains(p + Vector3.UnitX * 6.9f, 0) && g.Contains(p + Vector3.UnitX * 7.1f, 0), "Inverted seven.");
            Check(Group(f, "kb").Any(a => a.Display), "Preview.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; Scan(f);
            Check(!Group(f, "kb").Any(), "Immune.");
            Finish(f, 10); Check(!Group(f, "kb").Any(), "Clear.");
        });
        run("Clyteum StringUp source OID first observation clock movement threshold rolling anchor and release", () =>
        {
            using var f = Prepare(0x4C28); var p = f.Engine.Arena!.Position;
            Cast(f, 10, 48931); f.Now = f.Now.AddSeconds(4.1); f.Engine.Tick(f.Now);
            Check(!f.Engine.RequireClyteumMovement, "Helper created move.");
            var c = new DungeonCastObservation(48931, p, 0, f.Now.AddSeconds(-4.1), f.Now.AddSeconds(.9));
            f.Actors[1] = f.Actors[1] with { Cast = c }; f.Engine.StartCast(1, c, f.Now); f.Engine.Tick(f.Now); f.Render();
            Check(!f.Engine.RequireClyteumMovement, "Restored cast incorrectly used original start clock.");
            f.Now = f.Now.AddSeconds(4.1); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.RequireClyteumMovement && !f.Engine.RequireAllStop && Group(f, "move").Length == 1, "Observation clock / free move.");
            var anchor = Group(f, "move").Single().Position;
            f.Actors[100] = f.Actors[100] with { Position = p + Vector3.UnitX * 1.2f }; Scan(f);
            Check(Group(f, "move").Single().Position != anchor, "Rolling anchor.");
            Finish(f, 1); Check(!f.Engine.RequireClyteumMovement && !Group(f, "move").Any(), "Finish release.");
        });
        run("Clyteum status snapshot refresh expiry postpones hold and explicit event waits matching acknowledgement", () =>
        {
            using var f = Prepare(0x4C2C);
            f.Actors[100] = f.Actors[100] with { Statuses = [5191], StatusExpirations = new Dictionary<uint, DateTime> { [5191] = f.Now.AddSeconds(10) } }; Scan(f);
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [5191] = f.Now.AddSeconds(20) } }; Scan(f);
            f.Now = f.Now.AddSeconds(9.1); f.Engine.Tick(f.Now); Check(!f.Engine.RequireAllStop, "Stale expiration.");
            f.Engine.StatusChanged(100, 5191, true, f.Now, expiresAt: f.Now.AddSeconds(30)); Scan(f);
            Check(f.Engine.ClyteumStopActivation == f.Now.AddSeconds(30), "Stale snapshot overwrote packet.");
        });
        run("Clyteum StringUp live party slots survive replacements then finish and exclude dead at start", () =>
        {
            using var f = Prepare(0x4C28); var p = f.Engine.Arena!.Position;
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Cast(f, 1, 48931);
            f.Now = f.Now.AddSeconds(4.1); f.Actors[101] = f.Actors[101] with { Dead = false };
            f.PlayerId = 101; Scan(f); Check(!f.Engine.RequireClyteumMovement, "Dead-at-start got move.");
            f.Actors.Remove(100); f.Actors[201] = new(201, 0, p, PartySlot: 0, Hostile: false); f.PlayerId = 201; Scan(f);
            Check(f.Engine.RequireClyteumMovement, "Slot replacement lost move.");
            Finish(f, 1);
            f.Actors.Remove(201); f.Actors[100] = new(100, 0, p, PartySlot: 0, Hostile: false); f.PlayerId = 100; Scan(f);
            Check(!f.Engine.RequireClyteumMovement, "Returning member retained finished move.");
        });
        run("Clyteum independent Malphas spreads preserve own seven yard separation and no cast damage forecast", () =>
        {
            using var f = Prepare(0x4C28); var p = f.Engine.Arena!.Position;
            f.Actors[101] = f.Actors[101] with { Position = p + Vector3.UnitX * 6.5f }; Scan(f);
            Cast(f, 10, 50314, target: 100); Cast(f, 11, 48928, target: 101);
            Check(Group(f, "spread").Any(a => a.Id.EndsWith("/separate/101") && a.Shape.Resolve(a.Position, 0, null).Contains(p, 0)),
                "Different component excluded buddy.");
            Check(!f.Engine.PredictedDamage.Any(), "Cast spread invented prediction.");
        });
        run("Clyteum next tower predicts only players inside current tower", () =>
        {
            using var f = Prepare(0x4C3F); var p = f.Engine.Arena!.Position;
            Cast(f, 10, 48882, 6); Cast(f, 11, 48882, 8, position: p + Vector3.UnitX * 10);
            f.Actors[101] = f.Actors[101] with { Position = p + Vector3.UnitX * 10 }; Scan(f);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100 && f.Engine.PredictedDamage.Single().DamageType == "Raidwide", "Tower occupants.");
            Finish(f, 10); Check(f.Engine.PredictedDamage.Single().TargetId == 101, "Next tower occupants.");
        });
        run("Clyteum movement and tower constraints produce actual safe paths", () =>
        {
            foreach (var boss in new uint[] { 0x4C3F, 0x4C28 })
            {
                using var f = Prepare(boss); var p = f.Engine.Arena!.Position;
                if (boss == 0x4C3F) Cast(f, 10, 48882, 8, position: p + Vector3.UnitX * 9);
                else { Cast(f, 1, 48931); f.Now = f.Now.AddSeconds(4.1); f.Engine.Tick(f.Now); f.Render(); }
                var snap = f.Snapshot;
                var solution = new SafeZoneSolver().Solve(snap, new(p, 0, 6, false, false, 0, true, snap.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
                Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No route.");
                var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
                Check(path.Count >= 2 && Vector3.Distance(path[^1].Position, p) > .8f, "Stationary route.");
            }
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1345); var p = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = p };
        f.Actors[100] = new(100, 0, p, PartySlot: 0, PartyRole: "Tank", Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitX * 4, PartySlot: 1, PartyRole: "Healer", Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double seconds = 5, ulong target = 0, Vector3? position = null)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        Scan(f); var c = new DungeonCastObservation(action, position ?? f.Engine.Arena!.Position, .3f, f.Now, f.Now.AddSeconds(seconds), target);
        f.Actors[id] = f.Actors[id] with { Cast = c }; f.Engine.StartCast(id, c, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id)
    {
        var action = f.Actors[id].Cast!.Action; f.Actors[id] = f.Actors[id] with { Cast = null };
        f.Engine.Finish(id, action, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string group) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("cly/" + group)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
