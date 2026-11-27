using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBaldesionOwain(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.baldesion.owain";
        const string art = "masterplayer.foray.baldesion.art";
        const string hydatos = "masterplayer.foray.hydatos";
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
        object[] Spirit() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/owain/spirit/")).ToArray();
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaOwainCatalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = (Vector3)Read(definition, "Center")!;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object Spell(uint action, double at = 0, float total = 5, Vector3? point = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, point ?? center, .47f, target, at))!;
            node["TotalTime"] = total; node["IsSpell"] = true; return node;
        }
        object Boss(bool combat = true, object? cast = null, bool dead = false)
            => Actor(1, 0x265D, center, combat: combat, cast: cast, dead: dead, subKind: 5, isEnemy: true);
        object Npc(ulong actor = 20, uint oid = 0x233C, uint action = 0, double at = 0,
            Vector3? point = null, ulong target = 100, bool dead = false, bool targetable = true, uint status = 0,
            ulong? tetherTarget = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, oid, point ?? center,
                cast: action == 0 ? null : Spell(action, at), subKind: 5, targetId: target, dead: dead, targetable: targetable))!;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = status, Param = 0, SourceId = 0UL, ExpiresAt = Start.AddSeconds(-1), SlotIndex = 59 } });
            if (tetherTarget.HasValue) node["Tethers"] = JsonSerializer.SerializeToNode(new[]
            { new { Id = 999, TargetId = tetherTarget.Value } });
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
                data = new { SourceId = source, TargetId = 102UL, MarkerId = 55, Timestamp = Start.AddSeconds(at) } });
        string Effect(double at, uint action, ulong source = 20)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                    TargetPosition = Pose(center), GlobalSequence = (uint)(at * 10000), Timestamp = Start.AddSeconds(at) } });
        string Tether(double at, ulong source, ulong target, uint tether = 999)
            => JsonSerializer.Serialize(new { t = at, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Owain actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Clock(double at) => Read(Script(), "ReplayWorld")!.GetType().GetProperty("CurrentTime")!
            .SetValue(Read(Script(), "ReplayWorld")!, Start.AddSeconds(at));
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        void Render() => Script().GetType().BaseType!.GetMethod("Render", flags)!.Invoke(Script(), null);
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
                "Owain replay never requests PR facing targets or hold actions");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Owain all owners released");
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
                "Owain actual solver without least-danger fallback");
        }

        foreach (var (action, aoes, zones, damage) in new (uint, int, int, int)[]
        { (14661, 0, 0, 1), (14662, 0, 0, 1), (14655, 1, 2, 0), (14660, 1, 2, 0), (14669, 1, 1, 0), (14658, 0, 0, 0) })
        {
            load(id, [Header(827), World(0, [Boss(), Player(), Player(101, center - Vector3.UnitX * 10),
                Player(102, center + Vector3.UnitZ * 10), Npc(action: action)], true), World(5, [Npc()]), World(6, [])]);
            Seek(.3);
            Check((bool)Read(Model(), "IsOwain")! && Aoes().Length == aoes && dangers() == zones
                && Items(Script(), "PredictedDamage").Length == damage, "Owain actual standard output " + action);
            if (action == 14658)
                Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Ivory Palm is enraging!") == true,
                    "Owain Explosion is timed global hint without invented raidwide damage");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Owain generic suppression requires actual NPC");
            Solver(center + Vector3.UnitX * 25);
            Seek(5.1);
            Check(Aoes().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0, "Owain ordinary actual Finish " + action);
            Seek(.3); manager.SetMechanismEnabled(id, "欧文", false); Seek(.4);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Owain mechanism disable");
            manager.SetMechanismEnabled(id, "欧文", true); Seek(.3);
            Check(Aoes().Length == aoes, "Owain reverse Seek rebuild");
            Release();
            Console.WriteLine("PASS native Owain actual standard Finish solver suppression disable reverse Seek " + action);
        }

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(),
                Npc(30, 0x265E, point: center + Vector3.UnitZ * 8, status: 1783, dead: true, targetable: false)], true),
            World(1, [Npc(action: 14650, at: 1)]), World(2, [Npc()], removed: [30]), Effect(3, 14653, 1), World(10, [])]);
        Seek(1.1); Check(Aoes().Length == 2 && dangers() == 2, "Owain real slot59 expired-but-present Fire spear then boss");
        Seek(2.1); Check(Aoes().Length == 2 && dangers() == 2, "Owain Finish destruction preserves sticky elements");
        Seek(3.1); Check(Aoes().Length == 1 && dangers() == 1, "Owain cross-source Ice Effect pops Fire FIFO head");
        Solver(center + Vector3.UnitX * 25); Release();
        Console.WriteLine("PASS native Owain actual Elemental real sixty slots dead spear sticky FIFO cross-source Effect");

        load(id, [Header(827), World(0, [Boss(), Player(point: center), Player(101, center + Vector3.UnitX * 5),
                Player(102, center - Vector3.UnitX * 5), Npc()], true), Icon(.5, 100), Icon(.6, 101),
            World(3, [Player(102, status: 1753)]), World(4, [Player(102)]), World(5, [])]);
        Seek(1);
        var originalStack = Read(Model(), "OwainSpiritStack")!;
        Check((ulong)Read(Read(Read(originalStack, "Target")!, "Actor")!, "Id")! == 101
            && Spirit().Length == 1 && Items(Snapshot(), "GoalAreas").Length == 1
            && Items(Script(), "PredictedDamage").Length == 0, "Owain source Icon identity foreground buddy and no stack damage");
        var activation = (DateTime)Read(originalStack, "Activation")!;
        Seek(4.1);
        Check(Read(Model(), "OwainSpiritStack") == null && Spirit().Length == 0
            && Items(Snapshot(), "GoalAreas").Length == 0 && dangers() == 0, "Owain unrelated real status Lose clears Spirit");
        Seek(1); Check((DateTime)Read(Read(Model(), "OwainSpiritStack")!, "Activation")! == activation,
            "Owain reverse Seek retains original event clock");
        Release();
        Console.WriteLine("PASS native Owain actual Spirit original Icon foreground status Lose no damage reverse Seek");

        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, [Header(827), World(0, [Boss(), Player(point: center), Player(101, center + Vector3.UnitX * 5),
                Player(102, center + Vector3.UnitZ * 15)], true), Icon(.5, 100), Icon(.6, 101), Icon(.7, 102), World(10, [])]);
            Seek(1);
            radar.SetValue(settings, false); overlay.SetValue(settings, false);
            Apply(2, [Player(101, center + Vector3.UnitX * 20), Player(102, center + Vector3.UnitZ * 2)]);
            Check((ulong)Read(Read(Read(Read(Model(), "OwainSpiritStack")!, "Target")!, "Actor")!, "Id")! == 101,
                "Owain both views asleep never redirect Spirit");
            overlay.SetValue(settings, true); Render();
            Check((ulong)Read(Read(Read(Read(Model(), "OwainSpiritStack")!, "Target")!, "Actor")!, "Id")! == 101,
                "Owain event-only Render does not run framework foreground side effect");
            Clock(2.1); Update();
            Check((ulong)Read(Read(Read(Read(Model(), "OwainSpiritStack")!, "Target")!, "Actor")!, "Id")! == 102,
                "Owain waking foreground redirects nearest original target");
            var oldGoal = Read(Items(Snapshot(), "GoalAreas").Single(), "Geometry")!;
            Check((bool)oldGoal.GetType().GetMethod("Contains")!.Invoke(oldGoal, [center + Vector3.UnitX * 20, 0f])!
                && !(bool)oldGoal.GetType().GetMethod("Contains")!.Invoke(oldGoal, [center + Vector3.UnitZ * 2, 0f])!,
                "Owain same framework navigation retained pre-draw target");
            Check((bool)Read(Spirit().Single(), "Geometry")!.GetType().GetMethod("Contains")!
                .Invoke(Read(Spirit().Single(), "Geometry")!, [center + Vector3.UnitZ * 2, 0f])!,
                "Owain same framework foreground already uses new target");
            Clock(2.2); Update();
            var newGoal = Read(Items(Snapshot(), "GoalAreas").Single(), "Geometry")!;
            Check((bool)newGoal.GetType().GetMethod("Contains")!.Invoke(newGoal, [center + Vector3.UnitZ * 2, 0f])!
                && !(bool)newGoal.GetType().GetMethod("Contains")!.Invoke(newGoal, [center + Vector3.UnitX * 20, 0f])!,
                "Owain next framework navigation uses foreground target");
            Solver(center + Vector3.UnitX * 25); Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        Console.WriteLine("PASS native Owain actual both views sleep pre-draw navigation next framework nearest target solver");

        load(id, [Header(827), World(0, [Boss(), Player(point: center + Vector3.UnitZ * 10, rotation: MathF.PI / 2),
                Player(101, center + Vector3.UnitZ * 20), Npc(30, 0x266B, target: 101, targetable: false)], true),
            Tether(.5, 30, 100), World(.6, [Npc(30, 0x266B, target: 101, targetable: false, tetherTarget: 100)]),
            World(2, [Npc(30, 0x266B, target: 101, status: 610, tetherTarget: 100)]),
            World(3, [Npc(30, 0x266B, target: 101, tetherTarget: 100)]), Effect(4, 14657), World(5, [])]);
        Seek(1);
        Check(Read(Script(), "PlannedFacing") != null && dangers() == 1
            && previews().Count(p => ((string)Read(p, "Id")!).Contains("bozja/eye/")) == 1
            && previews().Count(p => ((string)Read(p, "Id")!).Contains("bozja/owain/cleave/")) == 1,
            $"Owain all tether IDs reverse eye untargetable continuous Aero: facing={Read(Script(), "PlannedFacing")} dangers={dangers()} tethers={Items(Model(), "OwainTethers").Length} previews={string.Join(",", previews().Select(p => Read(p, "Id")))}");
        Seek(2.1);
        Check(dangers() == 0 && Read(Script(), "PlannedFacing") != null
            && previews().Any(p => ((string)Read(p, "Id")!).Contains("bozja/eye/")),
            "Owain petrification real slot59 stops Aero but not eye");
        Seek(4.1); Check(dangers() == 1, "Owain actual Aero Effect does not remove continuous cleave");
        Release();
        Console.WriteLine("PASS native Owain actual inverse gaze planned facing real slot59 continuous cleave replay no execution");

        var nonParty = JsonNode.Parse(World(2, [Player(150)]))!;
        nonParty["data"]!["LocalPlayerId"] = 150;
        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(30, 0x266B, target: 101)], true),
            Icon(.5, 100), Icon(.6, 101), Tether(.7, 30, 100), nonParty.ToJsonString(), World(3, [])]);
        Seek(2.1);
        Check(previews().Length == 0 && dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0
            && Read(Script(), "PlannedFacing") == null && Read(Script(), "MechanicHintsText") == null
            && Read(Snapshot(), "ArenaBoundary") == null, "Owain nonParty perspective invalidates all cached consumers");
        Release();
        Console.WriteLine("PASS native Owain actual nonParty perspective clears cached navigation eye Spirit hint facing arena");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Npc(action: 14656)], true), World(5, [])]);
        Seek(.3);
        Check(Items(Script(), "PredictedDamage").Length == 0 && (string?)Read(Script(), "MechanicHintsText") == null,
            "Owain LegendaryImbas is absent before its temporary timeline activation");
        // Reach the real timeline through ordinary raw boss casts, not by setting the model index.
        var timeline = Read(Model(), "OwainTimeline")!;
        for (var step = 0; step < 100 && (uint)Read(timeline, "StateId")! != 0x70000; ++step)
        {
            var at = ((DateTime)Read(Model(), "Now")! - Start).TotalSeconds + 100;
            var capture = timeline.GetType().GetMethod("Capture", flags)!.Invoke(timeline, null)!;
            var first = Items(capture, "Entries")[0];
            var start = (Convert.ToInt32(Read(first, "Hints")) & 2) != 0;
            Apply(at, [Boss(cast: start ? Spell(999, at) : null)]);
        }
        Check((uint)Read(timeline, "StateId")! == 0x70000, "Owain actual timeline reaches Spiritcull");
        var now = ((DateTime)Read(Model(), "Now")! - Start).TotalSeconds;
        Apply(now, [Boss(cast: Spell(14654, now))]);
        Check((bool)Read(Model(), "OwainImbasActive")! && Items(Script(), "PredictedDamage").Length == 1,
            "Owain temporary CastEnd entry cold activates raw helper cast");
        Apply(now + .1, [Boss(), Npc()]); Apply(now + .2, []);
        Check((bool)Read(Model(), "OwainImbasActive")!, "Owain empty temporary component waits actual condition");
        Apply(now + 2, []);
        Check(!(bool)Read(Model(), "OwainImbasActive")! && Items(Script(), "PredictedDamage").Length == 0,
            "Owain real overdue exits temporary component");
        Release();
        Console.WriteLine("PASS native Owain actual temporary Imbas timeline raw cast cold activation overdue release");

        manager.SetScriptEnabled(art, true); manager.SetScriptEnabled(hydatos, true);
        var artCenter = new Vector3(-128.98f, 0, 747.835f);
        load(id, [Header(827), World(0, [Boss(), Actor(2, 0x265A, artCenter, combat: true, subKind: 5, isEnemy: true),
                Actor(3, 0x2765, center, combat: true, subKind: 5, isEnemy: true), Npc(40, action: 15468),
                Player(point: artCenter + Vector3.UnitX * 10), Player(101), Npc(action: 14669),
                Npc(30, action: 14639, point: artCenter)], true),
            World(.1, [Player()]), World(5, [])]);
        Seek(.3);
        Check(manager.Running.ContainsKey(art) && manager.Running.ContainsKey(hydatos)
            && Items(Read(Read(manager.Running[art], "Script")!, "region")!, "Encounters").Length == 1
            && Aoes().Length == 2 && dangers() == 3
            && previews().Count(p => ((string)Read(p, "Id")!).Contains("hydatos/aoe/")) == 1,
            "Owain same T827 Art Owain and Ceto independently active no duplicate outputs");
        manager.SetScriptEnabled(id, false);
        Check(Aoes().Length == 1 && dangers() == 2
            && Aoes().All(p => ((string)Read(p, "Id")!).StartsWith("MasterPlayer_Script_masterplayerforaybaldesionart_")),
            "Owain disable preserves active Art and Hydatos owners");
        manager.SetScriptEnabled(id, true); Seek(.3);
        manager.SetScriptEnabled(art, false);
        Check(Aoes().Length == 1 && dangers() == 2
            && Aoes().All(p => ((string)Read(p, "Id")!).StartsWith("MasterPlayer_Script_masterplayerforaybaldesionowain_")),
            "Owain independent active owner survives Art disable");
        manager.SetScriptEnabled(hydatos, false);
        Check(Aoes().Length == 1 && dangers() == 1, "Owain independent owner survives Hydatos disable");
        Release(); manager.SetScriptEnabled(art, true); manager.SetScriptEnabled(hydatos, true);
        Console.WriteLine("PASS native Owain actual Art Hydatos coexistence separate same territory owners");
    }
}
