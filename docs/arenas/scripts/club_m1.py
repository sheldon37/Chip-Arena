# Substation 128 (club: a nightclub in a decommissioned power substation, 52x40, two levels and a stage) draft painter, 3D
# spawn LOS, floor raster, hazard clearance and sightline checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Mirror symmetric across x = 0: team A holds the west green room,
# team B the east one; both share the DJ stage (north) and the bar (south). Floor tops: the hall 0, the stage 1.0, the
# mezzanines, the VIP balcony over the bar and the lighting catwalk 4.2. The hall's walls stand 12 m (the bounds' ceiling);
# the roof, the clerestory and the city through the south window are presentation above and beyond it.
# usage: python3 docs/arenas/scripts/club_m1.py [--out DIR] [--json PATH]   (stdout: plans + checks; --out writes club_m1.json,
#        draft map data in the UnityMaps.json shape, and club_m1.png, the two level plans, 8 px per metre)
# Ids carry the prefixes the presentation keys its looks and kit on (club-floor-*, club-wall-*, club-stage*, club-speaker-*,
# club-bar-*, club-vip-*, club-mezz-*, club-green-*, ...). The hazards keep to the authored kinds (grid, maglev, vent) and
# their timing sits on a 128 bpm bar grid counted from the arm time (FightAt + 4 s): docs/arenas/CLUB.md.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -26, 26, -20, 20
BOUNDS = {'minX': -26, 'maxX': 26, 'minZ': -20, 'maxZ': 20, 'ceiling': 12, 'killY': -6}
G, S, U = 0.0, 1.0, 4.2                     # tops: the hall floor, the DJ stage, the upper level (mezzanines, VIP, catwalk)
DECK = 3.9                                  # the upper decks' underside (0.3 m slabs): the ground rooms' ceiling
WI = -25.6                                  # the west wall's inner face
ARCH = 8.4                                  # the side walls' arch sills (the galleries' floor beyond)
PIERS = ((-20.0, -18.0), (-12.4, -10.4), (-4.8, -2.8), (2.8, 4.8), (10.4, 12.4), (18.0, 20.0))
RAIL = 1.1; CAP = .42
BPM = 128; BEAT = 60 / BPM; BAR = 4 * BEAT  # 0.46875 s, 1.875 s
ARM = 4.0                                   # hazards arm at FightAt + 4 s (NativeFactoryHazards.ArmDelay)
DANCE = (-9.0, 9.0, -7.5, 7.5)              # the LED dance floor (x0, x1, z0, z1)
els = []; PILOT = []   # els dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp; ch 'r' = see-through rail (never a sight blocker)
SWAP = {'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def v(x, y, z): return {'x': round(x, 4), 'y': round(y, 4), 'z': round(z, 4)}
def mbox(b): x0, x1, y0, y1, z0, z1 = b; return (-x1, -x0, y0, y1, z0, z1)
def pilot(i, b):   # an infinite pilot-only column from just under the top (bots and chips cannot stand or vault there)
    PILOT.append(dict(id=i.replace('club-', 'club-top-', 1), center=v((b[0] + b[1]) / 2, 0, (b[4] + b[5]) / 2), size=v(b[1] - b[0], 0, b[5] - b[4]), bottom=round(b[3] - .1, 3)))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='metal', role='static', hp=0, mirror=True, top=False):
    b = (x0, x1, y0, y1, z0, z1); els.append(dict(id=i, mat=mat, role=role, box=b, ch=ch, hp=hp))
    if top: pilot(i, b)
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=mbox(b), ch=ch, hp=hp))
        if top: pilot(m, mbox(b))
def rail(i, x0, x1, z0, z1, y, mirror=True, top=False): add(i, x0, x1, y, y + RAIL, z0, z1, 'r', top=top, mirror=mirror)
def stair(i, a, b, w0, w1, ya, yb, steps, base, axis='x', mirror=True):
    # a closed flight (each step solid from `base`, no pocket under it) from (a, ya) to (b, yb); along x (w0..w1 is z) or along z
    run = (b - a) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        s0, s1 = sorted((a + run * k, a + run * (k + 1))); y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), s0, s1, base, y1, w0, w1, '/', mirror=mirror)
        else: add('%s-%02d' % (i, k), w0, w1, base, y1, s0, s1, '/', mirror=mirror)

# ---- the floor: the hall slab (foundation, top 0) round the LED dance floor, which is its own block (the presentation lights it)
add('club-floor-w', X0, DANCE[0], -.4, 0, Z0, Z1, '.', mat='foundation')
add('club-floor-n', DANCE[0], DANCE[1], -.4, 0, DANCE[3], Z1, '.', mat='foundation', mirror=False)
add('club-floor-s', DANCE[0], DANCE[1], -.4, 0, Z0, DANCE[2], '.', mat='foundation', mirror=False)
add('club-floor-dance', *DANCE[:2], -.4, 0, *DANCE[2:], '.', mat='foundation', mirror=False)

