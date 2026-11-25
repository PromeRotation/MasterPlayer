using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static readonly LocalPolygonTemplate queenEyeSymbol = CreateQueenEyeSymbol();
    private static readonly LocalPolygonTemplate queenGazeArc = CreateQueenGazeArc();

    private IEnumerable<DungeonAmbientAoe> QueenGazeDrawing()
    {
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var forward = new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
        var index = 0;
        var showArc = false;
        foreach (var eye in QueenGazes())
        {
            var offset = new Vector2(eye.Location.X - player.Position.X, eye.Location.Z - player.Position.Z);
            var danger = offset.LengthSquared() > 0 && Vector2.Dot(forward, Vector2.Normalize(offset)) >= .707107f;
            var color = danger ? new Vector4(1, .15f, .15f, 1) : new Vector4(.2f, 1, .2f, 1);
            var id = $"queen/gaze/{index++}";
            yield return new(id + "/eye", "权能之眼", queenEyeSymbol, eye.Location,
                Navigate: false, Safe: !danger, Unclipped: true, Color: color);
            yield return new(id + "/pupil", "权能之眼瞳孔", new CircleTemplate(.28f), eye.Location,
                Navigate: false, Safe: !danger, Unclipped: true, Color: new Vector4(.04f, .04f, .04f, 1));
            showArc |= offset.LengthSquared() <= 10000f * 10000f;
        }
        if (showArc)
            yield return new("queen/gaze/view", "视线前方九十度判定弧", queenGazeArc, player.Position,
                player.Rotation, Navigate: false, Unclipped: true, Color: new Vector4(1, .15f, .15f, 1));
    }

    internal static LocalPolygonTemplate CreateQueenEyeSymbol()
    {
        // Two intersecting circular arcs retain the source eye silhouette.
        const float radius = (1 + .6f * .6f) / (2 * .6f);
        const float offset = radius - .6f;
        var points = new List<Vector2>();
        for (var i = 0; i <= 20; ++i)
        {
            var x = -1 + i * .1f;
            points.Add(new(x, MathF.Sqrt(MathF.Max(0, radius * radius - x * x)) - offset));
        }
        for (var i = 19; i > 0; --i)
        {
            var x = -1 + i * .1f;
            points.Add(new(x, offset - MathF.Sqrt(MathF.Max(0, radius * radius - x * x))));
        }
        return new(points);
    }

    internal static LocalPolygonTemplate CreateQueenGazeArc()
    {
        var points = new List<Vector2>();
        for (var i = 0; i <= 24; ++i)
        {
            var angle = -MathF.PI / 4 + i * MathF.PI / 48;
            points.Add(new(MathF.Sin(angle) * 1.035f, -MathF.Cos(angle) * 1.035f));
        }
        for (var i = 24; i >= 0; --i)
        {
            var angle = -MathF.PI / 4 + i * MathF.PI / 48;
            points.Add(new(MathF.Sin(angle) * .965f, -MathF.Cos(angle) * .965f));
        }
        return new(points);
    }
}
