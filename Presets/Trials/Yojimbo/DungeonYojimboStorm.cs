using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(bool Center, Vector3 Position, float Rotation, DateTime At)> yojimboStorms = [];
    private ulong yojimboStormCaster;
    private Vector3 yojimboLockedPosition;
    private float yojimboLockedRotation;
    private DateTime yojimboStormAt;
    private DateTime yojimboSeaAt;
    private bool yojimboSides;

    private void YojimboStormCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 14405 && _actors.TryGetValue(cast.Source, out var caster))
        {
            yojimboStormCaster = caster.Id;
            yojimboLockedPosition = caster.Position;
            yojimboLockedRotation = caster.Rotation;
            yojimboStormAt = _now.AddSeconds(5);
            yojimboStorms.Add((true, caster.Position, caster.Rotation, yojimboStormAt));
        }
        if (cast.Rule.Action != 14409) return;
        var totalTime = (float)((cast.FinishAt - cast.StartedAt).TotalSeconds - .3);
        yojimboSeaAt = _now.AddSeconds(totalTime);
        if (yojimboSides) return;
        var perpendicular = new Vector3(MathF.Cos(yojimboLockedRotation), 0, -MathF.Sin(yojimboLockedRotation)) * 10.5f;
        yojimboStorms.Add((false, yojimboLockedPosition - perpendicular, yojimboLockedRotation, yojimboSeaAt));
        yojimboStorms.Add((false, yojimboLockedPosition + perpendicular, yojimboLockedRotation, yojimboSeaAt));
        yojimboSides = true;
    }
}
