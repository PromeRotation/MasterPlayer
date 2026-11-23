using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunValigarmandaControls(Action<string, Action> run)
    {
        run("Valigarmanda real PR hints block and cancel casts while movement continues and release on replay disable status", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var replay = false; var hold = false; var block = false; var cancel = false;
            var control = new MechanicFacingControl((_, _, _, _, _, h, b, c) => { hold = h; block = b; cancel = c; },
                _ => { hold = false; block = false; });
            var hints = new ScriptHintsApi(() => replay, control: control, stopAutoAttack: (_, _) => { });
            var now = DateTime.UtcNow;
            var engine = new DungeonEncounter(1195);
            DungeonActor boss = new(1, 0x4115, new(100, 0, 100), InCombat: true);
            DungeonActor player = new(100, 0, boss.Position, PartySlot: 0, Hostile: false);
            void Publish()
            {
                hints.SetActionRestrictions(blockCasts: engine.RequireValigMovement, cancelCast: engine.RequireValigMovement);
                hints.SetMovementHold(false);
            }
            try
            {
                engine.Synchronize([boss, player], player.Position, now, playerId: 100);
                engine.StartCast(1, new(36177, boss.Position, 0, now, now.AddSeconds(5.3)), now);
                engine.Tick(now.AddSeconds(6.4)); Publish();
                Check(block && cancel && !hold && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Freeze failed cast restriction or held movement.");
                replay = true; Publish();
                Check(!block && !hold && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Replay retained PR execution.");
                replay = false; scope.Settings.EnableSmartMovement = false; Publish();
                Check(!block && !hold, "Movement master switch retained controls.");
                scope.Settings.EnableSmartMovement = true; Publish(); Check(block, "Active freeze did not renew request.");
                engine.StatusChanged(100, 3523, false, now.AddSeconds(7)); Publish();
                Check(!block && !hold, "Freeze status loss retained cast restriction.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
