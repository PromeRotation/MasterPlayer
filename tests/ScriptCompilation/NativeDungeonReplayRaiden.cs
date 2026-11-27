using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBaldesionRaiden(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.baldesion.raiden";
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
            || ((string)Read(p, "Id")!).Contains("bozja/spread/")).ToArray();
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaRaidenCatalog")!
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
            => Actor(1, 0x2605, center, combat: combat, cast: cast, dead: dead, subKind: 5, isEnemy: true, targetable: targetable);
        object Npc(ulong actor = 20, uint oid = 0x233C, uint action = 0, double at = 0,
            Vector3? point = null, ulong target = 100, bool dead = false, bool targetable = true, uint status = 0,
            ulong? tetherTarget = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, oid, point ?? center,
                cast: action == 0 ? null : Spell(action, at), subKind: 5, targetId: target, dead: dead, targetable: targetable))!;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = status, Param = 0, SourceId = 0UL, ExpiresAt = Start.AddSeconds(-1), SlotIndex = 59 } });
            if (tetherTarget.HasValue) node["Tethers"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = 18, TargetId = tetherTarget.Value } });
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
        string Icon(double at, ulong source)
            => JsonSerializer.Serialize(new { t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = source, TargetId = 102UL, MarkerId = 138, Timestamp = Start.AddSeconds(at) } });
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
                "Raiden actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
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
                "Raiden replay never requests PR facing targets or hold actions");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Raiden all owners released");
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
                "Raiden actual solver without least-danger fallback");
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
        string Animation(double at) => JsonSerializer.Serialize(new { t = at, kind = "EObjAnimation",
            type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
            data = new { SourceId = 40UL, Param1 = (ushort)1, Param2 = (ushort)2, Timestamp = Start.AddSeconds(at) } });

        foreach (var (action, aoes, zones, damage) in new (uint, int, int, int)[]
        { (14458, 0, 0, 1), (14459, 0, 0, 1), (14441, 1, 1, 0), (14442, 1, 1, 0),
          (14445, 1, 1, 0), (14443, 1, 1, 0), (14444, 1, 1, 0), (14447, 0, 0, 1),
          (14461, 1, 1, 0), (14451, 1, 1, 0), (14460, 1, 1, 0), (14456, 0, 0, 0) })
        {
            load(id, [Header(827), World(0, [Boss(), Player(), Player(101, center - Vector3.UnitX * 10),
                Player(102, center + Vector3.UnitZ * 10), Npc(action: action)], true),
                World(.1, [Boss(targetable: false)]), World(5, [Npc()]), World(6, [])]);
            Seek(.3);
            Check((bool)Read(Model(), "IsRaiden")! && (int)Read(Model(), "RaidenPhase")! == 1
                && Aoes().Length == aoes && dangers() == zones && Items(Script(), "PredictedDamage").Length == damage,
                $"Raiden actual standard output {action}: phase={Read(Model(), "RaidenPhase")} active={Read(Model(), "Active")} aoes={Aoes().Length} zones={dangers()} damage={Items(Script(), "PredictedDamage").Length}");
            if (action == 14456)
                Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Enrage, kill the adds!") == true,
                    "Raiden Ultimate has timed enrage text without invented damage");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Raiden suppression requires actual NPC");
            Solver(center + Vector3.UnitX * 25);
            Seek(5.1);
            Check(Aoes().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0,
                "Raiden ordinary actual Finish " + action);
            Seek(.3); manager.SetMechanismEnabled(id, "莱丁", false); Seek(.4);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "Raiden mechanism disable removes all output");
            manager.SetMechanismEnabled(id, "莱丁", true); Seek(.3);
            Check(Aoes().Length == aoes, "Raiden reverse Seek reconstructs actual output");
            Release();
            Console.WriteLine("PASS native Raiden actual standard Finish solver suppression disable reverse Seek " + action);
        }

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14463),
            Actor(40, 0x1EA1A1, center)], true), World(1, [Npc()]), Animation(2),
            World(3, [Boss(targetable: false)]), World(4, [])]);
        Seek(.3);
        Check(AoeOnly().Length == 1 && dangers() == 1 && Contains(Boundary(), center + Vector3.UnitX * 32),
            "Raiden initial clipped source arena and wall warning");
        Seek(1.1); Check(AoeOnly().Length == 1 && dangers() == 1, "Raiden wall warning ignores normal Finish");
        Seek(2.1);
        Check(AoeOnly().Length == 0 && dangers() == 0 && (bool)Read(Model(), "RaidenReduced")!
            && !Contains(Boundary(), center + Vector3.UnitX * 32) && (Vector3)Read(Model(), "Center")! == center,
            "Raiden real animation shrinks Bounds preserving center");
        Seek(3.1); Check((int)Read(Model(), "RaidenPhase")! == 1, "Raiden real targetability enters P2");
        Solver(center); Seek(.3); Check(AoeOnly().Length == 1 && !(bool)Read(Model(), "RaidenReduced")!,
            "Raiden reverse Seek rebuilds starting bounds and P1 ArenaChange");
        Release();
        Console.WriteLine("PASS native Raiden actual EAnim sticky warning exact arena center phases reverse Seek solver");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14441)], true),
            World(1, [Boss(targetable: false), Npc(30, 0x2607, point: center + Vector3.UnitZ * 8)]),
            World(2, [], removed: [30]), Effect(3, 14455), World(4, [Npc()]), World(5, [])]);
        Seek(.3); Check((int)Read(Model(), "RaidenPhase")! == 0 && AoeOnly().Length == 1, "Raiden P1 Keep component is active");
        Seek(1.1);
        Check(AoeOnly().Length == 2 && dangers() == 2 && Items(Model(), "Casts").Count(c =>
            (string)Read(Read(c, "Rule")!, "Component")! == "AmeNoSakahoko") == 1,
            "Raiden P2 keeps original circle once plus cold Created add");
        Check((ulong)Read(Script(), "PlannedTargetId")! == 0
            && (ulong)Read(Read(Script(), "Hints")!, "target")! == 0,
            "Raiden active StreakLightning priority never expands into unconsumed PR target execution");
        var add = Items(Model(), "Casts").Single(c => (string)Read(Read(c, "Rule")!, "Component")! == "LancingBlowAOE");
        Check((DateTime)Read(add, "Activation")! == Start.AddSeconds(2), "Raiden cold add uses activation Now plus one");
        Seek(2.1); Check(AoeOnly().Length == 2, "Raiden add destruction does not clear locked AOE");
        Seek(3.1); Check(AoeOnly().Length == 1, "Raiden cross-source Lancing Effect clears locked adds only");
        Seek(4.1); Check(AoeOnly().Length == 0, "Raiden kept circle consumes real Finish");
        Release();
        Console.WriteLine("PASS native Raiden actual P1 Keep original cast P2 cold Created add destruction real Effect");

        load(id, [Header(827), World(0, [Boss(), Player(point: center),
            Player(101, center + Vector3.UnitX * 5), Player(102, center - Vector3.UnitX * 5), Npc()], true),
            World(.1, [Boss(targetable: false)]),
            Icon(.5, 100), Icon(.6, 101), Icon(.7, 100),
            World(2, [Npc(30, 0x2607, point: center + Vector3.UnitZ * 15)]), Effect(3, 14455), World(4, [])]);
        Seek(1);
        Check(Aoes().Length == 3 && dangers() > 0 && Items(Script(), "PredictedDamage").Length == 1
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("GTFO from spreads!") == true,
            "Raiden duplicate original Icons retain outlines avoidance combined hints and diagnostic damage");
        Solver(center + Vector3.UnitX * 25);
        Seek(2.1); Check(Aoes().Length == 1 && dangers() == 1 && Items(Script(), "PredictedDamage").Length == 0,
            "Raiden real Created clears every Icon but publishes locked add");
        Seek(3.1); Check(Aoes().Length == 0 && dangers() == 0, "Raiden actual Lancing Effect clears adds");
        Release();
        Console.WriteLine("PASS native Raiden actual duplicate Icons combined hints solver Created clear real Effect no invented damage");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101, center - Vector3.UnitX * 10),
            Npc(action: 14449)], true), World(.1, [Boss(targetable: false)]), World(1, [Npc()]),
            Effect(2, 14450), World(3, [Npc(point: center + new Vector3(8 * MathF.Sin(.47f), 0, 8 * MathF.Cos(.47f)))]),
            Effect(3.1, 14450), World(4, [])]);
        Seek(.3); Check(AoeOnly().Length == 8 && dangers() == 8, "Raiden Cloud all imminent and seven future AOEs");
        Seek(1.1); Check(AoeOnly().Length == 8, "Raiden Cloud ignores normal Finish");
        Seek(2.1); Check(AoeOnly().Length == 7 && dangers() == 7, "Raiden Cloud real Effect first advance");
        Seek(3.2); Check(AoeOnly().Length == 6 && dangers() == 6, "Raiden Cloud second raw caster advance");
        Release();
        Console.WriteLine("PASS native Raiden actual Cloud first rest Effects cached queue Finish independent navigation");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101, center - Vector3.UnitX * 10), Npc()], true),
            World(.1, [Boss(targetable: false)]),
            Tether(.5, 100, 101), World(.6, [TetheredPlayer(100, center + Vector3.UnitX * 10, 101)]),
            World(2, [Player()]), World(3, [])]);
        Seek(1);
        Check((bool)Read(Model(), "RaidenTethersAssigned")! && dangers() == 1
            && Lines().Any(p => ((string)Read(p, "Id")!).Contains("raiden/chain/"))
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Break the chains!") == true,
            "Raiden actual Chains line hint and dynamic separation SDF");
        Solver(center + Vector3.UnitZ * 25);
        Check((Vector3)Read(Lines().Single(), "Start")! == center + Vector3.UnitX * 10
            && (Vector3)Read(Lines().Single(), "End")! == center - Vector3.UnitX * 10,
            "Raiden real displayed line uses original raw viewer partner endpoints");
        Seek(2.1); Check(dangers() == 0 && Lines().Length == 0 && (bool)Read(Model(), "RaidenTethersAssigned")!,
            "Raiden real Untether clears partner but retains Assigned");
        Release();
        Console.WriteLine("PASS native Raiden actual Chains real partner line hint solver Untether sticky Assigned");

        object TetheredPlayer(ulong actor, Vector3 point, ulong target)
        {
            var node = JsonSerializer.SerializeToNode(Player(actor, point))!;
            node["Tethers"] = JsonSerializer.SerializeToNode(new[] { new { Id = 18, TargetId = target } });
            return node;
        }

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14451)], true),
            World(.1, [Boss(targetable: false)]), World(5, [])]);
        Seek(.3);
        var timeline = Read(Model(), "RaidenTimeline")!;
        var visited = new HashSet<uint>();
        for (var step = 0; step < 1000 && (uint)Read(timeline, "StateId")! != 0x1FF0000; ++step)
        {
            visited.Add((uint)Read(timeline, "StateId")!);
            var at = ((DateTime)Read(Model(), "Now")! - Start).TotalSeconds + 100;
            var capture = timeline.GetType().GetMethod("Capture", flags)!.Invoke(timeline, null)!;
            var hint = Convert.ToInt32(Read(Items(capture, "Entries")[0], "Hints"));
            var targetable = (hint & 64) == 0;
            Apply(at, [Boss(targetable: targetable, cast: (hint & 2) != 0 ? Spell(999, at) : null)]);
        }
        Check((uint)Read(timeline, "StateId")! == 0x1FF0000 && visited.Count > 25,
            "Raiden actual P2 raw casts targetability component overdue and Reset reach permanent tail");
        var now = ((DateTime)Read(Model(), "Now")! - Start).TotalSeconds;
        Apply(now + 1000, []);
        Check((uint)Read(timeline, "StateId")! == 0x1FF0000, "Raiden no invented permanent-tail timeout");
        Apply(now + 1001, [Boss(dead: true)]);
        Check(!(bool)Read(Model(), "Active")! && dangers() == 0 && previews().Length == 0, "Raiden real death clears all output");
        Release();
        Console.WriteLine("PASS native Raiden actual timeline raw conditions sequential Reset permanent tail death owner release");

        var nonParty = JsonNode.Parse(World(2, [Player(150)]))!;
        nonParty["data"]!["LocalPlayerId"] = 150;
        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14441)], true),
            World(.1, [Boss(targetable: false)]), Icon(.5, 100), Tether(.6, 100, 101),
            World(.7, [TetheredPlayer(100, center + Vector3.UnitX * 10, 101)]), nonParty.ToJsonString(), World(3, [])]);
        Seek(1); Check(dangers() > 0 && previews().Length > 0 && Lines().Length == 1,
            "Raiden active navigation outlines and line exist before invalid perspective");
        Seek(2.1); Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0
            && Read(Script(), "MechanicHintsText") == null && Read(Snapshot(), "ArenaBoundary") == null
            && Items(Script(), "PredictedDamage").Length == 0,
            "Raiden nonParty viewer invalidates every published consumer");
        Release();
        Console.WriteLine("PASS native Raiden actual nonParty invalidates AOE line damage hints navigation arena caches");

        const string owain = "masterplayer.foray.baldesion.owain";
        manager.SetScriptEnabled(owain, true);
        var owainCenter = new Vector3(129, 0, 747.835f);
        load(id, [Header(827), World(0, [Boss(), Actor(2, 0x265D, owainCenter, combat: true, subKind: 5, isEnemy: true),
            Player(point: owainCenter), Player(101), Npc(action: 14441), Npc(30, action: 14669, point: owainCenter)], true),
            World(.1, [Boss(targetable: false), Player()]), Tether(.2, 100, 101),
            World(.3, [TetheredPlayer(100, center + Vector3.UnitX * 10, 101)]), World(5, [])]);
        Seek(.5); Check(dangers() == 3 && AoeOnly().Length == 2 && Lines().Length == 1,
            "Raiden Owain independently active with Raiden chain on same T827");
        manager.SetScriptEnabled(id, false);
        Check(dangers() == 1 && AoeOnly().Length == 1 && Lines().Length == 0
            && ((string)Read(AoeOnly().Single(), "Id")!).StartsWith("MasterPlayer_Script_masterplayerforaybaldesionowain_"),
            "Raiden disable preserves active Owain owner");
        manager.SetScriptEnabled(id, true); Seek(.5);
        manager.SetScriptEnabled(owain, false);
        Check(dangers() == 2 && AoeOnly().Length == 1 && Lines().Length == 1
            && ((string)Read(AoeOnly().Single(), "Id")!).StartsWith("MasterPlayer_Script_masterplayerforaybaldesionraiden_"),
            "Raiden owner survives Owain disable");
        Release(); manager.SetScriptEnabled(owain, true);
        Console.WriteLine("PASS native Raiden actual same territory Owain independent active owner isolation");
    }
}
