# Skyframe M2 (the sky garden at the top of a tower still under construction, above a sea of clouds at sunset: the garden terrace at 0
# round the lift core, the gallery and the two pavilion roofs at 3.6 joined by glass bridges, the cloud terraces at -3.2 hung outside
# the north and south edges; the tower crane's beam drop, the crosswind on the cloud terraces, open edges) draft and checks.
# The 2026-11-11 rebuild of the stage (owner: "COMPLETELY rework ... Skyframe"); skyframe_m1.py stays as history.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z). Team A west, team B east.
# usage: python3 docs/arenas/scripts/skyframe_m2.py [--out DIR] [--json PATH]   (stdout: plans + checks, exit 1 on any failed check;
#        --json writes the draft only; --out writes skyframe_m2.json, draft map data in the UnityMaps.json shape, and skyframe_m2.png,
#        three top-down plans (levels -3.2, 0, 3.6), 8 px per metre)
# Design: docs/arenas/SKYFRAME.md. Ids carry the prefixes the presentation keys its looks, kit and art on (skyframe-garden, -core-*,
# -gallery-*, -column-*, -pavilion-*, -bridge-*, -terrace-*, -stair-*, -rail-*, -planter-*, -tree-*, -hedge-*, -greenwall-*,
# -bundle-*, -pallet-*, -crate-*, -counter-*).
import math, json, itertools, sys, os, zlib, struct
LL, L0, LU = -3.2, 0.0, 3.6                    # the cloud terraces, the garden, the gallery and the pavilion roofs
X0, X1, Z0, Z1 = -26, 26, -22, 22              # the structure's extent (the garden x +-26, the cloud terraces out to z +-22)
# The sim clamps bots, abilities and teleports to the bounds minus the capsule (NativeUnityMaps.Clamp), not walking pilots: the bounds
# run 4 m past every open edge, so a pilot who walks, jumps short or is blown off falls and dies at killY. killY -6: a body's middle
# below it dies about half a second after it leaves the garden; one standing on the lowest floor (the cloud terraces, -3.2) is 3.7 m
# clear of it.
BOUNDS = {'minX': -30, 'maxX': 30, 'minZ': -26, 'maxZ': 26, 'ceiling': 24, 'killY': -6}
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, plate (a walkable floor: garden, gallery, roofs, bridges, terraces)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True, plate=False):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp, plate=plate))
def stair(i, xa, xb, z0, z1, ya, yb, steps, solid=False, mat='stone'):
    # a flight along x from (xa, ya) to (xb, yb), z0..z1 wide. Solid flights stand on the floor at min(ya, yb); others are 20 cm slabs
    # (hung over the void)
    run = (xb - xa) / steps; rise = (yb - ya) / steps; base = min(ya, yb)
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1))); y1 = ya + rise * (k + 1); y0 = base if solid else y1 - .2
        add('%s-%02d' % (i, k), round(a, 4), round(b, 4), round(y0, 4), round(y1, 4), z0, z1, '/', mat=mat)
def rail(i, x0, x1, z0, z1, y0, h=1.1, **k): add(i, x0, x1, y0, y0 + h, z0, z1, '-', mat='metal', **k)   # a low glass balustrade

# ---- the garden terrace (y 0): the tower's top finished floor, x +-26, z +-16, open on every side
add('skyframe-garden', -26, 26, -7, 0, -16, 16, '.', mat='foundation', mirror=False, plate=True)
# ---- the lift core (10 x 10, to 12 m; the climbing crane's mast rises out of it): solid but for a lift lobby cut into its north
#      face (x -4.5..-1.5, 3.2 deep, head 3.0) and another into its south: the west base never sees the east
add('skyframe-core-w', -5, -4.5, 0, 12, -5, 5, '#')
add('skyframe-core-body-w', -4.5, -1.5, 0, 12, -5, 1.8, '#')
add('skyframe-core-mid', -1.5, 1.5, 0, 12, -5, 5, '#', mirror=False)
add('skyframe-core-lintel-n', -4.5, -1.5, 3.0, 12, 1.8, 5, '#')
# ---- the gallery (3.6): a 4 m ring round the core on eight frame columns (to 12 m: the frame goes on up above)
for k, (a, b, c, d) in enumerate(((-9, 9, 5, 9), (-9, -5, -5, 5))):
    add('skyframe-gallery-%s' % 'nw'[k], a, b, LU - .4, LU, c, d, '.', plate=True)
