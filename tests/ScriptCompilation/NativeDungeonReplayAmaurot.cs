using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAmaurot(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.amaurot";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Contains(Vector3 p)
        {
            var arena = Encounter().GetType().GetProperty("Arena")!.GetValue(Encounter())!;
            return (bool)arena.GetType().GetMethod("Contains")!.Invoke(arena, [p, .001f])!;
        }
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-80, 0, 82);
        load(id,
        [
            Header(838), Frame(838, 0, [Actor(1, 0x27B6, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            ResearchIconEvent(.1, 100, 57),
            Frame(838, 8.2, [Actor(10, 0x29DC, c + Vector3.UnitZ * 14, hitboxRadius: 2.4f)], party: party),
            Frame(838, 9, [Actor(1, 0x27B6, c, combat: true, cast: Cast(15563, 12, c, startedAt: 9))], party: party),
            Frame(838, 21.1, [Actor(1, 0x27B6, c, combat: true)], party: party),
            Frame(838, 22, [Actor(20, 0x233C, c, cast: Cast(15560, 5.2, c, target: 100, startedAt: 22))], party: party),
            Frame(838, 27.3, [Actor(20, 0x233C, c)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && Aoes().Any(a => AoeId(a) == "amaurot/meteor/edge"), "Production Amaurot meteor icon/edge missing.");
        controller.Seek(8.3); Check(previews().Length == 0, "Production Amaurot star birth did not hand off bait.");
        controller.Seek(9.1); Check(previews().Length == 2 && dangers() == 1 && Aoes().Any(a => AoeId(a).StartsWith("amaurot/los/enter")),
            "Production Amaurot meteor LOS missing.");
        controller.Seek(21.2); Check(previews().Length == 0 && dangers() == 0, "Production Amaurot LOS finish leaked.");
        controller.Seek(22.1); Check(Aoes().Any(a => AoeId(a) == "party/separate/20/101"), "Production Amaurot spread missing.");
        controller.Seek(27.4); Check(previews().Length == 0 && dangers() == 0, "Production Amaurot spread finish leaked.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Amaurot first beast seek failed."); stop();

        c = new(60, 0, -361);
        load(id,
        [
            Header(838), Frame(838, 0, [Actor(1, 0x27B8, c, combat: true), Actor(10, 0x2879, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(838, .1, [Actor(10, 0x2879, c, cast: Cast(15571, 4, c, .7f))], party: party),
            Frame(838, 4.2, [Actor(10, 0x2879, c)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1 && !Contains(new(60, 0, -341)), "Production Amaurot Bellwether cast or removed lip missing.");
        controller.Seek(4.3); Check(previews().Length == 0 && dangers() == 0, "Production Amaurot Bellwether finish leaked."); stop();

        c = new(0, 0, -45);
        Vector3[] faces = [new(-10, 0, -68), new(-10, 0, -60), new(10, 0, -52), new(10, 0, -44)];
        Vector3[] platforms = [new(-12, 0, -71), new(12, 0, -71), new(-12, 0, -51), new(12, 0, -51),
            new(-12, 0, -31), new(12, 0, -31), new(-12, 0, -17), new(12, 0, -17)];
        List<string> frames =
        [
            Header(838), Frame(838, 0, [Actor(1, 0x27C1, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8),
                Actor(20, 0x233C, c),
                .. Enumerable.Range(0, 4).Select(i => Actor((ulong)(10 + i), 0x27C3, faces[i], rotation: i < 2 ? MathF.PI / 2 : -MathF.PI / 2))], full: true, party: party),
            Frame(838, .1, [Actor(1, 0x27C1, c, combat: true, cast: Cast(15575, 6, c, MathF.PI))], party: party),
            Frame(838, 6.2, [Actor(1, 0x27C1, c, combat: true)], party: party),
        ];
        for (var i = 0; i < 4; ++i) frames.Add(EffectEvent(6.4 + i * .5, 50, 15577, (uint)i));
        frames.Add(Frame(838, 10, [Actor(20, 0x233C, c, cast: Cast(17107, 5, c, .7f, startedAt: 10))], party: party));
        frames.Add(Frame(838, 15.1, [Actor(20, 0x233C, c)], party: party));
        for (var i = 0; i < 4; ++i) frames.Add(EffectEvent(15.3 + i * .5, 50, 15585, (uint)i));
        for (var face = 0; face < 4; ++face)
            for (var hit = 0; hit < 5; ++hit)
                frames.Add(EffectEvent(20 + face * 5 + hit, (ulong)(10 + face), hit == 0 ? 15580u : 15581u,
                    (uint)(face * 10 + hit), rotation: face < 2 ? MathF.PI / 2 : -MathF.PI / 2));
        for (var i = 0; i < 8; ++i)
        {
            frames.Add(Frame(838, 42 + i * 2, [Actor((ulong)(30 + i), 0x1EA000, platforms[i], eventAnimation: 0x00100020)], party: party));
            frames.Add(Frame(838, 43 + i * 2, [Actor((ulong)(30 + i), 0x1EA000, platforms[i], eventAnimation: 0x00040008)], party: party));
        }
        load(id, frames);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Amaurot apocalypse missing.");
        controller.Seek(6.3); Check(previews().Length == 1, "Production Amaurot apocalypse natural finish erased five-hit forecast.");
        controller.Seek(7.5); Check(previews().Length == 1, "Production Amaurot apocalypse ended before fourth rest.");
        controller.Seek(8); Check(previews().Length == 0 && dangers() == 0, "Production Amaurot apocalypse fourth rest not consumed.");
        controller.Seek(15.2); Check(previews().Length == 1, "Production Amaurot ray natural finish erased forecast.");
        controller.Seek(17); Check(previews().Length == 0 && dangers() == 0, "Production Amaurot ray rest effects leaked.");
        controller.Seek(20.1); Check(previews().Length == 4 && dangers() == 2, "Production Amaurot faces group prediction missing.");
        controller.Seek(29.1); Check(previews().Length == 2 && dangers() == 2, "Production Amaurot future faces did not promote after two fifth hits.");
        controller.Seek(39.1); Check(previews().Length == 0 && dangers() == 0, "Production Amaurot four faces five effects not cleared.");
        for (var i = 0; i < 8; ++i)
        {
            controller.Seek(42.1 + i * 2); Check(Aoes().Any(a => AoeId(a) == $"amaurot/break/{i}"), "Production Amaurot platform warning missing.");
            controller.Seek(43.1 + i * 2); Check(!Contains(platforms[i]) && !Aoes().Any(a => AoeId(a) == $"amaurot/break/{i}"), "Production Amaurot platform deletion failed.");
        }
        Check(!Contains(new(0, 0, -65)) && !Contains(new(0, 0, -45)) && Contains(new(0, 0, -25)), "Production Amaurot two main path cuts differ.");
        controller.Seek(.2); Check(Contains(platforms[0]) && previews().Length == 1, "Production Amaurot seek did not restore full arena."); stop();
        Console.WriteLine("PASS native Amaurot three bosses, meteor edge and LOS, spread, both five-hit beams, four faces five hits, all platform removals and main cuts, seek and stop");
    }
}
