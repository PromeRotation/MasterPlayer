using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsBurn => Definition.PrimaryOid == 0x31A0;
    private readonly List<BozjaActor> burnTrackers = [];
    private readonly List<BozjaActor> burnParticipants = [];
    private readonly List<BozjaActor> burnWeakpointCasters = [];
    private readonly Dictionary<ulong, float> burnWeakpoints = [];
    private readonly Dictionary<long, int> burnEscortHits = [];
    private DateTime burnTrackingActivation;
    private bool burnTrackingActive;
    internal int BurnBossHits { get; private set; }
    internal static readonly RayRectangleTemplate BurnEscortShape = RayRectangleTemplate.Symmetric(5, 60);

    private void ObserveBurnRawCast(BozjaActor actor)
    {
        if (!IsBurn && !IsGauntlet && !IsAllianceTrash) return;
        var next = actor.Actor.Cast is { } raw ? raw with
        {
            Rotation = raw.Rotation ?? actor.Actor.Rotation,
            TargetId = raw.TargetId == 0xE0000000 ? 0 : raw.TargetId,
        } : null;
        if (SameBurnRawCast(actor.RawCast, next)) return;
        actor.RawCast = next;
        actor.RawElapsed = next == null ? 0 : Math.Clamp((float)(Now - next.StartedAt).TotalSeconds, 0, BurnRawTotal(next));
        actor.Clock = Now;
    }

    // Live CastInfo preserves zero and sub-.05 TotalTime. Ordinary watched
    // casts use the legacy replay floor; raw weakpoint selection must not.
    private static float BurnRawTotal(ReplayCast cast)
        => cast.TotalTime ?? (float)(cast.FinishAt - cast.StartedAt).TotalSeconds - .3f;

    private static bool SameBurnRawCast(ReplayCast? a, ReplayCast? b)
        => a == null ? b == null : b != null && a.ActionId == b.ActionId && a.TargetId == b.TargetId
            && a.Location == b.Location && a.Rotation == b.Rotation && BurnRawTotal(a) == BurnRawTotal(b)
            && (a.IsSpell is not false) == (b.IsSpell is not false);

    private void ClearBurn()
    {
        burnTrackers.Clear();
        burnParticipants.Clear();
        burnWeakpointCasters.Clear();
        burnWeakpoints.Clear();
        burnEscortHits.Clear();
        burnTrackingActivation = default;
        burnTrackingActive = false;
        BurnBossHits = 0;
    }

    private void BurnStatus(BozjaActor actor, ReplayStatus status, bool gain, BozjaRuleKind kind)
    {
        if (!IsBurn) return;
        if (kind == BozjaRuleKind.BurnEscort && gain && status.Id == 2056)
        {
            burnTrackers.Add(actor);
            burnTrackingActive = true;
            if (burnTrackingActivation == default) burnTrackingActivation = status.ExpiresAt;
        }
        if (kind == BozjaRuleKind.BurnWeakpoint && status.Id is 2644 or 1709)
        {
            if (gain) burnWeakpoints[actor.Actor.Id] = status.Id == 2644 ? 0 : 180f * (MathF.PI / 180f);
            else burnWeakpoints.Remove(actor.Actor.Id);
        }
    }

    private void StartBurn(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        switch (rule.Kind)
        {
            case BozjaRuleKind.BurnEscort:
                if (spell.ActionId == 23614) burnTrackingActive = false;
                else
                {
                    burnTrackers.Clear();
                    Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
                }
                break;
            case BozjaRuleKind.BurnBoss:
                Casts.RemoveAll(c => c.Rule == rule);
                Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor, 1d)));
                break;
            case BozjaRuleKind.BurnWeakpoint:
                burnWeakpointCasters.Add(actor);
                break;
        }
    }

    private void BurnIcon(BozjaActor actor, uint icon)
    {
        if (!Active || !IsBurn || icon != 261) return;
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.BurnMissile);
        Casts.Add(new(++serial, rule, actor, actor.Actor.Position, 0, Now.AddSeconds(11d)));
    }

    private void BurnEffect(BozjaActor actor, uint action)
    {
        if (!IsBurn) return;
        if (action == 23610)
        {
            var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.BurnMissile);
            if (index >= 0) Casts.RemoveAt(index);
        }
        else if (action == 23613)
        {
            var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.BurnEscort
                && MathF.Abs(c.Origin.X - actor.Actor.Position.X) <= .1f
                && MathF.Abs(c.Origin.Z - actor.Actor.Position.Z) <= .1f);
            if (index < 0) return;
            var cast = Casts[index];
            var hits = burnEscortHits.GetValueOrDefault(cast.Generation) + 1;
            if (hits == 6)
            {
                Casts.RemoveAt(index);
                burnEscortHits.Remove(cast.Generation);
            }
            else burnEscortHits[cast.Generation] = hits;
        }
        else if (action == 24659)
        {
            var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.BurnBoss);
            Casts.RemoveAll(c => c.Rule == rule);
            if (++BurnBossHits >= 4) BurnBossHits = 0;
            else Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                actor.Actor.Rotation, Now.AddSeconds(1d)));
        }
    }

    internal IEnumerable<BozjaAoe> BurnAoes(BozjaRule rule)
    {
        if (rule.Kind is not (BozjaRuleKind.BurnMissile or BozjaRuleKind.BurnEscort or BozjaRuleKind.BurnBoss)) yield break;
        var casts = Casts.Where(c => c.Rule == rule);
        if (rule.Kind == BozjaRuleKind.BurnMissile) casts = casts.Take(2);
        foreach (var cast in casts) yield return new(cast, false, true);
    }

    // Tracking beams are extra Draw calls, not ActiveAOEs: no personal GTFO
    // hint and no duplicate base navigation are inferred from their appearance.
    internal IEnumerable<(BozjaCast Cast, bool Outline)> BurnTrackingViews(BozjaActor viewer)
    {
        if (!Active || !IsBurn) yield break;
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.BurnEscort);
        for (var i = 0; i < burnTrackers.Count; ++i)
        {
            var actor = burnTrackers[i];
            yield return (new(-100000 - i, rule, actor, actor.Actor.Position, actor.Actor.Rotation, burnTrackingActivation),
                actor.Actor.TargetId == viewer.Actor.Id);
        }
    }

    private IEnumerable<BozjaZone> BurnNavigation(BozjaActor viewer, BozjaRule rule)
    {
        foreach (var aoe in BurnAoes(rule))
            yield return new($"burn/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind != BozjaRuleKind.BurnEscort) yield break;
        for (var i = 0; i < burnTrackers.Count; ++i)
        {
            var caster = burnTrackers[i];
            if (caster.Actor.TargetId != viewer.Actor.Id || !burnTrackingActive)
                yield return new($"burn/tracking/{i}", rule.Component, burnTrackingActivation,
                    BurnEscortShape, caster.Actor.Position, caster.Actor.Rotation);
            else
            {
                // The source re-reads Count for every caster. The first empty
                // query fills the cache but iterates zero; a second caster in
                // this SAME query already uses it. Retain original references.
                var count = burnParticipants.Count;
                if (count == 0)
                    burnParticipants.AddRange(Actors.Values.Where(a => a.Actor.Oid == 0
                        && MathF.Abs(a.Actor.Position.X - Center.X) <= 24
                        && MathF.Abs(a.Actor.Position.Z - Center.Z) <= 24));
                for (var j = 0; j < count; ++j)
                {
                    var buddy = burnParticipants[j];
                    if (ReferenceEquals(buddy, viewer)) continue;
                    var delta = buddy.Actor.Position - caster.Actor.Position;
                    var distance = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
                    var half = (float)Math.Asin(2.5f / distance);
                    yield return new($"burn/tracking/{i}/buddy/{j}", rule.Component, burnTrackingActivation,
                        new FanTemplate(60, 2 * half), caster.Actor.Position, MathF.Atan2(delta.X, delta.Z));
                }
            }
        }
    }

    internal IEnumerable<BozjaEye> BurnEyes(BozjaActor viewer)
    {
        if (!Active || !IsBurn || !burnWeakpoints.TryGetValue(viewer.Actor.Id, out var forward)) yield break;
        BozjaActor? selected = null;
        var remaining = float.MaxValue;
        foreach (var actor in burnWeakpointCasters)
        {
            var raw = actor.RawCast;
            var rotation = raw?.Rotation ?? actor.Actor.Rotation;
            var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.BurnWeakpoint);
            var cast = new BozjaCast(0, rule, actor, actor.Actor.Position, rotation, default);
            if (!cast.Contains(viewer.Actor.Position)) continue;
            var next = raw == null ? 0 : BurnRawTotal(raw) - actor.RawElapsed;
            if (!(next < remaining)) continue;
            selected = actor;
            remaining = next;
        }
        if (selected == null) yield break;
        var rawCast = selected.RawCast;
        // Keep the two source float operations and only then convert to ticks.
        var activation = rawCast == null ? default : Now.AddSeconds(BurnRawTotal(rawCast) + .3f - selected.RawElapsed);
        yield return new(-200000 - selected.BirthOrder, selected.Actor.Position, activation, true,
            Forward: forward, Precise: true);
    }

    internal IEnumerable<BozjaEye> EyesFor(BozjaActor? viewer)
    {
        if (IsCll4 && Cll4DawonArena) return [];
        return viewer == null ? Eyes : Eyes.Concat(BurnEyes(viewer)).Concat(GauntletEyes(viewer)).Concat(OwainEyes(viewer));
    }
}
