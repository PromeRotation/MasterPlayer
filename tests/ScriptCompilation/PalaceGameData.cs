using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Data;
using Lumina.Excel;

internal sealed class PalaceGameData(GameData game) : IDataManager
{
    public ClientLanguage Language => ClientLanguage.ChineseSimplified;
    public GameData GameData => game;
    public ExcelModule Excel => game.Excel;
    public bool HasModifiedGameDataFiles => false;
    public ExcelSheet<T> GetExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelRow<T>
        => game.GetExcelSheet<T>(name: name)!;
    public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelSubrow<T>
        => game.GetSubrowExcelSheet<T>(name: name)!;
    public FileResource? GetFile(string path) => game.GetFile(path);
    public T? GetFile<T>(string path) where T : FileResource => game.GetFile<T>(path);
    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken) where T : FileResource
        => throw new NotSupportedException();
    public bool FileExists(string path) => game.FileExists(path);
}
