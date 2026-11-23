using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? xelHotBlast;
    private bool xelShrunk;
    private DungeonAmbientAoe? xelArenaWarning;
    private bool xelHaste;
    private DungeonPendingCast? xelKnockback;
    private IGeometryShape[] xelShelters = [];
    private readonly HashSet<ulong> xelSeenWhirlwinds = [];
    private readonly Dictionary<ulong, DateTime> xelSpreads = [];
    private readonly Dictionary<ulong, DateTime> xelStacks = [];
    private bool IsXelTrash => territory == XelphatolCatalog.TERRITORY && XelphatolCatalog.IsTrash(Active?.BossOid ?? 0);
    private bool XelKeysReady => _actors.Values.Any(a => XelphatolCatalog.IsKey(a.Oid) && a.Targetable);

    private bool XelCanStart(DungeonActor actor)
    {
        if (!XelphatolCatalog.IsTrash(actor.Oid)) return actor.InCombat;
        if (actor.Oid == 0x17BA && XelKeysReady) return false;
        if (actor.Oid == 0x17B8) return actor.InCombat;
        var arena = Definitions.First(d => d.BossOid == actor.Oid).Arena(actor.Position);
        return _actors.Values.Any(a => !a.Dead && a.InCombat && XelphatolCatalog.InGroup(actor.Oid, a.Oid) && arena.Contains(a.Position));
    }

    private bool XelTrashAlive() => IsXelTrash && !(Active!.BossOid == 0x17BA && XelKeysReady)
        && _actors.Values.Any(a => !a.Dead && XelphatolCatalog.InGroup(Active!.BossOid, a.Oid) && Arena!.Contains(a.Position));

    private bool XelTrashMember(DungeonActor actor) => IsXelTrash && !actor.Dead
        && XelphatolCatalog.InGroup(Active!.BossOid, actor.Oid) && Arena!.Contains(actor.Position);

    private void XelCast(DungeonPendingCast cast)
    {
        if (territory != XelphatolCatalog.TERRITORY) return;
        switch (cast.Rule.Action)
        {
            case 6604:
                if (_actors.TryGetValue(PrimaryId, out var boss))
                    xelHotBlast = cast with { Location = QuantizedDungeonPosition(boss.Position) };
                break;
            case 6609: xelHaste = true; break;
            case 6606: xelHaste = false; break;
            case 6607: xelKnockback = cast; XelBuildShelters(); break;
        }
    }

    private void XelBuildShelters()
    {
        var shelters = new List<IGeometryShape>();
        var center = XelphatolKnockback.CENTER with { Y = Arena!.Position.Y };
        for (var i = 0; i < 4; ++i)
        {
            var angle = i * MathF.PI / 2;
            var blocked = _actors.Values.Any(a => a.Oid == 0x17A0
                && MathF.Abs(MathF.Atan2(MathF.Sin(MathF.Atan2(a.Position.X - center.X, a.Position.Z - center.Z) - angle),
                    MathF.Cos(MathF.Atan2(a.Position.X - center.X, a.Position.Z - center.Z) - angle))) <= MathF.PI / 16);
            if (!blocked) shelters.Add(new FanGeometry(center, angle, 20, MathF.PI / 8));
        }
        xelShelters = shelters.ToArray();
    }

    private void XelFinish(DungeonPendingCast cast, bool completed)
    {
        if (cast.Rule.Action == 6604 && !completed) xelHotBlast = null;
        if (cast.Rule.Action == 6607) { xelKnockback = null; xelShelters = []; }
    }

    private void XelAnimation(ulong source, uint state)
    {
        if (Active?.BossOid == 0x179F && !xelShrunk && state == 0x00010002
            && _actors.GetValueOrDefault(source)?.Oid == 0x1EA187)
        {
            xelShrunk = true;
            Arena = XelphatolArenas.DotoliSmall(Arena!.Position.Y);
            xelArenaWarning = null;
        }
    }

    private void XelYell(ulong source, DateTime timestamp)
    {
        if (Active?.BossOid == 0x17B5 && xelArenaWarning == null && _actors.TryGetValue(source, out var actor) && actor.Position.Z < -10)
            XelSiegeWarning(true, timestamp);
    }

    private void XelSiegeWarning(bool first, DateTime timestamp)
    {
        var shape = first ? XelphatolArenas.SiegeHazardFirst(Arena!.Position.Y) : XelphatolArenas.SiegeHazardSecond(Arena!.Position.Y);
        xelArenaWarning = new("xel/siege/ring", "风暴环", new DungeonShapeTemplate(shape), shape.Position, Activation: timestamp.AddSeconds(4));
    }

    private void XelIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (!_actors.TryGetValue(target, out var actor) || actor.Dead) return;
        if (Active?.BossOid == 0x179F && icon == 139) xelSpreads[target] = timestamp.AddSeconds(5.1);
        if (Active?.BossOid == 0x17A2 && icon == 62) xelStacks[target] = timestamp.AddSeconds(5.5);
    }

    private void XelEffect(uint action, ulong target)
    {
        if (Active?.BossOid == 0x179F && action == 32556 && !xelSpreads.Remove(target) && xelSpreads.Count > 0)
            xelSpreads.Remove(xelSpreads.Keys.First());
        if (Active?.BossOid == 0x17A2 && action == 6617 && !xelStacks.Remove(target) && xelStacks.Count > 0)
            xelStacks.Remove(xelStacks.Keys.First());
    }

    private void SynchronizeXel()
    {
        if (territory != XelphatolCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x17B5 && xelArenaWarning == null && _actors.GetValueOrDefault(PrimaryId)?.Position.Z > -10)
            XelSiegeWarning(false, _now);
        if (Active?.BossOid == 0x179F)
        {
            if (!xelShrunk && xelArenaWarning == null)
            {
                var ring = XelphatolArenas.DotoliRing(Arena!.Position.Y);
                xelArenaWarning = new("xel/dotoli/ring", "缩场预告", new DungeonShapeTemplate(ring), ring.Position, Activation: _now.AddSeconds(4));
            }
            var fresh = false;
            foreach (var wind in _actors.Values.Where(a => a.Oid == 0x17A0))
                fresh |= xelSeenWhirlwinds.Add(wind.Id);
            if (fresh && xelKnockback != null && xelShelters.Length > 0) XelBuildShelters();
        }
        foreach (var id in xelSpreads.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) xelSpreads.Remove(id);
        foreach (var id in xelStacks.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) xelStacks.Remove(id);
        if (xelHotBlast != null && _now >= xelHotBlast.FinishAt.AddSeconds(1)) xelHotBlast = null;
    }

    private void XelRemoved(ulong id)
    {
        xelSpreads.Remove(id); xelStacks.Remove(id);
        // Whirlwind 的危险保留到对象销毁；其出生只刷新已有安全扇区。
        xelSeenWhirlwinds.Remove(id);
        if (xelKnockback?.Source == id) { xelKnockback = null; xelShelters = []; }
        if (xelHotBlast?.Source == id) xelHotBlast = null;
    }

    private void ResetXel()
    {
        xelHotBlast = null; xelShrunk = false; xelArenaWarning = null; xelHaste = false;
        xelKnockback = null; xelShelters = []; xelSeenWhirlwinds.Clear(); xelSpreads.Clear(); xelStacks.Clear();
    }
}
