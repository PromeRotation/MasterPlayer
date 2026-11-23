using System.Numerics;
using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunQueenEternalControls(Action<string, Action> run)
    {
        run("QueenEternal real PR hints renew gaze heading ACR hold cancel autoattack MP hold and release on replay master status", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var replay = false; var hold = false; var block = false; var cancel = false; var stopped = 0;
            float? heading = null;
            var control = new MechanicFacingControl((_, _, _, _, angle, h, b, c) => { heading = angle; hold = h; block = b; cancel = c; },
                _ => { heading = null; hold = false; block = false; });
            var hints = new ScriptHintsApi(() => replay, control: control, stopAutoAttack: (h, _) => { if (h) ++stopped; });
            var now = DateTime.UtcNow;
            var engine = new DungeonEncounter(1202);
            DungeonActor boss = new(1, 0x41C0, new(100, 0, 100), InCombat: true);
            DungeonActor player = new(100, 0, boss.Position, PartySlot: 0, Hostile: false);
            DungeonActor eye = new(101, 0, boss.Position + Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
            void Publish()
            {
                hints.SetFacing(engine.GazeFacing(player.Position, MathF.PI / 2));
                hints.SetActionRestrictions(holdActions: engine.RequireAllStop, blockCasts: engine.RequireAllStop, cancelCast: engine.RequireAllStop);
                hints.SetMovementHold(engine.RequireAllStop);
            }
            try
            {
                engine.Synchronize([boss, player, eye], player.Position, now, playerId: 100);
                engine.StatusChanged(100, 4130, true, now, expiresAt: now.AddSeconds(5));
                engine.StatusChanged(101, 3815, true, now, expiresAt: now.AddSeconds(5));
                engine.Tick(now.AddSeconds(4.4)); Publish();
                Check(heading != null && hold && block && cancel && stopped > 0 && plugin.SafeZone.Setting.RuntimeMovementHeld,
                    "Queen control failed PR heading/ACR/autoattack/MP hold.");
                replay = true; Publish();
                Check(heading == null && !hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Replay retained execution.");
                replay = false; scope.Settings.EnableSmartMovement = false; Publish();
                Check(heading == null && !hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Master disabled retained execution.");
                scope.Settings.EnableSmartMovement = true; Publish(); Check(hold && heading != null, "Enabled controls not renewed.");
                engine.StatusChanged(100, 4130, false, now); engine.StatusChanged(101, 3815, false, now); Publish();
                Check(heading == null && !hold && !block && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Status loss retained controls.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
