using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAlzadaal(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.alzadaal";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string Key(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        int Count(string part) => Aoes().Count(a => Key(a).StartsWith("alzadaal/" + part));
        DateTime At(string part) => (DateTime)Aoes().First(a => Key(a).StartsWith("alzadaal/" + part)).GetType().GetProperty("Activation")!.GetValue(
            Aoes().First(a => Key(a).StartsWith("alzadaal/" + part)))!;
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index, Param1 = (ushort)state,
                Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(124.05f, 0, -89.88f);
        load(id,
        [
            Header(1050), Frame(1050, 0, [Actor(1, 0x3879, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Map(.1, 0x11, 0x00080004),
            Frame(1050, .2, [Actor(10, 0x233C, c, cast: Cast(29556, 4, c, startedAt: .2)),
                Actor(11, 0x233C, c + Vector3.UnitX * 10, cast: Cast(29467, 7, c + Vector3.UnitX * 10, startedAt: .2))], party: party),
            Frame(1050, 4.6, [Actor(10, 0x233C, c)], party: party),
            Frame(1050, 7.6, [Actor(11, 0x233C, c)], party: party),
            Frame(1050, 12, [Actor(20, 0x233C, c, cast: Cast(29158, 2.5, c, startedAt: 12))], party: party),
            Frame(1050, 14.9, [Actor(20, 0x233C, c)], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 3 && dangers() == 3, "Production tentacle/fountains missing.");
        controller.Seek(4.7); Check(previews().Length == 2, "One fountain ending erased another.");
        controller.Seek(7.7); Check(previews().Length == 1 && Count("tentacle") == 1, "Tentacle lost before true ending.");
        controller.Seek(13); Check(previews().Length == 1, "Tentacle helper duplicated prediction.");
        controller.Seek(15); Check(previews().Length == 0 && dangers() == 0, "Tentacle true ending leaked.");
        controller.Seek(1); Check(Count("fountain") == 2, "Fountain Seek failed."); stop();
        c = new(0, 0, -182);
        List<string> records =
        [
            Header(1050), Frame(1050, 0, [Actor(1, 0x386C, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10),
                Actor(10, 0x386D, c), Actor(11, 0x386D, c), Actor(12, 0x386D, c), Actor(13, 0x386D, c),
                Actor(20, 0x233C, c), Actor(21, 0x233C, c)], full: true, party: party),
            EffectEvent(.1, 1, 28457, 1),
            StatusEvent(.2, 10, 2552, 0x180, true), StatusEvent(.2, 11, 2552, 0x180, true),
            StatusEvent(.2, 12, 2552, 0x181, true), StatusEvent(.2, 13, 2552, 0x181, true),
            Frame(1050, .3, [Actor(1, 0x386C, c, combat: true, modelState: 4),
                Actor(10, 0x386D, c, stackedStatus: 2552, statusParam: 0x180), Actor(11, 0x386D, c, stackedStatus: 2552, statusParam: 0x180),
                Actor(12, 0x386D, c, stackedStatus: 2552, statusParam: 0x181), Actor(13, 0x386D, c, stackedStatus: 2552, statusParam: 0x181)], party: party),
        ];
        for (uint i = 0; i < 14; ++i)
        {
            records.Add(EffectEvent(8 + i * .01, 20, 28455, i, target: 100));
            records.Add(EffectEvent(8 + i * .01, 20, 28455, i, target: 101));
            records.Add(EffectEvent(9 + i * .01, 21, 28445, i));
            records.Add(EffectEvent(16 + i * .01, 21, 28445, i + 14));
            records.Add(EffectEvent(17 + i * .01, 20, 28455, i + 14));
        }
        records.Add(Frame(1050, 19, [Actor(1, 0x386C, c, combat: true, modelState: 4, cast: Cast(28441, 3, c, startedAt: 19))], party: party));
        records.Add(Frame(1050, 22.5, [Actor(1, 0x386C, c, combat: true, modelState: 4)], party: party));
        records.Add(Frame(1050, 23, [Actor(30, 0x1EB69C, c)], party: party));
        records.Add(JsonSerializer.Serialize(new { t = 24, kind = "EObjSetState", type = "PromeRotation.LogSystem.LogSystemEObjSetStateEvent",
            data = new { SourceId = 30UL, State = 4, Timestamp = Start.AddSeconds(24) } }));
        load(id, records);
        controller.Seek(1); Check(previews().Length == 4 && dangers() == 4 && Count("reflection") == 2 && Count("cannon") == 2, "Production two-wave initial cap missing.");
        controller.Seek(8.05); Check(Math.Abs((At("reflection") - Start.AddSeconds(7.4)).TotalSeconds) < .000001, $"Duplicated target effects promoted wave early: {At("reflection"):O}.");
        controller.Seek(8.2); Check(Math.Abs((At("reflection") - Start.AddSeconds(15.3)).TotalSeconds) < .000001
            && Math.Abs((At("cannon") - Start.AddSeconds(7.1)).TotalSeconds) < .000001,
            $"Reflection/cannon counters not independent: reflection={At("reflection"):O}, cannon={At("cannon"):O}.");
        controller.Seek(9.2); Check(Math.Abs((At("cannon") - Start.AddSeconds(15.2)).TotalSeconds) < .000001, "Cannon second wave not promoted.");
        controller.Seek(17.2); Check(previews().Length == 0 && dangers() == 0, "Production second waves leaked.");
        controller.Seek(23.2); Check(Count("void") == 1, "Void lost at cast ending.");
        controller.Seek(24.2); Check(Count("void") == 0, "Void estate event not routed.");
        controller.Seek(1); Check(Count("reflection") == 2, "Two-wave Seek failed."); stop();
        c = new(110, 0, -68);
        load(id,
        [
            Header(1050), Frame(1050, 0, [Actor(1, 0x36C1, c, combat: true), Actor(100, 0, c),
                Actor(101, 0, c + Vector3.UnitX * 10), Actor(10, 0x233C, c), Actor(20, 0x233C, c)], full: true, party: party),
            Map(.1, 0x0D, 0x00020001),
            JsonSerializer.Serialize(new { t = .2, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 1UL, TargetId = 10UL, TetherId = 188, Timestamp = Start.AddSeconds(.2) } }),
            Frame(1050, 1, [Actor(10, 0x233C, new(110, 0, -45.5f))], party: party),
            Frame(1050, 2, [Actor(20, 0x233C, c, cast: Cast(28519, 6, c, startedAt: 2))], party: party),
            Frame(1050, 8.4, [Actor(20, 0x233C, c)], party: party),
            Frame(1050, 9, [Actor(10, 0x233C, new(110, 0, -45.5f), cast: Cast(28523, 3, c, startedAt: 9))], party: party),
            Frame(1050, 12.4, [Actor(10, 0x233C, c)], party: party),
            Map(13, 1, 0x00400004), StatusEvent(14, 100, 2973, 7, true),
            Frame(1050, 14.1, [Actor(100, 0, c, stackedStatus: 2973)], party: party),
            Frame(1050, 15, [Actor(1, 0x36C1, c, combat: true, cast: Cast(28520, 5.5, c, startedAt: 15))], party: party),
            Frame(1050, 20.9, [Actor(1, 0x36C1, c, combat: true)], party: party),
            StatusEvent(21, 100, 2973, 0, false), Frame(1050, 21.1, [Actor(100, 0, c)], party: party),
            Frame(1050, 22, [Actor(1, 0x36C1, c, combat: true, cast: Cast(28530, 5, c, target: 100, startedAt: 22))], party: party),
            Frame(1050, 27.4, [Actor(1, 0x36C1, c, combat: true)], party: party), EffectEvent(28, 1, 28530, 1, target: 100),
        ]);
        controller.Seek(1.1); Check(Count("mana") == 3 && Math.Abs((At("mana") - Start.AddSeconds(11.7)).TotalSeconds) < .000001, "Production late tether pattern/time missing.");
        controller.Seek(8.5); Check(Count("trap") == 1 && Count("mana") == 3, "Trap lost at cast end.");
        controller.Seek(12.5); Check(Count("mana") == 0 && Count("trap") == 1, "Mana end erased persistent trap.");
        controller.Seek(13.1); Check(Count("trap") == 0, "Trap map event not routed.");
        controller.Seek(14.2); Check(Count("staging") == 0, "Spinning status failed staging mask.");
        controller.Seek(21.2); Check(Count("staging") == 1, "Blade staging not advanced.");
        controller.Seek(27.5); Check(Damage() == 1, "Delayed buster lost at cast end.");
        controller.Seek(28.1); Check(Damage() == 0, "Delayed buster effect not routed.");
        controller.Seek(8.5); Check(Count("trap") == 1, "Trap Seek failed."); stop();
        Console.WriteLine("PASS production Alzadaal: three bosses, tentacle/fountain independent endings, fourteen-effect waves with duplicate targets, status/tether/estate routing, persistent traps, spinning staging, delayed buster, seek/stop");
    }
}
