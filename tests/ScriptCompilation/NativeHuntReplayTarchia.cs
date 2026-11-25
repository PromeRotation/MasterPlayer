using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntTarchia(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object[] Mechanics() => previews().Where(p => ((string)Value(p, "Id")).Contains("hunt/")).ToArray();
        DateTime Activation()
        {
            var area = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Single();
            var window = ((IEnumerable)Value(area, "DangerWindows")).Cast<object>().Single();
            return (DateTime)Value(window, "DangerFrom");
        }
        string Cancel(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = 1ul, ActionId = 18023, Timestamp = Start.AddSeconds(t) },
        });
        const uint territory = 817;
        var initial = new Vector3(1.2345f, 0, 2.3456f);
        var moved = new Vector3(20, 0, 10);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        load("masterplayer.hunts.simple",
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x2873, initial), Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, 1, [Actor(1, 0x2873, moved, combat: true)], party: party),
            Frame(territory, 1.1, [Actor(1, 0x2873, moved, combat: true, cast: Cast(18023, 5, moved, startedAt: 1.1))], party: party),
            CastEvent(1.1, 1, 18023, 5, moved), Cancel(2), EffectEvent(2.1, 1, 18023, 41),
            Frame(territory, 2.2, [Actor(1, 0x2873, moved, combat: true)], party: party),
            EffectEvent(3, 1, 870, 42),
            Frame(territory, 4.1, [Actor(1, 0x2873, moved, combat: true, cast: Cast(18023, 5, moved, startedAt: 4.1))], party: party),
            CastEvent(4.1, 1, 18023, 5, moved), Cancel(5),
            Frame(territory, 5.1, [Actor(1, 0x2873, moved, combat: true)], party: party),
            EffectEvent(6, 1, 870, 42), EffectEvent(7, 1, 999, 43),
            StatusEvent(8, 100, 1422, 0, true), StatusEvent(9, 100, 1422, 0, false),
            Frame(territory, 10, [Actor(1, 0x2873, moved, combat: true, dead: true)], party: party),
            Frame(territory, 11, [], party: party),
        ]);
        controller.Seek(.1);
        Check(dangers() == 0 && Mechanics().Length == 1 && previews().Length == 2, "Tarchia pre-fight preview/navigation contract.");
        dynamic circle = Value(Mechanics().Single(), "Geometry");
        var origin = (Vector3)circle.Position;
        controller.Seek(1.01);
        circle = Value(Mechanics().Single(), "Geometry");
        Check(dangers() == 1 && (Vector3)circle.Position == origin, "Tarchia pull moved constructor circle.");
        controller.Seek(2.3);
        Check(dangers() == 1 && Mechanics().Length == 1 && Math.Abs((Activation() - Start.AddSeconds(10.4)).TotalSeconds) < .00001,
            "Tarchia early cancel/Groundstorm effect follow-up time.");
        circle = Value(Mechanics().Single(), "Geometry");
        Check((Vector3)circle.Position == moved, "Tarchia follow-up did not use the original Groundstorm location.");
        controller.Seek(3.1); Check(dangers() == 0 && Mechanics().Length == 0, "Tarchia auto-attack did not clear.");
        controller.Seek(6.1);
        Check(dangers() == 1 && Math.Abs((Activation() - Start.AddSeconds(13.4)).TotalSeconds) < .00001, "Tarchia duplicate effect cleared the next generation.");
        controller.Seek(7.1); Check(dangers() == 0 && Mechanics().Length == 0, "Tarchia arbitrary effect blocked by a whitelist.");
        controller.Seek(8.1);
        Check((bool)Value(Script(), "MisdirectionActive"), "Tarchia status did not enable existing misdirection control.");
        var nativeInput = Script().GetType().BaseType!.GetField("misdirectionInput", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script());
        Check(nativeInput == null, "Tarchia offline replay issued live PR control.");
        controller.Seek(9.1); Check(!(bool)Value(Script(), "MisdirectionActive"), "Tarchia status loss leaked control.");
        controller.Seek(10.1); Check(previews().Length == 0 && dangers() == 0, "Tarchia death leaked previews.");
        controller.Seek(.1); Check(previews().Length == 2 && dangers() == 0, "Tarchia reverse Seek lost the constructor stage.");
        stop();

        foreach (var action in new uint[] { 18026, 18027, 18028, 18030, 18031, 18029 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory), Frame(territory, 0, [Actor(1, 0x2873, initial, combat: true), Actor(100, 0, Vector3.Zero)], full: true, party: party),
                EffectEvent(.05, 1, 870, 1),
                Frame(territory, .1, [Actor(1, 0x2873, initial, combat: true, cast: Cast(action, 4, initial, MathF.PI / 2))], party: party),
                CastEvent(.1, 1, action, 4, initial, MathF.PI / 2),
                Frame(territory, 6, [Actor(1, 0x2873, initial, combat: true)], party: party), Frame(territory, 7, [], party: party),
            ]);
            controller.Seek(1d);
            var geometric = action is 18026 or 18027 or 18028 or 18030;
            Check(dangers() == (geometric ? 1 : 0) && Mechanics().Length == (geometric ? 1 : 0), $"Tarchia {action} shape/pure-hint output.");
            if (action == 18031) Check(((string)Value(Script(), "PredictedDamageText")).Contains("Raidwide"), "Tarchia raidwide prediction.");
            if (action == 18029) Check(((string)Value(Script(), "MechanicHintsText")).Contains("Pull + Temporary Misdirection"), "Tarchia source hint.");
            controller.Seek(6.1); Check(dangers() == 0 && Mechanics().Length == 0, $"Tarchia {action} end cleanup.");
            stop();
        }
        Console.WriteLine("PASS production Tarchia: seven source casts, constructor preview and pull anchor, cancel follow-up, all-effect cleanup, generation dedup, status control, offline isolation and Seek");
    }
}
