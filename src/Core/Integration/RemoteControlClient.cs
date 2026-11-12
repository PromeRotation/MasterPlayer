using System.Numerics;
using System.Text.Json;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using MasterPlayer.Settings;

namespace MasterPlayer.Integration;

public sealed class RemoteControlClient : IDisposable
{
    private const string PREFIX = "XSZToolbox.RemoteControl.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ICallGateSubscriber<string?> roomId;
    private readonly ICallGateSubscriber<bool> connected;
    private readonly ICallGateSubscriber<int> memberCount;
    private readonly ICallGateSubscriber<int> onlineMemberCount;
    private readonly ICallGateSubscriber<string> roomSnapshot;
    private readonly ICallGateSubscriber<string, string> createVirtualRoom;
    private readonly ICallGateSubscriber<string> refreshVirtualRoom;
    private readonly ICallGateSubscriber<string, Vector3, object> setPos;
    private readonly ICallGateSubscriber<string, Vector3, int, object> lockPos;
    private readonly ICallGateSubscriber<string, Vector3, long, object> slideTp;
    private readonly ICallGateSubscriber<string, Vector3, long, object> moveManaged;
    private readonly ICallGateSubscriber<string, Vector3, long, object> setPosManaged;
    private readonly ICallGateSubscriber<string, string, Vector3, object> setMoveAssemble;
    private readonly ICallGateSubscriber<string, int, object> setMoveAssembleDelay;
    private readonly ICallGateSubscriber<string, float, int, object> setRot;
    private readonly ICallGateSubscriber<string, Vector3, object> moveTo;
    private readonly ICallGateSubscriber<string, object> moveStop;
    private readonly ICallGateSubscriber<string, bool, object> stop;
    private readonly ICallGateSubscriber<string, bool, object> jump;
    private readonly ICallGateSubscriber<string, uint, object> useSkill;
    private readonly ICallGateSubscriber<string, uint, uint, object> useSkillWithTarget;
    private readonly ICallGateSubscriber<string, uint, object> setTarget;
    private readonly ICallGateSubscriber<string, string, object> echo;
    private readonly ICallGateSubscriber<string, string, object> cmd;
    private readonly ICallGateSubscriber<string, object> kick;
    private readonly ICallGateSubscriber<string, string, object> setRole;
    private readonly ICallGateSubscriber<string, string?> roleByName;
    private readonly ICallGateSubscriber<string, string?> roleByCid;
    private readonly ICallGateSubscriber<string, string?> nameByRole;
    private readonly ICallGateSubscriber<string, Vector3, long, object> slideMoveDelay;
    private readonly ICallGateSubscriber<string, Vector3, long, object> slideMoveTimeout;
    private bool disposed;

    public static bool HasPermission => PromeRotation.Core.Core.MyPrLevel() >= 3;
    public bool Available { get; private set; }
    public bool Connected { get; private set; }
    public string? RoomId { get; private set; }
    public int MemberCount { get; private set; }
    public int OnlineMemberCount { get; private set; }
    public bool SupportsRoomSnapshot => roomSnapshot.HasFunction;
    public RemoteRoomSnapshot? Room { get; private set; }
    public string StatusError { get; private set; } = "";
    public string LastOperation { get; private set; } = "";
    public bool LastOperationSucceeded { get; private set; }

