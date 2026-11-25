using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureNarkissos => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.NARKISSOS;
    private readonly TreasurePlayerStatusState treasurePlayerStatuses = new();
    private int treasureSapCasts;
    private bool treasureSapActive;
    internal bool TreasureMisdirectionActive => IsTreasureNarkissos && treasureParty.Contains(PlayerId)
        && treasurePlayerStatuses.Misdirected(PlayerId);
    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> TreasureMarchMovements =>
        IsTreasureNarkissos && _actors.GetValueOrDefault(PlayerId) is { } player
            ? treasurePlayerStatuses.Movements(TreasureStatusActor(player), _now) : [];

    private static ReplayActorState TreasureStatusActor(DungeonActor actor) => new()
    {
        Id = actor.Id,
        Oid = actor.Oid,
        Position = actor.Position,
        Rotation = actor.Rotation,
        Dead = actor.Dead,
        // Expirations carries actual occupied raw slots even after the script's active-status filter expires.
        Statuses = (actor.StatusExpirations?.Keys ?? []).Concat(actor.Statuses ?? []).Distinct()
            .Select(id => new ReplayStatus(id, actor.StatusParams?.GetValueOrDefault(id) ?? 0,
                actor.StatusSources?.GetValueOrDefault(id) ?? 0,
                actor.StatusExpirations?.TryGetValue(id, out var expiry) == true ? expiry : DateTime.MaxValue)).ToArray(),
    };

    private void TreasurePlayerStatusesObserve()
    {
        if (IsTreasureNarkissos)
            treasurePlayerStatuses.Observe(_actors.ToDictionary(p => p.Key, p => TreasureStatusActor(p.Value)), _now);
    }

    private void TreasureNarkissosStart(DungeonPendingCast cast, bool correction)
    {
        if (!IsTreasureNarkissos || cast.Rule.Action != 32336 || correction) return;
        ++treasureSapCasts;
        treasureSapActive = true;
    }

    private void TreasureNarkissosFinish(DungeonPendingCast cast)
    {
        if (IsTreasureNarkissos && cast.Rule.Action == 32336) treasureSapActive = false;
    }

    private IEnumerable<DungeonAmbientAoe> TreasureNarkissosAoes()
    {
        if (!IsTreasureNarkissos) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 32336))
            yield return new($"treasure/sap/{cast.Source}", cast.Rule.Name, new CircleTemplate(8),
                cast.Location, Activation: cast.FinishAt, Color: new Vector4(1, 0, 0, 1));
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var index = 0;
        foreach (var movement in TreasureMarchMovements)
            foreach (var aoe in QuestKnockbackProjection($"treasure/march/{index++}", player, movement.From, movement.To))
                yield return aoe with { Name = "Brainstorm / Forced march",
                    Rotation = aoe.Id.EndsWith("/player") ? movement.Rotation : aoe.Rotation };
    }

    private IEnumerable<string> TreasureNarkissosHints()
    {
        if (!IsTreasureNarkissos) yield break;
        if (treasureSapActive)
            yield return treasureSapCasts <= 4 ? "Circles resolve before cross"
                : "Circles resolve before cross, aim forced march into cross";
        if (_casts.Values.Any(c => c.Rule.Action == 32331)) yield return "Applies temporary misdirection";
        if (TreasureMisdirectionActive) yield return "SpecialMode: Misdirection";
        var last = TreasureMarchMovements.LastOrDefault();
        // This instance checks Sap only, regardless of its predicted activation or arena/cross.
        if (last.From != last.To && _casts.Values.Where(c => c.Rule.Action == 32336)
            .Any(c => new CircleTemplate(8).Resolve(c.Location, 0, null).Contains(last.To, 0)))
            yield return "Aim for safe spot!";
    }

    private void ResetTreasureNarkissos()
    {
        treasurePlayerStatuses.Clear();
        treasureSapCasts = 0;
        treasureSapActive = false;
    }
}
