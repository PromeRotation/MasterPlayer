using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    // CastGaze uses the same eye silhouette and forward 90-degree arc.
    private static readonly LocalPolygonTemplate eddaEyeSymbol = CreateQueenEyeSymbol();
    private static readonly LocalPolygonTemplate eddaGazeArc = CreateQueenGazeArc();

    private IEnumerable<DungeonAmbientAoe> EddaGazeDrawing()
    {
        if (territory is not (HeavenswardIntroCatalog.EDDA or JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS or JobQuestCatalog.GAMBOLING_FOR_GIL) && !IsFadedThordan && !HasTreasureGaze && !IsTactical && !HasCarnivaleGaze && !IsHeavenOnHigh
            || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var prefix = HasTreasureGaze ? "treasure" : IsFadedThordan ? "thordan" : territory == HeavenswardIntroCatalog.EDDA ? "edda" : IsGamboling ? "gamboling" : IsPortrait ? "portrait" : "griffin";
        if (IsTreasureTigris) prefix = "tigris";
        if (IsTactical) prefix = "tactical";
        if (HasCarnivaleGaze) prefix = "carnivale";
        if (IsHeavenOnHigh) prefix = "hoh";
        var forward = new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
        var showArc = false;
        foreach (var gaze in Gazes.Where(c => c.Rule.Action is 11900 or 11922 or 14691 or 14951 or 14875 or 6403 or 37523 or 37537 or 17198 or 21090 or 40743 or 40757 || NativeTreasureCatalog.GazeAction(c.Rule.Action)))
        {
            var offset = new Vector2(gaze.Location.X - player.Position.X, gaze.Location.Z - player.Position.Z);
            var dangerous = offset.LengthSquared() > 0
                && Vector2.Dot(forward, Vector2.Normalize(offset)) >= .707107f;
            var color = dangerous ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1);
            yield return new($"{prefix}/gaze/{gaze.Source}/eye", "背对视线", eddaEyeSymbol, gaze.Location,
                Navigate: false, Safe: !dangerous, Unclipped: true, Color: color);
            yield return new($"{prefix}/gaze/{gaze.Source}/pupil", "视线瞳孔", new CircleTemplate(.28f), gaze.Location,
                Navigate: false, Safe: !dangerous, Unclipped: true, Color: new Vector4(.04f, .04f, .04f, 1));
            showArc |= offset.LengthSquared() <= 10000f * 10000f;
        }
        if (showArc)
            yield return new($"{prefix}/gaze/view", "视线前方九十度判定弧", eddaGazeArc, player.Position, player.Rotation,
                Navigate: false, Unclipped: true, Color: new Vector4(1, 0, 0, 1));
    }

    private bool EddaGazeDanger(Vector3 player)
    {
        if (territory is not (HeavenswardIntroCatalog.EDDA or JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS or JobQuestCatalog.GAMBOLING_FOR_GIL) && !IsFadedThordan && !HasTreasureGaze && !IsTactical && !HasCarnivaleGaze && !IsHeavenOnHigh
            || _actors.GetValueOrDefault(PlayerId) is not { } actor) return false;
        var forward = new Vector2(MathF.Sin(actor.Rotation), MathF.Cos(actor.Rotation));
        return Gazes.Where(c => c.Rule.Action is 11900 or 11922 or 14691 or 14951 or 14875 or 6403 or 37523 or 37537 or 17198 or 21090 or 40743 or 40757 || NativeTreasureCatalog.GazeAction(c.Rule.Action)).Any(c =>
        {
            var offset = new Vector2(c.Location.X - player.X, c.Location.Z - player.Z);
            return offset.LengthSquared() <= 10000f * 10000f
                && offset.LengthSquared() > 0 && Vector2.Dot(forward, Vector2.Normalize(offset)) >= .707107f;
        });
    }
}
