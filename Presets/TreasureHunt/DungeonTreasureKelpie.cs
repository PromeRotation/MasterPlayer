using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureKelpie => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.ALTAR_KELPIE;
    private readonly List<TreasureHydrosphere> treasureHydrospheres = [];
    private readonly Dictionary<ulong, bool> treasureHydrosphereActors = [];
    internal IReadOnlyList<TreasureHydrosphere> TreasureHydrospheres => treasureHydrospheres;

    private void TreasureHydrosphereCreated(ulong id, uint oid, Vector3 position, bool observed = false)
    {
        if (!IsTreasureKelpie || oid != 0x255B) return;
        if (treasureHydrosphereActors.TryGetValue(id, out var seen))
        {
            treasureHydrosphereActors[id] = seen || observed;
            return;
        }
        treasureHydrosphereActors[id] = observed;
        treasureHydrospheres.Add(new(id, QuantizedDungeonPosition(position), _now, _now.AddSeconds(8.6)));
    }

    private void TreasureHydrosphereObserveMissing()
    {
        foreach (var id in treasureHydrosphereActors.Where(p => p.Value
            && (!_actors.TryGetValue(p.Key, out var actor) || actor.Oid != 0x255B)).Select(p => p.Key).ToArray())
            treasureHydrosphereActors.Remove(id);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureKelpieAoes()
    {
        foreach (var (puddle, index) in treasureHydrospheres.Select((p, i) => (p, i)))
            yield return new($"treasure/hydrosphere/{index}", "Bloody Puddle", new CircleTemplate(11.2f), puddle.Position,
                Activation: puddle.Activation);
        if (treasureWallKnockbacks.FirstOrDefault(k => k.Cast.Rule.Action == 13440) is not { } kb || Arena == null)
            yield break;
        foreach (var (puddle, index) in treasureHydrospheres.Select((p, i) => (p, i)))
        {
            var delta = puddle.Position - Arena.Position;
            yield return new($"treasure/rising-seas/{index}", "Rising Seas / 避开水球方向",
                new FanTemplate(20, 75 * MathF.PI / 180), Arena.Position, MathF.Atan2(delta.X, delta.Z),
                Display: false, Activation: kb.Cast.FinishAt);
        }
    }

    private bool TreasureKelpieLandingUnsafe(Vector3 position) => treasureHydrospheres.Any(p =>
        new CircleTemplate(11.2f).Resolve(p.Position, 0, null).Contains(position, 0));
}
