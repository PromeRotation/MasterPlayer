using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class AnamnesisRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Anamnesis source clipped unknown and disconnected split both views ownership", () =>
        {
            foreach (var (geometry, type, field) in new (IGeometryShape, string, string)[]
            {
                (AnamnesisArenas.Unknown(0), "D101Unknown.D101Unknown", "arena"),
                (AnamnesisArenas.Split(0), "D103RukshsDheem.D103RukshsDheem", "SplitBounds"),
            })
            {
                var source = (BossMod.ArenaBoundsCustom)typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Shadowbringers.Dungeon.D10AnamnesisAnyder.{type}")!
                    .GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var world = HostDrawingGeometry.Convert(geometry); var bounds = geometry.GetBounds();
                for (var x = bounds.MinX - 1.137f; x < bounds.MaxX + 1; x += .21f)
                    for (var z = bounds.MinZ - 1.231f; z < bounds.MaxZ + 1; z += .21f)
                    {
                        var p = new Vector3(x, 0, z); var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(geometry.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source arena mismatch.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.anamnesis") && new uint[] { 0x2CD9, 0x2CDA, 0x2CFE, 0x2CFF }.All(o => ArrDungeonCatalog.Owns(o, 898) && !ArrDungeonCatalog.Owns(o, 841)), "Ownership mismatch.");
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x2CD9, 0x2CD9, 19307, new BossMod.AOEShapeCone(9, new(MathF.PI / 3))),
            (0x2CD9, 0x2CD9, 19308, new BossMod.AOEShapeCone(9, new(MathF.PI / 3))),
            (0x2CD9, 0x2CDA, 19316, new BossMod.AOEShapeCone(9, new(MathF.PI / 3))),
            (0x2CD9, 0x2CDA, 19317, new BossMod.AOEShapeCone(9, new(MathF.PI / 3))),
            (0x2CD9, 0x2CDB, 19310, new BossMod.AOEShapeCircle(8)),
            (0x2CD9, 0x2CD9, 20006, new BossMod.AOEShapeRect(50, 4)),
            (0x2CD9, 0x2CDA, 20007, new BossMod.AOEShapeRect(50, 4)),
            (0x2CFE, 0x2CFE, 19287, new BossMod.AOEShapeDonut(8, 25)),
            (0x2CFE, 0x2CFE, 19286, new BossMod.AOEShapeRect(45, 3)),
            (0x2CFE, 0x2CFE, 19284, new BossMod.AOEShapeCone(12, new(MathF.PI / 3))),
            (0x2CFE, 0x2CFE, 19285, new BossMod.AOEShapeCircle(12)),
            (0x2CFF, 0x233C, 19330, new BossMod.AOEShapeCone(40, new(MathF.PI / 12))),
            (0x2CFF, 0x2D01, 19337, new BossMod.AOEShapeCone(6, new(MathF.PI / 4))),
            (0x2CFF, 0x2CFF, 19339, new BossMod.AOEShapeCross(50, 3)),
        })
            run($"Anamnesis cast {action} exact source shape both views cancellation", () =>
            {
                using var f = Prepare(boss); var id = oid == boss ? 1UL : 10UL;
                if (id != 1) { f.Actors[id] = new(id, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 5, .7f);
                var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Cast outputs missing.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled output leaked.");
            });
        run("Anamnesis unknown either starts both death orders and disappearance handoff", () =>
        {
            foreach (var first in new uint[] { 0x2CD9, 0x2CDA })
            {
                using var f = Prepare(first); var c = f.Actors[1].Position; var second = first == 0x2CD9 ? 0x2CDAu : 0x2CD9u;
                f.Actors[2] = new(2, second, c, InCombat: true); Scan(f);
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active?.BossOid == 0x2CD9 && f.Engine.PrimaryId == 2, "Pair ended before companion.");
                f.Engine.RemoveActor(2); f.Actors.Remove(2); f.Render(); Check(f.Engine.Active == null, "Both gone did not end.");
            }
        });
        foreach (var (state, degrees) in new (uint, float)[] { (0x00041000, -135.005f), (0x00042000, 134.999f), (0x00044000, -.003f) })
            run($"Anamnesis reflection {state:X} original animation clock snapshot dedup effect and replacement", () =>
            {
                using var f = Prepare(0x2CD9); var c = f.Actors[1].Position;
                f.Actors[20] = new(20, 0x1EAF6C, c, EventAnimation: state); Scan(f);
                var aoe = f.Engine.AmbientAoes().Single(); var start = aoe.Activation;
                Check(start == f.Start.AddSeconds(14.4) && Math.Abs(aoe.Rotation - degrees * MathF.PI / 180) < .000001f, "Reflection angle/timing changed.");
                Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeCone(40, new(MathF.PI / 8)));
                f.Now = f.Start.AddSeconds(5); Scan(f); Check(f.Engine.AmbientAoes().Single().Activation == start, "Snapshot retimed reflection.");
                f.Engine.Effect(1, 19311, 1, f.Now); Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Old animation resurrected consumed reflection.");
                f.Engine.Animation(20, state, f.Start.AddSeconds(6)); f.Render();
                Check(Math.Abs((f.Engine.AmbientAoes().Single().Activation!.Value - f.Start.AddSeconds(20.4)).TotalMilliseconds) < .001, "New animation not replaced.");
            });
        run("Anamnesis line stacks independent marker source dynamic target both members solve FIFO duplicate and remove", () =>
        {
            using var f = Prepare(0x2CD9); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, c - Vector3.UnitZ * 15);
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitZ * 8 };
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 12 }; Scan(f);
            f.Engine.Effect(10, 19319, 1, f.Now.AddSeconds(2), target: 101, occurredAt: f.Start); f.Render();
            var line = f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("anamnesis/line/19320/"));
            Check(line.Position == f.Actors[10].Position, "Line origin incorrectly replaced helper with boss.");
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "anamnesis/line/enter/19320").Activation == f.Start.AddSeconds(5.2), "Marker event clock lost.");
            Solve(f, p => line.Shape.Resolve(line.Position, line.Rotation, null).Contains(p));
            f.PlayerId = 101; Scan(f); Solve(f, p => Vector3.Distance(p, f.Actors[100].Position) <= 2);
            f.Engine.Effect(10, 19312, 2, f.Now, target: 100); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("anamnesis/line/193")) == 2, "Second independent line component replaced first.");
            f.Engine.Effect(1, 19320, 3, f.Now, target: 999); f.Engine.Effect(1, 19320, 3, f.Now, target: 999); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("anamnesis/line/193")) == 1, "Final target incorrectly matched marker or duplicate consumed both.");
            f.Engine.RemoveActor(10); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Source removal leaked line.");
        });
        run("Anamnesis dead line marker target retains last position until FIFO resolve", () =>
        {
            using var f = Prepare(0x2CD9); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, c - Vector3.UnitZ * 10); Scan(f);
            f.Engine.Effect(10, 19319, 1, f.Now, target: 101); f.Render();
            f.Actors[101] = f.Actors[101] with { Dead = true, Position = c + Vector3.UnitX * 6 }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "anamnesis/line/enter/19320"), "Dead target prematurely removed stack.");
            f.Actors.Remove(101); Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("anamnesis/line/19320/")), "Absent target lost last position.");
            f.Engine.Effect(1, 19320, 2, f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Dead target stack resolve leaked.");
        });
        foreach (var state in new uint[] { 0x08000400, 0x02000100, 0x20001000, 0x00800040 })
            run($"Anamnesis nine tile pattern {state:X} ordered groups original map clock ignores reset FIFO all hits", () =>
            {
                using var f = Prepare(0x2CFE); var count = state is 0x08000400 or 0x02000100 ? 5 : 4;
                f.Engine.MapEffect(0x0D, state, f.Start.AddSeconds(-1)); f.Render();
                Check(f.Draw.Previews.Count == count && f.Snapshot.DangerAreas.Count == count, "First pattern missing.");
                var head = f.Engine.AmbientAoes().First();
                Check(head.Activation == f.Start.AddSeconds(15), "Map timestamp ignored.");
                Compare(head.Shape, head.Position, head.Rotation, new BossMod.AOEShapeRect(5, 5, 5));
                f.Engine.MapEffect(0x0E, 0x00080004); f.Engine.MapEffect(0x0D, state); f.Render(); Check(f.Draw.Previews.Count == count, "Disappearing or duplicate map changed queue.");
                for (var i = 0; i < 9; ++i)
                {
                    f.Engine.Effect(10, 19293, (uint)i, f.Now); f.Engine.Effect(10, 19293, (uint)i, f.Now); f.Render();
                    if (i == count - 1) Check(f.Draw.Previews.Count == 9 - count && f.Engine.AmbientAoes().First().Activation == f.Start.AddSeconds(17.2), "Second group did not promote with its own time.");
                }
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Tiles did not finish.");
            });
        run("Anamnesis default narrow split wave timings locked origins filtered four FIFO and arena restore", () =>
        {
            using var f = Prepare(0x2CFF); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x2D00, c, Rotation: .7f); Scan(f);
            f.Engine.Effect(10, 19333, 0, f.Now, occurredAt: f.Start.AddSeconds(-1)); f.Render();
            var first = f.Engine.AmbientAoes().Single();
            Check(first.Shape is RayRectangleTemplate && first.Activation == f.Start.AddSeconds(8.8), "Default wave wrong.");
            Compare(first.Shape, first.Position, first.Rotation, new BossMod.AOEShapeRect(21, 5));
            f.Engine.Effect(10, 13269, 1, f.Now); f.Engine.MapEffect(0x17, 0x00020001);
            Check(Math.Abs(f.Engine.Arena!.GetBounds().MaxX - 8) < .001f, "Narrow arena not applied.");
            for (var i = 0; i < 8; ++i)
            {
                f.Actors[10] = f.Actors[10] with { Rotation = i % 2 == 0 ? 0 : MathF.PI }; Scan(f);
                f.Engine.Effect(10, 19333, (uint)(i + 2), f.Now);
            }
            f.Render(); var waves = f.Engine.AmbientAoes().ToArray();
            Check(waves.Length == 2 && waves.All(a => a.Rotation == 0 && a.Activation == f.Now.AddSeconds(8)), "Narrow source grouping wrong.");
            for (var i = 0; i < 4; ++i) f.Engine.Effect(10, 13268, (uint)(20 + i), f.Now);
            f.Render(); Check(f.Engine.AmbientAoes().Count() == 2 && f.Engine.AmbientAoes().All(a => a.Activation == f.Now.AddSeconds(11.6)), "Late wave time wrong.");
            f.Engine.MapEffect(0x18, 0x00020001); f.Render();
            Check(!f.Engine.Arena!.Contains(c) && f.Engine.AmbientAoes().Count() == 4, "Split disconnected arena or all waves lost.");
            for (var i = 0; i < 4; ++i) f.Engine.Effect(10, 13268, (uint)(30 + i), f.Now);
            f.Engine.Effect(10, 19333, 40, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Single().Activation == f.Now.AddSeconds(7.6), "Split wave time wrong.");
            f.Engine.MapEffect(0x18, 0x00080004); f.Render(); Check(f.Engine.Arena!.Contains(c), "Default arena not restored.");
        });
        run("Anamnesis drains active solve solved hold viewer switch reenabling permanent reset harpoon disappearance priority", () =>
        {
            using var f = Prepare(0x2CFF); var c = f.Actors[1].Position;
            f.Actors[20] = new(20, 0x2D01, c + Vector3.UnitX * 4); Scan(f);
            f.Engine.MapEffect(0x0F, 0x00020001, f.Start.AddSeconds(-1)); f.Engine.MapEffect(0x13, 0x00020001); f.Render();
            Check(f.Engine.PriorityTarget(1, c) == 20, "Harpooner not prioritized.");
            var left = new Vector3(-11, 0, -465); var right = new Vector3(11, 0, -465);
            Solve(f, p => Math.Abs(p.X) >= 9.75f && Math.Abs(p.Z + 465) <= 1.25f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "anamnesis/drain/enter").Activation == f.Start.AddSeconds(11), "Drain activation retimed.");
            f.Actors[100] = f.Actors[100] with { Position = left }; Scan(f);
            f.Engine.MapEffect(0x0F, 0x00080004); f.Render();
            var hold = f.Engine.AmbientAoes().Single(a => a.Id == "anamnesis/drain/enter");
            Check(!hold.Shape.Resolve(hold.Position, hold.Rotation, null).Contains(left)
                && hold.Shape.Resolve(hold.Position, hold.Rotation, null).Contains(right), "Solved drain hold not enforced.");
            f.PlayerId = 101; Scan(f); Solve(f, p => Math.Abs(p.X - 11) <= 1.25f && Math.Abs(p.Z + 465) <= 1.25f);
            f.Engine.MapEffect(0x0F, 0x00020001); f.Render(); Check(f.Draw.Previews.Count == 2, "Drain reenabling duplicated display.");
            f.Actors.Remove(20); Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Harpooner disappearance retained drain phase.");
            f.Engine.MapEffect(0x0F, 0x00200004); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0, "Permanent reset leaked navigation.");
        });
        foreach (var (boss, action, radius) in new (uint, uint, float)[] { (0x2CFE, 19296, 6), (0x2CFF, 19328, 6), (0x2CFE, 19295, 5), (0x2CFF, 19326, 8) })
            run($"Anamnesis party {action} both roles true solver and cancellation", () =>
            {
                using var f = Prepare(boss); var c = f.Actors[1].Position; var stack = action is 19296 or 19328;
                f.Actors[10] = new(10, 0x233C, c); f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * (stack ? 12 : 2) }; Scan(f);
                Cast(f, 10, action, 5, target: 101);
                Solve(f, p => stack ? Vector3.Distance(p, f.Actors[101].Position) <= radius : Vector3.Distance(p, f.Actors[101].Position) >= radius);
                f.Engine.Finish(10); f.Now = f.Now.AddSeconds(1); Cast(f, 10, action, 5, target: 100);
                Solve(f, p => stack ? Vector3.Distance(p, f.Actors[101].Position) <= 3 : Vector3.Distance(p, f.Actors[101].Position) >= radius);
                f.Engine.Finish(10); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Party cancellation leaked.");
            });
        foreach (var (boss, action) in new (uint, uint)[] { (0x2CD9, 19305), (0x2CDA, 19314), (0x2CFF, 19340) })
            run($"Anamnesis delayed buster {action} natural finish effect and cancellation", () =>
            {
                using var f = Prepare(boss); Cast(f, 1, action, 4, target: 100); f.Engine.Finish(1, completed: true); f.Render();
                Check(f.Engine.PredictedDamage.Count() == 1, "Delayed buster lost/duplicated.");
                f.Engine.Effect(1, action, 1, f.Now, target: 100); Check(!f.Engine.PredictedDamage.Any(), "Damage effect not cleared.");
                f.Now = f.Now.AddSeconds(10); Cast(f, 1, action, 4, target: 100); f.Engine.Finish(1);
                Check(!f.Engine.PredictedDamage.Any(), "Cancelled damage remained.");
            });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 898); f.Engine.ResetFight();
        var p = f.Engine.Definitions.Single(d => d.BossOid == boss || d.PrimaryAliases?.Contains(boss) == true).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, p, InCombat: true);
        f.Actors[100] = new(100, 0, p, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double seconds, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -52.137f; x < 52; x += .73f)
            for (var z = -52.231f; z < 52; z += .73f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source shape mismatch.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot; var player = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && accept(path.Count > 0 ? path[^1].Position : player),
            $"Real solver failed: {solution.Status}, from {player} to {(path.Count > 0 ? path[^1].Position : player)}, fallback {solution.IsLeastDangerFallback}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
