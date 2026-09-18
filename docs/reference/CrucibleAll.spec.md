# 斗兽奇弈全部机制迁移规格

源码基准：本地 BossmodRebornWork f6914f3e8（7.5.6.10，2026-09-17）。
目标：MasterPlayer；普通第一盘 B01–06、第二盘 B07–13、第三盘 B14–21，高段第一盘 B22–31、第二盘 B32–45。区域号已对照本地 BMR 开发记录与脚本框架核实。

| 脚本目录 | 区域号 | 组别 |
|---|---|---|
| Scripts/Crucible/Low1 | 1339 / 0x53B | B01–06 |
| Scripts/Crucible/Low2 | 1340 / 0x53C | B07–13 |
| Scripts/Crucible/Low3 | 1341 / 0x53D | B14–21 |
| Scripts/Crucible/High1 | 1342 / 0x53E | B22–31 |
| Scripts/Crucible/High2 | 1343 / 0x53F | B32–45 |

## 实现约定

通用走位优先级补齐（2026-09-18）：普通跟随和 GoalZones 是可放弃的输出偏好，必须服从危险与机制 GoalPoints。对齐 BMR：玩家读条或脚下危险有效起点不足 1 秒时，暂停普通偏好；以原目标覆盖范围计算安全占比，低于 25% 时整项放弃，禁止先裁剪再将残余狭条当完整目标。普通偏好不得覆盖 PR 的硬安全余量，危险结束余量内仍不得回位。精确机制点、危险时序和场地边界保留原语义；几何及分数在主线程冻结，不给后台任务捕获可变演员。

真实 PR 解算回归还确认：空跟随几何的 GoalWindow 若仍激活，目标启发式会把位于 (-13,0) 的安全玩家拉到约 (-6.25,0.25)。目标被完全覆盖或只剩不足 25% 安全范围时，现同时停用 GoalWindow，危险取消/结束后复用原句柄恢复；启停时单次递增版本，防止接受旧追击结果。自定义不可见网格使用其外露边缘计算余量；未知且无轮廓的几何保守放弃包围盒内的普通目标，不改变实际危险或机制点。

机制组件迁入 Mechanics/Crucible/Source，保留 AID/OID/NameID、实录注释、状态机、形状尺寸、时序与判定顺序。适配层只提供演员快照、事件分发和几何/提示语义；不加载 BMR DLL，不移植 BMR 导航引擎。路径规划、移动及绘制使用 PR Reborn 与脚本门面。

- 读条使用剩余秒数 +0.3s NPC 完成补正；锁朝向从 CastRotation 获取，失败跳过而不是用实体朝向。位置取原组件指定的 LocXZ 或演员快照。
- 效果按 (GlobalSequence, SourceId, ActionId) 去重，同一包多目标不会重复推进。取消、打断、演员死亡/销毁、战斗结束、卸载清理预测及注册句柄。
- 圆、环、锥、矩形、十字转成 PR 几何；锥半角乘 2、矩形半宽乘 2，后伸矩形保持偏移。复杂落点/逼近约束保留符号距离判断，作为 PR 区域输入，绝不另建寻路器。
- 持续伤害物件为持续危险；真正碰撞/触发陷阱才为障碍。非危险预览只绘制。引导点更换先移除旧点，精确放毒使用 R0.2 + Center。
- 场地根据 CrucibleArena / CrucibleLaudaFloor 固定，普通第三盘 0/1 电网索引反转、劳妲非矩形地板保留。物件休眠态 7 移除。
- 全部接管 AID 抑制通用躲避；静态形状表同步真实读条形状。
- 共 45 组、237 个组件绑定，425 个接管 AID。单一读条形状从原组件读取；纯视觉、点名、未知半径、无读条效果和不可躲避全屏技能不生成臆测的通用危险圈。
- 多目标效果在本帧合并、按 TargetIndex 选主目标，向组件保留全部目标；同序号只推进一次。显式打断即使发生在最后 0.5 秒内也撤销预测。战斗结束后暂停，下一次开战恢复，避免活体缓存重新激活旧组。
- BMR 的波次风险分级只用于显示。地面隆起、蛛网、树精五连环、牛魔、曼提克、魔界花五连锥、冰波、石像鬼钢铁月环、劳妲剑线等全部按各自判定时间注册，保留源代码提前 0.25–0.5 秒的准备余量。
- 所有事件走 MasterPlayerScript owner 订阅。运行状态属于脚本实例，停止后清区域、预览、连线及所持边界，不接管 GreenMove。

## 组件清单（包括隐式电网）

