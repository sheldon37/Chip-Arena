# Starfall Causeway M1 (an ancient causeway hung in deep space between two broken towers: the main span at 0 with the Breach to
# jump, side balconies, lower walkways at -3.2, tower crowns at 4.4; falling stars, the comet pass and the solar wind) draft and checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z). Team A west (Tower A), team B east.
# usage: python3 docs/arenas/scripts/causeway_m1.py [--out DIR] [--json PATH]   (stdout: plans + checks, exit 1 on any failed check;
#        --json writes the draft only; --out writes causeway_m1.json, draft map data in the UnityMaps.json shape, and causeway_m1.png,
#        three top-down plans (levels -3.2, 0, 4.4), 8 px per metre)
# Design: docs/arenas/CAUSEWAY.md. Ids carry the prefixes the presentation keys its looks and kit on (causeway-span-*, -plaza-*,
# -hall-*, -crown-*, -gable-*, -spire-*, -altar-*, -pylon-*, -warden-*, -watcher-*, -fallen-*, -brazier-*, -rail-*, -stair-*,
# -walk-*, -balcony-*, -keel-*, -keystone). Everything is indestructible stone and star-metal (static).
import math, json, itertools, sys, os, zlib, struct
LW, L0, LC = -3.2, 0.0, 4.4                    # the lower walkways, the span (plazas, halls, balconies), the tower crowns
X0, X1, Z0, Z1 = -38, 38, -9.8, 9.8            # the structure's extent (towers x +-38, walkways z +-9.8)
# The sim clamps bots, abilities and teleports to the bounds minus the capsule (NativeUnityMaps.Clamp), not walking pilots: the bounds
# run 4 m past every open edge, so a pilot who walks, jumps short or is blown off falls into the void and dies at killY.
# killY -6 (owner 2026-11-10: "On the bridge, we can let people get knocked off the map. Its a instant death when falling off"; was
# -10): a body's middle below it dies, about half a second after it leaves the span or the walkways; one standing on the lowest
# floor (the walkways' top, -3.2) is 3.7 m clear of it.
BOUNDS = {'minX': -42, 'maxX': 42, 'minZ': -14, 'maxZ': 14, 'ceiling': 24, 'killY': -6}
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, plate (a walkable floor: decks, plazas, walkways, crowns)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True, plate=False):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp, plate=plate))
def stair(i, xa, xb, z0, z1, ya, yb, steps, solid=False, axis='x', mat='stone'):
    # a flight from (xa, ya) to (xb, yb) along x (z0..z1 wide) or along z (xa/xb are z, z0/z1 x). Solid flights stand on the floor at
    # min(ya, yb); others are 20 cm slabs (hung over the void)
    run = (xb - xa) / steps; rise = (yb - ya) / steps; base = min(ya, yb)
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1))); y1 = ya + rise * (k + 1); y0 = base if solid else y1 - .2
        if axis == 'x': add('%s-%02d' % (i, k), a, b, y0, y1, z0, z1, '/', mat=mat)
        else: add('%s-%02d' % (i, k), z0, z1, y0, y1, a, b, '/', mat=mat)
def plates(i, x0, x1, z0, z1, holes, y0, y1, ch='d', mat='stone'):
    # a floor plate x0..x1, z0..z1 minus rectangular holes (hx0, hx1, hz0, hz1), cut into the fewest strips
    def cut(a0, a1, b0, b1, hs):
        cs = sorted({a0, a1} | {min(max(v, a0), a1) for h in hs for v in h[:2]}); strips = []
        for a, b in zip(cs, cs[1:]):
            if b - a < 1e-6: continue
            segs, z = [], b0
            for c0, c1 in sorted((h[2], h[3]) for h in hs if h[0] < b - 1e-6 and h[1] > a + 1e-6):
                if c0 > z + 1e-6: segs.append((z, min(c0, b1)))
                z = max(z, c1)
            if z < b1 - 1e-6: segs.append((z, b1))
            if strips and strips[-1][2] == segs and abs(strips[-1][1] - a) < 1e-6: strips[-1] = (strips[-1][0], b, segs)
            else: strips.append((a, b, segs))
        return [(a, b, za, zb) for a, b, segs in strips for za, zb in segs]
    px = cut(x0, x1, z0, z1, holes); pz = [(a, b, c, d) for c, d, a, b in cut(z0, z1, x0, x1, [(h[2], h[3], h[0], h[1]) for h in holes])]
    for k, (a, b, za, zb) in enumerate(min(px, pz, key=len)): add('%s-%d' % (i, k), a, b, y0, y1, za, zb, ch, mat=mat, plate=True)
