using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDAL1Gauntlet(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(222, 0, -689);
        object? Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object[] Items(object obj, string name) => ((IEnumerable)Read(obj, name)!).Cast<object>().ToArray();
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(m => (bool)Read(m, "IsGauntlet")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility")!;
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        object[] Shown(string key = "bozja/aoe/") => previews().Where(p => ((string)Read(p, "Id")!).Contains(key)).ToArray();
        string Text() => (string?)Read(Script(), "MechanicHintsText") ?? "";
        object Spell(uint action, double at = 0, Vector3? location = null, float rotation = .37f)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, location ?? center, rotation, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss() => Actor(1, 0x32BF, center, combat: true, subKind: 5, isEnemy: true);
        object Aux(ulong actorId, uint oid, bool targetable = false, bool dead = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, oid, center, subKind: 5))!;
            node["Targetable"] = targetable; node["Dead"] = dead;
            return node;
        }
        object Player(ulong actorId = 100, Vector3? position = null, uint status = 0, bool dead = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0, position ?? center))!;
            node["HitboxRadius"] = .5; node["Dead"] = dead;
            if (status != 0)
                node["Statuses"] = JsonSerializer.SerializeToNode(new[] {
                    new { Id = status, SlotIndex = 59, SourceId = 1UL, ExpiresAt = Start.AddSeconds(30) } });
            return node;
        }
        object Helper(uint action = 0, double at = 0, ulong actorId = 20, Vector3? position = null, float rotation = .37f)
            => Actor(actorId, 0x233C, position ?? center,
                cast: action == 0 ? null : Spell(action, at, position, rotation));
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, at, actors, full, removed, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, ulong target = 100, ulong source = 20)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = target,
                    TargetPosition = Pose(center), GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) } });
        string Icon(double at, uint icon, ulong target = 100)
            => JsonSerializer.Serialize(new { t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = target, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(at) } });
        string Tether(double at, ulong source)
            => JsonSerializer.Serialize(new { t = at, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = source, TargetId = 11UL, TetherId = 4u, Timestamp = Start.AddSeconds(at) } });
        string Map(double at)
            => JsonSerializer.Serialize(new { t = at, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = 0x31u, Param1 = (ushort)1, Param2 = (ushort)2, Timestamp = Start.AddSeconds(at) } });
        List<string> Begin(int phase, IEnumerable<object> extra, Vector3? player = null)
        {
            var records = new List<string> { Header(975), World(0,
                [Boss(), Player(position: player), Player(101, center - Vector3.UnitX * 15),
                    Aux(10, 0x32C8), Aux(11, 0x32CB), Aux(12, 0x32CA), .. extra], true) };
            if (phase >= 1) records.Add(World(.05, [Aux(10, 0x32C8, true)]));
            if (phase >= 2) records.Add(World(.1, [Aux(11, 0x32CB, true)]));
            return records;
        }
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Gauntlet actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")!, "Gauntlet replay no executable PR requests");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Gauntlet owner release");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Gauntlet actual solver");
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x32BFu])! && !(bool)owns.Invoke(null, [920u, 0x32BFu])!
            && !(bool)owns.Invoke(null, [975u, 0x32C8u])!, "Gauntlet precise primary ownership");
        foreach (var (action, phase, aoe) in new[] {
            (24337u, 0, true), (24326u, 0, false), (24345u, 1, true),
            (24352u, 1, false), (24361u, 2, false), (24357u, 2, true) })
        {
            var records = Begin(phase, [Helper(action)]);
            records.AddRange([Effect(1, action), World(2, [Helper()]), World(3, [])]);
            load(id, records); Seek(.3);
            Check((int)Read(Model(), "GauntletPhase")! == phase && Items(Read(Model(), "Definition")!, "Rules").Length == 16,
                "Gauntlet actual phase and sixteen components");
            Check(Shown().Length == (aoe ? 1 : 0) && dangers() == (aoe ? 1 : 0), "Gauntlet standard geometry " + action);
            if (action == 24326) Check(Text().Contains("Enrage!") && Text().Contains("s left"), "Gauntlet dynamic enrage");
            if (action is 24352 or 24361) Check(Text().Contains("Raidwide") && Items(Script(), "PredictedDamage").Length == 1,
                "Gauntlet global raidwide damage");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Gauntlet actual NPC suppression");
            if (aoe) Solver(center);
            Seek(1.1); Check(Items(Model(), "Casts").Length == 1, "Gauntlet Effect retains standard cast");
            Seek(2.1); Check(Items(Model(), "Casts").Length == 0 && dangers() == 0, "Gauntlet standard real Finish");
            Seek(.3); manager.SetMechanismEnabled(id, "第四军团黑色燃焰", false); Seek(.4);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Gauntlet disabled all output");
            manager.SetMechanismEnabled(id, "第四军团黑色燃焰", true); Seek(.3);
            Check((bool)Read(Model(), "Active")!, "Gauntlet reverse Seek rebuild");
            Release();
            Console.WriteLine($"PASS native Gauntlet actual standard action={action} phase={phase} geometry damage solver source gate real Finish disable Seek owner");
        }
        var first = Begin(0, [Helper(24339)]);
        first.AddRange([Map(1), World(2, [Helper()]), World(3, [])]);
        load(id, first); Seek(.3);
        Check(Shown().Length == 1 && dangers() == 1, "Gauntlet custom ring both views");
        Solver(center);
        Seek(1.1);
        dynamic arena = Read(Snapshot(), "ArenaBoundary")!;
        Check(Shown().Length == 0 && (bool)arena.Contains(center + Vector3.UnitX * 23, 0f)
            && !(bool)arena.Contains(center + Vector3.UnitX * 23.01f, 0f), "Gauntlet actual Map shrink23 clears ring");
        Release(); Console.WriteLine("PASS native Gauntlet actual custom arena hole navigation real solver Map23 owner");

        first = Begin(0, [Helper()]);
        first.AddRange([Icon(.2, 261), Icon(.3, 261), Icon(.4, 261), Effect(1, 24334), World(20, [])]);
        load(id, first); Seek(.5);
        Check(Shown().Length == 2 && Items(Model(), "Casts").Length == 3, "Gauntlet Impact first two of raw FIFO");
        Seek(1.1); Check(Shown().Length == 2 && Items(Model(), "Casts").Length == 2, "Gauntlet Impact first Effect pops one");
        Seek(20); Check(Shown().Length == 2, "Gauntlet Impact no invented TTL");
        Release(); Console.WriteLine("PASS native Gauntlet actual Icon261 locked 24square first2 FIFO real Effect overdue owner");

        var blades = Enumerable.Range(0, 10).Select(i => Actor((ulong)(30 + i), 0x32C1,
            center - Vector3.UnitZ * (10 + i), rotation: i < 6 ? 0 : MathF.PI / 2)).ToArray();
        first = Begin(0, [Helper(), .. blades]);
        first.AddRange([World(.2, [Player(status: 1709),
                Actor(30, 0x32C1, center - Vector3.UnitZ * 10, cast: Spell(24327, .2, rotation: 0))]),
            World(1, []), World(5.1, []), World(6, [Player()]), World(7, [])]);
        load(id, first); Seek(.3);
        Check(Shown().Length == 4 && Shown("bozja/eye/").Length == 1 && Shown("bozja/pupil/").Length == 1
            && Lines().Length > 0, $"Gauntlet actual Unseen eye arc shown={Shown().Length} eye={Shown("bozja/eye/").Length} pupil={Shown("bozja/pupil/").Length} lines={Lines().Length}");
        object Eye() => ((IEnumerable)Model().GetType().GetMethod("GauntletEyes", flags)!
            .Invoke(Model(), [((IDictionary)Read(Model(), "Actors")!)[100UL]])!).Cast<object>().Single();
        Seek(1.1); Check((DateTime)Read(Eye(), "Activation")! < Start.AddSeconds(5.6), "Gauntlet raw eye clock doesn't drift by elapsed second");
        Seek(5.1); Check(Read(Script(), "PlannedFacing") != null && Shown("bozja/eye/").Length == 1, "Gauntlet actual facing within lead window");
        Seek(6.1); Check(Shown("bozja/eye/").Length == 0 && Read(Script(), "PlannedFacing") == null, "Gauntlet actual weakpoint Lose");
        Release(); Console.WriteLine("PASS native Gauntlet actual ten blades minority weakpoint eye pupil arc facing dynamic raw clock Lose owner replay isolation");

        var second = Begin(1, [Helper(), Actor(40, 0x1EB213, center + Vector3.UnitX * 6)]);
        second.AddRange([Icon(.2, 288), Icon(.3, 288, 101), Effect(1, 24343, 999),
            World(2, [Helper(position: center + Vector3.UnitX * 6)]), Effect(2.1, 24348), World(3, [])]);
        load(id, second); Seek(.4);
        Check(Shown("gauntlet/donut/").Length == 2 && Items(Script(), "PredictedDamage").All(d => (string)Read(d, "Type")! != "Shared")
            && Items(Model(), "Towers").Length == 1, "Gauntlet donut background no shared damage tower");
        Check((DateTime)Read(Items(Model(), "Towers")[0], "Activation")! == Start.AddSeconds(.05).AddSeconds(9d), "Gauntlet tower exact nine");
        dynamic ring = Read(Shown("gauntlet/donut/")[0], "Geometry")!;
        Check(!(bool)ring.Contains(center + Vector3.UnitX * 3.9f, 0f)
            && (bool)ring.Contains(center + Vector3.UnitX * 6, 0f), "Gauntlet actual donut4to8");
        Seek(1.1); Check(Shown("gauntlet/donut/").Length == 0, "Gauntlet unmatched stack Effect clears all");
        Seek(2.2); Check(Items(Model(), "Towers").Length == 0 && dangers() == 0, "Gauntlet real position tower Effect");
        Release(); Console.WriteLine("PASS native Gauntlet actual cold tower9 donut4to8 background min2 nav no shared damage unmatched Effect owner");

        second = Begin(1, [Helper(24341, rotation: 0)], center + Vector3.UnitX * 3);
        second.AddRange([World(1, []), World(2, [Helper()]), World(3, [])]);
        load(id, second); Seek(.3);
        Check(Lines().Length == 1 && dangers() == 0 && (Vector3)Read(Lines()[0], "End")! == center + Vector3.UnitX * 18,
            "Gauntlet actual Turbine15 line while source broad predicate sampling is rejected");
        Solver(center + Vector3.UnitX * 3);
        Seek(2.1); Check(Lines().Length == 0 && dangers() == 0, "Gauntlet Turbine matching Finish");
        Release(); Console.WriteLine("PASS native Gauntlet actual Turbine15 line source broad sampling rejection solver Finish owner");

        second = Begin(1, [Helper(24341, position: center + new Vector3(20, 0, 20))], center);
        second.Insert(2, Map(.02));
        second.AddRange([World(1, []), World(2, [Helper()]), World(3, [])]);
        load(id, second); Seek(.3);
        Check((float)Read(Model(), "SampleRadius")! == 23 && dangers() > 0 && Lines().Length == 1,
            "Gauntlet shrunk23 retains actual offcenter Turbine converted navigation");
        Solver(center);
        Release(); Console.WriteLine("PASS native Gauntlet actual shrunk23 offcenter Turbine finite navigation retained real solver owner");

        var third = Begin(2, [Helper(), Actor(21, 0x32CC, center - Vector3.UnitZ * 20)], center - Vector3.UnitZ * 20);
        third.AddRange([Tether(.2, 21), World(.3, [Helper(24355, .3, rotation: 0)]),
            World(1, [Helper()]), Effect(2, 24814), World(3, [Aux(11, 0x32CB, true, true), Aux(12, 0x32CA, false, true)]), World(4, [])]);
        load(id, third); Seek(.4);
        Check(Lines().Length == 1 && dangers() == 0 && Shown().Length == 1
            && (Vector3)Read(Lines()[0], "End")! == center + Vector3.UnitZ * 20, "Gauntlet North40 Storm separate display");
        Solver(center - Vector3.UnitZ * 20);
        Seek(1.1); Check(Lines().Length == 1, "Gauntlet Wind Finish retained");
        Seek(2.1); Check(Lines().Length == 0 && Shown().Length == 1, "Gauntlet North actual Effect only clears KB");
        Seek(3.1); Check((bool)Read(Model(), "Completed")! && !(bool)Read(Model(), "Active")! && Read(Script(), "encounter") == null
            && dangers() == 0 && previews().Length == 0 && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
            "Gauntlet both original final actors dead completes all output");
        Release(); Console.WriteLine("PASS native Gauntlet actual Storm tether North40 source broad sampling rejection solver Finish retained real Effect final phase owner");
    }
}
