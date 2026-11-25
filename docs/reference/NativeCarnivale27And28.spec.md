# 假面狂欢第27、28场 MP 原生机制

实现位于 `Presets/MaskedCarnivale/`，沿用 `Entries/Stage01.cs` 的同区域入口。生产模型自行消费事件与对象快照，经 `DungeonEncounter → DungeonOutput → ScriptSafeZoneApi/ScriptDrawApi` 发布；不消费 BMR 桥输出。普通危险区和障碍继续使用现有接口，未引入寻路器内部依赖。

## 来源与身份

| 场次 | 本机正式名称 | 源模块 | CFC → InstanceContent → Territory | Primary / NameID | 场地 |
|---|---|---|---|---|---|
| 27 | 鼻息爆破死斗 | `Stage27LockUpYourSnorters/Stage27.cs` | 696 → 35027 → 796 | 2CF8 / 3046，提丰 | 中心100/100、R24.5、64边 |
| 28 | 绝妙傀儡师——都灵 | `Stage28DangerousWhenDead/Stage28.cs` | 697 → 35028 → 796 | 2CD2 / 9233，都灵 | 中心100/100、R16.01379、32边 |

源路径均在 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/`。两场为默认 Primary 拉战/完成生命周期，各构造期攻略提示在进入阶段时撤销，不等待杂兵全部死亡。第27场6个组件、第28场9个组件，共12种读条规则；第27场另消费3种效果动作，第28场消费6种状态。模型保存 `DungeonPendingCast` 的来源、目标、起手位置/朝向和开始/判定时间；对象引用通过 `AlphaActorState` 保留代际。

本机游戏表来自 `C:/Games/最终幻想XIV/game/sqpack`，使用现有 Lumina/Lumina.Excel、`ChineseSimplified`、`PanicOnSheetChecksumMismatch=true` 查询。两场实际消费的15种 AID（2+3+10）的 `ExtraCastTime100ms` 全为0；19054未被源 Brainstorm 组件消费。

## 第27场

| 源组件 / 输入 | 机制模型与结束条件 | 当前 MP 输出 |
|---|---|---|
| Fireball / 19258 | R8锁起手落点，真实 Finish；效果和预计时间不撤销 | 普通危险导航、双视图 |
| Snort / 19266 | 独立 AwayFromOrigin 15码，锁起手原点和判定时间；撞墙；真实 Finish撤销玩家击退 | `CarnivaleKnockbackCast`、公共投影；仅显示与落点提示 |
| Explosion / Bomb2CF9的ModelState，19259效果 | 每次真实模型变化且Anim1=1追加同一 Actor 引用，允许重复；共享处理时钟+6d。活跃阶段新对象非默认模型会在Create之后产生模型事件；组件激活只补播Create/Cast，不补播旧模型事件 | 每项独立ID的R8危险区及显示 |
| Explosion / 19266 Start | 共享投影窗口到该次FinishAt+2.5d。窗口内动态Bomb/Primary径向推15码，真实边界截墙再减固定0.4；取消不撤窗口，end==now时为空，end&lt;now后恢复原位置 | 同一爆炸模型投影；无自动推炸弹 |
| Explosion / 19259 | 原生包只删除首个相同Actor引用。死亡/不可选不清，Destroy保留冻结旧引用，同ID新对象不替换旧代 | 原生包入口排除按目标展开的legacy效果，防止一包多消费 |
| Fungah / Bomb Create、19259/19256/19257效果 | Bomb数量恰8时处理钟+5d；仅首2CEC在四组逐轴≤3位置时改+5.3d并置OtherPatterns。19259只清activation；19256/19257只清pattern，无超时 | 动态Primary原点、朝viewer的R12.5/90°锥内Away15、撞墙投影；危险文字只检查当前Explosion |
| Hints2 / 2CEC Create | 最新Create设置35秒倒计时起点；文字只看首实体，!Dead发攻略，Targetable发倒计时（可Dead） | 纯提示 |

Fungah与Snort独立计算玩家位移，不拼成跨组件连续链。击退免疫使用Role/Job/Duty三个覆盖槽，严格expiry&gt;activation；不能清除炸弹的普通危险导航。raw状态差分回调使用上帧队伍槽，组件激活补播使用当前名单；名单换槽本身不迁移免疫。

主怪红、炸弹橙、魔导爆炸物黄。ME优先1、Boss0、其余Pointless，保留基础Pointless。未新增全屏爆炸危险圈、自动水魔法、自动免疫或炸弹交互。

截墙沿用已验证的公共 `QuestWindRay` 对真实场地顶点逐边求交；上游边界优化器的顶部顶点漏交差异已在第20/21场规格记录，本次不复制该优化器错误。固定0.4炸弹余量与玩家hitbox截墙分别保留。

## 第28场

| 源组件 / 输入 | 机制模型与结束条件 | 当前 MP 输出 |
|---|---|---|
| Cackle / 19053 | 完整读条与打断意图，真实Finish | Interrupt文字，无自动打断 |
| BilrostSquall / 19082 | R10锁起手落点/时间，真实Finish | 普通危险导航、双视图 |
| NecrobaneCatapultVengefulSoul / 19059、19064、19067 | 同一R6队列，插入顺序；达到99项按判定时间排序；任意有效Finish按source删首项，迟旧结束不能跨代清理 | 共用Sap队列适配，真实危险/显示 |
| HelblarShriekFuneralPyre / 19084、19056 | 读取caster当前raw判定钟，活着的0–63队伍槽伤害预测，真实Finish | Raidwide文字/预测，无全屏躲避圈 |
| MarchOfTheDraugar / 19057 | CastHint直到真实Finish | `Summons adds! (Kill with fire!)` |
| DoomImpending / 19051 | CastHint直到真实Finish | `Heal to full before cast ends!`，不自动治疗 |
| NecrobaneVoidzone / 1EA9FA、19055 | 池实体仅筛EventState!=7，不筛Dead/Targetable。任意19055 Start覆盖单全局Inversion记录，真实匹配Finish清空；效果/超时不清，同代空间补正不重开 | 正常池并集危险；反转为池并集的补集危险，必须进入任意一个池；判定时间取Inversion。零池无导航约束但保留Go文字 |
| Brainstorm / 1958、1959、1960、1961 | 逐槽Gain追加方向0/180/90/270，按ExpireAt排序，每段2秒/6.6码每秒；Lose只删首个同方向；过期和死亡不自行删 | 动态位置/朝向累计链，逐段角色与线；仅显示/最终落点警告 |
| Brainstorm / 1257、3629 | 任一Gain覆盖ForcedEnd；任一Lose清零。有效时先沿当前朝向走6.6×剩余秒数，再走pending链 | 与同一强制行进链适配；无撞墙/免疫/自动朝向/移动暂停 |

`CarnivaleDraugarState.StatusSlots`保留真实Actor/slot、SID/source/Param/ExpiresAt。ID/source替换先Lose再Gain；同ID/source/Param且expiry增长≤1秒只更新槽，Param变化或增长&gt;1秒发Gain，不虚构Lose。Destroy逐槽Lose，允许保留源重复Gain尚未匹配的历史pending；同ID回来不凭空清除。`HuntMarchState`只复用方向与位移语义，传入source=0避免其内部按source更换而额外制造Lose，真实来源仍保留在槽账本。

池显示/文字使用原始位置，导航使用源量化位置。反转复用现有`HuntStackJoinTemplate`的并集补集几何；重叠区仍然安全，不用多个目标区冒充“进入任一池”。正常红、反转绿；`DungeonOutput`独立消费导航项与显示项。

强制行进最终非零落点只检查当前Bilrost R10与场地，不检查毒池或中间段。主怪和五类杂兵均红；选敌Boss0/其他1，沿用源setter的Pointless保护。枚举19061的拉近20码/无法移动、19054读条以及没有激活的怪物技能不擅自扩为导航或动作。

## 验证边界

模型验证覆盖创建/模型事件先后、共享时钟与多引用、原生多目标包、取消保留投影、身份冻结、恰8颗与分标志结束；第28场覆盖状态重复槽、source/Param/expiry阈值、强制剩余段、任意Finish、池重叠补集、零池、死亡/销毁/切人及预战取消后拉战。

实际 DLL 合成回放通过真实脚本入口与公共输出层验证危险导航/时间窗、独立预览、真实结束、反向Seek及停用/停止owner清理；两场分别5772点对照源场地Contains。回放不发送原生动作。源击退和强制行进没有专用导航/执行消费者，本迁移保留模型及显示范围，不把箭头当成特殊移动导航完成。

显示数据和当前普通危险/反转约束导航已离线验证。真实PR宿主执行、游戏内物理、最终双视图像素与ARR实录校准仍待实机；未部署。

证据：`.tmp/halatali-verify/native-carnivale-snorters-draugar-model-final.log`（166项集中模型）、`native-carnivale-snorters-draugar-build-initial.log`（0错误/4既有警告）、`native-carnivale-snorters-draugar-focused-initial.log`（12项PASS，含2公共项）、`native-carnivale-snorters-draugar-catalog-final.log`（784项PASS/0失败）。


第29/30场批次续验补齐共享免疫账本：旧名单保存完整Party身份，Actor缺席/Destroy不删除名单槽；同ID带状态重现重新Gain。模型与实际DLL均覆盖名单保留、Actor缺席、同ID重现后免疫恢复及真实Lose撤销。190项集中模型、19项专项及801项全目录PASS/0失败，证据为`native-carnivale-red-siegfried-*`日志；原有时序、代际和原生包分支继续通过。
