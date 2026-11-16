using System.Numerics;
using System.Text.Json;
using MasterPlayer;
using MasterPlayer.Integration;
using MasterPlayer.RemoteControlTests;
using MasterPlayer.Settings;
using PromeRotation.Core;

const string prefix = "XSZToolbox.RemoteControl.";
const string snapshotJson = """
{"roomCode":"test-room","roomName":"Test","isConnected":true,"isHost":true,"isVirtual":true,"transport":"Virtual",
 "totalMemberCount":1,"onlineMemberCount":1,"members":[{"cidHash":"test-member","name":"Test Player","role":"Host",
 "jobRole":"MT","isConnected":true,"isMe":true,"isInCombat":true,"worldX":1.5,"worldY":2,"worldZ":-3}]}
""";
var checks = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL " + name);
    checks++;
    Console.WriteLine("PASS " + name);
}

Core.Level = 3;
using var remote = new RemoteControlClient();
remote.Refresh();
Check(!remote.Available && remote.StatusError.Length > 0, "missing toolbox reports unavailable without throwing");
var handlers = TestIpcProxy.Handlers;
handlers[prefix + "IsConnected"] = () => true;
handlers[prefix + "GetRoomId"] = () => "test-room";
handlers[prefix + "GetMemberCount"] = () => 1;
handlers[prefix + "GetOnlineMemberCount"] = () => 1;
handlers[prefix + "GetRoomSnapshotJson"] = () => snapshotJson;
remote.Refresh();
Check(remote.Available && remote.Connected && remote.RoomId == "test-room", "provider can load after client initialization");
Check(remote.Room is { IsVirtual: true, Members.Length: 1 } && remote.Room.Members[0] is { Role: "Host", JobRole: "MT", WorldX: 1.5f }, "camelCase snapshot preserves permission role and job role separately");
Check(RemoteControlClient.ParseRoom("") == null && RemoteControlClient.ParseRoom("{\"members\":null}")!.Members.Length == 0 && RemoteControlClient.ParseRoom("{\"members\":[null]}")!.Members.Length == 0, "empty room and nullable member arrays are handled");

var sent = new List<string>();
handlers[prefix + "MoveTo"] = (string role, Vector3 pos) => sent.Add("move");
handlers[prefix + "MoveStop"] = (string role) => sent.Add("stop");
handlers[prefix + "SetRot"] = (string role, float rot, int duration) => sent.Add("rotate");
handlers[prefix + "Jump"] = (string role, bool value) => sent.Add("jump:" + value);
handlers[prefix + "Stop"] = (string role, bool value) => sent.Add("stop-all:" + value);
handlers[prefix + "Echo"] = (string role, string text) => sent.Add("echo");
handlers[prefix + "SlideTp"] = (string role, Vector3 pos, long time) => sent.Add("slide:" + time);
handlers[prefix + "GetNamebyRole"] = (string role) => "Test Player";
handlers[prefix + "GetRoleByPlayerCID"] = (string cid) => "MT";

foreach (var level in new[] { 0, 1, 2 })
{
    Core.Level = level;
    var before = TestIpcProxy.Calls.Count;
    remote.Refresh();
    Check(!RemoteControlClient.HasPermission && !remote.MoveTo("MT", Vector3.Zero) && !remote.MoveStop("MT")
        && remote.GetNameByRole("MT") == null && !remote.Available && remote.Room == null && TestIpcProxy.Calls.Count == before,
        "PR" + level + " cannot query or send through a retained client");
}
Core.Level = 3;
remote.Refresh();
MasterPlayerSettings.Instance.EnableSmartMovement = false;
Check(!remote.MoveTo("MT", Vector3.Zero) && !remote.SetRot("MT", 0, 5000) && !remote.Jump("MT", true), "movement switch blocks move, rotation and jump");
Check(remote.MoveStop("MT") && remote.Stop("MT", true), "stop commands remain available with movement disabled");
Check(remote.Echo("MT", "test"), "non-movement control remains available");
MasterPlayerSettings.Instance.EnableSmartMovement = true;
Check(remote.MoveTo("MT", new(1, 2, 3)) && remote.SlideTp("MT", Vector3.Zero, 5_000_000_000L)
    && sent.Contains("slide:5000000000"), "move dispatch and 64-bit time argument retain their contract");
Check(remote.GetNameByRole("MT") == "Test Player" && remote.GetRoleByPlayerCID("test-member") == "MT", "case-sensitive legacy channel names match provider");
var callCount = TestIpcProxy.Calls.Count;
Check(!remote.MoveTo("", Vector3.Zero) && !remote.MoveTo("MT", new(float.NaN, 0, 0))
    && !remote.SetRot("MT", float.PositiveInfinity, 5000) && sent.Count > 0
    && TestIpcProxy.Calls.Skip(callCount).All(call => call.Channel == prefix + "IsConnected"), "blank broadcast and non-finite coordinates never dispatch control");

MasterPlayerPlugin.Instance.Replay.SessionActive = true;
var sentBeforeReplay = sent.Count;
Check(!remote.MoveTo("MT", Vector3.Zero) && !remote.Echo("MT", "test") && !remote.MoveStop("MT") && sent.Count == sentBeforeReplay, "replay blocks all remote control dispatch");
MasterPlayerPlugin.Instance.Replay.SessionActive = false;
ECommons.DalamudServices.Svc.Condition = TestCondition.DutyReplay();
Check(!remote.MoveTo("MT", Vector3.Zero) && !remote.Echo("MT", "test") && !remote.MoveStop("MT") && sent.Count == sentBeforeReplay,
    "ARR blocks all remote control without entering MP offline replay");
ECommons.DalamudServices.Svc.Condition = null;
handlers[prefix + "IsConnected"] = () => false;
Check(!remote.MoveTo("MT", Vector3.Zero), "disconnected room rejects control");
handlers[prefix + "IsConnected"] = () => true;
handlers[prefix + "GetRoomSnapshotJson"] = () => "{";
remote.Refresh();
Check(!remote.Available && remote.Room == null && remote.MemberCount == 0 && remote.StatusError.Length > 0, "malformed snapshot clears stale state and reports an error");
handlers.Remove(prefix + "GetRoomSnapshotJson");
remote.Refresh();
Check(remote.Available && !remote.SupportsRoomSnapshot && remote.Room == null, "legacy status works without optional room snapshot");
handlers.Clear();
remote.Refresh();
Check(!remote.Available && remote.RoomId == null && !remote.MoveTo("MT", Vector3.Zero), "provider unload clears stale state and fails visibly");
handlers[prefix + "IsConnected"] = () => true;
handlers[prefix + "MoveTo"] = (string role, Vector3 pos) => sent.Add("reload");
Check(remote.MoveTo("MT", Vector3.Zero), "provider reload is observed by the existing subscriber");
remote.Dispose();
Check(!remote.MoveTo("MT", Vector3.Zero) && remote.Room == null, "retained client cannot act after dispose");
Console.WriteLine($"Passed {checks} remote control checks.");
