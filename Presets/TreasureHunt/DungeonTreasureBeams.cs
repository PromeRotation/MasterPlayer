using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<TreasureCreatedBeam> treasureBeams = [];
    private readonly Dictionary<ulong, bool> treasureBeamActors = [];
    private readonly HashSet<ulong> treasureDestroyedBeams = [];
    private int treasureBeamFinishes;
    internal IReadOnlyList<TreasureCreatedBeam> TreasureBeams => treasureBeams;
    private bool IsTreasureThunder => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.SPIRIT_OF_THUNDER;
    private uint TreasureBeamOid => IsTreasureThunder ? 0x48B6u : 0x48ADu;

    private void TreasureBeamCreated(ulong id, uint oid, Vector3 position, float rotation, bool observed = false)
    {
        if (!IsNativeTreasure || !(IsTreasureThunder || Active?.BossOid == NativeTreasureCatalog.GREAT_GIMME_CAT)
            || oid != TreasureBeamOid) return;
        if (observed && treasureDestroyedBeams.Contains(id)) return;
        if (!observed) treasureDestroyedBeams.Remove(id);
        if (treasureBeamActors.TryGetValue(id, out var seen))
        {
            treasureBeamActors[id] = seen || observed;
            return;
        }
        treasureBeamActors[id] = observed;
        var origin = IsTreasureThunder ? position : position - Vector3.UnitZ * 21;
        treasureBeams.Add(new(id, QuantizedDungeonPosition(origin), rotation, _now, _now.AddSeconds(7.7)));
    }

    private void TreasureBeamObserveMissing()
    {
        treasureDestroyedBeams.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || a.Oid != TreasureBeamOid);
        foreach (var id in treasureBeamActors.Where(p => p.Value
            && (!_actors.TryGetValue(p.Key, out var a) || a.Oid != TreasureBeamOid)).Select(p => p.Key).ToArray())
            treasureBeamActors.Remove(id);
    }

    private void TreasureBeamDestroyed(ulong id)
    {
        if (treasureBeamActors.Remove(id)) treasureDestroyedBeams.Add(id);
    }

    private void TreasureBeamFinish(DungeonPendingCast cast)
    {
        if (IsTreasureThunder && cast.Rule.Action == 43788)
        {
            treasureBeams.Clear();
            return;
        }
        if (!IsNativeTreasure || Active?.BossOid != NativeTreasureCatalog.GREAT_GIMME_CAT || cast.Rule.Action != 43690) return;
        if ((++treasureBeamFinishes & 3) == 0 && treasureBeams.Count >= 4)
            treasureBeams.RemoveRange(0, 4);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureBeamAoes()
    {
        foreach (var (beam, index) in treasureBeams.Take(IsTreasureThunder ? int.MaxValue : 4).Select((b, i) => (b, i)))
            yield return new($"treasure/beam/{index}", IsTreasureThunder ? "Power Line" : "Basking Beam",
                RayRectangleTemplate.Symmetric(IsTreasureThunder ? 10 : 5, IsTreasureThunder ? 40 : 42),
                beam.Origin, beam.Rotation, Activation: beam.Activation);
    }

    private void ResetTreasureBeams()
    {
        treasureBeams.Clear();
        treasureBeamActors.Clear();
        treasureDestroyedBeams.Clear();
        treasureBeamFinishes = 0;
    }
}
