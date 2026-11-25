using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<EurekaAdministratorAoe> eurekaAdministratorCombo = [];
    private readonly List<EurekaAdministratorAoe> eurekaAdministratorLines = [];
    private readonly List<EurekaAdministratorAoe> eurekaAdministratorOther = [];
    private readonly List<AlphaActorState> eurekaLaserstreamCasters = [];
    internal IReadOnlyList<EurekaAdministratorAoe> EurekaAdministratorCombo => eurekaAdministratorCombo;
    internal IReadOnlyList<EurekaAdministratorAoe> EurekaAdministratorLines => eurekaAdministratorLines;
    private bool IsEurekaAdministrator => territory == EurekaOrthosCatalog.ADMINISTRATOR;

    private void EurekaAdministratorIcon(ulong source, uint icon)
    {
        if (!IsEurekaAdministrator || Active == null || !eurekaEnabled
            || !eurekaActors.TryGetValue(source, out var actor)) return;
        GeometryTemplate? shape = actor.Actor.Oid switch
        {
            0x3D25 => RayRectangleTemplate.Symmetric(5, 40),
            0x3D26 => new DonutTemplate(8, 60),
            0x3D24 => new FanTemplate(50, 120 * (MathF.PI / 180f)),
            _ => null,
        };
        if (shape == null) return;
        var delay = icon switch { 390 => 7d, 391 => 10.5d, 392 => 14d, _ => 0d };
        eurekaAdministratorCombo.Add(new(++eurekaGeneration, shape, QuantizedDungeonPosition(actor.Actor.Position),
            actor.Actor.Oid == 0x3D26 ? 0 : actor.Actor.Rotation, _now.AddSeconds(delay), _now,
            IconSource: actor, Icon: icon));
        if (eurekaAdministratorCombo.Count == 6)
            DungeonAoeOrder.Sort(eurekaAdministratorCombo, a => a.Activation);
    }

    private void EurekaAdministratorCast(DungeonPendingCast cast)
    {
        if (!IsEurekaAdministrator || !cast.Observed) return;
        EurekaAdministratorAoe Aoe(GeometryTemplate shape) => new(++eurekaGeneration, shape,
            cast.Location, cast.Rotation, cast.FinishAt, _now, cast);
        switch (cast.Rule.Action)
        {
            case 31447:
                eurekaAdministratorCombo.Add(Aoe(new DonutTemplate(5, 60)));
                break;
            case 31448:
                eurekaAdministratorCombo.Add(Aoe(new DungeonCrossTemplate(60, 5)));
                break;
            case 31452:
                eurekaAdministratorLines.Add(Aoe(RayRectangleTemplate.Symmetric(5, 40)));
                break;
            case 32833:
                eurekaAdministratorOther.Add(Aoe(RayRectangleTemplate.Symmetric(5, 40)));
                break;
            case 32832:
                eurekaAdministratorOther.Add(Aoe(new FanTemplate(50, 120 * (MathF.PI / 180f))));
                break;
            case 31461:
                eurekaAdministratorOther.Add(Aoe(new CircleTemplate(6)));
                break;
            case 31456:
                if (eurekaActors.TryGetValue(cast.Source, out var actor)) eurekaLaserstreamCasters.Add(actor);
                break;
        }
    }

    private void EurekaAdministratorFinish(DungeonPendingCast cast)
    {
        if (!IsEurekaAdministrator || !cast.Observed) return;
        var action = cast.Rule.Action;
        if (action is 31451 or 31452 or 31453 or 31447 or 31448 && eurekaAdministratorCombo.Count != 0)
            eurekaAdministratorCombo.RemoveAt(0);
        if (action == 31452)
        {
            var index = eurekaAdministratorLines.FindIndex(a => a.Cast!.Source == cast.Source);
            if (index >= 0) eurekaAdministratorLines.RemoveAt(index);
        }
        if (action is 32833 or 32832 or 31461)
        {
            var index = eurekaAdministratorOther.FindIndex(a => a.Cast!.Source == cast.Source && a.Cast.Rule.Action == action);
            if (index >= 0) eurekaAdministratorOther.RemoveAt(index);
        }
        if (action == 31456 && eurekaActors.TryGetValue(cast.Source, out var actor))
            eurekaLaserstreamCasters.Remove(actor);
    }

    private IEnumerable<EurekaAdministratorAoe> EurekaAdministratorVisible()
    {
        if (eurekaAdministratorCombo.Count != 0)
        {
            var first = eurekaAdministratorCombo[0].Activation;
            var lastSet = eurekaAdministratorCombo[^1].Activation <= first.AddSeconds(1d);
            foreach (var aoe in eurekaAdministratorCombo)
            {
                if (aoe.Activation > first.AddSeconds(5d)) break;
                aoe.Risky = aoe.Activation < first.AddSeconds(1d);
                if (aoe.Risky && !lastSet) aoe.Danger = true;
                yield return aoe;
            }
        }
        else
            for (var i = 0; i < Math.Min(4, eurekaAdministratorLines.Count); ++i)
            {
                var aoe = eurekaAdministratorLines[i];
                aoe.Danger = eurekaAdministratorLines.Count > 2 && i < 2;
                aoe.Risky = i < 2;
                yield return aoe;
            }
        foreach (var action in new uint[] { 32833, 32832, 31461 })
            foreach (var aoe in eurekaAdministratorOther.Where(a => a.Cast!.Rule.Action == action))
                yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> EurekaAdministratorAoes()
    {
        if (!IsEurekaAdministrator) yield break;
        foreach (var aoe in EurekaAdministratorVisible())
            yield return new($"eureka/administrator/{aoe.Generation}", aoe.Cast?.Rule.Name ?? "Aetherochemical Laser",
                aoe.Shape, aoe.Origin, aoe.Rotation, Navigate: aoe.Risky, Activation: aoe.Activation,
                Color: aoe.Danger ? new Vector4(1, 1, 0, 1) : null);
    }

    private IEnumerable<DungeonDamagePrediction> EurekaAdministratorDamage()
    {
        if (!IsEurekaAdministrator || Active == null || !eurekaEnabled) yield break;
        foreach (var caster in eurekaLaserstreamCasters)
            foreach (var member in CurrentPartyMembers.Where(p => p.Slot is >= 0 and < 64
                && eurekaActors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false }))
                yield return new(member.EntityId, member.Slot, "", "", caster.Actor.Cast?.FinishAt ?? default, "Raidwide");
    }

    private IEnumerable<string> EurekaAdministratorHints(Vector3 player)
    {
        if (!IsEurekaAdministrator || Active == null || !eurekaEnabled) yield break;
        if (EurekaAdministratorVisible().Any(a => a.Risky && DungeonSourceGeometry.Contains(a.Shape, a.Origin, a.Rotation, player)))
            yield return "GTFO from aoe!";
        if (eurekaLaserstreamCasters.Count != 0) yield return "Raidwide";
    }

    private void ResetEurekaAdministrator()
    {
        eurekaAdministratorCombo.Clear();
        eurekaAdministratorLines.Clear();
        eurekaAdministratorOther.Clear();
        eurekaLaserstreamCasters.Clear();
    }
}
