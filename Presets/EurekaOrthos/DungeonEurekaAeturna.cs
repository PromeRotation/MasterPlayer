using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<EurekaShatter> eurekaShatters = [];
    private readonly List<EurekaSteelClaw> eurekaSteelClaws = [];
    private readonly List<AlphaActorState> eurekaAeturnaCrystals = [];
    private readonly EurekaStretchTether eurekaFerocityTether = new(57, 1, (double)5.7f, false);
    private bool eurekaFerocityCasted;
    private bool eurekaAeturnaCrystalsBootstrapped;
    internal IReadOnlyList<EurekaShatter> EurekaShatters => eurekaShatters;
    internal IReadOnlyList<EurekaSteelClaw> EurekaSteelClaws => eurekaSteelClaws;
    internal EurekaStretchTether EurekaFerocityTether => eurekaFerocityTether;

    private void EurekaAeturnaObserve(AlphaActorState state)
    {
        if (territory != EurekaOrthosCatalog.AETURNA || !eurekaAeturnaCrystalsBootstrapped) return;
        if (state.Actor.Oid == 0x3D1C && !eurekaAeturnaCrystals.Contains(state))
            eurekaAeturnaCrystals.Add(state);
    }

    private void EurekaAeturnaCast(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.AETURNA || !cast.Observed) return;
        if (cast.Rule.Action == 31442)
            eurekaFerocityCasted = true;
        else if (cast.Rule.Action is 31436 or 31437 && !eurekaFerocityCasted)
        {
            if (!eurekaAeturnaCrystalsBootstrapped)
            {
                // Enemies() builds its initial list lazily from the persistent world dictionary.
                eurekaAeturnaCrystals.AddRange(eurekaActors.Values.Where(a => a.Actor.Oid == 0x3D1C));
                eurekaAeturnaCrystalsBootstrapped = true;
            }
            GeometryTemplate shape = cast.Rule.Action == 31436
                ? new FanTemplate(23.95f, 150 * (MathF.PI / 180f)) : new CircleTemplate(8);
            foreach (var crystal in eurekaAeturnaCrystals)
                eurekaShatters.Add(new(++eurekaGeneration, cast, crystal,
                    QuantizedDungeonPosition(crystal.Actor.Position), crystal.Actor.Rotation,
                    _now, cast.FinishAt.AddSeconds((double).5f), shape));
        }
        else if (cast.Rule.Action == 31445 && eurekaActors.TryGetValue(cast.Source, out var source))
        {
            var targetId = cast.TargetId == cast.Source ? source.Actor.TargetId : cast.TargetId;
            if (eurekaActors.TryGetValue(targetId, out var target))
                eurekaSteelClaws.Add(new(++eurekaGeneration, cast, source, target,
                    EurekaInputParty.FirstOrDefault(p => p.EntityId == targetId)?.Slot ?? -1));
        }
    }

    private void EurekaAeturnaFinish(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.AETURNA || !cast.Observed) return;
        if (cast.Rule.Action is 31439 or 31440) eurekaShatters.Clear();
        else if (cast.Rule.Action is 31436 or 31437) eurekaFerocityCasted = false;
    }

    private void EurekaAeturnaEffect(uint action, ulong target)
    {
        if (action != 31445) return;
        var index = eurekaSteelClaws.FindIndex(c => c.Target.Actor.Id == target);
        if (index >= 0) eurekaSteelClaws.RemoveAt(index);
    }

    private IEnumerable<DungeonAmbientAoe> EurekaAeturnaAoes()
    {
        if (territory != EurekaOrthosCatalog.AETURNA) yield break;
        foreach (var shatter in eurekaShatters)
            yield return new($"eureka/shatter/{shatter.Generation}", "Shatter", shatter.Shape,
                shatter.Origin, shatter.Rotation, Activation: shatter.Activation);
        foreach (var aoe in EurekaTetherAoes()) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> EurekaAeturnaDamage()
    {
        if (territory != EurekaOrthosCatalog.AETURNA || Active == null || !eurekaEnabled) yield break;
        foreach (var claw in eurekaSteelClaws)
            if (claw.Slot is >= 0 and < 64)
                yield return new(claw.Target.Actor.Id, claw.Slot, claw.Target.Actor.PartyRole,
                    claw.Target.Actor.Name, claw.Cast.FinishAt, "Tankbuster");
    }

    private IEnumerable<string> EurekaAeturnaHints(Vector3 position)
    {
        if (territory != EurekaOrthosCatalog.AETURNA || Active == null || !eurekaEnabled) yield break;
        if (eurekaSteelClaws.Count != 0) yield return "Tankbuster";
        if (eurekaShatters.Any(s => EurekaShatterContains(s, position)))
            yield return "GTFO from aoe!";
        foreach (var hint in EurekaTetherHints(position)) yield return hint;
    }

    private static bool EurekaShatterContains(EurekaShatter shatter, Vector3 position)
    {
        var offset = new Vector2(position.X - shatter.Origin.X, position.Z - shatter.Origin.Z);
        if (shatter.Shape is CircleTemplate circle)
            return offset.LengthSquared() <= circle.Radius * circle.Radius;
        if (shatter.Shape is not FanTemplate fan || offset.LengthSquared() > fan.Radius * fan.Radius)
            return false;
        if (offset.LengthSquared() == 0) return false;
        var inverseLength = 1f / offset.Length();
        var (sin, cos) = Math.SinCos(shatter.Rotation);
        return offset.X * inverseLength * (float)sin + offset.Y * inverseLength * (float)cos
            >= (float)Math.Cos(fan.AngleRadians * .5f);
    }
}
