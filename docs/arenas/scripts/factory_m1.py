# Factory M1 (Corvane Works, 80x64, three lanes, five hazard families) draft painter, spawn LOS and hazard clearance checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z) unless noted.
# usage: python3 docs/arenas/scripts/factory_m1.py [--out DIR] [--json PATH]   (stdout: plan + checks; --json writes the draft only; --out writes
#        factory_m1.json, draft map data in the UnityMaps.json shape, and factory_m1.png, a top-down plan, 8 px per metre)
# A draft for Astra (docs/arenas/FACTORY-SIMULATION-CONTRACT.md), NOT wired into the game. Ids carry the prefixes the
# presentation keys its looks and dressing kits on (FACTORY_PROPOSAL.md 5.3); geometry and timing are sim-owned after H0.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -40, 40, -32, 32           # slab; play bounds x +-38, z +-30 inside the 2 m shell
BOUNDS = {'minX': -38, 'maxX': 38, 'minZ': -30, 'maxZ': 30, 'ceiling': 24, 'killY': -6}
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp))
def wall_x(i, x0, x1, y0, y1, z0, z1, openings, ch, **k):
    cuts = [x0] + [v for o in sorted(openings) for v in o] + [x1]
    for j in range(0, len(cuts), 2):
        if cuts[j + 1] > cuts[j]: add('%s-%d' % (i, j // 2), cuts[j], cuts[j + 1], y0, y1, z0, z1, ch, **k)
def stair(i, xa, xb, z0, z1, ya, yb, steps, mirror=True, axis='x'):
    # a flight of solid steps from (xa, ya) to (xb, yb); along x (z0..z1 wide) or along z (then xa/xb are z, z0/z1 are x)
    run = (xb - xa) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1)))
        y0 = ya if ya == 0 and yb > 0 else ya + rise * k - .2      # ground flights are solid; elevated flights are 20 cm slabs
        y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), a, b, max(0, y0), y1, z0, z1, '/', mat='metal', mirror=mirror)
        else: add('%s-%02d' % (i, k), z0, z1, max(0, y0), y1, a, b, '/', mat='metal', mirror=mirror)

# ---- shell: concrete plinth to 4.4 m, corrugated cladding to 14 m (indestructible; tops unstandable)
add('factory-wall-n-base', -40, 40, 0, 4.4, 30, 32, '#')
add('factory-wall-n-clad', -40, 40, 4.4, 14, 30.3, 32, '#', mat='metal')
add('factory-wall-w-base', -40, -38, 0, 4.4, -30, 30, '#')
add('factory-wall-w-clad', -40, -38.3, 4.4, 14, -30, 30, '#', mat='metal')
FLOOR = dict(id='factory-floor', mat='foundation', role='static', box=(-40, 40, -.5, 0, -32, 32), ch='.', hp=0)
# ---- bay walls between the bay/docks and the halls (z 9..10.4, 3.4 m), with the gallery deck on top (3.6) cantilevered over the bay
NWALL = [(-35, -31.5), (-12, -9), (3, 6.5), (31.5, 35)]       # dock A door, press tunnel, bay door, dock B door
wall_x('factory-baywall-n', -38, 38, 0, 3.4, 9.0, 10.4, NWALL, '=')
for j, (a, b) in enumerate(NWALL):
    if (a, b) == (-12, -9): add('factory-press-n-head', a, b, 3.0, 3.4, 9.0, 10.4, 'p')          # ram housing over the tunnel
    else: add('factory-baywall-n-lintel-%d' % j, a, b, 2.8, 3.4, 9.0, 10.4, '=')
add('factory-press-n-bolster', -12, -9, 0, .08, 8.6, 10.8, 'p', mat='metal')                  # die bed, flush (8 cm)
add('factory-gallery-n', -36.6, 36.6, 3.4, 3.6, 8.0, 10.4, 'g', mat='metal')
# ---- columns on the hall side of the bay wall, carrying the crane runway (12.2..13.2) and the hall roof
for k, x in enumerate((-36, -27, -18, -7.5, 1.5, 9, 18, 27, 36)):
    add('factory-column-n-%d' % k, x - .3, x + .3, 0, 12.2, 10.4, 11.0, 'C', mat='metal')
