using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntTarchiaState
{
    private static readonly HuntCastRule SPIN = new(18093, "MightySpin2", new CircleTemplate(14));
    private HuntPendingCast? area;

    internal HuntTarchiaState(ReplayActorState actor, DateTime now)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        var location = new Vector3(((int)MathF.Round(actor.Position.X * inverse) - .5f) * grid, actor.Position.Y,
            ((int)MathF.Round(actor.Position.Z * inverse) - .5f) * grid);
        var input = new ReplayCast(18093, 0, location, 0, now, now, false, false);
        area = new(actor.Id, SimpleHuntCatalog.Find(actor.Oid)!, SPIN, location, 0, now, default, 0, false, false,
            SPIN.Shape, input, null, SequenceId: $"{actor.Id}/mighty-spin2");
    }

    internal IEnumerable<HuntPendingCast> Areas => area == null ? [] : [area];

    internal void Finish(HuntPendingCast cast, DateTime now)
    {
        if (cast.Rule.Action != 18023) return;
        // Groundstorm's sheet ExtraCastTime is zero. The source clamps ElapsedTime
        // to TotalTime before CastFinished, leaving the NPC 0.3s delay on a late end.
        var total = cast.Input.TotalTime ?? (float)((cast.Activation - cast.StartedAt).TotalSeconds - .3);
        var elapsed = Math.Min((float)(now - cast.StartedAt).TotalSeconds, total);
        var activation = now.AddSeconds(total + .3f - elapsed + 4d);
        area = cast with
        {
            Rule = SPIN, Shape = SPIN.Shape, StartedAt = now, Activation = activation,
            SequenceId = $"{cast.Source}/mighty-spin2", Rotation = 0, Followup = true,
        };
    }

    internal void Effect(uint action, DateTime at)
    {
        if (action != 18023 && area != null && at >= area.StartedAt) area = null;
    }
}
