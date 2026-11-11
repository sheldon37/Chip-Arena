# Breakwater M2 (2026-11-11, the rebuild: a storm-battered granite breakwater at blue hour, the lighthouse at its head) draft and checks.
# The small 1v1 duel (2v2 fits): three lanes at three heights along a 44 m mole. North, the wave wall's walk (2.4) over the open sea;
# the deck (0); south, the harbour quay (-2.4). Both long edges are open: the sea kills. Hazard: the wash, two telegraphed seas
# breaking over the wall and across the deck, shoving pilots south toward the edges (kind `vent` with `push`, the existing wind mode).
# Coordinates: x east, z north, y up, metres; Unity frame. Mirror symmetric: x -> -x. Team A west (the harbour gate), team B east
# (the lighthouse's hall).
# usage: python3 docs/arenas/scripts/breakwater_m2.py [--out DIR] [--json PATH]   (stdout: plans + checks, exit 1 on any failed check;
#        --json writes the draft only; --out writes breakwater_m2.json, draft map data in the UnityMaps.json shape, and
#        breakwater_m2.png, three top-down plans (levels -2.4, 0, 2.4), 12 px per metre)
# Design: docs/arenas/BREAKWATER.md. Ids carry the prefixes the presentation keys its looks, kit and art on (breakwater-deck, -wall-*,
# -quay-*, -bastion, -ledge, -pulpit, -turret, -stone, -end-*, -stair-*, -landing-*, -parapet-*, -blocks-*, -boat-*, -bollard-*,
# -lamp-*, -pots-*, -barrel-*, -crate-*). The M1 draft (breakwater_m1.py, the flat pier head) stays as history.
import math, json, itertools, sys, os, zlib, struct
LQ, L0, LW = -2.4, 0.0, 2.4                    # the harbour quay, the deck, the wave wall's walk
X0, X1, Z0, Z1 = -22, 22, -9.5, 8.4            # the mole's extent
# The sim clamps bots, abilities and teleports to the bounds minus the capsule (NativeUnityMaps.Clamp), not walking pilots: the bounds
# run 4 m past both open edges, so a pilot who walks, jumps short or is washed off falls into the sea and dies at killY. killY -5:
# a body's middle below it dies; the sea's surface lies at -3.4, so a pilot goes under before the kill; one standing on the lowest
# floor (the quay, -2.4) is 3.5 m clear of it.
BOUNDS = {'minX': -22, 'maxX': 22, 'minZ': -13.5, 'maxZ': 12.4, 'ceiling': 16, 'killY': -5}
SEA = -3.4
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, plate (a walkable floor: deck, quay, wall walk, landings)
SWAP = {'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True, plate=False):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
def stair(i, xa, xb, z0, z1, ya, yb, steps, base):
    # a solid flight from (xa, ya) to (xb, yb) along x, z0..z1 wide, standing on the floor at `base`
    run = (xb - xa) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1))); add('%s-%02d' % (i, k), a, b, base, ya + rise * (k + 1), z0, z1, '/')

