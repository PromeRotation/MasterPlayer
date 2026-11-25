using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureCanalsLucky(object managerObject, object runtimeObject, object controllerObject,
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
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        const string lucky = "masterplayer.treasure.excitatron";
        const string hidden = "masterplayer.treasure.hidden-canals";
        const string lost = "masterplayer.treasure.lost-canals";
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var sourceDir = sourceAssembly.GetType("BossMod.WDir")!;
        foreach (var (guid, territory, oid, action, z, sourceType) in new[]
        {
            (lucky, 1000u, 0x377Fu, 27990u, -460f, "BossMod.Endwalker.TreasureHunt.Excitatron6000.LuckyFace.LuckyFace"),
            (hidden, 725u, 0x1FCCu, 9894u, -420f, "BossMod.Stormblood.TreasureHunt.FinalRoomArena"),
            (lost, 712u, 0x1F15u, 4486u, -420f, "BossMod.Stormblood.TreasureHunt.FinalRoomArena"),
        })
        {
            var c = new Vector3(0, 5, z);
            var cast = Cast(action, 3, c);
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(2, 0x233C, c),
                    Actor(100, 0, c)], full: true, party: party),
                Frame(territory, .1, [Actor(2, 0x233C, c, targetable: false, cast: cast)], party: party),
                Frame(territory, 1, [Actor(1, oid, c + Vector3.UnitX * 10, combat: true)], party: party),
                Frame(territory, 5, [], party: party),
                Frame(territory, 6, [Actor(2, 0x233C, c)], party: party),
                Frame(territory, 7, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == 1 && previews().Count(p => !((string)Value(p, "Id")).Contains("treasure/enemy/")) == 1,
                "Canals/Lucky helper geometry not published to previews and navigation.");
            var inactive = territory switch
            {
                1000 => new ushort[] { 27988, 27989, 27991, 27993, 27995, 27997, 27999, 28090, 28091, 28045, 27983 },
                725 => new ushort[] { 618, 8727, 8629, 8660, 967, 966, 969, 968, 971 },
                _ => new ushort[] { 9556, 967 },
            };
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && inactive.All(a => !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = a })),
                "Inactive/fake abilities or player source incorrectly claimed by native script.");
            dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            dynamic oracle = sourceAssembly.GetType(sourceType)!.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var sourceCenter = new Vector3((float)oracle.Center.X, 5, (float)oracle.Center.Z);
            var contains = ((object)oracle).GetType().GetMethod("Contains")!;
            Check(Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f, "Canals/Lucky final AABB center differs.");
            var compared = 0;
            for (var x = -29; x <= 29; ++x)
                for (var dz = -29; dz <= 24; ++dz)
                {
                    var point = new Vector3(x + .137f, 5, z + dz + .173f);
                    var offset = Activator.CreateInstance(sourceDir, [point.X - sourceCenter.X, point.Z - sourceCenter.Z])!;
                    Check((bool)boundary.Contains(point, 0f) == (bool)contains.Invoke((object)oracle, [offset])!,
                        $"Canals/Lucky source arena mismatch {territory} at {point}.");
                    ++compared;
                }
            controller.Seek(5.1);
            boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check(dangers() == 1 && Vector3.Distance((Vector3)boundary.Position, sourceCenter) < .002f,
                "Movement shifted arena or predicted expiry cleared actual cast.");
            controller.Seek(6.1);
            Check(dangers() == 0, "Observed Finish retained ordinary cast.");
            controller.Seek(.2);
            Check(dangers() == 1, "Reverse Seek failed ordinary cast.");
            stop();
            Console.WriteLine($"PASS native treasure production {territory} arena source oracle {compared} points observed casts helper and reverse Seek");
        }

        foreach (var (guid, territory, oid, cleaver) in new[] { (hidden, 725u, 0x1FCCu, 0x2064u), (lost, 712u, 0x1F15u, 0x1EBFu) })
        {
            var c = new Vector3(0, 0, -420);
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(2, cleaver, c, targetId: 101),
                    Actor(3, 0x2545, c, targetId: 101), Actor(4, oid, c), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 4)],
                    full: true, party: party),
                Frame(territory, .1, [Actor(2, cleaver, c, targetId: 101, cast: Cast(5352, 3, c))], party: party),
                Frame(territory, 1, [Actor(101, 0, c + Vector3.UnitX * 4)], party: party),
                Frame(territory, 2, [Actor(2, cleaver, c, targetId: 100), Actor(100, 0, c + Vector3.UnitZ * 4)], party: party),
                Frame(territory, 3, [Actor(2, cleaver, c, dead: true)], party: party),
                Frame(territory, 4, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Find("treasure/cleave/").Length == 1 && dangers() == 2
                && Find("treasure/enemy/").Length == (territory == 725 ? 3 : 2),
                "Canal source OID, all-primary markers or casting cleave differs.");
            var rotation = (float)Value(Value(Find("treasure/cleave/").Single(), "Geometry"), "Rotation");
            controller.Seek(1.1);
            var changed = (float)Value(Value(Find("treasure/cleave/").Single(), "Geometry"), "Rotation");
            Check(MathF.Abs(changed - rotation) > 1, "Cleave did not track target position.");
            controller.Seek(2.1);
            Check(dangers() == 1 && (bool)Value(Find("treasure/cleave/").Single(), "OutlineOnly"),
                "Self target did not switch to source-party bait navigation.");
            controller.Seek(3.1);
            Check(dangers() == 0 && Find("treasure/cleave/").Length == 0, "Dead cleaver retained.");
            controller.Seek(.2);
            Check(dangers() == 2, "Reverse Seek lost casting cleave.");
            stop();
            Console.WriteLine($"PASS native treasure production {territory} exact cleave source target tracking casting and owner cleanup");
        }

        var center = new Vector3(0, 0, -420);
        var eye = center + Vector3.UnitZ * 65;
        var gazeCast = Cast(9557, 4, eye);
        load(lost,
        [
            Header(712), Frame(712, 0, [Actor(1, 0x1F15, eye, combat: true), Actor(100, 0, center)], full: true, party: party),
            Frame(712, .1, [Actor(1, 0x1F15, eye, combat: true, cast: gazeCast)], party: party),
            Frame(712, 1, [Actor(1, 0x1F15, center, combat: true, cast: gazeCast)], party: party),
            Frame(712, 5, [Actor(1, 0x1F15, center, combat: true)], party: party),
            Frame(712, 6, [], party: party),
        ]);
        controller.Seek(4d);
        Check(dangers() == 0 && Find("treasure/gaze/").Length == 3
            && (Vector3)Value(Value(Find("treasure/gaze/1/eye").Single(), "Geometry"), "Position") == eye
            && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [center, 0f]) is float,
            "Icebeast default 10000 gaze source lock drawing or PR facing intent differs.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Icebeast replay executed PR facing.");
        controller.Seek(5.1);
        Check(Find("treasure/gaze/").Length == 0, "Gaze real end retained facing/drawing.");
        controller.Seek(4d);
        Check(Find("treasure/gaze/").Length == 3, "Reverse Seek lost gaze.");
        stop();
        Console.WriteLine("PASS native treasure production Icebeast locked default-range gaze PR facing intent dual views and replay isolation");

        var luckyCenter = new Vector3(0, 0, -460);
        var spread = Cast(28002, 5, luckyCenter, target: 101);
        load(lucky,
        [
            Header(1000), Frame(1000, 0, [Actor(1, 0x377F, luckyCenter, combat: true), Actor(2, 0x233C, luckyCenter),
                Actor(100, 0, luckyCenter), Actor(101, 0, luckyCenter), Actor(102, 0, luckyCenter + Vector3.UnitX * 10)],
                full: true, party: party),
            Frame(1000, .1, [Actor(2, 0x233C, luckyCenter, cast: spread)], party: party),
            Frame(1000, 1, [Actor(101, 0, luckyCenter + Vector3.UnitX * 2)], party: party),
            Frame(1000, 2, [Actor(101, 0, luckyCenter, dead: true)], party: party),
            Frame(1000, 3, [Actor(101, 0, luckyCenter + Vector3.UnitX * 2)], party: party),
            Frame(1000, 4, [], removed: [101], party: party),
            Frame(1000, 5, [Actor(101, 0, luckyCenter + Vector3.UnitX * 15)], party: party),
            Frame(1000, 7, [Actor(2, 0x233C, luckyCenter)], party: party),
            Frame(1000, 8, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("quest/spread/").Length == 1 && dangers() == 1, "Lucky Face spread output missing.");
        controller.Seek(1.1);
        var frozen = (Vector3)Value(Value(Find("quest/spread/").Single(), "Geometry"), "Position");
        controller.Seek(2.1);
        Check(Find("quest/spread/").Length == 0 && dangers() == 0, "Dead spread target retained.");
        controller.Seek(3.1);
        Check(Find("quest/spread/").Length == 1, "Spread revival failed.");
        controller.Seek(6.1);
        Check((Vector3)Value(Value(Find("quest/spread/").Single(), "Geometry"), "Position") == frozen && dangers() == 1,
            "Same-ID recreation rebound old spread or expiry ended it.");
        controller.Seek(7.1);
        Check(Find("quest/spread/").Length == 0 && dangers() == 0, "Spread actual end retained.");
        controller.Seek(1.1);
        Check(Find("quest/spread/").Length == 1, "Spread reverse Seek failed.");
        stop();
        Check(previews().Length == 0 && dangers() == 0, "Canals/Lucky owner stop leaked output.");
        Console.WriteLine("PASS native treasure production Lucky Face target spread live death revival frozen generation actual finish and owner cleanup");

        foreach (var (guid, territory, oid, reward, excluded, z) in new[]
        {
            (lucky, 1000u, 0x377Fu, 0x3808u, 0x233Cu, -460f),
            (hidden, 725u, 0x1FCCu, 0x1FCDu, 0x233Cu, -420f),
            (lost, 712u, 0x1F15u, 0x1EBFu, 0x233Cu, -420f),
        })
        {
            var c = new Vector3(0, 0, z);
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(2, reward, c),
                    Actor(3, excluded, c), Actor(100, 0, c)], full: true, party: party),
                Frame(territory, 1, [Actor(1, oid, c, dead: true)], party: party),
                Frame(territory, 2, [Actor(2, reward, c, dead: true)], party: party),
                Frame(territory, 3, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Find("treasure/enemy/").Length == 2
                && (Vector4)Value(Find("treasure/enemy/2").Single(), "Color") == new Vector4(1, 0, 1, 1),
                "Canal/Lucky reward marker missing or helper drawn.");
            controller.Seek(1.1);
            Check(Value(Encounter(), "Active") != null, "Live reward failed phase retain.");
            controller.Seek(2.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
                "AllDeadOrDestroyed incorrectly waited for corpses/helper.");
            stop();
            Console.WriteLine($"PASS native treasure production {territory} reward markers and AllDeadOrDestroyed lifecycle");
        }
    }
}