def rail_x(i, x0, x1, y0, z0, z1, h=1.1, ch='-', **k): add(i, x0, x1, y0, y0 + h, z0, z1, ch, **k)     # along x, z0..z1 thick
def rail_z(i, x0, x1, y0, z0, z1, h=1.1, ch='-', **k): add(i, x0, x1, y0, y0 + h, z0, z1, ch, **k)     # along z, x0..x1 thick

# ---- Tower A (x -38..-28): its masonry body (the hall's floor, 7 m deep, the stump below is scenery), the gate hall's walls (0..4),
#      the crown (4.4) over it, overhanging 1.8 m north and south as a gallery over the void
add('causeway-hall-a-floor', -38, -28, -7, 0, -8, 8, '.', mat='foundation', plate=True)
add('causeway-hall-a-wall-n', -38, -28, 0, 4.0, 7.4, 8, '#')
add('causeway-hall-a-wall-s', -38, -28, 0, 4.0, -8, -7.4, '#')
add('causeway-hall-a-wall-w', -38, -37.4, 0, 4.0, -7.4, 7.4, '#')
# the east wall: the great arch (z +-2.2, head 3.4) to the plaza and a side door each side (z 4.5..6.5, head 2.6)
for k, (a, b) in enumerate(((-7.4, -6.5), (-4.5, -2.2), (2.2, 4.5), (6.5, 7.4))): add('causeway-hall-a-wall-e-%d' % k, -28.6, -28, 0, 4.0, a, b, '#')
add('causeway-hall-a-arch', -28.6, -28, 3.4, 4.0, -2.2, 2.2, '#')
add('causeway-hall-a-lintel-0', -28.6, -28, 2.6, 4.0, -6.5, -4.5, '#'); add('causeway-hall-a-lintel-1', -28.6, -28, 2.6, 4.0, 4.5, 6.5, '#')
# the star altar: a rune monolith inside the arch, floor to vault, so nothing on the span sees into the hall (2.6 m clear behind the
# arch for the way round it)
add('causeway-altar-a', -32.4, -31.2, 0, 4.0, -2.8, 2.8, 'A')
# route 1 of 2 to the crown: from the north door up the hall's north wall (22 risers, rising west) under an open stairwell
stair('causeway-stair-hall-a', -29.6, -36.2, 5.6, 7.4, 0, LC, 22, solid=True)
HOLE = (-36.2, -29.6, 5.0, 7.4)                 # 0.6 m wider than the flight: headroom for the capsule on its upper steps
plates('causeway-crown-a', -38, -28, -9.8, 9.8, [HOLE], LC - .4, LC, ch='c')
# crown parapets (1.1 m; broken gaps are falls into the void), the east gable over the arch with its great window (sill 1.0 m over
# the crown, head 3.0 m), the broken spire in the south-west corner
for k, (a, b) in enumerate(((-9.8, -1.0), (2.4, 9.8))): rail_z('causeway-rail-crown-a-w-%d' % k, -38, -37.7, LC, a, b)
for k, (a, b) in enumerate(((-37.7, -34.0), (-32.0, -29.0))): rail_x('causeway-rail-crown-a-n-%d' % k, a, b, LC, 9.5, 9.8)
for k, (a, b) in enumerate(((-37.7, -33.0), (-31.0, -28.0))): rail_x('causeway-rail-crown-a-s-%d' % k, a, b, LC, -9.8, -9.5)
rail_z('causeway-rail-crown-a-e-0', -28.3, -28, LC, -9.5, -7.0); rail_z('causeway-rail-crown-a-e-1', -28.3, -28, LC, 7.0, 8.2)
add('causeway-gable-a-s', -28.6, -28, LC, LC + 4.8, -7.0, -1.4, 'G'); add('causeway-gable-a-n', -28.6, -28, LC, LC + 4.8, 1.4, 7.0, 'G')
add('causeway-gable-a-sill', -28.6, -28, LC, LC + 1.0, -1.4, 1.4, 'G'); add('causeway-gable-a-head', -28.6, -28, LC + 3.0, LC + 4.8, -1.4, 1.4, 'G')
add('causeway-spire-a', -37.4, -35.0, LC, 13.0, -9.2, -6.8, 'T')
add('causeway-brazier-a-crown', -36.8, -35.6, LC, LC + 1.0, -.6, .6, 'c')
# ---- the plaza (bridgehead, x -28..-20) between the tower and the span; route 2 to the crown: the outer stair up the tower's north
#      face from the plaza (22 risers, solid, arriving at the crown's north-east corner)
add('causeway-plaza-a', -28, -20, -7, 0, -9.8, 9.8, '.', mat='foundation', plate=True)
stair('causeway-stair-crown-a', -21.62, -28.0, 8.2, 9.8, 0, LC, 22, solid=True)   # 0.29 m treads: the wing's corner stays open
add('causeway-warden-a-n', -22.6, -21.2, 0, 5.4, 2.6, 4.0, 'S'); add('causeway-warden-a-s', -22.6, -21.2, 0, 5.4, -4.0, -2.6, 'S')
add('causeway-brazier-a-n', -26.6, -25.4, 0, 1.0, 6.6, 7.8, 'c'); add('causeway-brazier-a-s', -26.6, -25.4, 0, 1.0, -7.8, -6.6, 'c')
# the lantern gates: a wall across each plaza wing at the head of its walkway stair, its 2.2 m door (head 2.6) at the outer edge where
# the stair arrives, so the walkways never see into the wings
for side, (j0, j1, d0, d1) in (('n', (4.0, 7.4, 7.4, 9.8)), ('s', (-7.4, -4.0, -9.8, -7.4))):
    add('causeway-gate-a-%s' % side, -20.6, -20.0, 0, 4.0, j0, j1, '#'); add('causeway-gate-a-%s-head' % side, -20.6, -20.0, 2.6, 4.0, d0, d1, '#')
