using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Integration;
using MasterPlayer.Settings;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

internal sealed class RemoteControlPage
{
    private string roomName = "PromeRadar";
    private string role = "MT";
    private string queryRole = "MT";
    private string newRole = "MT";
    private string playerName = "";
    private string playerCid = "";
    private string queryResult = "";
    private string message = "";
    private string command = "";
    private Vector3 position;
    private Vector3 assemblePoint;
    private int durationMs = 5000;
    private int delayMs = 200;
    private int slideMs = 500;
    private int managedTimeoutMs;
    private int battleTimeMs;
    private int assembleMode;
    private float rotationDegrees;
    private uint skillId;
    private uint targetId;
    private DateTime nextRefreshUtc;

    public void Draw()
    {
        if (!RemoteControlClient.HasPermission)
            return;
        var remote = MasterPlayerPlugin.Instance?.RemoteControl;
        if (remote == null)
        {
            PluginUi.Hint("遥控客户端尚未就绪。");
            return;
        }
        if (DateTime.UtcNow >= nextRefreshUtc)
        {
            remote.Refresh();
            nextRefreshUtc = DateTime.UtcNow.AddSeconds(1);
        }

        ImGui.TextColored(remote.Connected ? PromeUiTheme.Success : PromeUiTheme.TextMuted,
            remote.Connected ? "● 遥控已连接" : "○ 遥控未连接");
        ImGui.SameLine();
        if (ImGui.SmallButton("刷新状态")) nextRefreshUtc = default;
        if (remote.StatusError.Length != 0)
            ImGui.TextWrapped(remote.StatusError);
        if (remote.LastOperation.Length != 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, remote.LastOperationSucceeded ? PromeUiTheme.Success : PromeUiTheme.Warning);
            ImGui.TextWrapped(remote.LastOperation);
            ImGui.PopStyleColor();
        }
        if (!remote.Available)
            return;

        ImGui.TextUnformatted($"房间：{remote.RoomId ?? "无"}    在线 {remote.OnlineMemberCount} / {remote.MemberCount}");
        if (remote.SupportsRoomSnapshot)
        {
            DrawRoom(remote);
            DrawMembers(remote);
        }
        else
            PluginUi.Hint("当前工具箱未提供房间快照接口；升级 XSZToolbox 后可查看成员、管理附近玩家虚拟房间。");

        PluginUi.Section("控制对象");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##remote_role", "职能或正则表达式，例如 MT、MT|ST、^H[12]$", ref role, 128);
        if (ImGui.SmallButton("坦克")) role = "MT|ST";
        ImGui.SameLine();
        if (ImGui.SmallButton("治疗")) role = "H1|H2";
        ImGui.SameLine();
        if (ImGui.SmallButton("输出")) role = "D1|D2|D3|D4";
        ImGui.SameLine();
        if (ImGui.SmallButton("全部已设职能成员")) role = ".*";
        PluginUi.Hint("按成员职能匹配，点击上方成员可选中其职能；同职能的成员会一起收到指令。未设置职能的成员不参与匹配。");

