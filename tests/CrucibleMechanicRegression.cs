using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

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
                using var fight = new CastFight();
                var origin = new Vector3(120f, 15f, -433f);
                const float rotation = 0.7f;
                fight.CasterPosition = origin + new Vector3(10f, 0f, 15f);
                fight.CasterRotation = rotation + MathF.PI;
                fight.CastActionId = action;
                fight.CastRotation = rotation;
                fight.Send(new LogSystemCastStartEvent
                {
                    ActionId = (ushort)action, SourceId = 1, CastTime = 5f,
                    EffectPosition = origin, SourcePosition = fight.CasterPosition, SourceRotation = fight.CasterRotation,
                });
                CheckCone();
                fight.CasterPosition += new Vector3(-20f, 4f, 30f);
                fight.CasterRotation += MathF.PI;
                fight.CastRotation += 1f;
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

            run($"B29 generic action {action} captures cast facing and rejects an ended cast", () =>
            {
                using var fight = new CastFight();
                fight.CastActionId = action;
                fight.CastRotation = 0.7f;
                var cast = new LogSystemCastStartEvent
                {
                    ActionId = (ushort)action, SourceId = 1,
                    EffectPosition = new Vector3(120f, 15f, -433f), SourceRotation = -2f,
                };
                Check(AoeShapeDatabase.TryGetShape(action, out var descriptor), "The known gust must be available.");
                Check(AoeShapeDatabase.TryCreateBinding(cast, descriptor!, out var binding) && binding is StaticTransform,
                    "The generic path must freeze a valid native cast pose.");
                var pose = (StaticTransform)binding;
                Check(pose.Position == cast.EffectPosition && pose.Rotation == 0.7f,
                    "Both generic and scripted gusts must use native CastRotation, independent of SourceRotation.");
                fight.CastRotation += 1f;
                Check(pose.Rotation == 0.7f, "Later native rotation changes must not rotate the registered AOE.");
                fight.IsCasting = false;
                Check(!AoeShapeDatabase.TryCreateBinding(cast, descriptor!, out _), "An ended cast must not fall back to ordinary SourceRotation.");
            });
        }

        foreach (var (rotation, expected) in new[]
        {
            (0f, new Vector3(920f, 15f, -440f)),
            (MathF.PI / 2f, new Vector3(900f, 15f, -420f)),
        })
            run($"B31 breath uses native cast facing {rotation} rather than actor facing", () =>
            {
                using var fight = new CastFight("B31DragonHandler");
                fight.CasterPosition = new Vector3(927f, 15f, -411f);
                fight.CastActionId = 48807;
                fight.CastRotation = rotation;
                fight.CasterRotation = rotation + MathF.PI / 2f;
                fight.Send(new LogSystemCastStartEvent
                {
                    ActionId = 48807, SourceId = 1, CastTime = 3f,
                    SourcePosition = fight.CasterPosition, EffectPosition = fight.CasterPosition,
                    SourceRotation = fight.CasterRotation,
                });
                var spot = (CircleGeometry)fight.Goals.Single().Geometry;
                Check(Vector3.Distance(spot.Position, expected) < 0.001f && spot.Radius == 7f,
                    "The breath goal must be 20 yalms behind the cast facing measured from arena center.");
                fight.CastRotation += 1f;
                Check(fight.Goals.Single().Geometry.Position == spot.Position, "A captured breath direction must stay fixed after registration.");
            });

        foreach (var (name, invalidate) in new (string, Action<CastFight>)[]
        {
            ("missing actor", fight => fight.CasterAvailable = false),
            ("zero address", fight => fight.HasAddress = false),
            ("ended cast", fight => fight.IsCasting = false),
            ("different action", fight => fight.CastActionId = 1),
            ("non-finite rotation", fight => fight.CastRotation = float.NaN),
        })
            run($"cast-facing mechanics reject {name} without drawing an invented direction", () =>
            {
                foreach (var (handler, action) in new[] { ("B29TreantHandler", (ushort)48778), ("B31DragonHandler", (ushort)48807) })
                {
                    using var fight = new CastFight(handler);
                    fight.CastActionId = action;
                    invalidate(fight);
                    fight.Send(new LogSystemCastStartEvent { ActionId = action, SourceId = 1, CastTime = 3f, SourceRotation = 0.7f });
                    Check(fight.Areas.Count == 0 && fight.Goals.Count == 0, "Unavailable cast facing must not produce a hazard or safe spot using actor facing.");
                    Check(fight.Errors.Count == 1, "Skipping a prediction must report the unavailable cast facing.");
                }
            });

        run("B29 treant body circle tracks the living treant and clears on death", () =>
        {
            using var fight = new TreantFight();
            fight.Update(0.5f);
            var area = fight.Areas.Single();
            var circle = (CircleGeometry)area.Geometry;
            Check(circle.Radius == 2.2f && circle.Position == new Vector3(120f, 15f, -433f),
                "The living treant must carry a hitbox-sized body circle at its own position.");
            fight.TreantDead = true;
            fight.Update(0.5f);
            Check(fight.Areas.Count == 0, "The body circle must be removed when the treant dies.");
        });

        run("B31 bait guide removes the previous safe spot when the round advances", () =>
        {
            using var fight = new BaitFight();
            var outer = MathF.Sqrt(18.3f * 18.3f - 7f * 7f);
            var route0 = new Vector3(920f - 7f, 15f, -420f - (outer - 7f));
            var route1 = new Vector3(920f - 7f, 15f, -420f - outer);
            fight.SendTether();
            fight.Update(0.4f);
            var first = fight.Goals.Single();
            Check(Vector3.Distance(first.Geometry.Position, route0) < 0.01f, "The first bait guide must sit on route point 1.");
            fight.SpawnPool(route0);
            fight.Update(0.4f);
            Check(fight.Goals.Count == 1, "Switching bait steps must remove the previous safe spot instead of stacking a new one.");
            Check(Vector3.Distance(fight.Goals[0].Geometry.Position, route1) < 0.01f,
                "After the pool lands, the guide must move to route point 2.");
            Check(!fight.Contains(first.Handle), "The previous step's safe spot must be deregistered, not left to time out.");
        });
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

    private sealed unsafe class CastFight : IDisposable
    {
        private readonly CrucibleHigh1Script _script = new();
        private readonly Type _type;
        private readonly object _handler;
        private readonly NativeBattleChara* _native = (NativeBattleChara*)NativeMemory.AllocZeroed((nuint)sizeof(NativeBattleChara));
        private readonly PropertyInfo _data = typeof(Svc).GetProperty(nameof(Svc.Data))!;
        private readonly PropertyInfo _objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        private readonly object? _originalData;
        private readonly object? _originalObjects;
        public Vector3 CasterPosition { get; set; }
        public float CasterRotation { get; set; }
        public bool CasterAvailable { get; set; } = true;
        public bool HasAddress { get; set; } = true;
        public bool IsCasting { get => _native->CastInfo.IsCasting; set => _native->CastInfo.IsCasting = value; }
        public uint CastActionId { get => _native->CastInfo.ActionId; set => _native->CastInfo.ActionId = value; }
        public float CastRotation { get => _native->CastRotation; set => _native->CastRotation = value; }
        public IReadOnlyList<DangerAreaSnapshot> Areas => _script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas;
        public IReadOnlyList<GoalAreaSnapshot> Goals => _script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).GoalAreas;
        public IReadOnlyList<string> Errors => _script.Errors;

        public CastFight(string handlerName = "B29TreantHandler")
        {
            IsCasting = true;
            _native->CastInfo.Rotation = -1.1f;
            _type = typeof(CrucibleHigh1Script).GetNestedType(handlerName, BindingFlags.NonPublic)!;
            _handler = Activator.CreateInstance(_type, _script)!;
            _type.GetMethod("Activate")!.Invoke(_handler, [new Vector3(920f, 15f, -420f)]);
            var trackerType = typeof(CrucibleHigh1Script).GetNestedType("CastAoeTracker", BindingFlags.NonPublic)!;
            typeof(CrucibleHigh1Script).GetField("_tracker", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_script, Activator.CreateInstance(trackerType, _script));
            _originalData = _data.GetValue(null);
            _originalObjects = _objects.GetValue(null);
            _data.SetValue(null, new ScriptDataManager());
            var caster = ScriptServiceProxy.Create<IBattleChara>((method, _) => method switch
            {
                "get_EntityId" => 1u,
                "get_Position" => CasterPosition,
                "get_Rotation" => CasterRotation,
                "get_Address" => HasAddress ? (nint)_native : nint.Zero,
                "get_IsCasting" => IsCasting,
                "get_CastActionId" => CastActionId,
                _ => throw new NotSupportedException(method),
            });
            _objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((method, args) => method switch
            {
                "get_Length" => 1,
                "get_Item" => (int)args[0]! == 0 ? caster : null,
                "SearchByEntityId" => (uint)args[0]! == 1 && CasterAvailable ? caster : null,
                _ => throw new NotSupportedException(method),
            }));
        }

        public void Send(LogSystemCastStartEvent e) => _type.GetMethod("OnCastStart")!.Invoke(_handler, [e]);

        public void Dispose()
        {
            _data.SetValue(null, _originalData);
            _objects.SetValue(null, _originalObjects);
            _script.SafeZone.Dispose();
            NativeMemory.Free(_native);
        }
    }

    private sealed class TreantFight : IDisposable
    {
        private static readonly Vector3 TreantPos = new(120f, 15f, -433f);
        private readonly CrucibleHigh1Script _script = new();
        private readonly Type _type = typeof(CrucibleHigh1Script).GetNestedType("B29TreantHandler", BindingFlags.NonPublic)!;
        private readonly object _handler;
        private readonly PropertyInfo _objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        private readonly object? _originalObjects;
        public bool TreantDead { get; set; }
        public IReadOnlyList<DangerAreaSnapshot> Areas => _script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas;

        public TreantFight()
        {
            _handler = Activator.CreateInstance(_type, _script)!;
            _type.GetMethod("Activate")!.Invoke(_handler, [TreantPos]);
            _originalObjects = _objects.GetValue(null);
            _objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((method, args) => method switch
            {
                "get_Length" => 1,
                "get_Item" => (int)args[0]! == 0 ? Treant : null,
                "SearchByEntityId" => (uint)args[0]! == 1u && !TreantDead ? Treant : null,
                _ => throw new NotSupportedException(method),
            }));
        }

        private IBattleChara Treant => ScriptServiceProxy.Create<IBattleChara>((method, _) => method switch
        {
            "get_EntityId" => 1u,
            "get_NameId" => 14618u,
            "get_ObjectKind" => ObjectKind.BattleNpc,
            "get_Position" => TreantPos,
            "get_HitboxRadius" => 2.2f,
            "get_IsDead" => TreantDead,
            _ => throw new NotSupportedException(method),
        });

        public void Update(float dt) => _type.GetMethod("Update")!.Invoke(_handler, [dt]);

        public void Dispose()
        {
            _objects.SetValue(null, _originalObjects);
            _script.SafeZone.Dispose();
        }
    }

    private sealed unsafe class BaitFight : IDisposable
    {
        private static readonly Vector3 ArenaCenter = new(920f, 15f, -420f);
        private readonly CrucibleHigh1Script _script = new();
        private readonly Type _type = typeof(CrucibleHigh1Script).GetNestedType("B31DragonHandler", BindingFlags.NonPublic)!;
        private readonly object _handler;
        private readonly FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* _poolNative =
            (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)NativeMemory.AllocZeroed(
                (nuint)sizeof(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject));
        private readonly PropertyInfo _objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        private readonly object? _originalObjects;
        private IGameObject? _poolObject;
        public IReadOnlyList<GoalAreaSnapshot> Goals => _script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).GoalAreas;

        public BaitFight()
        {
            _handler = Activator.CreateInstance(_type, _script)!;
            _type.GetMethod("Activate")!.Invoke(_handler, [ArenaCenter]);
            _originalObjects = _objects.GetValue(null);
            _objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((method, args) => method switch
            {
                "get_Length" => _poolObject == null ? 2 : 3,
                "get_Item" => (int)args[0]! switch { 0 => Dragon, 1 => Player, _ => _poolObject },
                "SearchByEntityId" => (uint)args[0]! switch { 1u => Dragon, 2u => Player, 3u => _poolObject, _ => null },
                "get_LocalPlayer" => Player,
                _ => throw new NotSupportedException(method),
            }));
        }

        private static IBattleChara Dragon => ScriptServiceProxy.Create<IBattleChara>((method, _) => method switch
        {
            "get_EntityId" => 1u,
            "get_NameId" => 14628u,
            "get_ObjectKind" => ObjectKind.BattleNpc,
            "get_Position" => ArenaCenter + new Vector3(0f, 0f, -5f),
            "get_IsDead" => false,
            _ => throw new NotSupportedException(method),
        });

        private static IPlayerCharacter Player => ScriptServiceProxy.Create<IPlayerCharacter>((method, _) => method switch
        {
            "get_EntityId" => 2u,
            "get_ObjectKind" => ObjectKind.Pc,
            "get_NameId" => 0u,
            "get_Position" => ArenaCenter,
            "get_IsDead" => false,
            _ => throw new NotSupportedException(method),
        });

        public void SendTether()
            => _type.GetMethod("OnTether")!.Invoke(_handler, [new LogSystemTetherEvent { SourceId = 1, TargetId = 2, TetherId = 0x11 }]);

        public void SpawnPool(Vector3 pos)
            => _poolObject = ScriptServiceProxy.Create<IGameObject>((method, _) => method switch
            {
                "get_EntityId" => 3u,
                "get_ObjectKind" => ObjectKind.EventObj,
                "get_BaseId" => 0x1EB704u,
                "get_Position" => pos,
                "get_Address" => (nint)_poolNative,
                "get_IsDead" => false,
                _ => throw new NotSupportedException(method),
            });

        public void Update(float dt) => _type.GetMethod("Update")!.Invoke(_handler, [dt]);

        public bool Contains(SafeZoneAreaHandle handle) => _script.SafeZone.Manager.ContainsArea(handle);

        public void Dispose()
        {
            _objects.SetValue(null, _originalObjects);
            _script.SafeZone.Dispose();
            NativeMemory.Free(_poolNative);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