for k, (a, b) in enumerate(((-28, -24.6), (-22.6, -20.0))): rail_x('causeway-rail-plaza-a-s-%d' % k, a, b, L0, -9.8, -9.5)
# ---- the span, west half (x -20..-3; the Breach x -3..3 with the floating keystone at its heart), its keel under the deck
add('causeway-span-w', -20, -3, -7, 0, -3.4, 3.4, '.', plate=True)               # the deck on its keel, one block (7 m deep)
add('causeway-span-w-n', -20, -3, -.6, 0, 3.4, 5, '.', plate=True); add('causeway-span-w-s', -20, -3, -.6, 0, -5, -3.4, '.', plate=True)
add('causeway-keystone', -1.1, 1.1, -3.4, 0, -1.4, 1.4, 'k', mirror=False, plate=True)
# star-metal pylons framing the balconies (full cover), balustrades (1.1), broken toward the Breach (open edges, falls)
for k, (a, b) in enumerate(((-17.2, -16.0), (-9.4, -8.2))):
    add('causeway-pylon-w-n-%d' % k, a, b, 0, 4.8, 3.8, 5.0, 'P', mat='metal'); add('causeway-pylon-w-s-%d' % k, a, b, 0, 4.8, -5.0, -3.8, 'P', mat='metal')
for k, (a, b) in enumerate(((-20.0, -17.2), (-8.2, -6.0))):
    rail_x('causeway-rail-span-w-n-%d' % k, a, b, L0, 4.7, 5.0); rail_x('causeway-rail-span-w-s-%d' % k, a, b, L0, -5.0, -4.7)
