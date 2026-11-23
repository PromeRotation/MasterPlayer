using System.Numerics;
using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    internal static void RunSirensongControls(Action<string, Action> run)
    {
        run("Sirensong real hints publish safe PR facing then hold active march and release on replay", () =>
        {
            using var scope = new FollowScope();
            using var plugin = new MasterPlayerPlugin(ScriptServiceProxy.Create<IGreenMoveSystem>((_, _) => null));
            var replay = false;
            float? published = null;
            var control = new MechanicFacingControl((_, heading, _, _, _, _, _, _) => published = heading, _ => published = null);
            var hints = new ScriptHintsApi(() => replay, control: control);
            var now = DateTime.UtcNow;
            var engine = new DungeonEncounter(1142);
            DungeonActor boss = new(1, 0x1AFE, new(-44.5f, 0, 465), InCombat: true);
            DungeonActor player = new(100, 0, boss.Position + Vector3.UnitZ * 4, PartySlot: 0, Hostile: false);
            try
            {
                engine.Synchronize([boss, player], player.Position, now, playerId: 100);
                engine.StartCast(1, new(8037, boss.Position, 0, now, now.AddSeconds(5.3)), now);
                engine.Tick(now.AddSeconds(5.3));
                hints.SetFacing(engine.GazeFacing(player.Position, 0));
                Check(published != null && !plugin.SafeZone.Setting.RuntimeMovementHeld, "Pending march safe PR facing missing.");
                engine.StatusChanged(100, 5174, true, now.AddSeconds(5.3), expiresAt: now.AddSeconds(8.3));
                hints.SetFacing(engine.GazeFacing(player.Position, 0));
                hints.SetMovementHold(engine.RequireSirenMovementHold);
                Check(published == null && plugin.SafeZone.Setting.RuntimeMovementHeld, "Active march competed with PR facing or MP movement.");
                replay = true;
                hints.SetMovementHold(true);
                Check(!plugin.SafeZone.Setting.RuntimeMovementHeld && published == null, "Replay retained live march hold.");
                replay = false;
                engine.StatusChanged(100, 5174, false, now.AddSeconds(5.3));
                hints.SetMovementHold(engine.RequireSirenMovementHold);
                Check(!plugin.SafeZone.Setting.RuntimeMovementHeld, "Forced status removal retained movement hold.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
