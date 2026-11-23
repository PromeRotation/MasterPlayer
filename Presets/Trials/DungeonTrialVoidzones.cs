using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(uint Action, Vector3 Position, DateTime At, int Key)> trialVoidzones = [];
    private int trialVoidzoneKey;

    private uint TrialVoidzoneOid(uint action) => (territory, action) switch
    {
        (ThornmarchCatalog.TERRITORY, 29207) => 0x1E8F67,
        (WhorleaterCatalog.TERRITORY, 1864) => 0x1E9230,
        (WhorleaterCatalog.TERRITORY, 1865) => 0x1E9231,
        (LabyrinthCatalog.TERRITORY, 1752) when Active?.BossOid == 0x932 => 0x98C,
        _ => 0,
    };

    private void TrialVoidzoneEffect(uint action, ulong target, Vector3? location, DateTime at)
    {
        if (Active == null || TrialVoidzoneOid(action) == 0) return;
        if ((_actors.GetValueOrDefault(target)?.Position ?? location) is { } position)
            trialVoidzones.Add((action, position, at.AddSeconds(action switch { 29207 => .8, 1752 => .1, _ => 0 }), ++trialVoidzoneKey));
    }

    private void TrialVoidzoneObserve()
    {
        if (territory is not (ThornmarchCatalog.TERRITORY or WhorleaterCatalog.TERRITORY or LabyrinthCatalog.TERRITORY)) return;
        // A live source consumes one matching event prediction per update, exactly as the source component.
        foreach (var actor in _actors.Values.Where(a => a.EventState != 7))
        {
            var index = trialVoidzones.FindIndex(p => TrialVoidzoneOid(p.Action) == actor.Oid
                && Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(actor.Position.X, actor.Position.Z)) <= 4);
            if (index >= 0) trialVoidzones.RemoveAt(index);
        }
    }

    private IEnumerable<DungeonAmbientAoe> TrialVoidzoneAoes()
    {
        if (Active == null || territory is not (ThornmarchCatalog.TERRITORY or WhorleaterCatalog.TERRITORY or LabyrinthCatalog.TERRITORY)) yield break;
        uint[] actions;
        if (territory == ThornmarchCatalog.TERRITORY) actions = [29207];
        else if (territory == WhorleaterCatalog.TERRITORY) actions = [1864, 1865];
        else if (Active.BossOid == 0x932) actions = [1752];
        else yield break;
        foreach (var action in actions)
        {
            var radius = action switch { 29207 => 8, 1752 => 6, _ => 5 };
            var shape = new CircleTemplate(radius);
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == action))
                yield return new($"trial/voidzone/cast/{cast.Source}", "毒区读条落点", shape,
                    _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
            foreach (var prediction in trialVoidzones.Where(p => p.Action == action))
                yield return new($"trial/voidzone/event/{prediction.Key}", "毒区生成预测", shape, prediction.Position, Activation: prediction.At);
            foreach (var actor in _actors.Values.Where(a => a.Oid == TrialVoidzoneOid(action) && a.EventState != 7))
                yield return new($"trial/voidzone/live/{actor.Id}", "持续毒区", shape, QuantizedDungeonPosition(actor.Position));
        }
    }
}
