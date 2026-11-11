# Pier 9 M2 (`harbor`, rebuilt 2026-11-11: a seaside amusement pier at night. Two piers, south and north, across a channel of open
# sea that the rollercoaster's track runs along on its own trestle; three bridges cross it and a catwalk passes under it; the Ferris
# wheel stands off the west side, the two-storey carousel and the band stand face each other across the channel, a drop tower
# stands on each pier, and a patch of missing boards on each opens straight down to the water) draft and checks. The M1 yard (harbor_m1.py) stays as history.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z). Team A south (the Gate), team B north.
# usage: python3 docs/arenas/scripts/harbor_m2.py [--out DIR] [--json PATH]   (stdout: plans + checks, exit 1 on any failed check;
#        --json writes the draft only; --out writes harbor_m2.json, draft map data in the UnityMaps.json shape, and harbor_m2.png,
#        three top-down plans (levels -2.6, 0, 4.2), 8 px per metre)
# Design: docs/arenas/HARBOR.md. Ids carry the prefixes the presentation keys its looks, kit and art on (harbor-deck-*, -hall-*,
# -kiosk-*, -crown-*, -turret-*, -sign-*, -stall-*, -booth-*, -ride-*, -tower-*, -planter-*, -crate-*, -cart-*, -signal-*, -rail-*,
# -stair-*, -bridge-*, -track, -landing-*, -catwalk-*, -caisson, -pile-*). Everything is static (timber, iron, glass).
import math, json, itertools, sys, os, zlib, struct
LW, L0, LC = -2.6, 0.0, 4.2                    # the catwalk under the boards, the boardwalk, the pavilions' roof terraces
X0, X1, Z0, Z1 = -20, 20, -34, 34              # the pier's extent
# The sim clamps bots, abilities and teleports to the bounds minus the capsule (NativeUnityMaps.Clamp), not walking pilots: the bounds
# run 4 m past every open edge, so a pilot who walks, jumps short or is knocked off falls into the sea and dies at killY. killY -5.5:
# 2 m under the lowest floor (the catwalk, -2.6) plus the body's middle (.9); a fall from the boardwalk dies about .75 s after it
# leaves the boards, just under the water's surface (-4.6).
BOUNDS = {'minX': -24, 'maxX': 24, 'minZ': -38, 'maxZ': 38, 'ceiling': 22, 'killY': -5.5}
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, plate (a walkable floor: decks, plazas, walkways, crowns)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='wood', role='static', hp=0, mirror=True, plate=False):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp, plate=plate))
def stair(i, xa, xb, z0, z1, ya, yb, steps, solid=False, axis='x', mat='wood'):
    # a flight from (xa, ya) to (xb, yb) along x (z0..z1 wide) or along z (xa/xb are z, z0/z1 x). Solid flights stand on the floor at
    # min(ya, yb); others are 20 cm slabs (hung over the void)
    run = (xb - xa) / steps; rise = (yb - ya) / steps; base = min(ya, yb)
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1))); y1 = ya + rise * (k + 1); y0 = base if solid else y1 - .2
        if axis == 'x': add('%s-%02d' % (i, k), a, b, y0, y1, z0, z1, '/', mat=mat)
        else: add('%s-%02d' % (i, k), z0, z1, y0, y1, a, b, '/', mat=mat)
def plates(i, x0, x1, z0, z1, holes, y0, y1, ch='d', mat='wood'):
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