| 组 | NameID | 迁移组件 |
|---|---|---|
| 01 | 14531, 14532 | CrucibleBoundary, XBMB01AOE, XBMB01Hint0 |
| 02 | 14533 | CrucibleBoundary, XBMB02AOE |
| 03 | 14535 | CrucibleBoundary, XBMB03AOE, CrucibleVoidFlare, CrucibleVoidFlareHint, XBMB03Hint0 |
| 04 | 14536, 14537 | CrucibleBoundary, XBMB04AOE, CrucibleGroundUpliftAOEs, CrucibleWebAOEs, XBMB04Hint0 |
| 05 | 14538, 14539, 14748 | CrucibleBoundary, XBMB05AOE, CrucibleLava, CrucibleFireSpiritWard, CrucibleFireball |
| 06 | 14541, 14542, 14543 | CrucibleBoundary, XBMB06AOE, XBMB06Hint0 |
| 07 | 14545 | CrucibleBoundary, XBMB07AOE, CrucibleManticoreHalfArena, CrucibleManticoreCharges |
| 08 | 14546, 14548 | XBMB08AOE, XBMB08Hint0 |
| 09 | 14549 | CrucibleBoundary, XBMB09AOE, CrucibleLiquidHell, CrucibleTyphoon, CrucibleTornadoes, XBMB09Hint0 |
| 10 | 14552 | CrucibleBoundary, XBMB10AOE, CrucibleZombies, CrucibleNecromist, CrucibleNecromancerHint |
| 11 | 14555, 14556 | CrucibleBoundary, XBMB11AOE, CrucibleMinotaurSequence, CrucibleMinotaurRotation, CrucibleMinotaurEnrageHint |
| 12 | 14557, 14558, 14559 | XBMB12AOE, XBMB12Hint0 |
| 13 | 14561, 14562 | CrucibleBoundary, XBMB13AOE, CrucibleSandpits, CrucibleGoblinBombs, CrucibleSandPillars, CrucibleWormQuakeHint, XBMB13Hint0 |
| 14 | 14564, 14567 | CrucibleBoundary, XBMB14AOE, CrucibleRangerKnockback |
| 15 | 14569, 14571 | CrucibleBoundary, XBMB15AOE, CrucibleYmirThunderHint, CrucibleYmirRocks, CrucibleYmirTsunami, XBMB15Hint0, XBMB15Hint1 |
| 16 | 14572, 14574 | CrucibleBoundary, XBMB16AOE, CrucibleZuCrossHint, CrucibleZuFrenzyHint, CrucibleZuRear, XBMB16Hint0 |
| 17 | 14577 | CrucibleBoundary, XBMB17AOE, CrucibleCatoblepasEyes, CrucibleCatoblepasGaze, XBMB17Hint0 |
| 18 | 14581, 14580 | CrucibleBoundary, XBMB18AOE, CrucibleLahamuKnockback, CrucibleLahamuGolems, CrucibleLahamuSand, CrucibleLahamuEarthshakerHint, XBMB18Hint0, XBMB18Hint1 |
| 19 | 14583, 14586, 14585 | CrucibleBoundary, XBMB19AOE, CrucibleSirenSong, XBMB19Hint0 |
| 20 | 14587, 14591, 14590, 14589 | XBMB20AOE |
| 21 | 14592, 14595 | CrucibleBoundary, XBMB21AOE, CrucibleGatlerTorrent, CrucibleGatlerExplosions, CrucibleGatlerTraps, XBMB21Hint0 |
| 22 | 14596 | CrucibleBoundary, XBMB22AOE, CrucibleOwlTether, CrucibleOwlGust, CrucibleOwlGustPriority, CrucibleOwlGustHint, CrucibleOwlChapterHint, CrucibleOwlChapters, XBMB22Hint0 |
| 23 | 14599, 14601, 14602, 14600 | CrucibleBoundary, XBMB23AOE, CrucibleMorbolAdds, CrucibleMorbolBreath, CrucibleMorbolPools, CrucibleMorbolKnockback, XBMB23Hint0, XBMB23Hint1, XBMB23Hint2, XBMB23Hint3 |
| 24 | 14603, 14605 | CrucibleBoundary, XBMB24AOE, CrucibleMorbolTether, CrucibleRottenStench, CrucibleFlowerTrapHint, XBMB24Hint0 |
| 25 | 14606, 14607 | CrucibleBoundary, XBMB25AOE, CrucibleIcePools |
| 26 | 14608 | CrucibleBoundary, XBMB26AOE, CrucibleGargoyleInOut, CrucibleGargoyleCharge, CrucibleMiasma, CrucibleGargoyleKnockback, CrucibleGargoyleSlashHint, CrucibleGargoyleRingHint, CrucibleGargoyleKnockbackHint |
| 27 | 14612, 14616, 14613, 14610, 14614 | XBMB27AOE, XBMB27Hint0, XBMB27Hint1, XBMB27Hint2 |
| 28 | 14617 | CrucibleBoundary, XBMB28AOE, CrucibleGolemJump, CrucibleRockRain, CrucibleRockWalls, CrucibleGolemKnockback, CrucibleGolemKnockbackHint, CrucibleGolemSelfDestructHint |
| 29 | 14618, 14620, 14621, 14622, 14619 | CrucibleBoundary, XBMB29AOE, CrucibleTreantBody, CrucibleGroveAdds |
| 30 | 14624, 14623, 14625, 14626, 14627 | CrucibleBoundary, XBMB30AOE, CrucibleMovingBombs, CrucibleBombPools, CrucibleBombMeltdown, CrucibleBombEnrageHint, XBMB30Hint0 |
| 31 | 14628, 14629 | CrucibleBoundary, XBMB31AOE, CrucibleDragonWaves, CrucibleDragonPoison, CrucibleDragonBaits, CrucibleDragonClouds, CrucibleDragonLanding, CrucibleDragonBreathHint, CrucibleDragonLandingHint, XBMB31Hint0 |
| 32 | 14631, 14632 | CrucibleBoundary, XBMB32AOE, XBMB32Hint0 |
| 33 | 14633, 14634 | CrucibleBoundary, XBMB33AOE, CrucibleMindflayerAdds, CrucibleFungalBeds, CrucibleMindflayerWater, CrucibleMindflayerThunder, XBMB33Hint0, XBMB33Hint1 |
| 34 | 14641, 14640, 14639, 14638 | XBMB34AOE, XBMB34Hint0, XBMB34Hint1, XBMB34Hint2 |
| 35 | 14642, 14643, 14644, 14646, 14645, 14647, 14648, 14649 | CrucibleBoundary, XBMB35AOE, CrucibleAtomosShockwave, CrucibleJellyWater, CrucibleJellyFire, CrucibleJellyThunder, XBMB35Hint0, XBMB35Hint1 |
| 36 | 14651, 14653, 14656 | XBMB36AOE, XBMB36Hint0, XBMB36Hint1, XBMB36Hint2, XBMB36Hint3 |
| 37 | 14657, 14659 | CrucibleBoundary, XBMB37AOE, CrucibleDurgaEdgeKnockback, CrucibleDurgaRayHint, CrucibleDurgaAdds |
| 38 | 14660, 14661, 14662 | CrucibleBoundary, XBMB38AOE, CrucibleMedusaRay, XBMB38Hint0 |
| 39 | 14663, 14664 | CrucibleBoundary, CrucibleChimeraTether, CrucibleChimeraExpandingIce, XBMB39AOE |
| 40 | 14665 | CrucibleBoundary, XBMB40AOE, CrucibleSphinxRiddles, CrucibleSphinxNumbers, CrucibleSphinxRaidwide |
| 41 | 14670, 14671, 14673, 14672 | CrucibleBoundary, XBMB41AOE, CrucibleGiantExplosionHint, CrucibleGiantSlimes |
| 42 | 14675, 14676, 14677, 14683, 14680, 14679, 14678, 14681 | XBMB42AOE, XBMB42Hint0, XBMB42Hint1, XBMB42Hint2 |
| 43 | 14684, 14685, 14686 | XBMB43AOE |
| 44 | 14688, 14691, 14692 | CrucibleBoundary, XBMB44AOE, CrucibleEyeAttract, CrucibleEyeRoulette, CrucibleEyeGazes, CrucibleEyeBrands, CrucibleEyeRaidwide |
| 45 | 14693, 14699 | CrucibleBoundary, XBMB45AOE, CrucibleLaudaBlades, CrucibleLaudaTorrent, CrucibleLaudaThunder, CrucibleLaudaBaits, CrucibleLaudaFireKnife, CrucibleLaudaShockwave, CrucibleLaudaLaterHint, CrucibleLaudaEmpoweredBlades, CrucibleLaudaAddRaidwide |

