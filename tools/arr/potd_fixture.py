"""Create anonymous Palace 151-200 regression evidence from the five ARR files.

Example: python tools/arr/potd_fixture.py --recordings C:/Users/xiaos/Downloads \
    --actions analysis/potd151-200/action-data.json \
    --out tests/Fixtures/PalaceOfTheDead/recordings.json
The actions input is a Lumina Action-sheet export, not an inferred action table.
"""
import argparse
import collections
import hashlib
import json
import pathlib
import struct

from arr_decode import HEADER, KINDS, decode


def sha(data):
    return hashlib.sha256(data).hexdigest()


def recording(path, maps):
    decoded = decode(path, maps, True)
    data = path.read_bytes()
    mapping = {int(k): v for k, v in json.loads(
        (maps / f'opcode-map-{decoded["build"]}.json').read_text(encoding='utf-8')).items()}
    # Object removal identifies an index, not the record header's actor ID.
    raw_objects, offset = [], HEADER
    while offset < len(data):
        opcode, size, ms, actor = struct.unpack_from('<HHII', data, offset)
        offset += 12
        payload = data[offset:offset + size]
        offset += size
        kind = KINDS.get(mapping.get(opcode))
        if kind in ('object', 'despawnobj'):
            raw_objects.append((kind, payload[0]))
    object_rows = iter(raw_objects)
    ids, entities, events, objects, npc = {}, [], [], {}, set()

    def local(value):
        if value in (None, 0, 0xE0000000):
            return 0
        if value not in ids:
            ids[value] = len(ids) + 1
            entities.append({'id': ids[value], 'kind': 'unknown'})
        return ids[value]

    for ev in decoded['events']:
        kind, actor = ev['kind'], ev['id']
        if kind in ('npc', 'boss'):
            npc.add(actor)
            entity = local(actor)
            entities[entity - 1].update(kind='npc', oid=ev['oid'])
            events.append(dict(t=ev['t'], kind='spawn', entity=entity, oid=ev['oid'], pos=ev['pos'], rot=ev['rot']))
        elif kind == 'object':
            _, index = next(object_rows)
            actor = ev['entity']
            entity = local(actor)
            entities[entity - 1].update(kind='object', oid=ev['oid'])
            objects[index] = actor
            events.append(dict(t=ev['t'], kind='objectSpawn', entity=entity, index=index,
                               oid=ev['oid'], pos=ev['pos'], rot=ev['rot']))
        elif kind == 'despawnobj':
            _, index = next(object_rows)
            if index in objects:
                events.append(dict(t=ev['t'], kind='despawn', entity=local(objects.pop(index))))
        elif kind == 'despawn' and actor in npc:
            events.append(dict(t=ev['t'], kind='despawn', entity=local(actor)))
            npc.discard(actor)
        elif kind == 'cast' and actor in npc:
            events.append(dict(t=ev['t'], kind='cast', entity=local(actor), action=ev['action'],
                actionType=ev['type'], target=local(ev['target']), seconds=ev['seconds'],
                loc=ev['loc'], rotation=ev['rotation'], sourcePos=ev.get('pos'), sourceRot=ev.get('rot')))
        elif kind.startswith('effect') and actor in npc:
            events.append(dict(t=ev['t'], kind='effect', entity=local(actor), action=ev['action'],
                actionType=ev['type'], seq=ev['seq'], targets=[local(t) for t in ev.get('targetIds', [])],
                sourcePos=ev.get('pos'), sourceRot=ev.get('rot')))
        elif kind.startswith('control') and (actor in npc or actor in objects.values()):
            category, p = ev['category'], ev['params']
            row = dict(t=ev['t'], entity=local(actor))
            if category == 14:
                row.update(kind='death')
            elif category in (15, 0x5F1):
                row.update(kind='cancel', action=p[2] if category == 15 else p[0])
            elif category == 20:
                row.update(kind='statusGain', status=p[0], extra=p[1])
            elif category == 21:
                row.update(kind='statusLose', status=p[0])
            elif category == 409:
                row.update(kind='objectState', state=p[0])
            else:
                continue
            events.append(row)
    counts, open_casts = collections.defaultdict(collections.Counter), {}
    delays = collections.defaultdict(list)
    for ev in events:
        if ev['kind'] == 'cast':
            counts[ev['action']]['casts'] += 1
            open_casts[ev['entity']] = ev
        elif ev['kind'] == 'effect':
            counts[ev['action']]['effects'] += 1
            cast = open_casts.get(ev['entity'])
            if cast and cast['action'] == ev['action']:
                delays[ev['action']].append(round(ev['t'] - cast['t'] - cast['seconds'], 6))
                counts[ev['action']]['matched'] += 1
                del open_casts[ev['entity']]
        elif ev['kind'] in ('cancel', 'death', 'despawn'):
            cast = open_casts.get(ev['entity'])
            if cast and (ev.get('action', 0) in (0, cast['action'])):
                counts[cast['action']]['canceledCasts'] += 1
                del open_casts[ev['entity']]
    coverage = []
    for action, count in sorted(counts.items()):
        row = dict(action=action, **{k: count[k] for k in ('casts', 'effects', 'matched', 'canceledCasts')})
        row.update(castsWithoutEffect=row['casts'] - row['matched'], effectsWithoutCast=row['effects'] - row['matched'])
        d = delays[action]
        row['delay'] = dict(count=len(d), min=min(d), max=max(d), mean=sum(d) / len(d)) if d else None
        coverage.append(row)
    return dict(source=dict(file=path.name, sha256=sha(data), build=decoded['build'], cfc=decoded['content'],
        territory=decoded['content'] + 389, duration=decoded['duration'], objectPositionOffset=52),
        entities=entities, events=events, coverage=coverage)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recordings', type=pathlib.Path, required=True)
    parser.add_argument('--actions', type=pathlib.Path, required=True)
    parser.add_argument('--out', type=pathlib.Path, required=True)
    parser.add_argument('--maps', type=pathlib.Path, default=pathlib.Path(__file__).parent / 'maps')
    args = parser.parse_args()
    files = sorted(p for p in args.recordings.glob('*.dat') if p.name.startswith('死者宫殿 地下')
                   and any(f'{floor}～{floor + 9}层' in p.name for floor in range(151, 201, 10)))
    if len(files) != 5:
        raise ValueError(f'Expected five recordings, got {len(files)}')
    actions = args.actions.read_bytes()
    exported = json.loads(actions)
    provenance = dict(kind='Lumina Action sheet export', sha256=sha(actions))
    if isinstance(exported, dict):
        provenance = exported['actionsSource']
        exported = exported['actions']
    result = dict(schemaVersion=1, actionsSource=provenance,
                  actions=exported, recordings=[recording(p, args.maps) for p in files])
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8')
    for rec in result['recordings']:
        print(rec['source']['file'], 'casts=', sum(c['casts'] for c in rec['coverage']),
              'events=', len(rec['events']), 'sha256=', rec['source']['sha256'])


if __name__ == '__main__':
    main()
