using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int troiaWalls = 0xFFF;
    private int troiaPending;
    private bool troiaDefaultArena;
    private bool troiaReturning;
    private DateTime? troiaPitch;
    private DungeonPendingCast? troiaVacuum;
    private IGeometryShape[] troiaShelters = [];
    private bool troiaShelterHint;
    private readonly List<(Vector2 A, Vector2 B)> troiaLines = TroiaKnockback.Walls.ToList();
    private (Vector2 A, Vector2 B)[] troiaLockedLines = [];
    private readonly HashSet<ulong> troiaDoom = [];
    private readonly HashSet<ulong> troiaDoomUnconfirmed = [];
    private readonly HashSet<ulong> troiaDoomLosses = [];
    private readonly List<ulong> troiaCappedOrder = [];

    private bool IsTroiaGroup => territory == TroiaCatalog.TERRITORY
        && Active != null && (TroiaCatalog.IsTrash(Active.BossOid) || Active.BossOid == 0x3988);

    private bool TroiaMember(DungeonActor actor) => Active != null
        && (actor.Oid == Active.BossOid || Active.PrimaryAliases?.Contains(actor.Oid) == true)
        && (Active.BossOid == 0x3988 || Arena?.Contains(actor.Position, 0) == true);

    private DungeonDefinition? TroiaDefinition(DungeonActor actor) => Definitions.FirstOrDefault(d => d.BossOid == actor.Oid)
        ?? Definitions.FirstOrDefault(d => d.PrimaryAliases?.Contains(actor.Oid) == true
            && (actor.Oid != 0x3980 || d.Arena(actor.Position).Contains(actor.Position, 0)));

    private IGeometryShape TroiaArena(DungeonActor primary)
    {
        if (Active == null) throw new InvalidOperationException("缺少特罗亚战斗定义。");
        if (primary.Oid == Active.BossOid) return Active.Arena(primary.Position);
        var original = _actors.Values.Where(a => a.Oid == Active.BossOid && !a.Dead)
            .OrderBy(a => Vector3.DistanceSquared(a.Position, primary.Position)).FirstOrDefault();
        if (original != null) return Active.Arena(original.Position);
        if (Active.BossOid == 0x397D)
            return TroiaArenas.ScavangerFirst(primary.Position.Y).Contains(primary.Position, 0)
                ? TroiaArenas.ScavangerFirst(primary.Position.Y) : TroiaArenas.ScavangerSecond(primary.Position.Y);
        return Active.Arena(primary.Position);
    }

    private bool TroiaCanStart(DungeonActor actor)
    {
        var definition = TroiaDefinition(actor);
        if (definition == null) return false;
        if (definition.BossOid != 0x3988) return actor.InCombat;
        return actor.Oid == 0x3966 && actor.InCombat;
    }

    private void TroiaSelectPrimary()
    {
        if (!IsTroiaGroup) return;
        var survivors = _actors.Values.Where(a => !a.Dead && TroiaMember(a)).ToArray();
        if (survivors.Length == 0) { ResetFight(); return; }
        if (!_actors.TryGetValue(PrimaryId, out var current) || current.Dead || !TroiaMember(current))
            PrimaryId = survivors[0].Id;
    }

    private void TroiaCast(DungeonPendingCast cast)
    {
        if (territory != TroiaCatalog.TERRITORY) return;
        if (cast.Rule.Action is 29817 or 29828 && !troiaCappedOrder.Contains(cast.Source)) troiaCappedOrder.Add(cast.Source);
        if (cast.Rule.Action != 30236 || Arena == null) return;
        // A corrected packet may refine the location, but must not relock walls mid-cast.
        if (troiaVacuum?.Source == cast.Source && troiaVacuum.StartedAt == cast.StartedAt)
        {
            troiaVacuum = cast;
            return;
        }
        troiaVacuum = cast;
        troiaLockedLines = troiaLines.ToArray();
        troiaShelterHint = troiaWalls != 0;
        troiaShelters = Enumerable.Range(0, 12).Where(i => (troiaWalls & ~troiaPending & (1 << i)) != 0)
            .Select(i =>
            {
                var wall = TroiaDynamicArena.WallCenters[i];
                var angle = MathF.Atan2(wall.X + 35, wall.Y + 298);
                return new FanTemplate(20, 14 * MathF.PI / 180).Resolve(TroiaDynamicArena.Center with { Y = Arena.Position.Y }, angle, null);
            }).ToArray();
    }

    private void TroiaFinish(DungeonPendingCast cast)
    {
        troiaCappedOrder.Remove(cast.Source);
        if (territory == TroiaCatalog.TERRITORY && cast.Rule.Action == 30236)
        {
            troiaVacuum = null;
            troiaShelters = [];
            troiaLockedLines = [];
            troiaShelterHint = false;
        }
    }

    private void TroiaMap(uint index, uint state)
    {
        if (territory != TroiaCatalog.TERRITORY || Active?.BossOid != 0x39C5 || Arena == null) return;
        if (state == 0x00020001 && index == 4)
        {
            troiaReturning = false;
            troiaDefaultArena = true;
            return;
        }
        if (index is < 5 or > 0x10) return;
        var wall = index <= 0xA ? (int)index + 1 : (int)index - 0xB;
        if (state == 0x00200010)
        {
            troiaWalls &= ~(1 << wall);
            troiaPending &= ~(1 << wall);
            Arena = TroiaDynamicArena.Create(Arena.Position.Y, troiaWalls, false);
        }
        else if (state == 0x00020001)
        {
            troiaPending |= 1 << wall;
            // Source matches the first segment by X, including the crossed 08/09 keys.
            float[] keys = [-36.947f, -45.288f, -51.547f, -54.477f, -51.652f, -45.457f,
                -33.053f, -24.712f, -18.453f, -18.348f, -36.947f, -24.543f];
            var line = troiaLines.FindIndex(l => l.A.X == keys[wall]);
            if (line >= 0) troiaLines.RemoveAt(line);
        }
    }

    private void SynchronizeTroia()
    {
        if (territory != TroiaCatalog.TERRITORY) return;
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
        {
            if (actor.Statuses?.Contains(3364u) == true)
            {
                troiaDoom.Add(actor.Id);
                troiaDoomUnconfirmed.Remove(actor.Id);
            }
            else if (troiaDoomLosses.Remove(actor.Id) || !troiaDoomUnconfirmed.Contains(actor.Id))
                troiaDoom.Remove(actor.Id);
        }
        troiaDoom.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || a.Dead);
        if (Active?.BossOid != 0x39C5 || Arena == null || !_actors.TryGetValue(PlayerId, out var player)) return;
        if (!troiaReturning && troiaDefaultArena && !Arena.Contains(player.Position, 0))
        {
            troiaReturning = true;
            Arena = TroiaDynamicArena.Create(Arena.Position.Y, troiaWalls, true);
        }
        else if (troiaReturning && Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(-35, -298)) < 399)
        {
            troiaReturning = false;
            Arena = TroiaDynamicArena.Create(Arena.Position.Y, troiaWalls, false);
        }
    }

    private void TroiaStatus(ulong target, uint status, bool present)
    {
        if (territory != TroiaCatalog.TERRITORY || status != 3364) return;
        if (present)
        {
            troiaDoom.Add(target);
            troiaDoomUnconfirmed.Add(target);
            troiaDoomLosses.Remove(target);
        }
        else
        {
            troiaDoomUnconfirmed.Remove(target);
            troiaDoomLosses.Add(target);
        }
    }

    internal DungeonActor? TroiaCleanseTarget(uint job)
    {
        if (territory != TroiaCatalog.TERRITORY || Active?.BossOid != 0x396D
            || job is not (6 or 24 or 28 or 33 or 40 or 23) || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => troiaDoom.Contains(a.Id) && !a.Dead && a.Targetable && a.PartySlot >= 0
            && Vector3.DistanceSquared(a.Position, player.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    private void TroiaEffect(uint action, DateTime time)
    {
        if (territory != TroiaCatalog.TERRITORY || Active?.BossOid != 0x39C5) return;
        if (action == 30245) troiaPitch = time.AddSeconds(8.1);
        if (action == 30249) troiaPitch = null;
    }

    private IEnumerable<DungeonPendingCast> TroiaGazes() => territory == TroiaCatalog.TERRITORY
        ? _casts.Values.Where(c => c.Rule.Action == 29821).Concat(troiaCappedOrder.Select(id => _casts.GetValueOrDefault(id))
            .OfType<DungeonPendingCast>().Where(c => c.Rule.Action == 29828).Take(2))
        : [];

    private bool TroiaCastVisible(DungeonPendingCast cast)
    {
        if (territory != TroiaCatalog.TERRITORY) return true;
        if (cast.Rule.Action is 29621 or 29622 or 30260) return false;
        if (cast.Rule.Action == 29628 && !_actors.Values.Any(a => a.Oid == 0x3968 && a.Dead)) return false;
        return cast.Rule.Action != 29817 || troiaCappedOrder.Select(id => _casts.GetValueOrDefault(id))
            .OfType<DungeonPendingCast>().Where(c => c.Rule.Action == 29817).Take(8).Any(c => c.Source == cast.Source);
    }

    private void ResetTroia()
    {
        troiaWalls = 0xFFF; troiaPending = 0; troiaDefaultArena = false; troiaReturning = false; troiaPitch = null;
        troiaVacuum = null; troiaShelters = []; troiaLockedLines = []; troiaShelterHint = false;
        troiaLines.Clear(); troiaLines.AddRange(TroiaKnockback.Walls);
        troiaDoom.Clear(); troiaDoomUnconfirmed.Clear(); troiaDoomLosses.Clear();
        troiaCappedOrder.Clear();
    }
}
