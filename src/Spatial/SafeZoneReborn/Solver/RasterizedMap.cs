using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 本次解算临时生成的光栅寻路网格。每格保存 <see cref="PixelMaxGAt"/> + <see cref="PixelPriorityAt"/>。
/// <list type="bullet">
///   <item><description><c>PixelMaxG = MaxValue</c> 表示安全格；<c>&lt; 0</c>（<see cref="BlockedMaxG"/>）表示永不可通行；<c>&gt;= 0</c> 表示"在累计移动距离 <c>g = MaxG</c> 之前必须离开"。</description></item>
///   <item><description><c>PixelPriority</c>：<c>0</c> = 普通；<c>&gt; 0</c> = 目标偏好（同格多目标取 max）。</description></item>
///   <item><description>目标格额外记录 <see cref="PixelGoalPreferenceAt"/> 与 <see cref="PixelGoalPreferenceScoreAt"/>，只在同安全等级、同 priority 下决定目标内部落点。</description></item>
/// </list>
/// <para>危险窗口只读 <see cref="DangerWindow.DangerFrom"/>/<see cref="DangerWindow.DangerUntil"/>，绝不读 <c>TelegraphFrom</c>。</para>
/// <para>坐标只在 X/Z 平面；格子行列沿世界 +X(列) / +Z(行) 递增。</para>
/// </summary>
public sealed class RasterizedMap
{
    private readonly record struct BoundaryGridKey(float X, float Z, float Resolution, int Columns, int Rows, int Margin);
    private sealed class BoundaryGridCache
    {
        public BoundaryGridKey Key;
        public bool[]? Blocked;
    }
    private static readonly ConditionalWeakTable<IStaticArenaBoundary, BoundaryGridCache> BoundaryGrids = new();
    /// <summary>单轴最大格子数上限。超过则自动放大 <c>Resolution</c> 收敛到该上限，并通过 <see cref="WasCoarsened"/> 标注。</summary>
    public const int MaxCellsPerAxis = 400;

    /// <summary>硬阻挡格的 MaxG 哨兵值。对齐 BossMod <c>Map.PixelMaxG &lt; 0 → 不可通行</c>。</summary>
    public const float BlockedMaxG = -1f;

    /// <summary>
    /// PixelMinG "永不解禁"哨兵值（永久激活危险）：表示该格在任何累计 g 下都不安全（危险不会结束）。
    /// </summary>
    public const float NeverSafeMinG = float.MaxValue;

    private readonly float[] _pixelMaxG;
    private readonly float[] _pixelPriority;
    private readonly PositionPreference[] _pixelGoalPreference;
    private readonly float[] _pixelGoalPreferenceScore;
    /// <summary>
    /// 每格解禁 g：到达该格时累计移动距离 <c>g</c> 必须 <c>&gt;= PixelMinG</c> 才安全（危险已解禁）。安全 g 区间 = <c>[PixelMinG, PixelMaxG]</c>。
    /// <list type="bullet">
    ///   <item><description><c>0</c>（默认）= 从一开始就安全。</description></item>
    ///   <item><description><c>&gt;0</c> = 当前危险，<c>(解禁时刻 - solveTime) * speed</c> 之后才安全进入。</description></item>
    ///   <item><description><see cref="NeverSafeMinG"/> = 永久激活危险，永不解禁。</description></item>
    /// </list>
    /// <para>当前 <see cref="BossModStyleAStar"/> 未读取此字段，仅供后续 wait 动作扩展与诊断使用。</para>
    /// </summary>
    private readonly float[] _pixelMinG;
    /// <summary>
    /// 危险"核心"位图：格子是否与某个有效危险区的**真实几何**（footprint，未经 HardSafetyMargin 膨胀）相交。
    /// <para>用于"起点逃逸豁免"：玩家几何安全但起点落在膨胀环里时，A* 逃逸期间穿越非核心（margin）格不毒化 leeway，
    /// 但核心格始终按真实危险处理 → 玩家绕开 AOE 而非穿过。</para>
    /// </summary>
    private readonly bool[] _dangerCore;
    private readonly List<DangerInterval>?[] _dangerIntervals;
    private readonly List<bool[]> _exitOnlyAreas = new();
    private float _goalHeuristicDistance = float.MaxValue;

    internal readonly record struct DangerInterval(float From, float Until);
    internal bool HasFiniteDanger { get; private set; }
    internal IReadOnlyList<DangerInterval> DangerIntervalsAt(int index)
        => _dangerIntervals[index] is { } intervals ? intervals : Array.Empty<DangerInterval>();

    internal bool CanTraverse(int fromIndex, int toIndex)
    {
        if (_pixelMaxG[toIndex] < 0f)
            return false;
        foreach (var area in _exitOnlyAreas)
        {
            if (!area[fromIndex] && area[toIndex])
                return false;
        }
        return true;
    }

