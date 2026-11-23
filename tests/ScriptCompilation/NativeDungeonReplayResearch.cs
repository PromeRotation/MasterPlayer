using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunResearch(Assembly assembly, object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers, Func<int> goals)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.research-facility";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        var center = new Vector3(-110.7f, 0, -295.495f);
        load(id,
        [
            Header(1110),
            Frame(1110, 0, [Actor(1, 0xE97, center, combat: true), Actor(10, 0xE98, center),
                Actor(100, 0, center + Vector3.UnitX * 10), Actor(101, 0, center + Vector3.UnitZ * 5)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            ResearchTetherEvent(.1, 10, 100, 17, true),
            ResearchTetherEvent(.8, 10, 100, 17, false),
        ]);
        controller.Seek(.2);
        Check(previews().Length == 1 && dangers() == 1, "Production research turret tether did not publish true beam and personal avoidance.");
        controller.Seek(.9); Check(previews().Length == 0 && dangers() == 0, "Production research turret cancel leaked.");
        controller.Seek(.2); Check(previews().Length == 1, "Production research turret seek failed."); stop();

        center = new(248.025f, 0, 272);
        load(id,
        [
            Header(1110),
            Frame(1110, 0, [Actor(1, 0xE9A, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 10),
                Actor(101, 0, center)], full: true, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            StatusEvent(.1, 101, 2518, 0, true),
            EffectEvent(6.3, 1, 4335, 1),
            Frame(1110, 7, [Actor(1, 0xE9A, center, combat: true, cast: new { ActionId = 4331, TargetId = 0UL,
                Location = Pose(center), Rotation = 0f, StartedAt = Start.AddSeconds(7), FinishAt = Start.AddSeconds(10.3) })]),
        ]);
        controller.Seek(.2); Check(goals() == 1 && previews().Length == 1, "Production research bind did not build two-player stack.");
        controller.Seek(6.4); Check(goals() == 0 && previews().Length == 0, "Production research stack effect leaked.");
        controller.Seek(7.1); Check(previews().Length == 1 && dangers() == 0, "Production research gaze lost its visual cue.");
        stop();

        center = new(230, 0, -181);
        load(id,
        [
            Header(1110),
            Frame(1110, 0, [Actor(1, 0x3DA4, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x3DA5, center - Vector3.UnitX * 10), Actor(11, 0x3DA5, center + Vector3.UnitX * 10),
                Actor(12, 0x3DA5, center + Vector3.UnitZ * 10)], full: true),
            ResearchTetherEvent(.1, 10, 11, 110, true),
            EffectEvent(10.7, 10, 33019, 1),
        ]);
        controller.Seek(.2);
        Check(previews().Length == 2 && dangers() == 2, "Production research fused star or remaining small star missing.");
        controller.Seek(10.8); Check(previews().Length == 0 && dangers() == 0, "Production research fused resolution leaked.");
        controller.Seek(.2); Check(dangers() == 2, "Production research fused seek failed."); stop();

        foreach (var iceFirst in new[] { true, false })
        {
            center = new(230, 0, 79);
            var firstAction = iceFirst ? 33021u : 33022u;
            var records = new List<string>
            {
                Header(1110),
                Frame(1110, 0, [Actor(1, 0x3DA7, center, combat: true), Actor(100, 0, center),
                    Actor(10, 0x3DA8, new(230, 0, iceFirst ? 86 : 92)),
                    Actor(11, 0x233C, center), Actor(12, 0x233C, center)], full: true),
                EffectEvent(11.8, 11, firstAction, 1),
            };
            if (!iceFirst) records.Add(EffectEvent(11.8, 12, firstAction, 2));
            records.Add(Frame(1110, 17, []));
            load(id, records);
            controller.Seek(.2); Check(dangers() == (iceFirst ? 1 : 2), "Production research Dualstar first wave order differs.");
            controller.Seek(11.9); Check(dangers() == (iceFirst ? 2 : 1), "Production research effect-only Dualstar did not advance.");
            controller.Seek(17.01); Check(dangers() == 0 && previews().Length == 0, "Production research missing final resolve leaked stars.");
            controller.Seek(.2); Check(dangers() == (iceFirst ? 1 : 2), "Production research Dualstar seek failed."); stop();
        }
        center = new(230, 0, 79);
        load(id,
        [
            Header(1110),
            Frame(1110, 0, [Actor(1, 0x3DA7, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            ResearchIconEvent(.1, 101, 384), ResearchIconEvent(.1, 101, 161),
            ResearchTetherEvent(.1, 100, 101, 9, true),
            ResearchTetherEvent(1, 100, 101, 9, false),
            EffectEvent(5.2, 1, 31904, 1), EffectEvent(8.2, 1, 31901, 2),
        ]);
        controller.Seek(.2); Check(goals() == 2 && dangers() == 1 && previews().Length == 3, "Production research icon goals or BurningChains missing.");
        controller.Seek(1.1); Check(dangers() == 0, "Production research chain cancel leaked.");
        controller.Seek(8.3); Check(goals() == 0 && previews().Length == 0, "Production research Ancient icon resolves leaked."); stop();
        Console.WriteLine("PASS production native dungeons: ResearchFacility four bosses, laser, bind, gaze, fused stars, both Dualstar orders, icon stacks, chains, missing resolves and seek");

        foreach (var (boss, caster, action, position) in new[]
        {
            (0xFA5u, 0xFA4u, 609u, new Vector3(-176, 0, -372)),
            (0xFA3u, 0xFA3u, 33030u, new Vector3(-176, 0, -372)),
            (0x1350u, 0xF4Bu, 866u, new Vector3(-176, 0, -296)),
            (0xF54u, 0xF55u, 4825u, new Vector3(-360, 0, -250)),
            (0xF58u, 0xF58u, 1260u, new Vector3(227, 0, 226)),
            (0xF56u, 0xF56u, 900u, new Vector3(267, 0, 238)),
            (0xF4Du, 0xF50u, 4464u, new Vector3(28, 0, 210)),
        })
        {
            var source = boss == caster ? 1UL : 10UL;
            var actors = new List<object> { Actor(1, boss, position, combat: true), Actor(100, 0, position + Vector3.UnitZ * 5),
                Actor(101, 0, position + Vector3.UnitX * 10) };
            if (source != 1) actors.Add(Actor(source, caster, position));
            load(id,
            [
                Header(1110), Frame(1110, 0, actors, full: true,
                    party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
                Frame(1110, .1, [Actor(source, caster, position, combat: source == 1, cast: Cast(action, 3, position, target: 101))]),
                Frame(1110, 3.1, [Actor(source, caster, position, combat: source == 1)]),
            ]);
            controller.Seek(.2); Check(dangers() == 1 && previews().Length == 1, $"Production research trash {boss:X} did not draw and navigate.");
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Production research trash not active.");
            controller.Seek(3.2); Check(dangers() == 0 && previews().Length == 0, "Production research trash finish leaked.");
            controller.Seek(.2); Check(dangers() == 1, "Production research trash seek failed."); stop();
        }
        center = new(28.122f, 0, 220.05f);
        load(id,
        [
            Header(1110),
            Frame(1110, 0, [Actor(1, 0xF4D, center), Actor(100, 0, center - Vector3.UnitZ * 10),
                Actor(10, 0x1E9934, center)], full: true),
            Frame(1110, 1, [Actor(10, 0x1E9934, center, eventAnimation: 0x00040008)]),
        ]);
        controller.Seek(.2);
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary != null && !(bool)boundary.Contains(center, 0f), "Production entry pull lost initial hole.");
        controller.Seek(1.1);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((bool)boundary.Contains(center, 0f), "Production EAnim did not fill node hole.");
        controller.Seek(.2);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(center, 0f), "Production backward seek retained a future hole closure."); stop();
        Console.WriteLine("PASS production native dungeons: ResearchFacility seven trash modules, cast cancellation, both views, entry pull, hole closure and backward seek");
    }

    private static string ResearchTetherEvent(double t, ulong source, ulong target, uint tether, bool present)
        => JsonSerializer.Serialize(new { t, kind = present ? "Tether" : "TetherCancel",
            type = "PromeRotation.LogSystem.LogSystem" + (present ? "Tether" : "TetherCancel") + "Event",
            data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(t) } });

    private static string ResearchIconEvent(double t, ulong target, uint icon)
        => JsonSerializer.Serialize(new { t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
}
