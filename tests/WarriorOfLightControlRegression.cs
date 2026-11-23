using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunWarriorOfLightControls(Action<string, Action> run)
    {
        run("WarriorOfLight real hints Pyretic stop and ice cast cancellation release across master replay and expiry", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var replay = false; var hold = false; var block = false; var cancel = false; var stopped = 0;
            var control = new MechanicFacingControl((_, _, _, _, _, h, b, c) => { hold = h; block = b; cancel = c; },
                _ => { hold = false; block = false; });
            var hints = new ScriptHintsApi(() => replay, control: control, stopAutoAttack: (h, _) => { if (h) ++stopped; });
            var now = DateTime.UtcNow;
            var engine = new DungeonEncounter(922);
            DungeonActor boss = new(1, 0x2DDE, new(100, 0, 100), InCombat: true);
            DungeonActor player = new(100, 0, boss.Position, PartySlot: 0, Hostile: false);
            void Publish()
            {
                hints.SetActionRestrictions(holdActions: engine.RequireAllStop,
                    blockCasts: engine.RequireAllStop || engine.RequireWolMovement,
                    cancelCast: engine.RequireAllStop || engine.RequireWolMovement);
                hints.SetMovementHold(engine.RequireAllStop);
            }
            try
            {
                engine.Synchronize([boss, player], player.Position, now, playerId: 100);
                engine.StatusChanged(100, 960, true, now, expiresAt: now.AddSeconds(5)); Publish();
                Check(hold && block && cancel && stopped > 0 && plugin.SafeZone.Setting.RuntimeMovementHeld,
                    "Pyretic failed PR ACR, autoattack, cancellation or MP movement hold.");
                replay = true; Publish();
                Check(!hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Replay retained Pyretic controls.");
                replay = false; scope.Settings.EnableSmartMovement = false; Publish();
                Check(!hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Movement disabled retained Pyretic.");
                scope.Settings.EnableSmartMovement = true;
                engine.StatusChanged(100, 960, false, now); engine.Icon(100, 225, now);
                engine.Tick(now.AddSeconds(4)); Publish();
                Check(!hold && block && cancel && !plugin.SafeZone.Setting.RuntimeMovementHeld,
                    "Ice failed cast cancellation or blocked required movement.");
                engine.Tick(now.AddSeconds(6)); Publish();
                Check(!hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Ice finish retained controls.");
                hints.Cleanup();
            }
            finally { hints.Cleanup(); }
        });
    }
}