## 语义卡

每个同名 Source 文件保留原组件的触发 OnCastStarted/OnEventCast/OnStatusGain/OnTethered/OnMapEffect/物件动画、AOEShape 参数、FutureTime/CastFinishAt 时序、OnCastFinished/OnActorDeath/OnActorDestroyed 及超时路径；Source/CrucibleEnums.cs 为十进制 AID/状态常量清单，Source/CrucibleArena.cs 为完整场地参数，Source/CrucibleSpells.cs 为读条表。组件清单对应 Catalog 中逐个工厂，不允许以通用 AOE 代替特殊状态机。

| 类型／关键组件 | 触发与时序 | PR 表达与撤销 |
|---|---|---|
| 普通读条／ReplayValidatedCastAOEs | 读条结束 +0.3s；锁朝向、原地或包内落点 | 原生圆、环、扇、矩形、十字；取消、命中、死亡和超时撤销 |
| 隆起／蛛网／树精波次 | 同时出现的读条携带不同结束时间 | 每段单独危险窗，禁止把后续波次折成永久禁区 |
| 曼提克冲锋 | 48128 / 0xBC00 预览结束 +7.1s，48130 / 0xBC02 实际读条校正 | 源点到落点，全宽 8；重复包不再追加路径，打断／销毁／超时撤销 |
| 牛魔旋转 | 图标 167/168（0xA7/0xA8）；48210/48212 开始，间隔 1.62s | R40、全角 60°；图标缺失不猜方向，保留当前真实读条 |
| 地面常驻伤害 | 地面物件与状态观察；怨毒龙毒池生成后 2s，第四池立即 | 持续危险；休眠／销毁删除。岩墙、瘴气接触雷等才为硬障碍 |
| 怨毒龙放毒 | 连线 17 / 0x11；48809/48810；毒池 2012932 / 0x1EB704 | 半径 18.3、半宽 7、深度 7 的矩形四点；匹配当前点／快照 1 米内的池推进，每批一次；R0.2 + Center，先撤旧点 |
| 怨毒龙空降 | 48816 外段、48828 中心 R6，外段释放后允许中心进入解算 | 外段等待时中心只预览；中断与死亡释放门控 |
| 拉哈穆／劳妲强制位移 | 击退、状态、方向告诉的原始状态机 | 位移后攻击在落点校验，不能当作当前位置的普通禁区 |
| 奇美拉扩张冰 | 2015490 / 0x1EC102；动画 0x00100020，半径从 6 增至 18 | 保留物件年龄、重复动画不重置；状态 4/7 清除；重载未知年龄按最大圈 |
| 斯芬克斯 | 状态规则匹配数字物件；动物身份追踪换位 | 只把正确数字区域标绿；记忆题引导到真实交互对象，文字提示交互 |
| 劳妲场地／剑线 | 20×40 主台 + 六个 5×5 凸台；时间轴 4562 +15s、4576 +8s | 地板用无接缝的单个多边形，内缩 0.5；剑线宽 5、长 50，重复旧告诉不推迟预警 |

