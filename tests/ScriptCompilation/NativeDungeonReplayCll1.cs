using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCLL1Brionac(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string name = "布里欧纳克";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var bottom = new Vector3(80, 0, -179.41f);
        var top = new Vector3(80, 0, -222);
        object? Read(object obj, string key)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(key, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(key, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, key);
        }
        FieldInfo Field(object obj, string key)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
                if (type.GetField(key, flags) is { } f) return f;
            throw new MissingFieldException(obj.GetType().Name, key);
        }
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(e => (bool)Read(e, "IsCll1")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object State() => Read(Model(), "Cll1")!;
        object[] Aoes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var visibility = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        }
        object Spell(uint action, Vector3 origin, double at = 0, ulong target = 100, float rotation = .4f)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, origin, rotation, target, at))!;
            node["TotalTime"] = 5; node["IsSpell"] = true; return node;
        }
        object Boss(bool combat = true, bool dead = false)
            => Actor(1, 0x2ECC, top, combat: combat, dead: dead, subKind: 5, objectIndex: 2, isEnemy: true);
        object Helper(ulong actor, Vector3 origin, uint action = 0, double at = 0, Vector3? castAt = null, ulong target = 100)
            => Actor(actor, 0x233C, origin, cast: action == 0 ? null : Spell(action, castAt ?? origin, at, target), subKind: 5);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(920, at, actors, full, removed,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, ulong source, uint action, uint sequence)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionType = 1, ActionId = action, GlobalSequence = sequence, MainTargetId = 100, Timestamp = Start.AddSeconds(at) } });
        string Tether(double at, ulong source, ushort tid, ulong target)
            => JsonSerializer.Serialize(new { t = at, kind = target == 0 ? "TetherCancel" : "Tether",
                type = target == 0 ? typeof(LogSystemTetherCancelEvent).FullName : typeof(LogSystemTetherEvent).FullName,
                data = new { SourceId = source, TargetId = target, TetherId = tid, Timestamp = Start.AddSeconds(at) } });
        string Icon(double at, ulong source, uint icon, ulong target)
            => JsonSerializer.Serialize(new { t = at, kind = "TargetIcon", type = typeof(LogSystemTargetIconEvent).FullName,
                data = new { SourceId = source, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CLL1 actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "CLL1 priorities remain diagnostic and replay never executes PR requests");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL1 owner cleanup");
        }
        void Render(bool framework)
        {
            var script = Script();
            var field = Field(script, "frameworkDrawing");
            var before = field.GetValue(script);
            try
            {
                field.SetValue(script, framework);
                script.GetType().BaseType!.GetMethod("Render", flags)!.Invoke(script, null);
            }
            finally { field.SetValue(script, before); }
        }
        void ObserveAs(ulong actor)
        {
            var model = Model();
            var viewer = ((IDictionary)Read(model, "Actors")!)[actor]!;
            model.GetType().GetMethod("ObserveCll1Drawing", flags)!.Invoke(model, [viewer]);
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "CLL1 real solver without least-danger fallback");
        }

        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCll1Catalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        foreach (var rule in Items(definition, "Rules").Where(r => Read(r, "Kind")!.ToString() != "Cll1Custom"))
            foreach (var action in (uint[])Read(rule, "Actions")!)
            {
                var component = (string)Read(rule, "Component")!;
                var isTop = component is "ElectricAnvil" or "Voltstream" or "FalseThunder" or "LightningShower";
                var center = isTop ? top : bottom;
                var damage = Read(rule, "Kind")!.ToString() is "Tankbuster" or "Raidwide";
                var destination = component == "CommandSuppressiveFormation" ? center + Vector3.UnitZ * 20 : center;
                load(id, [Header(920), World(0, [Boss(), Actor(100, 0, center), Actor(101, 0, center),
                        Helper(20, center, action, castAt: destination)], true), World(1, [Helper(20, center)]), World(2, [])]);
                Seek(.3);
                Check((bool)Read(Model(), "Active")! && Aoes().Length == (damage ? 0 : 1)
                    && dangers() == (damage ? 0 : 1) && Items(Script(), "PredictedDamage").Length == (damage ? 1 : 0),
                    "CLL1 actual component display navigation damage " + action);
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                    "CLL1 actual generic suppression");
                Solver(center);
                Seek(1.1); Check(Aoes().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0, "CLL1 watched Finish");
                Seek(.3); manager.SetMechanismEnabled(id, name, false); Seek(.4);
                Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL1 mechanism disable cleanup");
                manager.SetMechanismEnabled(id, name, true); Seek(.5); Release();
                Console.WriteLine("PASS native CLL1 actual DLL standard solver Finish disable reverse Seek owner " + action);
            }

        // Arm the previous bottom draw state while the actual replay perspective
        // is already top. Invoke the production render boundary exactly once.
        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, top), Actor(101, 0, bottom),
            Helper(20, top, 20955), Helper(21, bottom, 20984), Helper(22, top, 20957),
            Helper(23, bottom, 20991, target: 101)], true), World(2, [])]);
        Seek(.3);
        ObserveAs(101);
        Render(true);
        dynamic oldBoundary = Read(Snapshot(), "ArenaBoundary")!;
        Check((Vector3)Read(Model(), "Center")! == top && (bool)oldBoundary.Contains(bottom, 0f)
            && !(bool)oldBoundary.Contains(top, 0f), "CLL1 first frame keeps old bottom navigation boundary and new top drawing");
        Check(Aoes().Length == 1 && Items(Script(), "PredictedDamage").Single() is { } firstDamage
            && (ulong)Read(firstDamage, "Mask")! == 2, "CLL1 old bottom damage despite top display");
        Render(false);
        Check((bool)((dynamic)Read(Snapshot(), "ArenaBoundary")!).Contains(bottom, 0f)
            && (ulong)Read(Items(Script(), "PredictedDamage").Single(), "Mask")! == 2,
            "CLL1 extra event Render cannot overwrite prepared old navigation and damage");
        Render(true);
        Check((bool)((dynamic)Read(Snapshot(), "ArenaBoundary")!).Contains(top, 0f)
            && (ulong)Read(Items(Script(), "PredictedDamage").Single(), "Mask")! == 1,
            "CLL1 next Framework adopts top navigation and damage");
        Release();
        Console.WriteLine("PASS native CLL1 actual first old navigation boundary damage then new drawing text extra Render and next Framework");

        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, bottom), Actor(101, 0, bottom), Helper(20, bottom, 20984)], true),
            World(1, [Helper(20, bottom)]), World(2, [])]);
        Seek(.3); Check(dangers() == 1, "CLL1 prepared hazard exists before disable");
        manager.SetMechanismEnabled(id, name, false); Render(false);
        Seek(1.1);
        manager.SetMechanismEnabled(id, name, true); Render(false);
        Check(dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null && Items(Script(), "PredictedDamage").Length == 0,
            "CLL1 re-enable extra Render cannot republish expired prepared snapshot");
        Render(true); Check(dangers() == 0, "CLL1 next Framework rebuilds valid empty navigation");
        Release();
        Console.WriteLine("PASS native CLL1 actual disable Finish reenable extra Render invalidates prepared navigation damage owner");

        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, [Header(920), World(0, [Boss(combat: false), Actor(100, 0, top), Actor(101, 0, bottom)], true), World(2, [])]);
            Seek(.3); ObserveAs(101);
            radar.SetValue(settings, false); overlay.SetValue(settings, false); Render(true);
            Check(!(bool)Read(State(), "Top")! && !(bool)Read(Model(), "Active")! && dangers() == 0, "CLL1 all drawing disabled does not change constructor arena");
            overlay.SetValue(settings, true); Render(true);
            Check((bool)Read(State(), "Top")! && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CLL1 overlay-only constructor draw switches without navigation");
            ObserveAs(101); overlay.SetValue(settings, false); radar.SetValue(settings, true); Render(true);
            Check((bool)Read(State(), "Top")!, "CLL1 world-only constructor draw switches");
            Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        Console.WriteLine("PASS native CLL1 actual constructor arena drawing off radar world independent no prephase planning");

        var chain = new List<string> { Header(920), World(0, [Boss(), Actor(100, 0, bottom), Actor(101, 0, bottom),
            Helper(20, bottom, 20985)], true), World(1, [Helper(20, bottom)]) };
        for (var i = 0; i < 7; ++i) chain.Add(Effect(2 + i, 20, i == 0 ? 20985u : 20986u, (uint)(i + 1)));
        chain.Add(World(9, [])); load(id, chain);
        Seek(1.1); Check(Aoes().Length == 1 && dangers() == 1, "CLL1 Chain Finish retains AOE");
        Seek(7.1); Check(Aoes().Length == 1, "CLL1 Chain six Effects retain");
        Seek(8.1); Check(Aoes().Length == 0 && dangers() == 0, "CLL1 Chain seventh real packet consumes");
        Seek(3.1); Check(Aoes().Length == 1, "CLL1 Chain reverse Seek rebuilds hit count"); Release();
        Console.WriteLine("PASS native CLL1 actual ChainCannon seven native Effects Finish retained reverse Seek");

        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, top), Actor(101, 0, bottom),
                Actor(30, 0x2ECD, new(63, 0, -222), subKind: 5), Helper(20, top)], true),
            Icon(1, 30, 162, 30), Icon(2, 20, 231, 100), Tether(3, 100, 124, 30),
            Icon(4, 30, 162, 30), Effect(5, 20, 20952, 1), World(6, [])]);
        Seek(3.1);
        Check(((IEnumerable)Read(State(), "MagnetSources")!).Cast<object?>().All(s => s == null), "CLL1 actual Tether124 does not initialize on its own");
        Seek(4.1);
        Check(((IEnumerable)Read(State(), "MagnetSources")!).Cast<object?>().Any(s => s != null)
            && Lines().Any(p => ((string)Read(p, "Id")!).Contains("cll1"))
            && Items(Snapshot(), "GoalAreas").Length == 1, "CLL1 actual icon initializes direct complement goal and movement line");
        Seek(5.1); Check((ulong)Read(State(), "MagnetTethered")! == 0 && Items(State(), "Poles").Length == 0, "CLL1 actual effect clears magnet state");
        Release();
        Console.WriteLine("PASS native CLL1 actual orbital AOE Icon Tether readiness and magnet native Effect cleanup");

        var outside = bottom + new Vector3(0, 0, 20);
        var chargeStart = bottom - new Vector3(24, 0, 0);
        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, outside), Actor(101, 0, bottom),
                .. Enumerable.Range(0, 3).Select(i => Helper((ulong)(20 + i), chargeStart + Vector3.UnitZ * ((i - 1) * 10),
                    20977, castAt: bottom + new Vector3(24, 0, (i - 1) * 10)))], true),
            World(1, Enumerable.Range(0, 3).Select(i => Helper((ulong)(20 + i), chargeStart))), World(2, [])]);
        Seek(.3);
        Check(Aoes().Length == 3 && Aoes().All(a => (bool)Read(a, "Safe")!) && dangers() > 0 && Items(Snapshot(), "GoalAreas").Length == 0,
            "CLL1 safe charge display plus sampled inverted intersection danger, no invented stack goal");
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Share damage inside wildcharge!") == true, "CLL1 actual share text");
        Solver(outside); Seek(1.1); Check(Aoes().Length == 0 && dangers() == 0, "CLL1 actual FIFO charge Finish"); Release();
        Console.WriteLine("PASS native CLL1 actual WildCharges safe view sampled intersection real solver and FIFO Finish");

        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, bottom), Actor(101, 0, bottom),
                Actor(3, 0x2ED7, bottom, hitboxRadius: 7.8f, subKind: 5), Helper(20, bottom + new Vector3(20, 0, 0))], true),
            Tether(1, 100, 121, 20), Tether(2, 100, 121, 20), Tether(3, 100, 0, 0),
            Effect(4, 20, 20974, 1), World(5, [])]);
        Seek(1.1); Check(Items(State(), "InfraredTethers").Length == 1 && Items(State(), "InfraredPlayers").Length == 2,
            "CLL1 real first Infrared sides then player capture");
        Seek(3.1); Check(Items(State(), "InfraredTethers").Length == 1, "CLL1 real reclassified tuple survives Untether");
        Seek(4.1); Check(Items(State(), "InfraredTethers").Length == 1 && (DateTime)Read(State(), "InfraredActivation")! == default
            && Lines().Length == 1 && dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0,
            "CLL1 real Infrared Effect retains raw default clock and line while legacy dense complement sampling stays empty");
        Release();
        Console.WriteLine("PASS native CLL1 actual Infrared first-side cache duplicate untether foreground retained default activation navigation");

        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, bottom), Actor(101, 0, bottom),
                Actor(2, 0x2ED3, bottom, subKind: 5), Actor(40, 0x2F4E, top, subKind: 5)], true),
            World(1, [Boss(dead: true), Actor(2, 0x2ED3, bottom, dead: true, subKind: 5)]),
            World(2, [Actor(40, 0x2F4E, top, dead: true, subKind: 5)]), World(3, [])]);
        Seek(1.1); Check((bool)Read(Model(), "Active")!, "CLL1 SkyArmor keeps phase after both bosses dead");
        Seek(2.1); Check(!(bool)Read(Model(), "Active")! && previews().Length == 0 && dangers() == 0, "CLL1 all original bosses and SkyArmor finish");
        Seek(.3); Check((bool)Read(Model(), "Active")!, "CLL1 reverse Seek restores original lifecycle"); Release();
        Console.WriteLine("PASS native CLL1 actual two boss SkyArmor completion reverse Seek owner replay isolation");
    }
}
