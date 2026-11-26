using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunSeeker(Action<string, Action> run)
    {
        run("Bozja native DRN1 client all source actions exact initial and reduced polygon", SeekerClient);
        run("Bozja native DRN1 four standard ordinary Finish duplicates no TTL", SeekerStandard);
        run("Bozja native DRN1 ArenaChange only real Map effect shrink locked telegraph no raidwide", SeekerArena);
        run("Bozja native DRN1 Blade safe check draw origin split real old bridge sampling", SeekerBlade);
        run("Bozja native DRN1 Splitter strict original distance thresholds triple append any Finish", SeekerSplitter);
        run("Bozja native DRN1 Mercy status raw boss rotation hidden queue in place promotion", SeekerMercy);
        run("Bozja native DRN1 two Icon FIFO raw clocks target generation dead Update", SeekerIcons);
        run("Bozja native DRN1 interleaved Arc DeadIron four original targets independent FIFO first clocks", SeekerInterleavedIcons);
        run("Bozja native DRN1 Chains original partners old Party untether dynamic clock", SeekerChains);
        run("Bozja native DRN1 IronImpact ID hints reference AI multiple lines raw union and source dead", SeekerLines);
        run("Bozja native DRN1 IronImpact sole redirect FIFO no Finish target death and zero direction", SeekerLineEdges);
        run("Bozja native DRN1 IronImpact source target same finite oblique boundary text", SeekerLineBoundary);
        run("Bozja native DRN1 Moon original creation position overwrite dead destroy effect gaze", SeekerMoon);
        run("Bozja native DRN1 actual Feed thirteen components cold60 hot status Map Icon Tether Effect Finish", SeekerFeed);
        run("Bozja native DRN1 real Feed Chains hot previous Party missing target versus cold replay", SeekerChainFeedEdges);
        run("Bozja native DRN1 cold Pet real slot0 slot59 raw nullable primary Cast rotation hot ignored", SeekerColdPet);
    }

    private static void SeekerClient()
    {
        using var f = new Fixture(22);
        var name = Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9834)?.Singular.ToString();
        Console.WriteLine("DRN1 actual client NameID9834=" + name);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(760)?.TerritoryType.RowId == 936
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(936)?.ContentFinderCondition.RowId == 760
            && name == f.M.Definition.Name && f.M.Definition.Rules.Length == 13, "DRN1 client exact name/territory/components");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRN1TrinitySeeker.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRN1 source action " + action);
        void Compare()
        {
            Check(f.Source.Arena.Center.ToVec3() == f.M.Center && f.Source.Arena.Bounds.Radius == f.M.SampleRadius,
                "DRN1 true center and sampling radius");
            for (var x = -31f; x <= 31; x += .37f)
                for (var z = -31f; z <= 31; z += .43f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DRN1 resolved holes/gates");
                }
        }
        Compare();
        var arena = f.Activate<BossComponent>("ArenaChange");
        arena.OnMapEffect(0x1B, 0x00020001); f.M.MapEffect(0x1B, 0x00020001);
        Compare();
        Check(f.M.HostArena.GetContours().Count == 1, "DRN1 old host takes largest exterior");
    }

    private static void SeekerStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[22].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(22);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "DRN1 duplicate standard");
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "DRN1 standard no Effect/TTL");
            source.OnCastFinished(f.W.Actors.Find(1)!, spell);
            f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "DRN1 wrong source Finish");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "DRN1 ordinary first Finish");
        }
    }

    private static void SeekerArena()
    {
        using var f = new Fixture(22);
        var source = f.Activate<GenericAOEs>("ArenaChange");
        var spell = f.Start(source, 23222);
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN1 initial arena");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN1 arena no ordinary Finish TTL");
        source.OnMapEffect(0x1B, 0x00020000); f.M.MapEffect(0x1B, 0x00020000);
        Check(f.M.Visible().Count() == 1, "DRN1 unrelated map does not shrink");
        source.OnMapEffect(0x1B, 0x00020001); f.M.MapEffect(0x1B, 0x00020001);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        Check(source.ActiveAOEs(0, f.Viewer).Length == 0 && !f.M.Visible().Any() && !f.M.Damage(f.NativeViewer).Any(),
            "DRN1 reduced no ring no invented damage");
    }

    private static void SeekerBlade()
    {
        foreach (var action in new uint[] { 23201, 23202 })
        {
            using var f = new Fixture(22);
            var source = f.Activate<GenericAOEs>("BalefulBlade");
            var arena = f.Activate<BossComponent>("ArenaChange");
            var spell = f.Start(source, action);
            void Compare(string step)
            {
                var expected = source.ActiveAOEs(0, f.Viewer).ToArray().Single();
                var actual = f.M.Visible().Single().Cast;
                Check(expected.Origin.ToVec3() == actual.Origin && expected.Activation == actual.Activation,
                    step + " original origin/clock");
                for (var x = -31f; x <= 31; x += .41f)
                    for (var z = -31f; z <= 31; z += .47f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(expected.Check(new(p.X, p.Z)) == actual.Contains(p), step + " safe Check");
                    }
                var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
                var zones = f.M.Navigation(f.NativeViewer).ToArray();
                CompareMetalFoxZones(f, ai, zones, step);
                for (var x = -31f; x <= 31; x += .41f)
                    for (var z = -31f; z <= 31; z += .47f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check((ai.ForbiddenZones[0].Item1.Distance(new(p.X, p.Z)) <= 0)
                            == (zones[0].Distance(p.X, p.Z) <= 0), step + " raw sampled sign contract");
                    }
                var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                    step + " special hint only");
                Check(f.M.Navigation(f.NativeViewer).All(z => !z.Goal), step + " sampler not invented goals");
            }
            Compare("DRN1 blade starting " + action);
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            arena.OnMapEffect(0x1B, 0x00020001); f.M.MapEffect(0x1B, 0x00020001);
            Compare("DRN1 blade locked Check after recenter " + action);
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
            var parts = sink.Commands.OfType<MechanicDrawComplexPoly>().Single().Parts;
            var contours = f.M.SeekerBladeDrawing(f.M.Casts.Single(), 0).GetContours();
            Check(parts.Length == contours.Count, "DRN1 source custom drawing parts");
            for (var i = 0; i < parts.Length; ++i)
                Check(parts[i].Exterior.Select(p => p.ToVec3()).SequenceEqual(contours[i]),
                    "DRN1 actual draw relative polygon moves with new arena center");
            f.Start(source, action == 23201 ? 23202u : 23201u);
            Compare("DRN1 blade reduced new Start replaces prior variant " + action);
            Check(f.M.Casts.Count == 1 && f.M.Casts[0].Origin == f.M.Center, "DRN1 reduced blade new origin");
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23337)); f.M.Effect(1, 23337);
            Check(source.ActiveAOEs(0, f.Viewer).Length == 0 && !f.M.Visible().Any(), "DRN1 other real Blade Effect clears");
            f.Start(source, action);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23336)); f.M.Effect(1, 23336);
            Check(source.ActiveAOEs(0, f.Viewer).Length == 0 && !f.M.Visible().Any(), "DRN1 real Blade Effect clears");
        }
    }

    private static void SeekerSplitter()
    {
        foreach (var distance in new[] { 0f, 2.999f, 3, 9, 9.001f, 10, 11, 17, 17.001f, 18, 19 })
        {
            using var f = new Fixture(22);
            var source = f.Activate<GenericAOEs>("IronSplitter");
            f.Move(2, f.Center + Vector3.UnitX * distance);
            var spell = f.Start(source, 23203);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, "IronSplitter", "DRN1 splitter strict branch " + distance);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23204)); f.M.Effect(1, 23204);
            source.OnCastFinished(f.W.Actors.Find(1)!, spell); f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast!);
            Check(source.ActiveAOEs(0, f.Viewer).Length == 0 && !f.M.Visible().Any(), "DRN1 arbitrary Finish clears all six");
        }
    }

    private static void SeekerMercy()
    {
        using var f = new Fixture(22);
        var source = f.Activate<GenericAOEs>("MercyFourfold");
        f.W.Actors.Find(1)!.CastInfo = new() { Action = new(ActionType.Ability, 999), Rotation = new(.73f) };
        f.Set(1, a => a with { Cast = new(999, 0, f.Center, .73f, f.Now, f.Now.AddSeconds(3), false, false) { IsSpell = false } }); f.Sync();
        foreach (var extra in new ushort[] { 0xF7, 0xFA, 0xF8, 0xF9, 999 })
        {
            ActorStatus status = new(2056, extra, START.AddSeconds(99), 0);
            source.OnStatusGain(f.W.Actors.Find(2)!, ref status);
            var statuses = f.Actors.Single(a => a.Id == 2).Statuses.ToList();
            statuses.Add(new(2056, extra, 0, status.ExpireAt) { SlotIndex = statuses.Count });
            f.Set(2, a => a with { Statuses = statuses.ToArray() }); f.Sync();
            CompareMetalFoxBits(f, source, "MercyFourfold", "DRN1 hidden queue gain " + extra);
        }
        for (var i = 0; i < 5; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23348)); f.M.Effect(1, 23348);
            CompareMetalFoxBits(f, source, "MercyFourfold", "DRN1 hidden FIFO promotion " + i);
        }
    }

    private static void SeekerCompareBait(Fixture f, BossComponent source, string component, string step)
    {
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == component).ToArray();
        CompareMetalFoxZones(f, ai, zones, step, true);
        for (var i = 0; i < zones.Length; ++i)
            if (zones[i].Shape is BozjaSeekerStackTemplate)
                for (var x = -31f; x <= 31; x += .41f)
                    for (var z = -31f; z <= 31; z += .47f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z)) == zones[i].Distance(p.X, p.Z),
                            step + " exact raw inverted rectangle and OutsideOfUnion");
                    }
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), step + " hints");
        Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString())).SequenceEqual(
            f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " prediction");
    }

    private static void SeekerIcons()
    {
        foreach (var (component, icon, action) in new[] { ("MercifulArc", 243u, 23223u), ("DeadIron", 237u, 23216u) })
        {
            using var f = new Fixture(22);
            var source = f.Activate<BaitAwayIcon>(component);
            source.OnEventIcon(f.Viewer, icon, 101); f.M.Icon(100, icon, 101);
            f.Now = START.AddSeconds(1); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.OnEventIcon(f.Viewer, icon, 999); f.M.Icon(100, icon, 999);
            SeekerCompareBait(f, source, component, "DRN1 Icon fallback raw first clock");
            source.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(action, 100)); f.M.Effect(2, action, 100);
            SeekerCompareBait(f, source, component, "DRN1 global FIFO independent target");
            var old = f.Viewer; var oldNative = f.NativeViewer;
            f.Remove(100); f.Add(100, 0, f.Center + Vector3.UnitZ * 3, ActorType.Player); f.Sync();
            Check(ReferenceEquals(source.CurrentBaits.Single().Target, old)
                && ReferenceEquals(f.M.SeekerBaits.Single().Target, oldNative), "DRN1 Icon keeps destroyed original target");
            SeekerCompareBait(f, source, component, "DRN1 Icon same ID new generation");
            old.IsDead = true; oldNative.Actor = oldNative.Actor with { Dead = true };
            source.Update(); f.M.Update(f.Now);
            Check(!f.M.SeekerBaits.Any() && !source.CurrentBaits.Any(), "DRN1 Icon only TargetDead Update clears");
        }
    }

    private static void SeekerChains()
    {
        using var f = new Fixture(22);
        var source = f.Activate<Chains>("BurningChains");
        ActorTetherInfo tether = new(128, 101);
        source.OnTethered(f.Viewer, in tether); f.M.Tether(100, 128, 101);
        f.Set(100, a => a with { Tethers = [new(128, 101)] });
        void Compare(string step)
        {
            SeekerCompareBait(f, source, "BurningChains", step);
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
            Check(sink.Commands.OfType<MechanicDrawLine>().Count() == (f.M.SeekerPartner(f.NativeViewer) == null ? 0 : 1),
                step + " actual partner line");
        }
        Compare("DRN1 chain original");
        f.Now = START.AddSeconds(40); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        f.Move(101, f.Center + Vector3.UnitZ * 20); Compare("DRN1 chain query now plus ten");
        f.Remove(101); Compare("DRN1 chain destroyed partner retained");
        f.Add(101, 0, f.Center - Vector3.UnitZ * 20, ActorType.Player); f.Sync();
        Compare("DRN1 chain replacement not rebound");
        source.OnUntethered(f.Viewer, in tether); f.M.Tether(100, 0, 0);
        f.Set(100, a => a with { Tethers = [] });
        Compare("DRN1 chain actual untether");
    }

    private static void SeekerInterleavedIcons()
    {
        using var f = new Fixture(22);
        var arc = f.Activate<BaitAwayIcon>("MercifulArc");
        var iron = f.Activate<BaitAwayIcon>("DeadIron");
        for (var i = 2; i < 4; ++i)
        {
            var id = (ulong)(100 + i);
            f.Add(id, 0, f.Center + Vector3.UnitX * (i * 4), ActorType.Player);
            f.Party.Add(new(i, "", id));
            f.W.Execute(new PartyState.OpModify(i, new(id, id, false, "")));
        }
        f.Sync();
        foreach (var (icon, target) in new[] { (243u, 100UL), (237u, 101UL), (243u, 102UL), (237u, 103UL) })
        {
            f.Now = f.Now.AddSeconds(.2d); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            arc.OnEventIcon(f.Viewer, icon, target); iron.OnEventIcon(f.Viewer, icon, target); f.M.Icon(100, icon, target);
        }
        void Compare(string step)
        {
            foreach (var (component, source) in new[] { ("MercifulArc", arc), ("DeadIron", iron) })
                Check(source.CurrentBaits.Select(b => (b.Source.InstanceID, b.Target.InstanceID, b.Activation))
                    .SequenceEqual(f.M.SeekerBaits.Where(b => b.Rule.Component == component)
                        .Select(b => (b.Source.Actor.Id, b.Target.Actor.Id, b.Activation))), step + " original component order " + component);
            var ai = new AIHints(); arc.AddAIHints(0, f.Viewer, default, ai); iron.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))),
                step + " independent raw first clocks and masks");
        }
        Compare("DRN1 four interleaved Icon baits");
        arc.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(23223, 102)); f.M.Effect(2, 23223, 102);
        Compare("DRN1 Arc Effect target102 still consumes original100 first");
        iron.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(23216, 103)); f.M.Effect(2, 23216, 103);
        Compare("DRN1 DeadIron Effect target103 still consumes original101 first");
        Check(f.M.SeekerBaits.Select(b => b.Target.Actor.Id).SequenceEqual(new ulong[] { 102, 103 }),
            "DRN1 interleaved queues retain correct later targets");
    }

    private static void SeekerLines()
    {
        using var f = new Fixture(22);
        var source = f.Activate<LineStack>("IronImpact");
        void Marker(ulong caster, ulong target)
        {
            source.OnEventCast(f.W.Actors.Find(caster)!, SeekerEvent(14588, target)); f.M.Effect(caster, 14588, target);
        }
        Marker(2, 101); Marker(1, 101);
        SeekerCompareBait(f, source, "IronImpact", "DRN1 multi line non target raw union and complement goals");
        f.Move(100, f.Center + Vector3.UnitX * 5); SeekerCompareBait(f, source, "IronImpact", "DRN1 moved lines");
        Marker(2, 100);
        SeekerCompareBait(f, source, "IronImpact", "DRN1 own line double width others no fabricated count");
        f.W.Actors.Find(1)!.IsDead = true; f.Set(1, a => a with { Dead = true }); f.Sync(false);
        SeekerCompareBait(f, source, "IronImpact", "DRN1 hidden dead source target still excluded from buddy");
    }

    private static void SeekerLineEdges()
    {
        using var f = new Fixture(22);
        var source = f.Activate<LineStack>("IronImpact");
        source.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(14588, 100)); f.M.Effect(2, 14588, 100);
        source.OnCastFinished(f.W.Actors.Find(2)!, new() { Action = new(ActionType.Spell, 23221) });
        f.M.Finish(f.M.Actors[2], new(23221, 0, default, 0, f.Now, f.Now, false, false));
        Check(source.CurrentBaits.Count == 1 && f.M.SeekerLines.Count == 1, "DRN1 marker line ignores Finish");
        source.OnEventCast(f.W.Actors.Find(1)!, SeekerEvent(23221, 101)); f.M.Effect(1, 23221, 101);
        Check(source.CurrentBaits.Count == 0 && f.M.SeekerLines.Count == 0, "DRN1 sole target correction consumed");
        source.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(14588, 2)); f.M.Effect(2, 14588, 2);
        f.W.Actors.Find(2)!.PosRot = new(f.Center + Vector3.UnitX * 10, MathF.PI / 2);
        f.Set(2, a => a with { Rotation = MathF.PI / 2 }); f.Sync();
        SeekerCompareBait(f, source, "IronImpact", "DRN1 source target same raw zero AI versus draw rotation");
    }

    private static void SeekerMoon()
    {
        using var f = new Fixture(22);
        var source = f.Activate<BossComponent>("MercifulMoon");
        foreach (var id in new ulong[] { 3, 4 })
        {
            var actor = f.Add(id, 0x3132, f.Center + new Vector3((float)id + .0013f, 0, 7.2134f));
            source.OnActorCreated(actor); f.Sync();
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var actual = f.M.GazeDirections(f.NativeViewer.Actor).ToArray();
            Check(ai.ForbiddenDirections.Count == 1 && actual.Length == 1
                && ai.ForbiddenDirections[0].Item1.Rad == actual[0].Center
                && ai.ForbiddenDirections[0].Item2.Rad == actual[0].HalfWidth
                && ai.ForbiddenDirections[0].Item3 == actual[0].Activation, "DRN1 Moon last original eye clock/angle");
        }
        var eye = f.M.Eyes.Single();
        f.Move(4, f.Center + new Vector3(20, 0, -10));
        f.W.Actors.Find(4)!.IsDead = true; f.Set(4, a => a with { Dead = true }); f.Sync();
        f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Update(f.Now); source.Update();
        Check(f.M.Eyes.Single() == eye, "DRN1 Moon raw eye position/clock remain after move Dead timeout");
        var retained = new AIHints(); source.AddAIHints(0, f.Viewer, default, retained);
        Check(retained.ForbiddenDirections.Single().Item3 == eye.Activation, "DRN1 source Moon eye also retained");
        f.Remove(4);
        Check(f.M.Eyes.Count == 1, "DRN1 Moon destruction retains locked eye");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23212)); f.M.Effect(1, 23212);
        Check(f.M.Eyes.Count == 0, "DRN1 Moon real Effect clears");
    }

    private static void SeekerLineBoundary()
    {
        using var f = new Fixture(22);
        var source = f.Activate<LineStack>("IronImpact");
        const float rotation = .8575727343559265f;
        var origin = new Vector3(0, 0, 278);
        f.W.Actors.Find(2)!.PosRot = new(origin, rotation);
        f.Set(2, a => a with { Position = origin, Rotation = rotation });
        f.Move(100, new(21.523517608642578f, 0, 291.33184814453125f));
        source.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(14588, 2)); f.M.Effect(2, 14588, 2);
        var hints = new BossComponent.TextHints();
        source.AddHints(0, f.Viewer, hints);
        Check(hints.Single() == ("Stack!", false), "DRN1 actual source oblique edge is inside");
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            "DRN1 exact source oblique boundary personal text");
    }

    private static ActorCastEvent SeekerEvent(uint action, ulong target)
        => new(new(ActionType.Spell, action), target, 0, 0, default, 0, 0, default);

    private static void SeekerColdPet()
    {
        Initialize();
        using var session = new MechanicSession("DRN1-pet-cold");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[22]);
        var center = model.Center;
        var statuses = Enumerable.Range(0, 60).Select(i => new ReplayStatus(i is 0 or 59 ? 2056u : 0,
            i == 0 ? (ushort)0xF7 : (ushort)0xFA, 0, START.AddSeconds(100)) { SlotIndex = i }).ToArray();
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x3131, Kind = 2, SubKind = 5, Position = center, Rotation = .73f,
                Targetable = true, Hp = 1000, InCombat = true,
                Cast = new(999, 0, center, null, START, START.AddSeconds(6.3f), false, false) { TotalTime = 6, IsSpell = false } },
            new() { Id = 2, Oid = 0x1234, Kind = 2, SubKind = 2, Position = center + Vector3.UnitX * 10,
                Rotation = -.4f, Hp = 1000, Statuses = statuses },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var frame = 0; frame < 3; ++frame)
        {
            var now = START.AddSeconds(frame);
            session.BeginFrame(now);
            feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            model.Synchronize(actors, [new(0, "", 100)], 100, now);
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x3131);
            var pet = session.World.Actors.Find(2)!;
            Check(pet.Type == ActorType.Pet, "DRN1 real cold Pet classification");
            if (frame == 0)
                Check(pet.Statuses[0].ID == 2056 && pet.Statuses[0].Extra == 0xF7
                    && pet.Statuses[59].ID == 2056 && pet.Statuses[59].Extra == 0xFA
                    && model.Actors[2].Statuses[0]?.Param == 0xF7 && model.Actors[2].Statuses[59]?.Param == 0xFA,
                    "DRN1 source and MP actual status0 and59 input agree");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray()).ToArray();
            Check(expected.Length == 2 && model.SeekerMercies.Count == 2
                && expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky, a.Color))
                    .SequenceEqual(model.SeekerMercies.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation,
                        a.Risky, a.Danger ? Colors.Danger : 0))), "DRN1 cold Pet order nullable Primary fallback then hot ignored");
            actors[1] = actors[1] with { Statuses = frame == 0
                ? statuses.Select(s => s with { Param = 0xF8 }).ToArray() : [] };
        }
    }

    private static void SeekerChainFeedEdges()
    {
        Initialize();
        foreach (var mode in new[] { "hot-late-target", "hot-old-party", "cold" })
        {
            using var session = new MechanicSession("DRN1-chain-" + mode);
            var feed = new MechanicWorldFeed(session);
            var model = new BozjaEncounter(BozjaCatalog.Definitions[22]);
            var center = model.Center;
            var actors = new List<ReplayActorState>
            {
                new() { Id = 1, Oid = 0x3131, Kind = 2, SubKind = 5, Position = center, Targetable = true, Hp = 1000, InCombat = mode != "cold" },
                new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
                new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 12, Hp = 1000 },
            };
            ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Frame(int at)
            {
                var now = START.AddSeconds(at);
                session.BeginFrame(now);
                feed.ApplyFrame(new(936, 760, 100, 0,
                    party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush(); session.Update();
                model.Synchronize(actors, party, 100, now);
                if (!model.Active) return;
                var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x3131);
                var component = source.Components.Single(c => c.GetType().Name == "BurningChains");
                var slot = party.Single(p => p.EntityId == 100).Slot;
                var viewer = session.World.Actors.Find(100)!;
                var hints = new BossComponent.TextHints(); component.AddHints(slot, viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(model.Hints(model.Actors[100]).Select(h => (h.Text, h.Risky))),
                    "DRN1 chain " + mode + " actual old Party hints");
                var ai = new AIHints(); component.AddAIHints(slot, viewer, default, ai);
                var zones = model.Navigation(model.Actors[100]).ToArray();
                Check(ai.ForbiddenZones.Count == zones.Length, "DRN1 chain " + mode + " actual zones");
                for (var i = 0; i < zones.Length; ++i)
                    for (var x = -15f; x < 15; x += .7f)
                    {
                        var p = center + new Vector3(x, 0, 0);
                        Check(ai.ForbiddenZones[i].Item1.Contains(new(p.X, p.Z))
                            == zones[i].Convert(center, model.SampleRadius).Single().Contains(p, 0),
                            "DRN1 chain " + mode + " actual partner geometry");
                    }
            }
            Frame(0);
            actors[0] = actors[0] with { InCombat = true };
            actors[1] = actors[1] with { Tethers = [new(128, mode == "hot-old-party" ? 101UL : 102UL)] };
            actors.Add(new() { Id = 102, Kind = 1, Position = center + Vector3.UnitZ * 14, Hp = 1000 });
            party = [new(0, "", 101), new(1, "", 100), new(2, "", 102)];
            Frame(1); Frame(2);
            Check(mode switch
            {
                "hot-late-target" => model.SeekerPartner(model.Actors[100]) == null,
                "hot-old-party" => ReferenceEquals(model.SeekerPartner(model.Actors[100]), model.Actors[100]),
                _ => ReferenceEquals(model.SeekerPartner(model.Actors[100]), model.Actors[102]),
            }, "DRN1 chain explicit hot versus cold outcome " + mode);
        }
    }

    private static void SeekerFeed()
    {
        Initialize();
        using var session = new MechanicSession("DRN1-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(936);
        var model = region.Encounters.Single(m => m.IsSeeker);
        var center = model.Center;
        ReplayCast Spell(uint action, Vector3 position) => new(action, 100, position, .43f, START, START.AddSeconds(6.3f), false, false)
            { IsSpell = true, TotalTime = 6 };
        var bossStatuses = Enumerable.Range(0, 60).Select(i => new ReplayStatus(i == 59 ? 2056u : 0,
            0xF7, 0, START.AddSeconds(100)) { SlotIndex = i }).ToArray();
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x3131, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000,
                Cast = Spell(23201, center), Statuses = bossStatuses },
            new() { Id = 20, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 10, Hp = 1000,
                Cast = Spell(23203, center + Vector3.UnitX * 10) },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, Tethers = [new(128, 101)] },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitZ * 12, Hp = 1000 },
            new() { Id = 30, Oid = 0x3132, Kind = 2, SubKind = 5, Position = center + new Vector3(.123f, 0, 13), Hp = 1000 },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, MechanicFeedEvent[]? events = null)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            feed.ApplyFrame(new(936, 760, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            foreach (var e in events ?? []) feed.Enqueue(e);
            feed.Flush();
            session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
            {
                foreach (var e in events ?? [])
                    switch (e)
                    {
                        case MechanicEventIcon icon: region.Icon(icon.SourceId, icon.IconId, icon.TargetId); break;
                        case MechanicEventMapEffect map: region.MapEffect(map.Index, map.State); break;
                        case MechanicEventActionEffect effect: region.Effect(effect.CasterId, effect.ActionId, effect.MainTargetId); break;
                    }
            });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x3131);
            Check(source.Components.Count == 13 && model.Active, "DRN1 thirteen actually activated components");
            var viewer = session.World.Actors.Find(100)!;
            var native = model.Actors[100];
            Check(source.Arena.Center.ToVec3() == model.Center && source.Arena.Bounds.Radius == model.SampleRadius,
                "DRN1 Feed actual arena");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                    a.Danger ? Colors.Danger : a.Cast.Rule.Safe ? Colors.SafeFromAOE : 0))), "DRN1 Feed raw list order fields");
            for (var i = 0; i < actual.Length; ++i)
                for (var x = -29f; x <= 29; x += 2.31f)
                    for (var z = -29f; z <= 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(expected[i].Check(new(p.X, p.Z)) == actual[i].Cast.Contains(p), "DRN1 Feed raw shape " + i);
                    }
            var ai = new AIHints();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DRN1 Feed normalized zone clocks");
            for (var i = 0; i < zones.Length; ++i)
            {
                var e = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, model.Center, model.SampleRadius, out var kind);
                if (kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals))
                {
                    e = goals;
                    Check(zones[i].Goal, "DRN1 Feed accepted source complement goal");
                }
                var a = zones[i].Convert(model.Center, model.SampleRadius).ToArray();
                Check(e.Count == a.Length, "DRN1 Feed actual converter count zone " + i);
                for (var x = -29f; x < 29; x += 2.31f)
                    for (var z = -29f; z < 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(e.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == a.Any(s => s.Contains(p, 0)), "DRN1 Feed actual converted geometry zone " + i);
                    }
            }
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "DRN1 Feed both hint streams");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DRN1 Feed normalized damage");
            Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
                .SequenceEqual(model.GazeDirections(native.Actor)), "DRN1 Feed eye direction");
        }
        MechanicEventActionEffect Effect(uint action, ulong target = 100) => new(20, 1, action, target, 0, default, 0, 0, 0, []);
        Frame(0); // cold activation replays real status slot 59 and existing chain
        actors[0] = actors[0] with { Statuses = bossStatuses.Select((s, i) => i == 59 ? s with { Param = 0xFA } : s).ToArray() };
        Frame(1, [new MechanicEventMapEffect(0x1B, 0x00020001), new MechanicEventIcon(243, 101, 100), Effect(14588)]);
        actors[0] = actors[0] with { Cast = null };
        actors[1] = actors[1] with { Cast = Spell(23218, actors[1].Position) };
        Frame(2, [new MechanicEventIcon(237, 100, 101), Effect(23348), Effect(14588, 101)]);
        actors[1] = actors[1] with { Cast = null };
        Frame(3, [Effect(23223), Effect(23221, 101), Effect(23337), Effect(23212)]);
        Frame(100, [Effect(23216), Effect(23348), Effect(23221)]);
    }
}
