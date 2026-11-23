using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? anyderFirstCross;
    private DateTime? anyderCrossActivation;
    private int anyderCrossRemaining;
    private readonly List<(uint Visual, DungeonDamagePrediction Damage)> anyderLashes = [];
    private readonly Dictionary<ulong, DungeonPendingCast> anyderPunctures = [];
    private readonly HashSet<ulong> anyderCollectSeen = [];
    private readonly HashSet<ulong> anyderCollectOrbs = [];
    private readonly Dictionary<ulong, uint> anyderAnimations = [];
    private readonly HashSet<ulong> anyderGrowingOrbs = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> anyderGrowth = [];
    private int anyderGrowthHits;
    private IGeometryShape? anyderBlossomDanger;
    private bool IsAnyderSharks => territory == AnyderCatalog.TERRITORY && Active?.BossOid == 0x27AB;
    private static bool AnyderSharkAlive(DungeonActor a) => !a.Dead && a.Oid is 0x27AB or 0x27AC;

    private void AnyderSelectPrimary()
    {
        if (!IsAnyderSharks) return;
        if (!_actors.Values.Any(AnyderSharkAlive)) { ResetFight(); return; }
        if (!_actors.TryGetValue(PrimaryId, out var primary) || !AnyderSharkAlive(primary))
            PrimaryId = _actors.Values.First(AnyderSharkAlive).Id;
    }

    private void SynchronizeAnyder()
    {
        if (territory != AnyderCatalog.TERRITORY) return;
        if (Active?.BossOid != 0x28DA) return;
        foreach (var orb in _actors.Values.Where(a => a.Oid == 0x28DB && !a.Dead))
            if (anyderCollectSeen.Add(orb.Id)) anyderCollectOrbs.Add(orb.Id);
        foreach (var id in anyderCollectOrbs.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
            anyderCollectOrbs.Remove(id);
    }

    private void AnyderCast(DungeonPendingCast cast)
    {
        if (territory != AnyderCatalog.TERRITORY) return;
        if (cast.Rule.Action == 15876)
            anyderPunctures[cast.Source] = cast with
            {
                TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId,
            };
        if (Active?.BossOid != 0x249E) return;
        if (cast.Rule.Action != 15888) return;
        anyderFirstCross = cast;
        anyderCrossActivation = cast.FinishAt;
        anyderCrossRemaining = 5;
    }

    private void AnyderFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != AnyderCatalog.TERRITORY) return;
        if (!completed)
        {
            anyderPunctures.Remove(cast.Source);
            if (cast.Rule.Action == 15888) { anyderFirstCross = null; anyderCrossActivation = null; anyderCrossRemaining = 0; }
        }
        if (completed && cast.Rule.Action == 15903)
        {
            anyderGrowthHits = 0;
            RefreshAnyderGrowth(_now);
        }
    }

    private void AnyderEffect(ulong source, uint action, ulong target, DateTime time)
    {
        if (territory != AnyderCatalog.TERRITORY) return;
        if (action == 15876) anyderPunctures.Remove(source);
        if (Active?.BossOid == 0x249E)
        {
            if (action is 15888 or 15889)
            {
                anyderFirstCross = null;
                anyderCrossRemaining = Math.Max(0, anyderCrossRemaining - 1);
            }
            if (action == 15890) anyderCrossActivation = null;
            if (action is 15892 or 15890)
            {
                var targetId = target != source && target != 0 ? target : _actors.GetValueOrDefault(source)?.TargetId ?? 0;
                if (_actors.TryGetValue(targetId, out var victim) && victim.PartySlot >= 0 && !victim.Dead)
                    anyderLashes.Add((action, new(victim.Id, victim.PartySlot, victim.PartyRole, victim.Name, time.AddSeconds(3.4), "Tankbuster")));
            }
            if (action == 15894)
            {
                foreach (var visual in new uint[] { 15892, 15890 })
                {
                    var index = anyderLashes.FindIndex(l => l.Visual == visual && l.Damage.TargetId == target);
                    if (index >= 0) anyderLashes.RemoveAt(index);
                }
            }
        }
        if (action is 15901 or 17170) anyderCollectOrbs.Remove(source);
        if (action == 15904) { ++anyderGrowthHits; RefreshAnyderGrowth(time); }
    }

    private void AnyderAnimation(ulong source, uint state, DateTime time)
    {
        if (territory != AnyderCatalog.TERRITORY || !_actors.TryGetValue(source, out var actor)
            || anyderAnimations.GetValueOrDefault(source, uint.MaxValue) == state) return;
        anyderAnimations[source] = state;
        if (Active?.BossOid == 0x249E && actor.Oid == 0x1EA1A1)
            anyderBlossomDanger = state switch
            {
                0x00100020 => AnyderArenas.MorbolYellowDanger(actor.Position.Y),
                0x00010002 => AnyderArenas.MorbolBlueDanger(actor.Position.Y),
                _ => null,
            };
        if (Active?.BossOid != 0x28DA || actor.Oid != 0x1EAB51) return;
        if (state == 0x00100020) anyderGrowingOrbs.Add(source);
        else if (state == 0x00010002) anyderGrowingOrbs.Remove(source);
        RefreshAnyderGrowth(time);
    }

    private void RefreshAnyderGrowth(DateTime time)
    {
        anyderGrowth.Clear();
        // Source grows once per four helper effects, capped at twelve yalms.
        var radius = Math.Clamp(1.5f + anyderGrowthHits / 4, 0, 12);
        foreach (var id in anyderGrowingOrbs)
            if (_actors.TryGetValue(id, out var orb))
                anyderGrowth[id] = new($"anyder/growth/{id}", "膨胀雷球", new CircleTemplate(radius),
                    QuantizedDungeonPosition(orb.Position), Activation: time.AddSeconds(1.1));
    }

    private IEnumerable<DungeonDamagePrediction> AnyderDamage()
    {
        foreach (var lash in anyderLashes)
            if (_actors.TryGetValue(lash.Damage.TargetId, out var a) && !a.Dead) yield return lash.Damage;
        foreach (var cast in anyderPunctures.Values)
            if (_actors.TryGetValue(cast.TargetId, out var a) && !a.Dead && a.PartySlot >= 0)
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, cast.FinishAt, "Tankbuster");
    }

    private void AnyderRemoved(ulong source)
    {
        anyderCollectOrbs.Remove(source);
        anyderGrowingOrbs.Remove(source);
        anyderGrowth.Remove(source);
        anyderPunctures.Remove(source);
        anyderLashes.RemoveAll(l => l.Damage.TargetId == source);
    }

    private void ResetAnyder()
    {
        anyderFirstCross = null; anyderCrossActivation = null; anyderCrossRemaining = 0;
        anyderLashes.Clear(); anyderPunctures.Clear(); anyderCollectSeen.Clear(); anyderCollectOrbs.Clear();
        anyderAnimations.Clear(); anyderGrowingOrbs.Clear(); anyderGrowth.Clear(); anyderGrowthHits = 0;
        anyderBlossomDanger = null;
    }
}
