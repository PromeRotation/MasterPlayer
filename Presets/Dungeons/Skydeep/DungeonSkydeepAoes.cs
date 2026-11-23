using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> SkydeepAoes()
    {
        if (territory != SkydeepCatalog.TERRITORY || Arena == null) yield break;
        if (skyRing != null) yield return skyRing;
        if (skyShrink != null) yield return skyShrink;
        if (skyBubbleCasting)
            yield return new("sky/bubble/center", "召唤泡泡时离开中心", new CircleTemplate(5), Arena.Position, Display: false);
        foreach (var (bubble, serial) in skyBubbles)
        {
            yield return new($"sky/bubble/{serial}/display", "移动泡泡", new DungeonCapsuleTemplate(1.1f, 3),
                bubble.Position, bubble.Rotation, Navigate: false);
            yield return new($"sky/bubble/{serial}/nav", "移动泡泡预测", new DungeonCapsuleTemplate(1.1f, 3),
                bubble.Position, bubble.Rotation, Display: false, Activation: _now.AddSeconds(1.5));
            yield return new($"sky/bubble/{serial}/obstacle", "泡泡障碍", new CircleTemplate(1.1f),
                bubble.Position, Display: false, Obstacle: true);
        }
        foreach (var burst in skyBursts) yield return burst;
        foreach (var (actor, at, serial) in skyWaves.Where(w => !w.Actor.Dead))
            yield return new($"sky/wave/{serial}", "玩家喷吐引导", new FanTemplate(24, MathF.PI / 6),
                actor.Position, actor.Rotation, Navigate: actor.Id != PlayerId, OutlineOnly: actor.Id == PlayerId, Activation: at);
        for (var i = 0; i < skyStones.Count; ++i)
        {
            if (i == 0)
                skyStones[i] = skyStones[i] with { Navigate = true, Color = skyStones.Count > 1 ? new Vector4(1, 1, 0, 1) : skyStones[i].Color };
            yield return skyStones[i];
        }
        if (skyStones.Count > 0)
        {
            var impact = skyImpacts.FirstOrDefault(c => c.Rule.Action == 36667);
            yield return new("sky/stone/join", "石刻中线站位", new SohrKhaiWallHintTemplate(Arena,
                [new RectangleGeometry(Arena.Position, 0, 3, 80)]), Arena.Position, Display: false,
                Activation: impact?.FinishAt ?? skyStones[0].Activation);
        }
        if (skyShatters.Count > 0 && skyShatters[0].Activation!.Value.AddSeconds(-6) <= _now)
            foreach (var shatter in skyShatters) yield return shatter;
        foreach (var action in new uint[] { 36677, 36667, 36707 })
        {
            var cast = skyImpacts.FirstOrDefault(c => c.Rule.Action == action);
            if (cast == null) continue;
            var half = action == 36707 && skyHeatActive ? 14 : 19;
            var distance = action == 36707 ? 20 : 18;
            yield return new($"sky/impact/{action}/nav", "击退前安全站位", new VanaspatiKnockbackTemplate(
                new RectangleGeometry(Arena.Position, 0, half * 2, half * 2), cast.Location, [], distance),
                Arena.Position, Display: false, Activation: cast.FinishAt);
        }
        var player = _actors.GetValueOrDefault(PlayerId);
        if (player != null)
        {
            foreach (var component in skyImpacts.GroupBy(c => c.Rule.Action))
            {
                var start = player.Position;
                foreach (var cast in component)
                {
                    if (KnockbackImmune(player, cast.FinishAt)) continue;
                    var landing = SkydeepImpactLanding(cast, start);
                    foreach (var a in DungeonMovementPreview($"sky/impact/{cast.Source}", "冲击击退落点", start, landing)) yield return a;
                    start = landing;
                }
            }
        }
        var spreads = _casts.Values.Where(c => c.Rule.Action is 36450 or 36709
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead).ToArray();
        var destructive = spreads.Where(c => c.Rule.Action == 36709).ToArray();
        foreach (var cast in spreads)
            foreach (var a in ShisuiBait($"sky/spread/{cast.Source}", cast.TargetId, 6, cast.FinishAt))
                yield return a with { Shape = a.Display ? a.Shape : new CircleTemplate(7), OutlineOnly = a.Display,
                    Navigate = a.Navigate && (cast.Rule.Action != 36709 || skyHeatOrigin != null), Position = QuantizedDungeonPosition(a.Position) };
        if (destructive.Length > 0 && skyHeatOrigin is { } origin)
            yield return new("sky/spread/origin", "击退后分散保持来源十五码内", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(origin, 15)]),
                Arena.Position, Display: false, Activation: destructive[0].FinishAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 39122))
        {
            if (_actors.TryGetValue(cast.Source, out var source) && source.Dead) continue;
            if (!_actors.TryGetValue(cast.TargetId, out var target)) continue;
            var own = target.Id == PlayerId;
            yield return new($"sky/buster/{cast.Source}/display", "范围死刑", new CircleTemplate(6),
                target.Position, Navigate: !own, OutlineOnly: own, Activation: cast.FinishAt);
            if (own)
                foreach (var a in _actors.Values.Where(a => a.Id != PlayerId && a.PartySlot >= 0 && !a.Dead))
                    yield return new($"sky/buster/{cast.Source}/avoid/{a.Id}", "死刑远离队友", new CircleTemplate(6),
                        a.Position, Display: false, Activation: cast.FinishAt);
        }
        foreach (var aoe in SkydeepTowersAndStacks(player)) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> SkydeepTowersAndStacks(DungeonActor? player)
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var action in new uint[] { 36688, 36689 })
        {
            var towers = _casts.Values.Where(c => c.Rule.Action == action).ToArray();
            var join = new List<IGeometryShape>();
            foreach (var t in towers)
            {
                var shape = new CircleGeometry(t.Location, 6);
                var inside = player != null && shape.Contains(player.Position, 0);
                var count = party.Count(a => shape.Contains(a.Position, 0));
                var safe = count < 4 || inside && count <= 4;
                yield return new($"sky/tower/{t.Source}/display", "四人塔", new CircleTemplate(6), QuantizedDungeonPosition(t.Location),
                    Navigate: false, OutlineOnly: inside || count < 4, Safe: safe);
                if (count < 4 || inside && count == 4) join.Add(shape);
            }
            if (join.Count > 0)
                yield return new($"sky/tower/{action}/join", "进入四人塔", new SohrKhaiWallHintTemplate(Arena, join.ToArray()),
                    Arena.Position, Display: false, Activation: towers[0].FinishAt);
            else
                foreach (var t in towers)
                    yield return new($"sky/tower/{t.Source}/avoid", "避开满员塔", new CircleTemplate(6), t.Location,
                        Display: false, Activation: towers[0].FinishAt);
        }
        var stacks = _casts.Values.Where(c => c.Rule.Action == 36710
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead).ToArray();
        var stackTargets = stacks.Select(c => c.TargetId).ToHashSet();
        var ownStack = stackTargets.Contains(PlayerId);
        var stackJoin = new List<IGeometryShape>();
        foreach (var c in stacks)
        {
            var target = _actors[c.TargetId]; var shape = new CircleGeometry(target.Position, 6);
            var count = party.Count(a => shape.Contains(a.Position, 0));
            var inside = player != null && shape.Contains(player.Position, 0);
            var safe = target.Id == PlayerId || !ownStack && (inside ? count <= 4 : count < 4);
            yield return new($"sky/stack/{c.Source}/display", "四人分摊", new CircleTemplate(6), QuantizedDungeonPosition(target.Position),
                Navigate: false, OutlineOnly: true, Safe: safe);
            if (target.Id != PlayerId)
            {
                if (safe) stackJoin.Add(shape);
                else
                    yield return new($"sky/stack/{c.Source}/avoid", "避免重叠分摊", new CircleTemplate(ownStack ? 12 : 6), target.Position,
                        Display: false, Activation: c.FinishAt);
            }
            else
            {
                var candidates = party.Where(a => !stackTargets.Contains(a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"sky/stack/{c.Source}/gather", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: c.FinishAt);
            }
        }
        if (stackJoin.Count > 0)
            yield return new("sky/stack/join", "进入四人分摊", new DungeonExclusiveJoinTemplate(Arena, stackJoin.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].FinishAt);
    }

    private Vector3 SkydeepImpactLanding(DungeonPendingCast cast, Vector3 point)
    {
        if (Arena == null) return point;
        var delta = point - cast.Location; delta.Y = 0;
        if (delta.LengthSquared() < .000001f) return point;
        var direction = Vector3.Normalize(delta); var distance = cast.Rule.Action == 36707 ? 20f : 18f;
        var bounds = Arena.GetBounds();
        var x = direction.X == 0 ? float.PositiveInfinity : ((direction.X > 0 ? bounds.MaxX : bounds.MinX) - point.X) / direction.X;
        var z = direction.Z == 0 ? float.PositiveInfinity : ((direction.Z > 0 ? bounds.MaxZ : bounds.MinZ) - point.Z) / direction.Z;
        return point + direction * MathF.Min(distance, MathF.Min(x, z) + (.5f - .499f));
    }

    private IEnumerable<DungeonDamagePrediction> SkydeepDamage()
    {
        if (territory != SkydeepCatalog.TERRITORY) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 39122))
            if ((!_actors.TryGetValue(cast.Source, out var source) || !source.Dead)
                && _actors.TryGetValue(cast.TargetId, out var a) && a.PartySlot >= 0)
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, cast.FinishAt, "Tankbuster");
        if (skyAshlayer is { } ash)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, ash, "Raidwide");
        foreach (var action in new uint[] { 36688, 36689 })
        {
            var towers = _casts.Values.Where(c => c.Rule.Action == action).ToArray();
            if (towers.Length == 0) continue;
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && towers.Any(t =>
                Vector2.DistanceSquared(new(t.Location.X, t.Location.Z), new(a.Position.X, a.Position.Z)) <= 36)))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, towers[0].FinishAt, "Raidwide");
        }
    }

    private IEnumerable<string> SkydeepHints()
    {
        if (territory != SkydeepCatalog.TERRITORY || Arena == null) yield break;
        if (skyWaves.Any(w => w.Actor.Id == PlayerId && !w.Actor.Dead)) yield return "引导喷吐：面向远离队友";
        if (!_actors.TryGetValue(PlayerId, out var player)) yield break;
        foreach (var component in skyImpacts.GroupBy(c => c.Rule.Action))
        {
            var start = player.Position;
            foreach (var cast in component)
            {
                if (KnockbackImmune(player, cast.FinishAt)) continue;
                start = SkydeepImpactLanding(cast, start);
                if (!Arena.Contains(start, 0) || cast.Rule.Action == 36667 && skyStones.Count > 0
                    && skyStones[0].Shape.Resolve(skyStones[0].Position, skyStones[0].Rotation, null).Contains(start, 0))
                    yield return "击退落点危险";
            }
        }
    }
}
