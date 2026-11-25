using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureVaultTrio(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object Geometry(object preview) => Value(preview, "Geometry");
        bool Contains(object preview, Vector3 p) => (bool)Geometry(preview).GetType().GetMethod("Contains")!.Invoke(Geometry(preview), [p, 0f])!;
        var c = new Vector3(100, 0, 100);
        const string script = "masterplayer.treasure.vault-oneiron";
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (primary, boss, action, label) in new (uint, uint, uint, string)[]
        {
            (0x489E, 0x48A0, 43630, "Bat"),
            (0x4895, 0x4897, 43607, "FluidSoul"),
            (0x489B, 0x489D, 43623, "WindSpirit"),
        })
        {
            load(script,
            [
                Header(1279), Frame(1279, 0, [Actor(1, primary, c, combat: true), Actor(2, boss, c), Actor(3, boss, c),
                    Actor(100, 0, c + Vector3.UnitZ * 16), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
                Frame(1279, .1, [Actor(2, boss, c, cast: Cast(action, 5, c + Vector3.UnitX * 30, 1.2f, 101))], party: party),
                Frame(1279, 1, [Actor(100, 0, c + Vector3.UnitX * 16), Actor(101, 0, c + Vector3.UnitX * 8)], party: party),
                Frame(1279, 2, [Actor(2, boss, c)], party: party),
                Frame(1279, 3, [], removed: [101], party: party),
                Frame(1279, 4, [Actor(101, 0, c - Vector3.UnitZ * 8)], party: party),
                TreasureNativeEffect(5, 3, action, 1, 100, []),
                TreasureNativeEffect(6, 2, action, 1, 999, []),
                Frame(1279, 7, [], party: party),
            ]);
            controller.Seek(.2);
            var preview = Find("treasure/cast-bait/").Single();
            Check(!(bool)Value(preview, "OutlineOnly") && dangers() == 1 && Damage().Length == 1
                && Contains(preview, c + Vector3.UnitZ * 9) && !Contains(preview, c + Vector3.UnitX * 9),
                $"{label} initial dynamic bait, other fill, navigation or captured damage differs.");
            controller.Seek(1.1);
            Check(Contains(Find("treasure/cast-bait/").Single(), c + Vector3.UnitX * 9)
                && !Contains(Find("treasure/cast-bait/").Single(), c + Vector3.UnitZ * 9),
                $"{label} bait did not dynamically face target.");
            controller.Seek(4.1);
            Check(Find("treasure/cast-bait/").Length == 1 && dangers() == 1 && Damage().Length == 1
                && Contains(Find("treasure/cast-bait/").Single(), c + Vector3.UnitX * 9)
                && !(bool)Value(Find("treasure/cast-bait/").Single(), "OutlineOnly"),
                $"{label} cancel or same-ID recreated target replaced frozen old reference.");
            controller.Seek(5.1);
            Check(Find("treasure/cast-bait/").Length == 1, $"{label} effect consumed another source.");
            controller.Seek(6.1);
            Check(Find("treasure/cast-bait/").Length == 0 && dangers() == 0 && Damage().Length == 0,
                $"{label} native source effect depended on MainTarget or current cast.");
            controller.Seek(4.1);
            Check(Find("treasure/cast-bait/").Length == 1, $"{label} reverse Seek lost frozen reference.");
            stop();
            Check(dangers() == 0 && previews().Length == 0, $"{label} owner stop leaked outputs.");
            Console.WriteLine($"PASS native treasure production Vault {label} dynamic bait direction, frozen target, native source FIFO and Seek");

            load(script,
            [
                Header(1279), Frame(1279, 0, [Actor(1, primary, c, combat: true), Actor(2, boss, c), Actor(3, boss, c),
                    Actor(100, 0, c)], full: true, party: party),
                Frame(1279, 1, [Actor(1, primary, c, dead: true)], party: party),
                Frame(1279, 2, [], removed: [1], party: party),
                Frame(1279, 3, [Actor(2, boss, c, dead: true)], party: party),
                Frame(1279, 4, [], party: party),
            ]);
            controller.Seek(1.1);
            Check(Value(Encounter(), "Active") != null, $"{label} dead corpse ended AllDestroyed.");
            controller.Seek(2.1);
            Check(Value(Encounter(), "Active") != null && Find("treasure/enemy/").Length == 1
                && Find("treasure/enemy/2").Length == 1, $"{label} cache or duplicate marker selection differs.");
            controller.Seek(3.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
                $"{label} later boss substituted for dead first cache.");
            controller.Seek(2.1);
            Check(Find("treasure/enemy/2").Length == 1, $"{label} cache lifecycle Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production Vault {label} destroyed trash gate and first cached boss completion");
        }

        load(script,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x489E, c, combat: true), Actor(2, 0x48A0, c),
                Actor(100, 0, c + Vector3.UnitZ * 8), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x48A0, c, cast: Cast(43630, 5, c, target: 100))], party: party),
            Frame(1279, 1, [Actor(2, 0x48A0, c)], party: party),
            Frame(1279, 2, [Actor(2, 0x48A0, c, cast: Cast(43630, 5, c, target: 101, startedAt: 2))], party: party),
            Frame(1279, 3, [Actor(2, 0x48A0, c)], party: party),
            EffectEvent(4, 2, 43630, 1, target: 100),
            TreasureNativeEffect(5, 2, 43630, 1, 999, []),
            TreasureNativeEffect(5.1, 2, 43630, 1, 999, []),
            TreasureNativeEffect(6, 2, 43630, 2, 0, []),
            Frame(1279, 7, [], party: party),
        ]);
        controller.Seek(4.1);
        Check(Find("treasure/cast-bait/").Length == 2 && Damage().Length == 2
            && Damage().Select(p => (DateTime)Value(p, "Activation")).Distinct().Count() == 1,
            "Repeated cast FIFO or union damage first activation differs.");
        controller.Seek(5.2);
        Check(Find("treasure/cast-bait/").Length == 1 && Damage().Single() is var prediction
            && (ulong)Value(prediction, "TargetId") == 101, "Native source FIFO/duplicate consumption differs.");
        controller.Seek(6.1);
        Check(Find("treasure/cast-bait/").Length == 0 && Damage().Length == 0, "Final FIFO item retained.");
        stop();
        Console.WriteLine("PASS native treasure production Vault Bat multiple cast FIFO, first union damage and duplicate packet isolation");

        string Packet(double time, ulong target, Vector3 location) => JsonSerializer.Serialize(new
        {
            t = time, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
            data = new { SourceId = 2ul, TargetId = target, ActionId = 43630u, CastTime = 5f,
                EffectPosition = Pose(location), Timestamp = Start.AddSeconds(.1) },
        });
        load(script,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x489E, c, combat: true), Actor(2, 0x48A0, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x48A0, c, cast: Cast(43630, 5, c, target: 101))], party: party),
            Frame(1279, 1, [], removed: [101], party: party),
            Packet(1.1, 101, c + Vector3.UnitX),
            Frame(1279, 2, [], party: party),
        ]);
        controller.Seek(1.2);
        Check(Find("treasure/cast-bait/").Length == 1 && dangers() == 1
            && Damage().Single() is var absent && (ulong)Value(absent, "TargetId") == 101,
            "Missing target packet correction erased frozen bait or retained roster damage.");
        stop();
        Console.WriteLine("PASS native treasure production Vault absent frozen target retains bait and roster damage through packet correction");

        load(script,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x489E, c, combat: true), Actor(2, 0x48A0, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Packet(.1, 100, c),
            TreasureNativeEffect(.2, 2, 43630, 1, 0, []),
            Frame(1279, .3, [Actor(2, 0x48A0, c, cast: Cast(43630, 5, c, target: 101))], party: party),
            Frame(1279, 1, [], party: party),
        ]);
        controller.Seek(.4);
        Check(Find("treasure/cast-bait/").Length == 0 && Damage().Length == 0,
            "First raw target correction revived effect-consumed packet generation.");
        stop();
        Console.WriteLine("PASS native treasure production Vault consumed packet generation cannot revive at first raw target correction");
    }
}
