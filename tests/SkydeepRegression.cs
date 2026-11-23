using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class SkydeepRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Skydeep client territory discovery exact polygon both drawing views", () =>
        {
            Check(ArrDungeonCatalog.Create(1194).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.skydeep"), "Discovery.");
            var source = BossMod.Dawntrail.Dungeon.D03SkydeepCenote.D031FeatherRay.D031FeatherRay.CircleBounds;
            var native = SkydeepArenas.FeatherSmall(0); var center = native.Position;
            Compare(native, p => source.Contains(new(p.X - center.X, p.Z - center.Z)), 16, .17f);
            using var f = Prepare(0x41C7);
            Check(f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.RequireAllStop, "Invented shield or stop.");
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x41D3, 36728, new BossMod.AOEShapeCone(24, new(MathF.PI / 12))),
            (0x4184, 36445, new BossMod.AOEShapeCircle(35)),
            (0x4184, 38581, new BossMod.AOEShapeRect(42, 4)),
            (0x4184, 38582, new BossMod.AOEShapeRect(49, 4)),
            (0x4184, 38583, new BossMod.AOEShapeRect(35, 4)),
            (0x4184, 38584, new BossMod.AOEShapeRect(36, 4)),
            (0x4184, 38660, new BossMod.AOEShapeRect(10, 5)),
            (0x4184, 38661, new BossMod.AOEShapeRect(10, 5)),
            (0x4184, 38662, new BossMod.AOEShapeRect(10, 5)),
            (0x4184, 38663, new BossMod.AOEShapeRect(10, 5)),
            (0x41C7, 36676, new BossMod.AOEShapeCircle(10)),
            (0x41C7, 36666, new BossMod.AOEShapeCircle(10)),
            (0x41C7, 36706, new BossMod.AOEShapeCircle(10)),
            (0x41C7, 36683, new BossMod.AOEShapeCircle(8)),
        })
            run($"Skydeep {action} source geometry both drawing projections real finish no timeout", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var cast = f.Engine.OutputCasts.Single().Value;
                Compare(cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null),
                    p => source.Check(new(p.X, p.Z), new(cast.Location.X, cast.Location.Z), new(cast.Rotation)));
                f.Now = f.Now.AddSeconds(40); f.Engine.Tick(f.Now);
                Check(f.Engine.OutputCasts.Count() == 1, "Clock cleared active caster.");
                Finish(f, 10); Check(!f.Engine.OutputCasts.Any(), "Actual finish did not clear.");
            });
        run("Skydeep small bubble timeline duplicate entries dynamic capsule obstacles destruction identity", () =>
        {
            using var f = Prepare(0x41D3); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x999, c, HitboxRadius: 1.1f); Scan(f);
            f.Engine.Timeline(20, 0x1E46, f.Now); f.Engine.Timeline(20, 0x1E46, f.Now);
            Check(Group(f, "bubble").Length == 6, "Timeline must append duplicate actor references.");
            f.Engine.Timeline(20, 0x1E3C, f.Now);
            f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitX * 2, Rotation = .7f, Dead = true }; Scan(f);
            var aoe = Group(f, "bubble").Single(a => a.Display);
            Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null),
                p => new BossMod.AOEShapeCapsule(1.1f, 3).Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(.7f)), 8, .17f);
            Check(Group(f, "bubble").Single(a => !a.Display && !a.Obstacle).Activation == f.Now.AddSeconds(1.5), "Prediction time.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); Scan(f);
            Check(Group(f, "bubble").Length == 3, "Destroyed reference cleared.");
            f.Actors[20] = new(20, 0x41D4, c, HitboxRadius: 1.1f); Scan(f);
            f.Engine.Timeline(20, 0x1E3C, f.Now);
            Check(Group(f, "bubble").Single(a => a.Display).Position == aoe.Position, "New instance removed or moved retired bubble.");
            f.Engine.Timeline(20, 0x1E46, f.Now); f.Engine.Timeline(20, 0x1E3C, f.Now);
            Check(Group(f, "bubble").Length == 3, "New instance remove selected old reference.");
        });
        run("Skydeep bubble center cast hold and burst offset entire group finish", () =>
        {
            using var f = Prepare(0x41D3); var c = f.Engine.Arena!.Position;
            Cast(f, 1, 36732); Check(Group(f, "bubble/center").Length == 1, "Center missing.");
            Finish(f, 1); Check(Group(f, "bubble/center").Length == 0, "Center held after finish.");
            f.Actors[20] = new(20, 0x41D4, c, HitboxRadius: 2.2f);
            f.Actors[21] = new(21, 0x41D4, c, HitboxRadius: 1.1f); Scan(f);
            Cast(f, 1, 36736);
            Check(Group(f, "burst").Single().Position == c + Vector3.UnitX * 8
                && Math.Abs((Group(f, "burst").Single().Activation!.Value - f.Now).TotalSeconds - 8.4) < .00001, "West offset/delay/radius filter.");
            Finish(f, 1); Cast(f, 1, 36737);
            Check(Group(f, "burst").Length == 2 && Group(f, "burst")[1].Position == c - Vector3.UnitX * 8, "East append.");
            Cast(f, 20, 36738); Check(Group(f, "burst").Length == 2 && !f.Engine.OutputCasts.Any(c => c.Value.Rule.Action == 36738 && c.Value.Rule.Shape != null), "Burst duplicated cast geometry.");
            Finish(f, 20); Check(Group(f, "burst").Length == 0, "Burst must clear all.");
        });
        run("Skydeep bubble snapshot removal preserves old instance on same id recreation", () =>
        {
            using var f = Prepare(0x41D3); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x41D4, c, HitboxRadius: 1.1f); Scan(f);
            f.Engine.Timeline(20, 0x1E46, f.Now);
            f.Actors.Remove(20); Scan(f);
            f.Actors[20] = new(20, 0x41D4, c + Vector3.UnitX * 8, HitboxRadius: 1.1f); Scan(f);
            f.Engine.Timeline(20, 0x1E3C, f.Now);
            Check(Group(f, "bubble").Single(a => a.Display).Position == c, "Snapshot disappearance reattached old instance.");
        });
        run("Skydeep player wave rotation PR facing current perspective actual effect cleanup", () =>
        {
            using var f = Prepare(0x41D3); var c = f.Engine.Arena!.Position;
            f.Actors[102] = new(102, 0, c, PartySlot: 2, Buddy: true);
            f.Actors[103] = new(103, 0, c, PartySlot: 3, Dead: true); Scan(f);
            Cast(f, 1, 36728);
            Check(Group(f, "wave").Length == 2 && Group(f, "wave").All(a => a.Activation == f.Now.AddSeconds(6.3)), "Wave party filter/clock.");
            var own = Group(f, "wave").Single(a => a.Id.EndsWith("/1"));
            Check(!own.Navigate && own.OutlineOnly && f.Engine.GazeFacing(c, MathF.PI / 2) == null, "Early facing.");
            f.Now = f.Start.AddSeconds(5.56); Scan(f);
            var facing = f.Engine.GazeFacing(c, MathF.PI / 2);
            Check(facing != null && MathF.Abs(facing.Value - MathF.PI / 2) > .1f, "PR facing did not avoid teammate.");
            f.Actors[100] = f.Actors[100] with { Rotation = MathF.PI }; Scan(f);
            Check(Group(f, "wave").Single(a => a.OutlineOnly).Rotation == MathF.PI, "Self fan not dynamic.");
            f.PlayerId = 101; Scan(f);
            Check(Group(f, "wave").Single(a => a.OutlineOnly).Position == f.Actors[101].Position, "Perspective output.");
            Finish(f, 1); Check(Group(f, "wave").Length == 2, "Boss finish prematurely cleared player wave.");
            f.Engine.Effect(10, 36729, 1, f.Now); Check(Group(f, "wave").Length == 0, "Wave real effect.");
        });
        run("Skydeep ring confirmed shrink restore and artillery six tenths map gate", () =>
        {
            using var f = Prepare(0x41D3); Cast(f, 1, 36733); Finish(f, 1);
            Check(Group(f, "ring").Length == 1, "Ring vanished on cast finish.");
            f.Engine.MapEffect(0x13, 0x00020001); Check(Group(f, "ring").Length == 0 && f.Engine.Arena is LocalPolygonGeometry, "Circle map.");
            f.Engine.MapEffect(0x13, 0x00080004); Check(f.Engine.Arena!.GetBounds().MaxX == -89.5f, "Square restore.");
            using var g = Prepare(0x4184); Cast(g, 1, 36448);
            Check(Near(Group(g, "shrink").Single().Activation, g.Now, 5.6), "Shrink delay.");
            Finish(g, 1); Check(Group(g, "shrink").Length == 1, "Cast finish shrank without map.");
            g.Engine.MapEffect(0x14, 0x00020001); Check(Group(g, "shrink").Length == 0 && g.Engine.Arena!.GetBounds().MaxX == -65, "Artillery arena.");
        });
        run("Skydeep stone order exact two FIFO yellow persistent risk and impact middle strip", () =>
        {
            using var f = Prepare(0x41C7); Cast(f, 10, 36671, duration: 9); Cast(f, 11, 36670, duration: 7);
            var stones = Group(f, "stone").Where(a => a.Display).ToArray();
            Check(stones.Length == 2 && stones[0].Id.Contains("/11/") && stones[0].Navigate && stones[1].Navigate
                && stones[0].Color == new Vector4(1, 1, 0, 1), "Stone order/risk.");
            Cast(f, 12, 36667, duration: 6);
            var join = Group(f, "stone/join").Single();
            Check(join.Activation == f.Now.AddSeconds(6), "Impact timestamp not used.");
            var shape = join.Shape.Resolve(join.Position, 0, null);
            Check(!shape.Contains(join.Position, 0) && shape.Contains(join.Position + Vector3.UnitX * 2, 0), "Middle strip.");
            Finish(f, 10); Check(Group(f, "stone").Single(a => a.Display).Id.Contains("/10/"), "Must FIFO rather than remove matching source.");
            Finish(f, 11); Check(Group(f, "stone").Length == 0, "Stone end.");
        });
        foreach (var action in new uint[] { 36679, 36680, 36681, 36682 })
            run($"Skydeep shatter {action} observed finish capped elapsed plus15400ms six second visibility", () =>
            {
                using var f = Prepare(0x41C7); Cast(f, 1, action, duration: 5.3);
                f.Now = f.Start.AddSeconds(5.3); Finish(f, 1);
                Check(Group(f, "shatter").Length == 0, "Shatter visible early.");
                f.Now = f.Start.AddSeconds(14.699); f.Engine.Tick(f.Now); Check(Group(f, "shatter").Length == 0, "Six second boundary early.");
                f.Now = f.Start.AddSeconds(14.701); f.Engine.Tick(f.Now);
                var all = Group(f, "shatter");
                Check(all.Length == (action is 36679 or 36680 ? 1 : 2)
                    && all.All(a => Near(a.Activation, f.Start, 20.7)), "Finish capped clock.");
                foreach (var a in all)
                {
                    var expected = new BossMod.AOEShapeRect(action is 36679 or 36680 ? 40 : 45, action is 36679 or 36680 ? 10 : 11);
                    Compare(a.Shape.Resolve(a.Position, a.Rotation, null), p => expected.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)));
                }
                if (action is 36681 or 36682)
                    Check(all[0].Position == new Vector3(91.539f, 0, -453.025f) && all[1].Position == new Vector3(108.415f, 0, -453.025f)
                        && all[0].Rotation == -17.004f * MathF.PI / 180 && all[1].Rotation == 16.999f * MathF.PI / 180, "Side anchors/rotations.");
                else
                    Check(all[0].Position == f.Actors[1].Position && all[0].Rotation == .7f, "Locked center anchor/rotation.");
                Cast(f, 12, 36685); Check(Group(f, "shatter").Length == all.Length, "Real cast should not duplicate.");
                Finish(f, 12); Check(Group(f, "shatter").Length == 0, "Real Shatter finish clears all.");
            });
        foreach (var (ended, route) in new[] { (2d, "finish"), (5d, "scan"), (5.3d, "remove") })
            run($"Skydeep shatter finish branch {route} at{ended} and shared first entry window", () =>
            {
                using var f = Prepare(0x41C7); Cast(f, 10, 36679, duration: 5.3);
                f.Now = f.Start.AddSeconds(ended);
                if (route == "remove") { f.Engine.RemoveActor(10, f.Now); f.Actors.Remove(10); }
                else if (route == "scan") { f.Actors[10] = f.Actors[10] with { Cast = null }; Scan(f); }
                else Finish(f, 10);
                Cast(f, 11, 36681, duration: 5.3); f.Now = f.Now.AddSeconds(5.3); Finish(f, 11);
                var expected = ended <= 5 ? 20.4 : 20.7;
                f.Now = f.Start.AddSeconds(expected - 5.999); f.Engine.Tick(f.Now);
                var all = Group(f, "shatter");
                Check(all.Length == 3 && Near(all[0].Activation, f.Start, expected)
                    && all[1].Activation > f.Now.AddSeconds(6), "Shared visibility window or capped branch.");
                Cast(f, 12, 36686); Finish(f, 12); Check(Group(f, "shatter").Length == 0, "Whole list resolution.");
            });
        foreach (var action in new uint[] { 36677, 36667, 36707 })
            run($"Skydeep impact {action} precise square inverse landing preview immunity navigation remains", () =>
            {
                using var f = Prepare(0x41C7); var c = f.Engine.Arena!.Position;
                Cast(f, 10, action, location: c - Vector3.UnitZ * 10);
                var a = Group(f, $"impact/{action}/nav").Single();
                var shape = a.Shape.Resolve(a.Position, 0, null); var distance = action == 36707 ? 20f : 18f;
                Compare(shape, p =>
                {
                    var v = p - (c - Vector3.UnitZ * 10); v.Y = 0;
                    var end = v.LengthSquared() == 0 ? p : p + Vector3.Normalize(v) * distance;
                    return MathF.Abs(end.X - c.X) > 19 || MathF.Abs(end.Z - c.Z) > 19;
                }, 19, .31f);
                Check(Group(f, "impact").Count(a => a.Display) == 2, "Landing display.");
                f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; Scan(f);
                Check(Group(f, "impact").All(a => !a.Display) && Group(f, $"impact/{action}/nav").Length == 1, "Immunity must only remove preview.");
            });
        run("Skydeep heat independent stack spread eligibility origin history active spread only", () =>
        {
            using var f = Prepare(0x41C7); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36677); Finish(f, 10); Cast(f, 11, 36709, target: 101);
            Check(Group(f, "spread/origin").Length == 0, "Origin cached without active spread.");
            Cast(f, 12, 36667, location: c);
            Check(Group(f, "spread/origin").Single().Activation == f.Now.AddSeconds(5), "Active spread origin.");
            Finish(f, 12); Check(Group(f, "spread/origin").Length == 1, "Historical origin cleared.");
            Cast(f, 13, 36710, target: 100);
            Check(Group(f, "stack/13/gather").Length == 1, "Independent spread excluded gather target.");
            Cast(f, 14, 36707);
            var a = Group(f, "impact/36707/nav").Single(); var shape = a.Shape.Resolve(a.Position, 0, null);
            Check(shape.Contains(c + Vector3.UnitZ * 2, 0), "Heat did not narrow impact square.");
            Finish(f, 13); Check(Group(f, "stack").Length == 0, "Stack finish.");
        });
        run("Skydeep concurrent different knockback components start independently", () =>
        {
            using var f = Prepare(0x41C7); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36677, location: c + Vector3.UnitZ * 30);
            Cast(f, 11, 36667, location: c - Vector3.UnitX * 10);
            var circles = Group(f, "impact").Where(a => a.Display && a.Shape is CircleTemplate).ToArray();
            Check(circles.Length == 2 && Vector3.Distance(circles[0].Position, c - Vector3.UnitZ * 18) < .001f
                && Vector3.Distance(circles[1].Position, c + Vector3.UnitX * 18) < .001f, "Components incorrectly chained.");
        });
        run("Skydeep independent four player towers actual occupancy damage and delayed raidwide effect", () =>
        {
            using var f = Prepare(0x41C7); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36688, location: c); Cast(f, 11, 36689, location: c + Vector3.UnitX * 12);
            Check(Group(f, "tower").Count(a => !a.Display) == 2 && f.Engine.PredictedDamage.Count() == 2, "Independent towers/damage.");
            f.Engine.Effect(20, 36690, 1, f.Now); Check(Group(f, "tower").Count(a => a.Display) == 2, "Repeat cleared towers.");
            Finish(f, 10); Check(Group(f, "tower").Count(a => a.Display) == 1, "Tower wrong group clear.");
            Finish(f, 11); Cast(f, 1, 36711);
            Check(f.Engine.PredictedDamage.Count() == 2 && f.Engine.PredictedDamage.All(p => Near(p.Activation, f.Now, 7.2)), "Ashlayer delay.");
            Finish(f, 1); Check(f.Engine.PredictedDamage.Count() == 2, "Ashlayer visual cleared prediction.");
            f.Engine.Effect(20, 36712, 2, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Raidwide actual effect.");
        });
        run("Skydeep tankbuster target death allowed source death hides output reset clears all", () =>
        {
            using var f = Prepare(0x41C7); Cast(f, 10, 39122, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(Group(f, "buster").Count(a => a.Display) == 1 && f.Engine.PredictedDamage.Count() == 1, "Dead target filtered.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            Check(Group(f, "buster").Length == 0 && !f.Engine.PredictedDamage.Any(), "Dead source not filtered.");
            f.Engine.ResetFight(); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1194); f.Engine.ResetFight();
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
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("sky/" + key)).ToArray();
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
