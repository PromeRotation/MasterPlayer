using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntSansheyaState(ulong owner)
{
    private static readonly GeometryTemplate cone = new FanTemplate(40, MathF.PI);
    private static readonly GeometryTemplate circle = new CircleTemplate(15);
    private static readonly GeometryTemplate donut = new DonutTemplate(10, 40);
    private readonly List<HuntPendingCast> queue = [];
    private readonly List<HuntCastBait> charges = [];
    private readonly List<HuntStackState> stacks = [];
    internal readonly HuntStayMoveState Boiling = new(4140, 960, "Boiling", "Pyretic", 1000);
    internal IEnumerable<HuntCastBait> Baits => charges;
    internal IEnumerable<HuntStackState> Stacks => stacks;
    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            for (var i = 0; i < Math.Min(queue.Count, 2); ++i)
            {
                if (i == 0) queue[i] = queue[i] with { Risky = true, Color = queue.Count > 1 ? new Vector4(1, 1, 0, 1) : queue[i].Color };
                else if (ReferenceEquals(queue[i].Shape, queue[0].Shape)) queue[i] = queue[i] with { Risky = false };
            }
            return queue.Take(2).Select(c => c with { Source = owner });
        }
    }
    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast.Rule.Action == 39285 && actors.TryGetValue(cast.Source, out var source) && actors.TryGetValue(cast.TargetId, out var target))
            charges.Add(new(owner, cast, source, target, RayRectangleTemplate.Symmetric(6, 0), dynamicLength: true));
        if (cast.Rule.Action == 39296 && actors.ContainsKey(cast.TargetId))
        {
            var stack = new HuntStackState(int.MaxValue, 6, 8);
            stack.Start(cast with { Source = owner }, actors, []);
            stacks.Add(stack);
        }
        if (cast.Rule.Action is not (39292 or 39290 or 39289 or 39291 or 39601 or 39602)) return;
        Add(cone, 0, 0);
        Add(cone, MathF.PI, 2.3f);
        if (cast.Rule.Action is 39292 or 39290) Add(circle, 0, 4.5f);
        if (cast.Rule.Action is 39289 or 39291) Add(donut, 0, 4.5f);
        void Add(GeometryTemplate shape, float offset, float delay)
        {
            var stage = 0;
            if (delay != 0) stage = offset != 0 ? 1 : 2;
            queue.Add(cast with { Shape = shape, Rotation = cast.Rotation + offset, Activation = cast.Activation.AddSeconds(delay),
                Stage = stage, SequenceId = $"{owner}/twinscorch/{cast.Source}/{cast.Order}/{stage}" });
        }
    }
    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var bait in charges) bait.Correct(previous, updated, actors);
        foreach (var stack in stacks) stack.Correct(previous with { Source = owner }, updated with { Source = owner }, actors);
        for (var i = 0; i < queue.Count; ++i)
        {
            var c = queue[i];
            if (c.Source != previous.Source || c.Order != previous.Order) continue;
            queue[i] = c with
            {
                Location = updated.Location, Rotation = updated.Rotation + (c.Stage == 1 ? MathF.PI : 0),
                Activation = updated.Activation.AddSeconds(c.Stage switch { 0 => 0, 1 => 2.3f, _ => 4.5f }), Input = updated.Input
            };
        }
    }
    internal static bool Hit(uint action) => action is 39294 or 39293 or 39528 or 39557 or 39558 or 39529;
    internal void Effect()
    {
        if (queue.Count > 0) queue.RemoveAt(0);
    }
    internal void Finish(ulong source, uint action, ulong target)
    {
        if (action == 39285)
        {
            var index = charges.FindIndex(b => b.Cast.Source == source);
            if (index >= 0) charges.RemoveAt(index);
        }
        if (action != 39296) return;
        var stackIndex = stacks.FindIndex(s => s.TargetId == target);
        if (stackIndex >= 0) stacks.RemoveAt(stackIndex);
    }
    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var bait in charges) bait.Update(actors);
        foreach (var stack in stacks) stack.Update(actors);
    }
    internal void Remove(ulong actor)
    {
        foreach (var bait in charges) bait.Remove(actor);
        foreach (var stack in stacks) stack.Remove(actor);
        Boiling.Remove(actor);
    }
    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IReadOnlyList<ReplayPartyMember> party)
    {
        if (charges.Count == 0) yield break;
        var ids = party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).ToHashSet();
        foreach (var id in charges.Where(b => b.Active && ids.Contains(b.TargetId)).Select(b => b.TargetId).Distinct())
            yield return new(owner, id, charges[0].Cast.Activation, "Tankbuster");
    }
}
