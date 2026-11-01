# Calibration Bay M1 (`training`): the Training Room, a 96x56 m sandbox hangar for testing weapons, abilities, movement and
# hazards (not a PvP map). Draft painter, range sightline, 3D reachability, hazard clearance and hazard-bay enclosure checks.
# Coordinates: x east, z north, y up, metres; Unity frame. NOT symmetric (a sandbox): no mirrored partners, no mirror_id.
# usage: python3 docs/arenas/scripts/training_m1.py [--out DIR] [--json PATH]   (stdout: plan + checks; --json writes the draft only; --out writes
#        training_m1.json, draft map data in the UnityMaps.json shape, and training_m1.png, a top-down plan, 8 px per metre)
# A draft for Astra (docs/arenas/NEW-STAGES.md), NOT wired into the game. Ids carry the prefixes the presentation keys its looks and
# dressing on (training-wall-*, -kerb-*, -stair-*, -deck-*, -rail-*, -step-*, -platform-*, -cover-*, -crate-*, -hazard-*); the menu entry,
# the sandbox mode, the dummies and the hazard clock are Astra's. Look-only: the 1 m floor grid, the firing line, distance boards, sprint marks.
import math, json, itertools, sys, os, zlib, struct
from collections import defaultdict
X0, X1, Z0, Z1 = -50, 50, -30, 30           # slab; the hall interior (and the play bounds) is x +-48, z +-28 inside the 2 m shell
BOUNDS = {'minX': -48, 'maxX': 48, 'minZ': -28, 'maxZ': 28, 'ceiling': 14, 'killY': -6}
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp
STAIRS = []  # (id, riser, tread, width) of every flight, for the measurement checks
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0):
    els.append(dict(id=i, mat=mat, role=role, box=tuple(round(c, 3) for c in (x0, x1, y0, y1, z0, z1)), ch=ch, hp=hp))
