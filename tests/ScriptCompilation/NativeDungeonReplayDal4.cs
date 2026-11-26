using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDAL4DiabloArmament(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-720, 0, -760);
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
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
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(m => (bool)Read(m, "IsDiablo")!);
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
        object Boss(object? cast = null) => Actor(1, 0x31B3, center, cast: cast, combat: true, subKind: 5, isEnemy: true);
        object Player(ulong actorId = 100, uint status = 0, bool pet = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0, center + Vector3.UnitX * (actorId == 100 ? 3 : -15)))!;
            node["HitboxRadius"] = .5;
            if (pet) { node["Kind"] = 2; node["SubKind"] = 2; }
            if (status != 0)
                node["Statuses"] = JsonSerializer.SerializeToNode(new[] { new { Id = status, SlotIndex = 59, SourceId = 1UL, ExpiresAt = Start.AddSeconds(5) } });
            return node;
        }
        object Helper(uint action = 0, double at = 0, ulong actorId = 20, Vector3? position = null)
            => Actor(actorId, 0x233C, position ?? center, cast: action == 0 ? null : Spell(action, at, position));
        object Orb(ulong actorId = 80) => Actor(actorId, 0x31B5, center + Vector3.UnitZ * 12);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, at, actors, full, removed, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }, new { Slot = 2, EntityId = 102UL }], sourcePlayerId: 100);
        string Effect(double at, uint value, ulong source = 20, ulong target = 100)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = value, ActionType = 1, MainTargetId = target,
                    TargetPosition = Pose(center), GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) } });
        string Map(double at, uint state)
            => JsonSerializer.Serialize(new { t = at, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = 0x33u, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(at) } });
        string Icon(double at)
            => JsonSerializer.Serialize(new { t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100UL, TargetId = 100UL, MarkerId = 23u, Timestamp = Start.AddSeconds(at) } });
        void Seek(double at, bool expectedError = false)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id)
                && (expectedError || (string)manager.ErrorMessage == ""), "DAL4 replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release(bool expectedError = false)
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")!, "DAL4 replay has no executable PR facing target or action restriction");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "DAL4 owner release");
            if (expectedError)
            {
                Check(((string)manager.ErrorMessage).Contains("RuinousPseudoomen"), "DAL4 exact expected script error retained");
                manager.SetError("");
            }
        }
        void Solver()
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "DAL4 actual solver");
        }
        void Apply(double at, IEnumerable<object> actors)
        {
            var world = Read(Script(), "ReplayWorld")!;
            using var json = JsonDocument.Parse(World(at, actors));
            var frame = JsonSerializer.Deserialize(json.RootElement.GetProperty("data").GetRawText(),
                assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!;
            var decoded = Items(frame, "Actors");
            foreach (var input in json.RootElement.GetProperty("data").GetProperty("Actors").EnumerateArray())
            {
                var actual = decoded.Single(a => (ulong)Read(a, "Id")! == input.GetProperty("Id").GetUInt64());
                var position = input.GetProperty("Position");
                Check((Vector3)Read(actual, "Position")! == new Vector3(position.GetProperty("X").GetSingle(), position.GetProperty("Y").GetSingle(), position.GetProperty("Z").GetSingle()),
                    "DAL4 manual true production JSON coordinates");
            }
            world.GetType().GetMethod("Apply", flags)!.Invoke(world, [frame]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(at));
        }
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);

        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x31B3u])! && !(bool)owns.Invoke(null, [920u, 0x31B3u])!
            && !(bool)owns.Invoke(null, [975u, 0x31B4u])!, "DAL4 precise ownership");
        foreach (var action in new uint[] { 23726, 23746, 23718, 23730, 23737, 23715, 23728 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Helper(action)], true),
                Effect(1, action), World(2, [Helper()]), World(3, [])]);
            Seek(.2);
            Check(Shown().Length == 1 && dangers() == 1 && Items(Read(Model(), "Definition")!, "Rules").Length == 16, "DAL4 actual standard " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 23749 }), "DAL4 precise source suppression no DeathRay");
            Solver();
            Seek(1.1); Check(Shown().Length == 1, "DAL4 actual Effect doesn't Finish");
            Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL4 actual Finish");
            Seek(.2);
            manager.SetMechanismEnabled(id, "迪亚布罗魔兵", false); Seek(.3);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL4 disabled withdraws all");
            manager.SetMechanismEnabled(id, "迪亚布罗魔兵", true); Seek(.2);
            Check(dangers() == 1, "DAL4 reverse Seek rehydrates");
            Release();
            Console.WriteLine($"PASS native DAL4 actual action={action} geometry solver raw Finish source gate disabled Seek owner");
        }
        load(id, [Header(975), World(0, [Boss(), Player(), Helper(23711)], true), Map(1, 0x80004), Map(2, 0x20001), Map(3, 0x80004), World(4, [])]);
        Seek(.2); Check(Shown().Length == 1 && dangers() == 1, "DAL4 arena ring");
        Seek(1.2); Check(Shown().Length == 1, "DAL4 arena restore retains ring");
        Seek(2.2); Check(Shown().Length == 0 && (float)Read(Read(Snapshot(), "ArenaBoundary")!, "Radius")! == 17, "DAL4 actual shrink");
        Seek(3.2); Check(Read(Snapshot(), "ArenaBoundary")!.GetType().Name == "WorldPolygonGeometry", "DAL4 actual restore polygon");
        Release(); Console.WriteLine("PASS native DAL4 actual Map ring restore retained shrink17 default48 polygon owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Helper(23744)], true), World(1, [Helper()]),
            Effect(2, 23744), World(3, []), World(4, [Helper(position: center + Vector3.UnitZ * 10)]),
            Effect(4.1, 23745), World(5, [])]);
        Seek(.2); Check(Shown().Length == 5 && dangers() == 5, "DAL4 five published Nox");
        Seek(1.2); Check(Shown().Length == 5, "DAL4 Nox Finish keeps");
        Seek(2.2); Check(Shown().Length == 4 && dangers() == 4, "DAL4 Nox first actual Effect");
        Seek(4.2); Check(Shown().Length == 3, "DAL4 Nox next actual Effect");
        Release(); Console.WriteLine("PASS native DAL4 actual Nox cached future imminent five to four to three real Effect no Finish owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Orb()], true), World(1, [], removed: [80]),
            World(2, [Orb()]), Effect(3, 23733, 80), World(4, [])]);
        Seek(.2); Check(Shown("diablo/orb/").Length == 1 && Text().Contains("Soak"), "DAL4 orb safe outline hint");
        Check(Items(Snapshot(), "GoalAreas").Length == 0, "DAL4 orb no invented R1.5 goal");
        Seek(1.2); Check(Shown("diablo/orb/").Length == 1, "DAL4 old orb Destroy retained");
        Seek(2.2); Check(Shown("diablo/orb/").Length == 2, "DAL4 same ID new orb doesn't replace original");
        Seek(3.2); Check(Shown("diablo/orb/").Length == 1 && Items(Model(), "DiabloOrbs").Length == 1, "DAL4 orb Effect original identity");
        Release(); Console.WriteLine("PASS native DAL4 actual orb safe outline dynamic intersection destroyed original sameID new Effect no Goal owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Player(101), Helper()], true), Icon(.1),
            Effect(.2, 23741), Effect(1, 23740), Effect(2, 23742), World(3, [])]);
        Seek(.3); Check(Items(Model(), "SeekerBaits").Length == 1 && Items(Model(), "SeekerLines").Length == 1
            && Shown("seeker/bait/").Length == 1 && Shown("seeker/stack/").Length == 1, "DAL4 rectangle bait and line");
        Seek(1.2); Check(Items(Model(), "SeekerBaits").Length == 0 && Items(Model(), "SeekerLines").Length == 1, "DAL4 icon actual Effect FIFO");
        Seek(2.2); Check(Items(Model(), "SeekerLines").Length == 0, "DAL4 line actual target Effect");
        Release(); Console.WriteLine("PASS native DAL4 actual icon23 rectangle outline line stack effect target no invented min8 owner");

        foreach (var action in new uint[] { 23732, 23751, 25028, 23736, 23747 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Helper(action)], true),
                Effect(1, action), World(2, [Helper()]), World(3, [])]);
            Seek(.2);
            if (action == 23747)
                Check(Lines().Length == 1 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0, "DAL4 KB line no nav damage");
            else Check(Items(Script(), "PredictedDamage").Length == 1 && Text().Contains("Raidwide"), "DAL4 dynamic raidwide");
            Seek(1.2); Check(Items(Model(), "Casts").Length == 1, "DAL4 actual Effect retains cast");
            Seek(2.2); Check(Items(Model(), "Casts").Length == 0 && Lines().Length == 0, "DAL4 matching Finish");
            Release(); Console.WriteLine($"PASS native DAL4 actual action={action} raw NPC clock or wall line Effect retained Finish owner");
        }
        load(id, [Header(975), World(0, [Boss(), Player(100, 2657), Player(101), Player(102, 2657, true)], true),
            World(1, [Player(102, pet: true)]), World(3, []), World(4, [Player()]), World(6, [])]);
        Seek(.2); Check(Text().Contains("Pyretic") && !Text().Contains("Stop everything"), "DAL4 early Pyretic only diagnostic");
        Check(((DateTime[])Read(Model(), "DiabloBombs")!)[2] != default, "DAL4 cold Pet slot59 recovered");
        Seek(1.2); Check(((DateTime[])Read(Model(), "DiabloBombs")!)[2] != default, "DAL4 hot Pet Lose not consumed");
        Seek(3.2); Check(Text().Contains("Stop everything"), "DAL4 2.5 second hint");
        Seek(4.2); Check(!Text().Contains("Pyretic") && dangers() == 0 && Lines().Length == 0, "DAL4 normal Lose only own slot clear");
        Release(); Console.WriteLine("PASS native DAL4 actual Bomb cold Pet60 hot Lose old slot Pyretic diagnostic threshold no execution owner");

        var chainedCast = (System.Text.Json.Nodes.JsonNode)Spell(23712, 1);
        chainedCast["Rotation"] = 0;
        (uint Oid, Vector3 Offset, float Rotation)[] gates =
        [
            (0x1EB1D8, new(0, 0, -20), 0), (0x1EB1D8, new(10, 0, 0), 2),
            (0x1EB1D7, new(0, 0, 10), 1), (0x1EB1D7, new(-10, 0, 0), MathF.PI / 2),
            (0x1EB1D6, new(20, 0, 20), 3), (0x1EB1D6, new(-20, 0, -20), 4),
            (0x1EB1D9, new(20, 0, -20), 5), (0x1EB1D9, new(-20, 0, 20), 6),
        ];
        load(id, [Header(975), World(0, [Boss(), Player(), Helper(),
                .. gates.Select((g, i) => Actor((ulong)(200 + i), g.Oid, center + g.Offset, rotation: g.Rotation))], true),
            World(1, [Boss(chainedCast)]), World(1.5, [Helper(24995, 1.5)]), World(2, [Helper()]),
            World(2.5, [Helper(24995, 2.5)]), World(3, [Helper()]), World(20, [])]);
        Seek(1.2);
        Check(Items(Model(), "DiabloRuinous").Length == 3 && Shown().Length == 2, "DAL4 actual eight gates create three show two");
        var predictions = Items(Model(), "DiabloRuinous").Select(a => (DateTime)Read(Read(a, "Cast")!, "Activation")!).ToArray();
        Check(predictions[1] == predictions[0].AddSeconds(4d) && predictions[2] == predictions[1].AddSeconds(4.1d),
            "DAL4 actual chained event clocks");
        Seek(2.2); Check(Items(Model(), "DiabloRuinous").Length == 2, "DAL4 unrelated caster Finish global FIFO first");
        Seek(3.2); Check(Items(Model(), "DiabloRuinous").Length == 1
            && !(bool)Read(Items(Model(), "DiabloRuinous")[0], "Risky")!, "DAL4 single hidden promotion retains false risk");
        Seek(20); Check(Items(Model(), "DiabloRuinous").Length == 1
            && !(bool)Read(Items(Model(), "DiabloRuinous")[0], "Risky")!, "DAL4 overdue single never gains invented risk or TTL");
        Release(); Console.WriteLine("PASS native DAL4 actual eight gate chain exact clocks cross caster Finish FIFO hidden promotion retained risk no TTL owner");

        // Manual frames keep the same script instance and force exactly one
        // Framework commit, unlike Seek which reconstructs every script.
        foreach (var fresh in new[] { false, true })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Helper()], true), World(10, [])]);
            Seek(.2);
            Apply(1, fresh ? [Helper(23712, 1, 81), Orb(90)] : [Boss(Spell(23712, 1)), Orb(90)]);
            Update();
            Check(Items(Model(), "DiabloRuinous").Length == 1 && Text().Contains("RuinousPseudoomen")
                && previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL4 hot failure withdraws whole output");
            Update();
            Check(Items(Model(), "DiabloRuinous").Length == (fresh ? 1 : 2), "DAL4 same instance new versus old retry");
            Release(true);
            Console.WriteLine($"PASS native DAL4 actual hot failure fresh={fresh} exact retry partial state withdraw all drawing nav boundary owner");
        }
        foreach (var restore in new[] { false, true })
        {
            var committedCast = Spell(24911);
            load(id, [Header(975), World(0, [Boss(restore ? committedCast : null), Player()], true), World(10, [])]);
            Seek(.2);
            Apply(1, [Boss(Spell(23712, 1)), Orb(90)]); Update();
            Check(Items(Model(), "DiabloRuinous").Length == 1 && previews().Length == 0, "DAL4 manual initial failed callback");
            Apply(2, [Boss(restore ? committedCast : Spell(23712, 1.5)), Orb(90)]); Update();
            var model = Model();
            Check(Items(model, "DiabloRuinous").Length == (restore ? 1 : 2), "DAL4 committed snapshot restore versus corrected elapsed retry");
            var actors = (IDictionary)Read(model, "Actors")!;
            var actor = actors[1UL]!;
            Check((uint)Read(Read(actor, "Cast")!, "ActionId")! == 23712, "DAL4 physical failed cast survives prior committed restore");
            if (!restore)
            {
                var last = Items(model, "DiabloRuinous")[1];
                var activation = (DateTime)Read(Read(last, "Cast")!, "Activation")!;
                Check(activation == Start.AddSeconds(1.5).AddSeconds(5 + .3f).AddSeconds(1.5d),
                    "DAL4 corrected elapsed changes actual retry activation");
            }
            Release(true);
            Console.WriteLine($"PASS native DAL4 actual committed restore={restore} physical cast retained corrected snapshot elapsed no duplicate old callback owner");
        }
        load(id, [Header(975), World(0, [Boss(), Player()], true), World(10, [])]);
        Seek(.2);
        var frostCenter = new Vector3(-830, 0, 190);
        var frostPlayer = JsonSerializer.SerializeToNode(Player())!;
        frostPlayer["Position"] = JsonSerializer.SerializeToNode(Pose(frostCenter));
        object FrostCaster(object? cast) => Actor(40, 0x319F, frostCenter, cast: cast, subKind: 5);
        Apply(.5, [frostPlayer, Actor(39, 0x319C, frostCenter, combat: true, subKind: 5, isEnemy: true),
            FrostCaster(Spell(23589, .5))]);
        Update();
        var frost = Items(Read(Script(), "region")!, "Encounters").Single(m => (uint)Read(Read(m, "Definition")!, "PrimaryOid")! == 0x319C);
        Check((bool)Read(frost, "Active")! && Items(frost, "ThermalGusts").Length == 1, "DAL4 actual later CE54 active queue");
        var loaded = Items(Read(Script(), "region")!, "Loaded");
        Check(Array.IndexOf(loaded, Model()) >= 0 && Array.IndexOf(loaded, Model()) < Array.IndexOf(loaded, frost),
            "DAL4 actual failing subscriber precedes CE54");
        Apply(1, [FrostCaster(Spell(23712, 1)), Orb(90)]); Update();
        Check(Items(frost, "ThermalGusts").Length == 0, "DAL4 successful Finish before failing Start");
        var frostActors = (IDictionary)Read(frost, "Actors")!;
        Check((uint)Read(Read(frostActors[40UL]!, "Cast")!, "ActionId")! == 23712, "DAL4 global physical cast install before Start");
        Apply(2, [FrostCaster(Spell(23712, 1)), Orb(90)]); Update();
        Check(Items(Model(), "DiabloRuinous").Length == 2 && Read(frost, "ThermalSequenceError") == null,
            "DAL4 actual retry never repeats old Thermal Finish");
        Apply(3, [FrostCaster(Spell(23589, .5)), Orb(90)]); Update();
        Check((uint)Read(Read(frostActors[40UL]!, "Cast")!, "ActionId")! == 23712,
            "DAL4 restore committed input never rewrites failed physical cast in later subscriber");
        Apply(4, [FrostCaster(null), Orb(90)]); Update();
        Check(Read(frostActors[40UL]!, "Cast") == null && Read(frost, "ThermalSequenceError") == null,
            "DAL4 actual physical clear never consumes empty Thermal FIFO");
        Release(true);
        Console.WriteLine("PASS native DAL4 actual cross CE54 shared physical install committed restore and clear no repeated successful Finish owner");
        load(id, [Header(975), World(0, [Boss(Spell(23712)), Player(), Orb()], true), World(1, []), World(2, [])]);
        Seek(.2); Check(!(bool)Read(Model(), "Active")! && (bool)Read(Model(), "Completed")! && Read(Model(), "Primary") == null
            && previews().Length == 0 && dangers() == 0 && Items(Model(), "DiabloOrbs").Length == 0, "DAL4 cold failed unload no later components");
        Release(); Console.WriteLine("PASS native DAL4 actual cold gate failure unload diagnostic no orphan partial outputs owner");
    }
}
