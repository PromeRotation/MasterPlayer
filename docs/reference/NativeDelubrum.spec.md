# 女王古殿原生机制

## 范围与入口

普通版 `Presets/Foray/Entries/DelubrumNormal.cs`，GUID
`masterplayer.foray.delubrum.normal`，使用 `BozjaRegion` 的原生输入、模型、
输出和 owner 生命周期。生产不创建 BossModule 或 MechanicSession。
本规格已验收 DRN1TrinitySeeker、DRN2Dahu、DRN3QueensGuard、DRN4Phantom、DRS5Phantom、DRS3Dahu。
DRN1TrinitySeeker的接续实现与验收证据见本文对应节；不得因区域入口已存在
而接管其他首领 OID。

客户端实查普通 CFC760「女王古殿」双向对应 T936，零式 CFC761 对应 T937。
NameID9751为「大兀」，普通 Boss OID30A6，Marchosias30A7；零式
Boss30A8、Marchosias30A9、Crowned30AA。两套枚举39次声明、38唯一 AID
全部有客户端行且 ExtraCastTime=0。普通22360、零式22378为空名，
不表示缺行。普通 HeadDown22358实际4秒，旋转22362及零式22380实际
.3秒；不采用源注释的3秒或.5秒替代真实输入。

## DRN2 大兀

按源状态入口激活顺序保留十一组件：

| 顺序 | 组件 | 输入与几何 |
|---|---|---|
| 1 | FallingRock | 22364，R4 |
| 2 | HotCharge | 22372，Charge全宽8 |
| 3 | Firebreathe | 22373，R60、半角45° |
| 4 | HeadDown | 22358，Charge全宽4 |
| 5 | FirebreatheRotation | 22361 visual、22362普通Finish、Icon167/168 |
| 6 | Shockwave | 22368/69首段、22370/71次段，R20、半角90° |
| 7 | HuntersClaw | 22359，R8 |
| 8 | FeralHowl | 22357，Away30、ignoreImmunes、StopAtWall |
| 9 | TailSwing | 22367，R10 |
| 10 | HeatBreath | 22374，动态原引用诱导锥R10、半角45°、Tankbuster |
| 11 | RipperClaw | 22365，R10、半角45° |

普通watched AOE/Charge以普通Finish结束，不以Effect、超时或视觉技能代替。
Charge先从原位置到Loc算方向/长度，再量化Origin，保留重复及首source
Finish删除；小范围复用CE53，宽度从Rule的Ray模板读取，旧消费者仍为6。

场地来自源 `DRSharedBounds.Dahu` 的真实解析多边形：
中心(82,137.9855)，R29.5的48边形减上下矩形，44顶点。
`DelubrumArenas.cs` 由专项 `--delubrum-arenas` 导出，不手工近似成圆。
源默认pull仅要求Targetable/InCombat，不额外要求距场心30码；
区域管理保留500码加载边界。只有936/30A6被精确ownership接管。

Shockwave首段Start追加两项：packet Loc/Rotation和其+180°，钟分别为
原NPC结束钟与其加2.6f。次段Start不追加。任意watched普通Finish均
全局FIFO删首，不匹配source。Active查询原地修改列表：首项Risky=true，
总数>1时改Danger色，其他只Risky=false、不改颜色。剩单项的尾段
晋升仍保Color0；已变Danger的头项保色。

旋转分别等待非零visual角和非零Icon增量。未知Icon清增量，零角保持
缺输入；初始化位置取使输入齐备的最后一次回调Actor的量化当前位置，
不固定Boss或CastLoc。每序列五发、间隔2d；22362每次Finish先计数，
再推进序列索引0，空列表也计数，不匹配位置、角或source。Effect不推进。
只有序列数或成功Finish计数改变才重建缓存，每序列future→imminent，
future钟为重建时max(next,Now)+2；全部Risky，最后单发保Future色。
原SDInvertedCircle动态取查询时Primary位置、R5、default钟；按真实宿主
补集转换发布R5安全目标，不能错误采样为空。

HotCharge源AddAIHints每查询一次递增NumCasts，奇数提交以Primary当前任意
CastLoc或当前Arena.Center为中心的R6、weight5偏好；真实22372 Effect也递增
同一计数。Framework规划阶段调用一次，绘制不重复增加。weight5低于
现有宿主StrongGoalThreshold100，实际转换为空，保诊断且不扩自动追冲锋。

FeralHowl保每个原caster的锁点和NPC钟，普通Finish按sourceID删首。
Away方向逐caster链式推进，忽略免疫。墙距在导出多边形源局部坐标计算，
共线墙边必须命中首个重叠参数，包括本人已在墙上的0；随后按源减
Clamp(hitbox-.499f,.5f-.499f,hitbox-.499f)，最大30。危险落点只检查
HuntersClaw，不擅自附加场外危险条件。只画位移线并给危险文本，不新增
特殊位移执行。

击退AI由Primary查询时当前位置朝所有HuntersClaw锁点形成R100、
Asin(8/d)锥的单个raw SDUnion，钟取首击退项。Asin不钳位；NaN成员
按源if(d<min)忽略，不由MathF.Min污染整个Union。真实宿主先尝试全部
成员直接转换，全部成功才按原顺序拆；任一失败则整Union统一有限采样。
直接拆和无效近爪/零距离退化均做源对照。

HeatBreath保存原source/target引用，重复Start保留。方向动态读旧引用的
当前位置，source==target才用source朝向；Finish忽略，真实Effect按
sourceID删首，不看MainTarget。Update才删除source Dead/Destroyed或target
Dead；target仅Destroyed保留，同ID新代不能替换旧目标。Update前target
Dead仍可输出，source Dead立即隐藏但raw全局Tankbuster提示保留。
显示/个人文字按targetID分本人，AI按原引用判定，自己诱导时按存活Party
真实槽序为其他成员生成R100锥。伤害mask取活跃目标当前槽位，钟取raw
首项；仅诊断，不扩治疗策略。

## 离线验收与限制

源测试对照为六个真实DRN2源文件及真实通用组件，输入包含冷激活、普通
Finish、Effect、Icon、销毁/同ID新代、死亡前后Update、重复与空队列。
专项使用实际生产DLL，覆盖真实注册表、双视图预览、补集目标、场地切口、
真实SafeZoneSolver逃离R10且无fallback、机制关闭、owner释放、逆Seek、
回放执行隔离。公共Charge宽度和Cone直接转换提取必须联合验证南方、
札杜诺及Hydatos现有消费者。

