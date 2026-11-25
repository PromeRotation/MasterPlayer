using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPalaceGudanna(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.palace.gudanna";
        const uint territory = 595;
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
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var records = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1816, center, targetId: 100),
                Actor(10, 0x18F0, center + Vector3.UnitX * 12), Actor(100, 0, center + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x1816, center, combat: true, targetId: 100)], party: party),
            Frame(territory, 1, [Actor(1, 0x1816, center, combat: true, targetId: 100,
                cast: Cast(7095, 2.5f, center, .7f, startedAt: 1))], party: party),
            Frame(territory, 2, [Actor(1, 0x1816, center, combat: true, targetId: 100)], party: party),
            TreasureNativeEffect(2.1, 1, 7095, 11, 0, []),
            EffectEvent(2.11, 1, 7095, 11),
            TreasureNativeEffect(2.12, 1, 7095, 11, 0, []),
            TreasureNativeEffect(2.2, 1, 7096, 0, 0, []),
            Frame(territory, 3, [Actor(10, 0x18F0, center + Vector3.UnitX * 3.2f, dead: true, targetable: false, eventState: 7),
                Actor(20, 0x233C, center, cast: Cast(7096, 3, center + Vector3.UnitX * 15, startedAt: 3)),
                Actor(21, 0x233C, center, cast: Cast(7098, 2.5f, center, 1.2f, startedAt: 3)),
                Actor(22, 0x233C, center, cast: Cast(7099, 20, center, startedAt: 3))], party: party),
            Frame(territory, 4, [Actor(100, 0, center + Vector3.UnitZ * 10, dead: true)], party: party),
            TreasureNativeEffect(4.1, 99, 7095, 40, 0, []),
            Frame(territory, 4.5, [], removed: [100ul], party: party),
            TreasureNativeEffect(4.6, 99, 7095, 41, 0, []),
            Frame(territory, 5, [Actor(100, 0, center + Vector3.UnitZ * 10)], party: party),
            Frame(territory, 8, [], party: party),
            TreasureNativeEffect(9, 99, 7099, 15, 0, []),
            TreasureNativeEffect(9.1, 99, 7095, 16, 0, []),
            Frame(territory, 10, [Actor(20, 0x233C, center), Actor(21, 0x233C, center),
                Actor(22, 0x233C, center)], removed: [10ul], party: party),
            Frame(territory, 11, [Actor(1, 0x1816, center, dead: true)], party: party),
            Frame(territory, 12, [], party: party),
        };
        load(id, records);
        controller.Seek(.05);
        Check(Read(Encounter(), "PalaceGuidePrecombatHint") is string guide && guide.Contains("10% HP")
            && dangers() == 0, "Gudanna constructor guide/precombat scope.");
        controller.Seek(.5);
        Check(Read(Encounter(), "PalaceGuidePrecombatHint") == null
            && Shown("gudanna/tornado/").Length == 1 && dangers() == 0
            && !Values("PredictedDamage").Any() && (bool)Read(Encounter(), "PreserveStageAcrossBattleEnd"),
            "Gudanna phase guide and tornado semantics.");
        var bmr = Assembly.Load("MasterPlayer.MechanicRuntime");
        var bounds = bmr.GetType("BossMod.Heavensward.DeepDungeon.PalaceOfTheDead.SharedBounds")!
            .GetField("ArenaBounds607080", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var sourceCenter = Read(bounds, "Center");
        var sx = (float)Read(sourceCenter, "X");
        var sz = (float)Read(sourceCenter, "Z");
        dynamic arena = Read(Encounter(), "Arena");
        Check((Vector3)Read((object)arena, "Position") == new Vector3(sx, 0, sz), "Gudanna source arena AABB center.");
        var directionType = bmr.GetType("BossMod.WDir")!;
        var contains = bounds.GetType().GetMethod("Contains")!;
        var points = 0;
        for (var x = -326.137f; x < -274; x += .67f)
            for (var z = -246.173f; z < -194; z += .71f)
            {
                var offset = Activator.CreateInstance(directionType, [x - sx, z - sz])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == (bool)contains.Invoke(bounds, [offset])!,
                    "Gudanna source arena oracle.");
                ++points;
            }
        controller.Seek(1.5);
        Check(dangers() == 1 && (int)Read(Encounter(), "GudannaCastIndex") == 0, "Cast Start changed cycle.");
        controller.Seek(2.05);
        Check(dangers() == 0 && (int)Read(Encounter(), "GudannaCastIndex") == 0, "Cast Finish changed cycle.");
        controller.Seek(2.5);
        var positioning = Read(Encounter(), "GudannaPositioning");
        Check((int)Read(Encounter(), "GudannaCastIndex") == 3 && Read(positioning, "NextAction").ToString() == "TrounceEast",
            "Independent native packets/legacy exclusion or sequence-zero rotation.");
        var score = positioning.GetType().GetMethod("Score", flags)!;
        Check((float)score.Invoke(positioning, [new Vector3(sx + 20, 0, sz)])! == .5f
            && !((IEnumerable)Read(Snapshot(), "GoalAreas")).Cast<object>().Any(),
            "Gudanna raw weak half-plane model lost or navigation expanded.");
        controller.Seek(3.5);
        var tornado = Shown("gudanna/tornado/").Single();
        Check((float)Read(Read(tornado, "Geometry"), "Radius") == 10
            && (Vector3)Read(Read(tornado, "Geometry"), "Position") == center + Vector3.UnitX * 3.2f
            && ((IEnumerable)Read(Snapshot(), "HardObstacles")).Cast<object>().Count() == 1
            && dangers() == 2 && !Values("PredictedDamage").Any(),
            "Tornado raw obstacle/dead state7 or CastHint geometry.");
        controller.Seek(4.2);
        Check((int)Read(Encounter(), "GudannaCastIndex") == 4, "Dead viewpoint stopped global counter.");
        controller.Seek(4.8);
        Check((int)Read(Encounter(), "GudannaCastIndex") == 5 && Read(Encounter(), "Active") != null,
            "Absent viewpoint reset source component.");
        controller.Seek(8.5);
        Check(dangers() == 2 && (int)Read(Encounter(), "GudannaCastIndex") == 5,
            "Prediction timeout altered raw casts/cycle.");
        controller.Seek(9.5);
        Check((bool)Read(Encounter(), "GudannaRotationDisabled") && Read(Encounter(), "GudannaPositioning") == null
            && (int)Read(Encounter(), "GudannaCastIndex") == 5, "Meteor failed to latch disable.");
        controller.Seek(10.5);
        Check(dangers() == 0 && Shown("gudanna/tornado/").Length == 0
            && !((IEnumerable)Read(Snapshot(), "HardObstacles")).Cast<object>().Any(), "Real Finish/destroy cleanup.");
        controller.Seek(11.5);
        Check(Read(Encounter(), "Active") == null && Read(Encounter(), "PalaceGuidePrecombatHint") == null,
            "Dead source phase revived constructor guide.");
        controller.Seek(2.5);
        Check((int)Read(Encounter(), "GudannaCastIndex") == 3 && !(bool)Read(Encounter(), "GudannaRotationDisabled"),
            "Reverse Seek rotation reconstruction.");
        var hints = Read(Script(), "Hints");
        Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
            && !(bool)Read(Read(runtimeObject, "Setting"), "RuntimeMovementHeld"), "Gudanna replay executed native control.");
        var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        Check((bool)hostType.GetMethod("IsReplaySuppressed", flags)!
            .Invoke(RuntimeHelpers.GetUninitializedObject(hostType), null)!, "Gudanna territory still accepts the migrated floor bridge.");
        stop();
        Console.WriteLine($"PASS native Palace Gudanna {points} point source arena constructor guide consumed once exact ordinary shapes raw finish all tornado obstacle dead state7 native packet eight cycle meteor latch weak positioning model without movement expansion reverse Seek owner replay floor isolation");
    }
}
