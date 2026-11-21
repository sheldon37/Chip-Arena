# Deepwell M2 (rig: Deepwell Station, a research station on the ocean floor: the Well, a domed rotunda round the drill shaft with a
# gallery deck; two two-storey habitat wings on a glazed double-deck spine; two sunken moon-pool bays; four glass ring tunnels) draft
# and checks. The 2026-11-11 rebuild of the stage (docs/arenas/DEEPWELL.md); rig_m1.py (the offshore platform) stays as history.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z). Team A west (wing A), team B east.
# usage: python3 docs/arenas/scripts/rig_m2.py [--out DIR] [--json PATH]   (stdout: plans + checks, exit 1 on any failed check;
#        --json writes the draft only; --out writes rig_m2.json, draft map data in the UnityMaps.json shape, and rig_m2.png,
#        three top-down plans (levels -1.6, 0, 3.2), 8 px per metre)
# Ids carry the prefixes the presentation keys its looks, kit and art on (rig-well-*, rig-wing-*, rig-spine-*, rig-bay-*,
# rig-tunnel-*, rig-wall-*, rig-glass-*, rig-gallery-*, rig-catwalk-*, rig-stair-*, rig-rail-*, rig-pylon-*, rig-console-*,
# rig-screen-*, rig-locker-*, rig-chamber-*, rig-bulkhead-*, rig-sub-*, rig-case-*). Steel, brass and thick glass are static; the supply
# cases are breakable cover.
import math, json, itertools, sys, os, zlib, struct
LP, L0, LG = -1.6, 0.0, 3.2                    # the moon-pool bays, the main deck, the gallery deck
H2, HR, HT = 6.4, 7.2, 3.2                     # wall tops: wings, spine and bays; the Well; the ring tunnels
T = .4                                         # wall and glass thickness
X0, X1, Z0, Z1 = -28.4, 28.4, -21.4, 21.4      # the structure's extent
# The station is closed on every side (steel and glass are real blocks, capped by pilot columns where a double jump could top them),
# so the only falls are the two moon pools: open water in the bays' floors, no rail. killY -4.4: a body's middle below it dies,
# about half a second after it steps off the pool's edge (the lowest floor, the bays at -1.6, is 2.8 m clear of it).
BOUNDS = {'minX': -29.4, 'maxX': 29.4, 'minZ': -22.4, 'maxZ': 22.4, 'ceiling': 14, 'killY': -4.4}
POOL = (-4.0, 4.0, 13.6, 18.8)                 # the north bay's moon pool (x0, x1, z0, z1); the south bay's is its mirror
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, plate (a walkable floor)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='metal', role='static', hp=0, mirror=True, plate=False):
    x0, x1 = sorted((x0, x1)); z0, z1 = sorted((z0, z1))
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp, plate=plate))
def stair(i, xa, xb, z0, z1, ya, yb, steps, axis='x', base=None):
    # a solid flight from (xa, ya) to (xb, yb) along x (z0..z1 wide) or along z (xa/xb are z, z0/z1 x), standing on `base`
    run = (xb - xa) / steps; rise = (yb - ya) / steps; base = min(ya, yb) if base is None else base
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1))); y1 = round(ya + rise * (k + 1), 4)
        if axis == 'x': add('%s-%02d' % (i, k), a, b, base, y1, z0, z1, '/')
        else: add('%s-%02d' % (i, k), z0, z1, base, y1, a, b, '/')
def plates(i, x0, x1, z0, z1, holes, y0, y1, ch='d', mat='metal', mirror=True):
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
    for k, (a, b, za, zb) in enumerate(min(px, pz, key=len)): add('%s-%d' % (i, k), a, b, y0, y1, za, zb, ch, mat=mat, plate=True, mirror=mirror)
def case(i, x, z, y=L0, s=1.2, **k): add(i, x - s / 2, x + s / 2, y, y + s, z - s / 2, z + s / 2, 'x', role='cover', hp=150, **k)   # a supply case

