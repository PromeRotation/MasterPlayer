using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumDahuSavage(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.savage";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(82, 0, 137.9855f);
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        uint State() => (uint)Read(Read(Model(), "DahuTimeline"), "StateId");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        string[] Components() => ((IEnumerable)Read(Model(), "ActiveRules")).Cast<object>().Select(r => (string)Read(r, "Component")).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false)
            => Frame(937, t, actors, full, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? location = null, float rotation = .4f, bool spell = true)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, location ?? center, rotation, 100, at))!;
            node["IsSpell"] = spell; node["TotalTime"] = 3;
            return node;
        }
        object Boss(object? cast = null, bool dead = false)
            => Actor(1, 0x30A8, center, combat: true, isEnemy: true, cast: cast, dead: dead);
        object Helper(ulong number = 20, object? cast = null, Vector3? position = null)
            => Actor(number, 0x233C, position ?? center, cast: cast, subKind: 5);
        string Icon(double t, uint icon, ulong source = 100, ulong target = 101) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, MarkerId = icon, TargetId = target, Timestamp = Start.AddSeconds(t) },
        });
        string Effect(double t, uint action, ulong target = 100) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, GlobalSequence = (uint)(t * 10000),
                MainTargetId = target, Targets = Array.Empty<ulong>(), Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double t)
        {
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRS3 actual replay callback: " + controller.StatusMessage + " / " + manager.ErrorMessage);
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!
            .GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [937u, 0x30A8u])! && !(bool)owns.Invoke(null, [936u, 0x30A8u])!
            && !(bool)owns.Invoke(null, [937u, 0x3131u])!, "DRS3 precise ownership preserves unconverted bosses");
        var events = new List<string> { Header(937), World(0, [Boss(), Helper(),
            Actor(100, 0, center + Vector3.UnitZ * 3), Actor(101, 0, center + Vector3.UnitX * 15),
            Actor(30, 0x30A9, center + Vector3.UnitZ * 7, isEnemy: true)], true) };
        var t = 0d;
        double AddFrame(params object[] actors)
        {
            t += .1; events.Add(World(t, actors)); return t + .01;
        }
        void Pair(uint action)
        {
            AddFrame(Boss(Spell(action, t + .1)));
            AddFrame(Boss());
        }
        double AddIcon(uint icon, ulong source = 100, ulong target = 101)
        {
            t += .1; events.Add(Icon(t, icon, source, target)); return t + .01;
        }
        void AddEffect(uint action, ulong target = 100)
        {
            t += .1; events.Add(Effect(t, action, target));
        }
        double Rotate()
        {
            AddIcon(167);
            Pair(22379);
            var checkpoint = t + .01;
            for (var i = 0; i < 5; ++i) Pair(22380);
            return checkpoint;
        }
        var rock = AddFrame(Helper(20, Spell(22382, .1)));
        // Non-Spell raw Cast advances Start but does not create a charge.
        var raw = AddFrame(Boss(Spell(999, t + .1, spell: false)));
        var charge = AddFrame(Boss(Spell(22387, t + .1, center + Vector3.UnitX * 15)));
        AddFrame(Boss());
        Pair(22387); Pair(22388);
        AddFrame(Helper(21, Spell(22376, t + .1, center + Vector3.UnitZ * 15), center - Vector3.UnitZ * 15));
        var spit = AddIcon(79, target: 101);
        AddIcon(80, target: 101);
        Pair(22390);
        AddEffect(22391);
        AddFrame();
        var spitDone = t + .01;
        Pair(22383); Pair(22385);
        AddFrame(Helper(21));
        AddFrame(Helper(22, Spell(22377, t + .1, center + Vector3.UnitZ * 25), center + Vector3.UnitZ * 25));
        AddFrame(Boss(Spell(22375, t + .1)));
        var howl = AddFrame(Boss());
        AddEffect(23349); AddFrame();
        AddEffect(22377); AddFrame();
        var rotation = Rotate();
        AddFrame(Actor(31, 0x30AA, center, isEnemy: true));
        AddFrame(Helper(21, Spell(22376, t + .1, center + Vector3.UnitZ * 15), center - Vector3.UnitZ * 15));
        Pair(22383); Pair(22385);
        AddIcon(82, 101, 100); Pair(22390); AddEffect(22391, 101); AddFrame();
        var burn = AddIcon(87, 100, 101);
        AddFrame(Helper(22, Spell(22377, t + .1, center + Vector3.UnitZ * 25), center + Vector3.UnitZ * 25));
        AddFrame(Boss(Spell(22392, t + .1)));
        var assault = AddFrame(Boss());
        AddEffect(23363); AddFrame();
        AddEffect(21603, 999); AddFrame();
        var burnDone = t + .01;
        AddEffect(22377); AddFrame();
        Rotate();
        var tail = AddFrame();
        t += 1000; var permanent = AddFrame();
        var death = AddFrame(Boss(dead: true));
        load(id, events);
        Seek(rock);
        Check(State() == 0x10 && dangers() == 1 && Shown("bozja/aoe/").Length == 1
            && Components().SequenceEqual(["FallingRock"]), "DRS3 actual initial FallingRock cold entry");
        Seek(raw);
        Check(State() == 0x11 && Components().SequenceEqual(["FallingRock", "HotCharge"])
            && Items("Casts").Length == 1, "DRS3 actual raw Ability enters End without inventing Spell output");
        Seek(charge);
        Check(Items("Casts").Length == 2 && Shown("bozja/aoe/").Length == 2
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 22382 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 22260 }),
            "DRS3 actual charge publication and precise action gate");
        dynamic arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)arena.Contains(center + Vector3.UnitZ * 29.1f, 0f) && (bool)arena.Contains(center + Vector3.UnitX * 25, 0f),
            "DRS3 actual source notched arena");
        Console.WriteLine("PASS native DRS3 Dahu actual source ownership raw Ability cast states rock charge cold activation action gate real arena");
        Seek(spit);
        Check(Shown("bozja/spread/").Length == 1 && Items("Casts").Any(c => Read(c, "Target") is { } target
            && (ulong)Read(Read(target, "Actor"), "Id") == 100), "DRS3 Spit uses callback source not TargetID");
        Seek(spitDone);
        Check(Shown("bozja/spread/").Length == 0 && !Components().Contains("SpitFlame") && State() == 0x10200,
            "DRS3 actual Effect removes duplicate source spreads and retires component");
        Console.WriteLine("PASS native DRS3 Dahu actual pre-cast icons duplicate source order target distinction Effect all consumption");
        Seek(howl);
        Check(State() == 0x10410 && Lines().Length == 1
            && !((string?)Read(Script(), "MechanicHintsText") ?? "").Contains("About to be knocked into danger!"),
            "DRS3 actual Feral ordinary Finish retains wall line without normal unsafe hint");
        var end = (Vector3)Read(Lines().Single(), "End");
        Check((bool)arena.Contains(end, 0f) && end.Z > center.Z + 28, "DRS3 actual polygon wall hitbox line");
        Seek(rotation);
        Check(Shown("bozja/aoe/").Length == 2 && (int)Read(Model(), "NumDahuRotations") == 0
            && ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "GoalAreas")).Cast<object>().Any(),
            "DRS3 actual rotating foreground future and R5 goal");
        Console.WriteLine("PASS native DRS3 Dahu actual Feral wall-stop line Finish retained claw navigation rotating cache five Finish R5 goal");
        Seek(burn);
        Check(Items("DahuBurns").Length == 1 && Shown("dahu/burn/").Length == 1
            && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1,
            "DRS3 actual Burn icon target R30 background raw diagnostic");
        // Solve the real registry's Burn circle from its center; no fabricated navigation path.
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        object snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        var start = center + Vector3.UnitX * 15;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [start, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic solution = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        var targetPosition = (Vector3)Read((object)solution, "TargetPosition");
        Check((bool)solution.IsSuccess && !(bool)Read((object)solution, "IsLeastDangerFallback")
            && Vector3.Distance(targetPosition, start) > 30 && (bool)arena.Contains(targetPosition, 0f),
            "DRS3 real solver escapes target-centered Burn within true arena");
        Seek(assault);
        Check(State() == 0x40310 && Lines().Length == 1
            && Components().SequenceEqual(["Burn", "HuntersClaw", "HystericAssault"]), "DRS3 actual second knockback independent activation order");
        Seek(burnDone);
        Check(Items("DahuBurns").Length == 0 && Shown("dahu/burn/").Length == 0
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "DRS3 actual unrelated Effect FIFO");
        Console.WriteLine("PASS native DRS3 Dahu actual Burn target R30 raw damage diagnostic real solver Hysteric independent delay FIFO Effect");
        Seek(tail); Check(State() == 0xFF0000 && Components().Length == 0, "DRS3 actual completed known chain");
        Seek(permanent); Check(State() == 0xFF0000 && (bool)Read(Script(), "EncounterActive"), "DRS3 permanent unknown tail");
        Seek(death); Check(!(bool)Read(Script(), "EncounterActive") && dangers() == 0 && previews().Length == 0, "DRS3 death release");
        Seek(howl);
        manager.SetMechanismEnabled(id, "大兀", false); Seek(howl + .001);
        Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "DRS3 mechanism owner release");
        manager.SetMechanismEnabled(id, "大兀", true); Seek(burn);
        Check(Script().GetType().GetMethod("GetTimeline")!.Invoke(Script(), null) != null
            && !(bool)Read(Read(Script(), "Move"), "_hasControl") && Read(Script(), "PlannedFacing") == null
            && (ulong)Read(Script(), "PlannedTargetId") == 0
            && !(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"), "DRS3 reverse Seek timeline replay execution isolation");
        stop();
        Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0, "DRS3 stop owner release");
        Console.WriteLine("PASS native DRS3 Dahu actual full chain permanent unknown death mechanism owner reverse Seek native timeline replay execution isolation");
    }
}