# ---- the hall's shell: 12 m brick walls (the bounds' ceiling); the south wall stops at 7.5 m under the great window, the north
#      wall is the stage's LED wall between the speaker stacks. North of the side mezzanines the crowd pit opens beyond the barrier.
add('club-wall-w', X0, WI, 0, ARCH, Z0, Z1, '#', mat='stone', top=True)
# the upper wall: brick piers between five tall arches (8.4..12 m, out of reach) onto the crowd galleries beyond the bounds
for k, (z0, z1) in enumerate(PIERS): add('club-wall-w-pier-%d' % k, X0, WI, ARCH, 12, z0, z1, '#', mat='stone', top=True)
add('club-wall-s', WI, -WI, 0, 7.5, Z0, Z0 + .4, '#', mat='stone', mirror=False, top=True)
add('club-wall-n', -10.8, 10.8, 0, 12, Z1 - .4, Z1, 'L', mirror=False, top=True)
add('club-backstage-w-wall-n', WI, -17.0, 0, DECK, Z1 - .4, Z1, '#', mat='stone')
add('club-barrier-w', -17.0, -10.8, 0, RAIL, Z1 - .4, Z1, 'r', top=True)          # the crowd barrier: the crowd dances beyond it
rail('club-rail-mezz-w-n', WI, -17.0, Z1 - .4, Z1, U, top=True)                   # the mezzanine's north rail over the crowd pit

# ---- the upper level (top 4.2): the side mezzanines over the green rooms, the VIP balcony over the bar (two stairwells
#      through it), the lighting catwalk across the hall in front of the stage
add('club-mezz-w', WI, -17.0, DECK, U, Z0 + .4, Z1 - .4, '=')
add('club-vip-w-0', -17.0, -10.3, DECK, U, Z0 + .4, -14.6, '=')
add('club-vip-w-1', -10.3, -4.0, DECK, U, -17.8, -14.6, '=')
add('club-vip-c', -4.0, 4.0, DECK, U, Z0 + .4, -14.6, '=', mirror=False)
add('club-catwalk', -17.0, 17.0, DECK, U, 9.0, 10.8, '=', mirror=False)
# rails: the mezzanine's inner edge (gaps where the grand stair lands and the catwalk joins), the VIP's edge, the stairwells,
# both sides of the catwalk
rail('club-rail-mezz-w-0', -17.1, -17.0, -14.6, 4.3, U); rail('club-rail-mezz-w-1', -17.1, -17.0, 6.1, 9.0, U); rail('club-rail-mezz-w-2', -17.1, -17.0, 10.8, Z1 - .4, U)
rail('club-rail-vip', -17.0, 17.0, -14.7, -14.6, U, mirror=False)
rail('club-rail-well-w-n', -10.3, -4.0, -17.8, -17.7, U); rail('club-rail-well-w-e', -4.0, -3.9, Z0 + .4, -17.8, U)
rail('club-rail-catwalk-s', -17.0, 17.0, 9.0, 9.1, U, mirror=False); rail('club-rail-catwalk-n', -17.0, 17.0, 10.7, 10.8, U, mirror=False)

# ---- the ground rooms under each mezzanine (walls to the deck, 0.3 m): the cloakroom (FFA f), the green room (team A, FFA A1),
#      the backstage room (FFA g); doors open into the cross passages, never toward the hall, so no spawn sees across it
add('club-cloak-w-wall-n', -23.4, -17.3, 0, DECK, -11.3, -11.0, '#', mat='stone'); add('club-cloak-w-wall-e', -17.3, -17.0, 0, DECK, Z0 + .4, -11.0, '#', mat='stone')
add('club-cloak-w-counter', -22.6, -19.4, 0, RAIL, -14.4, -13.6, 'c')
add('club-green-w-wall-s', WI, -19.4, 0, DECK, -6.0, -5.7, '#', mat='stone'); add('club-green-w-wall-n', WI, -19.4, 0, DECK, 5.7, 6.0, '#', mat='stone')
add('club-green-w-wall-e', -17.3, -17.0, 0, DECK, -6.0, 6.0, '#', mat='stone')
add('club-green-w-sofa', WI, -24.8, 0, 1.0, -2.2, 2.2, 'c')
add('club-backstage-w-wall-s', WI, -19.4, 0, DECK, 13.0, 13.3, '#', mat='stone'); add('club-backstage-w-wall-e', -17.3, -17.0, 0, DECK, 13.0, 17.4, '#', mat='stone')
add('club-backstage-w-cases', -24.6, -22.2, 0, 1.2, 18.0, 19.6, 'x', role='cover', hp=150)
# cross passages under the mezzanine: lockers and keg stacks
add('club-kegs-w-s', -24.4, -23.2, 0, 1.2, -9.4, -8.2, 'x', mat='metal', role='cover', hp=150)
add('club-crates-w-n', -23.6, -22.4, 0, 1.2, 9.0, 10.2, 'x', mat='wood', role='cover', hp=34); add('club-crates-w-n-1', -22.4, -21.2, 0, 1.2, 9.0, 10.2, 'x', mat='wood', role='cover', hp=34)

