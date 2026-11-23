using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool lapisGriffinStage;
    private bool lapisSmall;
    private bool lapisFirstCross = true;
    private int lapisAntediluvian;
    private DateTime? lapisTsunami;
    private DungeonAmbientAoe? lapisArenaChange;
    private readonly List<LapisStampede> lapisStampedes = [];
    private readonly List<DungeonAmbientAoe> lapisCrosses = [];
    private readonly List<DungeonAmbientAoe> lapisRams = [];
    private readonly List<DungeonAmbientAoe> lapisBombs = [];
    private readonly List<(uint FirstAction, int Order, DungeonPendingCast Current)> lapisCycles = [];
    private readonly List<(Vector3 Position, DateTime Activation)> lapisTowers = [];
    private readonly Dictionary<ulong, (DungeonLosTemplate Safe, DungeonLosTemplate Danger)> lapisRoars = [];
    private readonly Dictionary<ulong, DateTime> lapisStacks = [];
    private readonly Dictionary<ulong, DateTime> lapisSpreads = [];
    private readonly Dictionary<ulong, (ulong Target, DateTime Changed)> lapisTethers = [];
    private readonly Dictionary<ulong, ulong> lapisTetherPending = [];

    private bool IsLapisCarriage => territory == LapisCatalog.TERRITORY && Active?.BossOid == 0x3D56;

    private void LapisSelectPrimary()
    {
        if (!IsLapisCarriage) return;
        var first = _actors.Values.Where(a => a.Oid is 0x3D56 or 0x3CE2).ToArray();
        if (!lapisGriffinStage && first.All(a => a.Dead))
        {
            lapisGriffinStage = true;
            foreach (var c in _casts.Values.Where(c => c.Rule.Action == 32535).ToArray()) Finish(c.Source);
        }
        var griffin = _actors.Values.FirstOrDefault(a => a.Oid == 0x3E9F);
        if (lapisGriffinStage && first.Length == 0 && (griffin == null || griffin.Dead))
        {
            ResetFight();
            return;
        }
        var next = lapisGriffinStage ? griffin : first.FirstOrDefault(a => !a.Dead);
        if (next != null) PrimaryId = next.Id;
    }

    private bool LapisAcceptCast(uint action) => !IsLapisCarriage || (lapisGriffinStage ? action != 32535 : action == 32535);

    private void LapisCast(DungeonPendingCast cast)
    {
        if (territory != LapisCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action == 31364 && !lapisRoars.ContainsKey(cast.Source))
        {
            var crystals = _actors.Values.Where(a => a.Oid == 0x3D04).ToArray();
            lapisRoars[cast.Source] = (new(new(cast.Location, crystals, true, 60, false, 0)),
                new(new(cast.Location, crystals, false, 60, false, 0)));
        }
        if (cast.Rule.Action is 32622 or 31377)
        {
            var index = lapisCycles.FindIndex(s => s.FirstAction == cast.Rule.Action && s.Current.Source == cast.Source && s.Order == 0);
            if (index < 0) lapisCycles.Add((cast.Rule.Action, 0, cast));
            else lapisCycles[index] = (cast.Rule.Action, 0, cast);
        }
        var key = $"lapis/{cast.Source}/{cast.StartedAt.Ticks}";
        if (cast.Rule.Action == 32693 && _actors.TryGetValue(cast.Source, out var caster))
        {
            var delta = cast.Location - caster.Position;
            var aoe = new DungeonAmbientAoe(key, "冲锋连续预告", RayRectangleTemplate.Symmetric(8, new Vector2(delta.X, delta.Z).Length()),
                QuantizedDungeonPosition(caster.Position), MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt.AddSeconds(5.7));
            var index = lapisRams.FindIndex(a => a.Id == key);
            if (index < 0) lapisRams.Add(aoe); else lapisRams[index] = aoe;
        }
        if (cast.Rule.Action == 32695)
        {
            var aoe = new DungeonAmbientAoe(key, "水圈连续预告", new CircleTemplate(4), cast.Location, Activation: cast.FinishAt.AddSeconds(6.1));
            var index = lapisBombs.FindIndex(a => a.Id == key);
            if (index < 0) lapisBombs.Add(aoe); else lapisBombs[index] = aoe;
        }
        if (cast.Rule.Action == 31139 && !lapisSmall)
            lapisArenaChange = new("lapis/shrink", "缩场预告", new DungeonShapeTemplate(LapisCatalog.ShrinkRing(Arena.Position)),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(.7));
    }

    private void LapisFinish(DungeonPendingCast cast, bool completed, DateTime now)
    {
        if (territory != LapisCatalog.TERRITORY) return;
        lapisRoars.Remove(cast.Source);
        if (completed && cast.Rule.Action == 32703 && lapisCrosses.Count > 0) lapisCrosses.RemoveAt(0);
        if (completed && cast.Rule.Action == 31120 && lapisAntediluvian == 6) lapisAntediluvian = 0;
        if (cast.Rule.Action is not (32622 or 32624 or 31377 or 31379)) return;
        var firstAction = cast.Rule.Action is 32622 or 32624 ? 32622u : 31377u;
        var order = cast.Rule.Action == firstAction ? 0 : 1;
        var index = lapisCycles.FindIndex(s => s.FirstAction == firstAction && s.Order == order
            && MathF.Abs(s.Current.Location.X - cast.Location.X) <= 1
            && MathF.Abs(s.Current.Location.Z - cast.Location.Z) <= 1);
        if (index < 0) return;
        var current = lapisCycles[index];
        if (!completed || order == 1) { lapisCycles.RemoveAt(index); return; }
        var rule = Active!.Casts.Single(r => r.Action == (firstAction == 32622 ? 32624 : 31379));
        lapisCycles[index] = (firstAction, 1, current.Current with
        {
            Rule = rule, FinishAt = now.AddSeconds(firstAction == 32622 ? 2 : 2.7),
        });
    }

    private void LapisMap(uint index, uint state, DateTime now)
    {
        if (territory != LapisCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x3CFE && state == 0x00020001 && index is >= 0x21 and <= 0x28)
        {
            var left = index < 0x25;
            var lane = (int)(index - (left ? 0x21u : 0x25u));
            // Reuse a vacant slot; source's active-count index can overwrite an ongoing lane.
            if (lapisStampedes.Count < 2)
                lapisStampedes.Add(new(new(left ? 4 : 44, Arena.Position.Y, -759 + lane * 10),
                    (left ? 89.999f : -90.004f) * MathF.PI / 180));
        }
        if (Active?.BossOid == 0x3971 && state == 0x00010008 && index is >= 7 and <= 10)
        {
            Vector3[] positions = [new(350, Arena.Position.Y, -404), new(360, Arena.Position.Y, -394),
                new(350, Arena.Position.Y, -384), new(340, Arena.Position.Y, -394)];
            lapisTowers.Add((QuantizedDungeonPosition(positions[index - 7]), now.AddSeconds(6)));
        }
        if (Active?.BossOid == 0x3AE2 && index == 0 && state == 0x00020001)
        {
            lapisSmall = true;
            Arena = new RectangleGeometry(Arena.Position, 0, 40, 40);
            lapisArenaChange = null;
        }
    }

    private void LapisIcon(ulong actorId, uint icon, DateTime now)
    {
        if (territory != LapisCatalog.TERRITORY || !_actors.TryGetValue(actorId, out var actor)) return;
        if (Active?.BossOid == 0x3971 && icon is >= 336 and <= 339)
        {
            lapisCrosses.Add(new($"lapis/cross/{lapisCrosses.Count}/{now.Ticks}/{actorId}", "连续十字",
                new DungeonCrossTemplate(40, 5), QuantizedDungeonPosition(actor.Position), 44.998f * MathF.PI / 180,
                Navigate: false, Activation: now.AddSeconds(9.9 + (icon - 336) * 3)));
            if (lapisCrosses.Count == 4 || lapisCrosses.Count == 2 && lapisFirstCross)
            {
                lapisFirstCross = false;
                lapisCrosses.Sort((a, b) => Nullable.Compare(a.Activation, b.Activation));
            }
        }
        if (Active?.BossOid != 0x3AE2) return;
        if (icon == 139) lapisSpreads[actorId] = now.AddSeconds(5);
        if (icon == 161) lapisStacks[actorId] = now.AddSeconds(5);
    }

    private void LapisEffect(ulong source, uint action, ulong target, DateTime now)
    {
        if (territory != LapisCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x3CFE && action == 31357 && _actors.TryGetValue(source, out var beast))
            foreach (var lane in lapisStampedes.Where(s => MathF.Abs(s.Position.Z - beast.Position.Z) < 1))
                if (++lane.Count == 30) lane.ResetAt = now.AddSeconds(.5);
        if (Active?.BossOid == 0x3971 && action == 31383 && _actors.TryGetValue(source, out var caster))
        {
            var position = QuantizedDungeonPosition(caster.Position);
            var index = lapisTowers.FindIndex(t => t.Position.X == position.X && t.Position.Z == position.Z);
            if (index >= 0) lapisTowers.RemoveAt(index);
        }
        if (Active?.BossOid != 0x3AE2) return;
        if (action == 31120) ++lapisAntediluvian;
        if (action == 32694 && lapisRams.Count > 0) lapisRams.RemoveAt(0);
        if (action == 32696 && lapisBombs.Count > 0) lapisBombs.RemoveAt(0);
        if (action is 31135 or 31376) lapisStacks.Clear();
        if (action == 31134 && !lapisSpreads.Remove(target) && lapisSpreads.Count > 0) lapisSpreads.Remove(lapisSpreads.Keys.First());
        if (action == 31137) lapisTsunami = null;
    }

    private void LapisTether(ulong source, ulong target, uint id, bool present, DateTime now, bool fromSnapshot = false)
    {
        if (territory != LapisCatalog.TERRITORY || Active?.BossOid != 0x3AE2 || id != 1) return;
        if (!_actors.TryGetValue(source, out var a) || !_actors.TryGetValue(target, out var b)) return;
        if (a.PartySlot >= 0 || a.Buddy) (source, target) = (target, source);
        if (!_actors.TryGetValue(target, out var player) || player.PartySlot < 0 && !player.Buddy) return;
        if (!fromSnapshot) lapisTetherPending[source] = present ? target : 0;
        if (present) lapisTethers[source] = (target, now);
        else if (lapisTethers.GetValueOrDefault(source).Target == target) lapisTethers.Remove(source);
    }

    private void SynchronizeLapis()
    {
        if (territory != LapisCatalog.TERRITORY) return;
        lapisStampedes.RemoveAll(s => s.ResetAt is { } reset && _now > reset);
        foreach (var source in lapisTethers.Keys.Where(id => !_actors.ContainsKey(id)
            || !_actors.ContainsKey(lapisTethers[id].Target)).ToArray()) lapisTethers.Remove(source);
        foreach (var actor in _actors.Values.Where(a => a.PartySlot < 0 && !a.Buddy))
        {
            var target = actor.Tethers?.GetValueOrDefault(1u) ?? 0;
            if (lapisTetherPending.TryGetValue(actor.Id, out var desired))
            {
                if (target != desired) continue;
                lapisTetherPending.Remove(actor.Id);
            }
            if (target == 0) lapisTethers.Remove(actor.Id);
            else if (lapisTethers.GetValueOrDefault(actor.Id).Target != target)
                LapisTether(actor.Id, target, 1, true, _now, true);
        }
        foreach (var id in lapisSpreads.Keys.Concat(lapisStacks.Keys).Distinct().Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
        {
            lapisSpreads.Remove(id); lapisStacks.Remove(id);
        }
        SynchronizeLapisStatuses();
    }

    private void ResetLapis()
    {
        lapisGriffinStage = false; lapisSmall = false; lapisFirstCross = true; lapisAntediluvian = 0;
        lapisTsunami = null; lapisArenaChange = null;
        lapisStampedes.Clear(); lapisCrosses.Clear(); lapisRams.Clear(); lapisBombs.Clear(); lapisCycles.Clear();
        lapisTowers.Clear(); lapisRoars.Clear(); lapisStacks.Clear(); lapisSpreads.Clear(); lapisTethers.Clear();
        lapisTetherPending.Clear();
        lapisEyes.Clear(); lapisEyeUnconfirmed.Clear(); lapisEyeLosses.Clear();
        lapisDoom.Clear(); lapisDoomUnconfirmed.Clear(); lapisDoomLosses.Clear();
        lapisGazeAt = default;
    }
}
