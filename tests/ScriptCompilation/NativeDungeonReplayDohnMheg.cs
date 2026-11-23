using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDohnMheg(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.dohn-mheg";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        bool Chasm() => (bool)Encounter().GetType().GetProperty("DohnChasm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Encounter())!;
        ulong[] Forbidden() => (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(0, 0, 30);
        List<string> geysers =
        [
            Header(821), Frame(821, 0, [Actor(1, 0x3F2, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitZ * 10), Actor(10, 0x1EAAA1, center), Actor(11, 0x1EAAA2, center)], full: true, party: party),
            ResearchIconEvent(.1, 101, 62),
            Frame(821, .2, [Actor(10, 0x1EAAA1, center, eventAnimation: 0x00100020)], party: party),
            Frame(821, 2.2, [Actor(11, 0x1EAAA2, center, eventAnimation: 0x00100020)], party: party),
            EffectEvent(5.2, 1, 8840, 1),
        ];
        for (var i = 0; i < 5; ++i) geysers.Add(EffectEvent(5.3 + .5 * i, 20, 8800, (uint)(10 + i)));
        geysers.Add(Frame(821, 8, [], party: party));
        load(id, geysers);
        controller.Seek(2.3); Check(previews().Length == 6 && dangers() == 3, "Production Dohn geyser waves/stack missing.");
        controller.Seek(5.9); Check(previews().Length == 3 && dangers() == 3, "Production Dohn next wave did not promote.");
        controller.Seek(8.1); Check(previews().Length == 0 && dangers() == 0, "Production Dohn stale animation revived geysers.");
        controller.Seek(2.3); Check(previews().Length == 6, "Production Dohn first boss seek failed."); stop();

        center = new(7.156f, 0, -339.132f);
        List<string> feeding =
        [
            Header(821), Frame(821, 0, [Actor(1, 0x98E, center, combat: true, hitboxRadius: 12.72f),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 10),
                Actor(20, 0xEFB, center + Vector3.UnitX * 20), Actor(19, 0xEFB, center - Vector3.UnitX * 20),
                Actor(18, 0xEFB, center + Vector3.UnitZ * 20), Actor(17, 0xEFB, center - Vector3.UnitZ * 20),
                Actor(10, 0xF08, center + Vector3.UnitX * 3)], full: true, party: party),
            ResearchTetherEvent(.1, 20, 1, 84, true), ResearchTetherEvent(.1, 19, 1, 84, true),
            Frame(821, .2, [Actor(1, 0x98E, center, combat: true, hitboxRadius: 12.72f,
                cast: Cast(8906, 4, center, .7f, startedAt: .2)),
                Actor(20, 0xEFB, center + Vector3.UnitX * 20, tethers: [new { Id = 84, TargetId = 1UL }])], party: party),
            ResearchTetherEvent(3, 20, 1, 84, false), ResearchTetherEvent(3, 19, 1, 84, false),
            Frame(821, 4.3, [Actor(1, 0x98E, center, combat: true, hitboxRadius: 12.72f)], removed: [10], party: party),
        ];
        load(id, feeding);
        controller.Seek(.3); Check(Aoes().Any(a => AoeId(a) == "dohn/feeding/enter") && Aoes().Any(a => AoeId(a) == "dohn/swinge"),
            "Production Dohn slot tether/scaled cone missing.");
        var swinge = Aoes().Single(a => AoeId(a) == "dohn/swinge");
        Check(Math.Abs((float)swinge.GetType().GetProperty("Rotation")!.GetValue(swinge)! - .7f) < .00001f,
            "Production Dohn scaled cone lost packet heading.");
        controller.Seek(3.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("dohn/tether") || AoeId(a).StartsWith("dohn/feeding")),
            "Production Dohn cancelled tether restored from old frame.");
        controller.Seek(4.4); Check(previews().Length == 0 && dangers() == 0, "Production Dohn Griaule cleanup failed.");
        controller.Seek(.3); Check(Aoes().Any(a => AoeId(a) == "dohn/feeding/enter"), "Production Dohn Griaule seek failed."); stop();

        center = new(-128.5f, 0, -244);
        List<string> finale =
        [
            Header(821), Frame(821, 0, [Actor(1, 0xF14, center, combat: true), Actor(100, 0, new(-128.5f, 0, -229)),
                Actor(101, 0, center), Actor(10, 0xF63, new(-128.5f, 0, -260), hitboxRadius: 2.5f)], full: true, party: party),
            Frame(821, .1, [Actor(1, 0xF14, center, combat: true, cast: Cast(13498, 4, center))], party: party),
            Frame(821, 4.2, [Actor(1, 0xF14, center, combat: true),
                Actor(10, 0xF63, new(-128.5f, 0, -260), hitboxRadius: 2.5f,
                    cast: Cast(15723, 60, new(-128.5f, 0, -260), startedAt: 4.2))], party: party),
            Frame(821, 5, [Actor(20, 0x233C, new(-128.5f, 0, -229),
                cast: Cast(13519, 4, new(-128.5f, 0, -229), startedAt: 5))], party: party),
            Frame(821, 9.1, [Actor(20, 0x233C, new(-128.5f, 0, -229))], party: party),
            Frame(821, 10, [Actor(100, 0, new(-128.5f, 0, -257))], party: party),
            Frame(821, 11, [Actor(10, 0xF63, new(-128.5f, 0, -260), dead: true, hitboxRadius: 2.5f)], party: party),
            Frame(821, 12, [Actor(1, 0xF14, center, combat: true, cast: Cast(13552, 4, center, startedAt: 12))], party: party),
            Frame(821, 16.1, [Actor(1, 0xF14, center, combat: true)], party: party),
            Frame(821, 17, [Actor(1, 0xF14, center, combat: true, cast: Cast(13547, 4, center, .6f, startedAt: 17))], party: party),
            Frame(821, 21.1, [Actor(1, 0xF14, center, combat: true)], party: party),
        ];
        for (var i = 0; i < 6; ++i) finale.Add(EffectEvent(21.3 + .5 * i, i == 0 ? 1UL : 20UL,
            i == 0 ? 13547u : 13548u, (uint)i));
        finale.Add(Frame(821, 25, [Actor(1, 0xF14, center, combat: true, rotation: .4f,
            cast: Cast(13952, 5, center, -.8f, startedAt: 25))], party: party));
        finale.Add(Frame(821, 30.1, [Actor(1, 0xF14, center, combat: true, rotation: .4f)], party: party));
        for (var i = 0; i < 5; ++i) finale.Add(EffectEvent(30.3 + .25 * i, i == 0 ? 1UL : 20UL,
            i == 0 ? 13952u : 13953u, (uint)(10 + i)));
        finale.Add(Frame(821, 32, [], party: party));
        load(id, finale);
        controller.Seek(4.3); Check(Chasm() && Aoes().Count(a => AoeId(a).StartsWith("dohn/bridge")) >= 5
            && Forbidden().SequenceEqual([10UL]), "Production Dohn bridge/lyre distance ban missing.");
        controller.Seek(5.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("dohn/bridge"))
            && Aoes().Any(a => AoeId(a).StartsWith("dohn/pull")), "Production Dohn pull did not suspend bridge route.");
        controller.Seek(9.2); Check(Aoes().Any(a => AoeId(a).StartsWith("dohn/bridge")), "Production Dohn pull finish did not restore route.");
        controller.Seek(10.1); Check(Forbidden().Length == 0, "Production Dohn nearby lyre remained banned.");
        controller.Seek(11.1); Check(!Chasm() && previews().Length == 0, "Production Dohn destroyed lyre retained chasm.");
        controller.Seek(15.8); Check(previews().Length == 1 && dangers() == 0, "Production Dohn gaze preview missing or treated as navigation circle.");
        controller.Seek(21.2); Check(previews().Length == 1, "Production Dohn natural bile finish erased six-hit sequence.");
        controller.Seek(23.4); Check(previews().Length == 1, "Production Dohn bile cleared before six.");
        controller.Seek(24); Check(previews().Length == 0, "Production Dohn sixth bile retained.");
        controller.Seek(30.2); Check(previews().Length == 1, "Production Dohn tentacle natural finish erased sequence.");
        controller.Seek(31.4); Check(previews().Length == 0 && dangers() == 0, "Production Dohn fifth tentacle retained.");
        controller.Seek(4.3); Check(Chasm() && Aoes().Any(a => AoeId(a).StartsWith("dohn/bridge")), "Production Dohn final boss seek failed."); stop();
        Console.WriteLine("PASS native DohnMheg three bosses, geyser wave promotion, assigned tethers, scaled cone, exact bridge/pull, lyre bans, gaze isolation, six bile and five tentacle effects, seek and stop");
    }
}
