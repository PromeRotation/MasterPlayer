using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IReadOnlyList<SolverTeleporter> DoomTeleporters()
    {
        if (Active == null || doomCar is not (3 or 5) || _actors.GetValueOrDefault(PlayerId) is not { } player) return [];
        var left = doomCar == 3 ? 204.9f : 310;
        var right = doomCar == 3 ? 204.9f : 300;
        Vector3 P(float x, float z) => new(x, player.Position.Y, z);
        return [new(P(96.1f, left), P(92.4f, left), 1, false, 0),
            new(P(104.1f, right), P(107.6f, right), 1, false, 0)];
    }

    private IEnumerable<DungeonAmbientAoe> DoomGeometryAoes()
    {
        if (doomCar is 3 or 5)
        {
            yield return new("doom/platforms", "高台轮廓", new DungeonShapeTemplate(DoomDeck(true)), Arena!.Position,
                Navigate: false, OutlineOnly: true, Safe: true);
            var portals = DoomTeleporters();
            for (var i = 0; i < portals.Count; ++i)
            {
                var portal = portals[i];
                yield return new($"doom/portal/{i}", "高台传送入口", new CircleTemplate(1), portal.Entrance,
                    Navigate: false, Safe: true);
            }
            if (_actors.GetValueOrDefault(PlayerId) is { } player)
            {
                if (player.Position.Y >= 4)
                    for (var i = 0; i < portals.Count; ++i)
                        yield return new($"doom/obstacle/portal/{i}", "高台禁止回落传送点", new CircleTemplate(1.5f),
                            portals[i].Entrance, Display: false, Obstacle: true);
                else
                {
                    var ranges = doomCar == 3
                        ? new[] { (X: 92.5f, A: 195f, B: 204f), (X: 92.5f, A: 206f, B: 215f),
                            (X: 107.5f, A: 195f, B: 204f), (X: 107.5f, A: 206f, B: 215f) }
                        : [(X: 92.5f, A: 305f, B: 309f), (X: 92.5f, A: 311f, B: 315f),
                            (X: 107.5f, A: 295f, B: 299f), (X: 107.5f, A: 301f, B: 305f)];
                    for (var i = 0; i < ranges.Length; ++i)
                    {
                        var range = ranges[i];
                        yield return new($"doom/obstacle/platform/{i}", "地面不可直接进入高台", new RectangleTemplate(6, range.B - range.A),
                            new(range.X, player.Position.Y, (range.A + range.B) * .5f), Display: false, Obstacle: true);
                    }
                }
            }
        }
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x4A34 or 0x4B80 or 0x4A36))
            yield return new($"doom/actor/{actor.Id}", actor.Name, new CircleTemplate(MathF.Max(.5f, actor.HitboxRadius)), actor.Position,
                Navigate: false, OutlineOnly: true, Unclipped: true);
        if (_actors.GetValueOrDefault(PrimaryId) is { Targetable: true } boss && Arena != null)
        {
            var radius = doomCar == 4 ? 14.6f : 15;
            if (doomStage == 15 || doomStage is >= 16 and <= 20) radius = 27;
            yield return new("doom/boss", boss.Name, new CircleTemplate(.7f), Arena.Position - new Vector3(0, 0, radius),
                Navigate: false, OutlineOnly: true, Unclipped: true);
        }
    }
}
