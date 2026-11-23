using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSyrcus(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.alliance.syrcus";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Group() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("syrcus/curtain")).ToArray();
        bool Active() => Value(Encounter(), "Active") != null;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        foreach (var (boss, c, actions) in new (uint, Vector3, uint[])[]
        {
            (0xBC1, new(0, 0, -192), [2321, 2330, 2331]),
            (0xBD7, new(0, 0, -200), [2348, 2353]),
            (0xBE0, new(0, 0, -200), [2382, 1875, 2384, 2383]),
            (0xBDB, new(-400, 0, -200), [2355, 2357, 2356, 2358]),
        })
        {
            var records = new List<string>
            {
                Header(372),
                Frame(372, 0, [Actor(1, boss, c, combat: true), Actor(100, 0, c),
                    .. actions.Select((a, i) => Actor((ulong)(10 + i), 0x1B2, c,
                        cast: Cast(a, 3 + i, c, .7f, startedAt: 0)))], full: true, party: party),
                Frame(372, 1, [Actor(100, 0, c, dead: true)], party: party),
            };
            for (var i = 0; i < actions.Length; ++i)
                records.Add(Frame(372, 3.4 + i, [Actor((ulong)(10 + i), 0x1B2, c)], party: party));
            records.Add(Frame(372, 10, [Actor(1, boss, c, dead: true)], party: party));
            records.Add(Frame(372, 11, [], party: party));
            load(id, records);
            controller.Seek(.1);
            Check(Active() && dangers() == actions.Length && previews().Length == actions.Length, $"Syrcus {boss:X} native helper output.");
            controller.Seek(1.1); Check(Active() && dangers() == actions.Length, "Syrcus party death reset.");
            for (var i = 0; i < actions.Length; ++i)
            {
                controller.Seek(3.5 + i);
                Check(dangers() == actions.Length - i - 1 && previews().Length == actions.Length - i - 1,
                    "Syrcus independent finish order.");
            }
            controller.Seek(10.1); Check(!Active() && dangers() == 0, "Syrcus primary death.");
            controller.Seek(.1); Check(dangers() == actions.Length, "Syrcus reverse Seek.");
            manager.SetScriptEnabled(id, false);
            Check(dangers() == 0 && previews().Length == 0, "Syrcus disabled owner cleanup.");
            stop();
        }
        var center = new Vector3(0, 0, -200);
        load(id,
        [
            Header(372), Frame(372, 0, [Actor(1, 0xBE0, center, combat: true), Actor(100, 0, center + new Vector3(10, 0, 4)),
                Actor(20, 0xBE1, center + Vector3.UnitX * 10, hitboxRadius: 1.8f)], full: true, party: party),
            Frame(372, .1, [Actor(1, 0xBE0, center, combat: true, cast: Cast(2441, 15, center))], party: party),
            Frame(372, .5, [Actor(20, 0xBE1, center + Vector3.UnitZ * 10, dead: true, hitboxRadius: 1.8f)], party: party),
            Frame(372, 1, [Actor(10, 0x1B2, center, cast: Cast(2384, 3, center + Vector3.UnitX * 15))], party: party),
            Frame(372, 4.5, [Actor(10, 0x1B2, center)], party: party),
            Frame(372, 16, [Actor(1, 0xBE0, center, combat: true)], party: party),
            Frame(372, 17, [], party: party),
        ]);
        controller.Seek(.6);
        Check(Group().Length == 2 && dangers() == 1 && previews().Length == 1, "Syrcus Curtain shelter output.");
        var safe = Group().Single(a => (bool)Value(a, "Safe"));
        dynamic geometry = Value(safe, "Shape").GetType().GetMethod("Resolve")!.Invoke(Value(safe, "Shape"), [center, 0f, null])!;
        Check((bool)geometry.Contains(center + Vector3.UnitX * 15, 0f)
            && !(bool)geometry.Contains(center + Vector3.UnitZ * 15, 0f), "Syrcus ice snapshot moved.");
        controller.Seek(1.1); Check(dangers() == 2 && previews().Length == 2, "Syrcus simultaneous shelter/thunder.");
        controller.Seek(4.6); Check(dangers() == 1 && Group().Length == 2, "Thunder finish consumed shelter.");
        controller.Seek(16.1); Check(dangers() == 0 && previews().Length == 0, "Curtain actual finish cleanup.");
        controller.Seek(1.1); Check(dangers() == 2 && Group().Length == 2, "Curtain Seek rebuild.");
        stop(); Check(dangers() == 0 && previews().Length == 0, "Syrcus stopped owner leak.");
        Console.WriteLine("PASS Syrcus actual DLL four bosses, independent helper clocks, party death, ice snapshot and concurrent thunder, actual finish, reverse Seek and owner cleanup");
    }
}