        if (!MasterPlayerSettings.Instance.EnableSmartMovement)
        {
            PluginUi.Hint("移动、传送、跳跃和朝向控制需要开启智能走位；停止操作仍可使用。");
            if (ImGui.Button("开启智能走位")) MasterPlayerPlugin.Instance?.SetSmartMovementEnabled(true);
        }
        var replay = MasterPlayerPlugin.Instance?.Replay?.SessionActive == true;
        if (replay) PluginUi.Hint("回放期间暂停发送遥控指令。");
        ImGui.BeginDisabled(!remote.Connected || string.IsNullOrWhiteSpace(role) || replay);
        DrawMovement(remote);
        DrawAssemble(remote);
        DrawActions(remote);
        DrawMessages(remote);
        DrawRoles(remote);
        ImGui.EndDisabled();
    }

    public void Reset()
    {
        queryResult = "";
        nextRefreshUtc = default;
        MasterPlayerPlugin.Instance?.RemoteControl?.ClearSnapshot();
    }

    private void DrawRoom(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("房间管理", ImGuiTreeNodeFlags.DefaultOpen))
            return;
        PluginUi.Hint("远程房间的创建、加入、关闭和退出请在 XSZToolbox 中操作，此处同步显示房间与成员。");
        ImGui.InputText("房间名称", ref roomName, 128);
        if (ImGui.Button("创建附近玩家虚拟房间")) { remote.CreateVirtualRoom(roomName); nextRefreshUtc = default; }
        PluginUi.Hint("虚拟房间使用附近玩家；连接、房间权限与指令执行由 XSZToolbox 管理。");
        if (remote.Room?.IsVirtual == true && ImGui.Button("刷新附近玩家")) { remote.RefreshVirtualRoom(); nextRefreshUtc = default; }
    }

    private void DrawMembers(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("房间成员", ImGuiTreeNodeFlags.DefaultOpen))
            return;
        var room = remote.Room;
        if (room == null)
        {
            PluginUi.Hint("当前没有房间。");
            return;
        }
        ImGui.TextUnformatted($"{room.RoomName} · {(room.IsVirtual ? "虚拟房间" : "远程房间")} · {(room.IsHost ? "我是房主" : "我是成员")}");
        if (!ImGui.BeginTable("##remote_members", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(-1, 190)))
            return;
        ImGui.TableSetupColumn("玩家", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("职能", ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("权限", ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("状态", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("位置 X / Y / Z", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        for (var i = 0; i < room.Members.Length; i++)
        {
            var member = room.Members[i];
            ImGui.PushID(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(string.IsNullOrWhiteSpace(member.JobRole));
            if (ImGui.Selectable(member.Name + (member.IsMe ? "（我）" : "")))
            {
                role = "^" + Regex.Escape(member.JobRole!) + "$";
                queryRole = member.JobRole!;
            }
            ImGui.EndDisabled();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member.JobRole ?? "未设置");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member.Role switch { "Host" => "房主", "Leader" => "管理", "Member" => "成员", _ => member.Role });
            ImGui.TableNextColumn();
            var state = member.IsConnected ? "在线" : "离线";
            if (member.IsInCombat) state += " · 战斗";
            if (member.IsNavigating) state += " · 移动";
            if (member.IsMounted) state += " · 坐骑";
            ImGui.TextWrapped(state);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member is { WorldX: { } x, WorldY: { } y, WorldZ: { } z } ? $"{x:F1} / {y:F1} / {z:F1}" : "未知");
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawMovement(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("位置与移动"))
            return;
        ImGui.InputFloat3("目标位置 X / Y / Z", ref position);
        if (ImGui.SmallButton("读取我的位置") && Svc.Objects.LocalPlayer is { } player) position = player.Position;
        ImGui.InputInt("锁定时长（毫秒）", ref durationMs);
        ImGui.InputInt("滑动时长（毫秒）", ref slideMs);
        PluginUi.Hint("已发送的锁定和延后动作由工具箱持有；关闭智能走位或停止移动不能保证撤销这些任务。");
        ImGui.BeginDisabled(!MasterPlayerSettings.Instance.EnableSmartMovement);
        if (ImGui.Button("移动到位置")) remote.MoveTo(role, position);
        ImGui.SameLine();
        if (ImGui.Button("设置位置")) remote.SetPos(role, position);
        ImGui.SameLine();
        if (ImGui.Button("锁定位置")) remote.LockPos(role, position, durationMs);
        ImGui.SameLine();
        if (ImGui.Button("滑动传送")) remote.SlideTp(role, position, slideMs);
        ImGui.InputInt("最晚延迟（毫秒）", ref managedTimeoutMs);
        PluginUi.Hint("相对当前战斗时间的延迟，≤ 0 不设截止时间。");
        if (ImGui.Button("延后移动")) remote.MoveManaged(role, position, managedTimeoutMs);
        ImGui.SameLine();
        if (ImGui.Button("延后传送")) remote.SetPosManaged(role, position, managedTimeoutMs);
        ImGui.InputInt("目标战斗时间（毫秒）", ref battleTimeMs);
        PluginUi.Hint("绝对战斗时间，区别于上方相对延迟。");
        if (ImGui.Button("定时滑步")) remote.SlideMoveDelay(role, position, battleTimeMs);
        ImGui.SameLine();
        if (ImGui.Button("截止滑步")) remote.SlideMoveTimeout(role, position, battleTimeMs);
        ImGui.EndDisabled();
        if (ImGui.Button("停止移动")) remote.MoveStop(role);
    }

    private void DrawAssemble(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("集合与朝向"))
            return;
        ImGui.BeginDisabled(!MasterPlayerSettings.Instance.EnableSmartMovement);
        ImGui.Combo("集合方式", ref assembleMode, "不集合\0各成员的当前目标\0指定位置\0");
        ImGui.InputFloat3("集合位置 X / Y / Z", ref assemblePoint);
        PluginUi.Hint("按目标集合时，使用各成员自己的当前目标；无目标时使用此位置。");
        if (ImGui.Button("设置集合")) remote.SetMoveAssemble(role, new[] { "None", "Target", "Position" }[assembleMode], assemblePoint);
        ImGui.InputInt("集合补偿（毫秒）", ref delayMs);
        if (ImGui.Button("设置集合补偿")) remote.SetMoveAssembleDelay(role, delayMs);
        ImGui.InputFloat("朝向（度）", ref rotationDegrees);
        PluginUi.Hint("0° 朝向 +Z，90° 朝向 +X。");
        ImGui.InputInt("朝向锁定（毫秒）", ref durationMs);
        if (ImGui.Button("锁定朝向")) remote.SetRot(role, rotationDegrees * MathF.PI / 180, durationMs);
        ImGui.EndDisabled();
    }

    private void DrawActions(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("动作与目标"))
            return;
        if (ImGui.Button("停止操作")) remote.Stop(role, true);
        ImGui.SameLine();
        ImGui.BeginDisabled(!MasterPlayerSettings.Instance.EnableSmartMovement);
        if (ImGui.Button("跳跃")) remote.Jump(role, true);
        ImGui.EndDisabled();
        ImGui.InputUInt("技能 ID", ref skillId);
        ImGui.InputUInt("目标实体 ID", ref targetId);
        if (ImGui.Button("使用技能")) remote.UseSkill(role, skillId);
        ImGui.SameLine();
        if (ImGui.Button("对指定目标使用")) remote.UseSkillWithTarget(role, skillId, targetId);
        ImGui.SameLine();
        if (ImGui.Button("设置目标")) remote.SetTarget(role, targetId);
    }

    private void DrawMessages(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("消息与命令"))
            return;
        ImGui.InputText("消息内容", ref message, 512);
        if (ImGui.Button("发送消息")) remote.Echo(role, message);
        ImGui.InputText("聊天指令", ref command, 512);
        if (ImGui.Button("执行指令")) remote.Cmd(role, command);
    }

    private void DrawRoles(RemoteControlClient remote)
    {
        if (!ImGui.CollapsingHeader("职能设置与查询"))
            return;
        ImGui.InputText("新职能", ref newRole, 32);
        if (ImGui.Button("设置职能")) { remote.SetRole(role, newRole); nextRefreshUtc = default; }
        ImGui.SameLine();
        if (ImGui.Button("移出房间")) { remote.Kick(role); nextRefreshUtc = default; }
        ImGui.InputText("玩家名称", ref playerName, 128);
        if (ImGui.Button("按名称查询职能")) queryResult = remote.GetRoleByPlayerName(playerName) ?? "未找到";
        ImGui.InputText("玩家 CID", ref playerCid, 128);
        if (ImGui.Button("按 CID 查询职能")) queryResult = remote.GetRoleByPlayerCID(playerCid) ?? "未找到";
        ImGui.InputText("查询职能（单个职能）", ref queryRole, 32);
        if (ImGui.Button("按职能查询名称")) queryResult = remote.GetNameByRole(queryRole) ?? "未找到";
        if (queryResult.Length != 0) ImGui.TextUnformatted("查询结果：" + queryResult);
    }
}
