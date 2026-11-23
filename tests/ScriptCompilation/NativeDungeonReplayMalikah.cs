using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMalikah(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.malikah";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(278, 0, 204);
        load(id,
        [
            Header(836), Frame(836, 0, [Actor(1, 0x2679, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitZ * 9)], full: true, party: party),
            Frame(836, .1, [Actor(1, 0x2679, center, combat: true, cast: Cast(15591, 2.5, center + Vector3.UnitZ * 7, .7f))], party: party),
            Frame(836, 2.6, [Actor(1, 0x2679, center, combat: true)], party: party),
            EffectEvent(3.5, 20, 15592, 1),
            Frame(836, 4, [Actor(1, 0x2679, center, combat: true, cast: Cast(15590, 5, center, target: 101, startedAt: 4))], party: party),
            Frame(836, 9.1, [Actor(1, 0x2679, center, combat: true)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Malikah jump capsule missing.");
        controller.Seek(2.7); Check(previews().Length == 1, "Production Malikah natural jump cast finish erased forecast.");
        controller.Seek(3.6); Check(previews().Length == 0 && dangers() == 0, "Production Malikah actual jump leaked.");
        controller.Seek(4.1); Check(Aoes().Any(a => AoeId(a).StartsWith("malikah/stack/enter")), "Production Malikah forced stack missing.");
        controller.Seek(9.2); Check(previews().Length == 0 && dangers() == 0, "Production Malikah stack finish leaked.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Malikah first boss seek failed."); stop();

        center = new(208, 0, 275);
        List<string> talos =
        [
            Header(836), Frame(836, 0, [Actor(1, 0x267A, center, combat: true), Actor(100, 0, center + Vector3.UnitZ * 5),
                Actor(101, 0, center - Vector3.UnitZ * 8)], full: true, party: party),
            Frame(836, .1, [Actor(1, 0x267A, center, combat: true, cast: Cast(15599, 7, center))], party: party),
            ResearchIconEvent(.2, 1, 156),
            Frame(836, 7.2, [Actor(1, 0x267A, center, combat: true)], party: party),
        ];
        for (var i = 0; i < 6; ++i) talos.Add(EffectEvent(7.4 + i * 1.1, 1, i == 0 ? 15599u : 15600u, (uint)i));
        talos.Add(Frame(836, 14, [Actor(1, 0x267A, center, combat: true, cast: Cast(15596, 4, center, startedAt: 14)),
            Actor(10, 0x1EAAC9, center + Vector3.UnitZ * 17)], party: party));
        talos.Add(Frame(836, 15, [Actor(100, 0, center + Vector3.UnitZ * 5, stackedStatus: 160)], party: party));
        talos.Add(Frame(836, 18.1, [Actor(1, 0x267A, center, combat: true), Actor(10, 0x1EAAC9, center + Vector3.UnitZ * 17, eventState: 7)], party: party));
        load(id, talos);
        controller.Seek(.3); Check(previews().Length == 2 && dangers() == 2, "Production Malikah zero-heading rotation missing.");
        controller.Seek(7.3); Check(previews().Length == 2, "Production Malikah natural finish erased sequence.");
        controller.Seek(12); Check(previews().Length == 1, "Production Malikah sixth cone not retained.");
        controller.Seek(13); Check(previews().Length == 0 && dangers() == 0, "Production Malikah rotation did not finish six.");
        controller.Seek(14.1); Check(Aoes().Any(a => AoeId(a) == "malikah/pressure/1") && Aoes().Any(a => AoeId(a) == "malikah/geyser/10"),
            "Production Malikah geyser/knockback output missing.");
        controller.Seek(15.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("malikah/pressure")), "Production Malikah immunity did not suppress displacement.");
        controller.Seek(18.2); Check(previews().Length == 0 && dangers() == 0, "Production Malikah geyser state seven leaked.");
        controller.Seek(.3); Check(previews().Length == 2, "Production Malikah Talos seek failed."); stop();

        center = new(196, 0, -95);
        foreach (var longDonut in new[] { true, false })
        {
            Vector3 Nail(int i) => center + new Vector3(i % 2 == 0 ? -10 : 10, 0, i < 2 ? -10 : 10);
            List<string> streak =
            [
                Header(836), Frame(836, 0, [Actor(1, 0x267B, center, combat: true), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 8),
                    .. Enumerable.Range(0, 4).Select(i => Actor((ulong)(10 + i), 0x267C, Nail(i)))], full: true, party: party),
                Frame(836, .1, [Actor(1, 0x267B, center, combat: true, cast: Cast(longDonut ? 15887u : 15886u, 29, center, .7f))], party: party),
                Frame(836, 1, Enumerable.Range(0, 4).Reverse().Select(i => Actor((ulong)(10 + i), 0x267C, Nail(i),
                    cast: Cast(longDonut ? 15609u : 15610u, 8 + i * 3, Nail(i), .3f, startedAt: 1))), party: party),
            ];
            for (var i = 0; i < 4; ++i) streak.Add(Frame(836, 9.1 + i * 3, [Actor((ulong)(10 + i), 0x267C, Nail(i))], party: party));
            streak.Add(Frame(836, 29.2, [Actor(1, 0x267B, center, combat: true)], party: party));
            load(id, streak);
            double[] checkpoints = [2, 9.2, 12.2, 15.2];
            for (var i = 0; i < 4; ++i)
            {
                controller.Seek(checkpoints[i]);
                var aoe = Aoes().Single(a => AoeId(a) == "malikah/streak");
                Check(previews().Length == 1 && dangers() == 1, "Production Malikah streak published future stages together.");
                Check((Vector3)aoe.GetType().GetProperty("Position")!.GetValue(aoe)! == Nail(i), "Production Malikah streak not sorted.");
            }
            controller.Seek(18.2); Check((Vector3)Aoes().Single().GetType().GetProperty("Position")!.GetValue(Aoes().Single())! == center,
                "Production Malikah long Boss segment lost after nails.");
            controller.Seek(29.3); Check(previews().Length == 0 && dangers() == 0, "Production Malikah final streak leaked.");
            controller.Seek(2); Check(previews().Length == 1, "Production Malikah Storge seek failed."); stop();
        }
        Console.WriteLine("PASS native Malikah three bosses, delayed jump, forced stack, zero-heading six rotates, wall knockback, geyser/immunity, both five-stage nail streaks, seek and stop");
    }
}
