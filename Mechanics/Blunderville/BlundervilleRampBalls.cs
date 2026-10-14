using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// FirstB slime ramp snowballs (16351). 2026-10-14 13:05 ARR: the straight
// climb (1.14.16) ran up x=5.4, a snowball lane, and was knocked into the
// slide twice. 22 FirstB rounds: a pair spawns at z -366.81 every 5.20s,
// alternating lanes {-1.8, 5.5} and {-5.5, 1.8}; each ball rests 2.0s, rolls
// 3.78m/s to z -344.81 (7.8s) and despawns ~8.9s after spawning (p10/p90 of
// the position at a given age about +-1m).
internal sealed class BlundervilleRampBalls
{
    internal const float SpawnZ = -366.81f, RestZ = -344.81f, Roll = 3.78f;
    internal const double Hold = 2, Despawn = 9.3, Period = 5.2;
    internal static readonly float[][] Pairs = [[-1.8333f, 5.5f], [-5.5f, 1.8333f]];

    internal readonly record struct Ball(DateTime Spawn, float X);
    private sealed record Track(Ball Ball, float Z, DateTime Wave);
    private readonly Dictionary<ulong, Track> _seen = [];

    public void Reset() => _seen.Clear();

    // Actor scans can begin during a hold or halfway down the ramp.
    public void Observe(BlundervilleActor actor, DateTime now)
    {
        if (actor.Oid != BlundervilleCatalog.Ball) return;
        var p = actor.Position;
        if (p.Z > -344 || p.Z < -370 || MathF.Abs(p.X) > 7) return;
        if (_seen.TryGetValue(actor.Id, out var old)
            && (MathF.Abs(old.Z - p.Z) < .1f || p.Z <= SpawnZ + .3f || p.Z >= RestZ - .3f)) return;
        // A late first observation can be anywhere in the initial hold. Once
        // the ball moves, correct its age from the observed position rather
        // than keeping that uncertain spawn time for the entire descent.
        var ball = new Ball(now - TimeSpan.FromSeconds(Math.Clamp((p.Z - SpawnZ) / Roll + (p.Z > SpawnZ + .3f ? Hold : 0), 0, Despawn)), p.X);
        _seen[actor.Id] = new(ball, p.Z, old?.Wave ?? ball.Spawn);
        foreach (var id in _seen.Where(b => (now - b.Value.Ball.Spawn).TotalSeconds > 60).Select(b => b.Key).ToArray()) _seen.Remove(id);
    }

    public bool Ready(DateTime now) => _seen.Values.Any(b => (now - b.Ball.Spawn).TotalSeconds < 2 * Period);

    // Seen balls plus the next pairs on the 5.2s alternation.
    public IReadOnlyList<Ball> Forecast(DateTime now, double horizon)
    {
        var result = _seen.Values.Select(b => b.Ball).Where(b => (now - b.Spawn).TotalSeconds < Despawn).ToList();
        if (!Ready(now)) return result;
        var latest = _seen.Values.MaxBy(b => b.Wave)!;
        var last = latest.Ball with { Spawn = latest.Wave };
        var pair = Array.FindIndex(Pairs, p => p.Any(x => MathF.Abs(x - last.X) < .5f));
        if (pair < 0) return result;
        for (var k = 1; last.Spawn.AddSeconds(k * Period) < now.AddSeconds(horizon); ++k)
            foreach (var x in Pairs[(pair + k) % 2])
                result.Add(new(last.Spawn.AddSeconds(k * Period), x));
        return result;
    }

    // The ball's z at an age, or null before it spawned / after it despawned.
    internal static float? Z(double age)
        => age < 0 || age > Despawn ? null : age < Hold ? SpawnZ : MathF.Min(RestZ, SpawnZ + Roll * (float)(age - Hold));
}
