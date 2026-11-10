# Top Dog Emporium (emporium): a glass-roofed hot dog palace at golden hour, 56x42 m, floor + bun bridges (2.4) + two
# sauce balconies (3.6). Draft painter, 3D spawn LOS, raster reachability, hazard clearance and timing checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z); ids swap n/s, e/w, a/b.
# usage: python3 docs/arenas/scripts/emporium_m1.py [--out DIR] [--json PATH]   (stdout: plans + checks; --json writes the draft only;
#        --out writes emporium_m1.json, draft map data in the UnityMaps.json shape, and emporium_m1.png, the three level plans)
# Integrated through unity/ChipArenaConversion/tools/generate_stage_hazards.py (navigation, music, rotation). Ids carry the
# prefixes the presentation keys its kit on (emporium-oven-*, -booth-*, -counter-*, -press-*, -bridge-*, -balcony-*, ...).
# The hazards use existing kinds only (docs/arenas/EMPORIUM.md):
#   the Wiener Run  : two `maglev` sweeps (a giant rolling wiener in each roller-grill lane, z = +-8, between the ovens);
#   the Bun Presses : two `press` rams (a giant sesame top bun slams the zone between four posts);
#   the Griddles    : four `burner` hazards, two checkerboard pairs per griddle that go hot in turns;
#   the Geysers     : four `vent` columns (mustard and ketchup) with `push`: a burst launches whoever stands in it (and a dab
#                     of damage). The vent push rule is the one rules addition (NativeStageHazards.VentLaunch, for Astra).
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -29, 29, -23, 23
BOUNDS = {'minX': -29, 'maxX': 29, 'minZ': -23, 'maxZ': 23, 'ceiling': 16, 'killY': -6}
FL, BR, BAL = 0.0, 2.4, 3.6                 # floor, bun bridge deck, balcony deck tops
LANE_Z, LANE_W, LANE_X = 10.0, 4.0, 18.0     # roller-grill lanes: centre |z|, width, half length (oven face to oven face)
CAP = .42
els = []; PILOT = []
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def v(x, y, z): return {'x': round(x, 3), 'y': round(y, 3), 'z': round(z, 3)}
def mbox(b): x0, x1, y0, y1, z0, z1 = b; return (-x1, -x0, y0, y1, -z1, -z0)
def pilot(i, b):
    PILOT.append(dict(id=i.replace('emporium-', 'emporium-top-', 1), center=v((b[0] + b[1]) / 2, 0, (b[4] + b[5]) / 2), size=v(b[1] - b[0], 0, b[5] - b[4]), bottom=round(b[3] - .1, 3)))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='metal', role='static', hp=0, mirror=True, top=False):
    b = (x0, x1, y0, y1, z0, z1); els.append(dict(id=i, mat=mat, role=role, box=b, ch=ch, hp=hp))
    if top: pilot(i, b)
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=mbox(b), ch=ch, hp=hp))
        if top: pilot(m, mbox(b))
def stair(i, a, b, w0, w1, ya, yb, steps, base, axis='x', mirror=True):
    run = (b - a) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        s0, s1 = sorted((a + run * k, a + run * (k + 1))); y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), s0, s1, base, y1, w0, w1, '/', mirror=mirror)
        else: add('%s-%02d' % (i, k), w0, w1, base, y1, s0, s1, '/', mirror=mirror)
def crate(i, x0, z0, layers=1, mirror=True):
    for l in range(layers): add('%s-%d' % (i, l), x0, x0 + 1.2, 1.2 * l, 1.2 * (l + 1), z0, z0 + 1.2, 'x', mat='wood', role='cover', hp=34, mirror=mirror)

