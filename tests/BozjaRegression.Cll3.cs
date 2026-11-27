using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using Cll3Source = BossMod.Shadowbringers.Foray.CastrumLacusLitore.CLL3Adrammelech;

internal static partial class BozjaRegression
{
    private static void RunCll3(Action<string, Action> run)
    {
        run("Bozja pending CLL3 client manifest exact clipped arena and raw source centers", Cll3Manifest);
        foreach (var rule in BozjaCll3Catalog.Definition.Rules.Where(r =>
            r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.Charge))
            foreach (var action in rule.Actions)
                run($"Bozja pending CLL3 real Feed standard {rule.Component}/{action}", () => Cll3Standard(rule, action));
        run("Bozja pending CLL3 persistent arena warning cold Created Cast interleaving", Cll3Arena);
        run("Bozja pending CLL3 two water first caster goals immunity strict expiry and sequential movement", Cll3Water);
        run("Bozja pending CLL3 fire ice independent slots exact clock reset death and diagnostic priority", Cll3StayMove);
        run("Bozja pending CLL3 Stone Loc stage first match mismatched count and published Update cache", Cll3Stone);
        run("Bozja pending CLL3 Stone cross caster duplicate FIFO inclusive per-axis boundary", Cll3StoneBoundaries);
        run("Bozja pending CLL3 Twister lazy original actor dead untargetable four clocks and obstacle", Cll3Twister);
        run("Bozja pending CLL3 WarpedLight 98 99 100 sorting risk one second grouped Finish", Cll3Warped);
        run("Bozja pending CLL3 cold snapshot spell ability player pet real sixty status slots", Cll3Cold);
        run("Bozja pending CLL3 no input replay tick refreshes previous height", Cll3NoInputTick);
        run("Bozja pending CLL3 real high to low status slot Gain then Lose immunity history", Cll3SlotOrder);
    }

    private sealed class CastrumFixture : IDisposable
    {
        internal readonly MechanicSession Session;
        private readonly MechanicWorldFeed feed;
        internal readonly BozjaRegion Region;
        private readonly BozjaDefinition definition;
        private readonly BossModuleInfo.Maturity previousMaturity;
        internal BozjaEncounter Native => Region.Encounters.Single(e => e.Definition == definition);
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly List<ReplayPartyMember> Party = [];
        internal BossModule? Source => Session.BossModules.LoadedModules.SingleOrDefault(m => m.PrimaryActor.OID == definition.PrimaryOid);
        internal Actor Viewer => Session.World.Actors.Find(100)!;
        internal BozjaActor NativeViewer => Native.Actors[100];
        internal Vector3 Center => definition.Center;
        internal ushort? ContentFinderOverride;
        private readonly MethodInfo convert = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;

        internal CastrumFixture(bool combat = true, bool dawon = false, bool art = false, bool owain = false, bool raiden = false, bool virtue = false,
            BozjaDefinition? alliance = null)
        {
            Initialize();
            if (alliance != null) definition = alliance;
            else if (virtue) definition = BozjaVirtueCatalog.Definition;
            else if (raiden) definition = BozjaRaidenCatalog.Definition;
            else if (owain) definition = BozjaOwainCatalog.Definition;
            else if (art) definition = BozjaArtCatalog.Definition;
            else if (dawon) definition = BozjaCll4Catalog.Definition;
            else definition = BozjaCll3Catalog.Definition;
            previousMaturity = BossModuleManager.Config.MinMaturity;
            if (alliance != null && (AllianceStandardCatalog.Definitions.Concat(AllianceKnockbackCatalog.Definitions).Contains(definition)
                || definition == OzmaCatalog.Definition || definition == IrminsulCatalog.Definition
                || definition == ArachneCatalog.Definition || definition == DeathgazeCatalog.Definition))
                BossModuleManager.Config.MinMaturity = BossModuleInfo.Maturity.WIP;
            Region = new(definition.Territory);
            Session = new(definition.Key + "-source-differential");
            feed = new(Session);
            Actors.Add(Cll1Fixture.Actor(1, definition.PrimaryOid, Center) with { InCombat = combat });
            Actors.Add(Cll1Fixture.Actor(20, 0x233C, Center + Vector3.UnitX * 3));
            for (var i = 0; i < 8; ++i)
            {
                var player = Cll1Fixture.Actor((ulong)(100 + i), 0, Center + Vector3.UnitX * (i + 10)) with { Kind = 1, SubKind = 4 };
                if (AllianceKnockbackCatalog.Definitions.Contains(definition) || definition == OzmaCatalog.Definition
                    || definition == IrminsulCatalog.Definition || definition == ArachneCatalog.Definition
                    || definition == DeathgazeCatalog.Definition)
                    player = player with { HitboxRadius = .5f };
                Actors.Add(player);
                Party.Add(new(i, "", (ulong)(100 + i)));
            }
        }

