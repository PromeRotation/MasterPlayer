using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class CosmosRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (boss, center, width, length) in new (uint, Vector3, float, float)[]
        {
            (0x2C1A, new(0, 0, 187), 41, 29), (0x2C04, new(0, 0, -60), 39, 39), (0x2C13, new(0, 0, -340), 49, 49),
        })
            run($"Cosmos arena {boss:X} source rectangle both views and ownership", () =>
            {
                using var f = Prepare(boss); var world = HostDrawingGeometry.Convert(f.Engine.Arena!);
                var source = new BossMod.ArenaBoundsRect(width / 2, length / 2);
                for (var x = -27f; x < 27; x += .21f)
                    for (var z = -27f; z < 27; z += .21f)
                    {
                        var p = center + new Vector3(x, 0, z); var expected = source.Contains(new(x, z));
                        Check(f.Engine.Arena!.Contains(p) == expected && world.Contains(p) == expected, "Arena mismatch.");
                    }
                Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.cosmos") && ArrDungeonCatalog.Owns(boss, 884) && !ArrDungeonCatalog.Owns(boss, 841), "Ownership/discovery missing.");
            });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x2C1A, 0x233C, 18287, new BossMod.AOEShapeCircle(6)),
            (0x2C04, 0x233C, 18211, new BossMod.AOEShapeCircle(8)),
            (0x2C04, 0x2C04, 18768, new BossMod.AOEShapeDonut(5, 60)),
            (0x2C13, 0x2C13, 18275, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x2C13, 0x2C13, 18274, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x2C13, 0x233C, 18268, new BossMod.AOEShapeCross(10, 2)),
            (0x2C13, 0x2C13, 18273, new BossMod.AOEShapeCone(20, new(MathF.PI / 4))),
            (0x2C13, 0x233C, 18279, new BossMod.AOEShapeCircle(3)),
        })
            run($"Cosmos cast {action} source shape packet heading both views and cancel", () =>
            {
                using var f = Prepare(boss); var id = boss == oid ? 1UL : 10UL;
                if (id == 10) { f.Actors[id] = new(id, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 5, .7f); var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Shape outputs missing.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cast cancellation leaked.");
            });
        run("Cosmos Tribulation actual radius six finish handoff birth within two state seven and cancellation", () =>
        {
            using var f = Prepare(0x2C1A); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 18852, 3);
            var forecast = f.Engine.AmbientAoes().Single(); Check(forecast.Shape is CircleTemplate { Radius: 6 }, "Tribulation used packet radius three.");
            f.Engine.Effect(10, 18852, 1, f.Start.AddSeconds(5), location: c, occurredAt: f.Start.AddSeconds(3));
            f.Engine.Finish(10, completed: true); f.Actors[10] = f.Actors[10] with { Cast = null }; f.Render();
            Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(3).AddSeconds(.1), "Pool event clock lost.");
            f.Actors[20] = new(20, 0x1EAEAE, c + Vector3.UnitX); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().Single().Id == "cosmos/pool/20", "Pool birth handoff duplicated.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(f.Draw.Previews.Count == 0, "Pool cleanup failed.");
            f.Now = f.Start.AddSeconds(10); Cast(f, 10, 18852, 3); f.Engine.Finish(10); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Cancelled pool prediction leaked.");
        });
        run("Cosmos broom animation dynamic capsule all four future windows both views", () =>
        {
            using var f = Prepare(0x2C1A); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x2C1B, c, Rotation: .7f, AnimationState1: 1); Scan(f);
            var aoes = f.Engine.AmbientAoes().ToArray(); Check(aoes.Length == 6 && f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 5, "Broom windows missing.");
            var visible = aoes.Single(a => a.Display); Compare(visible.Shape, c, .7f, new BossMod.AOEShapeCapsule(3.125f, 10), .025f);
            foreach (var (length, seconds) in new (int, double)[] { (5, 1.1), (10, 3), (20, 10) })
            {
                var next = aoes.Single(a => a.Id == $"cosmos/broom/10/{length}");
                Check(next.Activation == f.Now.AddSeconds(seconds) && next.Shape is DungeonCapsuleTemplate t && t.Length == length, "Broom timing differs.");
            }
            Check(aoes.Single(a => a.Id.EndsWith("/30")).Activation == DateTime.MaxValue, "Far future broom changed to current risk.");
            f.Actors[10] = f.Actors[10] with { Position = c + Vector3.UnitX * 3, Rotation = 0 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Display).Position.X == c.X + 3, "Broom snapshot did not move.");
            f.Actors[10] = f.Actors[10] with { AnimationState1 = 0 }; Scan(f); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Inactive broom leaked.");
        });
        run("Cosmos DarkPulse forces stack true solver", () =>
        {
            using var f = Prepare(0x2C1A); f.Actors[101] = f.Actors[101] with { Position = f.Actors[1].Position + Vector3.UnitX * 12 }; Scan(f);
            Cast(f, 1, 18282, 5, target: 101); Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 6);
        });
        foreach (var (state, degrees) in new (uint, float)[] { (0x00020001, 0), (0x00200010, 180), (0x01000080, -90), (0x08000400, 90) })
            run($"Cosmos tile rotation {degrees} source pattern carrying constraint pickup and actual solver", () =>
            {
                using var f = Prepare(0x2C04); f.Engine.MapEffect(0x0B, state); Cast(f, 1, 18205, 4);
                var tiles = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("cosmos/tile/")).ToArray();
                Check(tiles.Length == 8 && tiles.All(a => !a.Navigate), "First seed wave display differs.");
                BossMod.WPos[] source = [new(-5, -75), new(5, -75), new(-15, -65), new(15, -65), new(-15, -55), new(5, -55), new(-5, -45), new(15, -45)];
                var expected = source.Select(p => BossMod.WPos.RotateAroundOrigin(degrees, new(0, -60), p)).ToArray();
                for (var i = 0; i < 8; ++i) Check(Vector2.Distance(new(tiles[i].Position.X, tiles[i].Position.Z), new(expected[i].X, expected[i].Z)) < .0001f, "Source rotation differs.");
                f.Actors[20] = new(20, 0x1EAE9E, tiles[0].Position, Hostile: false); Scan(f);
                Check(f.Engine.CosmosSeedTarget?.Id == 20, "Clipped seed not selected.");
                var interaction = new DungeonObjectInteraction(); interaction.Prepare(f.Engine.CosmosSeedTarget, tiles[0].Position, true);
                var calls = 0; Check(interaction.Update(f.Now, false, _ => { ++calls; return true; }) && calls == 1, "Seed interaction not executed.");
                f.Actors[100] = f.Actors[100] with { Position = tiles[0].Position, Statuses = [404] }; Scan(f);
                Check(f.Engine.CosmosSeedTarget == null && !f.Engine.CosmosReadyToDrop && f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("cosmos/tile/")).All(a => a.Navigate), "Carrying seed did not change movement.");
                Solve(f, p => !tiles.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(p)));
                var safePoint = Enumerable.Range(-18, 37).SelectMany(x => Enumerable.Range(-18, 37)
                    .Select(z => f.Actors[1].Position + new Vector3(x + .25f, 0, z + .25f)))
                    .First(p => !tiles.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(p)));
                f.Actors[100] = f.Actors[100] with { Position = safePoint }; Scan(f);
                Check(f.Engine.CosmosReadyToDrop, "Safe carried position not ready.");
                f.Engine.MapEffect(0x0B, 0); f.Render(); Check(!f.Engine.CosmosReadyToDrop && !f.Engine.AmbientAoes().Any(), "Tile reset retained interaction/risk.");
            });
        run("Cosmos second seed wave hides unshifted tiles wind clones and source cardinal offset", () =>
        {
            using var f = Prepare(0x2C04); f.Engine.MapEffect(0x0B, 0x00020001); Cast(f, 1, 18205, 4);
            f.Engine.Finish(1); f.Now = f.Start.AddSeconds(5); Cast(f, 1, 18205, 4); f.Render();
            Check(!f.Engine.AmbientAoes().Any(), "Second wave showed eight unshifted tiles.");
            var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x2C06, c - Vector3.UnitZ * 20); Scan(f); Cast(f, 10, 18209, 13, .05f);
            var tiles = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("cosmos/tile/")).ToArray();
            Check(tiles.Length > 8 && tiles[0].Position == new Vector3(-5, 0, -85), "Wind did not shift by rounded cardinal ten.");
            Scan(f); Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("cosmos/tile/")) == tiles.Length, "Repeated scan shifted tiles twice.");
        });
        run("Cosmos directional wind wall stop danger landing solver and immunity expiry", () =>
        {
            using var f = Prepare(0x2C04); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x2C06, c - Vector3.UnitZ * 20);
            f.Actors[20] = new(20, 0x233C, c + Vector3.UnitZ * 10); Scan(f);
            Cast(f, 20, 18211, 13); Cast(f, 10, 18209, 13, 0);
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/wind/10"); var kb = (CosmosKnockbackTemplate)aoe.Shape;
            Check(Math.Abs(kb.Landing(c).Z - (c.Z + 10)) < .001f && Math.Abs(kb.Landing(c + Vector3.UnitZ * 15).Z - (c.Z + 19.5f)) < .001f, "Wall stop/direction differs.");
            Solve(f, p => !kb.Dangers.Any(d => d.Contains(kb.Landing(p))));
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(14) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("cosmos/wind/")), "Active immunity still constrained.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(12) } }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/wind/10"), "Expiring immunity suppressed knockback.");
        });
        run("Cosmos seed drop requires quarter second continuously safe obeys execution gates reset and retry", () =>
        {
            var now = DateTime.UnixEpoch; var control = new CosmosSeedDrop(); var calls = 0;
            bool Drop() { ++calls; return true; }
            Check(!control.Update(now, true, true, Drop) && !control.Update(now.AddSeconds(.24), true, true, Drop), "Dropped before server position settles.");
            Check(control.Update(now.AddSeconds(.25), true, true, Drop) && calls == 1, "Native drop not called after delay.");
            Check(!control.Update(now.AddSeconds(.5), true, true, Drop), "Drop retried every frame.");
            control.Update(now.AddSeconds(.6), false, true, Drop);
            Check(!control.Update(now.AddSeconds(.7), true, true, Drop), "Unsafe excursion kept old delay.");
            control.Update(now.AddSeconds(1), true, false, Drop);
            Check(calls == 1 && !control.Update(now.AddSeconds(2), true, true, Drop), "Disabled/replay execution retained pending drop.");
            Check(control.Update(now.AddSeconds(2.25), true, true, Drop) && calls == 2, "Reenable delay missing.");
        });
        run("Cosmos BlackFlame original icon clock both shapes furniture hitbox protection solver and effect", () =>
        {
            using var f = Prepare(0x2C13); var c = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 7 };
            f.Actors[20] = new(20, 0x2C16, c + Vector3.UnitX * 9, HitboxRadius: 1.5f); Scan(f);
            f.Engine.Icon(100, 25, f.Start); f.Render();
            var aoes = f.Engine.AmbientAoes().ToArray();
            var cross = aoes.Single(a => a.Id == "cosmos/black/100/cross");
            Compare(cross.Shape, cross.Position, cross.Rotation, new BossMod.AOEShapeCross(10, 2));
            Check(cross.Activation == f.Start.AddSeconds(4) && aoes.Single(a => a.Id == "cosmos/black/100/circle/avoid/20").Shape is CircleTemplate { Radius: 7.5f }, "Black flame clock/hitbox differs.");
            var danger = aoes.Where(a => a.Navigate).Select(a => a.Shape.Resolve(a.Position, a.Rotation, null)).ToArray();
            Solve(f, p => !danger.Any(d => d.Contains(p)));
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/black/100/cross").Navigate, "Viewer switch did not avoid bait.");
            f.Engine.Effect(50, 18269, 1, f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Black flame effect not cleared.");
        });
        run("Cosmos MortalFlame helper group chandelier promotion precise furniture soak solver and status removal", () =>
        {
            using var f = Prepare(0x2C13); var c = f.Actors[1].Position;
            f.Actors[20] = new(20, 0x2C16, c + Vector3.UnitX * 8, HitboxRadius: 1.5f);
            f.Actors[21] = new(21, 0x2C19, c - Vector3.UnitX * 8, HitboxRadius: 3);
            f.Actors[30] = new(30, 0x2C12, c, Targetable: false);
            f.Actors[100] = f.Actors[100] with { Statuses = [2136], StatusExpirations = new Dictionary<uint, DateTime> { [2136] = f.Start.AddSeconds(20) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/flame/furniture/21"), "Chandelier helper active before targetable.");
            Solve(f, p => Vector3.Distance(p, f.Actors[20].Position) <= .8f);
            f.Actors[30] = f.Actors[30] with { Targetable = true }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/flame/furniture/21"), "Chandelier helper did not promote.");
            f.Engine.StatusChanged(100, 2136, false, timestamp: f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Removed flame retained forced furniture.");
        });
        run("Cosmos four arrival FIFO charges dynamic lengths postcharge cones duplicate effects tether and buster cleanup", () =>
        {
            using var f = Prepare(0x2C13); var c = f.Actors[1].Position;
            for (var i = 0; i < 4; ++i)
            {
                var id = (ulong)(100 + i);
                f.Actors[id] = new(id, 0, c + new Vector3(i % 2 == 0 ? 15 : -15, 0, i < 2 ? 15 : -15), PartySlot: i, Hostile: false, HitboxRadius: .5f);
            }
            Scan(f); ulong[] order = [102, 100, 103, 101];
            for (var i = 0; i < 4; ++i) f.Engine.Icon(order[i], (uint)(53 - i), f.Start.AddSeconds(i * .1));
            f.Engine.Tether(100, 57, true, f.Now, 1); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/tether/stretch"), "Stretch tether central constraint missing.");
            f.Engine.Tether(100, 57, false, f.Now, 1); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/tether/stretch"), "Tether cancellation leaked.");
            for (var i = 0; i < 4; ++i)
            {
                var charge = f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/charge/0");
                var delta = f.Actors[order[i]].Position - c; var expected = new BossMod.AOEShapeRect(delta.Length(), 2);
                Compare(charge.Shape, charge.Position, charge.Rotation, expected, nativeBoundaryTolerance: .00001f);
                var cone = f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/charge/0/cone");
                Check(Vector3.Distance(cone.Position, f.Actors[order[i]].Position - Vector3.Normalize(delta) * 1) < .001, "Postcharge cone origin missed hitboxes.");
                f.Engine.Effect(1, i % 2 == 0 ? 18271u : 18272u, (uint)i, f.Now);
                f.Engine.Effect(1, i % 2 == 0 ? 18271u : 18272u, (uint)i, f.Now); f.Render();
                Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("cosmos/charge/") && a.Id.Count(c => c == '/') == 2) == 3 - i, "Charge FIFO double advanced.");
            }
            Cast(f, 1, 18276, 5, target: 100); f.Engine.Finish(1, completed: true); f.Render();
            Check(f.Engine.PredictedDamage.Count() == 1, "Delayable buster cleared on finish or duplicated.");
            f.Engine.Effect(1, 18276, 20, f.Now, target: 100); Check(!f.Engine.PredictedDamage.Any(), "Buster effect leaked.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stop/reset leaked.");
        });
        run("Cosmos coincident charge target retains cone without zero length rectangle script failure", () =>
        {
            using var f = Prepare(0x2C13); f.Engine.Icon(100, 50, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/charge/0/cone") && !f.Engine.AmbientAoes().Any(a => a.Id == "cosmos/charge/0"), "Coincident charge output invalid.");
            f.Engine.Effect(1, 18271, 1, f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Coincident charge not consumed.");
        });
        run("Cosmos stack bearer actively joins partner actual solver", () =>
        {
            using var f = Prepare(0x2C1A); var c = f.Actors[1].Position;
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 12 }; Scan(f);
            Cast(f, 1, 18282, 5, target: 100); Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= 3);
        });
        run("Cosmos black cross bearer moves away from partner beyond circle actual solver", () =>
        {
            using var f = Prepare(0x2C13); var c = f.Actors[1].Position; Scan(f);
            f.Engine.Icon(100, 25, f.Start); f.Render();
            var cross = f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/black/100/party-cross/101");
            Check(cross.Shape.Resolve(cross.Position, cross.Rotation, null).Contains(c)
                && Vector3.Distance(c, f.Actors[101].Position) > 6, "Test did not isolate cross from circle.");
            Solve(f, p => !cross.Shape.Resolve(cross.Position, cross.Rotation, null).Contains(p));
        });
        run("Cosmos charge bearer avoids path through distant partner actual solver and finite coincident input", () =>
        {
            using var f = Prepare(0x2C13); var c = f.Actors[1].Position;
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitZ * 10 };
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitZ * 24 }; Scan(f);
            f.Engine.Icon(100, 50, f.Start); f.Render();
            var line = f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/charge/0/line-avoid/101");
            Check(line.Shape.Resolve(line.Position, line.Rotation, null).Contains(f.Actors[100].Position), "Distant crossed partner not forbidden.");
            Solve(f, p => !line.Shape.Resolve(line.Position, line.Rotation, null).Contains(p));
            f.Actors[101] = f.Actors[101] with { Position = c }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == line.Id).Shape is CircleTemplate { Radius: 100 }, "Coincident partner generated invalid tangent.");
        });
        run("Cosmos tether both view geometry bad good transition cancellation snapshot and reset", () =>
        {
            using var f = Prepare(0x2C13); var c = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 18 }; Scan(f);
            f.Engine.Tether(100, 57, true, f.Now, 1); f.Render();
            var line = f.Engine.AmbientAoes().Single(a => a.Id == "cosmos/tether/1");
            Check(!line.Safe && !line.Navigate && f.Draw.Previews.Count == 1, "Short tether display missing.");
            var shape = line.Shape.Resolve(line.Position, line.Rotation, null);
            Check(shape.Contains(c + Vector3.UnitX * 9) && HostDrawingGeometry.Convert(shape).Contains(c + Vector3.UnitX * 9), "Tether endpoints wrong.");
            f.Engine.Tether(100, 1, true, f.Now, 1); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == line.Id).Safe, "Long tether safe color missing.");
            f.Engine.Tether(100, 1, false, f.Now, 1); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled tether display leaked.");
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [57] = 1 } };
            f.Now = f.Now.AddSeconds(1); Scan(f); Check(f.Draw.Previews.Count == 1, "Mid-observation tether not restored.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0, "Tether reset leaked.");
        });
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 884); f.Engine.ResetFight(); var p = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, p, InCombat: true); f.Actors[100] = new(100, 0, p, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double seconds, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source,
        float boundaryTolerance = 0, float nativeBoundaryTolerance = 0)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -65f; x < 65; x += .73f)
            for (var z = -65f; z < 65; z += .73f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                if (shape.Contains(p, 0) != expected)
                    Check(nativeBoundaryTolerance > 0 && Enumerable.Range(0, 12).Any(i => shape.Contains(p + new Vector3(MathF.Sin(i * MathF.PI / 6), 0,
                        MathF.Cos(i * MathF.PI / 6)) * nativeBoundaryTolerance, 0) == expected),
                        $"Native source shape differs beyond boundary inclusion epsilon: {x},{z}, rotation {rotation}, origin {origin}, native {shape.Contains(p, 0)}, expected {expected}.");
                if (world.Contains(p, 0) != expected)
                    Check(Math.Max(boundaryTolerance, nativeBoundaryTolerance) > 0 && Enumerable.Range(0, 12).Any(i => world.Contains(p + new Vector3(MathF.Sin(i * MathF.PI / 6), 0,
                        MathF.Cos(i * MathF.PI / 6)) * Math.Max(boundaryTolerance, nativeBoundaryTolerance), 0) == expected),
                        "World source shape differs.");
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
