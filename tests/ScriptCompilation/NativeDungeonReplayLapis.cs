using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunLapis(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.lapis";
        const uint territory = 1097;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!
            .GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string Key(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        string Name(object a) => (string)a.GetType().GetProperty("Name")!.GetValue(a)!;
        int Count(string part) => Aoes().Count(a => Key(a).StartsWith("lapis/" + part));
        int Named(string name) => Aoes().Count(a => Name(a) == name);
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        int Gazes() => ((IEnumerable)Encounter().GetType().GetProperty("Gazes")!.GetValue(Encounter())!).Cast<object>().Count();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string Icon(double t, ulong actor, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = actor, TargetId = 100UL,
                MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(47, 0, -570.5f);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3D56, c, combat: true),
                Actor(2, 0x3CE2, c), Actor(3, 0x3E9F, c), Actor(100, 0, c, classJob: 19),
                Actor(101, 0, c + Vector3.UnitX * 5)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x3D56, c, combat: true, cast: Cast(32535, 5, c))], party: party),
            Frame(territory, 1, [Actor(1, 0x3D56, c, dead: true), Actor(2, 0x3CE2, c, dead: true),
                Actor(3, 0x3E9F, c, combat: true, cast: Cast(32785, 5, c, startedAt: 1))], party: party),
            Frame(territory, 6.5, [Actor(3, 0x3E9F, c, combat: true)], party: party),
            Frame(territory, 7, [Actor(3, 0x3E9F, c, dead: true)], party: party),
            Frame(territory, 8, [], removed: [1, 2, 3], party: party),
        ]);
        controller.Seek(.5); Check(previews().Length == 1 && dangers() == 1, "Lapis carriage cast not native.");
        controller.Seek(1.5); Check(previews().Length == 0 && Damage() == 2, "Carriage phase transition/stun damage missing.");
        var stun = Encounter().GetType().GetMethod("LapisStunTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(stun.Invoke(Encounter(), [19u]) != null, "Production stun target missing.");
        controller.Seek(7.5); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Carriage dead corpses ended early.");
        controller.Seek(8.5); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null, "Destroyed carriage did not end.");
        controller.Seek(.5); Check(previews().Length == 1, "Carriage Seek failed."); stop();

        c = new(24, 0, -744);
        List<string> records =
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3CFE, c, combat: true),
                Actor(10, 0x3D04, c + Vector3.UnitX * 8, dead: true, targetable: false, hitboxRadius: 2),
                Actor(20, 0x3D03, new(4, 0, -759)), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Map(.1, 0x21, 0x00020001),
            Frame(territory, .2, [Actor(1, 0x3CFE, c, combat: true, cast: Cast(31364, 5, c, startedAt: .2))], party: party),
            Frame(territory, 1, [Actor(10, 0x3D04, c - Vector3.UnitX * 8, dead: true, targetable: false, hitboxRadius: 2)], party: party),
            Frame(territory, 5.6, [Actor(1, 0x3CFE, c, combat: true)], party: party),
        ];
        for (uint i = 0; i < 30; ++i) records.Add(EffectEvent(6 + i * .01, 20, 31357, i + 1));
        records.Add(Frame(territory, 7, [], party: party));
        load(id, records);
        controller.Seek(.5); Check(Count("stampede") == 1 && Count("roar") == 2 && dangers() == 2, "Albion lanes/LOS missing.");
        var shadow = Aoes().Single(a => Key(a).EndsWith("/safe"));
        dynamic geometry = shadow.GetType().GetProperty("Shape")!.GetValue(shadow)!;
        dynamic resolved = geometry.Resolve(c, 0f, null);
        Check((bool)resolved.Contains(c + Vector3.UnitX * 15, 0f), "Production LOS lost dead blocker.");
        controller.Seek(2); Check(Count("roar") == 2, "LOS snapshot lost after crystal move.");
        controller.Seek(6.7); Check(Count("roar") == 0 && Count("stampede") == 1, "Lane ended before thirty hits plus half second.");
        controller.Seek(7.1); Check(Count("stampede") == 0 && dangers() == 0, "Stampede completion leaked.");
        controller.Seek(.5); Check(Count("roar") == 2, "Albion Seek failed."); stop();

        c = new(350, 0, -394);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3971, c, combat: true), Actor(10, 0x3D06, c),
                Actor(11, 0x3D06, c + Vector3.UnitX * 4), Actor(12, 0x233C, c), Actor(13, 0x233C, new(350, 0, -404)),
                Actor(100, 0, c, classJob: 24), Actor(101, 0, c + Vector3.UnitZ * 10)], full: true, party: party),
            Icon(.1, 11, 337), Icon(.2, 10, 336),
            Frame(territory, .3, [Actor(1, 0x3971, c, combat: true, cast: Cast(32622, 3, c, startedAt: .3))], party: party),
            Frame(territory, 4, [Actor(1, 0x3971, c, combat: true)], party: party),
            Frame(territory, 8, [Actor(12, 0x233C, c, cast: Cast(32624, 2, c + new Vector3(.8f, 0, .8f), startedAt: 8))], party: party),
            Frame(territory, 10.5, [Actor(12, 0x233C, c)], party: party),
            Frame(territory, 11, [Actor(12, 0x233C, c, cast: Cast(32703, 2, c, startedAt: 11))], party: party),
            Frame(territory, 12, [], removed: [12], party: party),
            Map(13, 7, 0x00010008), EffectEvent(14, 13, 31384, 1), EffectEvent(15, 13, 31383, 2),
            Frame(territory, 16, [Actor(101, 0, c + Vector3.UnitZ * 10, stackedStatus: 3511, statusExpiresAt: Start.AddSeconds(30))], party: party),
            StatusEvent(18, 100, 3364, 0, true),
            Frame(territory, 18.2, [Actor(100, 0, c, classJob: 24, stackedStatus: 3364)], party: party),
            StatusEvent(19, 100, 3364, 0, false),
            Frame(territory, 19.2, [Actor(100, 0, c, classJob: 24)], party: party),
            Frame(territory, 31, [Actor(101, 0, c + Vector3.UnitZ * 10)], party: party),
        ]);
        controller.Seek(.5);
        Check(Count("cross") == 2 && Count("cycle") == 1, "Galatea icons/cycle missing.");
        var crossPreviews = previews().Where(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("lapis/cross")).ToArray();
        Check(crossPreviews.Length == 2 && !(bool)crossPreviews[1].GetType().GetProperty("OutlineOnly")!.GetValue(crossPreviews[1])!
            && crossPreviews[0].GetType().GetProperty("Color")!.GetValue(crossPreviews[0]) != null, "Production filled telegraphs/emphasis missing.");
        controller.Seek(7); Check(Count("cycle") == 1, "Concentric second expired by clock.");
        controller.Seek(10.7); Check(Count("cycle") == 0, "Concentric axis-tolerance ending failed.");
        controller.Seek(12.5); Check(Count("cross") == 1, "Helper destruction failed cross FIFO ending.");
        controller.Seek(14.5); Check(Count("tower/") > 0, "Failure incorrectly cleared tower.");
        controller.Seek(15.5); Check(Count("tower/") == 0, "Real tower effect failed to clear.");
        controller.Seek(18.5); Check(Gazes() == 0 && Encounter().GetType()
            .GetMethod("LapisCleanseTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Encounter(), [24u]) != null,
            "Early gaze gate/Doom restore failed.");
        controller.Seek(29.5); Check(Gazes() == 1 && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, 0f]) != null,
            "Production dynamic gaze/facing missing.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
            "Replay executed real PR facing.");
        controller.Seek(31.5); Check(Gazes() == 0, "Gaze loss leaked.");
        controller.Seek(.5); Check(Count("cross") == 2, "Galatea Seek failed."); stop();

        c = new(-250, 0, 130);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x3AE2, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(100, 0, c + Vector3.UnitX * 5, stackedStatus: 160),
                Actor(101, 0, c + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x3AE2, c, combat: true, cast: Cast(31139, 3, c))], party: party),
            Frame(territory, 4, [Actor(1, 0x3AE2, c, combat: true)], party: party),
            Map(5, 0, 0x00020001),
            Frame(territory, 6, [Actor(10, 0x233C, c, cast: Cast(32693, 2, c + Vector3.UnitZ * 16, startedAt: 6)),
                Actor(11, 0x233C, c, cast: Cast(32695, 2, c, startedAt: 6))], party: party),
            Frame(territory, 8.5, [Actor(10, 0x233C, c), Actor(11, 0x233C, c)], party: party),
            EffectEvent(9, 10, 31120, 1), EffectEvent(9.1, 10, 31120, 2),
            EffectEvent(9.2, 10, 31120, 3), EffectEvent(9.3, 10, 31120, 4),
            Frame(territory, 10, [Actor(10, 0x233C, c, cast: Cast(31122, 4, c, startedAt: 10))], party: party),
            Frame(territory, 14.5, [Actor(10, 0x233C, c)], party: party),
            Icon(15, 100, 139), Icon(15.1, 101, 161),
            JsonSerializer.Serialize(new { t = 16, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 100UL, TargetId = 1UL, TetherId = 1, Timestamp = Start.AddSeconds(16) } }),
            JsonSerializer.Serialize(new { t = 17, kind = "NpcYell", type = "PromeRotation.LogSystem.LogSystemNpcYellEvent",
                data = new { SourceId = 1UL, NpcYellId = 15175, Timestamp = Start.AddSeconds(17) } }),
            EffectEvent(18, 1, 32694, 5), EffectEvent(19, 1, 32696, 6),
            EffectEvent(20, 1, 31134, 7, target: 100), EffectEvent(21, 1, 31376, 8),
            EffectEvent(22, 1, 31138, 9), EffectEvent(23, 1, 31137, 10),
            JsonSerializer.Serialize(new { t = 24, kind = "TetherCancel", type = "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
                data = new { SourceId = 1UL, TargetId = 100UL, TetherId = 1, Timestamp = Start.AddSeconds(24) } }),
        ]);
        controller.Seek(4.5); Check(Count("shrink") == 1, "Shrink confirmation occurred before map.");
        controller.Seek(5.5); Check(Count("shrink") == 0, "Map did not confirm shrink.");
        controller.Seek(9.5); Check(Named("冲锋连续预告") == 1 && Named("水圈连续预告") == 1, "Ram/bomb telegraphs expired after ending.");
        controller.Seek(10.5); Check(Count("knockback/inside") == 1 && Count("knockback/10/landing") == 1, "Mandatory KB source constraint missing.");
        controller.Seek(17.5); Check(Count("spread") > 0 && Count("stack") > 0 && Count("tether") > 0 && Damage() >= 4, "Cagnazzo icons/tether/yell missing.");
        controller.Seek(22.5); Check(Damage() == 2, "Wrong Tsunami hit cleared damage.");
        controller.Seek(24.5); Check(previews().Length == 0 && dangers() == 0 && Damage() == 0, "Cagnazzo real ending leaked.");
        controller.Seek(17.5); Check(Count("tether") > 0, "Cagnazzo Seek failed."); stop();
        Check(previews().Length == 0 && dangers() == 0, "Lapis Stop leaked outputs.");
        Console.WriteLine("PASS production Lapis: carriage phases/stun planning, Albion stampede/LOS, Galatea concentric/cross/tower/gaze/Doom, Cagnazzo maps/KB/FIFOs/icons/tether/yell, replay facing isolation, seek/stop");
    }
}
