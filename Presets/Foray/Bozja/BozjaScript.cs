using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Bozja;

public abstract class BozjaScript : MasterPlayerScript
{
    public override ScriptTimeline? GetTimeline()
        => encounter?.ArtTimeline?.Capture() ?? encounter?.OwainTimeline?.Capture()
            ?? encounter?.RaidenTimeline?.Capture()
            ?? encounter?.VirtueTimeline?.Capture()
            ?? encounter?.LionsTimeline?.Capture()
            ?? encounter?.RhalgrTimeline?.Capture()
            ?? encounter?.PhantomTimeline?.Capture() ?? encounter?.DahuTimeline?.Capture();
    private readonly BozjaRegion region;
    private BozjaEncounter? encounter => region.Selected;
    private BozjaZone[] preparedBurnNavigation = [];
    private readonly Dictionary<string, (BozjaZone Zone, SafeZoneAreaHandle[] Handles)> areas = [];
    private readonly HashSet<string> drawing = [];
    private readonly List<LogSystemEvent> pendingEvents = [];
    private readonly Dictionary<ulong, uint> lastAnimation = [];
    private readonly List<(ulong Source, uint State)> frameAnimations = [];
    private string? reportedTimeError;
    private static readonly LocalPolygonTemplate eyeSymbol = DungeonEncounter.CreateQueenEyeSymbol();
    private uint? allowedFate;
    private IGeometryShape? boundary;
    private IGeometryShape? boundarySource;
    private DateTime nextScan;
    private bool needsFrameworkScan;
    private bool frameworkDrawing;
    private BozjaZone[] preparedArenaNavigation = [];
    private BozjaDamage[] preparedArenaDamage = [];
    private float? preparedArenaFacing;
    private IGeometryShape? preparedArenaBoundary;
    private Vector3 preparedArenaCenter;
    private float preparedArenaRadius;
    private (BozjaEncounter Encounter, long Version, ulong Viewer)? preparedArenaOwner;
    private string? preparedStayModeText;
    private (BozjaEncounter Encounter, long Version, ulong Viewer)? preparedStayModeOwner;
    private ReplayActorState[] sampledActors = [];
    private long observedVersion = -1;
    public bool EncounterActive => encounter?.Active == true;
    public string? MechanicHintsText { get; private set; }
    public string? PredictedDamageText { get; private set; }
    internal BozjaDamage[] PredictedDamage { get; private set; } = [];
    internal ulong[] InterruptTargetIds { get; private set; } = [];
    internal float? PlannedFacing { get; private set; }
    internal ulong PlannedTargetId { get; private set; }

    protected BozjaScript(uint territory = BozjaCatalog.TERRITORY) : this(territory, null)
    {
    }

    protected BozjaScript(uint territory, string? encounterKey)
    {
        region = new(territory, encounterKey);
    }

