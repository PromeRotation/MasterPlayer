using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class YuweyawataRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Yuweyawata discovers nine native modules and owns only exact territory", () =>
        {
            Check(ArrDungeonCatalog.Create(1242).Length == 9 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.yuweyawata"), "Discovery.");
            Check(!YuweyawataCatalog.Owns(0x4610) && YuweyawataCatalog.Owns(0x4612), "Source bat OID list.");
        });
        foreach (var (name, module, field) in Arenas())
            run($"Yuweyawata {name} exact source polygon radar world parity", () =>
            {
                var asm = typeof(BossMod.WorldState).Assembly;
                var type = asm.GetType("BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation." + module)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var native = (IGeometryShape)typeof(YuweyawataArenas).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [0f])!;
                Compare(native, p => source.Contains(new(p.X - source.Center.X, p.Z - source.Center.Z)), Math.Max(25, source.Radius + 2), .63f);
            });
        foreach (var (boss, action, source) in Shapes())
            run($"Yuweyawata {action} source geometry dual output and finish consumption", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var a = f.Engine.AmbientAoes().Single(a => a.Name == f.Engine.Casts[10].Rule.Name);
                Compare(a.Shape.Resolve(a.Position, a.Rotation, null), p => source.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)));
                Check(f.Draw.Previews.Count > 0, "No preview.");
                Finish(f, 10); Check(!f.Engine.AmbientAoes().Any(a => a.Name == f.Engine.Definitions.Single(d => d.BossOid == boss).Casts.Single(c => c.Action == action).Name), "Finish.");
            });
        foreach (var (boss, action, window) in new (uint, uint, double)[] { (0x4641, 41121, 1.5), (0x464A, 40642, 1.3) })
            run($"Yuweyawata {action} sticky strict batch deadline all visible current batch risky", () =>
            {
                using var f = Prepare(boss);
                Cast(f, 10, action, 5); Cast(f, 11, action, finishAt: f.Engine.Casts[10].FinishAt.AddSeconds(window)); Cast(f, 12, action, 10);
                var a = Group(f, "batch"); Check(a.Length == 3 && a.Count(a => a.Navigate) == 1 && a[0].Color != null, "Strict deadline.");
                Finish(f, 10); a = Group(f, "batch"); Check(a.Length == 2 && a[0].Navigate && a[0].Color != null, "Next batch.");
                Finish(f, 11); a = Group(f, "batch"); Check(a.Single().Navigate, "Last batch.");
                f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now); Check(Group(f, "batch").Length == 1, "Timeout consumed queue.");
            });
        run("Yuweyawata voltage removes by caster soulweave removes FIFO", () =>
        {
            using var f = Prepare(0x4641); Cast(f, 10, 41121); Cast(f, 11, 40625, 9); Finish(f, 11);
            Check(Group(f, "batch").Single().Id.Contains("/10/"), "Voltage FIFO.");
            using var g = Prepare(0x464A); Cast(g, 10, 40642); Cast(g, 11, 40641, 9); Finish(g, 11);
            Check(Group(g, "batch").Single().Id.Contains("/11/"), "Soul caster match.");
        });
        run("Yuweyawata cell shock map inverse default origin suppressed by voltage and finish only", () =>
        {
            using var f = Prepare(0x4641); var now = f.Now; f.Engine.MapEffect(0x0D, 0x00200010, now);
            Check(Group(f, "cell").Single().Position == Q(new(64.868f, 0, 285.132f)), "Inverse.");
            Check(Near(Group(f, "cell").Single().Activation, now, 8), "Map clock.");
            Cast(f, 10, 41121); Check(Group(f, "cell").Length == 0, "Cell hidden.");
            Finish(f, 10); Check(Group(f, "cell").Length == 1, "Cell restored.");
            f.Engine.MapEffect(0, 0x20001, now); Check(Group(f, "cell").Single().Position == Q(Vector3.Zero), "Default index behavior.");
            Cast(f, 11, 40626); f.Engine.Effect(11, 40626, 1, now); Check(Group(f, "cell").Length == 1, "Effect cleared.");
            Finish(f, 11); Check(Group(f, "cell").Length == 0, "Finish retained.");
        });
        run("Yuweyawata Dark II first six sort only beginning at twelfth cast", () =>
        {
            using var f = Prepare(0x464A);
            for (var i = 0; i < 11; ++i) Cast(f, (ulong)(10 + i), i % 2 == 0 ? 40656u : 40657u, 20 - i);
            Check(Group(f, "simple").First().Id.Contains("/10/"), "Premature sort.");
            Cast(f, 21, 40657, 1);
            var a = Group(f, "simple"); Check(a.Length == 6 && a[0].Id.Contains("/21/") && a.Zip(a.Skip(1)).All(p => p.First.Activation <= p.Second.Activation), "Twelfth sorting.");
        });
        run("Yuweyawata Kanilokka five map arenas clear shrink only recognized states prepare any state", () =>
        {
            using var f = Prepare(0x464A); Cast(f, 10, 40640); Check(Near(Group(f, "shrink").Single().Activation, f.Now, 5), "Shrink clock.");
            foreach (var state in new uint[] { 0x20001, 0x200010, 0x800040, 0x2000100, 0x80004 })
            {
                var before = f.Engine.Arena; f.Engine.MapEffect(7, state, f.Now); Check(!ReferenceEquals(before, f.Engine.Arena), "Arena not replaced.");
                Check(Group(f, "shrink").Length == 0, "Preview retained.");
            }
            Cast(f, 1, 40645); Finish(f, 1); var a = Group(f, "prepare").Single();
            Check(a.Safe && a.GoalPriority == 9 && ((CircleTemplate)a.Shape).Radius == 1, "Prepare goal.");
            f.Engine.MapEffect(6, 0, f.Now); Check(Group(f, "prepare").Length == 1, "Wrong index.");
            f.Engine.MapEffect(7, 0, f.Now); Check(Group(f, "prepare").Length == 0, "Any state.");
        });
        run("Yuweyawata misdirection status mask gain lose overlaps death perspective", () =>
        {
            using var f = Prepare(0x464A);
            f.Engine.StatusChanged(100, 1422, true, f.Now); Check(f.Engine.RequireYuweyawataMisdirection, "Gain.");
            f.Engine.StatusChanged(100, 2936, true, f.Now); f.Engine.StatusChanged(100, 1422, false, f.Now);
            Check(!f.Engine.RequireYuweyawataMisdirection, "Overlap counted instead of cleared.");
            f.Actors[100] = f.Actors[100] with { Statuses = [3909], Dead = true }; Scan(f);
            Check(f.Engine.RequireYuweyawataMisdirection, "Death cleared state.");
            f.PlayerId = 101; Scan(f); Check(!f.Engine.RequireYuweyawataMisdirection, "Perspective leak.");
        });
        run("Yuweyawata claw six hits boulder thirty hits persist through cast finish and clock", () =>
        {
            using var f = Prepare(0x464C); Cast(f, 10, 40613); Cast(f, 11, 40607); Finish(f, 10); Finish(f, 11);
            f.Now = f.Now.AddSeconds(90); f.Engine.Tick(f.Now);
            Check(f.Engine.AmbientAoes().Count(a => a.Name is "RagingClaw" or "BoulderDance") == 2, "Finish timeout.");
            for (uint i = 1; i <= 5; ++i) f.Engine.Effect(10, 40614, i, f.Now);
            Check(f.Engine.AmbientAoes().Any(a => a.Name == "RagingClaw"), "Claw early clear.");
            f.Engine.Effect(10, 40613, 6, f.Now); Check(!f.Engine.AmbientAoes().Any(a => a.Name == "RagingClaw"), "Six hits.");
            for (uint i = 1; i <= 29; ++i) f.Engine.Effect(11, 40609, i, f.Now);
            Check(f.Engine.AmbientAoes().Any(a => a.Name == "BoulderDance"), "Boulder early clear.");
            f.Engine.Effect(11, 40608, 30, f.Now); Check(!f.Engine.AmbientAoes().Any(a => a.Name == "BoulderDance"), "Thirty hits.");
        });
        run("Yuweyawata crater risk at exactly finish minus 2.5 radar remains visible", () =>
        {
            using var f = Prepare(0x464C); Cast(f, 10, 40605, 10);
            Check(!Group(f, "simple").Single().Navigate && f.Draw.Previews.Count == 1, "Initial risk.");
            f.Now = f.Now.AddSeconds(7.5); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "simple").Single().Navigate && f.Snapshot.DangerAreas.Count > 0, "Risk transition.");
        });
        foreach (var angles in new float[][] { [0, 0, 0, 4.8f], [0, 0, 0, -1.8f], [0, MathF.PI / 4, MathF.PI / 4, 0], [0, 0, 0, 0] })
            run($"Yuweyawata earth small rotation source arithmetic branch {angles.Sum()}", () =>
            {
                using var f = Prepare(0x464C); var now = f.Now;
                for (var i = 0; i < 4; ++i) Cast(f, (ulong)(10 + i), 40662, rotation: angles[i]);
                var a = Group(f, "earth"); Check(a.Length == 16 && a.Count(x => Near(x.Activation, now, 6.5)) == 4, "Four spirals.");
                var total = angles.Sum(); var angle = angles[0];
                if ((int)total is not (4 or -1))
                {
                    var inter = BossMod.Angle.AnglesIntercardinals.Any(x => x.AlmostEqual(new(angle), BossMod.Angle.DegToRad));
                    angle += ((int)(2 * total) == 3 ? (inter ? -45 : -135) : (inter ? 45 : -45)) * MathF.PI / 180;
                }
                var expected = BossMod.WPos.RotateAroundOrigin(angle * BossMod.Angle.RadToDeg, new(34, -710), new(31.8f, -715.5f));
                Check(a.Where(x => Near(x.Activation, now, 6.7)).Any(x => Vector2.Distance(new(x.Position.X, x.Position.Z), new(expected.X, expected.Z)) < .001f), "Source rotation.");
                Cast(f, 20, 40606); Check(Group(f, "earth").Length == 16, "Actual cast duplicated predictions."); Finish(f, 20);
                Check(Group(f, "earth").Length == 15, "FIFO.");
            });
        run("Yuweyawata earth big first ten dynamic rolling visibility and visual start clock", () =>
        {
            using var f = Prepare(0x464C); var now = f.Now; Cast(f, 10, 40661, 30, .5f);
            var a = Group(f, "earth"); Check(a.Length == 10 && Near(a[0].Activation, now, 4.5) && Near(a[9].Activation, now, 6.75), "Visual clock.");
            for (var i = 0; i < 11; ++i) { Cast(f, (ulong)(20 + i), 40606); Finish(f, (ulong)(20 + i)); }
            Check(Group(f, "earth").Length == 9 && Near(Group(f, "earth")[0].Activation, now, 7.25), "Rolling window.");
        });
        foreach (var clockwise in new[] { true, false })
            run($"Yuweyawata rock source cardinal direction {clockwise} fifteen predictions first eight", () =>
            {
                using var f = Prepare(0x464C); f.Actors[10] = new(10, 0x233C, new(34, 0, -697));
                Cast(f, 10, 40611, 1, clockwise ? BossMod.Angle.AnglesCardinals[0].Rad : 0);
                var a = Group(f, "rock"); var p = BossMod.WPos.RotateAroundOrigin(clockwise ? 22.5f : -22.5f, new(34, -710), new(34, -697));
                Check(a.Length == 8 && Near(a[1].Activation, f.Now, 1.6) && Vector2.Distance(new(a[1].Position.X, a[1].Position.Z), new(p.X, p.Z)) < .001f, "Direction/clock.");
                Cast(f, 11, 40611, 1); Check(Group(f, "rock").Length == 8, "Extra prediction.");
                Finish(f, 11); Check(Near(Group(f, "rock")[0].Activation, f.Now, 1.6), "Any finish FIFO.");
            });
        run("Yuweyawata Jagged Edge special keep behind boss one deep fifty half width original dead targets", () =>
        {
            using var f = Prepare(0x464C); Cast(f, 10, 40613); Cast(f, 11, 40615, target: 101);
            var a = Group(f, "jagged").Single(); var native = a.Shape.Resolve(a.Position, 0, null);
            var boss = f.Actors[1]; var original = new BossMod.SDInvertedRect(new(boss.Position.X, boss.Position.Z), new BossMod.Angle(boss.Rotation), default, 1, 50);
            Compare(native, p => original.Contains(new(p.X, p.Z)), 18, .41f);
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Check(Group(f, "jagged").Length == 1, "Raw spread condition.");
            f.PlayerId = 101; Scan(f); Check(Group(f, "jagged").Length == 0, "Raw IsSpreadTarget.");
        });
        foreach (var boss in new uint[] { 0x4612, 0x46A4, 0x4652, 0x4616, 0x4619, 0x461B })
            run($"Yuweyawata {boss:X} trash opposite pull end bounds retains detached living group", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                f.Actors[2] = new(2, boss, c, InCombat: false, Targetable: false); f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PrimaryId == 2, "Primary death ended group.");
                f.Actors[2] = f.Actors[2] with { Position = c + Vector3.UnitX * 1000 }; Scan(f);
                Check((f.Engine.Active != null) == YuweyawataCatalog.BoundedPull(boss), "Opposite end bounds.");
                f.Actors[2] = f.Actors[2] with { Dead = true }; Scan(f); Check(f.Engine.Active == null, "Empty group.");
            });
        run("Yuweyawata actual target priorities no invented gaze shield or stop restrictions", () =>
        {
            using var f = Prepare(0x4641); f.Actors[5] = new(5, 0x4642, f.Actors[1].Position); Scan(f);
            Check(f.Engine.PriorityTarget(1, f.Actors[100].Position) == 5 && !f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Controls.");
        });
        run("Yuweyawata packet corrections preserve batch risk color and update locked geometry", () =>
        {
            using var f = Prepare(0x4641); Cast(f, 10, 41121); Cast(f, 11, 40625, 10);
            var before = Group(f, "batch")[0];
            var correction = f.Actors[10].Cast! with { Location = before.Position + Vector3.UnitX * 4, Rotation = 1.3f };
            f.Engine.StartCast(10, correction, f.Now); var a = Group(f, "batch")[0];
            Check(a.Position == correction.Location && a.Rotation == 1.3f && a.Navigate && a.Color == before.Color, "Batch correction.");
            Finish(f, 10); Check(Group(f, "batch").Length == 1, "Duplicate batch.");
        });
        run("Yuweyawata corrected party target clears and cached final position death survives removal", () =>
        {
            using var f = Prepare(0x464A); Cast(f, 10, 40649, target: 101);
            var correction = f.Actors[10].Cast! with { TargetId = 102 }; f.Engine.StartCast(10, correction, f.Now);
            f.Actors[102] = f.Actors[102] with { Position = f.Actors[102].Position + Vector3.UnitZ * 7 }; Scan(f);
            var p = Group(f, "party").Single(a => a.Display).Position;
            f.Actors.Remove(102); Scan(f); Check(Group(f, "party").Single(a => a.Display).Position == p, "Reverted to start.");
            Finish(f, 10); Check(Group(f, "party").Length == 0, "Corrected target leaked.");
            f.Now = f.Now.AddSeconds(.1); Cast(f, 11, 40649, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); f.Actors.Remove(101); Scan(f);
            Check(Group(f, "party").Length == 0, "Dead marker reappeared.");
        });
        run("Yuweyawata stack marked gathers within three and full circles turn dangerous outline avoid", () =>
        {
            using var f = Prepare(0x464A); Cast(f, 10, 40651, target: 100);
            var gather = Group(f, "party").Single(a => a.Id.EndsWith("/gather"));
            var shape = gather.Shape.Resolve(gather.Position, 0, null);
            Check(!shape.Contains(f.Actors[101].Position + Vector3.UnitZ * 2.8f, 0), "Not radius three.");
            Finish(f, 10); f.Now = f.Now.AddSeconds(.1);
            var c = f.Engine.Arena!.Position;
            for (var i = 101UL; i <= 104; ++i) f.Actors[i] = new(i, 0, c + Vector3.UnitX * ((i - 101) * .5f), PartySlot: (int)(i - 100), Hostile: false);
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10 }; Scan(f); Cast(f, 11, 40651, target: 101);
            var a = Group(f, "party"); Check(a.Any(a => a.Id.EndsWith("/avoid")) && !Group(f, "stack/join").Any(), "Full stack join.");
            Check(a.Single(a => a.Display) is { Safe: false, OutlineOnly: true }, "Full stack display.");
            f.Actors.Remove(104); Scan(f);
            Check(Group(f, "stack/join").Length == 1 && Group(f, "party").Single(a => a.Display).Safe, "Available slot.");
        });
        run("Yuweyawata spiral packet correction preserves clocks and consumed FIFO", () =>
        {
            using var f = Prepare(0x464C); Cast(f, 10, 40661, rotation: 0); var initial = Group(f, "earth");
            Cast(f, 11, 40606); Finish(f, 11);
            var correction = f.Actors[10].Cast! with { Rotation = MathF.PI / 2 }; f.Engine.StartCast(10, correction, f.Now);
            var a = Group(f, "earth"); var p = BossMod.WPos.RotateAroundOrigin(90, new(34, -710), new(28.7f, -708.2f));
            Check(a[0].Activation == initial[1].Activation && Vector2.Distance(new(a[0].Position.X, a[0].Position.Z), new(p.X, p.Z)) < .001f, "Big spiral correction.");
            using var g = Prepare(0x464C);
            for (var i = 20UL; i < 24; ++i) Cast(g, i, 40662, rotation: 0);
            correction = g.Actors[20].Cast! with { Rotation = 4.8f }; g.Engine.StartCast(20, correction, g.Now);
            var small = Group(g, "earth");
            Check(small.Length == 16, "Small correction duplicated output.");
            for (var i = 0; i < 4; ++i)
            {
                var at = g.Now.AddSeconds(6.5 + .2 * i);
                BossMod.WPos[] points = [new(34, -710), new(31.8f, -715.5f), new(35, -721.7f), new(41, -722.5f)];
                var correctedPoint = BossMod.WPos.RotateAroundOrigin(4.8f * BossMod.Angle.RadToDeg, new(34, -710), points[i]);
                Check(small.Any(a => a.Activation == at && Vector2.Distance(new(a.Position.X, a.Position.Z), new(correctedPoint.X, correctedPoint.Z)) < .001f), "Small corrected branch.");
                Check(small.Count(a => a.Activation == at && Vector2.Distance(new(a.Position.X, a.Position.Z), new(points[i].X, points[i].Z)) < .001f) >= 3, "Other spiral branch correction.");
            }
        });
        run("Yuweyawata unresolved corrected party target removes old marker without late revival", () =>
        {
            using var f = Prepare(0x464A); Cast(f, 10, 40649, target: 101);
            var cast = f.Actors[10].Cast! with { TargetId = 900 }; f.Engine.StartCast(10, cast, f.Now);
            Check(Group(f, "party").Length == 0, "Old marker retained.");
            f.Actors[900] = new(900, 0, f.Actors[101].Position, PartySlot: 4, Hostile: false); Scan(f); Finish(f, 10);
            Check(Group(f, "party").Length == 0, "Unresolved target revived marker.");
        });
        run("Yuweyawata misdirection event precedes raw snapshot acknowledgement without timer rollback", () =>
        {
            using var f = Prepare(0x464A); f.Engine.StatusChanged(100, 1422, true, f.Now); Scan(f);
            f.Now = f.Now.AddSeconds(3); Scan(f); Check(f.Engine.RequireYuweyawataMisdirection, "Stale snapshot undid gain.");
            f.Actors[100] = f.Actors[100] with { Statuses = [1422, 2936] }; Scan(f);
            f.Engine.StatusChanged(100, 1422, false, f.Now); Scan(f); Check(!f.Engine.RequireYuweyawataMisdirection, "Stale snapshot undid lose.");
            f.Now = f.Now.AddSeconds(3); Scan(f); Check(!f.Engine.RequireYuweyawataMisdirection, "Timer revived overlap.");
            f.Actors[100] = f.Actors[100] with { Statuses = [2936] }; Scan(f); Check(!f.Engine.RequireYuweyawataMisdirection, "Acknowledgement revived mask.");
        });
        run("Yuweyawata rock packet correction preserves consumed step and activation", () =>
        {
            using var f = Prepare(0x464C); f.Actors[10] = new(10, 0x233C, new(34, 0, -697));
            Cast(f, 10, 40611, rotation: 0); Cast(f, 11, 40611); Finish(f, 11);
            var before = Group(f, "rock")[0];
            var cast = f.Actors[10].Cast! with { Rotation = BossMod.Angle.AnglesCardinals[0].Rad };
            f.Engine.StartCast(10, cast, f.Now); var a = Group(f, "rock")[0];
            var p = BossMod.WPos.RotateAroundOrigin(22.5f, new(34, -710), new(34, -697));
            Check(a.Activation == before.Activation && a.Id == before.Id && Vector2.Distance(new(a.Position.X, a.Position.Z), new(p.X, p.Z)) < .001f, "Rock correction.");
        });
    }

    private static IEnumerable<(string Name, string Module, string Field)> Arenas()
    {
        yield return ("Bat", "D90ForestBat.D90ForestBat", "arena");
        yield return ("Woolback", "D90ForestWoolback.D90ForestWoolback", "arena");
        yield return ("Zaghnal", "D091LindblumZaghnal.D091LindblumZaghnal", "arena");
        yield return ("Researcher", "D90RottenResearcher.D90RottenResearcher", "arena");
        yield return ("Specter", "D90StationSpecter.D90StationSpecter", "arena");
        yield return ("Phoebad", "D90SprightlyPhoebad.D90SprightlyPhoebad", "arena");
        yield return ("Golem", "D90SprightlyClayGolem.D90SprightlyClayGolem", "arena");
        foreach (var (name, field) in new[] { ("Starting", "StartingBounds"), ("Default", "DefaultArena"), ("Tiny", "TinyArena"), ("PathsA", "ArenaENVC00800040"), ("PathsB", "ArenaENVC02000100") })
            yield return ("Kanilokka" + name, "D092OverseerKanilokka.D092OverseerKanilokka", field);
        foreach (var (name, field) in new[] { ("Starting", "startingBounds"), ("Default", "DefaultBounds"), ("Donut", "DonutBounds") })
            yield return ("Lunipyati" + name, "D093Lunipyati.cs.D093Lunipyati", field);
    }
    private static IEnumerable<(uint Boss, uint Action, BossMod.AOEShape Source)> Shapes()
    {
        yield return (0x4612, 40668, new BossMod.AOEShapeCone(9, new(MathF.PI / 4)));
        yield return (0x4612, 40665, new BossMod.AOEShapeRect(14, 2));
        yield return (0x46A4, 40666, new BossMod.AOEShapeCircle(8));
        yield return (0x4616, 40671, new BossMod.AOEShapeCone(7, new(MathF.PI / 3)));
        yield return (0x4616, 40672, new BossMod.AOEShapeCircle(6));
        yield return (0x4619, 41118, new BossMod.AOEShapeCone(12, new(MathF.PI / 3)));
        yield return (0x461B, 40675, new BossMod.AOEShapeCone(15, new(MathF.PI / 3)));
        yield return (0x461B, 40676, new BossMod.AOEShapeCircle(10));
        yield return (0x4641, 40638, new BossMod.AOEShapeCircle(6));
        yield return (0x464A, 40646, new BossMod.AOEShapeCircle(18));
        yield return (0x464C, 40610, new BossMod.AOEShapeCircle(25));
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1242); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i * 4), PartySlot: i, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double duration = 5, float rotation = .7f, ulong target = 0, DateTime? finishAt = null)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, f.Actors[id].Position, rotation, f.Now, finishAt ?? f.Now.AddSeconds(duration), target);
        f.Actors[id] = f.Actors[id] with { Cast = cast }; Scan(f); f.Engine.StartCast(id, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id) { f.Actors[id] = f.Actors[id] with { Cast = null }; f.Engine.Finish(id, endedAt: f.Now); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("yuwe/" + key)).ToArray();
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } value && Math.Abs((value - start).TotalSeconds - seconds) < .00001;
    private static Vector3 Q(Vector3 p) { var q = new BossMod.WPos(p.X, p.Z).Quantized(); return new(q.X, p.Y, q.Z); }
    private static void Compare(IGeometryShape native, Func<Vector3, bool> source, float range = 55, float step = 1.17f)
    {
        var world = HostDrawingGeometry.Convert(native);
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z);
                Check(native.Contains(p, 0) == source(p) && world.Contains(p, 0) == source(p), $"Dual geometry at {p}.");
            }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
