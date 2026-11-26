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
    private static void RunDal4(Action<string, Action> run)
    {
        run("Bozja native DAL4 client sixteen activated components exported polygon no DeathRay", Dal4Client);
        foreach (var rule in BozjaCatalog.Definitions[32].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
            run("Bozja native DAL4 standard " + rule.Component, () => Dal4Standard(rule));
        run("Bozja native DAL4 Arena map restore retains pending donut no cold history", Dal4Arena);
        run("Bozja native DAL4 Ruinous original gate cache sequence partial failures FIFO risky promotion", Dal4Ruinous);
        run("Bozja native DAL4 Nox original actor direction cached five effects no Finish no TTL", Dal4Nox);
        run("Bozja native DAL4 Aetheroplasm original orb destroyed effect intersection raw convert", Dal4Orbs);
        run("Bozja native DAL4 knockback true polygon circle wall raw unsafe complete cache immunity", Dal4Knockback);
        run("Bozja native DAL4 icon rectangle line original targets real effects raw navigation", Dal4Baits);
        run("Bozja native DAL4 Bomb old slots cold Pet dead hint query no inferred movement", Dal4Bomb);
        run("Bozja native DAL4 true Feed sixteen cold components normalized events output", Dal4Feed);
        run("Bozja native DAL4 true Feed Ruinous cold unload hot old retry new single side effects", Dal4Failures);
    }

    private static void Dal4Client()
    {
        using var f = new Fixture(32);
        Check(f.M.Definition.Rules.Length == 16 && f.M.ActiveRules.Count() == 16, "DAL4 sixteen");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(10007)?.Singular.ToString() == f.M.Definition.Name, "DAL4 name");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.TheDalriada.DAL4DiabloArmament.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DAL4 AID " + action);
        Check(!f.M.Definition.Rules.Any(r => r.Actions.Contains(23749u)), "DAL4 inactive DeathRay");
        var wall = new CuchulainnGeometry(f.M.Arena);
        for (var x = -35f; x <= 35; x += 2.137f)
            for (var z = -35f; z <= 35; z += 2.719f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DAL4 source polygon InBounds");
                for (var i = 0; i < 64; ++i)
                {
                    var d = new Angle(i * MathF.Tau / 64).ToDirection();
                    var expected = f.Source.Arena.Bounds.IntersectRay(new(p.X - f.Center.X, p.Z - f.Center.Z), d);
                    Check(expected == wall.WallDistance(p, new(d.X, 0, d.Z)), $"DAL4 polygon wall {p}/{i}");
                }
            }
        foreach (var contour in f.M.Arena.GetContours())
            foreach (var v in contour)
                foreach (var x in new[] { MathF.BitDecrement(v.X), v.X, MathF.BitIncrement(v.X) })
                    for (var i = 0; i < 64; ++i)
                    {
                        var d = new Angle(i * MathF.Tau / 64).ToDirection();
                        var p = v with { X = x };
                        var expected = f.Source.Arena.Bounds.IntersectRay(new(p.X - f.Center.X, p.Z - f.Center.Z), d);
                        Check(expected == wall.WallDistance(p, new(d.X, 0, d.Z)), "DAL4 wall vertex ULP");
                    }
    }

    private static void Dal4Standard(BozjaRule rule)
    {
        using var f = new Fixture(32);
        var source = f.Activate<GenericAOEs>(rule.Component);
        var spells = new List<ActorCastInfo>();
        for (var i = 0; i < (rule.GroupedFinish ? 12 : 2); ++i)
        {
            spells.Add(RobotStart(f, source, rule.Actions[i % rule.Actions.Length],
                rotation: .37f + i % 3, total: 3 + i % 5));
            Dal2Compare(f, source, rule.Component);
        }
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(rule.Actions[0]));
        f.M.Effect(2, rule.Actions[0]);
        Dal2Compare(f, source, rule.Component);
        foreach (var spell in spells)
        {
            if (rule.GroupedFinish) spell.Action = new(ActionType.Spell, 99999);
            RobotFinish(f, source, spell);
            Dal2Compare(f, source, rule.Component);
        }
    }

    private static void Dal4Arena()
    {
        using var f = new Fixture(32);
        var source = f.Activate<GenericAOEs>("ArenaChange");
        RobotStart(f, source, 23711);
        Dal2Compare(f, source, "ArenaChange");
        source.OnMapEffect(0x33, 0x00080004); f.M.MapEffect(0x33, 0x00080004);
        Dal2Compare(f, source, "ArenaChange");
        Check(f.M.Visible().Count() == 1, "DAL4 restore keeps AOE");
        source.OnMapEffect(0x33, 0x00020001); f.M.MapEffect(0x33, 0x00020001);
        Check(!f.M.Visible().Any() && f.M.Arena is MasterPlayer.Spatial.Geometry.CircleGeometry { Radius: 17 }, "DAL4 shrink clears");
        RobotStart(f, source, 23711);
        source.OnMapEffect(0x33, 0x00080004); f.M.MapEffect(0x33, 0x00080004);
        Dal2Compare(f, source, "ArenaChange");
    }

    private static void Dal4Gates(Fixture f)
    {
        (uint Oid, Vector3 Offset, float Rotation)[] gates =
        [
            (0x1EB1D8, new(0, 0, -20), 0), (0x1EB1D8, new(10, 0, 0), 2),
            (0x1EB1D7, new(0, 0, 10), 1), (0x1EB1D7, new(-10, 0, 0), MathF.PI / 2),
            (0x1EB1D6, new(20, 0, 20), 3), (0x1EB1D6, new(-20, 0, -20), 4),
            (0x1EB1D9, new(20, 0, -20), 5), (0x1EB1D9, new(-20, 0, 20), 6),
        ];
        for (var i = 0; i < gates.Length; ++i)
        {
            var (oid, offset, rotation) = gates[i];
            f.Add((ulong)(20 + i), oid, f.Center + offset).PosRot.W = rotation;
            f.Set((ulong)(20 + i), a => a with { Rotation = rotation });
        }
        f.Sync();
    }

    private static void Dal4Ruinous()
    {
        using var f = new Fixture(32);
        Dal4Gates(f);
        var source = f.Activate<GenericAOEs>("RuinousPseudoomen");
        var spell = RobotStart(f, source, 23712, id: 1, total: 15);
        void Compare(string step)
        {
            var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == "RuinousPseudoomen").ToArray();
            Console.WriteLine("DAL4 Ruinous " + step + " source=" + string.Join(";", expected.Select(a => $"{a.Origin}/{a.Rotation.Rad:R}/{a.Activation:O}/{a.Risky}/{a.Color}"))
                + " native=" + string.Join(";", actual.Select(a => $"{a.Cast.Origin}/{a.Cast.Rotation:R}/{a.Cast.Activation:O}/{a.Risky}/{a.Danger}")));
            Dal2Compare(f, source, "RuinousPseudoomen");
        }
        Check(f.M.DiabloRuinous.Count == 3, "DAL4 three chained predictions");
        Compare("start");
        // An unrelated caster's Finish consumes globally; Effect never does.
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(24995)); f.M.Effect(2, 24995);
        var finish = new ActorCastInfo { Action = new(ActionType.Spell, 24995) };
        void Finish()
        {
            source.OnCastFinished(f.W.Actors.Find(2)!, finish);
            f.M.Finish(f.M.Actors[2], new(24995, 0, f.Center, 0, f.Now, f.Now, false, false));
        }
        Finish();
        Compare("pop1");
        Finish();
        Compare("pop2");
        Check(!f.M.DiabloRuinous[0].Risky, "DAL4 remaining single retains false risk");
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Compare("overdue");
        Check(!f.M.DiabloRuinous[0].Risky, "DAL4 single overclock doesn't promote");
        Finish();
        RobotStart(f, source, 24911);
        Compare("standalone");
        var bad = new ActorCastInfo { Action = new(ActionType.Spell, 23712), TotalTime = 15, Location = f.Center };
        f.W.Actors.Find(1)!.CastInfo = bad;
        var expectedFailure = false;
        try { source.OnCastStarted(f.W.Actors.Find(1)!, bad); } catch (ArgumentOutOfRangeException) { expectedFailure = true; }
        f.M.Primary!.Cast = new(23712, 0, f.Center, 0, f.Now, f.Now.AddSeconds(15.3f), false, false) { TotalTime = 15 };
        f.M.Primary.Elapsed = 0;
        var nativeFailure = false;
        try { f.M.Start(f.M.Primary!); } catch (ArgumentOutOfRangeException) { nativeFailure = true; }
        Check(expectedFailure && nativeFailure && f.M.SequenceError != null, "DAL4 depleted original cache must throw");
        Compare("partialFailure");
    }

    private static void Dal4Nox()
    {
        using var f = new Fixture(32);
        var source = f.Activate<Exaflare>("AdvancedNox");
        f.W.Actors.Find(2)!.PosRot.W = .79f; f.Set(2, a => a with { Rotation = .79f });
        f.Sync();
        var spell = RobotStart(f, source, 23744, rotation: -.38f);
        Check(source.ActiveAOEs(0, f.Viewer).Length == 0 && !f.M.Visible().Any(), "DAL4 start doesn't publish before Update");
        void Compare()
        {
            source.Update(); f.M.Update(f.Now);
            var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible().Where(a => a.Cast.Rule.Component == "AdvancedNox").ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Activation, a.Color == Colors.Danger, a.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Activation, a.Danger, a.Risky))), "DAL4 Nox cache");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "AdvancedNox").ToArray();
            Check(ai.ForbiddenZones.Count == zones.Length, "DAL4 Nox navigation count");
        }
        Compare();
        RobotFinish(f, source, spell); Compare();
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); Compare();
        f.M.Effect(1, 23745); source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23745)); Compare();
        Check(f.M.ExaflareErrors == 0 && f.M.Exaflares[0].Left == 5, "DAL4 unmatched Effect no diagnostic");
        while (source.Lines.Count != 0)
        {
            var p = source.Lines[0].Next.ToVec3();
            f.Move(1, p);
            var before = source.ActiveAOEs(0, f.Viewer).ToArray();
            var nativeBefore = f.M.Visible().Select(a => a.Cast.Origin).ToArray();
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23745)); f.M.Effect(1, 23745);
            Check(before.Select(a => a.Origin).SequenceEqual(source.ActiveAOEs(0, f.Viewer).ToArray().Select(a => a.Origin))
                && nativeBefore.SequenceEqual(f.M.Visible().Select(a => a.Cast.Origin)), "DAL4 Effect consumes another caster but cache stays until Update");
            Compare();
        }
    }

    private static void Dal4Orbs()
    {
        using var f = new Fixture(32);
        f.Add(8, 0x31B5, f.Center + new Vector3(2.1234f, 0, 5.8765f));
        f.Sync();
        var source = f.Activate<BossComponent>("Aetheroplasm");
        void Compare()
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Aetheroplasm").ToArray();
            Check(ai.ForbiddenZones.Count == zones.Length, "DAL4 orb one intersection");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, "DAL4 orb dynamic clock");
                var raw = ai.ForbiddenZones[i].Item1;
                var converted = MechanicHintsConvert.ConvertForbiddenZone(raw, 0, f.Center, f.M.SampleRadius, out _);
                var actual = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
                Check(converted.Count == actual.Length, "DAL4 orb actual sampler");
                for (var x = -29f; x < 29; x += .371f)
                    for (var z = -29f; z < 29; z += .719f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(raw.Distance(new(p.X, p.Z)) == zones[i].Distance(p.X, p.Z), "DAL4 orb exact raw distance");
                    }
            }
            var hints = new BossComponent.GlobalHints(); source.AddGlobalHints(hints);
            Check(hints.SequenceEqual(f.M.GlobalHints()), "DAL4 orb global");
        }
        Compare();
        var original = f.M.DiabloOrbs.Single();
        f.Remove(8); Compare();
        f.Add(8, 0x31B5, f.Center - Vector3.UnitX * 4); f.Sync(); Compare();
        Check(f.M.DiabloOrbs.Count == 2 && ReferenceEquals(original, f.M.DiabloOrbs[0]), "DAL4 destroyed old plus new generation");
        source.OnEventCast(f.W.Actors.Find(8)!, PatriotEvent(23733)); f.M.Effect(8, 23733); Compare();
        Check(f.M.DiabloOrbs.Count == 1 && ReferenceEquals(original, f.M.DiabloOrbs[0]), "DAL4 Effect only new original reference");
    }

    private static void Dal4Knockback()
    {
        using var f = new Fixture(32);
        var nox = f.Activate<Exaflare>("AdvancedNox");
        var cannon = f.Activate<GenericAOEs>("AssaultCannon");
        var source = f.Activate<GenericKnockback>("DeadlyDealingKB");
        f.Set(100, a => a with { HitboxRadius = .5f }); f.Sync();
        RobotStart(f, source, 23747, id: 1);
        void Compare()
        {
            Check(source.CalculateMovements(0, f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3()))
                .SequenceEqual(f.M.DiabloMovements(f.NativeViewer).Select(m => (m.From, m.To))), "DAL4 exact KB movements");
            for (var x = -35f; x < 35; x += .731f)
                for (var z = -35f; z < 35; z += 1.137f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(source.DestinationUnsafe(0, f.Viewer, new(p.X, p.Z)) == f.M.DiabloDestinationUnsafe(p),
                        $"DAL4 unsafe full published Nox cannon {p} source={source.DestinationUnsafe(0, f.Viewer, new(p.X, p.Z))} native={f.M.DiabloDestinationUnsafe(p)} sourceArena={f.Source.Arena.Bounds.GetType().Name}");
                }
            Check(!f.M.Navigation(f.NativeViewer).Any(z => z.Component == "DeadlyDealingKB"), "DAL4 KB no invented AI");
        }
        foreach (var p in new[] { new Vector3(3, 0, 2), new Vector3(-17.25f, 0, -4), new Vector3(29, 0, 0) })
        { f.Move(100, f.Center + p); Compare(); }
        RobotStart(f, cannon, 23726, rotation: .91f);
        RobotStart(f, nox, 23744, id: 1); nox.Update(); f.M.Update(f.Now); Compare();
        f.Source.Arena.Bounds = BossMod.Shadowbringers.Foray.TheDalriada.DAL4DiabloArmament.DAL4DiabloArmament.SmallArena;
        f.Source.Arena.Center = f.Source.Arena.Center.Quantized();
        f.M.MapEffect(0x33, 0x20001); Compare();
    }

    private static void Dal4Baits()
    {
        using var f = new Fixture(32);
        var bait = f.Activate<BossComponent>("PillarOfShamashBait");
        var line = f.Activate<BossComponent>("PillarOfShamashStack");
        bait.OnEventIcon(f.Viewer, 23, 100); f.M.Icon(100, 23, 100);
        line.OnEventCast(f.W.Actors.Find(1)!, new(new(ActionType.Spell, 23741), 100, 0, 0, default, 0, 0, default)); f.M.Effect(1, 23741, 100);
        void Compare()
        {
            var ai = new AIHints(); var hints = new BossComponent.TextHints();
            foreach (var c in f.Source.Components) { c.AddAIHints(0, f.Viewer, default, ai); c.AddHints(0, f.Viewer, hints); }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), "DAL4 bait line hints");
            var zones = f.M.NavigationForHost(f.NativeViewer);
            Check(ai.ForbiddenZones.Count == zones.Length, "DAL4 bait line nav count");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, "DAL4 bait line clock");
                for (var x = -29f; x < 29; x += .731f)
                    for (var z = -29f; z < 29; z += 1.137f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z)) == zones[i].Distance(p.X, p.Z), "DAL4 bait line raw SDF");
                    }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.DamageForHost(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), "DAL4 bait line damage");
        }
        Compare();
        f.Move(100, f.Center + Vector3.UnitZ * 12); Compare();
        f.Remove(100);
        f.Add(100, 0, f.Center + Vector3.UnitX * 5, ActorType.Player); f.Sync(); Compare();
        bait.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23740)); f.M.Effect(2, 23740);
        line.OnEventCast(f.W.Actors.Find(2)!, new(new(ActionType.Spell, 23742), 101, 0, 0, default, 0, 0, default)); f.M.Effect(2, 23742, 101); Compare();
    }

    private static void Dal4Bomb()
    {
        using var f = new Fixture(32);
        var source = f.Activate<StayMove>("AccelerationBomb");
        var status = new ActorStatus(2657, 0, START.AddSeconds(10), 1);
        source.OnStatusGain(f.Viewer, ref status);
        f.Set(100, a => a with { Statuses = [new(2657, 0, 1, status.ExpireAt) { SlotIndex = 59 }] }); f.Sync();
        Check(f.M.DiabloBombs[0] == source.PlayerStates[0].Activation, "DAL4 bomb slot59");
        foreach (var t in new[] { 0d, 7.4999999, 7.5, 11 })
        {
            f.Now = START.AddSeconds(t); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            foreach (var target in new ulong[] { 0, 1 })
            {
                f.Viewer.TargetID = target; f.Set(100, a => a with { TargetId = target }); f.Sync();
                var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), "DAL4 bomb exact hint");
            }
        }
        f.Viewer.TargetID = 0;
        f.Viewer.CastInfo = new() { Action = new(ActionType.Ability, 1234), TotalTime = 8 };
        f.Set(100, a => a with { TargetId = 0, Cast = new(1234, 0, f.Center, 0, f.Now, f.Now.AddSeconds(8.3f), false, false) { IsSpell = false, TotalTime = 8 } });
        f.Sync();
        var abilityHints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, abilityHints);
        Check(abilityHints.Single().Item2 && f.M.Hints(f.NativeViewer).Single().Risky, "DAL4 raw player Ability cast risky even without target");
        f.Viewer.CastInfo = null; f.Set(100, a => a with { Cast = null }); f.Sync();
        f.Viewer.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        var deadHints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, deadHints);
        Check(deadHints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky)))
            && f.M.DiabloBombs[0] == default, "DAL4 dead clears after hint query");
    }

    private static void Dal4Feed() => Dal4FeedScenario(false, false);
    private static void Dal4Failures()
    {
        Dal4FeedScenario(true, false);
        Dal4FeedScenario(true, true);
        Dal4FeedScenario(true, false, true);
        Dal4FeedScenario(true, false, restore: true);
        Dal4FeedScenario(true, false, correctElapsed: true);
        Dal4FeedScenario(true, false, crossModule: true);
    }

    private static void Dal4FeedScenario(bool failure, bool fresh, bool cold = false,
        bool restore = false, bool correctElapsed = false, bool crossModule = false)
    {
        Initialize();
        using var session = new MechanicSession("DAL4-real-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.IsDiablo);
        var center = model.Center;
        ReplayCast Spell(uint action, double at = 0)
            => new(action, 100, center, .37f, START.AddSeconds(at), START.AddSeconds(at + 8 + .3f), false, false) { TotalTime = 8, IsSpell = true };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31B3, Name = "Diablo", Kind = 2, SubKind = 5, Position = center,
                Targetable = true, InCombat = true, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center + Vector3.UnitX * 3, Hp = 1000, HitboxRadius = .5f },
            new() { Id = 101, Kind = 1, Position = center - Vector3.UnitX * 10, Hp = 1000, HitboxRadius = .5f },
            new() { Id = 102, Kind = 2, SubKind = 2, Position = center, Hp = 1000,
                Statuses = [new(2657, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 }] },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101), new(2, "", 102)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        (bool Source, bool Native) Frame(double at, uint effect = 0, byte map = 0, uint state = 0)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            var expectedFailure = false;
            try
            {
                feed.ApplyFrame(new(975, 778, 100, 0,
                    party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(1, 1, effect, 100, 0, center, 0, 0, 0, []));
                if (map != 0) feed.Enqueue(new MechanicEventMapEffect(map, state));
                feed.Flush(); session.Update();
            }
            catch (ArgumentOutOfRangeException) { expectedFailure = true; }
            var nativeFailure = false;
            try
            {
                region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
                {
                    if (effect != 0) region.Effect(1, effect, 100, center);
                    if (map != 0) region.MapEffect(map, state);
                });
            }
            catch (ArgumentOutOfRangeException) { nativeFailure = true; }
            Check(expectedFailure == nativeFailure, $"DAL4 Feed exception parity {at} source={expectedFailure} native={nativeFailure}");
            if (expectedFailure)
            {
                var failedSource = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31B3);
                var expected = failedSource.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "RuinousPseudoomen")
                    .ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                var actual = model.Visible(model.Actors[100]).Where(a => a.Cast.Rule.Component == "RuinousPseudoomen").ToArray();
                Check(expected.Select(a => (a.Origin.ToVec3(), a.Activation, a.Risky, a.Color == Colors.Danger))
                    .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Activation, a.Risky, a.Danger))), "DAL4 failed callback preserves exact partial AOE");
                Check(session.World.Actors.Find(90) == null && !model.Actors.ContainsKey(90), "DAL4 shared callback failure blocks later Actor");
                return (true, true);
            }
            var source = session.BossModules.LoadedModules.SingleOrDefault(m => m.PrimaryActor.OID == 0x31B3);
            if (source == null) { Check(model.Primary == null, "DAL4 cold failed unload"); return (false, false); }
            Check(source.Components.Select(c => c.GetType().Name).SequenceEqual(model.ActiveRules.Select(r => r.Component)), "DAL4 Feed sixteen activation order");
            var viewer = session.World.Actors.Find(100)!;
            foreach (var c in source.Components.OfType<GenericAOEs>())
            {
                var expected = c.ActiveAOEs(0, viewer).ToArray();
                var actual = model.Visible(model.Actors[100]).Where(a => a.Cast.Rule.Component == c.GetType().Name).ToArray();
                Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky, a.Color == Colors.Danger))
                    .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky, a.Danger))), "DAL4 Feed AOE " + c.GetType().Name);
            }
            var hints = new BossComponent.TextHints(); var ai = new AIHints();
            foreach (var c in source.Components) { c.AddHints(0, viewer, hints); c.AddAIHints(0, viewer, default, ai); }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(model.Hints(model.Actors[100]).Select(h => (h.Text, h.Risky))), "DAL4 Feed hints");
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints()), "DAL4 Feed global");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(model.Actors[100]).Select(d => (d.Mask, d.Activation, d.Type))), "DAL4 Feed damage");
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(model.NavigationForHost(model.Actors[100]).Select(z => z.Activation)), "DAL4 Feed normalized clocks");
            var bomb = source.Components.OfType<StayMove>().Single();
            Check(bomb.PlayerStates.Select(s => s.Activation).SequenceEqual(model.DiabloBombs), "DAL4 Feed exact old Party Bomb slots and Pet history");
            return (false, false);
        }
        if (failure)
        {
            if (cold)
            {
                actors[0] = actors[0] with { Cast = Spell(23712) };
                Frame(0);
                Check(!model.Active && model.Completed && model.Primary == null && model.DiabloRuinous.Count == 1,
                    "DAL4 cold failure unloads original boss after first AOE");
                Check(model.DiabloSequenceError != null && model.SequenceError == null, "DAL4 cold unload diagnostic doesn't block other modules");
                actors.Add(new() { Id = 90, Oid = 0x31B5, Kind = 2, Position = center, Hp = 1000 });
                Frame(1);
                Check(!model.Active && model.Primary == null && model.DiabloOrbs.Count == 0, "DAL4 cold failed later components never activate");
                return;
            }
            if (fresh)
            {
                Frame(0);
                actors.Add(new() { Id = 80, Oid = 0x233C, Kind = 2, Position = center, Cast = Spell(23712, 1), Hp = 1000 });
            }
            else
            {
                if (restore) actors[0] = actors[0] with { Cast = Spell(24911) };
                Frame(0);
            }
            if (crossModule)
            {
                var frostCenter = new Vector3(-830, 0, 190);
                actors[1] = actors[1] with { Position = frostCenter };
                actors.Add(new() { Id = 39, Oid = 0x319C, Kind = 2, SubKind = 5, Position = frostCenter,
                    Targetable = true, InCombat = true, Hp = 1000 });
                actors.Add(new() { Id = 40, Oid = 0x319F, Kind = 2, SubKind = 5, Position = frostCenter,
                    Cast = Spell(23589, .5), Hp = 1000 });
                Frame(.5);
                var frost = region.Encounters.Single(m => m.Definition.PrimaryOid == 0x319C);
                Check(frost.Active && frost.ThermalGusts.Count == 1 && region.Loaded.IndexOf(model) < region.Loaded.IndexOf(frost),
                    "DAL4 real cross module subscriber order and one Thermal queue");
                actors[^1] = actors[^1] with { Cast = Spell(23712, 1) };
                actors.Add(new() { Id = 90, Oid = 0x31B5, Kind = 2, Position = center, Hp = 1000 });
                Check(Frame(1).Source && frost.ThermalGusts.Count == 0 && frost.Actors[40].Cast!.ActionId == 23712,
                    "DAL4 first Start failure installs physical cast in later subscriber");
                Check(Frame(2).Source && frost.ThermalSequenceError == null && model.DiabloRuinous.Count == 2,
                    "DAL4 repeated Start failure never repeats consumed Thermal Finish");
                actors[^2] = actors[^2] with { Cast = Spell(23589, .5) };
                Check(!Frame(3).Source && frost.Actors[40].Cast!.ActionId == session.World.Actors.Find(40)!.CastInfo!.Action.ID
                    && frost.Actors[40].Cast!.ActionId == 23712, "DAL4 restored committed input keeps physical failed cast in every subscriber");
                actors[^2] = actors[^2] with { Cast = null };
                Check(!Frame(4).Source && frost.ThermalSequenceError == null && frost.Actors[40].Cast == null,
                    "DAL4 clear after restored input finishes physical cast once without Thermal double consumption");
                return;
            }
            var previous = actors[0].Cast;
            if (!fresh) actors[0] = actors[0] with { Cast = Spell(23712, 1) };
            actors.Add(new() { Id = 90, Oid = 0x31B5, Kind = 2, Position = center, Hp = 1000 });
            Check(Frame(1).Source, "DAL4 first hot failure");
            Check(!model.Actors.ContainsKey(90) && model.DiabloRuinous.Count == 1, "DAL4 failure blocks later Created");
            if (restore)
            {
                actors[0] = actors[0] with { Cast = previous };
                Check(!Frame(2).Source && model.DiabloRuinous.Count == 1 && model.Actors[1].Cast!.ActionId == 23712,
                    "DAL4 returning to committed cast emits no callback and retains failed physical cast");
                return;
            }
            if (correctElapsed)
            {
                actors[0] = actors[0] with { Cast = Spell(23712, 1.5) };
                Check(Frame(2).Source && model.DiabloRuinous.Count == 2, "DAL4 retry uses corrected snapshot elapsed exactly");
                return;
            }
            var repeat = Frame(1);
            Check(repeat.Source == !fresh && model.DiabloRuinous.Count == (fresh ? 1 : 2), "DAL4 old repeat versus new no repeat");
            return;
        }
        foreach (var action in new uint[] { 23711, 24911, 23732, 23744, 23726, 23747, 23746, 23718, 23730, 23737, 23715, 23728 })
            actors.Add(new() { Id = action, Oid = 0x233C, Kind = 2, Position = center, Cast = Spell(action), Hp = 1000 });
        actors.Add(new() { Id = 80, Oid = 0x31B5, Kind = 2, Position = center + Vector3.UnitZ * 10, Hp = 1000 });
        Frame(0); Frame(.1); Frame(1, map: 0x33, state: 0x20001);
        Frame(2, 23744); Frame(3, map: 0x33, state: 0x80004);
        for (var i = 0; i < actors.Count; ++i) actors[i] = actors[i] with { Cast = null };
        Frame(10); Frame(100);
    }
}
