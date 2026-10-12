using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;

// ARR 2026-10-12 02:33:25 (MP 1.12.0): both acceleration bombs (3802) exploded
// after the 0.6s stop, as in 01:01 and 00:17; the forced stop fell inside the
// FirstA ramp sweep rows.
internal static class BlundervilleFeedback023325Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 023325 bomb stop margin clears every recorded explosion border", () =>
        {
            // Seconds between the last movement and the 3802 expiry (gain + 5s)
            // across all recorded ARRs, with whether the bomb exploded.
            var recorded = new (double LastMove, bool Exploded)[]
            {
                (1.09, false), (.89, false), (.98, false), (.61, false), (.65, false), (1.11, false), (.61, false), (.88, false),
                (.56, false), (1.31, false), (1.33, false), (.60, false), (.29, true), (.50, true), (.50, true), (.57, true),
                (.60, true), (.48, true),
            };
            var border = recorded.Where(r => r.Exploded).Max(r => r.LastMove);
            Check(BlundervilleRaceState.AccelerationBombStopMargin >= border + .5,
                "The bomb stop margin does not clear the recorded explosion border by half a second.");
            Check(recorded.Where(r => !r.Exploded).Min(r => r.LastMove) < BlundervilleRaceState.AccelerationBombStopMargin,
                "The recorded safe stops no longer bracket the margin; re-check the evidence.");
        });
        run("Blunderville 023325 bomb stops early only where standing stays clear", () =>
        {
            var state = Bomb(out var expires);
            var player = new Vector3(-198, 16.8f, -218.5f);
            BlundervilleHazard Sweep(double from, double until) => new("animation/test", new RectangleGeometry(new(-197, 16.8f, -219.9f), 0, 2, 3),
                expires.AddSeconds(from), expires.AddSeconds(until), expires.AddSeconds(from));
            Check(!BlundervilleBombStop.StopEarly(state, [], player, expires.AddSeconds(-3)), "A bomb stopped before its safe-stop window.");
            Check(BlundervilleBombStop.StopEarly(state, [], player, expires.AddSeconds(-2.3)), "A clear position did not stop early.");
            Check(!BlundervilleBombStop.StopEarly(state, [Sweep(-.8, -.4)], player, expires.AddSeconds(-2.3)),
                "An early stop was taken inside a forecast sweep.");
            Check(BlundervilleBombStop.StopEarly(state, [Sweep(-3, -2.4)], player, expires.AddSeconds(-2.3)),
                "A sweep that already ended blocked a safe early stop.");
            Check(BlundervilleBombStop.StopEarly(state, [Sweep(-.8, -.4)], player, expires.AddSeconds(-1.1)),
                "The forced bomb stop was bypassed by a hazard check.");
            Check(BlundervilleBombStop.StopEarly(state, [Sweep(.2, .3)], new(-195, 16.8f, -222.5f), expires.AddSeconds(-2)),
                "A position outside the sweep was treated as unsafe.");
            Check(!state.CanMove(expires.AddSeconds(-1.1)) && state.CanMove(expires.AddSeconds(-1.4)),
                "The race state did not apply the 1.2s bomb stop.");
        });
    }

    private static BlundervilleRaceState Bomb(out DateTime expires)
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        expires = Epoch.AddSeconds(10);
        state.SynchronizeStatuses([new(3802, 0, expires)], Epoch.AddSeconds(5));
        return state;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
