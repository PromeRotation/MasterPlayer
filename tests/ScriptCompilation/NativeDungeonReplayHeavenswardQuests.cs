using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeavenswardQuests(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object[] Aoes() => previews().Where(p => !((string)Value(p, "Id")).Contains("hw-intro/enemy/")).ToArray();
        var center = new Vector3(-35.75f, 0, -205.5f);
        load("masterplayer.quest.a-spectacle-for-the-ages",
        [
            Header(533), Frame(533, 0, [Actor(1, 0x154E, center, combat: true, targetId: 100),
                Actor(100, 0, center), Actor(2, 0x1552, center + Vector3.UnitX * 10),
                Actor(3, 0x1E9FA8, center, targetable: false)], full: true),
            Frame(533, .1, [Actor(2, 0x1552, center, cast: Cast(5765, 3, center))]),
            CastEvent(.1, 2, 5765, 3, center),
            Frame(533, 1, [Actor(3, 0x1E9FA8, center, eventState: 7, targetable: false)]),
            Frame(533, 4, [Actor(2, 0x1552, center)]), Frame(533, 5, []),
        ]);
        controller.Seek(.5);
        Check(Obstacles() == 1 && dangers() == 1 && Aoes().Length == 2, "Aldynn obstacle/AOE/dual-view separation.");
        var encounter = Encounter();
        Check((ulong)encounter.GetType().GetMethod("PriorityTarget")!.Invoke(encounter, [1ul, center])! == 2,
            "Aldynn Tizona priority5.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 5765 }),
            "Aldynn helper AID ownership.");
        Check(!(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 9999 }),
            "Aldynn swallowed an unknown action.");
        dynamic sector = Value(Aoes().Single(p => Value(p, "Geometry").GetType().Name != "CircleGeometry"), "Geometry");
        Check(!(bool)sector.Contains(center + Vector3.UnitZ, 0f) && (bool)sector.Contains(center + Vector3.UnitZ * 6, 0f)
            && !(bool)sector.Contains(center - Vector3.UnitZ * 6, 0f), "Aldynn donut sector.");
        controller.Seek(1.1); Check(Obstacles() == 0 && dangers() == 1, "Aldynn EventState cleanup removed cast.");
        controller.Seek(4.1); Check(dangers() == 0 && Aoes().Length == 0, "Aldynn cast finish leaked.");
        controller.Seek(.5); Check(Obstacles() == 1 && dangers() == 1, "Aldynn reverse Seek.");
        stop();

        center = new(252.75f, 0, 553);
        load("masterplayer.quest.close-encounters-of-the-vith-kind",
        [
            Header(459), Frame(459, 0, [Actor(1, 0xF1C, center, combat: true), Actor(100, 0, center),
                Actor(2, 0xF5D, center, targetable: false), Actor(3, 0xF5D, center + Vector3.UnitX * 2),
                Actor(4, 0x1E88F5, center, eventState: 7, targetable: false, dead: true)], full: true),
            Frame(459, .1, [Actor(1, 0xF1C, center, combat: true, cast: Cast(4005, 3, center))]),
            CastEvent(.1, 1, 4005, 3, center),
            Frame(459, 1, [Actor(2, 0xF5D, center + new Vector3(3, 0, 4), rotation: MathF.PI / 2, targetable: false)]),
            Frame(459, 4, [Actor(1, 0xF1C, center, combat: true)]),
            EffectEvent(4.1, 2, 3825, 1),
            Frame(459, 5, [Actor(2, 0xF5D, center, targetable: false, dead: true)]),
            Frame(459, 6, [], removed: [2]),
            Frame(459, 7, [Actor(1, 0xF1C, center, combat: true, cast: Cast(4005, 3, center, startedAt: 7))]),
            CastEvent(7, 1, 4005, 3, center),
            Frame(459, 8, [], removed: [3]), Frame(459, 9, [], removed: [4]), Frame(459, 10, []),
        ]);
        controller.Seek(.01);
        Check(Obstacles() == 1 && dangers() == 0 && previews().Length == 4, "VIth Regula pre-activation swords/obstacle.");
        Check((Vector4)Value(previews().Single(p => ((string)Value(p, "Id")).EndsWith("hw-intro/enemy/2")), "Color") == new Vector4(1, 1, 0, 1),
            "VIth Regula untargetable sword lost yellow marker.");
        Check(!(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 9999 }),
            "VIth Regula swallowed an unknown action.");
        controller.Seek(.5); Check(Obstacles() == 1 && dangers() == 2, "VIth Regula sword activation.");
        controller.Seek(1.1);
        dynamic rectangle = Value(Aoes().Single(p => ((string)Value(p, "Id")).EndsWith("terminus/2")), "Geometry");
        var moved = center + new Vector3(3, 0, 4);
        Check((bool)rectangle.Contains(moved + Vector3.UnitX * 30, 0f)
            && !(bool)rectangle.Contains(moved + Vector3.UnitZ * 10, 0f), "VIth Regula dynamic sword facing.");
        controller.Seek(4.2); Check(dangers() == 2, "VIth Regula cast end/effect closed swords.");
        controller.Seek(5.1); Check(dangers() == 1, "VIth Regula one sword death closed other.");
        controller.Seek(6.1); Check(dangers() == 0 && Obstacles() == 1, "VIth Regula destruction did not close sword group.");
        controller.Seek(7.5); Check(dangers() == 1, "VIth Regula next cast failed to reactivate.");
        controller.Seek(8.1); Check(dangers() == 0, "VIth Regula last destruction leaked.");
        controller.Seek(9.1); Check(Obstacles() == 0, "VIth Regula obstacle removal leaked.");
        controller.Seek(1.1); Check(dangers() == 2 && Obstacles() == 1, "VIth Regula reverse Seek.");
        stop();
        Console.WriteLine("PASS production HW quests: true territories 533/459, donut-sector, adds priority, filtered/raw obstacles, dynamic sword groups, targetability, death vs destruction, effects, Seek and cleanup");
    }
}
