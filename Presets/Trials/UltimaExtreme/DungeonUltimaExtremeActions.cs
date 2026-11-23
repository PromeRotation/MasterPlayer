using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal IReadOnlyList<DungeonActionRequest> UltimaActions()
    {
        if (!IsUltimaExtreme || Active == null || _actors.GetValueOrDefault(PlayerId) is not { Dead: false } player) return [];
        List<DungeonActionRequest> result = [];
        var tank = ThornTank(player);
        var melee = TwinCombatRole(player) == "Melee";
        if (_casts.Values.FirstOrDefault(c => c.Rule.Action == 1514) is { } purge)
        {
            var first = (ultimaPurgeHits & 1) == 0;
            var selected = first ? CurrentAssignment is "MT" or "M1" or "R1" : CurrentAssignment is "OT" or "M2" or "R2";
            if (selected)
            {
                if (tank && _actors.Values.Any(a => a.Hostile && !a.Dead && a.Targetable
                    && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(player.Position.X, player.Position.Z))
                        <= MathF.Pow(5 + a.HitboxRadius, 2)))
                    result.Add(new(7535, PlayerId, purge.FinishAt));
                else if (melee) result.Add(new(7549, PrimaryId, purge.FinishAt));
                else if (TwintaniaActionAvailable(7560, player.ClassJob)) result.Add(new(7560, PrimaryId, purge.FinishAt));
            }
        }
        if (UltimaBoomCasting && ultimaBoomHits >= 2 && UltimaOrbsActive && _actors.GetValueOrDefault(PrimaryId)?.TargetId != PlayerId)
        {
            var until = _casts.Values.First(c => c.Rule.Action == 1511).FinishAt;
            var surecast = player.ClassJob is 6 or 7 or 24 or 25 or 26 or 27 or 28 or 33 or 35 or 36 or 40 or 42;
            result.Add(new(surecast ? 7559u : 7548u, PlayerId, until));
        }
        if (ultimaFreefires.Count > 0 && ultimaFreefireHits > 0 && _actors.GetValueOrDefault(PrimaryId)?.TargetId == PlayerId)
        {
            if (player.ClassJob == 21) result.Add(new(43, PlayerId, ultimaFreefireAt));
            if (player.ClassJob == 19) result.Add(new(30, PlayerId, ultimaFreefireAt));
        }
        return result;
    }

    private IEnumerable<string> UltimaHints()
    {
        if (!IsUltimaExtreme || Active == null) yield break;
        if (ultimaKiters.Contains(PlayerId)) yield return "引导追踪球远离队伍";
        if (UltimaTankSwap && _actors.GetValueOrDefault(PlayerId) is { } player && ThornTank(player))
            yield return _actors.GetValueOrDefault(PrimaryId)?.TargetId == PlayerId ? "四层粘性魔科学物质：交接仇恨" : "四层粘性魔科学物质：准备换坦";
    }
}
