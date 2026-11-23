using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TenderAoes()
    {
        if (territory != TenderValleyCatalog.TERRITORY || Arena == null) yield break;
        if (tenderShrink != null) yield return tenderShrink;
        foreach (var cast in tenderSimple.Where(c => c.Rule.Action != 36754).Concat(tenderSimple.Where(c => c.Rule.Action == 36754).Take(10)))
            yield return new($"tender/simple/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name, cast.Rule.Shape!,
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        var needles = tenderSimple.FirstOrDefault(c => c.Rule.Action == 37386);
        if (needles != null && tenderNeedlesCount >= 16)
            yield return new("tender/needles/center", "连续针刺靠近首个施法原点", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(needles.Location, 3)]), Arena.Position, Display: false, Activation: needles.FinishAt);
        var kb = _casts.Values.FirstOrDefault(c => c.Rule.Action is 37390 or 36756);
        var player = _actors.GetValueOrDefault(PlayerId);
        if (tenderCactiActive && needles == null)
            foreach (var cactus in tenderCacti.Take(8))
                yield return cactus with { Navigate = kb == null || player != null && KnockbackImmune(player, kb.FinishAt) };
        foreach (var statue in tenderStatues) yield return statue;
        foreach (var oid in new uint[] { 0x1EBA86, 0x1EBA87, 0x1EBA88 })
        {
            var zone = tenderSludge.Select(id => _actors.GetValueOrDefault(id)).FirstOrDefault(a => a?.Oid == oid);
            if (zone == null || zone.EventState == 7) continue;
            yield return new($"tender/sludge/{zone.Id}", "持续污泥", new CircleTemplate(oid == 0x1EBA86 ? 6 : oid == 0x1EBA87 ? 5 : 4),
                zone.Position, Obstacle: true);
        }
        foreach (var lab in tenderLabyrinth)
        {
            var shape = lab.Index switch
            {
                0 => TenderValleyArenas.Labyrinth0(Arena.Position.Y), 1 => TenderValleyArenas.Labyrinth1(Arena.Position.Y),
                2 => TenderValleyArenas.Labyrinth2(Arena.Position.Y), _ => TenderValleyArenas.Labyrinth3(Arena.Position.Y),
            };
            var key = $"tender/labyrinth/{lab.Serial}";
            yield return new(key + "/danger", "迷宫危险格", new DungeonShapeTemplate(shape), shape.Position);
            for (var i = 0; i < lab.Tiles.Length; ++i)
                yield return new(key + $"/tile/{i}", "迷宫安全格", new DungeonShapeTemplate(lab.Tiles[i]), lab.Tiles[i].Position,
                    Navigate: false, Safe: true, Activation: lab.At);
            // Safe-colored source AOEs invert their AI distance, rather than becoming soft goals.
            yield return new(key + "/enter", "十秒后必须进入安全格", new SohrKhaiWallHintTemplate(Arena, lab.Tiles),
                Arena.Position, Display: false, Activation: lab.At);
        }
        if (Active?.BossOid == 0x4253)
            foreach (var a in _actors.Values.Where(a => a.Oid is 0x424F or 0x448D or 0x4251))
                yield return new($"tender/trash/{a.Id}", a.Name, new CircleTemplate(a.HitboxRadius), a.Position,
                    Navigate: false, OutlineOnly: true);
        foreach (var aoe in TenderPartyAoes()) yield return aoe;
        foreach (var b in tenderBaits)
        {
            var key = $"tender/bait/{b.Serial}";
            yield return new(key + "/display", "死刑诱导", new CircleTemplate(3), QuantizedDungeonPosition(b.Target.Position),
                Navigate: false, OutlineOnly: b.Target.Id == PlayerId);
            if (b.Target.Id != PlayerId)
                yield return new(key + "/avoid", "远离死刑目标", new CircleTemplate(3), b.Target.Position, Display: false, Activation: b.At);
            else
                foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(key + $"/party/{a.Id}", "死刑远离队友", new CircleTemplate(3), a.Position, Display: false, Activation: b.At);
        }
        foreach (var aoe in TenderKnockbackAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> TenderPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var player = _actors.GetValueOrDefault(PlayerId);
        foreach (var spread in tenderSpreads.Where(s => !s.Target.Dead))
        {
            var key = $"tender/spread/{spread.Serial}";
            yield return new(key + "/display", "分散", new CircleTemplate(6), QuantizedDungeonPosition(spread.Target.Position),
                Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (spread.Target.Id != PlayerId)
                yield return new(key + "/avoid", "避开分散目标", new CircleTemplate(7), QuantizedDungeonPosition(spread.Target.Position),
                    Display: false, Activation: spread.At);
            else
                foreach (var a in party.Where(a => a.Id != PlayerId && !tenderSpreads.Any(s => s.Action == spread.Action && !s.Target.Dead && s.Target.Id == a.Id)))
                    yield return new(key + $"/party/{a.Id}", "分散远离队友", new CircleTemplate(7), a.Position,
                        Display: false, Activation: spread.At);
        }
        // Each icon size is a distinct source component and has an independent candidate pool.
        foreach (var component in tenderStacks.Where(s => !s.Target.Dead).GroupBy(s => s.Action))
        {
            var stacks = component.ToArray();
            var marked = stacks.Any(s => s.Target.Id == PlayerId);
            var joins = new List<IGeometryShape>();
            foreach (var s in stacks)
            {
                var circle = new CircleGeometry(QuantizedDungeonPosition(s.Target.Position), s.Radius);
                var count = party.Count(a => circle.Contains(a.Position, 0));
                var inside = player != null && circle.Contains(player.Position, 0);
                var safe = s.Target.Id == PlayerId || !marked && (inside ? count <= 4 : count < 4);
                var key = $"tender/stack/{s.Serial}";
                yield return new(key + "/display", "四人分摊", new CircleTemplate(s.Radius), circle.Position,
                    Navigate: false, OutlineOnly: true, Safe: safe);
                if (party.Length < 2) continue;
                if (s.Target.Id == PlayerId)
                {
                    var candidates = party.Where(a => !stacks.Any(other => other.Target.Id == a.Id))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, s.Radius * .5f)).ToArray();
                    if (candidates.Length > 0)
                        yield return new(key + "/gather", "分摊目标靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                            Arena.Position, Display: false, Activation: s.At);
                }
                else if (safe) joins.Add(circle);
                else yield return new(key + "/avoid", "避开不可参与的分摊", new CircleTemplate(marked ? s.Radius * 2 : s.Radius),
                    circle.Position, Display: false, Activation: s.At);
            }
            if (joins.Count > 0)
                yield return new($"tender/stack/join/{component.Key}", "进入一个分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                    Arena.Position, Display: false, Activation: stacks[0].At);
        }
    }

    private IEnumerable<DungeonAmbientAoe> TenderKnockbackAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var casts = _casts.Values.Where(c => c.Rule.Action is 37390 or 36756).ToArray();
        if (casts.Length == 0) yield break;
        var first = casts[0];
        var distance = first.Rule.Action == 37390 ? 20 : 15;
        if (!KnockbackImmune(player, first.FinishAt))
        {
            var dangers = first.Rule.Action == 37390
                ? tenderCacti.Take(8).Select(a => (IGeometryShape)new CircleGeometry(a.Position, ((CircleTemplate)a.Shape).Radius + 1)).ToArray()
                : [];
            var half = first.Rule.Action == 37390 ? 19 : 11;
            yield return new("tender/kb/nav", "击退落点安全约束", new VanaspatiKnockbackTemplate(
                new RectangleGeometry(Arena.Position, 0, half * 2, half * 2), first.Location, dangers, distance),
                Arena.Position, Display: false, Activation: first.FinishAt);
        }
        var start = player.Position;
        foreach (var cast in casts)
        {
            if (KnockbackImmune(player, cast.FinishAt)) continue;
            var landing = new VanaspatiKnockbackTemplate(Arena, cast.Location, [], cast.Rule.Action == 37390 ? 20 : 15).Landing(start);
            var safe = Arena.Contains(landing, 0) && (cast.Rule.Action != 37390
                || !tenderCacti.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(landing, 0)));
            foreach (var a in DungeonMovementPreview($"tender/kb/{cast.Source}", "击退落点", start, landing))
                yield return a with { Safe = safe };
            start = landing;
        }
    }

    private IEnumerable<DungeonDamagePrediction> TenderDamage()
    {
        if (territory != TenderValleyCatalog.TERRITORY) yield break;
        foreach (var b in tenderBaits.Where(b => b.Target.PartySlot >= 0))
            yield return new(b.Target.Id, b.Target.PartySlot, b.Target.PartyRole, b.Target.Name, b.At, "Tankbuster");
        var spreads = tenderSpreads.Where(s => s.Source == 0 && s.Target.PartySlot >= 0).ToArray();
        if (spreads.Length > 0)
            foreach (var a in spreads.Select(s => s.Target).DistinctBy(a => a.Id))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, spreads.Min(s => s.At), "Raidwide");
    }

    private IEnumerable<string> TenderHints()
    {
        if (territory != TenderValleyCatalog.TERRITORY) yield break;
        if (tenderLabyrinth.Count > 0 && _actors.GetValueOrDefault(PlayerId) is { } player
            && !tenderLabyrinth[0].Tiles.Any(t => t.Contains(player.Position, 0)))
            yield return "走到安全方格上";
        if (tenderStacks.Any(s => !s.Target.Dead)) yield return "四人分摊";
        if (tenderSpreads.Count > 0) yield return "分散远离队友";
        if (tenderBaits.Count > 0) yield return "死刑诱导远离队友";
    }
}
