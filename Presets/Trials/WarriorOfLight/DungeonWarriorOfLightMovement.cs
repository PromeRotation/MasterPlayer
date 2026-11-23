using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool wolImbuedBlizzard;
    private readonly (bool Move, DateTime At, DateTime Finish)[] wolStayMove = new (bool, DateTime, DateTime)[64];
    private readonly Dictionary<(ulong Actor, uint Status), bool> wolStatusRaw = [];
    private readonly Dictionary<(ulong Actor, uint Status), bool> wolStatusPending = [];
    private Vector3? wolMoveAnchor;
    private ulong wolMovePlayer;

    private (bool Move, DateTime At, DateTime Finish) WolPlayerMode => IsWarriorOfLight && Active != null
        && _actors.GetValueOrDefault(PlayerId) is { Dead: false, PartySlot: >= 0 and < 64 } player
        ? wolStayMove[player.PartySlot] : default;
    internal bool RequireWolStop => WolPlayerMode is { Move: false, At: var at, Finish: var end }
        && at != default && _now >= at && _now <= end;
    internal bool RequireWolMovement => WolPlayerMode is { Move: true, At: var at, Finish: var end }
        && at != default && _now >= at && _now <= end;

    private void WolStatus(ulong actorId, uint status, bool present, DateTime at, DateTime? expires, bool snapshot = false)
    {
        if (!IsWarriorOfLight || Active == null || status is not (960 or 2377)) return;
        if (!snapshot) wolStatusPending[(actorId, status)] = present;
        if (_actors.GetValueOrDefault(actorId) is not { PartySlot: >= 0 and < 64 } actor) return;
        var slot = actor.PartySlot;
        if (status == 960) wolStayMove[slot] = present ? (false, at, expires ?? default) : default;
        if (status == 2377 && !present && wolImbuedBlizzard)
        {
            wolStayMove[slot] = default;
            wolImbuedBlizzard = false;
        }
    }

    private void WolObserveStatuses()
    {
        foreach (var actor in _actors.Values.Where(a => !wolWaitingRemoval.Contains(a.Id)))
            foreach (var status in new[] { 960u, 2377u })
            {
                var key = (actor.Id, status);
                var present = actor.Statuses?.Contains(status) == true;
                if (wolStatusPending.TryGetValue(key, out var pending))
                {
                    if (present != pending) continue;
                    wolStatusPending.Remove(key);
                    wolStatusRaw[key] = present;
                    continue;
                }
                if (wolStatusRaw.GetValueOrDefault(key) == present) continue;
                wolStatusRaw[key] = present;
                WolStatus(actor.Id, status, present, _now, actor.StatusExpirations?.GetValueOrDefault(status), snapshot: true);
            }
    }

    private void WolTick()
    {
        if (!IsWarriorOfLight || Active == null) return;
        if (_actors.GetValueOrDefault(PlayerId) is { PartySlot: >= 0 and < 64 } player)
        {
            var state = wolStayMove[player.PartySlot];
            if (state.Finish != default && state.Finish <= _now) Array.Clear(wolStayMove);
            else if (player.Dead) wolStayMove[player.PartySlot] = default;
        }
        if (!RequireWolMovement) { wolMoveAnchor = null; return; }
        var current = _actors[PlayerId];
        if (wolMoveAnchor == null || wolMovePlayer != PlayerId || Vector3.DistanceSquared(current.Position, wolMoveAnchor.Value) > 1.21f)
        {
            wolMoveAnchor = current.Position;
            wolMovePlayer = PlayerId;
        }
    }

    private IEnumerable<(ulong Source, Vector3 From, Vector3 To)> WolKnockbackPaths()
    {
        if (!IsWarriorOfLight || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var cast in wolKnockbacks)
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var to = from + new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 30;
            yield return (cast.Source, from, to);
            from = to;
        }
    }

    private IEnumerable<DungeonAmbientAoe> WolMovementAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (RequireWolMovement && wolMoveAnchor is { } anchor)
            yield return new("wol/element/move", "冰判定保持移动", new CircleTemplate(.8f), anchor, Display: false);
        if (wolKnockbacks.FirstOrDefault() is { } first && !WhorlImmune(player, first.FinishAt))
            yield return new("wol/suiton/join", "水遁击退前站位", new SohrKhaiWallHintTemplate(Arena,
                [new RectangleGeometry(first.Location, first.Rotation, 40, 16)]), Arena.Position, Display: false);
        foreach (var (source, from, to) in WolKnockbackPaths())
            foreach (var aoe in TrialKnockbackDrawing($"wol/knockback/{source}", "水遁击退落点", from, to))
                yield return aoe;
    }
}
