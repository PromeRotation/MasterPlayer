using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBarPassageP2(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(!string.IsNullOrWhiteSpace(sqpack) && Directory.Exists(sqpack), "Set FFXIV_SQPACK for the production CFC metadata adapter.");
        using var game = new Lumina.GameData(sqpack!, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
        });
        var pluginAssembly = managerObject.GetType().Assembly;
        var sampler = pluginAssembly.GetType("MasterPlayer.MechanicHost.LiveWorldSampler")!;
        var territoryField = sampler.GetField("_cachedTerritory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var cfcField = sampler.GetField("_cachedCfc", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldTerritory = territoryField.GetValue(null);
        var oldCfc = cfcField.GetValue(null);
        var oldData = Svc.Data;
        typeof(Svc).GetProperty(nameof(Svc.Data))!.SetValue(null, new PalaceGameData(game));
        territoryField.SetValue(null, ushort.MaxValue);
        try
        {
            BarPassageP2Body(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
            BarPassageP1Body(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
            BarApyaahiBody(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        }
        finally
        {
            typeof(Svc).GetProperty(nameof(Svc.Data))!.SetValue(null, oldData);
            territoryField.SetValue(null, oldTerritory);
            cfcField.SetValue(null, oldCfc);
        }
    }

    private static void BarPassageP2Body(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.bar-the-passage-p2";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        int Hits() => (int)Value(Encounter(), "BarP2Hits");
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        var center = new Vector3(0, 0, 28.5f);
        var shieldPose = center + new Vector3(.12345f, 0, .54321f);
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(1246, t, actors, full: full, removed: removed, party: party);
        string Map(double t, uint position, ushort p1, ushort p2) => JsonSerializer.Serialize(new
        {
            t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = position, Param1 = p1, Param2 = p2, Timestamp = Start.AddSeconds(t) },
        });
        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Bar P2 replay leaked execution controls.");
        }
        load(guid, [Header(1246), At(0,
            [Actor(1, 0x4660, center, isEnemy: false),
             Actor(10, 0x4669, center + Vector3.UnitX * 10),
             Actor(20, 0x4667, center + Vector3.UnitZ * 10),
             Actor(100, 0, center - Vector3.UnitZ * 10), Actor(101, 0, center - Vector3.UnitZ * 12)], true),
            Map(.2, 3, 1, 2),
            At(1, [Actor(1, 0x4660, center, combat: true, isEnemy: false),
                Actor(10, 0x4669, center + Vector3.UnitX * 10, cast: Cast(40904, 20, center + Vector3.UnitX * 10, rotation: .32123f, startedAt: 1)),
                Actor(20, 0x4667, center + Vector3.UnitZ * 10, cast: Cast(40905, 20, center + Vector3.UnitZ * 10, startedAt: 1))]),
            At(2, [Actor(1, 0x4660, shieldPose, combat: true, rotation: .45678f, isEnemy: false)]),
            TreasureNativeEffect(2.1, 1, 40866, 1, 1, []),
            EffectEvent(2.1, 1, 40866, 1, target: 1),
            TreasureNativeEffect(2.2, 20, 40907, 2, 100, [100, 101]),
            EffectEvent(2.2, 20, 40907, 2, target: 100),
            EffectEvent(2.2, 20, 40907, 2, target: 101),
            TreasureNativeEffect(2.3, 20, 40908, 3, 0, []),
            TreasureNativeEffect(2.4, 20, 40908, 4, 100, [100]),
            TreasureNativeEffect(2.5, 20, 40908, 5, 100, [100]),
            Map(3, 3, 1, 2), Director(3.1, 0x40000011),
            At(3.2, [Actor(1, 0x4660, center, dead: true, isEnemy: false)]),
            At(4, [], removed: [1]),
            TreasureNativeEffect(4.1, 20, 40908, 6, 100, [100]),
            At(5, [Actor(1, 0x4660, shieldPose, rotation: .45678f, isEnemy: false),
                Actor(10, 0x4669, center + Vector3.UnitX * 10),
                Actor(20, 0x4667, center + Vector3.UnitZ * 10)]),
            TreasureNativeEffect(5.1, 1, 40866, 7, 0, []),
            TreasureNativeEffect(5.2, 20, 40908, 8, 0, []),
            At(6, [Actor(1, 0x4660, center, targetable: false, isEnemy: false)]), At(7, [])]);
        controller.Seek(.5);
        Check(Value(Encounter(), "Active") == null && Goals() == 0 && dangers() == 0, "Bar P2 activated before friendly primary pull.");
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") != null && (uint)Value(Encounter(), "CurrentCfcId") == 1016
            && dangers() == 2 && Goals() == 0 && !(bool)Value(Encounter(), "BarP2SecondArena"),
            "Bar P2 CFC metadata/pull/preactivation map replay/cast output failed.");
        Check(Shown("job-quest/enemy/").Length == 2
            && Shown("job-quest/enemy/").All(a => !((string)Value(a, "Id")).EndsWith("/1")),
            "Bar P2 trash drawing changed.");
        controller.Seek(2.6);
        Check(Hits() == 4 && Goals() == 1 && Shown("bar-p2/shield").Length == 1 && dangers() == 2,
            "Bar P2 packet dedup, legacy expansion, zero-target or shield safe-goal failed.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 40907 })
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 40908 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 60000 }),
            "Bar P2 Dirty Slash was left to generic rectangular dodge or unrelated actions suppressed.");
        var aoes = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>();
        var shield = aoes.Single(a => (string)Value(a, "Id") == "bar-p2/shield");
        Check((Vector3)Value(shield, "Position") == shieldPose && (float)Value(shield, "Rotation") == .45678f + MathF.PI
            && (DateTime)Value(shield, "Activation") == default && (bool)Value(shield, "Safe"),
            "Bar P2 raw shield pose/default time/safe colour changed.");
        controller.Seek(3.5);
        dynamic arena = Value(Encounter(), "Arena");
        Check((bool)Value(Encounter(), "BarP2SecondArena") && (Vector3)arena.Position == new Vector3(0, 0, 57)
            && Goals() == 1 && Hits() == 4 && Value(Encounter(), "Active") != null,
            "Bar P2 map/director/death changed source phase.");
        controller.Seek(4.5);
        Check(Value(Encounter(), "Active") != null && Hits() == 5 && Goals() == 0, "Bar P2 destruction ended phase or fifth hit failed.");
        controller.Seek(5.5);
        Check(Value(Encounter(), "Active") != null && Hits() == 6 && Goals() == 1 && dangers() == 0,
            "Bar P2 new shield reset count or sixth hit cleared it/real Finish failed.");
        controller.Seek(6.5);
        Check(Value(Encounter(), "Active") == null && Goals() == 0 && previews().Length == 0, "Bar P2 targetable end leaked owner outputs.");
        controller.Seek(2.6);
        Isolated();
        manager.SetMechanismEnabled(guid, "Bar the Passage P2", false);
        controller.Seek(2.7);
        Check(Goals() == 0 && dangers() == 0 && previews().Length == 0, "Bar P2 disabled owner leaked output.");
        manager.SetMechanismEnabled(guid, "Bar the Passage P2", true);
        stop();
        Console.WriteLine("PASS native Bar the Passage P2 production friendly primary CFC mapping effect-only frozen shield native zero targets and dedup fifth exact hit safe goal three casts true Finish map director independent completion reverse Seek and owner cleanup");

        // Both source rectangles use exact scalar half-extents and replacement centers.
        load(guid, [Header(1246), At(0,
            [Actor(1, 0x4660, center, combat: true, isEnemy: false), Actor(100, 0, center)], true),
            Map(1, 3, 1, 2), At(2, [])]);
        var samples = 0;
        foreach (var phase in new[] { 0, 1 })
        {
            controller.Seek(phase + .5);
            dynamic current = Value(Encounter(), "Arena");
            var zCenter = phase == 0 ? 28.5f : 57f;
            var halfZ = phase == 0 ? 23.5f : 24f;
            for (var x = -37.123f; x <= 37; x += .73f)
            for (var z = 2.171f; z <= 84; z += .71f)
            {
                var expected = MathF.Abs(x) <= 34.6f && MathF.Abs(z - zCenter) <= halfZ;
                Check((bool)current.Contains(new Vector3(x, 0, z), 0f) == expected, $"Bar P2 rectangle phase{phase} at {x},{z}.");
                ++samples;
            }
        }
        stop();
        Console.WriteLine($"PASS native Bar the Passage P2 production both exact rectangle bounds {samples} points replacement without union");

        load(guid, [Header(1246), At(0,
            [Actor(1, 0x4660, center, combat: true, isEnemy: false), Actor(20, 0x4667, center + Vector3.UnitX * 10),
             Actor(100, 0, center - Vector3.UnitZ * 10)], true),
            CastEvent(.1, 20, 40904, 10, center, 0, 1),
            At(.11, [Actor(20, 0x4667, center + Vector3.UnitX * 10,
                cast: Cast(40904, 10, shieldPose, rotation: .71234f, target: 1, startedAt: .1))]),
            TreasureNativeEffect(.2, 1, 40866, 0, 0, []),
            TreasureNativeEffect(.3, 20, 40907, 0, 0, []),
            TreasureNativeEffect(.4, 20, 40908, 0, 0, []),
            TreasureNativeEffect(.5, 20, 40908, 0, 0, []),
            TreasureNativeEffect(.5, 20, 40908, 0, 0, []),
            TreasureNativeEffect(.6, 20, 40908, 0, 0, []),
            TreasureNativeEffect(.7, 20, 40908, 0, 0, []),
            At(1, [Actor(1, 0x4660, shieldPose, combat: true, rotation: .45678f, isEnemy: false),
                Actor(20, 0x4667, center + Vector3.UnitX * 10, cast: Cast(40904, 10, shieldPose, rotation: .71234f, target: 1, startedAt: .1))]),
            TreasureNativeEffect(1.1, 1, 40866, 0, 0, []), Map(1.2, 3, 1, 2),
            Director(2, 0x40000005), At(2.1, []), Director(3, 0x40000011), At(3.1, []),
            At(3.2, [], removed: [1]), At(3.3, [Actor(1, 0x4660, center, combat: true, isEnemy: false)]), At(4, [])]);
        controller.Seek(.8);
        Check(Hits() == 5 && Goals() == 0, "Production distinct zero-sequence effects merged or duplicate counted.");
        controller.Seek(1.5);
        var pending = ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Select(kv => Value(kv, "Value")).Single();
        Check(Goals() == 1 && Hits() == 5 && (Vector3)Value(pending, "Location") == shieldPose
            && (float)Value(pending, "Rotation") == .71234f,
            "Production zero-sequence shield relock or first raw packet correction failed.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && Goals() == 0, "Production manager wipe repulled old actor.");
        controller.Seek(3.15);
        Check(Value(Encounter(), "Active") == null, "Production recommence recreated unchanged unloaded primary.");
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") != null && Hits() == 0 && Goals() == 0
            && !(bool)Value(Encounter(), "BarP2SecondArena"), "Production recommence failed fresh component state.");
        controller.Seek(.8);
        Check(Hits() == 5 && Goals() == 0, "Reverse Seek retained old packet receipts.");
        stop();
        Console.WriteLine("PASS native Bar the Passage P2 production zero-sequence packet identity duplicate fifth hit shield relock first raw correction manager wipe recommence and reverse Seek");

        var p1Center = new Vector3(0, 0, -336.3f);
        load(guid, [Header(1246), At(0, [Actor(100, 0, p1Center)], true),
            At(1, [Actor(20, 0x4660, center, combat: true, isEnemy: false),
                Actor(10, 0x465D, p1Center, combat: true, isEnemy: false)]),
            At(2, [Actor(20, 0x4660, center, targetable: false, isEnemy: false)]), At(3, [])]);
        controller.Seek(1.5);
        Check((bool)Value(Encounter(), "IsBarPassageP2"), "Production nearest P1 displaced first directly loaded P2.");
        controller.Seek(2.5);
        Check((bool)Value(Encounter(), "IsBarPassageP1"), "Production completed P2 failed ready P1 handoff.");
        stop();
        load(guid, [Header(1246), At(0,
            [Actor(20, 0x4660, center, combat: true, isEnemy: false),
             Actor(10, 0x465D, p1Center, combat: true, isEnemy: false), Actor(100, 0, center)], true), At(1, [])]);
        controller.Seek(.5);
        Check((bool)Value(Encounter(), "IsBarPassageP1"), "Production cold first-frame pending promotion lost reverse order.");
        stop();
        load(guid, [Header(1246), At(0, [Actor(100, 0, p1Center)], true),
            At(1, [Actor(10, 0x465D, p1Center, isEnemy: false)]),
            At(2, [Actor(11, 0x465D, p1Center, combat: true, isEnemy: false)]),
            At(3, [], removed: [10]), At(4, [])]);
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") == null && previews().Length == 0,
            "Production same OID pending primary bypassed loaded sibling.");
        controller.Seek(3.5);
        Check((bool)Value(Encounter(), "IsBarPassageP1") && (ulong)Value(Encounter(), "PrimaryId") == 11,
            "Production pending sibling failed later promotion.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") == null, "Production reverse Seek retained old loaded occupancy.");
        stop();
        Console.WriteLine("PASS native Bar the Passage production direct loaded order cold pending reverse same OID occupancy completion handoff reverse Seek and cleanup");

        load(guid, [Header(1246), At(0, [Actor(100, 0, p1Center)], true),
            At(1, [Actor(10, 0x465D, p1Center, combat: true, isEnemy: false),
                Actor(20, 0x4660, center, combat: true, isEnemy: false)]),
            TreasureNativeEffect(2, 20, 40866, 1, 0, []),
            TreasureNativeEffect(2.1, 20, 40907, 2, 0, []), Map(2.2, 3, 1, 2),
            At(3, [Actor(10, 0x465D, p1Center, targetable: false, isEnemy: false),
                Actor(20, 0x4660, center, isEnemy: false)]), At(4, [])]);
        controller.Seek(2.5);
        Check((bool)Value(Encounter(), "IsBarPassageP1") && Goals() == 0 && Hits() == 1
            && (bool)Value(Encounter(), "BarP2SecondArena"), "Production background P2 lost state or leaked display.");
        controller.Seek(3.5);
        Check((bool)Value(Encounter(), "IsBarPassageP2") && Goals() == 1 && Hits() == 1
            && (Vector3)Value(Value(Encounter(), "Arena"), "Position") == new Vector3(0, 0, 57),
            "Production handoff reset started background shield/count/arena or required current combat.");
        controller.Seek(2.5);
        Check((bool)Value(Encounter(), "IsBarPassageP1") && Goals() == 0 && Hits() == 1,
            "Production reverse Seek retained selected P2 state.");
        stop();
        Console.WriteLine("PASS native Bar the Passage production background stage independent shield count map no combat handoff filtered outputs reverse Seek and cleanup");


    }

    private static void BarPassageP1Body(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.bar-the-passage-p2";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var plugin = managerObject.GetType().Assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] Lines() => ((IEnumerable)Value(plugin, "AoeVisibility").GetType().GetMethod("GetLines")!
            .Invoke(Value(plugin, "AoeVisibility"), null)!).Cast<object>().ToArray();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        var center = new Vector3(0, 0, -336.3f);
        var primary = center + new Vector3(.12345f, 0, .54321f);
        var middle = new Vector3(13.12345f, 0, -302.54321f);
        var target = center + Vector3.UnitX * 12;
        object[] Tether(ulong id) => [new { Id = 84, TargetId = id }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(1246, t, actors, full: full, removed: removed, party: party);
        load(guid, [Header(1246), At(0,
            [Actor(1, 0x465D, primary, subKind: 5, isEnemy: false),
             Actor(10, 0x4664, middle, tethers: Tether(101)),
             Actor(20, 0x4661, center - Vector3.UnitX * 8),
             Actor(100, 0, center), Actor(101, 0, target)], true),
            At(1, [Actor(1, 0x465D, primary, combat: true, subKind: 5, isEnemy: false),
                Actor(10, 0x4664, middle, tethers: Tether(101), cast: Cast(41125, 10, middle, startedAt: 1)),
                Actor(20, 0x4661, center - Vector3.UnitX * 8, cast: Cast(40896, 10, center - Vector3.UnitX * 8, startedAt: 1))]),
            TreasureNativeEffect(2, 10, 40902, 1, 1, [1]),
            At(3, [Actor(1, 0x465D, primary, dead: true, subKind: 5, isEnemy: false),
                Actor(10, 0x4664, middle, tethers: Tether(101))]),
            At(4, [], removed: [101]), At(5, [Actor(101, 0, target + Vector3.UnitX * 5)]),
            At(6, [Actor(20, 0x4661, center - Vector3.UnitX * 8)], removed: [10]),
            At(7, [Actor(1, 0x465D, primary, targetable: false, subKind: 5, isEnemy: false)]), At(8, [])]);
        controller.Seek(.5);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && Goals() == 0 && Lines().Length == 0,
            "P1 precombat created component output.");
        controller.Seek(1.5);
        Check((bool)Value(Encounter(), "IsBarPassageP1") && !(bool)Value(Encounter(), "IsBarPassageP2")
            && Goals() == 1 && dangers() == 1 && Lines().Length == 1 && (bool)Value(Lines().Single(), "Safe")
            && Shown("cast/").Length == 1 && Shown("alpha/intercept").Length == 0,
            "P1 interception goal, visual-only cast or P2 isolation failed.");
        Check(Math.Abs(((DateTime)Value(Encounter(), "AlphaActivation") - Start.AddSeconds(12.4)).TotalSeconds) < .00001,
            "P1 F+1.1 shared clock incorrect.");
        var aoes = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>();
        var goal = aoes.Single(a => (string)Value(a, "Id") == "alpha/intercept");
        var expectedFrom = primary + Vector3.Normalize(middle - primary) * .6f;
        Check((Vector3)Value(goal, "Position") == expectedFrom && (bool)Value(goal, "Safe")
            && (int)Value(goal, "GoalPriority") == 1, "P1 navigation used tether target instead of fixed first465D.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 10, ActionId = 41958 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 40907 }),
            "P1 ignored visual or swallowed P2 action.");
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") != null && Goals() == 1 && Lines().Length == 1, "P1 primary death or Toxic Spit Finish ended intercept.");
        controller.Seek(5.5);
        Check((Vector3)Value(Lines().Single(), "End") == target && !(bool)Value(Lines().Single(), "Safe"),
            "P1 destroyed tether actor rebound to new same ID.");
        controller.Seek(6.5);
        Check(Goals() == 0 && Lines().Length == 1 && dangers() == 0, "P1 source destruction lost old reference line or retained corridor.");
        controller.Seek(7.5);
        Check(Value(Encounter(), "Active") == null && Goals() == 0 && Lines().Length == 0, "P1 targetable completion leaked state.");
        controller.Seek(1.5);
        manager.SetMechanismEnabled(guid, "Bar the Passage P1", false);
        controller.Seek(1.6);
        Check(Goals() == 0 && dangers() == 0 && Lines().Length == 0 && previews().Length == 0, "P1 mechanism owner did not clear.");
        manager.SetMechanismEnabled(guid, "Bar the Passage P1", true);
        stop();
        Console.WriteLine("PASS native Bar the Passage P1 production fixed friendly primary intercept original actor references raw positions and F+1.1 clock persistent safe goal real Finish destroyed target same ID line freeze P2 isolation reverse Seek and owner cleanup");
    }
}
