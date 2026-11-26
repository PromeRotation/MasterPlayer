using System.Numerics;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsAlteRoite => territory == AlteRoiteCatalog.TERRITORY;
    internal AlteRoiteEncounter AlteRoite { get; } = new();
    internal bool AlteRoiteInputScan { get; set; }

    private void AlteRoiteSynchronize(IEnumerable<DungeonActor> actors, DateTime now,
        Func<string, bool>? enabled, ulong playerId)
    {
        var values = actors.ToArray();
        _now = now;
        PlayerId = playerId;
        _actors.Clear();
        foreach (var actor in values) _actors[actor.Id] = actor;
        AlteRoite.Synchronize(values, CurrentPartyMembers, playerId, now,
            enabled?.Invoke(Definitions[0].Name) != false, !AlteRoiteInputScan);
        AlteRoitePublish();
    }

    private void AlteRoitePublish()
    {
        Active = AlteRoite.Active ? Definitions[0] : null;
        PrimaryId = AlteRoite.Primary?.Actor.Id ?? 0;
        Arena = AlteRoite.Loaded ? AlteRoiteCatalog.ARENA : null;
    }

    internal void AlteRoiteEvent(LogSystemEvent ev)
    {
        if (ev is NativeActionEffectEvent effect && effect.ActionType is null or 1)
            AlteRoite.Effect(effect.SourceId, effect.ActionId, effect.MainTargetId);
        else if (ev is LogSystemTargetIconEvent icon)
            AlteRoite.Icon(icon.SourceId, icon.MarkerId);
        else if (ev is LogSystemDirectorUpdateEvent director)
            AlteRoite.Director(director.Command);
        AlteRoitePublish();
    }

    private IEnumerable<DungeonAmbientAoe> AlteRoiteAoes()
    {
        var model = AlteRoite;
        if (!model.Loaded || model.Primary is not { } primary) yield break;
        var navigate = model.Active && model.Slot >= 0 && model.SourcePlayer != null && model.Actors.ContainsKey(PlayerId);
        if (!navigate)
            yield return new("alteroite/arena", "Arena outline", new CircleTemplate(20), default,
                Navigate: false, OutlineOnly: true);
        if (!primary.Actor.Dead && primary.Actor.Targetable)
            yield return new("alteroite/enemy", "Alte Roite", new LocalPolygonTemplate(
                [new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
                primary.Actor.Position, primary.Actor.Rotation, Navigate: false, Color: new(1, 0, 0, 1));
        if (!model.Active || !model.Actors.TryGetValue(PlayerId, out var viewer)) yield break;
        foreach (var aoe in model.Visible())
            yield return new($"alteroite/aoe/{aoe.Action}/{aoe.Source.Actor.Id}/{aoe.Generation}", $"Action {aoe.Action}",
                aoe.Shape!, aoe.Origin, aoe.Rotation, Navigate: navigate, Activation: aoe.Activation,
                Color: aoe.Action == 7896 ? new Vector4(1, 1, 0, 1) : null);
        foreach (var action in new uint[] { 9182, 9186, 7896 })
        {
            var i = 0;
            foreach (var (from, to) in model.Movements(action))
                foreach (var aoe in TrialKnockbackDrawing($"alteroite/knockback/{action}/{i++}", $"Action {action}", from, to))
                    yield return aoe;
        }
        if (model.Slot >= 0 && (model.IceMask & (1ul << model.Slot)) != 0)
        {
            yield return new("alteroite/ice/range", "ThinIce R6", new CircleTemplate(6), viewer.Actor.Position,
                Navigate: false, OutlineOnly: true, Color: new(1, 0, 1, 1));
            if (!LegacyMovement || CameraAzimuth != null)
                foreach (var aoe in TrialKnockbackDrawing("alteroite/ice", "ThinIce forward6 stop at wall",
                    viewer.Actor.Position, model.IceLanding(viewer, LegacyMovement, CameraAzimuth ?? 0)))
                    yield return aoe;
        }
        // SimpleKnockbacks has no AddAIHints override. ThinIce's source FZ is
        // rejected by the existing bridge sampler; neither adds hard navigation.
        foreach (var aoe in AlteRoitePartyAoes(viewer, navigate)) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> AlteRoitePartyAoes(AlteRoiteActor viewer, bool navigate)
    {
        var model = AlteRoite;
        var party = model.Party.Where(p => !p.Actor.Dead).ToArray();
        var spreads = model.Spreads.Where(s => !s.Target.Actor.Dead).ToArray();
        var stacks = model.Stacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var ownSpread = spreads.Any(s => ReferenceEquals(s.Target, viewer));
        var ownStack = stacks.Any(s => ReferenceEquals(s.Target, viewer));
        var storedMark = model.Stacks.Any(s => ReferenceEquals(s.Target, viewer))
            || model.Spreads.Any(s => ReferenceEquals(s.Target, viewer));
        foreach (var spread in spreads)
        {
            var position = QuantizedDungeonPosition(spread.Target.Actor.Position);
            yield return new($"alteroite/spread/{spread.Generation}", "Spread R6", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true);
            if (!navigate || party.Length < 2) continue;
            if (!ReferenceEquals(spread.Target, viewer))
                yield return new($"alteroite/spread/{spread.Generation}/avoid", "Spread avoid R7",
                    new CircleTemplate(7), position, Display: false, Activation: spread.Activation);
            else
                foreach (var buddy in party.Where(p => !spreads.Any(s => ReferenceEquals(s.Target, p))))
                    yield return new($"alteroite/spread/{spread.Generation}/buddy/{buddy.Actor.Id}", "Spread away from party",
                        new CircleTemplate(7), QuantizedDungeonPosition(buddy.Actor.Position),
                        Display: false, Activation: spread.Activation);
        }
        List<IGeometryShape> joins = [];
        foreach (var stack in stacks)
        {
            var self = ReferenceEquals(stack.Target, viewer);
            var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
            yield return new($"alteroite/stack/{stack.Generation}", "Stack at least2 R6", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: self || !storedMark);
            if (!navigate || party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(p => !ReferenceEquals(p, viewer)
                    && !stacks.Any(s => ReferenceEquals(s.Target, p)) && !spreads.Any(s => ReferenceEquals(s.Target, p)))
                    .Select(p => p.Actor.Position).ToArray();
                if (buddies.Length > 0)
                    yield return new($"alteroite/stack/{stack.Generation}/buddy", "Approach unmarked buddy R3",
                        new DungeonStackBuddyTemplate(Arena!, buddies, 3), default, Display: false, Activation: stack.Activation);
            }
            else if (!ownSpread)
                joins.Add(new CircleGeometry(position, 6));
            else
                yield return new($"alteroite/stack/{stack.Generation}/avoid", "Spread avoids stack",
                    new CircleTemplate(ownStack ? 12 : 6), position, Display: false, Activation: stack.Activation);
        }
        if (joins.Count > 0)
            yield return new("alteroite/stack/join", "Join stack R6", new SohrKhaiWallHintTemplate(Arena!, joins.ToArray()),
                default, Display: false, Activation: stacks[0].Activation);
    }
}