圆外就位约束使用精确不可变几何，避免半径 0.6 米的劳妲站位被 0.5 米网格封死；杜尔伽放置角落及击退前内侧站位使用 R0.2 的精确目标。其他复杂约束在主线程采样为不可变区域（网格 0.5 米、边界角点保守取样），求解线程不引用可变演员／组件。其边缘有网格近似，待游戏内验证；劳妲地板和通常 AOE 使用 PR 原生精确几何。常规危险几何继续应用用户配置的 DodgeDangerMargin，源组件的额外余量保留。

## 明确的差异与待实测项

- BMR 的 ACR 动作队列（疾跑）、强制选敌、交互、强制面向/停手不跨插件直接执行：保留走位约束/目标引导及文字提示，由用户/PR ACR 处理动作。斯芬克斯记忆题追踪答案与位置，引导到交互物件后提示交互。
- 目标优先级、反伤、驱散、禁止 DOT 与停手输出节流提示；不会直接改 PR ACR。猫头鹰普通击退只有提示；其他组件提供的落点约束继续交给 PR。
- PR DrawManager 无世界文字接口，动物／数字标签改为方位与距离文字提示；正确数字区域仍绘制。复杂约束区域服务于解算，不能用其离散边缘代替游戏提示图形。
- 奇美拉旧 BMR 导航器的“继续贴身输出多久”优化移除；PR 依全部危险时间窗计算撤离。
- BMR 标记未验证的尺寸仍待实测，特别是沙漠保守边界、部分月环与眼球品牌角度；不声称已经游戏内验收。
- 事件型连线/图标若刷新前已发出，无法凭对象表完整恢复；能够恢复的读条、状态和地面物件在启动时扫描。
- 回放系统本身不录完整对象快照，回放中的对象表仍来自当前游戏，不能代替游戏内全机制测试。

## 验收

2026-09-17 实测反馈跟进：雷达场地基准模式的背景、外框、方位和裁剪随场地形状；圆形/矩形保留 3 米显示留白以呈现电网（不修改解算边界），异形地板保留凹口。网格安全区只描外露边缘，不画每行内部横线。

2026-09-17 22:48:46 高二 ARR 已确认：夺心魔水 231.318s 两个不同 helper 的 49200 同时命中玩家，玩家距新宠 5.758m；激光 275.503s 起读，277.416s 玩家吃 49204 与易伤；杜尔伽约 1240.8s 从角落回跑，早于视觉效果 1241.429s、落点锁定 1242.327s 与真正击退 1244.304s，最终落地超界。该录像十字 50939/49207 确实躲过。72 条读条配对的补偿为 0.188–0.325s，现有 +0.3s 合理。分析和匿名 fixture 位于本地 `F:/project/卫月插件/analysis/masterplayer-high2-20260917-224846/`。

本地机制/脚本/自动躲避开关均启用，BMR 预设库没有漏迁的斗兽专用预设。旧 Follow 仅看 HasControl：到点释放控制后，机制仍存在也会直接 MoveTo 贴近目标。自动躲避开启时现改用 Reborn 动态目标区，机制目标优先，让跟随与危险窗统一解算；目标移动不反复注册，卸载只删除自己的目标。ARR 本身不能判定输入来自自动或手动，但该路径已由代码及回归确认。

