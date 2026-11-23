using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> SkallaAoes()
    {
        if (territory != SkallaCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x1FAB)
        {
            foreach (var pool in _actors.Values.Where(a => a.Oid == 0x1EA7D5 && !a.Dead))
            {
                // The original component intentionally draws R7 but forbids R8.
                yield return new($"skalla/pool/{pool.Id}/display", "持续水池", new CircleTemplate(7), pool.Position, Navigate: false);
                yield return new($"skalla/pool/{pool.Id}/avoid", "持续水池避让", new CircleTemplate(8), pool.Position, Display: false);
            }
            if (skallaPuddleBaits.Count > 0)
            {
                IGeometryShape[] corners = [new CircleGeometry(new(-207, Arena.Position.Y, -9), 1.5f),
                    new CircleGeometry(new(-233, Arena.Position.Y, -9), 1.5f),
                    new CircleGeometry(new(-233, Arena.Position.Y, 17), 1.5f),
                    new CircleGeometry(new(-207, Arena.Position.Y, 17), 1.5f)];
                yield return new("skalla/puddle/corners", "前往角落诱导水池", new SohrKhaiWallHintTemplate(Arena, corners),
                    Arena.Position, Display: false, Activation: _now.AddSeconds(5));
            }
            foreach (var (target, activation) in skallaPuddleBaits)
                if (_actors.TryGetValue(target, out var actor))
                    foreach (var aoe in SkallaCircleBait($"skalla/puddle/{target}", actor, actor, 4, activation)) yield return aoe;
            foreach (var (id, bait) in skallaSpheres)
                if (_actors.TryGetValue(id, out var sphere) && _actors.TryGetValue(bait.Target, out var target))
                {
                    foreach (var aoe in SkallaCircleBait($"skalla/sphere/{id}", sphere, target, 6, bait.Activation)) yield return aoe;
                    foreach (var line in AlaMhigoLine($"skalla/sphere/{id}/line", sphere.Position, target.Position, false)) yield return line;
                }
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 9809 or 9810))
            {
                if (!_actors.TryGetValue(PlayerId, out var player)
                    || player.StatusExpirations?.Any(s => s.Key is 160 or 1209 && s.Value > cast.FinishAt) == true) continue;
                var delta = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * (cast.Rule.Action == 9809 ? -20 : 20);
                var safeStarts = new RectangleGeometry(Arena.Position - delta, 0, 29, 29);
                yield return new($"skalla/kb/{cast.Source}/bounds", "击退落点留在场内", new SohrKhaiWallHintTemplate(Arena, [safeStarts]),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
                var rectangle = new RectangleGeometry(cast.Location, cast.Rotation, cast.Rule.Action == 9809 ? 29 : 22,
                    cast.Rule.Action == 9809 ? 64 : 20);
                if (cast.Rule.Action == 9809)
                    yield return new($"skalla/kb/{cast.Source}/strip", "拉近前离开中间长条", new DungeonShapeTemplate(rectangle),
                        cast.Location, Display: false, Activation: cast.FinishAt);
                else
                    yield return new($"skalla/kb/{cast.Source}/start", "击退前避开角落", new SohrKhaiWallHintTemplate(Arena, [rectangle]),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
                var landing = player.Position + delta;
                var safe = Arena.Contains(landing);
                yield return new($"skalla/kb/{cast.Source}/landing", "定向位移落点", new CircleTemplate(.4f), landing, Navigate: false, Safe: safe, OutlineOnly: true);
                foreach (var line in AlaMhigoLine($"skalla/kb/{cast.Source}/line", player.Position, landing, safe)) yield return line;
            }
        }
        if (SkallaSubservient is { } sub)
        {
            var position = sub.Position + Vector3.UnitZ;
            yield return new("skalla/subservient/goal", "接近仆从使用碎岩", new CircleTemplate(2), position, Safe: true, OutlineOnly: true);
        }
        foreach (var ((id, icon), activation) in skallaChaos)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            GeometryTemplate shape = icon switch { 121 => new DonutTemplate(10, 20), 122 => new DungeonCrossTemplate(40, 4), _ => new CircleTemplate(7) };
            yield return new($"skalla/chaos/{id}/{icon}/display", "混沌诱导", shape, target.Position,
                MathF.PI, Navigate: false, OutlineOnly: PlayerId == id, Activation: activation);
            if (PlayerId != id)
                yield return new($"skalla/chaos/{id}/{icon}/avoid", "避开混沌诱导", shape, target.Position, MathF.PI, Display: false, Activation: activation);
            else
            {
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"skalla/chaos/{id}/{icon}/party/{other.Id}", "诱导不要覆盖队友", shape,
                        other.Position, MathF.PI, Display: false, Activation: activation);
                if (icon == 121)
                    yield return new($"skalla/chaos/{id}/{icon}/center", "月环诱导至中央", new SohrKhaiWallHintTemplate(Arena,
                        [new CircleGeometry(Arena.Position, 6)]), Arena.Position, Display: false, Activation: activation);
                else
                    yield return new($"skalla/chaos/{id}/{icon}/edge", "圆与十字诱导至边缘", new CircleTemplate(16),
                        Arena.Position, Display: false, Activation: activation);
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> SkallaCircleBait(string key, DungeonActor source, DungeonActor target, float radius, DateTime activation)
    {
        yield return new(key + "/display", "圆形诱导", new CircleTemplate(radius), source.Position,
            Navigate: false, OutlineOnly: PlayerId == target.Id, Activation: activation);
        if (PlayerId != target.Id)
            yield return new(key + "/avoid", "避开圆形诱导", new CircleTemplate(radius), source.Position, Display: false, Activation: activation);
        else
            foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                yield return new(key + $"/party/{other.Id}", "圆形诱导远离队友", new CircleTemplate(radius),
                    other.Position, Display: false, Activation: activation);
    }
}
