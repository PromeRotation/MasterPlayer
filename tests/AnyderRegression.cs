using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class AnyderRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (type, field, native) in new (string, string, Func<float, IGeometryShape>)[]
        {
            ("D081Cladoselache", "arena", AnyderArenas.Sharks),
            ("D082MorbolMarquis", "DefaultBounds", AnyderArenas.Morbol),
            ("D082MorbolMarquis", "BlueBlossomBounds", AnyderArenas.MorbolBlue),
            ("D082MorbolMarquis", "YellowBlossomBounds", AnyderArenas.MorbolYellow),
            ("D083Quetzalcoatl", "arena", AnyderArenas.Quetzalcoatl),
        })
            run($"Anyder {field} {type} exact source arena both views ownership", () =>
            {
                var source = SourceArena(type, field); var shape = native(0); var world = HostDrawingGeometry.Convert(shape);
                for (var x = -27f; x < 27; x += .19f)
                    for (var z = -27f; z < 27; z += .19f)
                    {
                        var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z); var expected = source.Contains(new(x, z));
                        Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Arena/world mismatch.");
                    }
                Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.anyder") && AnyderCatalog.Create().Length == 3, "Native discovery missing.");
                foreach (var oid in new uint[] { 0x27AB, 0x27AC, 0x249E, 0x28DA })
                    Check(ArrDungeonCatalog.Owns(oid, 841) && !ArrDungeonCatalog.Owns(oid, 840), "Ownership escaped.");
            });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x27AB, 0x27AC, 15881, new BossMod.AOEShapeCone(40, new(MathF.PI / 6))),
            (0x27AB, 0x27AC, 15883, new BossMod.AOEShapeCone(50, new(MathF.PI / 6))),
            (0x27AB, 0x27AB, 15880, new BossMod.AOEShapeCircle(13)),
            (0x27AB, 0x27AB, 15882, new BossMod.AOEShapeCircle(13)),
            (0x28DA, 0x233C, 15900, new BossMod.AOEShapeCircle(5)),
            (0x28DA, 0x233C, 15906, new BossMod.AOEShapeCone(40, new(MathF.PI / 6))),
            (0x28DA, 0x28DA, 15903, new BossMod.AOEShapeDonut(5, 40)),
        })
            run($"Anyder cast {action} source shape packet heading both views cancellation", () =>
            {
                using var f = Prepare(boss);
                var id = boss == oid ? 1UL : 10UL;
                if (id == 10) { f.Actors[id] = new(id, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 8, .7f);
                var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Missing cast outputs.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled outputs leaked.");
            });
        foreach (var survivor in new uint[] { 0x27AB, 0x27AC })
            run($"Anyder sharks survivor {survivor:X} preserves primary session and delayed buster until actual effect", () =>
            {
                using var f = Prepare(0x27AB); var c = f.Actors[1].Position;
                f.Actors[2] = new(2, 0x27AC, c + Vector3.UnitX * 6, InCombat: true); Scan(f);
                var survivingId = survivor == 0x27AB ? 1UL : 2UL; var dyingId = survivor == 0x27AB ? 2UL : 1UL;
                Cast(f, survivingId, 15876, 4, target: 100);
                f.Engine.Finish(survivingId, completed: true); f.Actors[survivingId] = f.Actors[survivingId] with { Cast = null };
                f.Actors[dyingId] = f.Actors[dyingId] with { Dead = true }; Scan(f);
                Check(f.Engine.Active?.BossOid == 0x27AB && f.Engine.PrimaryId == survivingId && f.Engine.PredictedDamage.Count() == 1, "Partner death reset or duplicated delayed buster.");
                f.Now = f.Start.AddSeconds(8); Scan(f); Check(f.Engine.PredictedDamage.Count() == 1, "Delayable buster expired before real effect.");
                f.Engine.Effect(survivingId, 15876, 1, f.Now, target: 100); f.Render(); Check(!f.Engine.PredictedDamage.Any(), "Real buster effect leaked.");
                f.Actors[survivingId] = f.Actors[survivingId] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Both shark deaths retained session.");
            });
        run("Anyder Doliodus alias starts without first shark and pools state seven", () =>
        {
            using var f = Prepare(0x27AC); var c = f.Actors[1].Position;
            Check(f.Engine.Active?.BossOid == 0x27AB, "Companion could not start.");
            f.Actors[20] = new(20, 0x1E909F, c); Scan(f);
            Compare(f.Engine.AmbientAoes().Single().Shape, c, 0, new BossMod.AOEShapeCircle(8));
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Pool missing.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(f.Draw.Previews.Count == 0, "Pool expired state leaked.");
        });
        run("Anyder Morbol five cross effects preserve original 6.1 clock current heading and 27.1 breath", () =>
        {
            using var f = Prepare(0x249E); Cast(f, 1, 15888, 5, .7f);
            var first = f.Engine.AmbientAoes().Single();
            Compare(first.Shape, first.Position, first.Rotation, new BossMod.AOEShapeCross(25, 3));
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "First cross duplicate/missing.");
            f.Engine.Finish(1, completed: true); f.Actors[1] = f.Actors[1] with { Cast = null };
            Check(f.Engine.AmbientAoes().Single() == first, "Natural finish erased first cross.");
            var activation = f.Start.AddSeconds(5.3);
            for (var hit = 0; hit < 5; ++hit)
            {
                f.Now = activation.AddSeconds(hit * 6.1);
                if (hit > 0)
                {
                    f.Actors[1] = f.Actors[1] with { Rotation = hit * .4f }; Scan(f);
                    var next = f.Engine.AmbientAoes().Single(a => a.Id == "anyder/cross");
                    Check(Math.Abs((next.Activation!.Value - f.Now).TotalMilliseconds) < .001
                        && Math.Abs(next.Rotation - (hit * .4f + MathF.PI / 4)) < .00001, "Cross progression invented rotation or clock.");
                }
                f.Engine.Effect(1, hit == 0 ? 15888u : 15889u, (uint)hit, f.Now);
                f.Engine.Effect(1, hit == 0 ? 15888u : 15889u, (uint)hit, f.Now);
                f.Render();
                Check(!f.Engine.AmbientAoes().Any(a => a.Id == "anyder/cross"), "Cross advanced twice or displayed next too early.");
            }
            f.Now = activation.AddSeconds(27.1); Scan(f);
            var breath = f.Engine.AmbientAoes().Single(a => a.Id == "anyder/breath");
            Compare(breath.Shape, breath.Position, breath.Rotation, new BossMod.AOEShapeCone(25, new(MathF.PI / 4)));
            Check(breath.Activation == f.Now, "Breath original clock lost.");
            f.Engine.Effect(1, 15890, 10, f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Breath not cleared.");
        });
        run("Anyder cancelled first cross releases subsequent five hit prediction", () =>
        {
            using var f = Prepare(0x249E); Cast(f, 1, 15888, 5, 0); f.Engine.Finish(1); f.Engine.Tick(f.Start.AddSeconds(40)); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cross leaked.");
        });
        run("Anyder Morbol blossom animations restore exact yellow blue default arenas and solver", () =>
        {
            using var f = Prepare(0x249E); var c = f.Actors[1].Position; f.Actors[20] = new(20, 0x1EA1A1, c); Scan(f);
            foreach (var (state, field) in new (uint, string)[] { (0x00100020, "YellowBlossomBounds"), (0x00010002, "BlueBlossomBounds"), (0x00040008, "DefaultBounds") })
            {
                f.Engine.Animation(20, state); f.Render(); var source = SourceArena("D082MorbolMarquis", field);
                var flowers = f.Engine.AmbientAoes().SingleOrDefault(a => a.Id == "anyder/blossom");
                var poison = flowers?.Shape.Resolve(flowers.Position, flowers.Rotation, null);
                var worldPoison = poison == null ? null : HostDrawingGeometry.Convert(poison);
                bool Safe(Vector3 p) => f.Engine.Arena!.Contains(p) && poison?.Contains(p) != true;
                for (var x = -24f; x < 24; x += .37f)
                    for (var z = -24f; z < 24; z += .37f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        Check(Safe(p) == source.Contains(new(x, z)) && poison?.Contains(p) == worldPoison?.Contains(p),
                            $"Animation arena mismatch: {state:X} offset {x},{z}.");
                    }
                // Polygon edge inclusion differs at an exact boolean-cut vertex;
                // verify both sides of the fifteen-yalm flower boundary.
                foreach (var z in new[] { -15.002f, -14.998f })
                    Check(Safe(c + new Vector3(.002f, 0, z)) == source.Contains(new(.002f, z)),
                        "Flower boundary neighborhood differs.");
                var removed = Enumerable.Range(-20, 41).SelectMany(x => Enumerable.Range(-20, 41)
                    .Select(z => c + new Vector3(x + .1f, 0, z + .1f)))
                    .Where(p => AnyderArenas.Morbol(0).Contains(p) && !Safe(p)).ToArray();
                if (removed.Length > 0)
                {
                    f.Actors[100] = f.Actors[100] with { Position = removed[0] }; Scan(f);
                    Solve(f, Safe);
                }
            }
        });
        run("Anyder Morbol two delayed lashes original event clocks target double effects and next cast cancellation", () =>
        {
            using var f = Prepare(0x249E);
            f.Engine.Effect(1, 15892, 1, f.Start.AddSeconds(2), target: 100, occurredAt: f.Start);
            f.Engine.Effect(1, 15892, 2, f.Start.AddSeconds(2), target: 100, occurredAt: f.Start.AddSeconds(.2));
            Check(f.Engine.PredictedDamage.Count() == 2 && f.Engine.PredictedDamage.First().Activation == f.Start.AddSeconds(3.4), "Event clock or double hit lost.");
            f.Engine.Effect(1, 15894, 3, f.Now, target: 100); Check(f.Engine.PredictedDamage.Count() == 1, "Lash consumed more than one target.");
            f.Engine.Effect(1, 15894, 4, f.Now, target: 100); Check(!f.Engine.PredictedDamage.Any(), "Second lash leaked.");
            f.Engine.Effect(1, 15890, 5, f.Now, target: 100); Cast(f, 1, 15891, 4);
            Check(!f.Engine.PredictedDamage.Any(), "Skipped lash persisted into unrelated cast.");
        });
        run("Anyder collect orbs forced union of offset world squares real solver actual effects and stale snapshots", () =>
        {
            using var f = Prepare(0x28DA); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x28DB, c + Vector3.UnitX * 8, Rotation: MathF.PI / 2);
            f.Actors[11] = new(11, 0x28DB, c - Vector3.UnitX * 8, Rotation: 0);
            f.Actors[20] = new(20, 0x233C, c); Scan(f); Cast(f, 20, 15906, 5, 0);
            var enter = f.Engine.AmbientAoes().Single(a => a.Id == "anyder/collect/enter");
            Check(Math.Abs((enter.Activation!.Value - f.Start).TotalSeconds - 6.4) < .001 && f.Draw.Previews.Count == 3,
                $"Collect deadline or safe outlines wrong: {enter.Activation:O}, previews {f.Draw.Previews.Count}.");
            Solve(f, p => Math.Abs(p.X - (c.X + 8.5f)) <= .5f && Math.Abs(p.Z - c.Z) <= .5f
                || Math.Abs(p.X - (c.X - 8)) <= .5f && Math.Abs(p.Z - (c.Z + .5f)) <= .5f);
            f.Engine.Effect(10, 15901, 1, f.Now); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "anyder/collect/10"), "Collected orb revived.");
            f.Engine.Effect(11, 17170, 2, f.Now); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("anyder/collect/")), "Uncollected orb revived.");
        });
        run("Anyder expanding four orb effects grow every fourth hit cap twelve finish reset and animation off", () =>
        {
            using var f = Prepare(0x28DA); var c = f.Actors[1].Position;
            for (var i = 0; i < 4; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x1EAB51, c + Vector3.UnitX * (i * 4 - 6));
            Scan(f);
            for (var i = 0; i < 4; ++i) f.Engine.Animation((ulong)(10 + i), 0x00100020, f.Start);
            f.Render(); Check(f.Draw.Previews.Count == 4 && f.Engine.AmbientAoes().All(a => a.Shape is CircleTemplate { Radius: 1.5f }), "Initial expanding orbs missing.");
            for (var i = 0; i < 48; ++i)
            {
                f.Now = f.Start.AddSeconds(i * .25);
                f.Engine.Effect((ulong)(50 + i % 4), 15904, (uint)i, f.Now); f.Render();
                var expected = Math.Min(12, 1.5f + (i + 1) / 4);
                Check(f.Engine.AmbientAoes().All(a => a.Shape is CircleTemplate circle && circle.Radius == expected
                    && a.Activation == f.Now.AddSeconds(1.1)), "Growth count/radius/clock mismatch.");
                Scan(f); Check(f.Engine.AmbientAoes().All(a => ((CircleTemplate)a.Shape).Radius == expected), "Repeated snapshots reset growth.");
            }
            Cast(f, 1, 15903, 15); f.Engine.Finish(1, completed: true); f.Render();
            Check(f.Engine.AmbientAoes().All(a => a.Shape is CircleTemplate { Radius: 1.5f }), "Winding finish did not reset growth.");
            for (var i = 0; i < 4; ++i) f.Engine.Animation((ulong)(10 + i), 0x00010002);
            f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Disabled growth leaked.");
        });
        foreach (var (boss, oid, action, radius) in new (uint, uint, uint, float)[] { (0x27AB, 0x27AC, 15877, 8), (0x249E, 0x233C, 15893, 8), (0x28DA, 0x233C, 15899, 5) })
            run($"Anyder spread {action} viewer switch solver and cancel", () =>
            {
                using var f = Prepare(boss); f.Actors[20] = new(20, oid, f.Actors[1].Position);
                f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position + Vector3.UnitZ * 3 }; Scan(f);
                Cast(f, 20, action, 6, target: 100);
                Check(f.Engine.AmbientAoes().Single(a => a.Id == "party/separate/20/101").Shape is CircleTemplate circle && circle.Radius == radius, "Spread radius missing.");
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) >= radius);
                f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id == "party/avoid/20"), "Viewer switch failed.");
                f.Engine.Finish(20); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Spread cancel leaked.");
            });
    }

    private static BossMod.ArenaBoundsCustom SourceArena(string type, string field) =>
        (BossMod.ArenaBoundsCustom)typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Shadowbringers.Dungeon.D08AkadaemiaAnyder.{type}.{type}")!
            .GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 841); f.Engine.ResetFight();
        var p = f.Engine.Definitions.Single(d => d.BossOid == boss || d.PrimaryAliases?.Contains(boss) == true).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, p, InCombat: true);
        f.Actors[100] = new(100, 0, p, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false);
        Scan(f); return f;
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
        for (var x = -52f; x < 52; x += .71f)
            for (var z = -52f; z < 52; z += .71f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Source/world shape mismatch.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position), $"Real solver failed: {solution.Status}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
