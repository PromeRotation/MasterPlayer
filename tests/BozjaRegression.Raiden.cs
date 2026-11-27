using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using RaidenSource = BossMod.Stormblood.Foray.BaldesionArsenal.BA2Raiden;

internal static partial class BozjaRegression
{
    private static void RunRaiden(Action<string, Action> run)
    {
        run("Bozja pending BA2Raiden exact client manifest two source arenas fixed center", RaidenManifest);
        foreach (var rule in BozjaRaidenCatalog.Definition.Rules.Where(r =>
            r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.TimedCastHint))
            foreach (var action in rule.Actions)
                run($"Bozja pending BA2Raiden real Feed standard {rule.Component}/{action}", () =>
                {
                    using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenPhase2(f);
                    f.Change(20, a => a with { Cast = f.Spell(action, 1) }); f.Frame(1);
                    f.Frame(50); f.Change(20, a => a with { Cast = null }); f.Frame(51);
                });
        run("Bozja pending BA2Raiden P1 hp0 keep instances and P2 cold creations exact order", RaidenPhases);
        run("Bozja pending BA2Raiden Arena overwritten sticky Finish real EAnim fixed old center P2 removal", RaidenArena);
        run("Bozja pending BA2Raiden Icon original duplicate Effect target fallback Created clearing dead Update", RaidenIcons);
        run("Bozja pending BA2Raiden Cloud 8 6 4 spell Loc direction raw caster Effect axis cache", RaidenCloud);
        run("Bozja pending BA2Raiden Chains assigned missing partner current slots cold reset", RaidenChains);
        run("Bozja pending BA2Raiden all111 states metadata raw conditions resets permanent tail", RaidenTimeline);
        run("Bozja pending BA2Raiden original Icon generations damage by ID missing world target", RaidenOriginals);
        run("Bozja pending BA2Raiden real early conditions sequential reset cold callbacks Stop", RaidenResets);
        run("Bozja pending BA2Raiden Cloud exact internal queue two rectangle edges and cached clocks", RaidenCloudBoundaries);
        run("Bozja pending BA2Raiden death Destroy HP0 phase completion without stale outputs", RaidenDeath);
    }

    private static void RaidenPhase2(CastrumFixture f)
    {
        f.Change(1, a => a with { Targetable = false }); f.Frame((f.Native.Now - START).TotalSeconds);
        Check(f.Native.RaidenPhase == 1 && f.Native.ActiveRules.Count() == 15, "Raiden fifteen P2 components four kept eleven new");
    }

