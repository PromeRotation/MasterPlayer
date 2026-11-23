using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunGhimlyt(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.ghimlyt";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        DateTime Activation(object aoe) => (DateTime)aoe.GetType().GetProperty("Activation")!.GetValue(aoe)!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(296, 0, -110);
        load(id,
        [
            Header(1174), Frame(1174, 0, [Actor(1, 0x2526, center, combat: true), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(1174, .1, [Actor(1, 0x2526, center, combat: true, cast: Cast(14965, 2.5, center, MathF.PI / 2)),
                Actor(10, 0x233C, center, cast: Cast(14468, 5, center, target: 100))], party: party),
            Frame(1174, 2.7, [Actor(1, 0x2526, center, combat: true)], party: party),
            Frame(1174, 5.2, [Actor(10, 0x233C, center)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() >= 2, "Production Ghimlyt Schola beam/spread missing.");
        controller.Seek(5.3); Check(previews().Length == 0 && dangers() == 0, "Production Ghimlyt Schola cleanup failed.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Ghimlyt Schola seek failed."); stop();

        center = new(375, 0, -143);
        load(id,
        [
            Header(1174), Frame(1174, 0, [Actor(1, 0x2528, center, combat: true), Actor(2, 0x2527, center + Vector3.UnitX * 3, combat: true),
                Actor(100, 0, center), Actor(10, 0x2632, center + Vector3.UnitZ * 8)], full: true, party: party),
            EffectEvent(.1, 10, 14472, 1),
            Frame(1174, 1, [Actor(2, 0x2527, center + Vector3.UnitX * 3, combat: true,
                cast: Cast(14967, 3, center + Vector3.UnitX * 3, startedAt: 1))], removed: [1], party: party),
            EffectEvent(3, 2, 14470, 2), Frame(1174, 4.1, [Actor(2, 0x2527, center, combat: true)], party: party),
        ]);
        controller.Seek(.2); Check(Aoes().Any(a => AoeId(a) == "ghimlyt/shield/enter"), "Production Ghimlyt shield missing.");
        controller.Seek(1.1); Check(Aoes().Any(a => AoeId(a) == "ghimlyt/shield/enter") && previews().Length == 2,
            "Production Ghimlyt primary removal lost companion state.");
        controller.Seek(4.2); Check(previews().Length == 0 && dangers() == 0, "Production Ghimlyt shield/companion cleanup failed.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Ghimlyt Schola II seek failed."); stop();

        center = new(-180.569f, 0, 68.523f);
        List<string> rotation =
        [
            Header(1174), Frame(1174, 0, [Actor(1, 0x25DA, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitZ * 10)], full: true, party: party),
            ResearchIconEvent(.1, 1, 168),
            Frame(1174, .2, [Actor(1, 0x25DA, center, combat: true, cast: Cast(14196, 5, center, startedAt: .2))], party: party),
            Frame(1174, 5.3, [Actor(1, 0x25DA, center, combat: true)], party: party),
        ];
        for (var i = 0; i < 6; ++i) rotation.Add(EffectEvent(5.4 + 1.1 * i, i == 0 ? 1UL : 20UL,
            i == 0 ? 14196u : 14671u, (uint)i));
        rotation.Add(Frame(1174, 11, [Actor(10, 0x1EA1A1, center, eventState: 1, rotation: .7f)], party: party));
        rotation.Add(Frame(1174, 12, [Actor(10, 0x1EA1A1, center, eventState: 4, rotation: .7f)], party: party));
        load(id, rotation);
        controller.Seek(5.35); Check(Aoes().Count(a => AoeId(a).StartsWith("ghimlyt/slash")) == 2,
            "Production Ghimlyt slash lost at first natural finish.");
        Check(Aoes().Where(a => AoeId(a) == "ghimlyt/slash/0").All(a =>
            Math.Abs((Activation(a) - Start.AddSeconds(7.7)).TotalMilliseconds) < 1), "Production Ghimlyt slash clock differs.");
        controller.Seek(10.95); Check(previews().Length == 0 && dangers() == 0, "Production Ghimlyt sixth slash retained.");
        controller.Seek(11.1); Check(Aoes().Any(a => AoeId(a).StartsWith("ghimlyt/fire")), "Production Ghimlyt fire sector missing.");
        controller.Seek(12.1); Check(previews().Length == 0 && dangers() == 0, "Production Ghimlyt state four failed.");
        controller.Seek(5.35); Check(previews().Length == 2, "Production Ghimlyt slash seek failed."); stop();

        center = new(134, 0, -35);
        List<string> heat =
        [
            Header(1174), Frame(1174, 0, [Actor(1, 0x2515, center, combat: true), Actor(100, 0, center)], full: true, party: party),
            Frame(1174, .1, [Actor(1, 0x2515, center, combat: true, cast: Cast(13397, 4.5, center))], party: party),
            Frame(1174, 4.7, [Actor(1, 0x2515, center, combat: true)], party: party),
            Frame(1174, 5.5, [Actor(10, 0x1EA1A1, center, eventState: 1, rotation: 68 * MathF.PI / 180, eventAnimation: 0x00040008)], party: party),
        ];
        for (var i = 0; i < 5; ++i) heat.Add(EffectEvent(12.2 + .2 * i, 20, 13400, (uint)i));
        heat.Add(Frame(1174, 13.5, [], party: party));
        load(id, heat);
        controller.Seek(4.8); Check(Aoes().Any(a => AoeId(a) == "ghimlyt/nitro"), "Production Ghimlyt Nitrospin natural finish retained no rim.");
        controller.Seek(5.6); Check(!Aoes().Any(a => AoeId(a) == "ghimlyt/nitro")
            && Aoes().Any(a => AoeId(a) == "ghimlyt/heat"), "Production Ghimlyt shrink to heat failed.");
        controller.Seek(12.85); Check(previews().Length == 1, "Production Ghimlyt heat cleared before fifth.");
        controller.Seek(13.6); Check(previews().Length == 0 && dangers() == 0, "Production Ghimlyt old animation revived heat.");
        controller.Seek(5.6); Check(previews().Length == 1, "Production Ghimlyt heat seek failed."); stop();

        center = new(371, 0, -265);
        List<string> duo =
        [
            Header(1174), Frame(1174, 0, [Actor(1, 0x25B0, center, combat: true), Actor(2, 0x25AF, center, combat: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(1174, .1, [Actor(10, 0x233C, center - Vector3.UnitZ * 4, cast: Cast(14411, 3.5, center - Vector3.UnitZ * 4, .4f)),
                Actor(11, 0x233C, center + Vector3.UnitZ * 4, cast: Cast(14412, 3.5, center + Vector3.UnitZ * 4, .8f)),
                Actor(12, 0x25B2, center)], party: party),
            Frame(1174, 3.2, [Actor(2, 0x25AF, center, combat: true)], removed: [1], party: party),
            EffectEvent(3.3, 2, 14104, 1),
            Frame(1174, 3.7, [Actor(10, 0x233C, center - Vector3.UnitZ * 4), Actor(11, 0x233C, center + Vector3.UnitZ * 4)], party: party),
            EffectEvent(6, 20, 14114, 2), EffectEvent(6.5, 20, 14114, 3),
        ];
        for (var i = 0; i < 8; ++i) duo.Add(EffectEvent(7 + i, 12, 14106, (uint)(10 + i)));
        duo.Add(Frame(1174, 15, [], party: party));
        load(id, duo);
        controller.Seek(.2); Check(Aoes().Count(a => AoeId(a).StartsWith("ghimlyt/tank")) == 8
            && Aoes().Count(a => AoeId(a).StartsWith("ghimlyt/crosshatch")) == 2, "Production Ghimlyt tanks/crosshatch forecasts missing.");
        controller.Seek(3.8); Check(Aoes().Count(a => AoeId(a).StartsWith("ghimlyt/tank")) == 8
            && Aoes().Any(a => AoeId(a) == "ghimlyt/tank/7") && Aoes().Count(a => AoeId(a).StartsWith("ghimlyt/crosshatch")) == 2,
            "Production Ghimlyt companion transition/telegraph handoff lost state.");
        controller.Seek(6.6); Check(!Aoes().Any(a => AoeId(a).StartsWith("ghimlyt/crosshatch")), "Production Ghimlyt crosshatch FIFO failed.");
        controller.Seek(15.1); Check(previews().Length == 0 && dangers() == 0, "Production Ghimlyt tanks rearmed.");
        controller.Seek(.2); Check(previews().Length == 10, "Production Ghimlyt duo seek failed."); stop();
        Console.WriteLine("PASS native Ghimlyt five modules, shield solver output, companion lifetime, zero heading six slashes, persistent fire, arena shrink, five heat hits, tank promotion, crosshatch timing, seek and stop");
    }
}
