using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class AlexandriaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Alexandria discovery territory precise ownership and initial arenas", () =>
        {
            Check(ArrDungeonCatalog.Create(1199).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.alexandria"), "Discovery.");
            foreach (var (boss, x, z) in new[] { (0x4173u, 22.5f, 17.5f), (0x416Au, 23f, 23f), (0x41CEu, 15.5f, 15.5f) })
            {
                Check(ArrDungeonCatalog.Owns(boss, 1199) && !ArrDungeonCatalog.Owns(boss, 1208), "Ownership.");
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                Compare(f.Engine.Arena, p => MathF.Abs(p.X - c.X) <= x && MathF.Abs(p.Z - c.Z) <= z, 25);
            }
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x416A, 36327, new BossMod.AOEShapeRect(90, 7.5f)),
            (0x416A, 36331, new BossMod.AOEShapeRect(90, 12.5f)),
            (0x416A, 36330, new BossMod.AOEShapeRect(90, 12.5f)),
            (0x416A, 39136, new BossMod.AOEShapeTriCone(40, new(MathF.PI / 4))),
            (0x416A, 39138, new BossMod.AOEShapeRect(55, 4)),
            (0x416A, 36336, new BossMod.AOEShapeCircle(6)),
            (0x41CE, 39007, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x41CE, 39238, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x41CE, 39249, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x41CE, 39615, new BossMod.AOEShapeRect(40, 5)),
            (0x41CE, 39616, new BossMod.AOEShapeDonut(6, 40)),
            (0x41CE, 39239, new BossMod.AOEShapeRect(50, 4)),
            (0x41CE, 36793, new BossMod.AOEShapeCircle(6)),
        })
            run($"Alexandria {action} native geometry matches source radar and world until real finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var aoe = Group(f, "simple").Single();
                Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null),
                    p => shape.Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation)));
                f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "simple").Length == 1, "Elapsed prediction cleared cast.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Real finish retained cast.");
            });
        foreach (var (boss, action, index, seconds, x, z) in new (uint, uint, uint, double, float, float)[]
        { (0x4173, 36378, 3, .8, 20, 15), (0x416A, 36338, 0x27, .5, 20, 20), (0x41CE, 36765, 0x28, .7, 15, 15) })
            run($"Alexandria {action} shrink preview clock actual map confirmation", () =>
            {
                using var f = Prepare(boss); var old = f.Engine.Arena; Cast(f, 10, action);
                Check(Near(Group(f, "shrink").Single().Activation, f.Now, 5 + seconds), "Shrink clock.");
                Finish(f, 10); f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now);
                Check(ReferenceEquals(old, f.Engine.Arena) && Group(f, "shrink").Length == 1, "Timer shrank arena.");
                f.Engine.MapEffect(index, 0x00020001, f.Now); f.Render();
                Check(Group(f, "shrink").Length == 0, "Map retained preview.");
                var c = f.Engine.Arena!.Position;
                Compare(f.Engine.Arena, p => MathF.Abs(p.X - c.X) <= x && MathF.Abs(p.Z - c.Z) <= z, 25);
                Cast(f, 11, action); Check(Group(f, "shrink").Length == 0, "Repeated shrink preview.");
            });
        run("Alexandria Amalgam boss visual does not preempt helper shrink", () =>
        {
            using var f = Prepare(0x416A); Cast(f, 1, 36337);
            Check(!f.Engine.AmbientAoes().Any() && !f.Engine.PredictedDamage.Any(), "Visual raidwide.");
            Cast(f, 10, 36338); Check(Group(f, "shrink").Length == 1 && f.Engine.PredictedDamage.Count() == 4, "Helper raidwide.");
        });
        run("Alexandria object creation mixed queue sorts locks positions and consumes finish FIFO", () =>
        {
            using var f = Prepare(0x4173); var c = f.Engine.Arena!.Position; var start = f.Now;
            f.Actors[20] = new(20, 0x4174, c + Vector3.UnitX * .1234f);
            f.Actors[21] = new(21, 0x4175, c); Scan(f);
            var q = Group(f, "queue"); Check(q.Count(a => a.Display) == 2 && Near(q[0].Activation, start, 9.9) && Near(q[1].Activation, start, 12.4), "Object queue clock.");
            Check(q[0].Navigate && !q[1].Navigate && q[0].Color?.Y == 1, "Donut-cross risk.");
            var locked = q[0].Position;
            f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitX * 8, Dead = true }; Scan(f);
            Check(Group(f, "queue")[0].Position == locked, "Creation position moved.");
            Cast(f, 10, 36379, 2, location: c); Check(Group(f, "cast").Single().Activation == start.AddSeconds(2), "Cone not sorted before objects.");
            f.Engine.Effect(10, 36379, 1, f.Now); Check(Group(f, "cast").Length == 1, "Effect consumed queue.");
            Finish(f, 10); Check(Group(f, "cast").Length == 0, "Cone finish did not consume first.");
            Cast(f, 21, 36383, 1); Finish(f, 21);
            Check(Group(f, "queue").Length == 1 && Group(f, "queue")[0].Navigate, "Purge finish FIFO not promoted.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); Scan(f);
            Check(Group(f, "queue").Length == 1, "Destroy without cast consumed prediction.");
        });
        run("Alexandria existing objects restored before casts and donut nav promotion after purge", () =>
        {
            using var f = Prepare(0x4173); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x4174, c); f.Actors[21] = new(21, 0x4174, c);
            f.Actors[10] = new(10, 0x233C, c, Cast: new(36381, c, 0, f.Now, f.Now.AddSeconds(30))); Scan(f);
            Check(Group(f, "queue").Count(a => a.Display) == 2 && Group(f, "cast").Length == 0, "Cast restored before object creation.");
            Cast(f, 20, 36382); Finish(f, 20);
            var head = Group(f, "queue").First(a => a.Display);
            Check(head.Navigate && head.Shape is DonutTemplate { InnerRadius: 4 }, "First donut did not recover normal nav.");
            Check(!Group(f, "queue").Any(a => a.Id.EndsWith("/nav")), "Widened inner retained after purge.");
        });
        run("Alexandria donut display inner four navigation inner five point five before first purge", () =>
        {
            using var f = Prepare(0x4173); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36381, 1); f.Actors[20] = new(20, 0x4174, c + Vector3.UnitX * 4); Scan(f);
            Finish(f, 10);
            var q = Group(f, "queue");
            Check(q.Single(a => a.Display).Shape is DonutTemplate { InnerRadius: 4 } && !q.Single(a => a.Display).Navigate, "Donut display.");
            Check(q.Single(a => !a.Display).Shape is DonutTemplate { InnerRadius: 5.5f }, "Donut navigation inner.");
            f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now); f.Render(); Check(Group(f, "queue").Length == 2, "Queue timeout.");
        });
        foreach (var first in new uint[] { 36379, 36381 })
            run($"Alexandria {first} center donut risk and packet correction preserves queue identity", () =>
            {
                using var f = Prepare(0x4173); var c = f.Engine.Arena!.Position;
                Cast(f, 10, first, 1); f.Actors[20] = new(20, 0x4174, c); Scan(f);
                Check(Group(f, "queue").Any(a => a.Navigate) == (first == 36381), "Small-cone center exception.");
                var old = f.Actors[10].Cast!;
                f.Engine.StartCast(10, old with { Location = c + Vector3.UnitX, Rotation = 1.2f }, f.Now);
                var cone = Group(f, "cast").Single();
                Check(cone.Rotation == 1.2f && cone.Position == c + Vector3.UnitX && Group(f, "queue").Count(a => a.Display) == 1, "Correction duplicated queue.");
            });
        run("Alexandria ternary step origin match actual finish clock and no event or timeout advance", () =>
        {
            using var f = Prepare(0x416A); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 39254, 4); Cast(f, 11, 39255, 6, location: c + Vector3.UnitX * 2);
            f.Engine.Effect(10, 39254, 1, f.Now); Check(Group(f, "ternary").Single().Shape is CircleTemplate, "Effect advanced.");
            Finish(f, 11); Check(Group(f, "ternary").Single().Shape is CircleTemplate, "Out-of-order advanced.");
            f.Now = f.Now.AddSeconds(4.8); Finish(f, 10);
            var a = Group(f, "ternary").Single(); Check(a.Shape is DonutTemplate { InnerRadius: 10 } && Near(a.Activation, f.Now, 2), "Finish event clock.");
            Cast(f, 12, 39255, location: c + Vector3.UnitX * 1.01f); Finish(f, 12);
            Check(Group(f, "ternary").Single().Shape is DonutTemplate { InnerRadius: 10 }, "Wrong origin advanced.");
            Cast(f, 13, 39255, location: c + Vector3.UnitX); f.Now = f.Now.AddSeconds(7); Finish(f, 13);
            Check(Group(f, "ternary").Single().Shape is DonutTemplate { InnerRadius: 20 }, "Different helper valid origin did not advance.");
            f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now); Check(Group(f, "ternary").Length == 1, "Timeout advanced.");
            Cast(f, 14, 39256); Finish(f, 14); Check(Group(f, "ternary").Length == 0, "Final finish.");
        });
        foreach (var reversed in new[] { false, true })
            run($"Alexandria stack forbidden original party slot both icon orders {reversed} early goal strict two seconds", () =>
            {
                using var f = Prepare(0x4173);
                f.Actors[101] = f.Actors[101] with { PartySlot = 6, PartyRole = "Healer" }; Scan(f);
                if (reversed) { f.Engine.Icon(101, 344, f.Now); f.Engine.Icon(100, 62, f.Now); }
                else { f.Engine.Icon(100, 62, f.Now); f.Engine.Icon(101, 344, f.Now); }
                f.PlayerId = 101; Scan(f);
                Check(Group(f, "stack").Any(a => a.Id.EndsWith("/avoid")) && !Group(f, "stack").Any(a => a.Id.EndsWith("/join")), "Forbidden original slot ignored.");
                Check(Group(f, "disinfection/goal").Single().GoalPriority == 1 && !f.Engine.PredictedDamage.Any(p => p.DamageType == "Tankbuster"), "Early goal/damage.");
                f.Now = f.Now.AddSeconds(5.1).AddSeconds(-2); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "disinfection/goal").Length == 1 && !Group(f, "disinfection").Any(a => !a.Display && !a.Safe), "Exactly two seconds changed AI.");
                f.Now = f.Now.AddTicks(1); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "disinfection/goal").Length == 0 && Group(f, "disinfection").Any(a => !a.Display && !a.Safe), "Late base AI absent.");
                Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Tankbuster") == 1, "Late buster prediction.");
                f.PlayerId = 100; Scan(f);
                var gather = (SohrKhaiWallHintTemplate)Group(f, "stack").Single(a => a.Id.EndsWith("/gather")).Shape;
                Check(!gather.Shelters.Any(a => a.Position == f.Actors[101].Position), "Stack target gathered at forbidden.");
                f.Engine.Effect(10, 36385, 1, f.Now, 101); f.Engine.Effect(10, 36386, 2, f.Now, 100);
                Check(Group(f, "stack").Length == 0 && Group(f, "disinfection").Length == 0, "Resolve cleanup.");
            });
        run("Alexandria explosion all displayed first four risky first two yellow then promotion", () =>
        {
            using var f = Prepare(0x41CE);
            for (ulong i = 10; i < 16; ++i) Cast(f, i, 39239);
            var a = Group(f, "simple"); Check(a.Length == 6 && a.Count(x => x.Navigate) == 4 && a.Count(x => x.Color != null) == 2, "Explosion windows.");
            Finish(f, 10); Finish(f, 11); Finish(f, 12); Finish(f, 13);
            a = Group(f, "simple"); Check(a.Length == 2 && a.All(x => x.Navigate && x.Color == null), "Promotion did not reset colors.");
        });
        run("Alexandria fifth purge recovers head before counter reset next donut keeps widened navigation", () =>
        {
            using var f = Prepare(0x4173); var c = f.Engine.Arena!.Position;
            for (ulong i = 20; i < 27; ++i) f.Actors[i] = new(i, 0x4174, c); Scan(f);
            for (ulong i = 20; i < 25; ++i) { Cast(f, i, 36382); Finish(f, i); }
            Check(Group(f, "queue").First(a => a.Display).Navigate && !Group(f, "queue").Any(a => a.Id.EndsWith("/nav")), "Fifth head not recovered before reset.");
            Cast(f, 10, 36381, 100); Finish(f, 10);
            Check(Group(f, "queue").Any(a => a.Id.EndsWith("/nav") && a.Shape is DonutTemplate { InnerRadius: 5.5f }), "Reset retained positive purge count.");
        });
        run("Alexandria forbidden player standing inside stack does not consume capacity and shared mask excludes slot", () =>
        {
            using var f = Prepare(0x4173); var c = f.Engine.Arena!.Position;
            f.Actors[101] = f.Actors[101] with { Position = c };
            f.Actors[102] = f.Actors[102] with { Position = c };
            f.PlayerId = 103; Scan(f);
            f.Engine.Icon(100, 62, f.Now); f.Engine.Icon(101, 344, f.Now);
            Check(Group(f, "stack/join").Length == 1, "Forbidden counted toward max three.");
            Check(f.Engine.PredictedDamage.Count() == 3 && !f.Engine.PredictedDamage.Any(a => a.TargetId == 101)
                && f.Engine.PredictedDamage.All(a => a.DamageType == "Shared"), "Stack mask/type.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f); Check(Group(f, "stack").Length == 0, "Dead stack active.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; Scan(f); Check(Group(f, "stack").Length > 0, "Revived stack cache lost.");
        });
        run("Alexandria independent line components own target and two yard gather candidate pools", () =>
        {
            using var f = Prepare(0x41CE); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c + Vector3.UnitZ * 10); Scan(f);
            f.Engine.Effect(10, 36778, 1, f.Now, 100); f.Engine.Effect(10, 36786, 2, f.Now, 101);
            Check(Group(f, "line/join/36787").Length == 1, "Different component marker made local player marked.");
            var gather = (SohrKhaiWallHintTemplate)Group(f, "line").Single(a => a.Id.EndsWith("/gather")).Shape;
            Check(gather.Shelters.Any(a => a.Position == f.Actors[101].Position) && gather.Shelters.All(a => a is CircleGeometry { Radius: 2 }),
                "Cross-component target excluded or wrong radius.");
        });
        run("Alexandria cast spread and stack consume first matching target and retain destroyed target cache", () =>
        {
            using var f = Prepare(0x416A);
            Cast(f, 10, 36334, 4, target: 101); Cast(f, 11, 36334, 9, target: 101); Finish(f, 11);
            Check(Group(f, "spread").Count(a => a.Display) == 1 && Near(Group(f, "spread").Single(a => a.Navigate).Activation, f.Now, 9), "Spread finish matched caster instead of target FIFO.");
            Cast(f, 12, 36333, 5, target: 100); Cast(f, 13, 36333, 8, target: 100); Finish(f, 13);
            Check(Group(f, "stack").Count(a => a.Display) == 1 && Near(Group(f, "stack").Single(a => a.Id.EndsWith("/gather")).Activation, f.Now, 8), "Stack target FIFO.");
            f.Engine.RemoveActor(101); f.Actors.Remove(101); Scan(f);
            Check(Group(f, "spread").Count(a => a.Display) == 1, "Undead destroyed spread target cache lost.");
            Check(!f.Engine.PredictedDamage.Any(), "Source-disabled cast resolve predictions added.");
        });
        foreach (var action in new uint[] { 36333, 36334 })
            run($"Alexandria {action} consumed FIFO entry cannot be resurrected by old cast correction", () =>
            {
                using var f = Prepare(0x416A);
                Cast(f, 10, action, 9, target: 100); var old = f.Actors[10].Cast!;
                Cast(f, 11, action, 4, target: 100); Finish(f, 11);
                f.Engine.StartCast(10, old with { Location = old.Location + Vector3.UnitX }, f.Now);
                Check(Group(f, action == 36333 ? "stack" : "spread").Count(a => a.Display) == 1, "Consumed entry resurrected.");
                Finish(f, 10);
                Check(Group(f, action == 36333 ? "stack" : "spread").Length == 0, "Corrected consumed FIFO left residual.");
            });
        run("Alexandria own spread does not advance another marked players activation", () =>
        {
            using var f = Prepare(0x416A);
            Cast(f, 10, 36334, 4, target: 100); Cast(f, 11, 36334, 9, target: 101);
            var nav = Group(f, "spread").Where(a => a.Navigate).ToArray();
            Check(!nav.Any(a => a.Id.EndsWith("/party/101")), "Other spread target duplicated at own timing.");
            Check(Near(nav.Single(a => a.Id.EndsWith("/avoid")).Activation, f.Now, 9), "Other spread clock changed.");
        });
        run("Alexandria Salvation target angular avoidance source formula and undefined near teammate boundary", () =>
        {
            using var f = Prepare(0x41CE); var c = f.Engine.Arena!.Position;
            f.PlayerId = 101; Scan(f); Cast(f, 10, 36783, target: 101);
            var nav = Group(f, "salvation").Where(a => a.Navigate).ToArray();
            Check(nav.Length == 2 && nav.All(a => a.Shape is FanTemplate), "Near source NaN cone or target circle.");
            foreach (var aoe in nav)
            {
                var memberId = ulong.Parse(aoe.Id.Split('/')[^1]); var member = f.Actors[memberId];
                var distance = Vector3.Distance(c, member.Position);
                var source = new BossMod.SDCone(new(c.X, c.Z), 100, new(aoe.Rotation), new(MathF.Asin(3 / distance)));
                var native = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
                for (var x = -20f; x < 20; x += .91f)
                    for (var z = -20f; z < 20; z += .93f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        Check(native.Contains(p, 0) == (source.Distance(new(p.X, p.Z)) <= 0), "Salvation angular navigation.");
                    }
            }
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f); Check(Group(f, "salvation").Length == 0, "Dead source active.");
            f.Actors[10] = f.Actors[10] with { Dead = false }; Scan(f); Check(Group(f, "salvation").Length > 0, "Source revival cache lost.");
        });
        foreach (var action in new uint[] { 39007, 39238, 39249, 39616 })
            run($"Alexandria Electray display survives {action} while only AI and damage pause", () =>
            {
                using var f = Prepare(0x41CE); Cast(f, 10, 39243, target: 101); Cast(f, 11, action);
                Check(Group(f, "spread").Count(a => a.Display) == 1 && !Group(f, "spread").Any(a => a.Navigate), "Spread disappeared or AI stayed.");
                Check(!f.Engine.PredictedDamage.Any(), "Suppressed spread damage.");
                Finish(f, 11);
                Check(Group(f, "spread").Any(a => a.Navigate) && Group(f, "spread/south").Length == 1 && !f.Engine.PredictedDamage.Any(), "AI not restored or source-disabled damage added.");
                Check(Group(f, "spread").First(a => !a.Display && a.Id.EndsWith("/avoid")).Shape is CircleTemplate { Radius: 7 }, "Spread nav radius.");
            });
        run("Alexandria Impact first custom nav inset fourteen immunity preview and Salvation suspension", () =>
        {
            using var f = Prepare(0x41CE); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36783, target: 101); Finish(f, 10);
            Check(Group(f, "salvation").Count(a => a.Navigate) == 1 && !f.Engine.PredictedDamage.Any(), "Salvation base/no invented damage.");
            Cast(f, 11, 36794, location: c + Vector3.UnitX * 10); Cast(f, 12, 36794, location: c - Vector3.UnitX * 10);
            Check(Group(f, "salvation").Count(a => a.Display) == 1 && !Group(f, "salvation").Any(a => a.Navigate), "Salvation hidden or AI not suspended.");
            var nav = Group(f, "kb/nav").Single();
            var source = new BossMod.SDKnockbackInAABBSquareAwayFromOrigin(new(c.X, c.Z), new(c.X + 10, c.Z), 15, 14);
            var native = nav.Shape.Resolve(nav.Position, nav.Rotation, null);
            for (var x = -13.863f; x < 14; x += .71f)
                for (var z = -13.769f; z < 14; z += .71f)
                {
                    var point = c + new Vector3(x, 0, z);
                    Check(native.Contains(point, 0) == source.Contains(new(point.X, point.Z)), "Knockback custom navigation.");
                }
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; Scan(f);
            Check(Group(f, "kb").Length == 1, "Immunity removed custom AI or retained preview.");
            Finish(f, 11); Check(!Group(f, "salvation").Any(a => a.Navigate), "Second Impact ignored.");
            Finish(f, 12); Check(Group(f, "salvation").Any(a => a.Navigate), "Salvation not restored after Impact.");
            f.Engine.Effect(1, 36784, 1, f.Now); Check(Group(f, "salvation").Length == 0, "Salvation hit did not clear all.");
        });
        run("Alexandria selective LB map cancellation line delays cached deaths and generator display", () =>
        {
            using var f = Prepare(0x41CE); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c); f.Actors[20] = new(20, 0x41D1, c, HitboxRadius: 3); Scan(f);
            f.Engine.Effect(10, 36778, 1, f.Now, 100); f.Engine.Effect(10, 36786, 2, f.Now, 101);
            Cast(f, 11, 36783, target: 100);
            Check(Group(f, "line").Where(a => a.Display).Any(a => Near(a.Activation, f.Now, 5))
                && Group(f, "line").Where(a => a.Display).Any(a => Near(a.Activation, f.Now, 5.5)), "Line delay.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(Group(f, "line").Count(a => a.Display) == 2 && Group(f, "salvation").Count(a => a.Display) == 1, "Dead target lost generic bait.");
            f.Engine.MapEffect(0x2F, 0x00080004, f.Now);
            Check(Group(f, "line").Count(a => a.Display) == 1 && Group(f, "salvation").Length == 0 && f.Engine.Active != null, "Map cleared unrelated output/session.");
            Check(Group(f, "generator").Length == 1 && f.Engine.ForbiddenTargets.Length == 0, "Generator invented target restrictions.");
            f.Engine.Effect(10, 36780, 3, f.Now, 0); Check(Group(f, "line").Any(), "Unknown replacement target consumed unique line.");
            f.Engine.Effect(10, 36780, 4, f.Now, 101); Check(Group(f, "line").Length == 0, "Single line target correction.");
        });
        foreach (var boss in new uint[] { 0x4173, 0x416A, 0x41CE })
            run($"Alexandria {boss:X} retains transient disengage resets on death and owner cleanup", () =>
            {
                using var f = Prepare(boss); f.Actors[1] = f.Actors[1] with { InCombat = false, Targetable = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Transient flags ended session.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && !f.Engine.AmbientAoes().Any(), "Death did not clean owner.");
            });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1199); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i * 4), PartySlot: i, Hostile: false);
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
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("alex/" + key)).ToArray();
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
