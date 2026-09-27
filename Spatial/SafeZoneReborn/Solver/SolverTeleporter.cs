using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 解算传送边：入口圈（Entrance 为圆心、Radius 为半径）内可瞬移到 Exit，UseTimeSeconds 为传送耗时（按移速折算进 g 成本）。
/// Bidirectional 时出口圈内也可返回入口。数据来自机制模块 AIHints.Teleporters，经 SafeZoneRuntime.SetTeleporters 发布。
/// </summary>
public readonly record struct SolverTeleporter(Vector3 Entrance, Vector3 Exit, float Radius, bool Bidirectional, float UseTimeSeconds);