源枚举SHA256：
`8ec43923852c646ac7f6aeb298233a3d22a30b8f8c61428497f78220aa409358`。
源DRN2.cs：
`0abc20c43fc4fc5153570b60825c486ed7231117a6cbfaf87e208ecd3f407305`。
源States：
`f313227ccbea69dfba8287b3be6d6e06ae1cd8c3e48d218613250e592995e57d`。
源FeralHowl：
`d3b33c5c1d07f126648bc5fe9d762852511eb3a940d1af7817303677b43759e0`。
源Rotation：
`4bfd92620ba705a73e380e61eb718c70ac5512c91809f1456709bab534b3f409`。
源Shockwave：
`c1bb513d422cd3745539ddc99661d6485d670be76fe04db6814343d097de730d`。

最终日志前缀 `.tmp/halatali-verify/native-delubrum-dahu-20261125-`：model08为253相关模型（230 Bozja+16 Hydatos+7 controls），build02为0错4既有警告，同DLLreplay04为211联合专项PASS，catalog01为1639行首PASS（1630测试+9汇总）及295入口独立编译，全部exit0/0FAIL。DLL SHA256 `c293e3dec89467926eddce770d5ea7bc02902b2d3371f5ab62186d2e1df7194c`。登记边界monotonic435423.156。独立审查已复核十一组件，
发现的共线墙距差异已由真实源反例闭合；不据此宣称通用输入层所有
Start/Status异常屏障完全等价。SDK10.0.303在隔离目录运行，根SDK11、
游戏实时采样、像素和实际执行仍待验；未部署。

中间记录前缀 `native-delubrum-dahu-20261125-`：model01缺using编译失败；
model02夹具DTO默认hitbox0与源.5不一致，修夹具后继续；
model03发现可直接拆的SDUnion被一律采样，按真实宿主算法修正；
model05完成251场景但原退出码未保留；model06十三定向exit0；
model07新代际测试没有先结束旧cast，源手动Start而模型快照未产生Start，
修夹具真实输入后model08联合253场景exit0。replay01漏了源量化起点断言，
replay02误直接引用Smoke未链接的BossMod类型编译失败，改独立源量化公式，
replay03专项通过；最终公共共线墙修正后重建及联合专项。

首轮precode monotonic433630.984；缺完整分段，不报告净编码工时或加速比。
普通/零式事件结束及状态机差异不能因为共用形状而合并。

## DRN4 博兹雅幻灵

普通版六组件按ArenaChange→MaledictionOfAgony→SwirlingMiasma→UndyingHatred
→VileWave→CreepingMiasma顺序接入。客户端NameID9755为「博兹雅幻灵」，
普通Boss30AC、StuffyWraith30AD；零式Boss30AE、Bloody30B0、Misty30B1。
普通11声明、零式15声明去重24个AID全部有行、无空名、ExtraCastTime全零，
Cast表值与源注释逐项相同。OID按BNpcBase/EObj同号行核对，不据此虚构
OID→NameID身份映射。普通精确接管936/30AC，不接管937、30AE、未迁3131。
复用既有普通版入口，独立入口数保持295。

起始场地中心(202,-374)、半宽23.5/半长29.5；缩场后中心(202,-370)、
半宽23.5/半长24。预告形状使用半尺寸24×30减24×24两矩形，
与场地边界尺寸不同。由真实源Shape列表经Clipper导出为两个岛，
`--delubrum-arenas`增加PhantomHazard，已有Dahu导出段逐字保持。

ArenaFeatures1EA1A1的EAnim10002覆盖一个预告，锁当前钟+4d；重复不去重，
到钟不删、不自动缩场；80010清预告并换最终边界，无需前置动画。
其他OID/state忽略。冷激活不补EAnim历史，snapshot首次/变化与真实packet
为各自事件输入，拉起前Flush掉的事件不复活。

共享Phantom.ArenaChange是普通/零式共用的静态AOEShapeCustom。
Polygon在首次求值原点P建立，Distance另锁第一次AI查询原点D。对静态
世界差集G，本次AOE原点A、当前场心C，源Check/文字为G+A-P，
DrawSink为G+C-P，导航为G+D-P。原生模板保留两个独立惰性静态原点，
不因组件重建、owner释放或Seek重置。原生缓存属于本插件程序集，不读取
未迁旧桥内部的隐藏静态状态。典型初始P=D=(202,-374)，缩场后再10002，
预览/文字平移+Z4，导航仍原位置；(202,-392)为真实源反例：
Check及Draw命中，导航不命中。真实DrawSink与实际转换分别对照，
不能用一份重新按当前场心构造的差集代替三种消费。

VileWave22449为R45半角60°，Creeping22437为前50全宽12。
普通watched Finish按sourceID删首，重复保留，Effect/超时不清。
Malediction22447为RaidwideCast原Actor列表，预测每查询读取当前NPC钟，
Party存活mask与原引用Finish沿用现有模型，只诊断不扩治疗执行。

Swirling22441/22442复用现有Exaflare数据和发布缓存，保留独立输入合同：
首段Start取spell.LocXZ、当时caster.Rotation方向×6；不是caster位置或
packet朝向。Donut5–19、1.6d、8发、最多2。普通First/Rest Finish均按Loc
逐轴≤1从头匹配首条，不匹配caster；命中后以实际Finish位置+Advance、
Now+1.6更新，八次后删线并计NumLinesFinished。未命中静默，Effect不推进。
输出只有Count/version变化才重建，全部future先于全部imminent，
输出位置量化，future钟max(next,Now)+1.6；两种均Risky，
仅imminent为Danger色。最后一条raw线删除后、Update前，自定义AI已回
无线分支，落点危险仍消费旧缓存。

Undying22444为向前30码、ignoreImmunes，不启用StopAtWall。
重复casters按列表链式推进，Finish按sourceID删首；危险落点检查当前场外
或Swirling发布缓存中Danger色甜甜圈，future虽Risky但不用于该落点判断。
不因墙边截短30码，不把位移线扩成自动动作。

自定义AI仅用首caster钟。无线时源SDRect由当前场心±24/16倍锁定方向的
两个已舍入世界端点构造，随后WDir reciprocal乘法归一化，再经真实桥
二次归一化恢复矩形。原生保留全部运算，不能写死40长或直接场心+4方向；
六角度、raw lattice、独立边界点及最终长度/中心/朝向均精确源对照。
有线时每条raw Line.Next+Advance*6.66形成世界轴向2×2的SDInvertedRect，
合成一个SDIntersection；原生保raw交集并走同一有限采样。
真实桥不把此根对象识别为补集，典型几乎全危险布局超过65%门槛而返回空，
不能绕过它发布方块安全目标。

