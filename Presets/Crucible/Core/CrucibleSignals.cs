namespace MasterPlayer.Mechanics.Crucible;

// Tells the ACR over IPC that a mechanic wants the player on a fixed point, so it does not dash back to the boss.
internal static class CrucibleGoalSignal
{
    internal static DateTime LastGoalUtc;
    // run2181 B08: after the dark-ray orb spawned, the bait goal was gone but this 1.5s tail kept
    // "有机制站位目标，不贴回" for most of the ~3s walk back from the corner. Frames refresh it every tick.
    internal static bool Active => (DateTime.UtcNow - LastGoalUtc).TotalSeconds < 0.4;
}

/// <summary>斗兽脚本加载时注册的钩子，躲避形状库和雷达通过它们读斗兽数据。</summary>
internal static class CrucibleBundleHooks
{
    internal delegate bool TryGetShape(uint actionId, out MasterPlayer.Dodge.AoeShapeDescriptor descriptor);
    internal static Func<uint, bool>? IsClaimedAction;
    internal static TryGetShape? TryGetActionShape;
}

/// <summary>斗兽脚本给雷达提供显示用的场地边界。</summary>
internal interface ICrucibleRadarArena
{
    (MasterPlayer.Spatial.Geometry.IGeometryShape Boundary, System.Numerics.Vector3 Center)? GetRadarArena(MasterPlayer.Spatial.Geometry.IGeometryShape? navigationBoundary);
}