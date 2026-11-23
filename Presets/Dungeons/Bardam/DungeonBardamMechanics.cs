using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> bardamRush = [];
    private readonly Dictionary<ulong, DateTime> bardamRushClocks = [];
    private DateTime bardamTetherAt;
    private readonly List<(ulong Source, bool Heavy, int Stage, Vector3 Position, float Rotation, DateTime Activation)> bardamSequences = [];
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> bardamSeen = [];
    private readonly HashSet<ulong> bardamCometTargets = [];
    private readonly List<BardamChaser> bardamComets = [];
    private readonly Dictionary<ulong, DateTime> bardamRings = [];
    private readonly Dictionary<ulong, DateTime> bardamSpreads = [];
    private readonly HashSet<ulong> bardamWingbeats = [];
    private DungeonAmbientAoe? bardamSquall;
    private readonly Dictionary<ulong, DungeonAmbientAoe> bardamYamaa = [];

    private void BardamCast(DungeonPendingCast cast)
    {
        if (territory != BardamCatalog.TERRITORY || !bardamSeen.Add((cast.Source, cast.Rule.Action, cast.StartedAt))) return;
        if (cast.Rule.Action == 7932 && _actors.TryGetValue(cast.Source, out var yamaa))
        {
            var delta = cast.Location - yamaa.Position;
            bardamYamaa[cast.Source] = new($"bardam/yamaa/{cast.Source}", "草原羱羊冲锋",
                RayRectangleTemplate.Symmetric(8, new Vector2(delta.X, delta.Z).Length()), yamaa.Position,
                MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt);
        }
        if (cast.Rule.Action is 9591 or 9596)
            bardamSequences.Add((cast.Source, cast.Rule.Action == 9591, 0, cast.Location, cast.Rotation,
                cast.FinishAt.AddSeconds(cast.Rule.Action == 9591 ? .7 : 0)));
        if (cast.Rule.Action is not (9597 or 9598)) return;
        if (cast.Rule.Action == 9597 && bardamCometTargets.Count > 0)
        {
            var target = bardamCometTargets.Where(_actors.ContainsKey).OrderBy(id => Vector3.DistanceSquared(_actors[id].Position, cast.Location)).FirstOrDefault();
            if (target != 0)
            {
                bardamCometTargets.Remove(target);
                bardamComets.Add(new(cast.Source, target, cast.Location, 0, 9, cast.FinishAt));
            }
        }
        // 此试炼覆写了 OnEventCast：包含第一次在内，每次读条开始即推进一次。
        if (bardamComets.Count == 0) return;
        var index = bardamComets.Select((c, i) => (Distance: Vector3.DistanceSquared(BardamCometPosition(c), cast.Location), Index: i)).MinBy(c => c.Distance).Index;
        var comet = bardamComets[index];
        if (comet.Remaining == 1) bardamComets.RemoveAt(index);
        else bardamComets[index] = comet with { Previous = cast.Location, MoveDistance = 10, Remaining = comet.Remaining - 1, Activation = cast.StartedAt.AddSeconds(1.5) };
        if (bardamComets.Count == 0) bardamCometTargets.Clear();
    }

    private Vector3 BardamCometPosition(BardamChaser comet)
    {
        if (!_actors.TryGetValue(comet.Target, out var target)) return comet.Previous;
        var delta = target.Position - comet.Previous; delta.Y = 0;
        return QuantizedDungeonPosition(comet.Previous + delta * Math.Min(1, comet.MoveDistance / Math.Max(.001f, delta.Length())));
    }

    private void BardamFinish(DungeonPendingCast cast, bool completed, DateTime time)
    {
        if (territory != BardamCatalog.TERRITORY) return;
        if (cast.Rule.Action == 7932) bardamYamaa.Remove(cast.Source);
        if (!completed)
        {
            if (cast.Rule.Action is 9591 or 9596) bardamSequences.RemoveAll(s => s.Source == cast.Source);
            if (cast.Rule.Action is >= 9592 and <= 9595)
                bardamSequences.RemoveAll(s => MathF.Abs(s.Position.X - cast.Location.X) <= 1 && MathF.Abs(s.Position.Z - cast.Location.Z) <= 1
                    && s.Heavy == (cast.Rule.Action is >= 9592 and <= 9594));
            if (cast.Rule.Action is 9597 or 9598) bardamComets.RemoveAll(c => c.Source == cast.Source);
            if (cast.Rule.Action == 7929) bardamRush.Clear();
            return;
        }
        var (heavy, order) = cast.Rule.Action switch
        {
            9592 => (true, 0), 9593 => (true, 1), 9594 => (true, 2),
            9596 => (false, 0), 9595 => (false, 1), _ => (false, -1),
        };
        if (order < 0) return;
        var index = bardamSequences.FindIndex(s => s.Heavy == heavy && s.Stage == order
            && MathF.Abs(s.Position.X - cast.Location.X) <= 1 && MathF.Abs(s.Position.Z - cast.Location.Z) <= 1
            && MathF.Abs(MathF.IEEERemainder(s.Rotation - cast.Rotation, MathF.Tau)) <= .05f);
        if (index < 0) return;
        var sequence = bardamSequences[index];
        if (order == (heavy ? 2 : 1)) bardamSequences.RemoveAt(index);
        else bardamSequences[index] = sequence with { Stage = order + 1, Activation = time.AddSeconds(heavy ? 1.3 : 1.5) };
    }

    private void BardamIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x1AA5)
        {
            if (icon == 197) bardamCometTargets.Add(target);
            if (icon == 58) bardamRings[target] = time.AddSeconds(3.5);
        }
        if (Active?.BossOid == 0x1AA6)
        {
            if (icon == 23) bardamSpreads[target] = time.AddSeconds(5.4);
            if (icon == 16) bardamWingbeats.Add(target);
        }
    }

    private void BardamTether(ulong source, ulong target, uint tether, bool present, DateTime time)
    {
        if (Active?.BossOid != 0x1A9E || tether is not (1 or 57)) return;
        var player = _actors.GetValueOrDefault(source)?.PartySlot >= 0 ? source : target;
        bardamTetherAt = time;
        if (!present) { bardamRush.Remove(player); return; }
        if (!bardamRushClocks.TryGetValue(player, out var activation) || activation.AddSeconds(1) <= time)
            bardamRushClocks[player] = activation = time.AddSeconds(10.1);
        bardamRush[player] = activation;
    }

    private void BardamTimeline(ulong source, uint timeline, DateTime time)
    {
        if (Active?.BossOid != 0x1AA6 || source != PrimaryId || timeline != 0x1E43 || !_actors.TryGetValue(source, out var boss)
            || MathF.Abs(boss.Position.X - 24) <= 1 && MathF.Abs(boss.Position.Z + 475.5f) <= 1) return;
        bardamSquall = new("bardam/squall", "疾风冲锋", RayRectangleTemplate.Symmetric(6, 42.8f),
            QuantizedDungeonPosition(boss.Position), boss.Rotation, Activation: time.AddSeconds(6.7));
    }

    private void BardamEffect(uint action, ulong target)
    {
        if (territory != BardamCatalog.TERRITORY) return;
        if (action == 7929) bardamRush.Remove(target);
        if (action == 9601 && !bardamRings.Remove(target)) bardamRings.Clear();
        if (action == 7948) bardamSpreads.Remove(target);
        if (action == 7951) bardamWingbeats.Clear();
        if (action == 7950) bardamSquall = null;
    }

    private void SynchronizeBardam()
    {
        if (territory != BardamCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x1A9E)
        {
            if (_actors.GetValueOrDefault(PrimaryId)?.Cast == null) bardamRush.Clear();
            else if (_now >= bardamTetherAt.AddSeconds(.5))
            {
                var tethers = _actors.Values.Where(a => a.Tethers != null).SelectMany(a => a.Tethers!.Where(t => t.Key is 1 or 57)
                    .Select(t => (Source: a.Id, Target: t.Value))).ToArray();
                var present = tethers.Select(t => _actors.GetValueOrDefault(t.Source)?.PartySlot >= 0 ? t.Source : t.Target).ToHashSet();
                foreach (var id in bardamRush.Keys.Where(id => !present.Contains(id)).ToArray()) bardamRush.Remove(id);
                foreach (var tether in tethers)
                {
                    var id = _actors.GetValueOrDefault(tether.Source)?.PartySlot >= 0 ? tether.Source : tether.Target;
                    if (!bardamRush.ContainsKey(id)) BardamTether(tether.Source, tether.Target, 1, true, _now);
                }
            }
        }
        bardamComets.RemoveAll(c => !_actors.TryGetValue(c.Target, out var target) || target.Dead);
        foreach (var id in bardamRings.Keys.Where(id => !_actors.TryGetValue(id, out var target) || target.Dead).ToArray()) bardamRings.Remove(id);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 9602 && !losSnapshots.ContainsKey(c.Source)))
        {
            var blockers = _actors.Values.Where(a => a.Oid == 0x1F4A && !a.Dead).ToArray();
            if (blockers.Length != 1) continue;
            losSnapshots[cast.Source] = (new(new(cast.Location, blockers, true, 50, true, 0)),
                new(new(cast.Location, blockers, false, 50, true, 0)));
        }
    }

    private void BardamRemoved(ulong id)
    {
        bardamRush.Remove(id); bardamRushClocks.Remove(id); bardamCometTargets.Remove(id);
        bardamYamaa.Remove(id);
        bardamComets.RemoveAll(c => c.Target == id);
        bardamRings.Remove(id); bardamSpreads.Remove(id); bardamWingbeats.Remove(id);
    }

    private void ResetBardam()
    {
        bardamRush.Clear(); bardamRushClocks.Clear(); bardamSequences.Clear(); bardamSeen.Clear();
        bardamCometTargets.Clear(); bardamComets.Clear(); bardamRings.Clear(); bardamSpreads.Clear(); bardamWingbeats.Clear();
        bardamSquall = null; bardamTetherAt = default; bardamYamaa.Clear();
    }
}
