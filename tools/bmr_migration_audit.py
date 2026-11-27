"""Build the migration worklist from checked-in BMR inventory and MP ownership.

Signals are a conservative source review aid, never proof of mechanic coverage.
Run from any directory: python tools/bmr_migration_audit.py [--check].
"""

import argparse
import csv
import io
import json
import re
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REFERENCE = ROOT / "docs/reference"
UPSTREAM = ROOT / "MechanicRuntime/Upstream"
SIGNALS = {
    "sequence": r"OnEventCast|NumCasts|CastCounter|Exaflare|RotatingAOE|ConcentricAOE|AddSequence|AdvanceSequence",
    "facing_gaze": r"Gaze|ForbiddenDirections|LookAway|LookTowards",
    "line_of_sight": r"LineOfSight|CastLineOfSight",
    "action_hold": r"Pyretic|HoldActions|BlockCasts|Invincib|Reflect|Spikes|SpecialMode",
    "movement": r"NoMovement|Misdirection|ForcedMarch|Knockback|MustMove|Freezing|StayMove|Pyretic|Teleporters",
    "custom_drawing": r"DrawArena|DrawEnemies|Arena\.(?:Zone|Add|Line|Circle|Cone|Path|Actor)|MiniArena",
}
SYMBOL = re.compile(r"\b(?:class|struct|enum|record(?:\s+class|\s+struct)?)\s+(\w+)")


