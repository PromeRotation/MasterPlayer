using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, (ulong Player, ulong Enemy, DateTime At)> shinryuTethers = [];
    private readonly Dictionary<ulong, ulong> shinryuTetherPending = [];

    private void ShinryuTether(ulong source, ulong target, uint tether, bool present, DateTime at, bool snapshot = false)
    {
        if (!IsShinryu || tether != 9) return;
        if (!snapshot) shinryuTetherPending[source] = present ? target : 0;
        shinryuTethers.Remove(source);
        if (!present || !_actors.TryGetValue(source, out var from) || !_actors.ContainsKey(target)) return;
        var player = from.PartySlot >= 0 ? source : target;
        var enemy = player == source ? target : source;
        shinryuTethers[source] = (player, enemy, at);
    }

    private void ShinryuObserveTethers()
    {
        foreach (var source in _actors.Keys.Concat(shinryuTethers.Keys).Distinct().ToArray())
        {
            var target = _actors.GetValueOrDefault(source)?.Tethers?.GetValueOrDefault(9u) ?? 0;
            if (shinryuTetherPending.TryGetValue(source, out var pending))
            {
                if (target != pending) continue;
                shinryuTetherPending.Remove(source);
            }
            if (target == 0) shinryuTethers.Remove(source);
            else if (!shinryuTethers.TryGetValue(source, out var previous)
                || previous.Player != target && previous.Enemy != target)
                ShinryuTether(source, target, 9, true, _now, snapshot: true);
        }
    }

    private IEnumerable<(string Id, Vector3 From, Vector3 To)> ShinryuKnockbacks()
    {
        if (!IsShinryu || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new[] { 8075u, 8097u, 8984u, 8080u })
        {
            var position = player.Position;
            var count = 0;
            foreach (var cast in shinryuCastOrder.Where(c => c.Rule.Action == action))
            {
                if (WhorlImmune(player, cast.FinishAt)) continue;
                if (action == 8097 && !cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null).Contains(position)) continue;
                var directional = action is 8075 or 8097;
                var delta = directional ? new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) : position - cast.Location;
                delta.Y = 0;
                if (delta.LengthSquared() == 0) continue;
                var distance = action switch { 8075 => 35f, 8097 => 10, 8984 => 5, _ => 15 };
                var next = position + Vector3.Normalize(delta) * distance;
                yield return ($"{action}/{cast.Source}", position, next);
                position = next;
                if (++count == (action == 8097 ? 3 : int.MaxValue)) break;
            }
        }
    }

    private Vector3? ShinryuSlide(DungeonActor player)
    {
        if (!ShinryuSliding(player)) return null;
        var angle = LegacyMovement ? CameraAzimuth + MathF.PI : player.Rotation;
        return angle is { } rotation ? player.Position + new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * 30 : null;
    }

    private bool ShinryuSliding(DungeonActor player)
        => shinryuSlidePending.TryGetValue(player.Id, out var pending) ? pending : ShinryuRawStatus(player, 911);

    private IEnumerable<DungeonAmbientAoe> ShinryuMovementAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new[] { 8075u, 8984u, 8080u })
            if (shinryuCastOrder.FirstOrDefault(c => c.Rule.Action == action) is { } cast && !WhorlImmune(player, cast.FinishAt))
                yield return new($"shinryu/kb/{action}/origin", "接近击退源", new SohrKhaiWallHintTemplate(Arena,
                    [new CircleGeometry(cast.Location, action == 8075 ? 12 : 7)]), Arena.Position,
                    Display: false, Activation: cast.FinishAt);
        foreach (var (id, from, to) in ShinryuKnockbacks())
            foreach (var aoe in TrialKnockbackDrawing("shinryu/kb/" + id, "神龙击退落点", from, to)) yield return aoe;
        if (ShinryuSlide(player) is { } slide)
        {
            foreach (var aoe in TrialKnockbackDrawing("shinryu/slide", "薄冰三十码滑行", player.Position, slide)) yield return aoe;
        }
        if (ShinryuSliding(player))
            yield return new("shinryu/slide/radius", "薄冰滑行距离", new CircleTemplate(30), player.Position,
                Navigate: false, OutlineOnly: true, Color: new Vector4(1, 0, 1, 1));
        foreach (var (source, tether) in shinryuTethers)
        {
            if (tether.Player != PlayerId || shinryuActors.GetValueOrDefault(tether.Enemy) is not { Dead: false } enemy) continue;
            yield return new($"shinryu/chain/{source}/avoid", "拉开三十码断链", new CircleTemplate(30), enemy.Position,
                Display: false, Activation: tether.At);
            var delta = enemy.Position - player.Position; delta.Y = 0;
            if (delta.LengthSquared() > 0)
                yield return new($"shinryu/chain/{source}/line", "燃烧锁链", new RectangleTemplate(.08f, delta.Length()),
                    (enemy.Position + player.Position) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false,
                    Color: delta.LengthSquared() >= 900 ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1));
        }
    }
}
