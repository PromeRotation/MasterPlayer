namespace MasterPlayer.BmrRuntime;

public static class BmrRuntimeEnvironment
{
    public static bool IsInitialized { get; private set; }

    public static void Initialize(Lumina.GameData gameData, Dalamud.Plugin.Services.IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(log);
        if (IsInitialized)
        {
            if (!ReferenceEquals(BossMod.Service.LuminaGameData, gameData))
                throw new InvalidOperationException("BMR data cannot change after its static action definitions have been initialized.");
            return;
        }

        BossMod.Service.BindRuntimeData(gameData, log);
        BossMod.Service.LogHandlerDebug = message => log.Debug(message);
        BossMod.Service.LogHandlerVerbose = message => log.Verbose(message);
        BossMod.Service.Config.Initialize();
        _ = BossMod.ActionDefinitions.Instance;
        IsInitialized = true;
    }
}
