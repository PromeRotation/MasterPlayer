using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonPendingCast> delayedDamage = [];

    private ushort StatusParam(DungeonActor actor, uint status)
    {
        if (statusChanges.TryGetValue((actor.Id, status), out var change) && _now < change.ChangedAt.AddSeconds(.5))
            return change.Present ? change.Param : (ushort)0;
        return actor.StatusParams?.GetValueOrDefault(status) ?? 0;
    }

    internal IEnumerable<DungeonActor> CleansingFruits
    {
        get
        {
            var status = Active?.BossOid switch { 0x4B0C => 302u, 0x4B0E => 303u, _ => 0u };
            if (status == 0 || !_actors.TryGetValue(PlayerId, out var player) || StatusParam(player, status) <= 2)
                return [];
            return _actors.Values.Where(a => !a.Dead && a.Targetable && AurumValeCatalog.FruitFor(Active!.BossOid, a.Oid));
        }
    }

    internal DungeonActor? FruitTarget => _actors.TryGetValue(PlayerId, out var player)
        ? CleansingFruits.OrderBy(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(player.Position.X, player.Position.Z))).FirstOrDefault()
        : null;

    private IEnumerable<DungeonAmbientAoe> FruitAoes()
    {
        foreach (var fruit in CleansingFruits)
            yield return new($"fruit/display/{fruit.Id}", "解除三层减益：食用果实", new CircleTemplate(3),
                fruit.Position, Navigate: false, OutlineOnly: true, Safe: true);
        if (FruitTarget is { } target)
            yield return new("fruit/goal", "接近解除减益的果实", new CircleTemplate(2 + target.HitboxRadius),
                target.Position, Display: false, Safe: true);
    }
}