真实 ARR 换宠回归进一步复现：227.074s 求解的永久攻击目标可把已经分散的角色拉回新宠物水圈，目标预计 227.557s 到达、伤害 231.318s 才发生。PR 允许路径当前安全的高优先级目标暂时进入未来危险区；因此低优先级攻击偏好及自动跟随必须排除仍待判定的危险几何，不能只依赖 HasControl 或路径求解成功。明确的机制 GoalPoints 保留原有时序与就位语义。这证明了不必要回位的代码路径，不单独证明录像当次输入来源。

共享输出区分有限伤害窗与持续就位约束，数字格/击退/强制位移保持到组件释放；连续波次仍使用明确结束时间。读条朝向优先用 PR 捕获的 Raw.PacketFacing，避免事件处理时当前读条已结束或改变而丢弃预测；非法/缺失包兼容匹配的 native CastRotation，不使用 SourceRotation。四激光使用包内落点，不能因 Target=self 改用施法者位置；水换宠需要覆盖新旧宠重叠存在和旧宠销毁后的当前宠物。

B33 危险覆盖但未接管移动时每 4 秒的解算诊断继续保留。录像没有证明上次替换的 DLL 已被重载：最新已知初始化日志仍为 22:27:57。刷新脚本不等于重载主 DLL；此次修复需要重新加载 MasterPlayer 后再实战验证。

在实际 PR 本体 DLL 下 Release 编译；旧回归及新增状态机/几何/时间/清理回归全部通过。按五盘放入运行 Scripts/Crucible 子目录。备份放到扫描目录之外，记录迁移来源与部署哈希，不推送远端。

旧高一源码移到 tests/Legacy，仅供原回归测试；运行目录只保留新的五个入口。主程序集包含共享机制，不能只复制入口到旧 DLL 上。BMR 的 BSD 3-Clause 许可保留在 ThirdPartyNotices/BossModReborn.LICENSE，并随部署分发。

2026-09-17 初次迁移验收：Release 主程序集 0 警告、0 错误；233 项回归通过（包括 PR 真实解算器验证怨毒龙精确目标和劳妲凸台通路）；五个外部入口分别以 Roslyn 独立编译通过。未进行游戏内实战测试。

2026-09-17 最新 ARR 修复验收：264 项完整回归通过，其中包含真实四激光坐标、换宠水分散全时序、杜尔伽两阶段与40米落点、劳妲精确安全孔、危险结束前跟随不回位及结束后恢复。Release 主程序集 0 警告、0 错误；五盘外部入口 Roslyn 独立编译全部通过。测试工程存在3个仅测试链接源码的 CS0649 未赋值字段警告，运行均通过；未在游戏内实测修复件。启动日志新增 loaded-module MVID 标识，便于确认主DLL重载。

2026-09-17 场地罗盘适配：斗兽入口从当前 CrucibleOutput 提供与导航边界同属一个组件的 DisplayBounds 和固定场心；停止、关边界或被其他边界替换时不沿用旧轮廓。背景、裁剪、外框和罗盘使用每个场地的显示几何；电网场地显示原定义的3米电网，实墙不统一加3米，劳妲显示20x40主台加六个5x5凸台与真实凹口，普通第三盘尾王保持30x50长方形。按镜头旋转后的轮廓包围范围等比适配视口，方向文字放轮廓外。修复屏幕坐标面积相消与退化三角形造成的异形裁剪错误；导航安全边界和余量不因此改变。沙漠场地仍使用BMR已标注的保守边界，未臆造未经实测的地形。

场地适配验收：268项完整回归通过（圆/方/长方/异形、四个镜头角度、多个窗口位置、缩放及边界清理）；五盘脚本独立编译通过，Release 0警告0错误。已备份并替换本地测试DLL，SHA-256：3A506A3FBE20A70F2422C13BE8DEBBC8B0BB6EBC3057E70C286E1B210E18AAD7。游戏内需重载MasterPlayer后确认效果。

2026-09-18 通用走位优先级验收：新增7项回归在修改前均失败，修复后275项完整回归通过；真实PR解算确认狭窄攻击残区不再吸引安全位置的玩家，危险取消后恢复跟随，急迫危险仍正常触发逃离。五盘外部脚本独立编译通过，Release主程序集0警告0错误；测试链接源码仍有3个既有CS0649警告。已备份替换本地MasterPlayer插件4个文件并逐文件核对SHA-256；主DLL为2A125B25696165E73315CAB4E87940245FD685FC62E73562111ADF5DCF4B8E9F，MVID为c236e589-efb0-414f-9e5b-3e37b72d2506。本次未修改/替换PR本体；主插件须重新加载，尚未游戏内实战验收。部署记录：F:/project/卫月插件/analysis/masterplayer-high2-20260917-224846/optional-goal-safety-deployment.json。


