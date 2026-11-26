using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunZadnor(Action<string, Action> run)
    {
        run("Bozja native CE51 client CFC778 territory975 DynamicEvent24 every source action extra", ZadnorClient);
        foreach (var rule in BozjaCatalog.Definitions[10].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
            run("Bozja native CE51 standard differential " + rule.Component, () => ZadnorStandard(rule));
        run("Bozja native CE51 actual source dynamic damage masks original caster clocks", () => Damage(10));
        run("Bozja native CE51 ordinary gaze independent first ID Finish locked eyes full range directions", ZadnorGaze);
        run("Bozja native CE51 region isolation circular arena exact pull boundary source defaults", ZadnorRegion);
        run("Bozja native CE51 actual Feed cold ten components full cast Finish and next Framework", ZadnorFeed);
    }

    private static void ZadnorClient()
    {
        Initialize();
        Check(Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(975)?.ContentFinderCondition.RowId == 778
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(778)?.TerritoryType.RowId == 975,
            "CE51 CFC/territory actual bidirectional lookup.");
        var name = Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(24)?.Name.ToString();
        Console.WriteLine("INFO CE51 DynamicEvent24 actual client name: " + name);
        Check(name == BozjaCatalog.Definitions[10].Name, "CE51 client name differs from candidate catalog: " + name);
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE51ThereWouldBeBlood.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE51 client extra " + action);
    }

    private static void ZadnorStandard(BozjaRule rule)
    {
        using var f = new Fixture(10);
        var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
        var spell = f.Start(source, rule.Actions[0]);
        void Compare(string step) => CompareMetalFoxBits(f, source, rule.Component, rule.Component + "/" + step);
        Compare("Start");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        Compare("duplicate");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(rule.Actions[0]));
        f.M.Effect(2, rule.Actions[0]);
        Compare("watched Effect retains both casts");
        f.Add(3, 0x233C, f.Center + new Vector3(-10, 0, 7)); f.Sync();
        f.Start(source, rule.Actions[0], 3, total: 1);
        Compare("second caster unequal clock Normalize");
        var unrelated = new ActorCastInfo { Action = new(ActionType.Spell, 24810), TotalTime = 7 };
        source.OnCastFinished(f.W.Actors.Find(2)!, unrelated);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 24810 });
        Compare("visual Finish ignored");
        f.Move(2, f.Center + Vector3.UnitX * 20);
        f.Now = START.AddSeconds(50); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Compare("locked source moved and timeout retained");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("watched removes first ID only");
        f.Remove(3);
        Compare("second caster destroyed actual Finish");
    }

    private static void ZadnorGaze()
    {
        using var f = new Fixture(10);
        var source = f.Activate<BossMod.Components.CastGaze>("TragicalGaze");
        var spell = f.Start(source, 23573);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        f.Add(3, 0x319B, f.Center + Vector3.UnitZ * 10); f.Sync(); f.Start(source, 23573, 3, total: 5);
        void Compare(string step)
        {
            var expected = source.ActiveEyes(0, f.Viewer).ToArray();
            Check(expected.Select(e => (e.Position.ToVec3(), e.Activation, e.Inverted, e.ActorID))
                .SequenceEqual(f.M.Eyes.Select(e => (e.Position, e.Activation, e.Inverted, e.SourceId))),
                step + " exact eyes/order. source=" + string.Join(";", expected.Select(e => $"{e.ActorID}:{e.Position}:{e.Activation.Ticks}"))
                    + " native=" + string.Join(";", f.M.Eyes.Select(e => $"{e.SourceId}:{e.Position}:{e.Activation.Ticks}")));
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                step + " exact gaze hints.");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
                .SequenceEqual(f.M.GazeDirections(f.NativeViewer.Actor)), step + " exact directions/range/clocks.");
            Check(f.M.TargetPriority(f.NativeViewer.Actor) == null && !f.M.Damage(f.NativeViewer).Any(),
                step + " gaze has no target-priority override or damage.");
        }
        Compare("three eyes");
        f.Move(2, f.Center + Vector3.UnitX * 22);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Compare("all locked no TTL");
        source.OnCastFinished(f.W.Actors.Find(2)!, WithAction(24810));
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 24810 });
        Compare("visual unrelated Finish");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("first source ID only");
        foreach (var distance in new[] { 9999f, 10000f, 10001f })
        {
            f.Move(100, f.M.Eyes[0].Position + Vector3.UnitX * distance);
            Compare("default range " + distance);
        }
        f.Remove(3);
        Compare("destroy watched cast removes one");
        spell.ElapsedTime = spell.TotalTime;
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        f.Remove(2);
        Compare("destroy old generation removes only one duplicate");
        f.Add(2, 0x319B, f.Center + Vector3.UnitX * 5); f.Sync();
        var replacement = f.Start(source, 23573);
        Compare("same ID new caster appends after surviving old eye");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23573)); f.M.Effect(2, 23573);
        Compare("new caster Effect retains all original eyes");
        source.OnCastFinished(f.W.Actors.Find(2)!, replacement); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("new generation Finish consumes first matching ID, including old eye");

        ActorCastInfo WithAction(uint action) => new() { Action = new(ActionType.Spell, action), TotalTime = 7 };
    }

    private static void ZadnorRegion()
    {
        var south = new BozjaRegion();
        var zadnor = new BozjaRegion(975);
        Check(south.Encounters.Length == 15 && zadnor.Encounters.Length == 16
            && south.Encounters.All(e => e.Definition.Territory == 920)
            && zadnor.Encounters.All(e => e.Definition.Territory == 975), "CE51 territory model isolation.");
        Check(BozjaCatalog.Owns(975, 0x319A) && !BozjaCatalog.Owns(920, 0x319A)
            && !BozjaCatalog.Owns(975, 0x2E75), "CE51 exact ownership.");
        using var f = new Fixture(10);
        Check(f.M.Arena is MasterPlayer.Spatial.Geometry.CircleGeometry { Radius: 25 }
            && f.M.Center == new Vector3(-390, 0, 230), "CE51 actual circular arena.");
        f.M.Clear();
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 25.01f }); f.Sync();
        Check(!f.M.Active, "CE51 outside source pull circle.");
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 25 }); f.Sync();
        Check(f.M.Active, "CE51 exact25 source pull.");
        f.Set(1, a => a with { Dead = true }); f.Sync();
        Check(!f.M.Active, "CE51 default primary death completes.");
    }

    private static void ZadnorFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE51-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var definition = BozjaCatalog.Definitions[10];
        var model = region.Encounters.Single(e => e.Definition.PrimaryOid == definition.PrimaryOid);
        var center = definition.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Position = center,
                Targetable = true, InCombat = true, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, ClassJob = 19 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitZ * 20, Hp = 1000, ClassJob = 19 },
        };
        for (var i = 0; i < definition.Rules.Length; ++i)
        {
            var rule = definition.Rules[i];
            actors.Add(new() { Id = (ulong)(20 + i), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center + Vector3.UnitX * 3, Hp = 1000,
                Cast = new(rule.Actions[0], 100, center + Vector3.UnitX * 3, .4f, START, START.AddSeconds(4.3),
                    false, true) { TotalTime = 4, IsSpell = true } });
        }
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double seconds, bool apply)
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            if (apply)
            {
                var input = actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
                feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [], input));
                feed.Flush();
            }
            session.Update();
            if (apply) region.Synchronize(actors.ToArray(), [new(0, "", 100), new(1, "", 101)], 100, now, true);
            else region.Update(now);
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == definition.PrimaryOid);
            Check(source.Components.Count == 10 && (source.StateMachine.ActiveState != null) == model.Active,
                "CE51 full Feed components and active lifecycle.");
            var viewer = session.World.Actors.Find(100)!;
            var expected = source.Components.OfType<BossMod.Components.GenericAOEs>()
                .SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(model.Actors[100]).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky))),
                "CE51 full Feed ordered outputs at " + seconds);
            var ai = new AIHints();
            foreach (var component in source.Components) component.AddAIHints(0, viewer, default, ai);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.Damage(model.Actors[100]).Select(d => (d.Mask, d.Activation, d.Type))),
                "CE51 full Feed dynamic predictions at " + seconds);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(model.Actors[100]).Select(h => (h.Text, h.Risky))),
                "CE51 full Feed separate global/personal hints at " + seconds);
            var sourceEyes = source.Components.OfType<BossMod.Components.CastGaze>().Single().Eyes;
            Check(sourceEyes.Select(e => (e.Position.ToVec3(), e.Activation, e.ActorID))
                .SequenceEqual(model.Eyes.Select(e => (e.Position, e.Activation, e.SourceId))),
                "CE51 full Feed cold eyes and watched Finish.");
        }
        Frame(0, true);
        Frame(.01, false);
        for (var i = 3; i < actors.Count; ++i) actors[i] = actors[i] with { Cast = null };
        Frame(1, true);
        Check(model.Casts.Count == 0 && model.Eyes.Count == 0, "CE51 actual Finish clears all ten components.");
    }
}
