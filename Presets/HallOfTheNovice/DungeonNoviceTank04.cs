using System.Numerics;
using MasterPlayer.Mechanics.Quests;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private ulong noviceTank04Primary;
    private readonly HashSet<ulong> noviceTank04Known = [];
    private readonly List<ulong> noviceTank04Pending = [];

    private void ObserveNoviceTank04()
    {
        if (territory != NoviceCatalog.TANK04) return;
        noviceTank04Known.RemoveWhere(id => !_actors.TryGetValue(id, out var actor) || actor.Oid != 0x1564);
        noviceTank04Pending.RemoveAll(id => !_actors.TryGetValue(id, out var actor) || actor.Oid != 0x1564 || actor.Dead);
        foreach (var actor in NoviceActors.Where(a => a.Oid == 0x1564))
        {
            if (!noviceTank04Known.Add(actor.Id)) continue;
            if (noviceTank04Primary == 0)
                noviceTank04Primary = actor.Id;
            else if (noviceTank04Primary != actor.Id)
                noviceTank04Pending.Add(actor.Id);
        }
        // The source promotes pending modules in reverse insertion order, before unloading this tick's primary.
        if (noviceTank04Primary == 0 && noviceTank04Pending.Count > 0)
        {
            noviceTank04Primary = noviceTank04Pending[^1];
            noviceTank04Pending.RemoveAt(noviceTank04Pending.Count - 1);
        }
        if (noviceTank04Primary != 0
            && (!_actors.TryGetValue(noviceTank04Primary, out var primary) || primary.Oid != 0x1564
                || noviceCompleted.Contains(noviceTank04Primary)))
        {
            noviceTank04Primary = 0;
            if (Active != null) ResetFight();
        }
    }

    internal NoviceTank04Plan? NoviceTank04
    {
        get
        {
            if (territory != NoviceCatalog.TANK04 || Active == null
                || _actors.GetValueOrDefault(PlayerId) is not { } player) return null;
            var dummies = NoviceActors.Where(a => a.Oid == 0x1564).ToArray();
            if (dummies.Length != 3) return null;
            var minX = dummies.Min(a => a.Position.X);
            var maxX = dummies.Max(a => a.Position.X);
            var minZ = dummies.Min(a => a.Position.Z);
            var maxZ = dummies.Max(a => a.Position.Z);
            var center = new Vector3((maxX - minX) / 2 + minX, player.Position.Y, (maxZ - minZ) / 2 + minZ);
            var action = player.ClassJob switch
            {
                3 or 21 => 41u,
                1 or 19 => 7381u,
                32 => 3621u,
                _ => 11u,
            };
            return new(dummies, center, action,
                Vector2.Distance(new(player.Position.X, player.Position.Z), new(center.X, center.Z)) < 1);
        }
    }

    private IEnumerable<ulong> NoviceForbiddenTargets => territory == NoviceCatalog.TANK04 && Active != null
        ? QuestTargetPriority.ForbiddenTargets(territory, NoviceTargetActors,
            _actors.GetValueOrDefault(PlayerId)?.Position ?? default,
            _actors.Values.Where(a => a.PartySlot >= 0).Select(a => a.Id).ToHashSet(), AllowedFateId) : [];

    private IEnumerable<string> NoviceTank04Hints()
    {
        if (NoviceTank04 is not { } plan) yield break;
        // The previous bridge discarded weight-1 goals and had no ActionsToExecute consumer.
        yield return $"Goal: ({plan.Center.X:f2}, {plan.Center.Z:f2}), R{plan.GoalRadius}, weight={plan.GoalWeight}";
        if (plan.RequestsAction)
            yield return $"Action intent: Spell {plan.Action}, target=none, priority={plan.ActionPriority}, clear={plan.ClearsActionIntents}";
    }
}