# the balconies (y 0, x -16..-9.4) out over the gap, their balustrades broken mid-way (a drop to the walkway below)
for side, (z0, z1) in (('n', (5.0, 7.2)), ('s', (-7.2, -5.0))):
    add('causeway-balcony-%s-w' % side, -16.0, -9.4, -.5, 0, z0, z1, '.', plate=True)
    zo = (6.9, 7.2) if side == 'n' else (-7.2, -6.9); zs = (5.0, 6.9) if side == 'n' else (-6.9, -5.0)
    for k, (a, b) in enumerate(((-16.0, -13.6), (-11.6, -9.4))): rail_x('causeway-rail-balcony-%s-w-%d' % (side, k), a, b, L0, *zo)
    rail_z('causeway-rail-balcony-%s-w-end-0' % side, -16.0, -15.7, L0, *zs); rail_z('causeway-rail-balcony-%s-w-end-1' % side, -9.7, -9.4, L0, *zs)
# statues: the watcher at the Breach's lip, and fallen stones (half cover)
add('causeway-watcher-w', -6.4, -5.0, 0, 5.0, 2.0, 3.4, 'S')
add('causeway-fallen-w-0', -12.8, -10.6, 0, 1.1, -3.4, -2.2, 'f')
add('causeway-fallen-w-1', -4.4, -3.2, 0, 1.0, -4.4, -2.8, 'f')
add('causeway-fallen-w-2', -18.6, -17.4, 0, 1.1, -0.4, 1.0, 'f')
# ---- the lower walkways (y -3.2, z 6.8..9.8 and its mirror), hung on the keel's brackets alongside the span: the inner rail whole,
#      the outer rail broken (falls); a stair (16 risers, slabs over the void) up to the plaza's wing at each end
add('causeway-walk-n', -15.2, 15.2, -7, LW, 6.8, 9.8, '.', plate=True)        # the top of a deep stone beam, like the keel
stair('causeway-stair-walk-n-w', -15.2, -20.0, 6.8, 9.8, LW, L0, 16)
stair('causeway-stair-walk-n-e', 15.2, 20.0, 6.8, 9.8, LW, L0, 16)
rail_x('causeway-rail-walk-n-in', -15.2, 15.2, LW, 6.8, 7.0)
for k, (a, b) in enumerate(((-15.2, -10.4), (-7.6, 3.0), (5.8, 15.2))): rail_x('causeway-rail-walk-n-out-%d' % k, a, b, LW, 9.6, 9.8)

# ---- hazards (UnityMaps.json `hazards[]`; existing kinds). Factory kinds time from the arm time (FightAt + 4 s); the maglev from FightAt.
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), radius=0, first=0, start=0, period=0,
             offset=0, warn=0, danger=0, speed=0, length=0, charge=0, live=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
# Falling stars (kind `press`, re-rigged as a meteor): the "ram" is the star's lethal column, 2.6 m square, dropped from 46 m to the
# floor. Each cycle: open, a 3.2 s telegraph (the impact ring and the star kindling overhead), a 0.55 s fall, 0.5 s blast (lethal: the
# crater), a 0.35 s lift (the column clears; the presentation leaves the scorch). Six points in three point-symmetric pairs (the span
# by the Breach, the span's ends, the plazas before the tower arches), one strike every 3.3 s somewhere, each point every 20 s.
STAR = 2.6; STAR_H = 2.6; SKY = 46.0
# (the press kind's lethal band is absolute, the ram's underside below 1.8 m: every strike lands on a y-0 floor)
STRIKES = [(-7.0, L0, -2.4), (-19.0, L0, 2.6), (-24.2, L0, -0.6)]
HAZ = []
for k, (x, y, z) in enumerate(STRIKES):
    for side, (px, pz) in enumerate(((x, z), (-x, -z))):
        HAZ.append(hz(id='causeway-star-%d%s' % (k, 'ab'[side]), kind='press', a=v(px, SKY, pz), b=v(px, y + STAR_H / 2, pz), size=v(STAR, STAR_H, STAR),
                      start=0, offset=round((k * 2 + side) * 10 / 3, 3), period=20, warn=3.2, rise=.55, dwell=.5, travel=.35, lethal=True))