for nm, (x, z) in (('w-0', (-8.4, 5.6)), ('w-1', (-8.4, -5.6)), ('n-0', (-3.2, 8.4)), ('n-1', (3.2, 8.4))):
    add('skyframe-column-%s' % nm, x - .3, x + .3, 0, 12, z - .3, z + .3, 'C', mat='metal')
rail('skyframe-rail-gallery-w-0', -9, -8.8, -6.6, -1.5, LU); rail('skyframe-rail-gallery-w-1', -9, -8.8, 1.5, 7.2, LU)
for k, (a, b) in enumerate(((-9, -5), (-2, 2), (5, 9))): rail('skyframe-rail-gallery-n-%d' % k, a, b, 8.8, 9, LU, mirror=(k != 1))
rail('skyframe-rail-gallery-s-1', -2, 2, -9, -8.8, LU, mirror=False)
# the gallery stair (18 risers, solid, rising east to the gallery's west edge by its north-west corner)
stair('skyframe-stair-gallery-w', -14.4, -9.0, 7.2, 9.0, 0, LU, 18, solid=True)
# ---- the west pavilion (x -26..-14, z -16..-6.6): a garden room under a roof terrace (3.6); doors in its east and north walls
add('skyframe-pavilion-w-wall-w', -26, -25.5, 0, 3.2, -16, -6.6, '#')
add('skyframe-pavilion-w-wall-s', -25.5, -14, 0, 3.2, -16, -15.5, '#')
add('skyframe-pavilion-w-wall-e-0', -14.5, -14, 0, 3.2, -15.5, -13.6, '#'); add('skyframe-pavilion-w-wall-e-1', -14.5, -14, 0, 3.2, -11.6, -6.6, '#')
add('skyframe-pavilion-w-head-e', -14.5, -14, 2.6, 3.2, -13.6, -11.6, '#')
add('skyframe-pavilion-w-wall-n', -23.5, -14.5, 0, 3.2, -7.1, -6.6, '#'); add('skyframe-pavilion-w-head-n', -25.5, -23.5, 2.6, 3.2, -7.1, -6.6, '#')
add('skyframe-pavilion-w-roof', -26, -14, 3.2, LU, -16, -6.6, '.', plate=True)
add('skyframe-counter-w', -21.2, -17.2, 0, 1.0, -10.4, -9.6, 'p')
# its roof stair up the north face (18 risers, solid, rising west to a landing) and the roof's balustrades (gaps: falls and a jump down)
stair('skyframe-stair-roof-w', -15.6, -21.0, -6.6, -4.8, 0, LU, 18, solid=True)
add('skyframe-landing-roof-w', -22.8, -21.0, 0, LU, -6.6, -4.8, '.', plate=True)
rail('skyframe-rail-roof-w-w-0', -26, -25.8, -16, -12, LU); rail('skyframe-rail-roof-w-w-1', -26, -25.8, -10, -6.6, LU)
rail('skyframe-rail-roof-w-s-0', -25.8, -21, -16, -15.8, LU); rail('skyframe-rail-roof-w-s-1', -19, -14, -16, -15.8, LU)
rail('skyframe-rail-roof-w-n-0', -25.8, -22.8, -6.8, -6.6, LU); rail('skyframe-rail-roof-w-n-1', -21, -14.2, -6.8, -6.6, LU)
rail('skyframe-rail-roof-w-e-0', -14.2, -14, -15.8, -13, LU); rail('skyframe-rail-roof-w-e-1', -14.2, -14, -11, -9, LU)
# the glass bridge from the roof to the gallery's south-west corner
add('skyframe-bridge-w', -14, -9, 3.2, LU, -9, -6.6, '.', plate=True)
rail('skyframe-rail-bridge-w-s', -14, -9, -9, -8.8, LU); rail('skyframe-rail-bridge-w-n', -14, -9.2, -6.8, -6.6, LU)
# ---- the north cloud terrace (-3.2): hung outside the garden's north edge, x -18..6, a stair of slabs over the clouds at each end
add('skyframe-terrace-n', -18, 6, -3.8, LL, 16, 22, '.', plate=True)
stair('skyframe-stair-terrace-n-w', -18.0, -22.8, 16, 18, LL, L0, 16); add('skyframe-landing-terrace-n-w', -24.6, -22.8, -.2, 0, 16, 18, '.', plate=True)
stair('skyframe-stair-terrace-n-e', 6.0, 10.8, 16, 18, LL, L0, 16); add('skyframe-landing-terrace-n-e', 10.8, 12.6, -.2, 0, 16, 18, '.', plate=True)
rail('skyframe-rail-terrace-n-out-0', -18, -9, 21.8, 22, LL); rail('skyframe-rail-terrace-n-out-1', -5, 6, 21.8, 22, LL)   # the gap: the crosswind's
rail('skyframe-rail-terrace-n-end-w', -18, -17.8, 18, 21.8, LL); rail('skyframe-rail-terrace-n-end-e', 5.8, 6, 18, 21.8, LL)
# ---- the garden's balustrades (1.1 m glass; the gaps are open edges: falls)
rail('skyframe-rail-garden-w-0', -26, -25.8, -6.6, 6, L0); rail('skyframe-rail-garden-w-1', -26, -25.8, 9, 15.8, L0)
for k, (a, b) in enumerate(((-26, -24.6), (-22.8, -12), (-9, -1), (2, 10.8), (12.6, 14))): rail('skyframe-rail-garden-n-%d' % k, a, b, 15.8, 16, L0)
# ---- the garden's cover. The west base: a clipped hedge before the spawn, a living wall on its north flank, a crate of plants
add('skyframe-hedge-w-0', -17.4, -16.6, 0, 1.1, -2.6, 2.6, 'h')
add('skyframe-greenwall-w-0', -17.4, -16.8, 0, 2.8, 3.6, 6.6, 'W')
add('skyframe-crate-w-0', -20.0, -18.8, 0, 1.2, 4.6, 5.8, 'x', mat='wood', role='cover', hp=34)
# the west forecourt between the base and the gallery: a living wall across the middle
add('skyframe-greenwall-w-1', -12.6, -12.0, 0, 2.8, -2.2, 2.2, 'W')
# the north-west lawn (the crane's first drop zone at -15, 11): a tree in its planter, a hedge, pallets of paving
def tree(i, x, z, y=0.0, mirror=True):
    add('skyframe-planter-%s' % i, x - 1.2, x + 1.2, y, y + 1.0, z - 1.2, z + 1.2, 'p', mirror=mirror)
    add('skyframe-tree-%s' % i, x - .25, x + .25, y + 1.0, y + 4.4, z - .25, z + .25, 'T', mat='wood', mirror=mirror)