# ---- the mole's masses (7 m deep: their feet stand in the sea): the deck, the wave wall along its north side with the walk on top,
#      the harbour quay along its south side, the bastion (the deck bulging out over the quay at the centre) and the ledge round it
add('breakwater-deck', -22, 22, -7, L0, -5, 5, '.', mat='foundation', mirror=False, plate=True)
add('breakwater-wall-w', -22, -1.6, -7, LW, 5, 8.4, '.', plate=True)
add('breakwater-quay-w', -22, -2.6, -7, LQ, Z0, -5, '.', plate=True)
add('breakwater-bastion', -2.6, 2.6, -7, L0, -8.0, -5, '.', mirror=False, plate=True)
add('breakwater-ledge', -2.6, 2.6, -7, LQ, Z0, -8.0, '.', mirror=False, plate=True)
# the watch turret: a solid stone turret astride the wave wall at the centre (it parts the walk; the pulpit before it joins the two
# flights), and the pulpit, the raised landing in front of it (2.4, the power position over the deck)
add('breakwater-turret', -1.6, 1.6, -7, 6.6, 5, 8.4, 'T', mirror=False)
add('breakwater-pulpit', -3.0, 3.0, 0, LW, 3.4, 5, '.', mirror=False, plate=True)
# ---- the end buildings (west: the harbour gate; east: the lighthouse's hall; the tower itself stands outside the bounds): two rooms
#      each (north and south), their doors (2 m, head 2.6) off the spawns' own lines, a roof at 5
add('breakwater-end-w-back', -22, -21.4, 0, 5, -5, 5, '#')
add('breakwater-end-w-north', -21.4, -16.5, 0, 5, 4.4, 5, '#')
add('breakwater-end-w-south', -21.4, -16.5, 0, 5, -5, -4.4, '#')
add('breakwater-end-w-mid', -21.4, -18.6, 0, 5, -.3, .3, '#')
for k, (a, b) in enumerate(((-4.4, -2.5), (-.5, .5), (2.5, 4.4))): add('breakwater-end-w-front-%d' % k, -17.1, -16.5, 0, 5, a, b, '#')
add('breakwater-end-w-lintel-0', -17.1, -16.5, 2.6, 5, -2.5, -.5, '#'); add('breakwater-end-w-lintel-1', -17.1, -16.5, 2.6, 5, .5, 2.5, '#')
add('breakwater-end-w-roof', -22, -16.5, 5, 5.4, -5, 5, 'R')
# the wings: the end wall carried across the wave wall's walk and the quay (nobody walks off the map's ends)
add('breakwater-end-w-wing-wall', -22, -21.4, LW, 5.4, 5, 8.4, '#')
add('breakwater-end-w-wing-quay', -22, -21.4, LQ, 0, Z0, -5, '#')
# ---- stairs (0.2 m risers, 0.3 m treads, 1.6 m wide, solid): deck to the wall's walk at each end and each side of the pulpit; deck to
#      the quay from a landing at each end
stair('breakwater-stair-wall-w-end', -12.9, -16.5, 3.4, 5, L0, LW, 12, L0)
stair('breakwater-stair-wall-w-mid', -6.6, -3.0, 3.4, 5, L0, LW, 12, L0)
add('breakwater-landing-quay-w', -16.5, -15.0, LQ, L0, -6.6, -5, '.', plate=True)
stair('breakwater-stair-quay-w', -11.4, -15.0, -6.6, -5, LQ, L0, 12, LQ)
# ---- the storm parapet on the wall's seaward edge (1.1 m), breached where the wash comes over
add('breakwater-parapet-w-0', -21.4, -13.6, LW, LW + 1.1, 8.0, 8.4, '-')
add('breakwater-parapet-w-1', -6.4, -1.6, LW, LW + 1.1, 8.0, 8.4, '-')
# ---- the deck's furniture: the Mariners' Stone at the centre (full cover, the duel's pivot), quarried granite blocks (a full stack
#      and a half course: shelter from the wash), an upturned boat on its trestles (half), mooring bollards along the south edge
#      (half), lamp posts (thin), lobster pots and barrels (breakable)
add('breakwater-stone', -1.3, 1.3, 0, 3.4, -1.5, 1.5, 'S', mirror=False)
add('breakwater-blocks-w-0', -10.4, -8.4, 0, 2.6, -1.2, .8, 'G')
add('breakwater-blocks-w-1', -8.4, -7.0, 0, 1.2, -.8, .6, 'g')
add('breakwater-blocks-w-2', -14.6, -13.0, 0, 2.6, -3.4, -1.6, 'G')
add('breakwater-boat-w', -6.2, -3.6, 0, 1.1, -3.9, -2.7, 'b', mat='wood')
for k, x in enumerate((-13.9, -5.2)): add('breakwater-bollard-w-%d' % k, x - .35, x + .35, 0, .9, -4.9, -4.2, 'o', mat='metal')
add('breakwater-lamp-w-0', -11.0, -10.6, 0, 3.3, -4.9, -4.5, 'l', mat='metal')
add('breakwater-lamp-w-1', -3.6, -3.2, 0, 3.3, 2.8, 3.2, 'l', mat='metal')
add('breakwater-pots-w-0', -15.9, -14.9, 0, 1.0, 1.2, 2.2, 'x', mat='wood', role='cover', hp=34)
add('breakwater-pots-w-1', -15.9, -14.9, 0, 1.0, 2.2, 3.2, 'x', mat='wood', role='cover', hp=34)
add('breakwater-barrel-w-0', -4.8, -3.9, 0, 1.1, .2, 1.1, 'x', mat='wood', role='cover', hp=60)
add('breakwater-barrel-w-1', -12.0, -11.1, 0, 1.1, 2.3, 3.2, 'x', mat='wood', role='cover', hp=60)
# the wall's walk: a lamp post by each flight's head, fish crates
add('breakwater-lamp-w-2', -17.6, -17.2, LW, LW + 3.3, 7.6, 8.0, 'l', mat='metal')
add('breakwater-crate-w-0', -5.6, -4.6, LW, LW + 1.0, 6.9, 7.9, 'x', mat='wood', role='cover', hp=34)
# the quay: bollards on the harbour edge, a lamp post, fish crates and a net barrel
for k, x in enumerate((-17.8, -5.4)): add('breakwater-bollard-quay-w-%d' % k, x - .35, x + .35, LQ, LQ + .9, -9.4, -8.7, 'o', mat='metal')
add('breakwater-lamp-w-3', -15.4, -15.0, LQ, LQ + 3.3, -9.4, -9.0, 'l', mat='metal')
add('breakwater-crate-w-1', -7.0, -6.0, LQ, LQ + 1.0, -6.1, -5.1, 'x', mat='wood', role='cover', hp=34)
add('breakwater-crate-w-2', -6.0, -5.0, LQ, LQ + 1.0, -6.1, -5.1, 'x', mat='wood', role='cover', hp=34)
add('breakwater-barrel-w-2', -20.9, -20.0, LQ, LQ + 1.1, -9.2, -8.3, 'x', mat='wood', role='cover', hp=60)

