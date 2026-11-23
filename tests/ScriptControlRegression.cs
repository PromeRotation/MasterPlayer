using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunScriptControls(Action<string, Action> run)
    {
        run("script controls read existing host action restrictions through the optional interface", () =>
        {
            var host = typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl")
                ?? throw new InvalidOperationException("The development host must provide the existing control interface.");
            var requests = host.GetField("requests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var requestType = requests.FieldType.GetElementType()!;
            var original = requests.GetValue(null);
            var active = Array.CreateInstance(requestType, 1);
            // Populate the host snapshot directly: Update also touches the game's native action queue.
            active.SetValue(Activator.CreateInstance(requestType,
                ["MasterPlayer.Tests/HostRestrictions", Environment.TickCount64 + 10000, 0UL, 0UL,
                    new ulong[] { 42 }, null, true, true]), 0);
            try
            {
                requests.SetValue(null, active);
                Check(MechanicFacingControl.HoldActions && MechanicFacingControl.BlockCasts,
                    "The SDK-compatible reader lost the running host's action restrictions.");
                Check(!MechanicFacingControl.AllowsTarget(42) && MechanicFacingControl.AllowsTarget(43),
                    "The SDK-compatible reader lost the running host's target restrictions.");
                requests.SetValue(null, Array.CreateInstance(requestType, 0));
                Check(!MechanicFacingControl.HoldActions && !MechanicFacingControl.BlockCasts
                    && MechanicFacingControl.AllowsTarget(42), "Cleared host restrictions remained cached.");
            }
            finally
            {
                requests.SetValue(null, original);
            }
        });
        run("script controls shield restrictions stop native autoattack and never execute in replay", () =>
        {
            using var scope = new FollowScope();
            var replay = false;
            var stopped = new List<(bool Hold, ulong[] Targets)>();
            var published = new List<ulong[]>();
            var control = new MechanicFacingControl((_, _, _, targets, _, _, _, _) => published.Add(targets.ToArray()), _ => { });
            var hints = new ScriptHintsApi(() => replay, control: control,
                stopAutoAttack: (hold, targets) => stopped.Add((hold, targets.ToArray())));
            try
            {
                hints.SetActionRestrictions(forbiddenTargets: [1, 10]);
                Check(stopped.Count == 1 && !stopped[0].Hold && stopped[0].Targets.SequenceEqual(new ulong[] { 1, 10 }),
                    "Shield restrictions failed to stop target-only native autoattack.");
                hints.SetFacing(1);
                Check(published[^1].SequenceEqual(new ulong[] { 1, 10 }), "Facing erased target restriction.");
                hints.ClearActionRestrictions();
                Check(stopped.Count == 2, "Clearing restriction issued a native attack change.");
                replay = true;
                hints.SetActionRestrictions(holdActions: true, forbiddenTargets: [1]);
                Check(stopped.Count == 2, "Replay executed native autoattack control.");
                replay = false;
                scope.Settings.EnableSmartMovement = false;
                hints.SetActionRestrictions(holdActions: true);
                Check(stopped.Count == 2, "Movement switch off executed native control.");
            }
            finally
            {
                hints.Cleanup();
            }
        });
        run("script controls renew identical PR leases and preserve independent facing and ACR restrictions", () =>
        {
            using var scope = new FollowScope();
            var calls = 0;
            var clears = 0;
            long now = 0;
            long expires = 0;
            float? heading = null;
            var holding = false;
            var blocking = false;
            var cancelling = false;
            ulong target = 0;
            ulong[] forbidden = [];
            var control = new MechanicFacingControl((_, preferred, _, targets, face, hold, block, cancel) =>
            {
                target = preferred;
                ++calls;
                expires = now + 500;
                heading = face;
                holding = hold;
                blocking = block;
                cancelling = cancel;
                forbidden = targets.ToArray();
            }, _ => ++clears);
            var hints = new ScriptHintsApi(() => false, control: control, stopAutoAttack: (_, _) => { });
            try
            {
                hints.SetFacing(1.25f);
                hints.SetTarget(50);
                Check(target == 50 && heading == 1.25f, "Target request lost facing.");
                hints.SetTarget(0);
                now = 400;
                hints.SetFacing(1.25f);
                now = 600;
                Check(calls == 4 && expires > now, "An unchanged heading failed to renew PR's 500ms lease.");
                hints.SetActionRestrictions(holdActions: true, blockCasts: true, cancelCast: true);
                Check(heading == 1.25f && holding && blocking && cancelling, "ACR restrictions erased facing or lost cancellation.");
                hints.SetFacing(1.5f);
                Check(holding && blocking && !cancelling, "Facing erased ACR restrictions or repeated the one-shot cancel.");
                hints.ClearFacing();
                Check(heading == null && holding && clears == 0, "Clearing facing removed another mechanism's action restrictions.");
                hints.SetActionRestrictions(forbiddenTargets: [42]);
                Check(!holding && !blocking && forbidden.SequenceEqual([42UL]), "Target-only hold blocked healing/self-defense.");
                hints.ClearActionRestrictions();
                Check(clears == 1, "Ending the final constraint did not clear the owner.");
                hints.SetFacing(float.NaN);
                Check(clears == 1, "Invalid headings created a host request.");
            }
            finally
            {
                hints.Cleanup();
            }
        });

        run("script controls isolate owners and release ACR and movement holds on replay and movement off", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var live = new HashSet<string>();
            var replay = false;
            MechanicFacingControl NewControl() => new((owner, _, _, _, _, _, _, _) => live.Add(owner),
                owner => live.Remove(owner));
            var first = new ScriptHintsApi(() => replay, control: NewControl(), stopAutoAttack: (_, _) => { });
            var second = new ScriptHintsApi(() => false, control: NewControl(), stopAutoAttack: (_, _) => { });
            try
            {
                first.SetActionRestrictions(holdActions: true);
                second.SetFacing(2);
                first.SetMovementHold(true);
                second.SetMovementHold(true);
                Check(live.Count == 2 && plugin.SafeZone.Setting.RuntimeMovementHeld, "Owners did not publish independent controls.");
                replay = true;
                first.SetFacing(1);
                Check(live.Count == 1 && plugin.SafeZone.Setting.RuntimeMovementHeld, "Replay cleanup removed another owner's hold.");
                scope.Settings.EnableSmartMovement = false;
                second.SetActionRestrictions(holdActions: true);
                Check(live.Count == 0 && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Movement off retained ACR or movement controls.");
                scope.Settings.EnableSmartMovement = true;
                plugin.Scripts.ReplayModeActive = true;
                second.SetFacing(1);
                second.SetActionRestrictions(holdActions: true, blockCasts: true, cancelCast: true);
                second.SetMovementHold(true);
                Check(live.Count == 0 && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Offline replay published actual controls.");
                plugin.Scripts.ReplayModeActive = false;
                second.SetFacing(1);
                Check(live.Count == 1, "Ending replay failed to restore fresh controls.");
                second.Cleanup();
                second.Cleanup();
                Check(live.Count == 0, "Idempotent cleanup retained a host request.");
            }
            finally
            {
                first.Cleanup();
                second.Cleanup();
            }
        });

        run("script controls clear a partially published PR request after host failure", () =>
        {
            using var scope = new FollowScope();
            var live = new HashSet<string>();
            var control = new MechanicFacingControl((owner, _, _, _, _, _, _, _) =>
            {
                live.Add(owner);
                throw new InvalidOperationException("Host failed after storing the request.");
            }, owner => live.Remove(owner));
            var hints = new ScriptHintsApi(() => false, control: control, stopAutoAttack: (_, _) => { });
            var failed = false;
            try
            {
                hints.SetFacing(1);
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }
            finally
            {
                hints.Cleanup();
            }
            Check(failed && live.Count == 0, "A failed host update escaped owner cleanup.");
        });
    }
}