tree('n-w-0', -22.4, 12.6)
add('skyframe-hedge-n-w-0', -20.0, -16.0, 0, 1.1, 13.7, 14.5, 'h')
add('skyframe-pallet-n-w-0', -12.2, -11.0, 0, 1.1, 13.4, 14.4, 'x', role='cover', hp=150)
# the north lane (z 9..16 between the gallery and the edge): two trees, a living wall before the north lift lobby, a bundle of beams,
# crates, a hedge, and the screen before the east pavilion's door
tree('n-0', 1.6, 12.6); tree('n-1', 7.6, 11.4)
add('skyframe-greenwall-n-0', -5.0, -1.0, 0, 2.8, 10.4, 11.0, 'W')
add('skyframe-bundle-n-0', 3.4, 8.4, 0, .9, 14.3, 15.2, 'r', mat='metal')
add('skyframe-crate-n-0', 9.6, 10.8, 0, 1.2, 9.6, 10.8, 'x', mat='wood', role='cover', hp=34)
add('skyframe-greenwall-n-1', 11.4, 12.0, 0, 2.8, 10.4, 14.8, 'W')
add('skyframe-hedge-n-0', -9.6, -8.8, 0, 1.1, 10.4, 13.4, 'h')
# the pavilion roofs: a planter bed with a tree at the outer corner
tree('roof-w', -24.2, -14.2, y=LU)

