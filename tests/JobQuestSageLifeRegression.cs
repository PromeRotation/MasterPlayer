using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static (DungeonEncounter Encounter, DungeonActor Boss, DungeonActor Player) SageLife(uint oid)
    {
        var center = JobQuestCatalog.Create(1023).Single(d => d.BossOid == oid).Arena(Vector3.Zero).Position;
        var e = new DungeonEncounter(1023);
        DungeonActor boss = new(1, oid, center, InCombat: true);
        DungeonActor player = new(100, 0, center, PartySlot: 0, Player: true);
        e.Synchronize([boss, player], center, Start, playerId: 100);
        return (e, boss, player);
    }

    private static void SageLifeRocks()
    {
        var (e, boss, player) = SageLife(0x35C5);
        var center = boss.Position;
        DungeonCastObservation Cast(uint action, double t, Vector3? position = null)
            => new(action, position ?? center, 0, Start.AddSeconds(t), Start.AddSeconds(t + 5));
        e.StartCast(1, Cast(26832, 0), Start);
        e.Finish(1, 26832, Start.AddSeconds(1));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("sage/rock/")).Shape is CircleTemplate { Radius: 10 },
            "Visual Rockbreaker finish removed the actual circle.");
        e.Synchronize([boss, player, new(2, 0x233C, center, Cast: Cast(26833, 2))], center, Start.AddSeconds(2), playerId: 100);
        e.Tick(Start.AddSeconds(50));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("sage/rock/")).Activation == Start.AddSeconds(5),
            "Rockbreaker expired or advanced from time.");
        e.Finish(2, 26833, Start.AddSeconds(50), completed: false);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("sage/rock/")) is { Shape: DonutTemplate { InnerRadius: 10, OuterRadius: 20 } } ring
            && ring.Activation == Start.AddSeconds(52), "Helper cancellation failed to advance at consumed time plus two.");
        e.Synchronize([boss, player, new(3, 0x233C, center, Cast: Cast(26834, 51, center + new Vector3(.9f, 0, .9f)))],
            center, Start.AddSeconds(51), playerId: 100);
        e.RemoveActor(3);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("sage/rock/")), "Second ring destruction did not finish sequence.");
        e.Synchronize([boss, player, new(4, 0x233C, center, Cast: Cast(26833, 52))], center, Start.AddSeconds(52), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("sage/rock/")), "Actual helper circle invented missing visual history.");
    }

    private static void SageLifeTowers()
    {
        var (e, boss, player) = SageLife(0x35C5);
        var center = boss.Position;
        var a = center + Vector3.UnitX * 8;
        var b = center - Vector3.UnitX * 8;
        player = player with { Position = center + Vector3.UnitZ * 10 };
        DungeonActor npc = new(10, 0x3589, a, FriendlyNpc: true, IsEnemy: false);
        DungeonActor first = new(2, 0x233C, a, Cast: new(26844, a, 0, Start, Start.AddSeconds(8)));
        DungeonActor second = new(3, 0x233C, b, Cast: new(26844, b, 0, Start, Start.AddSeconds(9)));
        void Sync(double t) => e.Synchronize([boss, player, npc, first, second], player.Position, Start.AddSeconds(t), playerId: 100);
        Sync(0);
        var towers = e.AmbientAoes().Where(x => x.Id.StartsWith("sage/tower/")).ToArray();
        Check(towers.Single(x => x.Id == "sage/tower/2/display") is { Safe: false, OutlineOnly: false }
            && towers.Single(x => x.Id == "sage/tower/3/display") is { Safe: true, OutlineOnly: true },
            "NPC tower occupancy did not control safe-outline versus dangerous-fill display.");
        var enter = towers.Single(x => x.Id == "sage/tower/enter");
        var geometry = enter.Shape.Resolve(enter.Position, 0, null);
        Check(geometry.Contains(a) && !geometry.Contains(b) && towers.All(x => !x.Id.EndsWith("/avoid")),
            "Empty tower branch combined hard entry with forbidden full towers.");
        Check(e.PredictedDamage.Single() is { TargetId: 10, PartySlot: 24, DamageType: "Raidwide" },
            "Tower damage mask omitted NPC or included players outside.");
        npc = npc with { Position = center + Vector3.UnitZ * 12 };
        Sync(1);
        enter = e.AmbientAoes().Single(x => x.Id == "sage/tower/enter");
        geometry = enter.Shape.Resolve(enter.Position, 0, null);
        Check(!geometry.Contains(a) && !geometry.Contains(b), "Two empty towers did not offer either circle.");
        npc = npc with { Position = a };
        player = player with { Position = a };
        Sync(2);
        Check(e.Hints(a).Contains("Too many soakers in the tower!") && e.PredictedDamage.Count() == 2
            && e.AmbientAoes().Single(x => x.Id == "sage/tower/2/display") is { Safe: false, OutlineOnly: true },
            "Tower oversoak warning, mask or outline.");
        e.Finish(2, 26844, Start.AddSeconds(2));
        Check(e.AmbientAoes().All(x => !x.Id.StartsWith("sage/tower/2/")), "Tower finish removed wrong caster.");
        e.Finish(3, 26844, Start.AddSeconds(2));
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("sage/tower/")) && !e.PredictedDamage.Any(), "Tower cleanup.");
    }

    private static void SageLifeParty()
    {
        foreach (var (oid, stackAction, spreadAction, radius) in new (uint, uint, uint, float)[]
            { (0x35C5, 26847, 26848, 6), (0x35C6, 26860, 26867, 5) })
        {
            var (e, boss, player) = SageLife(oid);
            DungeonActor npc = new(10, 0x3589, boss.Position + Vector3.UnitX * 8, FriendlyNpc: true, IsEnemy: false);
            e.Synchronize([boss, player, npc, new(2, 0x233C, boss.Position)], boss.Position, Start, playerId: 100);
            var cast = new DungeonCastObservation(spreadAction, boss.Position, 0, Start, Start.AddSeconds(8), 10);
            e.StartCast(2, cast, Start);
            Check(e.AmbientAoes().Single(x => x.Id.EndsWith("/display") && x.Id.StartsWith("quest/spread/")).Shape is CircleTemplate r
                && r.Radius == radius && !e.PredictedDamage.Any(), "Sage Life spread radius or fabricated prediction.");
            Check(e.AmbientAoes().Single(x => x.Id.EndsWith("/avoid")).Shape is CircleTemplate ai && ai.Radius == radius + 1,
                "Sage Life AI spread radius.");
            e.StartCast(2, cast with { TargetId = 100 }, Start.AddSeconds(.1));
            e.StartCast(2, cast, Start.AddSeconds(.2));
            Check(e.AmbientAoes().Single(x => x.Id.EndsWith("/display") && x.Id.StartsWith("quest/spread/")).Position.X > boss.Position.X + 7.9f,
                "Repeated same-generation target correction blocked.");
            e.Finish(2, spreadAction, Start.AddSeconds(.3));
            e.StartCast(2, new(stackAction, boss.Position, 0, Start.AddSeconds(1), Start.AddSeconds(9), 10), Start.AddSeconds(1));
            Check(e.AmbientAoes().Single(x => x.Id.EndsWith("/display") && x.Id.StartsWith("quest/stack/")).Shape is CircleTemplate { Radius: 6 }
                && !e.PredictedDamage.Any(), "Sage Life stack radius or fabricated Shared prediction.");
            e.Finish(2, stackAction, Start.AddSeconds(2));
            Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("quest/")), "Sage Life actual stack finish retained Ompagne persistence.");
        }
    }

    private static void SageLifeGuildivain()
    {
        var (e, boss, player) = SageLife(0x35C6);
        var center = boss.Position;
        DungeonActor source = new(2, 0x35C8, center - Vector3.UnitX * 10);
        DungeonActor other = source with { Id = 3 };
        DungeonActor npc = new(10, 0x3589, center, FriendlyNpc: true, IsEnemy: false);
        DungeonActor state = new(50, 0x1EA1A1, center, EventState: 7);
        void Sync(double t) => e.Synchronize([boss, player, source, other, npc, state], player.Position, Start.AddSeconds(t), playerId: 100);
        Sync(0);
        e.Tether(10, 17, true, Start, 2);
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("guildivain/bait/")), "Friendly NPC was incorrectly classified as Player/Buddy.");
        e.Tether(10, 17, false, Start, 2);
        e.Tether(100, 17, true, Start, 2);
        e.Tether(100, 17, true, Start, 2);
        e.Tether(100, 17, true, Start, 3);
        Check(e.AmbientAoes().Count(x => x.Id.StartsWith("guildivain/bait/") && x.Id.EndsWith("/display")) == 2
            && e.PredictedDamage.Single().DamageType == "Tankbuster", "Tether deduplication or target mask.");
        Check(e.Hints(player.Position).Contains("Bait away from raid!")
            && e.AmbientAoes().Count(x => x.Id.Contains("/away/10")) == 2, "Own bait did not avoid NPC party.");
        player = player with { Position = center + Vector3.UnitZ * 10 };
        Sync(1);
        Check(e.AmbientAoes().Where(x => x.Id.EndsWith("/display") && x.Id.StartsWith("guildivain/"))
            .All(x => MathF.Abs(x.Rotation - MathF.PI / 4) < .001f), "Tether did not follow target or was erased by unchanged old snapshot.");
        e.Effect(999, 26853, 1, Start.AddSeconds(2), target: 100);
        source = source with { Tethers = new Dictionary<uint, ulong> { [17] = 100 } };
        Sync(3);
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("guildivain/")), "Delayed tether snapshot resurrected effect-consumed baits.");
        e.Tether(2, 17, true, Start.AddSeconds(3), 100);
        Check(e.AmbientAoes().Any(x => x.Id.EndsWith("/line")) && e.PredictedDamage.Single().Activation == Start,
            "Reverse tether or shared first-wave activation lost.");
        e.Tether(2, 17, false, Start.AddSeconds(3), 100);
        e.Tether(100, 174, true, Start.AddSeconds(3), 2);
        Check(!e.PredictedDamage.Any(), "Untether/unused174 handling.");
        Check(e.Arena!.Contains(center + Vector3.UnitZ * 15), "Guildivain initial large arena.");
        e.Effect(1, 26854, 2, Start.AddSeconds(4));
        Check(!e.Arena!.Contains(center + Vector3.UnitZ * 15), "Tracheostomy effect failed to shrink.");
        Sync(5);
        Check(!e.Arena!.Contains(center + Vector3.UnitZ * 15), "Unchanged old EventState7 incorrectly restored arena.");
        e.EventState(50, 6);
        e.EventState(50, 7);
        Check(!e.Arena!.Contains(center + Vector3.UnitZ * 15), "Network EventObjectStateChange incorrectly restored native arena.");
        state = state with { EventState = 6 };
        Sync(5.1);
        state = state with { EventState = 7 };
        Sync(5.2);
        Check(e.Arena!.Contains(center + Vector3.UnitZ * 15), "New EventState7 failed to restore.");
        e.Effect(1, 26854, 3, Start.AddSeconds(6));
        Check(!e.Arena!.Contains(center + Vector3.UnitZ * 15), "Second shrink failed.");
        source = source with { Tethers = null };
        Sync(7);
        e.Tether(100, 17, true, Start.AddSeconds(7), 2);
        e.Synchronize([boss, player, npc], player.Position, Start.AddSeconds(8), playerId: 100);
        e.RemoveActor(2, Start.AddSeconds(8));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("guildivain/")) && !e.PredictedDamage.Any(),
            "Packet-only outgoing tether survived a removal frame preceding ObjectRemove.");
    }
}
