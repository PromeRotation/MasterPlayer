# 狩猎原生脚本

入口为 `Presets/Hunts/Entries/SimpleHunts.cs`，共享生产实现位于 `Presets/Hunts/`，不依赖 BossMod 类型。按本机匹配CN数据的 `TerritoryIntendedUse=1` 发现48个野外区域；遇到支持的OID才建立实例。没有注册固定圆形场地，原局部地形继续生效。

## Aglaope / 阿格拉俄珀

来源`Shadowbringers/Hunt/RankS/Aglaope.cs`，本机表核实OID281E/Name8653、S级狩猎及Territory816。累计66/66狩猎源模块、482条读条规则；入口仍复用SimpleHunts，不注册固定场地。

八个激活组件使用七规则：16819环5–50、17074/18040圆6、16823前长42.4全宽6、16825只有“Interrupt! (Raidwide + Bleed)”提示，18056独立Raidwide预测与KB。870只有枚举；不增加自动打断、凝视、991强制行进执行或全局停手。

`HuntAglaopeState`保留16824的R16.2单槽、起读Loc/F和done锁存。新Start覆盖圈但不重置done，Finish/取消/来源销毁不清该自定义圈；真实16824完整效果先写世界pending，再置done。Update只查Raid0的pending991，不跟随观看角色。任意来源/序号/extra的991尚待确认时继续保留，不能用固定2.1秒或角色当前991替代。

`HuntAglaopePendingStatuses`按完整原生目标数组顺序和八槽读取Type14/15，保留所有待确认状态，含Actor/来源/GlobalSequence/原TargetIndex/ExtraLo及消费钟+3秒。bit45为FromTarget、bit47为AtSource；原目标不可解析时即使AtSource也不入队。重复/零序号包仍追加；严格Expiration<now才删除，每帧推进。ACK按接收者+sequence+原target index仅删首条任意状态；实际raw状态写入按原槽比较并删首条ID/来源匹配项，Lose不直接删pending。显式接收者销毁清其pending和观察基线；仅来源销毁不清其他接收者的记录，同ID新Actor不继承旧pending。

`HuntAglaopeKnockback`单独保留原施法/来源及判定、锁定Loc、AwayFromOrigin10、适用R30、不撞墙、不忽略免疫。六免疫复用三覆盖槽与严格到期>判定；同点及R30外不位移，多个源连续推进。首raw补正已投递packet但不重复Start，新CastInfo保float饱和NPC余量；同一raw不续期。未知读条替换、真实Finish/取消、显式或缺席销毁清KB。位移仅经HuntDrawing画线/落点及危险文字，不新增Danger/Goal/Obstacle或原生执行。

普通AOE和Sonata经`SimpleHuntEncounter → SimpleHuntScript → SafeZone`导航；同一HuntDrawing预览供两视图。新增`NativeActionEffectEvent.RawEffects`与目标完全同序、每目标八槽，采集器深复制并经生产Recorder/Reader回放；旧录像缺字段保持null且显示“缺少原始状态槽”，不合成待确认窗口。模型不依赖寻路内部类型，回放不提交控制。

验证：`native-aglaope-palace-model-final.log`687个集中模型通过，含10组Aglaope精确模型；`native-aglaope-palace-build-final.log`生产0错误/4既有警告；`native-aglaope-palace-catalog-final.log`实际DLL全目录exit0。`native-aglaope-palace-replay-final.log`强化验证原TargetIndex/第八槽、首123后991两次ACK、效果后超过2.1秒仍保圈、活动owner清理、移动helper不改锁点、R30外过滤、六免疫模型/实际160 Gain-Lose、旧JSON缺字段且实际991仍不替代pending、反向Seek，以及生产solver逃出Sonata。真实导航测试使用测试提供的R50空场边界；实机地形、两视图最终像素和宿主执行仍待验。

## Arch Aethereater、Sansheya与Urna Variabilis

来源分别为`Dawntrail/Hunt/RankS/ArchAethereater.cs`、`RankS/Sansheya.cs`、`RankA/UrnaVariabilis.cs`及实际激活组件。该历史批新增19组件/43读条规则，另消费Sansheya六种无读条命中，当时累计65/66模块、475条规则；Aglaope现已补齐真实PendingStatuses，见前节。

Arch Aethereater（4544）保留八组件：普通两组圆15/环6–40、长40宽10、八人最小且无人数上限的R6分摊、四类Raidwide及独立冷热要求。四种组合读条追加扇40/全180°和F+2.5f圆15或环6–40；全队列显示及参与导航，队列多于一项时首项黄色持久。白名单任意真实Finish消费全局首项，不按source匹配；Effect与预测到期不推进，短取消/施法中销毁只消费一次。分摊按目标匹配Finish删首项，未增加源没有的Shared预测。

Sansheya（43DD）保留七组件：圆15、环10–40、Raidwide、R6/min8/max无限分摊、动态宽6冲锋及Boiling。六种起手锁真实输入方向，不按技能名字额外旋转；两反向半圆的F/F+2.3f及可选F+4.5f圆15/环10–40完整保留。只显示前二；首项Risky，第二项仅在与首项共享同一静态shape引用时单向关闭Risky，消费首锥后圆仍参与导航。六种真实Effect全局FIFO，视觉Finish/销毁不清组合队列；同序号重复不推进，零序号以事件时间区分独立命中。冲锋动态跟随source/target并保留销毁旧引用，真实source Finish清首bait，Tankbuster预测取首bait时钟；已接受packet Start但未raw确认的施法者销毁也按真实结束消费。

冷热与Boiling使用`HuntStayMoveState`保留准备/判定时间、Stay/Move类型、准备标记及独立结束。Gain4141/4142/4140写真实packet expiry；准备Lose只清倒计时标记，Lose960/2540清要求，Gain resolving状态不创建要求，过期占槽不制造Lose。冷热提示窗口3秒，Boiling为1000秒；死亡查询清要求而保留标记。当前源及旧MP桥的SpecialMode只有诊断消费者，未新增自动持续移动或PR停手执行。

