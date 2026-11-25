using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPalaceNybeth(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags)!.GetValue(obj)!;
        }
        const string id = "masterplayer.palace.nybeth";
        object Script() => Read(manager.Running[id], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string property) => ((IEnumerable)Read(Encounter(), property)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        var center = new Vector3(300, 0, 300);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(id,
        [
            Header(597), Frame(597, 0, [Actor(1, 0x1808, center + Vector3.UnitX * 60),
                Actor(100, 0, center + Vector3.UnitX * 4), Actor(101, 0, center + Vector3.UnitZ * 4),
                Actor(20, 0x180A, center), Actor(21, 0x1809, center), Actor(22, 0x180B, center)], full: true, party: party),
            Frame(597, .1, [Actor(1, 0x1808, center)], party: party),
            Frame(597, .2, [Actor(1, 0x1808, center, combat: true)], party: party),
            Frame(597, 1,
            [
                Actor(3, 0x233C, center, cast: Cast(6872, 2, center, target: 100, startedAt: 1)),
                Actor(4, 0x233C, center, cast: Cast(6872, 4, center, target: 101, startedAt: 1)),
                Actor(10, 0x233C, center, cast: Cast(6878, 3, center + Vector3.UnitZ * 10, startedAt: 1)),
                Actor(11, 0x233C, center, cast: Cast(6875, 3, center, .7f, startedAt: 1)),
                Actor(12, 0x233C, center, cast: Cast(6874, 3, center, 1.2f, startedAt: 1)),
                Actor(13, 0x233C, center, cast: Cast(6876, 3, center, startedAt: 1)),
            ], party: party),
            Frame(597, 2, [Actor(100, 0, center + Vector3.UnitZ * 2), Actor(101, 0, center + Vector3.UnitX * 2)], party: party),
            Frame(597, 3, [Actor(3, 0x233C, center, dead: true,
                cast: Cast(6872, 2, center, target: 100, startedAt: 1))], party: party),
            Frame(597, 4, [], removed: [101], party: party),
            Frame(597, 5, [Actor(101, 0, center + Vector3.UnitX * 20)], party: party),
            EffectEvent(6, 4, 6872, 1),
            Frame(597, 8, [], party: party),
            Frame(597, 9, new ulong[] { 3, 4, 10, 11, 12, 13 }.Select(a => Actor(a, 0x233C, center)), party: party),
            Frame(597, 10, [Actor(1, 0x1808, center, dead: true)], party: party),
            Frame(597, 11, [], party: party),
        ]);
        controller.Seek(.05);
        Check(Read(Encounter(), "PalaceGuidePrecombatHint") is string guide && guide.Contains("90%, 65%, 40%")
            && Read(Encounter(), "Active") == null && dangers() == 0, "Constructor guide/nav scope.");
        Check((bool)Read(Shown("nybeth/precombat/enemy").Single(), "OutlineOnly")
            && ((Vector3)Read(Read(Shown("nybeth/precombat/enemy").Single(), "Geometry"), "Position")).X < 325,
            "Precombat outside marker was not clamped.");
        controller.Seek(.5);
        Check(Read(Encounter(), "PalaceGuidePrecombatHint") == null && Shown("hw-intro/enemy/").Length == 4,
            "Pull did not consume guide or activate adds markers.");
        dynamic arena = Read(Encounter(), "Arena");
        var bmr = Assembly.Load("MasterPlayer.MechanicRuntime");
        var module = bmr.GetType("BossMod.Heavensward.DeepDungeon.PalaceOfTheDead.DD100NybethObdilord.DD100NybethObdilord")!;
        var bounds = module.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var sourceCenter = Read(bounds, "Center");
        var sx = (float)Read(sourceCenter, "X");
        var sz = (float)Read(sourceCenter, "Z");
        Check((Vector3)Read((object)arena, "Position") == new Vector3(sx, 0, sz), "Nybeth source center.");
        var direction = bmr.GetType("BossMod.WDir")!;
        var contains = bounds.GetType().GetMethod("Contains")!;
        var points = 0;
        for (var x = 274.137f; x < 326; x += .67f)
            for (var z = 274.173f; z < 326; z += .71f)
            {
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f)
                    == (bool)contains.Invoke(bounds, [Activator.CreateInstance(direction, [x - sx, z - sz])!])!,
                    "Nybeth custom polygon differs from actual source oracle.");
                ++points;
            }
        controller.Seek(1.5);
        Check(dangers() == 5 && Shown("nybeth/abyss/").Length == 2
            && Shown("nybeth/abyss/").Count(p => (bool)Read(p, "OutlineOnly")) == 1
            && Values("PredictedDamage").Length == 2, "Abyss own/other shape or prediction target mask.");
        var firstClock = (DateTime)Read(Values("PredictedDamage")[0], "Activation");
        controller.Seek(2.5);
        Check(Shown("nybeth/abyss/").Any(p => (Vector3)Read(Read(p, "Geometry"), "Position") == center + Vector3.UnitX * 2),
            "Bait target did not track.");
        controller.Seek(3.5);
        Check(Values("PredictedDamage").Length == 1
            && (DateTime)Read(Values("PredictedDamage").Single(), "Activation") == firstClock,
            "Dead first source changed prediction clock.");
        controller.Seek(5.5);
        Check((Vector3)Read(Read(Shown("nybeth/abyss/").Single(), "Geometry"), "Position") == center + Vector3.UnitX * 2,
            "Removed target reference rebound same ID.");
        controller.Seek(8.5);
        Check(Shown("nybeth/abyss/").Length == 1 && dangers() == 4, "Effect/deadline ended bait early.");
        controller.Seek(9.5);
        Check(dangers() == 0 && Values("PredictedDamage").Length == 0 && Shown("nybeth/abyss/").Length == 0,
            "Actual raw Finish did not clear output.");
        controller.Seek(10.5);
        Check(Read(Encounter(), "Active") == null && Read(Encounter(), "PalaceGuidePrecombatHint") == null,
            "Living corse adds retained phase or revived guide.");
        var scriptBase = Script().GetType().BaseType!;
        scriptBase.GetMethod("Suspend", flags)!.Invoke(Script(), null);
        var eventType = scriptBase.GetMethod("HandleEvent", flags)!.GetParameters()[0].ParameterType.Assembly
            .GetType("PromeRotation.LogSystem.LogSystemDirectorUpdateEvent")!;
        var wipe = Activator.CreateInstance(eventType)!;
        eventType.GetProperty("Command")!.SetValue(wipe, 0x40000005u);
        scriptBase.GetMethod("HandleEvent", flags)!.Invoke(Script(), [wipe]);
        Check(Read(Encounter(), "palaceGuideLoaded") == null && (bool)Read(Encounter(), "palaceGuideWiping"),
            "Suspended script dropped director owner teardown.");
        controller.Seek(1.5);
        Check(dangers() == 5 && Values("NybethBaits").Length == 2, "Reverse Seek failed to rebuild native bait.");
        var hints = Read(Script(), "Hints");
        Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
            && !(bool)Read(Read(runtimeObject, "Setting"), "RuntimeMovementHeld"), "Replay sent native control.");
        var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        Check((bool)hostType.GetMethod("IsReplaySuppressed", flags)!.Invoke(RuntimeHelpers.GetUninitializedObject(hostType), null)!,
            "Nybeth territory still accepts the migrated floor bridge.");
        stop();
        Console.WriteLine($"PASS native Palace Nybeth source polygon {points} points constructor guide pull reset suspend director five actions target circle original reference dead first source target mask observed finish effect isolation reverse Seek owner floor");
    }
}