# ---- the Gate (pavilion A, the pier's south end, x +-10, z -34..-25): the hall (walls to 3.8 under the roof terrace at 4.2), the
#      front's great arch (x +-2.2, head 3.2) and a side door each side (x 5..7, head 2.6), the kiosk behind the arch (floor to roof,
#      so nothing on the midway sees into the hall), the stair up the west wall, the terrace with its twin turrets and the sign
add('harbor-hall-a-floor', -10, 10, -1.0, 0, -34, -25, '.', mat='foundation', plate=True)
add('harbor-hall-a-wall-s', -10, 10, 0, 3.8, -34, -33.5, '#')
add('harbor-hall-a-wall-w', -10, -9.5, 0, 3.8, -33.5, -25, '#')
add('harbor-hall-a-wall-e', 9.5, 10, 0, 3.8, -33.5, -25, '#')
for k, (a, b) in enumerate(((-9.5, -7.0), (-5.0, -2.2), (2.2, 5.0), (7.0, 9.5))): add('harbor-hall-a-front-%d' % k, a, b, 0, 3.8, -25.5, -25, '#')
add('harbor-hall-a-arch', -2.2, 2.2, 3.2, 3.8, -25.5, -25, '#')
add('harbor-hall-a-lintel-0', -7.0, -5.0, 2.6, 3.8, -25.5, -25, '#'); add('harbor-hall-a-lintel-1', 5.0, 7.0, 2.6, 3.8, -25.5, -25, '#')
add('harbor-kiosk-a', -3.4, 3.4, 0, 3.8, -29.2, -28.2, 'A')
# the hall's screens: a short wall inside each side door, so a door shows the stair or the wall, never the room
add('harbor-hall-a-screen-0', -7.4, -4.6, 0, 3.8, -28.4, -28.0, '#'); add('harbor-hall-a-screen-1', 4.6, 7.4, 0, 3.8, -28.4, -28.0, '#')
stair('harbor-stair-hall-a', -26.6, -32.9, -9.5, -7.7, 0, LC, 21, solid=True, axis='z')
HOLE = (-9.5, -7.1, -32.9, -26.0)               # 0.6 m wider than the flight: headroom for the capsule on its upper steps
plates('harbor-crown-a', -10, 10, -34, -25, [HOLE], LC - .4, LC, ch='c')
rail_x('harbor-rail-crown-a-s', -10, 10, LC, -34, -33.8)
rail_z('harbor-rail-crown-a-w', -10, -9.8, LC, -33.8, -25); rail_z('harbor-rail-crown-a-e', 9.8, 10, LC, -33.8, -25)
rail_x('harbor-rail-crown-a-n-0', -9.8, -7.2, LC, -25.2, -25); rail_x('harbor-rail-crown-a-n-1', 7.2, 9.8, LC, -25.2, -25)
add('harbor-turret-a-w', -5.2, -3.2, LC, LC + 4.4, -27, -25, 'T'); add('harbor-turret-a-e', 3.2, 5.2, LC, LC + 4.4, -27, -25, 'T')
add('harbor-sign-a', -3.2, 3.2, LC, LC + 2.6, -25.6, -25, 'G')
# the second way up, outside the east wall: prize crates stepped 1.1, 2.3, 3.4 (a jump each) to the terrace
add('harbor-crate-a-0', 10, 11.6, 0, 1.1, -28.4, -26.8, 'f'); add('harbor-crate-a-1', 10, 11.6, 0, 2.3, -30.0, -28.4, 'F'); add('harbor-crate-a-2', 10, 11.6, 0, 3.4, -31.6, -30.0, 'F')
# ---- the south pier's boardwalk (z -34..-5) round the hall, with the stair pit down to the catwalk (x +-1.5, z -17.4..-13.5)
PIT = (-1.5, 1.5, -17.4, -13.5)
GAP = (-7.2, -4.8, -20.2, -17.8)                # the missing boards: a hole straight down to the sea, no rail (a lantern marks it)
plates('harbor-deck-s', -20, 20, -34, -5, [(-10, 10, -34, -25), PIT, GAP], -.4, 0, ch='.', mat='wood')
# ---- the midway: the ticket booth on the centre line, the stalls (a back wall, two cheeks, a counter with a way in, a roof you
#      can stand on), planters with lamp posts (half cover), prize crates, a cart
add('harbor-booth-s', -1.6, 1.6, 0, 2.8, -22.6, -20.4, 'B')
def stall(i, x0, x1, z0, z1, face):
    # the open front faces `face` ('+x', '-x', '-z'); 3 m deep: the back .6, the nook 1.9 behind a .5 counter (1.1 high, a 1.6 m way in at one end)
    if face[1] == 'x':
        f = face[0] == '+'; xb0, xb1 = (x0, x0 + .6) if f else (x1 - .6, x1); xn0, xn1 = (x0 + .6, x1) if f else (x0, x1 - .6); xc0, xc1 = (x1 - .5, x1) if f else (x0, x0 + .5)
        add(i + '-back', xb0, xb1, 0, 2.8, z0, z1, '#', mat='wood')
        add(i + '-cheek-0', xn0, xn1, 0, 2.8, z0, z0 + .2, '#', mat='wood'); add(i + '-cheek-1', xn0, xn1, 0, 2.8, z1 - .2, z1, '#', mat='wood')
        add(i + '-counter', xc0, xc1, 0, 1.1, z0 + 1.8, z1 - .2, 'f', mat='wood')
    else:
        add(i + '-back', x0, x1, 0, 2.8, z1 - .6, z1, '#', mat='wood')
        add(i + '-cheek-0', x0, x0 + .2, 0, 2.8, z0, z1 - .6, '#', mat='wood'); add(i + '-cheek-1', x1 - .2, x1, 0, 2.8, z0, z1 - .6, '#', mat='wood')
        add(i + '-counter', x0 + 1.8, x1 - .2, 0, 1.1, z0, z0 + .5, 'f', mat='wood')
    add(i + '-roof', x0, x1, 2.8, 3.0, z0, z1, 'r', mat='wood', plate=True)
