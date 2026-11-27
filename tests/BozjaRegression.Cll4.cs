using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using Cll4Source = BossMod.Shadowbringers.Foray.CastrumLacusLitore.CLL4Dawon;

internal static partial class BozjaRegression
{
    private static readonly Vector3 cll4Lyon = new(80, 0, -874);

    private static void RunCll4(Action<string, Action> run)
    {
        run("Bozja pending CLL4 client manifest four exact arenas and ordinary main Boss completion", Cll4Manifest);
        foreach (var rule in BozjaCll4Catalog.Definition.Rules.Where(r => r.Kind is BozjaRuleKind.Aoe
            or BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.CastGaze))
            foreach (var action in rule.Actions)
                foreach (var lyon in new[] { false, true })
                    run($"Bozja pending CLL4 real Feed {rule.Component}/{action} lyon={lyon}",
                        () => Cll4Standard(rule, action, lyon));
        run("Bozja pending CLL4 draw gates split from forced center wall and persistent warning", Cll4Arena);
        run("Bozja pending CLL4 component Created Spell cold order wall before after warning", Cll4Cold);
        run("Bozja pending CLL4 Hammer sticky color strict first plus1.9 and actual caster Finish", Cll4Hammer);
        run("Bozja pending CLL4 Hammer short query mutates complete group before any item returned", Cll4HammerShort);
        run("Bozja pending CLL4 Plumes early prediction Flutter append lazy originals Finish only", Cll4Plumes);
        run("Bozja pending CLL4 Flutter CLR exact double trig rectangle boundary and ULP outside", Cll4FlutterBoundary);
        run("Bozja pending CLL4 Created Obey FIFO first later clocks no Destroy consumption", Cll4Obey);
        run("Bozja pending CLL4 NaturesBlood spell rotation cross caster first position seven Effects cache", Cll4Blood);
        run("Bozja pending CLL4 NaturesPulse stage Loc count mismatch plus1.5 Update cache", Cll4Pulse);
        run("Bozja pending CLL4 WindsPeak real 20847 immunity slot59 Goal and sequential displacement", Cll4Wind);
        run("Bozja pending CLL4 TheKingsNotice real eyes default range and original watched Finish", Cll4Gaze);
        run("Bozja pending CLL4 simultaneous reversed component damage normalized by raw NPC clock", Cll4DamageOrder);
    }

