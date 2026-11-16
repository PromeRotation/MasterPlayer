using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class EnuoExternalRegression
{
    public static void Run(string repository, string pluginPath, string scriptPath)
    {
        using var hashInput = File.OpenRead(pluginPath);
        Console.WriteLine("MP SHA256 " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(hashInput)));
        var config = Directory.CreateTempSubdirectory("MasterPlayer-EnuoExternal-");
        var originalInterface = Svc.PluginInterface; var originalLog = Svc.Log; var originalClient = Svc.ClientState;
        var uiSubscriptions = new HashSet<Delegate>();
        var ui = CatalogServiceProxy.Create(typeof(IDalamudPluginInterface).GetProperty("UiBuilder")!.PropertyType, (name, args) =>
        {
            if (name == "add_Draw") { uiSubscriptions.Add((Delegate)args[0]!); return null; }
            if (name == "remove_Draw") { uiSubscriptions.Remove((Delegate)args[0]!); return null; }
            throw new NotSupportedException(name);
        });
        try
        {
            SetService(nameof(Svc.PluginInterface), CatalogServiceProxy.Create<IDalamudPluginInterface>((name, _) => name switch
            {
                "get_ConfigDirectory" => config,
                "get_AssemblyLocation" => new FileInfo(typeof(PromeRotation.Plugin).Assembly.Location),
                "get_UiBuilder" => ui,
                _ => throw new NotSupportedException(name),
            }));
            SetService(nameof(Svc.Log), CatalogServiceProxy.Create<IPluginLog>((_, _) => null));
            SetService(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name switch
            {
                "add_TerritoryChanged" or "remove_TerritoryChanged" => null,
                "get_TerritoryType" => 999u,
                _ => throw new NotSupportedException(name),
            }));
            var package = Directory.CreateDirectory(Path.Combine(config.FullName, "Plugins", "MasterPlayer"));
            File.Copy(pluginPath, Path.Combine(package.FullName, "MasterPlayer.dll"));
            using var stream = File.OpenRead(pluginPath);
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            // Dalamud has already loaded these in game; the console harness has not.
            foreach (var name in new[] { "FFXIVClientStructs", "InteropGenerator.Runtime", "Lumina", "Lumina.Excel", "Dalamud.Bindings.ImGui" })
                Assembly.Load(name);
            dynamic manager = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripting.ScriptManager")!)!;
            using var lifetime = (IDisposable)manager;
            dynamic[] Scripts() => ((IEnumerable)manager.Scripts).Cast<dynamic>().ToArray();
            dynamic Enuo() => Scripts().Single(d => d.Metadata.Id == "masterplayer.extreme.enuo");
            manager.RefreshCatalog();
            var baselineCount = Scripts().Length;
            Check(Enuo().Source.ToString() == "BuiltIn", "Expected the current built-in Enuo baseline.");
            var target = Path.Combine((string)manager.ScriptsDirectory, "EnuoExtreme.cs");
            var source = File.ReadAllText(scriptPath);
            File.WriteAllText(target, source);
            manager.RefreshCatalog();
            Check(manager.ErrorMessage == "", (string)manager.ErrorMessage);
            Check(Scripts().Length == baselineCount && Enuo().Source.ToString() == "LocalSource", "External Enuo must replace the built-in exactly once.");
            var firstType = ((object)Enuo().CreateInstance()).GetType();
            Check(firstType.BaseType!.Assembly == firstType.Assembly && firstType.Assembly != assembly,
                "External entry must contain its own Enuo implementation, not inherit the built-in one.");
            Console.WriteLine("PASS actual ScriptCompiler: complete external source overrides built-in by GUID");
            var version = (string)Enuo().Metadata.Version;
            var refreshedVersion = version + "-refresh-test";
            var refreshedSource = source.Replace($"\"{version}\"", $"\"{refreshedVersion}\"");
            Check(refreshedSource != source, "The refresh test did not change the source version.");
            File.WriteAllText(target, refreshedSource);
            manager.ReloadScripts();
            Check(manager.ErrorMessage == "" && (string)Enuo().Metadata.Version == refreshedVersion
                && ((object)Enuo().CreateInstance()).GetType().Assembly != firstType.Assembly,
                "A source edit and refresh must load a new implementation without changing the MP DLL.");
            File.Delete(target); manager.RefreshCatalog();
            Check(Enuo().Source.ToString() == "BuiltIn", "Removing the external file must restore the built-in.");
            Console.WriteLine("PASS actual catalog: edit/refresh loads new assembly; removal restores built-in");

            // Run the SAME encounter assertions/three ARR fixtures against the bundled
            // implementation and actual MP facades/solver. Only replace test-host plumbing.
            var regression = File.ReadAllText(Path.Combine(repository, "tests", "EnuoRegression.cs"))
                .Replace("MasterPlayer.Mechanics.Enuo", "MasterPlayer.LocalScripts.Enuo")
                .Replace("new ScriptSafeZoneApi { UtcNowForTest = Epoch }", "new EnuoTestScope(Epoch)")
                .Replace("new ScriptSafeZoneApi { UtcNowForTest = now, DangerMarginForTest = 0.5f }", "new EnuoTestScope(now, 0.5f)")
                .Replace("var draw = new ScriptDrawApi();", "var draw = safe.Draw;")
                .Replace("new EnuoOutput(safe, draw, safe.Manager)", "new EnuoOutput(safe.Api, draw, safe.Manager, safe.Bridge)")
                .Replace("new EnuoOutput(safe, new(), safe.Manager)", "new EnuoOutput(safe.Api, safe.Draw, safe.Manager, safe.Bridge)")
                .Replace("draw.Previews.Count", "safe.PreviewCount")
                .Replace("AppContext.BaseDirectory", JsonSerializer.Serialize(Path.Combine(repository, "tests")));
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Append(Path.GetFullPath(pluginPath)).Append(Assembly.GetExecutingAssembly().Location)
                .Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p)).ToArray();
            var compilation = CSharpCompilation.Create("ExternalEnuoValidation_" + Guid.NewGuid().ToString("N"),
                new[] { "global using System; global using System.IO; global using System.Linq; global using System.Collections.Generic;", source, regression,
                    File.ReadAllText(Path.Combine(repository, "tests", "ScriptCompilation", "EnuoExternalHost.cs.in")) }
                    .Select(s => CSharpSyntaxTree.ParseText(s, new(LanguageVersion.Latest))), references,
                new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
            using var output = new MemoryStream();
            var result = compilation.Emit(output);
            Check(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            output.Position = 0;
            var validation = AssemblyLoadContext.Default.LoadFromStream(output);
            try { validation.GetType("EnuoExternalChecks")!.GetMethod("Run")!.Invoke(null, [(Func<int>)(() => uiSubscriptions.Count)]); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
        }
        finally
        {
            SetService(nameof(Svc.PluginInterface), originalInterface); SetService(nameof(Svc.Log), originalLog);
            SetService(nameof(Svc.ClientState), originalClient);
            config.Delete(recursive: true);
        }
    }
    private static void SetService(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
