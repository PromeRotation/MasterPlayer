using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using Cll1Source = BossMod.Shadowbringers.Foray.CastrumLacusLitore.CLL1Brionac4thLegionHelldiver;

internal static partial class BozjaRegression
{
    private static void RunCll1(Action<string, Action> run)
    {
        run("Bozja pending CLL1 client manifest all source actions and real rectangles", Cll1Manifest);
        run("Bozja pending CLL1 constructor draw side effect old navigation new drawing", Cll1Drawing);
        foreach (var rule in BozjaCll1Catalog.Definition.Rules.Where(r => r.Kind != BozjaRuleKind.Cll1Custom))
            run("Bozja pending CLL1 real Feed standard side gate " + rule.Component, () => Cll1Standard(rule));
        run("Bozja pending CLL1 chain actual effects seven no Finish timeout or source identity", Cll1Chain);
        run("Bozja pending CLL1 orb early Created late original actor moved swapped repeated clocks", Cll1Orbs);
        run("Bozja pending CLL1 magnets icon tether order original targets swapped positions ignore immunity", Cll1Magnets);
        run("Bozja pending CLL1 wildcharges FIFO independent caster union hint intersection navigation", Cll1Wild);
        run("Bozja pending CLL1 infrared first sides persistent players duplicate untether effect retains fire", Cll1Infrared);
        run("Bozja pending CLL1 actual manager bottom reference HP0 both bosses skyarmor complete", Cll1Lifecycle);
        run("Bozja pending CLL1 FalseThunder strict one second prefix ninety-nine sorting", Cll1Window);
        run("Bozja pending CLL1 cold components Created Spell Tether status no invented icons", Cll1Cold);
        run("Bozja pending CLL1 source priority diagnostics strict ten percent original bottom reference", Cll1Priorities);
        run("Bozja pending CLL1 lazy tunnel dictionary reuse and cold zero-target Infrared clock", Cll1LazyAndZero);
        run("Bozja pending CLL1 primary absent frame same ID module rebind original bottom cache", Cll1Rebind);
    }

