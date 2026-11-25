using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleSlimeBombRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Stage07 and Stage08 exact ownership and no invented cast or interrupt", Ownership);
        run("Carnivale Stage07 Act2 exact four hole arena source oracle and precombat boundary", Arena);
        run("Carnivale slime bomb constructor components group pull HP0 death and destruction completion", Lifecycle);
        run("Carnivale Stage07 Act1 fixed primary radius and phase specific instructions", Primary);
        run("Carnivale slime bomb original references destroy freeze same ID death revival and creation", References);
        run("Carnivale Stage08 priority overrides height and aggro but preserves pointless candidates", Priority);
        run("Carnivale slime bomb actual output display only source yellow outlines and owner cleanup", Output);
        run("Carnivale constructor module histories cold reverse promotion pending death and destroyed primary handoff", PendingHistory);
        run("Carnivale pending same ID generations without party and loaded cross OID identity remain independent", ModuleIdentity);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal bool Enabled = true;
        internal Fixture(uint oid)
        {
            Actors =
            [
                new(1, oid, Center, Hp: 1000, Name: "Test Slime"),
                new(10, oid == 0x2703 ? 0x2702u : oid == 0x2705 ? 0x2704u : 0x2709u, Center + Vector3.UnitX * 15, Hp: 1000),
                new(100, 0, Center, PartySlot: 0, Player: true),
            ];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, _ => Enabled, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal DungeonAmbientAoe[] Explosions() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/explosion/")).ToArray();
    }

    private static void Ownership()
    {
        foreach (var oid in new uint[] { 0x2703, 0x2705, 0x2708 })
        {
            var f = new Fixture(oid);
            Check(CarnivaleCatalog.Owns(796, oid) && !CarnivaleCatalog.Owns(795, oid)
                && f.E.Active == null && f.E.CarnivalePrecombatHint != null
                && f.E.Definitions.Single(d => d.BossOid == oid).Casts.Length == 0,
                "Exact native ownership or source no-AOE boundary.");
            f.Set(10, a => a with { InCombat = true });
            f.Sync();
            foreach (var action in new uint[] { 14696, 14709, 14687, 14689, 14688, 14680 })
                f.E.StartCast(10, new(action, Center, 0, Start, Start.AddSeconds(10), 100), Start);
            Check(f.E.Casts.Count == 0 && !f.E.PredictedDamage.Any(), "Enumeration became active cast component.");
        }
        Check(CarnivaleCatalog.Owns(796, 0x2706)
            && CarnivaleCatalog.Create().Single(d => d.BossOid == 0x2706).Casts.Select(c => c.Action).SequenceEqual(new uint[] { 14710 }),
            "Act3 must claim its implemented LOS action without activating enumerated slime actions.");
    }

    private static void Arena()
    {
        var f = new Fixture(0x2705);
        var source = BossMod.Global.MaskedCarnivale.Layouts.Layout4Quads;
        var arena = CarnivaleCatalog.Arena(0, 0x2705);
        Check(arena.GetContours().Count == 5, "Four holes collapsed into circle.");
        for (var ix = -110; ix <= 110; ++ix)
            for (var iz = -110; iz <= 110; ++iz)
            {
                var p = new Vector3(100 + ix * .25f + .137f, 0, 100 + iz * .25f + .271f);
                Check(arena.Contains(p, 0) == source.Contains(new BossMod.WDir(p.X - source.Center.X, p.Z - source.Center.Z)),
                    $"Resolved four-quad source mismatch {ix},{iz}.");
            }
        var preview = f.E.AmbientAoes().Single(a => a.Id == "carnivale/precombat/arena");
        Check(!preview.Navigate && preview.Shape.Resolve(preview.Position, 0, null).GetContours().Count == 5,
            "Preparation lost exact holes or invented navigation.");
        f.Set(10, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Arena?.Contains(new(110, 0, 110)) == false && f.E.Arena.Contains(Center),
            "Pull arena did not retain blockers.");
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2703, 0x2705, 0x2708 })
        {
            var f = new Fixture(oid);
            f.Set(1, a => a with { Targetable = false, Hp = 0 });
            f.Set(10, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.Active?.BossOid == oid && f.E.PrimaryId == 1, "Group pull gated on Primary.");
            f.Set(10, a => a with { InCombat = false, Dead = true });
            f.Sync();
            Check(f.E.Active != null, "HP0 incorrectly satisfied DeadOrDestroyed.");
            f.Actors.RemoveAll(a => a.Id == 1);
            f.Sync();
            Check(f.E.Active == null && !f.Explosions().Any(), "Destroyed plus all-dead did not complete/release.");
        }
    }

    private static void Primary()
    {
        var f = new Fixture(0x2703);
        f.Actors.Add(new(2, 0x2703, Center + Vector3.UnitX * 20));
        f.Sync();
        Check(f.Explosions().Length == 1 && f.Explosions()[0].Shape is CircleTemplate { Radius: 7.6f }
            && f.E.Hints(Center).Contains("In slime explosion radius!")
            && f.E.CarnivalePrecombatHint!.Contains("anything but fire"), "Act1 constructor contract.");
        f.Set(10, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Hints(Center).Contains("Hit the Test Slime from a safe distance to win this act.")
            && !f.E.Hints(Center).Any(h => h.Contains("Sticky Tongue")), "Act1 hints did not transition.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active != null && !f.Explosions().Any(), "Dead fixed primary rebound to sibling.");
    }

    private static void References()
    {
        foreach (var oid in new uint[] { 0x2705, 0x2708 })
        {
            var f = new Fixture(oid);
            var listOid = oid == 0x2705 ? 0x2705u : 0x270Au;
            f.Actors.Add(new(20, listOid, Center + Vector3.UnitZ * 10));
            f.Sync();
            var radius = oid == 0x2705 ? 7.6f : 6;
            Check(f.Explosions().Any(a => a.Shape is CircleTemplate c && c.Radius == radius
                && a.Position == Center + Vector3.UnitZ * 10), "Created member absent.");
            f.Set(20, a => a with { Position = Center + Vector3.UnitZ * 12 });
            f.Sync();
            f.Actors.RemoveAll(a => a.Id == 20);
            f.Sync();
            var old = f.Explosions().Single(a => a.Position == Center + Vector3.UnitZ * 12);
            f.Actors.Add(new(20, listOid, Center + Vector3.UnitZ * 20));
            f.Sync();
            Check(f.Explosions().Any(a => a.Id == old.Id && a.Position == old.Position)
                && f.Explosions().Any(a => a.Id != old.Id && a.Position == Center + Vector3.UnitZ * 20),
                "Destroyed reference lost or rebound same-ID generation.");
            f.Set(20, a => a with { Dead = true });
            f.Sync();
            Check(!f.Explosions().Any(a => a.Position == Center + Vector3.UnitZ * 20)
                && f.Explosions().Any(a => a.Id == old.Id), "Death removed wrong original reference.");
            f.Set(20, a => a with { Dead = false });
            f.Sync();
            Check(!f.Explosions().Any(a => a.Position == Center + Vector3.UnitZ * 20),
                "Revival re-created removed component entry.");
        }
    }

    private static void Priority()
    {
        var f = new Fixture(0x2708);
        f.Set(1, a => a with { InCombat = true, IsEnemy = true, ObjectIndex = 2, Hp = 1000, Position = Center + Vector3.UnitY * 20 });
        f.Actors.Add(new(20, 0x9999, Center, IsEnemy: true, ObjectIndex: 198, Hp: 1000));
        f.Actors.Add(new(21, 0x9999, Center, IsEnemy: true, ObjectIndex: 196, Hp: 1));
        f.Sync();
        Check(f.E.PriorityTarget(20, Center) == 1, "Primary direct priority override lost.");
        f.Set(1, a => a with { Targetable = false });
        f.Sync();
        Check(f.E.PriorityTarget(0, Center) == 20, "All other valid OIDs were not assigned zero priority.");
        f.Set(20, a => a with { Targetable = false });
        f.Sync();
        Check(f.E.PriorityTarget(21, Center) == 0, "Source Pointless priority was overwritten.");
    }

    private static void Output()
    {
        foreach (var oid in new uint[] { 0x2703, 0x2705, 0x2708 })
        {
            var f = new Fixture(oid);
            using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
            var draw = new ScriptDrawApi();
            var output = new DungeonOutput(safe, draw, safe.Manager);
            output.Apply(f.E, Start, false);
            var snapshot = safe.Manager.BuildSnapshot(Start);
            Check(snapshot.DangerAreas.Count == 0 && snapshot.GoalAreas.Count == 0 && snapshot.ArenaBoundary == null,
                "Preparation display became navigation.");
            Check(f.Explosions().All(a => !a.Navigate && a.OutlineOnly && a.Color == new Vector4(1, 1, 0, 1)),
                "Explosion outline source consumer boundary or colour lost.");
            f.Set(10, a => a with { InCombat = true });
            f.Sync();
            output.Apply(f.E, Start, false);
            snapshot = safe.Manager.BuildSnapshot(Start);
            Check(snapshot.DangerAreas.Count == 0 && snapshot.GoalAreas.Count == 0 && snapshot.ArenaBoundary != null,
                "Pull explosion gained dangerous navigation.");
            output.Clear();
            Check(safe.Manager.BuildSnapshot(Start).ArenaBoundary == null, "Owner leaked arena.");
        }
    }

    private static void PendingHistory()
    {
        var f = new Fixture(0x2705);
        f.Actors.Add(new(2, 0x2705, Center + Vector3.UnitZ * 10));
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.CarnivalePrecombatHint == null, "Pending promoted before loaded primary unload.");
        f.Sync();
        Check(f.E.CarnivalePrecombatHint != null && f.Explosions().Length == 2
            && f.Explosions().Any(a => a.Position == Center),
            "Promoted pending module lost its constructor reference to destroyed primary.");
        var discarded = new Fixture(0x2705);
        discarded.Actors.Add(new(2, 0x2705, Center + Vector3.UnitZ * 10));
        discarded.Sync();
        discarded.Set(2, a => a with { Dead = true });
        discarded.Sync();
        discarded.Set(2, a => a with { Dead = false });
        discarded.Sync();
        discarded.Actors.RemoveAll(a => a.Id == 1);
        discarded.Set(10, a => a with { InCombat = false });
        discarded.Sync();
        discarded.Sync();
        discarded.Set(10, a => a with { InCombat = true });
        discarded.Sync();
        Check(discarded.E.Active == null && !discarded.Explosions().Any(),
            "Dead pending module resurrected without ActorAdded.");

        var cold = new DungeonEncounter(796);
        DungeonActor[] actors =
        [
            new(1, 0x2703, Center),
            new(2, 0x2703, Center + Vector3.UnitX * 10),
            new(100, 0, Center, PartySlot: 0, Player: true),
        ];
        cold.Synchronize(actors, Center, Start, playerId: 100);
        Check(cold.AmbientAoes().Single(a => a.Id == "carnivale/explosion/primary").Position == actors[1].Position,
            "Cold source pending promotion was not reversed.");
        cold.DirectorUpdate(0x40000005);
        cold.DirectorUpdate(0x40000011);
        cold.Synchronize(actors, Center, Start, playerId: 100);
        Check(cold.AmbientAoes().Single(a => a.Id == "carnivale/explosion/primary").Position == actors[0].Position,
            "Wipe recreated unloaded owner instead of preserving pending sibling.");
    }

    private static void ModuleIdentity()
    {
        var e = new DungeonEncounter(796);
        var actors = new List<DungeonActor>
        {
            new(1, 0x2705, Center),
            new(100, 0, Center, Player: true, PartySlot: -1),
        };
        void Sync() => e.Synchronize(actors, Center, Start, playerId: 100);
        Sync();
        actors.RemoveAt(0);
        Sync();
        actors.Add(new(1, 0x2705, Center + Vector3.UnitZ * 10));
        actors[0] = actors[0] with { PartySlot = 0 };
        Sync();
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/explosion/")) == 1,
            "Removed pending generation survived as a current module.");
        actors[1] = actors[1] with { Dead = true };
        Sync();
        actors.Add(new(10, 0x2704, Center, InCombat: true));
        Sync();
        Check(e.Active?.BossOid == 0x2705, "Old pending deletion removed the new loaded module.");

        var f = new Fixture(0x2703);
        f.Set(10, a => a with { InCombat = true });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x2708, Center + Vector3.UnitZ * 10));
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2703
            && f.Explosions().Single().Shape is CircleTemplate { Radius: 7.6f }
            && f.Explosions().Single().Position == Center,
            "Different OID birth rebound the old loaded Primary or inherited Started.");
        f.Set(10, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null && f.E.CarnivalePrecombatHint!.Contains("Flying Sardine")
            && f.Explosions().Single().Shape is CircleTemplate { Radius: 10 },
            "Same ID different module selection retained old Active definition.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2708, "Independent new OID module did not pull.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