add('factory-runway-n', -30, 30, 12.2, 13.2, 10.2, 11.2, 'r', mat='metal')
# ---- hall roofs at 11.5..11.8 with four 1.5 m skylight slots each (sun shafts; the sawtooth glazing is kit)
for k, (a, b) in enumerate(((10.4, 12.6), (14.1, 17.1), (18.6, 21.6), (23.1, 26.1), (27.6, 30))):
    add('factory-roof-n-%d' % k, -38, 38, 11.5, 11.8, a, b, 'R', mat='metal')
SKYLIGHTS = [(12.6, 14.1), (17.1, 18.6), (21.6, 23.1), (26.1, 27.6)]
# ---- north hall: catwalk along the outer wall (3.6) with stairs at both ends
add('factory-catwalk-n', -30, 30, 3.4, 3.6, 27.6, 30, 'c', mat='metal')
stair('factory-stair-catwalk-n-w', -37.4, -30, 28.4, 30, 0, 3.4, 17)
stair('factory-stair-catwalk-n-e', 37.4, 30, 28.4, 30, 0, 3.4, 17)
# gallery stair from the hall floor (east half) up to the north gallery
stair('factory-stair-gallery-n', 26.4, 19.0, 11.0, 12.4, 0, 3.4, 17)
add('factory-stair-gallery-n-landing', 17.8, 19.0, 3.4, 3.6, 10.4, 12.4, '/', mat='metal')
# machine row (bay side of the slitter lane): lathes (bed + headstock), mills, the switch cabinets
for k, (x0, x1) in enumerate(((-25, -20), (-7, -2), (15, 20))):
    add('factory-lathe-n-%d' % k, x0, x1, 0, 1.1, 12.6, 14.0, 'm', mat='metal')
    add('factory-lathe-n-%d-head' % k, x0, x0 + 1.4, 1.1, 1.9, 12.6, 14.0, 'm', mat='metal')
for k, x in enumerate((-15.2, 9.2, 25.2)):
    add('factory-mill-n-%d' % k, x - 1.2, x + 1.2, 0, 2.4, 13.4, 15.8, 'M', mat='metal')
add('factory-cabinet-n-0', 0.5, 3.5, 0, 2.2, 12.4, 13.4, 'M', mat='metal')
# storage row (between the lane and the catwalk aisle): cantilever bar racks, coil stacks, crates (breakable)
for k, (x0, x1) in enumerate(((-26, -20), (-6, 0), (13, 19))):
    add('factory-rack-n-%d' % k, x0, x1, 0, 2.4, 24.0, 25.4, 'R', mat='metal')
