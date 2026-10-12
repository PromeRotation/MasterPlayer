using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleRaceStateRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville director prepare and countdown never release movement", () =>
        {
            var s = new BlundervilleRaceState();
            s.Director(0x80040000, 0x80000001, 0x4E2D, 0, 0, 0, Epoch);
            Check(s.Course == BlundervilleCourse.None && !s.CanMove(Epoch), "An unrelated director started racing.");
            Director(s, 5, 0);
            Director(s, 2, 4);
            Director(s, 0x80000001, 0);
            Director(s, 0x80000001, 1);
            Check(s.IsPrepared && !s.IsRacing && !s.CanMove(Epoch), "Countdown or a generic director flag started racing.");
            Check(!s.RecoverInProgress(BlundervilleCourse.FirstA, Epoch), "Recovery bypassed the preparation gate.");
        });
        foreach (var (stage, course) in new (uint, BlundervilleCourse)[]
                 { (0x4E2D, BlundervilleCourse.FirstA), (0x4E2E, BlundervilleCourse.FirstB),
                   (0x4E2F, BlundervilleCourse.Ring), (0x4E30, BlundervilleCourse.Delivery), (0x4E31, BlundervilleCourse.Final) })
            run($"Blunderville recorded director stage {stage:X} selects {course}", () =>
            {
                var s = Started(stage);
                Check(s.Course == course && s.IsRacing && s.CanMove(Epoch), "The recorded stage selected the wrong course or stayed locked.");
                Check(!s.IsPrepared && !s.Qualified && s.StartedAt == Epoch, "Start failed to initialize its lifecycle.");
            });
        run("Blunderville repeated stage signal preserves delivery progress", () =>
        {
            var s = Started(0x4E30);
            Director(s, 3, 2);
            var version = s.RoundVersion;
            s.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 0x4E30, 0, 0, 0, Epoch.AddSeconds(2));
            Check(s.Score == 2 && s.RoundVersion == version && s.StartedAt == Epoch, "A repeated signal restarted the round.");
        });
        foreach (var command in new uint[] { 6, 7, 8 })
            run($"Blunderville director end {command} stops and blocks mid-course recovery", () =>
            {
                var s = new BlundervilleRaceState();
                Director(s, command, 0);
                Check(!s.CanMove(Epoch) && !s.RecoverInProgress(BlundervilleCourse.FirstA, Epoch), "Ended content was recovered from a lingering position.");
                Director(s, 5, 0);
                Director(s, 0x80000001, 0x4E2E);
                Check(s.CanMove(Epoch) && !s.Qualified, "A new prepared round could not start.");
                Director(s, command, 0);
                Check(!s.IsRacing && !s.CanJump(Epoch), "The end signal retained racing input.");
            });
        run("Blunderville late enable can recover only an unprepared unlocked course", () =>
        {
            var s = new BlundervilleRaceState();
            Sync(s, new BlundervilleRaceStatus(3782, 0, DateTime.MaxValue));
            Check(!s.RecoverInProgress(BlundervilleCourse.FirstA, Epoch), "The start bind was bypassed.");
            Sync(s);
            Check(s.RecoverInProgress(BlundervilleCourse.FirstA, Epoch) && s.CanMove(Epoch), "A verified in-progress course was not recovered.");
            Check(!s.RecoverInProgress(BlundervilleCourse.Final, Epoch), "Recovery replaced an active course.");
        });
        foreach (var (extra, expected) in new (ushort, Vector3)[]
                 { (35, new(-240.2991f, 6, 275.4989f)), (36, new(-159.4339f, 6, 275.2527f)),
                   (37, new(-200.1303f, 6, 205.1951f)) })
            run($"Blunderville recorded crystal parameter {extra} selects its scoring floor", () =>
            {
                var s = Started(0x4E30);
                Check(s.NeedsPickup, "A delivery round without a crystal had no pickup target.");
                Sync(s, new BlundervilleRaceStatus(3704, extra, DateTime.MaxValue));
                Check(s.CarryCrystalParam == extra && s.DeliveryTarget == expected && !s.NeedsPickup, "The crystal selected the wrong floor.");
                Sync(s);
                Check(s.Score == 0 && s.NeedsPickup, "Losing a crystal fabricated a delivery score.");
            });
        run("Blunderville unknown crystal stays held without guessing a destination", () =>
        {
            var s = Started(0x4E30);
            Sync(s, new BlundervilleRaceStatus(3704, 99, DateTime.MaxValue));
            Check(s.DeliveryTarget == null && !s.NeedsPickup, "An unknown crystal was converted to a guessed floor or new pickup.");
        });
        run("Blunderville delivery score is director-confirmed monotone and stops after qualification", () =>
        {
            var s = Started(0x4E30);
            Director(s, 3, 1); Director(s, 3, 0); Director(s, 3, 2); Director(s, 3, 99);
            Check(s.Score == 2, "An old or malformed score changed confirmed progress.");
            Director(s, 3, 3);
            Check(!s.NeedsPickup, "Three deliveries requested another crystal.");
            Director(s, 7, 0);
            Check(s.Qualified && !s.CanMove(Epoch), "Qualification retained navigation.");
            var first = Started(0x4E2D); Director(first, 3, 3);
            Check(first.Score == 0, "A non-delivery director value became delivery progress.");
        });
        foreach (var status in new uint[] { 149, 3782, 3708, 3737 })
            run($"Blunderville movement lock {status} expires without leaving input active", () =>
            {
                var s = Started(0x4E2D);
                Sync(s, new BlundervilleRaceStatus(status, 0, Epoch.AddSeconds(1)));
                Check(!s.CanMove(Epoch) && !s.CanJump(Epoch), "An active movement lock accepted input.");
                s.SynchronizeStatuses([], Epoch.AddSeconds(2));
                Check(s.CanMove(Epoch.AddSeconds(2)), "An expired movement lock stayed latched.");
            });
        run("Blunderville spinning allows forward input while slippery slope prohibits jumping", () =>
        {
            var s = Started(0x4E2E);
            Sync(s, new BlundervilleRaceStatus(3702, 7, DateTime.MaxValue));
            Check(s.IsSpinning && s.CanMove(Epoch), "The permanent spinning zone deadlocked movement.");
            Sync(s, new BlundervilleRaceStatus(3703, 0, DateTime.MaxValue));
            Check(s.IsSlidingSlope && s.CanMove(Epoch) && !s.CanJump(Epoch), "The no-jump slope either stopped walking or accepted jumping.");
        });
        run("Blunderville misdirection uses the native arrow with angular wrapping", () =>
        {
            var s = Started(0x4E2D);
            Sync(s, new BlundervilleRaceStatus(3694, 360, DateTime.MaxValue));
            Check(!s.CanMoveToward(Vector3.Zero, -Vector3.UnitZ, null, Epoch), "Unknown arrow direction accepted input.");
            Check(s.CanMoveToward(Vector3.Zero, -Vector3.UnitZ, -MathF.PI + 0.02f, Epoch), "A wrapped aligned arrow was rejected.");
            Check(!s.CanMoveToward(Vector3.Zero, -Vector3.UnitZ, 0, Epoch), "An opposite arrow accepted input.");
            Check(!s.CanMoveToward(Vector3.Zero, Vector3.Zero, 0, Epoch), "Zero-length steering accepted input during misdirection.");
            Check(!s.CanMoveToward(Vector3.Zero, Vector3.UnitZ, float.NaN, Epoch), "A nonfinite arrow accepted input.");
            Sync(s);
            Check(s.CanMoveToward(Vector3.Zero, Vector3.UnitZ, null, Epoch), "Ordinary movement incorrectly required a native arrow.");
            Check(!s.CanMoveToward(new(float.PositiveInfinity, 0, 0), Vector3.Zero, null, Epoch), "A nonfinite position accepted input.");
        });
        foreach (var (id, offset) in new (uint, float)[] { (3698, 0), (3699, MathF.PI), (3700, MathF.PI / 2), (3701, -MathF.PI / 2) })
            run($"Blunderville forced command {id} retains its relative bearing and expiry", () =>
            {
                var s = Started(0x4E2D);
                Sync(s, new BlundervilleRaceStatus(id, 0, Epoch.AddSeconds(5)));
                Check(s.ForcedDirectionStatus == id && s.ForcedDirectionOffset == offset && s.ForcedMoveExpiresAt == Epoch.AddSeconds(5),
                    "A forced command lost its expiry or relative direction.");
                s.SynchronizeStatuses([new(3737, 4, Epoch.AddSeconds(8))], Epoch.AddSeconds(5));
                Check(s.IsForcedMoving && s.ForcedDirectionStatus == null && !s.CanMove(Epoch.AddSeconds(5)), "Forced execution allowed ordinary input or retained stale preparation.");
            });
        run("Blunderville acceleration bomb stops before expiry and holds through the network grace", () =>
        {
            var s = Started(0x4E2D);
            Sync(s, new BlundervilleRaceStatus(3802, 0, Epoch.AddSeconds(5)));
            Check(s.CanMove(Epoch.AddSeconds(3.7)) && !s.CanMove(Epoch.AddSeconds(3.9)), "The bomb stopping margin was not applied.");
            Check(s.MustStopForAccelerationBomb(Epoch.AddSeconds(5.2)) && !s.MustStopForAccelerationBomb(Epoch.AddSeconds(5.4)), "Bomb expiry grace was not bounded.");
            Sync(s, new BlundervilleRaceStatus(3802, 0, DateTime.MaxValue));
            Check(!s.CanMove(Epoch), "An acceleration bomb with unknown expiry accepted movement.");
        });
        run("Blunderville replay time reversal clears round and transient statuses", () =>
        {
            var s = Started(0x4E30);
            s.SynchronizeStatuses([new(3704, 35, DateTime.MaxValue)], Epoch.AddSeconds(10));
            s.SynchronizeStatuses([], Epoch.AddSeconds(1));
            Check(!s.IsRacing && s.Course == BlundervilleCourse.None && s.Score == 0 && s.DeliveryTarget == null,
                "Seeking backwards retained a racing round or a carried crystal.");
            Check(!s.CanMove(Epoch.AddSeconds(1)), "Seeking backwards retained navigation.");
        });
        run("Blunderville recorded carrying crystal does not slow ordinary navigation", () =>
        {
            var s = Started(0x4E30);
            Sync(s, new BlundervilleRaceStatus(3704, 35, DateTime.MaxValue));
            Check(s.NavigationBaseSpeed(Epoch) == 6, "Carrying a crystal inherited an unobserved movement penalty.");
        });
        run("Blunderville recorded spinning speed takes priority over sprint", () =>
        {
            var s = Started(0x4E2E);
            Sync(s, new BlundervilleRaceStatus(3702, 7, DateTime.MaxValue), new BlundervilleRaceStatus(1548, 30, Epoch.AddSeconds(5)));
            Check(s.NavigationBaseSpeed(Epoch) == 3.6f, "Sprint overrode the recorded fixed spinning speed.");
        });
        run("Blunderville recorded minigame sprint uses 7.8 and expires to ordinary speed", () =>
        {
            var s = Started(0x4E30);
            Sync(s, new BlundervilleRaceStatus(1548, 30, Epoch.AddSeconds(5)));
            Check(s.NavigationBaseSpeed(Epoch) == 7.8f, "Sprint ignored its recorded navigation speed.");
            Check(s.NavigationBaseSpeed(Epoch.AddSeconds(5.1)) == 6, "An expired sprint retained its movement speed.");
        });
        foreach (var command in new uint[] { 6, 7, 8 })
            run($"Blunderville director end {command} releases the race navigation speed", () =>
            {
                var s = Started(0x4E30);
                Sync(s, new BlundervilleRaceStatus(1548, 30, Epoch.AddSeconds(5)));
                Director(s, command, 0);
                Check(s.NavigationBaseSpeed(Epoch) == 6, "Ended content retained a race movement-speed override.");
            });
        run("Blunderville slippery zones use conservative navigation estimates before sprint", () =>
        {
            var s = Started(0x4E2D);
            Sync(s, new BlundervilleRaceStatus(3693, 50, DateTime.MaxValue), new BlundervilleRaceStatus(1548, 30, Epoch.AddSeconds(5)));
            Check(s.NavigationBaseSpeed(Epoch) <= 3.0f, "A variable slippery trajectory was planned at ordinary or sprint speed.");
            Sync(s, new BlundervilleRaceStatus(3703, 0, DateTime.MaxValue), new BlundervilleRaceStatus(1548, 30, Epoch.AddSeconds(5)));
            Check(s.NavigationBaseSpeed(Epoch) <= 3.6f, "A slope estimate was replaced with ordinary or sprint speed.");
        });
    }

    private static BlundervilleRaceState Started(uint stage)
    {
        var state = new BlundervilleRaceState();
        Director(state, 5, 0);
        Director(state, 0x80000001, stage);
        return state;
    }

    private static void Director(BlundervilleRaceState state, uint command, uint param1)
        => state.Director(BlundervilleRaceState.DirectorInstance, command, param1, 0, 0, 0, Epoch);

    private static void Sync(BlundervilleRaceState state, params BlundervilleRaceStatus[] statuses)
        => state.SynchronizeStatuses(statuses, Epoch);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
