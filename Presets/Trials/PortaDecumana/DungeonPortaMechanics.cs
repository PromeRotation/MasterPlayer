using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(DungeonPendingCast Cast, DungeonActor Target)> portaParty = [];
    private bool IsPorta => territory == PortaCatalog.TERRITORY;
    private bool IsNativeTrial => IsArrTrial || IsPorta || IsHardTrial || IsThornmarch || IsWhorleater || IsSusano || IsLakshmi || IsShinryu || IsTsukuyomi || IsYojimbo || IsByakko || IsSuzaku || IsSeiryu || IsTitania || IsInnocence || IsHades || IsWarriorOfLight || IsHydaelyn || IsAsura || IsZelenia || IsValigarmanda || IsArkveld || IsEnuo || IsNecron || IsQueenEternal || IsZoraalJa || IsDoomtrain || IsCoilNodes || IsCoilGauntlet || IsCaduceus || IsTwintania || IsLabyrinth || IsSyrcus || IsDarkness || IsArrQuest || IsUltimaExtreme || IsGarudaExtreme || IsTitanExtreme || IsIfritExtreme;
    private bool PortaForbidden(DungeonActor actor)
        => IsPorta && Active?.BossOid == 0x38FB && actor.Id == PrimaryId
            && (HasStatus(actor, 325) || HasStatus(actor, 3012));
    private int PortaPriority(DungeonActor actor)
    {
        if (PortaForbidden(actor)) return 0;
        if (actor.Id == PrimaryId || actor.InCombat
            && _actors.GetValueOrDefault(actor.TargetId) is { PartySlot: >= 0 }) return 1;
        return 0;
    }
    internal bool WantsPortaKnockbackImmunity => IsPorta && _casts.Values.Any(c => c.Rule.Action == 29015);
    internal bool WantsPortaSprint => IsPorta && Active?.BossOid == 0x3900 && PortaOrbs().Any();
    internal string? PortaCountdown => IsPorta && _casts.Values.FirstOrDefault(c => c.Rule.Action == 29024) is { } cast
        ? $"究极狂暴：{Math.Max(0, (cast.FinishAt - _now).TotalSeconds):F1} 秒" : null;

    private IEnumerable<DungeonActor> PortaOrbs() => _actors.Values.Where(a => a.Oid == 0x3902 && !a.Dead
        && MathF.Abs(a.Rotation) > MathF.PI / 180);

    private void PortaCast(DungeonPendingCast cast)
    {
        if (!IsPorta || cast.Rule.Action is not (29012 or 29014) || !_actors.TryGetValue(cast.TargetId, out var target)) return;
        var index = portaParty.FindIndex(p => p.Cast.Source == cast.Source && p.Cast.StartedAt == cast.StartedAt);
        if (index < 0) portaParty.Add((cast, target));
        else portaParty[index] = (cast, target);
    }

    private void PortaObserve()
    {
        if (!IsPorta) return;
        for (var i = 0; i < portaParty.Count; ++i)
        {
            var p = portaParty[i];
            if (_actors.TryGetValue(p.Target.Id, out var current)) portaParty[i] = (p.Cast, current);
        }
    }

    private void PortaFinish(DungeonPendingCast cast)
    {
        if (!IsPorta || cast.Rule.Action is not (29012 or 29014)) return;
        var index = portaParty.FindIndex(p => p.Cast.Rule.Action == cast.Rule.Action && p.Target.Id == cast.TargetId);
        if (index >= 0) portaParty.RemoveAt(index);
    }

    private IEnumerable<DungeonAmbientAoe> PortaAoes()
    {
        if (!IsPorta || Active == null || Arena == null) yield break;
        var knockbacks = _casts.Values.Where(c => c.Rule.Action is 29003 or 29015).ToArray();
        if (knockbacks.FirstOrDefault(c => c.Rule.Action == 29003) is { } first)
            yield return new("porta/vulcan/join", "进入场地中心五码", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(Arena.Position, 5)]), Arena.Position, Display: false, Activation: first.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is { } player)
        {
            var start = player.Position;
            foreach (var cast in knockbacks)
            {
                if (KnockbackImmune(player, cast.FinishAt)) continue;
                var landing = new CastrumKnockbackTemplate(Arena, cast.Location, cast.Rule.Action == 29003 ? 15 : 30, []).Landing(start);
                foreach (var aoe in DungeonMovementPreview($"porta/knockback/{cast.Source}", "撞墙停止的击退落点", start, landing))
                    yield return aoe;
                start = landing;
            }
        }
        if (Active.BossOid != 0x3900) yield break;
        var cannonsActive = _casts.Values.Any(c => c.Rule.Action == 29019);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 29021))
            yield return new($"porta/explosion/{cast.Source}", "飞空艇爆炸", new CircleTemplate(16), cast.Location,
                Navigate: !cannonsActive, Activation: cast.FinishAt);
        foreach (var orb in _actors.Values.Where(a => a.Oid == 0x3902 && !a.Dead))
            yield return new($"porta/orb/{orb.Id}/actor", "以太球", new CircleTemplate(orb.HitboxRadius),
                orb.Position, Navigate: false, OutlineOnly: true);
        var orbs = PortaOrbs().ToArray();
        foreach (var orb in orbs)
            yield return new($"porta/orb/{orb.Id}/soak", "吸收以太球", new CircleTemplate(1),
                orb.Position, Navigate: false, Safe: true, OutlineOnly: true);
        foreach (var orb in orbs)
        {
            var center = orb.Position + new Vector3(MathF.Sin(orb.Rotation), 0, MathF.Cos(orb.Rotation)) * .5f;
            // 上游的 MaxValue 补集是远期吸球意图，映射到目标通道以保留小方形与真实危险的优先关系。
            yield return new($"porta/orb/join/{orb.Id}", "吸收以太球", new RectangleTemplate(1, 1),
                center, Display: false, Safe: true);
        }
        foreach (var aoe in PortaPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> PortaPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = portaParty.Where(p => p.Cast.Rule.Action == 29012 && !p.Target.Dead).ToArray();
        var stacks = portaParty.Where(p => p.Cast.Rule.Action == 29014 && !p.Target.Dead).ToArray();
        foreach (var (cast, target) in spreads)
        {
            yield return new($"porta/spread/{cast.Source}/display", "六码分散", new CircleTemplate(6), target.Position,
                Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"porta/spread/{cast.Source}/avoid", "分散间距七码", new CircleTemplate(7), target.Position,
                    Display: false, Activation: cast.FinishAt);
            else
                foreach (var member in party.Where(a => a.Id != PlayerId && !spreads.Any(s => s.Target.Id == a.Id)))
                    yield return new($"porta/spread/{cast.Source}/{member.Id}", "分散远离无点名队友", new CircleTemplate(7),
                        member.Position, Display: false, Activation: cast.FinishAt);
        }
        var marked = stacks.Any(s => s.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (cast, target) in stacks)
        {
            var circle = new CircleGeometry(target.Position, 6);
            var inside = party.Count(p => circle.Contains(p.Position));
            var playerInside = _actors.GetValueOrDefault(PlayerId) is { } player && circle.Contains(player.Position);
            var safe = target.Id == PlayerId || !marked && (playerInside ? inside <= 4 : inside < 4);
            yield return new($"porta/stack/{cast.Source}/display", "四人分摊", new CircleTemplate(6), target.Position,
                Navigate: false, Safe: safe, OutlineOnly: true);
            if (target.Id == PlayerId)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"porta/stack/{cast.Source}/buddy", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
            else if (safe) joins.Add(circle);
            else
                yield return new($"porta/stack/{cast.Source}/avoid", "避免重叠或超员分摊", new CircleTemplate(marked ? 12 : 6),
                    target.Position, Display: false, Activation: cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("porta/stack/join", "进入四人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
    }

    private IEnumerable<string> PortaHints(Vector3? player)
    {
        if (!IsPorta || Active == null) yield break;
        if (WantsPortaKnockbackImmunity) yield return "准备吸收以太球";
        if (WantsPortaSprint) yield return "吸收以太球";
        if (PortaCountdown is { } countdown) yield return countdown;
        if (player is { } p && _casts.Values.Any(c => c.Rule.Action == 29021 && new CircleGeometry(c.Location, 16).Contains(p)))
            yield return "离开飞空艇爆炸";
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var (cast, target) in portaParty.Where(p => !p.Target.Dead))
        {
            if (cast.Rule.Action == 29014 && player is { } position)
            {
                var circle = new CircleGeometry(target.Position, 6);
                if (target.Id == PlayerId ? party.Count(a => circle.Contains(a.Position)) != 4 : !circle.Contains(position))
                    yield return "四人分摊";
            }
            else if (cast.Rule.Action == 29012 && (target.Id == PlayerId
                ? party.Any(a => a.Id != PlayerId && new CircleGeometry(target.Position, 6).Contains(a.Position))
                : player is { } spreadPosition && new CircleGeometry(target.Position, 6).Contains(spreadPosition)))
                yield return "分散远离队友";
        }
    }
}
