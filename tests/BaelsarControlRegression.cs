using System.Numerics;
using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunBaelsarControls(Action<string, Action> run)
    {
        run("Baelsar real hints publish caution ACR cancellation autoattack and MP movement hold", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var replay = false;
            var holds = false;
            var blocks = false;
            var cancels = false;
            var stopped = false;
            var cleared = 0;
            var control = new MechanicFacingControl((_, _, _, _, _, hold, block, cancel) =>
            {
                holds = hold; blocks = block; cancels = cancel;
            }, _ => { ++cleared; holds = false; blocks = false; });
            var hints = new ScriptHintsApi(() => replay, control: control, stopAutoAttack: (hold, _) => stopped |= hold);
            var now = DateTime.UtcNow;
            var engine = new DungeonEncounter(1114);
            DungeonActor boss = new(1, 0x193A, new(116, 0, 0), InCombat: true);
            DungeonActor player = new(100, 0, boss.Position, PartySlot: 0, Hostile: false, Statuses: [1132],
                StatusExpirations: new Dictionary<uint, DateTime> { [1132] = now.AddSeconds(2) });
            try
            {
                engine.Synchronize([boss, player], player.Position, now, playerId: 100);
                Check(!engine.RequireAllStop, "Caution stop began more than one second early.");
                engine.Tick(now.AddSeconds(1));
                hints.SetActionRestrictions(holdActions: engine.RequireAllStop, blockCasts: engine.RequireAllStop, cancelCast: engine.RequireAllStop);
                hints.SetMovementHold(engine.RequireAllStop);
                Check(holds && blocks && cancels && stopped && plugin.SafeZone.Setting.RuntimeMovementHeld,
                    "Caution did not stop ACR, queued casts, native attacks and MP movement together.");
                replay = true;
                hints.SetActionRestrictions(holdActions: true);
                Check(!holds && !plugin.SafeZone.Setting.RuntimeMovementHeld && cleared > 0, "Replay retained live controls.");
                replay = false;
                engine.StatusChanged(100, 1132, false, now.AddSeconds(1));
                hints.SetActionRestrictions(holdActions: engine.RequireAllStop, blockCasts: engine.RequireAllStop);
                hints.SetMovementHold(engine.RequireAllStop);
                Check(!holds && !blocks && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Caution removal did not release controls.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
