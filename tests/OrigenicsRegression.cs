using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class OrigenicsRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Origenics discovery client territory native arenas both views", () =>
        {
            Check(ArrDungeonCatalog.Create(1208).Length == 4 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.origenics"), "Discovery.");
            var asm = typeof(BossMod.Dawntrail.Dungeon.D05Origenics.D051Herpekaris.D051Herpekaris).Assembly;
            var original = (BossMod.ArenaBoundsCustom)asm.GetType("BossMod.Dawntrail.Dungeon.D05Origenics.D050OrigenicsAerostat.D050OrigenicsAerostat")!
                .GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            Compare(OrigenicsArenas.Aerostat(0), p => original.Contains(new(p.X - original.Center.X, p.Z - original.Center.Z)), 85, .91f);
            var map = (Dictionary<byte, BossMod.ArenaBoundsCustom>)asm.GetType("BossMod.Dawntrail.Dungeon.D05Origenics.D052Deceiver.ArenaChanges")!
                .GetField("ArenaBoundsMap")!.GetValue(null)!;
            using var f = Prepare(0x4170);
            foreach (var (index, arena) in map)
            {
                f.Engine.MapEffect(index, 0x00020001, f.Now);
                Compare(f.Engine.Arena!, p => arena.Contains(new(p.X + 172, p.Z + 142)), 26, .43f);
            }
        });
        run("Origenics trash retains all members until all dead or destroyed", () =>
        {
            using var f = Prepare(0x41E2); var c = f.Engine.Arena!.Position;
            f.Actors[2] = new(2, 0x43D7, c); Scan(f);
            f.Actors[1] = f.Actors[1] with { Dead = true, InCombat = false }; Scan(f);
            Check(f.Engine.PrimaryId == 2, "Primary death ended group.");
            f.Engine.RemoveActor(1); f.Actors.Remove(1);
            f.Actors[2] = f.Actors[2] with { Position = c + Vector3.UnitX * 200, Targetable = false }; Scan(f);
            Check(f.Engine.Active != null, "Living member outside arena ended group.");
            f.Actors[2] = f.Actors[2] with { Dead = true }; Scan(f); Check(f.Engine.Active == null, "All dead.");
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x41E2, 38328, new BossMod.AOEShapeDonut(3, 12)),
            (0x41E2, 35428, new BossMod.AOEShapeCircle(5)),
            (0x4185, 38518, new BossMod.AOEShapeCircle(6)),
            (0x4185, 38519, new BossMod.AOEShapeCircle(6)),
            (0x4170, 36370, new BossMod.AOEShapeCone(30, new(MathF.PI / 4))),
            (0x4170, 36372, new BossMod.AOEShapeRect(40, 2)),
            (0x4170, 36365, new BossMod.AOEShapeRect(4, 5)),
            (0x4170, 36366, new BossMod.AOEShapeRect(40, 5)),
            (0x417D, 36428, new BossMod.AOEShapeRect(70, 6.5f)),
            (0x417D, 36429, new BossMod.AOEShapeCircle(22)),
        })
            run($"Origenics {action} native source geometry both views real finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var aoe = Group(f, "simple").Single();
                Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null),
                    p => shape.Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation)));
                f.Now = f.Now.AddSeconds(40); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "simple").Length == 1, "Timeout cleared cast.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Finish retained cast.");
            });
        run("Origenics fake android turrets lashes and NPC surge produce no player AOEs", () =>
        {
            using var f = Prepare(0x4170);
            foreach (var action in new uint[] { 36373, 36426, 38807, 39736 })
            {
                Cast(f, 10, action); Check(!f.Engine.AmbientAoes().Any() && !f.Engine.OutputCasts.Any(), $"Fake {action} active.");
                Finish(f, 10); f.Now = f.Now.AddSeconds(1);
            }
        });
        run("Origenics riot event clock FIFO risk mutation close center and telegraph cleanup", () =>
        {
            using var f = Prepare(0x4185); var start = f.Now;
            Cast(f, 10, 36465, 2); f.Now = f.Now.AddSeconds(1); Cast(f, 11, 36466, 20);
            f.Now = f.Now.AddSeconds(1); Cast(f, 12, 36467, 1);
            var a = Group(f, "cast"); Check(a.Length == 2 && Near(a[0].Activation, start, 7.3) && Near(a[1].Activation, start, 7.3), "Riot event clock.");
            Check(a[0].Navigate && !a[1].Navigate && a[0].Color?.Y == 1, "Riot shared shape risk.");
            Compare(a[0].Shape.Resolve(a[0].Position, a[0].Rotation, null), p => new BossMod.AOEShapeCone(25, new(MathF.PI * 7 / 12))
                .Check(new(p.X, p.Z), new(a[0].Position.X, a[0].Position.Z), new(a[0].Rotation)), 28);
            Check(Group(f, "riot/close").Single().Activation == a[0].Activation, "Close-center time.");
            Finish(f, 10); f.Engine.RemoveActor(11); f.Actors.Remove(11); f.Render();
            Check(Group(f, "cast").Length == 2, "Telegraph end consumed sequence.");
            f.Engine.Effect(1, 36469, 1, f.Now); f.Render(); a = Group(f, "cast");
            Check(a.Length == 2 && a[0].Navigate && a[1].Navigate, "FIFO promotion risk.");
            f.Engine.Effect(1, 36470, 2, f.Now); f.Render();
            Check(Group(f, "cast").Length == 1 && Group(f, "riot/close").Length == 0, "Final sequence.");
            f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now); f.Render(); Check(Group(f, "cast").Length == 1, "Expired queue.");
            f.Engine.Effect(1, 36471, 3, f.Now); Check(Group(f, "cast").Length == 0, "Final hit.");
        });
        run("Origenics poison dynamic cast event prediction object transition one per update", () =>
        {
            using var f = Prepare(0x4185); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36460, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 4 }; Scan(f);
            Check(Group(f, "simple").Single().Position == f.Actors[101].Position, "Cast target not dynamic.");
            f.Actors[10] = f.Actors[10] with { Cast = f.Actors[10].Cast! with { TargetId = 0, Location = c + Vector3.UnitZ * 6 } }; Scan(f);
            Check(Group(f, "simple").Single().Position == c + Vector3.UnitZ * 6, "Current cast fallback.");
            Finish(f, 10);
            f.Engine.Effect(10, 36460, 1, f.Now, 101, c); f.Engine.Effect(11, 36460, 2, f.Now, 101, c);
            Check(Group(f, "poison").Length == 2 && Group(f, "poison").All(a => Near(a.Activation, f.Now, .9)), "Event activation.");
            f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render(); Check(Group(f, "poison").Length == 2, "Prediction timeout.");
            f.Actors[20] = new(20, 0x1E9E3C, f.Actors[101].Position, Dead: true, Targetable: false); Scan(f);
            Check(Group(f, "poison").Length == 1 && Group(f, "zone").Length == 1, "Object replace not one per update/dead discarded.");
            f.Render(); Check(Group(f, "poison").Length == 1, "Render consumed prediction twice.");
            Scan(f); Check(Group(f, "poison").Length == 0, "Second update.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(Group(f, "zone").Length == 0, "Inactive zone.");
        });
        run("Origenics line markers duplicates source target loss death and actual correction", () =>
        {
            using var f = Prepare(0x4185); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c + Vector3.UnitZ * 8); Scan(f);
            f.Engine.Effect(10, 36474, 1, f.Now, 100); f.Engine.Effect(10, 36474, 2, f.Now, 100); f.Render();
            Check(Group(f, "line").Count(a => a.Display) == 2 && f.Engine.PredictedDamage.All(a => Near(a.Activation, f.Now, 5.6)), "Marker duplicates/time.");
            Cast(f, 1, 36473); Finish(f, 1); Check(Group(f, "line").Count(a => a.Display) == 2, "CastFinish consumed line.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(Group(f, "line").Count(a => a.Display) == 2, "Target death cleared marker.");
            f.Engine.Effect(1, 36473, 3, f.Now, 100); Check(Group(f, "line").Count(a => a.Display) == 1, "One hit consumed wrong number.");
            f.Engine.Effect(1, 36473, 4, f.Now, 101); Check(Group(f, "line").Length == 0, "Last-target correction.");
        });
        run("Origenics delayed busters retained after finish resolve per target", () =>
        {
            using var f = Prepare(0x4185);
            Cast(f, 1, 36518, target: 100); Finish(f, 1); f.Now = f.Now.AddSeconds(1);
            Cast(f, 1, 36518, target: 101); Finish(f, 1);
            Check(f.Engine.PredictedDamage.Count() == 2, "Delayed buster overwritten.");
            f.Engine.Effect(1, 36518, 1, f.Now, 100); Check(f.Engine.PredictedDamage.Single().TargetId == 101, "Buster wrong target.");
        });
        foreach (var (boss, action, index, outerX, outerZ, innerX, innerZ) in new (uint, uint, uint, float, float, float, float)[]
            { (0x4170, 36371, 0x12, 25, 25, 20, 20), (0x417D, 36436, 0x28, 33, 24, 15, 19.5f) })
            run($"Origenics {action} shrink preview independent map confirmation", () =>
            {
                using var f = Prepare(boss); Cast(f, 1, action);
                var a = Group(f, "shrink").Single(); Check(Near(a.Activation, f.Now, 5.7), "Shrink time.");
                Compare(a.Shape.Resolve(a.Position, 0, null), p => MathF.Abs(p.X - a.Position.X) <= outerX && MathF.Abs(p.Z - a.Position.Z) <= outerZ
                    && !(MathF.Abs(p.X - a.Position.X) <= innerX && MathF.Abs(p.Z - a.Position.Z) <= innerZ), 35, .41f);
                Finish(f, 1); Check(Group(f, "shrink").Length == 1, "Finish cleared pending shrink.");
                if (boss == 0x4170)
                {
                    f.Engine.MapEffect(0x1B, 0x00020001, f.Now); f.Engine.MapEffect(0x99, 0x00080004, f.Now);
                    Check(Group(f, "shrink").Length == 1, "Other map cleared preview.");
                }
                f.Engine.MapEffect(index, 0x00020001, f.Now); Check(Group(f, "shrink").Length == 0, "Confirm retained preview.");
            });
        foreach (var map in new uint[] { 0x1B, 0x1E, 0x1D, 0x1C })
            run($"Origenics Surge {map:X} snapshotted hints current map navigation and walls", () =>
            {
                using var f = Prepare(0x4170); f.Engine.MapEffect(map, 0x00020001, f.Now); Cast(f, 1, 36367, rotation: 0);
                var hints = Group(f, "wall"); Check(hints.Length == 4 && hints.All(a => a.Safe && !a.Navigate), "Safe hints.");
                var nav = Group(f, "kb/nav").Single().Shape.Resolve(Vector3.Zero, 0, null);
                var origins = hints.Select(h => h.Position).ToArray();
                Compare(nav, p => !origins.Select((o, i) => new BossMod.SDInvertedRect(new(o.X, o.Z),
                    new BossMod.WDir(hints[i].Rotation < 0 ? -4 : 4, 0), 10, 0, 20)).Any(s => s.Distance(new(p.X, p.Z)) > 0), 26, .37f);
                var landing = (Vector3)typeof(DungeonEncounter).GetMethod("OrigenicsLanding", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(f.Engine, [new Vector3(-172, 0, origins[0].Z), -MathF.PI / 2, 30f,
                        new Vector2[] { new(-187.5f, origins[0].Z + 5), new(-187.5f, origins[0].Z - 5) }, .5f])!;
                Check(MathF.Abs(landing.X - (-187.5f + .5f - .499f)) < .0001f, "Safe-wall inset.");
                f.Engine.MapEffect(0x99, 0x00080004, f.Now); f.Render();
                Check(Group(f, "wall").Length == 4 && Group(f, "kb/nav").Length == 0, "AI used locked map.");
                Finish(f, 1); Check(Group(f, "wall").Length == 0 && Group(f, "kb").Length == 0, "Finish clear.");
            });
        run("Origenics Surge missing map handles source array overrun and missing rotation", () =>
        {
            using var f = Prepare(0x4170); Cast(f, 1, 36367);
            Check(Group(f, "wall").Length == 0 && Group(f, "kb").Length > 0, "Missing walls should retain unbounded preview.");
            Finish(f, 1); f.Now = f.Now.AddSeconds(1);
            f.Engine.StartCast(1, new(36367, f.Engine.Arena!.Position, null, f.Now, f.Now.AddSeconds(5)), f.Now);
            Check(!f.Engine.Casts.Any(), "Missing directional rotation accepted.");
        });
        run("Origenics Ambrose charge display reversed AI and max one applied knockback", () =>
        {
            using var f = Prepare(0x417D); var c = f.Engine.Arena!.Position;
            f.Engine.MapEffect(0x28, 0x00020001, f.Now);
            Cast(f, 1, 39233, rotation: MathF.PI);
            Check(Group(f, "charge").Single().Id == "origenics/charge", "Normal cone.");
            Cast(f, 10, 36433, rotation: 0, location: c - Vector3.UnitZ * 19);
            Cast(f, 11, 36434, rotation: MathF.PI, location: c + Vector3.UnitZ * 19);
            var aoes = Group(f, "charge");
            Check(aoes.Length == 2 && aoes.Single(a => a.Display).Safe && !aoes.Single(a => a.Display).Navigate, "Safe display mode.");
            var reversed = aoes.Single(a => !a.Display);
            Check(MathF.Abs(reversed.Rotation - MathF.PI * 2) < .0001, "Reverse cone nav.");
            Check(Group(f, "kb").Count(a => a.Display && a.Id.EndsWith("/landing")) <= 1, "MaxCasts one.");
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
            Check(Group(f, "kb").All(a => !a.Display) && Group(f, "kb/nav").Length == 1, "Immunity removed custom AI or retained preview.");
            Finish(f, 11); Check(Group(f, "kb").Length == 0 && Group(f, "charge").Single().Id == "origenics/charge", "Any KB finish clears all.");
        });
        run("Origenics Ambrose EW only retains full forbidden source intersection", () =>
        {
            using var f = Prepare(0x417D); Cast(f, 10, 36434);
            var nav = Group(f, "kb/nav").Single().Shape.Resolve(Vector3.Zero, 0, null);
            Check(nav.Contains(f.Engine.Arena!.Position) && nav.Contains(f.Engine.Arena.Position + Vector3.UnitX * 10), "Empty intersection.");
        });
        run("Origenics packet correction updates existing knockbacks and rush without ordinary damage rectangles", () =>
        {
            using (var f = Prepare(0x417D))
            {
                var c = f.Engine.Arena!.Position; Cast(f, 10, 36433, rotation: 0, location: c);
                var corrected = f.Actors[10].Cast! with { Location = c + Vector3.UnitX * 4, Rotation = MathF.PI / 2 };
                f.Engine.StartCast(10, corrected, f.Now); f.Render();
                Check(Group(f, "simple").Length == 0 && Group(f, "kb/nav").Length == 1, "Corrected KB fell into ordinary damage.");
                var safe = Group(f, "charge/safe").Single();
                Check(safe.Position == corrected.Location && safe.Rotation == corrected.Rotation, "Corrected KB kept stale origin/rotation.");
                Cast(f, 11, 38953, location: c + Vector3.UnitZ * 5);
                var rush = f.Actors[11].Cast! with { Location = c + Vector3.UnitX * 12 };
                f.Engine.StartCast(11, rush, f.Now); f.Render();
                Check(Group(f, "rush").Length == 1 && MathF.Abs(Group(f, "rush").Single().Rotation - MathF.PI / 2) < .00001f
                    && ((RayRectangleTemplate)Group(f, "rush").Single().Shape).Length == 12, "Rush correction duplicated or stale.");
            }
            using (var f = Prepare(0x4170))
            {
                f.Engine.MapEffect(0x1B, 0x00020001, f.Now); Cast(f, 1, 36367, rotation: 0);
                f.Engine.StartCast(1, f.Actors[1].Cast! with { Rotation = MathF.PI / 2 }, f.Now); f.Render();
                Check(Group(f, "wall").Length == 4 && Group(f, "simple").Length == 0, "Surge correction duplicated or fell through.");
                var landing = Group(f, "kb").First(a => a.Id.EndsWith("/landing"));
                Check(MathF.Abs(landing.Position.Z - f.Actors[100].Position.Z) > 1, "Surge correction ignored direction.");
            }
        });
        run("Origenics Ambrose pending effect confirmation timeout perspective and no renewal", () =>
        {
            using var f = Prepare(0x417D); Cast(f, 1, 39233);
            f.Actors[100] = f.Actors[100] with { IncomingEffects = [new ReplayIncomingEffect(0, 1, 0, false, true)] }; Scan(f);
            Check(Group(f, "charge").Length == 0, "Pending not hiding cone.");
            f.PlayerId = 101; Scan(f); Check(Group(f, "charge").Length == 1, "Perspective pending leak.");
            f.PlayerId = 100; f.Now = f.Now.AddSeconds(2); Scan(f); Check(Group(f, "charge").Length == 0, "Pending early expiry.");
            f.Now = f.Now.AddSeconds(1.01); Scan(f); Check(Group(f, "charge").Length == 1, "Unchanged slot renewed timeout.");
            f.Actors[100] = f.Actors[100] with { IncomingEffects = [new ReplayIncomingEffect(0, 2, 0, false, true)] }; Scan(f);
            Check(Group(f, "charge").Length == 0, "New sequence ignored.");
            f.Actors[100] = f.Actors[100] with { IncomingEffects = [new ReplayIncomingEffect(0, 2, 0, true, true)] }; Scan(f);
            Check(Group(f, "charge").Length == 0, "Target confirmation must retain pending knockback.");
            f.Actors[100] = f.Actors[100] with { IncomingEffects = [] }; Scan(f);
            Check(Group(f, "charge").Length == 1, "Slot removal did not clear pending.");
        });
        run("Origenics Rush FIFO fixed eighth current count finish lifetime both view geometry", () =>
        {
            using var f = Prepare(0x417D); var c = f.Engine.Arena!.Position;
            for (var i = 0; i < 8; ++i) Cast(f, (ulong)(10 + i), 38953, location: c + Vector3.UnitZ * 12);
            var a = Group(f, "rush"); Check(a.Length == 8 && a.All(x => x.Navigate) && Near(a[0].Activation, f.Now, 11.8), "Rush count/time.");
            Check(a[7].Position == new Vector3(190, 0, 19.5f) && a[7].Rotation == -MathF.PI, "Eighth fixed shape.");
            Compare(a[7].Shape.Resolve(a[7].Position, a[7].Rotation, null),
                p => new BossMod.AOEShapeRect(33, 5).Check(new(p.X, p.Z), new(190, 19.5f), new(-MathF.PI)), 36);
            Finish(f, 10); Check(Group(f, "rush").Length == 8, "Telegraph finish consumed rush.");
            f.Engine.Effect(20, 38954, 1, f.Now); f.Engine.Effect(20, 36431, 2, f.Now); f.Render();
            Check(Group(f, "rush").Length == 6, "Real hits FIFO.");
            Cast(f, 30, 38953, location: c + Vector3.UnitZ * 8);
            Check(Group(f, "rush").Last().Position == f.Actors[30].Position, "Rule used cumulative count.");
        });
        foreach (var boss in new uint[] { 0x4185, 0x4170, 0x417D })
            run($"Origenics {boss:X} active boss survives untargetable disengaged then resets death", () =>
            {
                using var f = Prepare(boss);
                f.Actors[1] = f.Actors[1] with { Targetable = false, InCombat = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Transient combat flag reset boss.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f); Check(f.Engine.Active == null, "Dead boss retained session.");
            });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1208); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double duration = 5, float rotation = .7f, Vector3? location = null, ulong target = 0)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, location ?? f.Actors[id].Position, rotation, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[id] = f.Actors[id] with { Cast = cast }; Scan(f); f.Engine.StartCast(id, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id)
    {
        f.Actors[id] = f.Actors[id] with { Cast = null }; f.Engine.Finish(id, endedAt: f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("origenics/" + key)).ToArray();
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } value && Math.Abs((value - start).TotalSeconds - seconds) < .00001;
    private static void Compare(IGeometryShape native, Func<Vector3, bool> expected, float range = 55, float step = 1.17f)
    {
        var world = HostDrawingGeometry.Convert(native);
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z); var wanted = expected(p);
                Check(native.Contains(p, 0) == wanted && world.Contains(p, 0) == wanted, $"Shape at {p}.");
            }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
