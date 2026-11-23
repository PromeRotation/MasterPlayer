using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> GhimlytAoes()
    {
        if (territory != GhimlytCatalog.TERRITORY || Arena == null) yield break;
        if (ghimlytShield is { } shield)
        {
            var shelter = new CircleGeometry(shield, 4.5f);
            yield return new("ghimlyt/shield/display", "进入嘉恩·艾·神纳的护盾", new CircleTemplate(4.5f), shield, Navigate: false, Safe: true);
            yield return new("ghimlyt/shield/enter", "护盾内承受攻击", new SohrKhaiWallHintTemplate(Arena, [shelter]), shield, Display: false);
        }
        if (ghimlytSlashRotation is { } rotation && ghimlytSlashIncrement is { } increment && ghimlytSlashRemaining > 0)
            for (var i = 0; i < Math.Min(2, ghimlytSlashRemaining); ++i)
                yield return new($"ghimlyt/slash/{i}", "旋转魔导斩", new FanTemplate(23.5f, MathF.PI / 3),
                    QuantizedDungeonPosition(new(-180.569f, Arena.Position.Y, 68.523f)), rotation + i * increment,
                    OutlineOnly: i != 0, Activation: ghimlytSlashActivation.AddSeconds(i * 1.1));
        foreach (var (id, heading) in ghimlytFire)
            yield return new($"ghimlyt/fire/{id}", "魔导斩持续火扇形", new FanTemplate(23.5f, MathF.PI / 3),
                new(-180.569f, Arena.Position.Y, 68.523f), heading);
        if (ghimlytHeat != null) yield return ghimlytHeat;
        if (ghimlytNitro != null) yield return ghimlytNitro;
        foreach (var (_, aoe) in ghimlytCrosshatches) yield return aoe;
        // The source hides only the ninth preview until the first actual hit.
        foreach (var aoe in ghimlytTanks.Take(ghimlytTanks.Count == 9 ? 8 : ghimlytTanks.Count)) yield return aoe;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 14191))
            if (cast.TargetId != PlayerId && _actors.TryGetValue(cast.TargetId, out var target))
                yield return new($"ghimlyt/stack/{cast.Source}", "四人分摊", new SohrKhaiWallHintTemplate(Arena,
                    [new CircleGeometry(target.Position, 6)]), Arena.Position, Display: false, Activation: cast.FinishAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 15488))
        {
            if (!_actors.TryGetValue(cast.Source, out var caster) || !_actors.TryGetValue(cast.TargetId, out var target)) continue;
            var delta = target.Position - caster.Position; delta.Y = 0;
            var angle = MathF.Atan2(delta.X, delta.Z);
            var length = delta.Length();
            yield return new($"ghimlyt/charge/{cast.Source}/display", "冲锋诱导", RayRectangleTemplate.Symmetric(4, Math.Max(.01f, length)),
                caster.Position, angle, Navigate: false, OutlineOnly: target.Id == PlayerId, Activation: cast.FinishAt);
            if (target.Id != PlayerId)
                yield return new($"ghimlyt/charge/{cast.Source}/avoid", "避开冲锋路径", RayRectangleTemplate.Symmetric(4, Math.Max(.01f, length)),
                    caster.Position, angle, Display: false, Activation: cast.FinishAt);
            else
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                {
                    var distance = Vector3.Distance(other.Position, caster.Position);
                    if (distance > length + 2 || distance <= .001f) continue;
                    var halfAngle = MathF.Asin(Math.Min(1, 2 / distance));
                    yield return new($"ghimlyt/charge/{cast.Source}/party/{other.Id}", "冲锋不要覆盖队友",
                        new FanTemplate(length, halfAngle * 2), caster.Position,
                        MathF.Atan2(other.Position.X - caster.Position.X, other.Position.Z - caster.Position.Z), Display: false, Activation: cast.FinishAt);
                }
            if (!_actors.TryGetValue(PlayerId, out var player)
                || player.StatusExpirations?.Any(s => s.Key is 160 or 1209 && s.Value > cast.FinishAt) == true) continue;
            var dangers = _casts.Values.Where(c => c.Rule.Action == 14097)
                .Select(c => c.Rule.Shape!.Resolve(c.Location, c.Rotation, null)).ToArray();
            var kb = new CastrumKnockbackTemplate(Arena, caster.Position, 15, dangers);
            yield return new($"ghimlyt/knockback/{cast.Source}/avoid", "击退落点避开轰炸", kb, caster.Position,
                Display: false, Activation: cast.FinishAt);
            var landing = kb.Landing(player.Position);
            var safe = !dangers.Any(d => d.Contains(landing));
            yield return new($"ghimlyt/knockback/{cast.Source}/landing", "击退落点", new CircleTemplate(.4f), landing,
                Navigate: false, Safe: safe, OutlineOnly: true);
            foreach (var line in AlaMhigoLine($"ghimlyt/knockback/{cast.Source}/line", player.Position, landing, safe)) yield return line;
        }
    }
}
