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

if (args.Length != 2) throw new ArgumentException("Usage: <MasterPlayer repository> <compiled MasterPlayer.dll>");
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Append(Path.GetFullPath(args[1])).Distinct(StringComparer.OrdinalIgnoreCase)
    .Select(p => MetadataReference.CreateFromFile(p)).ToArray();
var count = 0;
foreach (var file in Directory.EnumerateFiles(Path.Combine(args[0], "Scripts"), "*.cs", SearchOption.AllDirectories))
{
    var compilation = CSharpCompilation.Create($"ScriptSmoke{++count}",
        [CSharpSyntaxTree.ParseText(File.ReadAllText(file), new(LanguageVersion.Latest), file)], references,
        new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
    using var stream = new MemoryStream();
    var result = compilation.Emit(stream);
    if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
    Console.WriteLine($"PASS independent script compilation: {Path.GetRelativePath(args[0], file)}");
}
if (count != 9) throw new InvalidOperationException($"Expected five boards, three dungeons and Blunderville, got {count}.");
ScriptCatalogRegression.Run(args[1], references);
