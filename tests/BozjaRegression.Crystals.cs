using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunCrystals(Action<string, Action> run)
    {
        run("Bozja native CE41 client twentyone AIDs ten components exact pull arena", CrystalClient);
        run("Bozja native CE41 four simple AOEs true Finish duplicate no invented knockback", CrystalStandard);
        run("Bozja native CE41 two LoS true frozen polygon safe hitbox range60 live blockers", CrystalSightGeometry);
        run("Bozja native CE41 LoS no blockers earliest original caster wrong Finish FIFO", CrystalSightQueue);
        run("Bozja native CE41 Earthbreaker exact three stages cached publication wrong phase axis tolerance", CrystalEarth);
        run("Bozja native CE41 cold later Earth stages and Hailfire visual cannot invent history", CrystalCold);
        run("Bozja native CE41 Shardstrike raw target reference finish targetID dead crystal union first clock", CrystalSpread);
        run("Bozja native CE41 Shardstrike one party still crystal union exact cache order generations", CrystalCache);
        run("Bozja native CE41 Hailfire four icons absolute ListSort dynamic bait all targets FIFO", CrystalHailfire);
        run("Bozja native CE41 Hailfire dead target source hide revive original destroyed generation near NaN", CrystalHailfireReferences);
        run("Bozja native CE41 dynamic raw tankbuster exact source mask clock", () => Damage(13));
        run("Bozja native CE41 actual Feed ten cold components icon source target exact and next frame", CrystalFeed);
        run("Bozja native CE41 unordered Party slots source raw and Normalize union bait order", CrystalPartyOrder);
        run("Bozja native CE41 destroyed live old bait target new same ID viewer draw ID navigation reference", CrystalBaitDraw);
        run("Bozja native CE41 module Actor markers initialize cache but old real DrawConvert skips including outside", CrystalMarkerBoundary);
    }

    private static void CrystalClient()
    {
        Initialize();
        var definition = BozjaCatalog.Definitions[13];
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(21)?.Name.ToString() == definition.Name,
            "CE41 real DE21.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE41WithDiremiteAndMain.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE41 AID extra " + action);
        Check(definition.Rules.Length == 10 && definition.Territory == 975 && definition.PullRadius == 30,
            "CE41 ten actual components.");
        using var f = new Fixture(13);
        Check(f.Source.Arena.Center.ToVec3() == definition.Center, "CE41 fixed center.");
        for (var x = -31f; x <= 31; x += .43f)
            for (var z = -31f; z <= 31; z += .57f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0),
                    "CE41 exact32 polygon.");
            }
    }

    private static void CrystalStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[13].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(13);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "CE41 two raw starts " + rule.Component);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            CompareMetalFoxBits(f, source, rule.Component, "CE41 Effect retains");
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "CE41 no timeout");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "CE41 watched Finish first");
        }
    }

    private static Actor Crystal(Fixture f, ulong id, uint oid, Vector3 p, bool dead = false)
    {
        var result = f.Add(id, oid, p);
        result.HitboxRadius = 1.6f; result.IsDead = dead;
        f.Set(id, a => a with { HitboxRadius = 1.6f, Dead = dead, Targetable = false });
        f.Sync();
        return result;
    }

    private static void CrystalSightGeometry()
    {
        foreach (var (name, action, oid) in new[] { ("CrystallineStingers", 24077u, 0x31CDu), ("AetherialStingers", 24078u, 0x31CEu) })
        {
            using var f = new Fixture(13);
            Crystal(f, 30, oid, f.Center + new Vector3(4.123f, 0, 6.257f));
            Crystal(f, 31, oid, f.Center + new Vector3(8, 0, 1), true);
            Crystal(f, 32, oid == 0x31CD ? 0x31CEu : 0x31CDu, f.Center + Vector3.UnitZ * 9);
            var source = f.Activate<CastLineOfSightAOE>(name);
            f.Start(source, action);
            CompareMeteor(f, source, "CE41 " + name, name);
            var original = f.M.Visible().Single().Cast;
            f.Move(30, f.Center + Vector3.UnitX * 20);
            f.W.Actors.Find(30)!.IsDead = true; f.Set(30, a => a with { Dead = true }); f.Sync();
            CompareMeteor(f, source, "CE41 frozen blocker moves/dies", name);
            f.Remove(30);
            CompareMeteor(f, source, "CE41 frozen blocker destroys", name);
            Check(ReferenceEquals(original, f.M.Visible().Single().Cast), "CE41 safezone frozen.");
            Check(!f.M.Navigation(f.NativeViewer).Any(z => z.Obstacle), "CE41 default safeInsideHitbox creates no obstacle.");
        }
    }

    private static void CrystalSightQueue()
    {
        using var f = new Fixture(13);
        var source = f.Activate<CastLineOfSightAOE>("CrystallineStingers");
        f.Start(source, 24077);
        CompareMeteor(f, source, "CE41 no blocker no blanket danger", "CrystallineStingers");
        Crystal(f, 30, 0x31CD, f.Center + new Vector3(2, 0, 1));
        f.Add(3, 0x233C, f.Center + new Vector3(-15, 0, -5)); f.Sync();
        var second = f.Start(source, 24077, 3, total: 9);
        CompareMeteor(f, source, "CE41 earliest origin later callback clock", "CrystallineStingers");
        var wrong = f.Add(4, 0x233C, f.Center);
        f.Sync();
        var spell = f.M.Actors[3].Cast!;
        source.OnCastFinished(wrong, second); f.M.Finish(f.M.Actors[4], spell);
        CompareMeteor(f, source, "CE41 wrong reference still pops safe FIFO", "CrystallineStingers");
        f.Start(source, 24077, 3, total: .5f);
        CompareMeteor(f, source, "CE41 new earlier source same independent queue", "CrystallineStingers");
    }

    private static void CrystalEarth()
    {
        using var f = new Fixture(13);
        var source = f.Activate<ConcentricAOEs>("Earthbreaker");
        var first = f.Start(source, 24082);
        source.Update();
        CompareMetalFoxBits(f, source, "Earthbreaker", "CE41 first circle");
        source.OnCastStarted(f.W.Actors.Find(2)!, first); f.M.Start(f.M.Actors[2]);
        source.Update(); f.Sync();
        CompareMetalFoxBits(f, source, "Earthbreaker", "CE41 duplicate overlapping sequence");
        void Finish(uint action, float offset)
        {
            f.Now = f.Now.AddSeconds(1); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            var at = f.W.Actors.Find(2)!.Position.ToVec3() + new Vector3(offset, 0, offset);
            source.OnCastFinished(f.W.Actors.Find(1)!, new() { Action = new(ActionType.Spell, action), Location = at });
            f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast! with { ActionId = action, Location = at });
            CompareMetalFoxBits(f, source, "Earthbreaker", "CE41 cached before Update");
            source.Update(); f.Sync();
            CompareMetalFoxBits(f, source, "Earthbreaker", "CE41 advanced " + action + "/" + offset);
            Check(source.NumCasts == f.M.NumEarthCasts, "CE41 unmatched Finish counter.");
        }
        Finish(24084, 0); Finish(24082, 1.001f); Finish(24082, 1); Finish(24083, .9f);
        Finish(24084, 0); Finish(24082, 0); Finish(24083, 0); Finish(24084, 0);
        Check(!f.M.Visible().Any(), "CE41 two sequences fully consumed.");
    }

    private static void CrystalCold()
    {
        foreach (var action in new uint[] { 24083, 24084, 24088 })
        {
            using var f = new Fixture(13);
            var component = action == 24088 ? "Hailfire" : "Earthbreaker";
            var source = f.Activate<BossComponent>(component);
            f.Start(source, action); source.Update();
            Check(!f.M.Visible().Any() && f.M.Hailfires.Count == 0, "CE41 later/current visual has no direction/icon history.");
        }
    }

    private static void CompareCrystalSpread(Fixture f, CastStackSpread source, string step)
    {
        Check(source.Spreads.Select(s => (s.Target.InstanceID, s.Activation))
            .SequenceEqual(f.M.Casts.Where(c => c.Rule.Kind == BozjaRuleKind.CrystalSpread)
                .Select(c => (c.Target!.Actor.Id, c.Activation))), step + " raw spread order/clock.");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
            .Where(h => h.Text.StartsWith("Spread,")).Select(h => (h.Text, h.Risky))), step + " exact custom hints.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Shardstrike").ToArray(), step);
        ai.Normalize();
        CompareMetalFoxZones(f, ai, f.M.NavigationForHost(f.NativeViewer).Where(z => z.Component == "Shardstrike").ToArray(),
            step + " Normalize before union expansion");
        Check(ai.PredictedDamage.Count == 0 && !f.M.Damage(f.NativeViewer).Any(), "CE41 spreads do not predict damage.");
        Check(source.NumFinishedSpreads == f.M.NumFinishedSpreads, "CE41 raw finish count.");
    }

    private static void CrystalSpread()
    {
        using var f = new Fixture(13);
        Crystal(f, 30, 0x31CE, f.Center + Vector3.UnitZ * 12, true);
        Crystal(f, 31, 0x31CD, f.Center + Vector3.UnitX * 13);
        var source = f.Activate<CastStackSpread>("Shardstrike");
        var first = f.Start(source, 24087, total: 8);
        f.Add(3, 0x233C, f.Center);
        f.Start(source, 24087, 3, total: 3, target: 101);
        CompareCrystalSpread(f, source, "CE41 independent raw spreads");
        f.W.Actors.Find(100)!.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        CompareCrystalSpread(f, source, "CE41 dead raw own target still cage warning/union");
        Check(f.M.CrystalSpreads().Count() == 1 && f.M.Navigation(f.NativeViewer).Single(z => z.Union != null).Activation
            == START.AddSeconds(8 + .3f), "CE41 dead first raw target clock.");
        source.OnCastFinished(f.W.Actors.Find(1)!, first); f.M.Finish(f.M.Actors[1], f.M.Casts[0].Source.Cast!);
        CompareCrystalSpread(f, source, "CE41 wrong caster correct target Finish");
        f.Move(101, f.Center + new Vector3(3.123f, 0, 2.345f));
        CompareCrystalSpread(f, source, "CE41 quantized live spread target");
    }

    private static void CrystalCache()
    {
        using var f = new Fixture(13);
        Crystal(f, 30, 0x31CE, f.Center + Vector3.UnitZ * 10);
        Crystal(f, 31, 0x31CD, f.Center + Vector3.UnitX * 10);
        f.M.ObserveCrystalDrawing();
        var source = f.Activate<CastStackSpread>("Shardstrike");
        f.Source.Enemies(new uint[] { 0x31CD, 0x31CE });
        f.Party.RemoveAt(1);
        f.W.Execute(new PartyState.OpModify(1, default)); f.Sync();
        f.Start(source, 24087);
        CompareCrystalSpread(f, source, "CE41 Party1 retains crystal union");
        Check(f.M.Navigation(f.NativeViewer).Single().Union!.SequenceEqual(new[]
            { f.M.Actors[31].Actor.Position, f.M.Actors[30].Actor.Position }), "CE41 Dim list then Corrupted, not world interleave.");
        var original = f.M.Actors[31];
        f.Remove(31); Crystal(f, 31, 0x31CD, f.Center - Vector3.UnitX * 15);
        CompareCrystalSpread(f, source, "CE41 destroyed new crystal generation cache");
        Check(!f.M.Crystals.Contains(original), "CE41 old original removed from enemy cache.");
    }

    private static void CompareHailfire(Fixture f, GenericBaitAway source, string step)
    {
        Check(source.CurrentBaits.Select(b => (b.Source.InstanceID, b.Target.InstanceID, b.Activation))
            .SequenceEqual(f.M.Hailfires.Select(b => (b.Source.Actor.Id, b.Target.Actor.Id, b.Activation))),
            step + " raw target/order/clocks.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Hailfire").ToArray(), step);
        ai.Normalize();
        CompareMetalFoxZones(f, ai, f.M.NavigationForHost(f.NativeViewer).Where(z => z.Component == "Hailfire").ToArray(),
            step + " normalized bait sequence");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
            .Where(h => h.Text.Contains("bait") || h.Text.StartsWith("Bait")).Select(h => (h.Text, h.Risky))),
            step + " bait personal hints.");
        Check(!f.M.Damage(f.NativeViewer).Any() && ai.PredictedDamage.Count == 0, "CE41 baits no damage.");
    }

    private static void HailfireIcon(Fixture f, GenericBaitAway source, ulong actor, uint icon)
    {
        source.OnEventIcon(f.W.Actors.Find(actor)!, icon, 101);
        f.M.Icon(actor, icon, 101);
    }

    private static void CrystalHailfire()
    {
        using var f = new Fixture(13);
        var source = f.Activate<GenericBaitAway>("Hailfire");
        foreach (var icon in new uint[] { 82, 79, 81, 80, 79 })
        {
            HailfireIcon(f, source, icon % 2 == 0 ? 100UL : 101UL, icon);
            CompareHailfire(f, source, "CE41 reverse/repeat icon " + icon);
        }
        f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        HailfireIcon(f, source, 100, 79);
        CompareHailfire(f, source, "CE41 absolute activation sorts past expiry before new");
        f.Move(100, f.Center + new Vector3(6, 0, 7));
        CompareHailfire(f, source, "CE41 dynamic source/target without location lock");
        for (var i = 0; i < 7; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(24089)); f.M.Effect(2, 24089);
            CompareHailfire(f, source, "CE41 arbitrary source FIFO " + i);
        }
    }

    private static void CrystalHailfireReferences()
    {
        using var f = new Fixture(13);
        var source = f.Activate<GenericBaitAway>("Hailfire");
        HailfireIcon(f, source, 101, 79);
        f.W.Actors.Find(101)!.IsDead = true; f.Set(101, a => a with { Dead = true }); f.Sync();
        CompareHailfire(f, source, "CE41 allow dead target");
        // Keep phase alive while directly varying source Dead on both old refs.
        f.W.Actors.Find(1)!.IsDead = true;
        f.M.Primary!.Actor = f.M.Primary.Actor with { Dead = true };
        CompareHailfire(f, source, "CE41 source dead hides but retains");
        f.W.Actors.Find(1)!.IsDead = false;
        f.M.Primary.Actor = f.M.Primary.Actor with { Dead = false };
        var old = f.M.Hailfires[0].Target;
        f.Remove(101); f.Add(101, 0, f.Center + Vector3.UnitX * 4, ActorType.Player); f.Sync();
        CompareHailfire(f, source, "CE41 old destroyed dead target is retained across ID replacement");
        Check(ReferenceEquals(old, f.M.Hailfires[0].Target), "CE41 original target not rebound.");
        HailfireIcon(f, source, 100, 80);
        f.Move(101, f.Center + Vector3.UnitX);
        CompareHailfire(f, source, "CE41 close party unclamped Asin uses empty converted geometry");
    }

    private static void CrystalFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE41-full-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[13]);
        var center = model.Center;
        var actions = new uint[] { 24072, 24073, 24074, 24077, 24078, 24080, 24082, 24085, 24087, 24088 };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31CC, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true, Targetable = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
            new() { Id = 40, Oid = 0x31CD, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 5, HitboxRadius = 1.6f, Hp = 1000 },
            new() { Id = 41, Oid = 0x31CE, Kind = 2, SubKind = 5, Position = center + Vector3.UnitZ * 5, HitboxRadius = 1.6f, Hp = 1000 },
        };
        for (var i = 0; i < actions.Length; ++i)
            actors.Add(new() { Id = (ulong)(10 + i), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center, Hp = 1000, Cast = new(actions[i], 100, center, .4f, START,
                    START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true } });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double seconds, bool icon = false)
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (icon) feed.Enqueue(new MechanicEventIcon(79, 100, 101));
            feed.Flush(); session.Update();
            model.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, now, false);
            if (icon) model.Icon(101, 79, 100);
            model.Update(now);
        }
        Frame(0, true);
        var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31CC);
        Check(module.Components.Count() == 10 && model.Active && model.Hailfires.Count == 0,
            "CE41 actual source ten cold components, icon before activation is not recovered.");
        Frame(1, true);
        foreach (var source in module.Components.OfType<GenericAOEs>())
        {
            var expected = source.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
            var actual = model.Visible().Where(a => a.Cast.Rule.Component == source.GetType().Name).ToArray();
            Check(expected.Length == actual.Length && expected.Select(a => (a.Origin.ToVec3(), a.Activation))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Activation))),
                "CE41 true Feed cold shapes " + source.GetType().Name);
        }
        var hailfire = module.Components.OfType<GenericBaitAway>().Single();
        Check(hailfire.CurrentBaits.Single().Target.InstanceID == 101 && model.Hailfires.Single().Target.Actor.Id == 101
            && hailfire.CurrentBaits.Single().Activation == model.Hailfires.Single().Activation,
            "CE41 actual raw MarkerId source Actor identity, ignored targetID.");
        var ai = new AIHints();
        foreach (var component in module.Components)
            component.AddAIHints(0, session.World.Actors.Find(100)!, default, ai);
        ai.Normalize();
        var zones = model.NavigationForHost(model.Actors[100]);
        Check(ai.ForbiddenZones.Count == zones.Length, "CE41 full Feed normalized zone count.");
        for (var i = 0; i < zones.Length; ++i)
        {
            Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, "CE41 full Feed normalized clock " + i);
            var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, 29.5f, out _);
            var actual = zones[i].Convert(center, 29.5f).ToArray();
            Check(expected.Count == actual.Length, "CE41 full Feed normalized expanded shape count " + i);
            for (var x = -29f; x <= 29; x += 2.31f)
                for (var z = -29f; z <= 29; z += 2.67f)
                {
                    var p = center + new Vector3(x, 0, z);
                    Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                        == actual.Any(s => s.Contains(p, 0)), "CE41 full Feed source Normalize then Union expansion " + i);
                }
        }
        Frame(100);
        Check(model.Hailfires.Count == 1 && model.Visible().Count() == 7, "CE41 unchanged world next Framework retains history without TTL.");
    }

    private static void CrystalPartyOrder()
    {
        foreach (var spread in new[] { true, false })
        {
            using var f = new Fixture(13);
            f.Add(102, 0, f.Center + new Vector3(10, 0, 12), ActorType.Player);
            f.W.Execute(new PartyState.OpModify(2, new(102, 102, false, "Third")));
            f.Party.Insert(0, new(2, "", 102)); f.Sync();
            Crystal(f, 30, 0x31CD, f.Center + Vector3.UnitZ * 10);
            if (spread)
            {
                var source = f.Activate<CastStackSpread>("Shardstrike");
                f.Start(source, 24087);
                CompareCrystalSpread(f, source, "CE41 shuffled slots raw and Normalize union");
            }
            else
            {
                var source = f.Activate<GenericBaitAway>("Hailfire");
                HailfireIcon(f, source, 100, 79);
                CompareHailfire(f, source, "CE41 shuffled slots bait raw and Normalize");
            }
        }
    }

    private static void CrystalBaitDraw()
    {
        using var f = new Fixture(13);
        f.Move(100, f.Center + Vector3.UnitZ * 10);
        var source = f.Activate<GenericBaitAway>("Hailfire");
        HailfireIcon(f, source, 100, 79);
        var old = f.M.Hailfires.Single().Target;
        f.Remove(100);
        f.Add(100, 0, f.Center - Vector3.UnitZ * 10, ActorType.Player); f.Sync();
        Check(!old.Actor.Dead && old.Destroyed && !ReferenceEquals(old, f.NativeViewer), "CE41 destroyed live original target.");
        CompareHailfire(f, source, "CE41 destroyed old target same ID viewer");
        Check(f.M.Navigation(f.NativeViewer).Single().Shape is RayRectangleTemplate,
            "CE41 new viewer reference must avoid old target rectangle.");
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink))
        {
            source.DrawArenaBackground(0, f.Viewer);
            source.DrawArenaForeground(0, f.Viewer);
        }
        var rectangle = sink.Commands.OfType<MechanicDrawZoneRect>().Single();
        var bait = f.M.Hailfires.Single();
        var view = BozjaEncounter.HornView(bait);
        Check(rectangle.Outline && rectangle.LenFront == 40 && rectangle.LenBack == 0 && rectangle.HalfWidth == 2
            && rectangle.Origin.ToVec3() == view.Origin && rectangle.Direction == new Angle(view.Rotation).ToDirection()
            && bait.Target.Actor.Id == f.NativeViewer.Actor.Id,
            "CE41 same ID viewer gets own outline while navigation still compares original reference.");
    }

    private static void CrystalMarkerBoundary()
    {
        using var f = new Fixture(13);
        Crystal(f, 30, 0x31CD, f.Center + Vector3.UnitX * 40);
        Crystal(f, 31, 0x31CE, f.Center + Vector3.UnitZ * 4, true);
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink))
            f.Source.GetType().GetMethod("DrawEnemies", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(f.Source, [0, f.Viewer]);
        var markers = sink.Commands.OfType<MechanicDrawActorMarker>().ToArray();
        Check(markers.Length == 2 && markers.Any(m => !m.InsideBounds),
            "CE41 actual module source draws primary and alive outside crystal only.");
        var shapes = new List<MechanicPreviewShape>();
        var lines = new List<MechanicPreviewLine>();
        var extras = new List<MechanicRadarExtra>();
        Check(MechanicDrawConvert.Convert(sink.Commands, 0, shapes, lines, extras) == markers.Length
            && shapes.Count == 0 && lines.Count == 0 && extras.Count == 0,
            "CE41 actual old bridge consumes no Actor marker, including clamped outside position.");
    }
}
