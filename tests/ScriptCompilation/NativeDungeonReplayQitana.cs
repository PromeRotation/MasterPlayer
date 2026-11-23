using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunQitana(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.qitana";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        foreach (var z in new[] { 640f, 434.5f })
        {
            var center = new Vector3(0, 0, z);
            load(id,
            [
                Header(823), Frame(823, 0, [Actor(1, 0x2A40, center, combat: true), Actor(100, 0, center),
                    Actor(101, 0, center + Vector3.UnitZ * 8), Actor(10, 0x28E8, new(-17, 0, z > 550 ? 627 : 436), rotation: .7f)],
                    full: true, party: party),
                ResearchTetherEvent(.1, 10, 1, 37, true),
                Frame(823, .2, [Actor(10, 0x28E8, new(-17, 0, z > 550 ? 627 : 436), rotation: .7f,
                    tethers: [new { Id = 37, TargetId = 1UL }])], party: party),
                EffectEvent(6.1, 10, 15917, 1), Frame(823, 7, [], party: party),
            ]);
            controller.Seek(.3); Check(previews().Length == 1 && dangers() == 1, "Production Qitana statue beam missing.");
            dynamic arena = Encounter().GetType().GetProperty("Arena")!.GetValue(Encounter())!;
            Check(!(bool)arena.Contains(new Vector3(z > 550 ? -5 : 5, 0, z > 550 ? 643 : 436), 0f),
                "Production Qitana pillar hole filled.");
            controller.Seek(6.2); Check(previews().Length == 0 && dangers() == 0, "Production Qitana statue revived from frame.");
            controller.Seek(.3); Check(previews().Length == 1, "Production Qitana statue seek failed."); stop();
        }
        var lozatl = new Vector3(0, 0, 315);
        load(id,
        [
            Header(823), Frame(823, 0, [Actor(1, 0x27AF, lozatl, combat: true), Actor(100, 0, lozatl),
                Actor(101, 0, lozatl + Vector3.UnitZ * 8), Actor(10, 0x1EAB00, new(10, 0, 315))], full: true, party: party),
            Frame(823, .1, [Actor(10, 0x1EAB00, new(10, 0, 315), eventAnimation: 0x00040008),
                Actor(1, 0x27AF, lozatl, combat: true, cast: Cast(15504, 4, lozatl, -.6f))], party: party),
            Frame(823, 4.2, [Actor(1, 0x27AF, lozatl, combat: true)], party: party),
            EffectEvent(8.1, 20, 15500, 1), Frame(823, 9, [], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() == 2, "Production Qitana fury/light overlap missing.");
        controller.Seek(4.3); Check(previews().Length == 1 && dangers() == 1, "Production Qitana light lost with fury finish.");
        controller.Seek(8.2); Check(previews().Length == 0 && dangers() == 0, "Production Qitana light animation revived.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Qitana Lozatl seek failed."); stop();

        var bat = new Vector3(62, 0, -35);
        load(id,
        [
            Header(823), Frame(823, 0, [Actor(1, 0x27B0, bat, combat: true), Actor(100, 0, bat), Actor(101, 0, bat + Vector3.UnitZ * 8),
                Actor(10, 0x1EAACD, bat, rotation: .5f), Actor(20, 0x233C, bat)], full: true, party: party),
            Frame(823, 9, [Actor(20, 0x233C, bat, cast: Cast(15512, 3, bat + Vector3.UnitX * .3f, -.7f, startedAt: 9))], party: party),
            Frame(823, 12.1, [Actor(20, 0x233C, bat)], party: party),
            Frame(823, 13, [], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 1 && dangers() == 1, "Production Qitana preliminary pillar missing.");
        controller.Seek(9.1); Check(previews().Length == 1 && Aoes().Single().GetType().GetProperty("Position")!.GetValue(Aoes().Single()) is Vector3 p
            && Math.Abs(p.X - 62.3f) < .001f, "Production Qitana tower handoff duplicated or lost corrected location.");
        controller.Seek(12.2); Check(previews().Length == 0 && dangers() == 0, "Production Qitana pillar natural finish revived.");
        controller.Seek(9.1); Check(previews().Length == 1, "Production Qitana pillar seek failed."); stop();

        var eros = new Vector3(17, 0, -538);
        load(id,
        [
            Header(823), Frame(823, 0, [Actor(1, 0x27B1, eros - Vector3.UnitZ * 12, combat: true), Actor(100, 0, eros),
                Actor(101, 0, eros + Vector3.UnitZ * 8), Actor(10, 0x233C, eros)], full: true, party: party),
            ResearchTetherEvent(.1, 1, 100, 57, true), ResearchTetherEvent(.2, 1, 100, 57, false),
            ResearchTetherEvent(.2, 1, 100, 1, true),
            Frame(823, 1, [Actor(10, 0x233C, eros, cast: Cast(15517, 6, eros, target: 100, startedAt: 1))], party: party),
            EffectEvent(5.3, 1, 17079, 1, target: 100),
            Frame(823, 7.1, [Actor(10, 0x233C, eros)], party: party),
            EffectEvent(7.2, 10, 15518, 2, new(9, 0, -518.5f)),
            Frame(823, 8, [Actor(20, 0x1E972C, new(9, 0, -518.5f)),
                Actor(1, 0x27B1, eros - Vector3.UnitZ * 12, combat: true, cast: Cast(15520, 4, eros - Vector3.UnitZ * 12, startedAt: 8))], party: party),
            Frame(823, 12.1, [Actor(1, 0x27B1, eros - Vector3.UnitZ * 12, combat: true), Actor(20, 0x1E972C, new(9, 0, -518.5f), eventState: 7)], party: party),
            Frame(823, 13, [Actor(10, 0x233C, eros, cast: Cast(15525, 6, eros, target: 101, startedAt: 13))], party: party),
            Frame(823, 19.1, [Actor(10, 0x233C, eros)], party: party),
        ]);
        controller.Seek(.3); Check(Aoes().Count(a => AoeId(a).StartsWith("qitana/hound")) == 2, "Production Qitana tether missing.");
        controller.Seek(1.1); Check(Aoes().Any(a => AoeId(a).StartsWith("qitana/bait/edge")), "Production Qitana bait edge missing.");
        controller.Seek(5.4); Check(!Aoes().Any(a => AoeId(a).StartsWith("qitana/hound")), "Production Qitana resolved tether leaked.");
        controller.Seek(7.3); Check(previews().Length == 1, "Production Qitana poison prediction missing.");
        controller.Seek(8.1); Check(Aoes().Count(a => AoeId(a).StartsWith("qitana/poison")) == 1
            && Aoes().Any(a => AoeId(a) == "qitana/displacement/1"), "Production Qitana poison/knockback handoff missing.");
        controller.Seek(12.2); Check(previews().Length == 0 && dangers() == 0, "Production Qitana poison state/knockback cleanup failed.");
        controller.Seek(13.1); Check(Aoes().Any(a => AoeId(a).StartsWith("qitana/stack/enter")), "Production Qitana forced stack missing.");
        controller.Seek(19.2); Check(previews().Length == 0 && dangers() == 0, "Production Qitana stack cleanup failed.");
        controller.Seek(8.1); Check(Aoes().Any(a => AoeId(a) == "qitana/displacement/1"), "Production Qitana Eros seek failed."); stop();
        Console.WriteLine("PASS native Qitana two Dreamer rooms, Lozatl overlap, pillar precise handoff, Eros tether/bait/poison/knockback/forced stack, seek and stop");
    }
}
