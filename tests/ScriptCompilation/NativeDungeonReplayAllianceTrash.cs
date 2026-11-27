using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAllianceTrash(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        RunDeathgazeReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunArachneReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunIrminsulReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunOzmaReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunAllianceRhalgr(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunAllianceArtillery(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunLightArtilleryReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunAllianceStandardReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunAllianceStandardReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers, knockbacks: true);
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
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
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var catalogType = assembly.GetType("MasterPlayer.Mechanics.Bozja.AllianceTrashCatalog")!;
        var definitions = ((IEnumerable)catalogType.GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!).Cast<object>().Append(catalogType.GetField("Lions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).ToArray();
        var ids = new[] { "vanguard", "aquarius", "groundskeeper", "despot", "serpent", "angelosfirst", "angelossecond", "detector", "jack", "kraken", "lions" };
        for (var index = 0; index < definitions.Length; ++index)
        {
            var definition = definitions[index];
            var id = "masterplayer.alliance." + ids[index];
            var territory = (uint)Read(definition, "Territory")!;
            var oid = (uint)Read(definition, "PrimaryOid")!;
            var center = (Vector3)Read(definition, "Center")!;
            var name = (string)Read(definition, "Name")!;
            object Script() => Read(manager.Running[id], "Script")!;
            object Model() => Items(Read(Script(), "region")!, "Encounters").Single();
            object[] Views() => previews().Where(p => !((string)Read(p, "Id")!).EndsWith("bozja/arena", StringComparison.Ordinal)).ToArray();
            object Boss(bool combat = true, bool dead = false)
                => Actor(1, oid, center, combat: combat, dead: dead, subKind: 5);
            object Player(ulong actor = 100) => Actor(actor, 0,
                (center + Vector3.UnitX * 10) with { Y = index == 5 ? 17.25f : 0 }, subKind: 4);
            object Npc(uint action = 0, ulong actor = 20, Vector3? point = null, double at = 0,
                float total = 5, bool isSpell = true, ulong target = 100)
            {
                JsonNode? cast = action == 0 ? null : JsonSerializer.SerializeToNode(Cast(action, total, point ?? center, .47f, target, at));
                if (cast != null) { cast["TotalTime"] = total; cast["IsSpell"] = isSpell; }
                return Actor(actor, 0x233C, point ?? center, cast: cast, subKind: 5);
            }
            string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null, bool party = true)
                => Frame(territory, at, actors, full, removed, party: party
                    ? [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }] : [], sourcePlayerId: 100);
            void Seek(double at)
            {
                controller.Seek(at);
                Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                    "Alliance actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
            }
            void Release()
            {
                var hints = Read(Script(), "Hints")!;
                Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                    && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                    "Alliance replay never executes PR facing target or action hold");
                stop();
                Check(dangers() == 0 && previews().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                    && Read(Snapshot(), "ArenaBoundary") == null, "Alliance owner release");
            }
            void Solver(Vector3 origin)
            {
                var snapshot = Snapshot();
                dynamic boundary = Read(snapshot, "ArenaBoundary")!;
                var radius = (float)Read(definition, "SampleRadius")!;
                var starts = new List<Vector3>();
                for (var x = center.X - radius; x <= center.X + radius; x += 1)
                    for (var z = center.Z - radius; z <= center.Z + radius; z += 1)
                    {
                        var candidate = new Vector3(x, 0, z);
                        if ((bool)boundary.Contains(candidate, 0f)) starts.Add(candidate);
                    }
                Check(starts.Count > 0, "Alliance true arena contains valid solver origins");
                origin = starts.MinBy(p => Vector3.DistanceSquared(p, origin));
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [origin, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!,
                    "Alliance actual solver safe route");
            }
            foreach (var rule in Items(definition, "Rules").Where(r => (string)Read(r, "Component")! is not ("ScraplineTyphoon" or "Flood")))
            {
                var component = (string)Read(rule, "Component")!;
                var kind = Read(rule, "Kind")!.ToString()!;
                foreach (var action in (uint[])Read(rule, "Actions")!)
                {
                    var phase1 = index == 4 && component != "WaterIII";
                    load(id, [Header(territory), World(0, [Boss(false), Player(), Player(101), Npc(action)], true),
                        World(1, [Boss(), Actor(22, 0x4011, center)]),
                        World(2, phase1 ? [Boss(dead: true)] : []), World(6, [Npc()]), World(7, [])]);
                    Seek(.3);
                    Check(dangers() == 0 && previews().Length == 0, "Alliance inactive cold input has no output");
                    Seek(2.1);
                    Check((bool)Read(Model(), "Active")!, "Alliance source phase active");
                    var matching = Items(definition, "Rules").Where(r => ((uint[])Read(r, "Actions")!).Contains(action)).ToArray();
                    var expectedAoes = matching.Sum(r => Read(r, "Kind")!.ToString() switch { "CastGaze" => 2, "Aoe" => 1, _ => 0 });
                    var expectedZones = matching.Sum(r => Read(r, "Kind")!.ToString() switch
                    {
                        "Aoe" when Read(r, "Shape")?.GetType().Name == "DungeonCrossTemplate" => 2,
                        "Aoe" or "AlliancePull" => 1,
                        _ => 0,
                    });
                    Check(Views().Length == expectedAoes && previews().Length == expectedAoes + 1 && dangers() == expectedZones
                        && Read(Snapshot(), "ArenaBoundary") != null,
                        $"Alliance {ids[index]}/{component} real view, boundary and navigation: {previews().Length}/{dangers()}");
                    if (index == 5)
                    {
                        dynamic arena = Read(Snapshot(), "ArenaBoundary")!;
                        Check(!(bool)arena.Contains(new Vector3(811.25f, 0, 787), 0f)
                            && !(bool)arena.Contains(new Vector3(793.4f, 0, 762.75f), 0f)
                            && (bool)arena.Contains(center, 0f) && (float)arena.Position.Y == 17.25f,
                            "Alliance actual raised arena retains square and round holes with connected safe path");
                    }
                    if (kind is "Tankbuster" or "Raidwide")
                        Check(Items(Script(), "PredictedDamage").Length == 1
                            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains(kind == "Tankbuster" ? "Tankbuster" : "Raidwide") == true,
                            "Alliance actual single target global hint and diagnostic");
                    Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                        && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                        "Alliance suppression requires NPC and matching action");
                    if (kind == "Aoe") Solver(center + Vector3.UnitX * 10);
                    manager.SetMechanismEnabled(id, name, false); Seek(2.2);
                    Check(dangers() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                        "Alliance mechanism switch releases owner");
                    manager.SetMechanismEnabled(id, name, true); Seek(2.1);
                    Check(Views().Length == expectedAoes, "Alliance reverse Seek rebuilds cold output");
                    Seek(6.1);
                    Check(matching.Any(r => Read(r, "Kind")!.ToString() == "AlliancePull") ? dangers() == 1 && Views().Length == 1
                        : dangers() == 0 && Views().Length == 0, "Alliance actual watched Finish retains only custom queue");
                    Release();
                    Console.WriteLine($"PASS native Alliance trash actual cold Finish solver owner reverse Seek {ids[index]}/{component}/{action}");
                }
            }
            if (index == 3)
            {
                load(id, [Header(territory), World(0, [Boss(), Player(), Npc(40650)], true),
                    World(1, [Npc()]), World(2, [Npc(41393, 21, at: 2)]), World(3, [Npc(actor: 21)]),
                    World(4, [Npc(41902, 22, at: 4)]), World(5, [Npc(actor: 22)]), World(6, [])]);
                Seek(.3); Check(Views().Length == 1 && dangers() == 2, "Alliance Despot first circle and custom pull AI");
                Seek(1.1); Check(Views().Length == 1 && dangers() == 1, "Alliance Storm Finish retains two-stage FIFO");
                Seek(3.1);
                Check(Views().Length == 1 && Read(Views()[0], "Geometry")!.GetType().Name == "DonutGeometry", "Alliance cross-source FIFO promotes donut");
                Seek(5.1); Check(Views().Length == 0 && dangers() == 0, "Alliance Typhoon cross-source Finish clears");
                Seek(.3); Check(Views().Length == 1, "Alliance Despot reverse Seek restores head");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Despot two clocks cross-source Finish FIFO");
                load(id, [Header(territory), World(0, [Boss(), Player(), Player(101), Npc(41698, total: 0, target: 101)], true),
                    World(1, [Npc(999, at: 1, total: .02f, isSpell: false, target: 101)]), World(2, [Npc()]), World(3, [])]);
                Seek(.1);
                Check(Items(Script(), "PredictedDamage").Length == 1, "Alliance actual zero raw Total produces prediction");
                var model = Model();
                var tank = Items(definition, "Rules").Single(r => (string)Read(r, "Component")! == "Panzerfaust");
                var caster = ((IDictionary)Read(model, "Actors")!)[20UL]!;
                model.GetType().GetMethod("Start", flags, [caster.GetType(), tank.GetType()])!.Invoke(model, [caster, tank]);
                void ApplyRaw(double at, object npc)
                {
                    // Seek rebuilds the session and would discard the deliberate
                    // duplicate callback. Commit the next recorded frame in place.
                    var world = Read(Script(), "ReplayWorld")!;
                    var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
                        .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    var data = JsonNode.Parse(World(at, [npc]))!["data"]!.ToJsonString();
                    world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                        [JsonSerializer.Deserialize(data, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
                    world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(at));
                    Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
                }
                ApplyRaw(1.1, Npc(999, at: 1, total: .02f, isSpell: false, target: 101));
                var damage = Items(Script(), "PredictedDamage");
                Check(damage.Length == 1 && (ulong)Read(damage[0], "Mask")! == 2
                    && (DateTime)Read(damage[0], "Activation")! == Start.AddSeconds(1.1).AddSeconds((.02f + .3f) - .02f),
                    "Alliance actual duplicate residue consumes current non Spell raw clock and target");
                ApplyRaw(2.1, Npc());
                Check(Items(Script(), "PredictedDamage").Length == 0 && Items(Model(), "Casts").Length == 1,
                    "Alliance raw null hides prediction while preserving residual original caster");
                Release();
                Console.WriteLine("PASS native Alliance trash actual raw zero NPC non Spell original caster prediction");
            }
            if (index == 4)
            {
                load(id, [Header(territory), World(0, [Boss(), Player(), Actor(22, 0x4011, center)], true),
                    World(1, [Boss(dead: true), Npc(35439)]), World(2, [], removed: [1]),
                    World(3, [Actor(22, 0x4011, center, dead: true)]), World(4, [])]);
                Seek(1.1); Check((int)Read(Model(), "AlliancePhase")! == 1 && dangers() == 1, "Alliance serpent dead body retains active P1");
                Seek(2.1); Check((bool)Read(Model(), "Active")!, "Alliance live Triton preserves phase after primary Destroy");
                Seek(3.1); Check(!(bool)Read(Model(), "Active")! && dangers() == 0 && previews().Length == 0,
                    "Alliance final all adds dead and no serpent withdraw output");
                Release();
                Console.WriteLine("PASS native Alliance trash actual serpent two phases dead body destroy final withdrawal");
            }
            if (index == 9)
            {
                load(id, [Header(territory), World(0, [Boss(), Player(), Npc(43791)], true),
                    World(1, [Npc(43791, 22, center + Vector3.UnitX * 7, at: 1)]),
                    World(2, [Npc(), Npc(actor: 22)]),
                    World(100, []), World(101, [Npc(43792, 23, at: 101)]),
                    World(102, [Npc(actor: 23)]), World(103, [])]);
                Seek(.3);
                var flood = Items(Model(), "Casts").Single(c => (string)Read(Read(c, "Rule")!, "Component")! == "Flood");
                Check((DateTime)Read(flood, "Activation")! == Start.AddSeconds((double)(5f + .3f) + 3.5d)
                    && dangers() == 2, "Kraken actual Flood precise delay and first custom pull");
                Seek(1.1);
                var floods = Items(Model(), "Casts").Where(c => (string)Read(Read(c, "Rule")!, "Component")! == "Flood").ToArray();
                Check(floods.Length == 1 && (ulong)Read(Read(Read(floods[0], "Source")!, "Actor")!, "Id")! == 22
                    && dangers() == 2, "Kraken actual Flood replaces single source and AI still only first pull");
                Seek(100.1);
                Check(dangers() == 1 && Views().Length == 1, "Kraken actual both Sucker Finish and timeout preserve Flood");
                Seek(101.1); Check(dangers() == 1, "Kraken actual Flood Start does not duplicate prediction");
                Seek(102.1); Check(dangers() == 0 && Views().Length == 0, "Kraken actual cross source Flood Finish clears");
                Seek(.3); Check(dangers() == 2, "Kraken actual reverse Seek restores custom output");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Kraken overwrite Flood cross-source Finish no TTL");

                load(id, [Header(territory), World(0, [Boss(), Player(), Actor(22, 0x488B, center)], true),
                    World(1, [Boss(dead: true)]), World(2, [], removed: [1]),
                    World(3, [Actor(22, 0x488B, center, dead: true)]), World(4, [])]);
                Seek(1.1); Check((bool)Read(Model(), "Active")!, "Kraken actual dead P1 primary still blocks AllDestroyed");
                Seek(2.1); Check((bool)Read(Model(), "Active")!, "Kraken actual surviving P2 blocks after P1 removal");
                Seek(3.1); Check(!(bool)Read(Model(), "Active")!, "Kraken actual destroyed P1 and dead P2 complete");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Kraken P1 destroyed and P2 dead distinct completion");

                load(id, [Header(territory), World(0, [Boss(), Player(), Npc(43566, total: 0)], true),
                    World(1, [Npc(999, at: 1, total: .02f, isSpell: false)]), World(2, [Npc()]), World(3, [])]);
                Seek(.1);
                var model = Model();
                var rule = Items(definition, "Rules").Single(r => (string)Read(r, "Component")! == "ImpactRoar");
                var caster = ((IDictionary)Read(model, "Actors")!)[20UL]!;
                model.GetType().GetMethod("Start", flags, [caster.GetType(), rule.GetType()])!.Invoke(model, [caster, rule]);
                void ApplyRaidwideRaw(double at, object npc)
                {
                    var world = Read(Script(), "ReplayWorld")!;
                    var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
                        .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    var data = JsonNode.Parse(World(at, [npc]))!["data"]!.ToJsonString();
                    world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                        [JsonSerializer.Deserialize(data, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
                    world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(at));
                    Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
                }
                ApplyRaidwideRaw(1.1, Npc(999, at: 1, total: .02f, isSpell: false));
                var damage = Items(Script(), "PredictedDamage");
                Check(damage.Length == 1 && (DateTime)Read(damage[0], "Activation")! ==
                    Start.AddSeconds(1.1).AddSeconds((.02f + .3f) - .02f), "Kraken actual original raidwide raw non Spell capped clock");
                ApplyRaidwideRaw(2.1, Npc());
                Check(Items(Script(), "PredictedDamage").Length == 1
                    && (DateTime)Read(Items(Script(), "PredictedDamage")[0], "Activation")! == default,
                    "Kraken actual null CastInfo default diagnostic fallback retained");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Kraken original raw raidwide and default clock fallback");
                load(id, [Header(territory), World(0, [Boss(), Player(), Npc(43566, total: 10)], true),
                    World(1, [Npc(43566, 22, at: 1, total: 1)]), World(2, [Npc()]), World(3, [])]);
                Seek(1.1);
                var ordered = Items(Script(), "PredictedDamage");
                Check(ordered.Length == 2
                    && (DateTime)Read(ordered[0], "Activation")! < (DateTime)Read(ordered[1], "Activation")!,
                    "Kraken actual two original casters normalize later start earlier clock");
                Seek(2.1); Check(Items(Script(), "PredictedDamage").Length == 1, "Kraken actual first source Finish retains second");
                Seek(1.1); Check(Items(Script(), "PredictedDamage").Length == 2, "Kraken reverse Seek restores both raidwides");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Kraken two original casters sorted damage clocks");
            }
            if (index == 10)
            {
                foreach (var slash in new[] { true, false })
                {
                    var first = slash ? 29136u : 29134u;
                    var second = slash ? 29138u : 29135u;
                    var third = slash ? 29376u : 29375u;
                    load(id, [Header(territory), World(0, [Boss(), Player(),
                        Npc(first, total: 10), Npc(second, 21, center + Vector3.UnitX * 3, total: 2),
                        Npc(third, 22, center + Vector3.UnitX * 7, total: 5)], true),
                        World(1, []), World(2, [Npc()]), World(3, [Npc(actor: 21)]),
                        World(4, [Npc(actor: 22)]), World(5, [])]);
                    Seek(.3);
                    object[] Queue() => Items(Model(), "Casts").Where(c => (string)Read(Read(c, "Rule")!, "Component")! ==
                        (slash ? "SlashAndBurn" : "RoaringBlaze")).ToArray();
                    ulong SourceId(object cast) => (ulong)Read(Read(Read(cast, "Source")!, "Actor")!, "Id")!;
                    Check(Queue().Length == 3 && SourceId(Queue()[0]) == 21 && Views().Length == 1 && dangers() == 1,
                        "Lions actual earliest queue visible and two hidden entries");
                    Seek(2.1);
                    Check(Queue().Length == 2 && SourceId(Queue()[0]) == (slash ? 22UL : 21UL),
                        "Lions actual global Slash head differs from Roar hidden source removal");
                    Seek(3.1);
                    Check(Queue().Length == 1 && SourceId(Queue()[0]) == (slash ? 20UL : 22UL),
                        "Lions actual each queue promotes its precise remaining original source");
                    Seek(4.1); Check(Views().Length == 0 && dangers() == 0, "Lions actual final matching Finish clears queue");
                    Seek(.3); Check(Queue().Length == 3 && Views().Length == 1, "Lions actual reverse Seek restores full hidden queue");
                    Release();
                    Console.WriteLine("PASS native Alliance trash actual Lions hidden sorted queue source Finish " + (slash ? "Slash" : "Roar"));
                }
                var zeroBoss = JsonSerializer.SerializeToNode(Boss())!;
                zeroBoss["Hp"] = 0;
                load(id, [Header(territory), World(0, [Boss(), Player(), Actor(22, 0x38DB, center)], true),
                    World(1, [], removed: [22]), World(2, [Actor(22, 0x38DB, center)]),
                    World(3, [zeroBoss]), World(1000, []), World(1001, [Boss(dead: true)]), World(1002, [])]);
                Seek(2.1);
                var lioness = Read(Model(), "AllianceLioness")!;
                Check((bool)Read(lioness, "Destroyed")!, "Lions actual cached original is destroyed despite living same ID replacement");
                Seek(1000.1);
                var timeline = Script().GetType().GetMethod("GetTimeline")!.Invoke(Script(), null)!;
                Check((bool)Read(Model(), "Active")! && (string)Read(timeline, "PhaseName")! == "Single phase"
                    && Items(timeline, "Entries").Length == 1 && (uint)Read(Items(timeline, "Entries")[0], "StateId")! == 0,
                    "Lions actual HP0 preserves permanent 600 second state");
                Seek(1001.1);
                Check(!(bool)Read(Model(), "Active")! && dangers() == 0 && previews().Length == 0,
                    "Lions actual original pair death completes despite replacement lioness");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Lions cached original pair HP0 permanent timeline");

                load(id, [Header(territory), World(0, [Boss(false), Player(), Actor(22, 0x38DB, center)], true),
                    World(1, [Boss(dead: true)]), World(2, [])]);
                Seek(1.1);
                Check(!(bool)Read(Model(), "Active")! && Read(Model(), "AllianceLioness") == null,
                    "Lions actual first pull completion precedes module cache");
                Release();
                Console.WriteLine("PASS native Alliance trash actual Lions first activation completion before Lioness cache");
            }
            load(id, [Header(territory), World(0, [Boss(), Player(), Npc(((uint[])Read(Items(definition, "Rules")[0], "Actions")!)[0])], true, party: false),
                World(1, [])]);
            Seek(.3);
            Check(dangers() == 0 && previews().Length == 0, "Alliance actual Party0 absent pauses module load");
            Release();
            Console.WriteLine("PASS native Alliance trash actual absent Party0 and replay execution isolation " + ids[index]);
        }
        const string firstId = "masterplayer.alliance.angelosfirst";
        const string secondId = "masterplayer.alliance.angelossecond";
        var firstCenter = (Vector3)Read(definitions[5], "Center")!;
        var secondCenter = (Vector3)Read(definitions[6], "Center")!;
        manager.SetScriptEnabled(secondId, true);
        load(firstId, [Header(1178), Frame(1178, 0, [
            Actor(1, 0x4013, firstCenter, combat: true, subKind: 5),
            Actor(2, 0x40E1, secondCenter, combat: true, subKind: 5),
            Actor(100, 0, firstCenter, subKind: 4),
            Actor(20, 0x233C, firstCenter, cast: Cast(35444, 5, firstCenter, startedAt: 0), subKind: 5)],
            full: true, party: [new { Slot = 0, EntityId = 100UL }]), Frame(1178, 1, [])]);
        controller.Seek(.3);
        Check(dangers() == 2 && manager.Running.ContainsKey(firstId) && manager.Running.ContainsKey(secondId),
            "Alliance two active native owners share territory without erasing each other");
        manager.SetScriptEnabled(firstId, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(secondId), "Alliance first owner stop preserves second");
        manager.SetScriptEnabled(firstId, true); controller.Seek(.3);
        Check(dangers() == 2, "Alliance reverse Seek restores both owners");
        manager.SetScriptEnabled(secondId, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(firstId), "Alliance second owner stop preserves first");
        stop();
        manager.SetScriptEnabled(secondId, true);
        Check(previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Alliance coexisting owners release all output");
        Console.WriteLine("PASS native Alliance trash actual two active owners bidirectional release and reverse Seek");
    }
}