# ---- hazards (UnityMaps.json `hazards[]`; existing kinds). Factory kinds time from the arm time (FightAt + 4 s).
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), radius=0, first=0, start=0, period=0,
             offset=0, warn=0, danger=0, speed=0, length=0, charge=0, live=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
# The tower crane's beam drop (kind `crane`): the load is a bundle of steel beams (6 x 0.9 x 1.2 m), carried 11 m up round the tower
# (the zones ring the core, so no hop crosses it) and dropped on four zones in turn: the north-west lawn, the east pavilion's roof,
# the south-east lawn, the west pavilion's roof. Each cycle: 6 s travel, a 3 s telegraph over the zone, a 0.95 s fall, 6 s landed
# (cover), 1.5 s regrip, 2 s hoist. `a`/`b` only place the park (their midpoint, over the north lane). The bundle hangs along x
# all the way (taglines hold its heading), as its landed collider lies.
LOAD = (6.0, .9, 1.2)
ZONES = [(-15, L0, 11), (20, LU, 11), (15, L0, -11), (-20, LU, -11)]
crane = hz(id='skyframe-crane', kind='crane', a=v(-12, 38, 11.5), b=v(12, 38, 11.5), size=v(*LOAD), carry=11.0,
           start=6, period=20, warn=3.0, travel=6.0, rise=.95, dwell=6.0, hoist=2.0, lethal=True,
           zones=[{'center': v(x, y, z), 'size': v(round(LOAD[0] + .6, 3), 2.2, round(LOAD[2] + .6, 3))} for x, y, z in ZONES])
PAINT_ZONE = (LOAD[0] + 1.0, LOAD[2] + 1.0); REGRIP = 1.5                 # the painted footprint; NativeFactoryHazards.RegripSeconds
# The crosswind (kind `vent` with damage ticks and `push` along `axis`, as the Causeway's solar wind): a gust across the middle of
# each cloud terrace toward the gap in its outer balustrade. 2.6 s telegraph, a 2 s gust: 2 HP per 0.25 s tick and 12 m/s^2 outward.
WIND = (-11.0, -3.0)
HAZ = [crane]
for side, (sg, off) in enumerate(((1, 2.0), (-1, 12.0))):
    HAZ.append(hz(id='skyframe-wind-%s' % 'ns'[side], kind='vent', a=v(sg * (WIND[0] + WIND[1]) / 2, LL, sg * 19), size=v(WIND[1] - WIND[0], 3.0, 6.0), axis=v(0, 0, sg),
                  start=0, offset=off, period=20, warn=2.6, dwell=2.0, tick=.25, damage=2, push=12, pushMode='wind'))

