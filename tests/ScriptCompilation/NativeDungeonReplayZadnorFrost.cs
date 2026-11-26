using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorFrost(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "邪恶冰狼——恶名苍狼";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-830, 0, 190);
        object[] roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
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
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at = 0, Vector3? origin = null, float rotation = 0, float total = 5)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, origin ?? center, rotation, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = total;
            return node;
        }
        object Primary(object? cast = null) => Actor(1, 0x319C, center, combat: true, isEnemy: true, cast: cast);
        object Helper(object? cast = null, ulong actor = 20, Vector3? pos = null)
            => Actor(actor, 0x233C, pos ?? center, cast: cast, subKind: 5);
        object Imaginifer(ulong actor = 40, object? cast = null) => Actor(actor, 0x319F, center, cast: cast, subKind: 5);
        object[] Initial(object? cast = null) =>
            [Primary(), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10), Helper(cast)];
        string Effect(double t, uint action) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 1UL, ActionId = action, ActionType = 1, GlobalSequence = 900u,
                MainTargetId = 100UL, Targets = Array.Empty<ulong>(), Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double time, bool expectedError = false)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id),
                "CE54 actual replay callback remains running: " + controller.StatusMessage);
            if (!expectedError) Check((string)manager.ErrorMessage == "", "CE54 unexpected script diagnostic: " + manager.ErrorMessage);
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!
            .GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x319Cu])! && !(bool)owns.Invoke(null, [920u, 0x319Cu])!
            && !(bool)owns.Invoke(null, [975u, 0x2E75u])!, "CE54 exact ownership keeps unconverted source");
        foreach (var action in new uint[] { 23582, 23583, 23584, 23594, 23596, 23601, 23602 })
        {
            load(id, [Header(975), World(0, Initial(Spell(action)), true), World(10, []),
                World(11, [Helper()]), World(12, [])]);
            Seek(10.1);
            Check((bool)Read(Model(), "Active"), "CE54 square pull");
            Check(dangers() == (action == 23602 ? 0 : 1), "CE54 exact watched ordinary output");
            if (action == 23602)
                Check((string)Read(Script(), "MechanicHintsText") == "Raidwide"
                    && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1,
                    "CE54 visual Glaciation true diagnostic");
            else Check(Shown("bozja/aoe/").Length == 1, "CE54 real visual geometry");
            var arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check((bool)arena.Contains(center + new Vector3(24, 0, 24), 0f)
                && !(bool)arena.Contains(center + new Vector3(24.01f, 0, 0), 0f), "CE54 exact square boundary");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "CE54 sampled caster source gate");
            if (action == 23596)
            {
                Seek(.1);
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                var target = (Vector3)Read((object)result, "TargetPosition");
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance(center, target) > 10 && (bool)arena.Contains(target, 0f),
                    "CE54 actual solver leaves R10 inside square no fallback");
            }
            Seek(11.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
                && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE54 watched Finish clears");
            stop();
            Console.WriteLine($"PASS native CE54 {action} true watched geometry or diagnostic square source gate no TTL Finish owner");
        }
        foreach (var first in new[] { false, true })
        {
            var pillar = Actor(30, 0x319E, center + Vector3.UnitZ * 5, hitboxRadius: 3, dead: true);
            var boss = Primary(Spell(23588, total: 14));
            object[] order = first ? [pillar, boss] : [boss, pillar];
            load(id, [Header(975), World(0, [.. order, Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10),
                    Helper()], true),
                World(1, [Helper(Spell(23586, 1, center - Vector3.UnitZ * 20), pos: center - Vector3.UnitZ * 20)]),
                World(2, [Helper()]), World(3, [])]);
            Seek(.1);
            Check(Shown("bozja/aoe/").Length == (first ? 1 : 0), "CE54 cold private pillar Created then Start order");
            Seek(1.1);
            Check(Shown("bozja/aoe/").Length == (first ? 1 : 0) && Lines().Length == 1 && dangers() == 1,
                "CE54 Bracing replaces only Lunar AI retains safe display");
            var line = Lines().Single();
            Check(Vector3.Distance((Vector3)Read(line, "Start"), (Vector3)Read(line, "End")) == 40,
                "CE54 actual projected forty-unit wind");
            Seek(2.1); Check(Lines().Length == 0, "CE54 watched Finish clears wind");
            stop();
            Console.WriteLine($"PASS native CE54 Lunar cold pillar order={first} dead original blocker Bracing AI priority forty-unit line Finish");
        }
        var rotationActors = Initial();
        rotationActors[0] = Primary(Spell(23590));
        var helpers = Enumerable.Range(0, 6).Select(i => Helper(Spell(23592, rotation: i * MathF.PI / 3), (ulong)(30 + i))).ToArray();
        load(id, [Header(975), World(0, [.. rotationActors, .. helpers], true), Effect(.5, 23593),
            World(10, []), World(11, [Helper(actor: 35)]), World(12, [])]);
        Seek(.1);
        var published = Items("frostPublished");
        Check(published.Length == 12 && Shown("bozja/aoe/").Length == 12 && dangers() == 12
            && published.Select(p => (bool)Read(p, "Danger")).SequenceEqual(Enumerable.Range(0, 12).Select(i => i % 2 == 1)),
            "CE54 six sequences future then imminent per sequence true risk order");
        var clocks = published.Select(p => (DateTime)Read(Read(p, "Cast"), "Activation")).ToArray();
        Seek(10.1);
        Check((int)Read(Model(), "NumFrostCasts") == 0 && Items("frostPublished")
            .Select(p => (DateTime)Read(Read(p, "Cast"), "Activation")).SequenceEqual(clocks),
            "CE54 actual Effect does not advance and clock alone does not rebuild rotating cache");
        Seek(11.1); Check((int)Read(Model(), "NumFrostCasts") == 1 && Shown("bozja/aoe/").Length == 12,
            "CE54 ordinary snapshot Finish advances geometric first match");
        manager.SetMechanismEnabled(id, mechanism, false); Seek(11.1);
        Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "CE54 mechanism owner release");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(.1); stop();
        Console.WriteLine("PASS native CE54 six rotating actual twelve previews and hazards per-sequence order unchanged cache Effect ignored Finish mechanism owner reverse Seek");

        load(id, [Header(975), World(0, [.. Initial(Spell(23596)), Imaginifer()], true),
            World(1, [Imaginifer(cast: Spell(23589, 1, total: 2))]),
            World(2, [Imaginifer()]), World(3, [Imaginifer(cast: Spell(23589, 3, total: 2))]),
            World(4, [Imaginifer(), Imaginifer(41)]), World(5, []), World(6, [], removed: [40]),
            World(7, [Primary(), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10),
                Helper(Spell(23596)), Imaginifer(42), Imaginifer(), Imaginifer(41)], true), World(8, [])]);
        Seek(2.1); Check(Items("ThermalGusts").Length == 0 && dangers() == 1, "CE54 first snapshot Finish consumes Created prediction");
        foreach (var t in new[] { 4.1d, 5.1d, 6.1d })
        {
            Seek(t, true);
            var actors = (IDictionary)Read(Model(), "Actors");
            Check(Read(Model(), "ThermalSequenceError") != null && actors.Contains(40UL) && !actors.Contains(41UL)
                && Read(actors[40UL]!, "Cast") != null && Items("ThermalGusts").Length == 0
                && dangers() == 0 && previews().Length == 0 && Lines().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null
                && ((IList)Read(Script(), "pendingEvents")).Count == 0,
                "CE54 failed snapshot keeps cast, blocks later Created, retries after source removal, no invented packet retry");
        }
        Seek(7.1, true);
        Check(Read(Model(), "ThermalSequenceError") == null && Items("ThermalGusts").Length == 1 && dangers() == 2,
            "CE54 real earlier Created permits Finish then later Created commits");
        Check(((string)manager.ErrorMessage).Contains("ThermalGust 快照结束空队列"), "CE54 expected snapshot FIFO diagnostic");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0 && !(bool)Read(Read(Script(), "Hints"), "holdActions"),
            "CE54 replay never requests facing target or action execution");
        stop(); manager.SetError("");
        Console.WriteLine("PASS native CE54 actual snapshot FIFO partial commit retained old cast retries Destroy blocks later Created earlier Created recovery owner replay isolation");
    }
}
