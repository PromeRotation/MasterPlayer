using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntComboState
{
    private readonly List<HuntPendingCast> queue = [];
    private int serial;
    internal IEnumerable<HuntPendingCast> Areas => queue;

    internal void Start(HuntPendingCast cast, ReplayActorState actor)
    {
        var action = cast.Rule.Action;
        if (cast.Definition.Oid == 0x2854 && action is 17423 or 17424)
        {
            var shape = new FanTemplate(30, MathF.PI);
            Add(cast, shape, cast.Location, cast.Rotation, cast.Activation, 0);
            Add(cast, shape, cast.Location, cast.Rotation + MathF.PI, cast.Activation.AddSeconds(2.3f), 1);
        }
        else if (cast.Definition.Oid == 0x28B5 && action is 17495 or 17497 or 17498)
        {
            GeometryTemplate shape = action switch
            {
                17495 => RayRectangleTemplate.Symmetric(10, 15),
                17497 => new CircleTemplate(6),
                _ => new DonutTemplate(10, 20)
            };
            Add(cast, shape, cast.Location, cast.Rotation, cast.Activation, 0);
            var origin = (action == 17497 ? actor.Position : cast.Location)
                - new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 40;
            Add(cast, RayRectangleTemplate.Symmetric(10, 80), origin,
                action == 17497 ? actor.Rotation : cast.Rotation, cast.Activation.AddSeconds(3.1), 1);
        }
        Refresh(cast.Definition.Oid);
    }

    internal void Finish(uint oid, uint action)
    {
        if (oid == 0x2854 && action is 17423 or 17424) Pop(oid);
    }

    internal void Effect(uint oid, uint action, DateTime at)
    {
        if (queue.Count == 0 || at < queue[0].StartedAt) return;
        if (oid == 0x2854 && action is 17421 or 17422
            || oid == 0x28B5 && action is 18042 or 17495 or 17497 or 17498) Pop(oid);
    }

    internal void Update(ReplayActorState actor)
    {
        if (actor.Oid != 0x28B5 || queue.Count < 2 || queue[0].Shape is not CircleTemplate) return;
        var origin = actor.Position - new Vector3(MathF.Sin(actor.Rotation), 0, MathF.Cos(actor.Rotation)) * 40;
        const float gridSize = 2000f / 65535f;
        const float gridSizeInv = 1f / gridSize;
        origin = new(((int)MathF.Round(origin.X * gridSizeInv) - .5f) * gridSize, origin.Y,
            ((int)MathF.Round(origin.Z * gridSizeInv) - .5f) * gridSize);
        if (queue[1].Location != origin || queue[1].Rotation != actor.Rotation)
            queue[1] = queue[1] with { Location = origin, Rotation = actor.Rotation };
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, ReplayActorState actor)
    {
        var corrected = new HuntComboState();
        corrected.Start(updated, actor);
        foreach (var index in Enumerable.Range(0, queue.Count).Where(i => queue[i].Input == previous.Input).ToArray())
        {
            var old = queue[index];
            if (old.Stage < 0 || old.Stage >= corrected.queue.Count) continue;
            queue[index] = corrected.queue[old.Stage] with { SequenceId = old.SequenceId };
        }
        Refresh(updated.Definition.Oid);
    }

    private void Add(HuntPendingCast cast, GeometryTemplate shape, Vector3 origin, float rotation, DateTime activation, int stage)
        => queue.Add(cast with
        {
            Shape = shape, Location = origin, Rotation = rotation, Activation = activation,
            SequenceId = $"{cast.Source}/combo/{serial++}", Stage = stage
        });

    private void Pop(uint oid)
    {
        if (queue.Count == 0) return;
        queue.RemoveAt(0);
        Refresh(oid);
    }

    private void Refresh(uint oid)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            Vector4? color = i == 0 && queue.Count > 1 ? new(1, 1, 0, 1) : null;
            var risky = oid != 0x2854 || i == 0;
            if (queue[i].Color != color || queue[i].Risky != risky)
                queue[i] = queue[i] with { Color = color, Risky = risky };
        }
    }
}
