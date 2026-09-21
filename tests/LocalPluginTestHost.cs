using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer;

// Only the plugin shell is replaced; the runtime, registry, solver and dispatcher are production code.
internal sealed class MasterPlayerPlugin : IDisposable
{
    private readonly MasterPlayerPlugin? _previous;
    public static MasterPlayerPlugin? Instance { get; private set; }
    public SafeZoneRuntime SafeZone { get; }
    public ReplayState Scripts { get; } = new();

    public MasterPlayerPlugin(IGreenMoveSystem? movement = null,
        Func<bool>? externalMovementEnabled = null, Func<bool>? externalHasControl = null)
    {
        _previous = Instance;
        SafeZone = new(movement ?? ScriptServiceProxy.Create<IGreenMoveSystem>((name, _) =>
            throw new InvalidOperationException("Unexpected movement: " + name)),
            externalMovementEnabled ?? (() => false), externalHasControl ?? (() => false));
        Instance = this;
    }

    public void Dispose()
    {
        try { SafeZone.Dispose(); }
        finally { Instance = _previous; }
    }

    internal sealed class ReplayState
    {
        public bool ReplayModeActive { get; set; }
    }
}
