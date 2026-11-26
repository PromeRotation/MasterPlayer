"""Emit reviewed Crescent FATE declarations; never infer C# semantics.

Only this fixed manifest is supported. Source byte changes fail closed and
require a new review of inheritance/overrides/activation before updating it.
"""
import argparse
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "MechanicRuntime/Upstream/Modules/Dawntrail/Foray/FATE"
HASHES = {
    "TheWingedTerror.cs": "381c10795e9f2385f250be6996119fca4aa817bcb5a0006e702ef8675d67bb2b",
    "CrescentGremlin.cs": "ebe7b6ae60c6895e02cc6f93808d76f21b708e350689e8bc2df721773fc5683b",
    "CrescentFan.cs": "98fee0def06345d7ac07ea40cf9f34d556065b02199ec30f0964b578aeabcd41",
    "EyeToEye.cs": "81bcf372da642630ef98a5a76facc8482818db4ca7aad1a5897e738e4c96842a",
    "ScaleModel.cs": "7f82d6de579a397cb6e01442096a741ace29ab202b2a77ac0a13542a11071de5",
    "ARottenAffair.cs": "ce67030f766d0ab187d00d280ca427693e5055cbe451c195d5d87c957fd2bdb3",
    "ADelicateBalance.cs": "2f5288adbcd581c66b59ecfe60a98480274241cfb87781886b84a96454c0063c",
    "KingOfTheCrescent.cs": "c40ef9a9190fbba6d6bd733118e35bb58226b509436e2dfaf3727b5e9259f262",
    "SwornToSoil.cs": "19b16119a7f74ffcb13b6560629a1ea529464608e62fc3a3117f3b331b941762",
    "AllureOfTheOccult.cs": "79271f0cc7d9d21ab839405ea306c2c2823a26f7ba56aa15a6d35e39c55ea7df",
    "AnUnendingDuty.cs": "2598c77ed120a721d9d6d88d1e9eb07be81feaf4b4efa99f1ae4b984a689d9f8",
    "GaleForceEncounter.cs": "7045bdbcec9ae25d8921059af928615a20eb7882bc8370e3cc9010fadb608704",
    "NH110RelicIcewolf.cs": "1a59cd41dc7c6d36a1083a74b2aee3e5c8df7b971923e3dd76b1368ca6f5dfe3",
    "Thunderregnum.cs": "41b7964ae10224fb330b3563b06474f5f22fdc892acd7c5bbe5fe907a217f910",
    "APryingEye.cs": "e01b1801a99208510ff503b30d42b97853d515142b19dfca8a82416ef824a257",
    "ServingDarkness.cs": "31c00ddc114b3b99e3184f76281a9b11f73e141c368d7aef4d75b48370457bc2",
}
# Explicit source component order; an action group is one component/list.
# Shapes are MP production templates with already reviewed source dimensions.
MODULES = [
    ("TheWingedTerror", "甲板清扫者——巨大鸟", 1252, 1018, 1965, 0x46C1, False, [
        ("GaleCannon", [41274], "RayRectangleTemplate.Symmetric(10, 40)"),
        ("SphereShatter", [41273], "new CircleTemplate(7)"),
    ]),
    ("CrescentGremlin", "被欺负的魔法罐", 1346, 1093, 2072, 0x4D8A, False, [
        ("CrudeTaunt", [50225], "RayRectangleTemplate.Symmetric(6, 25)"),
        ("NonsenseTaunt", [50226], "new CircleTemplate(5)"),
    ]),
    ("CrescentFan", "被吹飞的魔法罐", 1346, 1093, 2073, 0x4D8C, True, [
        ("HighPressureTornado", [50221, 50222], "RayRectangleTemplate.Symmetric(4, 15)"),
    ]),
    ("EyeToEye", "诅咒宝珠——邪瞳", 1346, 1093, 2075, 0x4BA7, False, [
        ("AllEyes", [47147], "null", "Raidwide"),
        ("SeeNoEvil", [47148], "null", "Gaze", 30),
        ("Jettatura", [47151], "new CircleTemplate(8)"),
        ("ColdStare", [47149], "new FanTemplate(40, MathF.PI / 2)"),
        ("SinisterSight", [47152], "null", "Gaze", 50),
    ]),
    ("DemiMedusa", "仿制的蛇人偶——半灵美杜莎", 1346, 1093, 2083, 0x4C6A, False, [
        ("CursedSight", [48252, 48253], "new FanTemplate(60, MathF.PI / 3)"),
        ("LamianLesion", [48254], "new FanTemplate(25, MathF.PI)"),
        ("Dark", [48256], "new CircleTemplate(6)"),
    ]),
    ("ARottenAffair", "腐坏街道的守护者——忍耐基路伯", 1346, 1093, 2081, 0x4D61, False, [
        ("Glory", [49915], "new FanTemplate(50, MathF.PI / 2)"),
        ("StoneIII", [49907, 50071], "new FanTemplate(40, MathF.PI / 4)"),
        ("AeroIII", [49908, 50072], "RayRectangleTemplate.Symmetric(8, 40)"),
        ("Holy", [49912, 49913], "new CircleTemplate(6)"),
        ("ShortswordAndSorcery", [50118, 50119], "new CircleTemplate(15)"),
        ("LongswordAndSorcery", [50121, 50120], "new DonutTemplate(10, 25)"),
    ]),
    ("ADelicateBalance", "湿度猎手——除湿之火", 1252, 1018, 1968, 0x46B8, False, [
        ("FluidSwing", [30351, 30353], "new FanTemplate(60, MathF.PI / 2)"),
        ("HeatVortex", [30340], "new CircleTemplate(10)"),
        ("FireBlast", [30341], "RayRectangleTemplate.Symmetric(6, 25)"),
        ("Eruption", [30346], "new CircleTemplate(8)"),
        ("DryCycle", [30344], "new DonutTemplate(5, 40)"),
        ("ParanormalWave", [42160], "new FanTemplate(10, 60f * (MathF.PI / 180f) * 2)"),
    ]),
    ("KingOfTheCrescent", "悲鸣收集者——罗普罗斯", 1252, 1018, 1964, 0x46D7, False, [
        ("Airburst", [41386], "new CircleTemplate(11)", "CreatedCircle"),
        ("FeatherRain", [41390], "new CircleTemplate(11)"),
        ("AeroIV", [41391], "null", "Raidwide"),
        ("BitingScratch", [41388], "new FanTemplate(40, MathF.PI / 2)"),
        ("GlidingSwoop", [41387], "new CircleTemplate(50)"),
    ]),
    ("MadMudarch", "土壤守护者——癫泥怪", 1252, 1018, 1969, 0x46BD, False, [
        ("Rupture", [29808], "new CircleTemplate(16)", "StatusCircle"),
        ("RoyalFlushCircle", [29826], "new CircleTemplate(8)"),
        ("RoyalFlushDonut", [29827], "new DonutTemplate(10, 20)"),
        ("Unshower", [29807], "new CircleTemplate(6)"),
        ("FeculentFlood", [29825], "new FanTemplate(40, 30f * (MathF.PI / 180f) * 2)"),
        ("RockyRoll", [29810], "RayRectangleTemplate.Symmetric(6, 60)"),
    ]),
    ("AllureOfTheOccult", "魔界的叹息——妖艳魔花珊迪", 1346, 1093, 2078, 0x4D56, False, [
        ("PutridBreath", [48944, 48952], "new FanTemplate(25, 65f * (MathF.PI / 180f) * 2)"),
        ("WildWildBreath", [48945, 48946, 48947], "new DungeonCrossTemplate(30, 3)"),
        ("Burst", [48950], "new CircleTemplate(10)", "DeferredCircle"),
    ]),
    ("AnUnendingDuty", "神罚石兽——西西弗斯", 1252, 1018, 1966, 0x4735, False, [
        ("ThriceComeThunder", [41984, 41985, 41986], "new CircleTemplate(10)", "Concentric"),
        ("Trounce", [41972], "new FanTemplate(40, 30f * (MathF.PI / 180f) * 2)"),
        ("ThunderII", [41988], "new CircleTemplate(6)"),
        ("ThunderIV", [41992], "null", "Raidwide"),
        ("ThunderousResoundingMemoryCircle", [41974, 41980], "new CircleTemplate(10)"),
        ("ThunderousResoundingMemoryCone", [41978, 41982], "new FanTemplate(70, 22.5f * (MathF.PI / 180f) * 2)"),
        ("ThunderousResoundingMemoryDonut", [41976, 41981], "new DonutTemplate(10, 30)"),
    ]),
    ("GaleForceEncounter", "驾驭自然的巨兽——呼风狮鹫", 1346, 1093, 2082, 0x4BEC, False, [
        ("Windage", [47583], "new CircleTemplate(7)"),
        ("BitingScratch", [47588], "new FanTemplate(40, MathF.PI / 2)"),
        ("FreefallSequence", [47598, 47584], "new CircleTemplate(12)", "Freefall"),
        ("FocusedTremor", [47587, 47586, 47585, 47594, 47593, 47592, 47597, 47596, 47595], "new CircleTemplate(10)", "Concentric"),
    ]),
    ("RelicIcewolf", "狼占狗窝——遗迹冰狼", 1346, 1093, 2080, 0x4D5E, False, [
        ("StormWithinWithout", [49756, 49757, 49766, 49767], "new CircleTemplate(10)", "DelayedStorm"),
        ("IcePillar", [49770], "new CircleTemplate(4)"),
        ("Rush", [49759], "RayRectangleTemplate.Symmetric(4, 80)"),
        ("AgeOfEndlessFrost", [49761], "new FanTemplate(40, 30f * (MathF.PI / 180f) * 2)", "FirstWaveCone"),
        ("RoaringBlizzard", [49765], "new FanTemplate(50, 30f * (MathF.PI / 180f) * 2)"),
    ]),
    ("Thunderregnum", "高傲的雷兽——新月女王", 1346, 1093, 2084, 0x4D63, False, [
        ("RegalFulguration", [49494, 49495], "new FanTemplate(40, MathF.PI)"),
        ("Thunderbolt", [49500], "new CircleTemplate(10)"),
        ("NobleBlaster", [49501], "RayRectangleTemplate.Symmetric(5, 50)"),
        ("ThunderboltPuddle", [49502, 49919, 49920, 49921, 49922, 49923, 49924, 49925, 49926], "new CircleTemplate(10)", "Puddle"),
    ]),
    ("APryingEye", "监视之瞳——岛屿监视者", 1252, 1018, 1970, 0x47DC, False, [
        ("SearchMarkOfDeath", [43038, 43039], "new FanTemplate(9, 60f * (MathF.PI / 180f) * 2)", "Observer"),
        ("Stare", [43268, 43044], "RayRectangleTemplate.Symmetric(8, 60)"),
        ("VoidThunderII", [43045], "new CircleTemplate(6)"),
        ("Oogle", [43043], "null", "Gaze", 10000),
    ]),
    ("ServingDarkness", "凶恶使魔——生命收割者", 1252, 1018, 1972, 0x4772, False, [
        ("DismalRoar", [42185], "null", "Raidwide"),
        ("Menace", [42175], "new CircleTemplate(20)"),
        ("SoulSweep", [42177, 42181], "new FanTemplate(60, 65f * (MathF.PI / 180f) * 2)"),
        ("SweepingMenacingCharge", [42178, 42179], "null", "Charge"),
        ("HallOfSorrow", [42183], "new CircleTemplate(10)"),
        ("MenaceCharge", [42179, 42180], "new CircleTemplate(20)", "DelayedChargeCircle"),
    ]),
]


