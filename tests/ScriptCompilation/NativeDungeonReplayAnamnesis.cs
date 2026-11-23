using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAnamnesis(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.anamnesis";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        int Lines() => Aoes().Count(a => AoeId(a).StartsWith("anamnesis/line/193"));
        string Map(double time, byte index, uint state) => JsonSerializer.Serialize(new { t = time, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(time) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-40, 0, 290);
        load(id,
        [
            Header(898), Frame(898, 0, [Actor(1, 0x2CD9, c, combat: true), Actor(2, 0x2CDA, c + Vector3.UnitX * 8, combat: true),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8), Actor(10, 0x233C, c - Vector3.UnitZ * 10)], full: true, party: party),
            Frame(898, .1, [Actor(20, 0x1EAF6C, c, eventAnimation: 0x00041000)], party: party),
            EffectEvent(1, 10, 19319, 1, target: 101), EffectEvent(1.1, 10, 19312, 2, target: 100),
            Frame(898, 2, [Actor(1, 0x2CD9, c, combat: true, cast: Cast(19305, 4, c, target: 100, startedAt: 2))], party: party),
            Frame(898, 4, [Actor(101, 0, c + Vector3.UnitX * 6, dead: true)], party: party),
            EffectEvent(5, 2, 19320, 3, target: 999), EffectEvent(5.1, 1, 19313, 4, target: 999),
            EffectEvent(5.2, 1, 19311, 5),
            JsonSerializer.Serialize(new { t = 5.5, kind = "EObjAnimation", type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
                data = new { SourceId = 20UL, Param1 = 4, Param2 = 4096, Timestamp = Start.AddSeconds(5.5) } }),
            EffectEvent(5.6, 1, 19311, 7),
            Frame(898, 6.1, [Actor(1, 0x2CD9, c, combat: true)], party: party),
            EffectEvent(7, 1, 19305, 6, target: 100),
            Frame(898, 8, [Actor(1, 0x2CD9, c, dead: true)], party: party),
            Frame(898, 9, [Actor(2, 0x2CDA, c, dead: true)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Anamnesis reflection missing.");
        controller.Seek(1.2); Check(Lines() == 2 && Aoes().Any(a => AoeId(a) == "anamnesis/line/enter/19320")
            && Aoes().Any(a => AoeId(a) == "anamnesis/line/join/19313/10"), "Production Anamnesis independent line stacks not routed.");
        controller.Seek(4.1); Check(Lines() == 2, "Production Anamnesis dead marker prematurely cleared line.");
        controller.Seek(5.3); Check(previews().Length == 0 && dangers() == 0, "Production Anamnesis reflection or line effect leaked.");
        controller.Seek(5.55); Check(previews().Length == 1 && dangers() == 1, "Production Anamnesis repeated same animation event lost next reflection.");
        controller.Seek(6.2); Check(((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count() == 1, "Production Anamnesis delayed buster lost.");
        controller.Seek(7.1); Check(!((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Any(), "Production Anamnesis buster effect leaked.");
        controller.Seek(8.1); Check((ulong)Encounter().GetType().GetProperty("PrimaryId")!.GetValue(Encounter())! == 2, "Production Anamnesis pair handoff failed.");
        controller.Seek(9.1); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null, "Production Anamnesis both deaths did not end.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Anamnesis reflection seek failed."); stop();

        c = new(20, 0, -80.1f);
        List<string> tiles =
        [
            Header(898), Frame(898, 0, [Actor(1, 0x2CFE, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            Map(.1, 0x0D, 0x08000400), Map(1, 0x0D, 0x00080004),
        ];
        for (var i = 0; i < 9; ++i) tiles.Add(EffectEvent(16 + i * .2, 10, i < 5 ? 19293u : 19294u, (uint)i));
        tiles.Add(Frame(898, 20, [Actor(10, 0x233C, c, cast: Cast(19296, 5, c, target: 100, startedAt: 20))], party: party));
        tiles.Add(Frame(898, 25.1, [Actor(10, 0x233C, c)], party: party));
        load(id, tiles);
        controller.Seek(.2); Check(previews().Length == 5 && dangers() == 5, "Production Anamnesis first five tiles missing.");
        controller.Seek(1.1); Check(previews().Length == 5, "Production Anamnesis map disappear cleared future hits.");
        controller.Seek(16.81); Check(previews().Length == 4 && dangers() == 4, "Production Anamnesis second four tiles not promoted.");
        controller.Seek(18); Check(previews().Length == 0 && dangers() == 0, "Production Anamnesis nine hits leaked.");
        controller.Seek(20.1); Check(Aoes().Any(a => AoeId(a) == "anamnesis/stack/enter/10"), "Production Anamnesis stack bearer movement missing.");
        controller.Seek(25.2); Check(previews().Length == 0 && dangers() == 0, "Production Anamnesis stack finish leaked.");
        controller.Seek(.2); Check(previews().Length == 5, "Production Anamnesis tile seek failed."); stop();

        c = new(0, 0, -450);
        load(id,
        [
            Header(898), Frame(898, 0, [Actor(1, 0x2CFF, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8),
                Actor(10, 0x2D00, c, rotation: 0), Actor(11, 0x2D00, c + Vector3.UnitZ * 4, rotation: MathF.PI),
                Actor(20, 0x2D01, c + Vector3.UnitX * 4)], full: true, party: party),
            EffectEvent(.1, 10, 19333, 1), EffectEvent(.2, 10, 13269, 2),
            Map(1, 0x17, 0x00020001),
            EffectEvent(1.1, 10, 19333, 3), EffectEvent(1.2, 11, 19333, 4),
            EffectEvent(1.3, 10, 19333, 5), EffectEvent(1.4, 11, 19333, 6),
            EffectEvent(1.5, 10, 19333, 7), EffectEvent(1.6, 11, 19333, 8),
            Map(2, 0x18, 0x00020001),
            Frame(898, 2.1, [Actor(100, 0, c + Vector3.UnitZ * 10)], party: party),
            EffectEvent(3, 10, 13268, 9), Map(3.1, 0x18, 0x00080004),
            Map(4, 0x0F, 0x00020001), Map(4.1, 0x13, 0x00020001),
            Frame(898, 5, [Actor(100, 0, new(-11, 0, -465))], party: party),
            Map(5.1, 0x0F, 0x00080004),
            Frame(898, 6, [], removed: [20UL], party: party),
            EffectEvent(7, 10, 13268, 10), EffectEvent(7.1, 10, 13268, 11), EffectEvent(7.2, 10, 13268, 12),
            EffectEvent(7.3, 10, 13268, 13), EffectEvent(7.4, 10, 13268, 14),
        ]);
        controller.Seek(.15); Check(previews().Length == 1 && dangers() == 1, "Production Anamnesis default wave missing.");
        controller.Seek(1.7); Check(previews().Length == 2 && dangers() == 2, "Production Anamnesis narrow four/direction filter wrong.");
        controller.Seek(2.2); Check(previews().Length == 6 && dangers() == 6, "Production Anamnesis split all waves missing.");
        controller.Seek(4.2); Check(Aoes().Any(a => AoeId(a) == "anamnesis/drain/enter") && previews().Length == 7, "Production Anamnesis drains missing.");
        controller.Seek(5.2); Check(Aoes().Any(a => AoeId(a) == "anamnesis/drain/enter"), "Production Anamnesis solved drain hold missing.");
        controller.Seek(6.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("anamnesis/drain/")), "Production Anamnesis harpoon disappearance leaked drains.");
        controller.Seek(7.5); Check(previews().Length == 0 && dangers() == 0, "Production Anamnesis final waves leaked.");
        controller.Seek(4.2); Check(Aoes().Any(a => AoeId(a) == "anamnesis/drain/enter"), "Production Anamnesis map replay seek failed."); stop();
        Console.WriteLine("PASS native Anamnesis three bosses, paired lifecycle, animation reflection, both independent line stacks, delayed busters, nine tile hits, narrow/split wave timing and grouping, active/solved drains, removal, seek and stop");
    }
}
