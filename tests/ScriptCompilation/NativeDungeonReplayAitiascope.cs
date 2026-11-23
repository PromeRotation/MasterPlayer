using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAitiascope(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.aitiascope";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        bool At(string key, double seconds)
        {
            var aoe = Aoes().Single(a => AoeId(a) == key);
            return Math.Abs(((DateTime)aoe.GetType().GetProperty("Activation")!.GetValue(aoe)! - Start.AddSeconds(seconds)).TotalMilliseconds) < .001;
        }
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-6, 0, 471);
        load(id,
        [
            Header(978), Frame(978, 0, [Actor(1, 0x3469, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10),
                Actor(10, 0x346A, c)], full: true, party: party),
            Frame(978, .1, [Actor(10, 0x346A, c, cast: Cast(25670, 3, c))], party: party),
            Frame(978, 3.5, [Actor(10, 0x346A, c)], party: party),
            Frame(978, 8.7, [Actor(10, 0x346A, c + Vector3.UnitX * 5, rotation: 1)], party: party),
            Frame(978, 12.4, [Actor(10, 0x346A, c + Vector3.UnitX * 5, cast: Cast(25671, 1, c, 1, startedAt: 12.4))], party: party),
            Frame(978, 13.8, [Actor(10, 0x346A, c)], party: party),
            Frame(978, 15, [Actor(1, 0x3469, c, combat: true, cast: Cast(25673, 5, c, target: 100, startedAt: 15))], party: party),
            Frame(978, 20.4, [Actor(1, 0x3469, c, combat: true)], party: party),
            EffectEvent(21, 1, 25673, 1, target: 100),
        ]);
        controller.Seek(.2); Check(Has("aitiascope/shot/") && previews().Length == 1 && dangers() == 1, "Production first Livia shot missing.");
        controller.Seek(8.4); Check(previews().Length == 0, "Production return appeared too early.");
        controller.Seek(8.8); Check(Has("aitiascope/return/") && At("aitiascope/return/10", 13.5), "Production return clock missing.");
        controller.Seek(12.5); Check(!Has("aitiascope/return/") && Has("aitiascope/shot/"), "Production actual return failed to replace prediction.");
        controller.Seek(13.9); Check(previews().Length == 0 && dangers() == 0, "Production return finish leaked.");
        controller.Seek(20.5); Check(Has("aitiascope/bite") && Damage() == 1, "Production tankbuster disappeared before effect.");
        controller.Seek(21.1); Check(previews().Length == 0 && dangers() == 0 && Damage() == 0, "Production tankbuster effect leaked.");
        controller.Seek(8.8); Check(Has("aitiascope/return/"), "Livia Seek failed."); stop();
        c = new(11, 0, 144);
        load(id,
        [
            Header(978), Frame(978, 0, [Actor(1, 0x346B, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Map(.1, 0, 0x00020001), Map(1, 1, 0x00200010),
            Frame(978, 2, [Actor(1, 0x346B, c, combat: true, cast: Cast(25680, 11, c, startedAt: 2))], party: party),
            Map(8.4, 1, 0x00080004),
            Frame(978, 13.4, [Actor(1, 0x346B, c, combat: true)], party: party), Map(14, 0, 0x00080004),
            Frame(978, 15, [Actor(10, 0x346D, c - Vector3.UnitX)], party: party),
            Frame(978, 16, [], removed: [10UL], party: party),
            Frame(978, 17, [Actor(20, 0x233C, new(-6.027f, 0, 160.998f), cast: Cast(25684, 3.5, new(-6.027f, 0, 160.998f), startedAt: 17))], party: party),
            Frame(978, 20.9, [Actor(20, 0x233C, new(-6.027f, 0, 160.998f))], party: party),
            Frame(978, 24, [Actor(1, 0x346B, c, combat: true, cast: Cast(25686, 5, c, target: 100, startedAt: 24))], party: party),
            Frame(978, 29.4, [Actor(1, 0x346B, c, combat: true)], party: party),
            EffectEvent(30, 1, 25686, 1, target: 100),
        ]);
        controller.Seek(2.1); Check(Has("aitiascope/pockets/enter") && At("aitiascope/pockets/enter", 8.3)
            && !(bool)Aoes().Single(a => AoeId(a) == "aitiascope/skewer").GetType().GetProperty("Navigate")!.GetValue(Aoes().Single(a => AoeId(a) == "aitiascope/skewer"))!,
            "Production pockets or skewer risk gate missing.");
        controller.Seek(8.5); Check(!Has("aitiascope/pockets/enter") && dangers() == 1, "Production skewer risk did not return.");
        controller.Seek(14.1); Check(previews().Length == 0 && dangers() == 0, "Production skewer/arena reset leaked.");
        controller.Seek(15.1); Check(Aoes().Count(a => AoeId(a).StartsWith("aitiascope/shrapnel/")) == 18 && At("aitiascope/shrapnel/0", 23),
            "Production shrapnel marker missing.");
        controller.Seek(20.95); Check(previews().Length == 17 && dangers() == 17, "Production individual shrapnel helper failed to consume location.");
        controller.Seek(29.5); Check(Damage() == 1, "Production delayed buster vanished at finish.");
        controller.Seek(30.1); Check(Damage() == 0, "Production delayed buster effect leaked.");
        controller.Seek(2.1); Check(Has("aitiascope/pockets/enter"), "Rhitahtyn Seek failed."); stop();
        c = new(11, 0, -490);
        List<string> records =
        [
            Header(978), Frame(978, 0, [Actor(1, 0x346E, c - Vector3.UnitZ * 19, combat: true), Actor(100, 0, c + Vector3.UnitX * 12),
                Actor(101, 0, c + Vector3.UnitX * 10), Actor(20, 0x3470, c, hitboxRadius: 6)], full: true, party: party),
            Frame(978, .1, [Actor(1, 0x346E, c - Vector3.UnitZ * 19, combat: true, cast: Cast(25702, 32, c - Vector3.UnitZ * 19))], party: party),
            Map(15, 5, 0x00020001), Map(30, 5, 0x00080004),
            Frame(978, 32.5, [Actor(1, 0x346E, c - Vector3.UnitZ * 19, combat: true)], party: party),
        ];
        records.Add(Frame(978, 34, Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x233C, c, cast: Cast(i < 14 ? 25692u : 25691u, i < 14 ? 11 : 5, c, i * .3f, startedAt: 34))), party: party));
        records.Add(Frame(978, 39.4, Enumerable.Range(14, 4).Select(i => Actor((ulong)i, 0x233C, c)), party: party));
        records.Add(Frame(978, 45.4, Enumerable.Range(10, 4).Select(i => Actor((ulong)i, 0x233C, c)), party: party));
        load(id, records);
        controller.Seek(1); Check(previews().Length == 0 && dangers() == 0, "Production curtain active before hole map.");
        controller.Seek(15.1); Check(Has("aitiascope/curtain/enter") && previews().Length == 1 && dangers() == 1, "Production ice fan missing.");
        controller.Seek(30.1); Check(previews().Length == 0 && dangers() == 0, "Production curtain failed map gate clear.");
        controller.Seek(34.1); Check(previews().Length == 4 && Aoes().Where(a => AoeId(a).StartsWith("aitiascope/cone/"))
            .All(a => (DateTime)a.GetType().GetProperty("Activation")!.GetValue(a)! < Start.AddSeconds(40)), "Production eight caster sorting/cap differs.");
        controller.Seek(39.5); Check(previews().Length == 4 && dangers() == 4, "Production second cone wave failed to promote.");
        controller.Seek(45.5); Check(previews().Length == 0 && dangers() == 0, "Production final cones leaked.");
        controller.Seek(15.1); Check(Has("aitiascope/curtain/enter"), "Amon Seek failed."); stop();
        Console.WriteLine("PASS production Aitiascope: three bosses, returning shots, event-ended bait, pockets/skewer map risk, eighteen shrapnel and matched finish, delayed tankbuster, ice shadow map gate, sorted two cone waves, seek/stop");
    }
}