    private static void RaidenManifest()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0);
        var name = Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(7973)?.Singular.ToString();
        Check(name == f.Native.Definition.Name, "Raiden actual client name: " + name);
        foreach (var action in Enum.GetValues<RaidenSource.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "Raiden source action " + action);
        Check(f.Native.ActiveRules.Count() == 5 && f.Native.Center == new Vector3(.27999878f, 0, 457.825f),
            "Raiden exact source starting center and five components");
        RaidenArenaCompare(f);
    }

    private static void RaidenArenaCompare(CastrumFixture f)
    {
        for (var x = -36f; x <= 36; x += .431f)
            for (var z = 422f; z <= 494; z += .517f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0),
                    "Raiden exact source arena local contour");
    }

    private static void RaidenPhases()
    {
        using var f = new CastrumFixture(raiden: true);
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 200 });
        f.Frame(0);
        Check(f.Native.Active, "Raiden does not inherit Art Owain strict100 pull");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(1);
        Check(f.Native.Active && f.Native.RaidenPhase == 0, "Raiden P1 HP0 without Dead does not end phase");
        f.Change(1, a => a with { Hp = 1000 });
        f.Change(20, a => a with { Cast = f.Spell(14441, 2) }); f.Frame(2);
        var kept = f.Source!.Components.Where(c => c.KeepOnPhaseChange).ToArray();
        var native = f.Native.Casts.Single();
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2607, f.Center + Vector3.UnitZ * 3));
        f.Change(1, a => a with { Targetable = false }); f.Frame(3);
        Check(kept.SequenceEqual(f.Source.Components.Take(4)) && f.Native.Casts.Contains(native)
            && f.Native.Casts.Count(c => c.Rule.Component == "AmeNoSakahoko") == 1,
            "Raiden Keep source instances and native original cast without duplicate cold replay");
        Check(f.Native.Casts.Single(c => c.Rule.Kind == BozjaRuleKind.RaidenLancing).Activation == START.AddSeconds(4),
            "Raiden P2 existing adds get cold activation Now plus1");
    }

    private static void RaidenArena()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(40, 0x1EA1A1, f.Center)); f.Frame(.5);
        f.Change(20, a => a with { Cast = f.Spell(14463, 1) }); f.Frame(1);
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.Casts.Single().Activation == START.AddSeconds(1).AddSeconds(5f + .3f + 3.4d),
            "Raiden wall warning original NPC finish plus3.4 persists Finish");
        f.Frame(3, packet: new MechanicEventEObjAnimation(40, 1, 2), native: () => f.Region.EAnim(40, 0x00010002));
        Check(f.Native.RaidenReduced && f.Native.Center == BozjaRaidenCatalog.Definition.Center
            && f.Native.Casts.Count == 0, "Raiden real wall changes Bounds not Center");
        RaidenArenaCompare(f);
        f.Change(20, a => a with { Cast = f.Spell(14463, 4) }); f.Frame(4);
        Check(f.Native.Casts.Count == 1, "Raiden later Thundercall still overwrites warning after shrink");
        RaidenPhase2(f);
        Check(f.Native.Casts.Count == 0, "Raiden phase2 drops ArenaChange warning");
        f.Frame(5, packet: new MechanicEventEObjAnimation(40, 1, 2), native: () => f.Region.EAnim(40, 0x00010002));
        RaidenArenaCompare(f);
    }

    private static void RaidenIcons()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenPhase2(f);
        foreach (var source in new ulong[] { 100, 101, 100 })
            f.Frame(1, packet: new MechanicEventIcon(138, 107, source), native: () => f.Region.Icon(source, 138, 107));
        Check(f.Native.RaidenCasters("LancingBlowSpread") == 3, "Raiden source Icon original duplicate targets");
        f.Frame(2, packet: new MechanicEventActionEffect(20, 1, 14455, 101, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(20, 14455, 101));
        Check(f.Native.RaidenCasters("LancingBlowSpread") == 2 && f.Native.RaidenCount("LancingBlowAOE") == 1,
            "Raiden real target matching only first entry and cumulative AOE count");
        f.Effect(3, 14455);
        Check(f.Native.RaidenCasters("LancingBlowSpread") == 1, "Raiden unmatched Effect falls back first spread");
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2607, f.Center)); f.Frame(4);
        Check(f.Native.RaidenCasters("LancingBlowSpread") == 0 && f.Native.RaidenCasters("LancingBlowAOE") == 1,
            "Raiden Created clears all Icons and creates locked AOE");
        f.Actors.RemoveAll(a => a.Id == 30); f.Frame(5);
        Check(f.Native.RaidenCasters("LancingBlowAOE") == 1, "Raiden add Destroy does not clear AOE");
        f.Effect(6, 14455);
        f.Frame(7, packet: new MechanicEventIcon(138, 107, 100), native: () => f.Region.Icon(100, 138, 107));
        f.Change(100, a => a with { Dead = true }); f.Frame(8);
        Check(f.Native.RaidenCasters("LancingBlowSpread") == 0, "Raiden Icon Update removes Dead after timeline");
    }

    private static void RaidenCloud()
    {
        foreach (var (side, count) in new[] { (15f, 8), (15.01f, 6), (20f, 6), (20.01f, 4), (40f, 4) })
        {
            using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenPhase2(f);
            f.Change(20, a => a with { Position = f.Center + new Vector3(2, 0, 3),
                Rotation = 1.7f, Cast = f.Spell(14449, 1, point: f.Center + Vector3.UnitX * side) with { Rotation = 0 } });
            f.Frame(1);
            Check(f.Native.Exaflares.Single().Left == count && f.Native.Exaflares.Single().Advance == Vector3.UnitZ * 8,
                "Raiden Cloud count uses spell Loc and rotation not caster transform");
            f.Frame(50);
            f.Change(20, a => a with { Cast = null }); f.Frame(51);
            f.Change(20, a => a with { Position = a.Position + new Vector3(1, 0, 1) }); f.Effect(52, 14450);
            Check(f.Native.Exaflares.Single().Left == count - 1, "Raiden inclusive per-axis match with diagonal above Euclidean1");
            f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 100 }); f.Effect(53, 14450);
            Check(f.Native.ExaflareErrors == 0, "Raiden unmatched Effect silently leaves source lines");
        }
    }

    private static void RaidenChains()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0);
        f.Change(100, a => a with { Tethers = [new(18, 999)] }); f.Frame(1);
        RaidenPhase2(f);
        Check(f.Native.RaidenTethersAssigned && f.Native.RaidenPartner(f.NativeViewer) == null,
            "Raiden cold missing partner still latches Assigned");
        f.Change(100, a => a with { Tethers = [new(18, 101)] }); f.Frame(2);
        Check(f.Native.RaidenPartner(f.NativeViewer)?.Actor.Id == 101, "Raiden partner original reference");
        (f.Party[0], f.Party[1]) = (f.Party[1] with { Slot = 0 }, f.Party[0] with { Slot = 1 }); f.Frame(3);
        f.Change(100, a => a with { Tethers = [] }); f.Frame(4);
        Check(f.Native.RaidenTethersAssigned, "Raiden Untether does not reset Assigned");
    }

    private static void RaidenMetadata(CastrumFixture f)
    {
        var phase = f.Native.RaidenPhase;
        var source = new List<StateMachine.State>();
        for (var s = f.Source!.StateMachine.Phases[phase].InitialState; s != null; s = s.NextStates?.SingleOrDefault()) source.Add(s);
        var native = ((IEnumerable)typeof(BozjaDelubrumTimeline).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(f.Native.RaidenTimeline)!).Cast<ITuple>().ToArray();
        Check(source.Count == native.Length && source.Count == (phase == 0 ? 17 : 94), "Raiden source phase exact state count");
        for (var i = 0; i < native.Length; ++i)
        {
            var hints = (int)(MasterPlayer.Scripting.ScriptTimelineHint)native[i][8]!;
            if (i + 1 < native.Length && ((uint)native[i][0]! >> 16) == ((uint)native[i + 1][0]! >> 16)) hints |= 1;
            Check(source[i].ID == (uint)native[i][0]! && source[i].Duration == (float)native[i][1]!
                && source[i].Name == (string)native[i][2]! && (int)source[i].EndHint == hints, "Raiden source metadata " + phase + "/" + i);
        }
    }

    private static void RaidenTimelineCompare(CastrumFixture f)
    {
        var expected = MechanicTimeline.Capture(f.Source!)!;
        var actual = f.Native.RaidenTimeline!.Capture()!;
        Check(expected.PhaseIndex == actual.PhaseIndex && expected.PhaseName == actual.PhaseName
            && expected.ElapsedInState == actual.ElapsedInState
            && expected.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))
                .SequenceEqual(actual.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))),
            "Raiden exact phase and sixteen-entry published timeline");
    }

    private static void RaidenTimeline()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenMetadata(f);
        for (var phase = 0; phase < 2; ++phase)
        {
            if (phase == 1) { RaidenPhase2(f); RaidenMetadata(f); }
            var tail = phase == 0 ? 0x80000u : 0x1FF0000u;
            for (var i = 0; i < 1000 && f.Native.RaidenTimeline!.StateId != tail; ++i)
            {
                var state = f.Source!.StateMachine.ActiveState!;
                var targetable = f.Actors.Single(a => a.Id == 1).Targetable;
                if (state.EndHint.HasFlag(StateMachine.StateHint.DowntimeStart)) targetable = false;
                else if (state.EndHint.HasFlag(StateMachine.StateHint.DowntimeEnd)) targetable = true;
                // The final P1 targetability condition is the phase transition,
                // so leave it waiting until the explicit Phase2 call.
                if (phase == 0) targetable = true;
                var time = (f.Native.Now - START).TotalSeconds + 100;
                f.Change(1, a => a with { Targetable = targetable,
                    Cast = state.EndHint.HasFlag(StateMachine.StateHint.BossCastStart)
                        ? f.Spell(999, time) with { IsSpell = false } : null });
                f.Frame(time); RaidenTimelineCompare(f);
            }
            Check(f.Native.RaidenTimeline!.StateId == tail, "Raiden reaches phase tail");
        }
        f.Frame(200000); RaidenTimelineCompare(f);
        Check(f.Native.RaidenTimeline!.StateId == 0x1FF0000, "Raiden permanent tail no invented timeout");
    }

    private static void RaidenOriginals()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenPhase2(f);
        f.Frame(1, packet: new MechanicEventIcon(138, 107, 101), native: () => f.Region.Icon(101, 138, 107));
        var original = f.Native.Actors[101];
        var replacement = f.Actors.Single(a => a.Id == 101) with { Position = f.Center - Vector3.UnitZ * 20 };
        f.Actors.RemoveAll(a => a.Id == 101); f.Frame(2);
        f.Actors.Add(replacement); f.Frame(3);
        Check(original.Destroyed && ReferenceEquals(f.Native.Casts.Single(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread).Target, original)
            && f.Native.DamageForHost(f.NativeViewer).Single().Mask == 2,
            "Raiden original spread reference persists while damage resolves current ID slot");
        f.Frame(4, packet: new MechanicEventActionEffect(20, 1, 14455, 101, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(20, 14455, 101));
        Check(!f.Native.Casts.Any(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread), "Raiden Effect removes historical ID first");
        f.Frame(5, packet: new MechanicEventIcon(138, 100, 999), native: () => f.Region.Icon(999, 138, 100));
        Check(!f.Native.Casts.Any(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread), "Raiden unknown Icon source is ignored");
    }

    private static void RaidenReach(CastrumFixture f, uint id)
    {
        for (var i = 0; i < 1000 && f.Native.RaidenTimeline!.StateId != id; ++i)
        {
            var state = f.Source!.StateMachine.ActiveState!;
            var targetable = f.Actors.Single(a => a.Id == 1).Targetable;
            if (state.EndHint.HasFlag(StateMachine.StateHint.DowntimeStart)) targetable = false;
            else if (state.EndHint.HasFlag(StateMachine.StateHint.DowntimeEnd)) targetable = true;
            var at = (f.Native.Now - START).TotalSeconds + 100;
            f.Change(1, a => a with { Targetable = targetable,
                Cast = state.EndHint.HasFlag(StateMachine.StateHint.BossCastStart)
                    ? f.Spell(999, at) with { IsSpell = false } : null });
            f.Frame(at); RaidenTimelineCompare(f);
        }
        Check(f.Native.RaidenTimeline!.StateId == id, "Raiden reaches state " + id.ToString("X"));
    }

    private static void RaidenResets()
    {
        using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenPhase2(f);
        RaidenReach(f, 0x1030000);
        var at = (f.Native.Now - START).TotalSeconds;
        f.Frame(at + .01, packet: new MechanicEventIcon(138, 107, 100), native: () => f.Region.Icon(100, 138, 107));
        Check(f.Native.RaidenTimeline!.StateId == 0x1030010, "Raiden early nonempty Icon condition immediately advances");
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2607, f.Center)); f.Frame(at + .02);
        Check(f.Native.RaidenTimeline.StateId == 0x1030020, "Raiden early Created clear advances before expected time");
        f.Effect(at + .03, 14455);
        Check(f.Native.RaidenTimeline.StateId == 0x1030030, "Raiden actual count immediately passes AOE condition but waits targetability");
        f.Change(1, a => a with { Targetable = false }); f.Frame(at + .035);
        Check(f.Native.RaidenTimeline.StateId == 0x1030040, "Raiden actual untargetable frame immediately passes condition");
        f.Change(1, a => a with { Targetable = true }); f.Frame(at + .04);
        f.Change(1, a => a with { Cast = f.Spell(14456, at + .05) }); f.Frame(at + .05);
        Check(f.Native.RaidenTimeline.StateId == 0x1030060, "Raiden reaches first Reset exit with real cast");
        f.Frame(at + .06, packet: new MechanicEventIcon(138, 107, 100), native: () => f.Region.Icon(100, 138, 107));
        f.Change(1, a => a with { Cast = null }); f.Frame(at + .07);
        Check(f.Native.RaidenCasters("LancingBlowSpread") == 0
            && f.Native.ActiveRules.Last().Component == "LancingBlowSpread", "Raiden Reset clears Icons and moves component last");
        f.Change(20, a => a with { Cast = f.Spell(14449, at + .08) }); f.Frame(at + .08);
        RaidenReach(f, 0x1050060);
        var now = (f.Native.Now - START).TotalSeconds;
        while (f.Native.Exaflares.Single().Left > 1)
        {
            var pos = f.Native.Exaflares.Single().Cast.Origin;
            f.Change(20, a => a with { Position = pos }); f.Effect(now, 14450);
        }
        var last = f.Native.Exaflares.Single().Cast.Origin;
        f.Change(20, a => a with { Cast = f.Spell(14451, now), Position = last });
        f.Change(100, a => a with { Tethers = [new(18, 101)] });
        f.Effect(now, 14450);
        Check(f.Native.ActiveRules.TakeLast(2).Select(r => r.Component).SequenceEqual(new[] { "SilentLevin", "BitterBarbs" })
            && f.Native.RaidenCasters("SilentLevin") == 1 && f.Native.RaidenTethersAssigned,
            "Raiden sequential Silent then Chains Reset cold replays existing inputs");
        RaidenReach(f, 0x10D00D0);
        var stop = (f.Native.Now - START).TotalSeconds;
        f.Change(1, a => a with { Dead = true }); f.Frame(stop + .01);
        Check(!f.Native.Active && f.Native.Casts.Count == 0 && f.Native.RaidenTimeline == null,
            "Raiden Stop on paired Reset exit clears all reactivated components");
    }

    private static void RaidenCloudBoundaries()
    {
        foreach (var (x, z, count) in new[] { (15f, 35f, 8), (20f, -35f, 6), (15f, 35.01f, 4), (20f, -35.01f, 4) })
        {
            using var f = new CastrumFixture(raiden: true); f.Frame(0); RaidenPhase2(f);
            f.Change(20, a => a with { Cast = f.Spell(14449, 1, point: f.Center + new Vector3(x, 0, z)) with { Rotation = 0 } });
            f.Frame(1); Check(f.Native.Exaflares.Single().Left == count, "Raiden forward back rectangle inclusive boundaries");
            var cached = f.Native.Visible(f.NativeViewer).Select(a => (a.Cast.Origin, a.Cast.Activation)).ToArray();
            f.Frame(80);
            Check(cached.SequenceEqual(f.Native.Visible(f.NativeViewer).Select(a => (a.Cast.Origin, a.Cast.Activation))),
                "Raiden unchanged lines retain published clocks despite overdue");
            for (var i = 0; i < count; ++i)
            {
                var pos = f.Native.Exaflares[0].Cast.Origin;
                f.Change(20, a => a with { Position = pos }); f.Effect(81 + i, 14449);
            }
            Check(f.Native.Exaflares.Count == 0 && !f.Native.Visible(f.NativeViewer).Any(), "Raiden last actual explosion removes line");
        }
    }

    private static void RaidenDeath()
    {
        foreach (var mode in new[] { "dead", "destroy", "hp0" })
        {
            using var f = new CastrumFixture(raiden: true); f.Frame(0);
            if (mode == "hp0") RaidenPhase2(f);
            f.Change(20, a => a with { Cast = f.Spell(14441, 1) }); f.Frame(1);
            if (mode == "destroy") f.Actors.RemoveAll(a => a.Id == 1);
            else f.Change(1, a => a with { Dead = mode == "dead", Hp = 0 });
            f.Frame(2);
            Check(!f.Native.Active && f.Native.Casts.Count == 0 && f.Native.Exaflares.Count == 0,
                "Raiden real completion leaves no mechanism outputs " + mode);
        }
    }

    private static void RaidenRawCompare(CastrumFixture f)
    {
        foreach (var component in f.Source!.Components)
        {
            var name = component.GetType().Name;
            if (name == "LancingBlowSpread")
            {
                var spreads = ((GenericStackSpread)component).Spreads;
                var native = f.Native.Casts.Where(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread).ToArray();
                Check(spreads.Count == native.Length, "Raiden raw Icon ordered count");
                for (var i = 0; i < spreads.Count; ++i)
                    Check(spreads[i].Target.InstanceID == native[i].Target!.Actor.Id
                        && spreads[i].Target.Position.ToVec3() == native[i].Target!.Actor.Position with { Y = 0 }
                        && spreads[i].Target.IsDestroyed == native[i].Target!.Destroyed
                        && spreads[i].Activation == native[i].Activation, "Raiden raw original spread " + i);
            }
            if (name is "SilentLevin" or "LancingBlowAOE")
                Check(((GenericAOEs)component).NumCasts == f.Native.RaidenCount(name), "Raiden real Effect count " + name);
            if (component is Exaflare exa)
            {
                Check(exa.Lines.Count == f.Native.Exaflares.Count, "Raiden raw Cloud line count");
                for (var i = 0; i < exa.Lines.Count; ++i)
                {
                    var e = exa.Lines[i]; var a = f.Native.Exaflares[i];
                    Check(e.Next.ToVec3() == a.Cast.Origin && e.Advance.ToVec3() == a.Advance
                        && e.NextExplosion == a.Cast.Activation && e.ExplosionsLeft == a.Left
                        && e.MaxShownExplosions == a.MaxShown && e.TimeToMove == a.Interval,
                        "Raiden exact original Cloud queue " + i);
                }
            }
            if (component is Chains chains)
            {
                Check(chains.TethersAssigned == f.Native.RaidenTethersAssigned, "Raiden raw Chains Assigned");
                var partners = (Actor?[])typeof(Chains).GetField("_partner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(chains)!;
                var native = (MasterPlayer.Mechanics.Bozja.BozjaActor?[])typeof(BozjaEncounter)
                    .GetField("raidenPartners", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Native)!;
                Check(partners.Length == native.Length, "Raiden all raw partner slots");
                for (var i = 0; i < partners.Length; ++i)
                    Check(partners[i]?.InstanceID == native[i]?.Actor.Id && partners[i]?.IsDestroyed == native[i]?.Destroyed,
                        "Raiden original partner at old Party slot " + i);
            }
        }
    }
}
