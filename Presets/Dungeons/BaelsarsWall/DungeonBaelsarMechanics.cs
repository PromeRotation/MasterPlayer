using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> baelPrey = [];
    private readonly HashSet<ulong> baelSeenPrey = [];
    private readonly Dictionary<ulong, DateTime> baelCaution = [];
    private ulong baelBootTarget;
    private ulong baelChainTarget;
    private bool baelCollarCasting;
    private bool baelChainsActive;

    public DateTime? AllStopActivation => Active?.BossOid == 0x193A
        && _actors.TryGetValue(PlayerId, out var actor) && !actor.Dead && HasStatus(actor, 1132)
        ? baelCaution.GetValueOrDefault(PlayerId, _now) : null;
    // 对齐上游 PyreticThreshold=1 秒；状态实际移除前持续续期，含取消当前读条和自动攻击。
    public bool RequireAllStop => AllStopActivation is { } activation && activation <= _now.AddSeconds(1)
        || VanguardRequireAllStop || RequireClyteumStop || RequireWolStop || QueenRequireStop || IoRequireAllStop;

    private void BaelStatus(ulong target, uint status, bool present, DateTime timestamp, DateTime? expiresAt)
    {
        if (Active?.BossOid == 0x1938 && status == 562)
        {
            if (present)
            {
                baelSeenPrey.Add(target);
                baelPrey[target] = timestamp.AddSeconds(5);
            }
            else baelSeenPrey.Remove(target);
        }
        if (Active?.BossOid == 0x193A && status == 1132)
        {
            if (present && expiresAt != null) baelCaution[target] = expiresAt.Value;
            if (!present) baelCaution.Remove(target);
        }
    }

    private void SynchronizeBael()
    {
        if (territory != BaelsarCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x1938)
        {
            foreach (var actor in _actors.Values.Where(a => !a.Dead && HasStatus(a, 562)))
                if (baelSeenPrey.Add(actor.Id)) baelPrey[actor.Id] = _now.AddSeconds(5);
            baelSeenPrey.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || !HasStatus(a, 562));
            foreach (var id in baelPrey.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) baelPrey.Remove(id);
        }
        if (Active?.BossOid == 0x193A)
        {
            foreach (var actor in _actors.Values.Where(a => !a.Dead && HasStatus(a, 1132)))
                if (actor.StatusExpirations?.TryGetValue(1132, out var expiry) == true) baelCaution[actor.Id] = expiry;
            foreach (var id in baelCaution.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead || !HasStatus(a, 1132)).ToArray())
                baelCaution.Remove(id);
        }
        if (Active?.BossOid != 0x193D) return;
        if (baelBootTarget != 0 && (!_actors.TryGetValue(baelBootTarget, out var boot) || boot.Dead)) baelBootTarget = 0;
        if (baelChainTarget == 0)
            baelChainTarget = _actors.Values.FirstOrDefault(a => !a.Dead && HasStatus(a, 3421))?.Id ?? 0;
        if (_actors.TryGetValue(baelChainTarget, out var chained) && !chained.Dead)
        {
            if (HasStatus(chained, 3421)) baelChainsActive = true;
            else if (!baelCollarCasting) { baelChainTarget = 0; baelChainsActive = false; }
        }
        else { baelChainTarget = 0; baelChainsActive = false; }
    }

    private void BaelIcon(ulong target, uint icon)
    {
        if (Active?.BossOid != 0x193D || !_actors.TryGetValue(target, out var actor) || actor.Dead) return;
        if (icon == 22) baelBootTarget = target;
        if (icon == 1) { baelChainTarget = target; baelCollarCasting = true; baelChainsActive = false; }
    }

    private void BaelFinish(DungeonPendingCast cast, bool completed)
    {
        if (Active?.BossOid != 0x193D) return;
        if (cast.Rule.Action == 7369) baelCollarCasting = false;
        if (cast.Rule.Action == 7367 && !completed) baelBootTarget = 0;
    }

    private void BaelEffect(uint action, ulong target)
    {
        if (Active?.BossOid == 0x1938 && action == 7348) baelPrey.Remove(target);
        if (Active?.BossOid == 0x193D && action == 7367) baelBootTarget = 0;
    }

    private bool BaelCorrosionVisible
    {
        get
        {
            if (Active?.BossOid != 0x193D) return true;
            var blades = _actors.Values.Where(a => a.Oid == 0x193F).ToArray();
            return blades.Length > 0 && (blades.Length < 9 || blades.Any(a => a.Dead));
        }
    }

    private DungeonActor? BaelCollar
    {
        get
        {
            if (Active?.BossOid != 0x193D) return null;
            var collar = _actors.Values.FirstOrDefault(a => a.Oid == 0x193E);
            return collar is { Targetable: true, Dead: false } ? collar : null;
        }
    }

    private IEnumerable<DungeonAmbientAoe> BaelAoes()
    {
        if (territory != BaelsarCatalog.TERRITORY || Arena == null) yield break;
        foreach (var id in baelPrey.Keys)
            if (_actors.TryGetValue(id, out var target))
                yield return new($"bael/prey/{id}", "导弹点名：50%血量伤害", new CircleTemplate(.7f), target.Position, Navigate: false, OutlineOnly: true);
        if (BaelCollar is { } collar)
        {
            var safe = new CircleGeometry(collar.Position, collar.HitboxRadius + 2.6f);
            yield return new("bael/collar/display", "接近并打破拘束项圈", new CircleTemplate(collar.HitboxRadius + 2.6f), collar.Position,
                Navigate: false, Safe: true, OutlineOnly: true);
            yield return new("bael/collar/avoid", "拘束项圈攻击距离", new SohrKhaiWallHintTemplate(Arena, [safe]), Arena.Position, Display: false);
        }
        if (Active?.BossOid == 0x193D && baelBootTarget == PlayerId && _actors.TryGetValue(PlayerId, out var player)
            && !player.Dead && _actors.TryGetValue(PrimaryId, out var boss))
        {
            var activation = _casts.GetValueOrDefault(PrimaryId)?.Rule.Action == 7367 ? _casts[PrimaryId].FinishAt : _now;
            var dangers = OutputCasts.Where(c => c.Value.Rule.Shape != null && c.Value.FinishAt <= activation)
                .Select(c => c.Value.Rule.Shape!.Resolve(c.Value.Location, c.Value.Rotation, null)).ToArray();
            var template = new CastrumKnockbackTemplate(Arena, boss.Position, 15, dangers);
            yield return new("bael/boot/avoid", "击退落点危险", template, Arena.Position, Display: false);
            foreach (var aoe in DungeonMovementPreview("bael/boot", "点名击退15码（无视抗击退）", player.Position, template.Landing(player.Position)))
                yield return aoe;
        }
    }

    private IEnumerable<string> BaelHints()
    {
        if (baelPrey.Count > 0) yield return "导弹点名：目标将受到50%血量伤害";
        if (AllStopActivation is { } activation && activation <= _now.AddSeconds(3))
            yield return RequireAllStop ? "极度警戒：停手、停读条、停止自动攻击并保持不动" : "极度警戒即将判定：准备停止一切动作";
        if (_actors.TryGetValue(baelChainTarget, out var target))
            yield return baelChainsActive ? $"打破 {target.Name} 的拘束项圈" : $"{target.Name} 即将被拘束";
    }

    private void BaelRemoved(ulong id)
    {
        baelPrey.Remove(id); baelSeenPrey.Remove(id); baelCaution.Remove(id);
        if (baelBootTarget == id) baelBootTarget = 0;
        if (baelChainTarget == id) { baelChainTarget = 0; baelChainsActive = false; baelCollarCasting = false; }
    }

    private void ResetBael()
    {
        baelPrey.Clear(); baelSeenPrey.Clear(); baelCaution.Clear();
        baelBootTarget = 0; baelChainTarget = 0; baelCollarCasting = false; baelChainsActive = false;
    }
}
