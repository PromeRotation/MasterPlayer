using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;

internal static partial class BozjaRegression
{
    private static void RunLightArtillery(Action<string, Action> run)
    {
        run("Alliance artillery source Light client manifest seven components square arena", LightManifest);
        foreach (var rule in AllianceTrashCatalog.LightArtillery.Rules.Where(r =>
            r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Tankbuster or BozjaRuleKind.Interrupt or BozjaRuleKind.CircleBait))
            run("Alliance artillery source Light standard cold duplicate moving ordinary Finish " + rule.Component,
                () => AllianceStandard(AllianceTrashCatalog.LightArtillery, rule, rule.Actions.Single()));
        run("Alliance artillery source Light raw fire event state seven obstacles dead moving no prediction", LightFire);
        run("Alliance artillery source Light fire cached enemy list order after removed dictionary slot reused", LightFireOrder);
        run("Alliance artillery source Light homing original target dead source Effect preserved exact edge rectangle", LightHoming);
        run("Alliance artillery source Light homing first dead source raw clock original target generation", LightHomingClock);
        run("Alliance artillery source Light self target bait skips raid circles still applies edge zone", LightSelfBait);
        run("Alliance artillery source Light exactly three self destruct Effects completes four does not", LightCounter);
        run("Alliance artillery source Light Dead HP0 retained until all Boss Destroyed", LightDestroyed);
        run("Alliance artillery source Light cold missing target and historical Effect not invented", LightCold);
        run("Alliance artillery source Light raw non Spell tankbuster clock and target fallback", LightTankbuster);
    }

    private static void LightRawCompare(CastrumFixture f)
    {
        var source = f.Source!;
        var homing = source.Components.OfType<BaitAwayCast>().Single();
        var baits = f.Native.Casts.Where(c => c.Rule.Kind == BozjaRuleKind.CircleBait).ToArray();
        Check(homing.NumCasts == f.Native.LightHomingCount && homing.CurrentBaits.Count == baits.Length,
            "Light Homing independent Effect count and full raw bait list");
        for (var i = 0; i < baits.Length; ++i)
        {
            var expected = homing.CurrentBaits[i]; var actual = baits[i];
            Check(expected.Source.InstanceID == actual.Source.Actor.Id && expected.Source.IsDead == actual.Source.Actor.Dead
                && expected.Source.IsDestroyed == actual.Source.Destroyed
                && expected.Target.InstanceID == actual.Target!.Actor.Id && expected.Target.IsDead == actual.Target.Actor.Dead
                && expected.Target.IsDestroyed == actual.Target.Destroyed && expected.Target.Position.ToVec3() == actual.Target.Actor.Position
                && expected.Activation == actual.Activation, "Light exact original ordered source and target " + i);
        }
        var destruct = source.Components.Single(c => c.GetType().Name == "InitiateSelfDestruct");
        Check(((CastCounter)destruct).NumCasts == f.Native.LightSelfDestructCount, "Light self destruct independent exact Effect count");
    }