新实时采样与录像的`ReplayStatus.SlotIndex`保留过滤前原始状态槽，公共`HuntStatusInput.ObserveOrdered`按槽号及同槽Lose→Gain交付，仍复用raw/packet确认账本，包含同槽替换、低槽Lose/高槽Gain、相反顺序和同状态移槽。旧录像缺槽号时保持未知，不能恢复真实槽间顺序；该边界明确不作为原始live时序证据。已有owner在Actor状态更新时用旧队伍名单，随后更新名单；新owner的bootstrap使用新名单，与本仓库Feed直接应用Party Slot的顺序一致，不在迁移中新增稳定队伍排序策略。

Urna Variabilis（416F）保留四组件：独立单槽AOE、磁力、分摊和Raidwide。39109/11圆20、39110/12环8–60锁LocXZ/F+2.5；磁力单独保存F与正负/圆环类型；39106/07覆盖普通AOE/F，39113/14 Start不建圈。普通四动作Finish只清AOE，磁力四动作Finish只清磁力；Effect/预测到期不清。4071/4072按slot保存且不按expiry清，正电位移优先，双电时导航pull分支独立优先。位移相对动态Primary，距离10、拉min0、无范围筛选、不撞墙；源重写状态回调未调用base，免疫缓存实际从未填入，原生按此实际行为忽略免疫，不套通用免疫过滤。

带电且磁力活跃时仍显示普通AOE/文字，但导航改用专用约束：圆pull避30、push避10；环pull须18内、push须1内。无电或无合法槽恢复普通AOE导航。落点危险只检查当前独立AOE。`HuntUrnaVariabilisState`保留模型，`SimpleHuntEncounter.NavigationAreas/TargetDangers/Knockbacks → SimpleHuntScript/HuntDrawing → SafeZone/Draw/Hints`集中适配；专用导航不冒充显示，位移只画投影，无新增原生位移执行。

三模块覆盖真实结束、跨source FIFO、未来阶段、缺面向拒绝、同代更正/隐藏项、目标死亡复活/销毁新代、状态事件与raw确认、队伍变化、开关、切人/Seek和owner清理。集中436项模型（`native-final-hunts-model-slot-final.log`）、生产编译0错误/4项既有警告（`native-final-hunts-build-slot-final.log`）、实际DLL全目录536条PASS（`native-final-hunts-catalog-final.log`）及最终输入修正专项（`native-final-five-focused-final.log`）；均位于`.tmp/halatali-verify/`。显示和实际提供的导航离线已验，回放不执行PR或GreenMove，真实宿主控制和最终像素待实机。

## 已迁移来源

| 模块 | Boss OID | 已激活源规则 |
|---|---:|---|
| KerShroud | 0x3672 | 27725 R8；27724 长60全宽8 |
| CrystalIncarnation | 0x4571 | 39623 R5；39624 R40总45度 |
| Gajasura | 0x1ABF | 8188 R8.23总120度；8187 R6；8189单体预测和原提示 |
| Orghana | 0x1AB3 | 7997 长55.04全宽8；8000/8002全队预测 |
| OPoorestPauldia | 0x2820 | 16830 R12.025总120度；16831 R34.025总120度；16832长49.025全宽6；16833全队预测 |
| ArchEta | 0x35C0 | 27269长40全宽14；27270 R25总90度；27271 R17；27272全队预测；27273单体预测 |
| Hulder | 0x35DD | 27073 R30总120度；27075冲锋全宽8；27076环5–50；27077全队预测；27078单体预测 |
| ForgivenGossip | 0x2A03 | 17043 R5；18041普通凝视，真实默认范围10000 |
| BoneCrawler | 0x1AB5 | 7924 R14.2/90度、7920 R11.2/90度；7921/7922冲锋全宽8/12；7919 R10并击退20；7918 R18.2/90度 |
| Okina | 0x1AB1 | 7990长30全宽6；7991 R25/60度；7996环8–60；7995 R14并击退30；7993 R10并击退20；7994全队预测；7992纯Hint |
| Angada | 0x1AC0 | 8190 R9.4；8191 Butcher和8192 Rip同时R11.4/120度，Rip等实际效果结束；8193 R6 |
| Gamma | 0x1AB2 | 7914 R5；7913/7915全队预测及源Hint；个人SID960热病状态 |
| LunatenderQueen | 0x35DF | 27363/27369普通及反向凝视；27364/27371环6–40；27365/27370 R15；27366 R6；27367纯Hint |
| Baal | 0x2854 | 17956/17957 R12/180度；17423/17424建立两个R30/180度范围，后段反向并延迟2.3秒；17422/17421实际效果出队 |
| Huracan | 0x28B5 | 17496长80全宽10；17499全队预测；17495前长15全宽10、17497 R6、17498环10–20，各有延迟3.1秒的长80全宽10后段；18042效果出队 |
| Grassman | 0x283A | 17861 R5；17860单体预测；17862冲锋全宽6；17859/17863首次一击及后续五连击提示、持续伤害诊断 |
| Maliktender | 0x2874 | 18019 R8；18022/18024前长20全宽8；18020开始即锁存即死警告 |
| FanAil | 0x35C1 | 27373长30全宽11、固定落点随当前Boss朝向；27376 R6；27378 R8/90度；27379单体、27380全队预测 |
| Starcrier | 0x41FC | R8、环8–25、长40全宽8；真实SID1422/2936/3694/3909驱动方向错乱输入控制；37163全队预测 |
| Storsie | 0x35DE | 三种属性选择；R30/270度、环10–40、R22按观测时刻+10.4秒预告；实际结束消费 |
| MoussePrincess | 0x360B | SID2840/2841/2842/2958决定后续R40/120度方向；准备完成+2秒，实际Resolve结束清除 |
| Ophioneus | 0x35DC | 左右半圆；圈环与跳跃圈环预测，缺历史不猜形状，实际后续修正并清除 |
| Ruminator | 0x35BD | 记录圈环、顺序/逆序回放；26915/26916与四种无读条后续效果推进，动态队首 |
| Sphatika | 0x3670 | SID2835–2838按状态顺序记方向；两种stance决定正反侧；实际十种Cleave结束推进 |
| Gurangatch | 0x361B | 两次反向/八次左右旋转半圆；当前及下一次均参与规划，实际读条结束推进 |
| Burfurlur | 0x360A | Inhale记方向，五次Sneeze按真实读条结束推进；漏首段不推断，参考角按源取值 |
| Minerva | 0x3609 | Warning选择R6/环6–20并立即跟随目标；Warning结束锁点；实际落点仅补正，实际结束清除 |
| Armstrong | 0x35BE | 27470/27471选择±30度；十次四臂长50全宽7十字；第五效果后反转且延迟3.6秒 |
| Supay | 0x2839 | 17858 R8诱导及个人提示；17856才是真正凝视 |
| TheMudman | 0x281F | 环5–20、R40/60度、R8及R6目标诱导；GravityForce保留Interrupt提示 |
| Petalodus | 0x35FB | MarineMayhem纯Interrupt/Raidwide×3提示；R6分散、R13及R40/45度 |
| LilMurderer | 0x28B4 | R20分散、Tankbuster、R8地面雷；精确脚下雷预测结束+2.6秒R8 Slash，17489效果清除 |
| Nuckelavee | 0x288F | Torpedo开始锁目标并等实际主目标结算；R5分散；Gallop固定落点R8 Spite等待18037效果 |
| Rusalka | 0x2853 | R8、长12全宽4；R30范围吸引30至中心；固定R8 Flood在Finish+3.6秒预告，17369效果清除 |
| Sugaar | 0x2875 | R11、R13/R18总120度；三次±120度旋转，Finish+1.1f首击；动态Boss原点拉25，最小双方hitbox |
| Aegeiros | 0x3671 | 动态R10 Leafstorm及观测+9.6秒R40/180度Rimestorm；无读条R12/120度Canopy；R8/普通R12/120度 |
| Nariphon | 0x2890 | R12/120度、R6诱导；R6恰好8人分摊，起手冻结SID1435禁入名单 |
| Yilan | 0x35BF | Soundstorm Finish+12.1f预测动态R18；实际MiniLight补正/结束；R6及R10/90度；状态强制行进 |
| NarrowRift | 0x35DB | 圈环队列随Boss，任意主读条结束消费队首；27333+4秒预测动态长50全宽8光束，27516清；状态强制行进 |
| Tarchia | 0x2873 | 七条读条；构造期量化锚点R14仅预览，开战后规划；Groundstorm真实结束/取消后按NPC剩余时间+4预告R14，不按预测到期；任意本source非Groundstorm效果清除；四种真实误导状态联动PR |
| SallyTheSweeper | 0x4395 | 记录R10/环10–40/四臂40全宽10偏45度十字；正逆序执行锁点，效果消费；R18落点 |

