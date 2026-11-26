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
    private static void RunDal0(Action<string, Action> run)
    {
        run("Bozja native DAL0 exact source union arena radius client two components", Dal0Client);
        run("Bozja native DAL0 rotating both directions zero angle six true effects Finish ignored locked origin", Dal0Rotation);
        run("Bozja native DAL0 duplicate sequences index zero cross source consume overdue cache last color", Dal0Multiple);
        run("Bozja native DAL0 before Update cache and live sequence navigation separate", Dal0BeforeUpdate);
        run("Bozja native DAL0 dynamic raw NPC raidwide original clock", () => Damage(29));
        run("Bozja native DAL0 real Feed two components cold casts no historical rest inference normalized output", Dal0Feed);
    }

    private static void Dal0Client()
    {
        using var f = new Fixture(29);
        Check(f.M.Definition.Rules.Length == 2 && f.M.SampleRadius == f.Source.Arena.Bounds.Radius, "DAL0 exact radius");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9432)?.Singular.ToString() == f.M.Definition.Name,
            "DAL0 client name " + Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9432)?.Singular.ToString());
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.TheDalriada.DAL04thLegionColossusRubricatus.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DAL0 action " + action);
        Check(f.M.Center == f.Source.Arena.Center.ToVec3(), "DAL0 source union center");
        for (var x = -25f; x <= 25; x += .173f)
            for (var z = -25f; z <= 25; z += .187f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DAL0 exact union polygon");
            }
        f.M.Clear(); f.Move(100, f.Center + Vector3.UnitX * 100);
        Check(f.M.Active, "DAL0 no invented pull range");
        Check(BozjaCatalog.Owns(975, 0x326C) && !BozjaCatalog.Owns(920, 0x326C), "DAL0 exact ownership");
    }

    private static void Dal0Update(Fixture f, GenericRotatingAOE source)
    {
        source.Update();
        f.M.Update(f.Now);
        RobotCompare(f, source, "MagitekSlash", "DAL0 rotation " + f.M.ColossusNumCasts);
        Check(source.NumCasts == f.M.ColossusNumCasts, "DAL0 per Effect counter");
    }

    private static void Dal0Rotation()
    {
        foreach (var action in new uint[] { 24225, 24226 })
        {
            using var f = new Fixture(29);
            var source = f.Activate<GenericRotatingAOE>("MagitekSlash");
            var spell = RobotStart(f, source, action, rotation: 0, total: 5);
            Dal0Update(f, source);
            Check(source.Sequences.Single().NumRemainingCasts == 6 && f.M.Visible().Count() == 2, "DAL0 six casts zero direction valid");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Dal0Update(f, source);
            var before = f.M.Visible().ToArray();
            f.Move(2, f.Center + new Vector3(15, 0, 9));
            f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            Dal0Update(f, source);
            Check(f.M.Visible().SequenceEqual(before), "DAL0 overdue published cache stays exact");
            for (var i = 0; i < 6; ++i)
            {
                f.Now = START.AddSeconds(101 + i); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
                source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(i == 0 ? action : 24227));
                f.M.Effect(1, i == 0 ? action : 24227);
                Dal0Update(f, source);
            }
            Check(!f.M.Visible().Any() && !f.M.Navigation(f.NativeViewer).Any(), "DAL0 sixth actual Effect clears");
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24227)); f.M.Effect(1, 24227);
            Dal0Update(f, source);
            Check(f.M.ColossusNumCasts == 7, "DAL0 counter increments empty queue");
        }
    }

    private static void Dal0Multiple()
    {
        using var f = new Fixture(29);
        var source = f.Activate<GenericRotatingAOE>("MagitekSlash");
        RobotStart(f, source, 24225, 1, .37f, 7);
        RobotStart(f, source, 24226, 2, -.53f, 1);
        Dal0Update(f, source);
        for (var i = 0; i < 12; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(24227)); f.M.Effect(2, 24227);
            Dal0Update(f, source);
            Check(source.Sequences.Count == (i < 5 ? 2 : i < 11 ? 1 : 0), "DAL0 only index zero consumed");
        }
        Check(f.M.ColossusNumCasts == 12, "DAL0 not double-counting two sequences");
    }

    private static void Dal0BeforeUpdate()
    {
        using var f = new Fixture(29);
        var source = f.Activate<GenericRotatingAOE>("MagitekSlash");
        RobotStart(f, source, 24225, rotation: 0, total: 5);
        RobotCompare(f, source, "MagitekSlash", "DAL0 initial query before Update has only live near Goal");
        Dal0Update(f, source);
        for (var i = 0; i < 6; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24227));
            f.M.Effect(1, 24227);
        }
        Check(source.ActiveAOEs(0, f.Viewer).Length != 0 && f.M.Visible().Any(),
            "DAL0 published cones remain until Update");
        Check(!f.M.Navigation(f.NativeViewer).Any(), "DAL0 empty live sequences suppress cached navigation");
        RobotCompare(f, source, "MagitekSlash", "DAL0 empty before Update source navigation guard");
        RobotStart(f, source, 24226, 1, .7f, 3);
        RobotCompare(f, source, "MagitekSlash", "DAL0 new sequence before Update restores old cones with new near Goal");
        Dal0Update(f, source);
        RobotStart(f, source, 24225, 2, -.3f, 9);
        Dal0Update(f, source);
        for (var i = 0; i < 6; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24227));
            f.M.Effect(1, 24227);
        }
        RobotCompare(f, source, "MagitekSlash", "DAL0 live Goal promotes second origin while cached first cones remain");
        Dal0Update(f, source);
    }

    private static void Dal0Feed()
    {
        Initialize();
        using var session = new MechanicSession("DAL0-two-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.Definition.Key == "DAL04thLegionColossusRubricatus");
        var center = model.Center;
        ReplayCast Spell(uint action, float total = 5, double at = 0)
            => new(action, 100, center, 0, START.AddSeconds(at), START.AddSeconds(at + total + .3f), false, false)
                { TotalTime = total, IsSpell = true };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x326C, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000, Cast = Spell(24225) },
            new() { Id = 20, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Cast = Spell(24224, 3) },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 10, Hp = 1000 },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, uint effect = 0)
        {
            var now = START.AddSeconds(at); session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(),
                [], actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(20, 1, effect, 0, 0, default, 0, 0, 0, []));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () => { if (effect != 0) region.Effect(20, effect); });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x326C);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            Check(source.Components.Count == 2 && model.Active, "DAL0 two actual Feed components");
            var rotating = source.Components.OfType<GenericRotatingAOE>().Single();
            var expected = rotating.ActiveAOEs(0, viewer).ToArray(); var actual = model.Visible(native).ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Color, a.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Danger ? Colors.Danger : Colors.AOE, a.Risky))),
                "DAL0 Feed ordered raw publish " + at);
            var ai = new AIHints();
            foreach (var component in source.Components) component.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DAL0 normalized clocks " + at);
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, model.SampleRadius, out var kind);
                if (kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals)) converted = goals;
                var shapes = zones[i].Convert(center, model.SampleRadius).ToArray();
                Check(converted.Count == shapes.Length, "DAL0 real conversion " + at);
                for (var x = -18f; x < 18; x += 1.231f)
                    for (var z = -23f; z < 23; z += 1.137f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                            == shapes.Any(s => s.Contains(p, 0)), "DAL0 actual host geometry " + at);
                    }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DAL0 damage raw clock " + at);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints()), "DAL0 global hints");
        }
        Frame(0); Frame(1);
        actors[0] = actors[0] with { Cast = null }; Frame(2);
        actors[1] = actors[1] with { Cast = null }; Frame(3);
        for (var i = 0; i < 7; ++i) Frame(4 + i, 24227);
        Frame(100);
        // A cold rest Effect creates no missing First sequence.
        using var coldSession = new MechanicSession("DAL0-cold-rest");
        var coldFeed = new MechanicWorldFeed(coldSession);
        var coldRegion = new BozjaRegion(975);
        actors[0] = actors[0] with { Cast = Spell(24227, 0, 101) };
        var coldNow = START.AddSeconds(101);
        coldSession.BeginFrame(coldNow);
        coldFeed.ApplyFrame(new(975, 778, 100, 0,
            party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
            actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, coldNow])!).ToArray()));
        coldFeed.Flush(); coldSession.Update();
        coldRegion.Synchronize(actors.ToArray(), party, 100, coldNow, true);
        var coldModel = coldRegion.Encounters.Single(m => m.Definition.Key == "DAL04thLegionColossusRubricatus");
        var coldSource = coldSession.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x326C);
        Check(coldSource.Components.OfType<GenericRotatingAOE>().Single().Sequences.Count == 0
            && !coldModel.Visible(coldModel.Actors[100]).Any(), "DAL0 fresh cold rest does not invent sequence");
    }
}
