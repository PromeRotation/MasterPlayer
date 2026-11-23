using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsMesoHeadsmen => territory == MesoCatalog.TERRITORY && Active?.BossOid == 0x4891;
    private readonly Dictionary<uint, ulong> mesoBossIds = [];
    private readonly HashSet<ulong> mesoDestroyed = [];
    private readonly Dictionary<ulong, ulong> mesoAssigned = [];
    private readonly List<Vector3> mesoPrisons = [];
    private readonly List<DungeonAmbientAoe> mesoHellmakers = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Param)> mesoStatusObserved = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Param)> mesoPendingStatuses = [];
    private readonly HashSet<(ulong Actor, uint Status)> mesoInferredStatuses = [];
    private readonly Dictionary<(ulong Actor, uint Tether), ulong> mesoTethers = [];
    private readonly Dictionary<(ulong Actor, uint Tether), ulong> mesoPendingTethers = [];
    private bool mesoPrisonDirty = true;
    private IGeometryShape? mesoPrisonArena;
    private IGeometryShape? mesoDefaultArena;

    private void MesoSelectPrimary()
    {
        if (!IsMesoHeadsmen) return;
        foreach (var a in _actors.Values.Where(a => MesoCatalog.Headsman(a.Oid))) mesoBossIds.TryAdd(a.Oid, a.Id);
        if (mesoBossIds.Count > 0 && mesoBossIds.Values.All(id => mesoDestroyed.Contains(id) || !_actors.TryGetValue(id, out var a) || a.Dead))
            ResetFight();
        // Keep PrimaryId stable: CellBlockBeta refers to the original Pale Headsman.
    }

    private bool MesoHellmaker(DungeonActor a) => IsMesoHeadsmen && a.Oid == 0x48D2
        && _actors.TryGetValue(PlayerId, out var p)
        && Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(a.Position.X, a.Position.Z)) < 324;
    private bool MesoForbidden(DungeonActor a) => IsMesoHeadsmen && mesoAssigned.GetValueOrDefault(PlayerId) is var assigned
        && assigned != 0 && a.Id != assigned && !MesoHellmaker(a);
    private int MesoPriority(DungeonActor a) => MesoForbidden(a) ? 0 : MesoHellmaker(a) ? 2
        : mesoAssigned.GetValueOrDefault(PlayerId) == a.Id ? 1 : 0;

    private void MesoTether(ulong source, ulong target, uint id, bool present, bool snapshot = false)
    {
        if (!IsMesoHeadsmen || id is not (249 or 341)) return;
        var key = (source, id);
        if (!snapshot) mesoPendingTethers[key] = present ? target : 0;
        if (present && mesoTethers.GetValueOrDefault(key) == target) return;
        if (!present && !mesoTethers.ContainsKey(key)) return;
        if (present)
        {
            if (id == 341 && mesoTethers.TryGetValue(key, out var previousTarget))
                mesoAssigned.Remove(previousTarget);
            mesoTethers[key] = target;
            if (_actors.GetValueOrDefault(target)?.PartySlot >= 0) mesoAssigned[target] = source;
        }
        else
        {
            mesoTethers.Remove(key);
            if (id == 341) mesoAssigned.Remove(target);
        }
        MesoUpdateArena();
    }

    private void MesoStatus(ulong target, uint status, bool present, ushort param, bool snapshot = false)
    {
        if (territory != MesoCatalog.TERRITORY || status is not (2552 or 4542 or 4543 or 4544 or 4545 or 4550)) return;
        if (present && mesoDestroyed.Contains(target)) return;
        var key = (target, status);
        var previous = mesoStatusObserved.GetValueOrDefault(key);
        if (!present && param == 0) param = previous.Param;
        var desired = (present, param);
        if (!snapshot) mesoPendingStatuses[key] = desired;
        var unchanged = previous.Present == present && (!present || previous.Param == param);
        if (unchanged && (snapshot || mesoInferredStatuses.Remove(key))) return;
        if (snapshot) mesoInferredStatuses.Add(key);
        else mesoInferredStatuses.Remove(key);
        mesoStatusObserved[key] = desired;
        if (!IsMesoHeadsmen) return;
        var actor = _actors.GetValueOrDefault(target) ?? mesoLastActors.GetValueOrDefault(target);
        if (status == 2552 && param == 0x394 && actor != null)
        {
            var center = new Vector3(MathF.Round(actor.Position.X * 2) * .5f, actor.Position.Y, MathF.Round(actor.Position.Z * 2) * .5f);
            if (present) mesoPrisons.Add(center);
            else
            {
                var i = mesoPrisons.FindIndex(p => p.X == center.X && p.Z == center.Z);
                if (i >= 0) mesoPrisons.RemoveAt(i);
            }
            mesoPrisonDirty = true;
        }
        if (present && _actors.GetValueOrDefault(target)?.PartySlot >= 0)
        {
            var oid = status switch { 4542 => 0x4890u, 4543 => 0x4891u, 4544 => 0x4892u, 4545 => 0x4893u, _ => 0u };
            if (oid != 0 && mesoBossIds.TryGetValue(oid, out var boss)) mesoAssigned[target] = boss;
            if (status == 4550) mesoAssigned.Remove(target);
        }
        MesoUpdateArena();
    }

    private void MesoObservePrisons()
    {
        if (!IsMesoHeadsmen) return;
        foreach (var a in _actors.Values.Where(a => MesoCatalog.Headsman(a.Oid))) mesoBossIds.TryAdd(a.Oid, a.Id);
        var statuses = _actors.Values.Where(a => !mesoDestroyed.Contains(a.Id)).SelectMany(a => (a.Statuses ?? [])
            .Where(s => s is 2552 or 4542 or 4543 or 4544 or 4545 or 4550)
            .Select(s => (Key: (Actor: a.Id, Status: s), Value: (Present: true, Param: a.StatusParams?.GetValueOrDefault(s) ?? 0))))
            .ToDictionary(p => p.Key, p => p.Value);
        foreach (var key in mesoStatusObserved.Keys) statuses.TryAdd(key, (false, mesoStatusObserved[key].Param));
        foreach (var (key, value) in mesoPendingStatuses.ToArray())
        {
            var raw = statuses.GetValueOrDefault(key);
            if (raw.Present == value.Present && (!raw.Present || raw.Param == value.Param)) mesoPendingStatuses.Remove(key);
            else statuses[key] = value;
        }
        foreach (var (key, value) in statuses) MesoStatus(key.Actor, key.Status, value.Present, value.Param, true);
        var tethers = _actors.Values.Where(a => !mesoDestroyed.Contains(a.Id)).SelectMany(a => (a.Tethers ?? new Dictionary<uint, ulong>())
            .Where(t => t.Key is 249 or 341).Select(t => (Key: (Actor: a.Id, Tether: t.Key), t.Value)))
            .ToDictionary(p => p.Key, p => p.Value);
        foreach (var (key, value) in mesoPendingTethers.ToArray())
        {
            if (!_actors.ContainsKey(key.Actor) || tethers.GetValueOrDefault(key) == value) mesoPendingTethers.Remove(key);
            else if (value == 0) tethers.Remove(key);
            else tethers[key] = value;
        }
        foreach (var (key, target) in mesoTethers.ToArray())
            if (tethers.GetValueOrDefault(key) != target) MesoTether(key.Actor, target, key.Tether, false, true);
        foreach (var (key, target) in tethers)
            if (mesoTethers.GetValueOrDefault(key) != target) MesoTether(key.Actor, target, key.Tether, true, true);
        MesoUpdateArena();
    }

    private void MesoUpdateArena()
    {
        if (!IsMesoHeadsmen || Arena == null) return;
        mesoDefaultArena ??= MesoArenas.Headsmen(Arena.Position.Y);
        if (mesoPrisonDirty)
        {
            mesoPrisonArena = MesoPrisonArena.Build(Arena.Position.Y, mesoPrisons);
            mesoPrisonDirty = false;
        }
        Arena = mesoAssigned.ContainsKey(PlayerId) ? mesoPrisonArena : mesoDefaultArena;
    }

    public DungeonActor? MesoCleanseTarget(uint job)
    {
        if (territory != MesoCatalog.TERRITORY || job is not (6 or 24 or 28 or 33 or 40 or 23)
            || !_actors.TryGetValue(PlayerId, out var p)) return null;
        var status = Active?.BossOid == 0x488F ? 2104u : IsMesoHeadsmen ? 4594u : 0;
        return _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, status)
                && Vector3.DistanceSquared(a.Position, p.Position) <= 900)
            .OrderBy(a => a.PartySlot).FirstOrDefault();
    }

    public DungeonActor? MesoInterruptTarget(uint job)
    {
        if (!IsMesoHeadsmen || KtisisCatalog.InterruptAction(job) is not (7538 or 7551)
            || !_actors.TryGetValue(PlayerId, out var p)) return null;
        var range = KtisisCatalog.InterruptAction(job) == 7538 ? 3 : 25;
        return _casts.Values.Where(c => c.Rule.Action == 44856).Select(c => _actors.GetValueOrDefault(c.Source))
            .OfType<DungeonActor>().FirstOrDefault(a => !a.Dead && a.Targetable
                && Vector3.Distance(a.Position, p.Position) <= range + a.HitboxRadius + p.HitboxRadius);
    }
}
