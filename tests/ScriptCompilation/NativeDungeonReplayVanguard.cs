using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunVanguard(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.vanguard"; const uint territory = 1198;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("vanguard/" + key)).ToArray();
        bool Hold() => (bool)Value(Encounter(), "RequireAllStop");
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        foreach (var (boss, alias, action, c) in new (uint, uint, uint, Vector3)[]
        {
            (0x447A, 0x4478, 39017, new(-5, 0, 405)),
            (0x4479, 0x41D8, 38451, new(-100, 0, 300)),
            (0x41DA, 0x447B, 38452, new(-50, 0, -15)),
            (0x43D4, 0x41D6, 38060, new(40, 0, -320)),
        })
        {
            load(id,
            [
                Header(territory), Frame(territory, 0, [Actor(1, boss, c, combat: true), Actor(2, alias, c),
                    Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
                CastEvent(.1, 2, action, 5, c, .7f),
                Frame(territory, .2, [Actor(2, alias, c, cast: Cast(action, 5, c, .7f, startedAt: .1))], party: party),
                Frame(territory, 1, [Actor(1, boss, c, dead: true)], party: party),
                Frame(territory, 5.5, [Actor(2, alias, c)], party: party),
                Frame(territory, 6, [Actor(2, alias, c, dead: true)], party: party),
            ]);
            controller.Seek(1.5); Check(previews().Length == 1 && dangers() == 1, "Vanguard trash primary death lost active cast.");
            controller.Seek(5.6); Check(previews().Length == 0, "Vanguard trash real finish.");
            controller.Seek(1.5); Check(previews().Length == 1, "Vanguard trash Seek."); stop();
        }
        var center = new Vector3(-100, 0, 207);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x411D, center, combat: true), Actor(10, 0x233C, center),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 1, 39140, 5, center, .7f),
            Frame(territory, .2, [Actor(1, 0x411D, center, combat: true, cast: Cast(39140, 5, center, .7f, startedAt: .1))], party: party),
            Frame(territory, 5.5, [Actor(1, 0x411D, center, combat: true)], party: party),
            EffectEvent(7.2, 10, 36567, 1), EffectEvent(7.3, 10, 36565, 2),
            EffectEvent(7.4, 10, 36567, 3), EffectEvent(7.5, 10, 36565, 4),
            EffectEvent(7.6, 10, 36567, 5), EffectEvent(7.7, 10, 36565, 6),
            Frame(territory, 8, [Actor(1, 0x411D, center, combat: true, cast: Cast(36571, 1, center, startedAt: 8))], party: party),
            Frame(territory, 9.5, [Actor(1, 0x411D, center, combat: true)], party: party),
            Map(10, 0xA, 0x00020001),
        ]);
        controller.Seek(1); Check(Group("cast").Length == 1 && Group("rotary").Length == 6 && previews().Length == 7, "Commander cast/rotary.");
        controller.Seek(5.6); Check(Group("rotary").Length == 6 && Group("cast").Length == 0, "Commander real finish.");
        controller.Seek(7.25); Check(Group("rotary").Length == 5, "Commander popped two per hit.");
        controller.Seek(7.8); Check(previews().Length == 0, "Commander six hits.");
        controller.Seek(9.7); Check(Group("shrink").Length == 1, "Commander shrink awaits map.");
        controller.Seek(10.1); Check(Group("shrink").Length == 0, "Commander map clear.");
        controller.Seek(1); Check(Group("rotary").Length == 6, "Commander Seek."); stop();

        center = new(0, 0, -100);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4237, center, combat: true), Actor(10, 0x233C, center, rotation: .7f),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 8)], full: true, party: party),
            Map(.1, 0xC, 0x00020001), Map(.2, 0xD, 0x01000080),
            CastEvent(.3, 10, 37351, 5, center, .7f),
            Frame(territory, .4, [Actor(10, 0x233C, center, rotation: .7f, cast: Cast(37351, 5, center, .7f, startedAt: .3))], party: party),
            EffectEvent(1, 10, 37347, 1, target: 100),
            JsonSerializer.Serialize(new { t = 2d, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
                data = new { TargetId = 100UL, StatusId = 3802, DurationMilliseconds = 5000, Timestamp = Start.AddSeconds(2) } }),
            Frame(territory, 2.1, [Actor(100, 0, center, stackedStatus: 3802, statusExpiresAt: Start.AddSeconds(7))], party: party),
            Frame(territory, 5.7, [Actor(10, 0x233C, center, rotation: .7f)], party: party),
            EffectEvent(5.8, 10, 37351, 2),
            Frame(territory, 6, [Actor(10, 0x233C, center, rotation: .7f - 11 * MathF.PI / 180)], party: party),
            EffectEvent(6.3, 10, 37344, 3),
            StatusEvent(7, 100, 3802, 0, false),
            Frame(territory, 7.1, [Actor(100, 0, center)], party: party),
            EffectEvent(9, 1, 37345, 4, target: 101),
            Map(10, 0xD, 0x00080004),
        ]);
        controller.Seek(1.5); Check(Group("fence").Length == 1 && Group("battery").Length == 9 && Group("line").Count(a => (bool)Value(a, "Display")) == 1, "Protector inputs.");
        controller.Seek(3.3); Check(Group("fence").Length == 0 && Group("battery").Length == 9, "Fence activation.");
        controller.Seek(5.9); Check(Group("battery").Length == 9 && !Hold(), "Battery finish or bomb stopped early.");
        controller.Seek(6.4); Check(Hold(), "Bomb execution window.");
        var current = Group("battery").Single(a => ((string)Value(a, "Id")).EndsWith("/0"));
        Check(Math.Abs((float)Value(current, "Rotation") - (.7f - 22 * MathF.PI / 180)) < .0001, "Battery actual event rotation.");
        controller.Seek(7.2); Check(!Hold(), "Bomb loss hold.");
        controller.Seek(9.1); Check(Group("line").Length == 0, "Line real target correction.");
        controller.Seek(6.4); Check(Hold() && Group("battery").Length == 9, "Protector Seek."); stop();

        center = new(90, 0, -430);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x411E, center, combat: true), Actor(2, 0x41BA, center),
                Actor(10, 0x233C, center), Actor(11, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 36592, 4.5, center), CastEvent(.1, 11, 36591, 11, center, MathF.PI),
            Frame(territory, .2, [Actor(10, 0x233C, center, cast: Cast(36592, 4.5, center, startedAt: .1)),
                Actor(11, 0x233C, center, cast: Cast(36591, 11, center, MathF.PI, startedAt: .1))], party: party),
            Frame(territory, 1, [Actor(1, 0x411E, center, combat: false, targetable: false),
                Actor(2, 0x41BA, center, combat: true, cast: Cast(36597, 3, center, target: 100, startedAt: 1))], party: party),
            Map(2, 0, 0x00020001),
            Frame(territory, 4.4, [Actor(2, 0x41BA, center, combat: true)], party: party),
            Frame(territory, 5, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 11.5, [Actor(11, 0x233C, center)], party: party),
        ]);
        controller.Seek(1.5); Check(Group("cast").Length == 2 && dangers() == 1, "Zander phase/combo suppression.");
        controller.Seek(5.1); Check(Group("cast").Length == 1 && dangers() == 1, "Zander combo promotion.");
        controller.Seek(11.6); Check(previews().Length == 0, "Zander combo actual finish.");
        controller.Seek(1.5); Check(Group("cast").Length == 2, "Zander Seek."); stop();
        Console.WriteLine("Native Vanguard: four trash groups, three bosses, rotary FIFO, battery actual hits, fence arena, bomb control, line targets, P2 and Seek passed.");
    }
}
