using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> MatoyaAoes()
    {
        if (territory != MatoyaCatalog.TERRITORY || Arena == null) yield break;
        for (var i = 0; i < matoyaBreccia.Count; ++i)
        {
            var s = matoyaBreccia[i];
            yield return new($"matoya/breccia/{i}", "逐次碎石扇环", MatoyaCatalog.Breccia(s.Step), s.Position, s.Rotation, Activation: s.Activation);
        }
        var holeHintAdded = false;
        foreach (var (id, bait) in matoyaBaits)
        {
            if (!_actors.TryGetValue(id, out var source) || !matoyaBaitTargets.TryGetValue(bait.Target, out var target)) continue;
            foreach (var aoe in DomaLineBait($"matoya/bait/{id}", source, target, bait.HalfWidth, Active?.BossOid == 0x300C ? 60 : 80))
                yield return aoe with { Activation = bait.Activation, OutlineOnly = target.Id == PlayerId };
            if (Active?.BossOid == 0x300C && target.Id == PlayerId && matoyaHoles.Count > 0 && !holeHintAdded)
            {
                holeHintAdded = true;
                var lanes = matoyaHoles.Select(h =>
                {
                    var delta = MatoyaHole(h) - source.Position;
                    return RayRectangleTemplate.Symmetric(2, new Vector2(delta.X, delta.Z).Length())
                        .Resolve(source.Position, MathF.Atan2(delta.X, delta.Z), null);
                }).ToArray();
                yield return new($"matoya/bait/hole/{id}", "将泥球引入任一洞口", new SohrKhaiWallHintTemplate(Arena, lanes), Arena.Position, Display: false);
            }
        }
        foreach (var hole in matoyaHoles)
            yield return new($"matoya/hole/{hole}", "泥球洞口", new CircleTemplate(5), MatoyaHole(hole), Navigate: false, Safe: true, OutlineOnly: true, Unclipped: true);
        foreach (var pool in _actors.Values.Where(a => Active?.BossOid == 0x300C && a.Oid == 0x1EB145))
            yield return new($"matoya/mud/{pool.Id}", "持续泥潭", new CircleTemplate(5), pool.Position);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 21652))
            if (_actors.TryGetValue(cast.TargetId, out var target))
            {
                yield return new($"matoya/stack/{cast.Source}", "四人分摊", new CircleTemplate(6), target.Position, Navigate: false, Safe: true);
                var regions = target.Id == PlayerId
                    ? _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray()
                    : [new CircleGeometry(target.Position, 6)];
                if (regions.Length > 0)
                    yield return new($"matoya/stack/enter/{cast.Source}", "集合四人分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
        foreach (var aoe in matoyaGurgles) yield return aoe;
        if (Active?.BossOid == 0x307F && !matoyaCloud)
        {
            var safe = matoyaGeysersActive
                ? matoyaGeysers.OrderBy(g => Vector3.DistanceSquared(g.Value.Position, new(0, Arena.Position.Y, -175))).Select(g => g.Key).FirstOrDefault() : 0;
            foreach (var (id, geyser) in matoyaGeysers)
            {
                yield return new($"matoya/geyser/{id}", "间歇泉", new CircleTemplate(6), geyser.Position, Navigate: id != safe, Safe: id == safe, Activation: geyser.Activation);
                if (id == safe)
                    yield return new($"matoya/geyser/enter/{id}", "进入通往云台的间歇泉", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(geyser.Position, 6)]),
                        Arena.Position, Display: false, Activation: geyser.Activation);
            }
        }
        foreach (var (id, activation) in matoyaSpreads)
            if (_actors.TryGetValue(id, out var target))
            {
                yield return new($"matoya/spread/{id}", "明火分散", new CircleTemplate(5), target.Position, Navigate: id != PlayerId, OutlineOnly: id == PlayerId, Activation: activation);
                if (id == PlayerId)
                    foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                        yield return new($"matoya/spread/{id}/avoid/{other.Id}", "远离队友分散", new CircleTemplate(5), other.Position, Display: false, Activation: activation);
            }
        var huff1 = _casts.Values.FirstOrDefault(c => c.Rule.Action == 22809);
        var huff = matoyaHuff;
        if (huff1 != null) huff = (huff1.Location, huff1.Rotation, 15, huff1.FinishAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 22814 or 22815 or 20045))
            yield return new($"matoya/rear/{cast.Source}", "环形火焰", cast.Rule.Shape!, cast.Location,
                Navigate: huff == null, Activation: cast.FinishAt);
        if (huff is { } kb)
        {
            yield return new("matoya/huff/enter", "靠近击退来源5码内", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(kb.Origin, 5)]), Arena.Position, Display: false);
            if (_actors.TryGetValue(PlayerId, out var player))
            {
                var landing = DungeonWallLanding(Arena, player.Position, new Vector3(MathF.Sin(kb.Rotation), 0, MathF.Cos(kb.Rotation)) * kb.Distance);
                foreach (var preview in DungeonMovementPreview("matoya/huff", "无视抗击退的预计落点", player.Position, landing)) yield return preview;
            }
        }
        if (matoyaBarbeque && _actors.TryGetValue(PlayerId, out var current))
        {
            yield return new("matoya/barbeque", "持续吸入火焰", RayRectangleTemplate.Symmetric(40, 10),
                new(-19.5f, Arena.Position.Y, 0), 89.999f * MathF.PI / 180);
            var start = new Vector3(matoyaImminent ? 18.5f : current.Position.X + .1f, Arena.Position.Y, 0);
            var end = new Vector3(19, Arena.Position.Y, 0);
            var delta = end - start;
            var lane = RayRectangleTemplate.Symmetric(40, MathF.Abs(delta.X)).Resolve(start, MathF.Atan2(delta.X, delta.Z), null);
            yield return new("matoya/barbeque/advance", "前往对侧并抵抗吸入", new SohrKhaiWallHintTemplate(Arena, [lane]), Arena.Position, Display: false);
        }
    }

    private Vector3 MatoyaHole(uint index) => new(index is 0x0A or 0x0C ? -202.627f : -157.373f,
        Arena?.Position.Y ?? 0, index is 0x0A or 0x0B ? -162.627f : -117.373f);
}