    private static void Cll4Manifest()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9452)?.Singular.ToString() == f.Native.Definition.Name,
            "CLL4 client exact name");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(735)?.TerritoryType.RowId == 920, "CLL4 CFC");
        foreach (var action in Enum.GetValues<Cll4Source.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CLL4 real action extra " + action);
        void Arena()
        {
            for (var x = 44f; x < 116; x += .471f)
                for (var z = f.Native.Center.Z - 36; z < f.Native.Center.Z + 36; z += .513f)
                    Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0),
                        "CLL4 exact clipped world arena");
        }
        Arena(); f.Draw(cll4Lyon); Arena();
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EB02F, cll4Lyon)); f.Frame(1); Arena();
        f.Draw(f.Center); Arena();
        f.Change(1, a => a with { Hp = 0 }); f.Frame(2);
        Check(!f.Native.Active, "CLL4 main HP0 ends without waiting for Lyon");
    }

    private static void Cll4Standard(BozjaRule rule, uint action, bool lyon)
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        if (lyon) f.Draw(cll4Lyon);
        f.Change(20, a => a with { Cast = f.Spell(action, .1, point: f.Native.Center + Vector3.UnitZ * 4) }); f.Frame(.1);
        f.Change(20, a => a with { Position = f.Native.Center + Vector3.UnitX * 17, Rotation = 1.3f }); f.Frame(1);
        f.Frame(50);
        f.Change(20, a => a with { Cast = null }); f.Frame(51);
        Check(!f.Native.Casts.Any(c => c.Rule == rule), "CLL4 standard actual Finish " + action);
    }

    private static void Cll4Arena()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20858, 1) }); f.Frame(1);
        var old = f.Native.Casts.Single(c => c.Rule.Component == "ArenaChange");
        Check(old.Activation == START.AddSeconds(6.3f).AddSeconds(.2d), "CLL4 Dawon warning Finish plus .2");
        f.Draw(cll4Lyon);
        Check(!f.Native.Cll4DawonArena && f.Native.Center == cll4Lyon, "CLL4 draw switches flag and center");
        f.Change(20, a => a with { Cast = f.Spell(20836, 2, point: cll4Lyon) }); f.Frame(2);
        Check(f.Native.Casts.Single(c => c.Rule.Component == "ArenaChange").Activation == START.AddSeconds(2).AddSeconds((5f + .3f) + 1.2d),
            "CLL4 Lyon warning Finish plus1.2");
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EB03C, f.Center)); f.Frame(3);
        Check(!f.Native.Cll4DawonArena && f.Native.Center == new Vector3(80, 0, -813),
            "CLL4 Dawon wall forces center without changing component gate flag");
        f.Change(20, a => a with { Cast = f.Spell(20855, 4) }); f.Frame(4);
        Check(!f.Native.Visible(f.NativeViewer).Any(a => a.Cast.Rule.Component == "Pentagust"), "CLL4 component gate follows flag");
        f.Draw(f.Center);
        Check(f.Native.Cll4DawonArena && f.Native.SampleRadius == 30, "CLL4 draw restores Dawon default not starting");
    }

    private static void Cll4Cold()
    {
        foreach (var first in new[] { false, true })
        {
            using var f = new CastrumFixture(false, true);
            f.Change(20, a => a with { Cast = f.Spell(20858) });
            var wall = Cll1Fixture.Actor(30, 0x1EB03C, f.Center);
            if (first) f.Actors.Insert(1, wall); else f.Actors.Add(wall);
            f.Frame(0); f.Change(1, a => a with { InCombat = true }); f.Frame(1);
            Check(!f.Native.Casts.Any(c => c.Rule.Component == "ArenaChange"),
                "CLL4 cold wall before Start suppresses warning, after Start clears it");
        }
    }

    private static void Cll4Hammer()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20874) }); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(20879, 3) }); f.Frame(3);
        Check(f.Native.Visible(f.NativeViewer).First(a => a.Cast.Rule.Component == "LeftRightHammer").Danger,
            "CLL4 earlier group color mutates Danger");
        f.Change(21, a => a with { Cast = null }); f.Frame(4);
        Check(f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule.Component == "LeftRightHammer").Danger,
            "CLL4 Danger color remains after last group removed");
        f.Draw(cll4Lyon); f.Change(20, a => a with { Cast = null }); f.Frame(5); f.Draw(f.Center);
        Check(!f.Native.Visible(f.NativeViewer).Any(a => a.Cast.Rule.Component == "LeftRightHammer"), "CLL4 offside Finish still consumed");
        foreach (var late in new[] { 1.9d, 1.900001d })
        {
            using var g = new CastrumFixture(dawon: true); g.Frame(0);
            g.Change(20, a => a with { Cast = g.Spell(20874) }); g.Frame(0);
            g.Actors.Add(Cll1Fixture.Actor(21, 0x233C, g.Center) with { Cast = g.Spell(20876, late) }); g.Frame(late);
            var list = g.Native.Visible(g.NativeViewer).Where(a => a.Cast.Rule.Component == "LeftRightHammer").ToArray();
            Check(list.Length == 2 && list[0].Danger == (late > 1.9d) && !list[1].Risky,
                "CLL4 full list and strict deadline color risk");
        }
    }

    private static void Cll4Plumes()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2EBF, f.Center + Vector3.UnitZ * 12) with { Dead = true });
        f.Actors.Add(Cll1Fixture.Actor(31, 0x2EBE, f.Center + new Vector3(5, 0, 6)));
        f.Frame(1);
        f.Change(20, a => a with { Cast = f.Spell(20880, 2) with { Rotation = 0 }, Rotation = 2 }); f.Frame(2);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "VerdantScarletPlume") == 4, "CLL4 Flutter appends after early original including dead");
        f.Effect(3, 20865);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "VerdantScarletPlume") == 4, "CLL4 Effect does not clear plumes");
        f.Change(30, a => a with { Cast = f.Spell(20865, 4) }); f.Frame(4);
        f.Change(30, a => a with { Cast = null }); f.Frame(5);
        f.Actors.Add(Cll1Fixture.Actor(32, 0x2EBF, f.Center + Vector3.UnitZ * 20));
        f.Change(20, a => a with { Cast = null }); f.Frame(6);
        Check(!f.Native.Casts.Any(c => c.Rule.Component == "VerdantScarletPlume"), "CLL4 first false blocks later Created");
        f.Change(20, a => a with { Cast = f.Spell(20880, 7) with { Rotation = 0 } }); f.Frame(7);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "VerdantScarletPlume") == 3, "CLL4 late Flutter still adds lazy originals");
        f.Actors.RemoveAll(a => a.Id == 31); f.Frame(8);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "VerdantScarletPlume") == 3, "CLL4 Destroy does not clear locked plume forecasts");
    }

    private static void Cll4HammerShort()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20874) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(20877) });
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(20879, 3) });
        f.Frame(3, false);
        var source = (GenericAOEs)f.Source!.Components.Single(c => c.GetType().Name == "LeftRightHammer");
        _ = source.ActiveAOEs(0, f.Viewer);
        _ = f.Native.Visible(f.NativeViewer).First(a => a.Cast.Rule.Component == "LeftRightHammer");
        f.Change(20, a => a with { Cast = null });
        f.Change(22, a => a with { Cast = null }); f.Frame(4);
        Check(f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule.Component == "LeftRightHammer").Danger,
            "CLL4 second group member retained mutation despite short enumeration");
    }

    private static void Cll4FlutterBoundary()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        var origin = new Vector3(80, 0, -813);
        var point = new Vector3(110.04468536376953f, 0, -798.0897216796875f);
        const float rotation = .6464899778366089f;
        Check(new WPos(point.X, point.Z).InRect(new(origin.X, origin.Z), new Angle(rotation), 30, 0, 15),
            "CLL4 real CLR source accepts exact Flutter front edge");
        f.Actors.Add(Cll1Fixture.Actor(30, 0x2EBF, point));
        f.Actors.Add(Cll1Fixture.Actor(31, 0x2EBF, point with { X = float.BitIncrement(point.X) })); f.Frame(1);
        f.Change(20, a => a with { Cast = f.Spell(20880, 2, point: origin) with { Rotation = rotation } }); f.Frame(2);
        Check(f.Native.Casts.Count(c => c.Rule.Component == "VerdantScarletPlume") == 3,
            "CLL4 exact front edge appended, ULP outside rejected");
    }

    private static void Cll4Obey()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EB03E, f.Center + new Vector3(13.1234f, 0, 6)));
        f.Actors.Add(Cll1Fixture.Actor(31, 0x1EB03D, f.Center - Vector3.UnitX * 10)); f.Frame(1);
        Check(f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule.Component == "Obey").Cast.Activation == START.AddSeconds(12.5),
            "CLL4 first Obey Created Now plus11.5");
        f.Actors.RemoveAll(a => a.Id == 30); f.Frame(2);
        f.Effect(3, 20864);
        Check(f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule.Component == "Obey").Cast.Activation == START.AddSeconds(12.5).AddSeconds(5.1d),
            "CLL4 second Obey keeps first plus5.1");
        f.Effect(4, 20863);
        f.Actors.Add(Cll1Fixture.Actor(32, 0x1EB03E, f.Center)); f.Frame(5);
        Check(f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule.Component == "Obey").Cast.Activation == START.AddSeconds(5).AddSeconds(13.8d),
            "CLL4 later empty queue Now plus13.8");
    }

    private static void Cll4Blood()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0); f.Draw(cll4Lyon);
        f.Change(20, a => a with { Position = cll4Lyon, Rotation = 2, Cast = f.Spell(20831) with { Rotation = 0 } }); f.Frame(0);
        Check(f.Native.Exaflares.Single().Advance == Vector3.UnitZ * 6, "CLL4 uses spell rotation and advance6");
        var cached = f.Native.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == "NaturesBlood").Select(a => a.Cast.Activation).ToArray();
        f.Frame(50);
        Check(cached.SequenceEqual(f.Native.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == "NaturesBlood").Select(a => a.Cast.Activation)),
            "CLL4 elapsed clock alone does not republish exaflare cache");
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, cll4Lyon + new Vector3(1, 0, 1)));
        f.Effect(51, 20832, 21);
        for (var i = 1; i < 7; ++i)
        {
            var next = f.Native.Exaflares.Single().Cast.Origin;
            f.Change(21, a => a with { Position = next }); f.Effect(51 + i, 20832, 21);
        }
        Check(f.Native.Exaflares.Count == 0, "CLL4 seven actual cross caster position Effects consume line");
    }

    private static void Cll4Pulse()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0); f.Draw(cll4Lyon);
        f.Change(20, a => a with { Cast = f.Spell(20833, point: cll4Lyon) }); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, cll4Lyon) with { Cast = f.Spell(20834, 1, point: cll4Lyon) }); f.Frame(1);
        f.Change(21, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.NumEarthCasts == 1, "CLL4 known mismatched stage still counts");
        f.Change(20, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule.Component == "NaturesPulse").Cast.Activation == START.AddSeconds(4.5),
            "CLL4 next stage Now plus1.5");
        f.Change(21, a => a with { Cast = f.Spell(20834, 4, point: cll4Lyon + new Vector3(1, 0, 1)) }); f.Frame(4);
        f.Change(21, a => a with { Cast = null }); f.Frame(5);
        f.Change(21, a => a with { Cast = f.Spell(20835, 6, point: cll4Lyon) }); f.Frame(6);
        f.Change(21, a => a with { Cast = null }); f.Frame(7);
        Check(f.Native.NumEarthCasts == 4 && !f.Native.Visible(f.NativeViewer).Any(a => a.Cast.Rule.Component == "NaturesPulse"),
            "CLL4 source stage and first Loc completion");
    }

    private static void Cll4Wind()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0); f.Draw(cll4Lyon);
        f.Change(20, a => a with { Cast = f.Spell(20847, point: cll4Lyon) }); f.Frame(0);
        f.Change(100, a => a with { Position = cll4Lyon + Vector3.UnitX * 18 }); f.Frame(1);
        Check(f.Native.Cll4Movements(f.NativeViewer).Single().To == cll4Lyon + Vector3.UnitX * 28, "CLL4 source Away10 no wall stop");
        var at = f.Native.Casts.Single(c => c.Rule.Kind == BozjaRuleKind.Cll4Wind).Activation;
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, at) { SlotIndex = 59 }] }); f.Frame(2);
        Check(f.Native.Cll4Movements(f.NativeViewer).Any(), "CLL4 equal expiry is not immune");
        f.Change(100, a => a with { Statuses = [new(160, 0, 100, at.AddSeconds(.001d)) { SlotIndex = 59 }] }); f.Frame(3);
        Check(!f.Native.Cll4Movements(f.NativeViewer).Any()
            && f.Native.Navigation(f.NativeViewer).Any(z => z.Goal && z.JoinRadius == 10), "CLL4 immune still direct Goal");
        f.Change(20, a => a with { Cast = null }); f.Frame(4);
    }

    private static void Cll4Gaze()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0); f.Draw(cll4Lyon);
        f.Change(20, a => a with { Cast = f.Spell(20846, point: cll4Lyon) }); f.Frame(0);
        var source = f.Source!.Components.OfType<GenericGaze>().Single().ActiveEyes(0, f.Viewer).ToArray();
        var actual = f.Native.EyesFor(f.NativeViewer).ToArray();
        Check(source.Length == actual.Length && source[0].Range == actual[0].Range && actual[0].Range == 10000,
            "CLL4 default actual gaze range, not enum comment50");
        f.Draw(f.Center);
        Check(!f.Native.EyesFor(f.NativeViewer).Any(), "CLL4 Dawon gate hides gaze without deleting caster");
        f.Draw(cll4Lyon); f.Change(20, a => a with { Cast = null }); f.Frame(1);
        Check(!f.Native.EyesFor(f.NativeViewer).Any(), "CLL4 actual watched gaze Finish");
    }

    private static void Cll4DamageOrder()
    {
        using var f = new CastrumFixture(dawon: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20859, total: 2) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(20858) });
        f.Frame(.1);
        var damage = f.Native.DamageForHost(f.NativeViewer);
        Check(damage.Length == 2 && damage[0].Type == "Tankbuster"
            && damage[0].Activation < damage[1].Activation, "CLL4 normalized damage survives component order");
    }
}
