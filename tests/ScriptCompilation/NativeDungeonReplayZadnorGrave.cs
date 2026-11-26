using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorGrave(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "复制告密者——第四军团谢米哈扎";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-60, 0, 800);
        object? Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Read(Script(), "encounter")!;
        object[] Shown(string key = "bozja/aoe/") => previews().Where(p => ((string)Read(p, "Id")!).Contains(key)).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility")!;
        object[] AllLines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        object[] Lines() => AllLines()
            .Where(p => ((string)Read(p, "Id")!).Contains("bozja/grave/march/")).ToArray();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        string Text() => (string?)Read(Script(), "MechanicHintsText") ?? "";
        object Spell(uint action, double at = 0, Vector3? p = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, p ?? center, 0, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss(object? cast = null) => Actor(1, 0x2E35, center, combat: true, cast: cast, subKind: 5, isEnemy: true);
        object Player(ulong actorId = 100, Vector3? p = null, int marches = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0, p ?? center))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, marches).Select(i =>
                new { Id = 2161u, SlotIndex = i == 0 ? 0 : 59, ExpiresAt = Start.AddSeconds(30), SourceId = 1UL }).ToArray());
            return node;
        }
        object Helper(ulong actorId = 20, object? cast = null, Vector3? p = null, uint oid = 0x233C, bool dead = false)
            => Actor(actorId, oid, p ?? center, cast: cast, dead: dead);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, at, actors, full, removed: removed, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, uint action) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CE42 actual replay error: " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "CE42 owner release");
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x2E35u])! && !(bool)owns.Invoke(null, [920u, 0x2E35u])!
            && !(bool)owns.Invoke(null, [975u, 0x3233u])!, "CE42 exact source ownership");
        var previousLines = AllLines();
        var addLine = visibility.GetType().GetMethod("AddLine")!;
        foreach (var name in new[] { "last", "B-last", "other", "first", "B-first" })
            addLine.Invoke(visibility, ["CE42-line-" + name, center, center + Vector3.UnitZ, false, "other", "其他"]);
        var beforeOrdering = AllLines();
        var beforeIds = beforeOrdering.Select(l => (string)Read(l, "Id")!).ToArray();
        var ownerA = beforeIds.Where(lineId => lineId is "CE42-line-first" or "CE42-line-last").Reverse().ToArray();
        var ownerB = beforeIds.Where(lineId => lineId is "CE42-line-B-first" or "CE42-line-B-last").Reverse().ToArray();
        Check(ownerA.Length == 2 && ownerB.Length == 2
            && !beforeIds.Where(ownerA.Contains).SequenceEqual(ownerA)
            && !beforeIds.Where(ownerB.Contains).SequenceEqual(ownerB), "CE42 both owners require a real permutation before sorting");
        visibility.GetType().GetMethod("SetPreviewOrder")!.Invoke(visibility, ["CE42-line-owner", ownerA]);
        visibility.GetType().GetMethod("SetPreviewOrder")!.Invoke(visibility, ["CE42-line-owner-B", ownerB]);
        var orderedLines = AllLines();
        var queueA = new Queue<string>(ownerA);
        var queueB = new Queue<string>(ownerB);
        // Earlier runners can leave free Dictionary slots. The contract
        // preserves each owner's actual occupied positions, not insertion age.
        var expectedOrder = beforeOrdering.Select(l => (string)Read(l, "Id")!).Select(lineId =>
        {
            if (ownerA.Contains(lineId)) return queueA.Dequeue();
            if (ownerB.Contains(lineId)) return queueB.Dequeue();
            return lineId;
        }).ToArray();
        Check(orderedLines.Select(l => (string)Read(l, "Id")!).SequenceEqual(expectedOrder)
            && orderedLines.Where(l => ownerA.Contains((string)Read(l, "Id")!)).Select(l => (string)Read(l, "Id")!).SequenceEqual(ownerA)
            && orderedLines.Where(l => ownerB.Contains((string)Read(l, "Id")!)).Select(l => (string)Read(l, "Id")!).SequenceEqual(ownerB),
            "CE42 explicit line order permutes only owner positions");
        Check(orderedLines.Where(l => !((string)Read(l, "Id")!).StartsWith("CE42-line-")).SequenceEqual(previousLines),
            "CE42 other owners line outputs retained");
        for (var lineIndex = 0; lineIndex < beforeOrdering.Length; ++lineIndex)
            if (!((string)Read(beforeOrdering[lineIndex], "Id")!).StartsWith("CE42-line-"))
                Check(Equals(orderedLines[lineIndex], beforeOrdering[lineIndex]), "CE42 existing unrelated global line slot retained");
        visibility.GetType().GetMethod("RemovePreviews")!.Invoke(visibility, ["CE42-line-"]);
        Console.WriteLine("PASS native line order explicit owner permutation keeps unrelated output positions and cleanup");
        foreach (var action in new uint[] { 24115, 24103, 24104, 24109, 24111, 24112 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Player(101, center + Vector3.UnitX * 20),
                Helper(cast: Spell(action))], true), Effect(1, action), World(20, []),
                World(21, [Helper()]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Model(), "Active")! && Shown().Length == 1 && dangers() == (action == 24115 ? 0 : 1), "CE42 standard native output");
            Check((bool)Read(Shown()[0], "Safe")! == (action == 24115), "CE42 safe color distinct from danger");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 24095 }),
                "CE42 source/action suppression");
            if (action == 24104)
            {
                var snapshot = Snapshot();
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!
                    && Vector3.Distance(center, (Vector3)Read((object)result, "TargetPosition")!) >= 5, "CE42 real solver escapes R5");
            }
            Seek(20.1); Check(Shown().Length == 1, "CE42 no Effect TTL");
            Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "CE42 watched Finish");
            Seek(.1); Check(Shown().Length == 1, "CE42 reverse Seek rebuild");
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0, "CE42 disabled output released");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            Release();
            Console.WriteLine($"PASS native CE42 {action} actual safe risk geometry source ownership true Finish solver reverse Seek owner");
        }
        load(id, [Header(975), World(0, [Boss(Spell(24098)), Player(), Helper()], true),
            World(1, [Boss(Spell(24099, 1))]), World(2, [Boss()]), Effect(3, 24102), Effect(4, 24100), World(5, [])]);
        Seek(1.1);
        Check(Shown().Length == 2 && dangers() == 1, "CE42 dual two displayed one risky");
        Seek(2.1); Check(Shown().Length == 2 && dangers() == 1, "CE42 ordinary Finish retains");
        Seek(3.1); Check(Shown().Length == 1 && dangers() == 1, "CE42 opposite source Effect promotes");
        Seek(4.1); Check(Shown().Length == 0 && dangers() == 0, "CE42 final Effect clears");
        Seek(1.1); Check(Shown().Length == 2, "CE42 dual reverse Seek");
        Release();
        Console.WriteLine("PASS native CE42 actual dual circle donut queue risk promotion cross source Effect ordinary Finish retained owner");

        load(id, [Header(975), World(0, [Boss(Spell(24096)), Player(), Helper()], true),
            World(1, [Helper(30, oid: 0x1EB173)]), World(2, [Boss(Spell(24096, 2))]), World(3, [])]);
        Seek(.1); Check(Shown().Length == 1 && dangers() == 1 && Text().Contains("Apply doom"), "CE42 initial ring and raw global hint");
        Seek(1.1);
        Check(Shown().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") != null, "CE42 wall consumes ring and publishes R25");
        var shrunk = Read(Snapshot(), "ArenaBoundary")!;
        Check((bool)shrunk.GetType().GetMethod("Contains")!.Invoke(shrunk, [center + Vector3.UnitX * 24, 0f])!
            && !(bool)shrunk.GetType().GetMethod("Contains")!.Invoke(shrunk, [center + Vector3.UnitX * 26, 0f])!, "CE42 true R25 boundary");
        Seek(2.1); Check(Shown().Length == 0, "CE42 later Gallows no new ring");
        Release();
        Console.WriteLine("PASS native CE42 actual deathwall Created ring clear R25 current arena no later ring global hint owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Helper(21, oid: 0x3232)], true),
            World(1, [Helper(21, oid: 0x3232, dead: true)]), World(2, [], removed: [21]), World(3, [])]);
        Seek(.1);
        Check(Shown().Length == 1 && dangers() == 0 && ((IEnumerable)Read(Snapshot(), "HardObstacles")!).Cast<object>().Count() == 1,
            "CE42 sphere only obstacle channel");
        Seek(1.1); Check(Shown().Length == 1, "CE42 dead sphere remains");
        Seek(2.1); Check(Shown().Length == 0 && !((IEnumerable)Read(Snapshot(), "HardObstacles")!).Cast<object>().Any(), "CE42 sphere destroyed");
        Release();
        Console.WriteLine("PASS native CE42 actual sphere dead retained destroyed removed R7 obstacle no danger owner");

        load(id, [Header(975), World(0, [Boss(), Player(marches: 2), Helper(cast: Spell(24115, p: center + Vector3.UnitZ * 39.6f))], true),
            World(1, [Helper(cast: Spell(24115, 1, center - Vector3.UnitZ * 20))]), World(2, [Player(marches: 1)]),
            World(3, [Player()]), World(4, [])]);
        Seek(.1);
        Check(Lines().Length == 2 && dangers() == 0 && !Text().Contains("Aim for safe"), "CE42 two true slots project into safe circle");
        Check((Vector3)Read(Lines()[0], "Start")! == center
            && (Vector3)Read(Lines()[0], "End")! == center + Vector3.UnitZ * (6.6f * 3f)
            && (Vector3)Read(Lines()[1], "Start")! == (Vector3)Read(Lines()[0], "End")!
            && (Vector3)Read(Lines()[1], "End")! == (center + Vector3.UnitZ * (6.6f * 3f)) + Vector3.UnitZ * (6.6f * 3f),
            "CE42 exact sequential float line coordinates actual=" + string.Join(";", Lines().Select(l => $"{Read(l, "Start")}->{Read(l, "End")}"))
                + " expected=" + center + "->" + (center + Vector3.UnitZ * (6.6f * 3f))
                + "->" + ((center + Vector3.UnitZ * (6.6f * 3f)) + Vector3.UnitZ * (6.6f * 3f)));
        Seek(1.1);
        Check(Lines().Length == 2 && Text().Contains("Aim for safe"), "CE42 unsafe final endpoint hint");
        Check(Read(Script(), "PlannedFacing") == null && !(bool)Read(Read(Read(Script(), "Hints")!, "_facing")!, "_hasRequest")!
            && !(bool)Read(Read(Script(), "Hints")!, "holdActions")!, "CE42 projected march does not execute facing ACR");
        Seek(2.1); Check(Lines().Length == 1, "CE42 direction Lose removes first only");
        Seek(3.1); Check(Lines().Length == 0, "CE42 final Lose removes projection");
        Seek(.1); Check(Lines().Length == 2, "CE42 march reverse Seek");
        Release();
        Console.WriteLine("PASS native CE42 actual true slots march lines final safe hint no new motion facing or ACR reverse Seek owner");

        object Named(ulong actorId, uint action, string name)
        {
            var node = JsonSerializer.SerializeToNode(Helper(actorId, Spell(action)))!;
            node["Name"] = name;
            return node;
        }
        load(id, [Header(975), World(0, [Boss(), Player(), Named(20, 24107, "One"), Named(21, 24113, "Two")], true),
            World(1, [Helper(20)]), World(2, [Helper(21)]), World(3, [])]);
        Seek(.1); Check(Text().Contains("Interrupt One!") && Text().Contains("Interrupt Two!"), "CE42 independent named interrupts");
        Check(((ulong[])Read(Script(), "InterruptTargetIds")!).SequenceEqual(new ulong[] { 20, 21 }), "CE42 interrupt target diagnosis");
        Seek(1.1); Check(!Text().Contains("One!") && Text().Contains("Two!"), "CE42 separate interrupt Finish");
        Release();
        Console.WriteLine("PASS native CE42 actual concurrent interrupt names separate callback state diagnostic targets Finish owner");

        foreach (var action in new uint[] { 24093, 24094, 24106 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Player(101, center + Vector3.UnitX * 2),
                Helper(cast: Spell(action))], true), World(1, [Helper()]), World(2, [])]);
            Seek(.1);
            Check(action == 24106 ? Shown("bozja/spread/").Length != 0 : Read(Script(), "PredictedDamageText") != null,
                "CE42 standard damage or spread output " + action);
            Seek(1.1); Check(dangers() == 0 && Shown().Length == 0, "CE42 standard Finish " + action);
            Release();
            Console.WriteLine($"PASS native CE42 {action} actual native dynamic diagnosis or target spread true Finish owner");
        }
    }
}
