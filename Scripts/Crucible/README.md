# 斗兽奇弈脚本

五盘入口：Low1（1339）、Low2（1340）、Low3（1341）、High1（1342）、High2（1343），覆盖 B01–B45。

五盘入口随 `MasterPlayer.dll` 内置加载，更新并重载插件后即可在脚本面板启用，无需复制这些子目录。共享逻辑位于 `Mechanics/Crucible/`，修改内置入口或共享逻辑后都需重新构建并重载插件。

需要自定义入口时，可将 `.cs` 放入 PR 配置目录的 `MasterPlayer/Scripts/Crucible/`，点击“刷新脚本”。配置目录版本按名称或 GUID 优先覆盖内置版本；移除外部版本并刷新后恢复内置。已复制的旧入口也会覆盖内置版本，要使用随插件更新的版本，应移到扫描目录之外。旧 `CrucibleHigh1.cs` 历史源码位于仓库的 `tests/Legacy/`。

交互、疾跑、选敌、面向与停手保留提示，需手动或由 PR 循环处理。部分原始尺寸及复杂落点约束仍待实战验证。详细范围、来源与限制见 `docs/reference/CrucibleAll.spec.md`。
