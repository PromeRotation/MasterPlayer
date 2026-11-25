using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ThordanCleaves(C component, ThordanActor viewer, bool navigate)
    {
        foreach (var (source, target, shape, rotation) in Thordan.Cleaves(component))
        {
            var origin = ThordanEncounter.Quantized(source.Actor.Position);
            yield return new($"thordan/cleave/{component}/{source.Actor.Id}", "Cleave", shape, origin, rotation,
                Navigate: false, OutlineOnly: true);
            if (!navigate) continue;
            if (!ReferenceEquals(target, viewer))
                yield return new($"thordan/cleave/{component}/{source.Actor.Id}/avoid", "Avoid cleave", shape,
                    origin, rotation, Display: false);
            else
                foreach (var other in Thordan.Party.Where(p => !p.Actor.Actor.Dead && !ReferenceEquals(p.Actor, viewer)))
                    yield return new($"thordan/cleave/{component}/{source.Actor.Id}/bait/{other.Slot}",
                        "Aim cleave away from party", new FanTemplate(100, MathF.PI / 2), origin,
                        ThordanEncounter.Direction(source.Actor.Position, other.Actor.Actor.Position), Display: false);
        }
    }

    private IEnumerable<DungeonAmbientAoe> ThordanPartyAoes(C component, ThordanActor viewer, bool navigate)
    {
        var party = Thordan.Party.Where(p => !p.Actor.Actor.Dead).Select(p => p.Actor).ToArray();
        var baits = Thordan.Baits.Where(b => b.Component == component && !b.Target.Actor.Dead).ToArray();
        if (component != C.DragonsRage)
        {
            var radius = component == C.LightningStorm ? 5f : 6f;
            foreach (var spread in baits)
            {
                var position = ThordanEncounter.Quantized(spread.Target.Actor.Position);
                var self = ReferenceEquals(spread.Target, viewer);
                yield return new($"thordan/spread/{spread.Generation}", "Spread", new CircleTemplate(radius), position,
                    Navigate: false, OutlineOnly: true, Activation: spread.Activation);
                if (!navigate || party.Length < 2) continue;
                if (!self)
                    yield return new($"thordan/spread/{spread.Generation}/avoid", "Avoid spread", new CircleTemplate(radius + 1),
                        position, Display: false, Activation: spread.Activation);
                else
                    foreach (var other in party.Where(p => !baits.Any(b => ReferenceEquals(b.Target, p))))
                        yield return new($"thordan/spread/{spread.Generation}/avoid/{other.Actor.Id}", "Spread away from party",
                            new CircleTemplate(radius + 1), ThordanEncounter.Quantized(other.Actor.Position),
                            Display: false, Activation: spread.Activation);
            }
            yield break;
        }
        var marked = baits.Any(b => ReferenceEquals(b.Target, viewer));
        var stored = Thordan.Baits.Any(b => b.Component == component && ReferenceEquals(b.Target, viewer));
        List<IGeometryShape> joins = [];
        foreach (var stack in baits)
        {
            var position = ThordanEncounter.Quantized(stack.Target.Actor.Position);
            var circle = new CircleGeometry(position, 6);
            var count = party.Count(p => circle.Contains(p.Actor.Position, 0));
            var inside = circle.Contains(viewer.Actor.Position, 0);
            var canJoin = inside ? count <= 8 : count < 8;
            var self = ReferenceEquals(stack.Target, viewer);
            yield return new($"thordan/stack/{stack.Generation}", "Stack exactly 8", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: self || !stored && canJoin, Activation: stack.Activation);
            if (!navigate || party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(p => !ReferenceEquals(p, viewer) && !baits.Any(b => ReferenceEquals(b.Target, p)))
                    .Select(p => p.Actor.Position).ToArray();
                if (buddies.Length > 0)
                    yield return new($"thordan/stack/{stack.Generation}/buddy", "Stack target: approach party",
                        new DungeonStackBuddyTemplate(Arena!, buddies, 3), default, Display: false, Activation: stack.Activation);
            }
            else if (canJoin) joins.Add(circle);
            else
                yield return new($"thordan/stack/{stack.Generation}/avoid", "Avoid full or overlapping stack",
                    new CircleTemplate(marked ? 12 : 6), position, Display: false, Activation: stack.Activation);
        }
        if (joins.Count == 1)
            yield return new("thordan/stack/join", "Join one eight-player stack",
                new WorldPolygonTemplate(joins[0].GetContours()[0]), default, Display: false, Safe: true, Activation: baits[0].Activation);
        else if (joins.Count > 1)
            yield return new("thordan/stack/join", "Join one eight-player stack",
                new SohrKhaiWallHintTemplate(Arena!, joins.ToArray()), default, Display: false, Activation: baits[0].Activation);
    }

    private IEnumerable<DungeonAmbientAoe> ThordanBaits(C component, ThordanActor viewer, bool navigate)
    {
        foreach (var bait in Thordan.Baits.Where(b => b.Component == component && !b.Source.Actor.Dead))
        {
            var circle = component == C.SkywardLeap;
            GeometryTemplate shape = circle ? new CircleTemplate(20) : RayRectangleTemplate.Symmetric(12, 50);
            var origin = circle ? bait.Target.Actor.Position : bait.Source.Actor.Position;
            var rotation = ThordanEncounter.Direction(bait.Source.Actor.Position, bait.Target.Actor.Position);
            yield return new($"thordan/bait/{bait.Generation}", circle ? "Skyward Leap" : "Spiral Pierce",
                shape, origin, rotation, Navigate: false, OutlineOnly: bait.Target.Actor.Id == PlayerId,
                Activation: bait.Activation);
            if (!navigate) continue;
            if (!ReferenceEquals(bait.Target, viewer))
                yield return new($"thordan/bait/{bait.Generation}/avoid", "Avoid bait", shape, origin,
                    rotation, Display: false, Activation: bait.Activation);
            else if (!ReferenceEquals(bait.Source, bait.Target))
                foreach (var other in Thordan.Party.Where(p => !p.Actor.Actor.Dead && !ReferenceEquals(p.Actor, viewer)))
                {
                    if (circle)
                        yield return new($"thordan/bait/{bait.Generation}/party/{other.Slot}", "Bait away from party",
                            new CircleTemplate(20), other.Actor.Actor.Position, Display: false, Activation: bait.Activation);
                    else
                    {
                        var distance = MathF.Sqrt(ThordanEncounter.DistanceSquared(bait.Source.Actor.Position, other.Actor.Actor.Position));
                        var halfAngle = MathF.Asin(6 / distance);
                        // Source asin is non-finite inside six yalms. Preserve the
                        // model and diagnostics; do not invent a finite safe sector.
                        if (!float.IsFinite(halfAngle)) continue;
                        yield return new($"thordan/bait/{bait.Generation}/party/{other.Slot}", "Aim pierce away from party",
                            new FanTemplate(100, 2 * halfAngle), bait.Source.Actor.Position,
                            ThordanEncounter.Direction(bait.Source.Actor.Position, other.Actor.Actor.Position),
                            Display: false, Activation: bait.Activation);
                    }
                }
        }
    }

    private IEnumerable<DungeonAmbientAoe> ThordanTowers(ThordanActor viewer, bool navigate)
    {
        List<IGeometryShape> joins = [];
        List<ThordanAoe> forbidden = [];
        var towers = Thordan.Aoes.Where(a => a.Component == C.Conviction).ToArray();
        foreach (var tower in towers)
        {
            var count = Thordan.Party.Count(p => !p.Actor.Actor.Dead && ThordanEncounter.DistanceSquared(p.Actor.Actor.Position, tower.Origin) <= 9);
            var self = ThordanEncounter.DistanceSquared(viewer.Actor.Position, tower.Origin) <= 9;
            var full = count > 0 && !self || count > 1;
            yield return new($"thordan/tower/{tower.Generation}", "Conviction tower 1/1", tower.Shape,
                tower.Origin, Navigate: false, OutlineOnly: self || !full, Safe: !full, Activation: tower.Activation);
            if (!navigate) continue;
            if (full) forbidden.Add(tower);
            else joins.Add(new CircleGeometry(tower.Origin, 3));
        }
        if (joins.Count > 0)
            yield return new("thordan/tower/join", "Soak one tower", new SohrKhaiWallHintTemplate(Arena!, joins.ToArray()),
                default, Display: false, Activation: towers[0].Activation);
        else
            foreach (var tower in forbidden)
                yield return new($"thordan/tower/{tower.Generation}/avoid", "Avoid occupied tower", tower.Shape,
                    tower.Origin, Display: false, Activation: towers[0].Activation);
    }

    private IEnumerable<DungeonAmbientAoe> ThordanWildCharge(ThordanActor viewer, bool navigate)
    {
        if (Thordan.WildChargeSource is not { } source || Thordan.Slot < 0) yield break;
        var role = Thordan.WildRoles[Thordan.Slot];
        if (role == ThordanWildRole.Ignore) yield break;
        var targets = Thordan.Party.Where(p => !p.Actor.Actor.Dead && Thordan.WildRoles[p.Slot] == ThordanWildRole.Target).ToArray();
        List<IGeometryShape> joins = [];
        foreach (var target in targets)
        {
            var length = MathF.Sqrt(ThordanEncounter.DistanceSquared(source.Actor.Position, target.Actor.Actor.Position));
            if (length == 0) continue;
            var rotation = ThordanEncounter.Direction(source.Actor.Position, target.Actor.Actor.Position);
            var shape = RayRectangleTemplate.Symmetric(6, length);
            yield return new($"thordan/wild/{target.Slot}", "Spear of the Fury", shape, source.Actor.Position, rotation,
                Navigate: navigate && role == ThordanWildRole.Avoid, Safe: role != ThordanWildRole.Avoid);
            if (role is ThordanWildRole.Share or ThordanWildRole.ShareNotFirst)
                joins.Add(shape.Resolve(source.Actor.Position, rotation, null));
        }
        if (role == ThordanWildRole.Target)
        {
            var closest = Thordan.Party.Where(p => !p.Actor.Actor.Dead
                    && Thordan.WildRoles[p.Slot] is ThordanWildRole.Share or ThordanWildRole.ShareNotFirst)
                .OrderBy(p => ThordanEncounter.DistanceSquared(p.Actor.Actor.Position, viewer.Actor.Position)).FirstOrDefault();
            if (closest.Actor != null)
            {
                var length = MathF.Sqrt(ThordanEncounter.DistanceSquared(source.Actor.Position, closest.Actor.Actor.Position));
                if (length > 0)
                    joins.Add(RayRectangleTemplate.Symmetric(3, length).Resolve(source.Actor.Position,
                        ThordanEncounter.Direction(source.Actor.Position, closest.Actor.Actor.Position), null));
            }
        }
        if (navigate)
            for (var i = 0; i < joins.Count; ++i)
                yield return new($"thordan/wild/join/{i}", "Share charge",
                    new WorldPolygonTemplate(joins[i].GetContours()[0]), default, Display: false, Safe: true);
    }
}
