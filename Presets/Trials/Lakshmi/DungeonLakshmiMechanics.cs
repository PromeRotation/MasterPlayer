using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsLakshmi => territory == LakshmiCatalog.TERRITORY;
    internal ulong DutyActionPlayerId { get; set; }
    internal IReadOnlyList<ReplayDutyAction>? DutyActions { get; set; }
    private readonly List<(DungeonActor Target, uint Icon, DateTime At, int Key)> lakshmiBaits = [];
    private readonly List<(DungeonPendingCast Cast, DungeonActor Target)> lakshmiStacks = [];
    private bool lakshmiDenialCasting;
    private bool lakshmiShieldCasting;
    private bool lakshmiJagadishwari;
    private int lakshmiBaitKey;

    internal bool ConsumeLakshmiVrilRequest()
    {
        if (!IsLakshmi || Active == null) return false;
        var request = lakshmiDenialCasting || lakshmiShieldCasting || lakshmiJagadishwari;
        lakshmiJagadishwari = false;
        return request;
    }

    private void LakshmiCast(DungeonPendingCast cast)
    {
        if (!IsLakshmi) return;
        if (cast.Rule.Action == 9349) lakshmiDenialCasting = true;
        if (cast.Rule.Action == 9374) lakshmiShieldCasting = true;
        if (cast.Rule.Action == 9361 && _actors.TryGetValue(cast.TargetId, out var target))
        {
            var index = lakshmiStacks.FindIndex(s => s.Cast.Source == cast.Source && s.Cast.StartedAt == cast.StartedAt);
            if (index < 0) lakshmiStacks.Add((cast, target));
            else lakshmiStacks[index] = (cast, target);
        }
    }

    private void LakshmiFinish(DungeonPendingCast cast)
    {
        if (!IsLakshmi) return;
        if (cast.Rule.Action == 9349) lakshmiDenialCasting = false;
        if (cast.Rule.Action == 9374) lakshmiShieldCasting = false;
        if (cast.Rule.Action == 9361)
        {
            var index = lakshmiStacks.FindIndex(s => s.Target.Id == cast.TargetId);
            if (index >= 0) lakshmiStacks.RemoveAt(index);
        }
    }

    private void LakshmiIcon(ulong targetId, uint icon, DateTime at)
    {
        if (!IsLakshmi || Active == null || icon is not (14 or 107 or 109) || !_actors.TryGetValue(targetId, out var target)) return;
        // Path uses a float delay in the source; Blissful uses a double.
        lakshmiBaits.Add((target, icon, at.AddSeconds(icon == 14 ? (double)4.7f : 4.7d), ++lakshmiBaitKey));
    }

    private void LakshmiEffect(uint action)
    {
        if (!IsLakshmi) return;
        if (action == 9026) lakshmiJagadishwari = true;
        if (action is 9359 or 9377) lakshmiBaits.RemoveAll(b => b.Icon == 14);
        if (action is 9355 or 9356 or 9364 or 9365) lakshmiBaits.RemoveAll(b => b.Icon != 14);
    }

    private void LakshmiObserve()
    {
        if (!IsLakshmi) return;
        for (var i = 0; i < lakshmiBaits.Count; ++i)
        {
            var bait = lakshmiBaits[i];
            if (_actors.TryGetValue(bait.Target.Id, out var current)) lakshmiBaits[i] = (current, bait.Icon, bait.At, bait.Key);
        }
        for (var i = 0; i < lakshmiStacks.Count; ++i)
        {
            var stack = lakshmiStacks[i];
            if (_actors.TryGetValue(stack.Target.Id, out var current)) lakshmiStacks[i] = (stack.Cast, current);
        }
    }

    private IEnumerable<DungeonActor> LakshmiOrbs() => _actors.Values.Where(a => a.Oid == 0x1E21 && !a.Dead);
    private bool? LakshmiCharged => DutyActionPlayerId == PlayerId && DutyActions != null
        ? DutyActions.Any(a => a.Slot == 0 && a.CurrentCharges > 0) : null;

    private IEnumerable<DungeonAmbientAoe> LakshmiAoes()
    {
        if (!IsLakshmi || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1E20 or 0x1E22 or 0x1E21 && a.Targetable && !a.Dead))
            yield return new($"lakshmi/enemy/{actor.Id}", "吉祥天女及辅助体", new CircleTemplate(actor.HitboxRadius), actor.Position,
                Navigate: false, OutlineOnly: true, Color: actor.Oid == 0x1E21 ? new Vector4(1, 128f / 255, 0, 1) : null);
        if (_actors.GetValueOrDefault(PrimaryId) is { Targetable: true } primary)
            yield return new("lakshmi/primary", "吉祥天女", new CircleTemplate(primary.HitboxRadius),
                Arena.Position - Vector3.UnitZ * 20, Navigate: false, OutlineOnly: true);
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EA76C))
            yield return new($"lakshmi/zone/{zone.Id}", "固定十码水圈", new CircleTemplate(10), zone.Position, Obstacle: true);
        foreach (var orb in LakshmiOrbs())
        {
            yield return new($"lakshmi/orb/{orb.Id}/display", "元气球", new CircleTemplate(.75f), orb.Position, Navigate: false, Safe: true);
            if (LakshmiCharged == false && _actors.GetValueOrDefault(PlayerId) is { } player && !HasStatus(player, 1290))
            {
                var center = orb.Position + new Vector3(MathF.Sin(orb.Rotation), 0, MathF.Cos(orb.Rotation)) * .5f;
                yield return new($"lakshmi/orb/{orb.Id}/goal", "补充元气任务技", new RectangleTemplate(1, 1), center, Display: false, Safe: true);
            }
        }
        foreach (var aoe in LakshmiBaitAoes()) yield return aoe;
        foreach (var aoe in LakshmiStackAoes()) yield return aoe;
        foreach (var aoe in LakshmiGazeAoes()) yield return aoe;
        foreach (var (source, from, to) in LakshmiKnockbacks())
            foreach (var aoe in TrialKnockbackDrawing("lakshmi/knockback/" + source, "圣天击退落点", from, to))
                yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> LakshmiGazeAoes()
    {
        var player = _actors.GetValueOrDefault(PlayerId);
        foreach (var gaze in _casts.Values.Where(c => c.Rule.Action == 9613))
        {
            var delta = player == null ? Vector2.Zero : new Vector2(gaze.Location.X - player.Position.X, gaze.Location.Z - player.Position.Z);
            var facing = player == null ? Vector2.Zero : new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
            var danger = delta.LengthSquared() > 0 && Vector2.Dot(facing, Vector2.Normalize(delta)) >= .707107f;
            var color = danger ? new Vector4(1, .2f, .2f, 1) : new Vector4(.2f, .8f, 1, 1);
            var lens = new List<Vector2>();
            for (var i = 0; i <= 16; ++i)
            {
                var x = -.7f + 1.4f * i / 16;
                lens.Add(new(x, .35f * (1 - x * x / .49f)));
            }
            for (var i = 15; i > 0; --i)
            {
                var x = -.7f + 1.4f * i / 16;
                lens.Add(new(x, -.35f * (1 - x * x / .49f)));
            }
            yield return new($"lakshmi/gaze/{gaze.Source}/eye", "背对视线", new LocalPolygonTemplate(lens),
                gaze.Location, Navigate: false, Safe: !danger, Color: color);
            yield return new($"lakshmi/gaze/{gaze.Source}/pupil", "视线眼睛", new CircleTemplate(.18f),
                gaze.Location, Navigate: false, Color: new Vector4(0, 0, 0, 1));
            if (player != null)
                yield return new($"lakshmi/gaze/{gaze.Source}/facing", "当前角色面向",
                    new Mechanics.PilgrimsTraverse.PtDonutSectorTemplate(.98f, 1.02f, MathF.PI / 2),
                    player.Position, player.Rotation, Navigate: false, Color: new Vector4(1, .2f, .2f, 1));
        }
    }

    private IEnumerable<DungeonAmbientAoe> LakshmiBaitAoes()
    {
        if (_actors.GetValueOrDefault(PrimaryId) is not { } boss) yield break;
        foreach (var bait in lakshmiBaits.Where(b => b.Icon == 14 ? !boss.Dead : !b.Target.Dead))
        {
            var position = bait.Icon == 14 ? boss.Position : bait.Target.Position;
            var rotation = bait.Icon switch
            {
                14 => MathF.Atan2(bait.Target.Position.X - boss.Position.X, bait.Target.Position.Z - boss.Position.Z),
                107 => MathF.PI,
                _ => bait.Target.Rotation,
            };
            GeometryTemplate shape = bait.Icon switch
            {
                14 => new FanTemplate(40, MathF.PI * 75 / 180),
                107 => new DungeonCrossTemplate(40, 4),
                _ => new CircleTemplate(7),
            };
            yield return new($"lakshmi/bait/{bait.Key}/display", "光之引导", shape, position, rotation,
                Navigate: false, OutlineOnly: bait.Target.Id == PlayerId);
            if (bait.Target.Id != PlayerId)
                yield return new($"lakshmi/bait/{bait.Key}/avoid", "避开他人引导", shape, position, rotation,
                    Display: false, Activation: bait.At);
            else if (bait.Icon == 14)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"lakshmi/bait/{bait.Key}/{buddy.Id}", "扇形引导远离队友", new FanTemplate(100, MathF.PI * 75 / 180),
                        boss.Position, MathF.Atan2(buddy.Position.X - boss.Position.X, buddy.Position.Z - boss.Position.Z),
                        Display: false, Activation: bait.At);
        }
    }

    private IEnumerable<DungeonAmbientAoe> LakshmiStackAoes()
    {
        if (Arena == null) yield break;
        var stacks = lakshmiStacks.Where(s => !s.Target.Dead).ToArray();
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var marked = stacks.Any(s => s.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (cast, target) in stacks)
        {
            var position = QuantizedDungeonPosition(target.Position);
            var circle = new CircleGeometry(position, 7);
            var count = party.Count(a => circle.Contains(a.Position));
            var inside = _actors.GetValueOrDefault(PlayerId) is { } player && circle.Contains(player.Position);
            var canJoin = inside ? count <= 8 : count < 8;
            yield return new($"lakshmi/stack/{cast.Source}/display", "八人分摊", new CircleTemplate(7), position,
                Navigate: false, OutlineOnly: true, Safe: target.Id == PlayerId || !marked && canJoin);
            if (party.Length < 2) continue;
            if (target.Id == PlayerId)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3.5f)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"lakshmi/stack/{cast.Source}/buddy", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
            else if (canJoin) joins.Add(circle);
            else
                yield return new($"lakshmi/stack/{cast.Source}/avoid", "避免超员分摊", new CircleTemplate(marked ? 14 : 7), position,
                    Display: false, Activation: cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("lakshmi/stack/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
    }

    private IEnumerable<(ulong Source, Vector3 From, Vector3 To)> LakshmiKnockbacks()
    {
        if (!IsLakshmi || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var position = player.Position;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 9349))
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var delta = position - cast.Location; delta.Y = 0;
            if (delta.LengthSquared() == 0) continue;
            var next = position + Vector3.Normalize(delta) * 6.5f;
            yield return (cast.Source, position, next);
            position = next;
        }
    }

    private IEnumerable<string> LakshmiHints()
    {
        if (!IsLakshmi || Active == null) yield break;
        if (LakshmiOrbs().Any()) yield return LakshmiCharged == null ? "任务技充能未知：确认元气后取球" : "元气任务技未充能时取一个球";
        if (lakshmiDenialCasting || lakshmiShieldCasting || lakshmiJagadishwari) yield return "使用元气护盾";
        if (Arena != null && LakshmiKnockbacks().Any(k => !Arena.Contains(k.To))) yield return "击退落点将出界";
        if (lakshmiBaits.Any(b => b.Icon == 14)) yield return "扇形死刑引导";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var (cast, target) in lakshmiStacks.Where(s => !s.Target.Dead))
        {
            var circle = new CircleGeometry(QuantizedDungeonPosition(target.Position), 7);
            if (target.Id == PlayerId
                ? _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && circle.Contains(a.Position)) != 8
                    || lakshmiStacks.Any(s => s.Target.Id != target.Id && !s.Target.Dead && circle.Contains(s.Target.Position))
                : !circle.Contains(player.Position))
                yield return "八人分摊";
        }
        foreach (var aoe in LakshmiBaitAoes().Where(a => a.Display))
        {
            var bait = lakshmiBaits.Single(b => aoe.Id == $"lakshmi/bait/{b.Key}/display");
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            if (bait.Target.Id == PlayerId)
            {
                if (_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position)))
                    yield return "引导远离队友";
            }
            else if (shape.Contains(player.Position)) yield return "避开他人引导";
        }
    }

    private void ResetLakshmi()
    {
        lakshmiBaits.Clear(); lakshmiStacks.Clear(); lakshmiDenialCasting = false;
        lakshmiShieldCasting = false; lakshmiJagadishwari = false; lakshmiBaitKey = 0;
    }
}