| WhirlingOmen（OID43DB） | 0x43DB | 六种方向缓存、四种连续释放；R40总150度，Finish+1.4f/3.8f；38636–41实际Finish逐项消费；39878纯Raidwide |
| RraxYitya | 0x4232 | Wingblade R25总180度、缓存实际侧数，重复Finish+0.4f+i*2f；仅前二显示，效果出队；Leap长15全宽5 |
| Pkuucha | 0x4580 | R12圈/R40总180度扇，复合读条Finish+1.7f/5.7f；四种真实Finish消费；持续动态R14内导航但不绘制；单次全队预测及3x提示 |
| TheForecaster | 0x4397 | 四种预存三连、四种首匹配气候替换；Finish+0/3.1f/6.1f锁点释放；仅前二显示，八种效果推进；五种普通AOE |
| KirlirgerTheAbhorrent | 0x452A | 270度扇形组、R20及环10–40、长40全宽8；R8最少8人无人数上限分摊，无Shared预测 |
| Tyger | 0x288E | 三种R30/120度吐息、R9圈和环8–30；尾锥跨读条结束保留，按源分离轮廓与个人导航 |
| CatsEye | 0x4396 | 普通/反向凝视、纯Raidwide、两个独立R8无限分摊；凝视按组件原始第一项和动态未量化来源 |
| QueenHawk | 0x452B | 持续R12圈/环10–40等待任一对应Lose；SID2161–2164每段三秒强制行进，Bee终点警告；纯Raidwide/Tankbuster及直线 |
| TheRaintriller | 0x457F | 喊话缓存三段圈环/背后模式，六种起手/后续形成R12圈、环10–40、R40/270度FIFO；辅助来源真实Finish推进，R6最少2人无限分摊 |
| Ker | 0x35CF | 圈环/半圆组、锁点凝视、记忆及镜像提示；完整效果重建Flare与原槽状态历史；严格末一秒停手/禁读条/取消读条/暂停MP移动 |
| Gunitt | 0x2852 | 六读条；icon93取SourceId形成R10最少8人无限分摊，1138原槽历史mask、真实17361 Finish、组件级Shared交集 |
| AtticusThePrimogenitor | 0x416D | 六读条；30种喊话立即追加锁点R40/120度吐息，首段真实Start重排2.3d时钟，前二均Risky，六种效果FIFO；R6无限分摊及纯Haste/Raidwide |
| Heshuala | 0x416E | 十一读条；四档3–6次旋转，R50/90度前二均Risky，真实Finish按位置和角差推进2.7d时钟；半臂50全宽10冻结十字；R5圈及纯Raidwide |
| Ihnuxokiy | 0x4582 | 七读条；储存R12圈/环8–40，R40/180度Smog尾项+2.2f；全队列预览、仅队首导航，真实效果FIFO；长50全宽12、两种源单体预测及三秒强制行进 |
| Neyoozoteel | 0x4233 | 九读条；四种三连R40/120度预测，锁主怪原点、全队列Risky；八击半臂30全宽6旋转十字，真实Effect推进2.4f；R5无限分摊、普通R40/120度及纯Raidwide |
| Nechuciho | 0x452C | 六读条；三种四段R30/180度预测，前二显示、相反第二项仅预览，十二种Effect FIFO；普通R30半圆、纯Raidwide及Doom打断提示 |
| ForgivenPedantry | 0x298A | 八读条；Cheek两半圆及精确晋升、R15/120度Fever动态诱导；Native主目标Witch宽10动态矩形及真实Finish替换；默认10000范围凝视、普通圈/直线和纯Raidwide |
| Ixtab | 0x2838 | 十读条；双重咏唱圈环FIFO、F+5.1f第二击；R6 Abyss与R38.24/120度Crypt独立bait生命周期、R10无限分摊、SID1769重复Doom列表、普通AOE/纯Raidwide和Interrupt |
| Slippery（OID43DC） | 0x43DC | 八读条；38648长20全宽10与环10–40锁不同原点、全部Risky；39559/38650效果FIFO；五秒窗口三秒行进、两种R15 BodyPress、R6落圈及纯Raidwide/Interrupt |
| ForgivenRebellion | 0x28B6 | 十三读条；R40/45度三候选及七击Blizzard共用缓存；R28/180度Cyclone按SourceId图标配对量化锁点，六次实际Effect FIFO；R10、环5–40、半圆及纯Raidwide，独立两秒强制行进 |
| Sugriva | 0x35FC | 九读条；Twister R8/min8/max8分摊和独立单槽20码击退；动态R8/120度Butcher按来源Effect FIFO；Prey R6/F+1.1d、累加及恰第二投石Start清理，环14–30、R17、Rip和R6落圈及纯Raidwide |