# The comet pass (kind `maglev`): along the span's centre lane (x -20..20, z +-1.6, 2.6 m tall), alternating direction each pass.
# 7 s warning (the lane's runes run toward the far end, the comet kindles), 3.5 s danger, the pass (1.47 s), 2 s fading; every 36 s,
# the first at FightAt + 18.
COMET = hz(id='causeway-comet', kind='maglev', center=v(0, 1.3, 0), size=v(40, 2.6, 3.2), axis=v(1, 0, 0), first=18, period=36, warn=7,
           danger=3.5, speed=38, length=16, lethal=True)
HAZ.append(COMET)
# The solar wind (kind `vent` with damage ticks and `push` along `axis`): a gust across the balcony section of each half, the span,
# the balconies and the walkway under them, from y -3.2 to 3.4. 2.6 s telegraph (the shimmering front and streaming motes), a 2 s
# gust: 2 HP per 0.25 s tick and 12 m/s^2 toward the downwind edge (west half north, east half south; walk into it to hold).
WIND = (-16.5, -8.5)
for side, (x0, x1, ax, off) in enumerate(((WIND[0], WIND[1], 1, 2.0), (-WIND[1], -WIND[0], -1, 12.0))):
    HAZ.append(hz(id='causeway-wind-%s' % 'we'[side], kind='vent', a=v((x0 + x1) / 2, LW, 0), size=v(x1 - x0, 6.6, 20.0), axis=v(0, 0, ax),
                  start=0, offset=off, period=20, warn=2.6, dwell=2.0, tick=.25, damage=2, push=12, pushMode='wind'))

# ---- spawns (x, z, floor); yaw from +z toward +x, facing the origin
teamA = [(-34.6, 3.4, L0), (-34.6, -3.4, L0), (-36.6, 1.4, L0), (-36.6, -1.4, L0)]
teamB = [(-x, -z, y) for x, z, y in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
_f, _g, _h = (-4.6, 8.3, LW), (-22.6, -6.0, L0), (-31.0, -4.6, LC)
ffa = [teamA[0], teamB[0], _f, (-_f[0], -_f[1], _f[2]), _g, (-_g[0], -_g[1], _g[2]), _h, (-_h[0], -_h[1], _h[2])]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only exclusion columns (infinite upward): over every rail and unstandable top; the integration keeps those at a lethal edge
#      or out of jump reach (tools/generate_stage_hazards.py open_tops)
def pilot_for(e):
    x0, x1, y0, y1, z0, z1 = e['box']; return dict(id=e['id'] + '-top', center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .1, 3))
def open_sky(e):
    b = e['box']; return not any(o['plate'] and o['box'][2] >= b[3] and o['box'][0] < b[1] and o['box'][1] > b[0] and o['box'][4] < b[5] and o['box'][5] > b[4] for o in els)
def needs_pilot(e): return e['ch'] in '-PSTGA' or (e['ch'] == '#' and open_sky(e))
PILOT = [pilot_for(e) for e in els if needs_pilot(e)]

# ---- level bookkeeping
LEVELS = [(LW, 'level -3.2 (walkways)'), (L0, 'level 0 (span, plazas, halls, balconies)'), (LC, 'level 4.4 (crowns)')]
def base_of(e):
    y0 = e['box'][2]
    return LC if y0 >= LC - .45 else L0 if y0 >= -.65 else LW if y0 >= LW - .45 else y0
