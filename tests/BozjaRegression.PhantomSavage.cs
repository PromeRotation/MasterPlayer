using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.MechanicHost;

internal static partial class BozjaRegression
{
    private static void RunSavagePhantom(Action<string, Action> run)
    {
        run("Bozja native DRS5 Phantom client eight actual components exact territory", SavagePhantomClient);
        run("Bozja native DRS5 Phantom Creeping exact location triple Finish first match hidden promotion", SavagePhantomCreeping);
        run("Bozja native DRS5 Phantom two exaflare families independent matching and publication clocks", SavagePhantomExaflares);
        run("Bozja native DRS5 Phantom Agony real Effect counter no Raidwide prediction", SavagePhantomAgony);
        run("Bozja native DRS5 Phantom actual full state chain cold casts two rounds adds permanent unknown death", SavagePhantomTimeline);
        run("Bozja native DRS5 Phantom Adds original cache active filter destroyed generations actual marker consumer", SavagePhantomAdds);
        run("Bozja native DRS5 Phantom raw Ability advances Start and holds End independent of Spell callbacks", SavagePhantomRawCast);
    }

    private static void SavagePhantomClient()
    {
        using var f = new Fixture(20);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(761)?.TerritoryType.RowId == 937
            && f.M.Definition.Rules.Length == 8 && f.M.Definition.PrimaryOid == 0x30AE
            && f.M.ActiveRules.Select(r => r.Component).SequenceEqual(["ArenaChange"]), "DRS5 manifest/start components");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRS5Phantom.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRS5 action " + action);
        Check(!f.M.Visible().Any() && !f.M.Navigation(f.NativeViewer).Any() && !f.M.Damage(f.NativeViewer).Any(),
            "DRS5 inactive component outputs");
    }

    private static void SavagePhantomCreeping()
    {
        using var f = new Fixture(20);
        var source = f.Activate<GenericAOEs>("CreepingMiasma");
        f.M.ActivatePhantom("CreepingMiasma");
        var spell = f.Start(source, 22452);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        void Compare(string step) => CompareMetalFoxBits(f, source, "CreepingMiasma", step);
        Compare("DRS5 three rects only first two visible");
        var miss = new ActorCastInfo { Action = new(ActionType.Spell, 22453), Location = spell.Location + new Vector3(.001f, 0, 0) };
        source.OnCastFinished(f.W.Actors.Find(2)!, miss);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 22453, Location = miss.Location });
        Compare("DRS5 exact loc no 1m fuzzy");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(22452)); f.M.Effect(2, 22452);
        for (var hit = 0; hit < 9; ++hit)
        {
            source.OnCastFinished(f.W.Actors.Find(100)!, spell);
            f.M.Finish(f.M.Actors[100], f.M.Actors[2].Cast!);
            Compare("DRS5 arbitrary source Finish " + hit);
            Check(source.NumCasts == f.M.PhantomMiasmaCount, "DRS5 completed rect count " + hit);
        }
        Check(!f.M.Visible().Any() && f.M.PhantomMiasmaCount == 3, "DRS5 all rects consumed");
    }

    private static void SavagePhantomExaflares()
    {
        using var f = new Fixture(20);
        var circle = f.Activate<SimpleExaflare>("LingeringMiasma");
        var donut = f.Activate<SimpleExaflare>("SwirlingMiasma");
        f.M.ActivatePhantom("LingeringMiasma"); f.M.ActivatePhantom("SwirlingMiasma");
        var c = f.Start(circle, 22454);
        // Identical locations must not consume the other component's line.
        var d = new ActorCastInfo { Action = new(ActionType.Spell, 22456), Location = c.Location,
            Rotation = c.Rotation, TotalTime = 3 };
        donut.OnCastStarted(f.W.Actors.Find(2)!, d);
        // Native Start uses its current raw spell; commit the donut first.
        f.Set(2, a => a with { Cast = a.Cast! with { ActionId = 22456 } });
        f.Sync();
        // Replacing the source cast fires an ordinary Finish in the native side.
        circle.OnCastFinished(f.W.Actors.Find(2)!, c);
        circle.Update(); donut.Update();
        CompareMetalFoxBits(f, circle, "LingeringMiasma", "DRS5 circle", Colors.AOE, true);
        CompareMetalFoxBits(f, donut, "SwirlingMiasma", "DRS5 donut", Colors.AOE, true);
        var frozen = f.M.Visible().Where(a => a.Cast.Rule.Component == "SwirlingMiasma").Select(a => a.Cast.Activation).ToArray();
        f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        var line = circle.Lines[0];
        var finish = new ActorCastInfo { Action = new(ActionType.Spell, 22455), Location = line.Next.ToVec3() };
        circle.OnCastFinished(f.W.Actors.Find(2)!, finish);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 22455, Location = finish.Location });
        circle.Update(); donut.Update(); f.M.Update(f.Now);
        CompareMetalFoxBits(f, circle, "LingeringMiasma", "DRS5 moved circle", Colors.AOE, true);
        CompareMetalFoxBits(f, donut, "SwirlingMiasma", "DRS5 untouched donut", Colors.AOE, true);
        Check(frozen.SequenceEqual(f.M.Visible().Where(a => a.Cast.Rule.Component == "SwirlingMiasma")
            .Select(a => a.Cast.Activation)), "DRS5 other component event must not refresh overdue future clock");
        foreach (var pair in new[] { (circle, 22455u, "LingeringMiasma"), (donut, 22457u, "SwirlingMiasma") })
            while (pair.Item1.Lines.Count > 0)
            {
                var pos = pair.Item1.Lines[0].Next.ToVec3();
                var next = new ActorCastInfo { Action = new(ActionType.Spell, pair.Item2), Location = pos };
                pair.Item1.OnCastFinished(f.Viewer, next);
                f.M.Finish(f.NativeViewer, f.M.Actors[2].Cast! with { ActionId = pair.Item2, Location = pos });
                pair.Item1.Update(); f.M.Update(f.Now);
                CompareMetalFoxBits(f, pair.Item1, pair.Item3, "DRS5 eight-count final", Colors.AOE, true);
            }
        Check(f.M.PhantomMiasmaCount == 2, "DRS5 independent finished line counters");
    }

    private static void SavagePhantomAgony()
    {
        using var f = new Fixture(20);
        var source = f.Activate<CastCounter>("MaledictionOfAgony");
        f.M.ActivatePhantom("MaledictionOfAgony");
        f.Start(source, 22461);
        Check(!f.M.Damage(f.NativeViewer).Any() && !f.M.GlobalHints().Any(), "DRS5 must not inherit ordinary raidwide");
        foreach (var action in new[] { 22461u, 23350u, 23350u })
        {
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action)); f.M.Effect(2, action);
            Check(source.NumCasts == f.M.PhantomAgonyCount, "DRS5 actual helper Effect");
        }
        f.M.DeactivatePhantom("MaledictionOfAgony"); f.M.ActivatePhantom("MaledictionOfAgony");
        Check(f.M.PhantomAgonyCount == 0, "DRS5 new instance counter");
    }

    private static void SavagePhantomTimeline()
    {
        using var f = new Fixture(20);
        f.W.Actors.Find(1)!.HPMP = new(1000, 1000, 0, 0, 0);
        f.Source.StateMachine.Start(f.Now);
        void Compare(string step)
        {
            Check(f.Source.StateMachine.ActiveState?.ID == f.M.PhantomTimeline?.StateId,
                step + $" state source={f.Source.StateMachine.ActiveState?.ID:X} mp={f.M.PhantomTimeline?.StateId:X}");
            Check(f.Source.Components.Select(c => c.GetType().Name).SequenceEqual(f.M.ActiveRules.Select(r => r.Component)),
                step + " active component order");
            var expected = MechanicTimeline.Capture(f.Source);
            var actual = f.M.PhantomTimeline?.Capture();
            Check(expected.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))
                .SequenceEqual(actual?.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints)) ?? []),
                step + " exact timeline names clocks all flags");
        }
        void Tick(double seconds = 0)
        {
            f.Now = f.Now.AddSeconds(seconds); f.W.Frame.Timestamp = f.Now;
            f.Source.StateMachine.Update(f.Now);
            foreach (var comp in f.Source.Components.ToArray()) comp.Update();
            f.Sync();
            Compare("DRS5 frame " + f.Now);
        }
        void BossCast(uint action, bool start)
        {
            var actor = f.W.Actors.Find(1)!;
            var cast = actor.CastInfo;
            if (cast != null)
                foreach (var comp in f.Source.Components.ToArray()) comp.OnCastFinished(actor, cast);
            actor.CastInfo = start ? new() { Action = new(ActionType.Spell, action), TotalTime = 3, Location = f.Center } : null;
            if (actor.CastInfo != null)
                foreach (var comp in f.Source.Components.ToArray()) comp.OnCastStarted(actor, actor.CastInfo);
            f.Set(1, a => a with { Cast = start ? new(action, 0, f.Center, 0, f.Now, f.Now.AddSeconds(3.3f), false, false)
                { TotalTime = 3, IsSpell = true } : null });
            // Commit native raw cast without running Framework; then both chains see it.
            f.Sync(false); Tick();
        }
        Compare("DRS5 initial only Arena");
        BossCast(22461, true); BossCast(22461, false);
        Check(f.M.PhantomTimeline!.StateId == 2, "DRS5 raidwide state waits for actual Effect");
        foreach (var comp in f.Source.Components.ToArray()) comp.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23350));
        f.M.Effect(2, 23350); Tick();
        for (var round = 0; round < 2; ++round)
        {
            // Current helper cast is replayed when Weave's CastEnd activates the components.
            var cold = new ActorCastInfo { Action = new(ActionType.Spell, 22452), Location = f.Center, Rotation = new(.4f), TotalTime = 10 };
            f.W.Actors.Find(2)!.CastInfo = cold;
            f.Set(2, a => a with { Cast = new(22452, 0, f.Center, .4f, f.Now, f.Now.AddSeconds(10.3f), false, false)
                { TotalTime = 10, IsSpell = true } });
            BossCast(22450, true);
            Check(f.M.Visible().Count() == 1, "DRS5 cold helper at CastEnd entry");
            BossCast(22450, false);
            BossCast(round == 0 ? 22451u : 23022u, true);
            BossCast(22451, false);
            // No lower check delay: eight completed rects resolve immediately,
            // independent of the expected 22.2s duration.
            if (round == 0)
            {
                var comp = f.Source.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "CreepingMiasma");
                for (var line = 1; line < 8; ++line) { comp.OnCastStarted(f.W.Actors.Find(2)!, cold); f.M.Start(f.M.Actors[2]); }
                for (var hit = 0; hit < 24; ++hit)
                {
                    comp.OnCastFinished(f.Viewer, cold);
                    f.M.Finish(f.NativeViewer, f.M.Actors[2].Cast!);
                }
                Tick();
            }
            else Tick(23.2f);
            Check(f.M.ActiveRules.Select(r => r.Component).SequenceEqual(["ArenaChange"]), "DRS5 miasma deactivation");
            // Remove the old helper's cast while components are absent.
            f.W.Actors.Find(2)!.CastInfo = null; f.Set(2, a => a with { Cast = null }); Tick();
        }
        BossCast(22464, true); BossCast(22464, false);
        f.W.Actors.Find(1)!.IsTargetable = false; f.Set(1, a => a with { Targetable = false }); Tick();
        var add = f.Add(30, 0x30B0, f.Center); add.IsTargetable = false;
        f.Set(30, a => a with { Targetable = false }); Tick();
        Check(f.M.PhantomTimeline!.StateId == 0x30020 && !f.M.PhantomActiveBloody.Any(), "DRS5 untargetable add");
        add.IsTargetable = true; f.Set(30, a => a with { Targetable = true }); Tick();
        BossCast(22465, true); Tick(43);
        Check(f.M.PhantomTimeline!.StateId == 0xFF0000, "DRS5 unknown tail entry");
        Tick(1000);
        Check(f.M.PhantomTimeline.StateId == 0xFF0000 && f.M.PhantomComponents.Contains("BloodyWraith"),
            "DRS5 unknown tail never auto-clears");
        f.W.Actors.Find(1)!.IsDead = true; f.Set(1, a => a with { Dead = true }); Tick();
        Check(!f.M.Active && !f.M.ActiveRules.Any(), "DRS5 death phase release");
    }

    private static void SavagePhantomAdds()
    {
        using var f = new Fixture(20);
        f.Add(30, 0x30B0, f.Center); f.Add(31, 0x30B1, f.Center);
        f.Sync();
        var bloody = f.Activate<Adds>("BloodyWraith");
        var misty = f.Activate<Adds>("MistyWraith");
        f.M.ActivatePhantom("BloodyWraith"); f.M.ActivatePhantom("MistyWraith");
        void Compare(string step)
        {
            Check(bloody.Actors.Select(a => a.InstanceID).SequenceEqual(f.M.PhantomBloodyActors.Select(a => a.Actor.Id))
                && misty.Actors.Select(a => a.InstanceID).SequenceEqual(f.M.PhantomMistyActors.Select(a => a.Actor.Id))
                && bloody.ActiveActors.Select(a => a.InstanceID).SequenceEqual(f.M.PhantomActiveBloody.Select(a => a.Actor.Id)),
                step + " raw and active order");
        }
        Compare("DRS5 cold adds");
        f.Add(32, 0x30B0, f.Center); f.Sync(); Compare("DRS5 later Created");
        f.W.Actors.Find(30)!.IsDead = true; f.Set(30, a => a with { Dead = true });
        f.W.Actors.Find(32)!.IsTargetable = false; f.Set(32, a => a with { Targetable = false });
        f.Sync(); Compare("DRS5 separate Dead and Targetable filters");
        var old = f.M.PhantomBloodyActors[0];
        f.Remove(30);
        f.Add(30, 0x30B0, f.Center); f.Sync(); Compare("DRS5 same ID replacement appended");
        Check(old.Destroyed && !ReferenceEquals(old, f.M.PhantomBloodyActors.Last()), "DRS5 original generations");
        f.Source.Arena.Bounds.ScreenHalfSize = 200;
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) bloody.DrawArenaForeground(0, f.Viewer);
        var shapes = new List<MechanicPreviewShape>();
        var lines = new List<MechanicPreviewLine>();
        var extras = new List<MechanicRadarExtra>();
        Check(sink.Commands.Count > 0 && sink.Commands.All(c => c is MechanicDrawActorMarker), "DRS5 raw add drawing markers");
        MechanicDrawConvert.Convert(sink.Commands, 0, shapes, lines, extras);
        Check(shapes.Count == 0 && lines.Count == 0 && extras.Count == 0
            && !f.M.Visible().Any() && !f.M.Navigation(f.NativeViewer).Any(),
            "DRS5 actual bridge leaves actor markers unconverted without invented AOE");
    }

    private static void SavagePhantomRawCast()
    {
        using var f = new Fixture(20);
        var primary = f.W.Actors.Find(1)!;
        primary.HPMP = new(1000, 1000, 0, 0, 0);
        f.Source.StateMachine.Start(f.Now);
        void Raw(uint? action, bool spell)
        {
            primary.CastInfo = action is { } aid ? new() { Action = new(spell ? ActionType.Spell : ActionType.Ability, aid),
                TotalTime = 3, Location = f.Center } : null;
            f.Set(1, a => a with { Cast = action is { } value ? new(value, 0, f.Center, 0, f.Now,
                f.Now.AddSeconds(3.3f), false, false) { TotalTime = 3, IsSpell = spell } : null });
            f.Sync();
            f.Source.StateMachine.Update(f.Now);
            Check(f.Source.StateMachine.ActiveState?.ID == f.M.PhantomTimeline?.StateId, "DRS5 raw cast state");
        }
        Raw(999, false);
        Check(f.M.PhantomTimeline!.StateId == 1 && f.M.Primary!.Cast == null
            && f.M.PhantomTimeline.Diagnostics.Count == 1, "DRS5 raw Ability Start unexpected diagnostic");
        Raw(22461, true); Raw(998, false);
        Check(f.M.PhantomTimeline.StateId == 1 && !f.M.PhantomComponents.Contains("MaledictionOfAgony"),
            "DRS5 Ability holds CastEnd despite filtered Spell Finish");
        Raw(null, false);
        Check(f.M.PhantomTimeline.StateId == 2 && f.M.PhantomAgonyCount == 0, "DRS5 only raw null ends cast");
    }
}