## 2026-09-18 高一与界面复核

对照本地 BMR 的 XBMB22–31 States，包含隐式场地组件共 70 个，缺失/多余均为 0；数值归一后逐文件核对 High1Mechanics、Sequences、Branches、DragonBaits、DragonClouds、GroveAdds、TreantAOEs、Tethers、Arena。现有猫头鹰提示、停手、强制面向、动作队列等降级仍适用，组件数量一致不等于全机制实战验收。

本次行为修复：

- 50758 魔界花10m / 50649 巨像17m击退：正常 CastFinished 会早于效果事件，保留持续落点约束直到匹配效果；打断、死亡及 activation+2s 超时仍清理。
- 防击退按 BMR 状态ID识别：160、1209、3054、1722、2663、2345；ExpireAt 必须严格大于 activation，补齐亲疏自行等状态。
- 48719/48720/48721 石像鬼钢铁接月环：实际几何继续 R13 / R6–30、判定间隔2s。原来两段同时注册，加上脚本几何余量、PR 0.5m硬余量、提前0.5s和延后0.3s后，真实PR解算返回 NoTemporalPath。现在保留两段显示预测，但按阶段提交走位：钢铁前选 R14 待机点和避瘴气的径向通路，外围限制 R15.5 为网格/余量留空；钢铁命中（或原有+0.25s超时）后只保留月环，目标为最近方向R5内圈小点。月环注册起点为真实activation+0.35s，PR默认提前0.5s后仍提前0.15s判定；不是把实际伤害时间改晚。此局部补偿依赖当前实际PR默认SolveOptions，不能套到其他机制或全局延迟。
- 上述转场以当前用户 DodgeDangerMargin=0.5 和真实 PR SolveOptions 验证，覆盖四个方向、命中事件收到/缺失两种路径、第一击前不进内圈、第二击前到达、瘴气避让和打断清理。更大用户余量可能使两秒转场不可行，不应为了走位强行穿越危险。
- 魔界花五连锥验证五段时窗进入PR，旋转视觉不推进伤害步骤，多目标重复效果只推进一次，死亡清理。怨毒龙毒池/四连放毒等既有回归保留。

UI 与罗盘：PR主题统一主面板/回放/可视化编辑器/雷达菜单；六个侧栏入口，RGBA保持兼容，颜色和百分比滑条分开编辑。RadarScale 改为悬浮窗大小，旋转后的实际场地完整适配窗口，不再在固定方框里放大后被裁掉；方向文字边距随字号变化，回放使用相同边距规则。

验证边界：当前游戏未运行，未做新DLL的游戏内视觉或实战验收；执行源码与真实PR解算回归、Release编译、外部五盘脚本独立编译，再备份部署本地测试件。没有发布远端。

本轮最终验收：292项回归、5盘独立编译全部通过；Release 0警告0错误。已备份替换4文件并核对哈希。主DLL SHA-256 `96BE9EE3C96D437B2A9D8FEA6B06DF027A31FD6F65C51A314B09ABB0AF3C1816`；MVID `a519c637-0a24-48b5-96e8-0f9a95190e7b`；记录 `F:/project/卫月插件/analysis/masterplayer-ui-high1-20260918/deployment.json`。

## 2026-09-18 最新高二 ARR：躲避延迟与宿主寻路

`斗兽奇弈高段 第二盘 2026.09.18 23.02.30.dat` 只有 99.392 秒，是 B32 佛劳洛斯及雷元精。49.257s 三个 helper 开始 49194 短读条，51.218s 同时命中玩家；录像在 50.4–51.48s 显示玩家基本停留原地。49194 原始尺寸长100、全宽2，与 BMR 一致；不修改机制尺寸、+0.3s NPC 补正或真实判定时间。49680 是普攻，不计为漏躲。22:58:15 的初始化日志证实当时运行的是上一轮 MasterPlayer MVID `a519c637-0a24-48b5-96e8-0f9a95190e7b`。

真实脚本输出和 PR 解算复现：旧实现 50.1–50.7s 返回 NoTemporalPath 并停止移动，普通时刻解算约 160–204ms。ARR 不记录逐帧解算/移动输入，离线复现说明代码缺陷与停顿相符，不能单独认定现场每一步输入来源。

本次同步修复 PR Reborn 宿主：

