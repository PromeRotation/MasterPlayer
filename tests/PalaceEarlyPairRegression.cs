using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class PalaceEarlyPairRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -220);
    internal static void Run(Action<string, Action> run)
    {
        run("Palace early pair exact active rules source area and ownership", Rules);
        run("Palace Black Rider dynamic charge independent radial wall knockback captured raid slot", Charge);
        run("Palace Black Rider missing charge target real finish immunity and independent direction masks", Knockback);
        run("Palace Black Rider original target reference survives destruction same ID new lifetime", References);
        run("Palace Black Rider any raw cast hides auto cleave no invented Geirrothr timing", Cleave);
        run("Palace early pair native poison prediction consumption clock real identity no time expiry", Voidzones);
        run("Palace Yaquaru activation captured puddles dynamic originals freeze and no late appends", Puddles);
        run("Palace Yaquaru exact 1000 binary soft score old consumer no automatic movement", Goal);
        run("Palace Yaquaru four raw Heavy slots strict eight threshold sticky bit real lose", Heavy);
        run("Palace Yaquaru Heavy first native status duplicate source replacement old roster and dead", HeavySlots);
        run("Palace early pair feed ordering old raw cast roster all status loses before update", InputOrdering);
        run("Palace early pair production output real solver directional escape poison escape owner clear", NativeOutput);
        run("Palace early pair module reset director wipe old finished no phantom reload", Lifetime);
        run("Palace Black Rider reset raw retarget and first saturated observation keep source clock", RestoredRaw);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint territory, bool combat = true, bool pool = true)
        {
            E = new(territory);
            Actors = [new(1, territory == 593 ? 0x1814u : 0x1815u, Center, InCombat: combat,
                TargetId: 100, HitboxRadius: territory == 593 ? 3.92f : 5.75f),
                new(100, 0, Center + Vector3.UnitX * 10, PartySlot: 0, Player: true),
                new(101, 0, Center + Vector3.UnitZ * 12, PartySlot: 1, Player: true)];
            if (pool) Actors.Add(new(10, territory == 593 ? 0x1E858Eu : 0x1E9998u, Center + Vector3.UnitX * 15));
            Sync();
        }
        internal void Sync(ulong player = 100) => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == player)?.Position, Now, null, player);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change) => Actors[Actors.FindIndex(a => a.Id == id)] = change(Actors.Single(a => a.Id == id));
        internal void Cast(ulong source, uint action, ulong target, float seconds = 4)
        {
            var cast = new DungeonCastObservation(action, Center + Vector3.UnitX * 15, .8f, Now, Now.AddSeconds(seconds + .3f), target);
            Set(source, a => a with { Cast = cast });
            Sync();
        }
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Rules()
    {
        var black = new Fixture(593);
        var water = new Fixture(594);
        Check(black.E.Definitions.Single().Casts.Length == 3
            && black.E.Definitions.Single().Casts.Single(r => r.Action == 7157).Shape == new CircleTemplate(7)
            && black.E.Definitions.Single().Casts.All(r => r.Action is not (7087 or 7179)), "Inactive Black Rider attacks added.");
        Check(water.E.Definitions.Single().Casts.Length == 2 && water.E.Definitions.Single().Casts.All(r => r.Action is not (7093 or 7092))
            && water.E.Definitions.Single().Casts.Single(r => r.Action == 7094).Shape == new CircleTemplate(8), "Inactive Drench added.");
        Check(ArrDungeonCatalog.Owns(0x1814, 593) && !ArrDungeonCatalog.Owns(0x1815, 593)
            && ArrDungeonCatalog.Owns(0x1815, 594), "Boss identity/territory isolation.");
        Check(black.E.Arena!.Contains(Center) && !black.E.Arena.Contains(new(-300, 0, -245)), "Source cut entrance missing.");
    }
    private static void Charge()
    {
        var f = new Fixture(593, pool: false);
        f.Cast(1, 7089, 100);
        var kb = f.E.BlackRiderKnockback!;
        var charge = f.E.BlackRiderCharges.Single();
        Check(kb.TargetSlot == 0 && kb.Origin == Center && kb.Distance == 25 && kb.Kind == "AwayFromOrigin"
            && kb.StopAtWall && !kb.IgnoreImmunes && charge.HalfWidth == 3 && charge.Length == 10, "Raw models conflated.");
        var landing = f.E.BlackRiderLanding(f.Actors.Single(a => a.Id == 100));
        Check(landing.X > Center.X + 24 && landing.X < Center.X + 25 && landing.Z == Center.Z, "Wrong forward direction or AABB wall stop.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitZ * 5, Rotation = 2, TargetId = 101 });
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 8, Dead = true });
        f.Sync();
        Check(f.E.BlackRiderKnockback!.Origin == Center && f.E.BlackRiderCharges.Single().Length > 9
            && f.E.BlackRiderCharges.Single().Target.Actor.Id == 100, "Dynamic bait retargeted or KB origin moved.");
        Check(f.E.PredictedDamage.Single().Activation == charge.Cast.FinishAt && f.E.PredictedDamage.Single().TargetId == 100,
            "Dead captured target lost source Tankbuster prediction.");
        f.Set(100, a => a with { PartySlot = 1 });
        f.Set(101, a => a with { PartySlot = 0 });
        f.Sync(101);
        Check(f.E.BlackRiderLanding(f.Actors.Single(a => a.Id == 100)) == f.Actors.Single(a => a.Id == 100).Position
            && f.E.BlackRiderLanding(f.Actors.Single(a => a.Id == 101)) != f.Actors.Single(a => a.Id == 101).Position,
            "Captured slot incorrectly followed original target identity.");
        f.E.Effect(1, 7089, 50, f.Now);
        Check(f.E.BlackRiderCharges.Count == 1 && f.E.BlackRiderKnockback != null, "Effect finished independent cast components.");
        f.Set(1, a => a with { Cast = null });
        f.Sync();
        Check(f.E.BlackRiderCharges.Count == 0 && f.E.BlackRiderKnockback == null, "Real Finish retained cast components.");
    }
    private static void Knockback()
    {
        var f = new Fixture(593);
        f.E.CurrentPartyMembers = [new ReplayPartyMember(0, "", 999)];
        f.Sync();
        f.Cast(1, 7089, 999);
        Check(f.E.BlackRiderCharges.Count == 0 && f.E.BlackRiderKnockback!.TargetSlot == 0, "Missing bait target also deleted valid KB.");
        f.E.Finish(1, 7089, f.Now);
        f.Set(1, a => a with { Cast = null });
        f.Sync();
        f.Now = f.Now.AddSeconds(1);
        f.E.CurrentPartyMembers = [];
        f.Cast(1, 7089, 100);
        var hit = f.E.BlackRiderKnockback!.Cast.FinishAt;
        var player = f.Actors.Single(a => a.Id == 100);
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: hit.AddTicks(1));
        Check(f.E.BlackRiderLanding(player) == player.Position, "Strict > immunity boundary lost.");
        var nav = f.E.AmbientAoes().Where(a => a.Id.StartsWith("blackrider/kb/avoid")).ToArray();
        Check(nav.Length == 1 && nav[0].Navigate && !nav[0].Display, "Immunity removed independent forbidden direction.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: hit);
        Check(f.E.BlackRiderLanding(player) != player.Position, "Equality incorrectly immune.");
        f.Set(10, a => a with { EventState = 7 });
        f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Id.StartsWith("blackrider/kb/avoid"))
            && !f.E.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/live")), "Direction and live AOEs dataset conflated.");
        f.E.Finish(1, 7089, f.Now);
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("blackrider/charge") || a.Id.StartsWith("blackrider/kb")), "Cancel left outputs.");
    }
    private static void References()
    {
        var f = new Fixture(593, pool: false);
        f.Cast(1, 7089, 100);
        var original = f.E.BlackRiderCharges.Single().Target;
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync(101);
        f.Actors.Add(new(100, 0, Center - Vector3.UnitX * 10, PartySlot: 0, Player: true));
        f.Sync();
        Check(original.Removed && ReferenceEquals(original, f.E.BlackRiderCharges.Single().Target)
            && original.Actor.Position.X == Center.X + 10
            && f.E.AmbientAoes().Any(a => a.Id.EndsWith("/avoid") && a.Id.StartsWith("blackrider/charge")),
            "Same ID new actor replaced old bait target reference.");
        f.E.ResetFight();
        f.Sync();
        Check(f.E.BlackRiderCharges.Count == 1 && ReferenceEquals(original, f.E.BlackRiderCharges.Single().Target),
            "Output reset reconstructed surviving bait component or rebound old target.");
        f.E.RemoveActor(1, f.Now);
        Check(f.E.BlackRiderCharges.Count == 0 && f.E.BlackRiderKnockback == null, "Source destroy failed Finish before removal.");
    }
    private static void Cleave()
    {
        var f = new Fixture(593, pool: false);
        Check(f.E.AmbientAoes().Any(a => a.Id == "blackrider/cleave/1/display" && a.Shape == new FanTemplate(11.92f, MathF.PI / 2)),
            "Auto shape missing.");
        f.Set(1, a => a with { Cast = new(99999, Center, 0, Start, Start.AddSeconds(4)) });
        f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("blackrider/cleave")), "Unknown raw cast did not hide auto.");
    }
    private static void Voidzones()
    {
        foreach (var territory in new uint[] { 593, 594 })
        {
            var f = new Fixture(territory, pool: false);
            var action = territory == 593 ? 7088u : 7091u;
            var oid = territory == 593 ? 0x1E858Eu : 0x1E9998u;
            f.Now = Start.AddSeconds(5);
            f.E.Effect(1, action, 1, f.Now, 100, Center, Start);
            f.E.Effect(1, action, 1, f.Now, 100, Center, Start);
            var predictions = f.E.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/event")).ToArray();
            Check(predictions.Length == 2 && predictions.All(a => a.Position == f.Actors.Single(a => a.Id == 100).Position
                && a.Activation == f.Now.AddSeconds(territory == 593 ? 1.3 : .8)
                && a.Shape == new CircleTemplate(territory == 593 ? 9 : 8)), "Native packet event location/delay/identity.");
            f.Now = Start.AddSeconds(100);
            f.Sync();
            Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event")) == 2, "Event prediction expired by guessed clock.");
            f.Actors.Add(new(50, oid, f.Actors.Single(a => a.Id == 100).Position, Dead: true));
            f.Sync();
            Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event")) == 1, "Live source must consume exactly one prediction/update.");
            f.Sync();
            Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/event"))
                && f.E.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/live")), "Dead non-State7 source filtered.");
        }
    }
    private static void Puddles()
    {
        var f = new Fixture(594, combat: false);
        f.Set(10, a => a with { Position = Center, EventState = 7 });
        f.Sync();
        Check(f.E.YaquaruPuddles.Count == 0, "Puddles captured precombat.");
        f.Set(10, a => a with { EventState = 0 });
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        var original = f.E.YaquaruPuddles.Single();
        Check(f.E.YaquaruPull != null && f.E.AmbientAoes().Any(a => a.Id == "yaquaru/boss-in-puddle"
            && a.OutlineOnly && !a.Navigate && a.Shape == new CircleTemplate(5.75f)), "Activation captured pool/outline missing.");
        f.Set(10, a => a with { Position = Center + Vector3.UnitX * 3, Dead = true, EventState = 7 });
        f.Actors.Add(new(11, 0x1E9998, Center));
        f.Sync();
        Check(f.E.YaquaruPuddles.Count == 1 && original.Actor.Position.X == Center.X + 3 && f.E.YaquaruPull != null,
            "Captured array refiltered or late pool appended.");
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Sync();
        f.Actors.Add(new(10, 0x1E9998, Center + Vector3.UnitX * 100));
        f.Sync();
        Check(original.Removed && ReferenceEquals(original, f.E.YaquaruPuddles.Single()) && original.Actor.Position.X == Center.X + 3,
            "Destroyed captured pool failed to freeze old generation.");
        var empty = new Fixture(594, pool: false);
        empty.Actors.Add(new(10, 0x1E9998, Center));
        empty.Sync();
        Check(empty.E.YaquaruPuddles.Count == 0 && empty.E.YaquaruPull == null, "Initially empty array acquired later pools.");
    }
    private static void Goal()
    {
        var f = new Fixture(594);
        f.Set(10, a => a with { Position = Center });
        f.Sync();
        var pull = f.E.YaquaruPull!;
        Check(pull.Radius == 22.5f && pull.Score(Center + Vector3.UnitX * 22.5f) == 0
            && pull.Score(Center + Vector3.UnitX * 22.501f) == 1000, "Exact strict union-complement score.");
        Check(MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertGoalZones(
            [p => pull.Score(new(p.X, 0, p.Z))], new BossMod.WPos(-300, -220), 25, 0).Count == 0
            && !f.E.AmbientAoes().Any(a => a.Safe), "Old MP binary-score consumption expanded.");
        f.Set(1, a => a with { TargetId = 101 });
        f.Sync();
        Check(f.E.YaquaruPull == null && f.E.AmbientAoes().Any(a => a.Id == "yaquaru/boss-in-puddle"),
            "Boss target gate incorrectly gated independent outline.");
    }
    private static void Heavy()
    {
        var f = new Fixture(594);
        f.Set(100, a => a with { StatusSlots = [new(14, 0, 1, Start.AddSeconds(8))] });
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 0, "Exactly 8 seconds set Heavy bit.");
        f.Set(100, a => a with { StatusSlots = [new(14, 0, 1, Start.AddSeconds(8).AddTicks(1))] });
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 1, "Strict >8 not latched.");
        f.Now = Start.AddSeconds(100);
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 1, "Heavy expiration auto-cleared source bit.");
        f.Set(100, a => a with { StatusSlots = [] });
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 0, "Real status removal did not clear bit.");
        f.Set(100, a => a with { StatusSlots = [new(14, 0, 1, f.Now.AddSeconds(9))] });
        f.Sync();
        f.E.StatusChanged(100, 14, false, f.Now);
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 0, "Stale raw frame resurrected packet Lose.");
        f.E.StatusChanged(100, 14, true, f.Now, expiresAt: f.Now.AddSeconds(9));
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 1, "Real Gain failed to remove old loss overlay.");
    }
    private static void HeavySlots()
    {
        var f = new Fixture(594);
        f.Set(100, a => a with { Dead = true, StatusSlots =
            [new(14, 0, 1, Start.AddSeconds(7)) { SlotIndex = 0 }, new(14, 0, 2, Start.AddSeconds(30)) { SlotIndex = 1 }] });
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 0, "Aggregated maximum replaced first native Heavy.");
        f.Set(100, a => a with { StatusSlots = [new(14, 0, 2, Start.AddSeconds(30)) { SlotIndex = 0 }] });
        f.Set(101, a => a with { PartySlot = 4, StatusSlots = [new(14, 0, 1, Start.AddSeconds(30))] });
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 1, "Dead filtered or fifth slot scanned.");
        f.Set(100, a => a with { PartySlot = 2, StatusSlots = [] });
        f.Sync();
        Check(f.E.YaquaruShouldCleanse == 0, "Raw loss used new roster instead of previous event roster.");
    }
    private static void Lifetime()
    {
        foreach (var territory in new uint[] { 593, 594 })
        {
            var f = new Fixture(territory);
            f.E.PalaceGuideDirector(0x40000005);
            f.Sync();
            f.E.PalaceGuideDirector(0x40000011);
            f.Sync();
            Check(f.E.Active == null, "Director resume reloaded old owner.");
            f.Actors.RemoveAll(a => a.Id == 1);
            f.Sync();
            f.Actors.Add(new(1, territory == 593 ? 0x1814u : 0x1815u, Center, InCombat: true));
            f.Sync();
            Check(f.E.Active != null, "New generation unable to activate after wipe.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            f.Set(1, a => a with { Dead = false });
            f.Sync();
            Check(f.E.Active == null, "Finished old actor restarted after healing.");
        }
    }

    private static void InputOrdering()
    {
        var f = new Fixture(593);
        f.Set(100, a => a with { PartySlot = 1 });
        f.Set(101, a => a with { PartySlot = 0 });
        f.Cast(1, 7089, 100);
        Check(f.E.BlackRiderKnockback!.TargetSlot == 0
            && f.E.BlackRiderLanding(f.Actors.Single(a => a.Id == 101)) != f.Actors.Single(a => a.Id == 101).Position,
            "Raw cast callback captured the new roster before Party commit.");
        var cold = new Fixture(593, combat: false);
        cold.Set(1, a => a with { InCombat = true, Cast = new(7089, Center, 0, Start, Start.AddSeconds(4), 100) });
        cold.Set(100, a => a with { PartySlot = 1 });
        cold.Set(101, a => a with { PartySlot = 0 });
        cold.Sync();
        Check(cold.E.BlackRiderKnockback!.TargetSlot == 1, "Activation bootstrap incorrectly used pre-pull roster.");
        var missing = new Fixture(593);
        missing.Set(100, a => a with { PartySlot = -1 });
        missing.Sync();
        missing.Cast(1, 7089, 999);
        Check(missing.E.BlackRiderKnockback!.TargetSlot == -1
            && missing.E.BlackRiderLanding(missing.Actors.Single(a => a.Id == 100)) == missing.Actors.Single(a => a.Id == 100).Position
            && !missing.E.AmbientAoes().Any(a => a.Id.StartsWith("blackrider/kb/")), "Negative capture consumed by another negative slot.");

        var state = new PalaceYaquaruHeavy();
        var a = new DungeonActor(100, 0, Center, PartySlot: 0,
            StatusSlots: [new(14, 0, 1, Start.AddSeconds(20)) { SlotIndex = 2 }]);
        state.Observe([a], new Dictionary<ulong, int> { [100] = 0 }, Start);
        var b = a with { Id = 101 };
        state.Observe([b], new Dictionary<ulong, int> { [101] = 0 }, Start);
        Check(state.ShouldCleanse == 1, "Absent old actor Lose cleared new Heavy in the same slot.");
        a = a with { StatusSlots =
            [new(14, 0, 2, Start.AddSeconds(2)) { SlotIndex = 12 }, new(14, 0, 1, Start.AddSeconds(10)) { SlotIndex = 2 }] };
        var roster = new Dictionary<ulong, int> { [100] = 0 };
        state = new();
        state.Observe([a], roster, Start);
        state.Lose(100, 0, a.StatusSlots, 2);
        state.Observe([a], roster, Start);
        Check(state.ShouldCleanse == 1, "Losing second Heavy suppressed surviving first status.");
        a = a with { StatusSlots =
            [new(14, 0, 2, Start.AddSeconds(20)) { SlotIndex = 12 }, new(14, 0, 1, Start.AddSeconds(7)) { SlotIndex = 2 }] };
        state = new();
        state.Observe([a], roster, Start);
        Check(state.ShouldCleanse == 0, "Raw native SlotIndex order ignored.");
        a = a with { StatusSlots =
            [new(14, 0, 1, Start.AddSeconds(20)) { SlotIndex = 2 }, new(14, 0, 2, Start.AddSeconds(20)) { SlotIndex = 12 }] };
        state = new();
        state.Observe([a], roster, Start);
        state.Lose(100, 0, a.StatusSlots, 1);
        state.Lose(100, 0, a.StatusSlots, 2);
        state.Observe([a], roster, Start);
        Check(state.ShouldCleanse == 0, "Second Lose resurrected the first lost Heavy from a stale snapshot.");
        state.Observe([a with { StatusSlots = [a.StatusSlots[0]] }], roster, Start);
        Check(state.ShouldCleanse == 0, "Acknowledging one loss resurrected another stale native slot.");
        a = a with { StatusSlots =
            [new(14, 0, 1, Start.AddSeconds(20)) { SlotIndex = 2 }, new(14, 0, 2, Start.AddSeconds(2)) { SlotIndex = 12 }] };
        state = new();
        state.Observe([a], roster, Start);
        state.Lose(100, 0, a.StatusSlots, 1);
        state.Gain(100, 2);
        state.Observe([a], roster, Start);
        Check(state.ShouldCleanse == 0, "Other-source Gain resurrected a lost long Heavy.");
        state.Gain(100, 1);
        state.Observe([a], roster, Start);
        Check(state.ShouldCleanse == 1, "Matching-source Gain failed to release the corresponding loss.");
    }

    private static void NativeOutput()
    {
        foreach (var territory in new uint[] { 593, 594 })
        {
            var f = new Fixture(territory, pool: false);
            if (territory == 593) f.Actors.Add(new(10, 0x1E858E, Center + Vector3.UnitX * 15, EventState: 7));
            f.Sync();
            f.Cast(1, territory == 593 ? 7089u : 7091u, 100);
            using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
            var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
            output.Apply(f.E, Start, false);
            var snapshot = safe.Manager.BuildSnapshot(Start);
            var player = f.Actors.Single(a => a.Id == 100).Position;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0,
                "Real solver failed native direction/poison escape.");
            var destination = path[^1].Position;
            Check(f.E.AmbientAoes().Where(a => a.Navigate && !a.Safe).All(a =>
                !a.Shape.Resolve(a.Position, a.Rotation, null).Contains(destination)),
                "Solver's final position violates a migrated native constraint.");
            Check(snapshot.GoalAreas.Count == 0, "Weak/binary pull became an automatic goal.");
            output.Clear();
            Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Owner clear retained migrated navigation.");
        }
    }

    private static void RestoredRaw()
    {
        foreach (var viewer in new ulong[] { 100, 999 })
        {
            var f = new Fixture(593, pool: false);
            f.Cast(1, 7089, 100);
            f.E.ResetFight();
            f.Set(1, a => a with { Cast = a.Cast! with { TargetId = 101 } });
            f.Sync(viewer);
            Check(f.E.BlackRiderCharges.Single().Target.Actor.Id == 101
                && f.E.BlackRiderKnockback!.TargetSlot == 1,
                $"Reset raw retarget: viewer={viewer}, target={f.E.BlackRiderCharges.Single().Target.Actor.Id}, slot={f.E.BlackRiderKnockback?.TargetSlot}.");
        }
        var late = new Fixture(593, pool: false);
        var cast = new DungeonCastObservation(7089, Center, 0, Start, Start.AddSeconds(4.3), 100, Center, 4);
        late.E.StartCast(1, cast, Start, fromPacket: true);
        late.Now = Start.AddSeconds(4.4);
        late.Set(1, a => a with { Cast = cast });
        late.Sync();
        var expected = late.Now.AddSeconds((4f + .3f) - 4f);
        Check(late.E.BlackRiderKnockback!.Cast.FinishAt == expected
            && late.E.BlackRiderCharges.Single().Cast.FinishAt == expected,
            "First raw NPC saturation retained the expired packet clock.");
    }
}
