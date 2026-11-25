using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsDikaiosyne => territory == JobQuestCatalog.TO_HAVE_LOVED_AND_LOST;
    private readonly List<DungeonQuestTower> fractureTowers = [];
    private readonly List<(DungeonPendingCast Cast, Vector3 Origin, float Distance)> brandOfSinKnockbacks = [];
    private readonly Dictionary<ulong, DungeonPendingCast> dikaiosyneStarted = [];
    private readonly Dictionary<ulong, uint> dikaiosyneOids = [];
    private readonly HashSet<ulong> dikaiosyneCompleted = [];
    private long fractureGeneration;
    private bool dikaiosyneWiping;
    internal IReadOnlyList<DungeonQuestTower> FractureTowers => fractureTowers;
    internal IReadOnlyList<(DungeonPendingCast Cast, Vector3 Origin, float Distance)> BrandOfSinKnockbacks => brandOfSinKnockbacks;
    private static bool FractureAction(uint action) => action is 15576 or 13207 or 13208 or 13209 or 15374 or 16612;

    private void DikaiosyneInput()
    {
        if (!IsDikaiosyne) return;
        foreach (var id in dikaiosyneOids.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            Finish(id);
            dikaiosyneOids.Remove(id);
            dikaiosyneCompleted.Remove(id);
        }
        foreach (var actor in _actors.Values.ToArray())
        {
            if (dikaiosyneOids.TryGetValue(actor.Id, out var oid)) _actors[actor.Id] = actor with { Oid = oid };
            else dikaiosyneOids[actor.Id] = actor.Oid;
            if (actor.Oid == 0x2927 && (Active != null && actor.Id == PrimaryId || actor.Targetable && actor.InCombat)
                && (actor.Dead || actor.Hp == 0)) dikaiosyneCompleted.Add(actor.Id);
        }
    }

    private void DikaiosyneCast(DungeonPendingCast cast)
    {
        if (!IsDikaiosyne) return;
        var correction = dikaiosyneStarted.TryGetValue(cast.Source, out var previous)
            && previous.Rule.Action == cast.Rule.Action && previous.StartedAt == cast.StartedAt;
        if (FractureAction(cast.Rule.Action))
        {
            var index = correction ? fractureTowers.FindIndex(t => ReferenceEquals(t.Cast, previous)) : -1;
            if (correction && index < 0)
            {
                dikaiosyneStarted[cast.Source] = cast;
                return;
            }
            var tower = new DungeonQuestTower(index >= 0 ? fractureTowers[index].Id : ++fractureGeneration,
                cast, cast.Location, cast.FinishAt);
            if (index >= 0) fractureTowers[index] = tower;
            else fractureTowers.Add(tower);
        }
        if (cast.Rule.Action == 16132)
        {
            var index = correction ? brandOfSinKnockbacks.FindIndex(k => ReferenceEquals(k.Cast, previous)) : -1;
            var knockback = (cast, cast.Location, 10f);
            if (index >= 0) brandOfSinKnockbacks[index] = knockback;
            else brandOfSinKnockbacks.Add(knockback);
        }
        dikaiosyneStarted[cast.Source] = cast;
    }

    private void DikaiosyneFinish(DungeonPendingCast cast)
    {
        if (!IsDikaiosyne) return;
        if (FractureAction(cast.Rule.Action))
        {
            // One shared source list: any of the six actions removes the first exact LocXZ match.
            var index = fractureTowers.FindIndex(t => t.Position.X == cast.Location.X && t.Position.Z == cast.Location.Z);
            if (index >= 0) fractureTowers.RemoveAt(index);
        }
        if (cast.Rule.Action == 16132)
        {
            var index = brandOfSinKnockbacks.FindIndex(k => k.Cast.Source == cast.Source);
            if (index >= 0) brandOfSinKnockbacks.RemoveAt(index);
        }
        dikaiosyneStarted.Remove(cast.Source);
    }

    internal Vector3 DikaiosyneLanding(DungeonActor player)
    {
        var from = player.Position;
        foreach (var knockback in brandOfSinKnockbacks)
        {
            if (WhorlImmune(player, knockback.Cast.FinishAt)) continue;
            var direction = from - knockback.Origin;
            direction.Y = 0;
            if (direction.LengthSquared() > 0)
                from += Vector3.Normalize(direction) * knockback.Distance;
        }
        return from;
    }

    private IEnumerable<DungeonAmbientAoe> DikaiosyneAoes()
    {
        if (!IsDikaiosyne || Active == null) yield break;
        foreach (var aoe in QuestTowerAoes("dikaiosyne/fracture", fractureTowers)) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        // AwayFromOrigin, no wall stop and normal immunity. Source supplies display/text only, with no AI override.
        var from = player.Position;
        foreach (var knockback in brandOfSinKnockbacks)
        {
            if (WhorlImmune(player, knockback.Cast.FinishAt)) continue;
            var direction = from - knockback.Origin;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            var to = from + Vector3.Normalize(direction) * knockback.Distance;
            foreach (var aoe in QuestKnockbackProjection($"dikaiosyne/knockback/{knockback.Cast.Source}", player, from, to))
                yield return aoe;
            from = to;
        }
    }

    private IEnumerable<string> DikaiosyneHints(Vector3? position)
    {
        if (!IsDikaiosyne || Active == null || position is not { } player) yield break;
        foreach (var hint in QuestTowerHints(fractureTowers, player)) yield return hint;
        if (_actors.GetValueOrDefault(PlayerId) is { } actor && Arena?.Contains(DikaiosyneLanding(actor)) == false)
            yield return "Knockback into danger!";
    }

    private void DikaiosyneDirector(uint update)
    {
        if (!IsDikaiosyne || update is not (0x40000005 or 0x40000011)) return;
        dikaiosyneWiping = update == 0x40000005;
        if (!dikaiosyneWiping) return;
        ResetFight();
        dikaiosyneCompleted.Clear();
    }

    private void ResetDikaiosyne()
    {
        fractureTowers.Clear();
        brandOfSinKnockbacks.Clear();
        dikaiosyneStarted.Clear();
    }
}