对应源为 `MechanicRuntime/Upstream/Modules/<资料片>/Hunt/Rank*/<模块>.cs`。62模块共432条读条规则，另有实际无读条效果AID及不依赖27711事件的常驻Canopy。只计实际激活的组件。Orghana的`Raidwide x4`和Petalodus的`Raidwide x3`仅保留源Hint，不自造多次判定时钟；`Interrupt!`不等于已增加自动打断执行。TailDrive依据实际半角60度构造，而非过时的90度注释。Angada和Grassman技能注释中的击退未被源状态机实现，不增加臆测的击退组件；FanAil消失/再现AID也不增加源不存在的第二段。

## 生命周期和消费

首次激活要求敌人存活、可选中并战斗；激活后暂时不可选中不清会话，敌人脱战、死亡、移除或跨区清理。观看玩家死亡或BattleEnded不重置仍在战斗的敌人。机制开关独立控制对应定义及移动owner请求。

范围锁定读条落点和施法朝向，以事件UTC开始时间加实际读条长度和一次NPC补偿0.3秒。对象恢复的FinishAt已含补偿，不重复叠加。事件早于匹配Cast快照时等待首次观测，确认过本次Cast后观察无Cast才结束；取消/打断立即清除对应来源。已结束代保存时间水位，旧事件/旧快照不能替换较新一代或复活旧代。危险区在实际观察结束前保持，不以预测时刻加固定超时清除。

新快照及录像保存原始float `ReplayCast.TotalTime`，变化检测直接比较该字段，避免逐次采样通过开始/结束DateTime重建总时长产生假重启。旧录像无此字段时仅容许1微秒的重建误差，真实40ms修正仍重建。packet姿态补正修正原队列和时钟，不计作一次真实读条完成；对象输入相对上次原始观测有变化时保留Finished→Started语义，即使该值已由packet提前补正也不吞掉真实重启。每个真实代次拥有独立Order；同代packet保持Order，派生预测只按Order关联，不能按值相等的Input拖动历史队列。普通几何锁姿态，专用动态朝向按各自规则更新。

雷达和世界绘制共用原尺寸预览，导航使用脚本危险区门面应用一次余量；脚本停止仅清自身句柄、预览和移动请求，不改别人场地。通用躲避按Source/OID/规则/机制开关接管。BMR只在支持的已激活模块上休眠导航与绘制，未开战模块及其他野外机制保留原路径，不按整个territory压制。

全队预测排除死亡/缺失队员；单体仅在显式目标为施法者自身时，逐次读取敌人当前目标，否则保留显式目标。按旧MP桥契约持续发布目标/波次诊断和源Hint到脚本详情，没有新增PR治疗策略。

## 方向、击退及自定义时序

- 普通/反向凝视分别使用精确45度/135度禁向半角，联合求共同可行朝向，在命中前0.75秒通过PR面向门面续期。反向凝视要求面向眼睛，保留眼睛颜色反转及玩家背后270度弧；普通保留前方90度弧。窄共同解不额外扩大禁向；严格同XZ时不发布没有实际方向的控制。回放只保留规划和绘制。
- 冲锋起点按源倒数乘法量化，方向和长度取未量化起终点，不加hitbox；仅真实读条输入变化才重建。击退按OID/AID组件分别从玩家当前位置起算，同组件来源按开始顺序串联，重启来源移到尾部。六种免疫严格要求到期晚于activation，免疫不移除独立伤害圈。投影仅绘制连线/落点及源越界提示，不编造源没有的击退AI或技能请求。
- Angada：Butcher开始时同时发布普通和Rip扇形，activation相同；普通结束/取消只移除Butcher，Rip等待同实例8192效果。效果幂等，旧效果不清新一代，效果之后的首个对象观测不复活Rip。
- Baal：两个相反半圆使用不同activation；首段参与规划并显示危险色，次段仅预览。首段CastFinished后次段提升为危险，等待17422/17421效果结束。Huracan两段从创建起均参与规划，只有实际效果出队；WinterRain次段在首段未结算时随Boss更新并量化起点，首段出队后冻结。
- Grassman：首次读条完成结束单次提示；后续提示持续到四个17863后续效果，重复目标效果不多计。保留源始终发布的一条伤害诊断：首次施法前时间未知、机制结束后仍为上次activation，不补造五个伤害时钟。人员覆盖存活且有对象的0–23槽，排除24起NPC；生产采样保留PR本队槽并补普通联盟8–23，ARR取ReplayGroup，按源排除四人小组联盟。
- Maliktender：18020开始即锁存原警告，取消、状态缺失或状态移除不提前清除；脱战/死亡/停用重新建会话。SID1962本身不推断Haste开始。FanAil的Divebomb固定原落点，规划与双视图共用更新后的Boss朝向，27373结束/取消清除。
- Gamma：读条只预警，真实当前玩家SID960才触发PR ACR全停手、禁读条、取消当前施法和MP移动暂停；这是按用户要求补充的执行行为，源原来只给文字。状态事件按角色/来源隔离，接受来源0，旧来源移除不覆盖同帧新来源；大幅旧期限快照不提前确认刷新，100ms确认容差覆盖发布量化。死亡/缺对象清未确认事件，切人、到期、停用及停止撤销两种owner。移动hold不封锁手动输入或PR本体路线。
- Minerva目标丢失保留最后Actor位置，对象销毁后不绑定同ID替身。实际导弹AID不能改变已选择形状，也不能凭空补缺失Warning。Armstrong当前和下一十字均Risky；普通间隔2.1f，第五次先按旧方向推进、再对推进后的队首反向。
- 目标诱导本人只画轮廓、规划避队友；他人避目标圈。分散R原尺寸轮廓、R+1个人导航，使用源量化位置，死目标隐藏但复活恢复；销毁同ID替身不继承旧标记资格。读条补正不反复移除重加不变导航。
- Torpedo只由`NativeActionEffectEvent.MainTargetId`消费已锁目标。该事件来自FFCS `Header.AnimationTargetId`，包含一次完整包的目标集合；不从PR逐受击目标或TargetIndex推断主目标。目标暂时缺对象、pose补正或Boss换目标不改已锁预测。旧录像缺少该输入不能完整结算，保留未知边界。
- Rusalka的吸引范围不作为R30危险圆；拉动结束仅清投影，Flood保留到效果。Sugaar旋转首击Finish+1.1f，后两击由18098/18099真实Finish推进至now+2.7f，两个已显示扇形均Risky；拉动原点跟当前Boss，序列中心仍锁起手地点。多只Sugaar各从玩家位置独立计算，不串联。免疫严格晚于activation，落点提示只查源指定的Flood/已显示旋转扇形。
- Aegeiros圆及锥跟Boss当前位置；Leaf取消不清预测，实际Rimestorm显示时取代预测且其结束才清除。Canopy无需效果事件，任何读条期间隐藏，目标丢失或Boss不可选中时隐藏；真实R12锥仅轮廓，主目标规划避以每个存活队友方向构成的R100锥，其他人规划避真实R12锥。该源组件没有主动PR面向请求。

