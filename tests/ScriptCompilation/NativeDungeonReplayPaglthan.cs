using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPaglthan(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.paglthan";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        object[] Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().ToArray();
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-520, 0, 145);
        List<string> first =
        [
            Header(938), Frame(938, 0, [Actor(1, 0x3169, c, combat: true), Actor(100, 0, c), Actor(101, 0, c - Vector3.UnitX * 12),
                Actor(20, 0x31B6, c + Vector3.UnitX * 12)], full: true, party: party),
            StatusEvent(.1, 100, 2574, 0, true), StatusEvent(.5, 100, 2574, 0, false), StatusEvent(.5, 20, 2574, 0, true),
            EffectEvent(1, 30, 23628, 1),
            Frame(938, 2, [Actor(30, 0x31A2, c + Vector3.UnitZ * 8), Actor(31, 0x31A3, c - Vector3.UnitZ * 8)], party: party),
        ];
        for (var i = 0; i < 72; ++i) first.Add(EffectEvent(3 + i * .01, 30, 23634, (uint)(10 + i)));
        first.Add(Frame(938, 4, [Actor(32, 0x31A3, c + Vector3.UnitX * 8)], party: party));
        for (var i = 0; i < 30; ++i) first.Add(EffectEvent(5 + i * .01, 32, 23635, (uint)(100 + i)));
        first.Add(Frame(938, 6, [Actor(1, 0x3169, c, combat: true, cast: Cast(24773, 4, c, rotation: .7f, startedAt: 6))], party: party));
        first.Add(Frame(938, 10.1, [Actor(1, 0x3169, c, combat: true)], party: party));
        first.Add(EffectEvent(10.2, 1, 24773, 200)); first.Add(EffectEvent(12.8, 1, 23631, 201));
        load(id, first);
        controller.Seek(.2); Check(Has("paglthan/rod/enter") && previews().Length == 2, "Production lightning transfer missing.");
        controller.Seek(.6); Check(previews().Length == 1 && dangers() == 1, "Production transferred rod lost filled danger circle.");
        controller.Seek(1.1); Check(dangers() == 0, "Production lightning hit leaked.");
        controller.Seek(3.7); Check(previews().Length == 2, "Production shock wave cleared before hit seventy two.");
        controller.Seek(3.8); Check(previews().Length == 0 && dangers() == 0, "Production first shock final count leaked.");
        controller.Seek(5.28); Check(previews().Length == 1, "Production second shock cleared before hit thirty.");
        controller.Seek(5.4); Check(previews().Length == 0, "Production second shock final count leaked.");
        controller.Seek(6.1); Check(previews().Length == 2 && dangers() == 1, "Production future tail should only display.");
        controller.Seek(10.3); Check(previews().Length == 1 && dangers() == 1, "Production tail was not promoted.");
        controller.Seek(12.9); Check(previews().Length == 0 && dangers() == 0, "Production tail effect leaked.");
        controller.Seek(6.1); Check(previews().Length == 2, "Production first boss seek failed."); stop();

        c = new(-175, 0, 43);
        load(id,
        [
            Header(938), Frame(938, 0, [Actor(1, 0x32FE, c, targetable: false), Actor(20, 0x31AF, c, combat: true),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Map(.1, 8, 0x00200010), Map(.2, 9, 0x00200010),
            Frame(938, .3, [Actor(30, 0x31B2, c, rotation: .7f)], party: party),
            EffectEvent(.8, 30, 23704, 1), Map(1, 0x0D, 0x00020001),
            Frame(938, 2, [Actor(20, 0x31AF, c), Actor(100, 0, new(-175, 0, 8.5f))], party: party),
            Frame(938, 3, [Actor(100, 0, c)], party: party), Map(3.1, 0x0D, 0x00080004),
            Frame(938, 4, [Actor(10, 0x233C, c, cast: Cast(23703, 5, c, startedAt: 4))], party: party),
            Frame(938, 9.1, [Actor(10, 0x233C, c)], party: party),
        ]);
        controller.Seek(.25); Check(previews().Length == 2, "Production hidden fortress marker failed predator pull.");
        controller.Seek(.4); Check(Has("paglthan/missile/") && dangers() == 4, "Production missile current/future windows missing.");
        controller.Seek(.9); Check(!Has("paglthan/missile/") && dangers() == 2, "Production missile hit leaked.");
        controller.Seek(1.1); Check(Has("paglthan/platform") && previews().Length == 1 && dangers() == 0, "Production platform map did not hand off cannons.");
        controller.Seek(2.1); Check(previews().Length == 0 && !Has("paglthan/platform"), "Production core arena did not hide ground transfer.");
        controller.Seek(3.05); Check(Has("paglthan/platform"), "Production return to ground lost active platform.");
        controller.Seek(3.2); Check(previews().Length == 0, "Production platform deactivation leaked.");
        controller.Seek(4.1); Check(Has("paglthan/ballistic/") && dangers() == 1, "Production ballistic custom navigation missing.");
        controller.Seek(9.2); Check(previews().Length == 0 && dangers() == 0, "Production ballistic finish leaked.");
        controller.Seek(1.1); Check(Has("paglthan/platform"), "Production fortress seek failed."); stop();

        c = new(796.65f, 0, -97.55f);
        List<string> last =
        [
            Header(938), Frame(938, 0, [Actor(1, 0x316A, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 12)], full: true, party: party),
            Frame(938, .1, [Actor(1, 0x316A, c, combat: true, cast: Cast(23381, 5, c, target: 101))], party: party),
            Frame(938, 5.2, [Actor(1, 0x316A, c, combat: true)], party: party),
            EffectEvent(5.3, 1, 23381, 1, target: 101), EffectEvent(6.3, 1, 23382, 2, target: 101),
            EffectEvent(7.3, 1, 23382, 3, target: 101), EffectEvent(8.3, 1, 23382, 4, target: 101),
            ResearchIconEvent(9, 100, 260), ResearchIconEvent(9.1, 101, 260),
            Frame(938, 14.6, [Actor(20, 0x233C, c), Actor(21, 0x233C, c + Vector3.UnitX * 10)], party: party),
            EffectEvent(14.7, 20, 23377, 10), EffectEvent(14.8, 21, 23377, 11),
        ];
        for (var i = 0; i < 40; ++i) last.Add(EffectEvent(15 + i * .1, 20, 23376, (uint)(20 + i)));
        last.Add(Frame(938, 20, [Actor(1, 0x316A, c, combat: true, cast: Cast(23384, 5, c, target: 100, startedAt: 20))], party: party));
        last.Add(Frame(938, 25.1, [Actor(1, 0x316A, c, combat: true)], party: party)); last.Add(EffectEvent(25.5, 1, 23384, 100, target: 100));
        load(id, last);
        controller.Seek(5.25); Check(Has("paglthan/akhmorn") && previews().Length == 1, "Production stack visual finish cleared early.");
        controller.Seek(8.2); Check(previews().Length == 1, "Production four hit stack cleared before fourth hit.");
        controller.Seek(8.4); Check(previews().Length == 0 && dangers() == 0, "Production fourth stack hit leaked.");
        controller.Seek(9.2); Check(previews().Length == 2 && Has("paglthan/crossbait/"), "Production cross icons missing.");
        controller.Seek(14.9); Check(previews().Length == 2 && Has("paglthan/cross/") && !Has("paglthan/crossbait/"), "Production cross handoff failed.");
        controller.Seek(18.85); Check(previews().Length == 2, "Production two crosses cleared before hit forty.");
        controller.Seek(19); Check(previews().Length == 0 && dangers() == 0, "Production cross hit forty leaked.");
        controller.Seek(25.2); Check(Damage().Length == 1, "Production Flatten cleared at cast finish.");
        controller.Seek(25.6); Check(Damage().Length == 0, "Production Flatten effect leaked.");
        controller.Seek(9.2); Check(previews().Length == 2, "Production Bahamut seek failed."); stop();
        Console.WriteLine("PASS native Paglthan production replay: rod transfer, 72/30 shocks, tail promotion, hidden fortress pull, missile windows/core platform, four stacks, 40 cross hits, flatten, seek/stop");
    }
}
