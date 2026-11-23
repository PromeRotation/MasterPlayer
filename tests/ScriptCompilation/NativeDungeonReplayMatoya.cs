using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMatoya(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.matoya";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string Tether(double t, ulong source, ulong target, uint tether, bool present) => JsonSerializer.Serialize(new { t, kind = present ? "Tether" : "TetherCancel",
            type = "PromeRotation.LogSystem.LogSystem" + (present ? "TetherEvent" : "TetherCancelEvent"),
            data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-180, 0, -140);
        load(id,
        [
            Header(933), Frame(933, 0, [Actor(1, 0x300C, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 10),
                Actor(10, 0x300E, c - Vector3.UnitZ * 8, hitboxRadius: 2)], full: true, party: party),
            Tether(.1, 10, 100, 7, true), Map(.15, 0x0A, 0x00020001),
            Frame(933, 1, [Actor(10, 0x300E, c - Vector3.UnitZ * 8, hitboxRadius: 2.5f, tethers: [new { Id = 7, TargetId = 100UL }]),
                Actor(20, 0x233C, c, cast: Cast(21646, 4.3, c, rotation: .7f, startedAt: 1))], party: party),
            Frame(933, 2, [Actor(10, 0x300E, c - Vector3.UnitZ * 8, hitboxRadius: 4, tethers: [new { Id = 7, TargetId = 100UL }])], party: party),
            Frame(933, 5.4, [Actor(20, 0x233C, c)], party: party),
            Frame(933, 5.5, [Actor(21, 0x233C, c, cast: Cast(21647, 1.2, c, rotation: .7f, startedAt: 5.5))], party: party),
            Frame(933, 6.8, [Actor(21, 0x233C, c)], party: party),
            Frame(933, 7, [Actor(22, 0x233C, c, cast: Cast(21648, 1.2, c, rotation: .7f, startedAt: 7))], party: party),
            Frame(933, 8.3, [Actor(22, 0x233C, c)], party: party),
            Tether(9, 10, 100, 7, false), Map(9.1, 0x0A, 0x00080004),
            Frame(933, 9.2, [Actor(10, 0x300E, c - Vector3.UnitZ * 8, hitboxRadius: 4)], party: party),
        ]);
        controller.Seek(.2); Check(Has("matoya/bait/hole/") && previews().Length == 2, "Production mud tether/hole missing.");
        var hole = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).EndsWith("_matoya/hole/10"));
        Check((bool)hole.GetType().GetProperty("Unclipped")!.GetValue(hole)!, "Production hole lost outside-arena drawing.");
        controller.Seek(1.1); Check(Has("matoya/breccia/") && previews().Length == 3, "Production first breccia missing.");
        controller.Seek(5.45); Check(Aoes().Single(a => AoeId(a).StartsWith("matoya/breccia/")).GetType().GetProperty("Shape")!.GetValue(Aoes().Single(a => AoeId(a).StartsWith("matoya/breccia/")))!.GetType().Name.Contains("DonutSector"), "Production breccia failed advance.");
        controller.Seek(8.4); Check(!Has("matoya/breccia/") && previews().Length == 2, "Production final breccia leaked.");
        controller.Seek(9.3); Check(previews().Length == 0 && dangers() == 0, "Production mud cleanup leaked.");
        controller.Seek(1.1); Check(Has("matoya/breccia/"), "Production mud seek failed."); stop();

        c = new(0, 0, -150);
        load(id,
        [
            Header(933), Frame(933, 0, [Actor(1, 0x307F, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10),
                Actor(10, 0x3081, c - Vector3.UnitZ * 8)], full: true, party: party),
            Tether(.1, 10, 100, 8, true), Tether(.2, 10, 100, 8, false),
            Frame(933, .3, [Actor(20, 0x1EB0C7, new(0, 0, -163)), Actor(21, 0x1EB0C7, new(10, 0, -145))], party: party),
            Map(.4, 0x12, 0x00020001), Map(.5, 0x13, 0x00020001), Map(.6, 0x17, 0x00020001),
            Frame(933, 1, [Actor(100, 0, new(0, 0, -175))], party: party),
            Frame(933, 2, [Actor(100, 0, c)], party: party),
            EffectEvent(3, 10, 22928, 1), EffectEvent(3.1, 30, 22930, 2),
            Frame(933, 4, [Actor(30, 0x233C, new(0, 0, -163))], party: party),
            EffectEvent(4.1, 30, 22926, 3),
            Frame(933, 4.2, [Actor(30, 0x233C, new(10, 0, -145))], party: party),
            EffectEvent(4.3, 30, 22926, 4),
        ]);
        controller.Seek(.25); Check(Has("matoya/bait/") && previews().Length == 1, "Production icicle untether cleared early.");
        controller.Seek(.7); Check(previews().Length == 5 && Has("matoya/geyser/enter/"), "Production geyser/map output missing.");
        controller.Seek(1.1); Check(!Has("matoya/geyser/") && previews().Length == 3, "Production cloud arena kept geysers.");
        controller.Seek(2.1); Check(Has("matoya/geyser/enter/"), "Production return from cloud lost geysers.");
        controller.Seek(3.2); Check(!Has("matoya/bait/") && !Has("matoya/gurgle/") && previews().Length == 2, "Production icicle/gurgle effects leaked.");
        controller.Seek(4.4); Check(previews().Length == 0 && dangers() == 0, "Production geyser eruption leaked.");
        controller.Seek(.7); Check(previews().Length == 5, "Production nixie seek failed."); stop();

        c = Vector3.Zero;
        load(id,
        [
            Header(933), Frame(933, 0, [Actor(1, 0x300B, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(933, .1, [Actor(1, 0x300B, c, combat: true, cast: Cast(22803, 5, c))], party: party),
            Frame(933, 5.2, [Actor(1, 0x300B, c, combat: true)], party: party), EffectEvent(6, 10, 22804, 1),
            Frame(933, 7, [Actor(1, 0x300B, c, combat: true, cast: Cast(22801, 4, c, target: 100, startedAt: 7))], party: party),
            Frame(933, 11.1, [Actor(1, 0x300B, c, combat: true)], party: party), EffectEvent(12, 10, 22802, 2, target: 100),
            ResearchIconEvent(13, 100, 169), EffectEvent(19.7, 10, 22819, 3, target: 100),
            Frame(933, 20, [Actor(1, 0x300B, c, combat: true, cast: Cast(22809, 8, c, rotation: MathF.PI / 2, startedAt: 20)),
                Actor(20, 0x233C, c, cast: Cast(22814, 10.5, c, startedAt: 20))], party: party),
            Frame(933, 28.1, [Actor(1, 0x300B, c, combat: true)], party: party),
            Frame(933, 30.6, [Actor(20, 0x233C, c)], party: party),
            Frame(933, 31, [Actor(1, 0x300B, c, combat: true, cast: Cast(22810, 50, c, rotation: MathF.PI / 2, startedAt: 31)),
                Actor(30, 0x3052, c, cast: Cast(20020, 2.5, c, startedAt: 31))], party: party),
            Frame(933, 33.6, [], removed: [30UL], party: party), EffectEvent(44.5, 1, 22811, 4),
            Map(45, 0x10, 0x00020001),
            Frame(933, 46, [Actor(1, 0x300B, c, combat: true, cast: Cast(23331, 2.5, c, startedAt: 46))], party: party),
            Frame(933, 48.6, [Actor(1, 0x300B, c, combat: true)], party: party),
            Frame(933, 49, [Actor(40, 0x233C, c, cast: Cast(22807, 3, c, rotation: .7f, startedAt: 49))], party: party),
            EffectEvent(50, 40, 22821, 5),
            Frame(933, 52.1, [Actor(40, 0x233C, c)], party: party),
        ]);
        object[] Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().ToArray();
        controller.Seek(5.3); Check(Damage().Length == 2, "Production delayed raidwide cleared at visual finish.");
        controller.Seek(6.1); Check(Damage().Length == 0, "Production delayed raidwide effect leaked.");
        controller.Seek(11.2); Check(Damage().Length == 1, "Production delayed buster cleared at visual finish.");
        controller.Seek(12.1); Check(Damage().Length == 0, "Production delayed buster effect leaked.");
        controller.Seek(13.1); Check(Has("matoya/spread/"), "Production spread icon missing.");
        controller.Seek(19.8); Check(!Has("matoya/spread/"), "Production spread hit leaked.");
        controller.Seek(20.1); Check(Has("matoya/huff/landing") && dangers() == 1 && Has("matoya/rear/"), "Production huff did not preserve rear display.");
        controller.Seek(28.2); Check(!Has("matoya/huff/") && dangers() == 1, "Production huff finish did not resume rear navigation.");
        controller.Seek(33.7); Check(Has("matoya/huff/landing"), "Production explosion finish did not activate huff.");
        controller.Seek(44.6); Check(!Has("matoya/huff/"), "Production huff effect leaked.");
        controller.Seek(48.7); Check(Has("matoya/barbeque/advance") && dangers() == 2, "Production imminent suction missing.");
        controller.Seek(52.2); Check(previews().Length == 0 && dangers() == 0, "Production mother final cleanup leaked.");
        controller.Seek(20.1); Check(Has("matoya/huff/landing"), "Production mother seek failed."); stop();
        Console.WriteLine("PASS native Matoya production replay: ordered breccia, growing tether/hole unclipped drawing, icicle lifetime, cloud/geysers, delayed damage, huff/suction, seek/stop");
    }
}
