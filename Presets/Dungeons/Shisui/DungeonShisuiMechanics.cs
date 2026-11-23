using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<ulong> shisuiBindTargets = [];
    private readonly Dictionary<ulong, DateTime> shisuiDigestTargets = [];
    private readonly HashSet<ulong> shisuiSeenBindZones = [];
    private readonly List<DungeonAmbientAoe> shisuiPools = [];
    private readonly Dictionary<ulong, (Vector3 Position, bool Open)> shisuiChests = [];
    private bool shisuiSeduce;
    private DungeonAmbientAoe? shisuiKick;
    private readonly Dictionary<ulong, DateTime> shisuiChaseTargets = [];
    private readonly List<ShisuiChaser> shisuiChasers = [];
    private readonly HashSet<(ulong Source, DateTime Started)> shisuiChaseSeen = [];
    private int shisuiSplashes;
    private ulong shisuiSplashHelper;
    private DateTime shisuiBlackTideAt;

    private void PrepareShisuiChests()
    {
        if (Active?.BossOid != 0x1B0E) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x18D6 && a.NameId == 6274))
            shisuiChests.TryAdd(actor.Id, (actor.Position, false));
    }

    private void ShisuiAnimation(ulong source, uint state)
    {
        if (Active?.BossOid != 0x1B0E || state is not (0x00040008 or 0x00100020) || !_actors.TryGetValue(source, out var actor)) return;
        foreach (var (id, chest) in shisuiChests)
        {
            if (MathF.Abs(chest.Position.X - actor.Position.X) > 5 || MathF.Abs(chest.Position.Z - actor.Position.Z) > 5) continue;
            shisuiChests[id] = (chest.Position, state == 0x00040008);
            return;
        }
    }

    private void ShisuiIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (!_actors.ContainsKey(target)) return;
        if (Active?.BossOid == 0x1B0C)
        {
            if (icon == 1) shisuiBindTargets.Add(target);
            if (icon == 14) shisuiDigestTargets[target] = timestamp.AddSeconds(7.2);
        }
        if (Active?.BossOid == 0x1B0E && icon == 1) shisuiChaseTargets[target] = timestamp.AddSeconds(5.1);
    }

    private void ShisuiCast(DungeonPendingCast cast)
    {
        if (Active?.BossOid != 0x1B0E) return;
        if (cast.Rule.Action == 8058)
        {
            shisuiSeduce = true;
            shisuiKick = new("shisui/kick", "魅惑后踢击", new CircleTemplate(13),
                QuantizedDungeonPosition(new(-.046f, Arena!.Position.Y, -208.362f)), Activation: cast.FinishAt.AddSeconds(7.7));
        }
        if (cast.Rule.Action == 8059)
            shisuiKick = new("shisui/kick", "踢击", new CircleTemplate(13), cast.Location, Activation: cast.FinishAt);
        if (cast.Rule.Action == 9431 && shisuiChaseSeen.Add((cast.Source, cast.StartedAt)))
        {
            var target = shisuiChaseTargets.Keys.Where(_actors.ContainsKey)
                .OrderBy(id => Vector3.DistanceSquared(_actors[id].Position, cast.Location)).FirstOrDefault();
            if (target == 0) return;
            shisuiChaseTargets.Remove(target);
            shisuiChasers.Add(new(cast.Source, target, cast.Location, 0, 10, cast.FinishAt));
        }
    }

    private void ShisuiFinish(DungeonPendingCast cast, bool completed)
    {
        if (cast.Rule.Action == 8058)
        {
            shisuiSeduce = false;
            if (!completed) shisuiKick = null;
        }
        if (cast.Rule.Action == 8059) shisuiKick = null;
        if (cast.Rule.Action == 9431 && !completed) shisuiChasers.RemoveAll(c => c.Source == cast.Source && c.Remaining == 10);
    }

    private Vector3 ShisuiChasePosition(ShisuiChaser chase)
    {
        if (!_actors.TryGetValue(chase.Target, out var target)) return chase.Previous;
        var delta = target.Position - chase.Previous;
        delta.Y = 0;
        var distance = delta.Length();
        return QuantizedDungeonPosition(distance > chase.MoveDistance && distance > .001f
            ? chase.Previous + delta * (chase.MoveDistance / distance) : target.Position);
    }

    private void ShisuiEffect(ulong source, uint action, ulong target, uint sequence, Vector3? location, DateTime timestamp)
    {
        if (Active?.BossOid == 0x1B0C && action == 8057)
        {
            shisuiDigestTargets.Remove(target);
            var position = _actors.GetValueOrDefault(target)?.Position ?? location;
            if (position is { } p)
                shisuiPools.Add(new($"shisui/pool/predicted/{source}/{sequence}", "消化液预告", new CircleTemplate(5), p, Activation: timestamp.AddSeconds(.7)));
        }
        if (Active?.BossOid == 0x1B0E && action is 9431 or 8061)
        {
            var position = target == source ? _actors.GetValueOrDefault(source)?.Position : _actors.GetValueOrDefault(target)?.Position;
            position ??= location;
            if (position == null || shisuiChasers.Count == 0) return;
            if (target == source) position = QuantizedDungeonPosition(position.Value);
            var index = shisuiChasers.Select((c, i) => (Distance: Vector3.DistanceSquared(ShisuiChasePosition(c), position.Value), Index: i))
                .MinBy(c => c.Distance).Index;
            var chase = shisuiChasers[index];
            if (chase.Remaining == 1) shisuiChasers.RemoveAt(index);
            else shisuiChasers[index] = chase with { Previous = position.Value, MoveDistance = 3, Remaining = chase.Remaining - 1, Activation = timestamp.AddSeconds(.8) };
            if (shisuiChasers.Count == 0) shisuiChaseTargets.Clear();
        }
        if (Active?.BossOid != 0x1B0F) return;
        if (action == 8067) { ++shisuiSplashes; shisuiSplashHelper = source; shisuiBlackTideAt = timestamp.AddSeconds(4); }
        if (action == 8065) shisuiSplashes = 0;
    }

    private void SynchronizeShisui()
    {
        if (territory != ShisuiCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x1B0C)
        {
            foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EA0D7))
                if (shisuiSeenBindZones.Add(zone.Id)) shisuiBindTargets.Clear();
            shisuiSeenBindZones.RemoveWhere(id => !_actors.ContainsKey(id));
            if (_actors.Values.Where(a => a.Oid == 0x1B0D).All(a => a.Dead)) shisuiDigestTargets.Clear();
            foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E950D && a.EventState != 7))
            {
                var index = shisuiPools.FindIndex(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
                if (index >= 0) shisuiPools.RemoveAt(index);
            }
        }
        foreach (var id in shisuiBindTargets.Where(id => !_actors.ContainsKey(id)).ToArray()) shisuiBindTargets.Remove(id);
        foreach (var id in shisuiDigestTargets.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) shisuiDigestTargets.Remove(id);
        shisuiChasers.RemoveAll(c => c.Remaining < 10 && (!_actors.TryGetValue(c.Target, out var actor) || actor.Dead));
    }

    private void ShisuiRemoved(ulong id)
    {
        shisuiBindTargets.Remove(id); shisuiDigestTargets.Remove(id); shisuiSeenBindZones.Remove(id);
        shisuiChaseTargets.Remove(id);
        shisuiChasers.RemoveAll(c => c.Target == id);
    }

    private void ResetShisui()
    {
        shisuiBindTargets.Clear(); shisuiDigestTargets.Clear(); shisuiSeenBindZones.Clear(); shisuiPools.Clear();
        shisuiChests.Clear(); shisuiSeduce = false; shisuiKick = null;
        shisuiChaseTargets.Clear(); shisuiChasers.Clear(); shisuiChaseSeen.Clear();
        shisuiSplashes = 0; shisuiSplashHelper = 0; shisuiBlackTideAt = default;
    }
}
