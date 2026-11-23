using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long straySerial;
    private bool straySmall;
    private bool strayCross;
    private bool strayPlatforms;
    private DateTime? strayFleshbuster;
    private int strayTotalChasers;
    private DateTime strayChaseActivation;
    private DungeonAmbientAoe? strayShrink;
    private readonly HashSet<(ulong Source, DateTime Start)> strayStarts = [];
    private readonly List<DungeonPendingCast> straySimple = [];
    private readonly List<DungeonAmbientAoe> strayFalling = [];
    private readonly List<DungeonAmbientAoe> strayPour = [];
    private readonly List<DungeonAmbientAoe> strayTea = [];
    private readonly List<DungeonActor> strayCharges = [];
    private readonly List<DungeonActor> strayCups = [];
    private readonly List<DungeonActor> strayTargets = [];
    private readonly HashSet<ulong> strayTargetMask = [];
    private readonly List<(Vector3 Next, Vector3 Step, DateTime At, int Left, long Serial, DungeonAmbientAoe[] Aoes)> strayExas = [];
    private readonly List<(DungeonActor Target, Vector3 Previous, float Distance, int Left, DateTime At, long Serial)> strayChasers = [];
    private readonly List<(DungeonActor Target, DateTime At, ulong Source, DateTime Start, long Serial)> straySpreads = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> strayLines = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, uint Tether, long Serial)> strayStretches = [];
    private readonly Dictionary<ulong, DateTime> strayStretchActivation = [];
    private readonly HashSet<(ulong, uint)> strayObservedTethers = [];
    private readonly Dictionary<(ulong Source, uint Id), ulong> strayLiveTethers = [];
    private readonly HashSet<ulong> strayObservedPour = [];

    private void StrayObserveActors()
    {
        if (territory != StrayboroughCatalog.TERRITORY || Active == null) return;
        foreach (var a in _actors.Values)
        {
            if (Active.BossOid == 0x41CA && a.StatusParams?.GetValueOrDefault(1909u) == 1
                && strayObservedPour.Add(a.Id))
                StrayStatus(a.Id, 1909, true, 1);
            if (a.Tethers == null) continue;
            foreach (var (id, target) in a.Tethers)
                if (strayObservedTethers.Add((a.Id, id))) StrayTether(a.Id, target, id, true, _now);
        }
    }

    private void StrayCast(DungeonPendingCast cast)
    {
        if (territory != StrayboroughCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        var fresh = strayStarts.Add((cast.Source, cast.StartedAt));
        if (cast.Rule.PartyAoe != null && _actors.TryGetValue(cast.TargetId, out var target))
        {
            var i = straySpreads.FindIndex(s => s.Source == cast.Source && s.Start == cast.StartedAt);
            var value = (target, cast.FinishAt, cast.Source, cast.StartedAt, ++straySerial);
            if (i >= 0) straySpreads[i] = value with { Item5 = straySpreads[i].Serial };
            else if (fresh) straySpreads.Add(value);
        }
        else if (cast.Rule.Shape != null)
        {
            var i = straySimple.FindIndex(s => s.Source == cast.Source && s.StartedAt == cast.StartedAt);
            if (i >= 0) straySimple[i] = cast;
            else if (fresh) straySimple.Add(cast);
        }
        if (action == 36529 && !straySmall)
            strayShrink = new("stray/shrink", "缩场预告", new DonutTemplate(14, 20), Arena.Position,
                Activation: cast.FinishAt.AddSeconds(.9));
        if (action == 37148) strayFleshbuster = cast.FinishAt;
        if (action == 39683 && fresh && _actors.TryGetValue(cast.Source, out var caster))
        {
            var next = caster.Position;
            var step = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 4;
            var at = cast.FinishAt.AddSeconds(1.6);
            var serial = ++straySerial;
            strayExas.Add((next, step, at, 5, serial, StrayExaAoes(next, step, at, 5, serial, _now)));
            RebuildStrayExas(_now);
        }
        if (action == 39686 && fresh)
        {
            var targetActor = strayTargets.MinBy(a => Vector3.DistanceSquared(a.Position, cast.Location));
            if (targetActor != null)
            {
                strayTargets.Remove(targetActor);
                strayTargetMask.Remove(targetActor.Id);
                strayChasers.Add((targetActor, cast.Location, 0, strayTotalChasers > 1 ? 4 : 5, cast.FinishAt, ++straySerial));
            }
            // The source calls the base method before reducing MaxCasts after the second first-cast.
            ++strayTotalChasers;
        }
    }

    private static DungeonAmbientAoe[] StrayExaAoes(Vector3 next, Vector3 step, DateTime at, int left, long serial, DateTime now)
        => Enumerable.Range(0, left).Select(i => new DungeonAmbientAoe($"stray/exa/{serial}/{i}", "连续爆炸",
            new CircleTemplate(4), QuantizedDungeonPosition(next + step * i),
            Activation: i == 0 ? at : (at > now ? at : now).AddSeconds(1.5 * i),
            Color: i == 0 ? new Vector4(1, .15f, .15f, .45f) : null)).ToArray();

    private void StrayFinish(DungeonPendingCast cast)
    {
        if (territory != StrayboroughCatalog.TERRITORY) return;
        var i = straySimple.FindIndex(c => c.Source == cast.Source && c.Rule.Action == cast.Rule.Action);
        if (i >= 0) straySimple.RemoveAt(i);
        i = straySpreads.FindIndex(s => s.Target.Id == cast.TargetId);
        if (cast.Rule.Action == 37146 && i >= 0) straySpreads.RemoveAt(i);
        if (cast.Rule.Action is 36532 or 36536 && strayFalling.Count > 0) strayFalling.RemoveAt(0);
        if (cast.Rule.Action == 36720) { strayTea.Clear(); strayCups.Clear(); }
        if (cast.Rule.Action == 37148) strayFleshbuster = null;
    }

    private void StrayTimeline(ulong source, uint timeline, DateTime now)
    {
        if (territory != StrayboroughCatalog.TERRITORY || !_actors.TryGetValue(source, out var a)) return;
        if (Active?.BossOid == 0x4183)
        {
            if (timeline == 0x11D1 && a.Oid == 0x4205)
                strayFalling.Add(new($"stray/falling/{++straySerial}", "落下头颅", new CircleTemplate(2), a.Position, Activation: now.AddSeconds(3)));
            if (timeline == 0x1E3C)
            {
                var i = strayCharges.FindIndex(c => c.Id == source);
                if (i >= 0) strayCharges.RemoveAt(i);
            }
        }
        if (Active?.BossOid == 0x41CA && timeline == 0x11DD && a.Oid == 0x41CB && strayPour.Count > 0) strayPour.RemoveAt(0);
    }

    private void StrayIcon(ulong target, uint icon, DateTime now, ulong actorId)
    {
        if (territory != StrayboroughCatalog.TERRITORY || Active?.BossOid != 0x4183 || icon != 197
            || !_actors.TryGetValue(actorId is 0 or 0xE0000000 ? target : actorId, out var a)) return;
        strayTargets.Add(a);
        strayTargetMask.Add(target);
        strayChaseActivation = now.AddSeconds(5.1);
    }

    private void StrayStatus(ulong target, uint status, bool present, ushort param)
    {
        if (territory == StrayboroughCatalog.TERRITORY && status == 3949) SynchronizeStray();
        if (territory != StrayboroughCatalog.TERRITORY || Active?.BossOid != 0x41CA || status != 1909 || !present || param != 1
            || !_actors.TryGetValue(target, out var a)) return;
        strayObservedPour.Add(target);
        strayPour.Add(new($"stray/pour/{++straySerial}", "茶壶持续范围", new CircleTemplate(8), QuantizedDungeonPosition(a.Position),
            Activation: DateTime.MinValue));
    }

    private void StrayEffect(ulong source, uint action, ulong target, Vector3? location, DateTime now)
    {
        if (territory != StrayboroughCatalog.TERRITORY || !_actors.TryGetValue(source, out var caster)) return;
        if (Active?.BossOid == 0x4183)
        {
            if (action == 36533) strayCharges.Add(caster);
            if (action is 39683 or 39684)
            {
                var p = caster.Position;
                var i = strayExas.FindIndex(l => MathF.Abs(l.Next.X - p.X) <= 1 && MathF.Abs(l.Next.Z - p.Z) <= 1);
                if (i >= 0)
                {
                    var l = strayExas[i];
                    if (--l.Left == 0) strayExas.RemoveAt(i);
                    else
                    {
                        l.Next = p + l.Step; l.At = now.AddSeconds(1.5);
                        l.Aoes = StrayExaAoes(l.Next, l.Step, l.At, l.Left, l.Serial, now);
                        strayExas[i] = l;
                    }
                    RebuildStrayExas(now);
                }
            }
            if (action is 39686 or 39687)
            {
                var pos = target == source ? QuantizedDungeonPosition(caster.Position) : _actors.GetValueOrDefault(target)?.Position ?? location ?? Vector3.Zero;
                var i = strayChasers.Count == 0 ? -1 : Enumerable.Range(0, strayChasers.Count)
                    .MinBy(n => Vector3.DistanceSquared(StrayChaserPosition(strayChasers[n]), pos));
                if (i >= 0)
                {
                    var c = strayChasers[i];
                    if (--c.Left <= 0) strayChasers.RemoveAt(i);
                    else { c.Previous = pos; c.Distance = 3; c.At = now.AddSeconds((double)1.6f); strayChasers[i] = c; }
                }
                if (strayChasers.Count == 0) strayTargets.Clear(); // The source deliberately leaves TargetsMask intact.
            }
        }
        if (Active?.BossOid == 0x421F)
        {
            if (action == 37144 && _actors.TryGetValue(target, out var a))
                strayLines.Add((caster, a, now.AddSeconds(5), ++straySerial));
            if (action == 37143)
            {
                var i = strayLines.FindIndex(l => l.Target.Id == target);
                if (i < 0 && strayLines.Count == 1 && _actors.ContainsKey(target)) i = 0;
                if (i >= 0) strayLines.RemoveAt(i);
            }
        }
    }

    private void StrayTether(ulong source, ulong target, uint tether, bool present, DateTime now)
    {
        if (territory != StrayboroughCatalog.TERRITORY) return;
        var cachedActors = strayStretches.SelectMany(s => new[] { s.Source, s.Target }).ToArray();
        var a = _actors.GetValueOrDefault(source) ?? cachedActors.FirstOrDefault(a => a.Id == source);
        var b = _actors.GetValueOrDefault(target) ?? cachedActors.FirstOrDefault(a => a.Id == target);
        if (present)
        {
            strayObservedTethers.Add((source, tether));
            strayLiveTethers[(source, tether)] = target;
        }
        else
        {
            strayObservedTethers.Remove((source, tether));
            strayLiveTethers.Remove((source, tether));
        }
        if (Active?.BossOid == 0x41CA && tether == 276 && present && a != null) strayCups.Add(a);
        if (Active?.BossOid != 0x421F || tether is not (57 or 1) || a == null || b == null) return;
        var (player, enemy) = a.PartySlot >= 0 ? (a, b) : (b, a);
        if (present)
        {
            if (!strayStretchActivation.TryGetValue(player.Id, out var at))
                strayStretchActivation[player.Id] = at = now.AddSeconds(10);
            strayStretches.Add((enemy, player, at, tether, ++straySerial));
        }
        else strayStretches.RemoveAll(s => s.Source.Id == enemy.Id && s.Target.Id == player.Id);
    }

    private void SynchronizeStray()
    {
        if (territory != StrayboroughCatalog.TERRITORY) return;
        foreach (var p in strayLiveTethers.Where(p => !_actors.ContainsKey(p.Key.Source)).ToArray())
            StrayTether(p.Key.Source, p.Value, p.Key.Id, false, _now);
        DungeonActor Latest(DungeonActor a) => _actors.GetValueOrDefault(a.Id) ?? a;
        for (var i = 0; i < strayCharges.Count; ++i) strayCharges[i] = Latest(strayCharges[i]);
        for (var i = 0; i < strayCups.Count; ++i) strayCups[i] = Latest(strayCups[i]);
        for (var i = 0; i < strayTargets.Count; ++i) strayTargets[i] = Latest(strayTargets[i]);
        for (var i = 0; i < strayChasers.Count; ++i) { var c = strayChasers[i]; c.Target = Latest(c.Target); strayChasers[i] = c; }
        strayChasers.RemoveAll(c => (!_actors.ContainsKey(c.Target.Id) || c.Target.Dead) && c.Left < (strayTotalChasers > 1 ? 4 : 5));
        for (var i = 0; i < straySpreads.Count; ++i) { var s = straySpreads[i]; s.Target = Latest(s.Target); straySpreads[i] = s; }
        for (var i = 0; i < strayLines.Count; ++i) { var l = strayLines[i]; l.Source = Latest(l.Source); l.Target = Latest(l.Target); strayLines[i] = l; }
        for (var i = 0; i < strayStretches.Count; ++i) { var s = strayStretches[i]; s.Source = Latest(s.Source); s.Target = Latest(s.Target); strayStretches[i] = s; }
        foreach (var id in strayStretchActivation.Where(p => p.Value.AddSeconds(1) <= _now).Select(p => p.Key).ToArray()) strayStretchActivation.Remove(id);
        if (Active?.BossOid == 0x421F && Arena != null)
        {
            var cross = strayCross && _actors.GetValueOrDefault(PlayerId) is { } player && !HasStatus(player, 3949);
            if (cross && Arena is RectangleGeometry) Arena = StrayboroughArenas.Cross(Arena.Position.Y);
            else if (!cross && Arena is not RectangleGeometry) Arena = new RectangleGeometry(Arena.Position, 0, 39, 39);
        }
    }

    private void RebuildStrayExas(DateTime now)
    {
        for (var i = 0; i < strayExas.Count; ++i)
        {
            var l = strayExas[i];
            l.Aoes = StrayExaAoes(l.Next, l.Step, l.At, l.Left, l.Serial, now);
            strayExas[i] = l;
        }
    }

    private void StrayRemoved(ulong source, DateTime now)
    {
        if (territory != StrayboroughCatalog.TERRITORY) return;
        foreach (var pair in strayLiveTethers.Where(p => p.Key.Source == source).ToArray())
            StrayTether(source, pair.Value, pair.Key.Id, false, now);
        if (_actors.GetValueOrDefault(source)?.Tethers is { } tethers)
            foreach (var (id, target) in tethers) StrayTether(source, target, id, false, now);
        if (_casts.TryGetValue(source, out var cast)) StrayFinish(cast);
    }

    private static Vector3 StrayChaserPosition((DungeonActor Target, Vector3 Previous, float Distance, int Left, DateTime At, long Serial) c)
    {
        var delta = c.Target.Position - c.Previous;
        delta.Y = 0;
        var pos = delta.Length() > c.Distance ? c.Previous + Vector3.Normalize(delta) * c.Distance : c.Target.Position;
        return QuantizedDungeonPosition(pos);
    }

    private void ResetStray()
    {
        straySerial = 0; straySmall = false; strayCross = false; strayPlatforms = false; strayFleshbuster = null;
        strayTotalChasers = 0; strayChaseActivation = default; strayShrink = null;
        strayStarts.Clear(); straySimple.Clear(); strayFalling.Clear(); strayPour.Clear(); strayTea.Clear(); strayCharges.Clear();
        strayCups.Clear(); strayTargets.Clear(); strayTargetMask.Clear(); strayExas.Clear(); strayChasers.Clear(); straySpreads.Clear();
        strayLines.Clear(); strayStretches.Clear(); strayStretchActivation.Clear(); strayObservedTethers.Clear(); strayLiveTethers.Clear(); strayObservedPour.Clear();
    }
}
