using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTtokrrone => territory == NativeFateCatalog.TTOKRRONE;
    private readonly List<TtokrroneAoe> ttokrroneTempest = [];
    private readonly List<TtokrroneAoe> ttokrroneSpout = [];
    private readonly List<TtokrroneAoe> ttokrroneSwallow = [];
    private readonly List<TtokrroneAoe> ttokrroneSpheres = [];
    private readonly List<TtokrroneAoe> ttokrroneRotationDisplay = [];
    private TtokrroneRotation? ttokrroneRotation;
    private TtokrroneAoe? ttokrroneDustcloak;
    private long ttokrroneAoeId;
    private int ttokrroneRotationVersion;
    private int ttokrroneDisplayVersion;
    private bool ttokrroneDisplayHasRotation;

    private void TtokrroneCast(DungeonPendingCast cast, bool correction, DungeonPendingCast? previous)
    {
        if (!IsTtokrrone) return;
        var action = cast.Rule.Action;
        if (action is >= 37331 and <= 37334)
        {
            GeometryTemplate circle = new CircleTemplate(19);
            GeometryTemplate donut = new DonutTemplate(14, 60);
            GeometryTemplate cone = new FanTemplate(19, MathF.PI);
            GeometryTemplate sector = new PtDonutSectorTemplate(14, 60, MathF.PI);
            var first = action switch { 37331 => circle, 37332 => donut, 37333 => cone, _ => sector };
            Add(ttokrroneTempest, first, cast.FinishAt.AddSeconds(1), MathF.PI / 2);
            if (action is 37333 or 37334)
                Add(ttokrroneTempest, action == 37333 ? sector : cone, cast.FinishAt.AddSeconds(1), -MathF.PI / 2, stage: 1);
        }
        if (action is >= 37313 and <= 37316)
        {
            var offset = action switch { 37314 => MathF.PI, 37316 => MathF.PI / 2, 37315 => -MathF.PI / 2, _ => 0 };
            Add(ttokrroneSpout, new FanTemplate(60, MathF.PI / 2), cast.FinishAt.AddSeconds((double)6.1f), offset);
            Add(ttokrroneSpout, new CircleTemplate(13), cast.FinishAt.AddSeconds((double)6.1f), stage: 1, fixedRotation: true);
        }
        if (action is >= 37578 and <= 37583)
        {
            Add(ttokrroneSwallow, RayRectangleTemplate.Symmetric(27, 68), cast.FinishAt.AddSeconds(.4));
            if (!correction && ttokrroneSwallow.Count == 6) DungeonAoeOrder.Sort(ttokrroneSwallow, a => a.Activation);
        }
        if (action is 38647 or 39245 or 39246)
            Add(ttokrroneSpheres, new CircleTemplate(action == 38647 ? 6 : 12), cast.FinishAt, fixedRotation: true);
        if (action is not (37317 or 37321 or 37318 or 37322)) return;
        var increment = action is 37317 or 37318 ? -MathF.PI / 2 : MathF.PI / 2;
        var rotation = cast.Rotation + (action is 37318 or 37322 ? MathF.PI : 0);
        if (correction)
        {
            if (ttokrroneRotation is not { } old || !ReferenceEquals(old.Cast, previous)) return;
            ttokrroneRotation = old with { Cast = cast, Origin = cast.Location,
                Rotation = rotation + old.Consumed * increment, Increment = increment };
            if (old.Consumed == 0)
            {
                ttokrroneRotation = ttokrroneRotation with { NextActivation = cast.FinishAt.AddSeconds(.9) };
                if (ttokrroneDustcloak != null)
                    ttokrroneDustcloak = ttokrroneDustcloak with { Cast = cast, Position = cast.Location,
                        Activation = cast.FinishAt.AddSeconds(.9) };
            }
            // The source only rebuilds published cones when sequence count or consumed count changes.
            return;
        }
        var total = ttokrroneRotation == null ? 7 : 4;
        ttokrroneRotation = new(cast, cast.Location, rotation, increment, cast.FinishAt.AddSeconds(.9), total, total);
        ttokrroneDustcloak = new(++ttokrroneAoeId, cast, new CircleTemplate(13), cast.Location, 0,
            cast.FinishAt.AddSeconds(.9));

        void Add(List<TtokrroneAoe> list, GeometryTemplate shape, DateTime at, float offset = 0,
            int stage = 0, bool fixedRotation = false)
        {
            var index = correction ? list.FindIndex(a => ReferenceEquals(a.Cast, previous) && a.Stage == stage) : -1;
            if (correction && index < 0) return;
            var old = index >= 0 ? list[index] : null;
            var next = new TtokrroneAoe(old?.Id ?? ++ttokrroneAoeId, cast, shape, cast.Location,
                fixedRotation ? 0 : cast.Rotation + offset, at, stage, old?.DangerColor ?? false);
            if (index >= 0) list[index] = next;
            else list.Add(next);
        }
    }

    private void TtokrroneFinish(DungeonPendingCast cast)
    {
        if (!IsTtokrrone) return;
        if (cast.Rule.Action is 37328 or 37329 or 37335 or 37336 or 37337 or 37338) ttokrroneTempest.Clear();
        if (cast.Rule.Action is 38647 or 39245 or 39246)
        {
            var index = ttokrroneSpheres.FindIndex(a => a.Cast.Source == cast.Source);
            if (index >= 0) ttokrroneSpheres.RemoveAt(index);
        }
        if (cast.Rule.Action is not (39818 or 39817 or 39819 or 39820) || ttokrroneRotation is not { } rotation) return;
        ++ttokrroneRotationVersion;
        if (rotation.Remaining == 1)
        {
            ttokrroneRotation = null;
            ttokrroneDustcloak = null;
            return;
        }
        ttokrroneRotation = rotation with
        {
            Rotation = rotation.Rotation + rotation.Increment,
            NextActivation = _now.AddSeconds(TtokrroneRotation.INTERVAL),
            Remaining = rotation.Remaining - 1, Consumed = rotation.Consumed + 1,
        };
        ttokrroneDustcloak = new(++ttokrroneAoeId, cast, new CircleTemplate(13), cast.Location, 0, _now.AddSeconds(2.6));
    }

    private void TtokrroneEffect(uint action)
    {
        if (!IsTtokrrone) return;
        if (action is 39814 or 39843 or 39816 or 39813) ttokrroneSpout.Clear();
        if (action is >= 38642 and <= 38646 && ttokrroneSwallow.Count > 0) ttokrroneSwallow.RemoveAt(0);
    }

    private void TtokrroneUpdateRotation()
    {
        var hasRotation = ttokrroneRotation != null;
        if (ttokrroneDisplayHasRotation == hasRotation && ttokrroneDisplayVersion == ttokrroneRotationVersion) return;
        ttokrroneDisplayHasRotation = hasRotation;
        ttokrroneDisplayVersion = ttokrroneRotationVersion;
        ttokrroneRotationDisplay.Clear();
        if (ttokrroneRotation is not { } rotation) return;
        var shape = new FanTemplate(60, MathF.PI / 2);
        // The source caches future before imminent, rebuilding only when sequence count or completed casts change.
        if (rotation.Remaining > 1)
            ttokrroneRotationDisplay.Add(new(++ttokrroneAoeId, rotation.Cast, shape, rotation.Origin,
                rotation.Rotation + rotation.Increment,
                (rotation.NextActivation > _now ? rotation.NextActivation : _now).AddSeconds(TtokrroneRotation.INTERVAL),
                rotation.Consumed + 1));
        ttokrroneRotationDisplay.Add(new(++ttokrroneAoeId, rotation.Cast, shape, rotation.Origin, rotation.Rotation,
            rotation.NextActivation, rotation.Consumed, rotation.Remaining > 1));
    }

    private IEnumerable<DungeonAmbientAoe> TtokrroneAoes()
    {
        if (!IsTtokrrone || Active == null) yield break;
        TtokrroneUpdateRotation();
        if (ttokrroneSwallow.Count > 1) ttokrroneSwallow[0] = ttokrroneSwallow[0] with { DangerColor = true };
        var spheres = ttokrroneSpheres.TakeWhile(a => a.Activation < ttokrroneSpheres[0].Activation.AddSeconds(4.5));
        var all = ttokrroneTempest.Concat(ttokrroneSpout).Concat(ttokrroneSwallow).Concat(spheres)
            .Concat(ttokrroneDustcloak == null ? [] : new[] { ttokrroneDustcloak }).Concat(ttokrroneRotationDisplay);
        foreach (var aoe in all)
            yield return new($"ttokrrone/aoe/{aoe.Id}", aoe.Cast.Rule.Name, aoe.Shape, aoe.Position, aoe.Rotation,
                Activation: aoe.Activation, Color: aoe.DangerColor ? new(1, 1, 0, 1) : null);
    }

    private IEnumerable<string> TtokrroneHints(Vector3 player)
    {
        if (TtokrroneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player, 0)))
            yield return "GTFO from aoe!";
    }

    private void ResetTtokrrone()
    {
        ttokrroneTempest.Clear();
        ttokrroneSpout.Clear();
        ttokrroneSwallow.Clear();
        ttokrroneSpheres.Clear();
        ttokrroneRotationDisplay.Clear();
        ttokrroneRotation = null;
        ttokrroneDustcloak = null;
        ttokrroneAoeId = 0;
        ttokrroneRotationVersion = 0;
        ttokrroneDisplayVersion = 0;
        ttokrroneDisplayHasRotation = false;
    }
}
