using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? disclosureSpin;
    private readonly Dictionary<ulong, DungeonPendingCast> voidSparkBaits = [];
    private readonly List<DungeonPendingCast> voidSpheres = [];
    private readonly HashSet<ulong> gubalSeen = [];
    private readonly Dictionary<ulong, DateTime> gubalBirths = [];
    private readonly Dictionary<ulong, (Vector3 Position, DateTime Activation)> gubalTowers = [];
    private readonly Dictionary<ulong, (ulong Target, DateTime Activation, DateTime Changed)> whaleOilTethers = [];
    private readonly HashSet<ulong> whaleOilCancelled = [];
    public bool LegacyMovement { get; set; }
    public float? CameraAzimuth { get; set; }
    private int GubalTowerSoakers => _actors.Values.Count(a => a.Oid == 0x1E99F3) switch { 2 => 3, 3 => 2, _ => 1 };

    private void GubalObjectCreated(ulong source, DateTime timestamp)
    {
        if (Active?.BossOid != 0xE84) return;
        gubalBirths.TryAdd(source, timestamp);
        var index = voidSpheres.FindIndex(c => c.Source == source);
        if (index >= 0 && timestamp < voidSpheres[index].StartedAt)
            voidSpheres[index] = voidSpheres[index] with { StartedAt = timestamp, FinishAt = timestamp.AddSeconds(9.8) };
        if (gubalTowers.TryGetValue(source, out var tower) && timestamp.AddSeconds(17.8) < tower.Activation)
            gubalTowers[source] = (tower.Position, timestamp.AddSeconds(17.8));
    }

    private bool GubalBossForbidden(DungeonActor actor) => Active?.BossOid == 0xE83 && HasStatus(actor, 325);

    internal static Vector3 QuantizedDungeonPosition(Vector3 position)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(position.X * inverse) - .5f) * grid, position.Y,
            ((int)MathF.Round(position.Z * inverse) - .5f) * grid);
    }

    private void GubalCast(DungeonPendingCast cast)
    {
        if (Active?.BossOid == 0xE82 && cast.Rule.Action == 4818)
        {
            Arena = GubalArenas.DemonTomeSpin(cast.Location.Y);
            disclosureSpin = cast with { Rule = new(3989, 0xED6, "DisclosureSpin", new CircleTemplate(12.5f)),
                Location = QuantizedDungeonPosition(_actors[PrimaryId].Position), FinishAt = cast.FinishAt.AddSeconds(2.4) };
        }
    }

    private void GubalFinish(DungeonPendingCast cast, bool completed)
    {
        if (cast.Rule.Action == 4818 && !completed)
        {
            disclosureSpin = null;
            Arena = GubalArenas.DemonTome(cast.Location.Y);
        }
        if (cast.Rule.Action == 3780 && voidSpheres.Count > 0) voidSpheres.RemoveAt(0);
        if (cast.Rule.Action == 3524) gubalTowers.Clear();
    }

    private void GubalIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (Active?.BossOid == 0xE84 && icon == 23)
            voidSparkBaits[target] = new(PrimaryId, new(3526, 0xE84, "VoidSparkBait", new CircleTemplate(8)),
                default, 0, timestamp, timestamp.AddSeconds(5.1), target);
    }

    private void GubalEffect(uint action, ulong target)
    {
        if (Active?.BossOid == 0xE82 && action == 3989)
        {
            disclosureSpin = null;
            Arena = GubalArenas.DemonTome(_actors[PrimaryId].Position.Y);
        }
        if (Active?.BossOid == 0xE84 && action == 3526) voidSparkBaits.Remove(target);
    }

    private void GubalAnimation(ulong source, uint state)
    {
        if (Active?.BossOid != 0xE84 || state != 0x00100020 || !_actors.TryGetValue(source, out var actor)) return;
        foreach (var id in gubalTowers.Where(t => t.Value.Position.X == actor.Position.X && t.Value.Position.Z == actor.Position.Z)
            .Select(t => t.Key).ToArray()) gubalTowers.Remove(id);
    }

    private void GubalTether(ulong source, ulong target, uint tether, bool present, DateTime timestamp)
    {
        if (Active?.BossOid != 0xE83 || tether != 3) return;
        if (_actors.TryGetValue(source, out var tetherSource) && tetherSource.PartySlot >= 0) (source, target) = (target, source);
        if (present && target != 0)
        {
            whaleOilCancelled.Remove(source);
            whaleOilTethers[source] = (target, timestamp.AddSeconds(5), _now);
        }
        else
        {
            whaleOilTethers.Remove(source);
            whaleOilCancelled.Add(source);
        }
    }

    private void SynchronizeGubal()
    {
        if (Active?.BossOid == 0xE83)
        {
            foreach (var orb in _actors.Values.Where(a => a.Oid == 0x10C4 && !a.Dead))
            {
                if (whaleOilTethers.TryGetValue(orb.Id, out var previous) && _now < previous.Changed.AddSeconds(.5)) continue;
                var target = orb.Tethers?.GetValueOrDefault(3u) ?? 0;
                if (target == 0)
                {
                    whaleOilTethers.Remove(orb.Id);
                    whaleOilCancelled.Remove(orb.Id);
                }
                else if (!whaleOilCancelled.Contains(orb.Id) && previous.Target != target)
                    whaleOilTethers[orb.Id] = (target, _now.AddSeconds(5), _now);
            }
            foreach (var source in whaleOilTethers.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
                whaleOilTethers.Remove(source);
        }
        if (Active?.BossOid != 0xE84) return;
        var addsCreated = false;
        foreach (var actor in _actors.Values.Where(a => !a.Dead))
        {
            if (!gubalSeen.Add(actor.Id)) continue;
            var birth = gubalBirths.GetValueOrDefault(actor.Id, _now);
            if (actor.Oid == 0xEC5)
                voidSpheres.Add(new(actor.Id, new(3780, 0xEC5, "VoidSpark", new CircleTemplate(8)),
                    QuantizedDungeonPosition(actor.Position), 0, birth, birth.AddSeconds(9.8)));
            if (actor.Oid == 0x1E99F3) gubalTowers[actor.Id] = (actor.Position, birth.AddSeconds(17.8));
            if (actor.Oid is 0xE85 or 0xE86 or 0xE87) addsCreated = true;
        }
        if (addsCreated) gubalTowers.Clear();
        foreach (var target in voidSparkBaits.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) voidSparkBaits.Remove(target);
        foreach (var source in gubalTowers.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) gubalTowers.Remove(source);
    }

    private IEnumerable<DungeonAmbientAoe> GubalTowerAoes()
    {
        if (gubalTowers.Count == 0 || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        var needed = GubalTowerSoakers;
        var towers = gubalTowers.Select(t => new
        {
            Id = t.Key, t.Value.Position, t.Value.Activation,
            Inside = Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(t.Value.Position.X, t.Value.Position.Z)) <= 9,
            Count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead
                && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(t.Value.Position.X, t.Value.Position.Z)) <= 9),
        }).ToArray();
        var preferred = towers.FirstOrDefault(t => t.Count > 0 && t.Count <= needed && !(t.Count == 1 && t.Inside));
        foreach (var tower in towers)
            if (tower.Count > 0 && tower.Count <= needed && !(tower.Count == 1 && tower.Inside)
                && (preferred == null || tower.Count >= preferred.Count
                    && Vector3.DistanceSquared(tower.Position, player.Position) < Vector3.DistanceSquared(preferred.Position, player.Position)))
                preferred = tower;
        var anyGoal = preferred != null || towers.Any(t => t.Count < needed || t.Inside && t.Count == needed);
        foreach (var tower in towers)
        {
            var safe = tower.Count < needed || tower.Inside && tower.Count == needed;
            yield return new($"gubal/tower/{tower.Id}/display", $"召唤塔 {tower.Count}/{needed}", new CircleTemplate(3), tower.Position,
                Navigate: false, Safe: safe, OutlineOnly: safe);
            if (preferred != null ? preferred.Id == tower.Id : safe)
                yield return new($"gubal/tower/{tower.Id}/goal", "前往召唤塔", new CircleTemplate(3), tower.Position,
                    Display: false, Safe: true);
            else if (!anyGoal)
                yield return new($"gubal/tower/{tower.Id}/avoid", "离开人数已满的召唤塔", new CircleTemplate(3), tower.Position,
                    Display: false, Activation: towers[0].Activation);
        }
    }

    private IEnumerable<DungeonAmbientAoe> GubalAoes()
    {
        if (Active?.BossOid == 0xE82 && _actors.TryGetValue(PlayerId, out var player) && Arena != null)
        {
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 3519))
            {
                var forward = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
                var landing = DungeonWallLanding(Arena, player.Position, forward * 20);
                foreach (var aoe in DungeonMovementPreview("gubal/repel", "向前击退20码", player.Position, landing)) yield return aoe;
            }
            if (HasStatus(player, 911) && (!LegacyMovement || CameraAzimuth != null))
            {
                var rotation = LegacyMovement ? CameraAzimuth!.Value + MathF.PI : player.Rotation;
                var forward = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
                var landing = DungeonWallLanding(Arena, player.Position, forward * 15);
                yield return new("gubal/ice/range", "薄冰滑行15码", new CircleTemplate(15), player.Position, Navigate: false, OutlineOnly: true);
                foreach (var aoe in DungeonMovementPreview("gubal/ice", "薄冰滑行预测", player.Position, landing)) yield return aoe;
            }
        }
        if (Active?.BossOid == 0xE83)
        {
            foreach (var wind in _actors.Values.Where(a => a.Oid == 0x10C5 && !a.Dead))
                yield return new($"gubal/wind/{wind.Id}", "Tomewind", new CircleTemplate(3), wind.Position, Obstacle: true);
            foreach (var (source, tether) in whaleOilTethers)
            {
                if (_actors.TryGetValue(source, out var orb) && _actors.TryGetValue(tether.Target, out var tethered))
                    foreach (var aoe in DungeonMovementPreview($"gubal/oil/tether/{source}", "油球连线", orb.Position, tethered.Position)) yield return aoe;
                if (tether.Target == PlayerId && _actors.TryGetValue(PrimaryId, out var boss))
                    yield return new($"gubal/oil/{source}", "将油球引至首领", new CircleTemplate(boss.HitboxRadius), boss.Position, Safe: true);
            }
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 4163))
                if (_actors.TryGetValue(cast.Source, out var source) && _actors.TryGetValue(cast.TargetId, out var target))
                {
                    var delta = target.Position - source.Position;
                    var length = new Vector2(delta.X, delta.Z).Length();
                    if (length < .001f) continue;
                    var rotation = MathF.Atan2(delta.X, delta.Z);
                    yield return new("gubal/charge/display", "HeadDown", RayRectangleTemplate.Symmetric(8, length), source.Position,
                        rotation, Navigate: false, OutlineOnly: target.Id == PlayerId);
                    if (target.Id != PlayerId)
                        yield return new("gubal/charge/avoid", "HeadDown", RayRectangleTemplate.Symmetric(8, length), source.Position,
                            rotation, Display: false, Activation: cast.FinishAt);
                    else
                        foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                        {
                            var distance = new Vector2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z).Length();
                            var angle = MathF.Asin(Math.Clamp(4 / MathF.Max(.001f, distance), 0, 1));
                            yield return new($"gubal/charge/separate/{member.Id}", "冲锋避开队友", new FanTemplate(100, angle * 2),
                                source.Position, MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z),
                                Display: false, Activation: cast.FinishAt);
                        }
                }
        }
        if (Active?.BossOid != 0xE84) yield break;
        foreach (var aoe in GubalTowerAoes()) yield return aoe;
        foreach (var (targetId, cast) in voidSparkBaits)
        {
            if (!_actors.TryGetValue(targetId, out var target)) continue;
            yield return new($"gubal/bait/{targetId}", "VoidSparkBait", cast.Rule.Shape!, target.Position,
                Navigate: false, OutlineOnly: targetId == PlayerId);
            if (targetId != PlayerId)
                yield return new($"gubal/bait/avoid/{targetId}", "VoidSparkBait", cast.Rule.Shape!, target.Position,
                    Display: false, Activation: cast.FinishAt);
            else
            {
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"gubal/bait/separate/{member.Id}", "诱导避开队友", cast.Rule.Shape!, member.Position,
                        Display: false, Activation: cast.FinishAt);
                if (targetId == voidSparkBaits.Keys.First())
                    yield return new("gubal/bait/edge", "前往场边诱导", new DonutTemplate(22.5f, 24.5f),
                        new(377.8f, target.Position.Y, -59.7f), Display: false, Safe: true);
            }
        }
        foreach (var add in _actors.Values.Where(a => a.Oid == 0xE85 && !a.Dead && a.Targetable))
        {
            if (!_actors.TryGetValue(add.TargetId, out var target)) continue;
            var rotation = MathF.Atan2(target.Position.X - add.Position.X, target.Position.Z - add.Position.Z);
            yield return new($"gubal/cleave/{add.Id}/display", "AbyssalSwing", new FanTemplate(7.5f, MathF.PI / 2),
                add.Position, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"gubal/cleave/{add.Id}/avoid", "AbyssalSwing", new FanTemplate(7.5f, MathF.PI / 2),
                    add.Position, rotation, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"gubal/cleave/{add.Id}/party/{member.Id}", "避免顺劈队友", new FanTemplate(100, MathF.PI / 2),
                        add.Position, MathF.Atan2(member.Position.X - add.Position.X, member.Position.Z - add.Position.Z), Display: false);
        }
    }

    private static Vector3 DungeonWallLanding(IGeometryShape arena, Vector3 point, Vector3 displacement)
    {
        var start = new Vector2(point.X, point.Z);
        var direction = new Vector2(displacement.X, displacement.Z);
        var stop = 1f;
        foreach (var contour in arena.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = new Vector2(contour[i].X, contour[i].Z);
                var edge = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z) - a;
                var cross = direction.X * edge.Y - direction.Y * edge.X;
                if (MathF.Abs(cross) < .000001f) continue;
                var offset = a - start;
                var t = (offset.X * edge.Y - offset.Y * edge.X) / cross;
                var u = (offset.X * direction.Y - offset.Y * direction.X) / cross;
                if (t >= 0 && t < stop && u is >= 0 and <= 1) stop = t;
            }
        return point + displacement * Math.Max(0, stop - .00005f);
    }

    private static IEnumerable<DungeonAmbientAoe> DungeonMovementPreview(string id, string name, Vector3 from, Vector3 to)
    {
        yield return new(id + "/landing", name, new CircleTemplate(.5f), to, Navigate: false, OutlineOnly: true);
        var delta = to - from;
        var length = new Vector2(delta.X, delta.Z).Length();
        if (length > .001f)
            yield return new(id + "/path", name, new RectangleTemplate(.12f, length), (from + to) * .5f,
                MathF.Atan2(delta.X, delta.Z), Navigate: false);
    }
}
