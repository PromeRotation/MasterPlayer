using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsHeavyArtillery => Definition.Key == "A20HeavyArtilleryUnit";
    private readonly Dictionary<BozjaActor, int> heavyConstructions = [];
    internal int HeavyArenaStage { get; private set; }
    internal List<BozjaActor> HeavyEnergy { get; } = [];
    internal float HeavyRadius => HeavyArenaStage switch
    {
        0 => 44.25f,
        1 or 4 => 44.468f,
        _ => 44.5f,
    };

    private void ConstructHeavy(BozjaActor primary)
    {
        if (!IsHeavyArtillery) return;
        if (Primary is { Destroyed: true } old && old.Actor.Id == primary.Actor.Id
            && heavyConstructions.Remove(old, out var stage))
            heavyConstructions[primary] = stage;
        else
            heavyConstructions[primary] = primary.Actor.Position.Z > -262 ? 0 : primary.Actor.Position.Z > -362 ? 1 : 2;
    }

    private void ResetHeavyArena(BozjaActor primary)
    {
        if (!IsHeavyArtillery) return;
        // BMM constructs before deciding to queue; moving a pending primary
        // must not reconstruct its already frozen arena.
        HeavyArenaStage = heavyConstructions[primary];
        Center = HeavyArenaStage switch
        {
            0 => new(200, 0, -216.25f),
            1 => new(200, 0, -316.532f),
            _ => new(200, 0, -416.5f),
        };
        Arena = HeavyArenaStage switch
        {
            0 => new RectangleGeometry(Center, 0, 23, 88.5f),
            1 => new RectangleGeometry(Center, 0, 23, 88.936f),
            _ => AllianceArtilleryArenas.Stage3a(0),
        };
    }

    private void HeavyCreated(BozjaActor actor)
    {
        if (Active && IsHeavyArtillery && actor.NativeNpc && actor.Actor.Oid == 0x2E84)
            HeavyEnergy.Add(actor);
    }

    private bool HeavyComplete()
        => Actors.Values.Where(a => a.Actor.Oid == 0x2E83).All(a => a.Destroyed || a.Actor.Dead
            || HeavyArenaStage == 0 && a.Actor.Position.Z <= -262
            || HeavyArenaStage == 1 && a.Actor.Position.Z <= -362);

    private void HeavyMap(byte index)
    {
        if (!Active || !IsHeavyArtillery) return;
        if (index == 0x0D)
        {
            HeavyArenaStage = 3;
            // MiniArena changes Bounds only, retaining the original Center.
            Arena = AllianceArtilleryArenas.Stage3b(Center.Y, Center);
        }
        else if (index == 0x0E)
        {
            HeavyArenaStage = 4;
            Arena = new RectangleGeometry(Center, 0, 23, 88.936f);
        }
    }

    private IEnumerable<BozjaAoe> HeavyAoes(BozjaRule rule)
    {
        if (!IsHeavyArtillery || rule.Component != "Energy") yield break;
        foreach (var energy in HeavyEnergy)
            yield return new(new(-energy.BirthOrder, rule, energy, energy.Actor.Position, energy.Actor.Rotation, default), false, true);
    }

    private IEnumerable<BozjaZone> HeavyNavigation(BozjaRule rule)
    {
        if (!IsHeavyArtillery || rule.Component != "Energy" || HeavyEnergy.Count == 0) yield break;
        var sources = HeavyEnergy.Select(a => (a.Actor.Position, a.Actor.Rotation)).ToArray();
        yield return new("heavy/energy/future", rule.Component, Now.AddSeconds(1.1d),
            new BozjaEnergyTemplate(sources), default, 0);
        yield return new("heavy/energy/imminent", rule.Component, default,
            new CircleTemplate(1), default, 0, Union: sources.Select(a => a.Position).ToArray());
    }
}
