using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBabil(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.babil";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        Vector3 ArenaCenter() => (Vector3)Encounter().GetType().GetProperty("Arena")!.GetValue(Encounter())!.GetType().GetProperty("Position")!.GetValue(
            Encounter().GetType().GetProperty("Arena")!.GetValue(Encounter())!)!;
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-300, 0, 71);
        load(id,
        [
            Header(969), Frame(969, 0, [Actor(1, 0x33F7, c, combat: true), Actor(100, 0, c + Vector3.UnitX * 8),
                Actor(101, 0, c - Vector3.UnitX * 8)], full: true, party: party),
            Frame(969, .1, [Actor(1, 0x33F7, c, combat: true, cast: Cast(25326, 7, c))], party: party),
            Frame(969, .2, [Actor(10, 0x233C, c, cast: Cast(25327, 9.5, c, startedAt: .2))], party: party),
            ResearchIconEvent(.3, 100, 162),
            Frame(969, 7.2, [Actor(1, 0x33F7, c, combat: true)], party: party), EffectEvent(8.4, 10, 24694, 1),
            Frame(969, 9.8, [Actor(10, 0x233C, c)], party: party),
            Frame(969, 10, [Actor(1, 0x33F7, c, combat: true, cast: Cast(25159, 3.5, c, startedAt: 10))], party: party),
            Frame(969, 13.6, [Actor(1, 0x33F7, c, combat: true)], party: party), Map(19.7, 0, 0x00020001),
            Frame(969, 20, [Actor(1, 0x33F7, c, combat: true, cast: Cast(25328, 7, c, startedAt: 20)),
                Actor(10, 0x233C, c, cast: Cast(25329, 9.5, c, startedAt: 20))], party: party),
            ResearchIconEvent(20.1, 100, 163), EffectEvent(28.2, 10, 25053, 2),
            Frame(969, 29.6, [Actor(1, 0x33F7, c, combat: true), Actor(10, 0x233C, c)], party: party),
        ]);
        controller.Seek(.4); Check(Has("babil/magnet/avoid") && dangers() == 3, "Production magnetism input composition missing.");
        controller.Seek(8.5); Check(!Has("babil/magnet/"), "Production directional magnetic resolve leaked.");
        controller.Seek(13.7); Check(Has("babil/ring") && previews().Length == 1, "Production ground-pound finish did not leave ring.");
        controller.Seek(19.8); Check(!Has("babil/ring") && previews().Length == 0 && dangers() == 0, "Production map shrink did not clear ring.");
        controller.Seek(20.2); Check(Has("babil/magnet/avoid"), "Production circular magnetism missing.");
        controller.Seek(28.3); Check(!Has("babil/magnet/"), "Production 25053 did not clear circular magnetism.");
        controller.Seek(29.7); Check(previews().Length == 0 && dangers() == 0, "Production Barnabas final output leaked.");
        controller.Seek(.4); Check(Has("babil/magnet/"), "Barnabas Seek failed."); stop();

        c = new(221, 0, 306);
        load(id,
        [
            Header(969), Frame(969, 0, [Actor(1, 0x33FA, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(969, .1, [Actor(1, 0x33FA, c, combat: true, cast: Cast(25331, 5, c))], party: party),
            Frame(969, 5.2, [Actor(1, 0x33FA, c, combat: true)], party: party),
            StatusEvent(6, 100, 2504, 0, true), StatusEvent(7, 100, 2504, 0, false),
            Frame(969, 7.1, [Actor(1, 0x33FA, c, combat: true, cast: Cast(25338, 5, c, startedAt: 7.1))], party: party),
            Map(12, 2, 0x00080004),
            Frame(969, 13, [Actor(1, 0x33FA, c, combat: true, cast: Cast(25333, 5, c, startedAt: 13))], party: party),
            Frame(969, 18.1, [Actor(1, 0x33FA, c, combat: true)], party: party),
            StatusEvent(19, 100, 2672, 0, true), StatusEvent(20, 100, 2672, 0, false), Map(21, 1, 0x00080004),
        ]);
        controller.Seek(5.3); Check(Has("babil/floor/enter") && previews().Length == 2, "Production purple floor not activated by cast finish.");
        controller.Seek(6.1); Check(!Has("babil/floor/enter"), "Production Minimum did not turn purple floor dangerous.");
        controller.Seek(7.05); Check(Has("babil/floor/enter"), "Production Minimum loss did not restore entry.");
        controller.Seek(7.2); Check(!Has("babil/floor/enter") && previews().Length == 2, "Production Thermal failed to avoid floors.");
        controller.Seek(12.1); Check(previews().Length == 0 && dangers() == 0, "Production floor map clear leaked.");
        controller.Seek(18.2); Check(!Has("babil/floor/enter") && previews().Length == 2, "Production Downpour requires Breathless.");
        controller.Seek(19.1); Check(Has("babil/floor/enter"), "Production Breathless did not force green entry.");
        controller.Seek(20.1); Check(!Has("babil/floor/enter"), "Production Breathless loss did not release green entry.");
        controller.Seek(21.1); Check(previews().Length == 0 && dangers() == 0, "Production Downpour map clear leaked.");
        controller.Seek(5.3); Check(Has("babil/floor/enter"), "Lugae Seek failed."); stop();

        c = new(0, 0, -180);
        var lower = new Vector3(0, 0, -400);
        List<string> anima =
        [
            Header(969), Frame(969, 0, [Actor(1, 0x33FD, c, combat: true), Actor(100, 0, c, classJob: 24),
                Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Map(.1, 3, 0x00020001),
            Frame(969, .2, [Actor(1, 0x33FD, c, targetable: false), Actor(10, 0x3400, lower, combat: true),
                Actor(100, 0, lower, classJob: 24), Actor(101, 0, lower + Vector3.UnitX * 10)], party: party),
            Frame(969, .3, [Actor(20, 0x33FF, lower + Vector3.UnitX * 18, tethers: [new { Id = 57, TargetId = 100UL }])], party: party),
            Frame(969, 1, [Actor(20, 0x33FF, lower + Vector3.UnitX * 18, tethers: [new { Id = 17, TargetId = 100UL }])], party: party),
            EffectEvent(2, 30, 26229, 1),
            Frame(969, 2.4, [Actor(20, 0x33FF, lower + Vector3.UnitX * 18)], party: party),
        ];
        for (var i = 0; i < 20; ++i) anima.Add(EffectEvent(3 + i * .1, 30, i == 0 ? 25348u : 25349u, (uint)(2 + i)));
        anima.Add(ResearchIconEvent(6, 100, 197));
        anima.Add(Frame(969, 6.5, [Actor(30, 0x233C, lower, cast: Cast(25358, 3.5, lower, startedAt: 6.5)),
            Actor(100, 0, lower + Vector3.UnitX * 12, classJob: 24)], party: party));
        anima.Add(Frame(969, 10.1, [Actor(30, 0x233C, lower)], party: party));
        for (var i = 0; i < 5; ++i) anima.Add(EffectEvent(10.2 + i * 1.2, 30, i == 0 ? 25358u : 21239u, (uint)(30 + i),
            position: lower + Vector3.UnitX * Math.Min(12, i * 6)));
        anima.Add(ResearchIconEvent(16, 100, 197));
        anima.Add(Frame(969, 17, [Actor(10, 0x3400, lower, combat: true, cast: Cast(25359, 6, lower, startedAt: 17))], party: party));
        anima.Add(Frame(969, 18, Enumerable.Range(40, 7).Select(i => Actor((ulong)i, 0x3401, lower, cast: Cast(25357, 6, lower, rotation: .7f, startedAt: 18))), party: party));
        anima.Add(Frame(969, 18.5, [], removed: [40UL], party: party));
        anima.Add(Frame(969, 19, [Actor(47, 0x3401, lower, cast: Cast(25357, 5, lower, rotation: .7f, startedAt: 19))], party: party));
        anima.Add(Map(20, 3, 0x00080004));
        anima.Add(Frame(969, 24.2, Enumerable.Range(41, 7).Select(i => Actor((ulong)i, 0x3401, lower)), party: party));
        load(id, anima);
        controller.Seek(.4); Check(ArenaCenter().Z == -400 && Has("babil/tether/"), "Production Anima intermission reset lower arena or tether.");
        var clock = (DateTime)Aoes().Single(a => AoeId(a).EndsWith("/avoid")).GetType().GetProperty("Activation")!.GetValue(Aoes().Single(a => AoeId(a).EndsWith("/avoid")))!;
        controller.Seek(1.1);
        Check(Aoes().Any(a => AoeId(a).EndsWith("/line") && (bool)a.GetType().GetProperty("Safe")!.GetValue(a)!)
            && Aoes().Single(a => AoeId(a).EndsWith("/avoid")).GetType().GetProperty("Activation")!.GetValue(Aoes().Single(a => AoeId(a).EndsWith("/avoid")))!.Equals(clock),
            "Production good tether restarted clock or lost safe line.");
        controller.Seek(2.2);
        Check((bool)Encounter().GetType().GetProperty("WantsBabilKnockbackImmunity")!.GetValue(Encounter())!
            && (DateTime)Script().GetType().BaseType!.GetField("nextSirenImmunity", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())! == default,
            "Production Anima immunity request missing or replay attempted execution.");
        Check(Has("babil/pain"), "Production boundless pain pull event missing.");
        controller.Seek(4.8); Check(Has("babil/pain"), "Production pain cleared before twentieth hit.");
        controller.Seek(5); Check(!Has("babil/pain"), "Production twentieth pain hit leaked.");
        controller.Seek(6.1); Check(Has("babil/chase/edge"), "Production Coffin icon edge navigation missing.");
        controller.Seek(6.6); Check(!Has("babil/chase/edge") && Has("babil/chase/30/display"), "Production first chase failed to consume icon.");
        controller.Seek(14); Check(Has("babil/chase/30/display"), "Production chase cleared before fifth hit.");
        controller.Seek(15.1); Check(!Has("babil/chase/"), "Production fifth chase hit leaked.");
        controller.Seek(16.1); Check(Has("babil/chase/edge"), "Production next Coffin icon missing.");
        controller.Seek(17.1); Check(!Has("babil/chase/"), "Production Oblivion phase transition retained icons.");
        controller.Seek(18.1); Check(Aoes().Count(a => AoeId(a).StartsWith("babil/claw/")) == 5, "Production Charnel exceeded first five.");
        controller.Seek(19.1); Check(Has("babil/claw/45") && !Has("babil/claw/47"), "Production new claw jumped ahead after dictionary-slot reuse.");
        controller.Seek(20.1); Check(ArenaCenter().Z == -180, "Production Anima return map ignored.");
        controller.Seek(24.3); Check(!Has("babil/claw/"), "Production Charnel cast finish leaked.");
        controller.Seek(.4); Check(ArenaCenter().Z == -400 && Has("babil/tether/"), "Anima Seek failed."); stop();
        Check(previews().Length == 0 && dangers() == 0, "Babil Stop leaked output.");
        Console.WriteLine("PASS production Babil: all three bosses, magnetic inputs/resolves and shrink, two transformation floors, statuses, Anima layer persistence, tether colors/timing/replay action isolation, twenty hits, five chases, claw limit, seek and stop");
    }
}
