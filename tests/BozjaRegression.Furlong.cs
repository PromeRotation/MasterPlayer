using System.Numerics;
using BossMod;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunFurlong(Action<string, Action> run)
    {
        run("Bozja native CE21 actual source safe circle Coffin color risk promotion raw first Finish", FurlongAoes);
        run("Bozja native CE21 actual source stack raw target FIFO original reference dead revival no predicted damage", FurlongStacks);
        run("Bozja native CE21 actual source hand persistent order real TextHints Draw and no new facing navigation", FurlongHands);
        run("Bozja native CE21 actual source complete navigation conversion buddy sample and goal union", FurlongNavigation);
        run("Bozja native CE21 actual client seven consumed actions plus visual exclusions and fixed polygon", FurlongClient);
        run("Bozja native CE21 actual source duplicate damage clocks original caster missing cast zero mask", () => Damage(1));
        run("Bozja native CE21 actual source grouped 98 99 100 ties and unrelated Finish", () => GroupOrder(1));
        run("Bozja native region global pending immediate creation order sticky active and Director", Region);
        run("Bozja native real source manager distance requeue global pending order", RegionFeed);
    }

    private static void FurlongAoes()
    {
        foreach (var rule in BozjaCatalog.Definitions[1].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(1);
            var source = f.Activate<BossMod.Components.SimpleAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            for (var i = 0; i < 4; ++i)
            {
                Compare();
                source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            }
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Compare();
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Compare();
            void Compare()
            {
                var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
                var actual = f.M.Visible().ToArray();
                Check(expected.Length == actual.Length, rule.Component + " count.");
                for (var i = 0; i < actual.Length; ++i)
                {
                    var a = actual[i]; var e = expected[i];
                    Check(e.Activation == a.Cast.Activation && e.Rotation.Rad == a.Cast.Rotation
                        && e.Risky == a.Risky && e.Color == (a.Danger ? Colors.Danger : rule.Safe ? Colors.SafeFromAOE : 0),
                        $"{rule.Component}[{i}] clock/risk/color.");
                    for (var x = f.Center.X - 65; x < f.Center.X + 65; x += 2.31f)
                        for (var z = f.Center.Z - 65; z < f.Center.Z + 65; z += 1.73f)
                            Check(e.Check(new(x, z)) == MasterPlayer.Mechanics.Dungeons.DungeonSourceGeometry.Contains(
                                rule.Shape!, a.Cast.Origin, a.Cast.Rotation, new(x, 0, z)), $"{rule.Component}[{i}] geometry {x},{z}.");
                }
                var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
                var zones = f.M.Navigation(f.NativeViewer).ToArray();
                Check(ai.ForbiddenZones.Count == zones.Length, rule.Component + " raw navigation count.");
                var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                    rule.Component + " hints/risky.");
            }
        }
    }

    private static void FurlongStacks()
    {
        using var f = new Fixture(1);
        var source = f.Activate<BossMod.Components.CastStackSpread>("HatefulMiasma");
        var spell = f.Start(source, 20125);
        f.Add(3, 0x233C, f.Center); f.Sync();
        f.Start(source, 20125, 3, 8);
        var original = f.M.Casts[0].Target!;
        Check(source.Stacks.Count == 2 && f.M.Casts.Count == 2 && !source.RaidwideOnResolve, "Cast stack manifest.");
        source.OnCastFinished(f.W.Actors.Find(3)!, f.W.Actors.Find(3)!.CastInfo!);
        f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!);
        Check(source.NumFinishedStacks == 1 && f.M.NumFinishedStacks == 1
            && source.Stacks[0].Activation == f.M.Casts[0].Activation
            && f.M.Casts[0].Source.Actor.Id == 3, "Finish must consume first matching target, not finishing caster.");
        f.Set(100, a => a with { Dead = true }); f.Viewer.IsDead = true; f.Sync();
        Check(source.ActiveStacks.Count == 0 && f.M.Stacks(f.M.Definition.Rules[1]).Length == 0
            && f.M.Casts.Count == 1, "Dead target must hide, not remove.");
        f.Set(100, a => a with { Dead = false }); f.Viewer.IsDead = false; f.Sync();
        Check(source.ActiveStacks.Count == 1 && f.M.Stacks(f.M.Definition.Rules[1]).Length == 1, "Original target revival.");
        f.Remove(100); f.Add(100, 0, f.Center + new Vector3(20, 0, 0), ActorType.Player); f.Sync();
        Check(original.Destroyed && ReferenceEquals(f.M.Casts[0].Target, original)
            && !ReferenceEquals(original, f.M.Actors[100]) && source.ActiveStacks.Count == 1, "Destroyed target was rebound.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        Check(ai.PredictedDamage.Count == 0 && !f.M.Damage(f.NativeViewer).Any(), "CastStack fabricated damage.");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(source.Stacks.Count == 0 && f.M.Casts.Count == 0 && f.M.NumFinishedStacks == 2, "Same-ID Finish failed old target.");
        f.Start(source, 20125, target: 999);
        f.Add(999, 0, f.Center, ActorType.Player); f.Sync();
        Check(source.Stacks.Count == 0 && f.M.Casts.Count == 0, "Late target must not fill missing Start.");
    }

    private static void FurlongHands()
    {
        using var f = new Fixture(1);
        f.Add(10, 0x2DBC, f.Center); f.Add(11, 0x2DBC, f.Center + new Vector3(20, 0, 0)); f.Sync();
        var source = f.Activate<BossMod.Components.SimpleAOEs>("GraspingRancor");
        // The native component was activated before these Created callbacks.
        var spell = f.Start(source, 20132);
        foreach (var (id, tether) in new (ulong, uint)[] { (10, 1), (11, 2) })
        {
            f.W.Actors.Find(id)!.Tether = new(tether, 100);
            f.Set(id, a => a with { Tethers = [new((ushort)tether, 100)] });
        }
        f.Sync(); Compare("first hand inside");
        f.W.Actors.Find(10)!.IsDead = true; f.Set(10, a => a with { Dead = true, Targetable = false }); f.Sync();
        Compare("dead hand retained");
        f.Remove(10); f.Add(10, 0x2DBC, f.Center);
        f.W.Actors.Find(10)!.Tether = new(1, 100);
        f.Set(10, a => a with { Tethers = [new(0, 0), new(1, 100)] }); f.Sync();
        Compare("rebuilt first hand appended behind second");
        Check(f.M.Hand(f.NativeViewer)?.Actor.Id == 11, "Hand cache order replaced by dictionary slot reuse.");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("line remains after circle Finish");
        void Compare(string step)
        {
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), step + " hints.");
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
            var line = sink.Commands.OfType<MechanicDrawLine>().Single();
            var hand = f.M.Hand(f.NativeViewer)!;
            Check(line.A == new WPos(hand.Actor.Position.X, hand.Actor.Position.Z)
                && line.B == f.Viewer.Position && line.Color == (hand.Tether?.Id == 2 ? Colors.Safe : Colors.Danger), step + " actual line.");
            Check(!f.M.Navigation(f.NativeViewer).Any(), step + " safe display expanded into navigation.");
        }
    }

    private static void FurlongNavigation()
    {
        foreach (var name in new[] { "HatefulMiasma", "CoffinNails" })
        {
            using var f = new Fixture(1);
            var source = f.Activate<BossComponent>(name);
            f.Start(source, name == "HatefulMiasma" ? 20125u : 20129u);
            Compare();
            f.Add(3, 0x233C, f.Center + new Vector3(4, 0, 0)); f.Sync();
            f.Start(source, name == "HatefulMiasma" ? 20125u : 20129u, 3, target: 101);
            Compare();
            f.Add(4, 0x233C, f.Center + new Vector3(8, 0, 0)); f.Sync();
            f.Start(source, name == "HatefulMiasma" ? 20125u : 20129u, 4, target: 101);
            Compare();
            void Compare()
            {
                var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
                var zones = f.M.Navigation(f.NativeViewer).ToArray();
                Check(zones.Length == ai.ForbiddenZones.Count, name + " navigation count.");
                for (var i = 0; i < zones.Length; ++i)
                {
                    var actual = zones[i]; var raw = ai.ForbiddenZones[i];
                    Check(actual.Activation == raw.Item2, name + " first activation.");
                    if (actual.Join != null)
                        for (var x = f.Center.X - 35; x < f.Center.X + 35; x += 1.13f)
                            for (var z = f.Center.Z - 35; z < f.Center.Z + 35; z += .97f)
                                Check(actual.Distance(x, z) == raw.Item1.Distance(new(x, z)), $"{name}/{i} raw SDF {x},{z}.");
                    var expected = MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertForbiddenZone(
                        raw.Item1, 0, f.Center, 29.5f, out var kind);
                    var goal = kind == MasterPlayer.MechanicHost.MechanicNavZoneKind.ComplementSkipped
                        && MasterPlayer.MechanicHost.MechanicHintsConvert.TryConvertComplement(raw.Item1, 0, out var goals);
                    var count = expected.Count;
                    if (goal && MasterPlayer.MechanicHost.MechanicHintsConvert.TryConvertComplement(raw.Item1, 0, out var accepted)) count = accepted.Count;
                    Check(actual.Goal == goal && actual.Convert(f.Center, 29.5f).Count() == count, $"{name}/{i} real bridge conversion.");
                }
            }
        }
    }

    private static void FurlongClient()
    {
        Initialize();
        var definition = BozjaCatalog.Definitions[1];
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(6)?.Name.ToString() == definition.Name, "CE21 DynamicEvent.");
        var actions = definition.Rules.SelectMany(r => r.Actions).Order().ToArray();
        Check(actions.SequenceEqual(new uint[] { 20125, 20126, 20129, 20130, 20131, 20132, 21019 }), "CE21 seven consumed actions.");
        foreach (var action in actions)
            Check(new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, $"CE21 client extra {action}.");
        using var f = new Fixture(1);
        var expected = MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds, f.Source.Arena.Center, 0)!;
        for (var x = 612.013f; x < 676; x += .173f)
            for (var z = 196.037f; z < 260; z += .197f)
                Check(expected.Contains(new(x, 0, z), 0) == definition.Arena.Contains(new(x, 0, z), 0), $"CE21 polygon {x},{z}.");
    }

    private static void Region()
    {
        var region = new BozjaRegion();
        var center = BozjaCatalog.Definitions[1].Center;
        ReplayActorState p = new() { Id = 100, Kind = 1, Position = center, Hp = 100 };
        ReplayActorState a = new() { Id = 1, Oid = 0x2DBD, Position = CENTER, Targetable = true, Hp = 100 };
        ReplayActorState b = new() { Id = 2, Oid = 0x2DBB, Position = center, Targetable = true, Hp = 100 };
        ReplayPartyMember[] party = [new(0, "", 100)];
        // Cold creation has no old source. Both modules enter the same pending
        // list and the global reversed birth order must win the priority tie.
        var midpoint = p with { Position = (CENTER + center) / 2 };
        region.Synchronize([p with { Position = midpoint.Position }, b, a], party, 100, START, true);
        Check(region.Loaded.Select(m => m.Definition.PrimaryOid).SequenceEqual(new uint[] { 0x2DBD, 0x2DBB })
            && region.Selected?.Definition.PrimaryOid == 0x2DBD, "Global pending order became catalog order.");
        region.Clear();
        region.Synchronize([midpoint], party, 100, START, true);
        region.Synchronize([midpoint, b, a], party, 100, START, true);
        Check(region.Loaded.Select(m => m.Definition.PrimaryOid).SequenceEqual(new uint[] { 0x2DBB, 0x2DBD })
            && region.Selected?.Definition.PrimaryOid == 0x2DBB, "Global immediate order became catalog order.");
        region.Synchronize([p, b with { InCombat = true }, a with { InCombat = true }], party, 100, START, true);
        Check(region.Selected?.Definition.PrimaryOid == 0x2DBB && region.Selected.Active, "CE21 pull/selection.");
        region.Synchronize([p with { Position = CENTER }, b with { InCombat = true }, a with { InCombat = true }], party, 100, START, true);
        Check(region.Selected?.Definition.PrimaryOid == 0x2DBB, "Equal active priority replaced sticky selection.");
        region.Director(0x40000005);
        Check(region.Loaded.Count == 0 && region.Selected == null, "Director left selected output.");
        region.Clear();
        a = a with { InCombat = false };
        b = b with { InCombat = false };
        var atA = p with { Position = CENTER };
        region.Synchronize([atA], party, 100, START, true);
        region.Synchronize([atA, a], party, 100, START, true);
        region.Synchronize([p with { Position = new(-1000, 0, 830) }, a, b], party, 100, START, true);
        region.Synchronize([midpoint, a, b], party, 100, START, true);
        Check(region.Loaded.Select(m => m.Definition.PrimaryOid).SequenceEqual(new uint[] { 0x2DBD, 0x2DBB })
            && region.Selected?.Definition.PrimaryOid == 0x2DBD, "Distance requeue used old birth age instead of new pending order.");
    }

    private static void RegionFeed()
    {
        Initialize();
        using var session = new MechanicSession("bozja-source-manager-order");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaRegion();
        var a = new ReplayActorState { Id = 1, Oid = 0x2DBD, Kind = 2, SubKind = 5, Hostile = true,
            Targetable = true, Hp = 100, MaxHp = 100, Position = CENTER };
        var b = a with { Id = 2, Oid = 0x2DBB, Position = BozjaCatalog.Definitions[1].Center };
        var p = new ReplayActorState { Id = 100, Kind = 1, Hp = 100, MaxHp = 100, Position = CENTER };
        var converter = typeof(MasterPlayer.MechanicHost.ReplayFeedAdapter).GetMethod("ConvertActor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        void Apply(string step, params ReplayActorState[] values)
        {
            session.BeginFrame(START);
            feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, "")], [],
                values.Select((v, index) => (MechanicActorSnapshot)converter.Invoke(null, [v, index, START])!).ToArray()));
            session.Update();
            model.Synchronize(values, [new(0, "", 100)], 100, START, true);
            var expected = session.BossModules.LoadedModules.Select(m => m.PrimaryActor.InstanceID).ToArray();
            var actual = model.Loaded.Select(m => m.Primary!.Actor.Id).ToArray();
            Check(expected.SequenceEqual(actual), $"{step} actual source loaded order {string.Join(',', expected)} / {string.Join(',', actual)}.");
            Check(session.BossModules.ActiveModule?.PrimaryActor.InstanceID == model.Selected?.Primary?.Actor.Id,
                step + " actual source selection.");
        }
        Apply("source first", p);
        Apply("A immediate", p, a);
        Apply("B pending then A distance requeue", p with { Position = new(-1000, 0, 830) }, a, b);
        Apply("pending reverse append order", p with { Position = (a.Position + b.Position) / 2 }, a, b);
        Apply("reordered frames keep sticky tie", p with { Position = (a.Position + b.Position) / 2 }, b, a);
    }
}
