using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntPedantry(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object[] Cheeks() => previews().Where(p => ((string)Value(p, "Id")).Contains("/pedantry/cheek/")).ToArray();
        object[] Baits() => previews().Where(p => ((string)Value(p, "Id")).Contains("/bait/")).ToArray();
        string? Text(string name) => (string?)Value(Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script"), name);
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        foreach (var action in new uint[] { 17446, 17448 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x298A, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(action, 5, default))], party: party),
                Frame(territory, 6, [Actor(999, 0x233C, default)], party: party),
                EffectEvent(6.2, 1, 17447, 1), EffectEvent(6.21, 1, 17447, 1), EffectEvent(8, 999, 17449, 2),
                Frame(territory, 9, [], removed: [1], party: party)
            ]);
            controller.Seek(1d);
            Check(Cheeks().Length == 2 && dangers() == 1, "Pedantry Cheek second stage was not a preview-only half circle.");
            controller.Seek(6.1);
            Check(Cheeks().Length == 2 && dangers() == 1, "Pedantry Cheek Finish consumed the effect queue.");
            controller.Seek(6.5);
            Check(Cheeks().Length == 1 && dangers() == 1, "Pedantry Cheek effect failed promotion/deduplication.");
            controller.Seek(8.5);
            Check(Cheeks().Length == 0 && dangers() == 0, "Pedantry Cheek final effect retained output.");
            controller.Seek(1d);
            Check(Cheeks().Length == 2 && dangers() == 1, "Pedantry Cheek reverse Seek retained consumption.");
            controller.Seek(9.1);
            Check(previews().Length == 0 && dangers() == 0, "Pedantry owner removal retained Cheek output.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x298A, default, combat: true), Actor(100, 0, new(0, 0, 10)),
                Actor(101, 0, new(1, 0, 10)), Actor(102, 0, new(20, 0, 0))], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x298A, default, combat: true, cast: Cast(17440, 5, default, target: 100))], party: party),
            Frame(territory, 2, [Actor(1, 0x298A, default, combat: true)], party: party),
            Frame(territory, 3, [Actor(1, 0x298A, new(1, 0, 0), combat: true),
                Actor(100, 0, new(0, 0, 12)), Actor(101, 0, new(1, 0, 12))], party: party),
            EffectEvent(4, 1, 17440, 3),
            Frame(territory, 5, [], party: party),
        ]);
        controller.Seek(2.5);
        Check(Baits().Length == 1 && dangers() == 2 && Text("PredictedDamageText") == "Tankbuster：1人/1次"
            && Text("MechanicHintsText")?.Contains("Tankbuster cleave") == true,
            "Fever Finish lost dynamic bait, personal avoid-cones, damage or persistent global hint.");
        controller.Seek(3.5);
        dynamic cone = Value(Baits().Single(), "Geometry");
        Check((bool)cone.Contains(new Vector3(1, 0, 14), 0f) && !(bool)cone.Contains(new Vector3(1, 0, -10), 0f),
            "Fever did not follow live source/target orientation.");
        controller.Seek(4.5);
        Check(Baits().Length == 0 && dangers() == 0 && Text("PredictedDamageText") == null, "Fever real effect retained output.");
        stop();

        string Shock(double at) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 999, ActionId = 17900, MainTargetId = 100, Targets = new ulong[] { 101, 100 },
                GlobalSequence = 40, Timestamp = Start.AddSeconds(at) }
        });
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x298A, default, combat: true, targetId: 102),
                Actor(999, 0x233C, new(20, 0, 0)), Actor(100, 0, new(30, 0, 0)),
                Actor(101, 0, new(0, 0, 20)), Actor(102, 0, new(0, 0, -20))], full: true, party: party),
            Shock(.1), Shock(.11), EffectEvent(.12, 999, 17900, 40, target: 101),
            Frame(territory, 1, [Actor(100, 0, new(0, 0, 40))], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, new(20, 0, 0), cast: Cast(17444, 5, default, target: 101, startedAt: 2))], party: party),
            Frame(territory, 3, [Actor(999, 0x233C, new(20, 0, 0))], party: party),
            Frame(territory, 3.5, [Actor(101, 0, new(0, 0, 20), dead: true), Actor(1, 0x298A, default, combat: true, targetId: 102)], party: party),
            Frame(territory, 4, [], removed: [101], party: party),
            Frame(territory, 5, [Actor(101, 0, new(0, 0, 80))], party: party),
            EffectEvent(6, 1, 17445, 41),
            Frame(territory, 7, [], party: party)
        ]);
        controller.Seek(.5);
        Check(Baits().Length == 1 && dangers() == 2, "Native main-target Shock was missed, expanded or double-consumed.");
        dynamic line = Value(Baits().Single(), "Geometry");
        Check((bool)line.Contains(new Vector3(29, 0, 0), 0f) && !(bool)line.Contains(new Vector3(31, 0, 0), 0f),
            "Witch used helper origin, first target or current tank instead of Primary/MainTarget.");
        controller.Seek(1.5);
        line = Value(Baits().Single(), "Geometry");
        Check((bool)line.Contains(new Vector3(0, 0, 39), 0f) && !(bool)line.Contains(new Vector3(29, 0, 0), 0f),
            "Witch failed dynamic direction/length.");
        controller.Seek(3.2);
        line = Value(Baits().Single(), "Geometry");
        Check((bool)line.Contains(new Vector3(0, 0, 19), 0f) && !(bool)line.Contains(new Vector3(0, 0, 21), 0f),
            "Witch actual Finish failed to replace its target.");
        controller.Seek(5.5);
        line = Value(Baits().Single(), "Geometry");
        Check((bool)line.Contains(new Vector3(0, 0, 19), 0f) && !(bool)line.Contains(new Vector3(0, 0, 79), 0f),
            "Witch dead/destroyed target was cleared, tank-swapped or rebound to same-ID replacement.");
        controller.Seek(6.5);
        Check(Baits().Length == 0 && dangers() == 0, "Witch second effect retained bait.");
        controller.Seek(.5);
        Check(Baits().Length == 1 && dangers() == 2, "Witch reverse Seek retained future target state.");
        var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        var encounter = script.GetType().BaseType!.GetField("encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        var cachedActors = (IDictionary)encounter.GetType().GetField("actors", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(encounter)!;
        cachedActors.Remove(101ul);
        var routers = (IEnumerable)controllerObject.GetType().GetField("_routers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(controllerObject)!;
        var pair = routers.Cast<object>().Single();
        var router = pair.GetType().GetField("Item2")!.GetValue(pair)!;
        var effectType = script.GetType().Assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent")!;
        var effect = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            SourceId = 999ul, ActionId = 17900u, MainTargetId = 101ul,
            Targets = new ulong[] { 100 }, GlobalSequence = 42u, Timestamp = Start.AddSeconds(.5)
        }), effectType)!;
        router.GetType().GetMethod("Raise")!.Invoke(router, [effect]);
        Check(Baits().Length == 2 && (int)Value(router, "ErrorCount") == 0,
            "Native Shock failed to refresh actors before consuming a one-shot main-target effect.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x298A, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, new(0, 0, 100),
                cast: Cast(17955, 5, new(0, 0, 100)))], party: party),
            Frame(territory, 6, [Actor(999, 0x233C, default)], party: party)
        ]);
        controller.Seek(5.2);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("gaze/")) == 3
            && dangers() == 0, "Pedantry helper gaze lost its 10000-range eye/pupil/view arc or added navigation geometry.");
        controller.Seek(6.1);
        Check(!previews().Any(p => ((string)Value(p, "Id")).Contains("gaze/")),
            "Pedantry helper gaze Finish retained drawing.");
        stop();
        Console.WriteLine("PASS ForgivenPedantry actual DLL Cheek effect FIFO/promotion, Fever persistent dynamic cleave and target damage, Native main-target Witch dynamic rectangle, Finish replacement, dead/destroyed identity, reverse Seek and cleanup");
    }
}
