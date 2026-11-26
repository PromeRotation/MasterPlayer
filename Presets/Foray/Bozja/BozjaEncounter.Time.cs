using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private DateTime timeBombActivation;
    private DateTime timeEruptionStart;
    private List<BozjaActor>? timeBombs;
    private readonly List<(long Generation, BozjaActor Source, Vector3 Position, double Delay)> timeClocks = [];
    internal List<Vector3> TimeSafeSpots { get; } = [];
    internal string? TimeSequenceError { get; private set; }
    private static readonly GeometryTemplate timeBombShape = new FanTemplate(60, MathF.PI / 2);
    private static readonly GeometryTemplate timeCycloneShape = RayRectangleTemplate.Symmetric(20, 60);
    internal static readonly GeometryTemplate TimeEruptionShape = new RectangleTemplate(20, 20);

    private void ClearTime()
    {
        timeBombActivation = timeEruptionStart = default;
        timeBombs = null;
        timeClocks.Clear();
        TimeSafeSpots.Clear();
        TimeSequenceError = null;
    }

    private void StartTime(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (spell.ActionId == 23957 && ReferenceEquals(actor, Primary))
            timeBombActivation = Now.AddSeconds(23.2d);
        else if (spell.ActionId == 23959)
            Casts.Add(new(++serial, rule, actor, spell.Location with { Y = 0 }, spell.Rotation ?? 0, Activation(actor))
                { ShapeOverride = timeCycloneShape });
        else if (spell.ActionId == 23955)
            timeEruptionStart = Now;
    }

    internal void EAnim(ulong source, uint state)
    {
        PhantomAnimation(source, state);
        SaunionAnimation(source, state);
        if (!Active || !Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Time)
            || Actors.GetValueOrDefault(source) is not { Actor.Oid: 0x1EB17A } actor) return;
        var delay = state switch { 0x00010002 => 9.7d, 0x00010020 => 11.7d, _ => 0 };
        if (delay == 0) return;
        timeClocks.Add((++serial, actor, actor.Actor.Position with { Y = 0 }, delay));
        if (timeClocks.Count != 9) return;
        timeClocks.Sort(static (a, b) => a.Delay.CompareTo(b.Delay));
        TimeSafeSpots.Add(timeClocks[^2].Position);
        TimeSafeSpots.Add(timeClocks[^1].Position);
    }

    private void TimeEffect(BozjaActor actor, uint action)
    {
        if (!Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Time)) return;
        if (action == 23958) timeBombActivation = default;
        else if (action == 23959)
        {
            // Source consumes FIFO on Effect and reports malformed sequences
            // rather than accepting an invented empty completion.
            var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.Time);
            if (index < 0)
            {
                TimeSequenceError = $"TimeEruptionBombReproduce旋风Effect空队列：source={actor.Actor.Id:X} action={action}";
                throw new ArgumentOutOfRangeException(nameof(action), TimeSequenceError);
            }
            Casts.RemoveAt(index);
            TimeSequenceError = null;
        }
        else if (action == 23956)
        {
            TimeSafeSpots.Clear();
            var index = timeClocks.FindIndex(c => AlmostEqual(c.Position, actor.Actor.Position));
            if (index >= 0) timeClocks.RemoveAt(index);
            if (timeClocks.Count == 0) timeEruptionStart = default;
        }
    }

    private IEnumerable<BozjaAoe> TimeAoes(BozjaRule rule)
    {
        if (timeBombActivation != default)
        {
            foreach (var actor in timeBombs ?? [])
                yield return new(new(-actor.BirthOrder, rule, actor, actor.Actor.Position with { Y = 0 },
                    actor.Actor.Rotation, timeBombActivation) { ShapeOverride = timeBombShape }, false, true);
        }
        else if (Casts.Any(c => c.Rule == rule))
        {
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        }
        else if (timeEruptionStart != default)
        {
            var max = timeClocks.Count > 2 ? timeClocks.Count - 2 : timeClocks.Count;
            foreach (var clock in timeClocks.Take(max))
            {
                // Source eruption rectangle is centered with 10 forward/back.
                yield return new(new(clock.Generation, rule, clock.Source, clock.Position,
                    0, timeEruptionStart.AddSeconds(clock.Delay)) { ShapeOverride = TimeEruptionShape }, false, true);
            }
        }
    }
}