add('factory-coils-n-0', 5, 7.4, 0, 1.6, 23.4, 25.8, 'o', mat='metal')
add('factory-crate-n-0', -15, -13, 0, 1.2, 23.6, 25.6, 'x', mat='wood', role='cover', hp=34)
add('factory-crate-n-1', -14.5, -13.5, 1.2, 2.2, 24.1, 25.1, 'x', mat='wood', role='cover', hp=34)
add('factory-crate-n-2', 24, 26, 0, 1.2, 23.6, 25.6, 'x', mat='wood', role='cover', hp=34)
# hall-end rooms (x 27..38): a 3 m blockwork screen at x 27 leaves the aisles open; a shelf screens each dock door's vestibule
add('factory-screen-n-w', -27.6, -27.0, 0, 3.0, 12.4, 22.0, 'O')
add('factory-screen-n-e', 27.0, 27.6, 0, 3.0, 12.4, 22.0, 'O')
add('factory-shelf-n-w', -36.4, -30.2, 0, 2.6, 12.6, 13.4, 'O', mat='metal')
add('factory-shelf-n-e', 30.2, 36.4, 0, 2.6, 12.6, 13.4, 'O', mat='metal')
add('factory-crib-n-w', -38, -34.6, 0, 2.6, 15.0, 20.0, 'O', mat='metal')
add('factory-bins-n-w', -33.0, -31.0, 0, 1.2, 23.6, 25.6, 'o', mat='metal')
add('factory-crib-n-e', 34.6, 38, 0, 2.6, 20.0, 25.0, 'O', mat='metal')
add('factory-bins-n-e', 30.4, 32.4, 0, 1.2, 21.4, 23.4, 'o', mat='metal')
# ---- docks (spawn yards, x 27..38): site office, container under the pulpit, forklift, pallets, stairs to both galleries
add('factory-dock-a-office', -38, -34, 0, 3.0, -9, -4.6, 'O')
add('factory-dock-a-container', -30.6, -28.2, 0, 2.6, -3.0, 3.0, 'K', mat='metal')
add('factory-dock-a-forklift', -35.6, -34.2, 0, 2.2, 1.6, 3.8, 'O', mat='metal')
add('factory-dock-a-pallets', -33.6, -32.2, 0, 1.1, 5.2, 6.2, 'x', mat='wood', role='cover', hp=34)
stair('factory-stair-dock-a-n', -27.6, -35.0, 6.4, 8.0, 0, 3.4, 17)
add('factory-stair-dock-a-n-landing', -36.6, -35.0, 3.4, 3.6, 6.4, 8.0, '/', mat='metal')
stair('factory-stair-dock-a-s', -27.6, -35.0, -8.0, -6.4, 0, 3.4, 17, mirror=True)
add('factory-stair-dock-a-s-landing', -36.6, -35.0, 3.4, 3.6, -8.0, -6.4, '/', mat='metal')
# ---- control pulpits (6.0) over the dock mouths, reached from the north gallery (A) / south gallery (B)
add('factory-pulpit-a-deck', -27, -23, 5.8, 6.0, -2.5, 2.5, 'P', mat='metal')
for k, (x, z) in enumerate(((-26.8, -2.3), (-26.8, 2.3), (-23.2, -2.3), (-23.2, 2.3))):
    add('factory-pulpit-a-leg-%d' % k, x - .2, x + .2, 0, 5.8, z - .2, z + .2, 'C', mat='metal')
add('factory-pulpit-a-parapet-e', -23.2, -23.0, 6.0, 7.0, -2.5, 2.5, 'P', mat='metal')
add('factory-pulpit-a-parapet-s', -27, -23, 6.0, 7.0, -2.5, -2.3, 'P', mat='metal')
add('factory-pulpit-a-roof', -27.2, -22.8, 8.6, 8.8, -2.7, 2.7, 'P', mat='metal')
stair('factory-stair-pulpit-a', 8.0, 2.5, -26.6, -25.0, 3.6, 6.0, 11, axis='z')
# ---- the crane bay (x -27..27, z -8..8 plus the strips under the galleries): ladle on its car at the centre, low and tall cover
add('factory-ladle', -1.7, 1.7, .9, 4.0, -1.7, 1.7, 'L', mat='metal', mirror=False)
add('factory-ladle-car', -2.4, 2.4, 0, .9, -1.6, 1.6, 'L', mat='metal', mirror=False)
add('factory-bay-billets-a', -21.5, -18.5, 0, 2.4, -6.6, -5.2, 'O', mat='metal')
add('factory-bay-ingots-a', -9.6, -7.6, 0, 1.2, .6, 2.6, 'o', mat='metal')
add('factory-bay-plates-a', -21.0, -19.0, 0, 1.0, 2.6, 4.8, 'o', mat='metal')
add('factory-bay-moulds-a', -13.4, -11.6, 0, 1.4, -6.0, -4.2, 'o', mat='metal')
add('factory-bay-drums-a', -24.6, -23.4, 0, 1.2, 5.4, 6.6, 'x', mat='metal', role='cover', hp=150)
add('factory-bay-beams-a', -7.0, -3.0, 0, .9, 6.0, 7.2, 'o', mat='metal')
add('factory-bay-rack-a', -16.6, -14.6, 0, 2.6, -2.8, -1.2, 'O', mat='metal')
add('factory-bay-hammer-a', -10.0, -7.0, 0, 3.4, 3.0, 7.0, 'O', mat='metal')               # steam hammer frame: tall cover

