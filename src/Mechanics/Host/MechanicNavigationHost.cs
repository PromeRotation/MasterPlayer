using System.Reflection;
using System.Text;
using ECommons.DalamudServices;
using BossMod;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Settings;

namespace MasterPlayer.MechanicHost;

public sealed class MechanicNavigationHost : IDisposable
{
    private const float FacingLeadSeconds = 0.75f;

    private MechanicSession _session;
    private AIHintsBuilder _builder;
    private readonly AIHints _hints = new();
    private readonly MechanicNavAreaDiffer _differ = new();
    private readonly MechanicFacingControl _facing = new();
    private bool _boundaryApplied;
    private string? _boundarySignature;
    private Bitmap? _terrainBitmap;
    private int _terrainLeft, _terrainTop, _terrainRight, _terrainBottom;
    private IReadOnlyList<MechanicNavPrimitive>? _terrainPrims;
    private DateTime _goalZonesNextEval = DateTime.MinValue;
    private IReadOnlyList<MechanicNavPrimitive> _goalZonesPrims = [];

    public bool ModuleActive { get; private set; }
    public bool NavigationActive { get; private set; }
    public int DangerZoneCount => _differ.ZoneCount;
    public int GoalZoneCount => _differ.GoalCount;
    public int ObstacleZoneCount => _differ.ObstacleCount;
    public bool BoundaryActive => _boundaryApplied;
    public string? SpecialModeHint { get; private set; }
    public string? PredictedDamageText { get; private set; }
    public int SampledZoneCount { get; private set; }
    public int SkippedZoneCount { get; private set; }
    public string? LastError { get; private set; }

    public MechanicNavigationHost(MechanicSession session)
    {
        _session = session;
        _builder = new AIHintsBuilder(session.World, session.BossModules, session.ZoneModules, null);
    }

