using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunEndwalkerQuests(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(0, 0, -148);
        load("masterplayer.quest.where-everything-begins",
        [
            Header(1091), Frame(1091, 0, [Actor(1, 0x39B3, center, combat: true, targetable: false), Actor(100, 0, center)], full: true, party: party),
            Frame(1091, 1, [Actor(1, 0x39B3, center, targetable: false),
                Actor(3, 0x39B5, center + Vector3.UnitX * 4, combat: true, dead: true, targetable: false)], party: party),
            Frame(1091, 2, [Actor(3, 0x39B5, center + Vector3.UnitX * 4),
                Actor(2, 0x233C, center, cast: Cast(30021, 4, center, startedAt: 2))], party: party),
            Frame(1091, 10, [Actor(2, 0x233C, center)], party: party),
            Frame(1091, 11, [new { Id = 1ul, Oid = 0x39B3u, Kind = 2, Position = Pose(center), Dead = false,
                    Targetable = false, InCombat = false, Hp = 0u, MaxHp = 1000u },
                Actor(3, 0x39B5, center, combat: true)], party: party), Frame(1091, 12, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Value(Encounter(), "Active") == null!, "Scarmiglione primary combat incorrectly triggered pull.");
        controller.Seek(2.5);
        Check(dangers() == 1 && Aoes("job-quest/enemy/").Length == 1
            && previews().Any(p => ((string)Value(p, "Id")).Contains("cast/2")),
            "Scarmiglione dead untargetable trash pull, trash marker or dual-view circle.");
        controller.Seek(10.5);
        Check(dangers() == 0 && Value(Encounter(), "Active") != null, "Scarmiglione observed end or passive-primary lifetime.");
        controller.Seek(11.5);
        Check(dangers() == 0 && previews().Length == 0, "Scarmiglione primary zero HP must not restart from combat trash.");
        controller.Seek(2.5);
        Check(dangers() == 1, "Scarmiglione reverse Seek.");
        stop();
        Console.WriteLine("PASS production Scarmiglione: exact union arena, trash-only pull including dead untargetable actors, primary-free enemy drawing, observed Nox end, death and reverse Seek");

        foreach (var (territory, oid, script, action, max, arenaCenter) in new (uint, uint, string, uint, int, Vector3)[]
        {
            (1091, 0x39B3, "where-everything-begins", 30021, 5, new(0, 0, -148)),
            (1177, 0x4037, "the-game-is-afoot", 34888, 8, new(425, 0, -440)),
        })
        {
            var initial = new List<object> { Actor(1, oid, arenaCenter, combat: true), Actor(100, 0, arenaCenter),
                Actor(3, 0x39B5, arenaCenter, combat: true) };
            var records = new List<string> { Header(territory), Frame(territory, 0, initial, full: true, party: party),
                Frame(territory, 1, Enumerable.Range(0, max + 1).Select(i => Actor((ulong)(10 + i), 0x233C, arenaCenter,
                    cast: Cast(action, 20 - i, arenaCenter + Vector3.UnitX * i, startedAt: 1))), party: party),
                EffectEvent(2, 10, action, 1),
                Frame(territory, 3, [Actor(10, 0x233C, arenaCenter)], party: party),
                Frame(territory, 4, [Actor(30, 0x233C, arenaCenter, cast: Cast(action, 3, arenaCenter, startedAt: 4))], party: party),
                Frame(territory, 60, [], party: party) };
            load("masterplayer.quest." + script, records);
            controller.Seek(4.5);
            var sources = ((IEnumerable)Value(Encounter(), "OutputCasts")).Cast<object>()
                .Select(x => (ulong)Value(Value(x, "Value"), "Source")).ToArray();
            Check(sources.SequenceEqual(Enumerable.Range(11, max).Select(i => (ulong)i)) && dangers() == max,
                $"{action}: first-N FIFO promotion or vacant slot reuse in actual DLL.");
            controller.Seek(59d);
            Check(dangers() == max, $"{action}: timed out before observed end.");
            controller.Seek(2.5);
            Check(dangers() == max, $"{action}: effect removed circle or reverse Seek lost FIFO.");
            stop();
            Console.WriteLine($"PASS production Endwalker {action}: first {max} FIFO, hidden promotion, vacant-slot insertion, effect retention, real lifetime and reverse Seek");
        }

        center = new(-.07f, 0, -79);
        load("masterplayer.quest.a-frosty-reception",
        [
            Header(1010), Frame(1010, 0, [Actor(1, 0x3646, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(6, 0x777, center, isEnemy: false), Actor(7, 0x778, center, isEnemy: true),
                Actor(8, 0x779, center, isEnemy: true, targetable: false)], full: true, party: party),
            Frame(1010, 1, [Actor(2, 0x3648, center, targetable: false, dead: true), Actor(3, 0x3648, center + Vector3.UnitX * 5)], party: party),
            Frame(1010, 2, [Actor(2, 0x3648, center + Vector3.UnitX * 7, targetable: false, dead: true)], party: party),
            Frame(1010, 3, [Actor(2, 0x3648, center, cast: Cast(27461, 1, center - Vector3.UnitZ * 8, startedAt: 3)),
                Actor(3, 0x3648, center, cast: Cast(27463, 1, center + Vector3.UnitZ * 8, startedAt: 3))], party: party),
            Frame(1010, 4, [Actor(3, 0x3648, center)], party: party),
            Frame(1010, 5, [Actor(2, 0x3648, center)], party: party), Frame(1010, 6, [], party: party),
        ]);
        controller.Seek(2.5);
        Check(dangers() == 2 && Aoes("job-quest/enemy/").Length == 2
            && Aoes("frosty/lock/prediction/").All(a => (DateTime)Value(a, "Activation") == Start.AddSeconds(7.6))
            && Aoes("frosty/lock/prediction/").Any(a => Vector3.Distance((Vector3)Value(a, "Position"), center + Vector3.UnitX * 7) < .04f),
            "Frosty dynamic creation prediction or now+6.6.");
        controller.Seek(3.5);
        Check(Aoes("frosty/lock/prediction/").Length == 0 && dangers() == 2, "Frosty create-to-cast handoff.");
        controller.Seek(4.5);
        Check(dangers() == 1 && Vector3.Distance((Vector3)Value(Aoes("frosty/lock/cast/").Single(), "Position"), center + Vector3.UnitZ * 8) < .04f,
            "Frosty out-of-order caster finish did not remove global first AOE.");
        controller.Seek(5.5);
        Check(dangers() == 0 && Aoes("frosty/lock/").Length == 0, "Frosty final observed finish leaked.");
        controller.Seek(2.5);
        Check(dangers() == 2, "Frosty reverse Seek creation clock.");
        stop();
        Console.WriteLine("PASS production Vergilia: dead untargetable dynamic lock predictions, consumption clock, actual fixed-circle handoff, global FIFO ending and reverse Seek");
        load("masterplayer.quest.a-frosty-reception",
        [
            Header(1010), Frame(1010, 0, [Actor(1, 0x3646, center, combat: true), Actor(100, 0, center),
                Actor(101, 0x900, center + Vector3.UnitX * 10, subKind: 5, isEnemy: false)], full: true, party: party),
            Frame(1010, 1, [Actor(2, 0x233C, center, cast: Cast(27449, 5, center, target: 101, startedAt: 1)),
                Actor(3, 0x233C, center, cast: Cast(27457, 5, center, target: 100, startedAt: 1))], party: party),
            Frame(1010, 2, [Actor(3, 0x233C, center)], party: party),
            Frame(1010, 3, [Actor(3, 0x233C, center, cast: Cast(27457, 5, center, target: 101, startedAt: 3))], party: party),
            Frame(1010, 4, [Actor(3, 0x233C, center)], party: party),
            Frame(1010, 8, [Actor(2, 0x233C, center)], party: party), Frame(1010, 9, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Aoes("quest/spread/").Any(a => ((string)Value(a, "Id")).EndsWith("/separate/101")
            && (float)Value(Value(a, "Shape"), "Radius") == 7), "Frosty separate components skipped marked teammate.");
        controller.Seek(4.5);
        Check(Aoes("quest/spread/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")) is { } resin
            && (float)Value(Value(resin, "Shape"), "Radius") == 5, "Frosty cross-action target finish consumed Resin Bomb.");
        controller.Seek(8.5);
        Check(Aoes("quest/spread/").Length == 0, "Frosty spread final observed ending.");
        stop();
        Console.WriteLine("PASS production Vergilia spread components: independent teammate avoidance and action-scoped target FIFO endings");

        center = new(425, 0, -440);
        object[] colibriParty = [new { Slot = 0, EntityId = 100ul }, new { Slot = 8, EntityId = 102ul }];
        load("masterplayer.quest.the-game-is-afoot",
        [
            Header(1177), Frame(1177, 0, [Actor(1, 0x4037, center, combat: true), Actor(100, 0, center),
                Actor(101, 0x4039, center, subKind: 5, isEnemy: false), Actor(102, 0, center, dead: true, targetable: false)],
                full: true, party: colibriParty),
            Frame(1177, 1, [Actor(1, 0x4037, center, combat: true, cast: Cast(35888, 30, center + Vector3.UnitX * 20, startedAt: 1))], party: colibriParty),
            Frame(1177, 2, [Actor(1, 0x4037, center + Vector3.UnitZ * 9, combat: true,
                cast: Cast(35888, 30, center + Vector3.UnitX * 20, startedAt: 1))], party: colibriParty),
            Frame(1177, 3, [], removed: [102], party: party),
            Frame(1177, 4, [Actor(102, 0, center, dead: true, targetable: false)], party: colibriParty),
            Frame(1177, 35, [Actor(1, 0x4037, center, combat: true)], party: colibriParty), Frame(1177, 36, [], party: colibriParty),
        ]);
        controller.Seek(2.5);
        var projected = Aoes("colibri/charge/").Single();
        Check(dangers() == 1 && (bool)Value(projected, "Navigate")
            && Vector3.Distance((Vector3)Value(projected, "Position"), center) < .04f
            && MathF.Abs((float)Value(projected, "Rotation") - MathF.PI / 2) < .001f,
            "Colibri locked origin, destination-derived facing or dead/alliance/NPC-inclusive party count.");
        controller.Seek(3.5);
        Check(dangers() == 0 && !(bool)Value(Aoes("colibri/charge/").Single(), "Navigate")
            && previews().Any(p => ((string)Value(p, "Id")).Contains("colibri/charge/")),
            "Colibri two members should preserve drawing and remove only navigation.");
        controller.Seek(4.5);
        Check(dangers() == 1, "Colibri party-count navigation did not restore.");
        controller.Seek(35.5);
        Check(dangers() == 0 && Aoes("colibri/").Length == 0, "Colibri observed finish leaked charge.");
        controller.Seek(2.5);
        Check(dangers() == 1, "Colibri reverse Seek locked pose.");
        stop();
        Console.WriteLine("PASS production Colibri: locked endpoint charge, full party including dead alliance and NPC actors, navigation-only count switching, persistent dual-view drawing, observed finish and reverse Seek");

        foreach (var (territory, oid, script, stack, spread, radius, arenaCenter) in new (uint, uint, string, uint, uint, float, Vector3)[]
        {
            (1091, 0x39B3, "where-everything-begins", 30025, 30023, 6, new(0, 0, -148)),
            (1177, 0x4037, "the-game-is-afoot", 34890, 34893, 8, new(425, 0, -440)),
        })
        {
            load("masterplayer.quest." + script,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, arenaCenter, combat: true), Actor(100, 0, arenaCenter),
                    Actor(3, 0x39B5, arenaCenter, combat: true),
                    Actor(101, 0x900, arenaCenter + Vector3.UnitX * 12, subKind: 5, isEnemy: false)], full: true, party: party),
                Frame(territory, 1, [Actor(2, 0x233C, arenaCenter, cast: Cast(stack, 5, arenaCenter, target: 101, startedAt: 1))], party: party),
                Frame(territory, 2, [Actor(101, 0x900, arenaCenter + Vector3.UnitZ * 7, subKind: 5, isEnemy: false)], party: party),
                Frame(territory, 8, [Actor(2, 0x233C, arenaCenter)], party: party),
                Frame(territory, 9, [Actor(2, 0x233C, arenaCenter, cast: Cast(spread, 5, arenaCenter, target: 101, startedAt: 9))], party: party),
                Frame(territory, 10, [Actor(101, 0x900, arenaCenter + Vector3.UnitZ * 7, subKind: 5, isEnemy: false, dead: true)], party: party),
                Frame(territory, 11, [Actor(101, 0x900, arenaCenter + Vector3.UnitZ * 7, subKind: 5, isEnemy: false, targetable: false)], party: party),
                Frame(territory, 16, [Actor(2, 0x233C, arenaCenter)], party: party), Frame(territory, 17, [], party: party),
            ]);
            controller.Seek(2.5);
            Check(Aoes("quest/stack/").Any(a => (string)Value(a, "Id") == "quest/stack/join" && (bool)Value(a, "Navigate"))
                && Aoes("quest/stack/").Any(a => ((string)Value(a, "Id")).EndsWith("/display")
                    && Vector3.Distance((Vector3)Value(a, "Position"), arenaCenter + Vector3.UnitZ * 7) < .04f)
                && Damage().Length == 0, $"{stack}: dynamic NPC stack hard join or unexpected prediction.");
            controller.Seek(9.5);
            Check(Aoes("quest/spread/").Any(a => ((string)Value(a, "Id")).EndsWith("/display")
                    && (float)Value(Value(a, "Shape"), "Radius") == radius)
                && Aoes("quest/spread/").Any(a => ((string)Value(a, "Id")).EndsWith("/avoid")
                    && (float)Value(Value(a, "Shape"), "Radius") == radius + 1), $"{spread}: native radii.");
            controller.Seek(10.5);
            Check(Aoes("quest/spread/").Length == 0, $"{spread}: dead target retained component output.");
            controller.Seek(11.5);
            Check(Aoes("quest/spread/").Any(a => ((string)Value(a, "Id")).EndsWith("/display")),
                $"{spread}: untargetable revived reference lost display.");
            controller.Seek(16.5);
            Check(Aoes("quest/stack/").Length == 0 && Aoes("quest/spread/").Length == 0, $"{spread}: observed end cleanup.");
            controller.Seek(2.5);
            Check(Aoes("quest/stack/").Length > 0, $"{stack}: reverse Seek.");
            stop();
            Console.WriteLine($"PASS production Endwalker party {stack}/{spread}: dynamic NPC references, hard stack join, exact {radius}/{radius + 1} spread radii, dead hide and untargetable revival, observed endings, no invented damage, reverse Seek");
        }

        foreach (var (territory, oid, script, actions, arenaCenter) in new (uint, uint, string, uint[], Vector3)[]
        {
            (1010, 0x3646, "a-frosty-reception", [27440], new(-.07f, 0, -79)),
            (1177, 0x4037, "the-game-is-afoot", [34886, 34883, 34884], new(425, 0, -440)),
        })
        foreach (var action in actions)
        {
            load("masterplayer.quest." + script,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, arenaCenter, combat: true), Actor(100, 0, arenaCenter)], full: true, party: party),
                Frame(territory, 1, [Actor(1, oid, arenaCenter, combat: true, cast: Cast(action, 5, arenaCenter, target: 100, startedAt: 1))], party: party),
                Frame(territory, 9, [Actor(1, oid, arenaCenter, combat: true)], party: party), Frame(territory, 10, [], party: party),
            ]);
            controller.Seek(2d);
            Check(Damage().Length == 1 && dangers() == 0, $"{action}: unavoidable damage prediction or invented circle.");
            controller.Seek(9.5);
            Check(Damage().Length == 0, $"{action}: observed end retained damage.");
            stop();
        }
        Console.WriteLine("PASS production Endwalker unavoidable damage: Vergilia and Colibri independent raidwide/tankbuster predictions without geometry");
    }
}
