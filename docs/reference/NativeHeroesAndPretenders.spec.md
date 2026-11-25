# 祭器创造的假象：Heroes and Pretenders 原生机制

日期：2026-11-24。入口 `Presets/Quests/Entries/HeroesAndPretenders.cs`，GUID `masterplayer.quest.heroes-and-pretenders`；模型 `JobQuestCatalog`、`DungeonHeroesPretenders`；输出沿用 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`。

## 来源、区域及生命周期

源 `MechanicRuntime/Upstream/Modules/Dawntrail/Quest/Role/HeroesAndPretenders.cs`，Primary428A、NameID13176、Quest70383，六个激活组件。源 `all` 数组的六类演员仅用于绘制，不作AllDead完成条件或选敌优先级。

本机简中Quest70383名称“祭器创造的假象”，Id `KinGba551_04847`；InstanceContent5078 ⇄ CFC1001 ⇄ Territory1218，CFC名称同为“祭器创造的假象”。本次未取得Quest→InstanceContent的直接外键，Quest与CFC的关联依据是精确同名及源模块元数据；不伪称QuestBattle外键链。区域按已确认的CFC/Territory双向映射及Primary OID接管。

场地为中心 `(676,41)`、半径参数14.5的20边Polygon，不替换成精确圆。默认Primary可选且InCombat激活；Primary死亡、HP0或实际对象帧缺席结束。存活的小怪不保活主阶段。完成后回血或重复Create不重新拉战；raw缺席后重现建立新代。Director40000005卸载并阻止旧帧拉战，40000011解锁。

PR对象Create/Remove日志不直接改变本模块演员生命周期，与旧Feed按完整对象帧缺席/重现建立Actor一致；连续EntityId保留首次OID。实时逐Framework采样，MP回放随对象帧版本更新，避免100ms节流遗漏短暂缺席。标记目标保留Actor引用语义：死亡暂不活跃、同演员复活恢复；目标销毁保留最后位置，同ID新演员不接走旧标记。

## 六个完整组件

| 组件 | 生产模型及参数 | 时序、判定和撤销 | 显示、导航及执行 |
|---|---|---|---|
| PromisedFall | 37466，实际R13圆 | 起手位置锁定；真实读条结束、取消或施法者移除清理；效果及预测到期不清 | 雷达/世界AOE与危险区，不按注释扩大到R35 |
| FledglingFury | 37458，R4圆 | 同普通AOE真实结束合同 | 双视图及危险区 |
| GoldDust | 37464，目标R8，恰好2人 | Start当刻目标必须存在；Finish按动作+目标ID删除首匹配标记，效果不清 | R8轮廓；本人找R4同伴；容量与恰一圈导航，见下 |
| AcidRain | 37463，目标R8分散 | 独立于GoldDust；同目标引用及真实Finish合同 | 仅R8轮廓，导航R9，不将显示圈扩大 |
| UnboundArrow | 37459，目标中心R5圆形死刑诱导 | 源死时不活跃，保留死目标；真实Finish按source删首条，效果不清 | 本人轮廓及避队友，其他人填充及避目标；只预测目标槽位 |
| ForeseenFlurry | 37467 Start；37467/37468 Effect；R4，步长5，8次，每线显示3圈 | 起点caster.Position、方向spell.Rotation；首钟CastFinishAt，实际效果推进钟+1.1秒；按位置匹配首线 | 头圈红、未来两圈普通色；三圈均Risky并参与导航 |

37472 ApexJudgment未激活Raidwide组件；37457、37461、37465、37705等视觉技能也不新增机制或接管。该模块没有凝视、护盾禁攻、全停手、击退、任务交互或自动动作；不借原生迁移扩展执行范围。

### 八次步进爆炸

事件匹配按首个 `abs(Next.X-caster.X)<=1 && abs(Next.Z-caster.Z)<=1`，不是欧氏距离、来源ID、EffectPosition或TargetID。命中后用实际caster.Position+Advance更新下一点，剩余次数减1，最后一击删线；相同位置多线只推进首线。

未来圈时钟在行版本/数量变化时按 `max(NextExplosion,当前钟)+i*1.1d` 重建缓存，不逐帧向后滑动。普通Finish、取消、源死亡/销毁或超时均不消费地火线，阶段退出/owner停止才整体清理。模型保留来源、代际、Next、Advance、Activation和Remaining，显示投影不能替代这些字段。

### 分摊、分散与目标死刑

GoldDust与AcidRain是独立源组件，各自只看自己的标记列表，不跨组件过滤同伴或自动消解冲突。两者在当前源构造链中 `raidwideOnResolve=false`，因此都不输出伤害预测。

活队伍少于2人时不发布两类站位导航，但仍可显示标记。GoldDust按量化目标中心R8，直接统计活队伍当前位置，不额外补目标人数；圈外人数小于2可加入，圈内人数不超过2可留下。本人找未带其他GoldDust的活同伴，R4中心采用同伴原始位置。多个可加入目标复用现有 `DungeonExclusiveJoinTemplate`，允许恰好进入一个圈并避重叠；同一组件候选统一使用首个活分摊时钟。旧桥的Goal简化会丢失重叠排除，此处用已有原生几何恢复源Distance约束，没有新增自动执行类别。

AcidRain本人避其他非AcidRain标记成员的量化位置R9，其他视角避目标R9；GoldDust身份没有额外豁免。GoldDust显示R8安全/危险轮廓按本人、人数和其他GoldDust身份决定，AcidRain显示R8默认色轮廓。

UnboundArrow使用目标原始位置，不套分摊的量化规则。绘制按TargetID判断本人轮廓，导航按目标旧引用与当前视角身份判断；源与目标同一演员时不发布本人避队友区。死亡目标仍保留，死亡源暂停该条输出。伤害预测汇总活bait的目标槽位并去重，统一使用首个**已存储**bait时钟，即使首条源已死亡也不换钟；目标不在当前队伍时不虚构槽位。

## 分层验证与执行边界

- 模型：`tests/RoleHeroesPretendersRegression.cs`覆盖六组件精确表、普通AOE真实结束、八次效果/逐轴首匹配/源位置/缓存时钟、取消和销毁不清线、max2内外容量、独立分摊分散冲突、R4同伴和重叠排除、旧目标引用、死目标死刑、首存储预测钟、默认完成/新代/wipe，以及真实公共解算器脱离R13。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplayHeroesPretenders.cs`覆盖双视图、六OID标记、源20边形Contains与中心oracle、八次效果推进、真实读条结束、NPC24槽、目标死/活/销毁、重复ID、回放逆向Seek、机制开关、wipe及owner清理。
- 显示与现有普通AOE/站位导航通过离线模型及生产DLL验证；ARR/MP回放只绘制和规划。GreenMove实际走位、宿主控制权交接、最终游戏像素待实机验收。没有以预览或提示冒充实际执行完成。
- 集中证据：`.tmp/halatali-verify/native-heroes-model-final.log`共64项模型通过，`native-heroes-build-final.log`生产0错误/4项既有Crucible警告；`native-heroes-focused-final.log`两组定向DLL检查通过，`native-heroes-catalog-final.log`全目录613条PASS/0失败。独立复核确认Arrow同代补正保旧引用、同ID替身文字/显示/导航分离及根生命周期清理接线。
