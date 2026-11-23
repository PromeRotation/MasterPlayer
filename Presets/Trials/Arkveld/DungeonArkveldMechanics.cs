using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsArkveld => territory == ArkveldCatalog.TERRITORY;
    private readonly Dictionary<ulong, ArkveldActorReference> arkActors = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> arkCharges = [];
    private readonly List<DungeonPendingCast> arkTowers = [];
    private readonly List<(ArkveldActorReference Target, ulong Source, DateTime At, int Key)> arkSpreads = [];
    private readonly List<(ArkveldActorReference Target, DateTime At, int Key)> arkStacks = [];
    private readonly List<(ulong Source, Vector3 Position, DateTime At, int Left, int Key)> arkVengeance = [];
    private readonly List<DungeonAmbientAoe> arkPulses = [];
    private DungeonAmbientAoe? arkLane;
    private int arkStackHits;
    private int arkKey;

    private void ArkObserve()
    {
        if (!IsArkveld || Active == null) return;
        foreach (var id in arkActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) ArkRemoved(id);
        foreach (var actor in _actors.Values)
            if (arkActors.TryGetValue(actor.Id, out var reference)) reference.Actor = actor;
            else arkActors[actor.Id] = new(actor);
        ArkTick();
    }

    private void ArkCast(DungeonPendingCast cast)
    {
        if (!IsArkveld || Active == null || !arkActors.TryGetValue(cast.Source, out var source)) return;
        var action = cast.Rule.Action;
        if (action == 43842)
        {
            var delta = cast.Location - source.Actor.Position;
            arkCharges[cast.Source] = new($"ark/rush/{cast.Source}", "突进", RayRectangleTemplate.Symmetric(12,
                new Vector2(delta.X, delta.Z).Length()), QuantizedDungeonPosition(source.Actor.Position),
                MathF.Atan2(delta.X, delta.Z), Activation: cast.FinishAt);
        }
        if (action is 43853 or 43854) arkTowers.Add(cast);
        if (action == 43866 && arkActors.TryGetValue(cast.TargetId, out var target))
            arkSpreads.Add((target, cast.Source, cast.FinishAt, ++arkKey));
        if (action == 43860)
            arkVengeance.Add((cast.Source, source.Actor.Position, cast.FinishAt, 2, ++arkKey));
        if (cast.Source == PrimaryId && action is 45046 or 45047)
        {
            var forward = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
            var left = new Vector3(-forward.Z, 0, forward.X);
            var lateralShift = action == 45047 ? -29.5f : 29.5f;
            // 源先把 origin 向后移20，再创建前60/后20矩形；保留这两次后向覆盖。
            var position = source.Actor.Position + lateralShift * left - forward * 20;
            arkLane = new("ark/lane", "龙之恩惠偏侧扫射", new RectangleTemplate(61, 80),
                position + forward * 20, cast.Rotation, Activation: cast.FinishAt);
        }
    }

    private void ArkFinish(ulong source, uint action, ulong target)
    {
        if (!IsArkveld) return;
        if (action == 43842) arkCharges.Remove(source);
        if (action is 43853 or 43854)
        {
            var i = arkTowers.FindIndex(c => c.Rule.Action == action && c.Source == source);
            if (i >= 0) arkTowers.RemoveAt(i);
        }
        if (action == 43866)
        {
            var i = arkSpreads.FindIndex(s => s.Target.Actor.Id == target);
            if (i >= 0) arkSpreads.RemoveAt(i);
        }
        if (source == PrimaryId && action is 45046 or 45047) arkLane = null;
    }

    private void ArkIcon(ulong actor, uint icon, DateTime at)
    {
        if (IsArkveld && Active != null && icon == 100 && arkActors.TryGetValue(actor, out var target))
            arkStacks.Add((target, at.AddSeconds(8.4), ++arkKey));
    }

    private void ArkEffect(ulong source, uint action, ulong target, Vector3? location, DateTime at)
    {
        if (!IsArkveld || Active == null) return;
        if (action == 43883 && ++arkStackHits == 1 && arkStacks.Count > 0)
        {
            var i = arkStacks.FindIndex(s => s.Target.Actor.Id == target);
            arkStacks.RemoveAt(i >= 0 ? i : 0); arkStackHits = 0;
        }
        if (action == 43861)
        {
            var i = arkVengeance.FindIndex(v => v.Source == source);
            if (i >= 0)
            {
                var line = arkVengeance[i];
                if (line.Left == 1) arkVengeance.RemoveAt(i);
                else arkVengeance[i] = line with { Position = location ?? Vector3.Zero, At = at.AddSeconds(1.6), Left = line.Left - 1 };
            }
        }
        if (action == 43875 && arkActors.TryGetValue(source, out var caster))
            arkPulses.Add(new($"ark/pulse/{++arkKey}", "龙之恩惠脉冲", RayRectangleTemplate.Symmetric(6, 60),
                caster.Actor.Position, caster.Actor.Rotation, Activation: at.AddSeconds((double).8f)));
    }

    private void ArkTick()
    {
        arkPulses.RemoveAll(a => a.Activation <= _now);
        if (arkLane?.Activation <= _now) arkLane = null;
    }

    private void ArkRemoved(ulong source)
    {
        if (!IsArkveld || !arkActors.Remove(source)) return;
        if (_casts.TryGetValue(source, out var cast)) ArkFinish(source, cast.Rule.Action, cast.TargetId);
    }

    private void ResetArk()
    {
        arkActors.Clear(); arkCharges.Clear(); arkTowers.Clear(); arkSpreads.Clear(); arkStacks.Clear();
        arkVengeance.Clear(); arkPulses.Clear(); arkLane = null; arkStackHits = arkKey = 0;
    }
}
