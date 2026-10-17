using System.Numerics;
using System.Text.Json;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    private static void RunSmartMovementSettings(Action<string, Action> run)
    {
        foreach (var dodge in new[] { false, true })
            foreach (var follow in new[] { false, true })
                run($"Smart movement migrates legacy switches: dodge={dodge}, follow={follow}", () =>
                {
                    using var scope = new FollowScope();
                    var json = JsonSerializer.Serialize(new { EnableAutoDodge = dodge, EnableFollow = follow, EnableScripts = false, EnableMechanicModules = false, FollowDistance = 7 });
                    var settings = MasterPlayerSettings.FromJson(json);
                    Check(settings.EnableSmartMovement == (dodge || follow), "Legacy movement selection was lost.");
                    Check(!settings.EnableMechanics && settings.FollowDistance == 7, "Mechanic switches or player distance were lost.");
                    var saved = JsonSerializer.Serialize(settings, new JsonSerializerOptions { IncludeFields = true });
                    Check(!saved.Contains("EnableAutoDodge") && !saved.Contains("EnableFollow") && !saved.Contains("EnableScripts") && !saved.Contains("EnableMechanicModules"), "Redundant switches were persisted again.");
                });

        run("Smart movement preserves explicit new settings and independent territory switches", () =>
        {
            using var scope = new FollowScope();
            var settings = MasterPlayerSettings.FromJson("{\"SettingsVersion\":1,\"EnableSmartMovement\":false,\"EnableAutoDodge\":true,\"EnableMechanics\":false,\"TerritoryEnabled\":{\"1036\":false,\"1165\":true}}");
            Check(!settings.EnableSmartMovement && !settings.EnableMechanics, "Legacy settings overrode new choices.");
            Check(!settings.IsTerritoryEnabled(1036) && settings.IsTerritoryEnabled(1165) && settings.IsTerritoryEnabled(999), "Territory switches are not independent or default enabled.");
        });

        run("Smart movement off blocks every script movement facade and retains follow guidance", () =>
        {
            using var scope = new FollowScope();
            scope.Settings.EnableSmartMovement = false;
            scope.Update();
            Check(scope.Manager.BuildSnapshot(DateTime.UtcNow).GoalAreas.Count == 1 && scope.MoveCount == 0, "Drawing-only following lost guidance or sent movement.");
            var api = new ScriptMoveApi();
            var point = new GreenMovePoint(Vector3.UnitX);
            api.MoveTo(point); api.MoveTo([point]); api.Append(point);
            api.CatmullRomMoveTo(point); api.CatmullRomMoveTo([point]);
            api.ClearQueue(); api.SkipCurrent(); api.Stop(); api.Cleanup();
            var hints = new ScriptHintsApi(() => false);
            hints.SetFacing(1); hints.ClearFacing(); hints.Cleanup();
            Check(scope.MoveCount == 0 && scope.StopCount == 0, "Disabled script movement touched the executor.");
        });
    }
}
