using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonAmbientAoe> alaLasers = [];
    private int alaLaserHits;
    private readonly List<DungeonAmbientAoe> alaFirePredictions = [];
    private readonly Dictionary<ulong, (ulong Target, Vector3 Position)> alaBodies = [];
    private readonly Dictionary<ulong, (ulong Target, DateTime Activation)> alaSparkBaits = [];
    private readonly Dictionary<(ulong Source, uint Tether), (bool Present, DateTime Time)> alaTetherEvents = [];
    private readonly Dictionary<(ulong Source, ulong Target), DateTime> alaSwordBaits = [];
    private bool alaShrunk;
    private DateTime alaShrinkAt;
    private bool alaTroikaImminent;

    private void AlaMhigoCast(DungeonPendingCast cast)
    {
        if (territory != AlaMhigoCatalog.TERRITORY) return;
        if (cast.Rule.Action is 8265 or 8266)
            alaLasers[cast.Source] = new($"ala/laser/{cast.Source}", "十二次尾部激光", RayRectangleTemplate.Symmetric(10, 20.5f),
                cast.Location, cast.Rotation, Activation: cast.FinishAt.AddSeconds(1));
        if (cast.Rule.Action is 8296 or 9608)
            foreach (var player in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                alaSwordBaits[(cast.Source, player.Id)] = cast.StartedAt.AddSeconds(6.3);
    }

    private void AlaMhigoFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != AlaMhigoCatalog.TERRITORY || completed) return;
        if (cast.Rule.Action is 8265 or 8266)
        {
            alaLasers.Remove(cast.Source);
            if (alaLasers.Count == 0) alaLaserHits = 0;
        }
        if (cast.Rule.Action is 8296 or 9608)
            foreach (var key in alaSwordBaits.Keys.Where(k => k.Source == cast.Source).ToArray()) alaSwordBaits.Remove(key);
    }

    private void AlaMhigoEffect(ulong source, uint action, ulong target, uint sequence, Vector3? location, DateTime time)
    {
        if (territory != AlaMhigoCatalog.TERRITORY) return;
        if (action is 8267 or 8268 && ++alaLaserHits >= 12) { alaLasers.Clear(); alaLaserHits = 0; }
        if (action == 8263)
        {
            var position = _actors.GetValueOrDefault(target)?.Position ?? location;
            if (position is { } p)
                alaFirePredictions.Add(new($"ala/fire/predicted/{source}/{sequence}", "锁定火池预告", new CircleTemplate(5), p, Activation: time.AddSeconds(.7)));
        }
        if (action == 8288) alaTroikaImminent = false;
        if (action is 8297 or 9609) alaSwordBaits.Clear();
        if (action == 8299)
        {
            alaSparkBaits.Remove(source);
            alaTetherEvents[(source, 41)] = (false, time);
        }
    }

    private void AlaMhigoAnimation(ulong source, uint state)
    {
        if (alaShrunk || Active?.BossOid != 0x1BAA || state != 0x00040008 || _actors.GetValueOrDefault(source)?.Oid != 0x1EA1A1) return;
        alaShrunk = true;
        Arena = new CircleGeometry(new(250, Arena!.Position.Y, -353), 20);
    }

    private void AlaMhigoYell(ushort id)
    {
        if (Active?.BossOid == 0x1BAA && id == 5372) alaTroikaImminent = true;
    }

    private void AlaMhigoTether(ulong source, ulong target, uint tether, bool present, DateTime time)
    {
        if (territory != AlaMhigoCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x1BA6 && tether == 45 && _actors.GetValueOrDefault(source) is { Oid: 0x1BAF } body)
        {
            alaTetherEvents[(source, tether)] = (present, time);
            if (present) alaBodies.TryAdd(source, (target, body.Position));
            else alaBodies.Remove(source);
        }
        if (Active?.BossOid != 0x1BAA || tether != 41) return;
        if (_actors.GetValueOrDefault(source)?.PartySlot >= 0) (source, target) = (target, source);
        alaTetherEvents[(source, tether)] = (present, time);
        if (present)
        {
            var activation = alaSparkBaits.Count == 0 ? time.AddSeconds(8) : alaSparkBaits.Values.First().Activation;
            alaSparkBaits.TryAdd(source, (target, activation));
        }
        else alaSparkBaits.Remove(source);
    }

    private void SynchronizeAlaMhigo()
    {
        if (territory != AlaMhigoCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x1BAA && alaShrinkAt == default) alaShrinkAt = _now.AddSeconds(10);
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EA66D && a.EventState != 7))
            alaFirePredictions.RemoveAll(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1BAF or 0x1BAA))
        {
            var tether = actor.Oid == 0x1BAF ? 45u : 41u;
            var target = actor.Tethers?.GetValueOrDefault(tether) ?? 0;
            if (alaTetherEvents.TryGetValue((actor.Id, tether), out var change))
            {
                if (_now < change.Time.AddSeconds(.5)) continue;
                if (!change.Present)
                {
                    if (target == 0) alaTetherEvents.Remove((actor.Id, tether));
                    else continue;
                }
            }
            if (target != 0) AlaMhigoTether(actor.Id, target, tether, true, _now);
            else
            {
                alaBodies.Remove(actor.Id);
                alaSparkBaits.Remove(actor.Id);
            }
        }
        foreach (var id in alaBodies.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead
            || !_actors.TryGetValue(alaBodies[id].Target, out var target) || target.Dead).ToArray()) alaBodies.Remove(id);
        foreach (var id in alaSparkBaits.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead
            || !_actors.TryGetValue(alaSparkBaits[id].Target, out var target) || target.Dead).ToArray()) alaSparkBaits.Remove(id);
        foreach (var key in alaSwordBaits.Keys.Where(k => !_actors.TryGetValue(k.Source, out var source) || source.Dead
            || !_actors.TryGetValue(k.Target, out var target) || target.Dead).ToArray()) alaSwordBaits.Remove(key);
    }

    private void AlaMhigoRemoved(ulong id)
    {
        alaBodies.Remove(id); alaSparkBaits.Remove(id);
        foreach (var source in alaBodies.Where(p => p.Value.Target == id).Select(p => p.Key).ToArray()) alaBodies.Remove(source);
        foreach (var source in alaSparkBaits.Where(p => p.Value.Target == id).Select(p => p.Key).ToArray()) alaSparkBaits.Remove(source);
        alaLasers.Remove(id);
        foreach (var key in alaTetherEvents.Keys.Where(k => k.Source == id).ToArray()) alaTetherEvents.Remove(key);
        foreach (var key in alaSwordBaits.Keys.Where(k => k.Source == id || k.Target == id).ToArray()) alaSwordBaits.Remove(key);
    }

    private void ResetAlaMhigo()
    {
        alaLasers.Clear(); alaLaserHits = 0; alaFirePredictions.Clear(); alaBodies.Clear();
        alaSparkBaits.Clear(); alaTetherEvents.Clear(); alaSwordBaits.Clear();
        alaShrunk = false; alaShrinkAt = default; alaTroikaImminent = false;
    }
}
