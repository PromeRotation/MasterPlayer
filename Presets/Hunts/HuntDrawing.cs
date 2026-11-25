using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal static class HuntDrawing
{
    private static readonly LocalPolygonTemplate actor = new([new(-.45f, .4f), new(0, -.7f), new(.45f, .4f)]);
    private static readonly LocalPolygonTemplate eye = Eye();
    private static readonly LocalPolygonTemplate viewArc = ViewArc(-MathF.PI / 4, MathF.PI / 4, 24);
    private static readonly LocalPolygonTemplate invertedViewArc = ViewArc(MathF.PI / 4, 7 * MathF.PI / 4, 72);
    private static readonly Vector4 enemyColor = new(1, 0, 0, 1);
    private static readonly Vector4 safeColor = new(0, 1, 0, 1);
    private static readonly Vector4 dangerColor = new(1, 1, 0, 1);

    internal static IEnumerable<HuntDrawingPrimitive> Collect(SimpleHuntEncounter encounter, ReplayActorState? player)
    {
        foreach (var enemy in encounter.DrawingActors.Where(a => a.Targetable && !a.Dead))
            yield return new($"enemy/{enemy.Id}", actor.Resolve(enemy.Position, enemy.Rotation, null), false, enemyColor);
        if (player == null) yield break;
        var showArc = false;
        var showInvertedArc = false;
        foreach (var gaze in encounter.Gazes(player))
        {
            var dangerous = SimpleHuntEncounter.HitByGaze(gaze, player) != gaze.Rule.InvertedGaze;
            var color = dangerous ? enemyColor : safeColor;
            yield return new($"gaze/{gaze.AreaId}/eye", eye.Resolve(gaze.Input.Location, 0, null), !dangerous, color);
            yield return new($"gaze/{gaze.AreaId}/pupil", new CircleTemplate(.28f).Resolve(gaze.Input.Location, 0, null),
                !dangerous, Vector4.One);
            if (gaze.Rule.InvertedGaze) showInvertedArc |= SimpleHuntEncounter.InGazeRange(gaze, player);
            else showArc |= SimpleHuntEncounter.InGazeRange(gaze, player);
        }
        if (showArc)
            yield return new("gaze/view", viewArc.Resolve(player.Position, player.Rotation, null), false, enemyColor);
        if (showInvertedArc)
            yield return new("gaze/view/inverted", invertedViewArc.Resolve(player.Position, player.Rotation, null), false, enemyColor);
        foreach (var movement in encounter.Knockbacks(player))
        {
            var direction = movement.To - movement.From;
            var length = new Vector2(direction.X, direction.Z).Length();
            if (length < .0001f) continue;
            yield return new($"knockback/{movement.Source}/line", RayRectangleTemplate.Symmetric(.05f, length)
                .Resolve(movement.From, MathF.Atan2(direction.X, direction.Z), null), false, dangerColor);
            yield return new($"knockback/{movement.Source}/landing", actor.Resolve(movement.To, player.Rotation, null), false, dangerColor);
        }
        var stage = 0;
        foreach (var movement in encounter.MarchMovements(player))
        {
            var length = Vector2.Distance(new(movement.From.X, movement.From.Z), new(movement.To.X, movement.To.Z));
            if (length < .0001f) continue;
            yield return new($"march/{player.Id}/{stage}/line", RayRectangleTemplate.Symmetric(.05f, length)
                .Resolve(movement.From, movement.Rotation, null), false, dangerColor);
            yield return new($"march/{player.Id}/{stage}/landing", actor.Resolve(movement.To, movement.Rotation, null), false, dangerColor);
            ++stage;
        }
    }

    private static LocalPolygonTemplate Eye()
    {
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

    private static LocalPolygonTemplate ViewArc(float start, float end, int segments)
    {
        var points = new List<Vector2>();
        for (var i = 0; i <= segments; ++i)
        {
            var angle = start + (end - start) * i / segments;
            points.Add(new(MathF.Sin(angle) * 1.035f, -MathF.Cos(angle) * 1.035f));
        }
        for (var i = segments; i >= 0; --i)
        {
            var angle = start + (end - start) * i / segments;
            points.Add(new(MathF.Sin(angle) * .965f, -MathF.Cos(angle) * .965f));
        }
        return new(points);
    }
}
