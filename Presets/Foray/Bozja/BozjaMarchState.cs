using System.Numerics;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaMarchState
{
    internal readonly List<(float Direction, float Duration, DateTime Activation)> Pending = [];
    internal DateTime ForcedEnd;

    internal void Apply(int kind, bool gain, DateTime expires)
    {
        if (kind == 4)
        {
            ForcedEnd = gain ? expires : default;
            return;
        }
        var direction = kind * (90f * (MathF.PI / 180f));
        if (gain)
        {
            Pending.Add((direction, 3f, expires));
            Pending.Sort(static (a, b) => a.Activation.CompareTo(b.Activation));
        }
        else
        {
            var index = Pending.FindIndex(m => m.Direction == direction);
            if (index >= 0) Pending.RemoveAt(index);
        }
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Direction)> Movements(Vector3 position, float rotation,
        DateTime now, Func<Vector3, Vector3, float>? wallDistance = null)
    {
        var from = position;
        var direction = rotation;
        if (ForcedEnd > now)
        {
            var to = Destination(6.6f * (float)(ForcedEnd - now).TotalSeconds);
            yield return (from, to, direction);
            from = to;
        }
        foreach (var move in Pending)
        {
            direction += move.Direction;
            var to = Destination(6.6f * move.Duration);
            yield return (from, to, direction);
            from = to;
        }
        Vector3 Destination(float distance)
        {
            var (sin, cos) = Math.SinCos(direction);
            var forward = new Vector3((float)sin, 0, (float)cos);
            if (wallDistance != null)
                distance = MathF.Min(distance, wallDistance(from, forward) - (.5f - .499f));
            return from + distance * forward;
        }
    }
}
