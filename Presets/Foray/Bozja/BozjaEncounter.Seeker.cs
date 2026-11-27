using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsSeeker => Definition.Key == "DRN1TrinitySeeker";
    internal float SeekerSampleRadius => seekerReduced ? DelubrumArenas.SeekerDefaultRadius : DelubrumArenas.SeekerStartingRadius;
    internal List<BozjaAoe> SeekerMercies { get; } = [];
    internal List<BozjaRush> SeekerBaits { get; } = [];
    internal List<BozjaCast> SeekerLines { get; } = [];
    private readonly BozjaActor?[] seekerPartners = new BozjaActor?[64];
    private IGeometryShape? seekerHostArena;
    private bool seekerReduced;
    private bool seekerPhantomEdge;
    internal BozjaActor? SeekerPartner(BozjaActor viewer)
    {
        var slot = ViewerSlot(viewer);
        return slot is >= 0 and < 64 ? seekerPartners[slot] : null;
    }

    private void ClearSeeker()
    {
        SeekerMercies.Clear();
        SeekerBaits.Clear();
        SeekerLines.Clear();
        Array.Clear(seekerPartners);
        seekerHostArena = null;
        seekerReduced = seekerPhantomEdge = false;
    }

    internal IGeometryShape SeekerHostArena
        => seekerHostArena ??= new WorldPolygonGeometry(Arena.GetContours().OrderByDescending(c => c.Count).First(), Arena.Position);

    internal void MapEffect(byte index, uint state)
    {
        HeavyMap(index);
        RhalgrMap(index, state);
        GuardMap(index, state);
        QueenMap(index, state);
        DiabloMap(index, state);
        if (Active && IsGauntlet && gauntletRules.Any(r => r.Kind == BozjaRuleKind.GauntletArena)
            && index == 0x31 && state == 0x20001)
        {
            Arena = new RectangleGeometry(Center, 0, 46, 46);
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GauntletArena);
        }
        if (!Active || !IsSeeker || index != 0x1B || state != 0x00020001) return;
        Arena = DelubrumArenas.SeekerDefault(Center.Y);
        Center = Arena.Position;
        seekerReduced = true;
        seekerHostArena = null;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.SeekerArena);
    }

    private void SeekerCreated(BozjaActor actor)
    {
        if (!Active || !IsSeeker || actor.Actor.Oid != 0x3132) return;
        Eyes.Clear();
        Eyes.Add(new(++serial, actor.Actor.Position, Now.AddSeconds(5.8d), false));
    }

    private void StartSeeker(BozjaActor actor, BozjaRule rule)
    {
        var cast = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.SeekerArena)
        {
            if (seekerReduced) return;
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, 3.8d)));
        }
        if (rule.Kind == BozjaRuleKind.SeekerBlade)
        {
            seekerPhantomEdge = cast.ActionId == 23202;
            IGeometryShape shape;
            if (seekerReduced)
                shape = seekerPhantomEdge ? DelubrumArenas.SeekerBladeInner(0) : DelubrumArenas.SeekerBladeOuter(0);
            else
                shape = seekerPhantomEdge ? DelubrumArenas.SeekerBladeInnerStarting(0) : DelubrumArenas.SeekerBladeOuterStarting(0);
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, .1d))
                { ShapeOverride = new DungeonShapeTemplate(shape), Action = cast.ActionId });
        }
        if (rule.Kind == BozjaRuleKind.SeekerSplitter)
        {
            var distance = MathF.Sqrt(DistanceSquared(actor.Actor.Position, Center));
            GeometryTemplate[] shapes = distance is < 3 or > 9 and < 11 or > 17 and < 19
                ? [new CircleTemplate(4), new DonutTemplate(8, 12), new DonutTemplate(16, 20)]
                : [new DonutTemplate(4, 8), new DonutTemplate(12, 16), new DonutTemplate(20, 25)];
            foreach (var shape in shapes)
                Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(Center), 0, Activation(actor))
                    { ShapeOverride = shape });
        }
    }

    private void SeekerStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!Active || !IsSeeker || !gain || status.Id != 2056) return;
        var degrees = status.Param switch { 0xF7 => -45f, 0xF8 => -135f, 0xF9 => 45f, 0xFA => 135f, _ => 0 };
        if (degrees == 0) return;
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.SeekerMercy);
        var primaryRotation = actor.Actor.Rotation;
        if (Primary is { Actor.Cast: { } primaryCast })
            primaryRotation = primaryCast.Rotation ?? Primary.Actor.Rotation;
        var rotation = primaryRotation + degrees * (MathF.PI / 180f);
        SeekerMercies.Add(new(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            rotation, Now.AddSeconds(13.4d + 1.7d * SeekerMercies.Count)), false));
        if (SeekerMercies.Count > 1) PromoteSeekerMercy();
    }

    private void PromoteSeekerMercy()
    {
        SeekerMercies[0] = SeekerMercies[0] with { Risky = true };
        if (SeekerMercies.Count < 2) return;
        SeekerMercies[0] = SeekerMercies[0] with { Danger = true };
        var delta = SeekerMercies[0].Cast.Rotation - (SeekerMercies[1].Cast.Rotation + 180f * (MathF.PI / 180f));
        while (delta < -MathF.PI) delta += MathF.Tau;
        while (delta > MathF.PI) delta -= MathF.Tau;
        if (MathF.Abs(delta) <= MathF.PI / 180f) SeekerMercies[1] = SeekerMercies[1] with { Risky = false };
    }

    private void SeekerIcon(BozjaActor actor, uint icon, ulong targetId)
    {
        if (!Active || (!IsSeeker && !IsCuchulainn && !IsDiablo) || Primary == null) return;
        if (IsDiablo)
        {
            if (icon == 23)
                SeekerBaits.Add(new(++serial, Definition.Rules.Single(r => r.Component == "PillarOfShamashBait"),
                    Primary, Actors.GetValueOrDefault(targetId) ?? actor, 0, Now.AddSeconds(5.1d)));
            return;
        }
        if (IsCuchulainn)
        {
            if (icon == 40)
                SeekerBaits.Add(new(++serial, Definition.Rules.Single(r => r.Component == "FellFlowBait"),
                    Primary, Actors.GetValueOrDefault(targetId) ?? actor, 0, Now.AddSeconds(5.2d)));
            return;
        }
        var component = icon switch { 243 => "MercifulArc", 237 => "DeadIron", _ => null };
        if (component == null) return;
        var rule = Definition.Rules.Single(r => r.Component == component);
        SeekerBaits.Add(new(++serial, rule, Primary, Actors.GetValueOrDefault(targetId) ?? actor, 0,
            Now.AddSeconds(icon == 243 ? 5.1d : 4.6d)));
    }

    private void SeekerTether(BozjaActor actor, (uint Id, ulong Target) tether, bool remove)
    {
        if (!Active || !IsSeeker || tether.Id != 128) return;
        if (remove)
        {
            Set(actor.Actor.Id, null);
            Set(tether.Target, null);
        }
        else if (Actors.GetValueOrDefault(tether.Target) is { } target)
        {
            Set(actor.Actor.Id, target);
            Set(tether.Target, actor);
        }
        void Set(ulong id, BozjaActor? partner)
        {
            var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot).FirstOrDefault(p => p.EntityId == id)?.Slot;
            if (slot is { } value) seekerPartners[value] = partner;
        }
    }

    private void SeekerEffect(BozjaActor caster, uint action, ulong target)
    {
        if (!IsSeeker && !IsDiablo) return;
        if (action == 23348 && SeekerMercies.Count > 0)
        {
            SeekerMercies.RemoveAt(0);
            if (SeekerMercies.Count > 0) PromoteSeekerMercy();
        }
        if (action == 23212) Eyes.Clear();
        if (action is 23336 or 23337) Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.SeekerBlade);
        if (action is 23223 or 23216 || IsDiablo && action == 23740)
        {
            var index = SeekerBaits.FindIndex(b => b.Rule.Actions.Contains(action));
            if (index >= 0) SeekerBaits.RemoveAt(index);
        }
        if ((IsSeeker && action == 14588 || IsDiablo && action == 23741) && Actors.GetValueOrDefault(target) is { } marked)
        {
            var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.SeekerLine);
            SeekerLines.Add(new(++serial, rule, caster, default, 0, Now.AddSeconds(IsDiablo ? 6d : 5.9d)) { Target = marked });
        }
        if (IsSeeker && action == 23221 || IsDiablo && action == 23742)
        {
            if (SeekerLines.Count == 1 && SeekerLines[0].Target!.Actor.Id != target && Actors.GetValueOrDefault(target) is { } replacement)
                SeekerLines[0].Target = replacement;
            var index = SeekerLines.FindIndex(b => b.Target!.Actor.Id == target);
            if (index >= 0) SeekerLines.RemoveAt(index);
        }
    }

    private void UpdateSeeker()
    {
        if (IsSeeker || IsCuchulainn || IsDiablo) SeekerBaits.RemoveAll(b => b.Target.Actor.Dead);
    }

    internal static BozjaCast SeekerLineView(BozjaCast bait, bool drawing = false)
    {
        var direction = bait.Target!.Actor.Position - bait.Source.Actor.Position;
        // GenericBaitStack uses Angle.FromDirection even when source==target.
        return bait with { Origin = bait.Source.Actor.Position,
            Rotation = drawing && ReferenceEquals(bait.Source, bait.Target) ? bait.Source.Actor.Rotation : MathF.Atan2(direction.X, direction.Z) };
    }

    private static bool SeekerLineContains(BozjaCast bait, Vector3 point)
    {
        var cast = SeekerLineView(bait, true);
        var (sin, cos) = Math.SinCos(cast.Rotation);
        var dx = point.X - cast.Origin.X;
        var dz = point.Z - cast.Origin.Z;
        var along = dx * (float)sin + dz * (float)cos;
        var side = dx * (float)cos - dz * (float)sin;
        // Source AOEShapeRect.Check uses double SinCos narrowed to float.
        // GeometryHelper's MathF path differs at finite oblique boundaries.
        var shape = (RayRectangleTemplate)cast.Shape;
        return along >= 0 && along <= shape.Length && side >= shape.MinLocalX && side <= shape.MaxLocalX;
    }

    internal IGeometryShape SeekerBladeDrawing(BozjaCast blade, float y)
    {
        // Custom AOE capture draws its relative polygon at the current arena
        // center, while Check and ShapeDistance keep the Start origin.
        var starting = blade.Origin == Definition.Center;
        var center = Center with { Y = y };
        if (blade.Action == 23202)
            return starting ? DelubrumArenas.SeekerBladeInnerStarting(y, center) : DelubrumArenas.SeekerBladeInner(y, center);
        return starting ? DelubrumArenas.SeekerBladeOuterStarting(y, center) : DelubrumArenas.SeekerBladeOuter(y, center);
    }

    private IEnumerable<BozjaHint> SeekerHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.SeekerChains && SeekerPartner(viewer) != null) yield return new("Break the chains!");
        if (rule.Kind == BozjaRuleKind.SeekerBlade && Casts.FirstOrDefault(c => c.Rule == rule) is { } blade)
            yield return new(seekerPhantomEdge ? "Stay in front of barricade!" : "Hide behind barricade!",
                !blade.Shape.Resolve(blade.Origin, 0, null).Contains(viewer.Actor.Position, 0));
        if (rule.Kind == BozjaRuleKind.SeekerIconBait)
        {
            var baits = SeekerBaits.Where(b => b.Rule == rule && !b.Source.Actor.Dead).ToArray();
            if (baits.Any(b => b.Target.Actor.Id == viewer.Actor.Id && Party.Any(p => p.EntityId != b.Target.Actor.Id
                && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } member && HornView(b).Contains(member.Actor.Position))))
                yield return new("Bait away from raid!");
            if (baits.Any(b => b.Target.Actor.Id != viewer.Actor.Id && HornView(b).Contains(viewer.Actor.Position)))
                yield return new("GTFO from baited aoe!");
        }
        if (rule.Kind != BozjaRuleKind.SeekerLine) yield break;
        var lines = SeekerLines.Where(b => !b.Source.Actor.Dead).ToArray();
        if (lines.Length == 0) yield break;
        var own = lines.Any(b => b.Target!.Actor.Id == viewer.Actor.Id);
        var other = lines.Any(b => b.Target!.Actor.Id != viewer.Actor.Id && SeekerLineContains(b, viewer.Actor.Position));
        yield return new("Stack!", !own && !other);
        if (own && other) yield return new("GTFO from other stacks!");
    }

    private IEnumerable<BozjaZone> SeekerNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.SeekerChains && SeekerPartner(viewer) is { } partner)
            yield return new("seeker/chains", rule.Component, Now.AddSeconds(10d), new CircleTemplate(
                MathF.Sqrt(DistanceSquared(partner.Actor.Position, viewer.Actor.Position)) + 1), partner.Actor.Position, 0);
        if (rule.Kind == BozjaRuleKind.SeekerBlade && Casts.FirstOrDefault(c => c.Rule == rule) is { } blade)
            yield return new($"seeker/blade/{blade.Generation}", rule.Component, blade.Activation,
                new BozjaSeekerShelterTemplate(((DungeonShapeTemplate)blade.Shape).Shape), blade.Origin, 0);
        if (rule.Kind == BozjaRuleKind.SeekerIconBait)
            foreach (var bait in SeekerBaits.Where(b => b.Rule == rule && !b.Source.Actor.Dead))
            {
                var cast = HornView(bait);
                if (!ReferenceEquals(bait.Target, viewer))
                    yield return new($"seeker/bait/{bait.Generation}", rule.Component, bait.Activation, cast.Shape, cast.Origin, cast.Rotation);
                else if (!ReferenceEquals(bait.Source, bait.Target))
                    foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot))
                    {
                        if (Actors.GetValueOrDefault(member.EntityId) is not { Actor.Dead: false } actor || ReferenceEquals(actor, viewer)) continue;
                        var delta = actor.Actor.Position - cast.Origin;
                        yield return new($"seeker/bait/{bait.Generation}/{member.Slot}", rule.Component, bait.Activation,
                            new FanTemplate(100, cast.Shape is FanTemplate fan ? fan.AngleRadians
                                : 2 * (float)Math.Asin(((RayRectangleTemplate)cast.Shape).MaxLocalX / MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z))), cast.Origin, MathF.Atan2(delta.X, delta.Z));
                    }
            }
        if (rule.Kind != BozjaRuleKind.SeekerLine) yield break;
        var lines = SeekerLines.Where(b => !b.Source.Actor.Dead).ToArray();
        var own = lines.Any(b => ReferenceEquals(b.Target, viewer));
        var join = new List<BozjaCast>();
        foreach (var bait in lines)
        {
            var cast = SeekerLineView(bait);
            if (!ReferenceEquals(bait.Target, viewer))
            {
                if (own)
                    yield return new($"seeker/stack/{bait.Generation}/avoid", rule.Component, bait.Activation,
                        RayRectangleTemplate.Symmetric(16, ((RayRectangleTemplate)cast.Shape).Length), cast.Origin, cast.Rotation);
                else join.Add(cast);
            }
            else
            {
                var buddies = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
                    .Select(p => Actors.GetValueOrDefault(p.EntityId)).OfType<BozjaActor>()
                    .Where(a => !a.Actor.Dead && !ReferenceEquals(a, viewer) && !SeekerLines.Any(b => ReferenceEquals(b.Target, a)))
                    .Select(a => a.Actor.Position).ToArray();
                if (buddies.Length != 0)
                    yield return new($"seeker/stack/{bait.Generation}/buddy", rule.Component, bait.Activation,
                        null, default, 0, buddies, 2);
            }
        }
        if (join.Count > 0)
            yield return new("seeker/stack/join", rule.Component, lines[0].Activation,
                new BozjaSeekerStackTemplate(join.ToArray()), default, 0, Goal: true);
    }

    private IEnumerable<BozjaDamage> SeekerDamage(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.SeekerLine)
        {
            var lines = SeekerLines.Where(b => !b.Source.Actor.Dead).ToArray();
            if (lines.Length > 0) yield return new(ulong.MaxValue, lines.Min(b => b.Activation), "Shared");
        }
        if (rule.Kind != BozjaRuleKind.SeekerIconBait) yield break;
        ulong mask = 0;
        var raw = SeekerBaits.Where(b => b.Rule == rule).ToArray();
        foreach (var bait in raw.Where(b => !b.Source.Actor.Dead))
        {
            var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot).FirstOrDefault(p => p.EntityId == bait.Target.Actor.Id)?.Slot;
            if (slot is { } value) mask |= 1UL << value;
        }
        if (mask != 0) yield return new(mask, raw[0].Activation, rule.Component == "MercifulArc" ? "Tankbuster" : "Raidwide");
    }
}
