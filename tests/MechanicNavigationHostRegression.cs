using System.Numerics;
using BossMod;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class MechanicNavigationHostRegression
{
    private static readonly DateTime Start = new(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        var config = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "bmr-navigation-config"));
        Set(nameof(Svc.PluginInterface), ScriptServiceProxy.Create<IDalamudPluginInterface>((name, _) => name switch
        {
            "get_ConfigDirectory" => config,
            _ => throw new NotSupportedException(name),
        }));
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        if (string.IsNullOrWhiteSpace(sqpack) || !Directory.Exists(sqpack))
            throw new ArgumentException("Set FFXIV_SQPACK to the real game sqpack directory.");
        using var game = new Lumina.GameData(sqpack, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
            PanicOnSheetChecksumMismatch = true,
        });
        MechanicRuntimeEnvironment.Initialize(game, ScriptServiceProxy.Create<IPluginLog>((_, _) => null));
        run("BMR navigation real boss cast reaches MP solver", () => BossCast("cast"));
        run("BMR navigation recovers boss cast and boundary after external clear", () => BossCast("clear"));
        run("BMR navigation retains a still published hazard after activation", () => BossCast("persistent"));
        run("BMR navigation consumes zone hints without a boss module", () => BossCast("zone"));
        run("BMR navigation releases suppression and recovers after aggregation failure", () => BossCast("error"));
        run("BMR navigation releases suppression when perspective is missing", () => BossCast("perspective"));
        MechanicHintsConvertRegression.Run(run);
    }

    private static void BossCast(string mode)
    {
        var originalObjects = Svc.Objects;
        var originalCondition = Svc.Condition;
        var originalLog = Svc.Log;
        var settings = MasterPlayerSettings.Instance;
        var originalMovement = settings.EnableSmartMovement;
        var perspectiveId = 100u;
        var pc = ScriptServiceProxy.Create<IPlayerCharacter>((name, _) => name switch
        {
            "get_EntityId" => perspectiveId,
            _ => throw new NotSupportedException(name),
        });
        Set(nameof(Svc.Objects), ScriptServiceProxy.Create<IObjectTable>((name, _) => name switch
        {
            "get_LocalPlayer" => pc,
            _ => throw new NotSupportedException(name),
        }));
        Set(nameof(Svc.Condition), ScriptServiceProxy.Create<ICondition>((_, _) => false));
        Set(nameof(Svc.Log), ScriptServiceProxy.Create<IPluginLog>((_, _) => null));
        try
        {
            using var plugin = new MasterPlayerPlugin();
            settings.EnableSmartMovement = true;
            var runtime = plugin.SafeZone;
            runtime.Setting.EnableSafeZone = true;
            var now = Start;
            runtime.ReplayClock = () => now;
            using var session = new MechanicSession("navigation regression");
            var feed = new MechanicWorldFeed(session);
            using var nav = new MechanicNavigationHost(session);
            var zoneOnly = mode is "zone" or "error";
            var center = new Vector3(-212.99f, 0, 186.99f);
            MechanicActorSnapshot Actor(ulong id, uint oid, bool ally, MechanicCastSnapshot? cast = null)
                => new(id, oid, id == 100 ? 0 : 2, 0, "", 0, ally ? (byte)1 : (byte)2,
                    ally ? (byte)4 : (byte)5, ally ? 1u : 0u, 100, center, 0, ally ? .5f : 3.2f,
                    100000, 100000, 0, 10000, 10000, true, ally, false, !ally, 0, 100,
                    0, 0, 0, 0, 0, 0, 0, 0, cast, []);
            void Frame(MechanicCastSnapshot? cast)
            {
                session.BeginFrame(now);
                feed.ApplyFrame(new(zoneOnly ? (ushort)1036 : (ushort)1145,
                    zoneOnly ? (ushort)4 : (ushort)242,
                    100, 1, [new(0, 100, 100, "")], [],
                    [Actor(100, 0, true), Actor(1, zoneOnly ? 0xDEADu : 0x1AA9u, false, cast)]));
                session.Update();
                nav.Update();
            }
            Frame(null);
            if (zoneOnly)
                Check(session.BossModules.ActiveModule == null && session.ZoneModules.ActiveModule != null,
                    "The zone-only test unexpectedly activated a boss.");
            else
                Check(session.BossModules.ActiveModule?.StateMachine.ActivePhase != null, "Boss did not activate.");
            now = Start.AddSeconds(1);
            var cast = new MechanicCastSnapshot(7957, true, 100, 0, center, 3, false);
            Frame(cast);
            if (zoneOnly)
            {
                using var draw = new MechanicDrawHost(session);
                draw.Update();
                Check(draw.CurrentHints.Any(h => h.Text.Contains("Correct switch")),
                    "The real Sastasha zone's global hint did not reach the draw bridge.");
            }
            if (mode == "error")
            {
                var originalZone = session.ZoneModules.ActiveModule;
                using var failing = new BmrNavigationFailureZone(session.World);
                try
                {
                    session.ZoneModules.ActiveModule = failing;
                    nav.Update();
                    Check(!nav.ModuleActive && !nav.NavigationActive && nav.DangerZoneCount == 0
                        && nav.LastError == "injected navigation failure",
                        "Failed aggregation kept stale output or kept generic dodge suppressed.");
                }
                finally { session.ZoneModules.ActiveModule = originalZone; }
                nav.Update();
                Check(nav.ModuleActive && nav.LastError == null, "Navigation did not recover after the module recovered.");
            }
            if (mode == "perspective")
            {
                perspectiveId = 999;
                nav.Update();
                Check(!nav.ModuleActive && nav.DangerZoneCount == 0 && !nav.BoundaryActive,
                    "A missing perspective left stale output or kept generic dodge suppressed.");
                perspectiveId = 100;
                nav.Update();
            }
            if (mode == "clear")
            {
                runtime.Manager.ClearAllAreas();
                now = now.AddSeconds(.1);
                Frame(cast);
            }
            if (mode == "persistent")
            {
                now = Start.AddSeconds(6);
                runtime.Manager.PruneExpiredAreas(now);
                Frame(cast);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            Console.WriteLine($"BMR trace: ready={nav.ModuleActive}, danger={nav.DangerZoneCount}, boundary={nav.BoundaryActive}, error={nav.LastError}");
            Check(snapshot.DangerAreas.Count > 0, "Mechanic danger did not reach the MP danger registry.");
            if (!zoneOnly)
                Check(snapshot.ArenaBoundary?.Contains(center) == true, "Navigation arena missed its world center.");
            else
                Check(snapshot.ArenaBoundary == null, "A zone's automatic search box replaced live collision terrain.");
            if (mode == "persistent")
                Check(snapshot.DangerAreas.SelectMany(d => d.DangerWindows).Any(w => w.IsActiveAt(now)),
                    "A still published BMR danger stopped being dangerous after its first hit window.");
            var solution = new SafeZoneSolver().Solve(snapshot, new(center, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), SolveOptionsDefaults.Default, now);
            Check(solution.IsSuccess && solution.TargetPosition is { } target
                && Vector3.Distance(target, center) > 5, $"Solver did not escape the BMR cast: {solution.Status}");
        }
        finally
        {
            settings.EnableSmartMovement = originalMovement;
            Set(nameof(Svc.Objects), originalObjects);
            Set(nameof(Svc.Condition), originalCondition);
            Set(nameof(Svc.Log), originalLog);
        }
    }

    private static void Set(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
