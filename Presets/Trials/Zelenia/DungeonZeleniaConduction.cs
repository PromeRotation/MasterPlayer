using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private void ZeleniaAddTile(int tile, DateTime at, ulong source)
    {
        if (Arena == null) return;
        zeleniaThunderIII.Add((new($"zelenia/thunder3/{++zeleniaKey}", "传导扇区",
            new FanTemplate(16, MathF.PI / 3), Arena.Position, (180f - 60f * tile) * (MathF.PI / 180), Activation: at), source, tile));
    }

    private void ZeleniaConduct(DungeonPendingCast cast)
    {
        if (Arena == null) return;
        zeleniaThunderIII.Add((new($"zelenia/thunder3/{++zeleniaKey}", "亚历山大雷III落圈",
            new CircleTemplate(4), cast.Location, Activation: cast.FinishAt), cast.Source, -1));
        for (var seed = 0; seed < 6; ++seed)
        {
            if (!zeleniaTiles[seed] || !ZeleniaCircleIntersectsSector(cast.Location, Arena.Position, seed)) continue;
            var left = seed;
            // 源在六格全亮时左扩无限循环；最多五步保留正常输入顺序，全亮时各格发布一次。
            for (var i = 0; i < 5 && zeleniaTiles[(left + 5) % 6]; ++i) left = (left + 5) % 6;
            for (var i = 0; i < 6 && zeleniaTiles[left]; ++i, left = (left + 1) % 6)
                ZeleniaAddTile(left, cast.FinishAt, cast.Source);
            break;
        }
    }

    private static bool ZeleniaCircleIntersectsSector(Vector3 circle, Vector3 center, int tile)
    {
        var offset = new Vector2(circle.X - center.X, circle.Z - center.Z);
        var lengthSq = offset.LengthSquared();
        if (lengthSq <= 16) return true;
        if (lengthSq > 400) return false;
        var angle = (180f - 60f * tile) * (MathF.PI / 180);
        var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
        var normal = new Vector2(direction.Y, -direction.X);
        var distanceFromAxis = Vector2.Dot(offset, normal);
        var sin = MathF.Sin(30f * (MathF.PI / 180));
        if (Vector2.Dot(offset, direction) > 0 && distanceFromAxis * distanceFromAxis <= lengthSq * sin * sin) return true;
        if (distanceFromAxis < 0) normal = -normal;
        var side = direction * MathF.Cos(30f * (MathF.PI / 180)) + normal * sin;
        if (MathF.Abs(offset.X * side.Y - offset.Y * side.X) > 4) return false;
        var along = Vector2.Dot(offset, side);
        if (along < 0) return false;
        return along <= 16 || Vector2.DistanceSquared(offset, side * 16) <= 16;
    }
}
