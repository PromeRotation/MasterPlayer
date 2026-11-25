using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunChiMica(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Property(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        Vector3 Center(uint t) => t == 960 ? new(650, 0, 0) : new(791, 0, 593);
        object Boss(uint t, object? cast = null) => Actor(1, t == 960 ? 0x34CBu : 0x43EBu, Center(t), combat: true, cast: cast);
        string Native(double t, uint action, uint sequence) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 1ul, ActionId = action, MainTargetId = 100ul, GlobalSequence = sequence,
                Targets = new ulong[] { 100, 101, 102 }, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated(string type)
        {
            var script = ((IEnumerable)manager.Running.Values).Cast<object>().Select(s => Property(s, "Script"))
                .Single(s => s.GetType().Name == type);
            var hints = Property(script, "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Property(Property(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Chi/Mica replay leaked PR control or movement.");
        }
        load("masterplayer.fate.chi",
        [
            Header(960), Frame(960, 0, [Boss(960), Actor(100, 0, Center(960))], full: true),
            Frame(960, .1, [Boss(960, Cast(25957, 8, new(651, 0, 2), .3f))]),
            Frame(960, 1, [Boss(960)]), Native(2, 25957, 1), Native(3, 25961, 2), Frame(960, 4, []),
        ]);
        controller.Seek(.5);
        Check(previews().Length == 3 && dangers() == 2, "Chi combo did not publish two stages plus independent hard axis band.");
        controller.Seek(1.2);
        Check(dangers() == 2, "Real cast cancellation consumed effect-only combo.");
        controller.Seek(2.2);
        Check(previews().Length == 2 && dangers() == 1, "One native package consumed multiple stages or retained axis band.");
        controller.Seek(3.2);
        Check(previews().Length == 1 && dangers() == 0, "Chi second effect did not finish.");
        controller.Seek(.5);
        Isolated("Chi");
        stop();
        Console.WriteLine("PASS native fate Chi production source fixed arena dual combo previews independent axis navigation true Native FIFO cancel retains reverse Seek replay isolation");

        load("masterplayer.fate.chi",
        [
            Header(960), Frame(960, 0, [Boss(960), Actor(100, 0, Center(960)),
                Actor(10, 0x361E, new(640, 0, 0), targetable: false),
                Actor(11, 0x3505, new(660, 0, 0), targetable: false)], full: true),
            Frame(960, 1, [Actor(11, 0x3505, new(670, 0, 0), cast: Cast(25976, 12, new(670, 0, 0), startedAt: 1), targetable: false)]),
            Frame(960, 2, [Actor(11, 0x3505, new(670, 0, 0), targetable: false)]),
            Frame(960, 3, [], removed: [10]), Frame(960, 4, []),
        ]);
        controller.Seek(.5);
        Check(previews().Length == 3 && dangers() == 1, "Chi Bunker created display/risky split differs.");
        controller.Seek(2.2);
        Check(previews().Length == 2 && dangers() == 1, "Bunker other-source finish failed head removal and promotion.");
        controller.Seek(3.2);
        Check(previews().Length == 2 && dangers() == 1, "Destroy removed copied surviving Bunker.");
        manager.SetMechanismEnabled("masterplayer.fate.chi", "Chi", false);
        controller.Seek(3.3);
        Check(dangers() == 0 && previews().Length == 0, "Disabled Chi retained output.");
        manager.SetMechanismEnabled("masterplayer.fate.chi", "Chi", true);
        controller.Seek(.5);
        Isolated("Chi");
        stop();
        Console.WriteLine("PASS native fate Chi production Created Bunker clocks frozen positions risky window real finish global FIFO mechanism disable owner cleanup");

        var micaCards = Enumerable.Range(0, 6).Select(i => Actor((ulong)(20 + i), (uint)(0x43EE + i),
            new(770 + i % 3 * 14, 0, 583 + i / 3 * 20), targetable: false)).ToArray();
        load("masterplayer.fate.mica",
        [
            Header(1192), Frame(1192, 0, [Boss(1192), Actor(100, 0, Center(1192)), .. micaCards,
                Actor(10, 0x43EC, Center(1192))], full: true),
            Frame(1192, .1, [Boss(1192, Cast(38666, 4, Center(1192)))]),
            Frame(1192, 1, [Boss(1192, Cast(38673, 1.5f, Center(1192), startedAt: 1))]),
            Frame(1192, 2, [Boss(1192, Cast(38678, 4, Center(1192), startedAt: 2))]),
            Frame(1192, 3, [Actor(10, 0x43EC, Center(1192), cast: Cast(39156, 1.5f, Center(1192), startedAt: 3))]),
            Frame(1192, 4, [Actor(10, 0x43EC, Center(1192))]), Frame(1192, 5, []),
        ]);
        controller.Seek(.5);
        Check(previews().Length == 6 && dangers() == 0, "Unarmed Draw cards must be display-only.");
        controller.Seek(2.2);
        Check(previews().Length == 6 && dangers() == 5, "CardTrick failed to arm cached first pattern.");
        controller.Seek(4.2);
        Check(previews().Length == 6 && dangers() == 0, "Fake finish did not promote next unarmed pattern.");
        controller.Seek(2.2);
        Isolated("MicaTheMagicalMu");
        stop();
        Console.WriteLine("PASS native fate Mica production Draw five cards display-only cache first pattern CardTrick timing Fake finish navigation reverse Seek dual previews");

        load("masterplayer.fate.mica",
        [
            Header(1192), Frame(1192, 0, [Boss(1192), Actor(100, 0, Center(1192)),
                Actor(10, 0x43EC, Center(1192)), Actor(11, 0x43EC, Center(1192))], full: true),
            Frame(1192, .1, [Actor(10, 0x43EC, Center(1192), cast: Cast(38996, 2, Center(1192))),
                Actor(11, 0x43EC, Center(1192), cast: Cast(38999, 2, Center(1192)))]),
            Frame(1192, 1, [Actor(10, 0x43EC, Center(1192)), Actor(11, 0x43EC, Center(1192))]),
            Native(2, 38687, 1), Native(3, 38688, 2), Frame(1192, 4, []),
        ]);
        controller.Seek(.5);
        Check(previews().Length == 3 && dangers() == 2, "Mica all rolling stages were not both risky.");
        controller.Seek(1.2);
        Check(dangers() == 2, "Visual cancellation consumed rolling charge.");
        controller.Seek(2.2);
        Check(dangers() == 1 && previews().Length == 2, "Native charge package consumed multiple rolling stages.");
        controller.Seek(3.2);
        Check(dangers() == 0 && previews().Length == 1, "Last Native charge retained rolling stage.");
        controller.Seek(.5);
        Isolated("MicaTheMagicalMu");
        stop();
        Console.WriteLine("PASS native fate Mica production Rolling four source lengths count delay all risky true Native package FIFO visual finish retains reverse Seek");

        foreach (var territory in new uint[] { 960, 1192 })
        {
            var guid = territory == 960 ? "masterplayer.fate.chi" : "masterplayer.fate.mica";
            var type = territory == 960 ? "Chi" : "MicaTheMagicalMu";
            var action = territory == 960 ? 25972u : 38705u;
            var center = Center(territory);
            var cast = Cast(action, 5, center, target: 100);
            object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Boss(territory), Actor(100, 0, center),
                    Actor(101, 0, center + new Vector3(10, 0, 0))], full: true,
                    party: party),
                Frame(territory, .1, [Boss(territory, cast)], party: party),
                Frame(territory, 1, [Actor(100, 0, center + new Vector3(2, 0, 0))], party: party),
                Frame(territory, 2, [Boss(territory)], party: party), Frame(territory, 3, [], party: party),
            ]);
            controller.Seek(.5);
            Check(previews().Length == 2 && dangers() == 1, $"{type} self bait lacks outline or teammate avoidance.");
            controller.Seek(1.2);
            Check(previews().Length == 2 && dangers() == 1, $"{type} moving bait lost original target or activation.");
            controller.Seek(2.2);
            Check(previews().Length == 1 && dangers() == 0, $"{type} real finish did not clear bait.");
            controller.Seek(.5);
            Isolated(type);
            stop();
            Console.WriteLine($"PASS native fate {type} production dynamic circle tankbuster original target teammate navigation real finish damage and replay isolation");
        }
    }
}
