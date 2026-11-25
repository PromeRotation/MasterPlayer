using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class KefkaEncounter
{
    internal IEnumerable<KefkaMechanic> Eyes => Mechanics.Where(m => m.Action is 10539 or 10540);
    internal float? Facing(Vector3 player, float rotation)
    {
        if (!Active || Slot < 0) return null;
        var directions = Eyes.Where(m => m.Activation <= now.AddSeconds(.75)
            && DistanceSquared(player, m.Origin) <= 10000f * 10000f).Select(m =>
        {
            var delta = m.Action == 10539 ? player - m.Origin : m.Origin - player;
            return (Center: MathF.Atan2(delta.X, delta.Z),
                HalfWidth: m.Action == 10539 ? 3 * MathF.PI / 4 : MathF.PI / 4);
        }).ToArray();
        return DungeonFacingDirections.Solve(directions, rotation);
    }

    internal static bool EyeDanger(KefkaMechanic eye, DungeonActor player)
    {
        var offset = eye.Origin - player.Position;
        var length = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        var x = length > 0 ? offset.X * (1f / length) : 0;
        var z = length > 0 ? offset.Z * (1f / length) : 0;
        var (sin, cos) = Math.SinCos(player.Rotation);
        var hit = (float)sin * x + (float)cos * z >= .707107f;
        return hit != (eye.Action == 10539);
    }

    internal IEnumerable<string> Hints()
    {
        if (!Active) yield break;
        if (Mechanics.Any(m => m.Action == 10542)) yield return "Tankbuster";
        if (Mechanics.Any(m => m.Action == 10541)) yield return "Raidwide";
        if (Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var player = viewer.Actor;
        foreach (var eye in Eyes)
            if (DistanceSquared(player.Position, eye.Origin) <= 10000f * 10000f && EyeDanger(eye, player))
                yield return eye.Action == 10539 ? "Face the eye!" : "Turn away from gaze!";
        foreach (var mechanic in Mechanics.Where(m => m.Shape != null).Concat(Ruin is { } ruin ? [ruin] : []))
            if (DungeonSourceGeometry.Contains(mechanic.Shape!, mechanic.Origin, mechanic.Rotation, player.Position))
            { yield return "GTFO from aoe!"; break; }
        foreach (var action in new uint[] { 10532, 10535 })
        {
            var point = player.Position;
            foreach (var mechanic in Mechanics.Where(m => m.Action == action))
            {
                if (Immune(mechanic.Activation)) continue;
                point = mechanic.Displacement!.Landing(point);
                if (point.X * point.X + point.Z * point.Z > 400)
                { yield return "About to be knocked into danger!"; break; }
            }
        }
        var spreads = Mechanics.Where(m => m.Action == 10527 && m.Target?.Actor.Dead == false).ToArray();
        if (spreads.Any(m => ReferenceEquals(m.Target, viewer)))
            yield return Party.Any(p => !ReferenceEquals(p, viewer) && !p.Actor.Dead && DistanceSquared(
                DungeonEncounter.QuantizedDungeonPosition(player.Position), p.Actor.Position) <= 25)
                ? "Spread!" : "Spread!（已分散）";
        if (spreads.Any(m => !ReferenceEquals(m.Target, viewer) && DistanceSquared(player.Position,
            DungeonEncounter.QuantizedDungeonPosition(m.Target!.Actor.Position)) <= 25))
            yield return "GTFO from spreads!";
        var stacks = Mechanics.Where(m => m.Action == 10528 && m.Target?.Actor.Dead == false).ToArray();
        if (stacks.Any(m => ReferenceEquals(m.Target, viewer)))
        {
            var position = DungeonEncounter.QuantizedDungeonPosition(player.Position);
            var count = 1 + Party.Count(p => !ReferenceEquals(p, viewer) && !p.Actor.Dead
                && DistanceSquared(position, p.Actor.Position) <= 36);
            var overlap = Party.Any(p => !ReferenceEquals(p, viewer) && !p.Actor.Dead
                && DistanceSquared(position, p.Actor.Position) <= 36
                && Mechanics.Any(m => m.Action == 10528 && ReferenceEquals(m.Target, p)));
            yield return overlap || count != 8 ? $"Stack! ({count}/8)" : "Stack (8/8)";
        }
        else if (stacks.Length != 0)
        {
            var participating = stacks.Count(m => DistanceSquared(player.Position,
                DungeonEncounter.QuantizedDungeonPosition(m.Target!.Actor.Position)) <= 36);
            var unsatisfied = stacks.Any(m => 1 + Party.Count(p => !ReferenceEquals(p, m.Target) && !p.Actor.Dead
                && DistanceSquared(p.Actor.Position, DungeonEncounter.QuantizedDungeonPosition(m.Target!.Actor.Position)) <= 36) < 8);
            if (participating > 1 || participating == 0 && unsatisfied) yield return "Stack!";
            else if (participating == 1) yield return "Stack";
        }
    }

    internal IEnumerable<DungeonDamagePrediction> Damage()
    {
        if (!Active || Slot < 0) yield break;
        foreach (var mechanic in Mechanics.Where(m => m.Action is 10541 or 10542))
        {
            if (mechanic.Action == 10542)
            {
                if (mechanic.Source.Cast == null) continue;
                var targetId = mechanic.TargetId == mechanic.Source.Actor.Id
                    ? Sanitize(mechanic.Source.Actor.TargetId) : mechanic.TargetId;
                if (party.FirstOrDefault(p => p.EntityId == targetId) is { } member)
                {
                    var actor = Actors.GetValueOrDefault(targetId)?.Actor;
                    yield return new(targetId, member.Slot, actor?.PartyRole ?? "", actor?.Name ?? "",
                        Activation(mechanic.Source), "Tankbuster");
                }
            }
            else
                foreach (var target in Party.Where(p => !p.Actor.Dead))
                    if (party.FirstOrDefault(p => p.EntityId == target.Actor.Id) is { } member)
                        yield return new(target.Actor.Id, member.Slot, target.Actor.PartyRole, target.Actor.Name,
                            Activation(mechanic.Source), "Raidwide");
        }
    }

    internal static float DistanceSquared(Vector3 a, Vector3 b)
        => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
}
