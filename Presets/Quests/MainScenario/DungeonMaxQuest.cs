using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsMaxQuest => territory == MainScenarioQuestCatalog.MAX_QUEST;
    private readonly HashSet<ulong> maxQuestCompleted = [];
    private bool maxQuestEnabled;
    private bool maxQuestWiping;

    // Fate1409.MapIcon=60501: the source base AI forbids DoTs while synced to this non-boss FATE.
    // This remains an intention and diagnostic; the existing PR API has no per-action DoT filter.
    private bool MaxQuestForbidsDots(DungeonActor actor)
        => IsMaxQuest && AllowedFateId == 1409 && actor.FateId == 1409 && actor.IsEnemy == true
            && actor.ObjectIndex is { } index && index < 200 && (index & 1) == 0;

    private IEnumerable<string> MaxQuestHints()
    {
        if (!IsMaxQuest || Active == null) yield break;
        foreach (var id in ForbiddenDotTargets)
            yield return $"停止对 {id:X} 施加持续伤害";
    }

    private void ObserveMaxQuest(Func<string, bool>? enabled)
    {
        if (!IsMaxQuest) return;
        maxQuestEnabled = enabled?.Invoke("MA-X") != false;
        maxQuestCompleted.RemoveWhere(id => !_actors.TryGetValue(id, out var actor) || actor.Oid != 0x252B);
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x252B))
            if ((Active != null && actor.Id == PrimaryId || maxQuestEnabled && actor.Targetable && actor.InCombat)
                && (actor.Dead || actor.Hp == 0))
                maxQuestCompleted.Add(actor.Id);
    }

    private IEnumerable<DungeonAmbientAoe> MaxQuestAoes()
    {
        if (!IsMaxQuest || !maxQuestEnabled || maxQuestWiping) yield break;
        var primary = Active != null ? _actors.GetValueOrDefault(PrimaryId)
            : _actors.Values.Where(a => a.Oid == 0x252B && !a.Dead && !maxQuestCompleted.Contains(a.Id))
                .OrderByDescending(a => a.Targetable).FirstOrDefault();
        if (primary == null || primary.Dead) yield break;
        var arena = Arena ?? Definitions[0].Arena(primary.Position);
        if (Active == null)
            yield return new("max/precombat/arena", "场地", new CircleTemplate(25), arena.Position,
                Navigate: false, OutlineOnly: true);
        var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Targetable
            && (a.Id == primary.Id || a.Oid is 0x2529 or 0x252A)))
        {
            var offset = actor.Position - arena.Position;
            offset.Y = 0;
            var outside = offset.LengthSquared() > 625;
            var position = outside ? arena.Position + Vector3.Normalize(offset) * 25 : actor.Position;
            var color = actor.Oid switch
            {
                0x2529 => new Vector4(1, 0, 1, 1),
                0x252A => new Vector4(1, 128f / 255, 0, 1),
                _ => new Vector4(1, 0, 0, 1),
            };
            yield return new($"max/enemy/{actor.Id}", "敌人及朝向", marker, position, actor.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true, Color: color);
        }
    }

    private void MaxQuestDirector(uint update)
    {
        if (!IsMaxQuest || update is not (0x40000005 or 0x40000011)) return;
        maxQuestWiping = update == 0x40000005;
        if (!maxQuestWiping) return;
        ResetFight();
        maxQuestCompleted.Clear();
    }
}
