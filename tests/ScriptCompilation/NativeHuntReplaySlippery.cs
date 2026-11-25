using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntSlippery(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object[] Queue() => previews().Where(p => ((string)Value(p, "Id")).Contains("/slippery/"))
            .OrderBy(p => (string)Value(p, "Id"), StringComparer.Ordinal).ToArray();
        object[] March() => previews().Where(p => ((string)Value(p, "Id")).Contains("march/")).ToArray();
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x43DC, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(38648, 5, new(100, 0, 100)))], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, new(200, 0, 200))], party: party),
            Frame(territory, 3, [Actor(1, 0x43DC, new(50, 0, 0), combat: true,
                cast: Cast(38648, 5, new(-100, 0, -100), startedAt: 3))], party: party),
            Frame(territory, 4, [Actor(1, 0x43DC, default, combat: true)], party: party),
            EffectEvent(20, 999, 39559, 1), EffectEvent(20.01, 999, 39559, 1),
            EffectEvent(21, 1, 38650, 2), EffectEvent(22, 999, 38650, 3), EffectEvent(23, 1, 39559, 4),
            Frame(territory, 24, [], removed: [1], party: party)
        ]);
        controller.Seek(2.5);
        Check(Queue().Length == 2 && dangers() == 2, "Slippery Finish or future Risky semantics differ.");
        dynamic rect = Value(Queue()[0], "Geometry");
        dynamic ring = Value(Queue()[1], "Geometry");
        Check((bool)rect.Contains(new Vector3(100, 0, 110), 0f) && !(bool)rect.Contains(new Vector3(0, 0, 10), 0f)
            && (bool)ring.Contains(new Vector3(20, 0, 20), 0f) && !(bool)ring.Contains(new Vector3(100, 0, 100), 0f),
            "Slippery merged spell/caster origins or followed the moving caster.");
        controller.Seek(19d);
        Check(Queue().Length == 4 && dangers() == 4, "Slippery overlap hid or disabled future queue entries.");
        controller.Seek(20.5);
        Check(Queue().Length == 3 && dangers() == 3, "Slippery first effect failed FIFO/deduplication.");
        controller.Seek(23.5);
        Check(Queue().Length == 0 && dangers() == 0, "Slippery effect queue retained geometry.");
        controller.Seek(19d);
        Check(Queue().Length == 4 && dangers() == 4, "Slippery reverse Seek retained consumption.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x43DC, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default, stackedStatus: 2161, statusParam: 0,
                    statusExpiresAt: Start.AddSeconds(5.0001))], full: true, party: party),
            Frame(territory, .0001, [], party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(38648, 5, new(100, 0, 100)))], party: party),
            Frame(territory, 1, [Actor(100, 0, new(15, 0, 0), stackedStatus: 2161, statusParam: 0,
                statusExpiresAt: Start.AddSeconds(5.0001))], party: party),
            EffectEvent(3, 999, 39559, 5), EffectEvent(4, 1, 38650, 6),
            Frame(territory, 5, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 6, [], removed: [1], party: party)
        ]);
        controller.Seek(0d);
        Check(March().Length == 0, "PoisonGasMarch drew a stage beyond its five-second activation window.");
        controller.Seek(.00011);
        Check(March().Length == 2 && dangers() == 0, "PoisonGasMarch did not publish line/landing at its activation window.");
        controller.Seek(1.5);
        Check(((string?)Value(Script(), "MechanicHintsText"))?.Contains("Aim for green safe spot!") == true
            && !(bool)Value(Script(), "PyreticActive") && dangers() == 2,
            "PoisonGasMarch lost queue-only landing hint or added hold/navigation beyond the two existing AOEs.");
        controller.Seek(4.5);
        Check(((string?)Value(Script(), "MechanicHintsText")) == null && March().Length == 2 && dangers() == 0,
            "PoisonGasMarch retained consumed queue warning or lost its independent projection.");
        controller.Seek(6.1);
        Check(previews().Length == 0 && dangers() == 0, "Slippery owner removal retained march or AOE output.");
        stop();
        Console.WriteLine("PASS Slippery actual DLL independent spell/caster origins and locked queue, all-stage risky navigation, effect FIFO/deduplication, five-second forced-march projection and queue-only hint, reverse Seek and owner cleanup");
    }
}
