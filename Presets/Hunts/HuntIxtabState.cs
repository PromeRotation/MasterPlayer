using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntIxtabState(ulong owner)
{
    private static readonly GeometryTemplate circle = new CircleTemplate(20);
    private static readonly GeometryTemplate donut = new DonutTemplate(8, 40);
    private readonly List<HuntPendingCast> queue = [];
    private readonly List<HuntCastBait> abyss = [];
    private readonly List<HuntCastBait> crypt = [];
    private readonly List<HuntStackState> stacks = [];
    private readonly HuntIxtabDoomState doom = new();
    private bool dual;
    internal IEnumerable<HuntCastBait> Baits => abyss.Concat(crypt);
    internal IEnumerable<HuntStackState> Stacks => stacks;
    internal bool TankbusterHint => abyss.Count != 0;
    internal IEnumerable<HuntPendingCast> Areas => queue.Select((c, i) => c with
    {
        Source = owner, Risky = i == 0, Color = i == 0 && queue.Count > 1 ? new Vector4(1, 1, 0, 1) : null
    });
    internal static bool Hit(uint action) => action is 17843 or 17999 or 18074 or 18075 or 17847;

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        var action = cast.Rule.Action;
        if (action == 18077) dual = true;
        else if (action is 17843 or 17999)
        {
            Add(action == 17843 ? circle : donut, 0);
            if (dual)
            {
                Add(action == 17843 ? donut : circle, 1);
                dual = false;
            }
        }
        else if (action is 17848 or 17847 && actors.TryGetValue(cast.Source, out var source)
            && actors.TryGetValue(cast.TargetId, out var target))
        {
            var bait = new HuntCastBait(owner, cast, source, target,
                action == 17848 ? new CircleTemplate(6) : new FanTemplate(38.24f, 2 * MathF.PI / 3),
                centerAtTarget: action == 17848);
            (action == 17848 ? abyss : crypt).Add(bait);
        }
        else if (action == 17844 && actors.ContainsKey(cast.TargetId))
        {
            var stack = new HuntStackState(int.MaxValue, 10, 8);
            stack.Start(cast with { Source = owner }, actors, []);
            stacks.Add(stack);
        }

        void Add(GeometryTemplate shape, int stage) => queue.Add(cast with
        {
            Shape = shape, Rotation = 0, Stage = stage, Activation = cast.Activation.AddSeconds(stage * 5.1f),
            SequenceId = $"{owner}/ixtab/dual/{cast.Source}/{cast.Order}/{stage}"
        });
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var cast = queue[i];
            if (cast.Source != previous.Source || cast.Order != previous.Order) continue;
            queue[i] = cast with
            {
                Location = updated.Location, Activation = updated.Activation.AddSeconds(cast.Stage * 5.1f), Input = updated.Input
            };
        }
        foreach (var bait in Baits) bait.Correct(previous, updated, actors);
        foreach (var stack in stacks)
            stack.Correct(previous with { Source = owner }, updated with { Source = owner }, actors);
    }

    internal void Finish(ulong source, uint action, ulong target)
    {
        if (action == 17848)
        {
            var index = abyss.FindIndex(b => b.Cast.Source == source);
            if (index >= 0) abyss.RemoveAt(index);
        }
        else if (action == 17844)
        {
            var index = stacks.FindIndex(s => s.TargetId == target);
            if (index >= 0) stacks.RemoveAt(index);
        }
    }

    internal void Effect(uint action)
    {
        if (action == 17847) crypt.Clear();
        else if (queue.Count != 0 && action is 17843 or 17999 or 18074 or 18075) queue.RemoveAt(0);
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        foreach (var bait in Baits) bait.Update(actors);
        foreach (var stack in stacks) stack.Update(actors);
        doom.Observe(actors, now);
    }

    internal void Status(ReplayActorState actor, ReplayStatus? value, ulong source, DateTime at)
        => doom.Status(actor, value, source, at);

    internal IEnumerable<string> Hints(ReplayActorState player) => doom.Hints(player);

    internal void Remove(ulong id)
    {
        foreach (var bait in Baits) bait.Remove(id);
        foreach (var stack in stacks) stack.Remove(id);
        doom.Remove(id);
    }

    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IReadOnlyList<ReplayPartyMember> party)
    {
        foreach (var (baits, type) in new[] { (abyss, "Tankbuster"), (crypt, "Raidwide") })
        {
            if (baits.Count == 0) continue;
            var ids = party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).ToHashSet();
            foreach (var id in baits.Where(b => b.Active && ids.Contains(b.TargetId)).Select(b => b.TargetId).Distinct())
                yield return new(owner, id, baits[0].Cast.Activation, type);
        }
    }
}
