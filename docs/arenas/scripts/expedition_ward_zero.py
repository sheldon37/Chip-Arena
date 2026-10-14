# Expedition Ward Zero instance M1 draft: `expedition-ward-zero`, generated from the CURRENT `siren` map in
# unity/ChipArenaConversion/Assets/Conversion/Resources/UnityMaps.json plus an Expedition overlay. Siren's map is read,
# never edited: its blocks, ramps, drop hints and collision-off list are copied verbatim (so the 53 Siren stage entries,
# PveContent.json `entries` by `e`, stay valid: the instance's spawns are "inherit:siren"), and the overlay adds only
# the exterior exit door's frame. The manifest records (expedition_layout.py) add the door's anchor, interaction and
# closure volumes, approach and eight entry slots, two caches and the boss points.
# A draft for Astra: it is NOT wired into the game. Siren's stage hazards are not carried (their ids must start with the
# map id and NativeWardZeroHazards pins the siren-* table; see docs/EXPEDITION-LAYOUT.md).
# Checks: both floors reached from the entry slots, the overlay blocks no inherited entry (every exit still on reached
# floor at its own level, 2 m or more from any overlay block, every from-to path 1 m or more from it), every record on
# walkable floor with pilot clearance (Hulk-sized at the boss points), Siren's blocks unchanged.
# Usage: python3 docs/arenas/scripts/expedition_ward_zero.py [--json out.json] [--png plan.png]
# Coordinates: Unity frame, x east, z north, y up, metres; floors at y 0 and 6.
# Deterministic: no clocks, no randomness; the same inputs write the same bytes.
import argparse, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from expedition_common import (Collider, Canvas, components, block_hash, id_ok, v, q, vol, vol_in_bounds, pt, vol_block, in_vol, yaw_of, forward, dist_xz,
                               yspan, corners, footprint, ramp_block, r4, RADIUS, HEIGHT, PILOT_CLEAR, HULK_CLEAR, HULK_BAND, HULK_RADIUS, HULK_HEIGHT, HULK_SLOT)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
RES = os.path.join(ROOT, 'unity', 'ChipArenaConversion', 'Assets', 'Conversion', 'Resources')
SOURCE = 'siren'
MAP = 'expedition-ward-zero'; AREA = 'exp-ward-zero'; L1 = 6.0
DOOR_W, DOOR_H = 3.2, 3.4

def load():
    maps = json.load(open(os.path.join(RES, 'UnityMaps.json')))['maps']
    siren = next(m for m in maps if m['id'] == SOURCE)
    entries = json.load(open(os.path.join(RES, 'PveContent.json')))['entries']
    return siren, entries
SIREN, ENTRIES = load()

# =====================================================================================================================
# THE OVERLAY. The exit door: the SW Intake's west wall (the shell's inner face at x -38), the intake being where the
# facility took people in. A metal frame 0.5 m proud of the face round a 3.2 x 3.4 m doorway centred at z -23.4; the
# shell behind stays solid (Clamp keeps pilots inside x -38), so the door leaves and the outside beyond are drawn.
DOOR = (-38.0, -23.4); YAW = 270.0           # the anchor faces out of the doorway, the way a pilot walks to leave (-x)
FACE, DEPTH = -38.0, 0.5
overlay = []
def box(id, x0, x1, z0, z1, y0, y1, material='metal'):
    b = dict(id=MAP + '-' + id, material=material, pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0),
             rotation=q(), role='static', mass=26, segment=None, hp=0)
    overlay.append(b); return b
