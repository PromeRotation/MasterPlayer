using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBaldesionArt(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.baldesion.art";
        const string hydatos = "masterplayer.foray.hydatos";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
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
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(e => (bool)Read(e, "IsArt")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Aoes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")
            || ((string)Read(p, "Id")!).Contains("bozja/spread/")).ToArray();
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaArtCatalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = (Vector3)Read(definition, "Center")!;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>()
                .Where(p => ((string)Read(p, "Id")!).Contains("bozja/art/chase/")).ToArray();
        }
        object Spell(uint action, double at = 0, float total = 5, Vector3? point = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, point ?? center, .47f, target, at))!;
            node["TotalTime"] = total; node["IsSpell"] = true; return node;
        }
        object Boss(uint hp = 1000, bool combat = true, object? cast = null, bool targetable = true)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x265A, center, combat: combat, cast: cast,
                targetable: targetable, subKind: 5, isEnemy: true))!;
            node["Hp"] = hp; return node;
        }
        object Helper(ulong actor = 20, uint action = 0, double at = 0, Vector3? point = null, float total = 5, ulong target = 100)
            => Actor(actor, 0x233C, point ?? center, cast: action == 0 ? null : Spell(action, at, total, point, target), subKind: 5);
        object Player(ulong actor = 100, Vector3? point = null, bool dead = false, float rotation = 0, object? cast = null)
            => Actor(actor, 0, point ?? center + Vector3.UnitX * (actor == 100 ? 10 : -10),
                subKind: 4, dead: dead, rotation: rotation, cast: cast);
        object Link() => Actor(40, 0x1EA1A1, new(-135, 0, 750), subKind: 7);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(827, at, actors, full, removed,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Icon(double at, ulong source = 100, ulong target = 100)
            => JsonSerializer.Serialize(new { t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = source, TargetId = target, MarkerId = 92, Timestamp = Start.AddSeconds(at) } });
        string Animation(double at, uint state)
            => JsonSerializer.Serialize(new { t = at, kind = "EObjAnimation", type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
                data = new { SourceId = 40UL, Param1 = (ushort)(state >> 16), Param2 = (ushort)state, Timestamp = Start.AddSeconds(at) } });
        string Effect(double at, uint action = 14637, ulong source = 20, ulong target = 0, Vector3? point = null)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = target,
                    TargetPosition = Pose(point ?? center), GlobalSequence = (uint)(at * 10000), Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Art actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
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
                "Art replay never requests PR facing targets or hold actions");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Art all owners released");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Art real solver without least-danger fallback");
        }
        void ChaseNavigation(bool own)
        {
            var chaser = Items(Model(), "ArtChasers").Single();
            var predicted = (Vector3)chaser.GetType().GetMethod("Predicted", flags)!.Invoke(chaser, null)!;
            var origin = predicted - (own ? Vector3.UnitZ * 10 : Vector3.Zero);
            var radius = own ? 16.5f : 11f;
            dynamic geometry = Read(Items(Snapshot(), "DangerAreas").Single(), "Geometry")!;
            for (var x = center.X - 25; x < center.X + 25; x += 2.371f)
                for (var z = center.Z - 25; z < center.Z + 25; z += 2.713f)
                    Check((bool)geometry.Contains(new Vector3(x, 0, z), 0f)
                        == ((x - origin.X) * (x - origin.X) + (z - origin.Z) * (z - origin.Z) <= radius * radius),
                        "Art actual navigation own16.5 back10 versus other11");
        }

        foreach (var (action, aoes, zones, damage, retained) in new (uint, int, int, int, int)[]
        {
            (14644, 0, 0, 1, 0), (14645, 0, 0, 1, 0), (14643, 1, 1, 0, 0),
            (14639, 1, 1, 0, 0), (14642, 3, 5, 0, 2), (14641, 1, 1, 0, 0),
        })
        {
            load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Helper(action: action, target: 101)], true),
                World(5, [Helper()]), World(6, [])]);
            Seek(.3);
            Check((bool)Read(Model(), "Active")! && Aoes().Length == aoes && dangers() == zones
                && Items(Script(), "PredictedDamage").Length == damage, "Art actual ten-component standard output " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Art generic suppression only actual NPC source");
            Solver(center + Vector3.UnitX * 20);
            Seek(5.1);
            Check(Aoes().Length == retained && dangers() == (retained == 0 ? 0 : 4)
                && Items(Script(), "PredictedDamage").Length == 0, "Art ordinary Finish and separate persistent predictions " + action);
            Seek(.3); manager.SetMechanismEnabled(id, "亚特", false); Seek(.4);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Art disable owner");
            manager.SetMechanismEnabled(id, "亚特", true); Seek(.3);
            Check(Aoes().Length == aoes, "Art reverse Seek rebuilds native outputs");
            Release();
            Console.WriteLine("PASS native Art actual component Finish persistent prediction solver suppression disable reverse Seek " + action);
        }

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Helper(),
                .. Enumerable.Range(30, 4).Select(i => Actor((ulong)i, 0x265B, center + Vector3.UnitZ * (i - 28)))], true),
            World(1, [Helper(action: 14631, at: 1)]), World(2, [Helper(action: 14632, at: 2)]),
            World(3, [Helper()], removed: [30]), Effect(4, 14635, source: 1), World(5, [])]);
        Seek(2.1);
        Check(Aoes().Length == 5 && dangers() == 5, "Art Myth four original spears plus caster");
        Seek(3.1); Check(Aoes().Length == 5 && dangers() == 5, "Art Finish and spear destruction retain queue");
        Seek(4.1); Check(Aoes().Length == 4 && dangers() == 4, "Art cross-source Effect FIFO");
        Solver(center + Vector3.UnitX * 20); Seek(2.1); Check(Aoes().Length == 5, "Art Myth reverse Seek");
        Release();
        Console.WriteLine("PASS native Art actual Myth five queue original spears Finish Destroy cross-source Effect solver");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Helper(action: 14642)], true),
            World(1, [Helper()]), Effect(2, 14643), World(50, []),
            World(51, [Helper(21, 14643, 51)]), World(52, [])]);
        Seek(50.1); Check(Aoes().Length == 2 && dangers() == 4, "Art crosses have no TTL or Effect cleanup");
        Seek(51.1); Check(Aoes().Length == 1 && dangers() == 1, "Art other caster Defilers Start clears two predictions");
        Release();
        Console.WriteLine("PASS native Art actual Geas persistent two Crosses no TTL Effect cleanup only Defilers Start");

        var stay = new[] { Header(827), World(0, [Boss(), Player(), Player(101), Link()], true),
            Animation(1, 0x00010002), World(1.1, []), World(10, []) };
        load(id, stay); Seek(1.2);
        Apply(2, [Player(point: center + Vector3.UnitX * 14, cast: Spell(999, 2))]);
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Don't move!（已满足）") == true
            && ((string)Read(Script(), "MechanicHintsText")!).Contains("NoMovement"), "Art Stay2 permits attack and diagnoses movement");
        Apply(2.5, [Player(dead: true)]);
        Check(((DateTime[])Read(Model(), "ArtStay")!)[0] == default
            && !((string)Read(Script(), "MechanicHintsText")!).Contains("Don't move!")
            && ((string)Read(Script(), "MechanicHintsText")!).Contains("NoMovement"), "Art first Hint clears dead slot but captured mode survives");
        var captured = Read(Script(), "MechanicHintsText");
        Clock(2.56); Render(); Check(Equals(Read(Script(), "MechanicHintsText"), captured), "Art event Render retains published Framework mode");
        Update(); Check(Read(Script(), "MechanicHintsText") == null, "Art next Framework clears published mode");
        Release();
        Console.WriteLine("PASS native Art actual Stay2 attacks allowed dead two Hint queries Framework diagnostic cache");
        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, stay); Seek(1.2);
            radar.SetValue(settings, false); overlay.SetValue(settings, false);
            Apply(2, [Player(dead: true)]);
            Check(((DateTime[])Read(Model(), "ArtStay")!)[0] != default, "Art both views asleep do not invoke Stay Hint");
            Apply(2.5, [Player()]); overlay.SetValue(settings, true); Render();
            Check(((DateTime[])Read(Model(), "ArtStay")!)[0] != default
                && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Don't move!") == true, "Art waking draw keeps retained Stay");
            Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        Console.WriteLine("PASS native Art actual both views asleep death revive preserve Stay slot");

        load(id, [Header(827), World(0, [Boss(combat: false), Player(), Player(101), Link()], true),
            Animation(.5, 0x00010002), Icon(.6), World(1, [Boss(), Helper(action: 14636, at: 1)]), World(2, [])]);
        Seek(1.1); Check(((DateTime[])Read(Model(), "ArtStay")!).All(t => t == default)
            && Items(Model(), "ArtChasers").Length == 0, "Art cold history never reconstructs EAnim or icons");
        Release();
        Console.WriteLine("PASS native Art actual cold activation no EAnim icon recovery");

        load(id, [Header(827), World(0, [Boss(), Player(point: center + new Vector3(10, 3, 0)),
                Player(101, center - Vector3.UnitX * 10), Helper()], true), Icon(.5, 101, 100),
            World(1, [Helper(action: 14636, at: 1, target: 100)]),
            World(2, [Helper()], removed: [101]),
            World(2.1, [Player(101, center + Vector3.UnitZ * 25)]),
            Effect(3, source: 1, target: 101, point: center - Vector3.UnitZ * 25), World(4, [])]);
        Seek(1.1);
        var chaser = Items(Model(), "ArtChasers").Single();
        var original = Read(chaser, "Target")!;
        Check(Lines().Length == 1 && (Vector3)Read(Lines()[0], "End")! == center + new Vector3(-10, 3, 0)
            && (ulong)Read(Read(original, "Actor")!, "Id")! == 101
            && (ulong)Read(Model(), "ArtChaseTargetsMask")! == 1, "Art source original target foreground and distinct event target mask");
        Check((float)Read(Read(Aoes().Single(), "Geometry")!, "Radius")! == 10 && dangers() == 1,
            "Art displayed chasing radius remains10");
        ChaseNavigation(false);
        Solver(center + Vector3.UnitZ * 20);
        Apply(2, [Helper()], removed: [101]);
        Apply(2.1, [Player(101, center + Vector3.UnitZ * 25)]);
        Check(ReferenceEquals(Read(Items(Model(), "ArtChasers").Single(), "Target"), original)
            && (bool)Read(original, "Destroyed")! && (Vector3)Read(Lines().Single(), "End")! == center + new Vector3(-10, 3, 0),
            "Art same ID replacement does not redirect original target line before first hit");
        Seek(3.1); Check(Items(Model(), "ArtChasers").Length == 0 && Lines().Length == 0 && Aoes().Length == 0,
            "Art real Effect advances then dead original target Update removes");
        Release();
        Console.WriteLine("PASS native Art actual icon source target mask original Actor line same ID rebuild Finish Effect solver");

        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Helper()], true), Icon(.5),
            World(1, [Helper(action: 14636, at: 1, total: .01f)]),
            Effect(2, action: 14636, target: 20, point: center + Vector3.UnitZ * 25),
            Effect(3, target: 9999, point: center + Vector3.UnitZ * 13),
            Effect(4), Effect(5), Effect(6), World(7, [])]);
        Seek(1.01); Check((DateTime)Read(Items(Model(), "ArtChasers").Single(), "Activation")!
            == Start.AddSeconds(1).AddSeconds(.01f + .3f), "Art DLL exact short NPC clock");
        ChaseNavigation(true);
        Seek(2.1);
        var expected = (Vector3)assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonEncounter")!
            .GetMethod("QuantizedDungeonPosition", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [center])!;
        Check((Vector3)Read(Items(Model(), "ArtChasers").Single(), "Previous")! == expected, "Art self target uses quantized caster not packet");
        Seek(3.1); Check((Vector3)Read(Items(Model(), "ArtChasers").Single(), "Previous")! == center + Vector3.UnitZ * 13,
            "Art missing target uses real packet TargetPosition");
        Seek(6.1); Check(Aoes().Length == 0 && Lines().Length == 0 && dangers() == 0
            && (int)Read(Model(), "ArtChaseCount")! == 0, "Art fifth actual Effect resets Targets Count");
        Release();
        Console.WriteLine("PASS native Art actual short clock self quantized missing target raw five Effects reset");

        var nonParty = JsonNode.Parse(World(2, [Player(150)]))!;
        nonParty["data"]!["LocalPlayerId"] = 150;
        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Helper()], true),
            Icon(.5), World(1, [Helper(action: 14636, at: 1)]), nonParty.ToJsonString(), World(3, [])]);
        Seek(2.1); Check(previews().Length == 0 && Lines().Length == 0 && dangers() == 0
            && Read(Script(), "MechanicHintsText") == null && Read(Snapshot(), "ArenaBoundary") == null,
            "Art non Party perspective clears all consumers");
        Release();
        Console.WriteLine("PASS native Art actual non Party perspective clears navigation foreground hint boundary");

        load(id, [Header(827), World(0, [Boss(), Player(point: center + Vector3.UnitX * 100), Player(101), Helper(action: 14639)], true),
            World(1, [Player(point: center + Vector3.UnitX * 99.99f)]),
            World(2, [Boss(combat: false, targetable: false), Player(point: center + Vector3.UnitX * 120)]),
            World(3, [Boss(hp: 0)]), World(4, [])]);
        Seek(.1); Check(!(bool)Read(Model(), "Active")!, "Art strict100 pull");
        Seek(1.1); Check((bool)Read(Model(), "Active")! && Aoes().Length == 1 && Script().GetType().GetMethod("GetTimeline")!.Invoke(Script(), null) != null,
            "Art cold active cast and timeline");
        Seek(2.1); Check((bool)Read(Model(), "Active")!, "Art no post-pull range combat reset");
        Seek(3.1); Check(!(bool)Read(Model(), "Active")! && previews().Length == 0 && dangers() == 0
            && Script().GetType().GetMethod("GetTimeline")!.Invoke(Script(), null) == null, "Art HP0 clears timeline and output");
        Release();
        Console.WriteLine("PASS native Art actual strict pull cold cast timeline no reset HP0 release");

        manager.SetScriptEnabled(hydatos, true);
        load(id, [Header(827), World(0, [Boss(), Player(), Player(101), Helper(action: 14639)], true), World(5, [])]);
        Seek(.3);
        var host = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var bridge = RuntimeHelpers.GetUninitializedObject(host);
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        var hydatosOwns = assembly.GetType("MasterPlayer.Mechanics.Hydatos.HydatosCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check(manager.Running.ContainsKey(hydatos) && (bool)owns.Invoke(null, [827u, 0x265Au])!
            && !(bool)owns.Invoke(null, [827u, 0x2658u])! && !(bool)owns.Invoke(null, [827u, 0x2765u])!
            && (bool)hydatosOwns.Invoke(null, [827u, 0x2765u])! && !(bool)hydatosOwns.Invoke(null, [827u, 0x265Au])!
            && !(bool)host.GetMethod("IsReplaySuppressed", flags)!.Invoke(bridge, null)!,
            "Art Hydatos distinct OID ownership same territory without whole territory suppression");
        manager.SetScriptEnabled(hydatos, false);
        Check(manager.Running.ContainsKey(id) && Aoes().Length == 1 && dangers() == 1, "Hydatos disable preserves Art owner");
        Release();
        manager.SetScriptEnabled(hydatos, true);
        load(id, [Header(827), World(0, [Actor(2, 0x2765, center, combat: true, subKind: 5, isEnemy: true), Player(),
                Player(101), Helper(action: 15468)], true), World(5, [])]);
        Seek(.3);
        Check(manager.Running.ContainsKey(hydatos) && !(bool)Read(Model(), "Active")! && dangers() == 1
            && previews().Count(p => ((string)Read(p, "Id")!).Contains("hydatos/aoe/")) == 1, "Ceto retains own native output while Art dormant");
        manager.SetScriptEnabled(id, false);
        Check(manager.Running.ContainsKey(hydatos) && dangers() == 1, "Art disable preserves Hydatos owner");
        stop();
        manager.SetScriptEnabled(id, true);
        Console.WriteLine("PASS native Art actual Hydatos coexistence exact OID ownership no whole territory suppression independent release");
    }
}