- 圆、月环和旋转矩形用解析相交光栅化；无目标时抵达满足全部余量的安全格即结束搜索。有限伤害窗的严格搜索不继续展开累计余量已为负的路线，交由原时间搜索处理；公开 Search 签名保留，直接逃离当前伤害区仍保留原行为。
- 正常空间/时间解算均失败、且玩家脚下有 1.5s 内将生效的伤害时，只允许一次紧急重试：开始提前量由默认0.5s降为0.25s，不减几何余量或结束余量，不推迟真实伤害，不通过全场覆盖伪造安全点。执行器使用该次结果的提前量并立即打断等待；这属于 PR 本地宿主的策略，不是 BMR 机制数值。
- 新快照等待重算时，旧路线经最新边界/障碍/伤害时间窗复核安全才继续。普通路线也检查异步计算耗时后的可行性；失去判定期限则撤销。未来危险下的小幅逃离不再被目标到达容差吞掉。
- 等待读条按剩余折线路径估算撤离时间；紧急路线不等读条。路径简化/平滑后重新检查格子、禁止回入、危险时窗及对角侧格，不通过则用原始路径。下发路线还检查异形场地凹口，不能仅凭端点都在场内放行。
- 所有斗兽组在脚下危险且未持有移动控制权时每秒记录一次解算状态、快照版本、结果年龄和异步计数，取代原仅 B33 每4秒诊断。

验证：311 项完整回归通过，其中新增18项覆盖本次故障、150ms下发延迟、全场无解、旧路径切换、异形凹口与几何等价；MasterPlayer Release 0警告0错误，PR Debug（当前实际宿主配置）0错误、13个既有警告；5盘外部入口独立编译通过。真实B32脚本复现中普通/紧急整次解算分别约31/27ms（单次离线采样，非全场景性能承诺）。已有跟随让位与各机制时序回归全部保留。

分析、日志、源码备份与部署记录：`F:/project/卫月插件/analysis/masterplayer-high2-20260918-230230/`。本次必须重载 PR 本体并确认 MasterPlayer 重新初始化，仅刷新脚本不会更新宿主寻路；尚未游戏内实战验收。

23:42 已备份并替换PR与MasterPlayer共8文件，SHA-256逐件一致；五盘入口与源码一致。PR DLL `347C13EFA1EFAD34961CF1FDD59E243FF36A835A6997AFA7249E3F5A250840E1`，MasterPlayer DLL `1AB2E7A9FACA43FDFEDA59D6459BA456522C91CA8C47F050CC8167C87CC0DF1E`；MasterPlayer新MVID `c6a1851b-4dc4-4559-b85f-7523d813abe5`。完整文件、前后哈希与备份路径见上述目录 `deployment.json`。

## 2026-09-19 夺心魔继续核对

新版23:42已加载，23:52日志记录B33四激光起读2.00s时脚下危险存在，0.94s时NoTemporalPath且位置不变。后来落盘的 `斗兽奇弈高段 第二盘 2026.09.18 23.58.25.dat` 长509.172s：四激光177.259s起读，179.295s命中49204，玩家一直在(121.4466,3.6469)；十字155.440s起读，156.908s停在(122.6673,6.9429)，159.431s命中49207。

B33对照BMR的GroupPhase及States包含8组件，普通49196、菌床49197、水49200、雷49206、激光49203/49204、十字49207/50939均有对应。49198/49208是全屏提示；水/雷自身点名伤害不能仅按命中次数认定漏躲。该回放水时玩家与宠物约9m分离，明确的漏躲为十字和四激光。

确认并修复两层缺陷：

1. PR普通解算失败时，原“1.5秒内才允许紧急重试”的门槛使四激光在仍可逃离时不动。取消该门槛，只要脚下仍有待判定危险且普通路线失败，就尝试既有0.25s提前余量；继续保留真实几何、空间余量和真实伤害时间。连续重算中保留已采用的紧急余量，避免1.5s时恢复0.5s并中途停步。这两处为上一轮修复策略的遗漏。
2. 移植层`CrucibleEngine.Cast`去重只看技能/结束时间/位置，会吞掉同位置读条的新朝向；现在同时比较朝向和目标ID。BMR预测组件本来支持同源同次读条修正，不能在适配层先丢弃。回归先本地扫描朝向0、再事件包70.6954°，修改前失败，修改后单个十字方向正确且重复包不增殖。

十字几何多角度与BMR距离函数一致，真实停点仍被识别为危险且离线可以继续逃离。录像没有插件当帧采纳的朝向与GreenMove路线，尚不能唯一认定十字实战受击由上述去重缺陷造成。诊断新增读条角度/落点及持有控制权但GreenMove已停的记录，保留此不确定性供下次实战对照。

新增4项专项、完整315项回归通过；四激光验证0.5m用户余量、50ms起步延迟与持续重算直至实际判定。PR Debug 0错误/13既有警告，MasterPlayer Release 0警告0错误，5盘独立编译通过。构建、源码备份、回放提取与部署详情见 `F:/project/卫月插件/analysis/masterplayer-mindflayer-20260919/`；必须更新并重载PR与MasterPlayer，未宣称游戏内验收。

00:15:39已备份替换8文件并复核SHA-256；开发插件自动重载PR，00:15:41确认MasterPlayer加载新MVID `89f16a0a-cb79-477d-bf6d-9e2cf7b82146`。部署哈希及备份见该目录deployment.json。


