using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<EurekaExcaliburAoe> eurekaExcaliburOrdinary = [];
    private readonly List<EurekaExcaliburAoe> eurekaIceCrosses = [];
    private readonly List<EurekaExcaliburAoe> eurekaSteelstrikes = [];
    private readonly List<float> eurekaSteelAngles = [];
    private readonly List<EurekaExcaliburAoe> eurekaFireSwords = [];
    private readonly List<EurekaExcaliburAoe> eurekaIceSwords = [];
    private readonly Dictionary<AlphaActorState, ReplayStatus?[]> eurekaExcaliburStatuses = [];
    private readonly HashSet<int> eurekaSoulFire = [];
    private readonly HashSet<int> eurekaSoulIce = [];
    private EurekaThermalPattern? eurekaThermalPattern;
    private bool? eurekaNextIce;
    private int eurekaSteelCasts;
    internal IReadOnlyList<EurekaExcaliburAoe> EurekaSteelstrikes => eurekaSteelstrikes;
    internal IReadOnlyList<EurekaExcaliburAoe> EurekaFireSwords => eurekaFireSwords;
    internal IReadOnlyList<EurekaExcaliburAoe> EurekaIceSwords => eurekaIceSwords;
    internal IReadOnlyList<EurekaExcaliburAoe> EurekaIceCrosses => eurekaIceCrosses;
    internal IReadOnlyList<float> EurekaSteelAngles => eurekaSteelAngles;
    internal EurekaThermalPattern? EurekaThermalPattern => eurekaThermalPattern;
    internal bool? EurekaNextIce => eurekaNextIce;
    private bool IsEurekaExcalibur => territory == EurekaOrthosCatalog.EXCALIBUR;

    private static GeometryTemplate? EurekaExcaliburShape(uint action) => action switch
    {
        31339 => new PtDonutSectorTemplate(2, 7, MathF.PI),
        31340 => new PtDonutSectorTemplate(7, 12, MathF.PI),
        31341 => new PtDonutSectorTemplate(17, 22, MathF.PI),
        31342 => new FanTemplate(80, 45 * (MathF.PI / 180f)),
        32701 => RayRectangleTemplate.Symmetric(8, 40),
        31346 or 31347 => new CircleTemplate(6),
        31344 => new CircleTemplate(8),
        31328 => new DonutTemplate(5, 40),
        31327 => new CircleTemplate(10),
        _ => null,
    };

    private void EurekaExcaliburCast(DungeonPendingCast cast)
    {
        if (!IsEurekaExcalibur || !cast.Observed) return;
        EurekaExcaliburAoe Aoe(GeometryTemplate shape, Vector3 position, float angle, DateTime activation)
            => new(++eurekaGeneration, shape, position, angle, activation, _now, cast);
        if (EurekaExcaliburShape(cast.Rule.Action) is { } ordinary)
        {
            eurekaExcaliburOrdinary.Add(Aoe(ordinary, cast.Location, cast.Rotation, cast.FinishAt));
            if (cast.Rule.Action is 31346 or 31347)
            {
                var circles = eurekaExcaliburOrdinary.Where(a => a.Cast.Rule.Action is 31346 or 31347).ToList();
                if (circles.Count >= 99)
                {
                    DungeonAoeOrder.Sort(circles, a => a.Activation);
                    eurekaExcaliburOrdinary.RemoveAll(a => a.Cast.Rule.Action is 31346 or 31347);
                    eurekaExcaliburOrdinary.AddRange(circles);
                }
            }
        }
        var center = Arena!.Position;
        switch (cast.Rule.Action)
        {
            case 31333:
                var first = eurekaSteelCasts == 0;
                var start = (first ? -45f : -20f) * (MathF.PI / 180f);
                var step = 10f * (MathF.PI / 180f);
                for (var i = 0; i < (first ? 10 : 5); ++i)
                {
                    var angle = cast.Rotation + start + i * step;
                    for (var j = 0; j < (first ? 1 : 3); ++j)
                    {
                        var rotated = angle + j * (120f * (MathF.PI / 180f));
                        eurekaSteelAngles.Add(rotated);
                        eurekaSteelstrikes.Add(Aoe(RayRectangleTemplate.Symmetric(4, 20), center, rotated, default));
                    }
                }
                ++eurekaSteelCasts;
                break;
            case 32701:
            case 31353:
                foreach (var angle in eurekaSteelAngles)
                    eurekaSteelstrikes.Add(Aoe(new RectangleTemplate(4, 40), center, angle, cast.FinishAt.AddSeconds(3.4d)));
                break;
            case 31329:
                eurekaNextIce = false;
                break;
            case 31330:
                eurekaNextIce = true;
                break;
            case 31349:
            case 31350:
                var (sin, cos) = Math.SinCos(cast.Rotation + MathF.PI / 2);
                eurekaThermalPattern = new(++eurekaGeneration, cast, cast.Rule.Action == 31350,
                    cast.Rotation, new Vector3((float)sin, 0, (float)cos) * 4, _now, cast.FinishAt.AddSeconds(1.9d));
                break;
            case 31346:
                foreach (var degrees in new[] { 44.998f, -.003f })
                    eurekaIceCrosses.Add(Aoe(new DungeonCrossTemplate(40, 2.5f), cast.Location,
                        degrees * (MathF.PI / 180f), cast.FinishAt.AddSeconds(5.2d)));
                break;
        }
    }

    private void EurekaExcaliburFinish(DungeonPendingCast cast)
    {
        if (!IsEurekaExcalibur || !cast.Observed) return;
        EurekaExcaliburOrdinaryFinish(cast.Source, cast.Rule.Action);
        if (cast.Rule.Action is 31334 or 31335 or 31336 or 31337)
            eurekaSteelstrikes.Clear();
        if (cast.Rule.Action is 31335 or 31336 or 31337)
        {
            eurekaSteelAngles.Clear();
            eurekaFireSwords.Clear();
            eurekaIceSwords.Clear();
        }
    }

    private void EurekaExcaliburOrdinaryFinish(ulong source, uint action)
    {
        // SimpleAOEGroups consumes the first source match even on another action's Finish.
        var circles = eurekaExcaliburOrdinary.FindIndex(a => a.Cast.Source == source
            && a.Cast.Rule.Action is 31346 or 31347);
        if (circles >= 0) eurekaExcaliburOrdinary.RemoveAt(circles);
        if (action is 31346 or 31347) return;
        var index = eurekaExcaliburOrdinary.FindIndex(a => a.Cast.Source == source && a.Cast.Rule.Action == action);
        if (index >= 0) eurekaExcaliburOrdinary.RemoveAt(index);
    }

    private void EurekaExcaliburUnknownFinish(DungeonActor previous, DungeonActor current)
    {
        if (!IsEurekaExcalibur || previous.Cast is not { } old || !EurekaSpellSource(previous.Id)
            || Definitions[0].Casts.Any(c => c.Action == old.Action)) return;
        var next = current.Cast;
        if (next == null || old.Action != next.Action || old.Location != next.Location
            || old.Rotation != next.Rotation || old.TargetId != next.TargetId || old.TotalTime != next.TotalTime)
            EurekaExcaliburOrdinaryFinish(previous.Id, old.Action);
    }

    private void EurekaExcaliburStatus(AlphaActorState actor)
    {
        if (!IsEurekaExcalibur || Active == null || !eurekaEnabled) return;
        var next = EurekaKaliyaStatusSlots(actor.Actor);
        var old = eurekaExcaliburStatuses.GetValueOrDefault(actor) ?? new ReplayStatus?[60];
        if (!eurekaChimeraBootstrapped || actor.Actor.NativeType != 0x202)
            for (var slot = 0; slot < next.Length; ++slot)
            {
                var previous = old[slot];
                var current = next[slot];
                var same = previous?.Id == current?.Id && previous?.SourceId == current?.SourceId;
                if (previous != null && !same) EurekaExcaliburStatusChange(actor, previous, false);
                if (current != null && (!same || previous?.Param != current.Param
                    || (current.ExpiresAt - previous!.ExpiresAt).TotalSeconds > 1d))
                    EurekaExcaliburStatusChange(actor, current, true);
            }
        eurekaExcaliburStatuses[actor] = next;
    }

    private void EurekaExcaliburStatusChange(AlphaActorState actor, ReplayStatus status, bool gain)
    {
        if (status.Id == 2552 && gain)
        {
            var sword = eurekaSteelstrikes.FirstOrDefault(a => EurekaSwordAngleEqual(a.Rotation, actor.Actor.Rotation));
            if (sword != null) (status.Param == 0x219 ? eurekaFireSwords : eurekaIceSwords).Add(sword);
            if (eurekaFireSwords.Count == 5 && eurekaIceSwords.Count == 5)
            {
                eurekaFireSwords.Sort((a, b) => a.Rotation.CompareTo(b.Rotation));
                eurekaIceSwords.Sort((a, b) => a.Rotation.CompareTo(b.Rotation));
            }
        }
        else if (status.Id is 3410 or 3411)
        {
            var slot = EurekaInputParty.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
            if (slot is < 0 or >= 64) return;
            var mask = status.Id == 3410 ? eurekaSoulFire : eurekaSoulIce;
            if (gain) mask.Add(slot); else mask.Remove(slot);
        }
    }

    private static bool EurekaSwordAngleEqual(float first, float second)
    {
        var difference = first - second;
        while (difference < -MathF.PI) difference += MathF.Tau;
        while (difference > MathF.PI) difference -= MathF.Tau;
        return MathF.Abs(difference) <= MathF.PI / 180f;
    }

    private void EurekaExcaliburEffect(uint action)
    {
        if (action is 31331 or 31332) eurekaNextIce = null;
        else if (action == 31351) eurekaThermalPattern = null;
        else if (action == 31348 && eurekaIceCrosses.Count != 0) eurekaIceCrosses.RemoveAt(0);
    }

    internal EurekaExcaliburAoe[] EurekaSteelSafeSwords()
    {
        if (!IsEurekaExcalibur || eurekaSteelstrikes.Count == 0) return [];
        var slot = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == PlayerId)?.Slot ?? -1;
        var swords = eurekaNextIce == true && eurekaSoulIce.Contains(slot) ? eurekaFireSwords
            : eurekaNextIce == false && eurekaSoulFire.Contains(slot) ? eurekaIceSwords : null;
        // Incomplete capture cannot index the source's empty sword array. Retain the model
        // and ordinary hazards until at least one corresponding original sword was observed.
        return swords is { Count: > 0 } ? [swords[0], swords[^1]] : [];
    }

    internal (Vector3 Danger, float DangerRotation, Vector3 Safe, float SafeRotation, DateTime Activation)? EurekaThermalSides()
    {
        if (!IsEurekaExcalibur || eurekaThermalPattern is not { } pattern || Arena == null) return null;
        var slot = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == PlayerId)?.Slot ?? -1;
        var condition = pattern.FireLeft ? eurekaSoulFire.Contains(slot) : eurekaSoulIce.Contains(slot);
        var check = condition ? 1f : -1f;
        return (Arena.Position + (condition ? pattern.Offset : -pattern.Offset),
            pattern.Rotation + check * (MathF.PI / 2),
            Arena.Position + (condition ? -pattern.Offset : pattern.Offset),
            pattern.Rotation - check * (MathF.PI / 2), pattern.Activation);
    }

    private IEnumerable<(EurekaExcaliburAoe Aoe, bool Safe)> EurekaExcaliburVisible()
    {
        foreach (var action in new uint[] { 31339, 31340, 31341, 31342, 32701, 31346, 31344, 31328, 31327 })
            foreach (var aoe in eurekaExcaliburOrdinary.Where(a => action == 31346
                ? a.Cast.Rule.Action is 31346 or 31347 : a.Cast.Rule.Action == action))
                yield return (aoe, false);
        var swords = EurekaSteelSafeSwords();
        foreach (var sword in swords) yield return (sword, true);
        foreach (var aoe in eurekaSteelstrikes.Where(a => !swords.Any(s => s.Rotation == a.Rotation)))
            yield return (aoe, false);
        foreach (var aoe in eurekaIceCrosses.Take(6)) yield return (aoe, false);
    }

    private IEnumerable<DungeonAmbientAoe> EurekaExcaliburAoes()
    {
        if (!IsEurekaExcalibur) yield break;
        if (EurekaThermalSides() is { } sides)
        {
            yield return new("eureka/excalibur/thermal/danger", "Thermal Divide",
                new FanTemplate(40, MathF.PI), sides.Danger, sides.DangerRotation, Activation: sides.Activation);
            // SafeFromAOE is a display color; its inverse hard constraint is adapted separately.
            yield return new("eureka/excalibur/thermal/safe", "Thermal Divide",
                new FanTemplate(40, MathF.PI), sides.Safe, sides.SafeRotation, Navigate: false, Safe: true,
                Activation: sides.Activation);
        }
        var index = 0;
        foreach (var (aoe, safe) in EurekaExcaliburVisible())
            yield return new($"eureka/excalibur/{aoe.Generation}/{index++}", aoe.Cast.Rule.Name,
                aoe.Shape, aoe.Origin, aoe.Rotation, Navigate: !safe, Safe: safe, Activation: aoe.Activation);
    }

    internal bool? EurekaThermalHintRisky(Vector3 position)
        => EurekaThermalSides() is { } sides
            ? !DungeonSourceGeometry.Contains(new FanTemplate(40, MathF.PI), sides.Safe, sides.SafeRotation, position) : null;

    private IEnumerable<string> EurekaExcaliburHints(Vector3 position)
    {
        if (!IsEurekaExcalibur || Active == null || !eurekaEnabled) yield break;
        if (EurekaExcaliburVisible().Any(a => !a.Safe
            && DungeonSourceGeometry.Contains(a.Aoe.Shape, a.Aoe.Origin, a.Aoe.Rotation, position)))
            yield return "GTFO from aoe!";
        if (eurekaNextIce is { } next)
        {
            yield return next ? "Next raidwide: Ice" : "Next raidwide: Fire";
            var slot = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == PlayerId)?.Slot ?? -1;
            if (next && eurekaSoulIce.Contains(slot)) yield return "Get hit by a fire blade!";
            else if (!next && eurekaSoulFire.Contains(slot)) yield return "Get hit by a single ice blade!";
        }
        if (EurekaThermalHintRisky(position) is { } risky)
            yield return risky ? "Go to safe side!" : "Go to safe side!（已在安全侧）";
    }

    private void ResetEurekaExcalibur()
    {
        eurekaExcaliburOrdinary.Clear();
        eurekaIceCrosses.Clear();
        eurekaSteelstrikes.Clear();
        eurekaSteelAngles.Clear();
        eurekaFireSwords.Clear();
        eurekaIceSwords.Clear();
        eurekaExcaliburStatuses.Clear();
        eurekaSoulFire.Clear();
        eurekaSoulIce.Clear();
        eurekaThermalPattern = null;
        eurekaNextIce = null;
        eurekaSteelCasts = 0;
    }
}
