using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Spatial.Geometry;

internal static partial class BozjaRegression
{
    private static void RunDeathgaze(Action<string, Action> run)
    {
        run("Deathgaze source nine actual components client rectangle default lifecycle", DeathgazeManifest);
        foreach (var rule in DeathgazeCatalog.Definition.Rules.Where(r => r.Kind != BozjaRuleKind.AllianceWindSpread))
            run($"Deathgaze source {rule.Component} ordinary cold hot duplicate Finish", () =>
                AllianceStandard(DeathgazeCatalog.Definition, rule, rule.Actions.Single()));
        foreach (var rule in DeathgazeCatalog.Definition.Rules.Where(r => r.Kind == BozjaRuleKind.AllianceKnockback))
        {
            run($"Deathgaze source {rule.Component} exact rectangle sequential endpoints locked side", () =>
                AllianceKnockbackEndpoints(DeathgazeCatalog.Definition, rule));
            run($"Deathgaze source {rule.Component} immunity60 strict expiry old Party cold Pet Finish", () =>
            {
                AllianceKnockbackImmunity(DeathgazeCatalog.Definition, rule);
                AllianceKnockbackPet(DeathgazeCatalog.Definition, rule);
                AllianceKnockbackSlots(DeathgazeCatalog.Definition, rule);
                AllianceKnockbackFinishIdentity(DeathgazeCatalog.Definition, rule);
            });
        }
        run("Deathgaze source rectangle ray parallel corners outside reverse ULP", DeathgazeRectangle);
        run("Deathgaze source left right locked cast rotation and center origins", DeathgazeSides);
        run("Deathgaze source green icon actor mask target Effect slot replacement dead 24 versus 64 no navigation", DeathgazeGreen);
        run("Deathgaze source cold activation never restores old green Icon", DeathgazeCold);
        run("Deathgaze source same frame Party replacement Icon and Effect observe submitted slots", DeathgazePartyEvents);
    }

