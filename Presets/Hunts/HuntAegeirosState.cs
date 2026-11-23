using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntAegeirosState
{
    private HuntPendingCast? expected;
    private static readonly HuntCastRule canopy = new(27711, "Canopy", new FanTemplate(12, 2 * MathF.PI / 3));

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action == 27708)
            expected = cast with
            {
                Shape = new FanTemplate(40, MathF.PI),
                Activation = (cast.ObservedAt ?? cast.StartedAt).AddSeconds(9.6d),
                SequenceId = $"{cast.Source}/rimestorm"
            };
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (expected?.Input == previous.Input)
            expected = expected with { Input = updated.Input, StartedAt = updated.StartedAt };
    }

    internal void Finish(uint action)
    {
        if (action == 27709) expected = null;
    }

    internal IEnumerable<HuntPendingCast> Areas(ReplayActorState actor, HuntPendingCast? cast)
    {
        var rotation = cast?.Rotation ?? actor.Cast?.Rotation ?? actor.Rotation;
        if (cast?.Rule.Action == 27708)
            yield return cast with { Location = actor.Position, Shape = new CircleTemplate(10) };
        if (cast?.Rule.Action == 27709)
            yield return cast with { Location = actor.Position, Shape = new FanTemplate(40, MathF.PI) };
        else if (expected != null)
            yield return expected with { Location = actor.Position, Rotation = rotation };
    }

    internal static HuntPendingCast? Cleave(ReplayActorState actor, ReplayActorState? target, bool casting)
    {
        if (casting || !actor.Targetable || actor.Dead || target == null) return null;
        var delta = target.Position - actor.Position;
        var rotation = MathF.Atan2(delta.X, delta.Z);
        var input = new ReplayCast(27711, target.Id, actor.Position, rotation, default, default, true, false);
        return new(actor.Id, SimpleHuntCatalog.Find(actor.Oid)!, canopy, Quantize(actor.Position), rotation,
            default, default, target.Id, false, true, canopy.Shape, input, input,
            SequenceId: $"{actor.Id}/canopy", OutlineOnly: true);
    }

    internal static IEnumerable<HuntPendingCast> Dangers(HuntPendingCast cleave, ReplayActorState boss, ReplayActorState player,
        IEnumerable<ReplayActorState> party)
    {
        if (player.Id != cleave.TargetId)
        {
            yield return cleave;
            yield break;
        }
        foreach (var ally in party.Where(a => a.Id != player.Id))
        {
            var delta = ally.Position - boss.Position;
            yield return cleave with
            {
                Shape = new FanTemplate(100, 2 * MathF.PI / 3),
                Rotation = MathF.Atan2(delta.X, delta.Z), SequenceId = $"{boss.Id}/canopy/avoid/{ally.Id}"
            };
        }
    }

    internal static bool Hit(HuntPendingCast cleave, ReplayActorState player)
        => player.Id != cleave.TargetId && cleave.Shape!.Resolve(cleave.Input.Location, cleave.Rotation, null)
            .Contains(Quantize(player.Position), 0);

    private static Vector3 Quantize(Vector3 position)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(position.X * inverse) - .5f) * grid, position.Y,
            ((int)MathF.Round(position.Z * inverse) - .5f) * grid);
    }
}
