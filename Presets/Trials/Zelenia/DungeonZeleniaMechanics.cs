using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsZelenia => territory == ZeleniaCatalog.TERRITORY;
    private readonly Dictionary<ulong, ZeleniaActorReference> zeleniaActors = [];
    private readonly List<DungeonAmbientAoe> zeleniaThunderIV = [];
    private readonly List<(DungeonAmbientAoe Aoe, ulong Source, int Tile)> zeleniaThunderIII = [];
    private readonly bool[] zeleniaTiles = new bool[6];
    private readonly List<DungeonAmbientAoe> zeleniaShock = [];
    private readonly List<DungeonAmbientAoe> zeleniaAscension = [];
    private readonly List<DungeonPendingCast> zeleniaHoly = [];
    private readonly List<DungeonPendingCast> zeleniaSlash = [];
    private readonly List<DungeonPendingCast> zeleniaKnockbacks = [];
    private readonly List<(ZeleniaActorReference Target, uint Action, DateTime At, int Key)> zeleniaMarks = [];
    private readonly List<(ZeleniaActorReference Source, ZeleniaActorReference Target, DateTime At, int Key)> zeleniaBaits = [];
    private DungeonAmbientAoe? zeleniaShrink;
    private int zeleniaIVFinishes;
    private int zeleniaShockHits;
    private int zeleniaKey;

    private void ZeleniaObserve()
    {
        if (!IsZelenia || Active == null) return;
        foreach (var id in zeleniaActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) ZeleniaRemoved(id);
        foreach (var actor in _actors.Values)
            if (zeleniaActors.TryGetValue(actor.Id, out var current)) current.Actor = actor;
            else zeleniaActors[actor.Id] = new(actor);
    }

    private void ZeleniaCast(DungeonPendingCast cast)
    {
        if (!IsZelenia || Active == null || Arena == null) return;
        var action = cast.Rule.Action;
        if (action == 43092)
            zeleniaShrink = new("zelenia/shrink", "女王十字军缩场", new CircleTemplate(4), Arena.Position,
                Activation: cast.FinishAt.AddSeconds(.1));
        if (action is 43084 or 43085 or 43446 or 43447)
            zeleniaThunderIV.Add(new($"zelenia/thunder4/{++zeleniaKey}", "亚历山大雷IV",
                action is 43084 or 43446 ? new CircleTemplate(8) : new DonutTemplate(8, 24),
                cast.Location, Activation: cast.FinishAt));
        if (action is 43102 or 43439) ZeleniaConduct(cast);
        if (action is 43446 or 43447 && zeleniaThunderIII.Count == 0)
            for (var i = 0; i < 6; ++i)
                if (zeleniaTiles[i]) ZeleniaAddTile(i, cast.FinishAt, 0);
        if (action == 43126) zeleniaHoly.Add(cast);
        if (action == 43083) zeleniaSlash.Add(cast);
        if (action == 43479) zeleniaKnockbacks.Add(cast);
        if (action == 43129 && zeleniaActors.TryGetValue(cast.Source, out var source) && zeleniaActors.TryGetValue(cast.TargetId, out var target))
            zeleniaBaits.Add((source, target, cast.FinishAt, ++zeleniaKey));
    }

    private void ZeleniaFinish(ulong source, uint action)
    {
        if (!IsZelenia) return;
        if (action is 43084 or 43085 or 43446 or 43447 && zeleniaThunderIV.Count > 0) zeleniaThunderIV.RemoveAt(0);
        if (action is 43102 or 43439) zeleniaThunderIII.RemoveAll(a => a.Source == source);
        if (action is 43446 or 43447 && ++zeleniaIVFinishes == 2)
        {
            zeleniaThunderIII.Clear(); zeleniaIVFinishes = 0;
        }
        if (action == 43074 && zeleniaAscension.Count > 0) zeleniaAscension.RemoveAt(0);
        RemoveCaster(zeleniaHoly); RemoveCaster(zeleniaSlash); RemoveCaster(zeleniaKnockbacks);
        if (action == 43129)
        {
            var i = zeleniaBaits.FindIndex(b => b.Source.Actor.Id == source);
            if (i >= 0) zeleniaBaits.RemoveAt(i);
        }
        void RemoveCaster(List<DungeonPendingCast> list)
        {
            var index = list.FindIndex(c => c.Source == source);
            if (index >= 0) list.RemoveAt(index);
        }
    }

    private void ZeleniaIcon(ulong actor, uint icon, DateTime at)
    {
        if (!IsZelenia || Active == null || !zeleniaActors.TryGetValue(actor, out var target)) return;
        if (icon is 581 or 590) zeleniaMarks.Add((target, icon, at.AddSeconds(icon == 581 ? 8 : 7.1), ++zeleniaKey));
    }

    private void ZeleniaEffect(ulong source, uint action)
    {
        if (!IsZelenia || Active == null) return;
        if (action == 43057)
        {
            zeleniaMarks.RemoveAll(m => m.Action == 581);
            if (zeleniaActors.TryGetValue(source, out var caster))
                zeleniaShock.Add(new($"zelenia/shock/{++zeleniaKey}", "震击连续圆", new CircleTemplate(4),
                    QuantizedDungeonPosition(caster.Actor.Position)));
        }
        if (action == 43063 && ++zeleniaShockHits == 2 * zeleniaShock.Count)
        {
            zeleniaShock.Clear(); zeleniaShockHits = 0;
        }
        if (action == 43090) zeleniaMarks.RemoveAll(m => m.Action == 590);
    }

    private void ZeleniaMap(uint index, uint state)
    {
        if (!IsZelenia || Active == null || Arena == null) return;
        if (index == 1)
        {
            if (state == 0x00020001) { Arena = ZeleniaCatalog.Arena(Arena.Position.Y, true); zeleniaShrink = null; }
            if (state == 0x00080004) Arena = ZeleniaCatalog.Arena(Arena.Position.Y);
        }
        if (index is >= 4 and <= 9)
        {
            if (state == 0x00400100) zeleniaTiles[index - 4] = true;
            if (state == 0x00040020) zeleniaTiles[index - 4] = false;
        }
    }

    private void ZeleniaTimeline(ulong source, uint timeline, DateTime at)
    {
        if (!IsZelenia || Active == null || !_actors.TryGetValue(source, out var actor)) return;
        if (timeline == 0x11DB && actor.Oid == 0x47C4)
            zeleniaAscension.Add(new($"zelenia/ascension/{++zeleniaKey}", "英勇升天条带",
                RayRectangleTemplate.Symmetric(8, 40), QuantizedDungeonPosition(actor.Position), actor.Rotation,
                Activation: at.AddSeconds(11.1)));
        if (timeline != 0x1E46) return;
        var degrees = (int)(actor.Rotation * (180f / MathF.PI));
        var index = degrees switch
        {
            143 or -143 or 180 => 0,
            83 or 156 or 119 => 1,
            96 or 23 or 59 => 2,
            0 or -36 or 36 => 3,
            -96 or -23 or -60 => 4,
            -120 or -156 or -83 => 5,
            _ => -1,
        };
        if (index >= 0) zeleniaTiles[index] = true;
    }

    private void ZeleniaRemoved(ulong id)
    {
        if (!IsZelenia || !zeleniaActors.TryGetValue(id, out var actor)) return;
        if (_casts.TryGetValue(id, out var cast)) ZeleniaFinish(id, cast.Rule.Action);
        actor.Destroyed = true; zeleniaActors.Remove(id);
    }

    private void ResetZelenia()
    {
        zeleniaActors.Clear(); zeleniaThunderIV.Clear(); zeleniaThunderIII.Clear(); Array.Clear(zeleniaTiles);
        zeleniaShock.Clear(); zeleniaAscension.Clear(); zeleniaHoly.Clear(); zeleniaSlash.Clear();
        zeleniaKnockbacks.Clear(); zeleniaMarks.Clear(); zeleniaBaits.Clear(); zeleniaShrink = null;
        zeleniaIVFinishes = zeleniaShockHits = zeleniaKey = 0;
    }
}