        internal void Change(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }

        internal ReplayCast Spell(uint action, double at = 0, float total = 5, Vector3? point = null, ulong target = 100)
            => new(action, target, point ?? Center, .47f, START.AddSeconds(at),
                START.AddSeconds(at + total + .3f), false, false) { IsSpell = true, TotalTime = total };

        internal void Frame(double at, bool compare = true, MechanicFeedEvent? packet = null, Action? native = null,
            MechanicFeedEvent[]? packets = null)
        {
            var now = START.AddSeconds(at);
            Session.BeginFrame(now);
            var cfc = ContentFinderOverride ?? definition.Territory switch { 508 => 120, 556 => 168, 627 => 220, 966 => 779, 917 => 736, 1054 => 866, 1304 => 1058, 1248 => 1015, 1178 => 962,
                827 => 639, _ => (ushort)735 };
            feed.ApplyFrame(new((ushort)definition.Territory, cfc, 100, 0,
                Party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                Actors.Select((a, i) =>
                {
                    var snapshot = (MechanicActorSnapshot)convert.Invoke(null, [a, i, now])!;
                    // The legacy adapter compacts status positions and discards
                    // IsSpell. Restore recorded inputs explicitly in this oracle.
                    return snapshot with
                    {
                        Cast = snapshot.Cast == null ? null : snapshot.Cast with
                        {
                            IsSpell = a.Cast?.IsSpell != false,
                            TotalTime = Native.UsesRawNpcClock ? a.Cast!.TotalTime ?? snapshot.Cast.TotalTime : snapshot.Cast.TotalTime,
                            ElapsedTime = Native.UsesRawNpcClock ? Math.Clamp((float)(now - a.Cast!.StartedAt).TotalSeconds,
                                0, a.Cast.TotalTime ?? snapshot.Cast.TotalTime) : snapshot.Cast.ElapsedTime,
                        },
                        Statuses = snapshot.Statuses.Select((s, index) =>
                            s with { Slot = a.Statuses[index].SlotIndex ?? index }).ToArray(),
                    };
                }).ToArray()));
            // The legacy feed only submits eight slots. Restore the explicit
            // alliance/NPC inputs for this source-component oracle.
            if (Native.IsDeathgaze)
                for (var slot = 8; slot < 64; ++slot)
                {
                    var member = Party.FirstOrDefault(p => p.Slot == slot);
                    Session.Execute(new PartyState.OpModify(slot, member == null ? PartyState.EmptySlot
                        : new PartyState.Member(member.EntityId, member.EntityId, false, "")));
                }
            if (packet != null) feed.Enqueue(packet);
            foreach (var extra in packets ?? []) feed.Enqueue(extra);
            feed.Flush();
            Session.Update();
            Region.Synchronize(Actors.ToArray(), Party.ToArray(), 100, now, true, native, cfc: cfc);
            if (compare) Compare();
        }

        internal void Tick(double at)
        {
            var now = START.AddSeconds(at);
            Session.BeginFrame(now); Session.Update();
            Region.Update(now);
            Compare();
        }

        internal void Draw(Vector3 position)
        {
            Change(100, a => a with { Position = position });
            Frame((Native.Now - START).TotalSeconds);
            using (MechanicDrawSink.Install(new(new())))
                Source!.Components.Single(c => c.GetType().Name == "ArenaChange").DrawArenaBackground(0, Viewer);
            Native.ObserveCll4Drawing(NativeViewer);
            Compare();
        }

        internal void Effect(double at, uint action, ulong source = 20)
            => Frame(at, packet: new MechanicEventActionEffect(source, 1, action, 0, 0, default, 0, 0, 0, []),
                native: () => Region.Effect(source, action));

