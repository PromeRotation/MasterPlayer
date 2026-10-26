using System.Collections;
using System.Numerics;

namespace MasterPlayer.Mechanics.Crucible;

internal enum ActorType { Player, Enemy, EventObj, Other }
internal enum Role { None, Tank, Healer, Melee, Ranged }
internal readonly record struct ActorTetherInfo(uint ID, ulong Target);
internal struct ActorStatus
{
    public uint ID;
    public ushort Extra;
    public DateTime ExpireAt;
    public ulong SourceID;
}
internal sealed class Actor
{
    public ulong InstanceID, TargetID, OwnerID;
    public uint OID, NameID;
    public string Name = "";
    public ActorType Type;
    public Role Role;
    public WPos Position;
    public Angle Rotation;
    public float HitboxRadius;
    public bool IsDead, IsDestroyed, IsTargetable, InCombat;
    public bool IsDeadOrDestroyed => IsDead || IsDestroyed;
    public byte EventState;
    public (byte ModelState, byte AnimState1, byte AnimState2) ModelState;
    public (uint CurHP, uint MaxHP) HPMP;
    public ActorCastInfo? CastInfo;
    public ActorTetherInfo Tether;
    public ActorStatus[] Statuses = [];
    public ActorStatus? FindStatus(uint id) => Statuses.Where(s => s.ID == id).Select(s => (ActorStatus?)s).FirstOrDefault();
    public ActorStatus? FindStatus(uint id, ulong source) => Statuses.Where(s => s.ID == id && s.SourceID == source).Select(s => (ActorStatus?)s).FirstOrDefault();
}
internal readonly record struct ActionID(uint ID)
{
    public static ActionID MakeSpell<T>(T action) where T : Enum => new(Convert.ToUInt32(action));
}
internal sealed class ActorCastInfo
{
    public const float NPCFinishDelay = 0.3f;
    public ActionID Action;
    public WPos LocXZ;
    public Angle Rotation;
    public ulong TargetID;
    public DateTime FinishAt;
    public bool EventHappened;
    public bool Cancelled;
    public bool Interrupted => !EventHappened && (Cancelled || NPCRemainingTime > 0.5f);
    public float RemainingTime, NPCRemainingTime;
}
internal sealed class ActorCastEvent
{
    public ActionID Action;
    public WPos TargetXZ;
    public Angle Rotation;
    public ulong MainTargetID;
    public uint GlobalSequence;
    public readonly record struct Target(ulong ID);
    public Target[] Targets = [];
}
internal sealed class ActorCollection : IEnumerable<Actor>
{
    private readonly Dictionary<ulong, Actor> _actors = [];
    public Actor? Find(ulong id) => _actors.GetValueOrDefault(id);
    public void Add(Actor actor) => _actors[actor.InstanceID] = actor;
    public void Remove(ulong id) => _actors.Remove(id);
    public void Clear() => _actors.Clear();
    public IEnumerator<Actor> GetEnumerator() => _actors.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
internal sealed class WorldState
{
    public float NavigationMargin = 0.5f;
    public DateTime CurrentTime;
    public ActorCollection Actors { get; } = new();
    public Actor? Player;
    public DateTime FutureTime(double seconds) => CurrentTime.AddSeconds(seconds);
}
internal static class PartyRolesConfig { public enum Assignment { Unassigned } }
internal sealed class PartyState(WorldState world) { public Actor? Player() => world.Player; }

internal class TextHints : List<(string Text, bool Risk)>
{
    public void Add(string text, bool risk = true) => Add((text, risk));
}
internal sealed class GlobalHints : List<string>;
internal static class Colors
{
    public const uint Danger = 0xff4040ff, AOE = 0x804080ff, SafeFromAOE = 0xff40ff40, Safe = SafeFromAOE, Object = 0xffffffff, Enemy = Danger;
}

internal sealed class AIHints
{
    public readonly record struct ForbiddenZone(ShapeDistance Shape, DateTime Activation, DateTime? ExpiresAt, bool ExactGeometry = false)
    {
        public ShapeDistance ShapeDistance => Shape;
        public ShapeDistance shapeDistance => Shape;
        public DateTime activation => Activation;
    }
    public sealed class Enemy(Actor actor)
    {
        public const int PriorityForbidden = -1;
        public const int PriorityUndesirable = -2;
        public Actor Actor = actor;
        public int Priority;
        public bool Spikes, ShouldBeDispelled, ForbidDOTs, StayAtLongRange;
    }
    public readonly List<ForbiddenZone> ForbiddenZones = [];
    public readonly List<ShapeDistance> TemporaryObstacles = [];
    public readonly List<Func<WPos, float>> GoalZones = [];
    public readonly List<(WPos Position, float Radius, int Priority)> GoalPoints = [];
    public readonly List<Enemy> PotentialTargets = [];
    public readonly List<(Angle Center, Angle HalfWidth, DateTime Activation)> ForbiddenDirections = [];
    public readonly List<string> ActionHints = [];
    public WPos PathfindMapCenter;
    public ArenaBounds? PathfindMapBounds;
    public Actor? ForcedTarget, InteractWithTarget;
    public ulong ClearTargetID;
    public float MaxCastTime = float.MaxValue;
    public bool ForceCancelCast, ForcedMarchImminent, OwnsTargetSelection;
    public Vector3? ForcedMovement;
    // A timed route the player should walk now (no waits); executed as a direct traversal.
    public List<WPos>? DirectRoute;
    public Angle? DesiredFacing;
    public DateTime DesiredFacingExpire;
    // A hit has a finite window. A positioning deadline remains binding until its component releases it.
    public void AddForbiddenZone(ShapeDistance distance, DateTime activation = default, DateTime? expiresAt = null)
        => ForbiddenZones.Add(new(distance, activation, expiresAt ?? (activation == default || activation == DateTime.MaxValue ? null : activation.AddTicks(Math.Min(TimeSpan.TicksPerSecond * 6 / 10, DateTime.MaxValue.Ticks - activation.Ticks)))));
    public void AddForbiddenZone(AOEShape shape, WPos origin, Angle rotation = default, DateTime activation = default, DateTime? expiresAt = null)
        => AddForbiddenZone(shape.Distance(origin, rotation), activation, expiresAt);
    public void AddForbiddenConstraint(ShapeDistance distance, DateTime activation, DateTime? expiresAt = null)
        => ForbiddenZones.Add(new(distance, activation, expiresAt));
    public void SetPriority(Actor actor, int priority) => (FindEnemy(actor) ?? AddEnemy(actor)).Priority = priority;
    private Enemy AddEnemy(Actor actor) { var enemy = new Enemy(actor); PotentialTargets.Add(enemy); return enemy; }
    public Enemy? FindEnemy(Actor actor) => PotentialTargets.FirstOrDefault(e => e.Actor == actor);
    public static Func<WPos, float> GoalSingleTarget(WPos position, float radius, float weight = 1) => p => p.InCircle(position, radius) ? weight : 0;
    public static Func<WPos, float> GoalSingleTarget(Actor actor, float range) => GoalSingleTarget(actor.Position, actor.HitboxRadius + range);
    public enum SpecialMode { NoMovement }
    public void AddSpecialMode(SpecialMode mode, DateTime activation, DateTime expires = default) => ActionHints.Add("机制要求保持静止");
}

internal sealed class MiniArena
{
    public WPos Center;
    public ArenaBounds Bounds = new ArenaBoundsCircle(40);
    public readonly List<(AOEShape Shape, WPos Origin, Angle Rotation, uint Color)> Shapes = [];
    public readonly List<(WPos Start, WPos End, uint Color)> Lines = [];
    public readonly List<(WPos Position, string Text, uint Color)> Texts = [];
    public bool InBounds(WPos p) => Bounds.Contains(p - Center);
    public void Shape(AOEShape shape, WPos origin, Angle rotation, uint color) => Shapes.Add((shape, origin, rotation, color));
    public void ZoneCircleOutline(WPos p, float radius, uint color, float thickness = 1) => Shape(new AOEShapeCircle(radius), p, default, color);
    public void AddLine(WPos start, WPos end, uint color, float thickness = 1) => Lines.Add((start, end, color));
    public void Actor(Actor actor, uint color) => ZoneCircleOutline(actor.Position, Math.Max(actor.HitboxRadius, 0.5f), color);
    public void TextWorld(WPos position, string text, uint color) => Texts.Add((position, text, color));
    public void ClearDrawings() { Shapes.Clear(); Lines.Clear(); Texts.Clear(); }
}

internal sealed class BossModule
{
    public WorldState WorldState { get; }
    public Actor PrimaryActor;
    public PartyState Raid { get; }
    public MiniArena Arena { get; } = new();
    public WPos Center => Arena.Center;
    public int Group { get; }
    public BossComponent[] Components = [];
    public CrucibleArena? Field { get; }
    public BossModule(WorldState world, Actor primary, int group)
    {
        WorldState = world; PrimaryActor = primary; Group = group; Raid = new(world);
        Field = CrucibleArena.Find(primary.Position, primary.NameID);
        Arena.Center = Field?.Center ?? primary.Position;
        Arena.Bounds = Field?.SafeBounds ?? new ArenaBoundsCircle(40);
        Components = CrucibleCatalog.Create(group, this);
    }
    public DateTime CastFinishAt(ActorCastInfo spell, double offset = 0) => spell.FinishAt.AddSeconds(offset);
    public IEnumerable<Actor> Enemies(uint oid) => WorldState.Actors.Where(a => a.OID == oid);
    public IEnumerable<Actor> Enemies(params uint[] oids) => WorldState.Actors.Where(a => oids.Contains(a.OID));
    public T? FindComponent<T>() where T : BossComponent => Components.OfType<T>().FirstOrDefault();
}

internal abstract class BossComponent(BossModule module)
{
    public BossModule Module { get; } = module;
    protected WorldState WorldState => Module.WorldState;
    protected MiniArena Arena => Module.Arena;
    protected PartyState Raid => Module.Raid;
    public virtual void Update() { }
    public virtual void OnCastStarted(Actor actor, ActorCastInfo spell) { }
    public virtual void OnCastFinished(Actor actor, ActorCastInfo spell) { }
    public virtual void OnEventCast(Actor actor, ActorCastEvent spell) { }
    public virtual void OnActorCreated(Actor actor) { }
    public virtual void OnActorDeath(Actor actor) { }
    public virtual void OnActorDestroyed(Actor actor) { }
    public virtual void OnTethered(Actor source, in ActorTetherInfo tether) { }
    public virtual void OnUntethered(Actor source, in ActorTetherInfo tether) { }
    public virtual void OnEventIcon(Actor actor, uint iconID, ulong targetID) { }
    public virtual void OnStatusGain(Actor actor, ref ActorStatus status) { }
    public virtual void OnStatusLose(Actor actor, ref ActorStatus status) { }
    public virtual void OnMapEffect(byte index, uint state) { }
    public virtual void OnActorEAnim(Actor actor, uint state) { }
    public virtual void OnActorEState(Actor actor, ushort state) { }
    public virtual void OnActorEventStateChange(Actor actor, byte state) { }
    public virtual void OnActorModelStateChange(Actor actor, byte modelState, byte animState1, byte animState2) { }
    public virtual void OnActorPlayActionTimelineEvent(Actor actor, ushort id) { }
    public virtual void OnEventDirectorUpdate(uint updateID, uint param1, uint param2, uint param3, uint param4) { }
    public virtual void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) { }
    public virtual void AddHints(int slot, Actor actor, TextHints hints) { }
    public virtual void AddGlobalHints(GlobalHints hints) { }
    public virtual void DrawArenaBackground(int slot, Actor actor) { }
    public virtual void DrawArenaForeground(int slot, Actor actor) { }
}
