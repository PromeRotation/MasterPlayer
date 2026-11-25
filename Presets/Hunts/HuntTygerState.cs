using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntTygerState
{
    private static readonly GeometryTemplate cone = new FanTemplate(18, MathF.PI / 2);
    private HuntPendingCast? prediction;
    private Vector2 lastPosition;
    private float lastRotation;
    private bool moved;

    internal IEnumerable<HuntPendingCast> Areas => prediction is { } area ? [area] : [];

    internal void Start(HuntPendingCast cast, ReplayActorState actor)
    {
        if (cast.Rule.Action is not (16962 or 16963)) return;
        prediction = cast with
        {
            Shape = cone, Rotation = cast.Rotation + MathF.PI, Activation = cast.Activation.AddSeconds(2.3d),
            SequenceId = $"{cast.Source}/scorpions-sting"
        };
        lastPosition = new(actor.Position.X, actor.Position.Z);
        lastRotation = cast.Rotation;
        moved = false;
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (prediction is not { } area || area.Order != previous.Order) return;
        prediction = area with
        {
            Input = updated.Input, Order = updated.Order, Activation = updated.Activation.AddSeconds(2.3d),
            Location = moved ? area.Location : updated.Location, Rotation = moved ? area.Rotation : updated.Rotation + MathF.PI
        };
        if (!moved) lastRotation = updated.Rotation;
    }

    internal void Update(ReplayActorState actor)
    {
        var position = new Vector2(actor.Position.X, actor.Position.Z);
        if (prediction == null || lastPosition == position && lastRotation == actor.Rotation) return;
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        var origin = new Vector3(((int)MathF.Round(actor.Position.X * inverse) - .5f) * grid, actor.Position.Y,
            ((int)MathF.Round(actor.Position.Z * inverse) - .5f) * grid);
        // The source explicitly keeps the updated distance field facing forward while the outline faces backward.
        prediction = prediction with { Location = origin, Rotation = actor.Rotation + MathF.PI, NavigationRotation = actor.Rotation };
        lastPosition = position;
        lastRotation = actor.Rotation;
        moved = true;
    }

    internal void Effect(uint action, DateTime at)
    {
        if (action == 16961 && prediction != null && at >= prediction.StartedAt) prediction = null;
    }
}
