using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunYuweyawata(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.yuweyawata"; const uint territory = 1242;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("yuwe/" + key)).ToArray();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        var c = new Vector3(73, 0, 277);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4641, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(100, 0, c)], full: true),
            Map(.1, 0x0D, 0x20001), CastEvent(1, 10, 41121, 3, c),
            Frame(territory, 1.1, [Actor(10, 0x233C, c, cast: Cast(41121, 3, c, startedAt: 1))]),
            Frame(territory, 4.5, [Actor(10, 0x233C, c)]),
            CastEvent(5, 11, 40626, 2, c),
            Frame(territory, 5.1, [Actor(11, 0x233C, c, cast: Cast(40626, 2, c, startedAt: 5))]),
            Frame(territory, 7.5, [Actor(11, 0x233C, c)]),
        ]);
        controller.Seek(.5); Check(Group("cell").Length == 1 && dangers() == 1, "Yuweyawata cell map.");
        controller.Seek(2); Check(Group("cell").Length == 0 && Group("batch").Length == 1 && previews().Length > 0, "Yuweyawata voltage suppresses cell.");
        controller.Seek(4.7); Check(Group("cell").Length == 1, "Yuweyawata cell returns.");
        controller.Seek(8); Check(dangers() == 0 && previews().Length == 0, "Yuweyawata cell finish.");
        controller.Seek(2); Check(Group("batch").Length == 1, "Yuweyawata voltage Seek."); stop();

        c = new(116, 0, -66);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x464A, c, combat: true), Actor(10, 0x233C, c),
                Actor(100, 0, c)], full: true),
            CastEvent(.1, 1, 40645, 3, c),
            Frame(territory, .2, [Actor(1, 0x464A, c, combat: true, cast: Cast(40645, 3, c, startedAt: .1))]),
            Map(4, 7, 0x800040), CastEvent(5, 10, 40642, 3, c),
            Frame(territory, 5.1, [Actor(10, 0x233C, c, cast: Cast(40642, 3, c, startedAt: 5))]),
            Frame(territory, 8.5, [Actor(10, 0x233C, c)]),
            Map(9, 7, 0x200010), Map(10, 7, 0x80004),
        ]);
        controller.Seek(1); Check(Group("prepare").Length == 1, "Yuweyawata prepare.");
        controller.Seek(6); Check(Group("prepare").Length == 0 && Group("batch").Length == 1 && dangers() == 1, "Yuweyawata paths soulweave.");
        controller.Seek(9.5); Check(Group("batch").Length == 0, "Yuweyawata soulweave finish.");
        controller.Seek(1); Check(Group("prepare").Length == 1, "Yuweyawata prepare Seek."); stop();

        c = new(34, 0, -710);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x464C, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(100, 0, c)], full: true),
            CastEvent(.1, 10, 40613, 5, c),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(40613, 5, c, startedAt: .1))]),
            Frame(territory, 5.5, [Actor(10, 0x233C, c)]),
            EffectEvent(6, 10, 40613, 1), EffectEvent(6.5, 10, 40614, 2),
            EffectEvent(7, 10, 40614, 3), EffectEvent(7.5, 10, 40614, 4),
            EffectEvent(8, 10, 40614, 5), EffectEvent(8.5, 10, 40614, 6),
            CastEvent(9, 11, 40661, 5, c),
            Frame(territory, 9.1, [Actor(11, 0x233C, c, cast: Cast(40661, 5, c, startedAt: 9))]),
            Map(10, 0x11, 0x800040),
        ]);
        controller.Seek(5.7); Check(previews().Length == 1 && dangers() == 1, "Yuweyawata claws retained.");
        controller.Seek(8.7); Check(previews().Length == 0, "Yuweyawata six hits.");
        controller.Seek(10); Check(Group("earth").Length == 10 && dangers() == 10, "Yuweyawata spiral.");
        controller.Seek(5.7); Check(previews().Length == 1, "Yuweyawata claws Seek."); stop();

        foreach (var (boss, action, center) in new (uint, uint, Vector3)[]
        {
            (0x4612, 40668, new(-14.514999f, 0, 508.6f)),
            (0x46A4, 40666, Center("Woolback")), (0x4652, 0, Center("Researcher")),
            (0x4616, 40672, Center("Specter")), (0x4619, 41118, Center("Phoebad")), (0x461B, 40675, Center("Golem")),
        })
        {
            var lines = new List<string>
            {
                Header(territory), Frame(territory, 0, [Actor(1, boss, center, combat: true), Actor(100, 0, center)], full: true),
            };
            if (action != 0)
            {
                lines.Add(CastEvent(.1, 1, action, 3, center));
                lines.Add(Frame(territory, .2, [Actor(1, boss, center, combat: true, cast: Cast(action, 3, center, startedAt: .1))]));
            }
            lines.Add(Frame(territory, 4, [Actor(1, boss, center, combat: true)]));
            load(id, lines); controller.Seek(1);
            Check(Value(Value(Encounter(), "Active"), "BossOid").Equals(boss), $"Yuweyawata trash {boss:X} discovery.");
            Check(action == 0 || previews().Length == 1 && dangers() == 1, $"Yuweyawata trash {boss:X} shapes.");
            controller.Seek(4); Check(previews().Length == 0, $"Yuweyawata trash {boss:X} finish.");
            controller.Seek(1); Check(action == 0 || previews().Length == 1, $"Yuweyawata trash {boss:X} Seek."); stop();
        }
        Vector3 Center(string name)
        {
            var type = managerObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.YuweyawataArenas")!;
            var arena = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [0f])!;
            return (Vector3)Value(arena, "Position");
        }
        Console.WriteLine("PASS: native Yuweyawata nine encounters synthetic replay voltage/cell/paths/soulweave/claws/spirals/trash/Seek");
    }
}