def wall_x(i, x0, x1, y0, y1, z0, z1, openings, ch, **k):
    cuts = [x0] + [c for o in sorted(openings) for c in o] + [x1]
    for j in range(0, len(cuts), 2):
        if cuts[j + 1] > cuts[j]: add('%s-%d' % (i, j // 2), cuts[j], cuts[j + 1], y0, y1, z0, z1, ch, **k)
def wall_z(i, x0, x1, y0, y1, z0, z1, openings, ch, **k):
    cuts = [z0] + [c for o in sorted(openings) for c in o] + [z1]
    for j in range(0, len(cuts), 2):
        if cuts[j + 1] > cuts[j]: add('%s-%d' % (i, j // 2), x0, x1, y0, y1, cuts[j], cuts[j + 1], ch, **k)
def stair(i, a, b, w0, w1, ya, yb, steps, axis='x', base=None):
    # a flight of solid steps from (a, ya) to (b, yb) along x (w0..w1 is its z span) or along z (w0..w1 is its x span); solid from `base`
    # (default ya: the ground, or the deck the flight stands on)
    run = (b - a) / steps; rise = (yb - ya) / steps; base = ya if base is None else base
    STAIRS.append((i, round(rise, 3), round(abs(run), 3), round(w1 - w0, 3)))
    for k in range(steps):
        p, q = sorted((a + run * k, a + run * (k + 1)))
        if axis == 'x': add('%s-%02d' % (i, k), p, q, base, ya + rise * (k + 1), w0, w1, '/', mat='metal')
        else: add('%s-%02d' % (i, k), w0, w1, base, ya + rise * (k + 1), p, q, '/', mat='metal')

# ---- shell: 2 m walls to the 14 m roof (indestructible; the wall tops and the roof are unstandable: pilot blocks)
add('training-wall-n', -50, 50, 0, 14, 28, 30, '#')
add('training-wall-s', -50, 50, 0, 14, -30, -28, '#')
add('training-wall-w', -50, -48, 0, 14, -28, 28, '#')
add('training-wall-e', 48, 50, 0, 14, -28, 28, '#')
add('training-roof', -50, 50, 14, 14.4, -30, 30, 'R', mat='metal')
FLOOR = dict(id='training-floor', mat='foundation', role='static', box=(-50, 50, -.5, 0, -30, 30), ch='.', hp=0)

# ---- firing range (north strip): firing line x = -42 facing +x; four 3.5 m lanes split by 0.5 x 0.3 m kerbs; pads at 10..80 m; a 1.0 m
#      safety barrier on the south side (the range is entered from the firing-line apron x -48..-40); a 6 m bullet trap in front of the east wall
FIRE_X = -42.0
LANES = [(12.0, 15.5), (15.8, 19.3), (19.6, 23.1), (23.4, 26.9)]
DISTS = (10, 20, 30, 45, 60, 80)
STRAFE_GAP = (-23.5, -20.5)                                   # the lane 1/2 kerb opens where lane 1's strafing rail crosses it
for k in range(3):
    wall_x('training-kerb-range-%d' % (k + 1), FIRE_X, 44, 0, .5, LANES[k][1], LANES[k + 1][0], [STRAFE_GAP] if k == 0 else [], 'k')
add('training-barrier-range', -40, 45, 0, 1.0, 9.4, 9.7, 'b')
add('training-backstop-range', 45, 48, 0, 6.0, 9.4, 28, 'B')
TRAINING_PADS = []   # proposed `trainingPads[]`: pos is the dummy's capsule centre (floor + 1.1, as spawns); distance: metres from the firing
                     # line (range) or from the ring centre (melee); yaw from +z toward +x, as spawns
def pad(i, x, z, yaw, kind, dist, **extra): TRAINING_PADS.append(dict(id=i, pos=v(round(x, 3), 1.1, round(z, 3)), yaw=yaw, kind=kind, distance=dist, **extra))
for L, (za, zb) in enumerate(LANES):
    zc = (za + zb) / 2
    for d in DISTS:
        x, z = FIRE_X + d, zc
        if L == 0 and d == 20:                                   # lane 1's 20 m pad strafes +-3 m along z (Astra moves the dummy)
            pad('training-pad-l1-20', x, z, 270, 'strafe', d, rail={'a': v(x, 1.1, round(z - 3, 3)), 'b': v(x, 1.1, round(z + 3, 3))}); continue
        if L == 1 and d == 20: z = zc + .45                      # lane 2's 20 m pad sits 1.25 m clear of the rail's north end
        pad('training-pad-l%d-%d' % (L + 1, d), x, z, 270, 'static', d)

# ---- melee ring (centre): a 14 m octagon (apothem 7) of 0.3 m kerb, openings 2.4 m in the N/E/S/W sides; the diagonals are stepped
#      squares (0.56 m, 8 per side: the collision stays within 0.25 m of a 0.3 m kerb drawn on the octagon); four melee pads inside
RING = (0.0, 0.0); APO = 7.0; HALF = APO * math.tan(math.pi / 8); KW = .3; RING_OPEN = 2.4; SQ = .56
for nm, ax, az in (('n', 0, 1), ('s', 0, -1), ('e', 1, 0), ('w', -1, 0)):
    for k, (a, b) in enumerate(((-HALF, -RING_OPEN / 2), (RING_OPEN / 2, HALF))):
        if az: add('training-kerb-ring-%s-%d' % (nm, k), RING[0] + a, RING[0] + b, 0, .3, RING[1] + az * APO - KW / 2, RING[1] + az * APO + KW / 2, 'o')
        else: add('training-kerb-ring-%s-%d' % (nm, k), RING[0] + ax * APO - KW / 2, RING[0] + ax * APO + KW / 2, 0, .3, RING[1] + a, RING[1] + b, 'o')
for nm, sx, sz in (('ne', 1, 1), ('se', 1, -1), ('nw', -1, 1), ('sw', -1, -1)):
    for k in range(8):
        t = (k + .5) / 8; cx, cz = RING[0] + sx * (HALF + (APO - HALF) * t), RING[1] + sz * (APO - (APO - HALF) * t)
        add('training-kerb-ring-%s-%d' % (nm, k), cx - SQ / 2, cx + SQ / 2, 0, .3, cz - SQ / 2, cz + SQ / 2, 'o')
def yaw_to(p, c): return round((math.degrees(math.atan2(c[0] - p[0], c[1] - p[1])) + 360) % 360, 3)
for nm, sx, sz in (('ne', 1, 1), ('se', 1, -1), ('sw', -1, -1), ('nw', -1, 1)):
    p = (RING[0] + 2.5 * sx, RING[1] + 2.5 * sz); pad('training-pad-melee-' + nm, p[0], p[1], yaw_to(p, RING), 'melee', round(math.dist(p, RING), 2))

# ---- spawn pad (0, -20) facing north, with the loadout terminal (1.1 m console) 3.3 m ahead of the front row
SPAWN_PAD = (0.0, -20.0)
add('training-terminal-loadout', -1.2, 1.2, 0, 1.1, -15.2, -14.4, 'T', mat='metal')
teamA = [(-1.5, -18.5), (-4.5, -18.5), (-1.5, -21.5), (-4.5, -21.5)]
teamB = [(1.5, -18.5), (4.5, -18.5), (1.5, -21.5), (4.5, -21.5)]
ffa_names = ['A1', 'A2', 'A3', 'A4', 'B1', 'B2', 'B3', 'B4']
ffa = teamA + teamB                                          # co-op testers spawn together: FFA uses the same eight points
SPAWN_YAW = 0.0

# ---- movement course (south-west)
# steps: eight separate 2.2 m pads at the test heights, 2.0 m apart (walk-around on every side)
STEPS = (0.2, 0.45, 0.65, 1.0, 1.5, 2.0, 2.5, 3.0); STEP_Z = (-19.0, -16.8)
for k, h in enumerate(STEPS):
    x0 = -43.6 + 4.2 * k; add('training-step-%03d' % round(h * 100), x0, x0 + 2.2, 0, h, *STEP_Z, 's')
# gap run: 2.0 m platforms (a 4 m run-up first, then 2.5 m) with gaps of 2, 3, 4, 5, 6 m; a standard stair from the north to the first
GAP_Z = (-27.0, -24.0); GAPS = (2, 3, 4, 5, 6); PLAT = [(-46.0, -42.0)]
for g in GAPS: PLAT.append((PLAT[-1][1] + g, PLAT[-1][1] + g + 2.5))
for k, (a, b) in enumerate(PLAT): add('training-platform-gap-%d' % (k + 1), a, b, 0, 2.0, *GAP_Z, 'P')
stair('training-stair-gap', -21.0, -24.0, -46.0, -44.0, 0, 2.0, 10, axis='z')
# tower: ground (0), deck 1 (3.6, x -46..-38) and deck 2 (7.2, over deck 1 plus a 2 m east wing over flight 1's head); flight 1 climbs west
# from the ground to deck 1's east edge, flight 2 stands on deck 1's south strip and climbs east to deck 2's wing (open well above it)
add('training-deck-tower-1', -46, -38, 3.4, 3.6, -12, -4, 'D', mat='metal')
for k, (x, z) in enumerate(((-45.8, -11.8), (-45.8, -4.2), (-38.2, -11.8), (-38.2, -4.2))):
    add('training-tower-column-1-%d' % k, x - .2, x + .2, 0, 3.4, z - .2, z + .2, 'C', mat='metal')
stair('training-stair-tower-1', -32.96, -38.0, -6.0, -4.0, 0, 3.6, 18)
stair('training-stair-tower-2', -43.04, -38.0, -12.0, -10.0, 3.6, 7.2, 18)
add('training-deck-tower-2', -46, -38, 7.0, 7.2, -10, -4, 'E', mat='metal')
add('training-deck-tower-2-wing', -38, -36, 7.0, 7.2, -12, -4, 'E', mat='metal')
for k, (x, z, y0) in enumerate(((-45.8, -9.8, 3.6), (-45.8, -4.2, 3.6), (-42.2, -4.2, 3.6), (-36.2, -11.8, 0), (-36.2, -6.4, 0))):
    add('training-tower-column-2-%d' % k, x - .2, x + .2, y0, 7.0, z - .2, z + .2, 'C', mat='metal')
for nm, b in (('w', (-46, -45.8, -10, -4)), ('n', (-46, -36, -4.2, -4)), ('e', (-36.2, -36, -12, -4)), ('s', (-38, -36, -12, -11.8)), ('well', (-46, -38.6, -10.2, -10))):
    add('training-rail-tower-2-' + nm, b[0], b[1], 7.2, 8.2, b[2], b[3], 'r', mat='metal')
TOWER_LEVELS = [('tower ground (under deck 1)', (-45.6, -38.4, -11.6, -4.4), 0.0), ('tower deck 1', (-46, -38, -12, -4), 3.6),
                ('tower deck 2', (-46, -36, -12, -4), 7.2)]
# sprint straight: 30 m along x, kept clear (marks every 5 m are look only), 6.5 m run-out
SPRINT = (-46.5, -16.5, 4.0, 7.4); SPRINT_RUNOUT = -10.0

# ---- cover lab (east): the standard pieces (half 1.0, half 1.2, full 2.4, a 20 ft container), a wall with a window and a door, and the
#      destruction samples by colour code (wood crate = cover, brick panel = masonry, chevron column = structural)
add('training-cover-half-100', 14.0, 14.6, 0, 1.0, 4.8, 7.2, 'h')
add('training-cover-half-120', 14.0, 14.6, 0, 1.2, 0.0, 2.4, 'h')
add('training-cover-full-240', 14.0, 14.6, 0, 2.4, -4.8, -2.4, 'F')
add('training-container-20ft', 19.6, 22.0, 0, 2.6, -3.0, 3.0, 'K', mat='metal')
LAB_X = (28.0, 28.3); WINDOW = (2.2, 3.8, 1.0, 2.0); DOOR = (-3.8, -2.2, 2.2)   # window z0, z1, sill, head; door z0, z1, head
wall_z('training-wall-lab', LAB_X[0], LAB_X[1], 0, 3.0, -7, 7, [WINDOW[:2], DOOR[:2]], '=')
add('training-wall-lab-sill', LAB_X[0], LAB_X[1], 0, WINDOW[2], WINDOW[0], WINDOW[1], '=')
add('training-wall-lab-head', LAB_X[0], LAB_X[1], WINDOW[3], 3.0, WINDOW[0], WINDOW[1], '=')
add('training-wall-lab-lintel', LAB_X[0], LAB_X[1], DOOR[2], 3.0, DOOR[0], DOOR[1], '=')
add('training-crate-sample', 38.0, 39.2, 0, 1.2, 3.4, 4.6, 'x', mat='wood', role='cover', hp=34)
add('training-brick-sample', 38.0, 38.3, 0, 2.4, -1.2, 1.2, 'M', mat='brick', role='masonry', hp=120)
add('training-column-sample', 38.0, 38.8, 0, 4.0, -5.0, -4.2, 'Y', mat='metal', role='structural', hp=80)

# ---- hazard bay (far south-east): 3 m walls, one 2.4 x 2.6 m door facing the spawn side; the press frame and the crane runway are blocks
BAY = (28.4, 48.0, -28.0, -10.4); BAY_DOOR = (-21.2, -18.8); BAY_DOOR_H = 2.6
wall_z('training-wall-bay-w', 28.0, 28.4, 0, 3.0, -28.0, -10.0, [BAY_DOOR], '=')
add('training-wall-bay-w-lintel', 28.0, 28.4, BAY_DOOR_H, 3.0, *BAY_DOOR, '=')
add('training-wall-bay-n', 28.4, 48.0, 0, 3.0, -10.4, -10.0, '=')
add('training-hazard-press-cheek-w', 40.0, 40.5, 0, 3.0, -14.7, -13.3, 'p', mat='metal')
add('training-hazard-press-cheek-e', 43.5, 44.0, 0, 3.0, -14.7, -13.3, 'p', mat='metal')
add('training-hazard-press-crown', 40.0, 44.0, 3.0, 3.4, -14.7, -13.3, 'p', mat='metal')
add('training-hazard-press-bolster', 40.5, 43.5, 0, .08, -15.1, -12.9, 'p', mat='metal')               # die bed, flush (8 cm)
add('training-hazard-crane-runway', 30.0, 47.0, 12.2, 13.2, -21.0, -20.0, 'r', mat='metal')

# ---- hazards (one sample per kind; the factory's and Cyber City's timings, in seconds after the arm time; the sandbox clock or the
#      DEV preview drives them). The saw keeps the factory's 6.0 m/s over a 12 m slot (travel 2.0 s instead of 6.0 s over 36 m).
def hz(**k):
    d = dict(id='', kind='', a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), size=v(0, 0, 0), radius=0, start=0, period=0, offset=0,
             warn=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, lethal=False, alternate=False, zones=[])
    d.update(k); return d
saw = hz(id='training-hazard-saw-slot', kind='saw-slot', a=v(33, 0, -25.5), b=v(45, 0, -25.5), axis=v(0, 0, 1), size=v(1.6, 1.6, .1), radius=.8,
         start=2, period=14, offset=0, warn=1.6, rise=.35, travel=2.0, lethal=True, alternate=True)
vent = hz(id='training-hazard-vent', kind='vent', a=v(33, 0, -14), size=v(1.4, 3.2, 1.4), start=3, period=9, offset=0, warn=.8, dwell=2.2, damage=10, tick=.5)
press = hz(id='training-hazard-press', kind='press', a=v(42, 2.75, -14), b=v(42, .33, -14), size=v(2.9, .5, 1.3),
           start=.5, period=6, offset=0, warn=.8, rise=.22, dwell=.6, travel=1.0, lethal=True)
grid = dict(id='training-hazard-grid', kind='grid', center=v(43.5, 0.5, -20.5), size=v(4, 1.0, 6.0), axis=v(0, 0, 1),
            first=4, period=14, offset=0, warn=0, danger=0, speed=0, length=0, charge=2.5, live=2.0, tick=0.25, damage=13, lethal=False)
crane = hz(id='training-hazard-crane', kind='crane', a=v(31, 13.2, -20.5), b=v(46, 13.2, -20.5), size=v(2.2, .9, 4.0), carry=9.5,
           start=6, period=20, warn=3.0, travel=6.0, rise=.85, dwell=5.15, hoist=2.0, lethal=True, zones=[{'center': v(36, 0, -20.5), 'size': v(2.8, 2.2, 4.6)}])
HAZ = [saw, vent, press, grid, crane]
PAINT_ZONE = (3.2, 5.0)                                                  # painted zone footprint (load 2.2 x 4.0 plus 0.5 m)

# ---- pilot-only blocking columns above unstandable tops and the deck-2 rails (1.0 m over a 7.2 m drop)
def top_of(i, cut=.1):
    x0, x1, y0, y1, z0, z1 = next(e['box'] for e in els if e['id'] == i)
    return dict(id=i.replace('training-', 'training-top-', 1), center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - cut, 3))
PILOT = [top_of(i) for i in ('training-wall-n', 'training-wall-s', 'training-wall-w', 'training-wall-e', 'training-roof', 'training-backstop-range',
                             'training-wall-bay-n', 'training-column-sample', 'training-hazard-press-crown', 'training-hazard-crane-runway')]
PILOT += [dict(id='training-top-wall-lab', center=v(28.15, 0, 0), size=v(.3, 0, 14), bottom=2.9), dict(id='training-top-wall-bay-w', center=v(28.2, 0, -19), size=v(.4, 0, 18), bottom=2.9)]
PILOT += [top_of(e['id'], .05) for e in els if e['id'].startswith('training-rail-tower-2-')]

def hazard_rects():
    """2D footprints of every hazard's swept or kill volume (x0, x1, z0, z1, name): what spawns keep 3 m from."""
    out = []
    for h in HAZ:
        k = h['kind']
        if k == 'grid': c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, h['id'])); continue
        a, b = h['a'], h['b']
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
    for x in range(int(SPRINT[0]), int(SPRINT_RUNOUT)): put(x + .5, (SPRINT[2] + SPRINT[3]) / 2, '-')
    for x0, x1, z0, z1, name in hazard_rects():
        rect(x0, x1, z0, z1, 'Z' if name.startswith('crane') else 'S' if 'slot' in name else 'G' if 'grid' in name else 'v' if 'vent' in name else 'p')
    for e in sorted(els, key=lambda e: e['box'][3]):
        b = e['box']
        if e['ch'] in '#R' or e['id'] == 'training-hazard-crane-runway': continue
        rect(b[0], b[1], b[4], b[5], e['ch'])
    for e in els:
        if e['ch'] == '#': b = e['box']; rect(b[0], b[1], b[4], b[5], '#')
    for p in TRAINING_PADS: put(p['pos']['x'], p['pos']['z'], '@' if p['kind'] == 'strafe' else '*' if p['kind'] == 'static' else 'm')
    for i, (x, z) in enumerate(ffa): put(x, z, str(i + 1))
    out = ['         x=-50' + ' ' * 36 + 'x=0' + ' ' * 47 + 'x=+49']
    for r in range(R): z = Z1 - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    out.append('legend: # shell  = lab/bay walls  k range kerbs  b safety barrier  B backstop  o ring kerb  * @ m range/strafe/melee pads  1-8 spawns  T terminal')
    out.append('        s step pads  P gap platforms  / stairs  D E tower decks 1/2  r rails  C columns  h F K half/full/container  x M Y crate/brick/column samples')
    out.append('        p press  S saw slot  v vent  G grid  Z crane zone  - sprint straight')
    return '\n'.join(out)

