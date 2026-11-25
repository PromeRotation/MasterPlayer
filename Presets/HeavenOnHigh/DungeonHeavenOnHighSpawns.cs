using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonActor> heavenOnHighSpawnActors = [];
    private readonly List<HeavenOnHighSpawnAoe> heavenOnHighSpawnAoes = [];
    private readonly HashSet<(ulong Source, uint Sequence)> heavenOnHighSpawnEffects = [];
    private ulong heavenOnHighSpawnGeneration;
    internal IReadOnlyList<HeavenOnHighSpawnAoe> HeavenOnHighSpawnAoes => heavenOnHighSpawnAoes;

    private void HeavenOnHighObserveSpawn(DungeonActor actor)
    {
        if (Active == null || !IsHeavenOnHigh || actor.Player || actor.Buddy || actor.FriendlyNpc) return;
        var delay = (territory, actor.Oid) switch
        {
            (HeavenOnHighCatalog.SIXTH, 0x23E5) => 7.9d,
            (HeavenOnHighCatalog.EIGHTH, 0x23ED) => 8.5d,
            (HeavenOnHighCatalog.NINTH, 0x2413) => 3.2d,
            _ => 0,
        };
        if (delay == 0) return;
        if (!heavenOnHighSpawnActors.ContainsKey(actor.Id))
            heavenOnHighSpawnAoes.Add(new(++heavenOnHighSpawnGeneration, actor.Id, actor.Oid,
                QuantizedDungeonPosition(actor.Position), _now, _now.AddSeconds(delay)));
        heavenOnHighSpawnActors[actor.Id] = actor;
    }

    private void HeavenOnHighSpawnFinish(DungeonPendingCast cast)
    {
        if (!IsHeavenOnHigh) return;
        if (territory == HeavenOnHighCatalog.SIXTH && cast.Rule.Action == 11930)
        {
            var index = heavenOnHighSpawnAoes.FindIndex(a => a.ActorId == cast.Source);
            if (index >= 0) heavenOnHighSpawnAoes.RemoveAt(index);
        }
        if (territory == HeavenOnHighCatalog.NINTH && cast.Rule.Action == 12210
            && (_actors.GetValueOrDefault(cast.Source) ?? heavenOnHighSpawnActors.GetValueOrDefault(cast.Source)) is { } actor)
        {
            var index = HeavenOnHighSphereAt(actor.Position);
            if (index < 0) return;
            var aoe = heavenOnHighSpawnAoes[index];
            if (aoe.Hits == 2) heavenOnHighSpawnAoes.RemoveAt(index);
            else heavenOnHighSpawnAoes[index] = aoe with { Hits = aoe.Hits + 1 };
        }
    }

    private int HeavenOnHighSphereAt(Vector3 position) => heavenOnHighSpawnAoes.FindIndex(a =>
        MathF.Abs(a.Origin.X - position.X) <= .1f && MathF.Abs(a.Origin.Z - position.Z) <= .1f);

    private void HeavenOnHighSpawnRemoved(ulong source)
    {
        if (!heavenOnHighSpawnActors.Remove(source, out var actor)) return;
        if (territory == HeavenOnHighCatalog.NINTH)
        {
            var index = HeavenOnHighSphereAt(actor.Position);
            if (index >= 0) heavenOnHighSpawnAoes.RemoveAt(index);
        }
    }

    internal bool UsesHeavenOnHighNativeEffect(uint action) => territory == HeavenOnHighCatalog.EIGHTH && action == 11992
        || IsKenko && action is 12207 or 12204;

    private void HeavenOnHighSpawnEffect(ulong source, uint sequence)
    {
        if (sequence != 0 && !heavenOnHighSpawnEffects.Add((source, sequence))) return;
        var index = heavenOnHighSpawnAoes.FindIndex(a => a.ActorId == source);
        if (index >= 0) heavenOnHighSpawnAoes.RemoveAt(index);
    }

    private IEnumerable<DungeonAmbientAoe> HeavenOnHighSpawnAreas()
    {
        if (!IsHeavenOnHigh || Active == null || !heavenOnHighEnabled) yield break;
        foreach (var aoe in heavenOnHighSpawnAoes)
        {
            GeometryTemplate shape = aoe.Oid switch
            {
                0x23E5 => new DonutTemplate(6, 12),
                0x23ED => new CircleTemplate(8),
                _ => new CircleTemplate(9),
            };
            yield return new($"hoh/spawn/{aoe.Generation}", aoe.Oid switch
            {
                0x23E5 => "Infinite Anguish",
                0x23ED => "Sphere Shatter",
                _ => "Subduction",
            }, shape, aoe.Origin, Activation: aoe.Activation);
        }
    }

    private void ResetHeavenOnHighSpawns()
    {
        heavenOnHighSpawnActors.Clear();
        heavenOnHighSpawnAoes.Clear();
        heavenOnHighSpawnEffects.Clear();
        heavenOnHighSpawnGeneration = 0;
    }
}
