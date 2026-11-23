using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTwinning(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.twinning";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        object[] party = Enumerable.Range(0, 4).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var c = new Vector3(200, 0, 285);
        List<string> frames =
        [
            Header(840), Frame(840, 0, [Actor(1, 0x27D1, c, combat: true), Actor(20, 0x27D3, c + Vector3.UnitX * 14),
                .. Enumerable.Range(0, 4).Select(i => Actor((ulong)(100 + i), 0, c + new Vector3(i * 3 - 6, 0, 10)))], full: true, party: party),
        ];
        for (var i = 0; i < 4; ++i) frames.Add(ResearchIconEvent(.1 + i * .1, (ulong)(100 + i), (uint)(53 - i)));
        uint[] impact = [15713, 15718, 15719, 15720];
        for (var i = 0; i < 4; ++i) frames.Add(EffectEvent(8 + i, 1, impact[i], (uint)i));
        for (var i = 0; i < 4; ++i) frames.Add(ResearchIconEvent(12 + i * .1, (ulong)(100 + i), 90));
        uint[] pounce = [15711, 15721, 15722, 16842];
        for (var i = 0; i < 4; ++i) frames.Add(EffectEvent(17 + i, 1, pounce[i], (uint)(10 + i)));
        frames.Add(Frame(840, 21, [Actor(1, 0x27D1, c, combat: true, cast: Cast(15715, 5, c, target: 101, startedAt: 21))], party: party));
        frames.Add(Frame(840, 26.1, [Actor(1, 0x27D1, c, combat: true), Actor(30, 0x1E88F5, c)], party: party));
        frames.Add(Frame(840, 28, [Actor(30, 0x1E88F5, c, eventState: 7)], party: party));
        load(id, frames);
        controller.Seek(.5);
        Check(Aoes().Count(a => AoeId(a).StartsWith("twinning/impact/") && AoeId(a).Count(c => c == '/') == 2) == 4
            && Aoes().Any(a => AoeId(a) == "twinning/impact/0/avoid/20"), "Production Twinning line FIFO/cage protection missing.");
        for (var i = 0; i < 4; ++i)
        {
            controller.Seek(8.1 + i);
            Check(Aoes().Count(a => AoeId(a).StartsWith("twinning/impact/") && AoeId(a).Count(c => c == '/') == 2) == 3 - i,
                "Production Twinning line effects not consumed.");
        }
        controller.Seek(12.4); Check(Aoes().Any(a => AoeId(a) == "twinning/pounce/0/cage/20"), "Production Twinning pounce cage constraint missing.");
        for (var i = 0; i < 4; ++i)
        {
            controller.Seek(17.1 + i);
            Check(Aoes().Count(a => AoeId(a).StartsWith("twinning/pounce/") && AoeId(a).Count(c => c == '/') == 2) == 3 - i,
                "Production Twinning pounce effects not consumed.");
        }
        controller.Seek(21.1); Check(Aoes().Any(a => AoeId(a) == "twinning/stack/enter/1"), "Production Twinning mandatory stack missing.");
        controller.Seek(26.2); Check(previews().Length == 1 && dangers() == 1, "Production Twinning persistent pool missing.");
        controller.Seek(28.1); Check(previews().Length == 0 && dangers() == 0, "Production Twinning pool state seven leaked.");
        controller.Seek(.5); Check(previews().Length == 5, "Production Twinning Alpha seek failed."); stop();

        c = new(0, 0, 0);
        load(id,
        [
            Header(840), Frame(840, 0, [Actor(1, 0x2803, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(840, .1, [.. Enumerable.Range(0, 8).Select(i => Actor((ulong)(10 + i), 0x2804, c + Vector3.UnitX * i))], party: party),
            EffectEvent(8, 50, 15857, 1),
            Frame(840, 9, [Actor(50, 0x233C, c, cast: Cast(15855, 5, c, target: 100, startedAt: 9))], party: party),
            Frame(840, 14.1, [Actor(50, 0x233C, c)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 8 && dangers() == 8, "Production Twinning eight shock forecasts missing.");
        controller.Seek(8.1); Check(previews().Length == 0 && dangers() == 0, "Production Twinning shock clear revived stale objects.");
        controller.Seek(9.1); Check(Aoes().Any(a => AoeId(a) == "party/separate/50/101"), "Production Twinning thunder spread missing.");
        controller.Seek(14.2); Check(previews().Length == 0 && dangers() == 0, "Production Twinning spread finish leaked.");
        controller.Seek(.2); Check(previews().Length == 8, "Production Twinning Mithridates seek failed."); stop();

        load(id,
        [
            Header(840), Frame(840, 0, [Actor(1, 0x2805, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(840, .1, [Actor(10, 0x1EAC39, c, rotation: .7f), Actor(11, 0x1EAC39, c + Vector3.UnitZ * 5)], party: party),
            Frame(840, 5, [], removed: [10], party: party),
            Frame(840, 14.5, [Actor(50, 0x233C, c + Vector3.UnitX * .8f)], party: party),
            EffectEvent(14.6, 50, 15863, 1),
            Frame(840, 15, [Actor(51, 0x233C, c + Vector3.UnitZ * 5)], party: party),
            EffectEvent(15.1, 51, 15859, 2),
            Frame(840, 16, [Actor(52, 0x233C, c, cast: Cast(17306, 5, c, target: 100, startedAt: 16))], party: party),
            Frame(840, 21.1, [Actor(52, 0x233C, c)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 4 && dangers() == 4, "Production Twinning short/long laser forecasts missing.");
        var shortAoe = Aoes().Single(a => AoeId(a) == "twinning/laser/10/short");
        var longAoe = Aoes().Single(a => AoeId(a) == "twinning/laser/10/long");
        Check(Math.Abs(((DateTime)shortAoe.GetType().GetProperty("Activation")!.GetValue(shortAoe)! - Start).TotalSeconds - 6.6) < .001
            && Math.Abs(((DateTime)longAoe.GetType().GetProperty("Activation")!.GetValue(longAoe)! - Start).TotalSeconds - 14.4) < .001,
            "Production Twinning laser birth clocks differ.");
        controller.Seek(5.1); Check(previews().Length == 4, "Production Twinning disappeared VFX erased helper forecasts.");
        controller.Seek(14.7); Check(previews().Length == 2 && !Aoes().Any(a => AoeId(a).Contains("/10/")), "Production Twinning first laser proximity clear failed.");
        controller.Seek(15.2); Check(previews().Length == 0 && dangers() == 0, "Production Twinning second laser effect clear failed.");
        controller.Seek(16.1); Check(Aoes().Any(a => AoeId(a) == "party/separate/52/101"), "Production Twinning crystal spread missing.");
        controller.Seek(21.2); Check(previews().Length == 0 && dangers() == 0, "Production Twinning crystal finish leaked.");
        controller.Seek(.2); Check(previews().Length == 4, "Production Twinning Tycoon seek failed."); stop();
        Console.WriteLine("PASS native Twinning three bosses, four lines and pounces, cage protection, mandatory stack and pool, eight shocks, short/long laser clocks and helper cleanup, both spreads, seek and stop");
    }
}
