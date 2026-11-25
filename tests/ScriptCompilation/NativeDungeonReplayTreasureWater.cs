using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureAltarWater(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Models(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        string[] Hints(Vector3 p) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [p])!).Cast<string>().ToArray();
        string Icon(double time, ulong source, ulong target) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = 23, Timestamp = Start.AddSeconds(time) },
        });
        const string guid = "masterplayer.treasure.shifting-altars";
        var c = new Vector3(100, 0, 100);
        var p = c + Vector3.UnitZ * 5;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(guid,
        [
            Header(794), Frame(794, 0, [Actor(1, 0x2543, c, combat: true), Actor(2, 0x233C, c),
                Actor(100, 0, p), Actor(101, 0, c)], full: true, party: party),
            Icon(.1, 100, 101),
            Frame(794, 1, [Actor(1, 0x2543, c + Vector3.UnitZ * 2, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 4)], party: party),
            Frame(794, 2, [Actor(100, 0, c + Vector3.UnitX * 4,
                stackedStatus: 160, statusExpiresAt: Start.AddSeconds(20))], party: party),
            Frame(794, 5, [Actor(100, 0, c + Vector3.UnitX * 4)], party: party),
            Icon(6, 101, 100),
            Frame(794, 7, [Actor(2, 0x233C, c, cast: Cast(13374, 3, c, target: 101, startedAt: 7))], party: party),
            Frame(794, 8, [Actor(2, 0x233C, c)], party: party),
            Frame(794, 9, [], party: party),
        ]);
        controller.Seek(.2);
        var iconModel = Value(Encounter(), "TreasureBuffetTarget");
        Check((ulong)Value(Value(iconModel, "Target"), "Id") == 100 && (float)Value(iconModel, "Distance") == 20
            && Find("treasure/buffet/kb/").Length == 2 && dangers() == 1 && Hints(p).Contains("Bait away!"),
            "Buffet Source icon model, preview or edge navigation missing.");
        var firstLanding = (Vector3)Value(Value(Find("treasure/buffet/kb/player").Single(), "Geometry"), "Position");
        controller.Seek(1.1);
        var nextLanding = (Vector3)Value(Value(Find("treasure/buffet/kb/player").Single(), "Geometry"), "Position");
        Check(nextLanding.X > firstLanding.X && nextLanding.Z < 100, "Buffet did not track primary/target dynamically.");
        controller.Seek(2.1);
        Check(Find("treasure/buffet/kb/").Length == 0 && dangers() == 1, "Buffet immunity suppressed edge navigation.");
        controller.Seek(5.1);
        Check(Find("treasure/buffet/kb/").Length == 2, "Icon expired by activation time.");
        controller.Seek(6.1);
        Check(Find("treasure/buffet/kb/").Length == 0 && dangers() == 0, "Latest icon did not replace target.");
        controller.Seek(8.1);
        Check(Encounter().GetType().GetProperty("TreasureBuffetTarget", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter()) == null
            && Find("treasure/buffet/").Length == 0, "Actual Buffet finish retained icon or bait.");
        controller.Seek(.2);
        Check(Find("treasure/buffet/kb/").Length == 2 && dangers() == 1, "Icon reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Airavata icon Source latest replacement raw displacement model dynamic origin immunity actual finish and Seek");

        var buffet = Cast(13374, 4, c, target: 101);
        load(guid,
        [
            Header(794), Frame(794, 0, [Actor(1, 0x2543, c, combat: true), Actor(2, 0x233C, c + Vector3.UnitX * 10),
                Actor(100, 0, c + Vector3.UnitZ * 2), Actor(101, 0, c + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(794, .1, [Actor(2, 0x233C, c + Vector3.UnitX * 10, cast: buffet)], party: party),
            EffectEvent(.5, 2, 13374, 1, c, target: 101),
            Frame(794, 1, [Actor(101, 0, c + Vector3.UnitZ * 10, dead: true)], party: party),
            Frame(794, 2, [], removed: [101], party: party),
            Frame(794, 3, [Actor(101, 0, c + Vector3.UnitX * 15)], party: party),
            Frame(794, 5, [Actor(2, 0x233C, c + Vector3.UnitX * 10)], party: party),
            Frame(794, 6, [], party: party),
        ]);
        controller.Seek(.2);
        var cone = Find("treasure/buffet/cone").Single();
        Check((Vector3)Value(Value(cone, "Geometry"), "Position") == c && dangers() == 1
            && MathF.Abs((float)Value(Value(cone, "Geometry"), "Rotation") + MathF.PI / 4) < .001f
            && !Hints(c + Vector3.UnitZ * 2).Contains("GTFO from baited aoe!")
            && !Models("PredictedDamage").Any(), "Buffet primary origin/caster rotation, base text asymmetry or prediction scope differs.");
        controller.Seek(1.1);
        Check(Find("treasure/buffet/cone").Length == 1 && Models("TreasureBuffetBaits").Length == 1, "Dead bait target removed.");
        controller.Seek(3.1);
        Check((bool)Value(Models("TreasureBuffetBaits").Single(), "TargetRemoved")
            && (Vector3)Value(Value(Find("treasure/buffet/cone").Single(), "Geometry"), "Position") == c,
            "Recreated target rebound old bait.");
        controller.Seek(5.1);
        Check(Find("treasure/buffet/").Length == 0 && dangers() == 0, "Real cast finish retained cone.");
        stop();
        Console.WriteLine("PASS native treasure production Airavata first bait primary center caster direction asymmetric text death frozen identity no effect-end or damage prediction");

        var waterPosition = c + Vector3.UnitZ * 19;
        var kb = Cast(13440, 5, c - Vector3.UnitX * 2);
        load(guid,
        [
            Header(794), Frame(794, 0, [Actor(1, 0x2537, c, combat: true), Actor(9, 0x255B, waterPosition),
                Actor(100, 0, p)], full: true, party: party),
            Frame(794, .1, [Actor(1, 0x2537, c, combat: true, cast: kb)], party: party),
            Frame(794, 1, [Actor(9, 0x255B, c, targetable: false, dead: true, eventState: 7),
                Actor(100, 0, p, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(794, 2, [], removed: [9], party: party),
            Frame(794, 3, [Actor(9, 0x255B, c + Vector3.UnitX * 10, cast: Cast(13443, 4, c, startedAt: 3))], party: party),
            Frame(794, 6, [Actor(1, 0x2537, c, combat: true)], party: party),
            Frame(794, 7, [Actor(9, 0x255B, c + Vector3.UnitX * 10)], party: party),
            Frame(794, 8, [], party: party),
        ]);
        controller.Seek(.2);
        var sphere = Models("TreasureHydrospheres").Single();
        var waterOrigin = (Vector3)Value(sphere, "Position");
        Check(Find("treasure/hydrosphere/").Length == 1 && dangers() == 2 && Find("treasure/kb/").Length == 2
            && (DateTime)Value(sphere, "Activation") == Start.AddSeconds(8.6)
            && Hints(p).Contains("About to be knocked into danger!"), "Kelpie water creation clock, KB hint, display or separate navigation missing.");
        var ambient = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>()
            .Single(a => ((string)Value(a, "Id")).StartsWith("treasure/rising-seas/"));
        Check((Vector3)Value(ambient, "Position") == c && !(bool)Value(ambient, "Display"),
            "Kelpie sector derived from KB origin or rendered as danger.");
        controller.Seek(1.1);
        Check(Find("treasure/kb/").Length == 0 && dangers() == 2 && Models("TreasureHydrospheres").Length == 1
            && (Vector3)Value(Models("TreasureHydrospheres").Single(), "Position") == waterOrigin,
            "Immunity removed sectors or moved/dead/state7 sphere changed locked circle.");
        controller.Seek(2.1);
        Check(Models("TreasureHydrospheres").Length == 1, "Idle destruction removed predicted circle.");
        controller.Seek(3.1);
        Check(Models("TreasureHydrospheres").Length == 2 && Find("treasure/hydrosphere/").Length == 2,
            "Same-ID new sphere did not create independent prediction.");
        controller.Seek(6.1);
        Check(dangers() == 2 && Find("treasure/kb/").Length == 0, "KB finish retained sectors or removed puddles.");
        controller.Seek(7.1);
        Check(Models("TreasureHydrospheres").Length == 0 && dangers() == 0, "Bloody Puddle actual finish did not clear group.");
        controller.Seek(.2);
        Check(Models("TreasureHydrospheres").Length == 1 && dangers() == 2, "Water reverse Seek failed.");
        stop();
        Check(previews().Length == 0 && dangers() == 0, "Water owner cleanup leaked.");
        Console.WriteLine("PASS native treasure production Kelpie created locked water spheres generation lifetime wall KB destination and immunity-independent sectors Seek owner cleanup");

        foreach (var (oid, action, extra) in new[] { (0x2543u, 13375u, 0x233Cu), (0x2537u, 13442u, 0x255Bu) })
        {
            load(guid,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, c, combat: true), Actor(2, 0x2544, c),
                    Actor(3, extra, c), Actor(100, 0, p)], full: true, party: party),
                Frame(794, .1, [Actor(1, oid, c, combat: true, cast: Cast(action, 3, c))], party: party),
                Frame(794, 1, [Actor(1, oid, c, dead: true), Actor(2, 0x2544, c, dead: true)], party: party),
                Frame(794, 2, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Find("treasure/enemy/").Length == 2
                && (Vector4)Value(Find("treasure/enemy/2").Single(), "Color") == new Vector4(1, 0, 1, 1)
                && Models("TreasureWallKnockbackSources").Length == 0
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 13441 }),
                "Water rewards/source rules, inactive Gallop or HydroPush invented KB.");
            controller.Seek(1.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
                "Water sphere/helper incorrectly retained All phase.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} ordinary AOE reward markers source activation and All lifecycle");
        }
    }
}
