using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDal3(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(650, 0, -659);
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
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Shown(string key = "bozja/aoe/") => previews().Where(p => ((string)Read(p, "Id")!).Contains(key)).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility")!;
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string Text() => (string?)Read(Script(), "MechanicHintsText") ?? "";
        object Spell(uint action, double at = 0, Vector3? location = null)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, location ?? center, .37f, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss(object? cast = null, bool dead = false) => Actor(1, 0x31DD, center, cast: cast, combat: true, dead: dead, subKind: 5, isEnemy: true);
        object Dawon(bool targetable = true, object? cast = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(4, 0x31DE, center + Vector3.UnitX * 9, cast: cast, subKind: 5, isEnemy: true))!;
            node["Targetable"] = targetable;
            return node;
        }
        object Player(Vector3? p = null, ulong actorId = 100, uint status = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0, p ?? center))!;
            node["HitboxRadius"] = .5;
            if (status != 0)
                node["Statuses"] = JsonSerializer.SerializeToNode(new[] { new { Id = status, SlotIndex = 59, SourceId = 1UL, ExpiresAt = Start.AddSeconds(100) } });
            return node;
        }
        object Helper(uint action = 0, double at = 0, Vector3? position = null)
            => Actor(20, 0x233C, position ?? center, cast: action == 0 ? null : Spell(action, at, position));
        object Marker(ulong actorId, uint oid, Vector3 position)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, oid, position))!;
            node["Kind"] = 7;
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, at, actors, full, removed, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, uint value, ulong source = 20)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = value, ActionType = 1, MainTargetId = 100UL,
                    TargetPosition = Pose(center), GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) } });
        string Animation(double at, ulong source, uint state)
            => JsonSerializer.Serialize(new { t = at, kind = "EObjAnimation", type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
                data = new { SourceId = source, Param1 = (ushort)(state >> 16), Param2 = (ushort)state, Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DAL3 replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "DAL3 owner release");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "DAL3 real solver success");
        }
        foreach (var action in new uint[] { 23989, 23991, 24004, 24701, 24016, 24018 })
        {
            var second = action is 24701 or 24016 or 24018;
            var frames = new List<string> { Header(975), World(0,
                second ? [Boss(), Dawon(false), Player(), Helper(action)] : [Boss(), Player(), Helper(action)], true) };
            if (second) frames.Add(World(.05, [Dawon()]));
            frames.AddRange([Effect(1, action), World(2, [Helper()]), World(3, [])]);
            load(id, frames); Seek(.2);
            Check(Shown().Length == 1 && dangers() == (action == 23991 ? 2 : 1), "DAL3 standard geometry action " + action);
            Check((int)Read(Model(), "SaunionPhase")! == (second ? 1 : 0), "DAL3 standard phase");
            Solver(center);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }), "DAL3 source gate");
            Seek(1.1); Check(Shown().Length > 0, "DAL3 AOE Effect not Finish");
            Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL3 AOE Finish");
            Seek(.2);
            var name = (string)Read(Read(Model(), "Definition")!, "Name")!;
            manager.SetMechanismEnabled(id, name, false); Seek(.3);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL3 disabled mechanism");
            manager.SetMechanismEnabled(id, name, true); Seek(.2);
            Check(dangers() > 0, "DAL3 reverse Seek rehydrates");
            Release();
            Console.WriteLine($"PASS native DAL3 actual AOE action={action} phase geometry solver source gate Effect Finish switch reverse Seek owner");
        }
        load(id, [Header(975), World(0, [Boss(Spell(23997)), Player()], true),
            World(1, [Boss(), Dawon()]), Effect(2, 23992, 1), World(3, [])]);
        Seek(.2); Check(Shown().Length == 1 && dangers() == 2, "DAL3 mobile cross polygon drawing two navigation rectangles");
        dynamic mobile = Read(Shown()[0], "Geometry")!;
        Check((bool)mobile.Contains(center + new Vector3(8.5f, 0, 18), 0f)
            && !(bool)mobile.Contains(center + new Vector3(8.51f, 0, 26.51f), 0f), "DAL3 cross halfwidth8.5 exact gap");
        Seek(1.2); Check((int)Read(Model(), "SaunionPhase")! == 1 && Shown().Length == 1 && dangers() == 2, "DAL3 mobile Keep phase");
        Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL3 mobile true Effect");
        Release();
        Console.WriteLine("PASS native DAL3 actual mobile cross two rectangles phase kept instance Finish ignored real Effect owner");

        load(id, [Header(975), World(0, [Boss(), Dawon(false), Player(),
                Actor(30, 0x31E1, center + Vector3.UnitX * 13), Actor(31, 0x31E0, center - Vector3.UnitX * 13)], true),
            World(1, [Dawon()]), World(2, [Helper(24014, 2)]), World(3, [Helper()]), World(4, [])]);
        Seek(.2); Check(Shown().Length == 0, "DAL3 plumes absent P0");
        Seek(1.2); Check(Shown().Length == 2, "DAL3 cold plumes created P1");
        Seek(3.1); Check(Shown().Length == 0 && dangers() == 0, "DAL3 plume any relevant Finish clears all");
        Release();
        Console.WriteLine("PASS native DAL3 actual cold plume Created activation P1 two shapes global Finish clear solver owner");

        var spiral = new Vector3(629.4802f, 0, -679.0112f);
        load(id, [Header(975), World(0, [Boss(), Dawon(), Player(), Helper()], true),
            World(.1, [Helper(23986, .1, spiral)]), Effect(1, 23987), World(2, [Helper()]),
            World(3, [Marker(50, 0x1EB216, center)]), World(4, [], removed: [50]), World(5, [])]);
        Seek(.2); Check(Shown().Length == 6 && dangers() == 6, "DAL3 six risky spiral");
        Seek(1.1);
        Check((int)Read(Model(), "SpiralNumCasts")! == 1 && Shown().Length == 6, "DAL3 Spiral actual Effect increments");
        Seek(2.1); Check(Shown().Length == 6, "DAL3 Spiral Finish not clear");
        Seek(4.1); Check(Shown().Length == 0 && dangers() == 0 && (int)Read(Model(), "SpiralNumCasts")! == 1, "DAL3 marker destroyed clears only list");
        Seek(.2); Check(Shown().Length == 6, "DAL3 Spiral reverse Seek");
        Release();
        Console.WriteLine("PASS native DAL3 actual spiral six risks source points true Effect Finish ignored EObj Destroy count Seek owner");

        load(id, [Header(975), World(0, [Boss(), Dawon(), Player(), Helper()], true),
            World(.1, [Helper(23986, .1, spiral)]), World(.5, [], removed: [100]),
            Effect(1, 23987), Effect(2, 23987), World(3, [Helper(23986, 3)]),
            World(4, [Player()]), World(5, [])]);
        Seek(.2); Check(Shown().Length == 6, "DAL3 initial spiral query");
        Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL3 missing viewer withdraws output");
        Seek(4.1);
        var spiralViews = ((IEnumerable)Model().GetType().GetMethod("Visible", flags)!
            .Invoke(Model(), [null])!).Cast<object>().Take(6).ToArray();
        Check(spiralViews.Length == 6 && (bool)Read(spiralViews[0], "Danger")!
            && spiralViews.Skip(1).All(a => !(bool)Read(a, "Danger")!),
            "DAL3 missing viewer never queried hidden spiral heads before unknown Start reset");
        Release();
        Console.WriteLine("PASS native DAL3 actual missing viewer withdraws spiral without mutating hidden colors restores after reset");

        load(id, [Header(975), World(0, [Boss(), Dawon(), Player(), Helper(),
                Marker(40, 0x1EB1E8, center + Vector3.UnitZ * 16)], true),
            World(.05, []), Animation(.2, 40, 0x10002), Animation(.3, 40, 0x10002),
            Effect(1, 24011), Effect(2, 24012), World(3, [])]);
        Seek(.5); Check(Shown().Length == 1 && dangers() == 2 && Items(Model(), "saunionObey").Length == 2,
            $"DAL3 duplicate hot cross queue first only shown={Shown().Length} danger={dangers()} queue={Items(Model(), "saunionObey").Length} phase={Read(Model(), "SaunionPhase")} Dawon={Read(Model(), "Dawon")}");
        Seek(1.1); Check(Shown().Length == 1 && dangers() == 2 && Items(Model(), "saunionObey").Length == 1, "DAL3 unrelated shape true Effect pops FIFO");
        Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL3 FIFO drained");
        Release();
        Console.WriteLine("PASS native DAL3 actual EAnim duplicate first-only cross FIFO either Effect consumes owner");

        foreach (var action in new uint[] { 24002, 24003, 24005, 24020, 24013, 24006 })
        {
            var second = action is 24020 or 24013;
            load(id, [Header(975), World(0, second
                    ? [Boss(), Dawon(false), Player(center + Vector3.UnitX * 5), Player(center - Vector3.UnitX * 10, 101), Helper(action)]
                    : [Boss(), Player(center + Vector3.UnitX * 5), Player(center - Vector3.UnitX * 10, 101), Helper(action)], true),
                World(.05, second ? [Dawon()] : []), Effect(1, action), World(2, [Helper()]), World(3, [])]);
            Seek(.2);
            var damage = Items(Script(), "PredictedDamage").Length;
            Check(damage == (action is 24005 or 24020 or 24013 ? 1 : 0), "DAL3 actual prediction mode " + action);
            if (action == 24005) Check(Shown("saunion/bait/").Length == 1 && dangers() == 1 && Text().Contains("cleave"), "DAL3 target R5 outline peers");
            if (action == 24006)
            {
                Check(Lines().Length == 1 && dangers() == 0, "DAL3 KB line without navigation");
                Check((Vector3)Read(Lines()[0], "Start")! == center + Vector3.UnitX * 5
                    && (Vector3)Read(Lines()[0], "End")! == center + Vector3.UnitX * 26.499f,
                    "DAL3 actual square wall endpoint and hitbox");
            }
            Seek(1.1);
            Check(Items(Model(), "Casts").Any(), "DAL3 true Effect retained raw cast");
            Seek(2.1); Check(dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0
                && Shown("saunion/").Length == 0 && Lines().Length == 0, "DAL3 Finish cleanup " + action);
            Release();
            Console.WriteLine($"PASS native DAL3 actual action={action} target original NPC damage mode geometry line Effect Finish replay isolation owner");
        }
        load(id, [Header(975), World(0, [Boss(), Player(center + Vector3.UnitX * 26.4990234375f), Helper(24006)], true), World(1, [])]);
        Seek(.2);
        Check(Items(Model(), "Casts").Length == 1 && Lines().Length == 0 && dangers() == 0,
            "DAL3 zero movement remains in model without zero line preview");
        Release();
        Console.WriteLine("PASS native DAL3 actual Touchdown rounded zero displacement no line while raw cast retained");
        var namedPrimary = JsonSerializer.SerializeToNode(Boss())!;
        namedPrimary["Name"] = "named-primary"; namedPrimary["TargetId"] = 100UL;
        var namedDawon = JsonSerializer.SerializeToNode(Dawon())!;
        namedDawon["Name"] = "named-dawon"; namedDawon["TargetId"] = 101UL;
        load(id, [Header(975), World(0, [namedPrimary, namedDawon, Player(status: 2553), Player(center + Vector3.UnitX * 3, 101)], true),
            World(1, [Player()]), World(2, [])]);
        Seek(.2); Check(Text().Contains("named-primary & named-dawon") && dangers() == 1
            && Shown().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0, "DAL3 OneMind names future circle no draw/Goal");
        var actorMap = (IDictionary)Read(Model(), "Actors")!;
        var zones = ((IEnumerable)Model().GetType().GetMethod("NavigationForHost", flags)!.Invoke(Model(), [actorMap[100UL]])!).Cast<object>().ToArray();
        Check(zones.Length == 1 && (DateTime)Read(zones[0], "Activation")! == DateTime.MaxValue, "DAL3 original absolute future clock");
        var registered = Items(Snapshot(), "DangerAreas").Single();
        var window = Items(registered, "DangerWindows").Single();
        Check((DateTime)Read(window, "DangerFrom")! == DateTime.MaxValue
            && !(bool)window.GetType().GetMethod("IsActiveAt")!.Invoke(window, [(DateTime)runtime.CurrentTime])!,
            "DAL3 registered OneMind absolute window remains inactive now");
        Solver(center);
        Seek(1.1); Check(!Text().Contains("further") && dangers() == 0, "DAL3 OneMind Lose");
        Release();
        Console.WriteLine("PASS native DAL3 actual OneMind current names XOR absolute MaxValue future danger no immediate Goal no overflow real solver owner");

        var deadDawon = JsonSerializer.SerializeToNode(Dawon())!;
        deadDawon["Dead"] = true;
        load(id, [Header(975), World(0, [Boss(), Dawon(false), Player(center + Vector3.UnitX * 5), Helper(24006)], true),
            World(.05, [Dawon()]), World(.1, [Helper(23986, .1, spiral)]),
            World(1, [Boss(dead: true)]), World(2, [deadDawon]), World(3, [])]);
        Seek(.02); Check(Lines().Length == 1, "DAL3 P0 Touchdown visible before transition");
        Seek(.2); Check(Lines().Length == 0 && Shown().Length == 6 && dangers() == 6,
            "DAL3 P1 drops old Touchdown while new Spiral publishes");
        Seek(1.1); Check(Shown().Length == 6 && (bool)Read(Model(), "Active")!, "DAL3 one dead boss keeps P1 output");
        Seek(2.1);
        var completedModel = Items(Read(Script(), "region")!, "Encounters").Single(m => (bool)Read(m, "IsSaunion")!);
        Check(!(bool)Read(completedModel, "Active")! && (bool)Read(completedModel, "Completed")!
            && Read(Script(), "encounter") == null
            && previews().Length == 0 && Lines().Length == 0 && dangers() == 0
            && Items(Snapshot(), "GoalAreas").Length == 0 && Read(Snapshot(), "ArenaBoundary") == null
            && Items(Script(), "PredictedDamage").Length == 0 && Text() == "",
            $"DAL3 both dead completes and withdraws all published state before stop active={Read(completedModel, "Active")} complete={Read(completedModel, "Completed")} selected={Read(Script(), "encounter")} previews={previews().Length} lines={Lines().Length} danger={dangers()} goals={Items(Snapshot(), "GoalAreas").Length} boundary={Read(Snapshot(), "ArenaBoundary")} damage={Items(Script(), "PredictedDamage").Length} text={Text()}");
        Release();
        Console.WriteLine("PASS native DAL3 actual P0 KB phase retirement P1 Spiral single boss survival both dead full output withdrawal");
    }
}
