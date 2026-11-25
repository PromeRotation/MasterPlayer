using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleSunset(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints(Vector3? position = null) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!
            .Invoke(Encounter(), [position ?? center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action, double? eventTime = null)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(eventTime ?? t) } });
        object[] Initial(ulong target = 100) =>
        [
            Actor(1, 0x26F4, center, combat: true, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center, cast: Cast(14873, 4, center + Vector3.UnitX * 50, target: target)),
            Actor(100, 0, center + Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitZ * 5),
        ];
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Sunset replay leaked native controls.");
        }

        foreach (var (oid, name) in new[]
        {
            (0x26F2u, "怪力兽王——迪克巴朗·第一幕"),
            (0x26F4u, "怪力兽王——迪克巴朗·第二幕"),
        })
        {
            var sameGroup = oid == 0x26F2 ? oid : 0x26F3;
            load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, oid, center), Actor(4, sameGroup, center + Vector3.UnitX * 5, combat: true)]),
                At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, combat: true, dead: true)]),
                At(3, [], removed: [4]), At(4, [])]);
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x26F2), "Sunset group versus primary pull.");
            if (oid == 0x26F4)
                Check(((string)Value(Encounter(), "CarnivalePrecombatHint")).Contains("75%"), "Sunset preparation.");
            controller.Seek(1.5);
            Check(Shown("carnivale/enemy/").Length == 2 && Value(Encounter(), "CarnivalePrecombatHint") == null,
                "Sunset markers or preparation transition.");
            var assembly = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = assembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!
                .GetField(oid == 0x26F2 ? "CircleBig" : "CircleSmall", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var direction = assembly.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var compared = 0;
            for (var x = 73.13f; x < 127; x += .7f)
                for (var z = 73.17f; z < 127; z += .7f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(direction, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Sunset source arena oracle.");
                    ++compared;
                }
            controller.Seek(2.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x26F2), "Sunset completion waited wrong group.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Sunset completion owner.");
            controller.Seek(1.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(1.6);
            Check(previews().Length == 0 && dangers() == 0, "Sunset per-act switch.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage16 {oid:X} source arena oracle {compared} points preparation group or primary pull completion double view owner");
        }

        load(guid, [Header(796), At(0, Initial(), true),
            At(1, [Actor(2, 0x233C, center + Vector3.UnitX * 3,
                cast: Cast(14873, 4, center + Vector3.UnitX * 50, target: 100)),
                Actor(100, 0, center + Vector3.UnitZ * 13)]),
            At(2, [Actor(100, 0, center + Vector3.UnitZ * 13, dead: true)]),
            EffectEvent(7, 2, 14873, 1), At(8, []),
            At(9, [Actor(2, 0x233C, center)]), At(10, [])]);
        controller.Seek(1.5);
        var charge = Values("CarnivaleCharges").Single();
        var source = Value(charge, "Source");
        var target = Value(charge, "Target");
        Check((ulong)Value(Value(target, "Actor"), "Id") == 100
            && (Vector3)Value(Value(source, "Actor"), "Position") == center + Vector3.UnitX * 3
            && Shown("/charge/").Any(a => ((string)Value(a, "Id")).EndsWith("/display") && (bool)Value(a, "OutlineOnly"))
            && dangers() == 1, "Dynamic own bait missing direction navigation.");
        var oracleAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var wpos = oracleAssembly.GetType("BossMod.WPos")!;
        var angle = oracleAssembly.GetType("BossMod.Angle")!;
        var rect = oracleAssembly.GetType("BossMod.AOEShapeRect")!;
        var ctor = rect.GetConstructors().First(c => c.GetParameters().First().ParameterType == typeof(float));
        var origin = center + Vector3.UnitX * 3;
        var destination = center + Vector3.UnitZ * 13;
        var delta = destination - origin;
        var length = new Vector2(delta.X, delta.Z).Length();
        var rotation = MathF.Atan2(delta.X, delta.Z);
        var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
        args[0] = length; args[1] = 4f;
        var oracle = ctor.Invoke(args);
        dynamic geometry = Value(Shown("/charge/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Geometry");
        var checkedPoints = 0;
        for (var x = 85.13f; x < 119; x += .37f)
            for (var z = 85.17f; z < 119; z += .41f)
            {
                var p = new Vector3(x, 0, z);
                var expected = (bool)rect.GetMethod("Check", [wpos, wpos, angle])!.Invoke(oracle,
                    [Activator.CreateInstance(wpos, [x, z]), Activator.CreateInstance(wpos, [origin.X, origin.Z]),
                        Activator.CreateInstance(angle, [rotation])])!;
                Check((bool)geometry.Contains(p, 0f) == expected, "Sunset dynamic source rectangle oracle.");
                ++checkedPoints;
            }
        controller.Seek(8.5);
        Check(Values("CarnivaleCharges").Length == 1 && Shown("/charge/").Any(a => ((string)Value(a, "Id")).EndsWith("/display")),
            "Dead target, Effect or deadline cleared bait.");
        controller.Seek(9.5);
        Check(Values("CarnivaleCharges").Length == 0 && Value(Encounter(), "CarnivaleChargeKnockback") == null && dangers() == 0,
            "Observed Finish leaked charge/KB.");
        controller.Seek(1.5);
        Isolated();
        manager.SetMechanismEnabled(guid, "怪力兽王——迪克巴朗·第二幕", false);
        controller.Seek(1.6);
        Check(previews().Length == 0 && dangers() == 0, "Sunset disabled owner.");
        manager.SetMechanismEnabled(guid, "怪力兽王——迪克巴朗·第二幕", true);
        stop();
        Console.WriteLine($"PASS native Carnivale Stage16 dynamic charge source oracle {checkedPoints} points personal outline navigation dead target deadline Effect observed Finish reverse Seek owner replay isolation");

        load(guid, [Header(796), At(0, Initial(999), true),
            At(1, [Actor(999, 0, center + Vector3.UnitZ * 8),
                Actor(3, 0x233C, center, cast: Cast(14873, 7, center, target: 101, startedAt: 1))]),
            At(2, [Actor(1, 0x26F4, center + Vector3.UnitX * 10, combat: true)]),
            Cancel(3, 2, 14873), At(4, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleCharges").Length == 0 && Value(Encounter(), "CarnivaleChargeKnockback") != null
            && Shown("/knockback/").Length == 2 && dangers() == 0, "Missing target KB coupled to bait.");
        controller.Seek(2.5);
        Check(Values("CarnivaleCharges").Length == 1
            && (ulong)Value(Value(Value(Encounter(), "CarnivaleChargeKnockback"), "Cast"), "Source") == 3,
            "Late target or last-start slot.");
        var landing = (Vector3)Encounter().GetType().GetMethod("CarnivaleChargeLanding", flags)!.Invoke(Encounter(),
            [Value(Value(Values("CarnivaleCharges").Single(), "Target"), "Actor")])!;
        var expectedLanding = center + Vector3.UnitZ * 5 + Vector3.Normalize(new Vector3(-10, 0, 5)) * 20;
        Check(Vector3.Distance(landing, expectedLanding) < .0001f, "KB used helper rather than dynamic primary.");
        controller.Seek(3.5);
        Check(Values("CarnivaleCharges").Length == 1 && Value(Encounter(), "CarnivaleChargeKnockback") == null
            && Shown("/knockback/").Length == 0, "Any-source Finish did not clear independent KB.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage16 missing initial target no late bind independent last start slot dynamic primary 20y no wall stop any-source cancel reverse Seek");

        foreach (var action in new uint[] { 14872, 14871, 14876, 15268 })
        {
            load(guid, [Header(796), At(0, [Actor(1, 0x26F4, center, combat: true), Actor(100, 0, center)], true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, .3f))]),
                EffectEvent(7, 2, action, 1), At(8, []), Cancel(9, 2, action is 14871 or 14876 ? 60000u : action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == 1 && Shown("cast/").Length == 1
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action }),
                "Sunset ordinary native dual output.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Sunset ordinary deadline/Effect lifetime.");
            controller.Seek(9.5);
            Check(dangers() == 0, "Sunset ordinary/group arbitrary finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage16 four ordinary AOE exact rules takeover observed lifetime group any action Finish navigation double view reverse Seek");

        var gaze = center + Vector3.UnitX * 4;
        var viewer = gaze - Vector3.UnitZ * 120;
        load(guid, [Header(796), At(0, [Actor(1, 0x26F4, center, combat: true), Actor(100, 0, viewer)], true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14875, 3, gaze))]),
            At(5, [Actor(100, 0, gaze - Vector3.UnitZ * 10001)]),
            At(6, [Actor(2, 0x233C, center)]), At(7, [])]);
        controller.Seek(2.8);
        Check(Values("Gazes").Length == 1 && Shown("carnivale/gaze/").Length == 3
            && Hints(viewer).Contains("Turn away from gaze!") && dangers() == 0
            && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [viewer, 0f]) != null,
            "Sunset gaze full display range lock PR facing.");
        Isolated();
        controller.Seek(5.5);
        Check(Shown("carnivale/gaze/").Length == 2
            && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [gaze - Vector3.UnitZ * 10001, 0f]) == null, "Gaze range10000.");
        controller.Seek(6.5);
        Check(Shown("carnivale/gaze/").Length == 0, "Gaze observed end.");
        stop();
        load(guid, [Header(796), At(0, [Actor(1, 0x26F4, center, combat: true), Actor(100, 0, center)], true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14686, 9, center))]),
            CastEvent(.2, 2, 14686, 5, center), At(1, []),
            At(2, [Actor(2, 0x233C, center)]), At(3, [])]);
        controller.Seek(.5);
        var damage = Values("PredictedDamage").Single();
        Check((DateTime)Value(damage, "Activation") == Start.AddSeconds(9.4) && (string)Value(damage, "DamageType") == "Raidwide"
            && Hints().Contains("Use Diamondback!") && dangers() == 0
            && !new uint[] { 14685, 14874, 14779 }.Any(a => (bool)manager.IsGenericDodgeSuppressed(
                new LogSystemCastStartEvent { SourceId = 1, ActionId = (ushort)a })), "Raidwide raw clock or expanded source scope.");
        controller.Seek(2.5);
        Check(Values("PredictedDamage").Length == 0, "Raidwide Finish.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage16 gaze locked eye pupil arc range10000 PR facing isolated replay raidwide raw end clock hint exact source component scope");

        load(guid, [Header(796), At(0, [Actor(1, 0x26F4, center, combat: true), Actor(2, 0x233C, center),
                Actor(100, 0, center + Vector3.UnitZ * 10)], true),
            CastEvent(.1, 2, 14873, 4, center, target: 999),
            At(.15, [Actor(999, 0, center + Vector3.UnitZ * 7),
                Actor(2, 0x233C, center, cast: Cast(14873, 4, center, target: 999))]),
            At(1, [Actor(3, 0x233C, center, cast: Cast(14873, 4, center, target: 100, startedAt: 1))]),
            At(2, [Actor(2, 0x233C, center, cast: Cast(14873, 4, center, target: 999, startedAt: 2))]),
            At(3, [Actor(2, 0x233C, center, cast: Cast(14873, 4, center + Vector3.UnitX, target: 999, startedAt: 3))]),
            Cancel(3.1, 2, 14873, eventTime: 2.5), Cancel(4, 2, 14873), At(5, [])]);
        controller.Seek(.16);
        Check(Values("CarnivaleCharges").Length == 1
            && (ulong)Value(Value(Value(Values("CarnivaleCharges").Single(), "Target"), "Actor"), "Id") == 999,
            "Packet-missing target not confirmed by canonical first raw.");
        controller.Seek(2.5);
        Check(Values("CarnivaleCharges").Length == 2
            && (ulong)Value(Value(Value(Encounter(), "CarnivaleChargeKnockback"), "Cast"), "Source") == 3,
            "Clock-only drift manufactured Finish/Start.");
        controller.Seek(3.5);
        Check(Values("CarnivaleCharges").Length == 2
            && (ulong)Value(Value(Value(Encounter(), "CarnivaleChargeKnockback"), "Cast"), "Source") == 2,
            "True raw replacement or stale Finish boundary.");
        controller.Seek(4.5);
        Check(Values("CarnivaleCharges").Length == 1 && Value(Encounter(), "CarnivaleChargeKnockback") == null, "Current generation Finish.");
        controller.Seek(.16);
        stop();
        Console.WriteLine("PASS native Carnivale Stage16 canonical first raw target confirmation clock-only drift stable generations real field replacement old Finish isolation reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(1, [Actor(100, 0, center + Vector3.UnitZ * 10, dead: true)]),
            At(2, [], removed: [100]).Replace("\"LocalPlayerId\":100", "\"LocalPlayerId\":101"),
            At(3, [Actor(100, 0, center + Vector3.UnitZ * 4)]), At(4, [])]);
        controller.Seek(1.5);
        Check(Values("PredictedDamage").Single() is { } && dangers() == 1, "Dead bait target lost damage.");
        controller.Seek(2.5);
        Check(Values("PredictedDamage").Length == 1, "Missing target still in party roster lost damage.");
        controller.Seek(3.5);
        Check(Shown("/charge/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")) is { } rebuilt
            && (bool)Value(rebuilt, "OutlineOnly") && dangers() == 1, "Rebuilt viewer ID outline.");
        var ambient = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        Check(ambient.Any(a => ((string)Value(a, "Id")).EndsWith("/avoid"))
            && !ambient.Any(a => ((string)Value(a, "Id")).Contains("/party/")),
            "Rebuilt viewer reference navigation rebound frozen target.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage16 dead absent roster target damage rebuilt viewer ID outline reference avoid dynamic frozen target");

        load(guid, [Header(796), At(0, Initial(), true),
            StatusEvent(.5, 100, 160, 0, true), StatusEvent(.6, 100, 1209, 0, true),
            StatusEvent(.7, 100, 1209, 0, false),
            At(1, [Actor(100, 0, center + Vector3.UnitZ * 30)]),
            StatusEvent(1.1, 100, 1722, 0, true), At(2, [])]);
        controller.Seek(.65);
        Check(Shown("/knockback/").Length == 0, "Role category gain did not suppress projection.");
        controller.Seek(.8);
        Check(Shown("/knockback/").Length == 2, "Role category loss resurrected an older role buff.");
        controller.Seek(1.5);
        Check(Shown("/knockback/").Length == 0 && !Hints(center + Vector3.UnitZ * 30).Contains("About to be knocked into danger!"),
            "Immune outside viewer received projection or KB danger.");
        controller.Seek(.8);
        stop();
        Console.WriteLine("PASS native Carnivale Stage16 production status event category overwrite loss expiry independent job immunity outside viewer no false knockback warning reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(5, [Actor(2, 0x233C, center)]),
            At(6, [Actor(2, 0x233C, center, cast: Cast(14873, 4, center + Vector3.UnitX * 50, target: 100, startedAt: 6))]),
            At(7, [])]);
        controller.Seek(5.5);
        Check(Values("CarnivaleCharges").Length == 0 && Value(Encounter(), "CarnivaleChargeKnockback") == null, "Null raw did not Finish.");
        controller.Seek(6.5);
        Check(Values("CarnivaleCharges").Length == 1 && Value(Encounter(), "CarnivaleChargeKnockback") != null,
            "Identical parameters after observed null failed to restart.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage16 identical raw parameters repeated after null baseline reset creates new charge and independent knockback reverse Seek");
    }
}
