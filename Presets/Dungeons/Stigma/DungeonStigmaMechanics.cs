using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> stigmaMarches = [];
    private readonly HashSet<(ulong Actor, uint Status)> stigmaUnconfirmedStatuses = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> stigmaCharges = [];

    public bool RequireStigmaMovementHold => territory == StigmaCatalog.TERRITORY && Active?.BossOid == 0x3428
        && _actors.TryGetValue(PlayerId, out var player) && !player.Dead && StigmaForcedEnd(PlayerId) > _now;

    private DateTime StigmaForcedEnd(ulong id) => stigmaMarches.Where(s => s.Key.Actor == id && s.Key.Status is 1257 or 3629)
        .Select(s => s.Value).DefaultIfEmpty().Max();

    private void StigmaCast(DungeonPendingCast cast)
    {
        if (territory != StigmaCatalog.TERRITORY || !StigmaCatalog.IsCharge(cast.Rule.Action)
            || !_actors.TryGetValue(cast.Source, out var caster)) return;
        var delta = cast.Location - caster.Position; delta.Y = 0;
        stigmaCharges[cast.Source] = new($"stigma/charge/{cast.Source}", cast.Rule.Name,
            RayRectangleTemplate.Symmetric(cast.Rule.Action == 25645 ? 5 : 8, delta.Length()), QuantizedDungeonPosition(caster.Position),
            MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt);
    }

    private void StigmaStatus(ulong target, uint status, bool present, DateTime? expiresAt)
    {
        if (territory != StigmaCatalog.TERRITORY || Active?.BossOid != 0x3428 || !StigmaCatalog.IsMarchStatus(status)) return;
        stigmaUnconfirmedStatuses.Add((target, status));
        if (!present) stigmaMarches.Remove((target, status));
        else if (expiresAt is { } end) stigmaMarches[(target, status)] = end;
    }

    private void SynchronizeStigma()
    {
        if (territory != StigmaCatalog.TERRITORY) return;
        foreach (var key in stigmaMarches.Keys.Concat(stigmaUnconfirmedStatuses).Distinct().ToArray())
            if (!_actors.TryGetValue(key.Actor, out var actor) || actor.Dead)
            {
                stigmaMarches.Remove(key);
                stigmaUnconfirmedStatuses.Remove(key);
            }
        if (Active?.BossOid != 0x3428) return;
        foreach (var actor in _actors.Values.Where(a => !a.Dead))
            foreach (var status in new uint[] { 1958, 1959, 1960, 1961, 1257, 3629 })
            {
                var key = (actor.Id, status);
                var present = actor.Statuses?.Contains(status) == true;
                // Events remain authoritative until an object frame acknowledges the transition.
                if (stigmaUnconfirmedStatuses.Contains(key) && statusChanges.TryGetValue(key, out var change))
                {
                    if (present != change.Present) continue;
                    stigmaUnconfirmedStatuses.Remove(key);
                }
                if (!present) stigmaMarches.Remove(key);
                else if (actor.StatusExpirations?.TryGetValue(status, out var expires) == true)
                    stigmaMarches[key] = expires;
            }
    }

    internal Vector3[] StigmaMarchOffsets(DungeonActor player, float heading)
    {
        var offsets = new List<Vector3>();
        var delta = Vector3.Zero;
        var forcedEnd = StigmaForcedEnd(player.Id);
        var direction = heading;
        if (forcedEnd > _now)
        {
            direction = player.Rotation;
            delta += new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)) * (6.6f * (float)(forcedEnd - _now).TotalSeconds);
            offsets.Add(delta);
        }
        foreach (var move in stigmaMarches.Where(s => s.Key.Actor == player.Id && s.Key.Status is >= 1958 and <= 1961)
            .OrderBy(s => s.Value))
        {
            if (move.Value > _now.AddSeconds(8)) break;
            direction += move.Key.Status switch { 1959 => MathF.PI, 1960 => MathF.PI / 2, 1961 => -MathF.PI / 2, _ => 0 };
            delta += new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)) * 13.2f;
            offsets.Add(delta);
        }
        return offsets.ToArray();
    }

    private StigmaMarchTemplate? StigmaMarch(DungeonActor player, float heading)
    {
        if (territory != StigmaCatalog.TERRITORY || Active?.BossOid != 0x3428 || Arena == null || player.Dead) return null;
        var offsets = StigmaMarchOffsets(player, heading);
        return offsets.Length == 0 ? null : new(Arena, offsets, StigmaDangers());
    }

    private IGeometryShape[] StigmaDangers() => OutputCasts.Where(c => c.Value.Rule.Shape != null)
        .Select(c => c.Value.Rule.Shape!.Resolve(c.Value.Location, c.Value.Rotation, null))
        .Concat(stigmaCharges.Values.Select(c => c.Shape.Resolve(c.Position, c.Rotation, null))).ToArray();

    private float? StigmaFacing(Vector3 position, float rotation)
    {
        if (!_actors.TryGetValue(PlayerId, out var player) || RequireStigmaMovementHold
            || !stigmaMarches.Any(s => s.Key.Actor == PlayerId && s.Key.Status is >= 1958 and <= 1961 && s.Value <= _now.AddSeconds(.75)))
            return null;
        for (var step = 0; step <= 180; ++step)
            foreach (var sign in step == 0 ? new[] { 1 } : new[] { 1, -1 })
            {
                var heading = rotation + sign * step * MathF.PI / 180;
                if (StigmaMarch(player, heading) is { } march && !march.Unsafe(position)) return heading;
            }
        return null;
    }
}
