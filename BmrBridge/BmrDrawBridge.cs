using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using MasterPlayer.BmrRuntime;
using MasterPlayer.Settings;

namespace MasterPlayer.BmrBridge;

public sealed class BmrDrawBridge(BmrSession session) : IDisposable
{
    private const float SelfExpireMs = 500f;
    private const double ErrorThrottleSeconds = 5.0;

    private readonly BmrSession _session = session;
    private readonly List<BmrPreviewShape> _shapes = [];
    private readonly List<BmrPreviewLine> _lines = [];
    private readonly List<BmrRadarExtra> _extras = [];
    private readonly List<(string Text, bool IsRisk)> _hints = [];
    private HashSet<string> _registeredIds = new(StringComparer.Ordinal);
    private HashSet<string> _frameIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _errorThrottle = new(StringComparer.Ordinal);
    private bool _hadRegistrations;

    public IReadOnlyList<(string Text, bool IsRisk)> CurrentHints => _hints;
    public IReadOnlyList<BmrRadarExtra> Extras => _extras;
    public string? ExtrasKey { get; private set; }
    public string? ActiveDrawModuleName { get; private set; }
    public int CaptureErrorCount { get; private set; }
    public string? LastCaptureError { get; private set; }

    public void Update()
    {
        var settings = MasterPlayerSettings.Instance;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;
        var module = _session.BossModules.ActiveModule;
        if (!(settings.EnableRadarOverlay || settings.EnableRadar) || module == null || visibility == null
            || module.Raid[0] is not { } pc)
        {
            Sleep();
            return;
        }

        var typeName = module.GetType().Name;
        var key = $"bmr/{typeName}";
        ActiveDrawModuleName = typeName;
        ExtrasKey = key;

        var result = BmrModuleDrawCapture.Capture(module, 0, new BossMod.Angle(CameraAzimuth()));
        if (result.Errors.Count != 0)
            NoteErrors(typeName, result.Errors);

        _shapes.Clear();
        _lines.Clear();
        _extras.Clear();
        BmrDrawConvert.Convert(result.Commands, pc.PosRot.Y, _shapes, _lines, _extras);

        var frameIds = _frameIds;
        frameIds.Clear();
        var index = 0;
        foreach (var shape in _shapes)
        {
            var id = $"{key}/{index++}";
            visibility.AddPreview(id, shape.Shape, shape.Safe, SelfExpireMs, key, typeName, shape.OutlineOnly, shape.Progress);
            frameIds.Add(id);
        }
        foreach (var line in _lines)
        {
            var id = $"{key}/{index++}";
            visibility.AddLine(id, line.Start, line.End, line.Safe, key, typeName);
            frameIds.Add(id);
        }
        foreach (var stale in _registeredIds)
        {
            if (!frameIds.Contains(stale))
                visibility.RemovePreview(stale);
        }
        (_registeredIds, _frameIds) = (frameIds, _registeredIds);
        _hadRegistrations = _registeredIds.Count != 0;

        _hints.Clear();
        try
        {
            foreach (var hint in module.CalculateGlobalHints())
                _hints.Add((hint, false));
            foreach (var (text, risk) in module.CalculateHintsForRaidMember(0, pc))
                _hints.Add((text, risk));
        }
        catch (Exception ex)
        {
            NoteErrors(typeName, [new(null, "hints", ex)]);
        }
    }

    public void Sleep()
    {
        _hints.Clear();
        _extras.Clear();
        ExtrasKey = null;
        ActiveDrawModuleName = null;
        if (_hadRegistrations)
        {
            _hadRegistrations = false;
            try
            {
                MasterPlayerPlugin.Instance?.AoeVisibility?.RemovePreviews("bmr/");
            }
            catch
            {
            }
        }
        _registeredIds.Clear();
        _frameIds.Clear();
    }

    private void NoteErrors(string moduleType, IReadOnlyList<BmrDrawCaptureError> errors)
    {
        CaptureErrorCount += errors.Count;
        var now = DateTime.UtcNow;
        if (!_errorThrottle.TryGetValue(moduleType, out var last) || (now - last).TotalSeconds >= ErrorThrottleSeconds)
        {
            _errorThrottle[moduleType] = now;
            var first = errors[0];
            LastCaptureError = $"{moduleType}:{first.Stage}: {first.Exception.Message}";
            Svc.Log.Warning($"[MasterPlayer] BMR 模块 {moduleType} 绘制捕获异常 {errors.Count} 起（首起 {first.Stage}）：{first.Exception.Message}");
        }
    }

    public void Dispose() => Sleep();

    private static unsafe float CameraAzimuth()
    {
        try
        {
            var manager = CameraManager.Instance();
            if (manager == null)
                return MathF.PI;
            var camera = manager->GetActiveCamera();
            return camera != null ? camera->DirH + MathF.PI : MathF.PI;
        }
        catch
        {
            return MathF.PI;
        }
    }
}
