using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;

internal static partial class BozjaRegression
{
    private static void RunArachne(Action<string, Action> run)
    {
        run("Arachne source actual ten components client parameters and default lifecycle", ArachneManifest);
        foreach (var rule in ArachneCatalog.Definition.Rules.Where(r => r.Actions.Length == 1))
            run($"Arachne source {rule.Component} cold hot duplicate ordinary Finish", () =>
                AllianceStandard(ArachneCatalog.Definition, rule, rule.Actions.Single()));
        run("Arachne source SpiderWeb draw only strict heights fixed circle shared slot history", ArachneDrawing);
        run("Arachne source both real arenas triangles holes row rays and boundary ULP", ArachneArena);
        run("Arachne source Tremblor raw sequence stage position count cache and no timeout", ArachneTremblor);
        var stack = ArachneCatalog.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Stack);
        run("Arachne source stack min eight original targets dead generation and direct complement", () =>
            AlliancePilotStack(ArachneCatalog.Definition, stack));
        var damage = ArachneCatalog.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Tankbuster);
        run("Arachne source DarkSpike original current non Spell NPC damage clock", () =>
            AlliancePilotDamage(ArachneCatalog.Definition, damage));
        foreach (var kb in ArachneCatalog.Definition.Rules.Where(r => r.Kind == BozjaRuleKind.AllianceKnockback))
        {
            run($"Arachne source {kb.Component} exact wall pull independent sequential endpoints", () =>
                AllianceKnockbackEndpoints(ArachneCatalog.Definition, kb));
            run($"Arachne source {kb.Component} immunity60 expiry old Party cold Pet and Finish identity", () =>
            {
                AllianceKnockbackImmunity(ArachneCatalog.Definition, kb);
                AllianceKnockbackPet(ArachneCatalog.Definition, kb);
                AllianceKnockbackSlots(ArachneCatalog.Definition, kb);
                AllianceKnockbackFinishIdentity(ArachneCatalog.Definition, kb);
            });
        }
    }

    private static void ArachneManifest()
    {
        using var f = new CastrumFixture(false, alliance: ArachneCatalog.Definition);
        f.Frame(0);
        Check(!f.Native.Active && f.Native.Center == ArachneArenas.GroundCenter, "Arachne cold original ground");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(4871)?.Singular.ToString() == f.Native.Definition.Name
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(168)?.TerritoryType.RowId == 556
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(556)?.ContentFinderCondition.RowId == 168,
            "Arachne real client name and bidirectional identity");
        foreach (var action in f.Native.Definition.Rules.SelectMany(r => r.Actions))
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(action) != null
                && new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Arachne eleven Action extra " + action);
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Source!.Components.Select(c => c.GetType().Name).SequenceEqual(f.Native.ActiveRules.Select(r => r.Component))
            && f.Native.ActiveRules.Count() == 10, "Arachne ten activated components only");
        f.Frame(12000); Check(f.Native.Active, "Arachne nominal timer cannot finish");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(12001);
        Check(!f.Native.Active, "Arachne default Primary HP0 completion");
    }

    private static void ArachneDraw(CastrumFixture f, int slot, Vector3 point, bool dead = false)
    {
        var id = (ulong)(100 + slot);
        f.Change(id, a => a with { Position = point, Dead = dead });
        f.Frame((f.Native.Now - START).TotalSeconds);
        var component = f.Source!.Components.Single(c => c.GetType().Name == "SpiderWeb");
        component.DrawArenaBackground(slot, f.Session.World.Actors.Find(id)!);
        f.Native.ObserveArachneDrawing(f.Native.Actors[id]);
        var sourceSlots = (bool[])component.GetType().GetField("isSpiderWebArena",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
        Check(sourceSlots.SequenceEqual(f.Native.ArachneArenaStatus), "Arachne all 24 original slot histories");
        f.Compare();
    }

    private static void ArachneDrawing()
    {
        using var f = new CastrumFixture(alliance: ArachneCatalog.Definition);
        var center = new Vector3(16, 0, -55);
        f.Change(100, a => a with { Position = center with { Y = 11 } }); f.Frame(0);
        Check(!f.Native.ArachneWebArena && f.Source!.Arena.Center.ToVec3() == ArachneArenas.GroundCenter,
            "Arachne cold high input cannot synthesize Draw side effect");
        ArachneDraw(f, 0, center with { Y = 10 });
        Check(!f.Native.ArachneWebArena, "Arachne exact ten keeps false history");
        ArachneDraw(f, 0, new(47, 11, -55));
        Check(!f.Native.ArachneWebArena, "Arachne fixed horizontal circle rejects high outside player");
        ArachneDraw(f, 0, center with { Y = 11 });
        Check(f.Native.ArachneWebArena && f.Native.Center == ArachneArenas.WebCenter,
            "Arachne real background draw resolves web center");
        ArachneDraw(f, 1, center);
        Check(f.Native.ArachneWebArena && !f.Native.ArachneArenaStatus[1],
            "Arachne unchanged false slot cannot restore shared ground");
        ArachneDraw(f, 1, center with { Y = 11 });
        ArachneDraw(f, 0, center with { Y = 9 });
        Check(!f.Native.ArachneWebArena && f.Native.ArachneArenaStatus[1],
            "Arachne another slot can retain true while shared arena is ground");
        ArachneDraw(f, 1, center with { Y = 11 });
        Check(!f.Native.ArachneWebArena, "Arachne unchanged true slot cannot force shared web");
        ArachneDraw(f, 1, center with { Y = 10 });
        Check(f.Native.ArachneArenaStatus[1], "Arachne exact ten retains true history");
        ArachneDraw(f, 1, center with { Y = 9 });
        ArachneDraw(f, 1, center with { Y = 11 }, dead: true);
        Check(f.Native.ArachneWebArena && f.Native.ArachneArenaStatus[1], "Arachne source drawing does not filter dead viewer");
        ArachneDraw(f, 1, new(46, 9, -55), dead: true);
        Check(!f.Native.ArachneWebArena, "Arachne inclusive fixed R30 boundary permits transition");
    }

    private static void ArachneArena()
    {
        Console.WriteLine($"INFO Arachne ray AVX512F={System.Runtime.Intrinsics.X86.Avx512F.IsSupported} "
            + $"AVX2={System.Runtime.Intrinsics.X86.Avx2.IsSupported} FMA={System.Runtime.Intrinsics.X86.Fma.IsSupported}");
        using var f = new CastrumFixture(alliance: ArachneCatalog.Definition);
        f.Frame(0);
        foreach (var web in new[] { false, true })
        {
            ArachneDraw(f, 0, new(16, web ? 11 : 0, -55));
            var source = f.Source!.Arena;
            var native = (AllianceArenaGeometry)f.Native.Arena;
            Check(native.GetContours().Count == (web ? 18 : 1), "Arachne actual normalized contour count");
            Check(((ArenaBoundsCustom)source.Bounds).Polygon.Triangulate().SelectMany(t => new[] { t.A, t.B, t.C })
                .Select(p => source.Center.ToVec3() + p.ToVec3()).SequenceEqual(native.FillPolygons!.SelectMany(t => t)),
                "Arachne real resolved fill triangles preserve web holes");
            for (var x = -15f; x < 47; x += 2.713f)
                for (var z = -86f; z < -24; z += 2.791f)
                {
                    Check(source.InBounds(new(x, z)) == native.Contains(new(x, 0, z), 0), "Arachne grid Contains");
                    for (var i = 0; i < 16; ++i)
                    {
                        var angle = i * (MathF.Tau / 16);
                        var dir = new WDir((float)Math.Sin(angle), (float)Math.Cos(angle));
                        var expected = source.IntersectRayBounds(new(x, z), dir);
                        var actual = native.Raycast(new(x, 0, z), dir.ToVec3());
                        Check(expected == actual, $"Arachne web{web} source wall ray {x:R}/{z:R}/{i} {expected:R}/{actual:R}");
                    }
                }
            foreach (var part in ((ArenaBoundsCustom)source.Bounds).Polygon.Parts)
                foreach (var p in part.Vertices)
                {
                    var point = source.Center + p;
                    foreach (var x in new[] { MathF.BitDecrement(point.X), point.X, MathF.BitIncrement(point.X) })
                        foreach (var z in new[] { MathF.BitDecrement(point.Z), point.Z, MathF.BitIncrement(point.Z) })
                            Check(source.InBounds(new(x, z)) == native.Contains(new(x, 0, z), 0),
                                "Arachne source vertex ULP Contains");
                }
        }
    }

    private static void ArachneTremblor()
    {
        using var f = new CastrumFixture(alliance: ArachneCatalog.Definition);
        f.Actors.Add(Cll1Fixture.Actor(23, 0x233C, f.Center));
        f.Change(20, a => a with { Cast = f.Spell(6198) }); f.Frame(0);
        Check(!f.Native.Visible(f.NativeViewer).Any(), "Arachne later cold ring does not invent sequence");
        f.Change(20, a => a with { Cast = f.Spell(6199, at: 1) }); f.Frame(1);
        var source = (ConcentricAOEs)f.Source!.Components.Single(c => c.GetType().Name == "Tremblor");
        var caster = f.Session.World.Actors.Find(20)!;
        source.OnCastStarted(caster, caster.CastInfo!); f.Native.Start(f.Native.Actors[20]); f.Compare();
        Check(f.Native.Visible(f.NativeViewer).Count() == 1, "Arachne duplicate Start waits for Update publication");
        f.Tick(1.1); Check(f.Native.Visible(f.NativeViewer).Count() == 2, "Arachne Update publishes duplicate sequences");
        void Finish(uint action, Vector3 position)
        {
            var spell = new ActorCastInfo { Action = new(ActionType.Spell, action), Location = position };
            source.OnCastFinished(f.Session.World.Actors.Find(23)!, spell);
            f.Native.Finish(f.Native.Actors[23], f.Spell(action, point: position));
            Check(source.NumCasts == f.Native.NumEarthCasts, "Arachne sequence independent source NumCasts");
            f.Compare();
        }
        Finish(6199, f.Center + Vector3.UnitX * 2);
        Check(source.NumCasts == 1, "Arachne wrong position still counts watched Finish");
        f.Tick(1.2);
        Finish(6199, f.Center);
        Check(f.Native.Visible(f.NativeViewer).All(a => a.Cast.Shape is MasterPlayer.Spatial.SafeZone.CircleTemplate),
            "Arachne phase advancement remains hidden until Update");
        f.Tick(1.3);
        Check(f.Native.Visible(f.NativeViewer).First().Cast.Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate { InnerRadius: 10.5f, OuterRadius: 20.5f },
            "Arachne first ring advances to exact offset donut");
        f.Effect(1.4, 6198, 23);
        Check(source.NumCasts == 2, "Arachne Effect does not advance");
        Finish(6198, f.Center); f.Tick(1.5);
        Check(f.Native.Visible(f.NativeViewer).First().Cast.Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate { InnerRadius: 20.5f, OuterRadius: 30.5f },
            "Arachne last donut exact source dimensions");
        Finish(999, f.Center); Check(source.NumCasts == 3, "Arachne unrelated Finish does not increment");
        Finish(6197, f.Center); f.Tick(1.6);
        Check(f.Native.Visible(f.NativeViewer).Count() == 1, "Arachne only first matching sequence retires");
        f.Tick(1000); Check(f.Native.Visible(f.NativeViewer).Count() == 1, "Arachne no timeout removes second sequence");
    }
}