z0, z1 = DOOR[1] - DOOR_W / 2, DOOR[1] + DOOR_W / 2
box('exit-jamb-s', FACE, FACE + DEPTH, z0 - 0.4, z0, 0, DOOR_H + 0.4)
box('exit-jamb-n', FACE, FACE + DEPTH, z1, z1 + 0.4, 0, DOOR_H + 0.4)
box('exit-lintel', FACE, FACE + DEPTH, z0, z1, DOOR_H, DOOR_H + 0.4)
# Entry slots: two columns of four inside the doorway, clear of the interaction volume, the stair's foot and the bench,
# facing into the facility (+x).
ENTRIES_IN = [pt(x, 0, z, 90.0) for x in (-35.0, -33.6) for z in (-26.2, -24.7, -22.1, -20.6)]
# Caches: one on the ground floor (the atrium's south-west arcade, on the way in), one upstairs (the NE control room).
CACHES = [pt(-12.0, 0, -10.5, yaw_of(12.0, 10.5)), pt(25.0, L1, 24.0, 180.0)]
# Boss points: the Cradle (Siren's boss spawn, the Spine hatch's landing) and the atrium floor north and south of it.
BOSS_POINTS = [pt(0, 0, 0, 180.0), pt(0, 0, 8.6, 180.0), pt(0, 0, -8.6, 0.0)]
# Siren's own Hulk exits a Hulk-sized capsule cannot stand at (Siren content, inherited by `e`; not the overlay's): the
# gantry breaches' exits are 0.65 m from the gantry rail's top bar. Reported for Astra; the check fails on any other.
KNOWN_SIREN_HULK = {'breach-gantry-e', 'breach-gantry-w'}

def door_record():
    fx, fz = forward(YAW); s = -1.0                       # an instance exit: the party's side is behind the yaw (inside)
    cx, cz = DOOR                                         # volumes keep 5-10 cm off the face (it is the bounds' edge, x -38)
    return {'anchor': f'{AREA}:door:exit',
            'interact': vol(cx + s * fx * 1.1, 0, cz + s * fz * 1.1, 3.0, 3.0, 2.0, YAW),
            'closure': vol(cx + s * fx * 0.25, 0, cz + s * fz * 0.25, DOOR_W, DOOR_H, 0.4, YAW),
            'approach': pt(cx + s * fx * 1.6, 0, cz + s * fz * 1.6),
            'entries': ENTRIES_IN}

# =====================================================================================================================
def game_map():
    s = SIREN
    return dict(id=MAP, name='Ward Zero (Expedition)', tag='Meridian Biocontainment, Ward Zero. The way in is the way out.', theme=s['theme'], music=s['music'],
                ambience=s['ambience'], look=s['look'], sky=s['sky'], floor=s['floor'], accent=s['accent'], rotation=False,
                bounds=dict(s['bounds']), spawns=s['spawns'], blocks=list(s['blocks']) + overlay, segments=[], hazards=[], pilotBlocks=list(s['pilotBlocks']),
                nav=dict(nodes=[], links=[]), ramps=s['ramps'], navHints=dict(drops=s['navHints']['drops'], safe=[], collisionOff=s['navHints']['collisionOff']))
def draft():
    return dict(schema=1, draft=True, note=f'Draft instance from docs/arenas/scripts/expedition_ward_zero.py: the current siren map (blockHash {block_hash(SIREN)}) plus the Expedition overlay; not game content. Import steps in docs/EXPEDITION-LAYOUT.md.',
                materials=[], maps=[game_map()])
def area():
    fx, fz = forward(YAW)                                 # the anchor: the clear floor point 0.6 m inside the doorway
    anchors = [dict(id=f'{AREA}:door:exit', kind='door', building=AREA, **pt(DOOR[0] - fx * 0.6, 0, DOOR[1] - fz * 0.6, YAW))]
    anchors += [dict(id=f'{AREA}:cache:{i}', kind='chest', building='', **c) for i, c in enumerate(CACHES)]
    def front(p, d):
        fx, fz = forward(p['yaw']); return dict(x=r4(p['x'] + fx * d), y=p['y'], z=r4(p['z'] + fz * d))
    return {'id': AREA, 'role': 'instance', 'map': MAP, 'blockHash': block_hash(game_map()),
            'anchors': anchors, 'doors': [door_record()],
            'interactions': [dict(anchor=f'{AREA}:cache:{i}', **front(c, 0.8), range=2.0) for i, c in enumerate(CACHES)],
            'spawns': 'inherit:' + SOURCE, 'boss': {'points': BOSS_POINTS}}

