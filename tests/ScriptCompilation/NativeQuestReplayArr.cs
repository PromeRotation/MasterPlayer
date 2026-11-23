using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunArrQuests(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) },
        });
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        foreach (var (id, territory, boss, c, actions) in new (string, uint, uint, Vector3, uint[])[]
        {
            ("operation-archon", 1049, 0x38F5, new(-689, 0, -815), [28871, 28870, 28865, 28869, 28874, 28877, 28876]),
            ("ultimate-weapon", 1052, 0x3933, new(-704, 0, 480), [29329, 29331, 29762, 29335, 29760, 29337]),
            ("steps-of-faith", 1068, 0x3A5F, new(0, 0, 245), [31262, 30880, 26410, 30879, 30894, 30883]),
        })
        {
            var records = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, boss, c, combat: true, targetable: territory != 1068),
                    Actor(100, 0, c, combat: true),
                    .. actions.Select((a, i) => Actor((ulong)(10 + i), 0x233C, c,
                        cast: Cast(a, 3 + i, c, .7f, startedAt: 0)))], full: true, party: party),
                Frame(territory, 1, [Actor(100, 0, c, combat: true, dead: true)], party: party),
            };
            for (var i = 0; i < actions.Length; ++i)
                records.Add(Frame(territory, 3.4 + i, [Actor((ulong)(10 + i), 0x233C, c)], party: party));
            records.Add(Frame(territory, 12, [Actor(1, boss, c, combat: true, dead: true)], party: party));
            records.Add(Frame(territory, 13, [], party: party));
            load("masterplayer.quest." + id, records);
            controller.Seek(.1);
            var background = territory == 1068 ? 1 : 0;
            Check(dangers() == actions.Length && previews().Length == actions.Length + background, id + " helper output.");
            controller.Seek(1.1); Check(dangers() == actions.Length, id + " personal death reset.");
            for (var i = 0; i < actions.Length; ++i)
            {
                controller.Seek(3.5 + i);
                Check(dangers() == actions.Length - i - 1 && previews().Length == actions.Length - i - 1 + background,
                    id + " independent ending.");
            }
            controller.Seek(12.1); Check(Value(Encounter(), "Active") == null && dangers() == 0, id + " primary completion.");
            controller.Seek(.1); Check(dangers() == actions.Length, id + " reverse Seek.");
            stop(); Check(dangers() == 0 && previews().Length == 0, id + " owner cleanup.");
        }
        var center = new Vector3(-689, 0, -815);
        load("masterplayer.quest.operation-archon",
        [
            Header(1049), Frame(1049, 0, [Actor(1, 0x38F5, center, combat: true),
                Actor(100, 0, center + Vector3.UnitZ * 5, targetId: 1)], full: true, party: party),
            StatusEvent(.1, 1, 680, 1, true),
            Frame(1049, .5, [Actor(1, 0x38F5, center, combat: true, stackedStatus: 680, statusParam: 1)], party: party),
            Frame(1049, 1, [Actor(100, 0, center - Vector3.UnitZ * 5, targetId: 1)], party: party),
            Frame(1049, 2, [Actor(100, 0, center + Vector3.UnitZ * 5, targetId: 1)], party: party),
            StatusEvent(3, 1, 680, 0, false), Frame(1049, 3.1, [Actor(1, 0x38F5, center, combat: true)], party: party),
            Frame(1049, 4, [], party: party),
        ]);
        controller.Seek(.6);
        Check(((ulong[])Value(Encounter(), "ForbiddenTargets")).SequenceEqual(new ulong[] { 1 }) && dangers() == 1, "Archon parry restriction/navigation.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var restrictions = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)restrictions.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(restrictions)!
            && !(bool)Value(runtime.Setting, "RuntimeMovementHeld"), "Quest replay executed PR restrictions.");
        controller.Seek(1.1); Check(((ulong[])Value(Encounter(), "ForbiddenTargets")).Length == 0 && dangers() == 0, "Archon safe-side parry ban stale.");
        controller.Seek(2.1); Check(dangers() == 1, "Archon moving into shield.");
        controller.Seek(3.2); Check(dangers() == 0 && previews().Length == 0, "Archon parry status loss.");
        controller.Seek(.6); Check(dangers() == 1, "Archon status Seek.");
        stop();

        center = new(-704, 0, 480);
        load("masterplayer.quest.ultimate-weapon",
        [
            Header(1052), Frame(1052, 0, [Actor(1, 0x3933, center, combat: true),
                Actor(100, 0, center + Vector3.UnitX * 10), Actor(20, 0x3934, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(1052, .1, [Actor(1, 0x3933, center, combat: true, cast: Cast(29327, 4, center))], party: party),
            Map(5, 0, 0x00020001),
            Frame(1052, 5.1, [Actor(1, 0x3933, center, combat: true, cast: Cast(29332, 4, center, startedAt: 5.1))], party: party),
            Frame(1052, 9.5, [Actor(1, 0x3933, center, combat: true),
                Actor(10, 0x233C, center, cast: Cast(29758, 15, center, startedAt: 9.5))], party: party),
            Frame(1052, 11, [Actor(20, 0x3934, center + Vector3.UnitX * 8, cast: Cast(29333, 3, center + Vector3.UnitX * 8, startedAt: 11))], party: party),
            Frame(1052, 15, [Actor(20, 0x3934, center + Vector3.UnitX * 8)], party: party),
            Frame(1052, 25, [Actor(10, 0x233C, center)], party: party), Frame(1052, 26, [], party: party),
        ]);
        controller.Seek(4.5); Check(Group("lahabrea/arena").Length == 1 && dangers() == 1, "Lahabrea arena telegraph.");
        controller.Seek(5.1);
        dynamic arena = Value(Encounter(), "Arena");
        Check(!(bool)arena.Contains(center + Vector3.UnitX * 18, 0f), "Lahabrea map shrink.");
        controller.Seek(10);
        Check(Group("lahabrea/sphere").Length == 1 && Group("lahabrea/knockback/avoid").Length == 0, "Sphere suppresses KB navigation.");
        controller.Seek(14.4);
        Check(Group("lahabrea/sphere").Length == 0 && Group("lahabrea/knockback/avoid").Length == 1, "Sphere strict expiry KB navigation.");
        controller.Seek(25.1); Check(Group("lahabrea/knockback").Length == 0, "KB finish.");
        controller.Seek(10); Check(Group("lahabrea/sphere").Length == 1, "Lahabrea Seek clocks.");
        stop();

        center = new(0, 0, 245);
        var steps = new List<string>
        {
            Header(1068), Frame(1068, 0, [Actor(1, 0x3A5F, center, combat: true, targetable: false, hitboxRadius: 30),
                Actor(100, 0, center, combat: true), Actor(10, 0x233C, center), Actor(11, 0x233C, center)], full: true, party: party),
            Frame(1068, .1, [Actor(10, 0x233C, center, cast: Cast(30185, 5, center)),
                Actor(11, 0x233C, center, cast: Cast(26411, 4, center, .7f)),
                Actor(12, 0x233C, center, cast: Cast(30888, 7, center))], party: party),
            Frame(1068, 6, [Actor(10, 0x233C, center), Actor(11, 0x233C, center)], party: party),
            Frame(1068, 7.5, [Actor(12, 0x233C, center)], party: party),
            EffectEvent(7.5, 12, 30888, 1, center),
            Map(8, 0, 0x00800040),
            Frame(1068, 8.1, [Actor(100, 0, new(0, 0, 30), combat: true)], party: party),
        };
        for (var i = 1; i < 5; ++i)
        {
            steps.Add(Frame(1068, 8.5 + i, [Actor(12, 0x233C, center - Vector3.UnitZ * (7.5f * i))], party: party));
            steps.Add(EffectEvent(8.5 + i, 12, 26412, (uint)(1 + i), center - Vector3.UnitZ * (7.5f * i)));
        }
        for (uint i = 1; i <= 35; ++i) steps.Add(EffectEvent(14 + i * .2, 10, 30884, 100 + i));
        steps.Add(Frame(1068, 22, [], party: party));
        load("masterplayer.quest.steps-of-faith", steps);
        controller.Seek(6.1); Check(Group("steps/flame").Length == 2 && Group("steps/earthrising").Length == 2, "Steps channels after cast finish.");
        controller.Seek(8.2);
        Check(((Vector3)Value(Value(Encounter(), "Arena"), "Position")).Z == 30 && Group("steps/advance").Length == 1, "Steps scroll map/advance.");
        controller.Seek(12.6); Check(Group("steps/earthrising").Length == 0, "Steps five exaflare effects.");
        controller.Seek(16.9); Check(Group("steps/flame2").Length == 0 && Group("steps/flame").Length == 1, "Steps 14-hit channel.");
        controller.Seek(21.1); Check(Group("steps/flame").Length == 0, "Steps 35-hit channel.");
        controller.Seek(6.1); Check(Group("steps/flame").Length == 2, "Steps Seek rebuild channels.");
        manager.SetScriptEnabled("masterplayer.quest.steps-of-faith", false);
        Check(dangers() == 0 && previews().Length == 0, "Steps owner cleanup.");
        stop();
        Console.WriteLine("PASS ARR quests actual DLL three duties, independent helper clocks, personal death, directional parry and replay isolation, Lahabrea sphere/arena clocks, Vishap scroll and five/14/35-hit sequences, reverse Seek and owner cleanup");
    }
}
