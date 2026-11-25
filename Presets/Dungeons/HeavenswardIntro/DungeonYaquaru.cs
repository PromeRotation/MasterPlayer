using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsYaquaru => territory == HeavenswardIntroCatalog.YAQUARU;
    internal IReadOnlyList<AlphaActorState> YaquaruPuddles => palaceGuideLoaded?.YaquaruPuddles ?? [];
    internal ulong YaquaruShouldCleanse => palaceGuideLoaded?.YaquaruHeavy.ShouldCleanse ?? 0;
    private bool YaquaruBossInPuddle => IsYaquaru && Active != null && palaceGuideLoaded is { } module
        && module.YaquaruPuddles.Any(p => Vector2.Distance(new(module.Primary.Actor.Position.X, module.Primary.Actor.Position.Z),
            new(p.Actor.Position.X, p.Actor.Position.Z)) <= 8 + module.Primary.Actor.HitboxRadius);

    internal PalaceYaquaruPull? YaquaruPull => YaquaruBossInPuddle
        && palaceGuideLoaded!.Primary.Actor.TargetId == PlayerId
        ? new(palaceGuideLoaded.Primary, palaceGuideLoaded.YaquaruPuddles) : null;

    private void PalaceEarlyPairUpdate()
    {
        if (palaceGuideLoaded is not { Active: true } module) return;
        if (IsYaquaru)
        {
            if (!module.YaquaruInitialized)
            {
                module.YaquaruInitialized = true;
                module.YaquaruPuddles.AddRange(palaceGuideActors.Values.Where(a => a.Actor.Oid == 0x1E9998 && a.Actor.EventState != 7));
            }
            module.YaquaruHeavy.Observe(_actors.Values, PalaceEarlyPairRoster(), _now);
        }
        if (IsBlackRider)
        {
            var roster = PalaceEarlyPairRoster();
            if (!module.BlackRiderInitialized)
            {
                module.BlackRiderInitialized = true;
                foreach (var member in roster) module.BlackRiderEventRoster[member.Key] = member.Value;
            }
            module.BlackRiderImmunity.Observe(_actors.Values, roster);
        }
    }

    private Dictionary<ulong, int> PalaceEarlyPairRoster()
    {
        var roster = new Dictionary<ulong, int>();
        foreach (var member in CurrentPartyMembers) roster.TryAdd(member.EntityId, member.Slot);
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0)) roster.TryAdd(actor.Id, actor.PartySlot);
        return roster;
    }

    private int PalaceEarlyPairSlot(ulong actor) => CurrentPartyMembers.FirstOrDefault(m => m.EntityId == actor)?.Slot
        ?? _actors.GetValueOrDefault(actor)?.PartySlot ?? -1;

    internal bool UsesPalaceEarlyPairNativeEffect(uint action) => IsBlackRider && action == 7088 || IsYaquaru && action == 7091;

    private void PalaceEarlyPairCommitRoster()
    {
        if (!IsBlackRider || palaceGuideLoaded is not { Active: true } module) return;
        module.BlackRiderEventRoster.Clear();
        foreach (var member in PalaceEarlyPairRoster()) module.BlackRiderEventRoster[member.Key] = member.Value;
    }

    private void PalaceEarlyPairStatus(ulong actor, uint status, bool present, DateTime? expiry, ulong source)
    {
        if (palaceGuideLoaded is not { Active: true } module) return;
        if (IsBlackRider) module.BlackRiderImmunity.Status(PalaceEarlyPairSlot(actor), status, present, expiry ?? DateTime.MaxValue);
        if (!IsYaquaru || status != 14) return;
        if (present) module.YaquaruHeavy.Gain(actor, source);
        else module.YaquaruHeavy.Lose(actor, PalaceEarlyPairSlot(actor), _actors.GetValueOrDefault(actor)?.StatusSlots, source);
    }

    private IEnumerable<DungeonAmbientAoe> YaquaruAoes()
    {
        if ((IsBlackRider || IsYaquaru) && palaceGuideEnabled && Active == null
            && palaceGuideLoaded is { Started: false } loaded)
        {
            var primary = loaded.Primary.Actor;
            var boundary = Definitions[0].Arena(primary.Position);
            yield return new("palace-early/precombat/arena", "场地", new LocalPolygonTemplate(
                boundary.GetContours()[0].Select(p => MasterPlayer.Spatial.Geometry.GeometryHelper.WorldToLocal(p, boundary.Position, 0))),
                boundary.Position, Navigate: false, OutlineOnly: true);
            if (!primary.Dead && primary.Targetable)
                yield return HeavenswardIntroEnemyMarker(primary, boundary, "palace-early/precombat/enemy", new(1, 0, 0, 1));
        }
        if (!YaquaruBossInPuddle) yield break;
        var boss = palaceGuideLoaded!.Primary.Actor;
        yield return new("yaquaru/boss-in-puddle", "Pull boss out of puddle", new CircleTemplate(boss.HitboxRadius),
            boss.Position, Navigate: false, OutlineOnly: true);
        // Preserve YaquaruPull's exact 0/1000 score. ConvertGoalZones rejects a
        // binary-only plateau by its filled/positive-cell ratio, so no new goal is published.
    }

    private IEnumerable<string> YaquaruHints(Vector3? player)
    {
        if (!IsYaquaru || Active == null) yield break;
        if (YaquaruPull != null) yield return "Pull boss out of puddle!";
        if (YaquaruShouldCleanse != 0) yield return $"Heavy驱散建议（队伍槽mask 0x{YaquaruShouldCleanse:X}）";
        if (player is { } position && TrialVoidzoneAoes().Any(a =>
            a.Shape.Resolve(a.Position, a.Rotation, null).Contains(QuantizedDungeonPosition(position))))
            yield return "GTFO from aoe!";
    }
}