## 2026-09-19 高二全组与劳妲复核

联网核对中文维基及三鹿_36、丶行殇丶的攻略简介：尾王“魔斧之主 劳妲”，击退后要按下一招决定是否回位，全屏高伤使用减伤。维基当前无完整机制正文，视频仅核对简介，精确尺寸与时序仍取 CN 客户端、BMR 及 22:01:47/22:48:06 ARR，未据简介臆测参数。来源与逐组记录在 `F:/project/卫月插件/analysis/masterplayer-high2-lauda-20260919/`。

对照 BMR `XBMB32–45States`，包括 `GroupPhase` 隐式场地，共14组、73个组件，缺失/多余均为0。B34/B36/B42/B43继承普通状态基类，没有隐式电网；不虚构其场地尺寸。普通AOE尺寸表、作用组表及专用组件均已逐文件比较。B44由视线/烙印/轮盘专用组件处理，没有需要画成全场禁区的普通读条AOE。

修复与契约：

- 读条去重位置容差由1m改为0.01m，保留朝向/目标检查，允许事件包修正亚米级落点。普通AOE及劳妲剑线正常读条结束继续等伤害事件，取消、死亡或既有超时清理；实际实时适配层原本在完成后0.6s才派发结束，此项是事件顺序契约加固，不能单独认定为本次实战提前回位根因。
- 劳妲三剑由瞄准切至49464/50848固定线时清掉全波跟随预览与引导目标，不再依赖旧剑坐标逐个匹配；锁定后迟到连线不能重开旧波，新召唤/新瞄准正常恢复。
- 四波剑线4576重复不会把已有判定时间后移；每波仍完整注册准确伤害窗。洪流取消立即解除落点约束。
- 止步5555与飞刀5341乱序合并保留倒计时；无时长状态（MaxValue）不会日期溢出，未知到期时间不捏造击退时间；49485/49486使用效果事件携带朝向，避免依赖后续扫描的旧演员朝向。PR的SourceRotation仍是效果发布时对象表快照，不能称原始包锁定角度。
- 飞刀49468取消/落地前来源消失撤预测；49468实际落地后允许投放helper退场，地面刀49469接管位置与时序，命中/取消/地面刀死亡撤销。核爆R80是距离衰减范围，不生成全场必躲圈。
- 魔法强化双剑阶段允许从49477/49480真实读条恢复场边引导，避免中途重载漏掉spawn而失去目标；取消清理。全屏持续伤害仍提示减伤。
- B39冰圈投放来源死亡时撤未命中的预测，已落地痕迹继续衔接真实地面物件；B44烙印/视线正常结束不提前撤，取消/自己的效果/来源死亡清理，吸引取消清轨迹。
- 绘制增加两视图共用的击退轨迹、实际朝向预计落点和期望面向箭头；落点颜色校验场地及同期AOE，劳妲额外检查击退后紧接的半场/钢铁。非危险未来波次仅描边，临近波次按剩余时间填充；Outline标记保持空心，不再伪装成填充危险区。精确AOE预览接管显示，隐藏重叠的解算外扩副本但保留全部危险注册。两视图分别沿用用户颜色、不透明度和分组开关，owner卸载、命中与场景切换清理线/面预览。

实录回归：22:01:47 ARR 的1375005/1375095ms三线起读，1376974/1377065ms命中；以实际位置/锁定朝向及0.5m用户余量重建，加入50ms下发延迟并持续重算，验证在首次命中前离线撤出且路线不出异形场地。40m击退后实际南凸台落点重建，验证旧就位点释放，后续20m洪流前可回到合法落点区。另覆盖四波逐次命中、无时长状态、取消、死亡、晚到连线、晚启用、方向和绘制清理。

自动面向/停手/选怪/交互仍是原迁移降级：本轮补齐双视图可见箭头与落点，没有接管PR全局面向控制，也没有新增自动使用突进、减伤或自动交互。必须按箭头确认面向；轮盘停手/记忆题交互仍需操作者处理。离线解算与显示检查不等于游戏内全机制验收。


验证与部署：新增50项专项，完整365项回归通过；MasterPlayer Release 0警告0错误，5盘外部入口独立编译通过且与安装脚本逐件哈希一致。测试构建仅有原有3个CS0649警告。2026-09-19 01:16:53 已备份并替换本地MasterPlayer四文件，全部SHA-256与Release输出一致。DLL哈希 `42DABE5F524C35A652E5EE4D937DBBB53BB45E7214BE3EFB3C3BC319B4CB0AD2`，MVID `a77e577e-8462-44d3-901e-c6d4568d40c2`。PR本体DLL哈希保持上一轮值，未重新编译或替换。部署时游戏未运行，下次启动加载新DLL；本轮未进行游戏内实战验收。备份、逐件哈希和构建日志见本轮分析目录。
