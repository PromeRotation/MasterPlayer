using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class QuantumEncounter
{
    internal IEnumerable<DungeonDamagePrediction> Damage()
    {
        if (!Active || CurrentSlot < 0 || towers.Count == 0) yield break;
        foreach (var member in Party.Where(p => !p.Actor.Dead))
        {
            var slot = Slot(member);
            if (towers.Any(t => !HasBit(t.Forbidden, slot)
                && new CircleGeometry(t.Position, 2).Contains(member.Actor.Position, 0)))
                yield return new(member.Actor.Id, slot, member.Actor.PartyRole, member.Actor.Name,
                    towers[0].Activation, "Raidwide");
        }
    }

    private IEnumerable<string> MechanicHints(QuantumActor player)
    {
        if (!Active) yield break;
        if (Blaze?.HasUnknownDirection == true)
            yield return "Scourging Blaze: crystal direction history unavailable; directional preview/navigation pending.";
        var position = player.Actor.Position;
        var aoes = ordinary.Concat(small);
        if (endAoe != null) aoes = aoes.Append(endAoe);
        if (aoes.Any(a => a.Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate donut
                ? new CircleGeometry(a.Position, donut.OuterRadius).Contains(position, 0)
                    && !new CircleGeometry(a.Position, donut.InnerRadius).Contains(position, 0)
                : a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0))
            || Blaze?.Aoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0)) == true)
            yield return "GTFO from aoe!";

        var partyActors = Party.Where(p => !p.Actor.Dead).ToArray();
        int Inside((System.Numerics.Vector3 Position, DateTime Activation, ulong Forbidden) tower)
            => partyActors.Count(p => !HasBit(tower.Forbidden, Slot(p))
                && new CircleGeometry(tower.Position, 2).Contains(p.Actor.Position, 0));
        var slot = Slot(player);
        if (towers.Any(t => HasBit(t.Forbidden, slot)
            && new CircleGeometry(t.Position, 2).Contains(player.Actor.Position, 0)))
            yield return "GTFO from tower!";
        else
        {
            var index = towers.FindIndex(t => !HasBit(t.Forbidden, slot)
                && new CircleGeometry(t.Position, 2).Contains(player.Actor.Position, 0));
            if (index >= 0)
            {
                if (Inside(towers[index]) > 1) yield return "Too many soakers in the tower!";
                else yield return "Soak the tower!（已在塔内）";
            }
            else if (towers.Any(t => !HasBit(t.Forbidden, slot) && Inside(t) < 1))
                yield return "Soak the tower!";
        }

        var activeStacks = stacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var self = activeStacks.FirstOrDefault(s => ReferenceEquals(s.Target, player));
        if (self.Target != null)
        {
            var center = DungeonEncounter.QuantizedDungeonPosition(player.Actor.Position);
            var neighbors = partyActors.Where(p => !ReferenceEquals(p, player)
                && new CircleGeometry(center, 2).Contains(p.Actor.Position, 0)).ToArray();
            var risky = neighbors.Length != 1 || neighbors.Any(p => stacks.Any(s => ReferenceEquals(s.Target, p)));
            yield return risky ? "Stack!" : "Stack!（人数正确）";
        }
        else
        {
            var count = activeStacks.Count(s => new CircleGeometry(
                DungeonEncounter.QuantizedDungeonPosition(s.Target.Actor.Position), 2).Contains(player.Actor.Position, 0));
            if (count > 1) yield return "Stack!";
            else if (count == 1) yield return "Stack!（已参与）";
            else if (activeStacks.Any(s => 1 + partyActors.Count(p => !ReferenceEquals(p, s.Target)
                && new CircleGeometry(DungeonEncounter.QuantizedDungeonPosition(s.Target.Actor.Position), 2)
                    .Contains(p.Actor.Position, 0)) < 2))
                yield return "Stack!";
        }
    }
}