真实Feed六组件冷激活/失落动画/后续动画/缩场、Normalize禁区、提示、
伤害及转换后几何均比较。实际DLL专项覆盖普通Finish、Effect不清、
真实多岛注册、第二次动画缓存、地火八次推进、无线危险矩形与有线采样
拒绝的不同输出、真实SafeZoneSolver在初始场内逃出矩形且无fallback、
机制开关、owner、逆Seek和回放执行隔离。最终日志前缀`.tmp/halatali-verify/native-delubrum-phantom-20261125-`：model06为262相关模型（239 Bozja+16 Hydatos+7 controls），build01为0错4既有警告，同DLLreplay01为218联合专项PASS，catalog01为1646行首PASS（1637测试+9汇总）及295入口独立编译，全部exit0/0FAIL。DLL SHA256 `6dcd456b3a2c5c5bae66e252432b168a2c728cbf02730b9f3de0ccdb02e8f7c1`。独立复核已确认无线矩形端点舍入、reciprocal乘法和桥二次归一化可达路径；未发现新遗漏。

源文件SHA256：

| 文件 | SHA256 |
|---|---|
| ArenaChange.cs | c5349eb968dfaae2ee95dfd8a0562a76eab233d3390ddb7a44153c3f551395b4 |
| DRN4.cs | be64206b0fc3d23c197aeffbf862d387632f4c7bdb63534618e235f643f1b336 |
| DRN4Enums.cs | aa52b67fe6e97713d4fc0cee2bc5b29f8ced7a7ff167e6b60f80117abdf75096 |
| DRN4States.cs | c9d8ca047e9eb688c2c7a182e5ce28111b70e973fa6d69455266abd80c924896 |
| UndyingHatred.cs | 8061d06b6b382e85d384daaf9695a8b5a5a58d413e9e11acf87805a078db78b9 |

中间日志前缀`native-delubrum-phantom-20261125-`：model01使用错误ReplayStatus
构造而编译失败；model02源夹具未推进ElapsedTime导致动态钟断言失败，
按真实elapsed输入修复；model03八定向通过。model04联合261通过，
补无线矩形raw严格断言后model05复现reciprocal乘法与直接除法的数值差异，
生产修复后model06联合262通过；没有放宽raw距离或转换参数断言。
precode monotonic435513.656；缺完整分段，不报告净工时或加速比。
SDK10隔离、根11及游戏实际采样/像素/执行未验，未部署。

## DRS5 零式博兹雅幻灵

入口为 `Presets/Foray/Entries/DelubrumSavage.cs`，GUID
`masterplayer.foray.delubrum.savage`，T937/CFC761、OID30AE。
只接管该首领，未迁零式首领保留桥。本节八实际组件及全部已实现源状态已完成离线验收；
不将该首领完成视为零式全副本完成。

八实际组件按源阶段启停：ArenaChange常驻；MaledictionOfAgony只计
23350真实Effect，不套普通Raidwide提示或伤害预测；两轮Weave的CastEnd
进入时依次新建Creeping、Lingering、Swirling、MiasmaCounter。不是等Weave
结束才启用，也不将八组件改成全程常驻。第二轮计数与缓存为独立新实例。
ArenaChange复用普通/零式源共享静态Shape的双惰性原点语义。

状态机Start/End读取`Primary.Actor.Cast`原始读条，组件回调继续只消费
Spell。Ability非空也推进Start、保持End等待；意外AID记录诊断，不拒绝
状态推进。所有状态按链接顺序，同帧可多次推进，转移钟取当前帧。
ComponentCondition默认checkDelay=0，满足立即推进，否则expected+1秒
推进并留overdue诊断；Cast等待实际raw null，不按预测结束钟推进。
两轮Miasma完成条件是三实例累计八条，不是八次Effect或八次普通Finish。

原生时间轴逐项对照真实`MechanicTimeline.Capture`的ID、名称、预计钟和
全部Hint；保留两处“Miasma start”、CastStart/End、GroupWithNext、
Raidwide与DowntimeStart/End。Phase名称本地化为“博兹雅幻灵”。

Creeping22452 First Start追加前50全宽12矩形，只显示首两项。
First/Rest22453普通Finish按LocXZ精确相等找首项，以AOE的ActorID字段
累计三次才移除并递增NumCasts；不匹配caster，不使用1码近似，不因
Effect、预测钟或销毁额外清理隐藏项。

Lingering22454/55为R8，Swirling22456/57为Donut5–19；两者都是
Actor当前朝向前6、1.6d间隔、八次普通Finish、显示最多两发的独立
SimpleExaflare实例。按各自Rule保存Count/version及发布缓存，Circle事件
不得刷新Donut已缓存的过期future钟，也不能跨Rule匹配相同Loc的线。
每实例future先于imminent、量化显示位置，两者Risky，仅imminent Danger。

Adds在Boss实际不可选中后创建，Bloody30B0/Misty30B1均保存原Actor列表；
首次按当前字典顺序、Created追加、Destroyed删除，Dead保留，ActiveActors
只筛Targetable&&!Dead。Dead、不可选中、销毁和同ID新代顺序分别核对。
真实DrawSink产出ActorMarker，既有DrawConvert不消费该命令；默认
priority0/forbidDots=false也未由旧导航桥转为自动选敌。因此保留原列表和
阶段条件，不虚构AOE、禁攻或新目标策略。

MaledictionOfRuin只等原始CastStart后进入显式43秒Timeout，随后永久停在
“???”；100秒是显示元数据，无状态Update，不自动完成。该尾段Arena及Adds
仍激活，后续真实Arena动画继续生效；不补造源TODO的后续瘴气/击退/VileWave。
死亡、销毁或HP0才结束DeathPhase并释放组件。

中间日志前缀`native-delubrum-phantom-savage-20261125-`：model01四项通过、
一项失败，原因是新夹具未向公共比较器传源Exaflare的Colors.AOE，修正后
model02五项通过，model03联合267通过。独立审查发现raw Ability与时间轴
元数据缺口后修复，model04联合269通过。replay01/02把场地边框计入AOE数，
修为分组计数并保留边界断言；replay03三项专项已过，随后查询离线宿主未
初始化的GreenMove实例而抛异常，改为核对脚本自身控制权和PR请求字段；
replay04联合通过，但其DLL先于raw/时间轴修正，不作最终生产证据。
最终model04为269相关模型（246 Bozja+16 Hydatos+7 controls），build02为0错4既有警告，同DLLreplay05为223联合专项PASS，catalog01无筛选1652行首PASS（1643测试+9汇总）、296入口独立编译，全部exit0/0FAIL。DLL SHA256 `d00d938214eec7cec0be6f6df4f2cc395179bbd11a6b7ac5e659172fed451917`。独立复核已确认raw Cast/Spell消费分离、全时间轴元数据和新增Adds反例，未再发现可达遗漏。SDK10隔离，根SDK11、游戏实时采样/像素/实际执行仍未验；未部署。

源文件SHA256：

