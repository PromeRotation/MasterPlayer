using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonTyphoonLine(ulong source, Vector3 position)
{
    internal ulong Source { get; } = source;
    internal Vector3 Next { get; private set; } = position;
    internal int Left { get; private set; } = 4;
    internal int Shown { get; private set; } = 4;
    internal DateTime NextExplosion { get; private set; }
    internal DungeonPendingCast[] Predictions { get; private set; } = [];

    internal void Initialize(DateTime now)
    {
        NextExplosion = now;
        Rebuild(now);
    }

    internal void Advance(Vector3 position, DateTime now)
    {
        if (Shown == 4)
        {
            Shown = 10;
            Left = 15;
        }
        Next = position - Vector3.UnitX * 1.745f;
        NextExplosion = now.AddSeconds(.6);
        --Left;
        Rebuild(now);
    }

    private void Rebuild(DateTime now)
    {
        Predictions = Enumerable.Range(0, Math.Min(Left, Shown)).Select(i =>
        {
            var point = Next - Vector3.UnitX * (1.745f * i);
            var grid = 2000f / 65535f;
            var inverse = 1f / grid;
            point.X = ((int)MathF.Round(point.X * inverse) - .5f) * grid;
            point.Z = ((int)MathF.Round(point.Z * inverse) - .5f) * grid;
            return new DungeonPendingCast(Source, new(28731, 0x38D0, "Typhoon", new CircleTemplate(3)),
                point, 0, now, NextExplosion.AddSeconds(.6 * i));
        }).ToArray();
    }
}
