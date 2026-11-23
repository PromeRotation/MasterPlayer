using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsCoilGauntlet => territory == CoilGauntletCatalog.TERRITORY;
    private int gauntletStage = -1;
    private DateTime gauntletStageAt;
    private bool gauntletWiping;
    private bool gauntletComplete;
    private bool gauntletStartWasTargetable;
    private int gauntletEmergencyHits;

    private bool GauntletCanStart(DungeonActor actor)
    {
        var targetable = _actors.Values.FirstOrDefault(a => a.Oid == 0x1E86F9)?.Targetable == true;
        if (gauntletComplete && gauntletStartWasTargetable && !targetable)
            gauntletComplete = false;
        gauntletStartWasTargetable = targetable;
        return !gauntletWiping && !gauntletComplete && actor.Oid == 0x1E86F9
            && _actors.Values.FirstOrDefault(a => a.Oid == 0x1E86F9)?.Targetable == false;
    }

    private void GauntletDirector(uint command)
    {
        if (!IsCoilGauntlet) return;
        if (command == 0x40000005) { gauntletWiping = true; ResetFight(); }
        if (command == 0x40000011) { gauntletWiping = false; gauntletComplete = false; }
    }

    private void GauntletTick()
    {
        if (!IsCoilGauntlet || Active == null) return;
        if (gauntletStage < 0)
        {
            gauntletStage = 0;
            gauntletStageAt = _now;
        }
        // BossModule removes destroyed actors from Enemies before phase checks.
        // An empty terminal list therefore does not satisfy IsDestroyed.
        if (_actors.Values.FirstOrDefault(a => a.Oid == 0x1E86F9)?.Targetable == true
            || _actors.Values.FirstOrDefault(a => a.Oid == 0x1E86FA)?.Targetable == true)
        {
            gauntletStartWasTargetable = _actors.Values.FirstOrDefault(a => a.Oid == 0x1E86F9)?.Targetable == true;
            gauntletComplete = true;
            ResetFight();
            return;
        }
        bool Any(uint oid, bool alive = false) => _actors.Values.Any(a => a.Oid == oid && a.Targetable && (!alive || !a.Dead));
        // Condition checks can advance early; the builder also advances when
        // overdue by one second. Enter times reset at the observed transition.
        for (var guard = 0; guard < 5 && gauntletStage < 5; ++guard)
        {
            var ready = gauntletStage switch
            {
                0 => Any(0x7E1) || Any(0x7E2),
                1 => Any(0x7E4),
                2 => Any(0x7E3),
                3 => Any(0x7E4, true) && Any(0x7E1, true) && Any(0x7E2, true),
                4 => Any(0x7E4, true) && Any(0x7E1, true) && Any(0x7E2, true) && Any(0x7E0, true),
                _ => false,
            };
            var deadline = gauntletStageAt.AddSeconds(gauntletStage == 0 ? 62.1 : 61);
            if (!ready && _now < deadline) break;
            if (!ready) warning?.Invoke($"T04 wave {gauntletStage}: source overdue transition.");
            ++gauntletStage;
            gauntletStageAt = _now;
            if (gauntletStage == 5) gauntletEmergencyHits = 0;
        }
    }

    private IEnumerable<DungeonAmbientAoe> GauntletAoes()
    {
        if (!IsCoilGauntlet || Active == null) yield break;
        if (gauntletStage >= 2)
            foreach (var aoe in CoilCleaves(0x7E4, 11, activeWhileCasting: true)) yield return aoe;
        if (gauntletStage < 3) yield break;
        foreach (var rook in _actors.Values.Where(a => a.Oid == 0x7E3))
        {
            if (!CoilCastActive(rook)) continue;
            var target = rook.Cast?.TargetId ?? 0;
            var rotation = rook.Cast?.Rotation;
            var finish = rook.Cast?.FinishAt;
            if (_casts.TryGetValue(rook.Id, out var cast))
            {
                target = cast.TargetId;
                rotation = cast.Rotation;
                finish = cast.FinishAt;
            }
            if (target != PlayerId || target == 0 || rotation is not { } direction || finish == null) continue;
            yield return new($"coil4/rook/{rook.Id}", "绕到车背后", RayRectangleTemplate.Symmetric(100, 50),
                rook.Position, direction, Activation: finish);
        }
    }

    private int GauntletPriority(DungeonActor actor)
        => actor.Oid == 0x7E4 ? 2 : actor.Oid is 0x8EA or 0x7E0 or 0x7E1 or 0x7E2 or 0x7E3 ? 1 : 0;

    private void ResetGauntlet()
    {
        gauntletStage = -1;
        gauntletEmergencyHits = 0;
    }
}