    private RasterizedMap(
        float originX,
        float originZ,
        float resolution,
        int columns,
        int rows,
        float planeY,
        float[] pixelMaxG,
        float[] pixelPriority,
        PositionPreference[] pixelGoalPreference,
        float[] pixelGoalPreferenceScore,
        float[] pixelMinG,
        bool[] dangerCore,
        bool wasCoarsened)
    {
        OriginX = originX;
        OriginZ = originZ;
        Resolution = resolution;
        Columns = columns;
        Rows = rows;
        PlaneY = planeY;
        _pixelMaxG = pixelMaxG;
        _pixelPriority = pixelPriority;
        _pixelGoalPreference = pixelGoalPreference;
        _pixelGoalPreferenceScore = pixelGoalPreferenceScore;
        _pixelMinG = pixelMinG;
        _dangerCore = dangerCore;
        _dangerIntervals = new List<DangerInterval>?[pixelMaxG.Length];
        WasCoarsened = wasCoarsened;
    }

    public float OriginX { get; }
    public float OriginZ { get; }
    public float Resolution { get; }
    public int Columns { get; }
    public int Rows { get; }
    public float PlaneY { get; }
    public bool WasCoarsened { get; }
    public int CellCount => _pixelMaxG.Length;
    public float MaxGoalPriority { get; private set; }
    public bool HasSingleCenterGoal { get; private set; }
    public float MaxGoalPreferenceScore { get; private set; }

    public bool HasSingleNearestGoal { get; private set; }
    public Vector3? GoalHeuristicPosition { get; private set; }

    /// <summary>解算传送边（由 <see cref="SolveOptions.Teleporters"/> 经 <see cref="Build"/> 注入）。</summary>
    public IReadOnlyList<SolverTeleporter> Teleporters { get; private set; } = [];

    /// <summary>列行是否在网格范围内。</summary>
    public bool InBounds(int col, int row) => col >= 0 && col < Columns && row >= 0 && row < Rows;

    /// <summary>世界坐标 → 列行（向下取整）。可能越界，调用方需自行 <see cref="InBounds"/> 校验。</summary>
    public (int Col, int Row) WorldToCell(Vector3 world)
    {
        var col = (int)MathF.Floor((world.X - OriginX) / Resolution);
        var row = (int)MathF.Floor((world.Z - OriginZ) / Resolution);
        return (col, row);
    }

    /// <summary>列行 → 格子中心世界坐标。</summary>
    public Vector3 CellCenter(int col, int row)
        => new(
            OriginX + ((col + 0.5f) * Resolution),
            PlaneY,
            OriginZ + ((row + 0.5f) * Resolution));

    /// <summary>
    /// 取格子的 MaxG。<c>MaxValue</c>=安全；<c>&lt;0</c>=不可通行；<c>&gt;=0</c>=g 限值。越界返回 <see cref="BlockedMaxG"/>。
    /// </summary>
    public float PixelMaxGAt(int col, int row)
        => InBounds(col, row) ? _pixelMaxG[(row * Columns) + col] : BlockedMaxG;

    /// <summary>取格子的 Priority。0=普通；&gt;0=目标偏好。越界返回 0。</summary>
    public float PixelPriorityAt(int col, int row)
        => InBounds(col, row) ? _pixelPriority[(row * Columns) + col] : 0f;

    /// <summary>取目标格偏好。非目标格返回 <see cref="PositionPreference.Center"/>。</summary>
    public PositionPreference PixelGoalPreferenceAt(int col, int row)
        => InBounds(col, row) ? _pixelGoalPreference[(row * Columns) + col] : PositionPreference.Center;

    /// <summary>
    /// 取目标格偏好评分，范围约为 0..1，越大越符合该格的 <see cref="PixelGoalPreferenceAt"/>。
    /// <para>非目标格返回 0。该值只在同安全等级、同 priority 下参与目标内部落点选择。</para>
    /// </summary>
    public float PixelGoalPreferenceScoreAt(int col, int row)
        => InBounds(col, row) ? _pixelGoalPreferenceScore[(row * Columns) + col] : 0f;

    /// <summary>
    /// 取格子的解禁 g：到达累计 g 必须 &gt;= 该值才安全。<c>0</c>=一开始就安全；<see cref="NeverSafeMinG"/>=永不解禁。越界返回 0。
    /// 供诊断/可视化与后续 A* 扩展使用；当前 A* 不读。
    /// </summary>
    public float PixelMinGAt(int col, int row)
        => InBounds(col, row) ? _pixelMinG[(row * Columns) + col] : 0f;

    /// <summary>
    /// 格子是否属于"激活危险核心"（与某有效危险区真实几何 footprint 相交，不含 HardSafetyMargin 膨胀）。越界返回 false。
    /// <para>仅供 <see cref="BossModStyleAStar"/> 起点逃逸豁免使用：核心格永不豁免，margin 环格在逃逸期间可豁免。</para>
    /// </summary>
    public bool IsDangerCoreAt(int col, int row)
        => InBounds(col, row) && _dangerCore[(row * Columns) + col];

