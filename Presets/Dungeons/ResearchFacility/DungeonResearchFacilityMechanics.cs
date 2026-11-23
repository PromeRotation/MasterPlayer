using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, (ulong Target, DateTime Activation)> researchLasers = [];
    private readonly HashSet<ulong> researchLaserEvents = [];
    private readonly Dictionary<ulong, DateTime> researchLaserChanged = [];
    private readonly Dictionary<(ulong Target, uint Action), (float Radius, int Count, DateTime Activation)> researchStacks = [];
    private readonly HashSet<ulong> researchSeenBinds = [];

    private void ResearchIcon(ulong target, uint icon, DateTime timestamp)
    {
        ResearchLateIcon(target, icon, timestamp);
        if (Active?.BossOid == 0xE9A && icon == 93)
            researchStacks[(target, 4336)] = (5, 4, timestamp.AddSeconds(4.1));
    }

    private void ResearchStatus(ulong target, uint status, bool present, DateTime timestamp)
    {
        if (Active?.BossOid != 0xE9A || status != 2518) return;
        if (present)
        {
            researchSeenBinds.Add(target);
            researchStacks[(target, 4335)] = (4, 2, timestamp.AddSeconds(6.2));
        }
        else researchSeenBinds.Remove(target);
    }

    private void ResearchTether(ulong source, ulong target, uint tether, bool present, DateTime timestamp)
    {
        ResearchStarTether(source, target, tether, present, timestamp);
        if (Active?.BossOid != 0xE97 || tether != 17) return;
        if (_actors.TryGetValue(source, out var tetherSource) && tetherSource.PartySlot >= 0) (source, target) = (target, source);
        researchLaserEvents.Add(source);
        researchLaserChanged[source] = _now;
        if (present) researchLasers[source] = (target, timestamp);
        else researchLasers.Remove(source);
    }

    private void ResearchEffect(ulong source, uint action)
    {
        if (Active?.BossOid == 0xE97 && action == 4321)
        {
            researchLasers.Remove(source);
            researchLaserEvents.Add(source);
            researchLaserChanged[source] = _now;
        }
        if (Active?.BossOid == 0xE9A && action is 4335 or 4336)
            foreach (var key in researchStacks.Keys.Where(k => k.Action == action).ToArray()) researchStacks.Remove(key);
    }

    private void SynchronizeResearch()
    {
        SynchronizeResearchStars();
        if (Active?.BossOid == 0xE97)
            foreach (var turret in _actors.Values.Where(a => a.Oid == 0xE98 && !a.Dead))
            {
                if (researchLaserChanged.TryGetValue(turret.Id, out var changed) && _now < changed.AddSeconds(.5)) continue;
                var target = turret.Tethers?.GetValueOrDefault(17u) ?? 0;
                if (target == 0)
                    target = _actors.Values.FirstOrDefault(a => a.PartySlot >= 0 && !a.Dead
                        && a.Tethers?.GetValueOrDefault(17u) == turret.Id)?.Id ?? 0;
                if (target == 0)
                {
                    researchLasers.Remove(turret.Id);
                    researchLaserEvents.Remove(turret.Id);
                }
                else if (!researchLaserEvents.Contains(turret.Id) && !researchLasers.ContainsKey(turret.Id))
                    researchLasers[turret.Id] = (target, _now);
            }
        if (Active?.BossOid == 0xE9A)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 2518)))
                if (researchSeenBinds.Add(actor.Id)) researchStacks[(actor.Id, 4335)] = (4, 2, _now.AddSeconds(6.2));
        foreach (var key in researchStacks.Keys.Where(k => !_actors.TryGetValue(k.Target, out var a) || a.Dead).ToArray()) researchStacks.Remove(key);
        foreach (var id in researchLasers.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) researchLasers.Remove(id);
    }

    private IEnumerable<DungeonAmbientAoe> ResearchAoes()
    {
        foreach (var aoe in ResearchLateAoes()) yield return aoe;
        foreach (var (sourceId, tether) in researchLasers)
        {
            if (!_actors.TryGetValue(sourceId, out var source) || !_actors.TryGetValue(tether.Target, out var target) || target.Dead) continue;
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var shape = RayRectangleTemplate.Symmetric(4, 50);
            yield return new($"research/laser/{sourceId}/display", "激光连线：避开队友", shape, source.Position, rotation,
                Navigate: false, OutlineOnly: target.Id == PlayerId);
            if (target.Id != PlayerId)
                yield return new($"research/laser/{sourceId}/avoid", "激光连线", shape, source.Position, rotation,
                    Display: false, Activation: tether.Activation);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                {
                    var delta = member.Position - source.Position;
                    var angle = MathF.Asin(Math.Clamp(2 / MathF.Max(.001f, new Vector2(delta.X, delta.Z).Length()), 0, 1));
                    yield return new($"research/laser/{sourceId}/party/{member.Id}", "激光避开队友", new FanTemplate(100, angle * 2),
                        source.Position, MathF.Atan2(delta.X, delta.Z), Display: false, Activation: tether.Activation);
                }
        }
        if (!_actors.TryGetValue(PlayerId, out var player)) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var (key, stack) in researchStacks)
        {
            if (!_actors.TryGetValue(key.Target, out var target)) continue;
            var shape = new CircleTemplate(stack.Radius);
            var position = target.Position;
            bool Inside(DungeonActor actor) => Vector2.DistanceSquared(new(actor.Position.X, actor.Position.Z), new(position.X, position.Z)) <= stack.Radius * stack.Radius;
            var count = party.Count(Inside);
            yield return new($"research/stack/{key}/display", $"分摊 {count}/{stack.Count}", shape, position,
                Navigate: false, Safe: true, OutlineOnly: true);
            if (party.Length < 2) continue;
            var isTarget = key.Target == PlayerId;
            var otherStackTarget = researchStacks.Keys.Any(k => k.Target == PlayerId);
            if (isTarget)
            {
                foreach (var buddy in party.Where(a => a.Id != PlayerId && !researchStacks.Keys.Any(k => k.Target == a.Id)))
                    yield return new($"research/stack/{key}/buddy/{buddy.Id}", "靠近可分摊队友", new CircleTemplate(stack.Radius * .5f),
                        buddy.Position, Display: false, Safe: true);
            }
            else if (!otherStackTarget && (Inside(player) ? count <= stack.Count : count < stack.Count))
                yield return new($"research/stack/{key}/goal", "进入分摊", shape, position, Display: false, Safe: true);
            else
                yield return new($"research/stack/{key}/avoid", "分摊已满或避免重叠分摊", new CircleTemplate(otherStackTarget ? stack.Radius * 2 : stack.Radius),
                    position, Display: false, Activation: stack.Activation);
        }
    }
}
