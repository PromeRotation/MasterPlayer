using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, (DungeonLosTemplate Safe, DungeonLosTemplate Danger)> losSnapshots = [];
    private ulong plagueTarget;
    private DateTime plagueActivation;
    private DateTime plagueEventTime;

    private IEnumerable<DungeonActor> ActivePillars => _actors.Values.Where(a => a.Oid is >= 0x1E86A7 and <= 0x1E86AA && !a.Dead && a.EventState == 0);

    private void AmdaporCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action != 1074) return;
        var pillars = ActivePillars.ToArray();
        losSnapshots[cast.Source] = (new(new(cast.Location, pillars, true)), new(new(cast.Location, pillars, false)));
    }

    private void SynchronizeAmdapor()
    {
        if (Active?.BossOid != 0x5CC || _now < plagueEventTime.AddSeconds(.5)) return;
        var target = _actors.GetValueOrDefault(PrimaryId)?.Tethers?.GetValueOrDefault(1u) ?? 0;
        if (target == plagueTarget) return;
        plagueTarget = target;
        plagueActivation = _now.AddSeconds(6.1);
    }

    internal void Tether(ulong target, uint tether, bool present, DateTime now, ulong source = 0)
    {
        EurekaTether(source, tether, target, present);
        GuildivainTether(source, target, tether, present);
        SleepTether(source, target, tether, present, now);
        AlphaTether(source, target, tether, present);
        TacticalTetherChanged(source, target, tether, present, now);
        TitanExTether(source, target, tether, present, now);
        ShinryuTether(source, target, tether, present, now);
        YojimboTether(source, target, tether, present);
        SeiryuTether(source, target, tether, present, now);
        HadesTether(source, target, tether, present, now);
        WolTether(source, target, tether, present, now);
        EnuoTether(source, target, tether, present);
        NecronTether(source, present);
        QueenTether(source, present);
        ZoraalTether(source, tether, target, present);
        HardTrialTether(source, tether, present, now);
        LapisTether(source, target, tether, present, now);
        TenderTether(source, tether, present, now);
        StrayTether(source, target, tether, present, now);
        UnderTether(source, target, tether, present, now);
        MesoTether(source, target, tether, present);
        IhuyTether(source, target, tether, present, now);
        WorqorTether(source, target, tether, present, now);
        BabilTether(source, target, tether, present, now);
        AeryTether(target, tether, present, now, source);
        VaultTether(source, target, tether, present);
        GubalTether(source, target, tether, present, now);
        ResearchTether(source, target, tether, present, now);
        AntitowerTether(source, target, tether, present, now);
        SirenTether(source, target, tether, present, now);
        BardamTether(source, target, tether, present, now);
        DomaTether(source, tether, present, now);
        AlaMhigoTether(source, target, tether, present, now);
        SkallaTether(source, target, tether, present, now);
        DohnTether(source, target, tether, present);
        QitanaTether(source, target, tether, present, now);
        CosmosTether(source, target, tether, present, now);
        MatoyaTether(source, target, tether, present, now);
        AlzadaalTether(target, tether, present, now);
        if (Active?.BossOid != 0x5CC || tether != 1) return;
        plagueEventTime = now;
        if (present)
        {
            plagueTarget = target;
            plagueActivation = now.AddSeconds(6.1);
        }
        else if (target == 0 || target == plagueTarget) plagueTarget = 0;
    }

    internal void EventState(ulong source, ushort rawState)
    {
        TreasureMolterEState(source, rawState);
        // Cenote EState is an event, not the native EventState field used to finish rooms.
        if (territory == NativeTreasureCatalog.CENOTE_JA_JA_GURAL) return;
        var state = (byte)rawState;
        // This entry receives network EventObjectStateChange; Guildivain consumes native EventState frame changes.
        if (IsGuildivain) return;
        if (_actors.TryGetValue(source, out var actor)) _actors[source] = actor with { EventState = state };
        SirenEventState(source, state);
        GhimlytState(source, state);
        AlzadaalEventState(source, state);
        CaduceusPlatformEvent(source, state);
    }

    private IEnumerable<DungeonAmbientAoe> AmdaporAoes()
    {
        if (Active?.BossOid == 0x5D1)
        {
            foreach (var caster in _actors.Values.Where(a => a.Oid == 0x5D1 && !a.Dead && a.Targetable))
            {
                if (!_actors.TryGetValue(caster.TargetId, out var target) || target.Dead) continue;
                yield return new($"voidfire/display/{caster.Id}", "VoidFireCleave", new CircleTemplate(5), target.Position,
                    Navigate: false, OutlineOnly: true);
                if (PlayerId != target.Id)
                    yield return new($"voidfire/avoid/{caster.Id}", "VoidFireCleave", new CircleTemplate(5), target.Position, Display: false);
            }
        }
        if (Active?.BossOid == 0x283 && _actors.TryGetValue(PrimaryId, out var wall))
        {
            yield return new("wall/behind", "首领背后禁区", RayRectangleTemplate.Symmetric(20, 50), wall.Position, MathF.PI);
            if (_actors.Values.Any(a => a.Oid == 0x1E86B1 && !a.Dead && a.EventState == 0))
                yield return new("wall/pollen", "花粉禁区", RayRectangleTemplate.Symmetric(20, 50), new(200, wall.Position.Y, -122));
            if (_casts.Values.Any(c => c.Rule.Action == 1047))
            {
                yield return new("wall/repel-goal", "不可免疫击退：中央窄带", RayRectangleTemplate.Symmetric(2, 48),
                    wall.Position + Vector3.UnitZ * 2, Safe: true);
                if (_actors.TryGetValue(PlayerId, out var player))
                {
                    var offset = new Vector2(player.Position.X - wall.Position.X, player.Position.Z - wall.Position.Z);
                    if (offset.LengthSquared() > .0001f)
                    {
                        var direction = Vector2.Normalize(offset);
                        var landing = player.Position + new Vector3(direction.X, 0, direction.Y) * 20;
                        var safe = Arena?.Contains(landing) == true;
                        yield return new("wall/repel-landing", "击退落点", new CircleTemplate(.5f), landing,
                            Navigate: false, Safe: safe, OutlineOnly: true);
                        yield return new("wall/repel-path", "击退轨迹", new RectangleTemplate(.12f, 20),
                            (player.Position + landing) * .5f, MathF.Atan2(direction.X, direction.Y), Navigate: false, Safe: safe);
                    }
                }
            }
        }
        if (Active?.BossOid != 0x5CC) yield break;
        foreach (var (source, snapshot) in losSnapshots)
        {
            if (!_casts.TryGetValue(source, out var cast)) continue;
            yield return new($"los/display/{source}", "石柱后的遮挡安全区", snapshot.Safe, cast.Location, Navigate: false, Safe: true);
            yield return new($"los/danger/{source}", "躲到石柱后", snapshot.Danger, cast.Location, Display: false, Activation: cast.FinishAt);
        }
        foreach (var pillar in ActivePillars)
            yield return new($"pillar/{pillar.Id}", "仍存在的石柱", new CircleTemplate(pillar.HitboxRadius),
                pillar.Position, Navigate: false, OutlineOnly: true, Safe: true);
        foreach (var nova in _actors.Values.Where(a => a.Oid == 0x5CE && !a.Dead))
            yield return new($"nova/{nova.Id}", "BubonicCloud", new CircleTemplate(11.5f), nova.Position);
        if (plagueTarget == 0 || !_actors.TryGetValue(plagueTarget, out var marked) || marked.Dead) yield break;
        yield return new("plague/display", "瘟疫连线诱导", new CircleTemplate(11.5f), marked.Position, Navigate: false, OutlineOnly: true);
        if (PlayerId != plagueTarget)
            yield return new("plague/avoid", "远离瘟疫诱导目标", new CircleTemplate(11.5f), marked.Position, Display: false, Activation: plagueActivation);
        else
            foreach (var pillar in ActivePillars)
                yield return new($"plague/pillar/{pillar.Id}", "将瘟疫诱导到石柱以外", new CircleTemplate(11.5f), pillar.Position,
                    Display: false, Activation: plagueActivation);
    }
}
