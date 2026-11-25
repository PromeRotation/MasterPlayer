using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureBasket => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.SECRET_BASKET;
    private bool IsTreasurePorxie => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.SECRET_PORXIE;
    private readonly List<TreasureConcentricStrike> treasureStrikes = [];
    private readonly List<TreasureSweepLine> treasureSweeps = [];
    private readonly Dictionary<ulong, long> treasureSequenceCasts = [];
    private readonly List<DungeonAmbientAoe> treasureSweepAoes = [];
    private DateTime? treasureEarthquake;
    private DungeonPendingCast? treasureEarthquakeCast;
    private long treasureSequenceId;

    private void TreasureSequenceStart(DungeonPendingCast cast, bool correction)
    {
        if (IsTreasureBasket && cast.Rule.Action == 21729)
        {
            // RaidwideCastDelay persists through cancellation and only the real effect clears it.
            if (!correction || treasureEarthquakeCast is { } current
                && current.Source == cast.Source && current.StartedAt == cast.StartedAt)
            {
                treasureEarthquake = cast.FinishAt.AddSeconds(1.2d);
                treasureEarthquakeCast = cast;
            }
        }
        if (IsTreasureBasket && cast.Rule.Action == 21724)
        {
            var index = correction && treasureSequenceCasts.TryGetValue(cast.Source, out var id)
                ? treasureStrikes.FindIndex(s => s.Id == id && s.Order == 0) : -1;
            if (correction && index < 0) return;
            var strike = new TreasureConcentricStrike(index < 0 ? ++treasureSequenceId : treasureStrikes[index].Id,
                cast.Source, cast.StartedAt, cast.Location, cast.Rotation, cast.FinishAt);
            if (index < 0) treasureStrikes.Add(strike);
            else treasureStrikes[index] = strike;
            treasureSequenceCasts[cast.Source] = strike.Id;
        }
        if (IsTreasurePorxie && cast.Rule.Action == 22937)
        {
            var index = correction && treasureSequenceCasts.TryGetValue(cast.Source, out var id)
                ? treasureSweeps.FindIndex(s => s.Id == id && s.Remaining == 4) : -1;
            if (correction && index < 0) return;
            var direction = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
            var line = new TreasureSweepLine(index < 0 ? ++treasureSequenceId : treasureSweeps[index].Id,
                cast.Source, cast.StartedAt, cast.SourcePosition ?? _actors[cast.Source].Position,
                direction * 12, cast.FinishAt.AddSeconds(.9d));
            if (index < 0) treasureSweeps.Add(line);
            else treasureSweeps[index] = line;
            treasureSequenceCasts[cast.Source] = line.Id;
            RebuildTreasureSweepAoes(_now);
        }
    }

    private void TreasureSequenceFinish(DungeonPendingCast cast, DateTime time)
    {
        treasureSequenceCasts.Remove(cast.Source);
        if (!IsTreasureBasket) return;
        var order = cast.Rule.Action switch { 21724 => 0, 21725 => 1, 21726 => 2, _ => -1 };
        if (order < 0) return;
        // ConcentricAOEs matches the first origin/rotation/order, independently of caster identity.
        var index = treasureStrikes.FindIndex(s => s.Order == order
            && MathF.Abs(s.Origin.X - cast.Location.X) <= 1 && MathF.Abs(s.Origin.Z - cast.Location.Z) <= 1
            && MathF.Abs(MathF.IEEERemainder(s.Rotation - cast.Rotation, MathF.Tau)) <= .05f);
        if (index < 0) return;
        if (order == 2) treasureStrikes.RemoveAt(index);
        else treasureStrikes[index] = treasureStrikes[index] with { Order = order + 1, Activation = time.AddSeconds(1.1d) };
    }

    private void TreasureSequenceEffect(ulong source, uint action, DateTime time)
    {
        if (IsTreasureBasket && action == 21730)
        {
            treasureEarthquake = null;
            treasureEarthquakeCast = null;
        }
        if (!IsTreasurePorxie || action != 21672 || !_actors.TryGetValue(source, out var caster)) return;
        var position = caster.Position;
        var index = treasureSweeps.FindIndex(l =>
            MathF.Abs(l.Next.X - position.X) <= 1 && MathF.Abs(l.Next.Z - position.Z) <= 1);
        if (index < 0) return;
        var line = treasureSweeps[index];
        if (line.Remaining == 1) treasureSweeps.RemoveAt(index);
        else treasureSweeps[index] = line with
        {
            Next = position + line.Advance,
            NextExplosion = time.AddSeconds(4.5d),
            Remaining = line.Remaining - 1,
        };
        RebuildTreasureSweepAoes(time);
    }

    private void RebuildTreasureSweepAoes(DateTime time)
    {
        // Exaflare rebuilds future timestamps on line changes, not every update.
        treasureSweepAoes.Clear();
        foreach (var line in treasureSweeps)
        {
            var futureStart = line.NextExplosion > time ? line.NextExplosion : time;
            for (var i = 1; i < Math.Min(line.Remaining, 3); ++i)
                treasureSweepAoes.Add(new($"treasure/sweep/{line.Id}/{i}", "Sweep", new CircleTemplate(6),
                    QuantizedDungeonPosition(line.Next + line.Advance * i), Activation: futureStart.AddSeconds(4.5d * i)));
        }
        foreach (var line in treasureSweeps)
            treasureSweepAoes.Add(new($"treasure/sweep/{line.Id}/0", "Sweep", new CircleTemplate(6),
                QuantizedDungeonPosition(line.Next), Activation: line.NextExplosion, Color: new Vector4(1, 0, 0, 1)));
    }

    private IEnumerable<DungeonAmbientAoe> TreasureSequenceAoes()
    {
        foreach (var strike in treasureStrikes)
        {
            GeometryTemplate shape = strike.Order switch
            {
                0 => new FanTemplate(6.5f, 3 * MathF.PI / 2),
                1 => new PtDonutSectorTemplate(6.5f, 12.5f, 3 * MathF.PI / 2),
                _ => new PtDonutSectorTemplate(12.5f, 18.5f, 3 * MathF.PI / 2),
            };
            yield return new($"treasure/strike/{strike.Id}", "Heavy Strike", shape, strike.Origin,
                strike.Rotation, Activation: strike.Activation);
        }
        foreach (var aoe in treasureSweepAoes) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> TreasureEarthquakeDamage()
    {
        if (treasureEarthquake is not { } activation) yield break;
        for (var slot = 0; slot < treasureParty.Length; ++slot)
            if (_actors.GetValueOrDefault(treasureParty[slot]) is { Dead: false } member)
                yield return new(member.Id, slot, member.PartyRole, member.Name, activation, "Raidwide");
    }

    private void ResetTreasureSequences()
    {
        treasureStrikes.Clear();
        treasureSweeps.Clear();
        treasureSequenceCasts.Clear();
        treasureSweepAoes.Clear();
        treasureEarthquake = null;
        treasureEarthquakeCast = null;
        treasureSequenceId = 0;
    }
}