FLOOR = dict(id='emporium-floor', mat='foundation', role='static', box=(X0 - .4, X1 + .4, -.5, 0, Z0 - .4, Z1 + .4), ch='.', hp=0)
# ---- the hall: tiled walls 8 m to the glazed roof (unstandable); the shop windows, roof and street are backdrop
add('emporium-wall-n', X0 - .4, X1 + .4, 0, 8, Z1, Z1 + .4, '#', mat='stone', top=True)
add('emporium-wall-w', X0 - .4, X0, 0, 8, Z0, Z1, '#', mat='stone', top=True)
# ---- the Wiener Ovens: the lanes' portals, 4 m rotisserie cabinets at either end of each roller-grill lane (unstandable)
OZ0, OZ1 = LANE_Z - LANE_W / 2 - .6, LANE_Z + LANE_W / 2 + .6
add('emporium-oven-n-w', -22.6, -LANE_X, 0, 4.0, OZ0, OZ1, 'O', top=True)
add('emporium-oven-n-e', LANE_X, 22.6, 0, 4.0, OZ0, OZ1, 'O', top=True)
# ---- team A's Ketchup Kitchen: the pocket in the west aisle, sealed for sight by three baffles. The kitchen wall (flush with
#      the ovens) has one door into the airlock between the ovens, where the fryer bank stands in front of it; the shelving north
#      and south leaves an aisle exit each side, each turned by a baffle rack against the oven's west face.
add('emporium-kwall-a-n', -22.6, -22.2, 0, 4.0, 1.45, OZ0, 'K', top=True)
add('emporium-kwall-a-s', -22.6, -22.2, 0, 4.0, -OZ0, -1.45, 'K', top=True)
add('emporium-fryer-a', -19.4, -18.0, 0, 4.0, -4.0, 4.0, 'F', top=True)
add('emporium-shelf-a-n', X0, -25.5, 0, 4.0, 4.6, 5.2, 'S', top=True)
add('emporium-shelf-a-s', X0, -25.5, 0, 4.0, -5.2, -4.6, 'S', top=True)
add('emporium-baffle-a-n', -26.0, -22.6, 0, 4.0, 7.6, 8.8, 'S', top=True)
add('emporium-baffle-a-s', -26.0, -22.6, 0, 4.0, -8.8, -7.6, 'S', top=True)
# ---- the kitchen line (mid band): the Big Dog plinth under the turning sign, two bun presses, two griddles, four condiment towers
add('emporium-plinth', -1.5, 1.5, 0, 6.0, -1.4, 1.4, 'C', mirror=False, top=True)
PW = -6.5                                                                   # press-w ram centre x (press-e is its mirror)
for sx, tx in ((-1, 'w'), (1, 'e')):
    for sz, tz in ((-1, 's'), (1, 'n')):
        x0 = PW + sx * 1.6; x1 = PW + sx * 2.1; z0 = sz * 1.6; z1 = sz * 2.1
        add('emporium-press-w-post-%s-%s' % (tz, tx), min(x0, x1), max(x0, x1), 0, 6.6, min(z0, z1), max(z0, z1), 'P')
add('emporium-press-w-head', PW - 2.1, PW + 2.1, 6.6, 7.6, -2.1, 2.1, 'P', top=True)
add('emporium-tower-n-w', -11.0, -8.8, 0, 6.0, 4.6, 6.8, 'T', top=True)    # condiment towers: giant ketchup (west) / mustard (east) bottles
add('emporium-tower-s-w', -11.0, -8.8, 0, 6.0, -6.8, -4.6, 'T', top=True)
add('emporium-prep-n-w', -16.6, -15.8, 0, 1.1, 3.6, 6.4, 'p')               # prep tables by the airlock mouths (half cover)
crate('emporium-crate-mid-n-w', -14.0, 5.4, 1)                               # bun crates: breakable half cover
crate('emporium-crate-mid-s-w', -15.0, -5.6, 2)
# ---- the bun bridges: a deck over each lane at 2.4 m (the wiener rolls under it), a 12-riser flight down each side
BX0, BX1 = -4.4, -2.4
BZ0, BZ1 = LANE_Z - LANE_W / 2 - .2, LANE_Z + LANE_W / 2 + .2
add('emporium-bridge-n', BX0, BX1, 2.2, BR, BZ0, BZ1, '=')
stair('emporium-bridge-n-flight-s', BZ0 - 4.2, BZ0, BX0, BX1, 0, BR, 12, 0, axis='z')
stair('emporium-bridge-n-flight-n', BZ1 + 4.2, BZ1, BX0, BX1, 0, BR, 12, 0, axis='z')
# ---- the north diner: the sauce balcony at 3.6 over the booths, its two stairs along the wall, the counter, the soda fountain
#      and the jukebox; the condiment geysers at the balcony's foot launch pilots up to it through the gaps in its rail
BF = Z1 - 4.0                                                               # the balcony's front edge
add('emporium-balcony-n', -14.4, 14.4, 3.3, BAL, BF, Z1, '_')
for k, x in enumerate((-14.0, -7.0, 0.0, 7.0, 14.0)): add('emporium-balcony-n-post-%d' % k, x - .2, x + .2, 0, 3.3, BF, BF + .4, 'I')
for k, (x0, x1) in enumerate(((-14.4, -11.4), (-8.6, 8.6), (11.4, 14.4))): add('emporium-rail-balcony-n-%d' % k, x0, x1, BAL, BAL + 1.1, BF, BF + .12, 'r')
stair('emporium-stair-n-w', -21.6, -14.4, Z1 - 2.0, Z1, 0, BAL, 18, 0)
stair('emporium-stair-n-e', 21.6, 14.4, Z1 - 2.0, Z1, 0, BAL, 18, 0)
for k, c in enumerate((-12.4, -7.6, -2.8, 2.0, 6.8, 11.2)):                 # diner booths: two high-backed benches and the table between
    add('emporium-booth-n-%d-back-n' % k, c - .9, c + .9, 0, 1.1, Z1 - .5, Z1, 'b')
    add('emporium-booth-n-%d-back-s' % k, c - .9, c + .9, 0, 1.1, Z1 - 3.0, Z1 - 2.5, 'b')
    add('emporium-booth-n-%d-table' % k, c - .6, c + .6, 0, .78, Z1 - 2.25, Z1 - .75, 't')
