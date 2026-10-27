using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class BmrSourceInventory
{
    public static void Run(string bmrRoot, string destination)
    {
        var sourceRoot = Path.Combine(bmrRoot, "BossMod");
        var declarations = new Dictionary<string, (ClassDeclarationSyntax Node, CompilationUnitSyntax Root, string File)>();
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(sourceRoot, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj")))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new(LanguageVersion.Preview), file).GetCompilationUnitRoot();
            foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var ns = Namespace(node);
                var name = ns + "." + node.Identifier.ValueText;
                if (!declarations.ContainsKey(name) || node.BaseList != null)
                    declarations[name] = (node, root, file);
            }
        }

        string? Parent(string key)
        {
            var (node, root, _) = declarations[key];
            var parent = node.BaseList?.Types.FirstOrDefault()?.Type.ToString().Replace("global::", "").Split('<')[0];
            if (parent == null) return null;
            var ns = Namespace(node);
            var candidates = new List<string> { parent };
            for (var prefix = ns; prefix.Length != 0; prefix = prefix.Contains('.') ? prefix[..prefix.LastIndexOf('.')] : "")
                candidates.Add(prefix + "." + parent);
            candidates.AddRange(root.Usings.Where(u => u.Alias == null).Select(u => u.Name + "." + parent));
            return candidates.FirstOrDefault(c => c is "BossMod.BossModule" or "BossMod.ZoneModule" || declarations.ContainsKey(c));
        }

        string? Kind(string key)
        {
            var visited = new HashSet<string>();
            while (visited.Add(key) && declarations.ContainsKey(key))
            {
                var parent = Parent(key);
                if (parent is "BossMod.BossModule" or "BossMod.ZoneModule") return parent[8..];
                if (parent == null) return null;
                key = parent;
            }
            return null;
        }

        var rows = new List<string[]>();
        foreach (var (name, item) in declarations.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(bmrRoot, item.File).Replace('\\', '/');
            if (item.Node.Modifiers.Any(SyntaxKind.AbstractKeyword)
                || item.Node.Identifier.ValueText == "DemoModule" || Kind(name) is not { } kind) continue;
            var attr = item.Node.AttributeLists.SelectMany(a => a.Attributes).FirstOrDefault(a => a.Name.ToString() == (kind == "BossModule" ? "ModuleInfo" : "ZoneModuleInfo"));
            string Argument(string field) => attr?.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == field)?.Expression.ToString() ?? "";
            var parts = name.Split('.');
            var maturity = attr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString().Split('.').Last() ?? "WIP";
            var expansion = Argument("Expansion").Split('.').Last();
            if (expansion.Length == 0) expansion = parts.ElementAtOrDefault(1) ?? "Global";
            var category = Argument("Category").Split('.').Last();
            if (category.Length == 0) category = parts.ElementAtOrDefault(2) ?? "Uncategorized";
            if (relative.StartsWith("BossMod/QuestBattle/", StringComparison.Ordinal))
            {
                expansion = parts.ElementAtOrDefault(2) ?? "Global";
                category = "Quest";
            }
            var excluded = relative.Contains("/Dawntrail/Foray/Crucible/");
            var batch = relative.Contains("/D01Sastasha/") || relative.Contains("/D02TamTaraDeepcroft/") || relative.Contains("/D03Copperbell/");
            var group = kind == "ZoneModule" ? attr?.ArgumentList?.Arguments.ElementAtOrDefault(1)?.Expression.ToString() ?? "" : Argument("GroupID");
            rows.Add([name, kind, expansion, category, maturity, group, Argument("NameID"), Argument("PrimaryActorOID"),
                Argument("StatesType"), relative, (item.Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture),
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(item.File))),
                excluded ? "排除：用户指定斗兽棋不移植" : batch ? "首批已接入；差异及实机待验见ARRDungeons01.spec.md" : "运行库已接入；启动已验；完整机制未逐项验收",
                excluded ? "斗兽棋已由内置脚本承载（Presets/Crucible）；不在运行库范围"
                    : attr == null ? "无显式元信息；运行库注册/构造/初始更新已验（MechanicRuntimeCoverage.csv）"
                    : "运行库注册/构造/初始更新已验（MechanicRuntimeCoverage.csv）"]);
        }
        var text = new StringBuilder("module,kind,expansion,category,maturity,group_id,name_id,explicit_primary_oid,explicit_states_type,source,line,sha256,status,registration_note\n");
        foreach (var row in rows) text.AppendLine(string.Join(',', row.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")));
        File.WriteAllText(destination, text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        foreach (var group in rows.GroupBy(r => (r[1], Excluded: r[12].StartsWith("排除"))))
            Console.WriteLine($"{group.Key}: {group.Count()}");
        foreach (var group in rows.Where(r => r[1] == "BossModule" && !r[12].StartsWith("排除")).GroupBy(r => r[3]).OrderBy(g => g.Key))
            Console.WriteLine($"{group.Key}: {group.Count()}");
        Console.WriteLine($"Wrote {rows.Count} source candidates: {destination}");
    }

    // Export only immutable arena contours; no BMR assembly is needed by the plugin.
    public static void ExportArenas(string bmrRoot, string destination)
    {
        var binary = Path.Combine(bmrRoot, "BossMod/bin/Release/BossModReborn.dll");
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(binary).Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p)).ToArray();
        AssemblyLoadContext.Default.LoadFromAssemblyPath(binary);
        var prefix = "BossMod/Modules/RealmReborn/Dungeon/";
        string[] sources = ["D01Sastasha/D011Chopper.cs", "D01Sastasha/D013DennTheOrcatoothed.cs", "D02TamTaraDeepcroft/D021VoidSoulcounter.cs", "D03Copperbell/D031Kottos.cs", "D03Copperbell/D032IchorousIre.cs"];
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(bmrRoot, prefix + s)).Replace("namespace BossMod.", "namespace Exported."), new(LanguageVersion.Preview))).ToList();
        trees.Add(CSharpSyntaxTree.ParseText("""
            global using System;
            global using System.Collections.Generic;
            global using System.Linq;
            global using BossMod;
            global using Components = BossMod.Components;
            public static class ArenaExport
            {
                public static string Read()
                {
                    (string Name, ArenaBoundsCustom Bounds)[] arenas = [
                        ("Chopper", Exported.RealmReborn.Dungeon.D01Sastasha.D011Chopper.D011Chopper.arena),
                        ("Denn", Exported.RealmReborn.Dungeon.D01Sastasha.D013DennTheOrcatoothed.D013DennTheOrcatoothed.arena),
                        ("VoidFirst", Exported.RealmReborn.Dungeon.D02TamTaraDeepcroft.D021VoidSoulcounter.D021VoidSoulcounter.FirstArena),
                        ("VoidSecond", Exported.RealmReborn.Dungeon.D02TamTaraDeepcroft.D021VoidSoulcounter.D021VoidSoulcounter.SecondArena),
                        ("VoidThird", Exported.RealmReborn.Dungeon.D02TamTaraDeepcroft.D021VoidSoulcounter.D021VoidSoulcounter.ThirdArena),
                        ("Kottos", Exported.RealmReborn.Dungeon.D03Copperbell.D031Kottos.D031Kottos.arena),
                        ("Ichorous", Exported.RealmReborn.Dungeon.D03Copperbell.D032IchorousIre.D032IchorousIre.arena)];
                    var output = new System.Text.StringBuilder("// BMR arena contours. Regenerate with --bmr-arenas; see ARRDungeons01.spec.md.\nusing System.Numerics;\nusing MasterPlayer.Spatial.Geometry;\n\nnamespace MasterPlayer.Mechanics.Dungeons;\n\ninternal static class ArrDungeonArenas\n{\n");
                    foreach (var (name, bounds) in arenas)
                    {
                        if (bounds.Polygon.Parts.Count != 1 || bounds.Polygon.Parts[0].HoleStarts.Count != 0)
                            throw new InvalidOperationException(name + ": expected a single contour without holes");
                        output.Append(System.FormattableString.Invariant($"    internal static IGeometryShape {name}(float y) => new LocalPolygonGeometry(new({bounds.Center.X:R}f, y, {bounds.Center.Z:R}f), 0, [\n"));
                        foreach (var v in bounds.Polygon.Parts[0].Exterior)
                        {
                            output.Append(System.FormattableString.Invariant($"        new({-v.X:R}f, {-v.Z:R}f),\n"));
                        }
                        output.Append("    ]);\n\n");
                    }
                    return output.Append("}\n").ToString();
                }
            }
            """, new(LanguageVersion.Preview)));
        var compilation = CSharpCompilation.Create("BmrArenaExport", trees, refs,
            new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var buffer = new MemoryStream();
        var result = compilation.Emit(buffer);
        if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        buffer.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(buffer);
        var code = (string)assembly.GetType("ArenaExport")!.GetMethod("Read")!.Invoke(null, null)!;
        File.WriteAllText(destination, code, new UTF8Encoding(false));
        Console.WriteLine($"Exported seven arenas from source; geometry runtime SHA256: {Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(binary)))}");
    }

    private static string Namespace(ClassDeclarationSyntax node)
        => string.Join('.', node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
}
