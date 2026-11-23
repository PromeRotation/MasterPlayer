using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsZoraalJa => territory == ZoraalJaCatalog.TERRITORY;
    private readonly Dictionary<ulong, ZoraalJaActorReference> zoraalActors = [];
    private readonly List<DungeonPendingCast> zoraalSwords = [];
    private readonly HashSet<(ulong Source, DateTime Start)> zoraalSwordDanger = [];
    private readonly List<DungeonPendingCast> zoraalHalfFull = [];
    private readonly List<DungeonAmbientAoe> zoraalChasm = [];
    private readonly Dictionary<(ulong Source, DateTime Start), string> zoraalChasmKeys = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> zoraalCharges = [];
    private readonly List<DungeonAmbientAoe> zoraalTracks = [];
    private readonly Dictionary<(ulong Source, uint Id), ulong> zoraalRawTethers = [];
    private readonly Dictionary<(ulong Source, uint Id), (ulong Target, DateTime At)> zoraalPendingTethers = [];
    private readonly List<(ZoraalJaActorReference Target, DateTime At, int Key)> zoraalSpreads = [];
    private readonly List<(ZoraalJaActorReference Source, ZoraalJaActorReference Target, DateTime At, int Key)> zoraalStacks = [];
    private DungeonAmbientAoe? zoraalTransition;
    private int zoraalStackHits;
    private int zoraalKey;
    private bool zoraalWiping;

    private void ZoraalDirector(uint command)
    {
        if (!IsZoraalJa) return;
        if (command == 0x40000005)
        {
            zoraalWiping = true;
            ResetFight();
        }
        if (command == 0x40000011) zoraalWiping = false;
    }

    private void ZoraalObserve()
    {
        if (!IsZoraalJa || Active == null) return;
        foreach (var actor in _actors.Values)
            if (zoraalActors.TryGetValue(actor.Id, out var reference)) reference.Actor = actor;
            else zoraalActors[actor.Id] = new(actor);
        ZoraalTick();
        if (Active.BossOid != 0x42B4) return;
        var current = _actors.Values.SelectMany(a => (a.Tethers ?? new Dictionary<uint, ulong>())
            .Select(t => (Key: (a.Id, t.Key), Target: t.Value))).ToDictionary(t => t.Key, t => t.Target);
        foreach (var key in zoraalRawTethers.Keys.Concat(current.Keys).Distinct().ToArray())
        {
            var next = current.GetValueOrDefault(key);
            var previous = zoraalRawTethers.GetValueOrDefault(key);
            if (zoraalPendingTethers.TryGetValue(key, out var pending))
            {
                if (next == pending.Target) zoraalPendingTethers.Remove(key);
                else
                {
                    // Preserve the prior raw baseline while an older snapshot
                    // catches up. A later distinct raw state can supersede a
                    // packet that never received its matching snapshot.
                    if (next == previous || _now < pending.At.AddSeconds(.5)) continue;
                    zoraalPendingTethers.Remove(key);
                    if (next != 0) ZoraalAddTrack(key.Item1);
                }
            }
            else if (next != 0 && next != previous) ZoraalAddTrack(key.Item1);
            if (next == 0) zoraalRawTethers.Remove(key);
            else zoraalRawTethers[key] = next;
        }
    }

    private void ZoraalTick() => zoraalSpreads.RemoveAll(s => s.Target.Actor.Dead);

    private void ZoraalCast(DungeonPendingCast cast)
    {
        if (!IsZoraalJa || Active == null) return;
        var action = cast.Rule.Action;
        if (action is 37714 or 37738)
        {
            var list = action == 37714 ? zoraalSwords : zoraalHalfFull;
            var index = list.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index >= 0) list[index] = cast;
            else
            {
                if (action == 37714 && list.Count == 0) zoraalSwordDanger.Add((cast.Source, cast.StartedAt));
                list.Add(cast);
            }
        }
        if (action is 38184 or 37712 && _actors.TryGetValue(cast.Source, out var actor))
        {
            var delta = new Vector2(cast.Location.X - actor.Position.X, cast.Location.Z - actor.Position.Z);
            zoraalCharges[cast.Source] = new($"zoraal/charge/{cast.Source}", "飞剑冲锋",
                RayRectangleTemplate.Symmetric(4, delta.Length()), QuantizedDungeonPosition(actor.Position),
                MathF.Atan2(delta.X, delta.Y), Activation: cast.FinishAt);
        }
        if (action == 37720 && _actors.TryGetValue(cast.Source, out var sword) && Arena != null)
        {
            var position = cast.Location;
            if (!Arena.Contains(sword.Position))
                position = QuantizedDungeonPosition(position + new Vector3(position.X > 100 ? -21.2132f : 21.2132f,
                    0, position.Z > 100 ? -21.2132f : 21.2132f));
            var key = (cast.Source, cast.StartedAt);
            var correction = zoraalChasmKeys.TryGetValue(key, out var id);
            id ??= $"zoraal/chasm/{++zoraalKey}";
            zoraalChasmKeys[key] = id;
            var aoe = new DungeonAmbientAoe(id, "异界剑阵投影", cast.Rule.Shape!, position, cast.Rotation, Activation: cast.FinishAt);
            var index = zoraalChasm.FindIndex(a => a.Id == id);
            if (index >= 0) zoraalChasm[index] = aoe;
            else if (!correction) zoraalChasm.Add(aoe);
        }
    }

    private void ZoraalFinish(DungeonPendingCast cast)
    {
        if (!IsZoraalJa) return;
        if (cast.Rule.Action == 37714 && zoraalSwords.Count > 0) zoraalSwords.RemoveAt(0);
        if (cast.Rule.Action is 37720 or 37722) zoraalChasm.Clear();
        zoraalHalfFull.RemoveAll(c => c.Source == cast.Source);
        zoraalCharges.Remove(cast.Source);
    }

    private void ZoraalMap(uint index, uint state)
    {
        if (!IsZoraalJa || Active?.BossOid != 0x42B4 || Arena == null) return;
        if (index == 0x20 && state == 0x00020001)
            zoraalTransition = new("zoraal/transition", "场地收缩预告", ZoraalJaCatalog.Transition(Arena.Position.Y),
                Arena.Position, Activation: _now.AddSeconds(8));
        if (index == 0x20 && state == 0x00080004)
        {
            zoraalTransition = null;
            Arena = ZoraalJaCatalog.Arena(Arena.Position.Y, true);
        }
        if (index == 0x1B && state == 0x00080004) Arena = ZoraalJaCatalog.Arena(Arena.Position.Y);
    }

    private void ZoraalTether(ulong source, uint id, ulong target, bool present)
    {
        if (!IsZoraalJa || Active?.BossOid != 0x42B4) return;
        var key = (source, id);
        var previous = zoraalPendingTethers.TryGetValue(key, out var pending) ? pending.Target : zoraalRawTethers.GetValueOrDefault(key);
        zoraalPendingTethers[key] = (present ? target : 0, _now);
        // Raw capture and the packet describe one tether state transition.
        if (present && previous != target) ZoraalAddTrack(source);
    }

    private void ZoraalAddTrack(ulong source)
    {
        if (!zoraalActors.TryGetValue(source, out var reference)) return;
        var actor = reference.Actor;
        var rotation = actor.Rotation;
        var first = -45.003f * MathF.PI / 180;
        var third = 134.999f * MathF.PI / 180;
        bool Matches(float angle) => MathF.Abs(MathF.Atan2(MathF.Sin(rotation - angle), MathF.Cos(rotation - angle))) <= MathF.PI / 180;
        var patternX = Matches(first) || Matches(third);
        var right = new Vector2(-MathF.Cos(rotation), MathF.Sin(rotation));
        var offset = Vector2.Dot(new(actor.Position.X - 100, actor.Position.Z - 100), right);
        var special = offset is > -8 and < -7 or > 2 and < 3;
        var shift = 0;
        if (patternX) shift = special ? 5 : -5;
        var adjusted = offset + shift;
        var position = QuantizedDungeonPosition(new(100 + right.X * adjusted, actor.Position.Y, 100 + right.Y * adjusted));
        zoraalTracks.Add(new($"zoraal/track/{++zoraalKey}", "连线飞剑预判", new RectangleTemplate(5, 20),
            position, rotation, Activation: _now.AddSeconds(13.3)));
    }

    private void ZoraalIcon(ulong source, uint icon)
    {
        if (!IsZoraalJa || Active?.BossOid != 0x42B4 || icon != 376 || !zoraalActors.TryGetValue(source, out var actor)) return;
        zoraalSpreads.Add((actor, _now.AddSeconds(5.1), ++zoraalKey));
    }

    private void ZoraalEffect(ulong source, uint action, ulong target)
    {
        if (!IsZoraalJa || Active?.BossOid != 0x42B4) return;
        if (action == 37730 && zoraalTracks.Count > 0) zoraalTracks.RemoveAt(0);
        if (action == 37752 && zoraalSpreads.Count > 0)
        {
            var index = zoraalSpreads.FindIndex(s => s.Target.Actor.Id == target);
            zoraalSpreads.RemoveAt(index >= 0 ? index : 0);
        }
        if (action == 35567 && zoraalActors.TryGetValue(source, out var caster) && zoraalActors.TryGetValue(target, out var victim))
            zoraalStacks.Add((caster, victim, _now.AddSeconds(5.4), ++zoraalKey));
        if (action == 37750 && ++zoraalStackHits >= 4 && zoraalStacks.Count > 0)
        {
            zoraalStacks.RemoveAt(0);
            zoraalStackHits -= 4;
        }
    }

    private void ResetZoraal()
    {
        zoraalActors.Clear(); zoraalSwords.Clear(); zoraalSwordDanger.Clear(); zoraalHalfFull.Clear(); zoraalChasm.Clear(); zoraalChasmKeys.Clear();
        zoraalCharges.Clear(); zoraalTracks.Clear(); zoraalRawTethers.Clear(); zoraalPendingTethers.Clear();
        zoraalSpreads.Clear(); zoraalStacks.Clear(); zoraalTransition = null; zoraalStackHits = zoraalKey = 0;
    }
}