# =====================================================================================================================
# CHECKS
def checks():
    out, ok = [], True
    m = game_map(); off = set(m['navHints']['collisionOff'])
    base = Collider([b for b in SIREN['blocks'] if b['id'] not in off], SIREN['ramps'], SIREN['bounds'])
    col = Collider([b for b in m['blocks'] if b['id'] not in off], m['ramps'], m['bounds'])
    keep = lambda y: -0.01 <= y <= SIREN['bounds']['ceiling'] - 1
    base.build(keep); col.build(keep)
    starts = []
    for i, s in enumerate(ENTRIES_IN):
        n = col.node_at(s['x'], s['y'], s['z']); assert n, f'entry slot {i} not on walkable floor'; starts.append(n)
    reach = col.flood(starts)
    def floor(n, y): return abs(n[2] - y) < 0.05
    for lvl, y in (('ground', 0.0), ('upper', L1)):
        tot = [n for n in col.graph if floor(n, y)]; got = [n for n in tot if n in reach]; was = [n for n in base.graph if floor(n, y)]
        out.append(f'{lvl} floor (y {y:g}): {len(tot)} standable 0.5 m cells ({len(was)} in Siren alone), {len(got)} reached from the entry slots ({100 * len(got) / max(1, len(tot)):.1f}%)')
        if len(got) != len(tot):
            ok = False
            for g in components(col, [n for n in tot if n not in reach])[:8]: out.append('   UNREACHED %d cells round (%.1f, %.1f)' % g)
    lost = sorted(set(base.graph) - set(col.graph))
    xs = [base.cell_xz(n[0], n[1]) for n in lost]
    out.append(f'cells the overlay takes from Siren: {len(lost)}, all within 1 m of the exit frame: {all(math.hypot(max(abs(x - (FACE + DEPTH / 2)) - DEPTH / 2, 0), max(abs(z - DOOR[1]) - DOOR_W / 2 - 0.4, 0)) <= 1.0 for x, z in xs)}')
    ok &= all(math.hypot(max(abs(x - (FACE + DEPTH / 2)) - DEPTH / 2, 0), max(abs(z - DOOR[1]) - DOOR_W / 2 - 0.4, 0)) <= 1.0 for x, z in xs)
    # Siren's blocks are copied unchanged, the overlay's ids are its own.
    same = m['blocks'][:len(SIREN['blocks'])] == SIREN['blocks'] and m['ramps'] == SIREN['ramps']
    ids = [b['id'] for b in m['blocks']]
    out.append(f'Siren blocks copied unchanged: {same} ({len(SIREN["blocks"])} blocks, {len(SIREN["ramps"])} ramps, {len(off)} collision-off); overlay blocks {len(overlay)}, all {MAP}-*: {all(b["id"].startswith(MAP + "-") for b in overlay)}; ids unique: {len(set(ids)) == len(ids)}')
    ok &= same and len(set(ids)) == len(ids)
    out.append(f'Siren blockHash {block_hash(SIREN)}; instance blockHash {block_hash(m)}')
    # The inherited entries: every exit still on reached floor at its own level, clear of the overlay, paths too.
    bad = []
    for e in ENTRIES:
        x = e['exit']; n = col.node_at(x['x'], x['y'], x['z'])
        if n is None or n not in reach: bad.append(f"e{e['e']} {e['id']} exit off reached floor")
    out.append(f"inherited entries (PveContent.json, {len(ENTRIES)} by e): exits on reached floor at their own level: {len(ENTRIES) - len(bad)}" + ''.join('\n   BAD ' + s for s in bad))
    ok &= not bad and [e['e'] for e in ENTRIES] == list(range(len(ENTRIES)))
    def seg_dist(b, a, c):
        return min(dist_xz(b, a[0] + (c[0] - a[0]) * t / 20, a[1] + (c[1] - a[1]) * t / 20) for t in range(21))
    def band(b, e):
        lo = min(e['from']['y'], e['exit']['y']); hi = max(e['from']['y'], e['exit']['y']) + max(HULK_BAND, e.get('height') or 0)
        y0, y1 = yspan(b); return y0 < hi and y1 > lo
    ex = min((dist_xz(b, e['exit']['x'], e['exit']['z']), e['id'], b['id']) for e in ENTRIES for b in overlay if band(b, e))
    pa = min((seg_dist(b, (e['from']['x'], e['from']['z']), (e['exit']['x'], e['exit']['z'])), e['id'], b['id']) for e in ENTRIES for b in overlay if band(b, e))
    out.append('nearest inherited exit to an overlay block at its height: %.1f m (%s, %s); nearest from-to path: %.1f m (%s)' % (ex[0], ex[1], ex[2].replace(MAP + '-', ''), pa[0], pa[1]))
    ok &= ex[0] >= 2.0 and pa[0] >= 1.0
    breach = [e for e in ENTRIES if e['kind'] == 'breach']
    bok = all(any(b['id'] == e['block'] and b['role'] == 'static' and b['pos'] == e['center'] and b['size'] == e['size'] for b in m['blocks']) for e in breach)
    out.append(f'breach panels of the {len(breach)} breach entries present and unchanged: {bok}'); ok &= bok
    # The records on reached floor at their own level with clearance.
    d = door_record()
    def check_points(label, pts, need, yhi=HEIGHT + 0.05):
        bad = []; worst = 99
        for name, p in pts:
            n = col.node_at(p['x'], p['y'], p['z']); f = n is not None and n in reach and col.floor_under(p['x'], p['z'], p['y'])
            c = col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + yhi); worst = min(worst, c)
            if not f or c < need: bad.append(f"{name} ({p['x']:g}, {p['y']:g}, {p['z']:g}) floor {f} clear {c:.2f}")
        out.append(f'{label}: {len(pts)} on reached floor at their own level with {need:g} m clear: {len(pts) - len(bad)} (tightest {worst:.2f} m)' + ''.join('\n   BAD ' + s for s in bad))
        return not bad
    ok &= check_points('entry slots', [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)], PILOT_CLEAR)
    ok &= check_points('door approach', [('approach', d['approach'])], PILOT_CLEAR)
    a = area()
    ok &= check_points('cache anchors (piece footprint)', [(c['id'], c) for c in a['anchors'] if c['kind'] == 'chest'], 1.0)
    ok &= check_points('interaction points', [(c['anchor'], c) for c in a['interactions']], PILOT_CLEAR)
    ok &= check_points('boss points (Hulk-sized)', [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)], HULK_CLEAR, yhi=HULK_BAND)
    ok_d = in_vol(d['interact'], d['approach']['x'], 0.0, d['approach']['z']) and not any(in_vol(d['interact'], p['x'], 0.0, p['z']) for p in ENTRIES_IN)
    ok_d &= col.floor_under(d['interact']['x'], d['interact']['z'], 0.0) and col.floor_under(d['closure']['x'], d['closure']['z'], 0.0)
    cl = vol_block('', d['closure']); jam = [b for b in overlay if 'jamb' in b['id']]
    gap = min(dist_xz(b, x, z) for b in jam for x, z in corners(cl))
    ok_d &= gap < 0.05 and abs(d['closure']['sx'] - (z1 - z0)) < 1e-6
    out.append(f'exit door: doorway ({DOOR[0]:g}, 0, {DOOR[1]:g}) yaw {YAW:g}, anchor 0.6 m inside, closure {d["closure"]["sx"]:g} x {d["closure"]["sy"]:g} m between the jambs, approach inside the interaction volume, entries outside it: {ok_d}'); ok &= ok_d
    # Spacing to the inherited entries (the party's first seconds; the cache and its buyers).
    recs = [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)] + [('approach', d['approach'])] + [(c['id'], c) for c in a['anchors']]
    md = min((math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']), e['id'], n) for e in ENTRIES for n, p in recs if abs(e['exit']['y'] - p['y']) < 1)
    out.append('nearest inherited exit to an entry slot, the door or a cache in a straight line: %.1f m (%s, %s)' % md); ok &= md[0] >= 4.0
    # Walking distance (0.5 m grid steps) from the entry slots to the nearest inherited exit.
    dist = {n: 0 for n in starts}; frontier = list(starts)
    while frontier:
        nxt = []
        for n in frontier:
            for k in col.neighbours(n):
                if k not in dist: dist[k] = dist[n] + 1; nxt.append(k)
        frontier = nxt
    wk = min((dist.get(col.node_at(e['exit']['x'], e['exit']['y'], e['exit']['z']), 10 ** 9) * 0.5, e['id']) for e in ENTRIES)
    out.append('nearest inherited exit to the entry slots on foot: %.1f m (%s)' % wk); ok &= wk[0] >= 6.0
    bp = [(e['id'], i) for e in ENTRIES for i, p in enumerate(BOSS_POINTS) if math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']) < 3 and abs(e['exit']['y'] - p['y']) < 1]
    out.append('inherited exits within 3 m of a boss point: ' + (', '.join(f'{i} (boss{k})' for i, k in bp) or 'none') + ' (the Cradle is also the Spine hatch landing, as in Siren)')
    sl = min(math.hypot(a_['x'] - b_['x'], a_['z'] - b_['z']) for i, a_ in enumerate(ENTRIES_IN) for b_ in ENTRIES_IN[i + 1:])
    out.append(f'entry slots: closest pair {sl:.2f} m'); ok &= sl >= 1.4 - 1e-6
    hz = []
    for h in SIREN['hazards']:
        for vz in (h['zones'] or [dict(center=h['center'], size=h['size'])]):
            for n, p in recs + [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)]:
                c, s = vz['center'], vz['size']
                if c['y'] - s['y'] / 2 < p['y'] + 2 and c['y'] + s['y'] / 2 > p['y'] and math.hypot(max(abs(p['x'] - c['x']) - s['x'] / 2, 0), max(abs(p['z'] - c['z']) - s['z'] / 2, 0)) < 2.0: hz.append((n, h['id']))
    out.append(f'records within 2 m of a Siren hazard volume (not carried, kept clear in case the instance runs them): {len(hz)}'); ok &= not hz
    # The layout loader's own gate (NativeExpeditionLayout.ValidateGeometry), mirrored: entries clear for a Hulk too;
    # anchors, interactions and the approach clear for the pilot probe; from the first entry, Hulk-sized and pilot routes to
    # every entry, boss point and the approach; every inherited exit routed back with its capsule (a Hulk where the entry
    # rules allow one); the volumes inside the bounds.
    hulk = Collider([b for b in m['blocks'] if b['id'] not in off], m['ramps'], m['bounds']); hulk.build(keep, HULK_RADIUS, HULK_HEIGHT)
    hs = min((col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + HULK_HEIGHT + 0.05), f'entry{i}') for i, p in enumerate(ENTRIES_IN))
    out.append('entry slots clear for a Hulk (%.3f m radius, %.2f m tall): tightest %.2f m (%s)' % (HULK_SLOT, HULK_HEIGHT, hs[0], hs[1])); ok &= hs[0] >= HULK_SLOT
    pr = min((col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + HEIGHT + 0.05), n) for n, p in [(x['id'], x) for x in a['anchors']] + [(x['anchor'], x) for x in a['interactions']] + [('approach', d['approach'])])
    out.append('anchors (the door too), interactions and the approach clear for the pilot probe (%.2f m, %.1f m): tightest %.2f m (%s)' % (RADIUS, HEIGHT, pr[0], pr[1])); ok &= pr[0] >= RADIUS
    o = ENTRIES_IN[0]; rp = col.flood([col.node_at(o['x'], o['y'], o['z'])]); rh = hulk.flood([hulk.node_at(o['x'], o['y'], o['z'])])
    need = ENTRIES_IN + BOSS_POINTS + [d['approach']]
    miss = [p for p in need if hulk.node_at(p['x'], p['y'], p['z']) not in rh or col.node_at(p['x'], p['y'], p['z']) not in rp]
    out.append(f'Hulk-sized and pilot routes from entry 0 to every entry, boss point and the approach: {len(need) - len(miss)} of {len(need)}'); ok &= not miss
    def hulk_ok(e): return e['kind'] == 'breach' or e['kind'] == 'drop' and e.get('hulk')
    miss = [e['id'] for e in ENTRIES if col.node_at(e['exit']['x'], e['exit']['y'], e['exit']['z']) not in rp]
    out.append(f'inherited exits routed to entry 0 for the pilot probe: {len(ENTRIES) - len(miss)} of {len(ENTRIES)}' + ''.join('\n   BAD ' + i for i in miss)); ok &= not miss
    hk = [e for e in ENTRIES if hulk_ok(e)]; tight = []; far = []
    for e in hk:
        x = e['exit']; n = hulk.node_at(x['x'], x['y'], x['z'], 0.36)
        if n is None or n not in rh:
            tight.append(e['id']); c = col.clearance(x['x'], x['z'], x['y'] + 0.36, x['y'] + HULK_HEIGHT + 0.05)
            near = hulk.node_at(x['x'], x['y'], x['z'], 1.0)
            if near is None or near not in rh: far.append(e['id'])
            out.append(f"   Hulk exit {e['id']} ({x['x']:g}, {x['y']:g}, {x['z']:g}): free radius {c:.2f} m < {HULK_RADIUS:g} m ({col.nearest_block(x['x'], x['z'], x['y'] + 0.36, x['y'] + HULK_HEIGHT)[1]}); a Hulk-clear point within 1 m routes to entry 0: {near is not None and near in rh}")
    out.append(f'inherited Hulk exits ({len(hk)}) Hulk-clear and routed to entry 0 for a Hulk: {len(hk) - len(tight)}; not Hulk-clear at the exit (Siren content, known: {sorted(KNOWN_SIREN_HULK)}): {sorted(tight)}')
    ok &= set(tight) <= KNOWN_SIREN_HULK and not far
    vb = all(vol_in_bounds(V, m['bounds']) for V in (d['interact'], d['closure']))
    out.append(f'door volumes inside the bounds (1 cm margin): {vb}'); ok &= vb
    rec = [x['id'] for x in a['anchors']]
    out.append(f'record ids unique {len(set(rec)) == len(rec)}, pass the Expedition id rule {all(id_ok(i) for i in rec)}'); ok &= len(set(rec)) == len(rec) and all(id_ok(i) for i in rec)
    return ok, out, (col, reach)

