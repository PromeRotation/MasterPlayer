using System.Numerics;
using BossMod;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunHeavyArtillery(Action<string, Action> run)
    {
        foreach (var z in new[] { -216.25f, -316.532f, -416.5f })
            run($"Alliance artillery source Heavy initial stage {z} exact arena Map all state values retained center", () => HeavyArena(z));
        run("Alliance artillery source Heavy client identity real two AIDs extra and standard laser", HeavyManifest);
        run("Alliance artillery source Heavy targetable barricade or any combat turret pull HP0 preserved", HeavyPull);
        run("Alliance artillery source Heavy bounds identity completion ignores later stages until Map", HeavyCompletion);
        run("Alliance artillery source Heavy Energy cold created moving dead raw capsule union clocks and Effect", HeavyEnergy);
        run("Alliance artillery source Heavy Energy original generations foreign Effect no TTL destroy", HeavyEnergyGeneration);
        run("Alliance artillery source Heavy strict timewindow prefix hidden earlier item and any Finish", HeavyWindow);
        run("Alliance artillery source Heavy 98 99 100 casters sorting full hidden queue", HeavySorting);
        run("Alliance artillery source Heavy pending construction freezes initial arena before moving primary", HeavyPendingArena);
        run("Alliance artillery source Heavy same OID pending modules freeze arena without replaying inactive history", HeavyMultiple);
        run("Alliance artillery source Heavy pending promotion uses each original constructor stage", HeavyPromotion);
        run("Alliance artillery source Heavy distance unload reconstructs before a second pending move", HeavyReconstruct);
        run("Alliance artillery source Heavy loaded same ID rebinding retains Map and original center", HeavyRebind);
        run("Alliance artillery source Heavy pending same ID replacement constructs new stage", HeavyPendingGeneration);
    }

    private static void HeavyGeometry(CastrumFixture f)
    {
        for (var x = f.Native.Center.X - 14; x < f.Native.Center.X + 14; x += .617f)
            for (var z = f.Native.Center.Z - 47; z < f.Native.Center.Z + 47; z += .673f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Heavy exact source arena parts barriers");
    }

    private static void HeavyArena(float z)
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery);
        f.Change(1, a => a with { Position = new(200, 0, z) }); f.Frame(0);
        HeavyGeometry(f);
        var center = f.Native.Center;
        foreach (var (index, state) in new[] { ((byte)0x0D, 0u), ((byte)0x0E, 0xFFFFFFFFu), ((byte)0x0D, 0x00020001u) })
        {
            f.Frame((f.Native.Now - START).TotalSeconds + 1, packet: new MechanicEventMapEffect(index, state),
                native: () => f.Region.MapEffect(index, state));
            Check(f.Native.Center == center, "Heavy Map changes Bounds without moving original Arena.Center");
            HeavyGeometry(f);
        }
    }

    private static void HeavyManifest()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(736)?.TerritoryType.RowId == 917
            && Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9656)?.Singular.ToString() == f.Native.Definition.Name, "Heavy actual client identity");
        foreach (var action in new uint[] { 20657, 20658 })
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(action) != null
                && new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Heavy actual extra");
        AllianceStandard(AllianceTrashCatalog.HeavyArtillery,
            AllianceTrashCatalog.HeavyArtillery.Rules.Single(r => r.Component == "ManeuverHighPoweredLaser"), 20657);
    }

    private static void HeavyPull()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.HeavyArtillery);
        f.Change(1, a => a with { Targetable = false }); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E85, f.Center) with { Targetable = false }); f.Frame(1);
        Check(!f.Native.Active, "Heavy no targetable barrier or combat boss no pull");
        f.Change(22, a => a with { Targetable = true }); f.Frame(2);
        Check(f.Native.Active, "Heavy barrier alone pulls untargetable inactive primary");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(3);
        Check(f.Native.Active, "Heavy HP0 does not replace overridden Dead completion");
        f.Change(1, a => a with { Dead = true }); f.Frame(4); Check(!f.Native.Active, "Heavy original boss Dead completes");
    }

    private static void HeavyCompletion()
    {
        foreach (var map in new[] { false, true })
        {
            using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
            // Each turret is also a module primary. Compare the original source
            // module explicitly after adding a second same-OID module.
            var source = f.Source!;
            f.Actors.Add(Cll1Fixture.Actor(22, 0x2E83, new(200, 0, -300)));
            f.Frame(1, compare: false);
            if (map)
                f.Frame(2, compare: false, packet: new MechanicEventMapEffect(0x0E, 0),
                    native: () => f.Region.MapEffect(0x0E, 0));
            f.Change(1, a => a with { Dead = true }); f.Frame(3, compare: false);
            Check(f.Native.Active == (source.StateMachine.ActiveState != null)
                && f.Native.Active == map, "Heavy exact Bounds object identity selects all-Boss completion after map");
            if (map)
            {
                f.Change(22, a => a with { Dead = true }); f.Frame(4, compare: false);
                Check(!f.Native.Active && source.StateMachine.ActiveState == null, "Heavy map arena waits for all stages");
            }
        }
    }

    private static void HeavyEnergy()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.HeavyArtillery);
        var origin = f.Center + new Vector3(1.237f, 0, 2.713f);
        f.Change(20, a => a with { Oid = 0x2E84, Position = origin, Rotation = .47f }); f.Frame(0);
        Check(!f.Native.HeavyEnergy.Any(), "Heavy inactive Created not consumed by unstarted component");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.HeavyEnergy.Count == 1 && f.Native.Visible().Single().Cast.Origin == origin,
            "Heavy cold Created preserves raw energy origin");
        f.Change(20, a => a with { Position = origin + new Vector3(.137f, 0, -.193f), Rotation = -1.37f, Dead = true });
        f.Change(100, a => a with { Position = origin }); f.Frame(2);
        var nav = f.Native.NavigationForHost(f.NativeViewer).ToArray();
        Check(nav.Length == 2 && nav[0].Activation == default && nav[1].Activation == START.AddSeconds(2).AddSeconds(1.1d),
            "Heavy Normalize sorts immediate circle before dynamic future capsule");
        f.Frame(100);
        Check(f.Native.HeavyEnergy.Count == 1 && f.Native.NavigationForHost(f.NativeViewer).Last().Activation ==
            START.AddSeconds(100).AddSeconds(1.1d), "Heavy Energy has no TTL and future clock follows query time");
        f.Effect(101, 20658);
        Check(!f.Native.HeavyEnergy.Any() && !f.Native.Visible().Any() && !f.Native.Navigation(f.NativeViewer).Any(),
            "Heavy actual original Energy Effect removes display and both navigation unions");
    }

    private static void HeavyEnergyGeneration()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E84, f.Center) with { Rotation = 1.731f });
        f.Frame(1); var old = f.Native.HeavyEnergy.Single();
        f.Effect(2, 20658); Check(f.Native.HeavyEnergy.Count == 1, "Heavy foreign source Effect does not consume by OID");
        f.Actors.RemoveAll(a => a.Id == 22); f.Frame(3);
        Check(!f.Native.HeavyEnergy.Any() && old.Destroyed, "Heavy Destroy removes original reference");
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E84, f.Center + Vector3.UnitZ * 8)); f.Frame(4);
        Check(f.Native.HeavyEnergy.Count == 1 && !ReferenceEquals(old, f.Native.HeavyEnergy.Single()), "Heavy same ID new generation created once");
        f.Effect(5, 20658, 22); f.Frame(100);
        Check(!f.Native.HeavyEnergy.Any(), "Heavy consumed present actor does not reappear without Created");
    }

    private static void HeavyWindow()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20657, total: 3) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(20657, total: 4) });
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(20657, total: 2) });
        f.Actors.Add(Cll1Fixture.Actor(23, 0x233C, f.Center) with { Cast = f.Spell(20657, total: 3.5f) }); f.Frame(1);
        Check(f.Native.Visible().Count() == 1, "Heavy strict exact 1s boundary stops prefix before hidden earlier clock");
        foreach (var component in f.Source!.Components)
            component.OnCastFinished(f.Session.World.Actors.Find(21)!, new ActorCastInfo { Action = new(ActionType.Spell, 999) });
        f.Native.Finish(f.Native.Actors[21], f.Spell(999)); f.Compare();
        Check(f.Native.Visible().Count() == 3, "Heavy unrelated hidden caster Finish promotes later prefix");
    }

    private static void HeavySorting()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
        for (var i = 0; i < 98; ++i)
            f.Actors.Add(Cll1Fixture.Actor((ulong)(200 + i), 0x233C, f.Center)
                with { Cast = f.Spell(20657, total: i == 97 ? 1 : 3) });
        f.Frame(1); Check(f.Native.Casts[0].Source.Actor.Id == 200, "Heavy 98 keeps insertion order");
        f.Actors.Add(Cll1Fixture.Actor(298, 0x233C, f.Center) with { Cast = f.Spell(20657, at: 2, total: 0) }); f.Frame(2);
        Check(f.Native.Casts[0].Source.Actor.Id == 297, "Heavy 99 sorts full list including hidden earlier clock");
        f.Actors.Add(Cll1Fixture.Actor(299, 0x233C, f.Center) with { Cast = f.Spell(20657, at: 3, total: 0) }); f.Frame(3);
        Check(f.Native.Casts.Count == 100, "Heavy 100 keeps all list items while publishing time window");
    }

    private static void HeavyPendingArena()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.HeavyArtillery);
        f.Change(100, a => a with { Position = new(2000, 0, 0) });
        f.Frame(0);
        f.Change(1, a => a with { Position = new(200, 0, -416.5f) });
        f.Frame(1);
        f.Change(100, a => a with { Position = new(200, 0, -416.5f) });
        f.Frame(2);
        Check(f.Native.Center == f.Center, "Heavy source constructor freezes first arena while pending");
    }

    private static void HeavyMultiple()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E83, new(200, 0, -316.532f)) with { InCombat = true });
        f.Frame(0, compare: false);
        var source = f.Source!;
        Check(source.PrimaryActor.InstanceID == 22 && f.Native.Primary!.Actor.Id == 22
            && f.Session.BossModules.PendingModules.Count(m => m.PrimaryActor.OID == 0x2E83) == 1,
            "Heavy manager promotes reverse pending order and leaves same OID queued");
        f.Change(1, a => a with { Position = new(200, 0, -416.5f) });
        f.Frame(1, compare: false, packet: new MechanicEventMapEffect(0x0E, 0),
            native: () => f.Region.MapEffect(0x0E, 0));
        Check(f.Native.HeavyArenaStage == 4, "Heavy loaded module consumes Map");
        f.Actors.Add(Cll1Fixture.Actor(23, 0x2E84, f.Center)); f.Frame(2);
        f.Change(22, a => a with { Dead = true });
        f.Frame(3);
        Check(f.Native.Active, "Heavy mapped loaded module waits for all stages");
        f.Change(1, a => a with { Dead = true });
        f.Frame(4);
        Check(f.Region.Loaded.Count(m => m.IsHeavyArtillery) == 0
            && source.StateMachine.ActiveState == null, "Heavy mapped module finishes and queued dead primary retires");
    }

    private static void HeavyPromotion()
    {
        foreach (var (initial, moved) in new[] { (-316.532f, -416.5f), (-416.5f, -316.532f) })
        {
            using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
            f.Actors.Add(Cll1Fixture.Actor(22, 0x2E83, new(200, 0, initial)) with { InCombat = true });
            f.Frame(1);
            var queued = f.Session.BossModules.PendingModules.Single(m => m.PrimaryActor.InstanceID == 22);
            f.Change(22, a => a with { Position = new(200, 0, moved) });
            f.Change(1, a => a with { Dead = true }); f.Frame(2);
            f.Frame(3);
            Check(f.Native.Center == queued.Arena.Center.ToVec3() && f.Native.Center.Z == initial,
                "Heavy next pending primary restores its own frozen arena even if completion is immediate");
        }
    }

    private static void HeavyReconstruct()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
        f.Change(1, a => a with { Position = new(200, 0, -416.5f) });
        f.Change(100, a => a with { Position = new(2000, 0, 0) }); f.Frame(1);
        Check(f.Native.Primary == null, "Heavy inactive distance unloads and constructs a fresh pending module");
        f.Change(1, a => a with { Position = f.Center }); f.Frame(2);
        f.Change(100, a => a with { Position = f.Center }); f.Frame(3);
        Check(f.Native.Center.Z == -416.5f, "Heavy new pending keeps reconstruction position across subsequent move");
    }

    private static void HeavyRebind()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery);
        f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E83, new(200, 0, -316.532f))); f.Frame(1);
        f.Frame(2, packet: new MechanicEventMapEffect(0x0D, 0), native: () => f.Region.MapEffect(0x0D, 0));
        var source = f.Source!; var old = f.Native.Primary!;
        var original = f.Actors.Single(a => a.Id == 1);
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(3);
        f.Actors.Add(original with { Position = new(200, 0, -416.5f) }); f.Frame(4);
        Check(ReferenceEquals(source, f.Source) && !ReferenceEquals(old, f.Native.Primary) && old.Destroyed
            && f.Native.Center == f.Center && f.Native.HeavyArenaStage == 3,
            "Heavy loaded same ID rebinds primary without replacing arena Map history");
    }

    private static void HeavyPendingGeneration()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.HeavyArtillery); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E83, new(200, 0, -316.532f)) with { InCombat = true });
        f.Frame(1);
        f.Actors.RemoveAll(a => a.Id == 22); f.Frame(2);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x2E83, new(200, 0, -416.5f)) with { InCombat = true }); f.Frame(3);
        f.Change(1, a => a with { Dead = true }); f.Frame(4); f.Frame(5);
        Check(f.Native.Primary?.Actor.Id == 22 && f.Native.Center.Z == -416.5f,
            "Heavy pending replacement constructs third stage instead of inheriting removed second stage");
    }
}
