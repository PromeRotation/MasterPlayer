using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class AlteRoiteEncounter
{
    internal IEnumerable<AlteRoiteMechanic> Visible()
    {
        if (!Active || !Actors.ContainsKey(PlayerId)) yield break;
        foreach (var aoe in Ordinary.Where(a => a.Action == 9186)) yield return aoe;
        // BossModule.Enemies caches its first enumeration, then appends/removes
        // actor references. Dictionary hole reuse must not reorder later output.
        cachedTornadoes ??= Actors.Values.Where(a => a.Actor.Oid == 0x18D6).ToList();
        if (Primary?.Actor.Cast?.Action == 7896)
            foreach (var actor in cachedTornadoes.Where(a => DistanceSquared(a.Actor.Position, default) <= 4))
                yield return new(-1, actor, 7896, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                    actor.Actor.Rotation, default, new CircleTemplate(5));
        cachedOrbs ??= Actors.Values.Where(a => a.Actor.Oid == AlteRoiteCatalog.ORB).ToList();
        foreach (var actor in cachedOrbs)
            if (Orbs.TryGetValue(actor.Actor.Id, out var orb) && now >= (orb.Actual ?? orb.Predicted).AddSeconds(-5f))
                yield return new(orb.Generation, actor, 9173, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                    0, orb.Actual ?? orb.Predicted, new CircleTemplate(8));
        foreach (var aoe in Ordinary.Where(a => a.Action == 9176)) yield return aoe;
    }

    internal IEnumerable<DungeonDamagePrediction> Damage()
    {
        if (!Active || Slot < 0) yield break;
        DungeonDamagePrediction Prediction(AlteRoiteActor actor, DateTime hit, string type)
            => new(actor.Actor.Id, party.First(p => p.EntityId == actor.Actor.Id).Slot,
                actor.Actor.PartyRole, actor.Actor.Name, hit, type);
        foreach (var action in new uint[] { 9180, 9179, 9175 })
            foreach (var cast in Ordinary.Where(a => a.Action == action))
            {
                if (action != 9175)
                {
                    foreach (var actor in Party.Where(a => !a.Actor.Dead))
                        yield return Prediction(actor, Activation(cast.Source), "Raidwide");
                }
                else if (cast.Source.Cast is { } raw)
                {
                    var targetId = Sanitize(raw.TargetId);
                    if (targetId == cast.Source.Actor.Id) targetId = Sanitize(cast.Source.Actor.TargetId);
                    if (party.FirstOrDefault(p => p.EntityId == targetId && p.Slot is >= 0 and < 64) is { } target)
                    {
                        var actor = Actors.GetValueOrDefault(targetId)?.Actor;
                        yield return new(targetId, target.Slot, actor?.PartyRole ?? "", actor?.Name ?? "",
                            Activation(cast.Source), "Tankbuster");
                    }
                }
            }
        var spreads = Spreads.Where(s => !s.Target.Actor.Dead).ToArray();
        if (spreads.Length > 0)
            foreach (var member in party.Where(p => spreads.Any(s => s.Target.Actor.Id == p.EntityId)))
                if (member.Slot is >= 0 and < 64)
                {
                    var actor = Actors.GetValueOrDefault(member.EntityId)?.Actor;
                    yield return new(member.EntityId, member.Slot, actor?.PartyRole ?? "", actor?.Name ?? "",
                        spreads.Min(s => s.Activation), "Raidwide");
                }
        // Default ForbiddenPlayers is empty, so the source deliberately does
        // not emit Shared damage for these stack markers.
    }

    internal IEnumerable<(Vector3 From, Vector3 To)> Movements(uint action)
    {
        if (!Active || Slot < 0 || !Actors.TryGetValue(PlayerId, out var viewer)) yield break;
        var point = viewer.Actor.Position;
        foreach (var kb in Knockbacks.Where(k => k.Action == action))
        {
            if (Immune(kb.Activation) || kb.Shape != null
                && !DungeonSourceGeometry.Contains(kb.Shape, kb.Origin, kb.Rotation, point)) continue;
            Vector3 direction;
            if (action == 7896)
            {
                direction = point - kb.Origin;
                direction.Y = 0;
                var length = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
                if (length == 0) continue;
                direction *= 1f / length;
            }
            else direction = new(MathF.Sin(kb.Rotation), 0, MathF.Cos(kb.Rotation));
            var landing = point + 20f * direction;
            yield return (point, landing);
            point = landing;
        }
    }

    internal Vector3 IceLanding(AlteRoiteActor viewer, bool legacy, float camera)
    {
        var rotation = legacy ? camera + MathF.PI : viewer.Actor.Rotation;
        var direction = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
        var point = viewer.Actor.Position;
        var halfB = point.X * direction.X + point.Z * direction.Z;
        var discriminant = halfB * halfB - (point.X * point.X + point.Z * point.Z) + 400f;
        var intersection = discriminant < 0 ? float.MaxValue : -halfB + MathF.Sqrt(discriminant);
        if (intersection < 0) intersection = float.MaxValue;
        const float APPROX_RADIUS = .499f;
        var hitbox = viewer.Actor.HitboxRadius - APPROX_RADIUS;
        var correction = Math.Clamp(hitbox, .5f - APPROX_RADIUS, hitbox);
        return point + Math.Min(6f, intersection - correction) * direction;
    }

    internal IEnumerable<string> Hints()
    {
        if (!Active) yield break;
        foreach (var action in new uint[] { 9180, 9179 })
            if (Ordinary.Any(m => m.Action == action)) yield return "Raidwide";
        if (Ordinary.Any(m => m.Action == 9175)) yield return "Tankbuster! - Watch for Tethered Player!";
        if (!Actors.TryGetValue(PlayerId, out var viewer)) yield break;
        if (Visible().Any(m => DungeonSourceGeometry.Contains(m.Shape!, m.Origin, m.Rotation, viewer.Actor.Position)))
            yield return "GTFO from aoe!";
        foreach (var action in new uint[] { 9182, 9186, 7896 })
            if (Movements(action).Any(m => DistanceSquared(m.To, default) > 400))
                yield return "About to be knocked into danger!";
        var spreads = Spreads.Where(s => !s.Target.Actor.Dead).ToArray();
        var stacks = Stacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var partyAlive = Party.Where(a => !a.Actor.Dead).ToArray();
        if (spreads.Any(s => ReferenceEquals(s.Target, viewer)))
            yield return partyAlive.Any(p => !ReferenceEquals(p, viewer) && Inside(viewer, p))
                ? "Spread!" : "Spread!（已分散）";
        else if (stacks.FirstOrDefault(s => ReferenceEquals(s.Target, viewer)) is { } own)
        {
            var near = partyAlive.Where(p => !ReferenceEquals(p, viewer) && Inside(viewer, p)).ToArray();
            yield return near.Length == 0 || near.Any(p => Stacks.Any(s => ReferenceEquals(s.Target, p)))
                ? "Stack!" : "Stack（至少2人）";
        }
        else
        {
            var participating = stacks.Count(s => Inside(s.Target, viewer));
            var unsatisfied = stacks.Any(s => 1 + partyAlive.Count(p => !ReferenceEquals(p, s.Target) && Inside(s.Target, p)) < 2);
            if (participating > 1 || participating == 0 && unsatisfied) yield return "Stack!";
            else if (participating == 1) yield return "Stack（至少2人）";
        }
        if (spreads.Any(s => !ReferenceEquals(s.Target, viewer) && Inside(s.Target, viewer)))
            yield return "GTFO from spreads!";
    }

    private static bool Inside(AlteRoiteActor center, AlteRoiteActor point)
        => DistanceSquared(DungeonEncounter.QuantizedDungeonPosition(center.Actor.Position), point.Actor.Position) <= 36f;
    internal static float DistanceSquared(Vector3 a, Vector3 b)
        => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
}
