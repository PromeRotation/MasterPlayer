using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsDarkness => territory == DarknessCatalog.TERRITORY;
    private bool darknessWiping;
    private readonly List<DungeonActor> darknessTargets = [];
    private readonly List<DarknessChaser> darknessChasers = [];
    private readonly HashSet<(ulong Source, DateTime Started)> darknessChaseSeen = [];
    private long darknessSerial;

    private void DarknessDirector(uint command)
    {
        if (!IsDarkness) return;
        if (command == 0x40000005) { darknessWiping = true; ResetFight(); }
        if (command == 0x40000011) darknessWiping = false;
    }

    private void DarknessIcon(ulong target, uint icon)
    {
        if (IsDarkness && Active?.BossOid == 0xCFA && icon == 197 && _actors.TryGetValue(target, out var actor))
            darknessTargets.Add(actor);
    }

    private void DarknessTick()
    {
        if (!IsDarkness) return;
        for (var i = 0; i < darknessTargets.Count; ++i)
            if (_actors.TryGetValue(darknessTargets[i].Id, out var target)) darknessTargets[i] = target;
        for (var i = darknessChasers.Count - 1; i >= 0; --i)
        {
            var chase = darknessChasers[i];
            var exists = _actors.TryGetValue(chase.Target.Id, out var target);
            // StandardChasingAOEs retains the initial hit even if the marked actor has died.
            if ((!exists || target!.Dead) && chase.Remaining < 5) darknessChasers.RemoveAt(i);
            else if (exists) darknessChasers[i] = chase with { Target = target! };
        }
    }

    private void DarknessCast(DungeonPendingCast cast)
    {
        if (!IsDarkness) return;
        if (Active?.BossOid == 0xDF6 && cast.Rule.Action is 3253 or 3247)
        {
            var source = _actors[cast.Source].Position;
            var delta = cast.Location - source; delta.Y = 0;
            _casts[cast.Source] = cast with
            {
                Rule = cast.Rule with { Shape = RayRectangleTemplate.Symmetric(cast.Rule.Action == 3253 ? 4 : 14, delta.Length()) },
                Location = QuantizedDungeonPosition(source),
                Rotation = MathF.Atan2(delta.X, delta.Z),
            };
        }
        if (Active?.BossOid != 0xCFA || cast.Rule.Action != 3298) return;
        // Packet corrections keep one chaser and the original initial-hit clock.
        if (darknessChaseSeen.Any(v => v.Source == cast.Source && Math.Abs((v.Started - cast.StartedAt).TotalSeconds) < .25)) return;
        darknessChaseSeen.Add((cast.Source, cast.StartedAt));
        var target = darknessTargets.MinBy(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z),
            new(cast.Location.X, cast.Location.Z)));
        if (target == null) return;
        darknessTargets.Remove(target);
        darknessChasers.Add(new(++darknessSerial, cast.Source, target, cast.Location, 0, 5, cast.FinishAt));
    }

    private static Vector3 DarknessChasePosition(DarknessChaser chase)
    {
        var delta = chase.Target.Position - chase.Previous; delta.Y = 0;
        var distance = delta.Length();
        return QuantizedDungeonPosition(distance > chase.Distance
            ? chase.Previous + delta * (chase.Distance / distance) : chase.Target.Position);
    }

    private void DarknessEffect(ulong source, uint action, ulong target, Vector3? location, DateTime at)
    {
        if (!IsDarkness || Active?.BossOid != 0xCFA || action is not (3298 or 3299)) return;
        Vector3? position = target == source && _actors.TryGetValue(source, out var caster)
            ? QuantizedDungeonPosition(caster.Position) : _actors.GetValueOrDefault(target)?.Position ?? location;
        if (position == null || darknessChasers.Count == 0)
        {
            if (darknessChasers.Count == 0) darknessTargets.Clear();
            return;
        }
        var index = darknessChasers.Select((c, i) => (Distance: Vector2.DistanceSquared(
            new(DarknessChasePosition(c).X, DarknessChasePosition(c).Z), new(position.Value.X, position.Value.Z)), Index: i))
            .MinBy(c => c.Distance).Index;
        var chase = darknessChasers[index];
        if (chase.Remaining == 1) darknessChasers.RemoveAt(index);
        else darknessChasers[index] = chase with { Previous = position.Value, Distance = 4, Remaining = chase.Remaining - 1, Activation = at.AddSeconds(1.5) };
        if (darknessChasers.Count == 0) darknessTargets.Clear();
    }

    private IEnumerable<DungeonAmbientAoe> DarknessAoes()
    {
        if (!IsDarkness || Active == null) yield break;
        foreach (var gaze in Gazes.Where(g => g.Rule.Action == 3281))
        {
            if (_actors.GetValueOrDefault(PlayerId) is not { } player) continue;
            var delta = gaze.Location - player.Position; delta.Y = 0;
            var direction = delta.LengthSquared() > 0 ? Vector3.Normalize(delta) : Vector3.Zero;
            var facing = new Vector3(MathF.Sin(player.Rotation), 0, MathF.Cos(player.Rotation));
            var danger = Vector3.Dot(facing, direction) >= .707107f;
            var color = danger ? new Vector4(1, .2f, .2f, 1) : new Vector4(.2f, 1, .4f, 1);
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
            yield return new($"darkness/gaze/{gaze.Source}/eye", "死亡视线", new LocalPolygonTemplate(lens),
                gaze.Location, Navigate: false, Safe: !danger, Color: color);
            yield return new($"darkness/gaze/{gaze.Source}/pupil", "视线眼睛", new CircleTemplate(.18f),
                gaze.Location, Navigate: false, Color: new Vector4(0, 0, 0, 1));
            yield return new($"darkness/gaze/{gaze.Source}/arc", "死亡视线朝向", new FanTemplate(1, MathF.PI / 2),
                player.Position, player.Rotation, Navigate: false, OutlineOnly: true, Color: color);
        }
        foreach (var chase in darknessChasers)
        {
            var position = DarknessChasePosition(chase);
            var id = $"darkness/chase/{chase.Serial}";
            yield return new(id + "/display", $"追踪粒子光束：剩余{chase.Remaining}次", new CircleTemplate(10),
                position, Navigate: false, Activation: chase.Activation);
            var self = chase.Target.Id == PlayerId;
            var avoid = self ? position - new Vector3(MathF.Sin(chase.Target.Rotation), 0, MathF.Cos(chase.Target.Rotation)) * 10 : position;
            yield return new(id + "/avoid", "连续追踪避让", new CircleTemplate(self ? 14 : 11),
                avoid, Display: false, Activation: chase.Activation);
            var delta = chase.Target.Position - chase.Previous; delta.Y = 0;
            var length = delta.Length();
            if (length > .001f)
                yield return new(id + "/line", "追踪方向", RayRectangleTemplate.Symmetric(.12f, length),
                    chase.Previous, MathF.Atan2(delta.X, delta.Z), Navigate: false, OutlineOnly: true);
        }
    }

    private IEnumerable<string> DarknessHints(Vector3? position)
    {
        if (!IsDarkness || position is not { } player) yield break;
        if (_actors.GetValueOrDefault(PlayerId) is { } actor)
            foreach (var gaze in Gazes.Where(g => g.Rule.Action == 3281))
            {
                var delta = gaze.Location - player; delta.Y = 0;
                if (delta.LengthSquared() is > 0 and <= 100000000 && Vector3.Dot(Vector3.Normalize(delta),
                    new(MathF.Sin(actor.Rotation), 0, MathF.Cos(actor.Rotation))) >= .707107f)
                { yield return "背对死亡视线"; break; }
            }
        if (darknessChasers.Any(c => Vector2.DistanceSquared(new(player.X, player.Z),
            new(DarknessChasePosition(c).X, DarknessChasePosition(c).Z)) <= 100))
            yield return "避开追踪粒子光束";
    }
}
