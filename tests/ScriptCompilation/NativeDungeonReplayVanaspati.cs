using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunVanaspati(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.vanaspati";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Has(string prefix) => Aoes().Any(a => AoeId(a).StartsWith(prefix));
        bool Misdirection() => (bool)Encounter().GetType().GetProperty("RequireVanaspatiMisdirection")!.GetValue(Encounter())!;
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-375, 0, 85);
        load(id,
        [
            Header(970), Frame(970, 0, [Actor(1, 0x33E8, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(970, .1, [Actor(10, 0x233C, c + Vector3.UnitX * 7, cast: Cast(25138, 9, c + Vector3.UnitX * 7))], party: party),
            Frame(970, .2, [Actor(1, 0x33E8, c, combat: true, cast: Cast(25140, 8, c))], party: party),
            StatusEvent(1, 100, 1422, 0, true), StatusEvent(2, 100, 1422, 0, false),
            Frame(970, 3, [Actor(100, 0, c, stackedStatus: 2936, statusExpiresAt: Start.AddSeconds(4))], party: party),
            Frame(970, 4.2, [Actor(100, 0, c)], party: party),
            Frame(970, 9.2, [Actor(1, 0x33E8, c, combat: true), Actor(10, 0x233C, c)], party: party),
        ]);
        controller.Seek(.3); Check(previews().Length == 2 && dangers() == 2, "Production Snatcher cone/helper missing.");
        controller.Seek(1.1); Check(Misdirection() && Script().GetType().BaseType!.GetField("vanaspatiMisdirection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script()) == null,
            "Production misdirection event missing or replay initialized native hooks.");
        controller.Seek(2.1); Check(!Misdirection(), "Production misdirection loss leaked.");
        controller.Seek(3.1); Check(Misdirection(), "Production snapshot misdirection missing.");
        controller.Seek(4.3); Check(!Misdirection(), "Production expired misdirection leaked.");
        controller.Seek(9.3); Check(previews().Length == 0 && dangers() == 0, "Production Snatcher cast finish leaked.");
        controller.Seek(1.1); Check(Misdirection(), "Snatcher Seek did not rebuild status."); stop();
        c = new(-295, 0, -354);
        load(id,
        [
            Header(970), Frame(970, 0, [Actor(1, 0x33E9, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10),
                Actor(10, 0x3731, c + Vector3.UnitX * 15), Actor(11, 0x3731, c - Vector3.UnitX * 15)], full: true, party: party),
            Frame(970, .1, [Actor(1, 0x33E9, c, combat: true, cast: Cast(25147, 7, c))], party: party),
            Frame(970, 7.2, [Actor(1, 0x33E9, c, combat: true)], party: party),
            Frame(970, 8, [Actor(1, 0x33E9, c, combat: true, cast: Cast(25148, 7, c, startedAt: 8))], party: party),
            Frame(970, 9, [Actor(10, 0x3731, c + Vector3.UnitX * 15, cast: Cast(27847, 1, c, startedAt: 9))], party: party),
            Frame(970, 9.5, [], removed: [11UL], party: party),
            Frame(970, 10.1, [Actor(10, 0x3731, c + Vector3.UnitX * 15)], party: party),
            Frame(970, 15.2, [Actor(1, 0x33E9, c, combat: true)], party: party),
            Frame(970, 16, [Actor(1, 0x33E9, c, combat: true, cast: Cast(25153, 5, c, startedAt: 16))], party: party),
            Map(21.7, 6, 0x00020001),
            Frame(970, 21.8, [Actor(1, 0x33E9, c, combat: true)], party: party),
        ]);
        controller.Seek(.01); Check(Has("vanaspati/bubble/") && dangers() == 2, "Production bubble births missing.");
        controller.Seek(.2); Check(Has("vanaspati/fire/enter") && dangers() == 1 && previews().Length == 2, "Production fire did not replace bubble navigation with mandatory union.");
        controller.Seek(7.3); Check(!Has("vanaspati/fire/enter") && dangers() == 2, "Production fire finish retained safe bubbles.");
        controller.Seek(8.1); Check(Has("vanaspati/water/avoid-landing") && Has("vanaspati/water/cone/"), "Production water landing/tangent constraints missing.");
        controller.Seek(9.6); Check(!Has("vanaspati/bubble/") && !Has("vanaspati/water/avoid-landing"), "Production consumed/destroyed bubble navigation retained.");
        controller.Seek(10.2); Check(!Has("vanaspati/bubble/10"), "Production consumed bubble revived.");
        controller.Seek(16.1); Check(Has("vanaspati/ring"), "Production Wrecker shrink ring missing.");
        controller.Seek(21.9); Check(previews().Length == 0 && dangers() == 0, "Production Wrecker map clear leaked.");
        controller.Seek(.2); Check(Has("vanaspati/fire/enter"), "Wrecker Seek failed."); stop();
        c = new(300, 0, -157);
        List<string> records =
        [
            Header(970), Frame(970, 0, [Actor(1, 0x33EB, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(970, .1, [Actor(1, 0x33EB, c, combat: true, cast: Cast(25170, 5, c))], party: party),
            Frame(970, 5.2, [Actor(1, 0x33EB, c, combat: true)], party: party), Map(14, 7, 0x00020001),
            Map(15, 8, 0x00400020), EffectEvent(15.1, 1, 25165, 1),
            Frame(970, 16, [Actor(10, 0x233C, c, cast: Cast(25169, 6, c, startedAt: 16))], party: party),
            Frame(970, 17, [Actor(100, 0, c, stackedStatus: 160)], party: party),
            Frame(970, 18, [Actor(100, 0, c)], party: party),
            Frame(970, 22.1, [Actor(10, 0x233C, c)], party: party), EffectEvent(22.8, 10, 25163, 2),
            Map(24, 0x0A, 0x00020001),
        ];
        for (var i = 0; i < 9; ++i) records.Add(EffectEvent(28.3 + i * .1, 10, 25374, (uint)(3 + i)));
        load(id, records);
        controller.Seek(5.3); Check(Has("vanaspati/ring") && previews().Length == 1, "Production Svarbhanu finish ring missing.");
        controller.Seek(14.1); Check(!Has("vanaspati/ring"), "Production Svarbhanu map clear failed.");
        controller.Seek(15.2); Check(Aoes().Count(a => AoeId(a).StartsWith("vanaspati/stripe/")) == 2, "Production red-blue effect did not generate two lanes.");
        controller.Seek(16.1); Check(Aoes().Where(a => AoeId(a).StartsWith("vanaspati/stripe/"))
            .All(a => !(bool)a.GetType().GetProperty("Navigate")!.GetValue(a)!) && Has("vanaspati/cosmic/landing"), "Production knockback direct navigation suppression failed.");
        controller.Seek(17.1); Check(!Has("vanaspati/cosmic/landing") && Aoes().Where(a => AoeId(a).StartsWith("vanaspati/stripe/"))
            .All(a => (bool)a.GetType().GetProperty("Navigate")!.GetValue(a)!), "Production immunity failed.");
        controller.Seek(18.1); Check(Has("vanaspati/cosmic/landing"), "Production immunity loss did not restore knockback.");
        controller.Seek(22.9); Check(previews().Length == 0 && dangers() == 0, "Production stripe effect clear leaked.");
        controller.Seek(24.1); Check(Aoes().Count(a => AoeId(a).StartsWith("vanaspati/cosmic/")) == 3 && previews().Length == 3 && dangers() == 3, "Production first cosmic wave missing.");
        controller.Seek(28.55); Check(previews().Length == 3 && dangers() == 3, "Production cosmic FIFO failed to promote next wave.");
        controller.Seek(29.2); Check(previews().Length == 0 && dangers() == 0, "Production ninth cosmic hit leaked.");
        controller.Seek(24.1); Check(previews().Length == 3, "Svarbhanu Seek failed."); stop();
        Console.WriteLine("PASS production Vanaspati: three bosses, misdirection event/snapshot and native replay isolation, bubbles fire union/water landing/consumption, both shrinks, red-blue patterns and immunity, nine cosmic FIFO hits, seek/stop");
    }
}
