using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureFuathGargantuan(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        string[] Hints(Vector3 point) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [point])!).Cast<string>().ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        var c = new Vector3(100, 0, 100);
        object[] party = [.. Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) })];
        object[] players = [.. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c))];
        const string fuath = "masterplayer.treasure.shifting-oubliettes";
        load(fuath,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x302F, c, combat: true), Actor(2, 0x233C, c), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21737, 5, c, target: 100))], party: party),
            Frame(924, 1, [Actor(107, 0, c + Vector3.UnitX * 15)], party: party),
            Frame(924, 2, [Actor(100, 0, c, dead: true)], party: party),
            Frame(924, 3, [Actor(100, 0, c)], party: party),
            Frame(924, 7, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 8, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/stack/").Length == 1 && dangers() == 1 && Damage().Length == 0
            && (bool)Value(Find("treasure/stack/").Single(), "Safe") && Hints(c).Contains("Stack (8/8)"),
            "Fuath exact eight, safe outline, buddy navigation or no-shared-damage differs.");
        controller.Seek(1.1);
        Check(Hints(c).Contains("Stack! (7/8)"), "Fuath minimum was reduced below eight.");
        controller.Seek(2.1);
        Check(Find("treasure/stack/").Length == 0 && dangers() == 0, "Fuath dead target retained active stack.");
        controller.Seek(3.1);
        Check(Find("treasure/stack/").Length == 1 && dangers() == 1, "Fuath target revival failed to restore old reference.");
        controller.Seek(6.1);
        Check(Find("treasure/stack/").Length == 1, "Fuath prediction expiry ended real stack.");
        controller.Seek(7.1);
        Check(Find("treasure/stack/").Length == 0, "Fuath actual Finish retained stack.");
        controller.Seek(.2);
        Check(Hints(c).Contains("Stack (8/8)"), "Fuath reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Fuath exact eight, buddy navigation, target death and actual finish");

        var old = c + Vector3.UnitX * 2;
        load(fuath,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x302F, c, combat: true), Actor(2, 0x233C, c), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21737, 10, c, target: 101))], party: party),
            Frame(924, 1, [Actor(101, 0, old)], party: party),
            Frame(924, 2, [], removed: [101], party: party),
            Frame(924, 3, [Actor(101, 0, c + Vector3.UnitZ * 12)], party: party),
            Frame(924, 4, [], party: party),
        ]);
        controller.Seek(1.1);
        var frozen = (Vector3)Value(Value(Find("treasure/stack/").Single(), "Geometry"), "Position");
        controller.Seek(3.1);
        Check((Vector3)Value(Value(Find("treasure/stack/").Single(), "Geometry"), "Position") == frozen && dangers() == 1,
            "Fuath recreated target replaced old Actor reference.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Fuath stop leaked owner output.");
        Console.WriteLine("PASS native treasure production Fuath target destruction freezes old circle across same-ID recreation");

        load(fuath,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x302F, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, c), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21737, 5, c, target: 100)),
                Actor(3, 0x233C, c, cast: Cast(21737, 5, c, target: 101))], party: party),
            Frame(924, 1, [Actor(100, 0, c, dead: true)], party: party),
            Frame(924, 2, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Find("treasure/stack/").Length == 1 && !(bool)Value(Find("treasure/stack/").Single(), "Safe"),
            "Dead marked viewer incorrectly saw another stack as safe.");
        stop();
        Console.WriteLine("PASS native treasure production Fuath dead stored mark retains other stack danger outline");

        load(fuath,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x302F, c, combat: true), Actor(2, 0x233C, c), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21740, 3, c))], party: party),
            Frame(924, 4, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 5, [], party: party),
        ]);
        controller.Seek(.2);
        var cross = Value(previews().Single(p => ((string)Value(p, "Id")).EndsWith("/2")), "Geometry");
        bool Contains(Vector3 point) => (bool)cross.GetType().GetMethod("Contains")!.Invoke(cross, [point, 0f])!;
        Check(dangers() == 1 && Contains(c + new Vector3(2.4f, 0, 20)) && !Contains(c + new Vector3(3, 0, 3))
            && !Contains(c + Vector3.UnitZ * 41), "Fuath cross full width five or arm length forty differs.");
        stop();
        Console.WriteLine("PASS native treasure production Fuath helper cross exact forty arms and full width five");

        const string vault = "masterplayer.treasure.vault-oneiron";
        load(vault,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x4898, c, combat: true), Actor(2, 0x489A, c),
                Actor(3, 0x489A, c + Vector3.UnitX * 3), Actor(4, 0x4898, c), Actor(5, 0x4899, c), Actor(6, 0x48B9, c),
                Actor(100, 0, c)], full: true, party: party),
            Frame(1279, 1, [Actor(1, 0x4898, c, dead: true), Actor(4, 0x4898, c, dead: true),
                Actor(5, 0x4899, c, dead: true), Actor(6, 0x48B9, c, dead: true)], party: party),
            Frame(1279, 2, [], removed: [1, 4, 5, 6], party: party),
            Frame(1279, 3, [Actor(2, 0x489A, c, dead: true)], party: party),
            Frame(1279, 4, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/enemy/").Length == 5 && Find("treasure/enemy/2").Length == 1 && Find("treasure/enemy/3").Length == 0,
            "Gargantuan did not draw all trash/rewards and only first cached giant.");
        var model = Encounter();
        var castType = model.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonCastObservation")!;
        var packetCast = Activator.CreateInstance(castType, [43618u, c, 0f, Start, Start.AddSeconds(4.3), 0ul, null, null, null])!;
        model.GetType().GetMethod("StartCast")!.Invoke(model, [2ul, packetCast, Start.AddSeconds(.2), true]);
        var immediate = ((IEnumerable)model.GetType().GetMethod("AmbientAoes")!.Invoke(model, null)!).Cast<object>();
        Check(immediate.Any(a => (string)Value(a, "Id") == "treasure/enemy/2"), "Packet DTO replacement hid giant before next scan.");
        Console.WriteLine("PASS native treasure production Gargantuan cached marker survives immediate packet DTO replacement");
        controller.Seek(1.1);
        Check(Value(Encounter(), "Active") != null && Find("treasure/enemy/2").Length == 1, "Dead All corpses incorrectly ended phase.");
        controller.Seek(2.1);
        Check(Value(Encounter(), "Active") != null && Find("treasure/enemy/").Length == 1, "Cached live giant failed to retain after All destroy.");
        controller.Seek(3.1);
        Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
            "Later uncached giant extended phase after first giant died.");
        controller.Seek(2.1);
        Check(Find("treasure/enemy/2").Length == 1, "Gargantuan reverse Seek did not restore cached source.");
        stop();
        Console.WriteLine("PASS native treasure production Gargantuan AllDestroyed and first cached giant lifecycle");

        load(vault,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x4898, c, combat: true), Actor(2, 0x489A, c),
                Actor(100, 0, c), Actor(101, 0, c)], full: true, party: party),
            CastEvent(.1, 2, 43615, 5, c, target: 100),
            Frame(1279, 1, [], removed: [2], party: party),
            TreasureNativeEffect(2, 99, 43615, 1, 101, [100]),
            TreasureNativeEffect(3, 99, 43615, 2, 100, []),
            Frame(1279, 4, [], party: party),
        ]);
        controller.Seek(2.1);
        Check(Damage().Length == 1 && (ulong)Value(Damage().Single(), "TargetId") == 100,
            "Mash cancellation or expanded victim list consumed original target early.");
        controller.Seek(3.1);
        Check(Damage().Length == 0, "Mash cross-source native MainTarget failed FIFO.");
        controller.Seek(2.1);
        Check(Damage().Length == 1, "Mash Seek lost delayed history.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Gargantuan owner stop leaked output.");
        Console.WriteLine("PASS native treasure production Gargantuan native Mash original target survives source destruction and Seek");
    }
}