# ---- hazards (UnityMaps.json `hazards[]`; an existing kind). Factory kinds time from the arm time (FightAt + 4 s).
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), radius=0, first=0, start=0, period=0,
             offset=0, warn=0, danger=0, speed=0, length=0, charge=0, live=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
# The wash (kind `vent` with damage ticks and `push` along `axis`, the Causeway's "wind" mode): a sea breaks over the wave wall where
# its parapet is breached and runs across the walk, the deck and the quay, x 7..13 on each half, from the quay's floor to 2.2 m over
# the walk. A 3 s telegraph (the sea draws back and heaps against the wall, the bell tolls, foam lines run on the stone), a 2.2 s
# wash: 1 HP per 0.25 s tick and 13 m/s^2 south, toward the deck's edge, the quay and the harbour (walk into it to hold, or stand
# in the lee of the granite blocks). The two halves alternate, 11 s apart; each every 22 s.
WASH = (7.0, 13.0)
HAZ = []
for side, (x0, x1, off) in enumerate(((-WASH[1], -WASH[0], 0.0), (WASH[0], WASH[1], 11.0))):
    HAZ.append(hz(id='breakwater-wash-%s' % 'we'[side], kind='vent', a=v((x0 + x1) / 2, LQ, (Z0 + Z1) / 2), size=v(x1 - x0, 7.0, Z1 - Z0), axis=v(0, 0, -1),
                  start=0, offset=off, period=22, warn=3.0, dwell=2.2, tick=.25, damage=1, push=13, pushMode='wind'))

# ---- spawns (x, z, floor); yaw from +z toward +x, facing the origin
teamA = [(-19.9, 3.6, L0), (-19.9, -3.6, L0), (-18.2, 3.6, L0), (-18.2, -3.6, L0)]
teamB = [(-x, z, y) for x, z, y in teamA]
ffa_names = ['A1', 'B1', 'A2', 'B2', 'q', 'Q', 'w', 'W']
_q, _w = (-19.3, -6.9, LQ), (-19.6, 6.6, LW)
ffa = [teamA[0], teamB[0], teamA[1], teamB[1], _q, (-_q[0], _q[1], _q[2]), _w, (-_w[0], _w[1], _w[2])]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only exclusion columns (infinite upward). The integration (tools/generate_stage_hazards.py open_stage) keeps for everyone
#      only the end buildings' (EDGES: nobody leaves the map over a roof) and the turret's and the Stone's (OVERHANG: the art stands on
#      them), and for bots only those at a lethal drop (the storm parapet)
def pilot_for(e):
    x0, x1, y0, y1, z0, z1 = e['box']; return dict(id=e['id'] + '-top', center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .1, 3))
