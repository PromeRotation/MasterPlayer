using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAetherfont(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.aetherfont";
        const uint territory = 1126;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!
            .GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string Key(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        int Count(string part) => Aoes().Count(a => Key(a).StartsWith("aetherfont/" + part));
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string Icon(double t, ulong actor, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = actor, TargetId = 100UL,
                MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        foreach (var (boss, alias, action, c) in new (uint, uint, uint, Vector3)[]
        {
            (0x3EC7, 0x3EC6, 33991, new(-409.805f, 0, 398.01f)),
            (0x3EC9, 0x3EC6, 33991, new(-360.86f, 0, 265.405f)),
            (0x3ECB, 0x3ECC, 33994, new(223.685f, 0, 5.97f)),
            (0x3ECD, 0x3ECA, 33995, new(382.715f, 0, -112.505f)),
            (0x3ED1, 0x3ED0, 33996, new(-345.55f, 0, -105.955f)),
        })
        {
            load(id,
            [
                Header(territory), Frame(territory, 0, [Actor(1, boss, c), Actor(10, alias, c, combat: true),
                    Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
                Frame(territory, .1, [Actor(10, alias, c, combat: true, cast: Cast(action, 5, c))], party: party),
                Frame(territory, 1, [Actor(1, boss, c, dead: true)], party: party),
                Frame(territory, 2, [Actor(10, alias, c, cast: Cast(action, 5, c))], party: party),
                Frame(territory, 5.6, [Actor(10, alias, c)], party: party),
                Frame(territory, 6, [Actor(10, alias, c, dead: true)], party: party),
            ]);
            controller.Seek(.5); Check(previews().Length == 1 && dangers() == 1, $"Aetherfont trash {boss:X} alias pull failed.");
            controller.Seek(2.5); Check(previews().Length == 1, "Primary death/combat flag lost surviving trash.");
            controller.Seek(5.7); Check(previews().Length == 0 && dangers() == 0, "Trash real ending leaked.");
            controller.Seek(.5); Check(previews().Length == 1, "Trash Seek failed."); stop();
        }
        var center = new Vector3(-322, 0, 120);
        List<string> records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3EEB, center, combat: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, Enumerable.Range(10, 11).Select(i => Actor((ulong)i, 0x233C, center + Vector3.UnitX * (i - 10),
                cast: Cast(i == 10 ? 33340u : 33339u, i == 10 ? 10 : 5 + (i - 11) * .25, center + Vector3.UnitX * (i - 10)))), party: party),
            Frame(territory, 8, Enumerable.Range(11, 5).Select(i => Actor((ulong)i, 0x233C, center + Vector3.UnitX * (i - 10))), party: party),
            Frame(territory, 9, Enumerable.Range(16, 5).Select(i => Actor((ulong)i, 0x233C, center + Vector3.UnitX * (i - 10))), party: party),
            Frame(territory, 11, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 12, [Actor(30, 0x233C, center, cast: Cast(33343, 5, center, target: 101, startedAt: 12)),
                Actor(31, 0x233C, center, cast: Cast(33342, 5, center, target: 100, startedAt: 12)),
                Actor(1, 0x3EEB, center, combat: true, cast: Cast(33335, 5, center, startedAt: 12))], party: party),
            Frame(territory, 17.6, [Actor(30, 0x233C, center), Actor(31, 0x233C, center), Actor(1, 0x3EEB, center, combat: true)], party: party),
        ];
        load(id, records);
        controller.Seek(.5); Check(Count("crystal") == 4 && previews().Length == 4 && dangers() == 4, "Lyngbakr strict prefix or eleven sort failed.");
        controller.Seek(8.5); Check(Count("crystal") == 4, "Real crystal ends did not promote next group.");
        controller.Seek(9.5); Check(Count("crystal") == 1, "Last large crystal failed to promote.");
        controller.Seek(12.5); Check(Count("stack") > 0 && Count("spread") > 0 && Damage() == 2, "Lyngbakr stack/spread/raidwide missing.");
        controller.Seek(17.7); Check(previews().Length == 0 && dangers() == 0 && Damage() == 0, "Lyngbakr endings leaked.");
        controller.Seek(.5); Check(Count("crystal") == 4, "Lyngbakr Seek failed."); stop();

        center = new(425, 0, -440);
        records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3EEA, center, combat: true), Actor(10, 0x233C, center),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x3EEA, center, combat: true, cast: Cast(33364, 3, center))], party: party),
            Map(.2, 1, 0x00020001), Map(.3, 2, 0x00200010),
            Frame(territory, 4, [Actor(1, 0x3EEA, center, combat: true)], party: party),
            Map(5, 0, 0x00020001), Map(6, 0, 0x00080004),
            Icon(7, 101, 161), EffectEvent(8, 1, 33366, 1), EffectEvent(9, 1, 34712, 2, target: 101),
        ];
        for (var i = 0; i < 16; ++i)
        {
            records.Add(Frame(territory, 10 + i * 2, [Actor(10, 0x233C, center, cast: Cast(33361, 1, center, startedAt: 10 + i * 2))], party: party));
            records.Add(Frame(territory, 11.5 + i * 2, [Actor(10, 0x233C, center)], party: party));
        }
        records.Add(Frame(territory, 43, [Actor(10, 0x233C, center, cast: Cast(33361, 1, center, startedAt: 43))], party: party));
        records.Add(Frame(territory, 43.5, [], removed: [10], party: party));
        load(id, records);
        controller.Seek(4.5); Check(Count("shrink") == 1 && Count("fissure") == 16, "Arkas forecast prematurely confirmed or cap missing.");
        controller.Seek(5.5); Check(Count("shrink") == 0, "Arkas shrink map not confirmed.");
        controller.Seek(8.5); Check(Count("stack") > 0 && Damage() == 0, "Actor icon stack lost before actual hit or added raidwide.");
        controller.Seek(9.5); Check(Count("stack") == 0 && Damage() == 0, "Real icon stack hit failed to clear.");
        controller.Seek(42); Check(Count("fissure") == 16 && Aoes().Where(a => Key(a).Contains("/fissure/"))
            .All(a => Key(a).StartsWith("aetherfont/fissure/2/")), "Sixteen real endings did not promote second map.");
        controller.Seek(44); Check(Count("fissure") == 15, "Casting helper destruction failed FIFO.");
        controller.Seek(4.5); Check(Count("fissure") == 16, "Arkas Seek failed."); stop();

        center = new(-370, 0, -368);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3EAA, center, targetable: false),
                Actor(10, 0x3EAB, new(-370, 0, -343)), Actor(100, 0, center), Actor(101, 0, new(-370, 0, -343))], full: true, party: party),
            Frame(territory, .2, [Actor(100, 0, new(-370, 0, -343))], party: party),
            Frame(territory, 1, [Actor(1, 0x3EAA, center),
                Actor(10, 0x3EAB, new(-370, 0, -343), cast: Cast(33346, 5, new(-370, 0, -343), startedAt: 1)),
                Actor(11, 0x3EAB, new(-395, 0, -368), cast: Cast(33348, 8, new(-395, 0, -368), startedAt: 1)),
                Actor(12, 0x233C, center, cast: Cast(34436, 5, center, target: 101, startedAt: 1))], party: party),
            Frame(territory, 6.5, [Actor(10, 0x3EAB, new(-370, 0, -343)), Actor(12, 0x233C, center)], party: party),
            Frame(territory, 9.5, [Actor(11, 0x3EAB, new(-395, 0, -368))], party: party),
        ]);
        controller.Seek(.1); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null, "Octopus pulled from water.");
        controller.Seek(.5); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Platform entry still requires boss targetable/combat.");
        controller.Seek(2); Check(previews().Length == 3 && dangers() > 0 && (ulong)Encounter().GetType().GetMethod("PriorityTarget")!
            .Invoke(Encounter(), [10UL, new Vector3(-370, 0, -343)])! == 1, "Octopus real attacks/priority missing.");
        controller.Seek(7); Check(previews().Length == 1 && dangers() == 1, "Independent tentacle ending lost survivor.");
        controller.Seek(10); Check(previews().Length == 0 && dangers() == 0, "Octopus ending leaked.");
        controller.Seek(2); Check(previews().Length == 3, "Octopus Seek failed."); stop();
        Check(previews().Length == 0 && dangers() == 0, "Aetherfont Stop leaked outputs.");
        Console.WriteLine("PASS production Aetherfont: five trash groups, crystal time prefixes, reverse map FIFO/append/cap, map arena changes, icon/cast hard stacks, spread thresholds, octopus platform pull/priority, independent endings, seek/stop");
    }
}
