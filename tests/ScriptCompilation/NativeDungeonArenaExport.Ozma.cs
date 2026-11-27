using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;

internal static partial class NativeDungeonArenaExport
{
    public static void RunIrminsul(string runtimePath, string destination)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(runtimePath));
        var config = assembly.GetType("BossMod.Service", true)!.GetField("Config")!.GetValue(null)!;
        config.GetType().GetMethod("Initialize")!.Invoke(config, null);
        var type = assembly.GetType("BossMod.Heavensward.Alliance.A12IrminsulSawtooth.A12IrminsulSawtooth", true)!;
        var bounds = type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = Activator.CreateInstance(assembly.GetType("BossMod.WPos")!, [0f, 130f])!;
        var output = new StringBuilder("// Generated from the real Irminsul arena using --irminsul-arenas.\n"
            + "using System.Numerics;\nusing MasterPlayer.Spatial.Geometry;\n\n"
            + "namespace MasterPlayer.Mechanics.Dungeons;\n\ninternal static class IrminsulArenas\n{\n");
        WriteAllianceBounds(output, bounds, center, "Ring");
        output.Append("}\n");
        File.WriteAllText(destination, output.ToString(), new UTF8Encoding(false));
    }

    public static void RunOzma(string runtimePath, string destination)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(runtimePath));
        var config = assembly.GetType("BossMod.Service", true)!.GetField("Config")!.GetValue(null)!;
        config.GetType().GetMethod("Initialize")!.Invoke(config, null);
        var moduleType = assembly.GetType("BossMod.Heavensward.Alliance.A24Ozma.A24Ozma", true)!;
        var componentType = assembly.GetType("BossMod.Heavensward.Alliance.A24Ozma.ArenaChanges", true)!;
        var tuple = moduleType.GetMethod("BuildArena")!.Invoke(null, null)!;
        var center = tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
        var initial = tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
        // These two source methods depend only on Module.Arena. Do not create a
        // manager or simulate phase lifecycle just to export their resolved data.
        var module = RuntimeHelpers.GetUninitializedObject(moduleType);
        var arena = Activator.CreateInstance(assembly.GetType("BossMod.MiniArena")!, [center, initial])!;
        moduleType.GetField("Arena")!.SetValue(module, arena);
        var component = Activator.CreateInstance(componentType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [module], null)!;
        var output = new StringBuilder("// Generated from real A24Ozma BuildArena/ArenaChanges using --ozma-arenas.\n"
            + "using System.Numerics;\nusing MasterPlayer.Spatial.Geometry;\n\n"
            + "namespace MasterPlayer.Mechanics.Dungeons;\n\ninternal static class OzmaArenas\n{\n");
        foreach (var name in new[] { "Initial", "Split", "Shade" })
        {
            if (name != "Initial")
                componentType.GetMethod(name == "Split" ? "HandleSplitArena" : "HandleOzmaShadeArena",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null);
            var bounds = arena.GetType().GetProperty("Bounds")!.GetValue(arena)!;
            var resolvedCenter = arena.GetType().GetProperty("Center")!.GetValue(arena)!;
            WriteAllianceBounds(output, bounds, resolvedCenter, name);
        }
        output.Append("}\n");
        File.WriteAllText(destination, output.ToString(), new UTF8Encoding(false));
    }

    private static void WriteAllianceBounds(StringBuilder output, dynamic bounds, dynamic center, string name)
    {
        dynamic polygon = bounds.Polygon;
        output.Append(FormattableString.Invariant($"    internal const float {name}Radius = {(float)bounds.Radius:R}f;\n"));
        output.Append(FormattableString.Invariant($"    internal static IGeometryShape {name}(float y) => new AllianceArenaGeometry(new({(float)center.X:R}f, y, {(float)center.Z:R}f),\n    [\n"));
        var contourCount = 0;
        foreach (dynamic part in polygon.Parts)
        {
            var points = ((IEnumerable)part.Vertices).Cast<object>().Select(p =>
                ((float)p.GetType().GetField("X")!.GetValue(p)!, (float)p.GetType().GetField("Z")!.GetValue(p)!)).ToArray();
            var starts = new List<int> { 0 };
            foreach (int start in part.HoleStarts) starts.Add(start);
            starts.Add(points.Length);
            for (var i = 0; i < starts.Count - 1; ++i)
            {
                output.Append("        [");
                output.Append(string.Join(", ", points[starts[i]..starts[i + 1]]
                    .Select(p => FormattableString.Invariant($"new({p.Item1:R}f, {p.Item2:R}f)"))));
                output.Append("],\n");
                ++contourCount;
            }
        }
        output.Append("    ],\n    [\n");
        var triangleCount = 0;
        foreach (dynamic t in polygon.Triangulate())
        {
            output.Append(FormattableString.Invariant($"        [new({(float)t.A.X:R}f, {(float)t.A.Z:R}f), new({(float)t.B.X:R}f, {(float)t.B.Z:R}f), new({(float)t.C.X:R}f, {(float)t.C.Z:R}f)],\n"));
            ++triangleCount;
        }
        output.Append("    ]);\n\n");
        Console.WriteLine($"Exported {name}: {contourCount} contours / {triangleCount} triangles.");
    }
}
