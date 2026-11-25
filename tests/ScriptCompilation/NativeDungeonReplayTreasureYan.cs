using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureYan(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object o, string name) => o.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        const string guid = "masterplayer.treasure.vault-oneiron";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        var p = c + new Vector3(.012345f, 0, .04567f);
        load(guid,
        [
            Header(1279),
            Frame(1279, 0, [Actor(1, 0x1EBE48, c + Vector3.UnitZ * 2, targetable: false, dead: true),
                Actor(2, 0x1EBE47, c + Vector3.UnitX * 3, targetable: false, dead: true),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 15), Actor(20, 0x4902, c)], full: true, party: party),
            Frame(1279, 1, [Actor(2, 0x1EBE47, c + Vector3.UnitX * 4, targetable: false, dead: true)], party: party),
            Frame(1279, 2, [Actor(20, 0x4902, c, cast: Cast(44269, 4, c + Vector3.UnitZ * 6, startedAt: 2))], party: party),
            Frame(1279, 50, [], party: party),
            Frame(1279, 51, [Actor(20, 0x4902, c)], party: party),
            Frame(1279, 52, [], removed: [1], party: party),
            Frame(1279, 53, [], removed: [2], party: party), Frame(1279, 54, [], party: party),
        ]);
        controller.Seek(.1);
        Check(Goals() == 1 && dangers() == 0 && Aoes("treasure/sack/tower/").Length == 2
            && (Vector3)Value(Aoes("treasure/sack/goal").Single(), "Position") == c + Vector3.UnitX * 3,
            "Yan bootstrap dead sacks, weighted goal or tower display-only differs.");
        Check(Aoes("treasure/enemy/").Length == 1
            && (Vector4)Value(Aoes("treasure/enemy/").Single(), "Color") == new Vector4(1, 1, 0, 1)
            && (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [20ul, c])! == 0,
            "Yan acquired target or lost gold marker.");
        controller.Seek(1.1);
        Check((Vector3)Value(Aoes("treasure/sack/goal").Single(), "Position") == c + Vector3.UnitZ * 2,
            "Yan dynamic bag positions did not update weight comparison.");
        controller.Seek(50.1);
        Check(dangers() == 1 && Goals() == 1
            && (Vector3)Value(Aoes("treasure/sack/goal").Single(), "Position") == c + Vector3.UnitX * 4,
            "Rush did not retain real-end exclusion/navigation past predicted finish.");
        controller.Seek(51.1);
        Check(dangers() == 0 && (Vector3)Value(Aoes("treasure/sack/goal").Single(), "Position") == c + Vector3.UnitZ * 2,
            "Rush Finish failed to release sack exclusion.");
        controller.Seek(52.1);
        Check(Goals() == 1 && Aoes("treasure/sack/tower/").Length == 1, "Silver loss ended remaining gold.");
        controller.Seek(53.1);
        Check(Goals() == 0 && dangers() == 0 && previews().Length == 0, "AllDestroyed retained navigation/preview.");
        controller.Seek(.1);
        Check(Goals() == 1 && Aoes("treasure/sack/tower/").Length == 2, "Yan reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Yan silver CheckPull both sacks AllDestroyed weighted dynamic goals Rush actual clocks dual views no target execution Seek owner cleanup");

        load(guid,
        [
            Header(1279),
            Frame(1279, 0, [Actor(1, 0x1EBE48, p), Actor(2, 0x1EBE47, p),
                Actor(3, 0x1EBE47, c + Vector3.UnitZ * 10), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 15)], full: true, party: party),
            Frame(1279, 1, [], removed: [2], party: party),
            Frame(1279, 2, [Actor(1, 0x1EBE48, p + Vector3.UnitX * 5)], party: party),
            Frame(1279, 3, [], removed: [1], party: party),
            Frame(1279, 4, [Actor(1, 0x1EBE48, p)], party: party), Frame(1279, 5, [], party: party),
        ]);
        controller.Seek(.1);
        Check(Aoes("treasure/sack/tower/").Length == 3
            && (Vector3)Value(Aoes("treasure/sack/tower/")[0], "Position") == p, "Tower position was quantized.");
        var first = (string)Value(Aoes("treasure/sack/tower/")[0], "Id");
        controller.Seek(1.1);
        Check(Aoes("treasure/sack/tower/").Length == 2
            && (string)Value(Aoes("treasure/sack/tower/")[0], "Id") != first, "Coordinate FIFO used actor ownership.");
        controller.Seek(3.1);
        Check(Aoes("treasure/sack/tower/").Length == 2, "Moved bag destroy cleared original tower.");
        controller.Seek(4.1);
        Check(Aoes("treasure/sack/tower/").Length == 3, "Same ID recreated sack failed new tower.");
        stop();
        Console.WriteLine("PASS native treasure production Yan unquantized tower origins coordinate-first Destroy moved sack retention new generation MP replay isolation");

        string Create(double t, ulong id, uint oid, Vector3 position) => JsonSerializer.Serialize(new
        {
            t, kind = "ObjectCreate", type = "PromeRotation.LogSystem.LogSystemObjectCreateEvent",
            data = new { SourceId = id, DataId = oid, SourcePosition = Pose(position), Timestamp = Start.AddSeconds(t) }
        });
        string RemoveEvent(double t, ulong id) => JsonSerializer.Serialize(new
        {
            t, kind = "ObjectRemove", type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent",
            data = new { SourceId = id, Timestamp = Start.AddSeconds(t) }
        });
        string Rush(double t, Vector3 destination) => JsonSerializer.Serialize(new
        {
            t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
            data = new { SourceId = 20ul, ActionId = 44269u, CastTime = 4f, EffectPosition = Pose(destination), Timestamp = Start }
        });
        object CurrentCast() => ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>()
            .Select(pair => Value(pair, "Value")).Single();
        load(guid,
        [
            Header(1279),
            Frame(1279, 0, [Actor(1, 0x1EBE48, c + Vector3.UnitZ * 15), Actor(2, 0x1EBE48, c + Vector3.UnitZ * 10),
                Actor(3, 0x1EBE47, c + Vector3.UnitX * 2), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 15),
                Actor(20, 0x4902, c, cast: Cast(44269, 4, c + new Vector3(10, 0, 10)))], full: true, party: party),
            Frame(1279, .5, [Actor(20, 0x4902, c + Vector3.UnitX * 2,
                cast: Cast(44269, 4, c + new Vector3(10, 0, 10)))], party: party),
            Rush(.6, c + new Vector3(10, 0, 10)),
            Rush(.7, c + new Vector3(20, 0, 20)),
            Frame(1279, .8, [Actor(20, 0x4902, c + Vector3.UnitX * 2)], party: party),
            RemoveEvent(1, 2), Create(1.1, 2, 0x1EBE47, c - Vector3.UnitX * 2),
            Frame(1279, 1.2, [Actor(2, 0x1EBE48, c + Vector3.UnitZ * 10)], party: party),
            Frame(1279, 1.3, [Actor(2, 0x1EBE47, c - Vector3.UnitX * 2)], party: party),
            Frame(1279, 2, [], party: party),
        ]);
        controller.Seek(.1);
        var originalOrigin = (Vector3)Value(CurrentCast(), "Location");
        var originalRotation = (float)Value(CurrentCast(), "Rotation");
        controller.Seek(.65);
        Check((Vector3)Value(CurrentCast(), "Location") == originalOrigin
            && (float)Value(CurrentCast(), "Rotation") == originalRotation, "Duplicate packet relocked moved Rush origin.");
        controller.Seek(.75);
        Check((Vector3)Value(CurrentCast(), "Location") == originalOrigin
            && (float)Value(CurrentCast(), "Rotation") == originalRotation
            && MathF.Abs((float)Value(Value(Value(CurrentCast(), "Rule"), "Shape"), "Length") - MathF.Sqrt(800)) < .00001f,
            "Same direction packet length correction failed production navigation.");
        controller.Seek(1.25);
        Check(Aoes("treasure/sack/tower/").Any(a => (Vector3)Value(a, "Position") == c - Vector3.UnitX * 2),
            "Old OID snapshot destroyed the event-created new sack.");
        controller.Seek(1.5);
        Check(Aoes("treasure/sack/tower/").Length == 3
            && (Vector3)Value(Aoes("treasure/sack/goal").Single(), "Position") == c + Vector3.UnitX * 2,
            "Cross-OID confirmation lost a tower or recreated equal-score sack overtook older gold.");
        stop();
        Console.WriteLine("PASS native treasure production Yan packet creation cross OID stale frame generation equal-score creation order Rush raw origin duplicate and collinear length correction");
    }
}