# ---- checks
def seg_box(p, q, b):
    """Whether the 3D segment p-q passes through the inside of box b (x0, x1, y0, y1, z0, z1)."""
    x0, x1, y0, y1, z0, z1 = b; t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((p[0], q[0] - p[0], x0, x1), (p[1], q[1] - p[1], y0, y1), (p[2], q[2] - p[2], z0, z1)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)
def rect_gap(a, b): return math.hypot(max(a[0] - b[1], 0, b[0] - a[1]), max(a[2] - b[3], 0, b[2] - a[3]))
def fp(e): b = e['box']; return (b[0], b[1], b[4], b[5])

CELL = .25; NI, NJ = int((X1 - X0) / CELL), int((Z1 - Z0) / CELL)
def cell(x, z): return (int((x - X0) / CELL), int((z - Z0) / CELL))
def raster(extra=()):
    """3D walk raster, 0.25 m cells. A cell is standable at height h (the floor or a block top under its centre) if nothing occupies the
    1.8 m above it and no block intrudes the 0.42 m capsule ring between h+0.45 and h+1.8; neighbours (4-way) connect when |dy| <= 0.25.
    Flood fill from spawn A1; returns {h: set of (i, j)} reached. Blocks from 9 m up (roof, crane runway) cannot touch any standable level."""
    boxes = [e['box'] for e in els if e['box'][2] < 9.0] + list(extra)
    near = defaultdict(list); tops = defaultdict(set)
    for b in boxes:
        x0, x1, y0, y1, z0, z1 = b
        for i in range(max(0, int((x0 - .42 - X0) / CELL) - 1), min(NI - 1, int((x1 + .42 - X0) / CELL) + 1) + 1):
            cx = X0 + (i + .5) * CELL; dx = max(x0 - cx, 0, cx - x1)
            if dx >= .42: continue
            for j in range(max(0, int((z0 - .42 - Z0) / CELL) - 1), min(NJ - 1, int((z1 + .42 - Z0) / CELL) + 1) + 1):
                cz = Z0 + (j + .5) * CELL; dz = max(z0 - cz, 0, cz - z1)
                if dx * dx + dz * dz >= .42 * .42: continue
                inside = x0 < cx < x1 and z0 < cz < z1
                near[(i, j)].append((y0, y1, inside))
                if inside: tops[(i, j)].add(round(y1, 3))
    memo = {}
    def standable(i, j, h):
        k = (i, j, h)
        if k not in memo:
            memo[k] = not any((y0 < h + 1.8 and y1 > h + 1e-6) if inside else (y0 < h + 1.8 and y1 > h + .45) for y0, y1, inside in near.get((i, j), ()))
        return memo[k]
    start = cell(*teamA[0]) + (0.0,); assert standable(*start), 'spawn A1 is not standable'
    seen = {start}; todo = [start]
    while todo:
        i, j, h = todo.pop()
        for ni, nj in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if not (0 <= ni < NI and 0 <= nj < NJ): continue
            for nh in (0.0, *tops.get((ni, nj), ())):
                n = (ni, nj, nh)
                if abs(nh - h) <= .25 + 1e-6 and n not in seen and standable(*n): seen.add(n); todo.append(n)
    lv = defaultdict(set)
    for i, j, h in seen: lv[h].add((i, j))
    return lv
