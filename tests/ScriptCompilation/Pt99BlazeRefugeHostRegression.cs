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
        var greenMove = CatalogServiceProxy.Create<IGreenMoveSystem>((name, _) =>
            throw new InvalidOperationException("Host registration must not execute movement: " + name));
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
            var hostType = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
            var host = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(hostType).Invoke(null, [(Func<string, object?[], object?>)((name, _) => name switch
                {
                    "get_CurrentPlayer" => pc, "get_IsReplay" => replay, "get_CurrentTime" => now,
                    "get_SafeZone" => safeZone, "get_Draw" => draw, "Log" => null,
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
