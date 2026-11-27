using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsArt => Definition.PrimaryOid == 0x265A;
    private bool artMythcall;
    private List<BozjaActor>? artSpears;
    private readonly List<BozjaAoe> artLegend = [];
    internal List<BozjaArtChaser> ArtChasers { get; } = [];
    internal List<BozjaActor> ArtChaseTargets { get; } = [];
    internal DateTime[] ArtStay { get; } = new DateTime[64];
    internal BozjaDelubrumTimeline? ArtTimeline { get; private set; }
    private readonly Dictionary<BozjaActor, Vector3> artPrevious = [];
    internal int ArtChaseCount { get; private set; }
    internal DateTime ArtChaseActivation { get; private set; }
    internal ulong ArtChaseTargetsMask { get; private set; }
    internal int ArtLegendCount => artLegend.Count;

    private void ClearArt()
    {
        artMythcall = false;
        artSpears = null;
        artLegend.Clear();
        ArtChasers.Clear();
        ArtChaseTargets.Clear();
        Array.Clear(ArtStay);
        artPrevious.Clear();
        ArtChaseCount = 0;
        ArtChaseActivation = default;
        ArtChaseTargetsMask = 0;
        ArtTimeline?.Stop();
        ArtTimeline = null;
    }

    private void BeginArtFrame()
    {
        if (!IsArt) return;
        artPrevious.Clear();
        foreach (var actor in Actors.Values) artPrevious.Add(actor, actor.Actor.Position);
    }

    private void ArtCreated(BozjaActor actor)
    {
        if (IsArt && actor.Actor.Oid == 0x265B) artSpears?.Add(actor);
    }

    private void ArtDestroyed(BozjaActor actor) => artSpears?.Remove(actor);

    private void StartArt(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.ArtLegend)
        {
            if (spell.ActionId == 14631) artMythcall = true;
            if (spell.ActionId is not (14632 or 14633)) return;
            GeometryTemplate shape = spell.ActionId == 14632 ? new CircleTemplate(15) : new DonutTemplate(7, 22);
            artLegend.Add(new(new(++serial, rule, actor, spell.Location, 0, Activation(actor))
                { ShapeOverride = shape }, false, true));
            if (!artMythcall) return;
            artSpears ??= Actors.Values.Where(a => a.Actor.Oid == 0x265B).ToList();
            foreach (var spear in artSpears)
                artLegend.Add(new(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(spear.Actor.Position),
                    0, Activation(actor, 2.6f)) { ShapeOverride = shape }, false, true));
            artMythcall = false;
        }
        if (rule.Kind == BozjaRuleKind.ArtPredict)
        {
            if (spell.ActionId == 14643) Casts.RemoveAll(c => c.Rule == rule);
            else if (spell.ActionId == 14642)
                foreach (var rotation in new[] { 45f * (MathF.PI / 180f), 0f })
                    Casts.Add(new(++serial, rule, actor, spell.Location, rotation, Activation(actor, 6.9d))
                        { ShapeOverride = new DungeonCrossTemplate(35.5f, 4) });
        }
        if (rule.Kind == BozjaRuleKind.ArtChase && spell.ActionId == 14636)
        {
            BozjaActor? target = null;
            var minimum = float.MaxValue;
            foreach (var candidate in ArtChaseTargets)
            {
                var distance = DistanceSquared(candidate.Actor.Position, spell.Location);
                if (distance < minimum) { target = candidate; minimum = distance; }
            }
            if (target == null) return;
            ArtChaseTargets.Remove(target);
            var slot = ViewerSlot(target);
            if (slot is >= 0 and < 64) ArtChaseTargetsMask &= ~(1UL << slot);
            ArtChasers.Add(new(++serial, actor, target, spell.Location with { Y = 0 }, Activation(actor)));
        }
    }

    private void UpdateArt()
    {
        if (!IsArt) return;
        ArtTimeline?.Update();
        ArtChasers.RemoveAll(c => (c.Target.Destroyed || c.Target.Actor.Dead) && c.Remaining < 5);
    }

    private void ArtIcon(BozjaActor actor, uint icon, ulong target)
    {
        if (!IsArt || !Active || icon != 92) return;
        ArtChaseActivation = Now.AddSeconds(5.1d);
        ArtChaseTargets.Add(actor);
        var slot = Party.FirstOrDefault(p => p.EntityId == target)?.Slot ?? -1;
        if (slot is >= 0 and < 64) ArtChaseTargetsMask |= 1UL << slot;
    }

    private void ArtAnimation(ulong source, uint state)
    {
        if (!IsArt || !Active || Actors.GetValueOrDefault(source) is not { Actor.Oid: 0x1EA1A1 } actor
            || !AlmostEqual(actor.Actor.Position, new(-135, 0, 750))) return;
        if (state == 0x00010002) Array.Fill(ArtStay, Now);
        else if (state == 0x00040008) Array.Clear(ArtStay);
    }

    private void ArtEffect(BozjaActor actor, uint action, ulong mainTarget, Vector3? position)
    {
        if (!IsArt) return;
        if (artLegend.Count > 0 && action is 14632 or 14633 or 14634 or 14635) artLegend.RemoveAt(0);
        if (action is not (14636 or 14637)) return;
        var point = mainTarget == actor.Actor.Id ? DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position)
            : Actors.GetValueOrDefault(mainTarget)?.Actor.Position ?? position ?? default;
        BozjaArtChaser? selected = null;
        var minimum = float.MaxValue;
        ++ArtChaseCount;
        foreach (var chaser in ArtChasers)
        {
            var distance = DistanceSquared(chaser.Predicted(), point);
            if (distance < minimum) { minimum = distance; selected = chaser; }
        }
        if (selected != null)
        {
            if (--selected.Remaining <= 0) ArtChasers.Remove(selected);
            else
            {
                selected.Previous = point with { Y = 0 };
                selected.MoveDistance = 6.5f;
                selected.Activation = Now.AddSeconds(1.5d);
            }
        }
        if (ArtChasers.Count == 0)
        {
            ArtChaseTargets.Clear();
            ArtChaseCount = 0;
        }
    }

    private IEnumerable<BozjaAoe> ArtAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.ArtLegend)
        {
            if (artLegend.Count == 5) artLegend[0] = artLegend[0] with { Danger = true };
            foreach (var aoe in artLegend) yield return aoe;
        }
        if (rule.Kind == BozjaRuleKind.ArtPredict)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind == BozjaRuleKind.ArtChase)
            foreach (var chaser in ArtChasers)
            {
                var point = chaser.Predicted();
                var offset = point - chaser.Previous;
                var rotation = offset.X * offset.X + offset.Z * offset.Z > 0 ? MathF.Atan2(offset.X, offset.Z) : 0;
                yield return new(new(chaser.Generation, rule, chaser.Source, point, rotation, chaser.Activation), false, true);
            }
    }

    internal (string Mode, DateTime Activation)? ArtSpecialMode(BozjaActor viewer)
    {
        var slot = ViewerSlot(viewer);
        return IsArt && Active && slot is >= 0 and < 64 && ArtStay[slot] != default
            ? ("NoMovement", ArtStay[slot]) : null;
    }

    private IEnumerable<BozjaHint> ArtHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.ArtStay) yield break;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 64 || ArtStay[slot] == default) yield break;
        if (ArtStay[slot] <= Now.AddSeconds(1e3d))
        {
            var previous = artPrevious.GetValueOrDefault(viewer, viewer.Actor.Position);
            yield return new("Don't move!", float.IsNaN(previous.X) || float.IsNaN(previous.Z));
        }
        if (viewer.Actor.Dead) ArtStay[slot] = default;
    }

    private IEnumerable<BozjaZone> ArtNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.ArtChase)
        {
            foreach (var own in new[] { true, false })
                foreach (var chaser in ArtChasers.Where(c => ReferenceEquals(c.Target, viewer) == own))
                {
                    var point = chaser.Predicted();
                    var (sin, cos) = Math.SinCos(viewer.Actor.Rotation);
                    if (own) point -= 10 * new Vector3((float)sin, 0, (float)cos);
                    yield return new($"art/chase/{chaser.Generation}", rule.Component, chaser.Activation,
                        new CircleTemplate(own ? 16.5f : 11), point, 0);
                }
            yield break;
        }
        foreach (var aoe in ArtAoes(rule).Where(a => a.Risky))
            yield return new($"art/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
    }
}
