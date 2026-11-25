using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;

internal static class GraoullyRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-515, 0, -305);

    internal static void Run(Action<string, Action> run)
    {
        run("HW Graoully exact rules arena and observed cast lifetimes", Casts);
        run("HW Graoully latest Prey survives late old loss and exact three low-byte stacks swap", Prey);
        run("HW Graoully personal partner indices and untargetable boss preserve separate navigation and drawing", Perspective);
        run("HW Graoully NPC slots preserve holes and current prey destruction never revives an old holder", SlotsAndRemoval);
    }

    private static void Casts()
    {
        var definition = HeavenswardIntroCatalog.Create(477).Single();
        Check(definition.BossOid == 0x10B9 && definition.Casts.Select(c => c.Action).SequenceEqual(new uint[] { 3095, 4259, 4260, 4261 }), "Wrong Graoully rule set.");
        var e = new DungeonEncounter(477);
        DungeonActor boss = new(1, 0x10B9, Center, InCombat: true);
        DungeonActor player = new(100, 0, Center, PartySlot: 0);
        e.Synchronize([boss, player], Center, Start, playerId: 100);
        foreach (var rule in definition.Casts)
        {
            var cast = new DungeonCastObservation(rule.Action, Center, 0, Start, Start.AddSeconds(3));
            var source = boss with { Cast = cast };
            e.Synchronize([source, player], Center, Start, playerId: 100);
            e.StartCast(1, cast, Start);
            e.Tick(Start.AddSeconds(30));
            Check(e.OutputCasts.Single().Value.UntilObservedEnd, "Graoully cast expired by predicted clock.");
            e.Synchronize([boss, player], Center, Start.AddSeconds(31), playerId: 100);
            Check(!e.OutputCasts.Any(), "Observed Graoully cast finish leaked output.");
            e.ResetFight();
            e.Synchronize([boss, player], Center, Start, playerId: 100);
        }
        var shortCast = new DungeonCastObservation(4261, Center, 0, Start, Start.AddSeconds(3));
        e.Synchronize([boss with { Cast = shortCast }, player], Center, Start, playerId: 100);
        e.Synchronize([boss, player], Center, Start.AddSeconds(.1), playerId: 100);
        Check(!e.OutputCasts.Any(), "Already observed cast cancellation retained the packet-only grace period.");
        e.ResetFight();
        e.Synchronize([boss with { Cast = shortCast }, player], Center, Start, playerId: 100);
        var nextCast = shortCast with { StartedAt = Start.AddSeconds(1), FinishAt = Start.AddSeconds(4) };
        e.StartCast(1, nextCast, Start.AddSeconds(1));
        Check(!e.OutputCasts.Single().Value.Observed, "A replacement packet inherited the former cast's observation.");
        e.Synchronize([boss with { Cast = nextCast }, player], Center, Start.AddSeconds(1.05), playerId: 100);
        e.Synchronize([boss, player], Center, Start.AddSeconds(1.1), playerId: 100);
        Check(!e.OutputCasts.Any(), "The former finish record blocked an accepted replacement's observation.");
        e.ResetFight();
        e.Synchronize([boss with { Cast = shortCast }, player], Center, Start, playerId: 100);
        e.Finish(1, 4261, Start.AddSeconds(.05));
        nextCast = shortCast with { StartedAt = Start.AddSeconds(.1), FinishAt = Start.AddSeconds(3.1) };
        e.StartCast(1, nextCast, Start.AddSeconds(.1));
        e.Synchronize([boss with { Cast = shortCast }, player], Center, Start.AddSeconds(.12), playerId: 100);
        e.Synchronize([boss, player], Center, Start.AddSeconds(.15), playerId: 100);
        Check(e.OutputCasts.Single().Value.StartedAt == nextCast.StartedAt && !e.OutputCasts.Single().Value.Observed,
            "A closed prior generation within the timing tolerance contaminated the new packet's observation.");
        e.Finish(1, 4261, Start.AddSeconds(.2));
        var different = new DungeonCastObservation(4259, Center, 0, Start.AddSeconds(1), Start.AddSeconds(4));
        e.StartCast(1, different, Start.AddSeconds(1));
        e.Finish(1, 4259, Start.AddSeconds(1.05));
        e.Synchronize([boss with { Cast = shortCast }, player], Center, Start.AddSeconds(1.1), playerId: 100);
        Check(!e.OutputCasts.Any(), "An intervening different action erased the closed-cast watermark.");
        e.Tick(Start.AddSeconds(30));
        e.Synchronize([boss with { Cast = shortCast }, player], Center, Start.AddSeconds(31), playerId: 100);
        Check(!e.OutputCasts.Any(), "Time alone expired the closed-cast watermark and revived a stale observation.");
        Check(e.Arena!.Contains(Center) && !e.Arena.Contains(Center + Vector3.UnitX * 40), "Graoully quadrilateral missing.");
    }

    private static void Prey()
    {
        var e = new DungeonEncounter(477);
        DungeonActor boss = new(1, 0x10B9, Center, InCombat: true);
        DungeonActor player = new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0);
        DungeonActor buddy = new(200, 0x10BB, Center + Vector3.UnitX * 10, Hostile: false, FriendlyNpc: true);
        void Sync(double t) => e.Synchronize([boss, player, buddy], player.Position, Start.AddSeconds(t), playerId: 100);
        Sync(0);
        e.StatusChanged(200, 904, true, Start);
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("graoully/")).ToArray();
        Check(aoes.Length == 3 && aoes.Count(a => a.Navigate) == 2
            && aoes.Where(a => a.Navigate).All(a => a.Activation == Start.AddSeconds(1) && !a.Display),
            "Prey early-swap danger windows or display separation missing.");
        e.StatusChanged(100, 904, true, Start.AddSeconds(.1));
        e.StatusChanged(200, 904, false, Start.AddSeconds(.2));
        e.StatusChanged(100, 905, true, Start.AddSeconds(.2), 0x103);
        Sync(.3);
        var swap = e.AmbientAoes().Single(a => a.Id == "graoully/prey/swap");
        Check(swap.Safe && !swap.OverrideDangerMargin && swap.Position == buddy.Position
            && swap.GoalPriority == 1
            && !e.AmbientAoes().Any(a => a.Id == "graoully/prey/separate"), "Latest prey or low-byte three-stack swap failed.");
        e.StatusChanged(100, 905, true, Start.AddSeconds(.31), 4);
        Sync(.4);
        Check(e.AmbientAoes().Any(a => a.Id == "graoully/prey/separate") && !e.AmbientAoes().Any(a => a.Id == "graoully/prey/swap"),
            "Four stacks incorrectly treated as exactly three.");
        e.StatusChanged(100, 905, true, Start.AddSeconds(.4), 0x103);
        e.Tick(Start.AddSeconds(10));
        Check(e.AmbientAoes().Single(a => a.Id == "graoully/prey/swap").GoalPriority == 1,
            "Without a new actor observation, time alone invalidated a status event.");
        e.StatusChanged(100, 904, false, Start.AddSeconds(.41));
        Sync(.45);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("graoully/")), "Current holder loss retained Prey output.");
    }

    private static void Perspective()
    {
        var e = new DungeonEncounter(477);
        DungeonActor boss = new(1, 0x10B9, Center, InCombat: true, Targetable: false);
        DungeonActor player = new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0, Statuses: [904]);
        DungeonActor buddy = new(200, 0x10BB, Center + Vector3.UnitX * 10, PartySlot: 24, Hostile: false, FriendlyNpc: true);
        // Begin with a targetable primary, then test untargetability within the same fight.
        e.Synchronize([boss with { Targetable = true }, player, buddy], player.Position, Start, playerId: 100);
        e.Synchronize([boss, player, buddy], player.Position, Start.AddSeconds(1), playerId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("graoully/")) == 1,
            "Untargetable boss must hide its prey cone while retaining the partner circle.");
        e.Synchronize([boss with { Targetable = true }, player, buddy], buddy.Position, Start.AddSeconds(2), playerId: 200);
        Check(e.AmbientAoes().Single(a => a.Id == "graoully/prey/separate").Position == buddy.Position,
            "Nonzero source slot did not retain the source's self-index partner rule.");
    }

    private static void SlotsAndRemoval()
    {
        var e = new DungeonEncounter(477);
        DungeonActor boss = new(1, 0x10B9, Center, InCombat: true);
        DungeonActor player = new(100, 0, Center, PartySlot: 0, Statuses: [904]);
        DungeonActor first = new(200, 0x10BB, Center + Vector3.UnitX * 10, FriendlyNpc: true, Hostile: false);
        DungeonActor second = new(201, 0x10BB, Center + Vector3.UnitZ * 10, FriendlyNpc: true, Hostile: false);
        DungeonActor buddy = new(202, 0x10BB, Center - Vector3.UnitZ * 10, Buddy: true, Hostile: false);
        e.Synchronize([boss, player, buddy, first, second], Center, Start, playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id == "graoully/prey/separate").Position == first.Position,
            "A Buddy incorrectly occupied the source's Enemy/Ally NPC slot 24.");
        e.Synchronize([boss, player, buddy, second], Center, Start.AddSeconds(1), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Navigate), "NPC slot 25 compacted into the vacated slot 24.");
        e.Synchronize([boss, player, buddy, second, first], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id == "graoully/prey/separate").Position == first.Position,
            "A new eligible NPC did not occupy the first free slot.");
        e.StatusChanged(first.Id, 904, true, Start.AddSeconds(2));
        e.StatusChanged(first.Id, 904, false, Start.AddSeconds(2.1));
        e.Synchronize([boss, player, first, second], Center, Start.AddSeconds(3), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("graoully/")), "Current prey loss resurrected the old holder.");
        e.StatusChanged(player.Id, 904, true, Start.AddSeconds(3.1));
        e.StatusChanged(first.Id, 904, true, Start.AddSeconds(3.2));
        e.RemoveActor(player.Id);
        Check(e.AmbientAoes().Any(a => a.Id == "graoully/prey/display"), "Old prey destruction cleared the latest holder.");
        e.RemoveActor(first.Id);
        e.Synchronize([boss, second], Center, Start.AddSeconds(4), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("graoully/")), "Destroyed current prey revived a former holder.");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
