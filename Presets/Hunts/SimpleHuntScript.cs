using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Settings;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Hunts;

public abstract class SimpleHuntScript : MasterPlayerScript
{
    private readonly SimpleHuntEncounter encounter = new();
    private readonly Dictionary<string, (HuntPendingCast Cast, SafeZoneAreaHandle Handle)> areas = [];
    private readonly HashSet<string> areaPreviews = [];
    private readonly HashSet<string> extraPreviews = [];
    private IReadOnlyList<ReplayPartyMember> party = [];
    private DateTime nextScan;
    private VanaspatiMisdirectionControl? misdirectionInput;
    public bool EncounterActive => encounter.Active;
    public string? PredictedDamageText { get; private set; }
    public string? MechanicHintsText { get; private set; }
    public bool PyreticActive { get; private set; }
    public bool MisdirectionActive => encounter.Misdirected(CurrentPlayer, party);
    public override IEnumerable<string> GetMechanisms() => SimpleHuntCatalog.Definitions.Select(d => d.Name);

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        Scan();
        return encounter.Handles(cast.SourceId, cast.ActionId, IsMechanismEnabled);
    }

    protected override void OnStart()
    {
        OnStartCast(cast =>
        {
            Scan();
            if ((!encounter.Handles(cast.SourceId, cast.ActionId, IsMechanismEnabled)
                    && !encounter.ActiveActors.Any(a => a.Id == cast.SourceId && a.Oid == 0x35DB))
                || !float.IsFinite(cast.CastTime) || cast.CastTime is < 0 or > 3600) return;
            var started = cast.Timestamp.ToUniversalTime();
            encounter.Start(cast.SourceId, new(cast.ActionId, cast.TargetId, cast.EffectPosition,
                CastRotation.TryGet(cast, out var rotation) ? rotation : null, started,
                started.AddSeconds(cast.CastTime + .3), cast.CanInterrupt != 0, false) { TotalTime = cast.CastTime }, true, cast.SourcePosition, CurrentTime);
            Render();
        });
        OnCancelCast(cast => { encounter.Finish(cast.SourceId, cast.ActionId, cast.Timestamp.ToUniversalTime()); Render(); });
        OnInterruptCast(cast => { encounter.Finish(cast.SourceId, cast.ActionId, cast.Timestamp.ToUniversalTime()); Render(); });
        OnActionEffect(effect =>
        {
            if (effect.ActionId == 16964) return;
            encounter.Effect(effect.SourceId, effect.ActionId, effect.Timestamp.ToUniversalTime(), effect.GlobalSequence);
            Render();
        });
        OnLogEvent<NativeActionEffectEvent>(effect =>
        {
            if (effect.ActionId != 16964) return;
            encounter.Effect(effect.SourceId, effect.ActionId, effect.Timestamp.ToUniversalTime(), effect.GlobalSequence, effect.MainTargetId);
            Render();
        });
        OnStatusAdd(status =>
        {
            var at = status.Timestamp.ToUniversalTime();
            encounter.BossStatus(status.TargetId, status.StatusId, at,
                new(status.StatusId, status.Param, status.SourceId, ReplayStatus.Expiration(at, status.Duration)));
            if (HuntMarchState.Matches(status.StatusId))
            {
                encounter.MarchStatus(status.TargetId, status.StatusId, at,
                    new(status.StatusId, status.Param, status.SourceId, ReplayStatus.Expiration(at, status.Duration)));
                Render();
            }
            if (status.StatusId == 1435) { encounter.StackStatus(status.TargetId, at, true); Render(); }
            if (HuntMisdirectionState.Matches(status.StatusId))
            {
                encounter.Misdirection(status.TargetId, status.StatusId, true, party);
                Render();
            }
            if (status.StatusId != 960) return;
            encounter.Pyretic(status.TargetId, at, new(960, 0, status.SourceId, ReplayStatus.Expiration(at, status.Duration)));
            Render();
        });
        OnStatusRemove(status =>
        {
            encounter.BossStatus(status.TargetId, status.StatusId, status.Timestamp.ToUniversalTime(), null);
            if (HuntMarchState.Matches(status.StatusId))
            {
                encounter.MarchStatus(status.TargetId, status.StatusId, status.Timestamp.ToUniversalTime(), null);
                Render();
            }
            if (status.StatusId == 1435) { encounter.StackStatus(status.TargetId, status.Timestamp.ToUniversalTime(), false); Render(); }
            if (HuntMisdirectionState.Matches(status.StatusId))
            {
                encounter.Misdirection(status.TargetId, status.StatusId, false, party);
                Render();
            }
            if (status.StatusId != 960) return;
            encounter.Pyretic(status.TargetId, status.Timestamp.ToUniversalTime(), null, status.SourceId);
            Render();
        });
        OnLogEvent<LogSystemObjectRemoveEvent>(ev => { encounter.Remove(ev.SourceId); Render(); });
        OnTerritoryChanged(_ => Clear());
        Scan();
        Render();
    }

    protected override void OnUpdate(float deltaTime)
    {
        UpdateMisdirection();
        if (ReplayWorld == null && CurrentTime < nextScan) return;
        Scan();
        Render();
    }

    private void Scan()
    {
        var now = CurrentTime;
        nextScan = now.AddSeconds(.1);
        var observations = ReplayWorld is { } world ? world.Actors.Values
            : Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now));
        party = ReplayWorld?.Party ?? ReplayWorldCapture.CaptureParty();
        encounter.Synchronize(observations, IsMechanismEnabled, now, party);
        encounter.ObserveMisdirection(party);
    }

    private void Render()
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        var visible = encounter.Areas.Concat(encounter.TargetPreviews(CurrentPlayer)).Where(c => c.Shape != null).ToDictionary(c => c.AreaId);
        var wanted = encounter.Areas.Concat(encounter.TargetDangers(CurrentPlayer, party))
            .Where(c => c.Shape != null && c.Risky).ToDictionary(c => c.AreaId);
        foreach (var source in areas.Keys.Where(id => !wanted.ContainsKey(id)).ToArray()) Remove(source);
        foreach (var (source, cast) in wanted)
        {
            using var group = SafeZone.BeginDrawingGroup(cast.Definition.Name + " / " + cast.Rule.Name);
            if (!areas.TryGetValue(source, out var previous) || previous.Cast != cast
                || !runtime.Manager.ContainsArea(previous.Handle))
            {
                Remove(source);
                // Source default means immediately active; MP timestamps must remain bufferable.
                var startedAt = cast.StartedAt == default ? DateTime.UnixEpoch : cast.StartedAt;
                var binding = new StaticTransform(cast.Location, cast.Rotation);
                var handle = SafeZone.AddDangerSnapshot(cast.Shape!, binding,
                    [new(cast.Activation == default ? startedAt : cast.Activation, null, startedAt)]);
                SafeZone.SetNavigationOnly(handle, true);
                areas[source] = (cast, handle);
            }
        }
        foreach (var id in areaPreviews.Where(id => !visible.ContainsKey(id))) Draw.Remove($"hunt/{id}");
        foreach (var (id, cast) in visible)
        {
            using var group = SafeZone.BeginDrawingGroup(cast.Definition.Name + " / " + cast.Rule.Name);
            Draw.AddAoePreview($"hunt/{id}", cast.Shape!.Resolve(cast.Location, cast.Rotation, null), cast.Safe, cast.OutlineOnly, 1, color: cast.Color);
        }
        areaPreviews.Clear();
        areaPreviews.UnionWith(visible.Keys);
        var extras = HuntDrawing.Collect(encounter, CurrentPlayer).ToArray();
        var extraIds = extras.Select(p => p.Id).ToHashSet();
        foreach (var id in extraPreviews.Where(id => !extraIds.Contains(id))) Draw.Remove(id);
        foreach (var primitive in extras)
        {
            using var group = SafeZone.BeginDrawingGroup("狩猎实体与机制投影");
            Draw.AddAoePreview(primitive.Id, primitive.Geometry, primitive.Safe, false, 1, true, primitive.Color);
        }
        extraPreviews.Clear();
        extraPreviews.UnionWith(extraIds);
        var damage = encounter.PredictedDamage(party).ToArray();
        var hints = encounter.Hints(CurrentPlayer).Concat(encounter.TargetHints(CurrentPlayer, party)).ToArray();
        PyreticActive = encounter.StopEverything(CurrentPlayer, party, CurrentTime);
        if (PyreticActive) hints = [.. hints, "Pyretic on you! STOP everything!"];
        MechanicHintsText = hints.Length == 0 ? null : string.Join("；", hints);
        Hints.SetFacing(CurrentPlayer is { Dead: false } player ? encounter.Facing(player, CurrentTime) : null);
        Hints.SetActionRestrictions(holdActions: PyreticActive, blockCasts: PyreticActive, cancelCast: PyreticActive);
        Hints.SetMovementHold(PyreticActive);
        PredictedDamageText = damage.Length == 0 ? null : string.Join("；", damage.GroupBy(d => d.DamageType)
            .Select(g => $"{g.Key}：{g.Select(d => d.TargetId).Distinct().Count()}人/{g.Select(d => (d.Source, d.Activation)).Distinct().Count()}次"));
        runtime.RequestAutomaticMovement(this, encounter.Active && CurrentPlayer is { Dead: false } && !IsReplay,
            goalDirectedTemporalPlanning: true);
        UpdateMisdirection();
    }

    private void UpdateMisdirection()
    {
        var authorized = MisdirectionActive && !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true
            && CurrentPlayer is { Dead: false } player && PlayerPerspective.LivePlayer?.EntityId == player.Id;
        if (authorized) (misdirectionInput ??= new()).Renew(true, Svc.ClientState.TerritoryType);
        else misdirectionInput?.Clear();
    }

    private void Remove(string source)
    {
        if (areas.Remove(source, out var area)) SafeZone.Remove(area.Handle);
        Draw.Remove($"hunt/{source}");
    }

    private void Clear()
    {
        foreach (var source in areas.Keys.ToArray()) Remove(source);
        foreach (var id in areaPreviews) Draw.Remove($"hunt/{id}");
        areaPreviews.Clear();
        encounter.Reset();
        foreach (var id in extraPreviews) Draw.Remove(id);
        extraPreviews.Clear();
        Hints.ClearFacing();
        Hints.ClearActionRestrictions();
        Hints.SetMovementHold(false);
        PyreticActive = false;
        PredictedDamageText = null;
        MechanicHintsText = null;
        nextScan = default;
        misdirectionInput?.Dispose();
        misdirectionInput = null;
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }

    protected override void OnStop() => Clear();
    protected override void OnMovementDisabled()
    {
        misdirectionInput?.Clear();
        Hints.ClearFacing();
        Hints.ClearActionRestrictions();
        Hints.SetMovementHold(false);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current)
    {
        misdirectionInput?.Clear();
        Render();
    }
}
