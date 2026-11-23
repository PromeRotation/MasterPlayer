using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonAmbientAoe> cosmosPools = [];
    private readonly Dictionary<ulong, DateTime> cosmosBlackFlames = [];
    private readonly List<ulong> cosmosCharges = [];
    private readonly HashSet<(ulong Target, uint Icon, DateTime Time)> cosmosIcons = [];
    private readonly Dictionary<ulong, (ulong Target, uint Id, bool Present, DateTime Time)> cosmosTethers = [];
    private readonly Dictionary<ulong, DungeonPendingCast> cosmosBusters = [];
    private Vector3[] cosmosTiles = [];
    private int cosmosSeedWaves;
    private readonly HashSet<(ulong Source, DateTime Start)> cosmosTileCasts = [];
    private bool CosmosTilesActive => Active?.BossOid == 0x2C04 && (cosmosSeedWaves == 1 ? cosmosTiles.Length > 0 : cosmosTiles.Length > 8);
    private bool CosmosTransporting => Active?.BossOid == 0x2C04 && _actors.TryGetValue(PlayerId, out var p) && HasStatus(p, 404);
    internal bool CosmosReadyToDrop => CosmosTilesActive && CosmosTransporting && _actors.TryGetValue(PlayerId, out var p)
        && Arena?.Contains(p.Position) == true && !CosmosTileShapes().Any(t => t.Contains(p.Position));
    private IGeometryShape[] CosmosTileShapes() => cosmosTiles.Select(p => (IGeometryShape)new RectangleGeometry(p, 0, 10, 10)).ToArray();
    internal DungeonActor? CosmosSeedTarget => CosmosTilesActive && !CosmosTransporting && _actors.TryGetValue(PlayerId, out var p)
        ? _actors.Values.Where(a => CosmosCatalog.IsSeed(a.Oid) && !a.Dead && a.Targetable && a.EventState != 7
            && CosmosTileShapes().Any(t => t.Contains(a.Position))).OrderBy(a => Vector3.DistanceSquared(p.Position, a.Position)).FirstOrDefault() : null;

    private void CosmosMapEffect(uint index, uint state)
    {
        if (territory != CosmosCatalog.TERRITORY || index != 0x0B) return;
        Vector2[] defaults = [new(-5, -75), new(5, -75), new(-15, -65), new(15, -65), new(-15, -55), new(5, -55), new(-5, -45), new(15, -45)];
        var degrees = state switch { 0x00020001 => 0, 0x00200010 => 180, 0x01000080 => -90, 0x08000400 => 90, _ => float.NaN };
        if (!float.IsFinite(degrees)) { cosmosTiles = []; return; }
        var y = Arena?.Position.Y ?? _actors.GetValueOrDefault(PrimaryId)?.Position.Y ?? 0;
        var (sin, cos) = MathF.SinCos(degrees * MathF.PI / 180);
        cosmosTiles = defaults.Select(p => new Vector3(cos * p.X - sin * (p.Y + 60), y, sin * p.X + cos * (p.Y + 60) - 60)).ToArray();
    }

    private void CosmosCast(DungeonPendingCast cast)
    {
        if (territory != CosmosCatalog.TERRITORY) return;
        if (cast.Rule.Action == 18276)
            cosmosBusters[cast.Source] = cast with { TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId };
        if (!cosmosTileCasts.Add((cast.Source, cast.StartedAt))) return;
        if (cast.Rule.Action == 18205) ++cosmosSeedWaves;
        if (cast.Rule.Action != 18209 || cosmosTiles.Length < 8 || !_actors.TryGetValue(cast.Source, out var caster)) return;
        var direction = MathF.Round(cast.Rotation / (MathF.PI / 2)) * MathF.PI / 2;
        var offset = new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)) * 10;
        var tiles = new List<Vector3>();
        foreach (var tile in cosmosTiles.Take(8))
        {
            var moved = tile - offset;
            tiles.Add(moved);
            if (Vector3.DistanceSquared(caster.Position, moved) > 625) tiles.Add(moved + offset);
        }
        cosmosTiles = tiles.ToArray();
    }

    private void CosmosFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != CosmosCatalog.TERRITORY) return;
        if (!completed) cosmosBusters.Remove(cast.Source);
    }

    private void CosmosIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid != 0x2C13 || !cosmosIcons.Add((target, icon, time))) return;
        if (icon == 25) cosmosBlackFlames[target] = time.AddSeconds(4);
        if (icon is >= 50 and <= 53) cosmosCharges.Add(target);
    }

    private void CosmosTether(ulong source, ulong target, uint id, bool present, DateTime time)
    {
        if (Active?.BossOid != 0x2C13 || id is not (1 or 57)) return;
        cosmosTethers[source] = (target, id, present, time);
    }

    private void SynchronizeCosmos()
    {
        if (territory != CosmosCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2C1A)
            foreach (var pool in _actors.Values.Where(a => a.Oid == 0x1EAEAE && a.EventState != 7))
                foreach (var id in cosmosPools.Where(p => Vector3.DistanceSquared(p.Value.Position, pool.Position) <= 4).Select(p => p.Key).ToArray())
                    cosmosPools.Remove(id);
        if (Active?.BossOid != 0x2C13) return;
        foreach (var (source, tether) in cosmosTethers.ToArray())
            if (_now >= tether.Time.AddSeconds(.5)) cosmosTethers.Remove(source);
        foreach (var actor in _actors.Values.Where(a => a.Tethers != null))
            if (!cosmosTethers.ContainsKey(actor.Id))
                foreach (var tether in actor.Tethers!.Where(t => t.Key is 1 or 57))
                    cosmosTethers[actor.Id] = (tether.Value, tether.Key, true, _now.AddSeconds(-.5));
        cosmosCharges.RemoveAll(id => !_actors.TryGetValue(id, out var a) || a.Dead);
        foreach (var id in cosmosBlackFlames.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) cosmosBlackFlames.Remove(id);
    }

    private void CosmosEffect(ulong source, uint action, ulong target, Vector3? location, DateTime time)
    {
        if (territory != CosmosCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2C1A && action == 18852)
        {
            var position = _actors.GetValueOrDefault(target)?.Position ?? location ?? _casts.GetValueOrDefault(source)?.Location;
            if (position is { } p) cosmosPools[source] = new($"cosmos/predicted/{source}", "扫除前持续污渍", new CircleTemplate(6), p, Activation: time.AddSeconds(.1));
        }
        if (action == 18269) cosmosBlackFlames.Clear();
        if (action is 18271 or 18272 && cosmosCharges.Count > 0) cosmosCharges.RemoveAt(0);
        if (action == 18276) cosmosBusters.Remove(source);
    }

    private IEnumerable<DungeonActor> CosmosFurniture()
    {
        var chandelier = _actors.Values.Any(a => a.Oid == 0x2C12 && a.Targetable);
        return _actors.Values.Where(a => !a.Dead && (a.Oid is 0x2C16 or 0x2C18 or 0x2C14 or 0x2C17 or 0x2C15 || chandelier && a.Oid == 0x2C19));
    }

    private IEnumerable<DungeonDamagePrediction> CosmosDamage()
    {
        foreach (var cast in cosmosBusters.Values)
            if (_actors.TryGetValue(cast.TargetId, out var a) && !a.Dead && a.PartySlot >= 0)
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, cast.FinishAt, "Tankbuster");
    }

    private void CosmosRemoved(ulong source)
    {
        cosmosCharges.RemoveAll(id => id == source); cosmosBlackFlames.Remove(source); cosmosBusters.Remove(source);
        cosmosTethers.Remove(source);
        foreach (var id in cosmosTethers.Where(t => t.Value.Target == source).Select(t => t.Key).ToArray()) cosmosTethers.Remove(id);
    }

    private void ResetCosmos()
    {
        cosmosPools.Clear(); cosmosBlackFlames.Clear(); cosmosCharges.Clear(); cosmosIcons.Clear(); cosmosTethers.Clear();
        cosmosBusters.Clear(); cosmosTiles = []; cosmosSeedWaves = 0; cosmosTileCasts.Clear();
    }
}
