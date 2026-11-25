using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonPendingCast> shiningBlades = [];
    private readonly List<DungeonPendingCast> brightSpheres = [];
    private readonly HashSet<ulong> seenBrightSpheres = [];
    private readonly Dictionary<ulong, DateTime> brightSphereBirths = [];
    private readonly Dictionary<ulong, DungeonPendingCast> executions = [];
    private readonly List<(Vector3 Position, DateTime Activation)> dimensionalRips = [];
    private readonly Dictionary<ulong, ulong> holyChains = [];
    private readonly Dictionary<ulong, DateTime> holyChainEvents = [];

    public void ObjectCreated(ulong source, DateTime timestamp, uint oid = 0, Vector3? position = null, float rotation = 0)
    {
        if (IsNativeFate && position is { } fatePosition)
            FateCreated(source, oid, fatePosition, rotation);
        if ((IsSleep || IsOtis) && MainScenarioQuestCatalog.Owns(territory, oid)) sleepOtisCompleted.Remove(source);
        SlickshellCreated(source, oid);
        if (IsMaxQuest && oid == 0x252B) maxQuestCompleted.Remove(source);
        if (IsHopeOnTheWaves) hopeActors.Remove(source);
        TreasureRoomCreated(source, oid);
        TreasureYanCreateEvent(source, oid, position, rotation);
        if (position is { } minePosition) TreasureHexMineCreated(source, oid, minePosition, createEvent: true);
        TreasureRotationCreated(source, oid, rotation);
        if (position is { } hydrospherePosition) TreasureHydrosphereCreated(source, oid, hydrospherePosition);
        if (position is { } circlePosition) TreasureCreatedCircle(source, oid, circlePosition);
        if (position is { } beamPosition) TreasureBeamCreated(source, oid, beamPosition, rotation);
        if (position is { } jobPosition && Active != null && (IsSummoningNode || IsSteel))
        {
            var actor = _actors.GetValueOrDefault(source) ?? new DungeonActor(source, oid, jobPosition, Rotation: rotation);
            NodeCreated(actor);
            SteelCreated(actor);
        }
        PortraitCreated(source, oid, position, rotation);
        WhorlCreated(source, timestamp);
        YojimboCreated(source, timestamp);
        SeiryuCreated(source, timestamp);
        UnderCreated(source, timestamp);
        MesoCreated(source, timestamp);
        MistCreated(source, timestamp);
        GubalObjectCreated(source, timestamp);
        if (territory == MatoyaCatalog.TERRITORY) matoyaBirths.TryAdd(source, timestamp);
        if (territory == PaglthanCatalog.TERRITORY) paglthanBirths.TryAdd(source, timestamp);
        if (territory == ZotCatalog.TERRITORY) zotBirths.TryAdd(source, timestamp);
        ResearchObjectCreated(source, timestamp);
        AntitowerCreated(source, timestamp);
        SohrCreated(source, timestamp);
        BurnCreated(source, timestamp);
        GhimlytCreated(source, timestamp);
        DohnCreated(source, timestamp);
        QitanaCreated(source, timestamp);
        GulgCreated(source, timestamp);
        AmaurotCreated(source);
        TwinningCreated(source, timestamp);
        if (Active?.BossOid != 0x1051) return;
        brightSphereBirths.TryAdd(source, timestamp);
        var index = brightSpheres.FindIndex(c => c.Source == source);
        if (index >= 0 && timestamp < brightSpheres[index].StartedAt)
            brightSpheres[index] = brightSpheres[index] with { StartedAt = timestamp, FinishAt = timestamp.AddSeconds(4.6) };
    }

    public void NpcYell(ulong source, ushort id, DateTime timestamp)
    {
        MesoYell(source, id, timestamp);
        if (territory == LapisCatalog.TERRITORY && Active?.BossOid == 0x3AE2 && id == 15175)
            lapisTsunami = timestamp.AddSeconds(4.5);
        XelYell(source, timestamp);
        AlaMhigoYell(id);
        if (Active?.BossOid != 0x1051 || id != 2523 || !_actors.TryGetValue(PrimaryId, out var boss)
            || source != 0 && !_actors.ContainsKey(source)) return;
        var primary = boss.Position;
        var center = new Vector3(0, primary.Y, -100);
        Vector3 west = new(-18.509f, primary.Y, -100.023f), south = new(-.015f, primary.Y, -81.834f);
        Vector3 north = new(-.015f, primary.Y, -117.205f), east = new(18.387f, primary.Y, -100.053f);
        var angle = MathF.Atan2(primary.X - center.X, primary.Z - center.Z);
        bool InCone(float direction) => MathF.Abs(MathF.Atan2(MathF.Sin(angle - direction), MathF.Cos(angle - direction))) <= MathF.PI / 3;
        Vector3[] targets;
        if (InCone(MathF.PI / 2)) targets = [west, south, north, east];
        else if (InCone(-MathF.PI / 2)) targets = [east, north, south, west];
        else if (InCone(MathF.PI)) targets = [south, east, west, north];
        else targets = [north, west, east, south];
        shiningBlades.Clear();
        for (var i = 0; i < targets.Length; ++i)
        {
            var delta = targets[i] - primary;
            var length = new Vector2(delta.X, delta.Z).Length();
            if (length > .001f)
                shiningBlades.Add(new(PrimaryId, new(4130, 0x1051, "ShiningBlade", RayRectangleTemplate.Symmetric(6, length)),
                    primary, MathF.Atan2(delta.X, delta.Z), timestamp, timestamp.AddSeconds(.08 + 2.2 * i)));
            primary = targets[i];
        }
    }

    private void VaultIcon(ulong target, uint marker, DateTime timestamp)
    {
        if (Active?.BossOid == 0x1051 && marker == 32)
            executions[target] = new(PrimaryId, new(4131, 0x1051, "Execution", new CircleTemplate(5)),
                default, 0, timestamp, timestamp.AddSeconds(4.8), target);
    }

    private void VaultEffect(uint action, ulong target, Vector3? location, DateTime timestamp)
    {
        if (Active?.BossOid == 0x1051)
        {
            if (action == 4130 && shiningBlades.Count > 0) shiningBlades.RemoveAt(0);
            if (action == 4132 && brightSpheres.Count > 0) brightSpheres.RemoveAt(0);
            if (action == 4131)
            {
                if (!executions.Remove(target) && executions.Count > 0) executions.Remove(executions.Keys.First());
            }
        }
        if (Active?.BossOid == 0x1054 && action == 4140)
        {
            var position = _actors.GetValueOrDefault(target)?.Position ?? location;
            if (position is { } point) dimensionalRips.Add((point, timestamp.AddSeconds(1.1)));
        }
    }

    private void VaultTether(ulong source, ulong target, uint tether, bool present)
    {
        if (Active?.BossOid is not (0x1056 or 0x3DA7) || tether != 9) return;
        holyChainEvents[source] = _now;
        holyChainEvents[target] = _now;
        if (holyChains.Remove(source, out var previous)) holyChains.Remove(previous);
        if (holyChains.Remove(target, out previous)) holyChains.Remove(previous);
        if (present && source != 0 && target != 0 && source != target)
        {
            holyChains[source] = target;
            holyChains[target] = source;
        }
    }

    private void SynchronizeVault()
    {
        if (Active?.BossOid == 0x1051)
        {
            foreach (var sphere in _actors.Values.Where(a => a.Oid == 0x1052 && !a.Dead))
            {
                if (!seenBrightSpheres.Add(sphere.Id)) continue;
                var birth = brightSphereBirths.GetValueOrDefault(sphere.Id, _now);
                // The client serializes positions on this grid; match the resolved sphere center.
                const float grid = 2000f / 65535f;
                var pos = new Vector3((MathF.Round(sphere.Position.X / grid) - .5f) * grid, sphere.Position.Y,
                    (MathF.Round(sphere.Position.Z / grid) - .5f) * grid);
                brightSpheres.Add(new(sphere.Id, new(4132, 0x1052, "BrightFlare", new CircleTemplate(6)), pos, 0,
                    birth, birth.AddSeconds(4.6)));
            }
            foreach (var id in executions.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) executions.Remove(id);
        }
        if (Active?.BossOid == 0x1054)
            foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E91C1 && a.EventState != 7 && !a.Dead))
            {
                var index = dimensionalRips.FindIndex(r => Vector2.DistanceSquared(
                    new(r.Position.X, r.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
                if (index >= 0) dimensionalRips.RemoveAt(index);
            }
        if (Active?.BossOid is not (0x1056 or 0x3DA7)) return;
        foreach (var (source, target) in holyChains.ToArray())
        {
            if (!holyChains.ContainsKey(source) || _now < holyChainEvents.GetValueOrDefault(source).AddSeconds(.5)) continue;
            var forward = _actors.GetValueOrDefault(source)?.Tethers?.GetValueOrDefault(9u) == target;
            var reverse = _actors.GetValueOrDefault(target)?.Tethers?.GetValueOrDefault(9u) == source;
            if (!forward && !reverse) VaultTether(source, target, 9, false);
        }
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
        {
            if (holyChainEvents.TryGetValue(actor.Id, out var changed) && _now < changed.AddSeconds(.5)) continue;
            var target = actor.Tethers?.GetValueOrDefault(9u) ?? 0;
            if (target != 0 && holyChains.GetValueOrDefault(actor.Id) != target) VaultTether(actor.Id, target, 9, true);
        }
        foreach (var id in holyChains.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
            if (holyChains.TryGetValue(id, out var target)) VaultTether(id, target, 9, false);
    }

    private IEnumerable<DungeonAmbientAoe> VaultRipAoes()
    {
        for (var i = 0; i < dimensionalRips.Count; ++i)
            yield return new($"vault/rip/predicted/{i}", "DimensionalRip", new CircleTemplate(5),
                dimensionalRips[i].Position, Activation: dimensionalRips[i].Activation);
        foreach (var actor in _actors.Values.Where(a => !a.Dead && (a.Oid == 0x1055 || a.Oid == 0x1E91C1 && a.EventState != 7)))
            yield return new($"vault/rip/{actor.Id}", actor.Oid == 0x1055 ? "AetherialTear" : "DimensionalRip",
                new CircleTemplate(actor.Oid == 0x1055 ? 7 : 5), actor.Position);
    }

    private IEnumerable<DungeonAmbientAoe> VaultAoes()
    {
        if (Active?.BossOid == 0x1051)
        {
            foreach (var (targetId, cast) in executions)
            {
                if (!_actors.TryGetValue(targetId, out var target)) continue;
                yield return new($"vault/execution/{targetId}", "Execution", cast.Rule.Shape!, target.Position,
                    Navigate: false, OutlineOnly: targetId == PlayerId);
                if (targetId != PlayerId)
                    yield return new($"vault/execution/avoid/{targetId}", "Execution", cast.Rule.Shape!, target.Position,
                        Display: false, Activation: cast.FinishAt);
                else
                    foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                        yield return new($"vault/execution/separate/{member.Id}", "分散处刑", cast.Rule.Shape!, member.Position,
                            Display: false, Activation: cast.FinishAt);
            }
        }
        if (Active?.BossOid == 0x1054)
        {
            var rips = VaultRipAoes().ToArray();
            foreach (var aoe in rips) yield return aoe;
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 4135))
            {
                if (Arena == null || !_actors.TryGetValue(PlayerId, out var player) || KnockbackImmune(player, cast.FinishAt)) continue;
                var dangers = rips.Select(a => a.Shape.Resolve(a.Position, 0, null)).ToArray();
                var template = new VaultKnockbackTemplate(Arena, cast.Location, dangers);
                yield return new("vault/faith/constraint", "击退落点避开裂缝", template, cast.Location,
                    Display: false, Activation: cast.FinishAt);
                var landing = template.Landing(player.Position);
                var safe = !dangers.Any(d => d.Contains(landing));
                yield return new("vault/faith/landing", "贴墙击退落点", new CircleTemplate(.5f), landing,
                    Navigate: false, Safe: safe, OutlineOnly: true);
                var delta = landing - player.Position;
                var length = new Vector2(delta.X, delta.Z).Length();
                if (length > .001f)
                    yield return new("vault/faith/path", "击退轨迹", new RectangleTemplate(.12f, length),
                        (player.Position + landing) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: safe);
            }
        }
        if (Active?.BossOid is 0x1056 or 0x3DA7)
        {
            if (_actors.TryGetValue(PlayerId, out var player) && holyChains.TryGetValue(PlayerId, out var partnerId)
                && _actors.TryGetValue(partnerId, out var partner) && !partner.Dead)
            {
                var delta = partner.Position - player.Position;
                var length = new Vector2(delta.X, delta.Z).Length();
                yield return new("vault/chain/avoid", "持续远离连线队友断链", new CircleTemplate(length + 1), partner.Position,
                    Display: false, Activation: _now.AddSeconds(10));
                if (length > .001f)
                    yield return new("vault/chain/display", "神圣锁链", new RectangleTemplate(.12f, length),
                        (player.Position + partner.Position) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false);
            }
        }
        if (Active?.BossOid == 0x1056)
        {
            foreach (var knight in _actors.Values.Where(a => a.Oid is 0x1057 or 0x1058 && !a.Dead))
            {
                var length = knight.ModelState == 8 ? 10f : 3f;
                if (knight.ModelState != 8 && Vector2.DistanceSquared(new(knight.Position.X, knight.Position.Z), new(0, 4.1f)) <= 100) continue;
                yield return new($"vault/knight/{knight.Id}/display", "移动骑士", new DungeonCapsuleTemplate(2, length),
                    knight.Position, knight.Rotation, Navigate: false);
                yield return new($"vault/knight/{knight.Id}/contact", "骑士碰撞", new CircleTemplate(2), knight.Position,
                    Display: false, Obstacle: true);
                foreach (var (scale, delay) in new[] { (.5f, 1.1), (1f, 3d), (2f, 10d), (3f, double.PositiveInfinity) })
                    yield return new($"vault/knight/{knight.Id}/{scale}", "骑士移动预测", new DungeonCapsuleTemplate(2, length * scale),
                        knight.Position, knight.Rotation, Display: false,
                        Activation: double.IsPositiveInfinity(delay) ? DateTime.MaxValue.AddDays(-1) : _now.AddSeconds(delay));
            }
            foreach (var flame in _actors.Values.Where(a => a.Oid == 0x1059 && !a.Dead))
                yield return new($"vault/flame/{flame.Id}", "神圣火焰", new CircleTemplate(flame.HitboxRadius), flame.Position,
                    Navigate: false, Safe: true, OutlineOnly: true);
        }
        var casters = Active?.BossOid switch
        {
            0x1051 => _actors.Values.Where(a => a.Oid == 0x1051),
            0x1054 => _actors.Values.Where(a => a.Oid is 0x1053 or 0x1054),
            _ => Enumerable.Empty<DungeonActor>(),
        };
        foreach (var boss in casters.Where(a => !a.Dead && a.Targetable))
        {
            if (!_actors.TryGetValue(boss.TargetId, out var target)) continue;
            var radius = boss.Oid == 0x1053 ? 6.5f : 10.2f;
            var angle = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            yield return new($"vault/cleave/{boss.Id}/display", "顺劈", new FanTemplate(radius, MathF.PI / 2),
                boss.Position, angle, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"vault/cleave/{boss.Id}/avoid", "顺劈", new FanTemplate(radius, MathF.PI / 2),
                    boss.Position, angle, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"vault/cleave/{boss.Id}/party/{member.Id}", "顺劈避开队友", new FanTemplate(100, MathF.PI / 2),
                        boss.Position, MathF.Atan2(member.Position.X - boss.Position.X, member.Position.Z - boss.Position.Z), Display: false);
        }
    }
}