def floor_at(e, h): return abs(e['box'][3] - h) < .05 and (e['plate'] or e['ch'] == '/')
def in_slice(e, h): y0, y1 = e['box'][2], e['box'][3]; return y0 < h + 2.0 and y1 > h + .05
def thin(e): return e['plate']
def footprint(hd):
    """Ground footprints of every hazard's live volume: (x0, x1, z0, z1, y0, y1, name)."""
    out = []
    for h in hd:
        if h['kind'] == 'press': c, s = h['b'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, c['y'] - s['y'] / 2, h['a']['y'] + s['y'] / 2, h['id']))
        elif h['kind'] == 'maglev': c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, c['y'] - s['y'] / 2, c['y'] + s['y'] / 2, h['id']))
        elif h['kind'] == 'vent': a, s = h['a'], h['size']; out.append((a['x'] - s['x'] / 2, a['x'] + s['x'] / 2, a['z'] - s['z'] / 2, a['z'] + s['z'] / 2, a['y'], a['y'] + s['y'], h['id']))
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
        for x0, x1, z0, z1, y0, y1, nm in footprint(HAZ):
            if y0 - .05 <= h + .1 <= y1 and 'comet' not in nm: rect(x0, x1, z0, z1, 'Z' if 'star' in nm else '~')
        for e in sorted(els, key=lambda e: e['ch'] in '#-'):
            if in_slice(e, h) and not thin(e): b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
        for nm, (x, z, y) in [('1234'[i], p) for i, p in enumerate(teamA)] + [('5678'[i], p) for i, p in enumerate(teamB)] + list(zip(ffa_names[2:], ffa[2:])):
            if abs(y - h) < .05: put(x, z, nm)
        out.append(name)
        for r in range(R): z = BOUNDS['maxZ'] - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- spawn sightlines in 3D (as skyframe_m1.py): a box blocks where the segment passes through it. Boxes topping out below 2.0 m over
