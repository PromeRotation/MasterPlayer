using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureLyhe(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        string[] Hints(Vector3 point) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [point])!).Cast<string>().ToArray();
        const string guid = "masterplayer.treasure.dungeons-lyhe-ghiah";
        const uint territory = 879;
        var c = new Vector3(0, 5, -390);
        object[] party = [.. Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) })];
        object[] players = [.. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c))];
        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x2BA7, c, combat: true), Actor(2, 0x2BAA, c),
                Actor(3, 0x2BA9, c), .. players], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x2BA7, c, combat: true, cast: Cast(17881, 3, c)),
                Actor(2, 0x2BAA, c, targetable: false, cast: Cast(17885, 7, c)),
                Actor(3, 0x2BA9, c, cast: Cast(17887, 11, c))], party: party),
            Frame(territory, 2, [Actor(1, 0x2BA7, c + Vector3.UnitX * 10, combat: true, cast: Cast(17881, 3, c))], party: party),
            Frame(territory, 4, [Actor(1, 0x2BA7, c, combat: true)], party: party),
            Frame(territory, 12, [], party: party),
            Frame(territory, 13, [Actor(2, 0x2BAA, c), Actor(3, 0x2BA9, c)], party: party),
            Frame(territory, 14, [], party: party),
        ]);
        controller.Seek(.2);
        Check(dangers() == 3 && previews().Count(p => !((string)Value(p, "Id")).Contains("treasure/enemy/")) == 3,
            $"Alichino independent normal AOE or untargetable SweetSong missing: dangers={dangers()}, ids={string.Join(';', previews().Select(p => Value(p, "Id")))}.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        var runtimeAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var room = runtimeAssembly.GetType("BossMod.Shadowbringers.TreasureHunt.DungeonsOfLyheGhiah.DungeonsOfLyheGhiahRoom5")!;
        var tuple = room.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        dynamic oracle = tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
        var sourceCenter = new Vector3((float)oracle.Center.X, 5, (float)oracle.Center.Z);
        var sourceContains = ((object)oracle).GetType().GetMethod("Contains")!;
        var direction = runtimeAssembly.GetType("BossMod.WDir")!;
        Check(Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f, "Lyhe source AABB center differs.");
        var compared = 0;
        for (var x = -28; x <= 28; ++x)
            for (var z = -419; z <= -368; ++z)
            {
                var point = new Vector3(x + .137f, 5, z + .173f);
                var offset = Activator.CreateInstance(direction, [point.X - sourceCenter.X, point.Z - sourceCenter.Z])!;
                Check((bool)boundary.Contains(point, 0f) == (bool)sourceContains.Invoke((object)oracle, [offset])!,
                    $"Lyhe source concave boundary differs at {point}.");
                ++compared;
            }
        controller.Seek(2.1);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f && dangers() == 3, "Moving boss shifted fixed room.");
        controller.Seek(4.1);
        Check(dangers() == 2, "One source Finish cleared independent casts.");
        controller.Seek(12.1);
        Check(dangers() == 2, "Expected expiry cleared observed-end casts.");
        controller.Seek(13.1);
        Check(dangers() == 0, "Observed Finish retained casts.");
        controller.Seek(.2);
        Check(dangers() == 3, "Reverse Seek failed to rebuild Alichino.");
        stop();
        Console.WriteLine($"PASS native treasure production Lyhe concave room source oracle {compared} points and independent cast clocks");

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x2BA5, c, combat: true),
                Actor(9, 0x1E9998, c, dead: true, targetable: false), .. players], full: true, party: party),
            Frame(territory, 1, [Actor(9, 0x1E9998, c + Vector3.UnitX * 10, dead: true, targetable: false)], party: party),
            Frame(territory, 2, [Actor(9, 0x1E9998, c, eventState: 7)], party: party),
            Frame(territory, 3, [Actor(9, 0x1E9998, c)], party: party),
            Frame(territory, 4, [], removed: [9], party: party),
            Frame(territory, 5, [], party: party),
        ]);
        controller.Seek(.2);
        dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        Check(Find("treasure/water/").Length == 1 && ((IEnumerable)snapshot.HardObstacles).Cast<object>().Count() == 1 && dangers() == 0
            && Hints(c).Contains("GTFO from voidzone!"), "Goliath dead untargetable water not hard obstacle.");
        controller.Seek(1.1);
        Check((Vector3)Value(Value(Find("treasure/water/").Single(), "Geometry"), "Position") == c + Vector3.UnitX * 10,
            "Water did not follow raw current actor position.");
        controller.Seek(2.1);
        snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        Check(Find("treasure/water/").Length == 0 && !((IEnumerable)snapshot.HardObstacles).Cast<object>().Any(), "EventState seven retained water.");
        controller.Seek(3.1);
        Check(Find("treasure/water/").Length == 1, "EventState zero did not restore water.");
        controller.Seek(4.1);
        Check(Find("treasure/water/").Length == 0, "Destroy retained water.");
        stop();
        Console.WriteLine("PASS native treasure production Goliath live water EventState filter raw positions and hard obstacle lifecycle");

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x2BA5, c, combat: true), .. players], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x2BA5, c, combat: true, cast: Cast(17877, 5, c, target: 100))], party: party),
            Frame(territory, 1, [Actor(107, 0, c + Vector3.UnitX * 15)], party: party),
            Frame(territory, 2, [Actor(100, 0, c, dead: true)], party: party),
            Frame(territory, 3, [Actor(100, 0, c)], party: party),
            Frame(territory, 7, [Actor(1, 0x2BA5, c, combat: true)], party: party),
            Frame(territory, 8, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/stack/").Length == 1 && dangers() == 1 && Hints(c).Contains("Stack (8/8)"),
            "Goliath exact eight safe outline and buddy navigation missing.");
        controller.Seek(1.1);
        Check(Hints(c).Contains("Stack! (7/8)"), "Goliath minimum was reduced.");
        controller.Seek(2.1);
        Check(Find("treasure/stack/").Length == 0 && dangers() == 0, "Dead target did not hide stack.");
        controller.Seek(3.1);
        Check(Find("treasure/stack/").Length == 1, "Revival failed to restore stack.");
        controller.Seek(6.1);
        Check(Find("treasure/stack/").Length == 1, "Predicted expiry cleared stack.");
        controller.Seek(7.1);
        Check(Find("treasure/stack/").Length == 0, "Actual Finish failed to clear stack.");
        controller.Seek(.2);
        Check(Hints(c).Contains("Stack (8/8)"), "Reverse Seek lost original stack.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Lyhe stop leaked owner outputs.");
        Console.WriteLine("PASS native treasure production Goliath exact eight target death revival actual finish reverse Seek and owner cleanup");

        foreach (var (oid, add) in new[] { (0x2BA7u, 0x2BA8u), (0x2BA5u, 0x2BA6u) })
        {
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(2, add, c),
                    Actor(3, 0x2A06, c), Actor(4, 0x2BAA, c), Actor(9, 0x1E9998, c), .. players], full: true, party: party),
                Frame(territory, 1, [Actor(1, oid, c, dead: true), Actor(2, add, c, dead: true)], party: party),
                Frame(territory, 2, [Actor(3, 0x2A06, c, dead: true)], party: party),
                Frame(territory, 3, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Find("treasure/enemy/").Length == 3 && (Vector4)Value(Find("treasure/enemy/3").Single(), "Color") == new Vector4(1, 0, 1, 1),
                "Lyhe marker All or reward vulnerable color differs.");
            controller.Seek(1.1);
            Check(Value(Encounter(), "Active") != null, "Live reward failed to retain Lyhe phase.");
            controller.Seek(2.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
                "Dead corpses, SweetSong or water incorrectly retained phase.");
            stop();
        }
        Console.WriteLine("PASS native treasure production Lyhe reward AllDeadOrDestroyed helper exclusions and vulnerable markers");
    }
}
