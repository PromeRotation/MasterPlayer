using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntRebellionState(ulong owner)
{
    private static readonly GeometryTemplate blizzardCone = new FanTemplate(40, MathF.PI / 4);
    private static readonly GeometryTemplate cycloneCone = new FanTemplate(28, MathF.PI);
    private readonly HuntRebellionRotationState blizzards = new();
    private readonly HuntRebellionRotationState cyclones = new();
    private readonly HuntMarchState march = new();
    private readonly List<HuntPendingCast> ordinary = [];
    private HuntPendingCast? firstCast;
    private HuntPendingCast? rotationCast;
    private float increment;
    private long serial;
    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            blizzards.ColorCandidates();
            return blizzards.Areas.Concat(cyclones.Areas).Select(c => c with { Source = owner });
        }
    }

    internal void Start(HuntPendingCast cast, ReplayActorState caster)
    {
        if (cast.Rule.Action is 17601 or 17602) ordinary.Add(cast);
        switch (cast.Rule.Action)
        {
            case 17628:
                firstCast = cast;
                Add(0, 0, 0);
                Add(MathF.PI / 4, 1.3d, 1);
                Add(-MathF.PI / 4, 1.3d, 2);
                break;
            case 17629:
            case 18080:
                if (!blizzards.HasSequence)
                {
                    blizzards.Start(cast with
                    {
                        Shape = blizzardCone, Stage = 0, SequenceId = $"{owner}/rebellion/blizzard/{serial++}"
                    }, BlizzardIncrement(cast.Rotation), 1.3d, 7);
                }
                break;
            case 18078:
            case 18079:
                rotationCast = cast;
                InitCyclone(caster);
                break;
        }

        void Add(float offset, double delay, int stage) => blizzards.AddCandidate(cast with
        {
            Shape = blizzardCone, Rotation = cast.Rotation + offset, Activation = cast.Activation.AddSeconds(delay),
            Stage = stage, SequenceId = $"{owner}/rebellion/candidate/{serial++}"
        });
    }

    internal void Icon(ReplayActorState actor, uint icon)
    {
        if (icon is not (167 or 168)) return;
        increment = icon == 167 ? -MathF.PI / 2 : MathF.PI / 2;
        InitCyclone(actor);
    }

    private void InitCyclone(ReplayActorState actor)
    {
        // The upstream component treats a zero spell rotation as not ready.
        if (rotationCast == null || rotationCast.Rotation == 0 || increment == 0) return;
        var cast = rotationCast;
        cyclones.Start(cast with
        {
            Shape = cycloneCone, Location = Quantized(actor.Position), Activation = cast.Activation.AddSeconds(5.2d),
            Stage = 0, SequenceId = $"{owner}/rebellion/cyclone/{serial++}"
        }, increment, 1.7d, 6);
        rotationCast = null;
        increment = 0;
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, ReplayActorState caster)
    {
        for (var i = 0; i < ordinary.Count; ++i)
            if (ordinary[i].Source == previous.Source && ordinary[i].Order == previous.Order) ordinary[i] = updated;
        blizzards.Correct(previous, updated, (_, c) => c.Location,
            updated.Rule.Action is 17629 or 18080 ? BlizzardIncrement(updated.Rotation) : null);
        // An icon arriving later locks its own actor position; never replace that origin with spell.LocXZ.
        cyclones.Correct(previous, updated, (area, _) => area.Location);
        if (updated.Rule.Action == 17628 && firstCast?.Source == previous.Source && firstCast.Order == previous.Order)
            firstCast = updated;
        if (rotationCast?.Order == previous.Order && rotationCast.Source == previous.Source)
        {
            rotationCast = updated;
            InitCyclone(caster);
        }
    }

    internal void Finish(ulong source, uint action, DateTime now)
    {
        if (action is 17601 or 17602)
            ordinary.RemoveAll(c => c.Source == source && c.Rule.Action == action);
        if (action == 17628) blizzards.FinishCandidate();
        else if (action is 17629 or 18080) blizzards.Advance(now);
    }

    internal void Effect(uint action, DateTime now)
    {
        if (action is 18126 or 18127 or 18128) cyclones.Advance(now);
    }

    internal void Update(DateTime now)
    {
        blizzards.Update(now);
        cyclones.Update(now);
    }

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        ordinary.RemoveAll(c => !actors.ContainsKey(c.Source));
        march.Synchronize(actors);
    }
    internal void Status(ulong actor, uint status, DateTime at, ReplayStatus? value) => march.Status(actor, status, at, value);
    internal void Remove(ulong actor)
    {
        ordinary.RemoveAll(c => c.Source == actor);
        march.Remove(actor);
    }
    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> Movements(ReplayActorState player, DateTime now)
        => march.Movements(player, now);

    internal bool UnsafeMarch(ReplayActorState player, DateTime now)
    {
        var last = march.Movements(player, now).LastOrDefault();
        return last.From != last.To && new uint[] { 17601, 17602 }.Any(action =>
            ordinary.Where(c => c.Rule.Action == action).Take(1)
                .Any(c => c.Shape!.Resolve(c.Location, c.Rotation, null).Contains(last.To, 0)));
    }

    private float BlizzardIncrement(float rotation)
    {
        var delta = (firstCast?.Rotation ?? 0) - rotation;
        while (delta > MathF.PI) delta -= 2 * MathF.PI;
        while (delta < -MathF.PI) delta += 2 * MathF.PI;
        return delta > 0 ? -MathF.PI / 4 : MathF.PI / 4;
    }

    private static Vector3 Quantized(Vector3 position)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(position.X * inverse) - .5f) * grid, position.Y,
            ((int)MathF.Round(position.Z * inverse) - .5f) * grid);
    }
}
