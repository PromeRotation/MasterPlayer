using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsPalacePlant => territory is HeavenswardIntroCatalog.SPURGE or HeavenswardIntroCatalog.KIRTIMUKHA;
    internal bool UsesDeepDungeonCastIdentity => IsHeavenOnHigh || IsEurekaOrthos || IsPalacePlant || IsNybeth || IsGodmother || IsGudanna || IsBlackRider || IsYaquaru;
    private uint PalaceHornetOid => territory == HeavenswardIntroCatalog.SPURGE ? 0x1763u : 0x1905u;

    private IEnumerable<DungeonActor> PalacePlantCleaveSources => IsPalacePlant && Active != null
        ? _actors.Values.Where(a => a.Oid == Active.BossOid && !a.Dead && a.Targetable && a.Cast == null
            && _actors.ContainsKey(a.TargetId))
        : [];

    private IEnumerable<DungeonAmbientAoe> PalacePlantAoes()
    {
        if (!IsPalacePlant) yield break;
        if (Active == null && _actors.Values.FirstOrDefault(a => Definitions.Any(d => d.BossOid == a.Oid)) is { } primary
            && _actors.GetValueOrDefault(PlayerId) is { } viewer && Vector3.DistanceSquared(viewer.Position, primary.Position) <= 500 * 500)
        {
            var boundary = Definitions[0].Arena(primary.Position);
            yield return new("palace-plant/precombat/arena", "场地", new LocalPolygonTemplate(
                boundary.GetContours()[0].Select(p => MasterPlayer.Spatial.Geometry.GeometryHelper.WorldToLocal(p, boundary.Position, 0))),
                boundary.Position, Navigate: false, OutlineOnly: true);
            if (!primary.Dead && primary.Targetable)
                yield return HeavenswardIntroEnemyMarker(primary, boundary, "palace-plant/precombat/enemy", new(1, 0, 0, 1));
        }
        foreach (var source in PalacePlantCleaveSources)
        {
            var target = _actors[source.TargetId];
            var origin = QuantizedDungeonPosition(source.Position);
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var prefix = $"palace-plant/cleave/{source.Id}";
            yield return new(prefix + "/display", "Bloody Caress", new FanTemplate(11.6f, 2 * MathF.PI / 3),
                origin, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new(prefix + "/avoid", "Bloody Caress", new FanTemplate(11.6f, 2 * MathF.PI / 3),
                    origin, rotation, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId))
                    yield return new($"{prefix}/party/{member.Id}", "Do not cleave party", new FanTemplate(100, 2 * MathF.PI / 3),
                        origin, MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z), Display: false);
        }
    }

    private IEnumerable<string> PalacePlantHints(Vector3? position)
    {
        if (!IsPalacePlant || Active == null) yield break;
        foreach (var id in ForbiddenDotTargets) yield return $"停止对 {id:X} 施加持续伤害";
        if (position is not { } player) yield break;
        foreach (var source in PalacePlantCleaveSources.Where(a => a.TargetId != PlayerId))
        {
            var target = _actors[source.TargetId];
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            if (new FanTemplate(11.6f, 2 * MathF.PI / 3).Resolve(source.Position, rotation, null)
                .Contains(QuantizedDungeonPosition(player))) yield return "GTFO from cleave!";
        }
    }

    private DateTime PalacePlantCastFinish(DungeonPendingCast cast)
    {
        var observed = _actors.GetValueOrDefault(cast.Source)?.Cast;
        if (observed?.Action != cast.Rule.Action) return cast.FinishAt;
        var total = observed.TotalTime ?? (float)Math.Max(0, (observed.FinishAt - observed.StartedAt).TotalSeconds - .3);
        var minimum = _now.AddSeconds((total + .3f) - total);
        return observed.FinishAt > minimum ? observed.FinishAt : minimum;
    }

    private IEnumerable<DungeonDamagePrediction> PalacePlantDamage()
    {
        if (!IsPalacePlant || Active == null) yield break;
        foreach (var cast in _casts.Values)
        {
            var activation = PalacePlantCastFinish(cast);
            if (cast.Rule.Action is 6424 or 7136)
            {
                foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 8))
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
            }
            if (territory != HeavenswardIntroCatalog.KIRTIMUKHA || cast.Rule.Action != 919) continue;
            var caster = _actors.GetValueOrDefault(cast.Source);
            var targetId = caster?.Cast?.Action == 919 ? caster.Cast.TargetId : cast.TargetId;
            if (targetId == cast.Source) targetId = caster?.TargetId ?? 0;
            var member = CurrentPartyMembers.FirstOrDefault(m => m.EntityId == targetId);
            var target = _actors.GetValueOrDefault(targetId);
            var slot = member?.Slot ?? target?.PartySlot ?? -1;
            if (slot is >= 0 and < 8)
                yield return new(targetId, slot, member?.Role ?? target?.PartyRole ?? "", target?.Name ?? "", activation, "Tankbuster");
        }
    }
}
