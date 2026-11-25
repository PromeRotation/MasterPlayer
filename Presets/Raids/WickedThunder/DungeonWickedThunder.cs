using System.Numerics;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsWickedThunder => territory == WickedThunderCatalog.TERRITORY;
    internal WickedThunderEncounter WickedThunder { get; } = new();
    internal bool WickedThunderInputScan { get; set; }

    private void WickedThunderSynchronize(IEnumerable<DungeonActor> actors, DateTime now, Func<string, bool>? enabled, ulong playerId)
    {
        var values = actors.ToArray();
        _now = now;
        PlayerId = playerId;
        _actors.Clear();
        foreach (var actor in values) _actors[actor.Id] = actor;
        WickedThunder.Synchronize(values, CurrentPartyMembers, playerId, now,
            enabled?.Invoke(Definitions[0].Name) != false, !WickedThunderInputScan);
        WickedThunderPublish();
    }

    private void WickedThunderPublish()
    {
        Active = WickedThunder.Active ? Definitions[0] : null;
        PrimaryId = WickedThunder.Primary?.Actor.Id ?? 0;
        Arena = WickedThunder.Loaded ? WickedThunder.Arena : null;
    }

    internal void WickedThunderEvent(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case NativeActionEffectEvent e when e.ActionType is null or 1:
                WickedThunder.Effect(e.SourceId, e.ActionId, e.MainTargetId);
                break;
            case LogSystemTargetIconEvent e:
                WickedThunder.Icon(e.SourceId, e.MarkerId);
                break;
            case LogSystemPlayActionTimelineEvent e:
                WickedThunder.Timeline(e.SourceId, e.TimelineId);
                break;
            case LogSystemMapEffectEvent e:
                if (e.Position <= byte.MaxValue)
                    WickedThunder.Map((byte)e.Position, ((uint)e.Param2 << 16) | e.Param1);
                break;
            case LogSystemDirectorUpdateEvent e when e.Command == 0x40000005:
                WickedThunder.End();
                break;
        }
        WickedThunderPublish();
    }

    private IEnumerable<DungeonAmbientAoe> WickedThunderAoes()
    {
        var model = WickedThunder;
        if (!model.Loaded || model.Primary is not { } primary) yield break;
        var navigate = model.Active && model.Slot >= 0;
        if (!navigate)
            yield return new("wicked/arena", "Arena outline",
                new RectangleTemplate(model.Center.X == 100 ? 40 : 10, 40), model.Center,
                Navigate: false, OutlineOnly: true);
        if (!primary.Actor.Dead && primary.Actor.Targetable)
            yield return new("wicked/enemy", "Wicked Thunder", new LocalPolygonTemplate(
                [new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
                primary.Actor.Position, primary.Actor.Rotation, Navigate: false, Color: new(1, 0, 0, 1));
        if (!model.Active || model.Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;

        DungeonAmbientAoe Aoe(WickedThunderAoe aoe, bool safe = false)
            => new($"wicked/aoe/{aoe.Generation}", $"Action {aoe.Action}", aoe.Shape!, aoe.Origin, aoe.Rotation,
                Navigate: navigate && aoe.Risky && !safe, Safe: safe, Activation: aoe.Activation,
                Color: safe ? new(0, 1, 0, 1) : aoe.Danger ? new(1, 1, 0, 1) : null);
        // Component order matches the source state machine, including safe Hyper.
        if (model.Hyper is { } hyper) yield return Aoe(hyper, true);
        foreach (var bait in model.Baits.Where(b => !b.Source.Actor.Dead))
        {
            var self = ReferenceEquals(bait.Target, viewer);
            var origin = bait.Source.Actor.Position;
            var rotation = WickedThunderEncounter.BaitRotation(bait);
            yield return new($"wicked/bait/{bait.Generation}", "Wicked Jolt", bait.Shape!, origin, rotation,
                Navigate: navigate && !self, OutlineOnly: self, Activation: bait.Activation);
            if (!navigate || !self || ReferenceEquals(bait.Source, bait.Target)) continue;
            foreach (var buddy in model.Party.Where(p => !p.Actor.Dead && !ReferenceEquals(p, viewer)))
            {
                var delta = buddy.Actor.Position - origin;
                var distance = new Vector2(delta.X, delta.Z).Length();
                var halfAngle = MathF.Asin(2.5f / distance);
                // Source asin is NaN for a buddy inside the half-width; no
                // finite cone exists in that input branch.
                if (!float.IsFinite(halfAngle)) continue;
                yield return new($"wicked/bait/{bait.Generation}/buddy/{buddy.Actor.Id}", "Bait away from raid",
                    new FanTemplate(100, 2 * halfAngle), origin, MathF.Atan2(delta.X, delta.Z),
                    Display: false, Activation: bait.Activation);
            }
        }
        foreach (var aoe in WickedThunderStacks(viewer, navigate, 37571, 5)) yield return aoe;
        foreach (var aoe in model.VisibleCannons()) yield return Aoe(aoe);
        foreach (var aoe in model.VisibleSides()) yield return Aoe(aoe);
        foreach (var aoe in WickedThunderStacks(viewer, navigate, 37569, 6)) yield return aoe;
        if (model.Stampede is { } stampede) yield return Aoe(stampede);
        foreach (var action in new uint[] { 37560, 37574 })
            foreach (var aoe in model.Ordinary.Where(m => m.Action == action)) yield return Aoe(aoe);

        var spreads = model.Ordinary.Where(m => m.Action == 37573 && m.Target?.Actor.Dead == false).ToArray();
        var party = model.Party.Where(p => !p.Actor.Dead).ToArray();
        foreach (var spread in spreads)
        {
            var position = QuantizedDungeonPosition(spread.Target!.Actor.Position);
            yield return new($"wicked/spread/{spread.Generation}", "Spread R6", new CircleTemplate(6),
                position, Navigate: false, OutlineOnly: true);
            if (!navigate || party.Length < 2) continue;
            if (!ReferenceEquals(spread.Target, viewer))
                yield return new($"wicked/spread/{spread.Generation}/avoid", "Spread R7 navigation",
                    new CircleTemplate(7), position, Display: false, Activation: spread.Activation);
            else
                foreach (var buddy in party.Where(p => !spreads.Any(s => ReferenceEquals(s.Target, p))))
                    yield return new($"wicked/spread/{spread.Generation}/buddy/{buddy.Actor.Id}", "Spread away",
                        new CircleTemplate(7), QuantizedDungeonPosition(buddy.Actor.Position),
                        Display: false, Activation: spread.Activation);
        }
        foreach (var aoe in model.VisibleHunts()) yield return Aoe(aoe);
    }

    private IEnumerable<DungeonAmbientAoe> WickedThunderStacks(WickedThunderActor viewer, bool navigate, uint action, float radius)
    {
        var party = WickedThunder.Party.Where(p => !p.Actor.Dead).ToArray();
        // The two StackWithIcon instances are independent; the other component's
        // markers do not change its eligibility or capacity.
        var stacks = WickedThunder.Stacks.Where(s => s.Action == action && s.Target?.Actor.Dead == false).ToArray();
        var marked = stacks.Any(s => ReferenceEquals(s.Target, viewer));
        var stored = WickedThunder.Stacks.Any(s => s.Action == action && ReferenceEquals(s.Target, viewer));
        List<IGeometryShape> joins = [];
        foreach (var stack in stacks)
        {
            var position = QuantizedDungeonPosition(stack.Target!.Actor.Position);
            var circle = new CircleGeometry(position, radius);
            var inside = circle.Contains(viewer.Actor.Position, 0);
            var count = party.Count(p => circle.Contains(p.Actor.Position, 0));
            var canJoin = inside ? count <= 8 : count < 8;
            var self = ReferenceEquals(stack.Target, viewer);
            yield return new($"wicked/stack/{stack.Generation}", $"Stack exactly8 R{radius}",
                new CircleTemplate(radius), position, Navigate: false, OutlineOnly: true, Safe: self || !stored && canJoin);
            if (!navigate || party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(p => !ReferenceEquals(p, viewer) && !stacks.Any(s => ReferenceEquals(s.Target, p)))
                    .Select(p => p.Actor.Position).ToArray();
                if (buddies.Length > 0)
                    yield return new($"wicked/stack/{stack.Generation}/buddy", "Stack target: approach party",
                        new DungeonStackBuddyTemplate(Arena!, buddies, radius * .5f), default,
                        Display: false, Activation: stack.Activation);
            }
            else if (canJoin) joins.Add(circle);
            else
                yield return new($"wicked/stack/{stack.Generation}/avoid", "Avoid full or overlapping stack",
                    new CircleTemplate(marked ? 2 * radius : radius), position, Display: false, Activation: stack.Activation);
        }
        if (joins.Count > 0)
            yield return new($"wicked/stack/join/{action}", "Join one exact8 stack",
                new SohrKhaiWallHintTemplate(Arena!, joins.ToArray()), default, Display: false, Activation: stacks[0].Activation);
    }
}