# =====================================================================================================================
def plan(path, graphs, scale=7):
    col, reach = graphs; m = game_map(); b_ = m['bounds']
    SX, SZ = 40, 32; PW, PH = 2 * SX * scale, 2 * SZ * scale; W, H = 2 * PW + 30, PH + 110
    cv = Canvas(W, H); d = door_record()
    KC = {'vent': (190, 110, 255), 'cell': (255, 80, 80), 'door': (255, 150, 60), 'drop': (255, 70, 170), 'breach': (255, 235, 60)}
    for panel, yf in ((0, 0.0), (1, L1)):
        ox = 10 + panel * (PW + 10); oy = 46
        def X(x): return int(round((x + SX) * scale)) + ox
        def Y(z): return int(round((SZ - z) * scale)) + oy
        cv.rect(X(-SX), Y(SZ), X(SX), Y(-SZ), (24, 22, 28))
        for n in col.graph:
            if abs(n[2] - yf) < 0.05 or (panel == 0 and 0.05 < n[2] < 5.5) or (panel == 1 and n[2] > L1 + 0.05):
                x, z = col.cell_xz(n[0], n[1]); c = (64, 62, 70) if n in reach else (110, 50, 60)
                if abs(n[2] - yf) >= 0.05: c = (70, 80, 100)
                cv.rect(X(x - 0.25), Y(z + 0.25), X(x + 0.25), Y(z - 0.25), c)
        for r in m['ramps']:
            if (r['high']['y'] > 3) == (panel == 1) or panel == 0: cv.poly([(X(x), Y(z)) for x, z in corners(ramp_block(r))], (90, 170, 230), 0.35)
        for h in SIREN['hazards']:
            for vz in (h['zones'] or [dict(center=h['center'], size=h['size'])]):
                c, s = vz['center'], vz['size']
                if (c['y'] > 4) == (panel == 1): cv.frame(X(c['x'] - s['x'] / 2), Y(c['z'] + s['z'] / 2), X(c['x'] + s['x'] / 2), Y(c['z'] - s['z'] / 2), (120, 120, 130))
        for b in sorted(m['blocks'], key=lambda b: yspan(b)[1]):
            y0, y1 = yspan(b)
            if b['id'] in ('siren-slab', 'siren-vault') or b['id'] in set(m['navHints']['collisionOff']): continue
            if not (y0 < yf + 3 and y1 > yf + 0.05): continue
            c = (230, 150, 40) if b['id'].startswith(MAP) else (130, 136, 150) if y1 - yf > 2 else (150, 112, 74)
            cv.poly([(X(x), Y(z)) for x, z in corners(b)], c)
        for e in ENTRIES:
            if (e['exit']['y'] > 3) != (panel == 1): continue
            x, z = e['exit']['x'], e['exit']['z']; c = KC[e['kind']]
            cv.line(X(e['from']['x']), Y(e['from']['z']), X(x), Y(z), c, 1); cv.disc(X(x), Y(z), 3, c)
        if panel == 0:
            for key, c in (('interact', (80, 220, 255)), ('closure', (255, 255, 120))): cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', d[key]))], c, 0.6)
            for p in ENTRIES_IN:
                cx, cy = X(p['x']), Y(p['z']); fx, fz = forward(p['yaw']); cv.disc(cx, cy, 4, (255, 255, 255)); cv.line(cx, cy, cx + int(fx * 9), cy - int(fz * 9), (255, 255, 255), 2)
            cv.ring(X(d['approach']['x']), Y(d['approach']['z']), 5, (80, 220, 255), 2); cv.disc(X(DOOR[0]), Y(DOOR[1]), 5, (0, 160, 255))
            for p in BOSS_POINTS:
                cx, cy = X(p['x']), Y(p['z']); cv.line(cx - 6, cy - 6, cx + 6, cy + 6, (255, 90, 255), 3); cv.line(cx - 6, cy + 6, cx + 6, cy - 6, (255, 90, 255), 3)
        for i, c in enumerate(CACHES):
            if (c['y'] > 3) == (panel == 1): cv.rect(X(c['x']) - 5, Y(c['z']) - 5, X(c['x']) + 5, Y(c['z']) + 5, (255, 200, 40)); cv.text(X(c['x']) + 7, Y(c['z']) - 4, f'C{i}', (255, 200, 40))
        cv.text(ox, oy - 16, 'GROUND FLOOR  Y 0' if panel == 0 else 'UPPER FLOOR  Y 6', (200, 220, 235))
    cv.text(10, 8, 'EXPEDITION WARD ZERO M1 DRAFT  SIREN MAP + OVERLAY  80 X 64 M  NORTH UP  1 PX = %d CM' % (100 // scale), (232, 251, 255))
    lx, ly = 10, H - 52
    for label, c in [('EXIT DOOR', (0, 160, 255)), ('INTERACT', (80, 220, 255)), ('CLOSURE', (255, 255, 120)), ('ENTRY SLOT', (255, 255, 255)), ('OVERLAY BLOCK', (230, 150, 40)),
                     ('BOSS POINT', (255, 90, 255)), ('CACHE', (255, 200, 40)), ('VENT', KC['vent']), ('CELL', KC['cell']), ('DOOR', KC['door']), ('DROP', KC['drop']),
                     ('BREACH', KC['breach']), ('HAZARD (NOT CARRIED)', (120, 120, 130)), ('RAMP', (90, 170, 230))]:
        cv.rect(lx, ly, lx + 12, ly + 10, c); lx = cv.text(lx + 18, ly, label, (210, 214, 220)) + 18
        if lx > W - 200: lx, ly = 10, ly + 22
    cv.save(path)

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--png'); a = ap.parse_args()
    ok, report, graphs = checks()
    print('\n'.join(report))
    if a.json:
        with open(a.json, 'w') as f: json.dump(draft(), f, indent=None, separators=(',', ':'), sort_keys=False)
        print('wrote', a.json)
    if a.png: plan(a.png, graphs); print('wrote', a.png)
    sys.exit(0 if ok else 1)
