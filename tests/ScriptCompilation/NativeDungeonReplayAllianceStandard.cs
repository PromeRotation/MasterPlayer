using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAllianceStandardReplay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers,
        bool knockbacks = false, bool arachne = false, bool deathgaze = false)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
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
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>().ToArray();
        }
        var catalog = assembly.GetType("MasterPlayer.Mechanics.Bozja."
            + (knockbacks ? "AllianceKnockbackCatalog" : "AllianceStandardCatalog"), true)!;
        var definitions = ((IEnumerable)catalog.GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>().ToArray();
        var slugs = knockbacks ? new[] { "cetus", "echidna", "calofisteri" }
            : new[] { "cuchulainn", "forgall", "headstone", "ferdiadhollow", "protoultima", "diabolos", "paradigmtrash" };
        var label = knockbacks ? "knockback" : "standard";
        if (arachne)
        {
            definitions = [assembly.GetType("MasterPlayer.Mechanics.Bozja.ArachneCatalog", true)!
                .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!];
            slugs = ["arachneeve"];
            label = "arachne";
        }
        if (deathgaze)
        {
            definitions = [assembly.GetType("MasterPlayer.Mechanics.Bozja.DeathgazeCatalog", true)!
                .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!];
            slugs = ["deathgazehollow"];
            label = "deathgaze";
        }
        for (var index = 0; index < definitions.Length; ++index)
        {
            var definition = definitions[index];
            var id = "masterplayer.alliance." + slugs[index];
            var territory = (uint)Read(definition, "Territory")!;
            var oid = (uint)Read(definition, "PrimaryOid")!;
            var center = (Vector3)Read(definition, "Center")!;
            var name = (string)Read(definition, "Name")!;
            object Script() => Read(manager.Running[id], "Script")!;
            object Model() => Items(Read(Script(), "region")!, "Encounters").Single();
            object[] Views() => previews().Where(p => !((string)Read(p, "Id")!).EndsWith("bozja/arena", StringComparison.Ordinal)).ToArray();
            object Boss(bool combat = true, bool dead = false, bool hp0 = false)
            {
                var node = JsonSerializer.SerializeToNode(Actor(1, oid, center, combat: combat, dead: dead, subKind: 5))!;
                if (hp0) node["Hp"] = 0;
                return node;
            }
            object Player(ulong actor = 100)
            {
                var node = JsonSerializer.SerializeToNode(Actor(actor, 0, center + Vector3.UnitX * (actor == 100 ? 10 : 12), subKind: 4))!;
                node["HitboxRadius"] = .5f;
                return node;
            }
            object Npc(uint action = 0, Vector3? point = null, ulong target = 100)
            {
                var cast = action == 0 ? null : JsonSerializer.SerializeToNode(Cast(action, 5, point ?? center, .47f, target, 0));
                if (cast != null) { cast["IsSpell"] = true; cast["TotalTime"] = 5; }
                return Actor(20, 0x233C, center + Vector3.UnitX * 3, cast: cast, subKind: 5);
            }
            string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
                => Frame(territory, at, actors, full, removed,
                    party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
            void Seek(double at)
            {
                controller.Seek(at);
                Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                    "Alliance standard actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
            }
            void Release()
            {
                var hints = Read(Script(), "Hints")!;
                Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                    && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                    "Alliance standard replay never executes PR facing target or action hold");
                stop();
                Check(dangers() == 0 && previews().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                    && Items(Snapshot(), "HardObstacles").Length == 0 && Read(Snapshot(), "ArenaBoundary") == null && Lines().Length == 0,
                    "Alliance standard every owner output released");
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
                ((object)options).GetType().GetProperty("GridResolution")!.SetValue(options, .5f);
                ((object)options).GetType().GetProperty("ArenaSafetyMargin")!.SetValue(options, 0f);
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!,
                    "Alliance standard actual solver safe route");
                dynamic boundary = Read(snapshot, "ArenaBoundary")!;
                foreach (var point in (IEnumerable)result.ExecutablePath)
                    Check((bool)boundary.Contains((Vector3)Read(point, "Position")!, 0f), "Alliance standard route stays in exact arena");
            }
            var rules = Items(definition, "Rules");
            if (arachne)
                rules = rules.Where(r => (string)Read(r, "Component")! is not ("SpiderWeb" or "Tremblor")).ToArray();
            if (deathgaze)
                rules = rules.Where(r => (string)Read(r, "Component")! != "VoidAeroII").ToArray();
            foreach (var rule in rules)
            {
                var action = ((uint[])Read(rule, "Actions")!).Single();
                var kind = Read(rule, "Kind")!.ToString()!;
                var component = (string)Read(rule, "Component")!;
                var offsetCircle = Read(rule, "Shape") is { } shape && shape.GetType().Name == "CircleTemplate"
                    && kind == "Aoe" && (float)Read(shape, "Radius")! >= (float)Read(definition, "SampleRadius")!;
                load(id, [Header(territory), World(0, [Boss(false), Player(), Player(101),
                    Npc(action, offsetCircle ? center + Vector3.UnitX * 10 : null)], true),
                    World(1, [Boss()]), World(2, []), World(6, [Npc()]), World(7, [])]);
                Seek(.3); Check(previews().Length == 0 && dangers() == 0, "Pilot cold inactive no output");
                Seek(2.1);
                var matching = Items(definition, "Rules").Where(r => ((uint[])Read(r, "Actions")!).Contains(action)).ToArray();
                var visual = matching.Sum(r => Read(r, "Kind")!.ToString() switch { "Aoe" or "Charge" or "Stack" or "Spread" => 1, "CastGaze" => 2, _ => 0 });
                var aoes = matching.Count(r => Read(r, "Kind")!.ToString() is "Aoe" or "Charge" or "Spread");
                Check(Views().Length == visual && (kind == "Stack" ? dangers() > 0 : dangers() == aoes)
                    && Read(Snapshot(), "ArenaBoundary") != null,
                    $"Pilot actual {slugs[index]}/{component} visual and navigation views={Views().Length}/{visual} dangers={dangers()} "
                    + $"active={Read(Model(), "Active")} casts={Items(Model(), "Casts").Length} boundary={Read(Snapshot(), "ArenaBoundary")?.GetType().Name}");
                if (matching.FirstOrDefault(r => Read(r, "Kind")!.ToString() == "AllianceKnockback") is { } kbRule)
                {
                    var model = Model();
                    var viewer = ((IDictionary)Read(model, "Actors")!)[100UL]!;
                    var movements = ((IEnumerable)model.GetType().GetMethod("AllianceSimpleMovements", flags)!
                        .Invoke(model, [viewer, kbRule])!).Cast<object>().ToArray();
                    Check(Lines().Length == 1 && movements.Length == 1
                        && (Vector3)Read(Lines()[0], "Start")! == (Vector3)Read(movements[0], "Item2")!
                        && (Vector3)Read(Lines()[0], "End")! == (Vector3)Read(movements[0], "Item3")!
                        && Items(Snapshot(), "GoalAreas").Length == 0 && dangers() == aoes,
                        "Pilot knockback exact line and independent same-action AOE without fabricated navigation");
                }
                if (kind is "Raidwide" or "Tankbuster")
                    Check(Items(Script(), "PredictedDamage").Length == 1
                        && ((string?)Read(Script(), "MechanicHintsText"))?.Contains(kind == "Raidwide" ? "Raidwide" : "Tankbuster") == true,
                        "Pilot actual global hint and original caster diagnostics");
                if (kind == "Stack")
                {
                    Check(Items(Script(), "PredictedDamage").Length == 0
                        && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stack!") == true,
                        "Pilot stack minimum eight with no fabricated damage");
                    bool Forbidden(Vector3 point) => Items(Snapshot(), "DangerAreas")
                        .Any(area => (bool)((dynamic)Read(area, "Geometry")!).Contains(point, 0f));
                    Check(Forbidden(center) && !Forbidden(center + Vector3.UnitX * 12),
                        "Pilot own stack exact complement forbids distant origin and preserves buddy safe circle");
                    Solver(center);
                }
                if (kind is "Aoe" or "Charge") Solver(center + Vector3.UnitX * (offsetCircle ? -10 : 10));
                if (Read(Model(), "Arena")!.GetType().Name == "AllianceArenaGeometry")
                {
                    dynamic published = Read(Snapshot(), "ArenaBoundary")!;
                    dynamic modelArena = Read(Model(), "Arena")!;
                    Check(published.GetType().Name == "AllianceArenaGeometry", "Pilot actual boundary preserves relative-coordinate arithmetic");
                    foreach (var contour in (IEnumerable)modelArena.GetContours(64))
                        foreach (var point in (IEnumerable)contour)
                        {
                            var p = (Vector3)point;
                            foreach (var x in new[] { MathF.BitDecrement(p.X), p.X, MathF.BitIncrement(p.X) })
                                foreach (var z in new[] { MathF.BitDecrement(p.Z), p.Z, MathF.BitIncrement(p.Z) })
                                    Check((bool)published.Contains(new Vector3(x, 0, z), 0f) == (bool)modelArena.Contains(new Vector3(x, 0, z), 0f),
                                        "Pilot published exact boundary at source-reviewed corners");
                        }
                }
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                    && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                    "Pilot exact NPC action suppression");
                manager.SetMechanismEnabled(id, name, false); Seek(2.2);
                Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null && Lines().Length == 0,
                    "Pilot mechanism switch releases owner");
                manager.SetMechanismEnabled(id, name, true); Seek(2.1);
                Check(Views().Length == visual, "Pilot reverse Seek reconstructs");
                Seek(6.1); Check(Views().Length == 0 && dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && Lines().Length == 0,
                    "Pilot ordinary Finish removes display navigation and goals");
                Release();
                Console.WriteLine($"PASS native Alliance {label} actual {slugs[index]}/{component} cold Finish solver switch Seek owner isolation");
                if (kind == "Stack")
                {
                    load(id, [Header(territory), World(0, [Boss(), Player(), Player(101), Npc(action, target: 101)], true),
                        World(1, []), World(6, [Npc()]), World(7, [])]);
                    Seek(.3);
                    var goals = Items(Snapshot(), "GoalAreas");
                    Check(dangers() == 0 && goals.Length == 1 && Views().Length == 1
                        && Items(Script(), "PredictedDamage").Length == 0,
                        "Pilot non-target stack publishes join goal without damage prediction");
                    Check((bool)((dynamic)Read(goals[0], "Geometry")!).Contains(center + Vector3.UnitX * 12, 0f)
                        && !(bool)((dynamic)Read(goals[0], "Geometry")!).Contains(center, 0f),
                        "Pilot non-target join goal tracks original stack target");
                    Solver(center);
                    Seek(6.1);
                    Check(Items(Snapshot(), "GoalAreas").Length == 0 && Views().Length == 0, "Pilot non-target Finish releases join");
                    Release();
                    Console.WriteLine($"PASS native Alliance standard actual {slugs[index]}/{component} non-target join goal solver Finish");
                }
                if (kind == "AllianceKnockback")
                {
                    var immunePlayer = JsonSerializer.SerializeToNode(Player())!;
                    immunePlayer["Statuses"] = JsonSerializer.SerializeToNode(new[] {
                        new { Id = 1209u, SourceId = 100UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 } });
                    load(id, [Header(territory), World(0, [Boss(false), immunePlayer, Player(101), Npc(action)], true),
                        World(1, [Boss()]), World(2, []), World(3, [Player()]), World(4, []),
                        World(6, [Npc()]), World(7, [])]);
                    Seek(2.1);
                    Check(Lines().Length == 0 && dangers() == aoes && Views().Length == visual
                        && Items(Snapshot(), "GoalAreas").Length == 0,
                        "Pilot cold slot59 immunity hides knockback line while preserving separate AOE");
                    Seek(3.1);
                    Check(Lines().Length == 1 && dangers() == aoes, "Pilot real status Lose restores line only");
                    Seek(6.1);
                    Check(Lines().Length == 0 && dangers() == 0 && Views().Length == 0, "Pilot Finish clears both independent consumers");
                    Release();
                    Console.WriteLine($"PASS native Alliance knockback actual {slugs[index]}/{component} cold immunity59 Lose independent AOE Finish");
                }
            }
            load(id, [Header(territory), World(0, [Boss(), Player()], true), World(1, [Boss(dead: true, hp0: true)]),
                World(2, []), World(3, [], removed: [1]), World(4, [])]);
            Seek(1.1); Check((bool)Read(Model(), "Active")! == (!knockbacks && index == 6), "Pilot exact default versus all Destroy completion");
            Seek(3.1); Check(!(bool)Read(Model(), "Active")!, "Pilot Destroy completion");
            Release();
            Console.WriteLine($"PASS native Alliance {label} actual {slugs[index]} HP0 Dead Destroy exact lifecycle");
            load(id, [Header(territory), Frame(territory, 0,
                [Boss(), Player(), Npc(((uint[])Read(rules[0], "Actions")!)[0])],
                full: true, party: [], sourcePlayerId: 100), World(1, [])]);
            Seek(.3);
            Check(dangers() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "Pilot absent Party0 pauses native module load");
            Release();
            Console.WriteLine($"PASS native Alliance {label} actual {slugs[index]} absent Party0 no navigation or drawing");
        }
        if (knockbacks || arachne || deathgaze) return;
        const string firstId = "masterplayer.alliance.ferdiadhollow";
        const string secondId = "masterplayer.alliance.protoultima";
        var firstCenter = (Vector3)Read(definitions[3], "Center")!;
        var secondCenter = (Vector3)Read(definitions[4], "Center")!;
        manager.SetScriptEnabled(secondId, true);
        load(firstId, [Header(627), Frame(627, 0, [
            Actor(1, 0x1928, firstCenter, combat: true, subKind: 5),
            Actor(2, 0x1967, secondCenter, combat: true, subKind: 5),
            Actor(100, 0, firstCenter, subKind: 4),
            Actor(20, 0x233C, firstCenter, cast: Cast(7339, 5, firstCenter, startedAt: 0), subKind: 5),
            Actor(21, 0x233C, secondCenter, cast: Cast(4557, 5, secondCenter, startedAt: 0), subKind: 5)],
            full: true, party: [new { Slot = 0, EntityId = 100UL }]), Frame(627, 1, [])]);
        controller.Seek(.3);
        Check(dangers() == 2 && manager.Running.ContainsKey(firstId) && manager.Running.ContainsKey(secondId),
            "Pilot both same-territory native owners publish independent actions");
        manager.SetScriptEnabled(firstId, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(secondId), "Pilot first owner release preserves second");
        manager.SetScriptEnabled(firstId, true); controller.Seek(.3);
        Check(dangers() == 2, "Pilot reverse Seek reconstructs both owners");
        manager.SetScriptEnabled(secondId, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(firstId), "Pilot second owner release preserves first");
        stop(); manager.SetScriptEnabled(secondId, true);
        Check(dangers() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
            "Pilot both owners fully release");
        Console.WriteLine("PASS native Alliance standard actual same territory dual owner bidirectional release reverse Seek");
    }
}