# ---- wing A (the west habitat; interior x -28..-18, z -9..9; two storeys): the dormitory deck (0) under the observation lounge
#      (3.2), the panoramic window west, a pressure door to the spine on each storey (east), a door to a ring tunnel north and south
WX0, WX1, WZ = -28.0, -18.0, 9.0
add('rig-wing-a-floor', WX0 - T, WX1 + T, -1.0, 0, -WZ - T, WZ + T, '.', plate=True)
add('rig-glass-wing-a-w', WX0 - T, WX0, 0, H2, -WZ - T, WZ + T, 'g')
for side, sz in (('n', 1), ('s', -1)):
    za, zb = sz * WZ, sz * (WZ + T)
    add('rig-wall-wing-a-%s-0' % side, WX0, -24.0, 0, H2, za, zb, '#'); add('rig-wall-wing-a-%s-1' % side, -21.0, WX1 + T, 0, H2, za, zb, '#')
    add('rig-wall-wing-a-%s-head' % side, -24.0, -21.0, 2.6, H2, za, zb, '#')
add('rig-wall-wing-a-e-0', WX1, WX1 + T, 0, H2, -WZ, -1.5, '#'); add('rig-wall-wing-a-e-1', WX1, WX1 + T, 0, H2, 1.5, WZ, '#')
add('rig-wall-wing-a-e-sill', WX1, WX1 + T, 2.6, LG, -1.5, 1.5, '#', plate=True)       # the lower door's head is the upper door's sill
add('rig-wall-wing-a-e-head', WX1, WX1 + T, 5.8, H2, -1.5, 1.5, '#')
# the stair to the lounge along the east wall's north half (16 risers, rising north), under an open well in the lounge's floor
stair('rig-stair-wing-a', 3.0, 7.8, -19.8, WX1, 0, LG, 16, axis='z')
plates('rig-wing-a-upper', WX0, WX1, -WZ, WZ, [(-20.4, WX1, 2.4, 7.8)], LG - .4, LG)
add('rig-rail-wing-a-well', -20.55, -20.4, LG, LG + 1.1, 2.4, 7.8, '-'); add('rig-rail-wing-a-well-end', -20.4, WX1, LG, LG + 1.1, 2.4, 2.55, '-')
# cover: the bulkhead screen facing the spine door on each storey (nothing sees through both wings), lockers, the lounge's consoles
add('rig-screen-a', -22.6, -21.8, 0, 2.6, -3.4, 3.4, 'S'); add('rig-screen-a-upper', -22.6, -21.8, LG, LG + 2.6, -3.4, 3.4, 'S')
add('rig-locker-a-n', -27.4, -25.0, 0, 2.6, 6.2, 7.0, 'S'); add('rig-locker-a-s', -27.4, -25.0, 0, 2.6, -7.0, -6.2, 'S')
add('rig-console-a-upper-n', -25.6, -23.2, LG, LG + 1.1, 5.0, 5.8, 'c'); add('rig-console-a-upper-s', -25.6, -23.2, LG, LG + 1.1, -5.8, -5.0, 'c')
case('rig-case-a-0', -20.2, -6.6); case('rig-case-a-1', -24.4, 1.0)

# ---- the spine, west (interior x -17.6..-10.4, z -2.5..2.5): a double-deck glass tunnel between wing A and the Well
SX0, SX1, SZ = -17.6, -10.4, 2.5
add('rig-spine-w-floor', SX0, SX1, -1.0, 0, -SZ - T, SZ + T, '.', plate=True)
add('rig-spine-w-upper', SX0, SX1, LG - .4, LG, -SZ, SZ, 'd', plate=True)
add('rig-glass-spine-w-n', SX0, SX1, 0, H2, SZ, SZ + T, 'g'); add('rig-glass-spine-w-s', SX0, SX1, 0, H2, -SZ - T, -SZ, 'g')
case('rig-case-spine-w', -14.0, 1.7)

