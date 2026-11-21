# Polaris Station M2 (polar, 62 x 50 m): an arctic observatory under the aurora, rebuilt from scratch (docs/arenas/POLAR.md).
# The icebreaker frozen in at the north (its bow toward the middle, its foredeck at 3.2 m part of the play space), the refrozen
# channel it cut (thin ice over freezing water: four collapse segments), the observatory terrace (2.4 m) under the great dish at
# the south, the open shelf edge behind it (a fall into the sea), two cabins a side, the field between them. polar_m1.py is history.
# Coordinates: x east, z north, y up, metres; Unity frame. Mirror symmetric: (x, z) -> (-x, z). Team A west, team B east; FFA 8.
# usage: python3 docs/arenas/scripts/polar_m2.py [--out DIR] [--json PATH]   (stdout: plan + checks, exit 1 if any check fails;
#        --json writes the draft only; --out writes polar_m2.json, the map in the UnityMaps.json shape, and polar_m2.png, a plan)
# Ids carry the prefixes the presentation keys its looks, kit and art on: polar-ice-* (the shelf), polar-thin-* / polar-seam-*
# (the thin ice and its fracture seams), polar-ridge-* (pressure ridges: the outer walls), polar-ship-* (the icebreaker),
# polar-obs-* (the observatory), polar-cabin-* (the team cabins), polar-lab-*, polar-shed-*, polar-tank-*, polar-snowcat-*,
# polar-spire-* (ice crystal formations), polar-drift-* (snow drifts), polar-crate-*, polar-drums-*, polar-mast-*, polar-sled-*,
# polar-hut-*, polar-stair-*; tokens w e a b swap in mirrored partners.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -33, 33, -25, 27            # the shelf: its south edge (z -25) is open to the sea
# The sim clamps bots, abilities and teleports to the bounds minus the capsule, not walking pilots: the bounds run 4 m past the
# shelf edge, so a pilot who walks or is knocked off it falls into the sea. killY -3: a body's middle below it dies, about half a
# second after it leaves the edge or sinks with the thin ice (the water stands at -1.3).
BOUNDS = {'minX': -31, 'maxX': 31, 'minZ': -29, 'maxZ': 25, 'ceiling': 22, 'killY': -3}
WATER = -1.3
CAP = .42
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, seg
SWAP = {'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True, seg=None):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, seg=seg))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, z0, z1), ch=ch, hp=hp, seg=mirror_id(seg) if seg else None))
def stair(i, a, b, lo, hi, ya, yb, steps, axis='x', mat='wood', mirror=True):
    # a solid flight from (a, ya) up to (b, yb) along x (lo..hi its z) or along z (lo..hi its x); every step stands on the floor
    run = (b - a) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        p, q = sorted((a + run * k, a + run * (k + 1))); y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), p, q, ya, y1, lo, hi, '/', mat=mat, mirror=mirror)
        else: add('%s-%02d' % (i, k), lo, hi, ya, y1, p, q, '/', mat=mat, mirror=mirror)
