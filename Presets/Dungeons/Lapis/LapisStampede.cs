using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class LapisStampede(Vector3 position, float rotation)
{
    internal Vector3 Position { get; } = position;
    internal float Rotation { get; } = rotation;
    internal int Count { get; set; }
    internal DateTime? ResetAt { get; set; }
    private readonly List<DungeonActor> beasts = [];

    internal DungeonAmbientAoe Update(string id, IEnumerable<DungeonActor> actors)
    {
        var byId = actors.ToDictionary(a => a.Id);
        for (var i = 0; i < beasts.Count; ++i)
            if (byId.TryGetValue(beasts[i].Id, out var actor)) beasts[i] = actor;
        foreach (var actor in byId.Values.Where(a => a.Oid is 0x3CFF or 0x3D00 or 0x3D02 or 0x3D01))
        {
            // The source retains the arrival collection after an animal leaves or is destroyed.
            var offset = actor.Position - Position;
            var forward = offset.X * MathF.Sin(Rotation) + offset.Z * MathF.Cos(Rotation);
            var side = offset.X * MathF.Cos(Rotation) - offset.Z * MathF.Sin(Rotation);
            if (forward is >= -10 and <= 0 && MathF.Abs(side) <= 5 && beasts.All(b => b.Id != actor.Id)) beasts.Add(actor);
        }
        if (beasts.Count == 0)
            return new(id, "兽群通道", new RayRectangleTemplate(-5, 5, 40),
                Position - new Vector3(MathF.Sin(89.999f * MathF.PI / 180), 0, MathF.Cos(89.999f * MathF.PI / 180)) * 20,
                89.999f * MathF.PI / 180);
        var delta = beasts[0].Position - beasts[^1].Position;
        var length = new Vector2(delta.X, delta.Z).Length() + 30;
        return new(id, "动态兽群通道", RayRectangleTemplate.Symmetric(10, length),
            new(beasts[^1].Position.X, Position.Y, Position.Z), Rotation);
    }
}
