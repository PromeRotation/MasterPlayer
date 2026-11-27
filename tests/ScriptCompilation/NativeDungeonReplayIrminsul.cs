using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunIrminsulReplay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.irminsulsawtooth";
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
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Views() => previews().Where(p => !((string)Read(p, "Id")!).EndsWith("bozja/arena", StringComparison.Ordinal)).ToArray();
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>().ToArray();
        }
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.IrminsulCatalog", true)!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = (Vector3)Read(definition, "Center")!;
        var name = (string)Read(definition, "Name")!;
        var pc = center + Vector3.UnitX * 20;
        object Boss(bool combat = true, bool dead = false, uint hp = 1000)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x13F1, center, combat: combat, dead: dead, subKind: 5))!;
            node["Hp"] = hp; node["HitboxRadius"] = 9;
            return node;
        }
        object Player(Vector3? point = null, ulong actor = 100, uint status = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, 0, point ?? pc, subKind: 4))!;
            node["HitboxRadius"] = .5f;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[] {
                new { Id = status, SourceId = 100UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 } });
            return node;
        }
        object Npc(uint action = 0)
        {
            var location = action == 5213 ? center + Vector3.UnitX * 30 : center;
            var cast = action == 0 ? null : JsonSerializer.SerializeToNode(Cast(action, 5, location, .47f, 100, 0));
            if (cast != null) { cast["IsSpell"] = true; cast["TotalTime"] = 5; }
            return Actor(20, 0x1B2, location, cast: cast, subKind: 5);
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null, bool party = true)
            => Frame(508, at, actors, full, removed, party: party
                ? [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }] : [],
                sourcePlayerId: 100);
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Irminsul actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Irminsul no replay PR control");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null && Items(Snapshot(), "GoalAreas").Length == 0,
                "Irminsul complete owner cleanup");
        }
        void Boundary()
        {
            var arena = Read(Snapshot(), "ArenaBoundary")!;
            Check(arena.GetType().Name == "AllianceArenaGeometry"
                && ((IEnumerable)((dynamic)arena).GetContours(64)).Cast<object>().Count() == 2
                && !((dynamic)arena).Contains(center, 0f) && ((dynamic)arena).Contains(pc, 0f),
                "Irminsul real two-contour ring contains player and excludes center");
            var converted = assembly.GetType("MasterPlayer.Radar.HostDrawingGeometry")!
                .GetMethod("Convert")!.Invoke(null, [arena])!;
            Check(Items(converted, "FillPolygons").Length == Items(arena, "FillPolygons").Length
                && Items(converted, "FillPolygons").Length > 0, "Irminsul world adapter keeps hole triangulation");
        }
        void Solver(bool leaveDanger = false)
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [pc, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            ((object)options).GetType().GetProperty("GridResolution")!.SetValue(options, .5f);
            ((object)options).GetType().GetProperty("ArenaSafetyMargin")!.SetValue(options, 0f);
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Irminsul real solver safe route");
            dynamic arena = Read(snapshot, "ArenaBoundary")!;
            foreach (var point in (IEnumerable)result.ExecutablePath)
                Check((bool)arena.Contains((Vector3)Read(point, "Position")!, 0f), "Irminsul real solver stays out of inner hole");
            if (leaveDanger)
            {
                var path = ((IEnumerable)result.ExecutablePath).Cast<object>().ToArray();
                Check(path.Length != 0, "Irminsul exposed player receives an executable escape path");
                var destination = (Vector3)Read(path[^1], "Position")!;
                dynamic danger = Read(Items(snapshot, "DangerAreas").Single(), "Geometry")!;
                Check((bool)danger.Contains(pc, 0f) && !(bool)danger.Contains(destination, 0f),
                    "Irminsul real solver moves exposed player into LOS shadow");
            }
        }
        foreach (var action in new uint[] { 5208, 5209, 5205, 5472, 5212, 5172, 5213 })
        {
            pc = center + Vector3.UnitX * (action == 5213 ? -20 : 20);
            load(id, [Header(508), World(0, [Boss(false), Player(), Player(actor: 101), Npc(action)], true),
                World(1, [Boss()]), World(2, []), World(6, [Npc()]), World(7, [])]);
            Seek(.3); Check(previews().Length == 0 && dangers() == 0, "Irminsul cold inactive output");
            Seek(2.1); Boundary();
            Check(Views().Length == (action == 5212 ? 0 : 1) && dangers() == (action == 5212 ? 0 : 1)
                && Lines().Length == (action == 5209 ? 1 : 0),
                $"Irminsul actual action{action} AOE={Views().Length} danger={dangers()} KB={Lines().Length}");
            if (action == 5212) Check(Items(Script(), "PredictedDamage").Length == 1, "Irminsul current raw raidwide");
            if (action == 5213) Check((bool)Read(Views().Single(), "Safe")!, "Irminsul LOS source safe color");
            if (action == 5209)
            {
                var rule = Items(definition, "Rules").Single(r => Read(r, "Kind")!.ToString() == "AllianceKnockback");
                var actors = (IDictionary)Read(Model(), "Actors")!;
                var movements = ((IEnumerable)Model().GetType().GetMethod("AllianceSimpleMovements", flags)!
                    .Invoke(Model(), [actors[100UL], rule])!).Cast<object>().ToArray();
                Check(movements.Length == 1 && (Vector3)Read(Lines().Single(), "Start")! == (Vector3)Read(movements[0], "Item2")!
                    && (Vector3)Read(Lines().Single(), "End")! == (Vector3)Read(movements[0], "Item3")!,
                    "Irminsul real published KB endpoints equal model ring ray");
            }
            Solver();
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                "Irminsul matching native NPC input takeover");
            manager.SetMechanismEnabled(id, name, false); Seek(2.2);
            Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "Irminsul disable clears owned outputs");
            manager.SetMechanismEnabled(id, name, true); Seek(2.1);
            Seek(6.1); Check(Views().Length == 0 && dangers() == 0 && Lines().Length == 0
                && Items(Script(), "PredictedDamage").Length == 0, "Irminsul ordinary watched Finish clears both independent components");
            Release();
            Console.WriteLine($"PASS native Irminsul actual action{action} cold Finish solver ring holes safe color toggle Seek owner isolation");
        }
        pc = center + Vector3.UnitX * 10;
        load(id, [Header(508), World(0, [Boss(), Player(), Player(actor: 101), Npc(5213)], true),
            World(1, []), World(6, [Npc()]), World(7, [])]);
        Seek(.5); Check(dangers() == 1, "Irminsul LOS danger survives complement fill threshold");
        Solver(leaveDanger: true);
        Release();
        Console.WriteLine("PASS native Irminsul actual exposed player solver escapes LOS complement into shadow around ring hole");

        pc = center + Vector3.UnitX * 32;
        load(id, [Header(508), World(0, [Boss(), Player(status: 1209), Player(actor: 101), Npc(5209)], true),
            World(1, []), World(2, [Player()]), World(3, []), World(6, [Npc()]), World(7, [])]);
        Seek(.5); Check(Lines().Length == 0 && dangers() == 1, "Irminsul cold immunity59 leaves cone");
        Seek(2.1); Check(Lines().Length == 1 && dangers() == 1, "Irminsul real immunity Lose restores independent KB");
        var endpoint = (Vector3)Read(Lines().Single(), "End")!;
        Check(endpoint.X > 32 && endpoint.X < 35 && ((dynamic)Read(Snapshot(), "ArenaBoundary")!).Contains(endpoint, 0f),
            "Irminsul actual outer polygon wall clips KB before ten-unit travel");
        Release();
        Console.WriteLine("PASS native Irminsul actual immunity59 Lose cone retained outer ring wall no KB navigation");

        load(id, [Header(508), World(0, [Boss(), Player(), Npc(5172)], true, party: false), World(1, [])]);
        Seek(.5); Check(!(bool)Read(Model(), "Active")! && dangers() == 0 && previews().Length == 0,
            "Irminsul missing Party0 pauses module");
        Release();
        Console.WriteLine("PASS native Irminsul actual absent Party0 no output cleanup");

        load(id, [Header(508), World(0, [Boss(), Player(), Actor(2, 0x13EF, center, combat: true, subKind: 5)], true),
            World(1, [Boss(hp: 0)]), World(2, [])]);
        Seek(.3); Check((bool)Read(Model(), "Active")!, "Irminsul primary combat pulls");
        Seek(1.1); Check(!(bool)Read(Model(), "Active")! && Read(Snapshot(), "ArenaBoundary") == null,
            "Irminsul primary HP0 completes even with live Sawtooth");
        Seek(.3); Check((bool)Read(Model(), "Active")!, "Irminsul reverse Seek rebuilds primary-only lifecycle");
        Release();
        Console.WriteLine("PASS native Irminsul actual primary-only default HP0 completion live secondary reverse Seek");

        const string other = "masterplayer.alliance.cetus";
        var cetus = new Vector3(-288, 0, 0);
        manager.SetScriptEnabled(other, true);
        load(id, [Header(508), World(0, [Boss(), Player(), Npc(5172),
            Actor(2, 0x13F7, cetus, combat: true, subKind: 5),
            Actor(21, 0x233C, cetus, cast: Cast(5158, 5, cetus, startedAt: 0), subKind: 5)], true), World(1, [])]);
        Seek(.3); Check(dangers() == 2 && manager.Running.ContainsKey(other), "Irminsul Cetus dual owner");
        manager.SetScriptEnabled(id, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(other), "Irminsul stop preserves Cetus");
        manager.SetScriptEnabled(id, true); Seek(.3); Check(dangers() == 2, "Irminsul dual reverse Seek");
        manager.SetScriptEnabled(other, false); Seek(.4); Check(dangers() == 1, "Cetus stop preserves Irminsul");
        Release(); manager.SetScriptEnabled(other, true);
        Console.WriteLine("PASS native Irminsul actual same territory Cetus dual owner bidirectional release reverse Seek");
    }
}