| 文件 | SHA256 |
|---|---|
| ArenaChange.cs | 4debaee3db007bf7d75cdd6a169be8617086c8c156f214ba552bb54ec1d0f83f |
| DRS5.cs | 5cc78f185ab9625f31374cfb0254f2a54d2e6ee928a98c851614939e16212a75 |
| DRS5Enums.cs | a6e506c5278d04d44868e05122782921bf8eb3c714574377dc860549d0fc48df |
| DRS5States.cs | 3a9b4adb7fd73c4180f69bb19704fcc132100a48399b321a3ff4eeb91805ab94 |
| Miasma.cs | 7aaede5612f297674c8ad048262bf08cb85dd7b8f98a1d7a5e1dc4b92b2620f3 |

## DRS3 零式大兀

复用零式入口 `masterplayer.foray.delubrum.savage`，精确T937/OID30A8，
保留未迁首领的桥。原生模型按源完整状态链接逐组件激活，顺序不是Catalog顺序：
FallingRock、HotCharge（两独立实例）、Firebreathe、HeadDown、SpitFlame、
Shockwave、HuntersClaw、FeralHowl、FirebreatheRotating、Burn、HystericAssault。
五标准形状复用已验AOE/Charge，HotCharge无普通版的查询计数/弱goal；
Shockwave和旋转保留各自FIFO、原地颜色/Risky、回调量化原点及发布缓存。
普通Finish、实际Effect、源NPC钟分别消费，不用超时统一清理。

`BozjaDelubrumTimeline`从已验Phantom引擎局部复用，不重构其他首领。
Cast返回End，ActivateOnEnter在检测到raw主怪Cast后重播当前Spell；
Spit与旋转挂在等待Start的OnEnter，赶在图标前。第二轮旋转的HeadDown在
End进入才创建。所有激活按源列表顺序，冷重播逐Actor，不补历史Icon；
同帧多转换按当前钟重置，无预计时间结转。HeadDown expected1/maxOverdue5，
可提前满足或满6秒强制推进，5.9秒仍保留。保留0x40401回0x40310的非单调ID。
状态机先Update，再组件删Dead；raw列表仍影响当次条件。
16项时间轴逐项比较真实MechanicTimeline的ID/名称/预计钟/所有flags。
尾部100秒“???”没有自动结束；源TODO不补造，死亡/销毁/HP0释放。

Spit图标79–82用回调原Actor，忽略targetID。重复继续追加spread，顺序槽替换，
多槽同对象取首槽。R4量化轮廓、R5量化导航；本人判断按原引用。
Dead查询立即隐藏，Update才删raw，顺序槽不删；Destroyed不等于Dead。
22391真实Effect跨来源按MainTargetID删全部匹配代际，不按caster或单项去重。
无预测钟/伤害预测。Marchosias原引用缓存不排Dead，Destroyed才移除，
恰R4边界可满足“Hit at least one add!”；不额外生成靠近add目标。
Crowned出现条件仅Targetable，Dead但Targetable仍成立；marker不新增AOE或自动选敌。

Burn图标87保存当时原Primary，目标为World.Find(targetID)或回调Actor。
R30目标中心动态位置，8.2f先float再提升double，钟为图标时刻；不采用枚举注释R45。
显示按目标ID区分别人填充/本人轮廓，导航按原引用；本人对其他存活Party依槽序
发布当前位置R30，Source==Target则不发布本人诱导区。mask合并当前目标槽，
判定钟统一取raw首项。仅过滤Source.Dead；Target.Dead在Update才删，
Source/Target仅Destroyed均保留。21603真实Effect跨source全局FIFO，
目标ID不参与消费，空列表仍计数。只作既有伤害诊断，不新增治疗或动作策略。

FeralHowl/HystericAssault分别22375/23349与22392/23363。
Start覆盖单项，原点锁caster当前位置而非CastLoc，NPC钟加2.1d或.9d。
普通Finish、Effect和Destroyed都不自动删项；Effect只增加各组件独立计数，
状态机随后停用。IgnoreImmunes，Away30，按真实44点墙距及hitbox修正；
零方向不位移。源StopAtWall默认DestinationUnsafe=false，因此不复用普通版
爪区危险提示。AI独立使用查询时Primary当前位置、原爪锁点R100/Asin(8/d)
单SDUnion；不钳Asin，不补源墙安全TODO，不把显示位移扩展成自动执行。

最终日志前缀 `.tmp/halatali-verify/native-delubrum-dahu-savage-20261125-`：
model03为277相关模型（254 Bozja+16 Hydatos+7 controls），build01为0错4既有警告，
同DLLreplay03为228联合专项PASS，catalog01无筛选1657行首PASS
（1648测试+9汇总）、296独立入口，全部exit0/0FAIL。真实registry求解器逃出
目标中心R30且落在真实场地内，无fallback；owner、机制开关、反Seek及回放
无GreenMove/PR朝向/选敌请求均验。两轮独立静态复核覆盖状态链及特殊组件，无新可达差异。
DLL SHA256 `031fa8d17d1c553b8b459cf28c3e868952b626e6c6761b4592a6aad4697ba720`。
SDK10.0.303隔离构建，根SDK11及游戏实时采样/像素/实际执行仍未验；未部署。

失败证据保留：model01已修初版构造参数/只读字段，9定向通过；model02有1失败，
夹具伪造玩家发出NPC击退Effect，超出实际Feed路由，改为另一真实NPC源，
保留“跨source计数而不删KB”断言后model03联合通过。replay01在Spit结束后
错误期望Shockwave已激活；源此时仅进入等待Start，改断言为0x10200及Spit退休，
不改生产和预期语义。replay02五DRS3专项通过，replay03联合通过。
试点12源全部完成，但无完整分段工时，不能宣称实测加速比。

源文件SHA256：

| 文件 | SHA256 |
|---|---|
| DRS3.cs | c4d5a2b27d1c0801e6ac85a4423cc544f25fab57e57442d72a9263a641338ab8 |
| DRS3Enums.cs | 55d193e13d305bbd5b1939256b4be2c4a0d50864c89ca78d336346f0b8b47642 |
| DRS3States.cs | 6858230fc2092e17ebc3fa6b84de8c8135e21420b17f581645c31b348343209b |
| FeralHowlHystericAssault.cs | db4c4696d598c658303a673615bcd0f7dcc2729a7cd9d192b118b7e03db47e7c |
| FirebreatheRotating.cs | d15942044776b841ad61b677c90a02d20f4b9fbecf6ef5803265de98aca0ce97 |
| Shockwave.cs | 6892208f34f09fca7a4ed2ef1cfcc212e56bbd07b10c643bd600b6fd642a5655 |
| SpitFlame.cs | 5392cdc1fa7a35f922fa4bdb036286b1af83f3dc0d62d300bde9c7a5f71515c5 |

## DRN1 求道之三位一体

