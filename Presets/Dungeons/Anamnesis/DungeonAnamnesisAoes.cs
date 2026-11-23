using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AnamnesisAoes()
    {
        if (territory != AnamnesisCatalog.TERRITORY || Arena == null) yield break;
        if (anamnesisReflection is { } reflection) yield return reflection;
        foreach (var group in anamnesisLines.GroupBy(l => l.Resolve))
        {
            var own = group.Any(l => l.Target == PlayerId);
            var targets = group.Select(l => l.Target).ToHashSet();
            var enter = new List<IGeometryShape>();
            foreach (var line in group)
            {
                if (!_actors.TryGetValue(line.Source, out var s) || !anamnesisLineTargets.TryGetValue(line.Target, out var t)) continue;
                var delta = t.Position - s.Position;
                var rotation = MathF.Atan2(delta.X, delta.Z);
                var safe = !own || t.Id == PlayerId;
                var shape = RayRectangleTemplate.Symmetric(8, 50);
                yield return new($"anamnesis/line/{group.Key}/{line.Source}/{line.Target}", "直线分摊", shape, s.Position,
                    rotation, Navigate: false, Safe: safe);
                if (!own) enter.Add(shape.Resolve(s.Position, rotation, null));
                else if (t.Id != PlayerId)
                    yield return new($"anamnesis/line/avoid/{group.Key}/{line.Source}", "点名避开另一分摊", RayRectangleTemplate.Symmetric(16, 50),
                        s.Position, rotation, Display: false, Activation: line.Activation);
                else
                {
                    var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && !targets.Contains(a.Id))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                    if (buddies.Length > 0)
                        yield return new($"anamnesis/line/join/{group.Key}/{line.Source}", "直线点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: line.Activation);
                }
            }
            if (enter.Count > 0)
                yield return new($"anamnesis/line/enter/{group.Key}", "进入直线分摊", new SohrKhaiWallHintTemplate(Arena, enter.ToArray()),
                    Arena.Position, Display: false, Activation: group.First().Activation);
        }
        if (anamnesisTiles.Count > 0)
            foreach (var tile in anamnesisTiles.TakeWhile(a => a.Activation < anamnesisTiles[0].Activation!.Value.AddSeconds(1))) yield return tile;
        var waves = anamnesisArenaMode == 1 && anamnesisWaves.Count > 0
            ? anamnesisWaves.Take(4).Where(a => a.Rotation == anamnesisWaves[0].Rotation) : anamnesisWaves;
        foreach (var wave in waves) yield return wave;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 19296 or 19328))
            if (_actors.TryGetValue(cast.TargetId, out var target) && !target.Dead)
            {
                yield return new($"anamnesis/stack/{cast.Source}", "集合分摊", new CircleTemplate(6), target.Position, Navigate: false, Safe: true);
                var regions = target.Id == PlayerId
                    ? _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray()
                    : [new CircleGeometry(target.Position, 6)];
                if (regions.Length > 0)
                    yield return new($"anamnesis/stack/enter/{cast.Source}", "进入分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
        if (anamnesisDrainActivation is not { } activation || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        Vector3 Position(int i) => new(i < 4 ? -11 : 11, Arena.Position.Y, -465 + i % 4 * 10);
        IGeometryShape Shape(int i) => new RectangleGeometry(Position(i), 0, 2.5f, 2.5f);
        var holding = anamnesisSolvedDrains.Where(i => Shape(i).Contains(player.Position)).Select(i => (int?)i).FirstOrDefault();
        foreach (var i in anamnesisActiveDrains.Concat(anamnesisSolvedDrains).Order())
            yield return new($"anamnesis/drain/{i}", "阻挡排水口", new RectangleTemplate(2.5f, 2.5f), Position(i),
                Navigate: false, Safe: anamnesisActiveDrains.Contains(i) || holding == i, OutlineOnly: true);
        var drains = holding is { } held ? new[] { Shape(held) } : anamnesisActiveDrains.Select(Shape).ToArray();
        if (drains.Length > 0)
            yield return new("anamnesis/drain/enter", holding != null ? "留在已堵排水口" : "进入任一排水口",
                new SohrKhaiWallHintTemplate(Arena, drains), Arena.Position, Display: false, Activation: activation);
    }
}