# ---- the Well (the rotunda; interior +-10): the drill floor (0) round the wellhead, the gallery deck (3.2) round the open well,
#      steel below the gallery and the dome's glass above it, a pressure door each way on both decks
R, RI = 10.0, 5.8
add('rig-well-floor', -R - T, R + T, -2.4, 0, -R - T, R + T, '.', mirror=False, plate=True)
def well_wall(side, along_x, fixed, sign, door):
    # one side's wall: two steel lengths below the gallery, two glass lengths above, the lower door's head (the upper door's sill)
    # and the upper door's head
    lo, hi = (-R, R) if along_x else (-R - T, R + T)
    for k, (a, b) in enumerate(((lo, -door), (door, hi))):
        for nm, y0, y1, ch, mat in (('wall', 0, LG, '#', 'metal'), ('glass', LG, HR, 'g', 'metal')):
            if along_x: add('rig-%s-well-%s-%d' % (nm, side, k), a, b, y0, y1, fixed, fixed + sign * T, ch, mat=mat, mirror=False)
            else: add('rig-%s-well-%s-%d' % (nm, side, k), fixed, fixed + sign * T, y0, y1, a, b, ch, mat=mat, mirror=False)
    for nm, y0, y1, pl in (('sill', 2.6, LG, True), ('head', 5.8, HR, False)):
        if along_x: add('rig-wall-well-%s-%s' % (side, nm), -door, door, y0, y1, fixed, fixed + sign * T, '#', mirror=False, plate=pl)
        else: add('rig-wall-well-%s-%s' % (side, nm), fixed, fixed + sign * T, y0, y1, -door, door, '#', mirror=False, plate=pl)
well_wall('w', False, -R, -1, 1.5); well_wall('e', False, R, 1, 1.5); well_wall('n', True, R, 1, 2.0); well_wall('s', True, -R, -1, 2.0)
# the gallery ring and its two stairs (16 risers each): up the west wall's north half and the east wall's south half
STAIR_W = (-R, -7.8, 2.0, 7.4)
plates('rig-gallery', -R, R, -R, R, [(-RI, RI, -RI, RI), STAIR_W, (-STAIR_W[1], -STAIR_W[0], -STAIR_W[3], -STAIR_W[2])], LG - .4, LG, mirror=False)
stair('rig-stair-well-w', 2.6, 7.4, -R, -8.2, 0, LG, 16, axis='z')
add('rig-rail-well-stair-w', -7.8, -7.65, LG, LG + 1.1, 2.0, 7.4, '-'); add('rig-rail-well-stair-w-end', -R, -7.8, LG, LG + 1.1, 2.0, 2.15, '-')
# the gallery's inner rail (1.1 m), open at the middle of each side (a drop to the drill floor)
add('rig-rail-gallery-n-w', -RI, -2.0, LG, LG + 1.1, RI - .15, RI, '-'); add('rig-rail-gallery-n-e', 2.0, RI, LG, LG + 1.1, RI - .15, RI, '-')
add('rig-rail-gallery-w-n', -RI, -RI + .15, LG, LG + 1.1, 2.0, RI - .15, '-'); add('rig-rail-gallery-w-s', -RI, -RI + .15, LG, LG + 1.1, -RI + .15, -2.0, '-')
# the four pylons that carry the drill's gantry (full cover on both decks) and the consoles under the gallery (half cover)
add('rig-pylon-n-w', -4.9, -3.5, 0, HR, 3.5, 4.9, 'P'); add('rig-pylon-s-w', -4.9, -3.5, 0, HR, -4.9, -3.5, 'P')
add('rig-console-well-n', 3.6, 6.4, 0, 1.1, 8.4, 9.2, 'c'); add('rig-console-well-w', -9.2, -8.4, 0, 1.1, -7.0, -4.2, 'c')
add('rig-console-gallery-n', -6.0, -3.6, LG, LG + 1.1, 8.6, 9.4, 'c')
case('rig-case-well-e', 7.4, 3.4); case('rig-case-well-s', -3.2, -8.2)