# ---- the VIP lounge on each mezzanine (FFA h): walls 4.2..8.0, an unstandable roof (8.3, out of reach), doors at its inner end
add('club-lounge-w-wall-e', -20.8, -20.5, U, 8.0, -4.0, 4.0, '#', mat='stone')
add('club-lounge-w-wall-n', WI, -22.8, U, 8.0, 3.7, 4.0, '#', mat='stone'); add('club-lounge-w-wall-s', WI, -22.8, U, 8.0, -4.0, -3.7, '#', mat='stone')
add('club-lounge-w-roof', WI, -20.5, 8.0, 8.3, -4.0, 4.0, 'A', mat='stone', top=True)
add('club-lounge-w-sofa', WI, -24.8, U, U + 1.0, -2.0, 2.0, 'c')
# mezzanine cover: lounge sofas (1.0) and high tables at the rail
add('club-sofa-mezz-w-s', -24.0, -21.0, U, U + 1.0, -11.0, -10.2, 'c'); add('club-sofa-mezz-w-n', -24.0, -21.0, U, U + 1.0, 10.2, 11.0, 'c')
add('club-planter-mezz-w', -20.2, -19.0, U, U + 1.1, 14.0, 16.4, 'c')

# ---- the grand stairs (hall to mezzanine) beside each green room: 21 risers of 0.2 x 0.3 m, 1.8 m wide, a landing at the top
stair('club-stair-grand-w', -2.0, 4.3, -17.0, -15.2, G, U, 21, G, axis='z')
add('club-stair-grand-w-landing', -17.0, -15.2, DECK, U, 4.3, 6.1, '/')
rail('club-rail-grand-w-e', -15.3, -15.2, 4.3, 6.1, U); rail('club-rail-grand-w-n', -17.0, -15.2, 6.0, 6.1, U)

# ---- the bar (south): the counter (half cover), the back bar and the pillar under the VIP, a stair up through each stairwell
add('club-bar-counter', -10.0, 10.0, 0, RAIL, -13.4, -12.4, 'b', mirror=False)
add('club-bar-pillar', -1.0, 1.0, 0, DECK, -17.6, -15.6, 'P', mat='stone', mirror=False)
add('club-backbar', -4.0, 4.0, 0, DECK, Z0 + .4, -19.0, 'b', mirror=False)
stair('club-stair-bar-w', -4.0, -10.3, Z0 + .4, -17.8, G, U, 21, G)
add('club-vip-booth', -2.6, 2.6, U, U + RAIL, Z0 + .4, -18.6, 'c', mirror=False)
add('club-vip-sofa-w', -15.6, -12.0, U, U + 1.0, -19.4, -18.6, 'c')
add('club-vip-table-w', -13.4, -12.2, U, U + 1.0, -16.6, -15.4, 'x', mat='wood', role='cover', hp=34)

# ---- the stage (north): the deck at 1.0 with front steps, the DJ booth, the speaker stacks beside it (the bass drop), back
#      steps behind each stack, the stage's LED wall (club-wall-n)
add('club-stage', -8.0, 8.0, 0, S, 13.0, Z1 - .4, 'S', mirror=False)
for k in range(4): add('club-stage-step-%02d' % k, -3.0, 3.0, 0, .2 * (k + 1), 11.8 + .3 * k, 12.1 + .3 * k, '/', mirror=False)
for k in range(4): add('club-stage-back-w-%02d' % k, -9.2 + .3 * k, -8.9 + .3 * k, 0, .2 * (k + 1), 17.8, Z1 - .4, '/')
add('club-speaker-w', -10.8, -8.0, 0, 4.6, 13.5, 17.8, 'K', top=True)
add('club-dj-booth', -2.6, 2.6, S, S + 1.2, 15.2, 16.4, 'D', mirror=False)

