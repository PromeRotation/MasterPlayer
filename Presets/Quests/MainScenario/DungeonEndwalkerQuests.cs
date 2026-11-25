using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsScarmiglioneQuest => territory == MainScenarioQuestCatalog.WHERE_EVERYTHING_BEGINS;
    private bool IsFrostyQuest => territory == MainScenarioQuestCatalog.FROSTY_RECEPTION;
    private bool IsColibriQuest => territory == MainScenarioQuestCatalog.GAME_IS_AFOOT;
    private bool ScarmiglioneQuestPull => _actors.Values.Any(a => a.Oid is 0x39B5 or 0x39B6 or 0x39B4 && a.InCombat);
    private readonly List<(DungeonActor Actor, DateTime Activation, bool Removed, int Key)> frostyLockPredictions = [];
    private readonly HashSet<ulong> frostyLockKnown = [];
    private readonly List<DungeonPendingCast> frostyLockCasts = [];
    private int frostyLockKey;
    private readonly Dictionary<ulong, (DungeonPendingCast Cast, Vector3 Origin, Vector3 Start)> endwalkerCharges = [];

    private void EndwalkerQuestObserve()
    {
        if (!IsFrostyQuest || Active == null) return;
        for (var i = 0; i < frostyLockPredictions.Count; ++i)
        {
            var prediction = frostyLockPredictions[i];
            if (!prediction.Removed && _actors.TryGetValue(prediction.Actor.Id, out var actor) && actor.Oid == 0x3648)
                frostyLockPredictions[i] = (actor, prediction.Activation, false, prediction.Key);
            else
                frostyLockPredictions[i] = (prediction.Actor, prediction.Activation, true, prediction.Key);
        }
        foreach (var id in frostyLockKnown.Where(id => _actors.GetValueOrDefault(id)?.Oid != 0x3648).ToArray())
            frostyLockKnown.Remove(id);
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x3648))
            if (frostyLockKnown.Add(actor.Id))
                frostyLockPredictions.Add((actor, _now.AddSeconds(6.6), false, ++frostyLockKey));
    }

    private void EndwalkerQuestCast(DungeonPendingCast cast)
    {
        if (IsFrostyQuest && cast.Rule.Action is 27461 or 27463)
        {
            var existing = frostyLockCasts.FindIndex(c => c.Source == cast.Source
                && Math.Abs((c.StartedAt - cast.StartedAt).TotalSeconds) < .25
                && Math.Abs((c.FinishAt - cast.FinishAt).TotalSeconds) < .75);
            if (existing >= 0)
                frostyLockCasts[existing] = cast;
            else
            {
                var prediction = frostyLockPredictions.FindIndex(p => !p.Removed && p.Actor.Id == cast.Source);
                if (prediction >= 0) frostyLockPredictions.RemoveAt(prediction);
                frostyLockCasts.Add(cast);
            }
        }
        if (!IsColibriQuest || cast.Rule.Action is not (35717 or 35888)
            || !_actors.TryGetValue(cast.Source, out var source)) return;
        var same = endwalkerCharges.TryGetValue(cast.Source, out var previous)
            && previous.Cast.Rule.Action == cast.Rule.Action
            && Math.Abs((previous.Cast.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.Cast.FinishAt - cast.FinishAt).TotalSeconds) < .75;
        var start = same ? previous.Start : source.Position;
        endwalkerCharges[cast.Source] = (cast, QuantizedDungeonPosition(start), start);
    }

    private void EndwalkerQuestFinish(DungeonPendingCast cast)
    {
        // This component consumes the first actual AOE on every corresponding finish,
        // even if another caster supplied that AOE.
        if (IsFrostyQuest && cast.Rule.Action is 27461 or 27463 && frostyLockCasts.Count > 0)
            frostyLockCasts.RemoveAt(0);
        if (IsColibriQuest && cast.Rule.Action is 35717 or 35888)
            endwalkerCharges.Remove(cast.Source);
    }

    private void EndwalkerQuestRemoved(ulong source)
    {
        frostyLockKnown.Remove(source);
        for (var i = 0; i < frostyLockPredictions.Count; ++i)
        {
            var prediction = frostyLockPredictions[i];
            if (prediction.Actor.Id == source)
                frostyLockPredictions[i] = (prediction.Actor, prediction.Activation, true, prediction.Key);
        }
    }

    private IEnumerable<DungeonAmbientAoe> EndwalkerQuestAoes()
    {
        if (Active == null) yield break;
        if (IsFrostyQuest)
        {
            foreach (var prediction in frostyLockPredictions)
                yield return new($"frosty/lock/prediction/{prediction.Key}", "Lock On", new CircleTemplate(6),
                    prediction.Actor.Position, Activation: prediction.Activation);
            foreach (var (cast, i) in frostyLockCasts.Select((c, i) => (c, i)))
                yield return new($"frosty/lock/cast/{i}", cast.Rule.Name, new CircleTemplate(6),
                    cast.Location, Activation: cast.FinishAt);
        }
        if (IsColibriQuest)
        {
            var threeMembers = _actors.Values.Count(a => a.PartySlot >= 0) == 3;
            foreach (var (source, charge) in endwalkerCharges)
            {
                var delta = charge.Cast.Location - charge.Start;
                var length = new Vector2(delta.X, delta.Z).Length();
                yield return new($"colibri/charge/{source}", charge.Cast.Rule.Name, RayRectangleTemplate.Symmetric(16, length),
                    charge.Origin, MathF.Atan2(delta.X, delta.Z), Navigate: charge.Cast.Rule.Action != 35888 || threeMembers,
                    Activation: charge.Cast.FinishAt);
            }
        }
    }

    private IEnumerable<string> EndwalkerQuestHints(Vector3? player)
    {
        if (player is { } position && EndwalkerQuestAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from aoe!";
    }
}
