using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private readonly List<BozjaActor> lunarPillars = [];
    private readonly BozjaSight lunarSight = new();
    internal List<BozjaCast> ThermalGusts { get; } = [];
    internal string? ThermalSequenceError { get; private set; }
    private readonly List<float> frostAngles = [];
    private readonly List<(BozjaCast Cast, float Increment, int Remaining)> frostSequences = [];
    private readonly List<BozjaAoe> frostPublished = [];
    private float frostIncrement;
    private Vector3 frostOrigin;
    private DateTime frostActivation;
    internal int NumFrostCasts { get; private set; }
    private int frostVersion;
    private int frostCount;

    private void ClearFrost()
    {
        lunarPillars.Clear();
        lunarSight.Casters.Clear();
        lunarSight.Safezones.Clear();
        lunarSight.Origin = null;
        ThermalGusts.Clear();
        ThermalSequenceError = null;
        frostAngles.Clear();
        frostSequences.Clear();
        frostPublished.Clear();
        frostIncrement = 0;
        frostOrigin = default;
        frostActivation = default;
        NumFrostCasts = frostVersion = frostCount = 0;
    }

    private void FrostCreated(BozjaActor actor, BozjaRule rule)
    {
        if (!Active || !actor.NativeNpc) return;
        if (rule.Kind == BozjaRuleKind.LunarCry && actor.Actor.Oid == 0x319E)
            lunarPillars.Add(actor);
        if (rule.Kind == BozjaRuleKind.ThermalGust && actor.Actor.Oid == 0x319F)
            ThermalGusts.Add(new(++serial, rule, actor,
                DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position), actor.Actor.Rotation, Now.AddSeconds(6.5d)));
    }

    private void StartFrost(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        switch (rule.Kind)
        {
            case BozjaRuleKind.BracingWind:
                Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
                break;
            case BozjaRuleKind.LunarCry:
                if (spell.ActionId == 23583) lunarPillars.Remove(actor);
                if (spell.ActionId != 23588) break;
                lunarSight.Casters.Add(actor);
                RefreshCrystalSight(lunarSight);
                if (lunarSight.Origin is { } origin && lunarPillars.Count > 0)
                    lunarSight.Safezones.Add(new(++serial, rule, actor, Center, 0, Activation(actor))
                    {
                        ShapeOverride = new BozjaMeteorTemplate(new(Center, origin, lunarPillars.ToArray(), 80, false)),
                    });
                break;
            case BozjaRuleKind.EndlessFrost:
                if (spell.ActionId is 23590 or 23591)
                    frostIncrement = (spell.ActionId == 23590 ? -40f : 40f) * (MathF.PI / 180);
                if (spell.ActionId == 23592)
                {
                    frostAngles.Add(spell.Rotation ?? 0);
                    frostOrigin = spell.Location;
                    frostActivation = Activation(actor);
                }
                if (frostAngles.Count != 6 || frostIncrement == 0) break;
                foreach (var angle in frostAngles)
                    frostSequences.Add((new(++serial, rule, actor, frostOrigin, angle, frostActivation), frostIncrement, 7));
                frostAngles.Clear();
                frostIncrement = 0;
                break;
        }
    }

    private void FinishFrost(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.BracingWind)
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts.RemoveAt(index);
        }
        if (rule.Kind == BozjaRuleKind.LunarCry && spell.ActionId == 23588)
        {
            lunarSight.Casters.Remove(actor);
            RefreshCrystalSight(lunarSight);
            if (lunarSight.Safezones.Count > 0) lunarSight.Safezones.RemoveAt(0);
        }
        if (rule.Kind == BozjaRuleKind.ThermalGust)
        {
            if (ThermalGusts.Count == 0)
            {
                ThermalSequenceError = $"ThermalGust 快照结束空队列：source={actor.Actor.Id:X} action={spell.ActionId}";
                throw new ArgumentOutOfRangeException(nameof(spell), ThermalSequenceError);
            }
            ThermalGusts.RemoveAt(0);
        }
        if (rule.Kind != BozjaRuleKind.EndlessFrost || spell.ActionId is not (23592 or 23593)) return;
        var matched = frostSequences.FindIndex(s => AlmostEqual(s.Cast.Origin, spell.Location)
            && MathF.Abs(NormalizeFrostAngle(s.Cast.Rotation - (spell.Rotation ?? 0))) <= .05f);
        if (matched < 0) return;
        ++NumFrostCasts;
        var sequence = frostSequences[matched];
        if (sequence.Remaining <= 1) frostSequences.RemoveAt(matched);
        else frostSequences[matched] = (sequence.Cast with
        {
            Rotation = sequence.Cast.Rotation + sequence.Increment,
            Activation = Now.AddSeconds(2d),
        }, sequence.Increment, sequence.Remaining - 1);
    }

    private static float NormalizeFrostAngle(float angle)
    {
        while (angle < -MathF.PI) angle += MathF.Tau;
        while (angle > MathF.PI) angle -= MathF.Tau;
        return angle;
    }

    private void UpdateFrost()
    {
        if (frostVersion == NumFrostCasts && frostCount == frostSequences.Count) return;
        frostVersion = NumFrostCasts;
        frostCount = frostSequences.Count;
        frostPublished.Clear();
        foreach (var (cast, increment, remaining) in frostSequences)
        {
            if (remaining > 1)
                frostPublished.Add(new(cast with
                {
                    Generation = ++serial,
                    Rotation = cast.Rotation + increment,
                    Activation = (cast.Activation > Now ? cast.Activation : Now).AddSeconds(2d),
                }, false));
            if (remaining != 0) frostPublished.Add(new(cast, remaining > 1));
        }
    }

    private IEnumerable<BozjaAoe> FrostAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.LunarCry && lunarSight.Safezones.FirstOrDefault() is { } safe)
            yield return new(safe, false);
        if (rule.Kind == BozjaRuleKind.ThermalGust)
            foreach (var cast in ThermalGusts) yield return new(cast, false);
        if (rule.Kind == BozjaRuleKind.EndlessFrost)
            foreach (var aoe in frostPublished) yield return aoe;
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> BracingMovements(BozjaActor viewer)
    {
        if (!Active) yield break;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.BracingWind))
        {
            if (WindImmune(viewer, cast.Activation) || !cast.Contains(viewer.Actor.Position)) continue;
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var from = viewer.Actor.Position;
            yield return (cast, from, from + 40 * new Vector3((float)sin, 0, (float)cos));
            yield break;
        }
    }

    private IEnumerable<BozjaZone> FrostNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.BracingWind)
            foreach (var cast in Casts.Where(c => c.Rule == rule))
            {
                var (sin, cos) = Math.SinCos(cast.Rotation);
                var dx = (float)sin;
                var dz = (float)cos;
                var length = MathF.Sqrt(dx * dx + dz * dz);
                dx /= length;
                dz /= length;
                var origin = new Vector3(cast.Origin.X + dx * 56 * .5f, cast.Origin.Y,
                    cast.Origin.Z + dz * 56 * .5f);
                yield return new($"bracing/{cast.Generation}", rule.Component, cast.Activation,
                    new RectangleTemplate(12, 40), origin, MathF.Atan2(dx, dz));
            }
        if (rule.Kind == BozjaRuleKind.LunarCry && Casts.Any(c => c.Rule.Kind == BozjaRuleKind.BracingWind)) yield break;
        foreach (var aoe in FrostAoes(rule))
            yield return new($"aoe/{aoe.Cast.Generation}/{aoe.Danger}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
    }
}
