# Expedition layout manifest (ExpeditionLayout.json, schema 1; docs/EXPEDITION-LAYOUT.md), generated from the town
# (expedition_city.py) and the Ward Zero instance (expedition_ward_zero.py). It runs both generators' checks, then the
# manifest's own: ids unique and valid, door anchors paired between the town and each instance, every door, interaction
# and cache record pointing at an anchor of its own area, eight returns per town door and eight entries per instance exit,
# the town's sanctuary, final gate, spawns and boss, and each area's blockHash equal to its generated map's.
# Imported into Resources by tools/generate_expedition.py from the --fixtures copy (the import steps are in docs/EXPEDITION-LAYOUT.md).
# --draft also writes the exp-layout-4 join circle draft (docs/EXPEDITION-HUB.md "Join circle at the instance doors"): NOT LIVE,
# its own file, never imported; the live outputs are byte-identical with or without it. It prints the draft's checks
# (expedition_city.join_checks) and runs this manifest's checks on it.
# Usage: python3 docs/arenas/scripts/expedition_layout.py [--json manifest.json] [--city city.json] [--ward ward.json]
#        [--draft join-draft.json] [--fixtures]   (writes the three live fixtures and the draft under unity/ChipArenaConversion/Tests/)
# Deterministic: the same inputs write the same bytes.
import argparse, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import expedition_city as city, expedition_ward_zero as ward
from expedition_common import block_hash, id_ok

REVISION = 'exp-layout-3'     # bump when any generated record or map hash changes (the test pins this with the hashes)
DRAFT_REVISION = 'exp-layout-4'   # the join circle draft (not live; saves pinned to exp-layout-3 would not resume on it)
TESTS = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'unity', 'ChipArenaConversion', 'Tests'))
FIXTURES = {'city': 'expedition-city-draft.json', 'ward': 'expedition-ward-zero-draft.json', 'json': 'expedition-layout-draft.json',
            'draft': 'expedition-layout-4-join-draft.json'}

def manifest(draft=False):
    return {'schema': 1, 'revision': DRAFT_REVISION if draft else REVISION, 'world': city.AREA, 'instances': [ward.AREA], 'areas': [city.area(draft), ward.area()]}

