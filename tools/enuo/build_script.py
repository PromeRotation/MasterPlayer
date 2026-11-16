"""Bundle the local Enuo preset into one refreshable MP source script. No DLL build."""
from pathlib import Path
import argparse
import hashlib
import re

ROOT = Path(__file__).resolve().parents[2]
NAMESPACE = "MasterPlayer.LocalScripts.Enuo"


def replace_once(text, before, after):
    if text.count(before) != 1:
        raise ValueError(f"Source changed; review external adapter: {before!r}")
    return text.replace(before, after, 1)


def adapt(path):
    source = path.read_text(encoding="utf-8-sig")
    source = source.replace("MasterPlayer.Mechanics.Enuo", NAMESPACE)
    if path.name == "EnuoScript.cs":
        source = replace_once(source, "private EnuoOutput? _output;", "private EnuoOutput? _output;\n    private EnuoMpApi? _api;")
        source = replace_once(source, "_events = new(_encounter", "_api = new(SafeZone, Draw);\n        _events = new(_encounter")
        source = replace_once(source, "SafeZone.Manager);", "SafeZone.Manager, _api);")
        source = replace_once(source, "var runtime = MasterPlayerPlugin.Instance!.SafeZone;", "var runtime = _api!;")
        source = replace_once(source, "MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);", "_api?.RequestAutomaticMovement(this, false);")
    elif path.name == "EnuoOutput.cs":
        source = replace_once(source, "SafeZoneManager manager)", "SafeZoneManager manager, EnuoMpApi api)")
        source = source.replace("safe.SetNavigationOnly(", "api.SetNavigationOnly(").replace("draw.AddAoePreview(", "api.AddAoePreview(")
    elif path.name == "ReplayWorldCapture.cs":
        source = replace_once(source, "namespace MasterPlayer.Replay;", f"using MasterPlayer.Replay;\n\nnamespace {NAMESPACE};")
        source = replace_once(source, "ReplayStatus.Expiration(now, s.RemainingTime)", "Expiration(now, s.RemainingTime)")
        source = replace_once(source, "private ushort? _territory;", """private ushort? _territory;
    private static DateTime Expiration(DateTime now, float remaining)
        => !float.IsFinite(remaining) || remaining == 0 ? DateTime.MaxValue
            : now.AddSeconds(Math.Min(MathF.Abs(remaining), 100000f));""")
    elif path.name == "EnuoExtreme.cs":
        source = replace_once(source, "namespace MasterPlayer.Scripts.Enuo;", f"namespace {NAMESPACE}.Entry;")
        source = replace_once(source, '"0.1.0", "PromeRadar"', '"0.4.0-local", "PromeRadar"')
        source = replace_once(source, '"四钱苏木 BV1uKRuBkEg7：', '"外部脚本，修改后点刷新脚本。四钱苏木 BV1uKRuBkEg7：')
    # Keep each file's usings inside its namespace to avoid ambiguous MP/PR types.
    match = re.search(r"^namespace ([\w.]+);\s*", source, re.MULTILINE)
    if not match:
        raise ValueError(f"Expected one file-scoped namespace: {path}")
    relative = path.relative_to(ROOT).as_posix()
    return (f'\n// ===== {relative} =====\n#line 1 "{relative}"\n'
            f"namespace {match[1]}\n{{\n{source[:match.start()]}{source[match.end():]}\n}}\n#line default\n")


def bundle():
    files = [ROOT / "Presets/Enuo/Entries/EnuoExtreme.cs"]
    files += sorted((ROOT / "Presets/Enuo").glob("*.cs"))
    files += [ROOT / "src/Replay/ReplayWorldCapture.cs", ROOT / "Presets/Enuo/External/EnuoMpApi.cs.in"]
    return """// 恩欧歼殛战 · 视频打法 · 本地外部脚本（完整机制，不依赖内置 EnuoScript）
// 放入 pluginConfigs/PromeRotation/MasterPlayer/Scripts/，在 MP 点“刷新脚本”。
// 同 GUID 覆盖内置恩欧并沿用开关；仅保留一份本脚本。删除后刷新恢复内置。
// 模块源代码在 Presets/Enuo；tools/enuo/build_script.py 仅拼接源码，不编译 MP。
// 可以直接修改这份文件；重新生成会覆盖手工修改，请先同步回模块源码。
#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using MasterPlayer;
""" + "".join(adapt(path) for path in files)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "LocalOnly/Enuo/EnuoExtreme.cs")
    parser.add_argument("--check", action="store_true", help="Fail if the output is missing or stale; do not write")
    args = parser.parse_args()
    source = bundle()
    if args.check:
        if not args.output.is_file() or args.output.read_text(encoding="utf-8-sig") != source:
            raise SystemExit("External script is stale; run tools/enuo/build_script.py")
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(source, encoding="utf-8", newline="\n")
    print(f"{args.output}\nSHA256 {hashlib.sha256(source.encode()).hexdigest()}")
