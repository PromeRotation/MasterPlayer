using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, DungeonActor Actor, bool Primed, bool Removed, DateTime Activation)> nodeExplosives = [];
    private readonly Dictionary<ulong, DungeonActor> nodeObjects = [];
    private ulong nodeExplosiveId;
    private bool IsSummoningNode => territory == JobQuestCatalog.ART_FOR_THE_LIVING;

    private void NodeCreated(DungeonActor actor)
    {
        if (!IsSummoningNode || Active == null || actor.Oid is not (0x1CD7 or 0x1CD5) || nodeObjects.ContainsKey(actor.Id)) return;
        nodeObjects[actor.Id] = actor;
        if (actor.Oid == 0x1CD7)
            nodeExplosives.Add((++nodeExplosiveId, actor, false, false, _now.AddSeconds(3)));
        else
        {
            var index = nodeExplosives.FindIndex(e => NodePositionMatches(e.Actor.Position, actor.Position));
            if (index < 0) return;
            var explosive = nodeExplosives[index];
            nodeExplosives[index] = (explosive.Id, actor, true, false, explosive.Activation);
        }
    }

    private static bool NodePositionMatches(Vector3 a, Vector3 b)
        => MathF.Abs(a.X - b.X) <= 1 && MathF.Abs(a.Z - b.Z) <= 1;

    private void NodeRemoved(ulong id)
    {
        if (!IsSummoningNode || !nodeObjects.Remove(id, out var actor)) return;
        // Bomb destruction removes the first position match, including an unprimed indicator.
        if (actor.Oid == 0x1CD5)
        {
            var index = nodeExplosives.FindIndex(e => NodePositionMatches(e.Actor.Position, actor.Position));
            if (index >= 0) nodeExplosives.RemoveAt(index);
        }
        for (var i = 0; i < nodeExplosives.Count; ++i)
        {
            var explosive = nodeExplosives[i];
            if (!explosive.Removed && explosive.Actor.Id == id)
                nodeExplosives[i] = (explosive.Id, explosive.Actor, explosive.Primed, true, explosive.Activation);
        }
    }

    private void NodeObserve()
    {
        if (!IsSummoningNode || Active == null) return;
        for (var i = 0; i < nodeExplosives.Count; ++i)
        {
            var explosive = nodeExplosives[i];
            if (!explosive.Removed && _actors.TryGetValue(explosive.Actor.Id, out var actor) && actor.Oid == explosive.Actor.Oid)
                nodeExplosives[i] = (explosive.Id, actor, explosive.Primed, false, explosive.Activation);
        }
        // Refresh surviving actor references before destruction matches a bomb against their positions.
        foreach (var pair in nodeObjects.Where(p => !_actors.TryGetValue(p.Key, out var actor) || actor.Oid != p.Value.Oid).ToArray())
            NodeRemoved(pair.Key);
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1CD7 or 0x1CD5))
        {
            if (nodeObjects.ContainsKey(actor.Id)) nodeObjects[actor.Id] = actor;
            else NodeCreated(actor);
        }
    }

    private IEnumerable<DungeonAmbientAoe> NodeAoes()
    {
        if (!IsSummoningNode || Active == null) yield break;
        // A dead primed entry remains in history; the source's default/null AOE entry has no usable geometry.
        foreach (var explosive in nodeExplosives.Where(e => !e.Actor.Dead || !e.Primed))
            yield return new($"node/explosive/{explosive.Id}", "Aetherochemical Explosive", new CircleTemplate(5),
                explosive.Actor.Position, Activation: explosive.Activation);
    }
}