add('emporium-counter-n', 0.4, 9.0, 0, 1.1, BF - 4.6, BF - 3.6, 'c')
add('emporium-soda-n', 14.6, 16.8, 0, 2.6, BF - 6.0, BF - 4.4, 'D')
add('emporium-jukebox-n', -13.2, -11.8, 0, 2.6, BF - 5.8, BF - 4.6, 'J')
add('emporium-pump-n-w', -12.2, -11.0, 0, 3.0, BF - 2.2, BF - 1.0, 'U')    # condiment pump-dispensers beside the geysers
add('emporium-pump-n-e', 11.0, 12.2, 0, 3.0, BF - 2.2, BF - 1.0, 'U')
add('emporium-kiosk-n', -7.4, -3.8, BAL, BAL + 2.6, BF + .6, BF + 1.4, 'N')     # a neon menu kiosk on the balcony (full cover)
add('emporium-booth-balcony-n-0', -11.6, -9.8, BAL, BAL + 1.1, Z1 - 1.8, Z1, 'b')
add('emporium-booth-balcony-n-1', 3.0, 4.8, BAL, BAL + 1.1, Z1 - 1.8, Z1, 'b')
add('emporium-freezer-n-w', X0, -25.4, 0, 2.6, 13.4, 16.4, 'Z')
add('emporium-freezer-n-e', 25.4, X1, 0, 2.6, 13.4, 16.4, 'Z')
add('emporium-fridge-n-e', 22.4, 23.6, 0, 2.6, 17.0, Z1 - 2.4, 'Z')
add('emporium-barrel-n-w', -24.4, -23.2, 0, 1.0, 18.0, 19.2, 'o', role='cover', hp=150)    # pickle barrels
crate('emporium-crate-n-e', 22.4, 13.6, 2)

# ---- hazards (UnityMaps.json `hazards[]`), every field written
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), axis=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), first=0, period=0, offset=0, warn=0,
             danger=0, speed=0, length=0, charge=0, live=0, tick=0, damage=0, radius=0, start=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
def mv(p): return v(-p['x'], p['y'], -p['z'])
WIENER = 1.8                                                                # the rolling wiener's diameter (its x extent and height)
LANE_N = hz(id='emporium-lane-n', kind='maglev', center=v(0, WIENER / 2 + .05, LANE_Z), size=v(2 * LANE_X, WIENER + .1, LANE_W), axis=v(1, 0, 0),
            first=14, period=24, warn=6, danger=3, speed=11, length=WIENER, lethal=True)
LANE_S = dict(LANE_N, id='emporium-lane-s', center=mv(LANE_N['center']), first=26)
PRESS_W = hz(id='emporium-press-w', kind='press', a=v(PW, 5.9, 0), b=v(PW, .5, 0), size=v(3.0, 1.0, 3.0), start=2, period=12, offset=0,
             warn=1.8, rise=.32, dwell=1.0, travel=1.6, lethal=True)
PRESS_E = dict(PRESS_W, id='emporium-press-e', a=mv(PRESS_W['a']), b=mv(PRESS_W['b']), offset=6)
def grill(id, cells, first):
    zs = [{'center': v(x, .9, z), 'size': v(2.4, 1.8, 2.4)} for x, z in cells]
    xs = [c[0] for c in cells]; zz = [c[1] for c in cells]
    return hz(id=id, kind='burner', center=v((min(xs) + max(xs)) / 2, .9, (min(zz) + max(zz)) / 2), size=v(max(xs) - min(xs) + 2.4, 1.8, max(zz) - min(zz) + 2.4),
              axis=v(1, 0, 0), zones=zs, first=first, period=8, offset=0, warn=1.5, live=2.0, dwell=1.0, tick=.25, damage=8)
