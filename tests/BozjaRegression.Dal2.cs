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
    private static void RunDal2(Action<string, Action> run)
    {
        run("Bozja native DAL2 client ten components exact clipped48 polygon", Dal2Client);
        foreach (var rule in BozjaCatalog.Definitions[30].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
            run("Bozja native DAL2 standard " + rule.Component, () => Dal2Standard(rule));
        run("Bozja native DAL2 Ambient max6 danger3 hidden promotion all risky", Dal2Ambient);
        run("Bozja native DAL2 morph exact40 polygons raw Distance first void clock five effects no Start reset", Dal2Morph);
        run("Bozja native DAL2 jump actual TargetXZ replaces only23684 clears no TTL", Dal2Jumps);
        run("Bozja native DAL2 wall march directions float endpoints raw 6/25 bounds", Dal2March);
        run("Bozja native DAL2 horizontal wall ray ULP exact endpoint", Dal2WallBoundary);
        run("Bozja native DAL2 duplicate Raidwide array original dynamic NPC clock", Dal2Damage);
        run("Bozja native DAL2 Icon duplicate FIFO R100 original target generations Dead Update no TTL", Dal2Baits);
        run("Bozja native DAL2 real Feed all ten components status59 oldParty icon effects cold isolation", Dal2Feed);
    }

    private static void Dal2Client()
    {
        using var f = new Fixture(30);
        Check(f.M.Definition.Rules.Length == 10 && f.M.SampleRadius == f.Source.Arena.Bounds.Radius, "DAL2 ten components radius");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(10004)?.Singular.ToString() == f.M.Definition.Name, "DAL2 client name");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.TheDalriada.DAL2Cuchulainn.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DAL2 client AID " + action);
        Check(f.Center == f.Source.Arena.Center.ToVec3(), "DAL2 actual clipped arena center");
        for (var x = -27f; x < 27; x += .271f)
            for (var z = -27f; z < 27; z += .319f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DAL2 exact polygon");
            }
    }

    private static void Dal2Compare(Fixture f, GenericAOEs source, string name)
    {
        var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
        var actual = f.M.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == name).ToArray();
        Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky, a.Color))
            .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                a.Danger ? Colors.Danger : a.SafeColor ? Colors.SafeFromAOE : 0))), "DAL2 " + name + " fields");
        for (var i = 0; i < expected.Length; ++i)
            for (var x = -28f; x < 28; x += .731f)
                for (var z = -28f; z < 28; z += 1.137f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(expected[i].Check(new(p.X, p.Z)) == actual[i].Cast.Contains(p), "DAL2 " + name + " raw Check");
                }
        var hints = new BossComponent.TextHints();
        foreach (var component in f.Source.Components) component.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
            .Select(h => (h.Text, h.Risky))), "DAL2 " + name + " hints");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == name).ToArray();
        Check(ai.ForbiddenZones.Count == zones.Length, "DAL2 " + name + " nav count");
        for (var i = 0; i < zones.Length; ++i)
        {
            var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, f.Center, f.M.SampleRadius, out _);
            var shapes = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
            Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation && converted.Count == shapes.Length, "DAL2 actual conversion " + name);
            for (var x = -26f; x < 26; x += .971f)
                for (var z = -26f; z < 26; z += 1.137f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                        == shapes.Any(s => s.Contains(p, 0)), "DAL2 converted geometry " + name);
                }
        }
    }

    private static void Dal2Standard(BozjaRule rule)
    {
        using var f = new Fixture(30);
        var source = f.Activate<GenericAOEs>(rule.Component);
        var spell = f.Start(source, rule.Actions[0]);
        Dal2Compare(f, source, rule.Component);
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(rule.Actions[0])); f.M.Effect(2, rule.Actions[0]);
        Dal2Compare(f, source, rule.Component);
        RobotFinish(f, source, spell);
        Dal2Compare(f, source, rule.Component);
    }

    private static void Dal2Ambient()
    {
        using var f = new Fixture(30);
        var source = f.Activate<GenericAOEs>("AmbientPulsation");
        ActorCastInfo? spell = null;
        for (var i = 0; i < 8; ++i)
        {
            spell = RobotStart(f, source, 23694, total: 8 - i);
            Dal2Compare(f, source, "AmbientPulsation");
            Check(f.M.Visible().Count() == Math.Min(i + 1, 6), "DAL2 max6 visible");
        }
        for (var i = 0; i < 8; ++i)
        {
            RobotFinish(f, source, spell!);
            Dal2Compare(f, source, "AmbientPulsation");
        }
    }

    private static void Dal2Morph()
    {
        using var f = new Fixture(30);
        var source = f.Activate<GenericAOEs>("FleshNecromass");
        var jumps = f.Activate<GenericAOEs>("FleshNecromassJumps");
        var spell = RobotStart(f, source, 23695);
        Dal2Compare(f, source, "FleshNecromass");
        var first = f.M.Visible(f.NativeViewer).Single().Cast.Activation;
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        RobotFinish(f, source, spell);
        RobotStart(f, source, 23695);
        Check(f.M.Visible(f.NativeViewer).Single().Cast.Activation == first, "DAL2 first void remains no TTL no re-clock");
        RobotStart(f, source, 23682);
        Dal2Compare(f, source, "FleshNecromass");
        var raw = source.ActiveAOEs(0, f.Viewer)[0].Shape.Distance(f.Source.Arena.Center, default);
        for (var x = -25f; x <= 25; x += .357f)
            for (var z = -25f; z <= 25; z += .463f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                var shape = (BozjaOrderTemplate)f.M.Visible(f.NativeViewer).Single().Cast.Shape;
                var expected = raw.Distance(new(p.X, p.Z));
                var actual = shape.Distance(p.X, p.Z);
                Check(expected == actual, $"DAL2 exact inverted raw Distance at {p}: source={expected:R} native={actual:R}");
            }
        void Status(bool gain)
        {
            var s = new ActorStatus(2543, 0, START.AddSeconds(130), 1);
            if (gain) source.OnStatusGain(f.Viewer, ref s); else source.OnStatusLose(f.Viewer, ref s);
            typeof(BozjaEncounter).GetMethod("Status", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(f.M, [f.NativeViewer, new ReplayStatus(2543, 0, 1, s.ExpireAt), gain]);
            Dal2Compare(f, source, "FleshNecromass");
        }
        Status(true); Status(true); Status(false);
        for (var i = 0; i < 7; ++i)
        {
            if (i == 3) RobotStart(f, source, 23682);
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23683));
            jumps.OnEventCast(f.W.Actors.Find(2)!, new(new(ActionType.Spell, 23683), 100, 0, 0, f.Center, 0, 0, default));
            f.M.Effect(2, 23683, position: f.Center);
            Dal2Compare(f, source, "FleshNecromass");
            Check(source.NumCasts == f.M.CuchNumJumps, "DAL2 fifth resets without visual resetting count");
        }
    }

    private static void Dal2Jumps()
    {
        using var f = new Fixture(30);
        var source = f.Activate<GenericAOEs>("FleshNecromassJumps");
        foreach (var (action, p) in new[] { (23683u, f.Center + Vector3.UnitX * 5), (24953u, f.Center + Vector3.UnitZ * 9) })
        {
            source.OnEventCast(f.W.Actors.Find(2)!, new(new(ActionType.Spell, action), 100, 0, 0, p, 0, 0, default));
            f.M.Effect(2, action, position: p);
            Dal2Compare(f, source, "FleshNecromassJumps");
        }
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23685)); f.M.Effect(1, 23685);
        Dal2Compare(f, source, "FleshNecromassJumps");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23684)); f.M.Effect(1, 23684);
        Dal2Compare(f, source, "FleshNecromassJumps");
    }

    private static void Dal2March()
    {
        using var f = new Fixture(30);
        var source = f.Activate<GenericForcedMarch>("BurgeoningDread");
        void Status(uint id, bool gain, double at)
        {
            var s = new ActorStatus(id, 0, START.AddSeconds(at), 1);
            if (gain) source.OnStatusGain(f.Viewer, ref s); else source.OnStatusLose(f.Viewer, ref s);
            typeof(BozjaEncounter).GetMethod("Status", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(f.M, [f.NativeViewer, new ReplayStatus(id, 0, 1, s.ExpireAt), gain]);
        }
        void Compare()
        {
            Check(source.ForcedMovements(f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad))
                .SequenceEqual(f.M.CuchMovements(f.NativeViewer)), "DAL2 exact wall stopped float movements");
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
                .Where(h => h.Text == "Aim for safe spot!").Select(h => (h.Text, h.Risky))), "DAL2 march unsafe");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.ForbiddenDirections.Select(d => (d.center.Rad, d.halfWidth.Rad, d.activation))
                .SequenceEqual(f.M.CuchDirections(f.NativeViewer)), "DAL2 raw directions ordered");
            ai.Normalize();
            var headings = ai.ForbiddenDirections.Where(d => d.activation <= f.Now.AddSeconds(.75)).ToArray();
            float? heading = headings.Length > 0 ? headings[^1].center.Rad + MathF.PI : null;
            Check(f.M.Facing(f.NativeViewer.Actor) == heading, "DAL2 actual bridge normalized last direction");
            Check(source.NumActiveForcedMarches == f.M.CuchActiveMarches, "DAL2 active count");
        }
        Status(2163, true, 2); Status(2161, true, 1); Status(2161, true, 1);
        Compare();
        f.Now = START.AddSeconds(.5); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Compare();
        foreach (var p in new[] { f.Center, f.Center + Vector3.UnitX * 20, BozjaEncounter.CuchPositions[0] + Vector3.UnitX * 6,
            BozjaEncounter.CuchPositions[0] + Vector3.UnitX * 25 })
        {
            f.Move(100, p); Compare();
        }
        Status(1257, true, 3); Status(3629, true, 4); Compare();
        Status(2161, false, 1); Status(2161, false, 1); Status(2163, false, 2); Compare();
        Check(!f.M.CuchDirections(f.NativeViewer).Any(), "DAL2 forced without pending gives no forbidden directions");
        Status(1257, false, 3); Status(3629, false, 4); Compare();
    }

    private static void Dal2Damage()
    {
        using var f = new Fixture(30);
        var source = f.Activate<RaidwideCast>("PutrifiedSoulBurgeoningDreadGhastlyAura");
        foreach (var action in new uint[] { 23695, 23696, 23688, 23689, 24909, 24910 })
        {
            var spell = RobotStart(f, source, action);
            var expectedCount = action == 23695 ? 2 : action == 23696 ? 0 : 1;
            Check(source.Casters.Count == expectedCount && f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.Raidwide) == expectedCount,
                "DAL2 actual duplicate array match " + action);
            var expected = new AIHints(); source.AddAIHints(0, f.Viewer, default, expected);
            Check(expected.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Where(d => d.Type == "Raidwide").Select(d => (d.Mask, d.Activation, d.Type))),
                "DAL2 dynamic diagnostic multiplicity");
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(action)); f.M.Effect(1, action);
            Check(source.NumCasts == f.M.CuchRaidwideNumCasts, "DAL2 duplicate Effect counter");
            RobotFinish(f, source, spell);
            Check(source.Casters.Count == 0 && !f.M.Casts.Any(c => c.Rule.Kind == BozjaRuleKind.Raidwide), "DAL2 duplicate Finish removes twice");
        }
    }

    private static void Dal2WallBoundary()
    {
        using var f = new Fixture(30);
        var source = f.Activate<GenericForcedMarch>("BurgeoningDread");
        var point = new Vector3(647.4f, 0, -205.9f);
        f.Viewer.PosRot = new(point, MathF.PI / 2);
        f.Set(100, a => a with { Position = point, Rotation = MathF.PI / 2 }); f.Sync();
        var status = new ActorStatus(2161, 0, START.AddSeconds(1), 1);
        source.OnStatusGain(f.Viewer, ref status);
        typeof(BozjaEncounter).GetMethod("Status", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(f.M, [f.NativeViewer, new ReplayStatus(2161, 0, 1, status.ExpireAt), true]);
        var expected = source.ForcedMovements(f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad)).ToArray();
        var actual = f.M.CuchMovements(f.NativeViewer).ToArray();
        Check(expected.SequenceEqual(actual), $"DAL2 horizontal real source wall endpoint expected={string.Join(',', expected)} actual={string.Join(',', actual)}");
        var points = Enumerable.Range(0, 21).SelectMany(x => Enumerable.Range(0, 21)
            .Select(z => f.Center + new Vector3(-30 + x * 3, 0, -30 + z * 3)))
            .Append(new(637.6525268554688f, 0, -166.01547241210938f))
            .Concat(f.M.Arena.GetContours().SelectMany(c => c).SelectMany(p => new[] { p,
                p with { X = MathF.BitIncrement(p.X) }, p with { X = MathF.BitDecrement(p.X) },
                p with { Z = MathF.BitIncrement(p.Z) }, p with { Z = MathF.BitDecrement(p.Z) } }));
        for (var phase = 0; phase < 2; ++phase)
        {
            if (phase == 1)
            {
                foreach (var id in new uint[] { 2163, 2162, 1257 })
                {
                    var extra = new ActorStatus(id, 0, START.AddSeconds(3), 1);
                    source.OnStatusGain(f.Viewer, ref extra);
                    typeof(BozjaEncounter).GetMethod("Status", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(f.M, [f.NativeViewer, new ReplayStatus(id, 0, 1, extra.ExpireAt), true]);
                }
            }
            foreach (var p in points)
                for (var i = 0; i < 64; ++i)
                {
                    var angle = i * (MathF.Tau / 64);
                    f.Viewer.PosRot = new(p, angle);
                    f.NativeViewer.Actor = f.NativeViewer.Actor with { Position = p, Rotation = angle };
                    expected = source.ForcedMovements(f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad)).ToArray();
                    actual = f.M.CuchMovements(f.NativeViewer).ToArray();
                    if (!expected.SequenceEqual(actual))
                    {
                        var last = expected[^1];
                        var dir = new Angle(last.Item3).ToDirection();
                        var rawSource = f.Source.Arena.IntersectRayBounds(new(last.Item1.X, last.Item1.Z), dir);
                        var query = new CuchulainnGeometry(f.M.Arena);
                        var rawNative = query.WallDistance(last.Item1, new(dir.X, 0, dir.Z));
                        Check(false, $"DAL2 real source wall sweep phase{phase} {p}/{angle:R}: raw={rawSource:R}/{rawNative:R} expected={string.Join(',', expected)} actual={string.Join(',', actual)}");
                    }
                }
        }
        Console.WriteLine($"INFO DAL2 wall exact single/chained inside/outside/vertices ULP: AVX512={System.Runtime.Intrinsics.X86.Avx512F.IsSupported}, AVX2={System.Runtime.Intrinsics.X86.Avx2.IsSupported}, FMA={System.Runtime.Intrinsics.X86.Fma.IsSupported}");
    }

    private static void Dal2Feed()
    {
        Initialize();
        using var session = new MechanicSession("DAL2-ten-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.IsCuchulainn);
        var center = model.Center;
        ReplayCast Spell(uint action, double at = 0)
            => new(action, 100, center, .4f, START.AddSeconds(at), START.AddSeconds(at + 5 + .3f), false, false)
                { TotalTime = 5, IsSpell = true };
        ReplayStatus Status(uint id, int slot) => new(id, 0, 1, START.AddSeconds(20)) { SlotIndex = slot };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31AB, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000, Cast = Spell(23695) },
            new() { Id = 20, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Cast = Spell(23682) },
            new() { Id = 21, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 12, Hp = 1000, Cast = Spell(23694) },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, Statuses = [Status(2161, 0), Status(2936, 58), Status(1422, 59)] },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 12, Hp = 1000, Statuses = [Status(2543, 59)] },
            new() { Id = 102, Kind = 2, SubKind = 2, Position = center, Hp = 1000, Cast = Spell(23694),
                Statuses = [Status(2163, 0), Status(1422, 59)] },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101), new(2, "", 102)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, uint effect = 0, bool icon = false)
        {
            var now = START.AddSeconds(at); session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(),
                [], actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (icon) feed.Enqueue(new MechanicEventIcon(40, 101, 100));
            if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(20, 1, effect, 100, 0, center + Vector3.UnitX * 7, 0, 0, 0, []));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
            {
                if (icon) region.Icon(100, 40, 101);
                if (effect != 0) region.Effect(20, effect, 100, center + Vector3.UnitX * 7);
            });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31AB);
            Check(source.Components.Count == 10 && model.Active, "DAL2 ten actual Feed components");
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            var viewerSlot = session.World.Party.FindSlot(100);
            foreach (var component in source.Components.OfType<GenericAOEs>())
            {
                var name = component.GetType().Name;
                var expected = component.ActiveAOEs(viewerSlot, viewer).ToArray();
                var actual = model.Visible(native).Where(a => a.Cast.Rule.Component == name).ToArray();
                Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky, a.Color))
                    .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                        a.Danger ? Colors.Danger : a.SafeColor ? Colors.SafeFromAOE : 0))), "DAL2 Feed ordered AOE " + name + "/" + at);
            }
            var ai = new AIHints(); var hints = new BossComponent.TextHints();
            foreach (var component in source.Components)
            {
                component.AddAIHints(viewerSlot, viewer, default, ai);
                component.AddHints(viewerSlot, viewer, hints);
            }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "DAL2 all personal hints " + at);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints()), "DAL2 global hints " + at);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DAL2 all normalized damage " + at);
            Check((ai.ImminentSpecialMode.Item1 == AIHints.SpecialMode.Misdirection) == model.CuchMisdirection(native), "DAL2 real status59 special mode " + at);
            var pet = session.World.Actors.Find(102)!;
            Check(pet.Type == ActorType.Pet, "DAL2 actual cold Pet classification");
            var petAi = new AIHints();
            foreach (var component in source.Components) component.AddAIHints(2, pet, default, petAi);
            Check(petAi.ImminentSpecialMode.Item1 == AIHints.SpecialMode.Misdirection && model.CuchMisdirection(model.Actors[102]),
                "DAL2 cold Pet slot59 retained across ignored hot Lose");
            var march = source.Components.OfType<GenericForcedMarch>().Single();
            Check(march.ForcedMovements(pet).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad))
                .SequenceEqual(model.CuchMovements(model.Actors[102])), "DAL2 cold Pet march exact real slot0");
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DAL2 normalized nav count/clock " + at);
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, model.SampleRadius, out _);
                var actual = zones[i].Convert(center, model.SampleRadius).ToArray();
                Check(converted.Count == actual.Length, "DAL2 host converted count " + at);
                for (var x = -25f; x < 25; x += 1.131f)
                    for (var z = -25f; z < 25; z += 1.731f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                            == actual.Any(s => s.Contains(p, 0)), "DAL2 real host geometry " + at);
                    }
            }
        }
        Frame(0); Frame(.1, icon: true);
        actors[3] = actors[3] with { Statuses = [Status(2161, 0), Status(2936, 58)] };
        Frame(.2);
        Check(!model.CuchMisdirection(model.Actors[100]), "DAL2 Lose one matching status clears shared bit despite second status");
        actors[3] = actors[3] with { Statuses = [Status(2161, 0), Status(2936, 58), Status(2543, 59)] };
        party = [new(0, "", 101), new(1, "", 100), new(2, "", 102)]; Frame(.3);
        Check(!model.Visible(model.Actors[100]).Single(a => a.Cast.Rule.Kind == BozjaRuleKind.CuchFlesh).SafeColor,
            "DAL2 morph gain callback old slot then current slot inherits prior actor bit");
        actors[3] = actors[3] with { Statuses = [Status(2161, 0), Status(2936, 58)] }; Frame(.4);
        Check(model.Visible(model.Actors[100]).Single(a => a.Cast.Rule.Kind == BozjaRuleKind.CuchFlesh).SafeColor,
            "DAL2 next Lose current slot clears inherited bit while morph active");
        actors[0] = actors[0] with { Cast = null }; actors[1] = actors[1] with { Cast = null };
        Frame(1);
        for (var i = 0; i < 7; ++i) Frame(2 + i, i % 2 == 0 ? 23683u : 24953u);
        Frame(10, 23685); Frame(11, 23684); Frame(12, 23692);
        actors[3] = actors[3] with { Statuses = [Status(2543, 59)] };
        party = [new(0, "", 101), new(1, "", 100), new(2, "", 102)]; Frame(13);
        actors[3] = actors[3] with { Statuses = [] }; Frame(14);
        actors[5] = actors[5] with { Statuses = [] }; Frame(15);
        Frame(100);
    }

    private static void Dal2Baits()
    {
        using var f = new Fixture(30);
        var source = f.Activate<BaitAwayIcon>("FellFlowBait");
        void Icon(ulong target)
        {
            source.OnEventIcon(f.Viewer, 40, target); f.M.Icon(100, 40, target);
            Check(source.CurrentBaits.Select(b => (b.Source.InstanceID, b.Target.InstanceID, b.Activation))
                .SequenceEqual(f.M.SeekerBaits.Select(b => (b.Source.Actor.Id, b.Target.Actor.Id, b.Activation))), "DAL2 raw Icon order identity clock");
        }
        Icon(101);
        f.Now = START.AddSeconds(1); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Icon(999); Icon(999);
        SeekerCompareBait(f, source, "FellFlowBait", "DAL2 duplicate Icon own R100");
        source.OnEventCast(f.W.Actors.Find(2)!, SeekerEvent(23692, 100)); f.M.Effect(2, 23692, 100);
        Check(source.CurrentBaits.Count == 2 && f.M.SeekerBaits.Count == 2, "DAL2 FIFO pops one across source");
        SeekerCompareBait(f, source, "FellFlowBait", "DAL2 FIFO original own targets");
        var old = f.Viewer; var nativeOld = f.NativeViewer;
        f.Remove(100); f.Add(100, 0, f.Center + Vector3.UnitZ * 4, ActorType.Player); f.Sync();
        Check(source.CurrentBaits.All(b => ReferenceEquals(b.Target, old))
            && f.M.SeekerBaits.All(b => ReferenceEquals(b.Target, nativeOld)), "DAL2 Destroy sameID keeps original references");
        SeekerCompareBait(f, source, "FellFlowBait", "DAL2 new generation ID drawing versus reference navigation");
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        SeekerCompareBait(f, source, "FellFlowBait", "DAL2 no TTL");
        old.IsDead = true; nativeOld.Actor = nativeOld.Actor with { Dead = true };
        Check(source.CurrentBaits.Count == 2 && f.M.SeekerBaits.Count == 2, "DAL2 Dead before Update retains queue");
        source.Update(); f.M.Update(f.Now);
        Check(source.CurrentBaits.Count == 0 && f.M.SeekerBaits.Count == 0, "DAL2 Dead Update clears original target");
    }
}
