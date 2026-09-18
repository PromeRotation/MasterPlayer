using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Scripting.Visual;
using MasterPlayer.Settings;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

internal sealed class VisualScriptEditor
{
    private static Vector4 Accent => PromeUiTheme.Accent;
    private static Vector4 Warning => PromeUiTheme.Warning;
    private static Vector4 Error => PromeUiTheme.Danger;
    private static readonly string[] Shapes = ["圆形", "月环", "扇形", "中心矩形", "射线矩形"];
    private static readonly string[] Origins = ["施法者", "技能落点", "读条目标", "固定坐标"];
    private static readonly string[] Tracking = ["读条开始时锁定", "持续跟随位置", "持续跟随位置与普通朝向"];
    private static readonly string[] Directions = ["施法朝向", "施法者普通朝向", "固定角度"];
    private bool _visible;
    private bool _dirty;
    private VisualScriptDefinition? _draft;
    private string? _path;
    private string? _savedText;
    private string? _selectedRule;
    private string _filter = "";
    private string _feedback = "";
    private bool _feedbackError;
    private Action? _pendingAction;
    private string _confirmation = "";
    private bool _confirmDiscard;
    private bool _openConfirmation;
    private float _previewDirection = 180f;
    private float _previewZoom = 1f;

    internal void Open(string? path = null)
    {
        _visible = true;
        if (path != null && !string.Equals(path, _path, StringComparison.OrdinalIgnoreCase))
            Navigate(() => Load(path));
    }

    internal void Draw()
    {
        if (!_visible) return;
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager == null) return;
        using var theme = PromeUiTheme.PushWindowStyle();
        ImGui.SetNextWindowSize(new Vector2(1180f, 820f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(760f, 560f), new Vector2(2000f, 1600f));
        var open = true;
        if (ImGui.Begin("可视化脚本编辑器###MasterPlayer_VisualEditor", ref open))
        {
            DrawToolbar(manager);
            if (!string.IsNullOrEmpty(_feedback))
            {
                if (_feedbackError)
                {
                    if (ImGui.BeginChild("##visual_feedback", new Vector2(-1f, 80f), true)) TextWrapped(_feedback, Error);
                    ImGui.EndChild();
                }
                else TextWrapped(_feedback, Accent);
            }
            var wide = ImGui.GetContentRegionAvail().X >= 1040f;
            if (wide)
            {
                if (ImGui.BeginChild("##visual_library", new Vector2(220f, -1f), true)) DrawLibrary(manager);
                ImGui.EndChild();
                ImGui.SameLine();
            }
            if (ImGui.BeginChild("##visual_document", new Vector2(-1f, -1f)))
            {
                if (!wide && ImGui.BeginCombo("脚本库", _draft?.Name ?? "选择或新建脚本"))
                {
                    DrawLibrary(manager);
                    ImGui.EndCombo();
                }
                DrawDocument(manager);
            }
            ImGui.EndChild();
        }
        if (!open) Navigate(() => _visible = false);
        DrawConfirmation(manager);
        ImGui.End();
    }

    private void DrawToolbar(ScriptManager manager)
    {
        ImGui.BeginDisabled(_draft == null);
        if (PromeUi.PrimaryButton("save", "保存并应用", new Vector2(128, 32))) Save(manager);
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("新建脚本")) Navigate(NewDocument);
        ImGui.SameLine();
        if (ImGui.Button("更多")) ImGui.OpenPopup("##document_actions");
        if (ImGui.BeginPopup("##document_actions"))
        {
            if (ImGui.MenuItem("复制脚本", "", false, _draft != null)) DuplicateDocument();
            if (ImGui.MenuItem("重新读取", "", false, _savedText != null)) Navigate(() => Load(_path!));
            ImGui.Separator();
            if (ImGui.MenuItem("删除脚本", "", false, _savedText != null))
                Confirm($"删除「{_draft?.Name}」的脚本文件及未保存修改？", () => DeleteDocument(manager));
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.TextColored(_dirty ? Warning : PromeUiTheme.TextMuted, _draft == null ? "未选择脚本" : _dirty ? "有未保存修改" : "已保存");
        ImGui.Separator();
    }