def needs_pilot(e): return e['ch'] in '-TSR' or e['id'].endswith(('-wing-wall', '-wing-quay'))
PILOT = [pilot_for(e) for e in els if needs_pilot(e)]

# ---- level bookkeeping
LEVELS = [(LQ, 'level -2.4 (the harbour quay)'), (L0, 'level 0 (the deck)'), (LW, 'level 2.4 (the wave wall, the pulpit)')]
def base_of(e):
    y0 = e['box'][2]
    return LW if y0 >= LW - .05 else L0 if y0 >= -.05 else LQ if y0 >= LQ - .05 else y0
def floor_at(e, h): return abs(e['box'][3] - h) < .05 and (e['plate'] or e['ch'] == '/')
def in_slice(e, h): y0, y1 = e['box'][2], e['box'][3]; return y0 < h + 2.0 and y1 > h + .05
def footprint(hd):
    """Ground footprints of every hazard's live volume: (x0, x1, z0, z1, y0, y1, name)."""
    return [(h['a']['x'] - h['size']['x'] / 2, h['a']['x'] + h['size']['x'] / 2, h['a']['z'] - h['size']['z'] / 2, h['a']['z'] + h['size']['z'] / 2, h['a']['y'], h['a']['y'] + h['size']['y'], h['id']) for h in hd]