- Nariphon：读条开始冻结SID1435名单，后续状态变化不改该次资格；人数只算存活且合格队员，圈外人数<8才入圈，圈内<=8可留。本人选任一合格buddy的R3圈，其他人进入R6；这些是带原Activation的圈外禁区，不是可选goal。反向union覆盖整个规划图，多个buddy为OR；真实解算已覆盖旧priority101跟随落点锁、2秒入圈以及0.5/5秒不同deadline。预览单独保留原R6轮廓及资格颜色。当前MP按敌人实例隔离状态，避免上游全局事件广播将多只Boss队列/Shared合并；这属于MP多实例适配，不宣称源事件已按caster隔离。中途启用缺历史名单时仅能按当前快照接管。
- Yilan/NarrowRift共用1958/1959/1960/1961与1257/3629。每个pending段13.2码，按expiry排序、方向累计；active先按6.6乘剩余秒数投影，再接pending，无8秒截断，不依Param二次转向。只有最终落点参与Hint；Yilan始终查Boss当前R18，NarrowRift查玩家局部R30。事件先/后快照均不重复状态；状态Lose、对象移除、停用及Seek重建清理。源没有主动面向或移动hold策略，未增造。
- NarrowRift只显示队首圈/环，activation取当前主读条Finish，无读条时立即生效；未列AID的packet-only读条也接入结束消费。27333建立的光束跟Boss当前朝向/位置，超过预测时刻或27519视觉结束不清。Yilan的预测跨Soundstorm结束保留，actual MiniLight取代并最终清除。
- Sally记录和执行均可追加，显示全队列前两项；未来圈/环仅预览，未来十字仍Risky。倒序按源5.2f−2.6f*i，1/2/4项历史也不改成从0开始；缺历史不猜形状。只有七种真实Effect弹队首，CastFinished不消费；锁执行起手位置/朝向，packet修正未消耗项，重复Effect幂等。

- WhirlingOmen在缓存为空时才记录；释放消耗首个方向并追加两段，所有段显示且Risky；效果不推进，真实后续Finish才弹队首。Rrax起手只重置侧数、不清旧AOE；Reflex按实际缓存数量释放，空AOE时效果也不消费侧数；第二项相反方向误差≤1度时只清Risky，晋升首项才恢复。
- Pkuucha复合视觉结束和ActionEffect均不消费队列；短读条真实结束直接FIFO，不按形状匹配。R14约束空队列也持续，跟当前Boss位置，边界恰14码危险；不产生额外轮廓或GTFO文字。已释放AOE锁起手位置，不追随Boss。
- Forecaster不依赖天气事件；气候只替换缓存首个匹配形状，首击AID不覆盖缓存顺序。已有预测不因新释放清空；缓存消耗后真实Channel重启不追加旧历史，后续packet不改旧代。未来圈/环仅在与队首同属圈/环时取消Risky，其他第二项仍可参与规划；剩余时钟不随效果重排。

## 离线证据与边界

