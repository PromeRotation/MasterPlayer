using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using AID = BossMod.Shadowbringers.Foray.CriticalEngagement.CE44FamiliarFace.AID;

internal static partial class BozjaRegression
{
    private static ActorCastEvent HashmalEvent(uint action, ulong target = 0, Vector3 position = default)
        => new(new(ActionType.Spell, action), target, 0, 0, position, 0, 0, default);
    private static void RunHashmal(Action<string, Action> run)
    {
        run("Bozja native CE44 client all22 AIDs nine actual components exact arena pull", HashmalClient);
        run("Bozja native CE44 standard locked origin ray group any Finish 99 equal clocks", HashmalStandard);
        run("Bozja native CE44 delayed RockCutter frozen target slot clock dead destroy cross source Effect", HashmalRock);
        run("Bozja native CE44 delayed RockCutter missing nonparty self target counter first ID", HashmalRockTargets);
        run("Bozja native CE44 and CE34 delayed original target duplicate shuffled slots choose first actual slot", HashmalRockSlots);
        run("Bozja native CE44 shared raw CE41 dynamic target duplicate slots source FindSlot order", HashmalRawSlots);
        run("Bozja native CE44 AncientQuake float promoted delay overwrite Finish first actual Effect", HashmalQuake);
        run("Bozja native CE44 concurrent delayed damage actual Normalize sorts earlier Quake ahead of Rock", HashmalDamageOrder);
        run("Bozja native CE44 Roxxor ordinary spread R6 R7 no crystals no damage original reference", HashmalSpread);
        run("Bozja native CE44 towers duplicate Loc exact Finish actor position differs geometric holes host exterior", HashmalTowers);
        run("Bozja native CE44 towers edge notch exact old Nav and Draw consume exteriors no hole obstacles", HashmalTowerEdge);
        run("Bozja native CE44 exaflare actor versus cast origin direction future then current cached clock", HashmalExaflare);
        run("Bozja native CE44 exaflare Effect axis first match actual TargetPos unmatched null and eight explosions", HashmalExaflareEffects);
        run("Bozja native CE44 Hammer Created dead quantized callback clock top2 risky Finish FIFO no timeout", HashmalHammers);
        run("Bozja native CE44 cold current casts no invented tower hole history hammer cold callback clock", HashmalCold);
        run("Bozja native CE44 actual Feed nine cold components normalized combined output next frame", HashmalFeed);
        run("Bozja native CE44 actual source border capture keeps holes DrawConvert consumes only exterior", HashmalDraw);
    }

