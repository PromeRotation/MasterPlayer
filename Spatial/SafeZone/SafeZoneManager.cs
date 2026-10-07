using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 区域注册中心与快照发布者。
/// <para>所有公共方法仅供主线程调用（典型调用方：Plugin 主流程、<see cref="SafeZoneRegistry"/>、<see cref="SafeZoneMovementSystem"/>）。</para>
/// <para>负责维护运行对象表、分配单调 Handle、解析动态绑定、按 <see cref="MissingEntityPolicy"/> 处理实体丢失、生成不可变 <see cref="SafeZoneSnapshot"/>。</para>
/// <para>Solver 与后台线程只能通过 <see cref="BuildSnapshot"/> 拿到完全解析后的快照，不再触碰任何实体引用。</para>
/// </summary>
public sealed class SafeZoneManager : IDisposable
{
    private readonly SafeZoneSetting _setting;
    private readonly Dictionary<ulong, RuntimeArea> _areas = new();
    private readonly Dictionary<uint, IGameObject?> _entityCache = new();
    private readonly List<SafeZoneAreaHandle> _pendingRemovals = new();
    private IGeometryShape? _arenaBoundary;
    private ulong _nextHandleId = 1UL;
    private int _version;
    private bool _disposed;
    internal Func<uint, (System.Numerics.Vector3 Position, float Rotation)?>? EntityPoseOverride { get; set; }

    public SafeZoneManager(SafeZoneSetting setting)
    {
        _setting = setting ?? throw new ArgumentNullException(nameof(setting));

    }

    /// <summary>当前版本号。任何注册/移除/清空/边界变更都会自增；BuildSnapshot 本身不会动它。</summary>
    public int Version => _version;

    /// <summary>设置副本边界。当前只接受静态 <see cref="IGeometryShape"/>。</summary>
    public void SetArenaBoundary(IGeometryShape boundary)
    {
        ThrowIfDisposed();
        _arenaBoundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        _version++;
    }

    /// <summary>清除副本边界。</summary>
    public void ClearArenaBoundary()
    {
        ThrowIfDisposed();
        if (_arenaBoundary is null)
        {
            return;
        }

        _arenaBoundary = null;
        _version++;
    }

    /// <summary>注册危险区。</summary>
    public SafeZoneAreaHandle AddDangerArea(
        GeometryTemplate template,
        TransformBinding binding,
        IReadOnlyList<DangerWindow> dangerWindows)
    {
        ThrowIfDisposed();
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (binding is null) throw new ArgumentNullException(nameof(binding));
        if (dangerWindows is null) throw new ArgumentNullException(nameof(dangerWindows));

        var handle = AllocateHandle(AreaKind.Danger);
        _areas[handle.Id] = new RuntimeArea(handle, template, binding, dangerWindows, Array.Empty<GoalWindow>());
        _version++;
        return handle;
    }

    /// <summary>注册目标区。</summary>
    public SafeZoneAreaHandle AddGoalArea(
        GeometryTemplate template,
        TransformBinding binding,
        IReadOnlyList<GoalWindow> goalWindows)
    {
        ThrowIfDisposed();
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (binding is null) throw new ArgumentNullException(nameof(binding));
        if (goalWindows is null) throw new ArgumentNullException(nameof(goalWindows));

        var handle = AllocateHandle(AreaKind.Goal);
        _areas[handle.Id] = new RuntimeArea(handle, template, binding, Array.Empty<DangerWindow>(), goalWindows);
        _version++;
        return handle;
    }

    /// <summary>注册硬障碍。障碍区不带时间窗口。</summary>
    public SafeZoneAreaHandle AddObstacle(GeometryTemplate template, TransformBinding binding)
    {
        ThrowIfDisposed();
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (binding is null) throw new ArgumentNullException(nameof(binding));

        var handle = AllocateHandle(AreaKind.Obstacle);
        _areas[handle.Id] = new RuntimeArea(handle, template, binding, Array.Empty<DangerWindow>(), Array.Empty<GoalWindow>());
        _version++;
        return handle;
    }

