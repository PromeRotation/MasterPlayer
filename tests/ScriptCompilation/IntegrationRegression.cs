using System.Reflection;
using System.Text.Json;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.GreenMoveSystem;

internal static class IntegrationRegression
{
    public static void Run(Assembly assembly)
    {
        var services = new[] { nameof(Svc.PluginInterface), nameof(Svc.ClientState), nameof(Svc.Objects), nameof(Svc.DtrBar), nameof(Svc.Chat) }
            .ToDictionary(name => name, name => typeof(Svc).GetProperty(name)!.GetValue(null));
        var config = Svc.PluginInterface.ConfigDirectory;
        var funcs = new Dictionary<string, Delegate>();
        Action<bool>? setEnabled = null;
        Action<DtrInteractionEvent>? click = null;
        var dtrText = "";
        var dtrDisposed = 0;
        var notices = new List<bool>();
        var snapshots = new List<string>();
        string State() => ((Func<string>)funcs["PromeRadar.GetState"])();
        bool Enabled() => ((Func<bool>)funcs["PromeRadar.GetSmartMovementEnabled"])();
        ICallGateProvider<T> Query<T>(string channel) => CatalogServiceProxy.Create<ICallGateProvider<T>>((name, args) =>
        {
            if (name == "RegisterFunc") funcs[channel] = (Delegate)args[0]!;
            else if (name == "UnregisterFunc") funcs.Remove(channel);
            else throw new NotSupportedException(name);
            return null;
        });
        var stateChannel = CatalogServiceProxy.Create<ICallGateProvider<string, object>>((name, args) =>
        {
            if (name == "SendMessage") snapshots.Add((string)args[0]!);
            else throw new NotSupportedException(name);
            return null;
        });
        var enabledChannel = CatalogServiceProxy.Create<ICallGateProvider<bool, object>>((name, args) =>
        {
            if (name == "RegisterAction") setEnabled = (Action<bool>)args[0]!;
            else if (name == "UnregisterAction") setEnabled = null;
            else if (name == "SendMessage")
            {
                var value = (bool)args[0]!;
                using var json = JsonDocument.Parse(State());
                Check(json.RootElement.GetProperty("SmartMovementEnabled").GetBoolean() == value && Enabled() == value,
                    "The notification exposed a stale state snapshot.");
                notices.Add(value);
            }
            else throw new NotSupportedException(name);
            return null;
        });
        var entry = CatalogServiceProxy.Create<IDtrBarEntry>((name, args) =>
        {
            if (name == "set_OnClick") click = (Action<DtrInteractionEvent>?)args[0];
            else if (name == "set_Text") dtrText = args[0]!.ToString()!;
            else if (name == "Dispose") dtrDisposed++;
            else if (name is not ("set_Shown" or "set_Tooltip")) throw new NotSupportedException(name);
            return null;
        });
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var previous = (bool)settings.EnableSmartMovement;
        IDisposable? runtime = null;
        IDisposable? activation = null;
        IDisposable? integration = null;
        try
        {
            Set(nameof(Svc.PluginInterface), CatalogServiceProxy.Create<IDalamudPluginInterface>((name, args) => name switch
            {
                "get_ConfigDirectory" => config,
                "GetIpcProvider" => ((string)args[0]!) switch
                {
                    "PromeRadar.GetApiVersion" => Query<int>((string)args[0]!),
                    "PromeRadar.IsReady" or "PromeRadar.GetSmartMovementEnabled" => Query<bool>((string)args[0]!),
                    "PromeRadar.GetState" => Query<string>((string)args[0]!),
                    "PromeRadar.SetSmartMovementEnabled" or "PromeRadar.SmartMovementChanged" => enabledChannel,
                    "PromeRadar.StateChanged" => stateChannel,
                    _ => throw new NotSupportedException((string)args[0]!),
                },
                _ => throw new NotSupportedException(name),
            }));
            Set(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name == "get_TerritoryType" ? 1036u : throw new NotSupportedException(name)));
            Set(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) => name == "get_LocalPlayer" ? null : throw new NotSupportedException(name)));
            Set(nameof(Svc.DtrBar), CatalogServiceProxy.Create<IDtrBar>((name, _) => name == "Get" ? entry : throw new NotSupportedException(name)));
            Set(nameof(Svc.Chat), CatalogServiceProxy.Create<IChatGui>((name, _) => name == "Print" ? null : throw new NotSupportedException(name)));
            settings.EnableSmartMovement = true;
            var pluginType = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!;
            var plugin = Activator.CreateInstance(pluginType)!;
            var movement = CatalogServiceProxy.Create<IGreenMoveSystem>((name, _) => name == "get_IsRunning" ? false : throw new NotSupportedException(name));
            runtime = (IDisposable)Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZoneRuntime")!, BindingFlags.Instance | BindingFlags.NonPublic,
                null, [movement, (Func<bool>)(() => false), (Func<bool>)(() => false)], null)!;
            var setting = runtime.GetType().GetProperty("Setting")!.GetValue(runtime)!;
            activation = (IDisposable)Activator.CreateInstance(assembly.GetType("MasterPlayer.Settings.SafeZoneActivation")!, [setting, null])!;
            activation.GetType().GetMethod("Update")!.Invoke(activation, [true, false, true, true]);
            pluginType.GetField("_safeZone", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(plugin, runtime);
            pluginType.GetField("_safeZoneActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(plugin, activation);
            pluginType.GetField("_smartMovementEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(plugin, true);
            var command = pluginType.GetMethod("OnPanelCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
            command.Invoke(plugin, ["/prpr", "ai off"]);
            Check(!(bool)settings.EnableSmartMovement, "The ai off command did not apply the shared movement policy.");
            command.Invoke(plugin, ["/prpr", "ai invalid"]);
            Check(!(bool)settings.EnableSmartMovement, "Invalid AI arguments changed the movement switch.");
            command.Invoke(plugin, ["/prpr", " AI   ON "]);
            Check((bool)settings.EnableSmartMovement, "The ai on command did not accept spacing/case variations.");
            var integrationType = assembly.GetType("MasterPlayer.Integration.SmartMovementIntegration")!;
            integration = (IDisposable)Activator.CreateInstance(integrationType)!;
            var update = integrationType.GetMethod("Update")!;
            var apply = integrationType.GetMethod("ApplyRequests")!;
            Check(!((Func<bool>)funcs["PromeRadar.IsReady"])(), "IPC became ready before its first snapshot.");
            update.Invoke(integration, [plugin]);
            Check(((Func<bool>)funcs["PromeRadar.IsReady"])() && Enabled() && dtrText.Contains("开"), "Initial IPC/DTR state disagrees.");
            setEnabled!(false);
            Check((bool)settings.EnableSmartMovement, "IPC setter mutated game state before Framework applied the request.");
            apply.Invoke(integration, [plugin]); update.Invoke(integration, [plugin]);
            Check(!(bool)settings.EnableSmartMovement && !Enabled() && dtrText.Contains("关"), "IPC off did not update settings and DTR.");
            var count = notices.Count;
            setEnabled!(false); apply.Invoke(integration, [plugin]); update.Invoke(integration, [plugin]);
            Check(notices.Count == count, "An unchanged setting emitted another change notification.");
            click!(new DtrInteractionEvent { ClickType = MouseClickType.Right });
            apply.Invoke(integration, [plugin]);
            Check(!(bool)settings.EnableSmartMovement, "Right click unexpectedly toggled intelligent movement.");
            click!(new DtrInteractionEvent { ClickType = MouseClickType.Left });
            apply.Invoke(integration, [plugin]); update.Invoke(integration, [plugin]);
            Check(Enabled() && (bool)settings.EnableSmartMovement && notices.SequenceEqual([true, false, true]), "DTR did not use the same switch and notifications.");
            integration.Dispose(); integration.Dispose(); integration = null;
            Check(funcs.Count == 0 && setEnabled == null && click == null && dtrDisposed == 1, "IPC/DTR resources leaked across hot reload.");
            using var finalState = JsonDocument.Parse(snapshots.Last());
            Check(!finalState.RootElement.GetProperty("Ready").GetBoolean() && !finalState.RootElement.GetProperty("CanMove").GetBoolean()
                && finalState.RootElement.GetProperty("SmartMovementEnabled").GetBoolean(), "Unload lost the state schema or changed the user's choice.");
            Console.WriteLine("PASS production IPC/DTR: queued setter, consistent snapshots, shared toggle, unchanged state and hot-reload cleanup");
        }
        finally
        {
            integration?.Dispose(); activation?.Dispose(); runtime?.Dispose();
            settings.EnableSmartMovement = previous;
            foreach (var service in services) Set(service.Key, service.Value);
        }
    }

    private static void Set(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
