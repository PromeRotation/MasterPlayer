var failures = new List<string>();
var passed = 0;
if (args.Contains("--effect-identity"))
{
    void Run(string name, Action body)
    {
        if (!name.Contains("identity", StringComparison.OrdinalIgnoreCase) && !name.Contains("origins")) return;
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    }
    PilgrimsBossRegression.Run(Run);
    PilgrimsPreviewRegression.Run(Run);
    Pt60SpinRegression.Run(Run);
    Console.WriteLine($"{passed} identity scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
if (args.Contains("--pt10-leafmash"))
{
    Pt10LeafmashRegression.Run((name, body) =>
    {
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    });
    Console.WriteLine($"{passed} PT10 scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
if (args.Contains("--pt99-targeting") || args.Contains("--final-verse"))
{
    void Run(string name, Action body)
    {
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    }
    Pt99TargetRegression.Run(Run);
    if (args.Contains("--final-verse")) FinalVerseRegression.Run(Run);
    if (args.Contains("--final-verse")) Pt99BoundsRegression.Run(Run);
    if (args.Contains("--final-verse")) Pt99DrainRegression.Run(Run);
    PilgrimsBossRegression.Run((name, body) => { if (name.Contains("PT99")) Run(name, body); });
    Console.WriteLine($"{passed} PT99 scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
if (args.Contains("--previews"))
{
    PilgrimsPreviewRegression.Run((name, body) =>
    {
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    });
    Console.WriteLine($"{passed} preview scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
if (args.Contains("--pt70-halo"))
{
    PilgrimsBossRegression.Run((name, body) =>
    {
        if (!name.Contains("PT70")) return;
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    });
    Console.WriteLine($"{passed} PT70 scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
if (args.Contains("--pt60-spin"))
{
    Pt60SpinRegression.Run((name, body) =>
    {
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    });
    Console.WriteLine($"{passed} PT60 spin scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
if (args.Contains("--pt50-overlap"))
{
    Pt50OverlapRegression.Run((name, body) =>
    {
        try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
        catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
    });
    Console.WriteLine($"{passed} PT50 overlap scenarios passed; {failures.Count} failed.");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    return;
}
ArenaReentryRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
Pt10LeafmashRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
PilgrimsBossRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
PilgrimsPreviewRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
Pt20BurstRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
Pt50ChaseRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
Pt60SpinRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
Pt50OverlapRegression.Run((name, body) =>
{
    try { body(); Console.WriteLine($"PASS {name}"); ++passed; }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
});
if (args is [var sqpack])
{
    using var data = new Lumina.GameData(sqpack, new Lumina.LuminaOptions
    {
        DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
        PanicOnSheetChecksumMismatch = true
    });
    var duties = data.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>() ?? throw new Exception("Missing CFC sheet");
    for (uint cfc = 1032; cfc <= 1041; ++cfc)
        if (duties.GetRow(cfc).TerritoryType.RowId != cfc + 249) throw new Exception($"Unexpected territory for CFC{cfc}");
    var actions = data.GetExcelSheet<Lumina.Excel.Sheets.Action>() ?? throw new Exception("Missing Action sheet");
    foreach (var (id, radius) in new[] { (44057u, 15), (43534u, 6), (43535u, 6), (43536u, 6),
        (43456u, 11), (43457u, 11), (43458u, 11), (43459u, 11) })
    {
        var row = actions.GetRow(id);
        if (row.CastType != 2 || row.EffectRange != radius) throw new Exception($"Unexpected physical footprint for Action{id}");
        Console.WriteLine($"PASS real Action{id} {row.Name}: CastType={row.CastType}, radius={row.EffectRange}");
    }
    Console.WriteLine("PASS real CFC1032..1041 -> Territory1281..1290");
}
Console.WriteLine($"{passed} scenarios passed; {failures.Count} failed.");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;
