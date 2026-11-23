namespace MasterPlayer.Mechanics.Dungeons;

// 保留对象代际：源组件保存 Actor 引用，销毁后不会重新绑定相同 EntityId。
internal sealed class HydaelynActorReference(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal bool Destroyed { get; set; }
}
