using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntPedantryState(ulong owner)
{
    private static readonly GeometryTemplate cheek = new FanTemplate(60, MathF.PI);
    private static readonly GeometryTemplate fever = new FanTemplate(15, 2 * MathF.PI / 3);
    private readonly List<HuntPendingCast> cheeks = [];
    private readonly List<HuntCastBait> feverBaits = [];
    private readonly List<HuntCastBait> witches = [];
    private long witchOrder;
    internal IEnumerable<HuntPendingCast> Areas => cheeks.Select(c => c with { Source = owner });
    internal IEnumerable<HuntCastBait> Baits => feverBaits.Concat(witches);
    internal bool TankbusterHint => feverBaits.Count != 0;
    internal static bool Hit(uint action) => action is 17446 or 17447 or 17448 or 17449 or 17440 or 17445 or 17900;

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast.Rule.Action is 17446 or 17448)
        {
            cheeks.Add(cast with
            {
                Shape = cheek, Stage = 0, Color = new(1, 1, 0, 1),
                SequenceId = $"{owner}/pedantry/cheek/{cast.Order}/0"
            });
            cheeks.Add(cast with
            {
                Shape = cheek, Rotation = cast.Rotation + MathF.PI, Activation = cast.Activation.AddSeconds(3.1d),
                Stage = 1, Risky = false, SequenceId = $"{owner}/pedantry/cheek/{cast.Order}/1"
            });
        }
        if (cast.Rule.Action == 17440 && actors.TryGetValue(cast.Source, out var source)
            && actors.TryGetValue(cast.TargetId, out var target))
            feverBaits.Add(new(owner, cast, source, target, fever));
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        for (var i = 0; i < cheeks.Count; ++i)
        {
            var cast = cheeks[i];
            if (cast.Source != previous.Source || cast.Order != previous.Order) continue;
            cheeks[i] = cast with
            {
                Location = updated.Location, Rotation = updated.Rotation + cast.Stage * MathF.PI,
                Activation = updated.Activation.AddSeconds(cast.Stage * 3.1d), Input = updated.Input
            };
        }
        foreach (var bait in feverBaits) bait.Correct(previous, updated, actors);
    }

    internal void Effect(ReplayActorState caster, uint action, ulong mainTarget, DateTime at,
        IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (action is 17446 or 17447 or 17448 or 17449 && cheeks.Count != 0)
        {
            var count = cheeks.Count;
            cheeks.RemoveAt(0);
            // The source promotes only when the complete component had exactly two entries.
            if (count == 2) cheeks[0] = cheeks[0] with { Risky = true, Color = new(1, 1, 0, 1) };
        }
        if (action == 17440)
        {
            var index = feverBaits.FindIndex(b => b.Cast.Source == caster.Id);
            if (index >= 0) feverBaits.RemoveAt(index);
        }
        if (action == 17445) witches.Clear();
        else if (action == 17900) AddWitch(caster, mainTarget, at, actors);
    }

    internal void Finish(ReplayActorState caster, uint action, ulong target, DateTime at,
        IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (action != 17444 || !actors.ContainsKey(target)) return;
        witches.Clear();
        AddWitch(caster, target, at, actors);
    }

    private void AddWitch(ReplayActorState caster, ulong targetId, DateTime at,
        IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (!actors.TryGetValue(targetId, out var target) || !actors.TryGetValue(owner, out var primary)) return;
        var delta = target.Position - caster.Position;
        var shape = RayRectangleTemplate.Symmetric(10, new Vector2(delta.X, delta.Z).Length());
        var rule = new HuntCastRule(17900, "WitchHunt", null);
        var input = new ReplayCast(17900, target.Id, caster.Position, caster.Rotation, at, default, false, false);
        var cast = new HuntPendingCast(owner, SimpleHuntCatalog.Find(0x298A)!, rule, primary.Position, 0,
            at, default, target.Id, false, true, null, input, input, Order: --witchOrder);
        witches.Add(new(owner, cast, primary, target, shape, dynamicLength: true));
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var bait in Baits) bait.Update(actors);
        feverBaits.RemoveAll(b => b.SourceDead || b.SourceDestroyed || b.TargetDead);
        // witchHunt1done is never set true in the source; do not retarget to a new tank.
    }

    internal void Remove(ulong actor)
    {
        foreach (var bait in Baits) bait.Remove(actor);
    }

    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IReadOnlyList<ReplayPartyMember> party)
    {
        if (feverBaits.Count == 0) yield break;
        var ids = party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).ToHashSet();
        foreach (var target in feverBaits.Where(b => b.Active && ids.Contains(b.TargetId)).Select(b => b.TargetId).Distinct())
            yield return new(owner, target, feverBaits[0].Cast.Activation, "Tankbuster");
    }
}
