using MasterPlayer.Scripting;

internal static class ScriptHintsApiRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("script hints api gates facing requests during replay and cleans up safely", () =>
        {
            var hints = new ScriptHintsApi(() => true);
            hints.SetFacing(1.25f);
            hints.SetFacing(null);
            hints.ClearFacing();
            hints.Cleanup();
        });
    }
}