# ---- the north moon-pool bay (interior x -8..8, z 10.4..21; floor -1.6): steps down from the Well's north door, the pool open to the
#      sea in the floor, the great window north, a ring tunnel's mouth west and east, the catwalk from the gallery out over the pool
BX, BZ0, BZ1 = 8.0, R + T, 21.0
plates('rig-bay-n-floor', -BX - T, BX + T, BZ0, BZ1 + T, [POOL], LP - .8, LP, ch='.')
stair('rig-stair-bay-n', 12.8, BZ0, -2.0, 2.0, LP, L0, 8, axis='z')
add('rig-glass-bay-n', -BX - T, BX + T, LP, H2, BZ1, BZ1 + T, 'g')
MZ0, MZ1 = 13.6, 17.6                          # a ring tunnel's mouth in the bay's side wall
for side, sx in (('w', -1), ('e', 1)):
    xa, xb = sx * BX, sx * (BX + T)
    add('rig-wall-bay-n-%s-0' % side, xa, xb, LP, H2, BZ0, MZ0, '#'); add('rig-wall-bay-n-%s-1' % side, xa, xb, LP, H2, MZ1, BZ1, '#')
    add('rig-wall-bay-n-%s-head' % side, xa, xb, 1.4, H2, MZ0, MZ1, '#')
add('rig-catwalk-n', -2.0, 2.0, LG - .3, LG, BZ0, 16.6, 'd', plate=True)
add('rig-rail-catwalk-n-w', -2.0, -1.85, LG, LG + 1.1, BZ0, 14.4, '-'); add('rig-rail-catwalk-n-e', 1.85, 2.0, LG, LG + 1.1, BZ0, 14.4, '-')
# the decompression chamber (full cover) and the dive lockers (half cover) on the bay's floor
add('rig-chamber-n', -7.4, -5.0, LP, LP + 2.6, 11.0, 12.6, 'S'); add('rig-console-bay-n', 4.6, 7.2, LP, LP + 1.1, 19.6, 20.4, 'c')
case('rig-case-bay-n', -3.0, 11.6, y=LP)
# the survey sub moored in the pool: an island a jump from the south edge (1.3 m) or a drop from the catwalk, half cover on the bay's floor line
add('rig-sub-n', -1.3, 1.3, LP - .8, LP + 1.1, 14.9, 17.5, 'c')

# ---- the ring tunnels north (glass, one storey, 4 m wide): from each wing's north door north, then inward to the bay's mouth, the
#      last 3.2 m stepping down to the bay; a bulkhead with a 2 m door across each (the south pair are their mirrors)
def tunnel(side, sx):
    def X(a, b): return (sx * a, sx * b)
    nm = 'n-' + side
    add('rig-tunnel-%s-floor-0' % nm, *X(20.1, 24.9), -1.0, 0, WZ + T, 18.0, '.', plate=True)
    add('rig-tunnel-%s-floor-1' % nm, *X(11.6, 20.1), -1.0, 0, 13.2, 18.0, '.', plate=True)
    add('rig-tunnel-%s-base' % nm, *X(BX + T, 11.6), LP - .8, LP, 13.2, 18.0, '_')                    # under the steps
    stair('rig-stair-tunnel-%s' % nm, sx * (BX + T), sx * 11.6, MZ0, MZ1, LP, L0, 8)
    add('rig-glass-tunnel-%s-0' % nm, *X(24.5, 24.9), 0, HT, WZ + T, 18.0, 'g')          # the outer wall of the run north
    add('rig-glass-tunnel-%s-1' % nm, *X(20.1, 20.5), 0, HT, WZ + T, 13.2, 'g')          # its inner wall
    add('rig-glass-tunnel-%s-2' % nm, *X(11.6, 24.5), 0, HT, 17.6, 18.0, 'g')            # the outer wall of the run inward
    add('rig-glass-tunnel-%s-3' % nm, *X(11.6, 20.5), 0, HT, 13.2, 13.6, 'g')            # its inner wall
    add('rig-glass-tunnel-%s-4' % nm, *X(BX + T, 11.6), LP, HT, 17.6, 18.0, 'g')         # the two walls beside the steps
    add('rig-glass-tunnel-%s-5' % nm, *X(BX + T, 11.6), LP, HT, 13.2, 13.6, 'g')
    # the bulkhead across the run inward: two jambs and a head round a 2 m door
    add('rig-bulkhead-%s-0' % nm, *X(14.0, 14.4), 0, HT, 13.6, 14.6, '#'); add('rig-bulkhead-%s-1' % nm, *X(14.0, 14.4), 0, HT, 16.6, 17.6, '#')
    add('rig-bulkhead-%s-head' % nm, *X(14.0, 14.4), 2.6, HT, 14.6, 16.6, '#')
    case('rig-case-tunnel-%s' % nm, sx * 21.3, 16.8)
