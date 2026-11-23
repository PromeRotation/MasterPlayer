using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static readonly Vector2[] yuweSmallPoints = [new(34, -710), new(31.8f, -715.5f), new(35, -721.7f), new(41, -722.5f)];
    private static readonly Vector2[] yuweBigPoints =
    [
        new(34, -710), new(28.7f, -708.2f), new(29.4f, -714), new(35.4f, -715.8f),
        new(40, -711), new(38.7f, -705), new(34, -701.5f), new(28, -701.4f), new(24, -704.399f),
        new(22, -709.7f), new(23.1f, -715.099f), new(26.5f, -719.499f), new(32, -721.699f),
        new(38, -721.5f), new(43, -717.999f), new(45.7f, -712.699f), new(45.9f, -706.699f),
        new(42.9f, -701.2f), new(38.5f, -697), new(32.5f, -695.199f),
    ];

    private static bool YuweAngleEqual(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, MathF.Tau)) <= MathF.PI / 180;

    private static Vector3 YuweRotate(Vector3 position, float angle)
    {
        var x = position.X - 34;
        var z = position.Z + 710;
        return new(34 + x * MathF.Cos(angle) - z * MathF.Sin(angle), position.Y, -710 + x * MathF.Sin(angle) + z * MathF.Cos(angle));
    }

    private void YuweEarthCast(DungeonPendingCast cast)
    {
        if (Arena == null) return;
        void Add(Vector2[] points, float angle, double delay, double interval, string key)
        {
            for (var i = 0; i < points.Length; ++i)
                yuweEarth.Add(new($"yuwe/earth/{key}/{i}", "跃地连续爆炸", new CircleTemplate(5),
                    YuweRotate(new(points[i].X, Arena.Position.Y, points[i].Y), angle), Activation: _now.AddSeconds(delay + interval * i)));
        }
        if (cast.Rule.Action == 40661)
        {
            Add(yuweBigPoints, cast.Rotation, 4.5, .25, $"big/{cast.Source}/{cast.StartedAt.Ticks}");
            yuweEarthShown = 10;
            return;
        }
        yuweAngles.Add(cast);
        if (yuweAngles.Count < 4) return;
        var casts = yuweAngles.ToArray();
        var serial = ++yuweSerial;
        yuweSmallPatterns.Add((casts, serial));
        for (var i = 0; i < casts.Length; ++i)
            Add(yuweSmallPoints, YuweSmallAngle(casts, i), 6.5, .2, $"small/{serial}/{i}");
        yuweEarth.Sort((a, b) => a.Activation!.Value.CompareTo(b.Activation));
        yuweAngles.Clear(); yuweEarthShown = 16;
    }

    private static float YuweSmallAngle(DungeonPendingCast[] casts, int index)
    {
        var total = casts.Sum(c => c.Rotation);
        var angle = casts[index].Rotation;
        if ((int)total is 4 or -1) return angle;
        var intercardinal = new[] { -45.003f, 44.998f, 134.999f, -135.005f }.Any(a => YuweAngleEqual(a * MathF.PI / 180, angle));
        float offset;
        if ((int)(2 * total) == 3) offset = intercardinal ? -45 : -135;
        else offset = intercardinal ? 45 : -45;
        return angle + offset * MathF.PI / 180;
    }

    private void YuweEarthCorrection(DungeonPendingCast cast)
    {
        var i = yuweAngles.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
        if (i >= 0) yuweAngles[i] = cast;
        void Update(string prefix, Vector2[] points, float angle)
        {
            for (var n = 0; n < yuweEarth.Count; ++n)
            {
                var a = yuweEarth[n];
                if (!a.Id.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var step = int.Parse(a.Id.AsSpan(prefix.Length), System.Globalization.CultureInfo.InvariantCulture);
                yuweEarth[n] = a with { Position = YuweRotate(new(points[step].X, a.Position.Y, points[step].Y), angle) };
            }
        }
        if (cast.Rule.Action == 40661)
            Update($"yuwe/earth/big/{cast.Source}/{cast.StartedAt.Ticks}/", yuweBigPoints, cast.Rotation);
        foreach (var group in yuweSmallPatterns)
        {
            i = Array.FindIndex(group.Casts, c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (i < 0) continue;
            group.Casts[i] = cast;
            for (var n = 0; n < group.Casts.Length; ++n)
                Update($"yuwe/earth/small/{group.Serial}/{n}/", yuweSmallPoints, YuweSmallAngle(group.Casts, n));
        }
    }
}
