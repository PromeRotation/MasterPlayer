using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool AlexSpreadAllowed => !alexSimple.Any(c => c.Rule.Action == 39616 || AlexandriaCatalog.Partition(c.Rule.Action));
    private bool AlexSalvationAllowed => !_casts.Values.Any(c => c.Rule.Action == 36794);

    private IEnumerable<DungeonAmbientAoe> AlexAoes()
    {
        if (territory != AlexandriaCatalog.TERRITORY || Arena == null) yield break;
        if (alexShrink != null) yield return alexShrink;
        var explosions = alexSimple.Where(c => c.Rule.Action == 39239).ToArray();
        foreach (var cast in alexSimple)
        {
            var index = cast.Rule.Action == 39239 ? Array.IndexOf(explosions, cast) : -1;
            yield return new($"alex/simple/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name, cast.Rule.Shape!,
                cast.Location, cast.Rotation, Navigate: index < 4, Activation: cast.FinishAt,
                Color: index is >= 0 and < 2 && explosions.Length > 2 ? new Vector4(1, 1, 0, 1) : null);
        }
        foreach (var q in alexQueue.Take(2))
        {
            if (q.Kind == 0x4174 && q.Inner != 4)
            {
                yield return q.Aoe with { Navigate = false };
                if (q.Aoe.Navigate)
                    yield return q.Aoe with { Id = q.Aoe.Id + "/nav", Shape = new DonutTemplate(q.Inner, 40), Display = false };
            }
            else yield return q.Aoe;
        }
        foreach (var t in alexTernary)
        {
            GeometryTemplate shape = t.Step == 0 ? new CircleTemplate(10) : t.Step == 1 ? new DonutTemplate(10, 20) : new DonutTemplate(20, 30);
            yield return new($"alex/ternary/{t.Source}/{t.Start.Ticks}", "三段同心攻击", shape, t.Origin, Activation: t.At);
        }
        foreach (var spread in alexSpreads.Where(s => !s.Target.Dead))
        {
            var key = $"alex/spread/{spread.Serial}";
            var target = spread.Target;
            yield return new(key + "/display", "分散", new CircleTemplate(6), QuantizedDungeonPosition(target.Position), Navigate: false, OutlineOnly: true);
            if (spread.Cast.Rule.Action == 39243 && !AlexSpreadAllowed || _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead) < 2) continue;
            if (target.Id != PlayerId)
                yield return new(key + "/avoid", "避开分散目标", new CircleTemplate(7), QuantizedDungeonPosition(target.Position),
                    Display: false, Activation: spread.Cast.FinishAt);
            else
                foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && !alexSpreads.Any(s => s.Cast.Rule.Action == spread.Cast.Rule.Action && !s.Target.Dead && s.Target.Id == a.Id)))
                    yield return new(key + $"/party/{a.Id}", "分散远离队友", new CircleTemplate(7), a.Position, Display: false, Activation: spread.Cast.FinishAt);
        }
        var firstSpread = alexSpreads.FirstOrDefault(s => s.Cast.Rule.Action == 39243 && !s.Target.Dead);
        if (firstSpread.Cast != null && AlexSpreadAllowed)
            yield return new("alex/spread/south", "分散避开南侧半场", new CircleTemplate(15), Arena.Position - Vector3.UnitZ * 15,
                Display: false, Activation: firstSpread.Cast.FinishAt);
        foreach (var aoe in AlexStackAoes()) yield return aoe;
        foreach (var bait in alexDisinfection)
        {
            var key = $"alex/disinfection/{bait.Serial}";
            yield return new(key + "/display", "死刑点名", new CircleTemplate(6), bait.Target.Position, Navigate: false, OutlineOnly: bait.Target.Id == PlayerId);
            // Early positioning is a soft preference for the first bait only; the base AI starts strictly inside two seconds.
            if (alexDisinfection[0].At.AddSeconds(-2) >= _now) continue;
            if (bait.Target.Id != PlayerId)
                yield return new(key + "/avoid", "避开死刑目标", new CircleTemplate(6), bait.Target.Position, Display: false, Activation: bait.At);
            else
                foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(key + $"/party/{a.Id}", "死刑远离队友", new CircleTemplate(6), a.Position, Display: false, Activation: bait.At);
        }
        if (alexDisinfection.Count > 0 && alexDisinfection[0].Target.Id == PlayerId && alexDisinfection[0].At.AddSeconds(-2) >= _now)
            yield return new("alex/disinfection/goal", "死刑点名提前靠北", new CircleTemplate(3), new(852, Arena.Position.Y, 808),
                Safe: true, Display: false, GoalPriority: 1);
        foreach (var aoe in AlexLineAoes()) yield return aoe;
        foreach (var aoe in AlexKnockbackAoes()) yield return aoe;
        foreach (var a in _actors.Values.Where(a => a.Oid == 0x41D1))
            yield return new($"alex/generator/{a.Id}", "充能发生器", new CircleTemplate(a.HitboxRadius), QuantizedDungeonPosition(a.Position),
                Navigate: false, OutlineOnly: true, Color: new(0, 1, 1, 1));
    }

    private IEnumerable<DungeonAmbientAoe> AlexStackAoes()
    {
        if (Arena == null) yield break;
        var player = _actors.GetValueOrDefault(PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var stacks = alexStacks.Where(s => !s.Target.Dead).ToArray();
        var targets = stacks.Select(s => s.Target.Id).ToHashSet();
        var ownStack = targets.Contains(PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var s in stacks)
        {
            var shape = new CircleGeometry(QuantizedDungeonPosition(s.Target.Position), 6);
            var count = party.Count(a => (s.Forbidden & (1UL << a.PartySlot)) == 0 && shape.Contains(a.Position, 0));
            var inside = player != null && shape.Contains(player.Position, 0);
            var forbidden = player?.PartySlot is >= 0 and < 64 && (s.Forbidden & (1UL << player.PartySlot)) != 0;
            var max = Active?.BossOid == 0x4173 ? 3 : 4;
            var safe = !forbidden && (s.Target.Id == PlayerId || !ownStack && (inside ? count <= max : count < max));
            yield return new($"alex/stack/{s.Serial}/display", $"{max}人分摊", new CircleTemplate(6), QuantizedDungeonPosition(s.Target.Position),
                Navigate: false, OutlineOnly: true, Safe: safe);
            if (party.Length < 2) continue;
            if (s.Target.Id != PlayerId)
            {
                if (safe) joins.Add(shape);
                else yield return new($"alex/stack/{s.Serial}/avoid", "避开不可参与的分摊", new CircleTemplate(ownStack ? 12 : 6),
                    s.Target.Position, Display: false, Activation: s.At);
            }
            else
            {
                var candidates = party.Where(a => !targets.Contains(a.Id) && (s.Forbidden & (1UL << a.PartySlot)) == 0)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"alex/stack/{s.Serial}/gather", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: s.At);
            }
        }
        if (joins.Count > 0)
            yield return new("alex/stack/join", "进入分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<DungeonAmbientAoe> AlexLineStackAoes(uint resolve)
    {
        if (Arena == null) yield break;
        var lines = alexLines.Where(l => l.Resolve == resolve && !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var b in lines)
        {
            var delta = b.Target.Position - b.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(6, 40);
            yield return new($"alex/line/{b.Serial}/display", "直线分摊", shape, b.Source.Position, rotation, Navigate: false,
                Safe: b.Target.Id == PlayerId || !marked, Activation: b.At);
            if (b.Target.Id != PlayerId)
            {
                if (!marked) joins.Add(shape.Resolve(b.Source.Position, rotation, null));
                else yield return new($"alex/line/{b.Serial}/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(12, 40),
                    b.Source.Position, rotation, Display: false, Activation: b.At);
            }
            else
            {
                var candidates = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && !alexLines.Any(l => l.Target.Id == a.Id && l.Resolve == b.Resolve))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"alex/line/{b.Serial}/gather", "直线分摊靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: b.At);
            }
        }
        if (joins.Count > 0)
            yield return new($"alex/line/join/{resolve}", "进入直线分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonAmbientAoe> AlexLineAoes()
    {
        if (Arena == null) yield break;
        foreach (var resolve in new uint[] { 36780, 36787 })
            foreach (var aoe in AlexLineStackAoes(resolve)) yield return aoe;
        foreach (var b in alexSalvation.Where(b => !b.Source.Dead))
        {
            var delta = b.Target.Position - b.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(6, 40);
            yield return new($"alex/salvation/{b.Serial}/display", "直线诱导", shape, b.Source.Position, rotation, Navigate: false,
                OutlineOnly: b.Target.Id == PlayerId, Activation: b.At);
            if (!AlexSalvationAllowed) continue;
            if (b.Target.Id != PlayerId)
                yield return new($"alex/salvation/{b.Serial}/avoid", "避开队友直线诱导", shape, b.Source.Position, rotation, Display: false, Activation: b.At);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                {
                    var distance = Vector2.Distance(new(b.Source.Position.X, b.Source.Position.Z), new(member.Position.X, member.Position.Z));
                    // The source asin is undefined inside the half-width; its NaN SDF forbids no points.
                    if (distance < 3) continue;
                    yield return new($"alex/salvation/{b.Serial}/party/{member.Id}", "直线诱导避开队友",
                        new FanTemplate(100, 2 * MathF.Asin(3 / distance)),
                        b.Source.Position, MathF.Atan2(member.Position.X - b.Source.Position.X, member.Position.Z - b.Source.Position.Z),
                        Display: false, Activation: b.At);
                }
        }
    }

    private IEnumerable<DungeonAmbientAoe> AlexKnockbackAoes()
    {
        if (Arena == null) yield break;
        var casts = _casts.Values.Where(c => c.Rule.Action == 36794).ToArray();
        if (casts.Length == 0) yield break;
        var first = casts[0];
        yield return new("alex/kb/nav", "击退后留在场内", new VanaspatiKnockbackTemplate(
            new RectangleGeometry(Arena.Position, 0, 28, 28), first.Location, [], 15), Arena.Position,
            Display: false, Activation: first.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var start = player.Position;
        foreach (var cast in casts)
        {
            if (KnockbackImmune(player, cast.FinishAt)) continue;
            var landing = new VanaspatiKnockbackTemplate(Arena, cast.Location, [], 15).Landing(start);
            foreach (var aoe in DungeonMovementPreview($"alex/kb/{cast.Source}", "击退落点", start, landing))
                yield return aoe with { Safe = Arena.Contains(landing, 0) };
            start = landing;
        }
    }

    private IEnumerable<DungeonDamagePrediction> AlexDamage()
    {
        if (territory != AlexandriaCatalog.TERRITORY) yield break;
        var lateBaits = alexDisinfection.Count > 0 && alexDisinfection[0].At.AddSeconds(-2) < _now;
        if (lateBaits)
            foreach (var b in alexDisinfection)
                if (b.Target.PartySlot >= 0)
                    yield return new(b.Target.Id, b.Target.PartySlot, b.Target.PartyRole, b.Target.Name, alexDisinfection[0].At, "Tankbuster");
        var stacks = alexStacks.Where(s => s.Source == 0 && !s.Target.Dead).ToArray();
        // Icon stacks enable resolve predictions; cast stacks/spreads in this source do not.
        // Preserve its all-mask guard: an icon stack with no forbidden bit emits no Shared prediction.
        if (stacks.Length > 0 && stacks.Aggregate(ulong.MaxValue, (mask, s) => mask & s.Forbidden) != 0)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && stacks.Any(s => (s.Forbidden & (1UL << a.PartySlot)) == 0)))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, stacks.Min(s => s.At), "Shared");
        foreach (var resolve in new uint[] { 36780, 36787 })
        {
            var lines = alexLines.Where(b => b.Resolve == resolve && !b.Source.Dead).ToArray();
            if (lines.Length > 0)
                foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                    yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, lines.Min(b => b.At), "Shared");
        }
    }

    private IEnumerable<string> AlexHints()
    {
        if (territory != AlexandriaCatalog.TERRITORY) yield break;
        if (alexStacks.Any(s => !s.Target.Dead)) yield return Active?.BossOid == 0x4173 ? "三人分摊，死刑目标不要参与" : "四人分摊";
        if (alexDisinfection.Count > 0) yield return "死刑点名远离队友";
        if (alexLines.Count > 0) yield return "直线分摊";
        if (alexSalvation.Count > 0) yield return "直线诱导远离队友";
    }
}
