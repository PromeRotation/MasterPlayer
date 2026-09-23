using System.Reflection;
using ECommons.DalamudServices;
using BossMod;
using MasterPlayer.BmrRuntime;
using MasterPlayer.Settings;

namespace MasterPlayer.BmrBridge;

public sealed class BmrNavigationBridge : IDisposable
{
    private const float FacingLeadSeconds = 0.75f;

    private readonly BmrSession _session;
    private readonly AIHintsBuilder _builder;
    private readonly AIHints _hints = new();
    private readonly BmrNavAreaDiffer _differ = new();
    private readonly BmrFacingControl _facing = new();
    private bool _boundaryApplied;
    private string? _boundarySignature;

    public bool ModuleActive { get; private set; }
    public bool NavigationActive { get; private set; }
    public int DangerZoneCount => _differ.ZoneCount;
    public int ObstacleZoneCount => _differ.ObstacleCount;
    public bool BoundaryActive => _boundaryApplied;
    public string? SpecialModeHint { get; private set; }
    public string? PredictedDamageText { get; private set; }
    public int SampledZoneCount { get; private set; }
    public int SkippedZoneCount { get; private set; }
    public string? LastError { get; private set; }

    public BmrNavigationBridge(BmrSession session)
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
                Svc.Log.Warning($"[MasterPlayer] BMR 模块 {module.GetType().Name} AI 提示聚合失败：{ex.Message}");
            }
            Sleep();
            return;
        }

        UpdateDiagnostics();

        var settings = MasterPlayerSettings.Instance;
        var replay = MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true;
        NavigationActive = settings.EnableAutoDodge && !replay;
        if (!NavigationActive)
        {
            ClearRegistrations();
            return;
        }

        var registry = MasterPlayerPlugin.Instance!.SafeZone.Registry;
        var manager = MasterPlayerPlugin.Instance!.SafeZone.Manager;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;
        var typeName = module.GetType().Name;
        var key = $"bmr-nav/{typeName}";
        var y = module.WorldState.Party[0]?.PosRot.Y ?? 0f;
        var sampleCenter = module.Center.ToVec3(y);
        var sampleRadius = _hints.PathfindMapBounds?.Radius ?? 0f;

        var dangers = new List<BmrNavZoneRequest>(_hints.ForbiddenZones.Count);
        var sampled = 0;
        var skipped = 0;
        var now = _session.World.CurrentTime;
        for (var i = 0; i < _hints.ForbiddenZones.Count; ++i)
        {
            var (sd, activation, source) = _hints.ForbiddenZones[i];
            var prims = BmrHintsConvert.ConvertForbiddenZone(sd, y, sampleCenter, sampleRadius, out var kind);
            switch (kind)
            {
                case BmrNavZoneKind.Sampled: ++sampled; break;
                case BmrNavZoneKind.ComplementSkipped or BmrNavZoneKind.Failed: ++skipped; break;
            }
            if (prims.Count == 0)
                continue;
            var (delay, persistent) = BmrHintsConvert.MapActivation(activation, now);
            dangers.Add(new($"{key}/{source:X16}/{activation.Ticks}/{i}", prims, delay, persistent,
                BmrHintsConvert.SignatureOf(sd) + "|" + activation.Ticks));
        }
        SampledZoneCount = sampled;
        SkippedZoneCount = skipped;

        var obstacles = new List<(string Key, BmrNavPrimitive Prim)>(_hints.TemporaryObstacles.Count);
        for (var i = 0; i < _hints.TemporaryObstacles.Count; ++i)
        {
            var sd = _hints.TemporaryObstacles[i];
            var prims = BmrHintsConvert.ConvertForbiddenZone(sd, y, sampleCenter, sampleRadius, out _);
            for (var j = 0; j < prims.Count; ++j)
                obstacles.Add(($"{key}/obstacle/{i}/{j}", prims[j]));
        }

        _differ.Sync(registry, manager, dangers, obstacles, settings.DodgeDangerMargin,
            visibility != null ? h => visibility.Track(h, key, typeName) : null);

        ApplyBoundary(module, y);
        ApplyFacing(now);
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
        var shape = BmrHintsConvert.ConvertBounds(bounds, module.Center, y);
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
        if (_differ.ZoneCount != 0 || _differ.ObstacleCount != 0 || _boundaryApplied)
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
        _facing.Clear();
        SampledZoneCount = 0;
        SkippedZoneCount = 0;
    }

    public void Sleep()
    {
        ModuleActive = false;
        NavigationActive = false;
        SpecialModeHint = null;
        PredictedDamageText = null;
        ClearRegistrations();
    }

    public void Dispose()
    {
        Sleep();
        try { _facing.Dispose(); } catch { }
        try { _builder.Dispose(); } catch { }
    }

    private sealed class BmrFacingControl : IDisposable
    {
        private delegate void UpdateRequest(string owner, ulong target, ulong clearTarget, ulong[] forbiddenTargets,
            float? heading, bool holdActions, bool blockCasts, bool cancelCast);

        private readonly string _owner = "MasterPlayer/BmrNav/" + Guid.NewGuid().ToString("N");
        private UpdateRequest? _update;
        private Action<string>? _clear;
        private bool _resolved;
        private float? _appliedHeading;
        private bool _warned;

        public void Apply(float? heading)
        {
            Resolve();
            if (_update == null)
            {
                if (!_warned && heading != null)
                {
                    _warned = true;
                    Svc.Log.Warning("[MasterPlayer] 当前 PR 本体未提供机制控制接口：BMR 背对引导不可用；走位与绘制继续工作。");
                }
                return;
            }
            if (heading == _appliedHeading)
                return;
            if (heading == null)
            {
                _clear?.Invoke(_owner);
            }
            else
            {
                _update(_owner, 0, 0, [], heading, false, false, false);
            }
            _appliedHeading = heading;
        }

        public void Clear() => Apply(null);

        public void Dispose() => Clear();

        private void Resolve()
        {
            if (_resolved)
                return;
            _resolved = true;
            var type = typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
            MethodInfo? Find(string name, Type[] parameters) => type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == name && m.ReturnType == typeof(void) && !m.ContainsGenericParameters
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
            var update = Find("Update", [typeof(string), typeof(ulong), typeof(ulong), typeof(ulong[]), typeof(float?), typeof(bool), typeof(bool), typeof(bool)]);
            var clear = Find("Clear", [typeof(string)]);
            if (update != null && clear != null)
            {
                _update = update.CreateDelegate<UpdateRequest>();
                _clear = clear.CreateDelegate<Action<string>>();
            }
        }
    }
}