def reached(lv, r, h):
    """How many reached cells at height h have their centre inside the footprint r (x0, x1, z0, z1)."""
    return sum(1 for i, j in lv.get(round(h, 3), ()) if r[0] < X0 + (i + .5) * CELL < r[1] and r[2] < Z0 + (j + .5) * CELL < r[3])

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)),
                'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], 1.1, p[1]), 'yaw': SPAWN_YAW}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'training', 'name': 'Calibration Bay', 'tag': 'Calibration Bay. Test anything, break nothing that matters.', 'theme': 'training',
                      'music': 'training-room', 'ambience': 'training-hall', 'look': 'training-studio', 'sky': 0xC8CDD2, 'floor': 0x8C8F92, 'accent': 0x2EC8E6,
                      'rotation': False, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []},
                      'trainingPads': TRAINING_PADS}]}

def png(path, scale=8):
    W, H = (X1 - X0) * scale, (Z1 - Z0) * scale; img = bytearray(b'\x18\x1a\x1e' * W * H)
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        i0, i1 = int(round((x0 - X0) * scale)), int(round((x1 - X0) * scale)); j0, j1 = int(round((Z1 - z1) * scale)), int(round((Z1 - z0) * scale))
        for j in range(max(0, j0), min(H, max(j1, j0 + 1))):
            for i in range(max(0, i0), min(W, max(i1, i0 + 1))):
                o = (j * W + i) * 3
                for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    fill(X0, X1, Z0, Z1, (120, 123, 126))
    for x in range(-48, 49):                                               # the 1 m grid, the 5 m lines bolder (look only)
        fill(x - .02, x + .02, -28, 28, (98, 101, 104)) if x % 5 else fill(x - .05, x + .05, -28, 28, (84, 87, 90))
    for z in range(-28, 29):
        fill(-48, 48, z - .02, z + .02, (98, 101, 104)) if z % 5 else fill(-48, 48, z - .05, z + .05, (84, 87, 90))
    fill(SPRINT[0], SPRINT_RUNOUT, SPRINT[2], SPRINT[3], (96, 140, 150), .45)
    for k in range(7): fill(SPRINT[0] + 5 * k - .1, SPRINT[0] + 5 * k + .1, SPRINT[2], SPRINT[3], (40, 210, 235))
    fill(FIRE_X - .1, FIRE_X + .1, LANES[0][0], LANES[-1][1], (40, 210, 235))
    fill(SPAWN_PAD[0] - 6, SPAWN_PAD[0] + 6, SPAWN_PAD[1] - 4, SPAWN_PAD[1] + 4, (70, 150, 170), .35)
    col = {'#': (150, 152, 156), '=': (175, 170, 160), 'k': (40, 210, 235), 'b': (200, 200, 205), 'B': (110, 100, 90), 'o': (240, 150, 40), 'T': (40, 210, 235),
           's': (150, 160, 175), 'P': (130, 140, 160), '/': (200, 200, 205), 'D': (110, 125, 150), 'E': (90, 105, 135), 'r': (230, 230, 235), 'C': (60, 70, 90),
           'h': (175, 175, 165), 'F': (140, 140, 132), 'K': (180, 80, 55), 'x': (190, 140, 80), 'M': (170, 80, 60), 'Y': (245, 180, 30), 'p': (220, 170, 40)}
    for e in sorted(els, key=lambda e: e['box'][3]):
        b = e['box']
        if e['ch'] == 'R' or e['id'] == 'training-hazard-crane-runway': continue
        if e['ch'] == 's': shade = .45 + .55 * b[3] / 3.0; c = tuple(int(k * shade) for k in col['s'])
        else: c = col.get(e['ch'], (128, 128, 128))
        fill(b[0], b[1], b[4], b[5], c, .8 if e['ch'] == 'E' else 1.0)
    for x0, x1, z0, z1, name in hazard_rects():
        c = (230, 60, 40) if 'saw' in name else (240, 190, 30) if 'crane' in name else (230, 120, 30) if 'press' in name else (80, 170, 255) if 'grid' in name else (180, 200, 220)
        fill(x0, x1, z0, z1, c, .7)
    r = crane; fill(r['a']['x'], r['b']['x'], r['a']['z'] - .1, r['a']['z'] + .1, (240, 190, 30), .5)
    for p in TRAINING_PADS:
        x, z = p['pos']['x'], p['pos']['z']
        if 'rail' in p: fill(x - .08, x + .08, p['rail']['a']['z'], p['rail']['b']['z'], (40, 210, 235))
        fill(x - .45, x + .45, z - .45, z + .45, (255, 255, 255) if p['kind'] != 'melee' else (255, 120, 60))
    for i, p in enumerate(ffa): fill(p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if i < 4 else (255, 90, 120))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint())
    bad = 0
    def fail(ok, msg):
        global bad
        if not ok: bad += 1; print('   FAIL', msg)
    print('spawn sightlines (FFA / teams A-B): n/a: sandbox (co-op testers spawn together on the pad, facing north)')
    sp = ffa
    print('min FFA spacing %.1f m; min A-B spacing %.1f m (one pad, by design)' % (min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p, q) for p in teamA for q in teamB)))
    worst = min((dist_rect(p, r[:4]), r[4], p) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume: %.1f m (%s, spawn %s)' % worst); fail(worst[0] >= 3, 'a spawn is within 3 m of a hazard')
    clr = min((min(dist_rect(p, (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < 1.9), p) for p in sp)
    print('closest spawn to any block: %.1f m at %s' % clr); fail(clr[0] >= .42, 'a spawn capsule overlaps a block')
    inb = all(BOUNDS['minX'] + .42 <= x <= BOUNDS['maxX'] - .42 and BOUNDS['minZ'] + .42 <= z <= BOUNDS['maxZ'] - .42 for x, z in sp)
    print('all spawns inside the bounds minus the capsule: %s; spawn y = floor + 1.1, yaw %.0f (north)' % (inb, SPAWN_YAW)); fail(inb, 'spawn outside bounds')
    # (a) every range pad seen from its lane's firing-line midpoint at eye 1.6 (to the dummy's centre and head; the strafe rail's ends too)
    blk = [e['box'] for e in els]; los_bad = []
    for p in TRAINING_PADS:
        if p['kind'] == 'melee': continue
        L = int(p['id'].split('-l')[1][0]) - 1; eye = (FIRE_X, 1.6, sum(LANES[L]) / 2)
        pts = [p['pos']] + ([p['rail']['a'], p['rail']['b']] if 'rail' in p else [])
        for q in pts:
            for y in (1.1, 1.6):
                hit = [e['id'] for e, b in zip(els, blk) if seg_box(eye, (q['x'], y, q['z']), b)]
                if hit: los_bad.append('%s at y %.1f: %s' % (p['id'], y, hit[0]))
    nr = sum(1 for p in TRAINING_PADS if p['kind'] != 'melee')
    print('range sightlines: %d pads (+2 rail ends) x 2 heights from the firing line at eye 1.6: %d blocked' % (nr, len(los_bad)))
    for s in los_bad: print('   OPEN-FAIL', s)
    fail(not los_bad, 'a range pad is hidden from its firing point')
    # range measurements: pad distances, lane widths, kerbs
    dists = sorted({round(p['pos']['x'] - FIRE_X, 3) for p in TRAINING_PADS if p['kind'] != 'melee'})
    lw = [round(b - a, 3) for a, b in LANES]; kerbs = [e for e in els if e['id'].startswith('training-kerb-range')]
    kk = sorted({(round(e['box'][5] - e['box'][4], 3), round(e['box'][3], 3)) for e in kerbs})
    print('range: firing line x %.0f; pad distances %s m; lanes %s m wide, z %.1f..%.1f; kerbs (width, height) %s; strafe rail %s'
          % (FIRE_X, dists, lw, LANES[0][0], LANES[-1][1], kk, [(p['id'], p['rail']['a']['z'], p['rail']['b']['z']) for p in TRAINING_PADS if 'rail' in p]))
    fail(dists == [float(d) for d in DISTS] and lw == [3.5] * 4 and kk == [(.3, .5)], 'range measurements')
    # movement course measurements
    st = [e for e in els if e['id'].startswith('training-step-')]
    sh = [e['box'][3] for e in st]; ssz = min(min(e['box'][1] - e['box'][0], e['box'][5] - e['box'][4]) for e in st)
    sgap = round(min(rect_gap(fp(a), fp(b)) for a, b in itertools.combinations(st, 2)), 3)
    others = round(min(rect_gap(fp(s), fp(e)) for s in st for e in els if not e['id'].startswith('training-step-') and e['box'][2] < 2.0 and e['ch'] != '#'), 3)
    print('steps: tops %s m; pads %.1f m square, %.1f m apart, %.1f m from anything else' % (sh, ssz, sgap, others))
    fail(tuple(sh) == STEPS and ssz >= 2.0 and sgap >= 2.0 and others >= 2.0, 'step pad measurements')
    gaps = [round(PLAT[k + 1][0] - PLAT[k][1], 3) for k in range(len(PLAT) - 1)]
    print('gap run: %d platforms at 2.0 m, gaps %s m, first platform by %s' % (len(PLAT), gaps, 'training-stair-gap'))
    fail(gaps == [float(g) for g in GAPS], 'gap widths')
    for i, rise, tread, w in STAIRS: print('stair %s: riser %.2f m, tread %.2f m, width %.1f m' % (i, rise, tread, w)); fail(rise <= .2 + 1e-9 and w >= 1.6, 'stair ' + i)
    lane = [e['id'] for e in els if e['box'][2] < 2.0 and e['box'][0] < SPRINT_RUNOUT and e['box'][1] > SPRINT[0] - 1 and e['box'][4] < SPRINT[3] and e['box'][5] > SPRINT[2]]
    print('sprint straight: %.0f m (x %.1f..%.1f, z %.1f..%.1f) + %.1f m run-out; props in it: %s' % (SPRINT[1] - SPRINT[0], SPRINT[0], SPRINT[1], SPRINT[2], SPRINT[3], SPRINT_RUNOUT - SPRINT[1], lane or 'none'))
    fail(SPRINT[1] - SPRINT[0] == 30 and not lane, 'sprint straight')
    print('melee ring: %.0f m octagon (apothem %.1f), kerb 0.3 m high, %d openings of %.1f m; cover lab window sill %.1f head %.1f width %.1f; door %.1f x %.1f m'
          % (2 * APO, APO, 4, RING_OPEN, WINDOW[2], WINDOW[3], WINDOW[1] - WINDOW[0], DOOR[1] - DOOR[0], DOOR[2]))
    mid = [(e['id'], e['box'][3]) for e in els if 1.3 < e['box'][3] < 2.2 and e['ch'] != '/']
    print('tops in 1.3..2.2 m (head-glitch band) outside the stairs: %s (deliberate test heights)' % sorted(set(t for _, t in mid)) if all(i.startswith(('training-step-', 'training-platform-gap')) for i, _ in mid) else mid)
    fail(all(i.startswith(('training-step-', 'training-platform-gap')) for i, _ in mid), 'a non-test piece tops out in 1.3..2.2 m')
    # (b) reachability on foot from spawn A1 (3D raster)
    lv = raster()
    print('3D raster (0.25 m cells): reached %d cells; per level: %s' % (sum(len(s) for s in lv.values()),
          ', '.join('%.2f m: %d' % (h, len(lv[h])) for h in sorted(lv) if len(lv[h]) >= 40) + '; stair treads and small tops: %d cells on %d heights' % (sum(len(lv[h]) for h in lv if len(lv[h]) < 40), sum(1 for h in lv if len(lv[h]) < 40))))
    unr = [n for n, p in zip(ffa_names, ffa) if not reached(lv, (p[0] - .2, p[0] + .2, p[1] - .2, p[1] + .2), 0)]
    print('every spawn reachable: %s' % (not unr)); fail(not unr, 'unreachable spawns %s' % unr)
    upad = [p['id'] for p in TRAINING_PADS if not reached(lv, (p['pos']['x'] - .2, p['pos']['x'] + .2, p['pos']['z'] - .2, p['pos']['z'] + .2), 0)]
    print('every training pad reachable (%d pads): %s' % (len(TRAINING_PADS), upad or True)); fail(not upad, 'unreachable pads')
    for e in st:
        b = e['box']; n = reached(lv, (b[0], b[1], b[4], b[5]), b[3])
        print('   %s top %.2f m: %s' % (e['id'], b[3], 'walk (%d cells)' % n if n else 'step/jump test (+%.2f m from the floor)' % b[3]))
    fail(reached(lv, (st[0]['box'][0], st[0]['box'][1], st[0]['box'][4], st[0]['box'][5]), st[0]['box'][3]) > 0, 'the 0.2 m step is not walkable')
    for k, (a, b) in enumerate(PLAT):
        n = reached(lv, (a, b, GAP_Z[0], GAP_Z[1]), 2.0)
        print('   gap platform %d (x %.1f..%.1f): %s' % (k + 1, a, b, ('reached by the stair (%d cells)' % n if n else 'NOT REACHED') if k == 0 else 'jump test (gap %d m)' % GAPS[k - 1]))
        if k == 0: fail(n > 0, 'first gap platform not reachable')
    for nm, r, h in TOWER_LEVELS:
        n = reached(lv, r, h); print('   %s at %.1f m: %d cells' % (nm, h, n)); fail(n > 0, nm + ' not reachable')
    zones = [('range lane %d' % (k + 1), (FIRE_X, 44, a, b)) for k, (a, b) in enumerate(LANES)] + [('melee ring', (-6, 6, -6, 6)), ('cover lab', (10, 27.6, -8, 8)), ('hazard bay', BAY)]
    for nm, r in zones:
        n = reached(lv, r, 0); print('   %s floor: %d cells' % (nm, n)); fail(n > 0, nm + ' floor not reachable')
    # (c) above; (d) the hazard bay is enclosed: plug its door and no cell inside the bay is reachable at any height
    shut = raster(extra=[(28.0, 28.4, 0, 3.0, BAY_DOOR[0], BAY_DOOR[1])])
    leak = sum(1 for h, cs in shut.items() for i, j in cs if BAY[0] < X0 + (i + .5) * CELL < BAY[1] and BAY[2] < Z0 + (j + .5) * CELL < BAY[3])
    print('hazard bay enclosure: door %.1f x %.1f m; with the door plugged %d bay cells reachable (must be 0); door open: %d bay floor cells'
          % (BAY_DOOR[1] - BAY_DOOR[0], BAY_DOOR_H, leak, reached(lv, BAY, 0))); fail(leak == 0, 'the hazard bay leaks')
    near_bay = min(rect_gap(fp(e), (28.0, 48.0, -28.0, -10.0)) for e in els if 2.0 <= e['box'][3] < 3.6 and e['box'][2] < 1 and not e['id'].startswith(('training-wall-bay', 'training-hazard-', 'training-wall-lab')))
    print('closest 2.0..3.6 m top (a climb onto the 3 m bay wall) to the bay walls: %.1f m' % near_bay); fail(near_bay >= 3.0, 'a tall piece next to the bay walls')
    zc = []
    for h in HAZ:
        for zn in h.get('zones', []):
            c = zn['center']; r = (c['x'] - PAINT_ZONE[0] / 2, c['x'] + PAINT_ZONE[0] / 2, c['z'] - PAINT_ZONE[1] / 2, c['z'] + PAINT_ZONE[1] / 2)
            for e in els:
                b = e['box']
                if b[2] >= 2.5: continue
                zc.append((math.hypot(max(r[0] - b[1], 0, b[0] - r[1]), max(r[2] - b[5], 0, b[4] - r[3])), e['id']))
    print('closest block to the painted crane zone: %.2f m (%s)' % min(zc)); fail(min(zc)[0] >= .5, 'crane zone too close to a block')
    hr = hazard_rects(); hsep = min(rect_gap(a, b) for a, b in itertools.combinations(hr, 2))
    print('hazard samples: %d, closest pair of volumes %.1f m apart; all inside the bay: %s' % (len(HAZ), hsep, all(BAY[0] <= r[0] and r[1] <= BAY[1] and BAY[2] <= r[2] and r[3] <= BAY[3] for r in hr)))
    fail(all(BAY[0] <= r[0] and r[1] <= BAY[1] and BAY[2] <= r[2] and r[3] <= BAY[3] for r in hr), 'a hazard volume is outside the bay')
    # hazard fairness numbers (the factory's and Cyber City's timings)
    s = saw; Ls = abs(s['b']['x'] - s['a']['x']); print('saw-slot: %.0f m in %.1f s = %.1f m/s (sprint 6.8; factory 6.0); telegraph %.1f s + rise %.2f s; lethal %.2f s of %.0f s; alternates'
          % (Ls, s['travel'], Ls / s['travel'], s['warn'], s['rise'], s['travel'] + 2 * s['rise'], s['period']))
    c = crane; print('crane: %d zone, every %.0f s; %.1f s travel + %.1f s telegraph over the zone; fall %.2f s from %.1f m; landed cover %.2f s'
          % (len(c['zones']), c['period'], c['travel'], c['warn'], c['rise'], c['carry'], c['dwell']))
    p = press; print('press: open %.1f s of %.0f s, telegraph %.1f s, slam %.2f s; frame 1.4 m deep = %.2f s to cross at 6.8 m/s' % (p['period'] - p['warn'] - p['rise'] - p['dwell'] - p['travel'], p['period'], p['warn'], p['rise'], (1.4 + .84) / 6.8))
    print('vent: every %.0f s, telegraph %.1f s, burst %.1f s, %d per %.1f s tick' % (vent['period'], vent['warn'], vent['dwell'], vent['damage'], vent['tick']))
    print('grid: every %.0f s, charge (telegraph) %.1f s, live %.1f s, %d per %.2f s tick, first %.0f s after the arm time' % (grid['period'], grid['charge'], grid['live'], grid['damage'], grid['tick'], grid['first']))
    print('starts after the arm time (FightAt + 4 s): %s' % ', '.join('%s %.1f s' % (h['id'].split('hazard-')[1], h.get('start', h.get('first'))) for h in HAZ))
    ids = [e['id'] for e in els] + [FLOOR['id']]; dup = sorted({i for i in ids if ids.count(i) > 1})
    roles = {r: sum(1 for e in els if e['role'] == r) for r in ('static', 'cover', 'masonry', 'structural')}
    print('blocks: %d (+ floor) %s; unique ids: %s; hazards: %d; pilot blocks: %d; training pads: %d (%s)' % (len(els), roles, not dup, len(HAZ), len(PILOT), len(TRAINING_PADS),
          ', '.join('%s %d' % (k, sum(1 for p in TRAINING_PADS if p['kind'] == k)) for k in ('static', 'strafe', 'melee'))))
    fail(len(els) <= 350 and not dup, 'block budget or duplicate ids %s' % dup)
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'training_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'training_m1.png')); print('wrote', os.path.join(out, 'training_m1.json'), 'and training_m1.png')
    print('checks: %s' % ('all pass' if not bad else '%d FAILED' % bad))
    sys.exit(1 if bad else 0)
