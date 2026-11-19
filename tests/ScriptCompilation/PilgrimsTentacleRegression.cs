using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static class PilgrimsTentacleRegression
{
    public static void Run(Assembly assembly)
    {
        var database = assembly.GetType("MasterPlayer.Dodge.AoeShapeDatabase")!;
        var lookup = database.GetMethod("TryGetShape", [typeof(uint), typeof(float), assembly.GetType("MasterPlayer.Dodge.AoeShapeDescriptor")!.MakeByRefType()])!;
        var bind = database.GetMethod("TryCreateBinding", BindingFlags.Static | BindingFlags.NonPublic)!;
        var create = database.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == "CreateTemplate" && m.GetParameters().Length == 1);
        var replayData = assembly.GetType("MasterPlayer.Replay.ReplayCastData")!;
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse", "tentacle-casts.json")));
        var count = 0;
        foreach (var sample in fixture.RootElement.GetProperty("casts").EnumerateArray())
        {
            var action = sample.GetProperty("action").GetUInt32();
            var facing = sample.GetProperty("castRotation").GetSingle();
            var sourceFacing = sample.GetProperty("sourceRotation").GetSingle();
            var pos = sample.GetProperty("position").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            var origin = new Vector3(pos[0], pos[1], pos[2]);
            object?[] query = [action, 20f, null];
            Check((bool)lookup.Invoke(null, query)!, "Tentacles need verified entries without generic inference.");
            dynamic descriptor = query[2]!;
            Check((float)descriptor.Param1 == 60 && Math.Abs((float)descriptor.Param2 - MathF.PI) < .00001f
                && (bool)descriptor.UseCastRotation && !(bool)descriptor.FollowCaster, "Tentacles retain R60/180 degrees and locked facing, without hitbox inflation.");
            foreach (var laterFacing in new[] { sourceFacing, sourceFacing + .7f })
            {
                var cast = new LogSystemCastStartEvent
                {
                    SourceId = 1, ActionId = (ushort)action, EffectPosition = origin, SourceRotation = laterFacing,
                    Raw = Activator.CreateInstance(replayData, [facing]),
                };
                object?[] bindingArgs = [cast, (object)descriptor, null, 1284u];
                Check((bool)bind.Invoke(null, bindingArgs)!, "Recorded locked facing must bind without live objects.");
                dynamic pose = bindingArgs[2]!;
                Check(pose.GetType().Name == "StaticTransform" && (Vector3)pose.Position == origin && (float)pose.Rotation == facing,
                    "Both sides must use packet pose unchanged, including after the caster turns.");
                dynamic template = create.Invoke(null, [(object)descriptor])!;
                dynamic shape = template.Resolve((Vector3)pose.Position, (float)pose.Rotation, null);
                Vector3 Point(float angle, float radius) => origin + new Vector3(MathF.Sin(angle) * radius, 0, MathF.Cos(angle) * radius);
                Check((bool)shape.Contains(Point(facing, 59.9f), 0f) && !(bool)shape.Contains(Point(facing, 60.1f), 0f)
                    && !(bool)shape.Contains(Point(facing + MathF.PI, 30), 0f), "Recorded dangerous and safe sides and physical radius must agree.");
                foreach (var side in new[] { -1f, 1f })
                    Check((bool)shape.Contains(Point(facing + side * (MathF.PI / 2 - .01f), 30), 0f)
                        && !(bool)shape.Contains(Point(facing + side * (MathF.PI / 2 + .01f), 30), 0f), "Both 180-degree angular edges must match the packet facing.");
                cast = cast with { Raw = Activator.CreateInstance(replayData, new object?[] { null }) };
                bindingArgs = [cast, (object)descriptor, null, 1284u];
                Check(!(bool)bind.Invoke(null, bindingArgs)!, "Missing locked facing must not silently substitute ordinary facing.");
            }
            ++count;
        }
        Check(count == 12, "All twelve recorded left/right telegraphs must be checked.");
        Console.WriteLine("PASS Pilgrims floor 31-40 tentacles: 12 ARR casts, locked left/right half-planes, R60 edges, turning caster and missing-facing rejection");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
