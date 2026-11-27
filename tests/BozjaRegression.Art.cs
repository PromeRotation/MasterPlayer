using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using ArtSource = BossMod.Stormblood.Foray.BaldesionArsenal.BA1Art;

internal static partial class BozjaRegression
{
    private static void RunArt(Action<string, Action> run)
    {
        run("Bozja pending BA1Art manifest client exact arena ten components", ArtManifest);
        foreach (var rule in BozjaArtCatalog.Definition.Rules.Where(r => r.Kind is BozjaRuleKind.Aoe
            or BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.Spread))
            foreach (var action in rule.Actions)
                run($"Bozja pending BA1Art real Feed standard {rule.Component}/{action}", () =>
                {
                    using var f = new CastrumFixture(art: true); f.Frame(0);
                    f.Change(20, a => a with { Cast = f.Spell(action, .1) }); f.Frame(.1);
                    f.Change(100, a => a with { Position = a.Position + Vector3.UnitZ * 8 }); f.Frame(1);
                    f.Frame(50); f.Change(20, a => a with { Cast = null }); f.Frame(51);
                });
        run("Bozja pending BA1Art strict Party0 pull and ordinary main death completion", ArtPull);
        run("Bozja pending BA1Art Myth five sticky queue original lazy spears cross source Effect FIFO", ArtLegend);
        run("Bozja pending BA1Art Geas two crossed predictions clear only Defilers Start", ArtPredict);
        run("Bozja pending BA1Art real EAnim Stay2 sixty slots permits attack dead Hint clears no cold history", ArtStayMove);
        run("Bozja pending BA1Art chasing icons strict nearest duplicate original target and five Effects", ArtChasing);
        run("Bozja pending BA1Art chasing initial dead retained Update clear after hit distinct reset targets", ArtChasingDeath);
        run("Bozja pending BA1Art chasing current same ID versus original generation and three Effect positions", ArtChasingGenerations);
        run("Bozja pending BA1Art short raw NPC totals restart and exact initial clocks", ArtShortClock);
        run("Bozja pending BA1Art competing nearest strict ties duplicate sources and destroyed caster", ArtChasingCompetition);
        run("Bozja pending BA1Art cold icon no recovery old Party mask and no-slot source", ArtChasingCold);
        run("Bozja pending BA1Art timeline condition early overdue slot0 and Update order", ArtTimelineConditions);
        run("Bozja pending BA1Art complete 333 state metadata raw nonSpell transitions overdue permanent tail", ArtTimeline);
    }