#      the lower eye's floor never count, nor boxes under 2.0 m over their own level (half cover hides no standing pilot); floor plates do.
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
            'maps': [{'id': 'causeway', 'name': 'Starfall Causeway', 'tag': 'A bridge older than the stars it crosses. Mind the gaps.', 'theme': 'causeway',
                      'music': 'causeway-theme', 'ambience': 'causeway-void', 'look': 'causeway-starfall', 'sky': 0x0A0C1E, 'floor': 0xC9C2B4, 'accent': 0x7FD8FF,
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
    col = {'#': (170, 160, 150), 'A': (120, 200, 255), 'G': (180, 170, 160), 'T': (200, 190, 175), 'P': (110, 140, 190), 'S': (230, 220, 200), 'f': (150, 140, 130),
           'c': (120, 220, 255), '-': (220, 200, 160), '/': (200, 200, 205), 'K': (90, 90, 100), 'k': (210, 200, 180)}
    for k, (h, name) in enumerate(LEVELS):
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; fill(k, b[0], b[1], b[4], b[5], (150, 146, 140))
        for x0, x1, z0, z1, y0, y1, nm in footprint(HAZ):
            if y0 - .05 <= h + .1 <= y1: fill(k, x0, x1, z0, z1, (255, 120, 40) if 'star' in nm else (120, 220, 255) if 'comet' in nm else (255, 230, 120), .45)
        for e in sorted(els, key=lambda e: e['box'][3]):
            if in_slice(e, h) and not thin(e): b = e['box']; fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .55 if e['box'][3] - max(h, base_of(e)) < 1.25 else 1.0)
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
    ids = [e['id'] for e in els]; need(len(ids) == len(set(ids)), 'unique ids'); need(all(i.startswith('causeway-') for i in ids), 'id prefix')
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    need(bad == 0, 'spawn sightlines')
    sp = ffa + teamA[1:] + teamB[1:]
    mf = min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)); mab = min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m (3D); min A-B spacing %.1f m' % (mf, mab)); need(mf >= 9, 'FFA spacing'); need(mab >= 24, 'A-B spacing')
    print('FFA floors: ' + ', '.join('%s %.1f' % (n, p[2]) for n, p in named))
    # hazard clearance, per floor: a spawn keeps 3 m (plan) from any live volume that spans its body's height
    worst = min((dist_rect(p, r[:4]), r[6], p) for p in sp for r in footprint(HAZ) if r[4] - .1 <= p[2] + 1.8 and r[5] >= p[2])
    print('closest spawn to a hazard volume on its floor: %.1f m (%s, spawn %s)' % worst); need(worst[0] >= 3, 'hazard clearance')
    inb = all(BOUNDS['minX'] + 4 <= x <= BOUNDS['maxX'] - 4 and BOUNDS['minZ'] + 4 <= z <= BOUNDS['maxZ'] - 4 for x, z, y in sp)
    print('all spawns inside the bounds minus 4 m:', inb); need(inb, 'bounds')
    past = [e['id'] for e in els if e['box'][0] < BOUNDS['minX'] + 4 - 1e-6 or e['box'][1] > BOUNDS['maxX'] - 4 + 1e-6 or e['box'][4] < BOUNDS['minZ'] + 4 - 1e-6 or e['box'][5] > BOUNDS['maxZ'] - 4 + 1e-6]
    # The lowest floor a pilot can stand on keeps its body (its middle .9 m up) clear of killY by 2 m; the walkways' thick undersides
    # may hang past it (nothing stands on an underside).
    low_top = min(e['box'][3] for e in els if e['plate'])
    print('blocks within 4 m of the bounds (the void must run 4 m past every edge): %s; lowest floor %.1f, killY %d' % (past or 'none', low_top, BOUNDS['killY']))
    need(not past and low_top + .9 - 2 > BOUNDS['killY'], 'open edges')
    band = [e['id'] for e in els if e['ch'] not in '-/' and not e['plate'] and 1.3 < e['box'][3] - base_of(e) < 2.2]
    print('blocks topping out 1.3..2.2 m over their level:', band or 'none'); need(not band, 'cover band')
    pil_ids = {p['id'] for p in PILOT}; miss = [e['id'] for e in els if needs_pilot(e) and e['id'] + '-top' not in pil_ids]
    print('pilot blocks: %d (rails %d); rails or tops without one: %s' % (len(PILOT), sum(e['ch'] == '-' for e in els), miss or 'none')); need(not miss, 'pilot blocks')
    flights = {}
    for e in els:
        if e['ch'] == '/': flights.setdefault(e['id'].rsplit('-', 1)[0], []).append(e['box'])
    def flight_bad(bs):
        tops = sorted(round(b[3], 3) for b in bs); steps = [b - a for a, b in zip(tops, tops[1:])]
        return max(steps) > .2 + 1e-6 or min(max(b[1] - b[0], b[5] - b[4]) for b in bs) < 1.6 - 1e-6 or tops[-1] not in (L0, LC)
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
        if e['plate'] and e['ch'] in '.cd' and e['box'][3] - e['box'][2] < 8:
            b = e['box']; (i0, j0), (i1, j1) = cell_of(b[0], b[4]), cell_of(b[1], b[5])
            if not any((i, j, round(b[3], 3)) in seen for i in range(i0, i1 + 1) for j in range(j0, j1 + 1)): unreached.append(e['id'])
    print('every spawn and walkable plate reachable on foot from A1 (the keystone is a jump):', unreached or 'yes'); need(not unreached, 'reachability')
    # hazard timing: nothing lethal in the first 4 s; telegraphs at least today's (3 s for a drop from above, 0.8 s for a vent, the
    # maglev's warning before its danger)
    for h in HAZ:
        if h['kind'] == 'press':
            open_ = h['period'] - h['warn'] - h['rise'] - h['dwell'] - h['travel']; first = 4 + h['start'] + h['offset'] + open_
            need(open_ > 0 and h['warn'] >= 3 and first >= 4, 'star timing ' + h['id'])
        elif h['kind'] == 'vent':
            first = 4 + h['start'] + h['offset'] + h['period'] - h['warn'] - h['dwell']; need(h['warn'] >= .8 and first >= 4 and h['push'] > 0 and h['damage'] > 0, 'wind timing ' + h['id'])
        else:
            first = h['first'] - h['warn']; need(first >= 4 and (h['size']['x'] + h['length']) / h['speed'] + 2 + h['warn'] < h['period'], 'comet timing')
        print('  %-18s %-6s first telegraph at FightAt + %5.2f s, warn %.1f s, period %.0f s' % (h['id'], h['kind'], first, h['warn'], h['period']))
    print('blocks: %d; hazards: %d; pilot blocks: %d; spawns: %d FFA + 2 x %d team' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA))); need(len(els) <= 350, 'block budget')
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'causeway_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'causeway_m1.png')); print('wrote', os.path.join(out, 'causeway_m1.json'), 'and causeway_m1.png')
    print('FAILED: ' + ', '.join(fails) if fails else 'all checks pass')
    sys.exit(1 if fails else 0)
