using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> BardamBait(ulong sourceId, ulong targetId, bool charge, DateTime? activation)
    {
        if (!_actors.TryGetValue(sourceId, out var source) || !_actors.TryGetValue(targetId, out var target) || target.Dead) yield break;
        var delta = target.Position - source.Position;
        var length = new Vector2(delta.X, delta.Z).Length();
        var rotation = MathF.Atan2(delta.X, delta.Z);
        var key = $"bardam/bait/{sourceId}/{targetId}";
        GeometryTemplate shape = charge ? RayRectangleTemplate.Symmetric(8, length) : new FanTemplate(42.8f, MathF.PI / 2);
        yield return new(key + "/display", charge ? "拉长冲锋连线" : "翅膀扇形点名", shape, source.Position, rotation,
            Navigate: false, OutlineOnly: targetId == PlayerId, Activation: activation);
        if (targetId != PlayerId)
            yield return new(key + "/avoid", "避开队友点名", shape, source.Position, rotation, Display: false, Activation: activation);
        else
        {
            if (charge)
                yield return new(key + "/distance", "连线拉长至23码", new CircleTemplate(23), source.Position, Display: false, Activation: activation);
            foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var offset = other.Position - source.Position;
                var distance = new Vector2(offset.X, offset.Z).Length();
                var half = charge ? MathF.Asin(Math.Clamp(4 / Math.Max(.001f, distance), 0, 1)) : MathF.PI / 4;
                yield return new(key + $"/party/{other.Id}", "点名避开队友方向", new FanTemplate(100, 2 * half),
                    source.Position, MathF.Atan2(offset.X, offset.Z), Display: false, Activation: activation);
            }
            if (charge && length > .001f)
                yield return new(key + "/tether", "冲锋连线", new RectangleTemplate(.12f, length), (source.Position + target.Position) * .5f,
                    rotation, Navigate: false, Safe: length >= 23);
        }
    }

    private IEnumerable<DungeonAmbientAoe> BardamAoes()
    {
        if (territory != BardamCatalog.TERRITORY || Arena == null) yield break;
        foreach (var (target, activation) in bardamRush)
            foreach (var aoe in BardamBait(PrimaryId, target, true, activation)) yield return aoe;
        foreach (var aoe in bardamYamaa.Values) yield return aoe;
        foreach (var sequence in bardamSequences)
        {
            GeometryTemplate shape = (sequence.Heavy, sequence.Stage) switch
            {
                (true, 0) => new FanTemplate(6.5f, MathF.PI * 1.5f),
                (true, 1) => new PtDonutSectorTemplate(6.5f, 12.5f, MathF.PI * 1.5f),
                (true, _) => new PtDonutSectorTemplate(12.5f, 18.5f, MathF.PI * 1.5f),
                (false, 0) => new CircleTemplate(10),
                _ => new DonutTemplate(10, 20),
            };
            yield return new($"bardam/sequence/{sequence.Source}", sequence.Heavy ? "三段重击" : "内外震颤",
                shape, sequence.Position, sequence.Rotation, Activation: sequence.Activation);
        }
        if (!bardamSequences.Any(s => !s.Heavy))
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 9595))
                yield return new($"bardam/final/{cast.Source}", "最后震颤", new DonutTemplate(10, 20), cast.Location, Activation: cast.FinishAt);
        foreach (var comet in bardamComets)
        {
            var position = BardamCometPosition(comet);
            yield return new($"bardam/comet/{comet.Source}/display", $"彗星追踪：剩余{comet.Remaining}次", new CircleTemplate(4),
                position, Navigate: false, Activation: comet.Activation);
            if (!_actors.TryGetValue(comet.Target, out var target)) continue;
            var self = comet.Target == PlayerId;
            var avoid = self ? position - new Vector3(MathF.Sin(target.Rotation), 0, MathF.Cos(target.Rotation)) * 4 : position;
            yield return new($"bardam/comet/{comet.Source}/avoid", "连续彗星避让", new CircleTemplate(self ? 14 : 5),
                avoid, Display: false, Activation: comet.Activation);
            var delta = target.Position - comet.Previous;
            var length = new Vector2(delta.X, delta.Z).Length();
            if (length > .001f)
                yield return new($"bardam/comet/{comet.Source}/line", "彗星追踪方向", new RectangleTemplate(.12f, length),
                    (target.Position + comet.Previous) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false);
        }
        var stackTargets = bardamRings.Where(p => _actors.ContainsKey(p.Key)).ToArray();
        foreach (var (id, activation) in stackTargets)
            yield return new($"bardam/ring/{id}", "月环集合", new DonutTemplate(10, 20), _actors[id].Position, Navigate: false, Activation: activation);
        var stackAreas = stackTargets.Where(p => p.Key != PlayerId).Select(p => (IGeometryShape)new CircleGeometry(_actors[p.Key].Position, 2.5f)).ToArray();
        if (stackAreas.Length > 0)
            yield return new("bardam/ring/stack", "进入队友月环中心集合", new SohrKhaiWallHintTemplate(Arena, stackAreas), Arena.Position,
                Display: false, Activation: stackTargets[0].Value);
        var towers = _casts.Values.Where(c => c.Rule.Action == 7937).ToArray();
        var available = new List<IGeometryShape>();
        foreach (var tower in towers)
        {
            var inside = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(tower.Location.X, tower.Location.Z)) <= 9).ToArray();
            var safe = inside.Length == 0 || inside.Length == 1 && inside[0].Id == PlayerId;
            yield return new($"bardam/tower/{tower.Source}", "一人塔", new CircleTemplate(3), tower.Location,
                Navigate: false, Safe: safe, OutlineOnly: safe, Activation: tower.FinishAt);
            if (safe) available.Add(new CircleGeometry(tower.Location, 3));
            else
                yield return new($"bardam/tower/{tower.Source}/avoid", "避开已有人的塔", new CircleTemplate(3), tower.Location, Display: false, Activation: tower.FinishAt);
        }
        if (available.Count > 0)
            yield return new("bardam/tower/enter", "进入未满的一人塔", new SohrKhaiWallHintTemplate(Arena, available.ToArray()), Arena.Position,
                Display: false, Activation: towers[0].FinishAt);
        foreach (var (source, shelter) in losSnapshots)
        {
            if (!_casts.TryGetValue(source, out var cast) || cast.Rule.Action != 9602) continue;
            yield return new($"bardam/meteor/{source}/safe", "唯一星片遮挡区", shelter.Safe, cast.Location, Navigate: false, Safe: true);
            yield return new($"bardam/meteor/{source}/danger", "躲在唯一星片后", shelter.Danger, cast.Location, Display: false, Activation: cast.FinishAt);
        }
        foreach (var (id, activation) in bardamSpreads)
        {
            if (!_actors.TryGetValue(id, out var target) || target.Dead) continue;
            yield return new($"bardam/spread/{id}", "羽落分散", new CircleTemplate(6), target.Position,
                Navigate: id != PlayerId, OutlineOnly: id == PlayerId, Activation: activation);
            if (id == PlayerId)
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"bardam/spread/{id}/party/{other.Id}", "分散远离队友", new CircleTemplate(6), other.Position, Display: false, Activation: activation);
        }
        foreach (var id in bardamWingbeats)
            foreach (var aoe in BardamBait(PrimaryId, id, false, null)) yield return aoe;
        if (bardamSquall != null) yield return bardamSquall;
    }
}
