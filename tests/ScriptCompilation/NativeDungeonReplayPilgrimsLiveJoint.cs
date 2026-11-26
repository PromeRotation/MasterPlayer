using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private delegate void JointCapture(ulong source, uint action, ulong mainTarget, uint sequence,
        ReadOnlySpan<ulong> targets, DateTime? at, Vector3? targetPosition = null, ulong[][]? rawEffects = null,
        byte? actionType = null, uint? sourceOid = null, int? sourceObjectIndex = null, ushort? rotationInt = null);

    // Real live subscriptions, manager suppression, host ownership and output facades;
    // only world sampling is replaced by recorded DTOs (no native game process here).
    private static void RunPilgrimsLiveJoint(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        controller.StopSession();
        const string id = "masterplayer.pilgrims.floors";
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags all = hidden | BindingFlags.Public;
        var assembly = managerObject.GetType().Assembly;
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, all) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, all) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        void Invoke(object obj, string name, params object?[] args)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
                if (type.GetMethod(name, all | BindingFlags.DeclaredOnly) is { } method)
                {
                    method.Invoke(obj, args);
                    return;
                }
            throw new MissingMethodException(name);
        }
        void SetService(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
        var oldClient = Svc.ClientState;
        var oldObjects = Svc.Objects;
        var oldData = Svc.Data;
        var oldCondition = Svc.Condition;
        var oldChat = Svc.Chat;
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var pluginType = plugin.GetType();
        var oldCapture = pluginType.GetField("nativeActionEffects", hidden)!.GetValue(plugin);
        var oldHost = pluginType.GetField("_mechanicHost", hidden)!.GetValue(plugin);
        var pr = PromeRotation.Plugin.Instance;
        var oldLog = pr.LogSystem;
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var saved = ((bool)settings.EnableRadar, (bool)settings.EnableRadarOverlay,
            (bool)settings.EnableSmartMovement, (float)settings.DodgeDangerMargin);
        using var game = new Lumina.GameData(Environment.GetEnvironmentVariable("FFXIV_SQPACK")!,
            new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        var callbacks = new List<Delegate>();
        object? context = null;
        IDisposable? dodge = null;
        IDisposable? capture = null;
        try
        {
            Check(!(bool)Read(managerObject, "ReplayModeActive") && Read((object)runtime, "ReplayClock") == null,
                "Joint test must use the real live mode and clock.");
            SetService(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name switch
            {
                "get_TerritoryType" => 1287u,
                "get_IsPvP" => false,
                _ => throw new NotSupportedException(name),
            }));
            SetService(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) =>
                name == "SearchByEntityId" ? null : throw new NotSupportedException(name)));
            SetService(nameof(Svc.Condition), CatalogServiceProxy.Create<ICondition>((name, _) =>
                name == "get_Item" ? false : throw new NotSupportedException(name)));
            SetService(nameof(Svc.Data), new PalaceGameData(game));
            SetService(nameof(Svc.Chat), CatalogServiceProxy.Create<IChatGui>((_, _) => null));
            settings.EnableRadar = true;
            settings.EnableRadarOverlay = false;
            settings.EnableSmartMovement = false;
            settings.DodgeDangerMargin = 0f;
            var log = RuntimeHelpers.GetUninitializedObject(typeof(LogSystemService));
            typeof(LogSystemService).GetField("<Events>k__BackingField", hidden)!.SetValue(log,
                CatalogServiceProxy.Create<ILogSystemEventSource>((name, args) =>
                {
                    if (name != "Subscribe") throw new NotSupportedException(name);
                    var callback = (Delegate)args[0]!;
                    callbacks.Add(callback);
                    return new JointSubscription(() => callbacks.Remove(callback));
                }));
            typeof(PromeRotation.Plugin).GetProperty("LogSystem")!.SetValue(pr, log);
            var host = RuntimeHelpers.GetUninitializedObject(assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!);
            pluginType.GetField("_mechanicHost", hidden)!.SetValue(plugin, host);
            Check(!(bool)Read(host, "ModulesEnabled") && !(bool)Read(host, "HasActiveModule"),
                "PT native territory must disable the actual bridge before its empty session shortcut.");
            var captureType = assembly.GetType("MasterPlayer.Scripting.NativeActionEffectCapture")!;
            capture = (IDisposable)Activator.CreateInstance(captureType, hidden, null, [false], null)!;
            pluginType.GetField("nativeActionEffects", hidden)!.SetValue(plugin, capture);
            var emit = captureType.GetMethod("Capture", hidden)!.CreateDelegate<JointCapture>(capture);
            dodge = (IDisposable)Activator.CreateInstance(assembly.GetType("MasterPlayer.Dodge.DodgeController")!)!;
            void Publish(LogSystemEvent ev)
            {
                foreach (var callback in callbacks.ToArray())
                    if (callback.GetType().GenericTypeArguments[0].IsInstanceOfType(ev))
                        callback.DynamicInvoke(ev);
            }
            var worldType = assembly.GetType("MasterPlayer.Replay.ReplayWorldState")!;
            var world = Activator.CreateInstance(worldType)!;
            var now = DateTime.UtcNow;
            void Frame(uint? action = null, uint oid = 0x4923)
            {
                var cast = action == null ? null : new
                {
                    ActionId = action.Value, TargetId = 100ul, Location = Vector3.Zero, Rotation = -MathF.PI / 2,
                    StartedAt = now, FinishAt = now.AddSeconds(5), TotalTime = 4.7f, IsSpell = true,
                };
                var json = JsonSerializer.Serialize(new
                {
                    Full = true, Territory = 1287, LocalPlayerId = 100ul,
                    DeepDungeon = new { DungeonId = 4, Floor = 61, Tileset = 0, Rooms = Enumerable.Repeat((ushort)128, 25).ToArray() },
                    Party = new[] { new { Slot = 0, EntityId = 100ul, Role = "None" } },
                    Actors = new object[]
                    {
                        new { Id = 100ul, Kind = 1, Position = new { X = 0, Y = 0, Z = 5 }, Hp = 100, MaxHp = 100 },
                        new { Id = 10ul, Oid = oid, Kind = 2, SubKind = 5, ObjectIndex = 10,
                            Position = new { X = 0, Y = 0, Z = 0 }, Rotation = 0f, Targetable = true,
                            InCombat = true, IsEnemy = true, Hp = 100, MaxHp = 100, Cast = cast },
                    },
                }, new JsonSerializerOptions { IncludeFields = true });
                Invoke(world, "Apply", JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!)!);
                worldType.GetProperty("CurrentTime")!.SetValue(world, now);
            }
            Frame();
            var descriptor = ((IEnumerable)manager.Scripts).Cast<object>().Single(d => (string)Read(Read(d, "Metadata"), "Id") == id);
            var script = descriptor.GetType().GetMethod("CreateInstance")!.Invoke(descriptor, null)!;
            var configuration = managerObject.GetType().GetMethod("CreateConfiguration", hidden)!.Invoke(managerObject, [descriptor, script])!;
            context = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripting.MasterPlayerScriptContext")!,
                hidden, null, [managerObject, Read(descriptor, "Metadata"), Read(configuration, "Settings"), null], null)!;
            var hostType = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
            var hostProxy = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(hostType).Invoke(null, [(Func<string, object?[], object?>)((name, args) => name switch
                {
                    "get_ReplayWorld" => world,
                    "get_CurrentTime" => now,
                    "get_CurrentPlayer" => Read(world, "LocalPlayer"),
                    "get_CurrentPlayerId" => 100ul,
                    "get_IsReplay" => false,
                    _ => context.GetType().GetMethods().Single(m => m.Name == name && !m.ContainsGenericParameters)
                        .Invoke(context, args),
                })])!;
            script.GetType().GetProperty("Host", all)!.SetValue(script, hostProxy);
            var running = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripting.RunningScript")!,
                hidden, null, [descriptor, script, context, Read(configuration, "Settings"), false], null)!;
            ((IDictionary)Read(managerObject, "_running"))[id] = running;
            Invoke(script, "StartInternal");
            var sequences = (IDictionary)Read(dodge, "_pilgrimsFollowUps");
            var handles = (IList)Read(dodge, "_registeredHandles");
            string[] FloorKeys() => ((IDictionary)Read(script, "output")).Keys.Cast<string>().ToArray();
            void Cast(uint action, uint oid = 0x4923)
            {
                Frame(action, oid);
                Publish(new LogSystemCastStartEvent
                {
                    SourceId = 10, ActionId = (ushort)action, CastTime = 4.7f, Timestamp = now,
                    SourcePosition = Vector3.Zero, EffectPosition = Vector3.Zero, SourceRotation = 2f,
                    Raw = Activator.CreateInstance(assembly.GetType("MasterPlayer.Replay.ReplayCastData")!, [-MathF.PI / 2]),
                });
            }
            void Effect(uint action, uint sequence, ulong source = 10, int targetIndex = 0)
            {
                emit(source, action, 100, sequence, [100, 101], now, null, null, 1, 0x4923, 10);
                Publish(new LogSystemActionEffectEvent
                {
                    SourceId = source, ActionId = action, GlobalSequence = sequence,
                    Timestamp = now, TargetIndex = (ushort)targetIndex,
                });
            }
            Cast(42214);
            Check(!manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 10, ActionId = 42214 })
                && handles.Count == 1 && FloorKeys().Contains("cast/10/42214"),
                "Real live first cast must pass manager/host gates and register both consumers.");
            Frame();
            Effect(42214, 41);
            Check(sequences.Count == 1 && FloorKeys().Any(k => k.StartsWith("zone/void"))
                && handles.Count == 2, "Native capture and expanded first effect must publish both live outputs.");
            var followHandle = sequences[10ul]!.GetType().GetField("Item7")!.GetValue(sequences[10ul])!;
            Check((bool)runtime.Manager.ContainsArea((dynamic)followHandle), "Dodge follow-up is absent from the shared production registry.");
            dynamic[] shared = ((IEnumerable)runtime.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas).Cast<dynamic>().ToArray();
            Check(shared.Length >= 2 && shared.Count(a => a.Geometry.GetType().Name == "FanGeometry"
                && (bool)a.Geometry.Contains(new Vector3(20, 0, 0), 0f)
                && !(bool)a.Geometry.Contains(new Vector3(-20, 0, 0), 0f)) >= 2,
                "Both independent live consumers must publish the opposite R30 half-plane.");
            Publish(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 42214, GlobalSequence = 41, Timestamp = now, TargetIndex = 1 });
            Check(handles.Count == 2, "Expanded multi-target first effect duplicated the live follow-up.");
            Effect(42217, 42, 99);
            Check(sequences.Count == 1, "Another source consumed the live sequence.");
            Effect(42217, 42);
            Check(sequences.Count == 0 && !FloorKeys().Any(k => k.StartsWith("zone/void")),
                "Actual second effect must clear both live consumers.");
            Cast(42091, 0x4922);
            Check(manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 10, ActionId = 42091 })
                && handles.Count == 0 && FloorKeys().Any(k => k.StartsWith("zone/sector")),
                "Floor-owned cast must suppress only the generic registration through the actual manager.");
            Cast(44759);
            Frame();
            Effect(44759, 51);
            Check(sequences.Count == 1, "Live heel first effect failed to arm the three-hit follow-up.");
            for (var packet = 0; packet < 3; ++packet)
            {
                now = now.AddTicks(1);
                Effect(44760, 0);
                Publish(new LogSystemActionEffectEvent
                {
                    SourceId = 10, ActionId = 44760, GlobalSequence = 0, Timestamp = now, TargetIndex = 1,
                });
                Check(sequences.Count == (packet < 2 ? 1 : 0),
                    $"Live heel zero-sequence packet {packet + 1} must consume exactly one complete hit.");
            }
            settings.EnableRadar = false;
            Cast(42214);
            Check(handles.Count == 0, "Disabled display/movement gates accepted a real live cast.");
            settings.EnableRadar = true;
            managerObject.GetType().GetMethod("EnterReplayMode", hidden)!.Invoke(managerObject, null);
            Cast(42214);
            Check(handles.Count == 0, "Replay mode accepted a real live cast.");
            managerObject.GetType().GetMethod("ExitReplayMode", hidden)!.Invoke(managerObject, null);
            manager.StopScript(id);
            ((IDisposable)context).Dispose();
            context = null;
            dodge.Dispose();
            dodge = null;
            Check(callbacks.Count == 0 && dangers() == 0 && previews().Length == 0,
                "Joint live owner cleanup leaked subscriptions or output.");
            Console.WriteLine("PASS production PT live event joint routing: real subscriptions manager/host gates native-before-expanded effects shared registry first/follow geometry three zero-sequence heel packets target dedup suppression controls owner; DTO sampling only");
            PilgrimsFollowUpRegression.Run(assembly, runtimeObject);
            PilgrimsFollowUpRegression.Run(assembly, runtimeObject, "dive");
            PilgrimsFollowUpRegression.Run(assembly, runtimeObject, "water");
        }
        finally
        {
            manager.StopScript(id);
            (context as IDisposable)?.Dispose();
            dodge?.Dispose();
            capture?.Dispose();
            settings.EnableRadar = saved.Item1;
            settings.EnableRadarOverlay = saved.Item2;
            settings.EnableSmartMovement = saved.Item3;
            settings.DodgeDangerMargin = saved.Item4;
            pluginType.GetField("nativeActionEffects", hidden)!.SetValue(plugin, oldCapture);
            pluginType.GetField("_mechanicHost", hidden)!.SetValue(plugin, oldHost);
            typeof(PromeRotation.Plugin).GetProperty("LogSystem")!.SetValue(pr, oldLog);
            SetService(nameof(Svc.ClientState), oldClient);
            SetService(nameof(Svc.Objects), oldObjects);
            SetService(nameof(Svc.Data), oldData);
            SetService(nameof(Svc.Condition), oldCondition);
            SetService(nameof(Svc.Chat), oldChat);
            runtime.Manager.ClearAllAreas();
        }
    }

    private sealed class JointSubscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
