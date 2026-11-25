using System.Collections;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntGunitt(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        string? Text(string name)
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return (string?)Value(script, name);
        }
        object[] Stacks() => previews().Where(p => ((string)Value(p, "Id")).Contains("/stack/preview/")).ToArray();
        string Icon(double at, ulong actor, ulong decoy) => JsonSerializer.Serialize(new
        {
            t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = decoy, MarkerId = 93, Timestamp = Start.AddSeconds(at) }
        });
        const uint territory = 817;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul },
            new { Slot = 2, EntityId = 102ul }, new { Slot = 24, EntityId = 103ul }];
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x2852, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(30, 0, 0)), Actor(101, 0, default), Actor(102, 0, new(3, 0, 0)),
                Actor(103, 0, new(5, 0, 0))], full: true, party: party),
            Icon(.05, 101, 100),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(17361, 5, default, target: 100))], party: party),
            CastEvent(.1, 999, 17361, 5, default, target: 100),
            StatusEvent(.2, 100, 1138, 0, true),
            Icon(1, 102, 100),
            StatusEvent(2, 103, 1138, 0, true),
            EffectEvent(3, 1, 17361, 1),
            Frame(territory, 4, [Actor(101, 0, default, dead: true)], party: party),
            Frame(territory, 6, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 7, [], removed: [1], party: party),
        ]);
        controller.Seek(.15);
        Check(Stacks().Length == 1 && Stacks().All(p => (bool)Value(p, "OutlineOnly"))
            && dangers() == 1 && Text("PredictedDamageText") == null,
            "Gunitt icon SourceId, drawing outline, full-mask guard or native stack navigation missing.");
        controller.Seek(1.5);
        Check(Stacks().Length == 2 && dangers() == 2 && Text("PredictedDamageText")?.Contains("Shared") == true,
            "Gunitt icon append or post-icon forbidden status did not reach native drawing/navigation/prediction.");
        dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        foreach (var danger in ((IEnumerable)snapshot.DangerAreas).Cast<object>())
        {
            var windows = ((IEnumerable)Value(danger, "DangerWindows")).Cast<object>();
            Check(windows.All(w => (DateTime)Value(w, "DangerFrom") >= Start.AddSeconds(5.05)),
                "Gunitt icon lost its exact five-second activation clock.");
        }
        controller.Seek(3.5);
        Check(Stacks().Length == 2, "Gunitt Effect consumed the Finish-only queue.");
        controller.Seek(4.5);
        Check(Stacks().Length == 1 && dangers() == 1, "Gunitt dead FIFO head retained visible output.");
        controller.Seek(6.5);
        Check(Stacks().Length == 1 && dangers() == 1, "Gunitt helper Finish skipped the dead head or consumed the alive tail.");
        controller.Seek(7.1);
        Check(previews().Length == 0 && dangers() == 0 && Text("PredictedDamageText") == null,
            "Gunitt removal leaked stack output or prediction.");
        controller.Seek(1.5);
        Check(Stacks().Length == 2 && dangers() == 2, "Gunitt reverse Seek kept future FIFO consumption.");
        stop();
        Console.WriteLine("PASS HuntGunitt actual DLL icon SourceId, exact +5s stack clocks, outline and native navigation, live forbidden masks, Shared prediction, dead FIFO head, helper Finish, reverse Seek and cleanup");
    }
}
