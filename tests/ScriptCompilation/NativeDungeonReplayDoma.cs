using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDoma(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.doma-castle";
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
        var center = new Vector3(124.64f, 0, 17.54f);
        load(id,
        [
            Header(1144), Frame(1144, 0, [Actor(1, 0x1BCC, center, combat: true, targetId: 101),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 8),
                Actor(10, 0x1BCE, center + Vector3.UnitX * 8, rotation: MathF.PI / 2)], full: true, party: party),
            Frame(1144, 1, [Actor(1, 0x1BCC, center, combat: true, targetId: 101,
                cast: Cast(8350, 2, center, startedAt: 1))], party: party),
            Frame(1144, 3, [Actor(1, 0x1BCC, center, combat: true, targetId: 101)], removed: [10], party: party),
        ]);
        controller.Seek(.2);
        Check(previews().Length == 2 && dangers() == 5, "Production Doma moving mine/cleave missing.");
        Check(Aoes().Count(a => AoeId(a).Contains("/future/")) == 4, "Production Doma mine future windows missing.");
        controller.Seek(1.2); Check(previews().Length == 1 && dangers() == 4, "Production Doma cleave persisted during visual.");
        controller.Seek(3.2); Check(previews().Length == 1 && dangers() == 1, "Production Doma mine removal/cleave restore failed.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Doma mine seek failed."); stop();

        center = new(-240, 0, 130.5f);
        load(id,
        [
            Header(1144), Frame(1144, 0, [Actor(1, 0x1BD0, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 10), Actor(10, 0x1BD2, center, rotation: MathF.PI / 2),
                Actor(11, 0x1BD1, center - Vector3.UnitX * 10)], full: true, party: party),
            DomaTetherEvent(.1, 10, true), ResearchIconEvent(.1, 101, 62),
            Frame(1144, .2, [Actor(10, 0x1BD2, center, rotation: MathF.PI / 2,
                tethers: [new { Id = 60, TargetId = 11UL }]),
                Actor(11, 0x1BD1, center - Vector3.UnitX * 10, cast: Cast(8357, 8, center - Vector3.UnitX * 10, startedAt: .2))], party: party),
            DomaTetherEvent(2, 10, false), Frame(1144, 2, [Actor(10, 0x1BD2, center)], party: party),
            EffectEvent(3, 1, 8355, 1, target: 101),
            Frame(1144, 8.2, [Actor(11, 0x1BD1, center - Vector3.UnitX * 10)], party: party),
        ]);
        controller.Seek(.3); Check(previews().Length == 3 && dangers() == 3, "Production Doma chain/stack/tower missing.");
        var chain = Aoes().Single(a => AoeId(a).StartsWith("doma/chain/"));
        Check(Math.Abs((Activation(chain) - Start.AddSeconds(5.7)).TotalMilliseconds) < 1, "Production Doma tether clock shifted.");
        controller.Seek(2.2); Check(previews().Length == 2 && dangers() == 2, "Production Doma untether retained chain.");
        controller.Seek(3.2); Check(previews().Length == 1 && dangers() == 1, "Production Doma stack effect failed.");
        controller.Seek(8.3); Check(previews().Length == 0 && dangers() == 0, "Production Doma tower finish leaked.");
        controller.Seek(.3); Check(previews().Length == 3, "Production Doma Hexadrone seek failed."); stop();

        center = new(-240, 0, -198);
        List<string> records =
        [
            Header(1144), Frame(1144, 0, [Actor(1, 0x1BD4, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1BD6, center), Actor(11, 0x1BD5, center - Vector3.UnitX * 15),
                Actor(12, 0x1BD5, center + Vector3.UnitX * 15)], full: true, party: party),
            DomaTetherEvent(.1, 10, true),
            Frame(1144, .2, [Actor(10, 0x1BD6, center, tethers: [new { Id = 60, TargetId = 11UL }]),
                Actor(11, 0x1BD5, center - Vector3.UnitX * 15, cast: Cast(8367, 10, center - Vector3.UnitX * 15, startedAt: .2)),
                Actor(12, 0x1BD5, center + Vector3.UnitX * 15, cast: Cast(8367, 10, center + Vector3.UnitX * 15, startedAt: .2))], party: party),
            Frame(1144, 2, [Actor(12, 0x1BD5, center + Vector3.UnitX * 15)], party: party),
            DomaTetherEvent(3, 10, false), Frame(1144, 3, [Actor(10, 0x1BD6, center),
                Actor(11, 0x1BD5, center - Vector3.UnitX * 15)], party: party),
        ];
        load(id, records);
        controller.Seek(.3); Check(previews().Length == 3 && dangers() == 2, "Production Doma two bombs must retain chain drawing only.");
        controller.Seek(2.2); Check(previews().Length == 2 && dangers() == 2, "Production Doma single bomb failed to restore chain navigation.");
        controller.Seek(3.2); Check(previews().Length == 0 && dangers() == 0, "Production Doma bombs/chain leaked."); stop();
        records =
        [
            Header(1144), Frame(1144, 0, [Actor(1, 0x1BD4, center, combat: true), Actor(100, 0, center)], full: true, party: party),
            Frame(1144, .1, [Actor(1, 0x1BD4, center, combat: true, cast: Cast(8360, 2, center))], party: party),
            EffectEvent(2.1, 1, 8360, 1), Frame(1144, 2.1, [Actor(1, 0x1BD4, center, combat: true)], party: party),
            EffectEvent(2.2, 1, 8360, 1),
        ];
        for (uint i = 2; i <= 5; ++i) records.Add(EffectEvent(2 + i, 11, 8361, i));
        load(id, records);
        controller.Seek(2.3); Check(previews().Length == 1 && dangers() == 1, "Production Doma saw ended after first hit.");
        controller.Seek(6.1); Check(previews().Length == 1, "Production Doma saw duplicate advanced count.");
        controller.Seek(7.1); Check(previews().Length == 0 && dangers() == 0, "Production Doma fifth saw leaked.");
        controller.Seek(2.3); Check(previews().Length == 1, "Production Doma saw seek failed."); stop();
        load(id,
        [
            Header(1144), Frame(1144, 0, [Actor(1, 0x1BD4, center, combat: true),
                Actor(100, 0, center + Vector3.UnitZ * 8), Actor(101, 0, center + new Vector3(.5f, 0, 8)),
                Actor(10, 0x1BD8, center - Vector3.UnitX * 15)], full: true, party: party),
            EffectEvent(.1, 1, 8362, 1, target: 101), ResearchIconEvent(.1, 100, 99),
            Frame(1144, .2, [Actor(100, 0, center + Vector3.UnitX * 8, stackedStatus: 1253, statusExpiresAt: Start.AddSeconds(9)),
                Actor(10, 0x1BD8, center - Vector3.UnitX * 15, cast: Cast(8369, 6, center + Vector3.UnitX * 15, startedAt: .2))], party: party),
            StatusEvent(1, 100, 1253, 0, false), Frame(1144, 1.1, [], party: party),
            EffectEvent(2, 11, 8365, 2, target: 100),
            EffectEvent(3, 1, 8363, 3), EffectEvent(4, 1, 8363, 4), EffectEvent(5, 1, 8363, 5), EffectEvent(6, 1, 8363, 6),
            Frame(1144, 6.2, [Actor(10, 0x1BD8, center - Vector3.UnitX * 15)], party: party),
        ]);
        controller.Seek(.3); Check(previews().Length == 4, "Production Doma gunsaw/prey/spread/charge missing.");
        Check(Aoes().Any(a => AoeId(a) == "doma/gunsaw/party/101") &&
            Aoes().Any(a => AoeId(a) == "doma/prey/edge"), "Production Doma bait navigation missing.");
        controller.Seek(1.2); Check(!Aoes().Any(a => AoeId(a).StartsWith("doma/prey")), "Production Doma stale prey resurrected.");
        controller.Seek(2.2); Check(previews().Length == 2, $"Production Doma spread effect did not clear: {previews().Length}; {string.Join(", ", Aoes().Select(AoeId))}.");
        controller.Seek(5.2); Check(previews().Length == 2, "Production Doma gunsaw cleared before fourth rest.");
        controller.Seek(6.1); Check(previews().Length == 1, "Production Doma fourth rest retained gunsaw.");
        controller.Seek(6.3); Check(previews().Length == 0 && dangers() == 0, "Production Doma final charge leaked.");
        controller.Seek(.3); Check(previews().Length == 4, "Production Doma Grynewaht seek failed."); stop();
        Console.WriteLine("PASS native Doma three bosses, moving mine windows, tether timing, stack, towers, double bombs, five saw effects, gunsaw heading, prey status, charge, seek and stop");
    }

    private static string DomaTetherEvent(double t, ulong source, bool present)
        => JsonSerializer.Serialize(new { t, kind = present ? "Tether" : "TetherCancel",
            type = "PromeRotation.LogSystem.LogSystemTether" + (present ? "" : "Cancel") + "Event",
            data = new { SourceId = source, TargetId = 11, TetherId = 60, Timestamp = Start.AddSeconds(t) } });
}