    public void Update()
    {
        var module = _session.BossModules.ActiveModule;
        ModuleActive = module?.StateMachine.ActivePhase != null;
        if (module == null || !ModuleActive)
        {
            Sleep();
            return;
        }

        try
        {
            _builder.Update(_hints, 0, false);
        }
        catch (Exception ex)
        {
            if (LastError != ex.Message)
            {
                LastError = ex.Message;
                Svc.Log.Warning($"[MasterPlayer] 机制模块 {module.GetType().Name} AI 提示聚合失败：{ex.Message}");
            }
            Sleep();
            return;
        }

        UpdateDiagnostics();

        var settings = MasterPlayerSettings.Instance;
        var replay = MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true;
        NavigationActive = settings.EnableSmartMovement && !replay;

        var registry = MasterPlayerPlugin.Instance!.SafeZone.Registry;
        var manager = MasterPlayerPlugin.Instance!.SafeZone.Manager;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;
        var typeName = module.GetType().Name;
        var key = $"module-nav/{typeName}";
        var y = module.WorldState.Party[0]?.PosRot.Y ?? 0f;
        var sampleCenter = module.Center.ToVec3(y);
        var sampleRadius = _hints.PathfindMapBounds?.Radius ?? 0f;

        var dangers = new List<MechanicNavZoneRequest>(_hints.ForbiddenZones.Count);
        var sampled = 0;
        var skipped = 0;
        var now = _session.World.CurrentTime;
        for (var i = 0; i < _hints.ForbiddenZones.Count; ++i)
        {
            var (sd, activation, source) = _hints.ForbiddenZones[i];
            var prims = MechanicHintsConvert.ConvertForbiddenZone(sd, y, sampleCenter, sampleRadius, out var kind);
            var isGoal = false;
            switch (kind)
            {
                case MechanicNavZoneKind.Sampled: ++sampled; break;
                case MechanicNavZoneKind.ComplementSkipped:
                    if (MechanicHintsConvert.TryConvertComplement(sd, y, out var goals))
                    {
                        prims = goals;
                        isGoal = true;
                    }
                    else
                    {
                        ++skipped;
                    }
                    break;
                case MechanicNavZoneKind.Failed: ++skipped; break;
            }
            if (prims.Count == 0)
                continue;
            var (delay, persistent) = MechanicHintsConvert.MapActivation(activation, now);
            dangers.Add(new($"{key}/{source:X16}/{activation.Ticks}/{i}", prims, delay, persistent,
                MechanicHintsConvert.SignatureOf(sd) + "|" + activation.Ticks) { IsGoal = isGoal });
        }
        SampledZoneCount = sampled;
        SkippedZoneCount = skipped;

        // 机制级强 GoalZones（软覆盖站位）：500ms 节流评估，与补集目标共用目标通道
        if (now >= _goalZonesNextEval)
        {
            _goalZonesNextEval = now.AddSeconds(0.5);
            _goalZonesPrims = MechanicHintsConvert.ConvertGoalZones(_hints.GoalZones, module.Center, sampleRadius, y);
        }
        for (var i = 0; i < _goalZonesPrims.Count; ++i)
        {
            var prim = _goalZonesPrims[i];
            dangers.Add(new($"{key}/goalzone/{i}", [prim], 0f, true, prim.Signature) { IsGoal = true });
        }

        var obstacles = new List<(string Key, MechanicNavPrimitive Prim)>(_hints.TemporaryObstacles.Count);
        for (var i = 0; i < _hints.TemporaryObstacles.Count; ++i)
        {
            var sd = _hints.TemporaryObstacles[i];
            var prims = MechanicHintsConvert.ConvertForbiddenZone(sd, y, sampleCenter, sampleRadius, out _);
            for (var j = 0; j < prims.Count; ++j)
                obstacles.Add(($"{key}/obstacle/{i}/{j}", prims[j]));
        }

        // 场地障碍位图（SimpleBossModule 族开启 AllowObstacleMap）：静态数据，位图引用+矩形未变时复用转换结果。
        // 注意：Bitmap.Region 是 MechanicRuntime 值类型，只存引用与坐标基元，值类型字段会把程序集布局依赖引入本类。
        var terrain = _hints.PathfindMapObstacles;
        if (terrain.Bitmap != null)
        {
            var rect = terrain.Rect;
            if (_terrainPrims == null || !ReferenceEquals(_terrainBitmap, terrain.Bitmap)
                || _terrainLeft != rect.Left || _terrainTop != rect.Top || _terrainRight != rect.Right || _terrainBottom != rect.Bottom)
            {
                _terrainBitmap = terrain.Bitmap;
                (_terrainLeft, _terrainTop, _terrainRight, _terrainBottom) = (rect.Left, rect.Top, rect.Right, rect.Bottom);
                _terrainPrims = MechanicHintsConvert.ConvertObstacleBitmap(terrain, module.Center, y);
            }
            for (var i = 0; i < _terrainPrims.Count; ++i)
                obstacles.Add(($"{key}/terrain/{i}", _terrainPrims[i]));
        }
        else
        {
            _terrainBitmap = null;
            _terrainPrims = null;
        }

        _differ.Sync(registry, manager, dangers, obstacles, settings.DodgeDangerMargin,
            visibility != null ? h => visibility.Track(h, key, typeName) : null);

        ApplyBoundary(module, y);
        if (NavigationActive) ApplyFacing(now);
        else _facing.Clear();
        PublishTeleporters(y);
    }

    private string? _teleporterSignature;

    // AIHints.Teleporters → 解算传送边发布（内容签名去重；失活/睡眠时 ClearRegistrations 清空）
    private void PublishTeleporters(float y)
    {
        var list = _hints.Teleporters;
        var sb = new StringBuilder(list.Count * 32);
        foreach (var t in list)
        {
            sb.Append(t.Entrance.X.ToString("F1")).Append(',').Append(t.Entrance.Z.ToString("F1")).Append(';')
                .Append(t.Exit.X.ToString("F1")).Append(',').Append(t.Exit.Z.ToString("F1")).Append(';')
                .Append(t.Radius.ToString("F2")).Append(';').Append(t.UseTime.ToString("F2")).Append(';')
                .Append(t.Bidirectional ? '1' : '0');
        }
        var signature = sb.ToString();
        if (signature == _teleporterSignature)
            return;
        _teleporterSignature = signature;
        var converted = new List<Spatial.SafeZoneReborn.Solver.SolverTeleporter>(list.Count);
        foreach (var t in list)
            converted.Add(new(new(t.Entrance.X, y, t.Entrance.Z), new(t.Exit.X, y, t.Exit.Z), t.Radius, t.Bidirectional, t.UseTime));
        MasterPlayerPlugin.Instance?.SafeZone.SetTeleporters(converted);
    }

