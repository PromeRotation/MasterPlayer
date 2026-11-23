using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<ulong> zotSeenSpheres = [];
    private readonly Dictionary<ulong, DateTime> zotBirths = [];
    private readonly List<DungeonAmbientAoe> zotSpheres = [];
    private int zotSphereWaves;
    private bool zotSphereNeedsUpdate;
    private bool zotDhrupad;
    private int zotDhrupadHits;
    private bool IsZotSisters => territory == ZotCatalog.TERRITORY && Active?.BossOid == 0x33F1;
    private static bool ZotSisterAlive(DungeonActor a) => !a.Dead && a.Oid is 0x33F1 or 0x33F2 or 0x33F3;

    private void ZotSelectPrimary()
    {
        if (!IsZotSisters) return;
        if (!_actors.Values.Any(ZotSisterAlive)) { ResetFight(); return; }
        if (!_actors.TryGetValue(PrimaryId, out var primary) || !ZotSisterAlive(primary))
            PrimaryId = _actors.Values.First(ZotSisterAlive).Id;
    }

    private void SynchronizeZot()
    {
        if (territory != ZotCatalog.TERRITORY || Active?.BossOid is not (0x33EF or 0x33F1)) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x33F0))
        {
            if (!zotSeenSpheres.Add(actor.Id)) continue;
            var delay = IsZotSisters ? 7.3 : zotSphereWaves == 0 ? 10.8 : 20;
            zotSpheres.Add(new($"zot/sphere/{actor.Id}", "狂战士球爆炸", new CircleTemplate(15),
                QuantizedDungeonPosition(actor.Position), Navigate: IsZotSisters,
                Activation: zotBirths.GetValueOrDefault(actor.Id, _now).AddSeconds(delay)));
            if (!IsZotSisters) zotSphereNeedsUpdate = true;
        }
        TickZot();
    }

    private void TickZot()
    {
        if (!zotSphereNeedsUpdate || zotSpheres.Count == 0 || zotSpheres[0].Activation!.Value.AddSeconds(-7) >= _now) return;
        for (var i = 0; i < zotSpheres.Count; ++i) zotSpheres[i] = zotSpheres[i] with { Navigate = true };
        zotSphereNeedsUpdate = false;
    }

    private void ZotCast(DungeonPendingCast cast)
    {
        if (territory == ZotCatalog.TERRITORY && cast.Rule.Action is 25244 or 25281) zotDhrupad = true;
    }

    private void ZotFinish(DungeonPendingCast cast)
    {
        if (territory != ZotCatalog.TERRITORY || cast.Rule.Action is not (25252 or 25279)) return;
        // Upstream cast-finished includes cancellation and source destruction.
        zotSpheres.Clear();
        zotSphereNeedsUpdate = false;
        ++zotSphereWaves;
    }

    private void ZotEffect(uint action)
    {
        if (territory != ZotCatalog.TERRITORY) return;
        var dhrupadHit = Active?.BossOid == 0x33EE ? action is 25245 or 25246 or 25247
            : IsZotSisters && action is 25282 or 25283 or 25284;
        if (dhrupadHit && ++zotDhrupadHits == 3) { zotDhrupad = false; zotDhrupadHits = 0; }
    }

    internal DungeonActor? ZotCleanseTarget(uint job)
    {
        if (territory != ZotCatalog.TERRITORY || Active?.BossOid is not (0x33EE or 0x33F1)
            || job is not (6 or 24 or 28 or 33 or 40 or 23) || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Targetable && HasStatus(a, 18)
                && Vector3.DistanceSquared(player.Position, a.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    private IEnumerable<DungeonAmbientAoe> ZotAoes()
    {
        if (territory != ZotCatalog.TERRITORY || Arena == null) yield break;
        foreach (var sphere in zotSpheres) yield return sphere;
        // The target also has to join the group; a display-only stack circle cannot move it.
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25272 && c.TargetId == PlayerId))
        {
            var regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2.5f)).ToArray();
            if (regions.Length > 0)
                yield return new($"zot/stack/{cast.Source}/join", "四人集合分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
        }
    }

    private void ResetZot()
    {
        zotSeenSpheres.Clear(); zotBirths.Clear(); zotSpheres.Clear();
        zotSphereWaves = 0; zotSphereNeedsUpdate = false; zotDhrupad = false; zotDhrupadHits = 0;
    }
}
