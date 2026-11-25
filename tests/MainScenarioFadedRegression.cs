using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static void FadedThordanControls()
    {
        var (e, boss, player) = MainScenario(932, 0x2F1D);
        Check(e.Arena is CircleGeometry { Radius: 20 } && e.Arena.Position == new Vector3(-247, 0, 321),
            "Thordan arena differs from the source circle.");
        var eye = player.Position + Vector3.UnitZ * 10;
        var cast = new DungeonCastObservation(21090, eye, 0, Start, Start.AddSeconds(4.3));
        e.StartCast(1, cast, Start);
        Check(e.Gazes.Single().Location == eye && e.GazeFacing(player.Position, 0) == null,
            "Thordan gaze range hint created early facing or lost locked eye.");
        e.Tick(Start.AddSeconds(3.6));
        Check(e.GazeFacing(player.Position, 0) is { } facing && MathF.Cos(facing) < .7f
            && e.GazeFacing(eye - Vector3.UnitZ * 10001, 0) == null,
            "Thordan default range10000/45-degree forbidden facing not preserved.");
        var symbols = e.AmbientAoes().Where(a => a.Id.StartsWith("thordan/gaze/")).ToArray();
        Check(symbols.Length == 3 && symbols.All(a => !a.Navigate) && e.Hints(player.Position).Contains("Turn away from gaze!"),
            "Thordan gaze eye/pupil/view-arc or personal hint missing.");
        boss = boss with { Position = boss.Position + Vector3.UnitX * 5, Rotation = 2, Cast = cast };
        e.Synchronize([boss, player], player.Position, Start.AddSeconds(3.7), playerId: 100);
        Check(e.Gazes.Single().Location == eye, "Thordan gaze followed moving caster.");
        e.Finish(1, 21090, Start.AddSeconds(3.8));
        Check(e.GazeFacing(player.Position, 0) == null && !e.AmbientAoes().Any(a => a.Id.StartsWith("thordan/gaze/")),
            "Thordan actual gaze finish leaked facing or previews.");

        DungeonActor other = new(2, 0xFFFF, player.Position, IsEnemy: true, ObjectIndex: 4, Hp: 100);
        boss = boss with { Statuses = [671] };
        e.Synchronize([boss, player, other], player.Position, Start.AddSeconds(4), playerId: 100);
        Check(e.PriorityTarget(1, player.Position) == 2 && e.ForbiddenTargets.Length == 0,
            "Thordan changed invincible priority into hard forbidden or filtered passive legal candidates.");
        other = other with { Hp = 1 };
        e.Synchronize([boss, player, other], player.Position, Start.AddSeconds(4.1), playerId: 100);
        Check(e.PriorityTarget(2, player.Position) == 0, "Thordan resurrected Pointless pending-dead target.");
        other = other with { Position = player.Position + Vector3.UnitY * 13 };
        e.Synchronize([boss, player, other], player.Position, Start.AddSeconds(4.2), playerId: 100);
        Check(e.PriorityTarget(1, player.Position) == 2, "Thordan failed to overwrite height priority before PendingDead.");
        e.StatusChanged(2, 671, true, Start.AddSeconds(4.3));
        Check(e.PriorityTarget(2, player.Position) == 0 && e.ForbiddenTargets.Length == 0 && e.TargetToClear(0) == 1,
            "Thordan status overlay failed selector hold or invented incidental AOE ban.");
        e.StatusChanged(1, 671, false, Start.AddSeconds(4.4));
        Check(e.PriorityTarget(2, player.Position) == 1, "Thordan invincibility loss failed to restore priority.");
    }

    private static void FadedZenosLifecycle()
    {
        var center = new Vector3(-321.03f, 0, 617.73f);
        DungeonActor boss = new(1, 0x2F28, center, InCombat: true, Hp: 1000);
        DungeonActor player = new(100, 0, center, Player: true, PartySlot: 0);
        var e = new DungeonEncounter(932);
        e.Synchronize([boss, player], center, Start, playerId: 100);
        Check(e.Active == null, "Zenos primary combat substituted the sword pull predicate.");
        boss = boss with { InCombat = false, Targetable = false };
        DungeonActor sword = new(2, 0x2F2B, center, InCombat: true, Dead: true, Targetable: false);
        e.Synchronize([boss, player, sword], center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active?.BossOid == 0x2F28 && e.Arena is CircleGeometry { Radius: 20 } && e.Arena.Position == center,
            "Zenos any-sword combat pull incorrectly required living/targetable swords or primary.");
        sword = sword with { Dead = false, Targetable = true };
        DungeonActor second = new(3, 0x2F2A, center + Vector3.UnitX, Rotation: 1);
        DungeonActor third = new(4, 0x2F2C, center - Vector3.UnitX, Rotation: 2);
        DungeonActor specter = new(5, 0x2F29, center);
        e.Synchronize([boss, player, sword, second, third, specter], center, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Where(a => a.Id.StartsWith("job-quest/enemy/")).Select(a => a.Id).Order()
            .SequenceEqual(new[] { "job-quest/enemy/2", "job-quest/enemy/3", "job-quest/enemy/4" }), "Zenos drew primary or specter instead of exactly three swords.");
        e.Synchronize([boss, player], center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active?.BossOid == 0x2F28, "Zenos ended when swords disappeared.");
        e.Synchronize([boss with { Hp = 0 }, player, sword], center, Start.AddSeconds(4), playerId: 100);
        Check(e.Active == null, "Zenos HP0 primary restarted from surviving combat sword.");
    }

    private static void FadedCastRules()
    {
        foreach (var (oid, action, range, width) in new (uint, uint, float, float)[]
        {
            (0x2F1D, 21086, 45, 8), (0x2F28, 21117, 50, 8), (0x2F28, 21118, 10, 0)
        })
        {
            var (e, boss, player) = MainScenario(932, oid == 0x2F28 ? 0x2F1D : oid);
            if (oid == 0x2F28)
            {
                e.ResetFight();
                var center = new Vector3(-321.03f, 0, 617.73f);
                boss = boss with { Oid = oid, Position = center, InCombat = false, Targetable = false };
                player = player with { Position = center };
                e.Synchronize([boss, player, new(2, 0x2F2A, center, InCombat: true)], center, Start, playerId: 100);
            }
            var location = boss.Position + Vector3.UnitX * 5;
            DungeonActor helper = new(10, 0x233C, boss.Position, Cast: new(action, location, MathF.PI / 2, Start, Start.AddSeconds(5.3)));
            e.Synchronize([boss, player, helper], player.Position, Start, playerId: 100);
            var pending = e.OutputCasts.Single().Value;
            Check(pending.Rule.Shape is CircleTemplate circle ? circle.Radius == range
                : pending.Rule.Shape is RayRectangleTemplate rectangle && rectangle.Length == range
                    && rectangle.MinLocalX == -width / 2 && rectangle.MaxLocalX == width / 2,
                "Faded cast exact geometry mismatch.");
            e.Effect(10, action, 1, Start.AddSeconds(6));
            e.Tick(Start.AddSeconds(50));
            Check(e.OutputCasts.Count() == 1 && e.OutputCasts.Single().Value.Location == location,
                "Faded cast expired by effect/timeout.");
            e.Synchronize([boss, player, helper with { Cast = null }], player.Position, Start.AddSeconds(51), playerId: 100);
            Check(!e.OutputCasts.Any(), "Faded real observed finish failed cleanup.");
        }
        var zenos = new DungeonEncounter(932);
        var origin = new Vector3(-321.03f, 0, 617.73f);
        DungeonActor primary = new(1, 0x2F28, origin, Hp: 100);
        DungeonActor caster = new(100, 0, origin, Player: true, PartySlot: 0,
            Cast: new(21121, origin, 0, Start, Start.AddSeconds(7.3)));
        zenos.Synchronize([primary, caster, new(2, 0x2F2C, origin, InCombat: true),
            new(101, 0, origin, Player: true, PartySlot: 8), new(102, 0x123, origin, FriendlyNpc: true)],
            origin, Start, playerId: 100);
        Check(zenos.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 101, 102 })
            && zenos.OutputCasts.Single().Value.Rule.Shape == null, "Player FinalArt caster or source full party mask was filtered.");
        zenos.Finish(100, 21121, Start.AddSeconds(1));
        Check(!zenos.PredictedDamage.Any(), "FinalArt actual finish retained damage.");
    }
}
