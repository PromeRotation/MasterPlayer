using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntTargets(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        const uint territory = 956;
        var party = new object[]
        {
            new { Slot = 0, EntityId = 100UL }, new { Slot = 8, EntityId = 101UL }, new { Slot = 24, EntityId = 102UL }
        };
        foreach (var (oid, action, spread) in new[]
        {
            (0x2839u, 17858u, false), (0x281Fu, 16829u, false), (0x35FBu, 27067u, true),
            (0x28B4u, 17493u, true), (0x288Fu, 16965u, true)
        })
        foreach (var target in new ulong[] { 100, 101 })
        {
            var expectedDangers = target == 100 ? 2 : 1;
            object Preview() => previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/target"));
            dynamic Geometry() => Preview().GetType().GetProperty("Geometry")!.GetValue(Preview())!;
            object[] Actors(bool deadTarget = false) =>
            [
                Actor(100, 0, target == 100 ? new(10, 0, 20) : Vector3.Zero, dead: deadTarget && target == 100),
                Actor(101, 0, new(30, 0, 40), dead: deadTarget && target == 101),
                Actor(102, 0, new(50, 0, 60))
            ];
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, Actors().Append(Actor(1, oid, Vector3.Zero, combat: true)), full: true, party: party),
                Frame(territory, .1, [Actor(1, oid, Vector3.Zero, combat: true, cast: Cast(action, 8, Vector3.Zero, target: target))], party: party),
                Frame(territory, 3, [Actor(target, 0, new(70, 0, 80))], party: party),
                Frame(territory, 5, Actors(true), party: party),
                Frame(territory, 7, Actors(), party: party),
                Frame(territory, 9, [Actor(1, oid, Vector3.Zero, combat: true)], party: party),
                Frame(territory, 10, [Actor(1, oid, Vector3.Zero)], party: party),
                Frame(territory, 11, [], party: party)
            ]);
            controller.Seek(1d);
            Check(dangers() == expectedDangers && previews().Length == 2
                && (bool)Preview().GetType().GetProperty("OutlineOnly")!.GetValue(Preview())! == (spread || target == 100),
                "Target mechanic lost its personal navigation or own/other outline semantics.");
            var snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            var nav = ((IEnumerable)snapshot.DangerAreas).Cast<dynamic>().ToArray();
            var targetPosition = target == 100 ? new Vector3(10, 0, 20) : new Vector3(30, 0, 40);
            Check(nav.Any(a => (bool)a.Geometry.Contains(targetPosition, 0f)) == (target != 100),
                "The selected player was told to run out of its own moving circle.");
            dynamic manager = managerObject;
            object script = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
            var version = (int)snapshot.Version;
            script.GetType().BaseType!.GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(script, null);
            Check((int)runtime.Manager.BuildSnapshot(runtime.CurrentTime).Version == version,
                "An unchanged target render removed/re-added identical navigation areas.");
            controller.Seek(3.5d);
            Check((bool)Geometry().Contains(new Vector3(70, 0, 80), 0f), "Target circle failed to follow a moving selected player.");
            controller.Seek(5.5d);
            Check(dangers() == (spread ? 0 : expectedDangers) && previews().Length == (spread ? 1 : 2),
                "Bait/spread dead-target source behavior changed.");
            controller.Seek(7.5d);
            Check(dangers() == expectedDangers, "Revived target failed to restore an active marker.");
            controller.Seek(9.5d);
            Check(dangers() == 0 && previews().Length == 1, "Actual targeted cast finish retained its personal output.");
            controller.Seek(10.5d);
            Check(previews().Length == 0, "Disengaged target-mechanic boss retained enemy drawing.");
            controller.Seek(1d);
            Check(dangers() == expectedDangers, "Reverse Seek failed to rebuild a targeted mechanic.");
            stop();
        }
        uint OrdinaryOid(int i)
        {
            if (i <= 3) return 0x281F;
            return i < 7 ? 0x35FBu : 0x2839u;
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory), Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, [
                Actor(1, 0x281F, Vector3.Zero, combat: true, cast: Cast(16827, 4, Vector3.Zero)),
                Actor(2, 0x281F, Vector3.Zero, combat: true, cast: Cast(16828, 4, Vector3.Zero)),
                Actor(3, 0x281F, Vector3.Zero, combat: true, cast: Cast(16826, 4, Vector3.Zero)),
                Actor(4, 0x35FB, Vector3.Zero, combat: true, cast: Cast(27068, 4, Vector3.Zero)),
                Actor(5, 0x35FB, Vector3.Zero, combat: true, cast: Cast(27069, 4, Vector3.Zero)),
                Actor(6, 0x35FB, Vector3.Zero, combat: true, cast: Cast(27063, 4, Vector3.Zero)),
                Actor(7, 0x2839, Vector3.Zero, combat: true, cast: Cast(17856, 4, Vector3.Zero))], party: party),
            Frame(territory, 5, Enumerable.Range(1, 7).Select(i => Actor((ulong)i, OrdinaryOid(i), Vector3.Zero, combat: true)), party: party),
            Frame(territory, 6, [], party: party)
        ]);
        controller.Seek(1d);
        Check(dangers() == 5, "Mudman/Petalodus ordinary shapes or pure interrupt hint produced the wrong navigation count.");
        controller.Seek(5.5d);
        Check(dangers() == 0 && previews().Length == 7, "Ordinary targeted-module endings retained output.");
        stop();
        Console.WriteLine("PASS HuntTargets actual DLL Supay/Mudman bait and Petalodus/LilMurderer/Nuckelavee spreads, own/other navigation, exact outlines, alliance/NPC allies, moving/dead/revived targets, ordinary casts, reverse Seek and cleanup");
    }

    private static void RunHuntFollowups(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const uint territory = 956;
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL } };
        string? Damage()
        {
            var script = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
            return (string?)script.GetType().GetProperty("PredictedDamageText").GetValue(script);
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory), Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, [
                Actor(1, 0x28B4, Vector3.Zero, combat: true, cast: Cast(17492, 4, Vector3.Zero)),
                Actor(2, 0x28B4, Vector3.Zero, combat: true, cast: Cast(17492, 4, new(.001f, 0, 0)))], party: party),
            Frame(territory, 5, [
                Actor(1, 0x28B4, new(30, 0, 40), combat: true),
                Actor(2, 0x28B4, new(50, 0, 60), combat: true)], party: party),
            EffectEvent(8, 1, 17489, 1),
            Frame(territory, 9, [], party: party)
        ]);
        controller.Seek(1d);
        Check(dangers() == 3 && previews().Length == 5, "LilMurderer lost its conditional self Slash or predicted a near-self Slash.");
        controller.Seek(5.5d);
        Check(dangers() == 1 && previews().Length == 3, "LilMurderer thunder finish consumed its effect-ended Slash.");
        controller.Seek(8.5d);
        Check(dangers() == 0 && previews().Length == 2, "LilMurderer Slash effect did not clear its prediction.");
        controller.Seek(5.5d);
        Check(dangers() == 1, "LilMurderer reverse Seek failed to rebuild Slash.");
        stop();
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x288F, Vector3.Zero, combat: true, targetId: 100),
                Actor(100, 0, Vector3.Zero), Actor(101, 0, new(30, 0, 40))], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x288F, Vector3.Zero, combat: true, targetId: 100,
                cast: Cast(16964, 4, Vector3.Zero, target: 1))], party: party),
            Frame(territory, 5, [Actor(1, 0x288F, Vector3.Zero, combat: true, targetId: 101)], party: party),
            EffectEvent(6, 1, 16964, 1, target: 101), EffectEvent(6, 1, 16964, 1, target: 100),
            NativeEffect(7, 101, 2), NativeEffect(8, 100, 3),
            Frame(territory, 10, [Actor(1, 0x288F, Vector3.Zero, combat: true,
                cast: Cast(16967, 4.5, new(80, 0, 90), startedAt: 10))], party: party),
            Frame(territory, 15, [Actor(1, 0x288F, new(100, 0, 100), combat: true)], party: party),
            EffectEvent(16, 1, 18037, 3), Frame(territory, 17, [], party: party)
        ]);
        controller.Seek(5.5d);
        Check(dangers() == 0 && Damage()?.Contains("Tankbuster") == true, "Torpedo visual finish lost its pending single-target prediction.");
        controller.Seek(6.5d);
        Check(Damage()?.Contains("Tankbuster") == true, "Per-target PR events incorrectly supplied a Torpedo main target.");
        controller.Seek(7.5d);
        Check(Damage()?.Contains("Tankbuster") == true, "Wrong Torpedo effect main target consumed the original target.");
        controller.Seek(8.5d);
        Check(Damage() == null, "Actual Torpedo main target failed to consume its pending damage.");
        controller.Seek(15.5d);
        var preview = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/"));
        dynamic geometry = preview.GetType().GetProperty("Geometry")!.GetValue(preview)!;
        Check(dangers() == 1 && (bool)geometry.Contains(new Vector3(80, 0, 90), 0f)
            && !(bool)geometry.Contains(new Vector3(100, 0, 100), 0f), "Gallop finish cleared/dragged its fixed Spite prediction.");
        controller.Seek(16.5d);
        Check(dangers() == 0, "Spite effect failed to clear its prediction.");
        controller.Seek(5.5d);
        Check(Damage()?.Contains("Tankbuster") == true, "Nuckelavee reverse Seek lost the pending Torpedo.");
        stop();
        Console.WriteLine("PASS HuntFollowups actual DLL LilMurderer exact self-thunder Slash, Nuckelavee actual-target delayed Torpedo and effect-ended Spite, locked geometry, reverse Seek and cleanup");

        static string NativeEffect(double at, ulong mainTarget, uint sequence) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 1UL, ActionId = 16964u, MainTargetId = mainTarget,
                GlobalSequence = sequence, Targets = new ulong[] { 101, 100 }, Timestamp = Start.AddSeconds(at) }
        });
    }
}
