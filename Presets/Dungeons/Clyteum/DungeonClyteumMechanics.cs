using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonPendingCast> clySimple = [];
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast)> clyParty = [];
    private readonly List<DungeonPendingCast> clyTowers = [];
    private readonly List<DungeonPendingCast> clyKbs = [];
    private readonly List<(DungeonActor Target, DateTime At, long Serial)> clyIconSpreads = [];
    private readonly HashSet<(ulong Source, DateTime Start)> clyStarts = [];
    private readonly Dictionary<ulong, DateTime> clyStay = [];
    private readonly Dictionary<int, DateTime> clyMove = [];
    private readonly Dictionary<ulong, int?> clyRenderFlags = [];
    private readonly HashSet<ulong> clyObservedStatus = [];
    private readonly Dictionary<ulong, (bool Present, DateTime? Expires)> clyPendingStatus = [];
    private readonly Dictionary<ulong, DateTime> clyStatusExpirations = [];
    private DungeonActor? clyBeam;
    private Vector3? clyMoveAnchor;
    private ulong clyMovePlayer;
    private long clySerial;

    internal DateTime? ClyteumStopActivation => territory == ClyteumCatalog.TERRITORY
        && Active?.BossOid == 0x4C2C && _actors.GetValueOrDefault(PlayerId) is { Dead: false }
        && clyStay.TryGetValue(PlayerId, out var at) ? at : null;
    internal bool RequireClyteumStop => ClyteumStopActivation is { } at && at < _now.AddSeconds(1);
    internal bool RequireClyteumMovement => territory == ClyteumCatalog.TERRITORY
        && Active?.BossOid == 0x4C28 && _actors.GetValueOrDefault(PlayerId) is { Dead: false, PartySlot: >= 0 } player
        && clyMove.TryGetValue(player.PartySlot, out var at) && at <= _now.AddSeconds(1);

    private static int ClySimpleGroup(uint action) => action switch
    {
        50177 or 50178 => 1,
        50400 or 48876 => 2,
        48937 or 48943 => 3,
        48935 or 48940 => 4,
        _ => 0,
    };

    private void ClyCast(DungeonPendingCast c)
    {
        if (territory != ClyteumCatalog.TERRITORY) return;
        var fresh = clyStarts.Add((c.Source, c.StartedAt));
        void Upsert(List<DungeonPendingCast> list)
        {
            var i = list.FindIndex(v => v.Source == c.Source && v.StartedAt == c.StartedAt);
            if (i >= 0) list[i] = c;
            else if (fresh) list.Add(c);
        }
        if (c.Rule.PartyAoe != null && _actors.TryGetValue(c.TargetId, out var target))
        {
            var i = clyParty.FindIndex(v => v.Cast.Source == c.Source && v.Cast.StartedAt == c.StartedAt);
            if (i >= 0) clyParty[i] = (target, c);
            else if (fresh) clyParty.Add((target, c));
        }
        else if (c.Rule.Shape != null) Upsert(clySimple);
        if (c.Rule.Action == 48882) Upsert(clyTowers);
        if (c.Rule.Action == 48878) Upsert(clyKbs);
        if (c.Rule.Action == 48931 && fresh && _actors.GetValueOrDefault(c.Source)?.Oid == 0x4C28)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead)) clyMove[a.PartySlot] = _now.AddSeconds(5);
        var group = ClySimpleGroup(c.Rule.Action);
        if (group != 0 && clySimple.Count(v => ClySimpleGroup(v.Rule.Action) == group) >= 99)
        {
            var indices = Enumerable.Range(0, clySimple.Count).Where(n => ClySimpleGroup(clySimple[n].Rule.Action) == group).ToArray();
            var ordered = indices.Select(n => clySimple[n]).OrderBy(v => v.FinishAt).ToArray();
            for (var i = 0; i < indices.Length; ++i) clySimple[indices[i]] = ordered[i];
        }
    }

    private void ClyFinish(DungeonPendingCast c)
    {
        if (territory != ClyteumCatalog.TERRITORY) return;
        clySimple.RemoveAll(v => v.Source == c.Source && (ClySimpleGroup(v.Rule.Action) != 0 || v.Rule.Action == c.Rule.Action));
        var partyIndex = clyParty.FindIndex(v => v.Cast.Rule.Action == c.Rule.Action && v.Target.Id == c.TargetId);
        if (partyIndex >= 0) clyParty.RemoveAt(partyIndex);
        clyTowers.RemoveAll(v => v.Source == c.Source);
        if (c.Rule.Action == 48878) clyKbs.RemoveAll(v => v.Source == c.Source);
        if (c.Rule.Action == 48931 && _actors.GetValueOrDefault(c.Source)?.Oid == 0x4C28)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead)) clyMove.Remove(a.PartySlot);
    }

    private void ClyStatus(ulong id, uint status, bool present, DateTime? expires)
    {
        if (territory != ClyteumCatalog.TERRITORY || Active?.BossOid != 0x4C2C || status != 5191
            || _actors.GetValueOrDefault(id)?.PartySlot is not >= 0) return;
        clyPendingStatus[id] = (present, expires);
        if (present && expires is { } at) clyStay[id] = at;
        else if (!present) clyStay.Remove(id);
    }

    private void ClyIcon(ulong target, uint icon, DateTime now, ulong source)
    {
        if (territory != ClyteumCatalog.TERRITORY || Active?.BossOid != 0x4C3F || icon != 558) return;
        if (_actors.TryGetValue(source is 0 or 0xE0000000 ? target : source, out var actor))
            clyIconSpreads.Add((actor, now.AddSeconds(5), ++clySerial));
    }

    private void ClyEffect(uint action, ulong target)
    {
        if (territory != ClyteumCatalog.TERRITORY || action != 48885 || clyIconSpreads.Count == 0) return;
        var i = clyIconSpreads.FindIndex(v => v.Target.Id == target);
        clyIconSpreads.RemoveAt(i < 0 ? 0 : i);
    }

    private void ClyObserve()
    {
        if (territory != ClyteumCatalog.TERRITORY) return;
        DungeonActor Latest(DungeonActor a) => _actors.GetValueOrDefault(a.Id) ?? a;
        for (var i = 0; i < clyParty.Count; ++i) clyParty[i] = (Latest(clyParty[i].Target), clyParty[i].Cast);
        for (var i = 0; i < clyIconSpreads.Count; ++i)
        {
            var b = clyIconSpreads[i]; clyIconSpreads[i] = (Latest(b.Target), b.At, b.Serial);
        }
        if (Active?.BossOid == 0x4C2C)
        {
            foreach (var a in _actors.Values)
            {
                if (a.Oid == 0x4C2D && (!clyRenderFlags.TryGetValue(a.Id, out var previous) || previous != a.RenderFlags))
                {
                    clyRenderFlags[a.Id] = a.RenderFlags;
                    if (a.RenderFlags == 0) clyBeam = a;
                    else if (a.RenderFlags == 16384) clyBeam = null;
                }
                if (a.PartySlot < 0) continue;
                var present = a.Statuses?.Contains(5191u) == true;
                DateTime? expires = a.StatusExpirations?.TryGetValue(5191, out var expiration) == true ? expiration : null;
                if (clyPendingStatus.TryGetValue(a.Id, out var pending))
                {
                    if (pending.Present != present || present && pending.Expires.HasValue && pending.Expires != expires) continue;
                    clyPendingStatus.Remove(a.Id);
                }
                if (present && expires.HasValue)
                {
                    if (clyObservedStatus.Add(a.Id) || clyStatusExpirations.GetValueOrDefault(a.Id) != expires.Value)
                        clyStay[a.Id] = expires.Value;
                    clyStatusExpirations[a.Id] = expires.Value;
                }
                if (!present && clyObservedStatus.Remove(a.Id))
                {
                    clyStay.Remove(a.Id);
                    clyStatusExpirations.Remove(a.Id);
                }
            }
            if (clyBeam != null) clyBeam = Latest(clyBeam);
        }
        ClyTick();
    }

    private void ClyTick()
    {
        if (territory != ClyteumCatalog.TERRITORY) return;
        clyIconSpreads.RemoveAll(v => v.Target.Dead);
        foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && a.Dead)) clyMove.Remove(a.PartySlot);
        // Source writes this state while drawing. Evaluate on the simulation clock
        // so radar/world visibility toggles never disable the control contract.
        if (clyBeam != null && _actors.TryGetValue(PlayerId, out var player))
        {
            var shape = new RectangleGeometry(clyBeam.Position, clyBeam.Rotation, 40, 20);
            if (shape.Contains(player.Position, 0)) clyStay[PlayerId] = _now;
            else clyStay.Remove(PlayerId);
        }
        if (!RequireClyteumMovement) { clyMoveAnchor = null; return; }
        var moving = _actors[PlayerId];
        if (clyMoveAnchor == null || clyMovePlayer != PlayerId || Vector3.DistanceSquared(moving.Position, clyMoveAnchor.Value) > 1.21f)
        {
            clyMovePlayer = PlayerId; clyMoveAnchor = moving.Position;
        }
    }

    private void ResetCly()
    {
        clySimple.Clear(); clyParty.Clear(); clyTowers.Clear(); clyKbs.Clear(); clyIconSpreads.Clear(); clyStarts.Clear();
        clyStay.Clear(); clyMove.Clear(); clyRenderFlags.Clear(); clyObservedStatus.Clear(); clyPendingStatus.Clear();
        clyStatusExpirations.Clear();
        clyBeam = null; clyMoveAnchor = null; clyMovePlayer = 0; clySerial = 0;
    }
}
