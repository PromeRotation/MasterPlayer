using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntForecastRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Pkuucha full circle cone FIFO float clocks and persistent hidden R14 navigation", Samba);
        run("SimpleHunts Forecaster all forecast climate matrices exact geometry risk promotion and effect FIFO", Forecast);
        run("SimpleHunts Forecast and Samba generations corrections instances lifecycle and missing facing", Events);
        run("SimpleHunts Forecast and Samba actual raw restarts retain historical generations and strict duration changes", Restarts);
        run("SimpleHunts Forecast ordinary geometry and Pkuucha single raidwide prediction", Ordinary);
    }

    private static HuntPendingCast Pending(uint oid, uint action, double at = 0, long order = 1)
    {
        var definition = SimpleHuntCatalog.Find(oid)!;
        var rule = definition.Casts.Single(c => c.Action == action);
        var input = new ReplayCast(action, 100, new(10, 0, 20), .2f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
        return new(1, definition, rule, input.Location, .2f, input.StartedAt, input.FinishAt, 100, false, true, rule.Shape,
            input, input, Order: order);
    }

    private static ReplayActorState Boss(uint oid, ReplayCast? cast = null, ulong id = 1)
        => new() { Id = id, Oid = oid, Hp = 1000, InCombat = true, Targetable = true, Cast = cast };

    private static HuntPendingCast[] Shapes(SimpleHuntEncounter encounter) => encounter.Areas.Where(a => a.Shape != null).ToArray();

    private static void Samba()
    {
        var state = new HuntMarchingSambaState();
        var cast = Pending(0x4580, 39797);
        state.Start(cast);
        var initial = state.Areas.ToArray();
        Check(initial.Length == 2 && initial.All(a => a.Risky)
            && initial[0].Activation == cast.Activation.AddSeconds(1.7f)
            && initial[1].Activation == cast.Activation.AddSeconds(5.7f), "Samba timings or risk differ.");
        Check(initial[0].Color == new Vector4(1, 1, 0, 1), "Samba head lost source color.");
        var cone = initial[1].Shape!.Resolve(Vector3.Zero, 0, null);
        Check(cone.Contains(new(39, 0, .1f), 0) && !cone.Contains(new(39, 0, -.1f), 0), "Samba cone is not total 180 degrees.");
        state.Finish(39797);
        Check(state.Areas.Count() == 2, "Visual finish consumed Samba.");
        state.Finish(39756);
        Check(state.Areas.Single().Shape is FanTemplate, "Samba finish matched a shape instead of FIFO.");
        state.Finish(39755);
        Check(!state.Areas.Any(), "Samba last finish leaked.");

        var encounter = new SimpleHuntEncounter();
        var boss = Boss(0x4580);
        var player = new ReplayActorState { Id = 100, Position = new(30, 0, 0) };
        encounter.Synchronize([boss, player], _ => true, Start);
        var nav = encounter.TargetDangers(player, []).Single();
        Check(!encounter.Areas.Any() && !encounter.TargetPreviews(player).Any()
            && !encounter.Hints(player).Any(), "The permanent R14 navigation invented a preview or GTFO hint.");
        var shape = nav.Shape!.Resolve(default, 0, null);
        Check(shape.Contains(new(14, 0, 0), 0) && shape.Contains(new(14.01f, 0, 0), 0)
            && !shape.Contains(new(13.99f, 0, 0), 0)
            && shape.IntersectsSegment(default, new(14, 0, 0), 0)
            && !shape.IntersectsSegment(default, new(13.99f, 0, 0), 0), "R14 complement boundary or segment endpoint differs.");
        var tangent = new HuntStackJoinGeometry([new(-3, 0, 0), new(3, 0, 0)], 3);
        Check(!tangent.Contains(new(-.0001f, 0, 0), 0) && !tangent.Contains(new(.0001f, 0, 0), 0)
            && tangent.Contains(default, 0) && tangent.IntersectsSegment(new(-.0001f, 0, 0), new(.0001f, 0, 0), 0),
            "A short segment missed the dangerous contact between two open safe circles.");
        encounter.Synchronize([boss with { Position = new(50, 0, 50) }, player], _ => true, Start.AddSeconds(1));
        shape = encounter.TargetDangers(player, []).Single().Shape!.Resolve(default, 0, null);
        Check(!shape.Contains(new(50, 0, 50), 0) && shape.Contains(default, 0), "R14 did not follow the current boss position.");
    }

    private static void Forecast()
    {
        foreach (var (forecast, original) in new (uint, string)[]
        {
            (38521, "CRD"), (38523, "RDC"), (38520, "DCX"), (38522, "XCR")
        })
        foreach (var (climate, from, to) in new (uint, char, char)[]
        {
            (0, '-', '-'), (39128, 'R', 'X'), (39125, 'X', 'D'), (39127, 'C', 'R'), (39126, 'D', 'C')
        })
        {
            var state = new HuntForecastState();
            state.Start(Pending(0x4397, 38526));
            Check(!state.Areas.Any(), "Late join created guessed forecast history.");
            state.Start(Pending(0x4397, forecast));
            var expected = original.ToCharArray();
            if (climate != 0)
            {
                state.Start(Pending(0x4397, climate));
                var index = Array.IndexOf(expected, from);
                if (index >= 0) expected[index] = to;
            }
            Check(!state.Areas.Any(), "Recording forecast or climate produced an AOE.");
            var release = Pending(0x4397, 38530, 10, 10);
            state.Start(release);
            for (var i = 0; i < 3; ++i)
            {
                var areas = state.Areas.ToArray();
                Check(areas.Length == Math.Min(2, 3 - i) && Kind(areas[0].Shape!) == expected[i]
                    && areas[0].Risky, "Forecast order or head risk differs.");
                Check(areas[0].Activation == release.Activation.AddSeconds(i switch { 0 => 0, 1 => 3.1f, _ => 6.1f }),
                    "Forecast clocks were rounded or rebased to effects.");
                if (areas.Length > 1)
                    Check(areas[1].Risky == !(expected[i] is 'C' or 'D' && expected[i + 1] is 'C' or 'D'),
                        "Forecast second AOE risk does not follow circle/donut pairing.");
                state.Effect(38527, Start.AddSeconds(100 + i));
            }
            Check(!state.Areas.Any(), "Forecast third effect leaked.");
            state.Start(Pending(0x4397, 38526, 200));
            Check(!state.Areas.Any(), "Consumed forecast history was reused.");
        }

        var once = new HuntForecastState();
        once.Start(Pending(0x4397, 38521));
        once.Start(Pending(0x4397, 39127));
        once.Start(Pending(0x4397, 39128));
        once.Start(Pending(0x4397, 38526));
        Check(string.Concat(once.Areas.Select(a => Kind(a.Shape!))) == "XR", "Climate replaced every matching shape instead of only the first.");
    }

    private static void Events()
    {
        foreach (var oid in new uint[] { 0x4397, 0x4580 })
        {
            var encounter = new SimpleHuntEncounter();
            var action = oid == 0x4397 ? 38521u : 39797u;
            var cast = Pending(oid, action).Input;
            var boss = Boss(oid, cast);
            encounter.Synchronize([boss, boss with { Id = 2 }], _ => true, Start);
            if (oid == 0x4397)
            {
                cast = Pending(oid, 38526, 10).Input;
                encounter.Start(1, cast, true);
                encounter.Start(2, cast, true);
            }
            encounter.Start(1, cast, true);
            var corrected = cast with { Location = new(20, 0, 30), Rotation = 1.2f, FinishAt = cast.FinishAt.AddMilliseconds(50) };
            encounter.Start(1, corrected, true);
            Check(Shapes(encounter).Where(a => a.Source == 1).All(a => a.Location == corrected.Location && a.Rotation == corrected.Rotation),
                "A same-generation correction failed to update released geometry.");
            encounter.Finish(1, cast.ActionId, Start.AddSeconds(20));
            Check(Shapes(encounter).Count(a => a.Source == 1) == 2, "A visual finish consumed a sequence.");
            if (oid == 0x4397)
            {
                encounter.Effect(1, 38526, Start.AddSeconds(21), 1);
                encounter.Effect(1, 38526, Start.AddSeconds(21), 1);
                Check(Shapes(encounter).Count(a => a.Source == 1) == 2, "Duplicate effect consumed the hidden third AOE.");
                encounter.Effect(1, 38525, Start.AddSeconds(22), 2);
                Check(Shapes(encounter).Count(a => a.Source == 1) == 1, "Second effect failed FIFO.");
            }
            encounter.Remove(1);
            Check(Shapes(encounter).All(a => a.Source == 2), "Removing one boss cleared or retained the wrong instance.");
            encounter.Synchronize([boss with { Id = 2, Hp = 0 }], _ => true, Start.AddSeconds(30));
            Check(!encounter.Active && !Shapes(encounter).Any(), "HP0 failed to release outputs.");
            encounter.Reset();
            encounter.Synchronize([Boss(oid, Pending(oid, oid == 0x4397 ? 38526u : 39797u).Input with { Rotation = null })], _ => true, Start);
            Check(!Shapes(encounter).Any(), "A direction-dependent sequence guessed a missing facing.");
        }
    }

    private static void Ordinary()
    {
        foreach (var (oid, action) in new (uint, uint)[] { (0x4580, 39757), (0x4397, 38534), (0x4397, 38535), (0x4397, 38533), (0x4397, 38532), (0x4397, 38536) })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Pending(oid, action).Input;
            var boss = Boss(oid, cast);
            encounter.Synchronize([boss], _ => true, Start);
            Check(Shapes(encounter).Length == 1, "Ordinary AOE was lost.");
            encounter.Synchronize([boss with { Cast = null }], _ => true, Start.AddSeconds(20));
            Check(!Shapes(encounter).Any(), "Ordinary AOE did not finish by observation.");
        }
        var cross = HuntForecastState.Cross.Resolve(default, 0, null);
        Check(cross.Contains(new(2.49f, 0, 39.9f), 0) && !cross.Contains(new(2.51f, 0, 39.9f), 0)
            && cross.Contains(new(-39.9f, 0, 2.49f), 0), "Forecast cross full width or arm length differs.");
        var damage = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100 };
        damage.Synchronize([Boss(0x4580, Pending(0x4580, 39760).Input), player], _ => true, Start);
        Check(!Shapes(damage).Any() && damage.PredictedDamage([new(0, "Player", 100)]).Count() == 1
            && damage.Hints(player).Contains("Raidwide (3x)"), "Pkuucha invented three damage clocks or a raidwide circle.");
        damage.Effect(1, 39761, Start.AddSeconds(10));
        Check(damage.PredictedDamage([new(0, "Player", 100)]).Count() == 1, "An unused rest AID advanced an invented sequence.");
    }

    private static void Restarts()
    {
        var samba = new SimpleHuntEncounter();
        var cast = Pending(0x4580, 39797).Input;
        var boss = Boss(0x4580, cast);
        samba.Synchronize([boss], _ => true, Start);
        var packet = cast with { Location = new(30, 0, 40), Rotation = 1 };
        samba.Start(1, packet, true);
        samba.Synchronize([boss with { Cast = packet }], _ => true, Start.AddSeconds(1));
        Check(Shapes(samba).Length == 4, "Actual Samba CastInfo restart did not append another sequence.");
        var locked = Shapes(samba).Take(2).ToArray();
        var later = packet with { Location = new(50, 0, 60), Rotation = 2, FinishAt = packet.FinishAt.AddMilliseconds(1) };
        samba.Start(1, later, true);
        Check(Shapes(samba).Take(2).SequenceEqual(locked) && Shapes(samba).Skip(2).All(a => a.Location == later.Location && a.Rotation == 2),
            "Samba packet correction moved historical predictions or detached the active generation.");
        samba.Finish(1, 999999, Start.AddSeconds(2));
        samba.Effect(1, 39755, Start.AddSeconds(2), 1);
        Check(Shapes(samba).Length == 4, "Unknown finish or effect advanced Samba.");
        samba.Reset();
        samba.Synchronize([Boss(0x4580)], _ => true, Start);
        samba.Start(1, cast, true);
        var shortCast = Pending(0x4580, 39755, 10).Input with { TotalTime = .8f };
        samba.Synchronize([Boss(0x4580, shortCast)], _ => true, Start.AddSeconds(10));
        samba.Synchronize([Boss(0x4580, shortCast with { TotalTime = .8005f })], _ => true, Start.AddSeconds(10.1));
        Check(Shapes(samba).Length == 1, "A true 500us short-cast change did not consume the old finish.");
        samba.Synchronize([Boss(0x4580)], _ => true, Start.AddSeconds(11));
        Check(!Shapes(samba).Any(), "Observed short-cast finish failed to consume the last stage.");

        var forecast = new SimpleHuntEncounter();
        forecast.Synchronize([Boss(0x4397)], _ => true, Start);
        forecast.Start(1, Pending(0x4397, 38520).Input, true);
        var release = Pending(0x4397, 38526, 10).Input with { TotalTime = 5.3f };
        forecast.Synchronize([Boss(0x4397, release)], _ => true, Start.AddSeconds(10));
        var initial = Shapes(forecast);
        forecast.Synchronize([Boss(0x4397, release with { Location = new(80, 0, 90), TotalTime = 5.3005f })], _ => true, Start.AddSeconds(11));
        forecast.Start(1, release with { Location = new(90, 0, 100) }, true);
        Check(Shapes(forecast).SequenceEqual(initial), "A new empty-history Channel generation moved released historical weather.");
        forecast.Finish(1, 999999, Start.AddSeconds(12));
        foreach (var action in new uint[] { 38534, 38537, 39133 }) forecast.Effect(1, action, Start.AddSeconds(12), action);
        Check(Shapes(forecast).SequenceEqual(initial), "Unused effect or unknown finish consumed forecast.");
        for (var i = 0; i < 3; ++i)
        {
            forecast.Effect(1, 38527, Start.AddSeconds(20 + i), (uint)(100 + i));
            Check(Shapes(forecast).Length == Math.Min(2, 2 - i), "An empty-history restart appended hidden forecast items.");
        }
    }

    private static char Kind(GeometryTemplate shape) => shape switch
    {
        CircleTemplate => 'C', DonutTemplate => 'D', RayRectangleTemplate => 'R', _ => 'X'
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
