using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntUrnaVariabilisState
{
    private readonly ulong owner;
    private readonly HuntStatusInput positiveInput;
    private readonly HuntStatusInput negativeInput;
    private readonly HashSet<int> positive = [];
    private readonly HashSet<int> negative = [];
    private readonly List<HuntStackState> stacks = [];
    private IReadOnlyList<ReplayPartyMember> party = [];
    private HuntPendingCast? aoe;
    internal HuntPendingCast? Magnetism { get; private set; }
    internal const float DISTANCE = 10;
    internal const float MIN_DISTANCE = 0;
    internal const bool STOP_AT_WALL = false;
    // The source overrides the status callbacks without base, so its immunity cache is never filled.
    internal const bool IGNORE_IMMUNES = true;
    internal IEnumerable<HuntStackState> Stacks => stacks;
    internal IEnumerable<HuntPendingCast> Areas => aoe == null ? [] : [aoe with { Source = owner }];

    internal HuntUrnaVariabilisState(ulong owner)
    {
        this.owner = owner;
        positiveInput = new(4071, (actor, _) => positive.Add(Slot(actor.Id)), actor => positive.Remove(Slot(actor)));
        negativeInput = new(4072, (actor, _) => negative.Add(Slot(actor.Id)), actor => negative.Remove(Slot(actor)));
    }
    private int Slot(ulong actor) => party.FirstOrDefault(p => p.EntityId == actor && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal void SetParty(IReadOnlyList<ReplayPartyMember> members) => party = members;
    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, ulong actor, DateTime now)
    {
        positiveInput.Observe(actors, now, actor);
        negativeInput.Observe(actors, now, actor);
    }
    internal void Status(ReplayActorState actor, uint status, ReplayStatus? value, ulong source, DateTime at)
    {
        if (status == 4071) positiveInput.Status(actor, value, source, at);
        if (status == 4072) negativeInput.Status(actor, value, source, at);
    }
    internal void RemoveMissing(IReadOnlySet<ulong> actors)
    {
        positiveInput.RemoveMissing(actors);
        negativeInput.RemoveMissing(actors);
    }
    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (Magnetic(cast.Rule.Action)) Magnetism = cast;
        if (cast.Rule.Action is 39106 or 39107 || Magnetic(cast.Rule.Action))
        {
            aoe = cast with
            {
                Shape = cast.Rule.Action is 39107 or 39110 or 39112 ? new DonutTemplate(8, 60) : new CircleTemplate(20),
                Rotation = 0, Activation = cast.Activation.AddSeconds(Magnetic(cast.Rule.Action) ? 2.5d : 0),
                SequenceId = $"{owner}/magnetism/aoe"
            };
        }
        if (cast.Rule.Action != 39115 || !actors.ContainsKey(cast.TargetId)) return;
        var stack = new HuntStackState(int.MaxValue, 6, 8);
        stack.Start(cast with { Source = owner }, actors, []);
        stacks.Add(stack);
    }
    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (Magnetism?.Source == previous.Source && Magnetism.Order == previous.Order) Magnetism = updated;
        if (aoe?.Source == previous.Source && aoe.Order == previous.Order)
            aoe = aoe with { Location = updated.Location, Activation = updated.Activation.AddSeconds(Magnetic(updated.Rule.Action) ? 2.5d : 0), Input = updated.Input };
        foreach (var stack in stacks) stack.Correct(previous with { Source = owner }, updated with { Source = owner }, actors);
    }
    internal static bool Magnetic(uint action) => action is 39109 or 39110 or 39111 or 39112;
    internal static bool CircleFinish(uint action) => action is 39106 or 39107 or 39113 or 39114;
    internal void Finish(uint action, ulong target)
    {
        if (Magnetic(action)) Magnetism = null;
        if (CircleFinish(action)) aoe = null;
        if (action != 39115) return;
        var index = stacks.FindIndex(s => s.TargetId == target);
        if (index >= 0) stacks.RemoveAt(index);
    }
    private bool Charged(ReplayActorState player)
    {
        var slot = Slot(player.Id);
        return slot >= 0 && (positive.Contains(slot) || negative.Contains(slot));
    }
    internal bool UseOrdinaryNavigation(ReplayActorState? player) => Magnetism == null || player == null || !Charged(player);
    internal IEnumerable<HuntPendingCast> Navigation(ReplayActorState player, ReplayActorState boss)
    {
        if (Magnetism is not { } cast || !Charged(player)) yield break;
        var bossPositive = cast.Rule.Action is 39109 or 39110;
        var slot = Slot(player.Id);
        var pull = !bossPositive && positive.Contains(slot) || bossPositive && negative.Contains(slot);
        var ring = cast.Rule.Action is 39110 or 39112;
        var radius = ring ? (pull ? 18 : 1) : (pull ? 30 : 10);
        GeometryTemplate shape = ring ? new HuntStackJoinTemplate([boss.Position], radius) : new CircleTemplate(radius);
        yield return cast with { Source = owner, Shape = shape, Location = boss.Position, Rotation = 0,
            SequenceId = $"{owner}/magnetism/navigation" };
    }
    internal IEnumerable<HuntMovementProjection> Knockbacks(ReplayActorState player, ReplayActorState boss)
    {
        if (Magnetism is not { } cast || !Charged(player)) yield break;
        var slot = Slot(player.Id);
        var bossPositive = cast.Rule.Action is 39109 or 39110;
        var pull = positive.Contains(slot) ? !bossPositive : bossPositive;
        var delta = player.Position - boss.Position;
        delta.Y = 0;
        var length = delta.Length();
        if (length == 0) yield break;
        var distance = pull ? -MathF.Min(DISTANCE, length - MIN_DISTANCE) : DISTANCE;
        yield return new(owner, player.Position, player.Position + delta / length * distance);
    }
    internal bool Unsafe(Vector3 position) => aoe?.Shape?.Resolve(aoe.Location, aoe.Rotation, null).Contains(position, 0) == true;
    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var stack in stacks) stack.Update(actors);
    }
    internal void Remove(ulong actor)
    {
        positiveInput.Remove(actor);
        negativeInput.Remove(actor);
        foreach (var stack in stacks) stack.Remove(actor);
    }
}