GW = -13.6
G_WA = grill('emporium-griddle-w-a', [(GW - 1.2, 1.2), (GW + 1.2, -1.2)], 6)
G_WB = grill('emporium-griddle-w-b', [(GW - 1.2, -1.2), (GW + 1.2, 1.2)], 10)
def gmirror(g, id): return dict(g, id=id, center=mv(g['center']), zones=[{'center': mv(z['center']), 'size': z['size']} for z in g['zones']])
G_EB = gmirror(G_WA, 'emporium-griddle-e-b'); G_EA = gmirror(G_WB, 'emporium-griddle-e-a')
GEY_NW = hz(id='emporium-geyser-n-w', kind='vent', a=v(-10.0, 0, BF - 1.6), size=v(1.6, 4.0, 1.6), start=1, period=10, offset=0, warn=1.4, dwell=1.1, damage=3, tick=.5, push=15)
GEY_NE = dict(GEY_NW, id='emporium-geyser-n-e', a=v(10.0, 0, BF - 1.6), offset=5)
GEY_SE = dict(GEY_NW, id='emporium-geyser-s-e', a=mv(GEY_NW['a']))
GEY_SW = dict(GEY_NE, id='emporium-geyser-s-w', a=mv(GEY_NE['a']))
HAZ = [LANE_N, LANE_S, PRESS_W, PRESS_E, G_WA, G_WB, G_EA, G_EB, GEY_NW, GEY_NE, GEY_SE, GEY_SW]

# ---- spawns (x, z, floor top); yaw faces the origin. Team A in the Ketchup Kitchen pocket, B the mirror (the Mustard Kitchen).
teamA = [(-27.4, 2.4, FL), (-27.4, -2.4, FL), (-25.0, 1.4, FL), (-25.0, -1.4, FL)]
teamB = [(-x, -z, f) for x, z, f in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (-5.6, 21.9, BAL), (5.6, -21.9, BAL), (27.0, 18.2, FL), (-27.0, -18.2, FL), (13.0, 21.9, FL), (-13.0, -21.9, FL)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

def hazard_rects():
    """Plan footprints of every hazard volume (x0, x1, z0, z1, name, y0, y1)."""
    out = []
    for h in HAZ:
        if h['kind'] in ('maglev', 'burner'):
            c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, h['id'], c['y'] - s['y'] / 2, c['y'] + s['y'] / 2))
        elif h['kind'] == 'press':
            a, b, s = h['a'], h['b'], h['size']; out.append((a['x'] - s['x'] / 2, a['x'] + s['x'] / 2, a['z'] - s['z'] / 2, a['z'] + s['z'] / 2, h['id'], b['y'] - s['y'] / 2, a['y'] + s['y'] / 2))
        else:
            a, s = h['a'], h['size']; out.append((a['x'] - s['x'] / 2, a['x'] + s['x'] / 2, a['z'] - s['z'] / 2, a['z'] + s['z'] / 2, h['id'], a['y'], a['y'] + s['y']))
    return out

# ---- 3D sight lines (eyes 1.2/1.6/1.9 m over each spawn's own floor): a box blocks where the segment passes through it; boxes
#      topping out under 2.0 m above the lower floor do not count (half cover does not hide a standing pilot); rails never do.
def seg_hits(a, b, e, low):
    x0, x1, y0, y1, z0, z1 = e['box']
    if e['ch'] == 'r' or y1 < low + 2.0: return False
    t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], y0, y1), (a[2], b[2] - a[2], z0, z1)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
SOLID = [e for e in [FLOOR] + els if e['ch'] != 'r']
def visible(a, b, low, blocks=None):
    bx0, bx1, bz0, bz1 = min(a[0], b[0]), max(a[0], b[0]), min(a[2], b[2]), max(a[2], b[2])
    for e in blocks or SOLID:
        x0, x1, y0, y1, z0, z1 = e['box']
        if x1 < bx0 or x0 > bx1 or z1 < bz0 or z0 > bz1: continue
        if seg_hits(a, b, e, low): return False
    return True
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        low = min(a[2], b[2])
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9)
                if visible((a[0] + dx, a[2] + eye, a[1] + dz), (b[0] + ex, b[2] + eye, b[1] + ez), low))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a[:2], b[:2]):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights (3D, per-floor eyes)')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- 3D raster: 0.25 m cells; a cell is standable on a block top if nothing sits on that top, 1.8 m headroom and no block in the
