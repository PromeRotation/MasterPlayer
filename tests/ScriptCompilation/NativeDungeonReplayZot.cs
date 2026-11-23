using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZot(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.zot";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        bool Has(string prefix) => Aoes().Any(a => ((string)a.GetType().GetProperty("Id")!.GetValue(a)!).StartsWith(prefix));
        bool Dhrupad() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [null])!).Cast<string>().Contains("三次单体伤害及持续伤害");
        object? Cleanse() => Encounter().GetType().GetMethod("ZotCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Encounter(), [24u]);
        int DamageCount() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(68, 0, -124);
        load(id,
        [
            Header(952), Frame(952, 0, [Actor(1, 0x33EE, c, combat: true), Actor(100, 0, c, classJob: 24),
                Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(952, .1, [Actor(1, 0x33EE, c, combat: true, cast: Cast(25244, 4, c))], party: party),
            StatusEvent(.4, 101, 18, 0, true), StatusEvent(.7, 101, 18, 0, false),
            Frame(952, 1, [Actor(10, 0x233C, c, cast: Cast(25237, 4, c, startedAt: 1)),
                Actor(11, 0x233C, c, cast: Cast(25238, 4, c, rotation: .7f, startedAt: 1))], party: party),
            Frame(952, 2, [Actor(1, 0x33EE, c, combat: true, cast: Cast(25248, 2, c, target: 100, startedAt: 2))], party: party),
            Frame(952, 4.2, [Actor(1, 0x33EE, c, combat: true)], party: party),
            EffectEvent(4.3, 1, 25245, 1), EffectEvent(4.8, 1, 25246, 2), EffectEvent(5.3, 1, 25247, 3),
            Frame(952, 5.5, [Actor(10, 0x233C, c), Actor(11, 0x233C, c)], party: party),
        ]);
        controller.Seek(.5);
        Check(Cleanse() != null && Dhrupad(), "Production Zot poison/Dhrupad events not routed.");
        Check((DateTime)Script().GetType().BaseType!.GetField("nextSlimeCleanse", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())! == default,
            "Replay attempted native cleanse execution.");
        controller.Seek(.8); Check(Cleanse() == null, "Production poison removal retained cleanse.");
        controller.Seek(1.1); Check(previews().Length == 2 && dangers() == 2, "Production transmuted helpers missing.");
        controller.Seek(2.1); Check(DamageCount() == 1, "Production Minduruva Bio lost Tankbuster prediction.");
        controller.Seek(5); Check(Dhrupad(), "Production Dhrupad cleared before third hit.");
        controller.Seek(5.4); Check(!Dhrupad(), "Production Dhrupad third hit leaked.");
        controller.Seek(5.6); Check(previews().Length == 0 && dangers() == 0, "Production helper cast finish leaked.");
        controller.Seek(1.1); Check(previews().Length == 2, "Minduruva Seek failed."); stop();

        c = new(-258, 0, -25.95682f);
        load(id,
        [
            Header(952), Frame(952, 0, [Actor(1, 0x33EF, c, combat: true), Actor(100, 0, c),
                Actor(101, 0, c + Vector3.UnitX * 10), Actor(20, 0x33F0, c)], full: true, party: party),
            Frame(952, 4, [Actor(20, 0x33F0, c, cast: Cast(25252, 2, c, startedAt: 4))], party: party),
            Frame(952, 6.1, [Actor(20, 0x33F0, c)], party: party),
            Frame(952, 7, [Actor(21, 0x33F0, c + Vector3.UnitX)], party: party),
            Frame(952, 21, [Actor(21, 0x33F0, c, cast: Cast(25252, 2, c, startedAt: 21))], party: party),
            Frame(952, 21.5, [], removed: [21UL], party: party),
            Frame(952, 22, [Actor(1, 0x33EF, c, combat: true, cast: Cast(25255, 3, c, startedAt: 22))], party: party),
        ]);
        controller.Seek(.1); Check(previews().Length == 1 && dangers() == 0, "Production sphere should preview only.");
        controller.Seek(3.7); Check(dangers() == 0, "Production first wave promoted early.");
        controller.Seek(3.9); Check(dangers() == 1, "Production first wave never promoted.");
        controller.Seek(6.2); Check(previews().Length == 0 && dangers() == 0, "Production cast finish did not clear first wave.");
        controller.Seek(7.1); Check(previews().Length == 1 && dangers() == 0, "Production later sphere lost twenty second delay.");
        controller.Seek(20.1); Check(dangers() == 1, "Production later sphere never promoted.");
        controller.Seek(21.6); Check(previews().Length == 0 && dangers() == 0, "Production source removal did not finish sphere wave.");
        controller.Seek(22.1);
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).Length == 0,
            "Manusya Stop incorrectly imposed shield/action prohibition.");
        controller.Seek(.1); Check(previews().Length == 1 && dangers() == 0, "Sanduruva Seek failed."); stop();

        c = new(-27.5f, 0, -49.5f);
        load(id,
        [
            Header(952), Frame(952, 0, [Actor(1, 0x33F1, c, combat: true), Actor(2, 0x33F2, c, combat: true),
                Actor(3, 0x33F3, c, combat: true), Actor(100, 0, c - Vector3.UnitX * 10), Actor(101, 0, c + Vector3.UnitX * 5)], full: true, party: party),
            Frame(952, .1, [Actor(10, 0x3610, c, cast: Cast(25272, 5, c, target: 100)),
                Actor(3, 0x33F3, c, combat: true, cast: Cast(25281, 4, c))], party: party),
            Frame(952, 1, [Actor(1, 0x33F1, c, combat: true, dead: true), Actor(20, 0x33F0, c)], party: party),
            Frame(952, 2, [Actor(3, 0x33F3, c, combat: true, cast: Cast(25290, 2, c, target: 100, startedAt: 2))], party: party),
            Frame(952, 4.2, [Actor(3, 0x33F3, c, combat: true)], party: party),
            EffectEvent(4.3, 3, 25282, 1), EffectEvent(4.8, 3, 25283, 2), EffectEvent(5.3, 3, 25284, 3),
            Frame(952, 5.4, [Actor(10, 0x3610, c), Actor(20, 0x33F0, c, cast: Cast(25279, 1.5, c, startedAt: 5.4)),
                Actor(11, 0x233C, c, cast: Cast(25265, 5, c, target: 100, startedAt: 5.4))], party: party),
            Frame(952, 5.9, [], removed: [3UL], party: party),
            Frame(952, 7, [Actor(20, 0x33F0, c)], party: party),
            Frame(952, 10.5, [Actor(11, 0x233C, c)], party: party),
            Frame(952, 11, [Actor(2, 0x33F2, c, dead: true)], party: party),
        ]);
        controller.Seek(.2); Check(Has("zot/stack/") && previews().Length == 1, "Production own-target stack missing gathering navigation.");
        controller.Seek(1.1);
        Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null && Has("zot/stack/") && Has("zot/sphere/") && dangers() == 2,
            "Production primary death cleared sisters or sphere.");
        controller.Seek(5); Check(Dhrupad(), "Production sisters Dhrupad cleared early.");
        controller.Seek(2.1); Check(DamageCount() == 1, "Production sisters Bio lost Tankbuster prediction.");
        controller.Seek(5.5); Check(!Dhrupad() && !Has("zot/stack/") && Has("party/separate/"), "Production sisters third hit/stack/spread transition differs.");
        controller.Seek(6); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Production second sister removal ended fight.");
        controller.Seek(7.1); Check(!Has("zot/sphere/") && dangers() == 1, "Production sisters sphere finish leaked.");
        controller.Seek(11.1); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0 && dangers() == 0,
            "Production final sister death did not clear encounter.");
        controller.Seek(1.1); Check(previews().Length == 2, "Sisters Seek failed."); stop();
        Check(previews().Length == 0 && dangers() == 0, "Zot Stop leaked output.");
        Console.WriteLine("PASS production Zot: all three bosses, helper spells, poison replay isolation, Dhrupad three hits, two sphere clocks, cancellation/destruction, all-sisters lifecycle, stack/spread, seek and stop");
    }
}
