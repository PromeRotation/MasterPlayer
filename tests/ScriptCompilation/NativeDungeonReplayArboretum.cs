using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunArboretum(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.arboretum";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter()
        {
            var script = Script();
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        ulong[] Forbidden() => (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
        var center = new Vector3(0, 0, -82.146f);
        foreach (var sign in new[] { -1, 1 })
        {
            var records = new List<string>
            {
                Header(511),
                Frame(511, 0, [Actor(1, 0x142F, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 10),
                    Actor(10, 0x14C5, center)], full: true),
                Frame(511, .1, [Actor(10, 0x14C5, center, cast: Cast(5230, 5, center))]),
                Frame(511, 5.1, [Actor(10, 0x14C5, center)]),
                EffectEvent(5.1, 10, 5230, 1, rotation: 0),
                EffectEvent(6.1, 10, 5225, 2, rotation: .00001f),
                EffectEvent(7.1, 10, 5225, 3, rotation: sign * 11.6f * MathF.PI / 180),
            };
            for (var i = 0; i < 12; ++i)
                records.Add(EffectEvent(8.1 + i, 10, 5225, (uint)(4 + i), rotation: sign * (i + 2) * 11.6f * MathF.PI / 180));
            load(id, records);
            controller.Seek(.2); Check(previews().Length == 3 && dangers() == 3, "Production arboretum initial ambiguous rotation missing.");
            controller.Seek(5.2); Check(previews().Length == 2, "Production arboretum first effect lost ambiguous follow-ups.");
            controller.Seek(7.2); Check(previews().Length == 8 && dangers() == 8, "Production arboretum resolved rotation did not expose eight predictions.");
            controller.Seek(19.3); Check(previews().Length == 0 && dangers() == 0, "Production arboretum final rotation leaked.");
            controller.Seek(7.2); Check(dangers() == 8, "Production arboretum rotation backward seek failed."); stop();
        }
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x142F, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x14C6, center, modelState: 1)], full: true),
            Frame(511, .1, [Actor(10, 0x14C6, center, modelState: 1, cast: Cast(5316, 5, center))]),
            Frame(511, 5.1, [Actor(10, 0x14C6, center, modelState: 1)]),
            Frame(511, 8, [Actor(10, 0x14C6, center)]),
        ]);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() == 2, "Production arboretum cone and persistent line did not coexist.");
        controller.Seek(5.2); Check(previews().Length == 1 && dangers() == 1, "Production arboretum cone finish lost remaining line.");
        controller.Seek(8.1); Check(previews().Length == 0 && dangers() == 0, "Production arboretum model reset retained line."); stop();

        center = new(-268, 0, -134);
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x1432, center, combat: true, targetId: 101), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitZ * 5), Actor(10, 0x1433, center + Vector3.UnitX * 10)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            StatusEvent(.1, 1, 152, 0, true),
            Frame(511, .2, [Actor(1, 0x1432, center, combat: true, stackedStatus: 152, targetId: 101)]),
            StatusEvent(1, 1, 152, 0, false),
            Frame(511, 1.1, [Actor(1, 0x1432, center, combat: true, targetId: 101)]),
            EffectEvent(2, 1, 5237, 1, position: center + Vector3.UnitX * 5),
            Frame(511, 3, [Actor(11, 0x1E8FCA, center + Vector3.UnitX * 5)]),
            Frame(511, 4, [Actor(11, 0x1E8FCA, center + Vector3.UnitX * 5, eventState: 7)]),
        ]);
        controller.Seek(.3); Check(Forbidden().SequenceEqual([1UL]) && dangers() == 1 && previews().Length == 1,
            "Production arboretum shield ban or cleave missing.");
        controller.Seek(1.2); Check(Forbidden().Length == 0 && dangers() == 0, "Production arboretum shield removal retained target ban.");
        controller.Seek(2.2); Check(previews().Length == 1 && dangers() == 1, "Production arboretum poison prediction missing.");
        controller.Seek(3.1); Check(previews().Length == 1, "Production arboretum poison spawned twice.");
        controller.Seek(4.1); Check(dangers() == 0 && previews().Length == 0, "Production arboretum poison removal leaked."); stop();

        center = Vector3.Zero;
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x1434, center, combat: true, targetId: 100), Actor(100, 0, center + Vector3.UnitX * 5),
                Actor(101, 0, center + Vector3.UnitZ * 5), Actor(10, 0x1435, center)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            Frame(511, .1, [Actor(1, 0x1434, center, combat: true, targetId: 100, cast: Cast(5216, 4, center))]),
            ResearchIconEvent(.2, 101, 43),
            EffectEvent(3.3, 1, 5218, 1),
            Frame(511, 4.1, [Actor(1, 0x1434, center, combat: true, targetId: 100), Actor(10, 0x1435, center, animationState1: 1)]),
            Frame(511, 15, [Actor(10, 0x1435, center)]),
        ]);
        controller.Seek(.3); Check(previews().Length == 2 && dangers() == 1, "Production arboretum gaze/spread output missing.");
        controller.Seek(3.8); Check(previews().Length == 1 && dangers() == 0, "Production arboretum spread resolve leaked.");
        var script = Script();
        var hints = script.GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
            "Production arboretum offline gaze published PR facing.");
        controller.Seek(4.2); Check(previews().Length == 1 && dangers() == 1, "Production arboretum bulb transition missing.");
        controller.Seek(15.1); Check(previews().Length == 0 && dangers() == 0, "Production arboretum bulb reset leaked.");
        controller.Seek(4.2); Check(dangers() == 1, "Production arboretum bulb backward seek failed."); stop();
        bool ArenaIs(string method, string className = "ArboretumArenas")
        {
            dynamic expected = Script().GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons." + className)!
                .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [0f])!;
            dynamic actual = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            return actual != null && actual.GetBounds().Equals(expected.GetBounds());
        }
        foreach (var (boss, caster, action, position) in new[]
        {
            (0x1423u, 0x1422u, 417u, new Vector3(-115, 0, -100)),
            (0x1423u, 0x146Eu, 4526u, new Vector3(-115, 0, -100)),
            (0x142Au, 0x142Cu, 5353u, new Vector3(-110, 0, 30)),
            (0x142Au, 0x142Bu, 5350u, new Vector3(-110, 0, 30)),
            (0x142Au, 0x142Du, 4701u, new Vector3(-110, 0, 30)),
            (0x142Au, 0x142Cu, 5352u, new Vector3(-110, 0, 30)),
        })
        {
            load(id,
            [
                Header(511),
                Frame(511, 0, [Actor(1, boss, position, combat: true), Actor(100, 0, position),
                    Actor(10, caster, position)], full: true),
                Frame(511, .1, [Actor(10, caster, position, cast: Cast(action, 3, position))]),
                Frame(511, 3.1, [Actor(10, caster, position)]),
            ]);
            controller.Seek(.2); Check(dangers() == 1 && previews().Length == 1, "Production Arboretum trash cast missing: " + action);
            controller.Seek(3.2); Check(dangers() == 0 && previews().Length == 0, "Production Arboretum trash cast leaked: " + action); stop();
        }
        center = new(-115, 0, -100);
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x1423, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1E9DFA, center), Actor(11, 0x1E9DFB, center)], full: true),
            Frame(511, 1, [Actor(10, 0x1E9DFA, center, eventAnimation: 0x00040008)]),
            Frame(511, 2, [Actor(100, 0, new(-120, 0, -190))]),
            Frame(511, 3, [Actor(100, 0, new(-120, 0, -180))]),
            Frame(511, 4, [Actor(11, 0x1E9DFB, center, eventAnimation: 0x00040008)]),
            Frame(511, 5, [Actor(100, 0, new(-268, 0, -134))]),
        ]);
        controller.Seek(.2); Check(ArenaIs("AntFirst"), "Production Ant initial arena differs.");
        controller.Seek(1.1); Check(ArenaIs("AntFirstOpen"), "Production Ant first wall did not open.");
        controller.Seek(2.1); Check(ArenaIs("AntSecond"), "Production Ant corridor did not select second arena.");
        controller.Seek(3.1); Check(ArenaIs("AntFirstOpen"), "Production Ant returning corridor did not restore first arena.");
        controller.Seek(4.1); Check(ArenaIs("AntSecondOpen"), "Production Ant second wall did not open.");
        controller.Seek(5.1); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Production Ant Queen room did not end stage.");
        controller.Seek(2.1); Check(ArenaIs("AntSecond"), "Production Ant backward seek retained future wall."); stop();

        center = new(-110, 0, 30);
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x142A, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1E9DFC, center), Actor(11, 0x142B, center, combat: true)], full: true),
            Frame(511, 1, [Actor(10, 0x1E9DFC, center, eventAnimation: 0x00040008)]),
            Frame(511, 2, [Actor(100, 0, new(-90, 0, 30))], removed: [1]),
            Frame(511, 3, [Actor(100, 0, center)]),
            Frame(511, 4, [], removed: [11]),
        ]);
        controller.Seek(.2); Check(ArenaIs("NarbrooiFirst"), "Production Narbrooi initial arena differs.");
        controller.Seek(1.1); Check(ArenaIs("NarbrooiFirstOpen"), "Production Narbrooi wall did not open.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(new Vector3(-112.773f, 0, 8.201f), 0f), "Production Narbrooi wall filled original hole.");
        controller.Seek(2.1); Check(ArenaIs("NarbrooiSecond"), "Production Narbrooi primary removal lost group.");
        controller.Seek(3.1); Check(ArenaIs("NarbrooiFirstOpen"), "Production Narbrooi returning corridor failed.");
        controller.Seek(4.1); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Production Narbrooi all removed leaked.");
        controller.Seek(1.1); Check(ArenaIs("NarbrooiFirstOpen"), "Production Narbrooi backward seek failed."); stop();
        center = new(64, 0, 106);
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x141F, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1420, center), Actor(11, 0x1E9DF8, center)], full: true),
            Frame(511, .1, [Actor(1, 0x141F, center, combat: true, cast: Cast(4532, 4, center)),
                Actor(10, 0x1420, center, cast: Cast(5355, 3, center, MathF.PI / 2))]),
            Frame(511, 3.1, [Actor(10, 0x1420, center)]),
            Frame(511, 4.1, [Actor(1, 0x141F, center, combat: true)]),
            Frame(511, 5, [Actor(11, 0x1E9DF8, center, eventAnimation: 0x00040008), Actor(100, 0, new(114, 0, 17))]),
            Frame(511, 6, [Actor(100, 0, new(114, 0, 16))]),
            Frame(511, 7, [Actor(100, 0, new(114, 0, 15))]),
            Frame(511, 8, [Actor(100, 0, new(114, 0, 16))]),
            Frame(511, 9, [Actor(100, 0, new(114, 0, 17))]),
            Frame(511, 10, [], removed: [1, 10]),
            JsonSerializer.Serialize(new { t = 10.1, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
                data = new { Timestamp = Start.AddSeconds(10.1) } }),
            JsonSerializer.Serialize(new { t = 11d, kind = "LegacyMapEffect", type = "MasterPlayer.Scripting.LegacyMapEffectEvent",
                data = new { Sequence = (byte)1, Param = (byte)0, Data = new byte[] { 1, 0, 0 }, Timestamp = Start.AddSeconds(11) } }),
        ]);
        controller.Seek(.2);
        Check(ArenaIs("First", "MalboroArenas") && dangers() == 2 && previews().Length == 2, "Production Malboro initial arena or both casts missing.");
        controller.Seek(3.2); Check(dangers() == 1 && previews().Length == 1, "Production Malboro Spiritus finish consumed both casts.");
        controller.Seek(4.2); Check(dangers() == 0 && previews().Length == 0, "Production Malboro OffalBreath finish leaked.");
        controller.Seek(5.1); Check(ArenaIs("Open", "MalboroArenas"), "Production Malboro wall did not open.");
        controller.Seek(6.1); Check(ArenaIs("Open", "MalboroArenas"), "Production Malboro equality crossed forward.");
        controller.Seek(7.1); Check(ArenaIs("Second", "MalboroArenas"), "Production Malboro forward crossing failed.");
        controller.Seek(8.1); Check(ArenaIs("Second", "MalboroArenas"), "Production Malboro equality crossed backward.");
        controller.Seek(9.1); Check(ArenaIs("Open", "MalboroArenas"), "Production Malboro reverse crossing failed.");
        controller.Seek(10.2); Check(ArenaIs("Open", "MalboroArenas"), "Production Malboro group removal or battle end ended stage.");
        controller.Seek(11.1); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Production Malboro legacy completion leaked.");
        controller.Seek(7.1); Check(ArenaIs("Second", "MalboroArenas"), "Production Malboro backward seek retained completion.");
        controller.Seek(.2); Check(ArenaIs("First", "MalboroArenas") && dangers() == 2, "Production Malboro backward seek retained wall or cast endings."); stop();
        load(id,
        [
            Header(511),
            Frame(511, 0, [Actor(1, 0x141F, center, combat: true), Actor(100, 0, center)], full: true),
            JsonSerializer.Serialize(new { t = .5, kind = "PlayerDied", type = "PromeRotation.LogSystem.LogSystemPlayerDiedEvent",
                data = new { Timestamp = Start.AddSeconds(.5) } }),
            Frame(511, .6, [Actor(100, 0, center, dead: true)]),
            JsonSerializer.Serialize(new { t = 1d, kind = "LegacyMapEffect", type = "MasterPlayer.Scripting.LegacyMapEffectEvent",
                data = new { Sequence = (byte)1, Param = (byte)0, Data = new byte[] { 1 }, Timestamp = Start.AddSeconds(1) } }),
            JsonSerializer.Serialize(new { t = 2d, kind = "PlayerRevived", type = "PromeRotation.LogSystem.LogSystemPlayerRevivedEvent",
                data = new { Timestamp = Start.AddSeconds(2) } }),
            Frame(511, 2.1, [Actor(100, 0, center), Actor(1, 0x141F, center, combat: true)]),
        ]);
        controller.Seek(.2); Check(ArenaIs("First", "MalboroArenas"), "Malboro death fixture did not start.");
        controller.Seek(.7); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Malboro dead player retained output.");
        controller.Seek(2.2); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Malboro completion while suspended was lost.");
        controller.Seek(.2); Check(ArenaIs("First", "MalboroArenas"), "Malboro death fixture backward Seek retained completion."); stop();
        Console.WriteLine("PASS production native dungeons: Arboretum six modules, rotating breaths, shield ban, gaze, trash casts, exact walls, Malboro legacy completion across battle/death, group lifetime and backward seek");
    }
}
