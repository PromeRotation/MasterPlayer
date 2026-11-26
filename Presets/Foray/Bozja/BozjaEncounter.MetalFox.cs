using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaRush> Rushes { get; } = [];
    private List<BozjaActor>? bits;
    private uint bitMode;
    private int bitCasts;
    private float bitRotation;
    private DateTime[] bitTimes = [];

    private void ClearMetalFox()
    {
        Rushes.Clear();
        bits = null;
        bitMode = 0;
        bitCasts = 0;
        bitRotation = 0;
        bitTimes = [];
    }

    private void StartMetalFox(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.Rush)
        {
            if (Actors.TryGetValue(spell.TargetId, out var target))
                Rushes.Add(new(++serial, rule, actor, target,
                    MathF.Sqrt(DistanceSquared(actor.Actor.Position, target.Actor.Position)), Activation(actor)));
            return;
        }
        if (bitMode != 0) return;
        bitMode = spell.ActionId;
        if (bitMode == 20137)
            bitTimes = [Now.AddSeconds(2.5d), Now.AddSeconds(12.3d)];
        else if (bitMode == 20138)
        {
            bitRotation = (spell.Rotation ?? 0) + MathF.PI;
            bitTimes = [Now.AddSeconds(2d), Now.AddSeconds(8.8d), Now.AddSeconds(10.6d), Now.AddSeconds(12.4d)];
        }
        else
        {
            bitRotation = actor.Actor.Rotation;
            bitTimes = [Now, Now.AddSeconds(6.5d), Now.AddSeconds(8.3d), Now.AddSeconds(10.1d)];
        }
    }

    private void MetalFoxEffect(ulong source, uint action)
    {
        if (Active && action == 20141 && Actors.GetValueOrDefault(source) is { NativeNpc: true }
            && Definition.Rules.Any(r => r.Kind == BozjaRuleKind.MagitekBitLasers) && ++bitCasts == 14)
        {
            bitCasts = 0;
            bitMode = 0;
        }
    }

    private IEnumerable<BozjaAoe> BitLasers(BozjaRule rule)
    {
        if (bitMode == 0) yield break;
        // Enemies is queried before the visibility gate and survives mode resets.
        bits ??= Actors.Values.Where(a => a.Actor.Oid == 0x2DB6).ToList();
        if (bitMode != 20140 && Now <= bitTimes[0]) yield break;
        foreach (var actor in bits)
        {
            var rotation = actor.Actor.Rotation;
            var clock = bitTimes[1];
            var danger = false;
            if (bitMode != 20137)
            {
                var first = bitCasts < 5;
                if (AngleEqual(rotation, bitRotation + (first ? 0 : MathF.PI)))
                {
                    clock = first ? bitTimes[1] : bitTimes[3];
                    danger = first;
                }
                else if (bitCasts < 9 && (AngleEqual(rotation, bitRotation + 90f * (MathF.PI / 180f))
                    || AngleEqual(rotation, bitRotation - 90f * (MathF.PI / 180f))))
                {
                    clock = bitTimes[2];
                    danger = !first;
                }
                else continue;
            }
            yield return new(new(-actor.BirthOrder, rule, actor,
                DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position), rotation, clock), danger, true);
        }
    }

    private static bool AngleEqual(float a, float b)
    {
        var difference = a - b;
        while (difference < -MathF.PI) difference += MathF.Tau;
        while (difference > MathF.PI) difference -= MathF.Tau;
        return MathF.Abs(difference) <= MathF.PI / 180f;
    }

    private void UpdateRushes()
    {
        foreach (var bait in Rushes)
            bait.Length = MathF.Sqrt(DistanceSquared(bait.Source.Actor.Position, bait.Target.Actor.Position));
    }

    internal static BozjaCast RushView(BozjaRush bait)
    {
        var source = bait.Source.Actor;
        var target = bait.Target.Actor;
        var rotation = ReferenceEquals(bait.Source, bait.Target) ? source.Rotation
            : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
        return new(bait.Generation, bait.Rule, bait.Source, source.Position, rotation, bait.Activation)
            { ShapeOverride = RayRectangleTemplate.Symmetric(14, bait.Length), Target = bait.Target };
    }

    private IEnumerable<BozjaHint> RushHints(BozjaActor viewer)
    {
        var active = Rushes.Where(b => !b.Source.Actor.Dead).ToArray();
        if (active.Any(b => b.Target.Actor.Id == viewer.Actor.Id
            && Party.Any(p => p.EntityId != b.Target.Actor.Id
                && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } member
                && RushView(b).Contains(member.Actor.Position))))
            yield return new("Bait away from raid!");
        if (active.Any(b => b.Target.Actor.Id != viewer.Actor.Id && RushView(b).Contains(viewer.Actor.Position)))
            yield return new("GTFO from baited aoe!");
    }

    private IEnumerable<BozjaZone> RushNavigation(BozjaActor viewer)
    {
        foreach (var bait in Rushes.Where(b => !b.Source.Actor.Dead))
        {
            var cast = RushView(bait);
            if (!ReferenceEquals(bait.Target, viewer))
            {
                yield return new($"rush/{bait.Generation}/avoid", bait.Rule.Component, bait.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
                continue;
            }
            if (ReferenceEquals(bait.Source, bait.Target)) continue;
            foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64))
            {
                if (Actors.GetValueOrDefault(member.EntityId) is not { Actor.Dead: false } actor
                    || ReferenceEquals(actor, viewer)) continue;
                var delta = actor.Actor.Position - bait.Source.Actor.Position;
                var halfAngle = (float)Math.Asin(7 / MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z));
                yield return new($"rush/{bait.Generation}/party/{member.Slot}", bait.Rule.Component, bait.Activation,
                    new FanTemplate(100, 2 * halfAngle), cast.Origin, MathF.Atan2(delta.X, delta.Z));
            }
        }
    }

    private IEnumerable<BozjaDamage> RushDamage()
    {
        ulong mask = 0;
        foreach (var bait in Rushes.Where(b => !b.Source.Actor.Dead))
        {
            var slot = Party.FirstOrDefault(p => p.EntityId == bait.Target.Actor.Id && p.Slot is >= 0 and < 64)?.Slot;
            if (slot is { } i) mask |= 1UL << i;
        }
        if (mask != 0) yield return new(mask, Rushes[0].Activation, "Tankbuster");
    }
}
