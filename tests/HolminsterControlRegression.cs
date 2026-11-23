using System.Numerics;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

internal static partial class FollowRegression
{
    internal static void RunHolminsterControls(Action<string, Action> run)
    {
        run("Holminster real PR fetter rescue ban native autoattack target victim and replay controls", () =>
        {
            using var scope = new FollowScope();
            var replay = false; ulong[] forbidden = []; var stopped = 0; ulong selected = 0;
            var control = new MechanicFacingControl((_, target, _, targets, _, _, _, _) =>
            {
                forbidden = targets.ToArray(); selected = target;
            }, _ => { forbidden = []; selected = 0; });
            var hints = new ScriptHintsApi(() => replay, control: control,
                stopAutoAttack: (_, targets) => { if (targets.Contains(1UL)) ++stopped; });
            try
            {
                var now = DateTime.UtcNow; var encounter = new DungeonEncounter(837); var center = new Vector3(134, 0, -465);
                DungeonActor[] actors = [new(1, 0x278C, center, InCombat: true), new(10, 0x2895, center + Vector3.UnitZ * 8),
                    new(100, 0, center, PartySlot: 0, Hostile: false), new(101, 0, center + Vector3.UnitZ * 8,
                        PartySlot: 1, Hostile: false, Statuses: [1849])];
                encounter.Synchronize(actors, center, now, playerId: 100); encounter.Icon(101, 92, now);
                encounter.StatusChanged(101, 1849, true, now);
                hints.SetActionRestrictions(forbiddenTargets: encounter.ForbiddenTargets);
                hints.SetTarget(encounter.PriorityTarget(1, center));
                Check(forbidden.SequenceEqual([1UL]) && stopped >= 1 && selected == 10, "Rescue did not publish PR target ban/native autoattack stop.");
                encounter.Synchronize(actors, center, now, playerId: 101);
                hints.SetActionRestrictions(forbiddenTargets: encounter.ForbiddenTargets);
                Check(forbidden.Length == 0, "Victim retained rescuer restriction.");
                replay = true; hints.SetActionRestrictions(forbiddenTargets: [1]); hints.SetTarget(10);
                Check(forbidden.Length == 0 && selected == 0, "Replay executed PR rescue controls.");
            }
            finally { hints.Cleanup(); }
        });
    }
}
