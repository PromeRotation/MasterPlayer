using System.Numerics;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

internal static partial class FollowRegression
{
    internal static void RunDohnControls(Action<string, Action> run)
    {
        run("DohnMheg PR gaze lyre distance ban native attack replay and movement disable", () =>
        {
            using var scope = new FollowScope();
            var replay = false; ulong[] forbidden = []; float? heading = null; var stopped = 0;
            var control = new MechanicFacingControl((_, _, _, targets, face, _, _, _) =>
            { forbidden = targets; heading = face; }, _ => { forbidden = []; heading = null; });
            var hints = new ScriptHintsApi(() => replay, control: control,
                stopAutoAttack: (_, targets) => { if (targets.Contains(10UL)) ++stopped; });
            try
            {
                var now = DateTime.UtcNow; var center = new Vector3(-128.5f, 0, -244);
                var encounter = new DungeonEncounter(821);
                DungeonActor[] actors = [new(1, 0xF14, center, InCombat: true),
                    new(10, 0xF63, center + Vector3.UnitZ * 10), new(100, 0, center - Vector3.UnitZ * 8, PartySlot: 0, Hostile: false)];
                encounter.Synchronize(actors, actors[2].Position, now, playerId: 100);
                encounter.StartCast(1, new(13552, center, 0, now, now.AddSeconds(4.3)), now);
                encounter.Tick(now.AddSeconds(4));
                hints.SetActionRestrictions(forbiddenTargets: encounter.ForbiddenTargets);
                hints.SetFacing(encounter.GazeFacing(actors[2].Position, 0));
                Check(forbidden.SequenceEqual([10UL]) && stopped >= 1 && heading != null && MathF.Cos(heading.Value) < .72,
                    "Gaze/target restrictions did not reach PR.");
                replay = true; hints.SetFacing(1); Check(heading == null && forbidden.Length == 0, "Replay retained control.");
                replay = false; scope.Settings.EnableSmartMovement = false; hints.SetActionRestrictions(forbiddenTargets: [10]);
                Check(forbidden.Length == 0 && heading == null, "Disabled movement executed restriction.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