    private void DrawLibrary(ScriptManager manager)
    {
        ImGui.TextColored(Accent, "可视化脚本");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##visual_filter", "搜索名称 / 区域 ID", ref _filter, 128);
        var scripts = manager.Scripts.Where(d => d.Source == ScriptSource.Visual).ToArray();
        foreach (var descriptor in scripts)
        {
            var metadata = descriptor.Metadata;
            if (!metadata.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                && !metadata.Region.Contains(_filter, StringComparison.OrdinalIgnoreCase)) continue;
            ImGui.PushID(metadata.Id);
            if (ImGui.Selectable(Label(metadata.Name), _draft?.Id == metadata.Id))
                Navigate(() => Load(descriptor.SourcePath));
            var status = manager.Running.ContainsKey(metadata.Id) ? "运行中" : manager.IsScriptEnabled(metadata.Id) ? "已启用 · 等待区域" : "已停用";
            ImGui.TextDisabled($"区域 {metadata.Region} · {status}");
            ImGui.Separator();
            ImGui.PopID();
        }
        if (scripts.Length == 0) ImGui.TextWrapped("还没有可视化脚本。点击「新建脚本」开始。");
        ImGui.Spacing();
        ImGui.TextWrapped("同一区域可启用多份脚本，也可以同时运行 C# 脚本。");
        if (!MasterPlayerSettings.Instance.EnableScripts)
            TextWrapped("脚本总开关已关闭；仍可编辑和保存。", Warning);
    }

    private void DrawDocument(ScriptManager manager)
    {
        if (_draft == null)
        {
            ImGui.TextColored(Accent, "用读条创建机制范围");
            ImGui.TextWrapped("新建一份副本脚本，添加读条 ID，再选择 AOE 形状、位置与绑定方式。");
            ImGui.Spacing();
            ImGui.TextWrapped("适合圆形、月环、扇形和直线等独立读条预告。分阶段、点名分配和多事件推导继续使用 C# 脚本。");
            return;
        }
        ImGui.SetNextItemWidth(280f);
        TextInput("脚本名称", _draft.Name, v => _draft.Name = v);
        ImGui.SetNextItemWidth(160f);
        IntInput("区域 ID", _draft.TerritoryId, v => _draft.TerritoryId = v);
        ImGui.SameLine();
        if (ImGui.Button("使用当前区域")) { _draft.TerritoryId = (int)Svc.ClientState.TerritoryType; _dirty = true; }
        ImGui.SameLine();
        if (_savedText != null)
        {
            var enabled = manager.IsScriptEnabled(_draft.Id);
            if (ImGui.Checkbox("启用脚本", ref enabled)) manager.SetScriptEnabled(_draft.Id, enabled);
        }
        else ImGui.TextDisabled("首次保存后可启用");
        TextInput("备注 / Boss 名称", _draft.Note, v => _draft.Note = v);
        PluginUi.Hint("修改在保存后生效。尺寸单位：码；时间单位：秒。");
        ImGui.Separator();

        if (ImGui.BeginChild("##visual_rules", new Vector2(176f, -1f), true))
        {
            if (ImGui.Button("添加机制", new Vector2(-1f, 0f)))
            {
                var rule = new VisualMechanicRule();
                _draft.Rules.Add(rule);
                _selectedRule = rule.Id;
                _dirty = true;
            }
            foreach (var rule in _draft.Rules)
            {
                ImGui.PushID(rule.Id);
                if (ImGui.Selectable(Label(rule.Name), _selectedRule == rule.Id)) _selectedRule = rule.Id;
                ImGui.TextDisabled($"{(rule.Enabled ? "启用" : "停用")} · {rule.ActionId} · {Shapes[(int)rule.Shape]}");
                ImGui.Separator();
                ImGui.PopID();
            }
            if (_draft.Rules.Count == 0) ImGui.TextWrapped("点击上方添加第一条机制。");
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("##visual_inspector", new Vector2(-1f, -1f), true))
        {
            var rule = _draft.Rules.FirstOrDefault(r => r.Id == _selectedRule);
            if (rule == null) ImGui.TextWrapped("选择左侧机制，编辑触发条件和 AOE。");
            else DrawRule(rule);
        }
        ImGui.EndChild();
    }

