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

namespace MasterPlayer.Mechanics.Fates;

public abstract class CrescentFateScript : MasterPlayerScript
{
    private readonly CrescentFateEncounter encounter;
    private readonly Dictionary<long, (CrescentFateCast Cast, SafeZoneAreaHandle Handle)> areas = [];
    private readonly HashSet<string> drawing = [];
    private static readonly LocalPolygonTemplate eye = DungeonEncounter.CreateQueenEyeSymbol();
    private static readonly LocalPolygonTemplate arc = DungeonEncounter.CreateQueenGazeArc();
    private static readonly LocalPolygonTemplate marker = new([new(-.45f, .4f), new(0, -.7f), new(.45f, .4f)]);
    private CircleGeometry? boundary;
    private ReplayActorState[] actors = [];
    private ReplayPartyMember[] party = [];
    private uint? allowedFate;
    private long observedVersion = -1;
    private DateTime nextScan;
    public bool EncounterActive => encounter.Active;
    public string? MechanicHintsText { get; private set; }
    public string? PredictedDamageText { get; private set; }
    internal DungeonDamagePrediction[] PredictedDamage { get; private set; } = [];
    internal float? PlannedFacing { get; private set; }
    internal ulong PlannedTargetId { get; private set; }
    internal Vector3? PlannedChargeGoal => encounter.ChargeGoal;

    protected CrescentFateScript(uint primaryOid)
        => encounter = new(CrescentFateCatalog.Definitions.Single(d => d.PrimaryOid == primaryOid));

