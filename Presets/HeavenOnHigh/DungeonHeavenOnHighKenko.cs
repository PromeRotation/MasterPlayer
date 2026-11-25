using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsKenko => territory == HeavenOnHighCatalog.SEVENTH;
    private readonly Dictionary<ulong, AlphaActorState> kenkoActors = [];
    private readonly List<AlphaActorState> kenkoPuddles = [];
    private readonly HashSet<AlphaActorState> kenkoCreated = [];
    private readonly List<HeavenOnHighBait> kenkoCharges = [];
    private readonly List<HeavenOnHighBait> kenkoDevours = [];
    private readonly Dictionary<ulong, long> kenkoBaitInputs = [];
    private long kenkoBaitGeneration;
    private int kenkoTargetSlot = -1;
    private bool kenkoUlulation;
    internal HeavenOnHighPool? KenkoNormalPool { get; private set; }
    internal HeavenOnHighPool? KenkoTargetPool { get; private set; }
    internal IReadOnlyList<HeavenOnHighBait> KenkoCharges => kenkoCharges;
    internal IReadOnlyList<HeavenOnHighBait> KenkoDevours => kenkoDevours;
    internal int KenkoTargetSlot => kenkoTargetSlot;
    internal bool KenkoUlulation => kenkoUlulation;

    private void KenkoInput()
    {
        if (!IsKenko) return;
        foreach (var id in kenkoActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            KenkoRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (kenkoActors.TryGetValue(actor.Id, out var state)) state.Actor = actor;
            else kenkoActors.Add(actor.Id, new(actor));
        }
    }

    private void KenkoRemoved(ulong id)
    {
        if (!IsKenko || !kenkoActors.Remove(id, out var state)) return;
        state.Removed = true;
        kenkoCreated.Remove(state);
        kenkoPuddles.Remove(state);
    }

    private void KenkoObserveCreated(DungeonActor actor)
    {
        if (!IsKenko || Active == null || !kenkoActors.TryGetValue(actor.Id, out var state)
            || !kenkoCreated.Add(state)) return;
        if (actor.Oid == 0x1E9829)
        {
            kenkoPuddles.Add(state);
            KenkoNormalPool = new(actor.Position, default);
        }
    }

    private int KenkoSlot(ulong id)
    {
        var slot = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == id)?.Slot
            ?? _actors.GetValueOrDefault(id)?.PartySlot ?? -1;
        // The existing world feed publishes eight party slots.
        return slot is >= 0 and < 8 ? slot : -1;
    }

    private void KenkoIcon(ulong target, uint icon)
    {
        if (!IsKenko || Active == null || icon != 1) return;
        kenkoTargetSlot = KenkoSlot(target);
        if (KenkoNormalPool is { } pool) KenkoTargetPool = new(pool.Origin, default);
    }

    private void KenkoCast(DungeonPendingCast cast, bool correction,
        (DungeonPendingCast Cast, HeavenOnHighBait? Charge, HeavenOnHighBait? Devour)? firstRaw)
    {
        if (!IsKenko) return;
        if (cast.Rule.Action == 12208) kenkoUlulation = true;
        if (cast.Rule.Action != 12206 || !kenkoActors.TryGetValue(cast.Source, out var source)) return;
        long generation;
        var addCharge = true;
        var addDevour = true;
        if (firstRaw is { } confirmation)
        {
            // Capture before the synthetic Finish used by the shared raw replacement path.
            var previousDevour = confirmation.Devour;
            var previousCharge = confirmation.Charge;
            addCharge = previousCharge != null;
            addDevour = previousDevour != null;
            if (!addCharge && !addDevour) return;
            generation = (previousDevour ?? previousCharge)!.Generation;
            kenkoDevours.RemoveAll(b => ReferenceEquals(b.Cast, confirmation.Cast));
            kenkoCharges.RemoveAll(b => ReferenceEquals(b.Cast, confirmation.Cast));
        }
        else if (correction)
        {
            if (!kenkoBaitInputs.TryGetValue(cast.Source, out generation)) return;
            foreach (var list in new[] { kenkoCharges, kenkoDevours })
            {
                var index = list.FindIndex(b => b.Generation == generation);
                if (index >= 0)
                    list[index] = list[index] with
                    {
                        Cast = cast,
                        Activation = list == kenkoCharges ? cast.FinishAt : cast.FinishAt.AddSeconds((double)3.1f),
                    };
            }
            return;
        }
        else generation = ++kenkoBaitGeneration;
        kenkoBaitInputs[cast.Source] = generation;
        if (!kenkoActors.TryGetValue(cast.TargetId, out var target)) return;
        if (addCharge) kenkoCharges.Add(new(generation, source, target, cast, cast.FinishAt));
        if (addDevour) kenkoDevours.Add(new(generation, source, target, cast, cast.FinishAt.AddSeconds((double)3.1f)));
    }

    private void KenkoFinish(DungeonPendingCast cast)
    {
        if (!IsKenko) return;
        if (cast.Rule.Action == 12208) kenkoUlulation = false;
        if (cast.Rule.Action != 12206) return;
        var index = kenkoCharges.FindIndex(b => b.Cast.Source == cast.Source);
        if (index >= 0) kenkoCharges.RemoveAt(index);
        kenkoBaitInputs.Remove(cast.Source);
    }

    private void KenkoEffect(uint action, ulong target, DateTime now)
    {
        if (!IsKenko || Active == null) return;
        if (action == 12207 && _actors.TryGetValue(target, out var actor))
            KenkoNormalPool = new(QuantizedDungeonPosition(actor.Position), now.AddSeconds(7.2d));
        if (action == 12204)
        {
            kenkoTargetSlot = -1;
            KenkoTargetPool = null;
            kenkoDevours.Clear();
        }
    }

    private void KenkoUpdate()
    {
        if (IsKenko && KenkoNormalPool != null && kenkoPuddles.FirstOrDefault()?.Actor.EventState == 7)
            KenkoNormalPool = null;
    }

    private bool KenkoChargeNavigation => kenkoActors.TryGetValue(PlayerId, out var viewer)
        && kenkoCharges.Any(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, viewer));

    private static GeometryTemplate KenkoBaitShape(HeavenOnHighBait bait, bool charge) => charge
        ? RayRectangleTemplate.Symmetric(14, MathF.Sqrt(NativeEnemyDistanceSquared(bait.Source.Actor.Position, bait.Target.Actor.Position)))
        : new FanTemplate(10, MathF.PI / 2);

    private static float KenkoBaitRotation(HeavenOnHighBait bait) => ReferenceEquals(bait.Source, bait.Target)
        ? bait.Source.Actor.Rotation : MathF.Atan2(bait.Target.Actor.Position.X - bait.Source.Actor.Position.X,
            bait.Target.Actor.Position.Z - bait.Source.Actor.Position.Z);

    private IEnumerable<DungeonAmbientAoe> KenkoAoes()
    {
        if (!IsKenko || Active == null || !heavenOnHighEnabled) yield break;
        var target = KenkoSlot(PlayerId) == kenkoTargetSlot && !kenkoUlulation;
        var pool = target ? KenkoTargetPool : KenkoNormalPool;
        if (pool != null)
            yield return new("hoh/kenko/pool", "Innerspace", new CircleTemplate(3), pool.Origin,
                Safe: target, Activation: pool.Activation);
        foreach (var charge in new[] { true, false })
        {
            var baits = charge ? kenkoCharges : kenkoDevours;
            foreach (var bait in baits.Where(b => !b.Source.Actor.Dead))
            {
                var prefix = $"hoh/kenko/{(charge ? "charge" : "devour")}/{bait.Generation}";
                var shape = KenkoBaitShape(bait, charge);
                var origin = bait.Source.Actor.Position;
                var rotation = KenkoBaitRotation(bait);
                yield return new(prefix + "/display", charge ? "Hound out of Hell" : "Devour",
                    shape, origin, rotation, Navigate: false, OutlineOnly: bait.Target.Actor.Id == PlayerId);
                if (charge && !KenkoChargeNavigation) continue;
                var personal = kenkoActors.TryGetValue(PlayerId, out var viewer) && ReferenceEquals(bait.Target, viewer);
                if (!personal)
                {
                    yield return new(prefix + "/avoid", "避开诱导", shape, origin, rotation, Display: false, Activation: bait.Activation);
                    continue;
                }
                if (ReferenceEquals(bait.Source, bait.Target)) continue;
                foreach (var member in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId))
                {
                    var delta = member.Position - origin;
                    var distance = new Vector2(delta.X, delta.Z).Length();
                    var halfAngle = charge ? MathF.Asin(7 / distance) : MathF.PI / 4;
                    if (!float.IsFinite(halfAngle)) continue;
                    yield return new($"{prefix}/party/{member.Id}", "诱导远离队友",
                        new FanTemplate(100, 2 * halfAngle), origin, MathF.Atan2(delta.X, delta.Z),
                        Display: false, Activation: bait.Activation);
                }
            }
        }
    }

    private IEnumerable<string> KenkoHints(Vector3 position)
    {
        if (!IsKenko || Active == null) yield break;
        if (KenkoPoolHint(position) is { } hint) yield return hint.Text;
        foreach (var charge in new[] { true, false })
            foreach (var bait in (charge ? kenkoCharges : kenkoDevours).Where(b => !b.Source.Actor.Dead))
            {
                var shape = KenkoBaitShape(bait, charge).Resolve(bait.Source.Actor.Position, KenkoBaitRotation(bait), null);
                if (bait.Target.Actor.Id == PlayerId && _actors.Values.Any(a => a.PartySlot is >= 0 and < 8
                    && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))) yield return "Bait away from raid!";
                if (bait.Target.Actor.Id != PlayerId && shape.Contains(position)) yield return "GTFO from baited aoe!";
            }
    }

    internal (string Text, bool IsRisk)? KenkoPoolHint(Vector3 position)
    {
        if (!IsKenko || Active == null || KenkoNormalPool is not { } pool) return null;
        var inside = NativeEnemyDistanceSquared(position, pool.Origin) <= 9;
        if (KenkoSlot(PlayerId) == kenkoTargetSlot && !kenkoUlulation)
            return ("Go inside puddle!", !inside);
        return inside ? ("GTFO from AOE!", true) : null;
    }

    private IEnumerable<DungeonDamagePrediction> KenkoDamage()
    {
        if (!IsKenko || Active == null || !KenkoChargeNavigation) yield break;
        foreach (var actor in kenkoCharges.Where(b => !b.Source.Actor.Dead).Select(b => b.Target.Actor).DistinctBy(a => a.Id))
        {
            var slot = KenkoSlot(actor.Id);
            if (slot >= 0)
                yield return new(actor.Id, slot, actor.PartyRole, actor.Name, kenkoCharges[0].Activation, "Tankbuster");
        }
    }

    private void ResetKenko()
    {
        kenkoActors.Clear();
        kenkoPuddles.Clear();
        kenkoCreated.Clear();
        kenkoCharges.Clear();
        kenkoDevours.Clear();
        kenkoBaitInputs.Clear();
        kenkoBaitGeneration = 0;
        kenkoTargetSlot = -1;
        kenkoUlulation = false;
        KenkoNormalPool = null;
        KenkoTargetPool = null;
    }
}