stall('harbor-stall-s-0', -17, -13, -28, -25, '-z')
stall('harbor-stall-s-1', 17, 20, -25, -21, '-x')
stall('harbor-stall-s-2', 13, 17, -8, -5, '-z')
for k, (a, b, c, d) in enumerate(((-6.4, -4.0, -23.6, -22.7), (4.0, 6.4, -19.2, -18.3), (-16.4, -15.5, -8.8, -6.4), (4.6, 5.5, -10.4, -8.0), (13.4, 15.8, -20.2, -19.3))):
    add('harbor-planter-s-%d' % k, a, b, 0, 1.1, c, d, 'f', mat='wood')
add('harbor-crate-s-0', -4.6, -3.0, 0, 1.1, -11.6, -10.0, 'f'); add('harbor-crate-s-1', -4.6, -3.0, 0, 2.4, -10.0, -8.4, 'F')
add('harbor-crate-s-2', 14.2, 15.8, 0, 2.4, -12.6, -11.0, 'F'); add('harbor-crate-s-3', -13.0, -11.4, 0, 1.1, -23.4, -21.8, 'f')
add('harbor-cart-s', -18.6, -16.4, 0, 1.1, -13.2, -11.8, 'f', mat='wood')
# ---- the carousel (south pier, west): a low stepped round base (a .2 m step up), the mirrored drum at its heart (full cover), the
#      upper deck where the mounts ride (overhead: the play space is the ring under it); its twin by the mirror is the band stand
def ride(i, cx, cz):
    for k, (a, b, c, d) in enumerate(((-1.9, 1.9, -4.6, 4.6), (1.9, 3.25, -3.25, 3.25), (3.25, 4.6, -1.9, 1.9), (-3.25, -1.9, -3.25, 3.25), (-4.6, -3.25, -1.9, 1.9))):
        add('%s-base-%d' % (i, k), cx + a, cx + b, 0, .2, cz + c, cz + d, '.', mat='wood', plate=True)
    add(i + '-drum', cx - 1.3, cx + 1.3, .2, 2.8, cz - 1.3, cz + 1.3, 'D')
    for k, (a, b, c, d) in enumerate(((-1.9, 1.9, -4.4, 4.4), (1.9, 4.0, -3.2, 3.2), (-4.0, -1.9, -3.2, 3.2))):
        add('%s-deck-%d' % (i, k), cx + a, cx + b, 2.8, 3.0, cz + c, cz + d, 'R', mat='wood', plate=True)
ride('harbor-ride-w', -10, -15)
# ---- the drop tower (south pier, east): the mast, floor to sky; the gondola's square is the hazard
TOWER = (10.0, -15.0)
add('harbor-tower-s', TOWER[0] - .7, TOWER[0] + .7, 0, 20, TOWER[1] - .7, TOWER[1] + .7, 'T', mat='metal')
# ---- the channel (z -5..5): the coaster's trestle (the track bed, x -20..20, z +-1.6), the centre bridge and two footbridges
add('harbor-track', -20, 20, -.4, 0, -1.6, 1.6, '.', mat='wood', mirror=False, plate=True)
add('harbor-bridge-c-s', -2.5, 2.5, -.4, 0, -5, -1.6, '.', mat='wood', plate=True)
add('harbor-bridge-w-s', -12, -8, -.4, 0, -5, -1.6, '.', mat='wood', plate=True)
add('harbor-bridge-e-s', 8, 12, -.4, 0, -5, -1.6, '.', mat='wood', plate=True)
for nm, xs in (('w-s', (-12.0, -8.2)), ('e-s', (8.0, 11.8))):
    for k, x in enumerate(xs): rail_z('harbor-rail-bridge-%s-%d' % (nm, k), x, x + .2, L0, -5, -1.9)