    public override IEnumerable<string> GetMechanisms() => [encounter.Definition.DisplayName, "面向控制"];

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        Scan();
        return encounter.Active && IsMechanismEnabled(encounter.Definition.DisplayName)
            && encounter.Actors.TryGetValue(cast.SourceId, out var source) && CrescentFateEncounter.NativeNpc(source.Actor)
            && encounter.Definition.Rules.Any(r => r.Actions.Contains(cast.ActionId));
    }

    protected override void OnStart()
    {
        // The module consumes native ActorCast changes. Packet cancel/effect is
        // deliberately not a synthetic watched Finish.
        OnLogEvent(e =>
        {
            Scan();
            if (e is NativeActionEffectEvent effect && effect.ActionType is null or 1)
                encounter.Effect(effect.SourceId, effect.ActionId, effect.GlobalSequence);
            Render();
        });
        OnTerritoryChanged(_ => Clear());
        Scan(true);
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
        ReplayActiveFate? fate;
        ulong sourcePlayer;
        if (ReplayWorld is { } world)
        {
            if (!frameworkUpdate && observedVersion == world.Version)
            {
                encounter.Advance(now);
                return;
            }
            actors = world.Actors.Values.ToArray();
            fate = world.ActiveFate;
            party = world.Party.ToArray();
            allowedFate = world.AllowedFateId;
            sourcePlayer = world.SourcePlayerId ?? world.LocalPlayerId;
            observedVersion = world.Version;
        }
        else
        {
            var enmity = ReplayWorldCapture.CapturePlayerEnmity();
            actors = Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now, playerEnmity: enmity)).ToArray();
            fate = ReplayWorldCapture.CaptureActiveFate();
            allowedFate = ReplayWorldCapture.CaptureAllowedFate(CurrentPlayer?.Level, fate);
            party = ReplayWorldCapture.CaptureParty();
            sourcePlayer = ReplayWorldCapture.CaptureSourcePlayerId() ?? ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
        }
        if (frameworkUpdate) nextScan = now.AddSeconds(.1);
        encounter.Synchronize(actors, fate, sourcePlayer, now,
            updateModules: frameworkUpdate || ReplayWorld != null);
    }

    private void Render()
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        var now = CurrentTime;
        var player = CurrentPlayer;
        var enabled = IsMechanismEnabled(encounter.Definition.DisplayName);
        var wanted = new HashSet<string>();
        void Preview(string key, IGeometryShape geometry, bool safe = false, bool outline = false, Vector4? color = null)
        {
            wanted.Add(key);
            Draw.AddAoePreview(key, geometry, safe, outline, .5f, unclipped: true, color: color);
        }
        if (enabled && encounter.Primary is { Destroyed: false, Actor: { Targetable: true, Dead: false } } primary
            && !encounter.Completed)
            Preview("crescent/primary", marker.Resolve(primary.Actor.Position, primary.Actor.Rotation, null),
                color: new(1, 0, 0, 1));
        if (enabled && encounter.Definition.Name == "ADelicateBalance" && !encounter.Completed)
            foreach (var actor in encounter.Actors.Values.Where(a => a.Actor.Oid == 0x4770
                && a.Actor.Targetable && !a.Actor.Dead))
                Preview($"crescent/enemy/{actor.Actor.Id}", marker.Resolve(actor.Actor.Position, actor.Actor.Rotation, null),
                    color: new(1, 0, 0, 1));
        var navigating = enabled && encounter.Active && player is { Dead: false };
        if (navigating)
        {
            var next = (CircleGeometry)encounter.NavigationBoundary(player!);
            if (boundary == null || boundary.Position != next.Position || boundary.Radius != next.Radius)
                boundary = next;
            runtime.Manager.SetArenaBoundary(boundary);
            Preview("crescent/arena", new CircleGeometry(encounter.Center with { Y = player!.Position.Y },
                encounter.Radius), outline: true);
        }
        else ReleaseBoundary();
        var aoes = enabled && encounter.Active
            ? encounter.VisibleAoes() : [];
        var navigation = navigating ? aoes.Where(c => c.Risky).Concat(encounter.ObserverExtras()).ToArray() : [];
        var keys = navigation.Select(c => c.Id).ToHashSet();
        foreach (var key in areas.Keys.Where(k => !keys.Contains(k)).ToArray())
        {
            SafeZone.Remove(areas[key].Handle);
            areas.Remove(key);
        }
        foreach (var cast in aoes)
        {
            Vector4? color = null;
            if (cast.Danger) color = new(1, 1, 0, 1);
            else if (cast.AoeColor) color = new(128f / 255, 128f / 255, 0, 128f / 255);
            Preview($"crescent/aoe/{cast.Id}", cast.Shape!.Resolve(cast.Origin, cast.Rotation, null),
                color: color);
        }
        foreach (var cast in navigation)
        {
            if (areas.TryGetValue(cast.Id, out var previous))
            {
                var old = previous.Cast;
                if (old.Shape == cast.Shape && old.Origin == cast.Origin && old.Rotation == cast.Rotation
                    && old.Activation == cast.Activation && old.StartedAt == cast.StartedAt
                    && runtime.Manager.ContainsArea(previous.Handle)) continue;
                SafeZone.Remove(previous.Handle);
            }
            using var group = SafeZone.BeginDrawingGroup(cast.Rule.Component);
            var binding = new StaticTransform(cast.Origin, cast.Rotation);
            var handle = SafeZone.AddExactDangerSnapshot(cast.Shape!, binding,
                [new(cast.Activation == default ? now : cast.Activation, null,
                    cast.StartedAt == default ? now : cast.StartedAt)]);
            SafeZone.SetNavigationOnly(handle, true);
            areas[cast.Id] = (cast, handle);
        }
        PlannedFacing = null;
        var texts = new List<string>();
        if (encounter.Diagnostic is { } diagnostic) texts.Add(diagnostic);
        PredictedDamage = [];
        if (enabled && encounter.Active && player != null)
        {
            if (aoes.Any(c => c.Risky && DungeonSourceGeometry.Contains(c.Shape!, c.Origin, c.Rotation, player.Position)))
                texts.Add("离开危险范围");
            if (encounter.ChargeGoal != null) texts.Add("Follow the charge!");
            var eyes = encounter.Gazes(player);
            foreach (var gaze in encounter.Casts.Where(c => c.Rule.Kind == CrescentFateRuleKind.Gaze))
            {
                var direction = new Vector2(gaze.Origin.X - player.Position.X, gaze.Origin.Z - player.Position.Z);
                var forward = new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
                var danger = direction.LengthSquared() > 0 && Vector2.Dot(Vector2.Normalize(direction), forward) >= .707107f;
                Preview($"crescent/eye/{gaze.Id}", eye.Resolve(gaze.Origin, 0, null), !danger,
                    color: danger ? new(1, .15f, .15f, 1) : new(.2f, 1, .2f, 1));
                Preview($"crescent/pupil/{gaze.Id}", new CircleTemplate(.28f).Resolve(gaze.Origin, 0, null),
                    !danger, color: Vector4.One);
                if (danger && eyes.Contains(gaze)) texts.Add("背对视线");
            }
            if (eyes.Length > 0) Preview("crescent/gaze/view", arc.Resolve(player.Position, player.Rotation, null));
            if (!player.Dead && IsMechanismEnabled("面向控制")) PlannedFacing = encounter.Facing(player);
            var damage = new List<DungeonDamagePrediction>();
            foreach (var cast in encounter.Casts.Where(c => c.Rule.Kind == CrescentFateRuleKind.Raidwide))
            {
                texts.Add("全队伤害");
                foreach (var member in party.Where(p => p.Slot is >= 0 and < 64))
                    if (actors.FirstOrDefault(a => a.Id == member.EntityId) is { Dead: false } target)
                        damage.Add(new(target.Id, member.Slot, member.Role, target.Name,
                            encounter.Activation(cast.Source), "Raidwide"));
            }
            PredictedDamage = damage.ToArray();
        }
        foreach (var old in drawing.Where(k => !wanted.Contains(k)).ToArray()) Draw.Remove(old);
        drawing.Clear();
        drawing.UnionWith(wanted);
        Hints.SetFacing(PlannedFacing);
        PlannedTargetId = navigating ? QuestTargetPriority.Select(encounter.Definition.Territory, actors,
            player!.TargetId, player.Position, party.Select(p => p.EntityId).ToHashSet(), allowedFate,
            encounter.TargetPriority) : 0;
        Hints.SetTarget(PlannedTargetId);
        runtime.RequestAutomaticMovement(this, navigating && !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0);
        MechanicHintsText = texts.Count == 0 ? null : string.Join("；", texts.Distinct());
        PredictedDamageText = PredictedDamage.Length == 0 ? null : $"预计全队伤害：{PredictedDamage.Length}人";
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
        encounter.Clear();
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
    protected override void OnMovementDisabled()
    {
        Hints.SetFacing(null);
        Hints.SetTarget(0);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current) => Render();
}
