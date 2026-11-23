using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHolminster(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.holminster";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        ulong[] Forbidden() => (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-109, 0, 237);
        load(id,
        [
            Header(837), Frame(837, 0, [Actor(1, 0x278A, center, combat: true), Actor(100, 0, center)], full: true, party: party),
            Frame(837, .1, [Actor(1, 0x278A, center, combat: true, cast: Cast(15814, 4.8, center + Vector3.UnitX * 15))], party: party),
            Frame(837, 2, [Actor(1, 0x278A, center + Vector3.UnitX * 10, combat: true,
                cast: Cast(15814, 4.8, center + Vector3.UnitX * 15))], party: party),
            Frame(837, 5.1, [Actor(1, 0x278A, center + Vector3.UnitX * 15, combat: true)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Holminster charge missing.");
        var origin = (Vector3)Aoes().Single().GetType().GetProperty("Position")!.GetValue(Aoes().Single())!;
        controller.Seek(2.1); Check((Vector3)Aoes().Single().GetType().GetProperty("Position")!.GetValue(Aoes().Single())! == origin,
            "Production Holminster charge origin drifted.");
        controller.Seek(5.2); Check(previews().Length == 0 && dangers() == 0, "Production Holminster charge finish failed.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Holminster charge seek failed."); stop();

        center = new(-110, 0, -97);
        List<string> tesleen =
        [
            Header(837), Frame(837, 0, [Actor(1, 0x278B, center, combat: true),
                Actor(100, 0, center + Vector3.UnitX * 8), Actor(101, 0, center + Vector3.UnitZ * 10)], full: true, party: party),
            ResearchIconEvent(.1, 101, 82), ResearchIconEvent(.2, 100, 79), ResearchIconEvent(.3, 101, 62),
            EffectEvent(5.4, 1, 15827, 1), EffectEvent(8.4, 1, 15830, 2), EffectEvent(8.7, 1, 15830, 3),
            Frame(837, 9, [Actor(10, 0x233C, center + Vector3.UnitX * 4,
                cast: Cast(15828, 7, center + Vector3.UnitX * 4, startedAt: 9))], party: party),
            Frame(837, 16.2, [Actor(10, 0x233C, center + Vector3.UnitX * 4)], party: party),
            EffectEvent(16.3, 10, 15828, 4, center + Vector3.UnitX * 4),
            Frame(837, 17.1, [Actor(11, 0x1EABF9, center + Vector3.UnitX * 4)], party: party),
            Frame(837, 18, [Actor(11, 0x1EABF9, center + Vector3.UnitX * 4, eventState: 7)], party: party),
        ];
        load(id, tesleen);
        controller.Seek(.4); Check(previews().Length == 3 && dangers() >= 3, "Production Holminster charges/stack missing.");
        controller.Seek(8.5); Check(previews().Length == 1, "Production Holminster first charge did not advance FIFO.");
        controller.Seek(8.8); Check(previews().Length == 0 && dangers() == 0, "Production Holminster charges retained.");
        controller.Seek(16.4); Check(Aoes().Single() is { } prediction && AoeId(prediction).Contains("/water/event/"),
            "Production Holminster postcast water missing.");
        controller.Seek(17.2); Check(Aoes().Length == 1 && AoeId(Aoes()[0]) == "holminster/pool/11", "Production Holminster water handoff duplicated.");
        controller.Seek(18.1); Check(previews().Length == 0 && dangers() == 0, "Production Holminster inactive water retained.");
        controller.Seek(.4); Check(previews().Length == 3, "Production Holminster Tesleen seek failed."); stop();

        center = new(134, 0, -465);
        List<string> philia =
        [
            Header(837), Frame(837, 0, [Actor(1, 0x278C, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitZ * 8)], full: true, party: party),
            ResearchIconEvent(.1, 101, 92),
            Frame(837, .2, [Actor(1, 0x278C, center, combat: true, cast: Cast(17052, 5, center, startedAt: .2))], party: party),
            Frame(837, 5.5, [Actor(1, 0x278C, center, combat: true), Actor(10, 0x2895, center + Vector3.UnitZ * 8),
                Actor(101, 0, center + Vector3.UnitZ * 8, stackedStatus: 1849)], party: party),
            StatusEvent(5.5, 101, 1849, 0, true),
            Frame(837, 6, [Actor(1, 0x278C, center, combat: true, cast: Cast(15848, 15, center, startedAt: 6))], party: party),
            Frame(837, 8, [Actor(10, 0x2895, center + Vector3.UnitZ * 8, dead: true),
                Actor(101, 0, center + Vector3.UnitZ * 8)], party: party),
            StatusEvent(8, 101, 1849, 0, false),
            Frame(837, 21.2, [Actor(1, 0x278C, center, combat: true)], party: party),
        ];
        for (var i = 0; i < 4; ++i) philia.Add(EffectEvent(21.3 + .5 * i, i == 0 ? 1UL : 20UL,
            i == 0 ? 15848u : 15849u, (uint)(10 + i)));
        philia.Add(Frame(837, 24, [Actor(1, 0x278C, center, combat: true, cast: Cast(15834, 5, center, .4f, startedAt: 24))], party: party));
        philia.Add(Frame(837, 29.2, [Actor(1, 0x278C, center, combat: true)], party: party));
        for (var i = 0; i < 8; ++i)
        {
            var t = 29.3 + 2.1 * i;
            philia.Add(Frame(837, t, [Actor(20, 0x233C, center, cast: Cast(15841, 2, center, startedAt: t))], party: party));
            philia.Add(Frame(837, t + 2, [Actor(20, 0x233C, center)], party: party));
        }
        philia.Add(Frame(837, 47, [], party: party));
        load(id, philia);
        controller.Seek(6.1); Check(Forbidden().SequenceEqual([1UL]) && previews().Length == 2, "Production Holminster rescue restriction missing.");
        Check(!(bool)Aoes().Single(a => AoeId(a) == "holminster/aethersup").GetType().GetProperty("Navigate")!.GetValue(
            Aoes().Single(a => AoeId(a) == "holminster/aethersup"))!, "Production Holminster cone blocked rescue.");
        controller.Seek(8.1); Check(Forbidden().Length == 0 && dangers() == 1, "Production Holminster rescue status removal did not clear ban.");
        controller.Seek(22.4); Check(previews().Length == 1, "Production Holminster Aethersup ended before fourth effect.");
        controller.Seek(23); Check(previews().Length == 0 && dangers() == 0, "Production Holminster fourth Aethersup retained.");
        controller.Seek(29.25); Check(previews().Length == 2, "Production Holminster tail telegraph finish cleared sequence.");
        controller.Seek(31.35); Check(previews().Length == 2, "Production Holminster tail first finish failed.");
        controller.Seek(47.1); Check(previews().Length == 0 && dangers() == 0, "Production Holminster eighth tail retained.");
        controller.Seek(6.1); Check(Forbidden().SequenceEqual([1UL]) && previews().Length == 2, "Production Holminster Philia seek failed."); stop();

        var exaOrigin = center + Vector3.UnitX * 10;
        List<string> exas =
        [
            Header(837), Frame(837, 0, [Actor(1, 0x278C, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x233C, exaOrigin, rotation: MathF.PI)], full: true, party: party),
            Frame(837, .1, [Actor(10, 0x233C, exaOrigin, rotation: MathF.PI,
                cast: Cast(15837, 5, exaOrigin, MathF.PI))], party: party),
            Frame(837, 5.2, [Actor(10, 0x233C, exaOrigin, rotation: MathF.PI)], party: party),
        ];
        for (var i = 0; i < 7; ++i) exas.Add(EffectEvent(5.4 + i, 10, i == 0 ? 15837u : 15839u,
            (uint)i, exaOrigin - Vector3.UnitZ * (2.5f * i)));
        exas.Add(Frame(837, 12, [], party: party));
        load(id, exas);
        controller.Seek(.2); Check(previews().Length == 4 && dangers() == 4, "Production Holminster exaflare first/prediction missing.");
        controller.Seek(5.3); Check(previews().Length == 4, "Production Holminster natural finish erased exaflares.");
        controller.Seek(10.5); Check(previews().Length == 2, "Production Holminster exaflare seven-hit count differs.");
        controller.Seek(12.1); Check(Aoes().Length == 1 && AoeId(Aoes()[0]).Contains("/prediction/"), "Production Holminster exaflare line retained.");
        controller.Seek(.2); Check(previews().Length == 4, "Production Holminster exaflare seek failed."); stop();
        Console.WriteLine("PASS native Holminster three bosses, fixed charge, icon FIFO, water handoff, fetter bans, four Aethersups, eight tails, seven exaflares, seek and stop");
    }
}