rail_z('harbor-rail-bridge-c-s-0', -2.5, -2.3, L0, -5, -3.0); rail_z('harbor-rail-bridge-c-s-1', 2.3, 2.5, L0, -5, -3.0)
# the crossing's signals (posts, 3.4 m) at each bridge's foot
for k, x in enumerate((-7.6, 2.9, 7.3)): add('harbor-signal-s-%d' % k, x, x + .4, 0, 3.4, -5.9, -5.5, 'S', mat='metal')
# ---- rails (1.1 m): the outer edges and the channel's edge, with the gaps where the falls are
for k, (a, b) in enumerate(((-34, -30), (-28, -18.2), (-15, -11), (-9, -5))): rail_z('harbor-rail-edge-s-w-%d' % k, -20, -19.8, L0, a, b)
for k, (a, b) in enumerate(((-34, -25), (-21, -17), (-13.4, -10.6))): rail_z('harbor-rail-edge-s-e-%d' % k, 19.8, 20, L0, a, b)
for k, (a, b) in enumerate(((-19.8, -14.5), (-12.5, -10), (10, 16), (18, 19.8))): rail_x('harbor-rail-end-s-%d' % k, a, b, L0, -34, -33.8)
for k, (a, b) in enumerate(((-19.8, -16.6), (-13.4, -12), (-6, -2.5), (2.5, 6.4), (12, 13), (17, 18.4))): rail_x('harbor-rail-channel-s-%d' % k, a, b, L0, -5.2, -5)
rail_z('harbor-rail-pit-s-0', -1.7, -1.5, L0, -17.4, -13.3); rail_z('harbor-rail-pit-s-1', 1.5, 1.7, L0, -17.4, -13.3); rail_x('harbor-rail-pit-s-2', -1.5, 1.5, L0, -13.5, -13.3)
# ---- under the boards (y -2.6): the stair down the pit (13 risers, slabs), the landing under the deck, the catwalk out under the
#      centre bridge and round the caisson that carries the track (the train passes overhead), piles (full cover)
stair('harbor-stair-low-s', -13.5, -17.4, -1.2, 1.2, LW, L0, 13, axis='z', mat='wood')
add('harbor-landing-s', -3, 3, LW - .3, LW, -13.5, -5, '.', mat='wood', plate=True)
add('harbor-catwalk-s', -3.5, 3.5, LW - .3, LW, -5, -2, '.', mat='wood', plate=True)
add('harbor-catwalk-w', -3.5, -2, LW - .3, LW, -2, 2, '.', mat='wood', plate=True)
add('harbor-caisson', -2, 2, -7, -.4, -2, 2, 'K', mat='foundation', mirror=False)
rail_z('harbor-rail-landing-s-0', -3, -2.8, LW, -13.5, -8.4); rail_z('harbor-rail-landing-s-1', 2.8, 3, LW, -11.0, -5)
rail_z('harbor-rail-catwalk-w', -3.5, -3.3, LW, -2, 2)
add('harbor-pile-s-0', 1.5, 2.2, -7, -.4, -8.2, -7.5, 'P'); add('harbor-pile-s-1', -2.3, -1.6, -7, -.4, -11.6, -10.9, 'P')

# ---- hazards (UnityMaps.json `hazards[]`; existing kinds). Factory kinds time from the arm time (FightAt + 4 s); the maglev from FightAt.
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), radius=0, first=0, start=0, period=0,
             offset=0, warn=0, danger=0, speed=0, length=0, charge=0, live=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
