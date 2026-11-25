using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonActor[] QuestTowerParty => _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
    private static bool QuestTowerInside(DungeonQuestTower tower, Vector3 position)
        => NativeEnemyDistanceSquared(tower.Position, position) <= 9;
    private int QuestTowerCount(DungeonQuestTower tower)
        => QuestTowerParty.Count(a => a.PartySlot != tower.ForbiddenSlot && QuestTowerInside(tower, a.Position));

    private IEnumerable<DungeonAmbientAoe> QuestTowerAoes(string prefix, IReadOnlyList<DungeonQuestTower> towers, bool firstOnly = false)
    {
        if (towers.Count == 0 || Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var joins = new List<IGeometryShape>();
        var avoids = new List<DungeonQuestTower>();
        var inTower = towers.Any(t => QuestTowerInside(t, player.Position) && QuestTowerCount(t) == 1);
        foreach (var tower in towers)
        {
            var inside = QuestTowerInside(tower, player.Position);
            var count = QuestTowerCount(tower);
            var forbidden = tower.ForbiddenSlot >= 0 && player.PartySlot == tower.ForbiddenSlot;
            var canJoin = !forbidden && (count < 1 || inside && count == 1);
            if (forbidden || !inside && count >= 1)
                yield return new($"{prefix}/{tower.Id}/display", "Tower", new CircleTemplate(3), tower.Position, Navigate: false);
            else
                yield return new($"{prefix}/{tower.Id}/display", "Tower", new CircleTemplate(3), tower.Position,
                    Navigate: false, Safe: canJoin, OutlineOnly: true);
            if (canJoin) joins.Add(new CircleGeometry(tower.Position, 3));
            else if (forbidden || count > 1 || !inside && count == 1) avoids.Add(tower);
        }
        // Electrocution2 deliberately replaces the parent AI, including its damage prediction.
        if (firstOnly)
        {
            yield return new($"{prefix}/join", "Enter first tower",
                new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(towers[0].Position, 3)]),
                Arena.Position, Display: false, Activation: default(DateTime));
            yield break;
        }
        if (joins.Count > 0)
            yield return new($"{prefix}/join", "Soak the tower", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: towers[0].Activation);
        // Source inTower does not test ForbiddenSoakers. Keep that exception when a legal NPC shares the tower.
        else if (!inTower)
            foreach (var tower in avoids)
                yield return new($"{prefix}/{tower.Id}/avoid", "Avoid occupied or forbidden tower", new CircleTemplate(3),
                    tower.Position, Display: false, Activation: towers[0].Activation);
    }

    private IEnumerable<string> QuestTowerHints(IReadOnlyList<DungeonQuestTower> towers, Vector3 player)
    {
        if (towers.Count == 0) yield break;
        var slot = _actors.GetValueOrDefault(PlayerId)?.PartySlot ?? -1;
        bool Forbidden(DungeonQuestTower t) => t.ForbiddenSlot >= 0 && t.ForbiddenSlot == slot;
        if (towers.Any(t => Forbidden(t) && QuestTowerInside(t, player)))
        {
            yield return "GTFO from tower!";
            yield break;
        }
        var own = towers.FirstOrDefault(t => !Forbidden(t) && QuestTowerInside(t, player));
        if (own != null)
            yield return QuestTowerCount(own) switch
            {
                0 => "Too few soakers in the tower!",
                1 => "Soak the tower!",
                _ => "Too many soakers in the tower!",
            };
        else if (towers.Any(t => !Forbidden(t) && QuestTowerCount(t) < 1))
            yield return "Soak the tower!";
    }

    private IEnumerable<DungeonDamagePrediction> QuestTowerDamage(IReadOnlyList<DungeonQuestTower> towers)
    {
        if (towers.Count == 0) yield break;
        foreach (var actor in QuestTowerParty.Where(a => towers.Any(t => a.PartySlot != t.ForbiddenSlot && QuestTowerInside(t, a.Position))))
            yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, towers[0].Activation, "Raidwide");
    }
}
