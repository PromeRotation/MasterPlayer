using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunKtisis(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.ktisis";
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Single().GetType()
            .GetProperty("Script")!.GetValue(((IEnumerable)manager.Running.Values).Cast<object>().Single())!;
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        DateTime Activation(string key) => (DateTime)Aoes().Single(a => AoeId(a) == key).GetType()
            .GetProperty("Activation")!.GetValue(Aoes().Single(a => AoeId(a) == key))!;
        bool At(string key, double seconds) => Math.Abs((Activation(key) - Start.AddSeconds(seconds)).TotalMilliseconds) < .001;
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-144, 0, 49);
        load(id,
        [
            Header(974), Frame(974, 0, [Actor(1, 0x3323, c, combat: true), Actor(100, 0, c - Vector3.UnitX * 10),
                Actor(101, 0x123, new(-139, 0, 47), subKind: 9), Actor(10, 0x3324, c)], full: true, party: party),
            Map(.1, 0, 0x00200010),
            Frame(974, 1, [Actor(10, 0x3324, c, cast: Cast(25375, 10, c, startedAt: 1))], party: party),
            Frame(974, 2, [Actor(1, 0x3323, c, combat: true, cast: Cast(25180, 5, c, target: 100, startedAt: 2))], party: party),
            Frame(974, 7.4, [Actor(1, 0x3323, c, combat: true)], party: party),
            Frame(974, 11.4, [Actor(10, 0x3324, c)], party: party),
            Frame(974, 12, [Actor(11, 0x233C, c, cast: Cast(25177, 2, c, startedAt: 12))], party: party),
            Frame(974, 14.4, [Actor(11, 0x233C, c)], party: party), Map(15, 1, 0x00020001),
        ]);
        controller.Seek(.2); Check(At("ktisis/slice", 13.1) && previews().Length == 1, "Production first map clock missing.");
        controller.Seek(2.1); Check(At("ktisis/pillar/10", 7.3) && Has("ktisis/stack/gather")
            && Has("ktisis/stack/1"), "Production pillar retime/Buddy branch missing.");
        controller.Seek(7.5); Check(!Has("ktisis/stack/") && At("ktisis/pillar/10", 7.3), "Production smash finish erased pillar clock.");
        controller.Seek(12.1); Check(previews().Length == 1, "Production hidden helper duplicated map slice.");
        controller.Seek(14.5); Check(previews().Length == 0 && dangers() == 0, "Production map slice finish leaked.");
        controller.Seek(15.1); Check(At("ktisis/slice", 31), "Production later map clock differs.");
        controller.Seek(2.1); Check(Has("ktisis/stack/gather"), "Lyssa Seek failed."); stop();
        c = new(0, 0, 48);
        load(id,
        [
            Header(974), Frame(974, 0, [Actor(1, 0x3425, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            StatusEvent(.1, 1, 2813, 0, true), StatusEvent(.2, 1, 2814, 0, true),
            Frame(974, 1, [Actor(1, 0x3425, c, combat: true, cast: Cast(25735, 7, c, .7f, startedAt: 1))], party: party),
            Frame(974, 8.4, [Actor(1, 0x3425, c, combat: true)], party: party),
            EffectEvent(10.4, 1, 25739, 1),
        ]);
        controller.Seek(1.1); Check(Aoes().Count(a => AoeId(a).StartsWith("ktisis/breath/")) == 2 && previews().Length == 2, "Production ordered double breath missing.");
        controller.Seek(8.5); Check(previews().Length == 1 && dangers() == 1, "Production first finish failed to advance breath.");
        controller.Seek(10.5); Check(previews().Length == 0 && dangers() == 0, "Production second real hit leaked.");
        controller.Seek(1.1); Check(previews().Length == 2, "Ladon Seek failed."); stop();
        c = new(0, 0, -50);
        load(id,
        [
            Header(974), Frame(974, 0, [Actor(1, 0x348A, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10),
                Actor(10, 0x348B, c - Vector3.UnitZ * 20), Actor(20, 0x348C, c + Vector3.UnitX * 10, animationState2: 1, hitboxRadius: 2.4f),
                Actor(21, 0x348C, c - Vector3.UnitX * 5, hitboxRadius: 2.4f)], full: true, party: party),
            Frame(974, .1, [Actor(10, 0x348B, c - Vector3.UnitZ * 20, cast: Cast(27836, 4, c - Vector3.UnitZ * 20))], party: party),
            Frame(974, 4.5, [Actor(10, 0x348B, c - Vector3.UnitZ * 20)], party: party),
            EffectEvent(5, 10, 25887, 1, target: 100),
            EffectEvent(10.7, 10, 25900, 2, target: 100),
            JsonSerializer.Serialize(new { t = 11, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100UL, TargetId = 100UL, MarkerId = 218, Timestamp = Start.AddSeconds(11) } }),
            EffectEvent(16.1, 10, 25905, 3, target: 100),
            Frame(974, 17, [Actor(1, 0x348A, c, combat: true, cast: Cast(25886, 5, c, startedAt: 17))], party: party),
            Frame(974, 22.4, [Actor(1, 0x348A, c, combat: true)], party: party), Map(23, 8, 0x00020001),
            Frame(974, 24, [Actor(1, 0x348A, c, combat: true, cast: Cast(25907, 5, c, startedAt: 24)), Actor(100, 0, c, classJob: 19)], party: party),
        ]);
        controller.Seek(.2); Check(Has("ktisis/los/enter") && previews().Length == 1 && dangers() == 1, "Production meteor LoS missing.");
        var shadow = Aoes().Single(a => (bool)a.GetType().GetProperty("Safe")!.GetValue(a)!);
        Check(Math.Abs(((Vector3)shadow.GetType().GetProperty("Position")!.GetValue(shadow)!).X + 5) < .001, "Production used broken meteor.");
        controller.Seek(4.6); Check(previews().Length == 0 && dangers() == 0, "Production LoS finish leaked.");
        controller.Seek(5.1); Check(Has("ktisis/aero/") && At("ktisis/aero/0", 10.7), "Production aero clock missing.");
        controller.Seek(10.8); Check(previews().Length == 0 && dangers() == 0, "Production aero real hit leaked.");
        controller.Seek(11.1); Check(Has("ktisis/tornado/"), "Production tornado icon missing.");
        controller.Seek(16.2); Check(previews().Length == 0 && dangers() == 0, "Production tornado effect leaked.");
        controller.Seek(22.5); Check(Has("ktisis/ring") && At("ktisis/ring", 22.8), "Production shrink clock missing.");
        controller.Seek(23.1); Check(previews().Length == 0 && dangers() == 0, "Production shrink map leaked.");
        controller.Seek(24.1); Check(Encounter().GetType().GetMethod("KtisisInterruptTarget")!.Invoke(Encounter(), [19u]) != null,
            "Production interrupt target missing.");
        controller.Seek(.2); Check(Has("ktisis/los/enter"), "Hermes Seek failed."); stop();
        Console.WriteLine("PASS production Ktisis: three bosses, six-direction map timing, pillar retime, Buddy stack, ordered double breaths, intact meteor rectangular LoS, baits and true hit cleanup, shrink map, interrupt planning, seek/stop");
    }
}