    private static void LightManifest()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(736)?.TerritoryType.RowId == 917
            && Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9652)?.Singular.ToString() == f.Native.Definition.Name,
            "Light actual client identity");
        foreach (var action in f.Native.Definition.Rules.SelectMany(r => r.Actions))
            Check(new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Light exact actual client extra");
        for (var x = -256f; x <= -204; x += .317f)
            for (var z = -41f; z <= 11; z += .293f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Light exact square boundary");
    }

    private static void LightFire()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        Check(!f.Native.Visible().Any(), "Light does not predict an unspawned fire voidzone");
        var point = f.Center + new Vector3(.137f, 0, -.193f);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x1E8D9B, point) with { Rotation = 1.37f, Dead = true, Targetable = false });
        f.Frame(1);
        Check(f.Native.Visible().Single().Cast.Origin == point && f.Native.Navigation(f.NativeViewer).Single().Obstacle,
            "Light fire preserves raw dead untargetable actor without quantization");
        var ai = new AIHints(); f.Source!.Components.Single(c => c.GetType().Name == "FireVoidzone")
            .AddAIHints(0, f.Viewer, default, ai);
        var zone = f.Native.Navigation(f.NativeViewer).Single();
        for (var x = point.X - 7; x < point.X + 7; x += .337f)
            for (var z = point.Z - 7; z < point.Z + 7; z += .317f)
                Check(ai.TemporaryObstacles.Single().Distance(new(x, z)) == zone.Distance(x, z), "Light raw obstacle SDF");
        f.Change(22, a => a with { EventState = 7 }); f.Frame(2);
        Check(!f.Native.Visible().Any() && !f.Native.Navigation(f.NativeViewer).Any(), "Light EventState seven hides both views and obstacle");
        f.Change(22, a => a with { EventState = 0, Position = point + Vector3.UnitZ * 2 }); f.Frame(3); f.Frame(100);
        Check(f.Native.Visible().Single().Cast.Origin == point + Vector3.UnitZ * 2, "Light fire no TTL follows current actor");
        f.Actors.RemoveAll(a => a.Id == 22); f.Frame(101); Check(!f.Native.Visible().Any(), "Light fire Destroy removes obstacle");
    }

    private static void LightHoming()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(21008, 1) }); f.Frame(1);
        Check(f.Native.CircleBaitViews(f.NativeViewer, f.Native.ActiveRules.Single(r => r.Kind == BozjaRuleKind.CircleBait))
            .Single().Outline, "Light own bait is outline");
        var edge = f.Native.Navigation(f.NativeViewer).Single(z => z.Key == "light/homing/edge");
        Check(edge.Distance(f.Center.X, f.Center.Z) == -23 && edge.Distance(f.Center.X + 23, f.Center.Z) == 0,
            "Light exact instantaneous 23 half width forward backward rectangle");
        f.Change(100, a => a with { Dead = true, Position = f.Center + new Vector3(.137f, 0, .193f) }); f.Frame(2);
        f.Effect(3, 21008); f.Frame(100);
        Check(f.Native.Casts.Count == 1 && f.Native.LightHomingCount == 1, "Light dead target allowed and Effect does not end cast bait");
        f.Change(20, a => a with { Dead = true }); f.Frame(101);
        Check(f.Native.Casts.Count == 1 && !f.Native.Navigation(f.NativeViewer).Any(), "Light source dead hides bait and edge without deleting raw list");
        f.Change(20, a => a with { Dead = false, Cast = null }); f.Frame(102);
        Check(!f.Native.Casts.Any(), "Light actual watched Finish removes first bait");
    }

    private static void LightFireOrder()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x1E8D9B, f.Center + Vector3.UnitX));
        f.Actors.Add(Cll1Fixture.Actor(23, 0x1E8D9B, f.Center + Vector3.UnitZ)); f.Frame(1);
        f.Actors.RemoveAll(a => a.Id == 22);
        f.Actors.Add(Cll1Fixture.Actor(24, 0x1E8D9B, f.Center - Vector3.UnitX)); f.Frame(2);
        Check(f.Native.Visible().Select(a => a.Cast.Source.Actor.Id).SequenceEqual(new ulong[] { 23, 24 }),
            "Light cached enemy list appends new fire after surviving original");
    }

    private static void LightHomingClock()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(21008, 1, 10, target: 101) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(21008, 1, 2) }); f.Frame(1);
        f.Change(20, a => a with { Dead = true }); f.Frame(2);
        var damage = f.Native.DamageForHost(f.NativeViewer).Single();
        Check(damage.Mask == 1 && damage.Activation == f.Native.Casts[0].Activation, "Light prediction active targets but raw first dead source clock");
        var old = f.Native.Actors[100];
        f.Actors.RemoveAll(a => a.Id == 100);
        // Keep the fixture's fixed perspective present while changing generation
        // through a real remove/create pair without querying that missing slot.
        f.Frame(3, compare: false);
        f.Actors.Add(Cll1Fixture.Actor(100, 0, f.Center + Vector3.UnitX * 8) with { Kind = 1, SubKind = 4 }); f.Frame(4);
        Check(old.Destroyed && ReferenceEquals(f.Native.Casts[1].Target, old)
            && !f.Native.Navigation(f.NativeViewer).Any(z => z.Key == "light/homing/edge"),
            "Light replacement target same ID does not become original bait target for edge AI");
    }

    private static void LightSelfBait()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery);
        f.Change(20, a => a with { Cast = f.Spell(21008, target: 20) });
        f.Frame(0);
        var sourceViewer = f.Native.Actors[20];
        var actual = f.Native.Navigation(sourceViewer).ToArray();
        Check(actual.Length == 1 && actual[0].Key == "light/homing/edge",
            "Light source equals target skips generic target AI but custom edge still applies");
        var ai = new AIHints();
        f.Source!.Components.OfType<BaitAwayCast>().Single().AddAIHints(-1, f.Session.World.Actors.Find(20)!, default, ai);
        Check(ai.ForbiddenZones.Count == 1, "Light exact source self target custom AI");
    }

    private static void LightCounter()
    {
        foreach (var count in new[] { 3, 4 })
        {
            using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
            f.Frame(1, packets: Enumerable.Range(0, count).Select(_ =>
                (MechanicFeedEvent)new MechanicEventActionEffect(20, 1, 21013, 0, 0, default, 0, 0, 0, [])).ToArray(),
                native: () => { for (var i = 0; i < count; ++i) f.Region.Effect(20, 21013); });
            Check(f.Native.Active == (count == 4) && f.Native.LightSelfDestructCount == count,
                "Light exact equality three not greater than or equal");
        }
    }

    private static void LightDestroyed()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        f.Change(1, a => a with { Hp = 0, Dead = true }); f.Frame(1);
        Check(f.Native.Active, "Light HP0 and Dead body do not replace Destroyed completion");
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(2);
        Check(!f.Native.Active, "Light all Boss Destroyed completes");
    }

    private static void LightCold()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.LightArtillery);
        f.Change(20, a => a with { Cast = f.Spell(21008, target: 999) }); f.Frame(0); f.Effect(.5, 21013);
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.LightSelfDestructCount == 0 && !f.Native.Casts.Any(), "Light cold activation does not reconstruct missing target or historical Effect");
    }

    private static void LightTankbuster()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.LightArtillery); f.Frame(0);
        f.Change(20, a => a with { TargetId = 101, Cast = f.Spell(21011, total: 0, target: 20) }); f.Frame(.1);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Mask == 2, "Light current raw self target falls back to caster TargetID");
        var source = f.Source!.Components.Single(c => c.GetType().Name == "ManeuverMartialArm");
        source.OnCastStarted(f.Session.World.Actors.Find(20)!, f.Session.World.Actors.Find(20)!.CastInfo!);
        typeof(BozjaEncounter).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native,
            [f.Native.Actors[20], f.Native.ActiveRules.Single(r => r.Component == "ManeuverMartialArm")]);
        f.Change(20, a => a with { Cast = f.Spell(999, 1, .02f, target: 101) with { IsSpell = false } }); f.Frame(2);
        Check(f.Native.DamageForHost(f.NativeViewer).Single().Activation == START.AddSeconds(2).AddSeconds((.02f + .3f) - .02f),
            "Light retained original caster consumes non Spell capped NPC clock");
    }
}
