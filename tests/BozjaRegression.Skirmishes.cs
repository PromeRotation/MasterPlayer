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
    private static void RunSkirmishes(Action<string, Action> run)
    {
        RunCll1(run);
        RunPyromancer(run);
        run("Bozja pending Skirmish two modules same-frame reset order and selection", SkirmishResetOrder);
        run("Bozja pending Skirmish creation old Party0 and source-viewer separation", SkirmishPartyGate);
        foreach (var index in new[] { 35, 36 })
        {
            var definition = BozjaCatalog.Definitions[index];
            run("Bozja pending Skirmish client and FATE lifecycle " + definition.Key, () => SkirmishLifecycle(index));
            foreach (var rule in definition.Rules)
                run("Bozja pending Skirmish real Feed standard " + definition.Key + "/" + rule.Component,
                    () => SkirmishStandard(index, rule));
            run("Bozja pending Skirmish real Party 7 8 stack and target generations " + definition.Key,
                () => SkirmishStacks(index));
            run("Bozja pending Skirmish original targets FIFO first clock old Party " + definition.Key,
                () => SkirmishStackReferences(index));
        }
    }

    private sealed class SkirmishFixture : IDisposable
    {
        internal readonly MechanicSession Session;
        private readonly MechanicWorldFeed feed;
        internal readonly BozjaEncounter Native;
        internal readonly BozjaRegion Region = new();
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly List<ReplayPartyMember> Party = [];
        internal ReplayActiveFate Fate;
        internal ulong SourcePlayerId = 100;
        internal BossModule? Source => Session.BossModules.LoadedModules.SingleOrDefault(m => m.PrimaryActor.OID == Native.Definition.PrimaryOid);
        internal Actor Viewer => Session.World.Actors.Find(100)!;
        internal BozjaActor NativeViewer => Native.Actors[100];
        internal Vector3 Center => new(100, 0, 100);
        private readonly MethodInfo converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;
        private MechanicActorSnapshot SourceActor(ReplayActorState actor, int index, DateTime now)
        {
            var snapshot = (MechanicActorSnapshot)converter.Invoke(null, [actor, index, now])!;
            // Compare the real live Feed fields. The legacy replay adapter
            // drops FateId and forces every cast to Spell.
            return snapshot with
            {
                FateId = actor.FateId ?? 0,
                Cast = snapshot.Cast == null ? null : snapshot.Cast with { IsSpell = actor.Cast?.IsSpell != false },
            };
        }

        internal SkirmishFixture(int index)
        {
            Initialize();
            Native = Region.Encounters.Single(e => e.Definition == BozjaCatalog.Definitions[index]);
            Session = new("Skirmish-source-differential");
            feed = new(Session);
            Fate = new(Native.Definition.FateId, Center, 40);
            Actors.Add(new() { Id = 1, Oid = Native.Definition.PrimaryOid, Kind = 2, SubKind = 5,
                Position = Center, Targetable = true, InCombat = true, Hp = 1000, MaxHp = 1000 });
            Actors.Add(new() { Id = 20, Oid = 0x115F, Kind = 2, SubKind = 5, Position = Center, Hp = 1000 });
            for (var i = 0; i < 8; ++i)
            {
                Actors.Add(new() { Id = (ulong)(100 + i), Kind = 1, Position = Center, Hp = 1000 });
                Party.Add(new(i, "", (ulong)(100 + i)));
            }
        }

        internal void Change(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }

        internal ReplayCast Spell(uint action, double at = 0, ulong target = 100, float total = 5)
            => new(action, target, Center, .4f, START.AddSeconds(at), START.AddSeconds(at + total + .3f), false, false)
            { IsSpell = true, TotalTime = total };

        internal void Frame(double at, MechanicFeedEvent? ev = null, Action? nativeEvent = null,
            bool compare = true, Action? sourceEvent = null)
        {
            var now = START.AddSeconds(at);
            Session.BeginFrame(now);
            Session.World.Execute(new ClientState.OpActiveFateChange(new(Fate.Id, Fate.Center, Fate.Radius, 0, 0, 0)));
            feed.ApplyFrame(new(920, 735, 100, 0,
                Party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                Actors.Select((a, i) => SourceActor(a, i, now)).ToArray()));
            if (ev != null) feed.Enqueue(ev);
            feed.Flush();
            sourceEvent?.Invoke();
            Session.Update();
            Region.Synchronize(Actors.ToArray(), Party.ToArray(), SourcePlayerId, now, true, nativeEvent, Fate);
            if (compare) Compare();
        }

        internal void Compare()
        {
            var source = Source;
            Check(Native.Active == (source?.StateMachine.ActiveState != null),
                $"Skirmish active phase {Native.Definition.Key} at {Native.Now:o}: source={source?.StateMachine.ActiveState != null} native={Native.Active}");
            if (source == null) return;
            Check(source.Arena.Center.ToVec3() == Native.Center && source.Arena.Bounds.Radius == Native.SampleRadius,
                $"Skirmish actual FATE circle source={source.Arena.Center}/{source.Arena.Bounds.Radius} native={Native.Center}/{Native.SampleRadius}");
            if (!Native.Active && !Native.IsPyromancer) return;
            Check(source.Components.Select(c => c.GetType().Name).SequenceEqual(Native.ActiveRules.Select(r => r.Component)),
                "Skirmish eight components activation order");
            if (Session.World.Party[0] == null) return;
            foreach (var component in source.Components.OfType<GenericAOEs>())
            {
                var expected = component.ActiveAOEs(0, Viewer).ToArray();
                var actual = Native.Visible(NativeViewer).Where(v => v.Cast.Rule.Component == component.GetType().Name).ToArray();
                Check(expected.Length == actual.Length, "Skirmish AOE count " + component.GetType().Name);
                for (var i = 0; i < expected.Length; ++i)
                {
                    var e = expected[i]; var a = actual[i];
                    Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation && e.Activation == a.Cast.Activation
                        && e.Risky == a.Risky && (e.Color == Colors.Danger) == a.Danger, "Skirmish exact AOE fields");
                    for (var x = -77f; x < 77; x += 2.713f)
                        for (var z = -77f; z < 77; z += 3.791f)
                        {
                            var p = Center + new Vector3(x, 0, z);
                            Check(e.Check(new(p.X, p.Z)) == a.Cast.Contains(p), "Skirmish raw shape " + component.GetType().Name + p);
                        }
                }
            }
            var hints = new BossComponent.TextHints(); var ai = new AIHints();
            foreach (var c in source.Components) { c.AddHints(0, Viewer, hints); c.AddAIHints(0, Viewer, default, ai); }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(Native.Hints(NativeViewer).Select(h => (h.Text, h.Risky))),
                "Skirmish hints ordered Party min8");
            Check(ai.PredictedDamage.Count == 0 && !Native.DamageForHost(NativeViewer).Any(), "Skirmish no invented stack damage");
            var zones = Native.NavigationForHost(NativeViewer);
            if (!Native.Active)
            {
                Check(zones.Length == 0, "Pyromancer constructor component does not become host navigation");
                return;
            }
            Check(ai.ForbiddenZones.Count == zones.Length, "Skirmish normalized zone count");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, "Skirmish raw first stack clock");
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, Native.Center, Native.SampleRadius, out var kind);
                var expected = kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals) ? goals : converted;
                var actual = zones[i].Convert(Native.Center, Native.SampleRadius).ToArray();
                Check(expected.Count == actual.Length, "Skirmish actual conversion count");
                for (var x = -21f; x < 21; x += 1.731f)
                    for (var z = -21f; z < 21; z += 2.713f)
                    {
                        var p = Center + new Vector3(x, 0, z);
                        Check(expected.Any(g => g.Template.Resolve(g.Position, g.Rotation, null).Contains(p, 0))
                            == actual.Any(g => g.Contains(p, 0)), "Skirmish actual stack or AOE navigation " + p);
                    }
            }
        }

        public void Dispose() => Session.Dispose();
    }

    private static void SkirmishStandard(int index, BozjaRule rule)
    {
        foreach (var cold in new[] { false, true })
        {
            using var f = new SkirmishFixture(index);
            if (!cold) f.Frame(0);
            f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], cold ? 0 : .5) });
            f.Frame(cold ? 0 : .5);
            Check(f.Native.Casts.Count == 1, "Skirmish cast really hit");
            f.Frame(1, new MechanicEventActionEffect(20, 1, rule.Actions[0], 100, 0, f.Center, 0, 0, 0, []),
                () => f.Native.Effect(20, rule.Actions[0], 100, f.Center));
            f.Frame(99);
            Check(f.Native.Casts.Count == 1, "Skirmish Effect and timeout retain");
            f.Change(20, a => a with { Cast = null }); f.Frame(100);
            Check(f.Native.Casts.Count == 0, "Skirmish actual Finish clears first");
        }
    }

    private static void SkirmishLifecycle(int index)
    {
        using var f = new SkirmishFixture(index);
        var name = Service.LuminaRow<Lumina.Excel.Sheets.Fate>(f.Native.Definition.FateId)?.Name.ToString();
        Check(name == f.Native.Definition.Name, "Skirmish exact client FATE name");
        var sourceType = typeof(BossModule).Assembly.GetType(
            $"BossMod.Shadowbringers.Foray.Skirmish.{f.Native.Definition.Key}.AID")!;
        foreach (var action in Enum.GetValues(sourceType).Cast<object>().Select(Convert.ToUInt32))
            Check(new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Skirmish client extra " + action);
        f.Fate = new(0, default, 0);
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 31 }); f.Frame(0);
        Check(!f.Native.Active && f.Native.SampleRadius == 30, "Skirmish default R30");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 30 }); f.Frame(1);
        Check(f.Native.Active, "Skirmish exact default pull");
        f.Fate = new(f.Native.Definition.FateId, f.Center + Vector3.UnitX * 20, 50); f.Frame(2);
        var center = f.Native.Center;
        f.Fate = f.Fate with { Center = f.Fate.Center + new Vector3(.009f, 0, .009f), Radius = 50.009f }; f.Frame(3);
        Check(f.Native.Center == center && f.Native.SampleRadius == 50, "Skirmish coordinate-wise cache tolerance");
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(3.5);
        f.Actors.Insert(0, new() { Id = 2, Oid = f.Native.Definition.PrimaryOid, Kind = 2, SubKind = 5,
            Position = f.Center, Targetable = true, InCombat = true, Hp = 1000, MaxHp = 1000, FateId = 777 });
        f.Fate = new(777, f.Center, 60); f.Frame(4);
        Check(f.Native.SampleRadius == 60, "Skirmish primary FateID overrides metadata");
        f.Fate = new(f.Native.Definition.FateId, f.Center, 40); f.Frame(5);
        Check(f.Native.Active && f.Native.SampleRadius == 30, "Skirmish reset rebuilds and pulls same frame with default arena");
        f.Change(2, a => a with { FateId = 0 }); f.Frame(6);
        Check(f.Native.SampleRadius == 30, "Skirmish FateID stays original generation value");
        f.Fate = new(777, f.Center, 40); f.Frame(6.5);
        Check(f.Native.Active && f.Native.SampleRadius == 40, "Skirmish original FateID can match again");
        var priorities = new AIHints();
        foreach (var actor in f.Session.World.Actors)
            priorities.PotentialTargets.Add(new(actor, 0, false));
        f.Source!.GetType().GetMethod("CalculateModuleAIHints", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(f.Source, [0, f.Viewer, default(PartyRolesConfig.Assignment), priorities]);
        foreach (var enemy in priorities.PotentialTargets)
            Check(enemy.Priority == f.Native.TargetPriority(f.Actors.Single(a => a.Id == enemy.Actor.InstanceID)),
                "Skirmish actual module priority override");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 50 }); f.Frame(7);
        Check(f.Native.Active, "Skirmish exact R+10 remains");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 50.01f }); f.Frame(8);
        Check(!f.Native.Active, "Skirmish leaves R+10 resets");
    }

    private static void SkirmishStacks(int index)
    {
        using var f = new SkirmishFixture(index);
        var rule = f.Native.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Stack);
        f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], 1) }); f.Frame(1);
        f.Change(107, a => a with { Dead = true }); f.Frame(2);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stack!" && h.Risky), "Skirmish seven alive risky");
        f.Change(107, a => a with { Dead = false }); f.Frame(3);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stack!" && !h.Risky), "Skirmish eight alive satisfied");
        f.Change(100, a => a with { Dead = true }); f.Frame(4);
        Check(f.Native.Stacks(rule).Length == 0 && f.Native.Casts.Count == 1, "Skirmish dead target hidden original retained");
        f.Change(100, a => a with { Dead = false }); f.Frame(5);
        Check(f.Native.Stacks(rule).Length == 1, "Skirmish original target revive");
        f.Party.RemoveAll(p => p.Slot > 0); f.Frame(6);
        Check(!f.Native.NavigationForHost(f.NativeViewer).Any(), "Skirmish fewer than two live Party no stack navigation");
        f.Change(20, a => a with { Cast = null }); f.Frame(7);
        Check(f.Native.NumFinishedStacks == 1, "Skirmish independent stack counter");
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], 7.1, 101) }); f.Frame(7.1);
        var original = f.Native.Casts.Single().Target!;
        f.Actors.RemoveAll(a => a.Id == 100); f.Frame(7.2);
        Check(f.Native.Active && f.Native.NumFinishedStacks == 1 && ReferenceEquals(f.Native.Casts.Single().Target, original),
            "Skirmish missing Party0 actor pauses manager without reset");
        f.Change(20, a => a with { Cast = null }); f.Frame(7.3);
        Check(f.Native.NumFinishedStacks == 2, "Skirmish real Finish still consumed without viewer");
        f.Actors.Add(new() { Id = 100, Kind = 1, Position = f.Center, Hp = 1000 }); f.Frame(7.4);
        f.Party.Clear(); f.Frame(7.5);
        Check(f.Native.Active && f.Native.NumFinishedStacks == 2, "Skirmish absent Party0 slot also pauses manager");
        f.Party.Add(new(0, "", 100)); f.Frame(7.6);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], 8, 999) }); f.Frame(8);
        Check(f.Native.Casts.Count == 0, "Skirmish missing target no stack");
    }

    private static void SkirmishStackReferences(int index)
    {
        using var f = new SkirmishFixture(index);
        var rule = f.Native.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Stack);
        f.Frame(0);
        f.Actors.Add(new() { Id = 21, Oid = 0x115F, Kind = 2, SubKind = 5, Position = f.Center, Hp = 1000 });
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], 1, 101) }); f.Frame(1);
        var first = f.Native.Casts.Single();
        f.Change(21, a => a with { Cast = f.Spell(rule.Actions[0], 2, 101, 8) }); f.Frame(2);
        Check(f.Native.Casts.Count == 2 && f.Native.NavigationForHost(f.NativeViewer).Single().Activation == first.Activation,
            "Skirmish duplicate target retains FIFO and first clock");
        f.Actors.RemoveAll(a => a.Id == 101); f.Frame(3);
        f.Actors.Add(new() { Id = 101, Kind = 1, Position = f.Center + Vector3.UnitX * 20, Hp = 1000 }); f.Frame(4);
        Check(f.Native.Casts.All(c => ReferenceEquals(c.Target, first.Target))
            && !ReferenceEquals(first.Target, f.Native.Actors[101]), "Skirmish target replacement cannot steal original reference");
        // Finish from the second caster consumes the first matching target, not its own cast.
        f.Change(21, a => a with { Cast = null }); f.Frame(5);
        Check(f.Native.NumFinishedStacks == 1 && f.Native.Casts.Count == 1 && !ReferenceEquals(f.Native.Casts[0], first),
            "Skirmish cross-caster Finish removes first matching target");
        f.Change(20, a => a with { Cast = null }); f.Frame(6);
        f.Party.RemoveAll(p => p.Slot == 1);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], 7, 101) }); f.Frame(7);
        Check(f.Native.Casts.Count == 1, "Skirmish target outside Party still stored");
        f.Party.Add(new(1, "", 101));
        f.Change(21, a => a with { Cast = f.Spell(rule.Actions[0], 8, 102) }); f.Frame(8);
        Check(f.Native.Casts.Count == 2, "Skirmish same-frame Party commit and hot Start consumed");
    }

    private static void SkirmishResetOrder()
    {
        using var f = new SkirmishFixture(35);
        f.Change(1, a => a with { FateId = 777 });
        f.Actors.Insert(1, new() { Id = 2, Oid = BozjaCatalog.Definitions[36].PrimaryOid, Kind = 2, SubKind = 5,
            Position = f.Center, Targetable = true, InCombat = true, Hp = 1000, FateId = 777 });
        f.Fate = new(777, f.Center, 40); f.Frame(0);
        void CompareOrder()
        {
            Check(f.Session.BossModules.LoadedModules.Select(m => m.PrimaryActor.OID)
                .SequenceEqual(f.Region.Loaded.Select(m => m.Definition.PrimaryOid)), "Skirmish actual manager Loaded order");
            Check(f.Session.BossModules.ActiveModule?.PrimaryActor.OID == f.Region.Selected?.Definition.PrimaryOid,
                "Skirmish actual manager Selected tie priority");
        }
        CompareOrder();
        f.Change(20, a => a with { Cast = f.Spell(21315, 1) }); f.Frame(1);
        var versions = f.Region.Loaded.Select(m => m.LoadVersion).ToArray();
        f.Fate = new(1605, f.Center, 40); f.Frame(2);
        CompareOrder();
        Check(f.Region.Loaded.All(m => m.Active) && f.Native.Casts.Count == 1
            && f.Region.Loaded.Select(m => m.LoadVersion).Zip(versions).All(v => v.First > v.Second),
            "Skirmish both modules reset then cold-replay in same frame");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 41 }); f.Frame(3);
        CompareOrder();
        versions = f.Region.Loaded.Select(m => m.LoadVersion).ToArray();
        f.Frame(4); CompareOrder();
        Check(f.Region.Loaded.All(m => !m.Active)
            && f.Region.Loaded.Select(m => m.LoadVersion).SequenceEqual(versions), "Skirmish outside R+10 converges without repeated resets");
    }

    private static void SkirmishPartyGate()
    {
        using var f = new SkirmishFixture(35);
        var boss = f.Actors[0];
        f.Actors.RemoveAt(0); f.Party.Clear(); f.Frame(0);
        f.Actors.Insert(0, boss); f.Frame(1);
        Check(f.Session.BossModules.LoadedModules.Count == 0 && f.Session.BossModules.PendingModules.Count == 1
            && f.Region.Loaded.Count == 0 && f.Region.Selected == null, "Skirmish no Party0 creates pending, not selected");
        f.Party.Add(new(0, "", 101)); f.SourcePlayerId = 999; f.Frame(2);
        Check(f.Native.Active && f.Region.Selected == f.Native && f.Session.BossModules.ActiveModule == f.Source,
            "Skirmish actual Party0 promotes and selects without source actor");

        using var oldParty = new SkirmishFixture(36);
        boss = oldParty.Actors[0]; oldParty.Actors.RemoveAt(0); oldParty.Frame(0);
        oldParty.Party.Clear(); oldParty.Actors.Insert(0, boss); oldParty.Frame(1);
        Check(oldParty.Source != null && oldParty.Native.Primary != null && !oldParty.Native.Active
            && oldParty.Region.Selected == null && oldParty.Session.BossModules.ActiveModule == null,
            "Skirmish Created consumes old Party0, manager selection consumes newly committed empty Party");
    }
}
