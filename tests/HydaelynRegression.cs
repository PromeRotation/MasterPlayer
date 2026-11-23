using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class HydaelynRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Hydaelyn native identity exact polygon and primary death cleanup", () =>
        {
            using var f = Scene();
            Check(f.Engine.Active?.BossOid == 0x34EA && ArrDungeonCatalog.IsScript("masterplayer.trial.hydaelyn"), "Identity missing.");
            var angle = MathF.PI / 48;
            Check(f.Engine.Arena is LocalPolygonGeometry && f.Engine.Arena.Contains(new(100, 0, 119.99f))
                && !f.Engine.Arena.Contains(new(100 + 19.99f * MathF.Sin(angle), 0, 100 + 19.99f * MathF.Cos(angle))),
                "48-gon replaced by circle.");
            f.Engine.StatusChanged(1, 2273, true, f.Now, 0x1B4); f.Render();
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.ArenaBoundary == null, "Final death leaked.");
        });
        run("Hydaelyn parhelic actor creation retains ten circles and independent clocks", () =>
        {
            using var f = Scene(); Add(f, 30, 0x1EB24C, rotation: .31f);
            Check(Group(f, "parhelic").Length == 7 && Group(f, "parhelic").All(a => a.Activation == f.Start.AddSeconds(7.6)), "Hexagon differs.");
            At(f, 1); Add(f, 31, 0x1EB24D, rotation: -.4f);
            var circles = Group(f, "parhelic");
            Check(circles.Length == 10 && circles.Skip(7).All(a => a.Activation == f.Start.AddSeconds(8.6))
                && circles.All(a => a.Shape is CircleTemplate { Radius: 6 }), "Triangle independent clock/radius differs.");
            var c = new BossMod.WPos(100, 100);
            var dir = (c + 17 * new BossMod.Angle(.31f).ToDirection()).Quantized();
            for (var i = 1; i <= 6; ++i)
            {
                var expected = BossMod.WPos.RotateAroundOrigin(i * 60, c, dir);
                Check(Vector2.Distance(new(circles[i].Position.X, circles[i].Position.Z), new(expected.X, expected.Z)) < .00001f,
                    "Quantize-before-rotate geometry changed.");
            }
            f.Actors[30] = f.Actors[30] with { Rotation = 1 }; f.Scan();
            Check(Group(f, "parhelic")[1] == circles[1], "Object motion rebuilt creation snapshot.");
        });
        run("Hydaelyn parhelic ignores time effects destruction but any incandescence finish clears all", () =>
        {
            using var f = Scene(); Add(f, 30, 0x1EB24C); Add(f, 31, 0x1EB24D);
            f.Engine.Effect(10, 26061, 1, f.Now); f.Engine.RemoveActor(30); f.Actors.Remove(30); At(f, 100);
            Check(Group(f, "parhelic").Length == 10, "Prediction was consumed without finish.");
            Start(f, 10, 26061);
            Check(!f.Draw.Previews.ContainsKey("cast/10"), "Disabled ordinary Incandescence component was invented.");
            End(f, 10, 26061); Check(!Group(f, "parhelic").Any(), "Incandescence finish failed full clear.");
        });
        run("Hydaelyn weapon gains overwrite single AOE and unknown extra keeps prior output", () =>
        {
            using var f = Scene(); f.Engine.StatusChanged(1, 2273, true, f.Now, 0x1B4); f.Render();
            Check(Group(f, "weapon").Single() is { Shape: CircleTemplate { Radius: 10 }, Activation: var at }
                && at == f.Start.AddSeconds(6), "Staff gain differs.");
            At(f, 2); f.Engine.StatusChanged(10, 2273, true, f.Now, 0x1B5); f.Render();
            Check(Group(f, "weapon").Single() is { Shape: DonutTemplate { InnerRadius: 5, OuterRadius: 40 }, Activation: var dt }
                && dt == f.Start.AddSeconds(8), "Chakram gain differs.");
            f.Engine.StatusChanged(10, 2273, true, f.Now.AddSeconds(.1), 0); f.Render();
            Check(Group(f, "weapon").Single().Activation == f.Start.AddSeconds(8), "Unknown Extra erased/restarted prior AOE.");
        });
        run("Hydaelyn weapon loss snapshots actor position and rotation and only watched effects clear", () =>
        {
            using var f = Scene(); f.Actors[10] = f.Actors[10] with { Position = new(90, 0, 98), Rotation = .7f }; f.Scan();
            f.Engine.StatusChanged(10, 2273, false, f.Now); f.Render();
            var cross = Group(f, "weapon").Single();
            Check(cross.Shape is DungeonCrossTemplate { Length: 40, HalfWidth: 5 } && cross.Rotation == .7f
                && cross.Activation == f.Start.AddSeconds(6.9), "Cross loss snapshot differs.");
            f.Actors[10] = f.Actors[10] with { Position = new(110, 0, 101), Rotation = 0 }; At(f, 100);
            Check(Group(f, "weapon").Single() == cross, "Cross followed actor or timed out.");
            f.Engine.Effect(10, 26051, 1, f.Now); f.Render(); Check(Group(f, "weapon").Any(), "Equinox visual cleared cross.");
            foreach (var action in new[] { 26255u, 26055u, 26056u })
            {
                f.Engine.StatusChanged(1, 2273, true, f.Now, 0x1B4);
                f.Engine.Effect(11, action, action, f.Now); f.Render(); Check(!Group(f, "weapon").Any(), "Weapon hit clear differs.");
            }
        });
        run("Hydaelyn snapshot weapon status gain threshold and loss do not duplicate explicit callbacks", () =>
        {
            using var f = Scene(); var expiry = f.Start.AddSeconds(20);
            f.Actors[1] = f.Actors[1] with { Statuses = [2273], StatusParams = new Dictionary<uint, ushort> { [2273] = 0x1B4 },
                StatusExpirations = new Dictionary<uint, DateTime> { [2273] = expiry } }; f.Scan();
            Check(Group(f, "weapon").Single().Activation == f.Start.AddSeconds(6), "Initial snapshot gain missing.");
            At(f, 1);
            f.Actors[1] = f.Actors[1] with { StatusExpirations = new Dictionary<uint, DateTime> { [2273] = expiry.AddSeconds(.9) } }; f.Scan();
            Check(Group(f, "weapon").Single().Activation == f.Start.AddSeconds(6), "Small expiry shift fired gain.");
            At(f, 2);
            f.Actors[1] = f.Actors[1] with { StatusExpirations = new Dictionary<uint, DateTime> { [2273] = expiry.AddSeconds(2) } }; f.Scan();
            Check(Group(f, "weapon").Single().Activation == f.Start.AddSeconds(8), "Extension >1s failed gain.");
            f.Engine.StatusChanged(1, 2273, false, f.Now); f.Render();
            var cross = Group(f, "weapon").Single(); At(f, 2.2);
            Check(Group(f, "weapon").Single() == cross, "Stale raw snapshot erased explicit loss.");
            f.Actors[1] = f.Actors[1] with { Statuses = [] }; f.Scan(); At(f, 3);
            Check(Group(f, "weapon").Single() == cross, "Converged raw state repeated loss.");
        });
        run("Hydaelyn echoes uses icon actor and exactly fifth hit clears all without changing original activation", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 305, f.Now, 100); At(f, 1);
            f.Engine.Icon(102, 305, f.Now); f.Render();
            Check(Group(f, "stack/26065").Count(a => a.Display) == 2
                && Group(f, "stack/26065").First(a => a.Display).Position.X < 100,
                "Icon used targetID instead of actor.");
            for (uint i = 1; i <= 4; ++i) f.Engine.Effect(10, 26065, i, f.Now, 107);
            At(f, 20);
            Check(Group(f, "stack/26065").Count(a => a.Display) == 2 && Group(f, "stack/26065").Any(a => a.Activation == f.Start.AddSeconds(5)),
                "Echoes consumed early or advanced activation.");
            f.Engine.Effect(11, 26065, 5, f.Now, 999); f.Render();
            Check(!Group(f, "stack/26065").Any(), "Fifth hit failed global clear.");
        });
        run("Hydaelyn echoes counts empty hits and dead target revives before fifth hit", () =>
        {
            using var f = Scene(); f.Engine.Effect(10, 26065, 1, f.Now); f.Engine.Icon(101, 305, f.Now);
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan(); Check(!Group(f, "stack/26065").Any(), "Dead marker visible.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan(); Check(Group(f, "stack/26065").Any(), "Revival lost retained marker.");
            for (uint i = 2; i <= 5; ++i) f.Engine.Effect(10, 26065, i, f.Now);
            f.Render(); Check(!Group(f, "stack/26065").Any(), "Empty first hit did not count.");
        });
        run("Hydaelyn exodus strictly sixth destruction not death and never restarts after seventh", () =>
        {
            using var f = Scene();
            for (ulong i = 20; i < 27; ++i) Add(f, i, 0x34EC);
            for (ulong i = 20; i < 26; ++i) f.Actors[i] = f.Actors[i] with { Dead = true };
            f.Scan(); Check(!f.Engine.PredictedDamage.Any(), "Crystal deaths triggered Exodus.");
            for (ulong i = 20; i < 25; ++i) { f.Engine.RemoveActor(i); f.Actors.Remove(i); f.Scan(); }
            Check(!f.Engine.PredictedDamage.Any(), "Exodus began before sixth destruction.");
            At(f, 3); f.Engine.RemoveActor(25); f.Actors.Remove(25); f.Scan();
            Check(f.Engine.PredictedDamage.Count() == 8 && f.Engine.PredictedDamage.All(d => Math.Abs((d.Activation - f.Start).TotalSeconds - 10.2) < .000001),
                "Sixth destruction clock/mask differs.");
            At(f, 100); Check(f.Engine.PredictedDamage.Count() == 8, "Exodus elapsed prediction auto-cleared.");
            f.Engine.Effect(10, 27911, 1, f.Now); Check(f.Engine.PredictedDamage.Any(), "Enrage consumed Exodus.");
            f.Engine.Effect(10, 26066, 2, f.Now); f.Engine.RemoveActor(26); f.Actors.Remove(26); f.Scan();
            Check(!f.Engine.PredictedDamage.Any(), "Seventh destruction restarted Exodus.");
        });
        run("Hydaelyn lightwave dynamically follows with rolling clock and generation identity retention", () =>
        {
            using var f = Scene(); f.Engine.Effect(10, 26058, 1, f.Now); f.Engine.Effect(10, 26058, 2, f.Now); f.Render();
            Check(Group(f, "wave").Length == 1, "Same-generation wave duplicated.");
            var g = Group(f, "wave")[0].Shape.Resolve(Group(f, "wave")[0].Position, 0, null);
            Check(g.Contains(new(107.9f, 0, 115.9f)) && g.Contains(new(100, 0, 88.1f))
                && !g.Contains(new(100, 0, 87.9f)), "Wave front16/back12/halfwidth8 differs.");
            f.Actors[10] = f.Actors[10] with { Position = new(105, 0, 100), Rotation = MathF.PI / 2, Dead = true }; At(f, 2);
            var old = Group(f, "wave")[0];
            Check(old.Rotation == MathF.PI / 2 && old.Activation == f.Start.AddSeconds(3.1), "Dead wave stopped updating.");
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Scan(); At(f, 4);
            Check(Group(f, "wave")[0].Position == old.Position, "Destroyed wave lost frozen reference.");
            Add(f, 10, 0x233C, new(90, 0, 90)); f.Engine.Effect(10, 26058, 3, f.Now); f.Render();
            Check(Group(f, "wave").Length == 2 && Group(f, "wave")[0].Position == old.Position, "Reused ID rebound old wave.");
        });
        run("Hydaelyn charge uses quantized caster start and destination rather than rotation range", () =>
        {
            using var f = Scene(); Start(f, 10, 26062, location: new(112, 0, 100), rotation: MathF.PI);
            var a = Group(f, "charge").Single();
            Check(a.Shape is RayRectangleTemplate { Length: 12 } && Math.Abs(a.Rotation - MathF.PI / 2) < .001
                && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(new(111, 0, 102.9f)), "Charge geometry differs.");
            f.Actors[10] = f.Actors[10] with { Position = new(80, 0, 80) }; At(f, 100);
            Check(Group(f, "charge").Single() == a, "Charge snapshot moved or timed out.");
            End(f, 10, 26062); Check(!Group(f, "charge").Any(), "Charge finish leaked.");
        });
        run("Hydaelyn beacon exposes first ten FIFO casters and ray total width thirty", () =>
        {
            using var f = Scene();
            for (ulong i = 30; i < 42; ++i) { Add(f, i, 0x34F1); Start(f, i, 26063, rotation: .4f); }
            Check(Group(f, "beacon").Length == 10 && Group(f, "beacon").All(a => a.Rotation == .4f), "Beacon limit or rotation differs.");
            End(f, 30, 26063);
            Check(Group(f, "beacon").Length == 10 && Group(f, "beacon").Last().Id.EndsWith("/40"), "FIFO did not expose next caster.");
            Start(f, 11, 26060);
            var ray = f.Draw.Previews["cast/11"].Geometry;
            Check(ray.Contains(new(114.9f, 0, 120)) && !ray.Contains(new(115.1f, 0, 120)), "Ray interpreted comment width40.");
        });
        run("Hydaelyn shared tankbuster overwrites one target and predicts all tanks even unresolved target", () =>
        {
            using var f = Scene(); Start(f, 10, 26070, target: 100);
            Check(Group(f, "shared").Single(a => a.Display) is { Safe: false, OutlineOnly: true }
                && f.Engine.PredictedDamage.Select(d => d.TargetId).SequenceEqual([100UL, 101UL]), "Shared tank role mask differs.");
            Start(f, 11, 26070, target: 101); End(f, 10, 26070);
            Check(Group(f, "shared").Any(), "Old source finish cleared new source.");
            Start(f, 11, 26061); f.Render(); Check(!Group(f, "shared").Any(), "Replacement source cast failed end prior shared state.");
            At(f, 1);
            Start(f, 10, 26070, target: 999);
            Check(!Group(f, "shared").Any() && f.Engine.PredictedDamage.Count() == 2, "Unresolved target erased source prediction.");
        });
        run("Hydaelyn shared tank navigation changes with target tank and nontank perspective", () =>
        {
            using var f = Scene(); Start(f, 10, 26070, target: 100);
            Check(Group(f, "shared/buddy").Length == 6, "Target failed avoid non-tanks only.");
            f.PlayerId = 101; f.Scan();
            Check(Group(f, "shared").Single(a => a.Display) is { Safe: true, OutlineOnly: true }
                && Group(f, "shared/join").Length == 1, "Other tank lacks join.");
            f.PlayerId = 102; f.Scan();
            Check(Group(f, "shared").Single(a => a.Display) is { Safe: false, OutlineOnly: false }
                && Group(f, "shared/avoid").Length == 1, "Nontank lacks filled avoid.");
        });
        run("Hydaelyn sundering follows cast source target and preserves dead target until source finish", () =>
        {
            using var f = Scene(); Start(f, 10, 26069, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = new(115, 0, 100), Dead = true }; f.Scan();
            Check(Group(f, "sundering").Single(a => a.Display).Rotation == MathF.PI / 2
                && f.Engine.PredictedDamage.Single().DamageType == "Tankbuster", "Dead target filtered from allow-dead bait.");
            End(f, 10, 26069); Check(!Group(f, "sundering").Any(), "Source finish failed remove.");
        });
        run("Hydaelyn stone ice independent cast targets draw navigation but no invented damage predictions", () =>
        {
            using var f = Scene(); Start(f, 10, 27737, target: 100); Start(f, 11, 27738, target: 101);
            Check(Group(f, "stack/27737").Any() && Group(f, "spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 5 }
                && Group(f, "spread").Single(a => !a.Display).Shape is CircleTemplate { Radius: 6 }
                && !f.Engine.PredictedDamage.Any(), "Cast stack/spread output or prediction differs.");
            f.Engine.Icon(102, 305, f.Now); f.Render();
            Check(!f.Engine.PredictedDamage.Any(), "Echoes emitted source-absent Shared prediction.");
            End(f, 10, 27737); Check(!Group(f, "stack/27737").Any() && Group(f, "spread").Any(), "Stone finish erased independent ice.");
        });
        run("Hydaelyn three raidwides remain cast only and crystals add no target restrictions or control", () =>
        {
            using var f = Scene(); Add(f, 20, 0x34EC);
            foreach (var action in new[] { 26071u, 26072u, 26064u })
            {
                Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 8, "Raidwide mask missing.");
                End(f, 10, action); Check(!f.Engine.PredictedDamage.Any(), "Raidwide leaked.");
            }
            Check(f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.RequireAllStop && !f.Engine.Gazes.Any()
                && Group(f, "enemy").Count() == 2, "Crystal invented controls or absent object drawing.");
        });
        run("Hydaelyn outputs split exact preview and margin navigation and personal death retains fight", () =>
        {
            using var f = Scene(); Add(f, 30, 0x1EB24C); f.Engine.Effect(10, 26058, 1, f.Now); f.Render();
            var circle = Group(f, "parhelic")[0]; var point = circle.Position + Vector3.UnitX * 6.3f;
            Check(!f.Draw.Previews[circle.Id].Geometry.Contains(point) && f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(point)),
                "Exact preview and inflated navigation were conflated.");
            f.Actors[100] = f.Actors[100] with { Dead = true };
            f.Engine.Synchronize(f.Actors.Values, null, f.Now, playerId: 100); f.Render();
            Check(f.Engine.Active != null && Group(f, "wave").Any() && Group(f, "parhelic").Length == 7, "Personal death reset phase.");
            f.Output.Clear(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.ArenaBoundary == null,
                "Output owner cleanup leaked.");
        });
        run("Hydaelyn explicit gain reconciles subsecond expiry drift then observes raw loss", () =>
        {
            using var f = Scene(); var expiry = f.Start.AddSeconds(20);
            f.Engine.StatusChanged(1, 2273, true, f.Now, 0x1B4, expiry);
            f.Actors[1] = f.Actors[1] with { Statuses = [2273], StatusParams = new Dictionary<uint, ushort> { [2273] = 0x1B4 },
                StatusExpirations = new Dictionary<uint, DateTime> { [2273] = expiry.AddSeconds(.1) } }; At(f, .1);
            f.Actors[1] = f.Actors[1] with { Statuses = [] }; At(f, .2);
            Check(Group(f, "weapon").Single().Shape is DungeonCrossTemplate, "Expiry drift permanently blocked loss.");
        });
        run("Hydaelyn pending loss prevents destruction from resurrecting cleared cross", () =>
        {
            using var f = Scene();
            f.Actors[10] = f.Actors[10] with { Statuses = [2273], StatusParams = new Dictionary<uint, ushort> { [2273] = 0x1B4 } }; f.Scan();
            f.Engine.StatusChanged(10, 2273, false, f.Now); f.Engine.Effect(11, 26255, 1, f.Now);
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Scan();
            Check(!Group(f, "weapon").Any(), "Destroyed actor repeated already-observed loss.");
        });
        run("Hydaelyn immediate ID reuse needs no empty world scan to create new wave generation", () =>
        {
            using var f = Scene(); f.Engine.Effect(10, 26058, 1, f.Now);
            f.Engine.RemoveActor(10);
            f.Actors[10] = new(10, 0x34F1, new(90, 0, 90)); f.Scan();
            f.Engine.Effect(10, 26058, 2, f.Now); f.Render();
            Check(Group(f, "wave").Length == 2 && Group(f, "wave")[0].Position.X > 99
                && Group(f, "wave")[1].Position.X < 91, "New generation excluded without empty scan.");
        });
        run("Hydaelyn source-only status change emits lose gain and unknown extra retains resulting cross", () =>
        {
            using var f = Scene();
            f.Actors[1] = f.Actors[1] with { Statuses = [2273], StatusParams = new Dictionary<uint, ushort> { [2273] = 0 },
                StatusSources = new Dictionary<uint, ulong> { [2273] = 100 } }; f.Scan();
            Check(!Group(f, "weapon").Any(), "Unknown gain invented AOE.");
            f.Actors[1] = f.Actors[1] with { StatusSources = new Dictionary<uint, ulong> { [2273] = 101 } }; At(f, 1);
            Check(Group(f, "weapon").Single().Shape is DungeonCrossTemplate, "Source-only status transition lost Lose.");
            Start(f, 10, 26063, location: new(101.1234f, 0, 102.5678f));
            Check(Group(f, "beacon").Single().Position == new Vector3(101.1234f, 0, 102.5678f), "Beacon cast origin was additionally quantized.");
        });
        run("Hydaelyn stack hints preserve count and overlapping target checks", () =>
        {
            using var f = Scene(); Start(f, 10, 27737, target: 100);
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("人数不足")), "Missing source count hint.");
            f.Engine.Icon(100, 305, f.Now); f.Engine.Icon(101, 305, f.Now);
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position }; f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("点名重叠")), "Missing overlapping stack target hint.");
        });
        run("Hydaelyn stale finish cannot clear current shared tankbuster state", () =>
        {
            using var f = Scene(); At(f, 10); Start(f, 10, 26070, target: 100);
            f.Engine.Finish(10, 26070, f.Start.AddSeconds(9)); f.Render();
            Check(Group(f, "shared").Any() && f.Engine.Casts.ContainsKey(10), "Rejected old finish mutated shared state.");
        });
        run("Hydaelyn seventh inside member still requests eighth and stack count ignores height", () =>
        {
            using var f = Scene(); var c = new Vector3(100, 0, 100);
            for (ulong i = 101; i < 108; ++i) f.Actors[i] = f.Actors[i] with { Position = c };
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10 }; f.Scan();
            Start(f, 10, 27737, target: 101);
            Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("进入人数不足")), "Seven-inside count missed outside eighth.");
            f.Actors[100] = f.Actors[100] with { Position = c + new Vector3(5.8f, 2, 0) }; f.Scan();
            Check(!f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("进入人数不足")), "XZ membership used three-dimensional distance.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x34EA, 995); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i)
            f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + new Vector3((i % 4 - 1) * 5, 0, (i / 4 - 1) * 5),
                PartySlot: i, PartyRole: i < 2 ? "Tank" : "DPS", Hostile: false);
        Add(f, 10, 0x233C); Add(f, 11, 0x233C); return f;
    }
    private static void Add(Fight f, ulong id, uint oid, Vector3? position = null, float rotation = 0)
    {
        f.Actors[id] = new(id, oid, position ?? new(100, 0, 100), Rotation: rotation); f.Scan();
    }
    private static void At(Fight f, double time) { f.Now = f.Start.AddSeconds(time); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0, Vector3? location = null, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("hyda/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
