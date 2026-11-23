using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunIhuykatumu(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.ihuykatumu";
        const uint territory = 1167;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("ihuy/" + key)).ToArray();
        string Tether(double t, bool present) => JsonSerializer.Serialize(new { t, kind = present ? "Tether" : "TetherCancel",
            type = "PromeRotation.LogSystem.LogSystemTether" + (present ? "" : "Cancel") + "Event",
            data = new { SourceId = 20UL, TargetId = 101UL, TetherId = 17, Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(35, 0, -95);
        List<string> records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4190, center, combat: true), Actor(10, 0x233C, new(15.4f, 0, -95)),
                Actor(11, 0x233C, center), Actor(12, 0x233C, center), Actor(20, 0x4192, center - Vector3.UnitZ * 10),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, [Actor(10, 0x233C, new(15.4f, 0, -95), cast: Cast(36494, 1, new(15.4f, 0, -95)))], party: party),
            Frame(territory, 2, [Actor(10, 0x233C, new(15.4f, 0, -95))], party: party),
        ];
        for (var i = 0; i < 6; ++i) records.Add(EffectEvent(3 + i, 10, 36496, (uint)i + 1));
        records.Add(Frame(territory, 9, [Actor(10, 0x233C, center, cast: Cast(36497, 1, center, startedAt: 9)),
            Actor(11, 0x233C, center, cast: Cast(36505, 1, center + Vector3.UnitX * 4, startedAt: 9)),
            Actor(12, 0x233C, center, cast: Cast(36500, 1, center + Vector3.UnitX * 8, startedAt: 9))], party: party));
        records.Add(Frame(territory, 11, [Actor(12, 0x233C, center)], party: party));
        records.Add(Frame(territory, 12, [Actor(10, 0x233C, center), Actor(11, 0x233C, center)], party: party));
        records.Add(Frame(territory, 13, [Actor(10, 0x233C, center, cast: Cast(36515, 1, center, startedAt: 13))], party: party));
        records.Add(Frame(territory, 15, [Actor(10, 0x233C, center), Actor(11, 0x233C, center, cast: Cast(36516, 1, center, startedAt: 15))], party: party));
        records.Add(Frame(territory, 17, [Actor(11, 0x233C, center), Actor(12, 0x233C, center, cast: Cast(36517, 1, center, startedAt: 17))], party: party));
        records.Add(Frame(territory, 19, [Actor(12, 0x233C, center)], party: party));
        records.Add(Tether(20, true)); records.Add(Tether(21, false));
        records.Add(Frame(territory, 21.2, [Actor(101, 0, center + Vector3.UnitX * 10)], party: party));
        records.Add(EffectEvent(22, 10, 36512, 30));
        load(id, records);
        controller.Seek(.5); Check(previews().Length == 2 && Group("inhale").Length == 1, "Separate Resurface/Inhale missing.");
        controller.Seek(2.5); Check(previews().Length == 1, "Resurface real ending cleared fixed inhale.");
        controller.Seek(7.5); Check(Group("inhale").Length == 1, "Inhale cleared before sixth actual hit.");
        controller.Seek(8.5); Check(previews().Length == 0, "Sixth inhale failed.");
        controller.Seek(9.5); Check(Group("bury").Length == 3 && dangers() == 3, "Bury all three risk/previews.");
        controller.Seek(11.5); Check(Group("bury").Length == 2 && (Vector3)Value(Group("bury")[0], "Position") == center + Vector3.UnitX * 4,
            "Bury removed caster instead of FIFO.");
        controller.Seek(14); Check(Group("shore").Length == 1 && Value(Group("shore")[0], "Shape").GetType().Name == "CircleTemplate", "Shore first stage.");
        controller.Seek(16); Check(Value(Group("shore").Single(), "Shape").GetType().Name == "DonutTemplate", "Shore first finish did not advance.");
        controller.Seek(19.5); Check(Group("shore").Length == 0 && dangers() == 0, "Shore final real finish.");
        controller.Seek(21.5); Check(Group("bait").Length == 2 && MathF.Abs((float)Value(Group("bait").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Rotation")
            - MathF.PI / 4) < .00001f, "Tether cancel dropped bait or dynamic heading failed.");
        controller.Seek(22.5); Check(previews().Length == 0 && dangers() == 0, "Actual tether hit not cleared.");
        controller.Seek(9.5); Check(Group("bury").Length == 3, "Prime Punutiy Seek failed."); stop();

        center = new(80, 0, 53);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4195, center, combat: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, [Actor(10, 0x419C, center, cast: Cast(36479, 1, center)),
                Actor(11, 0x419C, center, cast: Cast(36482, 3, center)),
                Actor(12, 0x419B, center, cast: Cast(36522, 1, center, target: 101)),
                Actor(13, 0x4199, center, cast: Cast(36485, 1, center))], party: party),
            Frame(territory, 2, [Actor(10, 0x419C, center), Actor(12, 0x419B, center), Actor(13, 0x4199, center)], party: party),
            Frame(territory, 4, [Actor(11, 0x419C, center)], party: party),
        ]);
        controller.Seek(.5); Check(previews().Length == 4 && Group("spread").Length == 2, "Drowsie helper shapes/spread missing.");
        controller.Seek(2.5); Check(previews().Length == 1 && dangers() == 1, "Independent vine ending lost other.");
        controller.Seek(4.5); Check(previews().Length == 0 && dangers() == 0, "Drowsie finish leaked."); stop();

        center = new(-107, 0, 265);
        records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4165, center, combat: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            Frame(territory, .1, [Actor(20, 0x416C, new(-121, 0, 279), rotation: .7f, dead: true),
                Actor(10, 0x233C, center, cast: Cast(36349, 1, center, target: 101))], party: party),
            EffectEvent(2, 10, 36349, 1, target: 101),
            Frame(territory, 2.1, [Actor(10, 0x233C, center)], party: party),
            Frame(territory, 3, [Actor(30, 0x1EBA21, center + Vector3.UnitX * 8, dead: true)], party: party),
            Frame(territory, 4, [Actor(30, 0x1EBA21, center + Vector3.UnitX * 8, eventState: 7, dead: true)], removed: [20], party: party),
        ];
        for (var i = 0; i < 12; ++i) records.Add(EffectEvent(5 + i, 1, 36360, (uint)i + 2));
        records.Add(Frame(territory, 18, [Actor(10, 0x233C, center, cast: Cast(36357, 1, center, target: 101, startedAt: 18))], party: party));
        records.Add(Frame(territory, 20, [Actor(10, 0x233C, center)], party: party));
        load(id, records);
        controller.Seek(.5); Check(Group("cutting").Length == 4 && Group("whirlwind").Length == 6
            && Group("whirlwind").Count(a => (bool)Value(a, "Obstacle")) == 1, "Whirlwind full moving obstacles/predictions missing.");
        controller.Seek(2.5); Check(Group("spark/event").Length == 1 && Group("spark/cast").Length == 0, "Spark event delay handoff.");
        controller.Seek(3.5); Check(Group("spark/event").Length == 0 && Group("spark/object").Length == 1, "Actual lightning did not consume prediction.");
        controller.Seek(4.5); Check(Group("whirlwind").Length == 0 && Group("spark").Length == 0 && Group("cutting").Length == 4,
            "Whirlwind removal erased pending cutting or inactive spark retained.");
        controller.Seek(8.5); Check(Group("cutting").All(a => Math.Abs(((DateTime)Value(a, "Activation") - Start).TotalSeconds - 16.8) < .1),
            "Four cutting effects failed second group promotion.");
        controller.Seek(16.5); Check(previews().Length == 0 && dangers() == 0, "Twelfth cutting not consumed.");
        controller.Seek(18.5); Check(Group("buster").Length == 1 && dangers() == 1, "Area tankbuster not rendered/planned.");
        controller.Seek(20.5); Check(previews().Length == 0 && dangers() == 0, "Buster ending leaked.");
        controller.Seek(.5); Check(Group("cutting").Length == 4, "Apollyon Seek failed.");
        stop(); Check(previews().Length == 0 && dangers() == 0, "Ihuykatumu Stop leaked.");
        Console.WriteLine("PASS production Ihuykatumu: separate six-hit inhale, Bury all-risk FIFO/colors, event-ended concentric stages, tether dynamic/cancel retention, vine/spread independence, dead moving whirlwind/capsule/obstacles and twelve cutting FIFO, lightning handoff, area buster, Seek/Stop");
    }
}
