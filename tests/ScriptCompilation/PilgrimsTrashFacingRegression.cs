using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static class PilgrimsTrashFacingRegression
{
    public static void Run(Assembly assembly)
    {
        var database = assembly.GetType("MasterPlayer.Dodge.AoeShapeDatabase")!;
        var descriptorType = assembly.GetType("MasterPlayer.Dodge.AoeShapeDescriptor")!;
        var kindType = assembly.GetType("MasterPlayer.Dodge.AoeShapeKind")!;
        var replayData = assembly.GetType("MasterPlayer.Replay.ReplayCastData")!;
        var bind = database.GetMethod("TryCreateBinding", BindingFlags.Static | BindingFlags.NonPublic)!;
        var origin = new Vector3(10, 0, 20);
        var source = new Vector3(7, 0, 8);
        var packet = Activator.CreateInstance(replayData, [.8f]);
        var noPacket = Activator.CreateInstance(replayData, new object?[] { null });
        var cast = new LogSystemCastStartEvent { SourceId = 1, ActionId = 42091, EffectPosition = origin, SourcePosition = source, SourceRotation = -2f, Raw = packet };
        object Descriptor(string kind) => Activator.CreateInstance(descriptorType, [Enum.Parse(kindType, kind), 20f, MathF.PI, true, 0f])!;
        object? Binding(LogSystemCastStartEvent ev, object descriptor, uint territory)
        {
            object?[] args = [ev, descriptor, null, territory];
            return (bool)bind.Invoke(null, args)! ? args[2] : null;
        }

        foreach (var kind in new[] { "Fan", "Rectangle", "RayRectangle" })
        {
            var descriptor = Descriptor(kind);
            for (uint territory = 1281; territory <= 1290; ++territory)
            {
                foreach (var ordinaryFacing in new[] { -.8f, 0f, 2f })
                {
                    var binding = Binding(cast with { SourceRotation = ordinaryFacing }, descriptor, territory)
                        ?? throw new InvalidOperationException("Locked cast facing must produce a binding.");
                    dynamic pose = binding;
                    Check(binding.GetType().Name == "StaticTransform" && (Vector3)pose.Position == origin && (float)pose.Rotation == .8f,
                        "Every Pilgrims floor's directional cast must freeze packet location/facing, regardless of actor turns.");
                }
                Check(Binding(cast with { Raw = noPacket }, descriptor, territory) == null,
                    "Missing locked facing must not fall back to ordinary facing.");
            }
            foreach (var outside in new[] { 0u, 1280u, 1291u })
                Check(Binding(cast with { Raw = noPacket }, descriptor, outside)?.GetType().Name == "FollowEntityPositionAndRotation",
                    "The regional fix must preserve unrelated duties' binding behavior.");
        }

        foreach (var kind in new[] { "Circle", "Donut" })
            Check(Binding(cast with { Raw = noPacket }, Descriptor(kind), 1284)?.GetType().Name == "FollowEntityPositionAndRotation",
                "Ordinary circle/donut centers keep their existing following behavior.");
        var charge = Descriptor("RayRectangle");
        descriptorType.GetProperty("IsCharge")!.SetValue(charge, true);
        dynamic chargePose = Binding(cast with { Raw = noPacket }, charge, 1284)
            ?? throw new InvalidOperationException("A charge must produce a binding without packet facing.");
        var delta = origin - source;
        Check((Vector3)chargePose.Position == source && (float)chargePose.Rotation == MathF.Atan2(delta.X, delta.Z),
            "Charges keep source-to-destination orientation and do not require an unrelated packet angle.");

        var lookup = database.GetMethod("TryGetShape", [typeof(uint), typeof(float), descriptorType.MakeByRefType()])!;
        object?[] query = [42091u, 20f, null];
        Check((bool)lookup.Invoke(null, query)!, "Earthen Auger has an exact entry independent of generic inference.");
        var create = database.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == "CreateTemplate" && m.GetParameters().Length == 1);
        dynamic template = create.Invoke(null, [query[2]])!;
        dynamic ring = template.Resolve(origin, .8f, null);
        Vector3 Point(float angle, float radius) => origin + new Vector3(MathF.Sin(angle) * radius, 0, MathF.Cos(angle) * radius);
        Check(!(bool)ring.Contains(Point(.8f, 2.9f), 0f) && (bool)ring.Contains(Point(.8f, 3.1f), 0f)
            && (bool)ring.Contains(Point(.8f, 29.9f), 0f) && !(bool)ring.Contains(Point(.8f, 30.1f), 0f)
            && !(bool)ring.Contains(Point(.8f + MathF.PI, 10f), 0f), "Earthen Auger preserves the R3 inner hole, R30 edge and rear safe sector.");
        foreach (var side in new[] { -1f, 1f })
            Check((bool)ring.Contains(Point(.8f + side * (MathF.PI * .75f - .01f), 10), 0f)
                && !(bool)ring.Contains(Point(.8f + side * (MathF.PI * .75f + .01f), 10), 0f), "Both 270-degree ring-sector edges must retain the locked direction.");
        Console.WriteLine("PASS Pilgrims trash facing: all 10 territories, 3 directional shapes, actor turns, missing-facing rejection, charge/circle exceptions, outside-duty isolation and R3-30/270-degree Earthen Auger");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
