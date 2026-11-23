using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTsukuyomi => territory == TsukuyomiCatalog.TERRITORY;
    private readonly List<DungeonPendingCast> tsukuyomiCasts = [];
    private readonly List<DungeonPendingCast> tsukuyomiLunacy = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, int Key)> tsukuyomiLines = [];
    private readonly Dictionary<ulong, DungeonActor> tsukuyomiActors = [];
    private int tsukuyomiLineKey;

    private void TsukuyomiCast(DungeonPendingCast cast)
    {
        if (!IsTsukuyomi) return;
        var index = tsukuyomiCasts.FindIndex(c => c.Source == cast.Source);
        if (index < 0) tsukuyomiCasts.Add(cast);
        else tsukuyomiCasts[index] = cast;
        if (cast.Rule.Action == 11259 && _actors.ContainsKey(cast.TargetId))
        {
            var stackIndex = tsukuyomiLunacy.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (stackIndex < 0) tsukuyomiLunacy.Add(cast);
            else tsukuyomiLunacy[stackIndex] = cast;
        }
    }

    private void TsukuyomiFinish(DungeonPendingCast cast)
    {
        if (!IsTsukuyomi) return;
        tsukuyomiCasts.RemoveAll(c => c.Source == cast.Source);
        if (cast.Rule.Action == 11259)
        {
            var index = tsukuyomiLunacy.FindIndex(c => c.TargetId == cast.TargetId);
            if (index >= 0) tsukuyomiLunacy.RemoveAt(index);
        }
    }

    private void TsukuyomiEffect(ulong source, uint action, ulong target, DateTime at)
    {
        if (!IsTsukuyomi || Active == null) return;
        if (action == 11441 && _actors.TryGetValue(source, out var caster) && _actors.TryGetValue(target, out var marked))
            tsukuyomiLines.Add((caster, marked, at.AddSeconds(5), ++tsukuyomiLineKey));
        if (action != 11238) return;
        // LineStack can correct the final target only when exactly one marker remains.
        if (tsukuyomiLines.Count == 1 && tsukuyomiLines[0].Target.Id != target && _actors.TryGetValue(target, out var finalTarget))
        {
            var line = tsukuyomiLines[0];
            tsukuyomiLines[0] = (line.Source, finalTarget, line.At, line.Key);
        }
        var index = tsukuyomiLines.FindIndex(b => b.Target.Id == target);
        if (index >= 0) tsukuyomiLines.RemoveAt(index);
    }

    private void TsukuyomiObserve()
    {
        if (!IsTsukuyomi || Active == null) return;
        foreach (var removed in tsukuyomiActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            if (_casts.TryGetValue(removed, out var cast)) TsukuyomiFinish(cast);
            TsukuyomiRemoveMoon(removed);
            tsukuyomiActors.Remove(removed);
        }
        foreach (var actor in _actors.Values) tsukuyomiActors[actor.Id] = actor;
        for (var i = 0; i < tsukuyomiLines.Count; ++i)
        {
            var line = tsukuyomiLines[i];
            tsukuyomiLines[i] = (_actors.GetValueOrDefault(line.Source.Id) ?? line.Source,
                _actors.GetValueOrDefault(line.Target.Id) ?? line.Target, line.At, line.Key);
        }
        TsukuyomiObserveMoon();
    }

    private IEnumerable<DungeonAmbientAoe> TsukuyomiAoes()
    {
        if (!IsTsukuyomi || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x2210 || TsukuyomiCatalog.IsAdd(a.Oid))
            .Where(a => a.Targetable && !a.Dead))
            yield return new($"tsukuyomi/enemy/{actor.Id}", "月读及幻影", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        foreach (var cast in tsukuyomiCasts.Where(c => c.Rule.Action == 11245).Take(7))
            yield return new($"tsukuyomi/fan/{cast.Source}", "座敷游扇", new CircleTemplate(10),
                cast.Location, Activation: cast.FinishAt);
        foreach (var cast in tsukuyomiCasts.Where(c => c.Rule.Action == 11235))
        {
            if (tsukuyomiActors.GetValueOrDefault(cast.Source) is not { Dead: false } source
                || tsukuyomiActors.GetValueOrDefault(cast.TargetId) is not { } target) continue;
            var angle = source.Id == target.Id ? source.Rotation
                : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var own = target.Id == PlayerId;
            var shape = new FanTemplate(15, MathF.PI * 75 / 180);
            yield return new($"tsukuyomi/torment/{cast.Source}/display", "黄泉之枪引导", shape,
                source.Position, angle, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new($"tsukuyomi/torment/{cast.Source}/avoid", "避开黄泉之枪", shape,
                    source.Position, angle, Display: false, Activation: cast.FinishAt);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"tsukuyomi/torment/{cast.Source}/buddy/{buddy.Id}", "引导远离队友",
                        new FanTemplate(100, MathF.PI * 75 / 180), source.Position,
                        MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z),
                        Display: false, Activation: cast.FinishAt);
        }
        foreach (var aoe in TsukuyomiLineAoes()) yield return aoe;
        foreach (var aoe in TsukuyomiLunacyAoes()) yield return aoe;
        foreach (var aoe in TsukuyomiMoonAoes()) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> TsukuyomiDamage()
    {
        if (!IsTsukuyomi) yield break;
        var casts = tsukuyomiCasts.Where(c => c.Rule.Action == 11235
            && tsukuyomiActors.GetValueOrDefault(c.Source) is { Dead: false }).ToArray();
        if (casts.Length > 0)
            foreach (var id in casts.Select(c => c.TargetId).Distinct())
                if (tsukuyomiActors.GetValueOrDefault(id) is { PartySlot: >= 0 } actor)
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, casts[0].FinishAt, "Raidwide");
        var lines = tsukuyomiLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, lines.Min(l => l.At), "Shared");
    }

    private IEnumerable<string> TsukuyomiHints()
    {
        if (!IsTsukuyomi || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        if (tsukuyomiCasts.Any(c => c.Rule.Action == 11235)) yield return "扇形死刑";
        foreach (var aoe in TsukuyomiAoes().Where(a => a.Id.Contains("/torment/") && a.Id.EndsWith("/display")))
        {
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            if (aoe.OutlineOnly ? party.Any(a => a.Id != PlayerId && shape.Contains(a.Position)) : shape.Contains(player.Position))
                yield return aoe.OutlineOnly ? "引导远离队友" : "避开黄泉之枪";
        }
        var lines = tsukuyomiLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0 && !lines.Any(l => l.Target.Id == PlayerId)
            && !TsukuyomiLineAoes().Where(a => a.Display).Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "进入直线分摊";
        if (lines.Any(l => l.Target.Id == PlayerId)
            && TsukuyomiLineAoes().Where(a => a.Display && !a.Safe).Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "远离其他直线分摊";
        foreach (var cast in tsukuyomiLunacy)
            if (tsukuyomiActors.GetValueOrDefault(cast.TargetId) is { Dead: false } target)
            {
                var circle = new CircleGeometry(QuantizedDungeonPosition(target.Position), 6);
                var count = party.Count(a => circle.Contains(a.Position));
                if (target.Id == PlayerId ? count != 8 || tsukuyomiLunacy.Any(c => c.TargetId != target.Id
                        && tsukuyomiActors.GetValueOrDefault(c.TargetId) is { Dead: false } other && circle.Contains(other.Position))
                    : !circle.Contains(player.Position) && count < 8)
                    yield return "八人分摊";
            }
    }

    private void ResetTsukuyomi()
    {
        tsukuyomiCasts.Clear(); tsukuyomiLines.Clear(); tsukuyomiActors.Clear(); tsukuyomiLineKey = 0;
        tsukuyomiLunacy.Clear();
        tsukuyomiMoonStates.Clear(); tsukuyomiMoonRaw.Clear(); tsukuyomiMoonPending.Clear();
    }
}
