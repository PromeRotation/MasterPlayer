using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> HeroesAoes()
    {
        if (territory != HeroesCatalog.TERRITORY || Arena == null) yield break;
        var knives = _casts.Values.Any(c => c.Rule.Action == 20439);
        foreach (var aoe in heroesDash) yield return aoe with { Navigate = !knives };
        foreach (var aoe in heroesBursts) yield return aoe;
        foreach (var pool in _actors.Values.Where(a => a.Oid == 0x1EB02C && a.EventState != 7))
            yield return new($"heroes/pool/{pool.Id}", "持续出血池", new CircleTemplate(8), pool.Position);
        var pools = _actors.Values.Any(a => a.Oid == 0x1EB02C && a.EventState != 7);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 20388))
            yield return new($"heroes/mire/{cast.Source}", "出血池预告", cast.Rule.Shape!, cast.Location, Navigate: !pools, Activation: cast.FinishAt);
        for (var i = 0; i < heroesBombBaits.Count; ++i)
        {
            var bait = heroesBombBaits[i];
            if (!_actors.TryGetValue(bait.Target, out var t)) continue;
            var own = t.Id == PlayerId;
            yield return new($"heroes/bait/{i}", "僵尸爆炸诱导", new CircleTemplate(9.25f), t.Position,
                Navigate: !own, OutlineOnly: own, Activation: bait.Activation);
            if (own)
                foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"heroes/bait/{i}/avoid/{a.Id}", "爆炸诱导远离队友", new CircleTemplate(9.25f), a.Position,
                        Display: false, Activation: bait.Activation);
        }
        if (Active?.BossOid == 0x2DF1 && heroesBombBaits.Count == 0)
            foreach (var a in _actors.Values.Where(a => a.Hostile && a.Tethers?.ContainsKey(79) == true))
                yield return new($"heroes/crawling/{a.Id}", "避开爬行僵尸", new CircleTemplate(8), a.Position, Display: false);
        if (Active?.BossOid != 0x2EFD) yield break;
        if (heroesCollapse != null) yield return heroesCollapse;
        foreach (var (id, activation) in heroesSpreads)
        {
            if (!_actors.TryGetValue(id, out var t)) continue;
            var own = id == PlayerId;
            yield return new($"heroes/spread/{id}", "独自引导碎石", new CircleTemplate(8.5f), t.Position,
                Navigate: !own, OutlineOnly: own, Activation: activation);
            if (own)
                foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"heroes/spread/{id}/avoid/{a.Id}", "碎石分散", new CircleTemplate(8.5f), a.Position, Display: false, Activation: activation);
        }
        if (heroesRubble.Count > 0)
        {
            foreach (var (id, rubble) in heroesRubble)
                yield return new($"heroes/rubble/{id}", "独自接碎石", new CircleTemplate(rubble.Actor.HitboxRadius),
                    rubble.Actor.Position, Navigate: false, Safe: true, OutlineOnly: true);
            yield return new("heroes/rubble/enter", "站在任一碎石内", new SohrKhaiWallHintTemplate(Arena,
                heroesRubble.Values.Select(r => (IGeometryShape)new CircleGeometry(r.Actor.Position, r.Actor.HitboxRadius)).ToArray()),
                Arena.Position, Display: false, Activation: heroesRubble.Values.Min(r => r.Activation));
        }
        if (!_actors.Values.Any(a => a.Oid == 0x2EFE && !a.Dead))
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 21000))
                if (_actors.TryGetValue(cast.TargetId, out var target))
                {
                    yield return new($"heroes/stack/{cast.Source}", "四人分摊", new CircleTemplate(6), target.Position, Navigate: false, Safe: true);
                    var regions = target.Id == PlayerId
                        ? _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead)
                            .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray()
                        : [new CircleGeometry(target.Position, 6)];
                    if (regions.Length > 0)
                        yield return new($"heroes/stack/enter/{cast.Source}", "集合分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                            Arena.Position, Display: false, Activation: cast.FinishAt);
                }
        var rampage = _casts.Values.FirstOrDefault(c => c.Rule.Action == 20999);
        foreach (var crater in heroesCraters)
            yield return new($"heroes/crater/{crater.X}", "坑洞", new CircleTemplate(7), crater, Navigate: rampage == null, Safe: rampage != null);
        if (rampage != null && heroesCraters.Count > 0)
            yield return new("heroes/crater/enter", "进入任一坑洞避狂乱", new SohrKhaiWallHintTemplate(Arena,
                heroesCraters.Select(p => (IGeometryShape)new CircleGeometry(p, 7)).ToArray()),
                Arena.Position, Display: false, Activation: rampage.FinishAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 20996))
            if (_actors.TryGetValue(PlayerId, out var player) && !KnockbackImmune(player, cast.FinishAt))
            {
                var kb = new HeroesKnockbackTemplate(cast.Location, HeroesArenas.BerserkerLanding(Arena.Position.Y));
                yield return new($"heroes/knockback/{cast.Source}", "击退后避开坑洞和场外", kb, Arena.Position, Display: false, Activation: cast.FinishAt);
                yield return new($"heroes/knockback/{cast.Source}/landing", "预计击退落点", new CircleTemplate(.5f), kb.Landing(player.Position),
                    Navigate: false, Safe: true, OutlineOnly: true);
            }
    }
}
