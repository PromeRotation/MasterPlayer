namespace BossMod.Components;

// generic component dealing with 'forced march' mechanics
// these mechanics typically feature 'march left/right/forward/backward' debuffs, which rotate player and apply 'forced march' on expiration
// if there are several active march debuffs, we assume they are chained together

[SkipLocalsInit]
public class GenericForcedMarch(BossModule module, float activationLimit = float.MaxValue, bool stopAfterWall = false, bool stopAtWall = false) : BossComponent(module)
{
    public sealed class PlayerState
    {
        public List<(Angle dir, float duration, DateTime activation)> PendingMoves = [];
        public DateTime ForcedEnd; // zero if forced march not active

        public bool Active(BossModule module) => ForcedEnd > module.WorldState.CurrentTime || PendingMoves.Count > 0;
    }

    public readonly bool StopAfterWall = stopAfterWall;
    public readonly bool StopAtWall = stopAtWall;
    public bool OverrideDirection;
    public int NumActiveForcedMarches;
    public readonly Dictionary<ulong, PlayerState> State = []; // key = instance ID
    public float MovementSpeed = 6.6f; // 强制移动速度（2026-08-17 伊阿姆柏两场回放均值 ~6.6y/s，原 6f→6.4f；其他战斗若无实测可 override）
    // 2026-08-17 复核：强制移动位移 = ToDirection(朝向)（标准 BossMod 前方，无镜像）——两场精确验证
    // （本场 atan2(+10.459,+5.437)=62.5°=朝向 62.533°；23:18 场 atan2(+4.99,−11.93)=157.3°=朝向 157.307°）。
    // 此前"位移=180°−朝向"镜像结论为坐标系换算错误（把 MOVE 行 BossMod 系 rotation 误当游戏网络系套公式），已回滚。
    public readonly float ActivationLimit = activationLimit; // do not show pending moves that activate later than this limit
    private const float approxHitBoxRadius = 0.499f; // calculated because due to floating point errors this does not result in 0.001
    private const float maxIntersectionError = 0.5f - approxHitBoxRadius; // calculated because due to floating point errors this does not result in 0.001

    // called to determine whether we need to show hint
    public virtual bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !Arena.InBounds(pos);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var movements = ForcedMovements(actor);
        var count = movements.Count;
        if (count == 0)
        {
            return;
        }

        var last = movements[count - 1];
        if (last.from != last.to && DestinationUnsafe(slot, actor, last.to))
        {
            hints.Add("Aim for safe spot!");
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var movements = ForcedMovements(pc);
        var count = movements.Count;
        for (var i = 0; i < count; ++i)
        {
            var m = movements[i];
            Arena.ActorProjected(m.from, m.to, m.dir, Colors.Danger);
            Arena.AddLine(m.from, m.to);
        }
    }

    public void AddForcedMovement(Actor player, Angle direction, float duration, DateTime activation)
    {
        var moves = State.GetOrAdd(player.InstanceID).PendingMoves;
        moves.Add((direction, duration, activation));
        moves.Sort(static (a, b) => a.activation.CompareTo(b.activation));
    }

    public bool HasForcedMovements(Actor player) => State.GetValueOrDefault(player.InstanceID)?.Active(Module) ?? false;

    public void ActivateForcedMovement(Actor player, DateTime expiration)
    {
        State.GetOrAdd(player.InstanceID).ForcedEnd = expiration;
        ++NumActiveForcedMarches;
    }

    public void DeactivateForcedMovement(Actor player)
    {
        State.GetOrAdd(player.InstanceID).ForcedEnd = default;
        --NumActiveForcedMarches;
    }

