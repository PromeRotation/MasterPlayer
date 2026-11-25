using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPalaceGodmother(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.palace.godmother";
        const uint territory = 596;
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
        var center = new Vector3(-300, 0, -236);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(id,
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1817, center, targetId: 100),
                Actor(2, 0x18E8, center + Vector3.UnitX * 4),
                Actor(3, 0x18EA, center + Vector3.UnitZ * 5),
                Actor(10, 0x18E9, center + Vector3.UnitX * 8),
                Actor(100, 0, center + Vector3.UnitX * 12), Actor(101, 0, center + Vector3.UnitZ * 15)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x1817, center, combat: true, targetId: 100)], party: party),
            Frame(territory, 1, [Actor(3, 0x18EA, center + Vector3.UnitZ * 5, dead: true, targetable: false)], party: party),
            TreasureNativeEffect(1.2, 999, 7104, 11, 0, []),
            EffectEvent(1.21, 999, 7104, 11),
            TreasureNativeEffect(1.3, 999, 7104, 11, 0, []),
            TreasureNativeEffect(1.4, 999, 7104, 0, 0, []),
            Frame(territory, 2, [Actor(1, 0x1817, center, combat: true, targetId: 100,
                cast: Cast(7102, 25, center, startedAt: 2)),
                Actor(2, 0x18E8, center, cast: Cast(7105, 20, center, startedAt: 2))], party: party),
            Frame(territory, 12, [Actor(10, 0x18E9, center, dead: true)], party: party),
            Frame(territory, 13, [Actor(10, 0x18E9, center, dead: true, cast: Cast(7106, 3, center, startedAt: 13))], party: party),
            Frame(territory, 14, [Actor(10, 0x18E9, center, dead: true)], party: party),
            Frame(territory, 15, [Actor(1, 0x1817, center, combat: true, targetId: 101),
                Actor(2, 0x18E8, center)], party: party),
            Frame(territory, 16, [Actor(1, 0x1817, center, dead: true)], party: party),
            Frame(territory, 17, [], party: party),
        ]);
        controller.Seek(.05);
        Check(Read(Encounter(), "Active") == null && dangers() == 0, "Godmother precombat published active navigation.");
        controller.Seek(.5);
        Check(Values("GodmotherExplosions").Length == 1 && Shown("godmother/explosion/").Length == 1
            && Shown("godmother/cleave/").Length == 1 && Shown("hw-intro/enemy/").Length == 2,
            "Godmother creation and enemy/cleave previews.");
        Check(Values("ForbiddenTargets").Cast<ulong>().SequenceEqual([3ul])
            && Values("ForbiddenDotTargets").Cast<ulong>().SequenceEqual([2ul]), "Giddy and Grey restriction split.");
        var first = Values("GodmotherExplosions").Single();
        var firstOrigin = (Vector3)Read(first, "Origin");
        Check(Vector3.Distance(firstOrigin, center + Vector3.UnitX * 8) < .04f
            && Math.Abs(((DateTime)Read(first, "Activation") - Start.AddSeconds(10.1)).TotalSeconds) < .00001,
            $"Godmother bootstrap lock/clock: {Read(first, "Activation"):O}.");
        var bmr = Assembly.Load("MasterPlayer.MechanicRuntime");
        var bounds = bmr.GetType("BossMod.Heavensward.DeepDungeon.PalaceOfTheDead.SharedBounds")!
            .GetField("ArenaBounds2090110", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var sourceCenter = Read(bounds, "Center");
        var sx = (float)Read(sourceCenter, "X");
        var sz = (float)Read(sourceCenter, "Z");
        dynamic arena = Read(Encounter(), "Arena");
        Check((Vector3)Read((object)arena, "Position") == new Vector3(sx, 0, sz), "Godmother source arena center.");
        var directionType = bmr.GetType("BossMod.WDir")!;
        var contains = bounds.GetType().GetMethod("Contains")!;
        var points = 0;
        for (var x = -326.137f; x < -274; x += .67f)
            for (var z = -262.173f; z < -210; z += .71f)
            {
                var offset = Activator.CreateInstance(directionType, [x - sx, z - sz])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == (bool)contains.Invoke(bounds, [offset])!,
                    "Godmother source arena oracle.");
                ++points;
            }
        controller.Seek(1.1);
        var pull = Read(Encounter(), "GodmotherPull");
        Check((Vector3)Read(pull, "Destination") == new Vector3(-305, 0, -240), "Dead first Giddy initial destination.");
        var safeZoneManager = Read(runtimeObject, "Manager");
        var snapshot = safeZoneManager.GetType().GetMethod("BuildSnapshot")!.Invoke(safeZoneManager, [Start.AddSeconds(1.1)])!;
        Check(!((IEnumerable)Read(snapshot, "GoalAreas")).Cast<object>().Any()
            && Shown("godmother/pull").Length == 0, "Source weight10 must not expand existing bridge movement.");
        controller.Seek(1.5);
        Check((int)Read(Encounter(), "GodmotherBombIndex") == 3
            && (Vector3)Read(Read(Encounter(), "GodmotherPull"), "Destination") == new Vector3(-300, 0, -235),
            "Independent native packets/legacy exclusion or repeated source destination.");
        controller.Seek(2.5);
        Check(Shown("godmother/cleave/").Length == 0 && Values("PredictedDamage").Length == 4
            && dangers() == 1, "Godmother Raidwide became spatial or failed to suppress cleave.");
        controller.Seek(12.5);
        Check(Values("GodmotherExplosions").Length == 1 && (Vector3)Read(Values("GodmotherExplosions").Single(), "Origin") == firstOrigin
            && dangers() == 1, "Dead/expired bomb prediction moved or vanished.");
        controller.Seek(14.5);
        Check(Values("GodmotherExplosions").Length == 0 && Shown("godmother/explosion/").Length == 0,
            "Godmother real Finish did not clear wave.");
        controller.Seek(15.5);
        Check(Shown("godmother/cleave/").Length == 1 && Values("PredictedDamage").Length == 0
            && Read(Encounter(), "GodmotherPull") == null, "Restore cleave/stop pull on target change.");
        controller.Seek(16.5);
        Check(Read(Encounter(), "Active") == null && dangers() == 0, "Godmother primary death owner cleanup.");
        controller.Seek(.5);
        Check(Values("GodmotherExplosions").Length == 1 && (int)Read(Encounter(), "GodmotherBombIndex") == 0,
            "Godmother reverse Seek reconstruction.");
        var hints = Read(Script(), "Hints");
        Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
            && !(bool)Read(Read(runtimeObject, "Setting"), "RuntimeMovementHeld")
            && !((IEnumerable)Read(hints, "forbiddenTargets")).Cast<object>().Any(), "Godmother replay executed native controls.");
        var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        Check((bool)hostType.GetMethod("IsReplaySuppressed", flags)!
            .Invoke(RuntimeHelpers.GetUninitializedObject(hostType), null)!, "Palace territory still accepts the migrated floor bridge.");
        var scriptBase = Script().GetType().BaseType!;
        scriptBase.GetMethod("Suspend", flags)!.Invoke(Script(), null);
        var eventType = scriptBase.GetMethod("HandleEvent", flags)!.GetParameters()[0].ParameterType.Assembly
            .GetType("PromeRotation.LogSystem.LogSystemDirectorUpdateEvent")!;
        var wipe = Activator.CreateInstance(eventType)!;
        eventType.GetProperty("Command")!.SetValue(wipe, 0x40000005u);
        scriptBase.GetMethod("HandleEvent", flags)!.Invoke(Script(), [wipe]);
        Check(Read(Encounter(), "Active") == null && Values("GodmotherExplosions").Length == 0
            && Values("ForbiddenTargets").Length == 0 && (bool)Read(Encounter(), "palaceGuideWiping"),
            "Suspended Godmother lost director owner teardown.");
        stop();
        Console.WriteLine($"PASS native Palace Godmother exact {points} point source arena first Giddy no cast restriction Grey DoT split independent native packets legacy exclusion repeated destination weight10 pull model without movement expansion creation fixed time real whole wave Finish cleave suppression Raidwide raw clock reverse Seek owner replay floor isolation");
    }
}