tunnel('w', -1); tunnel('e', 1)

# ---- hazards (UnityMaps.json `hazards[]`; existing kinds, timed from the arm time, FightAt + 4 s)
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), radius=0, first=0, start=0, period=0,
             offset=0, warn=0, danger=0, speed=0, length=0, charge=0, live=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
# The drill's piston (kind `press`): the ram of the central drill shaft, 4.4 m square and 4 m tall, parked 7 m over the wellhead
# (above the gallery's eye lines), driven down onto it. Each cycle: open, a 3.2 s telegraph (the core flaring under the wellhead's
# grate, the klaxon lamps, the ram shuddering), a 0.6 s stroke, 1.4 s held (lethal; it stands in the well as cover across the drill
# floor), a 1.2 s lift. Every 16 s; the first telegraph at FightAt + 13.6 s. (The press kind's lethal band is absolute, the ram's
# underside below 1.8 m: it strikes the y-0 drill floor.)
RAM, RAM_H, PARK = 4.4, 4.0, 9.0
HAZ = [hz(id='rig-piston', kind='press', a=v(0, PARK, 0), b=v(0, RAM_H / 2, 0), size=v(RAM, RAM_H, RAM), start=0, offset=0, period=16, warn=3.2, rise=.6,
          dwell=1.4, travel=1.2, lethal=True)]
# The bubbling vents (kind `vent` with damage ticks and a `wind` push along `axis`): a floor vent each side of each moon pool, across
# the strip between the tunnel's mouth and the water. A 2.6 s telegraph (the grates rattling, bubbles and mist rising), a 2 s blast:
# 2 HP per 0.25 s tick and 11 m/s^2 toward the pool (walk into it to hold). Every 18 s; a bay's two vents alternate (9 s apart), and
# a vent and its mirror share their time, so both teams face the same clock. First telegraph at FightAt + 19.4 s.
VZ0, VZ1 = 14.6, POOL[3]
for side, sx, off in (('w', -1, 2.0), ('e', 1, 11.0)):
    for bay, sz in (('n', 1), ('s', -1)):
        # the south bay's vents are the north's turned half round: (x, z) -> (-x, -z)
        nm = '%s-%s' % (bay, side if sz > 0 else SWAP[side]); px = sx * sz * 6.0; pz = sz * (VZ0 + VZ1) / 2
        HAZ.append(hz(id='rig-vent-' + nm, kind='vent', a=v(px, LP, pz), size=v(3.2, 3.0, VZ1 - VZ0), axis=v(-sx * sz, 0, 0), start=0, offset=off,
                      period=18, warn=2.6, dwell=2.0, tick=.25, damage=2, push=11, pushMode='wind'))

# ---- spawns (x, z, floor); yaw from +z toward +x, facing the origin
teamA = [(-25.6, 2.2, L0), (-25.6, -2.2, L0), (-26.6, 4.6, L0), (-26.6, -4.6, L0)]
teamB = [(-x, -z, y) for x, z, y in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
_f, _g, _h = (-26.0, -6.6, LG), (6.4, 11.4, LP), (17.4, 15.6, L0)
ffa = [teamA[0], teamB[0], _f, (-_f[0], -_f[1], _f[2]), _g, (-_g[0], -_g[1], _g[2]), _h, (-_h[0], -_h[1], _h[2])]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only exclusion columns (infinite upward) over every wall, glass, pylon and rail; the integration
#      (tools/generate_stage_hazards.py open_stage) keeps the walls' and glass's (they keep pilots inside the station), the pylons'
#      (the drill's gantry sits on them) and, for bots only, a rail's over a lethal drop (the catwalks over the pools)
def pilot_for(e):
    x0, x1, y0, y1, z0, z1 = e['box']; return dict(id=e['id'].replace('rig-', 'rig-top-', 1), center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .1, 3))
