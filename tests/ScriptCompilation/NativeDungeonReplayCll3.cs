using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCLL3Adrammelech(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string name = "阿德拉梅里克";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = new Vector3(80, 0, -606.1005f);
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
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(e => (bool)Read(e, "IsCll3")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Aoes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var visibility = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        }
        object Spell(uint action, Vector3 point, double at = 0, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, point, .47f, target, at))!;
            node["TotalTime"] = 5; node["IsSpell"] = true; return node;
        }
        object Boss(uint hp = 1000, bool combat = true)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x2E01, center, combat: combat, subKind: 5, isEnemy: true))!;
            node["Hp"] = hp; return node;
        }
        object Helper(ulong actor = 20, uint action = 0, double at = 0, Vector3? point = null)
            => Actor(actor, 0x233C, center, cast: action == 0 ? null : Spell(action, point ?? center, at), subKind: 5);
        object Player(Vector3? point = null, uint status = 0, double expires = 20, bool dead = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(100, 0, point ?? center + Vector3.UnitX * 10, subKind: 4, dead: dead))!;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            {
                new { Id = status, Param = 0, SourceId = 100UL, ExpiresAt = Start.AddSeconds(expires), SlotIndex = 59 },
            });
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(920, at, actors, full, removed,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CLL3 actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "CLL3 priorities special mode remain diagnostics no replay PR requests");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL3 owner cleanup");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "CLL3 real solver without least-danger fallback");
        }
        void Render() => Script().GetType().BaseType!.GetMethod("Render", flags)!.Invoke(Script(), null);
        void Clock(double at) => Read(Script(), "ReplayWorld")!.GetType().GetProperty("CurrentTime")!
            .SetValue(Read(Script(), "ReplayWorld")!, Start.AddSeconds(at));
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        void Apply(double at, IEnumerable<object> actors)
        {
            var world = Read(Script(), "ReplayWorld")!;
            var json = System.Text.Json.Nodes.JsonNode.Parse(World(at, actors))!["data"]!.ToJsonString();
            world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                [JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
            Clock(at);
            Update();
        }

        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCll3Catalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        foreach (var rule in Items(definition, "Rules").Where(r => Read(r, "Kind")!.ToString() is "Aoe" or "Charge" or "Tankbuster"))
            foreach (var action in (uint[])Read(rule, "Actions")!)
            {
                var kind = Read(rule, "Kind")!.ToString();
                var charge = kind == "Charge";
                var damage = kind == "Tankbuster";
                load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                        Helper(action: action, point: center + Vector3.UnitZ * 20)], true),
                    World(5, [Helper()]), World(6, [])]);
                Seek(.3);
                Check((bool)Read(Model(), "Active")! && Aoes().Length == (damage ? 0 : 1)
                    && dangers() == (damage || charge ? 0 : 1)
                    && Items(Script(), "PredictedDamage").Length == (damage ? 1 : 0), "CLL3 actual component " + action);
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                    "CLL3 actual generic suppression");
                if (action != 20366) Solver(center);
                if (charge)
                {
                    Seek(4.5); Check(Aoes().Length == 1 && dangers() == 1, "CLL3 charge late risky window");
                }
                Seek(5.1);
                Check(Aoes().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0, "CLL3 watched Finish");
                Solver(center);
                Seek(.3); manager.SetMechanismEnabled(id, name, false); Seek(.4);
                Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL3 mechanism disable");
                manager.SetMechanismEnabled(id, name, true); Seek(.3);
                Check(Aoes().Length == (damage ? 0 : 1), "CLL3 reverse Seek restored owner");
                Release();
                Console.WriteLine("PASS native CLL3 actual standard Finish risk solver disable Seek owner " + action);
            }

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4), Helper(action: 20374)], true),
            World(1, [Actor(30, 0x1EB037, center)]), World(2, [Helper()]), World(3, []),
            World(4, [Boss(hp: 0)]), World(5, [])]);
        Seek(.3);
        Check(dangers() == 1 && Aoes().Length == 1 && Items(Script(), "PredictedDamage").Length == 1, "CLL3 Holy independent arena and damage");
        var warning = Read(Aoes()[0], "Geometry")!;
        Check((Vector3)Read(warning, "Position")! == center, "CLL3 warning original clipped center");
        Seek(2.1);
        Check(dangers() == 1 && Aoes().Length == 1 && Items(Script(), "PredictedDamage").Length == 0,
            "CLL3 wall and Finish retain old warning");
        Check((Vector3)Read(Read(Snapshot(), "ArenaBoundary")!, "Position")! == new Vector3(80, 0, -606), "CLL3 wall actual reduced center");
        Solver(new(80, 0, -606));
        Seek(4.1); Check(!(bool)Read(Model(), "Active")! && dangers() == 0, "CLL3 HP0 ends ordinary phase");
        Seek(.3); Check(Aoes().Length == 1, "CLL3 reverse Seek resets arena history"); Release();
        Console.WriteLine("PASS native CLL3 actual clipped center Holy persistent warning wall source center HP0 reverse Seek");

        load(id, [Header(920), World(0, [Boss(), Player(status: 1209), Actor(101, 0, center, subKind: 4),
                Helper(action: 20556), Helper(21, 20356)], true),
            World(1, [Player()]), World(2, [Helper()]), World(3, [Helper(21)]), World(4, [])]);
        Seek(.3);
        Check(Aoes().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 2 && Lines().Length == 0,
            "CLL3 actual immune two independent direct Goals");
        Solver(center + Vector3.UnitX * 20);
        Seek(1.1); Check(Lines().Length == 2 && Items(Snapshot(), "GoalAreas").Length == 2, "CLL3 real slot59 Lose restores water lines");
        Seek(2.1); Check(Lines().Length == 1 && Items(Snapshot(), "GoalAreas").Length == 1, "CLL3 first water watched Finish");
        Seek(3.1); Check(Lines().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0, "CLL3 second water watched Finish");
        Release();
        Console.WriteLine("PASS native CLL3 actual two water direct Goals real solver immunity slot59 Lose lines independent Finish");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 21595), Helper(21, 21596)], true),
            World(6, [Player(status: 960), Helper(), Helper(21)]),
            World(7, [Player()]), World(8, [])]);
        Seek(4.5);
        var text = (string?)Read(Script(), "MechanicHintsText");
        Check(text?.Contains("Move!") == true && text.Contains("Stop everything!") && text.Contains("Pyretic"),
            "CLL3 actual ice fire hints plus earlier diagnostic");
        Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0, "CLL3 StayMove does not invent navigation");
        Seek(7.1);
        Check(((DateTime[])Read(Model(), "Cll3Fire")!)[0] == default
            && ((DateTime[])Read(Model(), "Cll3Ice")!)[0] == default, "CLL3 actual Pyretic Lose clears both current slots");
        Release();
        Console.WriteLine("PASS native CLL3 actual ice fire raw diagnostics no hold movement or actions real status Lose");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 21596)], true), World(1, []), World(10, [])]);
        Seek(.3);
        Apply(2.5, [Player(dead: true), Helper()]);
        Check(((DateTime[])Read(Model(), "Cll3Fire")!)[0] == default
            && !(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stop everything!") ?? false)
            && (((string?)Read(Script(), "MechanicHintsText"))?.Contains("Pyretic") ?? false),
            "CLL3 actual capture first Hint clears dead slot before public second query");
        var captured = (string?)Read(Script(), "MechanicHintsText");
        Clock(2.56); Render();
        Check((string?)Read(Script(), "MechanicHintsText") == captured,
            "CLL3 ordinary event Render retains published mode and countdown after dead slot clear");
        Update();
        Check(Read(Script(), "MechanicHintsText") == null, "CLL3 next Framework clears published mode");
        Release();
        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                    Helper(action: 21596)], true), World(1, []), World(10, [])]);
            Seek(.3); radar.SetValue(settings, false); overlay.SetValue(settings, false);
            Apply(1.1, [Player(dead: true), Helper()]);
            Check(((DateTime[])Read(Model(), "Cll3Fire")!)[0] != default
                && !(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stop everything!") ?? false),
                "CLL3 both views asleep do not consume dead Stay slot");
            Apply(2.5, [Player()]); overlay.SetValue(settings, true); Render();
            Check(((DateTime[])Read(Model(), "Cll3Fire")!)[0] != default
                && (((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stop everything!") ?? false),
                "CLL3 revive then draw restores retained Stay hint");
            Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        Console.WriteLine("PASS native CLL3 actual dead capture public second Hint and both views asleep preserve slot across revive");
        Console.WriteLine("PASS native CLL3 actual ordinary event Render retains Framework mode countdown then next Framework clears");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 21595)], true), World(1, []), World(10, [])]);
        Seek(.3);
        Apply(3.5, [Player(center + Vector3.UnitX * 10 + Vector3.UnitY)]);
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Move!（已满足）") == true,
            "CLL3 height change satisfies Move in actual input frame");
        Clock(3.55); Update();
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Move!") == true
            && !((string)Read(Script(), "MechanicHintsText")!).Contains("Move!（已满足）"),
            "CLL3 same snapshot no input Framework catches previous height");
        Release();
        Console.WriteLine("PASS native CLL3 actual no input Framework previous height catches up inside ice window");

        var nonParty = System.Text.Json.Nodes.JsonNode.Parse(World(1, [Actor(150, 0, center + Vector3.UnitX * 22, subKind: 4)]))!;
        nonParty["data"]!["LocalPlayerId"] = 150;
        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4), Helper(action: 20556)], true),
            nonParty.ToJsonString(), World(2, [])]);
        Seek(1.1);
        Check(Lines().Length == 0 && previews().Length == 0 && dangers() == 0
            && Items(Snapshot(), "GoalAreas").Length == 0 && Read(Script(), "MechanicHintsText") == null
            && Read(Snapshot(), "ArenaBoundary") == null,
            "CLL3 non Party viewer sleeps all draw hint and navigation consumers");
        Release();
        Console.WriteLine("PASS native CLL3 actual non Party perspective no water lines hints Goals or boundary");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4), Helper(action: 20353)], true),
            World(1, [Helper()]), World(2, [Helper(action: 20354, at: 2)]), World(3, [Helper()]),
            World(4, [Helper(action: 20355, at: 4)]), World(5, [Helper()]), World(6, [])]);
        foreach (var at in new[] { .3, 1.1, 3.1 })
        {
            Seek(at); Check(dangers() == 1 && Aoes().Length == 1, "CLL3 actual Stone published stage " + at); Solver(center);
        }
        Seek(5.1); Check(dangers() == 0 && Aoes().Length == 0 && (int)Read(Model(), "NumEarthCasts")! == 3, "CLL3 actual Stone final stage");
        Seek(.3); Check((int)Read(Model(), "NumEarthCasts")! == 0 && Aoes().Length == 1, "CLL3 reverse Seek rebuilds Stone sequence"); Release();
        Console.WriteLine("PASS native CLL3 actual Stone three source stages published cache solver reverse Seek");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Actor(30, 0x2E09, center, rotation: .47f, dead: true, targetable: false, subKind: 5)], true),
            World(1, [Actor(30, 0x2E09, center + Vector3.UnitZ * 10, rotation: 1.2f, subKind: 5)]),
            World(2, [], removed: [30]), World(3, [])]);
        Seek(.3);
        Check(Aoes().Length == 1 && dangers() == 4 && Items(Snapshot(), "HardObstacles").Length == 1, "CLL3 actual Twister dead moving capsule four clocks obstacle");
        Solver(center + Vector3.UnitX * 20);
        Seek(1.1); Check(Aoes().Length == 1 && dangers() == 4, "CLL3 Twister dynamic movement no dead filter");
        Seek(2.1); Check(Aoes().Length == 0 && dangers() == 0 && Items(Snapshot(), "HardObstacles").Length == 0, "CLL3 Twister Destroy release");
        Release();
        Console.WriteLine("PASS native CLL3 actual Twister raw dead untargetable capsule four clocks obstacle move Destroy real solver");
    }
}
