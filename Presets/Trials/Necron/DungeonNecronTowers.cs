using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> NecronTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player || necronTowers.Count == 0) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        int Count(Vector3 position) => party.Count(a => new CircleGeometry(position, 3).Contains(a.Position));
        bool Inside(Vector3 position) => new CircleGeometry(position, 3).Contains(player.Position);
        var best = -1;
        for (var i = 0; i < necronTowers.Count; ++i)
        {
            var tower = necronTowers[i]; var count = Count(tower.Position);
            if (count == 0 || count > 4 || count == 1 && Inside(tower.Position)) continue;
            if (best < 0 || count >= Count(necronTowers[best].Position)
                && Distance(tower.Position, player.Position) < Distance(necronTowers[best].Position, player.Position)) best = i;
        }
        var joins = new List<IGeometryShape>();
        var avoids = new List<(Vector3 Position, int Key)>();
        var inCorrect = false;
        foreach (var tower in necronTowers)
        {
            var count = Count(tower.Position); var inside = Inside(tower.Position);
            var safe = count < 4 || inside && count <= 4;
            if (safe || inside || count >= 4)
                yield return new($"necron/tower/{tower.Key}/display", "四人塔", new CircleTemplate(3), tower.Position,
                    Navigate: false, Safe: safe, OutlineOnly: safe || inside);
            if (inside && count == 4) inCorrect = true;
            if (best >= 0) continue;
            if (safe) joins.Add(new CircleGeometry(tower.Position, 3));
            else avoids.Add((tower.Position, tower.Key));
        }
        if (best >= 0) joins.Add(new CircleGeometry(necronTowers[best].Position, 3));
        if (joins.Count > 0)
            yield return new("necron/tower/join", "优先补齐已有人的塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: necronTowers[0].At);
        else if (!inCorrect)
            foreach (var avoid in avoids)
                yield return new($"necron/tower/{avoid.Key}/avoid", "离开满员塔", new CircleTemplate(3), avoid.Position,
                    Display: false, Activation: necronTowers[0].At);
        static float Distance(Vector3 a, Vector3 b) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z));
    }
}
