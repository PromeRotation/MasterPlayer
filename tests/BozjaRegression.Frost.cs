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
    private static void RunFrost(Action<string, Action> run)
    {
        run("Bozja native CE54 client eleven components source watched actions square pull", FrostClient);
        run("Bozja native CE54 six standard watched AOEs Finish no visual substitution", FrostStandard);
        run("Bozja native CE54 Bracing all AI casters first applicable shape immune exact expiry", FrostBracing);
        run("Bozja native CE54 Lunar private pillars frozen geometry removal destroyed generations FIFO", FrostLunar);
        run("Bozja native CE54 Lunar Bracing suppresses only AI even immune", FrostLunarWind);
        run("Bozja native CE54 Thermal Created static clocks global Finish FIFO no Effect TTL", FrostThermal);
        run("Bozja native CE54 Glaciation raw dynamic NPC clock live Party diagnostic only", () => Damage(17));
        run("Bozja native CE54 rotating direction before six angles shared final clock cache per sequence order", () => FrostRotation(false));
        run("Bozja native CE54 rotating direction after six angles shared final clock cache per sequence order", () => FrostRotation(true));
        run("Bozja native CE54 rotating seventh angle prevents init first geometric match wrap and boundary", FrostRotationMatch);
        run("Bozja native CE54 real Feed eleven cold components Lunar actor order", () => FrostFeed(false));
        run("Bozja native CE54 real Feed eleven cold components pillar before boss", () => FrostFeed(true));
        run("Bozja native CE54 real Feed cold PillarPierce removes pillar before Lunar Start", () => FrostFeed(true, true));
        run("Bozja native CE54 real Feed snapshot Finish failure retains cast retries blocks later Created and Destroy", FrostFailure);
        run("Bozja native CE54 real Feed Location and Rotation change trigger Finish then repeated partial failure", FrostCastChange);
        run("Bozja native CE54 Created filters source player pet chocobo buddy hot and cold", FrostActorTypes);
        run("Bozja native CE54 Region failure blocks later Actor in every CE including RawSteel", () => FrostRegionFailure(false));
        run("Bozja native CE54 Region reverse loaded order failed Destroy retains shared Actor across earlier CE53", () => FrostRegionFailure(true));
    }

    private static void FrostClient()
    {
        using var f = new Fixture(17);
        var d = f.M.Definition;
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(25)?.Name.ToString() == d.Name
            && d.Rules.Length == 11 && d.SquarePull && d.PullRadius == 24 && d.Territory == 975, "CE54 exact DE25 eleven");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE54NeverCryWolf.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE54 source AID extra " + action);
        for (var x = -25f; x < 25; x += .43f)
            for (var z = -25f; z < 25; z += .57f)
                Check(f.Source.Arena.InBounds(new(f.Center.X + x, f.Center.Z + z))
                    == f.M.Arena.Contains(f.Center + new Vector3(x, 0, z), 0), "CE54 true square");
        f.Move(100, f.Center + new Vector3(24, 0, 24));
        var corner = new BozjaEncounter(d);
        corner.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
        Check(corner.Active, "CE54 square corner pull");
        f.Move(100, f.Center + new Vector3(24.001f, 0, 0));
        var far = new BozjaEncounter(d);
        far.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
        Check(!far.Active, "CE54 strict square pull");
    }

    private static void FrostStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[17].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(17);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "CE54 duplicate " + rule.Component);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "CE54 Effect no TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "CE54 Finish first");
        }
    }

    private static void CompareBracing(Fixture f, SimpleKnockbacks source, string step)
    {
        var expected = source.CalculateMovements(0, f.Viewer).ToArray();
        var actual = f.M.BracingMovements(f.NativeViewer).ToArray();
        Check(expected.Length == actual.Length, step + " movement count");
        for (var i = 0; i < actual.Length; ++i)
            Check(expected[i].from.ToVec3() == actual[i].From && expected[i].to.ToVec3() == actual[i].To,
                step + " exact movement");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "BracingWind").ToArray(), step);
    }

    private static void FrostBracing()
    {
        using var f = new Fixture(17);
        var source = f.Activate<SimpleKnockbacks>("BracingWind");
        f.Move(2, f.Center + Vector3.UnitX * 20);
        f.Start(source, 23586);
        var (sin, cos) = Math.SinCos(.4f);
        f.Add(3, 0x233C, f.Center - 20 * new Vector3((float)sin, 0, (float)cos)); f.Sync();
        f.Start(source, 23586, 3);
        CompareBracing(f, source, "CE54 first misses second applies");
        Check(source.Casters.Count == 2 && f.M.BracingMovements(f.NativeViewer).Count() == 1,
            "CE54 max1 applies after shape");
        var activation = f.M.Casts.First(c => c.Rule.Component == "BracingWind").Activation;
        foreach (var extra in new[] { 0d, .01d })
        {
            var expires = activation.AddSeconds(extra);
            var param = (ushort)(extra == 0 ? 0 : 1);
            var status = new ActorStatus(1209, param, expires, 100);
            source.OnStatusGain(f.Viewer, ref status);
            f.Set(100, a => a with { Statuses = [new(1209, 100, param, expires) { SlotIndex = 59 }] }); f.Sync();
            CompareBracing(f, source, "CE54 immunity " + extra);
            Check(f.M.BracingMovements(f.NativeViewer).Count() == (extra == 0 ? 1 : 0), "CE54 immunity strictly later");
        }
    }

    private static void FrostLunar()
    {
        using var f = new Fixture(17);
        var pillar = Crystal(f, 30, 0x319E, f.Center + Vector3.UnitZ * 5, true);
        var source = f.Activate<CastLineOfSightAOE>("LunarCry");
        var first = f.Start(source, 23588, 1, 14);
        CompareMeteor(f, source, "CE54 dead pillar accepted", "LunarCry");
        f.Move(30, f.Center + Vector3.UnitX * 15);
        f.Remove(30);
        Crystal(f, 30, 0x319E, f.Center - Vector3.UnitZ * 5);
        CompareMeteor(f, source, "CE54 old frozen safe shape retained", "LunarCry");
        var second = f.Start(source, 23588, 2, 10);
        CompareMeteor(f, source, "CE54 destroyed old pillar and same-ID new reference", "LunarCry");
        source.OnCastFinished(f.W.Actors.Find(2)!, second);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMeteor(f, source, "CE54 later caster Finish pops first safezone", "LunarCry");
        f.Start(source, 23583, 30);
        CompareMeteor(f, source, "CE54 PillarPierce removal does not rebuild old safezone", "LunarCry");
        source.OnCastFinished(f.W.Actors.Find(1)!, first);
        f.M.Finish(f.M.Actors[1], f.M.Actors[1].Cast!);
        CompareMeteor(f, source, "CE54 all safezones consumed", "LunarCry");
        Check(pillar.IsDestroyed, "CE54 old pillar original reference destroyed");
    }

    private static void FrostLunarWind()
    {
        using var f = new Fixture(17);
        Crystal(f, 30, 0x319E, f.Center + Vector3.UnitZ * 5);
        var wind = f.Activate<SimpleKnockbacks>("BracingWind");
        var source = f.Activate<CastLineOfSightAOE>("LunarCry");
        f.Start(source, 23588, 1, 14);
        var spell = f.Start(wind, 23586);
        CompareMeteor(f, source, "CE54 wind suppresses Lunar AI but not geometry text", "LunarCry");
        var immunity = new ActorStatus(1209, 0, START.AddSeconds(100), 100);
        wind.OnStatusGain(f.Viewer, ref immunity);
        f.Set(100, a => a with { Statuses = [new(1209, 100, 0, START.AddSeconds(100)) { SlotIndex = 59 }] }); f.Sync();
        CompareMeteor(f, source, "CE54 immune wind still suppresses Lunar AI", "LunarCry");
        Check(source.ActiveAOEs(0, f.Viewer).Length == 1 && f.M.BracingMovements(f.NativeViewer).Count() == 0,
            "CE54 immune does not suppress displayed safezone");
        wind.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMeteor(f, source, "CE54 Finish restores Lunar navigation", "LunarCry");
    }

    private static void FrostThermal()
    {
        using var f = new Fixture(17);
        var source = f.Activate<GenericAOEs>("ThermalGust");
        f.Add(30, 0x319F, f.Center + new Vector3(1.123f, 0, 2.456f)); f.Sync();
        f.Now = START.AddSeconds(2); f.W.Frame.Timestamp = f.Now;
        f.Add(31, 0x319F, f.Center - Vector3.UnitX * 10); f.Sync();
        CompareMetalFoxBits(f, source, "ThermalGust", "CE54 Created two original clocks");
        f.Move(30, f.Center + Vector3.UnitX * 20); f.Remove(30);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23589)); f.M.Effect(1, 23589);
        CompareMetalFoxBits(f, source, "ThermalGust", "CE54 no move destruction Effect TTL");
        var spell = f.Start(source, 23589, 2);
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ThermalGust", "CE54 arbitrary caster first FIFO");
    }

    private static void FrostRotation(bool directionLast)
    {
        using var cold = new Fixture(17);
        var component = cold.Activate<GenericRotatingAOE>("AgeOfEndlessFrost");
        if (!directionLast) cold.Start(component, 23590, 1);
        for (var i = 0; i < 6; ++i) FrostStartAngle(cold, component, (ulong)(30 + i), i * .7f, i);
        if (directionLast) cold.Start(component, 23591, 1);
        component.Update(); cold.Sync();
        CompareMetalFoxBits(cold, component, "AgeOfEndlessFrost", "CE54 six sequence publication", Colors.AOE);
        Check(component.Sequences.Count == 6 && component.ActiveAOEs(0, cold.Viewer).Length == 12, "CE54 six sequences two each");
        cold.Now = START.AddSeconds(100); cold.W.Frame.Timestamp = cold.Now; component.Update(); cold.Sync();
        CompareMetalFoxBits(cold, component, "AgeOfEndlessFrost", "CE54 unchanged version cache across clock", Colors.AOE);
        var sequence = component.Sequences[0];
        FrostFinishAngle(cold, component, sequence.Origin.ToVec3(), sequence.Rotation.Rad);
        CompareMetalFoxBits(cold, component, "AgeOfEndlessFrost", "CE54 source Finish advances first", Colors.AOE);
    }

    private static void FrostStartAngle(Fixture f, GenericRotatingAOE source, ulong id, float rotation, int offset = 0)
    {
        if (f.W.Actors.Find(id) == null) { f.Add(id, 0x233C, f.Center + new Vector3(offset, 0, -offset)); f.Sync(); }
        var actor = f.W.Actors.Find(id)!;
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 23592), TotalTime = 3 + offset,
            Location = actor.Position.ToVec3(), Rotation = new(rotation) };
        actor.CastInfo = spell;
        source.OnCastStarted(actor, spell);
        f.Set(id, a => a with { Cast = new(23592, 0, a.Position, rotation, f.Now,
            f.Now.AddSeconds(3 + offset + .3f), false, false) { TotalTime = 3 + offset, IsSpell = true } });
        f.Sync();
    }

    private static void FrostFinishAngle(Fixture f, GenericRotatingAOE source, Vector3 location, float rotation)
    {
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 23593), Location = location, Rotation = new(rotation) };
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], new(23593, 0, location, rotation, f.Now, f.Now, false, false));
        source.Update(); f.Sync();
    }

    private static void FrostRotationMatch()
    {
        using var f = new Fixture(17);
        var source = f.Activate<GenericRotatingAOE>("AgeOfEndlessFrost");
        for (var i = 0; i < 7; ++i) FrostStartAngle(f, source, (ulong)(30 + i), .2f);
        f.Start(source, 23590, 1); source.Update(); f.Sync();
        Check(source.Sequences.Count == 0 && !f.M.Visible().Any(a => a.Cast.Rule.Component == "AgeOfEndlessFrost"),
            "CE54 seventh angle before direction does not initialize");
        using var g = new Fixture(17);
        var other = g.Activate<GenericRotatingAOE>("AgeOfEndlessFrost");
        g.Start(other, 23591, 1);
        for (var i = 0; i < 6; ++i) FrostStartAngle(g, other, (ulong)(30 + i), MathF.PI - .01f);
        other.Update(); g.Sync();
        FrostFinishAngle(g, other, g.Center + Vector3.UnitX * 1.001f, -MathF.PI + .01f);
        Check(other.NumCasts == 0 && g.M.NumFrostCasts == 0, "CE54 strict axis tolerance");
        FrostFinishAngle(g, other, g.Center + Vector3.UnitX, -MathF.PI + .01f);
        Check(other.NumCasts == 1 && g.M.NumFrostCasts == 1, "CE54 wrapped angle first matching sequence");
        CompareMetalFoxBits(g, other, "AgeOfEndlessFrost", "CE54 first duplicate match", Colors.AOE);
        for (var i = 0; i < 6; ++i)
        {
            var seq = other.Sequences[0];
            FrostFinishAngle(g, other, seq.Origin.ToVec3(), seq.Rotation.Rad);
            CompareMetalFoxBits(g, other, "AgeOfEndlessFrost", "CE54 last imminent future colour " + i, Colors.AOE);
        }
        using var h = new Fixture(17);
        var boundary = h.Activate<GenericRotatingAOE>("AgeOfEndlessFrost");
        h.Start(boundary, 23591, 1);
        for (var i = 0; i < 6; ++i) FrostStartAngle(h, boundary, (ulong)(30 + i), 0);
        boundary.Update(); h.Sync();
        FrostFinishAngle(h, boundary, h.Center, .05f);
        Check(boundary.NumCasts == 1 && h.M.NumFrostCasts == 1, "CE54 exact angular tolerance included");
        FrostFinishAngle(h, boundary, h.Center, .0501f);
        Check(boundary.NumCasts == 1 && h.M.NumFrostCasts == 1, "CE54 outside angular tolerance excluded");
        CompareMetalFoxBits(h, boundary, "AgeOfEndlessFrost", "CE54 angular boundary", Colors.AOE);
    }

    private static void FrostFeed(bool pillarFirst, bool pierce = false)
    {
        Initialize();
        using var session = new MechanicSession("CE54-cold-order-" + pillarFirst);
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[17]);
        var center = model.Center;
        var primary = new ReplayActorState { Id = 1, Oid = 0x319C, Kind = 2, SubKind = 5,
            Position = center, Hp = 1000, InCombat = true, Targetable = true,
            Cast = new(23588, 0, center, 0, START, START.AddSeconds(14.3f), false, false) { TotalTime = 14, IsSpell = true } };
        var pillar = new ReplayActorState { Id = 30, Oid = 0x319E, Kind = 2, SubKind = 5,
            Position = center + Vector3.UnitZ * 5, Hp = 1000, HitboxRadius = 3,
            Cast = pierce ? new(23583, 0, center + Vector3.UnitZ * 5, 0, START, START.AddSeconds(3.3f), false, false)
                { TotalTime = 3, IsSpell = true } : null };
        List<ReplayActorState> actors = [];
        actors.AddRange(pillarFirst ? [pillar, primary] : [primary, pillar]);
        actors.AddRange([
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 10, Hp = 1000 },
            new() { Id = 40, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000 }]);
        uint[] actions = [23582, 23583, 23584, 23586, 23590, 23592, 23592, 23592, 23592, 23592, 23592, 23594, 23596, 23601, 23602];
        for (var i = 0; i < actions.Length; ++i)
            actors.Add(new()
            {
                Id = (ulong)(1000 + i), Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 1000,
                Position = center - Vector3.UnitZ * 20,
                Cast = new(actions[i], 100, center - Vector3.UnitZ * 20, i * .31f, START,
                    START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true },
            });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        session.BeginFrame(START);
        feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
            actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, START])!).ToArray()));
        feed.Flush(); session.Update();
        model.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, START);
        var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x319C);
        Check(module.Components.Count() == 11 && model.Active, "CE54 eleven actual source components");
        var lunar = module.Components.OfType<CastLineOfSightAOE>().Single();
        Check(lunar.ActiveAOEs(0, session.World.Actors.Find(100)!).Length == (pillarFirst && !pierce ? 1 : 0)
            && model.Visible().Count(a => a.Cast.Rule.Component == "LunarCry") == (pillarFirst && !pierce ? 1 : 0),
            "CE54 cold Created Start order no retroactive blocker");
        Check(module.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "ThermalGust")
            .ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray().Single().Activation == model.ThermalGusts.Single().Activation,
            "CE54 cold Thermal current clock");
        var ai = new AIHints();
        foreach (var component in module.Components)
            component.AddAIHints(0, session.World.Actors.Find(100)!, default, ai);
        ai.Normalize();
        var zones = model.NavigationForHost(model.Actors[100]);
        Check(ai.ForbiddenZones.Count == zones.Length && ai.ForbiddenZones.Select(z => z.Item2)
            .SequenceEqual(zones.Select(z => z.Activation)), "CE54 eleven component merged normalized clocks");
        for (var i = 0; i < zones.Length; ++i)
        {
            var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, 24, out _);
            var actual = zones[i].Convert(center, 24).ToArray();
            Check(expected.Count == actual.Length, "CE54 merged true conversion count " + i);
            for (var x = -24f; x < 24; x += 1.31f)
                for (var z = -24f; z < 24; z += 1.67f)
                {
                    var p = center + new Vector3(x, 0, z);
                    Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                        == actual.Any(s => s.Contains(p, 0)), "CE54 merged true conversion geometry " + i);
                }
        }
        Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
            .SequenceEqual(model.DamageForHost(model.Actors[100]).Select(d => (d.Mask, d.Activation, d.Type))),
            "CE54 real Feed merged diagnostic");
    }

    private static void FrostFailure()
    {
        Initialize();
        using var session = new MechanicSession("CE54-snapshot-failure");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[17]);
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x319C, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Targetable = true, InCombat = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 40, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000 },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var step = 0; step < 8; ++step)
        {
            if (step is 1 or 3)
                actors[2] = actors[2] with { Cast = new(23589, 0, center, 0, START.AddSeconds(step),
                    START.AddSeconds(step + 2.3f), false, false) { TotalTime = 2, IsSpell = true } };
            if (step is 2 or 4) actors[2] = actors[2] with { Cast = null };
            if (step == 4) actors.Add(new() { Id = 41, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000 });
            if (step == 6) actors.RemoveAt(2);
            if (step == 7)
            {
                actors.Insert(2, new() { Id = 42, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000 });
                actors.Insert(3, new() { Id = 40, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000 });
            }
            var now = START.AddSeconds(step);
            session.BeginFrame(now);
            var sourceFailed = false;
            try
            {
                feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush(); session.Update();
            }
            catch (ArgumentOutOfRangeException) { sourceFailed = true; }
            var nativeFailed = false;
            try { model.Synchronize(actors, [new(0, "", 100)], 100, now); }
            catch (ArgumentOutOfRangeException) { nativeFailed = true; }
            Check(sourceFailed == (step is >= 4 and <= 6) && nativeFailed == sourceFailed, "CE54 real snapshot failure " + step);
            if (step == 7)
            {
                var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x319C)
                    .Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "ThermalGust");
                Check(source.ActiveAOEs(0, session.World.Actors.Find(100)!).Length == 1
                    && model.ThermalGusts.Single().Source.Actor.Id == 41 && model.ThermalSequenceError == null
                    && session.World.Actors.Find(40)?.CastInfo == null && model.Actors[40].Cast == null,
                    "CE54 earlier real Created unblocks old Finish then later Created commits and clears snapshot error");
                continue;
            }
            if (step < 4) continue;
            Check(session.World.Actors.Find(40)?.CastInfo?.Action.ID == 23589 && model.Actors[40].Cast?.ActionId == 23589
                && session.World.Actors.Find(41) == null && !model.Actors.ContainsKey(41)
                && model.ThermalSequenceError != null, "CE54 partial commit retains old cast and blocks later actor " + step);
            if (step == 6)
                Check(session.World.Actors.Find(40)!.IsDestroyed && model.Actors[40].Destroyed,
                    "CE54 failed Destroy marks original reference before Finish");
        }
    }

    private static void FrostActorTypes()
    {
        foreach (var hot in new[] { false, true })
        {
            using var f = new Fixture(17);
            GenericAOEs? thermal = null;
            CastLineOfSightAOE? lunar = null;
            if (hot)
            {
                thermal = f.Activate<GenericAOEs>("ThermalGust");
                lunar = f.Activate<CastLineOfSightAOE>("LunarCry");
            }
            foreach (var (kind, subKind) in new (byte, byte)[] { (1, 4), (2, 2), (2, 3), (2, 9) })
                foreach (var oid in new[] { 0x319Eu, 0x319Fu })
                {
                    var id = (ulong)(1000 + f.Actors.Count);
                    var pos = f.Center + Vector3.UnitZ * 5;
                    f.W.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0,
                        (ActorType)(kind << 8 | subKind), Class.PLD, 100, new(pos, 0), 3, default, true, false, 0, 0, 0));
                    f.Actors.Add(new() { Id = id, Oid = oid, Kind = kind, SubKind = subKind, Position = pos, Hp = 1000, HitboxRadius = 3 });
                    f.Sync();
                }
            if (!hot)
            {
                thermal = f.Activate<GenericAOEs>("ThermalGust");
                lunar = f.Activate<CastLineOfSightAOE>("LunarCry");
            }
            f.Start(lunar!, 23588);
            CompareMeteor(f, lunar!, "CE54 non-native Created filter " + hot, "LunarCry");
            Check(thermal!.ActiveAOEs(0, f.Viewer).Length == 0 && f.M.ThermalGusts.Count == 0,
                "CE54 hot and cold source type exclusions");
        }
    }

    private static void FrostCastChange()
    {
        Initialize();
        using var session = new MechanicSession("CE54-location-rotation-finish");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[17]);
        var center = model.Center;
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var step = 0; step < 4; ++step)
        {
            var location = step == 0 ? center : center + Vector3.UnitX * .5f;
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = 0x319C, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Targetable = true, InCombat = true },
                new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
                new() { Id = 40, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000,
                    Cast = new(23589, 0, location, step < 2 ? 0 : .2f, START, START.AddSeconds(2.3f), false, false)
                        { TotalTime = 2, IsSpell = true } },
            ];
            var now = START.AddSeconds(step);
            session.BeginFrame(now);
            var sourceFailed = false;
            try
            {
                feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush(); session.Update();
            }
            catch (ArgumentOutOfRangeException) { sourceFailed = true; }
            var nativeFailed = false;
            try { model.Synchronize(actors, [new(0, "", 100)], 100, now); }
            catch (ArgumentOutOfRangeException) { nativeFailed = true; }
            Check(sourceFailed == (step >= 2) && nativeFailed == sourceFailed, "CE54 true Location/Rotation finish " + step);
            if (step >= 1)
                Check(model.ThermalGusts.Count == 0 && model.Actors[40].Cast!.Location == location
                    && model.Actors[40].Cast!.Rotation == 0 && session.World.Actors.Find(40)!.CastInfo!.Rotation.Rad == 0,
                    "CE54 first location committed then failed rotation retains old cast");
        }
    }

    private static void FrostRegionFailure(bool reverse)
    {
        Initialize();
        using var session = new MechanicSession("CE54-region-failure");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var center = BozjaCatalog.Definitions[17].Center;
        var cavalryCenter = BozjaCatalog.Definitions[16].Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x319C, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Targetable = true, InCombat = true },
            new() { Id = 2, Oid = 0x31C7, Kind = 2, SubKind = 5, Position = cavalryCenter, Hp = 1000, Targetable = true, InCombat = true },
            new() { Id = 100, Kind = 1, Position = reverse ? cavalryCenter : center, Hp = 1000 },
            new() { Id = 40, Oid = 0x319F, Kind = 2, SubKind = 5, Position = center, Hp = 1000 },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var step = 0; step < 7; ++step)
        {
            if (step == 1) actors[2] = actors[2] with { Position = reverse ? center : cavalryCenter };
            if (step is 1 or 3)
                actors[3] = actors[3] with { Cast = new(23589, 0, center, 0, START.AddSeconds(step),
                    START.AddSeconds(step + 2.3f), false, false) { TotalTime = 2, IsSpell = true } };
            if (step == 3) actors[3] = actors[3] with { Tethers = [new(57, 100)] };
            if (step is 2 or 4) actors[3] = actors[3] with { Cast = null };
            if (step == 4)
                actors.Add(new() { Id = 200, Oid = 0x233C, Kind = 2, SubKind = 5,
                    Position = cavalryCenter, Hp = 1000, Tethers = [new(57, 100)] });
            if (step == 5) actors.RemoveAt(3);
            var now = START.AddSeconds(step);
            session.BeginFrame(now);
            var sourceFailed = false;
            try
            {
                feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush(); session.Update();
            }
            catch (ArgumentOutOfRangeException) { sourceFailed = true; }
            var nativeFailed = false;
            try { region.Synchronize(actors.ToArray(), [new(0, "", 100)], 100, now, true); }
            catch (ArgumentOutOfRangeException) { nativeFailed = true; }
            if (step == 3)
                Check(session.BossModules.LoadedModules.First().PrimaryActor.OID == (reverse ? 0x31C7u : 0x319Cu)
                    && region.Loaded.First().Definition.PrimaryOid == (reverse ? 0x31C7u : 0x319Cu),
                    "CE54 both source and native exercise requested subscriber order");
            Check(sourceFailed == (step >= 4) && sourceFailed == nativeFailed, "CE54 cross-CE snapshot failure " + step);
            if (step < 4) continue;
            Check(region.Encounters.All(m => m.Actors[40].Cast?.ActionId == 23589
                && m.Actors[40].Destroyed == (step >= 5)), "CE54 all consumers retain original cast and shared Destroyed before failure " + step);
            var baits = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C7)
                .Components.OfType<BaitAwayChargeTether>().Single().CurrentBaits;
            var nativeBaits = region.Encounters.Single(m => m.Definition.PrimaryOid == 0x31C7).SteelBaits;
            Check(baits.Count == (step == 4 ? 1 : 0) && nativeBaits.Count == baits.Count,
                "CE54 shared Untether precedes failing Destroy Finish exactly once " + step);
        }
        var cavalry = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C7);
        var native = region.Encounters.Single(m => m.Definition.PrimaryOid == 0x31C7);
        Check(cavalry.Components.OfType<BaitAwayChargeTether>().Count() == 1 && native.Active,
            "CE54 RawSteel independent consumer really active");
        Check(session.World.Actors.Find(200) == null && region.Encounters.All(m => !m.Actors.ContainsKey(200))
            && cavalry.Components.OfType<BaitAwayChargeTether>().Single().CurrentBaits.Count == 0
            && native.SteelBaits.Count == 0, "CE54 later Created blocked across every model, no leaked RawSteel bait or clock");
    }
}
