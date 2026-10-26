"""Decode ARR duty recordings (.dat, "FFXIVREPLAY") into an event timeline JSON.

Each server packet becomes one event with its time (seconds from the start of
the recording), kind, source actor and the decoded fields below. Actor oid and
last known position/rotation are attached to every event of that actor.

Usage:
    python tools/arr/arr_decode.py recording.dat [...] [--maps tools/arr/maps] [--out DIR] [--all]

The opcode map for the recording's build must exist (see opcode_map.py).
Output: <out>/<recording>.json with header fields, packet counts and events.
--all keeps every packet kind (unknown ones as their ServerIPC index); by
default only the kinds used for mechanic analysis are written.
"""
import argparse
import collections
import json
import math
import pathlib
import struct

# ServerIPC indices -> names used in the event timeline.
KINDS = {
    228: 'control', 229: 'controlself', 230: 'controltarget',
    232: 'effect1', 235: 'effect8', 236: 'effect16', 237: 'effect24', 238: 'effect32',
    272: 'player', 273: 'npc', 274: 'boss', 275: 'despawn', 276: 'move', 277: 'movestate',
    279: 'setpos', 282: 'cast', 289: 'object', 290: 'despawnobj',
    410: 'map4', 411: 'map5', 412: 'map6', 414: 'map',
}
ANALYSED = {'cast', 'npc', 'boss', 'object', 'control', 'controlself', 'controltarget', 'map', 'map4', 'map5', 'map6',
            'move', 'movestate', 'setpos', 'despawn', 'despawnobj'}
HEADER = 0x36c


def angle(raw):
    return raw * 2 * math.pi / 65535 - math.pi


def quantized(values):
    return [x * 2000 / 65535 - 1000 for x in values]


def decode(path: pathlib.Path, maps: pathlib.Path, keep_all: bool):
    data = path.read_bytes()
    if data[:12] != b'FFXIVREPLAY\0':
        raise ValueError(f'{path.name}: not an FFXIVREPLAY file')
    build = struct.unpack_from('<I', data, 0x10)[0]
    content = struct.unpack_from('<H', data, 0x20)[0]
    length = struct.unpack_from('<I', data, 0x48)[0]
    if len(data) - HEADER != length:
        raise ValueError(f'{path.name}: truncated ({len(data) - HEADER} of {length} bytes)')
    map_path = maps / f'opcode-map-{build}.json'
    if not map_path.exists():
        raise FileNotFoundError(f'{map_path} missing: run opcode_map.py --arr "{path}" first')
    mapping = {int(k): v for k, v in json.loads(map_path.read_text(encoding='utf-8')).items()}

    actors, events, counts = {}, [], collections.Counter()
    offset, ms = HEADER, 0
    while offset < len(data):
        opcode, size, ms, actor = struct.unpack_from('<HHII', data, offset)
        offset += 12
        payload = data[offset:offset + size]
        offset += size
        ipc = mapping.get(opcode)
        kind = KINDS.get(ipc, str(ipc) if ipc is not None else f'op{opcode}')
        counts[(opcode, kind, size)] += 1
        event = {'t': ms / 1000, 'kind': kind, 'id': actor}
        if actor in actors:
            event.update(actors[actor])

        if kind == 'cast' and size == 32:
            action, action_type, _, _, seconds, target, rotation = struct.unpack_from('<HBBIfIH', payload)
            event.update(action=action, type=action_type, seconds=seconds, target=target, rotation=angle(rotation),
                         loc=quantized(struct.unpack_from('<HHH', payload, 0x18)))
        elif kind.startswith('effect') and size >= 40:
            event.update(action=struct.unpack_from('<I', payload, 8)[0], seq=struct.unpack_from('<I', payload, 12)[0],
                         animationTarget=struct.unpack_from('<H', payload, 0x1c)[0], type=payload[0x1f], targets=payload[0x21])
            # Header 0x28, then 64 bytes of effects per slot, 8 bytes padding, then the target IDs.
            slots = int(kind[6:])
            ids_at = 0x28 + slots * 64 + 8
            if size >= ids_at + 8 * slots:
                event['targetIds'] = [t for t in struct.unpack_from(f'<{slots}Q', payload, ids_at)[:payload[0x21]] if t]
        elif kind in ('control', 'controlself', 'controltarget'):
            event.update(category=struct.unpack_from('<H', payload)[0], params=list(struct.unpack_from('<IIII', payload, 4)))
        elif kind in ('npc', 'boss'):
            event.update(oid=struct.unpack_from('<I', payload, 0x40)[0], pos=list(struct.unpack_from('<fff', payload, 0x200)),
                         rot=angle(struct.unpack_from('<H', payload, 0x72)[0]))
            actors[actor] = {k: event[k] for k in ('oid', 'pos', 'rot')}
        elif kind == 'move':
            event.update(pos=quantized(struct.unpack_from('<HHH', payload, 6)), rot=angle(struct.unpack_from('<H', payload)[0]))
        elif kind == 'setpos':
            event.update(pos=list(struct.unpack_from('<fff', payload, 8)), rot=angle(struct.unpack_from('<H', payload)[0]))
        elif kind == 'movestate':
            # Unnamed move-state packet; its state values (0 ground, 1 slime controlled, 3 slime sliding,
            # 4 knocked airborne) were read from Gold Saucer recordings. Kept raw: the field offset varies by use.
            event.update(raw=payload.hex())
        elif kind == 'object':
            oid, entity, layout = struct.unpack_from('<III', payload, 4)
            event.update(oid=oid, entity=entity, layout=layout, pos=list(struct.unpack_from('<fff', payload, 0x34)),
                         rot=angle(struct.unpack_from('<H', payload, 0x22)[0]))
            actors[entity] = {k: event[k] for k in ('oid', 'pos', 'rot')}
        elif kind == 'map' and size >= 9:
            event.update(index=payload[8], state=struct.unpack_from('<I', payload, 4)[0])
        elif kind == 'despawn' and size == 8:
            event['id'] = struct.unpack_from('<I', payload, 4)[0]
            actors.pop(event['id'], None)

        if kind in ('move', 'setpos') and actor in actors:
            actors[actor].update(pos=event['pos'], rot=event['rot'])
        if keep_all or kind in ANALYSED or kind.startswith('effect'):
            events.append(event)

    return {
        'file': path.name, 'build': build, 'content': content, 'duration': ms / 1000,
        'counts': [{'op': op, 'kind': k, 'size': s, 'count': c} for (op, k, s), c in counts.most_common()],
        'events': events,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('recordings', nargs='+', type=pathlib.Path)
    parser.add_argument('--maps', type=pathlib.Path, default=pathlib.Path(__file__).parent / 'maps')
    parser.add_argument('--out', type=pathlib.Path, default=None, help='output directory (default: next to each recording)')
    parser.add_argument('--all', action='store_true', help='keep every packet kind')
    args = parser.parse_args()
    for path in args.recordings:
        result = decode(path, args.maps, args.all)
        out = (args.out or path.parent) / (path.stem + '.json')
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(result, ensure_ascii=False, indent=1), encoding='utf-8')
        kinds = collections.Counter(e['kind'] for e in result['events'])
        casts = collections.Counter(e['action'] for e in result['events'] if e['kind'] == 'cast')
        print(f"{path.name}: build {result['build']} content {result['content']} {result['duration']:.1f}s -> {out}")
        print('  kinds', dict(kinds.most_common()))
        print('  casts', dict(casts.most_common(20)))


if __name__ == '__main__':
    main()