    internal RemoteControlClient()
    {
        var ipc = Svc.PluginInterface;
        roomId = ipc.GetIpcSubscriber<string?>(PREFIX + "GetRoomId");
        connected = ipc.GetIpcSubscriber<bool>(PREFIX + "IsConnected");
        memberCount = ipc.GetIpcSubscriber<int>(PREFIX + "GetMemberCount");
        onlineMemberCount = ipc.GetIpcSubscriber<int>(PREFIX + "GetOnlineMemberCount");
        roomSnapshot = ipc.GetIpcSubscriber<string>(PREFIX + "GetRoomSnapshotJson");
        createVirtualRoom = ipc.GetIpcSubscriber<string, string>(PREFIX + "CreateVirtualRoomFromParty");
        refreshVirtualRoom = ipc.GetIpcSubscriber<string>(PREFIX + "RefreshVirtualRoomFromParty");
        setPos = ipc.GetIpcSubscriber<string, Vector3, object>(PREFIX + "SetPos");
        lockPos = ipc.GetIpcSubscriber<string, Vector3, int, object>(PREFIX + "LockPos");
        slideTp = ipc.GetIpcSubscriber<string, Vector3, long, object>(PREFIX + "SlideTp");
        moveManaged = ipc.GetIpcSubscriber<string, Vector3, long, object>(PREFIX + "MoveManaged");
        setPosManaged = ipc.GetIpcSubscriber<string, Vector3, long, object>(PREFIX + "SetPosManaged");
        setMoveAssemble = ipc.GetIpcSubscriber<string, string, Vector3, object>(PREFIX + "SetMoveAssemble");
        setMoveAssembleDelay = ipc.GetIpcSubscriber<string, int, object>(PREFIX + "SetMoveAssembleDelay");
        setRot = ipc.GetIpcSubscriber<string, float, int, object>(PREFIX + "SetRot");
        moveTo = ipc.GetIpcSubscriber<string, Vector3, object>(PREFIX + "MoveTo");
        moveStop = ipc.GetIpcSubscriber<string, object>(PREFIX + "MoveStop");
        stop = ipc.GetIpcSubscriber<string, bool, object>(PREFIX + "Stop");
        jump = ipc.GetIpcSubscriber<string, bool, object>(PREFIX + "Jump");
        useSkill = ipc.GetIpcSubscriber<string, uint, object>(PREFIX + "UseSkill");
        useSkillWithTarget = ipc.GetIpcSubscriber<string, uint, uint, object>(PREFIX + "UseSkillWithTarget");
        setTarget = ipc.GetIpcSubscriber<string, uint, object>(PREFIX + "SetTarget");
        echo = ipc.GetIpcSubscriber<string, string, object>(PREFIX + "Echo");
        cmd = ipc.GetIpcSubscriber<string, string, object>(PREFIX + "Cmd");
        kick = ipc.GetIpcSubscriber<string, object>(PREFIX + "Kick");
        setRole = ipc.GetIpcSubscriber<string, string, object>(PREFIX + "SetRole");
        roleByName = ipc.GetIpcSubscriber<string, string?>(PREFIX + "GetRoleByPlayerName");
        roleByCid = ipc.GetIpcSubscriber<string, string?>(PREFIX + "GetRoleByPlayerCID");
        nameByRole = ipc.GetIpcSubscriber<string, string?>(PREFIX + "GetNamebyRole");
        slideMoveDelay = ipc.GetIpcSubscriber<string, Vector3, long, object>(PREFIX + "SlideMoveDelay");
        slideMoveTimeout = ipc.GetIpcSubscriber<string, Vector3, long, object>(PREFIX + "SlideMoveTimeout");
    }

    public void Refresh()
    {
        ClearSnapshot();
        if (disposed || !HasPermission)
            return;
        try
        {
            if (!connected.HasFunction)
            {
                StatusError = "XSZToolbox 遥控 IPC 不可用，请安装并启用 XSZToolbox 的遥控模块。";
                return;
            }
            Available = true;
            Connected = connected.InvokeFunc();
            RoomId = roomId.InvokeFunc();
            MemberCount = memberCount.InvokeFunc();
            OnlineMemberCount = onlineMemberCount.InvokeFunc();
            if (roomSnapshot.HasFunction)
                Room = ParseRoom(roomSnapshot.InvokeFunc());
            StatusError = "";
        }
        catch (Exception ex)
        {
            ClearSnapshot();
            StatusError = $"遥控状态读取失败：{ex.Message}";
        }
    }

    internal static RemoteRoomSnapshot? ParseRoom(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        var snapshot = JsonSerializer.Deserialize<RemoteRoomSnapshot>(json, JsonOptions)
            ?? throw new JsonException("房间快照为空。");
        return snapshot with { Members = snapshot.Members?.Where(member => member != null).ToArray() ?? [] };
    }

    internal void ClearSnapshot()
    {
        Available = false;
        Connected = false;
        RoomId = null;
        MemberCount = 0;
        OnlineMemberCount = 0;
        Room = null;
        StatusError = "";
    }

    public bool CreateVirtualRoom(string name) => Run("创建附近玩家虚拟房间", () => Room = ParseRoom(createVirtualRoom.InvokeFunc(name.Trim())));
    public bool RefreshVirtualRoom() => Run("刷新附近玩家", () => Room = ParseRoom(refreshVirtualRoom.InvokeFunc()));

    public bool SetPos(string role, Vector3 position) => Control("设置位置", role, () => setPos.InvokeAction(role, position), true, position);
    public bool LockPos(string role, Vector3 position, int durationMs) => Control("锁定位置", role, () => lockPos.InvokeAction(role, position, Math.Max(0, durationMs)), true, position);
    public bool SlideTp(string role, Vector3 position, long timeMs) => Control("滑动传送", role, () => slideTp.InvokeAction(role, position, Math.Max(0, timeMs)), true, position);
    public bool MoveManaged(string role, Vector3 position, long timeoutMs) => Control("延后移动", role, () => moveManaged.InvokeAction(role, position, timeoutMs), true, position);
    public bool SetPosManaged(string role, Vector3 position, long timeoutMs) => Control("延后传送", role, () => setPosManaged.InvokeAction(role, position, timeoutMs), true, position);
    public bool MoveTo(string role, Vector3 position) => Control("移动到位置", role, () => moveTo.InvokeAction(role, position), true, position);
    public bool SlideMoveDelay(string role, Vector3 position, long battleTimeMs) => Control("定时滑步", role, () => slideMoveDelay.InvokeAction(role, position, battleTimeMs), true, position);
    public bool SlideMoveTimeout(string role, Vector3 position, long battleTimeMs) => Control("截止滑步", role, () => slideMoveTimeout.InvokeAction(role, position, battleTimeMs), true, position);
    public bool SetMoveAssemble(string role, string mode, Vector3 point) => Control("设置集合", role, () => setMoveAssemble.InvokeAction(role, mode, point), true, point);
    public bool SetMoveAssembleDelay(string role, int delayMs) => Control("设置集合补偿", role, () => setMoveAssembleDelay.InvokeAction(role, Math.Max(0, delayMs)), true);
    public bool SetRot(string role, float radians, int durationMs) => Control("锁定朝向", role, () =>
    {
        if (!float.IsFinite(radians)) throw new ArgumentException("朝向必须是有限数值。");
        setRot.InvokeAction(role, radians, Math.Max(0, durationMs));
    }, true);

