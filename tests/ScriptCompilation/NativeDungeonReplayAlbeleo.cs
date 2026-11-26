using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCLL2Albeleo(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string name = "妖术师 阿尔贝雷欧";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(80, 0, -376.45502f);
        object? Read(object obj, string key)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(key, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(key, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, key);
        }
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(e => (bool)Read(e, "IsAlbeleo")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility")!;
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")!).Contains(key)).ToArray();
        object Boss(uint hp = 1000)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x2F06, center, combat: true, subKind: 5, isEnemy: true, objectIndex: 2))!;
            node["Hp"] = hp; return node;
        }
        object Player(Vector3? position = null) => Actor(100, 0, position ?? center);
        object Helper(uint action = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(20, 0x2F09, center, subKind: 5, isEnemy: true, objectIndex: 20))!;
            node["Name"] = "Named caster";
            if (action != 0)
            {
                var spell = JsonSerializer.SerializeToNode(Cast(action, 5, center, .4f, 100, 0))!;
                spell["IsSpell"] = true; spell["TotalTime"] = 5; node["Cast"] = spell;
            }
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(920, at, actors, full, party: [new { Slot = 0, EntityId = 100UL }], sourcePlayerId: 100);
        string Effect(double at, uint action) => JsonSerializer.Serialize(new {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                TargetPosition = Pose(center), Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CLL2 actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")!, "CLL2 replay no executable PR requests");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL2 owner cleanup");
        }
        void Solver()
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "CLL2 real solver corridor");
        }
        foreach (var action in new uint[] { 21503, 21505, 21502, 21506, 21508, 21510 })
        {
            var aoe = action is 21503 or 21505 or 21502 or 21506;
            load(id, [Header(920), World(0, [Boss(), Player(), Helper(action)], true),
                Effect(1, action), World(2, [Helper()]), World(3, [])]);
            Seek(.3);
            Check((bool)Read(Model(), "Active")! && Items(Read(Model(), "Definition")!, "Rules").Length == 6, "CLL2 six actual components");
            Check(Shown("bozja/aoe/").Length == (aoe ? 1 : 0) && dangers() == (aoe ? 1 : 0), "CLL2 production shape " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "CLL2 NPC suppression");
            if (aoe) Solver();
            if (action == 21508) Check(Shown("bozja/eye/").Length == 1 && Lines().Length > 0, "CLL2 gaze eye and arc");
            if (action == 21510) Check((string)Read(Script(), "MechanicHintsText")! == "Interrupt!"
                && Items(Script(), "InterruptTargetIds").Length == 1, "CLL2 fixed interrupt hint and diagnostic");
            Seek(1.1);
            Check(action == 21508 ? Items(Model(), "Eyes").Length == 1 : Items(Model(), "Casts").Length == 1,
                "CLL2 Effect retains cast output");
            Seek(2.1);
            Check(Items(Model(), "Casts").Length == 0 && Items(Model(), "Eyes").Length == 0 && dangers() == 0, "CLL2 actual Finish");
            Seek(.3); manager.SetMechanismEnabled(id, name, false); Seek(.4);
            Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "CLL2 mechanism disabled");
            manager.SetMechanismEnabled(id, name, true); Seek(.5); Release();
            Console.WriteLine("PASS native CLL2 actual DLL component solver Finish owner replay isolation " + action);
        }
        foreach (var distance in new[] { 35f, MathF.BitIncrement(35f) })
        {
            load(id, [Header(920), World(0, [Boss(), Player(center - Vector3.UnitX * distance), Helper(21508)], true),
                World(5, [])]);
            Seek(.3);
            Check(Shown("bozja/eye/").Length == 1 && Shown("bozja/pupil/").Length == 1
                && (Lines().Length > 0) == (distance <= 35), "CLL2 finite range retains eye only outside");
            Release(); Console.WriteLine("PASS native CLL2 actual finite range eye arc " + distance);
        }
        load(id, [Header(920), World(0, [Boss(), Player(), Helper(21503)], true),
            World(1, [Boss(0)]), World(2, [Boss(1)]), World(3, [Boss()]), World(4, [])]);
        Seek(1.1); Check((bool)Read(Model(), "Active")! && dangers() == 1, "CLL2 HP0 alone retained");
        Seek(2.1); Check(!(bool)Read(Model(), "Active")! && (bool)Read(Model(), "Completed")!
            && previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL2 HP1 all output retired");
        Seek(3.1); Check(!(bool)Read(Model(), "Active")!, "CLL2 same primary recovered HP no repull");
        Release(); Console.WriteLine("PASS native CLL2 actual HP0 alive HP1 retreat recovered original primary no repull");
        var dyingAdd = JsonSerializer.SerializeToNode(Helper())!;
        dyingAdd["Hp"] = 1;
        load(id, [Header(920), World(0, [Boss(), Player(), Helper()], true),
            World(1, [dyingAdd]), World(2, [])]);
        Seek(.3); Check((ulong)Read(Script(), "PlannedTargetId")! == 20, "CLL2 actual script plans legal add over boss");
        Seek(1.1); Check((ulong)Read(Script(), "PlannedTargetId")! == 1, "CLL2 PendingDead add falls back to boss");
        Release(); Console.WriteLine("PASS native CLL2 actual target plan legal add PendingDead fallback replay executable requests absent");
    }
}
