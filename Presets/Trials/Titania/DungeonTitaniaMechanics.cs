using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTitania => territory == TitaniaCatalog.TERRITORY;
    private int titaniaKey;
    private bool titaniaFire;
    private DateTime titaniaFireAt;
    private readonly Dictionary<ulong, DungeonActor> titaniaActors = [];
    private readonly HashSet<ulong> titaniaSeen = [];
    private readonly HashSet<ulong> titaniaDestroyed = [];
    private readonly HashSet<ulong> titaniaWaitingRemoval = [];
    private readonly Dictionary<ulong, uint> titaniaAnimations = [];
    private readonly List<(Vector3 Position, int Key)> titaniaTowers = [];
    private readonly List<(DungeonActor Actor, float Length, float? Rotation, int Key, bool Destroyed)> titaniaRoots = [];
    private readonly List<TitaniaBait> titaniaBaits = [];
    private readonly List<(DungeonPendingCast? Cast, DungeonActor Target, DateTime At, int Key, bool Destroyed)> titaniaMarks = [];
    private readonly List<DungeonPendingCast> titaniaKnockbacks = [];

    private void TitaniaObserve()
    {
        if (!IsTitania || Active == null) return;
        foreach (var id in titaniaActors.Keys.Where(id => !_actors.ContainsKey(id) && !titaniaDestroyed.Contains(id)).ToArray())
            TitaniaRemoved(id);
        titaniaWaitingRemoval.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values)
        {
            if (titaniaWaitingRemoval.Contains(actor.Id)) continue;
            titaniaActors[actor.Id] = actor;
            if (!titaniaSeen.Add(actor.Id)) continue;
            titaniaDestroyed.Remove(actor.Id);
            if (actor.Oid == 0x1EABE5) titaniaTowers.Add((actor.Position, ++titaniaKey));
            if (actor.Oid == 0x1E9E3C) titaniaTowers.Clear();
        }
        for (var i = 0; i < titaniaRoots.Count; ++i)
        {
            var root = titaniaRoots[i];
            if (!root.Destroyed && _actors.TryGetValue(root.Actor.Id, out var actor))
                titaniaRoots[i] = root with { Actor = actor };
        }
        if (_actors.Values.Where(a => a.Oid == 0x1EABE4 && !titaniaWaitingRemoval.Contains(a.Id)).All(a => a.Dead))
            titaniaRoots.Clear();
        for (var i = 0; i < titaniaBaits.Count; ++i)
        {
            var bait = titaniaBaits[i];
            titaniaBaits[i] = bait with
            {
                Source = !bait.SourceDestroyed && _actors.TryGetValue(bait.Source.Id, out var source) ? source : bait.Source,
                Target = !bait.TargetDestroyed && _actors.TryGetValue(bait.Target.Id, out var target) ? target : bait.Target,
            };
        }
        for (var i = 0; i < titaniaMarks.Count; ++i)
        {
            var mark = titaniaMarks[i];
            if (!mark.Destroyed && _actors.TryGetValue(mark.Target.Id, out var actor))
                titaniaMarks[i] = mark with { Target = actor };
        }
        titaniaBaits.RemoveAll(b => b.Action == 15707 && b.Target.Dead);
    }

    private void TitaniaAnimation(ulong source, uint state, bool eventInput)
    {
        if (!IsTitania || Active == null || titaniaWaitingRemoval.Contains(source)
            || !_actors.TryGetValue(source, out var actor) || actor.Oid != 0x1EABE4) return;
        if (!eventInput && titaniaAnimations.TryGetValue(source, out var previous) && previous == state) return;
        titaniaAnimations[source] = state;
        var length = state switch { 0x00010002 => 10, 0x00100020 => 40, _ => 0 };
        if (length == 0) return;
        titaniaRoots.Add((actor, length, null, ++titaniaKey, false));
        titaniaRoots.Add((actor, length, MathF.PI / 4, ++titaniaKey, false));
    }

    private void TitaniaCast(DungeonPendingCast cast)
    {
        if (!IsTitania) return;
        if (cast.Rule.Action == 15687)
        {
            titaniaFire = true;
            // This fixed delay belongs to the callback, not the historical spell start.
            titaniaFireAt = _now.AddSeconds((double)2.7f);
        }
        if (cast.Rule.Action == 15705)
        {
            // The source feed finishes the previous cast before starting a corrected packet.
            titaniaKnockbacks.RemoveAll(c => c.Source == cast.Source);
            titaniaKnockbacks.Add(cast);
        }
        if (cast.Rule.Action == 15789)
        {
            var index = titaniaBaits.FindIndex(b => b.Action == 15789 && b.Source.Id == cast.Source);
            if (!_actors.TryGetValue(cast.Source, out var source) || !_actors.TryGetValue(cast.TargetId, out var target))
            {
                if (index >= 0) titaniaBaits.RemoveAt(index);
            }
            else
            {
                var bait = new TitaniaBait(source, target, 15789, cast.FinishAt, index >= 0 ? titaniaBaits[index].Key : ++titaniaKey);
                if (index < 0) titaniaBaits.Add(bait);
                else titaniaBaits[index] = bait;
            }
        }
        if (cast.Rule.Action is not (16927 or 15668 or 15703)) return;
        var markIndex = titaniaMarks.FindIndex(m => m.Cast?.Source == cast.Source);
        if (!_actors.TryGetValue(cast.TargetId, out var marked))
        {
            if (markIndex >= 0) titaniaMarks.RemoveAt(markIndex);
            return;
        }
        var mark = (cast, marked, cast.FinishAt, markIndex >= 0 ? titaniaMarks[markIndex].Key : ++titaniaKey, false);
        if (markIndex < 0) titaniaMarks.Add(mark);
        else titaniaMarks[markIndex] = mark;
    }

    private void TitaniaFinish(DungeonPendingCast cast)
    {
        if (!IsTitania) return;
        if (cast.Rule.Action == 15687) titaniaFire = false;
        if (cast.Rule.Action == 15685) titaniaTowers.Clear();
        if (cast.Rule.Action == 15705) titaniaKnockbacks.RemoveAll(c => c.Source == cast.Source);
        if (cast.Rule.Action == 15789) titaniaBaits.RemoveAll(b => b.Action == 15789 && b.Source.Id == cast.Source);
        if (cast.Rule.Action is not (16927 or 15668 or 15703)) return;
        var index = titaniaMarks.FindIndex(m => m.Cast?.Rule.Action == cast.Rule.Action && m.Target.Id == cast.TargetId);
        if (index >= 0) titaniaMarks.RemoveAt(index);
    }

    private void TitaniaIcon(ulong actor, uint icon, DateTime at)
    {
        if (!IsTitania || Active == null || !_actors.TryGetValue(actor, out var target)) return;
        if (icon == 62) titaniaMarks.Add((null, target, at.AddSeconds(3), ++titaniaKey, false));
        if (icon == 230 && _actors.TryGetValue(PrimaryId, out var source))
            titaniaBaits.Add(new(source, target, 15707, at.AddSeconds(5.1), ++titaniaKey));
    }

    private void TitaniaEffect(uint action, ulong target)
    {
        if (!IsTitania) return;
        if (action == 15707)
        {
            var index = titaniaBaits.FindIndex(b => b.Action == action);
            if (index >= 0) titaniaBaits.RemoveAt(index);
        }
        if (action != 17267) return;
        var stack = titaniaMarks.FindIndex(m => m.Cast == null && m.Target.Id == target);
        if (stack < 0) stack = titaniaMarks.FindIndex(m => m.Cast == null);
        if (stack >= 0) titaniaMarks.RemoveAt(stack);
    }

    private void TitaniaRemoved(ulong source)
    {
        if (!IsTitania || !titaniaDestroyed.Add(source)) return;
        if (_actors.ContainsKey(source)) titaniaWaitingRemoval.Add(source);
        if (_casts.TryGetValue(source, out var cast)) TitaniaFinish(cast);
        for (var i = 0; i < titaniaRoots.Count; ++i)
            if (titaniaRoots[i].Actor.Id == source) titaniaRoots[i] = titaniaRoots[i] with { Destroyed = true };
        for (var i = 0; i < titaniaBaits.Count; ++i)
            titaniaBaits[i] = titaniaBaits[i] with
            {
                SourceDestroyed = titaniaBaits[i].SourceDestroyed || titaniaBaits[i].Source.Id == source,
                TargetDestroyed = titaniaBaits[i].TargetDestroyed || titaniaBaits[i].Target.Id == source,
            };
        for (var i = 0; i < titaniaMarks.Count; ++i)
            if (titaniaMarks[i].Target.Id == source) titaniaMarks[i] = titaniaMarks[i] with { Destroyed = true };
        titaniaSeen.Remove(source); titaniaAnimations.Remove(source);
    }

    private void ResetTitania()
    {
        titaniaKey = 0; titaniaFire = false; titaniaFireAt = default;
        titaniaActors.Clear(); titaniaSeen.Clear(); titaniaDestroyed.Clear(); titaniaWaitingRemoval.Clear();
        titaniaAnimations.Clear(); titaniaTowers.Clear(); titaniaRoots.Clear(); titaniaBaits.Clear();
        titaniaMarks.Clear(); titaniaKnockbacks.Clear();
    }
}
