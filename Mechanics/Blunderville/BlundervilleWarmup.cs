using System.Reflection;
using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Blunderville;

// 23:48 ARR: the first lane-planner search of a race stalled the game thread
// ~70ms compiling it (later scans p99 ~10ms). The script compiles every course
// planner on a worker as soon as it starts.
internal static class BlundervilleWarmup
{
    public static void Run()
    {
        // The planners expand layers with Parallel.For: start the pool's workers too.
        Parallel.For(0, Environment.ProcessorCount, _ => { });
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Type?[] types;
        try { types = typeof(BlundervilleWarmup).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }
        foreach (var type in types)
        {
            if (type == null || type.Namespace != typeof(BlundervilleWarmup).Namespace || type.ContainsGenericParameters) continue;
            foreach (var method in type.GetMethods(all))
            {
                if (method.IsAbstract || method.ContainsGenericParameters) continue;
                try { RuntimeHelpers.PrepareMethod(method.MethodHandle); }
                catch { }
            }
        }
    }
}
