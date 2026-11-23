using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal DungeonActor? SlimeCleanseTarget(uint job)
    {
        if (Active?.BossOid != 0xCBA || job is not (6 or 24 or 28 or 33 or 40 or 23)
            || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Targetable && HasStatus(a, 569)
                && Vector3.DistanceSquared(player.Position, a.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    private IEnumerable<string> SastashaHardHints()
    {
        if (Active?.BossOid != 0xCBA) yield break;
        if (_actors.TryGetValue(PlayerId, out var player) && HasStatus(player, 420)
            && player.PartyRole is "MT" or "ST" or "Tank")
            yield return "猎物点名：穿过一名输出队友传递眩晕，保留坦克眩晕阻止螺旋尾";
        foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 569)))
            yield return $"粘液：尽快驱散 {member.PartyRole} {member.Name}";
    }

    private IEnumerable<DungeonAmbientAoe> SastashaHardAoes()
    {
        if (Active?.BossOid != 0xCBA) yield break;
        foreach (var marked in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 420)))
            yield return new($"prey/{marked.Id}", "猎物传递点名", new CircleTemplate(1), marked.Position,
                Navigate: false, OutlineOnly: true);
    }
}
