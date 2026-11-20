using System.IO.Compression;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    private const BindingFlags MEMBERS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly string[] REQUIRED_FILES = [
        "MasterPlayer.dll", "MasterPlayer.MechanicRuntime.dll", "MasterPlayer.deps.json",
        "DotRecast.Core.dll", "DotRecast.Recast.dll", "ThirdPartyNotices/DotRecast.LICENSE",
    ];

    private sealed class PackageContext(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            var path = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(path)) return LoadFromAssemblyPath(path);
            if (name.Name is "DotRecast.Core" or "DotRecast.Recast")
                throw new FileNotFoundException("Private terrain dependency is missing from the release package.", path);
            return null;
        }
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Usage: ReleasePackaging.Tests <Release ZIP>");
            var zip = Path.GetFullPath(args[0]);
            using (var archive = ZipFile.OpenRead(zip))
                foreach (var file in REQUIRED_FILES)
                    if (archive.GetEntry("MasterPlayer/" + file) == null)
                        throw new InvalidDataException("Release package is missing: " + file);
            var extracted = Path.Combine(Path.GetDirectoryName(zip)!, "package-smoke-" + Guid.NewGuid().ToString("N"));
            ZipFile.ExtractToDirectory(zip, extracted);
            var package = Path.Combine(extracted, "MasterPlayer");
            var context = new PackageContext(package);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(package, "MasterPlayer.dll"));
                var open = Build(assembly, false);
                Check(Contains(open, Vector3.Zero) && Contains(open, new(4, 0, 0)), "The packaged builder did not produce connected floor.");
                Console.WriteLine("PASS release: packaged terrain builder loads its dependencies and builds walkable floor");
                var closed = Build(assembly, true);
                Check(Contains(closed, Vector3.Zero) && !Contains(closed, new(4, 0, 0)), "The packaged builder allowed movement through a wall.");
                Console.WriteLine("PASS release: packaged terrain builder preserves wall collision and room connectivity");
                var dependencies = context.Assemblies.Where(a => a.GetName().Name is "DotRecast.Core" or "DotRecast.Recast").ToArray();
                Check(dependencies.Length == 2 && dependencies.All(a => AssemblyLoadContext.GetLoadContext(a) == context
                    && Path.GetDirectoryName(a.Location) == package), "Terrain dependencies escaped the isolated package directory.");
                var report = new
                {
                    Package = zip,
                    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))).ToLowerInvariant(),
                    PluginVersion = assembly.GetName().Version?.ToString(),
                    PluginMvid = assembly.ManifestModule.ModuleVersionId,
                    WalkableCells = open.GetType().GetProperty("WalkableCount", MEMBERS)!.GetValue(open),
                    Dependencies = dependencies.Select(a => new { Name = a.GetName().Name, a.Location }).ToArray(),
                    Passed = 2,
                };
                File.WriteAllText(Path.Combine(extracted, "validation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
                Console.WriteLine(JsonSerializer.Serialize(report));
            }
            finally { context.Unload(); }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL release: " + ex.GetBaseException());
            return 1;
        }
    }

    private static object Build(Assembly assembly, bool wall)
    {
        var sceneType = assembly.GetType("MasterPlayer.Spatial.Terrain.TerrainSceneSnapshot", true)!;
        var instanceType = sceneType.GetNestedType("Instance", BindingFlags.NonPublic)!;
        var box = assembly.GetType("MasterPlayer.Spatial.Terrain.TerrainPrimitives", true)!.GetField("Box", MEMBERS)!.GetValue(null)!;
        var instances = Array.CreateInstance(instanceType, wall ? 2 : 1);
        object Instance(ulong id, Vector3 scale, Vector3 position)
            => Activator.CreateInstance(instanceType, MEMBERS, null,
                [id, box, Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(position), 0x4000UL, ulong.MaxValue], null)!;
        instances.SetValue(Instance(1, new(30, 1, 30), new(0, -1, 0)), 0);
        if (wall) instances.SetValue(Instance(2, new(.01f, 2, 30), new(1.8f, 2, 0)), 1);
        var scene = Activator.CreateInstance(sceneType, MEMBERS, null, [instances], null);
        var builder = assembly.GetType("MasterPlayer.Spatial.Terrain.SceneTerrainBuilder", true)!;
        var result = builder.GetMethod("Build", MEMBERS)!.Invoke(null, [scene, Vector3.Zero, 8f, CancellationToken.None])!;
        return result.GetType().GetProperty("Boundary", MEMBERS)!.GetValue(result)!;
    }

    private static bool Contains(object map, Vector3 point)
        => (bool)map.GetType().GetMethod("Contains", MEMBERS)!.Invoke(map, [point, .0001f])!;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
