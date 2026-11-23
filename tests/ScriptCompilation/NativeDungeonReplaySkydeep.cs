using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSkydeep(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.skydeep";
        const uint territory = 1194;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("sky/" + key)).ToArray();
        string Timeline(double t, ulong actor, uint timeline) => JsonSerializer.Serialize(new { t, kind = "PlayActionTimeline",
            type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
            data = new { SourceId = actor, TimelineId = timeline, Timestamp = Start.AddSeconds(t) } });
        string MapEvent(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-105, 0, -160);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x41D3, center, combat: true),
                Actor(20, 0x41D4, center, hitboxRadius: 1.1f), Actor(21, 0x41D4, center, hitboxRadius: 2.2f),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Timeline(.1, 20, 0x1E46), Timeline(.2, 20, 0x1E46), Timeline(.3, 20, 0x1E3C),
            CastEvent(1, 1, 36728, 4, center, .7f),
            Frame(territory, 1.1, [Actor(1, 0x41D3, center, combat: true, cast: Cast(36728, 4, center, .7f, startedAt: 1))], party: party),
            Frame(territory, 5.4, [Actor(1, 0x41D3, center, combat: true)], party: party),
            EffectEvent(7.3, 1, 36729, 1),
            CastEvent(8, 1, 36736, 5, center),
            Frame(territory, 8.1, [Actor(1, 0x41D3, center, combat: true, cast: Cast(36736, 5, center, startedAt: 8))], party: party),
            Frame(territory, 13.4, [Actor(1, 0x41D3, center, combat: true),
                Actor(21, 0x41D4, center, hitboxRadius: 2.2f, cast: Cast(36738, 1.5, center, startedAt: 13.4))], party: party),
            Frame(territory, 15.4, [Actor(21, 0x41D4, center, hitboxRadius: 2.2f)], party: party),
            Frame(territory, 16, [Actor(1, 0x41D3, center, combat: true, cast: Cast(36733, 1, center, startedAt: 16))], party: party),
            Frame(territory, 17.4, [Actor(1, 0x41D3, center, combat: true)], party: party),
            MapEvent(18, 0x13, 0x00020001), Timeline(19, 20, 0x1E3C),
        ]);
        controller.Seek(.5); Check(Group("bubble").Length == 3 && previews().Length == 1, "Bubble timeline duplicates/removal.");
        controller.Seek(1.5); Check(Group("wave").Length == 2, "Player wave packet input.");
        controller.Seek(5.5); Check(Group("wave").Length == 2, "Boss finish cleared players.");
        controller.Seek(7.5); Check(Group("wave").Length == 0, "Actual wave effect.");
        controller.Seek(8.5); Check(Group("burst").Length == 1 && (Vector3)Value(Group("burst")[0], "Position") == center + Vector3.UnitX * 8, "Burst prediction.");
        controller.Seek(15.5); Check(Group("burst").Length == 0, "Real burst finish.");
        controller.Seek(17.5); Check(Group("ring").Length == 1, "Ring should await map.");
        controller.Seek(19.5); Check(Group("ring").Length == 0 && Group("bubble").Length == 0, "Map/timeline cleanup.");
        controller.Seek(1.5); Check(Group("wave").Length == 2, "Wave Seek."); stop();

        center = new(-85, 0, -155);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4184, center, combat: true),
                Actor(10, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 38582, 5, center, .7f),
            Frame(territory, .2, [Actor(10, 0x233C, center, cast: Cast(38582, 5, center, .7f))], party: party),
            Frame(territory, 5.5, [Actor(10, 0x233C, center),
                Actor(1, 0x4184, center, combat: true, cast: Cast(36448, 1, center, startedAt: 5.5))], party: party),
            Frame(territory, 7, [Actor(1, 0x4184, center, combat: true)], party: party),
            MapEvent(8, 0x14, 0x00020001),
        ]);
        controller.Seek(1); Check(previews().Length == 1 && dangers() == 1, "Artillery packet heading.");
        controller.Seek(7.5); Check(Group("shrink").Length == 1, "Shrink map gate.");
        controller.Seek(8.5); Check(Group("shrink").Length == 0 && previews().Length == 0, "Artillery map cleanup."); stop();

        center = new(100, 0, -430);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x41C7, center, combat: true),
                Actor(10, 0x233C, center), Actor(11, 0x233C, center), Actor(12, 0x233C, center),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 36671, 9, center, .7f), CastEvent(.1, 11, 36670, 7, center, -.7f),
            CastEvent(.1, 12, 36667, 6, center - Vector3.UnitZ * 10),
            Frame(territory, .2, [Actor(10, 0x233C, center, cast: Cast(36671, 9, center, .7f)),
                Actor(11, 0x233C, center, cast: Cast(36670, 7, center, -.7f)),
                Actor(12, 0x233C, center, cast: Cast(36667, 6, center - Vector3.UnitZ * 10))], party: party),
            Frame(territory, 6.5, [Actor(12, 0x233C, center)], party: party),
            Frame(territory, 7.5, [Actor(11, 0x233C, center)], party: party),
            Frame(territory, 9.5, [Actor(10, 0x233C, center),
                Actor(1, 0x41C7, center, combat: true, cast: Cast(36681, 5, center, startedAt: 9.5))], party: party),
            Frame(territory, 15, [Actor(1, 0x41C7, center, combat: true)], party: party),
            Frame(territory, 27, [Actor(10, 0x233C, center, cast: Cast(36685, 3, center, startedAt: 27))], party: party),
            Frame(territory, 31, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 32, [Actor(10, 0x233C, center, cast: Cast(36688, 3, center, startedAt: 32)),
                Actor(11, 0x233C, center + Vector3.UnitX * 12, cast: Cast(36689, 5, center + Vector3.UnitX * 12, startedAt: 32))], party: party),
            Frame(territory, 36, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 38, [Actor(11, 0x233C, center)], party: party),
        ]);
        controller.Seek(1); Check(Group("stone").Count(a => (bool)Value(a, "Display")) == 2
            && Group("impact/36667/nav").Length == 1, "Stone/impact composition.");
        controller.Seek(7.6); Check(Group("stone").Count(a => (bool)Value(a, "Display")) == 1, "Stone FIFO.");
        controller.Seek(15.5); Check(Group("shatter").Length == 0, "Shatter too early.");
        controller.Seek(25); Check(Group("shatter").Length == 2, "Delayed side shatter.");
        controller.Seek(31.5); Check(Group("shatter").Length == 0, "Shatter actual finish.");
        controller.Seek(33); Check(Group("tower").Count(a => (bool)Value(a, "Display")) == 2, "Independent towers.");
        controller.Seek(36.5); Check(Group("tower").Count(a => (bool)Value(a, "Display")) == 1, "First tower group.");
        controller.Seek(38.5); Check(previews().Length == 0 && dangers() == 0, "Final cleanup.");
        controller.Seek(1); Check(Group("stone").Count(a => (bool)Value(a, "Display")) == 2, "Stone Seek."); stop();
        Console.WriteLine("PASS production Skydeep: packet rotation, timeline bubble identity and capsule, player cone/facing intent, shifted bursts and real finish, map shrinking, stone FIFO/impact composition, delayed side shatter capped finish clock, independent towers, Seek/Stop");
    }
}
