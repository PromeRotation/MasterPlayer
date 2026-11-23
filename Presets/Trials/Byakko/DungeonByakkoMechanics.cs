using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsByakko => territory == ByakkoCatalog.TERRITORY;
    private readonly List<(DungeonPendingCast Cast, DungeonActor Target)> byakkoSpreads = [];

    private void ByakkoCast(DungeonPendingCast cast)
    {
        if (!IsByakko || cast.Rule.Action != 10797 || !_actors.TryGetValue(cast.TargetId, out var target)) return;
        var index = byakkoSpreads.FindIndex(s => s.Cast.Source == cast.Source && s.Cast.StartedAt == cast.StartedAt);
        if (index < 0) byakkoSpreads.Add((cast, target));
        else byakkoSpreads[index] = (cast, target);
    }

    private void ByakkoFinish(DungeonPendingCast cast)
    {
        if (!IsByakko || cast.Rule.Action != 10797) return;
        var index = byakkoSpreads.FindIndex(s => s.Target.Id == cast.TargetId);
        if (index >= 0) byakkoSpreads.RemoveAt(index);
    }

    private void ByakkoObserve()
    {
        if (!IsByakko) return;
        for (var i = 0; i < byakkoSpreads.Count; ++i)
        {
            var spread = byakkoSpreads[i];
            if (_actors.TryGetValue(spread.Target.Id, out var actor)) byakkoSpreads[i] = (spread.Cast, actor);
        }
    }

    private IEnumerable<DungeonAmbientAoe> ByakkoAoes()
    {
        if (!IsByakko || Active == null) yield break;
        foreach (var spread in byakkoSpreads.Where(s => !s.Target.Dead))
        {
            var position = QuantizedDungeonPosition(spread.Target.Position);
            yield return new($"byakko/spread/{spread.Cast.Source}/display", "天雷分散", new CircleTemplate(3),
                position, Navigate: false, OutlineOnly: true);
            if (spread.Target.Id != PlayerId)
                yield return new($"byakko/spread/{spread.Cast.Source}/avoid", "远离天雷目标", new CircleTemplate(4),
                    position, Display: false, Activation: spread.Cast.FinishAt);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                    && !byakkoSpreads.Any(s => s.Target.Id == a.Id)))
                    yield return new($"byakko/spread/{spread.Cast.Source}/buddy/{buddy.Id}", "天雷远离队友", new CircleTemplate(4),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: spread.Cast.FinishAt);
        }
        foreach (var orb in _actors.Values.Where(a => a.Oid == 0x20F9 && !a.Dead))
        {
            yield return new($"byakko/orb/{orb.Id}/display", "荒魂移动范围", new DungeonCapsuleTemplate(2, 2),
                orb.Position, orb.Rotation, Navigate: false);
            yield return new($"byakko/orb/{orb.Id}/contact", "荒魂碰撞", new CircleTemplate(2), orb.Position,
                Display: false, Obstacle: true);
            foreach (var (length, delay) in new[] { (1f, 1.1), (2f, 3d), (4f, 10d), (6f, double.PositiveInfinity) })
                yield return new($"byakko/orb/{orb.Id}/{length}", "荒魂移动预测", new DungeonCapsuleTemplate(2, length),
                    orb.Position, orb.Rotation, Display: false,
                    Activation: double.IsPositiveInfinity(delay) ? DateTime.MaxValue : _now.AddSeconds(delay));
        }
    }

    private IEnumerable<string> ByakkoHints()
    {
        if (!IsByakko || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (ByakkoAoes().Any(a => a.Id.StartsWith("byakko/orb/") && a.Display
            && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position))) yield return "远离荒魂移动范围";
        foreach (var spread in byakkoSpreads.Where(s => !s.Target.Dead))
        {
            var position = QuantizedDungeonPosition(spread.Target.Position);
            var overlap = spread.Target.Id == PlayerId
                ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && new CircleTemplate(3).Resolve(position, 0, null).Contains(a.Position))
                : new CircleTemplate(3).Resolve(position, 0, null).Contains(player.Position);
            if (overlap) yield return "天雷分散";
        }
    }
}
