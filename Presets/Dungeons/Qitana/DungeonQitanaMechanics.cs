using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonAmbientAoe> qitanaStatues = [];
    private readonly Dictionary<ulong, DateTime> qitanaStatueEvents = [];
    private readonly Dictionary<ulong, DateTime> qitanaBirths = [];
    private readonly HashSet<ulong> qitanaSeenTowers = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> qitanaPreliminaryTowers = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> qitanaTowers = [];
    private readonly Dictionary<ulong, (uint State, DateTime Time, bool Packet)> qitanaAnimations = [];
    private readonly Dictionary<(ulong Source, ulong Target), (uint Id, DateTime Activation)> qitanaHounds = [];
    private readonly HashSet<ulong> qitanaHoundEvents = [];
    private readonly Dictionary<ulong, DateTime> qitanaHoundActivations = [];
    private readonly List<DungeonAmbientAoe> qitanaPoison = [];
    private DungeonAmbientAoe? qitanaLight;

    private void QitanaCast(DungeonPendingCast cast)
    {
        if (territory != QitanaCatalog.TERRITORY || cast.Rule.Action != 15512) return;
        var caster = _actors[cast.Source];
        var preliminary = qitanaPreliminaryTowers.FirstOrDefault(t => t.Value.Position == caster.Position);
        if (preliminary.Value != null) qitanaPreliminaryTowers.Remove(preliminary.Key);
        qitanaTowers[cast.Source] = new($"qitana/tower/cast/{cast.Source}", "落柱精确判定", new FanTemplate(15, MathF.PI / 6),
            cast.Location, cast.Rotation, Activation: cast.FinishAt);
    }

    private void QitanaFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != QitanaCatalog.TERRITORY || cast.Rule.Action != 15512) return;
        if (completed) { qitanaTowers.Clear(); qitanaPreliminaryTowers.Clear(); }
        else qitanaTowers.Remove(cast.Source);
    }

    private void QitanaCreated(ulong source, DateTime time)
    {
        if (territory != QitanaCatalog.TERRITORY) return;
        qitanaBirths.TryAdd(source, time);
        if (qitanaPreliminaryTowers.TryGetValue(source, out var aoe))
            qitanaPreliminaryTowers[source] = aoe with { Activation = qitanaBirths[source].AddSeconds(12) };
    }

    private void QitanaAnimation(ulong source, uint state, DateTime time, bool packet)
    {
        if (Active?.BossOid != 0x27AF || !_actors.TryGetValue(source, out var actor)) return;
        if (qitanaAnimations.TryGetValue(source, out var old)
            && (!packet && old.State == state || packet && old.Packet && old.State == state && time <= old.Time)) return;
        qitanaAnimations[source] = (state, time, packet);
        if (state != 0x00040008) return;
        qitanaLight = new("qitana/light", "隆卡之光", RayRectangleTemplate.Symmetric(40, 60),
            new(0, actor.Position.Y, 315), actor.Position.X == 10 ? MathF.PI / 2 : -MathF.PI / 2, Activation: time.AddSeconds(8));
    }

    private void QitanaTether(ulong source, ulong target, uint id, bool present, DateTime time, bool packet = true)
    {
        if (territory != QitanaCatalog.TERRITORY || !_actors.TryGetValue(source, out var actor)) return;
        if (Active?.BossOid == 0x2A40 && id == 37)
        {
            if (packet)
            {
                if (qitanaStatueEvents.TryGetValue(source, out var previous) && time <= previous) return;
                qitanaStatueEvents[source] = time;
            }
            if (!present) { qitanaStatues.Remove(source); return; }
            if (target != PrimaryId) return;
            var length = 35f;
            (Vector2 Position, float Length)[] map =
            [
                (new(-17, 627), 22), (new(17, 642), 22), (new(-17, 436), 22), (new(17, 421), 22),
                (new(-17, 642), 12), (new(17, 627), 12), (new(17, 436), 12), (new(-17, 421), 12),
            ];
            foreach (var item in map)
                if (Vector2.DistanceSquared(new(actor.Position.X, actor.Position.Z), item.Position) <= 1) { length = item.Length; break; }
            // A packet corrects the clock if its world frame was already restored.
            qitanaStatues[source] = new($"qitana/statue/{source}", "石像连续光束", RayRectangleTemplate.Symmetric(8, length),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: time.AddSeconds(6));
        }
        if (Active?.BossOid != 0x27B1 || id is not (57 or 1) || !_actors.TryGetValue(target, out var other)) return;
        if (packet) { qitanaHoundEvents.Add(source); qitanaHoundEvents.Add(target); }
        var player = actor.PartySlot >= 0 ? actor : other;
        var enemy = player.Id == actor.Id ? other : actor;
        if (player.PartySlot < 0) return;
        var key = (enemy.Id, player.Id);
        if (!present) { qitanaHounds.Remove(key); return; }
        if (!qitanaHoundActivations.TryGetValue(player.Id, out var activation) || activation.AddSeconds(1) <= time)
            qitanaHoundActivations[player.Id] = activation = time.AddSeconds(5.2);
        else if (time.AddSeconds(5.2) < activation)
            qitanaHoundActivations[player.Id] = activation = time.AddSeconds(5.2);
        qitanaHounds[key] = (id, activation);
    }

    private void QitanaEffect(ulong source, uint action, ulong target, Vector3? location, DateTime time)
    {
        if (territory != QitanaCatalog.TERRITORY) return;
        if (action is 15916 or 15917 or 15918) qitanaStatues.Clear();
        if (action is 15500 or 15725) qitanaLight = null;
        if (action is 17079 or 17080)
        {
            foreach (var key in qitanaHounds.Keys.Where(k => k.Source == source && (target == 0 || k.Target == target)).ToArray())
            {
                qitanaHoundEvents.Add(key.Source); qitanaHoundEvents.Add(key.Target);
                qitanaHounds.Remove(key);
            }
            qitanaHoundEvents.Add(source);
        }
        if (action == 15518 && (location ?? _actors.GetValueOrDefault(target)?.Position) is { } poison)
            qitanaPoison.Add(new($"qitana/poison/event/{source}/{time.Ticks}", "毒池预告", new CircleTemplate(6),
                poison, Activation: time.AddSeconds(.8)));
    }

    private void SynchronizeQitana()
    {
        if (territory != QitanaCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2A40)
            foreach (var actor in _actors.Values.Where(a => !a.Dead && !qitanaStatueEvents.ContainsKey(a.Id)))
            {
                if (qitanaStatues.ContainsKey(actor.Id)) continue;
                if (actor.Tethers?.GetValueOrDefault(37u) is > 0 and var target)
                    QitanaTether(actor.Id, target, 37, true, _now, false);
            }
        if (Active?.BossOid == 0x27B0)
        {
            foreach (var id in qitanaPreliminaryTowers.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
                qitanaPreliminaryTowers.Remove(id);
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1EAACD && !a.Dead && qitanaSeenTowers.Add(a.Id)))
                qitanaPreliminaryTowers[actor.Id] = new($"qitana/tower/preliminary/{actor.Id}", "落柱预告",
                    new FanTemplate(15, MathF.PI / 6), actor.Position, actor.Rotation, Activation: qitanaBirths.GetValueOrDefault(actor.Id, _now).AddSeconds(12));
        }
        if (Active?.BossOid != 0x27B1) return;
        foreach (var actor in _actors.Values.Where(a => !a.Dead && !qitanaHoundEvents.Contains(a.Id) && a.Tethers != null))
            foreach (var tether in actor.Tethers!.Where(t => t.Key is 1 or 57))
                if (!qitanaHounds.Keys.Any(k => k.Source == actor.Id || k.Target == actor.Id))
                    QitanaTether(actor.Id, tether.Value, tether.Key, true, _now, false);
        foreach (var key in qitanaHounds.Keys.Where(k => !_actors.TryGetValue(k.Source, out var enemy) || enemy.Dead
            || !_actors.TryGetValue(k.Target, out var player) || player.Dead).ToArray()) qitanaHounds.Remove(key);
        var zones = _actors.Values.Where(a => a.Oid == 0x1E972C).ToArray();
        qitanaPoison.RemoveAll(p => zones.Any(z => Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(z.Position.X, z.Position.Z)) <= 4));
    }

    private void QitanaRemoved(ulong source)
    {
        qitanaStatues.Remove(source); qitanaTowers.Remove(source); qitanaPreliminaryTowers.Remove(source);
        foreach (var key in qitanaHounds.Keys.Where(k => k.Source == source || k.Target == source).ToArray()) qitanaHounds.Remove(key);
    }

    private void ResetQitana()
    {
        qitanaStatues.Clear(); qitanaStatueEvents.Clear(); qitanaBirths.Clear(); qitanaSeenTowers.Clear();
        qitanaPreliminaryTowers.Clear(); qitanaTowers.Clear(); qitanaAnimations.Clear();
        qitanaHounds.Clear(); qitanaHoundEvents.Clear(); qitanaHoundActivations.Clear(); qitanaPoison.Clear(); qitanaLight = null;
    }
}
