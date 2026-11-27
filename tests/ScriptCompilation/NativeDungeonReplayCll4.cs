using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCLL4Dawon(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string name = "达温";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = new Vector3(80, 0, -813.375f);
        var lyon = new Vector3(80, 0, -874);
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
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(e => (bool)Read(e, "IsCll4")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Aoes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        object[] Eyes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/eye/")).ToArray();
        Vector3 Position(object preview) => (Vector3)Read(Read(preview, "Geometry")!, "Position")!;
        Vector3 Quantized(Vector3 point)
        {
            // Source AOEInstance uses its recorded-coordinate grid for display;
            // the line's raw next position remains unquantized.
            const float grid = 2000f / 65535f;
            const float inverse = 1f / grid;
            return new(((int)MathF.Round(point.X * inverse) - .5f) * grid, point.Y,
                ((int)MathF.Round(point.Z * inverse) - .5f) * grid);
        }
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var visibility = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        }
        object Spell(uint action, Vector3 point, double at = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, point, 0, 100, at))!;
            node["TotalTime"] = 5; node["IsSpell"] = true; return node;
        }
        object Boss(uint hp = 1000)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x2EBD, center, combat: true, subKind: 5, isEnemy: true))!;
            node["Hp"] = hp; return node;
        }
        object Helper(ulong actor = 20, uint action = 0, double at = 0, Vector3? point = null)
            => Actor(actor, 0x233C, point ?? center,
                cast: action == 0 ? null : Spell(action, point ?? center, at), subKind: 5);
        object Player(Vector3? point = null, uint status = 0, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(100, 0, point ?? center + Vector3.UnitX * 10,
                subKind: 4, rotation: rotation))!;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            {
                new { Id = status, Param = 0, SourceId = 100UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 },
            });
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(920, at, actors, full, removed, party: [new { Slot = 0, EntityId = 100UL },
                new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, ulong source = 20)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                    TargetPosition = Pose(center), GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CLL4 actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        void Apply(double at, IEnumerable<object> actors)
        {
            var world = Read(Script(), "ReplayWorld")!;
            var json = JsonNode.Parse(World(at, actors))!["data"]!.ToJsonString();
            world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                [JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(at));
            Update();
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")!, "CLL4 no replay PR facing target actions");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL4 owner release");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "CLL4 real solver no least-danger fallback");
        }
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCll4Catalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        center = (Vector3)Read(definition, "Center")!;
        foreach (var rule in Items(definition, "Rules").Where(r => Read(r, "Kind")!.ToString() is "Aoe" or "Tankbuster"))
            foreach (var action in (uint[])Read(rule, "Actions")!)
                foreach (var upper in new[] { false, true })
                {
                    var origin = upper ? lyon : center;
                    var enabled = Read(rule, "Component")!.ToString() switch
                    {
                        "TasteOfBlood" or "TwinAgonies" or "WindsPeak" => upper,
                        "FrigidPulse" => true,
                        _ => !upper,
                    };
                    var tank = Read(rule, "Kind")!.ToString() == "Tankbuster";
                    var wind = action == 20847 && enabled;
                    load(id, [Header(920), World(0, [Boss(), Player(origin + Vector3.UnitX * 10),
                            Actor(101, 0, origin, subKind: 4), Helper(action: action, point: origin)], true),
                        World(5, [Helper()]), World(6, [])]);
                    Seek(.3);
                    Check((bool)Read(Model(), "Active")! && Aoes().Length == (enabled && !tank ? 1 : 0)
                        && Items(Script(), "PredictedDamage").Length == (enabled && tank ? 1 : 0)
                        && Items(Snapshot(), "GoalAreas").Length == (wind ? 1 : 0)
                        && dangers() == (enabled && !tank ? action == 20857 ? 2 : 1 : 0),
                        "CLL4 actual gated component " + action + "/" + upper);
                    Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                        "CLL4 generic suppression retains invisible component input");
                    Solver(origin + Vector3.UnitX * 18);
                    Seek(5.1); Check(Aoes().Length == 0 && dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                        && Items(Script(), "PredictedDamage").Length == 0, "CLL4 watched Finish");
                    Seek(.3); manager.SetMechanismEnabled(id, name, false); Seek(.4);
                    Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL4 disable");
                    manager.SetMechanismEnabled(id, name, true); Seek(.3);
                    Check(Aoes().Length == (enabled && !tank ? 1 : 0), "CLL4 reverse Seek native owner");
                    Release();
                    Console.WriteLine("PASS native CLL4 actual gate Finish solver suppression disable Seek owner " + action + "/" + upper);
                }

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 20855)], true), World(10, [])]);
        Seek(.3);
        Apply(1, [Player(lyon + Vector3.UnitX * 10)]);
        Check((Vector3)Read(Model(), "Center")! == lyon && !(bool)Read(Model(), "Cll4DawonArena")!
            && (Vector3)Read(Read(Snapshot(), "ArenaBoundary")!, "Position")! == center && Aoes().Length == 0
            && dangers() == 1 && Position(previews().Single(p => ((string)Read(p, "Id")!).EndsWith("bozja/arena"))) == lyon,
            "CLL4 Framework old arena navigation and new display component gate");
        Update();
        Check((Vector3)Read(Read(Snapshot(), "ArenaBoundary")!, "Position")! == lyon && dangers() == 0,
            "CLL4 next Framework publishes Lyon navigation");
        Apply(2, [Actor(30, 0x1EB03C, center)]);
        Check(!(bool)Read(Model(), "Cll4DawonArena")! && (Vector3)Read(Model(), "Center")! == new Vector3(80, 0, -813),
            "CLL4 actual wall forces center not flag");
        Apply(3, [Boss(hp: 0)]);
        Check(!(bool)Read(Model(), "Active")! && dangers() == 0 && previews().Length == 0, "CLL4 HP0 completion release");
        Release();
        Console.WriteLine("PASS native CLL4 actual old navigation new display draw side effect forced center flag split HP0");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 20858)], true), World(1, [Actor(30, 0x1EB03C, center)]),
            World(2, [Helper()]), World(3, [])]);
        Seek(.3); Check(Aoes().Length == 1 && dangers() == 1 && Items(Script(), "PredictedDamage").Length == 1, "CLL4 Molting warning and independent raidwide");
        Seek(1.1); Check(Aoes().Length == 0 && dangers() == 0 && (float)Read(Model(), "SampleRadius")! == 30, "CLL4 wall clears warning");
        Solver(new(80, 0, -813)); Release();
        Console.WriteLine("PASS native CLL4 actual Molting warning independent raidwide wall clears and reduced solver");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4), Helper(),
                Actor(30, 0x1EB03E, center + Vector3.UnitX * 10), Actor(31, 0x1EB03D, center)], true),
            Effect(1, 20864), Effect(2, 20863), World(3, [])]);
        Seek(.3); Check(Aoes().Length == 1 && dangers() == 2, "CLL4 Obey Cross two converted rectangles");
        Seek(1.1); Check(Aoes().Length == 1 && dangers() == 1, "CLL4 Obey FIFO donut promoted");
        Solver(center); Seek(2.1); Check(Aoes().Length == 0 && dangers() == 0, "CLL4 Obey actual Effect consumption");
        Seek(.3); Check(Aoes().Length == 1, "CLL4 Obey reverse Seek Created history"); Release();
        Console.WriteLine("PASS native CLL4 actual Obey Cross conversion FIFO real Effects solver and reverse Seek");

        load(id, [Header(920), World(0, [Boss(), Player(lyon + Vector3.UnitX * 18, 1209),
                Actor(101, 0, lyon, subKind: 4), Helper(action: 20847, point: lyon)], true),
            World(1, [Player(lyon + Vector3.UnitX * 18)]), World(2, [Helper()]), World(3, [])]);
        Seek(.3); Check(Lines().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 1, "CLL4 real slot59 immune retains direct Goal");
        Seek(1.1); Check(Lines().Length == 1 && Items(Snapshot(), "GoalAreas").Length == 1, "CLL4 real Lose restores no-wall line");
        Seek(2.1); Check(Lines().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0, "CLL4 WindsPeak watched Finish");
        Release();
        Console.WriteLine("PASS native CLL4 actual 20847 water line immunity slot59 direct Goal no wall and watched Finish");

        load(id, [Header(920), World(0, [Boss(), Player(lyon + Vector3.UnitX * 15),
                Actor(101, 0, lyon, subKind: 4), Helper(action: 20831, point: lyon)], true),
            Effect(1, 20831), World(2, [Helper()]), World(3, [])]);
        Seek(.3); Check(Aoes().Length == 3 && dangers() == 3, "CLL4 Blood future then imminent cache");
        Solver(lyon + Vector3.UnitX * 15);
        Seek(2.1);
        var blood = Items(Model(), "Exaflares").Single();
        Check(Aoes().Length == 3 && dangers() == 3 && (int)Read(blood, "Left")! == 6
            && (Vector3)Read(Read(blood, "Cast")!, "Origin")! == lyon + Vector3.UnitZ * 6
            && Aoes().Select(Position).SequenceEqual(new[] { lyon + Vector3.UnitZ * 12, lyon + Vector3.UnitZ * 18,
                lyon + Vector3.UnitZ * 6 }.Select(Quantized)), "CLL4 Blood consumes source position not Effect target; Finish preserves line: left="
                + Read(blood, "Left") + " origin=" + Read(Read(blood, "Cast")!, "Origin")
                + " shown=" + string.Join(";", Aoes().Select(Position)));
        Release();
        Console.WriteLine("PASS native CLL4 actual Blood three cached real zones solver and Finish preserves line");

        load(id, [Header(920), World(0, [Boss(), Player(lyon + Vector3.UnitX * 15),
                Actor(101, 0, lyon, subKind: 4), Helper(action: 20833, point: lyon)], true),
            World(1, [Helper()]), World(2, [Helper(action: 20834, at: 2, point: lyon)]), World(3, [Helper()]),
            World(4, [Helper(action: 20835, at: 4, point: lyon)]), World(5, [Helper()]), World(6, [])]);
        foreach (var at in new[] { .3, 1.1, 3.1 }) { Seek(at); Check(Aoes().Length == 1 && dangers() == 1, "CLL4 Pulse stage " + at); Solver(lyon); }
        Seek(5.1); Check(Aoes().Length == 0 && dangers() == 0 && (int)Read(Model(), "NumEarthCasts")! == 3, "CLL4 Pulse third actual Finish");
        Release();
        Console.WriteLine("PASS native CLL4 actual Pulse three stage plus1.5 cached navigation solver and final Finish");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 20874), Helper(21, 20877)], true),
            World(3, [Helper(22, 20879, 3), Helper(23, 20876, 3)]), Effect(3.1, 20874), World(4, [Helper(), Helper(22), Helper(23)]),
            World(5, [Helper(21)]), World(6, [])]);
        Seek(3.2);
        Check(Aoes().Length == 4 && dangers() == 2
            && Items(Model(), "cll4Hammers").Select(a => (bool)Read(a, "Risky")!).SequenceEqual(new[] { true, true, false, false })
            && Items(Model(), "cll4Hammers").Select(a => (bool)Read(a, "Danger")!).SequenceEqual(new[] { true, true, false, false }),
            "CLL4 Hammer ordered full display, first group risk, sticky color, Effect ignored");
        Solver(center - Vector3.UnitZ * 15);
        Seek(4.1);
        Check(Aoes().Length == 1 && dangers() == 1 && (bool)Read(Items(Model(), "cll4Hammers").Single(), "Danger")!,
            "CLL4 Hammer actual watched Finish removes own first group and late group, remaining color sticky");
        Seek(5.1); Check(Aoes().Length == 0 && dangers() == 0, "CLL4 Hammer final Finish");
        Release();
        Console.WriteLine("PASS native CLL4 actual Hammer full ordered list risk sticky color Effect ignored watched Finish solver owner");

        var green = center + Vector3.UnitZ * 10;
        var red = center + new Vector3(6, 0, 15);
        foreach (var plumeFinish in new uint[] { 20865, 20866 })
        {
            load(id, [Header(920), World(0, [Boss(), Player(center - Vector3.UnitX * 15),
                    Actor(101, 0, center, subKind: 4), Helper(),
                    Actor(30, 0x2EBE, green, subKind: 5), Actor(31, 0x2EBF, red, subKind: 5, dead: true)], true),
                World(1, [Helper(action: 20880, at: 1)]), Effect(1.1, 20865),
                World(2, [Actor(31, 0x2EBF, red, subKind: 5, cast: Spell(plumeFinish, red, 2))]),
                World(3, [Actor(31, 0x2EBF, red, subKind: 5), Helper()]),
                World(4, [Actor(32, 0x2EBF, center + Vector3.UnitZ * 20, subKind: 5)]),
                World(5, [Helper(action: 20880, at: 5)]), World(6, [])]);
            Seek(.3); Check(Aoes().Length == 2 && dangers() == 2, "CLL4 Created plume predicts both including dead actor");
            Seek(1.2);
            Check(Aoes().Length == 4 && dangers() == 4 && Aoes().Select(Position).SequenceEqual(new[]
                { green, red, green + Vector3.UnitZ * 28, red + Vector3.UnitZ * 28 }.Select(Quantized)),
                "CLL4 Flutter appends green then red and actual Effect does not clear");
            Solver(center - Vector3.UnitX * 15);
            Seek(3.1); Check(Aoes().Length == 0 && dangers() == 0, "CLL4 Burn Finish clears every plume");
            Seek(4.1); Check(Aoes().Length == 0, "CLL4 later Created does not restore first predictions");
            Seek(5.1); Check(Aoes().Length == 3 && dangers() == 3, "CLL4 later Flutter uses lazy original lists");
            Release();
            Console.WriteLine("PASS native CLL4 actual Plumes Created dead original Flutter append order real Effect ignored Finish first phase and late lazy lists " + plumeFinish);
        }

        load(id, [Header(920), World(0, [Boss(), Player(center + Vector3.UnitZ * 10, rotation: MathF.PI),
                Actor(101, 0, center, subKind: 4), Helper(action: 20846, point: lyon)], true), World(10, [])]);
        Seek(4.7);
        Apply(4.71, [Player(lyon + Vector3.UnitZ * 10, rotation: MathF.PI)]);
        Check(Read(Script(), "PlannedFacing") == null && Eyes().Length == 1 && Lines().Length >= 12
            && dangers() == 0, "CLL4 switching to Lyon draws eye but retains old Dawon facing this Framework");
        Update();
        Check(Read(Script(), "PlannedFacing") is float && Eyes().Length == 1, "CLL4 next Framework enables imminent gaze facing");
        Apply(4.72, [Player(center + Vector3.UnitZ * 10, rotation: MathF.PI)]);
        Check(Read(Script(), "PlannedFacing") is float && Eyes().Length == 0,
            "CLL4 switching to Dawon hides eye but retains old Lyon facing this Framework");
        Update(); Check(Read(Script(), "PlannedFacing") == null, "CLL4 next Framework clears gaze facing");
        Apply(4.73, [Player(lyon + Vector3.UnitZ * 10, rotation: MathF.PI)]); Update();
        Check(Eyes().Length == 1 && Read(Script(), "PlannedFacing") is float, "CLL4 live gaze restored before watched Finish");
        Check(Read(Script(), "preparedArenaFacing") is float, "CLL4 disable precondition has captured nonempty facing");
        manager.SetMechanismEnabled(id, name, false); Update();
        Check(Read(Script(), "preparedArenaFacing") == null && Read(Script(), "preparedArenaOwner") == null
            && Read(Script(), "PlannedFacing") == null && Eyes().Length == 0 && Lines().Length == 0
            && Read(Snapshot(), "ArenaBoundary") == null, "CLL4 disabling clears nonempty captured facing and all owner outputs");
        manager.SetMechanismEnabled(id, name, true); Update();
        Check(Read(Script(), "preparedArenaFacing") is float && Eyes().Length == 1,
            "CLL4 enabling rebuilds live captured facing before actual Finish");
        Apply(4.74, [Helper()]);
        Check(Eyes().Length == 0 && Lines().Length == 0 && Read(Script(), "PlannedFacing") == null
            && Items(Model(), "Eyes").Length == 0, "CLL4 watched gaze Finish clears original eye and facing");
        Release();
        Console.WriteLine("PASS native CLL4 actual gaze both direction draw transition old AI facing new eye geometry replay isolation");

        load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                Helper(action: 20859)], true), World(1, [Helper(21, 20858, 1)]), World(10, [])]);
        Seek(1.3);
        var damage = Items(Script(), "PredictedDamage");
        Check(damage.Length == 2 && (string)Read(damage[0], "Type")! == "Tankbuster"
            && (DateTime)Read(damage[0], "Activation")! < (DateTime)Read(damage[1], "Activation")!,
            "CLL4 actual damage normalized independently of component order");
        Release();
        foreach (var upper in new[] { false, true })
        {
            load(id, [Header(920), World(0, [Boss(), Player((upper ? lyon : center) + Vector3.UnitX * 10),
                    Actor(101, 0, center, subKind: 4), Helper(action: 20830, point: lyon)], true),
                World(2, [Helper()]), World(3, [])]);
            Seek(.3);
            Check(Items(Script(), "PredictedDamage").Length == (upper ? 1 : 0), "CLL4 HeartOfNature Lyon gate");
            Seek(2.1); Check(Items(Script(), "PredictedDamage").Length == 0, "CLL4 HeartOfNature watched Finish");
            Release();
        }
        Console.WriteLine("PASS native CLL4 actual normalized damage ordering and HeartOfNature side gate real Finish");

        load(id, [Header(920), World(0, [Boss(), Player(lyon + Vector3.UnitX * 10),
                Actor(101, 0, center, subKind: 4), Helper()], true),
            World(1, [Helper(action: 20836, at: 1, point: lyon)]),
            World(2, [Actor(30, 0x1EB02F, lyon)]), World(3, [])]);
        Seek(1.1);
        Check(!((bool)Read(Model(), "Cll4DawonArena")!) && Aoes().Length == 1 && dangers() == 1,
            "CLL4 Lyon warning hot cast before wall");
        Seek(2.1);
        Check(Aoes().Length == 0 && dangers() == 0 && (float)Read(Model(), "SampleRadius")! == 20
            && (Vector3)Read(Model(), "Center")! == lyon, "CLL4 Lyon wall clears warning and installs exact reduced arena");
        Solver(lyon); Release();
        Console.WriteLine("PASS native CLL4 actual Lyon 20836 warning distinct wall clears and reduced arena solver");

        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, [Header(920), World(0, [Boss(), Player(), Actor(101, 0, center, subKind: 4),
                    Helper(action: 20855)], true), World(10, [])]);
            Seek(.3); radar.SetValue(settings, false); overlay.SetValue(settings, false);
            Apply(1, [Player(lyon + Vector3.UnitX * 10)]);
            Check((bool)Read(Model(), "Cll4DawonArena")! && dangers() == 1,
                "CLL4 both displays asleep preserve draw-owned arena gate and live planning");
            overlay.SetValue(settings, true); Update();
            Check(!(bool)Read(Model(), "Cll4DawonArena")! && Aoes().Length == 0 && dangers() == 1,
                "CLL4 waking display switches gate after old AI capture");
            Update(); Check(dangers() == 0, "CLL4 next Framework adopts awakened gate");
            manager.SetMechanismEnabled(id, name, false); Update();
            Check(Read(Script(), "preparedArenaOwner") == null && Read(Script(), "preparedArenaFacing") == null
                && Read(Snapshot(), "ArenaBoundary") == null && previews().Length == 0, "CLL4 disabled clears prepared owner and outputs");
            manager.SetMechanismEnabled(id, name, true);
            Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        var nonParty = JsonNode.Parse(World(1, [Actor(150, 0, lyon, subKind: 4)]))!;
        nonParty["data"]!["LocalPlayerId"] = 150;
        load(id, [Header(920), World(0, [Boss(), Player(lyon + Vector3.UnitX * 18),
                Actor(101, 0, center, subKind: 4), Helper(action: 20847, point: lyon)], true),
            nonParty.ToJsonString(), World(2, [])]);
        Seek(1.1);
        Check(previews().Length == 0 && Lines().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null
            && Items(Snapshot(), "GoalAreas").Length == 0 && Read(Script(), "preparedArenaOwner") == null
            && Read(Script(), "PlannedFacing") == null, "CLL4 nonParty clears every owner consumer");
        Release();
        Console.WriteLine("PASS native CLL4 actual both displays asleep wake old AI disabled prepared cache nonParty owner invalidation");
    }
}
