using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static void FallenIcons()
    {
        var (e, boss, player) = BekkoOrcus(722);
        var center = boss.Position;
        DungeonActor npc = new(2, 0x1CAD, center + Vector3.UnitX * 8, FriendlyNpc: true, IsEnemy: false);
        e.Synchronize([boss, player, npc], center, Start, playerId: 100);
        e.Tick(Start.AddSeconds(10));
        e.Icon(100, 71, Start.AddSeconds(-5), 2);
        e.Icon(100, 71, Start.AddSeconds(-5), 2);
        e.Icon(2, 62, Start.AddSeconds(10), 100);
        var spread = e.AmbientAoes().Where(a => a.Id.StartsWith("fallen/spread/") && a.Display).ToArray();
        Check(spread.Length == 2 && spread.All(a => MathF.Abs(a.Position.X - npc.Position.X) < .05f
            && a.Shape is CircleTemplate { Radius: 10 }), "Icon source actor identity or repeated marks lost.");
        Check(e.AmbientAoes().Where(a => a.Id.Contains("/avoid")).All(a => a.Shape is CircleTemplate { Radius: 11 }
            && a.Activation == Start.AddSeconds(10).AddSeconds(5.15f)), "Spread AI radius or consumed icon clock.");
        var damage = e.PredictedDamage.Single();
        Check(damage.TargetId == 2 && damage.PartySlot == 24 && damage.DamageType == "Raidwide"
            && damage.Activation == Start.AddSeconds(10).AddSeconds(5.15f), "NPC spread prediction or extra Shared prediction.");
        var buddy = e.AmbientAoes().Single(a => a.Id.EndsWith("/buddy"));
        Check(!buddy.Shape.Resolve(buddy.Position, 0, null).Contains(npc.Position),
            "Independent Cure III component excluded Cure V buddy.");
        e.Tick(Start.AddSeconds(100));
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("fallen/spread/") && a.Display) == 2, "Icon activation became expiry.");
        e.Effect(999, 8636, 1, Start.AddSeconds(100), target: 2);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("fallen/spread/") && a.Display) == 1, "Effect removed multiple identical-target markers.");
        e.Effect(999, 8636, 2, Start.AddSeconds(101), target: 999);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fallen/spread/")), "Unmatched effect failed to remove first marker.");
        e.Effect(999, 8637, 3, Start.AddSeconds(102), target: 999);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fallen/")), "Stack fallback effect retained mark.");
    }

    private static void FallenLifetimes()
    {
        var (e, boss, player) = BekkoOrcus(722);
        var center = boss.Position;
        DungeonActor npc = new(2, 0x1CAD, center + Vector3.UnitX * 10, FriendlyNpc: true, IsEnemy: false);
        e.Synchronize([boss, player, npc], center, Start, playerId: 100);
        e.Icon(2, 71);
        e.Icon(2, 62);
        e.Synchronize([boss, player, npc with { Dead = true }], center, Start.AddSeconds(1), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fallen/")), "Dead stack target was drawn.");
        e.Synchronize([boss, player, npc], center, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("fallen/") && a.Display) == 1
            && e.AmbientAoes().Any(a => a.Id.StartsWith("fallen/stack/")), "Spread revived or stack failed to return.");
        e.RemoveActor(2);
        e.Synchronize([boss, player, npc with { Position = center + Vector3.UnitZ * 10 }], center, Start.AddSeconds(3), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("fallen/") && a.Display).Position.X > center.X + 9.9f,
            "Destroyed icon target rebound to a new actor sharing its ID.");
        var (solo, soloBoss, soloPlayer) = BekkoOrcus(722);
        solo.Icon(100, 71);
        Check(solo.AmbientAoes().Count(a => a.Id.StartsWith("fallen/")) == 1 && solo.PredictedDamage.Single().TargetId == 100,
            "Solo spread should display/predict without navigation.");
        solo.Synchronize([soloBoss with { Dead = true }, soloPlayer], center, Start.AddSeconds(1), playerId: 100);
        Check(!solo.AmbientAoes().Any() && !solo.PredictedDamage.Any(), "Fallen primary death retained icon state.");
    }

    private static void FallenTargets()
    {
        var (e, boss, player) = BekkoOrcus(722);
        boss = boss with { IsEnemy = true, ObjectIndex = 2, Statuses = [325] };
        DungeonActor add = new(2, 0x18D6, boss.Position, IsEnemy: true, ObjectIndex: 4);
        DungeonActor invalid = new(3, 0x1CB4, boss.Position, IsEnemy: true, ObjectIndex: 5);
        e.Synchronize([boss, player, add, invalid], boss.Position, Start, playerId: 100);
        Check(e.PriorityTarget(1, boss.Position) == 2 && e.ForbiddenTargets.Length == 0, "Invincibility did not exclude primary or forbade incidental AOE.");
        e.StatusChanged(2, 325, true, Start.AddSeconds(1));
        Check(e.PriorityTarget(1, boss.Position) == 0 && e.TargetToClear(1) == 1 && e.TargetToClear(0) == 1
            && e.ForbiddenTargets.Length == 0 && !e.RequireAllStop, "No positive target released selector ownership or stopped healing.");
        e.StatusChanged(1, 325, false, Start.AddSeconds(2));
        Check(e.PriorityTarget(2, boss.Position) == 1, "Invincibility removal did not restore selection.");
        e.StatusChanged(1, 325, true, Start.AddSeconds(2));
        e.Synchronize([boss, player, add with { Hp = 100, IncomingEffects = [new(0, 1, 0, false, false) { HpDelta = -99 }] }],
            boss.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(1, boss.Position) == 0, "Pending lethal damage failed to preserve source pointless priority.");
        e.Synchronize([boss, player, add with { Hp = 1 }], boss.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(1, boss.Position) == 0, "HP1 target was selected despite source pointless priority.");
        e.Synchronize([boss, player, add with { Hp = 1, Position = boss.Position + Vector3.UnitY * 13 }],
            boss.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(1, boss.Position) == 2, "Fallen override failed to retain the base height-priority ordering.");
        e.StatusChanged(1, 325, false, Start.AddSeconds(2));
        e.StartCast(1, new(8635, boss.Position, 0, Start, Start.AddSeconds(5)), Start.AddSeconds(2));
        Check(e.OutputCasts.Single().Value.Rule.Shape is CircleTemplate { Radius: 12 }, "Cure IV incorrectly used enum comment radius.");
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("job-quest/enemy/")) == 1, "Fallen default drawing included helpers.");
        e.Synchronize([boss with { Dead = true }, player, add], boss.Position, Start.AddSeconds(3), playerId: 100);
        Check(e.PriorityTarget(2, boss.Position) == 0 && e.TargetToClear(2) == 0, "Ended Fallen module retained custom target selection.");
        var (reuse, reuseBoss, reusePlayer) = BekkoOrcus(722);
        DungeonActor npc = new(2, 0x1CAD, reuseBoss.Position + Vector3.UnitX * 15, FriendlyNpc: true, IsEnemy: false);
        reuse.Synchronize([reuseBoss, reusePlayer, npc], reuseBoss.Position, Start, playerId: 100);
        reuse.Icon(100, 71);
        reuse.Icon(2, 71);
        reuse.RemoveActor(2);
        reuse.Synchronize([reuseBoss, reusePlayer, npc with { Position = reuseBoss.Position + Vector3.UnitX * 2 }],
            reuseBoss.Position, Start.AddSeconds(1), playerId: 100);
        Check(reuse.AmbientAoes().Any(a => a.Id.EndsWith("/separate/2")),
            "Frozen old mark incorrectly excluded the new same-ID party actor from spread avoidance.");
        reuse.Effect(999, 8636, 1, Start.AddSeconds(2), target: 100);
        reuse.Effect(999, 8636, 2, Start.AddSeconds(2), target: 2);
        reuse.Icon(2, 62);
        reuse.RemoveActor(2);
        reusePlayer = reusePlayer with { Position = reuseBoss.Position + Vector3.UnitX * 15 };
        reuse.Synchronize([reuseBoss, reusePlayer, npc with { Position = reuseBoss.Position + Vector3.UnitX * 2 }],
            reusePlayer.Position, Start.AddSeconds(3), playerId: 100);
        Check(!reuse.Hints(reusePlayer.Position).Contains("Stack!"),
            "A new same-ID member was excluded from the old actor's stack occupancy.");
    }
}