普通入口复用 `masterplayer.foray.delubrum.normal`，版本1.0.2，
精确身份为T936/CFC760/OID3131/源NameID9834，中文名由本机BNpcName核实。
全枚举44个唯一AID均有客户端行、ExtraCastTime全0，23194和14588为空名。
23212、23219、23221的客户端Cast值与源注释不同；仍使用源组件的
Created／实际Effect／普通Finish合同，不用静态表改写事件。

十三组件按源顺序常驻：
ArenaChange、MercifulBreeze、MercifulBlooms、MercifulArc、BurningChains、
IronImpact、ActOfMercy、BalefulBlade、BalefulSwathe、IronSplitter、
MercyFourfold、MercifulMoon、DeadIron。TrivialPhase按原Primary死亡、
Destroyed或HP0完成；不额外补造阶段状态机。

Breeze23211为前50全宽5，Blooms23213为真实R20，Act23218为长50半宽4
十字，Swathe23220为R50半角90°。沿用普通watched Finish按来源ID删首，
不由Effect或过钟删除。十字导航按真实旧桥拆成两个全宽8／全长100矩形，
显示仍为一个十字；本批新增转换只影响这个实际消费者。

场地由真实Shared Bounds导出：Starting为48边R30减四挡板与南北入口，
Default为48边R25.5减四挡板，均向内offset0.5。解析中心分别为
(0,277.925)和(0,278)，采样半径取源Bounds.Radius导出常量。
原模型保岛与孔，旧桥导航场地仍只消费顶点最多的外环。
23222仅在大场地预告Donut25–30，锁当前中心、原NPC结束钟+3.8d；
普通Finish不清，过钟不清，只有Map1B/00020001缩场并清环。
Map低高字为Param1|(Param2<<16)，不同于EObjAnimation。
不因技能注释另加Raidwide伤害预测。

Blade的23201／23202分别生成四个外环20–30／内锥20的六边挡板扇区，
半角22.5°，方向45°+90°i。导出器直接调用真实
AOEShapeCustom.GetCombinedPolygon(StartCenter)，不经场地二次居中推导。
每次Start覆盖单项、钟为NPC结束+0.1d；23336／23337任意真实NPC
Effect清空，普通Finish不清，不扩展源注释里的自动击退。
安全色填充和两种专属提示不附加通用GTFO。Check与导航锁Start原点，
DrawSink把同一相对polygon放在当前Arena.Center；Map缩场后显示平移
+Z0.075，旧Check／导航保留。缩场后新Start使用独立Default polygon。

Blade导航保留SDInvertedPolygonWithHoles的旧桥消费：该类型不在
IsComplement白名单，实际走0.5网格、65%覆盖门槛的有限采样，
本批两variant布局返回空，不绕过门槛发布四块新安全目标。
原始距离在polygon外同为0；内部本地求全部边最近距离，源用SpatialIndex
近邻边，**只承诺旧桥实际消费的Distance<=0符号及转换几何等价，
不宣称正幅值逐点相同，也不得把它用于新增距离排序或余量策略**。

Splitter23203按caster原位置到当前场心距离严格
<3、(9,11)、(17,19)选择R4／8–12／16–20，否则4–8／12–16／20–25。
随后量化场心，每次追加三项；任意caster的watched普通Finish清全部，
不按各层实际伤害Effect推进。

Mercy2056的F7/F8/F9/FA为−45/−135/+45/+135°，位置取回调actor量化值，
朝向优先raw Primary CastInfo，包括非Spell；nullable旧回放字段先由
读条持有者Primary.Rotation补齐，只有Primary无Cast才回退状态actor。
钟为Now+13.4d+1.7d×当前raw Count，显示前二。新增或弹首时原地晋升：
首Risky=true，有次项才染Danger，反向≤1°仅令第二Risky=false；
不统一重置隐藏项的颜色或风险。23348真实Effect全局FIFO，无TTL或Lose删除。
冷重演按组件→actor→真实60槽，冷Pet参与；热Pet状态只更新快照而不派发。

Arc Icon243／Effect23223与DeadIron Icon237／Effect23216为独立FIFO。
各持原Primary／Find(targetID)??回调actor，延迟5.1d／4.6d，
R12半角45°／R50半角15°。SourceDead只隐藏；Update仅清TargetDead，
Destroyed与同ID新代不重绑。显示和文字按ID，AI按原引用；
本人诱导按存活Party原槽为其他成员造R100锥。诊断mask取目标槽，
各组件钟取自己的raw首项，Arc为Tankbuster，DeadIron仍为源默认Raidwide；
Arc全局Tankbuster cleave按raw非空，不增加治疗执行。

Chains128双向按回调旧Party槽存partner原引用；Untether按当时槽清两端，
不补回热帧中尚未创建的目标，不因目标Destroyed重绑新代。
冷激活在完整世界／Party提交后重演已有Tether，因而与热输入不同。
前景线与Break the chains!读取该伙伴；导航为伙伴当前圆心、
双方当前距离+1的禁圆，钟每查询取Now+10d，不按建立钟超时。

IronImpact由真实14588建立原caster→原target的动态前50全宽8线，
Now+5.9d；只有raw剩一项才允许23221先重绑实际目标，再按目标ID删首。
普通Finish、目标Dead／Destroyed不删除，SourceDead仅隐藏。
文字／背景颜色按targetID，AI用原引用；source==target时文字／绘图使用
原source朝向，AI仍用零向量Atan2。文本矩形Check保留源double SinCos
再缩float路径；斜角有限边界反例已通过真实CLR确认，不能用MathF替代。
不补源TODO中的四人足额判定。非目标一线补集／多线OutsideOfUnion经
真实旧桥展开安全矩形目标；raw“恰好一条内”SDF与适配目标并集分开。
本人避其他线全宽16，以所有raw目标排除buddy，剩余存活Party R2
倒置圆Intersection走有限采样，不改成新增追人强目标。
Shared预测mask为ulong.MaxValue，钟取活跃线最早值，仅诊断。

Moon3132 Created覆盖唯一眼，锁未量化原位置、Now+5.8d；移动、Dead、
Destroyed、过钟不清，23212真实Effect清。沿用凝视眼、弧和受门控的
面向规划，不进入CE34的目标优先级分支；冷Created枚举最后一个眼获胜。

最终验证使用同一生产DLL，SHA256为
`70e53cb8c0e7a4ab0aa86a4ea54a1d86e4037a5c018eb37906097e802ba63d53`。
日志均在`.tmp/halatali-verify/`，前缀`native-delubrum-seeker-20261125-`：
model-07为293相关模型场景PASS/0FAIL（其中本模块16场景）；
build-03为0错4既有警告；replay-05为南方/札杜诺/女王古殿联合240条PASS；
catalog-02为无筛选1669条行首PASS（1660测试及9汇总），296独立入口编译。
各命令明确exit0。SDK使用临时目录的10.0.303，根要求的SDK11未验，未部署。
实际DLL专项含13组件Feed、R20真实solver无least-danger回退、普通Finish、
Map、Blade旧桥采样拒绝、图标/线分摊诊断、Chains、Moon、反向Seek、
owner清理和回放执行隔离。游戏像素、实时采样及执行仍待实机。

