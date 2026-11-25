using System.Numerics;
using System.Runtime.CompilerServices;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class QuantumOutput
{
    private static readonly ConditionalWeakTable<object, IGeometryShape> refuges = new();

    internal static IEnumerable<DungeonAmbientAoe> Aoes(QuantumEncounter encounter)
    {
        var navigate = encounter.Active && encounter.CurrentSlot >= 0;
        foreach (var aoe in Enumerate(encounter, navigate))
            yield return navigate ? aoe : aoe with { Navigate = false, Obstacle = false };
    }

    private static IEnumerable<DungeonAmbientAoe> Enumerate(QuantumEncounter encounter, bool navigate)
    {
        if (!encounter.Loaded || !encounter.Actors.TryGetValue(encounter.PlayerId, out var player)) yield break;
        if (!navigate)
            yield return new("quantum/arena/display", "Arena outline", new PtShapeTemplate(encounter.Arena),
                QuantumEncounter.CENTER, Navigate: false, OutlineOnly: true);
        // Both views consume these exact regions; Risky colour acquisition is an
        // inverted hard constraint, independent of the coloured pool previews.
        var requirement = navigate ? encounter.ColorRequirement() : default;
        yield return new("quantum/light/display", "Light aether", new PtShapeTemplate(QuantumGeometry.Light),
            QuantumEncounter.CENTER, Navigate: false, Color: new Vector4(1, 1, .6f, .5f));
        yield return new("quantum/dark/display", "Dark aether", new PtShapeTemplate(QuantumGeometry.Dark),
            QuantumEncounter.CENTER, Navigate: false, Color: new Vector4(.8f, .2f, .8f, .5f));
        if (requirement.Light || requirement.Dark)
            yield return new("quantum/color/acquire", "Acquire aether", new PtShapeTemplate(
                requirement.Light ? QuantumGeometry.LightInverse : QuantumGeometry.DarkInverse),
                QuantumEncounter.CENTER, Display: false, Activation: requirement.Activation);
        if (requirement.AvoidOther)
            yield return new("quantum/color/avoid", "Drain aether: other colour is an obstacle", new PtShapeTemplate(
                encounter.WantsLight ? QuantumGeometry.Dark : QuantumGeometry.Light),
                QuantumEncounter.CENTER, Display: false, Obstacle: true);
        foreach (var actor in encounter.Actors.Values.Where(a => ReferenceEquals(a, encounter.Primary)
            || a.Actor.Oid is 0x48F0 or 0x48F1 or 0x48F4))
            if (!actor.Actor.Dead && actor.Actor.Targetable)
                yield return new($"quantum/enemy/{actor.Actor.Id}", actor.Actor.Name,
                    new CircleTemplate(actor.Actor.HitboxRadius), actor.Actor.Position, actor.Actor.Rotation,
                    Navigate: false, OutlineOnly: true, Color: new Vector4(1, 0, 0, 1));
        if (!encounter.Active) yield break;
        foreach (var aoe in encounter.Ordinary) yield return Ordinary(aoe);
        if (encounter.Blaze is { } blaze)
        {
            foreach (var aoe in blaze.Aoes()) yield return aoe;
            if (blaze.Refuge is { } refuge)
                yield return new("quantum/blaze/refuge", "Scourging Blaze safe outline",
                    new PtShapeTemplate(refuges.GetValue(refuge, key => QuantumGeometry.Refuge(
                        (IReadOnlyList<(Vector3 Direction, Vector3 Position)>)key))), QuantumEncounter.CENTER,
                    Navigate: false, OutlineOnly: true, Safe: true);
        }
        foreach (var aoe in Towers(encounter, player)) yield return aoe;
        if (encounter.EndAoe is { } end) yield return Ordinary(end);
        foreach (var aoe in Stacks(encounter, player)) yield return aoe;
        for (var i = 0; i < encounter.Small.Count; ++i)
            yield return Ordinary(encounter.Small[i]) with
            {
                Color = encounter.Small.Count > 6 && i < 6 ? new Vector4(1, 0, 0, 1) : null,
            };
    }

    private static DungeonAmbientAoe Ordinary(QuantumAoe aoe)
        => new($"quantum/aoe/{aoe.Generation}", $"Action {aoe.Action}", aoe.Shape,
            aoe.Position, aoe.Rotation, Activation: aoe.Activation);

    private static IEnumerable<DungeonAmbientAoe> Towers(QuantumEncounter encounter, QuantumActor player)
    {
        var towers = encounter.Towers;
        if (towers.Count == 0) yield break;
        var slot = encounter.CurrentSlot;
        var party = encounter.Party.Where(p => !p.Actor.Dead).ToArray();
        List<IGeometryShape> joins = [];
        List<(int Index, IGeometryShape Shape)> avoids = [];
        var inTower = false;
        for (var i = 0; i < towers.Count; ++i)
        {
            var tower = towers[i];
            var circle = new CircleGeometry(tower.Position, 2);
            var inside = circle.Contains(player.Actor.Position, 0);
            var count = party.Count(p => !Forbidden(tower.Forbidden, encounter.SlotOf(p))
                && circle.Contains(p.Actor.Position, 0));
            var forbidden = Forbidden(tower.Forbidden, slot);
            if (inside && count == 1) inTower = true;
            if (!forbidden && (count < 1 || inside && count == 1)) joins.Add(circle);
            else if (forbidden || count > 1 || !inside && count == 1) avoids.Add((i, circle));
            if (forbidden || !inside && count >= 1)
                yield return new($"quantum/tower/{i}/display", "Bounds of Sin tower", new CircleTemplate(2),
                    tower.Position, Navigate: false);
            else if (count < 1 || inside && count <= 1)
                yield return new($"quantum/tower/{i}/display", "Bounds of Sin tower", new CircleTemplate(2),
                    tower.Position, Navigate: false, OutlineOnly: true, Safe: true);
            else if (inside && count > 1)
                yield return new($"quantum/tower/{i}/display", "Bounds of Sin tower", new CircleTemplate(2),
                    tower.Position, Navigate: false, OutlineOnly: true);
        }
        // GenericTowers' missingSoakers remains true whenever !inTower.
        if (joins.Count > 0)
            yield return new("quantum/tower/join", "Soak a tower", new SohrKhaiWallHintTemplate(
                QuantumGeometry.RestoredFloor, joins.ToArray()), QuantumEncounter.CENTER, Display: false,
                Activation: towers[0].Activation);
        else if (!inTower)
            foreach (var (index, shape) in avoids)
                yield return new($"quantum/tower/{index}/avoid", "Avoid tower", new PtShapeTemplate(shape),
                    QuantumEncounter.CENTER, Display: false, Activation: towers[0].Activation);
    }

    private static bool Forbidden(ulong mask, int slot) => slot is >= 0 and < 64 && (mask & 1UL << slot) != 0;

    private static IEnumerable<DungeonAmbientAoe> Stacks(QuantumEncounter encounter, QuantumActor player)
    {
        var stacks = encounter.Stacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var party = encounter.Party.Where(p => !p.Actor.Dead).ToArray();
        var marked = stacks.Any(s => ReferenceEquals(s.Target, player));
        var viewerHasStoredStack = encounter.Stacks.Any(s => ReferenceEquals(s.Target, player));
        List<IGeometryShape> joins = [];
        foreach (var (id, target, activation) in stacks)
        {
            var position = DungeonEncounter.QuantizedDungeonPosition(target.Actor.Position);
            var circle = new CircleGeometry(position, 2);
            var count = party.Count(p => circle.Contains(p.Actor.Position, 0));
            var inside = circle.Contains(player.Actor.Position, 0);
            var self = ReferenceEquals(target, player);
            var canJoin = inside ? count <= 2 : count < 2;
            yield return new($"quantum/stack/{id}/display", "Light/Dark Neutralize (2 players)", new CircleTemplate(2),
                position, Navigate: false, OutlineOnly: true, Safe: self || !viewerHasStoredStack && canJoin);
            if (party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(p => !ReferenceEquals(p, player)
                    && !stacks.Any(s => ReferenceEquals(s.Target, p)))
                    .Select(p => (IGeometryShape)new CircleGeometry(p.Actor.Position, 1)).ToArray();
                if (buddies.Length != 0)
                    yield return new($"quantum/stack/{id}/buddy", "Stack target: approach a partner",
                        new SohrKhaiWallHintTemplate(QuantumGeometry.RestoredFloor, buddies), QuantumEncounter.CENTER,
                        Display: false, Activation: activation);
            }
            else if (canJoin) joins.Add(circle);
            else
                yield return new($"quantum/stack/{id}/avoid", "Avoid overlapping stacks", new CircleTemplate(marked ? 4 : 2),
                    position, Display: false, Activation: activation);
        }
        if (joins.Count != 0)
            yield return new("quantum/stack/join", "Join one two-player stack",
                new SohrKhaiWallHintTemplate(QuantumGeometry.RestoredFloor, joins.ToArray()), QuantumEncounter.CENTER,
                Display: false, Activation: stacks[0].Activation);
    }
}
