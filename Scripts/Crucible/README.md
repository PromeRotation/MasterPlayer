# 斗兽奇弈脚本

五盘入口：Low1（1339）、Low2（1340）、Low3（1341）、High1（1342）、High2（1343），覆盖 B01–B45。

将这五个子目录复制到 PR 配置目录的 `MasterPlayer/Scripts/Crucible/`，并同步编译后的 `MasterPlayer.dll` 到 `Plugins/MasterPlayer/`。共享逻辑位于 `Mechanics/Crucible/`，只复制入口到旧 DLL 无法加载。

重载 MasterPlayer 后，在脚本面板点击“刷新脚本”。旧 `Scripts/CrucibleHigh1.cs` 必须移到脚本扫描目录之外，避免同 GUID 重复；其历史源码现位于仓库的 `tests/Legacy/`。

交互、疾跑、选敌、面向与停手保留提示，需手动或由 PR 循环处理。部分原始尺寸及复杂落点约束仍待实战验证。详细范围、来源与限制见 `docs/reference/CrucibleAll.spec.md`。
