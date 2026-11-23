using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTenderValley(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.tender-valley"; const uint territory = 1203;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("tender/" + key)).ToArray();
        bool Nav(object a) => (bool)Value(a, "Navigate");
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string IconEvent(double t, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = target, TargetId = target,
                MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        string TargetCast(double t, ulong source, uint action, double seconds, Vector3 position, ulong target)
            => JsonSerializer.Serialize(new { t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
                data = new { SourceId = source, TargetId = target, ActionId = action, CastTime = seconds,
                    EffectPosition = Pose(position), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL },
            new { Slot = 2, EntityId = 102UL }, new { Slot = 3, EntityId = 103UL }];
        var c = new Vector3(-65, 0, 470);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4234, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(20, 0x1EBBF0, c + Vector3.UnitX * 12), Actor(21, 0x1EBBF1, c - Vector3.UnitX * 12),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 1, 37387, 3, c),
            Frame(territory, .2, [Actor(1, 0x4234, c, combat: true, cast: Cast(37387, 3, c))], party: party),
            Frame(territory, 3.5, [Actor(1, 0x4234, c, combat: true)], party: party),
            CastEvent(4, 10, 37390, 5, c),
            Frame(territory, 4.1, [Actor(10, 0x233C, c, cast: Cast(37390, 5, c, startedAt: 4))], party: party),
            CastEvent(5, 11, 37386, 2, c),
            Frame(territory, 5.1, [Actor(11, 0x233C, c, cast: Cast(37386, 2, c, startedAt: 5))], party: party),
            Frame(territory, 7.5, [Actor(11, 0x233C, c)], party: party),
            Frame(territory, 9.5, [Actor(10, 0x233C, c)], party: party),
            CastEvent(10, 20, 37388, 1, c),
            Frame(territory, 10.1, [Actor(20, 0x1EBBF0, c, cast: Cast(37388, 1, c, startedAt: 10))], party: party),
            Frame(territory, 11.5, [Actor(20, 0x1EBBF0, c)], party: party),
        ]);
        controller.Seek(1); Check(Group("cactus").Length == 2 && Group("cactus").All(Nav), "TenderValley initial cactus wave.");
        controller.Seek(4.5); Check(Group("cactus").Length == 2 && !Group("cactus").Any(Nav) && Group("kb/nav").Length == 1, "TenderValley KB display/risk.");
        controller.Seek(6); Check(Group("cactus").Length == 0 && Group("kb/nav").Length == 1, "TenderValley needle hiding.");
        controller.Seek(9.8); Check(Group("cactus").All(Nav), "TenderValley KB finish risk restore.");
        controller.Seek(11.6); Check(previews().Length == 0 && dangers() == 0, "TenderValley cactus finish cleanup.");
        controller.Seek(4.5); Check(Group("cactus").Length == 2 && !Group("cactus").Any(Nav), "TenderValley cactus Seek."); stop();

        c = new(-130, 0, -51);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x41BE, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 36401, 5, c), TargetCast(.1, 11, 36553, 5, c, 101),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(36401, 5, c)), Actor(11, 0x233C, c, cast: Cast(36553, 5, c, target: 101))], party: party),
            Frame(territory, 5.5, [Actor(10, 0x233C, c), Actor(11, 0x233C, c)], party: party),
        ]);
        controller.Seek(1); Check(Group("simple").Length == 1 && Group("spread").Length == 2 && dangers() == 2, "TenderValley Anthracite.");
        controller.Seek(6); Check(previews().Length == 0 && dangers() == 0, "TenderValley Anthracite cleanup."); stop();

        c = new(-130, 0, -475);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4253, c, combat: true), Actor(20, 0x4254, new(-112.5f, 0, -486.5f)),
                Actor(100, 0, c)], full: true),
            JsonSerializer.Serialize(new { t = .1, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 20, TargetId = 1, TetherId = 37, Timestamp = Start.AddSeconds(.1) } }),
            Frame(territory, 2, [Actor(20, 0x4254, c, rotation: 2)]),
            EffectEvent(9, 20, 39824, 1),
        ]);
        controller.Seek(3); Check(Group("statue").Length == 1 && dangers() == 1, "TenderValley statue snapshot.");
        controller.Seek(9.1); Check(previews().Length == 0 && dangers() == 0, "TenderValley statue resolve.");
        controller.Seek(3); Check(Group("statue").Length == 1, "TenderValley statue Seek."); stop();

        c = new(-130, 0, -554);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4164, c, combat: true), Actor(10, 0x233C, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            Map(.1, 0, 0x00020001), Map(.2, 1, 0x01000080),
            IconEvent(1, 100, 62), IconEvent(1, 101, 542), IconEvent(1, 101, 139),
            EffectEvent(6, 10, 39983, 1, target: 100), EffectEvent(6, 10, 39982, 2, target: 101), EffectEvent(6, 10, 36755, 3, target: 101),
            Map(12, 1, 0x00100004),
        ]);
        controller.Seek(3); Check(Group("labyrinth").Length == 4 && Group("stack").Count(a => (bool)Value(a, "Display")) == 2
            && Group("spread").Length > 0, "TenderValley labyrinth/icons.");
        controller.Seek(11); Check(Group("labyrinth").Length == 4 && Group("stack").Length == 0 && Group("spread").Length == 0, "TenderValley delayed labyrinth survives icon resolve.");
        controller.Seek(12.1); Check(previews().Length == 0 && dangers() == 0, "TenderValley labyrinth cleanup.");
        controller.Seek(3); Check(Group("labyrinth").Length == 4 && Group("stack").Length > 0, "TenderValley final boss Seek."); stop();
        Console.WriteLine("PASS: native TenderValley four encounters synthetic replay cactus/KB/needles/cast-spread/statue/labyrinth/icons/Seek");
    }
}
