using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunWorqor(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.worqor";
        const uint territory = 1193;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("worqor/" + key)).ToArray();
        string Tether(double t, ulong source) => JsonSerializer.Serialize(new { t, kind = "Tether",
            type = "PromeRotation.LogSystem.LogSystemTetherEvent",
            data = new { SourceId = source, TargetId = 1UL, TetherId = 272, Timestamp = Start.AddSeconds(t) } });
        string MapEvent(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string IconEvent(double t, ulong actor, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = actor, TargetId = target,
                MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-108, 0, 119.1f);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4159, center, combat: true), Actor(10, 0x415B, center),
                Actor(11, 0x415A, center), Actor(12, 0x415B, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 36270, 4, center, .7f), CastEvent(.1, 11, 36271, 6, center, .7f),
            CastEvent(.1, 12, 36270, 8, center, .7f),
            Frame(territory, .2, [Actor(10, 0x415B, center, cast: Cast(36270, 4, center)),
                Actor(11, 0x415A, center, cast: Cast(36271, 6, center)),
                Actor(12, 0x415B, center, cast: Cast(36270, 8, center))], party: party),
            Tether(1, 10), Tether(2, 11),
            Frame(territory, 4.5, [Actor(10, 0x415B, center)], party: party),
            Frame(territory, 6.5, [Actor(11, 0x415A, center)], party: party),
            Frame(territory, 8.5, [Actor(12, 0x415B, center)], party: party),
            Frame(territory, 9, [Actor(20, 0x233C, center, cast: Cast(36280, 1, center, startedAt: 9))], party: party),
            Frame(territory, 11, [Actor(20, 0x233C, center)], party: party), MapEvent(12, 0x17, 0x00020001),
        ]);
        controller.Seek(.5); Check(previews().Length == 0 && dangers() == 0, "Unfrozen list must be hidden.");
        controller.Seek(1.5); Check(Group("frozen").Length == 2 && dangers() == 2, "Freeze first tutorial prefix.");
        controller.Seek(2.5); Check(((string)Value(Group("frozen")[0], "Id")).Contains("/12/"), "Second freeze sort all entries.");
        controller.Seek(4.6); Check(Group("frozen").Length == 2, "Real caster finish removed wrong entry.");
        controller.Seek(8.6); Check(previews().Length == 0 && dangers() == 0, "Frozen final real finish.");
        controller.Seek(11.5); Check(Group("shrink").Length == 1, "Shrink ended before map.");
        controller.Seek(12.5); Check(Group("shrink").Length == 0, "Map did not shrink.");
        controller.Seek(2.5); Check(Group("frozen").Length == 2, "Frozen Seek."); stop();

        center = new(-52.625f, 0, -57.5f);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x415D, center, combat: true), Actor(20, 0x415E, new(-43, 0, -57)),
                Actor(100, 0, center), Actor(101, 0, new(-43, 0, -57))], full: true, party: party),
            MapEvent(.1, 0x1E, 0x00800040), IconEvent(.2, 101, 100, 511),
            MapEvent(1, 0x20, 0x00800040), MapEvent(2, 99, 0x04000004),
            EffectEvent(3, 20, 36296, 1, target: 999),
            Frame(territory, 4, [Actor(10, 0x233C, center, cast: Cast(36153, 1, center, startedAt: 4)),
                Actor(11, 0x233C, center, cast: Cast(36297, 1, center, startedAt: 4))], party: party),
            Frame(territory, 6, [Actor(10, 0x233C, center), Actor(11, 0x233C, center)], party: party),
        ]);
        controller.Seek(.5); Check(Group("donut").Length == 2 && Group("donut").Single(a => (bool)Value(a, "Display")).GetType() != null, "Donut icon actor/eligible buddy.");
        controller.Seek(1.5); Check(Group("wind/join").Length == 1 && !(bool)Value(Group("wind/display").Single(), "Navigate"), "Inverted crystal navigation.");
        controller.Seek(2.5); Check(Group("wind").Length == 0 && Group("donut").Count(a => (bool)Value(a, "Display")) == 1, "Map empty lost icon or crashed.");
        controller.Seek(3.5); Check(Group("donut").Length == 0, "Unmatched donut resolve.");
        controller.Seek(4.5); Check(Group("tower/join").Length == 1 && previews().Any(), "Tower/gaze missing.");
        controller.Seek(6.5); Check(Group("tower").Length == 0, "Tower actual finish.");
        stop();

        center = new(-54, 0, -195);
        List<string> records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x415F, center, combat: true),
                Actor(100, 0, center + Vector3.UnitX * 2), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
        ];
        object[] helpers = Enumerable.Range(10, 16).Select(i => Actor((ulong)i, 0x233C, center,
            cast: Cast(36303, 5 + (25 - i) / 4d, center))).ToArray();
        records.Add(Frame(territory, .1, helpers, party: party));
        records.Add(Frame(territory, 5.2, [Actor(25, 0x233C, center)], party: party));
        records.Add(Frame(territory, 11, Enumerable.Range(10, 15).Select(i => Actor((ulong)i, 0x233C, center)).ToArray(), party: party));
        records.Add(Frame(territory, 12, [Actor(30, 0x233C, center, cast: Cast(36307, 2, center, MathF.PI / 2, startedAt: 12))], party: party));
        records.Add(Frame(territory, 15, [Actor(30, 0x233C, center), Actor(40, 0x4162, center, rotation: .7f),
            Actor(41, 0x4160, new(-40, 0, -210), dead: true),
            Actor(31, 0x233C, center, cast: Cast(39074, 10, center, startedAt: 15))], party: party));
        records.Add(EffectEvent(16, 1, 36315, 2, target: 101)); records.Add(EffectEvent(17, 1, 36314, 3, target: 101));
        records.Add(EffectEvent(18, 1, 39260, 4, target: 100));
        records.Add(Frame(territory, 26, [Actor(31, 0x233C, center), Actor(40, 0x4162, center, dead: true),
            Actor(41, 0x4160, new(-40, 0, -210), eventState: 7, dead: true)], party: party));
        load(id, records);
        controller.Seek(.5); Check(Group("fire").Length == 12 && Group("fire").Count(a => (bool)Value(a, "Navigate")) == 4, "Fire prefix/risk.");
        controller.Seek(5.5); Check(Group("fire").Length == 12, "Fire FIFO promotion.");
        controller.Seek(11.5); Check(Group("fire").Length == 0, "Fire not consumed.");
        controller.Seek(12.5); Check(Group("fire").Length == 2 && Group("fire").Single(a => (bool)Value(a, "Display")).GetType() != null,
            "Subsequent Flood safe rectangle.");
        controller.Seek(15.5); Check(Group("orb").Length == 2 && Group("whirlwind").Length == 6 && Group("knockback/39074/nav").Length == 1, "Orb/wind/dead object preservation.");
        controller.Seek(17.5); Check(Group("line").Count(a => (bool)Value(a, "Display")) == 1, "Middle hit cleared line.");
        controller.Seek(18.5); Check(Group("line").Length == 0, "Last line correction/resolve.");
        controller.Seek(22.5); Check(Group("knockback/39074/nav").Length == 1, "Late fans.");
        controller.Seek(26.5); Check(previews().Length == 0 && dangers() == 0, "Final event cleanup.");
        controller.Seek(.5); Check(Group("fire").Length == 12, "Allfire Seek."); stop();
        Check(previews().Length == 0 && dangers() == 0, "Worqor Stop.");
        Console.WriteLine("PASS production Worqor: packet cast input, freeze clocks/tutorial sort, exact map shrink and inverted crystal polygons, donut icon/empty-map boundary, towers/gaze, fire FIFO/risk/color, Flood safe phase, dead moving wind, orb/Sprint intent, late knockback fans, actual last line hit, Seek/Stop");
    }
}
