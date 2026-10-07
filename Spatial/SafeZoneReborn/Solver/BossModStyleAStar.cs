using MasterPlayer.Spatial.SafeZone;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// BossMod 风格单字段 A*：优先寻找长期安全落点。无安全落点的错峰危险由 <see cref="SafeIntervalPathfinder"/> 补充时间搜索。
/// <list type="number">
///   <item><description><c>PixelMaxG</c> = "在累计移动距离 g 之前必须离开"。<c>MaxValue</c>=安全；<c>&lt;0</c>=不可通行；<c>0</c>=立即危险。</description></item>
///   <item><description>节点内嵌 <see cref="Node.PathLeeway"/>（路径累计安全余量）与 <see cref="Node.PathMinG"/>（最小 cell-MaxG），搜索过程即知整段路径质量。</description></item>
///   <item><description><see cref="Score"/> 9 档评级配合 <see cref="ResolveBestIndex"/>，找不到严格安全解时自动退到最好次优解，永远输出 destination。</description></item>
/// </list>
/// <para>只读 <see cref="RasterizedMap.PixelMaxGAt"/>，光栅写入阶段已按 <see cref="DangerWindow.DangerFrom"/>/<see cref="DangerWindow.DangerUntil"/> 对齐，绝不读 <c>TelegraphFrom</c>。</para>
/// </summary>
public static class BossModStyleAStar
{
    private readonly struct NodeBuffer : IDisposable
    {
        public readonly Node[] Nodes;
        public NodeBuffer(int count)
        {
            Nodes = ArrayPool<Node>.Shared.Rent(count);
            // Parent-chain guards can inspect the whole rented array.
            Array.Clear(Nodes);
        }
        public void Dispose() => ArrayPool<Node>.Shared.Return(Nodes);
    }
    /// <summary>路径质量评级。数值越大越好。</summary>
    public enum Score
    {
        /// <summary>路径不安全且终点比起点更糟。</summary>
        JustBad,
        /// <summary>路径不安全但终点危险余量大于起点。</summary>
        UltimatelyBetter,
        /// <summary>路径不安全但终点是安全格。</summary>
        UltimatelySafe,
        /// <summary>路径不安全（部分 leeway 负但 minG 不低于起点），终点与起点同样危险。</summary>
        UnsafeAsStart,
        /// <summary>路径半安全（无负 leeway 也无 minG 低于起点），终点与起点同样危险。</summary>
        SemiSafeAsStart,
        /// <summary>路径不安全但终点危险余量优于起点。</summary>
        UnsafeImprove,
        /// <summary>路径半安全且终点危险余量优于起点。</summary>
        SemiSafeImprove,
        /// <summary>路径完全安全且终点是安全格。</summary>
        Safe,
        /// <summary>路径完全安全且终点目标优先级高于起点。</summary>
        SafeBetterPrio,
        /// <summary>路径完全安全且终点是最大优先级目标。</summary>
        SafeMaxPrio,
    }

    /// <summary>搜索结果。</summary>
    public sealed class Result
    {
        public Result(IReadOnlyList<TimedWaypoint> path, Vector3? destination, Score bestScore, float pathLeeway, bool budgetExceeded, int expansions)
        {
            Path = path;
            Destination = destination;
            BestScore = bestScore;
            PathLeeway = pathLeeway;
            BudgetExceeded = budgetExceeded;
            Expansions = expansions;
        }

        /// <summary>最终路径（含起点 path[0]=玩家真实坐标）。路点数 &lt; 2 视为"无需移动"。</summary>
        public IReadOnlyList<TimedWaypoint> Path { get; }

        /// <summary>destination 世界坐标；空表示"已在最优位置，无需移动"。</summary>
        public Vector3? Destination { get; }

        /// <summary>best 节点的 Score 档位。</summary>
        public Score BestScore { get; }

        /// <summary>best 节点的 PathLeeway（路径累计最小余量）。正=全程安全；负=有"赶不上"段。</summary>
        public float PathLeeway { get; }

        public bool BudgetExceeded { get; }
        internal bool IsPartialEscape { get; init; }
        public int Expansions { get; }

        public bool HasPath => Path.Count >= 2;
    }