def paint():
    W = int((X1 - X0) * 2); R = int(BOUNDS['maxZ'] - BOUNDS['minZ'] + 1); out = []
    for h, name in LEVELS:
        g = [[' '] * W for _ in range(R)]
        def put(x, z, ch):
            c = int(math.floor((x - X0) * 2)); r = int(math.floor(BOUNDS['maxZ'] - z))
            if 0 <= c < W and 0 <= r < R: g[r][c] = ch
        def rect(x0, x1, z0, z1, ch):
            zz = z0
            while zz < z1 - 1e-9:
                xx = x0
                while xx < x1 - 1e-9: put(xx + .1, zz + .1, ch); xx += .25
                zz += .25
        for e in els:
            if floor_at(e, h): b = e['box']; rect(b[0], b[1], b[4], b[5], '/' if e['ch'] == '/' else '.')
        for x0, x1, z0, z1, y0, y1, nm in footprint(HAZ):
            if y0 - .05 <= h + .1 <= y1:
                for z in (z0 + .5, z1 - .5): rect(x0, x1, z, z + .25, '~')
        for e in sorted(els, key=lambda e: e['box'][3]):
            if in_slice(e, h) and not e['plate'] and e['ch'] != '/' and e['box'][2] >= h - .05: b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
        for nm, (x, z, y) in [('1234'[i], p) for i, p in enumerate(teamA)] + [('5678'[i], p) for i, p in enumerate(teamB)] + list(zip(ffa_names[4:], ffa[4:])):
            if abs(y - h) < .05: put(x, z, nm)
        out.append(name + '   (1 char = 0.5 m x 1 m)')
        for r in range(R): z = BOUNDS['maxZ'] - r; out.append(f'{z-1:+6.1f}..{z:+5.1f} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- spawn sightlines in 3D: a box blocks where the segment passes through it. Boxes topping out below 2.0 m over the lower eye's
#      floor never count, nor boxes under 2.0 m over their own level (half cover hides no standing pilot); floor plates do.
def seg_hits(a, b, e, lo):
    x0, x1, y0, y1, z0, z1 = e['box']
    if e['role'] != 'static': return False                       # breakables hide nobody for long
    if y1 < lo + 2.0 or (not e['plate'] and y1 < base_of(e) + 2.0): return False
    t0, t1 = 0.0, 1.0
    for o, d, l, hgh in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], y0, y1), (a[2], b[2] - a[2], z0, z1)):
        if abs(d) < 1e-9:
            if not (l < o < hgh): return False
        else:
            ta, tb = (l - o) / d, (hgh - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        lo = min(a[2], b[2])
        opens = []
        for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4):
            for eye in (1.2, 1.6, 1.9):
                p, q = (a[0] + dx, a[2] + eye, a[1] + dz), (b[0] + ex, b[2] + eye, b[1] + ez)
                if not any(seg_hits(p, q, e, lo) for e in els): opens.append((p, q))
        if opens:
            p, q = opens[len(opens) // 2]
            bad.append(f'{na}-{nb} ({len(opens)}/243 variants open, {math.dist(a[:2], b[:2]):.1f} m; e.g. ({p[0]:.1f},{p[1]:.1f},{p[2]:.1f})->({q[0]:.1f},{q[1]:.1f},{q[2]:.1f}))')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights (3D)')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- reachability: a 0.25 m raster of standable tops (1.8 m headroom, the 0.42 m capsule ring clear from top+0.45 to top+1.8, no
#      pilot column over the body); neighbours join when |dy| <= 0.25
CELL, CAP = .25, .42
def raster():
    nx, nz = int((BOUNDS['maxX'] - BOUNDS['minX']) / CELL), int((BOUNDS['maxZ'] - BOUNDS['minZ']) / CELL)
    buck = {}
    def cells(x0, x1, z0, z1):
        for i in range(max(0, int((x0 - BOUNDS['minX']) / CELL)), min(nx, int((x1 - BOUNDS['minX']) / CELL) + 1)):
            for j in range(max(0, int((z0 - BOUNDS['minZ']) / CELL)), min(nz, int((z1 - BOUNDS['minZ']) / CELL) + 1)): yield i, j
    for e in els:
        b = e['box']
        for c in cells(b[0] - CAP, b[1] + CAP, b[4] - CAP, b[5] + CAP): buck.setdefault(c, []).append(e)
    pil = [(p['center']['x'] - p['size']['x'] / 2 - CAP, p['center']['x'] + p['size']['x'] / 2 + CAP, p['center']['z'] - p['size']['z'] / 2 - CAP, p['center']['z'] + p['size']['z'] / 2 + CAP, p['bottom']) for p in PILOT]
    pbuck = {}
    for p in pil:
        for c in cells(*p[:4]): pbuck.setdefault(c, []).append(p)
    def standable(cx, cz, t, local, plocal):
        for e in local:
            x0, x1, y0, y1, z0, z1 = e['box']
            if y0 < t + 1.8 and y1 > t + .01 and x0 < cx < x1 and z0 < cz < z1: return False
            if y0 < t + 1.8 and y1 > t + .45 and x0 - CAP < cx < x1 + CAP and z0 - CAP < cz < z1 + CAP: return False
        for x0, x1, z0, z1, bot in plocal:
            if t + 1.95 >= bot and x0 < cx < x1 and z0 < cz < z1: return False
        return True
    tops = {}
    for i in range(nx):
        for j in range(nz):
            cx, cz = BOUNDS['minX'] + (i + .5) * CELL, BOUNDS['minZ'] + (j + .5) * CELL; local = buck.get((i, j), []); plocal = pbuck.get((i, j), [])
            ts = sorted({round(e['box'][3], 3) for e in local if e['box'][0] < cx < e['box'][1] and e['box'][4] < cz < e['box'][5]})
            ok = [t for t in ts if standable(cx, cz, t, local, plocal)]
            if ok: tops[(i, j)] = ok
    def cell_of(x, z): return int((x - BOUNDS['minX']) / CELL), int((z - BOUNDS['minZ']) / CELL)
    i, j = cell_of(teamA[0][0], teamA[0][1]); start = (i, j, round(teamA[0][2], 3))
    seen = {start} if start[2] in tops.get((i, j), []) else set(); todo = list(seen)
    while todo:
        i, j, t = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            for t2 in tops.get((i + di, j + dj), ()):
                n = (i + di, j + dj, t2)
                if abs(t2 - t) <= .25 + 1e-6 and n not in seen: seen.add(n); todo.append(n)
    return tops, seen, cell_of, standable, buck, pbuck

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)),
                'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], round(p[2] + 1.1, 3), p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'breakwater', 'name': 'Breakwater', 'tag': 'Granite, iron and the sea. The lamp turns; the tide does not care.', 'theme': 'breakwater',
                      'music': 'breakwater-theme', 'ambience': 'breakwater-storm', 'look': 'breakwater-blue-hour', 'sky': 0x1E2C4C, 'floor': 0x5A626C, 'accent': 0xFFC878,
                      'rotation': True, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=12):
    PW, GAP = int((X1 - X0 + 2) * scale), 2 * scale; PH = int((BOUNDS['maxZ'] - BOUNDS['minZ']) * scale); W, H = PW, 3 * PH + 2 * GAP
    img = bytearray(b'\x14\x20\x30' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        oy = k * (PH + GAP); i0, i1 = int((x0 - X0 + 1) * scale), int((x1 - X0 + 1) * scale); j0, j1 = oy + int((BOUNDS['maxZ'] - z1) * scale), oy + int((BOUNDS['maxZ'] - z0) * scale)
        for j in range(max(oy, j0), min(oy + PH, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    col = {'#': (150, 150, 158), 'R': (110, 110, 120), 'T': (190, 180, 160), 'S': (220, 210, 190), 'G': (170, 170, 176), 'g': (150, 150, 156), 'b': (150, 110, 70),
           'o': (70, 74, 84), 'l': (255, 200, 120), 'x': (190, 140, 80), '-': (200, 196, 180), '/': (128, 132, 140)}
    for k, (h, name) in enumerate(LEVELS):
        for e in sorted(els, key=lambda e: e['box'][3]):
            b = e['box']
            if e['plate'] and b[3] <= h + .05: fill(k, b[0], b[1], b[4], b[5], (104, 110, 120) if abs(b[3] - h) < .05 else (64, 70, 80))
        for e in els:
            if e['ch'] == '/' and e['box'][3] <= h + .05 and e['box'][3] > h - 2.45: b = e['box']; s = int(96 + 40 * (b[3] - h + 2.4) / 2.4); fill(k, b[0], b[1], b[4], b[5], (s, s + 4, s + 12))
        for x0, x1, z0, z1, y0, y1, nm in footprint(HAZ):
            if y0 - .05 <= h + .1 <= y1: fill(k, x0, x1, z0, z1, (120, 200, 255), .3)
        for e in sorted(els, key=lambda e: e['box'][3]):
            if in_slice(e, h) and not e['plate'] and e['ch'] != '/' and e['box'][2] >= h - .05: b = e['box']; fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .6 if e['box'][3] - e['box'][2] < 1.25 else 1.0)
        for i, p in enumerate(teamA + teamB):
            if abs(p[2] - h) < .05: fill(k, p[0] - .4, p[0] + .4, p[1] - .4, p[1] + .4, (60, 200, 255) if i < 4 else (255, 90, 120))
        for p in ffa[4:]:
            if abs(p[2] - h) < .05: fill(k, p[0] - .4, p[0] + .4, p[1] - .4, p[1] + .4, (250, 250, 250))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint()); fails = []
    def need(ok, what):
        if not ok: fails.append(what)
        return ok
    ids = [e['id'] for e in els]; need(len(ids) == len(set(ids)), 'unique ids'); need(all(i.startswith('breakwater-') for i in ids), 'id prefix')
    # mirror symmetry: every block has its twin across x = 0
    boxes = {tuple(round(q, 3) for q in e['box']) for e in els}
    need(all((round(-b[1], 3), round(-b[0], 3)) + b[2:] in boxes for b in boxes), 'mirror symmetry')
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    need(bad == 0, 'spawn sightlines')
    sp = ffa + teamA + teamB
    mf = min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)); mab = min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m (3D); min A-B spacing %.1f m' % (mf, mab)); need(mf >= 3.8, 'FFA spacing')   # (the nearest pairs stand on different floors, a 5 m wall and a 14 m walk apart); need(mab >= 24, 'A-B spacing')
    print('FFA floors: ' + ', '.join('%s %.1f' % (n, p[2]) for n, p in named))
    worst = min((dist_rect(p, r[:4]), r[6], p) for p in sp for r in footprint(HAZ) if r[4] - .1 <= p[2] + 1.8 and r[5] >= p[2])
    print('closest spawn to a hazard volume on its floor: %.1f m (%s, spawn %s)' % worst); need(worst[0] >= 3, 'hazard clearance')
    inb = all(BOUNDS['minX'] + .5 <= x <= BOUNDS['maxX'] - .5 and BOUNDS['minZ'] + 4 <= z <= BOUNDS['maxZ'] - 4 for x, z, y in sp)
    print('all spawns inside the bounds (4 m from the open edges):', inb); need(inb, 'bounds')
    past = [e['id'] for e in els if e['box'][4] < BOUNDS['minZ'] + 4 - 1e-6 or e['box'][5] > BOUNDS['maxZ'] - 4 + 1e-6 or e['box'][0] < BOUNDS['minX'] - 1e-6 or e['box'][1] > BOUNDS['maxX'] + 1e-6]
    low_top = min(e['box'][3] for e in els if e['plate'])
    print('blocks within 4 m of the open bounds (the sea must run 4 m past both edges): %s; lowest floor %.1f, the sea %.1f, killY %d' % (past or 'none', low_top, SEA, BOUNDS['killY']))
    need(not past and low_top + .9 - 2 > BOUNDS['killY'] and SEA < low_top - .5 and SEA > BOUNDS['killY'] + .9, 'open edges')
    band = [e['id'] for e in els if e['ch'] not in '-/' and not e['plate'] and 1.3 < e['box'][3] - base_of(e) < 2.2]
    print('blocks topping out 1.3..2.2 m over their level:', band or 'none'); need(not band, 'cover band')
    pil_ids = {p['id'] for p in PILOT}; miss = [e['id'] for e in els if needs_pilot(e) and e['id'] + '-top' not in pil_ids]
    print('pilot blocks: %d; without one: %s' % (len(PILOT), miss or 'none')); need(not miss, 'pilot blocks')
    flights = {}
    for e in els:
        if e['ch'] == '/': flights.setdefault(e['id'].rsplit('-', 1)[0], []).append(e['box'])
    def flight_bad(bs):
        tops = sorted(round(b[3], 3) for b in bs); steps = [b - a for a, b in zip(tops, tops[1:])]
        return max(steps) > .2 + 1e-6 or min(max(b[1] - b[0], b[5] - b[4]) for b in bs) < 1.6 - 1e-6 or tops[-1] not in (L0, LW)
    bad_st = [f for f, bs in flights.items() if flight_bad(bs)]
    print('stairs: %d flights, risers <= 0.2 m, >= 1.6 m wide, ending flush with a level; bad: %s' % (len(flights), bad_st or 'none')); need(not bad_st, 'stairs')
    tops, seen, cell_of, standable, buck, pbuck = raster()
    for h, name in LEVELS:
        tot = sum(1 for c, ts in tops.items() for t in ts if abs(t - h) < .05); r = sum(1 for c in seen if abs(c[2] - h) < .05)
        print('raster %s: %d of %d standable cells reachable from A1' % (name, r, tot)); need(r > 0, 'reach ' + name)
    unreached = []
    for n, p in list(zip(ffa_names, ffa)) + A + B:
        i, j = cell_of(p[0], p[1]); ok = (i, j, round(p[2], 3)) in seen and standable(p[0], p[1], p[2], buck.get((i, j), []), pbuck.get((i, j), []))
        if not ok: unreached.append(n)
    for e in els:
        if e['plate']:
            b = e['box']; (i0, j0), (i1, j1) = cell_of(b[0], b[4]), cell_of(b[1], b[5])
            if not any((i, j, round(b[3], 3)) in seen for i in range(i0, i1 + 1) for j in range(j0, j1 + 1)): unreached.append(e['id'])
    print('every spawn and walkable plate reachable on foot from A1:', unreached or 'yes'); need(not unreached, 'reachability')
    # hazard timing: nothing live in the first 4 s; the telegraph at least today's vents' (0.8 s)
    for h in HAZ:
        first = 4 + h['start'] + h['offset'] + h['period'] - h['warn'] - h['dwell']; need(h['warn'] >= .8 and first >= 4 and h['push'] > 0 and h['damage'] > 0, 'wash timing ' + h['id'])
        print('  %-18s %-6s first telegraph at FightAt + %5.2f s, warn %.1f s, wash %.1f s, period %.0f s' % (h['id'], h['kind'], first, h['warn'], h['dwell'], h['period']))
    print('blocks: %d (breakable %d); hazards: %d; pilot blocks: %d; spawns: %d FFA + 2 x %d team' % (len(els), sum(e['role'] != 'static' for e in els), len(HAZ), len(PILOT), len(ffa), len(teamA))); need(len(els) <= 350, 'block budget')
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'breakwater_m2.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'breakwater_m2.png')); print('wrote', os.path.join(out, 'breakwater_m2.json'), 'and breakwater_m2.png')
    print('FAILED: ' + ', '.join(fails) if fails else 'all checks pass')
    sys.exit(1 if fails else 0)
