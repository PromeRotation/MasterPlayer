// 护锁刃龙上位狩猎战（Arkveld Extreme）· 个人走位。原单文件脚本 ArkveldExtreme.cs 按段拆分。
/*
BSD 3-Clause License

Copyright (c) 2022-2024, Andrew Gilewsky

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System.Numerics;

namespace MasterPlayer.Mechanics.Arkveld
{
using System.Reflection;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

// Compatibility with the already installed MP: keep its movement ownership,
// replay gates and preview bookkeeping instead of implementing another mover.
// All reflection is resolved before subscribing or claiming any control.
internal sealed class ArkveldMpApi
{
    private readonly MasterPlayerPlugin _plugin;
    private readonly MethodInfo _automatic, _direct, _preview;
    private readonly ScriptDrawApi _draw;
    private readonly object?[] _automaticArgs, _directArgs, _previewArgs;
    private readonly int _arenaMarginIndex;
    private readonly PropertyInfo _scripts, _replay;
    private readonly Action<SafeZoneAreaHandle, bool> _navigationOnly;

    public ArkveldMpApi(ScriptSafeZoneApi safe, ScriptDrawApi draw)
    {
        _plugin = MasterPlayerPlugin.Instance ?? throw new InvalidOperationException("锁刃龙：MP 尚未初始化。");
        _automatic = Find(typeof(SafeZoneRuntime), "RequestAutomaticMovement", typeof(object), typeof(bool), typeof(bool));
        _direct = Find(typeof(SafeZoneRuntime), "RequestDirectTraversal", typeof(object), typeof(Vector3?), typeof(DateTime));
        _automaticArgs = Defaults(_automatic);
        _arenaMarginIndex = Array.FindIndex(_automatic.GetParameters(), p => p.Name == "arenaSafetyMargin" && p.ParameterType == typeof(float?));
        _directArgs = Defaults(_direct);
        _scripts = typeof(MasterPlayerPlugin).GetProperty("Scripts", Flags) ?? throw Missing("Scripts");
        _replay = _scripts.PropertyType.GetProperty("ReplayModeActive", Flags) ?? throw Missing("ReplayModeActive");
        if (_replay.PropertyType != typeof(bool)) throw Missing("ReplayModeActive : bool");
        _navigationOnly = Find(typeof(ScriptSafeZoneApi), "SetNavigationOnly", typeof(SafeZoneAreaHandle), typeof(bool))
            .CreateDelegate<Action<SafeZoneAreaHandle, bool>>(safe);
        _preview = Find(typeof(ScriptDrawApi), "AddAoePreview", typeof(string), typeof(IGeometryShape), typeof(bool), typeof(bool), typeof(float));
        // 新版 MP 在后面多了可选参数，不能绑成固定五参数委托。
        _previewArgs = Defaults(_preview);
        _draw = draw;
    }

    public bool ReplayActive => _scripts.GetValue(_plugin) is { } scripts && (bool)_replay.GetValue(scripts)!;
    public void RequestAutomaticMovement(object owner, bool enabled, bool hold = false, float? arenaSafetyMargin = null)
    {
        _automaticArgs[0] = owner; _automaticArgs[1] = enabled; _automaticArgs[2] = hold;
        if (_arenaMarginIndex >= 0) _automaticArgs[_arenaMarginIndex] = arenaSafetyMargin;
        _automatic.Invoke(_plugin.SafeZone, _automaticArgs);
    }
    public void RequestDirectTraversal(object owner, Vector3? target, DateTime now)
    {
        _directArgs[0] = owner; _directArgs[1] = target; _directArgs[2] = now;
        _direct.Invoke(_plugin.SafeZone, _directArgs);
    }
    public void SetNavigationOnly(SafeZoneAreaHandle handle, bool value) => _navigationOnly(handle, value);
    public void AddAoePreview(string id, IGeometryShape shape, bool safe, bool outlineOnly, float progress)
    {
        _previewArgs[0] = id; _previewArgs[1] = shape; _previewArgs[2] = safe; _previewArgs[3] = outlineOnly; _previewArgs[4] = progress;
        _preview.Invoke(_draw, _previewArgs);
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static MethodInfo Find(Type type, string name, params Type[] prefix)
        => type.GetMethods(Flags).SingleOrDefault(m => m.Name == name && m.ReturnType == typeof(void)
            && m.GetParameters().Select(p => p.ParameterType).Take(prefix.Length).SequenceEqual(prefix)
            && m.GetParameters().Skip(prefix.Length).All(p => p.HasDefaultValue)) ?? throw Missing(type.Name + "." + name);
    private static object?[] Defaults(MethodInfo method)
        => method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
    private static Exception Missing(string name)
        => new NotSupportedException("锁刃龙脚本：当前 MP 缺少兼容接口 " + name + "，本脚本未接管控制。");
}

}
