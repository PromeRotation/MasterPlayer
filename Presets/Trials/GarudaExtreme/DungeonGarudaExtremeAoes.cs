using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> GarudaExAoes()
    {
        if (!IsGarudaExtreme || Active == null || Arena == null) yield break;
        if (GarudaExCombat)
        {
            foreach (var source in _actors.Values.Where(a => !a.Dead && a.Targetable
                && (a.Oid == 0xF5 || GarudaExAdds && a.Oid is 0x620 or 0x621)))
            {
                if (_actors.GetValueOrDefault(source.TargetId) is not { } target) continue;
                var shape = new FanTemplate(source.Oid == 0xF5 ? 11.7f : 11.36f, MathF.PI * 2 / 3);
                var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
                var origin = QuantizedDungeonPosition(source.Position);
                yield return new($"garuda-ex/cleave/{source.Id}/display", "下行突风", shape, origin, rotation,
                    Navigate: false, OutlineOnly: true);
                if (target.Id != PlayerId)
                    yield return new($"garuda-ex/cleave/{source.Id}/avoid", "远离下行突风", shape, origin, rotation, Display: false);
                else
                    foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                        yield return new($"garuda-ex/cleave/{source.Id}/party/{member.Id}", "顺劈避开队伍",
                            new FanTemplate(100, MathF.PI * 2 / 3), origin,
                            MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z), Display: false);
            }
        }
        if (GarudaExEyeEnabled && garudaExEye is { } eye)
            yield return new("garuda-ex/eye", "暴风圈", new DonutTemplate(12, 25),
                QuantizedDungeonPosition(eye.Position), Activation: garudaExEyeAt);
        if (garudaExLosSafe != null)
        {
            // The vendored MistralSong override only calls Modify and never AddSafezone.
            // Native output explicitly publishes the intended shelter and cast-timed danger.
            yield return new("garuda-ex/song/shelter", "石柱后的寒风安全区", garudaExLosSafe,
                garudaExLosSafe.Geometry.Position, Navigate: false, Safe: true);
            if (garudaExSongAt is { } at && garudaExLosDanger != null)
                yield return new("garuda-ex/song/avoid", "躲到石柱背后", garudaExLosDanger,
                    garudaExLosDanger.Geometry.Position, Display: false, Activation: at);
        }
        if (garudaExPhase2)
        {
            var shield = _actors.Values.FirstOrDefault(a => a.Oid == 0x1E8F68 && a.EventState != 7);
            if (shield != null)
            {
                yield return new("garuda-ex/shield/display", "刺羽护罩", new CircleTemplate(6), shield.Position, Navigate: false, Safe: true);
                yield return new("garuda-ex/shield/inside", "进入刺羽护罩", new DonutTemplate(6, 100), shield.Position, Display: false);
            }
        }
        if (GarudaExAdds && garudaExWheelAt != default && (garudaExWheelAt - _now).TotalSeconds < 3
            && _actors.GetValueOrDefault(PrimaryId) is { } boss)
            yield return new("garuda-ex/wheel", "邪轮", new CircleTemplate(8.7f), boss.Position,
                Navigate: boss.TargetId != PlayerId, OutlineOnly: true, Activation: garudaExWheelAt);
        if (!GarudaExAi) yield break;
        var monoliths = _actors.Values.Where(a => a.Oid == 0xF3 && !a.Dead).ToArray();
        foreach (var monolith in monoliths)
            yield return new($"garuda-ex/monolith/{monolith.Id}", "远离石柱避免摩擦溅射", new CircleTemplate(5), monolith.Position, Display: false);
        if (monoliths.Length > 0 && _actors.GetValueOrDefault(PlayerId) is { } player && TwinCombatRole(player) is "Healer" or "Ranged")
            yield return new("garuda-ex/ranged-center", "远程保持中央视线", new CircleTemplate(7), Arena.Position,
                Display: false, Safe: true, GoalPriority: 10);
    }

    private IEnumerable<string> GarudaExHints(Vector3? player)
    {
        if (!IsGarudaExtreme || Active == null) yield break;
        if (GarudaExAdds && garudaExWheelAt != default)
            yield return $"邪轮约 {Math.Max((garudaExWheelAt - _now).TotalSeconds, 0):F1} 秒";
        if (player is not { } p) yield break;
        if (garudaExSongAt != null && garudaExLosSafe != null && !garudaExLosSafe.Geometry.Contains(p)
            && Vector2.DistanceSquared(new(p.X, p.Z), new(garudaExLosSafe.Geometry.Position.X, garudaExLosSafe.Geometry.Position.Z)) < 31.7f * 31.7f)
            yield return "躲到石柱背后";
        if (garudaExPhase2 && _actors.Values.FirstOrDefault(a => a.Oid == 0x1E8F68 && a.EventState != 7) is { } shield
            && Vector2.DistanceSquared(new(p.X, p.Z), new(shield.Position.X, shield.Position.Z)) > 36)
            yield return "进入刺羽护罩";
    }
}
