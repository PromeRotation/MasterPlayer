using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsKefka => territory == KefkaCatalog.TERRITORY;
    internal KefkaEncounter Kefka { get; } = new();
    internal bool KefkaInputScan { get; set; }

    private void KefkaSynchronize(IEnumerable<DungeonActor> actors, DateTime now, Func<string, bool>? enabled, ulong playerId)
    {
        var values = actors.ToArray();
        _now = now;
        PlayerId = playerId;
        _actors.Clear();
        foreach (var actor in values) _actors[actor.Id] = actor;
        Kefka.Synchronize(values, CurrentPartyMembers, playerId, now, enabled?.Invoke(Definitions[0].Name) != false, !KefkaInputScan);
        KefkaPublish();
    }

    private void KefkaPublish()
    {
        Active = Kefka.Active ? Definitions[0] : null;
        PrimaryId = Kefka.Primary?.Actor.Id ?? 0;
        Arena = Kefka.Loaded ? Definitions[0].Arena(default) : null;
    }

    internal void KefkaEvent(LogSystemEvent ev, DateTime now)
    {
        if (ev is NativeActionEffectEvent effect && effect.ActionType is null or 1)
            Kefka.Effect(effect.SourceId, effect.ActionId);
        else if (ev is LogSystemDirectorUpdateEvent director && director.Command == 0x40000005)
            Kefka.End();
        KefkaPublish();
    }

    private IEnumerable<DungeonAmbientAoe> KefkaAoes()
    {
        if (!Kefka.Loaded || Kefka.Primary is not { } primary) yield break;
        if (!Kefka.Active || Kefka.Slot < 0)
            yield return new("kefka/arena/display", "Arena outline", new CircleTemplate(20),
                default, Navigate: false, OutlineOnly: true);
        if (!primary.Actor.Dead && primary.Actor.Targetable)
            yield return new("kefka/enemy", "Kefka", new LocalPolygonTemplate(
                [new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
                primary.Actor.Position, primary.Actor.Rotation, Navigate: false, Color: new(1, 0, 0, 1));
        if (!Kefka.Active || Kefka.Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var navigate = Kefka.Slot >= 0;
        foreach (var action in new uint[] { 10516, 10518 })
            foreach (var mechanic in Kefka.Mechanics.Where(m => m.Shape != null && (m.Action == action
                || action == 10516 && m.Action == 10521 || action == 10518 && m.Action == 10520)))
                yield return new($"kefka/aoe/{mechanic.Generation}", $"Action {mechanic.Action}",
                    mechanic.Shape!, mechanic.Origin, mechanic.Rotation, Navigate: navigate, Activation: mechanic.Activation);
        foreach (var aoe in KefkaPartyAoes(viewer, navigate)) yield return aoe;
        foreach (var mechanic in Kefka.Mechanics.Where(m => m.Action is 10524 or 10525))
            yield return new($"kefka/aoe/{mechanic.Generation}", $"Action {mechanic.Action}",
                mechanic.Shape!, mechanic.Origin, mechanic.Rotation, Navigate: navigate, Activation: mechanic.Activation);
        if (Kefka.Ruin is { } ruin)
            yield return new("kefka/ruin", "Revolting Ruin", ruin.Shape!, ruin.Origin, ruin.Rotation,
                Navigate: navigate, Activation: ruin.Activation);
        foreach (var action in new uint[] { 10532, 10535 })
        {
            var movements = Kefka.Mechanics.Where(m => m.Action == action).ToArray();
            if (navigate && movements.FirstOrDefault() is { } first && !Kefka.Immune(first.Activation))
                yield return new($"kefka/knockback/{action}/navigation", "Knockback landing in R19",
                    new KefkaKnockbackTemplate(first.Displacement!), default, Display: false, Activation: first.Activation);
            var point = viewer.Actor.Position;
            foreach (var mechanic in movements)
            {
                if (Kefka.Immune(mechanic.Activation)) continue;
                var landing = mechanic.Displacement!.Landing(point);
                if (point != landing)
                    foreach (var preview in TrialKnockbackDrawing($"kefka/knockback/{mechanic.Generation}",
                        $"Action {action}", point, landing)) yield return preview;
                point = landing;
            }
        }
        foreach (var action in new uint[] { 10536, 10537 })
            foreach (var mechanic in Kefka.Mechanics.Where(m => m.Action == action || action == 10537 && m.Action == 10538))
                yield return new($"kefka/aoe/{mechanic.Generation}", $"Action {mechanic.Action}",
                    mechanic.Shape!, mechanic.Origin, mechanic.Rotation, Navigate: navigate, Activation: mechanic.Activation);
        foreach (var eye in Kefka.Eyes)
        {
            var danger = KefkaEncounter.EyeDanger(eye, viewer.Actor);
            var color = danger ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1);
            var name = eye.Action == 10539 ? "Face the eye!" : "Turn away from gaze!";
            yield return new($"kefka/gaze/{eye.Generation}/eye", name, eddaEyeSymbol, eye.Origin,
                Navigate: false, Safe: !danger, Unclipped: true, Color: color);
            yield return new($"kefka/gaze/{eye.Generation}/pupil", name, new CircleTemplate(.28f), eye.Origin,
                Navigate: false, Safe: !danger, Unclipped: true, Color: new(.04f, .04f, .04f, 1));
            if (KefkaEncounter.DistanceSquared(eye.Origin, viewer.Actor.Position) <= 10000f * 10000f)
                yield return new($"kefka/gaze/{eye.Generation}/arc", name, KefkaGazeArc(eye.Action == 10539),
                    viewer.Actor.Position, viewer.Actor.Rotation, Navigate: false, Unclipped: true, Color: new(1, 0, 0, 1));
        }
    }

    private static LocalPolygonTemplate KefkaGazeArc(bool inverted)
    {
        if (!inverted) return eddaGazeArc;
        var points = Enumerable.Range(0, 73).Select(i =>
        {
            var angle = MathF.PI / 4 + 3 * MathF.PI / 2 * i / 72;
            return new Vector2(MathF.Sin(angle), MathF.Cos(angle));
        });
        return new LocalPolygonTemplate(points);
    }

    private IEnumerable<DungeonAmbientAoe> KefkaPartyAoes(KefkaActor viewer, bool navigate)
    {
        var party = Kefka.Party.Where(p => !p.Actor.Dead).ToArray();
        var spreads = Kefka.Mechanics.Where(m => m.Action == 10527 && m.Target?.Actor.Dead == false).ToArray();
        foreach (var spread in spreads)
        {
            var self = ReferenceEquals(spread.Target, viewer);
            var position = QuantizedDungeonPosition(spread.Target!.Actor.Position);
            yield return new($"kefka/spread/{spread.Generation}", "Spread R5", new CircleTemplate(5), position,
                Navigate: false, OutlineOnly: true, Activation: spread.Activation);
            if (!navigate || party.Length < 2) continue;
            if (!self)
                yield return new($"kefka/spread/{spread.Generation}/avoid", "Avoid spread R6", new CircleTemplate(6),
                    position, Display: false, Activation: spread.Activation);
            else
                foreach (var other in party.Where(p => !spreads.Any(m => ReferenceEquals(m.Target, p))))
                    yield return new($"kefka/spread/{spread.Generation}/avoid/{other.Actor.Id}", "Spread away from party",
                        new CircleTemplate(6), QuantizedDungeonPosition(other.Actor.Position), Display: false, Activation: spread.Activation);
        }
        var stacks = Kefka.Mechanics.Where(m => m.Action == 10528 && m.Target?.Actor.Dead == false).ToArray();
        var marked = stacks.Any(m => ReferenceEquals(m.Target, viewer));
        var stored = Kefka.Mechanics.Any(m => m.Action == 10528 && ReferenceEquals(m.Target, viewer));
        List<IGeometryShape> joins = [];
        foreach (var stack in stacks)
        {
            var position = QuantizedDungeonPosition(stack.Target!.Actor.Position);
            var circle = new CircleGeometry(position, 6);
            var count = party.Count(p => circle.Contains(p.Actor.Position, 0));
            var inside = circle.Contains(viewer.Actor.Position, 0);
            var canJoin = inside ? count <= 8 : count < 8;
            var self = ReferenceEquals(stack.Target, viewer);
            yield return new($"kefka/stack/{stack.Generation}", "Stack exactly 8", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: self || !stored && canJoin);
            if (!navigate || party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(p => !ReferenceEquals(p, viewer) && !stacks.Any(m => ReferenceEquals(m.Target, p)))
                    .Select(p => (IGeometryShape)new CircleGeometry(p.Actor.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"kefka/stack/{stack.Generation}/buddy", "Stack target: approach party",
                        new SohrKhaiWallHintTemplate(Arena!, buddies), default, Display: false, Activation: stack.Activation);
            }
            else if (canJoin) joins.Add(circle);
            else
                yield return new($"kefka/stack/{stack.Generation}/avoid", "Avoid full or overlapping stack",
                    new CircleTemplate(marked ? 12 : 6), position, Display: false, Activation: stack.Activation);
        }
        if (joins.Count > 0)
            yield return new("kefka/stack/join", "Join one eight-player stack", new SohrKhaiWallHintTemplate(Arena!, joins.ToArray()),
                default, Display: false, Activation: stacks[0].Activation);
    }
}
