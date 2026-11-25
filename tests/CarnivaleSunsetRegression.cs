using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleSunsetRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale sunset two act preparation pull completion and exact seven active actions", Lifecycle);
        run("Carnivale sunset dynamic charge actor references death destruction same id and viewer", DynamicCharge);
        run("Carnivale sunset missing initial target never binds later but independent knockback exists", MissingTarget);
        run("Carnivale sunset independent last start wins knockback any source finish current primary", Knockback);
        run("Carnivale sunset knockback six immunity exact expiry and no wall stop navigation", Immunities);
        run("Carnivale sunset charge target raid cones clipping predictions and self target", PersonalOutputs);
        run("Carnivale sunset shared cone group arbitrary finish and gaze default range raw raidwide", SimpleComponents);
        run("Carnivale sunset corrections new generation old finish and knockback source isolation", Generations);
        run("Carnivale sunset first raw confirms packet target distinct from later missing target", FirstRaw);
        run("Carnivale sunset rebuilt viewer reference alive npc raid dead and missing roster damage", PartyIdentity);
        run("Carnivale sunset knockback immunity category overwrite remove and outside immune hint", ImmunityCategories);
        run("Carnivale sunset observed null then identical raw parameters starts a new cast", RepeatedCast);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x26F4, Center, InCombat: true, IsEnemy: true, ObjectIndex: 2, Name: "迪克巴朗"),
            new(2, 0x233C, Center + Vector3.UnitX * 2), new(3, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 10, Player: true, PartySlot: 0),
            new(101, 0, Center + Vector3.UnitZ * 5, Player: true, PartySlot: 1),
        ];
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal Fixture() => Sync();
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == Viewer).Position, Now, playerId: Viewer);
        internal DungeonCastObservation Cast(ulong source = 2, ulong target = 100, double length = 4.3)
        {
            var cast = new DungeonCastObservation(14873, Center + Vector3.UnitX * 99, 1.7f, Now, Now.AddSeconds(length), target);
            Set(source, a => a with { Cast = cast });
            Sync();
            return cast;
        }
        internal DungeonAmbientAoe[] Charges() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/charge/")).ToArray();
    }

    private static void Lifecycle()
    {
        var actors = new List<DungeonActor>
        {
            new(1, 0x26F2, Center), new(2, 0x26F2, Center), new(100, 0, Center, Player: true, PartySlot: 0),
        };
        var e = new DungeonEncounter(796);
        e.Synchronize(actors, Center, Start, playerId: 100);
        Check(e.CarnivalePrecombatHint!.Contains("Toad Oil->Bristle") && e.AmbientAoes().All(a => !a.Navigate), "Act1 preparation.");
        actors[1] = actors[1] with { InCombat = true, Targetable = false, Dead = true };
        e.Synchronize(actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active?.BossOid == 0x26F2 && !e.Casts.Any() && e.CarnivalePrecombatHint == null, "Any actor combat pull.");
        actors[0] = actors[0] with { Hp = 0 };
        e.Synchronize(actors, Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null, "Act1 HP0 treated as dead.");
        actors[0] = actors[0] with { Dead = true };
        e.Synchronize(actors, Center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null, "Act1 all dead completion.");
        var f = new Fixture();
        f.Set(1, a => a with { InCombat = false });
        var pending = new DungeonEncounter(796);
        pending.Synchronize(f.Actors, Center, Start, playerId: 100);
        Check(pending.CarnivalePrecombatHint!.Contains("about 75%") && pending.Active == null, "Act2 preparation.");
        f.Actors.Add(new(4, 0x26F3, Center, InCombat: true, IsEnemy: true, ObjectIndex: 4));
        pending.Synchronize(f.Actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(pending.Active == null, "Cyclops pulled act2.");
        f.Set(1, a => a with { InCombat = true, Targetable = false });
        pending.Synchronize(f.Actors, Center, Start.AddSeconds(2), playerId: 100);
        Check(pending.Active == null, "Untargetable pull.");
        f.Set(1, a => a with { Targetable = true });
        pending.Synchronize(f.Actors, Center, Start.AddSeconds(3), playerId: 100);
        Check(pending.Active?.Casts.Length == 7 && pending.PriorityTarget(1, Center) == 4, "Act2 components/priorities.");
        Check(CarnivaleCatalog.VerticesFor(0x26F4).Length == 32, "Small arena.");
        f.Set(1, a => a with { Hp = 0 });
        pending.Synchronize(f.Actors, Center, Start.AddSeconds(4), playerId: 100);
        Check(pending.Active == null, "Act2 waited for cyclops.");
    }

    private static void DynamicCharge()
    {
        var f = new Fixture();
        f.Cast();
        var charge = f.E.CarnivaleCharges.Single();
        var source = charge.Source;
        var target = charge.Target;
        Check(charge.Cast.TargetId == 100 && charge.Cast.Location != source.Actor.Position, "Full original cast lost.");
        f.Set(2, a => a with { Position = Center + Vector3.UnitX * 3 });
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 13, Dead = true });
        f.Now = Start.AddSeconds(1);
        f.Sync();
        var display = f.Charges().Single(a => a.Id.EndsWith("/display"));
        Check(display.Position == source.Actor.Position && display.OutlineOnly
            && Math.Abs(((RayRectangleTemplate)display.Shape).Length - MathF.Sqrt(178)) < .0001f
            && target.Actor.Dead, "Dynamic/dead target.");
        var frozen = target.Actor.Position;
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Viewer = 101;
        f.Sync();
        f.Actors.Add(new(100, 0, Center - Vector3.UnitZ * 30, Player: true, PartySlot: 0));
        f.Sync();
        Check(ReferenceEquals(f.E.CarnivaleCharges.Single().Target, target) && target.Removed
            && target.Actor.Position == frozen && f.Charges().Any(a => a.Id.EndsWith("/avoid")), "Destroyed target rebound.");
        f.Set(2, a => a with { Dead = true });
        f.Sync();
        Check(f.E.CarnivaleCharges.Count() == 1 && !f.Charges().Any(a => a.Id.EndsWith("/display")), "Dead source removal or display.");
        f.Actors.RemoveAll(a => a.Id == 2);
        f.Sync();
        Check(!f.E.CarnivaleCharges.Any() && f.E.CarnivaleChargeKnockback == null, "Source destroy Finish.");
    }

    private static void MissingTarget()
    {
        var f = new Fixture();
        var cast = f.Cast(target: 999);
        Check(!f.E.CarnivaleCharges.Any() && f.E.CarnivaleChargeKnockback?.Cast.TargetId == 999, "Missing target erased KB.");
        f.Actors.Add(new(999, 0, Center + Vector3.UnitZ * 8, PartySlot: 2, Player: true));
        f.Now = Start.AddSeconds(1);
        f.Sync();
        f.E.StartCast(2, cast, f.Now);
        Check(!f.E.CarnivaleCharges.Any() && f.E.CarnivaleChargeKnockback != null, "Late target auto-bound.");
        f.E.Finish(2, 14873);
        Check(f.E.CarnivaleChargeKnockback == null, "Missing-target Finish.");
    }

    private static void Knockback()
    {
        var f = new Fixture();
        f.Cast(2);
        f.Now = Start.AddSeconds(1);
        f.Cast(3, 101, 7.3);
        var second = f.E.CarnivaleChargeKnockback!;
        Check(second.Cast.Source == 3 && second.Cast.FinishAt == f.Now.AddSeconds(7.3)
            && f.E.CarnivaleCharges.Count() == 2, "Slot and baits merged.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * 10 });
        f.Sync();
        var player = f.Actors.First(a => a.Id == 100);
        var expected = player.Position + Vector3.Normalize(player.Position - f.Actors[0].Position) * 20;
        Check(Vector3.Distance(f.E.CarnivaleChargeLanding(player), expected) < .0001f, "KB used source or locked primary.");
        f.E.Finish(2, 14873);
        Check(f.E.CarnivaleCharges.Single().Cast.Source == 3 && f.E.CarnivaleChargeKnockback == null, "Any finish did not clear independent slot.");
        f.E.Effect(3, 14873, 1, Start.AddSeconds(20));
        f.E.Tick(Start.AddSeconds(30));
        Check(f.E.CarnivaleCharges.Count() == 1, "Deadline/effect ended bait.");
    }

    private static void Immunities()
    {
        var f = new Fixture();
        f.Cast();
        var at = f.E.CarnivaleChargeKnockback!.Cast.FinishAt;
        var player = f.Actors.First(a => a.Id == 100);
        Check(f.E.CarnivaleChargeLanding(player).Z == 130, "KB stopped at wall.");
        Check(f.Charges().Where(a => a.Id.Contains("/knockback/")).All(a => !a.Navigate)
            && f.E.Hints(player.Position).Contains("About to be knocked into danger!"), "KB display/navigation.");
        foreach (var status in new uint[] { 160, 1209, 2663, 1722, 2345, 3054 })
        {
            var immune = player with { StatusExpirations = new Dictionary<uint, DateTime> { [status] = at.AddTicks(1) } };
            f.E.StatusChanged(100, status, true, f.Now, expiresAt: at.AddTicks(1));
            Check(f.E.CarnivaleChargeLanding(immune) == player.Position, $"Immunity {status}.");
            immune = immune with { StatusExpirations = new Dictionary<uint, DateTime> { [status] = at } };
            f.E.StatusChanged(100, status, true, f.Now, expiresAt: at);
            Check(f.E.CarnivaleChargeLanding(immune).Z == 130, $"Expiry equality {status}.");
        }
        Check(f.E.CarnivaleChargeLanding(player with { Position = Center }) == Center, "Coincident push.");
    }

    private static void PersonalOutputs()
    {
        var f = new Fixture();
        f.Cast();
        Check(f.Charges().Count(a => a.Id.Contains("/party/")) == 1
            && f.Charges().Single(a => a.Id.EndsWith("/display")).OutlineOnly
            && f.E.Hints(f.Actors[3].Position).Contains("Bait away from raid!"), "Own bait direction/clip.");
        var cone = f.Charges().Single(a => a.Id.Contains("/party/"));
        Check(cone.Shape is FanTemplate { Radius: 100 } && !cone.Display && cone.Navigate
            && cone.Activation == Start.AddSeconds(4.3), "Cone/clock.");
        f.Viewer = 101;
        f.Sync();
        Check(!f.Charges().Single(a => a.Id.EndsWith("/display")).OutlineOnly
            && f.Charges().Count(a => a.Id.EndsWith("/avoid")) == 1
            && f.E.Hints(f.Actors[4].Position).Contains("GTFO from baited aoe!"), "Other bait output.");
        Check(f.E.PredictedDamage.Single().TargetId == 100 && f.E.PredictedDamage.Single().Activation == Start.AddSeconds(4.3), "Target damage clock.");
        var self = new Fixture();
        self.Cast(2, 2);
        Check(!self.Charges().Any(a => a.Id.Contains("/party/")) && self.E.CarnivaleCharges.Single().Source == self.E.CarnivaleCharges.Single().Target,
            "Self bait reference/direction.");
    }

    private static void SimpleComponents()
    {
        var f = new Fixture();
        f.E.StartCast(2, new(14871, Center, .4f, Start, Start.AddSeconds(4.3)), Start);
        Check(f.E.Casts[2].Rule.Shape is FanTemplate { Radius: 44 }, "Cone dimensions.");
        f.E.Finish(2, 6497, Start.AddSeconds(1));
        Check(!f.E.Casts.ContainsKey(2), "Group ignores unrelated Finish.");
        f = new Fixture();
        f.E.StartCast(2, new(14875, Center, .4f, Start, Start.AddSeconds(.5)), Start);
        Check(f.E.Gazes.Count() == 1 && f.E.GazeFacing(Center + Vector3.UnitZ * 1000, MathF.PI) != null,
            "Gaze range clipped to action sheet.");
        f.E.Finish(2, 14875);
        f = new Fixture();
        var raw = new DungeonCastObservation(14686, Center, 0, Start, Start.AddSeconds(9.3));
        f.Set(2, a => a with { Cast = raw });
        f.Sync();
        Check(f.E.Hints(Center).Contains("Use Diamondback!") && f.E.PredictedDamage.Count() == 2
            && f.E.PredictedDamage.All(p => p.Activation == raw.FinishAt), "Raidwide raw clock.");
        foreach (var action in new uint[] { 6497, 14874, 14779, 14685 })
            Check(!f.E.Active!.Casts.Any(c => c.Action == action), "Invented source component.");
    }

    private static void Generations()
    {
        var f = new Fixture();
        var raw = f.Cast(2);
        var first = f.E.CarnivaleCharges.Single().Generation;
        f.Now = Start.AddSeconds(1);
        f.Cast(3, 101);
        var last = f.E.CarnivaleChargeKnockback!.Generation;
        f.E.StartCast(2, raw with { Location = Center }, f.Now);
        Check(f.E.CarnivaleChargeKnockback!.Generation == last && f.E.CarnivaleCharges.First().Generation == first,
            "Older source correction overwrote slot.");
        f.Now = Start.AddSeconds(2);
        f.Set(2, a => a with { Cast = raw with { StartedAt = f.Now, FinishAt = f.Now.AddSeconds(4.3) } });
        f.Sync();
        Check(f.E.CarnivaleChargeKnockback!.Generation == last && f.E.CarnivaleCharges.First().Generation == first,
            "Elapsed clock drift restarted source2.");
        f.Set(2, a => a with { Cast = a.Cast! with { Location = Center } });
        f.Sync();
        Check(f.E.CarnivaleCharges.Count() == 2 && f.E.CarnivaleCharges.All(c => c.Generation != first),
            "Raw replacement did not finish the old generation.");
        f.E.Finish(2, 14873, Start.AddSeconds(1));
        Check(f.E.CarnivaleCharges.Count() == 2 && f.E.CarnivaleChargeKnockback != null, "Old finish ended new cast.");
        f.E.Finish(2, 14873, Start.AddSeconds(3));
        Check(f.E.CarnivaleCharges.Count() == 1 && f.E.CarnivaleCharges.Single().Cast.Source == 3
            && f.E.CarnivaleChargeKnockback == null, "Finish must pop first by caster.");
    }

    private static void FirstRaw()
    {
        var f = new Fixture();
        var packet = new DungeonCastObservation(14873, Center, 0, Start, Start.AddSeconds(4.3), 999);
        f.E.StartCast(2, packet, Start);
        Check(!f.E.CarnivaleCharges.Any(), "Packet missing target.");
        f.Actors.Add(new(999, 0, Center + Vector3.UnitZ * 7, Player: true, PartySlot: 2));
        f.Set(2, a => a with { Cast = packet });
        f.Now = Start.AddSeconds(.1);
        f.Sync();
        Check(f.E.CarnivaleCharges.Single().Target.Actor.Id == 999, "Canonical first raw failed to bind target.");
    }

    private static void PartyIdentity()
    {
        var f = new Fixture();
        f.Actors.Add(new(200, 0x1234, Center + Vector3.UnitZ * 7, PartySlot: 24, FriendlyNpc: true, Hostile: false, IsEnemy: true));
        f.Cast();
        Check(f.Charges().Any(a => a.Id.EndsWith("/party/200")), "NPC24 omitted from raid.");
        f.Set(100, a => a with { Dead = true });
        f.Sync();
        Check(f.E.PredictedDamage.Single().TargetId == 100, "Dead target lost damage.");
        f.E.CurrentPartyMembers = [new(0, "", 100)];
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Viewer = 101;
        f.Sync();
        Check(f.E.PredictedDamage.Single().TargetId == 100, "Absent actor with roster entry lost damage.");
        f.Actors.Add(new(100, 0, Center + Vector3.UnitZ * 4, Player: true, PartySlot: 0));
        f.Viewer = 100;
        f.Sync();
        Check(f.Charges().Single(a => a.Id.EndsWith("/display")).OutlineOnly
            && f.Charges().Any(a => a.Id.EndsWith("/avoid")) && !f.Charges().Any(a => a.Id.Contains("/party/")),
            "Rebuilt viewer must keep ID outline but navigate as distinct reference.");
    }

    private static void ImmunityCategories()
    {
        var f = new Fixture();
        f.Cast();
        var at = f.E.CarnivaleChargeKnockback!.Cast.FinishAt;
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: at.AddSeconds(20));
        f.E.StatusChanged(100, 1209, true, f.Now, expiresAt: at.AddSeconds(1));
        f.E.StatusChanged(100, 1209, false, f.Now);
        var player = f.Actors.First(a => a.Id == 100);
        Check(f.E.CarnivaleChargeLanding(player) != player.Position, "Same-category removal left older immunity.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 30,
            StatusExpirations = new Dictionary<uint, DateTime> { [1722] = at.AddSeconds(1) } });
        f.Sync();
        Check(!f.E.Hints(f.Actors.First(a => a.Id == 100).Position).Contains("About to be knocked into danger!"),
            "Immune outside actor received KB danger hint.");
        var state = new CarnivaleKnockbackImmunity();
        var actor = new DungeonActor(100, 0, Center, PartySlot: 0, StatusSlots:
        [
            new MasterPlayer.Replay.ReplayStatus(160, 0, 1, at.AddSeconds(20)) { SlotIndex = 0 },
            new MasterPlayer.Replay.ReplayStatus(1209, 0, 1, at.AddSeconds(1)) { SlotIndex = 5 },
        ]);
        state.Observe([actor]);
        state.Observe([actor with { StatusSlots = [actor.StatusSlots![0]] }]);
        Check(!state.ImmuneAt(0, at), "Later native-slot loss did not clear shared category.");
        state.Clear();
        state.Observe([actor with { StatusSlots = [actor.StatusSlots![0]] }]);
        state.Observe([actor with { StatusSlots = [actor.StatusSlots![1] with { SlotIndex = 0 }] }]);
        Check(state.ImmuneAt(0, at), "Same-slot replacement lost newly gained status.");
        state.Observe([]);
        Check(!state.ImmuneAt(0, at), "Destroyed actor immunity leaked across slots.");
        state.Clear();
        state.Observe([actor]);
        state.Observe([actor with { PartySlot = -1 }]);
        Check(state.ImmuneAt(0, at), "Party removal manufactured status loss.");
        state.Observe([actor]);
        Check(state.ImmuneAt(0, at), "Rejoin lost unchanged status ledger.");
    }

    private static void RepeatedCast()
    {
        var f = new Fixture();
        f.Cast();
        f.Now = Start.AddSeconds(5);
        f.Set(2, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.CarnivaleCharges.Any() && f.E.CarnivaleChargeKnockback == null, "Observed null did not Finish.");
        f.Now = Start.AddSeconds(6);
        f.Cast();
        Check(f.E.CarnivaleCharges.Count() == 1 && f.E.CarnivaleChargeKnockback != null,
            "Identical second cast swallowed by old raw baseline.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
