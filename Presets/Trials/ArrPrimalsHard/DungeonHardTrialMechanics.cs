using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonActor> hardCyclones = [];
    private readonly List<DungeonPendingCast> hardBombs = [];
    private readonly List<(DungeonPendingCast Cast, DungeonLosTemplate Safe, DungeonLosTemplate Danger)> hardLos = [];
    private readonly Dictionary<ulong, (uint Id, DateTime At)> hardTethers = [];
    private readonly HashSet<(ulong Source, DateTime Start)> hardGeocrushCasts = [];
    private DateTime hardNailSpawn;
    private DateTime hardHeartSpawn;
    private int hardGeocrushCount;
    private float? hardGeocrushRadius;
    private DungeonPendingCast? hardGeocrush;
    private bool IsHardTrial => HardTrialCatalog.IsTerritory(territory);

    private void HardTrialObserve()
    {
        if (!IsHardTrial || Active == null) return;
        if (territory == HardTrialCatalog.IFRIT && hardNailSpawn == default
            && _actors.Values.Any(a => a.Oid == 0xD2 && a.Targetable && !a.Dead)) hardNailSpawn = _now;
        if (territory == HardTrialCatalog.TITAN && hardHeartSpawn == default
            && _actors.Values.Any(a => a.Oid == 0x5E4 && a.Targetable && !a.Dead)) hardHeartSpawn = _now;
        for (var i = 0; i < hardCyclones.Count; ++i)
            if (_actors.TryGetValue(hardCyclones[i].Id, out var actor)) hardCyclones[i] = actor;
    }

    private void HardTrialTimeline(ulong source, uint timeline)
    {
        if (territory == HardTrialCatalog.IFRIT && Active != null && source != PrimaryId && timeline == 0x008D
            && _actors.GetValueOrDefault(source) is { Oid: 0xD1 } actor)
            hardCyclones.Add(actor);
    }

    private void HardTrialTether(ulong source, uint id, bool present, DateTime now)
    {
        if (territory != HardTrialCatalog.GARUDA || Active == null) return;
        if (present) hardTethers[source] = (id, now);
        else if (!hardTethers.TryGetValue(source, out var previous) || previous.Id == id) hardTethers[source] = (0, now);
    }

    private void HardTrialCast(DungeonPendingCast cast)
    {
        if (!IsHardTrial) return;
        if (territory == HardTrialCatalog.GARUDA && cast.Rule.Action is 1384 or 1390)
        {
            var existing = hardLos.FindIndex(l => l.Cast.Source == cast.Source && l.Cast.StartedAt == cast.StartedAt);
            if (existing >= 0)
            {
                hardLos[existing] = (cast, hardLos[existing].Safe, hardLos[existing].Danger);
                return;
            }
            var active = _casts.Values.Where(c => c.Rule.Action == cast.Rule.Action).MinBy(c => c.FinishAt)!;
            var blockers = _actors.Values.Where(a => a.Oid == 0xF0).ToArray();
            var radius = cast.Rule.Action == 1384 ? 24.7f : 31.7f;
            if (blockers.Length > 0)
                hardLos.Add((cast, new(new(active.Location, blockers, true, radius)),
                    new(new(active.Location, blockers, false, radius))));
        }
        if (territory != HardTrialCatalog.TITAN) return;
        if (cast.Rule.Action == 1365)
        {
            // 同一读条的包补正不能推进跳跃次数。
            hardGeocrush = cast;
            if (!hardGeocrushCasts.Add((cast.Source, cast.StartedAt))) return;
            hardGeocrushRadius = ++hardGeocrushCount switch { 1 => 23, 2 => 20, 3 => 15, _ => 25 };
        }
        if (cast.Rule.Action != 1052) return;
        var index = hardBombs.FindIndex(b => b.Source == cast.Source && b.StartedAt == cast.StartedAt);
        if (index >= 0) hardBombs[index] = cast;
        else hardBombs.Add(cast);
        // 上游只在积累到默认最大显示数时排序，较小集合保持读条到达顺序。
        if (hardBombs.Count >= 99) hardBombs.Sort((a, b) => a.FinishAt.CompareTo(b.FinishAt));
    }

    private void HardTrialFinish(DungeonPendingCast cast)
    {
        if (territory == HardTrialCatalog.IFRIT && cast.Rule.Action == 457)
        {
            var index = hardCyclones.FindIndex(a => a.Id == cast.Source);
            if (index >= 0) hardCyclones.RemoveAt(index);
        }
        if (territory == HardTrialCatalog.GARUDA && cast.Rule.Action is 1384 or 1390)
        {
            var index = hardLos.FindIndex(l => l.Cast.Rule.Action == cast.Rule.Action);
            if (index >= 0) hardLos.RemoveAt(index);
        }
        if (territory == HardTrialCatalog.TITAN && cast.Rule.Action == 1052)
        {
            var index = hardBombs.FindIndex(b => b.Source == cast.Source);
            if (index >= 0) hardBombs.RemoveAt(index);
        }
        if (territory == HardTrialCatalog.TITAN && cast.Rule.Action == 1365) hardGeocrush = null;
    }

    private IEnumerable<DungeonAmbientAoe> HardTrialAoes()
    {
        if (!IsHardTrial || Active == null || Arena == null) yield break;
        var sources = _actors.Values.Where(a => a.Oid == Active.BossOid);
        foreach (var boss in sources.Where(a => !a.Dead && a.Targetable))
        {
            if (!_actors.TryGetValue(boss.TargetId, out var target)) continue;
            var radius = territory switch { HardTrialCatalog.IFRIT => 15f, HardTrialCatalog.TITAN => 21.25f, _ => 11.7f };
            var rotation = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            var shape = new FanTemplate(radius, MathF.PI * 2 / 3);
            yield return new($"hard-trial/cleave/{boss.Id}/display", "顺劈", shape, boss.Position, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"hard-trial/cleave/{boss.Id}/avoid", "避开顺劈", shape, boss.Position, rotation, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"hard-trial/cleave/{boss.Id}/{member.Id}", "顺劈避开队友", new FanTemplate(100, MathF.PI * 2 / 3),
                        boss.Position, MathF.Atan2(member.Position.X - boss.Position.X, member.Position.Z - boss.Position.Z), Display: false);
        }
        if (territory == HardTrialCatalog.IFRIT)
        {
            foreach (var nail in _actors.Values.Where(a => a.Oid == 0xD2 && a.Targetable && !a.Dead))
                yield return new($"hard-trial/nail/{nail.Id}", "炎狱之楔", new CircleTemplate(nail.HitboxRadius),
                    nail.Position, Navigate: false, OutlineOnly: true);
            for (var i = 0; i < hardCyclones.Count; ++i)
            {
                var actor = hardCyclones[i];
                var cast = actor.Cast;
                var locked = _casts.GetValueOrDefault(actor.Id);
                yield return new($"hard-trial/cyclone/{i}", "深红旋风", RayRectangleTemplate.Symmetric(12, 43),
                    locked?.Location ?? cast?.Location ?? actor.Position, locked?.Rotation ?? cast?.Rotation ?? actor.Rotation,
                    Activation: locked?.FinishAt ?? cast?.FinishAt ?? _now.AddSeconds(4));
            }
        }
        if (territory == HardTrialCatalog.GARUDA)
        {
            foreach (var pillar in _actors.Values.Where(a => a.Oid == 0xF0))
            {
                yield return new($"hard-trial/pillar/{pillar.Id}", "石柱", new CircleTemplate(pillar.HitboxRadius),
                    pillar.Position, Navigate: false, OutlineOnly: true, Color: new(1, 128f / 255, 0, 1));
                if (_actors.GetValueOrDefault(PrimaryId)?.Cast == null && !_casts.ContainsKey(PrimaryId))
                    yield return new($"hard-trial/friction/{pillar.Id}", "保护石柱", new CircleTemplate(5), pillar.Position, Display: false);
            }
            foreach (var action in new[] { 1384u, 1390u })
            {
                var first = hardLos.FirstOrDefault(l => l.Cast.Rule.Action == action);
                if (first.Cast == null) continue;
                yield return new($"hard-trial/los/{action}/display", "躲在石柱后", first.Safe, first.Cast.Location,
                    Navigate: false, Safe: true, Activation: first.Cast.FinishAt);
                yield return new($"hard-trial/los/{action}/avoid", "躲在石柱后", first.Danger, first.Cast.Location,
                    Display: false, Activation: first.Cast.FinishAt);
            }
            foreach (var add in _actors.Values.Where(a => a.Oid is 0x61E or 0x61F))
                yield return new($"hard-trial/add/{add.Id}", "分身", new CircleTemplate(add.HitboxRadius),
                    add.Position, Navigate: false, OutlineOnly: true);
        }
        if (territory == HardTrialCatalog.TITAN)
        {
            // 2025-03-12 的枚举接口重构误将两次 yield 改为提前 return；按父提交恢复内外圈同时输出。
            // 外圈仍跨读条保留，正式场地不缩小；25–25 的零面积环不发布为可外扩危险。
            if (hardGeocrushRadius is { } radius)
            {
                if (radius < 25)
                    yield return new("hard-trial/geocrush/outer", "大地粉碎外圈",
                        new DungeonShapeTemplate(new DonutGeometry(Arena.Position, radius, 25)), Arena.Position);
                if (hardGeocrush != null)
                    yield return new("hard-trial/geocrush/inner", "大地粉碎内圈",
                        new DungeonShapeTemplate(new CircleGeometry(Arena.Position, radius - 2)),
                        Arena.Position, Activation: hardGeocrush.FinishAt);
            }
            if (hardBombs.Count > 0)
            {
                var limit = hardBombs[0].FinishAt.AddSeconds(2.25);
                foreach (var bomb in hardBombs.TakeWhile(b => b.FinishAt < limit))
                    yield return new($"hard-trial/bomb/{bomb.Source}", "爆破", new CircleTemplate(6.3f), bomb.Location, Activation: bomb.FinishAt);
            }
        }
    }

    internal string? HardTrialCountdown
    {
        get
        {
            if (!IsHardTrial || Active == null) return null;
            if (territory == HardTrialCatalog.IFRIT && hardNailSpawn != default
                && _actors.Values.Any(a => a.Oid == 0xD2 && a.Targetable && !a.Dead))
                return $"火柱狂暴剩余：{Math.Max(55 - (_now - hardNailSpawn).TotalSeconds, 0):F1} 秒";
            if (territory == HardTrialCatalog.TITAN && hardHeartSpawn != default
                && _actors.Values.Any(a => a.Oid == 0x5E4 && a.Targetable && !a.Dead))
                return $"心核狂暴剩余：{Math.Max(62 - (_now - hardHeartSpawn).TotalSeconds, 0):F1} 秒";
            return null;
        }
    }

    private IEnumerable<string> HardTrialHints(Vector3? player)
    {
        if (!IsHardTrial || Active == null || player is not { } p) yield break;
        foreach (var aoe in HardTrialAoes().Where(a => a.Navigate && !a.Safe && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p)))
            yield return aoe.Name;
    }

    private int HardTrialPriority(DungeonActor actor) => territory switch
    {
        HardTrialCatalog.IFRIT => actor.Oid == 0xD2 ? 2 : actor.Id == PrimaryId ? 1 : 0,
        HardTrialCatalog.TITAN => actor.Oid switch { 0x5E1 => 3, 0x5E4 => 2, 0xF7 => 1, _ => 0 },
        HardTrialCatalog.GARUDA => actor.Oid switch
        {
            0x61E or 0x61F => HardTrialGreenTether(actor) ? 3 : 2,
            0x5FF => 3,
            0xF1 => 2,
            0xF2 => 1,
            _ => 0,
        },
        _ => 0,
    };

    private bool HardTrialGreenTether(DungeonActor actor)
    {
        if (hardTethers.TryGetValue(actor.Id, out var tether) && _now < tether.At.AddSeconds(.5)) return tether.Id == 4;
        return actor.Tethers?.ContainsKey(4) == true;
    }

    private void ResetHardTrial()
    {
        hardCyclones.Clear(); hardBombs.Clear(); hardLos.Clear(); hardTethers.Clear(); hardGeocrushCasts.Clear();
        hardNailSpawn = default; hardHeartSpawn = default; hardGeocrushCount = 0; hardGeocrushRadius = null; hardGeocrush = null;
    }
}
