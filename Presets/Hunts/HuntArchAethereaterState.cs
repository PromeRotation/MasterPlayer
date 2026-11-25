using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntArchAethereaterState(ulong owner)
{
    private static readonly GeometryTemplate cone = new FanTemplate(40, MathF.PI);
    private static readonly GeometryTemplate circle = new CircleTemplate(15);
    private static readonly GeometryTemplate donut = new DonutTemplate(6, 40);
    private readonly List<HuntPendingCast> queue = [];
    private readonly List<HuntStackState> stacks = [];
    internal readonly HuntStayMoveState Heat = new(4141, 960, "Heatstroke", "Pyretic", 3);
    internal readonly HuntStayMoveState Cold = new(4142, 2540, "Cold Sweats", "Freezing", 3);
    internal IEnumerable<HuntStackState> Stacks => stacks;
    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (queue.Count > 1) queue[0] = queue[0] with { Color = new Vector4(1, 1, 0, 1) };
            return queue.Select(c => c with { Source = owner });
        }
    }
    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast.Rule.Action == 39515 && actors.ContainsKey(cast.TargetId))
        {
            var stack = new HuntStackState(int.MaxValue, 6, 8);
            stack.Start(cast with { Source = owner }, actors, []);
            stacks.Add(stack);
        }
        if (cast.Rule.Action is not (39509 or 39508 or 39510 or 39507)) return;
        queue.Add(cast with { Shape = cone, Stage = 0, SequenceId = $"{owner}/soulless/{cast.Source}/{cast.Order}/0" });
        queue.Add(cast with
        {
            Shape = cast.Rule.Action is 39509 or 39508 ? circle : donut,
            Rotation = 0, Activation = cast.Activation.AddSeconds(2.5f), Stage = 1,
            SequenceId = $"{owner}/soulless/{cast.Source}/{cast.Order}/1"
        });
    }
    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var stack in stacks) stack.Correct(previous with { Source = owner }, updated with { Source = owner }, actors);
        for (var i = 0; i < queue.Count; ++i)
        {
            var c = queue[i];
            if (c.Source != previous.Source || c.Order != previous.Order) continue;
            queue[i] = c with
            {
                Location = updated.Location, Rotation = c.Stage == 0 ? updated.Rotation : 0,
                Activation = updated.Activation.AddSeconds(c.Stage == 0 ? 0 : 2.5f), Input = updated.Input
            };
        }
    }
    internal static bool ComboFinish(uint action) => action is 39509 or 39510 or 39507 or 39508 or 39514 or 39801 or 39803 or 39800 or 39513 or 39802;
    internal void Finish(uint action, ulong target)
    {
        if (ComboFinish(action) && queue.Count > 0) queue.RemoveAt(0);
        if (action != 39515) return;
        var index = stacks.FindIndex(s => s.TargetId == target);
        if (index >= 0) stacks.RemoveAt(index);
    }
    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var stack in stacks) stack.Update(actors);
    }
    internal void Remove(ulong actor)
    {
        foreach (var stack in stacks) stack.Remove(actor);
        Heat.Remove(actor);
        Cold.Remove(actor);
    }
}