保留失败证据：model-02名称/十字拆分及链夹具错误、replay-01旧ownership断言、
replay-02未创建Effect来源的夹具均已修正。model-05真实CLR斜角Check反例
先失败后按源double SinCos路径修复，不放宽断言。独立复核另发现Primary
nullable朝向回退错误与单元素假slot59夹具，分别修生产及完整60槽输入；
缩场后Blade原点、冷Pet双槽、冷热Tether与四图标交错队列已补入最终模型。
源仅作测试oracle，生产未包装BossModule或MechanicSession。

本批源SHA256：

| 文件 | SHA256 |
|---|---|
| ArenaChange.cs | 1c4a6549e011a26e85c39e1c10af0ed7fbd7e1daf9d8994b35a6b5dea6f66a95 |
| BalefulBlade.cs | 3bd0f22016bf1eb669311207f0be89d80d8bd12caaa47e819d29ecbf5687ea7e |
| DRN1.cs | bafa1d3c450299ecfd585f3fa7a3491a52b498881ce5d977747bd6696acb740b |
| DRN1Enums.cs | e4bb409dc1be0e2908b736da4bb3785796de6751f7c29bd66c2d1bb287e17d8e |
| DRN1States.cs | 98b319c19691afc20badc16363d11b043a7900168f2e66b881aadd1158e4506b |
| IronSplitter.cs | cf9d2b677d464f5dfdab09d992763768653c556bc530101312af3c425e4308be |
| MercifulMoon.cs | efaddebdd382255af484a725d98abb67c7fb264aa96129ae2a7eab3cc9015a62 |
| MercyFourfold.cs | d8c5f6ce19607f0f1a5f12c1b1aac8ffddf7c295758fa1598190043bd4a9b563 |

## DRN3 女王卫队

本节已完成原生离线验收；最终验证与审计登记证据见节末。
源为`DelubrumReginaeNormal/DRN3QueensGuard/`，T936/CFC760，
Primary OID30BA、NameID9838客户端正式名“女王骑士”。共47唯一AID
均有客户端行、ExtraCastTime全0；22584、22615、23187、23408、23409空名。
复用普通古殿入口及GUID，独立入口数不增加，未迁Boss继续由旧桥提供。

十组件按源顺序常驻：ArenaChange、BloodAndBoneQueenShotUnseen、
RapidSeverShotInTheDark、Enrages、OptimalPlaySword、OptimalPlayShield、
CoatOfArms、AboveBoard、TurretsTour、PawnOff。TrivialPhase仅按Primary
Knight Dead/Destroyed/HP0结束，不等待四王全部死亡。标准Sword22514为R10，
Shield22515为5–60环，Pawn22534为R20；普通watched Finish按来源ID删首，
Effect与超时不删。两组四动作伤害提示为全局提示，Raidwide/SingleTarget
动态读取原caster当前NPC钟及当前目标。Enrages是四动作CastHints，
其第四参数true为ShowCastTimeLeft，输出首个原caster剩余时间，
并非另外生成TimedEnrage伤害预测。三组cast列表按原Actor引用删首。

Shared真实Bounds导出为GuardStarting/GuardDefault：起始48边R29.5减
南北矩形，解析中心(244,-162.104)，默认48边R25、中心(244,-162)；
采样半径分别29.5/25，不用近似圆。ArenaChange仅在未缩场且无现存预警时
惰性查1EA1A1缓存，EventState0且XZ分别距(244,-129)≤1时锁当前场心、
25–30环与Now+5d。死亡、移动、Destroyed及过钟不清。
仅Map18/00020001换Default并清环，永久关闭检测；缺历史Map不推测缩场。

AboveBoard：30BD每次Created追加量化位置R10，钟Now+14d；23187 Start先全清，
再按30BE原Enemy缓存顺序锁量化位置，钟为原NPCRemainingTime加15.1f转double。
22525/23402真实Spell Effect全清，30BC真正Untargetable全清；
Short Effect、普通Finish、Death、Destroyed与超时均不清。
冷激活逐组件→Actor重演Created→Start→Targetable/Untargetable，
Bolt/Helper/Warrior次序影响结果；热新建false不合成Untargetable，
已有对象true→false才派发，位于同Actor普通Finish/Start之前。

TurretsTour：22541每次Start先追加LocXZ与caster未量化位置之差得到的长、
MathF.Atan2方向和量化原点，全宽6，钟取该caster当前NPC结束。
仅当前AOE总数等于30C2缓存数整数除2时按缓存顺序追加全部炮台；
不是“初始化一次”。每个炮台以当前原位置/朝向检查前50、后0、半宽0.1
闭矩形，严格更小平方距离选最近，等距保首；命中长度MathF.Sqrt，
无命中长50，半宽3，位置不量化。生成后位置/方向/长/钟均锁定。
22541/22542/22543真实Effect按来源ID删首，即使来源已被同ID新代替换；
普通Finish/Destroyed/过钟不清，30C1真正Untargetable全清。
所有Enemy缓存首次只按OID遍历Dictionary.Values，后续Created追加、
Destroyed移除原引用，Dead/Targetable不筛选，不统一按ID排序。

CoatOfArms：SID680按Actor ID存实际低四位与预测高四位。
22518预测Front|Back，22519预测Left|Right，保已有实际位；Gain覆盖全部预测，
Lose只清实际位。Gain Extra0保留零字典项，Lose结果0才删除。
真实60槽按槽序派发；任一槽Lose清低位，即使另一个同SID槽仍未变。
冷状态包括Pet；热Gain/Lose及Destroy引起的Lose排Pet。
正常Actor Destroy会派真实StatusLose，清实际位但保预测；同ID新代继承残余
ID状态。普通Finish、Counterplay、过钟均不直接清理。

方向判定保XZ差、MathF.Sqrt、先1f/length再乘分量、double SinCos缩float；
前点积严格>0.7071067f为Front、<-0.7071067f为Back，
否则左点积>0为Left，其余Right，同点Right。文字只查询Active Ward30BB
（Targetable且非Dead）的当前TargetId，命中实际|预测禁侧才提示。
AI则遍历状态ID并Find当前Actor，不限制OID/Dead/Targetable；
当前站位命中禁侧且正在选中该ID时发布全部禁侧R100半角45°扇区，
顺序Front/Left/Back/Right、钟MaxValue；安全侧整体撤销。
实际位非零且命中实际|预测任一禁侧时保留PriorityForbidden意图诊断。
旧`MechanicNavigationHost`不消费PotentialTargets/ForbiddenTargets执行，
因此本迁移不新增PR禁攻、全停手或目标切换。

