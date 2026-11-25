using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPalaceBlackRider(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.palace.blackrider";
        const uint territory = 593;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags)!.GetValue(obj)!;
        }
        object Script() => Read(manager.Running[id], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string property) => ((IEnumerable)Read(Encounter(), property)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        var center = new Vector3(-300, 0, -220);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        var charge = Cast(7089, 4, center, .7f, 100, 1);
        var records = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1814, center, targetId: 100, hitboxRadius: 3.92f),
                Actor(10, 0x1E858E, center + Vector3.UnitX * 15, eventState: 7),
                Actor(100, 0, center + Vector3.UnitX * 10), Actor(101, 0, center + Vector3.UnitZ * 12)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x1814, center, combat: true, targetId: 100, hitboxRadius: 3.92f)], party: party),
            Frame(territory, 1, [Actor(1, 0x1814, center, combat: true, targetId: 100, cast: charge, hitboxRadius: 3.92f),
                Actor(20, 0x18E6, center + Vector3.UnitX * 22, cast: Cast(7157, 6.5, center + Vector3.UnitX * 22, startedAt: 1))], party: party),
            TreasureNativeEffect(1.6, 1, 7088, 77, 100, [100ul, 101ul]),
            EffectEvent(1.61, 1, 7088, 77, target: 100),
            TreasureNativeEffect(1.7, 1, 7088, 77, 100, [100ul]),
            Frame(territory, 2, [Actor(1, 0x1814, center + Vector3.UnitZ * 3, combat: true, targetId: 101,
                cast: charge, hitboxRadius: 3.92f), Actor(100, 0, center + Vector3.UnitX * 8)], party: party),
            Frame(territory, 3, [Actor(100, 0, center + Vector3.UnitX * 8, stackedStatus: 160)], party: party),
            Frame(territory, 4, [Actor(1, 0x1814, center + Vector3.UnitZ * 3, combat: true, targetId: 100,
                hitboxRadius: 3.92f)], party: party),
            Frame(territory, 5, [Actor(20, 0x18E6, center + Vector3.UnitX * 22)], party: party),
            Frame(territory, 6, [Actor(100, 0, center + Vector3.UnitX * 8),
                Actor(1, 0x1814, center, combat: true, targetId: 100, hitboxRadius: 3.92f,
                    cast: Cast(7089, 4, center, 2, 101, 6))], party: party),
            Frame(territory, 7, [Actor(100, 0, center + Vector3.UnitX * 8, dead: true)], party: party),
            Frame(territory, 8, [], removed: [100ul], party: party),
            Frame(territory, 9, [Actor(100, 0, center + Vector3.UnitX * 8)], party: party),
            Frame(territory, 10, [Actor(1, 0x1814, center, combat: true, targetId: 100, hitboxRadius: 3.92f)], party: party),
            Frame(territory, 11, [Actor(1, 0x1814, center, dead: true)], party: party),
            Frame(territory, 12, [], party: party),
        };
        load(id, records);
        controller.Seek(.05);
        Check(Shown("palace-early/precombat/").Length == 2 && dangers() == 0, "Black Rider precombat display scope.");
        controller.Seek(.5);
        var points = PalaceEarlyPairArenaOracle(Encounter(), Read);
        Check(Shown("blackrider/cleave/").Length >= 1 && Shown("trial/voidzone/live").Length == 0,
            "Black Rider auto or state7 pool filter.");
        controller.Seek(1.5);
        var kb = Read(Encounter(), "BlackRiderKnockback");
        Check((Vector3)Read(kb, "Origin") == center && (int)Read(kb, "TargetSlot") == 0
            && (float)Read(kb, "Distance") == 25 && (string)Read(kb, "Kind") == "AwayFromOrigin"
            && Shown("blackrider/kb/path").Length == 1 && Shown("blackrider/charge/").Length == 1
            && Shown("blackrider/cleave/").Length == 0 && Values("PredictedDamage").Length == 1,
            "Black Rider independent raw charge/KB/prediction.");
        Check(dangers() >= 3, "Bait direction and special pool/Infatuation directions not navigated.");
        controller.Seek(1.8);
        Check(Shown("trial/voidzone/event/").Length == 2, "Black Rider complete native packet count/legacy exclusion.");
        controller.Seek(2.5);
        kb = Read(Encounter(), "BlackRiderKnockback");
        var bait = Values("BlackRiderCharges").Single();
        Check((Vector3)Read(kb, "Origin") == center && (ulong)Read(Read(Read(bait, "Target"), "Actor"), "Id") == 100
            && (float)Read(bait, "Length") > 8.5f, "Raw cast movement relocked KB or retargeted bait.");
        controller.Seek(3.5);
        Check(Shown("blackrider/kb/path").Length == 0 && dangers() >= 3
            && Shown("blackrider/charge/").Length == 1, "Immunity removed directional constraints/bait.");
        controller.Seek(4.5);
        Check(Read(Encounter(), "BlackRiderKnockback") == null && Values("BlackRiderCharges").Length == 0
            && Shown("blackrider/kb/").Length == 0 && Shown("blackrider/cleave/").Length == 1,
            "Black Rider real cancel failed to clear components/restore auto.");
        controller.Seek(6.5);
        Check((int)Read(Read(Encounter(), "BlackRiderKnockback"), "TargetSlot") == 1
            && Shown("blackrider/kb/path").Length == 0 && Values("BlackRiderCharges").Length == 1,
            "KB captured wrong slot for non-target viewer.");
        controller.Seek(8.5);
        Check(Values("BlackRiderCharges").Length == 1 && Read(Encounter(), "Active") != null,
            "Absent viewing actor reset global charge history.");
        controller.Seek(11.5);
        Check(Read(Encounter(), "Active") == null && Values("BlackRiderCharges").Length == 0,
            "Black Rider module finish retained public component output.");
        controller.Seek(1.8);
        Check(Values("BlackRiderCharges").Length == 1 && Shown("trial/voidzone/event/").Length == 2,
            "Black Rider reverse Seek did not rebuild event history.");
        PalaceEarlyPairNoExecution(Script(), runtimeObject, Read);
        stop();
        Console.WriteLine($"PASS native Palace Black Rider {points} point source arena dynamic captured charge independent origin slot radial wall25 immunity special directional navigation native poison packet clock real Finish missing viewer reverse Seek owner isolation");
    }

    private static void RunPalaceYaquaru(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.palace.yaquaru";
        const uint territory = 594;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags)!.GetValue(obj)!;
        }
        object Script() => Read(manager.Running[id], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string property) => ((IEnumerable)Read(Encounter(), property)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        object Snapshot()
        {
            var zone = Read(runtimeObject, "Manager");
            return zone.GetType().GetMethod("BuildSnapshot")!.Invoke(zone, [Read(runtimeObject, "CurrentTime")])!;
        }
        var center = new Vector3(-300, 0, -220);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 4, EntityId = 101ul }];
        var records = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1815, center, targetId: 100, hitboxRadius: 5.75f),
                Actor(10, 0x1E9998, center), Actor(12, 0x1E9998, center + Vector3.UnitX * 20, eventState: 7),
                Actor(100, 0, center + Vector3.UnitX * 10), Actor(101, 0, center + Vector3.UnitZ * 12)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x1815, center, combat: true, targetId: 100, hitboxRadius: 5.75f)], party: party),
            Frame(territory, 1, [Actor(1, 0x1815, center, combat: true, targetId: 100, hitboxRadius: 5.75f,
                cast: Cast(7091, 3, center, target: 100, startedAt: 1)),
                Actor(20, 0x233C, center, cast: Cast(7094, 3, center + Vector3.UnitZ * 10, startedAt: 1))], party: party),
            TreasureNativeEffect(2.1, 1, 7091, 7, 100, [100ul, 101ul]),
            EffectEvent(2.11, 1, 7091, 7, target: 100),
            Frame(territory, 3, [Actor(10, 0x1E9998, center + Vector3.UnitX * 3, dead: true, eventState: 7),
                Actor(11, 0x1E9998, center), Actor(100, 0, center + Vector3.UnitX * 10, stackedStatus: 14,
                    statusExpiresAt: Start.AddSeconds(12)), Actor(101, 0, center + Vector3.UnitZ * 12, stackedStatus: 14)], party: party),
            Frame(territory, 4, [], removed: [10ul], party: party),
            Frame(territory, 5, [Actor(10, 0x1E9998, center + Vector3.UnitX * 100)], party: party),
            Frame(territory, 6, [Actor(1, 0x1815, center, combat: true, targetId: 100, hitboxRadius: 5.75f),
                Actor(20, 0x233C, center)], party: party),
            StatusEvent(7, 100, 14, 0, false),
            Frame(territory, 8, [Actor(100, 0, center + Vector3.UnitX * 10)], party: party),
            Frame(territory, 9, [Actor(1, 0x1815, center, dead: true, hitboxRadius: 5.75f)], party: party),
            Frame(territory, 10, [], party: party),
        };
        load(id, records);
        controller.Seek(.05);
        Check(Values("YaquaruPuddles").Length == 0 && Shown("palace-early/precombat/").Length == 2,
            "Yaquaru captured constructor puddles before component activation.");
        controller.Seek(.5);
        var points = PalaceEarlyPairArenaOracle(Encounter(), Read);
        Check(Values("YaquaruPuddles").Length == 1 && Shown("yaquaru/boss-in-puddle").Length == 1
            && !((IEnumerable)Read(Snapshot(), "GoalAreas")).Cast<object>().Any(), "Yaquaru captured array/weak consumer boundary.");
        var pull = Read(Encounter(), "YaquaruPull");
        Check((float)Read(pull, "Radius") == 22.5f
            && (float)pull.GetType().GetMethod("Score", flags)!.Invoke(pull, [center + Vector3.UnitX * 22.5f])! == 0
            && (float)pull.GetType().GetMethod("Score", flags)!.Invoke(pull, [center + Vector3.UnitX * 22.6f])! == 1000,
            "Yaquaru strict score/model radius.");
        controller.Seek(1.5);
        Check(dangers() == 3 && Shown("trial/voidzone/cast").Length == 1
            && !Values("PredictedDamage").Any(), "Yaquaru ordinary AOE/dynamic cast poison or invented Tankbuster.");
        controller.Seek(2.3);
        Check(Shown("trial/voidzone/event").Length == 1, "Yaquaru legacy expanded complete native poison event.");
        controller.Seek(3.5);
        Check((ulong)Read(Encounter(), "YaquaruShouldCleanse") == 1
            && Values("YaquaruPuddles").Length == 1 && Shown("yaquaru/boss-in-puddle").Length == 1,
            "Yaquaru four-slot Heavy/late pool/dead state7 capture.");
        controller.Seek(5.5);
        var captured = Values("YaquaruPuddles").Single();
        Check((bool)Read(captured, "Removed") && (Vector3)Read(Read(captured, "Actor"), "Position") == center + Vector3.UnitX * 3
            && (ulong)Read(Encounter(), "YaquaruShouldCleanse") == 1, "Captured destroyed pool rebound/Heavy countdown cleared.");
        controller.Seek(6.5);
        Check(Shown("trial/voidzone/cast").Length == 0 && (ulong)Read(Encounter(), "YaquaruShouldCleanse") == 1,
            "Real Finish/Heavy latch.");
        controller.Seek(7.5);
        Check((ulong)Read(Encounter(), "YaquaruShouldCleanse") == 0, "Native Heavy Lose route missing.");
        controller.Seek(9.5);
        Check(Read(Encounter(), "Active") == null && Shown("yaquaru/").Length == 0, "Yaquaru module finish output cleanup.");
        controller.Seek(3.5);
        Check((ulong)Read(Encounter(), "YaquaruShouldCleanse") == 1 && Values("YaquaruPuddles").Length == 1,
            "Yaquaru reverse Seek reconstructed wrong captures/latch.");
        PalaceEarlyPairNoExecution(Script(), runtimeObject, Read);
        stop();
        Console.WriteLine($"PASS native Palace Yaquaru {points} point source arena activation-only puddle array reference freeze source score1000 strict complement no new automatic goal Heavy four slots raw expiry sticky real Lose double-view AOE native poison reverse Seek owner isolation");
    }

    private static int PalaceEarlyPairArenaOracle(object encounter, Func<object, string, object> read)
    {
        var bmr = Assembly.Load("MasterPlayer.MechanicRuntime");
        var bounds = bmr.GetType("BossMod.Heavensward.DeepDungeon.PalaceOfTheDead.SharedBounds")!
            .GetField("ArenaBounds607080", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var sourceCenter = read(bounds, "Center");
        var sx = (float)read(sourceCenter, "X");
        var sz = (float)read(sourceCenter, "Z");
        dynamic arena = read(encounter, "Arena");
        Check((Vector3)read((object)arena, "Position") == new Vector3(sx, 0, sz), "Palace early pair exact AABB center.");
        var directionType = bmr.GetType("BossMod.WDir")!;
        var contains = bounds.GetType().GetMethod("Contains")!;
        var points = 0;
        for (var x = -326.137f; x < -274; x += .67f)
            for (var z = -246.173f; z < -194; z += .71f)
            {
                var offset = Activator.CreateInstance(directionType, [x - sx, z - sz])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == (bool)contains.Invoke(bounds, [offset])!,
                    "Palace early pair source arena Contains oracle.");
                ++points;
            }
        return points;
    }

    private static void PalaceEarlyPairNoExecution(object script, object runtime, Func<object, string, object> read)
    {
        var hints = read(script, "Hints");
        Check(!(bool)read(read(hints, "_facing"), "_hasRequest")
            && !(bool)read(read(runtime, "Setting"), "RuntimeMovementHeld"), "Palace replay executed native controls.");
        var hostType = script.GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        Check((bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(RuntimeHelpers.GetUninitializedObject(hostType), null)!, "Palace territory still accepts the migrated floor bridge.");
    }
}
