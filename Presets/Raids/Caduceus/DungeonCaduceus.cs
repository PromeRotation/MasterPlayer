using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsCaduceus => territory == CoilNodesCatalog.FIRST && Active?.BossOid == 0x7D7;
    private DungeonActor? caduceusBoss;
    private ulong caduceusClone;
    private DateTime caduceusStarted;
    private DateTime caduceusSplit;
    private DateTime caduceusLastCleave;
    private int caduceusPlatforms;
    private DateTime caduceusExplosion;
    private readonly Dictionary<ulong, byte> caduceusPlatformStates = [];
    private readonly Dictionary<ulong, (byte State, DateTime At)> caduceusPendingPlatforms = [];
    private readonly Dictionary<ulong, Vector3> caduceusPreviousPositions = [];
    private readonly List<(uint Action, Vector3 Position, DateTime At, int Key)> caduceusPuddles = [];
    private int caduceusPuddleKey;
    internal bool RequireCaduceusJump { get; private set; }

    internal void ClearCaduceusMovementHistory()
    {
        caduceusPreviousPositions.Clear();
        RequireCaduceusJump = false;
    }

    private DungeonActor? CaduceusClone => _actors.GetValueOrDefault(caduceusClone) is { Dead: false, Targetable: true } clone ? clone : null;
    private bool CaduceusAlive => _actors.GetValueOrDefault(PrimaryId) is { Dead: false } || CaduceusClone != null;
    private bool CaduceusSplitSoon => caduceusClone == 0 && caduceusBoss is { Hp: { } hp, MaxHp: > 0 } boss && hp < .73f * boss.MaxHp;
    private float CaduceusNextCleave => Math.Max(0, 18 - (float)(_now - caduceusLastCleave).TotalSeconds);

    private void CaduceusObserve()
    {
        RequireCaduceusJump = false;
        if (!IsCaduceus) return;
        if (_actors.GetValueOrDefault(PrimaryId) is { } boss) caduceusBoss = boss;
        if (caduceusClone == 0 && caduceusBoss is { Hp: { } hp, MaxHp: > 0 } primary && hp <= primary.MaxHp / 2)
        {
            var clone = _actors.Values.FirstOrDefault(a => a.Oid == 0x7D7 && a.Id != PrimaryId);
            if (clone != null) { caduceusClone = clone.Id; caduceusSplit = _now; }
        }
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1E8729))
        {
            var state = actor.EventState;
            if (caduceusPendingPlatforms.TryGetValue(actor.Id, out var pending))
            {
                if (pending.State != state && _now < pending.At.AddSeconds(.5)) continue;
                caduceusPendingPlatforms.Remove(actor.Id);
            }
            if (!caduceusPlatformStates.TryGetValue(actor.Id, out var previous) || previous != state)
                CaduceusPlatformState(actor, state);
        }
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1E8B20 or 0x1E88F1 && a.EventState != 7))
        {
            var action = actor.Oid == 0x1E8B20 ? 1210u : 1214u;
            var index = caduceusPuddles.FindIndex(p => p.Action == action
                && Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(actor.Position.X, actor.Position.Z)) <= 4);
            if (index >= 0) caduceusPuddles.RemoveAt(index);
        }
        if (_actors.GetValueOrDefault(PlayerId) is { } player)
        {
            if (caduceusPreviousPositions.TryGetValue(player.Id, out var previous))
                RequireCaduceusJump = CaduceusPlatforms.WantJump(new(player.Position.X, player.Position.Z),
                    new(player.Position.X - previous.X, player.Position.Z - previous.Z));
            caduceusPreviousPositions[player.Id] = player.Position;
        }
    }

    private void CaduceusPlatformEvent(ulong source, byte state)
    {
        if (!IsCaduceus || _actors.GetValueOrDefault(source) is not { Oid: 0x1E8729 } actor) return;
        CaduceusPlatformState(actor, state);
        caduceusPendingPlatforms[source] = (state, _now);
    }

    private void CaduceusPlatformState(DungeonActor actor, byte state)
    {
        var point = new Vector2(actor.Position.X, actor.Position.Z);
        var index = Enumerable.Range(0, 12).FirstOrDefault(i => Vector2.DistanceSquared(point, CaduceusPlatforms.Center(i)) <= 4, 12);
        caduceusPlatformStates[actor.Id] = state;
        if (state == 2) { caduceusPlatforms |= 1 << index; caduceusExplosion = _now.AddSeconds(6); }
        else caduceusPlatforms &= ~(1 << index);
    }

    private void CaduceusEffect(ulong source, uint action, ulong target, Vector3? location)
    {
        if (!IsCaduceus) return;
        if (action == 1208 && source == PrimaryId) caduceusLastCleave = _now;
        if (action is 1210 or 1214 && (_actors.GetValueOrDefault(target)?.Position ?? location) is { } position)
            caduceusPuddles.Add((action, position, _now.AddSeconds(action == 1210 ? 2.1 : .3), ++caduceusPuddleKey));
    }

    private IEnumerable<DungeonAmbientAoe> CaduceusAoes()
    {
        if (!IsCaduceus || Arena == null) yield break;
        var y = _actors.GetValueOrDefault(PlayerId)?.Position.Y ?? Arena.Position.Y;
        yield return new("caduceus/platform/terrain", "平台可通行区域与高边",
            new CaduceusPlatformTemplate(0x1FFF, Inverted: true, ActorHeight: y), Arena.Position, Display: false);
        for (var i = 0; i < 13; ++i)
            yield return new($"caduceus/platform/outline/{i}", (caduceusPlatforms & (1 << i)) != 0 ? "发光平台" : "平台边界",
                new CaduceusPlatformTemplate(1 << i), new(Arena.Position.X, CaduceusPlatforms.Heights[i], Arena.Position.Z),
                Navigate: false, OutlineOnly: true, Color: (caduceusPlatforms & (1 << i)) != 0 ? new Vector4(1, 0, 0, 1) : Vector4.One);
        foreach (var cleave in CoilCleaves(0x7D7, 11, activeWhileCasting: true)) yield return cleave;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 1210 or 1214))
            yield return new($"caduceus/puddle/cast/{cast.Source}", cast.Rule.Name, new CircleTemplate(4),
                _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
        foreach (var p in caduceusPuddles)
            yield return new($"caduceus/puddle/event/{p.Key}", "粘液生成", new CircleTemplate(4), p.Position, Activation: p.At);
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1E8B20 or 0x1E88F1 && a.EventState != 7))
            yield return new($"caduceus/puddle/live/{actor.Id}", "粘液", new CircleTemplate(4), QuantizedDungeonPosition(actor.Position));
        if (_now < caduceusStarted.AddSeconds(10) || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var role = CoilRole(player);
        if (caduceusPlatforms != 0)
        {
            var spawner = caduceusClone == 0 && role == ((caduceusPlatforms & 1) != 0 ? "R2" : "R1");
            yield return new("caduceus/platform/personal", spawner ? "进入发光平台生成软泥怪" : "离开发光平台",
                new CaduceusPlatformTemplate(caduceusPlatforms, Inverted: spawner, Inset: spawner ? 1 : 0),
                Arena.Position, Display: false, Activation: caduceusExplosion);
            yield break;
        }
        var slime = _actors.Values.FirstOrDefault(a => a.Oid == 0x7D8 && a.TargetId == PlayerId);
        if (slime != null && caduceusBoss != null)
        {
            var dest = caduceusBoss.Position;
            var offset = new Vector2(slime.Position.X - dest.X, slime.Position.Z - dest.Z);
            if (slime.Hp > .2f * slime.MaxHp && offset.LengthSquared() > .000001f)
            {
                var delta = Vector2.Normalize(offset) * (caduceusBoss.HitboxRadius + slime.HitboxRadius + 6);
                dest += new Vector3(delta.X, 0, delta.Y);
            }
            yield return new("caduceus/slime/delivery", "引导软泥怪", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(dest, 2)]),
                dest, Display: false, Activation: DateTime.MaxValue);
            yield break;
        }
        if (role == "OT" && CaduceusSplitSoon)
        {
            var center = CaduceusPlatforms.Center(6);
            var dest = new Vector3(center.X, y, center.Y);
            yield return new("caduceus/offtank", "靠近分身站位", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(dest, 20)]),
                dest, Display: false, Activation: DateTime.MaxValue);
        }
        var platform = -1;
        if (role is "H1" or "H2") platform = 5;
        else if (role == "R1" && caduceusClone == 0) platform = 8;
        else if (role == "R2" && caduceusClone == 0 && y > CaduceusPlatforms.Heights[0] - .1f) platform = 0;
        if (platform >= 0)
            yield return new("caduceus/platform/preferred", "分工平台站位", new CaduceusPlatformTemplate(1 << platform, Inverted: true, Inset: 1),
                Arena.Position, Display: false, Activation: DateTime.MaxValue);
    }

    private int CaduceusPriority(DungeonActor actor)
    {
        if (actor.Oid == 0x7D8)
        {
            var predicted = (long)(actor.Hp ?? 0) + (actor.IncomingEffects?.Sum(e => e.HpDelta) ?? 0);
            return predicted > .2f * actor.MaxHp && actor.TargetId == PlayerId ? 3 : -1;
        }
        if (actor.Oid != 0x7D7) return 0;
        if (actor.Id == PrimaryId) return CaduceusSplitSoon && HasStatus(actor, 349) ? -1 : 1;
        var role = CoilRole(_actors.GetValueOrDefault(PlayerId));
        if (role == "MT") return 0;
        if (role == "OT") return 2;
        var difference = CaduceusHpDifference;
        if ((_now - caduceusSplit).TotalSeconds < 3 || difference < -5) return 0;
        if (difference > 5) return 2;
        if (Math.Min(actor.Hp ?? 0, caduceusBoss?.Hp ?? 0) <= .3f * actor.MaxHp) return 1;
        return role is "H2" or "M2" or "R2" ? 2 : 0;
    }

    private float CaduceusHpDifference => CaduceusClone is { Hp: { } cloneHp } && caduceusBoss is { Hp: { } hp, MaxHp: > 0 } boss
        ? ((long)cloneHp - hp) * 100f / boss.MaxHp.Value : 0;

    private IEnumerable<string> CaduceusHints()
    {
        if (!IsCaduceus) yield break;
        yield return $"下次顺劈约 {CaduceusNextCleave:F1} 秒";
        if (CaduceusClone is { } clone && _actors.GetValueOrDefault(PrimaryId) is { Dead: false, Targetable: true } boss)
        {
            var distance = Vector2.Distance(new(clone.Position.X, clone.Position.Z), new(boss.Position.X, boss.Position.Z));
            yield return $"分身血量差 {CaduceusHpDifference:+0.0;-0.0;0.0}%，距离 {distance:F2}，合体检查 {Math.Max(0, 20 - (_now - caduceusSplit).TotalSeconds):F1} 秒";
        }
        if (_actors.GetValueOrDefault(PlayerId) is { } player && CoilCleaves(0x7D7, 11, true)
            .Any(a => a.Navigate && a.Id.Contains("/outline/") && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "离开顺劈范围";
        if (RequireCaduceusJump) yield return "前方平台边缘需要跳跃";
    }

    private void ResetCaduceus()
    {
        caduceusBoss = null; caduceusClone = 0; caduceusStarted = default; caduceusSplit = default; caduceusLastCleave = default;
        caduceusPlatforms = 0; caduceusExplosion = default; caduceusPuddleKey = 0; RequireCaduceusJump = false;
        caduceusPlatformStates.Clear(); caduceusPendingPlatforms.Clear(); caduceusPreviousPositions.Clear(); caduceusPuddles.Clear();
    }
}
