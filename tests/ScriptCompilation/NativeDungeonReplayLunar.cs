using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunLunar(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.lunar";
        const uint territory = 1164;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!
            .GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        string Key(object a) => (string)Value(a, "Id");
        object[] Group(string key) => Aoes().Where(a => Key(a).StartsWith("lunar/" + key)).ToArray();
        bool Flag(object a, string key) => (bool)Value(a, key);
        object[] Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().ToArray();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-401, 0, -231);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3FE2, center, combat: true), Actor(10, 0x233C, center),
                Actor(11, 0x3FE3, center, animationState2: 1), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, [Actor(11, 0x3FE3, center, animationState2: 0)], party: party),
            Frame(territory, .2, [Actor(11, 0x3FE3, center, animationState2: 1)], party: party),
            Frame(territory, 4, [Actor(10, 0x233C, center, cast: Cast(34787, 2, center, startedAt: 4))], party: party),
            Frame(territory, 7, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 8, [Actor(11, 0x3FE3, center, animationState2: 1, cast: Cast(35254, 2, center, startedAt: 8))], party: party),
            Frame(territory, 11, [Actor(11, 0x3FE3, center, animationState2: 1)], party: party),
            StatusEvent(12, 101, 3364, 0, true),
            Frame(territory, 12.1, [Actor(101, 0, center + Vector3.UnitX * 8, stackedStatus: 3364, statusExpiresAt: Start.AddSeconds(40))], party: party),
            StatusEvent(13, 101, 3364, 0, false),
            Frame(territory, 13.1, [Actor(101, 0, center + Vector3.UnitX * 8)], party: party),
            Frame(territory, 14, [Actor(1, 0x3FE2, center, combat: true, cast: Cast(35205, 1, center, startedAt: 14))], party: party),
            Frame(territory, 16, [Actor(1, 0x3FE2, center, combat: true), Actor(11, 0x3FE3, center, animationState2: 0)], party: party),
            Frame(territory, 17, [Actor(11, 0x3FE3, center, animationState2: 1)], party: party),
        ]);
        controller.Seek(.05); Check(Group("cross").Length == 0, "Initial model incorrectly synthesized an event.");
        controller.Seek(1); Check(Group("cross").Length == 1 && !Flag(Group("cross")[0], "Navigate") && previews().Length == 1 && dangers() == 0,
            "Staff early filled display or non-risk phase missing.");
        controller.Seek(3.5); Check(dangers() == 1 && Group("cross").Length == 1, "Staff five-second risk not enabled.");
        controller.Seek(5); Check(Group("cross").Length == 0 && previews().Length == 1 && dangers() == 1, "Explosion did not hide cross group.");
        controller.Seek(7.5); Check(Group("cross").Length == 1 && dangers() == 1, "Cross cache lost while hidden.");
        controller.Seek(11.5); Check(previews().Length == 0 && dangers() == 0, "Real hex finish did not clear group.");
        controller.Seek(12.5);
        Check(Encounter().GetType().GetMethod("LunarCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Encounter(), [24u]) != null, "Doom status/cleanse target missing.");
        controller.Seek(13.5);
        Check(Encounter().GetType().GetMethod("LunarCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Encounter(), [24u]) == null, "Doom loss leaked.");
        controller.Seek(18); Check(Math.Abs(((DateTime)Value(Group("cross").Single(), "Activation") - Start).TotalSeconds - 42.9) < .1,
            "Observed confluence did not change staff delay to 25.9.");
        controller.Seek(1); Check(Group("cross").Length == 1, "Staff Seek did not rebuild.");
        stop(); Check(previews().Length == 0 && dangers() == 0, "Staff Stop leaked.");

        center = new(0, 0, 60);
        List<string> records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4022, center, combat: true), Actor(10, 0x233C, new(0, 0, 35)),
                Actor(11, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8),
                Actor(20, 0x1EB90E, center, dead: true), Actor(21, 0x1EB90E, center + Vector3.UnitX * 12, eventState: 7)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x4022, center, combat: true, cast: Cast(34813, 2, center))], party: party),
            Frame(territory, 3, [Actor(1, 0x4022, center, combat: true)], party: party),
            Map(4, 0, 0x00020001), Map(4.1, 1, 0x00020001), Map(4.2, 2, 0x00020001),
            Frame(territory, 5, [Actor(10, 0x233C, new(0, 0, 35), cast: Cast(34819, 2, new(0, 0, 35), startedAt: 5))], party: party),
            Frame(territory, 8, [Actor(10, 0x233C, new(0, 0, 35))], party: party),
            Frame(territory, 8.5, [Actor(20, 0x1EB90E, center, dead: true, eventState: 7)], party: party),
            Frame(territory, 9, [Actor(10, 0x233C, center, cast: Cast(35871, 1, center + Vector3.UnitZ * 20, startedAt: 9)),
                Actor(11, 0x233C, center, cast: Cast(35871, 1, center - Vector3.UnitZ * 20, startedAt: 9))], party: party),
            Frame(territory, 11, [Actor(10, 0x233C, center), Actor(11, 0x233C, center)], party: party),
            EffectEvent(12, 1, 34817, 1), EffectEvent(13, 1, 34817, 2), EffectEvent(14, 1, 34820, 3),
            Frame(territory, 15, [Actor(10, 0x233C, center, cast: Cast(34822, 1, center, target: 101, startedAt: 15))], party: party),
            Frame(territory, 17, [Actor(10, 0x233C, center)], party: party),
        ];
        load(id, records);
        controller.Seek(3.5); Check(Group("shrink").Length == 1 && Group("quicksand").Length == 2, "Antlion forecast or whole dead quicksand group missing.");
        controller.Seek(4.5); Check(Group("shrink").Length == 0 && Group("tower").Length == 2 && Group("tower").All(a => !Flag(a, "Navigate")), "Map towers or arena confirmation.");
        controller.Seek(5.5); Check(Group("tower/join").Length == 1 && Group("knockback").Any(a => Flag(a, "Navigate")), "Two-tower/KB navigation missing.");
        controller.Seek(8.7); Check(Group("quicksand").Length == 0 && Group("knockback").Length == 0, "First quicksand gate or KB real ending.");
        controller.Seek(11.5); Check(Group("march").Length == 2 && Group("march").All(a => Flag(a, "Navigate")), "March ending erased pending sequence.");
        controller.Seek(12.5); Check(Group("march").Length == 1, "March effect FIFO missing.");
        controller.Seek(14.5); Check(previews().Length == 0 && dangers() == 0, "Antlion clear leaked.");
        controller.Seek(15.5); Check(Group("stack/join").Length == 1 && Group("stack").Count(a => Flag(a, "Display")) == 1,
            "Cast circle stack not routed to hard movement.");
        controller.Seek(17.5); Check(Group("stack").Length == 0, "Circle stack real ending leaked.");
        controller.Seek(5.5); Check(Group("tower/join").Length == 1, "Antlion Seek failed."); stop();

        center = new(0, 0, -422);
        records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4042, center, combat: true), Actor(10, 0x233C, center),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x4042, center, combat: true, cast: Cast(35011, 1, center))], party: party),
            Frame(territory, 2, [Actor(1, 0x4042, center, combat: true)], party: party),
            Map(3, 10, 0x00020001),
            Frame(territory, 4, Enumerable.Range(20, 20).Select(i => Actor((ulong)i, 0x233C, center,
                cast: Cast(36058, 1, center + Vector3.UnitX * (i - 20), startedAt: 4))), party: party),
            Frame(territory, 6, Enumerable.Range(20, 20).Select(i => Actor((ulong)i, 0x233C, center)), party: party),
        ];
        for (var i = 0; i < 20; ++i) records.Add(EffectEvent(7 + i * .1, 10, 34994, (uint)i + 1));
        records.Add(EffectEvent(10, 10, 14588, 30, target: 101));
        records.Add(Frame(territory, 11, [Actor(10, 0x233C, center - Vector3.UnitZ * 10),
            Actor(101, 0, center + Vector3.UnitX * 10, dead: true)], party: party));
        records.Add(Frame(territory, 12, [], removed: [10], party: party));
        records.Add(EffectEvent(13, 1, 35008, 31, target: 100));
        records.Add(CastEvent(14, 20, 36058, 1, center + Vector3.UnitZ * 5));
        load(id, records);
        controller.Seek(2.5); Check(Group("shrink").Length == 1, "Durante forecast time-cleared.");
        controller.Seek(3.5); Check(Group("shrink").Length == 0, "Durante map did not shrink.");
        controller.Seek(6.5);
        Check(Group("battery").Length == 20 && Group("battery").Count(a => Flag(a, "Navigate")) == 16
            && previews().Length == 20 && dangers() == 16, "Battery all display/first16 navigation missing.");
        controller.Seek(8.5); Check(Group("battery").Length == 4 && dangers() == 4, "Battery FIFO did not promote final batch.");
        controller.Seek(10.5); Check(Group("battery").Length == 0 && Group("line").Count(a => Flag(a, "Display")) == 1
            && Damage().Length == 2 && Damage().All(a => (string)Value(a, "DamageType") == "Shared"), "Event line or aggregated Shared prediction missing.");
        controller.Seek(12.5); Check(Group("line").Count(a => Flag(a, "Display")) == 1
            && (Vector3)Value(Group("line").Single(a => Flag(a, "Display")), "Position") == center - Vector3.UnitZ * 10, "Dynamic line dead target/source disappearance.");
        controller.Seek(13.5); Check(previews().Length == 0 && dangers() == 0 && Damage().Length == 0, "Line resolve leaked.");
        controller.Seek(14.1); Check(Group("battery").Length == 1
            && Math.Abs(((DateTime)Value(Group("battery")[0], "Activation") - Start).TotalSeconds - 20.5) < .1,
            "Real CastStart packet path did not use event current time.");
        controller.Seek(6.5); Check(Group("battery").Length == 20, "Durante Seek did not rebuild.");
        stop(); Check(previews().Length == 0 && dangers() == 0, "Lunar Stop leaked.");
        Console.WriteLine("PASS production Lunar: model baseline/transitions, staff delays/risk/explosion hiding, Doom, map shrink, dead quicksand first gate, tower/KB constraints, delayed marches FIFO, all battery previews/first16 risk, event line dynamic actor lifecycle/Shared prediction, Seek/Stop");
    }
}
