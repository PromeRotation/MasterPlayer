namespace MasterPlayer.Scripting;

public enum ScriptSource
{
    LocalSource,
    LocalDll,
}

public sealed class ScriptDescriptor
{
    public ScriptDescriptor(ScriptMetadata metadata, ScriptSource source, string sourcePath, Func<MasterPlayerScript> factory)
    {
        Metadata = metadata;
        Source = source;
        SourcePath = sourcePath;
        _factory = factory;
    }

    public ScriptMetadata Metadata { get; }
    public ScriptSource Source { get; }
    public string SourcePath { get; }

    private readonly Func<MasterPlayerScript> _factory;

    public MasterPlayerScript CreateInstance() => _factory();
}
