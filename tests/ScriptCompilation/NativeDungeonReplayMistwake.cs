using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMistwake(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.mistwake"; const uint territory = 1314;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("mist/" + key)).ToArray();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = state & 0xFFFF, Param2 = state >> 16, Timestamp = Start.AddSeconds(t) } });
        string TargetCast(double t, ulong source, uint action, ulong target, Vector3 p)
            => JsonSerializer.Serialize(new { t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                data = new { SourceId = source, TargetId = target, ActionId = action, CastTime = 5, EffectPosition = Pose(p), Timestamp = Start.AddSeconds(t) } });
        string Icon(double t, ulong source, ulong target)
            => JsonSerializer.Serialize(new { t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = source, TargetId = target, MarkerId = 524, Timestamp = Start.AddSeconds(t) } });
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL } };
        var p = new Vector3(84, 0, 370);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4841, p, combat: true), Actor(100, 0, p), Actor(101, 0, p + Vector3.UnitX * 5),
                Actor(30, 0x4851, new(72, 0, 357.5f), hitboxRadius: 2.5f)], full: true, party: party),
            CastEvent(.1, 1, 43330, 5, p),
            Frame(territory, .2, [Actor(1, 0x4841, p, combat: true, cast: Cast(43330, 5, p))], party: party),
            Map(.3, 7, 0), Icon(.4, 1, 100),
            Frame(territory, 2, [], removed: [30], party: party),
            EffectEvent(3, 1, 43334, 1, target: 101),
            Frame(territory, 6, [Actor(1, 0x4841, p, combat: true)], party: party),
        ]);
        controller.Seek(1); Check(Group("los/display").Length == 1 && Group("line").Any(a => ((string)Value(a, "Id")).Contains("/rock/30/avoid")),
            "Mistwake LOS/line personal protect.");
        Check(previews().Length >= 3 && dangers() >= 2, "Mistwake both outputs.");
        controller.Seek(2.5); Check(Group("los/display").Length == 1 && !Group("line").Any(a => ((string)Value(a, "Id")).Contains("/rock/")),
            "Mistwake frozen LOS independent of removed actor.");
        controller.Seek(6.5); Check(Group("los").Length == 0 && Group("line").Length == 0 && previews().Length == 0, "Mistwake finish.");
        controller.Seek(1); Check(Group("los/display").Length == 1, "Mistwake rock Seek."); stop();

        p = new(281, 0, -285);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4A77, p, combat: true), Actor(100, 0, p), Actor(101, 0, p + Vector3.UnitX * 5),
                Actor(10, 0x233C, p), Actor(20, 0x4A78, p + Vector3.UnitZ * 10)], full: true, party: party),
            StatusEvent(.1, 20, 2536, 0, true),
            Frame(territory, .2, [Actor(20, 0x4A78, p + Vector3.UnitZ * 10, stackedStatus: 2536)], party: party),
            TargetCast(.3, 10, 45353, 100, p),
            Frame(territory, .4, [Actor(10, 0x233C, p, cast: Cast(45353, 5, p, startedAt: .3, target: 100))], party: party),
            CastEvent(1, 11, 45348, 1.5, p + Vector3.UnitX * 12),
            Frame(territory, 1.1, [Actor(11, 0x233C, p, cast: Cast(45348, 1.5, p + Vector3.UnitX * 12, startedAt: 1))], party: party),
            Frame(territory, 3, [Actor(11, 0x233C, p)], party: party),
            Frame(territory, 6, [Actor(10, 0x233C, p)], party: party),
            EffectEvent(7, 1, 45354, 1), EffectEvent(8, 1, 45354, 2), EffectEvent(9, 1, 45347, 3),
            CastEvent(10, 20, 45349, 2.5, p + Vector3.UnitZ * 10),
            Frame(territory, 10.1, [Actor(20, 0x4A78, p + Vector3.UnitZ * 10, cast: Cast(45349, 2.5, p + Vector3.UnitZ * 10, startedAt: 10))], party: party),
            Frame(territory, 13, [Actor(20, 0x4A78, p + Vector3.UnitZ * 10)], party: party),
        ]);
        controller.Seek(6.5); Check(Group("burst").Length == 1 && Group("charge").Length == 1 && Group("stack").Length > 0,
            "Mistwake queue and three-hit stack retain.");
        controller.Seek(7.5); Check(Group("stack").Length > 0, "Mistwake first repeat.");
        controller.Seek(8.5); Check(Group("stack").Length == 0, "Mistwake second repeat.");
        controller.Seek(13.5); Check(Group("burst").Length == 0 && Group("charge").Length == 0 && previews().Length == 0, "Mistwake FIFO clear.");
        controller.Seek(6.5); Check(Group("burst").Length == 1 && Group("charge").Length == 1, "Mistwake Amdusias Seek."); stop();

        p = new(281, 0, -620);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4A65, p, combat: true), Actor(100, 0, p + Vector3.UnitX * 4)], full: true),
            CastEvent(.1, 1, 45291, 5, p), Frame(territory, .2, [Actor(1, 0x4A65, p, combat: true, cast: Cast(45291, 5, p))]),
            Map(.3, 0x17, 0x00020001),
            Frame(territory, .4, [Actor(30, 0x4A67, p, rotation: .7f)]),
            CastEvent(.5, 10, 45304, 9.6, p), CastEvent(.6, 11, 45302, 6, p),
            Frame(territory, .7, [Actor(10, 0x233C, p, cast: Cast(45304, 9.6, p, startedAt: .5)),
                Actor(11, 0x233C, p, cast: Cast(45302, 6, p, startedAt: .6))]),
            CastEvent(1, 12, 47022, 6.2, p),
            Frame(territory, 1.1, [Actor(12, 0x233C, p, cast: Cast(47022, 6.2, p, startedAt: 1))]),
            Frame(territory, 7, [Actor(11, 0x233C, p), Actor(1, 0x4A65, p, combat: true)]),
            Frame(territory, 8, [Actor(12, 0x233C, p)]),
            CastEvent(9, 1, 45299, 3, p), Frame(territory, 9.1, [Actor(1, 0x4A65, p, combat: true, cast: Cast(45299, 3, p, startedAt: 9))]),
            Frame(territory, 13, [Actor(1, 0x4A65, p, combat: true), Actor(10, 0x233C, p)]),
        ]);
        controller.Seek(2); Check(Group("shrink").Length == 0 && Group("storm").Length == 1 && Group("kb/avoid").Length == 1
            && Group("simple").Length == 1 && (string)Value(Group("simple")[0], "Name") == "Rush", "Mistwake Griffin pending gating.");
        controller.Seek(7.2); Check(Group("simple").Length == 1 && (string)Value(Group("simple")[0], "Name") == "ElectrogeneticForce",
            "Mistwake Griffin force reveal.");
        controller.Seek(8.5); Check(Group("kb").Length == 0 && Group("storm").Length == 1, "Mistwake KB finish.");
        controller.Seek(13.5); Check(previews().Length == 0 && dangers() == 0, "Mistwake final cleanup.");
        controller.Seek(2); Check(Group("kb/avoid").Length == 1 && Group("storm").Length == 1, "Mistwake Griffin Seek."); stop();
        Console.WriteLine("PASS: Mistwake three encounters actual DLL synthetic replay rock LOS/protection/line stack/charge FIFO/status burst/three-hit stack/shrink/Rush gating/left-right knockback/Seek");
    }
}
