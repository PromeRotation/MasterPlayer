using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureHexapod => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.HEXAPOD;
    private readonly TreasurePlayerStatusState treasureHexStatuses = new([2161, 2162, 2163, 2164, 1257, 3629, 3802, 4551, 4552]);
    private readonly HuntMarchState treasureHexMarch = new(2161, 2162, 2163, 2164, duration: 3);
    private readonly Dictionary<int, DateTime> treasureHexBombs = [];
    private readonly HashSet<int> treasureHexPositive = [];
    private readonly HashSet<int> treasureHexNegative = [];
    private readonly Dictionary<int, TreasurePoleShift> treasureHexPoles = [];
    private readonly List<ulong> treasureHexShockOrder = [];
    private readonly List<(long Order, ulong Source, Vector3 Position)> treasureHexMines = [];
    private readonly HashSet<ulong> treasureHexMineSeen = [];
    private readonly HashSet<ulong> treasureHexMineDestroyed = [];
    private long treasureHexMineOrder;
    internal IReadOnlyDictionary<int, TreasurePoleShift> TreasureHexPoles => treasureHexPoles;
    internal IReadOnlyDictionary<int, DateTime> TreasureHexBombs => treasureHexBombs;
    internal IReadOnlyList<(long Order, ulong Source, Vector3 Position)> TreasureHexMines => treasureHexMines;

    private void TreasureHexStatusChanged(ulong actor, uint status, ReplayStatus? value)
    {
        if (HuntMarchState.Matches(status))
        {
            treasureHexMarch.ApplyObservedStatus(actor, status, value);
            return;
        }
        var slot = Array.IndexOf(treasureParty, actor);
        if (slot < 0) return;
        if (status == 3802)
        {
            if (value == null) treasureHexBombs.Remove(slot);
            else treasureHexBombs[slot] = value.ExpiresAt;
        }
        else
        {
            var charges = status == 4551 ? treasureHexPositive : treasureHexNegative;
            if (value == null) charges.Remove(slot);
            else charges.Add(slot);
        }
    }

    private void TreasureHexStatus(ulong actor, uint status, bool present, DateTime at, ushort param, DateTime? expiry, ulong source)
    {
        if (!IsTreasureHexapod) return;
        treasureParty = [.. TreasurePartyInput];
        treasureHexStatuses.Changed = TreasureHexStatusChanged;
        treasureHexStatuses.Status(actor, status, present, at, param, expiry, source);
    }

    private void TreasureHexObserve()
    {
        if (!IsTreasureHexapod) return;
        treasureParty = [.. TreasurePartyInput];
        treasureHexStatuses.Changed = TreasureHexStatusChanged;
        treasureHexStatuses.ObserveMissing(_actors.ToDictionary(p => p.Key, p => TreasureStatusActor(p.Value)), _now);
        foreach (var id in treasureHexMineSeen.Where(id => !_actors.ContainsKey(id)).ToArray()) treasureHexMineSeen.Remove(id);
        treasureHexMineDestroyed.RemoveWhere(id => !_actors.ContainsKey(id));
    }

    private void TreasureHexObserveActor(DungeonActor actor)
    {
        if (!IsTreasureHexapod) return;
        treasureHexStatuses.ObserveActor(TreasureStatusActor(actor), _now);
        // StayMove clears the viewed dead player's requirement even when text output is disabled.
        if (actor.Id == PlayerId && actor.Dead)
            treasureHexBombs.Remove(Array.IndexOf(treasureParty, actor.Id));
    }

    private void TreasureHexMineCreated(ulong id, uint oid, Vector3 position, bool createEvent = false)
    {
        if (!IsTreasureHexapod || oid != 0x48B4) return;
        if (createEvent) treasureHexMineDestroyed.Remove(id);
        if (treasureHexMineDestroyed.Contains(id) || !treasureHexMineSeen.Add(id)) return;
        treasureHexMines.Add((++treasureHexMineOrder, id, QuantizedDungeonPosition(position)));
    }

    private void TreasureHexRemoved(ulong id, DateTime at)
    {
        if (!IsTreasureHexapod) return;
        treasureHexStatuses.Remove(id, at);
        treasureHexMarch.Remove(id);
        if (treasureHexMineSeen.Remove(id)) treasureHexMineDestroyed.Add(id);
        // Mine AOEs intentionally outlive the actor; only Electromine consumes the first source match.
    }

    private void TreasureHexStart(DungeonPendingCast cast, bool correction)
    {
        if (!IsTreasureHexapod) return;
        if (cast.Rule.Action == 43755 && !correction && !treasureHexShockOrder.Contains(cast.Source))
            treasureHexShockOrder.Add(cast.Source);
        if (cast.Rule.Action is not (43752 or 43754)) return;
        foreach (var (id, slot) in treasureParty.Select((id, slot) => (id, slot)))
        {
            if (_actors.GetValueOrDefault(id) is not { Dead: false }) continue;
            if (correction)
            {
                if (treasureHexPoles.TryGetValue(slot, out var old) && old.Cast.Source == cast.Source
                    && old.Cast.StartedAt == cast.StartedAt)
                    treasureHexPoles[slot] = old with { Cast = cast };
                continue;
            }
            var pull = (cast.Rule.Action == 43754 ? treasureHexPositive : treasureHexNegative).Contains(slot);
            var push = (cast.Rule.Action == 43754 ? treasureHexNegative : treasureHexPositive).Contains(slot);
            if (pull || push) treasureHexPoles[slot] = new(cast, pull);
        }
    }

    private void TreasureHexFinish(DungeonPendingCast cast)
    {
        if (!IsTreasureHexapod) return;
        if (cast.Rule.Action == 43755) treasureHexShockOrder.Remove(cast.Source);
        if (cast.Rule.Action is 43752 or 43754) treasureHexPoles.Clear();
    }

    private Vector3 TreasureHexPoleLanding(DungeonActor player, TreasurePoleShift pole)
    {
        var direction = player.Position - pole.Origin;
        direction.Y = 0;
        var distance = direction.Length();
        if (distance == 0) return player.Position;
        direction /= distance;
        if (pole.Pull) direction = -direction;
        var length = pole.Pull ? MathF.Min(pole.Distance, distance - pole.MinDistance) : pole.Distance;
        return length > 0 ? TreasureWallLanding(player.Position, direction, length, player.HitboxRadius) : player.Position;
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> TreasureHexMarchMovements
    {
        get
        {
            if (!IsTreasureHexapod || _actors.GetValueOrDefault(PlayerId) is not { } player) return [];
            return treasureHexMarch.Movements(TreasureStatusActor(player), _now, (from, to) =>
            {
                var delta = to - from;
                var distance = delta.Length();
                return distance > 0 ? TreasureWallLanding(from, delta / distance, distance, .5f) : from;
            });
        }
    }

    internal IEnumerable<(float Center, float HalfWidth, DateTime Activation)> TreasureHexDirections(Vector3 position)
    {
        if (!IsTreasureHexapod || !treasureParty.Contains(PlayerId)
            || treasureHexMarch.FirstPending(PlayerId) is not { } first) yield break;
        var move = new Vector2(MathF.Sin(first.Rotation), MathF.Cos(first.Rotation));
        var left = new Vector2(move.Y, -move.X);
        foreach (var mine in treasureHexMines)
        {
            var delta = new Vector2(mine.Position.X - position.X, mine.Position.Z - position.Z);
            var distance = delta.Length();
            if (distance <= 5 || distance >= 24) continue;
            yield return (MathF.Atan2(Vector2.Dot(delta, left), Vector2.Dot(delta, move)), MathF.Asin(5 / distance), first.Activation);
        }
    }

    private IEnumerable<DungeonAmbientAoe> TreasureHexAoes()
    {
        if (!IsTreasureHexapod) yield break;
        foreach (var mine in treasureHexMines)
            yield return new($"treasure/hex/mine/{mine.Order}", "Electromine", new CircleTemplate(5),
                mine.Position, Obstacle: true);
        if (!_actors.TryGetValue(PlayerId, out var player)) yield break;
        var index = 0;
        foreach (var movement in TreasureHexMarchMovements)
            foreach (var aoe in QuestKnockbackProjection($"treasure/hex/march/{index++}", player, movement.From, movement.To))
                yield return aoe with { Name = "Brainjack / Forced march", Rotation = aoe.Id.EndsWith("/player") ? movement.Rotation : aoe.Rotation };
        var slot = Array.IndexOf(treasureParty, PlayerId);
        if (!treasureHexPoles.TryGetValue(slot, out var pole) || Arena == null) yield break;
        foreach (var aoe in QuestKnockbackProjection("treasure/hex/pole", player, player.Position, TreasureHexPoleLanding(player, pole)))
            yield return aoe;
        GeometryTemplate shape = pole.Pull ? new CircleTemplate(18) : new TreasurePolePushTemplate(Arena.Position, pole.Origin);
        yield return new("treasure/hex/pole/avoid", "Pole Shift", shape, pole.Origin,
            Display: false, Activation: pole.Activation);
    }

    private IEnumerable<string> TreasureHexHints()
    {
        if (!IsTreasureHexapod || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        var slot = Array.IndexOf(treasureParty, PlayerId);
        if (treasureHexBombs.TryGetValue(slot, out var activation))
        {
            if (activation <= _now.AddSeconds(2.5)) yield return "Stop everything!";
            if (player.Dead) treasureHexBombs.Remove(slot);
            else yield return $"SpecialMode: Pyretic activation={activation:O}";
        }
        if (treasureHexMines.Any(m => new CircleTemplate(5).Resolve(m.Position, 0, null).Contains(player.Position)))
            yield return "GTFO from aoe!";
        var last = TreasureHexMarchMovements.LastOrDefault();
        var delta = last.To - player.Position;
        delta.Y = 0;
        if (last.From != last.To && delta.LengthSquared() > 0)
        {
            var direction = Vector3.Normalize(delta);
            if (treasureHexMines.Any(m =>
            {
                var offset = player.Position - m.Position;
                offset.Y = 0;
                var along = Vector3.Dot(offset, direction);
                var discriminant = along * along - offset.LengthSquared() + 25;
                return discriminant >= 0 && -along + MathF.Sqrt(discriminant) >= 0 && -along - MathF.Sqrt(discriminant) <= 18;
            })) yield return "Aim for safe spot!";
        }
        if (treasureHexPoles.TryGetValue(slot, out var pole))
        {
            var landing = TreasureHexPoleLanding(player, pole);
            var casts = _casts.Values.Where(c => c.Rule.Action == (pole.Pull ? 43755u : 43756u));
            if (pole.Pull) casts = treasureHexShockOrder.Take(1).Where(_casts.ContainsKey).Select(id => _casts[id]);
            if (casts.Any(c => c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(landing)))
                yield return "Knockback into danger!";
        }
    }

    private void ResetTreasureHex()
    {
        treasureHexStatuses.Clear();
        treasureHexMarch.Clear();
        treasureHexBombs.Clear();
        treasureHexPositive.Clear();
        treasureHexNegative.Clear();
        treasureHexPoles.Clear();
        treasureHexShockOrder.Clear();
        treasureHexMines.Clear();
        treasureHexMineSeen.Clear();
        treasureHexMineDestroyed.Clear();
        treasureHexMineOrder = 0;
    }
}
