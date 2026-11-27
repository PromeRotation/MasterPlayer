using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private void Cll1MagnetTether(BozjaActor actor, (uint Id, ulong Target) tether)
    {
        if (Cll1 is not { } state) return;
        if (tether.Id == 124)
        {
            var slot = Cll1Slot(actor.Actor.Id);
            if (slot < 0) return;
            state.MagnetTethered |= Cll1Bit(slot);
            foreach (var orb in state.Poles)
                if (orb.Id == tether.Target) orb.Targets.Add(actor);
            // The source intentionally waits for an Icon/PoleShift callback.
        }
        else if (tether.Id == 21)
        {
            Array.Clear(state.MagnetSources);
            var source = -1;
            var target = -1;
            for (var i = 0; i < state.Poles.Count; ++i)
            {
                if (state.Poles[i].Id == actor.Actor.Id) source = i;
                else if (state.Poles[i].Id == tether.Target) target = i;
            }
            if (source >= 0 && target >= 0)
            {
                var a = state.Poles[source];
                var b = state.Poles[target];
                state.Poles[source] = (a.Id, b.Position, a.Targets, a.Pole);
                state.Poles[target] = (b.Id, a.Position, b.Targets, b.Pole);
            }
            Cll1InitMagnets();
        }
    }

    private void Cll1Icon(BozjaActor actor, uint icon, ulong target)
    {
        if (!IsCll1 || !Active || Cll1 is not { } state) return;
        if (icon is 162 or 163)
        {
            for (var i = 0; i < state.Poles.Count; ++i)
            {
                var orb = state.Poles[i];
                if (orb.Id != actor.Actor.Id) continue;
                state.Poles[i] = (orb.Id, orb.Position, orb.Targets, (byte)(icon == 162 ? 1 : 2));
                Cll1InitMagnets();
                return;
            }
        }
        if (icon is not (231 or 232)) return;
        var slot = Cll1Slot(target);
        if (slot < 0) return;
        // Preserve the source's explicit eight-slot contract. An allied slot
        // beyond seven is an invalid event, not an alias for a party member.
        state.PlayerPoles[slot] = (byte)(icon == 231 ? 1 : 2);
        Cll1InitMagnets();
    }

    private void Cll1InitMagnets()
    {
        if (Cll1 is not { MagnetTethered: not 0 } state) return;
        foreach (var member in Party.Where(p => p.Slot is >= 0 and < 8).OrderBy(p => p.Slot))
        {
            var slot = member.Slot;
            if (state.MagnetSources[slot] != null || (state.MagnetTethered & Cll1Bit(slot)) == 0
                || state.PlayerPoles[slot] == 0 || Actors.GetValueOrDefault(member.EntityId) is not { } player) continue;
            foreach (var orb in state.Poles)
            {
                if (orb.Pole == 0 || !orb.Targets.Contains(player)) continue;
                state.MagnetSources[slot] = (orb.Position, Now.AddSeconds(8.2d), orb.Pole == state.PlayerPoles[slot]);
                break;
            }
        }
    }

    private (int Slot, BozjaActor Player, BozjaActor Enemy)? Cll1InfraredSides(
        BozjaActor source, (uint Id, ulong Target) tether)
    {
        if (tether.Id != 121 || Actors.GetValueOrDefault(tether.Target) is not { } target || Cll1 == null) return null;
        var sourcePlayer = Cll1.InfraredPlayers.Contains(source);
        var player = sourcePlayer ? source : target;
        var enemy = sourcePlayer ? target : source;
        return (Cll1Slot(player.Actor.Id), player, enemy);
    }

    private void Cll1InfraredTether(BozjaActor actor, (uint Id, ulong Target) tether, bool remove)
    {
        if (Cll1 is not { } state) return;
        // InterceptTether determines sides before the derived first callback
        // populates its persistent player list.
        if (Cll1InfraredSides(actor, tether) is { } sides)
        {
            var pair = (sides.Player, sides.Enemy);
            if (remove)
            {
                state.InfraredTethers.Remove(pair);
                state.InfraredTethered &= ~Cll1Bit(sides.Slot);
            }
            else
            {
                state.InfraredTethers.Add(pair);
                state.InfraredTethered |= Cll1Bit(sides.Slot);
            }
        }
        if (remove || tether.Id != 121) return;
        if (state.InfraredPlayers.Count == 0)
            state.InfraredPlayers.AddRange(Actors.Values.Where(a => a.Actor.Oid == 0 && Cll1InBottom(a.Actor.Position)));
        if (state.InfraredActivation == default) state.InfraredActivation = Now.AddSeconds(3.7d);
    }

    private void Cll1Tether(BozjaActor actor, (uint Id, ulong Target) tether, bool remove)
    {
        if (!IsCll1 || !Active) return;
        if (!remove) Cll1MagnetTether(actor, tether);
        Cll1InfraredTether(actor, tether, remove);
    }

    private void Cll1Status(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsCll1 || !Active || Cll1 is not { } state || status.Id != 1255) return;
        var bit = Cll1Bit(Cll1Slot(actor.Actor.Id));
        if (gain) state.Fire |= bit;
        else state.Fire &= ~bit;
    }

    internal IEnumerable<(Vector3 From, Vector3 To, DateTime Activation)> Cll1MagnetMovements(BozjaActor viewer)
    {
        var slot = Cll1Slot(viewer.Actor.Id);
        if (!Active || Cll1 == null || slot is < 0 or > 7 || Cll1.MagnetSources[slot] is not { } source) yield break;
        var from = viewer.Actor.Position;
        var delta = Cll1XZ(source.Away ? from - source.Origin : source.Origin - from);
        var length = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (!(length > 0)) yield break;
        var inverse = 1f / length;
        var distance = source.Away ? 30 : MathF.Min(30, length);
        var to = from + new Vector3(distance * (delta.X * inverse), 0, distance * (delta.Y * inverse));
        yield return (from, to, source.Activation);
    }

    private IEnumerable<BozjaHint> Cll1Hints(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsCll1 || Cll1 is not { } state) yield break;
        if (rule.Component == "WildCharges")
        {
            var charges = Casts.Where(c => c.Rule == rule).ToArray();
            if (charges.Length > 0) yield return new("Share damage inside wildcharge!", !charges.Any(c => c.Contains(viewer.Actor.Position)));
        }
        if (rule.Component == "Magnetism" && Cll1MagnetMovements(viewer).Any(m =>
            Casts.Any(c => c.Rule.Component == "OrbsAOE" && c.Contains(m.To))))
            yield return new("About to be knocked into danger!");
        var slot = Cll1Slot(viewer.Actor.Id);
        if (rule.Component == "InfraredBlast" && state.InfraredTethers.Count > 0
            && (state.InfraredTethered & Cll1Bit(slot)) == 0)
        {
            if ((state.Fire & Cll1Bit(slot)) != 0) yield return new("Avoid taking tether!");
            else if (state.InfraredTethers.Any(t => t.Player.Actor.Oid != 0)) yield return new("Grab the tether!");
        }
    }

    private IEnumerable<string> Cll1GlobalHints(BozjaRule rule)
    {
        if (!IsCll1 || rule.Component != "BossHealths" || Primary == null || Cll1 == null) yield break;
        static float Ratio(BozjaActor actor) => (float)actor.Actor.Hp / actor.Actor.MaxHp;
        var top = Ratio(Primary) * 100f;
        var bottom = Cll1.Enemies[0x2ED3].FirstOrDefault();
        yield return bottom == null ? $"Top: {top:f1}%, Bottom: unknown"
            : $"Top: {top:f1}%, Bottom: {Ratio(bottom) * 100f:f1}%";
    }

    // The old host does not execute PotentialTargets as PR target controls.
    // Keep the exact module priorities available as diagnostics only.
    internal int Cll1TargetPriority(BozjaActor actor, int current = 0)
    {
        if (!IsCll1 || Cll1 is not { } state || Primary == null) return current;
        var value = actor.Actor;
        static float Ratio(BozjaActor a) => (float)a.Actor.Hp / a.Actor.MaxHp;
        if (state.Top)
        {
            if (value.Oid == 0x2F9A) return 1;
            if (actor == Primary && state.HellDiver != null && Ratio(actor) - Ratio(state.HellDiver) < -.1f) return -4;
            if (value.Oid == 0x2F4E && Cll1InTop(value.Position)) return 0;
            if (value.Oid != 0x2ECC) return -2;
        }
        else
        {
            if (value.Oid == 0x2ED5) return 1;
            if (actor == state.HellDiver && Ratio(actor) - Ratio(Primary) < -.1f) return -4;
            if (value.Oid == 0x2F4E && Cll1InBottom(value.Position)) return 0;
            if (value.Oid != 0x2ED3) return -2;
        }
        return current;
    }

    private IEnumerable<BozjaZone> Cll1Navigation(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsCll1 || Cll1 is not { } state) yield break;
        if (rule.Component == "ChainCannon" || rule.Component == "OrbsAOE")
            foreach (var cast in Casts.Where(c => c.Rule == rule))
                yield return new($"cll1/aoe/{cast.Generation}", rule.Component, cast.Activation, cast.Shape, cast.Origin, cast.Rotation);
        if (rule.Component == "WildCharges")
        {
            var charges = Casts.Where(c => c.Rule == rule).ToArray();
            if (charges.Length > 0)
            {
                var rectangles = charges.Select(c =>
                {
                    var (sin, cos) = Math.SinCos(c.Rotation);
                    var ray = (RayRectangleTemplate)c.Shape;
                    return (Cll1XZ(c.Origin), new Vector2((float)sin, (float)cos), ray.Length, ray.MaxLocalX);
                }).ToArray();
                yield return new("cll1/wildcharges", rule.Component, charges[0].Activation,
                    new BozjaCll1IntersectionTemplate(rectangles), default, 0);
            }
        }
        var slot = Cll1Slot(viewer.Actor.Id);
        if (rule.Component == "Magnetism" && slot is >= 0 and < 8 && state.MagnetSources[slot] is { } source)
        {
            if (!source.Away)
                yield return new("cll1/magnet", rule.Component, source.Activation, null, default, 0, [source.Origin], JoinRadius: 35, Goal: true);
            else
            {
                var right = source.Origin.X > 90;
                var opposite = new Vector2(right ? 63 : 97, -222);
                var angle = right ? MathF.PI / 2 : -MathF.PI / 2;
                var (sin, cos) = Math.SinCos(angle);
                var direction = new Vector2((float)sin, (float)cos);
                yield return new("cll1/magnet", rule.Component, source.Activation,
                    new BozjaCll1MagnetTemplate(opposite + 35f * direction, -direction), default, 0, Goal: true);
            }
        }
        if (rule.Component == "InfraredBlast" && state.InfraredTethers.Count > 0
            && state.Enemies[0x2ED7].FirstOrDefault() is { } tunnel)
        {
            var rectangles = new List<(Vector2, Vector2, float, float)>();
            foreach (var tether in state.InfraredTethers)
            {
                if (tether.Player.Actor.Oid == 0 && tether.Player != viewer) continue;
                var target = Cll1XZ(tunnel.Actor.Position);
                var enemy = Cll1XZ(tether.Enemy.Actor.Position);
                var delta = enemy - target;
                var length = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                var normalized = length > 0 ? delta * (1f / length) : default;
                var from = target + (tunnel.Actor.HitboxRadius + .1f) * normalized;
                var dir = enemy - from;
                var rectLength = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                rectangles.Add((from, dir * (1f / rectLength), rectLength, .5f));
            }
            if (rectangles.Count > 0)
                yield return new("cll1/infrared", rule.Component, state.InfraredActivation,
                    new BozjaCll1IntersectionTemplate(rectangles.ToArray()), default, 0);
        }
    }

    internal IEnumerable<(Vector3 From, Vector3 To, bool Safe)> Cll1InfraredLines()
    {
        if (!Active || Cll1 is not { Top: false } state) yield break;
        foreach (var tether in state.InfraredTethers)
            yield return (tether.Enemy.Actor.Position, tether.Player.Actor.Position, state.InfraredPlayers.Contains(tether.Player));
    }
}
