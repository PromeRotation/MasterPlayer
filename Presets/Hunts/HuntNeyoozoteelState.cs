using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntNeyoozoteelState(ulong owner)
{
    private static readonly GeometryTemplate cone = new FanTemplate(40, 2 * MathF.PI / 3);
    private static readonly GeometryTemplate cross = new LocalPolygonTemplate([
        new(-3, -30), new(3, -30), new(3, -3), new(30, -3), new(30, 3), new(3, 3),
        new(3, 30), new(-3, 30), new(-3, 3), new(-30, 3), new(-30, -3), new(-3, -3)]);
    private readonly List<(HuntPendingCast Cast, bool UsesCastRotation)> sap = [];
    private readonly List<(HuntPendingCast Cast, float Increment, int Remaining)> roots = [];
    private readonly List<HuntPendingCast> rootAreas = [];
    private readonly List<HuntStackState> stacks = [];
    private int version;
    private int lastVersion = -1;
    private int lastCount = -1;
    internal IEnumerable<HuntStackState> Stacks => stacks;
    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (sap.Count > 1)
                sap[0] = (sap[0].Cast with { Color = new(1, 1, 0, 1) }, sap[0].UsesCastRotation);
            return sap.Select(s => s.Cast).Concat(rootAreas).Select(c => c with { Source = owner });
        }
    }

    internal static bool SapHit(uint action) => action is 37370 or 37371 or 42172 or 42173 or 42174 or 37394 or 37395 or 37396;
    internal static bool RootsHit(uint action) => action is 37373 or 37374 or 37375;

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast.Rule.Action == 37307 && actors.ContainsKey(cast.TargetId))
        {
            var stack = new HuntStackState(int.MaxValue, 5);
            stack.Start(cast with { Source = owner }, actors, []);
            stacks.Add(stack);
        }
        if (RootsHit(cast.Rule.Action) && cast.Rule.Action != 37375)
            roots.Add((cast with { Shape = cross, Stage = 0 }, cast.Rule.Action == 37373 ? -MathF.PI / 4 : MathF.PI / 4, 8));
        float[] angles = cast.Rule.Action switch
        {
            37377 => [MathF.PI, -MathF.PI / 2, -MathF.PI / 2],
            37376 => [MathF.PI / 2, MathF.PI, -MathF.PI / 2],
            37378 => [-MathF.PI / 2, MathF.PI / 2, MathF.PI],
            37379 => [MathF.PI / 2, MathF.PI, MathF.PI / 2],
            _ => []
        };
        var initialCount = sap.Count;
        for (var i = 0; i < angles.Length; ++i)
        {
            // The source indexes the entire existing queue, even when another Omen overlaps.
            var rotation = (i == 0 ? cast.Rotation : sap[i - 1].Cast.Rotation) + angles[i];
            var usesRotation = i == 0 || i - 1 >= initialCount && sap[i - 1].UsesCastRotation;
            sap.Add((cast with
            {
                Shape = cone, Location = actors[owner].Position, Rotation = rotation,
                Activation = cast.Activation.AddSeconds(14.6f + 2.2f * i), Stage = i,
                SequenceId = $"{owner}/neyoozoteel/sap/{cast.Order}/{i}"
            }, usesRotation));
        }
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        for (var i = 0; i < sap.Count; ++i)
        {
            var (cast, usesRotation) = sap[i];
            if (cast.Source != previous.Source || cast.Order != previous.Order) continue;
            sap[i] = (cast with
            {
                Rotation = cast.Rotation + (usesRotation ? updated.Rotation - previous.Rotation : 0),
                Activation = updated.Activation.AddSeconds(14.6f + 2.2f * cast.Stage), Input = updated.Input
            }, usesRotation);
        }
        for (var i = 0; i < roots.Count; ++i)
        {
            var (cast, increment, remaining) = roots[i];
            if (cast.Source != previous.Source || cast.Order != previous.Order) continue;
            roots[i] = (cast with
            {
                Location = updated.Location, Rotation = cast.Rotation + updated.Rotation - previous.Rotation,
                Activation = cast.Stage == 0 ? updated.Activation : cast.Activation, Input = updated.Input
            }, increment, remaining);
            ++version;
        }
        foreach (var stack in stacks.Where(s => s.Cast?.Order == previous.Order).ToArray())
        {
            stack.Correct(previous, updated with { Source = owner }, actors);
            if (stack.TargetId == 0) stacks.Remove(stack);
        }
    }

    internal void Effect(uint action, DateTime at)
    {
        if (SapHit(action) && sap.Count != 0) sap.RemoveAt(0);
        if (!RootsHit(action)) return;
        ++version;
        if (roots.Count == 0) return;
        var (cast, increment, remaining) = roots[0];
        if (--remaining == 0) roots.RemoveAt(0);
        else roots[0] = (cast with
        {
            Rotation = cast.Rotation + increment, Activation = at.AddSeconds(2.4f), Stage = cast.Stage + 1
        }, increment, remaining);
    }

    internal void FinishStack(uint action, ulong target)
    {
        if (action != 37307) return;
        var index = stacks.FindIndex(s => s.TargetId == target);
        if (index >= 0) stacks.RemoveAt(index);
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        foreach (var stack in stacks) stack.Update(actors);
        if (lastCount == roots.Count && lastVersion == version) return;
        lastCount = roots.Count;
        lastVersion = version;
        rootAreas.Clear();
        foreach (var (cast, increment, remaining) in roots)
        {
            if (remaining > 1)
                rootAreas.Add(cast with
                {
                    Rotation = cast.Rotation + increment,
                    Activation = (cast.Activation > now ? cast.Activation : now).AddSeconds(2.4f),
                    Color = null, SequenceId = $"{owner}/neyoozoteel/roots/{cast.Order}/next"
                });
            rootAreas.Add(cast with
            {
                Color = remaining > 1 ? new Vector4(1, 1, 0, 1) : null,
                SequenceId = $"{owner}/neyoozoteel/roots/{cast.Order}/current"
            });
        }
    }

    internal void Remove(ulong actor)
    {
        foreach (var stack in stacks) stack.Remove(actor);
    }
}
