using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunAllianceStandardPilot(Action<string, Action> run)
    {
        foreach (var definition in AllianceStandardCatalog.Definitions)
        {
            run($"Alliance standard source {definition.Key} active manifest exact arena and default lifecycle", () =>
                AlliancePilotManifest(definition));
            foreach (var rule in definition.Rules)
                run($"Alliance standard source {definition.Key} {rule.Component} hot cold duplicate ordinary Finish", () =>
                    AllianceStandard(definition, rule, rule.Actions.Single()));
            foreach (var rule in definition.Rules.Where(r => r.Kind == BozjaRuleKind.Stack))
                run($"Alliance standard source {definition.Key} {rule.Component} original targets dead generation target Finish", () =>
                    AlliancePilotStack(definition, rule));
            foreach (var rule in definition.Rules.Where(r => r.Kind == BozjaRuleKind.Charge))
                run($"Alliance standard source {definition.Key} {rule.Component} raw delta locked origin no TTL", () =>
                    AlliancePilotCharge(definition, rule));
            foreach (var rule in definition.Rules.Where(r => r.Kind is BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster))
                run($"Alliance standard source {definition.Key} {rule.Component} original caster raw NPC clock Party order", () =>
                    AlliancePilotDamage(definition, rule));
            foreach (var rule in definition.Rules.Where(r => r.Kind == BozjaRuleKind.CastGaze))
                run($"Alliance standard source {definition.Key} {rule.Component} locked eye raw gaze threshold", () =>
                    AlliancePilotGaze(definition, rule));
        }
        run("Alliance standard source A30Trash hidden prefix promotion risk color Effect retains all Destroy completion",
            AlliancePilotHidden);
    }

    private static void AlliancePilotManifest(BozjaDefinition definition)
    {
        using var f = new CastrumFixture(false, alliance: definition);
        f.Frame(0);
        Check(f.Source != null && !f.Native.Active, "Pilot source module loaded before combat");
        var cfc = definition.Territory switch { 508 => 120u, 556 => 168u, 627 => 220u, 966 => 779u, _ => 0u };
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(cfc)?.TerritoryType.RowId == definition.Territory,
            "Pilot actual client CFC territory");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.Active && f.Source!.Components.Select(c => c.GetType().Name)
            .SequenceEqual(definition.Rules.Select(r => r.Component)), "Pilot exact activation order without inactive declarations");
        foreach (var action in definition.Rules.SelectMany(r => r.Actions))
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(action) != null
                && new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Pilot actual Action and extra " + action);
        var arena = f.Source!.Arena;
        for (var x = f.Center.X - 60; x <= f.Center.X + 60; x += .931f)
            for (var z = f.Center.Z - 60; z <= f.Center.Z + 60; z += 1.037f)
                Check(arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0),
                    $"{definition.Key} exact source boundary {x}/{z}");
        if (definition.Key is "A22Forgall" or "A23Headstone" or "A25Calofisteri")
        {
            Console.WriteLine($"INFO Pilot arena intrinsics AVX512F={System.Runtime.Intrinsics.X86.Avx512F.IsSupported} "
                + $"AVX2={System.Runtime.Intrinsics.X86.Avx2.IsSupported} FMA={System.Runtime.Intrinsics.X86.Fma.IsSupported}");
            foreach (var part in ((ArenaBoundsCustom)arena.Bounds).Polygon.Parts)
                foreach (var v in part.Vertices)
                {
                    var point = arena.Center + v;
                    foreach (var x in new[] { MathF.BitDecrement(point.X), point.X, MathF.BitIncrement(point.X) })
                        foreach (var z in new[] { MathF.BitDecrement(point.Z), point.Z, MathF.BitIncrement(point.Z) })
                            Check(arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0),
                                $"{definition.Key} exact corner adjacent floats {x}/{z}");
                }
        }
        var rowProbes = definition.Key switch
        {
            "A22Forgall" => new Vector2[] { new(-315.0050354003906f, -441.88397216796875f), new(-311.4476623535156f, -443.6774597167969f), new(-288.5523986816406f, -443.6774597167969f) },
            "A23Headstone" => new Vector2[] { new(-161.1993408203125f, 265.4786071777344f), new(-143.8584747314453f, 265.4786071777344f) },
            _ => []
        };
        foreach (var p in rowProbes)
            Check(arena.InBounds(new(p.X, p.Y)) == f.Native.Arena.Contains(new(p.X, 0, p.Y), 0),
                $"{definition.Key} exact source row hull {p.X}/{p.Y}");
        f.Frame(12000);
        Check(f.Native.Active, "Pilot trivial state not deleted by nominal enrage time");
        f.Change(1, a => a with { Hp = 0 }); f.Frame(12001);
        Check(f.Native.Active == (definition.Key == "A30Trash"), "Pilot HP0 default versus all Destroy override");
        if (f.Native.Active)
        {
            f.Change(1, a => a with { Dead = true }); f.Frame(12002);
            Check(f.Native.Active, "A30 dead primary is insufficient");
            f.Actors.RemoveAll(a => a.Id == 1); f.Frame(12003);
            Check(!f.Native.Active, "A30 all objects Destroyed completes");
        }
    }

    private static void AlliancePilotStack(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], target: 100) }); f.Frame(0);
        var ownZone = f.Native.NavigationForHost(f.NativeViewer).Single(z => z.Component == rule.Component && !z.Goal);
        var published = f.Native.ConvertNavigation(ownZone, f.Center, definition.SampleRadius).ToArray();
        Check(published.Length == 1, "Pilot own stack complement survives high coverage sampler rejection");
        for (var x = f.Center.X - 25; x <= f.Center.X + 25; x += .5f)
            for (var z = f.Center.Z - 25; z <= f.Center.Z + 25; z += .5f)
                if (f.Native.Arena.Contains(new(x, 0, z), 0))
                    Check(published.Any(g => g.Contains(new(x, 0, z), 0)) == (ownZone.Distance(x, z) <= 0),
                        "Pilot actual own stack complement preserves buddy union and overlap interior");
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(rule.Actions[0], at: 1, target: 101) });
        f.Frame(1);
        var original = f.Native.Actors[100];
        f.Change(100, a => a with { Position = f.Center + new Vector3(.137f, 0, .271f) });
        f.Frame(2);
        Check(f.Native.DamageForHost(f.NativeViewer).Length == 0, "Stack no invented raidwide prediction");
        f.Change(100, a => a with { Dead = true }); f.Frame(3);
        Check(f.Native.Stacks(rule).Length == 1 && f.Native.Casts.Count(c => c.Rule == rule) == 2,
            "Stack original dead target hidden but retained");
        f.Change(100, a => a with { Dead = false }); f.Frame(4);
        f.Actors.RemoveAll(a => a.Id == 100); f.Frame(5, compare: false);
        f.Actors.Add(Cll1Fixture.Actor(100, 0, f.Center + Vector3.UnitX * 15) with { Kind = 1, SubKind = 4 });
        f.Frame(6);
        Check(!ReferenceEquals(original, f.Native.Actors[100])
            && f.Native.Casts.Single(c => c.Rule == rule && c.Target?.Actor.Id == 100).Target == original
            && original.Destroyed, "Stack target retains original generation");
        var component = (CastStackSpread)f.Source!.Components.Single(c => c.GetType().Name == rule.Component);
        var nativeCaster = f.Native.Actors[22];
        var sourceCaster = f.Session.World.Actors.Find(22)!;
        // Finish ownership is the target ID, independently of the caster.
        foreach (var sourceComponent in f.Source.Components)
            sourceComponent.OnCastFinished(sourceCaster, new ActorCastInfo { Action = new(ActionType.Spell, rule.Actions[0]), TargetID = 100 });
        f.Native.Finish(nativeCaster, f.Spell(rule.Actions[0], target: 100)); f.Compare();
        Check(f.Native.Casts.Single(c => c.Rule == rule).Target?.Actor.Id == 101
            && component.NumFinishedStacks == f.Native.NumFinishedStacks, "Stack foreign caster consumes first target ID only");
        f.Frame(100);
        Check(f.Native.Stacks(rule).Length == 1, "Stack no TTL");
    }

    private static void AlliancePilotCharge(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        var origin = f.Center + new Vector3(.137f, 0, .271f);
        var destination = origin + new Vector3(17.123f, 0, 9.731f);
        f.Change(20, a => a with { Position = origin, Cast = f.Spell(rule.Actions[0], point: destination) });
        f.Frame(0);
        var cast = f.Native.Casts.Single(c => c.Rule == rule);
        Check(cast.Origin != origin && cast.Rotation != .47f, "Charge quantized original caster and raw delta direction");
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitZ * 20 }); f.Frame(1);
        f.Frame(100);
        Check(f.Native.Casts.Single(c => c.Rule == rule) == cast, "Charge locked shape no actor tracking or TTL");
        f.Native.Finish(f.Native.Actors[20], f.Spell(999));
        foreach (var c in f.Source!.Components)
            c.OnCastFinished(f.Session.World.Actors.Find(20)!, new ActorCastInfo { Action = new(ActionType.Spell, 999) });
        f.Compare();
        Check(f.Native.Casts.Contains(cast), "Charge unrelated Finish retained");
        f.Change(20, a => a with { Cast = null }); f.Frame(101);
        Check(!f.Native.Casts.Any(c => c.Rule == rule), "Charge watched Finish consumes");
    }

    private static void AlliancePilotDamage(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        f.Party.Reverse();
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], total: 0) }); f.Frame(0);
        var component = f.Source!.Components.Single(c => c.GetType().Name == rule.Component);
        component.OnCastStarted(f.Session.World.Actors.Find(20)!, f.Session.World.Actors.Find(20)!.CastInfo!);
        typeof(BozjaEncounter).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(BozjaActor), typeof(BozjaRule)])!.Invoke(f.Native, [f.Native.Actors[20], rule]);
        f.Compare();
        f.Change(20, a => a with { TargetId = 101, Cast = f.Spell(999, at: 1, total: .02f, target: 20) with { IsSpell = false } });
        f.Frame(1); f.Frame(2);
        var damage = f.Native.DamageForHost(f.NativeViewer);
        Check(damage.Length == 1 && damage[0].Activation == START.AddSeconds(2).AddSeconds((.02f + .3f) - .02f),
            "Raw duplicate original caster retains capped float NPC clock");
        if (rule.Kind == BozjaRuleKind.Tankbuster)
            Check(damage[0].Mask == 2, "Self target reads original caster current TargetID");
        f.Change(20, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.DamageForHost(f.NativeViewer).Length == (rule.Kind == BozjaRuleKind.Tankbuster ? 0 : 1),
            "Raw null tankbuster omitted while raidwide fallback remains");
    }

    private static void AlliancePilotGaze(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], point: f.Center + Vector3.UnitZ * 4) }); f.Frame(0);
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitZ * 30, Rotation = 2 }); f.Frame(1);
        foreach (var offset in new[] { -.00001f, 0, .00001f })
        {
            f.Change(100, a => a with { Position = f.Center, Rotation = MathF.PI / 4 + offset }); f.Frame(2);
            var hints = new AIHints();
            foreach (var c in f.Source!.Components) c.AddAIHints(0, f.Viewer, default, hints);
            Check(hints.ForbiddenDirections.Select(d => (d.center.Rad, d.halfWidth.Rad, d.activation))
                .SequenceEqual(f.Native.GazeDirections(f.NativeViewer.Actor)), "Gaze exact locked eye forbidden direction");
        }
        f.Frame(100);
        Check(f.Native.Eyes.Count == 1, "Gaze no TTL");
    }

    private static void AlliancePilotHidden()
    {
        var definition = AllianceStandardCatalog.Definitions.Single(d => d.Key == "A30Trash");
        using var f = new CastrumFixture(alliance: definition);
        for (var i = 0; i < 4; ++i)
            f.Actors.Add(Cll1Fixture.Actor((ulong)(30 + i), 0x3194, f.Center) with
            { Cast = f.Spell(23550, total: 9 - i, point: f.Center + Vector3.UnitX * i) });
        f.Frame(0);
        var visible = f.Native.Visible(f.NativeViewer).ToArray();
        Check(visible.Length == 2 && visible[0].Danger && !visible[1].Danger && visible.All(v => v.Risky)
            && f.Native.Casts.Count == 4, "LightfastBlade original unsorted prefix two first Danger");
        f.Effect(1, 23550, 30);
        Check(f.Native.Casts.Count == 4, "LightfastBlade Effect does not consume");
        f.Change(30, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.Visible(f.NativeViewer).First().Cast.Source.Actor.Id == 31, "Hidden promotion retains original order");
        f.Change(1, a => a with { Dead = true, Hp = 0 }); f.Frame(3);
        foreach (var id in new ulong[] { 30, 31, 32, 33 })
            f.Change(id, a => a with { Dead = true, Hp = 0 });
        f.Frame(4); Check(f.Native.Active, "A30 all Dead HP0 does not complete");
        f.Actors.RemoveAll(a => a.Oid is 0x3193 or 0x3194); f.Frame(5);
        Check(!f.Native.Active && f.Native.Casts.Count == 0, "A30 only all Destroy completes with owner output withdrawn");
    }
}
