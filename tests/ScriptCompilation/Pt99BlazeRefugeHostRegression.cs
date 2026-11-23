using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.GreenMoveSystem;

internal static class Pt99BlazeRefugeHostRegression
{
    public static void Run(Assembly assembly)
    {
        const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        var pluginType = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!;
        var instance = pluginType.GetProperty("Instance")!;
        var previous = instance.GetValue(null);
        var prInstance = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var previousPr = prInstance.GetValue(null);
        var previousTargets = Svc.Targets;
        var previousClient = Svc.ClientState;
        var previousObjects = Svc.Objects;
        var previousInterface = Svc.PluginInterface;
        var config = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "test-artifacts", "refuge-host-" + Guid.NewGuid().ToString("N")));
        PromeRotation.Spatial.Drawing.DrawManager? drawManager = null;
        var greenRunning = false;
        var greenStops = 0;
        var greenMove = CatalogServiceProxy.Create<IGreenMoveSystem>((name, _) =>
        {
            if (name == "get_IsRunning") return greenRunning;
            if (name == "Stop") { ++greenStops; greenRunning = false; return null; }
            throw new InvalidOperationException("Host registration must not execute movement: " + name);
        });
        dynamic runtime = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZoneRuntime")!,
            PRIVATE, null, [greenMove, (Func<bool>)(() => false), (Func<bool>)(() => false)], null)!;
        using var runtimeLifetime = (IDisposable)runtime;
        try
        {
            typeof(Svc).GetProperty(nameof(Svc.PluginInterface))!.SetValue(null,
                CatalogServiceProxy.Create<IDalamudPluginInterface>((name, _) => name switch
                {
                    "get_ConfigDirectory" => config,
                    "get_AssemblyLocation" => new FileInfo(typeof(PromeRotation.Plugin).Assembly.Location),
                    _ => throw new NotSupportedException(name)
                }));
            typeof(Svc).GetProperty(nameof(Svc.ClientState))!.SetValue(null,
                CatalogServiceProxy.Create<IClientState>((name, _) => name switch
                {
                    "add_TerritoryChanged" or "remove_TerritoryChanged" => null,
                    "get_TerritoryType" => 1333u,
                    _ => throw new NotSupportedException(name)
                }));
            drawManager = new PromeRotation.Spatial.Drawing.DrawManager();
            var plugin = Activator.CreateInstance(pluginType)!;
            pluginType.GetField("_safeZone", PRIVATE)!.SetValue(plugin, (object)runtime);
            instance.SetValue(null, plugin);
            var pr = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            typeof(PromeRotation.Plugin).GetField("_drawManager", PRIVATE)!.SetValue(pr, drawManager);
            prInstance.SetValue(null, pr);
            typeof(Svc).GetProperty(nameof(Svc.Targets))!.SetValue(null,
                CatalogServiceProxy.Create<ITargetManager>((name, _) => name == "get_Target" ? null : throw new NotSupportedException(name)));
            runtime.Setting.EnableSafeZone = true;
            object setting = runtime.Setting;
            var allowed = setting.GetType().GetProperty("MovementAllowed", PRIVATE)!;
            allowed.SetValue(setting, true);
            var prefer = setting.GetType().GetProperty("RuntimePreferSafeRefuge", PRIVATE)!;
            var now = DateTime.UtcNow;
            var actorType = assembly.GetType("MasterPlayer.Replay.ReplayActorState")!;
            object Actor(ulong id, uint oid, bool player = false)
            {
                var actor = Activator.CreateInstance(actorType)!;
                foreach (var (key, value) in new (string, object)[] {
                    ("Id", id), ("Oid", oid), ("Kind", (byte)(player ? 1 : 2)),
                    ("Position", new Vector3(-600, 0, -300)), ("Hp", 1000u), ("MaxHp", 1000u), ("InCombat", true) })
                    actorType.GetProperty(key)!.SetValue(actor, value);
                return actor;
            }
            var pc = Actor(100, 0, true);
            var script = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripts.PilgrimsTraverse.PilgrimsTraverse99")!)!;
            var scriptType = script.GetType().BaseType!;
            var model = scriptType.GetField("encounter", PRIVATE)!.GetValue(script)!;
            var actors = Array.CreateInstance(actorType, 3);
            actors.SetValue(Actor(1, 0x48EA), 0);
            actors.SetValue(pc, 1);
            actors.SetValue(Actor(10, 0x1EBE70), 2);
            model.GetType().GetMethod("Synchronize")!.Invoke(model, [actors, now]);
            var castType = assembly.GetType("MasterPlayer.Mechanics.PilgrimsTraverse.PtBossEncounter+Cast")!;
            var cast = Activator.CreateInstance(castType,
                [10ul, 44079u, new Vector3(-600, 0, -300), 0f, now, now.AddSeconds(7), 0ul, null])!;
            model.GetType().GetMethod("StartCast")!.Invoke(model, [cast]);
            object Create(string type, params object[] args) => Activator.CreateInstance(
                assembly.GetType("MasterPlayer.Scripting." + type)!, PRIVATE, null, args, null)!;
            var safeZone = Create("ScriptSafeZoneApi", "refuge-host-test", "Refuge host test");
            var draw = Create("ScriptDrawApi", "refuge-host-test", safeZone);
            var replay = false;
            var hints = Create("ScriptHintsApi", (Func<bool>)(() => replay), null!, null!, null!, null!);
            var hostType = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
            var host = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(hostType).Invoke(null, [(Func<string, object?[], object?>)((name, _) => name switch
                {
                    "get_CurrentPlayer" => pc, "get_IsReplay" => replay, "get_CurrentTime" => now, "get_ReplayWorld" => null,
                    "get_SafeZone" => safeZone, "get_Draw" => draw, "get_Hints" => hints, "Log" => null,
                    _ => throw new NotSupportedException(name)
                })])!;
            scriptType.BaseType!.GetProperty("Host")!.SetValue(script, host);
            var render = scriptType.GetMethod("Render", PRIVATE)!;
            void Render() => render.Invoke(script, [now]);
            Render();
            Check((bool)prefer.GetValue(setting)!, "Live PT99 must request safe refuge for the whole blaze round");
            Check(((IEnumerable)runtime.Manager.BuildSnapshot(now).GoalAreas).Cast<object>().Count() == 1,
                "Live host must publish the whole-round goal");
            var followType = assembly.GetType("MasterPlayer.Follow.FollowController")!;
            using var follow = (IDisposable)Activator.CreateInstance(followType)!;
            dynamic circle = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.CircleTemplate")!, [18.6f])!;
            dynamic binding = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.StaticTransform")!,
                [new Vector3(-600, 0, -315), 0f])!;
            dynamic followHandle = ((dynamic)safeZone).AddPersistentSafeArea(circle, binding, -1);
            followType.GetField("_goalManager", PRIVATE)!.SetValue(follow, (object)runtime.Manager);
            followType.GetField("_followGoal", PRIVATE)!.SetValue(follow, (object)followHandle);
            followType.GetMethod("Update")!.Invoke(follow, null);
            Check(!(bool)runtime.Manager.ContainsArea(followHandle), "The actual follow controller must withdraw its pre-existing approach goal");
            replay = true;
            Render();
            Check(!(bool)prefer.GetValue(setting)!, "Replay must release the live movement request");
            replay = false;
            allowed.SetValue(setting, false);
            Render();
            Check(!(bool)prefer.GetValue(setting)!, "Movement disable must release the live movement request");
            allowed.SetValue(setting, true);
            Render();
            Check((bool)prefer.GetValue(setting)!, "Re-enabled movement must retain blaze refuge ownership");
            now = now.AddSeconds(30);
            Render();
            Check(!(bool)prefer.GetValue(setting)!
                && !((IEnumerable)runtime.Manager.BuildSnapshot(now).GoalAreas).Cast<object>().Any(),
                "Expiry must release refuge ownership and the goal");
            var player = CatalogServiceProxy.Create<IBattleChara>((name, _) =>
                name == "get_IsCasting" ? false : throw new NotSupportedException(name));
            var target = CatalogServiceProxy.Create<IBattleNpc>((name, _) => name switch
            {
                "get_EntityId" => 2u,
                "get_Position" => new Vector3(-600, 0, -315),
                _ => throw new NotSupportedException(name)
            });
            typeof(Svc).GetProperty(nameof(Svc.Objects))!.SetValue(null,
                CatalogServiceProxy.Create<IObjectTable>((name, _) => name switch
                {
                    "get_Length" => 1,
                    "get_Item" => target,
                    _ => throw new NotSupportedException(name)
                }));
            followType.GetMethod("UpdatePlannedFollow", PRIVATE)!.Invoke(follow,
                [2u, new Vector3(-600, 0, -315), 18.6f, player]);
            var resumed = followType.GetField("_followGoal", PRIVATE)!.GetValue(follow)!;
            Check((bool)runtime.Manager.ContainsArea((dynamic)resumed), "Optional approach can register again after the round");
            Check(((IEnumerable)runtime.Manager.BuildSnapshot(now).GoalAreas).Cast<object>().Count() == 1,
                "Resumed approach must resolve the live target position");
            scriptType.GetMethod("ClearOutput", PRIVATE)!.Invoke(script, null);
            Check(!(bool)prefer.GetValue(setting)!, "Stopping the script releases refuge ownership");
            Console.WriteLine("PASS production PT99 whole-round refuge host: goal, actual follow withdrawal/resume, replay, movement disable, expiry and cleanup");

            var pt80 = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripts.PilgrimsTraverse.PilgrimsTraverse80")!)!;
            var pt80Type = pt80.GetType().BaseType!;
            pt80Type.BaseType!.GetProperty("Host")!.SetValue(pt80, host);
            var pt80Model = pt80Type.GetField("encounter", PRIVATE)!.GetValue(pt80)!;
            var pt80Actors = Array.CreateInstance(actorType, 2);
            pt80Actors.SetValue(Actor(1, 0x485D), 0);
            pt80Actors.SetValue(pc, 1);
            var statusType = assembly.GetType("MasterPlayer.Replay.ReplayStatus")!;
            void Stacks(ushort stacks, double duration = 30)
            {
                var statuses = Array.CreateInstance(statusType, 1);
                statuses.SetValue(Activator.CreateInstance(statusType, [4519u, stacks, 0ul, now.AddSeconds(duration)]), 0);
                actorType.GetProperty("Statuses")!.SetValue(pc, statuses);
                pt80Model.GetType().GetMethod("Synchronize")!.Invoke(pt80Model, [pt80Actors, now]);
                pt80Type.GetMethod("Render", PRIVATE)!.Invoke(pt80, [now]);
            }
            bool Held() => (bool)setting.GetType().GetProperty("RuntimeMovementHeld", PRIVATE)!.GetValue(setting)!;
            bool Requested() => (bool)setting.GetType().GetProperty("RuntimeMovementRequested", PRIVATE)!.GetValue(setting)!;
            Stacks(5);
            Check(Requested() && !Held(), "Live PT80 held movement below six stacks.");
            object movement = runtime.GetType().GetField("_movement", PRIVATE)!.GetValue((object)runtime)!;
            movement.GetType().GetField("_hasControl", PRIVATE)!.SetValue(movement, true);
            greenRunning = true;
            now = now.AddSeconds(.6);
            Stacks(6);
            Check(Requested() && Held() && !greenRunning && greenStops == 1,
                "Live PT80 failed to stop the owned active GreenMove route at six stacks.");
            replay = true;
            Stacks(6);
            Check(!Requested() && !Held(), "PT80 replay retained its live hold.");
            replay = false;
            allowed.SetValue(setting, false);
            Stacks(6);
            Check(!Requested() && !Held(), "PT80 movement disable retained its hold.");
            allowed.SetValue(setting, true);
            Stacks(6, .1);
            Check(Held(), "PT80 did not restore its hold after movement enable.");
            now = now.AddSeconds(.2);
            pt80Type.GetMethod("Render", PRIVATE)!.Invoke(pt80, [now]);
            Check(Requested() && !Held(), "Expired PT80 status retained its hold.");
            now = now.AddSeconds(.6);
            Stacks(6);
            pt80Type.GetMethod("OnCurrentPlayerChanged", PRIVATE)!.Invoke(pt80, [100ul, 101ul]);
            Check(!Requested() && !Held(), "PT80 perspective change retained the former player's hold.");
            Stacks(6);
            pt80Type.GetMethod("OnStop", PRIVATE)!.Invoke(pt80, null);
            Check(!Requested() && !Held(), "Stopping PT80 retained its hold.");
            Console.WriteLine("PASS production PT80 live movement hold: five/six stacks, replay, master gate, expiry, perspective and stop");

            typeof(Svc).GetProperty(nameof(Svc.Objects))!.SetValue(null,
                CatalogServiceProxy.Create<IObjectTable>((name, _) => name == "GetEnumerator"
                    ? ((IEnumerable<IGameObject>)Array.Empty<IGameObject>()).GetEnumerator()
                    : name == "get_Length" ? 0 : throw new NotSupportedException(name)));
            actorType.GetProperty("Position")!.SetValue(pc, new Vector3(-300, 0, -300));
            var pt10 = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripts.PilgrimsTraverse.PilgrimsTraverse10")!)!;
            var pt10Type = pt10.GetType().BaseType!;
            pt10Type.BaseType!.GetProperty("Host")!.SetValue(pt10, host);
            var pt10Model = pt10Type.GetField("encounter", PRIVATE)!.GetValue(pt10)!;
            var shrublet = Actor(2, 0x460E);
            actorType.GetProperty("Targetable")!.SetValue(shrublet, true);
            var pt10Actors = Array.CreateInstance(actorType, 3);
            pt10Actors.SetValue(Actor(1, 0x460D), 0);
            pt10Actors.SetValue(shrublet, 1);
            pt10Actors.SetValue(pc, 2);
            pt10Model.GetType().GetMethod("Synchronize")!.Invoke(pt10Model, [pt10Actors, now]);
            var spread = Activator.CreateInstance(castType,
                [1ul, 44055u, new Vector3(-300, 0, -300), 0f, now, now.AddSeconds(5), 0ul, null])!;
            pt10Model.GetType().GetMethod("StartCast")!.Invoke(pt10Model, [spread]);
            void Render10() => pt10Type.GetMethod("Render", PRIVATE)!.Invoke(pt10, [now]);
            string? Prediction() => (string?)pt10Type.GetProperty("PredictedDamageText")!.GetValue(pt10);
            Render10();
            Check(Prediction()?.Contains("Raidwide×1") == true, "PT10 lost its persistent prediction diagnostic.");
            pt10Type.GetMethod("OnCurrentPlayerChanged", PRIVATE)!.Invoke(pt10, [100ul, 101ul]);
            Check(Prediction() == null, "PT10 perspective change retained its diagnostic.");
            Render10();
            Check(Prediction() != null, "PT10 did not rebuild its diagnostic for the new perspective.");
            actorType.GetProperty("Dead")!.SetValue(shrublet, true);
            pt10Model.GetType().GetMethod("Synchronize")!.Invoke(pt10Model, [pt10Actors, now]);
            Render10();
            Check(Prediction() == null, "PT10 spread completion retained its diagnostic.");
            pt10Type.GetMethod("OnStop", PRIVATE)!.Invoke(pt10, null);
            Check(Prediction() == null, "Stopping PT10 retained its diagnostic.");
            Console.WriteLine("PASS production PT10 persistent damage diagnostic: one source prediction, perspective rebuild, spread completion and stop");
        }
        finally
        {
            drawManager?.Dispose();
            typeof(Svc).GetProperty(nameof(Svc.ClientState))!.SetValue(null, previousClient);
            typeof(Svc).GetProperty(nameof(Svc.Objects))!.SetValue(null, previousObjects);
            typeof(Svc).GetProperty(nameof(Svc.PluginInterface))!.SetValue(null, previousInterface);
            typeof(Svc).GetProperty(nameof(Svc.Targets))!.SetValue(null, previousTargets);
            prInstance.SetValue(null, previousPr);
            instance.SetValue(null, previous);
            config.Delete();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
