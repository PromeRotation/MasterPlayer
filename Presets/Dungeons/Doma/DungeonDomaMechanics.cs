using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonAmbientAoe> domaChains = [];
    private readonly Dictionary<ulong, DateTime> domaTetherEvents = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> domaCharges = [];
    private readonly Dictionary<ulong, DateTime> domaStacks = [];
    private readonly Dictionary<ulong, DateTime> domaSpreads = [];
    private readonly Dictionary<ulong, DateTime?> domaPrey = [];
    private readonly HashSet<ulong> domaPreyResolved = [];
    private DungeonAmbientAoe? domaSaw;
    private int domaSawHits;
    private ulong domaGunsawTarget;
    private int domaGunsawHits;

    private void DomaCast(DungeonPendingCast cast)
    {
        if (territory != DomaCatalog.TERRITORY) return;
        if (cast.Rule.Action == 8360)
            domaSaw = new("doma/chainsaw", "连续五次电锯", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (cast.Rule.Action == 8369 && _actors.TryGetValue(cast.Source, out var source))
        {
            var delta = cast.Location - source.Position;
            domaCharges[cast.Source] = new($"doma/cleancut/{cast.Source}", "环刃冲锋", RayRectangleTemplate.Symmetric(8, new Vector2(delta.X, delta.Z).Length()),
                source.Position, MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt);
        }
    }

    private void DomaFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != DomaCatalog.TERRITORY) return;
        if (cast.Rule.Action == 8369) domaCharges.Remove(cast.Source);
        if (cast.Rule.Action == 8360 && !completed) { domaSaw = null; domaSawHits = 0; }
    }

    private void DomaIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x1BD0 && icon == 62) domaStacks[target] = time.AddSeconds(5.1);
        if (Active?.BossOid == 0x1BD4 && icon == 99) domaSpreads[target] = time.AddSeconds(4);
    }

    private void DomaTether(ulong source, uint tether, bool present, DateTime time)
    {
        if (Active?.BossOid is not (0x1BD0 or 0x1BD4) || tether != 60 || !_actors.TryGetValue(source, out var actor)) return;
        domaTetherEvents[source] = time;
        if (!present) { domaChains.Remove(source); return; }
        if (domaChains.ContainsKey(source)) return;
        var p = QuantizedDungeonPosition(actor.Position);
        var dir = new Vector3(MathF.Sin(actor.Rotation), 0, MathF.Cos(actor.Rotation));
        domaChains[source] = new($"doma/chain/{source}", "连锁地雷", RayRectangleTemplate.Symmetric(4, 50), p - dir * 10,
            actor.Rotation, Activation: time.AddSeconds(5.6));
    }

    private void DomaStatus(ulong target, uint status, bool present, DateTime? expires)
    {
        if (Active?.BossOid != 0x1BD4 || status != 1253) return;
        if (present) { domaPrey[target] = expires; domaPreyResolved.Remove(target); }
        else { domaPreyResolved.UnionWith(domaPrey.Keys); domaPrey.Clear(); }
    }

    private void DomaEffect(uint action, ulong target)
    {
        if (territory != DomaCatalog.TERRITORY) return;
        if (action == 8355 && !domaStacks.Remove(target)) domaStacks.Clear();
        if (action == 8365) domaSpreads.Remove(target);
        if (action is 8360 or 8361 && ++domaSawHits >= 5) { domaSaw = null; domaSawHits = 0; }
        if (action == 8362 && _actors.TryGetValue(PrimaryId, out var boss))
        {
            var origin = QuantizedDungeonPosition(boss.Position);
            var direction = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
            domaGunsawTarget = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).OrderBy(a => a.PartySlot)
                .Where(a =>
                {
                    var delta = new Vector2(a.Position.X - origin.X, a.Position.Z - origin.Z);
                    var forward = Vector2.Dot(delta, direction);
                    var side = delta.X * direction.Y - delta.Y * direction.X;
                    return forward >= 0 && forward <= 60.9f && MathF.Abs(side) <= .02f;
                }).Select(a => a.Id).FirstOrDefault();
        }
        if (action == 8363 && ++domaGunsawHits >= 4) { domaGunsawTarget = 0; domaGunsawHits = 0; }
    }

    private void SynchronizeDoma()
    {
        if (territory != DomaCatalog.TERRITORY) return;
        foreach (var source in domaChains.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) domaChains.Remove(source);
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1BD2 or 0x1BD6))
        {
            if (domaTetherEvents.TryGetValue(actor.Id, out var observed) && _now < observed.AddSeconds(.5)) continue;
            if (actor.Tethers?.ContainsKey(60) == true)
            {
                if (!domaChains.ContainsKey(actor.Id)) DomaTether(actor.Id, 60, true, _now);
            }
            else domaChains.Remove(actor.Id);
        }
        foreach (var player in _actors.Values.Where(a => a.PartySlot >= 0))
        {
            if (!HasStatus(player, 1253))
            {
                domaPrey.Remove(player.Id);
                // 事件覆盖仅持续短窗口；只有原始快照确认消失后才允许下一轮恢复。
                if (player.Statuses?.Contains(1253u) != true) domaPreyResolved.Remove(player.Id);
            }
            else if (!domaPreyResolved.Contains(player.Id))
            {
                DateTime? expires = player.StatusExpirations?.GetValueOrDefault(1253u);
                if (!domaPrey.ContainsKey(player.Id)) domaPrey[player.Id] = expires;
                else if (expires > domaPrey[player.Id]) domaPrey[player.Id] = expires;
            }
        }
        foreach (var id in domaPrey.Keys.Where(id => !_actors.TryGetValue(id, out var player) || player.Dead
            || domaPrey[id] is { } expiration && expiration <= _now).ToArray()) domaPrey.Remove(id);
    }

    private void DomaRemoved(ulong id)
    {
        domaChains.Remove(id); domaTetherEvents.Remove(id); domaCharges.Remove(id);
        domaStacks.Remove(id); domaSpreads.Remove(id); domaPrey.Remove(id); domaPreyResolved.Remove(id);
        if (domaGunsawTarget == id) domaGunsawTarget = 0;
    }

    private void ResetDoma()
    {
        domaChains.Clear(); domaTetherEvents.Clear(); domaCharges.Clear(); domaStacks.Clear(); domaSpreads.Clear();
        domaPrey.Clear(); domaPreyResolved.Clear(); domaSaw = null; domaSawHits = 0; domaGunsawTarget = 0; domaGunsawHits = 0;
    }
}
