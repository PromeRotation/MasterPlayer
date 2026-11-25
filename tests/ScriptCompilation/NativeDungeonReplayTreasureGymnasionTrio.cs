using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureGymnasionTrio(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Models(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        DateTime Deadline(string key) => (DateTime)Value(Aoes().Single(a => ((string)Value(a, "Id")).StartsWith(key) && !(bool)Value(a, "Display")), "Activation");
        string[] Hints(Vector3 p) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [p])!).Cast<string>().ToArray();
        string Icon(double t, ulong source) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = source == 100 ? 101ul : 100ul, MarkerId = 111, Timestamp = Start.AddSeconds(t) } });
        const string scriptId = "masterplayer.treasure.shifting-gymnasion";
        var c = new Vector3(100, 0, 100);
        var p = c + Vector3.UnitZ * 5;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(scriptId,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D2B, c, combat: true), Actor(2, 0x233C, c),
                Actor(9, 0x3D4F, c), Actor(100, 0, p), Actor(101, 0, c + Vector3.UnitX * 5)], full: true, party: party),
            Icon(.1, 100), Icon(1, 101),
            Frame(1123, 2, [Actor(1, 0x3D2B, c, dead: true), Actor(100, 0, p, dead: true)], party: party),
            Frame(1123, 3, [Actor(1, 0x3D2B, c, combat: true), Actor(100, 0, p)], party: party),
            Frame(1123, 8, [Actor(2, 0x233C, c, cast: Cast(32212, 2.5, c, startedAt: 8))], party: party),
            Frame(1123, 9, [Actor(2, 0x233C, c)], party: party), Frame(1123, 10, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Find("treasure/bait/").Length == 2 && dangers() == 3 && !Models("PredictedDamage").Any()
            && Deadline("treasure/bait/edge") == Start.AddSeconds(.1).AddSeconds(6)
            && Deadline("treasure/bait/1/avoid") == Start.AddSeconds(7)
            && Hints(p).Contains("Bait levinorb away!") && !Hints(p).Contains("GTFO from baited aoe!"), "Pithekos Source, clock or text differs.");
        controller.Seek(2.1);
        Check(Find("treasure/bait/").Length == 0 && dangers() == 1 && Hints(p).Contains("Bait levinorb away!"), "Dead Pithekos lost derived edge.");
        controller.Seek(7.1);
        Check(Find("treasure/bait/").Length == 2, "Pithekos baits expired.");
        controller.Seek(8.1);
        Check(Find("treasure/bait/").Length == 0 && dangers() == 1, "Thundercall Start failed clear.");
        controller.Seek(9.1);
        Check(dangers() == 0, "Short cancellation retained circle.");
        controller.Seek(1.1);
        Check(Find("treasure/bait/").Length == 2, "Pithekos reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Pithekos Source111 independent six-second R18 baits dead first edge Start clear dual views navigation Seek cleanup");

        load(scriptId,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D37, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, c), Actor(100, 0, p)], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32247, 3.5, c + Vector3.UnitX * 9))], party: party),
            Frame(1123, 1, [Actor(3, 0x233C, c, cast: Cast(32248, 4, c + new Vector3(.9f, 0, .9f), rotation: 1.2f, startedAt: 1))], party: party),
            Frame(1123, 2, [Actor(3, 0x233C, c)], party: party),
            Frame(1123, 3, [Actor(3, 0x233C, c, cast: Cast(32249, 6, c, startedAt: 3))], party: party),
            EffectEvent(4, 3, 32249, 1, c), Frame(1123, 5, [Actor(3, 0x233C, c)], party: party), Frame(1123, 6, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureIceSequences").Length == 1 && Find("treasure/ice/").Length == 1 && dangers() == 1
            && Vector3.Distance((Vector3)Value(Models("TreasureIceSequences")[0], "Origin"), c) < .05f
            && (DateTime)Value(Models("TreasureIceSequences")[0], "Activation") == Start.AddSeconds(.1 + 3.5 + .3).AddSeconds(.5),
            "Earth Quaker center/initial clock differs.");
        controller.Seek(2.1);
        Check((int)Value(Models("TreasureIceSequences")[0], "Order") == 1
            && (DateTime)Value(Models("TreasureIceSequences")[0], "Activation") == Start.AddSeconds(4), "Earth Quaker axis/rotation/Finish clock differs.");
        controller.Seek(4.1);
        Check(Models("TreasureIceSequences").Length == 1 && dangers() == 1, "Effect advanced Earth Quaker.");
        controller.Seek(5.1);
        Check(Find("treasure/ice/").Length == 0 && dangers() == 0, "Final Earth Quaker Finish failed.");
        controller.Seek(.2);
        Check(Find("treasure/ice/").Length == 1, "Earth Quaker reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Styphnolobion concentric center phase clocks actual Finish axis match helper rotation dual views navigation Seek");

        load(scriptId,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D37, c, combat: true), Actor(2, 0x233C, c), Actor(3, 0x233C, c),
                Actor(100, 0, p), Actor(101, 0, c + Vector3.UnitX * 5)], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32251, 4, c, target: 100)),
                Actor(3, 0x233C, c, cast: Cast(32251, 5, c, target: 101))], party: party),
            Frame(1123, 1, [Actor(2, 0x233C, c), Actor(3, 0x233C, c)], party: party),
            TreasureNativeEffect(2, 2, 32251, 1, 999, []), TreasureNativeEffect(2.1, 2, 32251, 1, 100, []),
            Frame(1123, 3, [], removed: [101], party: party), Frame(1123, 4, [Actor(101, 0, c - Vector3.UnitZ * 15)], party: party),
            TreasureNativeEffect(5, 3, 32251, 1, 0, []), Frame(1123, 6, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Find("treasure/cast-bait/").Length == 2 && dangers() == 2 && Models("PredictedDamage").Length == 2
            && Models("PredictedDamage").All(d => (string)Value(d, "DamageType") == "Raidwide"
                && (DateTime)Value(d, "Activation") == Start.AddSeconds(.1 + 4 + .3))
            && !Hints(p).Contains("Tankbuster cleave"), "Earth Shaker Finish or target-only Raidwide differs.");
        controller.Seek(2.2);
        Check(Find("treasure/cast-bait/").Length == 1 && Models("PredictedDamage").Length == 1, "Native source FIFO/duplicate failed.");
        controller.Seek(4.1);
        Check(Find("treasure/cast-bait/").Length == 1 && dangers() == 1, "Destroyed target rebound/disappeared.");
        controller.Seek(5.1);
        Check(Find("treasure/cast-bait/").Length == 0 && dangers() == 0 && !Models("PredictedDamage").Any(), "Final effect failed clear.");
        controller.Seek(1.1);
        Check(Find("treasure/cast-bait/").Length == 2, "Earth Shaker reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Styphnolobion dynamic Earth Shaker target Raidwide Finish retention source FIFO frozen identity dual views navigation Seek");

        load(scriptId,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D39, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, c + Vector3.UnitZ * 30), Actor(4, 0x233C, c), Actor(100, 0, p)], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32258, 5, c)),
                Actor(3, 0x233C, c + Vector3.UnitZ * 30, cast: Cast(32258, 6, c + Vector3.UnitZ * 30)),
                Actor(4, 0x233C, c, cast: Cast(32259, 6.5, c + Vector3.UnitX * 12))], party: party),
            Frame(1123, 1, [Actor(100, 0, p, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(20))], party: party),
            Frame(1123, 2, [Actor(2, 0x233C, c)], party: party), Frame(1123, 3, [Actor(4, 0x233C, c)], party: party),
            Frame(1123, 4, [Actor(3, 0x233C, c + Vector3.UnitZ * 30)], party: party), Frame(1123, 5, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureWallKnockbackSources").Length == 2 && Find("treasure/kb/").Length == 4
            && dangers() == 2 && !Models("PredictedDamage").Any(), "Meganereis KB or direction/raidwide scope differs.");
        controller.Seek(1.1);
        Check(Find("treasure/kb/").Length == 0 && dangers() == 2
            && Deadline("treasure/turmoil/") == Start.AddSeconds(.1 + 5 + .3), "Immunity removed independent directions.");
        controller.Seek(2.1);
        Check(Deadline("treasure/turmoil/") == Start.AddSeconds(.1 + 6 + .3), "Second KB deadline failed.");
        controller.Seek(3.1);
        Check(dangers() == 0 && Models("TreasureWallKnockbackSources").Length == 1, "Hydrobomb Finish left directions.");
        controller.Seek(.2);
        Check(Find("treasure/kb/").Length == 4, "Meganereis reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Meganereis20 wall knockback immunity independent Hydrobomb directions first deadlines dual views navigation Seek");

        load(scriptId,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D39, c, combat: true), Actor(2, 0x233C, c), Actor(3, 0x233C, c),
                Actor(100, 0, p), Actor(101, 0, c)], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32260, 5, c, target: 101)),
                Actor(3, 0x233C, c, cast: Cast(32260, 6, c, target: 101))], party: party),
            Frame(1123, 1, [Actor(101, 0, c, dead: true)], party: party), Frame(1123, 2, [Actor(101, 0, c)], party: party),
            Frame(1123, 3, [Actor(3, 0x233C, c)], party: party), Frame(1123, 4, [Actor(2, 0x233C, c)], party: party),
            Frame(1123, 5, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("quest/spread/").Length == 2 && dangers() == 2 && !Models("PredictedDamage").Any(), "FallingWater output/prediction differs.");
        controller.Seek(1.1);
        Check(Find("quest/spread/").Length == 0 && dangers() == 0, "Dead spread visible.");
        controller.Seek(2.1);
        Check(Find("quest/spread/").Length == 2, "Spread revival failed.");
        controller.Seek(3.1);
        Check(Find("quest/spread/").Length == 1 && dangers() == 1
            && Deadline("quest/spread/") == Start.AddSeconds(.1 + 6 + .3), "Spread target FIFO consumed source item.");
        controller.Seek(4.1);
        Check(Find("quest/spread/").Length == 0 && dangers() == 0, "Final spread Finish failed.");
        controller.Seek(.2);
        Check(Find("quest/spread/").Length == 2, "Spread reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Meganereis R8 display R9 avoidance spread death revival target FIFO no added prediction Seek cleanup");

        foreach (var (oid, inactive) in new[]
        {
            (0x3D2Bu, new ushort[] { 32214, 9630 }), (0x3D37u, new ushort[] { 32199, 32250, 32252, 32254 }),
            (0x3D39u, new ushort[] { 32199, 32257, 32261 }),
        })
        {
            var catalog = runtimeObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.NativeTreasureCatalog")!;
            var definition = ((IEnumerable)catalog.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [1123u])!).Cast<object>().Single(d => (uint)Value(d, "BossOid") == oid);
            var ordinary = ((IEnumerable)Value(definition, "Casts")).Cast<object>().Where(r => r.GetType().GetProperty("Shape")!.GetValue(r) != null).ToArray();
            var casting = ordinary.Select((r, i) => Actor((ulong)(i + 2), 0x233C, c, targetable: false,
                cast: Cast((uint)Value(r, "Action"), 3 + i, c, rotation: .37f))).ToArray();
            load(scriptId,
            [
                Header(1123), Frame(1123, 0, [Actor(1, oid, c, combat: true), Actor(90, 0x3D4F, c), Actor(100, 0, p)], full: true, party: party),
                Frame(1123, .1, casting, party: party), Frame(1123, 20, [], party: party),
                Frame(1123, 21, casting.Select((_, i) => Actor((ulong)(i + 2), 0x233C, c)).ToArray(), party: party),
                Frame(1123, 22, [Actor(1, oid, c, dead: true)], party: party),
                Frame(1123, 23, [Actor(90, 0x3D4F, c, dead: true)], party: party), Frame(1123, 24, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == ordinary.Length && previews().Length - Find("treasure/enemy/").Length == ordinary.Length
                && Find("treasure/enemy/").Length == 2 && (Vector4)Value(Find("treasure/enemy/90").Single(), "Color") == new Vector4(1, 0, 1, 1)
                && ordinary.All(r => (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)(uint)Value(r, "Action") }))
                && inactive.All(a => !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = a })),
                "Ordinary output, reward or inactive boundary differs.");
            controller.Seek(20.1);
            Check(dangers() == ordinary.Length, "Expiry cleared observed casts.");
            controller.Seek(21.1);
            Check(dangers() == 0, "Real endings retained ordinary casts.");
            controller.Seek(22.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Reward All failed retain.");
            controller.Seek(23.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0, "All completion failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} complete ordinary rules independent clocks inactive boundaries rewards All dual views navigation owner cleanup");
        }
    }
}