        internal void Compare()
        {
            var source = Source;
            Check(Native.Active == (source?.StateMachine.ActiveState != null),
                $"CLL3 exact phase state t={(Native.Now - START).TotalSeconds} source={source?.PrimaryActor.InstanceID}/{source?.StateMachine.ActiveState?.ID} native={Native.Primary?.Actor.Id}/{Native.Active}");
            if (source == null) return;
            Check(source.Arena.Center.ToVec3() == Native.Center && source.Arena.Bounds.Radius == Native.SampleRadius,
                $"CLL3 source center radius {Native.Center}: source={source.Arena.Bounds.Radius:R} native={Native.SampleRadius:R}");
            if (!Native.Active) return;
            Check(source.Components.Select(c => c.GetType().Name).SequenceEqual(Native.ActiveRules.Select(r => r.Component)),
                "CLL3 fourteen actual components");
            if (Native.IsRaiden) RaidenRawCompare(this);
            if (Native.IsVirtue) VirtueRawCompare(this);
            if (Native.IsRhalgr) RhalgrRawCompare(this);
            if (Native.IsLightArtillery) LightRawCompare(this);
            if (Native.IsArt)
            {
                var chasing = source.Components.OfType<StandardChasingAOEs>().SingleOrDefault();
                if (chasing != null)
                {
                    Check(chasing.TargetsMask.Raw == Native.ArtChaseTargetsMask && chasing.Activation == Native.ArtChaseActivation
                        && chasing.NumCasts == Native.ArtChaseCount, "Art chase mask activation count raw source state");
                    Check(chasing.Targets.Select(a => (a.InstanceID, a.Position.ToVec3(), a.IsDestroyed, a.IsDead))
                        .SequenceEqual(Native.ArtChaseTargets.Select(a => (a.Actor.Id, a.Actor.Position with { Y = 0 }, a.Destroyed, a.Actor.Dead))),
                        "Art exact ordered original Targets");
                    Check(chasing.Chasers.Count == Native.ArtChasers.Count, "Art internal chaser count");
                    for (var i = 0; i < chasing.Chasers.Count; ++i)
                    {
                        var e = chasing.Chasers[i]; var a = Native.ArtChasers[i];
                        Check(e.Target.InstanceID == a.Target.Actor.Id && e.Target.Position.ToVec3() == a.Target.Actor.Position with { Y = 0 }
                            && e.Target.IsDestroyed == a.Target.Destroyed && e.Target.IsDead == a.Target.Actor.Dead
                            && e.PrevPos.ToVec3() == a.Previous && e.MoveDist == a.MoveDistance && e.NumRemaining == a.Remaining
                            && e.NextActivation == a.Activation, "Art exact internal chaser " + i);
                    }
                }
            }
            var ai = new AIHints();
            var viewerSlot = source.Raid.FindSlot(100);
            foreach (var component in source.Components)
            {
                if (component is GenericAOEs aoes)
                {
                    var expected = aoes.ActiveAOEs(viewerSlot, Viewer).ToArray();
                    var actual = Native.Visible(NativeViewer).Where(a => a.Cast.Rule.Component == component.GetType().Name).ToArray();
                    Check(expected.Length == actual.Length,
                        $"{definition.Key} t={(Native.Now - START).TotalSeconds} AOE count {component.GetType().Name}: source={expected.Length} native={actual.Length}");
                    for (var i = 0; i < actual.Length; ++i)
                    {
                        var e = expected[i]; var a = actual[i];
                        var expectedColor = component is Exaflare ? Colors.AOE : 0;
                        if (a.Danger)
                            expectedColor = Colors.Danger;
                        if (a.SafeColor)
                            expectedColor = Colors.SafeFromAOE;
                        Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation
                            && e.Activation == a.Cast.Activation && e.Risky == a.Risky
                            && e.Color == expectedColor,
                            $"Castrum fields {component.GetType().Name}/{i}");
                        if (e.Shape == null)
                        {
                            Check(a.Cast.Shape == null && !a.Risky, "Virtue exact default AOE tail");
                            continue;
                        }
                        for (var x = Native.Center.X - 35; x < Native.Center.X + 35; x += 3.371f)
                            for (var z = Native.Center.Z - 35; z < Native.Center.Z + 35; z += 3.713f)
                                Check(e.Check(new(x, z)) == a.Cast.Contains(new(x, 0, z)), "CLL3 source raw shape " + component.GetType().Name);
                    }
                }
                component.AddAIHints(viewerSlot, Viewer, default, ai);
                if (component is SimpleKnockbacks kb)
                {
                    var expected = kb.CalculateMovements(viewerSlot, Viewer);
                    var nativeRule = Native.ActiveRules.Single(r => r.Component == component.GetType().Name);
                    var actual = (nativeRule.Kind == BozjaRuleKind.AllianceKnockback
                        ? Native.AllianceSimpleMovements(NativeViewer, nativeRule)
                        : Native.IsAllianceTrash ? Native.AlliancePullMovements(NativeViewer)
                        : Native.IsCll4 ? Native.Cll4Movements(NativeViewer)
                        : Native.Cll3WaterMovements(NativeViewer,
                            Native.ActiveRules.Single(r => r.Component == component.GetType().Name))).ToArray();
                    Check(expected.Count == actual.Length, $"CLL3 water count {component.GetType().Name} "
                        + $"source={expected.Count} native={actual.Length} at={Native.Now:O}");
                    for (var i = 0; i < actual.Length; ++i)
                        Check(expected[i].from.ToVec3() == actual[i].From && expected[i].to.ToVec3() == actual[i].To,
                            "CLL3 exact water endpoints");
                }
            }
            ai.Normalize();
            if (Native.IsOwain)
            {
                var tethers = ((System.Collections.IEnumerable)source.Components.OfType<GenericGaze>().Single()
                    .GetType().GetField("Tethers")!.GetValue(source.Components.OfType<GenericGaze>().Single())!)
                    .Cast<(Actor? Target, Actor Source)>().ToArray();
                Check(tethers.Length == Native.OwainTethers.Count,
                    $"Owain t={(Native.Now - START).TotalSeconds} raw tether count: source={tethers.Length} native={Native.OwainTethers.Count}");
                for (var i = 0; i < tethers.Length; ++i)
                    Check(tethers[i].Target?.InstanceID == Native.OwainTethers[i].Target?.Actor.Id
                        && tethers[i].Source.InstanceID == Native.OwainTethers[i].Source.Actor.Id
                        && tethers[i].Source.IsDestroyed == Native.OwainTethers[i].Source.Destroyed,
                        "Owain original ordered source target tether tuple " + i);
                Check(ai.ForbiddenDirections.Select(d => (d.center.Rad, d.halfWidth.Rad, d.activation))
                    .SequenceEqual(Native.GazeDirections(NativeViewer.Actor)), "Owain exact inverse gaze forbidden directions");
                var sourceCleaves = source.Components.OfType<Cleave>().Single().OriginsAndTargets();
                var nativeCleaves = Native.OwainCleaves().ToArray();
                Check(sourceCleaves.Count == nativeCleaves.Length,
                    $"Owain t={(Native.Now - START).TotalSeconds} exact continuous cleave count: source={sourceCleaves.Count} native={nativeCleaves.Length}");
                for (var i = 0; i < nativeCleaves.Length; ++i)
                    Check(sourceCleaves[i].origin.Position.Quantized().ToVec3() == nativeCleaves[i].Origin
                        && sourceCleaves[i].target.InstanceID == nativeCleaves[i].Target!.Actor.Id
                        && sourceCleaves[i].angle.Rad == nativeCleaves[i].Rotation, "Owain cleave locked origin current target and angle");
            }
            var mode = Native.Cll3SpecialMode(NativeViewer) ?? Native.ArtSpecialMode(NativeViewer) ?? Native.OzmaSpecialMode(NativeViewer);
            Check((ai.ImminentSpecialMode.Item1 == AIHints.SpecialMode.Normal) == (mode == null), "CLL3 diagnostic presence");
            if (mode is { } special)
                Check(ai.ImminentSpecialMode.Item1.ToString() == special.Mode && ai.ImminentSpecialMode.Item2 == special.Activation,
                    "CLL3 earliest special mode and tie order");
            var nav = Native.NavigationForHost(NativeViewer).Where(z => !z.Obstacle).ToArray();
            Check(ai.ForbiddenZones.Count == nav.Length,
                $"{definition.Key} t={(Native.Now - START).TotalSeconds} raw navigation count: source={ai.ForbiddenZones.Count} native={nav.Length} [{string.Join(",", nav.Select(z => z.Component))}]");
            for (var i = 0; i < nav.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == nav[i].Activation, "CLL3 navigation clock and sort " + i);
                for (var x = Native.Center.X - 34; x < Native.Center.X + 34; x += 2.713f)
                    for (var z = Native.Center.Z - 34; z < Native.Center.Z + 34; z += 2.791f)
                    {
                        var expected = ai.ForbiddenZones[i].Item1.Distance(new(x, z));
                        var actual = nav[i].Distance(x, z);
                        Check(expected == actual || float.IsNaN(expected) && float.IsNaN(actual), $"CLL3 raw SDF {i} {x}/{z}: {expected}/{actual}");
                    }
            }
            Check(ai.TemporaryObstacles.Count == Native.Navigation(NativeViewer).Count(z => z.Obstacle), "CLL3 obstacle count");
            var damage = Native.DamageForHost(NativeViewer);
            Check(ai.PredictedDamage.Count == damage.Length, "CLL3 damage count");
            for (var i = 0; i < damage.Length; ++i)
                Check(ai.PredictedDamage[i].Players.Raw == damage[i].Mask && ai.PredictedDamage[i].Activation == damage[i].Activation,
                    "CLL3 raw NPC damage identity clock");
            var hints = new BossComponent.TextHints();
            var globals = new BossComponent.GlobalHints();
            foreach (var component in source.Components)
            {
                component.AddHints(viewerSlot, Viewer, hints);
                component.AddGlobalHints(globals);
            }
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(Native.Hints(NativeViewer).Select(h => (h.Text, h.Risky))),
                "CLL3 ordered hints: " + string.Join("/", hints.Select(h => h.Item1)));
            Check(globals.SequenceEqual(Native.GlobalHints()), "CLL3 global hints");
        }

        public void Dispose()
        {
            Session.Dispose();
            BossModuleManager.Config.MinMaturity = previousMaturity;
        }
    }

    private static void Cll3Manifest()
    {
        using var f = new CastrumFixture(); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9442)?.Singular.ToString() == f.Native.Definition.Name,
            "CLL3 actual client name");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(735)?.TerritoryType.RowId == 920, "CLL3 CFC");
        foreach (var action in Enum.GetValues<Cll3Source.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CLL3 action extra " + action);
        void Geometry()
        {
            for (var x = 48f; x < 113; x += .371f)
                for (var z = -639f; z < -573; z += .417f)
                    Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "CLL3 exact arena polygon");
        }
        Check(f.Native.Center.Z == -606.1005f, "CLL3 clipped arena actual center");
        Geometry();
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EB037, f.Center)); f.Frame(1); Geometry();
    }

    private static void Cll3Standard(BozjaRule rule, uint action)
    {
        using var f = new CastrumFixture();
        f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(action, .1, point: f.Center + new Vector3(8, 0, 9)) }); f.Frame(.1);
        f.Change(20, a => a with { Position = f.Center + new Vector3(-13, 0, 2), Rotation = 1.4f }); f.Frame(2);
        f.Frame(50); // elapsed caps but no invented timeout.
        f.Change(20, a => a with { Cast = null }); f.Frame(51);
        Check(!f.Native.Casts.Any(c => c.Rule == rule), "CLL3 actual watched Finish " + action);
    }

    private static void Cll3Arena()
    {
        foreach (var wallFirst in new[] { false, true })
        {
            using var f = new CastrumFixture(false);
            f.Change(20, a => a with { Cast = f.Spell(20374) });
            var wall = Cll1Fixture.Actor(30, 0x1EB037, f.Center);
            if (wallFirst) f.Actors.Insert(0, wall); else f.Actors.Add(wall);
            f.Frame(0); f.Change(1, a => a with { InCombat = true }); f.Frame(1);
            Check(f.Native.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.Cll3Arena) == (wallFirst ? 0 : 1), "CLL3 component-major interleaved cold");
            f.Change(20, a => a with { Cast = null }); f.Frame(2);
            f.Change(20, a => a with { Cast = f.Spell(20374, 3) }); f.Frame(3);
            Check(f.Native.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.Cll3Arena) == (wallFirst ? 0 : 1), "CLL3 retained warning after wall and later Holy");
        }
    }

    private static void Cll3Water()
    {
        using var f = new CastrumFixture();
        f.Change(20, a => a with { Cast = f.Spell(20556) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center + Vector3.UnitZ * 2) with { Cast = f.Spell(20356) });
        f.Frame(0);
        Check(f.Native.Navigation(f.NativeViewer).Count(z => z.Goal) == 2, "CLL3 independent two water Goals");
        var expiry = f.Native.Casts.First(c => c.Rule.Kind == BozjaRuleKind.Cll3Water).Activation;
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, expiry.AddSeconds(1)) { SlotIndex = 59 }] });
        f.Frame(1);
        Check(!f.Native.Cll3WaterMovements(f.NativeViewer).Any()
            && f.Native.Navigation(f.NativeViewer).Count(z => z.Goal) == 2, "CLL3 immunity does not remove custom AI");
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, expiry) { SlotIndex = 59 }] }); f.Frame(1.1);
        // A shortened expiry updates the physical slot but does not dispatch Gain.
        f.Change(100, a => a with { Statuses = [] }); f.Frame(1.2);
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, expiry) { SlotIndex = 58 }] }); f.Frame(1.3);
        Check(f.Native.Cll3WaterMovements(f.NativeViewer).Count() == 2, "CLL3 strict immunity equal expiry");
        foreach (var zone in f.Native.Navigation(f.NativeViewer).Where(z => z.Goal))
        {
            Check(zone.Convert(f.Native.Center, f.Native.SampleRadius).Count() == 1, "CLL3 water direct circle Goal");
        }
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.Navigation(f.NativeViewer).Count(z => z.Goal) == 1, "CLL3 independent water watched Finish");
    }

    private static void Cll3StayMove()
    {
        using var f = new CastrumFixture(); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(21595, .1, 3) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(21596, .1, 5) });
        f.Frame(.1);
        Check(f.Native.Cll3SpecialMode(f.NativeViewer)?.Mode == "Freezing", "CLL3 same activation tie keeps ice");
        f.Frame(3);
        f.Change(100, a => a with { Position = a.Position + Vector3.UnitX * 2 }); f.Frame(3.1);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Move!" && h.Risky), "CLL3 source finite XZ self comparison");
        f.Change(100, a => a with { Position = a.Position + Vector3.UnitY }); f.Frame(3.2);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Move!" && !h.Risky), "CLL3 source compares previous height");
        f.Change(100, a => a with { Cast = f.Spell(999, 3.3) with { IsSpell = false }, TargetId = 0 }); f.Frame(3.3);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stop everything!" && h.Risky),
            "CLL3 source raw Player Ability CastInfo still makes Stay risky");
        var activation = f.Native.Cll3Ice[0];
        f.Frame((activation - START).TotalSeconds);
        Check(f.Native.Cll3Ice[0] != default, "CLL3 ice equal activation retained");
        f.Frame((activation - START).TotalSeconds + .001);
        Check(f.Native.Cll3Ice[0] == default && f.Native.Cll3Fire[0] != default, "CLL3 ice strict elapsed fire retained");
        f.Change(100, a => a with { Statuses = [new(960, 0, 1, START.AddSeconds(100)) { SlotIndex = 59 }] }); f.Frame(10);
        f.Change(100, a => a with { Statuses = [] }); f.Frame(11);
        Check(f.Native.Cll3Fire[0] == default && f.Native.Cll3Fire[1] != default, "CLL3 Pyretic Lose clears real current slot");
        f.Change(100, a => a with { Dead = true });
        f.Change(20, a => a with { Cast = f.Spell(21596, 12) }); f.Frame(12);
        Check(f.Native.Cll3Fire[0] == default && f.Native.Cll3Fire[1] != default, "CLL3 death clears own slot via hints");
    }

    private static void Cll3Stone()
    {
        using var f = new CastrumFixture(); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20353, .1) }); f.Frame(.1);
        var source = (ConcentricAOEs)f.Source!.Components.Single(c => c.GetType().Name == "StoneIV");
        var published = f.Native.Visible().Single(a => a.Cast.Rule.Component == "StoneIV").Cast;
        var wrong = f.Spell(20360, 1, point: f.Center + Vector3.UnitX * 20);
        var sourceSpell = new ActorCastInfo { Action = new(ActionType.Spell, 20360), Location = wrong.Location };
        source.OnCastFinished(f.Session.World.Actors.Find(20)!, sourceSpell);
        f.Native.Finish(f.Native.Actors[20], wrong);
        Check(f.Native.NumEarthCasts == source.NumCasts, "CLL3 mismatched known stage still counts");
        f.Compare();
        Check(f.Native.Visible().Single(a => a.Cast.Rule.Component == "StoneIV").Cast == published, "CLL3 pre Update cache remains");
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        f.Change(20, a => a with { Cast = f.Spell(20354, 3) }); f.Frame(3);
        f.Change(20, a => a with { Cast = null }); f.Frame(4);
        f.Change(20, a => a with { Cast = f.Spell(20355, 5) }); f.Frame(5);
        f.Change(20, a => a with { Cast = null }); f.Frame(6);
        Check(!f.Native.Visible().Any(a => a.Cast.Rule.Component == "StoneIV"), "CLL3 actual three stages complete");
    }

    private static void Cll3Twister()
    {
        using var f = new CastrumFixture(); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2E09, f.Center + new Vector3(1, 0, 2)) with { Rotation = 1.1f, Dead = true, Targetable = false });
        f.Frame(1);
        var zones = f.Native.Navigation(f.NativeViewer).Where(z => z.Component == "Twister").ToArray();
        Check(zones.Length == 5 && zones.Count(z => z.Obstacle) == 1, "CLL3 dead untargetable Twister remains");
        Check(zones.Take(4).Select(z => ((BozjaCapsuleTemplate)z.Shape!).Length).SequenceEqual(new float[] { 2.5f, 5, 10, 15 }),
            "CLL3 actual four lengths");
        f.Change(30, a => a with { Position = a.Position + Vector3.UnitX * 6, Rotation = .3f }); f.Frame(2);
        f.Actors.RemoveAll(a => a.Id == 30); f.Frame(3);
        Check(!f.Native.Navigation(f.NativeViewer).Any(z => z.Component == "Twister"), "CLL3 real Destroy clears cached Twister");
    }

    private static void Cll3StoneBoundaries()
    {
        using var f = new CastrumFixture(); f.Frame(0);
        var source = (ConcentricAOEs)f.Source!.Components.Single(c => c.GetType().Name == "StoneIV");
        var sourceActor = f.Session.World.Actors.Find(20)!;
        var cast = f.Spell(20353);
        var sourceCast = new ActorCastInfo { Action = new(ActionType.Spell, 20353), TotalTime = 5,
            Location = cast.Location, Rotation = new(.47f) };
        sourceActor.CastInfo = sourceCast;
        f.Native.Actors[20].Cast = cast;
        for (var i = 0; i < 2; ++i)
        {
            source.OnCastStarted(sourceActor, sourceCast);
            f.Native.Start(f.Native.Actors[20]);
        }
        void Publish()
        {
            source.Update(); f.Native.Update(f.Native.Now); f.Compare();
        }
        void Finish(uint action, Vector3 point)
        {
            // A different original caster can advance the first matching Loc.
            var other = f.Session.World.Actors.Find(1)!;
            source.OnCastFinished(other, new() { Action = new(ActionType.Spell, action), Location = point });
            f.Native.Finish(f.Native.Actors[1], cast with { ActionId = action, Location = point });
        }
        Publish();
        Finish(20359, f.Center + new Vector3(1, 0, 1)); Publish();
        Check(source.Sequences[0].NumCastsDone == 1 && source.Sequences[1].NumCastsDone == 0,
            "CLL3 diagonal exactly one accepted first duplicate only");
        var count = f.Native.NumEarthCasts;
        Finish(999, f.Center); Publish();
        Check(f.Native.NumEarthCasts == count, "CLL3 unknown negative order true no-op");
        Finish(20353, f.Center with { X = MathF.BitIncrement(f.Center.X + 1) }); Publish();
        Check(f.Native.NumEarthCasts == count + 1 && source.Sequences[1].NumCastsDone == 0,
            "CLL3 one axis over boundary counts but does not match");
        Finish(20353, f.Center); Publish();
        Check(source.Sequences.All(s => s.NumCastsDone == 1), "CLL3 next duplicate advances across caster");
    }

    private static void Cll3Warped()
    {
        using var f = new Fixture(39);
        var source = f.Activate<GenericAOEs>("WarpedLight");
        var spell = f.Start(source, 20365);
        var rule = f.M.ActiveRules.Single(r => r.Component == "WarpedLight");
        void Compare()
        {
            var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible().Where(a => a.Cast.Rule == rule).ToArray();
            Check(expected.Length == actual.Length, "CLL3 99 charge count");
            for (var i = 0; i < expected.Length; ++i)
                Check(expected[i].Activation == actual[i].Cast.Activation && expected[i].Origin.ToVec3() == actual[i].Cast.Origin
                    && expected[i].Risky == actual[i].Risky, "CLL3 98/99/100 charge unstable queue sorting");
        }
        for (var i = 1; i < 101; ++i)
        {
            spell.TotalTime = i % 3 + 2;
            f.Set(2, a => a with { Cast = a.Cast! with { TotalTime = spell.TotalTime } });
            f.M.Actors[2].Cast = f.Actors.Single(a => a.Id == 2).Cast;
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            if (i >= 97) Compare();
        }
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); Compare();
        source.OnCastFinished(f.W.Actors.Find(2)!, new() { Action = new(ActionType.Spell, 999) });
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 999 }); Compare();
    }

    private static void Cll3Cold()
    {
        using var f = new CastrumFixture(false);
        f.Change(20, a => a with { Cast = f.Spell(20556) });
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 }] });
        f.Actors.Add(Cll1Fixture.Actor(30, 0x233C, f.Center) with { Cast = f.Spell(21596) with { IsSpell = false } });
        f.Actors.Add(Cll1Fixture.Actor(31, 0x233C, f.Center) with { Kind = 1, SubKind = 4, Cast = f.Spell(20363) });
        f.Frame(0); f.Change(1, a => a with { InCombat = true }); f.Frame(2);
        Check(!f.Native.Cll3WaterMovements(f.NativeViewer).Any() && f.Native.Cll3Fire[0] == default,
            "CLL3 cold real slot59 immune no raw Ability or Player Spell");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(3);
        Check(!f.Native.Active && f.Native.Completed, "CLL3 normal phase HP0 completion");
    }

    private static void Cll3NoInputTick()
    {
        using var f = new CastrumFixture();
        f.Change(20, a => a with { Cast = f.Spell(21595) }); f.Frame(0);
        f.Change(100, a => a with { Position = a.Position + Vector3.UnitY }); f.Frame(3.5);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Move!" && !h.Risky), "CLL3 moving height frame satisfied");
        f.Tick(3.55);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Move!" && h.Risky),
            "CLL3 same physical snapshot next tick previous height catches up");
    }

    private static void Cll3SlotOrder()
    {
        using var f = new CastrumFixture();
        f.Change(20, a => a with { Cast = f.Spell(20556) }); f.Frame(0);
        var status = new ReplayStatus(1209, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 };
        f.Change(100, a => a with { Statuses = [status] }); f.Frame(1);
        var source = (SimpleKnockbacks)f.Source!.Components.Single(c => c.GetType().Name == "WaterIV1");
        Check(f.Viewer.Statuses[59].ID == 1209 && f.Viewer.Statuses[0].ID == 0 && source.IsImmune(0, START.AddSeconds(10)),
            "CLL3 oracle has actual slot59, not compact index0");
        f.Change(100, a => a with { Statuses = [status with { SlotIndex = 0 }] }); f.Frame(2);
        Check(f.Viewer.Statuses[59].ID == 0 && f.Viewer.Statuses[0].ID == 1209 && !source.IsImmune(0, START.AddSeconds(10))
            && f.Native.Cll3WaterMovements(f.NativeViewer).Any(),
            "CLL3 gain low then lose high clears immunity despite physical same status");
    }
}
