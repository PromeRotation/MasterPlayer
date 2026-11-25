using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DateTime? arashiActivation;
    private DateTime arashiResolvedAt;
    private readonly Dictionary<ulong, DungeonPendingCast> arashiStarted = [];

    private void SamuraiCast(DungeonPendingCast cast)
    {
        if (territory != JobQuestCatalog.FACE_OF_TRUE_EVIL || cast.Rule.Action is not (8419 or 8643 or 8642)) return;
        if (arashiStarted.TryGetValue(cast.Source, out var previous) && previous.Rule.Action == cast.Rule.Action
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75) return;
        arashiStarted[cast.Source] = cast;
        arashiActivation = cast.FinishAt;
    }

    private IEnumerable<DungeonAmbientAoe> SamuraiAoes()
    {
        if (territory != JobQuestCatalog.FACE_OF_TRUE_EVIL || Active == null || arashiActivation is not { } activation) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1BF0))
            yield return new($"musosai/arashi/{actor.Id}", "Arashi", new CircleTemplate(4), actor.Position, Activation: activation);
    }
}
