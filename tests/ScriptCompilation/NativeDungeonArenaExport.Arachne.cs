using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;

internal static partial class NativeDungeonArenaExport
{
    public static void RunArachne(string runtimePath, string destination)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(runtimePath));
        var config = assembly.GetType("BossMod.Service", true)!.GetField("Config")!.GetValue(null)!;
        config.GetType().GetMethod("Initialize")!.Invoke(config, null);
        var moduleType = assembly.GetType("BossMod.Heavensward.Alliance.A21ArachneEve.A21ArachneEve", true)!;
        var tuple = moduleType.GetMethod("BuildArena")!.Invoke(null, null)!;
        var center = tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
        var initial = tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
        var module = RuntimeHelpers.GetUninitializedObject(moduleType);
        var arena = Activator.CreateInstance(assembly.GetType("BossMod.MiniArena")!, [center, initial])!;
        moduleType.GetField("Arena")!.SetValue(module, arena);
        var componentType = assembly.GetType("BossMod.Heavensward.Alliance.A21ArachneEve.SpiderWeb", true)!;
        var component = Activator.CreateInstance(componentType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [module], null)!;
        var output = new StringBuilder("// Generated from real A21ArachneEve BuildArena/BuildSpiderweb using --arachne-arenas.\n"
            + "using System.Numerics;\nusing MasterPlayer.Spatial.Geometry;\n\n"
            + "namespace MasterPlayer.Mechanics.Dungeons;\n\ninternal static class ArachneArenas\n{\n");
        foreach (var name in new[] { "Ground", "Web" })
        {
            if (name == "Web")
                componentType.GetMethod("BuildSpiderweb", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null);
            var bounds = arena.GetType().GetProperty("Bounds")!.GetValue(arena)!;
            dynamic resolvedCenter = arena.GetType().GetProperty("Center")!.GetValue(arena)!;
            output.Append(FormattableString.Invariant($"    internal static readonly Vector3 {name}Center = new({(float)resolvedCenter.X:R}f, 0, {(float)resolvedCenter.Z:R}f);\n"));
            WriteAllianceBounds(output, bounds, (object)resolvedCenter, name);
        }
        output.Append("}\n");
        File.WriteAllText(destination, output.ToString(), new UTF8Encoding(false));
    }
}
