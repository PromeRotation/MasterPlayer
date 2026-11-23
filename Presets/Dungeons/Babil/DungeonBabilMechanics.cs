using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<ulong> babilPositive = [];
    private readonly HashSet<ulong> babilNegative = [];
    private bool babilBossPositive;
    private bool babilMagnetRectangle;
    private DateTime? babilMagnetAt;
    private DungeonAmbientAoe? babilRing;
    private bool babilSmall;
    private int babilFloorMode;
    private bool babilAvoidFloors;
    private bool babilLower;
    private DungeonAmbientAoe? babilPain;
    private int babilPainHits;
    private readonly Dictionary<ulong, DungeonActor> babilChaseTargets = [];
    private DateTime babilChaseAt;
    private readonly List<BabilChaser> babilChasers = [];
    private readonly Dictionary<(ulong Source, ulong Target), (uint Id, DateTime Activation)> babilTethers = [];
    private readonly Dictionary<ulong, DateTime> babilTetherClocks = [];
    private readonly Dictionary<(ulong Source, ulong Target), DateTime> babilTetherEvents = [];
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> babilSeenCasts = [];
    private readonly Dictionary<ulong, long> babilClawOrder = [];
    private long babilClawSerial;
    private bool IsBabilAnima => territory == BabilCatalog.TERRITORY && Active?.BossOid == 0x33FD;

    public bool WantsBabilKnockbackImmunity => IsBabilAnima && _actors.TryGetValue(PlayerId, out var player)
        && babilTethers.Any(t => t.Key.Target == PlayerId && t.Value.Activation.AddSeconds(-6) <= _now
            && _now < t.Value.Activation.AddSeconds(1) && !KnockbackImmune(player, t.Value.Activation));

    private void SynchronizeBabil()
    {
        if (territory != BabilCatalog.TERRITORY) return;
        foreach (var id in babilChaseTargets.Keys.ToArray())
            if (_actors.TryGetValue(id, out var actor)) babilChaseTargets[id] = actor;
        for (var i = babilChasers.Count - 1; i >= 0; --i)
        {
            var chase = babilChasers[i];
            if (_actors.TryGetValue(chase.Target.Id, out var target)) babilChasers[i] = chase with { Target = target };
            if (chase.Remaining < 5 && (!_actors.TryGetValue(chase.Target.Id, out target) || target.Dead)) babilChasers.RemoveAt(i);
        }
        if (!IsBabilAnima) return;
        var observed = new HashSet<(ulong Source, ulong Target)>();
        foreach (var source in _actors.Values)
        {
            if (source.Tethers == null) continue;
            foreach (var (id, target) in source.Tethers.Where(t => t.Key is 17 or 57))
            {
                var pair = source.PartySlot >= 0 ? (Source: target, Target: source.Id) : (Source: source.Id, Target: target);
                observed.Add(pair);
                if (!babilTetherEvents.TryGetValue(pair, out var changed) || _now >= changed.AddSeconds(.5))
                    if (!babilTethers.TryGetValue(pair, out var old) || old.Id != id) BabilTether(source.Id, target, id, true, _now, false);
            }
        }
        foreach (var pair in babilTethers.Keys.Where(p => !observed.Contains(p)
            && (!babilTetherEvents.TryGetValue(p, out var changed) || _now >= changed.AddSeconds(.5))).ToArray())
            babilTethers.Remove(pair);
        foreach (var id in babilTetherClocks.Where(p => p.Value.AddSeconds(1) <= _now).Select(p => p.Key).ToArray())
            babilTetherClocks.Remove(id);
    }

    private void BabilCast(DungeonPendingCast cast)
    {
        if (territory != BabilCatalog.TERRITORY) return;
        switch (cast.Rule.Action)
        {
            case 25357:
                if (!babilClawOrder.ContainsKey(cast.Source)) babilClawOrder[cast.Source] = ++babilClawSerial;
                break;
            case 25326: case 25328: babilBossPositive = true; break;
            case 25157: case 25158: babilBossPositive = false; break;
            case 25327: babilMagnetRectangle = true; break;
            case 25329: babilMagnetRectangle = false; break;
            case 25338 when babilFloorMode != 0: babilAvoidFloors = true; break;
            case 25359:
                // Phase transition must also release icons that never received a first cast.
                babilChasers.Clear(); babilChaseTargets.Clear(); babilChaseAt = default;
                break;
            case 25358 when babilSeenCasts.Add((cast.Source, cast.Rule.Action, cast.StartedAt)):
                var target = babilChaseTargets.Values.MinBy(a => Vector3.DistanceSquared(a.Position, cast.Location));
                if (target != null)
                {
                    babilChaseTargets.Remove(target.Id);
                    babilChasers.Add(new(cast.Source, target, cast.Location, 0, 5, cast.FinishAt));
                }
                break;
        }
    }

    private void BabilFinish(DungeonPendingCast cast)
    {
        if (territory != BabilCatalog.TERRITORY) return;
        if (cast.Rule.Action == 25357) babilClawOrder.Remove(cast.Source);
        if (Active?.BossOid == 0x33F7 && cast.Rule.Action is 25159 or 25322 && !babilSmall && Arena != null)
            babilRing = new("babil/ring", "场地即将缩小", new DonutTemplate(15, 19.5f), Arena.Position, Activation: cast.FinishAt.AddSeconds(6.1));
        if (cast.Rule.Action == 25333) babilFloorMode = 1;
        if (cast.Rule.Action == 25331) babilFloorMode = 2;
    }

    private void BabilIcon(ulong target, uint icon, DateTime time)
    {
        if (territory != BabilCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x33F7 && icon is 162 or 163)
        {
            if (icon == 162) babilPositive.Add(target); else babilNegative.Add(target);
            babilMagnetAt = time.AddSeconds(8.1);
        }
        if (IsBabilAnima && icon == 197 && _actors.TryGetValue(target, out var actor))
        {
            babilChaseTargets[target] = actor; babilChaseAt = time.AddSeconds(5.1);
        }
    }

    private void BabilMap(uint index, uint state)
    {
        if (territory != BabilCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x33F7 && index == 0 && state == 0x00020001)
        {
            babilSmall = true; babilRing = null; Arena = BabilArenas.BarnabasSmall(Arena.Position.Y);
        }
        if (Active?.BossOid == 0x33FA && index is 1 or 2 && state == 0x00080004)
        {
            babilFloorMode = 0; babilAvoidFloors = false;
        }
        if (IsBabilAnima && index == 3 && state is 0x00020001 or 0x00080004)
        {
            babilLower = state == 0x00020001; Arena = BabilCatalog.AnimaArena(Arena.Position.Y, babilLower);
        }
    }

    private void BabilTether(ulong source, ulong target, uint id, bool present, DateTime time, bool fromEvent = true)
    {
        if (!IsBabilAnima || id is not (17 or 57) || !_actors.TryGetValue(source, out var actor)) return;
        var pair = actor.PartySlot >= 0 ? (Source: target, Target: source) : (Source: source, Target: target);
        if (fromEvent) babilTetherEvents[pair] = time;
        if (!present)
        {
            if (babilTethers.TryGetValue(pair, out var old) && old.Id == id) babilTethers.Remove(pair);
            return;
        }
        if (!babilTetherClocks.TryGetValue(pair.Target, out var activation) || activation.AddSeconds(1) <= time)
            babilTetherClocks[pair.Target] = activation = time.AddSeconds(7.9);
        babilTethers[pair] = (id, activation);
    }

    private Vector3 BabilChasePosition(BabilChaser chase)
    {
        var delta = chase.Target.Position - chase.Previous; delta.Y = 0;
        return QuantizedDungeonPosition(chase.Previous + delta * Math.Min(1, chase.MoveDistance / Math.Max(.001f, delta.Length())));
    }

    private void BabilEffect(ulong source, uint action, ulong target, Vector3? location, DateTime time)
    {
        if (territory != BabilCatalog.TERRITORY) return;
        // Fix the duplicated 25054 case upstream: both circular resolve actions release magnetism.
        if (action is 24693 or 24694 or 25053 or 25054)
        {
            babilPositive.Clear(); babilNegative.Clear(); babilMagnetAt = null;
        }
        if (!IsBabilAnima || Arena == null) return;
        if (action == 26229) babilPain = new("babil/pain", "无尽痛苦持续扩散", new CircleTemplate(18), Arena.Position);
        if (action is 25348 or 25349 && ++babilPainHits == 20) { babilPain = null; babilPainHits = 0; }
        if (action is not (25358 or 21239) || babilChasers.Count == 0) return;
        var position = target == source && _actors.TryGetValue(source, out var caster) ? QuantizedDungeonPosition(caster.Position)
            : _actors.TryGetValue(target, out var victim) ? victim.Position : location;
        if (position == null) return;
        var index = babilChasers.Select((c, i) => (Distance: Vector3.DistanceSquared(BabilChasePosition(c), position.Value), Index: i)).MinBy(c => c.Distance).Index;
        var chase = babilChasers[index];
        if (chase.Remaining == 1) babilChasers.RemoveAt(index);
        else babilChasers[index] = chase with { Previous = position.Value, MoveDistance = 6, Remaining = chase.Remaining - 1, Activation = time.AddSeconds(1) };
        if (babilChasers.Count == 0) babilChaseTargets.Clear();
    }

    private void ResetBabil()
    {
        babilPositive.Clear(); babilNegative.Clear(); babilBossPositive = false; babilMagnetRectangle = false; babilMagnetAt = null;
        babilRing = null; babilSmall = false; babilFloorMode = 0; babilAvoidFloors = false; babilLower = false;
        babilPain = null; babilPainHits = 0; babilChaseTargets.Clear(); babilChasers.Clear(); babilChaseAt = default;
        babilTethers.Clear(); babilTetherClocks.Clear(); babilTetherEvents.Clear(); babilSeenCasts.Clear();
        babilClawOrder.Clear(); babilClawSerial = 0;
    }

    private void BabilRemoved(ulong source)
    {
        if (_casts.TryGetValue(source, out var cast)) BabilFinish(cast);
        foreach (var pair in babilTethers.Keys.Where(p => p.Source == source || p.Target == source).ToArray())
            babilTethers.Remove(pair);
        babilTetherClocks.Remove(source);
    }
}
