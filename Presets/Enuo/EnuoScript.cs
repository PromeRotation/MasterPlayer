using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Enuo;

public abstract class EnuoScript : MasterPlayerScript
{
    private readonly EnuoEncounter _encounter = new();
    private readonly ReplayWorldCapture _capture = new();
    private readonly EnuoActions _actions = new();
    private EnuoEventAdapter? _events;
    private EnuoOutput? _output;
    private EnuoHostControl? _control;
    private EnuoPartyDebug? _partyDebug;
    private EnuoDebugDisplay? _debugDisplay;
    private bool _debugShown;
    private DateTime _nextScan, _nextHint;
    private bool _suspended;
    private long _replayVersion;
    [UserSetting("自动插言、驱散、减伤、防击退与冲刺")]
    public bool AutomaticActions { get; set; } = true;
    [UserSetting("调试模式：显示所有职能的目标位置")]
    public bool DebugAllRoles { get; set; }
    public override IEnumerable<string> GetMechanisms() => ["视频分工与自动处理", "文字提示"];

    protected override void OnStart()
    {
        _events = new(_encounter, e => CastRotation.TryGet(e, out var r) ? r : null);
        _partyDebug = new(e => CastRotation.TryGet(e, out var r) ? r : null);
        _debugDisplay = new(SafeZone, Draw, () => Settings.Set(nameof(DebugAllRoles), false));
        _output = new(SafeZone, Draw, MasterPlayerPlugin.Instance!.SafeZone.Manager);
        _control = new(Log);
        SuppressGenericDodge(EnuoEncounter.ClaimedActions);
        OnLogEvent(Handle);
        OnBattleEnded(Suspend); OnPlayerDied(Suspend);
        OnBattleStarted(Resume); OnPlayerRevived(Resume);
        Update();
    }
    protected override void OnUpdate(float deltaTime)
    {
        if (CurrentTime >= _nextScan || ReplayWorld != null && ReplayWorld.Version != _replayVersion
            || Settings.GetBool(nameof(DebugAllRoles)) != _debugShown) Update();
    }
    private void Scan()
    {
        if (ReplayWorld is { } world)
        {
            _replayVersion = world.Version;
            _encounter.Synchronize(world.Actors.Values, world.Party, CurrentPlayerId, CurrentTime);
            _partyDebug?.Synchronize(world.Actors.Values, world.Party, CurrentTime);
        }
        else
        {
            var frame = _capture.Capture(CurrentTime, CurrentPlayerId);
            _encounter.Synchronize(frame.Actors, frame.Party, CurrentPlayerId, CurrentTime);
            _partyDebug?.Synchronize(frame.Actors, frame.Party, CurrentTime);
        }
        _nextScan = CurrentTime.AddSeconds(0.1);
    }
    private void Handle(LogSystemEvent ev)
    {
        _capture.Observe(ev);
        if (ev is LogSystemDirectorUpdateEvent { Command: 0x40000005 or 0x40000006 }) { Suspend(); return; }
        if (_suspended) return;
        if (ev is LogSystemStatusAddEvent { StatusId: 4562 or 3523 or 2941 or 3943 } or LogSystemStatusRemoveEvent { StatusId: 4562 or 3523 or 2941 or 3943 }
            or LogSystemTargetIconEvent { MarkerId: 721 } or LogSystemActionEffectEvent { ActionId: 50013 or 50038 })
        {
            Scan(); Feed(ev); Apply(); return;
        }
        if (ev is LogSystemCastStartEvent c && !_encounter.Actors.ContainsKey(c.SourceId)) Scan();
        Feed(ev);
    }
    private void Feed(LogSystemEvent ev) { _events?.Handle(ev, CurrentTime); _partyDebug?.Handle(ev, CurrentTime); }
    private void Update()
    {
        if (_suspended) return;
        Scan();
        Apply();
    }
    private void Apply()
    {
        var local = CurrentPlayerId;
        var debugFrame = _partyDebug?.Snapshot(CurrentTime);
        var encounter = _partyDebug?.ForPlayer(local) ?? _encounter;
        var plan = encounter.Plan(CurrentTime);
        var enabled = IsMechanismEnabled("视频分工与自动处理");
        if (local == 0) { _output?.Clear(CurrentTime); Release(); _debugDisplay?.Update(null, ReplayWorld != null); return; }
        if (!enabled) plan.Goal = null;
        _output?.Apply(plan, CurrentTime);
        _debugShown = Settings.GetBool(nameof(DebugAllRoles));
        // Planning also advances role-local freeze/charge memory; keep that history
        // current while the overlay is off so mid-mechanic toggles show the same plan.
        _debugDisplay?.Update(_debugShown ? debugFrame : null, ReplayWorld != null);
        var alive = encounter.Actors.TryGetValue(local, out var player) && !player.Dead && player.InCombat;
        var allowed = EnuoExecutionGate.Allows(MasterPlayerSettings.Instance.EnableSmartMovement, enabled,
            IsReplay, alive, encounter.Role >= 0);
        var runtime = MasterPlayerPlugin.Instance!.SafeZone;
        runtime.RequestAutomaticMovement(this, allowed, plan.HoldMovement);
        if (allowed)
        {
            _control?.Apply(plan);
            runtime.RequestDirectTraversal(this, plan.KeepMoving && !plan.HoldMovement ? plan.Goal?.Position : null, CurrentTime);
            if (Settings.GetBool(nameof(AutomaticActions), true)) _actions.Apply(plan, CurrentTime, CurrentPlayer, IsReplay);
        }
        else Release();
        if (IsMechanismEnabled("文字提示") && CurrentTime >= _nextHint && plan.Hints.Count > 0)
        {
            Log(string.Join(" ", plan.Hints.Distinct())); _nextHint = CurrentTime.AddSeconds(3);
        }
    }
    protected override void OnMovementDisabled() => Release();
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current)
    {
        Release(); _output?.Clear(CurrentTime); _nextScan = _nextHint = default;
    }
    protected override void OnStop() { Release(); _output?.Clear(CurrentTime); _encounter.Reset(); ClearDebug(); }
    private void Suspend() { _suspended = true; Release(); _output?.Clear(CurrentTime); _encounter.Reset(); ClearDebug(); }
    private void ClearDebug() { _debugDisplay?.Dispose(); _partyDebug?.Reset(); _debugShown = false; }
    private void Resume() { _suspended = false; _nextScan = _nextHint = default; }
    private void Release()
    {
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        _control?.Clear(); _actions.Clear();
    }
}
