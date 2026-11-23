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


def mp_ownership(row):
    source = row["source"]
    module = row["module"].rsplit(".", 1)[-1]
    if "/Hunt/" in source and module in {
        "KerShroud", "CrystalIncarnation", "Gajasura", "Orghana", "OPoorestPauldia", "ArchEta",
        "Hulder", "ForgivenGossip", "BoneCrawler", "Okina", "Angada", "Gamma", "LunatenderQueen",
        "Baal", "Huracan", "Grassman", "Maliktender", "FanAil",
        "Starcrier", "Storsie", "MoussePrincess", "Ophioneus", "Ruminator", "Sphatika", "Gurangatch", "Burfurlur",
        "Minerva", "Armstrong", "Supay", "TheMudman", "Petalodus", "LilMurderer", "Nuckelavee",
        "Rusalka", "Sugaar", "Aegeiros", "Nariphon", "Yilan", "NarrowRift", "SallyTheSweeper",
    }:
        return "mp_native_offline_verified", "Presets/Hunts/Entries/SimpleHunts.cs", "40模块222条读条及实际后续判定原生迁移；连续队列/双视图/PR凝视/热病停手移动/方向错乱/目标诱导分散/旋转十字/原生主目标/拉动投影/常驻顺劈/分摊截止时序/强制行进/记录逆序均通过模型及实际DLL回放；实机待验"
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
    if "/DeepDungeon/DD" in source and re.match(r"DD(160|170|180|190)", module):
        return "mp_native_offline_verified", "Presets/PalaceOfTheDead/Entries/Palace151To200.cs", "四Boss原生判定、锁起点炸弹/成组释放、本人冲锋轮廓/诱导、击退落点、水圈拉位及停止清理已离线验证；Floor区域模块仍单独复核"
    if source.endswith("/PalaceFloorModule.cs") and module in {f"Palace{n}" for n in range(160, 201, 10)}:
        return "mp_native_offline_verified", "Presets/PalaceOfTheDead/Entries/Palace151To200.cs", "地图/墙/历史陷阱/BFS/凝视/清怪/箱子接近与交互/手选房间/忽略陷阱/反伤仅停自动攻击已原生离线验证；公开日志7222参数复制、满仓箱关联与槽位映射AutoPoms、录制开停及顺序通过实际DLL验证；原生动作实机待验"
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