#      capsule ring between top+0.45 and top+1.8; neighbours join when |dy| <= 0.25. Flood fill from spawn A1.
CELL = .25; NX, NZ = int((X1 - X0) / CELL), int((Z1 - Z0) / CELL)
def raster():
    cols = {}
    for e in [FLOOR] + els:
        x0, x1, y0, y1, z0, z1 = e['box']
        for i in range(max(0, int((x0 - CAP - X0) / CELL) - 1), min(NX, int((x1 + CAP - X0) / CELL) + 2)):
            cx = X0 + (i + .5) * CELL; dx = max(x0 - cx, 0, cx - x1)
            if dx >= CAP: continue
            for j in range(max(0, int((z0 - CAP - Z0) / CELL) - 1), min(NZ, int((z1 + CAP - Z0) / CELL) + 2)):
                cz = Z0 + (j + .5) * CELL; dz = max(z0 - cz, 0, cz - z1)
                if dx * dx + dz * dz < CAP * CAP: cols.setdefault((i, j), []).append((y0, y1, x0 < cx < x1 and z0 < cz < z1))
    surf = {}
    for c, lst in cols.items():
        for y0, y1, inside in lst:
            if not inside: continue
            t = y1
            if any(inn and b0 < t + .45 and b1 > t + .01 for b0, b1, inn in lst): continue
            if any(b0 < t + 1.8 and b1 > t + .45 for b0, b1, inn in lst): continue
            surf.setdefault(c, set()).add(round(t, 3))
    start = None; a = ffa[0]
    c = (int((a[0] - X0) / CELL), int((a[1] - Z0) / CELL))
    for t in surf.get(c, ()):
        if abs(t - a[2]) < .05: start = (c, t)
    seen = {start} if start else set(); todo = list(seen)
    while todo:
        (i, j), t = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            for u in surf.get(n, ()):
                if abs(u - t) <= .25 + 1e-6 and (n, u) not in seen: seen.add((n, u)); todo.append((n, u))
    return surf, seen
def level(t): return 'floor' if abs(t - FL) < .05 else 'bridge' if abs(t - BR) < .05 else 'balcony' if abs(t - BAL) < .05 else 'other'
def at(surf, seen, p):
    c = (int((p[0] - X0) / CELL), int((p[1] - Z0) / CELL)); return any((c, t) in seen for t in surf.get(c, ()) if abs(t - p[2]) < .05)

def flights(surf, seen):
    groups, out = {}, []
    for e in els:
        if e['ch'] == '/' and e['id'][-3] == '-' and e['id'][-2:].isdigit(): groups.setdefault(e['id'][:-3], []).append(e)
    for name, steps in sorted(groups.items()):
        own = {}
        for k, e in enumerate(steps):
            x0, x1, y0, y1, z0, z1 = e['box']
            for i in range(int((x0 - X0) / CELL), int((x1 - X0) / CELL) + 1):
                for j in range(int((z0 - Z0) / CELL), int((z1 - Z0) / CELL) + 1):
                    if x0 < X0 + (i + .5) * CELL < x1 and z0 < Z0 + (j + .5) * CELL < z1: own[(i, j)] = (k, round(y1, 3))
        nodes = {(c, t) for c, (k, t) in own.items() if t in surf.get(c, ())}
        bottom = {n for n in nodes if own[n[0]][0] == 0}; top = {n for n in nodes if own[n[0]][0] == len(steps) - 1}
        got = set(bottom); todo = list(bottom)
        while todo:
            (i, j), t = todo.pop()
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                c = (i + di, j + dj)
                if c in own and (c, own[c][1]) in nodes and abs(own[c][1] - t) <= .25 + 1e-6 and (c, own[c][1]) not in got: got.add((c, own[c][1])); todo.append((c, own[c][1]))
        out.append((name, len(steps), bool(top & got) and bool(bottom & seen) and bool(top & seen)))
    return out

