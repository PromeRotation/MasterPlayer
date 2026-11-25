using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<TreasureIceSequence> treasureIceSequences = [];
    private long treasureIceId;
    internal IReadOnlyList<TreasureIceSequence> TreasureIceSequences => treasureIceSequences;
    private bool IsTreasureLyssa => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.LYSSA_CHRYSINE;
    private bool IsTreasureStyphnolobion => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_STYPHNOLOBION;
    private bool IsTreasureFastitocalon => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.FASTITOCALON;
    private bool IsTreasurePaeonia => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.PAEONIA;

    private void TreasureIceStart(DungeonPendingCast cast, bool correction)
    {
        if (Arena == null || !(IsTreasureLyssa && cast.Rule.Action is 32310 or 32312
            || IsTreasureStyphnolobion && cast.Rule.Action == 32247
            || IsTreasureFastitocalon && cast.Rule.Action == 43664
            || IsTreasurePaeonia && cast.Rule.Action is 43710 or 43713)) return;
        var index = correction ? treasureIceSequences.FindIndex(s => s.Cast.Source == cast.Source
            && s.Cast.StartedAt == cast.StartedAt && s.Cast.Rule.Action == cast.Rule.Action && s.Order == 0) : -1;
        if (correction && index < 0) return;
        var origin = IsTreasureFastitocalon || IsTreasurePaeonia ? cast.Location : QuantizedDungeonPosition(Arena.Position);
        var activation = IsTreasureFastitocalon || IsTreasurePaeonia ? cast.FinishAt : cast.FinishAt.AddSeconds(.5);
        var value = new TreasureIceSequence(index < 0 ? ++treasureIceId : treasureIceSequences[index].Id, cast, origin, activation);
        if (index < 0) treasureIceSequences.Add(value);
        else treasureIceSequences[index] = value;
    }

    private void TreasureIceFinish(DungeonPendingCast cast, DateTime time)
    {
        if (IsTreasurePaeonia)
        {
            var (visual, order) = cast.Rule.Action switch
            {
                43710 => (43710u, 0),
                43712 => (43710u, 1),
                43713 => (43713u, 0),
                43709 => (43713u, 1),
                _ => (0u, -1),
            };
            var index = treasureIceSequences.FindIndex(s => s.Cast.Rule.Action == visual && s.Order == order
                && MathF.Abs(s.Origin.X - cast.Location.X) <= 1 && MathF.Abs(s.Origin.Z - cast.Location.Z) <= 1);
            if (index >= 0)
            {
                if (order == 1) treasureIceSequences.RemoveAt(index);
                else treasureIceSequences[index] = treasureIceSequences[index] with { Order = 1, Activation = time.AddSeconds(2) };
            }
            return;
        }
        if (!(IsTreasureLyssa && cast.Rule.Action is 32311 or 32313
            || IsTreasureStyphnolobion && cast.Rule.Action is 32248 or 32249
            || IsTreasureFastitocalon && cast.Rule.Action is >= 43664 and <= 43666)) return;
        if (IsTreasureFastitocalon)
        {
            var order = (int)cast.Rule.Action - 43664;
            var index = treasureIceSequences.FindIndex(s => s.Order == order
                && MathF.Abs(s.Origin.X - cast.Location.X) <= 1 && MathF.Abs(s.Origin.Z - cast.Location.Z) <= 1);
            if (index < 0) return;
            if (order == 2) treasureIceSequences.RemoveAt(index);
            else treasureIceSequences[index] = treasureIceSequences[index] with { Order = order + 1, Activation = time.AddSeconds(3) };
            return;
        }
        // Both source components receive the same Finish. Each advances its first independently matching sequence.
        foreach (var visual in IsTreasureStyphnolobion ? new uint[] { 32247 } : new uint[] { 32310, 32312 })
        {
            var firstStrike = visual == 32247 ? cast.Rule.Action == 32248
                : (visual == 32310) == (cast.Rule.Action == 32311);
            var order = firstStrike ? 0 : 1;
            var index = treasureIceSequences.FindIndex(s => s.Cast.Rule.Action == visual && s.Order == order
                && MathF.Abs(s.Origin.X - cast.Location.X) <= 1 && MathF.Abs(s.Origin.Z - cast.Location.Z) <= 1);
            if (index < 0) continue;
            if (order == 1) treasureIceSequences.RemoveAt(index);
            else treasureIceSequences[index] = treasureIceSequences[index] with { Order = 1, Activation = time.AddSeconds(2) };
        }
    }

    private IEnumerable<DungeonAmbientAoe> TreasureIceAoes()
    {
        foreach (var sequence in treasureIceSequences)
        {
            var circle = (sequence.Cast.Rule.Action is 32310 or 32247 or 43664 or 43710) == (sequence.Order == 0);
            GeometryTemplate shape = circle ? new CircleTemplate(10) : new DonutTemplate(10, IsTreasurePaeonia ? 30 : 20);
            if (sequence.Order == 2) shape = new DonutTemplate(20, 30);
            var name = circle ? "Frigid Needle" : "Circle of Ice";
            if (sequence.Cast.Rule.Action == 32247) name = "Earth Quaker";
            if (sequence.Cast.Rule.Action == 43664) name = "Tremblor";
            if (IsTreasurePaeonia) name = circle ? "Epicenter Shock" : "Levinroot Ring";
            yield return new($"treasure/ice/{sequence.Id}", name,
                shape, sequence.Origin, Activation: sequence.Activation);
        }
    }
}
