using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Crucible;

// 程序集加载时把斗兽动作形状表接到躲避形状库上。
internal static class CrucibleBundleInit
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
    {
        CrucibleBundleHooks.IsClaimedAction = CrucibleActionCatalog.IsClaimed;
        CrucibleBundleHooks.TryGetActionShape = CrucibleActionCatalog.TryGetShape;
    }
}