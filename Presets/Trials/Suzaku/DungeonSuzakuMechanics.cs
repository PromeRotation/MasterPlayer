using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsSuzaku => territory == SuzakuCatalog.TERRITORY;
    private bool suzakuRing;
    private void SuzakuFinish(DungeonPendingCast cast)
    {
        if (!IsSuzaku || cast.Rule.Action != 12844 || suzakuRing || Arena == null) return;
        suzakuRing = true;
        Arena = FourLordsArenas.SuzakuRing(Arena.Position.Y);
    }

    private IEnumerable<(ulong Source, Vector3 From, Vector3 To)> SuzakuKnockbacks()
    {
        if (!IsSuzaku || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 12848))
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var delta = from - cast.Location; delta.Y = 0;
            if (delta.LengthSquared() == 0) continue;
            var to = from + Vector3.Normalize(delta) * 8;
            yield return (cast.Source, from, to);
            from = to;
        }
    }

    private IEnumerable<DungeonAmbientAoe> SuzakuAoes()
    {
        if (!IsSuzaku || Active == null) yield break;
        foreach (var (source, from, to) in SuzakuKnockbacks())
            foreach (var aoe in TrialKnockbackDrawing($"suzaku/knockback/{source}", "朱雀击退落点", from, to))
                yield return aoe;
    }

    private IEnumerable<string> SuzakuHints()
    {
        if (!IsSuzaku || Arena == null) yield break;
        if (SuzakuKnockbacks().Any(k => !Arena.Contains(k.To))) yield return "击退落点将出界";
    }
}