    public List<(WPos from, WPos to, Angle dir)> ForcedMovements(Actor player)
    {
        var state = State.GetValueOrDefault(player.InstanceID);
        if (state == null)
        {
            return [];
        }

        var from = player.Position;
        var dir = !OverrideDirection ? player.Rotation : default;
        var movements = new List<(WPos, WPos, Angle)>();

        if (state.ForcedEnd > WorldState.CurrentTime)
        {
            // note: as soon as player starts marching, he turns to desired direction
            // TODO: would be nice to use non-interpolated rotation here...
            dir = player.Rotation;
            var movementDistance = MovementSpeed * (float)(state.ForcedEnd - WorldState.CurrentTime).TotalSeconds;
            var wdir = dir.ToDirection(); // 位移方向 = 标准前方（2026-08-17 复核无镜像）

            if (StopAfterWall)
            {
                movementDistance = Math.Min(movementDistance, Arena.IntersectRayBounds(from, wdir) + maxIntersectionError);
            }
            else if (StopAtWall)
            {
                movementDistance = Math.Min(movementDistance, Arena.IntersectRayBounds(from, wdir) - maxIntersectionError);
            }

            var to = from + movementDistance * wdir;
            movements.Add((from, to, dir)); // 箭头朝向 = 实际位移方向（标准前方）
            from = to;
        }

        var limit = ActivationLimit < float.MaxValue ? WorldState.FutureTime(ActivationLimit) : DateTime.MaxValue;
        var count = state.PendingMoves.Count;

        for (var i = 0; i < count; ++i)
        {
            var move = state.PendingMoves[i];
            if (move.activation > limit)
            {
                break;
            }

            dir += move.dir;
            var movementDistance = MovementSpeed * move.duration;
            var wdir = dir.ToDirection(); // 位移方向 = 标准前方（2026-08-17 复核无镜像）

            if (StopAfterWall)
            {
                movementDistance = Math.Min(movementDistance, Arena.IntersectRayBounds(from, wdir) + maxIntersectionError);
            }
            else if (StopAtWall)
            {
                movementDistance = Math.Min(movementDistance, Arena.IntersectRayBounds(from, wdir) - maxIntersectionError);
            }

            var to = from + movementDistance * wdir;
            movements.Add((from, to, dir)); // 箭头朝向 = 实际位移方向（标准前方）
            from = to;
        }
        return movements;
    }
}

// typical forced march is driven by statuses
[SkipLocalsInit]
public class StatusDrivenForcedMarch(BossModule module, float duration, uint statusForward, uint statusBackward, uint statusLeft, uint statusRight, uint statusForced = 1257u, uint statusForcedNPCs = 3629u, float activationLimit = float.MaxValue, bool stopAfterWall = false, bool stopAtWall = false) : GenericForcedMarch(module, activationLimit, stopAfterWall, stopAtWall)
{
    public float Duration = duration;
    public readonly uint[] Statuses = [statusForward, statusLeft, statusBackward, statusRight, statusForced, statusForcedNPCs]; // 5 elements: fwd, left, back, right, forced, forcedNPCs

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        var statusKind = Array.IndexOf(Statuses, status.ID);
        if (statusKind >= 4)
        {
            ActivateForcedMovement(actor, status.ExpireAt);
        }
        else if (statusKind >= 0)
        {
            AddForcedMovement(actor, statusKind * 90f.Degrees(), Duration, status.ExpireAt);
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        var statusKind = Array.IndexOf(Statuses, status.ID);
        if (statusKind >= 4)
        {
            DeactivateForcedMovement(actor);
        }
        else if (statusKind >= 0)
        {
            var dir = statusKind * 90f.Degrees();
            var pendingMoves = State.GetOrAdd(actor.InstanceID).PendingMoves;
            var count = pendingMoves.Count;
            for (var i = 0; i < count; ++i)
            {
                if (pendingMoves[i].dir == dir)
                {
                    pendingMoves.RemoveAt(i);
                    break;
                }
            }
        }
    }
}

// action driven forced march
[SkipLocalsInit]
public class ActionDrivenForcedMarch(BossModule module, uint aid, float duration, Angle rotation, float actioneffectdelay, uint statusForced = 5174u, uint statusForcedNPCs = 3629u, float activationLimit = float.MaxValue) : GenericForcedMarch(module, activationLimit)
{
    public readonly float Duration = duration;
    public readonly float Actioneffectdelay = actioneffectdelay;
    public readonly Angle Rotation = rotation;
    public readonly uint StatusForced = statusForced;
    public readonly uint StatusForcedNPCs = statusForcedNPCs;
    public readonly uint Aid = aid;

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID == StatusForced || status.ID == StatusForcedNPCs)
        {
            var pendingMoves = State.GetOrAdd(actor.InstanceID).PendingMoves;
            var count = pendingMoves.Count;
            for (var i = 0; i < count; ++i)
            {
                if (pendingMoves[i].dir == Rotation)
                {
                    pendingMoves.RemoveAt(i);
                    break;
                }
            }
            ActivateForcedMovement(actor, status.ExpireAt);
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == StatusForced || status.ID == StatusForcedNPCs)
        {
            DeactivateForcedMovement(actor);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Aid)
        {
            var party = Module.Raid.WithoutSlot();
            var len = party.Length;
            for (var i = 0; i < len; ++i)
            {
                AddForcedMovement(party[i], Rotation, Duration, Module.CastFinishAt(spell, Actioneffectdelay));
            }
        }
    }
}
