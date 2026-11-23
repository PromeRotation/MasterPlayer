using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AnyderAoes()
    {
        if (territory != AnyderCatalog.TERRITORY || Arena == null) yield break;
        if (IsAnyderSharks)
            foreach (var pool in _actors.Values.Where(a => a.Oid == 0x1E909F && a.EventState != 7 && !a.Dead))
                yield return new($"anyder/pool/{pool.Id}", "持续水池", new CircleTemplate(8), pool.Position);
        if (Active?.BossOid == 0x249E && _actors.TryGetValue(PrimaryId, out var boss))
        {
            if (anyderBlossomDanger is { } flowers)
                yield return new("anyder/blossom", "花色危险区域", new DungeonShapeTemplate(flowers), flowers.Position);
            if (anyderFirstCross is { } first)
                yield return new("anyder/cross", "五连十字首击", first.Rule.Shape!, first.Location, first.Rotation, Activation: first.FinishAt);
            else if (anyderCrossActivation is { } start)
            {
                var next = start.AddSeconds((5 - anyderCrossRemaining) * 6.1);
                if (anyderCrossRemaining > 0 && (next - _now).TotalSeconds <= 2.5)
                    yield return new("anyder/cross", "五连十字后续", new DungeonCrossTemplate(25, 3),
                        QuantizedDungeonPosition(boss.Position), boss.Rotation + MathF.PI / 4, Activation: next);
                var breath = start.AddSeconds(27.1);
                if ((breath - _now).TotalSeconds <= 4.9)
                    yield return new("anyder/breath", "五连后吐息", new FanTemplate(25, MathF.PI / 2),
                        QuantizedDungeonPosition(boss.Position), boss.Rotation, Activation: breath);
            }
        }
        foreach (var aoe in anyderGrowth.Values) yield return aoe;
        if (Active?.BossOid != 0x28DA || anyderCollectOrbs.Count == 0) yield break;
        var activation = _casts.Values.FirstOrDefault(c => c.Rule.Action == 15906)?.FinishAt.AddSeconds(1.1) ?? _now.AddSeconds(2);
        var squares = new List<IGeometryShape>();
        foreach (var id in anyderCollectOrbs)
            if (_actors.TryGetValue(id, out var orb))
            {
                yield return new($"anyder/collect/{id}", "可吸收雷球", new CircleTemplate(.7f),
                    orb.Position, Navigate: false, Safe: true, OutlineOnly: true);
                var center = orb.Position + new Vector3(MathF.Sin(orb.Rotation), 0, MathF.Cos(orb.Rotation)) * .5f;
                squares.Add(new RectangleGeometry(center, 0, 1, 1));
            }
        if (squares.Count > 0)
            yield return new("anyder/collect/enter", "进入任一雷球拾取区", new SohrKhaiWallHintTemplate(Arena, squares.ToArray()),
                Arena.Position, Display: false, Activation: activation);
    }
}
