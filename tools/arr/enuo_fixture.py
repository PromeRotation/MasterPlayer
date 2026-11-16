"""Create an anonymous Enuo regression fixture from ARR (no trigger/script execution).

Usage: python tools/arr/enuo_fixture.py RECORDING --maps DIR --out FILE.json.gz
For a different recording, also pass --roles MAP.json with {"roles": {"actorID": "MT", ...}, "note": "evidence"}.
Status layouts: MechanicRuntime/Upstream/Network/ServerIPC.cs.
"""
import argparse
import collections
import gzip
import hashlib
import json
import pathlib
import struct
from arr_decode import decode, HEADER

RELEVANT = {2941, 4562, 3523, 3943}
JOBS = {21: 'MT', 19: 'ST', 24: 'H1', 28: 'H2', 41: 'D1', 31: 'D2', 38: 'D3', 25: 'D4'}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('recording', type=pathlib.Path)
    p.add_argument('--maps', type=pathlib.Path, required=True)
    p.add_argument('--out', type=pathlib.Path, required=True)
    p.add_argument('--roles', type=pathlib.Path, help='JSON with explicit actor-ID -> role mapping and an evidence note; required for a different party.')
    args = p.parse_args()
    decoded = decode(args.recording, args.maps, False)
    raw = args.recording.read_bytes()
    mapping = json.loads((args.maps / f"opcode-map-{decoded['build']}.json").read_text())
    jobs, status, more = {}, {}, []
    offset, packets, previous = HEADER, 0, 0
    while offset < len(raw):
        assert offset + 12 <= len(raw)
        op, size, ms, actor = struct.unpack_from('<HHII', raw, offset)
        assert ms >= previous
        previous = ms
        offset += 12
        data = raw[offset:offset + size]
        offset += size
        assert len(data) == size and str(op) in mapping
        packets += 1
        ipc, t = mapping[str(op)], ms / 1000
        if ipc in (211, 241):
            start = 20 if ipc == 211 else 0
            assert size >= start + 360
            if ipc == 211 and actor >> 28 == 1: jobs[actor] = data[0]
            for i in range(30):
                sid, param, duration, source = struct.unpack_from('<HHfI', data, start + i * 12)
                if sid in RELEVANT:
                    more.append(dict(t=t, kind='status', id=actor, status=sid, param=param, duration=abs(duration)))
        elif ipc in (217, 218, 219, 220):
            for i in range(data[0]):
                start = 4 + i * 88
                assert start + 88 <= size
                target = struct.unpack_from('<I', data, start + 4)[0]
                if target >> 28 == 1: jobs[target] = data[start + 19]
                for j in range(min(data[start + 21], 4)):
                    at = start + 24 + j * 16
                    sid, param = struct.unpack_from('<HH', data, at + 2)
                    duration = struct.unpack_from('<f', data, at + 8)[0]
                    if sid in RELEVANT:
                        more.append(dict(t=t, kind='status', id=target, status=sid, param=param, duration=abs(duration)))
    # The recorder's own EffectResult can omit ClassID; confirm MCH from its unique Drill action.
    for e in decoded['events']:
        if e['kind'].startswith('effect') and e['action'] == 16498 and e['id'] >> 28 == 1:
            jobs[e['id']] = 31
    if args.roles:
        supplied = json.loads(args.roles.read_text('utf-8-sig'))
        players = {int(actor, 0): role for actor, role in supplied['roles'].items()}
        role_note = supplied['note']
        assert all(actor in jobs for actor in players), 'Role mapping references an unobserved player.'
    else:
        if hashlib.sha256(raw).hexdigest() != 'f76d1cd1d9aea16611ee9d9437b0e886f8613a1efa51902208290e72445ad28f':
            raise ValueError('A new recording needs --roles; do not reuse the first recording\'s job-to-role labels.')
        players = {actor: JOBS[job] for actor, job in jobs.items() if job in JOBS}
        role_note = 'Role labels inferred from the first eight-way spread; observed movement is not a success oracle.'
    assert len(players) == 8 and set(players.values()) == set(JOBS.values()), 'Need exactly one of each MT/ST/H1/H2/D1-D4.'
    ids = {actor: list(JOBS.values()).index(role) + 1 for actor, role in players.items()}
    def anon(actor):
        if actor in (0, 0xE0000000): return 0
        if actor not in ids: ids[actor] = len(ids) + 1
        return ids[actor]
    events = []
    last_move = {}
    for e in decoded['events'] + more:
        kind = e['kind']
        r = dict(t=e['t'], kind=kind, id=anon(e['id']))
        if kind in ('npc', 'boss'):
            r.update(oid=e['oid'], pos=e['pos'], rot=e['rot'])
        elif kind in ('move', 'setpos'):
            key = (e['id'], int(e['t'] * 10))
            if key in last_move: continue
            last_move[key] = True
            r.update(pos=e['pos'], rot=e['rot'])
        elif kind == 'cast' and (49972 <= e['action'] <= 50050 or e['action'] in (49369, 48475)):
            r.update(action=e['action'], target=anon(e['target']), loc=e['loc'], rot=e['rotation'], seconds=e['seconds'])
        elif kind.startswith('effect') and (49972 <= e['action'] <= 50050 or e['action'] == 48475):
            r.update(kind='effect', action=e['action'], seq=e['seq'])
        elif kind.startswith('control'):
            category, params = e['category'], e['params']
            if category == 34:
                r.update(kind='icon', icon=params[0], target=anon(params[1] or e['id']))
            elif category in (35, 47): r.update(kind='tether' if category == 35 else 'untether', tether=params[1], target=anon(params[2]))
            elif category == 109 and params[1] == 0x40000005: r['kind'] = 'wipe'
            elif category == 109 and params[1] == 0x40000003: r['kind'] = 'clear'
            elif category == 21 and params[0] in RELEVANT: r.update(kind='status', status=params[0], param=0, duration=0)
            elif category == 6: r['kind'] = 'death'
            elif category == 54: r.update(kind='targetable', value=params[0] != 0)
            else: continue
        elif kind == 'status': r.update(status=e['status'], param=e['param'], duration=e['duration'])
        elif kind != 'despawn': continue
        events.append(r)
    events.sort(key=lambda e: e['t'])
    result = dict(recording_sha256=hashlib.sha256(raw).hexdigest(), build=decoded['build'], content=decoded['content'], packets=packets,
                  duration=decoded['duration'], roles=[dict(id=ids[a], role=role, job=jobs[a]) for a, role in players.items()],
                  notes=[role_note,
                         'Actor IDs are anonymous; movement is sampled at at most 10 Hz per actor. All mechanic events retained.',
                         f"Observed director events: {sum(e['kind'] == 'wipe' for e in events)} wipes, {sum(e['kind'] == 'clear' for e in events)} completions. This does not establish plugin live validation."], events=events)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(result, separators=(',', ':'), ensure_ascii=False).encode()
    args.out.write_bytes(gzip.compress(payload, mtime=0))
    print(json.dumps(dict(events=len(events), kinds=dict(collections.Counter(e['kind'] for e in events)), bytes=args.out.stat().st_size)))


if __name__ == '__main__': main()
