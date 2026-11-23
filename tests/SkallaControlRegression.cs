using System.Numerics;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

internal static partial class FollowRegression
{
    internal static void RunSkallaControls(Action<string, Action> run)
    {
        run("Skalla real PR hints ban invincible target stop native attack publish gaze and release", () =>
        {
            using var scope = new FollowScope();
            var replay = false; ulong[] forbidden = []; float? heading = null; var stopped = 0;
            var control = new MechanicFacingControl((_, _, _, targets, face, _, _, _) =>
            {
                forbidden = targets.ToArray(); heading = face;
            }, _ => { forbidden = []; heading = null; });
            var hints = new ScriptHintsApi(() => replay, control: control,
                stopAutoAttack: (_, targets) => { if (targets.Contains(1UL)) ++stopped; });
            try
            {
                var now = DateTime.UtcNow; var e = new DungeonEncounter(1172);
                DungeonActor player = new(100, 0, new(115, 0, 4), PartySlot: 0, Hostile: false);
                DungeonActor boss = new(1, 0x1FAC, player.Position, InCombat: true, Statuses: [325]);
                e.Synchronize([boss, player], player.Position, now, playerId: 100);
                hints.SetActionRestrictions(forbiddenTargets: e.ForbiddenTargets);
                Check(forbidden.SequenceEqual([1UL]) && stopped == 1, "Skalla invincibility did not reach PR/native autoattack.");
                e.StatusChanged(1, 325, false, now); hints.SetActionRestrictions(forbiddenTargets: e.ForbiddenTargets);
                Check(forbidden.Length == 0, "Skalla invincibility removal retained PR restriction.");
                boss = new(1, 0x1FAE, new(479, 0, 4), InCombat: true);
                player = player with { Position = boss.Position - Vector3.UnitZ * 8 };
                e.ResetFight(); e.Synchronize([boss, player], player.Position, now, playerId: 100);
                e.StartCast(1, new(9829, boss.Position, 0, now, now.AddSeconds(3.3)), now);
                e.Tick(now.AddSeconds(3)); hints.SetFacing(e.GazeFacing(player.Position, 0));
                Check(heading != null, "Skalla gaze did not reach PR.");
                replay = true; hints.SetFacing(e.GazeFacing(player.Position, 0));
                Check(heading == null && forbidden.Length == 0 && stopped == 1, "Replay executed or retained PR controls.");
                replay = false; scope.Settings.EnableSmartMovement = false;
                hints.SetActionRestrictions(forbiddenTargets: [1]); hints.SetFacing(1);
                Check(heading == null && forbidden.Length == 0 && stopped == 1, "Disabled movement executed PR controls.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
