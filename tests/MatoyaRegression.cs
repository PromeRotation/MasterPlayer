using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class MatoyaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Matoya exact territory ownership arena both views and script identity", () =>
        {
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.matoya"), "Script not registered.");
            foreach (var oid in new uint[] { 0x300C, 0x307F, 0x300B })
            {
                Check(ArrDungeonCatalog.Owns(oid, 933) && !ArrDungeonCatalog.Owns(oid, 916), "Ownership leaked.");
                using var f = Prepare(oid); var shape = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(shape);
                for (var x = -21.131f; x < 21; x += .63f)
                    for (var z = -21.231f; z < 21; z += .63f)
                    {
                        var p = shape.Position + new Vector3(x, 0, z);
                        var expected = oid == 0x300C ? x * x + z * z <= 19.5f * 19.5f : Math.Abs(x) <= 19.5f && Math.Abs(z) <= 19.5f;
                        Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Arena differs between views.");
                    }
            }
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x300C, 0x233C, 21633, new BossMod.AOEShapeCircle(6)),
            (0x300B, 0x300B, 22806, new BossMod.AOEShapeCircle(30)),
            (0x300B, 0x233C, 22808, new BossMod.AOEShapeCircle(5)),
            (0x300B, 0x233C, 22807, new BossMod.AOEShapeRect(50, 2.5f)),
            (0x300B, 0x3052, 22822, new BossMod.AOEShapeRect(40, 3)),
        })
            run($"Matoya cast {action} source shape radar world cancel", () =>
            {
                using var f = Prepare(boss); var id = boss == oid ? 1UL : 10UL;
                if (id != 1) { f.Actors[id] = new(id, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 4, .7f);
                var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Cast drawing/navigation missing.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        run("Matoya breccia ordered fan donut sectors original finish clock only current stage both views", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 21646, 4.3, .7f);
            BossMod.AOEShape[] expected = [new BossMod.AOEShapeCone(6.5f, new(3 * MathF.PI / 4)),
                new BossMod.AOEShapeDonutSector(6.5f, 12.5f, new(3 * MathF.PI / 4)),
                new BossMod.AOEShapeDonutSector(12.5f, 18.5f, new(3 * MathF.PI / 4))];
            for (var step = 0; step < 3; ++step)
            {
                var aoe = f.Engine.AmbientAoes().Single();
                Compare(aoe.Shape, aoe.Position, aoe.Rotation, expected[step]);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "More than current concentric stage shown.");
                var time = f.Start.AddSeconds(4.3 + step * 1.5);
                if (step == 0) Check(aoe.Activation == f.Start.AddSeconds(4.6), "Initial finish delay differs.");
                else Check(aoe.Activation == time, "Following stage did not use finish plus 1.5.");
                f.Now = time;
                if (step > 0) Cast(f, 10, (uint)(21646 + step), 1.2, .7f);
                f.Engine.Finish(10, endedAt: time, completed: true); f.Actors[10] = f.Actors[10] with { Cast = null }; f.Render();
            }
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Last concentric stage leaked.");
        });
        run("Matoya breccia separate sequences matching origin rotation and cancellation finish advances", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c + Vector3.UnitX * 4); Scan(f);
            Cast(f, 10, 21646, 4, .3f); Cast(f, 11, 21646, 4, .8f);
            f.Now = f.Start.AddSeconds(1); f.Engine.Finish(10, endedAt: f.Now); f.Render();
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 2 && aoes.Single(a => a.Position == c).Shape is MasterPlayer.Mechanics.PilgrimsTraverse.PtDonutSectorTemplate
                && aoes.Single(a => a.Position != c).Shape is FanTemplate, "One finish advanced unrelated sequence.");
        });
        run("Matoya rocky roll tether clock growth steps rearm timers dynamic bait holes and untether", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x300E, c - Vector3.UnitZ * 5, HitboxRadius: 2); Scan(f);
            f.Engine.Tether(100, 7, true, f.Start.AddSeconds(-1), 10); f.Render();
            Check(Math.Abs((f.Engine.AmbientAoes().First().Activation!.Value - f.Start.AddSeconds(7.2)).TotalMilliseconds) < .001, "Initial tether clock lost.");
            var line = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Compare(line.Shape, line.Position, line.Rotation, new BossMod.AOEShapeRect(60, 2));
            f.Now = f.Start.AddSeconds(1); f.Actors[10] = f.Actors[10] with { HitboxRadius = 2.5f, Tethers = new Dictionary<uint, ulong> { [7] = 100 } }; Scan(f);
            line = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(Math.Abs((line.Activation!.Value - f.Start.AddSeconds(10.7)).TotalMilliseconds) < .001, "First growth not rearmed.");
            Compare(line.Shape, line.Position, line.Rotation, new BossMod.AOEShapeRect(60, 3));
            f.Now = f.Start.AddSeconds(2); f.Actors[10] = f.Actors[10] with { HitboxRadius = 4 }; Scan(f);
            line = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Compare(line.Shape, line.Position, line.Rotation, new BossMod.AOEShapeRect(60, 4));
            Check(Math.Abs((line.Activation!.Value - f.Start.AddSeconds(11.7)).TotalMilliseconds) < .001, "Second growth not rearmed.");
            f.Engine.MapEffect(0x0A, 0x00020001); f.Render();
            var lane = f.Engine.AmbientAoes().Single(a => a.Id == "matoya/bait/hole/10");
            Check(lane.Activation == null && !lane.Display && lane.Navigate && f.Engine.Hints(c).Any(h => h.Contains("洞口")), "Hole lane must apply immediately.");
            f.Engine.MapEffect(0x0A, 0x00080004); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.Contains("hole")), "Closed hole retained.");
            f.Engine.Tether(100, 7, false, f.Now, 10); f.Render();
            Check(!f.Engine.AmbientAoes().Any(), "Untether leaked bait.");
            Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Stale object tether resurrected cancelled bait.");
        });
        run("Matoya rocky roll initial oversized ball does not skip growth stage", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x300E, c, HitboxRadius: 4, Tethers: new Dictionary<uint, ulong> { [7] = 100 }); Scan(f);
            f.Now = f.Start.AddSeconds(1); Scan(f); var aoe = f.Engine.AmbientAoes().First();
            Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(60, 2));
        });
        run("Matoya rocky roll hole lane union actual solver and other perspective avoidance", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x300E, c - Vector3.UnitZ * 10, Tethers: new Dictionary<uint, ulong> { [7] = 100 });
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 6 };
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitZ * 14 }; Scan(f);
            f.Engine.MapEffect(0x0A, 0x00020001); f.Engine.MapEffect(0x0B, 0x00020001); f.Render();
            var forbidden = f.Engine.AmbientAoes().Single(a => a.Id == "matoya/bait/hole/10").Shape.Resolve(c, 0, null);
            Solve(f, p => !forbidden.Contains(p, 0));
            f.PlayerId = 101; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("matoya/bait/hole/"))
                && f.Engine.AmbientAoes().Any(a => a.Id == "matoya/bait/10/avoid"), "Perspective kept own hole constraint.");
        });
        run("Matoya mud voidzone state seven remains dangerous source lifetime clear", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            f.Actors[20] = new(20, 0x1EB145, c, EventState: 7); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Voidzone incorrectly filtered state seven.");
            f.Engine.RemoveActor(20); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed voidzone leaked.");
        });
        foreach (var own in new[] { false, true })
            run($"Matoya falling rock four player stack real solver own {own}", () =>
            {
                using var f = Prepare(0x300C); var c = f.Actors[1].Position;
                f.Actors[10] = new(10, 0x233C, c); f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 12 }; Scan(f);
                Cast(f, 10, 21652, 5, target: own ? 100UL : 101UL);
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (own ? 3 : 6));
            });
        run("Matoya gurgle eight map lanes original timestamp source rotations and effect clears whole wave", () =>
        {
            using var f = Prepare(0x307F);
            for (uint index = 0x13; index <= 0x1A; ++index) f.Engine.MapEffect(index, 0x00020001, f.Start.AddSeconds(-1));
            f.Render(); var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 8 && aoes.All(a => a.Activation == f.Start.AddSeconds(8)), "Gurgle map clock or count lost.");
            foreach (var aoe in aoes) Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(60, 5));
            Check(aoes[0].Rotation > 1.5 && aoes[^1].Rotation < -1.5, "Map lane rotation flipped.");
            f.Engine.Effect(1, 22930, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Gurgle wave leaked.");
        });
        run("Matoya icicle baits survives untether predicts tankbuster consumes effect no snapshot resurrection", () =>
        {
            using var f = Prepare(0x307F); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x3081, c - Vector3.UnitZ * 10, Tethers: new Dictionary<uint, ulong> { [8] = 100 }); Scan(f);
            var aoe = f.Engine.AmbientAoes().First(); Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(80, 1.5f));
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(5.4), "Icicle tankbuster timing lost.");
            f.Engine.Tether(100, 8, false, f.Now, 10); f.Render(); Check(f.Draw.Previews.Count == 1, "Icicle untether incorrectly cleared pending hit.");
            f.Engine.Effect(10, 22928, 1, f.Now); f.Render(); Scan(f);
            Check(f.Draw.Previews.Count == 0 && !f.Engine.PredictedDamage.Any(), "Icicle effect leaked or snapshot resurrected.");
        });
        run("Matoya geyser birth clock closest safe only real solver active reset cloud hides then restores", () =>
        {
            using var f = Prepare(0x307F); var c = f.Actors[1].Position;
            f.Engine.ObjectCreated(20, f.Start.AddSeconds(-2));
            f.Actors[20] = new(20, 0x1EB0C7, new(0, 0, -163)); f.Actors[21] = new(21, 0x1EB0C7, c + Vector3.UnitX * 10); Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "matoya/geyser/20").Activation == f.Start.AddSeconds(1.9), "Birth timestamp lost.");
            f.Engine.MapEffect(0x12, 0x00020001); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Safe) == 1 && f.Engine.AmbientAoes().Single(a => a.Safe).Id == "matoya/geyser/20", "Geyser safe inversion polluted other circles.");
            Solve(f, p => Vector3.Distance(p, f.Actors[20].Position) <= 6 && Vector3.Distance(p, f.Actors[21].Position) >= 6);
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, -175) }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any() && f.Engine.Arena!.Position.Z == -175 && f.Engine.Arena.Contains(new(9, 0, -170), 0)
                && !f.Engine.Arena.Contains(new(10, 0, -175), 0), "Cloud arena not switched without draw callback.");
            f.Actors[100] = f.Actors[100] with { Position = c }; Scan(f); Check(f.Draw.Previews.Count == 2, "Leaving cloud did not restore geysers.");
            f.Engine.MapEffect(0x12, 0x00080004); f.Render(); Check(f.Engine.AmbientAoes().All(a => !a.Safe), "Inactive geyser retained inversion.");
            f.Actors[10] = new(10, 0x233C, f.Actors[20].Position); Scan(f); f.Engine.Effect(10, 22926, 1, f.Now); f.Render(); Scan(f);
            Check(f.Draw.Previews.Count == 1 && !f.Engine.AmbientAoes().Any(a => a.Id == "matoya/geyser/20"), "Erupted geyser recreated from old object frame.");
        });
        foreach (var action in new uint[] { 22814, 22815, 20045 })
            run($"Matoya rear {action} huff navigation only suppression both views and forced origin", () =>
            {
                using var f = Prepare(0x300B); var c = f.Actors[1].Position;
                f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, action, 10);
                var aoe = f.Engine.AmbientAoes().Single(); Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeDonut(5, 40));
                Cast(f, 1, 22809, 8, MathF.PI / 2);
                var rear = f.Engine.AmbientAoes().Single(a => a.Id == "matoya/rear/10"); Check(!rear.Navigate && rear.Display, "Huff should only suppress rear navigation.");
                Check(f.Engine.AmbientAoes().Any(a => a.Id == "matoya/huff/landing"), "Huff landing not drawn.");
                f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10, Statuses = [160] }; Scan(f);
                Solve(f, p => Vector3.Distance(p, c) <= 5);
                f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Render();
                Check(f.Engine.AmbientAoes().Single(a => a.Id == "matoya/rear/10").Navigate, "Rear navigation did not resume.");
            });
        foreach (var fail in new[] { false, true })
            run($"Matoya limit phase huff cache {(fail ? "fifty" : "fifteen")} delayed activation stop wall ignore immunity effect clear", () =>
            {
                using var f = Prepare(0x300B); f.Actors[100] = f.Actors[100] with { Position = new(10, 0, 0), Statuses = [160] };
                f.Actors[10] = new(10, fail ? 0x233Cu : 0x3052u, Vector3.Zero); Scan(f); Cast(f, 1, 22810, 50, MathF.PI / 2);
                Check(!f.Engine.AmbientAoes().Any(), "Visual cache executed a knockback.");
                f.Now = f.Start.AddSeconds(1); Cast(f, 10, fail ? 20045u : 20020u, 2.5);
                if (!fail) { f.Now = f.Start.AddSeconds(3.5); f.Engine.Finish(10, endedAt: f.Now, completed: true); f.Render(); }
                var kb = f.Engine.AmbientAoes().Single(a => a.Id == "matoya/huff/landing");
                Check(kb.Position.X is > 19.49f and <= 19.5f && Math.Abs(kb.Position.Z) < .001f, "Directional landing not wall stopped or immunity incorrectly suppressed.");
                var field = typeof(DungeonEncounter).GetField("matoyaHuff", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var prediction = ((Vector3, float, float, DateTime))field.GetValue(f.Engine)!;
                Check(prediction.Item3 == (fail ? 50 : 15)
                    && Math.Abs((prediction.Item4 - f.Start.AddSeconds(fail ? 6.4 : 14.4)).TotalMilliseconds) < .001, "Huff activation clock lost.");
                f.Engine.Effect(1, fail ? 22812u : 22811u, 1, f.Now); f.Render();
                Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("matoya/huff/")), "Huff hit left control hints.");
            });
        run("Matoya barbeque permanent hazard immediate advance lane imminent right edge real solver rect cast relaxes effect clear", () =>
        {
            using var f = Prepare(0x300B); f.Actors[100] = f.Actors[100] with { Position = new(-5, 0, 0) }; Scan(f);
            f.Engine.MapEffect(0x10, 0x00020001); f.Render();
            var hazard = f.Engine.AmbientAoes().Single(a => a.Id == "matoya/barbeque");
            Compare(hazard.Shape, hazard.Position, hazard.Rotation, new BossMod.AOEShapeRect(10, 20));
            Solve(f, p => p.X >= -4.9f && p.X <= 19);
            Cast(f, 1, 23331, 2.5); f.Now = f.Start.AddSeconds(2.5); f.Engine.Finish(1, endedAt: f.Now, completed: true); f.Render();
            Solve(f, p => p.X is >= 18.5f and <= 19);
            f.Actors[10] = new(10, 0x233C, new(-19, 0, 0)); Scan(f); Cast(f, 10, 22807, 3, MathF.PI / 2);
            var lane = f.Engine.AmbientAoes().Single(a => a.Id == "matoya/barbeque/advance").Shape.Resolve(Vector3.Zero, 0, null);
            Check(!lane.Contains(new(0, 0, 0), 0), "BarbequeRect did not relax imminent lane.");
            f.Engine.Effect(10, 22821, 1, f.Now); f.Engine.Finish(10); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0, "ToACrisp cleanup leaked.");
        });
        run("Matoya icon spreads original clock both perspectives solver target effects", () =>
        {
            using var f = Prepare(0x300B); f.Actors[101] = f.Actors[101] with { Position = new(3, 0, 0) }; Scan(f);
            f.Engine.Icon(100, 169, f.Start.AddSeconds(-1)); f.Render();
            Check(f.Engine.AmbientAoes().First().Activation == f.Start.AddSeconds(5.7), "Icon clock lost.");
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) >= 5);
            f.PlayerId = 101; Scan(f); Solve(f, p => Vector3.Distance(p, f.Actors[100].Position) >= 5);
            f.Engine.Effect(10, 22819, 1, f.Now, target: 100); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Spread effect leaked.");
        });
        foreach (var action in new uint[] { 22803, 22801 })
            run($"Matoya delayed damage {action} visual finish preserved real effect removes", () =>
            {
                using var f = Prepare(0x300B); Cast(f, 1, action, 4, target: 100);
                var activation = f.Start.AddSeconds(action == 22803 ? 5.1 : 5.2);
                Check(f.Engine.PredictedDamage.All(p => Math.Abs((p.Activation - activation).TotalMilliseconds) < .001)
                    && f.Engine.PredictedDamage.Count() == (action == 22803 ? 2 : 1), "Damage prediction clock/count differs.");
                f.Now = f.Start.AddSeconds(4); f.Engine.Finish(1, endedAt: f.Now, completed: true);
                Check(f.Engine.PredictedDamage.Any(), "Visual finish lost pending damage.");
                f.Engine.Effect(10, action == 22803 ? 22804u : 22802u, 1, f.Now, target: 100); Check(!f.Engine.PredictedDamage.Any(), "Damage effect leaked.");
            });
        run("Matoya mud holes stay outside arena unclipped radar fitting and previews navigation unchanged", () =>
        {
            using var f = Prepare(0x300C); var c = f.Actors[1].Position;
            for (uint i = 0x0A; i <= 0x0D; ++i) f.Engine.MapEffect(i, 0x00020001);
            f.Render(); var holes = f.Engine.AmbientAoes().ToArray();
            Check(holes.Length == 4 && holes.All(a => a.Unclipped && a.OutlineOnly && !a.Navigate)
                && f.Snapshot.DangerAreas.Count == 0, "Outside holes became a navigation boundary.");
            var registry = new AoeVisibilityRegistry([]);
            foreach (var h in holes) registry.AddPreview(h.Id, h.Shape.Resolve(h.Position, 0, null), true, 0, "holes", "holes", true, 1, true);
            var previews = registry.GetPreviews(f.Now).ToArray();
            Check(previews.All(p => p.Unclipped && p.OutlineOnly), "Unclipped bit lost in visibility registry.");
            foreach (var rotation in new[] { 0f, .73f, MathF.PI / 2 })
            {
                var layout = RadarArenaLayout.Create(f.Engine.Arena!, c, Vector2.Zero, 150, rotation, previews.Select(p => p.Geometry));
                foreach (var point in previews.SelectMany(p => p.Geometry.GetContours()).SelectMany(p => p))
                {
                    var projected = layout.Projection.Project(point);
                    Check(Math.Abs(projected.X) <= 146.01f && Math.Abs(projected.Y) <= 146.01f, "Outside hole clipped by viewport fitting.");
                }
                Check(!f.Engine.Arena!.Contains(holes[0].Position, 0), "Arena was enlarged for display.");
            }
        });
        run("Matoya icicle dead target retains last position until resolve and other bait is filled", () =>
        {
            using var f = Prepare(0x307F); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x3081, c - Vector3.UnitZ * 10, Tethers: new Dictionary<uint, ulong> { [8] = 101 }); Scan(f);
            Check(!f.Engine.AmbientAoes().First().OutlineOnly, "Other bait should be filled.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 1, "Dead target prematurely cleared bait.");
            f.Actors.Remove(101); Scan(f); Check(f.Draw.Previews.Count == 1, "Missing target lost cached position.");
            f.Engine.Effect(10, 22928, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Resolved dead bait leaked.");
        });
        run("Matoya delayed tankbusters consume matching target one at a time", () =>
        {
            using var f = Prepare(0x300B); Cast(f, 1, 22801, 4, target: 100);
            f.Now = f.Start.AddSeconds(4); f.Engine.Finish(1, endedAt: f.Now, completed: true);
            f.Now = f.Start.AddSeconds(5); Cast(f, 1, 22801, 4, target: 101);
            Check(f.Engine.PredictedDamage.Count() == 2, "Second visual replaced pending first target.");
            f.Engine.Effect(10, 22802, 1, f.Now, target: 101);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Damage effect cleared unrelated target.");
            f.Engine.Effect(11, 22802, 2, f.Now, target: 100); Check(!f.Engine.PredictedDamage.Any(), "Final delayed target leaked.");
        });
        foreach (var explosion in new[] { false, true })
            run($"Matoya source removal emits cast finish {(explosion ? "activates huff" : "advances breccia")} event clock", () =>
            {
                using var f = Prepare(explosion ? 0x300Bu : 0x300Cu); var c = f.Actors[1].Position;
                f.Actors[10] = new(10, explosion ? 0x3052u : 0x233Cu, c); Scan(f);
                if (explosion) Cast(f, 1, 22810, 50, MathF.PI / 2);
                Cast(f, 10, explosion ? 20020u : 21646u, 4);
                f.Now = f.Start.AddSeconds(2); f.Engine.RemoveActor(10, f.Start.AddSeconds(1)); f.Render();
                if (explosion) Check(f.Engine.AmbientAoes().Any(a => a.Id == "matoya/huff/landing"), "Removed explosion failed finish dispatch.");
                else Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(2.5)
                    && f.Engine.AmbientAoes().Single().Shape is MasterPlayer.Mechanics.PilgrimsTraverse.PtDonutSectorTemplate, "Removed breccia did not advance at original event time.");
            });
        foreach (var geyser in new[] { false, true })
            run($"Matoya source position matching per XZ axis ignores height {(geyser ? "geyser" : "breccia")}", () =>
            {
                using var f = Prepare(geyser ? 0x307Fu : 0x300Cu); var c = f.Actors[1].Position;
                f.Actors[10] = new(10, 0x233C, c); Scan(f);
                if (geyser)
                {
                    f.Actors[20] = new(20, 0x1EB0C7, c); Scan(f);
                    f.Actors[10] = f.Actors[10] with { Position = c + new Vector3(.8f, 8, .8f) }; Scan(f);
                    f.Engine.Effect(10, 22926, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "XZ axis tolerance rejected geyser effect.");
                }
                else
                {
                    Cast(f, 10, 21646, 4); f.Engine.Finish(10, completed: true);
                    f.Now = f.Start.AddSeconds(1); f.Actors[10] = f.Actors[10] with { Position = c + new Vector3(.8f, 8, .8f) };
                    Cast(f, 10, 21647, 1); f.Engine.Finish(10, completed: true); f.Render();
                    var aoe = f.Engine.AmbientAoes().Single();
                    Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeDonutSector(12.5f, 18.5f, new(3 * MathF.PI / 4)));
                }
            });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 933); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
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
        for (var x = -82.137f; x < 82; x += .77f)
            for (var z = -82.231f; z < 82; z += .77f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source shape differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot; var player = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && accept(path.Count > 0 ? path[^1].Position : player),
            $"Real solver failed: {solution.Status}, from {player} to {(path.Count > 0 ? path[^1].Position : player)}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
