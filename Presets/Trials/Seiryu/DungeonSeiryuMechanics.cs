using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsSeiryu => territory == SeiryuCatalog.TERRITORY;
    private bool seiryuPhase2;
    private int seiryuKey;
    private int seiryuCreationKey;
    private bool seiryuModelsInitialized;
    private readonly List<DungeonPendingCast> seiryuKnockbackCasts = [];
    private readonly List<SeiryuBait> seiryuBaits = [];
    private readonly List<SeiryuBait> seiryuLines = [];
    private readonly List<(DungeonActor Target, DateTime At, int Key, bool Destroyed)> seiryuSpreads = [];
    private readonly Dictionary<ulong, DungeonActor> seiryuActors = [];
    private readonly HashSet<ulong> seiryuDestroyed = [];
    private readonly HashSet<ulong> seiryuWaitingRemoval = [];
    private readonly Dictionary<ulong, (byte Model, byte Anim1, byte Anim2)> seiryuModels = [];
    private readonly HashSet<ulong> seiryuSeen = [];
    private readonly Dictionary<ulong, (DateTime At, int Order)> seiryuBirths = [];
    private readonly List<(ulong Source, Vector3 Position, DateTime At, int Key, int Order)> seiryuTowers = [];
    private DungeonAmbientAoe? seiryuSigil;

    private void SeiryuCreated(ulong source, DateTime at)
    {
        if (!IsSeiryu) return;
        if (!seiryuBirths.TryAdd(source, (at, ++seiryuCreationKey))) return;
        if (!seiryuSeen.Contains(source) || seiryuDestroyed.Contains(source)) return;
        var index = seiryuTowers.FindLastIndex(t => t.Source == source);
        if (index >= 0)
            seiryuTowers[index] = seiryuTowers[index] with { At = at.AddSeconds(7.8), Order = seiryuCreationKey };
        SeiryuSortTowers();
    }

    private void SeiryuSortTowers() => seiryuTowers.Sort((a, b) =>
        a.Order == b.Order ? a.Key.CompareTo(b.Key) : a.Order.CompareTo(b.Order));

    private void SeiryuObserve()
    {
        if (!IsSeiryu || Active == null) return;
        foreach (var source in seiryuActors.Keys.Where(id => !_actors.ContainsKey(id) && !seiryuDestroyed.Contains(id)).ToArray())
            SeiryuRemoved(source);
        seiryuWaitingRemoval.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values)
        {
            if (seiryuWaitingRemoval.Contains(actor.Id)) continue;
            seiryuActors[actor.Id] = actor;
            if (seiryuSeen.Add(actor.Id))
            {
                seiryuDestroyed.Remove(actor.Id);
                if (actor.Oid == 0x1EAA3C)
                {
                    var birth = seiryuBirths.GetValueOrDefault(actor.Id, (_now, int.MaxValue));
                    seiryuTowers.Add((actor.Id, QuantizedDungeonPosition(actor.Position),
                        birth.Item1.AddSeconds(7.8), ++seiryuKey, birth.Item2));
                }
            }
            var model = (actor.ModelState, actor.AnimationState1, actor.AnimationState2);
            if (seiryuModels.TryGetValue(actor.Id, out var previous) ? previous != model
                : seiryuModelsInitialized && model != default)
            {
                GeometryTemplate? shape = actor.ModelState switch { 32 => new CircleTemplate(12), 33 => new DonutTemplate(7, 30), _ => null };
                if (shape != null)
                    seiryuSigil = new("seiryu/sigil", "阴阳与蛇眼之印", shape,
                        QuantizedDungeonPosition(actor.Position), Activation: _now.AddSeconds(5.6));
            }
            seiryuModels[actor.Id] = model;
        }
        seiryuModelsInitialized = true;
        SeiryuSortTowers();
        for (var i = 0; i < seiryuBaits.Count; ++i) seiryuBaits[i] = SeiryuRefresh(seiryuBaits[i]);
        for (var i = 0; i < seiryuLines.Count; ++i) seiryuLines[i] = SeiryuRefresh(seiryuLines[i]);
        for (var i = 0; i < seiryuSpreads.Count; ++i)
        {
            var spread = seiryuSpreads[i];
            if (!spread.Destroyed && _actors.TryGetValue(spread.Target.Id, out var current))
                seiryuSpreads[i] = spread with { Target = current };
        }
        seiryuSpreads.RemoveAll(s => s.Target.Dead);
        SeiryuObserveTethers();
        var first = seiryuLines.FindIndex(l => l.Action == 15388);
        if (first >= 0) seiryuLines[first] = seiryuLines[first] with { Forbidden = seiryuForbidden };
    }

    private SeiryuBait SeiryuRefresh(SeiryuBait bait) => bait with
    {
        Source = !bait.SourceDestroyed && _actors.TryGetValue(bait.Source.Id, out var source) ? source : bait.Source,
        Target = !bait.TargetDestroyed && _actors.TryGetValue(bait.Target.Id, out var target) ? target : bait.Target,
    };

    private void SeiryuCast(DungeonPendingCast cast)
    {
        if (!IsSeiryu) return;
        if (cast.Rule.Action is 14346 or 14349)
        {
            seiryuKnockbackCasts.RemoveAll(c => c.Source == cast.Source);
            seiryuKnockbackCasts.Add(cast);
        }
        if (cast.Rule.Action != 14333) return;
        var index = seiryuBaits.FindIndex(b => b.Action == 14333 && b.Source.Id == cast.Source);
        if (!_actors.TryGetValue(cast.Source, out var source) || !_actors.TryGetValue(cast.TargetId, out var target))
        {
            seiryuBaits.RemoveAll(b => b.Action == 14333 && b.Source.Id == cast.Source);
            return;
        }
        var bait = new SeiryuBait(source, target, cast.FinishAt, ++seiryuKey, 14333);
        if (index < 0) seiryuBaits.Add(bait);
        else seiryuBaits[index] = bait with { Key = seiryuBaits[index].Key };
    }

    private void SeiryuFinish(DungeonPendingCast cast)
    {
        if (!IsSeiryu) return;
        seiryuKnockbackCasts.RemoveAll(c => c.Source == cast.Source && c.Rule.Action == cast.Rule.Action);
        if (cast.Rule.Action == 14281 && !seiryuPhase2 && Arena != null)
        {
            seiryuPhase2 = true;
            Arena = FourLordsArenas.SeiryuPhase2(Arena.Position.Y);
        }
        if (cast.Rule.Action is 14855 or 14856) seiryuSigil = null;
        if (cast.Rule.Action == 14333) seiryuBaits.RemoveAll(b => b.Action == 14333 && b.Source.Id == cast.Source);
    }

    private void SeiryuEffect(ulong source, uint action, ulong target, DateTime at)
    {
        if (!IsSeiryu || Active == null) return;
        if (action is 14319 or 14277 && _actors.TryGetValue(source, out var caster) && _actors.TryGetValue(target, out var marked))
            seiryuLines.Add(new(caster, marked, at.AddSeconds(action == 14319 ? 5.9d : (double)5.2f),
                ++seiryuKey, action == 14319 ? 15388u : 15474u));
        if (action is 15388 or 15474)
        {
            var lines = seiryuLines.Where(l => l.Action == action).ToArray();
            if (lines.Length == 1 && lines[0].Target.Id != target && _actors.TryGetValue(target, out var corrected))
            {
                var index = seiryuLines.IndexOf(lines[0]);
                seiryuLines[index] = lines[0] with { Target = corrected, TargetDestroyed = false };
            }
            var resolved = seiryuLines.FindIndex(l => l.Action == action && l.Target.Id == target);
            if (resolved >= 0) seiryuLines.RemoveAt(resolved);
        }
        if (action == 14340 && seiryuSpreads.Count > 0)
        {
            var index = seiryuSpreads.FindIndex(s => s.Target.Id == target);
            seiryuSpreads.RemoveAt(index >= 0 ? index : 0);
        }
        if (action is 14988 or 14989) seiryuTowers.Clear();
        if (action == 15389 && !seiryuBaits.Any(b => b.Action == 15389)) seiryuRedAt = default;
        if (action == 15392 && !seiryuBaits.Any(b => b.Action == 15392)) seiryuKanaboAt = default;
    }

    private void SeiryuIcon(ulong actor, uint icon, DateTime at)
    {
        if (IsSeiryu && Active != null && icon == 169 && _actors.TryGetValue(actor, out var target))
            seiryuSpreads.Add((target, at.AddSeconds(6), ++seiryuKey, false));
    }

    private void SeiryuRemoved(ulong source)
    {
        if (!IsSeiryu || !seiryuDestroyed.Add(source)) return;
        if (_actors.ContainsKey(source)) seiryuWaitingRemoval.Add(source);
        if (_casts.TryGetValue(source, out var cast)) SeiryuFinish(cast);
        for (var i = 0; i < seiryuBaits.Count; ++i)
            seiryuBaits[i] = seiryuBaits[i] with { SourceDestroyed = seiryuBaits[i].SourceDestroyed || seiryuBaits[i].Source.Id == source,
                TargetDestroyed = seiryuBaits[i].TargetDestroyed || seiryuBaits[i].Target.Id == source };
        for (var i = 0; i < seiryuLines.Count; ++i)
            seiryuLines[i] = seiryuLines[i] with { SourceDestroyed = seiryuLines[i].SourceDestroyed || seiryuLines[i].Source.Id == source,
                TargetDestroyed = seiryuLines[i].TargetDestroyed || seiryuLines[i].Target.Id == source };
        for (var i = 0; i < seiryuSpreads.Count; ++i)
            if (seiryuSpreads[i].Target.Id == source) seiryuSpreads[i] = seiryuSpreads[i] with { Destroyed = true };
        if (seiryuTethers.Remove(source, out var old)) SeiryuTetherChange(source, old, false, _now);
        seiryuTetherPending.Remove(source);
        seiryuSeen.Remove(source); seiryuBirths.Remove(source); seiryuModels.Remove(source);
    }

    private void ResetSeiryu()
    {
        seiryuPhase2 = false; seiryuKey = 0; seiryuSigil = null;
        seiryuModelsInitialized = false; seiryuCreationKey = 0; seiryuKnockbackCasts.Clear();
        seiryuBaits.Clear(); seiryuLines.Clear(); seiryuSpreads.Clear(); seiryuActors.Clear();
        seiryuDestroyed.Clear(); seiryuWaitingRemoval.Clear(); seiryuModels.Clear(); seiryuSeen.Clear();
        seiryuBirths.Clear(); seiryuTowers.Clear(); seiryuTethers.Clear(); seiryuTetherPending.Clear();
        seiryuKanaboAt = default; seiryuRedAt = default; seiryuForbidden = 0;
    }
}
