namespace MasterPlayer.Scripting;

public sealed record ScriptMetadata(
    string Id,
    string Name,
    uint[] Territorys,
    string Version,
    string Author,
    string Note,
    string Region,
    string SourcePath);