    public bool MoveStop(string role) => Control("停止移动", role, () => moveStop.InvokeAction(role));
    public bool Stop(string role, bool value) => Control("停止操作", role, () => stop.InvokeAction(role, value));
    public bool Jump(string role, bool value) => Control("跳跃", role, () => jump.InvokeAction(role, value), value);
    public bool UseSkill(string role, uint skillId) => Control("使用技能", role, () =>
    {
        if (skillId == 0) throw new ArgumentException("技能 ID 不能为 0。");
        useSkill.InvokeAction(role, skillId);
    });
    public bool UseSkillWithTarget(string role, uint skillId, uint targetId) => Control("对指定目标使用技能", role, () =>
    {
        if (skillId == 0 || targetId == 0) throw new ArgumentException("技能与目标 ID 不能为 0。");
        useSkillWithTarget.InvokeAction(role, skillId, targetId);
    });
    public bool SetTarget(string role, uint targetId) => Control("设置目标", role, () => setTarget.InvokeAction(role, targetId));
    public bool Echo(string role, string message) => Control("发送消息", role, () => { RequireText(message, "消息"); echo.InvokeAction(role, message); });
    public bool Cmd(string role, string command) => Control("执行命令", role, () => { RequireText(command, "命令"); cmd.InvokeAction(role, command); });
    public bool Kick(string role) => Control("移出房间", role, () => kick.InvokeAction(role));
    public bool SetRole(string role, string newRole) => Control("设置职能", role, () => { RequireText(newRole, "新职能"); setRole.InvokeAction(role, newRole.Trim()); });

    public string? GetRoleByPlayerName(string name) => Query("按名称查询职能", () => { RequireText(name, "玩家名称"); return roleByName.InvokeFunc(name.Trim()); });
    public string? GetRoleByPlayerCID(string cid) => Query("按 CID 查询职能", () => { RequireText(cid, "玩家 CID"); return roleByCid.InvokeFunc(cid.Trim()); });
    public string? GetNameByRole(string role) => Query("按职能查询名称", () => { RequireText(role, "职能"); return nameByRole.InvokeFunc(role.Trim()); });

    private string? Query(string operation, Func<string?> query)
    {
        string? result = null;
        Run(operation, () => result = query());
        return result;
    }

    private bool Control(string operation, string role, Action action, bool movement = false, Vector3? position = null) => Run(operation, () =>
    {
        // Toolbox treats an empty selector as everyone with an assigned job role.
        RequireText(role, "目标职能（全部成员请明确填写 .*）");
        if (!connected.InvokeFunc()) throw new InvalidOperationException("尚未连接遥控房间。");
        if (MasterPlayerPlugin.Instance?.Replay?.SessionActive == true) throw new InvalidOperationException("回放期间不能发送遥控指令。");
        if (movement && !MasterPlayerSettings.Instance.EnableSmartMovement) throw new InvalidOperationException("请先开启智能走位。");
        if (position is { } p && (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)))
            throw new ArgumentException("位置必须是有限数值。");
        action();
    });

    private bool Run(string operation, Action action)
    {
        LastOperationSucceeded = false;
        if (disposed || !HasPermission)
        {
            ClearSnapshot();
            LastOperation = disposed ? "遥控客户端已卸载。" : "遥控需要 PR3 权限。";
            return false;
        }
        try
        {
            action();
            LastOperation = $"{operation}：IPC 调用完成；远端执行结果以工具箱为准。";
            LastOperationSucceeded = true;
            return true;
        }
        catch (Exception ex)
        {
            LastOperation = $"{operation}失败：{ex.Message}";
            Svc.Log.Warning(ex, $"[PromeRadar] 遥控 IPC {operation}失败");
            return false;
        }
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label}不能为空。");
    }

    public void Dispose()
    {
        disposed = true;
        ClearSnapshot();
        LastOperation = "";
    }
}
