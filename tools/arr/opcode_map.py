"""Build the server opcode -> ServerIPC index map for one client build.

Opcodes are reshuffled by every client patch, so each ARR must be decoded with
the map of the build that recorded it (uint32 at 0x10 of the .dat header).
The map is read from the client's packet dispatcher jump table.

Usage:
    python tools/arr/opcode_map.py --game "D:/.../最终幻想XIV/game" [--out tools/arr/maps]
Writes opcode-map-<build>.json, where <build> is read from --build or, when an
ARR is passed with --arr, from that recording's header.
"""
import argparse
import json
import pathlib
import re
import struct

DISPATCHER = re.compile(
    rb'\x49\x8b\x40\x10\x4c\x8b\x50\x38\x41\x0f\xb7\x42\x02\x83\xc0.\x3d....\x0f\x87....'
    rb'\x4c\x8d\x1d....\x48\x98\x45\x8b\x8c\x83....', re.S)


def build_map(exe: bytes) -> dict[int, int]:
    pe = struct.unpack_from('<I', exe, 0x3c)[0]
    count, optional = struct.unpack_from('<H', exe, pe + 6)[0], struct.unpack_from('<H', exe, pe + 20)[0]
    sections = [struct.unpack_from('<IIII', exe, pe + 24 + optional + i * 40 + 8) for i in range(count)]

    def raw(rva):
        for size, va, raw_size, offset in sections:
            if va <= rva < va + max(size, raw_size):
                return offset + rva - va
        raise ValueError(hex(rva))

    def rva(offset):
        for size, va, raw_size, pointer in sections:
            if pointer <= offset < pointer + raw_size:
                return va + offset - pointer
        raise ValueError(hex(offset))

    mapping = {}
    for match in DISPATCHER.finditer(exe):
        f = match.start(); fr = rva(f)
        minimum = -struct.unpack_from('<b', exe, f + 15)[0]
        cases = struct.unpack_from('<i', exe, f + 17)[0] + 1
        default = fr + 27 + struct.unpack_from('<i', exe, f + 23)[0]
        base = fr + 34 + struct.unpack_from('<i', exe, f + 30)[0]
        table = base + struct.unpack_from('<i', exe, f + 40)[0]
        for i in range(cases):
            target = base + struct.unpack_from('<i', exe, raw(table) + i * 4)[0]
            if target == default:
                continue
            b = raw(target)
            if exe[b:b + 9] != bytes.fromhex('488b014d8d4a1048ff'):
                continue
            slot = exe[b + 10] if exe[b + 9] == 0x60 else struct.unpack_from('<i', exe, b + 10)[0] if exe[b + 9] == 0xa0 else -1
            if slot >= 16 and slot % 8 == 0:
                mapping[minimum + i] = slot // 8 - 2
    return mapping


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--game', required=True, help='client game directory containing ffxiv_dx11.exe')
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--build', type=int, help='client build number')
    group.add_argument('--arr', type=pathlib.Path, help='an ARR recorded by this client, to read the build from')
    parser.add_argument('--out', type=pathlib.Path, default=pathlib.Path(__file__).parent / 'maps')
    args = parser.parse_args()

    build = args.build if args.build is not None else struct.unpack_from('<I', args.arr.read_bytes(), 0x10)[0]
    mapping = build_map((pathlib.Path(args.game) / 'ffxiv_dx11.exe').read_bytes())
    if not mapping:
        raise SystemExit('dispatcher signature not found; the client layout changed')
    args.out.mkdir(parents=True, exist_ok=True)
    path = args.out / f'opcode-map-{build}.json'
    path.write_text(json.dumps(mapping, indent=2), encoding='utf-8')
    print(f'{len(mapping)} opcodes -> {path}')


if __name__ == '__main__':
    main()
