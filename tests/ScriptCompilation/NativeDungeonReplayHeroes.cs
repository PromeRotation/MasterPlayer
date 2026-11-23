using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeroes(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.heroes";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-680, 0, 450);
        load(id,
        [
            Header(916), Frame(916, 0, [Actor(1, 0x2DEC, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 12),
                Actor(20, 0x1EAED9, c + Vector3.UnitZ * 8)], full: true, party: party),
            StatusEvent(.1, 1, 2193, 0xB1, true),
            Frame(916, .3, [Actor(10, 0x2E71, c, cast: Cast(20439, 3, c, startedAt: .3))], party: party),
            Frame(916, 3.4, [Actor(10, 0x2E71, c)], party: party),
            EffectEvent(8.3, 30, 20434, 1),
            StatusEvent(9, 1, 2193, 0xB1, true),
            Frame(916, 10, [Actor(1, 0x2DEC, c, combat: true, cast: Cast(20427, 4, c, target: 100, startedAt: 10))], party: party),
            Frame(916, 14.1, [Actor(1, 0x2DEC, c, combat: true)], party: party),
            EffectEvent(15, 1, 20427, 2), EffectEvent(17.2, 30, 20433, 3),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1 && Has("heroes/dash/"), "Production Heroes status dash not routed.");
        controller.Seek(.4); Check(previews().Length == 2 && dangers() == 1, "Production Heroes knives should suppress navigation only.");
        controller.Seek(3.5); Check(previews().Length == 1 && dangers() == 1, "Production Heroes dash navigation not restored.");
        controller.Seek(8.4); Check(previews().Length == 0 && dangers() == 0, "Production Heroes dash effect leaked.");
        controller.Seek(9.1); Check(previews().Length == 1, "Production Heroes repeated status param lost next wave.");
        controller.Seek(14.2); Check(((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count() == 1, "Production Heroes delayed buster lost.");
        controller.Seek(17.3); Check(previews().Length == 0 && dangers() == 0, "Production Heroes final dash leaked.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Heroes thief seek failed."); stop();

        c = new(-450, 0, -531);
        load(id,
        [
            Header(916), Frame(916, 0, [Actor(1, 0x2DF1, c, combat: true), Actor(100, 0, c, classJob: 24), Actor(101, 0, c + Vector3.UnitX * 12),
                Actor(20, 0x2DF6, c + Vector3.UnitX * 6, tethers: [new { Id = 79, TargetId = 100UL }])], full: true, party: party),
            Frame(916, .1, [Actor(21, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 54)], party: party),
            Frame(916, 1, [Actor(10, 0x233C, c, cast: Cast(20388, 5.5, c, startedAt: 1))], party: party),
            Frame(916, 2, [Actor(30, 0x1EB02C, c - Vector3.UnitZ * 8)], party: party),
            ResearchIconEvent(2.4, 100, 23),
            Frame(916, 3, [Actor(100, 0, c, classJob: 24, stackedStatus: 910),
                Actor(21, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 54, cast: Cast(20322, 4, c, startedAt: 3))], party: party),
            Frame(916, 7.1, [Actor(21, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 54), Actor(10, 0x233C, c)], party: party),
            EffectEvent(7.2, 20, 20323, 1),
            Frame(916, 8, [Actor(30, 0x1EB02C, c, eventState: 7), Actor(100, 0, c, classJob: 24)], removed: [20UL], party: party),
            Frame(916, 9, [Actor(21, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 0)], party: party),
            Frame(916, 9.1, [Actor(21, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 54, cast: Cast(20322, 4, c, startedAt: 9.1))], party: party),
            Frame(916, 9.5, [], removed: [21UL], party: party),
            Frame(916, 10, [Actor(40, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 54, cast: Cast(20322, 4, c, startedAt: 10))], party: party),
            Frame(916, 11, [Actor(40, 0x2DF2, c + Vector3.UnitZ * 8, modelState: 54)], party: party),
        ]);
        controller.Seek(.05); Check(previews().Length == 0 && dangers() == 1 && Has("heroes/crawling/"), "Production Heroes crawling hint missing.");
        controller.Seek(.2); Check(previews().Length == 1 && Has("heroes/burst/"), "Production Heroes model burst missing.");
        controller.Seek(2.1); Check(Has("heroes/mire/") && Has("heroes/pool/") && Aoes().Single(a => AoeId(a).StartsWith("heroes/mire/")).GetType().GetProperty("Navigate")!.GetValue(Aoes().Single(a => AoeId(a).StartsWith("heroes/mire/"))) is false,
            "Production Heroes persistent pool did not suppress mire navigation.");
        controller.Seek(3.1); Check(Has("heroes/bait/") && !Has("heroes/crawling/"), "Production Heroes icon bait failed takeover.");
        Check(Encounter().GetType().GetMethod("HeroesCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Encounter(), [24u]) != null, "Production Heroes doom cleanse target missing.");
        Check((DateTime)Script().GetType().BaseType!.GetField("nextSlimeCleanse", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())! == default,
            "Production Heroes replay attempted native Esuna.");
        controller.Seek(7.15); Check(!Has("heroes/burst/"), "Production Heroes natural Burst finish did not clear.");
        controller.Seek(7.3); Check(!Has("heroes/bait/") && Has("heroes/crawling/"), "Production Heroes bait effect did not restore crawling hint.");
        controller.Seek(8.1); Check(previews().Length == 0 && dangers() == 0, "Production Heroes necromancer cleanup leaked.");
        controller.Seek(9.2); Check(Has("heroes/burst/"), "Production Heroes second model wave missing.");
        controller.Seek(9.6); Check(!Has("heroes/burst/"), "Production Heroes burst source removal leaked.");
        controller.Seek(10.1); Check(Has("heroes/burst/"), "Production Heroes third model wave missing.");
        controller.Seek(11.1); Check(!Has("heroes/burst/"), "Production Heroes cancelled burst leaked.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Heroes necromancer seek failed."); stop();

        c = new(750, 0, 482);
        load(id,
        [
            Header(916), Frame(916, 0, [Actor(1, 0x2EFD, c, combat: true), Actor(100, 0, c), Actor(101, 0, c - Vector3.UnitX * 16)], full: true, party: party),
            Frame(916, .1, [Actor(1, 0x2EFD, c, combat: true, cast: Cast(21004, 4, c))], party: party),
            JsonSerializer.Serialize(new { t = 5.2, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = 11, Param1 = 1, Param2 = 2, Timestamp = Start.AddSeconds(5.2) } }),
            Frame(916, 5.3, [Actor(1, 0x2EFD, c, combat: true)], party: party),
            ResearchIconEvent(6, 100, 229),
            Frame(916, 6.1, [Actor(20, 0x2EFE, c + Vector3.UnitX * 10, hitboxRadius: 2.5f),
                Actor(21, 0x2EFE, c - Vector3.UnitX * 10, hitboxRadius: 2.5f)], party: party),
            Frame(916, 11.9, [Actor(100, 0, c + Vector3.UnitX * 10)], party: party),
            EffectEvent(12, 1, 21000, 1, target: 100),
            Frame(916, 13, [Actor(30, 0x1EA1A1, new(738, 0, 482), eventAnimation: 0x00010002),
                Actor(31, 0x1EA1A1, new(762, 0, 482), eventAnimation: 0x00010002), Actor(100, 0, c)], party: party),
            Frame(916, 14, [Actor(10, 0x233C, c, cast: Cast(20999, 5.5, c, startedAt: 14))], party: party),
            Frame(916, 19.6, [Actor(10, 0x233C, c)], party: party),
            Frame(916, 20, [Actor(11, 0x233C, c, cast: Cast(20996, 5.7, c, startedAt: 20))], party: party),
            Frame(916, 25.8, [Actor(11, 0x233C, c)], party: party),
        ]);
        controller.Seek(.2); Check(Has("heroes/collapse") && previews().Length == 1, "Production Heroes collapse missing.");
        controller.Seek(5.4); Check(previews().Length == 0 && dangers() == 0, "Production Heroes map collapse handoff leaked.");
        controller.Seek(6.2); Check(Has("heroes/rubble/enter") && Has("heroes/spread/100") && previews().Length == 3, "Production Heroes solo rubble phase missing.");
        controller.Seek(12.1); Check(previews().Length == 0 && dangers() == 0, "Production Heroes rubble effect leaked.");
        controller.Seek(13.1); Check(previews().Length == 2 && dangers() == 2, "Production Heroes crater animation not restored.");
        controller.Seek(14.1); Check(Has("heroes/crater/enter") && dangers() == 1, "Production Heroes rampage did not invert crater union.");
        controller.Seek(19.7); Check(!Has("heroes/crater/enter") && dangers() == 2, "Production Heroes rampage finish left safe craters.");
        controller.Seek(20.1); Check(Has("heroes/knockback/11") && previews().Length == 3, "Production Heroes knockback plan/landing missing.");
        controller.Seek(25.9); Check(!Has("heroes/knockback/") && previews().Length == 2 && dangers() == 2, "Production Heroes knockback cleanup failed.");
        controller.Seek(6.2); Check(Has("heroes/rubble/enter"), "Production Heroes rubble seek failed."); stop();
        object[] fullParty = Enumerable.Range(0, 4).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        Vector3[] rubblePositions = [c + new Vector3(-12, 0, -12), c + new Vector3(12, 0, -12), c + new Vector3(-12, 0, 12), c + new Vector3(12, 0, 12)];
        List<string> four =
        [
            Header(916), Frame(916, 0, [Actor(1, 0x2EFD, c, combat: true),
                .. rubblePositions.Select((p, i) => Actor((ulong)(100 + i), 0, p))], full: true, party: fullParty),
        ];
        for (var i = 0; i < 4; ++i) four.Add(ResearchIconEvent(.1 + i * .01, (ulong)(100 + i), 229));
        four.Add(Frame(916, .2, rubblePositions.Select((p, i) => Actor((ulong)(20 + i), 0x2EFE, p, hitboxRadius: 2.5f)).ToArray(), party: fullParty));
        for (var i = 0; i < 4; ++i) four.Add(EffectEvent(1 + i, 1, i == 0 ? 21000u : 21001u, (uint)i, target: (ulong)(100 + i)));
        load(id, four);
        controller.Seek(.3); Check(previews().Length == 8, "Production Heroes four spread/four rubble missing.");
        for (var i = 0; i < 4; ++i)
        {
            controller.Seek(1.1 + i);
            Check(Aoes().Count(a => AoeId(a).StartsWith("heroes/spread/") && AoeId(a).Count(c => c == '/') == 2) == 3 - i, "Production Heroes hit cleared remaining spread markers.");
            Check(Aoes().Count(a => AoeId(a).StartsWith("heroes/rubble/") && AoeId(a) != "heroes/rubble/enter") == 3 - i, "Production Heroes hit cleared other rubble.");
        }
        controller.Seek(.3); Check(previews().Length == 8, "Production Heroes four rubble seek failed."); stop();
        Console.WriteLine("PASS native Heroes three bosses, status dash and knife navigation suppression, delayed buster, model burst, mire and crawling/icon handoff, doom replay execution isolation, collapse, rubble solo towers, crater inversion, radial knockback, seek and stop");
    }
}
