using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsHydaelyn => territory == HydaelynCatalog.TERRITORY;
    private readonly Dictionary<ulong, HydaelynActorReference> hydaActors = [];
    private readonly List<DungeonAmbientAoe> hydaParhelic = [];
    private readonly List<HydaelynActorReference> hydaWaves = [];
    private readonly List<(HydaelynActorReference Target, uint Action, DateTime At, int Key)> hydaMarks = [];
    private readonly List<(HydaelynActorReference Source, HydaelynActorReference Target, DateTime At, int Key)> hydaBaits = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> hydaCharges = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> hydaBeacons = [];
    private readonly Dictionary<ulong, (bool Present, ushort Param, DateTime Expires, ulong Source)> hydaStatusRaw = [];
    private readonly Dictionary<ulong, (bool Present, ushort Param, DateTime Expires, ulong Source)> hydaStatusPending = [];
    private readonly Dictionary<ulong, DateTime> hydaStatusPendingAt = [];
    private DungeonAmbientAoe? hydaWeapon;
    private HydaelynActorReference? hydaSharedSource;
    private HydaelynActorReference? hydaSharedTarget;
    private DateTime hydaSharedAt;
    private DateTime hydaExodusAt;
    private int hydaCrystalsDestroyed;
    private int hydaEchoHits;
    private int hydaKey;

    private void HydaObserve()
    {
        if (!IsHydaelyn || Active == null) return;
        foreach (var id in hydaActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) HydaRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (hydaActors.TryGetValue(actor.Id, out var current)) current.Actor = actor;
            else
            {
                hydaActors[actor.Id] = new(actor);
                HydaCreated(actor);
            }
            var present = actor.Statuses?.Contains(2273u) == true;
            var state = (present, actor.StatusParams?.GetValueOrDefault(2273u) ?? 0,
                actor.StatusExpirations?.GetValueOrDefault(2273u) ?? default, actor.StatusSources?.GetValueOrDefault(2273u) ?? 0);
            if (state.Item4 == 0xE0000000) state.Item4 = 0;
            var old = hydaStatusRaw.GetValueOrDefault(actor.Id);
            if (hydaStatusPending.TryGetValue(actor.Id, out var pending))
            {
                var confirmed = pending.Present == present && (!present || pending.Param == state.Item2 && pending.Source == state.Item4);
                if (!confirmed && _now < hydaStatusPendingAt[actor.Id].AddSeconds(.5)) continue;
                hydaStatusPending.Remove(actor.Id);
                hydaStatusPendingAt.Remove(actor.Id);
                if (confirmed) { hydaStatusRaw[actor.Id] = state; continue; }
                old = pending;
            }
            hydaStatusRaw[actor.Id] = state;
            if (old.Present && present && old.Source != state.Item4)
                HydaStatus(actor.Id, false, old.Param, _now, old.Expires, old.Source, snapshot: true);
            if (old.Present != present || present && (old.Source != state.Item4 || old.Param != state.Item2 || (state.Item3 - old.Expires).TotalSeconds > 1))
                HydaStatus(actor.Id, present, state.Item2, _now, state.Item3, state.Item4, snapshot: true);
        }
    }

    private void HydaCreated(DungeonActor actor)
    {
        if (Arena == null || actor.Oid is not (0x1EB24C or 0x1EB24D)) return;
        var c = Arena.Position;
        var hexagon = actor.Oid == 0x1EB24C;
        var radius = hexagon ? 17 : 8;
        var dir = QuantizedDungeonPosition(c + new Vector3(MathF.Sin(actor.Rotation), 0, MathF.Cos(actor.Rotation)) * radius);
        if (hexagon) Add(c, quantize: true);
        for (var i = 1; i <= (hexagon ? 6 : 3); ++i)
        {
            var angle = (hexagon ? i * 60 : -60 + i * 120) * (MathF.PI / 180);
            var offset = dir - c;
            var sin = (float)Math.Sin(angle);
            var cos = (float)Math.Cos(angle);
            Add(c + new Vector3(offset.X * cos - offset.Z * sin, 0, offset.X * sin + offset.Z * cos));
        }
        void Add(Vector3 position, bool quantize = false) => hydaParhelic.Add(new($"hyda/parhelic/{++hydaKey}",
            "幻日环", new CircleTemplate(6), quantize ? QuantizedDungeonPosition(position) : position,
            Activation: _now.AddSeconds(7.6)));
    }

    private void HydaCast(DungeonPendingCast cast)
    {
        if (!IsHydaelyn || Active == null || !hydaActors.TryGetValue(cast.Source, out var source)) return;
        var target = hydaActors.GetValueOrDefault(cast.TargetId);
        var action = cast.Rule.Action;
        if (action == 26070)
        {
            hydaSharedSource = source; hydaSharedTarget = target; hydaSharedAt = cast.FinishAt;
        }
        if (action == 26069 && target != null) hydaBaits.Add((source, target, cast.FinishAt, ++hydaKey));
        if (action is 27737 or 27738 && target != null) hydaMarks.Add((target, action, cast.FinishAt, ++hydaKey));
        if (action == 26062)
        {
            var delta = cast.Location - source.Actor.Position;
            var length = new Vector2(delta.X, delta.Z).Length();
            if (length > 0)
                hydaCharges[cast.Source] = new($"hyda/charge/{cast.Source}", "灯火冲锋",
                    RayRectangleTemplate.Symmetric(6, length), QuantizedDungeonPosition(source.Actor.Position),
                    MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt);
        }
        if (action == 26063)
            hydaBeacons[cast.Source] = new($"hyda/beacon/{cast.Source}", "灯火", RayRectangleTemplate.Symmetric(6, 45),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
    }

    private void HydaFinish(ulong source, uint action)
    {
        if (!IsHydaelyn) return;
        if (hydaSharedSource?.Actor.Id == source) hydaSharedSource = hydaSharedTarget = null;
        if (action == 26061) hydaParhelic.Clear();
        if (action == 26062) hydaCharges.Remove(source);
        if (action == 26063) hydaBeacons.Remove(source);
        if (action == 26069)
        {
            var i = hydaBaits.FindIndex(b => b.Source.Actor.Id == source);
            if (i >= 0) hydaBaits.RemoveAt(i);
        }
        if (action is 27737 or 27738 && _casts.TryGetValue(source, out var cast))
        {
            var i = hydaMarks.FindIndex(m => m.Action == action && m.Target.Actor.Id == cast.TargetId);
            if (i >= 0) hydaMarks.RemoveAt(i);
        }
    }

    private void HydaIcon(ulong actor, uint icon, DateTime at)
    {
        if (IsHydaelyn && Active != null && icon == 305 && hydaActors.TryGetValue(actor, out var target))
            hydaMarks.Add((target, 26065, at.AddSeconds(5), ++hydaKey));
    }

    private void HydaEffect(ulong source, uint action)
    {
        if (!IsHydaelyn || Active == null) return;
        if (action is 26255 or 26055 or 26056) hydaWeapon = null;
        if (action == 26066) hydaExodusAt = default;
        if (action == 26065 && ++hydaEchoHits == 5)
        {
            hydaMarks.RemoveAll(m => m.Action == 26065); hydaEchoHits = 0;
        }
        if (action == 26058 && hydaActors.TryGetValue(source, out var actor) && !hydaWaves.Contains(actor))
            hydaWaves.Add(actor);
    }

    private void HydaStatus(ulong actorId, bool present, ushort param, DateTime at, DateTime expires, ulong sourceId = 0, bool snapshot = false)
    {
        if (!IsHydaelyn || Active == null || Arena == null) return;
        if (sourceId == 0xE0000000) sourceId = 0;
        if (!snapshot)
        {
            var old = hydaStatusPending.TryGetValue(actorId, out var pending) ? pending : hydaStatusRaw.GetValueOrDefault(actorId);
            if (old.Present && present && old.Source != sourceId)
                HydaStatus(actorId, false, old.Param, at, old.Expires, old.Source, snapshot: true);
            hydaStatusPending[actorId] = (present, param, expires, sourceId);
            hydaStatusPendingAt[actorId] = at;
        }
        GeometryTemplate? shape = present ? param switch
        {
            0x1B4 => new CircleTemplate(10),
            0x1B5 => new DonutTemplate(5, 40),
            _ => null,
        } : new DungeonCrossTemplate(40, 5);
        if (shape == null || !present && !hydaActors.ContainsKey(actorId)) return;
        var actor = hydaActors.GetValueOrDefault(actorId)?.Actor;
        hydaWeapon = new("hyda/weapon", "武器切换", shape,
            QuantizedDungeonPosition(present ? Arena.Position : actor!.Position), present ? 0 : actor!.Rotation,
            Activation: at.AddSeconds(present ? 6 : 6.9));
    }

    private void HydaRemoved(ulong id, DateTime? occurredAt = null)
    {
        if (!IsHydaelyn || !hydaActors.TryGetValue(id, out var actor)) return;
        if (_casts.TryGetValue(id, out var cast)) HydaFinish(id, cast.Rule.Action);
        if ((hydaStatusPending.TryGetValue(id, out var pending) ? pending : hydaStatusRaw.GetValueOrDefault(id)).Present)
            HydaStatus(id, false, 0, occurredAt ?? _now, default, snapshot: true);
        actor.Destroyed = true;
        hydaActors.Remove(id);
        hydaStatusRaw.Remove(id); hydaStatusPending.Remove(id);
        hydaStatusPendingAt.Remove(id);
        if (actor.Actor.Oid == 0x34EC && ++hydaCrystalsDestroyed == 6) hydaExodusAt = (occurredAt ?? _now).AddSeconds(7.2);
    }

    private void ResetHyda()
    {
        hydaActors.Clear(); hydaParhelic.Clear(); hydaWaves.Clear(); hydaMarks.Clear();
        hydaBaits.Clear(); hydaCharges.Clear(); hydaBeacons.Clear(); hydaStatusRaw.Clear(); hydaStatusPending.Clear(); hydaStatusPendingAt.Clear();
        hydaWeapon = null; hydaSharedSource = hydaSharedTarget = null; hydaSharedAt = hydaExodusAt = default;
        hydaCrystalsDestroyed = hydaEchoHits = hydaKey = 0;
    }
}