- 最新一批共130项Hunt模型、连同任务选敌/Graoully为141项通过；生产编译0错误、4项既有Crucible警告。证据为`.tmp/halatali-verify/native-hunt-three-model-final.log`、`native-hunt-three-build-final.log`、`native-hunt-three-catalog-final.log`。新增Pedantry、Ixtab、Slippery的定向实际DLL及Doom迟到A/B/C输入证据见`native-hunt-three-failures.log`、`native-ixtab-doom-replay-final.log`；全目录预览容器的无序枚举不作为机制顺序，断言按机制ID识别形状。上一批为116项Hunt、127项相关模型及276条集中PASS，保留在`native-neyoo-nechu-model-final.log`、`native-neyoo-nechu-build-final.log`、`native-neyoo-nechu-catalog-final.log`。以下早前批次数量是当时证据，不代表当前累计数。
- `tests/SimpleHuntRegression.cs`：19条规则逐项真实读条补偿、13形状边缘/宽度/角度、预测分类；同OID实例、锁姿态、取消旧快照、跨代旧输入、事件先于对象快照、目标更新、队员死亡/缺失、首次不可选中与机制停用。
- `tests/SimpleHuntControlsRegression.cs`、`SimpleHuntSequenceRegression.cs`、`SimpleHuntComboRegression.cs`：精确几何、两种凝视与窄共同解、击退分组件/同组件串联及重启顺序、六种免疫、实际后续判定、热病事件/快照/来源替换、采样时钟及packet补正。
- `tests/ScriptCompilation/NativeHuntReplaySimpleHunts.cs`、`NativeHuntReplayControls.cs`、`NativeHuntReplaySequences.cs`、`NativeHuntReplayCombos.cs`：实际MasterPlayer.dll回放六组模块；原19规则同时13危险区/32预览（含19个实例敌人标记）；后续批覆盖双形状、预览/风险分层、动态朝向、五连击、状态刷新、反向Seek、PR回放隔离及停止清理。
- `tests/ScriptCompilation/ReplayPartyCaptureRegression.cs`：直接调用生产DLL采样，使用匹配FFCS结构内存验证PR槽0/2、联盟8/16/23、Flags、非法/重复ID、洞位不压缩、ARR分组、小组联盟排除与空服务；不使用原生函数。
- `SimpleHuntStoredRegression`、`SimpleHuntTargetRegression`、`SimpleHuntPullRegression`、`SimpleHuntAegeirosRegression`、`SimpleHuntStackRegression`、`SimpleHuntMarchRegression`和`SimpleHuntExecutionRegression`补状态记忆、方向、导弹、十字、目标身份、分散、拉动和常驻顺劈边界；当前合计63个Hunt模型场景通过；新增RecordedTurns/Forecast矩阵覆盖原始读条重启、同值跨代补正隔离、500微秒时长变化及R14边界/线段端点。
- 实际DLL新增Misdirection/Stored/Rotations/Missiles/Compressor/Targets/Followups/Pulls/Aegeiros，共19组Hunt回放；当前共用DLL全目录集中回放247条PASS，0失败；新增RecordedTurns覆盖24种Omen组合及0–4侧Wingblade，Forecast覆盖四种记录、持续R14及真实FIFO。`NativeActionEffectRegression`直接验证生产Capture/Router/Recorder/Reader：输入数组复制、主目标、UTC/Local跨流顺序、epoch/回放/owner/异常隔离、录前排除、玩家过滤、正常停止和DisposeRuntime尾包。测试未安装原生hook。
- 本机Lumina探针确认野外territory清单。实际DLL回放夹具禁止读取实时对象或执行GreenMove；没有部署或游戏内执行证据。

狩猎66个来源模块现已离线验证62个，仍余4个，包括极性位移、持续移动、连段及pending状态机制；不能用入口启用数量代替模块完成数量。源组件异常重叠可能追加超过两项队列，原生不复制其空Shape绘制崩溃路径。没有部署或游戏内方向包、动作、移动执行的验收证据。

Tarchia保留本source的effect去重和时间屏障，避免旧效果消费新代；这属于MP多实例隔离契约，源BMR会将其他NPC效果全局广播，不能宣称严格等价。存活实体脱战时重建构造初始圈，不保留旧Groundstorm后续圈。四项模型及实际DLL回放覆盖预战/开战、早取消/晚结束、普通攻击/未知effect清除、独立实例、误导状态、逆向Seek和停止；原生输入hook仍待实机。

Gunitt的1138按事件时槽位维护bit历史，不随角色换槽搬移。创建的每个分摊保存当时mask，逐帧只刷新原始队首；死亡队首仍占位，后项晋升后才刷新。Shared只对活分摊Forbidden交集做一次预测，交集为0不发布，截止取活项最早；不是每圈各发一波。17361仅由真实Finish消费，观察Finish与迟到包有0.5秒协调窗；更晚可信但缺本地Start的Finish仍接受，当前输入缺服务器代际，不能宣称任意延迟重复均可区分。模型覆盖同帧Lose/换槽、过期Finish、Gain快照确认及未确认Gain过期；实际DLL覆盖SourceId点名、原槽mask、死亡队首、逆向Seek及owner清理。源文字Hint仍通过MP纯文本消费，风险颜色未作为独立结构迁入。

Atticus喊话在事件时锁来源位置/朝向及+10.5d初始时钟，未知喊话不清队列、不截断六项。39003/4/5只重排当时剩余项；同代补正保留原stage，已消费项不复活，后到喊话不被旧Start补正重排。仅前二显示且都参与导航；39003/4/5/39009/10/11真实效果逐次出队，Finish及预测到期不消费。39013分摊按owner持组件列表，Finish按目标删首匹配，不按caster分桶；新owner重放已观察helper读条，helper显式销毁及快照消失都结束分摊。模型与实际DLL分别验证双helper FIFO、新owner补播、旧owner撤销、逆向Seek与清理。

Heshuala的`HuntHeshualaState`直接保留Spins、来源/Order、旋转增量、剩余次数和真实Finish时钟；HigherPower赋值3/4/5/6，仅Spins==0时从主怪3979恢复。Finish以X/Z分别≤1码且角差≤0.05匹配首序列，匹配失败仍递减Spins；效果不推进。缓存显示先未来后当前，两段均Risky，未来时刻只在缓存重建时取max(now,NextActivation)+2.7d。十字首次Update即锁来源、位置、角度、形状选择及now+11.5d+2d*Spins，Spins<3才显示；后续状态与packet空间补正不改冻结结果，39102/39103真实Finish才允许重新初始化。内部保留原caster身份，仅输出层适配owner；packet提前预测未获raw确认便销毁时按Source+Order撤销，不伪造一次推进。`Encounter.Areas → SimpleHuntScript.Render → SafeZone/Draw`分别发布导航危险区和双视图原尺寸预览；此源没有额外强制面向、停手或特殊移动执行。模型、显示和导航的生产DLL回放已验证，最终像素及游戏内GreenMove仍待实机验收。

Ihnuxokiy的`HuntIhnuxokiyState`保留储存形状与独立效果队列。39778/39779在Start覆盖储存，取消不撤销；39773/39828锁起手落点和方向追加半圆及可选尾项，消费储存。Finish、caster销毁和预计时钟到期均不消费队列，即使先收到packet而未获得raw确认，已创建队列及其储存消费仍一起保留；39773/39828/39781/39780任一合法来源真实效果逐项弹首。全部项绘制，只有队首参与导航，同代补正不复活已消费项；新owner只补播当前读条，不虚构更早储存历史。ChaoticStorm按源SingleTargetCast保留Tankbuster类型及self-target回退当前攻击目标，未改成全队伤害预测。

