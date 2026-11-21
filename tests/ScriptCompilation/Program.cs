using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length == 3 && args[0] == "--bmr-inventory")
{
    BmrSourceInventory.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--bmr-arenas")
{
    BmrSourceInventory.ExportArenas(args[1], args[2]);
    return;
}

if (args.Length != 2 && !(args.Length == 3 && args[2] is "--scripts-only" or "--pt99-targeting" or "--palace"))
    throw new ArgumentException("Usage: <MasterPlayer repository> <compiled MasterPlayer.dll> [--scripts-only|--pt99-targeting|--palace]");
var pt99Only = args.Length == 3 && args[2] == "--pt99-targeting";
var palaceOnly = args.Length == 3 && args[2] == "--palace";
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Append(Path.GetFullPath(args[1])).Distinct(StringComparer.OrdinalIgnoreCase)
    .Select(p => MetadataReference.CreateFromFile(p)).ToArray();
var count = 0;
// Built-in entries live in each preset area: Presets/<area>/Entries.
foreach (var file in Directory.EnumerateDirectories(Path.Combine(args[0], "Presets"), "Entries", SearchOption.AllDirectories)
    .SelectMany(d => Directory.EnumerateFiles(d, "*.cs"))
    .Where(f => (!pt99Only || Path.GetFileName(f) == "PilgrimsTraverse99.cs")
        && (!palaceOnly || Path.GetFileName(f) == "Palace151To200.cs")))
{
    var compilation = CSharpCompilation.Create($"ScriptSmoke{++count}",
        [CSharpSyntaxTree.ParseText(File.ReadAllText(file), new(LanguageVersion.Latest), file)], references,
        new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
    using var stream = new MemoryStream();
    var result = compilation.Emit(stream);
    if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
    Console.WriteLine($"PASS independent script compilation: {Path.GetRelativePath(args[0], file)}");
}
if (count != (pt99Only || palaceOnly ? 1 : 20)) throw new InvalidOperationException($"Unexpected built-in script count: {count}.");
System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
if (palaceOnly) PalaceScriptRegression.Run(args[1]);
else if (pt99Only) Pt99TargetControlRegression.Run(args[1]);
else ScriptCatalogRegression.Run(args[1], references, args.Length == 3);
