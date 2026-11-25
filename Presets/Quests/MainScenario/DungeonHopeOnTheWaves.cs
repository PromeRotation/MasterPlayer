using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsHopeOnTheWaves => territory == MainScenarioQuestCatalog.HOPE_ON_THE_WAVES;
    private bool hopeWiping;
    private readonly Dictionary<ulong, DungeonActor> hopeActors = [];
    private readonly List<ulong> hopeDetonators = [];

    private void HopeObserve()
    {
        if (!IsHopeOnTheWaves) return;
        // Preserve the entire actor dictionary: unrelated creations also reuse its freed slots.
        foreach (var id in hopeActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            hopeActors.Remove(id);
        foreach (var actor in _actors.Values)
            hopeActors[actor.Id] = actor;
    }

    private Vector3? HopeLimitBreakCenter => hopeActors.Values.FirstOrDefault(a => a.Oid == 0x21AC)?.Cast is { Action: 10894 } cast
        ? cast.Location : null;

    internal IReadOnlyList<DungeonEnemyDirective> HopeAdds
    {
        get
        {
            if (!IsHopeOnTheWaves || Active == null || HopeLimitBreakCenter is not { } center) return [];
            return _actors.Values.Where(a => a.IsEnemy == true && a.Targetable && !a.Dead && a.Oid == 0x2114
                && a.ObjectIndex is { } index && index < 200 && (index & 1) == 0)
                .Select(a => new DungeonEnemyDirective(a, center, a.Rotation, true)).ToArray();
        }
    }

    private void HopeCast(DungeonPendingCast cast)
    {
        if (IsHopeOnTheWaves && cast.Rule.Action == 10928 && !hopeDetonators.Contains(cast.Source))
            hopeDetonators.Add(cast.Source);
    }

    private void HopeFinish(DungeonPendingCast cast)
    {
        if (IsHopeOnTheWaves && cast.Rule.Action == 10928) hopeDetonators.Remove(cast.Source);
    }

    private IEnumerable<string> HopeHints()
    {
        if (!IsHopeOnTheWaves || Active == null) yield break;
        var first = hopeDetonators.Select(id => _casts.GetValueOrDefault(id)).FirstOrDefault(c => c != null);
        if (first != null)
        {
            var remaining = 0f;
            if (_actors.GetValueOrDefault(first.Source)?.Cast is { } observed)
            {
                var total = Math.Max(.05f, observed.TotalTime ?? (float)(observed.FinishAt - observed.StartedAt).TotalSeconds - .3f);
                var elapsed = Math.Clamp((float)(_now - observed.StartedAt).TotalSeconds, 0, total);
                remaining = total + .3f - elapsed;
            }
            yield return $"Kill before detonation! {remaining:f1}s left";
        }
        foreach (var add in HopeAdds)
            yield return $"拉小怪 {add.Actor.Id:X} 至 ({add.Position.X:f2}, {add.Position.Z:f2})";
    }

    private void HopeDirector(uint update)
    {
        if (!IsHopeOnTheWaves || update is not (0x40000005 or 0x40000011)) return;
        hopeWiping = update == 0x40000005;
        if (hopeWiping) ResetFight();
    }
}
