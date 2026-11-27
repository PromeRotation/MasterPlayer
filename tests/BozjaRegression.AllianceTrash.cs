using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunAllianceTrash(Action<string, Action> run)
    {
        RunDeathgaze(run);
        RunArachne(run);
        RunIrminsul(run);
        RunOzma(run);
        RunAllianceKnockbackPilot(run);
        RunAllianceStandardPilot(run);
        RunRhalgr(run);
        RunHeavyArtillery(run);
        RunLightArtillery(run);
        foreach (var definition in AllianceTrashCatalog.Definitions.Append(AllianceTrashCatalog.Lions))
        {
            run($"Alliance trash source {definition.Key} true arena and multi actor lifecycle", () => AllianceLifecycle(definition));
            foreach (var rule in definition.Rules.Where(r => r.Component is not ("ScraplineTyphoon" or "Flood")))
                foreach (var action in rule.Actions)
                    run($"Alliance trash source {definition.Key} {rule.Component}/{action} hot cold Finish", () =>
                        AllianceStandard(definition, rule, action));
        }
        run("Alliance trash source A30Trash1 dead serpent retained P1 until Destroy all sources", AllianceSerpentPhase);
        run("Alliance trash source Despot any add pull locked FIFO two clocks cross source Finish no TTL", AllianceDespot);
        run("Alliance trash source Despot pull raw endpoints immunity60 first unsafe no arena check", AlliancePull);
        run("Alliance trash source same OID reverse pending three primaries next frame pack1 handoff", AlliancePending);
        run("Alliance trash source pack2 original destroyed primary all OID and same ID replacement", AllianceDestroyedPrimary);
        run("Alliance trash source grouped 98 99 100 casters exact ordering and unrelated Finish", AllianceGroupedOrder);
        run("Alliance trash source Kraken grouped four actions 98 99 100 unrelated Finish", () => AllianceGroupedOrder(true));
        run("Alliance trash source raw zero tiny NPC clocks self target fallback and capped predictions", AllianceClocks);
        run("Alliance trash source Kraken Flood overwrite cross source Finish raw clock no TTL", AllianceKraken);
        run("Alliance trash source Kraken two raidwides later caster earlier clock normalized order", AllianceKrakenDamageOrder);
        run("Alliance trash source Lions two hidden sorted queues cross source and actor ID Finish", AllianceLionsQueues);
        run("Alliance trash source Lions cached original lioness replacement HP0 completion and permanent timeline", AllianceLionsLifecycle);
        run("Alliance trash source Lions hidden duplicate Roar first ID match and cross generation Finish", AllianceLionsRoarGeneration);
        run("Alliance trash source Lions raw duplicate raidwide non Spell then null default", AllianceLionsRawDamage);
        run("Alliance trash source gaze default10000 exact threshold and original locked eye", AllianceGaze);
        run("Alliance trash source duplicate original tankbuster observes current non Spell NPC clock", AllianceRawDamage);
    }

    private static void AllianceActivatePhase(CastrumFixture f, BozjaRule rule)
    {
        if (f.Native.Definition.Key != "A30Trash1" || rule.Component == "WaterIII") return;
        f.Actors.Add(Cll1Fixture.Actor(22, 0x4011, f.Center));
        f.Change(1, a => a with { Dead = true });
        f.Frame(.5);
        Check(f.Native.AlliancePhase == 1, "Alliance real P1 entered");
    }

    private static void AllianceKraken()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions.Single(d => d.Key == "A20Kraken"));
        f.Actors.Add(Cll1Fixture.Actor(22, 0x488B, f.Center));
        f.Change(100, a => a with { Position = f.Center + new Vector3(1.237f, 0, 25.713f),
            Statuses = [new ReplayStatus(1209, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 }] });
        f.Change(20, a => a with { Cast = f.Spell(43791, total: .02f) }); f.Frame(0);
        var flood = f.Native.Casts.Single(c => c.Rule.Component == "Flood");
        Check(flood.Activation == START.AddSeconds((double)(.02f + .3f) + 3.5d), "Kraken exact Flood double delay");
        Check(!f.Native.AlliancePullMovements(f.NativeViewer).Any()
            && f.Native.Navigation(f.NativeViewer).Any(z => z.Component == "Sucker"), "Kraken immune display retains custom first source AI");
        f.Change(100, a => a with { Statuses = [] }); f.Frame(.1);
        Check(f.Native.AlliancePullMovements(f.NativeViewer).Count() == 1, "Kraken movement restores on real Lose");
        f.Change(22, a => a with { Cast = f.Spell(43791, at: 1, point: f.Center + Vector3.UnitX * 7) }); f.Frame(1);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "Flood") == 1
            && f.Native.Casts.Single(c => c.Rule.Component == "Flood").Source.Actor.Id == 22, "Kraken second Start replaces Flood only");
        f.Change(20, a => a with { Cast = null }); f.Change(22, a => a with { Cast = null }); f.Frame(2);
        f.Frame(100);
        Check(f.Native.Casts.Single().Rule.Component == "Flood", "Kraken Sucker Finish and timeout preserve Flood");
        f.Change(20, a => a with { Cast = f.Spell(43792, at: 101) }); f.Frame(101);
        Check(f.Native.Casts.Count == 1, "Kraken Flood Start does not create predicted Flood");
        f.Change(20, a => a with { Cast = null }); f.Frame(102);
        Check(f.Native.Casts.Count == 0, "Kraken any source Flood Finish clears overwritten item");

        f.Change(20, a => a with { Cast = f.Spell(43566, at: 103, total: 0) }); f.Frame(103);
        var component = f.Source!.Components.Single(c => c.GetType().Name == "ImpactRoar");
        var caster = f.Session.World.Actors.Find(20)!;
        component.OnCastStarted(caster, caster.CastInfo!);
        typeof(BozjaEncounter).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native,
                [f.Native.Actors[20], f.Native.Definition.Rules.Single(r => r.Component == "ImpactRoar")]);
        f.Compare();
        f.Change(20, a => a with { Cast = f.Spell(999, at: 104, total: .02f) with { IsSpell = false } }); f.Frame(104);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == START.AddSeconds(104).AddSeconds(.02f + .3f),
            "Kraken original raidwide observes raw non Spell CastInfo");
        f.Frame(105);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == START.AddSeconds(105).AddSeconds((.02f + .3f) - .02f),
            "Kraken original raidwide capped raw float clock");
        f.Change(20, a => a with { Cast = null }); f.Frame(106);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == default,
            "Kraken null CastInfo retains residual raidwide diagnostic with source default fallback");
    }

    private static void AllianceKrakenDamageOrder()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions.Single(d => d.PrimaryOid == 0x4885));
        f.Change(20, a => a with { Cast = f.Spell(43566, total: 10) }); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x488B, f.Center) with { Cast = f.Spell(43566, at: 1, total: 1) }); f.Frame(1);
        var damage = f.Native.DamageForHost(f.NativeViewer);
        Check(damage.Length == 2 && damage[0].Activation < damage[1].Activation,
            "Kraken actual source normalized ascending damage clocks");
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.DamageForHost(f.NativeViewer).Length == 1, "Kraken original first caster Finish leaves second only");
    }

    private static void AllianceLionsQueues()
    {
        foreach (var component in new[] { "SlashAndBurn", "RoaringBlaze" })
        {
            using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Lions);
            f.Frame(0);
            var rule = f.Native.Definition.Rules.Single(r => r.Component == component);
            for (var i = 0; i < 19; ++i)
            {
                var id = (ulong)(200 + i);
                f.Actors.Add(Cll1Fixture.Actor(id, 0x233C, f.Center) with
                {
                    Cast = f.Spell(rule.Actions[i % rule.Actions.Length], 1,
                        total: i % 3 == 0 ? 2 : 5, point: f.Center + new Vector3(i * .123f, 0, i * .317f)),
                });
            }
            f.Frame(1);
            void CompareQueue()
            {
                var source = f.Source!.Components.Single(c => c.GetType().Name == component);
                var expected = (List<BossMod.Components.GenericAOEs.AOEInstance>)source.GetType()
                    .GetField("_aoes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(source)!;
                var actual = f.Native.Casts.Where(c => c.Rule == rule).ToArray();
                Check(expected.Count == actual.Length && f.Native.Visible(f.NativeViewer).Count() == 1, "Lions full hidden queue count");
                for (var i = 0; i < expected.Count; ++i)
                    Check(expected[i].Origin.ToVec3() == actual[i].Origin && expected[i].Rotation.Rad == actual[i].Rotation
                        && expected[i].Activation == actual[i].Activation, "Lions source right pivot equal clock order " + i);
            }
            CompareQueue();
            var oldHead = f.Native.Casts.First(c => c.Rule == rule);
            var sourceActor = f.Session.World.Actors.Find(218)!;
            var otherSpell = new ActorCastInfo { Action = new(ActionType.Spell, rule.Actions[0]), Location = new(123, 0, 456) };
            f.Source!.Components.Single(c => c.GetType().Name == component).OnCastFinished(sourceActor, otherSpell);
            f.Native.Finish(f.Native.Actors[218], f.Spell(rule.Actions[0])); f.Compare();
            CompareQueue();
            if (component == "SlashAndBurn")
                Check(!f.Native.Casts.Contains(oldHead), "Lions Slash any matching source consumes global head");
            else
                Check(!f.Native.Casts.Any(c => c.Rule == rule && c.Source.Actor.Id == 218), "Lions Roar deletes matching caster beyond visible head");
            f.Frame(1000); CompareQueue();
        }
    }

    private static void AllianceLionsLifecycle()
    {
        using (var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.Lions))
        {
            f.Frame(0);
            f.Actors.Add(Cll1Fixture.Actor(22, 0x38DB, f.Center));
            f.Change(1, a => a with { InCombat = true, Dead = true }); f.Frame(1);
            Check(!f.Native.Active && f.Native.AllianceLioness == null, "Lions completion before first UpdateModule cache");
        }
        using (var f = new CastrumFixture(alliance: AllianceTrashCatalog.Lions))
        {
            f.Actors.Add(Cll1Fixture.Actor(22, 0x38DB, f.Center));
            f.Frame(0);
            var original = f.Native.AllianceLioness;
            Check(original?.Actor.Id == 22, "Lions first alive original Lioness cached");
            f.Change(1, a => a with { Hp = 0, InCombat = false, Targetable = false }); f.Frame(1);
            var timeline = f.Native.LionsTimeline!.Capture()!;
            Check(f.Native.Active && timeline.Entries.Single().Name == "???", "Lions HP0 preserves single phase timeline");
            f.Actors.RemoveAll(a => a.Id == 22); f.Frame(2);
            f.Actors.Add(Cll1Fixture.Actor(22, 0x38DB, f.Center)); f.Frame(3);
            Check(ReferenceEquals(original, f.Native.AllianceLioness) && original!.Destroyed,
                "Lions does not replace destroyed original cached Actor");
            f.Frame(1000);
            Check(f.Native.Active && f.Native.LionsTimeline!.Running && f.Native.LionsTimeline.StateId == 0,
                "Lions 600 expected duration is permanent SimpleState");
            f.Change(1, a => a with { Dead = true }); f.Frame(1001);
            Check(!f.Native.Active && !f.Native.LionsTimeline!.Running, "Lions dead original pair completes despite new living same ID lioness");
        }
    }

    private static void AllianceLionsRoarGeneration()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Lions);
        f.Change(20, a => a with { Cast = f.Spell(29134, total: 5) });
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(29135, total: 1) }); f.Frame(0);
        var rule = f.Native.Definition.Rules.Single(r => r.Component == "RoaringBlaze");
        var component = f.Source!.Components.Single(c => c.GetType().Name == "RoaringBlaze");
        var original = f.Native.Actors[20];
        var sourceOriginal = f.Session.World.Actors.Find(20)!;
        for (var i = 0; i < 2; ++i)
        {
            component.OnCastStarted(sourceOriginal, sourceOriginal.CastInfo!);
            typeof(BozjaEncounter).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native, [original, rule]);
        }
        f.Compare();
        var finish = new ActorCastInfo { Action = new(ActionType.Spell, 29375) };
        component.OnCastFinished(sourceOriginal, finish);
        f.Native.Finish(original, f.Spell(29375)); f.Compare();
        Check(f.Native.Casts.Count(c => c.Rule == rule && c.Source.Actor.Id == 20) == 2, "Lions hidden duplicate removes only first matching ID");
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(1);
        Check(f.Native.Casts.Count(c => c.Rule == rule && c.Source.Actor.Id == 20) == 1, "Lions Destroy Finish consumes one original entry");
        f.Actors.Add(Cll1Fixture.Actor(20, 0x233C, f.Center)); f.Frame(2);
        Check(!ReferenceEquals(original, f.Native.Actors[20]), "Lions new source generation differs from original");
        component.OnCastFinished(f.Session.World.Actors.Find(20)!, finish);
        f.Native.Finish(f.Native.Actors[20], f.Spell(29375)); f.Compare();
        Check(f.Native.Casts.Single(c => c.Rule == rule).Source.Actor.Id == 22, "Lions new generation Finish consumes old ID residue only");
    }

    private static void AllianceLionsRawDamage()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Lions);
        f.Change(20, a => a with { Cast = f.Spell(29143, total: 0) }); f.Frame(0);
        var rule = f.Native.Definition.Rules.Single(r => r.Component == "DoubleImmolation");
        var component = f.Source!.Components.Single(c => c.GetType().Name == "DoubleImmolation");
        var caster = f.Session.World.Actors.Find(20)!;
        component.OnCastStarted(caster, caster.CastInfo!);
        typeof(BozjaEncounter).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native, [f.Native.Actors[20], rule]);
        f.Compare();
        f.Change(20, a => a with { Cast = f.Spell(999, at: 1, total: .02f) with { IsSpell = false } }); f.Frame(1);
        f.Frame(2);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == START.AddSeconds(2).AddSeconds((.02f + .3f) - .02f),
            "Lions own original caster raw non Spell capped clock");
        f.Change(20, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == default
            && f.Native.GlobalHints().Single() == "Raidwide", "Lions own null CastInfo default diagnostic and original global hint");
    }

    private static void AllianceLifecycle(BozjaDefinition definition)
    {
        using var f = new CastrumFixture(false, alliance: definition); f.Frame(0);
        Check(!f.Native.Active, "Alliance not pulled without combat");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.Active, "Alliance targetable combat starts");
        var source = f.Source!;
        var raised = (f.Native.Arena as MasterPlayer.Spatial.Geometry.PolygonRegionGeometry)?.AtHeight(17.25f);
        if (raised != null)
            Check(raised.GetContours().SelectMany(c => c).All(p => p.Y == 17.25f)
                && raised.FillPolygons.SelectMany(c => c).All(p => p.Y == 17.25f), "Alliance raised boundaries and fill retain height");
        for (var x = f.Center.X - definition.SampleRadius - 1; x <= f.Center.X + definition.SampleRadius + 1; x += .617f)
            for (var z = f.Center.Z - definition.SampleRadius - 1; z <= f.Center.Z + definition.SampleRadius + 1; z += .673f)
            {
                Check(source.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Alliance resolved polygon islands holes");
                if (raised != null)
                    Check(source.Arena.InBounds(new(x, z)) == raised.Contains(new(x, 17.25f, z), 0), "Alliance raised exact local polygon and holes");
            }
        var oid = AllianceTrashCatalog.Trash(definition.Key).Last();
        f.Actors.Add(Cll1Fixture.Actor(22, oid, f.Center));
        f.Change(1, a => a with { Hp = 0, Targetable = false, InCombat = false }); f.Frame(2);
        Check(f.Native.Active, "Alliance HP0 and combat off do not end overridden trash phase");
        f.Change(1, a => a with { Dead = true }); f.Frame(3);
        Check(f.Native.Active, "Alliance live add preserves phase with dead primary");
        f.Change(22, a => a with { Dead = true }); f.Frame(4);
        if (definition.Key == "A30Trash1")
        {
            Check(f.Native.Active, "Alliance dead serpent still present blocks final phase completion");
            f.Actors.RemoveAll(a => a.Id == 1); f.Frame(5);
        }
        if (definition.Key == "A20Kraken")
        {
            Check(f.Native.Active, "Kraken dead original still blocks AllDestroyed P1");
            f.Actors.RemoveAll(a => a.Id == 1); f.Frame(6);
        }
        Check(!f.Native.Active && f.Native.Completed, "Alliance all dead destroyed completes");
    }

    private static void AllianceStandard(BozjaDefinition definition, BozjaRule rule, uint action)
    {
        foreach (var cold in new[] { false, true })
        {
            using var f = new CastrumFixture(!cold, alliance: definition); f.Frame(0);
            if (!cold) AllianceActivatePhase(f, rule);
            f.Change(20, a => a with { Name = "Source A", Cast = f.Spell(action, 1, point: f.Center + new Vector3(1.237f, 0, 2.713f)) });
            f.Frame(1);
            if (cold)
            {
                Check(!f.Native.Casts.Any(c => c.Rule == rule), "Alliance cast precedes component activation");
                f.Change(1, a => a with { InCombat = true }); f.Frame(1.1);
                if (definition.Key == "A30Trash1" && rule.Component != "WaterIII")
                {
                    f.Actors.Add(Cll1Fixture.Actor(22, 0x4011, f.Center));
                    f.Change(1, a => a with { Dead = true }); f.Frame(1.2);
                    Check(f.Native.AlliancePhase == 1, "Alliance P1 cold cast phase entry");
                }
            }
            var caster = f.Native.Actors[20];
            var original = f.Native.Casts.Where(c => c.Rule == rule).ToArray();
            if (rule.Kind != BozjaRuleKind.CastGaze)
            {
                var source = f.Source!.Components.Single(c => c.GetType().Name == rule.Component);
                source.OnCastStarted(f.Session.World.Actors.Find(20)!, f.Session.World.Actors.Find(20)!.CastInfo!);
                // One action can feed distinct component instances. Replay just
                // the same selected component on each side for duplicate input.
                typeof(BozjaEncounter).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native, [caster, rule]);
                f.Compare();
                if (rule.Kind == BozjaRuleKind.CrystalSight)
                {
                    var sight = (CastLineOfSightAOE)source;
                    var sights = (Dictionary<BozjaRule, BozjaSight>)typeof(BozjaEncounter)
                        .GetField("crystalSights", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .GetValue(f.Native)!;
                    Check(sights[rule].Casters.Count == sight.Casters.Count && sight.Casters.Count == 2
                        && sights[rule].Safezones.Count == sight.Safezones.Count, "Alliance independent LOS duplicate queues retained");
                }
                else Check(f.Native.Casts.Count(c => c.Rule == rule) == original.Length + 1, "Alliance duplicate Start retained");
            }
            f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 17 }); f.Frame(2);
            f.Frame(50);
            Check(f.Native.Active, "Alliance no cast timeout phase completion");
            f.Change(20, a => a with { Cast = null }); f.Frame(51);
            Check(f.Native.TargetPriority(f.NativeViewer.Actor) == null, "Alliance no added target execution");
        }
    }

    private static void AllianceSerpentPhase()
    {
        var definition = AllianceTrashCatalog.Definitions.Single(d => d.Key == "A30Trash1");
        using var f = new CastrumFixture(alliance: definition);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x4011, f.Center) with { Cast = f.Spell(35439) });
        f.Frame(0);
        Check(!f.Native.Casts.Any(), "Alliance P0 does not consume P1 hot casts");
        f.Change(1, a => a with { Dead = true }); f.Frame(1);
        Check(f.Native.AlliancePhase == 1 && f.Native.Casts.Count == 2, "Alliance P1 cold current cast enters AOE and Interrupt");
        // Grouped AOEs consume any same-caster Finish; the separate hint must
        // still require its watched action.
        var actor = f.Session.World.Actors.Find(22)!;
        var unrelated = new ActorCastInfo { Action = new(ActionType.Spell, 999), TargetID = 100 };
        foreach (var component in f.Source!.Components) component.OnCastFinished(actor, unrelated);
        f.Native.Finish(f.Native.Actors[22], f.Spell(999)); f.Compare();
        Check(f.Native.Casts.Single().Rule.Kind == BozjaRuleKind.Interrupt, "Alliance grouped Finish differs from watched hint");
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(2);
        Check(f.Native.Active, "Alliance live Triton preserves P1 after serpent Destroy");
        f.Change(22, a => a with { Dead = true }); f.Frame(3);
        Check(!f.Native.Active, "Alliance P1 exact final completion");
    }

    private static void AllianceDespot()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.Definitions.Single(d => d.Key == "A10Despot"));
        f.Frame(0);
        Check(f.Source != null && !f.Native.Active, "Alliance Despot living primary is loaded before death");
        f.Change(1, a => a with { Targetable = false, Dead = true });
        f.Actors.Add(Cll1Fixture.Actor(22, 0x4713, f.Center) with { InCombat = true, Dead = true });
        f.Actors.Add(Cll1Fixture.Actor(23, 0x4693, f.Center));
        f.Frame(.1);
        Check(f.Native.Active, "Alliance Despot any dead inCombat add pulls untargetable dead primary");
        f.Change(20, a => a with { Cast = f.Spell(40650, 1) }); f.Frame(1);
        var queue = f.Native.Casts.Where(c => c.Rule.Component == "ScraplineTyphoon").ToArray();
        var finish = (double)(5f + .3f);
        Check(queue.Length == 2 && queue[0].Activation == START.AddSeconds(1).AddSeconds(finish + 2.1d)
            && queue[1].Activation == START.AddSeconds(1).AddSeconds(finish + 5.6d), "Alliance exact source extra double clocks");
        f.Change(20, a => a with { Cast = null }); f.Frame(2); f.Frame(100);
        Check(f.Native.Casts.Count == 2 && f.Native.Visible(f.NativeViewer).Count() == 1, "Alliance ordinary Storm Finish and timeout retain head only");
        f.Change(23, a => a with { Cast = f.Spell(41393, 101) }); f.Frame(101);
        f.Change(23, a => a with { Cast = null }); f.Frame(102);
        Check(f.Native.Visible(f.NativeViewer).Single().Cast.Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate,
            "Alliance unrelated caster Scrapline Finish promotes donut");
        f.Change(23, a => a with { Cast = f.Spell(41902, 103) }); f.Frame(103);
        f.Change(23, a => a with { Cast = null }); f.Frame(104);
        Check(!f.Native.Casts.Any(), "Alliance Typhoon Finish clears last");
    }

    private static void AlliancePull()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions.Single(d => d.Key == "A10Despot"));
        f.Change(100, a => a with { Position = f.Center + new Vector3(1.237f, 0, 15.713f),
            Statuses = [new ReplayStatus(1209, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 }] });
        f.Change(20, a => a with { Cast = f.Spell(40650) }); f.Frame(0);
        Check(!f.Native.AlliancePullMovements(f.NativeViewer).Any(), "Alliance cold slot59 immunity");
        Check(f.Native.Navigation(f.NativeViewer).Any(z => z.Component == "ScraplineStorm"), "Alliance custom AI ignores immunity");
        f.Change(100, a => a with { Statuses = [] }); f.Frame(1);
        Check(f.Native.AlliancePullMovements(f.NativeViewer).Count() == 1, "Alliance Lose restores raw pull");
        f.Change(100, a => a with { Position = f.Center }); f.Frame(2);
        Check(!f.Native.AlliancePullMovements(f.NativeViewer).Any(), "Alliance zero vector skips movement");
    }

    private static void AlliancePending()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.Definitions.Single(d => d.Key == "A30Trash2Pack1"));
        f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x4013, f.Center) with { InCombat = true });
        f.Actors.Add(Cll1Fixture.Actor(31, 0x4013, f.Center) with { InCombat = true });
        f.Frame(.1);
        Check(f.Session.BossModules.PendingModules.Count == 2 && f.Native.Primary!.Actor.Id == 1, "Alliance pending cannot replace loaded original");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        f.Change(1, a => a with { Dead = true }); f.Frame(2);
        Check(f.Source == null && f.Native.Primary == null, "Alliance pack1 completes despite other living same OID");
        f.Frame(3);
        Check(f.Source!.PrimaryActor.InstanceID == 31 && f.Native.Primary!.Actor.Id == 31, "Alliance reverse pending load next frame");
        f.Change(31, a => a with { Dead = true }); f.Frame(4);
        f.Frame(5);
        Check(f.Source!.PrimaryActor.InstanceID == 30 && f.Native.Primary!.Actor.Id == 30, "Alliance third original module promoted");
    }

    private static void AllianceDestroyedPrimary()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions.Single(d => d.Key == "A30Trash2Pack2"));
        f.Frame(0);
        var original = f.Native.Primary;
        f.Actors.Add(Cll1Fixture.Actor(30, 0x40E1, f.Center) with { InCombat = true });
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(1);
        Check(f.Native.Active && ReferenceEquals(original, f.Native.Primary) && original!.Destroyed,
            "Alliance active destroyed primary retained while same OID lives");
        f.Actors.Add(Cll1Fixture.Actor(1, 0x40E1, f.Center) with { InCombat = true }); f.Frame(2);
        Check(!ReferenceEquals(original, f.Native.Primary) && f.Source!.PrimaryActor.InstanceID == 1,
            "Alliance same ID physical replacement rebinds loaded module");
        f.Change(30, a => a with { Dead = true }); f.Change(1, a => a with { Dead = true }); f.Frame(3);
        Check(!f.Native.Active && f.Native.Primary == null, "Alliance pack2 completes all same OID");
    }

    private static void AllianceGroupedOrder() => AllianceGroupedOrder(false);

    private static void AllianceGroupedOrder(bool kraken)
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions[kraken ? 9 : 4]);
        f.Frame(0);
        var rule = f.Native.Definition.Rules.Single(r => r.Component == (kraken ? "CatapultParalyzeIIIBanishga" : "WaterDivineFlood"));
        AllianceActivatePhase(f, rule);
        for (var i = 0; i < 98; ++i)
            f.Actors.Add(Cll1Fixture.Actor((ulong)(200 + i), 0x233C, f.Center) with
                { Cast = f.Spell(rule.Actions[i % rule.Actions.Length], 1, total: 110 - i) });
        f.Frame(1);
        Check(f.Native.Casts.First(c => c.Rule == rule).Source.Actor.Id == 200, "Alliance grouped 98 retain insertion order");
        f.Actors.Add(Cll1Fixture.Actor(298, 0x233C, f.Center) with { Cast = f.Spell(rule.Actions[0], 2, total: 1) }); f.Frame(2);
        Check(f.Native.Casts.First(c => c.Rule == rule).Source.Actor.Id == 298, "Alliance grouped 99 sorts activation");
        f.Actors.Add(Cll1Fixture.Actor(299, 0x233C, f.Center) with { Cast = f.Spell(rule.Actions[^1], 3, total: 0) }); f.Frame(3);
        var actor = f.Session.World.Actors.Find(200)!;
        foreach (var component in f.Source!.Components)
            component.OnCastFinished(actor, new ActorCastInfo { Action = new(ActionType.Spell, 999) });
        f.Native.Finish(f.Native.Actors[200], f.Spell(999)); f.Compare();
        Check(!f.Native.Casts.Any(c => c.Rule == rule && c.Source.Actor.Id == 200), "Alliance grouped unrelated Finish removes original caster first");
    }

    private static void AllianceClocks()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions[3]);
        f.Frame(0);
        foreach (var total in new[] { 0f, .01f, .02f, 5f })
        {
            f.Change(20, a => a with { TargetId = 101, Cast = f.Spell(41698, 1, total: total, target: 20) }); f.Frame(1);
            Check(f.Native.DamageForHost(f.NativeViewer).Single().Mask == 2, "Alliance raw self-target caster current target fallback");
        }
        f.Frame(20);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == START.AddSeconds(20).AddSeconds((5f + .3f) - 5f),
            "Alliance capped NPC diagnostic clock preserves float total plus .3 then minus elapsed");
        f.Change(20, a => a with { TargetId = 999 }); f.Frame(21);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Mask == 0, "Alliance missing target yields zero mask");
    }

    private static void AllianceGaze()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions[2]);
        f.Change(20, a => a with { Cast = f.Spell(41667) }); f.Frame(0);
        Check(f.Native.Eyes.Single().Range == 10000, "Alliance gaze uses constructor default rather than range comment");
        foreach (var distance in new[] { 10000f, MathF.BitIncrement(10000f), 30f, 0f })
        {
            f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * distance, Rotation = -MathF.PI / 2 }); f.Frame(1);
        }
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitZ * 7, Rotation = 2 }); f.Frame(2);
        Check(f.Native.Eyes.Single().Position == f.Center, "Alliance cast gaze locks original spell location");
    }

    private static void AllianceRawDamage()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Definitions[3]);
        f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(41698, 1) }); f.Frame(1);
        var rule = f.Native.Definition.Rules.Single(r => r.Component == "Panzerfaust");
        f.Source!.Components.Single(c => c.GetType().Name == "Panzerfaust")
            .OnCastStarted(f.Session.World.Actors.Find(20)!, f.Session.World.Actors.Find(20)!.CastInfo!);
        typeof(BozjaEncounter).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native, [f.Native.Actors[20], rule]);
        f.Compare();
        f.Change(20, a => a with { Cast = f.Spell(999, 2, total: 2, target: 101) with { IsSpell = false } }); f.Frame(2);
        Check(f.Native.DamageForHost(f.NativeViewer).Single() is { Mask: 2 } damage
            && damage.Activation == START.AddSeconds(2).AddSeconds(2f + .3f), "Alliance original leftover reads raw non Spell");
        f.Tick(3);
        f.Change(20, a => a with { Cast = null }); f.Frame(4);
        Check(!f.Native.DamageForHost(f.NativeViewer).Any() && f.Native.Casts.Single().Rule == rule,
            "Alliance null current cast hides prediction but preserves original caster list");
    }
}
