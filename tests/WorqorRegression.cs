using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class WorqorRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Worqor discovery exact client territory clipped arenas and both projections", () =>
        {
            Check(ArrDungeonCatalog.Create(1193).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.worqor"), "Discovery.");
            foreach (var (type, field, native) in new (Type, string, IGeometryShape)[]
            {
                (typeof(BossMod.Dawntrail.Dungeon.D02WorqorZormor.D021RyoqorTerteh.D021RyoqorTerteh), "StartingBounds", WorqorArenas.RyoqorStarting(0)),
                (typeof(BossMod.Dawntrail.Dungeon.D02WorqorZormor.D021RyoqorTerteh.D021RyoqorTerteh), "DefaultBounds", WorqorArenas.RyoqorSmall(0)),
                (typeof(BossMod.Dawntrail.Dungeon.D02WorqorZormor.D022Kahderyor.D022Kahderyor), "DefaultBounds", WorqorArenas.Kahderyor(0)),
            })
            {
                var bounds = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                var world = HostDrawingGeometry.Convert(native); var c = native.Position;
                Check(c.X == bounds.Center.X && c.Z == bounds.Center.Z, "Resolved center.");
                for (var x = -24.137f; x < 24; x += .37f)
                    for (var z = -24.231f; z < 24; z += .37f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = bounds.Contains(new(p.X - c.X, p.Z - c.Z));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Clipped boundary.");
                    }
            }
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x4159, 36278, new BossMod.AOEShapeRect(50, 3)),
            (0x415D, 36290, new BossMod.AOEShapeRect(50, 1)),
            (0x415D, 36293, new BossMod.AOEShapeCircle(15)),
            (0x415D, 36294, new BossMod.AOEShapeDonut(8, 40)),
            (0x415F, 36302, new BossMod.AOEShapeRect(4, 2)),
        })
            run($"Worqor {action} source geometry dual drawing no clock timeout real finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var c = f.Engine.OutputCasts.Single().Value;
                Compare(c.Rule.Shape!.Resolve(c.Location, c.Rotation, null), p => source.Check(new(p.X, p.Z), new(c.Location.X, c.Location.Z), new(c.Rotation)));
                f.Now = f.Now.AddSeconds(40); f.Engine.Tick(f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Premature timeout.");
                Finish(f, 10); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Actual finish.");
            });
        run("Worqor frozen visual circle and rect hidden without matching tether tutorial two then four", () =>
        {
            using var f = Prepare(0x4159);
            for (var round = 0; round < 3; ++round)
            {
                for (ulong id = 10; id < 16; ++id) Cast(f, id, id % 2 == 0 ? 36270u : 36271u);
                Check(Group(f, "frozen").Length == 0, "No tether should publish nothing.");
                f.Engine.Tether(1, 272, true, f.Now, 99);
                Check(Group(f, "frozen").Length == 0, "Unmatched tether counted.");
                f.Engine.Tether(1, 272, true, f.Now, 10); f.Render();
                var active = Group(f, "frozen");
                Check(active.Length == (round < 2 ? 2 : 4), "Tutorial prefix.");
                Check(active[0].Activation == f.Now.AddSeconds(14.9), "Freeze clock.");
                Compare(active[0].Shape.Resolve(active[0].Position, active[0].Rotation, null),
                    p => new BossMod.AOEShapeRect(20, 10).Check(new(p.X, p.Z), new(active[0].Position.X, active[0].Position.Z), new(active[0].Rotation)));
                for (ulong id = 10; id < 16; ++id) Finish(f, id);
                Check(Group(f, "frozen").Length == 0, "Round cleanup.");
                f.Now = f.Now.AddSeconds(6);
            }
        });
        run("Worqor frozen exact tether threshold stable correction and no untether reset", () =>
        {
            using var f = Prepare(0x4159);
            Cast(f, 10, 36270, duration: 10); Cast(f, 11, 36271, duration: 9); Cast(f, 12, 36270, duration: 8);
            f.Engine.Tether(1, 272, true, f.Now, 10);
            Check(Group(f, "frozen")[0].Id.Contains("/10/"), "Sorted before threshold.");
            f.Engine.Tether(1, 272, true, f.Now.AddSeconds(1), 11);
            Check(Group(f, "frozen")[0].Id.Contains("/12/"), "Did not sort entire list at second tether.");
            f.Engine.Tether(1, 272, false, f.Now.AddSeconds(2), 10);
            Finish(f, 12); var a = Group(f, "frozen");
            Check(a.Single(x => x.Id.Contains("/10/")).Activation == f.Now.AddSeconds(14.9), "Untether reverted freeze.");
            var observation = f.Actors[10].Cast! with { Location = f.Actors[10].Position + Vector3.UnitX };
            f.Engine.StartCast(10, observation, f.Now); f.Render();
            Check(Group(f, "frozen").Single(x => x.Id.Contains("/10/")).Activation == f.Now.AddSeconds(14.9), "Packet correction reverted freeze.");
            f.Engine.Effect(10, 36272, 1, f.Now);
            Check(Group(f, "frozen").Length == 2, "Damage effect advanced before finish.");
        });
        run("Worqor snow first six preserve insertion order after removed slot reused", () =>
        {
            using var f = Prepare(0x4159);
            for (ulong i = 10; i < 18; ++i) Cast(f, i, 36278);
            Check(f.Engine.OutputCasts.Count() == 6, "Snow max.");
            Finish(f, 10); Cast(f, 18, 36278);
            Check(f.Engine.OutputCasts.Select(c => c.Value.Source).Order().SequenceEqual(new ulong[] { 11, 12, 13, 14, 15, 16 }), "Dictionary slot changed ordering.");
        });
        run("Worqor shrink delayed six tenths real map resolution and restored centers", () =>
        {
            using var a = Prepare(0x4159); Cast(a, 10, 36280);
            Check(Math.Abs((Group(a, "shrink").Single().Activation!.Value - a.Now).TotalSeconds - 5.6) < .00001, "Ice shrink delay.");
            Finish(a, 10); Check(Group(a, "shrink").Length == 1, "Finish erased shrink.");
            a.Engine.MapEffect(0x17, 0x00020001, a.Now); a.Render();
            Check(Group(a, "shrink").Length == 0 && a.Engine.Arena!.Position.Z == 119.1f
                && a.Engine.Arena.Contains(new(-108, 0, 139.05f), 0), "Map shrink must retain initial center.");
            using var b = Prepare(0x415F); Cast(b, 10, 36375);
            var shape = Group(b, "shrink").Single(); var c = b.Engine.Arena!.Position;
            Check(shape.Shape.Resolve(c, 0, null).Contains(c + Vector3.UnitX * 24.8f, 0), "25 outer halfwidth.");
            b.Engine.MapEffect(0x18, 0x00020001, b.Now); b.Render();
            Check(b.Engine.Arena is RectangleGeometry { Width: 40, Length: 40 } && Group(b, "shrink").Length == 0, "Square shrink.");
        });
        foreach (var (index, state, field) in new (uint, uint, string)[]
        {
            (0x20, 0x00800040, "ENVC20Inverted"), (0x20, 0x00200010, "ENVC20"),
            (0x21, 0x00800040, "ENVC21Inverted"), (0x21, 0x00200010, "ENVC21"),
        })
            run($"Worqor environmental {field} exact union both views inverse navigation role zone", () =>
            {
                using var f = Prepare(0x415D); f.Engine.MapEffect(index, state, f.Now); f.Render();
                var type = typeof(BossMod.Dawntrail.Dungeon.D02WorqorZormor.D022Kahderyor.D022Kahderyor).Assembly
                    .GetType("BossMod.Dawntrail.Dungeon.D02WorqorZormor.D022Kahderyor.WindEarthShot")!;
                var source = (BossMod.AOEShapeCustom)type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var a = Group(f, "wind/display").Single(); var shape = a.Shape.Resolve(a.Position, a.Rotation, null);
                Compare(shape, p => source.Check(new(p.X, p.Z), new(-52.625f, -57.5f), default), range: 75);
                Check(a.Activation == f.Now.AddSeconds(5.9), "Environmental clock.");
                if (state == 0x00800040)
                {
                    var nav = Group(f, "wind/join").Single().Shape.Resolve(a.Position, 0, null);
                    for (var x = -17.13f; x < 17; x += .83f)
                        for (var z = -17.23f; z < 17; z += .83f)
                        {
                            var p = f.Engine.Arena!.Position + new Vector3(x, 0, z);
                            Check(nav.Contains(p, 0) == !shape.Contains(p, 0), "Inverted crystal union.");
                        }
                    Check(!a.Navigate && a.Color != null && Group(f, "wind/nontank").Length == 0, "Safe display vs nav.");
                }
                else
                {
                    Check(Group(f, "wind/nontank").Length == 1, "NonTank zone.");
                    f.Actors[100] = f.Actors[100] with { PartyRole = "Tank" }; Scan(f);
                    Check(Group(f, "wind/tank").Length == 1 && Group(f, "wind/nontank").Length == 0, "Tank zone.");
                }
                f.Engine.MapEffect(99, 0x00200010, f.Now); Check(Group(f, "wind/display").Single() == a, "Unknown map replaced AOE.");
                f.Engine.MapEffect(99, 0x04000004, f.Now); f.Render();
                Check(Group(f, "wind").Length == 0, "Global map reset.");
            });
        run("Worqor donut icon actor dynamic moon rings custom eligible buddy join map cleared safe", () =>
        {
            using var f = Prepare(0x415D);
            f.Actors[101] = f.Actors[101] with { Position = new(-43, 0, -57) }; Scan(f);
            f.Engine.Icon(100, 511, f.Now, actorId: 101);
            f.Engine.MapEffect(0x1E, 0x00800040, f.Now); f.Render();
            var a = Group(f, "donut");
            Check(a.Count(x => x.Display) == 1 && a.Single(x => x.Display).Shape is DonutTemplate { InnerRadius: 5, OuterRadius: 10 }
                && a.Single(x => x.Display).Activation == f.Now.AddSeconds(6), "Icon actor/moon fill.");
            var nav = a.Single(x => !x.Display).Shape.Resolve(Vector3.Zero, 0, null);
            Check(!nav.Contains(f.Actors[101].Position, 0) && nav.Contains(f.Actors[101].Position + Vector3.UnitX * 2, 0), "Buddy R1.66.");
            f.Engine.MapEffect(0, 0x02000001, f.Now); f.Render();
            Check(Group(f, "donut").Length == 1 && f.Draw.Previews.Count == 1, "Map empty crashed or erased donut display.");
            f.Engine.Effect(10, 36296, 1, f.Now, target: 999); f.Render();
            Check(Group(f, "donut").Length == 0, "Unmatched hit must clear all.");
            f.Engine.Icon(101, 511, f.Now); f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(Group(f, "donut").Length == 0, "Dead icon target retained.");
        });
        run("Worqor tower hard join occupancy damage and no invented shield", () =>
        {
            using var f = Prepare(0x415D); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x415E, c, HitboxRadius: 1.4f);
            f.Actors[102] = new(102, 0, c, PartySlot: 2, Hostile: false);
            f.Actors[103] = new(103, 0, c, PartySlot: 3, Hostile: false); Scan(f); Cast(f, 10, 36153);
            Check(Group(f, "tower/join").Length == 1 && Group(f, "tower/10/display").Single().Safe, "Tower hard join.");
            Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Raidwide") == 3, "Tower inside damage mask.");
            Check(f.Engine.PriorityTarget(1, c) == 20 && f.Engine.ForbiddenTargets.Length == 0, "Crystal attack priority/false shield.");
            Finish(f, 10); Check(Group(f, "tower").Length == 0, "Tower finish.");
        });
        run("Worqor gaze PR facing starts at 750ms survives immunity and clears on observed finish", () =>
        {
            using var f = Prepare(0x415D); Cast(f, 10, 36297, location: f.Actors[100].Position + Vector3.UnitZ * 10);
            Check(f.Engine.GazeFacing(f.Actors[100].Position, 0) == null, "Early facing.");
            f.Now = f.Now.AddSeconds(4.25); f.Engine.Tick(f.Now); f.Render();
            var facing = f.Engine.GazeFacing(f.Actors[100].Position, 0);
            Check(facing != null && MathF.Abs(facing.Value) > MathF.PI / 4 && f.Engine.Gazes.Count() == 1, "Gaze control.");
            var eye = Group(f, "gaze").Single(a => a.Id.EndsWith("/eye"));
            Check(!eye.Safe && Group(f, "gaze").Length == 3 && Group(f, "gaze").All(a => !a.Navigate), "Eye status/pupil/facing arc.");
            f.Actors[100] = f.Actors[100] with { Rotation = MathF.PI }; Scan(f);
            Check(Group(f, "gaze").Single(a => a.Id.EndsWith("/eye")).Safe, "Eye danger did not change with facing.");
            Finish(f, 10); Check(f.Engine.GazeFacing(f.Actors[100].Position, 0) == null, "Gaze lease ending.");
        });
        run("Worqor tether packet absent stale snapshot then confirmed snapshot counts one freeze", () =>
        {
            using var f = Prepare(0x4159); Cast(f, 10, 36270, duration: 8); Cast(f, 11, 36271, duration: 9);
            f.Engine.Tether(1, 272, true, f.Now, 10);
            f.Now = f.Now.AddSeconds(.1); Scan(f);
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [272] = 1 } };
            f.Now = f.Now.AddSeconds(.1); Scan(f);
            Check(Group(f, "frozen")[0].Id.Contains("/10/")
                && Group(f, "frozen")[0].Activation == f.Start.AddSeconds(14.9), "Stale snapshot recounted or postponed freeze.");
            f.Engine.Tether(1, 272, true, f.Now, 11); f.Render();
            Check(Group(f, "frozen")[0].Id.Contains("/10/"), "Real second tether failed sorting.");
        });
        run("Worqor Allfire exact sixteen sort display prefix strict deadline FIFO color persistence", () =>
        {
            using var f = Prepare(0x415F);
            for (ulong i = 10; i < 26; ++i) Cast(f, i, 36303, duration: 5 + (25 - i) / 4d);
            var a = Group(f, "fire");
            Check(a.Length == 12 && a[0].Activation == f.Now.AddSeconds(5) && a.Count(x => x.Navigate) == 4
                && a.Where(x => x.Navigate).All(x => x.Color == new Vector4(1, 1, 0, 1)), "Sort/prefix/risk/color.");
            f.Engine.Effect(25, 36303, 1, f.Now);
            Check(Group(f, "fire").Length == 12, "Effect advanced fire.");
            Finish(f, 10); a = Group(f, "fire");
            Check(a[0].Id.Contains("/24/") && a.Count(x => x.Navigate) == 4
                && !a.Single(x => x.Activation == f.Now.AddSeconds(6.25)).Navigate, "FIFO or strict first+1 deadline.");
            for (ulong i = 11; i < 26; ++i) Finish(f, i);
            Check(Group(f, "fire").Length == 0, "Final fire.");
            Cast(f, 30, 36307);
            a = Group(f, "fire");
            Check(a.Length == 2 && a.Single(x => x.Display).Shape is RayRectangleTemplate { Length: 15 }
                && !a.Single(x => x.Display).Navigate && a.Single(x => x.Display).Color != null, "Second phase safe rectangle.");
            var safe = a.Single(x => x.Display); var nav = a.Single(x => !x.Display);
            Check(!nav.Shape.Resolve(nav.Position, 0, null).Contains(safe.Position + new Vector3(MathF.Sin(safe.Rotation), 0, MathF.Cos(safe.Rotation)) * 5, 0),
                "Safe rectangle inverted direction.");
            Finish(f, 30); Check(Group(f, "fire").Length == 0, "Flood ending.");
        });
        foreach (var action in new uint[] { 36307, 36310, 39074 })
            run($"Worqor {action} knockback packet heading immunity only hides preview custom nav remains", () =>
            {
                using var f = Prepare(0x415F); var c = f.Engine.Arena!.Position;
                f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 2 };
                f.Actors[20] = new(20, 0x4160, c + Vector3.UnitX * 15, EventState: 1); Scan(f);
                Cast(f, 10, action, rotation: MathF.PI / 2, location: c);
                var display = Group(f, "knockback").Where(a => a.Display).ToArray();
                Check(display.Length == 2, "Landing and arrow.");
                var landing = display.Single(a => a.Shape is CircleTemplate).Position;
                Check(MathF.Abs(landing.X - (c.X + (action == 36307 ? 27 : 17))) < .001f, "Knockback type/distance/heading.");
                f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(6) } }; Scan(f);
                Check(Group(f, "knockback").All(a => !a.Display) && Group(f, "knockback").Any(a => a.Navigate), "Immunity removed custom positioning.");
                Finish(f, 10); Check(Group(f, "knockback").Length == 0, "Knockback finish.");
            });
        run("Worqor windswrath one exact projected square discrete forbidden", () =>
        {
            using var f = Prepare(0x415F); Cast(f, 10, 36310);
            var nav = Group(f, "knockback/36310/nav").Single().Shape.Resolve(Vector3.Zero, 0, null);
            var c = f.Engine.Arena!.Position; var source = new BossMod.SDKnockbackInAABBSquareAwayFromOrigin(new(c.X, c.Z), new(c.X, c.Z), 15, 19);
            for (var x = -20.13f; x < 20; x += .53f)
                for (var z = -20.23f; z < 20; z += .53f)
                {
                    var p = c + new Vector3(x, 0, z);
                    Check(nav.Contains(p, 0) == source.Contains(new(p.X, p.Z)), "Discrete knockback zone.");
                }
        });
        foreach (var pattern in new[] { false, true })
            run($"Worqor windswrath two pattern {pattern} exact creation early circle late four fans", () =>
            {
                using var f = Prepare(0x415F); var c = f.Engine.Arena!.Position;
                f.Actors[20] = new(20, 0x4160, new(-40, 0, pattern ? -210 : -180), Dead: true); Scan(f);
                Cast(f, 10, 39074, duration: 10, location: c);
                var nav = Group(f, "knockback/39074/nav").Single().Shape.Resolve(Vector3.Zero, 0, null);
                Check(!nav.Contains(c + Vector3.UnitX * 7, 0), "Early eight.");
                f.Now = f.Now.AddSeconds(7); f.Engine.Tick(f.Now); f.Render();
                nav = Group(f, "knockback/39074/nav").Single().Shape.Resolve(Vector3.Zero, 0, null);
                var angle = (pattern ? 15 : -15) * MathF.PI / 180;
                Check(!nav.Contains(c + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 4, 0)
                    && nav.Contains(c + Vector3.UnitX * 6, 0), "Late four fans or range.");
                Check(Group(f, "whirlwind").Length == 6 && Group(f, "whirlwind").Single(a => a.Obstacle).Shape is CircleTemplate { Radius: 5 }, "Dead wind filtered.");
                f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f);
                Check(Group(f, "knockback/39074/nav").Length == 0 && Group(f, "whirlwind").Length == 0, "Inactive wind retained.");
            });
        run("Worqor orb shifted axial square goal union Sprint dead filter", () =>
        {
            using var f = Prepare(0x415F); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x4162, c, Rotation: MathF.PI / 4); Scan(f);
            var goal = Group(f, "orb/join").Single();
            var nav = goal.Shape.Resolve(goal.Position, 0, null);
            var origin = c + new Vector3(MathF.Sqrt(.125f), 0, MathF.Sqrt(.125f));
            Check(nav.Contains(origin + new Vector3(.49f, 0, .49f), 0) && !nav.Contains(origin + Vector3.UnitX * .51f, 0), "Orb square rotated or no shift.");
            Check(f.Snapshot.GoalAreas.Count == 1 && goal.Safe, "Soft orb attraction must use goal channel.");
            Check(f.Engine.WantsWorqorSprint && Group(f, "orb").Single(a => a.Display).OutlineOnly, "Sprint/safe outline.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; Scan(f);
            Check(!f.Engine.WantsWorqorSprint && Group(f, "orb").Length == 0, "Dead orb.");
        });
        run("Worqor line stack marker real last hit dynamic target correction no middle resolution", () =>
        {
            using var f = Prepare(0x415F); f.Engine.Effect(1, 36315, 1, f.Now, 101); f.Render();
            Check(Group(f, "line").Single(a => a.Display).Activation == null
                && f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 2, "Line stack/predicted Shared.");
            f.Engine.Effect(1, 36314, 2, f.Now, 101); f.Render();
            Check(Group(f, "line").Any(a => a.Display), "Intermediate hit cleared stack.");
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[1].Position + Vector3.UnitZ * 8 }; Scan(f);
            Check(Group(f, "line").Single(a => a.Display).Rotation == 0, "Dynamic line target.");
            f.Engine.Effect(1, 39260, 3, f.Now, 100); f.Render();
            Check(Group(f, "line").Length == 0, "Actual last target correction.");
        });
        run("Worqor reset destroys cast state personal output and source ownership without BMR production", () =>
        {
            using var f = Prepare(0x4159); Cast(f, 10, 36270); f.Engine.Tether(1, 272, true, f.Now, 10); f.Render();
            Check(Group(f, "frozen").Length == 1, "Frozen setup.");
            f.Engine.RemoveActor(10, f.Now); f.Actors.Remove(10); f.Render();
            Check(Group(f, "frozen").Length == 0, "Destruction must finish frozen.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset cleanup.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1193); f.Engine.ResetFight();
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
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("worqor/" + key)).ToArray();
    private static void Compare(IGeometryShape native, Func<Vector3, bool> expected, float range = 55)
    {
        var world = HostDrawingGeometry.Convert(native);
        for (var x = -range + .137f; x < range; x += 1.17f)
            for (var z = -range + .231f; z < range; z += 1.17f)
            {
                var p = native.Position + new Vector3(x, 0, z); var wanted = expected(p);
                Check(native.Contains(p, 0) == wanted && world.Contains(p, 0) == wanted, $"Shape at {p}.");
            }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
