using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<TreasureCreatedCircle> treasureCreatedCircles = [];
    private readonly Dictionary<ulong, (uint Oid, bool Seen)> treasureCreatedCircleActors = [];
    private readonly Dictionary<ulong, uint> treasureDestroyedCircleActors = [];
    internal IReadOnlyList<TreasureCreatedCircle> TreasureCreatedCircles => treasureCreatedCircles;
    private (double Delay, float Radius)? TreasureCreatedCircleParameters(uint oid) => (Active?.BossOid, oid) switch
    {
        (NativeTreasureCatalog.SECRET_WORM, 0x302A) => (5.7, 10),
        (NativeTreasureCatalog.LYSSA_CHRYSINE, 0x3D44) => (3.7, 6),
        (NativeTreasureCatalog.GWYDDNEU, 0x48A6) => (7.9, 8),
        (NativeTreasureCatalog.GWYDDNEU, 0x48A7) => (7.9, 16),
        (NativeTreasureCatalog.GOLDEN_MOLTER, 0x1EBCB8) => (0, 17),
        _ => null,
    };

    private void TreasureCreatedCircle(ulong id, uint oid, Vector3 position, bool observed = false)
    {
        if (!IsNativeTreasure || TreasureCreatedCircleParameters(oid) is not { } parameters) return;
        if (observed && treasureDestroyedCircleActors.GetValueOrDefault(id) == oid) return;
        if (!observed) treasureDestroyedCircleActors.Remove(id);
        if (treasureCreatedCircleActors.TryGetValue(id, out var receipt) && receipt.Oid == oid)
        {
            treasureCreatedCircleActors[id] = (oid, receipt.Seen || observed);
            return;
        }
        treasureCreatedCircleActors[id] = (oid, observed);
        treasureCreatedCircles.Add(new(id, oid, QuantizedDungeonPosition(position), _now,
            IsTreasureMolter ? default : _now.AddSeconds(parameters.Delay), parameters.Radius));
    }

    private void TreasureCreatedCircleObserveMissing()
    {
        foreach (var (id, oid) in treasureDestroyedCircleActors.ToArray())
            if (!_actors.TryGetValue(id, out var actor) || actor.Oid != oid)
                treasureDestroyedCircleActors.Remove(id);
        foreach (var id in treasureCreatedCircleActors.Where(p => p.Value.Seen
            && (!_actors.TryGetValue(p.Key, out var actor) || actor.Oid != p.Value.Oid)).Select(p => p.Key).ToArray())
            treasureCreatedCircleActors.Remove(id);
    }

    private void TreasureCreatedCircleDestroyed(ulong id)
    {
        if (!treasureCreatedCircleActors.Remove(id, out var receipt)) return;
        treasureDestroyedCircleActors[id] = receipt.Oid;
    }

    private void TreasureCreatedCircleFinish(DungeonPendingCast cast)
    {
        // These components consume any matching Finish, including cancellation or a caster's destruction.
        if (IsTreasureWorm && cast.Rule.Action == 21715 && treasureCreatedCircles.Count > 0)
            treasureCreatedCircles.RemoveAt(0);
        else if (IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.LYSSA_CHRYSINE && cast.Rule.Action == 32315)
            treasureCreatedCircles.Clear();
        else if (IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GWYDDNEU && cast.Rule.Action == 43656)
            treasureCreatedCircles.Clear();
    }

    private IEnumerable<DungeonAmbientAoe> TreasureCreatedCircleAoes()
    {
        var name = IsTreasureWorm ? "Aqua Burst" : "Ice Pillar";
        if (Active?.BossOid == NativeTreasureCatalog.GWYDDNEU) name = "Shock / Electrify";
        if (IsTreasureMolter) name = "Vasoconstrictor Pool";
        foreach (var (circle, index) in treasureCreatedCircles.Select((c, i) => (c, i)))
            yield return new($"treasure/created/{index}", name,
                new CircleTemplate(circle.Radius), circle.Position, Activation: circle.Activation);
    }
}