前景盾弧按Active Ward缓存顺序，Front/Back/Left/Right、R1.5、
当前朝向±45°，分段公式复用源DrawSink运算路径；实际位优先颜色，
否则预测颜色。模型保颜色类别；真实旧DrawConvert把两者均转非safe
相邻线段，生产按该消费映射同时交给雷达与世界绘制。
ActorMarker原桥没有转换case，不将四王DrawEnemies缓存扩成新选敌策略。

最终DLL SHA256：
`4dc1d3f0389db35560d7b6af417672179041376e2da32bca9392f5ac3f0c1d53`。
日志均在`.tmp/halatali-verify/`，前缀`native-delubrum-guard-20261125-`：
model-08为306相关场景PASS/0FAIL，其中13场景为DRN3，含十组件真实Feed、
真实60槽/两同SID槽/冷Pet热过滤/Destroyed同ID新代、冷热不可选中和四种
Actor冷顺序、炮台0–6数量/重复/再触发、真实DrawSink逐顶点及实际转换。
build-01为0错4既有警告，replay-02为古殿53条PASS，
replay-03南方/札杜诺/古殿联合247条PASS，catalog-01无筛选1676条行首
PASS（1667测试+9汇总）及296独立入口编译；均明确exit0，同一DLL。
实际DLL含真实R20 solver成功且无least-danger回退、所有特殊组件、
精确来源抑制、两视图发布、反向Seek、owner清理及回放执行隔离。
使用SDK10.0.303临时目录，SDK11未验，未部署，实机采样/像素/执行待验。

失败证据保留：model-01与07分别为测试命名空间/泛型键类型编译错误；
model-02重复Start未同步两侧和主Actor SubKind夹具错误已修；
model-03空AIHints未建立目标数组导致优先级断言错误已修；
model-04真实Feed检出全局伤害提示与普通CastHint配置不符，修生产配置，
并补四动作原钟/原引用与Enrages动态倒计时。replay-01 solver严格断言
误用施法者位置而非记录Loc锁点，改为检查真实锁点，生产未改。
没有放宽几何、时钟、列表或输入断言。两轮独立静态复核未发现剩余
确定可达差异，提出的冷热新建false及状态/炮台代际反例已在最终模型补验。

本批源SHA256：

| 文件 | SHA256 |
|---|---|
| AboveBoard.cs | b9d21a7ae688eaa2e71596b60c9b863bf287a43ee0d983b7b7a8cf35384476b6 |
| ArenaChange.cs | 4b3f886b0933d3139d7de8e00b34ba57d70e863d7faf0be49d220417ae128683 |
| CoatOfArms.cs | c2f38bb0e5cdf30921a4da543d48f6e14418418b9e6206f2d83a493b4d90cdad |
| DRN3.cs | 94fcc564cae3946f5cea02c579ae7e211014036dd30b224c2d4567b33ba691ee |
| DRN3Enums.cs | 6a7db975a19812fee35177b08bd7a6c3ccd271c847783398675a9130d0493b39 |
| DRN3States.cs | b76521f7774a86b300d764706eca7b4e021cf7e6443af04a12ede189c33aca0c |
| TurretsTour.cs | 0e8def114d2e75bd6aa19a8dd974e64de4cf0c59726bd83f7eb2637cace7f84b |

## DRN5 誓约之三位一体：保留桥与待解输入合同

本机客户端核对CFC760↔T936、NameID9853正式名“誓约之三位一体”；
主OID30E6；源48唯一AID均有行、ExtraMS全0，五个空名为22914、22915、
22916、23332、23353。22884–22887表中CastMS6000，与源注释8秒不同，
实现必须消费实际NPC钟。十组件与24槽温度、四圆XOR、两种武器和箭阵
已经逐源审查，尚未新增原生生产接管，不计入已验数。

阻塞不是缺少可推导的静态参数。`ShimmeringShot.cs:25–41`的Unknown
分支得到空数组，`remap == default`不成立，随后访问`remap[srcRow]`
越界；无玩家温度也会先越界。冷激活不回放历史Map，已有箭因此令
BossModuleManager在Update catch中记录crashed并卸载整个模块。
热Created则经ActorState.OpCreate先写World字典，回调异常阻止Feed加入
tracked；`MechanicHostController`实时/回放帧catch撤导航/绘图但保留会话。
下一帧仍有该箭会再次OpCreate并再次异常，排队Map也因ApplyFrame早于
Flush而不能先恢复。不能统一成跳过Unknown箭、重建会话或默认一个图案。

解锁需要能恢复冷激活时四种Map图案的真实历史输入，或明确批准的MP恢复
合同；不能仅用已知图案PASS关闭该分支。保留源与桥，转迁独立DRN6。
后续实现四圆时，恰两安全为XOR减危险并集，其他为安全并集减危险并集；
64边、2^20 Clipper及每轮float往返、24槽共享列表别名、原点/Check/Draw/
首次Distance缓存须分别对照。取证不可额外调用GetCombinedPolygon覆盖
exactly2分支手写的Polygon。安全custom的旧桥有限采样与反转圆Goal不同。

## DRN6 天佑女王

本机表CFC760↔T936、NameID9863正式名“天佑女王”；主OID310B。
源47唯一AID全部有行、ExtraMS全0，五个空名为21928、23360、23436、
23437、23499。十四个组件按源顺序常驻，Primary的Dead/Destroyed/HP0结束。
实现位于`BozjaEncounter.Queen.cs`，与Guard仅共享已经核对相同的炸弹、
炮台几何和缓存算法，保留两者不同的Created时钟与结束条件。

- ArenaChange：起始中心(-272,-415)、半宽29.5方场；22984仅Radius>25时
  覆盖Donut25–43，锁当前Center、NPC结束+4.8d。Map19的00020001/
  00400001换真实48边R25导出，00200010换半宽25方场；三者均清预告。
  普通Finish/Effect/过钟不清。QueenDefault由既有导出器生成，不手写圆近似。
- 标准组：NorthswainsGlowPawnOff为22980/23002 R20；JudgmentBlade为
  23426/23427前70半宽15；二者Grouped Finish不核动作、按来源ID删首，
  99项排序沿用源RefSort。22988 R10、22989 Donut5–60、22983前60半宽5
  均为普通watched Finish按ID删首；Effect/超时不删除。22985全局Raidwide，
  22981全局“Tankbuster with doom”，保存原Caster并按当前原Cast预测伤害钟。
- AboveBoard：Bolt310E Created锁量化位置、Now+21d；23360 Start全清后按
  Burst310F惰性原引用缓存顺序锁量化R10、NPC结束+15.1f。仅22994/23430
  真实NPC Spell Effect全清，不采用卫队的Warrior Untargetable清理。
