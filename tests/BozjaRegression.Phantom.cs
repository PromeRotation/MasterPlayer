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
    private static void RunPhantom(Action<string, Action> run)
    {
        run("Bozja native DRN4 Phantom client six components initial and reduced bounds", PhantomClient);
        run("Bozja native DRN4 Phantom standard cone rect ordinary Finish no Effect TTL", PhantomStandard);
        run("Bozja native DRN4 Phantom Raidwide dynamic raw NPC clock", PhantomDamage);
        run("Bozja native DRN4 Phantom ArenaChange animation replacement four seconds no timeout real polygon", PhantomArena);
        run("Bozja native DRN4 Phantom Swirling cast location Actor direction Finish first match eight explosions cached future", PhantomExaflare);
        run("Bozja native DRN4 Phantom Undying forward30 immune ignored no wall stop first caster raw rectangle", () => PhantomHatred(false));
        run("Bozja native DRN4 Phantom Undying exaflare shelters raw intersection and imminent only unsafe", () => PhantomHatred(true));
        run("Bozja native DRN4 Phantom real Feed six cold components lost animation then actual shrink normalized outputs", PhantomFeed);
        run("Bozja native DRN4 Phantom two rounded world endpoints raw rectangle boundary and actual converter parameters", PhantomRect);
    }

    private static void PhantomClient()
    {
        using var f = new Fixture(19);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(760)?.TerritoryType.RowId == 936
            && Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9755)?.Singular.ToString() == f.M.Definition.Name
            && f.M.Definition.Rules.Length == 6 && f.M.Center == f.Source.Arena.Center.ToVec3(), "DRN4 client");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRN4Phantom.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRN4 action " + action);
        for (var x = -25f; x <= 25; x += .47f)
            for (var z = -31f; z <= 31; z += .53f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DRN4 actual initial bounds");
            }
    }

    private static void PhantomStandard()
    {
        foreach (var name in new[] { "VileWave", "CreepingMiasma" })
        {
            using var f = new Fixture(19);
            var source = f.Activate<GenericAOEs>(name);
            var rule = f.M.Definition.Rules.Single(r => r.Component == name);
            var spell = f.Start(source, rule.Actions[0]);
            CompareMetalFoxBits(f, source, name, "DRN4 standard");
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(rule.Actions[0]));
            f.M.Effect(2, rule.Actions[0]);
            f.Now = START.AddSeconds(50); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, name, "DRN4 no Effect or TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, name, "DRN4 watched Finish");
        }
    }

    private static void PhantomDamage()
    {
        using var f = new Fixture(19);
        var source = f.Activate<RaidwideCast>("MaledictionOfAgony");
        var spell = f.Start(source, 22447, total: 4);
        foreach (var delta in new[] { 0d, 2.31d, 30d })
        {
            f.Now = START.AddSeconds(delta); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            spell.ElapsedTime = MathF.Min((float)delta, spell.TotalTime);
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), "DRN4 raw capped NPC clock");
            var hints = new BossComponent.GlobalHints(); source.AddGlobalHints(hints);
            Check(hints.SequenceEqual(f.M.GlobalHints()), "DRN4 global raidwide");
        }
    }

    private static void PhantomArena()
    {
        using var f = new Fixture(19);
        var source = f.Activate<GenericAOEs>("ArenaChange");
        var feature = f.Add(40, 0x1EA1A1, f.Center);
        f.Sync();
        source.OnActorEAnim(feature, 0x1234); f.M.EAnim(40, 0x1234);
        Check(source.ActiveAOEs(0, f.Viewer).Length == 0 && !f.M.Visible().Any(), "DRN4 no guessed animation");
        void Compare(string step)
        {
            var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible().Where(a => a.Cast.Rule.Component == "ArenaChange").ToArray();
            Check(expected.Length == actual.Length, step + " count");
            for (var i = 0; i < actual.Length; ++i)
            {
                Check(expected[i].Origin.ToVec3() == actual[i].Cast.Origin
                    && expected[i].Activation == actual[i].Cast.Activation, step + " origin clock");
                for (var x = -25f; x <= 25; x += .43f)
                    for (var z = -32f; z <= 32; z += .61f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(expected[i].Check(new(p.X, p.Z)) == actual[i].Cast.Contains(p), step + " exact two strips");
                    }
            }
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "ArenaChange").ToArray(), step);
            f.Source.Arena.Bounds.ScreenHalfSize = 200;
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
            var shapes = new List<MechanicPreviewShape>();
            MechanicDrawConvert.Convert(sink.Commands, 0, shapes, [], []);
            var displays = actual.Select(a => a.Cast.Shape.Resolve(a.Cast.Origin, 0, null)).ToArray();
            for (var x = -25f; x <= 25; x += .47f)
                for (var z = -35f; z <= 35; z += .59f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(shapes.Any(s => s.Shape.Contains(p, 0)) == displays.Any(s => s.Contains(p, 0)),
                        step + " actual DrawSink geometry separate from cached navigation");
                }
            for (var x = -25f; x <= 25; x += .47f)
                for (var z = -31f; z <= 31; z += .53f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), step + " actual bounds");
                }
        }
        source.OnActorEAnim(feature, 0x00010002); f.M.EAnim(40, 0x00010002);
        Compare("DRN4 animated preview");
        f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Compare("DRN4 no timeout shrink");
        source.OnActorEAnim(feature, 0x00010002); f.M.EAnim(40, 0x00010002);
        Compare("DRN4 duplicate animation replaces clock");
        source.OnActorEAnim(feature, 0x00080010); f.M.EAnim(40, 0x00080010);
        Compare("DRN4 actual shrink");
        source.OnActorEAnim(feature, 0x00010002); f.M.EAnim(40, 0x00010002);
        Compare("DRN4 shared polygon warm second animation shifts Check but not Distance");
        var point = new Vector3(202, 0, -392);
        Check(source.ActiveAOEs(0, f.Viewer)[0].Check(new(point.X, point.Z))
            && f.M.Visible().Single().Cast.Contains(point), "DRN4 second Check shifts four units");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        Check(ai.ForbiddenZones[0].Item1.Distance(new(point.X, point.Z)) > 0
            && !f.M.Navigation(f.NativeViewer).Single().Convert(f.M.Center, f.M.SampleRadius).Any(s => s.Contains(point, 0)),
            "DRN4 first Distance origin remains locked");
    }

    private static void PhantomExaflare()
    {
        using var f = new Fixture(19);
        var source = f.Activate<SimpleExaflare>("SwirlingMiasma");
        var actor = f.W.Actors.Find(2)!;
        actor.PosRot = new(f.Center + Vector3.UnitX * 10, 1.2f);
        f.Set(2, a => a with { Position = actor.Position.ToVec3(), Rotation = 1.2f });
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 22441), Location = f.Center + Vector3.UnitZ * 2,
            Rotation = new(.3f), TotalTime = 8 };
        actor.CastInfo = spell; source.OnCastStarted(actor, spell);
        f.Set(2, a => a with { Cast = new(22441, 0, spell.Location, .3f, f.Now, f.Now.AddSeconds(8.3f), false, false)
            { IsSpell = true, TotalTime = 8 } });
        f.Sync(); source.Update();
        void Compare(string step)
        {
            Check(source.Lines.Select(l => (l.Next.ToVec3(), l.Advance.ToVec3(), l.NextExplosion, l.ExplosionsLeft))
                .SequenceEqual(f.M.Exaflares.Select(l => (l.Cast.Origin, l.Advance, l.Cast.Activation, l.Left))), step + " raw lines");
            Check(source.NumLinesFinished == f.M.NumPhantomLinesFinished, step + " finished count");
            CompareMetalFoxBits(f, source, "SwirlingMiasma", step, Colors.AOE, true);
        }
        Compare("DRN4 Actor direction versus packet angle");
        f.Now = START.AddSeconds(50); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); source.Update(); f.M.Update(f.Now);
        Compare("DRN4 cache unchanged across clock");
        source.OnEventCast(actor, PatriotEvent(22441)); f.M.Effect(2, 22441);
        Compare("DRN4 Effect ignored");
        var unmatched = new ActorCastInfo { Action = new(ActionType.Spell, 22442), Location = f.Center + Vector3.UnitX * 20 };
        source.OnCastFinished(actor, unmatched);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 22442, Location = unmatched.Location });
        source.Update(); f.M.Update(f.Now); Compare("DRN4 unmatched Finish ignored");
        for (var i = 0; i < 8; ++i)
        {
            var point = source.Lines[0].Next.ToVec3() + (i == 0 ? new Vector3(1, 0, -1) : Vector3.Zero);
            var finish = new ActorCastInfo { Action = new(ActionType.Spell, i == 0 ? 22441u : 22442u), Location = point };
            source.OnCastFinished(f.W.Actors.Find(1)!, finish);
            f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast! with { ActionId = finish.Action.ID, Location = point });
            source.Update(); f.M.Update(f.Now);
            Compare("DRN4 cross-source ordinary Finish " + i);
        }
        Check(f.M.Exaflares.Count == 0 && f.M.NumPhantomLinesFinished == 1, "DRN4 eight actual explosions");
    }

    private static void PhantomHatred(bool withLines)
    {
        using var f = new Fixture(19);
        var exa = f.Activate<SimpleExaflare>("SwirlingMiasma");
        var source = f.Activate<SimpleKnockbacks>("UndyingHatred");
        if (withLines)
        {
            f.Move(2, f.Center - Vector3.UnitZ * 10); f.Start(exa, 22441);
            f.Add(3, 0x233C, f.Center + Vector3.UnitX * 12); f.Sync(); f.Start(exa, 22441, 3);
            exa.Update(); f.M.Update(f.Now);
        }
        var spell = f.Start(source, 22444, 1, total: 6);
        f.Viewer.Statuses[0] = new(160, 0, f.Now.AddSeconds(1000), 100);
        source.OnStatusGain(f.Viewer, ref f.Viewer.Statuses[0]);
        f.Set(100, a => a with { Statuses = [new(160, 0, 100, f.Now.AddSeconds(1000)) { SlotIndex = 0 }] }); f.Sync();
        foreach (var offset in new[] { new Vector3(0, 0, -20), Vector3.Zero, new Vector3(10, 0, 24) })
        {
            f.Move(100, f.Center + offset);
            var expected = source.CalculateMovements(0, f.Viewer);
            var actual = f.M.PhantomMovements(f.NativeViewer).ToArray();
            Check(expected.Count == actual.Length && expected.Count == 1
                && Vector3.Distance(expected[0].to.ToVec3(), actual[0].To) < .00002f
                && Vector3.Distance(actual[0].From, actual[0].To) > 29.99f, "DRN4 no wall clipping no immunity");
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => h.Item1).SequenceEqual(f.M.Hints(f.NativeViewer)
                .Where(h => h.Text == "About to be knocked into danger!").Select(h => h.Text)), "DRN4 unsafe current imminent only and out of bounds");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "UndyingHatred").ToArray();
            Check(zones.Length == 1 && ai.ForbiddenZones.Count == 1, "DRN4 first caster one raw zone");
            for (var x = -30f; x < 30; x += .79f)
                for (var z = -30f; z < 30; z += .87f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(ai.ForbiddenZones[0].Item1.Distance(new(p.X, p.Z)) == zones[0].Distance(p.X, p.Z),
                        "DRN4 raw rectangle or inverted rectangle intersection");
                }
            CompareMetalFoxZones(f, ai, zones, "DRN4 true knockback conversion");
        }
        source.OnCastFinished(f.W.Actors.Find(1)!, spell);
        f.M.Finish(f.M.Actors[1], f.M.Actors[1].Cast!);
        Check(!f.M.PhantomMovements(f.NativeViewer).Any(), "DRN4 ordinary Finish removes caster");
    }

    private static void PhantomFeed()
    {
        Initialize();
        using var session = new MechanicSession("DRN4-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(936);
        var model = region.Encounters.Single(m => m.Definition.Key == "DRN4Phantom");
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x30AC, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, HitboxRadius = .5f },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitZ * 20, Hp = 1000, HitboxRadius = .5f },
            new() { Id = 40, Oid = 0x1EA1A1, Kind = 7, Position = center, EventAnimation = 0x10002 },
        };
        foreach (var (rule, i) in model.Definition.Rules.Select((r, i) => (r, i)).Where(e => e.r.Actions.Length > 0))
            actors.Add(new() { Id = (ulong)(20 + i), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center + Vector3.UnitX * 5, Rotation = 1.2f, Hp = 1000,
                Cast = new(rule.Actions[0], 100, center + Vector3.UnitZ * 5, .4f, START, START.AddSeconds(4.3f), false, false)
                    { TotalTime = 4, IsSpell = true } });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double seconds, uint animation = 0)
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (animation != 0) feed.Enqueue(new MechanicEventEObjAnimation(40, (ushort)(animation >> 16), (ushort)animation));
            feed.Flush();
            session.Update();
            region.Synchronize(actors.ToArray(), [new(0, "", 100), new(1, "", 101)], 100, now, true,
                () => { if (animation != 0) region.EAnim(40, animation); });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x30AC);
            var viewer = session.World.Actors.Find(100)!;
            Check(source.Components.Count == 6 && model.Active, "DRN4 six actual active components");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(model.Actors[100]).ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Color, a.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation,
                    a.Danger ? Colors.Danger : a.Cast.Rule.Kind == BozjaRuleKind.PhantomExaflare ? Colors.AOE : 0, a.Risky))),
                "DRN4 full Feed ordered previews clocks colors");
            var ai = new AIHints();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(model.Actors[100]);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)),
                "DRN4 full Feed normalized clocks");
            for (var i = 0; i < zones.Length; ++i)
            {
                var e = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, model.Center, model.SampleRadius, out _);
                var a = zones[i].Convert(model.Center, model.SampleRadius).ToArray();
                Check(e.Count == a.Length, "DRN4 full Feed conversion count");
                for (var x = -29f; x < 29; x += 1.17f)
                    for (var z = -29f; z < 29; z += 1.43f)
                    {
                        var p = model.Center + new Vector3(x, 0, z);
                        Check(e.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == a.Any(s => s.Contains(p, 0)), "DRN4 full Feed actual converted geometry");
                    }
            }
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(model.Actors[100]).Select(h => (h.Text, h.Risky))), "DRN4 full Feed both hint streams");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(model.Actors[100]).Select(d => (d.Mask, d.Activation, d.Type))), "DRN4 full Feed predicted damage");
        }
        Frame(0, 0x10002);
        Check(!model.Visible().Any(a => a.Cast.Rule.Component == "ArenaChange"), "DRN4 animation before activation not restored");
        Frame(.1, 0x10002);
        Frame(10);
        Frame(11, 0x80010);
        for (var i = 4; i < actors.Count; ++i) actors[i] = actors[i] with { Cast = null };
        Frame(12);
    }

    private static void PhantomRect()
    {
        using var f = new Fixture(19);
        foreach (var rotation in new[] { 0f, .37f, .4f, 1.2f, -2.7f, MathF.PI })
        {
            var dir = new Angle(rotation).ToDirection();
            var from = f.Source.Arena.Center + 24f * dir;
            var to = f.Source.Arena.Center - 16f * dir;
            var source = new SDRect(from, to, 24);
            var native = new BozjaPhantomRectTemplate(from.ToVec3(), to.ToVec3());
            var expected = MechanicHintsConvert.ConvertForbiddenZone(source, 0, f.Center, f.M.SampleRadius, out _)
                .Single();
            var geometry = expected.Template.Resolve(expected.Position, expected.Rotation, null);
            var actual = (MasterPlayer.Spatial.Geometry.RectangleGeometry)native.Resolve(default, 0, null);
            Check(geometry.Position == actual.Position && geometry.Rotation == actual.Rotation
                && ((MasterPlayer.Spatial.Geometry.RectangleGeometry)geometry).Length == actual.Length
                && ((MasterPlayer.Spatial.Geometry.RectangleGeometry)geometry).Width == actual.Width,
                "DRN4 actual bridge rounded length origin and rotation " + rotation);
            var point = new Vector3(225.4559783935547f, 0, -368.0994567871094f);
            Check(source.Distance(new(point.X, point.Z)) == native.Distance(point.X, point.Z), "DRN4 independent boundary counterexample");
            for (var x = -25f; x < 25; x += .79f)
                for (var z = -30f; z < 30; z += .87f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(source.Distance(new(p.X, p.Z)) == native.Distance(p.X, p.Z), "DRN4 exact raw two-point SDRect");
                }
        }
    }
}
