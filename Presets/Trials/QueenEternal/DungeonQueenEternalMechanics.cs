using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsQueenEternal => territory == QueenEternalCatalog.TERRITORY;
    private readonly Dictionary<ulong, QueenEternalActorReference> queenActors = [];
    private readonly List<DungeonAmbientAoe> queenForce = [];
    private readonly Dictionary<(ulong Source, DateTime Start), (string First, string Second)> queenForceCasts = [];
    private readonly List<DungeonAmbientAoe> queenBesiege = [];
    private int queenBesiegeCount;
    private DungeonAmbientAoe? queenTithe;
    private readonly List<(QueenEternalActorReference Actor, DateTime At, int Key)> queenWaltz = [];
    private (QueenEternalActorReference Actor, DateTime At)? queenRuthless;
    private readonly List<QueenEternalActorReference> queenTethered = [];
    private readonly List<DungeonPendingCast> queenBanishment = [];
    private readonly List<DungeonPendingCast> queenKnockbacks = [];
    private readonly List<(QueenEternalActorReference Target, DateTime At, int Key)> queenFlare = [];
    private readonly List<(QueenEternalActorReference Target, DateTime At, int Key)> queenLines = [];
    private QueenEternalActorReference? queenPrimary;
    private (QueenEternalActorReference Target, DateTime At)? queenDorito;
    private int queenLineCounter;
    private int queenLineCasts;
    private int queenKey;
    private string queenArenaMode = "default";
    private bool queenGravity;

    private void QueenObserve()
    {
        if (!IsQueenEternal || Active == null) return;
        foreach (var id in queenActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) QueenRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (queenActors.TryGetValue(actor.Id, out var reference)) reference.Actor = actor;
            else queenActors[actor.Id] = reference = new(actor);
            if (actor.Id == PrimaryId) queenPrimary = reference;
        }
        QueenObserveStatuses();
        QueenTick();
    }

    private void QueenCast(DungeonPendingCast cast)
    {
        if (!IsQueenEternal || Active == null) return;
        var action = cast.Rule.Action;
        if (action is >= 36638 and <= 36641)
        {
            var first = action is 36638 or 36639 ? -MathF.PI / 2 : MathF.PI / 2;
            var second = action is 36638 or 36641 ? -MathF.PI / 2 : MathF.PI / 2;
            var position = _actors.GetValueOrDefault(cast.Source)?.Position ?? cast.Location;
            var key = (cast.Source, cast.StartedAt);
            if (queenForceCasts.TryGetValue(key, out var ids))
            {
                // A packet corrects a restored snapshot without recreating hits
                // already consumed by action effects.
                for (var i = 0; i < queenForce.Count; ++i)
                    if (queenForce[i].Id == ids.First || queenForce[i].Id == ids.Second)
                        queenForce[i] = queenForce[i] with
                        {
                            Position = position,
                            Rotation = cast.Rotation + (queenForce[i].Id == ids.First ? first : second),
                        };
            }
            else
            {
                ids = ($"queen/force/{++queenKey}", $"queen/force/{++queenKey}");
                queenForceCasts[key] = ids;
                queenForce.Add(new(ids.First, "正统之力第一斩", RayRectangleTemplate.Symmetric(80, 20),
                    position, cast.Rotation + first, Activation: cast.FinishAt));
                queenForce.Add(new(ids.Second, "正统之力第二斩", RayRectangleTemplate.Symmetric(80, 20),
                    position, cast.Rotation + second, Activation: cast.FinishAt.AddSeconds(3.1)));
            }
        }
        if (action == 36647)
        {
            queenBanishment.Add(cast);
            if (queenBanishment.Count >= 99) QueenSortBanishment(0, queenBanishment.Count - 1);
        }
        if (action is 36610 or 36612) queenKnockbacks.Add(cast);
    }

    private void QueenFinish(DungeonPendingCast cast)
    {
        if (!IsQueenEternal) return;
        queenBanishment.RemoveAll(c => c.Source == cast.Source);
        queenKnockbacks.RemoveAll(c => c.Source == cast.Source);
    }

    private void QueenSortBanishment(int left, int right)
    {
        while (left < right)
        {
            var store = left;
            for (var i = left; i < right; ++i)
                if (queenBanishment[i].FinishAt < queenBanishment[right].FinishAt)
                {
                    (queenBanishment[store], queenBanishment[i]) = (queenBanishment[i], queenBanishment[store]);
                    ++store;
                }
            (queenBanishment[store], queenBanishment[right]) = (queenBanishment[right], queenBanishment[store]);
            if (store - left < right - store) { QueenSortBanishment(left, store - 1); left = store + 1; }
            else { QueenSortBanishment(store + 1, right); right = store - 1; }
        }
    }

    private void QueenMap(uint index, uint state)
    {
        if (!IsQueenEternal || Active == null) return;
        if (index == 1 && state == 0x00040004) QueenSetArena("default");
        if (state == 0x00020001 && index == 0) QueenSetArena("x");
        if (state == 0x00020001 && index == 2) QueenSetArena("split");
        if (index == 0 && queenPrimary != null)
        {
            float? degrees = state switch { 0x04000100 => -55, 0x08000100 => 0, 0x10000100 => 55, _ => null };
            if (degrees != null)
                queenTithe = new("queen/tithe", "以太税扇形预告", new FanTemplate(100, 70 * MathF.PI / 180),
                    QuantizedDungeonPosition(queenPrimary.Actor.Position), degrees.Value * MathF.PI / 180,
                    Activation: _now.AddSeconds(5));
        }
        if (index != 3) return;
        var first = queenBesiegeCount != 1;
        (int Width, float X)[] lanes = state switch
        {
            0x00200010 => first ? [(12, 114), (8, 90), (10, 101), (4, 82)] : [(8, 116), (8, 94), (10, 105), (8, 84)],
            0x02000100 => first ? [(10, 95), (18, 111), (8, 84)] : [],
            0x08000400 => first ? [(8, 116), (18, 101), (10, 85)] : [],
            0x00800040 => first ? [(8, 116), (10, 105), (8, 94), (8, 84)] : [(12, 114), (10, 101), (8, 90), (4, 82)],
            _ => [],
        };
        if (lanes.Length == 0) return;
        ++queenBesiegeCount;
        foreach (var lane in lanes)
            queenBesiege.Add(new($"queen/besiege/{++queenKey}", "围攻条带预告", RayRectangleTemplate.Symmetric(lane.Width, 60),
                QuantizedDungeonPosition(new(lane.X, Arena?.Position.Y ?? 0, 80)), -.003f * MathF.PI / 180, Activation: _now.AddSeconds(6.6)));
    }

    private void QueenSetArena(string mode)
    {
        queenArenaMode = mode;
        var y = Arena?.Position.Y ?? 0;
        Arena = mode switch
        {
            "x" => QueenEternalArenas.X(y),
            "split" => queenGravity ? new RectangleGeometry(new(100, y, 94), 0, 24, 16) : QueenEternalArenas.Split(y),
            "final" => new RectangleGeometry(new(100, y, 105), 0, 40, 30),
            _ => new RectangleGeometry(new(100, y, 100), 0, 40, 40),
        };
    }

    private void QueenTimeline(ulong source, uint timeline)
    {
        if (!IsQueenEternal || Active == null || !queenActors.TryGetValue(source, out var actor)) return;
        if (timeline == 0x2BFE) QueenSetArena("final");
        if (timeline == 0x11D7 && actor.Actor.Oid == 0x41C4) queenWaltz.Add((actor, _now.AddSeconds(7), ++queenKey));
        if (timeline == 0x11D2 && actor.Actor.Oid == 0x41C2) queenRuthless = (actor, _now.AddSeconds(11.1));
    }

    private void QueenTether(ulong source, bool present)
    {
        if (!IsQueenEternal || Active == null) return;
        if (present && queenActors.TryGetValue(source, out var actor)) queenTethered.Add(actor);
        if (!present) queenTethered.Clear();
    }

    private void QueenIcon(ulong source, ulong target, uint icon)
    {
        if (!IsQueenEternal || Active == null || !queenActors.TryGetValue(source, out var actor)) return;
        if (icon == 327) queenFlare.Add((queenActors.GetValueOrDefault(target) ?? actor, _now.AddSeconds(6), ++queenKey));
        if (icon == 55 && queenDorito == null) queenDorito = (actor, _now.AddSeconds(5.1));
        if (icon != 521 || queenLines.Count != 0) return;
        foreach (var member in queenActors.Values.Where(a => a.Actor.PartySlot is >= 0 and < 8))
            queenLines.Add((member, _now.AddSeconds(3), ++queenKey));
        queenLineCounter = 8;
    }

    private void QueenEffect(ulong source, uint action)
    {
        if (!IsQueenEternal) return;
        if (action is >= 36638 and <= 36643 && queenForce.Count > 0) queenForce.RemoveAt(0);
        if (action is 36657 or 36658 or 36659) queenTithe = null;
        if (action is >= 36614 and <= 36618) queenBesiege.Clear();
        if (action == 36634) queenRuthless = null;
        if (action == 39518 && queenFlare.Count > 0) queenFlare.RemoveAt(0);
        if (action is 39519 or 39520) queenDorito = null;
        if (action == 36631 && queenActors.TryGetValue(source, out var actor))
        {
            var i = queenWaltz.FindIndex(t => MathF.Abs(t.Actor.Actor.Position.X - actor.Actor.Position.X) <= .5f
                && MathF.Abs(t.Actor.Actor.Position.Z - actor.Actor.Position.Z) <= .5f);
            if (i >= 0) queenWaltz.RemoveAt(i);
        }
        if (action == 36637 && queenLines.Count > 0)
        {
            if (--queenLineCounter == 0) queenLines.Clear();
            if (++queenLineCasts > 8) { queenLines.Clear(); queenLineCasts = 0; }
        }
    }

    private void QueenTick()
    {
        if (!IsQueenEternal || Active == null) return;
        queenFlare.RemoveAll(b => b.Target.Actor.Dead);
        var gravity = _actors.Values.Any(a => a.PartySlot is >= 0 and < 8 && !a.Dead && QueenStatusPresent(a.Id, 3814));
        if (gravity != queenGravity)
        {
            queenGravity = gravity;
            if (queenArenaMode == "split") QueenSetArena("split");
        }
        if (queenDorito is not { } stack || _actors.GetValueOrDefault(PlayerId) is not { } player) return;
        var circles = _casts.Values.Where(c => c.Rule.Action == 36622).ToArray();
        var nearest = queenActors.Values.Where(a => a.Actor.PartySlot is >= 0 and < 8 && !a.Actor.Dead && a.Actor.Id != PlayerId
            && !circles.Any(c => new CircleGeometry(c.Location, 8).Contains(a.Actor.Position)))
            .OrderBy(a => Vector2.DistanceSquared(new(a.Actor.Position.X, a.Actor.Position.Z), new(player.Position.X, player.Position.Z)))
            .FirstOrDefault();
        if (nearest != null || queenActors.ContainsKey(PlayerId))
            queenDorito = (nearest ?? queenActors[PlayerId], stack.At);
    }

    private void QueenRemoved(ulong source)
    {
        if (!IsQueenEternal || !queenActors.TryGetValue(source, out var actor)) return;
        foreach (var key in queenStatuses.Keys.Where(k => k.Actor == source).ToArray())
        {
            QueenApplyStatus(source, key.Status, false, default);
            queenStatusRaw.Remove(key); queenStatusPending.Remove(key);
        }
        queenActors.Remove(source);
        var i = queenWaltz.FindIndex(t => t.Actor == actor);
        if (i >= 0 && actor.Actor.Oid == 0x41C4) queenWaltz.RemoveAt(i);
        if (_casts.TryGetValue(source, out var cast)) QueenFinish(cast);
    }

    private void ResetQueen()
    {
        queenActors.Clear(); queenForce.Clear(); queenForceCasts.Clear(); queenBesiege.Clear(); queenWaltz.Clear(); queenTethered.Clear();
        queenBanishment.Clear(); queenKnockbacks.Clear(); queenFlare.Clear(); queenLines.Clear();
        queenTithe = null; queenRuthless = null; queenDorito = null; queenPrimary = null;
        queenLineCounter = queenLineCasts = queenBesiegeCount = queenKey = 0;
        queenArenaMode = "default"; queenGravity = false; queenStatuses.Clear(); queenStatusRaw.Clear(); queenStatusPending.Clear();
        queenEyes.Clear(); queenGazeAt = default;
    }
}