# ---- hazards (UnityMaps.json `hazards[]`, FACTORY-SIMULATION-CONTRACT.md 2): timings in seconds after the arm time (FightAt + 4 s)
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), size=v(0, 0, 0), radius=0, start=0, period=0, offset=0,
             warn=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, lethal=False, alternate=False, zones=[])
    d.update(k); return d
def mv(p): return v(-p['x'], p['y'], -p['z'])
HAZ = []
slot_n = hz(id='factory-saw-slot-n', kind='saw-slot', a=v(-18, 0, 19.5), b=v(18, 0, 19.5), axis=v(0, 0, 1), size=v(1.6, 1.6, .1), radius=.8,
            start=2, period=14, offset=0, warn=1.6, rise=.35, travel=6.0, lethal=True, alternate=True)
rail_n = hz(id='factory-saw-rail-n', kind='saw-rail', a=v(8, 4.65, 30.6), b=v(8, 4.65, 27.0), axis=v(1, 0, 0), size=v(.1, 1.7, 1.7), radius=.85,
            start=1, period=5, offset=0, warn=1.0, travel=.45, dwell=.35, lethal=True)
press_n = hz(id='factory-press-n', kind='press', a=v(-10.5, 2.75, 9.7), b=v(-10.5, .33, 9.7), size=v(2.9, .5, 1.3),
             start=.5, period=6, offset=0, warn=.8, rise=.22, dwell=.6, travel=1.0, lethal=True)
vent_bay = hz(id='factory-vent-bay-a', kind='vent', a=v(-22.5, 0, 7.0), size=v(1.4, 3.2, 1.4), start=3, period=9, offset=0, warn=.8, dwell=2.2, damage=10, tick=.5)
vent_hall = hz(id='factory-vent-hall-n', kind='vent', a=v(12, 0, 27.4), size=v(1.4, 3.2, 1.4), start=3, period=9, offset=4.5, warn=.8, dwell=2.2, damage=10, tick=.5)
for h, off in ((slot_n, 7), (rail_n, 2.5), (press_n, 3), (vent_bay, 0), (vent_hall, 4.5)):
    # the point-symmetric partner runs half a cycle later (vents a quarter, so the four never burst together)
    HAZ.append(h); m = dict(h); m['id'] = mirror_id(h['id']); m['a'] = mv(h['a']); m['b'] = mv(h['b']); m['offset'] = (h['offset'] + 2.25) % h['period'] if h['kind'] == 'vent' else off
    HAZ.append(m)
ZONES = [(-14, 3), (14, -3), (-5, -4.5), (5, 4.5)]                      # visit order: A side, B side, A side, B side
crane = hz(id='factory-crane', kind='crane', a=v(-20, 13.2, 0), b=v(20, 13.2, 0), size=v(2.2, .9, 4.0), carry=9.5,
           start=6, period=20, warn=3.0, travel=6.0, rise=.85, dwell=5.15, hoist=2.0, lethal=True,
           zones=[{'center': v(x, 0, z), 'size': v(2.8, 2.2, 4.6)} for x, z in ZONES])
HAZ.insert(4, crane)
PAINT_ZONE = (3.2, 5.0)                                                  # painted zone footprint (load 2.2 x 4.0 plus 0.5 m)