def checks(m):
    out, ok = [], True
    areas = {a['id']: a for a in m['areas']}
    ok &= m['schema'] == 1 and m['world'] in areas and areas[m['world']]['role'] == 'world' and all(areas.get(i, {}).get('role') == 'instance' for i in m['instances'])
    out.append(f"schema {m['schema']}, revision {m['revision']}, world {m['world']}, instances {m['instances']}, areas {[a['id'] + ' -> ' + a['map'] for a in m['areas']]}")
    ids = [x['id'] for a in m['areas'] for x in a['anchors']] + [g['id'] for a in m['areas'] for g in a.get('gates', [])] + \
          [s['id'] for a in m['areas'] if isinstance(a['spawns'], list) for s in a['spawns']]
    out.append(f'ids: {len(ids)}, unique {len(set(ids)) == len(ids)}, pass the Expedition id rule {all(id_ok(i) for i in ids)}'); ok &= len(set(ids)) == len(ids) and all(id_ok(i) for i in ids)
    kinds = sorted({x['kind'] for a in m['areas'] for x in a['anchors']})
    out.append(f'anchor kinds {kinds} (public kinds only; no reward anchors are preauthored)'); ok &= set(kinds) <= {'door', 'machine', 'chest'}
    bad = []
    for a in m['areas']:
        anc = {x['id']: x for x in a['anchors']}
        for x in a['anchors']:
            if not (0 <= x['yaw'] < 360) or (x['kind'] == 'door') != bool(x['building']): bad.append(x['id'] + ' yaw/building')
            if not x['id'].startswith(a['id'] + ':'): bad.append(x['id'] + ' area prefix')
        for d in a['doors']:
            if anc.get(d['anchor'], {}).get('kind') != 'door': bad.append(d['anchor'] + ' door anchor')
            slots = d.get('returns' if a['role'] == 'world' else 'entries')
            if not slots or len(slots) != 8 or ('entries' in d) == (a['role'] == 'world'): bad.append(d['anchor'] + ' slots')
            for k in ('interact', 'closure'):
                v_ = d[k]
                if not (v_['sx'] > 0 and v_['sy'] > 0 and v_['sz'] > 0 and 0 <= v_['yaw'] < 360): bad.append(d['anchor'] + ' ' + k)
        used = [i['anchor'] for i in a.get('interactions', [])]
        for x in a['anchors']:
            if x['kind'] in ('machine', 'chest') and used.count(x['id']) != 1: bad.append(x['id'] + ' interaction')
        if any(anc.get(u, {}).get('kind') not in ('machine', 'chest') for u in used): bad.append(a['id'] + ' interaction anchor')
        if not a['boss']['points']: bad.append(a['id'] + ' boss points')
    out.append('area records consistent (anchor prefixes, door anchors, 8 returns per town door and 8 entries per instance exit, one interaction per machine and cache, boss points): ' + ('yes' if not bad else 'NO ' + ', '.join(bad)))
    ok &= not bad
    w = areas[m['world']]
    pairs = []
    for inst in m['instances']:
        t = [x for x in w['anchors'] if x['kind'] == 'door' and x['building'] == inst and x['id'] == f'{w["id"]}:door:{inst}']
        e = [x for x in areas[inst]['anchors'] if x['kind'] == 'door' and x['id'] == f'{inst}:door:exit' and x['building'] == inst]
        pairs.append((inst, len(t), len(e)))
    out.append('door pairs (town door, instance exit) per instance: ' + ', '.join(f'{i} {t}/{e}' for i, t, e in pairs)); ok &= all(t == 1 and e == 1 for _, t, e in pairs)
    joins = [(a['id'], d['anchor']) for a in m['areas'] for d in a['doors'] if 'join' in d]
    jok = all(i == m['world'] for i, _ in joins) and all(set(d['join']) == {'x', 'y', 'z', 'r', 'h'} and d['join']['r'] > 0 and d['join']['h'] > 0 for a in m['areas'] for d in a['doors'] if 'join' in d)
    out.append(f'join circles: {len(joins)}, on hub entry doors only with positive r and h: {jok}'); ok &= jok
    stray = [x['id'] for x in w['anchors'] if x['kind'] == 'door' and x['building'] not in m['instances']]
    out.append(f'town doors to buildings outside the instance list: {len(stray)} (exp-foundry and exp-transit stay out until their instances exist)'); ok &= not stray
    sp = w['spawns']
    out.append(f"world: sanctuary volumes {len(w['sanctuary']['volumes'])}, slots {len(w['sanctuary']['slots'])}; gates {[(g['id'], g['opens']) for g in w['gates']]}; spawns {len(sp)} (e 0..{len(sp) - 1}); boss trigger and {len(w['boss']['points'])} points")
    ok &= len(w['sanctuary']['volumes']) >= 1 and len(w['sanctuary']['slots']) == 8 and [g['id'] for g in w['gates']] == [f'{w["id"]}:gate:final'] and w['gates'][0]['opens'] == 'final'
    ok &= [s['e'] for s in sp] == list(range(len(sp))) and 'trigger' in w['boss']
    for a in m['areas']:
        if a['role'] == 'instance':
            out.append(f"{a['id']}: spawns {a['spawns']!r}, boss points {len(a['boss']['points'])}"); ok &= a['spawns'] == 'inherit:siren' or isinstance(a['spawns'], list)
    hashes = {city.AREA: block_hash(city.game_map()), ward.AREA: block_hash(ward.game_map())}
    hok = all(a['blockHash'] == hashes[a['id']] for a in m['areas'])
    out.append('blockHash per area equals its generated map: ' + str(hok) + ''.join(f"\n   {a['id']} ({a['map']}): {a['blockHash']}" for a in m['areas'])); ok &= hok
    return ok, out

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--city'); ap.add_argument('--ward'); ap.add_argument('--fixtures', action='store_true')
    ap.add_argument('--draft', help='also write the exp-layout-4 join circle draft (not live) here'); a = ap.parse_args()
    if a.fixtures:
        for k, name in FIXTURES.items():
            if getattr(a, k) is None: setattr(a, k, os.path.join(TESTS, name))
    ok_c, rep_c, graphs = city.checks(); ok_w, rep_w, _ = ward.checks()
    print('== expedition-city (exp-city)'); print('\n'.join(rep_c)); print('city checks: ' + ('pass' if ok_c else 'FAIL'))
    print('== expedition-ward-zero (exp-ward-zero)'); print('\n'.join(rep_w)); print('instance checks: ' + ('pass' if ok_w else 'FAIL'))
    m = manifest(); ok_m, rep_m = checks(m)
    print('== ExpeditionLayout.json'); print('\n'.join(rep_m)); print('manifest checks: ' + ('pass' if ok_m else 'FAIL'))
    ok_d, md = True, None
    if a.draft:
        ok_j, rep_j = city.join_checks(graphs); md = manifest(True); ok_md, rep_md = checks(md)
        print(f'== {DRAFT_REVISION} join circle draft (NOT LIVE: Resources/ExpeditionLayout.json stays {REVISION})'); print('\n'.join(rep_j))
        print('\n'.join(rep_md)); print('draft manifest checks: ' + ('pass' if ok_md else 'FAIL')); ok_d = ok_j and ok_md
    for path, data in ((a.city, city.draft()), (a.ward, ward.draft()), (a.json, m), (a.draft, md)):
        if path:
            with open(path, 'w') as f: json.dump(data, f, indent=None, separators=(',', ':'), sort_keys=False)
            print('wrote', path)
    sys.exit(0 if ok_c and ok_w and ok_m and ok_d else 1)

if __name__ == '__main__':
    main()