    /// <summary>
    /// 从快照光栅化生成网格。
    /// </summary>
    /// <param name="snapshot">不可变快照。</param>
    /// <param name="playerPosition">玩家世界坐标，用于无边界时的方盒中心与网格平面 Y。</param>
    /// <param name="options">解算选项（分辨率、安全余量、方盒半径等）。</param>
    /// <param name="solveTime">将危险有效开始时间转换为 g 所用的时间基准。默认 <c>DateTime.UtcNow</c>。</param>
    /// <param name="speed">g↔时间转换速度（yalm/秒）。默认 <see cref="MovementCapability.DefaultBaseSpeed"/>。</param>
    public static RasterizedMap Build(
        SafeZoneSnapshot snapshot,
        Vector3 playerPosition,
        SolveOptions options,
        DateTime? solveTime = null,
        float speed = MovementCapability.DefaultBaseSpeed)
    {
        var solveTimeUtc = solveTime ?? DateTime.UtcNow;
        var safeSpeed = MathF.Max(0.1f, speed);

        // 步骤 1：确定网格覆盖区域。
        var (minX, minZ, maxX, maxZ) = ResolveRegion(snapshot.ArenaBoundary, playerPosition, options.FallbackBoxRadius);
        minX = MathF.Min(minX, playerPosition.X) - options.GridResolution;
        minZ = MathF.Min(minZ, playerPosition.Z) - options.GridResolution;
        maxX = MathF.Max(maxX, playerPosition.X) + options.GridResolution;
        maxZ = MathF.Max(maxZ, playerPosition.Z) + options.GridResolution;

        var spanX = MathF.Max(options.GridResolution, maxX - minX);
        var spanZ = MathF.Max(options.GridResolution, maxZ - minZ);

        var resolution = MathF.Max(0.05f, options.GridResolution);
        // 小目标也必须容纳完整目标格；仅在其窗口激活时细化网格。
        foreach (var goal in snapshot.GoalAreas)
        {
            if (goal.Geometry is not CircleGeometry circle)
                continue;
            foreach (var window in goal.GoalWindows)
            {
                if (!window.IsActiveAt(solveTimeUtc))
                    continue;
                resolution = MathF.Min(resolution, MathF.Max(0.05f, circle.Radius * 0.5f));
                break;
            }
        }
        var columns = (int)MathF.Ceiling(spanX / resolution);
        var rows = (int)MathF.Ceiling(spanZ / resolution);

        var coarsened = false;
        var maxAxis = Math.Max(columns, rows);
        if (maxAxis > MaxCellsPerAxis)
        {
            var factor = (float)maxAxis / MaxCellsPerAxis;
            resolution *= factor;
            columns = (int)MathF.Ceiling(spanX / resolution);
            rows = (int)MathF.Ceiling(spanZ / resolution);
            coarsened = true;
        }

        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);

        var pixelMaxG = new float[columns * rows];
        var pixelPriority = new float[columns * rows];
        var pixelGoalPreference = new PositionPreference[columns * rows];
        var pixelGoalPreferenceScore = new float[columns * rows];
        var pixelMinG = new float[columns * rows]; // 默认 0 = 一开始就安全。
        var dangerCore = new bool[columns * rows];
        for (var i = 0; i < pixelMaxG.Length; i++)
        {
            pixelMaxG[i] = float.MaxValue;
        }

        var map = new RasterizedMap(
            minX,
            minZ,
            resolution,
            columns,
            rows,
            playerPosition.Y,
            pixelMaxG,
            pixelPriority,
            pixelGoalPreference,
            pixelGoalPreferenceScore,
            pixelMinG,
            dangerCore,
            coarsened);

        var marginCells = (int)MathF.Ceiling(MathF.Max(0f, options.HardSafetyMargin) / resolution);
        var arenaMarginCells = (int)MathF.Ceiling(MathF.Max(0f, options.ArenaSafetyMargin ?? options.HardSafetyMargin) / resolution);

        // 覆盖起点的障碍按立即危险处理，只允许退出；边界和其他障碍仍不可通行。
        map.MarkBoundaryAndObstacles(snapshot, playerPosition, marginCells, arenaMarginCells);

        // 危险区几何 → PixelMaxG（保守膨胀 + 时间窗口投影）。
        map.RasterizeDangerAreas(snapshot, playerPosition, options, marginCells, solveTimeUtc, safeSpeed);

        // 目标区几何 → PixelPriority（保守：完整在内才记目标 + 仅激活窗口计入）。
        map.RasterizeGoalAreas(snapshot, solveTimeUtc, playerPosition);

        map.Teleporters = options.Teleporters;

