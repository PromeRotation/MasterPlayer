using System.Reflection;
using System.Runtime.CompilerServices;

internal static class ScriptMovementRegression
{
    public static void Run(Assembly assembly, object manager, object runtime)
    {
        const BindingFlags INTERNAL = BindingFlags.Instance | BindingFlags.NonPublic;
        var pluginType = typeof(PromeRotation.Plugin);
        var instance = pluginType.GetProperty("Instance")!;
        var previousInstance = instance.GetValue(null);
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var previousEnabled = (bool)settings.EnableSmartMovement;
        var setting = runtime.GetType().GetProperty("Setting")!.GetValue(runtime)!;
        var allowed = setting.GetType().GetProperty("MovementAllowed", INTERNAL)!;
        var previousAllowed = (bool)allowed.GetValue(setting)!;
        var replay = manager.GetType().GetProperty("ReplayModeActive", INTERNAL)!;
        var previousReplay = (bool)replay.GetValue(manager)!;
        var apiType = assembly.GetType("MasterPlayer.Scripting.ScriptMoveApi")!;
        dynamic first = Activator.CreateInstance(apiType, INTERNAL, null, [null], null)!;
        dynamic second = Activator.CreateInstance(apiType, INTERNAL, null, [null], null)!;
        bool Flag(string name) => (bool)setting.GetType().GetProperty(name, INTERNAL)!.GetValue(setting)!;
        void SetField(object target, string name, object value) => target.GetType().GetField(name, INTERNAL)!.SetValue(target, value);
        void Cleanup(object api) => apiType.GetMethod("Cleanup", INTERNAL)!.Invoke(api, null);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        try
        {
            var host = RuntimeHelpers.GetUninitializedObject(pluginType);
            var legacy = RuntimeHelpers.GetUninitializedObject(pluginType.Assembly.GetType("PromeRotation.Spatial.SafeZone.SafeZoneMovementSystem")!);
            var legacySetting = new PromeRotation.Spatial.SafeZone.SafeZoneSetting();
            SetField(host, "_safeZoneMovementSystem", legacy);
            SetField(host, "_safeZoneSetting", legacySetting);
            var rebornType = pluginType.Assembly.GetType("PromeRotation.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem");
            object? reborn = null;
            var rebornSetting = new PromeRotation.Spatial.SafeZone.SafeZoneSetting();
            if (rebornType != null)
            {
                reborn = RuntimeHelpers.GetUninitializedObject(rebornType);
                SetField(host, "_safeZoneRebornMovementSystem", reborn);
                SetField(host, "_safeZoneRebornSetting", rebornSetting);
            }
            instance.SetValue(null, host);
            settings.EnableSmartMovement = true;
            allowed.SetValue(setting, true);
            replay.SetValue(manager, false);

            Check(first.TryHoldAutomaticMovement() && first.TryHoldAutomaticMovement() && second.TryHoldAutomaticMovement(),
                "Each facade must acquire an idempotent automatic-movement hold.");
            first.ReleaseAutomaticMovement();
            first.ReleaseAutomaticMovement();
            Check(Flag("RuntimeMovementHeld"), "Releasing one script removed another script's hold.");
            Cleanup((object)second);
            Check(!Flag("RuntimeMovementHeld") && !Flag("RuntimeMovementRequested"), "Cleanup retained a hold without a GreenMove route.");

            void Reject(Action block, Action restore)
            {
                Check(first.TryHoldAutomaticMovement() && second.TryHoldAutomaticMovement(), "Failed to set up two owners.");
                block();
                try
                {
                    Check(!first.TryHoldAutomaticMovement(), "Blocked movement accepted a hold.");
                    Check(Flag("RuntimeMovementHeld"), "A blocked script removed another owner's hold.");
                    second.ReleaseAutomaticMovement();
                    Check(!Flag("RuntimeMovementHeld") && !Flag("RuntimeMovementRequested"), "A blocked script retained its old hold.");
                }
                finally
                {
                    restore();
                }
            }

            Reject(() => settings.EnableSmartMovement = false, () => settings.EnableSmartMovement = true);
            Reject(() => allowed.SetValue(setting, false), () => allowed.SetValue(setting, true));
            Reject(() => replay.SetValue(manager, true), () => replay.SetValue(manager, false));
            legacySetting.EnableSafeZone = true;
            Reject(() => legacySetting.EnableAutoMovement = true, () => legacySetting.EnableAutoMovement = false);
            Reject(() => SetField(legacy, "_hasControl", true), () => SetField(legacy, "_hasControl", false));
            if (reborn != null)
            {
                rebornSetting.EnableSafeZone = true;
                Reject(() => rebornSetting.EnableAutoMovement = true, () => rebornSetting.EnableAutoMovement = false);
                Reject(() => SetField(reborn, "_hasControl", true), () => SetField(reborn, "_hasControl", false));
            }
            Console.WriteLine("PASS script movement: hold ownership, cleanup, master switch, replay and PR control gates");
        }
        finally
        {
            Cleanup((object)first);
            Cleanup((object)second);
            settings.EnableSmartMovement = previousEnabled;
            allowed.SetValue(setting, previousAllowed);
            replay.SetValue(manager, previousReplay);
            instance.SetValue(null, previousInstance);
        }
    }
}
