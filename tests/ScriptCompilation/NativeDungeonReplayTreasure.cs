using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasurePersistent(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        const string oubliette = "masterplayer.treasure.shifting-oubliettes";
        var eye = c + Vector3.UnitZ * 65;
        load(oubliette,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3022, eye, combat: true), Actor(2, 0x301D, c),
                Actor(3, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(1, 0x3022, eye, combat: true, cast: Cast(21674, 4, eye))], party: party),
            CastEvent(.1, 1, 21674, 4, eye),
            CastEvent(.2, 3, 21673, 3, c, target: 100),
            Frame(924, 1, [], removed: [3], party: party),
            TreasureNativeEffect(2, 99, 21673, 1, 101, [100]),
            TreasureNativeEffect(4.1, 99, 21673, 2, 100, []),
            Frame(924, 5, [Actor(1, 0x3022, eye, dead: true)], party: party),
            Frame(924, 6, [], party: party),
        ]);
        controller.Seek(4d);
        var plannedFacing = Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, 0f]);
        Check(Damage().Length == 1 && dangers() == 0 && Find("treasure/gaze/").Length == 3
            && plannedFacing is float,
            $"Korrigan delayed/gaze: damage={Damage().Length}, danger={dangers()}, eyes={Find("treasure/gaze/").Length}, facing={plannedFacing}.");
        controller.Seek(4.2);
        Check(Damage().Length == 0, "Korrigan matching native effect failed to consume original target.");
        controller.Seek(5.1);
        Check(Find("treasure/gaze/").Length == 0, "Korrigan observed eye finish leaked.");
        controller.Seek(4d);
        Check(Damage().Length == 1 && Find("treasure/gaze/").Length == 3, "Korrigan reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Korrigan delayed native target, cancelled source, gaze and Seek");

        var water = c + new Vector3(1.23456f, 0, 2.34567f);
        load(oubliette,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3025, c, combat: true), Actor(100, 0, c),
                Actor(2, 0x1EA7D5, water, dead: true, targetable: false)], full: true, party: party),
            Frame(924, .1, [Actor(1, 0x3025, c, combat: true, cast: Cast(21701, 3, water))], party: party),
            CastEvent(.1, 1, 21701, 3, water),
            Frame(924, 1, [Actor(2, 0x1EA7D5, water + Vector3.UnitX, dead: true, targetable: false)], party: party),
            Frame(924, 2, [Actor(2, 0x1EA7D5, water, dead: true, targetable: false, eventState: 7)], party: party),
            Frame(924, 3, [Actor(2, 0x1EA7D5, water, dead: true, targetable: false)], party: party),
            Frame(924, 4, [], removed: [2], party: party),
            Frame(924, 5, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Obstacles() == 1 && dangers() == 1 && Find("treasure/water/2").Length == 1
            && (Vector3)Value(Value(Find("treasure/water/2").Single(), "Geometry"), "Position") == water + Vector3.UnitX,
            "Serpent live water lost exact obstacle or merged with independent R8 cast.");
        controller.Seek(2.1);
        Check(Obstacles() == 0 && Find("treasure/water/2").Length == 0, "Serpent EventState7 retained water.");
        controller.Seek(3.1);
        Check(Obstacles() == 1, "Serpent water failed to reactivate.");
        controller.Seek(4.1);
        Check(Obstacles() == 0, "Destroyed Serpent water retained obstacle.");
        controller.Seek(1.1);
        Check(Obstacles() == 1 && dangers() == 1, "Serpent reverse Seek failed.");
        stop();
        Check(Obstacles() == 0, "Serpent owner stop leaked obstacle.");
        Console.WriteLine("PASS native treasure production Serpent independent R8 cast and R7.5 EventState water obstacle, movement and Seek");

        var next = c + Vector3.UnitX * 3;
        load("masterplayer.treasure.shifting-gymnasion",
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D33, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, next), Actor(100, 0, c)], full: true, party: party),
            CastEvent(.1, 2, 32242, 5.3, c, .6f),
            TreasureNativeEffect(.5, 2, 32242, 1, 100, []),
            TreasureNativeEffect(.6, 2, 33015, 2, 100, []),
            Frame(1123, 1, [], removed: [2], party: party),
            CastEvent(2, 3, 32242, 5.3, next, 1.2f),
            TreasureNativeEffect(2.5, 99, 33016, 3, 100, []),
            TreasureNativeEffect(3, 99, 33015, 4, 100, []),
            Frame(1123, 3.5, [], removed: [3], party: party),
            EffectEvent(4, 99, 33016, 5),
            TreasureNativeEffect(5, 99, 33016, 5, 100, []),
            Frame(1123, 6, [], party: party),
        ]);
        controller.Seek(2.6);
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 3, ActionId = 33015 })
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 3, ActionId = 33016 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = 33015 }),
            "Megakantha effect-only steps leaked generic dodge or claimed player source.");
        controller.Seek(4.5);
        Check(dangers() == 1 && Find("treasure/odious").Length == 1
            && (Vector3)Value(Value(Find("treasure/odious").Single(), "Geometry"), "Position") == next,
            "Megakantha new Start reset counter, source destroy cleared AOE or PR expanded effect double-counted.");
        controller.Seek(5.1);
        Check(dangers() == 0 && Find("treasure/odious").Length == 0, "Megakantha fifth effect failed to clear frozen half-circle.");
        controller.Seek(4.5);
        Check(dangers() == 1, "Megakantha reverse Seek lost continuous history.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Megakantha owner stop leaked output.");
        Console.WriteLine("PASS native treasure production Megakantha global five-hit half-circle, replacement, source destruction, native dedup and Seek");
    }

    private static void RunTreasureAltars(object managerObject, object runtimeObject, object controllerObject,
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
        const string id = "masterplayer.treasure.shifting-altars";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul },
            new { Slot = 2, EntityId = 102ul }];
        foreach (var (oid, action) in new[] { (0x2536u, 13263u), (0x253Bu, 13784u) })
        {
            var eye = c + Vector3.UnitZ * 65;
            var cast = Cast(action, 4, eye);
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, eye, combat: true), Actor(100, 0, c)], full: true, party: party),
                Frame(794, .1, [Actor(1, oid, eye, combat: true, cast: cast)], party: party),
                Frame(794, 1, [Actor(1, oid, c, combat: true, cast: cast)], party: party),
                Frame(794, 5, [Actor(1, oid, c, combat: true)], party: party),
                Frame(794, 6, [], party: party),
            ]);
            controller.Seek(4d);
            Check(dangers() == 0 && Find("treasure/gaze/").Length == 3
                && (Vector3)Value(Value(Find("treasure/gaze/1/eye").Single(), "Geometry"), "Position") == eye
                && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, 0f]) is float,
                "Altar gaze range/lock/dual views/facing intent differs.");
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                "Altar replay executed PR facing.");
            controller.Seek(5.1);
            Check(Find("treasure/gaze/").Length == 0, "Altar cancelled gaze retained preview.");
            controller.Seek(4d);
            Check(Find("treasure/gaze/").Length == 3, "Altar reverse Seek lost gaze.");
            stop();
            Console.WriteLine("PASS native treasure production altar gaze default range, locked eye, facing and replay isolation " + action);
        }
        foreach (var oid in new[] { 0x2535u, 0x2536u, 0x253Bu, 0x253Fu })
        {
            var source = c - Vector3.UnitZ * 5;
            var second = c - Vector3.UnitX * 4;
            var cast = Cast(8598, 3, source);
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, c, combat: true),
                    Actor(2, 0x2545, source, targetId: 101), Actor(3, 0x2545, second, targetId: 101),
                    Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 5), Actor(102, 0, c, dead: true)], full: true, party: party),
                Frame(794, .1, [Actor(2, 0x2545, source, targetId: 101, cast: cast)], party: party),
                TreasureNativeEffect(.5, 2, 8599, 1, 101, []),
                Frame(794, 2, [Actor(2, 0x2545, source, targetId: 100, cast: cast),
                    Actor(3, 0x2545, second, targetId: 100)], party: party),
                Frame(794, 3, [], removed: [2, 3], party: party),
                Frame(794, 4, [], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == 3 && Find("treasure/cleave/").Length == 2
                && Find("treasure/cleave/").All(p => (bool)Value(p, "OutlineOnly")),
                "Multiple casting Matanga cleave lost navigation or source outlines.");
            controller.Seek(2.1);
            var areas = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
            Check(areas.Length == 3 && Find("treasure/cleave/").Length == 2,
                "Current target bait did not replace old avoidance or included dead party.");
            var name = (string)Value(Value(Encounter(), "Active"), "Name");
            manager.SetMechanismEnabled(id, name, false);
            controller.Seek(2.2);
            Check(dangers() == 0 && Find("treasure/cleave/").Length == 0, "Disabled altar retained cleave.");
            manager.SetMechanismEnabled(id, name, true);
            controller.Seek(1d);
            Check(dangers() == 3, "Reverse Seek lost continuous cleave.");
            controller.Seek(3.1);
            Check(dangers() == 0 && Find("treasure/cleave/").Length == 0, "Destroyed Matanga retained cleave/cast.");
            stop();
            Console.WriteLine("PASS native treasure production Matanga multiple continuous cleaves, casting, target bait, disable and Seek " + oid);
        }
    }

    private static void RunTreasureGeometry(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] MechanicPreviews() => previews().Where(p => !((string)Value(p, "Id")).Contains("treasure/enemy/")).ToArray();
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (id, territory, oid, bonus, action, name) in new[]
        {
            ("masterplayer.treasure.vault-oneiron", 1279u, 0x48B0u, 0x48B7u, 43719u, "Gilded Sentry"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x2541u, 0x2544u, 13365u, "The Great Gold Whisker"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x2542u, 0x2546u, 13368u, "Altar Mandragora"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x2538u, 0x2546u, 13360u, "Hati"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x2535u, 0x2545u, 13310u, "Altar Skatene"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x2536u, 0x2544u, 13262u, "Altar Beast"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x253Bu, 0x2544u, 13455u, "Altar Arachne"),
            ("masterplayer.treasure.shifting-altars", 794u, 0x253Fu, 0x2544u, 13390u, "The Older One"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D41u, 0x3D4Fu, 32299u, "Gymnasiou Mandragoras"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D27u, 0x3D4Eu, 32816u, "Gymnasiou Leon"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D40u, 0x3D4Du, 32292u, "Lampas Chrysine"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D29u, 0x3D4Fu, 32209u, "Gymnasiou Tigris"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D2Du, 0x3D4Du, 32225u, "Gymnasiou Satyros"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D30u, 0x3D4Fu, 32231u, "Gymnasiou Triton"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D3Bu, 0x3D4Fu, 32272u, "Gymnasiou Sphinx"),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D33u, 0x3D4Fu, 32240u, "Gymnasiou Megakantha"),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3011u, 0x301Du, 21663u, "Secret Undine"),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x302Bu, 0x301Du, 21720u, "Secret Swallow"),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3016u, 0x301Du, 21669u, "Secret Pegasus"),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3018u, 0x301Du, 22780u, "Greedy Pixie"),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3022u, 0x301Du, 21675u, "Secret Korrigan"),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3025u, 0x301Du, 21700u, "Secret Serpent"),
        })
        {
            var cast = Cast(action, 3, center, .6f);
            load(id,
            [
                Header(territory), Frame(territory, 0, [
                    Actor(1, oid, center, combat: true, objectIndex: 2, isEnemy: true),
                    Actor(2, bonus, center + Vector3.UnitX * 30, objectIndex: 4, isEnemy: true),
                    Actor(3, 0x233C, center, objectIndex: 6, isEnemy: true),
                    Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX)], full: true, party: party),
                Frame(territory, .1, [Actor(3, 0x233C, center, cast: cast, objectIndex: 6, isEnemy: true)], party: party),
                CastEvent(.1, 3, action, 3, center, .6f),
                EffectEvent(1, 3, action, 1),
                Frame(territory, 2, [Actor(1, oid, center, combat: false, objectIndex: 2, isEnemy: true),
                    Actor(3, 0x233C, center + Vector3.UnitX * 10, cast: cast, objectIndex: 6, isEnemy: true)], party: party),
                Frame(territory, 7, [Actor(3, 0x233C, center)], party: party),
                Frame(territory, 8, [Actor(1, oid, center, dead: true),
                    Actor(2, bonus, center, cast: Cast(action, 3, center, startedAt: 8), objectIndex: 4, isEnemy: true)], party: party),
                Frame(territory, 10, [Actor(2, bonus, center, dead: true)], party: party),
                Frame(territory, 11, [], party: party),
            ]);
            controller.Seek(.5);
            Check(dangers() == 1 && MechanicPreviews().Length == 1, name + " navigation/dual-view AOE.");
            Check(!(bool)manager.IsGenericDodgeSuppressed(action)
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 3, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                name + " generic dodge source ownership.");
            var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
            var host = RuntimeHelpers.GetUninitializedObject(hostType);
            Check(!(bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, null)!,
                name + " suppressed unconverted territory modules.");
            var areas = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
            var windows = ((IEnumerable)Value(areas.Single(), "DangerWindows")).Cast<object>().ToArray();
            Check((DateTime)Value(windows.Single(), "DangerFrom") == Start.AddSeconds(3.4), name + " exact activation.");
            dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check((bool)boundary.Contains(center, 0f) && !(bool)boundary.Contains(center + Vector3.UnitX * 20, 0f),
                name + " fixed source boundary.");
            var bonusMarker = previews().Single(p => ((string)Value(p, "Id")).EndsWith("treasure/enemy/2"));
            Check((Vector4)Value(bonusMarker, "Color") == new Vector4(1, 0, 1, 1), name + " vulnerable color.");
            controller.Seek(6d);
            Check(dangers() == 1 && MechanicPreviews().Length == 1, name + " effect/time expired live cast.");
            controller.Seek(7.1);
            Check(dangers() == 0 && MechanicPreviews().Length == 0, name + " observed cancellation cleanup.");
            controller.Seek(8.5);
            Check(dangers() == 1 && (bool)Script().GetType().BaseType!.GetProperty("EncounterActive")!.GetValue(Script())!,
                name + " primary death stopped bonus mechanics.");
            manager.SetMechanismEnabled(id, name, false);
            controller.Seek(8.6);
            Check(dangers() == 0 && MechanicPreviews().Length == 0, name + " mechanism disable cleanup.");
            manager.SetMechanismEnabled(id, name, true);
            controller.Seek(.5);
            Check(dangers() == 1, name + " reverse Seek did not rebuild.");
            controller.Seek(10.5);
            Check(dangers() == 0 && previews().Length == 0 && Damage().Length == 0,
                name + " unlisted helper blocked AllDeadOrDestroyed.");
            stop();
            Console.WriteLine("PASS native treasure production " + name + " geometry/time/phase/ownership/Seek/cleanup");
        }
        load("masterplayer.treasure.vault-oneiron",
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48A4, center, combat: true,
                cast: Cast(43645, 5, center)), Actor(100, 0, center)], full: true, party: party),
            Frame(1279, 5, [], party: party),
        ]);
        controller.Seek(1d);
        Check(dangers() == 1 && MechanicPreviews().Length == 1
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 43645 })
            && (bool)Script().GetType().BaseType!.GetProperty("EncounterActive")!.GetValue(Script())!,
            "Gwyddneu native ownership or first-frame Violet output missing.");
        stop();
        Console.WriteLine("PASS native treasure migrated Gwyddneu native ownership first-frame Violet dual views navigation");
    }

    private static void RunTreasurePredictions(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (id, territory, oid, action) in new[]
        {
            ("masterplayer.treasure.vault-oneiron", 1279u, 0x48B0u, 43725u),
            ("masterplayer.treasure.vault-oneiron", 1279u, 0x48B0u, 43723u),
            ("masterplayer.treasure.shifting-altars", 794u, 0x253Bu, 13343u),
            ("masterplayer.treasure.shifting-altars", 794u, 0x253Fu, 13389u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D41u, 32297u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D27u, 32200u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D40u, 32290u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D29u, 32206u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D2Du, 32218u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D30u, 32228u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D3Bu, 32265u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D3Bu, 32266u),
            ("masterplayer.treasure.shifting-gymnasion", 1123u, 0x3D33u, 32238u),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3011u, 21662u),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3018u, 21682u),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x302Bu, 21716u),
            ("masterplayer.treasure.shifting-oubliettes", 924u, 0x3025u, 21699u),
            ("masterplayer.treasure.cenote-ja-ja-gural", 1209u, 0x4305u, 38261u),
            ("masterplayer.treasure.cenote-ja-ja-gural", 1209u, 0x4305u, 38262u),
        })
        {
            var raidwide = action is 43723 or 21662 or 21682 or 38262 or 32266 or 13343 or 13389;
            var caster = action is 32290 or 21662 ? 3ul : 1ul;
            var casterOid = caster == 3 ? 0x233Cu : oid;
            load(id,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true), Actor(100, 0, center),
                    Actor(101, 0, center + Vector3.UnitX), Actor(3, 0x233C, center)], full: true, party: party),
                Frame(territory, .1, [Actor(caster, casterOid, center, combat: true,
                    cast: Cast(action, 5, center, target: 100))], party: party),
                Frame(territory, .5, [Actor(caster, casterOid, center, combat: true,
                    cast: Cast(action, 5.5, center, target: 100))], party: party),
                Frame(territory, 1, [Actor(caster, casterOid, center, combat: true,
                    cast: Cast(action, 5.5, center, target: 101))], party: party),
                Frame(territory, 2, [Actor(caster, casterOid, center, combat: true)], party: party),
            ]);
            controller.Seek(.6);
            var castValues = ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>()
                .Select(p => Value(p, "Value")).ToArray();
            Check(castValues.Length == 1 && (DateTime)Value(castValues.Single(), "FinishAt") == Start.AddSeconds(.1).AddSeconds(5 + .3)
                && Damage().Length > 0 && Damage().All(p => (DateTime)Value(p, "Activation") == Start.AddSeconds(.1).AddSeconds(5.5 + .3)),
                "Clock-only current CastInfo update used cached Start activation.");
            controller.Seek(1.1);
            var damage = Damage();
            Check(dangers() == 0 && damage.Length == (raidwide ? 2 : 1)
                && damage.All(p => (DateTime)Value(p, "Activation") == Start.AddSeconds(.1 + 5.5 + .3))
                && (raidwide || (ulong)Value(damage.Single(), "TargetId") == 101),
                "Treasure prediction used frozen target/activation or invented danger geometry.");
            controller.Seek(2.1);
            Check(Damage().Length == 0, "Observed unavoidable finish retained prediction.");
            stop();
            Console.WriteLine("PASS native treasure production current prediction clock and target " + action);
        }
    }

    private static void RunTreasureGymnasion(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string id) => previews().Where(p => ((string)Value(p, "Id")).Contains(id)).ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object[] Areas() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        var c = new Vector3(100, 0, 100);
        var eye = c + Vector3.UnitZ * 8;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        const string id = "masterplayer.treasure.shifting-gymnasion";
        var gaze = Cast(32207, 4, eye);
        load(id,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D29, eye, combat: true), Actor(100, 0, c), Actor(101, 0, c)], full: true, party: party),
            Frame(1123, .1, [Actor(1, 0x3D29, eye, combat: true, cast: gaze)], party: party),
            Frame(1123, 2, [Actor(1, 0x3D29, c + Vector3.UnitX * 12, combat: true, cast: gaze)], party: party),
            Frame(1123, 5, [Actor(1, 0x3D29, c, combat: true)], party: party),
            Frame(1123, 6, [], party: party),
        ]);
        controller.Seek(4d);
        Check(dangers() == 0 && Find("tigris/gaze/").Length == 3
            && (Vector3)Value(Value(Find("tigris/gaze/1/eye").Single(), "Geometry"), "Position") == eye,
            "Tigris missing locked eye/pupil/view or invented floor danger.");
        Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, 0f]) is float,
            "Tigris facing request not planned in deadline window.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Tigris replay executed PR facing.");
        controller.Seek(5.1);
        Check(Find("tigris/gaze/").Length == 0, "Tigris observed finish retained eye.");
        controller.Seek(4d);
        Check(Find("tigris/gaze/").Length == 3, "Tigris reverse Seek lost gaze.");
        stop();
        Console.WriteLine("PASS native treasure production Tigris gaze lock, facing, dual views, replay isolation and Seek");

        var wind = c + Vector3.UnitX * 4;
        load(id,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D2D, c, combat: true), Actor(100, 0, c),
                Actor(2, 0x3D2F, wind, dead: true, targetable: false, eventState: 7)], full: true, party: party),
            Frame(1123, 1, [Actor(2, 0x3D2F, wind + Vector3.UnitZ * 3, dead: true, targetable: false, eventState: 7)], party: party),
            Frame(1123, 2, [], removed: [2], party: party),
            Frame(1123, 3, [], party: party),
        ]);
        controller.Seek(.5);
        Check(dangers() == 0 && Obstacles() == 1 && Find("treasure/wind/2").Length == 1, "Wind was filtered or became ordinary danger.");
        controller.Seek(1.1);
        Check((Vector3)Value(Value(Find("treasure/wind/2").Single(), "Geometry"), "Position") == wind + Vector3.UnitZ * 3,
            "Wind preview did not move.");
        controller.Seek(2.1);
        Check(Obstacles() == 0 && Find("treasure/wind/2").Length == 0, "Removed wind retained obstacle or preview.");
        controller.Seek(.5);
        Check(Obstacles() == 1, "Wind reverse Seek lost hard obstacle.");
        stop();
        Check(Obstacles() == 0, "Wind owner stop leaked hard obstacle.");
        Console.WriteLine("PASS native treasure production Satyros unfiltered moving hard obstacle, preview, removal and Seek");

        var target = c + Vector3.UnitX * 9;
        var moved = c + Vector3.UnitZ * 12;
        var waters = Cast(32229, 3, c, target: 101);
        load(id,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D30, c, combat: true), Actor(100, 0, c), Actor(101, 0, target)], full: true, party: party),
            Frame(1123, .1, [Actor(1, 0x3D30, c, combat: true, cast: waters)], party: party),
            Frame(1123, 1, [Actor(101, 0, moved)], party: party),
            Frame(1123, 2, [Actor(1, 0x3D30, c, combat: true)], party: party),
            TreasureNativeEffect(3, 1, 32229, 1, 0, [], target),
            EffectEvent(3, 1, 32229, 1, target: 100),
            Frame(1123, 6, [Actor(2, 0x3D32, target, dead: true, targetable: false, eventState: 7)], party: party),
            Frame(1123, 7, [], removed: [2], party: party),
            Frame(1123, 8, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(dangers() == 1 && (Vector3)Value(Value(Find("trial/voidzone/cast/1").Single(), "Geometry"), "Position") == moved,
            "Triton cast prediction failed to follow current target.");
        controller.Seek(2.1);
        Check(dangers() == 0, "Cancelled water invented an effect.");
        controller.Seek(5d);
        Check(dangers() == 1 && Find("trial/voidzone/event/").Length == 1
            && (Vector3)Value(Value(Find("trial/voidzone/event/").Single(), "Geometry"), "Position") == target,
            "Triton native fallback position failed or PR target expanded into extra prediction.");
        var window = ((IEnumerable)Value(Areas().Single(), "DangerWindows")).Cast<object>().Single();
        Check((DateTime)Value(window, "DangerFrom") == Start.AddSeconds(3).AddSeconds((double)1.4f),
            "Triton event-to-spawn clock differs.");
        controller.Seek(6.1);
        Check(dangers() == 1 && Find("trial/voidzone/event/").Length == 0 && Find("trial/voidzone/live/2").Length == 1,
            "Unfiltered bubble failed event/live handoff.");
        controller.Seek(7.1);
        Check(dangers() == 0, "Destroyed bubble retained navigation.");
        controller.Seek(5d);
        Check(dangers() == 1 && Find("trial/voidzone/event/").Length == 1, "Triton reverse Seek lost effect prediction.");
        stop();
        Console.WriteLine("PASS native treasure production Triton cast/effect/live R5 handoff, native fallback position, exact delay and Seek");
    }

    private static void RunTreasureTritonSampling(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        var c = new Vector3(100, 0, 100);
        var current = c + Vector3.UnitX * 10;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (territory, oid, action, live) in new[]
        {
            (1123u, 0x3D30u, 32229u, 0x3D32u), (794u, 0x2539u, 13396u, 0x1E8D9Cu),
            (794u, 0x253Au, 13219u, 0x1EA140u), (794u, 0x2534u, 13452u, 0x1EA8BBu),
        })
        {
        load(territory == 1123 ? "masterplayer.treasure.shifting-gymnasion" : "masterplayer.treasure.shifting-altars",
        [
            Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(100, 0, c), Actor(101, 0, c)], full: true, party: party),
            Frame(territory, 1, [Actor(101, 0, current), Actor(2, live, current)], party: party),
            Frame(territory, 2, [], party: party),
        ]);
        controller.Seek(.5);
        var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        var encounter = script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        var assembly = script.GetType().Assembly;
        var scanned = false;
        Action scan = () =>
        {
            scanned = true;
            var actors = (IDictionary)encounter.GetType().GetField("_actors", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(encounter)!;
            actors[101ul]!.GetType().GetProperty("Position")!.SetValue(actors[101ul], current);
        };
        var adapterType = assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonEventAdapter")!;
        var adapter = Activator.CreateInstance(adapterType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, [encounter, scan, (Func<LogSystemCastStartEvent, float?>)(_ => null),
                (Action<string>)(error => throw new InvalidOperationException(error))], null)!;
        var effect = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            SourceId = 1ul, ActionId = action, GlobalSequence = 55u, MainTargetId = 101ul, Targets = new ulong[] { 100, 101 },
            TargetPosition = Pose(c - Vector3.UnitX * 20), Timestamp = Start.AddSeconds(.5),
        }), assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent")!)!;
        adapterType.GetMethod("Handle")!.Invoke(adapter, [effect, Start.AddSeconds(.5)]);
        var eventAoe = ((IEnumerable)encounter.GetType().GetMethod("AmbientAoes")!.Invoke(encounter, null)!).Cast<object>()
            .Single(a => ((string)Value(a, "Id")).StartsWith("trial/voidzone/event/"));
        Check(scanned && (Vector3)Value(eventAoe, "Position") == current,
            "Native Triton event locked stale actor position before current scan.");
        controller.Seek(1.1);
        Check(dangers() == 1 && !previews().Any(p => ((string)Value(p, "Id")).Contains("trial/voidzone/event/")),
            "Current-position event prediction failed live bubble handoff.");
        stop();
        Console.WriteLine("PASS native treasure production voidzone native adapter scans current main target before locking effect prediction " + action);
        }
    }

    private static void RunTreasureSphinx(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string id) => previews().Where(p => ((string)Value(p, "Id")).Contains(id)).ToArray();
        object[] Areas() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        var c = new Vector3(100, 0, 100);
        var target = c + Vector3.UnitX * 9;
        var moved = c + Vector3.UnitZ * 12;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        const string id = "masterplayer.treasure.shifting-gymnasion";
        var spread1 = Cast(32271, 3, c, target: 101);
        var spread2 = Cast(32271, 6, c, target: 101);
        load(id,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D3B, c, combat: true), Actor(100, 0, c), Actor(101, 0, target),
                Actor(2, 0x233C, c), Actor(3, 0x233C, c)], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: spread1), Actor(3, 0x233C, c, cast: spread2)], party: party),
            EffectEvent(.5, 2, 32271, 1, target: 101),
            Frame(1123, 1, [Actor(101, 0, moved), Actor(3, 0x233C, c)], party: party),
            Frame(1123, 2, [Actor(101, 0, moved, dead: true)], party: party),
            Frame(1123, 3, [Actor(101, 0, target)], party: party),
            Frame(1123, 4, [Actor(2, 0x233C, c)], party: party),
            Frame(1123, 5, [], party: party),
        ]);
        controller.Seek(.6);
        Check(dangers() == 2 && Find("quest/spread/").Length == 2
            && Find("quest/spread/").All(p => (bool)Value(p, "OutlineOnly")
                && (float)Value(Value(p, "Geometry"), "Radius") == 6),
            "Sphinx effect resolved spread or display radius differs.");
        controller.Seek(1.1);
        var window = ((IEnumerable)Value(Areas().Single(), "DangerWindows")).Cast<object>().Single();
        Check(dangers() == 1 && (DateTime)Value(window, "DangerFrom") == Start.AddSeconds(.1).AddSeconds(6 + .3),
            $"Sphinx target FIFO activation differs: {Value(window, "DangerFrom"):O}.");
        Check(Vector3.Distance((Vector3)Value(Value(Find("quest/spread/").Single(), "Geometry"), "Position"), moved) < .05f,
            "Sphinx spread failed current target movement.");
        Check(!((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Any(),
            "Sphinx spread invented predicted damage.");
        controller.Seek(2.1);
        Check(dangers() == 0 && Find("quest/spread/").Length == 0, "Dead spread target still active.");
        controller.Seek(3.1);
        Check(dangers() == 1, "Revived target failed to restore retained mark.");
        controller.Seek(4.1);
        Check(dangers() == 0, "Final spread finish retained navigation.");
        controller.Seek(1.1);
        Check(dangers() == 1, "Sphinx reverse Seek lost target FIFO.");
        stop();
        Console.WriteLine("PASS native treasure production Sphinx target FIFO, R6 display/R7 navigation, death/revive, effect retention and Seek");
    }

    private static void RunTreasureBull(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object[] MechanicPreviews() => previews().Where(p => !((string)Value(p, "Id")).Contains("treasure/enemy/")).ToArray();
        var center = new Vector3(0, 5, -372);
        const uint territory = 1209;
        const string id = "masterplayer.treasure.cenote-ja-ja-gural";
        var mainCast = Cast(38249, 4, center, .3f);
        var helperCast = Cast(38250, 11, center + Vector3.UnitX * 3, .7f);
        var crossCast = Cast(38254, 11, center, 1.1f);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4305, center, combat: true, objectIndex: 2, isEnemy: true),
                Actor(2, 0x4300, new(1, 5, -350), objectIndex: 4, isEnemy: true),
                Actor(5, 0x42FE, new(0, 5, -350), objectIndex: 10, isEnemy: true),
                Actor(3, 0x233C, center), Actor(4, 0x233C, center), Actor(100, 0, center)], full: true),
            Frame(territory, .1, [Actor(1, 0x4305, center, combat: true, cast: mainCast),
                Actor(3, 0x233C, center, cast: helperCast), Actor(4, 0x233C, center, cast: crossCast)]),
            Frame(territory, 2, [Actor(1, 0x4305, center + Vector3.UnitX * 10, combat: true, cast: mainCast)]),
            Frame(territory, 5, [Actor(1, 0x4305, center, combat: true)]),
            Frame(territory, 12, []),
            Frame(territory, 13, [Actor(3, 0x233C, center), Actor(4, 0x233C, center)]),
            Frame(territory, 15, [Actor(1, 0x4305, center, dead: true),
                Actor(2, 0x4300, center, cast: Cast(38279, 3, center, startedAt: 15))]),
            Frame(territory, 20, [Actor(2, 0x4300, center, dead: true), Actor(5, 0x42FE, center, dead: true)]),
            Frame(territory, 21, []),
        ]);
        controller.Seek(.5);
        Check(dangers() == 3 && MechanicPreviews().Length == 3, "Bull independent group casts missing navigation/dual view.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        var runtimeAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        dynamic oracle = runtimeAssembly.GetType("BossMod.Dawntrail.TreasureHunt.CenoteJaJaGural.SharedBoundsBoss")!
            .GetField("ArenaBounds", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var dirType = runtimeAssembly.GetType("BossMod.WDir")!;
        var sourceContains = ((object)oracle).GetType().GetMethod("Contains")!;
        var sourceClamp = ((object)oracle).GetType().GetMethod("ClampToBounds")!;
        var sourceCenter = new Vector3((float)oracle.Center.X, 5, (float)oracle.Center.Z);
        Check(Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f, "Bull source AABB center differs.");
        var compared = 0;
        for (var x = -22; x <= 22; ++x)
            for (var z = -394; z <= -350; ++z)
            {
                var point = new Vector3(x + .137f, 5, z + .173f);
                var offset = Activator.CreateInstance(dirType, [point.X - sourceCenter.X, point.Z - sourceCenter.Z])!;
                Check((bool)boundary.Contains(point, 0f) == (bool)sourceContains.Invoke((object)oracle, [offset])!,
                    $"Bull source boundary mismatch at {point}.");
                ++compared;
            }
        var marker = previews().Single(p => ((string)Value(p, "Id")).EndsWith("treasure/enemy/2"));
        var markerPosition = (Vector3)Value(Value(marker, "Geometry"), "Position");
        var markerOffset = Activator.CreateInstance(dirType, [1f - sourceCenter.X, -350f - sourceCenter.Z])!;
        dynamic clamped = sourceClamp.Invoke((object)oracle, [markerOffset])!;
        Check(Vector3.Distance(markerPosition, sourceCenter + new Vector3((float)clamped.X, 0, (float)clamped.Z)) < .002f
            && (bool)Value(marker, "OutlineOnly"), "Bull marker clamp differs from source.");
        var axis = previews().Single(p => ((string)Value(p, "Id")).EndsWith("treasure/enemy/5"));
        Check((Vector3)Value(Value(axis, "Geometry"), "Position") == new Vector3(0, 5, -350),
            "Bull center-axis marker exception lost.");
        controller.Seek(2.1);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(dangers() == 3 && Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f,
            "Moving Bull changed fixed arena.");
        controller.Seek(6d);
        Check(dangers() == 2 && MechanicPreviews().Length == 2, "Bull primary finish cleared 11s helpers.");
        controller.Seek(12.1);
        Check(dangers() == 2, "Bull expected time expired active helpers.");
        controller.Seek(13.1);
        Check(dangers() == 0, "Bull observed helper finish leaked.");
        controller.Seek(15.5);
        Check(dangers() == 1, "Bull primary death ended reward phase.");
        manager.SetMechanismEnabled(id, "Bull Apollyon", false);
        controller.Seek(15.6);
        Check(dangers() == 0 && MechanicPreviews().Length == 0, "Bull disabled mechanism leaked output.");
        manager.SetMechanismEnabled(id, "Bull Apollyon", true);
        controller.Seek(.5);
        Check(dangers() == 3, "Bull reverse Seek failed to rebuild.");
        controller.Seek(20.5);
        Check(dangers() == 0 && previews().Length == 0, "Bull helper prolonged completed All group.");
        stop();
        Console.WriteLine($"PASS native treasure production Bull {compared} source-arena points/clamp/independent clocks/dual view/Seek/cleanup");
    }

    private static void RunTreasureRoster(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object[] Damage()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            var encounter = script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
            return ((IEnumerable)Value(encounter, "PredictedDamage")).Cast<object>().ToArray();
        }
        var center = new Vector3(100, 0, 100);
        const uint territory = 924;
        var party = Enumerable.Range(101, 8).Append(100)
            .Select((id, i) => (object)new { Slot = 8 - i, EntityId = (ulong)id }).ToArray();
        load("masterplayer.treasure.shifting-oubliettes",
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3011, center, combat: true),
                Actor(2, 0x233C, center),
                .. Enumerable.Range(100, 9).Select(i => Actor((ulong)i, 0, center))], full: true, party: party),
            Frame(territory, .1, [Actor(2, 0x233C, center, cast: Cast(21662, 4, center))], party: party),
            Frame(territory, .5, [Actor(103, 0, center, dead: true)], party: party),
            Frame(territory, 1, [], party: party),
        ]);
        controller.Seek(.3);
        var damage = Damage();
        Check(damage.Length == 8 && damage.Select(p => (ulong)Value(p, "TargetId")).SequenceEqual(Enumerable.Range(100, 8).Select(i => (ulong)i))
            && damage.Select(p => (int)Value(p, "PartySlot")).SequenceEqual(Enumerable.Range(0, 8)) && dangers() == 0,
            "Source roster must put local first, retain input order, ignore PR slots, truncate ninth and avoid duplicate raidwide.");
        controller.Seek(.6);
        damage = Damage();
        Check(damage.Length == 7 && damage.All(p => (ulong)Value(p, "TargetId") != 103)
            && damage.Select(p => (int)Value(p, "PartySlot")).SequenceEqual(new[] { 0, 1, 2, 4, 5, 6, 7 }),
            "Dead source member must retain a slot hole without admitting ninth member.");
        controller.Seek(.3);
        Check(Damage().Length == 8, "Reverse Seek failed to restore source roster/dead filtering.");
        stop();
        Console.WriteLine("PASS native treasure production source roster local-first/eight-slot truncation/death holes/Seek/cleanup");
    }

    private static void RunTreasureOubliettes(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Casts() => ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Select(p => Value(p, "Value")).ToArray();
        var center = new Vector3(100, 0, 100);
        const uint territory = 924;
        const string id = "masterplayer.treasure.shifting-oubliettes";
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3018, center, combat: true),
                Actor(2, 0x304C, center, targetable: false), Actor(100, 0, center)], full: true),
            Frame(territory, .1, [Actor(1, 0x3018, center, combat: true, cast: Cast(21685, 5, center)),
                Actor(2, 0x304C, center, targetable: false, cast: Cast(22780, 7, center + Vector3.UnitX * 5, MathF.PI))]),
            Frame(territory, 1, [Actor(2, 0x304C, center + Vector3.UnitX * 12, targetable: false,
                cast: Cast(22780, 7, center + Vector3.UnitX * 5, MathF.PI))]),
            Frame(territory, 5.5, [Actor(1, 0x3018, center, combat: true)]),
            Frame(territory, 9, [], removed: [2]),
            Frame(territory, 10, []),
        ]);
        controller.Seek(1.1);
        Check(dangers() == 2 && Casts().Length == 2, "Pixie clone requires targetability or primary prerequisite.");
        controller.Seek(6d);
        var clone = Casts().Single();
        Check(dangers() == 1 && (ulong)Value(clone, "Source") == 2
            && (Vector3)Value(clone, "Location") == center + Vector3.UnitX * 5
            && (float)Value(clone, "Rotation") == MathF.PI
            && (DateTime)Value(clone, "FinishAt") == Start.AddSeconds(.1).AddSeconds(7 + .3),
            "Pixie clone lost independent clock or locked cast transform.");
        controller.Seek(8d);
        Check(dangers() == 1, "Expected clock expired clone without real finish.");
        controller.Seek(9.1);
        Check(dangers() == 0, "Destroyed Pixie clone output leaked.");
        controller.Seek(1.1);
        Check(dangers() == 2, "Pixie reverse Seek did not rebuild both casts.");
        stop();
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x302B, center, combat: true),
                Actor(2, 0x301D, center), Actor(100, 0, center)], full: true),
            Frame(territory, .1, [Actor(2, 0x301D, center, cast: Cast(6448, 3.5, center))]),
            CastEvent(.1, 2, 6448, 3.5f, center),
            Frame(territory, 1, [Actor(1, 0x302B, center, combat: true, cast: Cast(21719, 4.5, center))]),
            Frame(territory, 2, []),
        ]);
        controller.Seek(.5);
        Check(dangers() == 0 && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 6448 }),
            "Swallow claimed defined but unactivated MandragoraAOEs.");
        controller.Seek(1.1);
        var cast = Casts().Single();
        var shape = Value(Value(cast, "Rule"), "Shape");
        dynamic geometry = shape.GetType().GetMethod("Resolve")!.Invoke(shape, [center, 0f, null])!;
        Check(dangers() == 1 && (bool)geometry.Contains(center, 0f) && !(bool)geometry.Contains(center + Vector3.UnitX * 12, 0f),
            "Swallow 21719 became donut due to swapped source class names.");
        stop();
        Console.WriteLine("PASS native treasure production Oubliettes clone clocks/inactive ownership/Swallow shapes/Seek/cleanup");
    }

    private static void RunTreasureLights(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object[] Lights() => previews().Where(p => ((string)Value(p, "Id")).Contains("treasure/light/")).ToArray();
        string[] Hints(Vector3 point)
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            var encounter = script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
            return ((IEnumerable)encounter.GetType().GetMethod("Hints")!.Invoke(encounter, [point])!).Cast<string>().ToArray();
        }
        ulong Source(object preview) => ulong.Parse(((string)Value(preview, "Id")).Split('/')[^1]);
        var center = new Vector3(100, 0, 100);
        const uint territory = 1123;
        const string id = "masterplayer.treasure.shifting-gymnasion";
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3D40, center, combat: true),
                Actor(100, 0, center),
                .. Enumerable.Range(10, 6).Select(i => Actor((ulong)i, 0x233C, center))], full: true, party: party),
            Frame(territory, .1, Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C,
                center, cast: Cast(32294, 19 - i, center + Vector3.UnitX * (i - 10), i))), party: party),
            Frame(territory, 1, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 2, [Actor(15, 0x233C, center,
                cast: Cast(32294, 15, center, startedAt: 2))], party: party),
            Frame(territory, 3, [], removed: [11], party: party),
            Frame(territory, 4, [Actor(12, 0x233C, center), Actor(13, 0x233C, center)], party: party),
            Frame(territory, 5, [Actor(11, 0x233C, center,
                cast: Cast(32294, 15, center, startedAt: 5))], party: party),
            Frame(territory, 12, [], party: party),
            Frame(territory, 20, [Actor(14, 0x233C, center), Actor(15, 0x233C, center), Actor(11, 0x233C, center)], party: party),
        ]);
        controller.Seek(.5);
        Check(dangers() == 4 && Lights().Select(Source).Order().SequenceEqual(new ulong[] { 10, 11, 12, 13 }),
            "Lampas must output first four inserted casters, not earliest finishes.");
        Check(Lights().Where(p => Source(p) is 10 or 11).All(p => (Vector4?)Value(p, "Color") == new Vector4(1, 1, 0, 1))
            && Lights().Where(p => Source(p) is 12 or 13).All(p => Value(p, "Color") == null), "Lampas first two danger colors differ.");
        controller.Seek(1.1);
        Check(dangers() == 4 && Lights().Select(Source).Order().SequenceEqual(new ulong[] { 11, 12, 13, 14 }),
            $"Lampas hidden tail did not promote after cancellation: dangers={dangers()}, sources={string.Join(',', Lights().Select(Source))}.");
        var areas = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        Check(Lights().Where(p => Source(p) is 11 or 12).All(p => (Vector4?)Value(p, "Color") == new Vector4(1, 1, 0, 1))
            && Lights().Where(p => Source(p) is 13 or 14).All(p => Value(p, "Color") == null),
            "Promoted light failed to preserve colors by mechanism order.");
        Check(areas.Length == 4 && areas.Any(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>()
                .Any(w => (DateTime)Value(w, "DangerFrom") == Start.AddSeconds(.1).AddSeconds(5 + .3))),
            "Promoted Lampas light lost original activation.");
        controller.Seek(2.1);
        Check(Lights().Select(Source).Order().SequenceEqual(new ulong[] { 11, 12, 13, 14 }),
            "Newest light reused a dictionary hole before older tail.");
        controller.Seek(3.1);
        Check(dangers() == 4 && Lights().Select(Source).Order().SequenceEqual(new ulong[] { 12, 13, 14, 15 }),
            "Destroyed helper did not promote next light.");
        controller.Seek(4.1);
        Check(dangers() == 2 && Lights().All(p => Value(p, "Color") == null),
            "Total count two must restore default AOE color.");
        controller.Seek(5.1);
        Check(dangers() == 3 && Lights().Where(p => Source(p) is 14 or 15)
                .All(p => (Vector4?)Value(p, "Color") == new Vector4(1, 1, 0, 1))
            && Value(Lights().Single(p => Source(p) == 11), "Color") == null,
            "Recreated source must append at new generation tail.");
        manager.SetMechanismEnabled(id, "Lampas Chrysine", false);
        controller.Seek(4.2);
        Check(dangers() == 0 && Lights().Length == 0, "Lampas disabled output leaked.");
        manager.SetMechanismEnabled(id, "Lampas Chrysine", true);
        controller.Seek(.5);
        Check(dangers() == 4 && Lights().Select(Source).Order().SequenceEqual(new ulong[] { 10, 11, 12, 13 }),
            "Lampas reverse Seek failed to rebuild insertion order.");
        controller.Seek(12d);
        Check(dangers() == 3 && Lights().Length == 3, "Predicted time expired observed lights.");
        controller.Seek(20.1);
        Check(dangers() == 0 && Lights().Length == 0, "Lampas observed endings did not clear navigation/dual view.");
        stop();
        var point = center + Vector3.UnitZ * 10;
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3D40, center, combat: true), Actor(100, 0, point),
                .. Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C, center))], full: true, party: party),
            Frame(territory, .1, Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C, center,
                cast: Cast(32294, 3, center + Vector3.UnitX * (i - 14) * 100))), party: party),
            Frame(territory, 1, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 2, [], party: party),
        ]);
        controller.Seek(.5);
        Check(!Hints(point).Contains("离开范围！"), "Hidden fifth light emitted a danger hint.");
        controller.Seek(1.1);
        Check(Hints(point).Contains("离开范围！"), "Promoted light lost danger hint.");
        stop();
        Console.WriteLine("PASS native treasure production Lampas insertion FIFO/hidden promotion/danger colors/Seek/cleanup");
    }

    private static void RunTreasureDelay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] MechanicPreviews() => previews().Where(p => !((string)Value(p, "Id")).Contains("treasure/enemy/")).ToArray();
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (oid, bonus, action) in new[] { (0x2541u, 0x2544u, 13364u), (0x2542u, 0x2546u, 13367u),
            (0x2535u, 0x2545u, 13311u), (0x253Bu, 0x2544u, 13342u), (0x253Fu, 0x2544u, 13385u) })
        {
            const string id = "masterplayer.treasure.shifting-altars";
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, center, combat: true),
                    Actor(2, bonus, center), Actor(3, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX)],
                    full: true, party: party),
                CastEvent(.1, 1, action, 3, center, target: 100),
                CastEvent(.2, 3, action, 4, center, target: 100),
                Frame(794, 1, [], removed: [1, 3], party: party),
                EffectEvent(2, 2, action, 90, target: 100),
                TreasureNativeEffect(3, 2, action, 1, 101, [100]),
                TreasureNativeEffect(4, 2, action, 2, 100, [101]),
                TreasureNativeEffect(4.1, 2, action, 2, 100, [101]),
                TreasureNativeEffect(5, 99, action, 3, 100, []),
                Frame(794, 6, [Actor(2, bonus, center, dead: true)], party: party),
            ]);
            controller.Seek(2.5);
            Check(Damage().Length == 2 && dangers() == 0 && MechanicPreviews().Length == 0,
                "Delayable damage did not survive cancel/destruction or consumed expanded PR target.");
            controller.Seek(3.5);
            Check(Damage().Length == 2, "Victim array was mistaken for MainTargetId.");
            controller.Seek(4.5);
            var remaining = Damage().Single();
            var activation = (DateTime)Value(remaining, "Activation");
            var expectedActivation = Start.AddSeconds(.2).AddSeconds(4 + .3);
            Check(activation == expectedActivation,
                $"Delayable target FIFO or duplicate packet consumption differs: {activation:O}, expected {expectedActivation:O}.");
            controller.Seek(5.5);
            Check(Damage().Length == 0, "Different-source MainTargetId did not clear delayable record.");
            controller.Seek(2.5);
            Check(Damage().Length == 2, "Reverse Seek lost delayed queue.");
            stop();
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, center, combat: true),
                    Actor(2, bonus, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX)],
                    full: true, party: party),
                Frame(794, .1, [Actor(1, oid, center, combat: true, cast: Cast(action, 3, center, target: 100))], party: party),
                Frame(794, .15, [Actor(1, oid, center, combat: true, cast: Cast(action, 3, center, target: 101))], party: party),
                JsonSerializer.Serialize(new { t = .16, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                    data = new { Timestamp = Start.AddSeconds(.16), SourceId = 1ul, ActionId = action } }),
                Frame(794, .2, [Actor(1, oid, center, combat: true,
                    cast: Cast(action, 3, center, target: 100, startedAt: .2))], party: party),
                TreasureNativeEffect(.4, 99, action, 10, 100, []),
                TreasureNativeEffect(.5, 99, action, 11, 101, []),
                TreasureNativeEffect(.6, 99, action, 12, 100, []),
                JsonSerializer.Serialize(new { t = .7, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                    packetFacing = 0f, data = new { Timestamp = Start.AddSeconds(.2), SourceId = 1ul, TargetId = 100ul,
                        ActionId = action, CastTime = 3f, EffectPosition = Pose(center + Vector3.UnitX) } }),
                Frame(794, 2, [Actor(1, oid, center, combat: true)], party: party),
            ]);
            controller.Seek(.3);
            Check(Damage().Select(p => (ulong)Value(p, "TargetId")).SequenceEqual([100ul, 101ul, 100ul]),
                "Raw target handoff or fast recast lost an earlier source generation.");
            controller.Seek(.45);
            Check(Damage().Select(p => (ulong)Value(p, "TargetId")).SequenceEqual([101ul, 100ul]),
                "Matching main target did not consume first generation.");
            controller.Seek(.55);
            Check(Damage().Single() is { } last && (ulong)Value(last, "TargetId") == 100, "Old handoff hit removed new recast.");
            controller.Seek(.8);
            Check(Damage().Length == 0, "Same-generation packet correction resurrected a resolved prediction.");
            stop();
            object RawDuration(float total, ulong target) => new { ActionId = action, TargetId = target,
                Location = Pose(center), Rotation = 0f, StartedAt = Start.AddSeconds(.1),
                FinishAt = Start.AddSeconds(.1 + total + .3), TotalTime = total };
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, center, combat: true),
                    Actor(2, bonus, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX)],
                    full: true, party: party),
                Frame(794, .1, [Actor(1, oid, center, combat: true, cast: RawDuration(3, 100))], party: party),
                Frame(794, .2, [Actor(1, oid, center, combat: true, cast: RawDuration(4, 100))], party: party),
                Frame(794, .3, [Actor(1, oid, center, combat: true, cast: RawDuration(4, 101))], party: party),
                Frame(794, .4, [], party: [new { Slot = 1, EntityId = 100ul }, new { Slot = 0, EntityId = 101ul }]),
                Frame(794, 2, [Actor(1, oid, center, combat: true)], party: party),
            ]);
            controller.Seek(.5);
            Check(Damage().Select(p => (ulong)Value(p, "TargetId")).SequenceEqual([100ul, 100ul, 101ul])
                && Damage().Select(p => (DateTime)Value(p, "Activation")).SequenceEqual(
                    [Start.AddSeconds(.1 + 3 + .3), Start.AddSeconds(.1 + 4 + .3), Start.AddSeconds(.1 + 4 + .3)]),
                "Large raw duration correction left a tombstone or PR roles replaced source slots.");
            stop();
            Console.WriteLine("PASS native treasure production delayable " + action + " MainTarget/FIFO/raw handoff/fast recast/receipts/Seek/cleanup");
        }
    }

    private static string TreasureNativeEffect(double time, ulong source, uint action, uint sequence, ulong main, ulong[] targets,
        Vector3? targetPosition = null)
        => JsonSerializer.Serialize(new { t = time, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { Timestamp = Start.AddSeconds(time), SourceId = source, ActionId = action, GlobalSequence = sequence,
                MainTargetId = main, Targets = targets, TargetPosition = targetPosition is { } p ? Pose(p) : null } });
}
