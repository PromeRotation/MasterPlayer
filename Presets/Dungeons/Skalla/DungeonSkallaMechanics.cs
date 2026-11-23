using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<(ulong Target, uint Icon), DateTime> skallaChaos = [];
    private readonly Dictionary<ulong, DateTime> skallaPuddleBaits = [];
    private readonly Dictionary<ulong, (ulong Target, DateTime Activation)> skallaSpheres = [];
    private readonly HashSet<ulong> skallaCancelledSpheres = [];

    internal DungeonActor? SkallaSubservient => Active?.BossOid == 0x1FAC
        ? _actors.Values.Where(a => a.Oid == 0x1FAD && !a.Dead).MaxBy(a => a.Position.Z) : null;

    internal DungeonActor? SkallaShatterstoneTarget
    {
        get
        {
            var target = SkallaSubservient;
            if (target == null || !_actors.TryGetValue(PlayerId, out var player) || !HasStatus(player, 1448)) return null;
            var distance = new Vector2(player.Position.X - target.Position.X, player.Position.Z - target.Position.Z).Length()
                - player.HitboxRadius - target.HitboxRadius;
            return distance < 3 ? target : null;
        }
    }

    private void SkallaIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x1FAB && icon == 43) skallaPuddleBaits.TryAdd(target, time);
        if (Active?.BossOid == 0x1FAE && icon is 121 or 122 or 28)
            skallaChaos.TryAdd((target, icon), time.AddSeconds(4.7));
    }

    private void SkallaEffect(ulong source, uint action)
    {
        if (territory != SkallaCatalog.TERRITORY) return;
        if (action is 9832 or 9833)
            foreach (var key in skallaChaos.Keys.Where(k => k.Icon is 122 or 28).ToArray()) skallaChaos.Remove(key);
        if (action == 9831)
            foreach (var key in skallaChaos.Keys.Where(k => k.Icon == 121).ToArray()) skallaChaos.Remove(key);
        if (action == 9813 && skallaPuddleBaits.Count > 0)
            skallaPuddleBaits.Remove(skallaPuddleBaits.Keys.First());
        if (action == 9755)
        {
            skallaSpheres.Remove(source);
            skallaCancelledSpheres.Add(source);
        }
    }

    private void SkallaTether(ulong source, ulong target, uint tether, bool present, DateTime time)
    {
        if (Active?.BossOid != 0x1FAB || tether != 3) return;
        if (_actors.GetValueOrDefault(source)?.PartySlot >= 0) (source, target) = (target, source);
        if (_actors.GetValueOrDefault(source)?.Oid != 0x2051) return;
        if (present)
        {
            skallaCancelledSpheres.Remove(source);
            var activation = skallaSpheres.Count == 0 ? time : skallaSpheres.Values.First().Activation;
            skallaSpheres.TryAdd(source, (target, activation));
        }
        else
        {
            skallaSpheres.Remove(source);
            skallaCancelledSpheres.Add(source);
        }
    }

    private void SynchronizeSkalla()
    {
        if (territory != SkallaCatalog.TERRITORY) return;
        foreach (var sphere in _actors.Values.Where(a => a.Oid == 0x2051 && !a.Dead))
        {
            var target = sphere.Tethers?.GetValueOrDefault(3u) ?? 0;
            if (target == 0)
            {
                // An explicit event can lead the object snapshot; retain it briefly.
                if (skallaSpheres.TryGetValue(sphere.Id, out var bait) && _now >= bait.Activation.AddSeconds(.5))
                    skallaSpheres.Remove(sphere.Id);
                skallaCancelledSpheres.Remove(sphere.Id);
            }
            else if (!skallaCancelledSpheres.Contains(sphere.Id) && !skallaSpheres.ContainsKey(sphere.Id))
                SkallaTether(sphere.Id, target, 3, true, _now);
        }
        foreach (var key in skallaChaos.Keys.Where(k => !_actors.TryGetValue(k.Target, out var a) || a.Dead).ToArray()) skallaChaos.Remove(key);
        foreach (var id in skallaPuddleBaits.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) skallaPuddleBaits.Remove(id);
        foreach (var id in skallaSpheres.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead
            || !_actors.TryGetValue(skallaSpheres[id].Target, out var p) || p.Dead).ToArray()) skallaSpheres.Remove(id);
    }

    private void SkallaRemoved(ulong id)
    {
        skallaPuddleBaits.Remove(id); skallaSpheres.Remove(id); skallaCancelledSpheres.Remove(id);
        foreach (var key in skallaChaos.Keys.Where(k => k.Target == id).ToArray()) skallaChaos.Remove(key);
        foreach (var source in skallaSpheres.Where(p => p.Value.Target == id).Select(p => p.Key).ToArray()) skallaSpheres.Remove(source);
    }

    private void ResetSkalla()
    {
        skallaChaos.Clear(); skallaPuddleBaits.Clear(); skallaSpheres.Clear(); skallaCancelledSpheres.Clear();
    }
}
