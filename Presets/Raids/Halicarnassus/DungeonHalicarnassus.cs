using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsHalicarnassus => territory == HalicarnassusCatalog.TERRITORY;
    internal HalicarnassusEncounter Halicarnassus { get; } = new();
    internal bool HalicarnassusInputScan { get; set; }

    private void HalicarnassusSynchronize(IEnumerable<DungeonActor> actors, DateTime now,
        Func<string, bool>? enabled, ulong playerId)
    {
        var values = actors.ToArray();
        _now = now;
        PlayerId = playerId;
        _actors.Clear();
        foreach (var actor in values) _actors[actor.Id] = actor;
        Halicarnassus.Synchronize(values, CurrentPartyMembers, playerId, now,
            enabled?.Invoke(Definitions[0].Name) != false, !HalicarnassusInputScan);
        HalicarnassusPublish();
    }

    private void HalicarnassusPublish()
    {
        Active = Halicarnassus.Active ? Definitions[0] : null;
        PrimaryId = Halicarnassus.Primary?.Actor.Id ?? 0;
        Arena = Halicarnassus.Loaded ? HalicarnassusCatalog.ARENA : null;
    }

    internal void HalicarnassusEvent(LogSystemEvent ev)
    {
        if (ev is NativeActionEffectEvent e && e.ActionType is null or 1)
            Halicarnassus.Effect(e.SourceId, e.ActionId);
        else if (ev is LogSystemDirectorUpdateEvent director)
            Halicarnassus.Director(director.Command);
        HalicarnassusPublish();
    }

    private IEnumerable<DungeonAmbientAoe> HalicarnassusAoes()
    {
        var model = Halicarnassus;
        if (!model.Loaded || model.Primary is not { } primary) yield break;
        var navigate = model.Active && model.Slot >= 0 && model.SourcePlayer != null && model.Actors.ContainsKey(PlayerId);
        if (!navigate)
            yield return new("halicarnassus/arena", "Arena outline", new RectangleTemplate(40, 40),
                default, Navigate: false, OutlineOnly: true);
        if (!primary.Actor.Dead && primary.Actor.Targetable)
            yield return new("halicarnassus/enemy", "Halicarnassus", new LocalPolygonTemplate(
                [new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
                primary.Actor.Position, primary.Actor.Rotation, Navigate: false, Color: new(1, 0, 0, 1));
        foreach (var aoe in model.Visible())
            yield return new($"halicarnassus/aoe/{aoe.Generation}", $"Action {aoe.Action}",
                aoe.Shape, aoe.Origin, aoe.Rotation, Navigate: navigate, Activation: aoe.Activation);
    }
}