# ---- spawns (yaw from +z toward +x, facing the origin as today)
teamA = [(-35.5, -1.0), (-32.0, 3.6), (-31.8, -5.0), (-36.6, 4.2)]
teamB = [(-x, -z) for x, z in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (-32.5, 18.0), (32.5, -18.0), (-3.0, 28.6), (3.0, -28.6), (33.5, 17.0), (-33.5, -17.0)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only blocking columns above unstandable tops (FACTORY_PROPOSAL.md 3.2)
PILOT = [dict(id='factory-top-wall-n', center=v(0, 0, 31), size=v(80, 0, 2), bottom=13.9), dict(id='factory-top-wall-s', center=v(0, 0, -31), size=v(80, 0, 2), bottom=13.9),
         dict(id='factory-top-wall-w', center=v(-39, 0, 0), size=v(2, 0, 64), bottom=13.9), dict(id='factory-top-wall-e', center=v(39, 0, 0), size=v(2, 0, 64), bottom=13.9),
         dict(id='factory-top-roof-n', center=v(0, 0, 20.2), size=v(76, 0, 19.6), bottom=11.7), dict(id='factory-top-roof-s', center=v(0, 0, -20.2), size=v(76, 0, 19.6), bottom=11.7),
         dict(id='factory-top-runway-n', center=v(0, 0, 10.7), size=v(60, 0, 1.0), bottom=13.1), dict(id='factory-top-runway-s', center=v(0, 0, -10.7), size=v(60, 0, 1.0), bottom=13.1),
         dict(id='factory-top-pulpit-a', center=v(-25, 0, 0), size=v(4.4, 0, 5.4), bottom=8.7), dict(id='factory-top-pulpit-b', center=v(25, 0, 0), size=v(4.4, 0, 5.4), bottom=8.7),
         dict(id='factory-top-ladle', center=v(0, 0, 0), size=v(3.4, 0, 3.4), bottom=3.9)]

def hazard_rects():
    """2D footprints of every hazard's swept or kill volume (x0, x1, z0, z1, name): what spawns keep 3 m from."""
    out = []
    for h in HAZ:
        a, b, k = h['a'], h['b'], h['kind']
        if k in ('saw-slot', 'saw-rail'):
            ex = h['radius'] if abs(h['axis']['z']) > .5 else h['size']['x'] / 2; ez = h['radius'] if abs(h['axis']['x']) > .5 else h['size']['z'] / 2
            out.append((min(a['x'], b['x']) - ex, max(a['x'], b['x']) + ex, min(a['z'], b['z']) - ez, max(a['z'], b['z']) + ez, h['id']))
        elif k == 'press': out.append((a['x'] - 1.5, a['x'] + 1.5, a['z'] - 1.1, a['z'] + 1.1, h['id']))
        elif k == 'vent': out.append((a['x'] - .7, a['x'] + .7, a['z'] - .7, a['z'] + .7, h['id']))
        elif k == 'crane':
            for j, zn in enumerate(h['zones']): c = zn['center']; out.append((c['x'] - PAINT_ZONE[0] / 2, c['x'] + PAINT_ZONE[0] / 2, c['z'] - PAINT_ZONE[1] / 2, c['z'] + PAINT_ZONE[1] / 2, 'crane-zone-%d' % j))
    return out

def paint():
    W = X1 - X0; R = (Z1 - Z0) // 2; g = [['.'] * W for _ in range(R)]
    def put(x, z, ch):
        c = int(math.floor(x - X0)); r = int((Z1 - z) // 2)
        if 0 <= c < W and 0 <= r < R: g[r][c] = ch
    def rect(x0, x1, z0, z1, ch):
        zz = z0
        while zz < z1 - 1e-9:
            xx = x0
            while xx < x1 - 1e-9: put(xx + .25, zz + .25, ch); xx += .5
            zz += .5
    over = [e for e in els if e['box'][2] >= 2.5]; ground = [e for e in els if e['box'][2] < 2.5]
    for e in over:
        if e['ch'] in 'gcP': b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
    for x0, x1, z0, z1, name in hazard_rects():
        ch = 'Z' if name.startswith('crane') else 'S' if 'slot' in name else '~' if 'rail' in name else 'v' if 'vent' in name else 'p'
        rect(x0, x1, z0, z1, ch)
    for e in ground:
        if e['ch'] in '#C': continue
        b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
    for e in els:
        if e['ch'] in '#C': b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
    for i, (x, z) in enumerate(teamA): put(x, z, str(i + 1)); put(-x, -z, str(i + 5))
    for nm, (x, z) in zip(ffa_names[2:], ffa[2:]): put(x, z, nm)
    out = ['         x=-40' + ' ' * 36 + 'x=0' + ' ' * 37 + 'x=+39']
    for r in range(R): z = Z1 - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

def seg_hits(a, b, e, eye):
    x0, x1, y0, y1, z0, z1 = e['box']
    if not (y0 < eye < y1) or y1 < 2.0: return False
    t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], z0, z1)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