    private void DrawRule(VisualMechanicRule rule)
    {
        ImGui.PushID(rule.Id);
        BoolInput("启用此机制", rule.Enabled, v => rule.Enabled = v);
        ImGui.SameLine();
        if (ImGui.Button("复制机制"))
        {
            var copy = VisualScriptStore.CloneRule(rule);
            copy.Name += "（副本）";
            _draft!.Rules.Add(copy);
            _selectedRule = copy.Id;
            _dirty = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("删除机制")) Confirm($"删除机制「{rule.Name}」？保存后生效。", () =>
        {
            _draft!.Rules.Remove(rule);
            _selectedRule = _draft.Rules.FirstOrDefault()?.Id;
            _dirty = true;
        });
        ImGui.SetNextItemWidth(-100f);
        TextInput("机制名称", rule.Name, v => rule.Name = v);
        DrawPreview(rule);

        Section("触发条件 · 读条开始");
        IntInput("读条 ID（十进制）", rule.ActionId, v => rule.ActionId = v);
        UIntInput("施法者 Data ID", rule.CasterDataId, v => rule.CasterDataId = v);
        PluginUi.Hint("Data ID 为怪物种类编号，0 表示任意施法者。");
        var target = Svc.Targets.Target;
        ImGui.BeginDisabled(target is not IBattleChara { IsCasting: true });
        if (ImGui.Button("读取目标当前读条") && target is IBattleChara battle)
        {
            rule.ActionId = (int)battle.CastActionId;
            rule.CasterDataId = battle.BaseId;
            _dirty = true;
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(target == null);
        if (ImGui.Button("读取目标 Data ID") && target != null) { rule.CasterDataId = target.BaseId; _dirty = true; }
        ImGui.EndDisabled();
        BoolInput("接管此读条的通用躲避", rule.SuppressGeneric, v => rule.SuppressGeneric = v);

        Section("AOE 形状");
        Choice("形状", (int)rule.Shape, Shapes, v => rule.ChangeShape((VisualAoeShape)v));
        if (rule.Shape is VisualAoeShape.Circle or VisualAoeShape.Donut or VisualAoeShape.Fan)
            FloatInput(rule.Shape == VisualAoeShape.Donut ? "外半径" : "半径", rule.Radius, v => rule.Radius = v);
        if (rule.Shape == VisualAoeShape.Donut) FloatInput("内半径", rule.InnerRadius, v => rule.InnerRadius = v);
        if (rule.Shape == VisualAoeShape.Fan) FloatInput("总开角（度）", rule.AngleDegrees, v => rule.AngleDegrees = v);
        if (rule.Shape is VisualAoeShape.Rectangle or VisualAoeShape.RayRectangle)
        {
            FloatInput("全宽", rule.Width, v => rule.Width = v);
            FloatInput("全长", rule.Length, v => rule.Length = v);
            ImGui.TextWrapped(rule.Shape == VisualAoeShape.Rectangle ? "锚点位于矩形中心。" : "锚点位于后边中心，沿朝向向前延伸。");
        }

        Section("位置与绑定");
        Choice("位置来源", (int)rule.Origin, Origins, v =>
        {
            rule.Origin = (VisualAoeOrigin)v;
            if (rule.Origin is VisualAoeOrigin.Fixed or VisualAoeOrigin.CastLocation) rule.Tracking = VisualAoeTracking.Snapshot;
            rule.ResetInactiveValues();
        });
        if (rule.Origin == VisualAoeOrigin.Target)
            ImGui.TextWrapped("收到读条时读取目标实体位置；找不到目标时跳过此机制。");
        if (rule.Origin == VisualAoeOrigin.Fixed)
        {
            var position = rule.Position;
            if (ImGui.InputFloat3("固定坐标 X / Y / Z", ref position, format: "%.2f")) { rule.Position = position; _dirty = true; }
            var player = Svc.Objects.LocalPlayer;
            ImGui.BeginDisabled(player == null);
            if (ImGui.Button("取玩家坐标") && player != null) { rule.Position = player.Position; _dirty = true; }
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(target == null);
            if (ImGui.Button("取目标坐标") && target != null) { rule.Position = target.Position; _dirty = true; }
            ImGui.EndDisabled();
        }
        ImGui.BeginDisabled(rule.Origin is VisualAoeOrigin.Fixed or VisualAoeOrigin.CastLocation);
        Choice("绑定方式", (int)rule.Tracking, Tracking, v => { rule.Tracking = (VisualAoeTracking)v; rule.ResetInactiveValues(); });
        ImGui.EndDisabled();
        if (rule.Tracking == VisualAoeTracking.FollowPositionAndRotation)
            ImGui.TextWrapped("跟随实体的普通朝向；偏移按实体局部坐标计算：X 向右，Z 向后。");
        else
        {
            ImGui.TextWrapped("偏移按世界坐标计算：X 向东，Z 向南。");
            if (rule.Shape is not (VisualAoeShape.Circle or VisualAoeShape.Donut))
            {
                Choice("锁定朝向", (int)rule.Direction, Directions, v => { rule.Direction = (VisualAoeDirection)v; rule.ResetInactiveValues(); });
                if (rule.Direction == VisualAoeDirection.Fixed)
                    FloatInput("角度（0 南 / 90 东）", rule.RotationDegrees, v => rule.RotationDegrees = v);
            }
        }
        if (rule.Shape is not (VisualAoeShape.Circle or VisualAoeShape.Donut))
        {
            FloatInput("角度偏移（度）", rule.AngleOffsetDegrees, v => rule.AngleOffsetDegrees = v);
            PluginUi.Hint("+90° 左转，-90° 右转，180° 反向。位置偏移不变。");
        }
        FloatInput("偏移 X", rule.Offset.X, v => rule.Offset = rule.Offset with { X = v });
        FloatInput("偏移 Z", rule.Offset.Y, v => rule.Offset = rule.Offset with { Y = v });
        if (rule.Tracking != VisualAoeTracking.Snapshot) ImGui.TextWrapped("绑定实体消失时移除此 AOE。");

        Section("判定时间");
        FloatInput("读条结束后延迟", rule.DelaySeconds, v => rule.DelaySeconds = v);
        FloatInput("判定持续时间", rule.HitDurationSeconds, v => rule.HitDurationSeconds = v);
        ImGui.TextWrapped("读条开始即显示预告，读条结束并经过延迟后才进入危险判定；取消或打断读条会移除本次预告。");
        foreach (var error in rule.Validate()) TextWrapped(error, Error);
        ImGui.PopID();
    }

    private void DrawPreview(VisualMechanicRule rule)
    {
        ImGui.TextColored(Accent, "AOE 预览");
        PluginUi.Slider("模拟朝向", ref _previewDirection, 0f, 360f, "%.0f°");
        PluginUi.Slider("预览大小", ref _previewZoom, 0.5f, 3f, "%.1f 倍");
        _dirty |= VisualAoePreview.Draw(rule, _previewDirection, _previewZoom);
        PluginUi.Hint("北在上，主题色圆点为锚点，连线为基准朝向。预览不包含躲避安全余量。");
    }

    private void NewDocument()
    {
        _draft = new VisualScriptDefinition { TerritoryId = (int)Svc.ClientState.TerritoryType };
        _path = null;
        _savedText = null;
        _selectedRule = null;
        _dirty = true;
        Feedback("填写区域 ID，再添加机制。首次保存的脚本默认停用。");
    }

    private void DuplicateDocument()
    {
        if (_draft == null) return;
        _draft = VisualScriptStore.Clone(_draft);
        _draft.Id = Guid.NewGuid().ToString("D");
        _draft.Name += "（副本）";
        foreach (var rule in _draft.Rules) rule.Id = Guid.NewGuid().ToString("D");
        _selectedRule = _draft.Rules.FirstOrDefault()?.Id;
        _path = null;
        _savedText = null;
        _dirty = true;
        Feedback("已复制为独立脚本；保存后可单独启用。");
    }

    private void Load(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var definition = VisualScriptStore.Parse(text);
            _draft = definition;
            _path = path;
            _savedText = text;
            _selectedRule = definition.Rules.FirstOrDefault()?.Id;
            _dirty = false;
            Feedback("已读取脚本。");
        }
        catch (Exception ex) { Feedback(ex.Message, true); }
    }

