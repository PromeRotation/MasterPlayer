using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;

internal static partial class BozjaRegression
{
    private static void RunIrminsul(Action<string, Action> run)
    {
        run("Irminsul source eight actual components client and primary-only default lifecycle", IrminsulManifest);
        foreach (var rule in IrminsulCatalog.Definition.Rules)
            run($"Irminsul source {rule.Component} cold hot duplicate ordinary Finish", () =>
                AllianceStandard(IrminsulCatalog.Definition, rule, rule.Actions.Single()));
        run("Irminsul source resolved ring triangles Contains and exact row ray all quadrants", IrminsulRay);
        run("Irminsul source ring ray corners ULP near parallel and collinear origins", IrminsulRayCorners);
        run("Irminsul source LOS original caster FIFO safe snapshot current raw selection and blocker generations", IrminsulSight);
        run("Irminsul source LOS direct navigation preserves source danger classification inside ring", IrminsulSightNavigation);
        run("Irminsul source LOS residual duplicate non Spell raw caster foreign Finish and destroyed generation", IrminsulSightResidual);
        var kb = IrminsulCatalog.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.AllianceKnockback);
        run("Irminsul source KB exact inner outer wall sequential no navigation", () => AllianceKnockbackEndpoints(IrminsulCatalog.Definition, kb));
        run("Irminsul source KB immunity60 strict expiry old Party and cold Pet", () => AllianceKnockbackImmunity(IrminsulCatalog.Definition, kb));
        run("Irminsul source KB original foreign ID ordinary Finish no Effect removal", () => AllianceKnockbackFinishIdentity(IrminsulCatalog.Definition, kb));
        var damage = IrminsulCatalog.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Raidwide);
        run("Irminsul source Rootstorm raw non Spell current original caster and dynamic clock", () => AlliancePilotDamage(IrminsulCatalog.Definition, damage));
    }

    private static void IrminsulManifest()
    {
        using var f = new CastrumFixture(false, alliance: IrminsulCatalog.Definition);
        f.Actors.Add(Cll1Fixture.Actor(2, 0x13EF, f.Center) with { InCombat = true });
        f.Frame(0);
        Check(!f.Native.Active, "Irminsul secondary combat alone does not pull default primary");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(4623)?.Singular.ToString() == f.Native.Definition.Name,
            "Irminsul actual NameID");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(120)?.TerritoryType.RowId == 508,
            "Irminsul actual CFC territory");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Source!.Components.Select(c => c.GetType().Name).SequenceEqual(f.Native.ActiveRules.Select(r => r.Component))
            && !f.Native.ActiveRules.Any(r => r.Component == "AmbushKnockback"), "Irminsul inactive AmbushKB stays absent");
        foreach (var id in f.Native.Definition.Rules.SelectMany(r => r.Actions))
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(id) != null
                && new ActionID(ActionType.Spell, id).CastTimeExtra() == 0, "Irminsul actual Action extra " + id);
        f.Frame(12000); Check(f.Native.Active, "Irminsul nominal timer never completes");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(12001);
        Check(!f.Native.Active && f.Actors.Single(a => a.Id == 2).Hp != 0, "Irminsul HP0 completes without secondary death");
    }

    private static void IrminsulRay()
    {
        using var f = new CastrumFixture(alliance: IrminsulCatalog.Definition);
        f.Frame(0);
        var native = (AllianceArenaGeometry)f.Native.Arena;
        var arena = f.Source!.Arena;
        Check(native.GetContours().Count == 2, "Irminsul ring hole retained");
        var polygon = ((ArenaBoundsCustom)arena.Bounds).Polygon;
        Check(polygon.Triangulate().SelectMany(t => new[] { t.A, t.B, t.C })
            .Select(p => arena.Center.ToVec3() + p.ToVec3()).SequenceEqual(native.FillPolygons!.SelectMany(t => t)),
            "Irminsul actual source fill triangles");
        for (var x = -40f; x <= 40; x += 2.713f)
            for (var z = 90f; z <= 170; z += 2.791f)
            {
                var point = new Vector3(x, 0, z);
                Check(arena.InBounds(new(x, z)) == native.Contains(point, 0), "Irminsul source ring containment");
                for (var i = 0; i < 32; ++i)
                {
                    var angle = i * (MathF.Tau / 32);
                    var direction = new WDir((float)Math.Sin(angle), (float)Math.Cos(angle));
                    var expected = arena.IntersectRayBounds(new(x, z), direction);
                    var actual = native.Raycast(point, direction.ToVec3());
                    Check(expected == actual, $"Irminsul source ring ray {x:R}/{z:R} dir={i} expected={expected:R} actual={actual:R}");
                }
            }
        Console.WriteLine($"INFO Irminsul ray AVX512F={System.Runtime.Intrinsics.X86.Avx512F.IsSupported} "
            + $"AVX2={System.Runtime.Intrinsics.X86.Avx2.IsSupported} FMA={System.Runtime.Intrinsics.X86.Fma.IsSupported}");
    }

    private static void IrminsulRayCorners()
    {
        using var f = new CastrumFixture(alliance: IrminsulCatalog.Definition);
        f.Frame(0);
        var arena = f.Source!.Arena;
        var native = (AllianceArenaGeometry)f.Native.Arena;
        foreach (var part in ((ArenaBoundsCustom)arena.Bounds).Polygon.Parts)
            foreach (var relative in part.Vertices)
            {
                var point = arena.Center + relative;
                foreach (var x in new[] { MathF.BitDecrement(point.X), point.X, MathF.BitIncrement(point.X) })
                    foreach (var z in new[] { MathF.BitDecrement(point.Z), point.Z, MathF.BitIncrement(point.Z) })
                    {
                        Check(arena.InBounds(new(x, z)) == native.Contains(new(x, 0, z), 0), "Irminsul corner Contains");
                        foreach (var direction in new WDir[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
                            new(1, 1e-7f), new(1, 1.000001e-7f), new(1e-7f, 1), new(1, -1), new(-1, 1) })
                            Check(arena.IntersectRayBounds(new(x, z), direction) == native.Raycast(new(x, 0, z), direction.ToVec3()),
                                $"Irminsul corner Ray {x:R}/{z:R}/{direction}");
                    }
            }
    }

    private static void IrminsulSight()
    {
        using var f = new CastrumFixture(alliance: IrminsulCatalog.Definition);
        f.Change(1, a => a with { HitboxRadius = 9 });
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 20,
            Cast = f.Spell(5213, point: f.Center + Vector3.UnitX * 20) });
        f.Frame(0);
        var source = (CastLineOfSightAOE)f.Source!.Components.Single(c => c.GetType().Name == "ShockwaveStomp");
        var rule = f.Native.ActiveRules.Single(r => r.Component == "ShockwaveStomp");
        var oldShape = f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule == rule).Cast.Shape;
        f.Change(1, a => a with { Position = f.Center + Vector3.UnitZ * 5, HitboxRadius = 4 });
        f.Frame(1);
        Check(ReferenceEquals(oldShape, f.Native.Visible(f.NativeViewer).Single(a => a.Cast.Rule == rule).Cast.Shape),
            "Irminsul blocker moving keeps frozen safe shape until next cast event");
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with
            { Cast = f.Spell(5213, at: 2, total: 1, point: f.Center - Vector3.UnitX * 20) });
        f.Frame(2);
        Check(source.Casters.Count == 2 && source.Safezones.Count == 2 && source.Origin == new WPos(f.Center.X - 20, f.Center.Z),
            "Irminsul earliest current raw selects origin while first snapshot remains visible");
        f.Change(20, a => a with { Cast = f.Spell(50000, at: 3, total: 0, point: f.Center + Vector3.UnitZ * 30) with { IsSpell = false } });
        f.Frame(3);
        f.Change(22, a => a with { Cast = null }); f.Frame(4);
        Check(source.Casters.Count == 0 && source.Safezones.Count == 0, "Irminsul watched Finish removes original refs and global safe FIFO");
        f.Change(20, a => a with { Cast = f.Spell(5213, at: 5) }); f.Frame(5);
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(6);
        f.Actors.Add(Cll1Fixture.Actor(20, 0x233C, f.Center) with { Cast = f.Spell(5213, at: 7) });
        f.Frame(7);
        Check(source.Casters.Count == 1 && !source.Casters[0].IsDestroyed,
            "Irminsul real Destroy Finish consumes original before same ID replacement");
        f.Effect(8, 5213);
        Check(source.Safezones.Count == 1, "Irminsul Effect does not consume safezone");
    }

    private static void IrminsulSightNavigation()
    {
        using var f = new CastrumFixture(alliance: IrminsulCatalog.Definition);
        f.Change(1, a => a with { HitboxRadius = 9 });
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 30,
            Cast = f.Spell(5213, point: f.Center + Vector3.UnitX * 30) });
        f.Frame(0);
        var component = f.Source!.Components.Single(c => c.GetType().Name == "ShockwaveStomp");
        var hints = new AIHints();
        component.AddAIHints(0, f.Viewer, default, hints);
        var source = hints.ForbiddenZones.Single().Item1;
        var zone = f.Native.NavigationForHost(f.NativeViewer).Single();
        var geometry = f.Native.ConvertNavigation(zone, f.Center, f.Native.SampleRadius).Single();
        var contours = geometry.GetContours();
        Check(geometry is IrminsulSightDangerGeometry && contours.Count != 0
            && contours.All(c => c.Count >= 3 && c.All(p => float.IsFinite(p.X) && float.IsFinite(p.Z))),
            "Irminsul direct bounded complement retains finite clipped contours");
        for (var x = -34f; x < 34; x += .713f)
            for (var z = 96f; z < 164; z += .791f)
            {
                var point = new Vector3(x, 0, z);
                if (!f.Native.Arena.Contains(point, 0)) continue;
                Check(geometry.Contains(point, 0) == (source.Distance(new(x, z)) <= 0),
                    $"Irminsul direct LOS source danger classification {x:R}/{z:R}");
            }
        Check(!geometry.Contains(f.Center - Vector3.UnitX * 20, 0)
            && geometry.Contains(f.Center + Vector3.UnitX * 20, 0),
            "Irminsul direct navigation retains safe shadow and forbids exposed ring");
        Check(geometry.IntersectsSegment(f.Center - Vector3.UnitX * 20,
            f.Center + Vector3.UnitX * 20, 0), "Irminsul unsafe segment crosses exact complement");
        foreach (var margin in new[] { 0f, .3f, .5f })
        {
            var a = f.Center + new Vector3(-.1f, 0, -8.7f);
            var b = f.Center + new Vector3(-.1f, 0, 8.7f);
            Check(!geometry.Contains(a, margin) && !geometry.Contains(b, margin)
                && geometry.Contains((a + b) * .5f, margin)
                && geometry.IntersectsSegment(a, b, margin),
                $"Irminsul two safe endpoints cross exposed concave shadow boundary margin{margin}");
            a = f.Center + new Vector3(-20, 0, -3);
            b = f.Center + new Vector3(-20, 0, 3);
            Check(!geometry.Contains(a, margin) && !geometry.Contains(b, margin)
                && !geometry.IntersectsSegment(a, b, margin),
                $"Irminsul complete segment remains inside safe shadow margin{margin}");
        }
    }

    private static void IrminsulSightResidual()
    {
        using var f = new CastrumFixture(alliance: IrminsulCatalog.Definition);
        f.Actors.Add(Cll1Fixture.Actor(23, 0x233C, f.Center));
        f.Change(20, a => a with { Cast = f.Spell(5213, point: f.Center + Vector3.UnitX * 20) });
        f.Frame(0);
        var source = (CastLineOfSightAOE)f.Source!.Components.Single(c => c.GetType().Name == "ShockwaveStomp");
        var caster = f.Session.World.Actors.Find(20)!;
        source.OnCastStarted(caster, caster.CastInfo!);
        f.Native.Start(f.Native.Actors[20]);
        f.Compare();
        f.Change(20, a => a with { Cast = f.Spell(50000, at: 1, total: 0,
            point: f.Center + Vector3.UnitZ * 25) with { IsSpell = false } });
        f.Frame(1);
        Check(source.Casters.Count == 1 && ReferenceEquals(source.Casters[0], caster)
            && caster.CastInfo != null && !caster.CastInfo.IsSpell(), "Irminsul duplicate retains current non Spell original caster");
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(5213, at: 2, total: 2) });
        f.Frame(2);
        Check(source.Origin == new WPos(f.Center.X, f.Center.Z + 25) && source.Safezones.Count == 2,
            "Irminsul refresh selects residual non Spell zero raw clock");
        var rule = f.Native.ActiveRules.Single(r => r.Component == "ShockwaveStomp");
        var sights = (Dictionary<BozjaRule, BozjaSight>)typeof(BozjaEncounter)
            .GetField("crystalSights", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Native)!;
        var native = sights[rule];
        void Raw()
        {
            Check(source.Casters.Select(a => (a.InstanceID, a.IsDestroyed))
                .SequenceEqual(native.Casters.Select(a => (a.Actor.Id, a.Destroyed)))
                && source.Safezones.Count == native.Safezones.Count
                && source.Origin?.ToVec3() == native.Origin,
                "Irminsul independent original caster list safe FIFO and current origin");
        }
        Raw();
        var finish = new ActorCastInfo { Action = new(ActionType.Spell, 5213) };
        source.OnCastFinished(f.Session.World.Actors.Find(23)!, finish);
        f.Native.Finish(f.Native.Actors[23], f.Spell(5213));
        Raw(); f.Compare();
        Check(source.Casters.Count == 2 && source.Safezones.Count == 1,
            "Irminsul foreign original Finish pops global safe without removing caster");
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(3);
        f.Actors.Add(Cll1Fixture.Actor(20, 0x233C, f.Center) with { Cast = f.Spell(5213, at: 4) });
        f.Frame(4); Raw();
        Check(source.Casters.Count == 3 && source.Casters[0].IsDestroyed && !source.Casters[2].IsDestroyed
            && source.Casters[0].InstanceID == source.Casters[2].InstanceID,
            "Irminsul destroyed non Spell residual and same ID replacement remain distinct originals");
    }
}
