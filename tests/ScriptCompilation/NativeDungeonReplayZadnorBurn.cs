using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorBurn(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "试制型飞行型魔导装甲——黑色燃焰";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-240, 0, -230);
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj)!;
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Items(string member) => ((IEnumerable)Read(Model(), member)).Cast<object>().ToArray();
        object[] Shown(string key = "bozja/aoe/") => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object Spell(uint action, double at = 0, Vector3? pos = null, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, pos ?? center, rotation, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss(object? cast = null) => Actor(1, 0x31A0, center, combat: true, isEnemy: true, cast: cast, subKind: 5);
        object Player(uint status = 0, float rotation = 0, ulong playerId = 100, Vector3? pos = null)
            => Actor(playerId, 0, pos ?? center, rotation: rotation, stackedStatus: status);
        object Escort(ulong source, object? cast = null, bool tracking = false, Vector3? pos = null)
            => Actor(source, 0x32FD, pos ?? center - Vector3.UnitZ * 10, cast: cast, subKind: 5,
                stackedStatus: tracking ? 2056u : 0, targetId: 100);
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(975, at, actors, full, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) },
        });
        string Icon(double at, ulong source) => JsonSerializer.Serialize(new
        {
            t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = 101UL, MarkerId = 261u, Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CE64 replay error: " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CE64 owner release");
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x31A0u])! && !(bool)owns.Invoke(null, [920u, 0x31A0u])!
            && !(bool)owns.Invoke(null, [975u, 0x31A1u])!, "CE64 exact source ownership");
        foreach (var action in new uint[] { 23606, 23615 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Player(playerId: 101, pos: center + Vector3.UnitX * 20),
                Escort(20, Spell(action, pos: center))], true), Effect(1, action), World(20, []),
                World(21, [Escort(20)]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Model(), "Active") && dangers() == 1 && Shown().Length == 1, "CE64 actual standard shape");
            var arena = Read(Snapshot(), "ArenaBoundary");
            Check(arena.GetType().Name == "WorldPolygonGeometry" && ((IEnumerable)arena.GetType().GetMethod("GetContours")!.Invoke(arena, [64])!).Cast<object>().Count() == 1,
                "CE64 square arena publication");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 23617 }),
                "CE64 precise source suppression");
            if (action == 23615)
            {
                // The helper's raw origin is center, so the real registered
                // R6 danger forces an actual path inside the 48x48 arena.
                var snapshot = Snapshot();
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance(center, (Vector3)Read((object)result, "TargetPosition")) >= 6, "CE64 real solver escape R6");
            }
            Seek(20.1); Check(dangers() == 1 && Shown().Length == 1, "CE64 Effect/TTL retain raw cast");
            Seek(21.1); Check(dangers() == 0 && Shown().Length == 0, "CE64 ordinary Finish consumes");
            Seek(.1); Check(dangers() == 1, "CE64 reverse Seek rebuild");
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0, "CE64 mechanism disable");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            Release();
            Console.WriteLine($"PASS native CE64 {action} actual geometry square arena source ownership true Finish solver reverse Seek owner");
        }
        load(id, [Header(975), World(0, [Boss(), Player(), Escort(20), Escort(21, pos: center + Vector3.UnitX * 8),
            Escort(22, pos: center - Vector3.UnitX * 8)], true),
            Icon(1, 20), Icon(2, 21), Icon(3, 22), Effect(4, 23610, 1), World(20, []), World(21, [])]);
        Seek(3.1);
        Check(Shown().Length == 2 && dangers() == 2 && Items("Casts").Length == 3, "CE64 icons first two of three");
        var first = (Vector3)Read(Read(Shown()[0], "Geometry"), "Position");
        Seek(4.1);
        Check(Shown().Length == 2 && Items("Casts").Length == 2
            && (Vector3)Read(Read(Shown()[0], "Geometry"), "Position") != first, "CE64 cross source hidden promotion");
        Seek(20.1); Check(dangers() == 2, "CE64 icons no TTL");
        Release();
        Console.WriteLine("PASS native CE64 actual icon FIFO source positions hidden promotion eleven second clock no TTL owner");

        load(id, [Header(975), World(0, [Boss(Spell(24658)), Player(), Escort(20)], true),
            Effect(1, 24659), Effect(2, 24659), Effect(3, 24659),
            World(3.1, [Boss(Spell(24658, 3.1))]), Effect(4, 24659), World(5, [])]);
        Seek(3.2);
        Check((int)Read(Model(), "BurnBossHits") == 3 && Shown().Length == 1 && dangers() == 1, "CE64 repeated Start preserves three");
        Seek(4.1);
        Check((int)Read(Model(), "BurnBossHits") == 0 && Shown().Length == 0 && dangers() == 0, "CE64 fourth clears all");
        Release();
        Console.WriteLine("PASS native CE64 actual boss fourth Effect repeated Start preserves counter quantized helper replacement owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Player(playerId: 101, pos: center + Vector3.UnitX * 15),
            Escort(20, tracking: true), Escort(21, tracking: true, pos: center - Vector3.UnitZ * 15)], true),
            World(1, [Escort(20), Escort(21, pos: center - Vector3.UnitZ * 15)]),
            World(2, [Boss(Spell(23614, 2))]), World(3, [Escort(20, Spell(23612, 3, center - Vector3.UnitZ * 10))]),
            Effect(4, 23613), Effect(4.1, 23613), Effect(4.2, 23613), Effect(4.3, 23613), Effect(4.4, 23613), Effect(4.5, 23613),
            World(5, [])]);
        Seek(1.1);
        Check(Shown("bozja/burn/tracking/").Length == 2 && Shown("bozja/burn/tracking/").All(p => (bool)Read(p, "OutlineOnly")),
            "CE64 tracking Lose retains two outline beams");
        Seek(2.1);
        Check(dangers() == 2 && Shown("bozja/burn/tracking/").Length == 2, "CE64 inactive tracking rectangular navigation without changing outlines");
        Seek(3.1);
        Check(Shown("bozja/burn/tracking/").Length == 0 && Shown().Length == 1 && dangers() == 1, "CE64 Start clears trackers and locks beam");
        Seek(4.4); Check(dangers() == 1, "CE64 fifth independent hit retains");
        Seek(4.6); Check(dangers() == 0 && Shown().Length == 0, "CE64 sixth independent hit clears");
        Release();
        Console.WriteLine("PASS native CE64 actual tracker Lose cached participants outline inactive navigation six real Effects owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Player(playerId: 101, pos: center + Vector3.UnitX * 15),
            Escort(20, tracking: true)], true), World(2, [])]);
        Seek(.1);
        ((IList)Read(Model(), "burnParticipants")).Clear();
        var script = Script();
        var prepare = script.GetType().BaseType!.GetMethod("PrepareNavigation", flags)!;
        var render = script.GetType().BaseType!.GetMethod("Render", flags)!;
        prepare.Invoke(script, null); render.Invoke(script, null);
        Check(dangers() == 0, "CE64 first prepared query fills cache without buddy iteration");
        render.Invoke(script, null); render.Invoke(script, null);
        Check(dangers() == 0, "CE64 repeated log rendering cannot consume next participant query");
        prepare.Invoke(script, null); render.Invoke(script, null);
        Check(dangers() == 1, "CE64 next Framework navigation consumes populated participant cache");
        Release();
        Console.WriteLine("PASS native CE64 actual Framework navigation caches first query repeated Render preserves output next frame promotes");

        load(id, [Header(975), World(0, [Boss(), Player(), Escort(20, Spell(23612, pos: center - Vector3.UnitZ * 10)),
            Escort(21, pos: center - Vector3.UnitZ * 15)], true),
            World(1, [Actor(21, 0x32FD, center - Vector3.UnitZ * 15, subKind: 5, stackedStatus: 2056, targetId: 101)]),
            World(2, [])]);
        Seek(1.1);
        var backgrounds = previews().Where(p => ((string)Read(p, "Id")).Contains("bozja/aoe/")
            || ((string)Read(p, "Id")).Contains("bozja/burn/tracking/")).ToArray();
        Check(backgrounds.Length == 2 && ((string)Read(backgrounds[0], "Id")).Contains("bozja/aoe/")
            && ((string)Read(backgrounds[1], "Id")).Contains("bozja/burn/tracking/"), "CE64 static beam background precedes nonviewer tracking");
        Release();
        Console.WriteLine("PASS native CE64 actual static beam then nonviewer tracking background source DrawSink publication order");

        load(id, [Header(975), World(0, [Boss(), Player(2644), Escort(20, Spell(23619, pos: center - Vector3.UnitZ * 10))], true),
            World(4.8, []), World(5, [Player(1709)]), World(6, [Player()]), World(7, [])]);
        Seek(4.9);
        var forwardFacing = Read(Script(), "PlannedFacing");
        Check(Shown("bozja/pupil/").Length == 1 && forwardFacing != null && dangers() == 0
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Face open weakpoint"), "CE64 front weakpoint eye no invented damage");
        Seek(5.1);
        Check(Shown("bozja/pupil/").Length == 1 && (float)Read(Script(), "PlannedFacing") == 0, "CE64 backward weakpoint already faces open side");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE64 replay does not execute facing/target/ACR");
        Seek(6.1); Check(Shown("bozja/pupil/").Length == 0, "CE64 weakpoint Lose clears");
        Seek(4.9); Check(Shown("bozja/pupil/").Length == 1, "CE64 eye reverse Seek");
        Release();
        Console.WriteLine("PASS native CE64 actual front back weakpoint offset eyes arc facing plan replay isolation Lose reverse Seek owner");

        foreach (var action in new uint[] { 23616, 23607 })
        {
            load(id, [Header(975), World(0, [Boss(Spell(action)), Player(), Player(playerId: 101)], true),
                World(20, []), World(21, [Boss()]), World(22, [])]);
            Seek(20.1);
            Check(dangers() == 0 && Shown().Length == 0 && ((string)Read(Script(), "MechanicHintsText")).Contains(
                action == 23616 ? "Raidwide" : "Face open weakpoint"), "CE64 global visual hint");
            Check(((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == (action == 23616 ? 1 : 0),
                "CE64 visual rather than helper raidwide and analysis diagnostic only");
            Seek(21.1); Check(Read(Script(), "MechanicHintsText") == null, "CE64 global Finish");
            Release();
            Console.WriteLine($"PASS native CE64 {action} actual global hint raw NPC diagnostic no geometry true Finish owner");
        }
    }
}
