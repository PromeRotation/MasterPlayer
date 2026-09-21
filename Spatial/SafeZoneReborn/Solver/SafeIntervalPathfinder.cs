using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>静态安全落点不存在时，在（格子、安全时间区间）上搜索，允许等待危险结束后再次进入同一格。</summary>
internal static class SafeIntervalPathfinder
{
    private const int MaxExpansions = 80_000;
    private const float TimeEpsilon = 0.001f;
    private static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] Dz = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private readonly record struct Interval(float From, float Until);
    private readonly record struct State(int Cell, int Interval);
    private sealed record Node(State State, float Arrival, float Waited, int Parent, float Departure);

    // null 表示起点已经危险，保留原搜索的立即逃逸逻辑；JustBad 表示安全起点也找不到可行的时间路径。
    internal static BossModStyleAStar.Result? Search(
        RasterizedMap map, Vector3 player, float speed, DateTime now, float startDelay, SolveOptions options)
    {
        speed = MathF.Max(0.1f, speed);
        startDelay = MathF.Max(0f, startDelay);
        var horizon = MathF.Max(0f, options.PlanningHorizon);
        var intervals = new Dictionary<int, List<Interval>>();
        List<Interval> Intervals(int cell)
        {
            if (intervals.TryGetValue(cell, out var cached))
                return cached;
            var result = new List<Interval>();
            intervals.Add(cell, result);
            if (map.PixelMaxGAt(cell % map.Columns, cell / map.Columns) < 0f)
                return result;

            var dangers = new List<RasterizedMap.DangerInterval>(map.DangerIntervalsAt(cell));
            dangers.Sort((a, b) => a.From.CompareTo(b.From));
            var from = 0f;
            foreach (var danger in dangers)
            {
                if (danger.From > from)
                    result.Add(new Interval(from, danger.From));
                from = MathF.Max(from, danger.Until + TimeEpsilon);
            }
            if (!float.IsPositiveInfinity(from))
                result.Add(new Interval(from, float.PositiveInfinity));
            return result;
        }

        var (startCol, startRow) = map.WorldToCell(player);
        if (!map.InBounds(startCol, startRow))
            return null;
        var startCell = startRow * map.Columns + startCol;
        var startIntervals = Intervals(startCell);
        if (startIntervals.Count == 0 || startIntervals[0].From > 0f)
            return null;

        var nodes = new List<Node> { new(new State(startCell, 0), startDelay, 0f, -1, 0f) };
        var best = new Dictionary<State, int> { [nodes[0].State] = 0 };
        var open = new PriorityQueue<int, float>();
        if (startDelay < startIntervals[0].Until)
            open.Enqueue(0, startDelay);
        var expansions = 0;
        var safestPartial = -1;
        var safestUntil = startIntervals[0].Until;

        while (open.TryDequeue(out var index, out _))
        {
            var current = nodes[index];
            if (best[current.State] != index)
                continue;
            if (++expansions > MaxExpansions)
                break;
            var currentInterval = Intervals(current.State.Cell)[current.State.Interval];
            // A conservative hit tail can temporarily prevent planning the entire
            // sequence. Still leave the current hazard for a strictly longer safe
            // interval; effect packets and the next solve can open the continuation.
            if (safestPartial < 0 && current.Parent >= 0 && currentInterval.Until > safestUntil + TimeEpsilon)
            {
                safestPartial = index;
                safestUntil = currentInterval.Until;
            }
            if (currentInterval.Until >= horizon)
            {
                var path = BuildPath(map, nodes, index, player, now);
                return new BossModStyleAStar.Result(path, path[^1].Position, BossModStyleAStar.Score.Safe,
                    0f, false, expansions);
            }

            var col = current.State.Cell % map.Columns;
            var row = current.State.Cell / map.Columns;
            var position = index == 0 ? player : map.CellCenter(col, row);
            for (var dir = 0; dir < 8; dir++)
            {
                var nc = col + Dx[dir];
                var nr = row + Dz[dir];
                if (!map.InBounds(nc, nr))
                    continue;
                var cell = nr * map.Columns + nc;
                var nextIntervals = Intervals(cell);
                var duration = GeometryHelper.DistanceXZ(position, map.CellCenter(nc, nr)) / speed;
                for (var intervalIndex = 0; intervalIndex < nextIntervals.Count; intervalIndex++)
                {
                    var nextInterval = nextIntervals[intervalIndex];
                    var departure = MathF.Max(current.Arrival, nextInterval.From);
                    // 整条边的两个端格及对角侧格都必须在通过期间安全，禁止切角或提前进入刚解禁的格子。
                    if ((dir & 1) != 0)
                    {
                        var sideA = Intervals(row * map.Columns + nc);
                        var sideB = Intervals(nr * map.Columns + col);
                        while (departure + duration < MathF.Min(currentInterval.Until, nextInterval.Until))
                        {
                            var adjusted = NextDeparture(sideA, departure, duration);
                            adjusted = NextDeparture(sideB, adjusted, duration);
                            if (adjusted == departure)
                                break;
                            departure = adjusted;
                        }
                    }
                    var arrival = departure + duration;
                    var waited = current.Waited + departure - current.Arrival;
                    if (arrival >= currentInterval.Until || arrival >= nextInterval.Until || arrival > horizon ||
                        waited > MathF.Max(0f, options.MaximumWaitTime))
                        continue;

                    var state = new State(cell, intervalIndex);
                    if (best.TryGetValue(state, out var previous) && nodes[previous].Arrival <= arrival)
                        continue;
                    var next = nodes.Count;
                    nodes.Add(new Node(state, arrival, waited, index, departure));
                    best[state] = next;
                    open.Enqueue(next, arrival);
                }
            }
        }

        if (safestPartial >= 0)
        {
            var path = BuildPath(map, nodes, safestPartial, player, now);
            var last = path[^1];
            path[^1] = last with
            {
                IsWaitPoint = true,
                PlannedDepartureTime = now.AddSeconds(MathF.Max(nodes[safestPartial].Arrival, safestUntil - 0.05f)),
            };
            return new BossModStyleAStar.Result(path, last.Position, BossModStyleAStar.Score.Safe,
                0f, expansions > MaxExpansions, expansions) { IsPartialEscape = true };
        }

        return new BossModStyleAStar.Result(Array.Empty<TimedWaypoint>(), null, BossModStyleAStar.Score.JustBad,
            0f, expansions > MaxExpansions, expansions);
    }

    private static float NextDeparture(List<Interval> intervals, float departure, float duration)
    {
        foreach (var interval in intervals)
        {
            var candidate = MathF.Max(departure, interval.From);
            if (candidate + duration < interval.Until)
                return candidate;
        }
        return float.PositiveInfinity;
    }

    private static List<TimedWaypoint> BuildPath(RasterizedMap map, List<Node> nodes, int index, Vector3 player, DateTime now)
    {
        var chain = new List<int>();
        for (var i = index; i >= 0; i = nodes[i].Parent)
            chain.Add(i);
        chain.Reverse();
        var path = new List<TimedWaypoint> { TimedWaypoint.Pass(player, now) };
        for (var i = 1; i < chain.Count; i++)
        {
            var node = nodes[chain[i]];
            var departure = now.AddSeconds(node.Departure);
            var last = path[^1];
            path[^1] = last with
            {
                PlannedDepartureTime = departure,
                IsWaitPoint = departure > last.PlannedArrivalTime.AddSeconds(TimeEpsilon / 2f),
            };
            path.Add(TimedWaypoint.Pass(map.CellCenter(node.State.Cell % map.Columns, node.State.Cell / map.Columns),
                now.AddSeconds(node.Arrival)));
        }
        return path;
    }
}