    /// <summary>
    /// 按 handle 更新区域。
    /// <para>任一 <paramref name="template"/> / <paramref name="binding"/> / <paramref name="dangerWindows"/> / <paramref name="goalWindows"/> 为 null 表示保持原值；DangerWindows 和 GoalWindows 不会被互换写入。</para>
    /// </summary>
    public bool UpdateArea(
        SafeZoneAreaHandle handle,
        GeometryTemplate? template = null,
        TransformBinding? binding = null,
        IReadOnlyList<DangerWindow>? dangerWindows = null,
        IReadOnlyList<GoalWindow>? goalWindows = null,
        bool incrementVersion = true)
    {
        ThrowIfDisposed();
        if (!_areas.TryGetValue(handle.Id, out var area))
        {
            return false;
        }

        if (template is not null) area.Template = template;
        if (binding is not null)
        {
            area.Binding = binding;
            area.LastResolvedGeometry = null;
        }

        if (dangerWindows is not null && area.Kind == AreaKind.Danger)
        {
            area.DangerWindows = dangerWindows;
        }

        if (goalWindows is not null && area.Kind == AreaKind.Goal)
        {
            area.GoalWindows = goalWindows;
        }

        if (incrementVersion)
        {
            _version++;
        }

        return true;
    }

    /// <summary>按 handle 移除区域。返回是否实际移除。</summary>
    public bool RemoveArea(SafeZoneAreaHandle handle)
    {
        ThrowIfDisposed();
        if (!_areas.Remove(handle.Id))
        {
            return false;
        }

        _version++;
        return true;
    }

    /// <summary>判断 handle 当前是否仍然指向有效的运行对象。</summary>
    /// <remarks>
    /// UI / 调用方在 <c>ClearAllAreas</c>（Territory 切换、玩家死亡、战斗结束）或
    /// <see cref="MissingEntityPolicy.RemoveArea"/> 触发后无法直接感知；
    /// 通过该方法每帧同步本地缓存的 handle 即可正确回退到"未注册"状态。
    /// </remarks>
    public bool ContainsArea(SafeZoneAreaHandle handle)
    {
        ThrowIfDisposed();
        return handle.IsValid && _areas.ContainsKey(handle.Id);
    }

    /// <summary>清空所有危险区。</summary>
    public void ClearDangerAreas() => ClearByKind(AreaKind.Danger);

    /// <summary>清空所有目标区。</summary>
    public void ClearGoalAreas() => ClearByKind(AreaKind.Goal);

    /// <summary>清空所有障碍。</summary>
    public void ClearObstacles() => ClearByKind(AreaKind.Obstacle);

    /// <summary>清空所有运行时注册内容（含边界）。Territory 切换、玩家死亡、战斗结束时由生命周期事件自动调用。</summary>
    public void ClearAllAreas()
    {
        ThrowIfDisposed();
        if (_areas.Count == 0 && _arenaBoundary is null)
        {
            return;
        }

        _areas.Clear();
        _arenaBoundary = null;
        _version++;
    }

    /// <summary>
    /// 移除所有窗口都已整体过期的限时危险区/目标区。返回移除的数量。
    /// <para>判定：区域至少有一个窗口、每个窗口都有有限结束时间、且 <c>max(结束时间) &lt; utcNow</c>。</para>
    /// <para>不回收：任一窗口无结束时间（永久）、窗口列表为空的危险/目标区、以及障碍（无时间窗口）。</para>
    /// <para>必须传实时 <see cref="DateTime.UtcNow"/>，否则会提前删掉未来才过期的区域。只读 <c>DangerUntil</c>/<c>GoalUntil</c>，不读 <c>TelegraphFrom</c>。</para>
    /// </summary>
    public int PruneExpiredAreas(DateTime utcNow)
    {
        ThrowIfDisposed();

        List<ulong>? expired = null;
        foreach (var pair in _areas)
        {
            if (IsAreaExpired(pair.Value, utcNow))
            {
                (expired ??= new List<ulong>()).Add(pair.Key);
            }
        }

        if (expired is null)
        {
            return 0;
        }

        var removed = 0;
        foreach (var id in expired)
        {
            if (_areas.Remove(id))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            _version++;
        }

        return removed;
    }