# ---- the hall: truss towers at the dance floor's corners (solid, out of reach), go-go podiums, booths, the cocktail islands,
#      the catwalk's columns, road cases by the crowd barrier
add('club-tower-w-s', -10.2, -9.4, 0, 10.0, -8.6, -7.8, 'T', top=True); add('club-tower-w-n', -10.2, -9.4, 0, 10.0, 7.8, 8.6, 'T', top=True)
add('club-podium-w', -12.0, -10.6, 0, 1.0, -.7, .7, 'p')
add('club-booth-w-s', -14.4, -11.2, 0, RAIL, -11.8, -11.5, 'c'); add('club-booth-w-s-arm', -14.4, -14.1, 0, RAIL, -11.5, -9.6, 'c')
add('club-booth-w-n', -14.4, -11.2, 0, RAIL, 11.5, 11.8, 'c'); add('club-booth-w-n-arm', -14.4, -14.1, 0, RAIL, 9.6, 11.5, 'c')
add('club-table-w-s', -13.4, -12.2, 0, 1.0, -10.8, -9.8, 'x', mat='wood', role='cover', hp=34); add('club-table-w-n', -13.4, -12.2, 0, 1.0, 9.8, 10.8, 'x', mat='wood', role='cover', hp=34)
add('club-column-cat-w', -6.3, -5.7, 0, DECK, 9.6, 10.2, 'P', mat='stone')
# the hall's cast-iron columns (the substation's crane rails once ran on them): full cover in the side lanes, to the roof
add('club-column-w-s', -15.6, -14.8, 0, 12.0, -8.0, -7.2, 'P', mat='stone', top=True); add('club-column-w-n', -15.6, -14.8, 0, 12.0, 7.2, 8.0, 'P', mat='stone', top=True)
add('club-case-w-0', -15.4, -14.2, 0, 1.2, 15.2, 16.4, 'x', role='cover', hp=150); add('club-case-w-1', -15.4, -14.2, 0, 1.2, 16.4, 17.6, 'x', role='cover', hp=150)
add('club-case-w-2', -13.0, -11.8, 0, 1.2, 18.2, 19.4, 'x', role='cover', hp=150)
add('club-island-w', -14.6, -13.4, 0, RAIL, -4.6, -1.0, 'c')
# the video cube hung over the dance floor's centre (5.2..8.2 m, out of reach): the landmark, and a screen across the upper
# level's diagonals over the hall
add('club-cube', -2.0, 2.0, 5.2, 8.2, -2.0, 2.0, 'V', mirror=False, top=True)

# ---- hazards (UnityMaps.json `hazards[]`), on a 128 bpm bar grid from the arm time (FightAt + 4 s), a 16-bar phrase (30 s):
#      bar 0 the DROP (speaker vents), bars 3 and 11 the charged floor's X (corners and centre), bars 7 and 15 its + (the edges),
#      bar 8 the laser wall's pass. Grid and maglev clocks count from FightAt (first), the vent from the arm time (start).
def hz(**k):
    d = dict(id='', kind='', center=v(0, 0, 0), size=v(0, 0, 0), axis=v(0, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), first=0, period=0, offset=0, warn=0,
             danger=0, speed=0, length=0, charge=0, live=0, tick=0, damage=0, radius=0, start=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
GRID_CHARGE, GRID_LIVE, GRID_PERIOD = 6 * BEAT, BAR, 8 * BAR          # 2.8125 s charge, one bar live, every 8 bars (15 s)
def grid_first(bar):   # FightAt-relative `first` that puts a grid's live window on `bar` (counted from the arm time), first >= 0
    f = ARM + bar * BAR - (GRID_PERIOD - GRID_LIVE - 1)
    while f < 0: f += GRID_PERIOD
    return round(f, 4)
COLS = (('w', -9.0, -3.0), ('c', -3.0, 3.0), ('e', 3.0, 9.0)); ROWS = (('s', -7.5, -2.5), ('c', -2.5, 2.5), ('n', 2.5, 7.5))
HAZ = []
for cn, x0, x1 in COLS:
    for rn, z0, z1 in ROWS:
        cross = (cn == 'c') != (rn == 'c')                              # the edges: the "+" pattern; corners and centre: the "X"
        HAZ.append(hz(id='club-live-%s-%s' % (cn, rn), kind='grid', center=v((x0 + x1) / 2, .3, (z0 + z1) / 2), size=v(x1 - x0, .6, z1 - z0), axis=v(0, 0, 1),
                      first=grid_first(7 if cross else 3), period=GRID_PERIOD, offset=0, charge=GRID_CHARGE, live=GRID_LIVE, tick=.25, damage=10))
LASER = hz(id='club-laser', kind='maglev', center=v(0, .35, 0), size=v(DANCE[1] - DANCE[0], .6, DANCE[3] - DANCE[2]), axis=v(1, 0, 0),
           first=ARM + 8 * BAR, period=16 * BAR, warn=4 * BAR, danger=2 * BAR, speed=8.0, length=.3, lethal=True)
HAZ.append(LASER)
DROP_W = hz(id='club-drop-w', kind='vent', a=v(-9.4, 0, 11.5), size=v(2.8, 2.6, 4.0), axis=v(0, 0, -1), start=.75, period=16 * BAR, offset=0,
            warn=2 * BAR, dwell=.75, damage=6, tick=.25, push=7.0, pushMode='shove')
DROP_E = dict(DROP_W, id='club-drop-e', a=v(9.4, 0, 11.5))
HAZ += [DROP_W, DROP_E]

# ---- spawns (x, z, floor top); yaw from +z toward +x, facing the hall's centre. Team A in the west green room, B the mirror.
teamA = [(-23.0, -3.4, G), (-23.0, 3.4, G), (-19.4, -1.8, G), (-19.4, 1.8, G)]
teamB = [(-x, z, f) for x, z, f in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (-22.4, -15.8, G), (22.4, -15.8, G), (-22.2, 16.4, G), (22.2, 16.4, G), (-23.4, 0.0, U), (23.4, 0.0, U)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

def hazard_rects():
    """2D footprints of every hazard volume (x0, x1, z0, z1, name, y0, y1): what spawns keep 3 m from."""
    out = []
    for h in HAZ:
        if h['kind'] in ('grid', 'maglev'):
            c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, h['id'], c['y'] - s['y'] / 2, c['y'] + s['y'] / 2))
        else:
            a, s = h['a'], h['size']; out.append((a['x'] - s['x'] / 2, a['x'] + s['x'] / 2, a['z'] - s['z'] / 2, a['z'] + s['z'] / 2, h['id'], a['y'], a['y'] + s['y']))
    return out