    private void ApplyBoundary(BossModule module, float y)
    {
        var bounds = _hints.PathfindMapBounds;
        var registry = MasterPlayerPlugin.Instance!.SafeZone.Registry;
        if (bounds == null)
        {
            if (_boundaryApplied)
            {
                registry.ClearArenaBoundary();
                _boundaryApplied = false;
                _boundarySignature = null;
            }
            return;
        }
        var signature = bounds.GetType().Name + "|" + bounds.Radius.ToString("F2") + "|" + module.Center;
        if (signature == _boundarySignature)
            return;
        var shape = MechanicHintsConvert.ConvertBounds(bounds, module.Center, y);
        if (shape == null)
            return;
        registry.SetArenaBoundary(shape);
        _boundaryApplied = true;
        _boundarySignature = signature;
    }

    private void ApplyFacing(DateTime now)
    {
        var soon = now.AddSeconds(FacingLeadSeconds);
        float? heading = null;
        foreach (var (center, _, activation) in _hints.ForbiddenDirections)
        {
            if (activation == default || activation <= soon)
                heading = center.Rad + MathF.PI;
        }
        _facing.Apply(heading);
    }

    private void UpdateDiagnostics()
    {
        var (mode, activation, finish) = _hints.ImminentSpecialMode;
        var now = _session.World.CurrentTime;
        SpecialModeHint = mode switch
        {
            AIHints.SpecialMode.Normal => null,
            _ when now < activation => $"{mode}：{(activation - now).TotalSeconds:F1}s 后生效",
            _ when finish != default && now < finish => $"{mode}：生效中，剩 {(finish - now).TotalSeconds:F1}s",
            _ when finish == default => $"{mode}：生效中",
            _ => null,
        };
        if (_hints.PredictedDamage.Count == 0)
        {
            PredictedDamageText = null;
        }
        else
        {
            var groups = _hints.PredictedDamage.GroupBy(p => p.Type).OrderBy(g => g.Key);
            PredictedDamageText = string.Join("，", groups.Select(g => $"{g.Key}×{g.Count()}"));
        }
    }

    private void ClearRegistrations()
    {
        if (_differ.ZoneCount != 0 || _differ.GoalCount != 0 || _differ.ObstacleCount != 0 || _boundaryApplied)
        {
            var registry = MasterPlayerPlugin.Instance?.SafeZone.Registry;
            if (registry != null)
            {
                try { _differ.Clear(registry); } catch { }
                if (_boundaryApplied)
                {
                    try { registry.ClearArenaBoundary(); } catch { }
                }
            }
            _boundaryApplied = false;
            _boundarySignature = null;
        }
        _terrainBitmap = null;
        _terrainPrims = null;
        _goalZonesNextEval = DateTime.MinValue;
        _goalZonesPrims = [];
        if (_teleporterSignature != null)
        {
            _teleporterSignature = null;
            MasterPlayerPlugin.Instance?.SafeZone.SetTeleporters([]);
        }
        _facing.Clear();
        SampledZoneCount = 0;
        SkippedZoneCount = 0;
    }

    public void SwitchSession(MechanicSession session)
    {
        Sleep();
        _terrainBitmap = null;
        _terrainPrims = null;
        _goalZonesNextEval = DateTime.MinValue;
        _goalZonesPrims = [];
        try { _builder.Dispose(); } catch { }
        _session = session;
        _builder = new AIHintsBuilder(session.World, session.BossModules, session.ZoneModules, null);
    }

    public void Sleep()
    {
        ModuleActive = false;
        NavigationActive = false;
        SpecialModeHint = null;
        PredictedDamageText = null;
        ClearRegistrations();
    }

    internal void SuspendMovement() => _facing.Clear();

    public void Dispose()
    {
        Sleep();
        try { _facing.Dispose(); } catch { }
        try { _builder.Dispose(); } catch { }
    }
}
