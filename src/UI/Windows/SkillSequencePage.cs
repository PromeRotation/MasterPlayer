using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Combat.Sequences;
using MasterPlayer.Settings;
using Lumina.Excel.Sheets;

namespace MasterPlayer.Windows;

internal sealed class SkillSequencePage
{
    private uint jobId;
    private string? selectedId;
    private string message = "";

    public void Draw()
    {
        var settings = MasterPlayerSettings.Instance;
        var enabled = settings.EnableSkillSequences;
        if (ImGui.Checkbox("启用技能序列", ref enabled))
        {
            settings.EnableSkillSequences = enabled;
            if (!enabled) MasterPlayerPlugin.Instance?.SkillSequences?.Stop();
            settings.Save();
        }
        ImGui.TextWrapped("按职业选择执行预设，进入战斗后自动执行。技能通过 PR 本体释放；回放期间暂停。编辑内容在下次开战生效。");
        ImGui.TextUnformatted("状态：" + (MasterPlayerPlugin.Instance?.SkillSequences?.Status ?? "未初始化"));
        if (ImGui.Button("停止本轮")) MasterPlayerPlugin.Instance?.SkillSequences?.Stop();
        if (jobId == 0) jobId = Svc.Objects.LocalPlayer?.ClassJob.RowId ?? 19;
        var jobs = Svc.Data.GetExcelSheet<ClassJob>().Where(j => j.RowId > 0).ToArray();
        var current = Array.FindIndex(jobs, j => j.RowId == jobId);
        var labels = jobs.Select(j => $"{j.Name}（{j.RowId}）").ToArray();
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("职业", ref current, labels, labels.Length) && current >= 0)
        {
            jobId = jobs[current].RowId;
            selectedId = null;
        }
        ImGui.SameLine();
        if (ImGui.Button("使用当前职业") && Svc.Objects.LocalPlayer is { } player)
        {
            jobId = player.ClassJob.RowId;
            selectedId = null;
        }
        var profiles = settings.SkillSequences.Where(p => p.JobId == jobId).ToArray();
        if (ImGui.Button("新建预设"))
        {
            var profile = new SkillSequenceProfile { JobId = jobId, IsActive = profiles.Length == 0 };
            settings.SkillSequences.Add(profile);
            selectedId = profile.PresetId;
            settings.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("从剪贴板导入"))
        {
            try
            {
                var profile = SkillSequenceExchange.Import(ImGui.GetClipboardText());
                profile.JobId = jobId;
                profile.IsActive = profiles.Length == 0;
                var originalName = profile.PresetName;
                for (var suffix = 2; profiles.Any(p => p.PresetName == profile.PresetName); ++suffix)
                    profile.PresetName = $"{originalName} ({suffix})";
                settings.SkillSequences.Add(profile);
                selectedId = profile.PresetId;
                settings.Save();
                message = "已导入。支持原工具箱 XSZSEQ1 分享码及 JSON。";
            }
            catch (Exception ex) { message = $"导入失败：{ex.Message}"; }
        }
        if (!string.IsNullOrEmpty(message)) ImGui.TextWrapped(message);
        profiles = settings.SkillSequences.Where(p => p.JobId == jobId).ToArray();
        if (profiles.Length == 0)
        {
            ImGui.TextDisabled("此职业尚无预设。新建预设后添加技能步骤，或导入原工具箱分享码。");
            return;
        }
        var selected = Array.FindIndex(profiles, p => p.PresetId == selectedId);
        if (selected < 0) selected = 0;
        ImGui.SetNextItemWidth(280);
        var names = profiles.Select(p => (p.IsActive ? "[执行] " : "") + p.PresetName).ToArray();
        ImGui.Combo("预设", ref selected, names, names.Length);
        var chosen = profiles[selected];
        selectedId = chosen.PresetId;
        var changed = false;
        if (!chosen.IsActive && ImGui.Button("设为执行预设"))
        {
            foreach (var profile in profiles) profile.IsActive = ReferenceEquals(profile, chosen);
            changed = true;
        }
        if (chosen.IsActive) ImGui.TextDisabled("当前职业执行预设");
        ImGui.SameLine();
        if (ImGui.Button("复制"))
        {
            var copy = SkillSequenceExchange.Clone(chosen);
            copy.PresetName += " 副本";
            copy.IsActive = false;
            settings.SkillSequences.Add(copy);
            selectedId = copy.PresetId;
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("导出到剪贴板"))
        {
            ImGui.SetClipboardText(SkillSequenceExchange.Export(chosen));
            message = "已复制分享码。";
        }
        ImGui.SameLine();
        if (ImGui.Button("删除预设")) ImGui.OpenPopup("删除预设确认");
        if (ImGui.BeginPopup("删除预设确认"))
        {
            ImGui.TextWrapped($"删除“{chosen.PresetName}”？");
            if (ImGui.Button("确认删除"))
            {
                settings.SkillSequences.Remove(chosen);
                if (chosen.IsActive && profiles.FirstOrDefault(p => !ReferenceEquals(p, chosen)) is { } next) next.IsActive = true;
                settings.Save();
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }
            ImGui.SameLine();
            if (ImGui.Button("取消")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        var name = chosen.PresetName;
        if (ImGui.InputText("名称", ref name, 128)) { chosen.PresetName = name; changed = true; }
        var auto = chosen.EnableAutoRun;
        if (ImGui.Checkbox("开战自动执行", ref auto)) { chosen.EnableAutoRun = auto; changed = true; }
        ImGui.SameLine();
        var loop = chosen.LoopExecution;
        if (ImGui.Checkbox("循环执行", ref loop)) { chosen.LoopExecution = loop; changed = true; }
        var command = chosen.CompletionCommand;
        if (ImGui.InputText("完成命令", ref command, 512)) { chosen.CompletionCommand = command; changed = true; }
        var delay = chosen.CompletionCommandDelayMs;
        if (ImGui.InputInt("命令延迟（毫秒）", ref delay)) { chosen.CompletionCommandDelayMs = Math.Clamp(delay, 0, 3000); changed = true; }
        if (ImGui.Button("添加步骤")) { chosen.Entries.Add(new()); changed = true; }
        ImGui.SameLine();
        if (ImGui.Button("清空步骤")) ImGui.OpenPopup("清空步骤确认");
        if (ImGui.BeginPopup("清空步骤确认"))
        {
            ImGui.TextUnformatted("清空此预设全部步骤？");
            if (ImGui.Button("确认清空")) { chosen.Entries.Clear(); changed = true; ImGui.CloseCurrentPopup(); }
            ImGui.SameLine();
            if (ImGui.Button("取消")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.Separator();
        for (var i = 0; i < chosen.Entries.Count; ++i)
        {
            ImGui.PushID(i);
            var entry = chosen.Entries[i];
            var active = entry.Enabled;
            if (ImGui.Checkbox($"第 {i + 1} 步", ref active)) { entry.Enabled = active; changed = true; }
            ImGui.SameLine();
            if (ImGui.SmallButton("上移") && i > 0)
            {
                (chosen.Entries[i - 1], chosen.Entries[i]) = (chosen.Entries[i], chosen.Entries[i - 1]);
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("下移") && i + 1 < chosen.Entries.Count)
            {
                (chosen.Entries[i + 1], chosen.Entries[i]) = (chosen.Entries[i], chosen.Entries[i + 1]);
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("删除"))
            {
                chosen.Entries.RemoveAt(i);
                changed = true;
                ImGui.PopID();
                break;
            }
            changed |= EditId("技能 ID", entry.SkillId, v => entry.SkillId = v);
            if (entry.SkillId != 0) ImGui.TextDisabled(Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(entry.SkillId)?.Name.ToString() ?? "未知技能");
            var self = entry.TargetSelf;
            if (ImGui.Checkbox("对自己释放", ref self)) { entry.TargetSelf = self; changed = true; }
            changed |= EditId("拥有此状态则跳过（0=关闭）", entry.SkipIfHasStatusId, v => entry.SkipIfHasStatusId = v);
            changed |= EditId("需要此状态（0=关闭）", entry.RequireStatusId, v => entry.RequireStatusId = v);
            var note = entry.Note;
            if (ImGui.InputText("备注", ref note, 256)) { entry.Note = note; changed = true; }
            ImGui.Separator();
            ImGui.PopID();
        }
        if (changed) settings.Save();
    }

    private static bool EditId(string label, uint id, Action<uint> apply)
    {
        var value = (int)Math.Min(id, int.MaxValue);
        if (!ImGui.InputInt(label, ref value)) return false;
        apply((uint)Math.Max(0, value));
        return true;
    }
}
