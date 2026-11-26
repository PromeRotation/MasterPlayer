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
    private static void RunGauntlet(Action<string, Action> run)
    {
        run("Bozja pending Gauntlet client manifest and true Feed three phase component contract", GauntletSource);
        run("Bozja pending Gauntlet real Feed initial arena icon FIFO minority blades raw Finish", GauntletFirst);
        run("Bozja pending Gauntlet real Feed ten blade status cold order original caster weakpoint", GauntletUnseen);
        run("Bozja pending Gauntlet real Feed tower nine seconds donut raw target missing Effect clears", GauntletSecond);
        run("Bozja pending Gauntlet real Feed Turbine boolean navigation zero origin movement fallback", GauntletTurbine);
        run("Bozja pending Gauntlet real Feed Stormcall tether clocks two item swap North priorities real Effect", GauntletThird);
        foreach (var rule in BozjaCatalog.Gauntlet.Rules.Where(r => r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Raidwide or BozjaRuleKind.Enrage))
            run("Bozja pending Gauntlet real Feed standard " + rule.Component, () => GauntletStandard(rule));
        run("Bozja pending Gauntlet real Feed status before ten Created retains first selection and Lose semantics", GauntletEarlyStatus);
        run("Bozja pending Gauntlet real Feed Donut original target generation dead Update no inferred cold icon", GauntletDonutReferences);
        run("Bozja pending Gauntlet real Feed Turbine immunity first only AI next valid movement strict clock", GauntletImmunity);
        run("Bozja pending Gauntlet real Feed original Augur destroyed replacement doesn't rebind phase", GauntletBossGeneration);
        run("Bozja pending Gauntlet real Feed missing cached bosses transitions through all phases in same Update", GauntletMissingBosses);
        run("Bozja pending Gauntlet real Feed tied ten blades eleventh destroy retains original minority", GauntletTiedBlades);
        run("Bozja pending Gauntlet real Feed custom arena exact vertices ULP raw distance and finite conversion", GauntletArenaEdges);
        run("Bozja pending Gauntlet real Feed reduced arena sample radius retains source offcenter Turbine navigation", GauntletSampleRadius);
        foreach (var action in new uint[] { 24362, 24363, 24364, 24365, 24366, 24367 })
            run("Bozja pending Gauntlet real Feed North Storm Pain mixed immune priority " + action, () => GauntletPain(action));
    }

    private sealed class GauntletFixture : IDisposable
    {
        internal readonly MechanicSession Session;
        private readonly MechanicWorldFeed feed;
        internal readonly BozjaEncounter Native = new(BozjaCatalog.Gauntlet);
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly Vector3 Center = new(222, 0, -689);
        internal DateTime Now = START;
        internal BossModule Source => Session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x32BF);
        internal Actor Viewer => Session.World.Actors.Find(100)!;
        internal BozjaActor NativeViewer => Native.Actors[100];
        private readonly MethodInfo converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;

        internal GauntletFixture()
        {
            Initialize();
            Session = new("Gauntlet-source-differential");
            feed = new(Session);
            Add(1, 0x32BF);
            Change(1, a => a with { Targetable = true, InCombat = true });
            Add(100, 0);
            Add(101, 0, Center - Vector3.UnitX * 15);
            Add(10, 0x32C8);
            Add(11, 0x32CB);
            Add(12, 0x32CA);
        }

        internal void Add(ulong id, uint oid, Vector3? position = null, float rotation = 0)
            => Actors.Add(new() { Id = id, Oid = oid, Kind = oid == 0 ? (byte)1 : (byte)2, SubKind = oid == 0 ? (byte)0 : (byte)5,
                Position = position ?? Center, Rotation = rotation, Hp = 1000, HitboxRadius = .5f });

        internal void Change(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }

        internal ReplayCast Spell(uint action, double at, Vector3? location = null, float rotation = 0, float total = 5)
            => new(action, 100, location ?? Center, rotation, START.AddSeconds(at), START.AddSeconds(at + total + .3f), false, false)
            { IsSpell = true, TotalTime = total };

        internal void Frame(double at, MechanicFeedEvent? ev = null, Action? nativeEvent = null, bool compare = true)
        {
            Now = START.AddSeconds(at);
            Session.BeginFrame(Now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                Actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, Now])!).ToArray()));
            if (ev != null) feed.Enqueue(ev);
            feed.Flush();
            Session.Update();
            Native.Synchronize(Actors, [new(0, "", 100), new(1, "", 101)], 100, Now, update: false);
            nativeEvent?.Invoke();
            Native.Update(Now);
            if (compare) Compare();
        }

        internal void Effect(double at, uint action, ulong source = 20, ulong target = 100)
            => Frame(at, new MechanicEventActionEffect(source, 1, action, target, 0, Center, 0, 0, 0, []),
                () => Native.Effect(source, action, target, Center));

        internal void Icon(double at, uint icon, ulong target = 100)
            => Frame(at, new MechanicEventIcon(icon, target, target), () => Native.Icon(target, icon, target));

        internal void Phase(int phase)
        {
            Frame(0);
            if (phase >= 1) { Change(10, a => a with { Targetable = true }); Frame(.1); }
            if (phase >= 2) { Change(11, a => a with { Targetable = true }); Frame(.2); }
            Check(Native.GauntletPhase == phase, "Gauntlet requested phase really active");
        }

        internal void Compare()
        {
            Check(Source.StateMachine.ActivePhaseIndex == Native.GauntletPhase
                && Source.Components.Select(c => c.GetType().Name).SequenceEqual(Native.ActiveRules.Select(r => r.Component)), "Gauntlet component phase parity");
            foreach (var component in Source.Components.OfType<GenericAOEs>())
            {
                var key = component.GetType().Name;
                var expected = component.ActiveAOEs(0, Viewer).ToArray();
                var actual = Native.Visible(NativeViewer).Where(a => a.Cast.Rule.Component == key).ToArray();
                Check(expected.Length == actual.Length, $"Gauntlet {key} AOE count source={expected.Length} native={actual.Length}");
                for (var i = 0; i < expected.Length; ++i)
                {
                    var e = expected[i];
                    var a = actual[i];
                    Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation
                        && e.Activation == a.Cast.Activation && e.Risky == a.Risky && (e.Color == Colors.Danger) == a.Danger,
                        $"Gauntlet {key} AOE fields index={i}");
                    for (var x = -33f; x <= 33; x += 2.137f)
                        for (var z = -33f; z <= 33; z += 3.719f)
                        {
                            var p = Center + new Vector3(x, 0, z);
                            Check(e.Check(new(p.X, p.Z)) == a.Cast.Contains(p), $"Gauntlet {key} Check {p}");
                        }
                }
            }
            var hints = new BossComponent.TextHints();
            var ai = new AIHints();
            foreach (var c in Source.Components) { c.AddHints(0, Viewer, hints); c.AddAIHints(0, Viewer, default, ai); }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(Native.Hints(NativeViewer).Select(h => (h.Text, h.Risky))),
                "Gauntlet ordered text hints");
            Check(Source.CalculateGlobalHints().SequenceEqual(Native.GlobalHints()), "Gauntlet global hints");
            var zones = Native.NavigationForHost(NativeViewer);
            Check(Source.Arena.Bounds.Radius == Native.SampleRadius, "Gauntlet source arena current sampling radius");
            Check(ai.ForbiddenZones.Count == zones.Length,
                $"Gauntlet zones source={ai.ForbiddenZones.Count} native={zones.Length}");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, $"Gauntlet zone clock {i}");
                for (var x = -33f; x < 33; x += 1.371f)
                    for (var z = -33f; z < 33; z += 2.719f)
                    {
                        var p = Center + new Vector3(x, 0, z);
                        var e = ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z));
                        var a = zones[i].Distance(p.X, p.Z);
                        Check(e == a, $"Gauntlet zone distance {i} {zones[i].Component} point={p} source={e:R} native={a:R}");
                    }
                if (zones[i].Component != "ArenaChange")
                {
                    var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, Center, Source.Arena.Bounds.Radius, out _);
                    var actual = zones[i].Convert(Center, Native.SampleRadius).ToArray();
                    Check(expected.Count == actual.Length, $"Gauntlet actual host conversion count {zones[i].Component}");
                    for (var x = -29f; x <= 29; x += 3.731f)
                        for (var z = -29f; z <= 29; z += 4.917f)
                        {
                            var p = Center + new Vector3(x, 0, z);
                            Check(expected.Any(g => g.Template.Resolve(g.Position, g.Rotation, null).Contains(p, 0))
                                == actual.Any(g => g.Contains(p, 0)), $"Gauntlet actual host converted geometry {zones[i].Component}");
                        }
                }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(Native.DamageForHost(NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), "Gauntlet damage no invented donut prediction");
            foreach (var component in Source.Components.OfType<GenericKnockback>())
            {
                var rule = Native.ActiveRules.Single(r => r.Component == component.GetType().Name);
                Check(component.CalculateMovements(0, Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3()))
                    .SequenceEqual(Native.GauntletMovements(NativeViewer, rule).Select(m => (m.From, m.To))), "Gauntlet exact movement " + rule.Component);
            }
            foreach (var gaze in Source.Components.OfType<GenericGaze>())
                Check(gaze.ActiveEyes(0, Viewer).ToArray().Select(e => (e.Position.ToVec3(), e.Activation))
                    .SequenceEqual(Native.GauntletEyes(NativeViewer).Select(e => (e.Position, e.Activation))),
                    "Gauntlet raw eye position and original dynamic NPC clock");
            var donut = Source.Components.OfType<DonutStack>().SingleOrDefault();
            if (donut != null)
            {
                var actual = Native.GauntletDonuts.ToArray();
                Check(donut.Stacks.Count == actual.Length, "Gauntlet donut raw drawing queue");
                for (var i = 0; i < actual.Length; ++i)
                {
                    var expected = donut.Stacks[i];
                    Check(expected.Target.Position.ToVec3() == actual[i].Origin && expected.Activation == actual[i].Activation,
                        "Gauntlet donut raw original target position and clock");
                    for (var x = -10f; x <= 10; x += .371f)
                        for (var z = -10f; z <= 10; z += .719f)
                        {
                            var point = actual[i].Origin + new Vector3(x, 0, z);
                            Check(donut.Donut.Check(new(point.X, point.Z), expected.Target.Position, default)
                                == actual[i].Contains(point), "Gauntlet donut exact displayed 4 to 8 ring");
                        }
                }
            }
        }

        public void Dispose() => Session.Dispose();
    }

    private static void GauntletFirst()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C);
        f.Change(20, a => a with { Cast = f.Spell(24339, 0) });
        for (var i = 0; i < 10; ++i)
            f.Add((ulong)(30 + i), 0x32C1, f.Center + new Vector3(i - 5, 0, -10), i < 6 ? 0 : 1);
        f.Frame(0);
        Check(f.Native.GauntletTerminusCasters.Count == 10 && f.Native.GauntletTerminus.Count == 4, "Gauntlet ten choose four minority");
        f.Icon(.1, 261); f.Icon(.2, 261); f.Icon(.3, 261);
        f.Effect(1, 24334);
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.Visible().Any(a => a.Cast.Rule.Kind == BozjaRuleKind.GauntletArena), "Gauntlet arena Finish retains");
        f.Frame(3, new MechanicEventMapEffect(0x31, 0x20001), () => f.Native.MapEffect(0x31, 0x20001));
        f.Change(20, a => a with { Cast = f.Spell(24327, 4) }); f.Frame(4);
        f.Change(20, a => a with { Cast = null }); f.Frame(5);
        Check(f.Native.GauntletTerminus.Count == 0 && f.Native.GauntletTerminusCasters.Count == 10, "Gauntlet Terminus Finish preserves original list");
    }

    private static void GauntletUnseen()
    {
        using var f = new GauntletFixture();
        for (var i = 0; i < 10; ++i)
            f.Add((ulong)(30 + i), 0x32C1, f.Center - Vector3.UnitZ * (10 + i), i < 6 ? 0 : MathF.PI / 2);
        f.Change(100, a => a with { Statuses = [new(1709, 0, 1, START.AddSeconds(40)) { SlotIndex = 59 }] });
        f.Frame(0);
        Check(f.Native.GauntletEyes(f.NativeViewer).Single().Activation == default, "Gauntlet no-cast fallback selection no invented activation");
        f.Change(30, a => a with { Cast = f.Spell(24327, 1, rotation: 0) }); f.Frame(1);
        var source = f.Source.Components.OfType<GenericGaze>().Single();
        Check(source.ActiveEyes(0, f.Viewer).ToArray().Select(e => (e.Position.ToVec3(), e.Activation))
            .SequenceEqual(f.Native.GauntletEyes(f.NativeViewer).Select(e => (e.Position, e.Activation))), "Gauntlet raw eye chosen by remaining not fallback activation");
        f.Frame(1.5);
        f.Change(100, a => a with { Statuses = [] }); f.Frame(2);
        Check(!f.Native.GauntletEyes(f.NativeViewer).Any(), "Gauntlet Lose clears direction");
    }

    private static void GauntletSecond()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C);
        f.Add(40, 0x1EB213, f.Center + Vector3.UnitX * 6);
        f.Phase(1);
        Check(f.Native.Towers.Single().Activation == START.AddSeconds(.1).AddSeconds(9d), "Gauntlet cold tower exact +9");
        f.Icon(1, 288); f.Icon(2, 288, 101);
        Check(!f.Native.Damage(f.NativeViewer).Any(d => d.Type == "Shared"), "Gauntlet Donut doesn't inherit base AI damage");
        f.Effect(3, 24343, target: 999);
        Check(!f.Native.GauntletDonuts.Any(), "Gauntlet missing Effect target clears all");
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 6 }); f.Effect(4, 24348);
        Check(f.Native.Towers.Length == 0, "Gauntlet tower exact caster position first match");
    }

    private static void GauntletTurbine()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C); f.Add(21, 0x233C, f.Center - Vector3.UnitX * 5);
        f.Add(22, 0x32C7, f.Center + Vector3.UnitZ * 10);
        f.Phase(1);
        f.Change(20, a => a with { Cast = f.Spell(24341, 1) });
        f.Change(21, a => a with { Cast = f.Spell(24341, 1, f.Center - Vector3.UnitX * 5) });
        f.Change(22, a => a with { Cast = f.Spell(24345, 1, f.Center + Vector3.UnitZ * 10) });
        f.Frame(1);
        f.Change(100, a => a with { Position = f.Center + new Vector3(3.17f, 0, 2.71f) }); f.Frame(2);
        f.Change(20, a => a with { Cast = null }); f.Frame(3);
    }

    private static void GauntletThird()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C); f.Add(21, 0x32CC, f.Center - Vector3.UnitX * 20);
        f.Add(22, 0x32CE, f.Center + Vector3.UnitX * 20);
        f.Add(23, 0x32CE);
        f.Phase(2);
        f.Frame(1, new MechanicEventTether(21, 4, 11), () => f.Native.Tether(21, 4, 11));
        f.Frame(2, new MechanicEventTether(22, 4, 11), () => f.Native.Tether(22, 4, 11));
        f.Change(20, a => a with { Cast = f.Spell(24355, 3) }); f.Frame(3);
        f.Change(23, a => a with { Cast = f.Spell(24363, 4, rotation: .73f) }); f.Frame(4);
        f.Change(20, a => a with { Cast = null }); f.Frame(5);
        Check(f.Native.Casts.Any(c => c.Rule.Kind == BozjaRuleKind.GauntletWind), "Gauntlet ordinary Wind Finish retains");
        f.Effect(6, 24814);
        Check(f.Native.Casts.All(c => c.Rule.Kind != BozjaRuleKind.GauntletWind), "Gauntlet actual NorthWind clears");
        f.Change(23, a => a with { Cast = f.Spell(99999, 7) }); f.Frame(7);
        Check(f.Native.Casts.All(c => c.Rule.Kind != BozjaRuleKind.GauntletPain), "Gauntlet any cast Finish consumes caster first");
    }

    private static void GauntletStandard(BozjaRule rule)
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C);
        var phase = Array.IndexOf(BozjaCatalog.Gauntlet.Rules, rule) switch { < 6 => 0, < 11 => 1, _ => 2 };
        f.Phase(phase);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], 1, f.Center + Vector3.UnitZ * 3, .73f) });
        f.Frame(1);
        Check(f.Native.Casts.Count(c => c.Rule == rule) == 1, "Gauntlet standard actual watched caster");
        f.Effect(2, rule.Actions[0]);
        Check(f.Native.Casts.Count(c => c.Rule == rule) == 1, "Gauntlet standard Effect retains");
        f.Frame(100);
        Check(f.Native.Casts.Count(c => c.Rule == rule) == 1, "Gauntlet standard no TTL");
        f.Change(20, a => a with { Cast = null }); f.Frame(101);
        Check(f.Native.Casts.All(c => c.Rule != rule), "Gauntlet standard matching Finish");
    }

    private static void GauntletEarlyStatus()
    {
        using var f = new GauntletFixture();
        f.Frame(0);
        f.Add(30, 0x32C1, f.Center - Vector3.UnitZ * 10);
        f.Frame(.5);
        f.Change(100, a => a with { Statuses = [new(1708, 0, 1, START.AddSeconds(30)) { SlotIndex = 58 }] });
        f.Frame(1);
        Check(f.Native.GauntletEyes(f.NativeViewer).Any(), "Gauntlet early selection initially exists");
        var original = f.Native.GauntletEyes(f.NativeViewer).Single();
        for (var i = 1; i < 10; ++i)
            f.Add((ulong)(30 + i), 0x32C1, f.Center - Vector3.UnitZ * (10 + i), MathF.PI / 2);
        f.Frame(2);
        Check(f.Native.GauntletEyes(f.NativeViewer).Any(), "Gauntlet early selection survives ten Created");
        Check(f.Native.GauntletCommonAngle == MathF.PI / 2 && f.Native.GauntletEyes(f.NativeViewer).Single().Position == original.Position,
            "Gauntlet no rescan after late majority changes CommonAngle");
        f.Change(100, a => a with { Statuses = [
            new(1708, 0, 1, START.AddSeconds(30)) { SlotIndex = 58 },
            new(1709, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 }] }); f.Frame(3);
        Check(f.Native.GauntletEyes(f.NativeViewer).Any(), "Gauntlet later direction still selects original");
        Check(f.Native.GauntletEyes(f.NativeViewer).Single().Forward == MathF.PI, "Gauntlet later status overrides direction");
        f.Change(100, a => a with { Statuses = [new(1709, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 }] }); f.Frame(4);
        Check(!f.Native.GauntletEyes(f.NativeViewer).Any(), "Gauntlet Lose clears without counting retained other slot");
    }

    private static void GauntletDonutReferences()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C);
        f.Change(20, a => a with { Cast = f.Spell(24343, 0) });
        f.Phase(1);
        Check(!f.Native.GauntletDonuts.Any(), "Gauntlet cold cast doesn't invent icon stack");
        f.Icon(1, 288, 101); f.Icon(1.1, 288, 101);
        var original = f.Native.GauntletDonuts.First().Target!;
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Frame(2);
        f.Add(101, 0); f.Frame(2.1);
        Check(f.Native.GauntletDonuts.All(c => ReferenceEquals(c.Target, original)), "Gauntlet destroyed original target retained");
        f.Icon(3, 288, 101);
        Check(f.Native.GauntletDonuts.Count() == 3 && !ReferenceEquals(f.Native.GauntletDonuts.Last().Target, original),
            "Gauntlet same ID new icon target distinct original generation");
        f.Effect(4, 24343, target: 101);
        Check(f.Native.GauntletDonuts.Count() == 2 && ReferenceEquals(f.Native.GauntletDonuts.First().Target, original),
            "Gauntlet MainTargetID removes first duplicate across generations");
        f.Change(101, a => a with { Dead = true, Hp = 0 }); f.Frame(5);
        Check(f.Native.GauntletDonuts.Count() == 1 && ReferenceEquals(f.Native.GauntletDonuts.Single().Target, original),
            "Gauntlet dead new generation removed only Update while original survives");
    }

    private static void GauntletImmunity()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C, f.Center - Vector3.UnitX * 5);
        f.Add(21, 0x233C, f.Center - Vector3.UnitX * 10);
        f.Phase(1);
        f.Change(20, a => a with { Cast = f.Spell(24341, 1, f.Center - Vector3.UnitX * 5, total: .7f) });
        f.Change(21, a => a with { Cast = f.Spell(24341, 1, f.Center - Vector3.UnitX * 10, total: 8) });
        f.Change(100, a => a with { Statuses = [new(3054, 0, 1, START.AddSeconds(6)) { SlotIndex = 59 }] });
        f.Frame(1);
        var rule = f.Native.ActiveRules.Single(r => r.Kind == BozjaRuleKind.GauntletTurbine);
        Check(f.Native.GauntletMovements(f.NativeViewer, rule).Single().Cast.Source.Actor.Id == 21,
            "Gauntlet first immune skips to second movement while AI stays first and silent");
        var first = f.Native.Casts.First(c => c.Rule == rule).Activation;
        f.Change(100, a => a with { Statuses = [] }); f.Frame(1.5);
        f.Change(100, a => a with { Statuses = [new(3054, 0, 1, first) { SlotIndex = 59 }] });
        f.Frame(2);
        Check(f.Native.GauntletMovements(f.NativeViewer, rule).First().Cast.Source.Actor.Id == 20,
            "Gauntlet exact immune expiration doesn't count as immune");
    }

    private static void GauntletBossGeneration()
    {
        using var f = new GauntletFixture();
        f.Phase(1);
        var original = f.Native.GauntletAugur!;
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Frame(1);
        Check(f.Native.GauntletPhase == 2 && ReferenceEquals(f.Native.GauntletAugur, original) && original.Destroyed,
            "Gauntlet original Augur Destroy moves phase");
        f.Add(10, 0x32C8);
        f.Change(10, a => a with { Targetable = true });
        f.Frame(2);
        Check(ReferenceEquals(f.Native.GauntletAugur, original) && !ReferenceEquals(original, f.Native.Actors[10]),
            "Gauntlet same ID replacement never rebinds cached original");
    }

    private static void GauntletMissingBosses()
    {
        using var f = new GauntletFixture();
        f.Actors.RemoveAll(a => a.Id is 10 or 11 or 12);
        f.Frame(0);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Frame(1, compare: false);
        Check(f.Native.Completed && !f.Native.Active && f.Native.Primary == null
            && f.Session.BossModules.LoadedModules.All(m => m.PrimaryActor.OID != 0x32BF),
            "Gauntlet while loop reaches completion before post-update GetActor");
    }

    private static void GauntletTiedBlades()
    {
        using var f = new GauntletFixture();
        for (var i = 0; i < 10; ++i)
            f.Add((ulong)(30 + i), 0x32C1, f.Center - Vector3.UnitZ * (10 + i), i < 5 ? 0 : 1);
        f.Frame(0);
        Check(f.Native.GauntletCommonAngle == 1 && f.Native.GauntletTerminus.Count == 5
            && f.Native.GauntletTerminus.All(c => c.Rotation == 0), "Gauntlet tie selects B common A minority");
        f.Add(40, 0x32C1);
        f.Frame(1);
        Check(f.Native.GauntletTerminusCasters.Count == 11 && f.Native.GauntletTerminus.Count == 5, "Gauntlet eleventh doesn't regenerate");
        f.Actors.RemoveAll(a => a.Id == 30);
        f.Frame(2);
        Check(f.Native.GauntletTerminusCasters.Count == 11 && f.Native.GauntletTerminus.Count == 5
            && f.Native.GauntletTerminus[0].Source.Destroyed, "Gauntlet Destroy doesn't erase original prediction");
    }

    private static void GauntletArenaEdges()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C);
        f.Change(20, a => a with { Cast = f.Spell(24339, 0) });
        f.Frame(0);
        var expected = f.Source.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "ArenaChange")
            .ActiveAOEs(0, f.Viewer).ToArray().Single();
        var native = f.Native.Visible().Single(a => a.Cast.Rule.Kind == BozjaRuleKind.GauntletArena).Cast;
        var shape = native.Shape.Resolve(native.Origin, native.Rotation, null);
        foreach (var contour in shape.GetContours())
            foreach (var vertex in contour)
                foreach (var x in new[] { MathF.BitDecrement(vertex.X), vertex.X, MathF.BitIncrement(vertex.X) })
                    foreach (var z in new[] { MathF.BitDecrement(vertex.Z), vertex.Z, MathF.BitIncrement(vertex.Z) })
                    {
                        var point = new Vector3(x, 0, z);
                        Check(expected.Check(new(x, z)) == native.Contains(point), "Gauntlet custom arena vertex ULP Check");
                        var zone = f.Native.NavigationForHost(f.NativeViewer).Single();
                        Check(expected.Shape.Distance(expected.Origin, expected.Rotation).Distance(new(x, z)) == zone.Distance(x, z),
                            "Gauntlet custom arena vertex ULP raw Distance");
                    }
        var converted = f.Native.NavigationForHost(f.NativeViewer).Single().Convert(f.Center, f.Native.SampleRadius).ToArray();
        Check(converted.Length != 0 && converted.All(g => g.GetContours().All(c => c.All(p => float.IsFinite(p.X) && float.IsFinite(p.Z)))),
            "Gauntlet exact arena finite production navigation geometry");
        var ai = new AIHints();
        f.Source.Components.Single(c => c.GetType().Name == "ArenaChange").AddAIHints(0, f.Viewer, default, ai);
        var old = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones.Single().Item1, 0, f.Center, f.Native.SampleRadius, out _);
        Check(old.Any(g => g.Template.Resolve(g.Position, g.Rotation, null).Contains(f.Center, 0))
            && !expected.Check(new(f.Center.X, f.Center.Z)) && !converted.Any(g => g.Contains(f.Center, 0)),
            "Gauntlet native preserves actual source safe hole lost by old exterior-only bridge conversion");
    }

    private static void GauntletSampleRadius()
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C);
        f.Frame(0);
        f.Frame(.1, new MechanicEventMapEffect(0x31, 0x20001), () => f.Native.MapEffect(0x31, 0x20001));
        f.Change(10, a => a with { Targetable = true }); f.Frame(.2);
        f.Change(20, a => a with { Cast = f.Spell(24341, 1, f.Center + new Vector3(20, 0, 20)) }); f.Frame(1);
        Check(f.Native.SampleRadius == 23, "Gauntlet actual shrunk source23 rather than initial29.5");
        var ai = new AIHints();
        f.Source.Components.Single(c => c.GetType().Name == "Turbine").AddAIHints(0, f.Viewer, default, ai);
        var old = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones.Single().Item1, 0, f.Center, f.Source.Arena.Bounds.Radius, out _);
        var actual = f.Native.NavigationForHost(f.NativeViewer).Single().Convert(f.Center, f.Native.SampleRadius).ToArray();
        Check(old.Count > 0 && actual.Length == old.Count, "Gauntlet shrunk radius source and native sampler retain real geometry");
        for (var x = -23f; x <= 23; x += .731f)
            for (var z = -23f; z <= 23; z += .917f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(old.Any(g => g.Template.Resolve(g.Position, g.Rotation, null).Contains(p, 0)) == actual.Any(g => g.Contains(p, 0)),
                    "Gauntlet actual old bridge and native shrunk sampler geometry");
            }
    }

    private static void GauntletPain(uint action)
    {
        using var f = new GauntletFixture();
        f.Add(20, 0x233C); f.Add(21, 0x32CC, f.Center - Vector3.UnitZ * 20); f.Add(22, 0x32CE);
        f.Phase(2);
        f.Frame(1, new MechanicEventTether(21, 4, 11), () => f.Native.Tether(21, 4, 11));
        f.Change(20, a => a with { Cast = f.Spell(24355, 2, rotation: .37f) });
        f.Change(22, a => a with { Cast = f.Spell(action, 2, rotation: .73f) });
        f.Frame(2);
        Check(f.Native.NavigationForHost(f.NativeViewer).All(z => z.Component == "NorthSouthwind"),
            "Gauntlet nonimmune Wind suppresses ordinary Storm and Pain navigation");
        f.Change(100, a => a with { Statuses = [new(3054, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 }] });
        f.Frame(3);
        Check(!f.Native.GauntletMovements(f.NativeViewer, f.Native.ActiveRules.Single(r => r.Kind == BozjaRuleKind.GauntletWind)).Any()
            && f.Native.NavigationForHost(f.NativeViewer).All(z => z.Component != "NorthSouthwind")
            && f.Native.NavigationForHost(f.NativeViewer).Length == 2, "Gauntlet immune restores both ordinary AI zones");
        f.Change(100, a => a with { Statuses = [] }); f.Frame(4);
        f.Change(22, a => a with { Cast = null }); f.Frame(5);
        Check(f.Native.NavigationForHost(f.NativeViewer).Single().Component == "NorthSouthwind", "Gauntlet no Pain uses Storm fallback");
    }

    private static void GauntletSource()
    {
        Initialize();
        var name = Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(10212)!.Value.Singular.ToString();
        Console.WriteLine("Gauntlet actual NameID10212=" + name);
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.TheDalriada.DAL1Gauntlet.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "Gauntlet actual AID " + action);
        using var session = new MechanicSession("Gauntlet-real-phases");
        var feed = new MechanicWorldFeed(session);
        var native = new BozjaEncounter(BozjaCatalog.Gauntlet);
        var center = new Vector3(222, 0, -689);
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x32BF, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true, Targetable = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 10, Oid = 0x32C8, Kind = 2, SubKind = 5, Position = center, Hp = 1000 },
            new() { Id = 11, Oid = 0x32CB, Kind = 2, SubKind = 5, Position = center, Hp = 1000 },
            new() { Id = 12, Oid = 0x32CA, Kind = 2, SubKind = 5, Position = center, Hp = 1000 },
        };
        void Frame(double at)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush();
            session.Update();
            native.Synchronize(actors, [new(0, "", 100)], 100, now);
        }
        BossModule Module() => session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x32BF);
        void Components(string[] expected, int phase)
        {
            Check(Module().StateMachine.ActivePhaseIndex == phase
                && Module().Components.Select(c => c.GetType().Name).SequenceEqual(expected), "Gauntlet source exact phase " + phase);
            Check(native.Active && native.GauntletPhase == phase && native.ActiveRules.Select(r => r.Component).SequenceEqual(expected),
                "Gauntlet native exact phase " + phase);
        }
        Frame(0);
        Components(["ArenaChange", "BallisticImpact", "SurfaceMissile", "TerminusEst", "TerminusEstUnseen", "CeruleumExplosion"], 0);
        actors[2] = actors[2] with { Targetable = true };
        Frame(1);
        Components(["Pyroclysm", "SeventyFourDegrees", "FlamingCyclone", "Turbine", "SanctifiedQuakeIII"], 1);
        actors[3] = actors[3] with { Targetable = true };
        Frame(2);
        Components(["Stormcall", "NorthSouthwind", "NihilitysSong", "PainStormFrigidPulsePainfulGust", "BroadsideBarrage"], 2);
        actors[3] = actors[3] with { Dead = true, Hp = 0 };
        actors[4] = actors[4] with { Dead = true, Hp = 0 };
        Frame(3);
        Check(!native.Active && native.Completed && native.Primary == null && !native.ActiveRules.Any(), "Gauntlet native both final original actors finish module");
        Check(session.BossModules.LoadedModules.All(m => m.PrimaryActor.OID != 0x32BF), "Gauntlet source both final actors finish module");
    }
}
