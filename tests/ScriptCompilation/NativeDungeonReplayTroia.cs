using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTroia(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.troia";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!
            .GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string Key(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        int Count(string part) => Aoes().Count(a => Key(a).StartsWith("troia/" + part));
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        int Gazes() => ((IEnumerable)Encounter().GetType().GetProperty("Gazes")!.GetValue(Encounter())!).Cast<object>().Count();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index, Param1 = (ushort)state,
                Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        foreach (var (boss, alias, action, c, p) in new (uint, uint, uint, Vector3, Vector3)[]
        {
            (0x397D, 0x397C, 30214, new(100, 0, 175), new(90, 0, 155)),
            (0x399E, 0x397F, 30218, new(-165, 0, 20), new(-165, 0, 20)),
            (0x3983, 0x3982, 30216, new(0, 0, -80), new(0, 0, -80)),
            (0x3986, 0x3984, 30223, new(10, 0, -116), new(10, 0, -116)),
        })
        {
            load(id,
            [
                Header(1070), Frame(1070, 0, [Actor(1, boss, c), Actor(10, alias, p, combat: true),
                    Actor(100, 0, p), Actor(101, 0, p + Vector3.UnitX * 10)], full: true, party: party),
                Frame(1070, .2, [Actor(10, alias, p, combat: true, cast: Cast(action, 5, p, startedAt: .2))], party: party),
                Frame(1070, 1, [Actor(1, boss, c, dead: true)], party: party),
                Frame(1070, 2, [Actor(10, alias, p, cast: Cast(action, 5, p, startedAt: .2))], party: party),
                Frame(1070, 5.6, [Actor(10, alias, p)], party: party),
                Frame(1070, 6, [Actor(10, alias, p, dead: true)], party: party),
            ]);
            controller.Seek(.5); Check(previews().Length == 1 && dangers() == 1, $"Production trash {boss:X} did not pull from companion.");
            controller.Seek(2.5); Check(previews().Length == 1, $"Production trash {boss:X} lost surviving cast.");
            controller.Seek(5.7); Check(previews().Length == 0 && dangers() == 0, "Trash ending leaked.");
            controller.Seek(.5); Check(previews().Length == 1, "Trash Seek failed."); stop();
        }
        var center = new Vector3(0, 0, 0);
        // Boss one needs a combatant dreamer; the primary itself does not enter combat.
        load(id,
        [
            Header(1070), Frame(1070, 0, [Actor(1, 0x3988, center), Actor(2, 0x3966, center, combat: true),
                Actor(10, 0x3968, center), Actor(11, 0x3968, center + Vector3.UnitX * 5),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10)], full: true, party: party),
            Frame(1070, .2, [Actor(2, 0x3966, center, combat: true, cast: Cast(29621, 10, center, startedAt: .2))], party: party),
            Frame(1070, 11, [Actor(2, 0x3966, center, combat: true),
                Actor(10, 0x3968, center, cast: Cast(29628, 1, center, startedAt: 11)),
                Actor(11, 0x3968, center, cast: Cast(29628, 1, center, startedAt: 11))], party: party),
            Frame(1070, 14, [Actor(10, 0x3968, center, dead: true, cast: Cast(29628, 1, center, startedAt: 11))], party: party),
            Frame(1070, 15, [Actor(10, 0x3968, center, dead: true)], party: party),
            Frame(1070, 16, [Actor(11, 0x3968, center)], party: party),
        ]);
        controller.Seek(1); Check(Count("risky") == 1 && previews().Length == 1 && dangers() == 0, "Early dreamer preview/risk differs.");
        controller.Seek(8.1); Check(dangers() == 1, "Dreamer late risk not enabled.");
        controller.Seek(11.5); Check(previews().Length == 0, "Third dreamer visible before death.");
        controller.Seek(14.5); Check(previews().Length == 2 && dangers() == 2, "Late death gate lost drawing/navigation.");
        controller.Seek(15.5); Check(previews().Length == 1 && dangers() == 1, "Dead caster ending erased group.");
        controller.Seek(22); Check(previews().Length == 0 && dangers() == 0, "Dreamer ending leaked.");
        controller.Seek(14.5); Check(previews().Length == 2 && dangers() == 2, "Dreamer Seek failed."); stop();
        center = new(0, 0, -200);
        List<string> records =
        [
            Header(1070), Frame(1070, 0, [Actor(1, 0x396D, center, combat: true),
                Actor(100, 0, center, classJob: 24), Actor(101, 0, center + Vector3.UnitX * 10)], full: true, party: party),
        ];
        records.Add(Frame(1070, .1, [Actor(9, 0x233C, center, cast: Cast(31207, 20, center))], party: party));
        records.Add(Frame(1070, .2, Enumerable.Range(10, 9).Select(i =>
            Actor((ulong)i, 0x233C, center, cast: Cast(29817, 10, center, startedAt: .2))), party: party));
        records.Add(Frame(1070, 1, [Actor(9, 0x233C, center), Actor(10, 0x233C, center),
            Actor(19, 0x233C, center, cast: Cast(29817, 10, center, startedAt: 1))], party: party));
        records.Add(Frame(1070, 11, Enumerable.Range(11, 8).Select(i => Actor((ulong)i, 0x233C, center)), party: party));
        records.Add(Frame(1070, 11.5, [Actor(19, 0x233C, center)], party: party));
        records.Add(Frame(1070, 12, [
            Actor(20, 0x233C, center + Vector3.UnitX * 10, cast: Cast(29821, 5, center + Vector3.UnitX * 10, startedAt: 12)),
            Actor(21, 0x233C, center + Vector3.UnitX * 10, cast: Cast(29828, 5, center + Vector3.UnitX * 10, startedAt: 12)),
            Actor(22, 0x233C, center - Vector3.UnitX * 10, cast: Cast(29828, 5, center - Vector3.UnitX * 10, startedAt: 12)),
            Actor(23, 0x233C, center + Vector3.UnitZ * 10, cast: Cast(29828, 5, center + Vector3.UnitZ * 10, startedAt: 12)),
        ], party: party));
        records.Add(Frame(1070, 16.8, [Actor(21, 0x233C, center + Vector3.UnitX * 10)], party: party));
        records.Add(Frame(1070, 17.5, [Actor(20, 0x233C, center), Actor(22, 0x233C, center), Actor(23, 0x233C, center)], party: party));
        records.Add(StatusEvent(18, 100, 3364, 0, true));
        records.Add(Frame(1070, 18.2, [Actor(100, 0, center, classJob: 24, stackedStatus: 3364)], party: party));
        records.Add(StatusEvent(19, 100, 3364, 0, false));
        records.Add(Frame(1070, 19.2, [Actor(100, 0, center, classJob: 24)], party: party));
        records.Add(Frame(1070, 20, [Actor(30, 0x233C, center, cast: Cast(31208, 5, center, target: 101, startedAt: 20))], party: party));
        records.Add(Frame(1070, 25.5, [Actor(30, 0x233C, center)], party: party));
        load(id, records);
        controller.Seek(.5); Check(previews().Length == 9 && dangers() == 9, "Production eight-AOE cap missing.");
        controller.Seek(1.5); Check(previews().Length == 8, "Ninth AOE did not promote.");
        var outputs = ((IEnumerable)Encounter().GetType().GetProperty("OutputCasts")!.GetValue(Encounter())!).Cast<object>();
        Check(outputs.Select(x => x.GetType().GetProperty("Value")!.GetValue(x)!)
            .All(x => (ulong)x.GetType().GetProperty("Source")!.GetValue(x)! != 19), "Dictionary hole reordered circle arrivals.");
        controller.Seek(12.5); Check(Gazes() == 3 && Count("gaze") == 3, "Production independent gaze caps or arcs missing.");
        controller.Seek(16.7);
        Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [center, MathF.PI / 2]) != null, "Production gaze facing missing.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!, "Replay published real PR facing.");
        controller.Seek(16.9); Check(Gazes() == 3, "Queued third gaze did not promote.");
        controller.Seek(18.5); Check(Encounter().GetType().GetMethod("TroiaCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Encounter(), [24u]) != null, "Production Doom gain missing.");
        controller.Seek(19.5); Check(Encounter().GetType().GetMethod("TroiaCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Encounter(), [24u]) == null, "Production Doom loss leaked.");
        controller.Seek(20.5); Check(Count("stack") == 2 && dangers() > 0, "Production stack hard constraint missing.");
        controller.Seek(26); Check(previews().Length == 0 && dangers() == 0, "Beatrice ending leaked.");
        controller.Seek(12.5); Check(Gazes() == 3, "Beatrice Seek failed."); stop();
        center = new(-35, 0, -298);
        load(id,
        [
            Header(1070), Frame(1070, 0, [Actor(1, 0x39C5, center, combat: true), Actor(100, 0, center + Vector3.UnitZ * 2),
                Actor(101, 0, center + Vector3.UnitX * 10), Actor(10, 0x233C, center),
                Actor(11, 0x39C8, center + Vector3.UnitZ * 19, hitboxRadius: 1)], full: true, party: party),
            Map(.1, 5, 0x00020001),
            Frame(1070, .2, [Actor(10, 0x233C, center, cast: Cast(30236, 5, center, startedAt: .2))], party: party),
            Map(1, 6, 0x00020001), Map(1.1, 6, 0x00200010),
            Frame(1070, 5.6, [Actor(10, 0x233C, center)], party: party),
            Map(6, 4, 0x00020001),
            Frame(1070, 6.1, [Actor(100, 0, center + Vector3.UnitZ * 22)], party: party),
            Frame(1070, 7, [Actor(100, 0, center + Vector3.UnitZ * 19)], party: party),
            Frame(1070, 8, [Actor(10, 0x233C, center, cast: Cast(30233, 5, center, target: 100, startedAt: 8))], party: party),
            Frame(1070, 13.5, [Actor(10, 0x233C, center)], party: party),
            Frame(1070, 14, [Actor(10, 0x233C, center, cast: Cast(30263, 5, center, target: 101, startedAt: 14))], party: party),
            Frame(1070, 15, [Actor(101, 0, center + Vector3.UnitZ * 12)], party: party),
            Frame(1070, 19.5, [Actor(10, 0x233C, center)], party: party),
            EffectEvent(20, 1, 30245, 1), EffectEvent(21, 1, 30245, 1, target: 101),
            EffectEvent(29, 10, 30247, 2), EffectEvent(30, 10, 30248, 3),
            EffectEvent(31, 10, 30250, 4), EffectEvent(32, 10, 30249, 5),
        ]);
        controller.Seek(.5); Check(Count("vacuum/safe") == 11 && Count("vacuum/avoid") == 1, "Production pending wall snapshots missing.");
        controller.Seek(2); Check(Count("vacuum/safe") == 11, "Map relocked active safe fans.");
        controller.Seek(5.7); Check(Count("vacuum") == 0, "Vacuum end leaked.");
        controller.Seek(6.5); Check(Count("return") == 1, "Return map route missing.");
        controller.Seek(7.5); Check(Count("return") == 0, "Return failed strict distance.");
        controller.Seek(8.5); Check(Count("wall") == 2 && dangers() > 0, "Spread wall navigation/display missing.");
        controller.Seek(15.5);
        var bait = Aoes().Single(a => Key(a) == "party/avoid/10");
        Check((Vector3)bait.GetType().GetProperty("Position")!.GetValue(bait)! == center + Vector3.UnitZ * 12 && Damage() == 1,
            "Production Firedamp tracking/buster missing.");
        controller.Seek(21.5); Check(Damage() == 2, "Pitch damage missing.");
        controller.Seek(31.5); Check(Damage() == 2, "Pitch consumed by wrong hit or timeout.");
        controller.Seek(32.5); Check(Damage() == 0, "Pitch third hit failed to clear.");
        controller.Seek(.5); Check(Count("vacuum/safe") == 11, "Scarmiglione Seek failed."); stop();
        Check(previews().Length == 0 && dangers() == 0, "Troia Stop leaked outputs.");
        Console.WriteLine("PASS production Troia: four trash groups, three bosses, risk thresholds, death gate, gaze caps/facing replay isolation, Doom, hard stacks, wall maps, vacuum snapshots, return, spread walls, tracking buster, delayed third hit, seek/stop");
    }
}