    /// <summary>判断区域是否所有时间窗口都已整体过期。障碍与无窗口区域恒不过期。</summary>
    private static bool IsAreaExpired(RuntimeArea area, DateTime utcNow)
    {
        switch (area.Kind)
        {
            case AreaKind.Danger:
            {
                var windows = area.DangerWindows;
                if (windows.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < windows.Count; i++)
                {
                    var until = windows[i].DangerUntil;
                    if (until is null || until.Value >= utcNow)
                    {
                        return false;
                    }
                }

                return true;
            }

            case AreaKind.Goal:
            {
                var windows = area.GoalWindows;
                if (windows.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < windows.Count; i++)
                {
                    var until = windows[i].GoalUntil;
                    if (until is null || until.Value >= utcNow)
                    {
                        return false;
                    }
                }

                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// 构造指定 UTC 时间下的不可变快照。
    /// <para>调用此方法会在主线程一次性解析所有动态绑定（每帧重新解析）。</para>
    /// <para><see cref="SafeZoneSetting.EnableSafeZone"/> 为 false 时返回仅含 ArenaBoundary 的最小快照。</para>
    /// </summary>
    public SafeZoneSnapshot BuildSnapshot(DateTime utcNow)
    {
        ThrowIfDisposed();

        if (!_setting.EffectiveEnableSafeZone)
        {
            return new SafeZoneSnapshot(
                _version,
                utcNow,
                _arenaBoundary,
                Array.Empty<ObstacleSnapshot>(),
                Array.Empty<DangerAreaSnapshot>(),
                Array.Empty<GoalAreaSnapshot>());
        }

        _entityCache.Clear();
        _pendingRemovals.Clear();

        var obstacles = new List<ObstacleSnapshot>();
        var dangers = new List<DangerAreaSnapshot>();
        var goals = new List<GoalAreaSnapshot>();

        foreach (var area in _areas.Values)
        {
            var resolved = ResolveGeometry(area);
            if (resolved is null)
            {
                continue;
            }

            switch (area.Kind)
            {
                case AreaKind.Danger:
                    dangers.Add(new DangerAreaSnapshot(area.Handle, resolved, CopyDangerWindows(area.DangerWindows)));
                    break;
                case AreaKind.Goal:
                    goals.Add(new GoalAreaSnapshot(area.Handle, resolved, CopyGoalWindows(area.GoalWindows)));
                    break;
                case AreaKind.Obstacle:
                    obstacles.Add(new ObstacleSnapshot(area.Handle, resolved));
                    break;
            }
        }

        if (_pendingRemovals.Count > 0)
        {
            foreach (var handle in _pendingRemovals)
            {
                if (_areas.Remove(handle.Id))
                {
                    _version++;
                }
            }

            _pendingRemovals.Clear();
        }

        _entityCache.Clear();

        return new SafeZoneSnapshot(_version, utcNow, _arenaBoundary, obstacles.ToArray(), dangers.ToArray(), goals.ToArray());
    }

    private static IReadOnlyList<DangerWindow> CopyDangerWindows(IReadOnlyList<DangerWindow> source)
    {
        if (source.Count == 0)
        {
            return Array.Empty<DangerWindow>();
        }

        var copy = new DangerWindow[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            copy[i] = source[i];
        }

        return copy;
    }

    private static IReadOnlyList<GoalWindow> CopyGoalWindows(IReadOnlyList<GoalWindow> source)
    {
        if (source.Count == 0)
        {
            return Array.Empty<GoalWindow>();
        }

        var copy = new GoalWindow[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            copy[i] = source[i];
        }

        return copy;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _areas.Clear();
        _entityCache.Clear();
        _pendingRemovals.Clear();
        _arenaBoundary = null;
        _disposed = true;
    }

    private SafeZoneAreaHandle AllocateHandle(AreaKind kind)
    {
        var handle = new SafeZoneAreaHandle(_nextHandleId, kind);
        _nextHandleId++;
        return handle;
    }

    private void ClearByKind(AreaKind kind)
    {
        ThrowIfDisposed();
        var changed = false;
        var toRemove = new List<ulong>();

        foreach (var pair in _areas)
        {
            if (pair.Value.Kind == kind)
            {
                toRemove.Add(pair.Key);
            }
        }

        foreach (var id in toRemove)
        {
            if (_areas.Remove(id))
            {
                changed = true;
            }
        }

        if (changed)
        {
            _version++;
        }
    }

    private IGeometryShape? ResolveGeometry(RuntimeArea area)
    {
        switch (area.Binding)
        {
            case StaticTransform staticBinding:
            {
                var shape = area.Template.Resolve(staticBinding.Position, staticBinding.Rotation, lengthOverride: null);
                area.LastResolvedGeometry = shape;
                return shape;
            }
            case DynamicTransformBinding dynamic:
                return ResolveDynamicGeometry(area, dynamic);
            default:
                return null;
        }
    }

    private IGeometryShape? ResolveDynamicGeometry(RuntimeArea area, DynamicTransformBinding binding)
    {
        switch (binding)
        {
            case FollowEntityPosition follow:
            {
                if (TryGetEntityPosition(follow.EntityId, out var pos))
                {
                    var world = new System.Numerics.Vector3(
                        pos.X + follow.WorldOffsetXZ.X,
                        pos.Y,
                        pos.Z + follow.WorldOffsetXZ.Y);
                    var shape = area.Template.Resolve(world, follow.Rotation, lengthOverride: null);
                    area.LastResolvedGeometry = shape;
                    return shape;
                }

                return HandleMissingEntity(area, binding.MissingPolicy);
            }

            case FollowEntityPositionAndRotation followBoth:
            {
                if (TryGetEntityPose(followBoth.EntityId, out var pos, out var rotation))
                {
                    var world = GeometryHelper.LocalToWorld(followBoth.LocalOffsetXZ, pos, rotation);
                    var shape = area.Template.Resolve(world, rotation, lengthOverride: null);
                    area.LastResolvedGeometry = shape;
                    return shape;
                }

                return HandleMissingEntity(area, binding.MissingPolicy);
            }

            case FixedPositionFaceEntity face:
            {
                var rotation = TryGetEntityPosition(face.TargetEntityId, out var target)
                    ? GeometryHelper.AngleTo(face.Anchor, target)
                    : face.FallbackRotation;
                var shape = area.Template.Resolve(face.Anchor, rotation, lengthOverride: null);
                area.LastResolvedGeometry = shape;
                return shape;
            }

            case BetweenTwoAnchors between:
            {
                if (TryResolveAnchor(between.AnchorA, out var a) && TryResolveAnchor(between.AnchorB, out var b))
                {
                    var midpoint = new System.Numerics.Vector3(
                        (a.X + b.X) * 0.5f,
                        a.Y,
                        (a.Z + b.Z) * 0.5f);
                    var rotation = GeometryHelper.AngleTo(a, b);
                    var length = GeometryHelper.DistanceXZ(a, b);
                    var shape = area.Template.Resolve(midpoint, rotation, length);
                    area.LastResolvedGeometry = shape;
                    return shape;
                }

                return HandleMissingEntity(area, binding.MissingPolicy);
            }

            default:
                return null;
        }
    }

    private IGeometryShape? HandleMissingEntity(RuntimeArea area, MissingEntityPolicy policy)
    {
        switch (policy)
        {
            case MissingEntityPolicy.KeepLastResolved:
                return area.LastResolvedGeometry;
            case MissingEntityPolicy.RemoveArea:
                _pendingRemovals.Add(area.Handle);
                return null;
            case MissingEntityPolicy.DisableArea:
            default:
                return null;
        }
    }

    private bool TryResolveAnchor(AnchorSource source, out System.Numerics.Vector3 position)
    {
        switch (source)
        {
            case WorldAnchor world:
                position = world.Position;
                return true;
            case EntityAnchor entity:
                return TryGetEntityPosition(entity.EntityId, out position);
            default:
                position = default;
                return false;
        }
    }

    private bool TryGetEntityPosition(uint entityId, out System.Numerics.Vector3 position)
    {
        if (EntityPoseOverride != null) return TryGetEntityPose(entityId, out position, out _);
        var entity = ResolveEntity(entityId);
        if (entity is null)
        {
            position = default;
            return false;
        }

        position = entity.Position;
        return true;
    }

    private bool TryGetEntityPose(uint entityId, out System.Numerics.Vector3 position, out float rotation)
    {
        if (EntityPoseOverride != null)
        {
            var pose = EntityPoseOverride(entityId);
            position = pose?.Position ?? default;
            rotation = pose?.Rotation ?? 0;
            return pose != null;
        }
        var entity = ResolveEntity(entityId);
        if (entity is null)
        {
            position = default;
            rotation = 0f;
            return false;
        }

        position = entity.Position;
        rotation = entity.Rotation;
        return true;
    }

    private IGameObject? ResolveEntity(uint entityId)
    {
        if (entityId == 0u)
        {
            return null;
        }

        if (_entityCache.TryGetValue(entityId, out var cached))
        {
            return cached;
        }

        IGameObject? found = null;
        for (var i = 0; i < Svc.Objects.Length; i++)
        {
            var obj = Svc.Objects[i];
            if (obj is null)
            {
                continue;
            }

            if (obj.EntityId == entityId)
            {
                found = obj;
                break;
            }
        }

        _entityCache[entityId] = found;
        return found;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SafeZoneManager));
        }
    }
}