    public override IEnumerable<string> GetMechanisms() => region.Encounters.Select(e => e.Definition.Name).Append("面向控制");

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        Scan();
        return encounter is { Active: true } model && IsMechanismEnabled(model.Definition.Name)
            && sampledActors.Any(a => a.Id == cast.SourceId && a.Kind != 1
                && !(a.Kind == 2 && a.SubKind is 2 or 3 or 9))
            && model.Definition.Rules.Any(r => r.Actions.Contains(cast.ActionId));
    }

    protected override void OnStart()
    {
        OnLogEvent(e =>
        {
            Scan();
            if (e is LogSystemDirectorUpdateEvent or LogSystemTetherEvent or LogSystemTetherCancelEvent or LogSystemEObjAnimationEvent or LogSystemTargetIconEvent or LogSystemMapEffectEvent or NativeActionEffectEvent)
                pendingEvents.Add(e);
            Render();
        });
        OnTerritoryChanged(_ => Clear());
        Scan();
        Render();
    }

    protected override void OnUpdate(float deltaTime)
    {
        try
        {
            if (ReplayWorld == null || needsFrameworkScan || pendingEvents.Count > 0 || CurrentTime >= nextScan
                || ReplayWorld != null && ReplayWorld.Version != observedVersion) Scan(true);
            else region.Update(CurrentTime);
        }
        catch (ArgumentOutOfRangeException ex) when (region.Encounters.Any(e => e.SequenceError != null))
        {
            // Match the old bridge's withdrawal after an invalid FIFO Effect.
            // The script host stops a script on an escaping exception, while
            // the old bridge retains this event and retries next frame.
            var error = region.Encounters.First(e => e.SequenceError != null).SequenceError!;
            if (reportedTimeError != error) LogError($"{error}：{ex.Message}");
            reportedTimeError = error;
            needsFrameworkScan = true;
            Render();
            return;
        }
        reportedTimeError = null;
        PrepareNavigation();
        frameworkDrawing = true;
        try { Render(); }
        finally { frameworkDrawing = false; }
    }

    private void PrepareNavigation()
    {
        preparedStayModeText = null;
        preparedStayModeOwner = null;
        if (encounter is { Active: true } cll3 && (cll3.IsCll3 || cll3.IsArt || cll3.IsOzma) && CurrentPlayer is { } perspective
            && IsMechanismEnabled(cll3.Definition.Name)
            && cll3.Party.Any(p => p.EntityId == perspective.Id)
            && cll3.Actors.TryGetValue(perspective.Id, out var cll3Viewer))
        {
            preparedStayModeOwner = (cll3, cll3.LoadVersion, perspective.Id);
            if ((cll3.Cll3SpecialMode(cll3Viewer) ?? cll3.ArtSpecialMode(cll3Viewer) ?? cll3.OzmaSpecialMode(cll3Viewer)) is { } mode)
                preparedStayModeText = mode.Activation > CurrentTime
                    ? $"{mode.Mode}：{(mode.Activation - CurrentTime).TotalSeconds:F1}s 后生效"
                    : $"{mode.Mode}：生效中";
        }
        if (encounter is { Active: true } model && CurrentPlayer is { Dead: false } player
            && IsMechanismEnabled(model.Definition.Name) && model.Party.Any(p => p.EntityId == player.Id)
            && model.Actors.ContainsKey(model.SourcePlayerId) && model.Actors.TryGetValue(player.Id, out var viewer))
        {
            model.PrepareFireNavigation(viewer);
            if (model.IsBurn) preparedBurnNavigation = model.NavigationForHost(viewer);
            // A single weight-five source goal stays below the existing host's
            // strong-goal threshold. Preserve its query counter, not new motion.
            _ = model.DahuChargeGoal();
        }
    }

    private void FlushEvents()
    {
        // ReplayFeedAdapter appends snapshot animations after queued packets.
        // Keep them in the same retry queue if a later/earlier Effect fails.
        foreach (var (source, state) in frameAnimations)
            pendingEvents.Add(new LogSystemEObjAnimationEvent
            {
                SourceId = source, Param1 = (ushort)(state >> 16), Param2 = (ushort)state,
            });
        frameAnimations.Clear();
        // Match Feed.ApplyFrame -> Flush -> Update. Re-reading the same live
        // tether between queued packets would invent additional Tethered calls.
        foreach (var e in pendingEvents)
        {
            if (e is LogSystemDirectorUpdateEvent director) region.Director(director.Command);
            if (e is LogSystemTetherEvent tether) region.Tether(tether.SourceId, tether.TetherId, tether.TargetId);
            if (e is LogSystemTetherCancelEvent cancel) region.Tether(cancel.SourceId, 0, 0);
            if (e is LogSystemTargetIconEvent icon) region.Icon(icon.SourceId, icon.MarkerId, icon.TargetId);
            if (e is LogSystemMapEffectEvent map) region.MapEffect((byte)map.Position, map.Param1 | ((uint)map.Param2 << 16));
            if (e is LogSystemEObjAnimationEvent animation)
                region.EAnim(animation.SourceId, ((uint)animation.Param1 << 16) | animation.Param2);
            if (e is NativeActionEffectEvent effect && effect.ActionType is null or 1)
                region.Effect(effect.SourceId, effect.ActionId, effect.MainTargetId, effect.TargetPosition);
        }
        pendingEvents.Clear();
        foreach (var model in region.Encounters) model.CompleteEventBatch();
    }

    private void Scan(bool update = false)
    {
        var now = CurrentTime;
        ReplayActorState[] actors;
        ReplayPartyMember[] party;
        ReplayActiveFate? fate;
        ulong source;
        if (ReplayWorld is { } world)
        {
            if (!update && world.Version == observedVersion)
            {
                foreach (var model in region.Encounters) model.Advance(now);
                return;
            }
            actors = world.Actors.Values.ToArray();
            foreach (var actor in actors)
            {
                if (actor.EventAnimation is { } animation)
                {
                    if (!lastAnimation.TryGetValue(actor.Id, out var previous) || previous != animation)
                    {
                        lastAnimation[actor.Id] = animation;
                        frameAnimations.Add((actor.Id, animation));
                    }
                }
                else lastAnimation.Remove(actor.Id);
            }
            party = world.Party.ToArray();
            source = world.SourcePlayerId ?? world.LocalPlayerId;
            allowedFate = world.AllowedFateId;
            fate = world.ActiveFate;
            observedVersion = world.Version;
        }
        else
        {
            var enmity = ReplayWorldCapture.CapturePlayerEnmity();
            actors = Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now, playerEnmity: enmity)).ToArray();
            party = ReplayWorldCapture.CaptureParty();
            source = ReplayWorldCapture.CaptureSourcePlayerId() ?? ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
            var partyPlayer = actors.FirstOrDefault(a => a.Id == party.FirstOrDefault(p => p.Slot == 0)?.EntityId);
            fate = ReplayWorldCapture.CaptureActiveFate();
            allowedFate = ReplayWorldCapture.CaptureAllowedFate(partyPlayer?.Level, fate);
        }
        if (!update) needsFrameworkScan = true;
        sampledActors = actors;
        var cfc = region.Encounters.Any(e => e.IsOzma)
            ? LiveWorldSampler.ResolveCfc((ushort)(ReplayWorld?.Territory ?? Svc.ClientState.TerritoryType)) : (ushort)0;
        region.Synchronize(actors, party, source, now, update, update ? FlushEvents : null, fate, cfc);
        // Event sampling must not move the Framework deadline forward. A busy
        // zone can deliver events more frequently than the sampling interval.
        if (update)
        {
            nextScan = now.AddSeconds(.1);
            needsFrameworkScan = false;
        }
    }

    private void Render()
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        var encounter = this.encounter;
        var timeError = region.Encounters.FirstOrDefault(e => e.SequenceError != null)?.SequenceError ?? encounter?.FateDiagnostic;
        if (encounter != null && IsMechanismEnabled(encounter.Definition.Name)
            && (MasterPlayerSettings.Instance.EnableRadar || MasterPlayerSettings.Instance.EnableRadarOverlay))
            encounter.ObserveFireDrawing();
        if (encounter != null && IsMechanismEnabled(encounter.Definition.Name)
            && (MasterPlayerSettings.Instance.EnableRadar || MasterPlayerSettings.Instance.EnableRadarOverlay))
            encounter.ObserveCrystalDrawing();
        var player = CurrentPlayer;
        var viewer = player == null ? null : encounter?.Actors.GetValueOrDefault(player.Id);
        if (viewer != null) encounter!.PrepareRobotOutputs(viewer);
        var constructionDrawing = (encounter?.HasConstructionDrawing == true || encounter?.Cll1 != null) && viewer != null
            && encounter.Party.Any(p => p.EntityId == viewer.Actor.Id)
            && MasterPlayerPlugin.Instance?.MechanicHost?.ActiveZoneModuleName == null;
        var enabled = encounter != null && (encounter.Active || constructionDrawing) && IsMechanismEnabled(encounter.Definition.Name)
            && timeError == null && (!encounter.IsRobots
                || viewer != null && encounter.Party.Any(p => p.EntityId == viewer.Actor.Id))
            && (!encounter.IsSaunion || viewer != null
                && encounter.Party.Any(p => p.Slot is >= 0 and < 64 && p.EntityId == viewer.Actor.Id))
            && (!(encounter.IsCll3 || encounter.IsCll4 || encounter.IsBaldesion || encounter.IsOzma) || viewer != null && encounter.Party.Any(p => p.EntityId == viewer.Actor.Id));
        var navigating = enabled && encounter!.Active && player is { Dead: false } && viewer != null
            && encounter!.Party.Any(p => p.EntityId == player.Id)
            && encounter.Actors.ContainsKey(encounter.SourcePlayerId);
        if (navigating && encounter!.FireNavigationError != null) navigating = false;
        if (navigating && encounter!.RobotNavigationError != null) navigating = false;
        if (encounter?.UsesDrawnArena != true || !navigating) ClearPreparedArena();
        if (encounter?.UsesDrawnArena == true && navigating && frameworkDrawing)
        {
            preparedArenaNavigation = encounter.NavigationForHost(viewer!);
            preparedArenaDamage = encounter.DamageForHost(viewer!);
            preparedArenaFacing = encounter.Facing(player!);
            preparedArenaBoundary = encounter.HostArena;
            preparedArenaCenter = encounter.Center;
            preparedArenaRadius = encounter.SampleRadius;
            preparedArenaOwner = (encounter, encounter.LoadVersion, viewer!.Actor.Id);
        }
        if (encounter?.UsesDrawnArena == true && (preparedArenaBoundary == null
            || preparedArenaOwner is not { } preparedOwner || preparedOwner.Encounter != encounter
            || preparedOwner.Version != encounter.LoadVersion || preparedOwner.Viewer != viewer?.Actor.Id)) navigating = false;
        var navigationArena = encounter?.UsesDrawnArena == true ? preparedArenaBoundary : encounter?.HostArena;
        var navigationCenter = encounter?.UsesDrawnArena == true ? preparedArenaCenter : encounter?.Center ?? default;
        var navigationRadius = encounter?.UsesDrawnArena == true ? preparedArenaRadius : encounter?.SampleRadius ?? 0;
        var wanted = new HashSet<string>();
        var order = new List<string>();
        var publish = new List<Action>();
        void Preview(string key, IGeometryShape shape, bool safe = false, bool outline = false, Vector4? color = null)
        {
            wanted.Add(key);
            order.Add(key);
            publish.Add(() => Draw.AddAoePreview(key, shape, safe, outline, .5f, color: color));
        }
        if (navigating)
        {
            var center = navigationCenter with { Y = player!.Position.Y };
            if (boundary == null || boundary.Position != center || !ReferenceEquals(boundarySource, navigationArena))
            {
                boundarySource = navigationArena!;
                boundary = boundarySource switch
                {
                    CircleGeometry circle => new CircleGeometry(center, circle.Radius),
                    AllianceArenaGeometry alliance => alliance.AtHeight(player.Position.Y),
                    PolygonRegionGeometry region => region.AtHeight(player.Position.Y),
                    _ => new WorldPolygonGeometry(boundarySource.GetContours().Single()
                        .Select(p => p with { Y = player.Position.Y }), center),
                };
            }
            runtime.Manager.SetArenaBoundary(boundary);
            if (!encounter!.UsesDrawnArena) Preview("bozja/arena", boundary, outline: true);
        }
        else
        {
            ReleaseBoundary();
            if (enabled && encounter != null
                && (constructionDrawing || encounter.FireNavigationError != null || encounter.RobotNavigationError != null) && player != null)
            {
                var center = encounter.Center with { Y = player.Position.Y };
                var displayArena = new WorldPolygonGeometry(encounter.HostArena.GetContours().Single()
                    .Select(p => p with { Y = player.Position.Y }), center);
                Preview("bozja/arena", displayArena, outline: true);
            }
        }
        BozjaZone[] navigation = [];
        if (navigating)
        {
            if (encounter!.UsesDrawnArena) navigation = preparedArenaNavigation;
            else navigation = encounter.IsBurn ? preparedBurnNavigation : encounter.NavigationForHost(viewer!);
        }
        var keys = navigation.Select(z => z.Key).ToHashSet();
        foreach (var old in areas.Keys.Where(k => !keys.Contains(k)).ToArray())
        {
            foreach (var handle in areas[old].Handles) SafeZone.Remove(handle);
            areas.Remove(old);
        }
        foreach (var zone in navigation)
        {
            if (areas.TryGetValue(zone.Key, out var previous))
            {
                var old = previous.Zone;
                if (old.Activation == zone.Activation && old.Shape == zone.Shape && old.Origin == zone.Origin
                    && old.Rotation == zone.Rotation && old.Goal == zone.Goal && old.Obstacle == zone.Obstacle && old.JoinRadius == zone.JoinRadius
                    && (old.Join == null ? zone.Join == null
                        : zone.Join != null && old.Join.SequenceEqual(zone.Join))
                    && (old.Union == null ? zone.Union == null
                        : zone.Union != null && old.Union.SequenceEqual(zone.Union))) continue;
                foreach (var handle in previous.Handles) SafeZone.Remove(handle);
            }
            using var group = SafeZone.BeginDrawingGroup(zone.Component);
            var handles = new List<SafeZoneAreaHandle>();
            foreach (var shape in encounter!.ConvertNavigation(zone, navigationCenter with { Y = player!.Position.Y }, navigationRadius))
            {
                var template = new DungeonShapeTemplate(shape);
                SafeZoneAreaHandle handle;
                if (zone.Obstacle) handle = SafeZone.AddObstacle(template, new StaticTransform(default, 0));
                else if (zone.Goal) handle = SafeZone.AddPersistentSafeArea(template, new StaticTransform(default, 0), 1);
                else handle = SafeZone.AddExactDangerSnapshot(template,
                    // BMR's default clock means immediate. A real publication
                    // clock also permits the solver's preparation-time buffer.
                    new StaticTransform(default, 0), [new(zone.Activation == default ? CurrentTime : zone.Activation, null, default)]);
                if (!zone.Obstacle) SafeZone.SetNavigationOnly(handle, true);
                handles.Add(handle);
            }
            areas[zone.Key] = (zone, handles.ToArray());
        }
        PredictedDamage = [];
        PlannedFacing = null;
        PlannedTargetId = 0;
        InterruptTargetIds = navigating ? encounter!.InterruptTargets.Select(a => a.Actor.Id).ToArray() : [];
        MechanicHintsText = PredictedDamageText = null;
        if (enabled && encounter != null)
        {
            // Capture AI before DetermineArena's draw side effect. The old
            // DrawHost queries its public text again after the drawing pass.
            if (encounter.UsesDrawnArena && frameworkDrawing && viewer != null
                && encounter.Party.Any(p => p.EntityId == viewer.Actor.Id)
                && (MasterPlayerSettings.Instance.EnableRadar || MasterPlayerSettings.Instance.EnableRadarOverlay))
            {
                if (encounter.IsOzma)
                {
                    _ = encounter.Hints(viewer).ToArray();
                    encounter.ObserveOzmaDrawing(viewer);
                }
                else if (encounter.IsCll1) encounter.ObserveCll1Drawing(viewer);
                else if (encounter.IsOwain) encounter.ObserveOwainDrawing(viewer);
                else if (encounter.IsArachne) encounter.ObserveArachneDrawing(viewer);
                else encounter.ObserveCll4Drawing(viewer);
            }
            if (encounter.UsesDrawnArena && player != null)
            {
                var displayCenter = encounter.Center with { Y = player.Position.Y };
                var displayArena = encounter.HostArena switch
                {
                    AllianceArenaGeometry alliance => (IGeometryShape)alliance.AtHeight(player.Position.Y),
                    PolygonRegionGeometry region => region.AtHeight(player.Position.Y),
                    _ => new WorldPolygonGeometry(encounter.HostArena.GetContours().Single()
                        .Select(p => p with { Y = player.Position.Y }), displayCenter),
                };
                Preview("bozja/arena", displayArena, outline: true);
            }
            var cll3Drawing = !(encounter.IsCll3 || encounter.IsBaldesion || encounter.IsOzma)
                || MasterPlayerSettings.Instance.EnableRadar || MasterPlayerSettings.Instance.EnableRadarOverlay;
            var globalHints = cll3Drawing
                ? encounter.GlobalHints().Concat(viewer == null ? [] : encounter.VirtueDiagnostics(viewer)).ToArray() : [];
            MechanicHintsText = globalHints.Length == 0 ? null : string.Join("；", globalHints);
            var visible = encounter.Visible(viewer).ToArray();
            var towerViews = viewer == null ? [] : encounter.TowerViews(viewer).ToArray();
            var mineViews = viewer == null ? [] : encounter.MineViews(viewer).ToArray();
            // Source draws every component background in activation order,
            // followed by the tower foreground pass.
            foreach (var rule in encounter.ActiveRules)
            {
                if (rule.Kind == BozjaRuleKind.GauntletDonut && viewer != null)
                    foreach (var cast in encounter.GauntletDonuts)
                        Preview($"bozja/gauntlet/donut/{cast.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, 0, null));
                if (rule.Kind == BozjaRuleKind.SeekerIconBait && viewer != null)
                    foreach (var bait in encounter.SeekerBaits.Where(b => b.Rule == rule && !b.Source.Actor.Dead && b.Target.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.HornView(bait);
                        Preview($"bozja/seeker/bait/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                    }
                if (rule.Kind == BozjaRuleKind.SeekerLine && viewer != null)
                    foreach (var bait in encounter.SeekerLines.Where(b => !b.Source.Actor.Dead))
                    {
                        var cast = BozjaEncounter.SeekerLineView(bait, true);
                        var own = encounter.SeekerLines.Any(b => !b.Source.Actor.Dead && b.Target!.Actor.Id == viewer.Actor.Id);
                        Preview($"bozja/seeker/stack/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null),
                            safe: !own || bait.Target!.Actor.Id == viewer.Actor.Id);
                    }
                if (rule.Kind == BozjaRuleKind.Time)
                    for (var i = 0; i < encounter.TimeSafeSpots.Count; ++i)
                        Preview($"bozja/time/safe/{i}", BozjaEncounter.TimeEruptionShape.Resolve(
                            encounter.TimeSafeSpots[i] with { Y = player?.Position.Y ?? 0 }, 0, null), safe: true);
                if (rule.Kind == BozjaRuleKind.Hailfire && viewer != null)
                    foreach (var bait in encounter.Hailfires.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.HornView(bait);
                        Preview($"bozja/hailfire/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                    }
                if (rule.Kind == BozjaRuleKind.ChargeTether && viewer != null)
                    foreach (var bait in encounter.SteelBaits.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.SteelView(bait);
                        if (cast.Drawable) Preview($"bozja/steel/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                    }
                if (rule.Kind == BozjaRuleKind.CircleBait && viewer != null)
                    foreach (var (cast, outline) in encounter.CircleBaitViews(viewer, rule).Where(v => !v.Outline))
                        Preview($"bozja/virtue/wind/{cast.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, 0, null));
                foreach (var aoe in visible.Where(a => a.Cast.Rule == rule))
                {
                    var cast = aoe.Cast;
                    if (cast.Shape == null && encounter.IsVirtue) continue;
                    if (!cast.Drawable) continue;
                    var shape = cast.Rule.Kind == BozjaRuleKind.SeekerBlade
                        ? encounter.SeekerBladeDrawing(cast, player?.Position.Y ?? 0)
                        : cast.Shape!.Resolve(cast.Origin with { Y = player?.Position.Y ?? 0 }, cast.Rotation, null);
                    Preview($"bozja/aoe/{cast.Generation}", shape,
                        safe: cast.Rule.Safe || aoe.SafeColor, color: aoe.Danger ? new(1, 0, 0, 1) : null);
                }
                if (rule.Kind == BozjaRuleKind.BurnEscort && viewer != null)
                    foreach (var (cast, outline) in encounter.BurnTrackingViews(viewer).Where(v => !v.Outline))
                        Preview($"bozja/burn/tracking/{cast.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                if (rule.Kind == BozjaRuleKind.Tower)
                    foreach (var tower in towerViews.Where(t => t.Background && (!encounter.IsVirtue || t.Cast.Rule == rule)))
                        Preview($"bozja/tower/{tower.Cast.Generation}/background", new CircleGeometry(tower.Cast.Origin with { Y = player?.Position.Y ?? 0 }, BozjaEncounter.TowerRadius(tower.Cast)));
                if (rule.Kind == BozjaRuleKind.LightningRod)
                    foreach (var mine in mineViews.Where(m => m.Background))
                        Preview($"bozja/mine/{mine.Mine.Generation}/background",
                            new CircleGeometry(mine.Mine.Position with { Y = player?.Position.Y ?? 0 }, mine.Radius));
                if (rule.Kind == BozjaRuleKind.Rush && viewer != null)
                    foreach (var bait in encounter.Rushes.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.RushView(bait);
                        if (cast.Drawable) Preview($"bozja/rush/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                    }
                if (rule.Kind == BozjaRuleKind.Horn && viewer != null)
                    foreach (var bait in encounter.Horns.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.HornView(bait);
                        Preview($"bozja/horn/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                    }
                if (rule.Kind == BozjaRuleKind.DahuBait && viewer != null)
                    foreach (var bait in encounter.DahuBaits.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.HornView(bait);
                        Preview($"bozja/dahu/bait/{bait.Generation}/background",
                            cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null));
                    }
                if (rule.Kind == BozjaRuleKind.DahuBurn && viewer != null)
                    foreach (var bait in encounter.DahuBurns.Where(b => !b.Source.Actor.Dead && b.Target!.Actor.Id != viewer.Actor.Id))
                    {
                        var cast = BozjaEncounter.DahuBurnView(bait);
                        Preview($"bozja/dahu/burn/{bait.Generation}/background", cast.Shape.Resolve(
                            cast.Origin with { Y = player?.Position.Y ?? 0 }, cast.Rotation, null));
                    }
            }
            if (viewer != null)
            {
                // The old host captures AI diagnostics before AddHints clears a dead slot.
                var bombActivation = encounter.DiabloBombActivation(viewer);
                // Ordinary event renders retain the Framework AI publication,
                // including its countdown, after drawing has cleared dead slots.
                var cll3ModeText = preparedStayModeOwner is { } modeOwner
                    && modeOwner.Encounter == encounter && modeOwner.Version == encounter.LoadVersion
                    && modeOwner.Viewer == viewer.Actor.Id ? preparedStayModeText : null;
                // Capture queries StayMove before DrawHost queries its public
                // hints again. With both views asleep neither query runs.
                if ((encounter.IsCll3 || encounter.IsArt) && cll3Drawing) _ = encounter.Hints(viewer).ToArray();
                // Event redraws must not consume Ozma's dead slot before the
                // next Framework AI capture and its two source draw queries.
                var hints = cll3Drawing ? encounter.Hints(viewer, consumeOzmaState: frameworkDrawing).ToArray() : [];
                var texts = globalHints.Concat(hints.Select(h => h.Risky ? h.Text : $"{h.Text}（已满足）")).ToArray();
                MechanicHintsText = texts.Length == 0 ? null : string.Join("；", texts);
                if (bombActivation != default)
                    MechanicHintsText = string.Join("；", new[] { MechanicHintsText,
                        bombActivation > CurrentTime ? $"Pyretic：{(bombActivation - CurrentTime).TotalSeconds:F1}s 后生效" : "Pyretic：生效中" }.Where(t => t != null));
                if (encounter.CuchMisdirection(viewer))
                    MechanicHintsText = string.Join("；", new[] { MechanicHintsText, "Misdirection：生效中" }.Where(t => t != null));
                if (cll3ModeText != null)
                    MechanicHintsText = string.Join("；", new[] { MechanicHintsText, cll3ModeText }.Where(t => t != null));
                if (encounter.IsArt)
                    foreach (var chaser in encounter.ArtChasers)
                    {
                        var key = $"bozja/art/chase/{chaser.Generation}";
                        wanted.Add(key);
                        order.Add(key);
                        publish.Add(() => Draw.AddLinePreview(key, chaser.Previous with { Y = player!.Position.Y },
                            chaser.Target.Actor.Position with { Y = player.Position.Y }, false));
                    }
                var cll1LineIndex = 0;
                foreach (var line in encounter.Cll1InfraredLines())
                {
                    var key = $"bozja/cll1/infrared/{cll1LineIndex++}";
                    wanted.Add(key); order.Add(key);
                    publish.Add(() => Draw.AddLinePreview(key, line.From, line.To, line.Safe));
                }
                foreach (var movement in encounter.Cll1MagnetMovements(viewer))
                {
                    const string key = "bozja/cll1/magnet/line";
                    wanted.Add(key); order.Add(key);
                    publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                }
                foreach (var (slot, target) in encounter.DeathgazeGreenPlayers())
                {
                    var point = target.Actor.Position with { Y = player!.Position.Y };
                    var key = $"bozja/deathgaze/green/{slot}";
                    wanted.Add(key); order.Add(key);
                    publish.Add(() => Draw.AddAoePreview(key, new CircleGeometry(point, 5), true, true, 1,
                        unclipped: true, color: new Vector4(0, 1, 0, 1), strokeThickness: target.Actor.Id == viewer.Actor.Id ? 2 : 1));
                    if (target.Actor.Id == viewer.Actor.Id || target.Destroyed || !target.Actor.Targetable) continue;
                    // MP renders actor priority as a point marker. Keep the
                    // source's own-player exclusion, actor filter and wall clamp.
                    var markerPoint = new Vector3(Math.Clamp(point.X, encounter.Center.X - 30, encounter.Center.X + 30),
                        point.Y, Math.Clamp(point.Z, encounter.Center.Z - 15, encounter.Center.Z + 15));
                    var markerKey = $"bozja/deathgaze/interesting/{slot}";
                    wanted.Add(markerKey); order.Add(markerKey);
                    publish.Add(() => Draw.AddAoePreview(markerKey, new CircleGeometry(markerPoint, .5f), true, false, 1,
                        color: new Vector4(192 / 255f, 192 / 255f, 192 / 255f, 1)));
                }
                foreach (var rule in encounter.ActiveRules)
                {
                    if (rule.Kind == BozjaRuleKind.AllianceKnockback)
                        foreach (var movement in encounter.AllianceSimpleMovements(viewer, rule))
                        {
                            if (movement.From == movement.To) continue;
                            var key = $"bozja/alliance/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.AlliancePull)
                        foreach (var movement in encounter.AlliancePullMovements(viewer))
                        {
                            var key = $"bozja/alliance/pull/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.CircleBait)
                        foreach (var (cast, outline) in encounter.CircleBaitViews(viewer, rule).Where(v => v.Outline))
                            Preview($"bozja/virtue/wind/{cast.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, 0, null), outline: true);
                    if (rule.Kind == BozjaRuleKind.Cll3Water && cll3Drawing)
                        foreach (var movement in encounter.Cll3WaterMovements(viewer, rule))
                        {
                            var key = $"bozja/cll3/water/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.Cll4Wind)
                        foreach (var movement in encounter.Cll4Movements(viewer))
                        {
                            var key = $"bozja/cll4/wind/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind is BozjaRuleKind.GauntletTurbine or BozjaRuleKind.GauntletWind)
                        foreach (var movement in encounter.GauntletMovements(viewer, rule))
                        {
                            var key = $"bozja/gauntlet/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.BurnEscort)
                        foreach (var (cast, outline) in encounter.BurnTrackingViews(viewer).Where(v => v.Outline))
                            Preview($"bozja/burn/tracking/{cast.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null), outline: true);
                    if (rule.Kind == BozjaRuleKind.GuardParry)
                        foreach (var arc in encounter.GuardArcs())
                            for (var i = 1; i < arc.Points.Length; ++i)
                            {
                                var key = $"bozja/{arc.Key}/{i}";
                                var start = arc.Points[i - 1] with { Y = player!.Position.Y };
                                var end = arc.Points[i] with { Y = player.Position.Y };
                                wanted.Add(key);
                                order.Add(key);
                                publish.Add(() => Draw.AddLinePreview(key, start, end, false));
                            }
                    if (rule.Kind == BozjaRuleKind.SeekerIconBait)
                        foreach (var bait in encounter.SeekerBaits.Where(b => b.Rule == rule && !b.Source.Actor.Dead && b.Target.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.HornView(bait);
                            Preview($"bozja/seeker/bait/{bait.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null), outline: true);
                        }
                    if (rule.Kind == BozjaRuleKind.SaunionBait)
                        foreach (var cast in encounter.Casts.Where(c => c.Rule == rule && !c.Source.Actor.Dead && c.Target!.Actor.Id == viewer.Actor.Id))
                        {
                            var view = BozjaEncounter.SaunionBaitView(cast);
                            Preview($"bozja/saunion/bait/{cast.Generation}/outline",
                                view.Shape.Resolve(view.Origin with { Y = player!.Position.Y }, 0, null), outline: true);
                        }
                    if (rule.Kind == BozjaRuleKind.DiabloOrbs)
                        foreach (var orb in encounter.DiabloOrbs)
                            Preview($"bozja/diablo/orb/{orb.BirthOrder}", new CircleGeometry(orb.Actor.Position with { Y = player!.Position.Y }, 1.5f), safe: true, outline: true);
                    if (rule.Kind == BozjaRuleKind.DiabloKnockback)
                        foreach (var movement in encounter.DiabloMovements(viewer).Where(m => m.From != m.To))
                        {
                            var key = $"bozja/diablo/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.SaunionKnockback)
                        foreach (var movement in encounter.SaunionMovements(viewer).Where(m => m.From != m.To))
                        {
                            var key = $"bozja/saunion/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.SeekerChains && encounter.SeekerPartner(viewer) is { } partner)
                    {
                        var key = "bozja/seeker/chain";
                        wanted.Add(key);
                        order.Add(key);
                        publish.Add(() => Draw.AddLinePreview(key, viewer.Actor.Position with { Y = player!.Position.Y },
                            partner.Actor.Position with { Y = player!.Position.Y }, false));
                    }
                    if (rule.Kind is BozjaRuleKind.CrystalSpread or BozjaRuleKind.Spread or BozjaRuleKind.RaidenSpread or BozjaRuleKind.DahuSpit)
                    {
                        foreach (var spread in encounter.Spreads(rule))
                            Preview($"bozja/spread/{spread.Generation}/outline", spread.Shape.Resolve(
                                DungeonEncounter.QuantizedDungeonPosition(spread.Target!.Actor.Position)
                                    with { Y = player!.Position.Y }, 0, null), outline: true);
                        if (encounter.IsCrystalSpreadTarget(viewer))
                            foreach (var crystal in encounter.Crystals)
                                Preview($"bozja/crystal/{crystal.BirthOrder}/hitbox",
                                    new CircleGeometry(crystal.Actor.Position with { Y = player!.Position.Y },
                                        crystal.Actor.HitboxRadius), outline: true);
                    }
                    if (rule.Kind == BozjaRuleKind.Hailfire)
                        foreach (var bait in encounter.Hailfires.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.HornView(bait);
                            Preview($"bozja/hailfire/{bait.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null), outline: true);
                        }
                    if (rule.Component == "GraspingRancor" && encounter.Hand(viewer) is { } hand)
                    {
                        var key = $"bozja/hand/{hand.Actor.Id}";
                        wanted.Add(key);
                        order.Add(key);
                        publish.Add(() => Draw.AddLinePreview(key, hand.Actor.Position with { Y = player?.Position.Y ?? 0 },
                            viewer.Actor.Position with { Y = player?.Position.Y ?? 0 }, hand.Tether?.Id == 2));
                    }
                    if (rule.Kind is BozjaRuleKind.Stack or BozjaRuleKind.BombStack)
                        foreach (var stack in encounter.Stacks(rule))
                            Preview($"bozja/stack/{stack.Generation}", stack.Shape.Resolve(
                                BozjaEncounter.StackCenter(stack) with { Y = player?.Position.Y ?? 0 }, 0, null),
                                encounter.StackSafe(stack, viewer), true);
                    if (rule.Kind == BozjaRuleKind.Wind)
                        foreach (var movement in encounter.WindMovements(viewer))
                        {
                            var key = $"bozja/wind/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.BracingWind)
                        foreach (var movement in encounter.BracingMovements(viewer))
                        {
                            var key = $"bozja/bracing/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind is BozjaRuleKind.GallowsMarch or BozjaRuleKind.RobotMarch)
                    {
                        var index = 0;
                        foreach (var movement in rule.Kind == BozjaRuleKind.GallowsMarch
                            ? encounter.GraveMovements(viewer) : encounter.RobotMovements(viewer))
                        {
                            var key = $"bozja/{(rule.Kind == BozjaRuleKind.GallowsMarch ? "grave" : "robots")}/march/{index++}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    }
                    if (rule.Kind == BozjaRuleKind.CuchMarch)
                    {
                        var index = 0;
                        foreach (var movement in encounter.CuchMovements(viewer))
                        {
                            var key = $"bozja/cuch/march/{index++}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    }
                    if (rule.Kind == BozjaRuleKind.PhantomKnockback)
                        foreach (var movement in encounter.PhantomMovements(viewer))
                        {
                            var key = $"bozja/phantom/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.QueenKnockback)
                        foreach (var movement in encounter.QueenMovements(viewer))
                        {
                            var key = $"bozja/queen/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind is BozjaRuleKind.DahuKnockback or BozjaRuleKind.DahuSavageKnockback)
                        foreach (var movement in encounter.DahuMovements(viewer))
                        {
                            var key = $"bozja/dahu/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind is BozjaRuleKind.ForwardKnockback or BozjaRuleKind.SideKnockback)
                        foreach (var movement in encounter.CavalryMovements(viewer, rule))
                        {
                            var key = $"bozja/knockback/{movement.Cast.Generation}";
                            wanted.Add(key);
                            order.Add(key);
                            publish.Add(() => Draw.AddLinePreview(key, movement.From, movement.To, false));
                        }
                    if (rule.Kind == BozjaRuleKind.ChargeTether)
                    {
                        foreach (var bait in encounter.SteelBaits.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.SteelView(bait);
                            if (cast.Drawable) Preview($"bozja/steel/{bait.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null), outline: true);
                        }
                        var activeOwn = encounter.SteelBaits.Any(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, viewer));
                        var bad = encounter.SteelTethers.Contains((viewer, 57));
                        var good = encounter.SteelTethers.Contains((viewer, 1));
                        if (activeOwn && (bad || good))
                            foreach (var bait in encounter.SteelBaits.Where(b => ReferenceEquals(b.Target, viewer)))
                            {
                                var key = $"bozja/steel/{bait.Generation}/tether";
                                wanted.Add(key);
                                order.Add(key);
                                publish.Add(() => Draw.AddLinePreview(key, bait.Source.Actor.Position with { Y = player!.Position.Y },
                                    viewer.Actor.Position with { Y = player.Position.Y }, !bad && good));
                            }
                    }
                    if (rule.Kind == BozjaRuleKind.Tower)
                        foreach (var tower in towerViews.Where(t => !encounter.IsVirtue || t.Cast.Rule == rule))
                        {
                            var shape = new CircleGeometry(tower.Cast.Origin with { Y = player?.Position.Y ?? 0 }, BozjaEncounter.TowerRadius(tower.Cast));
                            if (tower.Outline) Preview($"bozja/tower/{tower.Cast.Generation}/outline", shape, tower.Safe, true);
                        }
                    if (encounter.VirtueTowerSource(viewer, rule) is { } virtueSource)
                    {
                        var key = $"bozja/virtue/tower/{rule.Component}/line";
                        wanted.Add(key);
                        order.Add(key);
                        publish.Add(() => Draw.AddLinePreview(key, virtueSource.Actor.Position, player!.Position, false));
                        Preview($"bozja/virtue/tower/{rule.Component}/source",
                            new CircleGeometry(virtueSource.Actor.Position with { Y = player!.Position.Y }, 2), outline: true);
                    }
                    if (rule.Kind == BozjaRuleKind.LightningRod)
                        foreach (var mine in mineViews.Where(m => m.Outline))
                            Preview($"bozja/mine/{mine.Mine.Generation}/outline",
                                new CircleGeometry(mine.Mine.Position with { Y = player?.Position.Y ?? 0 }, mine.Radius), mine.Safe, true);
                    if (rule.Kind == BozjaRuleKind.Rush)
                        foreach (var bait in encounter.Rushes.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.RushView(bait);
                            if (cast.Drawable) Preview($"bozja/rush/{bait.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null),
                                outline: true, color: new(1, 0, 0, 1));
                        }
                    if (rule.Kind is BozjaRuleKind.Gaze or BozjaRuleKind.CastGaze or BozjaRuleKind.SeekerMoon or BozjaRuleKind.BurnWeakpoint or BozjaRuleKind.GauntletUnseen or BozjaRuleKind.OwainGaze)
                        foreach (var eye in encounter.EyesFor(viewer))
                        {
                            var danger = BozjaEncounter.EyeDanger(eye, player!);
                            var eyePosition = eye.Position with { Y = player!.Position.Y };
                            Preview($"bozja/eye/{eye.Generation}", eyeSymbol.Resolve(eyePosition, 0, null), !danger,
                                color: danger ? new(1, .15f, .15f, 1) : new(.2f, 1, .2f, 1));
                            Preview($"bozja/pupil/{eye.Generation}", new CircleGeometry(eyePosition, .28f), !danger,
                                color: Vector4.One);
                            if (BozjaEncounter.DistanceSquared(eye.Position, player!.Position) > eye.Range * eye.Range) continue;
                            var points = BozjaEncounter.EyeArc(eye, player);
                            for (var i = 1; i < points.Length; ++i)
                            {
                                var key = $"bozja/eye/{eye.Generation}/arc/{i}";
                                var start = points[i - 1];
                                var end = points[i];
                                wanted.Add(key);
                                order.Add(key);
                                publish.Add(() => Draw.AddLinePreview(key, start, end, false));
                            }
                        }
                    if (rule.Kind == BozjaRuleKind.Horn)
                        foreach (var bait in encounter.Horns.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.HornView(bait);
                            Preview($"bozja/horn/{bait.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null),
                                outline: true, color: new(1, 0, 0, 1));
                        }
                    if (rule.Kind == BozjaRuleKind.DahuBait)
                        foreach (var bait in encounter.DahuBaits.Where(b => !b.Source.Actor.Dead && b.Target.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.HornView(bait);
                            Preview($"bozja/dahu/bait/{bait.Generation}/outline",
                                cast.Shape.Resolve(cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null), outline: true);
                        }
                    if (rule.Kind == BozjaRuleKind.DahuBurn)
                        foreach (var bait in encounter.DahuBurns.Where(b => !b.Source.Actor.Dead && b.Target!.Actor.Id == viewer.Actor.Id))
                        {
                            var cast = BozjaEncounter.DahuBurnView(bait);
                            Preview($"bozja/dahu/burn/{bait.Generation}/outline", cast.Shape.Resolve(
                                cast.Origin with { Y = player!.Position.Y }, cast.Rotation, null), outline: true);
                        }
                    if (rule.Kind == BozjaRuleKind.Cleave)
                        foreach (var cleave in encounter.FireCleaves())
                            Preview($"bozja/cleave/{cleave.Generation}/outline",
                                cleave.Shape.Resolve(DungeonEncounter.QuantizedDungeonPosition(cleave.Origin)
                                    with { Y = player!.Position.Y }, cleave.Rotation, null), outline: true);
                    if (rule.Kind == BozjaRuleKind.OwainCleave)
                        foreach (var cleave in encounter.OwainCleaves())
                            Preview($"bozja/owain/cleave/{cleave.Generation}/outline", cleave.Shape.Resolve(
                                cleave.Origin with { Y = player!.Position.Y }, cleave.Rotation, null), outline: true);
                    if (rule.Kind == BozjaRuleKind.RaidenChains && encounter.RaidenPartner(viewer) is { } raidenPartner)
                    {
                        var key = $"bozja/raiden/chain/{viewer.Actor.Id}";
                        wanted.Add(key);
                        publish.Add(() => Draw.AddLinePreview(key, player!.Position, raidenPartner.Actor.Position, false));
                    }
                    if (rule.Kind == BozjaRuleKind.OwainSpirit && encounter.OwainSpiritTargets.Contains(viewer)
                        && encounter.OwainSpiritStack is { Target: { Actor.Dead: false } } spiritStack)
                        Preview($"bozja/owain/spirit/{spiritStack.Generation}/outline", spiritStack.Shape.Resolve(
                            BozjaEncounter.StackCenter(spiritStack) with { Y = player!.Position.Y }, 0, null),
                            safe: encounter.OwainSpiritSafe(viewer), outline: true);
                }
                if (navigating) PredictedDamage = encounter.UsesDrawnArena ? preparedArenaDamage : encounter.DamageForHost(viewer).ToArray();
                if (navigating && IsMechanismEnabled("面向控制")) PlannedFacing = encounter.UsesDrawnArena ? preparedArenaFacing : encounter.Facing(player!);
                if (navigating && (encounter.IsAlbeleo || encounter.IsSkirmish || encounter.Definition.Rules.Any(r => r.Kind is BozjaRuleKind.Gaze or BozjaRuleKind.TooHot)))
                {
                    var partyPlayer = encounter.Party.FirstOrDefault(p => p.Slot == 0);
                    var playerHeight = partyPlayer == null ? 0
                        : encounter.Actors.GetValueOrDefault(partyPlayer.EntityId)?.Actor.Position.Y ?? 0;
                    PlannedTargetId = QuestTargetPriority.Select(encounter.Definition.Territory,
                        encounter.Actors.Values.Select(a => a.Actor), player!.TargetId, player.Position,
                        encounter.Party.Select(p => p.EntityId).ToHashSet(), allowedFate, encounter.TargetPriority, playerHeight);
                }
                PredictedDamageText = PredictedDamage.Length == 0 ? null
                    : $"预计伤害：{PredictedDamage.Length}组，{PredictedDamage.Sum(d => BitOperations.PopCount(d.Mask))}人次";
            }
        }
        if (timeError != null) MechanicHintsText = timeError;
        foreach (var old in drawing.Where(k => !wanted.Contains(k)).ToArray()) Draw.Remove(old);
        foreach (var submit in publish) submit();
        Draw.SetPreviewOrder(order);
        drawing.Clear();
        drawing.UnionWith(wanted);
        Hints.SetFacing(PlannedFacing);
        Hints.SetTarget(PlannedTargetId);
        runtime.RequestAutomaticMovement(this, navigating && !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0);
    }

    private void ClearPreparedArena()
    {
        preparedArenaNavigation = [];
        preparedArenaDamage = [];
        preparedArenaFacing = null;
        preparedArenaBoundary = null;
        preparedArenaCenter = default;
        preparedArenaRadius = 0;
        preparedArenaOwner = null;
    }

    private void ReleaseBoundary()
    {
        var manager = MasterPlayerPlugin.Instance?.SafeZone.Manager;
        if (boundary != null && manager != null
            && ReferenceEquals(manager.BuildSnapshot(CurrentTime).ArenaBoundary, boundary)) manager.ClearArenaBoundary();
        boundary = null;
        boundarySource = null;
    }

    private void Clear()
    {
        foreach (var area in areas.Values)
            foreach (var handle in area.Handles) SafeZone.Remove(handle);
        areas.Clear();
        foreach (var key in drawing) Draw.Remove(key);
        drawing.Clear();
        ReleaseBoundary();
        region.Clear();
        preparedBurnNavigation = [];
        ClearPreparedArena();
        frameworkDrawing = false;
        pendingEvents.Clear();
        lastAnimation.Clear();
        frameAnimations.Clear();
        sampledActors = [];
        preparedStayModeText = null;
        preparedStayModeOwner = null;
        reportedTimeError = null;
        PredictedDamage = [];
        InterruptTargetIds = [];
        PlannedFacing = null;
        PlannedTargetId = 0;
        Hints.SetFacing(null);
        Hints.SetTarget(0);
        MechanicHintsText = PredictedDamageText = null;
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        nextScan = default;
        observedVersion = -1;
        needsFrameworkScan = false;
    }

    protected override void OnStop() => Clear();
    protected override void OnMovementDisabled()
        => MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
}
