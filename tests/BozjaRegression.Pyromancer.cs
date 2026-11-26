using System.Numerics;
using BossMod;
using BossMod.Components;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;

internal static partial class BozjaRegression
{
    private static void RunPyromancer(Action<string, Action> run)
    {
        run("Bozja pending Pyromancer constructor thermal actual draw no host navigation", PyromancerThermal);
        run("Bozja pending Pyromancer real old DrawHost inactive module and Zone precedence", PyromancerZoneDraw);
        run("Bozja pending Pyromancer independent pending construction history promotion", PyromancerPending);
        run("Bozja pending Pyromancer cold constructor raw capped clock locked point pet ability", PyromancerCold);
        run("Bozja pending Pyromancer phase HP0 untargetable and same-ID rebind", PyromancerCompletion);
        run("Bozja pending Pyromancer wipe disposes loaded keeps pending and destroyed far unload", PyromancerUnload);
        foreach (var rule in BozjaPyromancerCatalog.Definition.Rules)
            foreach (var action in rule.Actions)
                run("Bozja pending Pyromancer real Feed phase " + rule.Component + "/" + action,
                    () => SkirmishStandard(37, rule with { Actions = [action] }));
        run("Bozja pending Pyromancer grouped any Finish ninety-nine sorting", PyromancerGrouped);
    }

    private static void ComparePyromancerConstructions(SkirmishFixture f)
    {
        var sources = f.Session.BossModules.LoadedModules.Concat(f.Session.BossModules.PendingModules)
            .Where(m => m.PrimaryActor.OID == 0x2EF7).ToArray();
        Check(sources.Length == f.Native.PyromancerConstructions.Count, "Pyromancer actual module instance count");
        foreach (var module in sources)
        {
            var actor = f.Native.PyromancerConstructions.Keys.Single(a => a.Actor.Id == module.PrimaryActor.InstanceID);
            var construction = f.Native.PyromancerConstructions[actor];
            var thermal = module.Components.OfType<SimpleAOEs>().SingleOrDefault(c => c.GetType().Name == "ThermalShock");
            Check((thermal != null) == construction.ThermalActive, "Pyromancer per-instance ThermalShock activation");
            if (thermal == null) continue;
            Check(thermal.NumCasts == construction.NumCasts && thermal.Casters.Count == construction.Casts.Count,
                "Pyromancer constructor counts");
            for (var i = 0; i < thermal.Casters.Count; ++i)
            {
                var expected = thermal.Casters[i]; var actual = construction.Casts[i];
                Check(expected.ActorID == actual.Source.Actor.Id && expected.Origin.ToVec3() == actual.Origin
                    && expected.Rotation.Rad == actual.Rotation && expected.Activation == actual.Activation,
                    "Pyromancer independent constructor original order origin rotation raw clock");
            }
        }
    }