# ---- 3D sight lines: eyes 1.2/1.6/1.9 m over each spawn's own floor; a box blocks where the segment passes through it in x, y and z.
#      Boxes topping out under 2.0 m above the lower floor do not count (half cover does not hide a standing pilot); rails never do.
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
SOLID = [e for e in els if e['ch'] != 'r']
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

# ---- 3D floor raster: 0.25 m cells; a cell is standable on a block top if nothing sits on that top at the cell centre, there is
#      1.8 m headroom and no block intrudes the 0.42 m capsule ring between top+0.45 and top+1.8; neighbours connect when
#      |dy| <= 0.25 (the 0.2 m risers). Flood fill from spawn A1.
CELL = .25; NX, NZ = int((X1 - X0) / CELL), int((Z1 - Z0) / CELL)
def raster():
    cols = {}
    for e in els:
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
            if any(inn and b0 < t + .45 and b1 > t + .01 for b0, b1, inn in lst): continue           # something sits on this top
            if any(b0 < t + 1.8 and b1 > t + .45 for b0, b1, inn in lst): continue                  # headroom / capsule ring
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
def level(t): return 'hall' if abs(t - G) < .05 else 'stage' if abs(t - S) < .05 else 'upper' if abs(t - U) < .05 else 'other'
def at(surf, seen, p):
    c = (int((p[0] - X0) / CELL), int((p[1] - Z0) / CELL)); return any((c, t) in seen for t in surf.get(c, ()) if abs(t - p[2]) < .05)

# ---- longest sight lines per level (design check): eyes 1.6 m over walkable sample points every 2 m
def sightlines(surf, seen):
    out = {}
    for lv, y in (('hall', G), ('upper', U)):
        pts = sorted({(round(X0 + (i + .5) * CELL), round(Z0 + (j + .5) * CELL)) for (i, j), t in seen if abs(t - y) < .05 and i % 8 == 4 and j % 8 == 4})
        blocks = [e for e in SOLID if e['box'][3] >= y + 2.0 and e['box'][2] < y + 1.9]
        best, diag = (0, None, None), (0, None, None)
        for p, q in itertools.combinations(pts, 2):
            d = math.dist(p, q)
            if d < 30 or (d <= best[0] and (abs(p[1] - q[1]) < 14 or d <= diag[0])): continue
            if visible((p[0], y + 1.6, p[1]), (q[0], y + 1.6, q[1]), y, blocks):
                if d > best[0]: best = (d, p, q)
                if abs(p[1] - q[1]) >= 14 and abs(p[0] - q[0]) >= 14 and d > diag[0]: diag = (d, p, q)
        out[lv] = (best, diag)
    return out