# The Comet Coaster's crossing (kind `maglev`): the train sweeps the trestle's lane (x -20..20, z +-1.6, 2.6 m tall), out from under
# the Ferris wheel and back again (a shuttle coaster: the direction alternates each pass and round). 7 s warning (the signals' bells
# and wig-wag lamps, the lane's lamps chasing toward the train, the train let go on its hill), 3.5 s danger, the pass (a 13 m train
# at 30 m/s: 1.77 s), 2 s clearing; every 34 s, the first at FightAt + 22.
COASTER = hz(id='harbor-coaster', kind='maglev', center=v(0, 1.3, 0), size=v(40, 2.6, 3.2), axis=v(1, 0, 0), first=22, period=34, warn=7,
             danger=3.5, speed=30, length=13, lethal=True)
# The drop towers (kind `press`): the gondola, a 4.6 m square ring round the mast, falls from 18 m to the boards. Each cycle: up
# top, a 3.4 s telegraph (the siren, the mast's lamps running down, the square on the boards filling), a .7 s fall, .6 s held on
# the boards (lethal), a 2.4 s climb (lethal while its underside is under 1.8 m: the first half second). The press kind's lethal
# band is absolute, so each tower stands on a y-0 floor. Every 24 s each, the north tower 12 s after the south.
DROP = 4.6; DROP_H = 2.4; DROP_TOP = 18.0
HAZ = [COASTER]
for nm, (x, z), off in (('s', TOWER, 0), ('n', (-TOWER[0], -TOWER[1]), 12)):
    HAZ.append(hz(id='harbor-drop-' + nm, kind='press', a=v(x, DROP_TOP, z), b=v(x, L0 + DROP_H / 2, z), size=v(DROP, DROP_H, DROP), start=0, offset=off,
                  period=24, warn=3.4, rise=.7, dwell=.6, travel=2.4, lethal=True))

