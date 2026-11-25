using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntAtticusState(ulong owner)
{
    private static readonly GeometryTemplate cone = new FanTemplate(40, 2 * MathF.PI / 3);
    private readonly List<HuntPendingCast> aoes = [];
    private readonly Dictionary<long, int> retimed = [];
    private readonly List<HuntStackState> stacks = [];
    private long yellOrder;
    private long timingOrder;
    internal IEnumerable<HuntStackState> Stacks => stacks;

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (aoes.Count > 1) aoes[0] = aoes[0] with { Color = new(1, 1, 0, 1) };
            return aoes.Take(2);
        }
    }

    internal static bool First(uint action) => action is 39003 or 39004 or 39005;
    internal static bool Hit(uint action) => First(action) || action is 39009 or 39010 or 39011;

    internal void Yell(ReplayActorState actor, ushort id, DateTime at)
    {
        float? offset = id switch
        {
            16874 or 16881 or 16882 or 16885 or 16888 or 16889 or 16896 or 16897 or 16900 or 16903 => 0,
            16876 or 16877 or 16880 or 16883 or 16884 or 16891 or 16892 or 16895 or 16898 or 16899 => 2 * MathF.PI / 3,
            16875 or 16878 or 16879 or 16886 or 16887 or 16890 or 16893 or 16894 or 16901 or 16902 => -2 * MathF.PI / 3,
            _ => null
        };
        if (offset == null) return;
        var definition = SimpleHuntCatalog.Find(0x416D)!;
        var rule = new HuntCastRule(0, "BreathSequence", cone);
        var input = new ReplayCast(0, actor.Id, actor.Position, actor.Rotation + offset.Value, at, at.AddSeconds(10.5d), false, false);
        var order = ++yellOrder;
        aoes.Add(new(owner, definition, rule, actor.Position, input.Rotation!.Value, at, input.FinishAt,
            actor.Id, false, true, cone, input, null, SequenceId: $"{owner}/atticus/{order}", Order: order));
    }

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast.Rule.Action == 39013 && actors.ContainsKey(cast.TargetId))
        {
            var stack = new HuntStackState(int.MaxValue, 6);
            stack.Start(cast with { Source = owner }, actors, []);
            stacks.Add(stack);
        }
        if (!First(cast.Rule.Action)) return;
        timingOrder = cast.Order;
        retimed.Clear();
        for (var i = 0; i < aoes.Count; ++i)
        {
            retimed[aoes[i].Order] = i;
            aoes[i] = aoes[i] with { Activation = cast.Activation.AddSeconds(2.3d * i) };
        }
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var stack in stacks.Where(s => s.Cast?.Order == previous.Order).ToArray())
        {
            stack.Correct(previous, updated with { Source = owner }, actors);
            if (stack.TargetId == 0) stacks.Remove(stack);
        }
        if (timingOrder != previous.Order) return;
        for (var i = 0; i < aoes.Count; ++i)
            if (retimed.TryGetValue(aoes[i].Order, out var stage))
                aoes[i] = aoes[i] with { Activation = updated.Activation.AddSeconds(2.3d * stage) };
    }

    internal void Effect()
    {
        if (aoes.Count == 0) return;
        retimed.Remove(aoes[0].Order);
        aoes.RemoveAt(0);
    }

    internal void FinishStack(uint action, ulong target)
    {
        if (action != 39013) return;
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
    }
}
