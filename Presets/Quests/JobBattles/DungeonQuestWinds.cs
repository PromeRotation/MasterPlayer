using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonPendingCast> questWindStarted = [];
    private readonly Dictionary<ulong, DungeonCastObservation> questWindObserved = [];
    internal IReadOnlyList<ReplayPartyMember> CurrentPartyMembers { get; set; } = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DungeonPendingCast Cast, bool TargetRemoved)> foxshotBaits = [];
    private (DungeonPendingCast Cast, Vector3 Origin)? questWindKnockback;
    private bool IsGamboling => territory == JobQuestCatalog.GAMBOLING_FOR_GIL;
    private bool IsRaisingSword => territory == JobQuestCatalog.RAISING_THE_SWORD;
    private uint QuestWindOid => IsGamboling ? 0x29D5u : 0x1B52u;
    private bool QuestWindAction(uint action) => IsGamboling && action == 17289 || IsRaisingSword && action == 8136;
    private bool QuestWindVisible(DungeonActor actor) => IsGamboling ? !actor.Dead : actor.EventState != 7;

    private void QuestWindObserveCast(ulong source, DungeonCastObservation cast)
    {
        if (!IsOtis && !QuestWindAction(cast.Action) && !((IsBarPassage || IsCarnivale || IsMainScenarioQuest || IsHeroesPretenders || IsDikaiosyne) && Active?.Casts.Any(r => r.Action == cast.Action) == true)) return;
        var changed = questWindObserved.TryGetValue(source, out var old) && old.Action == cast.Action
            && Math.Abs((old.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((old.FinishAt - cast.FinishAt).TotalSeconds) < .75
            && (old.Location != cast.Location || old.Rotation != cast.Rotation || old.TargetId != cast.TargetId
                || (old.TotalTime is { } previousTotal && cast.TotalTime is { } total
                    ? previousTotal != total
                    : Math.Abs(((old.FinishAt - old.StartedAt) - (cast.FinishAt - cast.StartedAt)).TotalSeconds) > .00001));
        questWindObserved[source] = cast;
        if (!changed || !_casts.TryGetValue(source, out var current)) return;
        // Real CastInfo changes emit Finished then Started upstream; a duplicate packet does not.
        Finish(source, current.Rule.Action, _now);
        _finished.Remove(source);
    }

    private void QuestWindCast(DungeonPendingCast cast)
    {
        if (!QuestWindAction(cast.Rule.Action) || !_actors.TryGetValue(cast.Source, out var source)) return;
        if (questWindStarted.TryGetValue(cast.Source, out var previous)
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            if (questWindKnockback is { } kb && kb.Cast.Source == cast.Source)
                questWindKnockback = (cast, kb.Origin);
            var index = foxshotBaits.FindIndex(b => ReferenceEquals(b.Cast, previous));
            if (index >= 0)
            {
                var bait = foxshotBaits[index];
                if (previous.TargetId != cast.TargetId)
                {
                    if (_actors.TryGetValue(cast.TargetId, out var target))
                        foxshotBaits[index] = (bait.Source, target, cast, false);
                    else
                        foxshotBaits.RemoveAt(index);
                }
                else
                    foxshotBaits[index] = (bait.Source, bait.Target, cast, bait.TargetRemoved);
            }
            else if (IsGamboling && _actors.TryGetValue(cast.TargetId, out var target))
                foxshotBaits.Add((source, target, cast, false));
        }
        else
        {
            questWindKnockback = (cast, QuantizedDungeonPosition(source.Position));
            if (IsGamboling && _actors.TryGetValue(cast.TargetId, out var target))
                foxshotBaits.Add((source, target, cast, false));
        }
        questWindStarted[cast.Source] = cast;
    }

    private void QuestWindFinish(DungeonPendingCast cast)
    {
        if (!QuestWindAction(cast.Rule.Action)) return;
        questWindKnockback = null;
        questWindStarted.Remove(cast.Source);
        var index = foxshotBaits.FindIndex(b => b.Source.Id == cast.Source);
        if (index >= 0) foxshotBaits.RemoveAt(index);
    }

    private void QuestWindRemoved(ulong id)
    {
        questWindObserved.Remove(id);
        for (var i = 0; i < foxshotBaits.Count; ++i)
        {
            var bait = foxshotBaits[i];
            if (bait.Target.Id == id)
                foxshotBaits[i] = (bait.Source, bait.Target, bait.Cast, true);
        }
    }

    private void QuestWindObserve()
    {
        if ((!IsGamboling && !IsRaisingSword && !IsMainScenarioQuest && !IsHeroesPretenders && !IsDikaiosyne) || Active == null) return;
        foreach (var id in questWindObserved.Keys.Where(id => _actors.GetValueOrDefault(id)?.Cast == null).ToArray())
            questWindObserved.Remove(id);
        for (var i = 0; i < foxshotBaits.Count; ++i)
        {
            var bait = foxshotBaits[i];
            var source = _actors.GetValueOrDefault(bait.Source.Id);
            if (source?.Oid != bait.Source.Oid) source = null;
            var target = !bait.TargetRemoved ? _actors.GetValueOrDefault(bait.Target.Id) : null;
            if (target?.Oid != bait.Target.Oid) target = null;
            foxshotBaits[i] = (source ?? bait.Source, target ?? bait.Target, bait.Cast, bait.TargetRemoved || target == null);
        }
    }

    private IEnumerable<DungeonAmbientAoe> QuestWindAoes()
    {
        if ((!IsGamboling && !IsRaisingSword) || Active == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid == QuestWindOid && QuestWindVisible(a)))
            yield return new($"quest-wind/live/{actor.Id}", "Wind voidzone", new CircleTemplate(6), actor.Position, Obstacle: true);
        foreach (var bait in foxshotBaits.Where(b => !b.Source.Dead))
        {
            var delta = bait.Target.Position - bait.Source.Position;
            var length = new Vector2(delta.X, delta.Z).Length();
            var angle = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(4, length);
            var self = bait.Target.Id == PlayerId;
            yield return new($"foxshot/{bait.Source.Id}/display", "Foxshot", shape, bait.Source.Position, angle,
                Navigate: false, OutlineOnly: self);
            if (bait.TargetRemoved || !self)
                yield return new($"foxshot/{bait.Source.Id}/avoid", "Foxshot", shape, bait.Source.Position, angle,
                    Display: false, Activation: bait.Cast.FinishAt);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                {
                    var offset = member.Position - bait.Source.Position;
                    var distance = new Vector2(offset.X, offset.Z).Length();
                    yield return new($"foxshot/{bait.Source.Id}/party/{member.Id}", "Bait away from raid",
                        new FanTemplate(100, 2 * MathF.Asin(2 / distance)), bait.Source.Position,
                        MathF.Atan2(offset.X, offset.Z), Display: false, Activation: bait.Cast.FinishAt);
                }
        }
        if (questWindKnockback is not { } kb || _actors.GetValueOrDefault(PlayerId) is not { } player
            || WhorlImmune(player, kb.Cast.FinishAt)) yield break;
        // The source deliberately uses all wind actors here, even those hidden by its voidzone component.
        foreach (var actor in _actors.Values.Where(a => a.Oid == QuestWindOid))
        {
            var delta = actor.Position - kb.Origin;
            var distance = new Vector2(delta.X, delta.Z).Length();
            yield return new($"quest-wind/kb/avoid/{actor.Id}", "Avoid knockback into wind",
                new FanTemplate(100, 2 * MathF.Asin(6 / distance)), kb.Origin,
                MathF.Atan2(delta.X, delta.Z), Display: false, Activation: kb.Cast.FinishAt);
        }
        var landing = QuestWindLanding(player, kb.Origin);
        foreach (var aoe in QuestKnockbackProjection("quest-wind/kb", player, player.Position, landing)) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> QuestKnockbackProjection(string prefix, DungeonActor player, Vector3 from, Vector3 landing,
        IGeometryShape? displayBoundary = null, Vector4? markerColor = null)
    {
        if (landing == from) yield break;
        var movement = landing - from;
        var lengthToLanding = new Vector2(movement.X, movement.Z).Length();
        var markerPosition = landing;
        var outside = (displayBoundary ?? Arena)?.Contains(landing) == false;
        var showMarker = true;
        if (outside)
        {
            var ray = QuestWindRay(from, movement / lengthToLanding, displayBoundary);
            if (ray <= lengthToLanding) markerPosition = from + movement / lengthToLanding * ray;
            else showMarker = false;
        }
        if (showMarker)
        {
            var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
            yield return new($"{prefix}/player", "Knockback projection", marker, markerPosition, player.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true, Color: markerColor ?? new Vector4(1, 0, 0, 1));
        }
        yield return new($"{prefix}/path", "Knockback path", new RectangleTemplate(.12f, lengthToLanding),
            (landing + from) * .5f, MathF.Atan2(movement.X, movement.Z), Navigate: false, Unclipped: true);
    }

    private Vector3 QuestWindLanding(DungeonActor player, Vector3 origin)
    {
        if (Arena == null) return player.Position;
        var from = player.Position;
        var delta = from - origin;
        delta.Y = 0;
        if (delta.LengthSquared() == 0) return from;
        var direction = Vector3.Normalize(delta);
        var distance = IsGamboling ? 25f : 10f;
        var ray = QuestWindRay(from, direction);
        distance = MathF.Min(distance, ray - MathF.Max(.001f, player.HitboxRadius - .499f));
        return from + direction * distance;
    }

    private float QuestWindRay(Vector3 from, Vector3 direction, IGeometryShape? displayBoundary = null)
    {
        var ray = float.PositiveInfinity;
        var boundary = displayBoundary ?? Arena;
        if (boundary is CircleGeometry circle)
        {
            var relative = from - circle.Position;
            relative.Y = 0;
            var along = Vector3.Dot(relative, direction);
            var discriminant = along * along + circle.Radius * circle.Radius - relative.LengthSquared();
            var intersection = discriminant >= 0 ? -along + MathF.Sqrt(discriminant) : -1;
            var entry = discriminant >= 0 ? -along - MathF.Sqrt(discriminant) : -1;
            if (displayBoundary != null && entry >= 0)
                intersection = entry;
            if (intersection >= 0) ray = intersection;
        }
        else
        {
            var start = new Vector2(from.X, from.Z);
            var dir = new Vector2(direction.X, direction.Z);
            foreach (var contour in boundary!.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = new Vector2(contour[i].X, contour[i].Z);
                var b = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z);
                var edge = b - a;
                var denominator = dir.X * edge.Y - dir.Y * edge.X;
                if (MathF.Abs(denominator) < 1e-7f) continue;
                var offset = a - start;
                var t = (offset.X * edge.Y - offset.Y * edge.X) / denominator;
                var u = (offset.X * dir.Y - offset.Y * dir.X) / denominator;
                if (t >= 0 && u is >= 0 and <= 1) ray = MathF.Min(ray, t);
            }
        }
        return ray;
    }

    private IEnumerable<string> QuestWindHints(Vector3? position)
    {
        if ((!IsGamboling && !IsRaisingSword) || Active == null || position is not { } p) yield break;
        if (_actors.Values.Any(a => a.Oid == QuestWindOid && QuestWindVisible(a)
            && NativeEnemyDistanceSquared(a.Position, p) <= 36)) yield return "GTFO from voidzone!";
        foreach (var bait in foxshotBaits.Where(b => !b.Source.Dead))
        {
            var delta = bait.Target.Position - bait.Source.Position;
            var angle = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(4, new Vector2(delta.X, delta.Z).Length());
            var self = bait.Target.Id == PlayerId;
            if (self && _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                && shape.Resolve(bait.Source.Position, angle, null).Contains(a.Position)))
                yield return "Bait away from raid!";
            if (!self && shape.Resolve(bait.Source.Position, angle, null).Contains(p))
                yield return "GTFO from baited aoe!";
        }
        if (questWindKnockback is not { } kb || _actors.GetValueOrDefault(PlayerId) is not { } player
            || WhorlImmune(player, kb.Cast.FinishAt)) yield break;
        var landing = QuestWindLanding(player, kb.Origin);
        if (_actors.Values.Any(a => a.Oid == QuestWindOid && QuestWindVisible(a)
            && NativeEnemyDistanceSquared(a.Position, landing) <= 36))
            yield return "About to be knocked into danger!";
    }

    private IEnumerable<DungeonDamagePrediction> FoxshotDamage()
    {
        if (!IsGamboling || Active == null || foxshotBaits.Count == 0) yield break;
        foreach (var bait in foxshotBaits.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
        {
            var member = CurrentPartyMembers.FirstOrDefault(m => m.EntityId == bait.Target.Id);
            var slot = member?.Slot ?? jobQuestNpcSlots.Where(p => p.Value == bait.Target.Id).Select(p => p.Key).DefaultIfEmpty(-1).First();
            if (slot >= 0)
                yield return new(bait.Target.Id, slot, member?.Role ?? bait.Target.PartyRole, bait.Target.Name,
                    foxshotBaits[0].Cast.FinishAt, "Tankbuster");
        }
    }
}
