using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly DateTime[] valigFreeze = new DateTime[64];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Param, DateTime Expires, ulong Source)> valigStatusRaw = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Param, DateTime Expires, ulong Source)> valigStatusPending = [];
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> valigStatusPendingAt = [];
    private Vector3? valigMoveAnchor;
    private ulong valigMovePlayer;

    private DateTime ValigFreezeAt => IsValigarmanda && Active != null
        && _actors.GetValueOrDefault(PlayerId) is { Dead: false, PartySlot: >= 0 and < 64 } player
        ? valigFreeze[player.PartySlot] : default;
    internal bool RequireValigMovement => ValigFreezeAt != default && _now >= ValigFreezeAt;
    internal bool WantsValigKnockbackImmunity => IsValigarmanda && _actors.GetValueOrDefault(PlayerId) is { Dead: false, PartyRole: "Tank" } player
        && valigKnockbacks.FirstOrDefault() is { } cast && (cast.FinishAt - _now).TotalSeconds < 5
        && !WhorlImmune(player, cast.FinishAt) && valigTowers.FirstOrDefault() is { } tower
        && new CircleGeometry(tower.Location, 6).Contains(player.Position);

    private void ValigStatus(ulong actorId, uint status, bool present, ushort param = 0, DateTime expires = default, ulong source = 0, DateTime? at = null, bool snapshot = false)
    {
        if (!IsValigarmanda || Active == null || status is not (3523 or 4150 or 3974)) return;
        if (source == 0xE0000000) source = 0;
        if (!snapshot)
        {
            var key = (actorId, status);
            var old = valigStatusPending.TryGetValue(key, out var pending) ? pending : valigStatusRaw.GetValueOrDefault(key);
            // ActorState emits Lose before Gain when a status changes source, including packet-first updates.
            if (present && old.Present && old.Source != source)
                ValigStatus(actorId, status, false, old.Param, old.Expires, old.Source, snapshot: true);
            valigStatusPending[(actorId, status)] = (present, param, expires, source);
            valigStatusPendingAt[(actorId, status)] = at ?? _now;
        }
        if (_actors.GetValueOrDefault(actorId) is not { PartySlot: >= 0 and < 64 } actor) return;
        if (status == 3523 && !present || status == 4150 && present) valigFreeze[actor.PartySlot] = default;
    }

    private void ValigObserveStatuses()
    {
        foreach (var actor in _actors.Values)
            foreach (var status in new[] { 3523u, 4150u, 3974u })
            {
                var key = (actor.Id, status);
                var present = HasStatus(actor, status);
                var state = (Present: present, Param: actor.StatusParams?.GetValueOrDefault(status) ?? 0,
                    Expires: actor.StatusExpirations?.GetValueOrDefault(status) ?? default,
                    Source: actor.StatusSources?.GetValueOrDefault(status) ?? 0);
                if (state.Source == 0xE0000000) state.Source = 0;
                var old = valigStatusRaw.GetValueOrDefault(key);
                if (valigStatusPending.TryGetValue(key, out var pending))
                {
                    var confirmed = pending.Present == present && (!present || pending.Param == state.Param && pending.Source == state.Source);
                    if (!confirmed && _now < valigStatusPendingAt[key].AddSeconds(.5)) continue;
                    valigStatusPending.Remove(key); valigStatusPendingAt.Remove(key);
                    if (confirmed) { valigStatusRaw[key] = state; continue; }
                    old = pending;
                }
                valigStatusRaw[key] = state;
                if (old.Present && present && old.Source != state.Source)
                    ValigStatus(actor.Id, status, false, old.Param, old.Expires, old.Source, snapshot: true);
                if (old.Present != present || present && (old.Source != state.Source || old.Param != state.Param || (state.Expires - old.Expires).TotalSeconds > 1))
                    ValigStatus(actor.Id, status, present, state.Param, state.Expires, state.Source, snapshot: true);
            }
        foreach (var actor in _actors.Values.Where(a => a.Dead && a.PartySlot is >= 0 and < 64)) valigFreeze[actor.PartySlot] = default;
    }

    private void ValigTickMovement()
    {
        if (!RequireValigMovement) { valigMoveAnchor = null; return; }
        var player = _actors[PlayerId];
        if (valigMoveAnchor == null || valigMovePlayer != PlayerId || Vector3.DistanceSquared(player.Position, valigMoveAnchor.Value) > 1.21f)
        {
            valigMovePlayer = PlayerId; valigMoveAnchor = player.Position;
        }
    }

    private IEnumerable<DungeonAmbientAoe> ValigMovementAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (ValigFreezeAt is var activation && activation != default)
            yield return new("valig/freezing/center", "冻结期间留在中央", new SohrKhaiWallHintTemplate(Arena,
                [new RectangleGeometry(Arena.Position, 0, 20, 20)]), Arena.Position, Display: false, Activation: activation);
        if (RequireValigMovement && valigMoveAnchor is { } anchor)
            yield return new("valig/freezing/move", "冻结粉尘持续移动", new CircleTemplate(.8f), anchor, Display: false);
        if (valigKnockbacks.FirstOrDefault() is { } first && !WhorlImmune(player, first.FinishAt) && player.PartyRole != "Tank")
        {
            var delta = new Vector3(MathF.Sin(first.Rotation), 0, MathF.Cos(first.Rotation)) * 21;
            yield return new("valig/ruinfall/join", "非坦克击退前站位", new SohrKhaiWallHintTemplate(Arena,
                [new RectangleGeometry(Arena.Position - delta, 0, 38, 28)]), Arena.Position, Display: false, Activation: first.FinishAt);
        }
        var start = player.Position;
        foreach (var cast in valigKnockbacks)
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var delta = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 21;
            var fraction = 1f;
            if (delta.X > 0) fraction = Math.Min(fraction, (120 - start.X) / delta.X);
            if (delta.X < 0) fraction = Math.Min(fraction, (80 - start.X) / delta.X);
            if (delta.Z > 0) fraction = Math.Min(fraction, (115 - start.Z) / delta.Z);
            if (delta.Z < 0) fraction = Math.Min(fraction, (85 - start.Z) / delta.Z);
            var landing = start + delta * Math.Clamp(fraction + .001f / 21, 0, 1);
            foreach (var aoe in DungeonMovementPreview($"valig/ruinfall/{cast.Source}", "坠落击退轨迹", start, landing)) yield return aoe;
            start = landing;
        }
    }
}
