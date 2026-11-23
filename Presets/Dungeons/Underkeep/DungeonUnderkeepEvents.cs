using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int underCloneCount;
    private bool underDirection;
    private bool underSectorActive;
    private ulong underFirstClone;
    private DungeonAmbientAoe? underSector;
    private readonly List<(DungeonActor Source, DungeonActor Target)> underEdges = [];
    private readonly Dictionary<ulong, (byte Model, byte Anim1, byte Anim2)> underModels = [];
    private readonly Dictionary<(ulong Source, uint Id), ulong> underLiveTethers = [];
    private readonly Dictionary<(ulong Source, uint Id), ulong> underPendingTethers = [];
    private readonly Dictionary<(ulong Source, uint Id), (ulong Target, DateTime At, (string Id, DateTime? At)[] Aoes)> underInferredTeleports = [];
    private readonly List<DungeonAmbientAoe> underCrosses = [];
    private bool underTeleported;
    private Vector3 underCenter;
    private readonly List<Vector3> underStartingPositions = [];
    private int underCrossBatches;

    private static bool UnderNear(Vector3 a, Vector3 b) => MathF.Abs(a.X - b.X) <= 1 && MathF.Abs(a.Z - b.Z) <= 1;

    private void UnderObserveSector()
    {
        if (Active?.BossOid != 0x47AD) return;
        for (var i = 0; i < underEdges.Count; ++i)
        {
            var e = underEdges[i];
            e.Source = _actors.GetValueOrDefault(e.Source.Id) ?? e.Source;
            e.Target = _actors.GetValueOrDefault(e.Target.Id) ?? e.Target;
            underEdges[i] = e;
        }
        foreach (var a in _actors.Values.Where(a => a.Oid == 0x47AE))
        {
            var state = (a.ModelState, a.AnimationState1, a.AnimationState2);
            if (underModels.TryGetValue(a.Id, out var old) && old == state) continue;
            underModels[a.Id] = state;
            if (a.ModelState is 5 or 6) underDirection = a.ModelState == 6;
        }
    }

    private void UnderObserveTethers()
    {
        var current = _actors.Values.SelectMany(a => (a.Tethers ?? new Dictionary<uint, ulong>())
            .Where(p => p.Key is 313 or 327 or 282).Select(p => (Key: (Source: a.Id, Id: p.Key), Target: p.Value)))
            .ToDictionary(p => p.Key, p => p.Target);
        // Event callbacks can lead the raw frame. Wait for acknowledgement before
        // synthesizing a subsequent transition from the object snapshot.
        foreach (var (key, desired) in underPendingTethers.ToArray())
        {
            if (!_actors.ContainsKey(key.Source) || current.GetValueOrDefault(key) == desired) underPendingTethers.Remove(key);
            else if (desired == 0) current.Remove(key);
            else current[key] = desired;
        }
        foreach (var (key, target) in underLiveTethers.ToArray())
            if (current.GetValueOrDefault(key) != target) UnderTether(key.Source, target, key.Id, false, _now, true);
        foreach (var (key, target) in current)
            if (underLiveTethers.GetValueOrDefault(key) != target) UnderTether(key.Source, target, key.Id, true, _now, true);
    }

    private void UnderTether(ulong source, ulong target, uint id, bool present, DateTime now, bool fromSnapshot = false)
    {
        if (territory != UnderkeepCatalog.TERRITORY || id is not (313 or 327 or 282)) return;
        var key = (source, id);
        if (!fromSnapshot && present && underInferredTeleports.TryGetValue(key, out var inferred) && inferred.Target == target)
        {
            underInferredTeleports.Remove(key);
            for (var i = 0; i < underCrosses.Count; ++i)
                if (inferred.Aoes.Any(a => a.Id == underCrosses[i].Id && a.At == underCrosses[i].Activation)
                    && underCrosses[i].Activation is { } at)
                    underCrosses[i] = underCrosses[i] with { Activation = at + (now - inferred.At) };
            return;
        }
        if (present && underLiveTethers.GetValueOrDefault(key) is var previous && previous != 0 && previous != target)
            UnderTether(source, previous, id, false, now, true);
        if (!fromSnapshot) underPendingTethers[key] = present ? target : 0;
        if (present)
        {
            if (underLiveTethers.GetValueOrDefault(key) == target)
            {
                return;
            }
            underLiveTethers[key] = target;
        }
        else underLiveTethers.Remove(key);
        if (Active?.BossOid == 0x47AD)
        {
            if (present && id == 313) ++underCloneCount;
            if (present && id == 327 && _actors.TryGetValue(source, out var a) && _actors.TryGetValue(target, out var b)) underEdges.Add((a, b));
            if (!present && id == 327 && underSectorActive)
            {
                var i = underEdges.FindIndex(e => e.Source.Id == source);
                if (i >= 0) underEdges.RemoveAt(i);
            }
        }
        if (!present || Active?.BossOid != 0x478E || id != 282 || _actors.GetValueOrDefault(source) is not { Oid: 0x47B5 } actor) return;
        underTeleported = true;
        if (!_actors.TryGetValue(target, out var orb)) return;
        var index = underCrosses.FindIndex(a => UnderNear(a.Position, orb.Position));
        if (index >= 0) underCrosses[index] = underCrosses[index] with { Position = QuantizedDungeonPosition(actor.Position) };
        UnderSortCrosses();
        double[] timings = underCrossBatches == 1 ? [2.8, 7.9] : [.6, 7.9, 14.4];
        for (var i = 0; i < Math.Min(timings.Length, underCrosses.Count); ++i)
            underCrosses[i] = underCrosses[i] with { Activation = now.AddSeconds(timings[i]) };
        if (fromSnapshot) underInferredTeleports[key] = (target, now, underCrosses.Take(timings.Length).Select(a => (a.Id, a.Activation)).ToArray());
        else underInferredTeleports.Remove(key);
    }

    private void UnderTimeline(ulong source, uint timeline, DateTime now)
    {
        if (territory != UnderkeepCatalog.TERRITORY || Active?.BossOid != 0x478E || !_actors.TryGetValue(source, out var actor)) return;
        if (timeline == 0x11DA && !underTeleported)
        {
            underCrosses.Add(new($"under/cross/{++underSerial}", "EnforcementRay", new DungeonCrossTemplate(36, 4.5f),
                QuantizedDungeonPosition(actor.Position), Activation: DateTime.MinValue));
            if (underCenter.X != 0 || underCenter.Z != 0) UnderUpdateCrosses(now);
        }
        else if (timeline == 0x11D5)
        {
            underStartingPositions.Add(actor.Position);
            if (underStartingPositions.Count == 2)
            {
                underCenter = (underStartingPositions[0] + underStartingPositions[1]) * .5f;
                UnderUpdateCrosses(now);
            }
        }
    }

    private void UnderUpdateCrosses(DateTime now)
    {
        if (underCrosses.Count < 2) return;
        UnderSortCrosses();
        underCrosses[0] = underCrosses[0] with { Activation = now.AddSeconds(underCrossBatches switch { 0 => 8.3, 1 => 11.4, _ => 7.3 }) };
        if (underCrossBatches == 0) underCrosses[1] = underCrosses[1] with { Activation = now.AddSeconds(11.4) };
    }

    private void UnderSortCrosses()
    {
        float Distance(int i) => Vector2.DistanceSquared(new(underCrosses[i].Position.X, underCrosses[i].Position.Z), new(underCenter.X, underCenter.Z));
        void Swap(int i, int j)
        {
            if (Distance(i) > Distance(j)) (underCrosses[i], underCrosses[j]) = (underCrosses[j], underCrosses[i]);
        }
        if (underCrosses.Count < 2) return;
        Swap(0, 1);
        if (underCrosses.Count == 3) { Swap(0, 2); Swap(1, 2); }
    }

    private void UnderRemoved(ulong source, DateTime now)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        foreach (var (key, target) in underLiveTethers.Where(p => p.Key.Source == source).ToArray())
            UnderTether(source, target, key.Id, false, now, true);
        foreach (var key in underPendingTethers.Keys.Where(k => k.Source == source).ToArray()) underPendingTethers.Remove(key);
        if (_casts.TryGetValue(source, out var cast)) UnderFinish(cast);
    }
}