    /// <summary>搜索节点。</summary>
    private struct Node
    {
        public float GScore;
        public float HScore;
        public int ParentIndex;
        public bool InOpen;
        public bool Visited;
        public float PathLeeway;
        public float PathMinG;
        public Score Score;
        public float GoalPriority;
        public PositionPreference GoalPreference;
        public float GoalPreferenceScore;
        /// <summary>
        /// 起点逃逸豁免状态：路径是否已经踏上真安全地面（<c>MaxG == MaxValue</c>）。
        /// <para>玩家几何安全但起点落在 <c>HardSafetyMargin</c> 膨胀环里时，逃逸期间穿越 margin 环格不毒化 leeway；
        /// 踩上安全地面后置 true 恢复正常危险约束。核心格（真危险几何）始终不豁免。</para>
        /// </summary>
        public bool HasEscaped;
    }

    /// <summary>展开次数上限。</summary>
    private const int MaxExpansions = 80_000;

    private static readonly int[] DirCol = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] DirRow = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private const float Sqrt2 = 1.41421356f;

    /// <summary>
    /// 运行搜索并直接输出 <see cref="TimedWaypoint"/> 路径。
    /// </summary>
    /// <param name="map">已光栅化网格（B1 PixelMaxG/PixelPriority 已写入）。</param>
    /// <param name="playerPosition">玩家世界坐标。</param>
    /// <param name="speed">规划移动速度（yalm/秒），决定 g↔时间转换。</param>
    /// <param name="solveTime">解算基准时间（UTC）。</param>
    /// <param name="movementStartDelay">移动启动延迟（秒）。</param>
    /// <param name="options">解算选项（PlanningHorizon 等）。</param>
    /// <param name="playerGeometricallySafe">
    /// 玩家"几何上"是否不在任何激活危险内（由 <c>SafeZoneSolver</c> 用真几何判定，不读光栅 margin）。
    /// 为 true 且起点格因 <c>HardSafetyMargin</c> 膨胀被标成 <c>MaxG&lt;=0</c> 时，抬升起点基线为安全（见 <c>startMaxG</c> 注释）。
    /// </param>
    public static Result Search(
        RasterizedMap map,
        Vector3 playerPosition,
        float speed,
        DateTime solveTime,
        float movementStartDelay,
        SolveOptions options,
        bool playerGeometricallySafe = false)
        => Search(map, playerPosition, speed, solveTime, movementStartDelay, options, playerGeometricallySafe, false);

    internal static Result Search(
        RasterizedMap map,
        Vector3 playerPosition,
        float speed,
        DateTime solveTime,
        float movementStartDelay,
        SolveOptions options,
        bool playerGeometricallySafe,
        bool requireSafePath)
    {
        var safeSpeed = MathF.Max(speed, 0.1f);
        var resolution = map.Resolution;
        var horizon = MathF.Max(0f, options.PlanningHorizon);
        var maxG = horizon * safeSpeed;

        var (startCol, startRow) = map.WorldToCell(playerPosition);
        if (!map.InBounds(startCol, startRow))
        {
            return new Result(Array.Empty<TimedWaypoint>(), null, Score.JustBad, 0f, false, 0);
        }

        var totalPixels = map.Columns * map.Rows;
        using var nodeBuffer = new NodeBuffer(totalPixels);
        var nodes = nodeBuffer.Nodes;
        var startIndex = (startRow * map.Columns) + startCol;
        var startMaxG = map.PixelMaxGAt(startCol, startRow);
        var startPrio = map.PixelPriorityAt(startCol, startRow);
        var startPreference = map.PixelGoalPreferenceAt(startCol, startRow);
        var startPreferenceScore = map.PixelGoalPreferenceScoreAt(startCol, startRow);

        // 起点逃逸豁免：玩家几何上不在任何激活危险内但起点格因 HardSafetyMargin 膨胀被标成 MaxG<=0 时，
        // 起点其实踩着不会掉血的地面。此时：
        //   ① leeway 基线抬成 ∞，让逃逸路径穿越 margin 环格不被毒化为负。
        //   ② Score 评级仍用真实 startMaxG，起点保持 UnsafeAsStart，让目标格 SafeBetterPrio 严格更优 → 玩家会动。
        //   ③ HasEscaped 标记：踩上真安全地面（MaxG==MaxValue）即置 true，之后恢复正常 leeway 约束。
        // 玩家真在危险里（playerGeometricallySafe=false）时不豁免，保持就近逃出语义。
        var startPoisoned = playerGeometricallySafe && startMaxG < float.MaxValue;
        var leewayBaseline = startPoisoned ? float.MaxValue : startMaxG;
        var startEscaped = !startPoisoned;
        var startScore = CalculateScore(startMaxG, startMaxG, leewayBaseline, startPrio, startPrio, startMaxG);

        nodes[startIndex] = new Node
        {
            GScore = 0f,
            HScore = CalculateHScore(map, startCol, startRow),
            ParentIndex = startIndex,
            InOpen = true,
            Visited = true,
            PathLeeway = leewayBaseline,
            PathMinG = startMaxG,
            Score = startScore,
            GoalPriority = startPrio,
            GoalPreference = startPreference,
            GoalPreferenceScore = startPreferenceScore,
            HasEscaped = startEscaped,
        };

        var open = new PriorityQueue<int, float>();
        open.Enqueue(startIndex, nodes[startIndex].HScore);

        var bestIndex = startIndex;
        var fallbackIndex = startIndex;
        var goalStandbyIndex = startIndex;
        var goalStandbyDistance = float.MaxValue;
        var goalStandbyG = float.MaxValue;
        var mayUseGoalStandby = map.GoalHeuristicPosition is not null && startPrio <= 0f;
        var expansions = 0;
        var budgetExceeded = false;

        while (open.TryDequeue(out var currentIndex, out _))
        {
            ref var current = ref nodes[currentIndex];
            if (!current.InOpen)
            {
                continue;
            }

            current.InOpen = false;

            if (expansions++ >= MaxExpansions)
            {
                budgetExceeded = true;
                break;
            }

            if (CompareNodeScore(ref current, ref nodes[bestIndex], options.AllowBestEffort) < 0)
            {
                bestIndex = currentIndex;
            }

            // Without an active goal, reaching safe ground completes the escape.
            // Do not spend a short cast's reaction budget searching the entire arena
            // for a different safe point. All spatial/time margins still apply.
            if (map.MaxGoalPriority <= 0f && current.Score >= Score.Safe)
            {
                bestIndex = currentIndex;
                break;
            }

            // A single goal's best possible preference is known after rasterization.
            // Nearest goals (including attack range) can finish too once that bound is
            // reached safely; scanning the rest of the arena cannot improve the destination.
            if ((map.HasSingleCenterGoal || map.HasSingleNearestGoal) && current.Score >= Score.Safe && current.GoalPriority > 0f &&
                current.GoalPriority >= map.MaxGoalPriority &&
                current.GoalPreferenceScore >= map.MaxGoalPreferenceScore - 1e-5f)
            {
                bestIndex = currentIndex;
                break;
            }

            if (current.Score == Score.UltimatelySafe &&
                (fallbackIndex == startIndex || CompareNodeScore(ref current, ref nodes[fallbackIndex], options.AllowBestEffort) < 0))
            {
                fallbackIndex = currentIndex;
            }

            if (current.GScore >= maxG)
            {
                continue;
            }

            var curX = currentIndex % map.Columns;
            var curY = currentIndex / map.Columns;
            if (mayUseGoalStandby &&
                currentIndex != startIndex &&
                current.Score == Score.Safe &&
                current.GoalPriority <= 0f &&
                TryImproveGoalStandby(currentIndex, current.HScore, current.GScore, ref goalStandbyIndex, ref goalStandbyDistance, ref goalStandbyG))
            {
                // Active goal exists but this node is a plain safe cell, not a goal cell.
                // Keep the reachable safe cell closest to the goal as a standby target in case every real goal cell is blocked.
            }

            var curReal = map.PixelMaxGAt(curX, curY);
            // 当前格"有效 MaxG"（仅用于 leeway，不影响 Score 的 cellMaxG/pathMinG）：
            //   · 起点格（玩家几何安全时）：恒豁免为 ∞。
            //   · 其余格：路径尚未逃逸且该格不是危险核心（margin 环）时豁免为 ∞；否则用真实值。
            var curExempt = currentIndex == startIndex
                ? startPoisoned
                : (!current.HasEscaped && !map.IsDangerCoreAt(curX, curY));
            var curEff = curExempt ? float.MaxValue : curReal;
            var curEscaped = current.HasEscaped;

            for (var dir = 0; dir < 8; dir++)
            {
                var nx = curX + DirCol[dir];
                var ny = curY + DirRow[dir];
                if (!map.InBounds(nx, ny))
                {
                    continue;
                }

                var nIndex = (ny * map.Columns) + nx;
                var nReal = map.PixelMaxGAt(nx, ny);
                if (!map.CanTraverse(currentIndex, nIndex))
                {
                    continue;
                }

                var isDiag = (dir & 1) == 1;
                if (isDiag)
                {
                    var sideX = (curY * map.Columns) + nx;
                    var sideY = (ny * map.Columns) + curX;
                    if (!map.CanTraverse(currentIndex, sideX) || !map.CanTraverse(sideX, nIndex) ||
                        !map.CanTraverse(currentIndex, sideY) || !map.CanTraverse(sideY, nIndex))
                        continue;
                }
                var stepG = (isDiag ? Sqrt2 : 1f) * resolution;
                var newG = current.GScore + stepG;
                if (newG > maxG)
                {
                    continue;
                }

                // 邻居有效 MaxG（leeway 用）：未逃逸 + 非核心 → 豁免 ∞；核心格永不豁免。
                var nExempt = !curEscaped && !map.IsDangerCoreAt(nx, ny);
                var nEff = nExempt ? float.MaxValue : nReal;

                var stepLeeway = MathF.Min(nEff, curEff) - newG;
                var pathLeeway = MathF.Min(current.PathLeeway, stepLeeway);
                // pathMinG 用真实 MaxG，保持 Score 评级诚实：豁免只放宽 leeway，不谎报危险纵深。
                var pathMinG = MathF.Min(current.PathMinG, nReal);
                var nPrio = map.PixelPriorityAt(nx, ny);
                var newScore = CalculateScore(nReal, pathMinG, pathLeeway, nPrio, startPrio, startMaxG);
                var hScore = CalculateHScore(map, nx, ny);
                // 踩上真安全地面即视为已逃逸，此后恢复正常危险约束。
                var nEscaped = curEscaped || nReal >= float.MaxValue;

                ref var neighbor = ref nodes[nIndex];
                var stepAlt = new Node
                {
                    GScore = newG,
                    HScore = hScore,
                    ParentIndex = currentIndex,
                    InOpen = neighbor.InOpen,
                    Visited = true,
                    PathLeeway = pathLeeway,
                    PathMinG = pathMinG,
                    Score = newScore,
                    GoalPriority = nPrio,
                    GoalPreference = map.PixelGoalPreferenceAt(nx, ny),
                    GoalPreferenceScore = map.PixelGoalPreferenceScoreAt(nx, ny),
                    HasEscaped = nEscaped,
                };

                var alt = stepAlt;
                if (TryBuildLineOfSightAlternative(
                        map,
                        nodes,
                        startIndex,
                        current.ParentIndex,
                        nIndex,
                        playerPosition,
                        maxG,
                        startPoisoned,
                        startPrio,
                        startMaxG,
                        hScore,
                        out var losAlt) &&
                    CompareForRelaxation(ref losAlt, ref stepAlt, options.AllowBestEffort) < 0)
                {
                    alt = losAlt;
                }

                // A negative cumulative leeway cannot recover later. The temporal
                // planner handles finite windows; do not flood the arena with routes
                // already known to miss a deadline before asking it for a transition.
                if (requireSafePath && alt.PathLeeway < 0f)
                    continue;

                if (!neighbor.Visited)
                {
                    neighbor = alt;
                    neighbor.InOpen = true;
                    open.Enqueue(nIndex, alt.GScore + hScore);
                }
                else if (CompareForRelaxation(ref alt, ref neighbor, options.AllowBestEffort) < 0)
                {
                    neighbor = alt;
                    neighbor.InOpen = true;
                    open.Enqueue(nIndex, alt.GScore + hScore);
                }
            }

            // 传送边：当前格位于入口圈内时，出口格作为直接后继；传送耗时按移速折算进 g，落点不可站立则跳过。
            var teleporters = map.Teleporters;
            if (teleporters.Count != 0)
            {
                var curWorld = map.CellCenter(curX, curY);
                var curGT = current.GScore;
                var curLeewayT = current.PathLeeway;
                var curMinGT = current.PathMinG;
                for (var tp = 0; tp < teleporters.Count; ++tp)
                {
                    var edge = teleporters[tp];
                    TryTeleport(edge, edge.Entrance, edge.Exit);
                    if (edge.Bidirectional)
                        TryTeleport(edge, edge.Exit, edge.Entrance);
                }

                void TryTeleport(SolverTeleporter edge, Vector3 anchor, Vector3 dest)
                {
                    var dx = curWorld.X - anchor.X;
                    var dz = curWorld.Z - anchor.Z;
                    if (dx * dx + dz * dz > edge.Radius * edge.Radius)
                        return;
                    var (tx, ty) = map.WorldToCell(dest);
                    if (!map.InBounds(tx, ty))
                        return;
                    var tIndex = (ty * map.Columns) + tx;
                    var tReal = map.PixelMaxGAt(tx, ty);
                    if (tReal < 0f)
                        return;
                    var stepG = MathF.Sqrt(dx * dx + dz * dz) + edge.UseTimeSeconds * safeSpeed;
                    var newGT = curGT + stepG;
                    if (newGT > maxG)
                        return;
                    var tExempt = !curEscaped && !map.IsDangerCoreAt(tx, ty);
                    var tEff = tExempt ? float.MaxValue : tReal;
                    var stepLeewayT = MathF.Min(tEff, curEff) - newGT;
                    var pathLeewayT = MathF.Min(curLeewayT, stepLeewayT);
                    var pathMinGT = MathF.Min(curMinGT, tReal);
                    var tPrio = map.PixelPriorityAt(tx, ty);
                    var newScoreT = CalculateScore(tReal, pathMinGT, pathLeewayT, tPrio, startPrio, startMaxG);
                    var hScoreT = CalculateHScore(map, tx, ty);
                    var tEscaped = curEscaped || tReal >= float.MaxValue;
                    if (requireSafePath && pathLeewayT < 0f)
                        return;

                    ref var target = ref nodes[tIndex];
                    var altT = new Node
                    {
                        GScore = newGT,
                        HScore = hScoreT,
                        ParentIndex = currentIndex,
                        InOpen = target.InOpen,
                        Visited = true,
                        PathLeeway = pathLeewayT,
                        PathMinG = pathMinGT,
                        Score = newScoreT,
                        GoalPriority = tPrio,
                        GoalPreference = map.PixelGoalPreferenceAt(tx, ty),
                        GoalPreferenceScore = map.PixelGoalPreferenceScoreAt(tx, ty),
                        HasEscaped = tEscaped,
                    };
                    if (!target.Visited)
                    {
                        target = altT;
                        target.InOpen = true;
                        open.Enqueue(tIndex, altT.GScore + hScoreT);
                    }
                    else if (CompareForRelaxation(ref altT, ref target, options.AllowBestEffort) < 0)
                    {
                        target = altT;
                        target.InOpen = true;
                        open.Enqueue(tIndex, altT.GScore + hScoreT);
                    }
                }
            }
        }

        var resolved = ResolveBestIndex(nodes, startIndex, bestIndex, fallbackIndex, options.AllowBestEffort);
        if (resolved == startIndex && goalStandbyIndex != startIndex)
        {
            // 如果有激活目标，但所有严格安全目标格都不可达/被危险核心覆盖，安全起点会因为同档偏好 G 更小而保持 start。
            // 此时改用已访问的安全待机点：它必须是 Score.Safe，意味着路径和终点都安全；不会恢复 BestEffort 的硬穿危险语义。
            resolved = goalStandbyIndex;
        }

        var resolvedNode = nodes[resolved];

        if (resolved == startIndex)
        {
            // 起点就是 best → 已在最优位置 → 无需移动（空成功）。
            return new Result(Array.Empty<TimedWaypoint>(), null, resolvedNode.Score, resolvedNode.PathLeeway, budgetExceeded, expansions);
        }

        var path = BuildPath(map, nodes, resolved, startIndex, playerPosition, solveTime, movementStartDelay, safeSpeed);
        var destination = path[path.Count - 1].Position;
        return new Result(path, destination, resolvedNode.Score, resolvedNode.PathLeeway, budgetExceeded, expansions);
    }

    private static bool TryImproveGoalStandby(
        int candidateIndex,
        float candidateDistance,
        float candidateG,
        ref int bestIndex,
        ref float bestDistance,
        ref float bestG)
    {
        if (candidateDistance + 1e-5f < bestDistance ||
            (MathF.Abs(candidateDistance - bestDistance) <= 1e-5f && candidateG + 1e-5f < bestG))
        {
            bestIndex = candidateIndex;
            bestDistance = candidateDistance;
            bestG = candidateG;
            return true;
        }

        return false;
    }

    /// <summary>沿父链构造 TimedWaypoint 路径。起点用玩家真实坐标，其余用格子中心。</summary>
    private static List<TimedWaypoint> BuildPath(
        RasterizedMap map,
        Node[] nodes,
        int bestIndex,
        int startIndex,
        Vector3 playerPosition,
        DateTime solveTime,
        float movementStartDelay,
        float speed)
    {
        var indices = new List<int>();
        var visited = new HashSet<int>();
        var i = bestIndex;
        // 最大循环次数 = nodes.Length（一个不重复的父链最长就这么长）。多重防护避免 OOM：
        //   ① 起点 ParentIndex 指向自己 → break（正常出口）。
        //   ② 重复访问同一节点 → break（防 ParentIndex 形成环）。
        //   ③ ParentIndex 越界 → break（防默认值 Node 的 ParentIndex=0 误用）。
        //   ④ 父节点 Visited=false → 视作未访问节点的默认值串，break。
        //   ⑤ 循环次数超过 nodes.Length → break（终极兜底）。
        var maxIterations = nodes.Length;
        for (var iter = 0; iter < maxIterations; iter++)
        {
            if (i < 0 || i >= nodes.Length)
            {
                break; // ③
            }
            if (!visited.Add(i))
            {
                break; // ②
            }
            indices.Add(i);

            var node = nodes[i];
            if (!node.Visited)
            {
                break; // ④（含默认值 Node 串入的情形）
            }

            var parent = node.ParentIndex;
            if (parent == i)
            {
                break; // ①
            }
            i = parent;
        }
        indices.Reverse();

        var path = new List<TimedWaypoint>(indices.Count);
        var startDelay = TimeSpan.FromSeconds(MathF.Max(0f, movementStartDelay));
        for (var k = 0; k < indices.Count; k++)
        {
            var idx = indices[k];
            var node = nodes[idx];
            var col = idx % map.Columns;
            var row = idx / map.Columns;

            Vector3 pos;
            DateTime arrival;
            DateTime departure;
            if (k == 0)
            {
                pos = playerPosition;
                arrival = solveTime;
                departure = solveTime + startDelay;
            }
            else
            {
                pos = map.CellCenter(col, row);
                arrival = solveTime + startDelay + TimeSpan.FromSeconds(node.GScore / speed);
                departure = arrival;
            }

            path.Add(new TimedWaypoint(pos, arrival, departure, IsWaitPoint: false, SpeedMultiplier: 1f));
        }

        return path;
    }

    private static bool TryBuildLineOfSightAlternative(
        RasterizedMap map,
        Node[] nodes,
        int startIndex,
        int parentIndex,
        int targetIndex,
        Vector3 playerPosition,
        float maxG,
        bool startPoisoned,
        float startPrio,
        float startMaxG,
        float hScore,
        out Node alt)
    {
        alt = default;

        if (parentIndex < 0 ||
            parentIndex >= nodes.Length ||
            parentIndex == targetIndex ||
            !nodes[parentIndex].Visited)
        {
            return false;
        }

        ref var parent = ref nodes[parentIndex];
        var targetCol = targetIndex % map.Columns;
        var targetRow = targetIndex / map.Columns;
        var from = parentIndex == startIndex
            ? playerPosition
            : map.CellCenter(parentIndex % map.Columns, parentIndex / map.Columns);
        var to = map.CellCenter(targetCol, targetRow);
        var segmentLength = GeometryHelper.DistanceXZ(from, to);
        if (segmentLength <= GeometryHelper.DefaultEpsilon)
        {
            return false;
        }

        var newG = parent.GScore + segmentLength;
        if (newG > maxG)
        {
            return false;
        }

        // 同格已有不更长的安全路径时，视线扫描不可能改善安全档、目标偏好或距离。
        ref var target = ref nodes[targetIndex];
        if (target.Visited && target.Score >= Score.Safe && newG + 1e-5f >= target.GScore)
            return false;

        var trace = new LineTraceState(parent.PathLeeway, parent.PathMinG, parent.HasEscaped, parentIndex);
        if (!TraceLineOfSight(map, from, to, parent.GScore, segmentLength, startIndex, startPoisoned, ref trace))
        {
            return false;
        }

        var nReal = map.PixelMaxGAt(targetCol, targetRow);
        var nPrio = map.PixelPriorityAt(targetCol, targetRow);
        var score = CalculateScore(nReal, trace.PathMinG, trace.PathLeeway, nPrio, startPrio, startMaxG);
        alt = new Node
        {
            GScore = newG,
            HScore = hScore,
            ParentIndex = parentIndex,
            InOpen = false,
            Visited = true,
            PathLeeway = trace.PathLeeway,
            PathMinG = trace.PathMinG,
            Score = score,
            GoalPriority = nPrio,
            GoalPreference = map.PixelGoalPreferenceAt(targetCol, targetRow),
            GoalPreferenceScore = map.PixelGoalPreferenceScoreAt(targetCol, targetRow),
            HasEscaped = trace.HasEscaped,
        };

        return true;
    }

    private struct LineTraceState
    {
        public LineTraceState(float pathLeeway, float pathMinG, bool hasEscaped, int cellIndex)
        {
            PathLeeway = pathLeeway;
            PathMinG = pathMinG;
            HasEscaped = hasEscaped;
            CellIndex = cellIndex;
        }

        public float PathLeeway;
        public float PathMinG;
        public bool HasEscaped;
        public int CellIndex;
    }

    private static bool TraceLineOfSight(
        RasterizedMap map,
        Vector3 from,
        Vector3 to,
        float baseG,
        float segmentLength,
        int startIndex,
        bool startPoisoned,
        ref LineTraceState state)
    {
        var sx = (from.X - map.OriginX) / map.Resolution;
        var sy = (from.Z - map.OriginZ) / map.Resolution;
        var ex = (to.X - map.OriginX) / map.Resolution;
        var ey = (to.Z - map.OriginZ) / map.Resolution;

        var col = (int)MathF.Floor(sx);
        var row = (int)MathF.Floor(sy);
        var endCol = (int)MathF.Floor(ex);
        var endRow = (int)MathF.Floor(ey);
        if (!map.InBounds(col, row) || !map.InBounds(endCol, endRow))
        {
            return false;
        }

        var dx = ex - sx;
        var dy = ey - sy;
        var stepX = Math.Sign(dx);
        var stepY = Math.Sign(dy);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : 1f / MathF.Abs(dx);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : 1f / MathF.Abs(dy);
        var tMaxX = InitialGridBoundaryT(sx, dx, col, stepX);
        var tMaxY = InitialGridBoundaryT(sy, dy, row, stepY);
        var maxIterations = map.Columns + map.Rows + 8;

        for (var iter = 0; iter < maxIterations; iter++)
        {
            var exitT = MathF.Min(1f, MathF.Min(tMaxX, tMaxY));
            if (!AccumulateLineOfSightCell(map, col, row, baseG + (segmentLength * exitT), startIndex, startPoisoned, ref state))
            {
                return false;
            }

            if (col == endCol && row == endRow)
            {
                return true;
            }

            if (tMaxX < tMaxY)
            {
                col += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                row += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                // Corner crossing: also require the two side cells to be passable, preventing diagonal corner cutting.
                if (stepX != 0 &&
                    !AccumulateLineOfSightCell(map, col + stepX, row, baseG + (segmentLength * exitT), startIndex, startPoisoned, ref state))
                {
                    return false;
                }
                if (stepY != 0 &&
                    !AccumulateLineOfSightCell(map, col, row + stepY, baseG + (segmentLength * exitT), startIndex, startPoisoned, ref state))
                {
                    return false;
                }

                col += stepX;
                row += stepY;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }

            if (!map.InBounds(col, row))
            {
                return false;
            }
        }

        return false;
    }

    private static float InitialGridBoundaryT(float start, float delta, int cell, int step)
    {
        if (step == 0)
        {
            return float.PositiveInfinity;
        }

        var boundary = step > 0 ? cell + 1f : cell;
        return (boundary - start) / delta;
    }

    private static bool AccumulateLineOfSightCell(
        RasterizedMap map,
        int col,
        int row,
        float gAtCellExit,
        int startIndex,
        bool startPoisoned,
        ref LineTraceState state)
    {
        if (!map.InBounds(col, row))
        {
            return false;
        }

        var real = map.PixelMaxGAt(col, row);
        var index = (row * map.Columns) + col;
        if (!map.CanTraverse(state.CellIndex, index))
        {
            return false;
        }

        state.CellIndex = index;
        var exempt = index == startIndex
            ? startPoisoned
            : (!state.HasEscaped && !map.IsDangerCoreAt(col, row));
        var effective = exempt ? float.MaxValue : real;
        state.PathLeeway = MathF.Min(state.PathLeeway, effective - gAtCellExit);
        state.PathMinG = MathF.Min(state.PathMinG, real);
        if (real >= float.MaxValue)
        {
            state.HasEscaped = true;
        }

        return true;
    }

    /// <summary>BossMod ResolveBestIndex：best 比 start 严格更好则用 best；否则在非安全起点场景尝试 fallback。</summary>
    private static int ResolveBestIndex(Node[] nodes, int startIndex, int bestIndex, int fallbackIndex, bool allowBestEffort)
    {
        if (CompareNodeScore(ref nodes[bestIndex], ref nodes[startIndex], allowBestEffort) < 0)
        {
            return bestIndex;
        }

        if (nodes[startIndex].Score >= Score.Safe)
        {
            return bestIndex;
        }

        if (fallbackIndex != startIndex)
        {
            var idx = fallbackIndex;
            // 同 BuildPath 防护：最多遍历 nodes.Length 次，遇 Visited=false 视作未访问节点串退出，防 ParentIndex 形成环。
            var visited = new HashSet<int>();
            for (var iter = 0; iter < nodes.Length; iter++)
            {
                if (idx == startIndex)
                {
                    break;
                }
                if (idx < 0 || idx >= nodes.Length || !visited.Add(idx) || !nodes[idx].Visited)
                {
                    idx = startIndex;
                    break;
                }
                if (nodes[idx].Score >= nodes[startIndex].Score)
                {
                    break;
                }
                idx = nodes[idx].ParentIndex;
            }
            if (idx != startIndex)
            {
                return idx;
            }
        }

        return bestIndex;
    }

    private static Score CalculateScore(float cellMaxG, float pathMinG, float pathLeeway, float cellPriority, float startPriority, float startMaxG)
    {
        var destSafe = cellMaxG >= float.MaxValue;
        var pathSafe = pathLeeway > 0f;
        // 与起点 MaxG（非 pathMinG）比较：玩家逃出危险后新格的 destBetter 判定才能正确升档。
        var destBetter = cellMaxG > startMaxG;

        if (destSafe && pathSafe)
        {
            return cellPriority > startPriority
                ? Score.SafeBetterPrio
                : Score.Safe;
        }

        // 目标格 pathSafe（能赶在开火前踩到）但 destSafe=false（终点格本身仍在某危险窗口内）：
        // 视为 SafeBetterPrio。语义：goal 声明"此处是必去点"，pathLeeway 已证明能在开火前抵达；
        // 踩到后 goal 窗口结束、下一 tick 重解会自然规划撤离。
        // 用 cellPriority > startPriority 门控，起点也在同 goal 时不会触发，避免自抖动。
        // 永久激活的核心格 MaxG=0 使 pathLeeway 恒负，pathSafe=false，自然被排除。
        if (pathSafe && cellPriority > 0f && cellPriority > startPriority)
        {
            return Score.SafeBetterPrio;
        }

        // 用 pathMinG 与 startMaxG 比较，判定"路径有没有踏入更糟格"。
        if (MathF.Abs(pathMinG - startMaxG) < 0.001f)
        {
            return pathSafe
                ? (destBetter ? Score.SemiSafeImprove : Score.SemiSafeAsStart)
                : (destBetter ? Score.UnsafeImprove : Score.UnsafeAsStart);
        }

        return destSafe ? Score.UltimatelySafe : destBetter ? Score.UltimatelyBetter : Score.JustBad;
    }

    /// <summary>
    /// 用于 best/fallback 跟踪：Score 优先；同 Score 在安全档或 BestEffort 开启时先比目标 priority，再比目标内部 preference，最后偏好 GScore 更小（标准 A* 单调性）。
    /// <para>tie-break 偏好 G 更小是为了避免同 Score 邻居被持续替换为更远版本，使 A* 一路向边界跑。严格安全目标场景由 Score.SafeBetterPrio 自然胜出。</para>
    /// </summary>
    private static int CompareNodeScore(ref Node a, ref Node b, bool allowBestEffort)
    {
        if (a.Score != b.Score)
        {
            return a.Score > b.Score ? -1 : 1;
        }

        var mayPreferGoal = allowBestEffort || a.Score >= Score.Safe;
        if (mayPreferGoal && MathF.Abs(a.GoalPriority - b.GoalPriority) > 1e-5f)
        {
            return a.GoalPriority > b.GoalPriority ? -1 : 1;
        }
        if (mayPreferGoal &&
            a.GoalPriority > 0f &&
            b.GoalPriority > 0f &&
            MathF.Abs(a.GoalPreferenceScore - b.GoalPreferenceScore) > 1e-5f)
        {
            return a.GoalPreferenceScore > b.GoalPreferenceScore ? -1 : 1;
        }
        if (a.GScore + 1e-5f < b.GScore)
        {
            return -1;
        }
        if (b.GScore + 1e-5f < a.GScore)
        {
            return 1;
        }
        return 0;
    }

    /// <summary>邻居松弛用的节点比较。当前与 <see cref="CompareNodeScore"/> 完全一致，保留独立函数以便后续引入不同的松弛准则。</summary>
    private static int CompareForRelaxation(ref Node a, ref Node b, bool allowBestEffort)
        => CompareNodeScore(ref a, ref b, allowBestEffort);

    private static float CalculateHScore(RasterizedMap map, int col, int row)
    {
        if (map.GoalHeuristicPosition is not { } goal)
        {
            return 0f;
        }

        return GeometryHelper.DistanceXZ(map.CellCenter(col, row), goal);
    }

    private static int CompareScoreAndLeeway(Score aScore, float aLeeway, Score bScore, float bLeeway)
    {
        if (aScore != bScore)
        {
            return aScore > bScore ? -1 : 1;
        }
        return aLeeway > bLeeway ? -1 : aLeeway < bLeeway ? 1 : 0;
    }
}