    private static void Cll1Manifest()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9436)?.Singular.ToString() == BozjaCll1Catalog.Definition.Name,
            "CLL1 actual client primary name");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(735)?.TerritoryType.RowId == 920
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(920)?.ContentFinderCondition.RowId == 735, "CLL1 CFC territory");
        var ids = Enum.GetValues<Cll1Source.AID>();
        Check(ids.Length == 46, "CLL1 source action enumeration remains 46");
        foreach (var action in ids)
        {
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null, "CLL1 action row " + action);
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CLL1 full action extra " + action);
        }
        foreach (var center in new[] { BozjaCll1Catalog.BottomCenter, BozjaCll1Catalog.TopCenter })
        {
            f.Draw(center);
            for (var x = 48f; x < 114; x += .713f)
                for (var z = -247f; z < -151; z += .791f)
                    Check(f.Source!.Arena.InBounds(new WPos(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "CLL1 exact rectangle");
        }
    }

    private sealed class Cll1Fixture : IDisposable
    {
        internal readonly MechanicSession Session;
        private readonly MechanicWorldFeed feed;
        internal readonly BozjaRegion Region = new();
        internal BozjaEncounter Native => Region.Encounters.Single(e => e.IsCll1);
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly List<ReplayPartyMember> Party = [];
        internal BossModule? Source => Session.BossModules.LoadedModules.SingleOrDefault(m => m.PrimaryActor.OID == 0x2ECC);
        internal Actor Viewer => Session.World.Actors.Find(100)!;
        internal BozjaActor NativeViewer => Native.Actors[100];
        private readonly MethodInfo convert = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;

        internal Cll1Fixture(bool combat = true)
        {
            Initialize();
            Session = new("CLL1-source-differential");
            feed = new(Session);
            Actors.Add(Actor(1, 0x2ECC, BozjaCll1Catalog.TopCenter) with { InCombat = combat });
            Actors.Add(Actor(2, 0x2ED3, BozjaCll1Catalog.BottomCenter));
            Actors.Add(Actor(20, 0x233C, BozjaCll1Catalog.TopCenter));
            Actors.Add(Actor(3, 0x2ED7, BozjaCll1Catalog.BottomCenter) with { HitboxRadius = 7.8f });
            for (var i = 0; i < 8; ++i)
            {
                Actors.Add(Actor((ulong)(100 + i), 0, BozjaCll1Catalog.BottomCenter) with { Kind = 1 });
                Party.Add(new(i, "", (ulong)(100 + i)));
            }
        }

        internal static ReplayActorState Actor(ulong id, uint oid, Vector3 position)
            => new() { Id = id, Oid = oid, Kind = 2, SubKind = 5, Position = position,
                Targetable = true, Hp = 1000, MaxHp = 1000 };

        internal void Change(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }

        internal ReplayCast Spell(uint action, double at = 0, ulong target = 100, float total = 5, Vector3? point = null)
            => new(action, target, point ?? BozjaCll1Catalog.TopCenter, .4f, START.AddSeconds(at),
                START.AddSeconds(at + total + .3f), false, false) { IsSpell = true, TotalTime = total };

        internal void Frame(double at, MechanicFeedEvent? ev = null, Action? native = null, bool compare = true)
        {
            var now = START.AddSeconds(at);
            Session.BeginFrame(now);
            feed.ApplyFrame(new(920, 735, 100, 0,
                Party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                Actors.Select((a, i) =>
                {
                    var snapshot = (MechanicActorSnapshot)convert.Invoke(null, [a, i, now])!;
                    return snapshot with { Cast = snapshot.Cast == null ? null : snapshot.Cast with { IsSpell = a.Cast?.IsSpell != false } };
                }).ToArray()));
            if (ev != null) feed.Enqueue(ev);
            feed.Flush(); Session.Update();
            Region.Synchronize(Actors.ToArray(), Party.ToArray(), 100, now, true, native);
            if (compare) Compare();
        }

        internal void Draw(Vector3 position)
        {
            Change(100, a => a with { Position = position });
            Frame((Native.Now - START).TotalSeconds);
            Source!.Components.Single(c => c.GetType().Name == "DetermineArena").DrawArenaBackground(0, Viewer);
            Native.ObserveCll1Drawing(NativeViewer);
            Check(Source.Arena.Center.ToVec3() == Native.Center, "CLL1 draw arena center");
            Compare();
        }

        internal void Packet(double at, MechanicFeedEvent ev, Action native) => Frame(at, ev, native);
        internal void Tether(double at, ulong source, uint id, ulong target)
            => Packet(at, new MechanicEventTether(source, id, target), () => Native.Tether(source, id, target));
        internal void Icon(double at, ulong source, uint icon, ulong target)
            => Packet(at, new MechanicEventIcon(icon, target, source), () => Native.Icon(source, icon, target));

        internal void Compare()
        {
            var source = Source;
            Check(Native.Active == (source?.StateMachine.ActiveState != null), "CLL1 phase lifecycle");
            if (source == null) return;
            Check(source.Arena.Center.ToVec3() == Native.Center && source.Arena.Bounds.Radius == Native.SampleRadius,
                $"CLL1 arena center/radius {source.Arena.Bounds.Radius}/{Native.SampleRadius}");
            if (!Native.Active) return;
            Check(source.Components.Select(c => c.GetType().Name).SequenceEqual(new[] { "DetermineArena" }.Concat(Native.ActiveRules.Select(r => r.Component))),
                "CLL1 fourteen phase plus constructor components");
            foreach (var component in source.Components)
            {
                var name = component.GetType().Name;
                if (component is GenericAOEs aoes)
                {
                    var expected = aoes.ActiveAOEs(0, Viewer).ToArray();
                    var actual = Native.Visible(NativeViewer).Where(a => a.Cast.Rule.Component == name).ToArray();
                    Check(expected.Length == actual.Length, "CLL1 AOE count " + name);
                    for (var i = 0; i < expected.Length; ++i)
                    {
                        var e = expected[i]; var a = actual[i];
                        Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation
                            && e.Activation == a.Cast.Activation && e.Risky == a.Risky,
                            "CLL1 AOE exact fields " + name);
                        for (var x = 48f; x < 114; x += 3.713f)
                            for (var z = -244f; z < -150; z += 3.791f)
                                Check(e.Check(new(x, z)) == a.Cast.Contains(new(x, 0, z)), "CLL1 raw shape " + name);
                    }
                }
                var texts = new BossComponent.TextHints();
                var global = new BossComponent.GlobalHints();
                var ai = new AIHints();
                component.AddHints(0, Viewer, texts);
                component.AddGlobalHints(global);
                component.AddAIHints(0, Viewer, default, ai);
                var nativeRule = Native.ActiveRules.SingleOrDefault(r => r.Component == name);
                if (nativeRule == null) continue;
                var nav = Native.Navigation(NativeViewer).Where(z => z.Component == name).ToArray();
                Check(ai.ForbiddenZones.Count == nav.Length, "CLL1 raw navigation count " + name);
                for (var i = 0; i < nav.Length; ++i)
                {
                    Check(ai.ForbiddenZones[i].Item2 == nav[i].Activation, "CLL1 navigation clock " + name);
                    if (name is "Magnetism" or "WildCharges" or "InfraredBlast")
                    {
                        var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0,
                            Native.Center, Native.SampleRadius, out var kind);
                        var complement = kind == MechanicNavZoneKind.ComplementSkipped;
                        var expected = complement && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals)
                            ? goals : converted;
                        var actual = nav[i].Convert(Native.Center, Native.SampleRadius).ToArray();
                        Check(complement == nav[i].Goal && expected.Count == actual.Length, "CLL1 real Goal versus sampled Danger conversion " + name);
                        for (var x = 47f; x < 114; x += 1.713f)
                            for (var z = -245f; z < -150; z += 2.791f)
                            {
                                var p = new Vector3(x, 0, z);
                                Check(expected.Any(g => g.Template.Resolve(g.Position, g.Rotation, null).Contains(p, 0))
                                    == actual.Any(g => g.Contains(p, 0)), "CLL1 real converted geometry " + name);
                            }
                    }
                    if (nav[i].Shape is not BozjaCll1IntersectionTemplate && name != "Magnetism") continue;
                    for (var x = 47f; x < 114; x += 1.713f)
                        for (var z = -245f; z < -150; z += 2.791f)
                        {
                            var expected = ai.ForbiddenZones[i].Item1.Distance(new(x, z));
                            var actual = nav[i].Distance(x, z);
                            Check(expected == actual || float.IsNaN(expected) && float.IsNaN(actual),
                                $"CLL1 raw SDF {name} ({x},{z}) source={expected} native={actual}");
                        }
                }
            }
            var hints = new BossComponent.TextHints();
            var globals = new BossComponent.GlobalHints();
            var all = new AIHints();
            foreach (var c in source.Components)
            {
                c.AddHints(0, Viewer, hints);
                c.AddGlobalHints(globals);
                c.AddAIHints(0, Viewer, default, all);
            }
            all.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(Native.Hints(NativeViewer).Select(h => (h.Text, h.Risky))),
                "CLL1 ordered text hints");
            Check(globals.SequenceEqual(Native.GlobalHints()), "CLL1 ordered global hints");
            var damage = Native.DamageForHost(NativeViewer);
            Check(all.PredictedDamage.Count == damage.Length, "CLL1 damage count");
            for (var i = 0; i < damage.Length; ++i)
                Check(all.PredictedDamage[i].Players.Raw == damage[i].Mask && all.PredictedDamage[i].Activation == damage[i].Activation,
                    "CLL1 original caster dynamic damage clock and mask");
        }

        public void Dispose() => Session.Dispose();
    }

    private static void Cll1Drawing()
    {
        using var f = new Cll1Fixture(false);
        f.Frame(0);
        Check(f.Native.Cll1 is { Top: false } && !f.Native.Active, "CLL1 constructor bottom default");
        f.Change(100, a => a with { Position = BozjaCll1Catalog.TopCenter });
        f.Frame(1);
        Check(f.Native.Cll1 is { Top: false }, "CLL1 Update does not invent Draw transition");
        f.Draw(BozjaCll1Catalog.TopCenter);
        f.Draw(new(200, 0, -222));
        Check(f.Native.Cll1 is { Top: true }, "CLL1 outside both arenas keeps previous selection");
        f.Draw(BozjaCll1Catalog.BottomCenter);
        f.Change(2, a => a with { InCombat = true }); f.Frame(2);
        Check(!f.Native.Active && f.Native.Cll1!.HellDiver == null, "CLL1 bottom combat not sampled before active UpdateModule");
        f.Change(1, a => a with { InCombat = true }); f.Frame(3);
        f.Change(20, a => a with { Cast = f.Spell(20955, 4) }); f.Frame(4);
        Check(!f.Native.Navigation(f.NativeViewer).Any(), "CLL1 old bottom navigation hides top cast");
        f.Draw(BozjaCll1Catalog.TopCenter);
        Check(f.Native.Visible(f.NativeViewer).Any(), "CLL1 same drawing transition exposes top AOE");
    }

    private static void Cll1Standard(BozjaRule rule)
    {
        foreach (var cold in new[] { true, false })
        {
            using var f = new Cll1Fixture();
            if (!cold) f.Frame(0);
            var action = rule.Actions[0];
            f.Change(20, a => a with { Cast = f.Spell(action, 1) });
            f.Frame(1);
            f.Draw(BozjaCll1Catalog.TopCenter);
            f.Draw(BozjaCll1Catalog.BottomCenter);
            f.Frame(8);
            f.Change(20, a => a with { Cast = null }); f.Frame(9);
        }
    }

    private static void Cll1Chain()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        var origin = BozjaCll1Catalog.BottomCenter;
        f.Change(20, a => a with { Position = origin, Cast = f.Spell(20985, 1, point: origin) }); f.Frame(1);
        f.Change(20, a => a with { Cast = null }); f.Frame(20);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "ChainCannon") == 1, "CLL1 chain Finish and timeout retain original activation");
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, origin + new Vector3(.09f, 0, .09f)));
        for (var i = 0; i < 7; ++i)
            f.Packet(21 + i, new MechanicEventActionEffect(21, 1, i == 0 ? 20985u : 20986u, 0, 0, origin, 0, 0, 0, []),
                () => f.Native.Effect(21, 20986));
        Check(!f.Native.Casts.Any(c => c.Rule.Component == "ChainCannon"), "CLL1 seventh positional cross-source effect consumes first matching origin");
    }

    private static void Cll1Orbs()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2ECD, new(63.12345f, 0, -222.12345f)));
        f.Frame(1); f.Draw(BozjaCll1Catalog.TopCenter);
        Check(f.Native.Casts.Single(c => c.Rule.Component == "OrbsAOE").Activation == START.AddSeconds(12), "CLL1 early Created event clock");
        for (var i = 0; i < 3; ++i)
        {
            f.Change(20, a => a with { Cast = f.Spell(20944, 2 + i) }); f.Frame(2 + i);
            f.Change(20, a => a with { Cast = null }); f.Frame(2.1 + i);
        }
        f.Actors.Add(Cll1Fixture.Actor(31, 0x2ECE, new(97, 0, -222))); f.Frame(5);
        Check(f.Native.Cll1!.Orbs.Count == 1 && f.Native.Casts.Count(c => c.Rule.Component == "OrbsAOE") == 1, "CLL1 late orb stores original without immediate AOE");
        f.Change(31, a => a with { Position = new(93.12345f, 0, -220.54321f) });
        f.Change(20, a => a with { Cast = f.Spell(20947, 6) }); f.Frame(6);
        Check(f.Native.Casts.Last().Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate, "CLL1 shift swaps late circle to donut");
        f.Actors.RemoveAll(a => a.Id == 31); f.Frame(7);
        f.Change(20, a => a with { Cast = f.Spell(20949, 8) }); f.Frame(8);
        Check(f.Native.Cll1.Orbs.Single().Actor.Destroyed, "CLL1 destroyed original orb stays cached for future triggers");
        f.Change(20, a => a with { Cast = f.Spell(20945, 9) }); f.Frame(9);
        f.Change(20, a => a with { Cast = null }); f.Frame(10);
        Check(f.Native.Cll1.Orbs.Count == 0 && !f.Native.Casts.Any(c => c.Rule.Component == "OrbsAOE"), "CLL1 any caster actual burst Finish clears both lists");
    }

    private static void Cll1Magnets()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2ECD, new(63, 0, -222)));
        f.Actors.Add(Cll1Fixture.Actor(31, 0x2ECE, new(97, 0, -222))); f.Frame(1);
        f.Icon(2, 30, 162, 30); f.Icon(3, 20, 231, 100);
        f.Tether(4, 100, 124, 30);
        Check(f.Native.Cll1!.MagnetSources[0] == null, "CLL1 tether124 does not eagerly initialize after earlier icons");
        f.Icon(5, 30, 162, 30);
        Check(f.Native.Cll1.MagnetSources[0] is { Away: true, Activation: var time } && time == START.AddSeconds(13.2d),
            "CLL1 magnet clock belongs to initializing icon");
        f.Change(30, a => a with { Position = new(400, 0, 400) }); f.Frame(6);
        f.Tether(7, 30, 21, 31);
        Check(f.Native.Cll1.MagnetSources[0]!.Value.Origin.X == 97, "CLL1 PoleShift swaps captured creation positions");
        f.Icon(8, 20, 232, 100);
        Check(f.Native.Cll1.MagnetSources[0]!.Value.Away, "CLL1 initialized source not recomputed by later icon");
        f.Packet(9, new MechanicEventActionEffect(20, 1, 20950, 0, 0, BozjaCll1Catalog.TopCenter, 0, 0, 0, []), () => f.Native.Effect(20, 20950));
        Check(f.Native.Cll1.MagnetTethered == 0 && f.Native.Cll1.Poles.Count == 0, "CLL1 actual pull clears poles only when tethered");
    }

    private static void Cll1Wild()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        f.Change(20, a => a with { Position = new(55, 0, -179), Cast = f.Spell(20971, 1, point: new(105, 0, -179)) }); f.Frame(1);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, new(80, 0, -200)) with { Cast = f.Spell(20977, 2, point: new(80, 0, -150)) }); f.Frame(2);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text.StartsWith("Share damage") && !h.Risky), "CLL1 any safe charge satisfies share hint");
        Check(f.Native.Navigation(f.NativeViewer).Single(z => z.Component == "WildCharges").Activation == START.AddSeconds(6.3f),
            "CLL1 multi charge intersection keeps first activation");
        f.Change(21, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.Casts.Single(c => c.Rule.Component == "WildCharges").Source.Actor.Id == 21, "CLL1 finish from second caster removes global first");
        f.Draw(BozjaCll1Catalog.TopCenter);
    }

    private static void Cll1Infrared()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        f.Tether(1, 100, 121, 20);
        Check(f.Native.Cll1!.InfraredTethers[0].Player.Actor.Id == 20, "CLL1 first sides run before cached players exist");
        f.Tether(2, 100, 121, 20);
        Check(f.Native.Cll1.InfraredTethers.Count == 2 && f.Native.Cll1.InfraredTethers[1].Player.Actor.Id == 100,
            "CLL1 later sides use original cached player list");
        f.Tether(3, 100, 0, 0);
        Check(f.Native.Cll1.InfraredTethers.Count == 1, "CLL1 re-evaluated Untether cannot remove differently classified first tuple");
        f.Change(100, a => a with { Statuses = [new(1255, 1, 0, START.AddSeconds(40)) { SlotIndex = 59 }] }); f.Frame(4);
        Check(f.Native.Cll1.Fire == 1, "CLL1 sixty-slot fire gain");
        f.Packet(5, new MechanicEventActionEffect(20, 1, 20974, 0, 0, BozjaCll1Catalog.TopCenter, 0, 0, 0, []), () => f.Native.Effect(20, 20974));
        Check(f.Native.Cll1.InfraredActivation == default && f.Native.Cll1.InfraredTethers.Count == 1
            && f.Native.Cll1.Fire == 1, "CLL1 Effect resets activation only while tethers players fire remain");
        f.Draw(BozjaCll1Catalog.TopCenter);
        f.Draw(BozjaCll1Catalog.BottomCenter);
        f.Change(100, a => a with { Statuses = [] }); f.Frame(6);
    }

    private static void Cll1Lifecycle()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        var original = f.Native.Cll1!.HellDiver;
        f.Change(1, a => a with { Hp = 0 }); f.Frame(1);
        Check(f.Native.Active, "CLL1 HP0 does not finish");
        f.Actors.Add(Cll1Fixture.Actor(40, 0x2F4E, BozjaCll1Catalog.TopCenter)); f.Frame(2);
        f.Change(1, a => a with { Dead = true }); f.Change(2, a => a with { Dead = true }); f.Frame(3);
        Check(f.Native.Active, "CLL1 live SkyArmor prevents completion");
        f.Actors.RemoveAll(a => a.Id == 2); f.Actors.Add(Cll1Fixture.Actor(4, 0x2ED3, BozjaCll1Catalog.BottomCenter)); f.Frame(4);
        Check(ReferenceEquals(f.Native.Cll1.HellDiver, original) && original!.Destroyed, "CLL1 cached bottom primary never replaced by new list head");
        f.Change(40, a => a with { Dead = true }); f.Frame(5);
        Check(!f.Native.Active, "CLL1 both original bosses and all SkyArmor complete");
    }

    private static void Cll1Window()
    {
        using var f = new Cll1Fixture(); f.Frame(0); f.Draw(BozjaCll1Catalog.TopCenter);
        foreach (var (id, total) in new[] { (20ul, 7f), (21ul, 8f), (22ul, 6f) })
        {
            if (id != 20) f.Actors.Add(Cll1Fixture.Actor(id, 0x233C, BozjaCll1Catalog.TopCenter));
            f.Change(id, a => a with { Cast = f.Spell(20943, 1, total: total) });
        }
        f.Frame(1);
        Check(f.Native.Visible(f.NativeViewer).Count(a => a.Cast.Rule.Component == "FalseThunder") == 1,
            "CLL1 strict +1 cutoff stops at first failing item even later earlier clock");
        f.Change(20, a => a with { Cast = f.Spell(20944, 2) }); f.Frame(2);
        for (var i = 300; i < 400; ++i)
            f.Actors.Add(Cll1Fixture.Actor((ulong)i, 0x233C, BozjaCll1Catalog.TopCenter) with { Cast = f.Spell(20942, 3, total: 5 + i % 4) });
        f.Frame(3);
    }

    private static void Cll1Cold()
    {
        using var f = new Cll1Fixture(false);
        f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2ECD, new(63, 0, -222)));
        f.Change(100, a => a with
        {
            Tethers = [new(124, 30)],
            Statuses = [new(1255, 0, 20, START.AddSeconds(40)) { SlotIndex = 59 }],
        });
        f.Change(20, a => a with { Cast = f.Spell(20985, 1) });
        f.Frame(1);
        Check(f.Native.Casts.Count == 0, "CLL1 inactive only DetermineArena consumes no combat history");
        f.Change(1, a => a with { InCombat = true }); f.Frame(2);
        Check(f.Native.Cll1!.Fire == 1 && f.Native.Cll1.MagnetTethered == 1
            && f.Native.Cll1.MagnetSources.All(s => s == null), "CLL1 cold true status slots/tethers but no icon replay");
        f.Icon(3, 30, 162, 30); f.Icon(4, 20, 232, 100);
        Check(f.Native.Cll1.MagnetSources[0] == null, "CLL1 cold player tether before later orb Created is not backfilled");
        f.Tether(5, 100, 124, 30);
        f.Icon(6, 30, 162, 30);
        Check(f.Native.Cll1.MagnetSources[0] is { Away: false }, "CLL1 actual later tether and icon initialize pull");
        f.Change(100, a => a with
        {
            Statuses = [new(1255, 0, 20, START.AddSeconds(40)) { SlotIndex = 59 },
                new(1209, 0, 100, START.AddSeconds(40)) { SlotIndex = 58 }],
        });
        f.Frame(7);
        f.Draw(BozjaCll1Catalog.TopCenter);
        var knockback = f.Source!.Components.OfType<GenericKnockback>().Single();
        Check(knockback.IsImmune(0, knockback.ActiveKnockbacks(0, f.Viewer)[0].Activation), "CLL1 fixture really has active knockback immunity");
        var expected = knockback.CalculateMovements(0, f.Viewer);
        var actual = f.Native.Cll1MagnetMovements(f.NativeViewer).ToArray();
        Check(expected.Count == actual.Length && expected[0].from.ToVec3() == actual[0].From
            && expected[0].to.ToVec3() == actual[0].To, "CLL1 exact clamped pull movement ignores immunity");
    }

    private static void Cll1Priorities()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2F9A, BozjaCll1Catalog.TopCenter));
        f.Actors.Add(Cll1Fixture.Actor(31, 0x2ED5, BozjaCll1Catalog.BottomCenter));
        f.Actors.Add(Cll1Fixture.Actor(32, 0x2F4E, BozjaCll1Catalog.BottomCenter));
        var at = 1d;
        foreach (var top in new[] { false, true })
        {
            f.Draw(top ? BozjaCll1Catalog.TopCenter : BozjaCll1Catalog.BottomCenter);
            foreach (var hp in new uint[] { 1000, 900, 899, 0 })
            {
                f.Change(top ? 1ul : 2ul, a => a with { Hp = hp }); f.Frame(at++);
                var ai = new AIHints();
                foreach (var actor in f.Session.World.Actors) ai.PotentialTargets.Add(new(actor, 0, false));
                f.Source!.GetType().GetMethod("CalculateModuleAIHints", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(f.Source, [0, f.Viewer, default(PartyRolesConfig.Assignment), ai]);
                foreach (var enemy in ai.PotentialTargets)
                    Check(enemy.Priority == f.Native.Cll1TargetPriority(f.Native.Actors[enemy.Actor.InstanceID]), "CLL1 diagnostic priority " + enemy.Actor.InstanceID);
            }
        }
    }

    private static void Cll1LazyAndZero()
    {
        using var f = new Cll1Fixture(false);
        var bottom = BozjaCll1Catalog.BottomCenter;
        f.Actors.Add(Cll1Fixture.Actor(4, 0x2ED7, bottom + Vector3.UnitZ * 10)); f.Frame(0);
        Check(f.Native.Cll1!.Enemies.Count == 0, "CLL1 constructor does not request combat Enemies caches");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Actors.Add(Cll1Fixture.Actor(5, 0x2ED7, bottom - Vector3.UnitX * 10));
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2ECD, BozjaCll1Catalog.TopCenter));
        f.Change(100, a => a with { Tethers = [new(121, 0)] }); f.Frame(1);
        f.Change(101, a => a with { Tethers = [new(124, 0)] });
        f.Change(1, a => a with { InCombat = true }); f.Frame(2);
        Check(f.Source!.Enemies(0x2ED7).First().InstanceID == 5 && f.Native.Cll1.Enemies[0x2ED7].First().Actor.Id == 5,
            "CLL1 phase first Enemies snapshot preserves Dictionary reused-slot order");
        Check(f.Native.Cll1.InfraredActivation == START.AddSeconds(5.7d),
            "CLL1 cold nonzero Infrared ID with zero target still locks clock");
        Check(f.Native.Cll1.MagnetTethered == 2 && f.Native.Cll1.Poles.Count == 1,
            "CLL1 cold zero-target124 still sets actual Party bit");
        f.Tether(3, 100, 121, 20);
        Check(f.Native.Cll1.InfraredActivation == START.AddSeconds(5.7d),
            "CLL1 later valid tether retains cold clock and lazy first Tunnel");
        f.Packet(4, new MechanicEventActionEffect(20, 1, 20950, 0, 0, bottom, 0, 0, 0, []),
            () => f.Native.Effect(20, 20950));
        Check(f.Native.Cll1.MagnetTethered == 0 && f.Native.Cll1.Poles.Count == 0,
            "CLL1 cold zero-target bit allows actual pull Effect to clear orb data");
    }

    private static void Cll1Rebind()
    {
        using var f = new Cll1Fixture(); f.Frame(0);
        var sourceModule = f.Source;
        var state = f.Native.Cll1;
        var bottom = state!.HellDiver;
        var version = f.Native.LoadVersion;
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(1);
        Check(f.Native.Active && f.Native.Primary!.Destroyed, "CLL1 bottom still alive preserves phase while primary absent");
        f.Actors.Add(Cll1Fixture.Actor(1, 0x2ECC, BozjaCll1Catalog.TopCenter) with { InCombat = true }); f.Frame(2);
        Check(ReferenceEquals(sourceModule, f.Source) && ReferenceEquals(state, f.Native.Cll1)
            && f.Native.LoadVersion == version && ReferenceEquals(bottom, f.Native.Cll1!.HellDiver)
            && !f.Native.Primary!.Destroyed, "CLL1 actual Manager primary rebind preserves phase state and original bottom");
    }
}