def flights(surf, seen):
    """Each flight on its own: flood from its bottom step's cells through its own steps only; ok if the top step is reached and
    both end steps are reachable from A1 (so the flight joins its two levels)."""
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
    W = X1 - X0; R = (Z1 - Z0) // 2; outs = []
    hz_rects = hazard_rects()
    for lv, y, ylo, yhi in (('hall (top 0) and stage (top 1.0)', G, -1, 3.85), ('upper level (top 4.2): mezzanines, VIP balcony, catwalk; lounges', U, 3.85, 99)):
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
            if ylo <= t < yhi: put(X0 + (i + .5) * CELL, Z0 + (j + .5) * CELL, '.')
        for x0, x1, z0, z1, name, h0, h1 in hz_rects:
            if ylo <= h0 < yhi and 'laser' not in name: rect(x0, x1, z0, z1, 'v' if 'drop' in name else ('x' if (('-c-' in name) != name.endswith('-c')) else '~'))
        for e in sorted(els, key=lambda e: e['ch'] == 'r'):
            b = e['box']
            if e['ch'] in '.=' or not (ylo <= b[2] < yhi): continue
            rect(b[0], b[1], b[4], b[5], '+' if e['ch'] == 'r' else e['ch'])
        for nm, p in zip(ffa_names, ffa):
            if abs(p[2] - y) < .05: put(p[0], p[1], nm)
        for k, p in enumerate(teamA + teamB):
            if abs(p[2] - y) < .05 and p not in ffa: put(p[0], p[1], str(k + 1))
        outs.append(lv + ':\n' + ' ' * 10 + 'x=-26' + ' ' * 21 + 'x=0' + ' ' * 21 + 'x=+25')
        for r in range(R): z = Z1 - 2 * r; outs.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(outs)

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), 'size': v(x1 - x0, y1 - y0, z1 - z0),
                'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], p[2] + 1.1, p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'club', 'name': 'Substation 128', 'tag': 'Substation 128, peak hour. The floor is live; the drop is coming.', 'theme': 'club',
                      'music': 'club-theme', 'ambience': 'club-crowd', 'look': 'club-substation', 'sky': 0x07050F, 'floor': 0x1A1626, 'accent': 0xFF3DCF,
                      'rotation': True, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ,
                      'nav': {'nodes': [], 'links': []}}]}

def png(path, surf, seen, scale=8):
    PW, GAP = (X1 - X0) * scale, 16; PH = (Z1 - Z0) * scale; W, H = 2 * PW + GAP, PH; img = bytearray(b'\x0c\x08\x18' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        ox = k * (PW + GAP); i0, i1 = int((x0 - X0) * scale), int(math.ceil((x1 - X0) * scale)); j0, j1 = int((Z1 - z1) * scale), int(math.ceil((Z1 - z0) * scale))
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(PW, i1)):
                o = (j * W + ox + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    for k in range(2): fill(k, X0, X1, Z0, Z1, (20, 14, 34))
    for (i, j), t in surf.items():
        for u in t:
            k = 1 if u > 3.85 else 0; c = (70, 66, 86) if u < .5 else (104, 84, 120) if u < 3.85 else (96, 100, 116)
            fill(k, X0 + i * CELL, X0 + (i + 1) * CELL, Z0 + j * CELL, Z0 + (j + 1) * CELL, c if ((i, j), u) in seen else (120, 50, 50))
    fill(0, *DANCE, (40, 20, 70), .5)
    col = {'#': (150, 130, 120), 'A': (170, 150, 110), 'L': (60, 200, 255), 'S': (120, 90, 150), 'D': (230, 80, 200), 'K': (40, 40, 48), 'T': (200, 200, 210),
           'P': (130, 135, 145), 'b': (200, 140, 60), 'c': (150, 60, 120), 'p': (255, 80, 200), '/': (185, 185, 190), 'x': (170, 130, 90), 'r': (250, 210, 60)}
    for e in sorted(els, key=lambda e: (e['ch'] == 'r', e['box'][3])):
        b = e['box']
        if e['ch'] in '.=': continue
        k = 0 if b[2] < 3.85 else 1
        fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)))
    for p in PILOT:
        c, z = p['center'], p['size']; fill(1, c['x'] - z['x'] / 2 - .05, c['x'] + z['x'] / 2 + .05, c['z'] - z['z'] / 2 - .05, c['z'] + z['z'] / 2 + .05, (230, 60, 200), .45)
    for x0, x1, z0, z1, name, h0, h1 in hazard_rects():
        if 'laser' in name: continue
        fill(0, x0, x1, z0, z1, (180, 210, 240) if 'drop' in name else (255, 120, 20) if (('-c-' in name) != name.endswith('-c')) else (255, 220, 40), .35)
    for idx, p in enumerate(teamA + teamB):
        k = 1 if p[2] > 3.85 else 0; fill(k, p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if idx < 4 else (255, 90, 120))
    for p in ffa[2:]:
        k = 1 if p[2] > 3.85 else 0; fill(k, p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (245, 245, 245))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

def grid_state(h, t):
    """The Cyber City grid clock (NativeCyberCityHazards.Grid, round 1: no offset): 'idle', 'warn', 'live' or 'clear' at FightAt-relative t."""
    u = t - h['first']
    if u < 0: return 'idle'
    c = u % h['period']; idle = h['period'] - h['charge'] - h['live'] - 1
    return 'idle' if c < idle else 'warn' if c < idle + h['charge'] else 'live' if c < idle + h['charge'] + h['live'] else 'clear'