    private static void ArtManifest()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(7968)?.Singular.ToString() == f.Native.Definition.Name, "Art exact name");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(639)?.TerritoryType.RowId == 827, "Art CFC639 territory827");
        foreach (var action in Enum.GetValues<ArtSource.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "Art source action " + action);
        Check(f.Native.ActiveRules.Count() == 10, "Art ten actual phase components");
        for (var x = -160f; x < -98; x += .371f)
            for (var z = 716f; z < 779; z += .419f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Art resolved clipped arena");
    }

    private static void ArtPull()
    {
        using var f = new CastrumFixture(art: true);
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 100 }); f.Frame(0);
        Check(!f.Native.Active, "Art exact 100 not pulled");
        f.Change(100, a => a with { Position = a.Position - Vector3.UnitX * .001f }); f.Frame(1);
        Check(f.Native.Active, "Art strict below100 pulled");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 200 });
        f.Change(1, a => a with { InCombat = false, Targetable = false }); f.Frame(2);
        Check(f.Native.Active, "Art no CheckReset or range reset after pull");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(3);
        Check(!f.Native.Active, "Art primary HP0 completes");
    }

    private static void ArtLegend()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        for (var i = 0; i < 4; ++i)
            f.Actors.Add(Cll1Fixture.Actor((ulong)(30 + i), 0x265B, f.Center + new Vector3(i * 5, 0, 8)));
        f.Change(20, a => a with { Cast = f.Spell(14631) }); f.Frame(1);
        f.Change(20, a => a with { Cast = f.Spell(14632, 2) }); f.Frame(2);
        Check(f.Native.ArtLegendCount == 5 && f.Native.Visible(f.NativeViewer)
            .First(a => a.Cast.Rule.Kind == BozjaRuleKind.ArtLegend).Danger, "Art five item sticky danger");
        f.Change(20, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.ArtLegendCount == 5, "Art ordinary Finish preserves five forecasts");
        f.Actors.RemoveAll(a => a.Id == 30); f.Frame(4);
        f.Effect(5, 14635);
        Check(f.Native.ArtLegendCount == 4, "Art cross Effect FIFO not caster or target");
        f.Change(20, a => a with { Cast = f.Spell(14633, 6) }); f.Frame(6);
        Check(f.Native.ArtLegendCount == 5, "Art consumed mythcall does not append spears twice");
    }

    private static void ArtPredict()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(14642, 1) }); f.Frame(1);
        Check(f.Native.Visible(f.NativeViewer).Count(a => a.Cast.Rule.Kind == BozjaRuleKind.ArtPredict) == 2, "Art two Geas crosses");
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        f.Effect(3, 14643); f.Frame(50);
        Check(f.Native.Visible(f.NativeViewer).Count(a => a.Cast.Rule.Kind == BozjaRuleKind.ArtPredict) == 2, "Art no Finish Effect or TTL cleanup");
        f.Change(20, a => a with { Cast = f.Spell(14643, 51) }); f.Frame(51);
        Check(!f.Native.Visible(f.NativeViewer).Any(a => a.Cast.Rule.Kind == BozjaRuleKind.ArtPredict), "Art Defilers Start clears all");
    }

    private static void ArtStayMove()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(40, 0x1EA1A1, new(-135, 0, 750))); f.Frame(1);
        f.Frame(2, packet: new MechanicEventEObjAnimation(40, 1, 2), native: () => f.Region.EAnim(40, 0x00010002));
        Check(f.Native.ArtStay.All(a => a == START.AddSeconds(2)), "Art animation fills sixty-four states");
        f.Change(100, a => a with { Cast = f.Spell(999, 2), TargetId = 1, Position = a.Position + Vector3.UnitX * 4 });
        f.Frame(3);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Don't move!" && !h.Risky)
            && f.Native.ArtSpecialMode(f.NativeViewer)?.Mode == "NoMovement", "Art Stay2 allows attacks and preserves raw self comparison");
        f.Change(100, a => a with { Dead = true }); f.Frame(4, false);
        Check(f.Native.ArtStay[0] != default, "Art death alone not Update clear");
        f.Compare();
        Check(f.Native.ArtStay[0] == default && f.Native.ArtStay[1] != default, "Art dead Hint clears own slot after producing text");
        f.Frame(5, packet: new MechanicEventEObjAnimation(40, 4, 8), native: () => f.Region.EAnim(40, 0x00040008));
        Check(f.Native.ArtStay.All(a => a == default), "Art clear animation clears all slots");
        using var cold = new CastrumFixture(false, art: true);
        cold.Actors.Add(Cll1Fixture.Actor(40, 0x1EA1A1, new(-135, 0, 750)));
        cold.Frame(0, packet: new MechanicEventEObjAnimation(40, 1, 2), native: () => cold.Region.EAnim(40, 0x00010002));
        cold.Change(1, a => a with { InCombat = true }); cold.Frame(1);
        Check(cold.Native.ArtStay.All(a => a == default), "Art no synthetic cold EAnim recovery");
    }

    private static void ArtIcon(CastrumFixture f, double at, ulong source, ulong target)
        => f.Frame(at, packet: new MechanicEventIcon(92, target, source), native: () => f.Region.Icon(source, 92, target));

    private static void ArtHit(CastrumFixture f, double at, ulong source = 20, ulong target = 0, Vector3 point = default, uint action = 14637)
        => f.Frame(at, packet: new MechanicEventActionEffect(source, 1, action, target, 0, point, 0, 0, 0, []),
            native: () => f.Region.Effect(source, action, target, point));

    private static void ArtChasing()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        ArtIcon(f, 1, 100, 107); ArtIcon(f, 1.1, 100, 107);
        f.Change(20, a => a with { Cast = f.Spell(14636, 2, point: f.Center, target: 101) }); f.Frame(2);
        Check(f.Native.ArtChasers.Count == 1 && ReferenceEquals(f.Native.ArtChasers[0].Target, f.NativeViewer)
            && f.Native.ArtChaseTargets.Count == 1 && f.Native.ArtChaseTargetsMask == 1UL << 7,
            "Art icon original actor differs from target mask and cast target");
        f.Change(100, a => a with { Position = f.Center + new Vector3(26, 0, 7), Rotation = 1.347f }); f.Frame(3);
        f.Change(20, a => a with { Cast = null }); f.Frame(4);
        Check(f.Native.ArtChasers.Single().Remaining == 5, "Art Finish no consumption");
        for (var i = 0; i < 5; ++i) ArtHit(f, 5 + i, point: f.Center + new Vector3(i * 4, 0, 3));
        Check(f.Native.ArtChasers.Count == 0 && f.Native.ArtChaseTargets.Count == 0
            && f.Native.ArtChaseCount == 0 && f.Native.ArtChaseTargetsMask == 1UL << 7, "Art fifth Effect resets Targets and Count only");
    }

    private static void ArtChasingDeath()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        ArtIcon(f, 1, 100, 100);
        f.Change(20, a => a with { Cast = f.Spell(14636, 2) }); f.Frame(2);
        f.Change(100, a => a with { Dead = true }); f.Frame(3);
        Check(f.Native.ArtChasers.Count == 1, "Art initial dead target remains at five");
        ArtIcon(f, 4, 101, 101); ArtHit(f, 5, point: f.Center);
        Check(f.Native.ArtChasers.Count == 0 && f.Native.ArtChaseTargets.Count == 1
            && f.Native.ArtChaseCount == 1, "Art Update removal preserves Targets and Count");
        ArtHit(f, 6);
        Check(f.Native.ArtChaseTargets.Count == 0 && f.Native.ArtChaseCount == 0, "Art empty real Effect resets targets");
    }

    private static void ArtChasingGenerations()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        ArtIcon(f, 1, 101, 101);
        f.Change(20, a => a with { Cast = f.Spell(14636, 2) }); f.Frame(2);
        var original = f.Native.ArtChasers.Single().Target;
        var replacement = f.Actors.Single(a => a.Id == 101) with { Position = f.Center + Vector3.UnitZ * 30 };
        f.Actors.RemoveAll(a => a.Id == 101); f.Frame(3);
        f.Actors.Add(replacement); f.Frame(3.1);
        Check(!ReferenceEquals(original, f.Native.Actors[101]) && original.Destroyed && f.Native.ArtChasers.Count == 1,
            "Art unhit old destroyed target remains original generation");
        ArtHit(f, 4, target: 101, point: f.Center - Vector3.UnitZ * 40);
        Check(f.Native.ArtChasers.Count == 0 && f.Native.ArtChaseCount == 1,
            "Art Effect current ID position advances old generation then Update removes");
        ArtIcon(f, 5, 100, 100);
        f.Change(20, a => a with { Cast = null }); f.Frame(6);
        f.Change(20, a => a with { Position = f.Center + new Vector3(3.123f, 0, 4.456f), Cast = f.Spell(14636, 7) }); f.Frame(7);
        ArtHit(f, 8, target: 20, point: f.Center);
        Check(f.Native.ArtChasers.Single().Previous == MasterPlayer.Mechanics.Dungeons.DungeonEncounter.QuantizedDungeonPosition(
            f.Native.Actors[20].Actor.Position), "Art self MainTarget uses quantized caster");
        var fallback = f.Center + new Vector3(9, 0, 17);
        ArtHit(f, 9, target: 9999, point: fallback);
        Check(f.Native.ArtChasers.Single().Previous == fallback, "Art missing target uses real TargetPos");
    }

    private static void ArtShortClock()
    {
        foreach (var action in new uint[] { 14632, 14642, 14636, 14645, 14641 })
        {
            using var f = new CastrumFixture(art: true); f.Frame(0);
            ArtIcon(f, 1, 100, 100); ArtIcon(f, 1.1, 101, 101);
            f.Change(20, a => a with { Cast = f.Spell(action, 2, total: .01f) }); f.Frame(2);
            if (action == 14636)
                Check(f.Native.ArtChasers.Single().Activation == START.AddSeconds(2).AddSeconds(.01f + .3f),
                    "Art unclamped initial NPC time");
            f.Change(20, a => a with { Cast = f.Spell(action, 2, total: .02f) }); f.Frame(2);
            if (action == 14636)
                Check(f.Native.ArtChasers.Count == 2 && f.Native.ArtChasers[1].Activation == START.AddSeconds(2).AddSeconds(.02f + .3f),
                    "Art raw short Total change is genuine Finish Start");
            f.Frame(5);
        }
    }

    private static void ArtChasingCompetition()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        f.Change(100, a => a with { Position = f.Center - Vector3.UnitX * 10 });
        f.Change(101, a => a with { Position = f.Center + Vector3.UnitX * 10 }); f.Frame(.5);
        ArtIcon(f, 1, 100, 100); ArtIcon(f, 1.1, 101, 101);
        f.Change(20, a => a with { Cast = f.Spell(14636, 2) }); f.Frame(2);
        Check(f.Native.ArtChasers[0].Target.Actor.Id == 100, "Art equidistant Targets keeps first");
        f.Change(20, a => a with { Cast = f.Spell(14636, 2, total: 6) }); f.Frame(2);
        Check(f.Native.ArtChasers.Count == 2 && f.Native.ArtChasers[1].Target.Actor.Id == 101,
            "Art actual second Start creates competing chaser");
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(3);
        Check(f.Native.ArtChasers.Count == 2, "Art destroying caster and ordinary Finish do not clear chasers");
        // Both initial predictions equal the quantized cast Loc; strict ties consume the first.
        ArtHit(f, 4, source: 1, point: f.Center, action: 14636);
        Check(f.Native.ArtChasers[0].Remaining == 4 && f.Native.ArtChasers[1].Remaining == 5, "Art equidistant Effect keeps first");
        // Current target position is far to the right, while packet TargetPos is on the left.
        f.Change(101, a => a with { Position = f.Center + Vector3.UnitX * 20 }); f.Frame(4.1);
        ArtHit(f, 5, source: 1, target: 101, point: f.Center - Vector3.UnitX * 20);
        Check(f.Native.ArtChasers[0].Remaining == 4 && f.Native.ArtChasers[1].Remaining == 4
            && f.Native.ArtChasers[1].Previous == f.Native.Actors[101].Actor.Position with { Y = 0 },
            "Art nonself existing ID uses current raw position and nearest geometry rather than target identity");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 20 }); f.Frame(5.1);
        ArtHit(f, 5.2, source: 1, target: 100, point: f.Center - Vector3.UnitX * 20);
        Check(f.Native.ArtChasers[0].Remaining == 4 && f.Native.ArtChasers[1].Remaining == 3,
            "Art MainTarget100 position is nearer chaser targeting101 so selection cannot use target identity");
        ArtIcon(f, 6, 100, 100); ArtIcon(f, 6.1, 100, 100);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(14636, 7) });
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(14636, 7) }); f.Frame(7);
        Check(f.Native.ArtChasers.Count == 4 && ReferenceEquals(f.Native.ArtChasers[2].Target, f.Native.ArtChasers[3].Target),
            "Art duplicate icon same original actor yields two independent chasers");
    }

    private static void ArtChasingCold()
    {
        using var f = new CastrumFixture(false, art: true);
        ArtIcon(f, 0, 100, 100);
        f.Change(20, a => a with { Cast = f.Spell(14636) });
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.ArtChasers.Count == 0 && f.Native.ArtChaseTargets.Count == 0, "Art cold Start does not invent old icon");
        f.Actors.Add(Cll1Fixture.Actor(40, 0, f.Center)); f.Frame(2);
        ArtIcon(f, 3, 40, 9999);
        Check(f.Native.ArtChaseTargets.Count == 1 && f.Native.ArtChaseTargetsMask == 0, "Art no-slot source still enters Targets");
        f.Frame(4, packet: new MechanicEventIcon(92, 100, 100), native: () => f.Region.Icon(100, 92, 100));
        var activation = f.Native.ArtChaseActivation;
        f.Party[0] = new(0, "", 101); f.Party[1] = new(1, "", 100);
        f.Change(20, a => a with { Cast = f.Spell(14636, 5, point: f.Native.Actors[100].Actor.Position) }); f.Frame(5);
        Check(f.Native.ArtChaseTargetsMask == 0, "Art Start clears source slot in previous submitted Party");
        f.Change(100, a => a with { Dead = true }); f.Frame(6);
        ArtHit(f, 7, point: f.Center);
        Check(f.Native.ArtChasers.Count == 0 && f.Native.ArtChaseTargets.Count == 1, "Art Update clears only consumed dead chaser");
        ArtHit(f, 8);
        Check(f.Native.ArtChaseTargets.Count == 0 && f.Native.ArtChaseCount == 0 && f.Native.ArtChaseActivation == activation,
            "Art empty Effect resets Targets Count while retaining auxiliary Activation");
    }

    private static void ArtTimelineCompare(CastrumFixture f)
    {
        var source = MasterPlayer.MechanicRuntime.MechanicTimeline.Capture(f.Source!)!;
        var native = f.Native.ArtTimeline!.Capture()!;
        Check(source.PhaseName == native.PhaseName && source.PhaseIndex == native.PhaseIndex
            && source.ElapsedInState == native.ElapsedInState && f.Source!.StateMachine.ActiveState!.ID == f.Native.ArtTimeline.StateId,
            "Art timeline outer fields and raw current state");
        Check(source.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))
            .SequenceEqual(native.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))), "Art exact sixteen step timeline window");
    }

    private static void ArtTimelineReach(CastrumFixture f, uint id)
    {
        for (var i = 0; i < 700 && f.Native.ArtTimeline!.StateId != id; ++i)
        {
            var at = (f.Native.Now - START).TotalSeconds + 100;
            var start = f.Source!.StateMachine.ActiveState!.EndHint.HasFlag(StateMachine.StateHint.BossCastStart);
            f.Change(1, a => a with { Cast = start ? f.Spell(999, at) with { IsSpell = false } : null });
            f.Frame(at); ArtTimelineCompare(f);
        }
        Check(f.Native.ArtTimeline!.StateId == id, "Art reaches requested condition " + id.ToString("X"));
    }

    private static void ArtTimelineConditions()
    {
        using (var f = new CastrumFixture(art: true))
        {
            f.Frame(0);
            ArtTimelineReach(f, 0x30011);
            var at = (f.Native.Now - START).TotalSeconds;
            f.Change(20, a => a with { Cast = f.Spell(14632, at) });
            f.Change(1, a => a with { Cast = null }); f.Frame(at);
            Check(f.Native.ArtTimeline!.StateId == 0x30020, "Art Legend nonempty condition entered and blocked");
            f.Frame(at + 3.999); ArtTimelineCompare(f);
            Check(f.Native.ArtTimeline.StateId == 0x30020, "Art Legend holds until expected plus1");
            f.Frame(at + 4); ArtTimelineCompare(f);
            Check(f.Native.ArtTimeline.StateId == 0x40000, "Art Legend overdue exact boundary");
        }
        using (var f = new CastrumFixture(art: true))
        {
            f.Frame(0); ArtTimelineReach(f, 0x30011);
            var at = (f.Native.Now - START).TotalSeconds;
            f.Change(20, a => a with { Cast = f.Spell(14632, at) });
            f.Change(1, a => a with { Cast = null }); f.Frame(at);
            f.Effect(at + .1, 14634); ArtTimelineCompare(f);
            Check(f.Native.ArtTimeline!.StateId == 0x40000, "Art Legend real Effect clears and condition advances before expected");
        }
        using (var f = new CastrumFixture(art: true))
        {
            f.Frame(0); ArtTimelineReach(f, 0x70001);
            var at = (f.Native.Now - START).TotalSeconds;
            f.Actors.Add(Cll1Fixture.Actor(40, 0x1EA1A1, new(-135, 0, 750))); f.Frame(at);
            f.Change(1, a => a with { Cast = null });
            f.Frame(at, packet: new MechanicEventEObjAnimation(40, 1, 2), native: () => f.Region.EAnim(40, 0x00010002));
            Check(f.Native.ArtTimeline!.StateId == 0x70010, "Art Geas Stay slot0 blocks");
            f.Frame(at + .1, packet: new MechanicEventEObjAnimation(40, 4, 8), native: () => f.Region.EAnim(40, 0x00040008));
            Check(f.Native.ArtTimeline.StateId == 0x70020, "Art Geas cleared slot0 advances immediately but Cross waits");
            f.Change(20, a => a with { Cast = f.Spell(14643, at + .2) }); f.Frame(at + .2);
            ArtTimelineCompare(f); Check(f.Native.ArtTimeline.StateId == 0xF0000, "Art Cross actual Start condition early");
        }
        using (var f = new CastrumFixture(art: true))
        {
            f.Frame(0); ArtTimelineReach(f, 0x180010);
            var at = (f.Native.Now - START).TotalSeconds;
            ArtIcon(f, at, 100, 100);
            f.Change(20, a => a with { Cast = f.Spell(14636, at) }); f.Frame(at);
            f.Change(1, a => a with { Cast = f.Spell(14639, at) }); f.Frame(at);
            Check(f.Native.ArtTimeline!.StateId == 0x180020, "Art Glory proximity cast state");
            f.Change(1, a => a with { Cast = null }); f.Frame(at);
            Check(f.Native.ArtTimeline.StateId == 0x180030, "Art Glory ends sees live chaser");
            ArtHit(f, at + .1, point: f.Center);
            f.Change(100, a => a with { Dead = true }); f.Frame(at + .2);
            ArtTimelineCompare(f);
            Check(f.Native.ArtTimeline.StateId == 0x180030 && f.Native.ArtChasers.Count == 0,
                "Art timeline precedes dead Chaser Update cleanup");
            f.Frame(at + .3); ArtTimelineCompare(f);
            Check(f.Native.ArtTimeline.StateId == 0x220000, "Art next frame sees cleanup and advances early");
        }
    }

    private static void ArtTimeline()
    {
        using var f = new CastrumFixture(art: true); f.Frame(0);
        var sourceSteps = new List<StateMachine.State>();
        for (var state = f.Source!.StateMachine.Phases[0].InitialState; state != null; state = state.NextStates?.SingleOrDefault())
            sourceSteps.Add(state);
        var steps = ((IEnumerable)typeof(BozjaDelubrumTimeline).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(f.Native.ArtTimeline)!).Cast<ITuple>().ToArray();
        Check(sourceSteps.Count == 333 && steps.Length == 333 && sourceSteps[^1].ID == 0x6720000, "Art all333 source states permanent tail");
        for (var i = 0; i < steps.Length; ++i)
        {
            var hints = (int)(MasterPlayer.Scripting.ScriptTimelineHint)steps[i][8]!;
            if (i + 1 < steps.Length && ((uint)steps[i][0]! >> 16) == ((uint)steps[i + 1][0]! >> 16)) hints |= 1;
            Check(sourceSteps[i].ID == (uint)steps[i][0]! && sourceSteps[i].Duration == (float)steps[i][1]!
                && sourceSteps[i].Name == (string)steps[i][2]! && (int)sourceSteps[i].EndHint == hints, "Art exact state metadata " + i);
        }
        for (var i = 0; i < 700 && f.Source.StateMachine.ActiveState?.ID != 0x6720000; ++i)
        {
            var source = MasterPlayer.MechanicRuntime.MechanicTimeline.Capture(f.Source);
            var native = f.Native.ArtTimeline!.Capture()!;
            Check(source.PhaseName == native.PhaseName && source.PhaseIndex == native.PhaseIndex
                && source.ElapsedInState == native.ElapsedInState && f.Source.StateMachine.ActiveState!.ID == f.Native.ArtTimeline.StateId,
                "Art timeline outer fields and raw current state");
            Check(source.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))
                .SequenceEqual(native.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))), "Art exact sixteen step timeline window");
            var time = (f.Native.Now - START).TotalSeconds + 100;
            var start = f.Source.StateMachine.ActiveState!.EndHint.HasFlag(StateMachine.StateHint.BossCastStart);
            f.Change(1, a => a with { Cast = start ? f.Spell(999, time) with { IsSpell = false } : null });
            f.Frame(time);
        }
        Check(f.Native.ArtTimeline!.StateId == 0x6720000, "Art reaches permanent tail through raw Ability casts and overdue conditions");
        ArtTimelineCompare(f);
        f.Frame(100000);
        ArtTimelineCompare(f);
        Check(f.Native.ArtTimeline.StateId == 0x6720000, "Art terminal ten seconds is not timeout completion");
    }
}