    private static void PyromancerThermal()
    {
        using var f = new SkirmishFixture(37);
        f.Change(1, a => a with { InCombat = false });
        f.Change(20, a => a with { Cast = f.Spell(21022) }); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.Fate>(1616)?.Name.ToString() == BozjaPyromancerCatalog.Definition.Name,
            "Pyromancer exact client FATE name");
        var sourceAids = typeof(BossModule).Assembly.GetType("BossMod.Shadowbringers.Foray.Skirmish.PyromancerSupreme.AID")!;
        foreach (var action in Enum.GetValues(sourceAids).Cast<object>().Select(Convert.ToUInt32))
            Check(new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, "Pyromancer all source AID client extra " + action);
        ComparePyromancerConstructions(f);
        Check(!f.Native.Active && f.Native.Visible(f.NativeViewer).Single().Cast.Contains(f.Center + Vector3.UnitX * 20)
            && !f.Native.Visible(f.NativeViewer).Single().Cast.Contains(f.Center + Vector3.UnitX * 20.01f),
            "Pyromancer constructor exact R20, not commented range30");
        var capture = MechanicModuleDrawCapture.Capture(f.Source!, 0, default);
        var shapes = new List<MechanicPreviewShape>(); var lines = new List<MechanicPreviewLine>(); var extras = new List<MechanicRadarExtra>();
        MechanicDrawConvert.Convert(capture.Commands, 0, shapes, lines, extras);
        Check(capture.Errors.Count == 0 && shapes.Any(s => s.Shape.Contains(f.Center + Vector3.UnitX * 19.9f, 0)
            && !s.Shape.Contains(f.Center + Vector3.UnitX * 20.1f, 0)), "Pyromancer real old draw capture R20");
        // No auto-AOE history in this builder: any R20 here could only be an
        // incorrect traversal of the inactive boss module's constructor component.
        using (var builder = new AIHintsBuilder(f.Session.World, f.Session.BossModules, f.Session.ZoneModules, null))
        {
            var hints = new AIHints(); builder.Update(hints, 0, false);
            Check(hints.ForbiddenZones.Count == 0 && f.Native.NavigationForHost(f.NativeViewer).Length == 0,
                "Pyromancer actual builder does not consume inactive ThermalShock");
        }
        f.Frame(1, new MechanicEventActionEffect(20, 1, 21022, 100, 0, f.Center, 0, 0, 0, []),
            () => f.Native.Effect(20, 21022));
        ComparePyromancerConstructions(f);
        f.Change(20, a => a with { Dead = true }); f.Frame(2); ComparePyromancerConstructions(f);
        f.Frame(99); ComparePyromancerConstructions(f);
        Check(f.Native.ConstructionCasts.Count() == 1, "Pyromancer constructor Effect Dead and timeout retain");
        f.Change(20, a => a with { Cast = null }); f.Frame(100); ComparePyromancerConstructions(f);
        Check(!f.Native.ConstructionCasts.Any(), "Pyromancer inactive actual Finish removes");
        f.Change(20, a => a with { Cast = f.Spell(21022, 101) }); f.Frame(101);
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(102); ComparePyromancerConstructions(f);
        Check(!f.Native.ConstructionCasts.Any(), "Pyromancer inactive caster Destroy Finish removes");
    }

    private static void PyromancerPending()
    {
        using var f = new SkirmishFixture(37);
        f.Frame(0);
        f.Actors.Add(new() { Id = 21, Oid = 0x2EA1, Kind = 2, SubKind = 5, Hp = 1000, Position = f.Center });
        var primary = f.Actors.Single(a => a.Id == 1);
        f.Actors.Add(primary with { Id = 2, InCombat = false }); f.Frame(1);
        f.Change(21, a => a with { Cast = f.Spell(21022, 2) }); f.Frame(2);
        f.Change(20, a => a with { Cast = f.Spell(21022, 3) }); f.Frame(3);
        f.Actors.Add(primary with { Id = 3, InCombat = false }); f.Frame(4);
        ComparePyromancerConstructions(f);
        var second = f.Native.PyromancerConstructions.Single(kv => kv.Key.Actor.Id == 2).Value;
        var third = f.Native.PyromancerConstructions.Single(kv => kv.Key.Actor.Id == 3).Value;
        Check(second.Casts.Select(c => c.Source.Actor.Id).SequenceEqual(new ulong[] { 21, 20 })
            && third.Casts.Select(c => c.Source.Actor.Id).SequenceEqual(new ulong[] { 20, 21 }),
            "Pyromancer hot-history and later cold-history remain distinct");
        f.Change(1, a => a with { Targetable = false }); f.Frame(5);
        f.Change(21, a => a with { Position = f.Center + Vector3.UnitX * 70 });
        f.Frame(6); ComparePyromancerConstructions(f);
        Check(f.Native.Primary?.Actor.Id == 3 && !f.Native.Active
            && ReferenceEquals(f.Native.PyromancerConstructions[f.Native.Primary], third),
            "Pyromancer reverse pending promotion preserves original construction, not new snapshot");
        f.Change(3, a => a with { InCombat = true }); f.Frame(7); ComparePyromancerConstructions(f);
        Check(f.Native.Active && !third.ThermalActive && second.ThermalActive && !f.Native.ConstructionCasts.Any(),
            "Pyromancer phase removes only its own ThermalShock");
        f.Frame(8, new MechanicEventActionEffect(20, 1, 21022, 100, 0, f.Center, 0, 0, 0, []),
            () => f.Native.Effect(20, 21022)); ComparePyromancerConstructions(f);
        Check(second.NumCasts == 1, "Pyromancer other pending module still receives Effects");
    }

    private static void PyromancerCold()
    {
        using var f = new SkirmishFixture(37);
        var primary = f.Actors[0]; f.Actors.RemoveAt(0);
        f.Change(20, a => a with { Cast = f.Spell(21022) });
        f.Actors.Add(new() { Id = 22, Oid = 0x2EA1, Kind = 2, SubKind = 2, Hp = 1000,
            Position = f.Center, Cast = f.Spell(21022) });
        f.Actors.Add(new() { Id = 23, Oid = 0x2EA1, Kind = 2, SubKind = 5, Hp = 1000,
            Position = f.Center, Cast = f.Spell(21022) with { IsSpell = false } });
        f.Frame(0);
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 80, Rotation = 2, Dead = true });
        f.Actors.Add(primary with { InCombat = false }); f.Frame(99);
        ComparePyromancerConstructions(f);
        var cast = f.Native.ConstructionCasts.Single();
        Check(cast.Origin == f.Center && cast.Rotation == .4f && cast.Activation == f.Native.Now.AddSeconds(5f + .3f - 5f),
            "Pyromancer cold capture locks spell origin rotation and exact capped raw NPC clock, excludes Pet and Ability");
    }

    private static void PyromancerZoneDraw()
    {
        using var f = new SkirmishFixture(37);
        f.Change(1, a => a with { InCombat = false });
        f.Change(20, a => a with { Cast = f.Spell(21022) }); f.Frame(0);
        var oldInterface = Svc.PluginInterface;
        var oldCondition = Svc.Condition;
        void Service(string key, object? value) => typeof(Svc).GetProperty(key)!.SetValue(null, value);
        var config = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "pyromancer-draw-config"));
        Service(nameof(Svc.PluginInterface), ScriptServiceProxy.Create<IDalamudPluginInterface>((key, _) => key switch
        {
            "get_ConfigDirectory" => config,
            _ => throw new NotSupportedException(key),
        }));
        Service(nameof(Svc.Condition), ScriptServiceProxy.Create<ICondition>((_, _) => false));
        try
        {
            var settings = MasterPlayerSettings.Instance;
            var oldRadar = settings.EnableRadar;
            try
            {
                settings.EnableRadar = true;
                using var plugin = new MasterPlayerPlugin();
                plugin.Scripts.ReplayModeActive = true;
                plugin.SafeZone.ReplayClock = () => f.Native.Now;
                using var draw = new MechanicDrawHost(f.Session);
                draw.Update();
                Check(plugin.AoeVisibility!.GetPreviews(f.Native.Now).Any(p => p.Geometry.Contains(f.Center + Vector3.UnitX * 19.9f, 0)
                    && !p.Geometry.Contains(f.Center + Vector3.UnitX * 20.1f, 0)), "Pyromancer real DrawHost publishes constructor circle");
                var oldZone = f.Session.ZoneModules.ActiveModule;
                using var zone = new BossMod.Shadowbringers.Foray.Bozja(f.Session.World);
                try
                {
                    f.Session.ZoneModules.ActiveModule = zone;
                    draw.Update();
                    Check(!plugin.AoeVisibility.GetPreviews(f.Native.Now).Any(), "Pyromancer real Zone precedence withdraws inactive boss drawing");
                }
                finally { f.Session.ZoneModules.ActiveModule = oldZone; }
            }
            finally { settings.EnableRadar = oldRadar; }
        }
        finally
        {
            Service(nameof(Svc.PluginInterface), oldInterface);
            Service(nameof(Svc.Condition), oldCondition);
        }
    }

    private static void PyromancerCompletion()
    {
        using var f = new SkirmishFixture(37);
        f.Frame(0); f.Change(1, a => a with { Hp = 0 }); f.Frame(1);
        Check(f.Native.Active, "Pyromancer HP0 without Dead retains phase");
        var boss = new ReplayActorState { Id = 1, Oid = 0x2EF7, Kind = 2, SubKind = 5,
            Position = f.Center, Targetable = true, InCombat = true, Hp = 1000 };
        var sourceModule = f.Source;
        var nativeConstruction = f.Native.PyromancerConstructions[f.Native.Primary!];
        var loadVersion = f.Native.LoadVersion;
        f.Frame(1.1, nativeEvent: () =>
        {
            f.Native.RemoveActor(1);
            f.Native.SynchronizeActor(boss);
        }, sourceEvent: () =>
        {
            f.Session.World.Execute(new ActorState.OpDestroy(1));
            f.Session.World.Execute(new ActorState.OpCreate(1, 0x2EF7, 2, 0, "", 0, ActorType.Enemy, Class.PLD, 100,
                new(f.Center, 0), .5f, default, true, false, 0, 0, 0));
            f.Session.World.Execute(new ActorState.OpCombat(1, true));
        });
        Check(ReferenceEquals(f.Source, sourceModule) && f.Native.LoadVersion == loadVersion
            && ReferenceEquals(f.Native.PyromancerConstructions[f.Native.Primary!], nativeConstruction),
            "Pyromancer same-frame same-ID primary rebind preserves module phase and constructor identity");
        f.Actors.RemoveAll(a => a.Id == 1); f.Frame(1.2, compare: false);
        f.Actors.Insert(0, boss); f.Frame(2);
        Check(f.Native.Active, "Pyromancer new module after previous frame unload can pull");
        f.Change(1, a => a with { Targetable = false }); f.Frame(3);
        Check(!f.Native.Active && !f.Native.HasConstructionDrawing, "Pyromancer untargetable completes and disposes");
        f.Change(1, a => a with { Targetable = true }); f.Frame(4);
        Check(!f.Native.Active, "Pyromancer same original actor cannot repull after phase completion");
    }

    private static void PyromancerGrouped()
    {
        using var f = new SkirmishFixture(37);
        f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(20929, 1) }); f.Frame(1);
        var caster = f.Session.World.Actors.Find(20)!;
        var nativeCaster = f.Native.Actors[20];
        // A direct non-watched Finish is deliberately delivered to the real
        // component and native module: SimpleAOEGroups consumes it by caster ID.
        var source = f.Source!.Components.OfType<SimpleAOEGroups>().First();
        source.OnCastFinished(caster, new() { Action = new(ActionType.Spell, 999), TargetID = 100 });
        f.Native.Finish(nativeCaster, f.Spell(999, 1));
        Check(source.Casters.Count == 0 && f.Native.Casts.Count == 0, "Pyromancer groups remove on any-action Finish");
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        for (var i = 0; i < 100; ++i)
            f.Actors.Add(new() { Id = (ulong)(200 + i), Oid = 0x2EA1, Kind = 2, SubKind = 5, Hp = 1000,
                Position = f.Center, Cast = f.Spell(i % 2 == 0 ? 20929u : 20930u, 3, total: 7 - i % 3) });
        f.Frame(3);
        Check(f.Native.Casts.Count == 100, "Pyromancer expected99 sort boundary really hit");
    }

    private static void PyromancerUnload()
    {
        using var f = new SkirmishFixture(37);
        f.Change(1, a => a with { InCombat = false }); f.Frame(0);
        f.Actors.Add(f.Actors[0] with { Id = 2 }); f.Frame(1);
        f.Frame(2, new MechanicEventDirectorUpdate(0, 0x40000005, 0, 0, 0, 0), () => f.Region.Director(0x40000005));
        ComparePyromancerConstructions(f);
        Check(f.Native.PyromancerConstructions.Count == 1
            && f.Native.PyromancerConstructions.Keys.Single().Actor.Id == 2,
            "Pyromancer wipe disposes only Loaded, preserves and promotes actual Pending instance");
        f.Change(20, a => a with { Cast = f.Spell(21022, 3) }); f.Frame(3);
        f.Frame(4, new MechanicEventActionEffect(20, 1, 21022, 100, 0, f.Center, 0, 0, 0, []),
            () => f.Native.Effect(20, 21022)); ComparePyromancerConstructions(f);
        Check(f.Native.PyromancerConstructions.Values.Single().NumCasts == 1, "Pyromancer retained pending alone receives new packets");
        f.Frame(5, new MechanicEventDirectorUpdate(0, 0x40000011, 0, 0, 0, 0), () => f.Region.Director(0x40000011));
        Check(f.Native.SampleRadius == 40 && f.Native.ConstructionCasts.Count() == 1,
            "Pyromancer wipe completion resumes arena update and retains actual component history");

        using var far = new SkirmishFixture(37);
        far.Change(1, a => a with { InCombat = false }); far.Frame(0);
        far.Actors.RemoveAll(a => a.Id == 1);
        far.Change(100, a => a with { Position = far.Center + Vector3.UnitX * 501 }); far.Frame(1);
        ComparePyromancerConstructions(far);
        Check(far.Native.PyromancerConstructions.Count == 0, "Pyromancer destroyed plus distance unload disposes constructor");
        far.Change(20, a => a with { Cast = far.Spell(21022, 2) }); far.Frame(2);
        Check(far.Native.PyromancerConstructions.Count == 0, "Pyromancer unloaded constructor does not receive later Start");
    }
}
