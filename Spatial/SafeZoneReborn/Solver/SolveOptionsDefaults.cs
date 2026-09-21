using MasterPlayer.Spatial.SafeZone;
using System.Collections.Generic;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// <see cref="SolveOptions"/> 的默认常量。集中放置便于统一调参。
/// </summary>
public static class SolveOptionsDefaults
{
    /// <summary>放射状候选目标的默认距离档位（yalm）。</summary>
    public static readonly IReadOnlyList<float> DefaultCandidateDistances = new[] { 5f, 10f, 15f };

    /// <summary>默认 <see cref="SolveOptions"/> 实例。</summary>
    public static readonly SolveOptions Default = new();
}