def capped(e):   # another block stands on this one's whole top (the Well's steel under its glass): no column needed
    b = e['box']; return any(abs(o['box'][2] - b[3]) < 1e-6 and o['box'][0] <= b[0] + 1e-6 and o['box'][1] >= b[1] - 1e-6 and o['box'][4] <= b[4] + 1e-6 and o['box'][5] >= b[5] - 1e-6 for o in els)
def needs_pilot(e): return e['ch'] in '-P' or (e['ch'] in '#g' and not e['plate'] and e['box'][3] >= HT - 1e-6 and not capped(e))
PILOT = [pilot_for(e) for e in els if needs_pilot(e)]

# ---- level bookkeeping
LEVELS = [(LP, 'level -1.6 (moon-pool bays)'), (L0, 'level 0 (drill floor, dormitories, spine, tunnels)'), (LG, 'level 3.2 (gallery, lounges, catwalks)')]
def base_of(e):
    y0 = e['box'][2]
    return LG if y0 >= LG - .45 else L0 if y0 >= -.65 else LP if y0 >= LP - .85 else y0
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
    W = int(math.ceil(X1 - X0)) + 4; R = int((BOUNDS['maxZ'] - BOUNDS['minZ']) // 2); out = []
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
            if y0 - .05 <= h + .1 <= y1: rect(x0, x1, z0, z1, 'Z' if 'piston' in nm else '~')
        for e in sorted(els, key=lambda e: e['ch'] in '#-g'):
            if in_slice(e, h) and not thin(e): b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
        for nm, (x, z, y) in [('1234'[i], p) for i, p in enumerate(teamA)] + [('5678'[i], p) for i, p in enumerate(teamB)] + list(zip(ffa_names[2:], ffa[2:])):
            if abs(y - h) < .05: put(x, z, nm)
        out.append(name)
        for r in range(R): z = BOUNDS['maxZ'] - 2 * r; out.append(f'{z-2:+6.1f}..{z:+5.1f} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- spawn sightlines in 3D (as skyframe_m1.py): a box blocks where the segment passes through it. Boxes topping out below 2.0 m over
#      the lower eye's floor never count, nor boxes under 2.0 m over their own level (half cover hides no standing pilot); floor plates do.
def seg_hits(a, b, e, lo):
    x0, x1, y0, y1, z0, z1 = e['box']
    if not e['plate'] and (y1 < lo + 2.0 or y1 < base_of(e) + 2.0): return False
    t0, t1 = 0.0, 1.0
    for o, d, l, hgh in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], y0, y1), (a[2], b[2] - a[2], z0, z1)):
        if abs(d) < 1e-9:
            if not (l <= o <= hgh): return False
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
            'maps': [{'id': 'rig', 'name': 'Deepwell', 'tag': 'Deepwell Station, 3,800 m down. Mind the moon pool.', 'theme': 'rig',
                      'music': 'rig-theme', 'ambience': 'rig-deep', 'look': 'rig-abyss', 'sky': 0x021018, 'floor': 0x2C3B42, 'accent': 0x3FE0D0,
                      'rotation': True, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    PW, GAP = int((X1 - X0 + 4) * scale), 2 * scale; PH = int((BOUNDS['maxZ'] - BOUNDS['minZ']) * scale); W, H = PW, 3 * PH + 2 * GAP
    img = bytearray(b'\x06\x12\x1a' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        oy = k * (PH + GAP); i0, i1 = int((x0 - X0 + 2) * scale), int((x1 - X0 + 2) * scale); j0, j1 = oy + int((BOUNDS['maxZ'] - z1) * scale), oy + int((BOUNDS['maxZ'] - z0) * scale)
        for j in range(max(oy, j0), min(oy + PH, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    col = {'#': (150, 160, 165), 'g': (90, 220, 230), 'P': (200, 160, 80), 'S': (190, 180, 160), 'c': (220, 190, 110), '-': (230, 210, 150), '/': (200, 200, 205), 'x': (170, 120, 70)}
    for k, (h, name) in enumerate(LEVELS):
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; fill(k, b[0], b[1], b[4], b[5], (70, 88, 96))
        for x0, x1, z0, z1, y0, y1, nm in footprint(HAZ):
            if y0 - .05 <= h + .1 <= y1: fill(k, x0, x1, z0, z1, (255, 120, 40) if 'piston' in nm else (120, 240, 255), .45)
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
    ids = [e['id'] for e in els]; need(len(ids) == len(set(ids)), 'unique ids'); need(all(i.startswith('rig-') for i in ids), 'id prefix')
    dup = sorted({i for i in ids if ids.count(i) > 1})
    if dup: print('duplicate ids:', dup)
    # point symmetry: every block has its partner turned half round (the map is fair to both teams)
    boxes = {tuple(round(q, 3) for q in e['box']) for e in els}
    lone = [e['id'] for e in els if tuple(round(q, 3) for q in (-e['box'][1], -e['box'][0], e['box'][2], e['box'][3], -e['box'][5], -e['box'][4])) not in boxes]
    print('blocks without a point-symmetric partner:', lone or 'none'); need(not lone, 'symmetry')
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    need(bad == 0, 'spawn sightlines')
    sp = ffa + teamA[1:] + teamB[1:]
    mf = min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)); mab = min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m (3D); min A-B spacing %.1f m' % (mf, mab)); need(mf >= 9, 'FFA spacing'); need(mab >= 24, 'A-B spacing')
    print('FFA floors: ' + ', '.join('%s %.1f' % (n, p[2]) for n, p in named))
    # hazard clearance, per floor: a spawn keeps 3 m (plan) from any live volume that spans its body's height, and from a pool
    worst = min((dist_rect(p, r[:4]), r[6], p) for p in sp for r in footprint(HAZ) if r[4] - .1 <= p[2] + 1.8 and r[5] >= p[2])
    print('closest spawn to a hazard volume on its floor: %.1f m (%s, spawn %s)' % worst); need(worst[0] >= 3, 'hazard clearance')
    pools = [POOL, (-POOL[1], -POOL[0], -POOL[3], -POOL[2])]
    wp = min((dist_rect(p, r), p) for p in sp for r in pools if abs(p[2] - LP) < .05)
    print('closest bay spawn to a moon pool: %.1f m (spawn %s)' % wp); need(wp[0] >= 1.5, 'pool clearance')
    inb = all(BOUNDS['minX'] + 1 <= x <= BOUNDS['maxX'] - 1 and BOUNDS['minZ'] + 1 <= z <= BOUNDS['maxZ'] - 1 for x, z, y in sp)
    print('all spawns inside the bounds minus 1 m:', inb); need(inb, 'bounds')
    low_top = min(e['box'][3] for e in els if e['plate'])
    print('lowest floor %.1f, killY %.1f' % (low_top, BOUNDS['killY'])); need(low_top + .9 - 2 > BOUNDS['killY'] >= low_top - 3.2, 'killY')
    band = [e['id'] for e in els if e['ch'] not in '-/' and not e['plate'] and 1.3 < e['box'][3] - base_of(e) < 2.2]
    print('blocks topping out 1.3..2.2 m over their level:', band or 'none'); need(not band, 'cover band')
    pil_ids = {p['id'] for p in PILOT}; miss = [e['id'] for e in els if needs_pilot(e) and e['id'].replace('rig-', 'rig-top-', 1) not in pil_ids]
    print('pilot blocks: %d (rails %d); walls, glass, pylons or rails without one: %s' % (len(PILOT), sum(e['ch'] == '-' for e in els), miss or 'none')); need(not miss, 'pilot blocks')
    flights = {}
    for e in els:
        if e['ch'] == '/': flights.setdefault(e['id'].rsplit('-', 1)[0], []).append(e['box'])
    def flight_bad(bs):
        tops = sorted(round(b[3], 3) for b in bs); steps = [b - a for a, b in zip(tops, tops[1:])]
        return max(steps) > .2 + 1e-6 or min(max(b[1] - b[0], b[5] - b[4]) for b in bs) < 1.6 - 1e-6 or tops[-1] not in (L0, LG)
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
        if e['plate'] and e['ch'] in '.d':
            b = e['box']; (i0, j0), (i1, j1) = cell_of(b[0], b[4]), cell_of(b[1], b[5])
            if not any((i, j, round(b[3], 3)) in seen for i in range(i0, i1 + 1) for j in range(j0, j1 + 1)): unreached.append(e['id'])
    print('every spawn and walkable plate reachable on foot from A1:', unreached or 'yes'); need(not unreached, 'reachability')
    # falls: the only cells a pilot on a reachable floor can step into with nothing under them are the moon pools
    void = set()
    for (i, j, t) in seen:
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            c = (i + di, j + dj); cx, cz = BOUNDS['minX'] + (c[0] + .5) * CELL, BOUNDS['minZ'] + (c[1] + .5) * CELL
            if not any(e['box'][0] < cx < e['box'][1] and e['box'][4] < cz < e['box'][5] and e['box'][2] < t + 1.8 for e in buck.get(c, [])): void.add((round(cx, 3), round(cz, 3)))
    stray = [c for c in void if not any(r[0] - .01 < c[0] < r[1] + .01 and r[2] - .01 < c[1] < r[3] + .01 for r in pools)]
    print('cells with nothing under them beside a reachable floor: %d, all in the moon pools: %s' % (len(void), not stray)); need(void and not stray, 'falls')
    if stray: print('   stray', sorted(stray)[:12])
    # hazard timing: nothing lethal in the first 4 s; telegraphs at least today's (3 s for a drop from above, 0.8 s for a vent)
    for h in HAZ:
        if h['kind'] == 'press':
            open_ = h['period'] - h['warn'] - h['rise'] - h['dwell'] - h['travel']; first = 4 + h['start'] + h['offset'] + open_
            need(open_ > 0 and h['warn'] >= 3 and first >= 8 and h['b']['y'] - h['size']['y'] / 2 == 0 and h['a']['y'] - h['size']['y'] / 2 >= LG + 3.4, 'piston timing ' + h['id'])
        else:
            first = 4 + h['start'] + h['offset'] + h['period'] - h['warn'] - h['dwell']; need(h['warn'] >= .8 and first >= 8 and h['push'] > 0 and h['damage'] > 0, 'vent timing ' + h['id'])
            # each vent blows toward its own bay's pool
            pl = min(pools, key=lambda r: abs((r[2] + r[3]) / 2 - h['a']['z'])); need((((pl[0] + pl[1]) / 2 - h['a']['x']) * h['axis']['x']) > 0, 'vent aim ' + h['id'])
        print('  %-14s %-6s first telegraph at FightAt + %5.2f s, warn %.1f s, period %.0f s' % (h['id'], h['kind'], first, h['warn'], h['period']))
    props = sum(e['role'] != 'static' for e in els)
    print('blocks: %d (%d breakable); hazards: %d; pilot blocks: %d; spawns: %d FFA + 2 x %d team' % (len(els), props, len(HAZ), len(PILOT), len(ffa), len(teamA))); need(len(els) <= 350, 'block budget')
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'rig_m2.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'rig_m2.png')); print('wrote', os.path.join(out, 'rig_m2.json'), 'and rig_m2.png')
    print('FAILED: ' + ', '.join(fails) if fails else 'all checks pass')
    sys.exit(1 if fails else 0)
