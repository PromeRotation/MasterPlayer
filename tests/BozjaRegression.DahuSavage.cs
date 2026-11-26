using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunSavageDahu(Action<string, Action> run)
    {
        run("Bozja native DRS3 Dahu client eleven components shared real arena", SavageDahuClient);
        run("Bozja native DRS3 Dahu actual full state chain cold casts activation order clocks flags permanent unknown", () => SavageDahuTimeline(false));
        run("Bozja native DRS3 Dahu HeadDown six second forced overdue with live caster full state differential", () => SavageDahuTimeline(true));
        run("Bozja native DRS3 Dahu five standard components actual geometry duplicates watched Finish no TTL", SavageDahuStandard);
        run("Bozja native DRS3 Dahu Shockwave FIFO risk color rotating callback origin cache five Finishes", SavageDahuSequences);
        run("Bozja native DRS3 Dahu Spit actor not target duplicate order dead add original generations Effect all", SavageDahuSpit);
        run("Bozja native DRS3 Dahu Burn original target raw first clock FIFO dead Update source identity", SavageDahuBurn);
        run("Bozja native DRS3 Dahu both knockbacks overwrite locked source double delay ignore Finish true wall union", SavageDahuKnockbacks);
    }

    private static void SavageDahuClient()
    {
        using var f = new Fixture(21);
        Check(f.M.Definition.Rules.Length == 11 && f.M.Definition.Territory == 937
            && f.M.Definition.PrimaryOid == 0x30A8 && f.M.ActiveRules.Select(r => r.Component).SequenceEqual(["FallingRock"]),
            "DRS3 manifest and initial component");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRS3Dahu.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRS3 action " + action);
        for (var x = -31f; x <= 31; x += .73f)
            for (var z = -31f; z <= 31; z += .67f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DRS3 source 44 point arena");
            }
    }

    private static T SavageDahuComponent<T>(Fixture f, string name) where T : BossComponent
    {
        if (f.M.DahuTimeline!.Running) f.M.DahuTimeline.Stop();
        f.M.ActivateDahu(name);
        return f.Activate<T>(name);
    }

    private static void SavageDahuStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[21].Rules.Where(r => r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Charge))
        {
            using var f = new Fixture(21);
            var source = SavageDahuComponent<GenericAOEs>(f, rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "DRS3 duplicate " + rule.Component);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            Check(((CastCounter)source).NumCasts == f.M.DahuCount(rule.Component), "DRS3 independent actual Effect counter");
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "DRS3 no TTL " + rule.Component);
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "DRS3 watched Finish " + rule.Component);
            Check(f.M.DahuChargeGoal() == null, "DRS3 HotCharge has no normal query mutation or goal");
            if (rule.Kind != BozjaRuleKind.Charge) continue;
            ((ChargeAOEs)source).Casters.Clear(); f.M.Casts.Clear();
            var actor = f.W.Actors.Find(2)!;
            actor.PosRot = new(f.Center + new Vector3(10.00031f, 0, .0023f), 2);
            f.Set(2, a => a with { Position = actor.Position.ToVec3(), Cast = null }); f.Sync();
            spell = new() { Action = new(ActionType.Spell, rule.Actions[0]), TotalTime = 7,
                Rotation = new(-1), Location = f.Center + new Vector3(-13.471f, 0, 8.219f) };
            actor.CastInfo = spell; source.OnCastStarted(actor, spell);
            f.Set(2, a => a with { Cast = new(rule.Actions[0], 0, spell.Location, -1, f.Now, f.Now.AddSeconds(7.3f), false, false)
                { TotalTime = 7, IsSpell = true } }); f.Sync();
            CompareMetalFoxBits(f, source, rule.Component, "DRS3 original delta quantized origin width " + rule.Component);
        }
    }

    private static void SavageDahuSequences()
    {
        using (var f = new Fixture(21))
        {
            var source = SavageDahuComponent<GenericAOEs>(f, "Shockwave");
            f.Start(source, 22383);
            CompareMetalFoxBits(f, source, "Shockwave", "DRS3 shock head");
            for (var i = 0; i < 3; ++i)
            {
                source.OnCastFinished(f.W.Actors.Find(1)!, new() { Action = new(ActionType.Spell, 22386) });
                f.M.Finish(f.M.Actors[1], new(22386, 0, default, 0, f.Now, f.Now, false, false));
                CompareMetalFoxBits(f, source, "Shockwave", "DRS3 wrong-source FIFO promoted color " + i);
            }
        }
        foreach (var iconFirst in new[] { false, true })
        {
            using var f = new Fixture(21);
            var source = SavageDahuComponent<GenericRotatingAOE>(f, "FirebreatheRotating");
            void Icon() { source.OnEventIcon(f.Viewer, 167, 999); f.M.Icon(100, 167, 999); }
            if (iconFirst) Icon();
            f.Start(source, 22379);
            if (!iconFirst) Icon();
            source.Update(); f.M.Update(f.Now);
            CompareMetalFoxBits(f, source, "FirebreatheRotating", "DRS3 rotation callback", Colors.AOE, true);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.Update(); f.M.Update(f.Now);
            CompareMetalFoxBits(f, source, "FirebreatheRotating", "DRS3 rotation cache", Colors.AOE, true);
            for (var i = 0; i < 6; ++i)
            {
                source.OnEventCast(f.Viewer, PatriotEvent(22380)); f.M.Effect(100, 22380);
                source.OnCastFinished(f.Viewer, new() { Action = new(ActionType.Spell, 22380) });
                f.M.Finish(f.NativeViewer, new(22380, 0, default, 0, f.Now, f.Now, false, false));
                source.Update(); f.M.Update(f.Now);
                Check(source.NumCasts == f.M.NumDahuRotations, "DRS3 rotation Finish count including empty");
                CompareMetalFoxBits(f, source, "FirebreatheRotating", "DRS3 rotation advance " + i, Colors.AOE, true);
            }
        }
    }

    private static void SavageDahuSpit()
    {
        using var f = new Fixture(21);
        var source = SavageDahuComponent<UniformStackSpread>(f, "SpitFlame");
        var add = f.Add(3, 0x30A9, f.Center + Vector3.UnitX * 4, ActorType.Enemy);
        add.IsDead = true; f.Set(3, a => a with { Dead = true }); f.Sync();
        void Icon(ulong actor, uint icon, ulong target)
        {
            source.OnEventIcon(f.W.Actors.Find(actor)!, icon, target); f.M.Icon(actor, icon, target);
        }
        void Compare(string step)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "SpitFlame").ToArray(), step);
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                step + " hints and order");
            Check(source.Spreads.Select(s => (s.Target.InstanceID, s.Activation))
                .SequenceEqual(f.M.Casts.Select(c => (c.Target!.Actor.Id, c.Activation))), step + " raw duplicates");
            Check(ai.PredictedDamage.Count == 0 && !f.M.Damage(f.NativeViewer).Any(), step + " no invented prediction");
        }
        Icon(100, 79, 101); Icon(100, 80, 101); Icon(100, 79, 101);
        Check(f.M.Casts.Count == 3 && f.M.Casts.All(c => ReferenceEquals(c.Target, f.NativeViewer)),
            "DRS3 actor input and duplicates retained");
        Compare("DRS3 duplicate slots first order Dead add inclusive R4");
        f.Remove(3); Compare("DRS3 Destroyed add removed");
        var original = f.Viewer; var nativeOriginal = f.NativeViewer;
        f.Remove(100); f.Add(100, 0, f.Center + Vector3.UnitZ * 2, ActorType.Player); f.Sync();
        Check(ReferenceEquals(source.Spreads[0].Target, original) && ReferenceEquals(f.M.Casts[0].Target, nativeOriginal),
            "DRS3 original spread target reference");
        Icon(100, 82, 101); Compare("DRS3 same ID new generation does not become original spread");
        original.IsDead = true; nativeOriginal.Actor = nativeOriginal.Actor with { Dead = true };
        Compare("DRS3 Dead original hidden before Update raw retained");
        source.Update(); f.M.Update(f.Now); Compare("DRS3 Dead Update removes spreads not order slots");
        source.OnEventCast(f.W.Actors.Find(2)!, new(new(ActionType.Spell, 22391), 100, 0, 0, default, 0, 0, default));
        f.M.Effect(2, 22391, 100); Compare("DRS3 actual target ID deletes every matching generation");
    }

    private static void SavageDahuBurn()
    {
        using var f = new Fixture(21);
        var source = SavageDahuComponent<BaitAwayIcon>(f, "Burn");
        void Icon(ulong actor, ulong target)
        {
            source.OnEventIcon(f.W.Actors.Find(actor)!, 87, target); f.M.Icon(actor, 87, target);
        }
        void Compare(string step)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Burn").ToArray(), step);
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                step + " hints");
            Check(source.CurrentBaits.Select(b => (b.Source.InstanceID, b.Target.InstanceID, b.Activation))
                .SequenceEqual(f.M.DahuBurns.Select(b => (b.Source.Actor.Id, b.Target!.Actor.Id, b.Activation))), step + " raw baits");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " raw first clock damage");
            Check(source.NumCasts == f.M.DahuCount("Burn"), step + " actual Effect count");
        }
        Icon(100, 101);
        Check(ReferenceEquals(source.CurrentBaits[0].Source, f.W.Actors.Find(1))
            && ReferenceEquals(f.M.DahuBurns[0].Target, f.M.Actors[101])
            && f.M.DahuBurns[0].Activation == START.AddSeconds(8.2f), "DRS3 targetID and float delay");
        f.Now = START.AddSeconds(.3); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Icon(100, 999); Compare("DRS3 missing target fallback and shared first clock");
        var original = f.Viewer; var nativeOriginal = f.NativeViewer;
        f.Remove(100); source.Update();
        f.Add(100, 0, f.Center + Vector3.UnitZ * 7, ActorType.Player); f.Sync();
        Check(ReferenceEquals(source.CurrentBaits[1].Target, original) && ReferenceEquals(f.M.DahuBurns[1].Target, nativeOriginal),
            "DRS3 destroyed target remains old generation");
        Compare("DRS3 same ID text versus original reference navigation");
        original.IsDead = true; nativeOriginal.Actor = nativeOriginal.Actor with { Dead = true };
        Compare("DRS3 target Dead before Update allowed");
        source.Update(); f.M.Update(f.Now); Compare("DRS3 target Dead Update removes");
        Icon(100, 100);
        source.OnEventCast(f.W.Actors.Find(2)!, new(new(ActionType.Spell, 21603), 100, 0, 0, default, 0, 0, default));
        f.M.Effect(2, 21603, 100); Compare("DRS3 unrelated source mismatched target consumes FIFO");
        f.W.Actors.Find(1)!.IsDead = true; f.Set(1, a => a with { Dead = true }); f.Sync(false);
        Compare("DRS3 Source Dead hides raw but does not remove");
        source.Update();
        Check(source.CurrentBaits.Count == f.M.DahuBurns.Count && f.M.DahuBurns.Count == 1, "DRS3 Source Dead raw retained");
    }

    private static void SavageDahuKnockbacks()
    {
        foreach (var (component, action, effect, delay) in new[] { ("FeralHowl", 22375u, 23349u, 2.1d), ("HystericAssault", 22392u, 23363u, .9d) })
        {
            using var f = new Fixture(21);
            var claw = SavageDahuComponent<GenericAOEs>(f, "HuntersClaw");
            var source = SavageDahuComponent<GenericKnockback>(f, component);
            f.Set(100, a => a with { HitboxRadius = f.Viewer.HitboxRadius }); f.Sync();
            f.Move(2, f.Center + Vector3.UnitZ * 25); f.Start(claw, 22377);
            var spell = f.Start(source, action, 1);
            Check(source.ActiveKnockbacks(0, f.Viewer)[0].Activation == f.M.Casts.Single(c => c.Rule.Component == component).Activation
                && f.M.Casts.Single(c => c.Rule.Component == component).Activation == START.AddSeconds(3 + .3f + delay),
                "DRS3 double delay exact NPC clock " + component);
            f.Add(4, 0x233C, f.Center - Vector3.UnitX * 10); f.Sync();
            f.Start(source, action, 4);
            Check(source.ActiveKnockbacks(0, f.Viewer).Length == 1 && f.M.Casts.Count(c => c.Rule.Component == component) == 1,
                "DRS3 last Start overwrites single knockback");
            f.Move(4, f.Center + Vector3.UnitX * 7); f.Move(1, f.Center + Vector3.UnitZ);
            foreach (var offset in new[] { new Vector3(1, 0, 3), new Vector3(10, 0, 1), new Vector3(-3, 0, -12), new Vector3(-10, 0, 0) })
            {
                f.Move(100, f.Center + offset);
                var expected = source.CalculateMovements(0, f.Viewer); var actual = f.M.DahuMovements(f.NativeViewer).ToArray();
                Check(expected.Count == actual.Length, "DRS3 wall movement count");
                for (var i = 0; i < actual.Length; ++i)
                    Check(expected[i].from.ToVec3() == actual[i].From && Vector3.Distance(expected[i].to.ToVec3(), actual[i].To) < .00002f,
                        "DRS3 true wall and locked caster origin");
                var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
                CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == component).ToArray(), "DRS3 live primary claws union");
                var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
                Check(hints.Count == 0 && !f.M.Hints(f.NativeViewer).Any(h => h.Text == "About to be knocked into danger!"),
                    "DRS3 default StopAtWall DestinationUnsafe false");
            }
            source.OnCastFinished(f.W.Actors.Find(4)!, spell);
            f.M.Finish(f.M.Actors[4], f.M.Actors[4].Cast!);
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(effect)); f.M.Effect(2, effect);
            Check(source.NumCasts == f.M.DahuCount(component) && f.M.DahuCasters(component) == 1,
                "DRS3 ordinary Finish and actual Effect never clear single KB before component retirement");
        }
    }

    private static void SavageDahuTimeline(bool overdue)
    {
        using var f = new Fixture(21);
        f.W.Actors.Find(1)!.HPMP = new(1000, 1000, 0, 0, 0);
        f.Add(3, 0x30A9, f.Center + Vector3.UnitZ * 10, ActorType.Enemy);
        f.Add(4, 0x30A9, f.Center + Vector3.UnitX * 10, ActorType.Enemy);
        f.Sync();
        f.Source.StateMachine.Start(f.Now);
        void Compare(string step)
        {
            Check(f.Source.StateMachine.ActiveState?.ID == f.M.DahuTimeline?.StateId,
                step + $" state source={f.Source.StateMachine.ActiveState?.ID:X} mp={f.M.DahuTimeline?.StateId:X}");
            Check(f.Source.Components.Select(c => c.GetType().Name).SequenceEqual(f.M.ActiveRules.Select(r => r.Component)),
                step + " actual component activation order");
            var source = MechanicTimeline.Capture(f.Source);
            var native = f.M.DahuTimeline?.Capture();
            Check(source.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))
                .SequenceEqual(native?.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints)) ?? []),
                step + " exact timeline metadata");
        }
        void Tick(double seconds = 0)
        {
            f.Now = f.Now.AddSeconds(seconds); f.W.Frame.Timestamp = f.Now;
            f.Source.StateMachine.Update(f.Now);
            foreach (var comp in f.Source.Components.ToArray()) comp.Update();
            f.Sync();
            Compare("DRS3 frame " + f.Now);
        }
        void Cast(ulong id, uint? action, float rotation = .4f)
        {
            var actor = f.W.Actors.Find(id)!;
            if (actor.CastInfo is { } previous)
                foreach (var comp in f.Source.Components.ToArray()) comp.OnCastFinished(actor, previous);
            actor.CastInfo = action is { } aid ? new() { Action = new(ActionType.Spell, aid), Location = actor.Position.ToVec3(),
                Rotation = new(rotation), TotalTime = 3 } : null;
            if (actor.CastInfo != null)
                foreach (var comp in f.Source.Components.ToArray()) comp.OnCastStarted(actor, actor.CastInfo);
            f.Set(id, a => a with { Cast = action is { } value ? new(value, 0, a.Position, rotation, f.Now,
                f.Now.AddSeconds(3.3f), false, false) { TotalTime = 3, IsSpell = true } : null });
            f.Sync(false); Tick();
        }
        void Effect(uint action, ulong target = 100)
        {
            var packet = new ActorCastEvent(new(ActionType.Spell, action), target, 0, 0, default, 0, 0, default);
            foreach (var comp in f.Source.Components.ToArray()) comp.OnEventCast(f.W.Actors.Find(2)!, packet);
            f.M.Effect(2, action, target); Tick();
        }
        void Icon(ulong actor, uint icon, ulong target = 100)
        {
            foreach (var comp in f.Source.Components.ToArray()) comp.OnEventIcon(f.W.Actors.Find(actor)!, icon, target);
            f.M.Icon(actor, icon, target); Tick();
        }
        void Pair(uint action) { Cast(1, action); Cast(1, null); }
        void Shockwaves() { Pair(22383); Pair(22385); }
        void Rotations(bool empty)
        {
            Icon(100, empty ? 999u : 167u);
            Cast(1, 22379, empty ? 0 : .4f); Cast(1, null);
            for (var i = 0; i < 5; ++i) Pair(22380);
        }
        Compare("DRS3 initial");
        Cast(2, 22382);
        Pair(22387); Pair(22387); Pair(22388);
        Cast(3, 22376);
        Icon(100, 79); Pair(22390);
        Check(f.M.DahuTimeline!.StateId == 0x10110, "DRS3 Spit waits for actual target Effect");
        Effect(22391);
        Shockwaves();
        Check(f.M.DahuTimeline.StateId == 0x10300, "DRS3 HeadDown wait");
        Tick(5.9);
        Check(f.M.DahuTimeline.StateId == 0x10300, "DRS3 maxOverdue5 is six seconds not checkDelay5");
        if (overdue)
        {
            Tick(.1);
            Check(f.M.DahuTimeline.StateId == 0x10400 && f.M.DahuTimeline.Diagnostics.Any(d => d.Contains("10300")),
                "DRS3 six second forced overdue with live caster");
        }
        Cast(3, null);
        Cast(4, 22377);
        Pair(22375);
        Check(f.M.DahuTimeline.StateId == 0x10410 && f.M.DahuCasters("FeralHowl") == 1,
            "DRS3 Feral ordinary Finish retains knockback");
        Effect(23349); Effect(22377);
        Rotations(false);
        Check(f.M.DahuTimeline.StateId == 0x30000, "DRS3 first rotation five Finishes");
        var crowned = f.Add(30, 0x30AA, f.Center, ActorType.Enemy);
        crowned.IsDead = true; f.Set(30, a => a with { Dead = true });
        Tick();
        Check(f.M.DahuTimeline.StateId == 0x40001, "DRS3 Targetable dead Crowned qualifies and old rock cold replays");
        Cast(3, 22376);
        Shockwaves();
        Icon(101, 82, 100); Pair(22390); Effect(22391, 101);
        Check(f.M.DahuTimeline.StateId == 0x40300, "DRS3 Burn waits for Icon");
        Icon(100, 87, 101);
        Pair(22392);
        Check(f.M.ActiveRules.Select(r => r.Component).SequenceEqual(["Burn", "HuntersClaw", "HystericAssault"]),
            "DRS3 Burn before HuntersClaw before KB real activation order");
        Effect(23363); Effect(21603, 999); Effect(22377);
        Rotations(true);
        Check(f.M.DahuTimeline.StateId == 0xFF0000 && !f.M.Visible().Any() && !f.M.ActiveRules.Any(),
            "DRS3 empty rotation still counts five and releases HeadDown");
        Tick(1000);
        Check(f.M.DahuTimeline.StateId == 0xFF0000 && f.M.Active, "DRS3 permanent unknown");
        f.W.Actors.Find(1)!.IsDead = true; f.Set(1, a => a with { Dead = true });
        Tick();
        Check(!f.M.Active, "DRS3 DeathPhase");
    }
}
