using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Hydatos;

public abstract class HydatosScript : MasterPlayerScript
{
    private readonly HydatosRegion region = new();
    private readonly Dictionary<string, (HydatosNavigation Zone, SafeZoneAreaHandle Handle)> areas = [];
    private readonly HashSet<string> drawing = [];
    private static readonly LocalPolygonTemplate eye = DungeonEncounter.CreateQueenEyeSymbol();
    private static readonly LocalPolygonTemplate arc = DungeonEncounter.CreateQueenGazeArc();
    private static readonly LocalPolygonTemplate marker = new([new(-.45f, .4f), new(0, -.7f), new(.45f, .4f)]);
    private IGeometryShape? boundary;
    private ReplayActorState[] actors = [];
    private ReplayPartyMember[] party = [];
    private uint? allowedFate;
    private DateTime nextScan;
    private long observedVersion = -1;
    public bool EncounterActive => region.Selected?.Active == true;
    public string? MechanicHintsText { get; private set; }
    public string? PredictedDamageText { get; private set; }
    internal DungeonDamagePrediction[] PredictedDamage { get; private set; } = [];
    internal float? PlannedFacing { get; private set; }
    internal ulong PlannedTargetId { get; private set; }

    public override IEnumerable<string> GetMechanisms()
        => HydatosCatalog.Definitions.Select(d => d.Name).Append("面向控制");

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        Scan();
        return region.Selected is { Active: true } model && IsMechanismEnabled(model.Definition.Name)
            && model.Actors.TryGetValue(cast.SourceId, out var source) && source.NativeNpc
            && model.Definition.Rules.Any(r => r.Kind != HydatosRuleKind.Dispel && r.Actions.Contains(cast.ActionId));
    }

    protected override void OnStart()
    {
        OnLogEvent(e =>
        {
            Scan();
            if (e is NativeActionEffectEvent effect && effect.ActionType is null or 1)
                region.Effect(effect.SourceId, effect.ActionId,
                    effect.RotationInt is { } raw ? HydatosEncounter.DecodeRotation(raw) : null, effect.MainTargetId);
            else if (e is LogSystemTargetIconEvent icon)
                region.Icon(icon.SourceId, icon.MarkerId, icon.TargetId);
            // Expanded PR ActionEffect and Status events do not supply a
            // second copy of the native component callbacks.
            Render();
        });
        OnTerritoryChanged(_ => Clear());
        Scan();
        Render();
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (CurrentTime >= nextScan || ReplayWorld != null && ReplayWorld.Version != observedVersion) Scan(true);
        Render();
    }

    private void Scan(bool frameworkUpdate = false)
    {
        var now = CurrentTime;
        ulong source;
        if (ReplayWorld is { } world)
        {
            // Several events can share one world snapshot. They advance their
            // clocks, but must not each retire/promote the manager's modules.
            if (!frameworkUpdate && world.Version == observedVersion)
            {
                foreach (var model in region.Encounters) model.Advance(now);
                return;
            }
            actors = world.Actors.Values.ToArray();
            party = world.Party.ToArray();
            source = world.SourcePlayerId ?? world.LocalPlayerId;
            allowedFate = world.AllowedFateId;
            observedVersion = world.Version;
        }
        else
        {
            var enmity = ReplayWorldCapture.CapturePlayerEnmity();
            actors = Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now, playerEnmity: enmity)).ToArray();
            party = ReplayWorldCapture.CaptureParty();
            source = ReplayWorldCapture.CaptureSourcePlayerId() ?? ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
            var fate = ReplayWorldCapture.CaptureActiveFate();
            allowedFate = ReplayWorldCapture.CaptureAllowedFate(CurrentPlayer?.Level, fate);
        }
        nextScan = now.AddSeconds(.1);
        region.Synchronize(actors, party, source, now);
    }

    private void Render()
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        var model = region.Selected;
        var enabled = model != null && IsMechanismEnabled(model.Definition.Name);
        var player = CurrentPlayer;
        var wanted = new HashSet<string>();
        void Preview(string key, IGeometryShape geometry, bool safe = false, bool outline = false, Vector4? color = null)
        {
            wanted.Add(key);
            Draw.AddAoePreview(key, geometry, safe, outline, .5f, unclipped: true, color: color);
        }
        if (enabled && model!.Primary is { } primary)
        {
            foreach (var actor in model.Actors.Values.Where(a => ReferenceEquals(a, primary)
                || model.Definition.DrawOids.Contains(a.Actor.Oid)))
                if (!actor.Actor.Dead && actor.Actor.Targetable)
                    Preview($"hydatos/enemy/{actor.Actor.Id}", marker.Resolve(actor.Actor.Position, actor.Actor.Rotation, null),
                        color: new(1, 0, 0, 1));
        }
        var navigating = enabled && model!.Active && player is { Dead: false }
            && party.Any(p => p.EntityId == player.Id) && model.Actors.ContainsKey(model.SourcePlayerId);
        if (navigating)
        {
            var next = model!.NavigationBoundary(player!.Position.Y);
            if (boundary == null || boundary.GetType() != next.GetType() || boundary.Position != next.Position) boundary = next;
            runtime.Manager.SetArenaBoundary(boundary);
            Preview("hydatos/arena", new CircleGeometry(model.ArenaCenter with { Y = player.Position.Y }, 30), outline: true);
        }
        else ReleaseBoundary();
        var navigation = navigating ? model!.Navigation(player!).ToArray() : [];
        var keys = navigation.Select(n => $"{model!.Definition.Name}/{n.Key}").ToHashSet();
        foreach (var key in areas.Keys.Where(k => !keys.Contains(k)).ToArray())
        {
            SafeZone.Remove(areas[key].Handle);
            areas.Remove(key);
        }
        foreach (var zone in navigation)
        {
            var key = $"{model!.Definition.Name}/{zone.Key}";
            if (areas.TryGetValue(key, out var previous))
            {
                if (previous.Zone.SameOutput(zone)) continue;
                SafeZone.Remove(previous.Handle);
            }
            using var group = SafeZone.BeginDrawingGroup(zone.Component);
            var handle = SafeZone.AddExactDangerSnapshot(zone.Shape, new StaticTransform(zone.Origin, zone.Rotation),
                [new(zone.Activation, null, zone.StartedAt)]);
            SafeZone.SetNavigationOnly(handle, true);
            areas[key] = (zone, handle);
        }
        PlannedFacing = null;
        PredictedDamage = [];
        var hints = new List<string>();
        if (enabled && model!.Active)
        {
            foreach (var aoe in model.VisibleAoes())
            {
                var shape = aoe.Shape;
                var origin = aoe.Origin;
                var safe = false;
                var outline = false;
                switch (aoe.Rule.Kind)
                {
                    case HydatosRuleKind.Gaze:
                        if (player == null) continue;
                        var delta = HydatosEncounter.Xz(aoe.Origin - player.Position);
                        var danger = delta != Vector2.Zero && Vector2.Dot(Vector2.Normalize(delta),
                            new(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation))) >= .707107f;
                        Preview($"hydatos/eye/{aoe.Id}", eye.Resolve(aoe.Origin, 0, null), !danger,
                            color: danger ? new(1, .15f, .15f, 1) : new(.2f, 1, .2f, 1));
                        Preview($"hydatos/pupil/{aoe.Id}", new CircleGeometry(aoe.Origin, .28f), !danger, color: Vector4.One);
                        continue;
                    case HydatosRuleKind.Pull:
                    case HydatosRuleKind.Petrattraction:
                        continue;
                    case HydatosRuleKind.Bait:
                        if (aoe.Source.Actor.Dead || aoe.Target == null) continue;
                        origin = aoe.Target.Actor.Position;
                        outline = aoe.Target.Actor.Id == player?.Id;
                        break;
                    case HydatosRuleKind.IconStack:
                    case HydatosRuleKind.AkhMorn:
                    case HydatosRuleKind.IonShower:
                        if (aoe.Target?.Actor.Dead != false) continue;
                        origin = HydatosEncounter.Quantized(aoe.Target.Actor.Position);
                        safe = aoe.Rule.Kind != HydatosRuleKind.IonShower && model.StackSafe(aoe, player);
                        outline = true;
                        break;
                }
                if (shape != null)
                    Preview($"hydatos/aoe/{aoe.Id}", shape.Resolve(origin, aoe.Rotation, null), safe, outline,
                        aoe.Danger ? new(1, 0, 0, 1) : null);
            }
            foreach (var dispel in model.DispelTargets)
                Preview($"hydatos/dispel/{dispel.Rule.Component}/{dispel.Actor.Actor.Id}",
                    marker.Resolve(dispel.Actor.Actor.Position, dispel.Actor.Actor.Rotation, null), color: new(1, 1, 0, 1));
            if (player != null)
            {
                if (model.Gazes(player).Length > 0)
                    Preview("hydatos/gaze/view", arc.Resolve(player.Position, player.Rotation, null));
                if (navigating && IsMechanismEnabled("面向控制")) PlannedFacing = model.Facing(player);
                foreach (var pull in model.Pulls(player))
                {
                    var key = $"hydatos/pull/{pull.Aoe.Id}";
                    wanted.Add(key);
                    Draw.AddLinePreview(key, pull.From, pull.To, false);
                    Preview(key + "/destination", marker.Resolve(pull.To, player.Rotation, null));
                }
                hints.AddRange(model.Hints(player));
            }
            PredictedDamage = model.Damage().ToArray();
        }
        foreach (var old in drawing.Where(k => !wanted.Contains(k)).ToArray()) Draw.Remove(old);
        drawing.Clear();
        drawing.UnionWith(wanted);
        Hints.SetFacing(PlannedFacing);
        PlannedTargetId = navigating ? QuestTargetPriority.Select(HydatosCatalog.TERRITORY, actors, player!.TargetId,
            player.Position, party.Select(p => p.EntityId).ToHashSet(), allowedFate, model!.TargetPriority) : 0;
        Hints.SetTarget(PlannedTargetId);
        runtime.RequestAutomaticMovement(this, navigating && !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0);
        MechanicHintsText = hints.Count == 0 ? null : string.Join("；", hints.Distinct());
        PredictedDamageText = PredictedDamage.Length == 0 ? null : $"预计伤害：{PredictedDamage.Length}人";
    }

    private void ReleaseBoundary()
    {
        var manager = MasterPlayerPlugin.Instance?.SafeZone.Manager;
        if (boundary != null && manager != null
            && ReferenceEquals(manager.BuildSnapshot(CurrentTime).ArenaBoundary, boundary)) manager.ClearArenaBoundary();
        boundary = null;
    }

    private void Clear()
    {
        foreach (var area in areas.Values) SafeZone.Remove(area.Handle);
        areas.Clear();
        foreach (var key in drawing) Draw.Remove(key);
        drawing.Clear();
        ReleaseBoundary();
        region.Clear();
        Hints.SetFacing(null);
        Hints.SetTarget(0);
        PlannedFacing = null;
        PlannedTargetId = 0;
        PredictedDamage = [];
        PredictedDamageText = MechanicHintsText = null;
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        nextScan = default;
        observedVersion = -1;
    }

    protected override void OnStop() => Clear();
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current) => Render();
    protected override void OnMovementDisabled()
    {
        Hints.SetFacing(null);
        Hints.SetTarget(0);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }
}