def visible(a, b, eye): return not any(seg_hits(a, b, e, eye) for e in els)
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9) if visible((a[0] + dx, a[1] + dz), (b[0] + ex, b[1] + ez), eye))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a, b):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

def ground_raster():
    """1 m capsule-width raster of the ground floor: a cell is blocked by any block reaching below 1.0 m and above a 0.45 m
    step (the bots' obstacle ray starts about .45 m up). Returns the flood-filled reachable set from spawn A1."""
    W, H = X1 - X0, Z1 - Z0; blocked = set()
    for e in els:
        x0, x1, y0, y1, z0, z1 = e['box']
        if y0 >= 1.0 or y1 <= .45 or e['ch'] == '/': continue
        for i in range(int(math.floor(x0 - X0 - .42)), int(math.ceil(x1 - X0 + .42))):
            for j in range(int(math.floor(z0 - Z0 - .42)), int(math.ceil(z1 - Z0 + .42))):
                cx, cz = X0 + i + .5, Z0 + j + .5
                if x0 - .42 < cx < x1 + .42 and z0 - .42 < cz < z1 + .42: blocked.add((i, j))
    start = (int(teamA[0][0] - X0), int(teamA[0][1] - Z0)); seen = {start}; todo = [start]
    while todo:
        i, j = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            if 0 <= n[0] < W and 0 <= n[1] < H and n not in blocked and n not in seen: seen.add(n); todo.append(n)
    return seen, blocked

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        d = {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)),
             'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
        return d
    sp = lambda p: {'pos': v(p[0], 1.1, p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'factory', 'name': 'Factory', 'tag': 'Corvane Works, first shift. Mind the saws, watch the hook.', 'theme': 'factory',
                      'music': 'factory-line-three', 'ambience': 'factory', 'look': 'factory-first-shift', 'sky': 0x9AA6B0, 'floor': 0x6E6A63, 'accent': 0xF2B01E,
                      'rotation': False, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    W, H = (X1 - X0) * scale, (Z1 - Z0) * scale; img = bytearray(b'\x18\x1a\x1e' * W * H)
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        i0, i1 = int((x0 - X0) * scale), int((x1 - X0) * scale); j0, j1 = int((Z1 - z1) * scale), int((Z1 - z0) * scale)
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    fill(X0, X1, Z0, Z1, (58, 60, 62))
    col = {'#': (110, 112, 116), '=': (140, 134, 122), 'C': (60, 80, 100), 'g': (120, 128, 136), 'c': (120, 128, 136), 'P': (150, 110, 70), 'm': (70, 105, 90),
           'M': (60, 95, 80), 'R': (110, 92, 70), 'o': (130, 120, 100), 'O': (95, 95, 105), 'x': (180, 130, 75), 'K': (160, 70, 50), 'L': (200, 90, 30), '/': (170, 170, 175), 'p': (220, 170, 40), 'r': (80, 80, 90)}
    for e in sorted(els, key=lambda e: e['box'][3]):
        b = e['box']
        if e['id'].startswith('factory-roof') or e['id'].startswith('factory-runway'): continue
        fill(b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .55 if b[2] >= 2.5 else 1.0)
    for x0, x1, z0, z1, name in hazard_rects():
        c = (230, 60, 40) if 'saw' in name else (240, 190, 30) if 'crane' in name else (230, 120, 30) if 'press' in name else (180, 200, 220)
        fill(x0, x1, z0, z1, c, .7)
    for i, p in enumerate(teamA + teamB):
        fill(p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if i < 4 else (255, 90, 120))
    for p in ffa[2:]: fill(p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (240, 240, 240))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint())
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    sp = ffa + teamA[1:] + teamB[1:]
    print('min FFA spacing %.1f m; min A-B spacing %.1f m' % (min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p, q) for p in teamA for q in teamB)))
    worst = min((dist_rect(p, r[:4]), r[4], p) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume: %.1f m (%s, spawn %s)' % worst)
    clr = min((min(dist_rect(p, (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < 1.9), p) for p in sp)
    print('closest spawn to any block: %.1f m at %s' % clr)
    print('all spawns inside the bounds minus the capsule:', all(abs(x) <= 38 - .42 and abs(z) <= 30 - .42 for x, z in sp))
    # zones and the slot lanes are free of props; the painted zones keep 0.5 m from any block
    zc = []
    for (x, z) in ZONES:
        r = (x - PAINT_ZONE[0] / 2, x + PAINT_ZONE[0] / 2, z - PAINT_ZONE[1] / 2, z + PAINT_ZONE[1] / 2)
        for e in els:
            b = e['box']
            if b[2] >= 2.5: continue
            dx = max(r[0] - b[1], 0, b[0] - r[1]); dz = max(r[2] - b[5], 0, b[4] - r[3]); zc.append((math.hypot(dx, dz), e['id'], (x, z)))
    print('closest block to a painted crane zone: %.2f m (%s, zone %s)' % min(zc))
    lane = [e['id'] for e in els if e['box'][2] < 2 and e['box'][4] < 20.5 and e['box'][5] > 18.5 and e['box'][1] > -19 and e['box'][0] < 19]
    print('props in the slitter lanes (z 18.5..20.5, x -19..19):', lane or 'none')
    seen, blocked = ground_raster()
    reach = {n: (int(p[0] - X0), int(p[1] - Z0)) in seen for n, p in list(zip(ffa_names, ffa)) + [(f'B{i+1}', p) for i, p in enumerate(teamB)]}
    print('ground raster: %d of %d open cells reachable from A1; every spawn reachable: %s' % (len(seen), (X1 - X0) * (Z1 - Z0) - len(blocked), all(reach.values())))
    # hazard fairness numbers (FACTORY_PROPOSAL.md 3.8)
    s = slot_n; L = abs(s['b']['x'] - s['a']['x']); print('slitter: %.0f m in %.1f s = %.1f m/s (sprint 6.8); telegraph %.1f s + rise %.2f s; lethal %.1f s of %.0f s'
          % (L, s['travel'], L / s['travel'], s['warn'], s['rise'], s['travel'] + 2 * s['rise'], s['period']))
    c = crane; print('crane: %d zones, every %.0f s; %.1f s travel + %.1f s telegraph over the zone; fall %.2f s from %.1f m; landed cover %.2f s'
          % (len(c['zones']), c['period'], c['travel'], c['warn'], c['rise'], c['carry'], c['dwell']))
    p = press_n; print('press: open %.1f s of %.0f s, telegraph %.1f s, slam %.2f s; tunnel 1.4 m deep = %.2f s to cross at 6.8 m/s' % (p['period'] - p['warn'] - p['rise'] - p['dwell'] - p['travel'], p['period'], p['warn'], p['rise'], (1.4 + .84) / 6.8))
    print('blocks: %d (+ floor); hazards: %d; pilot blocks: %d' % (len(els), len(HAZ), len(PILOT)))
    if '--json' in sys.argv:                                              # the presentation checks' fixture (unity/ChipArenaConversion/Tests/factory-draft.json)
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'factory_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'factory_m1.png')); print('wrote', os.path.join(out, 'factory_m1.json'), 'and factory_m1.png')
    sys.exit(1 if bad else 0)
