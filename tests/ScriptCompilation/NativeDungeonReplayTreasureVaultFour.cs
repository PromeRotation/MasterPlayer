using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureVaultFour(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        const string guid = "masterplayer.treasure.vault-oneiron";
        var c = new Vector3(100, 0, 100);
        var p = c + Vector3.UnitZ * 5;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (oid, action, total, shown) in new[]
        {
            (0x48A4u, 43645u, 5, 4), (0x48ACu, 43687u, 8, 4), (0x48A8u, 43659u, 16, 16),
        })
        {
            var casting = Enumerable.Range(2, total).Select(i => Actor((ulong)i, 0x233C, c,
                cast: Cast(action, 10, c, rotation: .37f))).ToArray();
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, oid, c, combat: true), Actor(100, 0, p)], full: true, party: party),
                Frame(1279, .1, casting, party: party), Frame(1279, 7, [], party: party), Frame(1279, 20, [], party: party),
                Frame(1279, 21, [Actor(2, 0x233C, c)], party: party),
                Frame(1279, 22, casting.Skip(1).Select((_, i) => Actor((ulong)i + 3, 0x233C, c)).ToArray(), party: party),
                Frame(1279, 23, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Models("TreasureVaultGroups").Length == total && Find("treasure/vault-group/").Length == shown
                && dangers() == (oid == 0x48A8 ? 0 : shown), "Group display/navigation expected count differs.");
            if (oid == 0x48A8)
            {
                Check(Aoes("treasure/vault-group/").Count(a => Value(a, "Color") is Vector4 color && color == new Vector4(1, 1, 0, 1)) == 4,
                    "Uplift first four danger colors differ.");
                controller.Seek(7.35);
                Check(dangers() == 0, "Uplift became risky before its own F-3.");
                controller.Seek(7.41);
                Check(dangers() == shown, "Uplift risk clock stopped between object scans.");
            }
            controller.Seek(7.5);
            Check(dangers() == shown, "F-3 activation window failed.");
            controller.Seek(20.1);
            Check(Find("treasure/vault-group/").Length == shown && dangers() == shown, "Group expired without Finish.");
            controller.Seek(21.1);
            Check(Models("TreasureVaultGroups").Length == total - 1 && Find("treasure/vault-group/").Length == Math.Min(shown, total - 1),
                "Group real Finish did not promote queue.");
            controller.Seek(22.1);
            Check(Find("treasure/vault-group/").Length == 0 && dangers() == 0, "Group final Finish failed clear.");
            controller.Seek(.2);
            Check(Find("treasure/vault-group/").Length == shown && dangers() == (oid == 0x48A8 ? 0 : shown), "Group reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} group complete queue risky windows color real Finish promotion dual views navigation Seek cleanup");
        }

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48A8, c, combat: true), Actor(100, 0, p)], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43664, 4, c + Vector3.UnitX * 3, rotation: 1))], party: party),
            Frame(1279, 1, [Actor(3, 0x233C, c, cast: Cast(43664, 4, c + new Vector3(3.9f, 0, .9f), rotation: 2, startedAt: 1))], party: party),
            Frame(1279, 2, [Actor(3, 0x233C, c)], party: party),
            Frame(1279, 3, [Actor(4, 0x233C, c, cast: Cast(43665, 4, c + Vector3.UnitX * 3, rotation: 3, startedAt: 3))], party: party),
            Frame(1279, 4, [Actor(4, 0x233C, c)], party: party),
            Frame(1279, 5, [Actor(5, 0x233C, c, cast: Cast(43666, 4, c + Vector3.UnitX * 3, rotation: -2, startedAt: 5))], party: party),
            Frame(1279, 6, [Actor(5, 0x233C, c)], party: party), Frame(1279, 7, [], party: party),
        ]);
        controller.Seek(2.1);
        Check((int)Value(Models("TreasureIceSequences")[0], "Order") == 1
            && (DateTime)Value(Models("TreasureIceSequences")[0], "Activation") == Start.AddSeconds(5)
            && Find("treasure/ice/").Length == 2 && dangers() == 2, "Tremblor axis/rotation match or second clock differs.");
        controller.Seek(4.1);
        Check((int)Value(Models("TreasureIceSequences")[0], "Order") == 2
            && (DateTime)Value(Models("TreasureIceSequences")[0], "Activation") == Start.AddSeconds(7), "Tremblor third stage clock differs.");
        controller.Seek(6.1);
        Check(Find("treasure/ice/").Length == 1 && dangers() == 1, "Tremblor consumed unrelated sequence.");
        controller.Seek(.2);
        Check(Models("TreasureIceSequences").Length == 1 && Find("treasure/ice/").Length == 1, "Tremblor reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Fastitocalon Tremblor independent origins three true finishes axis match no facing gate consumption clocks Seek");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48A4, c, combat: true), Actor(20, 0x48A7, c),
                Actor(21, 0x48A6, c + Vector3.UnitX * 7), Actor(100, 0, p)], full: true, party: party),
            Frame(1279, 1, [Actor(20, 0x48A7, c + Vector3.UnitX * 15, dead: true, targetable: false)], removed: [21], party: party),
            Frame(1279, 10, [Actor(2, 0x233C, c, cast: Cast(43656, 3, c, startedAt: 10))], party: party),
            EffectEvent(10.5, 2, 43656, 1, c), Frame(1279, 11, [Actor(2, 0x233C, c)], party: party),
            Frame(1279, 12, [], party: party),
        ]);
        controller.Seek(.2);
        var balls = Models("TreasureCreatedCircles");
        Check(balls.Length == 2 && (float)Value(balls[0], "Radius") == 16 && (float)Value(balls[1], "Radius") == 8
            && balls.All(b => (DateTime)Value(b, "Activation") == Start.AddSeconds(7.9)) && dangers() == 2,
            "Gwyddneu balls bootstrap parameters/clock differs.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 21, ActionId = 43655 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 43655 }),
            "Shock creation-owned source suppression missing or broadened.");
        controller.Seek(10.6);
        Check(Models("TreasureCreatedCircles").SequenceEqual(balls) && Find("treasure/created/").Length == 2,
            "Ball moved/died/destroyed/expired or effect cleared it.");
        controller.Seek(11.1);
        Check(Find("treasure/created/").Length == 0 && dangers() == 0, "Electrify Finish failed clear all.");
        controller.Seek(.2);
        Check(Find("treasure/created/").Length == 2, "Ball reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Gwyddneu created R16 R8 balls locked clock death destroy retain any Electrify Finish clear dual views navigation Seek");

        var collars = Enumerable.Range(20, 8).Select(i => Actor((ulong)i, 0x48AD, c + Vector3.UnitX * (i - 20), rotation: .7f)).ToArray();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48AC, c, combat: true), .. collars, Actor(100, 0, p)], full: true, party: party),
            Frame(1279, 1, Enumerable.Range(2, 4).Select(i => Actor((ulong)i, 0x233C, c, cast: Cast(43690, 4, c, startedAt: 1))).ToArray(),
                removed: [20], party: party),
            Frame(1279, 2, [Actor(2, 0x233C, c), Actor(3, 0x233C, c), Actor(4, 0x233C, c)], party: party),
            Frame(1279, 3, [Actor(5, 0x233C, c)], party: party), Frame(1279, 20, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureBeams").Length == 8 && Find("treasure/beam/").Length == 4 && dangers() == 4
            && MathF.Abs(((Vector3)Value(Models("TreasureBeams")[0], "Origin")).Z - 79) < .05f
            && (float)Value(Models("TreasureBeams")[0], "Rotation") == .7f
            && (DateTime)Value(Models("TreasureBeams")[0], "Activation") == Start.AddSeconds(7.7), "CatCollar beam origin/lock/clock differs.");
        controller.Seek(2.1);
        Check(Models("TreasureBeams").Length == 8, "Third Finish advanced beams.");
        controller.Seek(3.1);
        Check(Models("TreasureBeams").Length == 4 && (ulong)Value(Models("TreasureBeams")[0], "Source") == 24 && dangers() == 4,
            "Fourth Finish failed original tail promotion.");
        controller.Seek(19);
        Check(Find("treasure/beam/").Length == 4, "Beam clock expired.");
        controller.Seek(.2);
        Check(Models("TreasureBeams").Length == 8, "Beam reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production GreatGimmeCat eight locked beams world offset first four fourth Finish tail clocks destroy retention Seek");

        foreach (var (oid, action) in new[] { (0x48A4u, 43650u), (0x48ACu, 43698u), (0x48A8u, 43668u), (0x48A1u, 43640u) })
        {
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, oid, c, combat: true), Actor(2, 0x233C, c),
                    Actor(100, 0, p), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
                Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(action, 4, c, target: 100))], party: party),
                Frame(1279, 1, [Actor(100, 0, c + new Vector3(6, 20, 8), dead: true)], party: party),
                Frame(1279, 2, [Actor(2, 0x233C, c, dead: true, cast: Cast(action, 4, c, target: 100))], party: party),
                Frame(1279, 3, [Actor(2, 0x233C, c, cast: Cast(action, 4, c, target: 100))], party: party),
                TreasureNativeEffect(3.1, 2, action, 1, 100, []),
                Frame(1279, 4, [Actor(2, 0x233C, c)], party: party), Frame(1279, 5, [], party: party),
            ]);
            controller.Seek(1.1);
            Check(Find("treasure/cast-bait/").Length == 1 && dangers() == 1 && Models("PredictedDamage").Length == 1,
                "Finish bait lost dead target or target prediction.");
            if (action == 43640)
                Check(MathF.Abs((float)Value(Value(Aoes("treasure/cast-bait/").Single(a => (bool)Value(a, "Display")), "Shape"), "Length") - 10) < .001f,
                    "RunAmok used nonplanar distance or extra length.");
            controller.Seek(2.1);
            Check(Find("treasure/cast-bait/").Length == 0 && dangers() == 0, "Dead bait source visible.");
            controller.Seek(3.2);
            Check(Find("treasure/cast-bait/").Length == 1 && dangers() == 1, "Source revival or effect-only retention failed.");
            controller.Seek(4.1);
            Check(Find("treasure/cast-bait/").Length == 0 && dangers() == 0 && !Models("PredictedDamage").Any(), "Real bait Finish failed.");
            controller.Seek(.2);
            Check(Find("treasure/cast-bait/").Length == 1, "Bait reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} dynamic Finish bait dead target retained source revival no native effect consumption prediction dual views navigation Seek");
        }

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48AC, c, combat: true), Actor(100, 0, p), Actor(101, 0, c)], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43696, 5, c, target: 101)),
                Actor(3, 0x233C, c, cast: Cast(43696, 6, c, target: 101))], party: party),
            Frame(1279, 1, [Actor(101, 0, c, dead: true)], party: party),
            Frame(1279, 2, [Actor(101, 0, c)], party: party),
            Frame(1279, 3, [Actor(3, 0x233C, c)], party: party),
            Frame(1279, 4, [Actor(2, 0x233C, c)], party: party), Frame(1279, 5, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("quest/spread/").Length == 2 && dangers() == 2 && !Models("PredictedDamage").Any(), "Cat spread outputs differ.");
        controller.Seek(1.1);
        Check(Find("quest/spread/").Length == 0 && dangers() == 0, "Dead Cat spread visible.");
        controller.Seek(2.1);
        Check(Find("quest/spread/").Length == 2 && dangers() == 2, "Cat spread revival failed.");
        controller.Seek(3.1);
        Check((DateTime)Value(Aoes("quest/spread/").Single(a => !(bool)Value(a, "Display")), "Activation") == Start.AddSeconds(.1 + 6 + .3),
            "Cat spread Finish did not consume first matching target.");
        controller.Seek(4.1);
        Check(dangers() == 0 && Find("quest/spread/").Length == 0, "Cat spread Finish retained.");
        controller.Seek(.2);
        Check(dangers() == 2, "Cat spread reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production GreatGimmeCat spread R5 display R6 navigation dead revival target FIFO no extra prediction Seek cleanup");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48A1, c, combat: true), Actor(20, 0x48A3, c),
                Actor(21, 0x48A3, c), Actor(100, 0, p)], full: true, party: party),
            Frame(1279, .1, [Actor(20, 0x48A3, c, cast: Cast(43638, 5, c, target: 100))], party: party),
            Frame(1279, 1, [Actor(20, 0x48A3, c)], party: party),
            TreasureNativeEffect(2, 20, 43638, 1, 101, []),
            TreasureNativeEffect(3, 20, 43638, 2, 100, []),
            Frame(1279, 4, [Actor(1, 0x48A1, c, dead: true)], party: party),
            Frame(1279, 5, [], removed: [1], party: party),
            Frame(1279, 6, [Actor(20, 0x48A3, c, dead: true)], party: party), Frame(1279, 7, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Models("PredictedDamage").Length == 1 && (ulong)Value(Models("PredictedDamage")[0], "TargetId") == 100,
            "Wheel Finish lost locked target.");
        controller.Seek(2.1);
        Check(Models("PredictedDamage").Length == 1, "Wrong Wheel MainTarget consumed queue.");
        controller.Seek(3.1);
        Check(!Models("PredictedDamage").Any(), "Wheel actual MainTarget failed consume.");
        controller.Seek(4.1);
        Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Trash corpse ended AllDestroyed.");
        controller.Seek(5.1);
        Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Cached Roadripper failed retention.");
        controller.Seek(6.1);
        Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null, "Later Roadripper replaced first dead cache.");
        controller.Seek(.2);
        Check(Models("PredictedDamage").Length == 1, "Roadripper reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Roadripper Wheel native MainTarget FIFO corpse AllDestroyed first independent Boss cache no replacement Seek cleanup");

        foreach (var oid in new uint[] { 0x48A4, 0x48AC, 0x48A1, 0x48A8 })
        {
            var catalog = runtimeObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.NativeTreasureCatalog")!;
            var definition = ((IEnumerable)catalog.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [1279u])!).Cast<object>().Single(d => (uint)Value(d, "BossOid") == oid);
            bool Group(uint a) => a is 43645 or 43687 or 43688 or >= 43659 and <= 43662;
            var ordinary = ((IEnumerable)Value(definition, "Casts")).Cast<object>()
                .Where(r => r.GetType().GetProperty("Shape")!.GetValue(r) != null && !Group((uint)Value(r, "Action"))
                    && (uint)Value(r, "Action") != 43696).ToArray();
            var casts = ordinary.Select((r, i) => Actor((ulong)(i + 2), 0x233C, c,
                cast: Cast((uint)Value(r, "Action"), 3 + i, c, rotation: .37f))).ToArray();
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, oid, c, combat: true), Actor(90, 0x48B7, c), Actor(100, 0, p)], full: true, party: party),
                Frame(1279, .1, casts, party: party), Frame(1279, 20, [], party: party),
                Frame(1279, 21, casts.Select((_, i) => Actor((ulong)(i + 2), 0x233C, c)).ToArray(), party: party),
                Frame(1279, 22, [Actor(1, oid, c, dead: true)], party: party),
                Frame(1279, 23, [], removed: [1, 90], party: party), Frame(1279, 24, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == ordinary.Length && previews().Length - Find("treasure/enemy/").Length == ordinary.Length
                && ordinary.All(r => (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)(uint)Value(r, "Action") }))
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 43655 }),
                "Complete ordinary output or inactive Shock boundary differs.");
            controller.Seek(20.1);
            Check(dangers() == ordinary.Length, "Observed casts expired.");
            controller.Seek(21.1);
            Check(dangers() == 0, "Ordinary real Finish retained danger.");
            controller.Seek(22.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null, "Reward/corpse lost phase.");
            controller.Seek(23.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0, "All completion failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} complete ordinary rules clocks source suppression rewards phase lifecycle dual views navigation cleanup");
        }
    }
}
