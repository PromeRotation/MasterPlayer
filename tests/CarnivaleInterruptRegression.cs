using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleInterruptRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale interrupt stages exact component ownership and no enumerated auto effects", Ownership);
        run("Carnivale primary death phases use targetable primary pull and HP0 death or destroy completion", PrimaryPhases);
        run("Carnivale Stage08 Act2 trash pull ignores HP0 and completes only all dead or destroyed", GroupPhase);
        run("Carnivale interrupt components activate on pull restore ongoing casts and keep full observed generations", Interrupts);
        run("Carnivale interrupt cancel unchanged raw late old packet unknown replacement and new generation", Cancel);
        run("Carnivale interrupt intent preserves caster references generation correction and enemy projection independently", Intent);
        run("Carnivale display handoff preserves background cancel receipts and packet grace", Handoff);
        run("Carnivale raw generation replacement rejects older queued finish for AOEs and interrupt intents", LateFinish);
        run("Carnivale Stage10 four exact locked cone stages and cloudcover independent geometry", Shapes);
        run("Carnivale Stage08 Act2 precise two corner holes thin walls asymmetric inset and display boundary", Arena);
        run("Carnivale interrupt stage priorities preserve pointless and all unenumerated valid enemies", Priority);
        run("Carnivale Stage08 Act2 constructor bomb histories destroyed freeze death and revival", Bombs);
        run("Carnivale interrupt production output is navigation for AOEs only with independent display and owner cleanup", Output);
        run("Carnivale interrupt stage hints markers selection disable and wipe preserve source scope", Hints);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid, bool combat = false)
        {
            Actors =
            [
                new(1, oid, Center, InCombat: combat, Name: "Named Boss", Hp: 1000, IsEnemy: true, ObjectIndex: 2),
                new(100, 0, Center, PartySlot: 0, Player: true),
            ];
            Sync();
        }
        internal void Sync(Func<string, bool>? enabled = null) => E.Synchronize(Actors, Center, Now, enabled, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Raw(uint action, double start, double finish, Vector3? location = null, float rotation = 0)
        {
            Set(1, a => a with { Cast = new(action, location ?? Center, rotation,
                Start.AddSeconds(start), Start.AddSeconds(finish), 100) });
            Sync();
        }
    }

    private static void Ownership()
    {
        foreach (var stage in new[]
        {
            (Oid: 0x25D5u, Actions: new uint[] { 14366, 14368, 14369 }),
            (Oid: 0x270Bu, Actions: new uint[] { 14708, 14680 }),
            (Oid: 0x2717u, Actions: new uint[] { 14725, 14726, 14727, 14728, 14722, 14719, 14720, 14721, 14724 }),
        })
        {
            Check(CarnivaleCatalog.Owns(796, stage.Oid) && !CarnivaleCatalog.Owns(795, stage.Oid), "Ownership.");
            var f = new Fixture(stage.Oid, true);
            Check(f.E.Active!.Casts.Select(c => c.Action).Order().SequenceEqual(stage.Actions.Order()), "Activated components.");
            foreach (var action in new uint[] { 6497, 6499, 14362, 14730, 14731, 14723 })
                f.E.StartCast(1, new(action, Center, 0, Start, Start.AddSeconds(5)), Start);
            Check(f.E.Casts.Count == 0 && !f.E.PredictedDamage.Any(), "Enumeration added unsupported mechanism.");
        }
    }

    private static void PrimaryPhases()
    {
        foreach (var oid in new uint[] { 0x25D5, 0x2717 })
        {
            var f = new Fixture(oid);
            f.Actors.Add(new(10, 0x25D6, Center, InCombat: true, Hp: 1000));
            f.Sync();
            Check(f.E.Active == null, "Add pulled primary-only module.");
            f.Set(1, a => a with { InCombat = true, Targetable = false });
            f.Sync();
            Check(f.E.Active == null, "Untargetable primary pulled.");
            f.Set(1, a => a with { Targetable = true });
            f.Sync();
            Check(f.E.Active?.BossOid == oid, "Primary did not pull.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null && f.E.CarnivalePrecombatHint == null, "HP0 did not complete default death phase.");
            f.Set(1, a => a with { Hp = 1000 });
            f.Sync();
            Check(f.E.Active == null, "Completed owner re-created without birth.");
            foreach (var destroy in new[] { false, true })
            {
                var g = new Fixture(oid, true);
                g.Actors.Add(new(10, 0x25D6, Center, InCombat: true));
                if (destroy) g.Actors.RemoveAll(a => a.Id == 1);
                else g.Set(1, a => a with { Dead = true });
                g.Sync();
                Check(g.E.Active == null, "Default death/destroy waited for irrelevant adds.");
            }
        }
    }

    private static void GroupPhase()
    {
        var f = new Fixture(0x270B);
        f.Actors.Add(new(10, 0x270C, Center, InCombat: true, Hp: 1000));
        f.Sync();
        Check(f.E.Active?.BossOid == 0x270B, "Bomb did not pull.");
        f.Set(1, a => a with { Hp = 0, Dead = true });
        f.Sync();
        f.Set(10, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active != null, "HP0 treated as Dead.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null, "Destroyed primary prematurely completed trash.");
        f.Set(10, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "AllDeadOrDestroyed did not complete.");
    }

    private static void Interrupts()
    {
        foreach (var stage in new[]
        {
            (Oid: 0x25D5u, Actions: new uint[] { 14369 }),
            (Oid: 0x270Bu, Actions: new uint[] { 14680 }),
            (Oid: 0x2717u, Actions: new uint[] { 14719, 14720, 14721, 14724 }),
        })
        {
            foreach (var action in stage.Actions)
            {
                var f = new Fixture(stage.Oid);
                f.Raw(action, 0, 6);
                Check(!f.E.CarnivaleInterruptCasts.Any() && !f.E.Hints(Center).Contains("Interrupt!"), "Pre-pull component activated.");
                f.Set(1, a => a with { InCombat = true });
                f.Sync();
                var cast = f.E.CarnivaleInterruptCasts.Single();
                Check(cast.Source == 1 && cast.TargetId == 100 && cast.Rule.Action == action
                    && cast.StartedAt == Start && cast.FinishAt == Start.AddSeconds(6) && cast.UntilObservedEnd,
                    "Existing cast bootstrap lost identity, preparation or deadline.");
                Check(f.E.Hints(Center).Contains("Interrupt!") && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null)
                    && !f.E.PredictedDamage.Any(), "Interrupt became damage or AOE.");
                f.E.Effect(1, action, 1, Start.AddSeconds(6), 100, Center);
                f.Now = Start.AddSeconds(10);
                f.Sync();
                Check(f.E.CarnivaleInterruptCasts.Single().FinishAt == cast.FinishAt, "Effect/time ended interrupt component.");
                f.E.Finish(1, action + 100, f.Now);
                Check(f.E.CarnivaleInterruptCasts.Any(), "Wrong action ended component.");
                f.Set(1, a => a with { Cast = null });
                f.Sync();
                Check(!f.E.CarnivaleInterruptCasts.Any() && !f.E.Hints(Center).Contains("Interrupt!"), "Observed Finish leaked intent.");
            }
        }
    }

    private static void Cancel()
    {
        var f = new Fixture(0x2717, true);
        f.Raw(14719, 0, 6);
        f.E.Finish(1, 14719, Start.AddSeconds(1));
        f.Now = Start.AddSeconds(1.5);
        f.Sync();
        Check(!f.E.CarnivaleInterruptCasts.Any(), "Unchanged raw resurrected cancelled cast.");
        f.Raw(6497, 1.5, 7.5);
        f.Raw(14720, 2, 8);
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 14720, "Unknown-to-known replacement not restored.");
        f.E.StartCast(1, new(14719, Center, 0, Start, Start.AddSeconds(6)), f.Now);
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 14720, "Late old packet displaced new generation.");
        f.Now = Start.AddSeconds(2.5);
        f.Raw(14720, 2.5, 8.5);
        Check(f.E.CarnivaleInterruptCasts.Single().StartedAt == Start.AddSeconds(2.5), "Same action new generation ignored.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(!f.E.CarnivaleInterruptCasts.Any(), "Destruction retained old caster intent.");
        f.Actors.Add(new(1, 0x2717, Center, InCombat: true, Cast: new(14724, Center, 0,
            Start.AddSeconds(3), Start.AddSeconds(9)), Hp: 1000));
        f.Now = Start.AddSeconds(3);
        f.Sync();
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 14724, "Same ID new birth reused old cast.");
    }

    private static void Shapes()
    {
        var f = new Fixture(0x2717, true);
        foreach (var (action, radius) in new[] { (14725u, 9f), (14726u, 9.5f), (14727u, 10f), (14728u, 10.5f) })
        {
            f.Now = f.Now.AddSeconds(4);
            var at = (f.Now - Start).TotalSeconds;
            var location = Center + Vector3.UnitX * 2;
            f.Raw(action, at, at + 2.5, location, MathF.PI / 2);
            var cast = f.E.OutputCasts.Single().Value;
            Check(cast.Rule.Shape is FanTemplate fan && fan.Radius == radius
                && MathF.Abs(fan.AngleRadians - MathF.PI * 2 / 3) < .00001f, "Cone dimensions drifted.");
            Check(cast.Location == location && cast.Rotation == MathF.PI / 2, "Source lock lost.");
            Check(cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null).Contains(location + Vector3.UnitX * (radius - .1f))
                && !cast.Rule.Shape.Resolve(cast.Location, cast.Rotation, null).Contains(location - Vector3.UnitX),
                "Locked cone orientation.");
        }
        var g = new Fixture(0x25D5, true);
        g.Raw(14366, 0, 1.5);
        var rect = g.E.Casts[1].Rule.Shape!.Resolve(Center, 0, null);
        Check(rect.Contains(Center + new Vector3(1.99f, 0, 77.49f))
            && !rect.Contains(Center + new Vector3(2.01f, 0, 1)) && !rect.Contains(Center - Vector3.UnitZ),
            "GrandStrike halfwidth 2 and forward 77.5.");
    }

    private static void Handoff()
    {
        foreach (var cancelled in new[] { false, true })
        {
            var f = new Fixture(0x25D5, true);
            f.Actors.Add(new(2, 0x2717, Center, InCombat: true, Hp: 1000,
                Cast: cancelled ? new(14719, Center, 0, Start, Start.AddSeconds(6)) : null));
            f.Sync();
            Check(f.E.Active?.BossOid == 0x25D5, "Background fixture selected wrong module.");
            f.Now = Start.AddSeconds(1);
            if (cancelled)
                f.E.Finish(2, 14719, f.Now);
            else
                f.E.StartCast(2, new(14719, Center, 0, f.Now, f.Now.AddSeconds(6)), f.Now);
            f.Set(1, a => a with { Hp = 0 });
            f.Now = Start.AddSeconds(1.05);
            f.Sync();
            Check(f.E.Active?.BossOid == 0x2717, "Selection did not hand off.");
            Check(f.E.CarnivaleInterrupts.Any() != cancelled,
                cancelled ? "Handoff resurrected cancelled unchanged background raw."
                    : "Handoff cleared a valid unobserved packet inside the 0.2 second grace.");
            if (!cancelled)
                Check(!f.E.CarnivaleInterruptCasts.Single().Observed, "Packet became a fabricated raw observation.");
        }
    }

    private static void LateFinish()
    {
        foreach (var action in new uint[] { 14719, 14725 })
        {
            var f = new Fixture(0x2717, true);
            f.Raw(action, 0, 6);
            f.Now = Start.AddSeconds(1);
            f.Raw(action, 0, 6, Center + Vector3.UnitZ);
            var accepted = f.E.Casts[1];
            Check(accepted.FinishEventBoundary == f.Now, "Raw replacement boundary not retained.");
            f.E.Finish(1, action, Start.AddSeconds(.5));
            f.Sync();
            Check(f.E.Casts[1] == accepted, "Queued old Finish consumed current generation.");
            f.E.Finish(1, action, Start.AddSeconds(1.1));
            Check(!f.E.Casts.Any(), "Current generation Finish not accepted.");
        }
        var delayed = new Fixture(0x2717, true);
        delayed.Now = Start.AddSeconds(2);
        delayed.E.StartCast(1, new(14719, Center, 0, Start, Start.AddSeconds(6)), delayed.Now);
        delayed.E.Finish(1, 14719, Start.AddSeconds(1));
        Check(!delayed.E.CarnivaleInterrupts.Any(), "Delayed same-generation packet Cancel was rejected by processing time.");
        delayed.Raw(14719, 0, 6);
        Check(!delayed.E.CarnivaleInterrupts.Any(), "Delayed accepted Cancel failed to suppress stale raw.");
    }

    private static void Intent()
    {
        var f = new Fixture(0x2717, true);
        f.Raw(14719, 0, 6);
        var original = f.E.CarnivaleInterrupts.Single();
        Check(original.ShouldBeInterrupted && !original.ShouldBeStunned, "Default flags.");
        f.E.StartCast(1, new(14719, Center + Vector3.UnitX, 0, Start.AddSeconds(.1), Start.AddSeconds(6.1), 100), Start);
        var corrected = f.E.CarnivaleInterrupts.Single();
        Check(corrected.Generation == original.Generation && ReferenceEquals(corrected.Caster, original.Caster)
            && corrected.Cast.Location == Center + Vector3.UnitX, "Same packet generation correction became a new intent.");
        f.Raw(14719, 0, 6, Center + Vector3.UnitZ);
        var replaced = f.E.CarnivaleInterrupts.Single();
        Check(replaced.Generation > original.Generation && ReferenceEquals(replaced.Caster, original.Caster),
            "Same time raw replacement did not create a generation.");
        f.Set(1, a => a with { Hp = 1 });
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Single().ShouldBeInterrupted, "Pointless removed enemy flag.");
        f.Set(1, a => a with { Targetable = false });
        f.Sync();
        Check(!f.E.CarnivaleInterrupts.Single().ShouldBeInterrupted && f.E.Hints(Center).Contains("Interrupt!"),
            "Enemy projection removed caster or kept an invalid enemy flag.");
        f.Set(1, a => a with { Targetable = true });
        f.Actors.Add(new(20, 0x9999, Center, Dead: false, InCombat: true,
            Cast: new(14719, Center, 0, Start, Start.AddSeconds(6)), Hp: 1000, IsEnemy: true, ObjectIndex: 4));
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Count() == 2, "Concurrent caster list lost.");
        f.Set(20, a => a with { Dead = true });
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Count() == 2 && !f.E.CarnivaleInterrupts.Single(c => c.Cast.Source == 20).ShouldBeInterrupted,
            "Death cleared observed cast or retained enemy flag.");
        f.E.Finish(1, 14719, Start.AddSeconds(1));
        Check(f.E.CarnivaleInterrupts.Single().Cast.Source == 20 && f.E.Hints(Center).Contains("Interrupt!"),
            "Finish one concurrent caster consumed another.");
    }

    private static void Arena()
    {
        var f = new Fixture(0x270B);
        Check(f.E.Arena == null && f.E.AmbientAoes().Any(a => a.Id == "carnivale/precombat/arena" && !a.Navigate), "Preview gated.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        var arena = f.E.Arena!;
        Check(arena.GetContours().Count() == 3, "Two corners holes lost.");
        foreach (var (x, z, inside) in new[]
        {
            (100f, 94.75f, true), (90f, 94.75f, false), (94.75f, 92f, false),
            (90f, 95.49f, false), (90f, 95.51f, true), (94.01f, 92f, false), (93.99f, 92f, true),
            (95.5003f, 90f, false), (95.501f, 90f, true), (95.4997f, 94f, true), (95.499f, 94f, false),
            (124.4993f, 100f, true), (124.4995f, 100f, false),
        })
            Check(arena.Contains(new(x, 0, z)) == inside, $"Thin wall/inset probe {x},{z}.");
    }

    private static void Priority()
    {
        foreach (var oid in new uint[] { 0x25D5, 0x270B })
        {
            var f = new Fixture(oid, true);
            f.Actors.Add(new(10, 0x25D6, Center + Vector3.UnitZ * 20, IsEnemy: true, ObjectIndex: 4, Hp: 1000));
            f.Actors.Add(new(11, 0x9999, Center, IsEnemy: true, ObjectIndex: 198, Hp: 1000));
            f.Sync();
            var preferred = oid == 0x25D5 ? 10ul : 1ul;
            Check(f.E.PriorityTarget(11, Center) == preferred, "Source priority override lost.");
            f.Set(preferred, a => a with { Hp = 1 });
            f.Sync();
            Check(f.E.PriorityTarget(preferred, Center) != preferred, "Pointless priority overridden.");
            f.Set(1, a => a with { Targetable = false });
            f.Set(10, a => a with { Targetable = false });
            f.Sync();
            Check(f.E.PriorityTarget(0, Center) == 11, "Unenumerated enemy priority omitted.");
        }
    }

    private static void Bombs()
    {
        var f = new Fixture(0x270B);
        f.Actors.Add(new(10, 0x270C, Center + Vector3.UnitZ * 10));
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Sync();
        f.Actors.Add(new(10, 0x270D, Center + Vector3.UnitZ * 20));
        f.Sync();
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/explosion/")) == 2, "Original reference rebound.");
        f.Set(10, a => a with { Dead = true });
        f.Sync();
        f.Set(10, a => a with { Dead = false });
        f.Sync();
        var old = f.E.AmbientAoes().Single(a => a.Id.StartsWith("carnivale/explosion/"));
        Check(old.Position == Center + Vector3.UnitZ * 10 && !old.Navigate && old.OutlineOnly
            && old.Shape is CircleTemplate { Radius: 6 }, "Death/revival or freeze lost source contract.");
    }

    private static void Output()
    {
        var f = new Fixture(0x270B, true);
        f.Raw(14708, 0, 5, Center + Vector3.UnitZ * 5);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var draw = new ScriptDrawApi();
        var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, Start, false);
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 1, "Sap did not enter actual navigation.");
        f.Now = Start.AddSeconds(6);
        f.Raw(14680, 6, 12);
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0 && f.E.CarnivaleInterruptCasts.Any(),
            "Interrupt became dangerous navigation or old Sap leaked.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(f.Now).ArenaBoundary == null, "Owner boundary leaked.");
    }

    private static void Hints()
    {
        var f = new Fixture(0x2717);
        Check(f.E.CarnivalePrecombatHint!.Contains("Named Boss") && f.E.Hints(Center).Any(h => h.Contains("achievement")),
            "Constructor Name or achievement hint lost.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(!f.E.Hints(Center).Any(h => h.Contains("achievement") || h.Contains("Flying Sardine")),
            "Hints survived phase deactivation.");
        f.Raw(14719, 0, 6);
        f.Sync(_ => false);
        Check(f.E.Active == null && !f.E.CarnivaleInterruptCasts.Any() && !f.E.AmbientAoes().Any(),
            "Mechanism off leaked output.");
        f.Set(1, a => a with { Cast = null });
        f.Sync(_ => false);
        f.Sync();
        Check(!f.E.CarnivaleInterruptCasts.Any(), "Stopped raw did not converge after re-enabling module.");
        f.E.DirectorUpdate(0x40000005);
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active == null && !f.E.CarnivaleInterruptCasts.Any(), "Wipe recreated owner without birth.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
