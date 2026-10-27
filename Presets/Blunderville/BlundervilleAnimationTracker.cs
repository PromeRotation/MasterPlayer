using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Hammers have no cast. Helpers turn before each sweep.
// Timings and helper positions are measured independently of the navigation path.
internal sealed class BlundervilleAnimationTracker
{
    private sealed record Device(BlundervilleActor Actor, int Column, int Columns, uint Action, double Period,
        DateTime Phase, int Serial, IReadOnlyList<BlundervilleHazard> Forecast);
    private readonly Dictionary<ulong, Device> _devices = [];
    private readonly Dictionary<int, DateTime> _starts = [];

    public void Map(byte index, uint state, DateTime now)
    {
        if (index is not (0 or 12 or 41)) return;
        if (state is 0x20001 or 0x80004) _starts[index] = now;
        else
        {
            _starts.Remove(index);
            foreach (var id in _devices.Where(p => Group(p.Value.Actor, p.Value.Action) == index).Select(p => p.Key).ToArray())
                _devices.Remove(id);
        }
    }

    public void Observe(BlundervilleActor actor, DateTime now)
    {
        if (!Identify(actor, out var column, out var columns, out var action)) { Remove(actor.Id); return; }
        if (_devices.TryGetValue(actor.Id, out var old)
            && Vector3.DistanceSquared(old.Actor.Position, actor.Position) < .04f && old.Action == action)
        {
            if (MathF.Cos(actor.Rotation - old.Actor.Rotation) > -.95f)
            {
                if (old.Phase == default && _starts.TryGetValue(Group(actor, action), out var seed)
                    && now >= seed && now - seed < TimeSpan.FromSeconds(.3))
                {
                    var seeded = old with { Phase = seed.AddSeconds(-.05) };
                    _devices[actor.Id] = seeded with { Forecast = Forecast(seeded) };
                }
                return;
            }
            var next = old with { Actor = actor, Phase = now.AddSeconds(-.05), Serial = old.Serial + 1 };
            _devices[actor.Id] = next with { Forecast = Forecast(next) };
            return;
        }
        var phase = _starts.TryGetValue(Group(actor, action), out var start) && now >= start && now - start < TimeSpan.FromSeconds(.3)
            ? start.AddSeconds(-.05) : default;
        var device = new Device(actor, column, columns, action, action == 34775 ? 2.13 : action == 34716 ? 2.1 : 4.15, phase, 0, []);
        _devices[actor.Id] = phase == default ? device : device with { Forecast = Forecast(device) };
    }

    public void Effect(ulong source, uint action, Vector3 position, float rotation, DateTime now)
    {
        // Loading halfway through a sweep has no preceding turn sample. Its real
        // effect supplies the clock; subsequent turns replace that estimate.
        if (!_devices.TryGetValue(source, out var device) || device.Action != action
            || device.Phase != default && now - device.Phase < TimeSpan.FromSeconds(device.Period * 1.5)
            || Vector3.DistanceSquared(position, device.Actor.Position) > .04f || !float.IsFinite(rotation)) return;
        device = device with { Actor = device.Actor with { Rotation = rotation } };
        device = device with { Phase = now.AddSeconds(-Delay(device, rotation)) };
        _devices[source] = device with { Forecast = Forecast(device) };
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now, bool spinRows = true)
    {
        foreach (var device in _devices.Values.Where(d => spinRows || d.Action != 34775))
            foreach (var hazard in device.Forecast)
                if (now <= hazard.Until) yield return hazard;
    }

    public void Remove(ulong source) => _devices.Remove(source);
    public void Reset() { _devices.Clear(); _starts.Clear(); }

    private static IReadOnlyList<BlundervilleHazard> Forecast(Device device)
    {
        var result = new BlundervilleHazard[3];
        for (var i = 0; i < result.Length; ++i)
        {
            var rotation = i % 2 == 0 ? device.Actor.Rotation : -device.Actor.Rotation;
            var phase = device.Phase.AddSeconds(i * device.Period);
            var impact = phase.AddSeconds(Delay(device, rotation));
            var geometry = new RectangleGeometry(device.Actor.Position, rotation,
                device.Action == 34775 ? 3 : device.Action == 34716 ? 2 : 10, device.Action == 34718 ? 4 : 3);
            result[i] = new($"animation/{device.Actor.Id}/{device.Serial + i}", geometry,
                impact.AddSeconds(device.Action == 34775 ? -.55 : -.15),
                impact.AddSeconds(device.Action == 34775 ? .6 : .3), device.Phase);
        }
        return result;
    }

    private static double Delay(Device device, float rotation)
    {
        var rank = rotation > 0 ? device.Column : device.Columns - 1 - device.Column;
        if (device.Action == 34775) return .88 + rank * .2;
        return device.Action == 34716 ? rank switch { 0 => .53, 1 => .935, _ => 1.245 } : .83 + rank * .407;
    }

    private static int Group(BlundervilleActor actor, uint action) => action == 34775 ? -1 : actor.Position.Z > 0 ? 41 : action == 34716 ? 0 : 12;

    private static bool Identify(BlundervilleActor actor, out int column, out int columns, out uint action)
    {
        column = 0; columns = 3; action = 34716;
        if (actor.Oid != 16338 || actor.Dead || !BlundervilleEncounter.Finite(actor.Position)
            || !float.IsFinite(actor.Rotation) || MathF.Abs(MathF.Abs(actor.Rotation) - MathF.PI / 2) > .03f) return false;
        var p = actor.Position;
        float left;
        if (MathF.Abs(p.X) < 8 && Near(p.Y, 10)
            && (Near(p.Z, -281) || Near(p.Z, -288) || Near(p.Z, -301) || Near(p.Z, -308) || Near(p.Z, -321) || Near(p.Z, -328)))
        { left = Near(p.Z, -281) || Near(p.Z, -301) || Near(p.Z, -321) ? .5f : -6.5f; action = 34775; }
        else if (Near(p.Z, -130.3f) || Near(p.Z, -138.8f) || Near(p.Z, -147.3f)) left = p.X < -200 ? -212 : -194;
        else if (Near(p.Z, -219.9f) || Near(p.Z, -232)) left = -203;
        else if (Near(p.Z, -225.3f) || Near(p.Z, -239.05f)) left = p.X < -200 ? -209 : -197;
        else if (Near(p.Z, -261) || Near(p.Z, -281)) { left = -206; columns = 4; action = 34718; }
        else if (Near(p.Z, 226.9f) && Near(p.Y, 6)) left = -203;
        else return false;
        var spacing = action == 34718 ? 4 : 3;
        column = (int)MathF.Round((p.X - left) / spacing);
        return column >= 0 && column < columns && Near(p.X, left + column * spacing)
            && (p.Z > 0 ? Near(p.Y, 6) : p.Y is > 8 and < 19);
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < .05f;
}
