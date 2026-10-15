using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

var source = File.ReadAllText(args[0]);
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(p => MetadataReference.CreateFromFile(p)).ToArray();
var stubs = """
global using System;
global using System.Linq;
global using System.Collections.Generic;
using System.Numerics;
namespace Dalamud.Bindings.ImGui {
public enum ImGuiCol { Text }
[Flags] public enum ImGuiColorEditFlags { NoInputs = 1, NoLabel = 2 }
public static class ImGui {
public static void SameLine(float a = 0, float b = 0) { }
public static void PushStyleColor(ImGuiCol c, Vector4 v) { }
public static void TextWrapped(string s) { }
public static void PopStyleColor() { }
public static Vector2 CalcTextSize(string s) => new(s.Length, 1);
public static Vector2 GetContentRegionAvail() => new(100, 100);
public static void PushID(string s) { }
public static void PopID() { }
public static bool Checkbox(string s, ref bool v) { v = !v; return true; }
public static void Spacing() { }
public static void TextColored(Vector4 c, string s) { }
public static void Separator() { }
public static void TextUnformatted(string s) { }
public static void SetNextItemWidth(float f) { }
public static bool SliderFloat(string s, ref float v, float a, float b, string f) => false;
public static void AlignTextToFramePadding() { }
public static bool ColorEdit3(string s, ref Vector3 v, ImGuiColorEditFlags f) => false;
}}
namespace PromeRotation.UI.Components {
public static class PromeUiTheme { public static Vector4 TextMuted => default; public static Vector4 Accent => default; }
public static class PromeUi {
public static bool TabButton(string i, string l, bool a) => false;
HOST_METHOD
}}
""";
var cases = new (string Name, string Method)[] {
    ("4 parameters", "public static bool ToggleRow(string i, string l, string d, ref bool v) { v = !v; return true; }"),
    ("5 parameters", "public static bool ToggleRow(string i, string l, string d, ref bool v, bool enabled = true) { if (!enabled) throw new Exception(); v = !v; return true; }"),
    ("6 parameters", "public static bool ToggleRow(string i, string l, string d, ref bool v, bool enabled = true, bool compact = false) { if (!enabled || compact) throw new Exception(); v = !v; return true; }"),
    ("missing method fallback", ""),
    ("incompatible overload fallback", "public static bool ToggleRow(string i, string l, string d, ref bool v, int required) => false;")
};
foreach (var test in cases) {
    var compilation = CSharpCompilation.Create("UiTest" + Guid.NewGuid().ToString("N"),
        [CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText(stubs.Replace("HOST_METHOD", test.Method))], references,
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    using var stream = new MemoryStream();
    var emitted = compilation.Emit(stream);
    if (!emitted.Success) throw new Exception(string.Join("\n", emitted.Diagnostics));
    stream.Position = 0;
    var context = new AssemblyLoadContext(test.Name, isCollectible: true);
    var assembly = context.LoadFromStream(stream);
    var toggle = assembly.GetType("MasterPlayer.Windows.PluginUi")!.GetMethod("Toggle")!;
    foreach (var description in new[] { "short", new string('x', 120) }) {
        object?[] parameters = ["test", "label", description, false];
        if (!(bool)toggle.Invoke(null, parameters)! || !(bool)parameters[3]!) throw new Exception(test.Name + " failed to enable");
        if (!(bool)toggle.Invoke(null, parameters)! || (bool)parameters[3]!) throw new Exception(test.Name + " failed to disable");
    }
    Console.WriteLine("PASS " + test.Name + ": wide/narrow layout and ref bool updates");
    context.Unload();
}
using var binary = File.OpenRead(args[1]);
using var pe = new PEReader(binary);
var reader = pe.GetMetadataReader();
if (reader.MemberReferences.Any(h => reader.GetString(reader.GetMemberReference(h).Name) == "ToggleRow"))
    throw new Exception("Compiled plugin still binds ToggleRow directly");
Console.WriteLine("PASS compiled DLL contains no direct ToggleRow binding");
var hostCompilation = CSharpCompilation.Create("HostMetadata", references: references.Append(MetadataReference.CreateFromFile(args[2])));
var host = hostCompilation.GetTypeByMetadataName("PromeRotation.UI.Components.PromeUi")!;
foreach (var method in host.GetMembers("ToggleRow")) Console.WriteLine("Installed PR: " + method.ToDisplayString());
