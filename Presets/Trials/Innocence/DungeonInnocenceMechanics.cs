using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsInnocence => territory == InnocenceCatalog.TERRITORY;
    private bool InnocencePhaseAlive => IsInnocence && _actors.Values.Any(a => a.Oid == 0x28FE && !a.Dead
        && !innocenceDestroyed.Contains(a.Id));
    private int innocenceKey;
    private readonly Dictionary<ulong, DungeonActor> innocenceActors = [];
    private readonly HashSet<ulong> innocenceSeen = [];
    private readonly HashSet<ulong> innocenceDestroyed = [];
    private readonly HashSet<ulong> innocenceWaitingRemoval = [];
    private readonly List<(Vector3 Position, int Key)> innocenceTowers = [];
    private readonly List<DungeonAmbientAoe> innocenceWaves = [];
    private readonly List<InnocenceBait> innocenceBaits = [];

    private void InnocenceObserve()
    {
        if (!IsInnocence || Active == null) return;
        foreach (var id in innocenceActors.Keys.Where(id => !_actors.ContainsKey(id) && !innocenceDestroyed.Contains(id)).ToArray())
            InnocenceRemoved(id);
        innocenceWaitingRemoval.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values)
        {
            if (innocenceWaitingRemoval.Contains(actor.Id)) continue;
            innocenceActors[actor.Id] = actor;
            if (!innocenceSeen.Add(actor.Id)) continue;
            innocenceDestroyed.Remove(actor.Id);
            if (actor.Oid == 0x1EAD40) innocenceTowers.Add((actor.Position, ++innocenceKey));
        }
        for (var i = 0; i < innocenceBaits.Count; ++i)
        {
            var bait = innocenceBaits[i];
            innocenceBaits[i] = bait with
            {
                Source = !bait.SourceDestroyed && _actors.TryGetValue(bait.Source.Id, out var source) ? source : bait.Source,
                Target = !bait.TargetDestroyed && _actors.TryGetValue(bait.Target.Id, out var target) ? target : bait.Target,
            };
        }
        innocenceBaits.RemoveAll(b => b.Action == 16069 && b.Target.Dead);
    }

    private void InnocenceCast(DungeonPendingCast cast)
    {
        if (!IsInnocence || Active == null || Arena == null) return;
        if (cast.Rule.Action == 16035)
        {
            innocenceBaits.RemoveAll(b => b.Action == 16035 && b.Source.Id == cast.Source);
            if (_actors.TryGetValue(cast.Source, out var source) && _actors.TryGetValue(cast.TargetId, out var target))
                innocenceBaits.Add(new(source, target, 16035, cast.FinishAt, ++innocenceKey));
        }
        if (cast.Rule.Action is not (16049 or 16050)) return;
        for (var i = 0; i < 26; ++i)
        {
            var angle = 8f * i * MathF.PI / 180;
            var (sin, cos) = Math.SinCos(-8f * i * (Math.PI / 180));
            var delta = cast.Location - Arena.Position;
            var position = new Vector3(Arena.Position.X + delta.X * (float)cos - delta.Z * (float)sin,
                cast.Location.Y, Arena.Position.Z + delta.X * (float)sin + delta.Z * (float)cos);
            innocenceWaves.Add(new($"innocence/wave/{++innocenceKey}", "身魂流转连续扇环",
                new PtDonutSectorTemplate(15, 20, MathF.PI * 2 / 9), QuantizedDungeonPosition(position),
                cast.Rotation + angle, Activation: _now.AddSeconds((double)(2.7f + .6f * i))));
        }
        innocenceWaves.Sort((a, b) => Nullable.Compare(a.Activation, b.Activation));
    }

    private void InnocenceResolveWaves() => innocenceWaves.RemoveAll(a => a.Activation <= _now);

    private void InnocenceFinish(DungeonPendingCast cast)
    {
        if (!IsInnocence) return;
        if (cast.Rule.Action is 16049 or 16050) InnocenceResolveWaves();
        if (cast.Rule.Action == 16035)
        {
            var index = innocenceBaits.FindIndex(b => b.Action == 16035 && b.Source.Id == cast.Source);
            if (index >= 0) innocenceBaits.RemoveAt(index);
        }
    }

    private void InnocenceIcon(ulong target, uint icon, DateTime at)
    {
        if (IsInnocence && Active != null && icon == 138 && _actors.TryGetValue(target, out var marked)
            && innocenceActors.TryGetValue(PrimaryId, out var primary))
            innocenceBaits.Add(new(primary, marked, 16069, at.AddSeconds(5.1), ++innocenceKey,
                innocenceDestroyed.Contains(primary.Id)));
    }

    private void InnocenceEffect(ulong source, uint action, ulong target, DateTime at)
    {
        if (!IsInnocence || Active == null) return;
        if (action == 16121) InnocenceResolveWaves();
        if (action == 14588 && _actors.TryGetValue(source, out var caster) && _actors.TryGetValue(target, out var marked))
            innocenceBaits.Add(new(caster, marked, 16070, at.AddSeconds(4.7), ++innocenceKey));
        if (action == 16069)
        {
            var index = innocenceBaits.FindIndex(b => b.Action == action);
            if (index >= 0) innocenceBaits.RemoveAt(index);
        }
        if (action != 16070) return;
        var lines = innocenceBaits.Where(b => b.Action == action).ToArray();
        if (lines.Length == 1 && lines[0].Target.Id != target && _actors.TryGetValue(target, out var corrected))
        {
            var index = innocenceBaits.IndexOf(lines[0]);
            innocenceBaits[index] = lines[0] with { Target = corrected, TargetDestroyed = false };
        }
        var resolved = innocenceBaits.FindIndex(b => b.Action == action && b.Target.Id == target);
        if (resolved >= 0) innocenceBaits.RemoveAt(resolved);
    }

    private void InnocenceRemoved(ulong source)
    {
        if (!IsInnocence || !innocenceDestroyed.Add(source)) return;
        if (_actors.ContainsKey(source)) innocenceWaitingRemoval.Add(source);
        if (_casts.TryGetValue(source, out var cast)) InnocenceFinish(cast);
        if (innocenceActors.TryGetValue(source, out var actor))
        {
            if (actor.Oid == 0x1EAD40) innocenceTowers.Clear();
            if (actor.Oid == 0x2900 && Arena != null && actor.Position.X == Arena.Position.X && actor.Position.Z == Arena.Position.Z)
                innocenceWaves.Clear();
        }
        for (var i = 0; i < innocenceBaits.Count; ++i)
            innocenceBaits[i] = innocenceBaits[i] with
            {
                SourceDestroyed = innocenceBaits[i].SourceDestroyed || innocenceBaits[i].Source.Id == source,
                TargetDestroyed = innocenceBaits[i].TargetDestroyed || innocenceBaits[i].Target.Id == source,
            };
        innocenceSeen.Remove(source);
    }

    private void ResetInnocence()
    {
        innocenceKey = 0; innocenceActors.Clear(); innocenceSeen.Clear(); innocenceDestroyed.Clear();
        innocenceWaitingRemoval.Clear(); innocenceTowers.Clear(); innocenceWaves.Clear(); innocenceBaits.Clear();
    }
}
