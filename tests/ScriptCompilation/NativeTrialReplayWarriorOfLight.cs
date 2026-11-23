using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunWarriorOfLight(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("wol/" + prefix, StringComparison.Ordinal)).ToArray();
        bool Mode(string name) => (bool)Value(Encounter(), name);
        string Icon(double t, ulong actor, uint icon, ulong target = 0) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = target == 0 ? actor : target, MarkerId = icon, Timestamp = Start.AddSeconds(t) }
        });
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) }
        });
        string Status(double t, bool present) => JsonSerializer.Serialize(new
        {
            t, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = 100UL, StatusId = 960u, Duration = 3f, Timestamp = Start.AddSeconds(t) }
        });
        const string id = "masterplayer.trial.warrioroflight";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var players = Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0,
            c + new Vector3(i - 3, 0, 0))).ToArray();
        load(id,
        [
            Header(922), Frame(922, 0, [Actor(1, 0x2DDD, c, combat: true), .. players,
                Actor(10, 0x233C, c), Actor(11, 0x233C, c), Actor(12, 0x233C, c),
                Actor(20, 0x2DEB, c - Vector3.UnitX * 15), Actor(30, 0x1EAFFE, c)], full: true, party: party),
            Frame(922, .1, [Actor(10, 0x233C, c, cast: Cast(20254, 7, c)),
                Actor(11, 0x233C, c, cast: Cast(20264, 7, c, target: 101)),
                Actor(12, 0x233C, c, cast: Cast(20251, 7, c, target: 100))], party: party),
            Map(.2, 0, 0x00020001), Map(.21, 2, 0x00020001), Map(.3, 0x14, 0x00020001),
            Icon(.4, 101, 161), Icon(.41, 100, 87), Icon(.42, 101, 234), Icon(.43, 102, 234),
            EffectEvent(.5, 10, 20294, 1, target: 101), EffectEvent(.51, 11, 20294, 2, target: 102),
            Frame(922, .6, [Actor(20, 0x2DEB, c - Vector3.UnitX * 15, tethers: [new { Id = 17, TargetId = 100UL }])], party: party),
            Icon(1, 100, 225), Status(1.2, true), Status(2, false),
            Icon(2.1, 100, 225),
            Frame(922, 3, [Actor(11, 0x233C, c)], party: party),
            EffectEvent(3.1, 10, 20262, 3), EffectEvent(3.2, 10, 20237, 4, target: 101),
            EffectEvent(3.3, 10, 20256, 5), EffectEvent(3.4, 10, 20246, 6),
            EffectEvent(3.5, 10, 20247, 7), EffectEvent(3.6, 11, 20264, 8),
            Map(4, 0, 0x00080004),
            Frame(922, 5, [Actor(1, 0x2DDD, c, dead: true)], party: party),
            Frame(922, 5.1, [Actor(2, 0x2DDE, c, combat: true), Actor(10, 0x233C, c),
                Actor(12, 0x233C, c)], removed: [1, 30], party: party),
            Frame(922, 5.2, [Actor(10, 0x233C, c, cast: Cast(20252, 6, c, startedAt: 5.2))], party: party),
            Icon(5.3, 100, 225),
            Frame(922, 9.5, [Actor(100, 0, c + Vector3.UnitX * 2)], party: party),
            Frame(922, 10, [Actor(100, 0, c + Vector3.UnitX * 2, dead: true)], party: party),
            Frame(922, 10.2, [Actor(100, 0, c + Vector3.UnitX * 2)], party: party),
            Frame(922, 12, [Actor(10, 0x233C, c)], party: party),
            Frame(922, 13, [Actor(2, 0x2DDE, c, dead: true)], party: party),
            Frame(922, 14, [], party: party)
        ]);
        controller.Seek(.8);
        Check(Group("brimstone").Length == 4 && Group("sword").Length == 1 && Group("tower").Count(a => (bool)Value(a, "Display")) == 1
            && Group("line").Count(a => (bool)Value(a, "Display")) == 2 && Group("bait/20260").Any(a => ((string)Value(a, "Id")).EndsWith("/tether")),
            "Warrior native map, raw tether, icon or cast outputs missing.");
        controller.Seek(1.5); Check(Mode("RequireAllStop") && !Mode("RequireWolMovement"), "Warrior event Pyretic did not enter stop mode.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!
            && !(bool)Value((object)runtime.Setting, "RuntimeMovementHeld"), "Warrior replay executed PR or movement hold.");
        controller.Seek(2.05); Check(!Mode("RequireAllStop"), "Warrior Pyretic removal failed.");
        controller.Seek(3.8);
        Check(!Group("sword").Any() && !Group("stack/20237").Any() && !Group("bait/20256").Any()
            && !Group("bait/21076").Any() && !Group("bait/20264").Any() && Group("spread").Any(),
            "Warrior actual independent effect endings differ.");
        controller.Seek(5.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2DDE && !Group("brimstone").Any() && !Group("tower").Any()
            && Group("knockback").Any(), "Warrior checkpoint failed independent state rebuild.");
        controller.Seek(9.6); Check(Mode("RequireWolMovement") && Group("element/move").Single() is { } moving
            && (Vector3)Value(moving, "Position") == c + Vector3.UnitX * 2, "Warrior ice movement anchor missing.");
        controller.Seek(10.1); Check(!Mode("RequireWolMovement") && Group("knockback").Any(), "Warrior personal death lost P2 or retained own mode.");
        controller.Seek(12.5); Check(!Group("knockback").Any(), "Warrior actual Suiton finish leaked.");
        controller.Seek(13.5); Check(previews().Length == 0 && dangers() == 0, "Warrior final phase death leaked.");
        controller.Seek(.8); Check(Group("brimstone").Length == 4 && Group("sword").Length == 1, "Warrior reverse Seek failed.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Warrior disable retained arena or outputs.");
        stop();
        Console.WriteLine("PASS WarriorOfLight actual DLL replay checkpoint, Pyretic/ice windows, sequential towers, growth, swords, markers, tethers, independent endings, Seek and isolation");
    }
}