# ---- spawns (x, z, floor); yaw from +z toward +x, facing the origin
teamA = [(-22.5, 1.4, L0), (-22.5, -1.4, L0), (-24.6, 1.4, L0), (-24.6, -1.4, L0)]
teamB = [(-x, -z, y) for x, z, y in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
_f, _g, _h = (2.6, 19.6, LL), (-20.0, -12.6, L0), (-3.0, 3.4, L0)        # the north cloud terrace, the west pavilion, the north lift lobby
ffa = [teamA[0], teamB[0], _f, (-_f[0], -_f[1], _f[2]), _g, (-_g[0], -_g[1], _g[2]), _h, (-_h[0], -_h[1], _h[2])]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only exclusion columns (infinite upward): over every balustrade and every tree (its crown hangs over the trunk's top);
#      the integration keeps a balustrade's only at a lethal drop, for bots (tools/generate_stage_hazards.py open_stage)
def pilot_for(e):
    x0, x1, y0, y1, z0, z1 = e['box']; return dict(id=e['id'] + '-top', center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .1, 3))
def needs_pilot(e): return e['ch'] in '-T'
PILOT = [pilot_for(e) for e in els if needs_pilot(e)]

# ---- level bookkeeping
LEVELS = [(LL, 'level -3.2 (cloud terraces)'), (L0, 'level 0 (garden, pavilions, lift lobbies)'), (LU, 'level 3.6 (gallery, roofs, bridges)')]
def base_of(e):
    y0 = e['box'][2]
    return LU if y0 >= LU - .45 else L0 if y0 >= -.65 else LL if y0 >= LL - .65 else y0
def floor_at(e, h): return abs(e['box'][3] - h) < .05 and (e['plate'] or e['ch'] == '/')
def in_slice(e, h): y0, y1 = e['box'][2], e['box'][3]; return y0 < h + 2.0 and y1 > h + .05
def footprint():
    """Ground footprints of every hazard's live volume: (x0, x1, z0, z1, y0, y1, name)."""
    out = []
    for j, zn in enumerate(crane['zones']):
        c = zn['center']; out.append((c['x'] - PAINT_ZONE[0] / 2, c['x'] + PAINT_ZONE[0] / 2, c['z'] - PAINT_ZONE[1] / 2, c['z'] + PAINT_ZONE[1] / 2, c['y'], crane['carry'] + LOAD[1], 'crane-zone-%d' % j))
    for h in HAZ:
        if h['kind'] == 'vent': a, s = h['a'], h['size']; out.append((a['x'] - s['x'] / 2, a['x'] + s['x'] / 2, a['z'] - s['z'] / 2, a['z'] + s['z'] / 2, a['y'], a['y'] + s['y'], h['id']))
    return out

def paint():
    W = int(X1 - X0) + 4; R = int((BOUNDS['maxZ'] - BOUNDS['minZ']) // 2); out = []
    for h, name in LEVELS:
        g = [[' '] * W for _ in range(R)]
        def put(x, z, ch):
            c = int(math.floor(x - X0 + 2)); r = int((BOUNDS['maxZ'] - z) // 2)
            if 0 <= c < W and 0 <= r < R: g[r][c] = ch
        def rect(x0, x1, z0, z1, ch):
            zz = z0
            while zz < z1 - 1e-9:
                xx = x0
                while xx < x1 - 1e-9: put(xx + .25, zz + .25, ch); xx += .5
                zz += .5
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; rect(b[0], b[1], b[4], b[5], '.')
        for x0, x1, z0, z1, y0, y1, nm in footprint():
            if abs(y0 - h) < .05: rect(x0, x1, z0, z1, 'Z' if 'crane' in nm else '~')
        for e in sorted(els, key=lambda e: e['ch'] in '#-'):
            if in_slice(e, h) and not e['plate']: b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
        for nm, (x, z, y) in [('1234'[i], p) for i, p in enumerate(teamA)] + [('5678'[i], p) for i, p in enumerate(teamB)] + list(zip(ffa_names[2:], ffa[2:])):
            if abs(y - h) < .05: put(x, z, nm)
        out.append(name)
        for r in range(R): z = BOUNDS['maxZ'] - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- spawn sightlines in 3D: a box blocks where the segment passes through it. Boxes topping out below 2.0 m over the lower eye's
#      floor never count, nor boxes under 2.0 m over their own level (half cover hides no standing pilot); floor plates do.
def seg_hits(a, b, e, lo):
    x0, x1, y0, y1, z0, z1 = e['box']
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
        lo = min(a[2], b[2]); opens = []
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
            'maps': [{'id': 'skyframe', 'name': 'Skyframe', 'tag': 'A garden above the clouds. The crane is still working.', 'theme': 'skyframe',
                      'music': 'skyframe-theme', 'ambience': 'skyframe-wind', 'look': 'skyframe-golden-hour', 'sky': 0xF2A65E, 'floor': 0xD8CDB8, 'accent': 0xFFB347,
                      'rotation': True, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    PW, GAP = int((X1 - X0 + 4) * scale), 2 * scale; PH = int((BOUNDS['maxZ'] - BOUNDS['minZ']) * scale); W, H = PW, 3 * PH + 2 * GAP
    img = bytearray(b'\x10\x12\x1c' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        oy = k * (PH + GAP); i0, i1 = int((x0 - X0 + 2) * scale), int((x1 - X0 + 2) * scale); j0, j1 = oy + int((BOUNDS['maxZ'] - z1) * scale), oy + int((BOUNDS['maxZ'] - z0) * scale)
        for j in range(max(oy, j0), min(oy + PH, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    col = {'#': (170, 160, 150), 'C': (110, 140, 190), 'T': (120, 90, 60), 'p': (150, 170, 120), 'h': (80, 150, 80), 'W': (60, 130, 70), 'r': (200, 120, 60),
           'x': (190, 150, 100), '-': (160, 220, 240), '/': (200, 200, 205)}
    for k, (h, name) in enumerate(LEVELS):
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; fill(k, b[0], b[1], b[4], b[5], (150, 146, 140))
        for x0, x1, z0, z1, y0, y1, nm in footprint():
            if abs(y0 - h) < .05: fill(k, x0, x1, z0, z1, (255, 120, 40) if 'crane' in nm else (120, 220, 255), .45)
        for e in sorted(els, key=lambda e: e['box'][3]):
            if in_slice(e, h) and not e['plate']: b = e['box']; fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .55 if e['box'][3] - max(h, base_of(e)) < 1.25 else 1.0)
        for i, p in enumerate(teamA + teamB):
            if abs(p[2] - h) < .05: fill(k, p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if i < 4 else (255, 90, 120))
        for p in ffa[2:]:
            if abs(p[2] - h) < .05: fill(k, p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (250, 250, 250))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint()); fails = []
    def need(ok, what):
        if not ok: fails.append(what)
        return ok
    ids = [e['id'] for e in els]; need(len(ids) == len(set(ids)), 'unique ids'); need(all(i.startswith('skyframe-') for i in ids), 'id prefix')
    # point symmetry: every block has its mirror
    boxes = {tuple(round(q, 3) for q in e['box']) for e in els}
    need(all(tuple(round(q, 3) for q in (-b[1], -b[0], b[2], b[3], -b[5], -b[4])) in boxes for b in boxes), 'point symmetry')
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    need(bad == 0, 'spawn sightlines')
    sp = ffa + teamA[1:] + teamB[1:]
    mf = min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)); mab = min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m (3D); min A-B spacing %.1f m' % (mf, mab)); need(mf >= 9, 'FFA spacing'); need(mab >= 24, 'A-B spacing')
    print('FFA floors: ' + ', '.join('%s %.1f' % (n, p[2]) for n, p in named))
    # hazard clearance, per floor: a spawn keeps 3 m (plan) from any live volume that spans its body's height
    worst = min((dist_rect(p, r[:4]), r[6], p) for p in sp for r in footprint() if r[4] - .1 <= p[2] + 1.8 and r[5] >= p[2])
    print('closest spawn to a hazard volume on its floor: %.1f m (%s, spawn %s)' % worst); need(worst[0] >= 3, 'hazard clearance')
    inb = all(X0 + CAP <= x <= X1 - CAP and Z0 + CAP <= z <= Z1 - CAP for x, z, y in sp)
    print('all spawns inside the structure:', inb); need(inb, 'bounds')
    past = [e['id'] for e in els if e['box'][0] < BOUNDS['minX'] + 4 - 1e-6 or e['box'][1] > BOUNDS['maxX'] - 4 + 1e-6 or e['box'][4] < BOUNDS['minZ'] + 4 - 1e-6 or e['box'][5] > BOUNDS['maxZ'] - 4 + 1e-6]
    low_top = min(e['box'][3] for e in els if e['plate'])
    print('blocks within 4 m of the bounds (the void must run 4 m past every edge): %s; lowest floor %.1f, killY %d' % (past or 'none', low_top, BOUNDS['killY']))
    need(not past and low_top + .9 - 2 > BOUNDS['killY'], 'open edges')
    # crane zones: 0.5 m clear of every block at the zone's level, and an open shaft from the zone up to the carried load; the load's
    # way round the tower (straight hops between zones at the carry height) clear of every block
    zc = []; shaft = []
    for x0, x1, z0, z1, fy, top, nm in footprint():
        if 'crane' not in nm: continue
        for e in els:
            b = e['box']
            if abs(b[3] - fy) < .01 and e['plate']: continue
            if b[3] > fy + .01 and b[2] < top and b[0] < x1 and b[1] > x0 and b[4] < z1 and b[5] > z0: shaft.append((nm, e['id']))
            elif b[3] > fy + .01 and b[2] < fy + 2.2: dx = max(x0 - b[1], 0, b[0] - x1); dz = max(z0 - b[5], 0, b[4] - z1); zc.append((math.hypot(dx, dz), e['id'], nm))
    print('closest block to a painted crane zone: %.2f m (%s, %s); blocks in the drop shafts: %s' % (min(zc) + (shaft or 'none',))); need(min(zc)[0] >= .5 and not shaft, 'crane zone clearance')
    zs = [(x, z) for x, y, z in ZONES]; way = []
    for (ax, az), (bx, bz) in zip(zs, zs[1:] + zs[:1]):
        for k in range(41):
            x, z = ax + (bx - ax) * k / 40, az + (bz - az) * k / 40
            for e in els:
                b = e['box']
                if b[3] > crane['carry'] - .5 and b[0] - LOAD[0] / 2 - .5 < x < b[1] + LOAD[0] / 2 + .5 and b[4] - LOAD[2] / 2 - .5 < z < b[5] + LOAD[2] / 2 + .5: way.append(e['id'])
    print('blocks in the carried load\'s way round the tower:', sorted(set(way)) or 'none'); need(not way, 'crane way')
    band = [e['id'] for e in els if e['ch'] not in '-/' and not e['plate'] and 1.3 < e['box'][3] - base_of(e) < 2.2]
    print('blocks topping out 1.3..2.2 m over their level:', band or 'none'); need(not band, 'cover band')
    pil_ids = {p['id'] for p in PILOT}; miss = [e['id'] for e in els if needs_pilot(e) and e['id'] + '-top' not in pil_ids]
    print('pilot blocks: %d (balustrades %d, trees %d); without one: %s' % (len(PILOT), sum(e['ch'] == '-' for e in els), sum(e['ch'] == 'T' for e in els), miss or 'none')); need(not miss, 'pilot blocks')
    flights = {}
    for e in els:
        if e['ch'] == '/': flights.setdefault(e['id'].rsplit('-', 1)[0], []).append(e['box'])
    def flight_bad(bs):
        tops = sorted(round(b[3], 3) for b in bs); steps = [b - a for a, b in zip(tops, tops[1:])]
        return max(steps) > .2 + 1e-6 or min(max(b[1] - b[0], b[5] - b[4]) for b in bs) < 1.6 - 1e-6 or tops[-1] not in (L0, LU)
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
    # hazard timing: nothing lethal in the first 4 s; telegraphs at least today's (3 s for the drop, 0.8 s for a vent)
    c = crane; cyc = c['travel'] + c['warn'] + c['rise'] + c['dwell'] + REGRIP + c['hoist']
    hop = max(math.hypot(ax - bx, az - bz) for (ax, az), (bx, bz) in zip(zs, zs[1:] + zs[:1]))
    print('crane: %d zones (%s), every %.0f s (cycle %.2f s); %.1f s travel (longest hop %.1f m = %.1f m/s) + %.1f s telegraph; fall %.2f s from %.1f m; landed cover %.1f s; first drop at arm + %.0f s'
          % (len(c['zones']), ', '.join('y %.1f' % z['center']['y'] for z in c['zones']), c['period'], cyc, c['travel'], hop, hop / c['travel'], c['warn'], c['rise'], c['carry'], c['dwell'], c['start'] + c['travel'] + c['warn']))
    need(cyc <= c['period'] and c['warn'] >= 3 and all(c['carry'] > z['center']['y'] + z['size']['y'] for z in c['zones']), 'crane timing')
    for h in HAZ:
        if h['kind'] == 'vent':
            first = 4 + h['start'] + h['offset'] + h['period'] - h['warn'] - h['dwell']; need(h['warn'] >= .8 and first >= 4 and h['push'] > 0 and h['damage'] > 0, 'wind timing ' + h['id'])
            print('  %-16s vent  first telegraph at FightAt + %5.2f s, warn %.1f s, gust %.1f s, period %.0f s' % (h['id'], first, h['warn'], h['dwell'], h['period']))
    print('blocks: %d; hazards: %d; pilot blocks: %d; spawns: %d FFA + 2 x %d team' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA))); need(len(els) <= 350, 'block budget')
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'skyframe_m2.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'skyframe_m2.png')); print('wrote', os.path.join(out, 'skyframe_m2.json'), 'and skyframe_m2.png')
    print('FAILED: ' + ', '.join(fails) if fails else 'all checks pass')
    sys.exit(1 if fails else 0)