Ihnuxokiy强制行进复用`HuntMarchState(2161,2162,2163,2164,3)`及`HuntDrawing`，同QueenHawk共用三秒行进输入但保持各自落点提示。生产模型保留方向、状态到期、累计顺序、1257/3629 active剩余时长，速度6.6、每个pending段19.8码、无提前窗口或撞墙截断；只按最终落点相对当前玩家超过R30提示，不登记硬边界。源没有额外导航、面向或移动暂停，未扩展成自动执行。模型与实际DLL已分别覆盖显示投影、累计与active剩余段、末段提示、效果队列导航、逆向Seek和owner清理；移除一个owner保留其他owner的输入，最后owner移除立即撤销玩家投影，不等待下一轮Scan。两种回放均无原生移动执行，像素和游戏内物理仍待验。

Neyoozoteel的`HuntNeyoozoteelState`保留各owner独立Sap队列、原caster/Order和八击Roots序列。Sap起手四种方向数组按源读取整个队列的前项，不改成新三项内部递推；时钟保留`F+(14.6f+2.2f*i)`，原点锁主怪起手位置，所有项显示且Risky。只有八种真实Sap效果弹首，37308普通效果、37397视觉、Finish、caster销毁和预计到期不消费。Roots锁读条落点/角度，±45度增量、8击，37373/37374/37375真实Effect始终消费第0序列，下一次为事件时刻+2.4f；前二均Risky，缓存仅在序列数或版本变化时重建。37307分摊为R5/min8/max∞的owner组件列表，Finish按目标删首，不按caster分桶。普通37308按真实构造R40/120度，不采用旧R30注释。模型与生产DLL分别覆盖方向/float时钟、显示及导航、完整八击、辅助体与owner生命周期、反向Seek；无新增特殊移动或控制执行。

Nechuciho的`HuntNechucihoState`保留四段队列及`F+(9.3f+2f*i)`，非空追加仍读取全局前项。全部锁读条落点，显示前二；相反第二项（角差≤1度）只清Risky，后续不再相反也不自动恢复，晋升首项才恢复。39785–39788及42164–42171真实效果逐项FIFO，Finish、39496视觉、39872普通效果及预计到期不消费，剩余时钟不随实际效果重排。39492保留`Interrupt! (Applies Doom!)`；源自主AI有ShouldBeInterrupted建议，但旧MP导航桥未消费ActionsToExecute，也未运行RotationModuleManager，PR机制接口没有对应自动打断消费者，因此不把文字迁移宣称为自动打断执行。模型及实际DLL验证显示/导航、十二效果矩阵、重叠与代次、风险晋升、普通提示及撤销；没有游戏内像素或宿主实际移动验收。

Atticus、Heshuala、Ihnuxokiy、Neyoozoteel及Nechuciho通过`SimpleHuntEncounter.StartBroadcastComponents`统一补播当前进行中的读条。新owner初始化期间不接旧缓存Start/Finish；完成本帧观察对账后按演员顺序补播，收据以owner/source/Order记录实际投递输入。已收packet的旧owner保留接受值，新owner用当前raw姿态、目标与时钟；后续有效packet更正经`CorrectBroadcastComponents`按各owner基线补正，完全重复旧packet不触发收敛。stack隐式Finish及Heshuala几何Finish也使用各owner实际收到的目标/角度。首次raw可在owner激活同帧或更晚出现，均仅补缺失owner；已消费队列不会因稳定raw重新建立。模型与实际DLL覆盖同帧结束/变更、首次确认、packet/raw不同输入、补播顺序、隐藏尾队列完整消耗、Seek和最后owner即时撤销。公共输出仍为`Encounter → SimpleHuntScript.Render → SafeZone/Draw`，未引入寻路内部类型或新原生执行范围。

ForgivenPedantry通过`HuntPedantryState`保留Cheek双段全队列、`F+3.1d`、真实效果FIFO和仅出队前总数恰2时晋升Risky的规则；重叠时不普遍晋升尾项。Fever通过当前有真实消费者的`HuntCastBait`保存来源/目标Actor代际、动态R15/120度、原始首项Tankbuster时钟；Finish不清，真实17440效果按来源删首，Source死亡/销毁或Target死亡才删除。Witch只由完整17900的`MainTargetId`或17444真实Finish目标建立，原点固定Primary，宽10矩形动态更新长度；17444无法解析目标时保留旧组，17445效果清全部。源码未设置`witchHunt1done=true`，不补造换坦。结束目标优先显式参数及结束代raw，不用各owner Started收据；Native效果消费前刷新角色，防止主目标尚未进入上次扫描缓存而漏建。目标销毁后冻结旧引用，提示/轮廓按ID，导航本人判断按附着身份；目标死亡不清Witch。17955保留10000范围、眼睛/瞳孔/前90度弧及PR面向。旧录像若没有完整17900主目标且未录17444结束目标，不能从受击列表首项或当前坦克补猜。

`HuntCastBait → TargetPreviews/TargetDangers → SimpleHuntScript.Render`分别发布原尺寸双视图和个人导航。目标本人对队友生成R100方向禁区，其他人避让真实形状；矩形半角按`asin(halfWidth/distance)`，源在队友距离小于半宽时无定义，该边界不擅自扩大为全场禁区。模型、显示及导航用例覆盖取消、效果去重、动态来源/目标、死亡/销毁、同ID新代、逆向Seek和owner撤销；PR面向执行、真实GreenMove及最终像素仍待实机验收。

Ixtab的`HuntIxtabState`保留18077跨Finish/取消的dual标记，下一圈/环消费标记并追加相反形状，第二击精确`F+5.1f`；全队列显示、仅队首Risky，四种真实效果FIFO，取消和预计到期不清。Abyss动态目标中心R6，Finish按source删首；Crypt为动态R38.24/120度，Finish不清，任意17847真实效果清全部，目标死亡/销毁不删除旧记录。两者预测各取原始首项时钟，Crypt的Raidwide类型仅包含bait目标，不扩成全队。Meteor为R10/min8/max∞，结束按目标删首，无Shared预测。同帧raw换目标再销毁helper以source/action/Order分别结束，不能用同时间水位吞掉新代。无方向的dual、圆环、bait和stack输入仍可接收；Blizzard保留真实方向要求。

