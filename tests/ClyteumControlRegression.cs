using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunClyteumControls(Action<string, Action> run)
    {
        run("Clyteum real hints publish scanner ACR cancellation autoattack movement hold and release gates", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var replay = false; var hold = false; var block = false; var cancel = false; var stopped = 0; var clears = 0;
            var control = new MechanicFacingControl((_, _, _, _, _, h, b, c) => { hold = h; block = b; cancel = c; },
                _ => { ++clears; hold = false; block = false; });
            var hints = new ScriptHintsApi(() => replay, control: control, stopAutoAttack: (h, _) => { if (h) ++stopped; });
            var now = DateTime.UtcNow;
            var engine = new DungeonEncounter(1345);
            DungeonActor boss = new(1, 0x4C2C, new(-615, 0, 575), InCombat: true);
            DungeonActor player = new(100, 0, boss.Position, PartySlot: 0, Hostile: false);
            DungeonActor scanner = new(30, 0x4C2D, boss.Position, RenderFlags: 0);
            void Publish()
            {
                hints.SetActionRestrictions(holdActions: engine.RequireAllStop,
                    blockCasts: engine.RequireAllStop || engine.RequireClyteumMovement,
                    cancelCast: engine.RequireAllStop || engine.RequireClyteumMovement);
                hints.SetMovementHold(engine.RequireAllStop);
            }
            try
            {
                engine.Synchronize([boss, player, scanner], player.Position, now, playerId: 100);
                Publish();
                Check(hold && block && cancel && stopped > 0 && plugin.SafeZone.Setting.RuntimeMovementHeld,
                    "Scanner control did not reach PR/MP facades.");
                replay = true; Publish();
                Check(!hold && !block && clears > 0 && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Replay retained controls.");
                replay = false;
                scope.Settings.EnableSmartMovement = false; Publish();
                Check(!hold && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Master switch retained controls.");
                scope.Settings.EnableSmartMovement = true;
                player = player with { Position = boss.Position + System.Numerics.Vector3.UnitX * 21 };
                engine.Synchronize([boss, player, scanner], player.Position, now, playerId: 100); Publish();
                Check(!hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Leaving beam retained controls.");
                engine.ResetFight();
                boss = boss with { Oid = 0x4C28, Position = new(760, 0, -803) };
                player = player with { Position = boss.Position };
                engine.Synchronize([boss, player], player.Position, now, playerId: 100);
                engine.StartCast(1, new(48931, boss.Position, 0, now, now.AddSeconds(5)), now);
                engine.Tick(now.AddSeconds(4.1)); Publish();
                Check(!hold && block && cancel && !plugin.SafeZone.Setting.RuntimeMovementHeld, "StringUp blocked movement or failed cast gate.");
                engine.Finish(1, 48931, now.AddSeconds(5)); Publish();
                Check(!hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "StringUp did not release.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
