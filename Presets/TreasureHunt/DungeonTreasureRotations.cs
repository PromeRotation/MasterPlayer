using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureCladoselache => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.SECRET_CLADOSELACHE;
    private bool IsTreasureTyphon => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.DAEN_TYPHON;
    private readonly List<TreasureRotatingSequence> treasureRotations = [];
    private readonly List<DungeonAmbientAoe> treasureRotationAoes = [];
    private readonly Dictionary<ulong, (uint Oid, bool Seen)> treasureRotationActors = [];
    private readonly Dictionary<ulong, uint> treasureDestroyedRotationActors = [];
    private TreasureCreatedCircle? treasureSnortsaultCircle;
    private DungeonPendingCast? treasureRotationCast;
    private bool treasureRotationPending;
    private float treasurePendingRotation;
    private float treasureRotationIncrement;
    private long treasureRotationId;
    internal IReadOnlyList<TreasureRotatingSequence> TreasureRotations => treasureRotations;
    internal TreasureCreatedCircle? TreasureSnortsaultCircle => treasureSnortsaultCircle;

    private void TreasureRotationStart(DungeonPendingCast cast, bool correction)
    {
        if (!IsTreasureCladoselache || cast.Rule.Action != 21706) return;
        if (correction && (treasureRotationCast is not { } current || current.Source != cast.Source
            || current.StartedAt != cast.StartedAt || !treasureRotationPending)) return;
        treasureRotationCast = cast;
        treasureRotationPending = true;
        treasurePendingRotation = cast.Rotation;
        if (_actors.GetValueOrDefault(cast.Source) is { } source) InitializeTreasureRotation(source);
    }

    private void TreasureRotationIcon(ulong actorId, uint icon)
    {
        if (!IsTreasureCladoselache || _actors.GetValueOrDefault(actorId) is not { } actor) return;
        treasureRotationIncrement = icon switch { 167 => -MathF.PI / 3, 168 => MathF.PI / 3, _ => 0 };
        InitializeTreasureRotation(actor);
    }

    private void InitializeTreasureRotation(DungeonActor actor)
    {
        // The source intentionally requires a nonzero initial facing and uses the pairing call's actor as origin.
        if (treasurePendingRotation == 0 || treasureRotationIncrement == 0 || treasureRotationCast == null) return;
        treasureRotations.Add(new(++treasureRotationId, actor.Id, actor.Oid, treasureRotationCast.StartedAt,
            QuantizedDungeonPosition(actor.Position), treasurePendingRotation, treasureRotationIncrement,
            treasureRotationCast.FinishAt, 2.1d, 6, 2, 40, MathF.PI / 3));
        treasurePendingRotation = 0;
        treasureRotationIncrement = 0;
        treasureRotationPending = false;
        RebuildTreasureRotationAoes(_now);
    }

    private void TreasureRotationCreated(ulong id, uint oid, float rotation, bool observed = false)
    {
        if (!IsTreasureTyphon || Arena == null || oid is not (0x3051 or 0x3050 or 0x3082)) return;
        if (observed && treasureDestroyedRotationActors.GetValueOrDefault(id) == oid) return;
        if (!observed) treasureDestroyedRotationActors.Remove(id);
        if (treasureRotationActors.TryGetValue(id, out var receipt) && receipt.Oid == oid)
        {
            treasureRotationActors[id] = (oid, receipt.Seen || observed);
            return;
        }
        treasureRotationActors[id] = (oid, observed);
        var center = QuantizedDungeonPosition(Arena.Position);
        if (oid == 0x3051)
        {
            treasureSnortsaultCircle = new(id, oid, center, _now, _now.AddSeconds(14.3d), 5);
            return;
        }
        var increment = oid == 0x3082 ? MathF.PI / 30 : -MathF.PI / 30;
        foreach (var offset in new[] { 0f, MathF.PI })
            treasureRotations.Add(new(++treasureRotationId, id, oid, _now, center, rotation + offset, increment,
                _now.AddSeconds(14.5d), 1.1d, 31, 9, 20, MathF.PI / 4));
        RebuildTreasureRotationAoes(_now);
    }

    private void TreasureRotationObserveMissing()
    {
        foreach (var (id, oid) in treasureDestroyedRotationActors.ToArray())
            if (!_actors.TryGetValue(id, out var actor) || actor.Oid != oid)
                treasureDestroyedRotationActors.Remove(id);
        foreach (var id in treasureRotationActors.Where(p => p.Value.Seen
            && (!_actors.TryGetValue(p.Key, out var actor) || actor.Oid != p.Value.Oid)).Select(p => p.Key).ToArray())
            treasureRotationActors.Remove(id);
    }

    private void TreasureRotationDestroyed(ulong id)
    {
        if (treasureRotationActors.Remove(id, out var receipt)) treasureDestroyedRotationActors[id] = receipt.Oid;
    }

    private void TreasureRotationEffect(ulong source, uint action, DateTime time)
    {
        if (IsTreasureTyphon && action == 21691)
        {
            treasureSnortsaultCircle = null;
            return;
        }
        var index = -1;
        if (IsTreasureCladoselache && action is 21706 or 21707)
            index = treasureRotations.Count > 0 ? 0 : -1;
        else if (IsTreasureTyphon && action == 21781 && _actors.GetValueOrDefault(source) is { } caster)
            index = treasureRotations.FindIndex(s => MathF.Abs(s.Origin.X - caster.Position.X) <= 1
                && MathF.Abs(s.Origin.Z - caster.Position.Z) <= 1
                && MathF.Abs(MathF.IEEERemainder(s.Rotation - caster.Rotation, MathF.Tau)) <= .05f);
        if (index < 0) return;
        AdvanceTreasureRotation(index, time);
    }

    private void AdvanceTreasureRotation(int index, DateTime time)
    {
        var sequence = treasureRotations[index];
        if (sequence.Remaining == 1) treasureRotations.RemoveAt(index);
        else treasureRotations[index] = sequence with
        {
            Rotation = sequence.Rotation + sequence.Increment,
            NextActivation = time.AddSeconds(sequence.Interval),
            Remaining = sequence.Remaining - 1,
        };
        RebuildTreasureRotationAoes(time);
    }

    private void RebuildTreasureRotationAoes(DateTime time)
    {
        treasureRotationAoes.Clear();
        foreach (var sequence in treasureRotations)
        {
            var rotation = sequence.Rotation;
            var next = sequence.NextActivation > time ? sequence.NextActivation : time;
            for (var i = 1; i < Math.Min(sequence.Remaining, sequence.MaxShown); ++i)
            {
                rotation += sequence.Increment;
                next = next.AddSeconds(sequence.Interval);
                treasureRotationAoes.Add(new($"treasure/rotation/{sequence.Id}/{i}", "Rotating AOE",
                    new FanTemplate(sequence.Radius, sequence.Angle), sequence.Origin, rotation, Activation: next));
            }
            treasureRotationAoes.Add(new($"treasure/rotation/{sequence.Id}/0", "Rotating AOE",
                new FanTemplate(sequence.Radius, sequence.Angle), sequence.Origin, sequence.Rotation,
                Activation: sequence.NextActivation, Color: sequence.Remaining > 1 ? new Vector4(1, 0, 0, 1) : null));
        }
    }

    private IEnumerable<DungeonAmbientAoe> TreasureRotationAoes()
    {
        if (IsTreasureTyphon && _casts.Values.Any(c => c.Rule.Action == 21695)) yield break;
        if (treasureSnortsaultCircle is { } circle)
            yield return new("treasure/snortsault/circle", "Snortsault", new CircleTemplate(5), circle.Position,
                Activation: circle.Activation);
        foreach (var aoe in treasureRotationAoes) yield return aoe;
    }

    private void ResetTreasureRotations()
    {
        treasureRotations.Clear();
        treasureRotationAoes.Clear();
        treasureRotationActors.Clear();
        treasureDestroyedRotationActors.Clear();
        treasureSnortsaultCircle = null;
        treasureRotationCast = null;
        treasureRotationPending = false;
        treasurePendingRotation = 0;
        treasureRotationIncrement = 0;
        treasureRotationId = 0;
    }
}
