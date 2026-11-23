using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsWarriorOfLight => territory == WarriorOfLightCatalog.TERRITORY;
    private int wolKey;
    private readonly Dictionary<ulong, DungeonActor> wolActors = [];
    private readonly HashSet<ulong> wolDestroyed = [];
    private readonly HashSet<ulong> wolWaitingRemoval = [];
    private readonly List<DungeonAmbientAoe> wolBrimstone = [];
    private readonly List<DungeonAmbientAoe> wolSwords = [];
    private readonly List<WarriorOfLightBait> wolBaits = [];
    private readonly List<WarriorOfLightMark> wolMarks = [];
    private readonly List<(Vector3 Position, float Radius, int Max, DateTime At, ulong Source, int Key)> wolTowers = [];
    private readonly List<DungeonPendingCast> wolKnockbacks = [];
    private readonly List<DungeonPendingCast> wolDecimation = [];

    private void WolObserve()
    {
        if (!IsWarriorOfLight || Active == null) return;
        foreach (var id in wolActors.Keys.Where(id => !_actors.ContainsKey(id) && !wolDestroyed.Contains(id)).ToArray())
            WolRemoved(id);
        wolWaitingRemoval.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values.Where(a => !wolWaitingRemoval.Contains(a.Id)))
        {
            wolActors[actor.Id] = actor;
            wolDestroyed.Remove(actor.Id);
        }
        for (var i = 0; i < wolBaits.Count; ++i)
        {
            var b = wolBaits[i];
            wolBaits[i] = b with
            {
                Source = !b.SourceDestroyed && _actors.TryGetValue(b.Source.Id, out var s) ? s : b.Source,
                Target = !b.TargetDestroyed && _actors.TryGetValue(b.Target.Id, out var t) ? t : b.Target,
            };
        }
        wolBaits.RemoveAll(b => b.Action is 20256 or 21076 && b.Target.Dead
            || b.Action == 20264 && (b.Source.Dead || b.SourceDestroyed || b.Target.Dead));
        for (var i = 0; i < wolMarks.Count; ++i)
            if (!wolMarks[i].Destroyed && _actors.TryGetValue(wolMarks[i].Target.Id, out var target))
                wolMarks[i] = wolMarks[i] with { Target = target };
        WolObserveStatuses();
        WolObserveTethers();
    }

    private void WolCast(DungeonPendingCast cast)
    {
        if (!IsWarriorOfLight || Active == null) return;
        var action = cast.Rule.Action;
        if (action == 20243) wolImbuedBlizzard = true;
        if (action is 20299 or 20300 && wolImbuedBlizzard)
        {
            var at = _now.AddSeconds(5.5f);
            for (var i = 0; i < wolStayMove.Length; ++i) wolStayMove[i] = (true, at, at.AddSeconds(2));
        }
        if (action == 20254)
        {
            for (var i = 0; i < 16; ++i)
                wolBrimstone.Add(new($"wol/brimstone/{++wolKey}", "地火逐段增长",
                    i == 0 ? new WarriorOfLightZeroCircleTemplate() : new CircleTemplate(i),
                    QuantizedDungeonPosition(cast.Location), cast.Rotation, Activation: _now.AddSeconds(5f + i)));
            wolBrimstone.Sort((a, b) => Nullable.Compare(a.Activation, b.Activation));
        }
        if (action == 20258) wolTowers.Add((cast.Location, 4, 5, cast.FinishAt, cast.Source, ++wolKey));
        if (action == 20252) wolKnockbacks.Add(cast);
        if (action == 20259) wolDecimation.Add(cast);
        if (action is 20251 or 20253 && _actors.TryGetValue(cast.TargetId, out var marked))
            wolMarks.Add(new(marked, action, cast.FinishAt, ++wolKey));
        if (action == 20264 && _actors.TryGetValue(cast.Source, out var source) && _actors.TryGetValue(cast.TargetId, out var target))
            wolBaits.Add(new(source, target, action, cast.FinishAt, ++wolKey));
    }

    private void WolFinish(DungeonPendingCast cast)
    {
        if (!IsWarriorOfLight) return;
        var action = cast.Rule.Action;
        if (action is 20254 or 20255) wolBrimstone.RemoveAll(a => a.Activation <= _now);
        if (action == 20258)
        {
            var i = wolTowers.FindIndex(t => t.Source == cast.Source);
            if (i >= 0) wolTowers.RemoveAt(i);
        }
        if (action == 20252)
        {
            var i = wolKnockbacks.FindIndex(c => c.Source == cast.Source);
            if (i >= 0) wolKnockbacks.RemoveAt(i);
        }
        if (action == 20259)
        {
            var i = wolDecimation.FindIndex(c => c.Source == cast.Source);
            if (i >= 0) wolDecimation.RemoveAt(i);
        }
        if (action is 20251 or 20253)
        {
            var i = wolMarks.FindIndex(m => m.Action == action && m.Target.Id == cast.TargetId);
            if (i >= 0) wolMarks.RemoveAt(i);
        }
    }

    private void WolIcon(ulong actorId, ulong targetId, uint icon, DateTime at)
    {
        if (!IsWarriorOfLight || Active == null || !_actors.TryGetValue(actorId, out var actor)) return;
        var target = _actors.GetValueOrDefault(targetId) ?? actor;
        if (icon == 225 && actor.PartySlot >= 0 && actor.PartySlot < wolStayMove.Length)
            wolStayMove[actor.PartySlot] = (true, at.AddSeconds(4), at.AddSeconds(6));
        if (icon == 161) wolMarks.Add(new(actor, 20237, at.AddSeconds(2), ++wolKey));
        if (icon == 87 && targetId == PlayerId)
        {
            var i = wolMarks.FindIndex(m => m.Action == 20237);
            if (i >= 0) wolMarks[i] = wolMarks[i] with { ForbiddenPlayer = PlayerId };
        }
        if (icon == 87 && wolActors.TryGetValue(PrimaryId, out var primary))
            wolBaits.Add(new(primary, target, 20256, at.AddSeconds(5.1), ++wolKey, wolDestroyed.Contains(primary.Id)));
        if (icon == 234 && Arena != null)
            wolBaits.Add(new(new(0, 0, Arena.Position), target, 21076, at.AddSeconds(6), ++wolKey));
    }

    private void WolConsumeBait(uint action)
    {
        var i = wolBaits.FindIndex(b => b.Action == action);
        if (i >= 0) wolBaits.RemoveAt(i);
    }

    private void WolEffect(ulong source, uint action, ulong target, DateTime at)
    {
        if (!IsWarriorOfLight || Active == null) return;
        if (action == 20255) wolBrimstone.RemoveAll(a => a.Activation <= _now);
        if (action == 20262) wolSwords.RemoveAll(a => a.Activation <= _now);
        if (action == 20256) WolConsumeBait(20256);
        if (action is 21076 or 20246 or 20247) WolConsumeBait(21076);
        if (action == 20264)
        {
            var i = wolBaits.FindIndex(b => b.Action == action && b.Source.Id == source);
            if (i >= 0) wolBaits.RemoveAt(i);
        }
        if (action == 20294 && _actors.TryGetValue(source, out var caster) && _actors.TryGetValue(target, out var marked))
            wolBaits.Add(new(caster, marked, 20248, at.AddSeconds(6), ++wolKey));
        if (action is 20248 or 20249)
        {
            var lines = wolBaits.Where(b => b.Action == 20248).ToArray();
            if (lines.Length == 1) wolBaits.Remove(lines[0]);
            // 源按点名对象当前选中的 TargetID 消费，不是点名对象自己的 ID。
            else wolBaits.RemoveAll(b => b.Action == 20248 && b.Target.TargetId == target);
        }
        if (action == 20237)
        {
            var i = wolMarks.FindIndex(m => m.Action == action && m.Target.Id == target);
            if (i < 0) i = wolMarks.FindIndex(m => m.Action == action);
            if (i >= 0) wolMarks.RemoveAt(i);
        }
        if (action == 20253) wolMarks.RemoveAll(m => m.Action == 20237);
        if (action == 20260 && !wolBaits.Any(b => b.Action == 20260)) wolTetherAt = default;
    }

    private void WolMap(uint index, uint state, DateTime at)
    {
        if (!IsWarriorOfLight || Active == null || Arena == null) return;
        Vector3? pos = index switch
        {
            <= 7 => new(100 + 10.5f * MathF.Sin(index * MathF.PI / 4), Arena.Position.Y, 100 - 10.5f * MathF.Cos(index * MathF.PI / 4)),
            8 => new(104, Arena.Position.Y, 100),
            9 => new(96, Arena.Position.Y, 100),
            _ => null,
        };
        if (pos is { } p)
        {
            p = QuantizedDungeonPosition(p);
            if (state == 0x00020001) wolTowers.Add((p, 3, 2, at.AddSeconds(10f), 0, ++wolKey));
            if (state == 0x00080004) wolTowers.RemoveAll(t => t.Position.X == p.X && t.Position.Z == p.Z);
        }
        if (state != 0x00020001) return;
        var sword = index switch
        {
            0x14 => (new Vector3(100, Arena.Position.Y, 80), 0f),
            0x15 => (new Vector3(120, Arena.Position.Y, 100), MathF.PI * 1.5f),
            0x16 => (new Vector3(100, Arena.Position.Y, 120), MathF.PI),
            0x17 => (new Vector3(80, Arena.Position.Y, 100), MathF.PI / 2),
            _ => default,
        };
        if (sword.Item1 != default)
            wolSwords.Add(new($"wol/sword/{++wolKey}", "光剑三角范围", new FanTemplate(50, MathF.PI * 53 / 180),
                QuantizedDungeonPosition(sword.Item1), sword.Item2, Activation: at.AddSeconds(2.7f)));
    }

    private void WolRemoved(ulong source)
    {
        if (!IsWarriorOfLight || !wolDestroyed.Add(source)) return;
        if (_actors.ContainsKey(source)) wolWaitingRemoval.Add(source);
        if (_casts.TryGetValue(source, out var cast)) WolFinish(cast);
        if (wolActors.GetValueOrDefault(source)?.Oid == 0x1EAFFE) wolBrimstone.Clear();
        foreach (var status in new[] { 960u, 2377u })
        {
            if (wolStatusRaw.GetValueOrDefault((source, status)) || wolStatusPending.GetValueOrDefault((source, status)))
                WolStatus(source, status, false, _now, null, snapshot: true);
            wolStatusRaw.Remove((source, status));
            wolStatusPending.Remove((source, status));
        }
        if (wolTethers.Remove(source, out var old)) WolTetherChange(source, old, false);
        wolTetherPending.Remove(source);
        for (var i = 0; i < wolBaits.Count; ++i)
            wolBaits[i] = wolBaits[i] with
            {
                SourceDestroyed = wolBaits[i].SourceDestroyed || wolBaits[i].Source.Id == source,
                TargetDestroyed = wolBaits[i].TargetDestroyed || wolBaits[i].Target.Id == source,
            };
        wolBaits.RemoveAll(b => b.Action == 20264 && b.SourceDestroyed);
        for (var i = 0; i < wolMarks.Count; ++i)
            if (wolMarks[i].Target.Id == source) wolMarks[i] = wolMarks[i] with { Destroyed = true };
    }

    private void ResetWol()
    {
        wolKey = 0; wolActors.Clear(); wolDestroyed.Clear(); wolWaitingRemoval.Clear(); wolBaits.Clear();
        wolMarks.Clear(); wolBrimstone.Clear(); wolSwords.Clear(); wolTowers.Clear(); wolKnockbacks.Clear(); wolDecimation.Clear();
        wolImbuedBlizzard = false; Array.Clear(wolStayMove); wolStatusRaw.Clear(); wolStatusPending.Clear();
        wolMoveAnchor = null; wolMovePlayer = 0; wolTethers.Clear(); wolTetherPending.Clear(); wolTetherAt = default;
    }
}
