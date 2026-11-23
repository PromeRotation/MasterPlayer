using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBurn(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.burn";
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
        var center = new Vector3(174, 0, 178);
        load(id,
        [
            Header(1173), Frame(1173, 0, [Actor(1, 0x2419, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 10), Actor(10, 0x241A, center + Vector3.UnitZ * 7)], full: true, party: party),
            ResearchIconEvent(.1, 100, 96), ResearchIconEvent(.1, 101, 96),
            Frame(1173, .2, [Actor(1, 0x2419, center, combat: true, cast: Cast(12692, 6, center, target: 100, startedAt: .2))], party: party),
            EffectEvent(2, 20, 12697, 1, target: 100), EffectEvent(2, 20, 12697, 1, target: 101),
            Frame(1173, 6.3, [Actor(1, 0x2419, center, combat: true, cast: Cast(12689, 5, center, startedAt: 6.3))], party: party),
            Frame(1173, 11.4, [Actor(1, 0x2419, center, combat: true)], removed: [10], party: party),
        ]);
        controller.Seek(.3); Check(Aoes().Any(a => AoeId(a) == "burn/hail/enter")
            && Aoes().Any(a => AoeId(a) == "burn/spread/100") && dangers() > 0, "Production Burn crystal mechanics missing.");
        Check(Aoes().Where(a => AoeId(a).StartsWith("burn/spread/")).All(a =>
            Math.Abs((Activation(a) - Start.AddSeconds(5.9)).TotalMilliseconds) < 1), "Production Burn spread clock shifted.");
        controller.Seek(2.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("burn/spread")), "Production Burn batch spread cleanup differs.");
        controller.Seek(6.4); Check(!Aoes().Any(a => AoeId(a).StartsWith("burn/hail"))
            && Aoes().Any(a => AoeId(a).StartsWith("burn/shardfall")), "Production Burn Hailfire to Shardfall handoff failed.");
        controller.Seek(11.5); Check(previews().Length == 0 && dangers() == 0, "Production Burn crystal cleanup failed.");
        controller.Seek(.3); Check(Aoes().Any(a => AoeId(a) == "burn/hail/enter"), "Production Burn crystal seek failed."); stop();

        center = new(0, 0, -70);
        load(id,
        [
            Header(1173), Frame(1173, 0, [Actor(1, 0x23AA, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x23AB, new(-18, 0, -75), rotation: MathF.PI / 2)], full: true, party: party),
            EffectEvent(.1, 1, 13526, 1), ResearchIconEvent(.1, 100, 2),
            Frame(1173, 2, [Actor(11, 0x1EA9EF, center + Vector3.UnitX * 6)], party: party),
            EffectEvent(4, 20, 11636, 2),
            Frame(1173, 5, [Actor(1, 0x23AA, center, combat: true, cast: Cast(11637, 3, center, startedAt: 5))], party: party),
            Frame(1173, 8.1, [Actor(1, 0x23AA, center, combat: true)], removed: [11], party: party),
        ]);
        controller.Seek(.2); Check(Aoes().Count(a => AoeId(a).StartsWith("burn/throttle")) == 2
            && Aoes().Where(a => AoeId(a).StartsWith("burn/throttle")).All(a =>
                Math.Abs((Activation(a) - Start.AddSeconds(8.3)).TotalMilliseconds) < 1), "Production Burn charge formation/time differs.");
        controller.Seek(4.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("burn/residue"))
            && Aoes().Any(a => AoeId(a).StartsWith("burn/sludge")), "Production Burn residue handoff differs.");
        controller.Seek(8.2); Check(previews().Length == 0 && dangers() == 0, "Production Burn full throttle cleanup failed.");
        controller.Seek(.2); Check(Aoes().Length == 3, "Production Burn drone seek failed."); stop();

        center = new(-300, 0, -400);
        load(id,
        [
            Header(1173), Frame(1173, 0, [Actor(1, 0x2431, center, combat: true), Actor(100, 0, center + Vector3.UnitZ * 10),
                Actor(101, 0, center + Vector3.UnitX * 10), Actor(10, 0x2432, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(1173, .1, [Actor(11, 0x233C, center, cast: Cast(12613, 3, center))], party: party),
            Frame(1173, 3.2, [Actor(11, 0x233C, center), Actor(1, 0x2431, center, combat: true,
                cast: Cast(12609, 24, center, startedAt: 3.2))], party: party),
            StatusEvent(3.3, 1, 618, 5, true),
            Frame(1173, 4, [], removed: [10], party: party),
            Frame(1173, 5, [Actor(11, 0x233C, center, cast: Cast(12614, 1.5, center, startedAt: 5))], party: party),
            Frame(1173, 6.6, [Actor(11, 0x233C, center)], party: party),
            EffectEvent(9.6, 20, 12610, 1), ResearchIconEvent(10, 100, 14),
            EffectEvent(16.1, 1, 12621, 2),
            Frame(1173, 17, [Actor(12, 0x2434, center, rotation: MathF.PI / 2)], party: party),
            Frame(1173, 23.6, [Actor(11, 0x233C, center, cast: Cast(12617, .7, center, startedAt: 23.6))], party: party),
            Frame(1173, 24.4, [Actor(11, 0x233C, center)], party: party),
        ]);
        controller.Seek(3.3); Check(Aoes().Count(a => AoeId(a).StartsWith("burn/cross")) == 2, "Production Burn future crosses lost at circle finish.");
        controller.Seek(4.1); Check(Aoes().Any(a => AoeId(a) == "burn/cold"
            && Math.Abs((Activation(a) - Start.AddSeconds(9.6)).TotalMilliseconds) < 1), "Production Burn last add fog retime differs.");
        controller.Seek(6.7); Check(!Aoes().Any(a => AoeId(a).StartsWith("burn/cross")), "Production Burn cross finish retained.");
        controller.Seek(10.1); Check(!Aoes().Any(a => AoeId(a) == "burn/cold")
            && Aoes().Any(a => AoeId(a).Contains("/pool/")), "Production Burn fog to ice bait handoff failed.");
        controller.Seek(16.2); Check(previews().Length == 0 && dangers() == 0, "Production Burn bait effect cleanup failed.");
        controller.Seek(17.1); Check(Aoes().Any(a => AoeId(a).StartsWith("burn/cauterize")), "Production Burn helper birth forecast missing.");
        controller.Seek(24.5); Check(previews().Length == 0 && dangers() == 0, "Production Burn cauterize finish retained.");
        controller.Seek(3.3); Check(Aoes().Count(a => AoeId(a).StartsWith("burn/cross")) == 2, "Production Burn dragon seek failed."); stop();
        Console.WriteLine("PASS native Burn three bosses, crystal LOS, spread batch effects, charge formation, sludge, circle-cross timing, cold fog retime, ice bait, helper birth, seek and stop");
    }
}
