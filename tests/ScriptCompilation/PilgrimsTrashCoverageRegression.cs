using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PromeRotation.LogSystem;

internal static class PilgrimsTrashCoverageRegression
{
    public static void Run(Assembly assembly, dynamic runtime)
    {
        var database = assembly.GetType("MasterPlayer.Dodge.AoeShapeDatabase")!;
        var descriptorType = assembly.GetType("MasterPlayer.Dodge.AoeShapeDescriptor")!;
        var lookup = database.GetMethod("TryGetShape", [typeof(uint), typeof(float), descriptorType.MakeByRefType()])!;
        var templateMethod = database.GetMethod("CreateTemplate", BindingFlags.Static | BindingFlags.NonPublic,
            [descriptorType])!;
        foreach (var (action, inner, outer) in new[] { (44732u, 5f, 30f), (44758u, 5f, 60f),
            (42523u, 0f, 45f), (43133u, 0f, 46f) })
        {
            object?[] query = [action, 20f, null];
            Check((bool)lookup.Invoke(null, query)!, "Special trash shapes must not depend on generic sheet inference.");
            dynamic template = templateMethod.Invoke(null, [query[2]])!;
            dynamic shape = template.Resolve(Vector3.Zero, 0f, null);
            Check((bool)shape.Contains(new Vector3(0, 0, outer - .1f), 0f)
                && !(bool)shape.Contains(new Vector3(0, 0, outer + .1f), 0f), "Preserve the actual outer radius without a hitbox.");
            if (inner > 0)
                Check(!(bool)shape.Contains(new Vector3(0, 0, inner - .1f), 0f)
                    && (bool)shape.Contains(new Vector3(0, 0, inner + .1f), 0f), "Preserve the R5 safe inner circle.");
        }

        var type = assembly.GetType("MasterPlayer.Dodge.DodgeController")!;
        var controller = RuntimeHelpers.GetUninitializedObject(type);
        foreach (var name in new[] { "_registeredHandles", "_pilgrimsFollowUps" })
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(controller, Activator.CreateInstance(field.FieldType));
        }
        var track = type.GetMethod("TrackPilgrimsFollowUpCast", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var effect = type.GetMethod("HandlePilgrimsFollowUpEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var clear = type.GetMethod("ClearPilgrimsFollowUps", BindingFlags.Instance | BindingFlags.NonPublic)!;
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var margin = (float)settings.DodgeDangerMargin;
        settings.DodgeDangerMargin = 0f;
        object manager = runtime.Manager;
        var poseOverride = manager.GetType().GetProperty("EntityPoseOverride", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalOverride = poseOverride.GetValue(manager);
        var origin = new Vector3(100, 0, 100);
        var rotation = 0f;
        var present = true;
        poseOverride.SetValue(manager, (Func<uint, (Vector3, float)?>)(id => id == 42 && present ? (origin, rotation) : null));
        dynamic[] Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas).Cast<dynamic>().ToArray();
        var cast = new LogSystemCastStartEvent { SourceId = 42, ActionId = 44759, CastTime = 2.7f, EffectPosition = origin };
        var handle = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.SafeZoneAreaHandle")!);
        var pose = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.StaticTransform")!, [origin, 0f]);
        void Start(uint territory = 1289) => track.Invoke(controller, [cast, handle, pose, territory]);
        var first = new LogSystemActionEffectEvent { SourceId = 42, ActionId = 44759, GlobalSequence = 1 };
        try
        {
            Start();
            effect.Invoke(controller, [first with { ActionId = 44760, GlobalSequence = 2 }]);
            Check(Dangers().Length == 0, "A late follow-up cannot start a new sequence before its first effect.");
            effect.Invoke(controller, [first]);
            effect.Invoke(controller, [first with { TargetIndex = 1 }]);
            Check(Dangers().Length == 1, "Hail of Heels must create exactly one continuous frontal danger after the cast hit.");
            dynamic shape = Dangers().Single().Geometry;
            Check((bool)shape.Contains(origin + new Vector3(0, 0, 7.9f), 0f)
                && !(bool)shape.Contains(origin + new Vector3(0, 0, 8.1f), 0f)
                && !(bool)shape.Contains(origin - Vector3.UnitZ, 0f), "Hail of Heels must preserve R8 and a 180-degree front.");
            rotation = MathF.PI;
            shape = Dangers().Single().Geometry;
            Check((bool)shape.Contains(origin - Vector3.UnitZ, 0f)
                && !(bool)shape.Contains(origin + Vector3.UnitZ, 0f), "Subsequent kicks follow the source's current facing.");
            present = false;
            Check(Dangers().Length == 0, "Missing sources must disable their dynamic frontal danger.");
            present = true;
            for (var i = 0; i < 3; ++i)
            {
                var hit = first with { ActionId = 44760, GlobalSequence = (uint)(10 + i) };
                effect.Invoke(controller, [hit with { SourceId = 99 }]);
                effect.Invoke(controller, [hit]);
                effect.Invoke(controller, [hit with { TargetIndex = 1 }]);
                effect.Invoke(controller, [first with { ActionId = 44760, GlobalSequence = 10 }]);
                Check(Dangers().Length == (i == 2 ? 0 : 1), "Exactly three distinct kick effects finish the sequence; targets and late duplicates cannot advance it.");
            }
            foreach (var ending in new LogSystemEvent[] {
                new LogSystemCancelCastEvent { SourceId = 42, ActionId = 44759 },
                new LogSystemInterruptCastEvent { SourceId = 42, ActionId = 44759 },
                new LogSystemObjectRemoveEvent { SourceId = 42 },
                new LogSystemBattleEndedEvent(), new LogSystemTerritoryEvent() })
            {
                Start();
                effect.Invoke(controller, [first]);
                effect.Invoke(controller, [ending]);
                Check(Dangers().Length == 0, "Every lifecycle ending must clear pending and published kicks.");
            }
            Start(1200);
            effect.Invoke(controller, [first]);
            Check(Dangers().Length == 0, "Hail follow-ups must remain isolated to Pilgrims territories.");
        }
        finally
        {
            clear.Invoke(controller, null);
            settings.DodgeDangerMargin = margin;
            poseOverride.SetValue(manager, originalOverride);
            runtime.Manager.ClearAllAreas();
        }
        Console.WriteLine("PASS Pilgrims trash coverage: R5 inner donuts, R45/R46 circles, three dynamic Hail of Heels follow-ups, source/sequence deduplication and lifecycle cleanup");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
