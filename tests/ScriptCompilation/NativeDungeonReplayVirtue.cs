using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBaldesionVirtue(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.baldesion.virtue";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object? Read(object obj, string key)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(key, flags) is { } property) return property.GetValue(obj);
                if (type.GetField(key, flags) is { } field) return field.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, key);
        }
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Aoes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")
            || ((string)Read(p, "Id")!).Contains("bozja/virtue/wind/")).ToArray();
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaVirtueCatalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = (Vector3)Read(definition, "Center")!;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object Spell(uint action, double at = 0, float total = 5, Vector3? point = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, point ?? center, .47f, target, at))!;
            node["TotalTime"] = total; node["IsSpell"] = true; return node;
        }
        object Boss(bool combat = true, object? cast = null, bool dead = false, bool targetable = true)
            => Actor(1, 0x25DC, center, combat: combat, cast: cast, dead: dead, subKind: 5, isEnemy: true, targetable: targetable);
        object Npc(ulong actor = 20, uint oid = 0x233C, uint action = 0, double at = 0,
            Vector3? point = null, ulong target = 100, bool dead = false, bool targetable = true, uint status = 0,
            ulong? tetherTarget = null, uint tetherId = 1)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, oid, point ?? center,
                cast: action == 0 ? null : Spell(action, at), subKind: 5, targetId: target, dead: dead, targetable: targetable))!;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = status, Param = 0, SourceId = 0UL, ExpiresAt = Start.AddSeconds(-1), SlotIndex = 59 } });
            if (tetherTarget.HasValue) node["Tethers"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = tetherId, TargetId = tetherTarget.Value } });
            return node;
        }
        object Player(ulong actor = 100, Vector3? point = null, bool dead = false, float rotation = 0, uint status = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, 0, point ?? center + Vector3.UnitX * 10,
                subKind: 4, dead: dead, rotation: rotation))!;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = status, Param = 0, SourceId = 0UL, ExpiresAt = Start.AddSeconds(-1), SlotIndex = 59 } });
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(827, at, actors, full, removed,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL },
                    new { Slot = 2, EntityId = 102UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, ulong source = 20)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                    TargetPosition = Pose(center), GlobalSequence = (uint)(at * 10000), Timestamp = Start.AddSeconds(at) } });
        string Tether(double at, ulong source, ulong target, uint tether = 18)
            => JsonSerializer.Serialize(new { t = at, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Virtue actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Clock(double at) => Read(Script(), "ReplayWorld")!.GetType().GetProperty("CurrentTime")!
            .SetValue(Read(Script(), "ReplayWorld")!, Start.AddSeconds(at));
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        void Apply(double at, IEnumerable<object> actors, ulong[]? removed = null)
        {
            var world = Read(Script(), "ReplayWorld")!;
            var json = JsonNode.Parse(World(at, actors, removed: removed))!["data"]!.ToJsonString();
            world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                [JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
            Clock(at); Update();
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Virtue replay never requests PR facing targets or hold actions");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Virtue all owners released");
        }
        void Solver(Vector3 origin)
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [origin, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!,
                "Virtue actual solver without least-danger fallback");
        }

        object[] AoeOnly() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>().ToArray();
        }
        bool Contains(object geometry, Vector3 point)
            => (bool)geometry.GetType().GetMethod("Contains")!.Invoke(geometry, [point, 0f])!;
        object Boundary() => Read(Snapshot(), "ArenaBoundary")!;
        foreach (var (action, aoes, zones, damage) in new (uint, int, int, int)[]
        { (14233, 0, 0, 1), (14235, 1, 1, 0), (14227, 1, 1, 0), (14228, 1, 1, 0), (14676, 0, 0, 0),
          (14234, 1, 2, 1) })
        {
            load(id, [Header(827), World(0, [Boss(), Player(), Player(101, center - Vector3.UnitX * 10),
                Player(102, center + Vector3.UnitZ * 10), Npc(action: action)], true),
                World(5, [Npc()]), World(6, [])]);
            Seek(.3);
            Check((bool)Read(Model(), "IsVirtue")! && Aoes().Length == aoes && dangers() == zones
                && Items(Script(), "PredictedDamage").Length == damage && Contains(Boundary(), center),
                "Virtue actual standard output " + action);
            if (action == 14676) Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Aerns Wyvnav is enraging!") == true,
                "Virtue real timed add enrage text");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Virtue suppression requires real NPC");
            Solver(center + Vector3.UnitX * 25);
            Seek(5.1);
            Check(Aoes().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0,
                "Virtue actual ordinary Finish clears " + action);
            Seek(.3); manager.SetMechanismEnabled(id, "绝对的美德", false); Seek(.4);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "Virtue mechanism toggle releases all output");
            manager.SetMechanismEnabled(id, "绝对的美德", true); Seek(.3);
            Check(Aoes().Length == aoes, "Virtue reverse Seek reconstructs");
            Release();
            Console.WriteLine("PASS native Virtue actual standard Finish solver suppression disable reverse Seek " + action);
        }

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(status: 1711, action: 14218)], true),
            World(1, [Npc()]), World(2, [Npc(21, 0x2628, action: 14217, at: 2)]),
            World(3, [Npc(21, 0x2628)]), World(4, [])]);
        Seek(.3); Check(AoeOnly().Length == 1 && dangers() == 1, "Virtue cold real slot59 Aurora");
        Seek(1.1); Check(AoeOnly().Length == 1 && dangers() == 1, "Virtue Dark ordinary Finish retains");
        Seek(2.1); Check(AoeOnly().Length == 1, "Virtue wrong status Bright adds no rectangle");
        Seek(3.1); Check(AoeOnly().Length == 0 && dangers() == 0, "Virtue cross source Bright Finish consumes first");
        Release();
        Console.WriteLine("PASS native Virtue actual status59 Aurora Bright cross source FIFO Dark persists");

        object StatusBoss(uint status)
        {
            var node = JsonSerializer.SerializeToNode(Boss())!;
            node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = status, Param = 0, SourceId = 1UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 } });
            return node;
        }
        load(id, [Header(827), World(0, [StatusBoss(1711), Player(), Player(101), Npc(action: 14222)], true),
            World(1, [Npc(21, 0x2628, action: 14220, at: 1)]),
            World(2, [Npc(21, 0x2628)]), World(3, [])]);
        Seek(.3);
        var rays = Items(Model(), "Casts");
        Check(rays.Length == 1 && (float)Read(Read(rays[0], "Shape")!, "Radius")! == 16,
            "Virtue Rays takes Primary real slot59");
        Seek(1.1); Check(AoeOnly().Length == 2 && dangers() == 2, "Virtue Rays multiple sources");
        Seek(2.1); Check(AoeOnly().Length == 0 && dangers() == 0, "Virtue Rays any corresponding Finish clears all");
        Release();
        Console.WriteLine("PASS native Virtue actual Primary status Rays R16 R8 multi-source Finish clears all");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(tetherTarget: 100),
            Npc(30, 0x1EAA4A, point: center)], true),
            World(1, [Npc(21, 0x25DF, tetherTarget: 100, tetherId: 77)]), World(2, [])]);
        Seek(.3);
        Check(Items(Model(), "Towers").Length == 1 && dangers() == 1 && Lines().Length == 1
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("opposite tether element") == true,
                $"Virtue own tether tower complement navigation line and custom hint: towers={Items(Model(), "Towers").Length} zones={dangers()} lines={Lines().Length} hints={Read(Script(), "MechanicHintsText")}");
        Check((DateTime)Read(Items(Model(), "Towers").Single(), "Activation")! == Start.AddSeconds(20),
            "Virtue actual tower cold Created plus20");
        Check((Vector3)Read(Lines().Single(), "Start")! == center && (Vector3)Read(Lines().Single(), "End")! == center + Vector3.UnitX * 10,
            "Virtue tower raw original source viewer endpoints");
        Solver(center);
        Seek(1.1);
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("源空形状") == true && dangers() == 1
            && Lines().Length == 1 && (int)controller.RouterErrorCount == 0, "Virtue default AOE diagnostic isolates source drawing failure");
        Release();
        Console.WriteLine("PASS native Virtue actual tower opposite element raw line solver default AOE explicit diagnostics");

        load(id, [Header(827), World(0, [Boss(), Player(point: center), Player(101),
            Npc(tetherTarget: 100), Npc(30, 0x1EAA4A, point: center - Vector3.UnitX),
            Npc(31, 0x1EAA4A, point: center + Vector3.UnitX)], true), World(2, [])]);
        Seek(.3);
        Check(Items(Model(), "Towers").Length == 2 && dangers() == 1, "Virtue actual two allowed towers join one complement");
        var overlap = Read(Items(Snapshot(), "DangerAreas").Single(), "Geometry")!;
        Check(!(bool)overlap.GetType().GetMethod("IntersectsSegment")!.Invoke(overlap,
            [center - Vector3.UnitX * 1.2f, center - Vector3.UnitX * .8f, .0001f])!,
            "Virtue registered geometry has no internal overlap boundary");
        Solver(center);
        Release();
        Console.WriteLine("PASS native Virtue actual overlapping towers registered complement no false segment real solver owner");

        var animation = JsonSerializer.Serialize(new { t = 2, kind = "EObjAnimation",
            type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
            data = new { SourceId = 30UL, Param1 = (ushort)4, Param2 = (ushort)8, Timestamp = Start.AddSeconds(2) } });
        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(), Npc(30, 0x1EAA4A)], true),
            Tether(.1, 20, 100, 1), World(.2, [Npc(tetherTarget: 100)]), animation, World(3, [])]);
        Seek(.3); Check(dangers() == 1 && Lines().Length == 1, "Virtue real tether event and frame update publish tower");
        Seek(2.1); Check(Items(Model(), "Towers").Length == 0 && dangers() == 0 && Lines().Length == 1,
            "Virtue real animation removes tower but retains raw tether line");
        Release();
        Console.WriteLine("PASS native Virtue actual hot tether EAnim exact-position tower removal preserves tether line");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(tetherTarget: 999),
            Npc(30, 0x1EAA4A, point: center), Npc(21, 0x2628, action: 14235)], true), World(2, [])]);
        Seek(.3);
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("目标未解析") == true
            && dangers() == 2 && AoeOnly().Length == 2 && (int)controller.RouterErrorCount == 0,
            "Virtue unresolved tower diagnostic preserves other valid AOE navigation");
        Check(previews().Any(p => ((string)Read(p, "Id")!).Contains("bozja/tower/")),
            "Virtue unresolved target retains legal forbidden-viewer background");
        Release();
        Console.WriteLine("PASS native Virtue actual unresolved tower explicit diagnostics other component outputs survive");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14234)], true),
            Effect(1, 14234), World(2, [Player(dead: true)]), World(3, [Npc()]), World(4, [])]);
        Seek(1.1); Check(Aoes().Length == 1 && dangers() > 0, "Virtue Wind Effect leaves bait");
        Seek(2.1); Check(Aoes().Length == 1 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0,
            "Virtue dead viewer retains original visible bait and revokes execution planning");
        Seek(3.1); Check(Aoes().Length == 0, "Virtue Wind ordinary Finish clears dead target bait");
        Release();
        Console.WriteLine("PASS native Virtue actual Wind Effect retains dead target display ordinary Finish execution isolation");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14235)], true),
            World(1, [Boss(dead: true)]), World(2, [])]);
        Seek(.3); Check(dangers() == 1, "Virtue live output before death");
        Seek(1.1); Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null
            && Items(Script(), "PredictedDamage").Length == 0, "Virtue full death owner release");
        Release();
        Console.WriteLine("PASS native Virtue actual DeathPhase clears every owner and timeline output");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14235)], true),
            World(1, []), World(2, [])]);
        Seek(.3); manager.SetScriptEnabled(id, false); controller.Seek(.4);
        Check(!manager.Running.ContainsKey(id) && (int)controller.RouterErrorCount == 0
            && dangers() == 0 && previews().Length == 0 && Lines().Length == 0, "Virtue whole script owner release");
        manager.SetScriptEnabled(id, true); Seek(.3);
        Check(dangers() == 1, "Virtue reenabled reverse Seek reconstruction");
        Release();
        Console.WriteLine("PASS native Virtue actual script disable owner release reverse Seek rebuild");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc()], true), World(2, [])]);
        Seek(.3);
        var timeline = Read(Model(), "VirtueTimeline")!;
        var visited = new HashSet<uint>();
        for (var step = 0; step < 200 && (uint)Read(timeline, "StateId")! != 0x250020; ++step)
        {
            visited.Add((uint)Read(timeline, "StateId")!);
            var at = ((DateTime)Read(Model(), "Now")! - Start).TotalSeconds + 100;
            var capture = Script().GetType().GetMethod("GetTimeline")!.Invoke(Script(), null)!;
            var hint = Convert.ToInt32(Read(Items(capture, "Entries")[0], "Hints"));
            var cast = (JsonNode)Spell(999, at);
            cast["IsSpell"] = false;
            Apply(at, [Boss(cast: (hint & 2) != 0 ? cast : null)]);
        }
        Check((uint)Read(timeline, "StateId")! == 0x250020 && visited.Count > 30,
            "Virtue actual raw cast metadata and component overdue reach terminal state");
        var now = ((DateTime)Read(Model(), "Now")! - Start).TotalSeconds;
        Apply(now + 10000, []);
        Check((uint)Read(timeline, "StateId")! == 0x250020, "Virtue terminal no successor persists");
        Release();
        Console.WriteLine("PASS native Virtue actual timeline rawcasts component overdue GetTimeline permanent terminal");

        const string raiden = "masterplayer.foray.baldesion.raiden";
        manager.SetScriptEnabled(raiden, true);
        var raidenCenter = new Vector3(.27999878f, 0, 457.825f);
        load(id, [Header(827), World(0, [Boss(), Actor(2, 0x2605, raidenCenter, combat: true, subKind: 5, isEnemy: true),
            Player(), Player(101), Npc(action: 14235), Npc(21, 0x2628, action: 14441),
            Npc(22, 0x25E0, tetherTarget: 100), Npc(30, 0x1EAA4A)], true), World(2, [])]);
        Seek(.3); Check(dangers() == 3 && AoeOnly().Length == 2 && Lines().Length == 1, "Virtue Raiden same territory active owners");
        manager.SetScriptEnabled(id, false);
        Check(dangers() == 1 && AoeOnly().Length == 1 && Lines().Length == 0
            && ((string)Read(AoeOnly().Single(), "Id")!).StartsWith("MasterPlayer_Script_masterplayerforaybaldesionraiden_"),
            "Virtue disabled preserves Raiden owner");
        manager.SetScriptEnabled(id, true); Seek(.3); manager.SetScriptEnabled(raiden, false);
        Check(dangers() == 2 && AoeOnly().Length == 1 && Lines().Length == 1
            && ((string)Read(AoeOnly().Single(), "Id")!).StartsWith("MasterPlayer_Script_masterplayerforaybaldesionvirtue_"),
            "Virtue line and AOE owners survive Raiden disabled");
        Release(); manager.SetScriptEnabled(raiden, true);
        Console.WriteLine("PASS native Virtue actual Raiden independent active owner AOE line isolation both directions");
    }
}
