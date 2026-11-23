using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class TsukuyomiRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Tsukuyomi fan first seven actual endings source reuse and no predicted timeout", () =>
        {
            using var f = Scene();
            for (ulong i = 10; i < 18; ++i) f.Actors[i] = new(i, 0x2241, new(100, 0, 100));
            f.Scan();
            for (ulong i = 10; i < 18; ++i) Start(f, i, 11245);
            Check(Group(f, "tsukuyomi/fan").Length == 7, "Fan first-seven cap differs.");
            End(f, 10, 11245); Start(f, 10, 11245);
            Check(Group(f, "tsukuyomi/fan").All(a => a.Id != "tsukuyomi/fan/10")
                && Group(f, "tsukuyomi/fan").Any(a => a.Id == "tsukuyomi/fan/17"), "Reused source jumped FIFO.");
            f.Now = f.Start.AddSeconds(100); f.Scan();
            Check(Group(f, "tsukuyomi/fan").Length == 7, "Estimated cast expiry erased active fan.");
            f.Engine.Effect(11, 11245, 1, f.Now); f.Render();
            Check(Group(f, "tsukuyomi/fan").Any(a => a.Id == "tsukuyomi/fan/11"), "Effect erased active cast.");
            End(f, 11, 11245);
            Check(!Group(f, "tsukuyomi/fan").Any(a => a.Id == "tsukuyomi/fan/11"), "Real finish leaked.");
        });
        run("Tsukuyomi torment follows caster and target includes dead target until source finish", () =>
        {
            using var f = Scene();
            Start(f, 10, 11235, 101);
            var display = Group(f, "tsukuyomi/torment").Single(a => a.Display);
            var shape = display.Shape.Resolve(display.Position, display.Rotation, null);
            Check(shape.Contains(new(114.9f, 0, 100)) && !shape.Contains(new(115.1f, 0, 100))
                && f.Engine.PredictedDamage.Single().TargetId == 101, "Torment radius/bait prediction differs.");
            f.Actors[101] = f.Actors[101] with { Position = new(100, 0, 110), Dead = true }; f.Scan();
            display = Group(f, "tsukuyomi/torment").Single(a => a.Display);
            Check(display.Shape.Resolve(display.Position, display.Rotation, null).Contains(new(100, 0, 114)),
                "Dead target hid bait or dynamic aim was locked.");
            f.Engine.Effect(10, 11235, 1, f.Now); f.Render();
            Check(Group(f, "tsukuyomi/torment").Any(), "Effect consumed cast bait.");
            End(f, 10, 11235); Check(!Group(f, "tsukuyomi/torment").Any(), "Finish leaked bait.");
            Start(f, 10, 11955, 101);
            Check(!Group(f, "tsukuyomi/torment").Any(), "Inactive action11955 invented a component.");
        });
        run("Tsukuyomi marker effect line source target fixed five shared prediction and actual resolve", () =>
        {
            using var f = Scene();
            f.Now = f.Start.AddSeconds(2);
            f.Engine.Effect(10, 11441, 1, f.Now, 101, occurredAt: f.Start); f.Render();
            var line = Group(f, "tsukuyomi/line").Single(a => a.Display);
            var shape = line.Shape.Resolve(line.Position, line.Rotation, null);
            Check(shape.Contains(new(169, 0, 103.9f)) && !shape.Contains(new(169, 0, 104.1f))
                && Group(f, "tsukuyomi/line/join").Single().Activation == f.Start.AddSeconds(5),
                "Line source geometry or dispatched event clock differs.");
            Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 3
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(5)), "Line shared prediction missing.");
            Start(f, 1, 11238, 101); End(f, 1, 11238);
            f.Now = f.Start.AddSeconds(30); f.Scan();
            Check(Group(f, "tsukuyomi/line").Any(), "Cast finish or timeout cleared event marker.");
            f.Engine.Effect(1, 11238, 2, f.Now, 102); f.Render();
            Check(!Group(f, "tsukuyomi/line").Any(), "Single line final-target correction failed.");
        });
        run("Tsukuyomi multiple line markers consume only matching target and marked buddy rules", () =>
        {
            using var f = Scene();
            f.Engine.Effect(10, 11441, 1, f.Now, 100);
            f.Engine.Effect(11, 11441, 2, f.Now, 101); f.Render();
            Check(Group(f, "tsukuyomi/line").Count(a => a.Display) == 2
                && Group(f, "tsukuyomi/line").Any(a => a.Id.EndsWith("/avoid"))
                && !Group(f, "tsukuyomi/line/join").Any(), "Marked player tried joining another marker.");
            f.Engine.Effect(1, 11238, 3, f.Now, 999); f.Render();
            Check(Group(f, "tsukuyomi/line").Count(a => a.Display) == 2, "Unmatched resolve cleared multiple markers.");
            f.Engine.Effect(1, 11238, 4, f.Now, 101); f.Render();
            Check(Group(f, "tsukuyomi/line").Count(a => a.Display) == 1, "Matched resolve did not consume one.");
        });
        run("Tsukuyomi lunacy cast strict circle source ends independent of icon and follow up hit", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 305, f.Now); f.Render();
            Check(!Group(f, "tsukuyomi/lunacy").Any(), "Commented triple icon invented stack.");
            Start(f, 1, 11259, 101);
            Check(Group(f, "tsukuyomi/lunacy").Single(a => a.Display).Shape is CircleTemplate { Radius: 6 }
                && !f.Engine.PredictedDamage.Any(), "Lunacy radius or default mask prediction differs.");
            f.Engine.Effect(1, 11260, 1, f.Now, 101); f.Render();
            Check(Group(f, "tsukuyomi/lunacy").Any(), "Follow-up consumed stack.");
            End(f, 1, 11259); Check(!Group(f, "tsukuyomi/lunacy").Any(), "Real cast end leaked stack.");
        });
        run("Tsukuyomi moon latches gain side east precedence and either actual loss clears", () =>
        {
            using var f = Scene();
            Moon(f, 100, 1538, 3, new(90, 0, 100));
            Check(MoonContains(f, new(90, 0, 100)) && !MoonContains(f, new(110, 0, 100)), "West gain not latched.");
            f.Actors[100] = f.Actors[100] with { Position = new(110, 0, 100) }; f.Scan();
            Check(MoonContains(f, new(90, 0, 100)), "Moving without gain changed history.");
            Moon(f, 100, 1539, 3, new(110, 0, 100));
            Check(MoonContains(f, new(110, 0, 100)) && !MoonContains(f, new(90, 0, 100)), "East did not take precedence.");
            f.Actors[100] = f.Actors[100] with { StatusParams = new Dictionary<uint, ushort> { [1538] = 3 },
                Statuses = [1538], StatusExpirations = new Dictionary<uint, DateTime> { [1538] = f.Start.AddSeconds(30) } }; f.Scan();
            Check(!Group(f, "tsukuyomi/moon").Any(), "Loss of one status retained other latch.");
        });
        run("Tsukuyomi moon actual slots refresh threshold lower stacks center and perspective history", () =>
        {
            using var f = Scene();
            Moon(f, 100, 1538, 3, new(90, 0, 100));
            f.Actors[100] = f.Actors[100] with { Position = new(110, 0, 100),
                StatusExpirations = new Dictionary<uint, DateTime> { [1538] = f.Start.AddSeconds(31) } }; f.Scan();
            Check(MoonContains(f, new(90, 0, 100)), "One-second extension invented gain.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1538] = f.Start.AddSeconds(32.1) } }; f.Scan();
            Check(MoonContains(f, new(110, 0, 100)), "Greater-than-one-second refresh did not gain.");
            Moon(f, 101, 1539, 3, new(90, 0, 100)); f.PlayerId = 101; f.Scan();
            Check(MoonContains(f, new(90, 0, 100)), "Perspective used another player's side history.");
            f.PlayerId = 100; f.Scan(); Check(MoonContains(f, new(110, 0, 100)), "Perspective erased previous player's history.");
            Moon(f, 100, 1538, 2, new(90, 0, 100)); Check(MoonContains(f, new(110, 0, 100)), "Lower stack cleared latch.");
            f.Now = f.Start.AddSeconds(100); f.Scan();
            Check(Group(f, "tsukuyomi/moon").Any(), "Predicted expiry replaced actual status removal.");
            Check(Group(f, "tsukuyomi/moon").All(a => !a.Display), "Moon status invented half-field graphics.");
        });
        run("Tsukuyomi moon packets do not lose to stale snapshots before acknowledgement", () =>
        {
            using var f = Scene();
            f.Engine.StatusChanged(100, 1538, true, f.Now, 3); f.Render();
            f.Now = f.Start.AddSeconds(3); f.Scan();
            Check(MoonContains(f, new(90, 0, 100)), "Stale raw lost packet gain.");
            Moon(f, 100, 1538, 3, new(90, 0, 100));
            f.Engine.StatusChanged(100, 1538, false, f.Now); f.Render();
            f.Now = f.Start.AddSeconds(6); f.Scan();
            Check(!Group(f, "tsukuyomi/moon").Any(), "Stale raw revived packet loss.");
        });
        run("Tsukuyomi duplicate packet cannot swallow a later raw duration refresh", () =>
        {
            using var f = Scene(); Moon(f, 100, 1538, 3, new(90, 0, 100));
            f.Actors[100] = f.Actors[100] with { Position = new(110, 0, 100) }; f.Scan();
            f.Engine.StatusChanged(100, 1538, true, f.Now, 3);
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1538] = f.Start.AddSeconds(32) } }; f.Scan();
            Check(MoonContains(f, new(110, 0, 100)), "Duplicate packet swallowed raw gain.");
            f.Engine.StatusChanged(100, 1538, false, f.Now); f.Render();
            f.Actors[100] = f.Actors[100] with { StatusParams = null, StatusExpirations = null, Statuses = [] }; f.Scan();
            f.Actors[100] = f.Actors[100] with { Position = new(90, 0, 100) }; f.Scan();
            f.Engine.StatusChanged(100, 1538, true, f.Now, 3, f.Start.AddSeconds(40)); f.Render();
            f.Actors[100] = f.Actors[100] with { Position = new(110, 0, 100), StatusParams = new Dictionary<uint, ushort> { [1538] = 3 },
                StatusExpirations = new Dictionary<uint, DateTime> { [1538] = f.Start.AddSeconds(42) } }; f.Scan();
            Check(MoonContains(f, new(110, 0, 100)), "Newer raw refresh blocked pending packet forever.");
        });
        run("Tsukuyomi Lunacy same-target ends consume first marker and danger hints remain", () =>
        {
            using var f = Scene(); Start(f, 10, 11235, 101);
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("扇形死刑"), "Global tankbuster hint missing.");
            End(f, 10, 11235);
            Start(f, 10, 11259, 101); f.Now = f.Start.AddSeconds(1); Start(f, 11, 11259, 101);
            End(f, 11, 11259);
            Check(Group(f, "tsukuyomi/lunacy/join").Single().Activation == f.Start.AddSeconds(6.3), "Lunacy finish consumed source instead of first target.");
            f.Actors[11] = f.Actors[11] with { Position = new(80, 0, 100) }; f.Scan();
            f.Engine.Effect(10, 11441, 1, f.Now, 100); f.Engine.Effect(11, 11441, 2, f.Now, 101); f.Render();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("远离其他直线分摊"), "Overlapping marked line hint missing.");
        });
        foreach (var (action, radius, degrees) in new[] { (11239u, 70f, 90f), (11436u, 39f, 75f),
            (11437u, 39f, 75f), (11257u, 70f, 210f), (11258u, 70f, 210f) })
            run($"Tsukuyomi exact cone {action} radar and world geometry", () =>
            {
                using var f = Scene(); Start(f, 10, action);
                var shape = f.Draw.Previews["cast/10"].Geometry;
                var angle = (degrees / 2 - .1f) * MathF.PI / 180;
                var p = new Vector3(100 + MathF.Sin(angle) * (radius - .1f), 0, 100 + MathF.Cos(angle) * (radius - .1f));
                Check(shape.Contains(p) && HostDrawingGeometry.Convert(shape).Contains(p)
                    && !shape.Contains(new(100, 0, 100 + radius + .1f)), "Cone dual view differs.");
            });
        run("Tsukuyomi lunar halo raidwide adds hidden primary and death phase ownership", () =>
        {
            using var f = Scene(); Start(f, 10, 11379);
            var shape = f.Draw.Previews["cast/10"].Geometry;
            Check(!shape.Contains(new(105.9f, 0, 100)) && shape.Contains(new(106.1f, 0, 100))
                && !shape.Contains(new(114.1f, 0, 100)), "Donut estimates differ.");
            End(f, 10, 11379);
            foreach (var action in new[] { 11234u, 11246u, 11440u, 11256u, 11897u, 11243u })
            {
                Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 3, "Raidwide omitted: " + action);
                End(f, 10, action);
            }
            f.Actors[20] = new(20, 0x2242, new(100, 0, 105)); f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Actors[100].Position) == 20 && f.Engine.ForbiddenTargets.Length == 0, "Adds priority differs.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Now = f.Start.AddSeconds(10001); f.Scan();
            Check(f.Engine.Active != null && ArrDungeonCatalog.IsScript("masterplayer.trial.tsukuyomi"), "Hidden phase ended session.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Death leaked output.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x2210, 778);
        f.Actors[1] = f.Actors[1] with { Position = new(100, 0, 100) };
        f.Actors[100] = new(100, 0, new(90, 0, 100), PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, new(110, 0, 100), PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, new(100, 0, 110), PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x18D6, new(100, 0, 100));
        f.Actors[11] = new(11, 0x18D6, new(100, 0, 100)); f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, new(100, 0, 100), 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static void Moon(Fight f, ulong id, uint status, ushort extra, Vector3 position)
    {
        var actor = f.Actors[id];
        var parameters = actor.StatusParams?.ToDictionary() ?? [];
        var expirations = actor.StatusExpirations?.ToDictionary() ?? [];
        parameters[status] = extra; expirations[status] = f.Start.AddSeconds(30);
        f.Actors[id] = actor with { Position = position, StatusParams = parameters, StatusExpirations = expirations, Statuses = parameters.Keys.ToArray() };
        f.Scan();
    }
    private static bool MoonContains(Fight f, Vector3 p)
    {
        var a = Group(f, "tsukuyomi/moon").Single();
        return a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p);
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
