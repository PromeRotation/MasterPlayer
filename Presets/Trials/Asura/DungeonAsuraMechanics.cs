using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsAsura => territory == AsuraCatalog.TERRITORY;
    private readonly List<DungeonAmbientAoe> asuraLacerations = [];
    private readonly List<DungeonAmbientAoe> asuraKhadga = [];
    private readonly List<DungeonPendingCast> asuraMyriad = [];
    private readonly Dictionary<ulong, (DungeonActor Actor, bool Destroyed)> asuraActors = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, bool SourceDestroyed, bool TargetDestroyed, uint Action, DateTime At, int Key)> asuraBaits = [];
    private DungeonAmbientAoe? asuraFace;
    private DungeonAmbientAoe? asuraShrink;
    private bool asuraReduced;
    private int asuraKey;

    private void AsuraObserve()
    {
        if (!IsAsura || Active == null) return;
        foreach (var id in asuraActors.Keys.Where(id => !_actors.ContainsKey(id) && !asuraActors[id].Destroyed).ToArray()) AsuraRemoved(id);
        foreach (var actor in _actors.Values)
            asuraActors[actor.Id] = (actor, false);
        for (var i = 0; i < asuraBaits.Count; ++i)
        {
            var bait = asuraBaits[i];
            asuraBaits[i] = bait with
            {
                Source = !bait.SourceDestroyed && _actors.TryGetValue(bait.Source.Id, out var source) ? source : bait.Source,
                Target = !bait.TargetDestroyed && _actors.TryGetValue(bait.Target.Id, out var target) ? target : bait.Target,
            };
        }
    }

    private void AsuraCast(DungeonPendingCast cast)
    {
        if (!IsAsura || Active == null) return;
        var action = cast.Rule.Action;
        if (action is 36000 or 36003 && _actors.TryGetValue(cast.Source, out var source) && _actors.TryGetValue(cast.TargetId, out var target))
            asuraBaits.Add((source, target, false, false, action, cast.FinishAt, ++asuraKey));
        if (action is 36020 or 36021)
        {
            asuraMyriad.Add(cast);
            if (asuraMyriad.Count >= 12) asuraMyriad.Sort((a, b) => a.FinishAt.CompareTo(b.FinishAt));
        }
        if (action is 36011 or 36012 or 36013)
            asuraKhadga.Add(new($"asura/khadga/{++asuraKey}", "六连斩", new FanTemplate(20, MathF.PI),
                cast.Location, cast.Rotation, Activation: cast.FinishAt.AddSeconds(11.9f)));
        float? face = action switch
        {
            35989 or 35986 => MathF.PI,
            35987 or 35988 => -.003f * (MathF.PI / 180),
            35985 or 35984 => 89.999f * (MathF.PI / 180),
            _ => null,
        };
        if (face != null)
            asuraFace = new("asura/face", "脸谱半场斩", new FanTemplate(20, MathF.PI), cast.Location,
                face.Value, Activation: cast.FinishAt.AddSeconds(.1));
        if (action == 36001 && !asuraReduced && Arena != null)
            asuraShrink = new("asura/shrink", "缩场预告", new DonutTemplate(19, 20), Arena.Position,
                Activation: cast.FinishAt.AddSeconds(.8));
    }

    private void AsuraFinish(ulong source, uint action)
    {
        if (!IsAsura) return;
        var groupIndex = asuraMyriad.FindIndex(c => c.Source == source);
        if (groupIndex >= 0) asuraMyriad.RemoveAt(groupIndex);
        if (action == 35991) asuraLacerations.Clear();
        if (action is 36000 or 36003)
        {
            var target = _casts.GetValueOrDefault(source)?.TargetId;
            var index = asuraBaits.FindIndex(b => b.Action == action && (action == 36000 ? b.Source.Id == source : b.Target.Id == target));
            if (index >= 0) asuraBaits.RemoveAt(index);
        }
    }

    private void AsuraEffect(uint action)
    {
        if (!IsAsura || Active == null) return;
        if (action is >= 35977 and <= 35982 && asuraKhadga.Count > 0) asuraKhadga.RemoveAt(0);
        if (action is 36006 or 36007) asuraFace = null;
    }

    private void AsuraTimeline(ulong actor, uint timeline, DateTime at)
    {
        if (!IsAsura || Active == null || timeline != 0x11D6 || !_actors.TryGetValue(actor, out var source)) return;
        asuraLacerations.Add(new($"asura/laceration/{++asuraKey}", "幻影爆炸", new CircleTemplate(9),
            QuantizedDungeonPosition(source.Position), Activation: at.AddSeconds(7.1 - .5 * asuraLacerations.Count)));
    }

    private void AsuraMap(uint index, uint state)
    {
        if (IsAsura && Active != null && Arena != null && index == 1 && state == 0x00020001)
        {
            asuraReduced = true;
            asuraShrink = null;
            Arena = AsuraCatalog.Arena(Arena.Position.Y, reduced: true);
        }
    }

    private void AsuraRemoved(ulong id)
    {
        if (!IsAsura || !asuraActors.TryGetValue(id, out var actor) || actor.Destroyed) return;
        if (_casts.TryGetValue(id, out var cast)) AsuraFinish(id, cast.Rule.Action);
        asuraActors[id] = (actor.Actor, true);
        for (var i = 0; i < asuraBaits.Count; ++i)
        {
            var b = asuraBaits[i];
            asuraBaits[i] = b with { SourceDestroyed = b.SourceDestroyed || b.Source.Id == id, TargetDestroyed = b.TargetDestroyed || b.Target.Id == id };
        }
    }

    private void ResetAsura()
    {
        asuraLacerations.Clear(); asuraKhadga.Clear(); asuraMyriad.Clear(); asuraActors.Clear();
        asuraBaits.Clear(); asuraFace = asuraShrink = null; asuraReduced = false; asuraKey = 0;
    }
}