        return map;
    }

    private static (float MinX, float MinZ, float MaxX, float MaxZ) ResolveRegion(
        IGeometryShape? boundary,
        Vector3 player,
        float fallbackRadius)
    {
        if (boundary is not null)
        {
            var b = boundary.GetBounds();
            return (b.MinX, b.MinZ, b.MaxX, b.MaxZ);
        }

        var r = MathF.Max(1f, fallbackRadius);
        return (player.X - r, player.Z - r, player.X + r, player.Z + r);
    }

    private void MarkBoundaryAndObstacles(SafeZoneSnapshot snapshot, Vector3 playerPosition, int marginCells, int arenaMarginCells)
    {
        var boundary = snapshot.ArenaBoundary;
        bool[] blocked;
        if (boundary is IStaticArenaBoundary immutable)
        {
            var cache = BoundaryGrids.GetValue(immutable, _ => new());
            var key = new BoundaryGridKey(OriginX, OriginZ, Resolution, Columns, Rows, arenaMarginCells);
            lock (cache)
            {
                if (cache.Blocked == null || cache.Key != key)
                {
                    cache.Blocked = BuildBoundaryGrid(boundary, arenaMarginCells);
                    cache.Key = key;
                }
                // Per-solve obstacles must never mutate the shared terrain mask.
                blocked = (bool[])cache.Blocked.Clone();
            }
        }
        else blocked = BuildBoundaryGrid(boundary, arenaMarginCells);

        var obstacles = snapshot.HardObstacles.Count == 0 ? null : new bool[blocked.Length];
        // 永久障碍：相交即阻挡。
        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.Contains(playerPosition))
                continue;
            ForEachCellInGeometryBounds(obstacle.Geometry, marginExpand: 0, (col, row) =>
            {
                if (CellIntersectsGeometry(obstacle.Geometry, col, row))
                {
                    obstacles![(row * Columns) + col] = true;
                }
            });
        }

        if (obstacles != null)
        {
            if (marginCells > 0) DilateBoolGrid(obstacles, marginCells);
            for (var i = 0; i < blocked.Length; i++) blocked[i] |= obstacles[i];
        }

        for (var i = 0; i < _pixelMaxG.Length; i++)
        {
            if (blocked[i])
            {
                _pixelMaxG[i] = BlockedMaxG;
            }
        }
    }

    private bool[] BuildBoundaryGrid(IGeometryShape? boundary, int margin)
    {
        var blocked = new bool[_pixelMaxG.Length];
        if (boundary == null) return blocked;
        for (var row = 0; row < Rows; ++row)
            for (var col = 0; col < Columns; ++col)
                blocked[row * Columns + col] = !IsCellFullyInside(boundary, col, row);
        if (margin > 0) DilateBoolGrid(blocked, margin);
        return blocked;
    }

    /// <summary>
    /// 危险区几何 → PixelMaxG；同时保留完整时间窗口，供错峰危险的安全区间搜索使用。
    /// <para>
    /// 已激活危险（<c>solveTime ∈ 有效窗口</c>）→ MaxG=0（必须立刻离开）；
    /// 未来才激活 → MaxG = <c>(EffectiveDangerFrom - solveTime) * speed</c>（剩余可达距离）；
    /// 已结束 → 跳过；
    /// 多窗口/多危险叠加 → 取 min（最早威胁决定 MaxG）。
    /// </para>
    /// </summary>
    private void RasterizeDangerAreas(SafeZoneSnapshot snapshot, Vector3 playerPosition, SolveOptions options, int marginCells, DateTime solveTime, float speed)
    {
        var startSafety = TimeSpan.FromSeconds(MathF.Max(0f, options.DangerStartSafetyTime));
        var endSafety = TimeSpan.FromSeconds(MathF.Max(0f, options.DangerEndSafetyTime));

        foreach (var danger in EnumerateDangers(snapshot, playerPosition, solveTime))
        {
            if (danger.DangerWindows.Count == 0)
            {
                continue;
            }

            // 计算该危险区对 solveTime 而言的有效 MaxG 与 MinG，多窗口聚合：
            //   · MaxG（截止）：未来威胁中最早的，多窗口取 min。
            //   · MinG（解禁）：当前激活窗口结束时刻投影到 g。有结束时取 (effectiveUntil - solveTime) * speed，
            //     多个激活窗口取 max（全部解禁才安全），任一永久激活 → NeverSafeMinG。未来才激活的窗口不产生 MinG。
            var effectiveMaxG = float.MaxValue;
            var effectiveMinG = 0f;
            var intervals = new List<DangerInterval>();
            foreach (var window in danger.DangerWindows)
            {
                var buffered = window.Buffered((float)startSafety.TotalSeconds, (float)endSafety.TotalSeconds);
                var effectiveFrom = buffered.DangerFrom;
                var effectiveUntil = buffered.DangerUntil;

                if (effectiveUntil is { } until && until < solveTime)
                {
                    continue; // 已结束，跳过。
                }

                intervals.Add(new DangerInterval(
                    MathF.Max(0f, (float)(effectiveFrom - solveTime).TotalSeconds),
                    effectiveUntil is { } end ? (float)(end - solveTime).TotalSeconds : float.PositiveInfinity));
                HasFiniteDanger |= effectiveUntil is not null;

                float maxG;
                if (solveTime >= effectiveFrom)
                {
                    maxG = 0f; // 已激活。

                    // 当前激活 → 贡献 MinG（解禁时刻）。
                    if (effectiveUntil is { } activeUntil)
                    {
                        var secondsUntilClear = (float)(activeUntil - solveTime).TotalSeconds;
                        var clearG = MathF.Max(0f, secondsUntilClear) * speed;
                        if (effectiveMinG != NeverSafeMinG && clearG > effectiveMinG)
                        {
                            effectiveMinG = clearG;
                        }
                    }
                    else
                    {
                        effectiveMinG = NeverSafeMinG; // 永久激活危险，该格永不解禁。
                    }
                }
                else
                {
                    var secondsUntilActivation = (float)(effectiveFrom - solveTime).TotalSeconds;
                    maxG = secondsUntilActivation * speed;
                }

                if (maxG < effectiveMaxG)
                {
                    effectiveMaxG = maxG;
                }
            }

            if (effectiveMaxG >= float.MaxValue && effectiveMinG == 0f)
            {
                continue; // 所有窗口都已结束、且无激活解禁约束。
            }

            // 危险几何 footprint：相交即写入；随后按 margin 膨胀。
            var footprint = new bool[_pixelMaxG.Length];
            ForEachCellInGeometryBounds(danger.Geometry, marginExpand: 0, (col, row) =>
            {
                if (CellIntersectsGeometry(danger.Geometry, col, row))
                {
                    footprint[(row * Columns) + col] = true;
                }
            });

            // 记录危险核心 = 膨胀前的真实几何 footprint，供起点逃逸豁免区分核心 vs margin 环。
            // 必须在 DilateBoolGrid 之前记录，确保 _dangerCore 不含 HardSafetyMargin 膨胀部分。
            for (var i = 0; i < footprint.Length; i++)
            {
                if (footprint[i])
                {
                    _dangerCore[i] = true;
                }
            }

            // 每个持续区域分别约束退出方向，避免重叠区域相互掩盖退出后重入。
            if (effectiveMinG == NeverSafeMinG)
                _exitOnlyAreas.Add((bool[])footprint.Clone());

            if (marginCells > 0)
            {
                DilateBoolGrid(footprint, marginCells);
            }

            // 硬阻挡格不被危险覆盖；其余格 MaxG 取 min(当前, effectiveMaxG)，MinG 同格多危险取 max（NeverSafeMinG 吸收一切）。
            for (var i = 0; i < footprint.Length; i++)
            {
                if (!footprint[i])
                {
                    continue;
                }
                if (_pixelMaxG[i] < 0f)
                {
                    continue; // 硬阻挡，跳过。
                }
                (_dangerIntervals[i] ??= new List<DangerInterval>()).AddRange(intervals);
                if (effectiveMaxG < _pixelMaxG[i])
                {
                    _pixelMaxG[i] = effectiveMaxG;
                }
                if (effectiveMinG == NeverSafeMinG)
                {
                    _pixelMinG[i] = NeverSafeMinG;
                }
                else if (_pixelMinG[i] != NeverSafeMinG && effectiveMinG > _pixelMinG[i])
                {
                    _pixelMinG[i] = effectiveMinG;
                }
            }
        }
    }

    private static IEnumerable<DangerAreaSnapshot> EnumerateDangers(SafeZoneSnapshot snapshot, Vector3 playerPosition, DateTime solveTime)
    {
        foreach (var danger in snapshot.DangerAreas)
            yield return danger;
        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.Contains(playerPosition))
                yield return new DangerAreaSnapshot(default, obstacle.Geometry, [new DangerWindow(solveTime, null)]);
        }
    }

    /// <summary>
    /// 目标区几何 → PixelPriority + 偏好元数据。仅 solveTime 激活的窗口计入；同格多目标取 max priority。
    /// <para>保守：只有完整位于目标几何内部的格子才记为目标格。</para>
    /// <para>写入时 priority 会 clamp 到 <c>max(priority, 1f)</c>：保证有激活目标就一定能被 A* 看见为目标格，避免用户注册时省略 priority（默认 0）导致 A* 完全忽略该目标区。</para>
    /// </summary>
    private void RasterizeGoalAreas(SafeZoneSnapshot snapshot, DateTime solveTime, Vector3 playerPosition)
    {
        float[]? unsafeClearance = null;
        var clearanceBuilt = false;
        var activeGoals = 0;

        foreach (var goal in snapshot.GoalAreas)
        {
            if (goal.GoalWindows.Count == 0)
            {
                continue;
            }

            // 取该目标在 solveTime 的最高激活 priority；任一窗口激活即视为"该目标有效"，priority 下界 1f。
            var hasActive = false;
            var activePriority = int.MinValue;
            var activePreference = PositionPreference.Center;
            foreach (var window in goal.GoalWindows)
            {
                if (!window.IsActiveAt(solveTime))
                {
                    continue;
                }
                if (!hasActive || window.Priority > activePriority)
                {
                    hasActive = true;
                    activePriority = window.Priority;
                    activePreference = window.Preference;
                }
            }

            if (!hasActive)
            {
                continue;
            }

            if (activePreference == PositionPreference.Deepest && !clearanceBuilt)
            {
                unsafeClearance = BuildUnsafeClearanceGrid();
                clearanceBuilt = true;
            }

            HasSingleCenterGoal = ++activeGoals == 1 && activePreference == PositionPreference.Center;
            HasSingleNearestGoal = activeGoals == 1 && activePreference == PositionPreference.Nearest;

            // 默认 priority=0 也让 A* 看见此目标（最低 baseline 1f）。
            var effectivePriority = MathF.Max(activePriority, 1f);
            var goalCenter = EstimateGeometryCenter(goal.Geometry);
            RegisterGoalHeuristic(goalCenter, effectivePriority, playerPosition);

            ForEachCellInGeometryBounds(goal.Geometry, marginExpand: 0, (col, row) =>
            {
                if (!IsCellFullyInside(goal.Geometry, col, row))
                {
                    return;
                }
                var idx = (row * Columns) + col;

                // 显式目标区内部让 HardSafetyMargin 膨胀环让位，避免"目标被危险环绕时内部无真安全格"：
                //   · margin 环（finite MaxG 且非危险核心）→ 目标内恢复为安全格 MaxValue，并清掉对应 MinG。
                //   · 真危险核心（_dangerCore=true）→ 保持真实 MaxG，安全区内的实际危险由核心承载。
                //   · 硬阻挡（MaxG<0：边界外/障碍）→ 不让步。
                var maxG = _pixelMaxG[idx];
                if (maxG >= 0f && maxG < float.MaxValue && !_dangerCore[idx])
                {
                    _pixelMaxG[idx] = float.MaxValue;
                    _pixelMinG[idx] = 0f;
                    _dangerIntervals[idx] = null;
                }

                var center = CellCenter(col, row);
                var preferenceScore = ComputeGoalPreferenceScore(goal.Geometry, activePreference, goalCenter, center, playerPosition, unsafeClearance, idx);
                if (maxG >= 0f)
                    MaxGoalPreferenceScore = MathF.Max(MaxGoalPreferenceScore, preferenceScore);

                if (ShouldReplaceGoalCell(idx, effectivePriority, activePreference, preferenceScore))
                {
                    _pixelPriority[idx] = effectivePriority;
                    _pixelGoalPreference[idx] = activePreference;
                    _pixelGoalPreferenceScore[idx] = preferenceScore;
                    if (effectivePriority > MaxGoalPriority)
                    {
                        MaxGoalPriority = effectivePriority;
                    }
                }
            });
        }
    }

    private bool ShouldReplaceGoalCell(int idx, float priority, PositionPreference preference, float preferenceScore)
    {
        var currentPriority = _pixelPriority[idx];
        if (priority > currentPriority)
        {
            return true;
        }

        if (priority < currentPriority)
        {
            return false;
        }

        if (currentPriority <= 0f)
        {
            return true;
        }

        // 同优先级、同偏好时用偏好评分选更符合该偏好的格子。
        // 同优先级但不同偏好通常意味着重叠目标区；保留先注册者，避免跨偏好评分尺度制造惊喜。
        return preference == _pixelGoalPreference[idx] &&
               preferenceScore > _pixelGoalPreferenceScore[idx] + 1e-5f;
    }

    private void RegisterGoalHeuristic(Vector3 goalCenter, float priority, Vector3 playerPosition)
    {
        var distance = GeometryHelper.DistanceXZ(goalCenter, playerPosition);
        if (priority > MaxGoalPriority ||
            (MathF.Abs(priority - MaxGoalPriority) < 1e-5f && distance < _goalHeuristicDistance))
        {
            GoalHeuristicPosition = goalCenter;
            _goalHeuristicDistance = distance;
        }
    }

    private float ComputeGoalPreferenceScore(
        IGeometryShape geometry,
        PositionPreference preference,
        Vector3 goalCenter,
        Vector3 cellCenter,
        Vector3 playerPosition,
        float[]? unsafeClearance,
        int idx)
    {
        return preference switch
        {
            PositionPreference.Nearest => ReciprocalScore(GeometryHelper.DistanceXZ(cellCenter, playerPosition)),
            PositionPreference.Deepest => DepthScore(geometry, cellCenter, unsafeClearance, idx),
            PositionPreference.Custom => 0f,
            PositionPreference.Center or _ => ReciprocalScore(GeometryHelper.DistanceXZ(cellCenter, goalCenter)),
        };
    }

    private static Vector3 EstimateGeometryCenter(IGeometryShape geometry)
    {
        var sumX = 0f;
        var sumZ = 0f;
        var count = 0;
        foreach (var contour in geometry.GetContours())
        {
            for (var i = 0; i < contour.Count; i++)
            {
                sumX += contour[i].X;
                sumZ += contour[i].Z;
                count++;
            }
        }

        return count == 0
            ? geometry.Position
            : new Vector3(sumX / count, geometry.Position.Y, sumZ / count);
    }

    private float DepthScore(IGeometryShape geometry, Vector3 cellCenter, float[]? unsafeClearance, int idx)
    {
        var contourDistance = DistanceToGeometryContourXZ(geometry, cellCenter);
        var clearance = unsafeClearance is null ? contourDistance : MathF.Min(contourDistance, unsafeClearance[idx]);
        if (clearance >= float.MaxValue * 0.5f)
        {
            clearance = contourDistance;
        }
        return clearance <= 0f ? 0f : clearance / (clearance + 1f);
    }

    private static float ReciprocalScore(float distance)
        => 1f / (1f + MathF.Max(0f, distance));

    private static float DistanceToGeometryContourXZ(IGeometryShape geometry, Vector3 point)
    {
        var minSq = float.MaxValue;
        foreach (var contour in geometry.GetContours())
        {
            if (contour.Count == 0)
            {
                continue;
            }

            if (contour.Count == 1)
            {
                minSq = MathF.Min(minSq, GeometryHelper.DistanceSquaredXZ(point, contour[0]));
                continue;
            }

            for (var i = 0; i < contour.Count; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Count];
                minSq = MathF.Min(minSq, GeometryHelper.DistanceSquaredPointToSegmentXZ(point, a, b));
            }
        }

        return minSq >= float.MaxValue * 0.5f ? 0f : MathF.Sqrt(minSq);
    }

    private float[]? BuildUnsafeClearanceGrid()
    {
        var distance = new float[_pixelMaxG.Length];
        var anySeed = false;
        const float inf = 1_000_000f;

        for (var i = 0; i < distance.Length; i++)
        {
            if (_pixelMaxG[i] < float.MaxValue)
            {
                distance[i] = 0f;
                anySeed = true;
            }
            else
            {
                distance[i] = inf;
            }
        }

        if (!anySeed)
        {
            return null;
        }

        var orth = Resolution;
        var diag = Resolution * 1.41421356f;

        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Columns; col++)
            {
                var idx = (row * Columns) + col;
                var best = distance[idx];
                if (col > 0) best = MathF.Min(best, distance[idx - 1] + orth);
                if (row > 0) best = MathF.Min(best, distance[idx - Columns] + orth);
                if (col > 0 && row > 0) best = MathF.Min(best, distance[idx - Columns - 1] + diag);
                if (col + 1 < Columns && row > 0) best = MathF.Min(best, distance[idx - Columns + 1] + diag);
                distance[idx] = best;
            }
        }

        for (var row = Rows - 1; row >= 0; row--)
        {
            for (var col = Columns - 1; col >= 0; col--)
            {
                var idx = (row * Columns) + col;
                var best = distance[idx];
                if (col + 1 < Columns) best = MathF.Min(best, distance[idx + 1] + orth);
                if (row + 1 < Rows) best = MathF.Min(best, distance[idx + Columns] + orth);
                if (col + 1 < Columns && row + 1 < Rows) best = MathF.Min(best, distance[idx + Columns + 1] + diag);
                if (col > 0 && row + 1 < Rows) best = MathF.Min(best, distance[idx + Columns - 1] + diag);
                distance[idx] = best;
            }
        }

        return distance;
    }

    /// <summary>格子与几何是否相交（中心 + 四角 + 边线 + 几何轮廓点多点检查）。</summary>
    private bool CellIntersectsGeometry(IGeometryShape geometry, int col, int row)
    {
        if (geometry is CircleGeometry or DonutGeometry)
        {
            var center = CellCenter(col, row);
            var half = Resolution * 0.5f;
            var dx = MathF.Abs(center.X - geometry.Position.X);
            var dz = MathF.Abs(center.Z - geometry.Position.Z);
            var nearX = MathF.Max(0, dx - half);
            var nearZ = MathF.Max(0, dz - half);
            var outer = (geometry is CircleGeometry circle ? circle.Radius : ((DonutGeometry)geometry).OuterRadius) + GeometryHelper.DefaultEpsilon;
            if (nearX * nearX + nearZ * nearZ > outer * outer) return false;
            if (geometry is not DonutGeometry donut) return true;
            var inner = MathF.Max(0, donut.InnerRadius - GeometryHelper.DefaultEpsilon);
            return (dx + half) * (dx + half) + (dz + half) * (dz + half) >= inner * inner;
        }
        if (geometry is RectangleGeometry rectangle)
        {
            // Four separating axes give the exact cell/rotated-rectangle overlap.
            // Avoid allocating and walking the same contour for every grid edge.
            var halfCell = Resolution * 0.5f;
            var halfWidth = rectangle.Width * 0.5f;
            var halfLength = rectangle.Length * 0.5f;
            var center = CellCenter(col, row);
            var sin = MathF.Abs(MathF.Sin(rectangle.Rotation));
            var cos = MathF.Abs(MathF.Cos(rectangle.Rotation));
            var epsilon = GeometryHelper.DefaultEpsilon;
            if (MathF.Abs(center.X - rectangle.Position.X) > halfCell + cos * halfWidth + sin * halfLength + epsilon ||
                MathF.Abs(center.Z - rectangle.Position.Z) > halfCell + sin * halfWidth + cos * halfLength + epsilon)
                return false;
            var local = GeometryHelper.WorldToLocal(center, rectangle.Position, rectangle.Rotation);
            var projectedCell = halfCell * (sin + cos);
            return MathF.Abs(local.X) <= halfWidth + projectedCell + epsilon &&
                   MathF.Abs(local.Y) <= halfLength + projectedCell + epsilon;
        }
        var minX = OriginX + (col * Resolution);
        var minZ = OriginZ + (row * Resolution);
        var maxX = minX + Resolution;
        var maxZ = minZ + Resolution;
        var y = PlaneY;

        var c0 = new Vector3(minX, y, minZ);
        var c1 = new Vector3(maxX, y, minZ);
        var c2 = new Vector3(maxX, y, maxZ);
        var c3 = new Vector3(minX, y, maxZ);

        if (geometry.Contains(CellCenter(col, row)) ||
            geometry.Contains(c0) || geometry.Contains(c1) ||
            geometry.Contains(c2) || geometry.Contains(c3))
        {
            return true;
        }

        if (geometry.IntersectsSegment(c0, c1) ||
            geometry.IntersectsSegment(c1, c2) ||
            geometry.IntersectsSegment(c2, c3) ||
            geometry.IntersectsSegment(c3, c0))
        {
            return true;
        }

        foreach (var contour in geometry.GetContours())
        {
            for (var i = 0; i < contour.Count; i++)
            {
                var p = contour[i];
                if (p.X >= minX && p.X <= maxX && p.Z >= minZ && p.Z <= maxZ)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>格子是否完整位于几何内部：中心 + 四角全部 Contains。</summary>
    private bool IsCellFullyInside(IGeometryShape geometry, int col, int row)
    {
        var minX = OriginX + (col * Resolution);
        var minZ = OriginZ + (row * Resolution);
        var maxX = minX + Resolution;
        var maxZ = minZ + Resolution;
        var y = PlaneY;

        return geometry.Contains(new Vector3(minX, y, minZ)) &&
               geometry.Contains(new Vector3(maxX, y, minZ)) &&
               geometry.Contains(new Vector3(maxX, y, maxZ)) &&
               geometry.Contains(new Vector3(minX, y, maxZ)) &&
               geometry.Contains(CellCenter(col, row));
    }

    /// <summary>遍历几何边界盒覆盖的格子。</summary>
    private void ForEachCellInGeometryBounds(IGeometryShape geometry, int marginExpand, Action<int, int> action)
    {
        var bounds = geometry.GetBounds();
        var (minCol, minRow) = WorldToCell(new Vector3(bounds.MinX, PlaneY, bounds.MinZ));
        var (maxCol, maxRow) = WorldToCell(new Vector3(bounds.MaxX, PlaneY, bounds.MaxZ));

        minCol = Math.Max(0, minCol - marginExpand);
        minRow = Math.Max(0, minRow - marginExpand);
        maxCol = Math.Min(Columns - 1, maxCol + marginExpand);
        maxRow = Math.Min(Rows - 1, maxRow + marginExpand);

        for (var row = minRow; row <= maxRow; row++)
        {
            for (var col = minCol; col <= maxCol; col++)
            {
                action(col, row);
            }
        }
    }

    /// <summary>Chebyshev（方形）膨胀 radius 格。</summary>
    private void DilateBoolGrid(bool[] grid, int radius)
    {
        if (radius <= 0)
        {
            return;
        }

        var source = (bool[])grid.Clone();
        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Columns; col++)
            {
                if (!source[(row * Columns) + col])
                {
                    continue;
                }

                var minCol = Math.Max(0, col - radius);
                var maxCol = Math.Min(Columns - 1, col + radius);
                var minRow = Math.Max(0, row - radius);
                var maxRow = Math.Min(Rows - 1, row + radius);
                for (var r = minRow; r <= maxRow; r++)
                {
                    for (var c = minCol; c <= maxCol; c++)
                    {
                        grid[(r * Columns) + c] = true;
                    }
                }
            }
        }
    }
}
