using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class ThordanEncounter
{
    internal static uint Watched(C component) => component switch
    {
        C.AscalonsMight => 5246, C.HeavenlySlashAdelphel or C.HeavenlySlashJanlenoux => 5268,
        C.Meteorain => 5252, C.AscalonsMercy => 5247, C.AscalonsMercyHelper => 5248,
        C.LightningStorm => 5250, C.DragonsRage => 5259, C.Heavensflame => 5292,
        C.Conviction => 5277, C.BurningChains => 35308, C.HoliestOfHoly => 5269,
        C.SkywardLeap => 5289, C.SpiralPierce => 5287, C.DimensionalCollapse => 5274,
        C.FaithUnmoving => 5275, C.BossReappear => 4187, C.LightOfAscalon => 5263,
        C.UltimateEnd => 5262, C.SpiralThrust1 or C.SpiralThrust2 => 5286,
        C.HeavenswardLeap => 5290, C.PureOfSoul => 5297, C.AbsoluteConviction => 5285,
        _ => 0,
    };

    private void Start(C component, ThordanActor actor, DungeonCastObservation cast)
    {
        var action = cast.Action;
        var rotation = cast.Rotation ?? 0;
        var activation = Activation(actor);
        if (component == C.DragonsGaze && action is 5257 or 5258)
        {
            Aoes.Add(new(++generation, component, actor, action, cast.Location, 0, activation, new CircleTemplate(10000)));
            EyePositionHint = default;
            return;
        }
        if (component == C.HeavyImpact && action == 5280)
        {
            Heavy.Add(new(++generation, actor, cast.Location, rotation, activation));
            return;
        }
        if (component is C.SpiralThrust1 or C.SpiralThrust2 && action == 5286)
        {
            if (thrustDelay[component] > 0)
            {
                thrustDelay[component] = 0;
                Aoes.RemoveAll(a => a.Component == component);
            }
            Aoes.Add(new(++generation, component, actor, action, cast.Location, rotation, activation,
                RayRectangleTemplate.Symmetric(12, 54.2f)));
            return;
        }
        if (component == C.HolyShieldBash)
        {
            if (action == 5271)
                foreach (var (slot, target) in Party.Where(p => p.Slot < 8))
                    WildRoles[slot] = target.Actor.Id == Sanitize(cast.TargetId) ? ThordanWildRole.Target
                        : target.Actor.PartyRole != "Tank" ? ThordanWildRole.ShareNotFirst
                        : target.Actor.Id != Primary?.Actor.TargetId ? ThordanWildRole.Share : ThordanWildRole.Avoid;
            else if (action == 5266) WildChargeSource = actor;
            return;
        }
        if (action != Watched(component)) return;
        if (component is C.LightningStorm or C.DragonsRage)
        {
            if (Actors.GetValueOrDefault(Sanitize(cast.TargetId)) is { } target)
                Baits.Add(new(++generation, component, actor, target, activation));
            return;
        }
        GeometryTemplate? shape = component switch
        {
            C.Meteorain or C.Heavensflame => new CircleTemplate(6),
            C.AscalonsMercy => new FanTemplate(34.8f, 20 * MathF.PI / 180),
            C.AscalonsMercyHelper => new FanTemplate(34.5f, 20 * MathF.PI / 180),
            C.Conviction => new CircleTemplate(3),
            C.DimensionalCollapse => new CircleTemplate(9),
            // Keep caster references and current clocks, but never create a
            // fictitious navigational raidwide circle.
            C.HoliestOfHoly => new CircleTemplate(0),
            _ => null,
        };
        if (shape != null)
            Aoes.Add(new(++generation, component, actor, action, cast.Location, rotation, activation, shape));
    }

    private void Finish(ThordanActor actor, DungeonCastObservation cast)
    {
        if (!NativeNpc(actor.Actor) || cast.IsSpell != true) return;
        foreach (var component in componentOrder.ToArray())
        {
            if (component == C.DragonsGaze && cast.Action is 5257 or 5258)
            {
                var first = Aoes.FindIndex(a => a.Component == component);
                if (first >= 0) Aoes.RemoveAt(first);
            }
            else if (component == C.HeavyImpact && cast.Action is >= 5280 and <= 5283)
            {
                ++components[component];
                var order = (int)cast.Action - 5280;
                var sequence = Heavy.FirstOrDefault(s => s.Order == order
                    && MathF.Abs(s.Origin.X - cast.Location.X) <= 1 && MathF.Abs(s.Origin.Z - cast.Location.Z) <= 1
                    && MathF.Abs(Normalize(s.Rotation - (cast.Rotation ?? 0))) <= .05f);
                if (sequence == null) continue;
                ++sequence.Order;
                sequence.Activation = now.AddSeconds(2d);
                if (sequence.Order == 4) Heavy.Remove(sequence);
            }
            else if (cast.Action == Watched(component))
            {
                if (component is C.LightningStorm or C.DragonsRage)
                {
                    var index = Baits.FindIndex(b => b.Component == component && b.Target.Actor.Id == Sanitize(cast.TargetId));
                    if (index >= 0) Baits.RemoveAt(index);
                }
                else if (component is not (C.SpiralThrust1 or C.SpiralThrust2))
                {
                    var index = Aoes.FindIndex(a => a.Component == component && a.Source.Actor.Id == actor.Actor.Id);
                    if (index >= 0) Aoes.RemoveAt(index);
                }
            }
        }
    }

    internal void Effect(ulong source, uint action, DateTime time)
    {
        now = time;
        if (!Actors.TryGetValue(source, out var actor) || !NativeNpc(actor.Actor)) return;
        foreach (var component in componentOrder.ToArray())
        {
            if (action == Watched(component)) ++components[component];
            if (component is C.SpiralThrust1 or C.SpiralThrust2 && action == 4120
                && actor.Actor.Oid is 0x1457 or 0x1458 or 0x1459
                && actor.Actor.Position.X * actor.Actor.Position.X + actor.Actor.Position.Z * actor.Actor.Position.Z > 625)
                Aoes.Add(new(++generation, component, actor, 5286, Quantized(actor.Actor.Position),
                    MathF.Atan2(-actor.Actor.Position.X, -actor.Actor.Position.Z), now.AddSeconds(thrustDelay[component]),
                    RayRectangleTemplate.Symmetric(12, 54.2f), false));
            if (component == C.SkywardLeap && action == 5289)
            {
                var index = Baits.FindIndex(b => b.Component == component);
                if (index >= 0) Baits.RemoveAt(index);
            }
            if (component == C.SpiralPierce && action == 5287 && !Baits.Any(b => b.Component == component))
                pierceActivation = default;
            if (component == C.HiemalStormSpread && action == 5295)
                Baits.RemoveAll(b => b.Component == component);
            if (component == C.LightOfAscalon)
            {
                if (action == 5263 && Light.Count > 0) Light.RemoveAt(0);
                else if (action == 4187)
                    for (var i = 0d; i < 7; ++i)
                        Light.Add(new(new(-.822f, 0, -16.314f), 3, now.AddSeconds(10.7d + i * 1.3d)));
            }
            if (component == C.HolyShieldBash)
            {
                if (action == 5271 && Count(component) == 0) components[component] = 1;
                else if (action == 5266) { WildChargeSource = null; components[component] = 2; }
            }
        }
    }

    internal void Icon(ulong source, uint icon, DateTime time)
    {
        now = time;
        if (!Actors.TryGetValue(source, out var actor)) return;
        if (icon == 14 && Has(C.SkywardLeap) && Primary != null)
            Baits.Add(new(++generation, C.SkywardLeap, Primary, actor, default));
        if (icon == 29 && Has(C.HiemalStormSpread))
            Baits.Add(new(++generation, C.HiemalStormSpread, actor, actor, now.AddSeconds(3d)));
    }

    internal void Animation(ulong source, uint state)
    {
        if (Has(C.DragonsGaze) && state == 0x00040008
            && Actors.GetValueOrDefault(source) is { Actor.Oid: >= 0x1E9E27 and <= 0x1E9E2E } actor)
        {
            var angle = (180 - (int)(actor.Actor.Oid - 0x1E9E27) * 45) * MathF.PI / 180;
            EyePositionHint = new(MathF.Sin(angle) * 19, 0, MathF.Cos(angle) * 19);
        }
    }

    private void Tether(ThordanActor actor, ReplayTether tether, bool gained)
    {
        foreach (var component in componentOrder.ToArray()) Tether(component, actor, tether, gained);
    }

    private void Tether(C component, ThordanActor actor, ReplayTether tether, bool gained)
    {
        if (component == C.BurningChains && tether.Id == 9)
        {
            var target = Actors.GetValueOrDefault(Sanitize(tether.TargetId));
            if (gained && target == null) return;
            SetPartner(actor.Actor.Id, gained ? target : null);
            SetPartner(Sanitize(tether.TargetId), gained ? actor : null);
        }
        if (component != C.SpiralPierce || tether.Id != 5
            || !Actors.TryGetValue(Sanitize(tether.TargetId), out var other)) return;
        var sourcePlayer = actor.Actor.Player || actor.Actor.Buddy;
        var targetPlayer = other.Actor.Player || other.Actor.Buddy;
        if (sourcePlayer == targetPlayer) return;
        var source = sourcePlayer ? other : actor;
        var targetActor = sourcePlayer ? actor : other;
        if (gained)
        {
            if (pierceActivation == default) pierceActivation = now;
            Baits.Add(new(++generation, component, source, targetActor, pierceActivation));
        }
        else
        {
            var index = Baits.FindIndex(b => b.Component == component && b.Source.Actor.Id == source.Actor.Id
                && b.Target.Actor.Id == targetActor.Actor.Id);
            if (index >= 0) Baits.RemoveAt(index);
        }
    }

    private void SetPartner(ulong id, ThordanActor? target)
    {
        var slot = party.FirstOrDefault(p => p.EntityId == id)?.Slot ?? -1;
        if (slot is >= 0 and < 64) ChainPartners[slot] = target;
    }

    private static float Normalize(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }

    internal static Vector3 Quantized(Vector3 position)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(position.X * inverse) - .5f) * grid, position.Y,
            ((int)MathF.Round(position.Z * inverse) - .5f) * grid);
    }
}