def paint(surf, seen):
    W = X1 - X0; R = (Z1 - Z0) // 2 + 1; outs = []
    for lv, y, ylo, yhi in (('floor (top 0)', FL, -1, 2.2), ('bun bridges (top 2.4)', BR, 2.2, 3.3), ('balconies (top 3.6) and above', BAL, 3.3, 99)):
        g = [[' '] * W for _ in range(R)]
        def put(x, z, ch):
            c = int(math.floor(x - X0)); r = int((Z1 - z) // 2)
            if 0 <= c < W and 0 <= r < R: g[r][c] = ch
        def rect(x0, x1, z0, z1, ch):
            zz = z0
            while zz < z1 - 1e-9:
                xx = x0
                while xx < x1 - 1e-9: put(xx + .05, zz + .05, ch); xx += .5
                zz += .5
        for (i, j), t in seen:
            if abs(t - y) < .05: put(X0 + (i + .5) * CELL, Z0 + (j + .5) * CELL, '.')
        for x0, x1, z0, z1, name, h0, h1 in hazard_rects():
            if lv.startswith('floor'): rect(x0, x1, z0, z1, '~' if 'lane' in name else '*' if 'griddle' in name else 'v' if 'geyser' in name else '!')
        for e in sorted(els, key=lambda e: e['ch'] == 'r'):
            b = e['box']
            if e['ch'] in '.' or not (ylo <= b[2] < yhi): continue
            rect(b[0], b[1], b[4], b[5], e['ch'])
        for nm, p in zip(ffa_names, ffa):
            if abs(p[2] - y) < .05: put(p[0], p[1], nm)
        for k, p in enumerate(teamA + teamB):
            if abs(p[2] - y) < .05 and p not in ffa: put(p[0], p[1], str(k + 1))
        outs.append(lv + ':\n' + ' ' * 10 + 'x=-28' + ' ' * 23 + 'x=0' + ' ' * 20 + 'x=+27')
        for r in range(R): z = Z1 - 2 * r; outs.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(outs)

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), 'size': v(x1 - x0, y1 - y0, z1 - z0),
                'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], p[2] + 1.1, p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'emporium', 'name': 'Top Dog Emporium', 'tag': 'Hot dogs since 1956. Mind the wieners.', 'theme': 'emporium',
                      'music': 'cybercity-neon-riot', 'ambience': 'emporium-sizzle', 'look': 'emporium-golden-hour', 'sky': 0xFF9A5A, 'floor': 0xF2E6D0, 'accent': 0xFFC21E,
                      'rotation': True, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, surf, seen, scale=8):
    PW_, GAP = (X1 - X0) * scale, 16; PH = (Z1 - Z0) * scale; W, H = 3 * PW_ + 2 * GAP, PH; img = bytearray(b'\x0c\x18\x28' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        ox = k * (PW_ + GAP); i0, i1 = int((x0 - X0) * scale), int(math.ceil((x1 - X0) * scale)); j0, j1 = int((Z1 - z1) * scale), int(math.ceil((Z1 - z0) * scale))
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(PW_, i1)):
                o = (j * W + ox + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    for k in range(3): fill(k, X0, X1, Z0, Z1, (14, 30, 48))
    floor = {FL: (0, (200, 190, 170)), BR: (1, (210, 160, 90)), BAL: (2, (160, 110, 90))}
    for (i, j), t in surf.items():
        for u in t:
            for y, (k, c) in floor.items():
                if abs(u - y) < .05: fill(k, X0 + i * CELL, X0 + (i + 1) * CELL, Z0 + j * CELL, Z0 + (j + 1) * CELL, c if ((i, j), u) in seen else (120, 50, 50))
    col = {'#': (150, 140, 120), 'O': (120, 120, 130), 'F': (200, 80, 60), 'S': (140, 120, 90), 'C': (240, 200, 40), 'P': (180, 180, 190), 'p': (190, 190, 200),
           'T': (220, 60, 50), 'x': (200, 150, 90), '=': (230, 170, 80), '/': (185, 185, 190), '_': (150, 100, 80), 'I': (90, 90, 100), 'r': (250, 210, 60),
           'b': (200, 40, 50), 't': (240, 230, 210), 'c': (230, 230, 235), 'D': (90, 200, 230), 'J': (250, 120, 200), 'K': (200, 120, 110), 'U': (250, 220, 40), 'N': (255, 80, 200), 'Z': (170, 220, 240), 'o': (90, 160, 60), 'i': (190, 230, 250)}
    for e in sorted(els, key=lambda e: (e['ch'] == 'r', e['box'][3])):
        b = e['box']
        if e['ch'] == '.': continue
        k = 0 if b[2] < 2.2 else 1 if b[2] < 3.3 else 2
        fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)))
    for p in PILOT:
        c, z = p['center'], p['size']; fill(2, c['x'] - z['x'] / 2 - .05, c['x'] + z['x'] / 2 + .05, c['z'] - z['z'] / 2 - .05, c['z'] + z['z'] / 2 + .05, (230, 60, 200), .45)
    for x0, x1, z0, z1, name, h0, h1 in hazard_rects():
        fill(0, x0, x1, z0, z1, (255, 120, 20) if 'griddle' in name else (255, 60, 60) if 'press' in name else (255, 220, 40) if 'geyser' in name else (255, 160, 200), .45)
    for idx, p in enumerate(teamA + teamB):
        k = 0 if p[2] < 2.2 else 1 if p[2] < 3.3 else 2; fill(k, p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if idx < 4 else (255, 90, 120))
    for p in ffa[2:]:
        k = 0 if p[2] < 2.2 else 1 if p[2] < 3.3 else 2; fill(k, p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (245, 245, 245))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    surf, seen = raster()
    print(paint(surf, seen))
    print('legend: . walkable  # wall  O oven  F fryer  S shelf  i icebox  C cart  P press post/head  p prep table  T condiment tower  x crate'
          '\n        = bun bridge  / stairs  _ balcony  I post  r rail  b booth  c counter  D soda fountain  J jukebox  U pump  N menu board'
          '\n        Z freezer  o pickle barrels  ~ wiener lane  * griddle  ! press ram  v geyser; A1 B1 f F g G h H FFA, 1-8 team spawns')
    bad = 0
    named = list(zip(ffa_names, ffa)); bad += check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    d3 = lambda p, q: math.dist((p[0], p[2], p[1]), (q[0], q[2], q[1]))
    print('min FFA spacing %.1f m (plan; %.1f m 3D); min A-B spacing %.1f m' % (min(math.dist(p[:2], q[:2]) for p, q in itertools.combinations(ffa, 2)),
          min(d3(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)))
    sp = ffa + teamA[1:] + teamB[1:]
    worst = min((dist_rect(p, r[:4]), r[4], p[:2]) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume (plan footprint): %.1f m (%s, spawn %s)' % worst); bad += worst[0] < 3
    clr = min((min(dist_rect(p[:2], (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < p[2] + 1.9 and e['box'][3] > p[2] + .3), p[:2]) for p in sp)
    print('closest spawn to any block at its floor: %.2f m at %s' % clr); bad += clr[0] < CAP
    inb = all(abs(p[0]) <= 29 - CAP and abs(p[1]) <= 23 - CAP for p in sp); print('all spawns inside the bounds minus the capsule:', inb); bad += not inb
    lv = {}
    for c, ts in surf.items():
        for t in ts: k = level(t); lv.setdefault(k, [0, 0]); lv[k][1] += 1; lv[k][0] += (c, t) in seen
    print('raster (0.25 m cells) reachable on foot from A1: ' + '; '.join('%s %d of %d' % (k, lv[k][0], lv[k][1]) for k in ('floor', 'bridge', 'balcony', 'other') if k in lv))
    reach = {n: at(surf, seen, p) for n, p in list(zip(ffa_names, ffa)) + A + B}
    print('every spawn reachable:', all(reach.values()), '' if all(reach.values()) else [n for n, r in reach.items() if not r]); bad += not all(reach.values())
    levels = all(lv.get(k, [0])[0] > 0 for k in ('floor', 'bridge', 'balcony')); print('every level reachable:', levels); bad += not levels
    # every hazard lane, zone and ram footprint stays walkable floor (no block inside it below 2.2 m)
    blocked = [r[4] for r in hazard_rects() for e in els if e['box'][2] < 2.0 and e['box'][0] < r[1] - .01 and e['box'][1] > r[0] + .01 and e['box'][4] < r[3] - .01 and e['box'][5] > r[2] + .01]
    print('hazard footprints clear of blocks below 2 m:', not blocked, blocked[:4]); bad += bool(blocked)
    fl = flights(surf, seen); okf = all(r[2] for r in fl)
    print('stairs: %d flights, each walkable bottom to top on its own and joined to both levels: %s (%s)' % (len(fl), okf, ', '.join('%s %s' % (n.replace('emporium-', ''), 'ok' if r else 'BROKEN') for n, k, r in fl))); bad += not okf
    key = lambda b: tuple(round(c, 3) for c in b)
    sym = sorted(key(e['box']) for e in [FLOOR] + els) == sorted(key(mbox(e['box'])) for e in [FLOOR] + els)
    psym = sorted((round(p['center']['x'], 3), round(p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT) == \
           sorted((round(-p['center']['x'], 3), round(-p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT)
    ssym = sorted(teamB) == sorted((-x, -z, f) for x, z, f in teamA) and sorted(ffa) == sorted((-x, -z, f) for x, z, f in ffa)
    print('point symmetric (x,z)->(-x,-z): blocks %s, pilot blocks %s, spawns %s' % (sym, psym, ssym)); bad += not (sym and psym and ssym)
    band = [e['id'] for e in els if e['ch'] not in '/' and 1.3 < e['box'][3] - max(FL, 0) < 2.2 and e['box'][2] < .05]
    print('no tops in the 1.3-2.2 m band (stairs excepted):', not band, band[:4]); bad += bool(band)
    # hazard fairness: idle >= 4 s, telegraphs at least today's (maglev 6 s, press 0.8 s, burner 1.5 s, vent 0.8 s), fits the period
    for h in HAZ:
        if h['kind'] == 'maglev':
            ok = h['first'] - h['warn'] >= 4 and h['warn'] >= 6 and h['danger'] >= 3 and (h['size']['x'] + h['length']) / h['speed'] + 2 + h['warn'] < h['period']
            print('  %s: first pass %.0f s (warning from %.0f s), every %.0f s, warn %.0f s (danger %.0f s), pass %.2f s at %.0f m/s: %s'
                  % (h['id'], h['first'], h['first'] - h['warn'], h['period'], h['warn'], h['danger'], (h['size']['x'] + h['length']) / h['speed'], h['speed'], ok))
        elif h['kind'] == 'press':
            busy = h['warn'] + h['rise'] + h['dwell'] + h['travel']; opn = h['period'] - busy
            ok = h['warn'] >= .8 and busy <= h['period'] and 4 + h['start'] + h['offset'] + opn >= 4
            print('  %s: warn %.1f s, slam %.2f s, held %.1f s, lift %.1f s every %.0f s (first warning %.1f s into the fight): %s' % (h['id'], h['warn'], h['rise'], h['dwell'], h['travel'], h['period'], 4 + h['start'] + h['offset'] + opn, ok))
        elif h['kind'] == 'burner':
            ok = h['first'] >= 4 and h['warn'] >= 1.5 and h['warn'] + h['live'] + h['dwell'] <= h['period'] and not h['lethal']
            ok = ok and all(abs(z['center'][k] - h['center'][k]) + z['size'][k] / 2 <= h['size'][k] / 2 + 1e-3 for z in h['zones'] for k in 'xyz')
            print('  %s: first warning %.0f s, warn %.1f s, live %.1f s (%d x %d = %d HP), cool %.1f s, every %.0f s: %s' % (h['id'], h['first'], h['warn'], h['live'], round(h['live'] / h['tick']), h['damage'], round(h['live'] / h['tick']) * h['damage'], h['dwell'], h['period'], ok))
        else:
            ok = h['warn'] >= .8 and h['warn'] + h['dwell'] <= h['period'] and h['push'] > 0 and not h['lethal'] and h['tick'] > 0
            print('  %s: pumps %.1f s, bursts %.1f s every %.0f s (offset %.0f), launch %.0f m/s (apex %.1f m), %d per %.1f s tick: %s'
                  % (h['id'], h['warn'], h['dwell'], h['period'], h['offset'], h['push'], h['push'] ** 2 / 46, h['damage'], h['tick'], ok))
        bad += not ok
    ids = [e['id'] for e in [FLOOR] + els] + [h['id'] for h in HAZ]; dup = len(ids) - len(set(ids)); print('duplicate ids: %d; every id emporium-*: %s' % (dup, all(i.startswith('emporium-') for i in ids))); bad += dup > 0
    print('blocks: %d (+ floor); hazards: %d; pilot blocks: %d; spawns: %d FFA, %d + %d team; breakable: %d' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA), len(teamB), sum(1 for e in els if e['hp'] > 0)))
    bad += len(els) + 1 > 350
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'emporium_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'emporium_m1.png'), surf, seen); print('wrote', os.path.join(out, 'emporium_m1.json'), 'and emporium_m1.png')
    print('checks:', 'PASS' if not bad else 'FAIL (%d)' % bad)
    sys.exit(1 if bad else 0)
