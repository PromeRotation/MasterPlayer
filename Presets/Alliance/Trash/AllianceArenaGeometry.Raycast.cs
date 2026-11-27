using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class AllianceArenaGeometry
{
    private (int[] Indices, int Ending, int New, int Single, int End)[]? rayRows;

    // Only the ring-wall knockback needs this query. Preserve source row groups,
    // padding and scalar/SIMD arithmetic; a circle ray would miss the inner wall.
    private void BuildRayRows()
    {
        rayRows = new (int[], int, int, int, int)[rowEdges.Length];
        var width = Avx512F.IsSupported ? 16 : Avx2.IsSupported ? 8 : 1;
        for (var row = 0; row < rowEdges.Length; ++row)
        {
            int Group(int index)
            {
                var edge = edges[index];
                var first = Row(edge.Low);
                var last = Row(MathF.BitDecrement(edge.High));
                if (first == row) return last == row ? 3 : 2;
                return last == row ? 1 : 0;
            }
            var groups = rowEdges[row].GroupBy(Group).ToDictionary(g => g.Key, g => g.ToArray());
            var indices = Enumerable.Range(0, 4).SelectMany(g => groups.GetValueOrDefault(g) ?? []).ToArray();
            var ending = groups.GetValueOrDefault(0)?.Length ?? 0;
            var start = ending + (groups.GetValueOrDefault(1)?.Length ?? 0);
            var single = start + (groups.GetValueOrDefault(2)?.Length ?? 0);
            var padded = Enumerable.Repeat(-1, (indices.Length + width - 1) / width * width).ToArray();
            indices.CopyTo(padded, 0);
            rayRows[row] = (padded, ending, start, single, indices.Length);
        }
    }

    internal float Raycast(Vector3 origin, Vector3 direction)
    {
        if (rayRows == null) BuildRayRows();
        var ox = origin.X - Position.X;
        var oz = origin.Z - Position.Z;
        var dx = direction.X;
        var dz = direction.Z;
        var hasX = MathF.Abs(dx) > EDGE_EPSILON;
        var hasZ = MathF.Abs(dz) > EDGE_EPSILON;
        var inverseX = hasX ? 1f / dx : 0f;
        var inverseZ = hasZ ? 1f / dz : 0f;
        var minTime = -float.Epsilon;
        var maxTime = float.MaxValue;
        if (hasX)
        {
            var a = (minimum.X - ox) * inverseX;
            var b = (maximum.X - ox) * inverseX;
            if (a > b) (a, b) = (b, a);
            minTime = Math.Max(minTime, a);
            maxTime = Math.Min(maxTime, b);
        }
        else if (ox < minimum.X - EDGE_EPSILON || ox > maximum.X + EDGE_EPSILON) return float.MaxValue;
        if (hasZ)
        {
            var a = (minimum.Y - oz) * inverseZ;
            var b = (maximum.Y - oz) * inverseZ;
            if (a > b) (a, b) = (b, a);
            minTime = Math.Max(minTime, a);
            maxTime = Math.Min(maxTime, b);
        }
        else if (oz < minimum.Y - EDGE_EPSILON || oz > maximum.Y + EDGE_EPSILON) return float.MaxValue;
        if (maxTime < 0 || minTime > maxTime) return float.MaxValue;
        var startTime = Math.Max(0f, minTime);
        var best = float.MaxValue;
        if (!hasZ)
        {
            if (!hasX) return best;
            var row = Row(oz);
            var data = rayRows![row];
            var fused = Avx512F.IsSupported || Avx2.IsSupported && Fma.IsSupported;
            foreach (var index in data.Indices)
            {
                if (index < 0) continue;
                var edge = edges[index];
                if (oz < edge.Low - EDGE_EPSILON || oz >= edge.High - EDGE_EPSILON) continue;
                var x = fused ? MathF.FusedMultiplyAdd(edge.Slope, oz, edge.Intercept) : edge.Slope * oz + edge.Intercept;
                var time = (x - ox) * inverseX;
                if (time >= startTime && time <= maxTime && time < best
                    && x >= edge.MinX - EDGE_EPSILON && x <= edge.MaxX + EDGE_EPSILON) best = time;
            }
            foreach (var index in rowHorizontal[row])
            {
                var edge = horizontal[index];
                if (edge.Y < oz - EDGE_EPSILON || edge.Y > oz + EDGE_EPSILON) continue;
                var x = ox;
                if (dx > 0)
                {
                    if (ox <= edge.MinX) x = edge.MinX;
                    else if (ox > edge.MaxX) x = float.PositiveInfinity;
                }
                else
                {
                    if (ox >= edge.MaxX) x = edge.MaxX;
                    else if (ox < edge.MinX) x = float.NegativeInfinity;
                }
                var time = (x - ox) * inverseX;
                if (time >= startTime && time <= maxTime && time < best) best = time;
            }
            return best;
        }
        var cellHeight = Math.Max(maximum.Y - minimum.Y, EDGE_EPSILON) / rowEdges.Length;
        var firstY = oz + dz * startTime;
        var current = Row(firstY);
        var step = dz > 0 ? 1 : -1;
        var rowEdge = Math.Clamp((int)((firstY - minimum.Y) * inverseCellHeight), 0, rowEdges.Length - 1);
        var nextY = minimum.Y + (dz > 0 ? rowEdge + 1 : rowEdge) * cellHeight;
        var xEnter = ox + dx * startTime;
        var complete = false;
        var hit = false;
        var rowMinimum = startTime;
        while ((uint)current < (uint)rowEdges.Length)
        {
            var boundaryTime = (nextY - oz) * inverseZ;
            var xExit = ox + dx * boundaryTime;
            var rowMaximum = Math.Min(maxTime, best);
            if (!(Math.Max(xEnter, xExit) + 2e-6f < rowMinX[current]
                || Math.Min(xEnter, xExit) - 2e-6f > rowMaxX[current]))
            {
                var oldBest = best;
                var data = rayRows![current];
                if (!complete) RayKernel(data.Indices, 0, data.Indices.Length, ox, oz, dx, dz, startTime, rowMaximum, ref best);
                else if (step > 0) RayKernel(data.Indices, data.New, data.End, ox, oz, dx, dz, startTime, rowMaximum, ref best);
                else
                {
                    RayKernel(data.Indices, data.Ending, data.New, ox, oz, dx, dz, startTime, rowMaximum, ref best);
                    RayKernel(data.Indices, data.Single, data.End, ox, oz, dx, dz, startTime, Math.Min(rowMaximum, best), ref best);
                }
                complete = true;
                if (best < oldBest)
                {
                    hit = true;
                    rowMaximum = Math.Min(rowMaximum, best);
                }
                var yLimit = oz + dz * rowMaximum;
                var lowY = Math.Min(oz + dz * rowMinimum, yLimit) - EDGE_EPSILON;
                var highY = Math.Max(oz + dz * rowMinimum, yLimit) + EDGE_EPSILON;
                var horizontalIndices = rowHorizontal[current].OrderBy(i => horizontal[i].Y).ToArray();
                var horizontalStart = 0;
                // Source small rows scan from the beginning: reprojecting t to Y can round
                // above a boundary even when the boundary's t equals rowMaximum exactly.
                if (horizontalIndices.Length >= 8)
                {
                    var horizontalEnd = horizontalIndices.Length;
                    while (horizontalStart < horizontalEnd)
                    {
                        var middle = (horizontalStart + horizontalEnd) / 2;
                        if (horizontal[horizontalIndices[middle]].Y < lowY) horizontalStart = middle + 1;
                        else horizontalEnd = middle;
                    }
                }
                for (var horizontalIndex = horizontalStart; horizontalIndex < horizontalIndices.Length; ++horizontalIndex)
                {
                    var edge = horizontal[horizontalIndices[horizontalIndex]];
                    if (edge.Y > highY) break;
                    var time = (edge.Y - oz) * inverseZ;
                    if (time < startTime || time > rowMaximum) continue;
                    var x = ox + time * dx;
                    if (x >= edge.MinX - 1e-6f && x <= edge.MaxX + 1e-6f)
                    {
                        best = rowMaximum = time;
                        hit = true;
                    }
                }
                if (hit && best <= boundaryTime + 1e-6f) break;
            }
            else complete = false;
            if (boundaryTime >= maxTime - 1e-6f) break;
            current += step;
            rowMinimum = boundaryTime;
            xEnter = xExit;
            nextY += step * cellHeight;
        }
        return hit ? best : float.MaxValue;
    }

    private void RayKernel(int[] indices, int first, int end, float ox, float oz, float dx, float dz,
        float minTime, float maxTime, ref float best)
    {
        if (first >= end || minTime > maxTime || best <= minTime) return;
        var cursor = first;
        var stageMaximum = maxTime;
        if (Avx512F.IsSupported)
        {
            for (; cursor + 16 <= end; cursor += 16) Block(cursor, 16, 512, stageMaximum, ref best);
            stageMaximum = Math.Min(stageMaximum, best);
        }
        if (Avx2.IsSupported)
        {
            for (; cursor + 8 <= end; cursor += 8) Block(cursor, 8, 256, stageMaximum, ref best);
            stageMaximum = Math.Min(stageMaximum, best);
        }
        for (; cursor < end; ++cursor) Block(cursor, 1, 0, stageMaximum, ref best);

        void Block(int at, int count, int mode, float stageLimit, ref float currentBest)
        {
            var vector = mode != 0;
            var fused = mode == 512 || mode == 256 && Fma.IsSupported;
            var limit = Math.Min(stageLimit, currentBest);
            var blockBest = currentBest;
            var inverseLength = 1f / (dx * dx + dz * dz + 1e-20f);
            for (var lane = at; lane < at + count; ++lane)
            {
                var index = indices[lane];
                if (index < 0) continue;
                var edge = edges[index];
                var ey = edge.High - edge.Low;
                var ex = edge.Slope * ey;
                var wx = edge.StartX - ox;
                var wz = edge.Low - oz;
                var denominator = mode == 512 ? MathF.FusedMultiplyAdd(dx, ey, -(dz * ex)) : dx * ey - dz * ex;
                var absolute = MathF.Abs(denominator);
                if (absolute > 1e-9f)
                {
                    var timeNumerator = fused ? MathF.FusedMultiplyAdd(wx, ey, -(wz * ex)) : wx * ey - wz * ex;
                    var segmentNumerator = fused ? MathF.FusedMultiplyAdd(wx, dz, -(wz * dx)) : wx * dz - wz * dx;
                    if (denominator < 0) { timeNumerator = -timeNumerator; segmentNumerator = -segmentNumerator; }
                    var segmentLimit = vector ? absolute : (1f - 1e-6f) * absolute;
                    if (timeNumerator < minTime * absolute || timeNumerator > limit * absolute
                        || segmentNumerator < 0 || segmentNumerator >= segmentLimit) continue;
                    // Scalar source assigns the quotient even when division rounds one ULP above best.
                    var candidate = timeNumerator / absolute;
                    blockBest = vector ? Math.Min(blockBest, candidate) : candidate;
                }
                else
                {
                    var collinear = fused ? MathF.FusedMultiplyAdd(wx, dz, -(wz * dx)) : wx * dz - wz * dx;
                    if (MathF.Abs(collinear) > EDGE_EPSILON) continue;
                    var a = (fused ? MathF.FusedMultiplyAdd(wx, dx, wz * dz) : wx * dx + wz * dz) * inverseLength;
                    var bx = vector ? wx + ex : edge.StartX + ex - ox;
                    var bz = vector ? wz + ey : edge.Low + ey - oz;
                    var b = (fused ? MathF.FusedMultiplyAdd(bx, dx, bz * dz) : bx * dx + bz * dz) * inverseLength;
                    if (a > b) (a, b) = (b, a);
                    var overlap = Math.Max(a, minTime);
                    if (overlap <= b && overlap <= limit) blockBest = Math.Min(blockBest, overlap);
                }
            }
            currentBest = blockBest;
        }
    }
}
