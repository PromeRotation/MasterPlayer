using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureIceBubble(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Models(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        string[] Hints(Vector3 p) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [p])!).Cast<string>().ToArray();
        string Icon(double t, ulong source, ulong target) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = 23, Timestamp = Start.AddSeconds(t) } });
        string Create(double t, uint oid, Vector3 position) => JsonSerializer.Serialize(new { t, kind = "ObjectCreate",
            type = "PromeRotation.LogSystem.LogSystemObjectCreateEvent",
            data = new { SourceId = 9ul, DataId = oid, SourcePosition = Pose(position), Timestamp = Start.AddSeconds(t) } });
        string Remove(double t) => JsonSerializer.Serialize(new { t, kind = "ObjectRemove",
            type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent",
            data = new { SourceId = 9ul, Timestamp = Start.AddSeconds(t) } });
        const string oubliettes = "masterplayer.treasure.shifting-oubliettes";
        const string gymnasion = "masterplayer.treasure.shifting-gymnasion";
        var c = new Vector3(100, 0, 100);
        var p = c + Vector3.UnitZ * 5;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];

        load(oubliettes,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x300F, c, combat: true), Actor(2, 0x233C, c),
                Actor(100, 0, p), Actor(101, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(1, 0x300F, c, combat: true, cast: Cast(21654, 4, c)),
                Actor(2, 0x233C, c + Vector3.UnitZ * 30, cast: Cast(21654, 5, c + Vector3.UnitZ * 30))], party: party),
            Frame(924, 1, [Actor(100, 0, p, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(10))], party: party),
            EffectEvent(2, 1, 21654, 1, c),
            Frame(924, 3, [Actor(100, 0, p)], party: party),
            Frame(924, 7, [Actor(1, 0x300F, c, combat: true)], party: party),
            Frame(924, 8, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 9, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureWallKnockbackSources").Length == 2
            && Models("TreasureWallKnockbackSources").All(k => (float)Value(k, "Distance") == 25)
            && Find("treasure/kb/").Length == 4 && dangers() == 0 && Models("PredictedDamage").Length == 4,
            $"Djinn raw25 model, raidwide or display-only knockback differs: sources={Models("TreasureWallKnockbackSources").Length}, previews={Find("treasure/kb/").Length}, navigation={dangers()}, prediction={Models("PredictedDamage").Length}.");
        controller.Seek(1.1);
        Check(Find("treasure/kb/").Length == 0 && dangers() == 0, "Djinn immunity failed.");
        controller.Seek(6.1);
        Check(Find("treasure/kb/").Length == 4, "Effect or predicted expiry cleared Djinn KB.");
        controller.Seek(7.1);
        Check(Models("TreasureWallKnockbackSources").Length == 1, "Djinn observed end cleared unrelated source.");
        controller.Seek(8.1);
        Check(Find("treasure/kb/").Length == 0, "Djinn actual finish retained KB.");
        controller.Seek(.2);
        Check(Find("treasure/kb/").Length == 4, "Djinn reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Djinn25 displacement wall model immunity independent source clocks no KB navigation observed ends and Seek");

        load(oubliettes,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3029, c, combat: true), Actor(2, 0x233C, c), Actor(9, 0x301D, c),
                Actor(100, 0, p), Actor(101, 0, c)], full: true, party: party),
            Icon(.1, 100, 101), Icon(1, 101, 100),
            Frame(924, 2, [Actor(1, 0x3029, c, dead: true)], party: party),
            Frame(924, 3, [Actor(1, 0x3029, c, combat: true), Actor(101, 0, c + Vector3.UnitX * 3)], party: party),
            EffectEvent(4, 1, 21713, 1, c),
            Frame(924, 6, [Actor(2, 0x233C, c, cast: Cast(21713, 3, c, startedAt: 6))], party: party),
            Frame(924, 7, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 8, [], party: party),
        ]);
        controller.Seek(1.1);
        var ambient = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        Check(Find("treasure/bait/").Length == 2 && dangers() == 3
            && (DateTime)Value(ambient.Single(a => (string)Value(a, "Id") == "treasure/bait/edge"), "Activation") == Start.AddSeconds(.1).AddSeconds(5)
            && (DateTime)Value(ambient.Single(a => (string)Value(a, "Id") == "treasure/bait/1/avoid"), "Activation") == Start.AddSeconds(6)
            && Hints(p).Contains("Bait bubble away!") && !Hints(p).Contains("GTFO from baited aoe!")
            && !Models("PredictedDamage").Any(), "Worm icon Source deadlines base/first navigation or text differs.");
        controller.Seek(2.1);
        Check(Find("treasure/bait/").Length == 0 && dangers() == 1 && Hints(p).Contains("Bait bubble away!"),
            "Dead Worm failed base/derived split.");
        controller.Seek(5.1);
        Check(Find("treasure/bait/").Length == 2, "Worm effect/expiry cleared baits.");
        controller.Seek(6.1);
        Check(Find("treasure/bait/").Length == 0 && dangers() == 1, "Hydrocannon Start did not clear icons while retaining ordinary circle.");
        controller.Seek(7.1);
        Check(dangers() == 0, "Hydrocannon actual end retained circle.");
        controller.Seek(1.1);
        Check(Find("treasure/bait/").Length == 2, "Worm icon reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Worm independent icon deadlines first text dead source derived edge no damage Start-only clear dual views navigation and Seek");

        foreach (var (guid, territory, oid, createdOid, finish, delay, radius) in new[]
        {
            (oubliettes, 924u, 0x3029u, 0x302Au, 21715u, 5.7, 10f),
            (gymnasion, 1123u, 0x3D43u, 0x3D44u, 32315u, 3.7, 6f),
        })
        {
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true),
                    Actor(9, createdOid, c), Actor(10, createdOid, c + Vector3.UnitX * 7), Actor(100, 0, p)], full: true, party: party),
                Frame(territory, 1, [Actor(9, createdOid, c + Vector3.UnitX * 15, dead: true, targetable: false, eventState: 7)],
                    removed: [10], party: party),
                Frame(territory, 10, [Actor(2, 0x233C, c, cast: Cast(finish, 5, c, startedAt: 10))], party: party),
                EffectEvent(10.5, 2, finish, 1, c),
                Frame(territory, 11, [Actor(2, 0x233C, c)], party: party),
                Frame(territory, 12, [], party: party),
            ]);
            controller.Seek(.2);
            var original = Models("TreasureCreatedCircles");
            Check(original.Length == 2 && original.All(x => (float)Value(x, "Radius") == radius
                && (DateTime)Value(x, "Activation") == Start.AddSeconds(delay))
                && Find("treasure/created/").Length == 2 && dangers() == 2, "Created circle bootstrap timing or dual output differs.");
            controller.Seek(10.6);
            Check(Models("TreasureCreatedCircles").SequenceEqual(original) && Find("treasure/created/").Length == 2,
                "Created circle moved/died/expired/disappeared or effect cleared it.");
            controller.Seek(11.1);
            Check(Models("TreasureCreatedCircles").Length == (territory == 924 ? 1 : 0)
                && dangers() == (territory == 924 ? 1 : 0), "Created circle global FIFO/all finish policy differs.");
            if (territory == 924)
                Check((ulong)Value(Models("TreasureCreatedCircles").Single(), "Source") == 10, "Any-caster Finish removed wrong created circle.");
            controller.Seek(.2);
            Check(Find("treasure/created/").Length == 2, "Created circle reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} creation bootstrap locked identity clocks stale lifetime global finish dual views navigation Seek and owner cleanup");
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(100, 0, p)], full: true, party: party),
                Create(.1, createdOid, c), Remove(.2), Create(.3, createdOid, c + Vector3.UnitX * 7),
                Frame(territory, 1, [Actor(9, createdOid, c + Vector3.UnitX * 7)], party: party),
                Remove(2),
                Frame(territory, 2.1, [Actor(100, 0, p)], party: party),
                Frame(territory, 3, [], removed: [9], party: party),
                Frame(territory, 4, [Actor(9, createdOid, c + Vector3.UnitX * 12)], party: party),
                Frame(territory, 5, [], party: party),
            ]);
            controller.Seek(1.1);
            Check(Models("TreasureCreatedCircles").Length == 2 && dangers() == 2,
                "Explicit Destroy/Create before first snapshot swallowed new object generation.");
            controller.Seek(2.2);
            Check(Models("TreasureCreatedCircles").Length == 2, "Stale frame after Destroy duplicated old generation.");
            controller.Seek(4.1);
            Check(Models("TreasureCreatedCircles").Length == 3, "Observed absence and new same ID failed creation.");
            controller.Seek(1.1);
            Check(Models("TreasureCreatedCircles").Length == 2, "Explicit creation reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} explicit create destroy recreate before first frame stale-frame rejection new generation and reverse Seek");
        }

        load(gymnasion,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D43, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, c), Actor(4, 0x233C, c), Actor(100, 0, p)], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32310, 3.5, c + Vector3.UnitX * 10, rotation: 1)),
                Actor(3, 0x233C, c, cast: Cast(32312, 3.5, c, rotation: 2))], party: party),
            EffectEvent(1, 2, 32311, 1, c),
            Frame(1123, 2, [Actor(4, 0x233C, c, cast: Cast(32313, 4, c, rotation: 1.3f, startedAt: 2))], party: party),
            Frame(1123, 3, [Actor(4, 0x233C, c)], party: party),
            Frame(1123, 4, [Actor(4, 0x233C, c, cast: Cast(32311, 4, c, rotation: 2.1f, startedAt: 4))], party: party),
            Frame(1123, 5, [Actor(4, 0x233C, c)], party: party),
            Frame(1123, 7, [Actor(4, 0x233C, c, cast: Cast(32313, 4, c, startedAt: 7))], party: party),
            Frame(1123, 8, [Actor(4, 0x233C, c)], party: party),
            Frame(1123, 9, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Models("TreasureIceSequences").Length == 2 && Find("treasure/ice/").Length == 2 && dangers() == 2
            && Models("TreasureIceSequences").All(s => Vector3.Distance((Vector3)Value(s, "Origin"), c) < .05f
                && (DateTime)Value(s, "Activation") == Start.AddSeconds(.1 + 3.5 + .3).AddSeconds(.5)),
            "Lyssa current-only sequences center, initial clock or effect behavior differs.");
        controller.Seek(3.1);
        Check(Models("TreasureIceSequences").Count(s => (int)Value(s, "Order") == 1) == 1, "Reverse Lyssa component failed initial advancement.");
        controller.Seek(5.1);
        Check(Models("TreasureIceSequences").Length == 1 && (int)Value(Models("TreasureIceSequences").Single(), "Order") == 1
            && (DateTime)Value(Models("TreasureIceSequences").Single(), "Activation") == Start.AddSeconds(7)
            && Find("treasure/ice/").Length == 1 && dangers() == 1, "One finish did not reach both independent sequence lists.");
        controller.Seek(8.1);
        Check(Find("treasure/ice/").Length == 0 && dangers() == 0, "Second Lyssa strike failed completion.");
        controller.Seek(1.1);
        Check(Find("treasure/ice/").Length == 2, "Lyssa sequence reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Lyssa independent forward reverse sequences one Finish reaches both locked center actual clocks current-only dual views navigation and Seek");

        var eight = Enumerable.Range(100, 8).Select(i => (object)new { Slot = i - 100, EntityId = (ulong)i }).ToArray();
        load(gymnasion,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D43, c, combat: true), Actor(2, 0x233C, c),
                .. Enumerable.Range(100, 8).Select(i => Actor((ulong)i, 0, p))], full: true, party: eight),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32314, 5, c, target: 101))], party: eight),
            Frame(1123, 1, [Actor(101, 0, p, dead: true)], party: eight),
            Frame(1123, 2, [Actor(101, 0, p)], party: eight),
            Frame(1123, 3, [Actor(2, 0x233C, c)], party: eight),
            Frame(1123, 4, [], party: eight),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/stack/").Length == 1 && dangers() == 1 && !Models("PredictedDamage").Any(), "Lyssa exact-eight stack output or extra damage differs.");
        controller.Seek(1.1);
        Check(Find("treasure/stack/").Length == 0 && dangers() == 0, "Lyssa stack target death retained output.");
        controller.Seek(2.1);
        Check(Find("treasure/stack/").Length == 1, "Lyssa stack revival failed.");
        controller.Seek(3.1);
        Check(Find("treasure/stack/").Length == 0 && dangers() == 0, "Lyssa actual stack finish retained.");
        stop();
        Console.WriteLine("PASS native treasure production Lyssa exact-eight R6 stack death revival actual finish no added prediction dual views and navigation");

        foreach (var (guid, territory, oid, reward, inactive) in new[]
        {
            (oubliettes, 924u, 0x300Fu, 0x3033u, new ushort[] { 21770 }),
            (oubliettes, 924u, 0x3029u, 0x301Du, new ushort[] { 21714, 21770 }),
            (gymnasion, 1123u, 0x3D43u, 0x3D4Du, new ushort[] { 32307, 32308 }),
        })
        {
            var catalog = runtimeObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.NativeTreasureCatalog")!;
            var definition = ((IEnumerable)catalog.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [territory])!).Cast<object>().Single(d => (uint)Value(d, "BossOid") == oid);
            var ordinary = ((IEnumerable)Value(definition, "Casts")).Cast<object>()
                .Where(r => r.GetType().GetProperty("Shape")!.GetValue(r) != null).ToArray();
            var casting = ordinary.Select((r, i) => Actor((ulong)(i + 2), 0x233C, c, targetable: false,
                cast: Cast((uint)Value(r, "Action"), 3 + i, c, rotation: .37f))).ToArray();
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, c, combat: true), Actor(90, reward, c), Actor(100, 0, p)], full: true, party: party),
                Frame(territory, .1, casting, party: party),
                Frame(territory, 20, [], party: party),
                Frame(territory, 21, casting.Select((_, i) => Actor((ulong)(i + 2), 0x233C, c)).ToArray(), party: party),
                Frame(territory, 22, [Actor(1, oid, c, dead: true)], party: party),
                Frame(territory, 23, [Actor(90, reward, c, dead: true)], party: party),
                Frame(territory, 24, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == ordinary.Length && previews().Length - Find("treasure/enemy/").Length == ordinary.Length
                && Find("treasure/enemy/").Length == 2
                && (Vector4)Value(Find("treasure/enemy/90").Single(), "Color") == new Vector4(1, 0, 1, 1)
                && ordinary.All(r => (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent
                { SourceId = 2, ActionId = (ushort)(uint)Value(r, "Action") }))
                && inactive.All(a => !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = a })),
                "Ordinary rules, reward marker or inactive source boundary differs.");
            controller.Seek(20.1);
            Check(dangers() == ordinary.Length, "Predicted expiry removed ordinary source casts.");
            controller.Seek(21.1);
            Check(dangers() == 0, "Ordinary real endings retained.");
            controller.Seek(22.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Live reward failed All retain.");
            controller.Seek(23.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
                "All completion incorrectly retained helper.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} all ordinary source geometries helper clocks inactive actions rewards All and owner cleanup");
        }
        Check(previews().Length == 0 && dangers() == 0, "Ice/bubble final owner cleanup leaked.");
    }
}