def outputs():
    lines = [
        "// Generated by tools/generate_crescent_fates.py from the reviewed manifest.",
        "using MasterPlayer.Mechanics.Dungeons;", "using MasterPlayer.Spatial.SafeZone;", "",
        "namespace MasterPlayer.Mechanics.Fates;", "",
        "internal static class CrescentFateCatalog", "{",
        "    internal static readonly CrescentFateDefinition[] Definitions =", "    [",
    ]
    result = {}
    for name, title, territory, cfc, fate, oid, fan, rules in MODULES:
        lines.append(f'        new("{name}", "{title}", {territory}, {cfc}, {fate}, 0x{oid:X}, {str(fan).lower()},')
        lines.append("        [")
        for rule in rules:
            component, actions, shape, *extra = rule
            kind = extra[0] if extra else "Aoe"
            radius = extra[1] if len(extra) > 1 else 0
            lines.append(f'            new("{component}", [{", ".join(map(str, actions))}], {shape}, CrescentFateRuleKind.{kind}, {radius}),')
        lines.extend(["        ]),"])
        result[f"Presets/Fates/Crescent/Entries/{name}.cs"] = f'''// Generated by tools/generate_crescent_fates.py.
using MasterPlayer.Mechanics.Fates;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：{title}", [{territory}],
    "masterplayer.fate.{name.lower()}", "1.0.0", "XSZ", "原生FATE战斗机制、动态圆场地与实际读条结束。")]
public sealed class {name} : CrescentFateScript
{{
    public {name}() : base(0x{oid:X}) {{ }}
}}
'''
    lines.extend(["    ];", "",
                  "    // Client Action.ExtraCastTime100ms: AllEyes=5; all other reviewed actions=0.",
                  "    internal static float ExtraCastTime(uint action) => action == 47147 ? .5f : 0;",
                  "", "    internal static bool Owns(uint territory, uint oid)",
                  "        => Definitions.Any(d => d.Territory == territory && d.PrimaryOid == oid);", "}", ""])
    result["Presets/Fates/Crescent/CrescentFateCatalog.cs"] = "\n".join(lines)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    for name, expected in HASHES.items():
        if hashlib.sha256((SOURCE / name).read_bytes()).hexdigest() != expected:
            raise SystemExit(f"Unreviewed C# source change: {name}; inspect activation, inheritance and overrides.")
    for relative, text in outputs().items():
        path = ROOT / relative
        if args.check:
            if not path.exists() or path.read_bytes() != text.encode("utf-8"):
                raise SystemExit(f"Generated declaration differs: {relative}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(text.encode("utf-8"))
    print(f"{len(MODULES)} reviewed modules, {sum(len(m[-1]) for m in MODULES)} components; declarations {'verified' if args.check else 'written'}.")


if __name__ == "__main__":
    main()
