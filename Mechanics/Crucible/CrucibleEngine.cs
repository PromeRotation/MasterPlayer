using MasterPlayer.Mechanics.Crucible.Components;

namespace MasterPlayer.Mechanics.Crucible;

internal sealed class CrucibleEngine(WorldState world, int firstGroup, int lastGroup)
{
    private readonly Dictionary<(uint Sequence, ulong Source, uint Action), DateTime> _effects = [];
    public WorldState World { get; } = world;
    public BossModule? Module { get; private set; }
    public int Generation { get; private set; }
    public Func<string, bool> Enabled { get; set; } = _ => true;
    private readonly HashSet<BossComponent> _disabled = [];

    public void Reset()
    {
        Module = null; _effects.Clear(); _disabled.Clear(); ++Generation;
    }
    public void SelectGroup()
    {
        var player = World.Player;
        if (player == null || player.IsDeadOrDestroyed) { if (Module != null) Reset(); return; }
        bool Member(Actor a) => !a.IsDeadOrDestroyed && a.Type == ActorType.Enemy && a.OID != 9020 && a.HPMP.CurHP > 0
            && CrucibleCatalog.GroupFor(a.NameID) >= firstGroup && CrucibleCatalog.GroupFor(a.NameID) <= lastGroup
            && a.Position.InCircle(player.Position, 80);
        var members = World.Actors.Where(Member).ToArray();
        var primary = members.FirstOrDefault(a => CrucibleCatalog.GroupFor(a.NameID) == Module?.Group)
            ?? members.OrderByDescending(a => a.InCombat).ThenBy(a => (a.Position - player.Position).LengthSq()).FirstOrDefault();
        if (primary == null) { if (Module != null) Reset(); return; }
        var group = CrucibleCatalog.GroupFor(primary.NameID);
        if (Module?.Group == group) { Module.PrimaryActor = primary; return; }
        Reset();
        Module = new(World, primary, group);
        foreach (var actor in World.Actors)
        {
            Dispatch(c => c.OnActorCreated(actor));
            foreach (var item in actor.Statuses)
            {
                var status = item;
                foreach (var c in Module.Components) c.OnStatusGain(actor, ref status);
            }
            if (actor.CastInfo is { EventHappened: false } cast && cast.FinishAt > World.CurrentTime) Dispatch(c => c.OnCastStarted(actor, cast));
        }
    }
    public void Dispatch(Action<BossComponent> action)
    {
        if (Module == null) return;
        foreach (var c in Module.Components) if (!_disabled.Contains(c)) action(c);
    }
    public void Cast(Actor actor, ActorCastInfo cast)
    {
        if (cast.EventHappened || cast.FinishAt <= World.CurrentTime) return;
        if (actor.CastInfo is { } previous && previous.Action == cast.Action
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < 0.75
            && previous.TargetID == cast.TargetID
            && Math.Abs((previous.Rotation - cast.Rotation).Normalized().Rad) < 0.0001f
            && previous.LocXZ.AlmostEqual(cast.LocXZ, 0.01f)) return;
        var generation = Generation;
        actor.CastInfo = cast;
        SelectGroup();
        if (generation == Generation) Dispatch(c => c.OnCastStarted(actor, cast));
    }
    public void FinishCast(Actor actor, uint action = 0, bool cancelled = false)
    {
        if (actor.CastInfo is not { } cast || action != 0 && cast.Action.ID != action) return;
        cast.NPCRemainingTime = (float)(cast.FinishAt - World.CurrentTime).TotalSeconds;
        cast.RemainingTime = cast.NPCRemainingTime - ActorCastInfo.NPCFinishDelay;
        cast.Cancelled = cancelled;
        Dispatch(c => c.OnCastFinished(actor, cast));
        actor.CastInfo = null;
    }
    public void Effect(Actor actor, ActorCastEvent spell)
    {
        var key = (spell.GlobalSequence, actor.InstanceID, spell.Action.ID);
        if (spell.GlobalSequence != 0 && _effects.TryGetValue(key, out var expiry) && expiry > World.CurrentTime) return;
        if (spell.GlobalSequence != 0) _effects[key] = World.FutureTime(10);
        if (actor.CastInfo is { } cast && cast.Action.ID == spell.Action.ID) cast.EventHappened = true;
        Dispatch(c => c.OnEventCast(actor, spell));
    }
    public void Destroy(Actor actor, bool removed)
    {
        if (removed) actor.IsDestroyed = true; else actor.IsDead = true;
        Dispatch(c => { c.OnActorDeath(actor); c.OnActorDestroyed(actor); });
        actor.CastInfo = null; actor.Tether = default;
        if (removed) World.Actors.Remove(actor.InstanceID);
    }
    public void Update()
    {
        SelectGroup();
        if (Module == null) return;
        for (var i = 0; i < Module.Components.Length; ++i)
        {
            var c = Module.Components[i];
            if (!Enabled(c.GetType().Name)) { _disabled.Add(c); continue; }
            if (_disabled.Remove(c))
            {
                // Re-enabling starts clean; an old event chain must never resume from a stale step.
                c = (BossComponent)Activator.CreateInstance(c.GetType(), Module)!;
                Module.Components[i] = c;
                foreach (var actor in World.Actors)
                {
                    c.OnActorCreated(actor);
                    if (actor.CastInfo is { EventHappened: false } cast && cast.FinishAt > World.CurrentTime) c.OnCastStarted(actor, cast);
                }
            }
            c.Update();
        }
        foreach (var key in _effects.Where(e => e.Value <= World.CurrentTime).Select(e => e.Key).ToArray()) _effects.Remove(key);
    }
    public CrucibleFrame Frame()
    {
        var result = new CrucibleFrame();
        if (Module == null || World.Player is not { } player) return result;
        Module.Arena.ClearDrawings();
        result.Hints.PathfindMapCenter = Module.Center;
        result.Hints.PathfindMapBounds = Module.FindComponent<CrucibleMiasma>()?.NavigationBounds ?? Module.Arena.Bounds;
        result.Hints.PotentialTargets.AddRange(World.Actors.Where(a => a.Type == ActorType.Enemy && a != player && a.OwnerID != player.InstanceID && !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0).Select(a => new AIHints.Enemy(a)));
        foreach (var c in Module.Components)
        {
            if (_disabled.Contains(c)) continue;
            var index = result.Hints.TemporaryObstacles.Count;
            c.AddAIHints(0, player, PartyRolesConfig.Assignment.Unassigned, result.Hints);
            // The original navigator used obstacles for pools too. PR must actively escape those.
            if (c is not (CrucibleYmirRocks or CrucibleRockWalls or CrucibleMiasma or CrucibleGatlerTraps))
            {
                foreach (var shape in result.Hints.TemporaryObstacles.Skip(index)) result.Hints.AddForbiddenZone(shape);
                result.Hints.TemporaryObstacles.RemoveRange(index, result.Hints.TemporaryObstacles.Count - index);
            }
            if (c is GenericAOEs aoes)
                foreach (var a in aoes.ActiveAOEs(0, player)) result.Aoes.Add((c.GetType().Name, a));
            c.AddHints(0, player, result.Text);
            c.AddGlobalHints(result.Global);
            c.DrawArenaBackground(0, player);
            c.DrawArenaForeground(0, player);
        }
        return result;
    }
}

internal sealed class CrucibleFrame
{
    public AIHints Hints { get; } = new();
    public TextHints Text { get; } = [];
    public GlobalHints Global { get; } = [];
    public List<(string Component, GenericAOEs.AOEInstance Aoe)> Aoes { get; } = [];
}