    private static void DeathgazeManifest()
    {
        using var f = new CastrumFixture(false, alliance: DeathgazeCatalog.Definition);
        f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(5507)?.Singular.ToString() == f.Native.Definition.Name
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(220)?.TerritoryType.RowId == 627
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(627)?.ContentFinderCondition.RowId == 220,
            "Deathgaze actual client identity");
        foreach (var action in f.Native.Definition.Rules.SelectMany(r => r.Actions))
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(action) != null
                && new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Deathgaze actual Action extra " + action);
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.ActiveRules.Count() == 9 && !f.Native.ActiveRules.Any(r => r.Actions.Contains(7315u)),
            "Deathgaze commented BoltOfDarkness3 stays absent");
        f.Frame(12000); Check(f.Native.Active, "Deathgaze nominal timeout never completes");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(12001); Check(!f.Native.Active, "Deathgaze default HP0 completion");
    }

    private static void DeathgazeRectangle()
    {
        using var f = new CastrumFixture(alliance: DeathgazeCatalog.Definition);
        f.Frame(0);
        var arena = f.Source!.Arena;
        var rectangle = (RectangleGeometry)f.Native.Arena;
        var left = f.Center.X - 30; var right = f.Center.X + 30;
        var bottom = f.Center.Z - 15; var top = f.Center.Z + 15;
        Check(MathF.BitIncrement(left) > left && MathF.BitDecrement(right) < right,
            "Deathgaze world ULP points remain distinct");
        foreach (var x in new[] { left - 1, MathF.BitDecrement(left), left, MathF.BitIncrement(left), f.Center.X,
            MathF.BitDecrement(right), right, MathF.BitIncrement(right), right + 1 })
            foreach (var z in new[] { bottom - 1, MathF.BitDecrement(bottom), bottom, MathF.BitIncrement(bottom), f.Center.Z,
                MathF.BitDecrement(top), top, MathF.BitIncrement(top), top + 1 })
                foreach (var direction in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ,
                    new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1e-20f, 0, 1) })
                {
                    var point = new Vector3(x, 0, z);
                    Check(arena.InBounds(new(point.X, point.Z)) == rectangle.Contains(point, 0), "Deathgaze exact rectangle Contains");
                    Check(arena.IntersectRayBounds(new(point.X, point.Z), new(direction.X, direction.Z))
                        == BozjaEncounter.AllianceRectangleRay(rectangle, point, direction), "Deathgaze exact rectangle RayRect/NaN");
                }
    }

    private static BossComponent DeathgazeSource(CastrumFixture f)
        => f.Source!.Components.Single(c => c.GetType().Name == "VoidAeroII");

    private static void DeathgazeSides()
    {
        foreach (var rule in DeathgazeCatalog.Definition.Rules.Where(r => r.KnockbackSide != 0))
        {
            using var f = new CastrumFixture(alliance: DeathgazeCatalog.Definition);
            f.Change(100, a => a with { Position = f.Center });
            for (var i = 0; i < 32; ++i)
            {
                var angle = i * MathF.Tau / 32;
                f.Change(20, a => a with { Rotation = angle + 1, Position = f.Center + Vector3.UnitX * 4,
                    Cast = f.Spell(rule.Actions.Single(), at: i) with { Rotation = angle } });
                f.Frame(i);
                var cast = f.Native.Casts.Single(c => c.Rule == rule);
                Check(cast.Rotation == angle && f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Count() == 1,
                    "Deathgaze center origin side direction uses spell rotation");
                f.Change(20, a => a with { Rotation = angle - 1, Position = f.Center - Vector3.UnitX * 4 });
                f.Frame(i + .1);
                Check(f.Native.Casts.Single(c => c.Rule == rule).Rotation == angle,
                    "Deathgaze actor turning never changes locked side direction");
            }
        }
    }

    private static void DeathgazeCheckGreen(CastrumFixture f)
    {
        var source = DeathgazeSource(f);
        var mask = (BitMask)source.GetType().GetField("_greenTargets", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source)!;
        Check(mask.Raw == f.Native.DeathgazeGreenTargets, "Deathgaze exact source slot mask");
        var sink = new MechanicDrawSink(new());
        var viewerSlot = f.Source!.Raid.FindSlot(100);
        using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(viewerSlot, f.Viewer);
        var circles = sink.Commands.OfType<MechanicDrawZoneCircle>().ToArray();
        var targets = f.Native.DeathgazeGreenPlayers().ToArray();
        Check(circles.Length == targets.Length, "Deathgaze source alive Party64 foreground count");
        for (var i = 0; i < circles.Length; ++i)
        {
            var circle = circles[i]; var target = targets[i];
            Check(circle.Center.ToVec3() == target.Actor.Actor.Position with { Y = 0 } && circle.Radius == 5
                && circle.Outline && circle.Color == Colors.Safe && circle.Thickness == (target.Slot == viewerSlot ? 2 : 1),
                "Deathgaze exact source green foreground position radius outline thickness color order");
        }
        foreach (var party in f.Party)
        {
            var color = 0u;
            var actor = f.Session.World.Actors.Find(party.EntityId);
            if (actor == null) continue;
            var priority = source.CalcPriority(viewerSlot, f.Viewer, party.Slot, actor, ref color);
            Check((priority == BossComponent.PlayerPriority.Interesting) ==
                ((f.Native.DeathgazeGreenTargets & (1UL << party.Slot)) != 0), "Deathgaze source Interesting mask");
        }
        Check(!f.Native.Navigation(f.NativeViewer).Any(), "Deathgaze icon circles never fabricate spread navigation");
    }

    private static void DeathgazeIcon(CastrumFixture f, double at, ulong actor, ulong target = 107, uint icon = 70)
    {
        f.Frame(at, packet: new MechanicEventIcon(icon, target, actor), native: () => f.Region.Icon(actor, icon, target));
        DeathgazeCheckGreen(f);
    }

    private static void DeathgazeGreen()
    {
        using var f = new CastrumFixture(alliance: DeathgazeCatalog.Definition);
        foreach (var (id, slot) in new[] { (108UL, 23), (109UL, 24), (110UL, 63) })
        {
            f.Actors.Add(Cll1Fixture.Actor(id, 0, f.Center + Vector3.UnitX * 10) with { Kind = 1, SubKind = 4, HitboxRadius = .5f });
            f.Party.Add(new(slot, "", id));
        }
        f.Actors.Add(Cll1Fixture.Actor(111, 0, f.Center) with { Kind = 1, SubKind = 4 });
        f.Frame(0);
        DeathgazeIcon(f, 1, 100, 101);
        Check(f.Native.DeathgazeGreenTargets == 1, "Deathgaze Icon uses actor instead of TargetId");
        DeathgazeIcon(f, 2, 100);
        DeathgazeIcon(f, 3, 111);
        Check(f.Native.DeathgazeGreenTargets == 1, "Deathgaze duplicate idempotent unlisted slot not shift63");
        DeathgazeIcon(f, 4, 108); DeathgazeIcon(f, 5, 109); DeathgazeIcon(f, 6, 110);
        Check(f.Native.Hints(f.NativeViewer).Single().Risky, $"Deathgaze alive alliance slot23 can clip viewer "
            + $"mask={f.Native.DeathgazeGreenTargets:X16} viewer={f.NativeViewer.Actor.Position} "
            + $"targets={string.Join(';', f.Native.DeathgazeGreenPlayers().Select(p => $"{p.Slot}/{p.Actor.Actor.Id}/{p.Actor.Actor.Position}"))}");
        f.Change(108, a => a with { Dead = true }); f.Frame(7); DeathgazeCheckGreen(f);
        Check(!f.Native.Hints(f.NativeViewer).Single().Risky,
            "Deathgaze dead alliance target hidden and alive slot24/63 never participate in Hint24");
        f.Change(100, a => a with { Dead = true }); f.Frame(8); DeathgazeCheckGreen(f);
        Check((f.Native.DeathgazeGreenTargets & 1) != 0, "Deathgaze death never clears mask");
        f.Party.RemoveAll(p => p.EntityId == 100);
        f.Party.Add(new(0, "", 111)); f.Frame(9); DeathgazeCheckGreen(f);
        Check(f.Native.DeathgazeGreenPlayers().Any(p => p.Slot == 0 && p.Actor.Actor.Id == 111),
            "Deathgaze same slot new actor inherits green history");
        f.Frame(10, packet: new MechanicEventActionEffect(20, 1, 7308, 100, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(20, 7308, 100));
        DeathgazeCheckGreen(f);
        Check((f.Native.DeathgazeGreenTargets & 1) != 0, "Deathgaze Effect original ID now absent slot cannot clear new slot history");
        f.Frame(11, packet: new MechanicEventActionEffect(100, 1, 7308, 111, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(100, 7308, 111));
        DeathgazeCheckGreen(f);
        Check((f.Native.DeathgazeGreenTargets & 1) != 0, "Deathgaze source module rejects Player Effect");
        f.Frame(12, packet: new MechanicEventActionEffect(20, 1, 7308, 111, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(20, 7308, 111));
        DeathgazeCheckGreen(f);
        Check((f.Native.DeathgazeGreenTargets & 1) == 0, "Deathgaze any valid NPC Effect clears current MainTarget slot");
        f.Frame(1000); DeathgazeCheckGreen(f);
        Check(f.Native.DeathgazeGreenTargets != 0, "Deathgaze no timeout or dead target cleanup");
    }

    private static void DeathgazeCold()
    {
        using var f = new CastrumFixture(false, alliance: DeathgazeCatalog.Definition);
        f.Frame(0, packet: new MechanicEventIcon(70, 100, 100), native: () => f.Region.Icon(100, 70, 100));
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        DeathgazeCheckGreen(f);
        Check(f.Native.DeathgazeGreenTargets == 0 && !f.Native.Hints(f.NativeViewer).Any(),
            "Deathgaze no invented historical Icon on cold activation");
    }

    private static void DeathgazePartyEvents()
    {
        using var f = new CastrumFixture(alliance: DeathgazeCatalog.Definition);
        f.Actors.Add(Cll1Fixture.Actor(111, 0, f.Center) with { Kind = 1, SubKind = 4 });
        f.Frame(0);
        f.Party.RemoveAll(p => p.Slot == 1);
        f.Party.Add(new(23, "", 111));
        DeathgazeIcon(f, 1, 111);
        Check(f.Native.DeathgazeGreenTargets == 1UL << 23, "Deathgaze same frame Icon sees new alliance slot");
        f.Party.RemoveAll(p => p.EntityId == 111);
        f.Party.Add(new(63, "", 111));
        f.Frame(2, packet: new MechanicEventActionEffect(20, 1, 7308, 111, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(20, 7308, 111));
        DeathgazeCheckGreen(f);
        Check(f.Native.DeathgazeGreenTargets == 1UL << 23,
            "Deathgaze same frame Effect sees new slot63 and preserves old slot23 history");
        f.Party.RemoveAll(p => p.EntityId == 111);
        f.Party.Add(new(23, "", 111));
        f.Frame(3, packet: new MechanicEventActionEffect(20, 1, 7308, 111, 0, default, 0, 0, 0, []),
            native: () => f.Region.Effect(20, 7308, 111));
        DeathgazeCheckGreen(f);
        Check(f.Native.DeathgazeGreenTargets == 0, "Deathgaze same frame Effect clears returned slot23");
    }
}