- TurretsTour：23008先追加Helper原点量化、未量化delta长度方向的矩形；
  raw数恰等炮台3113个数整数除二时追加全部炮台。原位置/朝向、前50后0/
  半宽.1选严格最近，等距保首，无目标长50；共用Helper原钟。
  23008/23009/23010按来源ID删首，普通Finish、Gunner Untargetable不清。
- Chess：0–23槽；2474–2476 Gain置距离2–4并覆盖共享Now+22d；
  2480–2482 Gain按当前Actor位置追加实体格，不清旧列表、钟取已有共享钟，
  默认Risky=true；任一Lose清整个人列表，Edict Lose仅清pending。
  无实体格时按当前位置生成准备格、Risky=false；固定ArenaCenter、10码
  网格、Math.Round ToEven、dx外层/dz内层，只保5×5内恰曼哈顿距离的格。
  2056/E2/E3/E4按当前朝向推进1/2/3格，仅恰两排除点时去掉两整行和两整列。
  排除点不量化。准备格使用每槽和格序号唯一的稳定负绘制ID。
  AI始终对所有Risky格的反转矩形取交集；准备格非空但Risky全false时仍
  产生空intersection、raw距离float.MinValue；真实旧桥采样因填充>65%
  而零primitive。中心八个实体格的外侧也超过该阈值，保持零导航输出，
  不为显示格额外制造Goal。
- MeansEnds：2056/E2/E3/E4最多两十字，终点量化、R60半宽5、原Actor朝向；
  22969 Start只置后续short，现有十字不改，生成钟Now+11.7d/22d且初始风险
  等于short。查询看到当前槽实体Chess列表非空时，原地晋升全部共享十字
  为Risky=true，之后不降回false。22970–22973任意来源普通Finish清十字/
  排除点并复位short，不清个人格或pending。冷回补严格逐组件逐Actor，
  先完整Chess后完整MeansEnds，保主怪Start与移动Actor状态的两种22/11.7钟。
- HeavensWrathKnockback：22983 Start锁caster当前位置，两个顺序来源
  为packet Rotation±90°、前向[5,60]/侧向[-50,50]、20码，不贴墙，
  成功位移才推进下一段from；任意22983普通Finish全清。真实60槽冷免疫
  仅回补免疫组件，热状态按旧Party，不额外重放Chess/MeansEnds。
  落点提示与前景线按免疫过滤，AI override仍无条件发布源固定倒三角内侧Goal，
  与viewer高度一致；不根据免疫撤掉该Goal，也不新增实际击退/移动执行。
- Doom：910 Gain按原Actor去重；Lose可重复入pending，Update检查该原Actor
  是否仍有任一真实槽910再移除。CNJ/WHM/SCH/AST/SGE及BRD按源提示自己
  或按affected顺序提示他人，其余职业提示求净化。普通Destroy经Lose+清槽
  后退出，冷Pet回补但热Pet Gain/Lose/Destroy回调过滤，因此冷Pet遗留提示
  保留源行为。同ID新代不替换旧引用。不新增Esuna/Paean执行或治疗策略。

Bozja矩形raw Check与导航/显示转换分离：raw使用double SinCos缩float、
原始点积及含边界比较。model-08先复现HeavensWrathAOE旋转-0.7012279、
forward60、side4.9999995的差异，再修公共BozjaCast；不修改全地城几何。
公共免疫输入只作小范围提取并覆盖原Wind/Bracing与所有Bozja消费者。

本批备份`.tmp/bozja-20261125-backup/queen/`，登记前另备份queen-registration-final。
最终DLL SHA256 `4e5d47cf634118d954f8b65ba286cc32f90b6d3dc7bd423d8ac7ddadc54a00e5`。
日志前缀`.tmp/halatali-verify/native-delubrum-queen-20261125-`：
model09为314相关模型PASS/0FAIL，model10为最终8个Queen定向PASS；
build01为0错4既有警告；replay02为13个Queen专项加3基础汇总，
replay03为260条南方/札杜诺/女王古殿联合PASS；catalog02无筛选1689行首PASS
（1680测试+9汇总）、296独立入口编译，全部明确exit0。catalog01也为1689/0，
但恢复时原进程句柄已失效、无法取退出码，因此使用catalog02作为最终证据，
另存catalog02.exit=0。实际DLL覆盖R20真实solver无fallback、精确ownership、
八准备格注册、实体格采样拒绝、冷免疫无位移线但Goal保留、热Lose恢复、Map、
炸弹炮台Effect、Doom/global damage、reverse Seek和owner释放。

保留失败历史：model01只读ActorStatus夹具编译错，model03漏Synchronize update参数；
model05严格复现准备格Generation冲突和冷KB漏免疫两生产缺陷。model06修复时
误用通用Status引发Chess/MeansEnds重复回补，改为只回补ImmuneStatus。
model08斜矩形精确Check反例失败后修double SinCos路径；model09覆盖公共消费者。
replay01错误预期中心八实体格应有导航；真实源Convert显示填充超过65%而拒绝，
新增model10源组件八格及零primitive严格对照，replay02/03按同合同通过，
未放宽容差或改生产算法。两轮独立复核确认上述修复，无剩余确定可达差异。
SDK10.0.303隔离；根SDK11与游戏实时采样/像素/实际执行未验，未部署。
正式登记783 verified /430 pending /1 review，另45 excluded；DRN5仍pending。

本批源SHA256：

| 文件 | SHA256 |
|---|---|
| AboveBoard.cs | 83dbb6ac696efa372b84d8138e3464c44edc0231708a2be7f024848094d3d1cb |
| ArenaChange.cs | 8e0a3063a55cba9e81d22ac76047a7ea1b391bffd4c47c07ad3a7b09d795f63e |
| Chess.cs | f61145b50304300fb3cec01f5e6210a0f15ecc38214c0b20e8da27ae95425797 |
| DRN6.cs | e12ba8f32920b13e7c36f3042ab671d15bb0a31aaf67203377310d7e1415cbac |
| DRN6Enums.cs | 3520119640aa0971d39a9f55773867ce6fbe4cbe2eb6db821a4055907ecc0b69 |
| DRN6States.cs | e44f44de57537aafe705c8c34396eb3bcae121e47e2dea7c3f5985bd40e63e33 |
| HeavensWrath.cs | 0c21baae022ab6dd3b4090e69872c435a45792463478d286543b6cc4fa464bd7 |
| MeansEnds.cs | d8fd9e75a98ab3f26a628f36abdae946467a5cab4e4b3861d27afb4a885871a2 |
| TurrretsTour.cs | 5643c8e8e1cf87ed4ce8b8288dcd39f121416ea474326d42980009586a1ca62d |