    private static void HashmalClient()
    {
        using var f = new Fixture(14);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(29)?.Name.ToString() == f.M.Definition.Name,
            "CE44 DE29.");
        foreach (var action in Enum.GetValues<AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE44 extra " + action);
        Check(f.M.Definition.Rules.Length == 9 && f.M.Definition.PullRadius == 30, "CE44 actual nine.");
        for (var x = -31f; x <= 31; x += .43f)
            for (var z = -31f; z <= 31; z += .57f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "CE44 polygon.");
            }
    }

    private static void HashmalStandard()
    {
        foreach (var (name, action) in new[] { ("TectonicEruption", 23826u), ("Towerfall", 23818u), ("ExtremeEdge", 23821u) })
        {
            using var f = new Fixture(14);
            var source = f.Activate<GenericAOEs>(name);
            var spell = f.Start(source, action);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, name, "CE44 duplicate standard");
            f.Move(2, f.Center + new Vector3(-13, 0, 14));
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(action)); f.M.Effect(1, action);
            f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, name, "CE44 locked no TTL/Effect");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, name, "CE44 Finish");
        }
        GroupOrder(14);
    }

    private static void CompareHashmalDamage(Fixture f, BossComponent source, string step)
    {
        var hints = new AIHints();
        source.AddAIHints(0, f.Viewer, PartyRolesConfig.Assignment.MT, hints);
        var actual = f.M.Damage(f.NativeViewer).ToArray();
        Check(hints.PredictedDamage.Count == actual.Length, step + " damage count");
        for (var i = 0; i < actual.Length; ++i)
            Check(hints.PredictedDamage[i].Players.Raw == actual[i].Mask
                && hints.PredictedDamage[i].Activation == actual[i].Activation, step + " mask clock " + i);
    }

    private static void HashmalRock()
    {
        using var f = new Fixture(14);
        var source = f.Activate<SingleTargetInstant>("RockCutter");
        var spell = f.Start(source, 23827);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        var old = f.M.DelayedHits[0].Target;
        f.W.Execute(new PartyState.OpModify(0, new(101, 101, false, "Changed")));
        f.Party[0] = new(0, "", 101);
        f.W.Actors.Find(100)!.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        f.Remove(100);
        f.Add(100, 0, f.Center + Vector3.UnitZ * 20, ActorType.Player); f.Sync();
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareHashmalDamage(f, source, "CE44 target slot clock immutable");
        Check(old.Destroyed && !ReferenceEquals(old, f.NativeViewer) && f.M.DelayedHits.Count == 2,
            "CE44 original target generations");
        source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23827, 100));
        f.M.Effect(1, 23827, 100);
        CompareHashmalDamage(f, source, "CE44 cross source first target");
        Check(f.M.DelayedHits.Count == 1 && f.M.NumDelayedHits == source.NumCasts, "CE44 FIFO duplicates");
    }

    private static void HashmalRockTargets()
    {
        foreach (var target in new ulong[] { 999, 2, 1 })
        {
            using var f = new Fixture(14);
            var source = f.Activate<SingleTargetInstant>("RockCutter");
            f.W.Actors.Find(2)!.TargetID = 101;
            f.Set(2, a => a with { TargetId = 101 }); f.Sync();
            f.Start(source, 23827, target: target);
            CompareHashmalDamage(f, source, "CE44 missing self nonparty " + target);
            source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23827, 888));
            f.M.Effect(1, 23827, 888);
            Check(f.M.NumDelayedHits == source.NumCasts, "CE44 unmatched still counter");
        }
    }

    private static void HashmalRockSlots()
    {
        foreach (var (definition, name, action) in new[] { (14, "RockCutter", 23827u), (7, "BullHorn", 20968u) })
        {
            using var f = new Fixture(definition);
            f.W.Execute(new PartyState.OpModify(3, new(100, 100, false, "Duplicate")));
            f.Party.Insert(0, new(3, "", 100)); f.Sync();
            var source = f.Activate<SingleTargetInstant>(name);
            f.Start(source, action);
            CompareHashmalDamage(f, source, "CE44/34 shuffled duplicated slot");
            Check(f.M.DelayedHits.Single().Slot == 0, "CE44/34 actual first slot not DTO order");
        }
    }

    private static void HashmalRawSlots()
    {
        using var f = new Fixture(13);
        f.W.Execute(new PartyState.OpModify(3, new(100, 100, false, "Duplicate")));
        f.Party.Insert(0, new(3, "", 100)); f.Sync();
        var source = f.Activate<SingleTargetCast>("CrystalNeedle");
        f.Start(source, 24085);
        CompareHashmalDamage(f, source, "CE41 dynamic duplicate target slots");
        Check(f.M.Damage(f.NativeViewer).Single().Mask == 1, "CE41 raw dynamic first actual slot");
    }

    private static void HashmalQuake()
    {
        using var f = new Fixture(14);
        var source = f.Activate<RaidwideInstant>("AncientQuake");
        var spell = f.Start(source, 23828);
        CompareHashmalDamage(f, source, "CE44 promoted float delay");
        f.Now = START.AddSeconds(2); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        f.Start(source, 23828, id: 1, total: 7);
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareHashmalDamage(f, source, "CE44 overwrite and Finish retains");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23832)); f.M.Effect(1, 23832);
        CompareHashmalDamage(f, source, "CE44 no invented Sanction");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23829)); f.M.Effect(2, 23829);
        CompareHashmalDamage(f, source, "CE44 first Effect clears");
        Check(!f.M.Damage(f.NativeViewer).Any(), "CE44 Quake empty");
    }

    private static void HashmalDamageOrder()
    {
        using var f = new Fixture(14);
        var rock = f.Activate<SingleTargetInstant>("RockCutter");
        var quake = f.Activate<RaidwideInstant>("AncientQuake");
        f.Start(rock, 23827, total: 10);
        f.Start(quake, 23828, id: 1, total: 1);
        var hints = new AIHints();
        rock.AddAIHints(0, f.Viewer, default, hints);
        quake.AddAIHints(0, f.Viewer, default, hints);
        hints.Normalize();
        var damage = f.M.DamageForHost(f.NativeViewer);
        Check(damage.Length == 2 && damage[0].Type == "Raidwide"
            && hints.PredictedDamage.Select(d => (d.Players.Raw, d.Activation))
                .SequenceEqual(damage.Select(d => (d.Mask, d.Activation))), "CE44 real normalized two component damage order");
    }

    private static void HashmalSpread()
    {
        using var f = new Fixture(14);
        var source = f.Activate<CastStackSpread>("Roxxor");
        var spell = f.Start(source, 23823);
        var rule = f.M.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Spread);
        void Compare(string step)
        {
            var hints = new AIHints(); source.AddAIHints(0, f.Viewer, PartyRolesConfig.Assignment.MT, hints);
            var actual = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Roxxor").ToArray();
            Check(actual.Length == hints.ForbiddenZones.Count && actual.All(z => z.Shape is CircleTemplate { Radius: 7 }),
                step + " R7 ordinary spread");
            var texts = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, texts);
            Check(texts.SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                step + " ordinary texts");
            Check(source.ActiveSpreads.Count == f.M.Spreads(rule).Count() && !f.M.Damage(f.NativeViewer).Any(),
                step + " dead filter no predictions");
        }
        Compare("CE44 own");
        f.Move(101, f.Center + Vector3.UnitX * 3); Compare("CE44 crowded own");
        f.W.Actors.Find(100)!.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        Compare("CE44 dead");
        f.W.Actors.Find(100)!.IsDead = false; f.Set(100, a => a with { Dead = false }); f.Sync();
        source.OnCastFinished(f.W.Actors.Find(1)!, spell); f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast!);
        Compare("CE44 Finish targetID");
    }

    private static void CompareHashmalArena(Fixture f, string step)
    {
        var expected = MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds, f.Source.Arena.Center, 0)!;
        for (var x = -31f; x <= 31; x += .43f)
            for (var z = -31f; z <= 31; z += .57f)
            {
                var p = f.M.Definition.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), step + " model holes " + p);
                Check(expected.Contains(p, 0) == f.M.HostArena.Contains(p, 0), step + " host exterior " + p);
            }
        Check(f.Source.Arena.Center.ToVec3() == f.M.Center, step + " arena center");
    }

    private static void TowerFinish(Fixture f, GenericAOEs source, uint action, Vector3 loc, Vector3 actorPos)
    {
        f.Move(2, actorPos);
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, action), Location = loc };
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], new(action, 0, loc, 0, f.Now, f.Now, false, false));
        CompareMetalFoxBits(f, source, "ControlTowerAppear", "CE44 tower Finish");
        CompareHashmalArena(f, "CE44 tower " + action);
    }

    private static void HashmalTowers()
    {
        using var f = new Fixture(14);
        var source = f.Activate<GenericAOEs>("ControlTowerAppear");
        var spell = f.Start(source, 23830);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        CompareMetalFoxBits(f, source, "ControlTowerAppear", "CE44 duplicate exact location");
        Check(f.M.Visible().Count() == 1, "CE44 dedup only exact Loc");
        var loc = f.M.Actors[2].Cast!.Location;
        TowerFinish(f, source, 23830, loc, f.Center + Vector3.UnitZ * 8);
        Check(f.M.ControlTowers.Count == 1 && !f.M.Arena.Contains(f.Center + Vector3.UnitZ * 8, 0)
            && f.M.HostArena.Contains(f.Center + Vector3.UnitZ * 8, 0), "CE44 separate model hole and host");
        TowerFinish(f, source, 23830, loc, f.Center + Vector3.UnitZ * 8);
        Check(f.M.ControlTowers.Count == 2, "CE44 CastLoc differs => duplicate ActorPos holes");
        TowerFinish(f, source, 23830, f.Center + Vector3.UnitX * 8, f.Center + Vector3.UnitX * 8);
        TowerFinish(f, source, 23818, f.Center, f.Center + Vector3.UnitZ * 8);
        Check(f.M.ControlTowers.Count == 2, "CE44 Towerfall removes first exact tower");
        TowerFinish(f, source, 23818, f.Center, f.Center + Vector3.UnitZ * 8);
        TowerFinish(f, source, 23818, f.Center, f.Center + Vector3.UnitX * 8);
        Check(ReferenceEquals(f.M.Arena, f.M.Definition.Arena), "CE44 restores DefaultArena");
    }

    private static void HashmalTowerEdge()
    {
        using var f = new Fixture(14);
        var source = f.Activate<GenericAOEs>("ControlTowerAppear");
        var edge = f.Center + Vector3.UnitX * 28;
        TowerFinish(f, source, 23830, edge, edge);
        Check(!f.M.HostArena.Contains(edge, 0) && !f.M.Navigation(f.NativeViewer).Any(z => z.Obstacle),
            "CE44 edge notch in exterior not new obstacle");
    }

    private static void HashmalExaflare()
    {
        using var f = new Fixture(14);
        var source = f.Activate<Exaflare>("IntractableLand");
        var position = f.W.Actors.Find(2)!.Position.ToVec3();
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 23819), TotalTime = 3,
            Location = position + new Vector3(10, 0, 10), Rotation = new(.4f) };
        f.W.Actors.Find(2)!.CastInfo = spell;
        source.OnCastStarted(f.W.Actors.Find(2)!, spell);
        f.Set(2, a => a with { Cast = new(23819, 0, spell.Location, .4f, f.Now, f.Now.AddSeconds(3.3f), false, false)
            { IsSpell = true, TotalTime = 3 } }); f.Sync();
        source.Update();
        CompareMetalFoxBits(f, source, "IntractableLand", "CE44 cast location differs", Colors.AOE);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.Update(); f.Sync();
        CompareMetalFoxBits(f, source, "IntractableLand", "CE44 unchanged cached past future clocks", Colors.AOE);
        f.Add(3, 0x233C, f.Center + new Vector3(-15, 0, -10)); f.Sync();
        f.Start(source, 23819, 3);
        source.Update();
        CompareMetalFoxBits(f, source, "IntractableLand", "CE44 future all lines before current all lines", Colors.AOE);
    }

    private static void HashmalExaflareEffects()
    {
        using var f = new Fixture(14);
        var source = f.Activate<Exaflare>("IntractableLand");
        var spell = f.Start(source, 23819);
        source.Update();
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(f.M.Exaflares.Single().Left == 8, "CE44 Finish no advance");
        f.M.Effect(1, 23820, position: null);
        Check(f.M.MissingExaflarePosition == 1 && f.M.Exaflares.Single().Left == 8, "CE44 absent raw coordinate diagnostic");
        for (var i = 0; i < 8; ++i)
        {
            var point = source.Lines[0].Next.ToVec3() + new Vector3(.9f, 0, .9f);
            f.Now = f.Now.AddSeconds(1); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            var e = HashmalEvent(23820, position: point);
            source.OnEventCast(f.W.Actors.Find(1)!, e); f.M.Effect(1, 23820, position: point);
            CompareMetalFoxBits(f, source, "IntractableLand", "CE44 callback before Update " + i, Colors.AOE);
            source.Update(); f.Sync();
            CompareMetalFoxBits(f, source, "IntractableLand", "CE44 real TargetPos drift " + i, Colors.AOE);
        }
        Check(!f.M.Exaflares.Any() && !f.M.Visible().Any(), "CE44 eight actual effects");
    }

    private static void HashmalHammers()
    {
        using var f = new Fixture(14);
        var source = f.Activate<GenericAOEs>("Hammerfall");
        for (ulong i = 0; i < 3; ++i)
        {
            var actor = f.Add(30 + i, 0x1EB17F, f.Center + new Vector3(5.123f * i, 0, 3.257f));
            f.Sync();
            CompareMetalFoxBits(f, source, "Hammerfall", "CE44 hammer Created " + i);
        }
        f.Remove(30);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "Hammerfall", "CE44 no destroy or timeout");
        for (var i = 0; i < 4; ++i)
        {
            source.OnCastFinished(f.W.Actors.Find(2)!, new() { Action = new(ActionType.Spell, 23825) });
            f.M.Finish(f.M.Actors[2], new(23825, 0, f.Center, 0, f.Now, f.Now, false, false));
            CompareMetalFoxBits(f, source, "Hammerfall", "CE44 global FIFO " + i);
        }
    }

    private static void HashmalCold()
    {
        using var f = new Fixture(14);
        f.Add(30, 0x1EB17F, f.Center + Vector3.UnitZ);
        f.Add(31, 0x1EB17E, f.Center);
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now;
        var source = f.Activate<GenericAOEs>("Hammerfall");
        var cold = new BozjaEncounter(f.M.Definition);
        cold.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
        var aoe = source.ActiveAOEs(0, f.Viewer).ToArray().Single();
        Check(cold.Hammers.Single().Activation == aoe.Activation && cold.ControlTowers.Count == 0,
            "CE44 cold current callback clock not birth history");
    }

    private static void HashmalFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE44-full-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[14]);
        var center = model.Center;
        var actions = new uint[] { 23826, 23827, 23828, 23823, 23830, 23818, 23821, 23819 };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x2DD2, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true, Targetable = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
            new() { Id = 40, Oid = 0x1EB17F, Kind = 7, Position = center + Vector3.UnitZ * 25, Hp = 1000 },
        };
        for (var i = 0; i < actions.Length; ++i)
            actors.Add(new() { Id = (ulong)(10 + i), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center, Hp = 1000, Cast = new(actions[i], 100, center, .4f, START,
                    START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true } });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var seconds in new[] { 0d, 1d, 100d })
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            model.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, now);
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2DD2);
            Check(module.Components.Count() == 9 && model.Active, "CE44 actual source nine cold components");
            foreach (var component in module.Components.OfType<GenericAOEs>())
            {
                var expected = component.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                var actual = model.Visible().Where(a => a.Cast.Rule.Component == component.GetType().Name).ToArray();
                Check(expected.Length == actual.Length && expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky))
                    .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky))),
                    "CE44 cold actual Feed AOE " + component.GetType().Name);
            }
            var ai = new AIHints();
            foreach (var component in module.Components)
                component.AddAIHints(0, session.World.Actors.Find(100)!, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(model.Actors[100]);
            Check(ai.ForbiddenZones.Count == zones.Length, "CE44 full Feed normalized count");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, "CE44 full normalized clock " + i);
                var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, 29.5f, out _);
                var actual = zones[i].Convert(center, 29.5f).ToArray();
                Check(expected.Count == actual.Length, "CE44 full normalized shape count " + i);
                for (var x = -29f; x <= 29; x += 2.31f)
                    for (var z = -29f; z <= 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == actual.Any(s => s.Contains(p, 0)), "CE44 true Feed converted navigation " + i);
                    }
            }
            Check(model.ControlTowers.Count == 0 && model.Hammers.Count == 1,
                "CE44 cold current tower cast no fabricated hole current Hammer exactly once");
            var damage = model.DamageForHost(model.Actors[100]);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation))
                .SequenceEqual(damage.Select(d => (d.Mask, d.Activation))), "CE44 actual Normalize damage order");
        }
    }

    private static void HashmalDraw()
    {
        using var f = new Fixture(14);
        var source = f.Activate<GenericAOEs>("ControlTowerAppear");
        TowerFinish(f, source, 23830, f.Center + Vector3.UnitX * 8, f.Center + Vector3.UnitX * 8);
        f.Source.Arena.Bounds.ScreenHalfSize = 200;
        foreach (var color in new[] { Colors.Border, Colors.Enemy })
        {
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink))
                f.Source.Arena.AddComplexPolygon(f.Source.Arena.Bounds.ShapeSimplified, color, 2f, false);
            var command = sink.Commands.OfType<MechanicDrawComplexPoly>().Single();
            Check(command.Parts.Single().Holes.Length == 1, "CE44 source capture includes true hole");
            var shapes = new List<MechanicPreviewShape>();
            var lines = new List<MechanicPreviewLine>();
            var extras = new List<MechanicRadarExtra>();
            MechanicDrawConvert.Convert(sink.Commands, 0, shapes, lines, extras);
            Check(lines.Count == 0 && extras.Count == 0 && shapes.Count == (color == Colors.Border ? 0 : 1),
                "CE44 old bridge skips white border and red draws outer only");
            if (shapes.Count != 0)
                Check(shapes[0].OutlineOnly && shapes[0].Shape.Contains(f.Center + Vector3.UnitX * 8, 0),
                    "CE44 red exterior outline contains tower hole");
        }
    }
}
