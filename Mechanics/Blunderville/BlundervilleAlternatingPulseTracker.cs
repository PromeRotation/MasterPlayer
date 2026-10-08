using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Final's endpoint helpers alternate two positions before a circular impact.
// Learn both endpoints and two effect intervals before predicting another cycle.
internal sealed class BlundervilleAlternatingPulseTracker
{
    private sealed record Device(BlundervilleActor Actor, uint Action, Vector3? Other, DateTime LastEffect,
        double Period, int Samples, int Serial, bool Moved, IReadOnlyList<BlundervilleHazard> Forecast);
    private readonly Dictionary<ulong, Device> _devices = [];
    private readonly Dictionary<ulong, BlundervilleActor> _actors = [];

    public void Observe(BlundervilleActor actor, DateTime now)
    {
        if (actor.Oid != 16338 || actor.Dead || !BlundervilleEncounter.Finite(actor.Position)
            || MathF.Abs(actor.Position.X) > 15 || actor.Position.Z is < 134 or > 165 || actor.Position.Y is < 30 or > 38)
        { Remove(actor.Id); return; }
        _actors[actor.Id] = actor;
        if (!_devices.TryGetValue(actor.Id, out var device) || Near(device.Actor.Position, actor.Position)) return;
        if (device.Other is { } other && !Near(other, actor.Position))
        { _devices.Remove(actor.Id); return; }
        device = device with { Other = device.Actor.Position, Actor = actor, Serial = device.Serial + 1, Moved = true };
        _devices[actor.Id] = device with { Forecast = Forecast(device, now.AddSeconds(.35), now) };
    }

    public void Effect(ulong source, uint action, Vector3 position, DateTime now)
    {
        if (action is not (29795 or 34796) || !_actors.TryGetValue(source, out var actor) || !Near(actor.Position, position)) return;
        if (!_devices.TryGetValue(source, out var device) || device.Action != action)
        { _devices[source] = new(actor, action, null, now, 0, 0, 0, false, []); return; }
        var interval = (now - device.LastEffect).TotalSeconds;
        var valid = device.Moved && interval is >= 2.3 and <= 2.8 && (device.Samples == 0 || Math.Abs(interval - device.Period) < .2);
        device = device with { LastEffect = now, Period = valid ? interval : 0,
            Samples = valid ? device.Samples + 1 : 0, Serial = device.Serial + 1, Moved = false };
        _devices[source] = device with { Forecast = Forecast(device, now, now) };
    }

    private static IReadOnlyList<BlundervilleHazard> Forecast(Device device, DateTime impact, DateTime now)
    {
        if (device.Samples < 2 || device.Other is not { } other) return [];
        return Enumerable.Range(0, 3).Select(i => new BlundervilleHazard($"pulse/{device.Actor.Id}/{device.Serial}/{i}",
            new CircleGeometry(i % 2 == 0 ? device.Actor.Position : other, device.Action == 29795 ? 3 : 5),
            impact.AddSeconds(i * device.Period - .15), impact.AddSeconds(i * device.Period + .3), now)).ToArray();
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
        => _devices.Values.SelectMany(d => d.Forecast).Where(h => now <= h.Until);
    public void Map(byte index, uint state)
    {
        if (index is >= 44 and <= 49 && state is not (0x20001 or 0x80004)) Reset();
    }
    public void Remove(ulong id) { _devices.Remove(id); _actors.Remove(id); }
    public void Reset() { _devices.Clear(); _actors.Clear(); }
    private static bool Near(Vector3 a, Vector3 b) => Vector3.DistanceSquared(a, b) < .04f;
}
