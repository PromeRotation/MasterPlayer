using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, DungeonActor Actor, bool Removed)> steelSwords = [];
    private readonly Dictionary<ulong, DungeonActor> steelObjects = [];
    private readonly List<(ulong Id, DungeonPendingCast Cast)> steelBolts = [];
    private readonly Dictionary<ulong, DungeonPendingCast> steelBoltStarted = [];
    private ulong steelObjectId;
    private bool steelCasting;
    private bool IsSteel => territory == JobQuestCatalog.STEEL_AGAINST_STEEL;

    private void SteelCreated(DungeonActor actor)
    {
        if (!IsSteel || Active == null || actor.Oid != 0x2A46 || steelObjects.ContainsKey(actor.Id)) return;
        steelObjects[actor.Id] = actor;
        steelSwords.Add((++steelObjectId, actor, false));
    }

    private void SteelRemoved(ulong id)
    {
        if (!IsSteel || !steelObjects.Remove(id)) return;
        for (var i = 0; i < steelSwords.Count; ++i)
        {
            var sword = steelSwords[i];
            if (!sword.Removed && sword.Actor.Id == id) steelSwords[i] = (sword.Id, sword.Actor, true);
        }
    }

    private void SteelObserve()
    {
        if (!IsSteel || Active == null) return;
        foreach (var id in steelObjects.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) SteelRemoved(id);
        for (var i = 0; i < steelSwords.Count; ++i)
        {
            var sword = steelSwords[i];
            if (!sword.Removed && _actors.TryGetValue(sword.Actor.Id, out var actor))
                steelSwords[i] = (sword.Id, actor, false);
        }
    }

    private void SteelCast(DungeonPendingCast cast)
    {
        if (!IsSteel) return;
        if (cast.Rule.Action == 17568 && steelSwords.Count > 0) steelCasting = true;
        if (cast.Rule.Action != 17561) return;
        if (steelBoltStarted.TryGetValue(cast.Source, out var previous)
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            var index = steelBolts.FindIndex(b => ReferenceEquals(b.Cast, previous));
            if (index >= 0) steelBolts[index] = (steelBolts[index].Id, cast);
        }
        else
            steelBolts.Add((++steelObjectId, cast));
        steelBoltStarted[cast.Source] = cast;
    }

    private void SteelFinish(DungeonPendingCast cast)
    {
        if (!IsSteel || cast.Rule.Action != 17561) return;
        steelBoltStarted.Remove(cast.Source);
        var index = steelBolts.FindIndex(b => b.Cast.Source == cast.Source);
        if (index >= 0) steelBolts.RemoveAt(index);
    }

    private void SteelEffect(ulong source, uint action)
    {
        if (!IsSteel || action != 17567 || !steelObjects.ContainsKey(source)) return;
        var index = steelSwords.FindIndex(s => !s.Removed && s.Actor.Id == source);
        if (index >= 0) steelSwords.RemoveAt(index);
        if (steelSwords.Count == 0) steelCasting = false;
    }

    private IEnumerable<DungeonAmbientAoe> SteelAoes()
    {
        if (!IsSteel || Active == null) yield break;
        foreach (var bolt in steelBolts.Take(4))
            yield return new($"steel/bolt/{bolt.Id}", "Captive Bolt", RayRectangleTemplate.Symmetric(10, 50),
                bolt.Cast.Location, bolt.Cast.Rotation, Activation: bolt.Cast.FinishAt);
        if (!steelCasting) yield break;
        // The primary's current cast supplies the clock; cancellation does not disable the sword group.
        var activation = _actors.GetValueOrDefault(PrimaryId)?.Cast?.FinishAt ?? default;
        foreach (var sword in steelSwords)
            yield return new($"steel/sword/{sword.Id}", "Terminus Est", RayRectangleTemplate.Symmetric(4, 40),
                sword.Actor.Position, sword.Actor.Rotation, Activation: activation);
    }
}
