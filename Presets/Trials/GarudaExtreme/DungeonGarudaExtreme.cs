using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsGarudaExtreme => territory == GarudaExtremeCatalog.TERRITORY;
    private bool garudaExWiping;
    private bool garudaExInitialized;
    private bool garudaExPhase2;
    private int garudaExSongNumber;
    private int garudaExSubphase;
    private int garudaExAerialBlasts;
    private GarudaExtremeStep garudaExStep;
    private DateTime garudaExCombatSince;
    private DateTime garudaExWheelAt;
    private DungeonActor? garudaExEye;
    private DateTime garudaExEyeAt;
    private DungeonLosTemplate? garudaExLosSafe;
    private DungeonLosTemplate? garudaExLosDanger;
    private DateTime? garudaExSongAt;
    private bool GarudaExCombat => garudaExStep == GarudaExtremeStep.Combat;
    private bool GarudaExAdds => garudaExPhase2 && garudaExSubphase % 2 == 0 && GarudaExCombat;
    private bool GarudaExAi => garudaExPhase2 || GarudaExCombat;
    private bool GarudaExEyeEnabled => !garudaExPhase2 || garudaExSubphase % 2 == 1
        && garudaExStep is not (GarudaExtremeStep.AfterShriekReappear or GarudaExtremeStep.Terminal);
    private bool GarudaExWhirlwind => garudaExPhase2 && garudaExSubphase >= 2
        && garudaExSubphase % 2 == 0 && garudaExStep is GarudaExtremeStep.Combat or GarudaExtremeStep.Reappear;

    private void GarudaExDirector(uint command)
    {
        if (!IsGarudaExtreme) return;
        if (command == 0x40000005) { garudaExWiping = true; ResetFight(); }
        if (command == 0x40000011) garudaExWiping = false;
    }

    private void GarudaExObserve()
    {
        if (!IsGarudaExtreme || Active == null || !_actors.TryGetValue(PrimaryId, out var boss)) return;
        if (!garudaExInitialized)
        {
            garudaExInitialized = true;
            garudaExSongNumber = 1;
            garudaExSubphase = -1;
            GarudaExEnterCombat();
        }
        if (!garudaExPhase2 && _actors.Values.Any(a => a.Oid is 0x620 or 0x621))
        {
            garudaExPhase2 = true;
            garudaExSubphase = 0;
            garudaExEye = null;
            garudaExLosSafe = garudaExLosDanger = null;
            garudaExSongAt = null;
            GarudaExEnterCombat();
        }
        if (garudaExEye != null) garudaExEye = _actors.GetValueOrDefault(garudaExEye.Id) ?? garudaExEye;
        // Targetable states are observation driven; their annotated durations are not timers.
        for (var i = 0; i < 6; ++i)
        {
            var previous = garudaExStep;
            switch (garudaExStep)
            {
                case GarudaExtremeStep.Combat when !boss.Targetable
                    && (!garudaExPhase2 || garudaExSubphase != 0 || _now >= garudaExCombatSince.AddSeconds(60)):
                    garudaExStep = GarudaExtremeStep.Reappear;
                    garudaExWheelAt = default;
                    if (!garudaExPhase2)
                        GarudaExSnapshotLos(garudaExSongNumber == 1 ? new(0, boss.Position.Y, -13) : new(13, boss.Position.Y, 0));
                    break;
                case GarudaExtremeStep.Reappear when boss.Targetable:
                    garudaExStep = GarudaExtremeStep.CastStart;
                    break;
                case GarudaExtremeStep.CastStart when _casts.ContainsKey(PrimaryId) || boss.Cast != null:
                    garudaExStep = GarudaExtremeStep.CastEnd;
                    break;
                case GarudaExtremeStep.CastEnd when !_casts.ContainsKey(PrimaryId) && boss.Cast == null:
                    GarudaExAfterCast();
                    break;
                case GarudaExtremeStep.AfterShriekDisappear when !boss.Targetable:
                    garudaExEye = null;
                    garudaExStep = GarudaExtremeStep.AfterShriekReappear;
                    break;
                case GarudaExtremeStep.AfterShriekReappear when boss.Targetable:
                    ++garudaExSubphase;
                    GarudaExEnterCombat();
                    break;
            }
            if (previous == garudaExStep) break;
        }
        foreach (var source in _casts.Values.Where(c => !GarudaExCastAllowed(c.Rule.Action)).Select(c => c.Source).ToArray())
            _casts.Remove(source);
    }

    private void GarudaExEnterCombat()
    {
        garudaExStep = GarudaExtremeStep.Combat;
        garudaExCombatSince = _now;
        garudaExWheelAt = GarudaExAdds ? _now.AddSeconds(25) : default;
    }

    private void GarudaExAfterCast()
    {
        if (!garudaExPhase2)
        {
            garudaExLosSafe = garudaExLosDanger = null;
            garudaExSongAt = null;
            if (++garudaExSongNumber <= 2) GarudaExEnterCombat();
            else garudaExStep = GarudaExtremeStep.Terminal;
        }
        else if (garudaExSubphase % 2 == 1)
            garudaExStep = GarudaExtremeStep.AfterShriekDisappear;
        else if (garudaExSubphase == 6)
            garudaExStep = GarudaExtremeStep.Terminal;
        else
        {
            ++garudaExSubphase;
            GarudaExEnterCombat();
        }
    }

    private bool GarudaExCastAllowed(uint action) => action switch
    {
        1382 => GarudaExCombat,
        1549 => GarudaExAdds,
        1550 => true,
        1554 or 1384 or 1555 => garudaExPhase2,
        1777 => GarudaExWhirlwind,
        1671 => GarudaExEyeEnabled,
        1761 => !garudaExPhase2 && garudaExStep is GarudaExtremeStep.Reappear or GarudaExtremeStep.CastStart or GarudaExtremeStep.CastEnd,
        _ => false,
    };

    private void GarudaExSnapshotLos(Vector3 origin)
    {
        var blockers = _actors.Values.Where(a => a.Oid == 0xF3 && !a.Dead)
            .Select(a => a with { HitboxRadius = Math.Max(0, a.HitboxRadius - .5f) }).ToArray();
        garudaExLosSafe = new(new(origin, blockers, true, 31.7f));
        garudaExLosDanger = new(new(origin, blockers, false, 31.7f));
    }

    private void GarudaExCast(DungeonPendingCast cast)
    {
        if (!IsGarudaExtreme) return;
        if (cast.Source == PrimaryId && garudaExStep == GarudaExtremeStep.CastStart)
            garudaExStep = GarudaExtremeStep.CastEnd;
        if (cast.Rule.Action == 1671 && _actors.TryGetValue(cast.Source, out var eye))
        {
            garudaExEye = eye;
            garudaExEyeAt = cast.FinishAt;
        }
        if (cast.Rule.Action == 1761)
        {
            GarudaExSnapshotLos(_actors[cast.Source].Position);
            garudaExSongAt = cast.FinishAt;
        }
    }

    private void GarudaExFinish(DungeonPendingCast cast, DateTime at)
    {
        if (!IsGarudaExtreme) return;
        if (cast.Rule.Action == 1671) garudaExEyeAt = at.AddSeconds(4.2);
        if (cast.Rule.Action == 1761)
        {
            garudaExLosSafe = garudaExLosDanger = null;
            garudaExSongAt = null;
        }
        if (cast.Source == PrimaryId && garudaExStep == GarudaExtremeStep.CastEnd)
            GarudaExAfterCast();
    }

    private void GarudaExEffect(uint action)
    {
        if (!IsGarudaExtreme) return;
        if (action == 1554) ++garudaExAerialBlasts;
        if (action == 1552 && GarudaExAdds)
            garudaExWheelAt = _actors.Values.Any(a => a.Oid == 0x621 && a.Targetable && !a.Dead)
                ? _now.AddSeconds(25) : default;
    }

    private void ResetGarudaEx()
    {
        garudaExInitialized = garudaExPhase2 = false;
        garudaExSongNumber = 0;
        garudaExAerialBlasts = 0;
        garudaExSubphase = -1;
        garudaExStep = default;
        garudaExWheelAt = default;
        garudaExEye = null;
        garudaExLosSafe = garudaExLosDanger = null;
        garudaExSongAt = null;
    }
}
