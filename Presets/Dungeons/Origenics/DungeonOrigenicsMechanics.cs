using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long origenicsSerial;
    private bool origenicsSmall;
    private uint origenicsMap;
    private DungeonAmbientAoe? origenicsShrink;
    private DungeonAmbientAoe? origenicsCharge;
    private readonly List<DungeonPendingCast> origenicsSimple = [];
    private readonly HashSet<(ulong Source, DateTime Start)> origenicsStarts = [];
    private readonly List<DungeonAmbientAoe> origenicsRiot = [];
    private readonly List<DungeonAmbientAoe> origenicsRush = [];
    private readonly HashSet<(ulong Source, DateTime Start)> origenicsFixedRush = [];
    private readonly List<DungeonAmbientAoe> origenicsPoison = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> origenicsLines = [];
    private readonly List<(ulong Source, DateTime Start, DungeonActor Target, DateTime At)> origenicsBusters = [];
    private readonly List<(Vector3 Origin, float Rotation, DateTime At, uint Action, Vector2[] Walls, ulong Source, DateTime Start)> origenicsKnockbacks = [];
    private readonly List<DungeonAmbientAoe> origenicsWallHints = [];
    private readonly Dictionary<(ulong Actor, int Slot), MasterPlayer.Replay.ReplayIncomingEffect> origenicsIncoming = [];
    private readonly Dictionary<(ulong Actor, uint Sequence, byte Target), DateTime> origenicsPending = [];

    private bool IsOrigenicsTrash => territory == OrigenicsCatalog.TERRITORY && Active?.BossOid == 0x41E2;

    private void OrigenicsSelectPrimary()
    {
        if (!IsOrigenicsTrash || Active == null) return;
        var alive = _actors.Values.Where(a => !a.Dead && (a.Oid == Active.BossOid || Active.PrimaryAliases?.Contains(a.Oid) == true)).ToArray();
        if (alive.Length == 0) { ResetFight(); return; }
        if (!alive.Any(a => a.Id == PrimaryId)) PrimaryId = alive[0].Id;
    }

    private void OrigenicsCast(DungeonPendingCast cast, DateTime now)
    {
        if (territory != OrigenicsCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        var fresh = origenicsStarts.Add((cast.Source, cast.StartedAt));
        var id = $"origenics/cast/{cast.Source}/{cast.StartedAt.Ticks}";
        if (OrigenicsCatalog.Riot(action))
        {
            var index = origenicsRiot.FindIndex(a => a.Id == id);
            if (index >= 0) origenicsRiot[index] = origenicsRiot[index] with { Position = cast.Location, Rotation = cast.Rotation };
            else if (fresh)
                origenicsRiot.Add(new(id, cast.Rule.Name, cast.Rule.Shape!, cast.Location, cast.Rotation,
                    Activation: now.AddSeconds(7.3 - origenicsRiot.Count)));
        }
        else if (action == 38953 && _actors.TryGetValue(cast.Source, out var source))
        {
            var delta = cast.Location - source.Position; delta.Y = 0;
            var key = (cast.Source, cast.StartedAt);
            if (fresh && origenicsRush.Count >= 7) origenicsFixedRush.Add(key);
            var normal = !origenicsFixedRush.Contains(key);
            var rushId = $"origenics/rush/{cast.Source}/{cast.StartedAt.Ticks}";
            var index = origenicsRush.FindIndex(a => a.Id == rushId);
            var aoe = new DungeonAmbientAoe(rushId, "连续电枪冲锋",
                RayRectangleTemplate.Symmetric(10, normal ? delta.Length() : 33),
                normal ? source.Position : new(190, Arena.Position.Y, 19.5f),
                normal ? MathF.Atan2(delta.X, delta.Z) : -MathF.PI, Activation: cast.FinishAt.AddSeconds(6.8));
            if (index >= 0) origenicsRush[index] = aoe with { Color = origenicsRush[index].Color };
            else if (fresh) origenicsRush.Add(aoe);
        }
        else if (action is 39233 or 39072)
            origenicsCharge = new("origenics/charge", cast.Rule.Name, cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        else if (action == 36367 && _actors.TryGetValue(cast.Source, out var caster))
        {
            if (fresh)
            {
                var walls = OrigenicsWalls();
                foreach (var rotation in new[] { cast.Rotation + MathF.PI / 2, cast.Rotation - MathF.PI / 2 })
                    origenicsKnockbacks.Add((caster.Position, rotation, cast.FinishAt, action, walls, cast.Source, cast.StartedAt));
                for (var i = 0; i < walls.Length; i += 2)
                    origenicsWallHints.Add(new($"origenics/wall/{++origenicsSerial}", "击退安全位置", RayRectangleTemplate.Symmetric(10, 15.5f),
                        new(Arena.Position.X, Arena.Position.Y, walls[i].Y - 5), walls[i].X == -187.5f ? -MathF.PI / 2 : MathF.PI / 2,
                        Navigate: false, Safe: true));
            }
            else
            {
                var indices = Enumerable.Range(0, origenicsKnockbacks.Count).Where(i => origenicsKnockbacks[i].Source == cast.Source
                    && origenicsKnockbacks[i].Start == cast.StartedAt).ToArray();
                for (var i = 0; i < indices.Length; ++i)
                {
                    var old = origenicsKnockbacks[indices[i]];
                    origenicsKnockbacks[indices[i]] = (caster.Position, cast.Rotation + (i == 0 ? MathF.PI / 2 : -MathF.PI / 2),
                        old.At, old.Action, old.Walls, old.Source, old.Start);
                }
            }
        }
        else if (OrigenicsCatalog.Expulsion(action))
        {
            if (fresh) origenicsKnockbacks.Add((cast.Location, cast.Rotation, cast.FinishAt, action, [], cast.Source, cast.StartedAt));
            else
                for (var i = 0; i < origenicsKnockbacks.Count; ++i)
                {
                    var old = origenicsKnockbacks[i];
                    if (old.Source == cast.Source && old.Start == cast.StartedAt)
                        origenicsKnockbacks[i] = (cast.Location, cast.Rotation, old.At, old.Action, old.Walls, old.Source, old.Start);
                }
        }
        else if (cast.Rule.Shape != null && cast.Rule.PartyAoe == null)
        {
            var index = origenicsSimple.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index < 0) origenicsSimple.Add(cast); else origenicsSimple[index] = cast;
            if (action is 38518 or 38519 && origenicsSimple.Count(c => c.Rule.Action is 38518 or 38519) >= 99)
                origenicsSimple.Sort((a, b) => a.FinishAt.CompareTo(b.FinishAt));
        }
        if (action == 36518)
        {
            var target = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
            if (_actors.TryGetValue(target, out var actor))
            {
                var index = origenicsBusters.FindIndex(b => b.Source == cast.Source && b.Start == cast.StartedAt);
                var prediction = (cast.Source, cast.StartedAt, actor, cast.FinishAt);
                if (index < 0) origenicsBusters.Add(prediction); else origenicsBusters[index] = prediction;
            }
        }
        if (!origenicsSmall && action is 36371 or 36436)
            origenicsShrink = new("origenics/shrink", "缩场预告",
                VanguardRing(Arena.Position, action == 36371 ? new(25, 25) : new(33, 24),
                    action == 36371 ? new(20, 20) : new(15, 19.5f)), Arena.Position, Activation: cast.FinishAt.AddSeconds(.7));
    }

    private void OrigenicsFinish(DungeonPendingCast cast)
    {
        if (territory != OrigenicsCatalog.TERRITORY) return;
        var index = origenicsSimple.FindIndex(c => c.Source == cast.Source);
        if (index >= 0) origenicsSimple.RemoveAt(index);
        if (cast.Rule.Action is 39233 or 39072) origenicsCharge = null;
        if (cast.Rule.Action == 36367 || OrigenicsCatalog.Expulsion(cast.Rule.Action))
        {
            origenicsKnockbacks.Clear();
            origenicsWallHints.Clear();
        }
    }

    private void OrigenicsEffect(ulong source, uint action, ulong target, Vector3? location, DateTime now)
    {
        if (territory != OrigenicsCatalog.TERRITORY) return;
        if (action is 36469 or 36470 or 36471 && origenicsRiot.Count > 0) origenicsRiot.RemoveAt(0);
        if (action is 38954 or 36431 && origenicsRush.Count > 0) origenicsRush.RemoveAt(0);
        if (action == 36460)
        {
            var position = _actors.GetValueOrDefault(target)?.Position ?? location;
            if (position is { } p)
                origenicsPoison.Add(new($"origenics/poison/{++origenicsSerial}", "毒池出现预测", new CircleTemplate(2), p, Activation: now.AddSeconds(.9)));
        }
        if (action == 36518)
        {
            var index = origenicsBusters.FindIndex(b => b.Target.Id == target);
            if (index >= 0) origenicsBusters.RemoveAt(index);
        }
        if (action == 36474 && _actors.TryGetValue(source, out var a) && _actors.TryGetValue(target, out var b))
            origenicsLines.Add((a, b, now.AddSeconds(5.6), ++origenicsSerial));
        if (action == 36473)
        {
            var index = origenicsLines.FindIndex(l => l.Target.Id == target);
            if (index >= 0) origenicsLines.RemoveAt(index);
            else if (origenicsLines.Count == 1 && _actors.ContainsKey(target)) origenicsLines.Clear();
        }
    }

    private void OrigenicsMap(uint index, uint state)
    {
        if (territory != OrigenicsCatalog.TERRITORY || Arena == null) return;
        var y = Arena.Position.Y;
        if (Active?.BossOid == 0x417D && index == 0x28 && state == 0x00020001)
        {
            origenicsSmall = true; origenicsShrink = null;
            Arena = new RectangleGeometry(new(190, y, 0), 0, 30, 39);
        }
        if (Active?.BossOid != 0x4170) return;
        if (state == 0x00020001 && index is 0x2A or 0x1B or 0x2C or 0x1E or 0x2D or 0x1D or 0x2B or 0x1C)
        {
            origenicsSmall = true; origenicsMap = index;
            Arena = index switch
            {
                0x2A => OrigenicsArenas.Deceiver2A(y), 0x1B => OrigenicsArenas.Deceiver1B(y),
                0x2C => OrigenicsArenas.Deceiver2C(y), 0x1E => OrigenicsArenas.Deceiver1E(y),
                0x2D => OrigenicsArenas.Deceiver2D(y), 0x1D => OrigenicsArenas.Deceiver1D(y),
                0x2B => OrigenicsArenas.Deceiver2B(y), _ => OrigenicsArenas.Deceiver1C(y),
            };
        }
        else if (state == 0x00080004 || state == 0x00020001 && index == 0x12)
        {
            origenicsSmall = true; origenicsMap = 0;
            Arena = new RectangleGeometry(new(-172, y, -142), 0, 40, 40);
            if (state == 0x00020001) origenicsShrink = null;
        }
    }

    private Vector2[] OrigenicsWalls() => origenicsMap switch
    {
        0x1B => [new(-187.5f, -142), new(-187.5f, -152), new(-187.5f, -122), new(-187.5f, -132),
            new(-156.5f, -152), new(-156.5f, -162), new(-156.5f, -132), new(-156.5f, -142)],
        0x1E => [new(-187.5f, -142), new(-187.5f, -152), new(-187.5f, -132), new(-187.5f, -142),
            new(-156.5f, -152), new(-156.5f, -162), new(-156.5f, -122), new(-156.5f, -132)],
        0x1D => [new(-187.5f, -152), new(-187.5f, -162), new(-187.5f, -122), new(-187.5f, -132),
            new(-156.5f, -142), new(-156.5f, -152), new(-156.5f, -132), new(-156.5f, -142)],
        0x1C => [new(-187.5f, -152), new(-187.5f, -162), new(-187.5f, -132), new(-187.5f, -142),
            new(-156.5f, -142), new(-156.5f, -152), new(-156.5f, -122), new(-156.5f, -132)],
        _ => [],
    };

    private void SynchronizeOrigenics()
    {
        if (territory != OrigenicsCatalog.TERRITORY) return;
        foreach (var actor in _actors.Values)
        {
            foreach (var effect in actor.IncomingEffects ?? [])
            {
                var slot = (actor.Id, effect.Slot);
                if (origenicsIncoming.TryGetValue(slot, out var previous) && previous == effect) continue;
                origenicsIncoming[slot] = effect;
                if (previous != null && previous.GlobalSequence == effect.GlobalSequence && previous.TargetIndex == effect.TargetIndex)
                    continue;
                if (previous != null) origenicsPending.Remove((actor.Id, previous.GlobalSequence, previous.TargetIndex));
                var key = (actor.Id, effect.GlobalSequence, effect.TargetIndex);
                if (effect.Knockback && effect.GlobalSequence != 0) origenicsPending[key] = _now.AddSeconds(3);
            }
            var present = actor.IncomingEffects?.Select(e => e.Slot).ToHashSet() ?? [];
            foreach (var slot in origenicsIncoming.Keys.Where(k => k.Actor == actor.Id && !present.Contains(k.Slot)).ToArray())
                if (origenicsIncoming.Remove(slot, out var previous))
                    origenicsPending.Remove((actor.Id, previous.GlobalSequence, previous.TargetIndex));
        }
        foreach (var key in origenicsPending.Keys.Where(k => !_actors.ContainsKey(k.Actor) || origenicsPending[k] < _now).ToArray())
            origenicsPending.Remove(key);
        foreach (var slot in origenicsIncoming.Keys.Where(k => !_actors.ContainsKey(k.Actor)).ToArray())
            origenicsIncoming.Remove(slot);
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E9E3C && a.EventState != 7))
        {
            var index = origenicsPoison.FindIndex(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
            if (index >= 0) origenicsPoison.RemoveAt(index);
        }
        for (var i = 0; i < origenicsLines.Count; ++i)
        {
            var l = origenicsLines[i];
            origenicsLines[i] = (_actors.GetValueOrDefault(l.Source.Id) ?? l.Source, _actors.GetValueOrDefault(l.Target.Id) ?? l.Target, l.At, l.Serial);
        }
    }

    private bool OrigenicsPendingKnockback(ulong actor) => origenicsPending.Any(p => p.Key.Actor == actor && p.Value >= _now);

    private void ResetOrigenics()
    {
        origenicsSerial = 0; origenicsSmall = false; origenicsMap = 0; origenicsShrink = null; origenicsCharge = null;
        origenicsSimple.Clear(); origenicsStarts.Clear(); origenicsRiot.Clear(); origenicsRush.Clear(); origenicsPoison.Clear();
        origenicsFixedRush.Clear();
        origenicsLines.Clear(); origenicsBusters.Clear(); origenicsKnockbacks.Clear(); origenicsWallHints.Clear();
        origenicsIncoming.Clear(); origenicsPending.Clear();
    }
}
