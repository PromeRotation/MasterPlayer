using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// Q1's twelve crystals retain the direction at Created, including a zero direction.
// Cast/effect history is never reconstructed from the current crystal orientation.
internal sealed class QuantumScourgingBlaze
{
    internal readonly List<(Vector3 Direction, Vector3 Position)> Crystals = [];
    internal readonly List<QuantumBlazeLine> Lines = [];
    private DungeonAmbientAoe[] cachedAoes = [];
    private Vector3 next;
    private int version;
    private int lastVersion = -1;
    private int lastCount = -1;
    private long generation;
    internal int Direction { get; private set; }
    internal int NumCasts { get; private set; }
    internal bool Show { get; private set; }
    internal bool HasUnknownDirection => Lines.Any(line => line.Advance == Vector3.Zero);
    internal IReadOnlyList<(Vector3 Direction, Vector3 Position)>? Refuge { get; private set; }

    internal void Created(DungeonActor actor)
    {
        if (actor.Oid != 0x1EBE70) return;
        Crystals.Add((next, actor.Position));
        if (Crystals.Count == 12) Refuge = Crystals.ToArray();
    }

    internal void Start(DungeonActor caster, DungeonCastObservation cast)
    {
        if (cast.Action != 44118) return;
        foreach (var crystal in Crystals)
        {
            if (!AlmostEqual(crystal.Position, caster.Position, 1)) continue;
            var offset = crystal.Position - QuantumEncounter.CENTER;
            Add(crystal.Direction);
            Add(-crystal.Direction);
            if (Lines.Count == 24) Crystals.Clear();
            return;

            void Add(Vector3 direction)
            {
                var distance = RayAabb(offset, direction, 18.1f, 14.1f);
                // The source's zero direction gives float.MaxValue and an unchecked
                // float-to-int conversion; do not replace it with a guessed direction.
                var count = unchecked((int)distance) / 4 + 1;
                Lines.Add(new(++generation, crystal.Position, direction * 4,
                    cast.FinishAt, count, MathF.Atan2(direction.X, direction.Z)));
            }
        }
    }

    internal void Effect(DungeonActor caster, uint action, Vector3 target, DateTime now)
    {
        switch (action)
        {
            case 44797 or 44799:
                next = Vector3.UnitX;
                ++Direction;
                break;
            case 44798 or 44800:
                next = Vector3.UnitZ;
                ++Direction;
                break;
            case 44118:
                ++NumCasts;
                for (var i = Lines.Count - 1; i >= 0; --i)
                    if (AlmostEqual(Lines[i].Next, caster.Position, .1f)
                        && (now - Lines[i].Activation).TotalSeconds < 1d)
                        Advance(i, caster.Position, now);
                break;
            case 44119:
                for (var i = 0; i < Lines.Count; ++i)
                    if (AlmostEqual(Lines[i].Next, target, .1f)
                        && MathF.Abs(Normalize(caster.Rotation - Lines[i].Rotation)) <= MathF.PI / 180f)
                    {
                        Advance(i, target, now);
                        return;
                    }
                break;
        }
    }

    internal void ShowAoes()
    {
        Show = true;
        Refuge = null;
    }

    internal void Update(DateTime now)
    {
        if (Lines.Count == lastCount && version == lastVersion) return;
        List<DungeonAmbientAoe> future = [];
        List<DungeonAmbientAoe> imminent = [];
        foreach (var line in Lines)
        {
            // A cold component cannot reconstruct the preceding direction
            // effects. Preserve that raw zero-direction line, but do not
            // materialize its RayAABB(float.MaxValue) number of predictions.
            if (line.Advance == Vector3.Zero) continue;
            var point = line.Next;
            var at = line.Activation > now ? line.Activation : now;
            for (var i = 1; i < Math.Min(line.Remaining, line.MaxShown); ++i)
            {
                point += line.Advance;
                at = at.AddSeconds(1d);
                future.Add(Aoe(line, i, point, at, false));
            }
            if (line.Remaining != 0)
                imminent.Add(Aoe(line, 0, line.Next, line.Activation, true));
        }
        cachedAoes = [.. future, .. imminent];
        lastVersion = version;
        lastCount = Lines.Count;
    }

    internal IEnumerable<DungeonAmbientAoe> Aoes() => Show ? cachedAoes : [];

    private static DungeonAmbientAoe Aoe(QuantumBlazeLine line, int index, Vector3 position, DateTime at, bool danger)
        => new($"quantum/blaze/{line.Generation}/{index}", "Scourging Blaze", new CircleTemplate(5),
            DungeonEncounter.QuantizedDungeonPosition(position), line.Rotation, Activation: at,
            Color: danger ? new Vector4(1, 0, 0, 1) : null);

    private void Advance(int index, Vector3 position, DateTime now)
    {
        var line = Lines[index];
        line.Next = position + line.Advance;
        line.Activation = now.AddSeconds(1d);
        --line.Remaining;
        ++version;
        if (line.Remaining == 0) Lines.RemoveAt(index);
    }

    internal static bool AlmostEqual(Vector3 a, Vector3 b, float tolerance)
        => MathF.Abs(a.X - b.X) <= tolerance && MathF.Abs(a.Z - b.Z) <= tolerance;

    internal static float Normalize(float angle)
    {
        while (angle < -MathF.PI) angle += MathF.Tau;
        while (angle > MathF.PI) angle -= MathF.Tau;
        return angle;
    }

    internal static float RayAabb(Vector3 offset, Vector3 direction, float halfWidth, float halfHeight)
    {
        var inverseX = 1f / direction.X;
        var inverseZ = 1f / direction.Z;
        var minimum = -float.Epsilon;
        var maximum = float.MaxValue;
        var x1 = (-halfWidth - offset.X) * inverseX;
        var x2 = (halfWidth - offset.X) * inverseX;
        var z1 = (-halfHeight - offset.Z) * inverseZ;
        var z2 = (halfHeight - offset.Z) * inverseZ;
        minimum = Min(Max(x1, minimum), Max(x2, minimum));
        maximum = Max(Min(x1, maximum), Min(x2, maximum));
        minimum = Min(Max(z1, minimum), Max(z2, minimum));
        maximum = Max(Min(z1, maximum), Min(z2, maximum));
        if (minimum > maximum) return float.MaxValue;
        if (minimum >= 0) return minimum;
        return maximum >= 0 ? maximum : float.MaxValue;

        static float Min(float x, float y) => x < y ? x : y;
        static float Max(float x, float y) => x > y ? x : y;
    }
}