    private bool Save(ScriptManager manager)
    {
        if (_draft == null) return false;
        try
        {
            var errors = _draft.Validate();
            if (errors.Count > 0) { Feedback(string.Join("\n", errors), true); return false; }
            var path = _path ?? Path.Combine(manager.ScriptsDirectory, "Visual", _draft.Id + VisualScriptStore.FileExtension);
            _savedText = manager.SaveVisualScript(path, _draft, _savedText);
            _path = path;
            _dirty = false;
            if (MasterPlayerSettings.Instance.EnableScripts && manager.IsScriptEnabled(_draft.Id)
                && _draft.TerritoryId == Svc.ClientState.TerritoryType && !manager.Running.ContainsKey(_draft.Id))
            {
                Feedback($"文件已保存，但脚本启动失败：{manager.ErrorMessage}", true);
                return false;
            }
            Feedback(manager.IsScriptEnabled(_draft.Id) ? "已保存并应用；当前脚本的旧预告已清理。" : "已保存。勾选「启用脚本」后将在匹配区域运行。");
            return true;
        }
        catch (Exception ex) { Feedback(ex.Message, true); return false; }
    }

    private void DeleteDocument(ScriptManager manager)
    {
        try
        {
            var descriptor = manager.Scripts.FirstOrDefault(d => d.Source == ScriptSource.Visual && d.Metadata.Id == _draft?.Id)
                ?? throw new InvalidOperationException("脚本已从目录移除，请重新读取。");
            manager.DeleteVisualScript(descriptor, _savedText!);
            _draft = null;
            _path = null;
            _savedText = null;
            _selectedRule = null;
            _dirty = false;
            Feedback("已删除脚本。");
        }
        catch (Exception ex) { Feedback(ex.Message, true); }
    }

