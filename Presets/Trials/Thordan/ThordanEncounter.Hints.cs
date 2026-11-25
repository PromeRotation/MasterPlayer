using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class ThordanEncounter
{
    internal static float DistanceSquared(Vector3 a, Vector3 b)
        => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
    internal static float Direction(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);
    internal static bool EyeDanger(Vector3 origin, DungeonActor player)
    {
        var offset = origin - player.Position;
        var length = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        var inverse = length > 0 ? 1f / length : 0;
        var (sin, cos) = Math.SinCos(player.Rotation);
        return offset.X * inverse * (float)sin + offset.Z * inverse * (float)cos >= .707107f;
    }

    internal float? Facing(Vector3 player, float rotation)
    {
        if (!Active || Slot < 0) return null;
        var directions = Aoes.Where(a => a.Component == C.DragonsGaze
            && a.Activation <= now.AddSeconds(.75) && DistanceSquared(player, a.Origin) <= 10000f * 10000f)
            .Select(a => (Center: Direction(player, a.Origin), HalfWidth: MathF.PI / 4)).ToArray();
        return DungeonFacingDirections.Solve(directions, rotation);
    }

    internal IEnumerable<(ThordanActor Source, ThordanActor Target, FanTemplate Shape, float Rotation)> Cleaves(C component)
    {
        var oid = component == C.AscalonsMight ? Primary?.Actor.Oid
            : component == C.HeavenlySlashAdelphel ? 0x1455u : 0x1456u;
        foreach (var source in Actors.Values.Where(a => a.Actor.Oid == oid && !a.Actor.Dead && a.Actor.Targetable))
            if (Actors.GetValueOrDefault(Sanitize(source.Actor.TargetId)) is { } target)
                yield return (source, target, new(component == C.AscalonsMight ? 11.8f : 10.2f, MathF.PI / 2),
                    Direction(source.Actor.Position, target.Actor.Position));
    }

    internal IEnumerable<string> Hints()
    {
        if (!Active) yield break;
        if (Has(C.SwordShieldOfTheHeavens))
        {
            if (Swords.Count(a => a.Actor.Destroyed || !a.Actor.Actor.Targetable) == 2
                && DistanceSquared(Swords[0].Actor.Actor.Position, Swords[1].Actor.Actor.Position) <= 100)
                yield return "Separate adds!";
            var focus = Swords.FirstOrDefault(a => a.Buff == 944).Actor;
            if (focus != null) yield return $"Focus on {focus.Actor.Name}!";
        }
        if (Has(C.SpiralPierce) && Baits.Any(b => b.Component == C.SpiralPierce)) yield return "Tankbuster cleave";
        if (Aoes.Any(a => a.Component == C.HoliestOfHoly)) yield return "Raidwide";
        if (Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var player = viewer.Actor;
        if (Has(C.SwordShieldOfTheHeavens))
        {
            if (Swords.Any(a => a.Buff == 944 && a.Actor.Cast is { Action: 5270, IsSpell: true } cast
                && Sanitize(cast.TargetId) == PlayerId)) yield return "Mitigate NOW!";
            if (Swords.Any(a => a.Buff == 945 && a.Actor.Actor.TargetId != PlayerId
                && a.Actor.Actor.Id == player.TargetId)) yield return "Swap target!";
        }
        foreach (var component in ActiveComponents)
        {
            if (component is C.AscalonsMight or C.HeavenlySlashAdelphel or C.HeavenlySlashJanlenoux)
                if (Cleaves(component).Any(c => !ReferenceEquals(c.Target, viewer)
                    && DungeonSourceGeometry.Contains(c.Shape, c.Source.Actor.Position, c.Rotation, Quantized(player.Position))))
                    yield return "GTFO from cleave!";
            if (component == C.DragonsGaze && Aoes.Any(a => a.Component == component
                && DistanceSquared(a.Origin, player.Position) <= 10000f * 10000f && EyeDanger(a.Origin, player)))
                yield return "Turn away from gaze!";
            if (Aoes.Any(a => a.Component == component && a.Component is not (C.DragonsGaze or C.Conviction or C.HoliestOfHoly)
                && a.Risky && DungeonSourceGeometry.Contains(a.Shape, a.Origin, a.Rotation, player.Position)))
                yield return "GTFO from aoe!";
            if (component == C.HeavyImpact && Heavy.Any(s => DungeonSourceGeometry.Contains(
                DungeonEncounter.ThordanHeavyShape(s.Order), s.Origin, s.Rotation, player.Position)))
                yield return "GTFO from aoe!";
            if (component == C.ArenaChange && DungeonSourceGeometry.Contains(new DonutTemplate(21, 24), default, 0, player.Position))
                yield return "GTFO from aoe!";
            if (component == C.HiemalStormVoidzone && Actors.Values.Any(a => a.Actor.Oid == 0x1E9729
                && a.Actor.EventState != 7 && DistanceSquared(a.Actor.Position, player.Position) <= 36))
                yield return "GTFO from voidzone!";
            if (component == C.BurningChains && Slot >= 0 && ChainPartners[Slot] != null)
                yield return "Break the chains!";
            if (component is C.LightningStorm or C.HiemalStormSpread)
            {
                var radius = component == C.LightningStorm ? 5f : 6f;
                var spreads = Baits.Where(b => b.Component == component && !b.Target.Actor.Dead).ToArray();
                if (spreads.Any(b => ReferenceEquals(b.Target, viewer)))
                    yield return Party.Any(p => !p.Actor.Actor.Dead && !ReferenceEquals(p.Actor, viewer)
                        && DistanceSquared(Quantized(player.Position), p.Actor.Actor.Position) <= radius * radius)
                        ? "Spread!" : "Spread!（已分散）";
                if (spreads.Any(b => !ReferenceEquals(b.Target, viewer)
                    && DistanceSquared(player.Position, Quantized(b.Target.Actor.Position)) <= radius * radius))
                    yield return "GTFO from spreads!";
            }
            if (component == C.DragonsRage)
            {
                var stacks = Baits.Where(b => b.Component == component && !b.Target.Actor.Dead).ToArray();
                var self = stacks.Any(b => ReferenceEquals(b.Target, viewer));
                if (self)
                {
                    var count = 1 + Party.Count(p => !p.Actor.Actor.Dead && !ReferenceEquals(p.Actor, viewer)
                        && DistanceSquared(Quantized(player.Position), p.Actor.Actor.Position) <= 36);
                    var overlap = Party.Any(p => !p.Actor.Actor.Dead && !ReferenceEquals(p.Actor, viewer)
                        && DistanceSquared(Quantized(player.Position), p.Actor.Actor.Position) <= 36
                        && stacks.Any(b => ReferenceEquals(b.Target, p.Actor)));
                    yield return count == 8 && !overlap ? "Stack (8/8)" : $"Stack! ({count}/8)";
                }
                else if (stacks.Length != 0)
                {
                    var participating = stacks.Count(b => DistanceSquared(player.Position, Quantized(b.Target.Actor.Position)) <= 36);
                    var unsatisfied = stacks.Any(b => 1 + Party.Count(p => !p.Actor.Actor.Dead && !ReferenceEquals(p.Actor, b.Target)
                        && DistanceSquared(p.Actor.Actor.Position, Quantized(b.Target.Actor.Position)) <= 36) < 8);
                    if (participating > 1 || participating == 0 && unsatisfied) yield return "Stack!";
                    else if (participating == 1) yield return "Stack";
                }
            }
            if (component is C.SkywardLeap or C.SpiralPierce)
                foreach (var bait in Baits.Where(b => b.Component == component && !b.Source.Actor.Dead))
                {
                    var shape = component == C.SkywardLeap ? (GeometryTemplate)new CircleTemplate(20) : RayRectangleTemplate.Symmetric(12, 50);
                    var origin = component == C.SkywardLeap ? bait.Target.Actor.Position : bait.Source.Actor.Position;
                    var rotation = Direction(bait.Source.Actor.Position, bait.Target.Actor.Position);
                    if (bait.Target.Actor.Id == player.Id)
                    {
                        if (Party.Any(p => !p.Actor.Actor.Dead && p.Actor.Actor.Id != bait.Target.Actor.Id
                            && DungeonSourceGeometry.Contains(shape, origin, rotation, p.Actor.Actor.Position)))
                            yield return "Bait away!";
                        if (component == C.SpiralPierce && Party.Any(p => !p.Actor.Actor.Dead
                            && !ReferenceEquals(p.Actor, viewer) && DistanceSquared(bait.Source.Actor.Position, p.Actor.Actor.Position) < 36))
                            yield return "Spiral Pierce: source asin undefined within 6y; directional navigation unavailable";
                    }
                    else if (DungeonSourceGeometry.Contains(shape, origin, rotation, player.Position))
                        yield return "GTFO from baited aoe!";
                }
            if (component == C.Conviction)
            {
                var towers = Aoes.Where(a => a.Component == component).ToArray();
                var inside = towers.Where(a => DistanceSquared(a.Origin, player.Position) <= 9).ToArray();
                if (inside.Length > 0)
                {
                    if (inside.Any(a => Party.Count(p => !p.Actor.Actor.Dead && DistanceSquared(a.Origin, p.Actor.Actor.Position) <= 9) > 1))
                        yield return "Too many soakers!";
                }
                else if (towers.Any(a => !Party.Any(p => !p.Actor.Actor.Dead && DistanceSquared(a.Origin, p.Actor.Actor.Position) <= 9)))
                    yield return "Soak the tower!";
            }
        }
        var arena = ReducedArena ? ThordanCatalog.REDUCED_ARENA : ThordanCatalog.STARTING_ARENA;
        if (Faith is { } faith && !arena.Contains(faith.Landing(player.Position), 0))
            yield return "About to be knocked into danger!";
        var point = player.Position;
        foreach (var movement in Light)
        {
            point = movement.Landing(point);
            if (!arena.Contains(point, 0)) { yield return "About to be knocked into danger!"; break; }
        }
        foreach (var hint in WildHints(viewer)) yield return hint;
    }

    private IEnumerable<string> WildHints(ThordanActor viewer)
    {
        if (WildChargeSource is not { } source || Slot < 0) yield break;
        var role = WildRoles[Slot];
        if (role is ThordanWildRole.Ignore or ThordanWildRole.Target) yield break;
        var charges = Party.Where(p => !p.Actor.Actor.Dead && WildRoles[p.Slot] == ThordanWildRole.Target)
            .Select(p => p.Actor).ToArray();
        bool Inside(ThordanActor target, ThordanActor player)
        {
            var length = MathF.Sqrt(DistanceSquared(source.Actor.Position, target.Actor.Position));
            return length > 0 && DungeonSourceGeometry.Contains(RayRectangleTemplate.Symmetric(6, length),
                source.Actor.Position, Direction(source.Actor.Position, target.Actor.Position), player.Actor.Position);
        }
        var inCharges = charges.Where(t => Inside(t, viewer)).ToArray();
        if (role == ThordanWildRole.Avoid)
        {
            if (inCharges.Length > 0) yield return "GTFO from charge!";
            yield break;
        }
        if (inCharges.Length == 0) yield return "Stay inside charge!";
        else if (inCharges.Length > 1) yield return "Stay in single charge!";
        else
        {
            var distance = DistanceSquared(viewer.Actor.Position, source.Actor.Position);
            var closer = Party.Any(p => !p.Actor.Actor.Dead && Inside(inCharges[0], p.Actor)
                && DistanceSquared(p.Actor.Actor.Position, source.Actor.Position) < distance
                && (role == ThordanWildRole.Share ? WildRoles[p.Slot] == ThordanWildRole.ShareNotFirst
                    : WildRoles[p.Slot] is ThordanWildRole.Share or ThordanWildRole.Target));
            if (role == ThordanWildRole.Share && closer) yield return "Move closer to charge source!";
            if (role == ThordanWildRole.ShareNotFirst && !closer) yield return "Hide behind tank!";
        }
    }

    internal IEnumerable<DungeonDamagePrediction> Damage()
    {
        if (!Active || Slot < 0) yield break;
        foreach (var raidwide in Aoes.Where(a => a.Component == C.HoliestOfHoly && a.Source.Cast != null))
            foreach (var target in Party.Where(p => !p.Actor.Actor.Dead))
                yield return new(target.Actor.Actor.Id, target.Slot, target.Actor.Actor.PartyRole, target.Actor.Actor.Name,
                    Activation(raidwide.Source), "Raidwide");
        var pierces = Baits.Where(b => b.Component == C.SpiralPierce).ToArray();
        if (pierces.Length > 0)
            foreach (var member in party.Where(p => p.Slot is >= 0 and < 64
                && pierces.Any(b => !b.Source.Actor.Dead && b.Target.Actor.Id == p.EntityId)))
            {
                var target = Actors.GetValueOrDefault(member.EntityId)
                    ?? pierces.First(b => b.Target.Actor.Id == member.EntityId).Target;
                yield return new(member.EntityId, member.Slot, target.Actor.PartyRole, target.Actor.Name, pierces[0].Activation, "Tankbuster");
            }
        var hiemal = Baits.Where(b => b.Component == C.HiemalStormSpread && !b.Target.Actor.Dead).ToArray();
        if (hiemal.Length > 0)
            foreach (var member in party.Where(p => p.Slot is >= 0 and < 64 && hiemal.Any(b => b.Target.Actor.Id == p.EntityId)))
            {
                var target = Actors.GetValueOrDefault(member.EntityId) ?? hiemal.First(b => b.Target.Actor.Id == member.EntityId).Target;
                yield return new(member.EntityId, member.Slot, target.Actor.PartyRole, target.Actor.Name, hiemal.Min(b => b.Activation), "Raidwide");
            }
        var towers = Aoes.Where(a => a.Component == C.Conviction).ToArray();
        if (towers.Length > 0)
            foreach (var target in Party.Where(p => !p.Actor.Actor.Dead
                && towers.Any(t => DistanceSquared(p.Actor.Actor.Position, t.Origin) <= 9)))
                yield return new(target.Actor.Actor.Id, target.Slot, target.Actor.Actor.PartyRole, target.Actor.Actor.Name,
                    towers[0].Activation, "Raidwide");
        if (WildChargeSource is { } source)
            foreach (var target in Party.Where(p => !p.Actor.Actor.Dead && WildRoles[p.Slot] == ThordanWildRole.Target))
            {
                var length = MathF.Sqrt(DistanceSquared(source.Actor.Position, target.Actor.Actor.Position));
                if (length == 0) continue;
                var shape = RayRectangleTemplate.Symmetric(6, length);
                var rotation = Direction(source.Actor.Position, target.Actor.Actor.Position);
                foreach (var member in Party.Where(p => !p.Actor.Actor.Dead
                    && DungeonSourceGeometry.Contains(shape, source.Actor.Position, rotation, p.Actor.Actor.Position)))
                    yield return new(member.Actor.Actor.Id, member.Slot, member.Actor.Actor.PartyRole,
                        member.Actor.Actor.Name, default, "Raidwide");
            }
    }
}
