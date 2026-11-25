using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFaded(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Views(string part) => previews().Where(p => ((string)Value(p, "Id")).Contains(part)).ToArray();
        ulong Target(Vector3 p) => (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1ul, p])!;
        float? Facing(Vector3 p) => (float?)Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [p, 0f]);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 8, EntityId = 101ul }];
        const uint territory = 932;
        var center = new Vector3(-247, 0, 321);
        var eye = center + Vector3.UnitZ * 10;
        load("masterplayer.quest.faded-memories",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x2F1D, center, combat: true, objectIndex: 2, isEnemy: true),
                Actor(2, 0x2F1A, center, objectIndex: 4, isEnemy: true), Actor(3, 0x2F1C, center, objectIndex: 6, isEnemy: true),
                Actor(4, 0x2F1B, center, objectIndex: 8, isEnemy: true), Actor(100, 0, center)], full: true, party: party),
            Frame(territory, 1, [Actor(1, 0x2F1D, center, combat: true, objectIndex: 2, isEnemy: true,
                cast: Cast(21090, 4, eye, startedAt: 1)),
                Actor(2, 0x2F1A, center, objectIndex: 4, isEnemy: true, cast: Cast(21086, 3.5, center, startedAt: 1))], party: party),
            CastEvent(1, 1, 21090, 4, eye), CastEvent(1, 2, 21086, 3.5, center),
            StatusEvent(2, 1, 671, 0, true), StatusEvent(2.1, 2, 671, 0, true),
            StatusEvent(2.2, 3, 671, 0, true), StatusEvent(2.3, 4, 671, 0, true),
            StatusEvent(3, 1, 671, 0, false),
            Frame(territory, 6, [Actor(1, 0x2F1D, center, combat: true, objectIndex: 2, isEnemy: true),
                Actor(2, 0x2F1A, center, objectIndex: 4, isEnemy: true)], party: party),
            Frame(territory, 7, [Actor(1, 0x2F1D, center, combat: true, dead: true)], party: party),
            Frame(territory, 8, [], party: party)
        ]);
        controller.Seek(1.5);
        Check(dangers() == 1 && Views("thordan/gaze/").Length == 3 && Views("job-quest/enemy/").Length == 4,
            "Thordan four enemies, eye/pupil/arc or rectangular navigation missing.");
        Check(Facing(center) == null, "Thordan forced gaze-facing too early.");
        controller.Seek(2.05);
        Check(Target(center) != 1 && ((IEnumerable)Value(Encounter(), "ForbiddenTargets")).Cast<ulong>().Count() == 0,
            "Thordan 671 status event ignored or became hard incidental AOE forbidden.");
        controller.Seek(2.4);
        Check(Target(center) == 0, "Thordan all-invincible target selector did not hold.");
        controller.Seek(3.1);
        Check(Target(center) == 1, "Thordan status loss failed target handoff.");
        controller.Seek(4.8);
        Check(Facing(center) is { } facing && MathF.Cos(facing) < .7f
            && Facing(eye - Vector3.UnitZ * 10001) == null, "Thordan PR-facing plan lost its source gaze range/deadline.");
        var hints = Value(Script(), "Hints");
        var control = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)control.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!,
            "Thordan offline replay submitted live PR facing.");
        controller.Seek(6.1);
        Check(dangers() == 0 && Views("thordan/gaze/").Length == 0 && Facing(center) == null, "Thordan actual Finish leaked output.");
        controller.Seek(7.1);
        Check(previews().Length == 0, "Thordan primary death retained helper output.");
        controller.Seek(1.5);
        Check(Views("thordan/gaze/").Length == 3, "Thordan reverse Seek failed gaze reconstruction.");
        stop();
        Console.WriteLine("PASS production Faded Thordan: exact rectangle, locked range10000 gaze, PR-facing plan/offline isolation, four markers, event671 target priority and selector hold, actual endings and Seek");

        center = new(-321.03f, 0, 617.73f);
        load("masterplayer.quest.faded-memories",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x2F28, center, combat: true, targetable: false),
                Actor(2, 0x2F2B, center), Actor(3, 0x2F2A, center), Actor(4, 0x2F2C, center), Actor(5, 0x2F29, center),
                Actor(100, 0, center), Actor(101, 0, center), Actor(102, 0xFFFF, center, subKind: 5, isEnemy: false)], full: true, party: party),
            Frame(territory, 1, [Actor(1, 0x2F28, center, targetable: false),
                Actor(2, 0x2F2B, center, combat: true, dead: true, targetable: false)], party: party),
            Frame(territory, 2, [Actor(2, 0x2F2B, center, combat: true)], party: party),
            Frame(territory, 3, [Actor(100, 0, center, cast: Cast(21121, 7, center, startedAt: 3)),
                Actor(10, 0x233C, center, cast: Cast(21117, 5, center, startedAt: 3)),
                Actor(5, 0x2F29, center, cast: Cast(21118, 5, center, startedAt: 3))], party: party),
            Frame(territory, 9, [Actor(10, 0x233C, center), Actor(5, 0x2F29, center)], party: party),
            Frame(territory, 11, [Actor(100, 0, center)], party: party),
            Frame(territory, 12, [Actor(1, 0x2F28, center, dead: true)], party: party),
            Frame(territory, 13, [], party: party)
        ]);
        controller.Seek(.5);
        Check(!(bool)Value(Script(), "EncounterActive") && previews().Length == 0, "Zenos primary combat bypassed sword pull.");
        controller.Seek(1.5);
        Check((bool)Value(Script(), "EncounterActive") && Views("job-quest/enemy/").Length == 2,
            "Zenos dead untargetable combat sword failed pull or drew itself.");
        controller.Seek(3.5);
        var markers = Views("job-quest/enemy/").Select(p => (string)Value(p, "Id")).Order().ToArray();
        Check(markers.Length == 3 && markers.All(id => id.EndsWith("/2") || id.EndsWith("/3") || id.EndsWith("/4"))
            && dangers() == 2, "Zenos drew wrong actors or omitted active geometric components.");
        Check(Damage().Select(d => (ulong)Value(d, "TargetId")).Order().SequenceEqual(new ulong[] { 100, 101, 102 }),
            "Zenos player-cast FinalArt lost party/alliance/NPC prediction mask.");
        controller.Seek(9.5);
        Check(dangers() == 0 && Damage().Length == 3, "Independent Zenos cast endings cleared player raidwide early.");
        controller.Seek(11.5);
        Check(Damage().Length == 0, "FinalArt actual finish retained prediction.");
        controller.Seek(12.5);
        Check(previews().Length == 0 && dangers() == 0, "Zenos primary death waited on combat swords.");
        controller.Seek(3.5);
        Check(dangers() == 2 && Damage().Length == 3, "Zenos reverse Seek failed to reconstruct sword pull/player raidwide.");
        stop();
        Console.WriteLine("PASS production Faded Zenos: any-sword pull, primary HP/death lifecycle, sword-only markers, exact two geometric casts, player-origin raidwide and full mask, independent finishes, Seek and cleanup");
    }
}
