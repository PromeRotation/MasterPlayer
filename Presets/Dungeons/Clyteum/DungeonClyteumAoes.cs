using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ClyAoes()
    {
        if (territory != ClyteumCatalog.TERRITORY || Arena == null) yield break;
        var beams = clySimple.Where(c => ClySimpleGroup(c.Rule.Action) == 1).Take(2).ToArray();
        foreach (var c in clySimple)
        {
            if (ClySimpleGroup(c.Rule.Action) == 1 && !beams.Contains(c)) continue;
            yield return new($"cly/simple/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation, Activation: c.FinishAt);
        }
        if (clyBeam != null)
            yield return new("cly/beam/display", "扫描光束：停手并停止移动", new RectangleTemplate(40, 18),
                clyBeam.Position, clyBeam.Rotation, Navigate: false);
        if (clyTowers.MinBy(v => v.FinishAt) is { } tower)
        {
            yield return new("cly/tower/display", "四人顺序踏塔", new CircleTemplate(4), tower.Location,
                Navigate: false, Safe: true, Activation: tower.FinishAt);
            yield return new("cly/tower/join", "进入下一座塔", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(tower.Location, 4)]),
                Arena.Position, Display: false, Activation: tower.FinishAt);
        }
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = clyParty.Where(v => v.Cast.Rule.PartyAoe == "分散").Select(v =>
            (v.Target, At: v.Cast.FinishAt, Radius: ((CircleTemplate)v.Cast.Rule.Shape!).Radius, Key: $"{v.Cast.Source}/{v.Cast.StartedAt.Ticks}", Component: v.Cast.Rule.Action))
            .Concat(clyIconSpreads.Select(v => (v.Target, v.At, Radius: 4f, Key: $"icon/{v.Serial}", Component: 48885u))).Where(v => !v.Target.Dead).ToArray();
        foreach (var s in spreads)
        {
            var key = $"cly/spread/{s.Key}";
            yield return new(key + "/display", "分散", new CircleTemplate(s.Radius), QuantizedDungeonPosition(s.Target.Position),
                Navigate: false, OutlineOnly: true, Activation: s.At);
            if (s.Target.Id != PlayerId)
                yield return new(key + "/avoid", "避开分散", new CircleTemplate(s.Radius + 1), QuantizedDungeonPosition(s.Target.Position),
                    Display: false, Activation: s.At);
            else
                foreach (var other in party.Where(a => a.Id != PlayerId && !spreads.Any(v => v.Component == s.Component && v.Target.Id == a.Id)))
                    yield return new(key + $"/separate/{other.Id}", "分散远离队友", new CircleTemplate(s.Radius + 1),
                        QuantizedDungeonPosition(other.Position), Display: false, Activation: s.At);
        }
        foreach (var (target, c) in clyParty.Where(v => v.Cast.Rule.PartyAoe == "分摊" && !v.Target.Dead))
        {
            var key = $"cly/stack/{c.Source}/{c.StartedAt.Ticks}";
            yield return new(key + "/display", "四人分摊", new CircleTemplate(6), QuantizedDungeonPosition(target.Position),
                Navigate: false, Safe: true, Activation: c.FinishAt);
            if (target.Id != PlayerId)
                yield return new(key + "/join", "进入分摊", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(QuantizedDungeonPosition(target.Position), 6)]),
                    Arena.Position, Display: false, Activation: c.FinishAt);
            else
            {
                var buddies = party.Where(a => a.Id != PlayerId && !clyParty.Any(v => v.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(QuantizedDungeonPosition(a.Position), 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new(key + "/gather", "分摊靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: c.FinishAt);
            }
        }
        if (clyKbs.Count > 0 && _actors.TryGetValue(PlayerId, out var player))
        {
            var kb = clyKbs[0];
            if (!KnockbackImmune(player, kb.FinishAt))
                yield return new("cly/kb/avoid", "击退前进入原点七码内", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(kb.Location, 7)]),
                    Arena.Position, Display: false, Activation: kb.FinishAt);
            var dest = player.Position;
            foreach (var c in clyKbs)
            {
                if (KnockbackImmune(player, c.FinishAt)) continue;
                var delta = dest - c.Location; delta.Y = 0;
                if (delta.LengthSquared() > 0) dest += Vector3.Normalize(delta) * 8;
            }
            if (dest != player.Position)
                foreach (var a in DungeonMovementPreview("cly/kb", "八码击退落点", player.Position, dest))
                    yield return a with { Color = Arena.Contains(dest, 0) ? new Vector4(.2f, 1, .3f, 1) : new Vector4(1, .1f, .1f, 1) };
        }
        if (RequireClyteumMovement && clyMoveAnchor is { } anchor)
            yield return new("cly/move", "保持移动", new CircleTemplate(.8f), anchor, Display: false);
    }

    private IEnumerable<DungeonDamagePrediction> ClyDamage()
    {
        if (territory != ClyteumCatalog.TERRITORY) yield break;
        foreach (var a in clyIconSpreads.Select(v => v.Target).DistinctBy(a => a.Id).Where(a => !a.Dead && a.PartySlot >= 0))
            yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, clyIconSpreads.Min(v => v.At), "Raidwide");
        if (clyTowers.MinBy(v => v.FinishAt) is { } tower)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && new CircleGeometry(tower.Location, 4).Contains(a.Position, 0)))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, tower.FinishAt, "Raidwide");
    }

    private IEnumerable<string> ClyHints()
    {
        if (territory != ClyteumCatalog.TERRITORY) yield break;
        if (clyBeam != null) yield return "扫描光束内停止一切动作";
        if (clyMove.Count > 0) yield return "保持移动";
        if (clyTowers.Count > 0) yield return "按结算顺序四人踏塔";
    }
}
