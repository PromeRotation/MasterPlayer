using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, DungeonPendingCast Cast)> orcusRings = [];
    private readonly Dictionary<ulong, DungeonPendingCast> orcusStarted = [];
    private ulong orcusRingId;
    private bool IsOrcus => territory == JobQuestCatalog.KILLING_ART;

    private void OrcusCast(DungeonPendingCast cast)
    {
        if (!IsOrcus || cast.Rule.Action is not (27602 or 27600 or 27603 or 27601)) return;
        if (orcusStarted.TryGetValue(cast.Source, out var previous)
            && previous.Rule.Action == cast.Rule.Action
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            var index = orcusRings.FindIndex(r => ReferenceEquals(r.Cast, previous));
            if (index >= 0) orcusRings[index] = (orcusRings[index].Id, cast);
        }
        else
            orcusRings.Add((++orcusRingId, cast));
        orcusStarted[cast.Source] = cast;
    }

    private void OrcusFinish(DungeonPendingCast cast)
    {
        if (!IsOrcus || cast.Rule.Action is not (27602 or 27600 or 27603 or 27601)) return;
        orcusStarted.Remove(cast.Source);
        if (orcusRings.Count > 0) orcusRings.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> OrcusAoes()
    {
        if (!IsOrcus || Active == null) yield break;
        foreach (var (id, cast) in orcusRings.Take(4))
        {
            var inner = cast.Rule.Action switch { 27602 => 12, 27600 => 2, 27603 => 17, _ => 7 };
            yield return new($"orcus/ring/{cast.Source}/{id}", "Carnem Levare", new PtDonutSectorTemplate(inner, inner + 5, MathF.PI),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
    }
}