    private void Navigate(Action action)
    {
        if (!_dirty) { action(); return; }
        if (_pendingAction != null) return;
        _confirmDiscard = true;
        _confirmation = "当前脚本有未保存修改。";
        _pendingAction = action;
        _openConfirmation = true;
    }

    private void Confirm(string message, Action action)
    {
        _confirmDiscard = false;
        _confirmation = message;
        _pendingAction = action;
        _openConfirmation = true;
    }

    private void DrawConfirmation(ScriptManager manager)
    {
        if (_openConfirmation) { ImGui.OpenPopup("确认操作##visual_confirm"); _openConfirmation = false; }
        ImGui.SetNextWindowSize(new Vector2(560f, 0f), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("确认操作##visual_confirm", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextWrapped(_confirmation);
        if (_confirmDiscard && _feedbackError)
        {
            if (ImGui.BeginChild("##visual_confirm_error", new Vector2(-1f, 80f), true)) TextWrapped(_feedback, Error);
            ImGui.EndChild();
        }
        if (_confirmDiscard)
        {
            if (ImGui.Button("保存并继续") && Save(manager)) CompleteAction();
            ImGui.SameLine();
        }
        if (ImGui.Button(_confirmDiscard ? "放弃修改并继续" : "确认删除")) CompleteAction(discard: _confirmDiscard);
        ImGui.SameLine();
        if (ImGui.Button("取消")) { _pendingAction = null; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }

    private void CompleteAction(bool discard = false)
    {
        var action = _pendingAction;
        _pendingAction = null;
        if (discard)
        {
            _draft = _savedText == null ? null : VisualScriptStore.Parse(_savedText);
            _dirty = false;
        }
        ImGui.CloseCurrentPopup();
        action?.Invoke();
    }

    private void Feedback(string message, bool error = false) { _feedback = message; _feedbackError = error; }
    private static string Label(string text) => text.Replace("##", "# #");
    private static void TextWrapped(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(Accent, title);
        ImGui.Separator();
    }
    private void TextInput(string label, string value, Action<string> apply)
    {
        value ??= "";
        if (ImGui.InputText(label, ref value, 512)) { apply(value); _dirty = true; }
    }
    private void IntInput(string label, int value, Action<int> apply)
    {
        if (ImGui.InputInt(label, ref value)) { apply(value); _dirty = true; }
    }
    private void UIntInput(string label, uint value, Action<uint> apply)
    {
        if (ImGui.InputUInt(label, ref value)) { apply(value); _dirty = true; }
    }
    private void FloatInput(string label, float value, Action<float> apply)
    {
        if (ImGui.InputFloat(label, ref value, format: "%.2f")) { apply(value); _dirty = true; }
    }
    private void BoolInput(string label, bool value, Action<bool> apply)
    {
        if (ImGui.Checkbox(label, ref value)) { apply(value); _dirty = true; }
    }
    private void Choice(string label, int value, string[] choices, Action<int> apply)
    {
        if (ImGui.Combo(label, ref value, choices)) { apply(value); _dirty = true; }
    }
}
