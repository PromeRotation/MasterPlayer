using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBozjaSkirmishes(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
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
        object Model(uint fate) => Items(Read(Script(), "region")!, "Encounters")
            .Single(e => (uint)Read(Read(e, "Definition")!, "FateId")! == fate);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object Spell(uint action)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, center, .4f, 101, 0))!;
            node["TotalTime"] = 5; node["IsSpell"] = true; return node;
        }
        object Boss(uint oid, bool combat = true) => Actor(1, oid, center, combat: combat, subKind: 5, isEnemy: true, objectIndex: 2);
        object Helper(uint action = 0) => Actor(20, 0x115F, center, cast: action == 0 ? null : Spell(action), subKind: 5);
        object[] Players(Vector3? position = null) =>
            [Actor(100, 0, position ?? center - Vector3.UnitX * 10),
                .. Enumerable.Range(1, 7).Select(i => Actor((ulong)(100 + i), 0, center))];
        string World(double at, uint fate, IEnumerable<object> actors, bool full = false, float radius = 40,
            bool known = true, bool hasParty = true)
        {
            var node = JsonSerializer.SerializeToNode(new { t = at, kind = "WorldFrame", data = new {
                Full = full, Territory = 920, LocalPlayerId = 100, SourcePlayerId = 100, AllowedFateId = fate,
                Actors = actors.ToArray(), Party = Enumerable.Range(0, hasParty ? 8 : 0)
                    .Select(i => new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray() } })!;
            if (known) node["data"]!["ActiveFate"] = JsonSerializer.SerializeToNode(new { Id = fate, Center = Pose(center), Radius = radius });
            return node.ToJsonString();
        }
        string Effect(double at, uint action) => JsonSerializer.Serialize(new {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, MainTargetId = 101UL,
                TargetPosition = Pose(center), Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Skirmish actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")!, "Skirmish replay no executable PR requests");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Skirmish owner cleanup");
        }
        void Solver(Vector3 position)
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [position, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Skirmish actual solver");
        }
        var catalog = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaSkirmishCatalog")!;
        var definitions = ((IEnumerable)catalog.GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>().ToArray();
        Check(definitions.Length == 2, "Skirmish two reviewed modules");
        foreach (var definition in definitions)
        {
            var fate = (uint)Read(definition, "FateId")!;
            var oid = (uint)Read(definition, "PrimaryOid")!;
            var name = (string)Read(definition, "Name")!;
            foreach (var rule in Items(definition, "Rules"))
            {
                var action = ((uint[])Read(rule, "Actions")!)[0];
                var stack = Read(rule, "Kind")!.ToString() == "Stack";
                // The current navigation adapter decomposes a Cross into two
                // rectangles while retaining one composite display preview.
                var dangerCount = stack ? 0 : 1;
                if ((string)Read(rule, "Component")! == "Compress") dangerCount = 2;
                var player = stack ? center - Vector3.UnitX * 10 : center;
                load(id, [Header(920), World(0, fate, [Boss(oid), .. Players(player), Helper(action)], true),
                    Effect(1, action), World(2, fate, [Helper()]), World(3, fate, [])]);
                Seek(.3);
                Check((bool)Read(Model(fate), "Active")! && (float)Read(Model(fate), "SampleRadius")! == 40,
                    "Skirmish FATE dynamic boundary");
                Check(previews().Length == 2 && previews().Count(p => ((string)Read(p, "Id")!).Contains("bozja/arena")) == 1
                    && dangers() == dangerCount
                    && Items(Snapshot(), "GoalAreas").Length == (stack ? 1 : 0),
                    $"Skirmish shape and stack join channels {action}: previews={previews().Length} [{string.Join(",", previews().Select(p => Read(p, "Id")))}], dangers={dangers()}, goals={Items(Snapshot(), "GoalAreas").Length}");
                Check(Items(Script(), "PredictedDamage").Length == 0, "Skirmish no invented stack damage");
                Check((ulong)Read(Script(), "PlannedTargetId")! == 1, "Skirmish actual legal boss target plan");
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                    "Skirmish active NPC suppression");
                Solver(player);
                Seek(1.1); Check(Items(Model(fate), "Casts").Length == 1, "Skirmish real Effect retained");
                Seek(2.1); Check(Items(Model(fate), "Casts").Length == 0 && dangers() == 0
                    && Items(Snapshot(), "GoalAreas").Length == 0, "Skirmish watched Finish removes");
                Seek(.3); manager.SetMechanismEnabled(id, name, false); Seek(.4);
                Check(previews().Length == 0 && dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                    && Read(Snapshot(), "ArenaBoundary") == null, "Skirmish mechanism disabled");
                manager.SetMechanismEnabled(id, name, true); Seek(.5);
                Release(); Console.WriteLine("PASS native Skirmish actual DLL cold geometry stack solver Finish owner " + fate + "/" + action);
            }
            load(id, [Header(920), World(0, fate, [Boss(oid), .. Players()], true),
                World(1, fate, [], radius: 60), World(2, 0, []), World(3, fate, []), World(4, fate, [])]);
            Seek(1.1); Check((float)Read(Model(fate), "SampleRadius")! == 60, "Skirmish dynamic radius changed");
            Seek(2.1); Check((bool)Read(Model(fate), "Active")! && (float)Read(Model(fate), "SampleRadius")! == 30,
                "Skirmish actual FATE change resets and repulls in same manager frame");
            Seek(3.1); Check((bool)Read(Model(fate), "Active")!, "Skirmish same primary repull after reset");
            Release(); Console.WriteLine("PASS native Skirmish actual dynamic radius changed FATE reset same-primary rebuild " + fate);
            load(id, [Header(920), World(0, fate, [Boss(oid), .. Players()], true, known: false), World(1, fate, [], known: false)]);
            Seek(.3); Check(!(bool)Read(Model(fate), "Active")!
                && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("缺少") == true
                && Read(Snapshot(), "ArenaBoundary") == null, "Skirmish old replay missing FATE diagnosed");
            Release(); Console.WriteLine("PASS native Skirmish actual missing legacy FATE input no guessed output " + fate);
        }
        var firstOid = (uint)Read(definitions[0], "PrimaryOid")!;
        var secondOid = (uint)Read(definitions[1], "PrimaryOid")!;
        object WithFate(object actor)
        {
            var node = JsonSerializer.SerializeToNode(actor)!; node["FateId"] = 777; return node;
        }
        load(id, [Header(920), World(0, 777, [WithFate(Boss(firstOid)),
            WithFate(Actor(2, secondOid, center, combat: true, subKind: 5, isEnemy: true, objectIndex: 4)),
            .. Players(), Helper(21315)], true), World(1, 1605, []), World(2, 1605, [])]);
        Seek(.3);
        uint[] LoadedFates() => Items(Read(Script(), "region")!, "Loaded")
            .Select(e => (uint)Read(Read(e, "Definition")!, "FateId")!).ToArray();
        Check(LoadedFates().SequenceEqual(new uint[] { 1612, 1605 }), "Skirmish actual pending promotion reverse order");
        Seek(1.1);
        Check(LoadedFates().SequenceEqual(new uint[] { 1612, 1605 })
            && Read(Read(Read(Script(), "region")!, "Selected")!, "Definition") == definitions[1]
            && (bool)Read(Model(1605), "Active")! && (bool)Read(Model(1612), "Active")!
            && Items(Model(1605), "Casts").Length == 1, "Skirmish actual double reset append order selected tie cold replay");
        Release(); Console.WriteLine("PASS native Skirmish actual two-module reset append order Selected tie and cold replay");
        load(id, [Header(920), World(0, 1605, Players(), true, hasParty: false),
            World(1, 1605, [Boss(firstOid)], hasParty: false), World(2, 1605, []), World(3, 1605, [])]);
        Seek(1.1); Check(Read(Read(Script(), "region")!, "Selected") == null
            && Read(Model(1605), "Primary") == null, "Skirmish actual no Party0 keeps new boss pending");
        Seek(2.1); Check((bool)Read(Model(1605), "Active")!, "Skirmish actual Party0 commit promotes pending");
        Release(); Console.WriteLine("PASS native Skirmish actual empty Party0 creation pending then roster promotion");
    }
}
