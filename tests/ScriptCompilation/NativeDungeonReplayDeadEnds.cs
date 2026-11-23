using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDeadEnds(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.dead-ends";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string Key(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => Key(a).StartsWith(prefix));
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(266.5f, 0, -178);
        var p = new Vector3(266.5f, 0, -196.9f);
        List<string> records =
        [
            Header(973), Frame(973, 0, [Actor(1, 0x34C4, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10),
                Actor(10, 0x34C5, p)], full: true, party: party),
            Map(.01, 0x2B, 0x00020080),
            Frame(973, .1, [Actor(10, 0x34C5, p, cast: Cast(25919, 6.5, p)),
                Actor(1, 0x34C4, c, combat: true, cast: Cast(28347, 5.5, c))], party: party),
            Frame(973, 5.9, [Actor(1, 0x34C4, c, combat: true)], party: party),
            Frame(973, 9, [Actor(10, 0x34C5, p)], party: party),
        ];
        for (var i = 1; i < 7; ++i)
        {
            var pos = p + Vector3.UnitZ * (6 * i);
            var t = 9.1 + i * 2;
            records.Add(Frame(973, t, [Actor((ulong)(20 + i), 0x233C, pos, cast: Cast(28348, 1.3, pos, startedAt: t))], party: party));
            records.Add(Frame(973, t + 1.7, [Actor((ulong)(20 + i), 0x233C, pos)], party: party));
        }
        load(id, records);
        controller.Seek(1); Check(previews().Length == 7 && Has("deadends/wave/safe")
            && Aoes().Where(a => Key(a).StartsWith("deadends/exa/")).All(a => !(bool)a.GetType().GetProperty("Navigate")!.GetValue(a)!),
            "Production nausea safe gap/exa display and navigation gate missing.");
        controller.Seek(6); Check(previews().Length == 5 && dangers() == 5, "Production nausea end did not restore exa.");
        controller.Seek(8.95); Check(Aoes().Single(a => Key(a) == "deadends/exa/1/0").GetType().GetProperty("Position")!.GetValue(
            Aoes().Single(a => Key(a) == "deadends/exa/1/0"))!.Equals(p), "Production timeout advanced exa without cast ending.");
        controller.Seek(9.05); Check(previews().Length == 5, "Production first exa failed to advance.");
        controller.Seek(23); Check(previews().Length == 0 && dangers() == 0, "Production last exa leaked.");
        controller.Seek(1); Check(Has("deadends/wave/safe"), "Grebuloff Seek failed."); stop();

        c = new(-104.99f, 0, -74.99f);
        load(id,
        [
            Header(973), Frame(973, 0, [Actor(1, 0x34C6, c, combat: true), Actor(100, 0, c + Vector3.UnitX * 5),
                Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(973, .1, [Actor(1, 0x34C6, c, combat: true, cast: Cast(25936, 5, c))], party: party),
            Frame(973, 5.5, [Actor(1, 0x34C6, c, combat: true)], party: party), Map(6, 0x17, 0x00020001),
            Frame(973, 7, [Actor(1, 0x34C6, c, combat: true, cast: Cast(28360, 3, c, target: 100, startedAt: 7))], party: party),
            EffectEvent(10.4, 1, 28360, 1, target: 100),
            Frame(973, 10.5, [Actor(1, 0x34C6, c, combat: true)], party: party),
            Frame(973, 11, [Actor(20, 0x1EB5F7, c + Vector3.UnitX * 5)], party: party),
            Frame(973, 12, [Actor(20, 0x1EB5F7, c + Vector3.UnitX * 5, eventState: 7)], party: party),
            Frame(973, 13, [Actor(1, 0x34C6, c, combat: true, cast: Cast(25935, 5, c, target: 100, startedAt: 13))], party: party),
            Frame(973, 18.4, [Actor(1, 0x34C6, c, combat: true)], party: party),
            EffectEvent(19, 1, 25935, 2, target: 100),
        ]);
        controller.Seek(5.6); Check(Has("deadends/ring"), "Production shrink ring ended before map.");
        controller.Seek(6.1); Check(previews().Length == 0, "Production shrink map leaked ring.");
        controller.Seek(10.6); Check(Has("deadends/void/predict/") && previews().Length == 1, "Production void effect prediction missing.");
        controller.Seek(11.1); Check(Has("deadends/void/actor/") && !Has("deadends/void/predict/") && dangers() == 1, "Production real void did not consume effect prediction.");
        controller.Seek(12.1); Check(previews().Length == 0, "Production first void state gate failed.");
        controller.Seek(18.5); Check(Has("deadends/elimination") && Damage() == 1, "Production elimination disappeared at finish.");
        controller.Seek(19.1); Check(previews().Length == 0 && Damage() == 0, "Production elimination actual effect leaked.");
        controller.Seek(11.1); Check(Has("deadends/void/actor/"), "Peacekeeper Seek failed."); stop();

        c = new(-269, 0, 465);
        records =
        [
            Header(973), Frame(973, 0, [Actor(1, 0x34C7, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(973, .1, Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C, c, cast: Cast(25939, i < 12 ? 3 : 6, c))), party: party),
            Frame(973, 3.5, Enumerable.Range(10, 2).Select(i => Actor((ulong)i, 0x233C, c)), party: party),
            Frame(973, 6.5, Enumerable.Range(12, 3).Select(i => Actor((ulong)i, 0x233C, c)), party: party),
            Frame(973, 7, [Actor(1, 0x34C7, c, combat: true, cast: Cast(25949, 5, c, target: 100, startedAt: 7))], party: party),
            Frame(973, 12.4, [Actor(1, 0x34C7, c, combat: true)], party: party), EffectEvent(13, 1, 25949, 1, target: 100),
        ];
        load(id, records);
        controller.Seek(1); Check(previews().Length == 3 && dangers() == 3, "Production Rala first three missing.");
        controller.Seek(3.6); Check(previews().Length == 3 && Has("deadends/light/14"), "Production Rala did not promote later circles.");
        controller.Seek(6.6); Check(previews().Length == 0, "Production Rala circles leaked.");
        controller.Seek(12.5); Check(Damage() == 1, "Production delayed Pity disappeared at finish.");
        controller.Seek(13.1); Check(Damage() == 0, "Production Pity effect leaked.");
        controller.Seek(1); Check(previews().Length == 3, "Rala Seek failed."); stop();
        Console.WriteLine("PASS production DeadEnds: three bosses, seven exaflare steps, no timeout advancement, wind safe gap, void prediction/objects, shrink map, effect-ended bait and buster, first-three circles, seek/stop");
    }
}