PILOT = []   # pilot columns: only the caps of the outer walls (they keep pilots inside the map) and the pedestal's top under the dish's yoke
def pilot(i, x0, x1, z0, z1, bottom, mirror=True):
    PILOT.append(dict(id=i, center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=bottom))
    if mirror: PILOT.append(dict(id=mirror_id(i), center=v(round(-(x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=bottom))

# ---- the shelf: 4 m of ice (the channel's walls), cut by the channel the icebreaker broke (x +-2.6) and left open at the south
CH = 2.6; POND_N = (1.2, 7.8); POND_S = (-8.4, -1.2)
add('polar-ice-w', X0, -CH, -4, 0, Z0, Z1, '.', mat='foundation')
add('polar-ice-n', -CH, CH, -4, 0, POND_N[1], Z1, '.', mat='foundation', mirror=False)          # under the bow
add('polar-ice-bridge', -CH, CH, -4, 0, POND_S[1], POND_N[0], '.', mat='foundation', mirror=False)   # the solid crossing between the ponds
add('polar-ice-s', -CH, CH, -4, 0, Z0, POND_S[0], '.', mat='foundation', mirror=False)
# ---- thin ice: each pond is two sheets (a west and an east plate either side of a fracture seam). A blast that breaks a seam
#      (structural, 36 hp) cracks its sheet (1.4 s), which then sinks 3.2 m with whoever stands on it and is gone: collapse segments
SEGS = []
def sheet(name, z0, z1):
    sid = 'polar-thin-' + name; seam = 'polar-seam-' + name; zc = (z0 + z1) / 2; ln = z1 - z0
    add(sid + '-w', -CH, -.15, -.3, 0, z0, z1, '~', mat='stone', role='carried', seg=sid)
    add(seam, -.15, .15, -.3, 0, z0, z1, '=', mat='stone', role='structural', hp=36, mirror=False)
    SEGS.append(dict(id=sid, building=sid, mode='pancake', material='stone', supports=[dict(member=seam, w=1)], failBelow=1, telegraph=1.4, duration=1.6,
                     maxAccel=6.9, mass=24, direction=v(0, -1, 0), pivot=v(0, 0, zc), axis=v(1, 0, 0), angle=0, blocks=[sid + '-w', sid + '-e'],
                     poses=[dict(block=sid + '-' + s, pivot=v(x, -.15, zc), axis=v(1, 0, 0), angle=0.0, drop=3.2, delay=d) for s, x, d in (('w', -(CH + .15) / 2, 0.0), ('e', (CH + .15) / 2, 0.12))],
                     crush=dict(center=v(0, -1.7, zc), size=v(2 * CH, 3.4, ln), damage=45, push=6, cap=60, capHpFraction=0.6),
                     sweeps=[dict(center=v(0, .6, zc), size=v(2 * CH, 3.2, ln))], rubble=[], nav=dict(remove=[], add=[])))
sheet('n-0', POND_N[0], 4.5); sheet('n-1', 4.5, POND_N[1]); sheet('s-0', -4.8, POND_S[1]); sheet('s-1', POND_S[0], -4.8)

# ---- pressure ridges: the outer walls west, east and north (a foot and a taller ridge behind it), 5 m and more; capped
for k, (z0, z1, hf, hr) in enumerate(((Z0, -9, 5.4, 6.8), (-9, 9, 5.8, 7.4), (9, Z1, 5.2, 6.4))):
    add('polar-ridge-w-%d-foot' % k, -32, -31, 0, hf, z0, z1, '#'); add('polar-ridge-w-%d-back' % k, -33, -32, 0, hr, z0, z1, '#')
for k, (x0, x1, hf, hr) in enumerate(((-31, -18, 5.6, 7.0), (-18, -6, 5.2, 6.2))):
    add('polar-ridge-n-w-%d-foot' % k, x0, x1, 0, hf, 25, 26, '#'); add('polar-ridge-n-w-%d-back' % k, x0, x1, 0, hr, 26, 27, '#')
pilot('polar-top-ridge-w', -33, -31, Z0, Z1, 4.9); pilot('polar-top-ridge-n-w', -31, -6, 25, 27, 4.9)

# ---- the icebreaker: the hull from the bow (z 8.2) north out of the map, its foredeck at 3.2; the wheelhouse front closes the north
DECK = 3.2
add('polar-ship-hull', -6, 6, 0, DECK, 14, Z1, 'S', mat='metal', mirror=False)
BOW = [(13, 14, 5.8), (12, 13, 5.3), (11, 12, 4.6), (10.5, 11, 4.0), (10, 10.5, 3.5), (9.5, 10, 2.9), (9, 9.5, 2.2), (8.6, 9, 1.5), (8.2, 8.6, .8)]
for k, (z0, z1, w) in enumerate(BOW): add('polar-ship-bow-%d' % k, -w, w, 0, DECK, z0, z1, 'S', mat='metal', mirror=False)
add('polar-ship-bridge', -6, 6, DECK, 12.0, 24, Z1, 'B', mat='metal', mirror=False)                 # the wheelhouse front
pilot('polar-top-ship-bridge', -6, 6, 24, Z1, 11.9, mirror=False)
add('polar-ship-rail-w-0', -6, -5.75, DECK, DECK + 1.1, 14, 18.4, 'r', mat='metal')                 # bulwarks (half cover); open at the gangway
add('polar-ship-rail-w-1', -6, -5.75, DECK, DECK + 1.1, 20, 24, 'r', mat='metal')
add('polar-ship-house', -2.4, 2.4, DECK, DECK + 2.6, 18.4, 20.0, 'H', mat='metal', mirror=False)    # the winch house: full cover
add('polar-ship-boat-w', -5.4, -3.6, DECK, DECK + 2.4, 21.0, 23.4, 'H', mat='wood')                 # a lifeboat on its cradle: full cover
add('polar-ship-windlass', -1.2, 1.2, DECK, DECK + 1.1, 10.8, 11.8, 'o', mat='metal', mirror=False)
add('polar-ship-mast', -.5, .5, DECK, 15.0, 12.6, 13.6, 'M', mat='metal', mirror=False)             # the foremast
add('polar-crate-ship-w', -4.6, -3.4, DECK, DECK + 1.2, 15.2, 16.4, 'x', mat='wood', role='cover', hp=34)
# gangways: a solid timber flight up each side from the ice (z 12) to a landing beside the deck (z 18.4..20)
stair('polar-stair-gang-w', 12.0, 18.4, -7.6, -6.0, 0, DECK, 16, axis='z')
add('polar-ship-landing-w', -7.6, -6.0, 0, DECK, 18.4, 20.0, 'g', mat='wood')

# ---- the observatory: a terrace (2.4) under the dish's pedestal, a windbreak behind it, stairs west and east
OBS = 2.4
add('polar-obs-terrace', -8, 8, 0, OBS, -21.4, -14, 'T', mat='stone', mirror=False)
add('polar-obs-back', -8, 8, OBS, OBS + 2.6, -21.4, -21.0, 'W', mat='wood', mirror=False)           # the windbreak (full cover)
add('polar-obs-pedestal', -1.6, 1.6, OBS, 9.5, -18.2, -15.0, 'P', mat='metal', mirror=False)        # the dish's pedestal
pilot('polar-top-obs-pedestal', -1.6, 1.6, -18.2, -15.0, 9.4, mirror=False)                  # the dish's yoke turns right on the pedestal's top (7 m over the terrace)
add('polar-obs-rack-w', -4.0, -3.6, OBS, OBS + 2.6, -21.0, -17.2, 'W', mat='metal')                 # instrument racks: screen the spawn
add('polar-obs-rail-w', -8, -2.4, OBS, OBS + 1.1, -14.25, -14, 'r', mat='metal')                    # the front parapet (half cover), open in the middle
add('polar-obs-console-w', -6.6, -4.6, OBS, OBS + 1.1, -17.0, -16.2, 'o', mat='metal')
stair('polar-stair-obs-w', -12.8, -8.0, -19.2, -17.2, 0, OBS, 12, axis='x', mat='stone')

# ---- team A's cabin (west; B's is the mirror): timber and steel, 8 x 12 m, two doors east, a screen wall inside
H = 3.2
add('polar-cabin-a-wall-w', -31.0, -30.6, 0, H, -6, 6, 'C', mat='wood')
add('polar-cabin-a-wall-n', -30.6, -22.6, 0, H, 5.6, 6.0, 'C', mat='wood'); add('polar-cabin-a-wall-s', -30.6, -22.6, 0, H, -6.0, -5.6, 'C', mat='wood')
for k, (z0, z1) in enumerate(((-5.6, -4.4), (-2.4, 2.4), (4.4, 5.6))): add('polar-cabin-a-wall-e-%d' % k, -23.0, -22.6, 0, H, z0, z1, 'C', mat='wood')
for k, (z0, z1) in enumerate(((-4.4, -2.4), (2.4, 4.4))): add('polar-cabin-a-lintel-%d' % k, -23.0, -22.6, 2.4, H, z0, z1, 'C', mat='wood')
add('polar-cabin-a-roof', -31.2, -22.2, H, H + .3, -6.4, 6.4, 'R', mat='metal')
add('polar-cabin-a-screen', -26.4, -26.0, 0, H, -4.2, 4.2, 'C', mat='wood')            # the screen wall: round its ends (1.4 m) to the doors
add('polar-cabin-a-stove', -24.9, -24.1, 0, 1.1, -.5, .5, 'o', mat='metal')
add('polar-cabin-a-bunk', -30.6, -29.9, 0, 1.0, -.8, .8, 'o', mat='wood')
# ---- the lab (north-west) and the generator shed (south-west): 6 x 6 m, one door toward the cabin
def hut(i, x0, x1, z0, z1, h, door_south, mat):
    T = .4; xd0, xd1 = (x0 + x1) / 2 - 1.0, (x0 + x1) / 2 + 1.0; zf0, zf1 = (z0, z0 + T) if door_south else (z1 - T, z1); zb0, zb1 = (z1 - T, z1) if door_south else (z0, z0 + T)
    add(i + '-wall-w', x0, x0 + T, 0, h, z0, z1, 'L', mat=mat); add(i + '-wall-e', x1 - T, x1, 0, h, z0, z1, 'L', mat=mat)
    add(i + '-wall-back', x0 + T, x1 - T, 0, h, zb0, zb1, 'L', mat=mat)
    add(i + '-wall-front-0', x0 + T, xd0, 0, h, zf0, zf1, 'L', mat=mat); add(i + '-wall-front-1', xd1, x1 - T, 0, h, zf0, zf1, 'L', mat=mat)
    add(i + '-lintel', xd0, xd1, 2.4, h, zf0, zf1, 'L', mat=mat)
    add(i + '-roof', x0 - .2, x1 + .2, h, h + .3, z0 - .2, z1 + .2, 'R', mat='metal')
hut('polar-lab-w', -29.6, -23.6, 14.6, 20.6, 3.0, True, 'wood')
hut('polar-shed-w', -29.8, -24.2, -20.8, -15.2, 3.0, False, 'metal')
add('polar-lab-w-porch', -28.4, -23.6, 0, 2.6, 12.6, 13.0, 'L', mat='wood')                 # the windbreak before the lab's door (full cover)
add('polar-lab-w-bench', -29.2, -28.5, 0, 1.0, 17.0, 19.4, 'o', mat='wood')
add('polar-shed-w-genset', -29.4, -28.4, 0, 1.2, -20.0, -17.6, 'o', mat='metal')

# ---- the field (west; the east is the mirror): full cover alternating with snow drifts and breakable crates
def drift(i, x0, x1, z0, z1, top=1.1):                # a snow drift: a 0.3 m apron (walked over) and the crest (half cover)
    add(i + '-apron', x0 - .5, x1 + .5, 0, .3, z0 - .5, z1 + .5, 'b'); add(i + '-crest', x0, x1, .3, top, z0, z1, 'b')
# the channel's banks: slabs of ice the icebreaker heaved up either side of its channel (full cover), open at the crossing and
# at one gap a pond
for k, (z0, z1, h) in enumerate(((-9.2, -5.8, 2.8), (-3.6, -1.6, 3.4), (1.6, 3.6, 3.0), (5.2, 9.4, 3.8))): add('polar-floe-w-%d' % k, -4.8, -3.2, 0, h, z0, z1, 'F')
add('polar-spire-w-0', -9.6, -7.4, 0, 3.8, -1.7, 1.7, 'I')                                   # ice crystal formations: full cover
add('polar-spire-w-1', -18.8, -17.0, 0, 4.4, -11.4, -9.6, 'I')
add('polar-spire-w-2', -11.6, -10.0, 0, 3.2, 14.6, 16.2, 'I')
add('polar-spire-w-3', -14.4, -12.8, 0, 3.4, 3.0, 4.6, 'I')
add('polar-spire-w-4', -11.4, -9.6, 0, 3.0, -15.6, -13.8, 'I')
add('polar-spire-w-5', -16.6, -15.0, 0, 3.6, 16.6, 18.2, 'I')
add('polar-spire-road', -1.3, 1.3, 0, 4.2, -11.8, -9.8, 'I', mirror=False)                 # the great cluster before the terrace
add('polar-tank-w-0', -21.0, -18.4, 0, 3.0, 8.2, 10.8, 'O', mat='metal'); add('polar-tank-w-1', -21.0, -18.4, 0, 3.0, 11.4, 14.0, 'O', mat='metal')
add('polar-snowcat-w', -13.6, -11.0, 0, 2.5, -9.4, -4.6, 'V', mat='metal'); add('polar-snowcat-w-blade', -14.0, -10.6, 0, 1.0, -4.6, -4.0, 'o', mat='metal')
add('polar-hut-w', -22.2, -19.6, 0, 2.6, -14.0, -11.4, 'h', mat='wood')                    # the instrument hut
add('polar-stack-w', -11.4, -9.2, 0, 2.6, -23.4, -21.4, 'O', mat='wood')                  # a tarped cargo stack on the shelf path
add('polar-obs-beacon', -.8, .8, 0, 5.0, -24.8, -23.2, 'M', mat='metal', mirror=False)     # the shelf beacon behind the observatory
add('polar-mast-w', -9.4, -8.6, 0, 9.0, 8.4, 9.2, 'M', mat='metal')                        # the weather mast
add('polar-cabin-a-porch-0', -21.0, -20.6, 0, 2.6, -4.9, -1.9, 'C', mat='wood')            # windbreaks before the doors (full cover)
add('polar-cabin-a-porch-1', -21.0, -20.6, 0, 2.6, 1.9, 4.9, 'C', mat='wood')
drift('polar-drift-w-0', -18.4, -15.2, -.6, .6)
drift('polar-drift-w-1', -9.0, -5.6, -9.0, -7.8)
drift('polar-drift-w-2', -21.0, -17.6, 21.2, 22.4)
drift('polar-drift-w-3', -14.6, -11.4, 9.6, 10.8)
drift('polar-drift-w-4', -24.4, -21.2, -23.6, -22.4)
add('polar-crate-w-0', -16.6, -15.4, 0, 2.4, 5.6, 6.8, 'x', mat='wood', role='cover', hp=34)
add('polar-crate-w-1', -17.4, -16.2, 0, 1.2, -5.4, -4.2, 'x', mat='wood', role='cover', hp=34)
add('polar-crate-w-2', -7.0, -5.8, 0, 1.2, 3.8, 5.0, 'x', mat='wood', role='cover', hp=34)
add('polar-crate-w-3', -7.2, -6.0, 0, 1.2, -5.2, -4.0, 'x', mat='wood', role='cover', hp=34)
add('polar-crate-w-4', -10.6, -9.4, 0, 1.2, 11.4, 12.6, 'x', mat='wood', role='cover', hp=34)
add('polar-drums-w', -23.4, -22.2, 0, 1.2, 9.6, 10.8, 'x', mat='metal', role='cover', hp=150)
add('polar-sled-w', -14.4, -12.0, 0, 1.1, 22.6, 23.8, 'o', mat='wood')

# ---- hazards: steam vents that scald (kind `vent`, damage; the Factory's clock: FightAt + 4 s arm delay)
def hz(**k):
    h = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), radius=0, first=0, start=0, period=0, offset=0, warn=0,
             danger=0, speed=0, length=0, charge=0, live=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, lethal=False, alternate=False, zones=[])
    h.update(k); return h
VENT = (1.6, 3.4, 1.6)                       # the steam column
HAZ = []
for name, x, z, y, st in (('road', -5.2, -11.6, 0, 1), ('gang', -6.8, 9.6, 0, 4.5)):
    for side in (0, 1):
        HAZ.append(hz(id='polar-vent-%s-%s' % (name, 'we'[side]), kind='vent', a=v(x if side == 0 else -x, y, z), size=v(*VENT), start=st + side * 7,
                      period=14, warn=1.8, dwell=2.6, damage=8, tick=0.5))
def hazard_rects(): return [(h['a']['x'] - VENT[0] / 2, h['a']['x'] + VENT[0] / 2, h['a']['z'] - VENT[2] / 2, h['a']['z'] + VENT[2] / 2, h['a']['y'], h['id']) for h in HAZ]

# ---- spawns: (x, z, floor). yaw from +z toward +x, facing the middle of the map
teamA = [(-29.2, -1.4, 0), (-29.2, 1.4, 0), (-27.6, -3.2, 0), (-27.6, 3.2, 0)]
teamB = [(-x, z, f) for x, z, f in teamA]
ffa_names = ['A1', 'B1', 'labW', 'labE', 'shedW', 'shedE', 'obs', 'deck']
ffa = [teamA[0], teamB[0], (-26.6, 18.2, 0), (26.6, 18.2, 0), (-26.4, -18.4, 0), (26.4, -18.4, 0), (0.0, -19.8, OBS), (0.0, 22.0, DECK)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1] - 2)) + 360) % 360
LEVELS = [('foredeck', 0.0, 16.5, DECK), ('bow', 0.0, 10.0, DECK), ('terrace', 5.0, -19.0, OBS), ('thin ice n', 1.2, 5.0, 0.0), ('thin ice s', -1.2, -5.0, 0.0),
          ('shelf path', 0.0, -22.2, 0.0), ('north-east pocket', 20.0, 20.0, 0.0)]

