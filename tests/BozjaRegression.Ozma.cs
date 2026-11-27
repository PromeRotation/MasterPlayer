using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunOzma(Action<string, Action> run)
    {
        run("Ozma source six active components client tables and permanent phase", OzmaManifest);
        foreach (var rule in OzmaCatalog.Definition.Rules.Where(r => r.Actions.Length > 0))
            run($"Ozma source standard {rule.Component} hot cold duplicate Finish", () =>
                AllianceStandard(OzmaCatalog.Definition, rule, rule.Actions[0]));
        run("Ozma source three exact arenas holes islands triangles vertex ULP rows and slot history", OzmaArena);
        run("Ozma source destroyed current primary AND Party0 area CFC with manager same ID rebinding", OzmaLifecycle);
        run("Ozma source CFC alone does not complete missing Party0 pauses", OzmaCfc);
        run("Ozma source bomb60 cold hot Pet raw target cast expiry and dead query cleanup", OzmaBombStates);
        run("Ozma source bomb cold Pet restoration and hot Lose filtering", OzmaBombPet);
        run("Ozma source bomb old Party slots same SID overwrite Lose preserves slot history", OzmaBombSlots);
        var holy = OzmaCatalog.Definition.Rules.Single(r => r.Component == "Holy");
        run("Ozma source Holy raw current original caster diagnostic clocks", () => AlliancePilotDamage(OzmaCatalog.Definition, holy));
        run("Ozma source Holy multiple caster dynamic NPC clocks", () => AllianceKnockbackDamage(OzmaCatalog.Definition, holy));
        var kb = OzmaCatalog.Definition.Rules.Single(r => r.Component == "HolyKB");
        run("Ozma source HolyKB exact sequential endpoints no navigation", () => AllianceKnockbackEndpoints(OzmaCatalog.Definition, kb));
        run("Ozma source HolyKB immunity all60 strict expiry cold Lose", () => AllianceKnockbackImmunity(OzmaCatalog.Definition, kb));
        run("Ozma source HolyKB foreign same ID watched Finish and unrelated AID", () => AllianceKnockbackFinishIdentity(OzmaCatalog.Definition, kb));
    }

    private static void OzmaManifest()
    {
        using var f = new CastrumFixture(false, alliance: OzmaCatalog.Definition);
        f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(168)?.TerritoryType.RowId == 556,
            "Ozma client CFC territory");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(4896)?.Singular.ToString() == f.Native.Definition.Name,
            "Ozma client NPC name");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Source!.Components.Select(c => c.GetType().Name)
            .SequenceEqual(f.Native.ActiveRules.Select(r => r.Component)), "Ozma six actually activated components only");
        foreach (var id in f.Native.Definition.Rules.SelectMany(r => r.Actions))
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(id) != null
                && new ActionID(ActionType.Spell, id).CastTimeExtra() == 0, "Ozma Action extra " + id);
        f.Frame(12000);
        Check(f.Native.Active, "Ozma permanent trivial phase");
        f.Change(1, a => a with { Hp = 0, Dead = true }); f.Frame(12001);
        Check(f.Native.Active, "Ozma dead HP0 is not Destroyed");
    }

    private static void OzmaDraw(CastrumFixture f, ulong id = 100)
    {
        var viewer = f.Session.World.Actors.Find(id)!;
        f.Source!.Components.Single(c => c.GetType().Name == "ArenaChanges")
            .DrawArenaBackground(f.Source.Raid.FindSlot(id), viewer);
        f.Native.ObserveOzmaDrawing(f.Native.Actors[id]);
        var expected = (uint[])f.Source.Components.Single(c => c.GetType().Name == "ArenaChanges").GetType()
            .GetField("arenaStatus", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(
                f.Source.Components.Single(c => c.GetType().Name == "ArenaChanges"))!;
        Check(expected.SequenceEqual(f.Native.OzmaArenaStatus), "Ozma exact per-slot shared arena status history");
        f.Compare();
    }

    private static void OzmaArena()
    {
        using var f = new CastrumFixture(alliance: OzmaCatalog.Definition);
        f.Frame(0);
        void Geometry(int count)
        {
            var arena = f.Source!.Arena;
            var native = (AllianceArenaGeometry)f.Native.Arena;
            Check(native.GetContours().Count == count && native.FillPolygons != null, "Ozma all contours and resolved fill");
            var expected = ((ArenaBoundsCustom)arena.Bounds).Polygon;
            Check(expected.Triangulate().Select(t => new[] { t.A, t.B, t.C })
                .SelectMany(t => t).Select(p => arena.Center.ToVec3() + p.ToVec3())
                .SequenceEqual(native.FillPolygons!.SelectMany(t => t)), "Ozma exact source triangulation with holes");
            for (var x = arena.Center.X - 45; x < arena.Center.X + 45; x += .713f)
                for (var z = arena.Center.Z - 45; z < arena.Center.Z + 45; z += .791f)
                    Check(arena.InBounds(new(x, z)) == native.Contains(new(x, 0, z), 0), "Ozma source grid topology and row hull");
            foreach (var p in expected.Parts.SelectMany(p => p.Vertices))
            {
                var point = arena.Center + p;
                foreach (var x in new[] { MathF.BitDecrement(point.X), point.X, MathF.BitIncrement(point.X) })
                    foreach (var z in new[] { MathF.BitDecrement(point.Z), point.Z, MathF.BitIncrement(point.Z) })
                        Check(arena.InBounds(new(x, z)) == native.Contains(new(x, 0, z), 0),
                            $"Ozma exact source corner {x:R}/{z:R}");
            }
            var atHeight = native.AtHeight(17);
            Check(atHeight.GetContours().SelectMany(c => c).All(p => p.Y == 17)
                && atHeight.FillPolygons!.SelectMany(c => c).All(p => p.Y == 17), "Ozma relative shape height retains holes and triangles");
        }
        Geometry(2);
        f.Change(100, a => a with { Position = new(300, 0, 260) }); f.Frame(1); OzmaDraw(f); Geometry(3);
        f.Change(101, a => a with { Position = new(301.5f, 0, 205.5f) }); f.Frame(2); OzmaDraw(f, 101); Geometry(2);
        var shared = f.Native.Arena;
        OzmaDraw(f);
        Check(ReferenceEquals(shared, f.Native.Arena), "Ozma slot0 already split keeps slot1's shared Shade arena");
        f.Change(100, a => a with { Position = f.Center }); f.Frame(3); OzmaDraw(f); Geometry(2);
        f.Change(100, a => a with { Position = new(300, 0, 239) }); f.Frame(4); OzmaDraw(f);
        Check(f.Native.OzmaArenaStatus[0] == 0, "Ozma four-unit gap between source rectangle and shade square");
        f.Change(100, a => a with { Position = new(334, 0, 279) }); f.Frame(5); OzmaDraw(f);
        Check(f.Native.OzmaArenaStatus[0] == 1, "Ozma inclusive split rectangle boundary");
    }

    private static void OzmaLifecycle()
    {
        using var f = new CastrumFixture(alliance: OzmaCatalog.Definition);
        f.Frame(0);
        var original = f.Native.Primary;
        f.Change(1, a => a with { Hp = 0, Dead = true }); f.Frame(1);
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(2);
        Check(f.Native.Active && ReferenceEquals(original, f.Native.Primary), "Ozma destroyed original remains active in arena");
        f.Actors.Add(Cll1Fixture.Actor(1, 0x1560, f.Center) with { InCombat = true }); f.Frame(3);
        Check(f.Native.Active && !ReferenceEquals(original, f.Native.Primary)
            && ReferenceEquals(f.Native.Primary, f.Native.Actors[1]), "Ozma source manager rebinds Primary to same ID new Actor");
        f.Change(101, a => a with { Position = new(2000, 0, 2000) }); f.Frame(4);
        Check(f.Native.Active, "Ozma viewer1 outside cannot complete while Party0 remains");
        f.Change(100, a => a with { Position = new(300, 0, 265) }); f.Frame(5);
        Check(f.Native.Active, "Ozma second Party0 square also keeps original active");
        f.Change(100, a => a with { Position = new(380.01f, 0, 265) }); f.Frame(6);
        Check(f.Native.Active, "Ozma rebound living Primary means old destruction and Party0 outside are insufficient");
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(7);
        Check(!f.Native.Active, "Ozma current Primary Destroyed AND Party0 outside both squares completes");
    }

    private static void OzmaCfc()
    {
        // Supply the CFC at the initial real Feed frame. Changing it later also
        // emits ZoneInit and forces the source manager to unload before Raw.Update.
        foreach (var missing in new[] { false, true })
        {
            using var f = new CastrumFixture(alliance: OzmaCatalog.Definition) { ContentFinderOverride = 0 };
            f.Frame(0);
            Check(f.Native.Active, "Ozma CFC alone insufficient");
            if (missing)
            {
                f.Actors.RemoveAll(a => a.Id == 100); f.Frame(1, compare: false);
            }
            f.Actors.RemoveAll(a => a.Id == 1); f.Frame(2, compare: !missing);
            if (missing)
            {
                Check(f.Native.Active && f.Source!.StateMachine.ActiveState != null, "Ozma missing Party0 pauses completion");
                f.Actors.Add(Cll1Fixture.Actor(100, 0, f.Center) with { Kind = 1, SubKind = 4 }); f.Frame(3);
            }
            Check(!f.Native.Active, "Ozma destroyed original CFC mismatch completes despite in-bounds Party0");
        }
    }

    private static void OzmaBombStates()
    {
        using var f = new CastrumFixture(false, alliance: OzmaCatalog.Definition);
        var status = new ReplayStatus(1072, 0, 1, START.AddSeconds(2)) { SlotIndex = 59 };
        f.Change(100, a => a with { Statuses = [status] }); f.Frame(0);
        Check(f.Native.OzmaBomb[0] == default, "Ozma inactive bomb not restored early");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.OzmaSpecialMode(f.NativeViewer)?.Mode == "Pyretic", "Ozma slot59 cold status restored");
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 15 }); f.Frame(3);
        Check(f.Native.OzmaBomb[0] != default && f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stop everything!" && !h.Risky),
            "Ozma expired without Lose remains; finite movement does not fix source self-comparison");
        f.Change(100, a => a with { TargetId = 1 }); f.Frame(4);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stop everything!" && h.Risky), "Ozma selected target marks risk");
        f.Change(100, a => a with { TargetId = 0, Cast = f.Spell(999, 5) }); f.Frame(5);
        Check(f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stop everything!" && h.Risky), "Ozma current raw player cast marks risk");
        f.Change(100, a => a with { Dead = true, Cast = null }); f.Frame(6, compare: false);
        Check(f.Native.OzmaSpecialMode(f.NativeViewer) != null, "Ozma AI before dead hint still sees mode");
        f.Compare();
        Check(f.Native.OzmaSpecialMode(f.NativeViewer) == null && !f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Stop everything!"),
            "Ozma first dead hint clears slot before second public query");
        f.Change(100, a => a with { Dead = false, Statuses = [] }); f.Frame(7);
        Check(f.Native.OzmaBomb[0] == default, "Ozma real Lose clears");
    }

    private static void OzmaBombSlots()
    {
        using var f = new CastrumFixture(alliance: OzmaCatalog.Definition);
        f.Frame(0);
        var early = new ReplayStatus(1072, 0, 1, START.AddSeconds(10)) { SlotIndex = 0 };
        var late = early with { ExpiresAt = START.AddSeconds(20), SlotIndex = 59 };
        f.Change(100, a => a with { Statuses = [late] }); f.Frame(1);
        var previous = f.Native.OzmaBomb[0];
        f.Party[0] = new(1, "", 100); f.Party[1] = new(0, "", 101);
        f.Change(100, a => a with { Statuses = [early] }); f.Frame(2);
        Check(f.Native.OzmaBomb[0] == default && f.Native.OzmaBomb[1] == default,
            "Ozma lower-slot Gain then high-slot Lose clears old Party slot before roster commit");
        Check(previous != default, "Ozma high real status slot was present");
        f.Change(100, a => a with { Statuses = [late] }); f.Frame(3);
        Check(f.Native.OzmaBomb[0] == default && f.Native.OzmaBomb[1] != default, "Ozma next hot gain follows committed new Party slot");
    }

    private static void OzmaBombPet()
    {
        using var f = new CastrumFixture(false, alliance: OzmaCatalog.Definition);
        f.Change(100, a => a with { Kind = 2, SubKind = 2,
            Statuses = [new(1072, 0, 1, START.AddSeconds(20)) { SlotIndex = 59 }] });
        f.Frame(0);
        f.Change(1, a => a with { InCombat = true }); f.Frame(.1);
        Check(f.Native.OzmaBomb[0] != default, "Ozma cold Pet status is replayed outside hot Actor filter");
        f.Change(100, a => a with { Statuses = [] }); f.Frame(.2);
        Check(f.Native.OzmaBomb[0] != default, "Ozma hot Pet Lose remains filtered");
    }
}
