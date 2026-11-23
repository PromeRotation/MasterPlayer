using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> antBirths = [];
    private readonly HashSet<ulong> antSeenChirps = [];
    private readonly Dictionary<ulong, (Vector3 Position, DateTime Activation)> antChirps = [];
    private DungeonAmbientAoe? antCroak;
    private ulong antCroakSource;
    private int antCroakHits;
    private readonly Dictionary<(ulong Source, uint Id), ulong> antTethers = [];
    private readonly Dictionary<(ulong Source, uint Id), ulong> antSeenTethers = [];
    private readonly Dictionary<(ulong Source, uint Id), DateTime> antTetherEvents = [];
    private ulong antMeteorTarget;
    private readonly Dictionary<ulong, DungeonPendingCast> antGazeBaits = [];
    private DateTime antGazeActivation;
    private readonly Dictionary<ulong, int> antParryPredictions = [];
    private readonly HashSet<ulong> antObservedParries = [];
    private bool IsAntDolls => territory == AntitowerCatalog.TERRITORY && Active?.BossOid == 0x1503;

    private bool AntDollAlive(DungeonActor actor) => IsAntDolls && AntitowerCatalog.IsNpcDoll(actor.Oid) && !actor.Dead;

    private void AntitowerCreated(ulong id, DateTime timestamp)
    {
        if (Active?.BossOid != 0x14FC) return;
        if (!antBirths.TryGetValue(id, out var previous) || timestamp < previous) antBirths[id] = timestamp;
        if (antChirps.TryGetValue(id, out var chirp))
            antChirps[id] = (chirp.Position, antBirths[id].AddSeconds(8.7));
    }

    private void AntitowerCast(DungeonPendingCast cast)
    {
        if (IsAntDolls && cast.Rule.Action == 5557) antParryPredictions[cast.Source] = 14;
    }

    private void AntitowerStatus(ulong id, uint status, bool present)
    {
        if (IsAntDolls && status == 680 && present) antParryPredictions.Remove(id);
    }

    private void AntitowerIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (!IsAntDolls || icon != 73 || !_actors.TryGetValue(target, out var victim) || victim.Dead) return;
        var source = _actors.Values.FirstOrDefault(a => a.Oid == 0x1502 && !a.Dead);
        if (source == null) return;
        var rule = new DungeonCastRule(5559, 0x1502, "TerrifyingGlance", new FanTemplate(42.25f, 122 * MathF.PI / 180));
        antGazeBaits[target] = new(source.Id, rule, source.Position, 0, timestamp, timestamp.AddSeconds(3.1), target);
        antGazeActivation = timestamp.AddSeconds(3.1);
    }

    private void AntitowerTether(ulong source, ulong target, uint id, bool present, DateTime timestamp)
    {
        if (Active?.BossOid != 0x3D82) return;
        var key = (source, id);
        if (antTetherEvents.TryGetValue(key, out var previous) && timestamp <= previous) return;
        antTetherEvents[key] = timestamp;
        if (present)
        {
            antSeenTethers[key] = target;
            if (antMeteorTarget == 0 && _actors.ContainsKey(target)) antMeteorTarget = target;
            if (id is 1 or 2) antTethers[key] = target;
        }
        else antTethers.Remove(key);
    }

    private void AntitowerEffect(ulong source, uint action, ulong target, float? rotation)
    {
        if (Active?.BossOid == 0x14FC)
        {
            if (action == 5543) antChirps.Clear();
            if (action == 5540 && _actors.TryGetValue(source, out var actor))
            {
                if (antCroak == null) antCroakSource = source;
                antCroak ??= new("antitower/croak", "OdiousCroak", new FanTemplate(14, MathF.PI * 2 / 3),
                    QuantizedDungeonPosition(actor.Position), rotation ?? actor.Rotation);
                if (++antCroakHits == 12) { antCroak = null; antCroakHits = 0; }
            }
        }
        if (Active?.BossOid == 0x3D82 && action == 31840)
        {
            antMeteorTarget = 0;
            foreach (var key in antTethers.Where(t => t.Value == target || target == 0).Select(t => t.Key).ToArray())
                antTethers.Remove(key);
        }
        if (IsAntDolls && action == 5559) { antGazeBaits.Clear(); antGazeActivation = default; }
    }

    private void SynchronizeAntitower()
    {
        if (antCroak != null && (!_actors.TryGetValue(antCroakSource, out var croaker) || croaker.Dead))
        {
            antCroak = null; antCroakHits = 0;
        }
        if (IsAntDolls)
            foreach (var doll in _actors.Values.Where(a => a.Oid == 0x1502))
            {
                if (HasStatus(doll, 680))
                {
                    if (antObservedParries.Add(doll.Id)) antParryPredictions.Remove(doll.Id);
                }
                else antObservedParries.Remove(doll.Id);
            }
        if (Active?.BossOid == 0x14FC)
            foreach (var chirp in _actors.Values.Where(a => a.Oid == 0x14FE && !a.Dead))
                if (antSeenChirps.Add(chirp.Id))
                    antChirps[chirp.Id] = (QuantizedDungeonPosition(chirp.Position), antBirths.GetValueOrDefault(chirp.Id, _now).AddSeconds(8.7));
        if (Active?.BossOid == 0x3D82)
        {
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Tethers != null))
                foreach (var (id, target) in actor.Tethers!)
                {
                    var key = (actor.Id, id);
                    if (antSeenTethers.TryGetValue(key, out var old) && old == target) continue;
                    if (antTetherEvents.TryGetValue(key, out var changed) && _now < changed.AddSeconds(.5)) continue;
                    AntitowerTether(actor.Id, target, id, true, _now);
                }
            foreach (var key in antTethers.Keys.ToArray())
                if (!_actors.TryGetValue(key.Source, out var source) || source.Dead
                    || !_actors.TryGetValue(antTethers[key], out var target) || target.Dead
                    || source.Tethers?.GetValueOrDefault(key.Id) != target.Id
                        && (!antTetherEvents.TryGetValue(key, out var changed) || _now >= changed.AddSeconds(.5)))
                    antTethers.Remove(key);
            if (!_actors.TryGetValue(antMeteorTarget, out var victim) || victim.Dead) antMeteorTarget = 0;
            foreach (var key in antSeenTethers.Keys.Where(k => !_actors.TryGetValue(k.Source, out var source)
                || source.Tethers?.ContainsKey(k.Id) != true).ToArray())
                if (!antTetherEvents.TryGetValue(key, out var changed) || _now >= changed.AddSeconds(.5))
                    antSeenTethers.Remove(key);
        }
        foreach (var id in antGazeBaits.Keys.Where(id => !_actors.TryGetValue(id, out var target) || target.Dead
            || !_actors.TryGetValue(antGazeBaits[id].Source, out var source) || source.Dead).ToArray()) antGazeBaits.Remove(id);
    }

    private int AntParrySides(DungeonActor actor)
    {
        if (!IsAntDolls || actor.Oid != 0x1502) return 0;
        var active = 0;
        if (HasStatus(actor, 680))
        {
            active = actor.StatusParams?.GetValueOrDefault(680u) & 15 ?? 0;
            if (statusChanges.TryGetValue((actor.Id, 680), out var change) && _now < change.ChangedAt.AddSeconds(.5))
                active = change.Param & 15;
        }
        return active | antParryPredictions.GetValueOrDefault(actor.Id);
    }

    private bool AntParryForbidden(DungeonActor actor)
        => HasStatus(actor, 680) && AntAttackSideForbidden(actor);

    private bool AntAttackSideForbidden(DungeonActor actor)
    {
        if (!_actors.TryGetValue(PlayerId, out var player)) return false;
        var direction = MathF.Atan2(player.Position.X - actor.Position.X, player.Position.Z - actor.Position.Z);
        var delta = MathF.IEEERemainder(direction - actor.Rotation, 2 * MathF.PI);
        var side = delta > 0 ? 4 : 8;
        if (MathF.Abs(delta) < MathF.PI / 4) side = 1;
        else if (MathF.Abs(delta) > 3 * MathF.PI / 4) side = 2;
        return (AntParrySides(actor) & side) != 0;
    }

    private IEnumerable<DungeonPendingCast> AntitowerGazes()
    {
        if (antGazeActivation == default || antGazeBaits.Count == 0 || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        var bait = antGazeBaits.Values.First();
        if (!_actors.TryGetValue(bait.Source, out var source) || !_actors.TryGetValue(bait.TargetId, out var target)) yield break;
        var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
        var direction = MathF.Atan2(player.Position.X - source.Position.X, player.Position.Z - source.Position.Z);
        if (MathF.Abs(MathF.IEEERemainder(direction - rotation, 2 * MathF.PI)) <= 61 * MathF.PI / 180)
            yield return bait with { Location = source.Position, FinishAt = antGazeActivation };
    }

    private IEnumerable<DungeonAmbientAoe> AntitowerAoes()
    {
        if (antCroak != null) yield return antCroak;
        foreach (var (id, chirp) in antChirps)
            yield return new($"antitower/chirp/{id}", "DiscordantHarmony", new CircleTemplate(6), chirp.Position, Activation: chirp.Activation);
        foreach (var ((sourceId, _), targetId) in antTethers)
        {
            if (!_actors.TryGetValue(sourceId, out var source) || !_actors.TryGetValue(targetId, out var target)) continue;
            var length = Vector2.Distance(new(source.Position.X, source.Position.Z), new(target.Position.X, target.Position.Z));
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var shape = RayRectangleTemplate.Symmetric(6, length);
            yield return new($"antitower/charge/{sourceId}", "JitteringJounce", shape, source.Position, rotation, Navigate: targetId != PlayerId, OutlineOnly: targetId == PlayerId);
            if (targetId == PlayerId)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                {
                    var distance = Vector2.Distance(new(source.Position.X, source.Position.Z), new(buddy.Position.X, buddy.Position.Z));
                    var halfAngle = MathF.Asin(Math.Clamp(3 / Math.Max(distance, .001f), 0, 1));
                    yield return new($"antitower/charge/party/{sourceId}/{buddy.Id}", "冲锋避开队友", new FanTemplate(100, halfAngle * 2),
                        source.Position, MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z), Display: false);
                }
        }
        if (Active?.BossOid == 0x3D82 && antMeteorTarget == PlayerId && PlayerId != 0 && _actors.TryGetValue(PrimaryId, out var boss))
            foreach (var meteor in _actors.Values.Where(a => a.Oid == 0x3D83 && !a.Dead))
                yield return new($"antitower/meteor/{meteor.Id}", "躲在陨石后", new PtDonutSectorTemplate(11.1f, 20, 2 * MathF.Asin(1.5f / 11)),
                    new(185.8f, meteor.Position.Y, 137.5f), MathF.Atan2(meteor.Position.X - boss.Position.X, meteor.Position.Z - boss.Position.Z), Safe: true);
        foreach (var bait in antGazeBaits.Values)
        {
            if (!_actors.TryGetValue(bait.Source, out var source) || !_actors.TryGetValue(bait.TargetId, out var target)) continue;
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            yield return new($"antitower/glance/{target.Id}", "TerrifyingGlance", bait.Rule.Shape!, source.Position, rotation,
                Navigate: false, OutlineOnly: target.Id == PlayerId);
            if (target.Id != PlayerId)
                yield return new($"antitower/glance/avoid/{target.Id}", "离开视线扇形", bait.Rule.Shape!, source.Position, rotation,
                    Display: false, Activation: bait.FinishAt);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"antitower/glance/party/{buddy.Id}", "视线避开队友", new FanTemplate(100, 122 * MathF.PI / 180),
                        source.Position, MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z),
                        Display: false, Activation: bait.FinishAt);
        }
        if (!IsAntDolls) yield break;
        foreach (var doll in _actors.Values.Where(a => a.Oid == 0x1502 && !a.Dead))
        {
            var sides = AntParrySides(doll);
            foreach (var (side, angle) in new[] { (1, 0f), (2, MathF.PI), (4, MathF.PI / 2), (8, -MathF.PI / 2) })
            {
                if ((sides & side) == 0) continue;
                yield return new($"antitower/parry/{doll.Id}/{side}", HasStatus(doll, 680) ? "招架生效" : "即将招架",
                    new PtDonutSectorTemplate(1.45f, 1.55f, MathF.PI / 2), doll.Position, doll.Rotation + angle, Navigate: false, OutlineOnly: true);
                if (_actors.GetValueOrDefault(PlayerId)?.TargetId == doll.Id && AntAttackSideForbidden(doll))
                    yield return new($"antitower/parry/move/{doll.Id}/{side}", "前往未招架方向", new FanTemplate(100, MathF.PI / 2),
                        doll.Position, doll.Rotation + angle, Display: false);
            }
        }
    }

    private IEnumerable<string> AntitowerHints()
    {
        if (Active?.BossOid == 0x14FC)
        {
            if (_actors.Values.Any(a => a.PartySlot >= 0 && HasStatus(a, 439))) yield return "击杀小怪，解除蛙变";
            if (_actors.Values.Any(a => a.PartySlot >= 0 && HasStatus(a, 3513))) yield return "脑震荡：可驱散";
        }
        if (Active?.BossOid == 0x3D82 && antMeteorTarget == PlayerId && PlayerId != 0) yield return "躲在陨石后！";
        if (IsAntDolls && _actors.Values.Any(a => a.PartySlot >= 0 && HasStatus(a, 1055))) yield return "击杀小人偶，解救被束缚队友";
        if (IsAntDolls && _actors.Values.Any(AntAttackSideForbidden)) yield return "从未招架方向攻击";
    }

    private void ResetAntitower()
    {
        antBirths.Clear(); antSeenChirps.Clear(); antChirps.Clear(); antCroak = null; antCroakHits = 0; antCroakSource = 0;
        antTethers.Clear(); antSeenTethers.Clear(); antTetherEvents.Clear(); antMeteorTarget = 0;
        antGazeBaits.Clear(); antGazeActivation = default; antParryPredictions.Clear(); antObservedParries.Clear();
    }
}
