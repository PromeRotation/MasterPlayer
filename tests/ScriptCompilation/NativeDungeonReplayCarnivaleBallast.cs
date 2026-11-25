using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleBallast(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Sequences() => ((IEnumerable)Value(Encounter(), "CarnivaleBallasts")).Cast<object>().ToArray();
        object[] Shown() => previews().Where(a => ((string)Value(a, "Id")).Contains("carnivale/ballast/")).ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action, double? eventTime = null)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(eventTime ?? t) } });
        object[] Initial() => [Actor(1, 0x26F9, center, combat: true), Actor(2, 0x233C, center), Actor(100, 0, center)];

        load(guid, [Header(796), At(0, Initial(), true), CastEvent(.1, 1, 14893, 1, center, .7f),
            Cancel(.2, 1, 14893), EffectEvent(8, 1, 14955, 1, center), At(10, []),
            CastEvent(11, 2, 14894, 3, center, .7f), Cancel(11.1, 2, 14894),
            CastEvent(12, 2, 14895, 3, center, .7f), At(12.1, [], removed: [2]),
            At(13, [Actor(3, 0x233C, center)]), CastEvent(13.1, 3, 14896, 3, center, .7f),
            Cancel(13.2, 3, 14896), At(14, [])]);
        controller.Seek(.15);
        var sequence = Sequences().Single();
        Check(Shown().Length == 3 && dangers() == 3 && (Vector3)Value(sequence, "Origin") == center
            && (float)Value(sequence, "Rotation") == .7f && (DateTime)Value(sequence, "Activation") == Start.AddSeconds(5),
            "Ballast creation clock or showall.");
        var source = Assembly.Load("MasterPlayer.MechanicRuntime");
        var angle = source.GetType("BossMod.Angle")!;
        var position = source.GetType("BossMod.WPos")!;
        var sourceRotation = Activator.CreateInstance(angle, [.7f])!;
        var sourceOrigin = Activator.CreateInstance(position, [100f, 100f])!;
        var compared = 0;
        for (var order = 0; order < 3; ++order)
        {
            var half = Activator.CreateInstance(angle, [135f * (MathF.PI / 180)])!;
            var type = source.GetType(order == 0 ? "BossMod.AOEShapeCone" : "BossMod.AOEShapeDonutSector")!;
            var constructor = type.GetConstructors().Single();
            var args = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            if (order == 0) { args[0] = 5.5f; args[1] = half; }
            else { args[0] = order == 1 ? 5.5f : 10.5f; args[1] = order == 1 ? 10.5f : 15.5f; args[2] = half; }
            var oracle = constructor.Invoke(args);
            dynamic geometry = Value(Shown().Single(a => ((string)Value(a, "Id")).EndsWith("/" + order)), "Geometry");
            for (var x = -16.13f; x < 16; x += .37f)
                for (var z = -16.17f; z < 16; z += .41f)
                {
                    var p = center + new Vector3(x, 0, z);
                    var expected = (bool)type.GetMethod("Check", [position, position, angle])!.Invoke(oracle,
                        [Activator.CreateInstance(position, [p.X, p.Z]), sourceOrigin, sourceRotation])!;
                    Check((bool)geometry.Contains(p, 0f) == expected, $"Ballast source shape mismatch {order} {p}.");
                    ++compared;
                }
        }
        controller.Seek(10.5);
        Check(Sequences().Length == 1 && Shown().Length == 3 && dangers() == 3, "Visual cancel, deadline or Effect advanced sequence.");
        controller.Seek(11.15);
        Check(Shown().Length == 2 && dangers() == 2 && (int)Value(Sequences().Single(), "Order") == 1
            && Math.Abs(((DateTime)Value(Sequences().Single(), "Activation") - Start.AddSeconds(11.7)).TotalSeconds) < .02,
            "Helper cancel wrong order/consumer clock.");
        controller.Seek(12.5);
        Check(Shown().Length == 1 && dangers() == 1 && (int)Value(Sequences().Single(), "Order") == 2, "Helper destroyed did not Finish.");
        controller.Seek(13.5);
        Check(Shown().Length == 0 && dangers() == 0 && Sequences().Length == 0, "Third Finish cleanup.");
        controller.Seek(.15);
        manager.SetMechanismEnabled(guid, "来自亚拉戈的威胁——斗兽系统", false);
        controller.Seek(.16);
        Check(previews().Length == 0 && dangers() == 0, "Ballast disabled owner.");
        manager.SetMechanismEnabled(guid, "来自亚拉戈的威胁——斗兽系统", true);
        stop();
        Console.WriteLine($"PASS native Carnivale Stage15 production Ballast all three 270 degree bands source oracle {compared} points shared clock cancel deadline effect helper destruction three stages reverse Seek disabled owner");

        foreach (var (action, shape, p1, p2) in new[]
        {
            (14891u, "RayRectangleTemplate", 32.3f, 4f), (14892u, "CircleTemplate", 12.3f, 0f),
            (14970u, "DonutTemplate", 8f, 20f), (14968u, "FanTemplate", 7.1f, MathF.PI * 2 / 3),
            (14899u, "CircleTemplate", 7.2f, 0f),
        })
        {
            load(guid, [Header(796), At(0, Initial(), true),
                At(.1, [Actor(2, action == 14899 ? 0x26FBu : 0x233Cu, center,
                    cast: Cast(action, 3, center, rotation: .3f), dead: action == 14899)]),
                EffectEvent(6, 2, action, 1), At(9, []),
                At(10, [Actor(2, 0x233C, center)]), At(11, [])]);
            controller.Seek(.5);
            var casts = ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Select(c => Value(c, "Value")).ToArray();
            var template = Value(Value(casts.Single(), "Rule"), "Shape");
            Check(template.GetType().Name == shape && dangers() == 1
                && previews().Any(a => ((string)Value(a, "Id")).Contains("cast/")), "Stage15 simple AOE.");
            Check((float)Value(template, shape switch { "RayRectangleTemplate" => "Length", "DonutTemplate" => "InnerRadius", _ => "Radius" }) == p1
                && (p2 == 0 || Math.Abs((float)Value(template, shape switch
                    { "RayRectangleTemplate" => "MaxLocalX", "DonutTemplate" => "OuterRadius", _ => "AngleRadians" }) - p2) < .00001f), "Stage15 exact dimensions.");
            controller.Seek(9.5);
            Check(dangers() == 1, "Stage15 AOE expired without Finish.");
            controller.Seek(10.5);
            Check(dangers() == 0, "Stage15 observed end leaked.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage15 production five simple AOE exact dimensions dead serpent scope effect deadline observed end double view navigation reverse Seek");

        load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
            At(.1, [Actor(1, 0x26F9, center), Actor(4, 0x26FA, center, combat: true), Actor(5, 0x26FB, center)]),
            At(1, [Actor(1, 0x26F9, center, combat: true)]), CastEvent(1.1, 1, 14890, 7, center),
            At(2, [Actor(1, 0x26F9, center, combat: true, dead: true)]), At(3, [])]);
        controller.Seek(.5);
        Check(Value(Encounter(), "Active") == null && ((string)Value(Encounter(), "CarnivalePrecombatHint")).Contains("Acorn Bomb")
            && dangers() == 0, "Stage15 add pull or preparation.");
        controller.Seek(1.15);
        var interrupt = ((IEnumerable)Value(Encounter(), "CarnivaleInterruptCasts")).Cast<object>().Single();
        Check((uint)Value(Value(interrupt, "Rule"), "Action") == 14890 && dangers() == 0
            && ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 0
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 14890 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 14971 }),
            "Stage15 High Voltage intent or component scope.");
        var adds = previews().Where(a => ((string)Value(a, "Id")).Contains("carnivale/enemy/")
            && !((string)Value(a, "Id")).EndsWith("/1")).ToArray();
        Check(adds.Length == 2 && adds.All(a => (Vector4)Value(a, "Color") == new Vector4(1, 128f / 255, 0, 1)), "Stage15 add colours.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0, "Stage15 primary completion waited adds.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage15 production precombat only hint primary pull completion interrupt intent no automatic sleep or extra mechanics object colour owner cleanup");

        var secondOrigin = center + Vector3.UnitX * 10;
        load(guid, [Header(796), At(0, [Actor(1, 0x26F9, center, combat: true,
                cast: Cast(14893, 1, center, .3f, startedAt: 0)), Actor(2, 0x233C, center), Actor(100, 0, center)], true),
            At(1, [Actor(1, 0x26F9, center, combat: true, cast: Cast(14893, 1, secondOrigin, .3f, startedAt: 0))]),
            CastEvent(2, 2, 14894, 3, secondOrigin, .3f), Cancel(2.1, 2, 14894),
            JsonSerializer.Serialize(new { t = 2.2, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = .3f,
                data = new { SourceId = 1ul, ActionId = 14893u, CastTime = 1f, EffectPosition = Pose(secondOrigin + Vector3.UnitX), Timestamp = Start } }),
            At(3, [])]);
        controller.Seek(2.3);
        Check(Sequences().Length == 2 && (Vector3)Value(Sequences()[0], "Origin") == center
            && (int)Value(Sequences()[0], "Order") == 0
            && (Vector3)Value(Sequences()[1], "Origin") == secondOrigin && (int)Value(Sequences()[1], "Order") == 1
            && Shown().Length == 5 && dangers() == 5, "Current visual correction rewrote an older same-clock sequence.");
        controller.Seek(.5);
        Check(Shown().Length == 3 && dangers() == 3, "Same-clock sequence reverse Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage15 production same source same clock raw visual replacement independent sequence generations packet correction cannot rewrite old or advanced sequence reverse Seek");
    }
}