def load_csv(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def csv_text(rows, fields):
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fields, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue()


ALLIANCE_TRASH_VERIFIED = {'BossMod.Dawntrail.Alliance.A10Aquarius.A10Aquarius': ('BossMod/Modules/Dawntrail/Alliance/A10Trash/A10Aquarius.cs', 'Presets/Alliance/Entries/JeunoAquarius.cs'), 'BossMod.Dawntrail.Alliance.A10Despot.A10Despot': ('BossMod/Modules/Dawntrail/Alliance/A10Trash/A10Despot.cs', 'Presets/Alliance/Entries/JeunoDespot.cs'), 'BossMod.Dawntrail.Alliance.A10Groundskeeper.A10Groundskeeper': ('BossMod/Modules/Dawntrail/Alliance/A10Trash/A10Groundskeeper.cs', 'Presets/Alliance/Entries/JeunoGroundskeeper.cs'), 'BossMod.Dawntrail.Alliance.A10Trash.VanguardPathfinder.A10VanguardPathfinder': ('BossMod/Modules/Dawntrail/Alliance/A10Trash/A10VanguardPathfinder.cs', 'Presets/Alliance/Entries/JeunoVanguard.cs'), 'BossMod.Endwalker.Alliance.A30Trash2.A30Trash2Pack1': ('BossMod/Modules/Endwalker/Alliance/A30Trash/A30Trash2.cs', 'Presets/Alliance/Entries/ThaleiaAngelosFirst.cs'), 'BossMod.Endwalker.Alliance.A30Trash2.A30Trash2Pack2': ('BossMod/Modules/Endwalker/Alliance/A30Trash/A30Trash2.cs', 'Presets/Alliance/Entries/ThaleiaAngelosSecond.cs'), 'BossMod.Endwalker.Alliance.A30Trash1.A30Trash1': ('BossMod/Modules/Endwalker/Alliance/A30Trash/A30Trash1.cs', 'Presets/Alliance/Entries/ThaleiaSerpent.cs')}


ALLIANCE_SANDORIA_VERIFIED = {'BossMod.Dawntrail.Alliance.A20Detector.A20Detector': ('BossMod/Modules/Dawntrail/Alliance/A20Trash/A20Detector.cs', 'Presets/Alliance/Entries/SanDoriaDetector.cs'), 'BossMod.Dawntrail.Alliance.A20JaggedyEaredJack.A20JaggedyEaredJack': ('BossMod/Modules/Dawntrail/Alliance/A20Trash/A20JaggedyEaredJack.cs', 'Presets/Alliance/Entries/SanDoriaJack.cs'), 'BossMod.Dawntrail.Alliance.A20Kraken.A20Kraken': ('BossMod/Modules/Dawntrail/Alliance/A20Trash/A20Kraken.cs', 'Presets/Alliance/Entries/SanDoriaKraken.cs')}


def mp_ownership(row):
    source = row["source"]
    module = row["module"].rsplit(".", 1)[-1]
    if module == 'A31DeathgazeHollow' and source == 'BossMod/Modules/Heavensward/Alliance/A31DeathgazeHollow/A31DeathgazeHollow.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/DeathgazeHollow.cs", '虚空死亡凝视9实际组件，T627/CFC220/NameID5507/9AID extra0核表；固定30x15矩形/精确RayRect与世界ULP/独立五击退/左右锁向/60槽免疫/原Actor Finish；绿色slot64 mask与Hint24/绘图64/同槽换人/同帧Party提交/死亡留bit/有效NPC MainTarget Effect/无TTL无导航，24专项和354相关模型通过；同DLL2304行PASS（2295测试+9汇总）/328入口独立编译/19本批实际专项/exit0；公开提示/圈线宽2或1/灰R.5优先标记显示适配/筛选与clamp/Director/双owner/反Seek/回放禁执行；实时采样0..23，显式24..63输入仅离线源组件合同，实机待验，详见NativeAllianceTrash.spec.md'
    if module == 'A21ArachneEve' and source == 'BossMod/Modules/Heavensward/Alliance/A21ArachneEve/A21ArachneEve.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/ArachneEve.cs", '阿剌克涅10实际组件；T556/CFC168/NameID4871及11AID extra0核表；真实Ground1轮廓84三角形/Web18轮廓467三角形，24槽共享绘图历史/严格高度/固定R30/死viewer/同槽换人；首帧旧导航新KB及次帧发布，双视图休眠；修复horizontal小行下界ULP漏交，AVX512/AVX2-FMA/scalar真实分支源差分；Tremblor10.5/20.5/30.5位置推进/NumCasts/Update缓存/无TTL，raw NPC伤害钟/60槽免疫/独立KB无额外导航；345相关模型含18专项，同DLL2275行首PASS（2266测试+9汇总）/327独立入口，最终20实际专项及3公共PASS，全部exit0；真实solver路径段/三角形孔洞/Director清理与新Load冷回补/双owner/开关/逆Seek/回放禁执行；实机待验，见NativeAllianceTrash.spec.md'
    if module == 'A12IrminsulSawtooth' and source == 'BossMod/Modules/Heavensward/Alliance/A12IrminsulSawtooth/A12IrminsulSawtooth.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/IrminsulSawtooth.cs", '天柱树8实际组件；T508/CFC120/NameID4623及7唯一AID extra0核表；真实Donut场地2轮廓90三角形，原row射线含AVX512/AVX2-FMA/scalar边界及尾段浮点运算；LOS原Actor列表与独立safe FIFO、raw非Spell残留、冻结blocker，源5码SpatialIndex SDF；限定直接补集导航修复旧65%采样丢失，源场内分类/真实solver暴露逃影区，非新增位移；457相关模型18专项及补充线段余量，同DLL2263行首PASS（2254测试+9汇总）/326入口/12实际专项，全部exit0；60槽免疫/原Actor动态NPC伤害钟/Primary单体默认完成/owner/逆Seek/回放隔离；实机待验，见NativeAllianceTrash.spec.md'
    if module == 'A24Ozma' and source == 'BossMod/Modules/Heavensward/Alliance/A24Ozma/A24Ozma.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Ozma.cs", '奥兹玛六实际组件；三场地真实BuildArena/Handle导出多轮廓和三角形，relative精确索引；24槽共享场地历史、当前Primary同ID重绑、Destroyed AND Party0方形/CFC168自定义完成；真实60槽输入/冷热Pet/旧Party顺序，Bomb槽59死亡两查询与AI先采集，事件重绘不额外消费；Holy独立KB线/伤害与原Actor动态NPC钟，无KB导航；391相关模型/16专项，同DLL2250行首PASS（2241测试）/325独立入口/10奥兹玛实际专项，全部exit0；solver/三角形孔洞/双视图休眠/开关/逆Seek/同T556双owner/回放隔离；实机待验，见NativeAllianceTrash.spec.md'
    if module == 'A11Cetus' and source == 'BossMod/Modules/Heavensward/Alliance/A11Cetus/A11Cetus.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Cetus.cs", '联盟击退变体试点/A11Cetus，三模块29实际激活组件；T/CFC/NameID和27唯一AID extra核表；普通WatchedAction+casterID Finish删首、Effect仅计数、逐条位移不排序不去重无TTL；三免疫类60真实槽/严格过期/旧Party提交顺序/冷热Pet；精确圆墙ray/hitbox clamp与pull乘加；源无KB导航不扩执行，同AID独立AOE保留；55最终模型专项，生产0错4既有警告；同DLL2239行首PASS（2230测试+9汇总）/324独立入口/38本批实际专项，全部exit0；真实solver/双视图/开关/逆Seek/owner/回放禁执行，源SHA和DLL证据见NativeAllianceTrash.spec.md；实机待验'
    if module == 'A14Echidna' and source == 'BossMod/Modules/Heavensward/Alliance/A14Echidna/A14Echidna.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Echidna.cs", '联盟击退变体试点/A14Echidna，三模块29实际激活组件；T/CFC/NameID和27唯一AID extra核表；普通WatchedAction+casterID Finish删首、Effect仅计数、逐条位移不排序不去重无TTL；三免疫类60真实槽/严格过期/旧Party提交顺序/冷热Pet；精确圆墙ray/hitbox clamp与pull乘加；源无KB导航不扩执行，同AID独立AOE保留；55最终模型专项，生产0错4既有警告；同DLL2239行首PASS（2230测试+9汇总）/324独立入口/38本批实际专项，全部exit0；真实solver/双视图/开关/逆Seek/owner/回放禁执行，源SHA和DLL证据见NativeAllianceTrash.spec.md；实机待验'
    if module == 'A25Calofisteri' and source == 'BossMod/Modules/Heavensward/Alliance/A25Calofisteri/A25Calofisteri.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Calofisteri.cs", '联盟击退变体试点/A25Calofisteri，三模块29实际激活组件；T/CFC/NameID和27唯一AID extra核表；普通WatchedAction+casterID Finish删首、Effect仅计数、逐条位移不排序不去重无TTL；三免疫类60真实槽/严格过期/旧Party提交顺序/冷热Pet；精确圆墙ray/hitbox clamp与pull乘加；源无KB导航不扩执行，同AID独立AOE保留；55最终模型专项，生产0错4既有警告；同DLL2239行首PASS（2230测试+9汇总）/324独立入口/38本批实际专项，全部exit0；真实solver/双视图/开关/逆Seek/owner/回放禁执行，源SHA和DLL证据见NativeAllianceTrash.spec.md；实机待验'
    if module == 'A13Cuchulainn' and source == 'BossMod/Modules/Heavensward/Alliance/A13Cuchulainn/A13Cuchulainn.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Cuchulainn.cs", '七模块标准组件试点/A13Cuchulainn，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == 'A22Forgall' and source == 'BossMod/Modules/Heavensward/Alliance/A22Forgall/A22Forgall.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Forgall.cs", '七模块标准组件试点/A22Forgall，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == 'A23Headstone' and source == 'BossMod/Modules/Heavensward/Alliance/A23Headstone/A23Headstone.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Headstone.cs", '七模块标准组件试点/A23Headstone，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == 'A32FerdiadHollow' and source == 'BossMod/Modules/Heavensward/Alliance/A32FerdiadHollow/A32FerdiadHollow.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/FerdiadHollow.cs", '七模块标准组件试点/A32FerdiadHollow，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == 'A33ProtoUltima' and source == 'BossMod/Modules/Heavensward/Alliance/A33ProtoUltima/A33ProtoUltima.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/ProtoUltima.cs", '七模块标准组件试点/A33ProtoUltima，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == 'A35Diabolos' and source == 'BossMod/Modules/Heavensward/Alliance/A35Diabolos/A35Diabolos.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/Diabolos.cs", '七模块标准组件试点/A35Diabolos，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == 'A30Trash' and source == 'BossMod/Modules/Shadowbringers/Alliance/A30Trash/A30Trash.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/ParadigmTrash.cs", '七模块标准组件试点/A30Trash，50实际激活组件共享已审定配置，T/CFC/NameID与AID extra核表；普通WatchedAction Finish/原引用代际/真实状态槽/动态raw NPC钟/隐藏晋升源差分；Forgall/Headstone真实并差集及固定内缩，relative行hull/EPS/FMA精确输出；ownStack补集复用精确几何修正旧采样丢失，非own保持既有Goal适配；873相关模型含81专项，生产0错4既有警告，同DLL2198行首PASS（2189测试+9汇总）/321独立入口/68本批实际专项，exit0；真实solver/双视图/双owner/开关/逆Seek/回放禁执行；源、DLL哈希及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == "A20LightArtilleryUnit" and source == 'BossMod/Modules/Shadowbringers/Alliance/A20Trash/A20LightArtilleryUnit.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/PuppetsBunkerLightArtillery.cs", '轻型陆战装置七实际组件，T917/CFC736/NameID与AID核表；Fire原引用缓存顺序/EventState7临时障碍，Homing原source/target、死亡/代际/首钟、own边缘矩形、Effect仅计数，普通WatchedAction Finish；自毁恰好3与同批4次、Dead/HP0/Destroyed差分；792相关模型含15专项，生产0错4既有警告，同DLL2123行首PASS（2114测试+9汇总）/314独立入口/104联盟专项含10本模块，exit0；CircleBait提取后美德与全目录实际消费者覆盖，双视图/solver/双owner/开关/逆Seek/回放禁执行，SHA与失败历史见NativeAllianceTrash.spec.md；实机待验'
    if module == "A20HeavyArtilleryUnit" and source == 'BossMod/Modules/Shadowbringers/Alliance/A20Trash/A20HeavyArtilleryUnit.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/PuppetsBunkerHeavyArtillery.cs", '重型陆战装置三实际组件，T917/CFC736/NameID及AID extra核表；三段精确场地、Map只换Bounds保原Center、pending原Actor构造冻结/距离重构/已加载同ID重绑/队列换代、Energy原引用/冷热/双union时钟无TTL、Laser严格连续1秒窗口/98-100排序/任意Finish；777相关模型含16本模块专项，生产0错4既有警告，同DLL2112行首PASS（2103测试加9汇总）/313入口独立编译/94联盟专项，全部exit0；真实solver捕获默认立即钟下溢并在输出适配修复，双视图/开关/owner/逆Seek/回放禁执行；SHA及失败历史见NativeAllianceTrash.spec.md，实机待验'
    if module == "A10RhalgrEmissary" and source == 'BossMod/Modules/Endwalker/Alliance/A10RhalgrEmissary/A10RhalgrEmissary.cs':
        return "mp_native_offline_verified", "Presets/Alliance/Entries/AglaiaRhalgrEmissary.cs", '拉尔戈神使六组件与32状态；T1054/CFC866/NameID及AID extra核表、真实凹场；Map双锥16.1d追加/真实Effect、Boltloop首匹配/缓存/冷Reset、Strike原引用/Finish保留/Effect源ID、Blue纯Counter/组件重排及完整正常事件链无overdue均有真实Feed差分；761相关模型含12本模块专项、生产0错4既有警告、同DLL2102行首PASS（2093测试加9汇总）/312入口独立编译/85联盟专项，全部exit0；实际solver/双视图/双owner/逆Seek/回放禁执行，失败历史和SHA见NativeAllianceTrash.spec.md，实机待验'
    if module == "A10Lions" and source == 'BossMod/Modules/Endwalker/Alliance/A10Lions/A10Lions.cs':
        return "mp_native_offline_verified", 'Presets/Alliance/Entries/AglaiaLions.cs', '双狮T1054/CFC866/NameID及AID核表、真实凹场地；三个实际组件、原母狮惰性引用与完成先于缓存、Slash全局首Finish和Roar按ID隐藏首匹配、非稳定排序全列表逐项、永久600状态与raw/default伤害均有源差分；749相关模型含80联盟专项、生产0错4既有警告、同DLL2092全目录行首PASS（2083测试加9汇总）/311入口独立编译及76联盟专项，全部exit0；实际solver/开关/逆Seek/owner/回放禁执行，源SHA与证据见NativeAllianceTrash.spec.md，实机待验'
    sandoria = ALLIANCE_SANDORIA_VERIFIED.get(row["module"])
    if sandoria and source == sandoria[0]:
        return "mp_native_offline_verified", sandoria[1], '桑多利亚三项杂兵；T1304/CFC1058、NameID及AID extra核表；真实多孔场地、原Actor/pending、普通与Grouped Finish/四技能98-100排序、Flood唯一覆盖/跨source Finish/无TTL、15码pull免疫与首source R23导航、原caster当前raw非Spell/default伤害及双caster判定钟排序均有真实Feed差分；734相关模型含65联盟专项、生产0错4既有警告、同DLL2067全目录行首PASS及310入口独立编译/61联盟专项，全部exit0；实际solver/开关/逆Seek/owner/回放禁执行，失败历史与源SHA见NativeAllianceTrash.spec.md，实机待验'
    alliance = ALLIANCE_TRASH_VERIFIED.get(row["module"])
    if alliance and source == alliance[0]:
        return "mp_native_offline_verified", alliance[1], "七项联盟杂兵精确ownership；36唯一源AID/extra与T1248/CFC1015及T1178/CFC962核表；真实多轮廓孔洞场地、阶段完成/原Actor代际/同OID pending、普通与Grouped Finish、跨source FIFO、真实免疫槽与pull导航、raw非Spell动态NPC钟均有源差分；716相关模型含47专项、生产0错4既有警告、同DLL2045全目录行首PASS及307入口独立编译/42联盟专项，全部exit0；真实solver/开关/逆Seek/双owner双向释放/回放禁执行；失败历史及SHA见NativeAllianceTrash.spec.md，实机待验"
    if module == "BA3AbsoluteVirtue" and source.endswith(
        "/Modules/Stormblood/Foray/BaldesionArsenal/BA3AbsoluteVirtue/BA3AbsoluteVirtue.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BaldesionVirtue.cs", "十常驻及两个动态Counter；CFC639/T827、BNpcName7976绝对的美德与全部源AID/extra核表；真实resolved场地、原Actor代际与60状态槽、Wind原target/raw钟/首死caster预测、Aurora跨source Bright FIFO与Dark保留、Rays主Boss状态/任意对应Finish全清、Explosion所有tether及default空尾、两独立塔Created/raw位置/原target集合/精确EAnim/无TTL与92状态精确Counter均有真实Feed差分；669相关模型含21专项、生产0错4既有警告、同DLL2005全目录行首PASS(1996测试及9汇总)/300入口及17AV专项，全部exit0；实际solver/开关/非Party/逆Seek/莱丁同T827活跃双owner双向释放/回放禁执行；塔补集修复采样丢失及重叠假边界，源null/default异常显式适配及源SHA/失败历史见NativeBaldesionArsenal.spec.md，实机待验"
    if module == "BA2Raiden" and source.endswith(
        "/Modules/Stormblood/Foray/BaldesionArsenal/BA2Raiden/BA2Raiden.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BaldesionRaiden.cs", "十六实际组件；CFC639/T827与BNpcName7973莱丁、全部源AID/extra核表；真实双resolved场地缩Bounds保旧Center、P1不按HP0结束和Keep四原组件、P2逐组件冷Created/Spell/tether回补；Icon原引用重复/Created清空/Effect按ID删首回退与伤害mask按ID、Cloud按Loc选8/6/4而raw caster匹配首line及完整内部缓存、Chains旧Party槽原partner和Assigned锁存、Silent真实Effect计数/111状态早条件/有序Reset及Stop/永久尾均有真实Feed差分；648相关模型含23专项、生产0错4既有警告、同DLL355相关PASS和20莱丁专项、1987全目录行首PASS(1978测试及9汇总)/299入口，全部exit0；真实solver/非Party/开关/逆Seek/活跃链线owner双向释放/回放禁执行，无新增源Module选敌执行；源SHA和失败历史见NativeBaldesionArsenal.spec.md，实机待验"
    if module == "BA1Owain" and source.endswith(
        "/Modules/Stormblood/Foray/BaldesionArsenal/BA1Owain/BA1Owain.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BaldesionOwain.cs", "十常驻加临时LegendaryImbas；CFC639/T827、BNpcName7970与实际AID/extra核表、真实减形场地；真实60槽元素枪先于boss FIFO/跨来源Effect不随Finish清、Spirit原Icon重复/死亡Destroyed最近等距首项与前景改目标及旧AI新显示缓存、冷热tether ID与null目标原引用代际、default钟反向眼10000范围和raw cleave文本/量化显示导航、临时Imbas冷补重建/原始dead Spread条件/完整457状态永久尾均有真实Feed差分；625相关模型含20专项、生产0错4既有警告、同DLL125相关PASS及13欧文专项、1966全目录行首PASS(1957测试及9汇总)/298入口，全部exit0；真实solver/双视图睡眠/非Party/开关/逆Seek/owner/回放禁执行，Art Owain Ceto同T827活跃三owner双向释放；源SHA和失败历史见NativeBaldesionArsenal.spec.md，实机待验"
    if module == "BA1Art" and source.endswith(
        "/Modules/Stormblood/Foray/BaldesionArsenal/BA1Art/BA1Art.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BaldesionArt.cs", "十实际组件；CFC639/T827、BNpcName7968与17唯一AID/extra核表，真实减形场地；幻枪原列表五项sticky风险与跨来源Effect FIFO、Geas双Cross仅Defilers Start清、Stay2双Hint/双视图休眠/Framework诊断寿命、追踪原Actor及旧PartyMask/严格最近/三路Effect落点/首发与续发五击/独立Update重置、完整333状态raw Cast/条件早转overdue/永久尾均有真实Feed源差分；605相关模型含19专项、生产0错4既有警告、同DLL16专项加3公共与1952全目录行首PASS(1943测试及9汇总)/297入口全部exit0；真实solver/非Party/开关/逆Seek/owner/回放禁执行，Hydatos同T827精确OID共存；源SHA与失败历史见NativeBaldesionArsenal.spec.md，实机待验"
    if module == "CLL4Dawon" and source.endswith(
        "/Modules/Shadowbringers/Foray/CastrumLacusLitore/CLL4Dawon/CLL3Dawon.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "二十一实际组件；CFC735/T920、BNpcName9452与45唯一AID/extra核表；双场绘图副作用旧Navigation/Damage/Facing新显示gate/双墙独立预告、Hammer整组sticky风险与watched Finish、Plumes原Actor懒列表/源double三角ULP/追加和两Finish、Obey跨source Effect FIFO、Blood原caster位置7击/缓存与Pulse首位置stage计数、20847真实60槽免疫/直接Goal、凝视10000和Normalize伤害排序均有真实源对照；586相关模型含41专项、生产0错4既有警告、同DLL288相关/35专项加3公共/1935全目录行首PASS(1926测试及9汇总)/296入口，全部exit0；solver/双视图睡眠/非Party/非空Facing禁用缓存/逆Seek/owner/回放禁执行；失败及源SHA见NativeBozjaSouth.spec.md，实机待验"
    if module == "CLL3Adrammelech" and source.endswith(
        "/Modules/Shadowbringers/Foray/CastrumLacusLitore/CLL3Adrammelech/CLL3Adrammelech.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "十四实际组件；CFC735/T920、BNpcName9442与43唯一AID核表；持久Holy环与两真实中心/双水独立直接Goal及60槽严格免疫/冰火独立槽真实Player读条与前帧高度/绘图双查询睡眠不消费和Framework诊断寿命/Stone跨caster首位置匹配缓存/Warped第99排序1秒风险/Twister四钟障碍；545相关模型含26专项、生产0错4既有警告、同DLL237相关与23专项加3公共、1900全目录行首PASS/296入口，全部exit0；真实solver/开关/逆Seek/owner/回放禁执行，源SHA和失败证据见NativeBozjaSouth.spec.md，实机待验"
    if module == "CLL1Brionac4thLegionHelldiver" and source.endswith(
        "/Modules/Shadowbringers/Foray/CastrumLacusLitore/CLL1Brionac4thLegionHelldiver/CLL1Brionac4thLegionHelldiver.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "十五实际组件；CFC735/T920、BNpcName9436与46枚举AID核表；构造双场绘制副作用旧导航新显示/缓存owner失效、原HellDiver与SkyArmor完成/HP0保留、七次空间Effect/Orb历史/磁力八槽冷零Target与直接Goal/WildFIFO/红外原分边重复60槽；519相关模型含21专项、生产0错4既有警告、同DLL185相关/17专项加3公共、1877全目录行首PASS(1868测试及9汇总)/296入口，全部exit0；真实solver/开关/逆Seek/owner/回放禁执行；原优先级仅诊断、密补集真实Host采样边界及SHA见NativeBozjaSouth.spec.md，实机待验"
    if module == "PyromancerSupreme" and source.endswith(
        "/Modules/Shadowbringers/Foray/Skirmish/PyromancerSupreme.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "七实际组件；T920/CFC735、FATE1616最强的火焰法师与源AID核表；每个Pending独立构造ThermalShock R20原点原钟/无导航，原历史晋升及phase局部停用，六标准组件八动作/任意Finish99项排序；HP0保留与不可选退场/同帧重绑、wipe仅卸载Loaded保Pending且冻结场地、Destroyed远距释放；498相关模型、生产0错4既有警告、同DLL168相关/12专项加3公共、1860全目录行首PASS(1851测试及9汇总)/296入口，全部exit0；真实solver/计划选敌/开关/逆Seek/owner/回放禁执行，真实旧Zone桥与预战显示互补门控；旧ReplayFeedAdapter丢FateId/IsSpell的oracle边界与SHA见NativeBozjaSouth.spec.md，实机待验"
    if module in {"ConflictingWithTheFirstLaw", "HeavyBootsOfLead"} and source.endswith(
        f"/Modules/Shadowbringers/Foray/Skirmish/{module}.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "两遭遇战十六实际组件；T920/CFC735与FATE1605/1612名称及全部源AID extra核表；确定性指纹manifest生成、R30默认/FATE缓存逐轴阈值/创建代FateID，真实Party0门控与旧Party Created、同帧reset重建正序尾部/Selected；原目标分摊FIFO/异钟/换代/少于二人导航，普通watched Finish与Effect保留；483相关模型、生产0错4既有警告、同DLL156相关及22专项、1848全目录行首PASS(1839测试及9汇总)/296入口，全部exit0；实际solver/选敌/开关/逆Seek/owner/回放禁执行；源oracle显式补FATE输入，旧桥缺字段未改，SHA及适配见NativeBozjaSouth.spec.md，实机待验"
    if module == "CLL2AlbeleoTheMaleficent" and source.endswith(
        "/Modules/Shadowbringers/Foray/CastrumLacusLitore/CLL2AlbeleoTheMaleficient/CLL2AlbeleoTheMaleficient.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "六实际组件；CFC735/T920与BNpcName9433/全源AID extra核表；四标准AOE、有限35凝视及打断原引用/固定hint，HP恰1冷/热退场与HP0存活保留，不规则世界多边形及全部转角ULP；457相关及最终13定向真实源差分、生产0错4既有警告、同DLL271相关/10专项加3公共、1826全目录行首PASS/296入口独立编译，全部exit0；真实solver/计划选敌/濒死回退/开关/Seek/owner/回放禁执行；Actor marker未消费、Ray原始边界与旧桥浮点舍入差异及SHA见NativeBozjaSouth.spec.md，实机待验"
    if module in {"CE14VigilForLost", "CE21FinalFurlong"} and source.endswith(
        f"/Modules/Shadowbringers/Foray/CriticalEngagement/{module}.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "两CE十四实际组件；CFC735/T920及16动作extra本机核表；开放世界原Actor塔、NPC动态钟、原目标分摊、手缓存/连线及首手提示；99项源排序/隐藏Risky晋升/跨CE超距Pending新序号；真实组件与管理器差分24相关模型、生产0错4既有警告、同DLL24专项及无筛选1441PASS场景行/0FAIL、293入口独立编译，全部exit0；显式owner预览顺序兼容既有消费者，实际solver脱险及入分摊无fallback，Seek/owner/回放禁执行；SHA256、旧桥raw/转换边界和全局阶段结构限制见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE12BayingOfHounds" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE12BayingOfHounds.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "八实际组件/十三动作extra核零；R24.5/pull25真实Manager；两跳单项覆盖及Finish+3.7镜像、三段地震全局FIFO、纯CastHint、NPC动态钟；真实组件差分33相关及最终9定向模型，生产0错4既有警告，同DLL35专项/1452全目录PASS场景行与293入口独立编译，全部exit0；冷枚举双顺序、空历史、零长raw/绘图拒绝/真实导航转换、真实solver脱险、Seek/owner/回放隔离已验；指纹及失败历史见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE13KillItWithFire" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE13KillItWithFire.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "七实际组件/七动作extra核零；真实60槽旧Party冷热Pet与双色历史bit、首次输出惰性敌人缓存、移动胶囊平方Check/raw投影Distance/四钟及独立障碍；源Normalize顺序、min8提示和两人buddy真实转换、NPC动态伤害钟；41相关模型、生产0错4既有警告、同DLL42专项及1459全目录PASS场景行/0FAIL、293入口独立编译，全部exit0；Seek/owner/回放隔离已验；MaxValue绝对钟适配、稳定句柄与旧桥索引键差别及最终SHA256见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE11ShadowOfDeathHand" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE11ShadowOfDeathHand.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "十一实际组件；31源AID及extra核零；墙Created逐Actor冷顺序与当前量化中心、Wind锁点锁向/三类免疫旧Party真实60槽/四真实Effect清理，72方向实际导航转换及线逐字段；中断原目标仅诊断、胶囊四钟障碍、min8及动态伤害钟；49相关模型、生产0错4既有警告、同DLL57专项及1474全目录行首PASS场景/0FAIL、293入口独立编译，全部exit0；Seek/owner/回放隔离，SHA256及失败历史见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE24PatriotGames" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE24PatriotGames.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "七实际组件；T920/CFC735-DynamicEvent10及全AID extra本机核表；方形场地与pull、普通watched Finish、两原caster动态单体、每塔R4/R8/前8含死Forbidden与64存活人数/默认钟；真实Feed冷热连线/同值真实包/零目标/原Actor顺序及帧快照-按序事件-Update，四位置半充能/Effect仅清AOE/首塔条件导航；59相关模型、生产0错4既有警告、同DLL67专项与1484全目录行首PASS/0FAIL、293入口独立编译，全部exit0；实际转换/Seek/owner/回放禁执行，SHA256与失败见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE31MetalFoxChaos" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE31MetalFoxChaos.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "五实际组件；T920/CFC735-DynamicEvent13及全AID extra本机核表；29.5正方形/pull30、原caster动态raidwide、普通watched Finish；Bit惰性原Actor缓存/三模式严格gate/默认色仍Risky/任意NPC Effect恰14重置及全禁区RefSort；Rush原source/target、Update长度动态方向/首ID Finish/Effect保留/同ID新代显示按ID导航按引用/聚合mask与首原钟；67相关模型、生产0错4既有警告、同DLL74专项及1491全目录行首PASS/0FAIL、293入口独立编译，全部exit0；180度实际采样/NaN空转换、真实solver/跨组件绘制顺序/Seek/owner/回放禁执行，SHA256与失败见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE34TrampledUnderHoof" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE34TrampledUnderHoof.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "七实际组件；T920/CFC735-DynamicEvent11及全AID extra本机核表；Finish原NPC钟缩场环/墙Created量化冷顺序、BullHorn原target槽钟/跨source MainTarget首项消费、Glimmer锁点无限队列/逐轴首项十计数、R8分摊R4 buddy、凝视翻转全眼与任意Finish全清；Party0高度等级归一化选敌、精确32边形及全禁区RefSort；92相关模型、生产0错4既有警告、同DLL99专项及1507全目录行首PASS/0FAIL、293入口独立编译，全部exit0；面向与选敌为MP原生消费适配，回放仅规划/Seek/owner清理，SHA256及失败见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE33WhereStrodeTheBehemoth" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE33WhereStrodeTheBehemoth.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "六实际组件；T920/CFC735-DynamicEvent15及全部源AID extra本机核表；LoS原caster当前剩余最小/FIFO安全区/原corpse锁点与实际hitbox/精确Clipper及SIMD-FMA-行X范围；Juice真实nullable包点/重复包/惰性原Actor/一次Update一项，固定延迟Raidwide、原引用Horn/真实Effect及corpse首圈全矩形Finish预告；105相关模型、生产0错4既有警告、同DLL88专项和1514全目录行首PASS/0FAIL、293入口独立编译，全部exit0；实际Feed/solver/双视图/空预览拒绝/Seek/owner/回放隔离，原始距离幅值与仅消费符号采样边界、SHA256及失败见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE23FiresOfWar" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE23FiresOfWar.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "八实际组件；T920/CFC735-DynamicEvent9及全部源AID extra本机核表；NPC60槽原引用分摊/原Effect、R4开放塔、动态NPC钟、Concentric独立原始与发布缓存、R24、九圆锁Mater和重复最远部分绘制、TimedEnrage、常驻动态Cleave；特殊Trash与cached原Mater完成、每Framework生命周期/AI与切视角单次消费、源索引异常整导航撤销和恢复、弱goal不扩移动；123相关模型、生产0错4既有警告、同DLL97专项和1523全目录行首PASS/0FAIL、293入口独立编译，全部exit0，真实Feed/solver/双视图/Seek/owner/回放隔离，SHA256及失败见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE51ThereWouldBeBlood" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE51ThereWouldBeBlood.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十实际组件；T975/CFC778-DynamicEvent24及全部源AID extra本机核表；七标准AOE锁点锁向/原始watched Finish与RefSort导航，原caster动态单体/全屏诊断和全局提示，普通CastGaze按ID删首且无CE34选敌；独立区域/GUID/机制设置与精确ownership，源完整Feed冷激活/无scan下一Framework/Finish，135相关模型、生产0错4既有警告、同DLL南方和札杜诺109专项/1536全目录行首PASS/0FAIL、294入口独立编译，全部exit0；真实solver/Seek/owner/回放禁执行，面向联合求解适配与失败及SHA256见NativeZadnor.spec.md，实机待验"
    if module == "CE63WornToShadow" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE63WornToShadow.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "五实际组件；T975/CFC778-DE28及16源AID extra本机核表；Stormcall真实60状态槽、恰3 RefSort/前2/按ID首项修正与Finish、零方向与倒数乘法ULP边界，Foreshadowing原Actor引用/触发回调NPC钟+11.1f/四记录重复初始化/恰5隐藏/不同Finish清状态，三标准组件复用；独立审查、真实Feed冷顺序异钟与5→4→9和代际、150相关模型、生产0错4既有警告、同DLL119联合专项/1546全目录PASS行含1537测试行与9汇总行/294独立入口，全部exit0；真实solver R20多边形脱险/Seek/owner/回放隔离，失败与SHA256见NativeZadnor.spec.md，实机待验"
    if module == "CE52TimeToBurn" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE52TimeToBurn.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "四实际组件；T975/CFC778-DE26及13源AID extra核表；三组互斥含空Bomb/独立safe背景/原Bomb1缓存/Effect FIFO，Clock原位置/恰9 ListSort/隐藏两项/按caster轴1删首，真实动画与快照独立队列/冷丢失，OnlyLoadIfTargetable pending、空FIFO保历史撤整个Region输出恢复，原生Ray保持源精确边界并记录旧桥舍入差；160相关模型、生产0错4既有警告、同DLL130联合专项/1557全目录PASS行含1548测试与9汇总/294独立入口，全部exit0；真实solver/Seek/owner/回放禁执行；失败与SHA256见NativeZadnor.spec.md，实机待验"
    if module == "CE41WithDiremiteAndMain" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE41WithDiremiteAndMain.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十实际组件；T975/CFC778-DE21与21源AID extra核表；两独立LoS原引用/冻结R60 polygon/活typed晶体/错Finish FIFO、Earth按阶段轴1匹配与Update缓存、Shardstrike原target/raw首钟/单Union先Normalize后展开、Hailfire原source-target图标ListSort/Effect FIFO/死亡与代际及绘图ID导航引用；独立审查后乱序Party反例先失败再局部修槽顺序，真实DrawConvert证明Actor marker未消费不扩展；175相关模型、生产0错4既有警告、同DLL143联合专项/1570全目录PASS行含1561测试与9汇总/294独立入口，全部exit0；真实Feed十组件冷激活/图标、真实solver/Seek/owner/回放隔离；证据与SHA256见NativeZadnor.spec.md，实机待验"
    if module == "CE44FamiliarFace" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE44FamiliarFace.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "九实际组件；T975/CFC778-DE29和22源AID extra本机核表；标准AOE/99项分组、冻结原target槽钟的延迟单体、.8f提升double全屏、普通分散R6/R7；塔Loc去重/Actor洞/首项Finish与布尔模型洞、MiniArena固定中心和Bounds重心分离、旧Nav最多顶点外环及Draw孔洞未消费；地火原位置包方向/真实TargetPos逐轴首匹配/Update缓存future与current、Created锤队列前2全Risky；独立审查修归一化禁区与合并伤害顺序、CE34/41重复槽反例及实时每Framework采样接线；192相关模型、生产0错4既有警告、同DLL155联合专项/1582全目录PASS行含1573测试与9汇总/294独立入口，全部exit0；真实Feed九组件、实际solver/精确ownership/Seek/owner/回放隔离；证据与SHA256见NativeZadnor.spec.md，实机待验"
    if module == "CE62LooksToDieFor" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE62LooksToDieFor.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十一实际组件；T975/CFC778-DE30及24源AID extra本机核表、两个真实空名；Lightning重复原点首项原位替换donut/真实TargetPos与caster位置分别消费，Cloud步长5四爆最多2双Effect均取原caster/真实Update发布缓存；Burn完整ModelState三字段热回调仅排Pet、冷激活不补历史、严格头钟连续前缀/全局Effect FIFO空抛保整批/缺source成功恢复；标准射线半圆分组、三全屏/单体动态原NPC钟、普通分散和纯CastHint；203相关模型、生产0错4既有警告、同DLL170联合专项/1597全目录PASS行含1588测试与9汇总/294独立入口，全部exit0；真实Feed十一组件、实际solver/ownership/Seek/owner/回放隔离；独立核验修preScan正式快照先于冷激活和整批错误恢复，CE52手工JSON坐标保真补验；证据与SHA见NativeZadnor.spec.md，实机待验"
    if module == "CE53HereComesTheCavalry" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE53HereComesTheCavalry.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十四实际组件；T975/CFC778-DE22与24源AID本机核表；未选中主怪仍pull、Charge原位置再量化、两击退仅线、FireShot三列表；RawSteel旧Party/Created-Dead原引用两侧、真实Op先Untether再Tether/重复callback、原目标5秒缓存/非TTL、跨source MainTarget首消费/动态几何和合并mask原头钟；Framework统一提交世界与全局出生序/采样source gate，真实Feed多preScan异钟/晚创建target/同帧Party/跨CE选择；221相关模型、生产0错4既有警告、同DLL186联合专项/1613全目录PASS行含1604测试与9汇总/294独立入口，全部exit0；solver、ownership、Seek/owner/回放隔离；分摊buddy反向有限采样丢弃与他人R8目标分验，证据及SHA见NativeZadnor.spec.md；实机待验"
    if module == "CE54NeverCryWolf" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE54NeverCryWolf.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十一实际组件；T975/CFC778-DE25与28源AID本机核表；LunarCry私有原Actor柱/冷逐Actor Created-Start/最小Remaining/FIFO安全区，Bracing真实免疫槽/成功移动max1与全部caster AI分流；六旋转序列共同末点钟/每序列future-current缓存/普通Finish首位置角匹配，Thermal Created锁点及snapshot Finish空FIFO失败保旧cast/Destroyed-untether副作用和跨CE逐Actor Finish屏障；239相关模型及最终两种Loaded顺序2定向，生产全量0错4既有警告且增量exit0同hash，同DLL197联合专项/1624全目录PASS行含1615测试与9汇总/294独立入口，全部测试exit0；solver、精确ownership、owner/Seek/回放无执行；证据、适配边界与SHA见NativeZadnor.spec.md，实机待验"
    if module == "DRN1TrinitySeeker" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeNormal/DRN1TrinitySeeker/DRN1.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumNormal.cs", "十三实际组件；T936/CFC760-NameID9834及44唯一AID客户端核表；真实场地/Blade多环导出与Map低高字、普通watched Finish、Mercy隐藏晋升及真实60槽/冷Pet/raw Primary朝向、两图标独立FIFO/原引用、Chains冷热旧Party、动态线分摊文字/AI分流与斜角精确Check、Moon锁点/实际Effect均有源对照；293相关模型、生产0错4既有警告、同DLL240联合专项/1669全目录PASS行含1660测试加9汇总/296独立入口，全部exit0；真实solver、ownership、Seek/owner/回放禁执行已验；Blade仅承诺符号及旧桥采样消费，不宣称正距离幅值等价；证据与SHA见NativeDelubrum.spec.md，实机待验"
    if module == "DAL1Sartauvoir" and source.endswith(
        "/Modules/Shadowbringers/Foray/TheDalriada/DAL1Sartauvoir/DAL1Sartauvoir.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十三实际组件；T975/CFC778及NameID9384本机核表，全40唯一AID存在且extra0；标准锁点AOE/普通与Grouped Finish、TimeEruption四阈值RefSort与隐藏二晋升、Created塔R4/current+8.7/冷延迟与热Player过滤/首caster空间Effect/原Actor惰性资格缓存、min8分摊及分散原target代际；364相关及最终13定向源差分，生产0错4既有警告，同DLL312联合专项/1732全目录行首PASS/296独立入口，全部exit0；完整Feed十三组件Normalize/实际solver/机制开关/Seek/owner/回放禁执行，失败/适配及SHA见NativeDalriada.spec.md，实机待验"
    if module == "DAL1Gauntlet" and source.endswith(
        "/Modules/Shadowbringers/Foray/TheDalriada/DAL1Gauntlet/DAL1Gauntlet.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十六实际组件三阶段；T975/CFC778、NameID10212及源全AID客户端核表/extra0；组件优先冷回补、原Boss引用/同帧连续阶段、十剑恰10少数方向与Unseen状态先后/真实60槽/原Actor动态raw钟、塔+9/独立图标FIFO与Donut原target/无共享伤害、Storm前二swap和North/Pain导航优先/免疫/真实Effect均有源差分；446相关模型及最终26定向PASS，生产0错4既有警告，同DLL387联合专项/13专项加3公共/1816全目录行首PASS/296独立入口，全部exit0；缩场23真实采样保留偏心KB/大面积原有拒绝，Arena源安全孔保留并显式修复旧桥丢孔；实际solver/绘图/面向计划/开关/Seek/owner/回放禁执行；失败/适配及SHA见NativeDalriada.spec.md，实机待验"
    if module == "DAL4DiabloArmament" and source.endswith(
        "/Modules/Shadowbringers/Foray/TheDalriada/DAL4DiabloArmament/DAL4DiabloArmament.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十六实际组件；T975/CFC778、NameID10007/51唯一AID本机核表/extra0；原门缓存/异常副作用/FIFO与Risky历史、Nox发布、Orb原Actor矩形反向交集、双矩形bait/line、KB真实墙与Bomb60槽冷Pet/raw Ability均有源对照；统一Feed committed差分及全部Finish后跨模型Cast安装，热失败/旧输入恢复/clear不重复消费CE54；420相关模型、生产0错4既有警告、同DLL374联合/最终24专项加3公共/1803全目录行首PASS/296独立入口，全部exit0；solver/开关/Seek/owner/回放禁执行；适配、失败和SHA见NativeDalriada.spec.md，实机待验"
    if module == "DAL3SaunionDawon" and source.endswith(
        "/Modules/Shadowbringers/Foray/TheDalriada/DAL3SaunionDawon/DAL3SaunionDawon.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十七实际组件双阶段；T975/CFC778、NameID10192与41唯一AID本机核表/extra0；五Keep原实例/激活顺序、惰性Dawon列表与原Actor代际、8套272点螺旋/查询染色/缺视角不查询、Obey热动画FIFO/弹首后追加、冷Pet60槽与热过滤、R5原target/真实Party槽序/死亡raw首钟、KB倒数乘/墙ULP/零线/匹配Finish、MaxValue真实绝对窗口均有源对照；402相关模型PASS、生产0错4既有警告、同DLL349联合专项/最终20个DAL3专项加3公共/1770全目录行首PASS/296独立入口，全部exit0；真实solver、双Boss完成全输出撤销、开关/Seek/owner/回放禁执行；适配、失败及SHA见NativeDalriada.spec.md，实机待验"
    if module == "DAL2Cuchulainn" and source.endswith(
        "/Modules/Shadowbringers/Foray/TheDalriada/DAL2Cuchulainn/DAL2Cuchulainn.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十实际组件；T975/CFC778、NameID10004及19唯一AID本机核表/extra0；真实48边形减双矩形场地、四个40边形常驻变形区/原始5码cell Distance、恰五Jump且Start不重置/真实TargetXZ与仅23684清、Icon40重复FIFO/原target代际/R100、60槽冷Pet与热排除/旧Party、墙行军逐段raw float及方向RefSort、重复23695动态NPC预测均有真实源对照；382相关及最终12定向PASS，墙查询AVX512/AVX2-FMA/scalar均精确源差分；生产0错4既有警告，同DLL330联合专项/1750全目录行首PASS/296独立入口，全部exit0；真实solver、反转采样拒绝不造Goal、开关/Seek/owner/回放禁执行；适配、失败及SHA见NativeDalriada.spec.md，实机待验"
    if module == "DAL04thLegionColossusRubricatus" and source.endswith(
        "/Modules/Shadowbringers/Foray/TheDalriada/DAL04thLegionColossusRubricatus.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "两实际组件；T975/CFC778、NameID9432与6唯一AID客户端核表/extra0；源三矩形union真实polygon、动态原NPC全屏诊断、两旋向六击原LocXZ锁点、跨source Effect仅消费index0/空队列计数、独立发布缓存和live R5 goal/末次Future色仍Risky/Update前后空导航边界；370相关及最终6定向真实源差分，生产0错4既有警告，同DLL316联合专项/1736全目录行首PASS/296独立入口及最终移动caster专项，全部exit0；真实Feed、solver、机制开关、Seek/owner/回放隔离；失败/适配及SHA见NativeDalriada.spec.md，实机待验"
    if module == "CE32RiseOfTheRobots" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE32RiseOfTheRobots.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/BozjaSouth.cs", "五实际组件；T920/CFC735-DE14及20唯一AID extra本机核表；Finish环与冷Created顺序、MaxHP旧Party八槽数学/20边形双规范化及顶点ULP、反转polygon原始Distance与实际旧桥采样消费、60槽冷Pet行军与独立计数、恰二Start双旋转/前Effect全序列推进/发布缓存；352相关及最终11定向真实源差分，生产0错4既有警告，同DLL294联合专项/1714全目录行首PASS/296独立入口，全部exit0；真实Feed源异常明确撤销导航且保留绘图并恢复、无Party睡眠、solver/owner/Seek/回放禁执行；适配、失败及SHA见NativeBozjaSouth.spec.md，实机待验"
    if module == "CE42FromBeyondTheGrave" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE42FromBeyondTheGrave.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "十五实际组件；T975/CFC778-DE20与32源AID extra核表；六标准AOE/安全圈、动态诊断和分散、双独立中断复用；冷死亡墙顺序、行军真实60槽/重复状态/float分段/全部圈交集仅提示和线、全类型OID原Actor球体障碍与SoulPurge双色风险FIFO；339相关及最终15定向源差分，生产0错4既有警告，同DLL285联合专项/1714全目录PASS含1705测试与9汇总/296独立入口，全部exit0；线owner显式排序及跨owner位置、solver/Seek/释放/回放禁执行，适配/失败/SHA见NativeZadnor.spec.md，实机待验"
    if module == "CE64FeelingTheBurn" and source.endswith(
        "/Modules/Shadowbringers/Foray/CriticalEngagement/CE64FeelingTheBurn.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Zadnor.cs", "八实际组件；T975/CFC778-DE18及18源AID extra核表；Icon前二FIFO/动态原Actor追踪与六击独立炮/四击Boss炮、弱点原引用残项及真实raw非Spell时钟和60槽；324相关及最终11定向源差分，生产0错4既有警告，同DLL270联合专项/1699全目录行首PASS含1690测试与9汇总/296独立入口，全部exit0；Framework唯一缓存/锁点先绘/真实solver/面向计划/owner/Seek/回放禁执行，短Total原始Feed边界及适配/失败/SHA见NativeZadnor.spec.md，实机待验"
    if module == "DRN6Queen" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeNormal/DRN6Queen/DRN6.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumNormal.cs", "十四实际组件；T936/CFC760-NameID9863及47唯一AID客户端核表；真实方场/48边Map、普通与Grouped Finish、AboveBoard冷热原钟/实际Effect、Turrets半数门槛/原引用/ID首Effect、Chess真实24槽/60状态槽/唯一准备格/实体格采样拒绝、MeansEnds共享风险原地晋升/22与11.7冷顺序、KB冷免疫分离及保留三角Goal、Doom原Actor/重复Lose/冷Pet/Destroy/角色提示均有真实源对照；314相关模型及最终8Queen定向、生产0错4既有警告、同DLL260联合专项/1689全目录PASS行含1680测试与9汇总/296独立入口，全部exit0；真实solver/ownership/Seek/owner/回放执行隔离已验；raw斜矩形与公共消费者回归通过，两轮独立复核无剩余确定差异；证据和SHA见NativeDelubrum.spec.md，实机待验"
    if module == "DRN3QueensGuard" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeNormal/DRN3QueensGuard/DRN3.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumNormal.cs", "十实际组件；T936/CFC760-NameID9838及47唯一AID客户端核表；真实起始凹场/缩圈Map、原Actor动态NPC钟及四动作倒计时、AboveBoard逐Actor冷热Created-Start-Untargetable、Turrets整数半数触发/窄矩形最近/ID首Effect/旧炮台代际、CoatOfArms预测实际位/真实60槽/重复Lose/冷Pet热过滤/ID继承/方向阈值/原DrawSink弧均有真实源对照；306相关模型、生产0错4既有警告、同DLL247联合专项/1676全目录PASS行含1667测试加9汇总/296独立入口，全部exit0；真实solver/ownership/Seek/owner/回放执行隔离已验；目标禁攻优先级只保源意图诊断，旧桥未执行，不新增PR控制；两轮独立复核无确定可达差异，证据与SHA见NativeDelubrum.spec.md，实机待验"
    if module == "DRN2Dahu" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeNormal/DRN2Dahu/DRN2.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumNormal.cs", "十一实际组件；T936/CFC760-NameID9751大兀及全部源AID本机核表；真实44点凹场地、两Charge宽度、Shockwave全局Finish FIFO原地颜色/Risky晋升、五旋转Icon/非零角/最后回调量化原点与缓存/R5补集，HotCharge查询与Effect共计数weight5只诊断；Howl原caster链式墙距含共线0命中/真实hitbox及claws单Union直接拆或整采样/NaN忽略，Heat原source-target代际/Dead-Update边界/Effect首source及raw头钟；253相关模型、生产0错4既有警告、同DLL211联合专项/1639全目录PASS行含1630测试与9汇总/295独立入口，全部exit0；实际solver/精确ownership/owner/Seek/回放隔离；证据和SHA见NativeDelubrum.spec.md，实机待验"
    if module == "DRN4Phantom" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeNormal/DRN4Phantom/DRN4.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumNormal.cs", "六实际组件；T936/CFC760-NameID9755及全部源AID本机核表；Arena双岛/缩场与静态Polygon-Distance独立惰性原点、普通Finish/动态原NPC伤害钟、八次Loc地火及发布缓存；向前30不贴墙、危险落点只读缓存Danger圈；无线raw端点/WDir reciprocal/真实桥二次归一化及有线Intersection采样拒绝分流；262相关模型、生产0错4既有警告、同DLL218联合专项/1646全目录PASS行含1637测试与9汇总/295独立入口，全部exit0；真实solver/精确ownership/owner/Seek/回放隔离；证据和SHA见NativeDelubrum.spec.md，实机待验"
    if module == "DRS5Phantom" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeSavage/DRS5Phantom/DRS5.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumSavage.cs", "八实际组件阶段启停；T937/CFC761-NameID9755及全部源AID本机核表；真实raw Ability/Spell状态边界、CastEnd冷激活/两轮独立实例、Creeping精确Loc三Finish、两地火独立Count/version缓存、Miasma合计八条/overdue、Agony只Effect；Adds原引用/Dead-Targetable/销毁同ID新代及ActorMarker旧桥未消费；真实16项时间轴所有名称钟flags、43秒Timeout与永久???尾段不补TODO；269相关模型、生产0错4既有警告、同DLL223联合专项/1652全目录PASS行含1643测试与9汇总/296独立入口，全部exit0；实际solver/ownership/owner/Seek/回放无执行请求；证据与SHA见NativeDelubrum.spec.md，实机待验"
    if module == "DRS3Dahu" and source.endswith(
        "/Modules/Shadowbringers/Foray/DelubrumReginaeSavage/DRS3Dahu/DRS3.cs"
    ):
        return "mp_native_offline_verified", "Presets/Foray/Entries/DelubrumSavage.cs", "十一实际组件阶段启停；T937/CFC761-NameID9751及全部源AID本机核表；五标准AOE与Charge/普通Finish、Shockwave FIFO颜色、旋转回调量化原点与缓存复用；Spit原Actor图标/顺序槽与重复/Dead Update前后/ID全消费、Burn原Primary-target/8.2f首钟/FIFO/R30本人导航和诊断；两KB单项覆盖/锁原位置/2.1d与.9d/普通Finish保留/真实Effect独立计数/墙线无额外危险提示/当前Primary爪Union；完整源状态及16项时间轴差分、HeadDown六秒overdue、raw Ability、冷激活顺序/永久TODO尾段不补造；277相关模型、生产0错4既有警告、同DLL228联合专项/1657全目录PASS行含1648测试与9汇总/296独立入口，全部exit0；实际solver/精确ownership/owner/Seek/回放无执行请求，独立复核无新可达差异；证据与SHA见NativeDelubrum.spec.md，实机待验"
    hydatos = {name: name for name in ("Ceto", "Daphne", "Molech", "Ovni", "ProvenanceWatcher", "Tristitia")}
    hydatos["Phuabo"] = "Tristitia"
    if module in hydatos and source.endswith(f"/Modules/Stormblood/Foray/Hydatos/{hydatos[module]}.cs"):
        return "mp_native_offline_verified", "Presets/Foray/Entries/Hydatos.cs", "七源50唯一组件/51激活；T827/CFC639与51动作extra本机核表；Loaded/Pending/原Actor/60真实槽/冷Pet热过滤/旧Party免疫/单体动态NPC钟；真实RotationInt录制读回/图标主目标FIFO/DualCast隐藏晋升/凝视切视角；按七OID精确禁桥不吞BA，分摊稳定句柄/真实solver/回放禁执行/owner；32相关模型、生产0错4既有警告、同DLL21专项和无筛选全目录1356PASS/0FAIL、279入口独立编译，SHA256及FFCS来源限制见NativeHydatos.spec.md；二值goal与驱散仅诊断，拉拽仅显示，不新增治疗或位移执行，实机待验"
    crescent = {
        "TheWingedTerror": "TheWingedTerror", "CrescentGremlin": "CrescentGremlin",
        "CrescentFan": "CrescentFan", "EyeToEye": "EyeToEye", "DemiMedusa": "ScaleModel",
    }
    if module in crescent and source.endswith(
        f"/Modules/Dawntrail/Foray/FATE/{crescent[module]}.cs"
    ):
        return "mp_native_offline_verified", f"Presets/Fates/Crescent/Entries/{module}.cs", "五源十三实际组件固定manifest；T1252/1346-CFC1018/1093及五FATE本机核表；原始ActiveFate完整/增量/旧null诊断、保护罐4D8C与BigFan真实消失、原NPC/代际/raw watched Finish/99分组排序/30与50凝视/旧Party动态预测；首帧pending逆序、同ID重绑/Party0换代/500码远距退回已模型及真Manager差分；用户余量只应用一次/真实solver/默认启用/精确禁桥/Seek/owner；26相关模型加7下一批模型、生产0错4既有警告、同DLL全目录1337条PASS/0FAIL且278入口独立编译，指纹与历史失败见NativeCrescentFates.spec.md；实机待验，无新增动作/治疗/位移执行"
    crescent_variants = {name: name for name in (
        "ARottenAffair", "ADelicateBalance", "KingOfTheCrescent", "AllureOfTheOccult",
        "AnUnendingDuty", "GaleForceEncounter", "Thunderregnum", "APryingEye", "ServingDarkness",
    )}
    crescent_variants.update(MadMudarch="SwornToSoil", RelicIcewolf="NH110RelicIcewolf")
    if module in crescent_variants and source.endswith(
        f"/Modules/Dawntrail/Foray/FATE/{crescent_variants[module]}.cs"
    ):
        return "mp_native_offline_verified", f"Presets/Fates/Crescent/Entries/{module}.cs", "十一源56激活组件(44普通/12变体)；95规则动作及142源AID/两组CFC-Territory/FATE名称本机核表；2247普通事件边界/150984几何点与真实组件差分，Created/Growth真60槽冷热/Concentric跨来源Finish/Freefall非危险FIFO/Storm响应历史/时窗风险三态颜色/动态双cone/Charge原参数；事件扫描不退休晋升，同cast EffectSeen累积不重启钟；弱goal5按旧桥100门槛不扩导航；56相关模型、生产0错4既有警告、72专项及最终同DLL无筛选1396PASS/0FAIL、290入口独立编译；真实solver/稳定句柄/Seek/owner/回放禁执行，最终指纹见NativeCrescentFates.spec.md；实机待验，不新增动作治疗或位移执行"
    if source.endswith("/Modules/Stormblood/Raid/O1NAlteRoite/O1NAlteRoite.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/AlteRoiteNormal.cs", "十二实际组件；CFC252/T691双向本机核表；普通AOE和三独立20码击退、911冰面精确墙裁剪、火球driver优先级/实际锁钟/5秒显示/原Actor惰性缓存顺序、龙卷风raw主怪及未量化中心过滤、重复Source图标/独立计数/死亡spread Update；真实源组件逐事件差分含18冰面Convert边界和18伤害时钟身份边界，补冷激活外圈和非施法首次缓存反例；35相关模型及最终8定向、生产0错4既有警告、同DLL13专项及无筛选1428PASS/0FAIL、292入口独立编译，全部exit0；自己stack R3与ThinIce保留实际旧转换拒绝边界，未点名R6真实solver无fallback，Seek/owner/回放禁执行；指纹与失败证据见NativeAlteRoiteNormal.spec.md，实机待验"
    if source.endswith("/Modules/Stormblood/Raid/O3NHalicarnassus/O3NHalicarnassus.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/HalicarnassusNormal.cs", "八实际组件；CFC254/T693双向本机核表；六普通watched raw Finish与两独立绝对棋盘，真实ClassJob动态Role/前16/重复追加/Native Spell跨来源清全；原Actor代际/旧Party0/Pending逆序/500三维/缺源玩家冻结，未开场HP0与即时Dead加载、Director0005至0011屏障；85真实源事件职业边界/851550几何点精确差分，52相关模型及最终5定向，生产0错4既有警告，同DLL无筛选1408PASS/0FAIL、291入口独立编译exit0；真实即时solver/非Party撤导航/逆Seek/owner/回放禁执行，指纹及中间失败见NativeHalicarnassusNormal.spec.md；实机待验"
    if source.endswith("/Modules/Dawntrail/Raid/M04NWickedThunder/M04NWickedThunder.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/WickedThunderNormal.cs", "十三实际组件；T1231/CFC991双向本机核表；真实60槽/旧Party/冷热激活/原Actor引用、两独立分摊/完整Effect FIFO、Hunt14与Side三来源隐藏晋升/颜色Risky、raw watched Finish及实际BattleEnded/Director；真实源组件40边界精确尺寸/时钟/微角差分，58相关模型、生产0错4既有警告、同DLL64专项及1028全目录PASS/0FAIL且exit0，指纹见NativeWickedThunderNormal.spec.md和native-wickedthunder-deepzones-20261126-*；无新增治疗/位移执行，实机待验"
    if source.endswith("/Modules/Endwalker/DeepDungeon/EurekaOrthos/EOFloorModule.cs") and module in {f"EO{n}" for n in range(10, 101, 10)}:
        return "mp_native_offline_verified", "Presets/EurekaOrthos/Entries/EurekaFloors.cs", "仅十Zone授权战斗机制：七动态凝视/四独立donut/逃离/两位移模型与真实Finish Spikes；原Actor/60槽/旧Party/跨动作替换/完整Native及legacy双向收据；24源LOS位图331688点oracle和十组墙陷阱索引，保留MasterPlayer.EurekaLOS资源；首次默认启用/显式关闭/十区禁桥/Boss交接/联合面向/真实solver/Seek/owner；58相关模型、生产0错4既有警告、同DLL64专项与1028全目录PASS/0FAIL且exit0，指纹见NativeEurekaOrthosBosses.spec.md；旧槽复用Spikes源缺陷不复制，不新增探索/箱/机关/道具/推进或自动技能，实机待验"
    if source.endswith("/Modules/Dawntrail/DeepDungeon/PilgrimsTraverse/PTFloorModule.cs") and module in {f"PT{n}" for n in range(10, 101, 10)}:
        return "mp_native_offline_verified", "Presets/PilgrimsTraverse/Entries/PilgrimsFloors.cs", "仅十Zone授权战斗机制：精确270环扇/独立donut/持续效果与三Heel计数/Turtle末秒硬导航/拉近逃离/真实Finish Spikes及原60槽免疫；无源墙陷阱表不借死宫表；九首击保留通用Dodge，实际DLL实时订阅经Manager/Host门控与Native先行效果同时注册两路，三零序号Heel包及目标去重/功能门控/owner已验；默认启用和十区禁桥/Boss交接，58相关模型/生产0错4既有警告/64专项/1028全目录PASS且exit0，见PilgrimsTraverseBosses.spec.md及native-wickedthunder-deepzones-20261126-*；联合夹具仅替代世界采样，无流程自动化，实机待验"
    if source.endswith("/Modules/Stormblood/DeepDungeon/HeavenOnHigh/HoHFloorModule.cs") and module in {f"HoH{n}" for n in range(10, 101, 10)}:
        return "mp_native_offline_verified", "Presets/HeavenOnHigh/Entries/HeavenOnHighFloors.cs", "仅十Zone授权机制：七动态凝视/两独立donut/普通VFX25原目标引用与NPC钟/Source0 HintDisabled、真实raw结束及非空替换、packet首见nullable初始化/冷启与热Created/7248保收据；500房间4432陷阱源表与墙符号oracle、双视图同状态/联合PR面向/真实solver脱陷阱/九Boss即时交接/十区禁桥。38相关模型、生产0错4既有警告、16条专项含2公共与988全目录PASS/0FAIL且exit0，见NativeHeavenOnHighBosses.spec.md及native-hoh-zones-20261125-*。interrupt/stun只意图，不新增动作；无探索/主动清怪/箱/机关/道具/层间推进，真实宿主及像素待实机"
    if source.endswith("/Modules/Heavensward/Extreme/Ext3Thordan/Ex3Thordan.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/ThordanExtreme.cs", "34真实激活组件，T448/CFC91/OID1453本机核表；三phase/335state/134预期AID及有序EnterExit源oracle，凝视联合PR面向、原引用bait/盾标记不扩大禁攻、Heavy四段/隐藏预测Thrust/动态Pierce、Faith16与Holy精确导航；36相关模型、生产0错4既有警告、11实际DLL专项与974全目录PASS/0FAIL，Faith28227/双场各26244点及真实solver/Seek/owner，DLL指纹见NativeThordanExtreme.spec.md与native-thordan-20261125-*；真实宿主物理像素待验"
    if source.endswith("/Modules/Stormblood/Raid/O8NKefka/O8NKefka.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/KefkaNormal.cs", "14真实组件，T751/CFC289/OID2153本机核对；四共享AOE任意Finish/独立Wave、原引用R5/R6分散与exact8、F+3.1持久Ruin、径向10/固定15及R19落点硬导航、正反10000凝视联合PR面向；raw动态钟/60状态槽/旧Party/Pet冷热/缺Actor预测已验；119集中模型及Pet单跑1、生产0错4既有警告、46相关实际DLL专项与948全目录PASS/0FAIL，78732位移和169362文本源oracle/真实solver/Seek/owner；真实宿主物理像素待验，见NativeKefkaNormal.spec.md及native-kefka-20261125-*-05.log"
    if source.endswith("/Modules/Dawntrail/Quantum/Q1FinalVerse/Q1FinalVerse.cs"):
        return "bmr_pending", "Presets/Dungeons/Entries/QuantumFinalVerse.cs", "九实际机制组件已原生实现；T1311-CFC1063/OID48EE本机核表，源Q15-35与Q40后半WIP不扩实现，AutoPotion禁止迁移；17模型与生产0错4既有警告及实际DLL连续地火/Map/重复Source图标/VFX/真实solver/Seek/owner已验，场地205200点及双色各23852点真实源oracle；预战与非Party视角仅显示不导航。缺前置方向历史的冷激活保留零方向线及诊断，不物化异常巨量预告，定向显示/导航仍待输入证据，故不登记已完成；详见NativeQuantumFinalVerse.spec.md"
    if "/Modules/Endwalker/DeepDungeon/EurekaOrthos/" in source:
        for prefix, entry in [("DD10", "EurekaGancanagh"), ("DD20", "EurekaCloningNode")]:
            if Path(source).stem.startswith(prefix):
                return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅Boss机制，禁止深宫道中自动化；T1099/1100-CFC897/898本机核表；五激活组件，地雷无限存储插入前六/隐藏晋升及R20实参，完整32552重复及零目标包消费钟加9点6/FIFO真实32864 Finish/严格首钟一秒前缀，原姿态及源float投影量化；首次raw权威无假Finish/迟packet不回滚/缺视角冷激活/Director卸loaded保pending/旧Actor不复活；798集中模型含三旧夹具修钟后单跑，生产0错4既有警告，实际DLL全目录902PASS和专项源场各6724点/角度72例/真实解算/Seek/owner；仅源显示与普通导航，无玩家传送或新执行，实机待验；见NativeEurekaOrthosBosses.spec.md"
        for prefix, entry in [("DD30", "EurekaTiamat"), ("DD40", "EurekaTwintania")]:
            if Path(source).stem.startswith(prefix):
                return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅Boss机制，禁止深宫道中自动化；T1101/1102-CFC899/900本机核表；13激活组件，五击原目标引用/原Cast/每完整效果包消费及四头移动窗口，双独立Twister实际Update与提前Finish/Created代际和索引交错，风池三路及单帧首匹配，Timeline锁当前姿态/真实Finish，Turbine原位移与交集导航；已知Player/Pet/Chocobo/Buddy及非Spell排除，旧录像未知保持未知；30定向模型/生产0错4既有警告/实际DLL14专项和907全目录PASS，场各6724点及5043源Distance对照/真实solver/Seek/owner；扩大模型另有失败独立处理，不据此声称全量通过；真实宿主像素待验，见NativeEurekaOrthosBosses.spec.md"
        for prefix, entry in [("DD50", "EurekaChimera"), ("DD60", "EurekaMinotaur"), ("DD70", "EurekaAeturna")]:
            if Path(source).stem.startswith(prefix):
                return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅Boss战斗机制，禁止深宫道中自动化；T1103/1104/1105与CFC901/902/903本机双向核表；21激活组件，双Voice队列与Model先于Cast、原引用冲锋/球及双Stretch、旧Party输入时序、八连FIFO/出生Shock与Groan15模型及免疫外导航、Shatter惰性创建序锁点/闩锁/全清、冷回溯顺序及SteelClaw历史主目标FIFO；59定向模型、生产0错4既有警告、同DLL21专项及914全目录PASS/0FAIL，每场6724源场点及真实solver/缺视角/Seek/owner撤销；仅源实际显示/导航，无新增自动动作，真实物理/宿主执行和像素待验；见NativeEurekaOrthosBosses.spec.md"
        for prefix, entry in [("DD80", "EurekaProtoKaliya"), ("DD90", "EurekaAdministrator"), ("DD99", "EurekaExcalibur")]:
            if Path(source).stem.startswith(prefix):
                return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅Boss战斗机制，禁止深宫道中自动化；T1106/1107/1108与CFC904/905/906核表；25激活组件，真实状态槽/旧Party/冷补播与Pet门控，Barofield真孔及源顶点边界差异如实保留、磁力10秒锁点和Distance OR导航；90Source图标恰6排序/连续前缀与颜色、隐藏线独立FIFO；99原短刀状态快照/双完整安全带补集四多边形及退化片修复、属性槽mask、Thermal双侧与Effect-only Cross FIFO；87集中模型、生产0错4既有警告、同DLL32专项及925全目录PASS/0FAIL，源Distance26244/19683/19109点及实际solver无fallback、双视图/Seek/owner已验；真实物理/宿主执行和像素待验；见NativeEurekaOrthosBosses.spec.md"
    hoh_bosses = {"DD10Mojabune": 10, "DD20Beccho": 20, "DD30Hiruko": 30, "DD40Bhima": 40, "DD50Gozu": 50,
                  "DD60Suikazura": 60, "DD70Kenko": 70, "DD80Kajigakaka": 80, "DD90Onra": 90}
    if any(source.endswith(f"/Modules/Stormblood/DeepDungeon/{name}.cs") for name in hoh_bosses):
        floor = next(floor for name, floor in hoh_bosses.items() if source.endswith(f"/{name}.cs"))
        if floor == 70:
            return "mp_native_offline_verified", "Presets/HeavenOnHigh/Entries/HeavenOnHigh70.cs", "七激活组件五读条及12207/12204真实native效果；T774-CFC546/Name7489本机核表，5772源场点；双独立池捕获Icon目标槽/default与处理钟+7点2/首实体State7/Ululation真实Finish，R3软Goal与R3分散显示R4导航；Devour与Hound独立原引用/时钟/结束，完整组件目标导航门控/首存储预测钟/R100诱导，首次raw合成Finish前捕获及已消费防复活；453集中模型893全目录PASS，生产0错4既有警告，源显示及实际导航/Seek/owner已验，池内安全提示文字与风险模型保留但脚本日志无旧桥风险着色，楼层Zone仍待迁/真实宿主像素待验"
        if floor in (60, 80, 90):
            return "mp_native_offline_verified", f"Presets/HeavenOnHigh/Entries/HeavenOnHigh{floor}.cs", "三Boss17组件16读条输入；本机T783/784/775-CFC545/547/548及中文Name独立查表；源场每场5772点/90独立半径25差集，60创建7点9环仅11930真实Finish首source消费、80创建8点5冰球仅11992 native效果、90创建3点2沙球逐轴首项三Finish/销毁先Finish后remove及缺席先于同帧新生；无限容量/冷激活/原钟/无超时/死亡销毁/同ID已验；447集中模型38天宫专项886全目录PASS，生产0错误4既有警告，显示与实际导航窗口及Raidwide仅预测；保楼层Zone未计迁移，真实宿主像素待验"
        return "mp_native_offline_verified", f"Presets/HeavenOnHigh/Entries/HeavenOnHigh{floor}.cs", "五Boss共20组件18规则；真实区域770/771/772/782/773与CFC540-544独立核表；精确双场每场5772源点，30径向KB忽略免疫/连续撞墙源ray对照仅显示，默认10000凝视PR模型，分散R6显示R7导航/目标FIFO销毁冻结，原位置R6/R8硬障碍及文字；输入钟/晚NPCcap/同代packet/旧Cancel水位/raw基线销毁/脱战保持已补；444集中模型24实际DLL专项876全目录PASS，生产0错误4既有警告；五真实Zone会话不重建仍待迁，真实宿主执行和像素待验"
    if source.endswith("/Modules/Dawntrail/Quest/Role/BarThePassage/P3Apyaahi.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/BarThePassageP2.cs", "十一激活组件/十二规则；惰性首4670战斗拉战、466A默认死亡HP0销毁完成；Power全局FIFO与sticky风险、九击地火Finish首匹配/原位置方向与缓存时钟、独立R6分摊分散和旧引用、径向13连续落点/严格免疫/首R6即时Goal，双场20及64边形7442点源oracle与精确Map环F+.5及F+2；206集中模型/生产0错4既有警告/实际DLL全目录633PASS，公共采样拒绝边界/动态float饱和钟与首raw elapsed补正/Seek/owner已验，真实宿主执行待实机"
    if source.endswith("/Modules/Dawntrail/Quest/Role/BarThePassage/P1Trash.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/BarThePassageP2.cs", "六组件/七规则；1246-CFC1016，友方465D可选战斗拉战/独立CFC与可选性完成；84及17原引用/直接清槽/首中央4664和惰性首465D目标/F+1.1钟及priority1矩形Goal；loaded-pending顺序/同OID占位/后台阶段历史与缺席Primary交接；249集中模型/生产0错4既有警告/实际DLL628PASS、显示普通导航Seek与owner已验，P3已另完成原生迁移，真实执行待实机"
    if source.endswith("/Modules/Dawntrail/Quest/Role/BarThePassage/P2Trash.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/BarThePassageP2.cs", "五组件/三普通规则；友方4660独立阶段、Native40866原pose/default安全锥priority1Goal、40907及40908共享恰第五包清除/零序号时钟收据/后建盾不重置次数；精确Map替换两矩形23664点对照、后台盾次数场地/Director只卸Loaded保Pending；249集中模型/生产0错4既有警告/实际DLL628PASS，显示导航Seek与owner已验，旧无Native录像效果及实机执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage07AChorusSlime/Stage07Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "三提示及显示组件；CFC617-Instance35007-T796、本机表非反向CFC识别；2703固定出生引用R7.6动态黄色轮廓/圈内提示/准备到战斗Name文字，仅显示无导航动作，2703及2702群体拉战全DeadOrDestroyed完成；共用Loaded/Pending冷倒序/旧Party0直接加载/500码3D/同OID占位/wipe保Pending与无出生复活不重建；215集中模型/生产0错4既有警告/实际DLL全目录641PASS，Seek/机制停用/owner已验，真实像素宿主待实机"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage07AChorusSlime/Stage07Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两构造组件；2705全实例原引用R7.6黄色轮廓/提示无导航、Death删除/Destroyed冻结/同ID新生分代/复活不重入；逐模块构造历史含Pending晋升前驱销毁旧圆，死Pending模块永久丢弃到新出生；Layout4Quads差集Round内缩0.5的64外顶点四24点孔及166三角形，生产48847点源oracle含边缘；215集中模型/生产0错4既有警告/实际DLL全目录641PASS，预战战斗场地/显示/Seek/owner已验，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage08BombedyOfErrors/Stage08Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "三提示及显示组件；CFC618-Instance35008-T796本机表、2708固定R10及2709/270A原引用R6黄色轮廓/Death删除Destroyed冻结无导航动作；全候选2708优先级1其他0保留Pointless不可覆盖，准备到战斗提示，下一幕打断建议不冒充本幕意图；共用Loaded/Pending及群体DeadOrDestroyed；215集中模型/生产0错4既有警告/实际DLL全目录641PASS，Seek/机制停用/owner已验，Act2继续保桥，真实宿主像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage04GentlemanPreferSwords/Stage04Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "五实际组件：Primary默认可选中拉战/HP0死亡销毁结束、两阶段Name提示、77.5前长全宽4及R6锁定AOE、14369完整caster引用/代际/Enemy打断意图无自动技能、甲虫1其他0保Pointless及原红色markers；共用切展示Unselect保后台取消收据/packet宽限、同代补正与raw真替换、旧Finish拒绝；218共用模型/0错4既有警告/实际DLL全目录653PASS，显示导航Seekowner已验，游戏宿主像素执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage08BombedyOfErrors/Stage08Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "四实际组件：Hints与270C/270D原引用R6黄色轮廓持续、Death删除Destroyed冻结同ID分代不导航；14708 R8与14680独立打断意图，群体拉战全DeadOrDestroyed、270B1其他0保Pointless；Layout2Corners四墙差集Round内缩保89.98度、64外39/33孔137三角，547组XZ位值一致/生产48851点oracle；218共用模型/0错4既有警告/实际DLL全目录653PASS，未增自动打断/引爆/移动，真实像素宿主待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage10ALittleKnightMusic/Stage10.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "十实际组件：构造Name攻略与成就提示开战整体撤、默认Primary可选中拉战/HP0死亡销毁结束、四独立R9/9.5/10/10.5总120度扇及14722 R6；14719/20/21/24四独立Interrupt组件保存原caster代际/时间及Enemy投影，既有MP无执行消费者不增自动技能，14723枚举不消费；后台切换取消收据packet宽限/同代补正raw换代/迟旧Finish/Seek关停owner；218共用模型/0错4既有警告/实际DLL全目录653PASS，宿主与实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage06EyeSociety/Stage06Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "06两幕及07第三幕共12组件8逐幕规则：SID571四独立mask及source Raid0不随PR排序，构造Pending/旧roster与开战新roster分离；默认10000真凝视及精确两90度普通锥、R6点3目标FIFO/销毁冻结/补正不复活；Voltage锁Actor起点复杂视线99秒风险与Slime R7点6仅轮廓；244集中模型24专项854全目录PASS/三场地各5694点及8源点各7569点含真实世界填充；真实宿主像素执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage06EyeSociety/Stage06Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "06两幕及07第三幕共12组件8逐幕规则：SID571四独立mask及source Raid0不随PR排序，构造Pending/旧roster与开战新roster分离；默认10000真凝视及精确两90度普通锥、R6点3目标FIFO/销毁冻结/补正不复活；Voltage锁Actor起点复杂视线99秒风险与Slime R7点6仅轮廓；244集中模型24专项854全目录PASS/三场地各5694点及8源点各7569点含真实世界填充；真实宿主像素执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage07AChorusSlime/Stage07Act3.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "06两幕及07第三幕共12组件8逐幕规则：SID571四独立mask及source Raid0不随PR排序，构造Pending/旧roster与开战新roster分离；默认10000真凝视及精确两90度普通锥、R6点3目标FIFO/销毁冻结/补正不复活；Voltage锁Actor起点复杂视线99秒风险与Slime R7点6仅轮廓；244集中模型24专项854全目录PASS/三场地各5694点及8源点各7569点含真实世界填充；真实宿主像素执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage02MuchAdoAboutPudding/Stage02Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "02两幕及03/09共14组件7逐幕规则：纯Interrupt/精确R14总120锥/R5普通与R4三路独立池/R6三路及float1点2和double1效果钟；群体与默认Primary分离/六橙adds无条件priority含PendingDead；晚owner补播/五动作饱和raw及未claim取消墓碑；227模型14专项832全目录PASS/每幕5772源场地点；真实宿主与像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage02MuchAdoAboutPudding/Stage02Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "02两幕及03/09共14组件7逐幕规则：纯Interrupt/精确R14总120锥/R5普通与R4三路独立池/R6三路及float1点2和double1效果钟；群体与默认Primary分离/六橙adds无条件priority含PendingDead；晚owner补播/五动作饱和raw及未claim取消墓碑；227模型14专项832全目录PASS/每幕5772源场地点；真实宿主与像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage03WaitingForGolem/Stage03.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "02两幕及03/09共14组件7逐幕规则：纯Interrupt/精确R14总120锥/R5普通与R4三路独立池/R6三路及float1点2和double1效果钟；群体与默认Primary分离/六橙adds无条件priority含PendingDead；晚owner补播/五动作饱和raw及未claim取消墓碑；227模型14专项832全目录PASS/每幕5772源场地点；真实宿主与像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage09ToKillAMockingslime/Stage09.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "02两幕及03/09共14组件7逐幕规则：纯Interrupt/精确R14总120锥/R5普通与R4三路独立池/R6三路及float1点2和double1效果钟；群体与默认Primary分离/六橙adds无条件priority含PendingDead；晚owner补播/五动作饱和raw及未claim取消墓碑；227模型14专项832全目录PASS/每幕5772源场地点；真实宿主与像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage31AnythingGogos/Stage31Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两幕22组件25逐幕规则及23127原生效果；独立双Meteor队列/Fire全局Blizzard5点1/Icestorm点9真实清理；Pyretic64逐actor先cast后status；三路Thunder/全状态原位置Maelstrom硬障碍；215模型/21专项/820全目录PASS/每幕5772源场地点，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage31AnythingGogos/Stage31Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两幕22组件25逐幕规则及23127原生效果；独立双Meteor队列/Fire全局Blizzard5点1/Icestorm点9真实清理；Pyretic64逐actor先cast后status；三路Thunder/全状态原位置Maelstrom硬障碍；215模型/21专项/820全目录PASS/每幕5772源场地点，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage32AGoldenOpportunity/Stage32Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两幕29组件29逐幕规则及两真实效果；三段Quake消费钟1点5/四路Beam交集异或并集洞与自接触填充/三独立AwayPull仅显示/双延迟全屏/Rupture首caster动态倒计时；215模型/21专项/820全目录PASS/Beam30240格点288边侧/每幕5772源场地点，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage32AGoldenOpportunity/Stage32Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两幕29组件29逐幕规则及两真实效果；三段Quake消费钟1点5/四路Beam交集异或并集洞与自接触填充/三独立AwayPull仅显示/双延迟全屏/Rupture首caster动态倒计时；215模型/21专项/820全目录PASS/Beam30240格点288边侧/每幕5772源场地点，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage29RedFraughtAndBlue/Stage29Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两幕25组件20逐幕规则；R39总30与Pillar共享队列任意Finish；Forward50独立Away25完整击退模型仅显示；Ferrofluid全局6吸斥/真实Finish仅清KB/原生效果清AOE；Pyretic64要求/任意960Lose清全/同帧逐actor先cast后status；纯提示保持旧MP消费；190模型/19专项/801全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage29RedFraughtAndBlue/Stage29Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两幕25组件20逐幕规则；R39总30与Pillar共享队列任意Finish；Forward50独立Away25完整击退模型仅显示；Ferrofluid全局6吸斥/真实Finish仅清KB/原生效果清AOE；Pyretic64要求/任意960Lose清全/同帧逐actor先cast后status；纯提示保持旧MP消费；190模型/19专项/801全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage30TheCatchOfTheSiegfried/Stage30Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "三幕29组件30逐幕规则；Bomb8点4与Rubber20六点3出生模型冻结代际/同帧FinishCreate原序/锁原始Primary；BoulderR10原生效果全清；HyperTorchSpark独立队列/8排序/严格1秒前缀；SwiftAway10仅显示/三路R6火池source门控；完整旧Party缺席重现免疫；190模型/19专项/801全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage30TheCatchOfTheSiegfried/Stage30Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "三幕29组件30逐幕规则；Bomb8点4与Rubber20六点3出生模型冻结代际/同帧FinishCreate原序/锁原始Primary；BoulderR10原生效果全清；HyperTorchSpark独立队列/8排序/严格1秒前缀；SwiftAway10仅显示/三路R6火池source门控；完整旧Party缺席重现免疫；190模型/19专项/801全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage30TheCatchOfTheSiegfried/Stage30Act3.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "三幕29组件30逐幕规则；Bomb8点4与Rubber20六点3出生模型冻结代际/同帧FinishCreate原序/锁原始Primary；BoulderR10原生效果全清；HyperTorchSpark独立队列/8排序/严格1秒前缀；SwiftAway10仅显示/三路R6火池source门控；完整旧Party缺席重现免疫；190模型/19专项/801全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage27LockUpYourSnorters/Stage27.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "6组件2读条/3效果；Fireball R8普通导航；Snort15撞墙独立击退与取消后炸弹投影窗口，真实模型变化含活跃Create非default追加重复Actor引用/共享6d钟/原生多目标包只删首项/Destroy旧代冻结；恰8Bomb触发Fungah5d或5.3d独立标志/免疫按旧名单槽回调/仅显示，ME首引用攻略与最新Create35秒钟；166模型/12专项/784全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage28DangerousWhenDead/Stage28.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "9组件10读条/6状态；R10与三动作共享R6队列/99排序/任意Finish/迟旧代保护，raidwide64存活槽raw钟/纯打断治疗召唤提示；量化1EA9FA池并集补集反转/原始绿显/19055单全局钟/零池不导航；真实逐槽sourceParamexpiry差分/重复Gain/首方向Lose，6.6速两秒连续强制行进仅显示/最终Bilrost或出界提示；166模型/12专项/784全目录PASS/5772点源场地，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage25DirtyRottenAzulmagia/Stage25Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "9组件7规则；保留源51.2长90宽矩形/36.2总120锥/R8/R7.2及6-30/10-20/20-30独立环，真实Finish不按效果超时；1307反伤910Doom完整原始槽仅文字/成就提示，不增动作限制；147集中模型/774全目录PASS/5772点源oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage25DirtyRottenAzulmagia/Stage25Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "6组件4规则；51.2长8宽矩形/36.2总120锥/R8/6-30环，反伤556个人910只文字；首!Dead Angon提示/橙标记，全部候选priority1 adds其余0保Pointless不禁Boss；无Burn预测/技能执行；147模型774全目录PASS/5772点源oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage25DirtyRottenAzulmagia/Stage25Act3.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "14组件10规则；9普通AOE及Web全局bool任意Finish/同代补正不重开；Meteor R15锁点与R10动态cast/1.2d处理钟effect/量化live三路独立，state7过滤逐实体原始位置首匹配无超时；2681全实体R8硬障碍，556优先1307/Doom原始槽无自动Loom；147模型774全目录/5772点oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage26PapaMia/Stage26Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "5组件3规则；R3锁点Gust/18686纯CastHint/18688独立打断意图，63/269完整原始状态槽只提示不自动驱散；默认Primary生命周期与标记，无增益禁攻；147模型/11项身份复核后定向PASS/774全目录/5772点源oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage26PapaMia/Stage26Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "8组件4规则；R4圈及纯提示/动态Raidwide和SingleTarget，原始1797/271状态提示；2C59 state!=7原始R8硬障碍含dead；icon23处理钟5d覆盖动态DirForward15同点+Z/三类别严格免疫/无超时/现存source18605清除，Primary同ID重绑保钟，KB仅显示无新增导航执行；147模型774全目录/5772点oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage23BehemothsAndBroomsticks/Stage23.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "6组件4规则；R6/R10及55.8总60锥、动态Meteor原钟预测不造R50；2733全实体原始动态R8硬障碍含Dead/state7真实Destroy，未增加拉近；晚激活及激活前取消水位/反向Seek/owner；130集中模型17实际专项755全目录PASS/5772点源oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage24AmazingTechnicolorPitFiends/Stage24Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "4组件3规则；2735/2734任意InCombat拉战全DeadOrDestroyed结束HP0不代Dead；R5圆/总90锥及独立15318打断intent，固定Primary与首Viking魔物免文字不增伤害类型禁攻；全红标记/无自动技能；130模型17专项755全目录PASS/5772点源oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage24AmazingTechnicolorPitFiends/Stage24Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "7组件6规则；R5圆/锥及R6圈，15321打断intent；15319/20共享单体当前raw目标/时钟、自目标回退、dead/缺Actor名单，双免文字源均查Magus保留不扩限制；priority1 adds其余0无Pointless保护；130模型17专项755全目录/5772点oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage24AmazingTechnicolorPitFiends/Stage24Act3.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "9组件4规则；R8圆/总90锥，活273B原始R3硬障碍死藏复活无cast依赖；15325动态旧引用半宽4诱导导航与独立Start锁向10码Forward KB链/严格免疫/活电场落点、KB仅双视图不增导航；15326动态预测，add priority1/全红标记；130模型17专项755全目录/5772点oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage22HereComesTheBoom/Stage22Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "2提示组件无AOE，Source可选26FE阻挡/玩家本帧净XZ与任意可选26FC拉战，MP按统一视角自身历史，冷创建/Y/换人/同ID/静止不造移动；全DeadOrDestroyed非Primary或HP0/全部标记；116集中模型9专项740全目录PASS/5772点源场地oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage22HereComesTheBoom/Stage22Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "6组件5规则，Sap共享R8/99排序/任意Finish，11.75总120锥与7.2圆；Ignition动态NPC剩余钟/64槽预测不造R50避让；晚激活未认领raw及原0.3f延迟、后台接替/反向Seek/owner，炸弹原序!Dead提示和橙标记无自动StickyTongue，116模型9专项740全目录/5772点源oracle，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage20MissTyphon/Stage20Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "4组件2规则，大场默认Primary，14706独立R8与14704提示/30码锁Loc径向StopAtWall；原生状态三类免疫严格expiry，击退仅显示，源顶点逐边180点oracle并明确索引漏顶点差异；109集中模型、20关12条及21关9条实际DLL专项含各2公共、733全目录PASS，Seek/owner/后台时钟已验，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage20MissTyphon/Stage20Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "6唯一组件5规则，小场默认Primary，四普通圈锥及14712仅打断；重复LightningBolt激活不双圈；109集中模型、20关12条及21关9条实际DLL专项含各2公共、733全目录PASS，Seek/owner/后台时钟已验，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage20MissTyphon/Stage20Act3.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "9组件7规则，小场Primary与橙adds/2和1和0无Pointless保护；共享14706/14747队列99排序任意Finish，Wallop矩形与20码/独立30码各自链，无截墙；具名Interrupt只Start/Finish缓存，无自动技能；109集中模型、20关12条及21关9条实际DLL专项含各2公共、733全目录PASS，Seek/owner/后台时钟已验，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage21ChimeraOnAHotTinRoof/Stage21Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "4组件2规则，大场任意imp入战全DeadOrDestroyed非HP0，全部标记/持续弱火抗冰文本，R5圆与独立打断，无额外单体预测；109集中模型、20关12条及21关9条实际DLL专项含各2公共、733全目录PASS，Seek/owner/后台时钟已验，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage21ChimeraOnAHotTinRoof/Stage21Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "8组件5规则，小场默认Primary/橙imp，R5/R9/8至30环及15081三路R9，MainTarget/零seq/空Targets/消费钟0.9d/Dead实体state非7逐实体首匹配，无超时；双独立打断和!Dead imp文本；109集中模型、20关12条及21关9条实际DLL专项含各2公共、733全目录PASS，Seek/owner/后台时钟已验，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage18MidsummerNightsExplosion/Stage18Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "7组件5规则，大场群体入战和全DeadOrDestroyed非HP0；构造桶R10仅轮廓与combat独立缓存，Destroy冻结/Death按引用删/复活不加/同ID新生；动态charge原引用几何与继承导航伤害但覆写Aim文本，无KB或自动瞄桶；持久创建序bootstrap首钟反例，5929点场地源oracle，96集中模型20实际DLL专项716全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage18MidsummerNightsExplosion/Stage18Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "7组件5规则，与Act1独立两桶缓存/动态原引用bait及共享动作后台接替；画全部本幕Boss及橙桶，1/0并Pointless保护，普通R10/R6/R8与R15总90锥保真实结束，BoneShaker仅枚举；5929点源场地oracle，96模型20专项716全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage19OnAClearDayYouCanSmellForever/Stage19Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "6组件4规则，小场默认Primary生命周期/绘制；反射任意caster Start/Finish双bool锁存，仅文字无禁攻；15076打断与三路R6，真实MainTarget消费钟加double1.6f、cast动态目标/Loc、实体state非7含dead量化2码首匹配，无超时；已激活原锁钟与晚bootstrap now加0.3f分离，5236点源oracle，96模型20专项716全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage19OnAClearDayYouCanSmellForever/Stage19Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "7组件6规则，15076精确1.6d；15077仅开InkJet提示，15078只有Finish关且覆写base使Eyes为空，Blind15原生槽差分/旧roster时序/缺席身份/销毁同ID/切人，绝不误造凝视或自动动作；新模块不收激活前Reflect Finish，后台状态/事件独立，5236点源oracle，96模型20专项716全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage16SunsetBullevard/Stage16Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "仅构造Hints，任意26F2入战/全DeadOrDestroyed结束非HP0；大场全部同OID敌人标记，原攻略不新增自动风筝或自爆；5929点源场地oracle，83集中模型/19实际DLL专项/698全目录PASS，反向Seek及owner已验，实机像素执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage16SunsetBullevard/Stage16Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "8组件7规则，R12圆/R44共享60锥任意Finish/10-20环，range10000凝视完整眼瞳视线与PR面向、14686原文raw伤害钟；14873动态原引用bait与独立lastStart单槽Primary动态20码击退分开，Raid64/NPC/死缺目标预测、同ID显示与引用导航、三类别免疫真实状态槽、首raw确认/时钟漂移/null后再施法/旧Finish均验；7636点冲锋和5929点场地源oracle，83模型19专项698全目录PASS，无击退导航及自动青魔，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage17TheSwordOfMusic/Stage17Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "4组件，2720/271F群体入战及全DeadOrDestroyed结束；14759前6半宽2及14760R8总120锥，反魔法Primary与反物理首右爪保持久创建顺序，仅文字不增加禁攻或注释击退；5236点小场源oracle，83模型19专项698全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage17TheSwordOfMusic/Stage17Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "7组件5规则，前77.5半宽2/前6半宽2/R8总120及15049仅打断；15048完整原生三路R6，cast跟当前目标或Loc，真实MainTarget事件消费钟加double1.1f、零序号独立包/legacy展开跳过，Dead实体state非7量化每Update按创建序只吃首个2码预测；后台起止和事件保留即时接替，爪1/0橙色及首活反伤仅文字；5236点源场地，83模型19专项698全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage14BlobsInTheWoods/Stage14Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "3组件，群体DeadOrDestroyed与独立LastSongHint bool；Layout2Corners正式场地和64/12/8原遮挡轮廓分离，14756锁Actor位置，真实Finish撤销；NonZero多part原生视域、导航与世界全填充，后台激活/同帧交接/迟raw和取消/缺视角仍消费起止；每幕60552点源与实际世界MeshBuilder oracle，64模型/14专项/681全目录PASS，实机像素移动待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage14BlobsInTheWoods/Stage14Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "3组件，群体阶段及独立bool非列表计数；LayoutBigQuad正式内缩场地与64/4原遮挡域，锁Actor原点，maxRange0不造60圆；每幕60552点源和世界填充oracle，packet SourcePosition反例/双caster真实结束/取消代际/Seek/owner通过；纯Clipper2Lib归一非BMR桥，64模型/14专项/681全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage15TheMeNobodyNodes/Stage15.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "8组件10规则，Primary默认阶段/32边小场，14893锁Loc方向首Finish加3.6三段270扇环5.5/10.5/15.5全剩余Risky共享钟，14894-6跨source逐轴1角差0.05首匹配消费钟加0.6；视觉取消持久helper取消销毁推进，当前visual代际补正不串旧同钟序列；5普通AOE/14890仅打断、Shabti2Serpent1与橙色；20619点源oracle，64模型/14专项/681全目录PASS，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage13BeautyAndABeast/Stage13Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "构造攻略开战撤、26F5/26F6任意拉战及全部DeadOrDestroyed结束HP0不替Dead、固定Primary加全Vodoriga默认敌色无改优先级；14879 R7.4总120锁定扇与真实Finish；CFC623-Instance35013-T796本机名猩红死妖姬卡蜜拉；69模型/生产0错4既有警告/实际DLL669PASS含CircleBig5929点源oracle反Seek及owner，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage13BeautyAndABeast/Stage13Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "构造攻略及8战斗组件9AID，R5/R10组/R6/前42半宽4/10至20环；14888与14884任意Finish仍保新代时间边界；14951真实默认range10000锁Cast.Loc完整眼形瞳孔弧与PR末窗面向，15045仅Interrupt、14882原文整队伤害raw结束时钟及后台作用域；默认Primary阶段不等Succubus且橙色无改优先级；69模型/44专项/生产0错4既有警告/实际DLL669PASS含5929点小场地oracle，实机PR执行像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage11SomeLikeItExcruciatinglyHot/Stage11Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "仅一个构造Hints；2718任意同OID拉战/全DeadOrDestroyed结束HP0不替代Dead、全部同OID箭头、开战撤攻略；14583源未激活组件不造AOE伤害打断动作，仅活跃11场精确抑制该读条通用推断并隔离玩家未知动作其他act；CFC621-Instance35011-T796本机表；227共用模型/生产0错4既有警告/实际DLL659PASS，Seek关停owner已验，真实宿主像素待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage11SomeLikeItExcruciatinglyHot/Stage11Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "仅一个构造Hints，2719群体拉战全DeadOrDestroyed、HP0不替代Dead及全同OID箭头，四炸弹攻略开战撤；源14583枚举无组件不扩AOE/打断，精确通用抑制；Layout4Quads完整四孔源几何复用，预战显示/战斗正式边界独立；227共用模型/生产0错4既有警告/实际DLL659PASS含Pending/反Seek/机制关闭owner，真实像素宿主待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage12ThePlantomOfTheOpera/Stage12Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "仅一个构造Hints，IceSpikes/BombToss及下一幕攻略开战撤；271A默认Primary可选中进战及死亡销毁HP0结束不等同OID、全部271A箭头、已出生同OID候补可下一帧交接；14750仅枚举不造战斗组件；CFC622-Instance35012-T796及Name8103本机表；227共用模型/生产0错4既有警告/实际DLL659PASS，显示导航边界/Seek/owner已验实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage12ThePlantomOfTheOpera/Stage12Act2.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "五组件：构造Name攻略开战撤，14751 R16.96总120及14754 R46.96总60锁定扇，首次packet到raw锁点确认及演员转向不改锁；14752纯召唤提示、14753仅Stun完整caster代际时钟/Enemy资格保Pointless无自动技能；默认Primary结束、Roselet源Object橙色及1其他0优先级；227共用模型/生产0错4既有警告/实际DLL659PASS，真实像素宿主执行待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage04GentlemanPreferSwords/Stage04Act1.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "两提示组件/无AOE动作组件；CFC614-Instance35004-T796按本机表核对、25C8精确接管/25D2群体拉战与全DeadOrDestroyed完成、准备战斗文字/固定Primary/完整marker；64边形5183点源oracle，249集中模型/生产0错4既有警告/实际DLL628PASS，双视图/跨场尸体与旧Pending回血新生/Seek/owner已验，Act2保桥不扩打断，实机待验"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage05TheThreepennyTurtles/Stage05.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "单构造提示组件/战斗无组件；CFC615-Instance35005-T796/全部25CC任意拉战与全DeadOrDestroyed完成、真实R16.01379正32边形5183点源oracle、全部龟marker与固定Primary/旧Pending丢弃后回血不重建；249集中模型/生产0错4既有警告/实际DLL628PASS，双视图/跨场/Seek/owner已验，不增加驱散及自动动作，实机待验"
    if source.endswith("/Modules/Dawntrail/Quest/MSQ/TheWarmthOfTheFamily/AlphaAlligator.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/TheWarmthOfTheFamily.cs", "双组件/1244-CFC1009；84及17旧Actor引用/直接清槽/raw顺序与共享40562钟、首中央蜥蜴列表及优先级1窄矩形安全目标、Primary死亡保活及缓存4637独立结束；219集中模型/生产0错误4既有警告/实际DLL全目录619PASS、6643点源凹场oracle，显示普通导航Seek与owner已验，零长源NaN导航无定义不扩执行，真实执行待实机"
    if source.endswith("/Modules/Global/MaskedCarnivale/Stage01AllsWellThatStartsWell/Stage01.cs"):
        return "mp_native_offline_verified", "Presets/MaskedCarnivale/Entries/Stage01.cs", "战前Hints及IronJustice14199原生；共享796仅25BE接管/CFC610身份、R24.5正64边形5624点源oracle、任意群怪拉战/固定Primary/全DeadOrDestroyed完成、raw取消未知动作旧包及同ID新生；219集中模型/生产0错误4既有警告/实际DLL全目录619PASS，战前纯显示及战斗双视图导航Seek与owner已验，真实执行待实机"
    if source.endswith("/Modules/Global/Quest/FF15Collab/Iseultalon.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/Iseultalon.cs", "八组件/839-CFC647；两类R3塔/default钟/占员及Forbidden异常/首塔独立AI、重复Icon129旧引用、宽线原始动态姿态与消费钟、窄线危险色、销毁Finish先于同帧新塔；235集中模型/生产0错误4既有警告/实际DLL全目录616PASS、6084点源圆oracle，显示普通导航Seek与owner已验，真实执行待实机"
    if source.endswith("/Modules/Shadowbringers/Quest/Role/ToHaveLovedAndLost.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/ToHaveLovedAndLost.cs", "十一组件/十六动作、869-CFC672；六动作共享R3塔精确Loc首删除/已消费补正不复活、min1-max无限分摊、R6-R7分散、锁Loc径向10码击退不截墙严格免疫且源仅显示；235集中模型/生产0错误4既有警告/实际DLL全目录616PASS、4096点源圆oracle，显示普通导航Seek与owner已验，真实执行待实机"
    if source.endswith("/Modules/Dawntrail/Quest/Role/HeroesAndPretenders.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/HeroesAndPretenders.cs", "六激活组件；1218/CFC1001、20边形2916点源oracle、八次Flurry首逐轴匹配/实际施法者推进/三圈Risky、Gold恰二人与独立Acid导航、Arrow旧引用/同代补正及首存储预测钟；64集中模型/生产0错误4既有警告/实际DLL全目录613PASS，双视图导航Seek与owner已验，真实执行待实机"
    if source.endswith("/Modules/Stormblood/Quest/MSQ/TheTimeBetweenTheSeconds.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/TheTimeBetweenTheSeconds.cs", "双阶段六组件/五规则；685/CFC465、八顶点凹场4422点源oracle、P1可选性与P2真实列表结束、raw身份/同ID主怪重绑、ActorID0火圈锁点量化及阶段重播、迟到旧包屏障；54集中模型/生产0错误4既有警告/实际DLL全目录611PASS，双视图普通导航Seek与owner已验，真实执行待实机"
    if source.endswith("/Modules/Global/Quest/FF15Collab/MA-x.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/MAXQuest.cs", "三激活组件/三实际读条原生实现；野外141/OID252B精确接管、战前无导航场地及多实例候选、Noctis洋红与Trooper橙色、默认完成/新Create代际/wipe、FATE资格及1409禁DoT意图不扩大PR禁攻；43集中模型/生产0错误4既有警告/实际DLL全目录608PASS，双视图导航Seek与owner已验，实机待验"
    if source.endswith("/Modules/Stormblood/Quest/MSQ/HopeOnTheWaves.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/HopeOnTheWaves.cs", "八激活组件/七规则；757/CFC472区域阶段保活、真实50边旋转椭圆4623点源oracle、六AOE真实结束与独立自爆float倒计时FIFO、首Alphinaud完整Actor空槽历史、Adds全候选setter及坦克拉位意图原生实现；旧桥无自动拉位消费者不扩执行；43集中模型/生产0错误4既有警告/实际DLL全目录608PASS，双视图普通导航Seek与owner已验，任务Zone保桥，实机待验"
    if source.endswith("/Modules/Stormblood/Quest/MSQ/HisForgottenHome.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/HisForgottenHome.cs", "4激活组件/3规则；七顶点场地4292点源oracle、区域内Primary死亡及BattleEnded保活、同ID新代及旧引用、wipe屏障、Icon26独立5.1秒与任意1087Start清理、R6毒区Native主目标及cast/event/live交接、Isse目标完整priority原生实现；46集中模型/生产0错误4既有警告/实际DLL全目录594PASS，显示导航Seek与owner已验，实机待验"
    if source.endswith("/Modules/Shadowbringers/Quest/MSQ/ASleepDisturbed.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/ASleepDisturbed.cs", "8激活组件/6规则；双独立首次钟连线、Player/Buddy双向及旧引用、真实OpTether替换、raw/typed确认和销毁绕过收据、NPC分散分摊、R19.5方场及priority0原生实现；68集中模型及真实解算、生产0错误/4既有警告、实际DLL全目录588PASS，显示导航Seek与owner已验，任务交互保桥，实机待验"
    if source.endswith("/Modules/Dawntrail/Quest/MSQ/TheProtectorAndTheDestroyer/E1OtisOathbroken.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/OtisOathbroken.cs", "9激活组件/10规则；精确20边形、玩家InCombat拉战、前6锥与共享圆组、Rush前4显示/前2导航/动态黄色、99排序及原始起点补正、NPC站位原生实现；68集中模型及真实解算、生产0错误/4既有警告、实际DLL全目录588PASS，双视图Seek与owner已验，4349和任务交互保桥，实机待验"
    if source.endswith("/Global/HallOfTheNovice/NoviceTactical.cs"):
        return "mp_native_offline_verified", "Presets/HallOfTheNovice/Entries/NoviceTactical.cs", "33激活组件/28读条规则/19完整Native效果与三区域入口原生实现；独立标记/五击追踪/环FIFO/Surge与Fan前缀/双塔/57及84连线/四独立径向击退/10000凝视PR面向；raw事件收据及旧Actor冻结；78专项模型/493集中模型分批及失败单跑通过、生产0错误/实际DLL583PASS、入口226；显示导航Seek与owner已验，交互仅保留源意图不扩大旧消费，真实宿主执行待实机"
    if source.endswith("/Endwalker/FATE/Chi.cs"):
        return "mp_native_offline_verified", "Presets/Fates/Entries/Chi.cs", "六组合两段FIFO/首段导航与轴带、创建Bunker/Bomb严格风险及显示窗口、真实Finish及完整Native消费、动态原目标死刑原生实现；同钟多代补正/创建OID代际/Reset补播已验；623集中模型/生产0错误/实际DLL573PASS，双视图导航Seek与owner已验，实机待验"
    if source.endswith("/Dawntrail/FATE/MicaTheMagicalMu.cs"):
        return "mp_native_offline_verified", "Presets/Fates/Entries/MicaTheMagicalMu.cs", "五槽Draw首Actor与空槽/缓存晋升/CardTrick风险、Flourish首对交换/前三pizza/前四hoop、四Rolling原count时钟全Risky及完整Native FIFO原生实现；真实cast引用与创建代际已验；623集中模型/生产0错误/实际DLL573PASS，双视图导航Seek与owner已验，实机待验"
    if source.endswith("/Dawntrail/FATE/TheSerpentlordSeethes/Ttokrrone.cs"):
        return "mp_native_offline_verified", "Presets/Fates/Entries/Ttokrrone.cs", "八激活组件原生实现；真实48边形/4225点源oracle及marker Clamp、四Tempest/四Sandspout、六Swallow含38643、strict4.5连续球前缀、7或4击旋转真实Finish及独立float/double时钟和同count缓存；623集中模型/生产0错误/实际DLL573PASS，双视图导航Seek与owner已验，实机待验"
    if source.endswith("/Shadowbringers/FATE/Formidable.cs"):
        return "mp_native_offline_verified", "Presets/Fates/Entries/Formidable.cs", "十二组件原生实现；源本地玩家R30显示/strict轴差5更新缓存方场分离、七击真实效果消费、Source图标独立min2分摊、创建锁点充能与强制导弹标记、SteamDome15原模型仅投影、1269原始Stay诊断不扩大控制；600集中模型/生产0错误/实际DLL562PASS，双视图及既有导航/Seek/owner已验，世界圆裁剪和真实宿主执行待验"
    if source.endswith("/Shadowbringers/FATE/Archaeotania.cs"):
        return "mp_native_offline_verified", "Presets/Fates/Entries/Archaeotania.cs", "十五组件/十普通几何原生实现；严格slot0拉战/半宽29方场、Source图标独立min8分摊与Native真实主目标清理、动态R5硬障碍、Forward48原始击退模型按开始顺序连续投影且仅显示；raw/packet/未知动作交接与代际、dead caster/viewer、严格免疫及owner已验；587集中模型/生产0错误/真实DLL全目录集中回放，双视图和当前导航离线已验，真实物理与宿主执行待实机"
    if source.endswith("/Endwalker/FATE/Daivadipa.cs"):
        return "mp_native_offline_verified", "Presets/Fates/Entries/Daivadipa.cs", "十组件/六普通几何原生实现；半宽24.5方场/strict25源slot0拉战，双球队列整体Reverse/严格前缀/前8原时钟晋升，持久Actor顺序、当前CastInfo单体与+1延迟全屏Native效果、四方向2秒6.6强制行进模型/完整隐藏Lit与首半圆危险集合；587集中模型/生产0错误/真实DLL集中回放，双视图/当前导航/Seek/owner已验，行进仅显示不扩PR执行，实机待验"
    if source.endswith("/Global/HallOfTheNovice/NoviceTank04.cs"):
        return "mp_native_offline_verified", "Presets/HallOfTheNovice/Entries/NoviceTank04.cs", "单组件原生实现；首对象绑定/不可选不跳过/完成下一轮Pending逆序晋升、targetable-only拉战、全1564合法候选禁攻/Pointless保护、恰三全对象bbox弱Goal及严格距1职业null-target动作意图；弱Goal/动作不扩大旧消费边界，592集中模型/生产0错误/实际DLL557PASS，双视图/Seek/owner清理已验，真实PR执行待实机"
    if source.endswith(tuple(f"/Global/HallOfTheNovice/{name}.cs"
                             for name in ("NoviceDPS03", "NoviceDPS05", "NoviceBasicFinal"))):
        return "mp_native_offline_verified", f"Presets/HallOfTheNovice/Entries/{module}.cs", "三个单组件原生迁移；持久首角斗士指示、全部增援/同OID partial setter、Pointless与NPC队伍基础优先级、活跃模块不套auto无敌；精确R20场地与源默认Primary双端绘制、初见HP0及同ID新代；568集中模型/生产0错误/真实DLL全目录541PASS，入口216，Seek/开关/owner清理已验；无新增禁攻/停手或特殊移动，PR真实选敌及最终像素待实机"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/VaultOneiron/{name}.cs"
                             for name in ("VaultYan", "Hexapod"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "十五组件/十六规则原生实现；Yan袋塔坐标FIFO/创建顺序/跨OID旧帧屏障及Rush锁原点和长度补正；Hex正负Pole/矿硬障碍/状态参数与真实Lose/逐Actor首次顺序/Bomb死亡复活/强制行进模型；436集中模型、生产0错误、实际DLL536PASS及最终专项，双视图/当前导航/Seek/owner已验，特殊移动未扩执行，实机待验"
    if source.endswith(("/Dawntrail/Hunt/RankS/ArchAethereater.cs", "/Dawntrail/Hunt/RankS/Sansheya.cs", "/Dawntrail/Hunt/RankA/UrnaVariabilis.cs")):
        return "mp_native_offline_verified", "Presets/Hunts/Entries/SimpleHunts.cs", "十九组件/四十三读条及六Effect原生规则；Arch全局Finish双段FIFO/独立冷热、Sans前二引用Risky三段真实效果FIFO/动态冲锋、Urna正负磁力动态主怪与静态AOE/独立结束/专用导航；原始状态槽序和队伍帧序/packet收据/销毁代际已验；436集中模型、生产0错误、实际DLL536PASS及最终专项，双视图/导航/Seek/owner清理，StayMove诊断与磁力投影不扩执行，实机待验"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/VaultOneiron/{name}.cs"
                             for name in ("OldBitterEye", "MobileFortress"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "二十三激活组件/三十六消费规则原生实现；Bitter全局预告/精确中心/旧读条剩余时间封顶与7.1延迟、50拉近min4.9仅显示及免疫；Fortress三套队列恰8扩展/IV复位、Quiet精确3.5与方向AND前缀、Systematic仅16排序及单向Risky/Danger/全局Finish；两动态矩形真实Native效果消费及分散6/7、奖励优先级；406集中模型/生产0错误/实际DLL全目录520PASS，双视图/当前导航/Seek/owner清理离线已验，实机待验"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/VaultOneiron/{name}.cs"
                             for name in ("Paeonia", "SpiritOfThunder"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "十九激活组件/三十五消费规则原生实现；Paeonia独立正反双环/源锁点与消费钟2、八项前四/默认10000凝视PR面向；Spirit普通八项前四及高压四项前三独立、首黄全Risky、六创建beam原量化位置40x10/+7.7与任意Finish全清；动态目标圈6/Finish-only、两类分散及独立目标/类型/判定伤害预测、源奖励R7；398集中模型/生产0错误/实际DLL全目录511PASS，后续520集中补强预测类型目标时钟和Native效果保留，双视图/当前导航/Seek/owner清理已验，实机待验"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/CenoteJaJaGural/{name}.cs"
                             for name in ("GoldenMolter", "Room1"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/CenoteJaJaGural.cs", "二十六激活组件/三十七消费规则原生实现；Crypsis真实Start与任意3997 Lose对账、动态9码进入约束/销毁主引用冻结、Sprint仅intent；创建池量化R17/default钟/完整ushort4全局FIFO、光线前四前二黄；Room1真实组合场地3186点oracle/12trash启动/原生state7结束、动态宽3冲锋Finish消费；390集中模型/生产0错误/实际DLL全目录505PASS，颜色后续按源默认Danger补正并包含520集中回归，双视图/当前导航/身份代际/Seek/owner清理已验；实机待验"
    if source.endswith(tuple(f"/Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/{name}.cs"
                             for name in ("GymnasiouAcheloios", "Hippomenes", "Narkissos"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingGymnasion.cs", "三十激活组件/五十规则原生实现；双锤绝对角及四锤配对/真实Finish消费钟、四种Blaster顺序与六动作Native全局FIFO、仅首导航和R5中心约束；Sap累计五次/任意Finish、四方向2秒6.6强制行进/只查Sap末落点及迷乱诊断；231集中模型、生产0错误及实际DLL全目录495PASS，双视图/源当前导航/PR凝视/身份代际/旧raw屏障/Seek/owner清理已验，不扩强制行进或迷乱执行；实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/{name}.cs"
                             for name in ("SecretCladoselache", "DaenOseTheAvariciousTyphon"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "十九激活组件/二十一规则原生实现；Clado双单槽配对六击全局效果FIFO/2.1消费钟，Typhon创建中心单圈14.3及双31击旋转14.5/1.1、隐藏期间推进和轴角匹配；动态宽5冲锋、分散/精确八人分摊及仅显示20码撞墙击退；204集中模型、生产0错误及实际DLL全目录476PASS，双视图/当前导航/切人/Seek/owner清理离线已验，实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/{name}.cs"
                             for name in ("SecretKeeper", "DaenOseTheAvariciousUltros", "DaenOseTheAvariciousEchidna"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "三十三激活组件/四十二规则原生实现；Keeper锥与20码拉近独立/首槽hitbox起手缓存/max1有效位移、精确1.4d毒区三路及全同OID R12常驻顺劈；Ultros20码撞墙/水圈方向独立导航与免疫、R8显示R9分散；Echidna全局动态凝视槽/跨来源Finish/原目标代际冻结/PR面向、精确八人分摊、VoidStream expected99前四/晋升；193集中模型/生产0错误/实际DLL全目录467PASS，双视图/导航/Seek/owner清理；实机待验"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/VaultOneiron/{name}.cs"
                             for name in ("Gwyddneu", "GreatGimmeCat", "VaultRoadripper", "Fastitocalon"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "三十二激活组件/五十一规则原生实现；Violet前四插入FIFO、真三角Prism八项源快排、Uplift十二/十六项全显示与逐项F-3风险/前四色；创建球锁点+7.9且Electrify全清、beam世界Z-21/锁向+7.7且每四Finish晋升；Tremblor三段轴向匹配/消费钟+3；四动态Finish bait含冲锋二维实时长度、源死亡隐藏复活/销毁冻结；Roadripper尸体AllDestroyed/首独立缓存及Wheel原目标FIFO；179集中模型/实际DLL全目录455PASS，双视图/导航/Seek/owner清理；实机待验"
    if source.endswith(tuple(f"/Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/{name}.cs"
                             for name in ("GymnasiouPithekos", "GymnasiouStyphnolobion", "GymnasiouMeganereis"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingGymnasion.cs", "共二十六激活组件/三十九规则原生实现；Source111逐条六秒R18诱导及首目标R17.5、32212 Start清理；中心量化两段地震F+.5/Finish消费钟+2且忽略helper朝向；动态R60全30度EarthShaker真实Native source FIFO及目标Raidwide预测；20码撞墙击退仅投影、免疫外保留Hydrobomb方向导航/首KB时钟、R8显示R9分散导航target FIFO；164集中模型/生产0错误/实际DLL全目录439PASS，双视图/真实结束/切人/Seek/owner清理，实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/{name}.cs" for name in ("SecretDjinn", "SecretWorm"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "Djinn九组件/八规则和Worm七组件/九规则原生实现；25码径向撞墙击退仅显示/提示、免疫/独立来源/真实结束；Source图标逐条五秒诱导R10及首目标R17.5导航，Start全清；创建圈锁点加5.7秒且保留销毁历史，任意21715真实Finish全局FIFO；显式销毁/同ID新代/旧帧屏障，155集中模型/实际DLL全目录429PASS、双视图/导航/切人/Seek/owner清理，实机待验"
    if source.endswith("/Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/LyssaChrysine.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingGymnasion.cs", "八组件/十一规则原生实现；创建柱锁点加3.7秒且任意32315真实Finish全清，显式销毁/新代屏障；双同心队列独立中心量化锁点/F+.5首判定/逐轴匹配/Finish消费钟+2，helper朝向不参与，八人精确分摊；155集中模型及活视觉已消费补正定向、实际DLL全目录429PASS，双视图/导航/切人/Seek/owner清理，实机待验"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/CenoteJaJaGural/Room{room}.cs" for room in (2, 3, 4))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/CenoteJaJaGural.cs", "三十二激活组件/五十六读条规则原生实现；按房间trash存在启动、不筛死亡/可选/战斗，原房间对象state7或销毁结束，同ID换OID不替代；保留独立Start锁点/方向/判定和真实结束，三真实组合场地/最终中心及每房3186点源oracle、完整trash/奖励priority/洋红且不画房间对象；143集中模型、实际DLL全目录418PASS，双视图/导航/短取消/切人/逆向Seek/owner清理；未激活吸引/击退不扩控制，实机待验"
    if source.endswith(tuple(f"/Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/{name}.cs"
                             for name in ("AltarAiravata", "AltarKelpie"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingAltars.cs", "共十九激活组件/十九消费规则原生实现；Airavata Icon Source单槽覆盖/动态DirForward二十码撞墙击退与Buffet首读条诱导独立，保留Primary锥心/Source旋转/target-centered文字不对称及无额外伤害预测；Kelpie水球创建锁量化位置加8.6秒、死亡/空闲销毁/到期保留至13443真实结束，径向击退与免疫仍生效的场地中心水球方向导航分离；134集中模型、实际DLL全目录412PASS、双视图/导航/引用冻结/代际/取消/Seek/owner清理；真实物理与宿主执行待验"
    if source.endswith(tuple(f"/Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/{name}.cs"
                             for name in ("AltarDullahan", "TheWinged"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingAltars.cs", "十激活组件/十二规则各自原生实现；保留二十码径向击退原始来源代际/起手锁点/判定/墙截断/免疫/真实结束，Dullahan插入顺序串联与TerrorEye-only落点提示、恰好八人分摊，Winged首个有效锥内击退及Feather矩形；KB源无导航仅显示，普通危险/分摊导航，奖励All与优先级完整；125集中模型、实际DLL全目录407PASS、取消/销毁/切人/逆向Seek/owner清理验证；实机待验"
    if source.endswith("/Endwalker/TreasureHunt/TheExcitatron6000/LuckyFace.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/Excitatron.cs", "CFC819双向核对Territory1000；十三激活组件/十七规则、源32边形入口差集及最终AABB中心、真实helper独立读条及Fake/MerryGoRound边界、R6显示/R7分散导航与原目标FIFO/引用冻结/死亡复活、源单体/全屏及奖励All/priority原生实现；117集中模型及实际DLL全目录401PASS，源场地3186点对照、双视图/导航/Seek/owner清理验证；实机待验"
    if source.endswith("/Stormblood/TreasureHunt/TheHiddenCanalsOfUznair/Airavata.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/HiddenCanals.cs", "CFC276双向核对Territory725；七激活组件/十一规则、真实三形状并集减入口及最终AABB中心、2064常驻顺劈与施法中/本人源队伍方向诱导、全部同OID主怪标记、完整16OID All和奖励优先级原生实现；未激活小怪类不接管，117集中模型及实际DLL全目录401PASS，源场地3186点对照、双视图/导航/Seek/owner清理验证；实机待验"
    if source.endswith("/Stormblood/TreasureHunt/TheLostCanalsOfUznair/CanalIcebeast.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/LostCanals.cs", "CFC268双向核对Territory712；七激活组件/六规则、真实并集减入口场地、1EBF常驻顺劈/源队伍诱导、9557默认10000范围锁点凝视/眼睛/瞳孔/视角弧与PR面向及回放执行隔离、实际R6 Freezeover与奖励All原生实现；117集中模型及实际DLL全目录401PASS，源场地3186点对照、双视图/导航/真实结束/Seek/owner清理验证；实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheDungeonsOfLyheGhiah/{name}.cs"
                             for name in ("Alichino", "Goliath"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/DungeonsOfLyheGhiah.cs", "CFC688双向核对Territory879；二十一唯一激活组件/二十九规则，130世界顶点凹场及AABB中心、真实独立读条、8人精确分摊目标FIFO/引用冻结/死亡复活、EventState-only原位置R8硬障碍、AllDeadOrDestroyed奖励与优先级原生实现；110集中模型及实际DLL全目录391PASS，源场地2964点对照、双视图/导航/Seek/owner清理验证；未激活Fount/吸引及HeatGaze凝视不扩控制；实机待验"
    if source.endswith("/Dawntrail/TreasureHunt/VaultOneiron/GildedSentry.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "七激活组件、固定方场、锁起手几何及观察结束、动态单体/全队预测、奖励怪优先级和洋红方向标记、All组生命周期原生迁移；模型及实际DLL覆盖双视图/导航、原桥八槽、Pointless与高差/FATE顺序、真实raw更正、逆向Seek和owner清理；实机待验"
    if source.endswith("/Dawntrail/TreasureHunt/VaultOneiron/VaultGargantuan.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "入口主OID4898；九激活组件/十四规则、固定方场及R15实际Scoop、Mash延迟原目标FIFO、全队预测、奖励优先级/洋红和全部trash标记原生实现；AllDestroyed保留尸体及首489A缓存独立死亡/销毁结束，检查先于首次缓存，同ID新代不重绑、packet DTO替换不漏标；模型及实际DLL验证双视图/导航/Seek/owner清理；未激活Inhale不扩吸引；实机待验"
    if source.endswith(tuple(f"/Dawntrail/TreasureHunt/VaultOneiron/{name}.cs"
                             for name in ("VaultBat", "VaultFluidSoul", "VaultWindSpirit"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/VaultOneiron.cs", "二十八激活组件/四十三规则；三入口小怪与首个独立缓存Boss、AllDestroyed尸体/代际及首次缓存顺序原生实现；三动态诱导死刑保留原来源/目标引用、销毁冻结/死亡清理、原八槽队友方向导航、列表首项伤害时钟及Native按source首项消费；取消保留、同代补正不复活已消费项、缺Actor保留槽位预测、奖励priority差异经102模型与实际DLL双视图/导航/Seek/owner集中验证；无额外特殊移动执行；实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/{name}.cs"
                             for name in ("SecretBasket", "SecretPorxie"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "十八激活组件/二十八规则；Basket三段锁位锁向、首项轴向/角度匹配及处理时钟Finish+1.1，延迟全屏唯一代际时钟仅Native效果清；Porxie四爆三圈、原位置状态/量化输出、异来源首项方框匹配、实际效果位置及处理时钟+4.5、future缓存和取消销毁保留；同代原点补正/旧来源不可回滚或复活、普通组件/奖励All经88项模型与实际DLL双视图/导航/逆向Seek/owner集中验证；源无激活击退吸引不扩执行；实机待验"
    if source.endswith("/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/FuathTroublemaker.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "五激活组件/五规则；R6精确8人分摊保留原目标引用/销毁冻结/死亡隐藏复活、目标FIFO及真实结束，R3同伴/满员拒绝/重叠目标R12避让/任一合法圈导航，死标记者显示与Active导航分离；十字臂40半宽2.5、动态单体/Calls adds、All/奖励经模型及实际DLL验证双视图/导航/Seek/owner清理，无额外Shared预测或源未激活动作；实机待验"
    if source.endswith(tuple(f"/Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/{name}.cs"
                             for name in ("Hati", "AltarMandragora", "TheGreatGoldWhisker"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingAltars.cs", "十四激活组件、48边场地和中心轴投影例外、锁起手AOE/真实结束、奖励怪优先级及洋红标记原生迁移；两Delayable按捕获源槽与原目标FIFO/Native MainTarget清理，取消和销毁保留队列，raw真实变化与packet同代更正隔离；模型及实际DLL验证双视图/导航、源八槽、代际、反向Seek和owner清理；未激活凝视及变身不扩接管；实机待验"
    if source.endswith(tuple(f"/Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/{name}.cs"
                             for name in ("AltarSkatene", "AltarBeast", "AltarArachne", "TheOlderOne"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingAltars.cs", "三十八激活组件/四十六规则原生迁移；全Matanga常驻顺劈在读条中保持，真实R9.42轮廓与非目标避让、目标本人的源八槽R100队友方向约束分离；三延迟单体保留原目标FIFO/取消历史且效果先到不残留预测；两默认10000凝视、独立三环及不可选Idol几何、奖励All/priority/颜色通过模型和实际DLL集中回归；未激活视觉和特殊移动不扩接管；实机待验"
    if source.endswith(tuple(f"/Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/{name}.cs"
                             for name in ("AltarChimera", "AltarDiresaur", "AltarTotem"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingAltars.cs", "三十二激活组件/二十四规则原生迁移；Icon23来源身份、原位置R6/R10诱导、首目标R17.5导航、原八槽队友避让、死亡保留/销毁冻结及同ID新代显示与引用身份分离；真实Start恰一次/三次清全表，同代packet不重数、raw真实变化重启；毒区cast/native主目标/event/live三路及EventState过滤、每轮首项交接；Chimera两Interrupt意图和文字保留、不扩自动打断，Languorous为普通扇；共享Matanga/延迟单体/预测及All奖励通过模型与实际DLL双视图/导航/Seek/owner集中验证；实机待验"
    if source.endswith(tuple(f"/Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/{name}.cs"
                             for name in ("GymnasiouMandragoras", "GymnasiouLeon", "LampasChrysine"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingGymnasion.cs", "十五激活组件/二十四读条、48边场地、三种当前CastInfo单体预测、Leon实际R12、奖励怪差异优先级与颜色/All生命期原生迁移；Lampas前四插入FIFO全部导航/总数大于二时前二危险色、隐藏项晋升及文字、取消/销毁/同ID重建/同代补正/逆向Seek和owner清理通过模型与实际DLL；无源特殊移动不扩执行；实机待验"
    if source.endswith(tuple(f"/Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/{name}.cs"
                             for name in ("GymnasiouTigris", "GymnasiouSatyros", "GymnasiouTriton", "GymnasiouSphinx"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingGymnasion.cs", "二十八激活组件/三十九规则、48边场地、动态单体/全屏预测、奖励优先级/颜色及All生命期原生迁移；凝视眼睛/视角弧及PR面向保留窄安全缝；无过滤R4硬障碍；R5毒区cast/native主目标或包落点/event/live及每轮首项交接；R6显示/R7分散目标FIFO、销毁冻结与死亡复活；模型和实际DLL覆盖导航/双视图、当前对象采样、逆向Seek及owner隔离；未扩未激活拉近/囚禁；实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/{name}.cs"
                             for name in ("SecretUndine", "SecretSwallow", "SecretPegasus", "GreedyPixie"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "三十五激活组件/五十读条、固定48边场地、分身独立时钟及锁起手几何、动态单体/全队预测、四套奖励优先级与All/辅助体排除原生迁移；Swallow仅激活九组件且21719圆21720环，BodySlam不扩击退；模型及实际DLL验证未激活动作接管边界、不可选辅助来源、双视图/导航、当前时钟、源八槽九人截断与死亡空洞、逆向Seek及owner清理；实机待验"
    if source.endswith(tuple(f"/Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/{name}.cs"
                             for name in ("SecretKorrigan", "SecretSerpent"))):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingOubliettes.cs", "十四激活组件/二十二规则、源奖励优先级/颜色及All生命周期原生迁移；Korrigan默认10000凝视/双视图/PR面向及原目标MainTarget延迟FIFO，Serpent独立R8读条/R7.5仅EventState过滤实时硬障碍/动态单体；模型和实际DLL验证观察结束、销毁历史、双视图/导航、逆向Seek及owner清理；未激活吸引不扩执行；实机待验"
    if source.endswith("/Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/GymnasiouMegakantha.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/ShiftingGymnasion.cs", "八激活组件/十二读条及三动作真实效果原生迁移；32242锁R40半圆/新Start覆盖但不重置全局五效果计数、取消和销毁保留、第五次清零、旧代补正不覆盖后源或复活已结算状态；33015/33016只计数及来源级通用躲避压制不建新AOE；动态单体、全部rest标记及奖励All通过模型与实际DLL集中回归/Seek/owner清理；实机待验"
    if source.endswith("/Dawntrail/TreasureHunt/CenoteJaJaGural/BullApollyon.cs"):
        return "mp_native_offline_verified", "Presets/TreasureHunt/Entries/CenoteJaJaGural.cs", "十一激活组件/十八规则、独立4/11/2.5秒读条、当前单体/全屏预测、七奖励优先级/颜色及All生命期原生迁移；32边形减入口矩形并按差集AABB居中，源运行库2025点Contains及clamp对照通过；模型和实际DLL覆盖双视图/导航、真实结束、机制开关、逆向Seek和owner清理；未扩传送/吸引及其他房间接管；实机待验"
    if "/Hunt/" in source and module == "Aglaope":
        return "mp_native_offline_verified", "Presets/Hunts/Entries/SimpleHunts.cs", "八激活组件/七规则；Sonata锁R16.2单槽及done跨Start，真实native八槽raw/原TargetIndex/全世界pending状态/首任意状态ACK/原槽确认/严格三秒保留；Raid0等待不随视角，Finish不清圈；独立Away10/R30/严格免疫KB仅显示与危险文字，无新增导航或执行。首raw权威与NPC饱和时钟、未认领读条替换、显式销毁及接收者同ID新代均已验证；687集中模型、生产0错4既有警告、实际DLL全目录与强化专项通过，显示数据/真实Sonata解算/Seek/owner及旧录像缺失诊断已验；实机待验"
    for filename, entry in [
        ("DD60TheBlackRider.cs", "PalaceBlackRider"),
        ("DD70Yaquaru.cs", "PalaceYaquaru"),
    ]:
        if source.endswith(f"/Heavensward/DeepDungeon/{filename}"):
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅Boss机制，禁止深宫道中流程自动化；真实593/594区域核表，五/四组件原生迁移。BlackRider动态原引用冲锋/独立起读原点及队伍槽25撞墙击退/免疫门外方向约束/双视图落点与三路毒池，Yaquaru激活时捕获池引用/严格评分模型/原始Heavy四槽及逐副本Lose-Gain对账；Reset真实raw基线/缺视角同帧换目标和首饱和raw已补。687集中模型、生产0错4既有警告、两场各5772点源oracle、实际DLL专项与全目录exit0；显示/实际导航/Seek/owner及楼层共存已验，弱Goal及驱散不扩大执行，实机待验；见NativePalaceBosses.spec.md"
    if "/Hunt/" in source and module == "ForgivenRebellion":
        return "mp_native_offline_verified", "Presets/Hunts/Entries/SimpleHunts.cs", "Blizzard三候选与共享缓存、七次真实Finish旋转、SourceId图标配对/量化锁点及六次Effect旋转、每owner行进历史与两普通组件首项落点均原生迁移；模型及实际DLL验证精确时钟/参数、仅raw时长改变、代际更正、活动owner/helper销毁、显示/导航分离、逆向Seek及回放控制隔离；源行进仅显示提示，不新增执行；实机待验"
    if "/Hunt/" in source and module == "Sugriva":
        return "mp_native_offline_verified", "Presets/Hunts/Entries/SimpleHunts.cs", "九读条与九激活组件；Twister独立单槽20码击退/R8双视图与min8max8分摊导航、动态Butcher效果按来源FIFO及首项Tankbuster时钟、Prey累加/F+1.1/恰第二投石Start/Chocobo和raw事件Lose收据原生迁移；同帧Destroy先于新Start、各actor读条先于状态、helper补播、精确参数/严格免疫/引用代际/切人隔离/逆向Seek/owner清理通过模型及实际DLL；源无击退逆推导航或原生动作执行，不扩范围；实机待验"
    if "/Hunt/" in source and module in {
        "QueenHawk", "TheRaintriller", "Ker", "Gunitt", "AtticusThePrimogenitor", "Heshuala", "Ihnuxokiy", "Neyoozoteel", "Nechuciho",
        "ForgivenPedantry", "Ixtab", "Keheniheyamewi",
        "ForgivenRebellion",
        "KerShroud", "CrystalIncarnation", "Gajasura", "Orghana", "OPoorestPauldia", "ArchEta",
        "Hulder", "ForgivenGossip", "BoneCrawler", "Okina", "Angada", "Gamma", "LunatenderQueen",
        "Baal", "Huracan", "Grassman", "Maliktender", "FanAil",
        "Starcrier", "Storsie", "MoussePrincess", "Ophioneus", "Ruminator", "Sphatika", "Gurangatch", "Burfurlur",
        "Minerva", "Armstrong", "Supay", "TheMudman", "Petalodus", "LilMurderer", "Nuckelavee",
        "Rusalka", "Sugaar", "Aegeiros", "Nariphon", "Yilan", "NarrowRift", "SallyTheSweeper", "Tarchia",
        "Yehehetoauapyo", "RraxYitya", "Pkuucha", "TheForecaster", "KirlirgerTheAbhorrent", "Tyger", "CatsEye",
    }:
        return "mp_native_offline_verified", "Presets/Hunts/Entries/SimpleHunts.cs", "62模块432条读条及实际后续判定原生迁移；连续队列/双视图/PR凝视/热病停手移动/方向错乱/目标诱导分散/旋转十字/原生主目标/拉动投影/常驻顺劈/分摊截止时序/强制行进/记录逆序/Tarchia构造预览/取消后续、Omen方向缓存、Wingblade实际侧数、Pkuucha完整FIFO及隐藏R14、Forecaster气候替换、Kirlirger无限分摊、Tyger尾锥绘制导航分离、CatsEye两组件分摊及动态原始首项凝视、QueenHawk状态清圈与三秒行进、Raintriller辅助来源与三段FIFO、Ker逐帧PR停手禁读条取消施法及MP移动暂停、Gunitt原槽禁入mask及Shared聚合/死亡队首/真实Finish、Atticus喊话锁点及双风险吐息/辅助体FIFO、Heshuala几何匹配/真实Finish/冻结十字/未确认预测撤销、Ihnuxokiy效果队列/三秒行进/单体预测、Neyoozoteel全局索引三连/八击十字/目标FIFO、Nechuciho四段全局索引/前二风险晋升、Pedantry双段/动态Fever/Native主目标Witch/结束目标、Ixtab双重咏唱/组件目标FIFO/Doom转移对账、Slippery独立起点/全Risky队列/五秒行进窗口、公共按演员顺序补播及各owner输入基线均通过模型及实际DLL回放；实机待验"
    for filename, entry in [
        ("DD80Gudanna.cs", "PalaceGudanna"),
        ("DD90TheGodmother.cs", "PalaceGodmother"),
    ]:
        if source.endswith(f"/Heavensward/DeepDungeon/{filename}"):
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅Boss机制，不含深宫道中流程自动化；真实595/596区域核表，Gudanna八次native效果循环/7099锁存/原位置全Tornado硬障碍/构造攻略与缺视角历史，Godmother创建锁点10秒/任意7106真实Finish全清/首Giddy禁攻及四点重复索引/动态顺劈/灰炸弹priority5与独立DoT意图；weak0.5和10精确模型保留但不扩大旧MP导航。505集中模型、生产0错4既有警告、两场各5772点源oracle、实际DLL专项和全目录889条PASS/exit0；Director卸载/旧实体不重启/死亡缺视角/Seek/owner及楼层共存已验，实机待验；见NativePalaceBosses.spec.md"
    for filename, entry in [
        ("DD20Spurge.cs", "PalaceSpurge"),
        ("DD120Kirtimukha.cs", "PalaceKirtimukha"),
        ("DD100NybethObdilord.cs", "PalaceNybeth"),
    ]:
        if source.endswith(f"/Heavensward/DeepDungeon/{filename}"):
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅接管Boss；本机真实区域核表，双植物顺劈全来源/未知读条/蜂priority与独立禁DoT意图/当前NPC预测钟，Nybeth五激活动作/原引用目标圆/首存储预测钟/当前队伍mask/迟packet与raw代际/构造攻略loaded-pending-Director生命周期；场地实际源5772或5694点oracle、463集中模型、生产0错4既有警告、实际DLL专项与全目录exit0；双视图数据/生产导航/Seek/owner及楼层共存已验，禁DoT执行按总规格暂缓，实机待验；见NativePalaceBosses.spec.md"
    for filename, entry in [
        ("DD10PalaceDeathgaze.cs", "PalaceDeathgaze"),
        ("DD110Alicanto.cs", "PalaceAlicanto"),
        ("DD50EddaBlackbosom.cs", "PalaceEdda"),
        ("DD40Ixtab.cs", "PalaceIxtab"),
        ("DD140AhPuch.cs", "PalaceAhPuch"),
        ("DD150Tisiphone.cs", "PalaceTisiphone"),
        ("DD30Ningishzida.cs", "PalaceNingishzida"),
        ("DD130Alfard.cs", "PalaceAlfard"),
    ]:
        if source.endswith(f"/Heavensward/DeepDungeon/{filename}"):
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "仅接管Boss；精确场地/来源级通用躲避/观察结束/双视图已通过模型及实际DLL回放；含凝视、多路毒圈交接、顺劈诱导、四落圈后回中及Tisiphone职业优先级/滚动僵尸规划；楼层Zone会话持续保留；实机待验"
    if source.endswith("/Heavensward/Quest/MSQ/DivineIntervention.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/DivineIntervention.cs", "七组件及精确胶囊差集、锁起点冲锋、双Boss联合生命周期、铁链选敌及有色标记已通过模型和实际DLL回放；实机待验"
    if source.endswith("/Heavensward/Quest/Job/Dragoon/DragoonsFate.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/DragoonsFate.cs", "四AOE及精确四边形、Prey最近Gain及迟到Lose、NPC24槽空洞、恰三层薄冰低权重交接、隐藏导航与独立朝向轮廓、真实结束及旧帧隔离通过模型和实际DLL回放；旧录像缺原生敌我分类时不推断NPC槽；实机待验"
    for filename, entry, evidence in [
        ("/Endwalker/Quest/MSQ/WhereEverythingBegins/P1.cs", "WhereEverythingBegins",
         "真实1091/CFC874映射、精确36边形并集、任意trash入战触发与主体HP0终止、前五Nox显式FIFO、NPC动态R6分摊/R6显示R7分散导航、仅可选存活trash标记通过模型与实际DLL回放；P2仍保留"),
        ("/Endwalker/Quest/MSQ/FrostyReception.cs", "AFrostyReception",
         "真实1010/CFC812映射、精确20边形、八读条/七组件、LockOn动态创建时钟与冻结旧代、固定落圈全局FIFO、两独立分散按action和target清理、原生敌我标记通过模型和实际DLL回放；Zone仍保留"),
        ("/Endwalker/Quest/MSQ/TheGameIsAfoot.cs", "TheGameIsAfoot",
         "真实1177/CFC960映射、精确48边形加偏心矩形并集、前八落石FIFO、动态NPC分摊分散、冲锋锁起点与终点派生长度及仅恰三人导航/死亡联盟NPC计数、独立Raidwide与Tankbuster通过模型和实际DLL回放；Zone仍保留"),
        ("/Stormblood/Quest/MSQ/RhalgrsBeacon.cs", "RhalgrsBeacon",
         "真实671/CFC466映射、Termini动态朝向及共享/滑动时钟、销毁冻结代际与逐个效果、毒圈读条/效果/实体三路交接、10码锁点击退及全部实体导航与有效落点判定分离通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/MSQ/AFeastOfLies.cs", "AFeastOfLies",
         "真实863/CFC664映射、十四组件、前五Burn显式FIFO、十一/十二/十三雷圈显示与导航分离、最近hitbox选敌/PendingDead/空雷球回退及Serpent、20码撞墙击退通过模型和实际DLL回放"),
        ("/Stormblood/Quest/MSQ/TheMeasureOfHisReach.cs", "TheMeasureOfHisReach",
         "真实687/CFC468映射、四激活AOE及R10月光小队/NPC伤害预测、联盟排除、主怪独立结束和双视图通过模型及实际DLL回放"),
        ("/Shadowbringers/Quest/MSQ/FadedMemories/FlameGeneralAldynn.cs", "FadedMemories",
         "真实932/CFC743映射、两激活AOE、三将朝向标记、任意AID匹配施法来源及实际观察结束通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/MSQ/FadedMemories/KingThordan.cs", "FadedMemories",
         "真实932映射、10000凝视及PR面向/眼形弧双视图、真实结束/锁点、671仅单体优先级及事件快照交接、四种原生实体标记通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/MSQ/FadedMemories/Zenos.cs", "FadedMemories",
         "真实932映射、三剑任意入战触发与仅主体HP0结束、只画三剑、玩家来源21121纯全队预测及普通AOE观察结束通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/MSQ/FadedMemories/Nidhogg.cs", "FadedMemories",
         "真实932映射、R8高跳与全宽8前62龙眼雷光、分身施法不额外画分身、仅主怪结束通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/MSQ/DeathUntoDawn/P4LunarIfrit.cs", "DeathUntoDawn",
         "真实977/CFC780映射、前三冲锋/前二十字显式FIFO及空槽晋升、三独立Raidwide含联盟/NPC、钉子priority5保PendingDead及附带AOE许可通过模型和实际DLL回放；其他阶段与Zone仍保留"),
        ("/Shadowbringers/Quest/MSQ/TheOracleOfLight.cs", "TheOracleOfLight",
         "真实861/CFC662映射、双雷线及前八火圈显式FIFO、真实CastInfo变化重新排队、锁方向20码多源依次击退/撞墙射线/严格免疫/三角投影及位移线、raw时长和后续朝向修正通过模型和实际DLL回放；独立Zone仍保留"),
        ("/Stormblood/Quest/Job/Paladin/RaisingTheSword.cs", "RaisingTheSword",
         "真实706/CFC270映射、R20.5圆场、三AOE、EventState风圈与全部风圈锥导航、锁点10码击退/严格免疫/墙截断/角色投影及场外射线边界通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/Job/Dancer/GambolingForGil.cs", "GambolingForGil",
         "真实867任务映射和50段椭圆、动态Foxshot/NPC诱导/死亡目标/名单伤害、独立锁点25码击退、两套风圈集合、凝视PR面向和眼弧双视图通过模型和实际DLL回放；独立Zone仍保留"),
        ("/Stormblood/Quest/Job/Monk/ThePowerToProtect.cs", "ThePowerToProtect",
         "真实四边形场地、火圈读条/效果/实体逐帧交接、包落点修正与双视图、1269严格末一秒停手及移动保持至真实Lose、死亡/销毁重建独立状态通过模型和实际DLL回放"),
        ("/Stormblood/Quest/Job/Summoner/AnArtForTheLiving.cs", "AnArtForTheLiving",
         "真实16顶点场地、指示器原序创建时钟、逐轴炸弹替换及首位置销毁、死亡隐藏复活/冻结旧代、325选敌与PendingDead边界通过模型和实际DLL回放"),
        ("/Shadowbringers/Quest/Job/Gunbreaker/SteelAgainstSteel.cs", "SteelAgainstSteel",
         "动态剑组按Actor原序激活、无读条default时钟、销毁冻结同ID代际效果、前四直线FIFO晋升、独立半环及NPC动态R6分摊实际结束通过模型和实际DLL回放"),
        ("/Stormblood/Quest/Job/Scholar/OurUnsungHeroes.cs", "OurUnsungHeroes",
         "五激活组件、图标源对象/独立消费时钟/重复点名与逐项效果出队、NPC24伤害及冻结引用、死亡与同ID重建、325仅改变选敌并保持空目标续租通过模型和实际DLL回放"),
        ("/Endwalker/Quest/Job/Sage/LifeEphemeralPathEternal/AncelAndMahaud.cs", "LifeEphemeralPathEternal",
         "十激活组件、精确20边形、视觉建环/实际helper结束及消费时钟、NPC单人塔硬进入与满塔避让分支、超员轮廓和伤害、分摊分散目标交接通过模型和实际DLL回放"),
        ("/Endwalker/Quest/Job/Sage/LifeEphemeralPathEternal/Guildivain.cs", "LifeEphemeralPathEternal",
         "九激活组件、Player/Buddy双向动态诱导及NPC避让、同pair重连/效果清理/快照交接/包独有连线销毁、原生EventState与网络状态分离、重复缩场恢复通过模型和实际DLL回放"),
        ("/Stormblood/Quest/Job/Samurai/TheBattleOnBekko.cs", "TheBattleOnBekko",
         "四段半环保留真实朝向/逐轴和角度匹配/实际取消销毁推进及处理时钟、前八线插入顺序、原位置R4障碍与EventState、纯Raidwide通过模型和实际DLL回放"),
        ("/Endwalker/Quest/Job/Reaper/TheKillingArt.cs", "TheKillingArt",
         "精确64边形入口差集和源中心、前四半环FIFO及同caster多代独立身份、动态毒圈读条/效果/实体一对一交接与处理时钟、小怪选敌及未激活技能回退通过模型和实际DLL回放"),
        ("/Dawntrail/Quest/Job/Viper/FangsOfTheViper.cs", "FangsOfTheViper",
         "精确20边形、六读条范围、原始PlayerState/ARR对象零拉怪、Gowrow引用缓存与判定顺序、主代切换/同ID重建/Director、指定敌人绘制及SourcePlayerId回放通过模型和实际DLL回放"),
        ("/Stormblood/Quest/Job/Samurai/TheFaceOfTrueEvil.cs", "TheFaceOfTrueEvil",
         "四读条范围、动态全分身Arashi、三触发共享时钟、取消不清/8419效果清、旧快照屏障、纯Raidwide预测和主怪独立生命周期通过模型和实际DLL回放"),
        ("/Endwalker/Quest/Job/Sage/SagesFocus.cs", "SagesFocus",
         "十激活组件、精确20边形、NPC24单体及全队预测、R5显示/R6分散导航、死亡恢复/读条目标交接、未激活DosisIII不接管及三Boss标记通过模型和实际DLL回放"),
        ("/Stormblood/Quest/Job/DarkKnight/TheOrphansAndTheBrokenBlade.cs", "TheOrphansAndTheBrokenBlade",
         "四激活组件、NPC持续分摊及硬进入圈、8457取消/完成/效果不清、9298统一清、目标引用和权威目标交接、链priority5及原生敌我绘制通过模型和实际DLL回放"),
    ]:
        if source.endswith(filename):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", evidence + "；逆向Seek、回放控制隔离和owner清理通过；实机待验"
    for filename, entry in [
        ("/Stormblood/Quest/Job/Samurai/BloodOnTheDeck.cs", "BloodOnTheDeck"),
        ("/Dawntrail/Quest/Job/Pictomancer/MindOverManor.cs", "MindOverManor"),
        ("/Dawntrail/Quest/Job/Pictomancer/SomewhereOnlySheKnows/TheWingedSteed.cs", "SomewhereOnlySheKnows"),
        ("/Dawntrail/Quest/Job/Pictomancer/SomewhereOnlySheKnows/FlightOfTheGriffin.cs", "SomewhereOnlySheKnows"),
        ("/Dawntrail/Quest/Job/Pictomancer/SomewhereOnlySheKnows/JanquetilaquesPortrait.cs", "SomewhereOnlySheKnows"),
    ]:
        if source.endswith(filename):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "原生职业任务Boss；精确场地和源中心、独立观察结束、两组联合生命周期、原生敌我标记、10000凝视及PR面向、花创建快照/地震实际结束/双侧水线校正和前九插入次序通过模型与实际DLL回放；伤害预测非AOE，回放控制隔离及owner清理通过；实机待验"
    for filename, entry in [
        ("PLD/BigSolleretsToFill.cs", "BigSolleretsToFill"),
        ("THM/TheThreatOfPerplexity.cs", "TheThreatOfPerplexity"),
    ]:
        if source.endswith(f"/QuestBattle/ARealmReborn/ClassJobQuests/{filename}"):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "本机CFC435/325与Territory498/267双向核验；分别仅OID11D2→5、OID2A4→677(真实标量重载，2A5非第二OID)，未匹配保基础规则及Pointless；模型、实际DLL验证合法槽、预测致死、开关/逆向Seek/owner撤销；源无路线交互/专属AOE/移动；实机待验"
    for filename, entry in [
        ("BLM/TheVoidgateBreathesGloomy.cs", "TheVoidgateBreathesGloomy"),
        ("MNK/BrotherFromAnotherMother.cs", "BrotherFromAnotherMother"),
        ("SMN/AusteritiesofFlame.cs", "AusteritiesOfFlame"),
    ]:
        if source.endswith(f"/QuestBattle/ARealmReborn/ClassJobQuests/{filename}"):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "本机CFC/Territory/Quest双向核验317/314/323；显式0/5精确部分覆盖，未匹配保留基础仇恨/队伍/高度/FATE/无敌规则及Pointless保护；合法敌人槽、设置、逆向Seek、实时PR目标发布和owner清理通过模型与实际DLL；源无专属AOE或移动，通用躲避保留；实机待验"
    if source.endswith("/QuestBattle/ARealmReborn/MSQ/TheLominsanWay.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/TheLominsanWay.cs", "本机CFC354/Territory307/Quest66347核验；OID6EA显式1、6E9显式Pointless(-1)，未匹配保留基础规则；仅合法敌人显式负优先级清当前PR目标，无禁攻/全停手/禁AOE/移动扩张；模型、实际DLL和实时Hints验证设置、预测致死回退、逆向Seek与owner撤销；实机待验"
    for filename, entry in [
        ("AST/TheHandsOfFate.cs", "TheHandsOfFate"),
        ("MCH/ThePowerofaTourney.cs", "ThePowerOfATourney"),
        ("NIN/NinjaAssassin.cs", "NinjaAssassin"),
        ("SCH/Quarantine.cs", "Quarantine"),
    ]:
        if source.endswith(f"/QuestBattle/Heavensward/ClassJobQuests/{filename}"):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "区域原生优先级5选敌，其余保留仇恨/队伍含NPC/高度/FATE/无敌基础优先级及Pointless保护，无Boss或入战前提；合法敌人槽及原生敌我分类、可用性与设置开关、反向Seek和owner清理通过模型/实际DLL回放；通用躲避及其他区域机制保留；旧录像缺ObjectIndex/IsEnemy/FATE时不猜测；实机待验"
    if source.endswith("/QuestBattle/ARealmReborn/MSQ/OhCaptainMyCaptain.cs"):
        return "mp_native_offline_verified", "Presets/Quests/Entries/OhCaptainMyCaptain.cs", "本机CFC337/Territory273双向核验；OID3BC优先级2，其余保留源仇恨/战斗队伍含NPC/FATE/高度/无敌及Pointless基础规则；普通与原生NPC槽、FATE进出和逐帧PR目标发布、逆向Seek/回放隔离/owner清理通过模型及实际DLL；通用躲避保留；实机待验"
    for filename, entry in [
        ("ChasingShadows.cs", "ChasingShadows"), ("LurkersInTheGrotto.cs", "LurkersInTheGrotto"),
    ]:
        if source.endswith(f"/QuestBattle/ARealmReborn/MSQ/{filename}"):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "本机CFC双向确认Territory233/252；全部合法候选优先级覆盖、未知OID最高组、零优先级仍可攻击、无入战前提及原生对象槽、开关/反向Seek/回放隔离/owner清理通过模型和实际DLL回放；通用躲避保留；实机待验"
    for filename, entry in [
        ("ARealmReborn/ClassJobQuests/MNK/StarcrossedRivals.cs", "StarcrossedRivals"),
        ("ARealmReborn/MSQ/LordOfTheInferno.cs", "LordOfTheInferno"),
        ("ARealmReborn/ClassJobQuests/MNK/FiveEasyPieces.cs", "FiveEasyPieces"),
        ("Heavensward/MSQ/FamiliarFaces.cs", "FamiliarFaces"),
        ("ARealmReborn/ClassJobQuests/NIN/TheCrowKnows.cs", "TheCrowKnows"),
        ("Stormblood/ClassJobQuests/BLM/ShadesofShatotto.cs", "ShadesOfShatotto"),
        ("Stormblood/ClassJobQuests/BLM/OneGolemtoRuleThemAll.cs", "OneGolemToRuleThemAll"),
        ("ARealmReborn/MSQ/NotoriousBiggs.cs", "NotoriousBiggs"),
        ("ARealmReborn/MSQ/EveryLittleThingSheDoesIsMagitek.cs", "EveryLittleThingSheDoesIsMagitek"),
        ("ARealmReborn/ClassJobQuests/MNK/ReturnoftheHolyfist.cs", "ReturnOfTheHolyfist"),
        ("ARealmReborn/MSQ/TheReasonRoaille.cs", "TheReasonRoaille"),
    ]:
        if source.endswith(f"/QuestBattle/{filename}"):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "本机CFC/Territory双向核验；全部合法候选精确优先级、PrioritizeAll可达域及HP先行分支、Pointless保护/预测伤害/待生效治疗/高度顺序、零优先级回退、设置/反向Seek/回放隔离/owner清理通过模型和实际DLL回放；实时16ms内预测致死改目标及实际Hints发布验证通过；通用躲避保留；实机待验"
    if source.endswith("/Heavensward/Quest/TheWarringTriad/ABloodyReunion.cs"):
        return "mp_native_offline_verified", "Presets/Dungeons/Entries/ABloodyReunion.cs", "原生Regula及两炮台；锁定AOE/首登记炮台当前读条倒计时/当前爆炸优先级/来源接管/双视图标记已离线验证；实机待验"
    for filename, entry in [
        ("ASpectacleForTheAges.cs", "ASpectacleForTheAges"),
        ("CloseEncountersOfTheVIthKind.cs", "CloseEncountersOfTheVIthKind"),
    ]:
        if source.endswith(f"/Heavensward/Quest/MSQ/{filename}"):
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "本机表确认Territory533/459；原生范围/临时障碍/选敌和动态剑阵，死亡与销毁分离、来源级接管及双视图、Seek通过模型与实际DLL回放；仅接管对应Boss，区域Zone保留；实机待验"
    if "/Crucible/" in source:
        return "excluded_existing_mp", "Presets/Crucible/Entries", "MP 区域脚本；不导入 BMR"
    if source.endswith("/Raid/T01Caduceus/T01ADS.cs"):
        return "mp_native_offline_verified", "Presets/Raids/Entries/Coil1.cs", "T01 ADS 原生接管；卡杜凯乌斯独立核算"
    if source.endswith("/Raid/T01Caduceus/T01Caduceus.cs"):
        return "mp_native_offline_verified", "Presets/Raids/Entries/Coil1.cs", "平台/跳跃/分裂/软禁攻/待结算HP/坦克拉位及双视图实体标记已离线验证；禁DoT仅保留模型意图，执行暂缓；伤害预测保留诊断消费"
    if "/Raid/T02MultiADS/" in source:
        return "mp_native_offline_verified", "Presets/Raids/Entries/Coil2.cs", "七节点按主 OID 和实际能力隔离；含病毒分工与生命周期"
    if "/Raid/T04Gauntlet/" in source:
        return "mp_native_offline_verified", "Presets/Raids/Entries/Coil4.cs", "原生六波条件与超时、个人车点名和顺劈；保留终端名单销毁语义"
    if "/Raid/T05Twintania/" in source:
        return "mp_native_offline_verified", "Presets/Raids/Entries/Coil5.cs", "五阶段/六俯冲/分组/双蛇/真结束时采样旋风/孵化/五连液体地狱及减伤已离线验证；坦克归属/挑衅/拉位/双视图实体与持续伤害诊断通过实际DLL回放；未新增PR治疗策略"
    if "/RealmReborn/Extreme/Ex1Ultima/" in source:
        return "mp_native_offline_verified", "Presets/Trials/Entries/UltimaWeaponExtreme.cs", "蛮神独立时钟/实时转向、爆弹/追踪球/减伤及实际DLL回放通过；坦克归属/挑衅/拉位/朝向轴线与双视图实体标记已补；伤害预测保留诊断消费"
    if "/RealmReborn/Extreme/Ex2Garuda/" in source:
        return "mp_native_offline_verified", "Presets/Trials/Entries/GarudaExtreme.cs", "双寒风真实石柱遮挡/姐妹/Eye/Shriek/旋风/邪轮/刺羽护罩及实际DLL回放通过；坦克牵引/朝向/归属选敌与双视图实体标记已补；伤害预测保留诊断消费"
    if "/RealmReborn/Extreme/Ex3Titan/" in source:
        return "mp_native_offline_verified", "Presets/Trials/Entries/TitanExtreme.cs", "七阶段/缩圈/激震/牢狱/炸弹队列/最终循环及实际DLL回放通过；TankDistance0/狱卒归属/朝向/承伤强度与双视图实体标记已补；伤害预测保留诊断消费"
    if "/RealmReborn/Extreme/Ex4Ifrit/" in source:
        return "mp_native_offline_verified", "Presets/Trials/Entries/IfritExtreme.cs", "三轮火楔/灼热风/地火/锁链/光辉炎柱/深红旋风/减伤及实际DLL回放通过；坦克牵引与双视图实体标记已补；小钉禁DoT仅保留模型意图，执行暂缓；伤害预测保留诊断消费"
    for filename, entry in [
        ("OperationArchon.cs", "OperationArchon"),
        ("TheUltimateWeapon.cs", "TheUltimateWeapon"),
        ("TheStepsOfFaith.cs", "TheStepsOfFaith"),
    ]:
        if source.endswith(f"/RealmReborn/Quest/MSQ/{filename}"):
            return "mp_native_offline_verified", f"Presets/Quests/Entries/{entry}.cs", "原生主线单人战；方向格挡/缩圈及火球/滚动边界与连续5/14/35次机制均按源实现离线验证"
    for folders, entry, note in [
        (("A11BoneDragon", "A12Thanatos", "A13KingBehemoth", "A14Phlegethon"), "LabyrinthOfTheAncients",
         "四首领源已启用组件原生接管；骨龙复活/独立辅助体/彗星遮挡/漩涡已离线验证；源 WIP 未实现部分不计覆盖"),
        (("A21Scylla", "A22Glasya", "A23Amon", "A24Xande"), "SyrcusTower",
         "四首领源已启用组件原生接管；独立范围/全屏预告/谢幕冰块遮挡已离线验证；源 WIP 未实现部分不计覆盖"),
        (("A31AngraMainyu", "A32FiveheadedDragon", "A33Cerberus", "A34CloudofDarkness"), "WorldOfDarkness",
         "四首领源已启用组件原生接管；PR凝视/独立范围/动态冲锋/五连追踪/全屏预告已离线验证；源 WIP 未实现部分不计覆盖"),
    ]:
        if any(f"/Alliance/{folder}/" in source for folder in folders):
            return "mp_native_offline_verified", f"Presets/Raids/Entries/{entry}.cs", note
    for folder, entry in [
        ("T01IfritN", "IfritNormal"), ("T02TitanN", "TitanNormal"), ("T03GarudaN", "GarudaNormal"),
        ("T04PortaDecumana", "PortaDecumana"), ("T05IfritH", "IfritHard"),
        ("T06GarudaH", "GarudaHard"), ("T07TitanH", "TitanHard"),
        ("T08ThornmarchH", "ThornmarchHard"), ("T09WhorleaterH", "WhorleaterHard"),
        ("T01Susano", "Susano"), ("T02Lakshmi", "Lakshmi"),
        ("T03Shinryu", "Shinryu"), ("T04Tsukuyomi", "Tsukuyomi"),
        ("T05Yojimbo", "Yojimbo"), ("T07Byakko", "Byakko"),
        ("T08Suzaku", "Suzaku"), ("T09Seiryu", "Seiryu"),
        ("T01Titania", "Titania"), ("T02Innocence", "Innocence"), ("T03Hades", "Hades"),
        ("T04WarriorOfLight", "WarriorOfLight"),
        ("T02Hydaelyn", "Hydaelyn"), ("T08Asura", "Asura"), ("T04Zelenia", "Zelenia"),
        ("T01Valigarmanda", "Valigarmanda"), ("T06Arkveld", "GuardianArkveld"), ("T08Enuo", "Enuo"),
        ("T05Necron", "Necron"),
        ("T03QueenEternal", "QueenEternal"),
        ("T02ZoraalJa", "ZoraalJa"),
        ("T07Doomtrain", "Doomtrain"),
    ]:
        if f"/Trial/{folder}/" in source:
            return "mp_native_offline_verified", f"Presets/Trials/Entries/{entry}.cs", "按 Territory + Boss OID 隔离；讨伐战已由 MP 原生接管"
    for folder, entry in [
        ("D04Halatali", "Halatali"), ("D05Totorak", "Totorak"),
        ("D06Haukke", "HaukkeManor"), ("D07Brayflox", "BrayfloxLongstop"),
        ("D08Qarn", "Qarn"), ("D09CuttersCry", "CuttersCry"),
        ("D10StoneVigil", "StoneVigil"), ("D11DzemaelDarkhold", "DzemaelDarkhold"),
        ("D12AurumVale", "AurumVale"), ("D13CastrumMeridianum", "CastrumMeridianum"),
        ("D14Praetorium", "Praetorium"), ("D15WanderersPalace", "WanderersPalace"),
        ("D16Amdapor", "AmdaporKeep"), ("D17PharosSirius", "PharosSirius"),
        ("D26Snowcloak", "Snowcloak"), ("D27SastashaHard", "SastashaHard"),
        ("D29KeeperOfTheLake", "KeeperOfTheLake"), ("D02SohmAl", "SohmAl"),
        ("D03Aery", "Aery"), ("D04TheVault", "Vault"), ("D05GreatGubalLibrary", "GreatGubalLibrary"),
        ("D06AetherochemicalResearchFacility", "AetherochemicalResearchFacility"),
        ("D08FractalContinuum", "FractalContinuum"),
        ("D09SaintMociannesArboretum", "SaintMociannesArboretum"),
        ("D11Antitower", "Antitower"),
        ("D13SohrKhai", "SohrKhai"),
        ("D15Xelphatol", "Xelphatol"),
        ("D17BaelsarsWall", "BaelsarsWall"),
        ("D01SirensongSea", "SirensongSea"),
        ("D02ShisuiOfTheVioletTides", "ShisuiOfTheVioletTides"),
        ("D03BardamsMettle", "BardamsMettle"),
        ("D04DomaCastle", "DomaCastle"),
        ("D05CastrumAbania", "CastrumAbania"),
        ("D06AlaMhigo", "AlaMhigo"),
        ("D09DrownedCityOfSkalla", "DrownedCityOfSkalla"),
        ("D13Burn", "TheBurn"),
        ("D15GhimlytDark", "TheGhimlytDark"),
        ("D01Holminster", "HolminsterSwitch"),
        ("D03QitanaRavel", "QitanaRavel"),
        ("D04MalikahsWell", "MalikahsWell"),
        ("D05MtGulg", "MtGulg"),
        ("D06Amaurot", "Amaurot"),
        ("D07Twinning", "TheTwinning"),
        ("D08AkadaemiaAnyder", "AkadaemiaAnyder"),
        ("D09GrandCosmos", "GrandCosmos"),
        ("D10AnamnesisAnyder", "AnamnesisAnyder"),
        ("D11HeroesGauntlet", "HeroesGauntlet"),
        ("D12MatoyasRelict", "MatoyasRelict"),
        ("D13Paglthan", "Paglthan"),
        ("D01TowerOfZot", "TowerOfZot"),
        ("D02TowerOfBabil", "TowerOfBabil"),
        ("D03Vanaspati", "Vanaspati"),
        ("D04KtisisHyperboreia", "KtisisHyperboreia"),
        ("D05Aitiascope", "Aitiascope"),
        ("D06DeadEnds", "DeadEnds"),
        ("D07Smileton", "Smileton"),
        ("D08Stigma", "StigmaDreamscape"),
        ("D09AlzadaalsLegacy", "AlzadaalsLegacy"),
        ("D10FellCourtOfTroia", "FellCourtOfTroia"),
        ("D11LapisManalis", "LapisManalis"),
        ("D12Aetherfont", "Aetherfont"),
        ("D13LunarSubterrane", "LunarSubterrane"),
        ("D03SkydeepCenote", "SkydeepCenote"),
        ("D04Vanguard", "Vanguard"),
        ("D05Origenics", "Origenics"),
        ("D06Alexandria", "Alexandria"),
        ("D07TenderValley", "TenderValley"),
        ("D08StrayboroughDeadwalk", "StrayboroughDeadwalk"),
        ("D09YuweyawataFieldStation", "YuweyawataFieldStation"),
        ("D10Underkeep", "Underkeep"),
        ("D11MesoTerminal", "MesoTerminal"),
        ("D12Mistwake", "Mistwake"),
        ("D13TheClyteum", "TheClyteum"),
        ("D01Ihuykatumu", "Ihuykatumu"),
        ("D02WorqorZormor", "WorqorZormor"),
        ("D02DohnMheg", "DohnMheg"),
    ]:
        if f"/{folder}/" in source:
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", "按 Territory + Boss OID 隔离；未接管对象保留桥"
    for folder, entry in [
        ("D01Sastasha", "Sastasha"), ("D02TamTaraDeepcroft", "TamTaraDeepcroft"),
        ("D03Copperbell", "CopperbellMines"),
    ]:
        if f"/{folder}/" in source:
            mode = "原生状态及连续判定、守卫/小鬼选敌和敌人轮廓已定向验证；双视图预览、实际DLL回放及停止清理通过；道中按原模块范围隔离"
            return "mp_native_offline_verified", f"Presets/Dungeons/Entries/{entry}.cs", mode
    if "/PilgrimsTraverse/" in source and row["kind"] == "BossModule":
        floor = re.search(r"/DD(\d+)", source).group(1)
        return "mp_native_offline_verified", f"Presets/PilgrimsTraverse/Entries/PilgrimsTraverse{int(floor)}.cs", "十Boss连续时序/后续预览/真实形状与双视图击退投影已离线验证；PT80实时宿主六层停止已持有路线及清理通过；PT10持续伤害保留诊断，禁DoT执行暂缓；普通楼层保留桥"
    if source.endswith("/Modules/Heavensward/DeepDungeon/DD190TheGodfather.cs"):
        return "mp_script_needs_parity_review", "Presets/PalaceOfTheDead/Entries/Palace151To200.cs", "模型、真实读条结束/整波炸弹/原引用及双视图导航已离线验证；仅灰炸弹指定目标禁攻的宿主执行仍有缺口：PR MechanicControl.ApplyTarget遇任何ForbiddenTargets返回true，使TargetSelectorService跳过其他合法目标的普通重评估。不能换门面绕过、不全停手补偿、不修改PR。保留既有模型及native-palace-earlyzones-20261125-palace-02.log证据；待宿主能力与实机证据，详见PalaceOfTheDead151To200.spec.md"
    if "/DeepDungeon/DD" in source and re.match(r"DD(160|170|180)", module):
        return "mp_native_offline_verified", "Presets/PalaceOfTheDead/Entries/Palace151To200.cs", "独立复核后补齐：观察结束及短取消/毒圈逐项交接/冻水对象身份/首炸弹禁攻与整波结束/永久旋风/单向阶段及循环；击退专用方向禁区、双视图线与角色投影、个人提示和伤害预测诊断已接MP；模型、实际DLL四Boss与161条真实ARR读条验证通过；PR执行及最终像素实机待验"
    if source.endswith("/PalaceFloorModule.cs") and module in {f"Palace{n}" for n in range(10, 201, 10)}:
        entry = "Palace1To150" if int(module.removeprefix("Palace")) <= 150 else "Palace151To200"
        return "mp_native_offline_verified", f"Presets/PalaceOfTheDead/Entries/{entry}.cs", "仅授权战斗机制：20Zone共用raw真实结束/动态来源凝视及独立VFX25目标原引用、NPC float钟、60状态槽反伤停普攻、普通AOE/打断/HintDisabled与Boss楼层消费门控；1000房间墙体/6556历史陷阱生成对照，换层7248/旧图等待及即时Boss交接已验。77模型、生产0错4既有警告、同DLL29相关专项/18Palace检查/956全目录PASS且进程exit0，真实solver/逆向Seek/owner/回放隔离覆盖，见native-palace-earlyzones-20261125-*及PalaceOfTheDead151To200.spec.md。只迁机制，无探索/箱/清层目标/道具/层间推进，已删流程不恢复；独立Boss分别登记，宿主执行及最终像素待实机"
    return "bmr_pending", "", "BMR 候选；实际激活还受区域、成熟度、脚本和设置门控"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify committed reports without writing.")
    parser.add_argument("--runtime-types", type=Path, help="JSON array exported from the newly built runtime's concrete module types.")
    args = parser.parse_args()
    inventory = load_csv(REFERENCE / "BMRModuleInventory.csv")
    coverage = {r["module"]: r for r in load_csv(REFERENCE / "MechanicRuntimeCoverage.csv")}
    names = [r["module"] for r in inventory]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate inventory module.")
    included = {r["module"] for r in inventory if not r["status"].startswith("排除")}
    if included != coverage.keys():
        raise ValueError(f"Inventory/startup coverage mismatch: {included ^ coverage.keys()}")
    if args.runtime_types:
        actual = set(json.loads(args.runtime_types.read_text(encoding="utf-8")))
        if actual != included:
            raise ValueError(f"Inventory/live runtime mismatch: {actual ^ included}")
        print(f"Runtime census matched: {len(actual)} concrete module types.")

    # Restrict traversal to local encounter files and shared Components; do not
    # follow WorldState/core internals and accidentally label every boss alike.
    text_cache = {}
    def text(path):
        if path not in text_cache:
            text_cache[path] = path.read_text(encoding="utf-8-sig")
        return text_cache[path]

    shared = defaultdict(set)
    for path in sorted((UPSTREAM / "Components").glob("*.cs")):
        for symbol in SYMBOL.findall(text(path)):
            shared[symbol].add(path)
    folder_cache = {}
    signal_cache = {}

    def signal_hits(path):
        if path not in signal_cache:
            signal_cache[path] = {}
            for line, value in enumerate(text(path).splitlines(), 1):
                for signal, pattern in SIGNALS.items():
                    if signal not in signal_cache[path] and re.search(pattern, value):
                        signal_cache[path][signal] = line
        return signal_cache[path]

    def review_files(path):
        if path.parent not in folder_cache:
            symbols = defaultdict(set)
            for candidate in sorted(path.parent.glob("*.cs")):
                for symbol in SYMBOL.findall(text(candidate)):
                    symbols[symbol].add(candidate)
            folder_cache[path.parent] = symbols
        local = folder_cache[path.parent]
        seen, pending = set(), [path]
        while pending:
            current = pending.pop()
            if current in seen:
                continue
            seen.add(current)
            body = text(current)
            identifiers = set(re.findall(r"\b\w+\b", body))
            index = shared if current.parent == UPSTREAM / "Components" else local
            for identifier in identifiers:
                pending.extend(index.get(identifier, set()) - seen)
            for component in re.findall(r"\bComponents\.(\w+)", body):
                pending.extend(shared.get(component, set()) - seen)
        return sorted(seen)

    rows = []
    groups = defaultdict(list)
    for row in inventory:
        source = UPSTREAM / row["source"].removeprefix("BossMod/")
        state, entry, ownership = mp_ownership(row)
        if row["kind"] == "ZoneModule" and row["category"] == "DeepDungeon":
            ownership += "；深宫楼层Zone含机制与流程混合职责，按2026-11-25用户范围仅迁移AOE、面向/视线、停手禁攻及机制所需站位，保持源机制及已有MP消费范围。禁止迁移道中探索、开箱、机关交互、自动前进、道具使用和层间推进等流程自动化。保留既有审计状态及历史验证记录；历史流程验证不构成本轮继续移植或扩展的授权，楼层会话共存不代表Zone机制已迁，未核实机制仍须逐项验收"
        if state == "excluded_existing_mp":
            files = []
        else:
            if not source.is_file():
                raise ValueError(f"Missing module source: {source}")
            if row["module"].rsplit(".", 1)[-1] not in SYMBOL.findall(text(source)):
                raise ValueError(f"Missing type declaration: {row['module']}")
            if entry and not (ROOT / entry).is_file():
                raise ValueError(f"Missing MP entry: {entry}")
            files = [source] + [p for p in review_files(source) if p != source]
        evidence = []
        signals = []
        for signal in SIGNALS:
            found = []
            for path in files:
                if line := signal_hits(path).get(signal):
                    found.append(f"{path.relative_to(ROOT).as_posix()}:{line}")
            if found:
                signals.append(signal)
                evidence.append(signal + "=" + ";".join(found[:2]))
        group_id = re.sub(r"(?<=\d)[uU]$", "", row["group_id"])
        # Preserve metadata meaning: GroupID may identify a quest, FATE or hunt
        # rank, and must never be passed to Territory APIs as a territory ID.
        group_key = f"{row['expansion']}/{row['category']}/{group_id or row['module']}"
        result = {
            "module": row["module"], "kind": row["kind"], "expansion": row["expansion"],
            "category": row["category"], "group_key": group_key, "group_id_expression": row["group_id"],
            "primary_oid": coverage.get(row["module"], {}).get("primary_oid", ""),
            "maturity": row["maturity"], "source": row["source"],
            "migration_state": state, "mp_entry": entry, "ownership": ownership,
            "review_signals": "|".join(signals), "signal_evidence": "|".join(evidence),
            "acceptance": "excluded" if state == "excluded_existing_mp" else
                "MP 原生状态/双视图几何/控制门面/解算与合成回放已定向验证；录像校准及实机待验"
                if state == "mp_native_offline_verified" else
                "逐机制时序/双视图/面向视线/ACR停手/移动/回放清理均待逐项验收",
        }
        rows.append(result)
        if state != "excluded_existing_mp":
            groups[group_key].append(result)
    group_rows = [{
        "group_key": key,
        "boss_modules": sum(r["kind"] == "BossModule" for r in values),
        "zone_modules": sum(r["kind"] == "ZoneModule" for r in values),
        "bmr_pending": sum(r["migration_state"] == "bmr_pending" for r in values),
        "mp_needs_parity_review": sum(r["migration_state"] == "mp_script_needs_parity_review" for r in values),
        "mp_native_offline_verified": sum(r["migration_state"] == "mp_native_offline_verified" for r in values),
        "review_signals": "|".join(sorted({s for r in values for s in r["review_signals"].split("|") if s})),
        "modules": "|".join(r["module"] for r in values),
    } for key, values in sorted(groups.items())]
    for filename, content in [
        ("BMRMigrationInventory.csv", csv_text(rows, list(rows[0]))),
        ("BMRMigrationGroups.csv", csv_text(group_rows, list(group_rows[0]))),
    ]:
        path = REFERENCE / filename
        if args.check:
            if not path.is_file() or path.read_bytes() != content.encode("utf-8"):
                raise ValueError(f"Stale report: {path}")
        else:
            path.write_text(content, encoding="utf-8", newline="")
    print("States:", dict(Counter(r["migration_state"] for r in rows)))
    print("Included:", dict(Counter(r["kind"] for r in rows if r["migration_state"] != "excluded_existing_mp")))
    print(f"Metadata groups: {len(groups)} (not a verified unique-duty/territory count)")
    print("Reports verified." if args.check else "Reports written.")


if __name__ == "__main__":
    main()
