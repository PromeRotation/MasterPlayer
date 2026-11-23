using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonPendingCast> arbBreathGuesses = [];
    private readonly Dictionary<ulong, DungeonPendingCast> arbEarthyLines = [];
    private readonly Dictionary<ulong, (byte Model, byte Animation)> arbModels = [];
    private readonly Dictionary<ulong, (Vector3 Position, DateTime Activation)> arbMildew = [];
    private readonly List<(Vector3 Position, DateTime Activation)> arbPoison = [];
    private readonly Dictionary<ulong, DateTime> arbPetals = [];
    private readonly Dictionary<ulong, DateTime> arbPetalResolutions = [];
    private bool arbFirstRotation = true;
    private int arbCorrectSteps;
    private int arbRotationRemaining;
    private float arbFirstHeading;
    private float arbRotationHeading;
    private float arbRotationIncrement;
    private Vector3 arbRotationOrigin;
    private DateTime arbRotationActivation;
    private bool arbInitialBuster;
    private DungeonDamagePrediction? arbBuster;

    private void ArboretumCast(DungeonPendingCast cast)
    {
        if (Active?.BossOid == 0x142F)
        {
            if (cast.Rule.Action == 5230)
            {
                arbBreathGuesses.Clear();
                arbRotationRemaining = 0;
                arbCorrectSteps = 0;
                arbFirstHeading = cast.Rotation;
                arbBreathGuesses.Add(cast);
                foreach (var sign in new[] { -1, 1 })
                    arbBreathGuesses.Add(cast with { Rotation = cast.Rotation + sign * MathF.PI / 4, FinishAt = cast.FinishAt.AddSeconds(1) });
            }
            else if (cast.Rule.Action == 5316)
                arbEarthyLines[cast.Source] = cast with
                {
                    Rule = new(5232, 0x14C6, "EarthyBreathRest", RayRectangleTemplate.Symmetric(3, 7.5f)),
                };
        }
    }

    private void ArboretumFinish(DungeonPendingCast cast, bool completed, DateTime now)
    {
        if (Active?.BossOid == 0x142F && !completed)
        {
            if (cast.Rule.Action == 5230) { arbBreathGuesses.Clear(); arbRotationRemaining = 0; }
            if (cast.Rule.Action == 5316) arbEarthyLines.Remove(cast.Source);
        }
        if (Active?.BossOid == 0x1434 && cast.Rule.Action == 5216 && completed)
            ArboretumTankbuster(now.AddSeconds(4.1));
    }

    private void ArboretumEffect(ulong source, uint action, ulong target, Vector3? position, float? heading, DateTime timestamp)
    {
        if (Active?.BossOid == 0x142F)
        {
            if (action == 5230 && arbBreathGuesses.Count != 0) arbBreathGuesses.RemoveAt(0);
            if (action != 5225) return;
            if (arbRotationRemaining != 0)
            {
                --arbRotationRemaining;
                arbRotationHeading += arbRotationIncrement;
                arbRotationActivation = timestamp.AddSeconds(1);
                if (arbRotationRemaining == 0) arbCorrectSteps = 0;
                return;
            }
            if (arbBreathGuesses.Count == 0 || heading is not { } rotation || !float.IsFinite(rotation)) return;
            var delta = MathF.IEEERemainder(arbFirstHeading - rotation, 2 * MathF.PI);
            if (MathF.Abs(delta) < .001f) { ++arbCorrectSteps; return; }
            arbRotationRemaining = Math.Max(0, (arbFirstRotation ? 13 : 25) - arbCorrectSteps);
            arbFirstRotation = false;
            arbRotationIncrement = (delta > 0 ? -1 : 1) * 11.6f * MathF.PI / 180;
            arbRotationHeading = rotation;
            arbRotationOrigin = QuantizedDungeonPosition(_actors[PrimaryId].Position);
            arbRotationActivation = timestamp.AddSeconds(1);
            arbBreathGuesses.Clear();
        }
        else if (Active?.BossOid == 0x1432 && action == 5237)
        {
            if (_actors.TryGetValue(target, out var victim)) position = victim.Position;
            if (position is { } p) arbPoison.Add((p, timestamp.AddSeconds(.7)));
        }
        else if (Active?.BossOid == 0x1434)
        {
            if (action == 5219) arbBuster = null;
            if (action == 5218)
            {
                if (target != 0) { arbPetals.Remove(target); arbPetalResolutions[target] = timestamp; }
                else
                {
                    foreach (var id in arbPetals.Keys) arbPetalResolutions[id] = timestamp;
                    arbPetals.Clear();
                }
            }
        }
    }

    private void ArboretumTankbuster(DateTime activation)
    {
        if (_actors.TryGetValue(PrimaryId, out var boss) && _actors.TryGetValue(boss.TargetId, out var target) && target.PartySlot >= 0 && !target.Dead)
            arbBuster = new(target.Id, target.PartySlot, target.PartyRole, target.Name, activation, "Tankbuster");
    }

    private void SynchronizeArboretum()
    {
        if (Active?.BossOid is not (0x142F or 0x1432 or 0x1434)) return;
        if (Active.BossOid == 0x1434 && !arbInitialBuster)
        {
            arbInitialBuster = true;
            ArboretumTankbuster(_now.AddSeconds(3.1));
        }
        foreach (var actor in _actors.Values)
        {
            var state = (actor.ModelState, actor.AnimationState1);
            if (arbModels.TryGetValue(actor.Id, out var previous) ? previous == state : state == default)
            {
                arbModels[actor.Id] = state;
                continue;
            }
            arbModels[actor.Id] = state;
            if (Active.BossOid == 0x142F && actor.ModelState == 0) arbEarthyLines.Clear();
            if (Active.BossOid != 0x1434 || actor.Oid != 0x1435) continue;
            if (actor.AnimationState1 == 1)
            {
                arbMildew[actor.Id] = (QuantizedDungeonPosition(actor.Position), _now.AddSeconds(10));
                if (arbBuster == null && _actors.Values.Count(a => a.Oid == 0x1435) == 5) ArboretumTankbuster(_now.AddSeconds(8.1));
            }
            else arbMildew.Clear();
        }
        foreach (var id in arbModels.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) arbModels.Remove(id);
        if (Active.BossOid == 0x1432)
        {
            var zones = _actors.Values.Where(a => a.Oid == 0x1E8FCA && a.EventState != 7 && !a.Dead).ToArray();
            arbPoison.RemoveAll(p => zones.Any(z => Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(z.Position.X, z.Position.Z)) <= 4));
        }
        foreach (var id in arbPetals.Where(p => p.Value.AddSeconds(2) <= _now || !_actors.TryGetValue(p.Key, out var a) || a.Dead).Select(p => p.Key).ToArray())
            arbPetals.Remove(id);
        if (arbBuster != null && (!_actors.TryGetValue(arbBuster.TargetId, out var victim) || victim.Dead)) arbBuster = null;
    }

    private void ArboretumIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (Active?.BossOid == 0x1434 && icon == 43
            && (!arbPetalResolutions.TryGetValue(target, out var resolved) || timestamp > resolved))
            arbPetals[target] = timestamp.AddSeconds(3.1);
    }

    private IEnumerable<KeyValuePair<string, DungeonPendingCast>> ArboretumCasts()
    {
        for (var i = 0; i < arbBreathGuesses.Count; ++i) yield return new($"arboretum/breath/guess/{i}", arbBreathGuesses[i]);
        if (arbRotationRemaining <= 0) yield break;
        for (var i = 0; i < Math.Min(arbRotationRemaining, 8); ++i)
            yield return new($"arboretum/breath/rotation/{i}", new(PrimaryId,
                new(5225, 0x14C5, "ExtremelyBadBreathRotation", new FanTemplate(24.775f, MathF.PI / 2)),
                arbRotationOrigin, arbRotationHeading + i * arbRotationIncrement, arbRotationActivation.AddSeconds(-1),
                arbRotationActivation.AddSeconds(i)));
    }

    private IEnumerable<DungeonAmbientAoe> ArboretumAoes()
    {
        if (Active?.BossOid == 0x142F)
        {
            foreach (var line in arbEarthyLines.Values)
                yield return new($"arboretum/earthy/{line.Source}", line.Rule.Name, line.Rule.Shape!, line.Location, line.Rotation, Activation: line.FinishAt);
        }
        if (Active?.BossOid == 0x1432)
            for (var i = 0; i < arbPoison.Count; ++i)
                yield return new($"arboretum/poison/predicted/{i}", "毒液", new CircleTemplate(6), arbPoison[i].Position, Activation: arbPoison[i].Activation);
        if (Active?.BossOid is 0x142F or 0x1432)
        {
            var oid = Active.BossOid == 0x142F ? 0x1E9E38u : 0x1E8FCAu;
            foreach (var zone in _actors.Values.Where(a => a.Oid == oid && !a.Dead && a.EventState != 7))
                yield return new($"arboretum/voidzone/{zone.Id}", "持续毒区", new CircleTemplate(Active.BossOid == 0x142F ? 5 : 6), zone.Position);
        }
        if (Active?.BossOid == 0x1434)
        {
            foreach (var (id, mildew) in arbMildew)
                yield return new($"arboretum/mildew/{id}", "孢子爆炸", new CircleTemplate(6), mildew.Position, Activation: mildew.Activation);
            foreach (var (id, activation) in arbPetals)
            {
                if (!_actors.TryGetValue(id, out var target)) continue;
                yield return new($"arboretum/petal/{id}/display", "花瓣分散", new CircleTemplate(8), target.Position, Navigate: false, OutlineOnly: true);
                if (id != PlayerId)
                    yield return new($"arboretum/petal/{id}/avoid", "花瓣分散", new CircleTemplate(8), target.Position, Display: false, Activation: activation);
                else
                    foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != id && !a.Dead))
                        yield return new($"arboretum/petal/{id}/{buddy.Id}", "远离队友", new CircleTemplate(8), buddy.Position, Display: false, Activation: activation);
            }
        }
        if (Active?.BossOid is not (0x142F or 0x1432) || !_actors.TryGetValue(PrimaryId, out var boss)
            || boss.Cast != null || _casts.ContainsKey(PrimaryId) || boss.Dead || !boss.Targetable
            || Active.BossOid == 0x142F && arbRotationRemaining > 0 || Active.BossOid == 0x1432 && !HasStatus(boss, 152)
            || !_actors.TryGetValue(boss.TargetId, out var victim)) yield break;
        var shape = RayRectangleTemplate.Symmetric(Active.BossOid == 0x142F ? 7 : 5, Active.BossOid == 0x142F ? 11.775f : 8.4f);
        var rotation = MathF.Atan2(victim.Position.X - boss.Position.X, victim.Position.Z - boss.Position.Z);
        yield return new("arboretum/cleave/display", "顺劈", shape, boss.Position, rotation, Navigate: false, OutlineOnly: true);
        if (victim.Id != PlayerId)
            yield return new("arboretum/cleave/avoid", "顺劈", shape, boss.Position, rotation, Display: false);
        else
            foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                yield return new($"arboretum/cleave/{buddy.Id}", "顺劈避开队友",
                    new FanTemplate(100, 2 * MathF.Asin(Math.Clamp((Active.BossOid == 0x142F ? 3.5f : 2.5f)
                        / Math.Max(.001f, Vector2.Distance(new(buddy.Position.X, buddy.Position.Z), new(boss.Position.X, boss.Position.Z))), 0, 1))), boss.Position,
                    MathF.Atan2(buddy.Position.X - boss.Position.X, buddy.Position.Z - boss.Position.Z), Display: false);
    }

    private void ResetArboretum()
    {
        arbBreathGuesses.Clear(); arbEarthyLines.Clear(); arbModels.Clear(); arbMildew.Clear(); arbPoison.Clear();
        arbPetals.Clear(); arbPetalResolutions.Clear();
        arbFirstRotation = true; arbCorrectSteps = 0; arbRotationRemaining = 0;
        arbInitialBuster = false; arbBuster = null;
        arbLayout = -1;
        malboroCasts.Clear(); malboroFinished.Clear();
    }
}
