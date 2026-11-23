using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> holminsterSeenCasts = [];
    private readonly Dictionary<ulong, DateTime> holminsterCharges = [];
    private readonly Dictionary<ulong, DateTime> holminsterStacks = [];
    private readonly Dictionary<ulong, DateTime> holminsterFlares = [];
    private readonly List<DungeonAmbientAoe> holminsterWater = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> holminsterDissonanceCharges = [];
    private readonly List<HolminsterExaflare> holminsterExas = [];
    private readonly List<(ulong Source, DungeonAmbientAoe Aoe)> holminsterExaPredictions = [];
    private DungeonAmbientAoe? holminsterAethersup;
    private DungeonAmbientAoe? holminsterCat;
    private (ulong Source, ulong Target, DateTime Activation)? holminsterLineStack;
    private ulong holminsterChainTarget;
    private bool holminsterChainCasting;
    private bool holminsterChained;
    private int holminsterAethersupHits;
    private int holminsterCatRemaining;
    private int holminsterExaStarts;
    private int holminsterExaId;

    private DungeonActor? HolminsterChain => Active?.BossOid == 0x278C
        ? _actors.Values.FirstOrDefault(a => a.Oid == 0x2895 && !a.Dead) : null;
    private bool HolminsterBossForbidden(DungeonActor actor) => Active?.BossOid == 0x278C && holminsterChained
        && PlayerId != holminsterChainTarget && actor.Oid == 0x278C;

    private void HolminsterCast(DungeonPendingCast cast)
    {
        if (territory != HolminsterCatalog.TERRITORY || !holminsterSeenCasts.Add((cast.Source, cast.Rule.Action, cast.StartedAt))) return;
        if (cast.Rule.Action == 15814)
        {
            var origin = _actors[cast.Source].Position;
            var delta = cast.Location - origin; delta.Y = 0;
            holminsterDissonanceCharges[cast.Source] = new($"holminster/charge/{cast.Source}", "直线冲锋",
                RayRectangleTemplate.Symmetric(8, Math.Max(.01f, delta.Length())), origin,
                MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt);
        }
        if (cast.Rule.Action == 15848)
        {
            holminsterAethersupHits = 0;
            holminsterAethersup = new("holminster/aethersup", "连续四次吸收", new FanTemplate(24, MathF.PI * 2 / 3),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        if (cast.Rule.Action == 15834)
        {
            holminsterCatRemaining = 8;
            holminsterCat = new("holminster/cat", "八段尾击", new FanTemplate(25, MathF.PI * 2 / 3),
                cast.Location, cast.Rotation + MathF.PI, Activation: cast.FinishAt);
        }
        if (cast.Rule.Action == 15837)
            HolminsterAddExa(cast.Source, _actors[cast.Source].Position, cast.Rotation, cast.FinishAt, cast.StartedAt);
    }

    private void HolminsterFinish(DungeonPendingCast cast, bool completed, DateTime time)
    {
        if (territory != HolminsterCatalog.TERRITORY) return;
        if (cast.Rule.Action == 15814) holminsterDissonanceCharges.Remove(cast.Source);
        if (cast.Rule.Action == 17052) holminsterChainCasting = false;
        if (completed && cast.Rule.Action == 15841 && holminsterCat != null)
        {
            if (--holminsterCatRemaining == 0) holminsterCat = null;
            else holminsterCat = holminsterCat with { Rotation = holminsterCat.Rotation - MathF.PI / 4, Activation = time.AddSeconds(2) };
        }
        if (completed) return;
        if (cast.Rule.Action == 15848) { holminsterAethersup = null; holminsterAethersupHits = 0; }
        if (cast.Rule.Action == 15834) { holminsterCat = null; holminsterCatRemaining = 0; }
        if (cast.Rule.Action == 15829) holminsterCharges.Clear();
        if (cast.Rule.Action == 15837)
        {
            holminsterExas.RemoveAll(e => e.Source == cast.Source);
            holminsterExaPredictions.RemoveAll(e => e.Source == cast.Source);
        }
    }

    private void HolminsterIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x278B)
        {
            if (icon is >= 79 and <= 82) holminsterCharges.TryAdd(target, time.AddSeconds(8.3 + holminsterCharges.Count * .3));
            if (icon == 62) holminsterStacks.TryAdd(target, time.AddSeconds(5.1));
        }
        if (Active?.BossOid != 0x278C) return;
        if (icon == 87) holminsterFlares.TryAdd(target, time.AddSeconds(5.1));
        if (icon == 92) { holminsterChainTarget = target; holminsterChainCasting = true; holminsterChained = false; }
    }

    private void HolminsterEffect(ulong source, uint action, ulong target, Vector3? location, float? rotation, DateTime time)
    {
        if (territory != HolminsterCatalog.TERRITORY) return;
        if (action == 15830 && holminsterCharges.Count > 0) holminsterCharges.Remove(holminsterCharges.Keys.First());
        if (action == 15827) holminsterStacks.Clear();
        if (action == 16790) holminsterFlares.Clear();
        if (action == 15828 && (_actors.GetValueOrDefault(target)?.Position ?? location) is { } water)
            holminsterWater.Add(new($"holminster/water/event/{source}/{time.Ticks}", "圣水持续区预告", new CircleTemplate(6),
                water, Activation: time.AddSeconds(.8)));
        if (action is 15848 or 15849 && ++holminsterAethersupHits == 4)
        {
            holminsterAethersup = null; holminsterAethersupHits = 0;
        }
        if (action == 15844) holminsterLineStack = (source, target, time.AddSeconds(5.3));
        if (action == 15845) holminsterLineStack = null;
        if (action == 15838 && _actors.TryGetValue(source, out var helper))
            HolminsterAddExa(source, helper.Position, rotation ?? helper.Rotation, time.AddSeconds(1), time);
        if (action is not (15837 or 15838 or 15839)) return;
        Vector3? impact = action == 15839 ? location : _actors.GetValueOrDefault(source)?.Position;
        if (impact is not { } position) return;
        var index = holminsterExas.FindIndex(e => new Vector2(e.Next.X - position.X, e.Next.Z - position.Z).LengthSquared() <= 1);
        if (index < 0) return;
        var line = holminsterExas[index];
        if (line.Remaining == 1) holminsterExas.RemoveAt(index);
        else holminsterExas[index] = line with { Next = position + line.Advance, Activation = time.AddSeconds(1), Remaining = line.Remaining - 1 };
    }

    private void HolminsterAddExa(ulong source, Vector3 origin, float rotation, DateTime activation, DateTime time)
    {
        var advance = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * 2.5f;
        holminsterExas.Add(new(++holminsterExaId, source, origin, advance, activation, 7));
        ++holminsterExaStarts;
        if (holminsterExaPredictions.Count > 0 && holminsterExaStarts > 2) holminsterExaPredictions.RemoveAt(0);
        if (holminsterExaStarts <= 14)
        {
            var p = QuantizedDungeonPosition(origin) - new Vector3(134, origin.Y, -465);
            var c = MathF.Sqrt(.5f);
            var nextOrigin = new Vector3(134 + c * (p.X - p.Z), origin.Y, -465 + c * (p.X + p.Z));
            holminsterExaPredictions.Add((source, new($"holminster/exa/prediction/{holminsterExaId}", "下一组地火预告",
                new CircleTemplate(4), nextOrigin, Activation: time.AddSeconds(3.7))));
        }
        if (holminsterExaStarts == 16) holminsterExaStarts = 0;
    }

    private void SynchronizeHolminster()
    {
        if (territory != HolminsterCatalog.TERRITORY) return;
        foreach (var zones in new[] { holminsterCharges, holminsterStacks, holminsterFlares })
            foreach (var id in zones.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) zones.Remove(id);
        var pools = _actors.Values.Where(a => a.Oid == 0x1EABF9 && a.EventState != 7).ToArray();
        holminsterWater.RemoveAll(p => pools.Any(a => new Vector2(p.Position.X - a.Position.X, p.Position.Z - a.Position.Z).LengthSquared() <= 4));
        if (holminsterChainTarget != 0 && _actors.TryGetValue(holminsterChainTarget, out var target) && !target.Dead)
        {
            if (HasStatus(target, 1849)) holminsterChained = true;
            else if (!holminsterChainCasting) { holminsterChainTarget = 0; holminsterChained = false; }
        }
        else if (holminsterChainTarget != 0) { holminsterChainTarget = 0; holminsterChained = false; }
        if (holminsterLineStack is { } stack && (!_actors.TryGetValue(stack.Target, out var lineTarget) || lineTarget.Dead))
            holminsterLineStack = null;
    }

    private void HolminsterStatus(ulong target, uint status, bool present)
    {
        if (Active?.BossOid != 0x278C || status != 1849 || target != holminsterChainTarget) return;
        if (present) holminsterChained = true;
        else if (!holminsterChainCasting) { holminsterChainTarget = 0; holminsterChained = false; }
    }

    private void HolminsterRemoved(ulong source)
    {
        holminsterCharges.Remove(source); holminsterStacks.Remove(source); holminsterFlares.Remove(source);
        holminsterDissonanceCharges.Remove(source);
        if (holminsterChainTarget == source) { holminsterChainTarget = 0; holminsterChained = false; }
        if (holminsterLineStack is { } stack && (stack.Source == source || stack.Target == source)) holminsterLineStack = null;
    }

    private void ResetHolminster()
    {
        holminsterSeenCasts.Clear(); holminsterCharges.Clear(); holminsterStacks.Clear(); holminsterFlares.Clear(); holminsterWater.Clear();
        holminsterDissonanceCharges.Clear();
        holminsterExas.Clear(); holminsterExaPredictions.Clear(); holminsterAethersup = holminsterCat = null; holminsterLineStack = null;
        holminsterChainTarget = 0; holminsterChainCasting = holminsterChained = false; holminsterCatRemaining = 0;
        holminsterAethersupHits = 0; holminsterExaStarts = holminsterExaId = 0;
    }
}
