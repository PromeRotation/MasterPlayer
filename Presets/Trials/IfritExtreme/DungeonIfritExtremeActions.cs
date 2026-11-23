using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int IfritExPriority(DungeonActor actor)
    {
        if (!IfritExAi) return 0;
        if (actor.Oid == 0xD3) return 1;
        return IfritExNails && IfritExNextNail?.Id == actor.Id ? 2 : 0;
    }

    internal IReadOnlyList<DungeonActionRequest> IfritExActions()
    {
        if (!IsIfritExtreme || Active == null || !IfritExAi || IfritExBoss is not { } boss
            || _actors.GetValueOrDefault(PlayerId) is not { Dead: false } player) return [];
        List<DungeonActionRequest> actions = [];
        if (!IfritExNails || boss.TargetId != PlayerId) return actions;
        var elapsed = (_now - ifritExNailsAt).TotalSeconds;
        void Plan(uint action, double from, double to)
        {
            if (action != 0 && elapsed >= from && elapsed < to)
                actions.Add(new(action, PlayerId, ifritExNailsAt.AddSeconds(to)));
        }
        if (ifritExWave == 0) Plan(7531, 10, 20);
        else if (ifritExWave == 1) Plan(7531, 8, 13);
        else
        {
            Plan(7531, 9, 13); Plan(7531, 91, 97);
            if (_actors.Values.Any(a => a.Hostile && a.Targetable && !a.Dead
                && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(player.Position.X, player.Position.Z)) <= MathF.Pow(5 + a.HitboxRadius, 2)))
            {
                Plan(7535, 20, 25); Plan(7535, 102, 109);
            }
            var strong = player.ClassJob switch { 19u => 17u, 21u => 44u, _ => 0u };
            Plan(strong, 35, 40); Plan(strong, 57, 63);
        }
        return actions;
    }
}
