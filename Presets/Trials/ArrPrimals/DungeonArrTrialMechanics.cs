using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DateTime arrNailSpawn;
    private DateTime arrHeartSpawn;
    private bool arrEyeActive;
    private readonly List<(DungeonPendingCast Cast, DungeonLosTemplate Safe, DungeonLosTemplate Danger)> arrTrialLos = [];
    private bool IsArrTrial => ArrTrialCatalog.IsTerritory(territory);

    private void ArrTrialObserve()
    {
        if (!IsArrTrial || Active == null) return;
        if (territory == ArrTrialCatalog.IFRIT && arrNailSpawn == default
            && _actors.Values.FirstOrDefault(a => a.Oid == 0xD0) is { Targetable: true })
            arrNailSpawn = _now;
        if (territory == ArrTrialCatalog.TITAN && arrHeartSpawn == default
            && _actors.Values.Any(a => a.Oid == 0x5E3 && a.Targetable && !a.Dead))
            arrHeartSpawn = _now;
    }

    private void ArrTrialCast(DungeonPendingCast cast)
    {
        if (territory != ArrTrialCatalog.GARUDA || cast.Rule.Action != 667) return;
        var index = arrTrialLos.FindIndex(v => v.Cast.Source == cast.Source && v.Cast.StartedAt == cast.StartedAt);
        if (index >= 0)
        {
            // 同一读条的包补正只纠正时序记录，已生成的阴影不随之后的石柱移动。
            arrTrialLos[index] = (cast, arrTrialLos[index].Safe, arrTrialLos[index].Danger);
            return;
        }
        var active = _casts.Values.Where(c => c.Rule.Action == 667).MinBy(c => c.FinishAt)!;
        var pillars = _actors.Values.Where(a => a.Oid == 0xED).ToArray();
        if (pillars.Length == 0) return;
        arrTrialLos.Add((cast, new(new(active.Location, pillars, true, 31.7f)),
            new(new(active.Location, pillars, false, 31.7f))));
    }

    private void ArrTrialFinish(DungeonPendingCast cast)
    {
        if (territory == ArrTrialCatalog.GARUDA && cast.Rule.Action == 667 && arrTrialLos.Count > 0)
            arrTrialLos.RemoveAt(0);
    }

    private void ArrTrialEffect(uint action)
    {
        if (territory == ArrTrialCatalog.GARUDA && action == 662) arrEyeActive = true;
    }

    private IEnumerable<DungeonAmbientAoe> ArrTrialAoes()
    {
        if (!IsArrTrial || Active == null || Arena == null) yield break;
        if (_actors.TryGetValue(PrimaryId, out var boss) && !boss.Dead && boss.Targetable
            && _actors.TryGetValue(boss.TargetId, out var target))
        {
            var radius = territory switch
            {
                ArrTrialCatalog.IFRIT => 16f, ArrTrialCatalog.TITAN => 11.25f, _ => 11.7f,
            };
            var rotation = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            var shape = new FanTemplate(radius, MathF.PI * 2 / 3);
            yield return new("arr-trial/cleave/display", "顺劈", shape, boss.Position, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new("arr-trial/cleave/avoid", "避开顺劈", shape, boss.Position, rotation, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"arr-trial/cleave/{member.Id}", "顺劈避开队友", new FanTemplate(100, MathF.PI * 2 / 3),
                        boss.Position, MathF.Atan2(member.Position.X - boss.Position.X, member.Position.Z - boss.Position.Z), Display: false);
        }
        if (territory == ArrTrialCatalog.IFRIT)
            foreach (var nail in _actors.Values.Where(a => a.Oid == 0xD0))
                yield return new($"arr-trial/nail/{nail.Id}", "炎狱之楔", new CircleTemplate(Math.Max(.5f, nail.HitboxRadius)),
                    nail.Position, Navigate: false, OutlineOnly: true);
        if (territory != ArrTrialCatalog.GARUDA) yield break;
        foreach (var pillar in _actors.Values.Where(a => a.Oid == 0xED))
        {
            yield return new($"arr-trial/pillar/{pillar.Id}", "石柱", new CircleTemplate(pillar.HitboxRadius),
                pillar.Position, Navigate: false, OutlineOnly: true);
            if (_actors.GetValueOrDefault(PrimaryId)?.Cast == null)
                yield return new($"arr-trial/friction/{pillar.Id}", "保护石柱", new CircleTemplate(5),
                    pillar.Position, Display: false);
        }
        if (arrEyeActive)
            foreach (var eye in _actors.Values.Where(a => a.Oid == 0x622))
                yield return new($"arr-trial/eye/{eye.Id}", "风眼", new DonutTemplate(12, 25), eye.Position);
        if (arrTrialLos.Count == 0) yield break;
        var los = arrTrialLos[0];
        yield return new("arr-trial/los/display", "躲在石柱后", los.Safe, los.Cast.Location,
            Navigate: false, Safe: true, Activation: los.Cast.FinishAt);
        yield return new("arr-trial/los/avoid", "躲在石柱后", los.Danger, los.Cast.Location,
            Display: false, Activation: los.Cast.FinishAt);
    }

    internal string? ArrTrialCountdown
    {
        get
        {
            if (!IsArrTrial || Active == null) return null;
            if (territory == ArrTrialCatalog.IFRIT && arrNailSpawn != default
                && _actors.Values.FirstOrDefault(a => a.Oid == 0xD0) is { Targetable: true, Dead: false })
                return $"火柱狂暴剩余：{Math.Max(35 - (_now - arrNailSpawn).TotalSeconds, 0):F1} 秒";
            if (territory == ArrTrialCatalog.TITAN && arrHeartSpawn != default
                && _actors.Values.Any(a => a.Oid == 0x5E3 && a.Targetable && !a.Dead))
                return $"心核狂暴剩余：{Math.Max(62 - (_now - arrHeartSpawn).TotalSeconds, 0):F1} 秒";
            return null;
        }
    }

    private IEnumerable<string> ArrTrialHints(Vector3? player)
    {
        if (!IsArrTrial || Active == null) yield break;
        if (ArrTrialCountdown is { } countdown) yield return countdown;
        if (player is { } position)
        {
            var ambient = ArrTrialAoes().Where(a => a.Id == "arr-trial/cleave/avoid" || a.Id.StartsWith("arr-trial/eye/"));
            foreach (var aoe in ambient.Where(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
                yield return aoe.Id == "arr-trial/cleave/avoid" ? "避开顺劈" : "离开风眼危险环";
        }
        if (arrTrialLos.Count > 0 && player is { } p && !arrTrialLos[0].Safe.Geometry.Contains(p)
            && Vector2.DistanceSquared(new(p.X, p.Z), new(arrTrialLos[0].Cast.Location.X, arrTrialLos[0].Cast.Location.Z)) < 31.7f * 31.7f)
            yield return "躲在石柱后";
    }

    private int ArrTrialPriority(DungeonActor actor) => territory switch
    {
        ArrTrialCatalog.IFRIT => actor.Oid == 0xD0 ? 2 : 0,
        ArrTrialCatalog.TITAN => actor.Oid switch { 0x5A4 => 3, 0x5E3 => 2, 0xF6 => 1, _ => 0 },
        ArrTrialCatalog.GARUDA => actor.Oid switch { 0xEE or 0x2B0 => 2, 0xEF => 1, _ => 0 },
        _ => 0,
    };

    private void ResetArrTrial()
    {
        arrNailSpawn = default; arrHeartSpawn = default; arrEyeActive = false; arrTrialLos.Clear();
    }
}
