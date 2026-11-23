using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsYojimbo => territory == YojimboCatalog.TERRITORY;
    private readonly Dictionary<ulong, DungeonActor> yojimboActors = [];
    private readonly Dictionary<ulong, DateTime> yojimboBirths = [];
    private readonly HashSet<ulong> yojimboSeen = [];
    private readonly HashSet<ulong> yojimboDestroyed = [];
    private readonly HashSet<ulong> yojimboWaitingRemoval = [];
    private readonly List<(ulong Source, Vector3 Position, DateTime At, uint Kind, int Key)> yojimboBirthAoes = [];
    private readonly List<(DungeonPendingCast Cast, DateTime At)> yojimboDonuts = [];
    private readonly List<(ulong Source, Vector3 Next, Vector3 Step, DateTime At, float Rotation, int Left, int Key)> yojimboDragons = [];
    private readonly Dictionary<ulong, DungeonPendingCast> yojimboCharges = [];
    private readonly HashSet<(ulong Source, DateTime Start)> yojimboStarted = [];
    private readonly List<(DungeonActor Target, DateTime At, int Key, bool Destroyed)> yojimboJumps = [];
    private readonly List<(DungeonActor Target, bool Destroyed)> yojimboGekko = [];
    private readonly List<(DungeonActor Target, bool Destroyed)> yojimboTiny = [];
    private DateTime yojimboTinyAt;
    private DateTime yojimboYukikazeEnd;
    private DateTime yojimboDragonHintEnd;
    private int yojimboKey;

    private void YojimboCreated(ulong source, DateTime at)
    {
        if (!IsYojimbo) return;
        yojimboBirths.TryAdd(source, at);
        for (var i = 0; i < yojimboBirthAoes.Count; ++i)
        {
            var aoe = yojimboBirthAoes[i];
            var activation = at.AddSeconds(aoe.Kind == 0x25FE ? 4.85d : (double)6.7f);
            if (aoe.Source == source && activation < aoe.At)
                yojimboBirthAoes[i] = aoe with { At = activation };
        }
        for (var i = 0; i < yojimboDragons.Count; ++i)
        {
            var line = yojimboDragons[i];
            if (line.Source == source && line.Left == 20 && at.AddSeconds(5.7) < line.At)
                yojimboDragons[i] = line with { At = at.AddSeconds(5.7) };
        }
    }

    private void YojimboObserve()
    {
        if (!IsYojimbo || Active == null) return;
        foreach (var id in yojimboActors.Keys.Where(id => !_actors.ContainsKey(id) && !yojimboDestroyed.Contains(id)).ToArray())
            YojimboRemoved(id);
        yojimboWaitingRemoval.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values)
        {
            if (yojimboWaitingRemoval.Contains(actor.Id)) continue;
            yojimboActors[actor.Id] = actor;
            if (!yojimboSeen.Add(actor.Id)) continue;
            yojimboDestroyed.Remove(actor.Id);
            var born = yojimboBirths.GetValueOrDefault(actor.Id, _now);
            if (actor.Oid is 0x25FE or 0x2603)
                yojimboBirthAoes.Add((actor.Id, actor.Oid == 0x25FE ? QuantizedDungeonPosition(actor.Position) : actor.Position,
                    born.AddSeconds(actor.Oid == 0x25FE ? 4.85d : (double)6.7f), actor.Oid, ++yojimboKey));
            if (actor.Oid == 0x2601)
            {
                var step = new Vector3(MathF.Sin(actor.Rotation), 0, MathF.Cos(actor.Rotation)) * 3;
                yojimboDragons.Add((actor.Id, actor.Position, step, born.AddSeconds(5.7), actor.Rotation, 20, ++yojimboKey));
            }
            if (actor.Oid == 0x2602) yojimboChains.Add((actor, false, ++yojimboKey));
        }
        for (var i = 0; i < yojimboJumps.Count; ++i)
        {
            var mark = yojimboJumps[i];
            if (!mark.Destroyed && _actors.TryGetValue(mark.Target.Id, out var current))
                yojimboJumps[i] = mark with { Target = current };
        }
        yojimboJumps.RemoveAll(mark => mark.Target.Dead);
        for (var i = 0; i < yojimboGekko.Count; ++i)
        {
            var mark = yojimboGekko[i];
            if (!mark.Destroyed && _actors.TryGetValue(mark.Target.Id, out var current))
                yojimboGekko[i] = (current, false);
        }
        for (var i = 0; i < yojimboTiny.Count; ++i)
        {
            var mark = yojimboTiny[i];
            if (!mark.Destroyed && _actors.TryGetValue(mark.Target.Id, out var current))
                yojimboTiny[i] = (current, false);
        }
        for (var i = 0; i < yojimboChains.Count; ++i)
        {
            var chain = yojimboChains[i];
            if (!chain.Destroyed && _actors.TryGetValue(chain.Target.Id, out var current))
                yojimboChains[i] = chain with { Target = current };
        }
        YojimboObserveTethers();
        if (!_actors.Values.Any(a => a.Oid == 0x25FF && !a.Dead)) yojimboGekko.Clear();
    }

    private void YojimboTick()
    {
        if (!IsYojimbo || Active == null) return;
        if (yojimboTinyAt != default && yojimboTinyAt < _now) { yojimboTiny.Clear(); yojimboTinyAt = default; }
        for (var i = 0; i < yojimboDragons.Count; ++i)
        {
            var line = yojimboDragons[i];
            if (line.Left > 0 && _now >= line.At)
                yojimboDragons[i] = line with { Next = line.Next + line.Step, At = _now.AddSeconds(.6), Left = line.Left - 1 };
        }
        if (yojimboStormCaster != 0 && _now >= yojimboStormAt)
            yojimboStorms.RemoveAll(a => a.Center);
        if (yojimboStormCaster != 0 && yojimboSides && _now >= yojimboSeaAt)
        {
            yojimboStorms.RemoveAll(a => !a.Center);
            yojimboSides = false;
        }
    }

    private void YojimboCast(DungeonPendingCast cast)
    {
        if (!IsYojimbo) return;
        if (cast.Rule.Action == 14403 && _actors.TryGetValue(cast.Source, out var caster))
        {
            var delta = cast.Location - caster.Position; delta.Y = 0;
            var length = delta.Length();
            yojimboCharges[cast.Source] = cast with { Location = caster.Position, Rotation = MathF.Atan2(delta.X, delta.Z),
                Rule = cast.Rule with { Shape = RayRectangleTemplate.Symmetric(8, length) } };
        }
        if (!yojimboStarted.Add((cast.Source, cast.StartedAt))) return;
        if (cast.Rule.Action == 14388) yojimboDonuts.Add((cast, _now.AddSeconds((double)4.164f)));
        if (cast.Rule.Action == 14386 && _now >= yojimboYukikazeEnd)
            yojimboYukikazeEnd = cast.FinishAt.AddSeconds(3.5);
        YojimboStormCast(cast);
    }

    private void YojimboFinish(DungeonPendingCast cast)
    {
        if (!IsYojimbo) return;
        if (cast.Rule.Action == 14378) yojimboBirthAoes.RemoveAll(a => a.Kind == 0x25FE);
        if (cast.Rule.Action == 14400) yojimboBirthAoes.RemoveAll(a => a.Kind == 0x2603);
        if (cast.Rule.Action == 14388) yojimboDonuts.Clear();
        if (cast.Rule.Action == 14403) yojimboCharges.Remove(cast.Source);
        if (cast.Rule.Action == 14405) yojimboStorms.RemoveAll(a => a.Center);
        if (cast.Rule.Action == 14409) { yojimboStorms.RemoveAll(a => !a.Center); yojimboSides = false; }
    }

    private void YojimboEffect(uint action)
    {
        if (!IsYojimbo) return;
        if (action == 14387) yojimboGekko.Clear();
        if (action is 14396 or 14398) yojimboJumps.Clear();
    }

    private void YojimboIcon(ulong actor, uint icon, DateTime at)
    {
        if (!IsYojimbo || Active == null || !_actors.TryGetValue(actor, out var target)) return;
        if (icon == 144) yojimboGekko.Add((target, false));
        if (icon == 87) yojimboJumps.Add((target, at.AddSeconds(6), ++yojimboKey, false));
        if (icon == 55 && target.PartySlot >= 0)
        {
            yojimboTiny.Add((target, false));
            if (yojimboTinyAt == default) yojimboTinyAt = at.AddSeconds(5);
        }
    }

    private void YojimboTimeline(ulong source, uint timeline, DateTime at)
    {
        if (IsYojimbo && timeline == 0x008F && _actors.GetValueOrDefault(source)?.Oid == 0x2601)
            yojimboDragonHintEnd = at.AddSeconds(5.5);
    }

    private void YojimboRemoved(ulong source)
    {
        if (!IsYojimbo || !yojimboDestroyed.Add(source)) return;
        if (_actors.ContainsKey(source)) yojimboWaitingRemoval.Add(source);
        var actor = _actors.GetValueOrDefault(source) ?? yojimboActors.GetValueOrDefault(source);
        var hadBound = yojimboBound.Count > 0 && actor is { Oid: not 0, Buddy: false };
        for (var i = 0; i < yojimboJumps.Count; ++i)
            if (yojimboJumps[i].Target.Id == source) yojimboJumps[i] = yojimboJumps[i] with { Destroyed = true };
        for (var i = 0; i < yojimboGekko.Count; ++i)
            if (yojimboGekko[i].Target.Id == source) yojimboGekko[i] = (yojimboGekko[i].Target, true);
        for (var i = 0; i < yojimboTiny.Count; ++i)
            if (yojimboTiny[i].Target.Id == source) yojimboTiny[i] = (yojimboTiny[i].Target, true);
        for (var i = 0; i < yojimboChains.Count; ++i)
            if (yojimboChains[i].Target.Id == source) yojimboChains[i] = yojimboChains[i] with { Destroyed = true };
        foreach (var tether in yojimboTethers.Where(p => p.Key.Source == source).ToArray())
            YojimboTether(source, tether.Value, tether.Key.Tether, false, snapshot: true);
        foreach (var pending in yojimboTetherPending.Keys.Where(k => k.Source == source).ToArray())
            yojimboTetherPending.Remove(pending);
        if (_casts.TryGetValue(source, out var cast)) YojimboFinish(cast);
        yojimboBound.Remove(source);
        if (hadBound) YojimboPruneChains();
        yojimboSeen.Remove(source); yojimboBirths.Remove(source);
    }

    private void ResetYojimbo()
    {
        yojimboActors.Clear(); yojimboBirths.Clear(); yojimboSeen.Clear(); yojimboDestroyed.Clear(); yojimboWaitingRemoval.Clear();
        yojimboBirthAoes.Clear(); yojimboDonuts.Clear(); yojimboDragons.Clear(); yojimboCharges.Clear(); yojimboStarted.Clear();
        yojimboJumps.Clear(); yojimboGekko.Clear(); yojimboTiny.Clear(); yojimboTinyAt = default;
        yojimboYukikazeEnd = default; yojimboDragonHintEnd = default; yojimboKey = 0;
        yojimboTethers.Clear(); yojimboTetherPending.Clear(); yojimboEnchain.Clear(); yojimboBound.Clear(); yojimboChains.Clear();
        yojimboStorms.Clear(); yojimboStormCaster = 0; yojimboStormAt = default; yojimboSeaAt = default;
        yojimboLockedPosition = default; yojimboLockedRotation = 0; yojimboSides = false;
    }
}