def paint():
    W = X1 - X0; R = (Z1 - Z0) // 2; g = [[' '] * W for _ in range(R)]
    def put(x, z, ch):
        c = int(math.floor(x - X0)); r = int((Z1 - z) // 2)
        if 0 <= c < W and 0 <= r < R: g[r][c] = ch
    def rect(x0, x1, z0, z1, ch):
        zz = z0
        while zz < z1 - 1e-9:
            xx = x0
            while xx < x1 - 1e-9: put(xx + .25, zz + .25, ch); xx += .5
            zz += .5
    for e in sorted(els, key=lambda e: e['box'][3]):
        if e['ch'] == 'R': continue
        b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
    for i, p in enumerate(teamA): put(p[0], p[1], str(i + 1)); put(-p[0], p[1], str(i + 5))
    for nm, p in zip('fFgGhH', ffa[2:]): put(p[0], p[1], nm)
    for h in HAZ: put(h['a']['x'], h['a']['z'], '*')
    out = ['legend: # ridge  . shelf ice  ~ thin ice  = seam  S hull/deck (3.2)  B wheelhouse  H deck cover  T terrace (2.4)  P pedestal  W windbreak/racks',
           '        C cabin  L lab/shed  I ice spires  F heaved floes  O tanks  V snowcat  h hut  M masts  b drifts  o half cover  x crates  r rails  / stairs  g landing  * vents',
           '        1-4 team A  5-8 team B  f F labs  g G sheds  h obs  H deck (FFA)']
    for r in range(R): z = Z1 - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- line of sight: a 3D segment against every block that is not breakable cover
def seg_box(a, b, box):
    x0, x1, y0, y1, z0, z1 = box; t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], y0, y1), (a[2], b[2] - a[2], z0, z1)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
