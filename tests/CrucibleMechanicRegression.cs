using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Dodge;
using MasterPlayer.Scripting;
using MasterPlayer.Scripts;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

internal static class CrucibleMechanicRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("B26 clears the resolved first cleave and never predicts a third", () =>
        {
            using var fight = new GargoyleFight();
            fight.Send(Charge(100));
            Check(fight.Areas.Count == 2, "A dash must predict exactly two cleaves.");
            fight.Send(Hit(101));
            Check(fight.Areas.Count == 1, "The first hit must immediately remove the first cleave.");
            fight.Send(Hit(102));
            Check(fight.Areas.Count == 0, "The second hit must clear both drawings instead of spawning a third cleave.");
        });

        run("B26 multi-target packets advance each cleave only once", () =>
        {
            using var fight = new GargoyleFight();
            fight.Send(Charge(100));
            var handles = fight.Areas.Select(a => a.Handle).ToArray();
            fight.Send(Charge(100) with { TargetIndex = 1 });
            Check(fight.Areas.Select(a => a.Handle).SequenceEqual(handles), "Duplicate dash targets must not restart the sequence.");
            fight.Send(Hit(101));
            var second = fight.Areas.Single().Handle;
            fight.Send(Hit(101) with { TargetIndex = 1 });
            Check(fight.Areas.Single().Handle == second, "Duplicate first-hit targets must not consume the second cleave.");
            fight.Send(Hit(102));
            fight.Send(Hit(102) with { TargetIndex = 1 });
            fight.Send(Hit(103));
            Check(fight.Areas.Count == 0, "Repeated or late hits after completion must not recreate a hazard.");
            fight.Send(Charge(104));
            Check(fight.Areas.Count == 2, "The next dash must start a fresh two-cleave sequence.");
        });

        run("B26 accepts damage helpers and keeps the predicted pose when a snapshot is missing", () =>
        {
            using var fight = new GargoyleFight();
            fight.Send(Charge(100));
            var handles = fight.Areas.Select(a => a.Handle).ToArray();
            fight.Send(Hit(101) with { SourceId = 2, SourcePosition = null, EffectPosition = null, SourceRotation = null });
            Check(fight.Areas.Count == 1 && fight.Areas[0].Handle == handles[1], "Missing correction data must retain the second prediction while clearing the first.");
            fight.Send(Hit(102) with { SourceId = 3 });
            Check(fight.Areas.Count == 0, "A missing first-hit pose must not prevent final cleanup.");
        });

        run("B26 second-cleave correction uses the hit position and opposite direction", () =>
        {
            using var fight = new GargoyleFight();
            fight.Send(Charge(100));
            var origin = new Vector3(7f, 3f, -9f);
            const float rotation = 0.8f;
            fight.Send(Hit(101) with { SourcePosition = origin, SourceRotation = rotation });
            var second = (FanGeometry)fight.Areas.Single().Geometry;
            Check(second.Position == origin, "Correction must use the caster position at the first hit.");
            Check(second.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation + MathF.PI, 10f)) &&
                !second.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation, 10f)), "The remaining cleave must face opposite the first hit.");
            fight.Expire();
            Check(fight.Areas.Count == 0, "Missing hit events must still have a timeout cleanup.");
            fight.Send(Hit(102));
            Check(fight.Areas.Count == 0, "A late hit after timeout must not revive the sequence.");
        });

        run("B26 missing first-hit event does not turn the second hit into another prediction", () =>
        {
            using var fight = new GargoyleFight();
            fight.Send(Charge(100));
            fight.ExpireFirst();
            fight.Send(Hit(102));
            Check(fight.Areas.Count == 0, "After the first cleave's deadline, a hit must finish the sequence even if its first event was missed.");
        });

        foreach (var (action, fullAngle) in new[] { (0xBE8Au, 90f), (0xBE8Bu, 150f), (0xBE8Cu, 150f) })
        {
            run($"B29 action {action} uses the BMR cone instead of generic angle inference", () =>
            {
                Check(AoeShapeDatabase.TryGetShape(action, out var descriptor) && descriptor != null, "A verified shape must be available without game-sheet inference.");
                Check(!descriptor!.FollowCaster, "The cone must remain at the cast location instead of tracking the actor.");
                var origin = new Vector3(120f, 15f, -433f);
                const float rotation = 0.7f;
                var shape = (FanGeometry)AoeShapeDatabase.CreateTemplate(descriptor)!.Resolve(origin, rotation, null);
                Check(shape.Radius == 60f, "BMR specifies a 60-yalm radius.");
                var half = float.DegreesToRadians(fullAngle / 2f);
                Check(shape.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation + half - 0.01f, 30f)) &&
                    !shape.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation + half + 0.01f, 30f)), "Both sides of the cone boundary must match BMR's half-angle.");
                Check(!shape.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation, 61f)), "The cone must not extend past 60 yalms before the configured safety margin.");
            });

            run($"B29 script action {action} keeps the cast location and facing when the actor moves", () =>
            {
                using var fight = new TreantFight();
                var origin = new Vector3(120f, 15f, -433f);
                const float rotation = 0.7f;
                fight.CasterPosition = origin + new Vector3(10f, 0f, 15f);
                fight.CasterRotation = rotation;
                fight.Send(new LogSystemCastStartEvent
                {
                    ActionId = (ushort)action, SourceId = 1, CastTime = 5f,
                    EffectPosition = origin, SourcePosition = fight.CasterPosition, SourceRotation = rotation,
                });
                CheckCone();
                fight.CasterPosition += new Vector3(-20f, 4f, 30f);
                fight.CasterRotation += MathF.PI;
                CheckCone();

                void CheckCone()
                {
                    var shape = (FanGeometry)fight.Areas.Single().Geometry;
                    Check(shape.Position == origin, "The script must anchor the cone to EffectPosition, independent of the actor's position.");
                    Check(shape.Radius == 60f, "The script must use the BMR radius.");
                    var half = float.DegreesToRadians(fullAngle / 2f);
                    foreach (var side in new[] { -1f, 1f })
                        Check(shape.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation + side * (half - 0.01f), 30f)) &&
                            !shape.Contains(GeometryHelper.PointFromRotationDistanceXZ(origin, rotation + side * (half + 0.01f), 30f)),
                            "The script cone must retain both angular boundaries at the cast's original facing.");
                }
            });
        }
    }

    private static LogSystemActionEffectEvent Charge(uint sequence) => new()
    {
        ActionId = 0xC6F4, GlobalSequence = sequence, SourceId = 1, SourceDataId = 0x4CC2,
        EffectPosition = new Vector3(4f, 0f, 7f), SourcePosition = Vector3.Zero, SourceRotation = 0.3f,
    };

    private static LogSystemActionEffectEvent Hit(uint sequence) => Charge(sequence) with { ActionId = 0xBE4E };

    private sealed class GargoyleFight : IDisposable
    {
        private readonly CrucibleHigh1Script _script = new();
        private readonly object _handler;
        private readonly Type _type = typeof(CrucibleHigh1Script).GetNestedType("B26GargoyleHandler", BindingFlags.NonPublic)!;
        public IReadOnlyList<DangerAreaSnapshot> Areas => _script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas;

        public GargoyleFight()
        {
            _handler = Activator.CreateInstance(_type, _script)!;
            _type.GetMethod("Activate")!.Invoke(_handler, [Vector3.Zero]);
        }

        public void Send(LogSystemActionEffectEvent e) => _type.GetMethod("OnEffect")!.Invoke(_handler, [e]);

        public void Expire()
        {
            _type.GetField("_cleaveBExpireUtc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_handler, DateTime.UtcNow.AddSeconds(-1));
            _type.GetMethod("Update")!.Invoke(_handler, [0.01f]);
        }

        public void ExpireFirst()
            => _type.GetField("_cleaveAExpireUtc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_handler, DateTime.UtcNow.AddSeconds(-1));

        public void Dispose() => _script.SafeZone.Dispose();
    }

    private sealed class TreantFight : IDisposable
    {
        private readonly CrucibleHigh1Script _script = new();
        private readonly Type _type = typeof(CrucibleHigh1Script).GetNestedType("B29TreantHandler", BindingFlags.NonPublic)!;
        private readonly object _handler;
        private readonly PropertyInfo _data = typeof(Svc).GetProperty(nameof(Svc.Data))!;
        private readonly PropertyInfo _objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        private readonly object? _originalData;
        private readonly object? _originalObjects;
        public Vector3 CasterPosition { get; set; }
        public float CasterRotation { get; set; }
        public IReadOnlyList<DangerAreaSnapshot> Areas => _script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas;

        public TreantFight()
        {
            _handler = Activator.CreateInstance(_type, _script)!;
            var trackerType = typeof(CrucibleHigh1Script).GetNestedType("CastAoeTracker", BindingFlags.NonPublic)!;
            typeof(CrucibleHigh1Script).GetField("_tracker", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_script, Activator.CreateInstance(trackerType, _script));
            _originalData = _data.GetValue(null);
            _originalObjects = _objects.GetValue(null);
            _data.SetValue(null, new ScriptDataManager());
            var caster = ScriptServiceProxy.Create<IGameObject>((method, _) => method switch
            {
                "get_EntityId" => 1u,
                "get_Position" => CasterPosition,
                "get_Rotation" => CasterRotation,
                _ => throw new NotSupportedException(method),
            });
            _objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((method, args) => method switch
            {
                "get_Length" => 1,
                "get_Item" => (int)args[0]! == 0 ? caster : null,
                _ => throw new NotSupportedException(method),
            }));
        }

        public void Send(LogSystemCastStartEvent e) => _type.GetMethod("OnCastStart")!.Invoke(_handler, [e]);

        public void Dispose()
        {
            _data.SetValue(null, _originalData);
            _objects.SetValue(null, _originalObjects);
            _script.SafeZone.Dispose();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
