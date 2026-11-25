using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureCenoteRooms(object managerObject, object runtimeObject, object controllerObject,
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
        bool Active() => Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null;
        const string guid = "masterplayer.treasure.cenote-ja-ja-gural";
        const uint territory = 1209;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var sourceDir = sourceAssembly.GetType("BossMod.WDir")!;
        foreach (var (room, trash, c, name) in new[]
        {
            (0x1EBAA4u, 0x4312u, new Vector3(0, 5, 377), "Room1"),
            (0x1EBAA5u, 0x4310u, new Vector3(0, 5, 192), "Room2"),
            (0x1EBAA6u, 0x431Du, new Vector3(160, 5, 19), "Room3"),
            (0x1EBAA7u, 0x4325u, new Vector3(-197, 5, -145), "Room4"),
        })
        {
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, room, c, targetable: false),
                    Actor(2, 0x4300, c), Actor(100, 0, c)], full: true, party: party),
                Frame(territory, 1, [Actor(3, trash, c, targetable: false, dead: true)], party: party),
                Frame(territory, 2, [Actor(1, room, c, targetable: false, dead: true)], removed: [3], party: party),
                JsonSerializer.Serialize(new { t = 2.2, kind = "ObjectRemove",
                    type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent",
                    data = new { SourceId = 1ul, Timestamp = Start.AddSeconds(2.2) } }),
                Frame(territory, 2.3, [Actor(3, trash, c)], party: party),
                Frame(territory, 2.5, [], removed: [1], party: party),
                Frame(territory, 2.6, [Actor(1, room, c, targetable: false)], party: party),
                Frame(territory, 3, [Actor(1, room, c, targetable: false, eventState: 7)], party: party),
                Frame(territory, 3.5, [Actor(1, room, c, targetable: false), Actor(3, trash, c)], party: party),
                Frame(territory, 4, [Actor(4, room, c, targetable: false)], removed: [1], party: party),
                Frame(territory, 5, [], removed: [4], party: party),
                Frame(territory, 6, [], party: party),
            ]);
            controller.Seek(.2);
            Check(!Active(), "Reward alone incorrectly started room.");
            controller.Seek(1.1);
            Check(Active() && Find("treasure/enemy/1").Length == 0 && Find("treasure/enemy/2").Length == 1,
                "Room CheckPull filtered dead/untargetable/noncombat trash or drew room object.");
            dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            dynamic oracle = sourceAssembly.GetType("BossMod.Dawntrail.TreasureHunt.CenoteJaJaGural." + name + "." + name)!
                .GetField("ArenaBounds", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            var sourceCenter = new Vector3((float)oracle.Center.X, 5, (float)oracle.Center.Z);
            var contains = ((object)oracle).GetType().GetMethod("Contains")!;
            Check(Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f, "Room source AABB center differs.");
            var compared = 0;
            for (var x = -29; x <= 29; ++x)
                for (var z = -29; z <= 24; ++z)
                {
                    var point = c + new Vector3(x + .137f, 0, z + .173f);
                    var offset = Activator.CreateInstance(sourceDir, [point.X - sourceCenter.X, point.Z - sourceCenter.Z])!;
                    Check((bool)boundary.Contains(point, 0f) == (bool)contains.Invoke((object)oracle, [offset])!,
                        $"Cenote {name} source arena mismatch at {point}.");
                    ++compared;
                }
            controller.Seek(2.1);
            Check(Active(), "Room object death or absent trash incorrectly ended phase.");
            controller.Seek(2.4);
            Check(!Active(), "ObjectRemove ahead of stale world resurrected old room.");
            controller.Seek(2.7);
            Check(Active(), "New room generation after observed absence failed.");
            controller.Seek(3.1);
            Check(!Active() && dangers() == 0 && previews().Length == 0, "Room state7 failed phase cleanup.");
            controller.Seek(3.6);
            Check(!Active(), "Completed same object restarted by state restoration.");
            controller.Seek(4.1);
            Check(Active(), "Restored room failed pull.");
            controller.Seek(5.1);
            Check(!Active() && previews().Length == 0, "Room destruction retained phase on live trash.");
            controller.Seek(1.1);
            Check(Active(), "Room reverse Seek failed.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Room stop leaked owner output.");
            Console.WriteLine($"PASS native treasure production Cenote {name} CheckPull object lifetime source arena oracle {compared} points reverse Seek and owner cleanup");

            var catalog = runtimeObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.NativeCenoteRoomCatalog")!;
            var definition = ((IEnumerable)catalog.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null)!).Cast<object>().Single(d => (uint)Value(d, "BossOid") == room);
            // Room1's dynamic charge is verified separately with real source/target references.
            var rules = ((IEnumerable)Value(definition, "Casts")).Cast<object>().Where(r => Value(r, "Shape") != null).ToArray();
            var initial = new List<object> { Actor(1, room, c, targetable: false), Actor(100, 0, c),
                Actor(90, trash, c), Actor(91, 0x4300, c), Actor(92, 0x42FF, c), Actor(93, 0x233C, c) };
            var casting = rules.Select((r, i) => Actor((ulong)(i + 2), trash, c,
                targetable: false, cast: Cast((uint)Value(r, "Action"), 3 + i, c, rotation: .37f))).ToArray();
            load(guid,
            [
                Header(territory), Frame(territory, 0, initial.ToArray(), full: true, party: party),
                Frame(territory, .1, casting, party: party),
                EffectEvent(.5, 2, (uint)Value(rules[0], "Action"), 1, c),
                Frame(territory, 1, [Actor(2, trash, c + Vector3.UnitX * 10,
                    targetable: false, cast: Cast((uint)Value(rules[0], "Action"), 3, c, rotation: .37f))], party: party),
                Frame(territory, 50, [], party: party),
                Frame(territory, 51, [Actor(2, trash, c, targetable: false)], party: party),
                Frame(territory, 52, casting.Skip(1).Select((_, i) => Actor((ulong)(i + 3), trash, c, targetable: false)).ToArray(), party: party),
                Frame(territory, 53, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == rules.Length && previews().Length - Find("treasure/enemy/").Length == rules.Length,
                "Room source rule geometry missing from navigation or dual-view previews.");
            Check(Find("treasure/enemy/").Length == 3
                && (Vector4)Value(Find("treasure/enemy/91").Single(), "Color") == new Vector4(1, 0, 1, 1),
                "Room complete trash/reward markers or helper exclusion differs.");
            Check(rules.All(r => (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent
                { SourceId = 2, ActionId = (ushort)(uint)Value(r, "Action") }))
                && new ushort[] { 38280, 38305, 9630 }.All(a => !(bool)manager.IsGenericDodgeSuppressed(
                    new LogSystemCastStartEvent { SourceId = 2, ActionId = a }))
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = 38279 }),
                "Room inactive actions or player source wrongly claimed.");
            var casts = ((IDictionary)Value(Encounter(), "Casts")).Values.Cast<object>().ToArray();
            Check(casts.All(v => Value(Value(v, "Rule"), "Shape").GetType().Name == "CircleTemplate"
                    || (float)Value(v, "Rotation") == .37f)
                && casts.Select(v => (DateTime)Value(v, "FinishAt")).Distinct().Count() == rules.Length,
                "Room source rotations or independent finish clocks lost.");
            controller.Seek(50.1);
            Check(dangers() == rules.Length, "Room effect, movement or predicted expiry cleared observed casts.");
            casts = ((IDictionary)Value(Encounter(), "Casts")).Values.Cast<object>().ToArray();
            Check(casts.All(v => (Vector3)Value(v, "Location") == c), "Room source movement changed cast lock location.");
            controller.Seek(51.1);
            Check(dangers() == rules.Length - 1, "Room first actual cancellation failed independent clear.");
            controller.Seek(52.1);
            Check(dangers() == 0 && Active(), "Room actual finish retained casts or ended phase.");
            controller.Seek(.2);
            Check(dangers() == rules.Length, "Room cast reverse Seek failed.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Room cast owner cleanup leaked.");
            Console.WriteLine($"PASS native treasure production Cenote {name} all {rules.Length} rules locked geometry independent clocks observed endings inactive boundary dual views navigation and Seek");
        }
        var a = new Vector3(0, 5, 192);
        var b = new Vector3(160, 5, 19);
        var d = new Vector3(-197, 5, -145);
        uint ActiveOid() => Active() ? (uint)Value(Value(Encounter(), "Active"), "BossOid") : 0;
        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x1EBAA5, a), Actor(2, 0x1EBAA6, b), Actor(3, 0x1EBAA7, d),
                Actor(11, 0x4310, a), Actor(12, 0x431D, b), Actor(13, 0x4325, d), Actor(100, 0, b)], full: true, party: party),
            Frame(territory, 1, [Actor(3, 0x1EBAA7, d), Actor(2, 0x1EBAA6, b), Actor(1, 0x1EBAA5, a, eventState: 7),
                Actor(11, 0x4310, a), Actor(12, 0x431D, b), Actor(13, 0x4325, d), Actor(100, 0, b)], full: true, party: party),
            Frame(territory, 2, [Actor(3, 0x1EBAA7, d, eventState: 7)], party: party),
            Frame(territory, 3, [Actor(2, 0x1EBAA6, b, eventState: 7), Actor(3, 0x1EBAA7, d)], party: party),
            Frame(territory, 4, [], party: party),
        ]);
        controller.Seek(.1);
        Check(ActiveOid() == 0x1EBAA5, "Closest room replaced first loaded module.");
        controller.Seek(1.1);
        Check(ActiveOid() == 0x1EBAA6, "Reverse full frame reordered room inheritance.");
        controller.Seek(3.1);
        Check(!Active(), "Unselected completed room restarted after restoration.");
        controller.Seek(.1);
        Check(ActiveOid() == 0x1EBAA5, "Cenote coexistence reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Cenote persistent room order reversed full frames unselected completion restore reverse Seek cleanup");

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x1EBAA5, a), Actor(11, 0x4310, a), Actor(100, 0, a)], full: true, party: party),
            Frame(territory, 1, [Actor(1, 0x1EBAA6, b, eventState: 7), Actor(12, 0x431D, b)], party: party),
            Frame(territory, 2, [Actor(1, 0x1EBAA6, b)], party: party),
            Frame(territory, 3, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(!Active(), "Completed replacement room started.");
        controller.Seek(2.1);
        Check(!Active(), "Old active room overwrote new OID completion receipt.");
        controller.Seek(.1);
        Check(ActiveOid() == 0x1EBAA5, "Cenote replacement reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Cenote same-ID different room completion receipt survives old primary ending reverse Seek cleanup");
    }
}