SOLID = None
def visible(a, b):
    global SOLID
    if SOLID is None: SOLID = [e['box'] for e in els if e['role'] not in ('cover',)]
    return not any(seg_box(a, b, bx) for bx in SOLID)
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9)
                if visible((a[0] + dx, a[2] + eye, a[1] + dz), (b[0] + ex, b[2] + eye, b[1] + ez)))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a[:2], b[:2]):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- reachability: a 0.25 m raster of standable block tops, capsule ring and 1.8 m headroom, |dy| <= 0.25 links
CELL = .25
def raster():
    tiles = {}
    for e in els:
        x0, x1, y0, y1, z0, z1 = e['box']
        for ti in range(int(math.floor(x0 - CAP)), int(math.floor(x1 + CAP)) + 1):
            for tj in range(int(math.floor(z0 - CAP)), int(math.floor(z1 + CAP)) + 1): tiles.setdefault((ti, tj), []).append(e['box'])
    nx, nz = int((BOUNDS['maxX'] - BOUNDS['minX']) / CELL), int((BOUNDS['maxZ'] - BOUNDS['minZ']) / CELL)
    stand = {}
    for i in range(nx):
        cx = BOUNDS['minX'] + (i + .5) * CELL
        for j in range(nz):
            cz = BOUNDS['minZ'] + (j + .5) * CELL
            near = tiles.get((int(math.floor(cx)), int(math.floor(cz))), [])
            over = [b for b in near if b[0] < cx < b[1] and b[4] < cz < b[5]]
            tops = []
            for t in sorted({round(b[3], 3) for b in over}):
                if any(b[2] < t + 1.8 - 1e-3 and b[3] > t + 1e-3 for b in over): continue
                if any(b[0] - CAP < cx < b[1] + CAP and b[4] - CAP < cz < b[5] + CAP and b[2] < t + 1.8 and b[3] > t + .45 for b in near): continue
                tops.append(t)
            if tops: stand[(i, j)] = tops
    def cell(x, z): return (int((x - BOUNDS['minX']) / CELL), int((z - BOUNDS['minZ']) / CELL))
    start = (cell(*teamA[0][:2]), 0.0); seen = {start}; todo = [start]
    while todo:
        (i, j), t = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            for u in stand.get(n, ()):
                if abs(u - t) <= .25 + 1e-3 and (n, u) not in seen: seen.add((n, u)); todo.append((n, u))
    return stand, seen, cell

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        b = {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3) + 0.0, round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3) + 0.0),
             'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
        if e['seg']: b['segment'] = e['seg']
        return b
    sp = lambda p: {'pos': v(p[0], round(p[2] + 1.1, 3), p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'polar', 'name': 'Polaris Station', 'tag': 'The dish is listening. Mind the thin ice.', 'theme': 'polar',
                      'music': 'cybercity-neon-riot', 'ambience': 'polar-wind', 'look': 'polar-aurora', 'sky': 0x06101E, 'floor': 0xCFE0EE, 'accent': 0x5CF2A8,
                      'rotation': True, 'bounds': BOUNDS,
                      'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(e) for e in els], 'segments': SEGS, 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    W, H_ = (X1 - X0) * scale, (Z1 - Z0 + 4) * scale; ZT = Z1; img = bytearray(b'\x10\x1c\x30' * W * H_)
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        i0, i1 = int((x0 - X0) * scale), int((x1 - X0) * scale); j0, j1 = int((ZT - z1) * scale), int((ZT - z0) * scale)
        for j in range(max(0, j0), min(H_, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    col = {'.': (196, 212, 228), '~': (120, 190, 220), '=': (40, 90, 150), '#': (236, 242, 250), 'S': (150, 60, 50), 'B': (230, 230, 224), 'H': (110, 110, 120), 'T': (150, 156, 168),
           'P': (60, 70, 90), 'W': (120, 96, 70), 'C': (150, 104, 66), 'L': (130, 110, 90), 'I': (150, 220, 240), 'F': (170, 205, 235), 'O': (90, 120, 110), 'V': (220, 120, 30), 'h': (120, 90, 80),
           'M': (240, 120, 40), 'b': (232, 238, 246), 'o': (130, 125, 110), 'x': (180, 130, 75), 'r': (230, 200, 60), '/': (170, 150, 120), 'g': (160, 140, 110)}
    for e in sorted(els, key=lambda e: e['box'][3]):
        if e['ch'] == 'R': continue
        b = e['box']; fill(b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)))
    for h in HAZ: fill(h['a']['x'] - .8, h['a']['x'] + .8, h['a']['z'] - .8, h['a']['z'] + .8, (255, 255, 255), .8)
    for i, p in enumerate(teamA + teamB): fill(p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (40, 170, 255) if i < 4 else (240, 60, 100))
    for p in ffa[2:]: fill(p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (250, 230, 40))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H_))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H_, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint()); fails = []
    named = list(zip(ffa_names, ffa)); n = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; n += check([(p, q) for p in A for q in B], 'Teams A-B')
    if n: fails.append('spawn sightlines')
    sp = ffa + teamA[1:] + teamB[1:]
    d3 = lambda p, q: math.dist((p[0], p[1], p[2]), (q[0], q[1], q[2]))
    mf = min(d3(p, q) for p, q in itertools.combinations(ffa, 2)); mab = min(d3(p, q) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m; min A-B spacing %.1f m' % (mf, mab))
    if mf < 12: fails.append('FFA spacing')
    hc = min((dist_rect(p, r), p, r[5]) for p in sp for r in hazard_rects() if abs(r[4] - p[2]) < .5)
    print('closest spawn to a vent on its own floor: %.1f m (%s)' % (hc[0], hc[2]))
    if hc[0] < 3: fails.append('spawn within 3 m of a hazard')
    tc = min((dist_rect(p, (-CH, CH, POND_S[0], POND_N[1])), p) for p in sp if p[2] == 0)
    print('closest ground spawn to the thin ice: %.1f m; to the shelf edge: %.1f m' % (tc[0], min(p[1] - Z0 for p in sp if p[2] == 0)))
    if tc[0] < 6: fails.append('spawn by the thin ice')
    clr = min((min(dist_rect(p, (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < p[2] + 1.9 and e['box'][3] > p[2] + .45), p) for p in sp)
    print('closest spawn to any block: %.1f m at %s' % clr)
    if clr[0] < .6: fails.append('spawn inside or against a block')
    stand, seen, cell = raster()
    lv = {}
    for (c, t) in seen: lv[t] = lv.get(t, 0) + 1
    print('3D raster (%.2f m cells): %d standable cells, %d reachable from A1; by top: %s' % (CELL, sum(len(t) for t in stand.values()), len(seen),
          ', '.join('%.1f: %d' % (k, lv[k]) for k in sorted(lv) if lv[k] >= 60)))
    unreach = [nm for nm, p in named + A + B if (cell(p[0], p[1]), float(p[2])) not in seen]
    print('every spawn reachable on foot:', not unreach, ('(unreachable: %s)' % ', '.join(unreach)) if unreach else '')
    if unreach: fails.append('spawn reachability')
    for nm, x, z, y in LEVELS:
        ok = (cell(x, z), y) in seen
        print('   %-18s y %.1f: %s' % (nm, y, 'reachable' if ok else 'NOT REACHABLE'))
        if not ok: fails.append('level ' + nm)
    # team spawns hidden from every reachable elevated cell (the deck, the terrace, roofs) at chest and head
    perch = [(BOUNDS['minX'] + (c[0] + .5) * CELL, t + 1.6, BOUNDS['minZ'] + (c[1] + .5) * CELL) for (c, t) in seen if t >= 2.3 and c[0] % 2 == 0 and c[1] % 2 == 0]
    peek = sorted({nm for nm, p in A + B for q in perch if any(visible(q, (p[0], p[2] + y, p[1])) for y in (1.2, 1.9))})
    print('team spawns seen from %d elevated cells (deck, terrace): %s' % (len(perch), ', '.join(peek) or 'none'))
    if peek: fails.append('spawns overlooked')
    # the longest open ground sightline (2 m grid of reachable ground cells, eye 1.6): no whole-map lanes
    pts = [(x, z) for x in range(-30, 31, 2) for z in range(-24, 25, 2) if (cell(x + .1, z + .1), 0.0) in seen]
    long = max((math.dist(a, b), a, b) for a, b in itertools.combinations(pts, 2) if math.dist(a, b) > 30 and visible((a[0] + .3, 1.6, a[1] + .3), (b[0] + .3, 1.6, b[1] + .3)))
    print('longest open ground sightline: %.1f m %s -> %s (map diagonal %.0f m; limit 50)' % (long[0], long[1], long[2], math.hypot(62, 50)))
    if long[0] > 50: fails.append('long sightline')
    boxes = {(e['id'], tuple(round(c, 3) + 0.0 for c in e['box'])) for e in els}
    def partner(e): b = e['box']; return (mirror_id(e['id']), tuple(round(c, 3) + 0.0 for c in (-b[1], -b[0], b[2], b[3], b[4], b[5])))
    asym = [e['id'] for e in els if partner(e) not in boxes]
    print('mirror symmetry: every block has its (-x, z) partner with the mirrored id:', not asym, asym[:5] if asym else '')
    if asym: fails.append('symmetry')
    ids = [e['id'] for e in els]; dup = sorted({i for i in ids if ids.count(i) > 1})
    if dup: fails.append('duplicate ids'); print('duplicate ids:', dup)
    rep = sum(1 for e in els if e['role'] in ('cover', 'structural', 'carried'))
    print('blocks: %d (target <= 350; %d breakable, structural or carried); segments: %d; hazards: %d; pilot columns: %d; spawns: %d ffa + %d + %d team'
          % (len(els), rep, len(SEGS), len(HAZ), len(PILOT), len(ffa), len(teamA), len(teamB)))
    if len(els) > 350: fails.append('block budget')
    def base(e):                                           # the floor a block stands on (the highest top under its middle at or below its bottom)
        b = e['box']; cx, cz = (b[0] + b[1]) / 2, (b[4] + b[5]) / 2
        return max([o['box'][3] for o in els if o is not e and o['box'][0] <= cx <= o['box'][1] and o['box'][4] <= cz <= o['box'][5] and o['box'][3] <= b[2] + 1e-6] or [b[2]])
    mid = [e['id'] for e in els if e['ch'] not in '/.~=' and 1.3 < e['box'][3] - base(e) < 2.2 and e['box'][2] - base(e) < .5]
    print('tops between 1.3 and 2.2 m over their floor (head-glitch band; stairs excepted):', mid or 'none')
    if mid: fails.append('cover heights')
    print('RESULT:', 'all checks pass' if not fails else 'FAILED: ' + ', '.join(fails))
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'polar_m2.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'polar_m2.png')); print('wrote', os.path.join(out, 'polar_m2.json'), 'and polar_m2.png')
    sys.exit(1 if fails else 0)