Doom的`HuntIxtabDoomState`保留Actor列表而非布尔状态：刷新Gain追加、Lose仅删首个同引用；满血、死亡和预计到期不自动清。治疗者自己中Doom时只显示自疗文字，否则按名单显示名字；没有新增治疗动作、停手或移动约束。raw与事件分别维护输入基线，并记录已消费转移；Gain按新状态确认，Lose按被移除状态确认，避免迟到旧A与已采样B/C交错时重复消费或漏掉刷新。销毁仅对仍占用的状态槽补Lose，残留刷新引用冻结，不绑定同ID新对象。上述模型及显示/导航经定向回归，事件交错、取消、helper消失、回放Seek和owner清理均为验收边界。

OID43DC的`HuntSlipperyState`保留38648双段及全部重叠项，均Risky；首段锁读条落点/方向，长20全宽10、`F+0.2d`，次段锁独立caster起点沿方向20码后的量化位置，环10–40、`F+2.8d`。`HuntPendingCast.SourcePosition`由公共输入适配保存真实起点，SourcePosition单独改变也可更正对应队列；量化严格按源先乘float倒数再ToEven舍入。39559/38650真实效果向各owner删全局首项，Finish/取消/到期不清。BodyPress两动作R15保留组AOE按caster结束、无须相同AID；PoisonGas仅Raidwide，Mucus分别为Interrupt提示与R6普通落圈。

PoisonGasMarch复用`HuntMarchState`的真实方向/expiry/active结束输入，独立3秒段长、6.6速度及5秒激活窗口；恰5秒包括，已发动剩余段不受窗口过滤，无撞墙截断。仅最后非零落点命中Slippery队列任一形状时给`Aim for green safe spot!`，不查普通BodyPress、其他普通AOE或判定时刻。`HuntDrawing → Draw`输出双视图线和落点；源没有行进导航、自动面向或移动保持消费者，不增加这些执行。模型分别验证精确窗口、位移规则、独立原点、float舍入边界和效果代次，实际DLL验证预览/导航分离、累计队列、反向Seek及撤销；真实物理、像素和宿主移动仍待实机验收。

ForgivenRebellion通过`HuntRebellionState`和`HuntRebellionRotationState`保留各owner的原caster/Order、候选共享缓存、序列次数/增量/版本及真实消费时钟。17628建立中央F、左右45度F+1.3d三个Risky候选，颜色相对首项activation判断，Finish只删共享缓存首项；17629/18080无活动序列时才建立七次Blizzard，方向按最近17628与第二次旋转差推导，实际Finish推进、Effect不推进。显示未来在前、当前在后，均参与导航；未来时钟仅在数量或消费版本变化时取max(now,current)+1.3d。相同代的姿态补正不改变缓存版本或滑动时钟，raw时长变化则先Finish旧代。

Cyclone由18078/18079方向和167/168的SourceId图标配对，后到一方的当前Actor位置按源量化锁定；合法零方向保留源“未就绪”哨兵。初始F+5.2d，六次18126/18127/18128真实Effect全局FIFO、后续+1.7d，Finish不消费，锁点后Actor移动不跟随。MindJack复用每owner独立`HuntMarchState`，保留两秒/6.6速度的强制行进语义、实际状态及顺序；最终落点仅检查各17601/17602组件的首项，源无行进导航、面向或移动保持执行，不增加这些范围。内部原caster身份直到`Areas → SimpleHuntScript.Render`才转换成owner输出，使用现有SafeZone/Draw。六项模型及实际DLL覆盖量化源oracle、形状参数、完整七/六击、仅raw时长变化、双owner与helper活动销毁、逆向Seek及控制隔离；显示与现有AOE导航已离线验证，游戏内像素、物理和GreenMove执行仍待验。

Ixtab Doom对账另验证raw合并A→C→空的前向链及raw先A→空、随后迟到C Gain/Lose的反向链。仅确认完整连续转移后原子消费，反向仅在已知真实事件前态与对应移除水位内重接收据；未知旧事件不借用未来状态。模型及实际DLL用重复刷新残留引用和逆向Seek核对，不能只以当前raw有无Doom判断列表清空。

Sugriva通过`HuntSugrivaState`保留Twister的最后有效目标Actor引用和独立分摊列表；有效27219 Start覆盖击退槽，无效目标Start保留原槽，任意27219 Finish清当前槽、仅按结束目标删首个stack。击退按目标实时位置、R8内非目标角色、20码、六种免疫严格expiry>activation；目标死亡不清，销毁冻结引用，同ID重建不接替。默认无撞墙截断或危险AOE落点检查，源`GenericKnockback`仅提供双视图线、投影和提示，不增加击退逆推导航/动作执行；stack经现有`StackStates → TargetDangers → SafeZone`提供R8加入、R4集合及满8/同持标记避让，只有这部分提供导航，没有Shared伤害预测。

Butcher复用`HuntCastBait`，锁Start目标身份、动态source/target位置及总120度；Finish不清，27223真实Effect按source删首项，source死亡/销毁或target死亡清除，target仅销毁保留冻结引用。Tankbuster预测用原始首项activation。Prey的27229先重置计数但不清旧bait，非Chocobo目标追加R6、F+1.1d；27226恰第二次Start或任意2939真实Lose清完整组件。原Doom输入账本提取为实际由Doom和Prey共用的`HuntStatusInput`，保证事件/raw双向确认与合并转移不重复清后续bait。快照缺失先发Destroy Lose，再按actor顺序处理读条和状态，不能把所有状态统一延到帧尾。普通投石仍是独立分组AOE，任意caster Finish删其当前普通圈；27220注释击退没有激活组件，不接管。

Sugriva七项模型与实际DLL覆盖几何和严格免疫、单槽/列表重叠、动态诱导、两个方向的Lose收据、同帧Destroy/Start及按actor顺序的raw Lose、helper补播/代际修正、反向Seek和owner撤销；Doom完整旧场景同步通过。双视图输出与当前分摊/诱导导航已离线验证，源无面向、停手或移动hold，未增加这些执行；游戏内像素、真实击退与GreenMove仍待实机验收。证据见`native-sugriva-zone-model-final.log`、`native-sugriva-zone-build.log`及`native-sugriva-zone-catalog.log`。
