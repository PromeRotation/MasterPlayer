using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(uint Action, Vector3 Position, DateTime At, int Key)> trialVoidzones = [];
    private int trialVoidzoneKey;

    private uint TrialVoidzoneOid(uint action) => (territory, action) switch
    {
        (EurekaOrthosCatalog.TWINTANIA, 31463) => 0x3D1E,
        (ThornmarchCatalog.TERRITORY, 29207) => 0x1E8F67,
        (WhorleaterCatalog.TERRITORY, 1864) => 0x1E9230,
        (WhorleaterCatalog.TERRITORY, 1865) => 0x1E9231,
        (LabyrinthCatalog.TERRITORY, 1752) when Active?.BossOid == 0x932 => 0x98C,
        (HeavenswardIntroCatalog.NINGISHZIDA, 6427) or (HeavenswardIntroCatalog.ALFARD, 7139) => 0x1E8D9B,
        (HeavenswardIntroCatalog.NINGISHZIDA, 6428) or (HeavenswardIntroCatalog.ALFARD, 7140) => 0x1E8D9C,
        (HeavenswardIntroCatalog.BLACK_RIDER, 7088) => 0x1E858E,
        (HeavenswardIntroCatalog.YAQUARU, 7091) => 0x1E9998,
        (JobQuestCatalog.KILLING_ART, 27593) => 0x1E963D,
        (JobQuestCatalog.POWER_TO_PROTECT, 2175) => 0x1EA4C6,
        (MainScenarioQuestCatalog.RHALGRS_BEACON, 8346) => 0x1EA4D9,
        (MainScenarioQuestCatalog.HIS_FORGOTTEN_HOME, 1087) => 0x1E8D9C,
        (NativeTreasureCatalog.SHIFTING_GYMNASION, 32229) when IsTreasureTriton => 0x3D32,
        (NativeTreasureCatalog.SHIFTING_ALTARS, 13396) when TreasureVoidzoneAction == action => 0x1E8D9C,
        (NativeTreasureCatalog.SHIFTING_ALTARS, 13219) when TreasureVoidzoneAction == action => 0x1EA140,
        (NativeTreasureCatalog.SHIFTING_ALTARS, 13452) when TreasureVoidzoneAction == action => 0x1EA8BB,
        (NativeTreasureCatalog.SHIFTING_OUBLIETTES, 21679) when TreasureVoidzoneAction == action => 0x1E8FC7,
        _ => 0,
    };

    private void TrialVoidzoneEffect(uint action, ulong target, Vector3? location, DateTime at)
    {
        if (Active == null || TrialVoidzoneOid(action) == 0) return;
        if ((_actors.GetValueOrDefault(target)?.Position ?? location) is { } position)
            trialVoidzones.Add((action, position, at.AddSeconds(action switch
                { 29207 => .8, 1752 => .1, 6427 or 6428 or 7139 or 7140 => 2.1, 8346 => .5, 32229 => (double)1.4f,
                    31463 or 13396 => (double).9f, 13219 => (double).7f, 13452 => (double)1.2f, 21679 => 1.4d, 7088 => 1.3d, 7091 => .8d, _ => 0 }), ++trialVoidzoneKey));
    }

    private void TrialVoidzoneObserve()
    {
        if (IsEurekaOrthos && EurekaInputScan) return;
        if (!IsPalaceSerpent && !IsBlackRider && !IsYaquaru && !IsOrcus && !IsIo && !IsRhalgr && !IsSlickshell && territory != EurekaOrthosCatalog.TWINTANIA && TreasureVoidzoneAction == 0 && territory is not (ThornmarchCatalog.TERRITORY or WhorleaterCatalog.TERRITORY or LabyrinthCatalog.TERRITORY)) return;
        // A live source consumes one matching event prediction per update, exactly as the source component.
        foreach (var actor in _actors.Values.Where(a => IsTreasureTriton || a.EventState != 7))
        {
            var index = trialVoidzones.FindIndex(p => TrialVoidzoneOid(p.Action) == actor.Oid
                && Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(actor.Position.X, actor.Position.Z)) <= 4);
            if (index >= 0) trialVoidzones.RemoveAt(index);
        }
    }

    private IEnumerable<DungeonAmbientAoe> TrialVoidzoneAoes()
    {
        if (Active == null || !IsPalaceSerpent && !IsBlackRider && !IsYaquaru && !IsOrcus && !IsIo && !IsRhalgr && !IsSlickshell && territory != EurekaOrthosCatalog.TWINTANIA && TreasureVoidzoneAction == 0 && territory is not (ThornmarchCatalog.TERRITORY or WhorleaterCatalog.TERRITORY or LabyrinthCatalog.TERRITORY)) yield break;
        uint[] actions;
        if (territory == EurekaOrthosCatalog.TWINTANIA) actions = [31463];
        else if (territory == ThornmarchCatalog.TERRITORY) actions = [29207];
        else if (territory == WhorleaterCatalog.TERRITORY) actions = [1864, 1865];
        else if (territory == HeavenswardIntroCatalog.NINGISHZIDA) actions = [6427, 6428];
        else if (territory == HeavenswardIntroCatalog.ALFARD) actions = [7139, 7140];
        else if (IsBlackRider) actions = [7088];
        else if (IsYaquaru) actions = [7091];
        else if (IsOrcus) actions = [27593];
        else if (IsIo) actions = [2175];
        else if (IsRhalgr) actions = [8346];
        else if (IsSlickshell) actions = [1087];
        else if (TreasureVoidzoneAction != 0) actions = [TreasureVoidzoneAction];
        else if (Active.BossOid == 0x932) actions = [1752];
        else yield break;
        foreach (var action in actions)
        {
            var radius = action switch { 29207 or 7091 => 8, 7088 => 9, 13452 => 10, 1752 or 6427 or 6428 or 7139 or 7140 or 27593 or 1087 or 13396 or 13219 or 21679 => 6, _ => 5 };
            var shape = new CircleTemplate(radius);
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == action && (territory != EurekaOrthosCatalog.TWINTANIA || c.Observed)))
            {
                var observed = _actors.GetValueOrDefault(cast.Source)?.Cast;
                // Rhalgr accepts real CastInfo changes through the native observer; unchanged stale scans must not undo packets.
                var current = (IsPalaceSerpent || IsBlackRider || IsYaquaru || IsOrcus || IsIo || IsSlickshell || territory == EurekaOrthosCatalog.TWINTANIA || TreasureVoidzoneAction != 0) && observed?.Action == action ? observed : null;
                yield return new($"trial/voidzone/cast/{cast.Source}", "毒区读条落点", shape,
                    _actors.GetValueOrDefault(current?.TargetId ?? cast.TargetId)?.Position ?? current?.Location ?? cast.Location,
                    Activation: cast.FinishAt);
            }
            foreach (var prediction in trialVoidzones.Where(p => p.Action == action))
                yield return new($"trial/voidzone/event/{prediction.Key}", "毒区生成预测", shape, prediction.Position, Activation: prediction.At);
            foreach (var actor in _actors.Values.Where(a => a.Oid == TrialVoidzoneOid(action) && (IsTreasureTriton || a.EventState != 7)))
                yield return new($"trial/voidzone/live/{actor.Id}", "持续毒区", shape, QuantizedDungeonPosition(actor.Position));
        }
    }
}
