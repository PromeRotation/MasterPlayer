using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Linq.Expressions;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntKer(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const uint territory = 956;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
        string? Hint() => (string?)Value(Script(), "MechanicHintsText");
        bool Held() => (bool)Value(Script(), "PyreticActive");
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 8, EntityId = 108ul }];
        string FullEffect(double at, uint sequence, ulong[] targets) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 9, ActionId = 27704, MainTargetId = 100, Targets = targets,
                GlobalSequence = sequence, Timestamp = Start.AddSeconds(at) }
        });
        foreach (var action in new uint[] { 27636, 27637, 27652, 27659, 27646, 27639, 27653,
            27641, 27642, 27643, 27644, 27661, 27662, 27663, 27664, 27647, 27640, 27654 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x35CF, default, combat: true), Actor(100, 0, new(0, 0, -10)),
                    Actor(9, 0x233C, default)], full: true, party: party),
                Frame(territory, .1, [Actor(9, 0x233C, default, cast: Cast(action, 6, default))], party: party),
                CastEvent(.1, 9, action, 6, default),
                Frame(territory, 7, [Actor(9, 0x233C, default)], party: party),
            ]);
            controller.Seek(1d);
            var gaze = action is 27647 or 27640 or 27654;
            Check(dangers() == (gaze ? 0 : 1) && (string?)Value(Script(), "PredictedDamageText") == null,
                "Ker ordinary cast lost navigation or invented damage.");
            if (gaze) Check(Hint()?.Contains("Turn away from gaze!") == true && previews().Length > 1,
                "Ker cast gaze lost eye drawing or source-range hint.");
            controller.Seek(7.1);
            Check(dangers() == 0 && Hint() == null, "Ker actual helper finish retained ordinary output.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35CF, default, combat: true), Actor(9, 0x233C, default),
                Actor(100, 0, new(0, 0, -10)), Actor(101, 0, default), Actor(108, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(9, 0x233C, default, cast: Cast(27704, 6, default))], party: party),
            CastEvent(.1, 9, 27704, 6, default),
            Frame(territory, 2, [Actor(9, 0x233C, default)], party: party),
            Frame(territory, 6.5, [], party: party),
            FullEffect(7, 1, [100, 101, 108]),
            StatusEvent(8, 100, 960, 0, false),
            FullEffect(8.1, 1, [100]),
            FullEffect(9, 2, []),
            Frame(territory, 10, [Actor(9, 0x233C, default, cast: Cast(27928, 3, default, startedAt: 10))], party: party),
            Frame(territory, 11, [Actor(9, 0x233C, default)], party: party),
            StatusEvent(12, 101, 2848, 2, true),
            StatusEvent(13, 101, 2848, 0, false),
            StatusEvent(14, 100, 1422, 0, true),
            StatusEvent(15, 100, 1422, 0, false),
            Frame(territory, 16, [], removed: [1], party: party),
        ]);
        controller.Seek(1d);
        Check(!Held() && Hint() == null, "Ker Flare held actions or displayed early text at cast start.");
        var liveSeedWorld = Value(Script(), "ReplayWorld");
        var liveActors = ((IDictionary)Value(liveSeedWorld, "Actors")).Values.Cast<object>().ToArray();
        var liveParty = Value(liveSeedWorld, "Party");
        var livePlayer = Value(liveSeedWorld, "LocalPlayer");
        controller.Seek(3.9);
        Check(!Held() && Hint()?.Contains("Stop everything!") == true, "Ker Flare text lead window differs.");
        controller.Seek(6.5);
        Check(Held(), "Ker Flare cancel cleared its planned activation.");
        var script = Script();
        var hints = Value(script, "Hints");
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Ker offline Stay published live PR action control.");
        controller.Seek(7.1);
        Check(Held(), "Ker complete effect did not retain the hit player's Stay.");
        controller.Seek(8.2);
        Check(!Held(), "Ker per-player loss or duplicate full packet revived Stay.");
        controller.Seek(9.1);
        Check(!Held() && Hint() == null && dangers() == 0, "Ker empty effect did not clear Stay or invented geometry.");
        controller.Seek(11.1);
        Check(Hint()?.Contains("The next 4 interments will be mirrored!") == true, "Ker mirror cast cancel lost its counter.");
        controller.Seek(12.1);
        Check(Hint()?.Contains("The next 2 interments will be mirrored!") == true, "Ker other actor mirror Gain was ignored.");
        controller.Seek(13.1);
        Check(Hint() == null, "Ker mirror status loss retained its counter.");
        controller.Seek(14.1);
        Check((bool)Value(Script(), "MisdirectionActive"), "Ker misdirection event was ignored.");
        controller.Seek(15.1);
        Check(!(bool)Value(Script(), "MisdirectionActive"), "Ker misdirection loss retained control.");
        controller.Seek(6.5);
        Check(Held(), "Ker reverse Seek retained later effect/status cleanup.");
        stop();
        RunHuntKerLive(managerObject.GetType().Assembly, runtimeObject, liveActors, liveParty, livePlayer);
        Console.WriteLine("PASS HuntKer actual DLL helper geometry and three gazes, Flare cancel/lead/activation/full packet/duplicate/loss/empty targets, persistent mirror and status mapping, misdirection, replay control isolation, reverse Seek and cleanup");
    }

    private static void RunHuntKerLive(Assembly assembly, object runtime, object[] observations, object party, object player)
    {
        const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(obj)!;
        var actorType = assembly.GetType("MasterPlayer.Replay.ReplayActorState")!;
        var actors = Array.CreateInstance(actorType, observations.Length);
        for (var i = 0; i < observations.Length; ++i) actors.SetValue(observations[i], i);
        var script = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripts.Hunts.SimpleHunts")!)!;
        var scriptType = script.GetType().BaseType!;
        var encounter = scriptType.GetField("encounter", PRIVATE)!.GetValue(script)!;
        encounter.GetType().GetMethod("Synchronize", PRIVATE)!.Invoke(encounter, [actors, (Func<string, bool>)(_ => true), Start, party]);
        var now = Start.AddSeconds(6.399);
        scriptType.GetField("nextScan", PRIVATE)!.SetValue(script, Start.AddSeconds(60));
        var controlType = assembly.GetType("MasterPlayer.MechanicHost.MechanicFacingControl")!;
        var updateType = controlType.GetNestedType("UpdateRequest", BindingFlags.NonPublic)!;
        var parameters = updateType.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        var hold = false;
        var block = false;
        var cancel = false;
        var calls = 0;
        Action<bool, bool, bool> record = (h, b, c) => { hold = h; block = b; cancel = c; ++calls; };
        var update = Expression.Lambda(updateType,
            Expression.Invoke(Expression.Constant(record), parameters[5], parameters[6], parameters[7]), parameters).Compile();
        using var control = (IDisposable)Activator.CreateInstance(controlType, PRIVATE, null,
            [update, (Action<string>)(_ => { hold = false; block = false; })], null)!;
        object Create(string name, params object?[] args) => Activator.CreateInstance(
            assembly.GetType("MasterPlayer.Scripting." + name)!, PRIVATE, null, args, null)!;
        var safeZone = Create("ScriptSafeZoneApi", "ker-live-control", "Ker live control");
        var draw = Create("ScriptDrawApi", "ker-live-control", safeZone);
        var hints = Create("ScriptHintsApi", (Func<bool>)(() => false), null, control, (Action<bool, ulong[]>)((_, _) => { }));
        var hostType = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
        var host = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(hostType).Invoke(null, [(Func<string, object?[], object?>)((name, _) => name switch
            {
                "get_CurrentPlayer" => player, "get_CurrentPlayerId" => 100ul,
                "get_CurrentTime" => now, "get_ReplayWorld" => null, "get_IsReplay" => false,
                "get_Hints" => hints, "get_SafeZone" => safeZone, "get_Draw" => draw,
                _ => throw new InvalidOperationException("Unexpected Ker live host access: " + name)
            })])!;
        scriptType.BaseType!.GetProperty("Host")!.SetValue(script, host);
        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var enabled = settings.GetType().GetField("EnableSmartMovement")!;
        var wasEnabled = enabled.GetValue(settings);
        var runtimeSettings = Value(runtime, "Setting");
        var allowed = runtimeSettings.GetType().GetProperty("MovementAllowed", PRIVATE)!;
        var wasAllowed = allowed.GetValue(runtimeSettings);
        bool HeldMovement() => (bool)runtimeSettings.GetType().GetProperty("RuntimeMovementHeld", PRIVATE)!.GetValue(runtimeSettings)!;
        bool PlannedHold() => (bool)scriptType.GetProperty("PyreticActive")!.GetValue(script)!;
        try
        {
            enabled.SetValue(settings, true);
            allowed.SetValue(runtimeSettings, true);
            scriptType.GetMethod("OnUpdate", PRIVATE)!.Invoke(script, [.016f]);
            Check(!PlannedHold() && !hold && !HeldMovement(), "Ker live control executed before its exact activation.");
            now = Start.AddSeconds(6.4);
            scriptType.GetMethod("OnUpdate", PRIVATE)!.Invoke(script, [.001f]);
            Check(PlannedHold() && hold && block && cancel && HeldMovement(),
                "Ker live activation waited for the next actor scan or lost PR ACR/cast/MP movement controls.");
            var previousCalls = calls;
            now = now.AddMilliseconds(16);
            scriptType.GetMethod("OnUpdate", PRIVATE)!.Invoke(script, [.016f]);
            Check(hold && HeldMovement() && calls > previousCalls, "Ker live owner lease was not renewed between actor scans.");
            encounter.GetType().GetMethod("KerStatus", PRIVATE)!.Invoke(encounter, [100ul, 960u, now, null]);
            scriptType.GetMethod("OnUpdate", PRIVATE)!.Invoke(script, [.016f]);
            Check(!PlannedHold() && !hold && !block && !HeldMovement(), "Ker status loss retained live action or movement owners.");
        }
        finally
        {
            scriptType.GetMethod("OnStop", PRIVATE)!.Invoke(script, null);
            hints.GetType().GetMethod("Cleanup", PRIVATE)!.Invoke(hints, null);
            enabled.SetValue(settings, wasEnabled);
            allowed.SetValue(runtimeSettings, wasAllowed);
        }
        Console.WriteLine("PASS HuntKer live host: exact Flare activation between actor scans, PR hold/block/cancel publication, MP movement hold, per-frame lease renewal, status loss and owner cleanup");
    }
}