# ---- spawns (x, z, floor); yaw from +z toward +x, facing the origin
teamA = [(-1.4, -31.2, L0), (1.4, -31.2, L0), (-1.4, -32.6, L0), (1.4, -32.6, L0)]
teamB = [(-x, -z, y) for x, z, y in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
_f, _g, _h = (0.0, -9.6, LW), (15.0, -6.5, L0), (-15.0, -26.5, L0)
ffa = [teamA[0], teamB[0], _f, (-_f[0], -_f[1], _f[2]), _g, (-_g[0], -_g[1], _g[2]), _h, (-_h[0], -_h[1], _h[2])]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only exclusion columns (infinite upward): over every rail and unstandable top; the integration (tools/
#      generate_stage_hazards.py open_stage) keeps those under art that hangs over a top (the rides' upper decks under the turning
#      mounts and the band stand's dome, the turrets under their domes, the masts) and, for bots only, those at a lethal drop
def pilot_for(e):
    x0, x1, y0, y1, z0, z1 = e['box']; return dict(id=e['id'] + '-top', center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .1, 3))
def open_sky(e):
    b = e['box']; return not any(o['plate'] and o['box'][2] >= b[3] and o['box'][0] < b[1] and o['box'][1] > b[0] and o['box'][4] < b[5] and o['box'][5] > b[4] for o in els)
def needs_pilot(e): return e['ch'] in '-TR'
PILOT = [pilot_for(e) for e in els if needs_pilot(e)]

# ---- level bookkeeping
LEVELS = [(LW, 'level -2.6 (under the boards)'), (L0, 'level 0 (the boardwalk)'), (LC, 'level 4.2 (roof terraces)')]
def base_of(e):
    y0 = e['box'][2]
    return LC if y0 >= LC - .45 else L0 if y0 >= -.65 else LW if y0 >= LW - .45 else y0
def floor_at(e, h): return abs(e['box'][3] - h) < .25 and (e['plate'] or e['ch'] == '/') and e['ch'] not in 'rR'
def in_slice(e, h): y0, y1 = e['box'][2], e['box'][3]; return y0 < h + 2.0 and y1 > h + .25
def thin(e): return e['plate']
def footprint(hd):
    """Ground footprints of every hazard's live volume: (x0, x1, z0, z1, y0, y1, name)."""
    out = []
    for h in hd:
        if h['kind'] == 'press': c, s = h['b'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, c['y'] - s['y'] / 2, h['a']['y'] + s['y'] / 2, h['id']))
        elif h['kind'] == 'maglev': c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, c['y'] - s['y'] / 2, c['y'] + s['y'] / 2, h['id']))
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
            if y0 - .05 <= h + .1 <= y1: rect(x0, x1, z0, z1, 'Z' if 'drop' in nm else '~')
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
            'maps': [{'id': 'harbor', 'name': 'Pier 9', 'tag': 'Pier 9, the last night of the season. Mind the gap; mind the train.', 'theme': 'harbor',
                      'music': 'harbor-theme', 'ambience': 'harbor-pier', 'look': 'harbor-pier-lights', 'sky': 0x0B1030, 'floor': 0x6B4A36, 'accent': 0xFFB347,
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
    col = {'#': (170, 120, 90), 'A': (120, 200, 255), 'G': (255, 200, 90), 'T': (200, 60, 70), 'P': (90, 80, 70), 'S': (230, 220, 100), 'f': (150, 140, 130), 'F': (190, 160, 110), 'B': (90, 200, 180), 'D': (220, 190, 255), 'R': (255, 150, 200), 'r': (120, 90, 150),
           'c': (120, 220, 255), '-': (220, 200, 160), '/': (200, 200, 205), 'K': (90, 90, 100), 'k': (210, 200, 180)}
    for k, (h, name) in enumerate(LEVELS):
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; fill(k, b[0], b[1], b[4], b[5], (150, 146, 140))
        for x0, x1, z0, z1, y0, y1, nm in footprint(HAZ):
            if y0 - .05 <= h + .1 <= y1: fill(k, x0, x1, z0, z1, (255, 120, 40) if 'drop' in nm else (120, 220, 255), .45)
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
    ids = [e['id'] for e in els]; need(len(ids) == len(set(ids)), 'unique ids'); need(all(i.startswith('harbor-') for i in ids), 'id prefix')
    # point symmetry of the collision
    boxes = {tuple(round(q, 3) for q in e['box']) for e in els}
    need(all((round(-b[1], 3), round(-b[0], 3), b[2], b[3], round(-b[5], 3), round(-b[4], 3)) in boxes for b in boxes), 'point symmetry')
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
    # The lowest floor a pilot can stand on keeps its body (its middle .9 m up) clear of killY by 2 m.
    low_top = min(e['box'][3] for e in els if e['plate'])
    print('blocks within 4 m of the bounds (the sea must run 4 m past every edge): %s; lowest floor %.1f, killY %.1f' % (past or 'none', low_top, BOUNDS['killY']))
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
        if e['plate'] and e['ch'] in '.cd' and min(e['box'][1] - e['box'][0], e['box'][5] - e['box'][4]) >= 1:
            b = e['box']; (i0, j0), (i1, j1) = cell_of(b[0], b[4]), cell_of(b[1], b[5])
            if not any((i, j, round(b[3], 3)) in seen for i in range(i0, i1 + 1) for j in range(j0, j1 + 1)): unreached.append(e['id'])
    print('every spawn and walkable plate reachable on foot from A1 (the stalls\' roofs and the rides\' decks are jumps):', unreached or 'yes'); need(not unreached, 'reachability')
    # hazard timing: nothing lethal in the first 4 s; telegraphs at least today's (3 s for a drop from above, the maglev's warning
    # before its danger)
    for h in HAZ:
        if h['kind'] == 'press':
            open_ = h['period'] - h['warn'] - h['rise'] - h['dwell'] - h['travel']; first = 4 + h['start'] + h['offset'] + open_
            need(open_ > 0 and h['warn'] >= 3 and first >= 4 and abs(h['b']['y'] - h['size']['y'] / 2) < 1e-6, 'drop timing ' + h['id'])
        else:
            first = h['first'] - h['warn']; need(first >= 4 and (h['size']['x'] + h['length']) / h['speed'] + 2 + h['warn'] < h['period'], 'coaster timing')
        print('  %-18s %-6s first telegraph at FightAt + %5.2f s, warn %.1f s, period %.0f s' % (h['id'], h['kind'], first, h['warn'], h['period']))
    print('blocks: %d; hazards: %d; pilot blocks: %d; spawns: %d FFA + 2 x %d team' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA))); need(len(els) <= 350, 'block budget')
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'harbor_m2.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'harbor_m2.png')); print('wrote', os.path.join(out, 'harbor_m2.json'), 'and harbor_m2.png')
    print('FAILED: ' + ', '.join(fails) if fails else 'all checks pass')
    sys.exit(1 if fails else 0)