if __name__ == '__main__':
    surf, seen = raster()
    print(paint(surf, seen))
    print('legend: . walkable (reached from A1)  + rail, barrier  / stair, steps, landing  # wall  A lounge roof (unwalkable)  L the LED wall'
          '\n        S stage  D DJ booth  K speaker stack  T truss tower  P pillar, column  b bar  c sofa, booth, counter (half cover)  p podium'
          '\n        x cases, crates, kegs, tables (breakable)  ~ charged floor (X: corners and centre)  x charged floor (+: edges)  v drop vent'
          '\n        A1 B1 f F g G h H FFA spawns, 1-8 team spawns; the laser wall sweeps the whole dance floor (x +-9, z +-7.5)')
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    d3 = lambda p, q: math.dist((p[0], p[2], p[1]), (q[0], q[2], q[1]))
    print('min FFA spacing %.1f m (plan; %.1f m 3D); min A-B spacing %.1f m' % (min(math.dist(p[:2], q[:2]) for p, q in itertools.combinations(ffa, 2)),
          min(d3(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)))
    sp = ffa + teamA[1:] + teamB[1:]
    worst = min((dist_rect(p, r[:4]), r[4], p[:2]) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume (plan footprint): %.1f m (%s, spawn %s)' % worst); bad += worst[0] < 3
    clr = min((min(dist_rect(p[:2], (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < p[2] + 1.9 and e['box'][3] > p[2] + .3), p[:2]) for p in sp)
    print('closest spawn to any block at its floor: %.2f m at %s' % clr); bad += clr[0] < CAP
    inb = all(abs(p[0]) <= 26 - CAP and abs(p[1]) <= 20 - CAP for p in sp); print('all spawns inside the bounds minus the capsule:', inb); bad += not inb
    lv = {}
    for c, ts in surf.items():
        for t in ts: k = level(t); lv.setdefault(k, [0, 0]); lv[k][1] += 1; lv[k][0] += (c, t) in seen
    print('floor raster (0.25 m cells) reachable on foot from A1: ' + '; '.join('%s %d of %d' % (k, lv[k][0], lv[k][1]) for k in ('hall', 'stage', 'upper', 'other') if k in lv) + ' (other: treads, cover tops; the rest are walled or capped tops)')
    reach = {n: at(surf, seen, p) for n, p in list(zip(ffa_names, ffa)) + A + B}
    print('every spawn reachable:', all(reach.values()), '' if all(reach.values()) else [n for n, r in reach.items() if not r]); bad += not all(reach.values())
    levels = all(lv.get(k, [0])[0] > 0 for k in ('hall', 'stage', 'upper')); print('every level reachable:', levels); bad += not levels
    for k, ((d, p, q), (dd, pp, qq)) in sightlines(surf, seen).items():
        print('longest clear sightline, %s (eyes 1.6 m, 2 m sample grid): %.1f m %s-%s; longest diagonal (|dx|,|dz| >= 14, fails at 60 m): %s' % (k, d, p, q, '%.1f m %s-%s' % (dd, pp, qq) if pp else 'none of 30 m or more'))
        bad += dd >= 60
    fl = flights(surf, seen); okf = all(r[2] for r in fl)
    print('stairs: %d flights, each walkable bottom to top on its own and joined to both levels: %s (%s)'
          % (len(fl), okf, ', '.join('%s %s' % (n.replace('club-', ''), 'ok' if r else 'BROKEN') for n, k, r in fl))); bad += not okf or len(fl) != 7
    key = lambda b: tuple(round(c, 3) for c in b)
    sym = sorted(key(e['box']) for e in els) == sorted(key(mbox(e['box'])) for e in els)
    psym = sorted((round(p['center']['x'], 3), round(p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT) == \
           sorted((round(-p['center']['x'], 3), round(p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT)
    ssym = sorted(teamB) == sorted((-x, z, f) for x, z, f in teamA) and sorted(ffa) == sorted((-x, z, f) for x, z, f in ffa)
    hsym = sorted((round(h['center']['x'] + h['a']['x'], 3), round(h['center']['z'] + h['a']['z'], 3)) for h in HAZ) == sorted((round(-(h['center']['x'] + h['a']['x']), 3), round(h['center']['z'] + h['a']['z'], 3)) for h in HAZ)
    print('mirror symmetric (x,z)->(-x,z): blocks %s, pilot blocks %s, spawns %s, hazards %s' % (sym, psym, ssym, hsym)); bad += not (sym and psym and ssym and hsym)
    tops = [e['id'] for e in els if e['ch'] not in 'r/' and any(1.3 < e['box'][3] - f < 2.2 for f in (G, S, U) if e['box'][2] >= f - .01 and e['box'][2] <= f + .01)]
    print('tops in the 1.3-2.2 m band over their own floor (head-glitch ambiguity): %s' % (tops or 'none')); bad += len(tops)
    print('pilot blocks: %d (walls, the LED wall, speaker stacks, truss towers, lounge roofs, the crowd barrier and rail over the pit)' % len(PILOT))
    # ---- hazard rhythm: the phrase on the bar grid, telegraph lengths, the first 4 s idle
    print('128 bpm: beat %.5f s, bar %.4f s; the phrase is 16 bars (%.1f s) from the arm time (FightAt + %.0f s)' % (BEAT, BAR, 16 * BAR, ARM))
    for h in HAZ:
        if h['kind'] == 'grid':
            live = h['first'] + h['period'] - h['live'] - 1
            print('   %-18s grid  charge %.4f s, live %.4f s, every %.2f s; first live at FightAt + %.4f s = bar %.2f; %d x %d HP ticks per full window'
                  % (h['id'], h['charge'], h['live'], h['period'], live, (live - ARM) / BAR, round(h['live'] / h['tick']), h['damage']))
        elif h['kind'] == 'maglev':
            print('   %-18s maglev warn %.4f s (danger %.4f s), pass %.2f s at %.1f m/s, every %.2f s; first pass at FightAt + %.4f s = bar %.2f'
                  % (h['id'], h['warn'], h['danger'], (h['size']['x'] + h['length']) / h['speed'], h['speed'], h['period'], h['first'], (h['first'] - ARM) / BAR))
        else:
            burst = ARM + h['start'] + h['offset'] + h['period'] - h['dwell']
            print('   %-18s vent  warn %.4f s, burst %.2f s (%d ticks x %d HP, push %.1f m/s each along %s), every %.2f s; first burst at FightAt + %.4f s = bar %.2f'
                  % (h['id'], h['warn'], h['dwell'], round(h['dwell'] / h['tick']), h['damage'], h['push'], (h['axis']['x'], h['axis']['z']), h['period'], burst, (burst - ARM - h['dwell']) / BAR + h['dwell'] / BAR))
    okg = all(h['charge'] >= 2.5 and h['charge'] + h['live'] + 1 <= h['period'] and h['first'] >= 0 and not h['lethal'] for h in HAZ if h['kind'] == 'grid')
    okm = all(h['lethal'] and h['warn'] >= 6 and h['danger'] >= 3 and h['warn'] > h['danger'] and (h['size']['x'] + h['length']) / h['speed'] + 2 + h['warn'] < h['period'] and h['first'] - h['warn'] >= ARM for h in HAZ if h['kind'] == 'maglev')
    okv = all(h['warn'] >= .8 and h['warn'] + h['dwell'] <= h['period'] and h['start'] >= 0 and not h['lethal'] and h['tick'] > 0 for h in HAZ if h['kind'] == 'vent')
    # the grid groups never live together, never live while the laser passes, and the floor is never all live at once
    clash = []
    for t10 in range(0, 6000):
        t = t10 / 10; lives = [h['id'] for h in HAZ if h['kind'] == 'grid' and grid_state(h, t) == 'live']
        u = t - LASER['first']; k = math.floor(u / LASER['period']) if u >= 0 else -1; since = u - k * LASER['period'] if k >= 0 else -1
        laser = 0 <= since < (LASER['size']['x'] + LASER['length']) / LASER['speed']
        if len(lives) == 9 or (laser and lives) or t < 4 and lives: clash.append(t)
    print('grids (charge >= 2.5 s, first >= 0, non-lethal): %s; laser (warn >= 6 s, danger >= 3 s, fits the period, no warning before the arm time): %s; vents (warn >= 0.8 s): %s; no clash over 600 s: %s'
          % (okg, okm, okv, not clash)); bad += not (okg and okm and okv) or bool(clash)
    jump = 8.5; grav = 23.0; top = LASER['center']['y'] + LASER['size']['y'] / 2
    air = (jump + math.sqrt(jump * jump - 2 * grav * top)) / grav - (jump - math.sqrt(jump * jump - 2 * grav * top)) / grav
    passing = (LASER['length'] + 2 * CAP) / LASER['speed']
    print('the laser wall: beams to %.2f m over the floor; a jump keeps the feet over them for %.2f s, the wall takes %.2f s to pass a pilot: a %.2f s window'
          % (top, air, passing, air - passing)); bad += air - passing < .3
    ids = [e['id'] for e in els]; dup = len(ids) - len(set(ids)); print('duplicate block ids: %d' % dup); bad += dup > 0
    props = sum(1 for e in els if e['role'] != 'static')
    print('blocks: %d; replicated props at round start: %d; hazards: %d; pilot blocks: %d; spawns: %d FFA, %d + %d team' % (len(els), props, len(HAZ), len(PILOT), len(ffa), len(teamA), len(teamB)))
    bad += len(els) > 350 or props > 200
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'club_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'club_m1.png'), surf, seen); print('wrote', os.path.join(out, 'club_m1.json'), 'and club_m1.png')
    print('checks:', 'PASS' if not bad else 'FAIL (%d)' % bad)
    sys.exit(1 if bad else 0)
