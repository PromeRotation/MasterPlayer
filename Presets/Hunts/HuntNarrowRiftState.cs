using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntNarrowRiftState
{
    private readonly List<(GeometryTemplate Shape, HuntPendingCast Cast)> shapes = [];
    private HuntPendingCast? beam;

    internal void Start(HuntPendingCast cast)
    {
        switch (cast.Rule.Action)
        {
            case 27487: shapes.Add((new CircleTemplate(10), cast)); break;
            case 27488: shapes.Add((new DonutTemplate(6, 40), cast)); break;
            case 27331:
                shapes.Add((new CircleTemplate(10), cast));
                shapes.Add((new DonutTemplate(6, 40), cast));
                break;
            case 27332:
                shapes.Add((new DonutTemplate(6, 40), cast));
                shapes.Add((new CircleTemplate(10), cast));
                break;
        }
    }

    internal void Finish()
    {
        if (shapes.Count != 0) shapes.RemoveAt(0);
    }

    internal void Effect(ReplayActorState actor, uint action, DateTime at)
    {
        if (action == 27516 && beam?.StartedAt <= at) beam = null;
        if (action != 27333 || beam?.StartedAt > at) return;
        var definition = SimpleHuntCatalog.Find(actor.Oid)!;
        var rule = new HuntCastRule(action, "VanishingRay", null);
        var cast = new ReplayCast(action, actor.Id, actor.Position, actor.Rotation, at, at.AddSeconds(4), false, false);
        beam = new(actor.Id, definition, rule, actor.Position, actor.Rotation, at, cast.FinishAt,
            actor.Id, false, true, RayRectangleTemplate.Symmetric(8, 50), cast, cast, SequenceId: $"{actor.Id}/beam");
    }

    internal IEnumerable<HuntPendingCast> Areas(ReplayActorState actor, HuntPendingCast? current)
    {
        if (shapes.Count != 0)
            yield return shapes[0].Cast with
            {
                Shape = shapes[0].Shape, Location = actor.Position, Rotation = 0,
                Activation = current?.Activation ?? default, SequenceId = $"{actor.Id}/promise"
            };
        if (beam != null) yield return beam with { Location = actor.Position, Rotation = actor.Rotation };
    }
}
