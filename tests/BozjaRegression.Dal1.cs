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
    private static void RunDal1(Action<string, Action> run)
    {
        run("Bozja native DAL1 client thirteen actual components square arena pull", Dal1Client);
        foreach (var rule in BozjaCatalog.Definitions[28].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
            run("Bozja native DAL1 standard raw Start Finish hidden queue " + rule.Component, () => Dal1Standard(rule));
        run("Bozja native DAL1 damage dynamic NPC raw original caster", () => Damage(28));
        run("Bozja native DAL1 stack spread original target generation duplicate target Finish dead hide min8", Dal1Targets);
        run("Bozja native DAL1 TimeEruption threshold4 RefSort hidden2 promotion any Finish", Dal1Time);
        run("Bozja native DAL1 Created towers quantized snapshot first spatial Effect no TTL generations", Dal1Towers);
        run("Bozja native DAL1 full Feed thirteen components cold Created old world target stack spread clocks", Dal1Feed);
    }

    private static void Dal1Client()
    {
        using var f = new Fixture(28);
        Check(f.M.Definition.Rules.Length == 13, "DAL1 actual components");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(975)?.ContentFinderCondition.RowId == 778
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(778)?.TerritoryType.RowId == 975, "DAL1 CFC territory");
        var name = Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9384)?.Singular.ToString();
        Check(name == f.M.Definition.Name, "DAL1 actual client name " + name);
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.TheDalriada.DAL1Sartauvoir.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DAL1 client AID " + action);
        for (var x = -21f; x <= 21; x += .417f)
            for (var z = -21f; z <= 21; z += .389f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DAL1 source square");
            }
        f.M.Clear();
        f.Move(100, f.Center + Vector3.UnitX * 100);
        Check(f.M.Active, "DAL1 default CheckPull has no invented arena range");
        Check(BozjaCatalog.Owns(975, 0x3250) && !BozjaCatalog.Owns(920, 0x3250), "DAL1 exact ownership");
    }

    private static void Dal1Standard(BozjaRule rule)
    {
        foreach (var action in rule.Actions)
        {
            using var f = new Fixture(28);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, action);
            RobotCompare(f, source, rule.Component, "DAL1 Start " + action);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            RobotCompare(f, source, rule.Component, "DAL1 duplicate " + action);
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action)); f.M.Effect(2, action);
            RobotCompare(f, source, rule.Component, "DAL1 Effect retains " + action);
            f.Move(2, f.Center + Vector3.UnitX * 22);
            f.Now = START.AddSeconds(80); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            RobotCompare(f, source, rule.Component, "DAL1 locked no TTL " + action);
            var other = new ActorCastInfo { Action = new(ActionType.Spell, 6497) };
            source.OnCastFinished(f.W.Actors.Find(2)!, other); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 6497 });
            RobotCompare(f, source, rule.Component, "DAL1 unrelated Finish " + action);
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            RobotCompare(f, source, rule.Component, "DAL1 watched Finish " + action);
        }
    }

    private static void Dal1Time()
    {
        using var f = new Fixture(28);
        var source = f.Activate<SimpleAOEGroups>("TimeEruption");
        var starts = new List<ActorCastInfo>();
        for (var i = 0; i < 5; ++i)
        {
            f.Add((ulong)(20 + i), 0x233C, f.Center + Vector3.UnitX * (i - 2)); f.Sync();
            starts.Add(f.Start(source, i % 2 == 0 ? 24174u : 24177u, (ulong)(20 + i), i == 0 ? 6 : 2));
            RobotCompare(f, source, "TimeEruption", "DAL1 sort count " + (i + 1));
            Check(f.M.Visible().Count() == Math.Min(i + 1, 2), "DAL1 exactly max2 visible");
        }
        var first = source.Casters[0].ActorID;
        source.OnCastFinished(f.W.Actors.Find(first)!, new() { Action = new(ActionType.Spell, 6497) });
        f.M.Finish(f.M.Actors[first], f.M.Actors[first].Cast! with { ActionId = 6497 });
        RobotCompare(f, source, "TimeEruption", "DAL1 hidden promotion first unrelated Finish");
        Check(source.Casters.Count == 4 && f.M.Casts.Count == 4, "DAL1 hidden queue retained");
    }

    private static void Dal1Targets()
    {
        foreach (var (name, action, stack) in new[] { ("Pyrodoxy", 24206u, true), ("Pyrocrisis", 24207u, false) })
        {
            using var f = new Fixture(28);
            var rule = f.M.Definition.Rules.Single(r => r.Component == name);
            var source = f.Activate<CastStackSpread>(name);
            var spell = f.Start(source, action);
            f.Add(3, 0x233C, f.Center); f.Sync(); f.Start(source, action, 3, 8);
            var original = f.M.Casts[0].Target!;
            void Compare(string step)
            {
                var expected = stack ? source.ActiveStacks.ToArray().Select(s => (s.Target.Position.ToVec3(), s.Activation, s.Radius))
                    : source.ActiveSpreads.ToArray().Select(s => (s.Target.Position.ToVec3(), s.Activation, s.Radius));
                var actual = stack ? f.M.Stacks(rule) : f.M.Spreads(rule).ToArray();
                Check(expected.SequenceEqual(actual.Select(s => (s.Target!.Actor.Position, s.Activation, 6f))), "DAL1 " + name + "/" + step);
                var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                    "DAL1 target hints " + step);
            }
            Compare("duplicate target");
            if (stack) Check(source.Stacks.All(s => s.MinSize == 8) && rule.MinStackSize == 8, "DAL1 min8");
            f.Move(100, f.Center + Vector3.UnitZ * 5); Compare("dynamic original target");
            f.Set(100, a => a with { Dead = true }); f.Viewer.IsDead = true; f.Sync(); Compare("dead hidden");
            f.Set(100, a => a with { Dead = false }); f.Viewer.IsDead = false; f.Sync(); Compare("revive");
            source.OnCastFinished(f.W.Actors.Find(3)!, f.W.Actors.Find(3)!.CastInfo!);
            f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!); Compare("first target consumed");
            Check(f.M.Casts.Single().Source.Actor.Id == 3, "DAL1 Finish uses first target not caster");
            f.Remove(100); f.Add(100, 0, f.Center + Vector3.UnitZ * -10, ActorType.Player); f.Sync(); Compare("original destroyed not rebound");
            Check(original.Destroyed && ReferenceEquals(original, f.M.Casts.Single().Target), "DAL1 original identity");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Check(f.M.Casts.Count == 0, "DAL1 final target Finish");
            f.Start(source, action, target: 999); f.Add(999, 0, f.Center, ActorType.Player); f.Sync();
            Check(f.M.Casts.Count == 0, "DAL1 missing target no late fabrication");
        }
    }

    private static void Dal1Towers()
    {
        foreach (var cold in new[] { false, true })
        {
            using var f = new Fixture(28);
            if (cold)
            {
                f.Add(20, 0x1EB214, f.Center + new Vector3(5.12345f, 0, 1.23456f)); f.Sync();
                f.Now = START.AddSeconds(12); f.W.Frame.Timestamp = f.Now;
                f.M.Clear(); f.Sync();
            }
            var source = f.Activate<GenericTowersOpenWorld>("Pyroclysm");
            if (!cold) { f.Add(20, 0x1EB214, f.Center + new Vector3(5.12345f, 0, 1.23456f)); f.Sync(); }
            f.Add(21, 0x1EB214, f.Center + new Vector3(5.12345f, 0, 1.23456f)); f.Sync();
            f.Add(22, 0x1EB213, f.Center); f.Sync();
            f.Add(23, 0x1EB214, f.Center, ActorType.Player); f.Sync();
            Check(source.Towers.Count == 2 && f.M.Towers.Length == 2, "DAL1 hot Player same OID excluded");
            void Compare(string step, ulong viewerId = 100)
            {
                var viewer = f.W.Actors.Find(viewerId)!;
                var native = f.M.Actors[viewerId];
                Check(source.Towers.Select(t => (t.Position.ToVec3(), t.Activation))
                    .SequenceEqual(f.M.Towers.Select(t => (t.Origin, t.Activation))), step + " raw snapshot order clock");
                var hints = new BossComponent.TextHints(); source.AddHints(0, viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(native).Select(h => (h.Text, h.Risky))), step + " hints");
                for (var i = 0; i < source.Towers.Count; ++i)
                {
                    Check(source.Towers[i].NumInside(f.Source) == f.M.NumInside(f.M.Towers[i]), step + " lazy original count");
                    Check(source.Towers[i].AllowedSoakers!.Select(a => a.InstanceID).Order()
                        .SequenceEqual(f.M.Soakers(f.M.Towers[i]).Select(a => a.Actor.Id).Order()), step + " lazy exact OID0 members");
                }
                var ai = new AIHints(); source.AddAIHints(0, viewer, default, ai);
                var zones = f.M.Navigation(native).ToArray();
                Check(ai.ForbiddenZones.Count == zones.Length, step + " zones");
                for (var i = 0; i < zones.Length; ++i)
                    for (var x = -18f; x < 18; x += .921f)
                        for (var z = -18f; z < 18; z += 1.317f)
                        {
                            var p = f.Center + new Vector3(x, 0, z);
                            var zone = zones[i];
                            var actualDistance = zone.Join != null ? zone.Distance(p.X, p.Z)
                                : Vector2.Distance(new(p.X, p.Z), new(zone.Origin.X, zone.Origin.Z)) - 4;
                            Check(ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z)) == actualDistance, step + " exact signed geometry");
                        }
                Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation))
                    .SequenceEqual(f.M.Damage(native).Select(d => (d.Mask, d.Activation))), step + " damage");
                var views = f.M.TowerViews(native).ToArray();
                Check(views.Length == source.Towers.Count, step + " tower display count");
            }
            Check(f.M.Towers.All(t => t.AllowedSoakers == null), "DAL1 lazy at Created");
            Compare("DAL1 " + cold + " initial");
            var ignoredCast = f.Start(source, 24184);
            source.OnCastFinished(f.W.Actors.Find(2)!, ignoredCast); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Compare("DAL1 cast Start Finish do not consume Created");
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(24184));
            f.M.Effect(2, 24184, position: f.M.Towers[0].Origin);
            Compare("DAL1 far caster ignores target location");
            f.Add(102, 0, f.Center + Vector3.UnitX * 5, ActorType.Player); f.Sync();
            Compare("DAL1 late forbidden", 102);
            f.Move(100, f.M.Towers[0].Origin); Compare("DAL1 one soaking");
            f.Move(101, f.M.Towers[0].Origin); Compare("DAL1 overfilled");
            f.Remove(101); f.Add(101, 0, f.Center + Vector3.UnitX * -15, ActorType.Player); f.Sync();
            Compare("DAL1 destroyed original remains");
            f.Move(20, f.Center + Vector3.UnitX * 17);
            f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            f.Remove(20); Compare("DAL1 Created actor destroy no removal no TTL");
            f.Move(2, f.M.Towers[0].Origin + Vector3.UnitX);
            foreach (var action in new uint[] { 24184, 24183, 24184 })
            {
                source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action)); f.M.Effect(2, action);
                Compare("DAL1 first spatial Effect " + action);
            }
            Check(f.M.Towers.Length == 0, "DAL1 both towers consumed first only");
        }
    }

    private static void Dal1Feed()
    {
        Initialize();
        using var session = new MechanicSession("DAL1-thirteen-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.Definition.Key == "DAL1Sartauvoir");
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x3250, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Targetable = true, InCombat = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * -15, Hp = 1000 },
            new() { Id = 20, Oid = 0x1EB214, Kind = 7, Position = center + Vector3.UnitX * 10, Hp = 1 },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;
        void Frame(double at, uint effect = 0)
        {
            var now = START.AddSeconds(at); session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(),
                [], actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(1, 1, effect, 0, 0, default, 0, 0, 0, []));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () => { if (effect != 0) region.Effect(1, effect); });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x3250);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            Check(source.Components.Count == 13 && source.StateMachine.ActiveState != null && model.Active, "DAL1 actual13 active " + at);
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky))), "DAL1 Feed ordered geometry clocks " + at);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "DAL1 Feed hints " + at);
            var ai = new AIHints();
            foreach (var component in source.Components) component.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DAL1 Feed normalized clocks " + at);
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, model.SampleRadius, out var kind);
                if (kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals)) converted = goals;
                var shapes = zones[i].Convert(center, model.SampleRadius).ToArray();
                Check(converted.Count == shapes.Length, "DAL1 Feed actual conversion " + at + "/" + i);
                for (var x = -18f; x < 18; x += .823f)
                    for (var z = -18f; z < 18; z += 1.317f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                            == shapes.Any(s => s.Contains(p, 0)), "DAL1 Feed converted membership " + at);
                    }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DAL1 Feed damage " + at);
        }
        Frame(0);
        foreach (var (rule, index) in model.Definition.Rules.Where(r => r.Kind != BozjaRuleKind.Tower).Select((r, i) => (r, i)))
            actors.Add(new() { Id = (ulong)(30 + index), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center + Vector3.UnitX * (index % 3), Hp = 1000,
                Cast = new(rule.Actions[0], 100, center + Vector3.UnitX * (index % 3), .37f, START.AddSeconds(1),
                    START.AddSeconds(2.3f + index % 4), false, false) { TotalTime = 1 + index % 4, IsSpell = true } });
        Frame(1); Frame(1.5);
        actors[0] = actors[0] with { Position = center + Vector3.UnitX * 10 };
        Frame(2, 24184);
        actors.RemoveAll(a => a.Id >= 30 && a.Id < 100);
        Frame(3);
        Frame(80);
    }
}
