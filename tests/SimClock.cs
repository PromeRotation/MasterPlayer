using System.Diagnostics;

// Simulated background-solve latency. By default the measured wall time of a
// solve is added on top of the scenario's dispatch latency; under a saturated
// parallel run (run-tests.ps1) that wall time reflects CPU contention, not the
// solver, so MP_SIM_FIXED_LATENCY=1 keeps only the scenario's fixed latency.
internal static class SimClock
{
    private static readonly bool Fixed = Environment.GetEnvironmentVariable("MP_SIM_FIXED_LATENCY") == "1";
    public static double Solve(Stopwatch watch) => Fixed ? 0 : watch.Elapsed.TotalSeconds;
}
