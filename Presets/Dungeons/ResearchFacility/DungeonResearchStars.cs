using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<ulong> researchSeenStars = [];
    private readonly Dictionary<ulong, DateTime> researchBirths = [];
    private readonly List<ulong> researchUnfusedStars = [];
    private readonly List<DungeonPendingCast> researchStars = [];
    private readonly HashSet<(ulong, ulong)> researchStarPairs = [];
    private bool researchTutorialFire;
    private bool researchTutorialIce;
    private readonly Dictionary<ulong, DateTime> researchDonuts = [];
    private readonly Dictionary<ulong, DateTime> researchSpreads = [];
    private readonly Dictionary<ulong, (ulong Target, uint Action, DateTime Activation)> researchLineStacks = [];
    private readonly Dictionary<(uint Action, ulong Target), DateTime> researchResolvedMarkers = [];
    private readonly Dictionary<(ulong Source, uint Action), DateTime> researchStarResolutions = [];

    private void ResearchObjectCreated(ulong source, DateTime timestamp)
    {
        if (Active?.BossOid is not (0x3DA4 or 0x3DA7)) return;
        researchBirths.TryAdd(source, timestamp);
        for (var i = 0; i < researchStars.Count; ++i)
        {
            var cast = researchStars[i];
            if (cast.Source == source && cast.Rule.Name != "FusedStar" && timestamp < cast.StartedAt)
                researchStars[i] = cast with { StartedAt = timestamp, FinishAt = timestamp + (cast.FinishAt - cast.StartedAt) };
        }
    }

    private void SynchronizeResearchStars()
    {
        if (Active?.BossOid is not (0x3DA4 or 0x3DA7)) return;
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Oid is 0x3DA5 or 0x3DA6 or 0x3DA8 or 0x3DA9))
        {
            if (!researchSeenStars.Add(actor.Id)) continue;
            var birth = researchBirths.GetValueOrDefault(actor.Id, _now);
            if (Active.BossOid == 0x3DA7)
            {
                if (actor.Oid != 0x3DA8 || actor.Position.X != 230 || actor.Position.Z is not (86 or 92)) continue;
                var iceFirst = actor.Position.Z == 86;
                var entries = new[]
                {
                    (new Vector3(241, actor.Position.Y, 79), false),
                    (new Vector3(219, actor.Position.Y, 79), false),
                    (new Vector3(230, actor.Position.Y, 79), true),
                };
                var predictions = entries.Select(e => new DungeonPendingCast(actor.Id,
                    new(e.Item2 ? 33021u : 33022u, 0x233C, "Dualstar", e.Item2 ? new DonutTemplate(5, 40) : new CircleTemplate(16)),
                    QuantizedDungeonPosition(e.Item1), 0, birth, birth.AddSeconds(e.Item2 == iceFirst ? 11.8 : 14.8))).ToArray();
                if (iceFirst) Array.Reverse(predictions);
                researchStars.AddRange(predictions);
                continue;
            }
            researchUnfusedStars.Add(actor.Id);
            var ice = actor.Oid == 0x3DA5;
            var count = researchUnfusedStars.Count(id => _actors.GetValueOrDefault(id)?.Oid == actor.Oid);
            if (ice ? researchTutorialIce || count != 4 : researchTutorialFire || count != 5) continue;
            if (ice) researchTutorialIce = true;
            else researchTutorialFire = true;
            foreach (var id in researchUnfusedStars)
                if (_actors.TryGetValue(id, out var star))
                    researchStars.Add(new(actor.Id, new(ice ? 31879u : 31887u, star.Oid, "TutorialStar",
                        ice ? new DonutTemplate(5, 15) : new CircleTemplate(8)), star.Position, 0, birth, birth.AddSeconds(7.8)));
            researchUnfusedStars.Clear();
        }
        if (Active.BossOid == 0x3DA4)
            foreach (var actor in _actors.Values.Where(a => a.Oid is 0x3DA5 or 0x3DA6 && !a.Dead))
                if ((actor.Tethers?.GetValueOrDefault(110u) ?? 0) is var target && target != 0)
                    ResearchStarTether(actor.Id, target, 110, true, _now);
        foreach (var key in researchDonuts.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) researchDonuts.Remove(key);
        foreach (var key in researchSpreads.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) researchSpreads.Remove(key);
    }

    private void ResearchStarTether(ulong source, ulong target, uint tether, bool present, DateTime timestamp)
    {
        if (Active?.BossOid != 0x3DA4 || tether != 110 || !present || !_actors.TryGetValue(source, out var a)
            || !_actors.TryGetValue(target, out var b) || a.Oid is not (0x3DA5 or 0x3DA6)) return;
        var pair = source < target ? (source, target) : (target, source);
        if (!researchStarPairs.Add(pair))
        {
            for (var i = 0; i < researchStars.Count; ++i)
            {
                var cast = researchStars[i];
                if ((cast.Source == source || cast.Source == target) && cast.Rule.Name == "FusedStar" && timestamp < cast.StartedAt)
                    researchStars[i] = cast with { StartedAt = timestamp, FinishAt = timestamp.AddSeconds(10.6) };
            }
            return;
        }
        researchUnfusedStars.Remove(source); researchUnfusedStars.Remove(target);
        var ice = a.Oid == 0x3DA5;
        var midpoint = QuantizedDungeonPosition((a.Position + b.Position) * .5f);
        researchStars.Add(new(source, new(ice ? 33019u : 33020u, a.Oid, "FusedStar",
            ice ? new DonutTemplate(5, 40) : new CircleTemplate(16)), midpoint, 0, timestamp, timestamp.AddSeconds(10.6)));
        if (!researchStars.Any(c => c.Rule.Shape is DonutTemplate { OuterRadius: 40 })
            && researchStars.Count(c => c.Rule.Shape is CircleTemplate { Radius: 16 }) != 2) return;
        foreach (var id in researchUnfusedStars)
            if (_actors.TryGetValue(id, out var star))
                researchStars.Add(new(source, new(ice ? 31879u : 31887u, star.Oid, "FusedStar",
                    ice ? new DonutTemplate(5, 15) : new CircleTemplate(8)), star.Position, 0, timestamp, timestamp.AddSeconds(10.6)));
        researchUnfusedStars.Clear();
    }

    private IEnumerable<KeyValuePair<string, DungeonPendingCast>> ResearchStarOutputs()
    {
        var deadline = researchStars.Count > 0 ? researchStars[0].FinishAt.AddSeconds(1) : DateTime.MaxValue;
        for (var i = 0; i < researchStars.Count; ++i)
        {
            if (Active?.BossOid == 0x3DA7 && researchStars[i].FinishAt >= deadline) break;
            yield return new($"research/star/{i}", researchStars[i]);
        }
    }

    private void ResearchStarFinish(DungeonPendingCast cast, bool completed)
    {
        if (completed && !cast.EffectSeen)
            ResearchResolveStar(cast.Source, cast.Rule.Action, cast.FinishAt);
    }

    private void ResearchResolveStar(ulong source, uint action, DateTime timestamp)
    {
        if (Active?.BossOid != 0x3DA7 || action is not (33021 or 33022)) return;
        var key = (source, action);
        if (researchStarResolutions.TryGetValue(key, out var resolved) && timestamp <= resolved.AddSeconds(.6)) return;
        var index = researchStars.FindIndex(c => c.Rule.Action == action);
        if (index < 0) return;
        researchStars.RemoveAt(index);
        researchStarResolutions[key] = timestamp;
    }

    private bool ResearchMarkerResolved(uint action, ulong target, DateTime timestamp)
        => researchResolvedMarkers.TryGetValue((action, target), out var resolved) && timestamp <= resolved
            || researchResolvedMarkers.TryGetValue((action, 0), out resolved) && timestamp <= resolved;

    private void TickResearch(DateTime now)
    {
        // Match the cast recovery grace: absent resolve packets must not leave permanent movement goals.
        researchStars.RemoveAll(c => now >= c.FinishAt.AddSeconds(2));
        foreach (var key in researchDonuts.Where(p => now >= p.Value.AddSeconds(2)).Select(p => p.Key).ToArray()) researchDonuts.Remove(key);
        foreach (var key in researchSpreads.Where(p => now >= p.Value.AddSeconds(2)).Select(p => p.Key).ToArray()) researchSpreads.Remove(key);
        foreach (var key in researchStacks.Where(p => now >= p.Value.Activation.AddSeconds(2)).Select(p => p.Key).ToArray()) researchStacks.Remove(key);
        foreach (var key in researchLineStacks.Where(p => now >= p.Value.Activation.AddSeconds(2)
            || !_actors.TryGetValue(p.Key, out var source) || source.Dead
            || !_actors.TryGetValue(p.Value.Target, out var target) || target.Dead).Select(p => p.Key).ToArray()) researchLineStacks.Remove(key);
    }

    private void ResearchLateIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (Active?.BossOid != 0x3DA7) return;
        var action = icon switch { 384 => 31901u, 139 => 31903u, 161 => 31904u, _ => 0u };
        if (ResearchMarkerResolved(action, target, timestamp)) return;
        switch (icon)
        {
            case 384: researchDonuts[target] = timestamp.AddSeconds(8); break;
            case 139: researchSpreads[target] = timestamp.AddSeconds(5); break;
            case 161: researchStacks[(target, 31904)] = (6, 4, timestamp.AddSeconds(5)); break;
        }
    }

    private void ResearchLateEffect(ulong source, uint action, ulong target, DateTime timestamp)
    {
        ResearchResolveStar(source, action, timestamp);
        if (action is 31901 or 31903 or 31904 or 33029 or 32555)
        {
            var resolutionTarget = action is 31903 or 31904 ? 0 : target;
            var key = (action, resolutionTarget);
            if (!researchResolvedMarkers.TryGetValue(key, out var previous) || previous < timestamp)
                researchResolvedMarkers[key] = timestamp;
        }
        if (Active?.BossOid == 0x3DA4)
        {
            if (action is 31879 or 33019 or 31887 or 31889) researchStars.Clear();
            if (action == 31892 && !ResearchMarkerResolved(33029, target, timestamp))
                researchLineStacks[source] = (target, 33029, timestamp.AddSeconds(5.1));
        }
        if (Active?.BossOid == 0x3DA7)
        {
            if (action == 31901 && !researchDonuts.Remove(target)) researchDonuts.Clear();
            if (action == 31903) researchSpreads.Clear();
            if (action == 31904)
                foreach (var key in researchStacks.Keys.Where(k => k.Action == action).ToArray()) researchStacks.Remove(key);
            if (action == 31907 && !ResearchMarkerResolved(32555, target, timestamp))
                researchLineStacks[source] = (target, 32555, timestamp.AddSeconds(5.2));
        }
        if (action is 33029 or 32555)
        {
            var matching = researchLineStacks.Where(p => p.Value.Action == action && p.Value.Target == target).Select(p => p.Key).FirstOrDefault();
            if (matching != 0) researchLineStacks.Remove(matching);
            else if (researchLineStacks.Count == 1) researchLineStacks.Clear();
        }
    }

    private IEnumerable<DungeonAmbientAoe> ResearchLateAoes()
    {
        foreach (var (targetId, activation) in researchDonuts)
        {
            if (!_actors.TryGetValue(targetId, out var target)) continue;
            yield return new($"research/donut/{targetId}/display", "古代之环", new DonutTemplate(10, 20), target.Position, Navigate: false);
            if (targetId != PlayerId)
                yield return new($"research/donut/{targetId}/goal", "古代之环：集中", new CircleTemplate(2.5f), target.Position,
                    Display: false, Safe: true);
        }
        foreach (var (targetId, activation) in researchSpreads)
        {
            if (!_actors.TryGetValue(targetId, out var target)) continue;
            yield return new($"research/whisper/{targetId}/display", "古代黑暗：分散", new CircleTemplate(6), target.Position,
                Navigate: false, OutlineOnly: true);
            if (targetId != PlayerId)
                yield return new($"research/whisper/{targetId}/avoid", "古代黑暗", new CircleTemplate(6), target.Position, Display: false, Activation: activation);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead && !researchSpreads.ContainsKey(a.Id)))
                    yield return new($"research/whisper/{targetId}/party/{buddy.Id}", "分散远离队友", new CircleTemplate(6), buddy.Position,
                        Display: false, Activation: activation);
        }
        foreach (var (sourceId, line) in researchLineStacks)
        {
            if (!_actors.TryGetValue(sourceId, out var source) || source.Dead || !_actors.TryGetValue(line.Target, out var target) || target.Dead) continue;
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            yield return new($"research/line/{sourceId}/display", "直线分摊", RayRectangleTemplate.Symmetric(8, 50), source.Position,
                rotation, Navigate: false, Safe: true, OutlineOnly: true);
            if (line.Target != PlayerId)
            {
                var marked = researchLineStacks.Values.Any(l => l.Target == PlayerId);
                yield return new($"research/line/{sourceId}/goal", "直线分摊", RayRectangleTemplate.Symmetric(marked ? 16 : 8, 50), source.Position,
                    rotation, Display: false, Safe: !marked, Activation: line.Activation);
            }
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead
                    && !researchLineStacks.Values.Any(l => l.Target == a.Id)))
                    yield return new($"research/line/{sourceId}/buddy/{buddy.Id}", "直线分摊靠近队友", new CircleTemplate(2), buddy.Position,
                        Display: false, Safe: true);
        }
    }
}
