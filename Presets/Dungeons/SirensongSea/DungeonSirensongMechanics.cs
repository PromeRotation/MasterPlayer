using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonAmbientAoe> sirenShadow = [];
    private int sirenShadowCones;
    private readonly Dictionary<ulong, DateTime> sirenStacks = [];
    private readonly Dictionary<ulong, DateTime> sirenTethers = [];
    private DateTime sirenTetherEventAt;
    private ulong sirenPullTarget;
    private DateTime sirenPullAt;
    private int sirenTears;
    private readonly HashSet<(ulong Source, DateTime Started)> sirenTearsSeen = [];
    private bool sirenSmall;
    private DungeonAmbientAoe? sirenRing;
    private DungeonPendingCast? sirenMarch;
    private readonly HashSet<ulong> sirenMarchConsumed = [];
    private readonly Dictionary<ulong, DateTime> sirenForced = [];

    public bool RequireSirenMovementHold => Active?.BossOid == 0x1AFE
        && _actors.TryGetValue(PlayerId, out var player) && !player.Dead && SirenForcedEnd(player) > _now;
    public bool WantsSirenKnockbackImmunity => Active?.BossOid == 0x1AFC && sirenPullTarget == PlayerId
        && _actors.TryGetValue(PlayerId, out var player) && !player.Dead && !SirenImmune(player, sirenPullAt);

    private static Vector3 SirenLoreleiCenter(float y) => new(-44.5f, y, 465);
    private IEnumerable<DungeonActor> SirenMines => _actors.Values.Where(a => a.Oid == 0x1EA300 && a.EventState != 7);

    private bool SirenImmune(DungeonActor actor, DateTime activation)
        => new uint[] { 160, 1209, 2663, 1722, 2345, 3054 }.Any(s => HasStatus(actor, s)
            && actor.StatusExpirations?.TryGetValue(s, out var end) == true && end > activation);

    private DateTime SirenForcedEnd(DungeonActor actor)
    {
        var end = sirenForced.GetValueOrDefault(actor.Id);
        foreach (var status in new uint[] { 5174, 3629 })
            if (HasStatus(actor, status) && actor.StatusExpirations?.TryGetValue(status, out var expires) == true && expires > end) end = expires;
        return end;
    }

    private void SirenCast(DungeonPendingCast cast)
    {
        if (Active?.BossOid != 0x1AFE) return;
        if (cast.Rule.Action == 8041 && !sirenSmall && sirenTearsSeen.Add((cast.Source, cast.StartedAt)) && ++sirenTears > 3)
            sirenRing = new("siren/ring", "缩场预告", new DonutTemplate(15.75f, 22), SirenLoreleiCenter(Arena!.Position.Y),
                Activation: cast.FinishAt.AddSeconds(.4));
        if (cast.Rule.Action is 8037 or 8038 && (sirenMarch?.Source != cast.Source || sirenMarch.StartedAt != cast.StartedAt))
        {
            sirenMarch = cast;
            sirenMarchConsumed.Clear();
        }
    }

    private void SirenFinish(DungeonPendingCast cast, bool completed)
    {
        if (completed) return;
        if (sirenMarch?.StartedAt == cast.StartedAt && sirenMarch.Source == cast.Source) sirenMarch = null;
        if (cast.Rule.Action == 8041) sirenRing = null;
    }

    private void SirenIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (!_actors.TryGetValue(target, out var actor) || actor.Dead) return;
        if (Active?.BossOid == 0x1AFB && icon == 62) sirenStacks[target] = timestamp.AddSeconds(5);
        if (Active?.BossOid == 0x1AFC && icon == 22) { sirenPullTarget = target; sirenPullAt = timestamp.AddSeconds(3); }
    }

    private void SirenTether(ulong source, ulong target, uint tether, bool present, DateTime timestamp)
    {
        if (Active?.BossOid != 0x1AFC || tether != 61) return;
        sirenTetherEventAt = timestamp;
        var player = _actors.GetValueOrDefault(source)?.Oid == 0x1AFC ? target : source;
        if (present) sirenTethers.TryAdd(player, timestamp.AddSeconds(4.3));
        else
        {
            sirenTethers.Remove(player);
            // 上游拉近组件收到任一 61 断线即解除，不依赖读条结束。
            sirenPullTarget = 0;
        }
    }

    private void SirenStatus(ulong target, uint status, bool present, DateTime? expiresAt)
    {
        if (Active?.BossOid != 0x1AFE || status is not (5174 or 3629)) return;
        if (present)
        {
            sirenMarchConsumed.Add(target);
            if (expiresAt != null) sirenForced[target] = expiresAt.Value;
        }
        else sirenForced.Remove(target);
    }

    private void SirenEventState(ulong source, byte state)
    {
        if (Active?.BossOid != 0x1AFE) return;
        // 上游对任意 EState 1/2 响应；不推测仅限某个 EObj。
        if (state == 1 && sirenSmall) { sirenSmall = false; Arena = SirensongArenas.Lorelei(Arena!.Position.Y); }
        if (state == 2 && !sirenSmall) { sirenSmall = true; Arena = SirensongArenas.LoreleiSmall(Arena!.Position.Y); sirenRing = null; }
    }

    private void SirenEffect(ulong source, uint action, ulong target, DateTime timestamp, float? rotation)
    {
        if (Active?.BossOid == 0x1AFB && action == 8023 && !sirenStacks.Remove(target) && sirenStacks.Count > 0)
            sirenStacks.Remove(sirenStacks.Keys.First());
        if (Active?.BossOid != 0x1AFC || action != 8031 || !_actors.TryGetValue(source, out var caster)) return;
        var center = QuantizedDungeonPosition(new(-8, caster.Position.Y, 79));
        var activation = timestamp.AddSeconds(8);
        sirenShadow.Add(new($"siren/shadow/cone/{sirenShadowCones}", "暗影波扇形",
            new FanTemplate(22, 46 * MathF.PI / 180), center, rotation ?? caster.Rotation, Activation: activation));
        if (++sirenShadowCones != 6) return;
        sirenShadow.Add(new("siren/shadow/center", "暗影波中心", new CircleTemplate(6), center, Activation: activation));
        foreach (var add in _actors.Values.Where(a => a.Oid == 0x1AFD))
            sirenShadow.Add(new($"siren/shadow/add/{add.Id}", "暗影波分身", new CircleTemplate(6),
                QuantizedDungeonPosition(add.Position), Activation: activation));
    }

    private void SynchronizeSiren()
    {
        if (territory != SirensongCatalog.TERRITORY) return;
        if (sirenShadow.Count > 0 && sirenShadow[0].Activation < _now) { sirenShadow.Clear(); sirenShadowCones = 0; }
        foreach (var id in sirenStacks.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray()) sirenStacks.Remove(id);
        foreach (var id in sirenTethers.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray()) sirenTethers.Remove(id);
        if (Active?.BossOid == 0x1AFC && _now >= sirenTetherEventAt.AddSeconds(.5))
        {
            var targets = _actors.Values.Where(a => a.Tethers?.ContainsKey(61) == true)
                .Select(a => a.Oid == 0x1AFC ? a.Tethers![61] : a.Id).ToHashSet();
            var removed = sirenTethers.Keys.Where(id => !targets.Contains(id)).ToArray();
            foreach (var id in removed) sirenTethers.Remove(id);
            if (removed.Length > 0) sirenPullTarget = 0;
            foreach (var id in targets) sirenTethers.TryAdd(id, _now.AddSeconds(4.3));
        }
        if (sirenPullTarget != 0 && (!_actors.TryGetValue(sirenPullTarget, out var target) || target.Dead)) sirenPullTarget = 0;
        if (Active?.BossOid != 0x1AFE) return;
        foreach (var id in sirenForced.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead
            || !HasStatus(a, 5174) && !HasStatus(a, 3629)).ToArray()) sirenForced.Remove(id);
        foreach (var actor in _actors.Values.Where(a => !a.Dead))
            if (SirenForcedEnd(actor) > _now) sirenMarchConsumed.Add(actor.Id);
    }

    private SirensongDisplacementTemplate? SirenMarchTemplate(DungeonActor player, float rotation)
    {
        if (Active?.BossOid != 0x1AFE || Arena == null || player.Dead) return null;
        var forcedEnd = SirenForcedEnd(player);
        var pending = sirenMarch != null && !sirenMarchConsumed.Contains(player.Id);
        if (forcedEnd <= _now && !pending) return null;
        var distance = forcedEnd > _now ? (float)(forcedEnd - _now).TotalSeconds * 6.6f : 3 * 6.6f;
        var direction = rotation + (forcedEnd <= _now && sirenMarch?.Rule.Action == 8038 ? MathF.PI : 0);
        return new(Arena, player.Position, new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)), distance,
            SirenMines.Select(a => (IGeometryShape)new CircleGeometry(a.Position, 7)).ToArray());
    }

    private float? SirenFacing(Vector3 position, float rotation)
    {
        if (!_actors.TryGetValue(PlayerId, out var player) || RequireSirenMovementHold || sirenMarch == null
            || sirenMarch.FinishAt.AddSeconds(.7) > _now.AddSeconds(.75) || sirenMarchConsumed.Contains(PlayerId)) return null;
        // 保留当前安全朝向；有危险才从最近角度开始寻找可行方向。
        for (var step = 0; step <= 180; ++step)
            foreach (var sign in step == 0 ? new[] { 1 } : new[] { 1, -1 })
            {
                var candidate = rotation + sign * step * MathF.PI / 180;
                if (SirenMarchTemplate(player, candidate) is { } template && !template.Unsafe(position)) return candidate;
            }
        return null;
    }

    private void SirenRemoved(ulong id)
    {
        sirenStacks.Remove(id); sirenTethers.Remove(id); sirenForced.Remove(id); sirenMarchConsumed.Remove(id);
        if (sirenPullTarget == id) sirenPullTarget = 0;
    }

    private void ResetSiren()
    {
        sirenShadow.Clear(); sirenShadowCones = 0; sirenStacks.Clear(); sirenTethers.Clear(); sirenPullTarget = 0;
        sirenPullAt = default; sirenTetherEventAt = default; sirenTears = 0; sirenTearsSeen.Clear(); sirenSmall = false; sirenRing = null;
        sirenMarch = null; sirenMarchConsumed.Clear(); sirenForced.Clear();
    }
}
