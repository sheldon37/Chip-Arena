# Expedition CENTRAL STATION instance, design draft: `expedition-transit` (area `exp-transit`), the boarded transit hub at
# the end of the hub's Station Road (docs/EXPEDITION-CENTRAL-STATION.md). NOT LIVE: it writes one draft file that nothing at
# runtime loads (unity/ChipArenaConversion/Tests/expedition-central-station-draft.json): the map entry (UnityMaps.json shape,
# no navigation yet), the instance's area record (ExpeditionLayout.json schema 1), the hub door record it needs in the town,
# the look (a MapLooks.json-shaped entry) and the proposals for Astra (machines, hazards, the new enemies' entrance kinds).
# Resources/UnityMaps.json, ExpeditionLayout.json and MapLooks.json are read, never written.
# Plan (instance frame): a through station at night. The exit door is in the ticket hall's north wall (z 36). The hall (low
# ceiling) opens through a gate line onto the grand concourse (80 x 24 m under a broken glass roof, balconies east and west,
# the boss arena placeholder). South of the concourse, four platforms run east-west under the train shed: P1 (the side
# platform at the concourse), P2 and P3 (islands) and P4 (the far side), with three track pits between them (T1 the live
# express line, T2 the stalled train, T3 the flooded freight line) that meet in the throats at both ends and run into the
# tunnel mouths. Two footbridges (y 6) cross the shed; the east one ends in the signal box (the far objective), over the
# maintenance corridor behind P4. The upper floor (y 6) is the balconies, the north gallery and the staff offices.
# Checks: the three floor levels (ground y 0, upper y 6, track bed y -1.2) all reached from the entry slots; every record on
# walkable floor with pilot clearance (Hulk-sized at the entries and boss points); Hulk and pilot routes from entry 0; every
# spawn exit on reached floor with its clear radius and routed back with its own capsule; allow lists within the existing
# entry rules; breach panels on solid faces; no two colliding blocks overlap; ramps valid; ids; the door's volumes; the
# hub door record in the town's frame against the live town blocks (reported, with the town spawns it would crowd).
# Usage: python3 docs/arenas/scripts/expedition_central_station.py [--json draft.json] [--png plan.png] [--fixture]
# Coordinates: Unity frame, x east, z north, y up, metres. Deterministic: no clocks, no randomness.
import argparse, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from expedition_common import (Collider, Canvas, text_width, components, block_hash, id_ok, v, q, qyaw, vol, pt, vol_block, in_vol, vol_in_bounds,
                               yaw_of, yaw_rad, forward, dist_xz, yspan, corners, footprint, ramp_block, r4, RADIUS, HEIGHT, PILOT_CLEAR,
                               HULK_CLEAR, HULK_BAND, HULK_RADIUS, HULK_HEIGHT, HULK_SLOT)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
RES = os.path.join(ROOT, 'unity', 'ChipArenaConversion', 'Assets', 'Conversion', 'Resources')
FIXTURE = os.path.join(ROOT, 'unity', 'ChipArenaConversion', 'Tests', 'expedition-central-station-draft.json')
MAP = 'expedition-transit'; AREA = 'exp-transit'; TOWN = 'exp-city'; LOOK = 'central-station'
L1, TB = 6.0, -1.2                     # the upper floor and the track bed
DOOR_W, DOOR_H = 3.2, 3.4
BOUNDS = dict(minX=-48.0, maxX=48.0, minZ=-60.0, maxZ=36.0, ceiling=22.0, killY=-10.0)
SMALL = ['shambler', 'runner', 'spitter']; BIG = SMALL + ['brute']

blocks = []; visual = []
def box(id, x0, x1, z0, z1, y0, y1, material='stone', vis=False):
    """An axis-aligned static block (min/max corners). `vis`: a visual block with no collider (a stair's steps)."""
    assert x1 > x0 and z1 > z0 and y1 > y0, id
    b = dict(id=MAP + '-' + id, material=material, pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0),
             rotation=q(), role='static', mass=26, segment=None, hp=0)
    blocks.append(b)
    if vis: visual.append(b['id'])
    return b
def obox(id, cx, cz, sx, sz, y0, y1, yaw, material='metal'):
    b = dict(id=MAP + '-' + id, material=material, pos=v(cx, (y0 + y1) / 2, cz), size=v(sx, y1 - y0, sz), rotation=qyaw(yaw),
             role='static', mass=26, segment=None, hp=0)
    blocks.append(b); return b
def strips(u0, u1, y0, y1, openings):
    """A face u0..u1 x y0..y1 minus opening rects (a, b, lo, hi): solid pieces (a, b, v0, v1), merged along u."""
    cuts = sorted({u0, u1, *[min(max(o[0], u0), u1) for o in openings], *[min(max(o[1], u0), u1) for o in openings]}); out = []
    for a, b in zip(cuts, cuts[1:]):
        if b - a < 1e-6: continue
        mid = (a + b) / 2; y = y0
        for lo, hi in sorted((o[2], o[3]) for o in openings if o[0] <= mid <= o[1]):
            if lo > y: out.append([a, b, y, lo])
            y = max(y, hi)
        if y < y1: out.append([a, b, y, y1])
    merged = []
    for p in out:
        if merged and abs(merged[-1][1] - p[0]) < 1e-6 and merged[-1][2:] == p[2:]: merged[-1][1] = p[1]
        else: merged.append(p)
    return merged
def wall(id, axis, t0, t1, u0, u1, y0, y1, openings=(), material='stone'):
    """A wall along `axis` ('x': it runs along x, t0..t1 is its z thickness; 'z': along z, t0..t1 its x) with openings
    (a, b, lo, hi) in absolute u and y."""
    for k, (a, b, lo, hi) in enumerate(strips(u0, u1, y0, y1, openings)):
        name = f'{id}-{k}' if len(openings) else id
        if axis == 'x': box(name, a, b, t0, t1, lo, hi, material)
        else: box(name, t0, t1, a, b, lo, hi, material)
ramps = []
def stair(id, low, high, width, bottom, step=0.25, kind='stair', material='stone'):
    """A walkable wedge from low (x, y, z) to high (axis-aligned run) with visual step blocks (no colliders), as Siren's."""
    (lx, ly, lz), (hx, hy, hz) = low, high
    n = max(1, int(round((hy - ly) / step))); ids = []
    for k in range(n):
        t0, t1 = k / n, (k + 1) / n; top = ly + (hy - ly) * t1
        if abs(hx - lx) > abs(hz - lz):
            a, b = sorted((lx + (hx - lx) * t0, lx + (hx - lx) * t1)); bb = box(f'{id}-{k:02d}', a, b, lz - width / 2, lz + width / 2, bottom, top, material, vis=True)
        else:
            a, b = sorted((lz + (hz - lz) * t0, lz + (hz - lz) * t1)); bb = box(f'{id}-{k:02d}', lx - width / 2, lx + width / 2, a, b, bottom, top, material, vis=True)
        ids.append(bb['id'])
    ramps.append(dict(id=MAP + '-' + id, kind=kind, low=v(lx, ly, lz), high=v(hx, hy, hz), width=r4(width), bottom=r4(bottom), blocks=ids))

# =====================================================================================================================
# SHELL (the bounds' edge; outside it), GROUND AND TRACK BED.
X0, X1, Z0, Z1 = BOUNDS['minX'], BOUNDS['maxX'], BOUNDS['minZ'], BOUNDS['maxZ']
box('shell-n', X0 - 1, X1 + 1, Z1, Z1 + 1, -1.7, 20, 'brick'); box('shell-s', X0 - 1, X1 + 1, Z0 - 1, Z0, -1.7, 20, 'brick')
box('shell-w', X0 - 1, X0, Z0, Z1, -1.7, 20, 'brick'); box('shell-e', X1, X1 + 1, Z0, Z1, -1.7, 20, 'brick')
box('floor-north', X0, X1, -4, Z1, -0.5, 0, 'foundation')         # hall, concourse and back of house (terrazzo)
box('track-bed', X0, X1, -54, -4, -1.7, TB, 'foundation')         # ballast under the shed, the throats and the tunnel stubs
box('floor-corridor', X0, X1, Z0, -54, -0.5, 0, 'foundation')     # the maintenance corridor
# Platforms (tops at 0) on the bed. P1 is the concourse's side platform, P2 and P3 islands, P4 the far side platform.
PLAT = {'p1': (-44, 44, -10, -4), 'p2': (-38, 38, -27, -15), 'p3': (-38, 38, -42, -32), 'p4': (-44, 44, -53, -47)}
# Barrow crossings on the central axis (x -1.5..1.5): each platform edge is notched 3 m deep for a ramp down to the track
# bed, so the express line (T1) and the freight line (T3) can be crossed at rail level (the third rail is gapped there; the
# train is not). T2 is crossed through the stalled train's car 2.
NOTCH = {'p1': (-10, -7), 'p2': (-18, -15), 'p3': (-42, -39), 'p4': (-50, -47)}
for k, (a, b, c, d) in PLAT.items():
    n0, n1 = NOTCH[k]
    box(f'plat-{k}-w', a, -1.5, c, d, TB, 0, 'stone'); box(f'plat-{k}-e', 1.5, b, c, d, TB, 0, 'stone')
    if n0 > c: box(f'plat-{k}-c0', -1.5, 1.5, c, n0, TB, 0, 'stone')
    if n1 < d: box(f'plat-{k}-c1', -1.5, 1.5, n1, d, TB, 0, 'stone')
stair('crossing-p1', (0.0, TB, -10.0), (0.0, 0, -7.0), 3.0, TB, step=0.3); stair('crossing-p2', (0.0, TB, -15.0), (0.0, 0, -18.0), 3.0, TB, step=0.3)
stair('crossing-p3', (0.0, TB, -42.0), (0.0, 0, -39.0), 3.0, TB, step=0.3); stair('crossing-p4', (0.0, TB, -47.0), (0.0, 0, -50.0), 3.0, TB, step=0.3)
TRACKS = {'t1': (-15, -10), 't2': (-32, -27), 't3': (-47, -42)}   # z ranges of the pits (bed at -1.2)

# =====================================================================================================================
# THE NORTH BLOCK: the ticket hall (y 0, ceiling 5.5) under the upper floor (y 6): the north gallery over the concourse and
# the staff offices. Back of house either side is solid to the upper floor; the outer side masses rise to the upper roof.
HALL = (-22, 22, 20, 36)
box('mass-nw', -40, -22, 20, Z1, 0, L1, 'brick'); box('mass-ne', 22, 40, 20, Z1, 0, L1, 'brick')
box('mass-nw-outer', X0, -40, 20, Z1, 0, 10, 'brick'); box('mass-ne-outer', 40, X1, 20, Z1, 0, 10, 'brick')
box('hall-ceiling', -22, 22, 20, Z1, 5.5, L1, 'stone')
box('upper-roof', X0, X1, 20, Z1, 10, 10.5, 'stone')
# The gate line (turnstile banks 1.05 m) with three openings, the centre one 6 m (a Hulk passes).
for k, (a, b) in enumerate(((-22, -13), (-9, -3), (3, 9), (13, 22))): box(f'hall-gates-{k}', a, b, 21, 22, 0, 1.05, 'metal')
box('hall-office', -22, -17, 26, 33, 0, 3.2, 'wood'); box('hall-counter', -17, -16.4, 26, 33, 0, 1.1, 'wood')
box('hall-machines', 20.6, 22, 25, 33, 0, 1.9, 'metal')
for s in (-1, 1): box(f'hall-column-{"w" if s < 0 else "e"}', 11 * s - 0.4, 11 * s + 0.4, 27.6, 28.4, 0, 5.5, 'stone')
box('hall-sandbags-w', -9, -4.6, 29.0, 29.6, 0, 0.9, 'wood'); box('hall-sandbags-e', 4.6, 9, 29.0, 29.6, 0, 0.9, 'wood')
# The exit door: a frame 0.5 m proud of the shell's inner face (z 36) round the 3.2 x 3.4 m doorway at x 0; the shell
# behind stays solid (the leaves and the street beyond are drawn), as Ward Zero's instance exit.
DOOR = (0.0, Z1); YAW = 0.0; FACE, DEPTH = Z1, 0.5
box('exit-jamb-w', -DOOR_W / 2 - 0.4, -DOOR_W / 2, FACE - DEPTH, FACE, 0, DOOR_H + 0.4, 'metal')
box('exit-jamb-e', DOOR_W / 2, DOOR_W / 2 + 0.4, FACE - DEPTH, FACE, 0, DOOR_H + 0.4, 'metal')
box('exit-lintel', -DOOR_W / 2, DOOR_W / 2, FACE - DEPTH, FACE, DOOR_H, DOOR_H + 0.4, 'metal')
# Upper floor: the gallery rail over the concourse, the office wall with three doors, the room partitions with doors.
box('gallery-rail', -30, 30, 20, 20.2, L1, L1 + 1.1, 'metal')
wall('office-wall', 'x', 24, 24.4, -40, 40, L1, 10, [(-27, -25.4, L1, L1 + 2.2), (-1.6, 1.6, L1, L1 + 3.4), (25.4, 27, L1, L1 + 2.2)])
for s, nm in ((-1, 'w'), (1, 'e')): wall(f'office-part-{nm}', 'z', 14 * s - (0.4 if s > 0 else 0), 14 * s + (0.4 if s < 0 else 0), 24.4, Z1, L1, 10, [(30, 31.6, L1, L1 + 2.2)])
box('office-desk', -3, 3, 28.5, 29.5, L1, L1 + 0.8, 'wood')
box('office-lockers', -34, -26, 35, Z1, L1, L1 + 2.0, 'metal'); box('office-desk-pa', 30, 36, 33.6, 34.6, L1, L1 + 0.85, 'metal')

# =====================================================================================================================
# THE GRAND CONCOURSE (y 0, x -40..40, z -4..20): roof beams at 18 (the glass between them is drawn), balconies east and
# west (y 6) on posts, a grand stair up to each, the departure board over the information booth, kiosks, benches, a
# fallen roof girder. Side masses close it east and west (staff doors and bricked arches are the spawn faces).
box('mass-cw', X0, -40, -4, 20, 0, 20, 'brick'); box('mass-ce', 40, X1, -4, 20, 0, 20, 'brick')
for k, z in enumerate((-3, 3, 9, 15)): box(f'roof-beam-{k}', -40, 40, z - 0.4, z + 0.4, 18, 18.8, 'metal')
box('screen', -40, 40, -4.4, -4, 10, 18, 'metal')                  # the train-shed screen (its glazing is drawn)
for s, nm in ((-1, 'w'), (1, 'e')):
    ox = lambda x: x * s                                            # mirror x for the east side
    xs = lambda a, b: (min(ox(a), ox(b)), max(ox(a), ox(b)))
    box(f'deck-{nm}', *xs(-40, -30), -4, 20, 5.5, L1, 'stone')                                 # balcony
    box(f'landing-{nm}', *xs(-30, -25.6), -4, 2, 5.5, L1, 'stone')                             # the stair's top landing
    for k, z in enumerate((-4.0, 3.7, 11.7, 19.4)): box(f'post-{nm}-{k}', *xs(-30.3, -29.7), z, z + 0.6, 0, 5.5, 'metal')
    box(f'post-{nm}-landing', *xs(-26.2, -25.6), -4, -3.4, 0, 5.5, 'metal')
    box(f'rail-{nm}-inner', *xs(-30.2, -30), 2, 20, L1, L1 + 1.1, 'metal')
    box(f'rail-{nm}-landing', *xs(-25.8, -25.6), -3.8, 2, L1, L1 + 1.1, 'metal')
    box(f'rail-{nm}-s0', *xs(-40, -36), -4, -3.8, L1, L1 + 1.1, 'metal'); box(f'rail-{nm}-s1', *xs(-32, -25.6), -4, -3.8, L1, L1 + 1.1, 'metal')
    stair(f'stair-{nm}', (ox(-27.5), 0, 14), (ox(-27.5), L1, 2), 3.6, 0)
    box(f'kiosk-{nm}', *xs(-20, -14), 12, 16, 0, 3.0, 'wood')                                  # the cafe (west) and the newsagent (east)
    box(f'benches-{nm}-0', *xs(-10, -5), 7, 7.6, 0, 0.5, 'wood'); box(f'benches-{nm}-1', *xs(-10, -5), 4, 4.6, 0, 0.5, 'wood')
    box(f'trolley-{nm}', *xs(-36, -34), -2, 0, 0, 1.1, 'metal')
box('booth', -2.5, 2.5, 9, 13, 0, 3.2, 'wood')                      # the information booth under the departure board
obox('girder', 8.0, -1.2, 10.0, 0.9, 0, 1.1, 20.0, 'metal')       # a roof girder fallen through the glass
# Platform barrier (1.1 m) between the concourse and P1, three 6 m gates.
for k, (a, b) in enumerate(((-40, -24), (-18, -3), (3, 18), (24, 40))): box(f'barrier-{k}', a, b, -4.4, -4, 0, 1.1, 'metal')

# =====================================================================================================================
# THE TRAIN SHED: end masses with the tunnel mouths (4 m stubs to the shell), the roof at 12 with skylight gaps, the
# stalled train on T2 (cars 1 and 3 sealed; car 2 a walk-through between P2 and P3), a derailed freight wagon on T3,
# platform end steps into the throats, the footbridges (y 6) with their stairs, the south wall with two corridor doors.
for s, nm in ((-1, 'w'), (1, 'e')):
    x0, x1 = (X0, -44) if s < 0 else (44, X1)
    bands = [(-54, -47, False), (-47, -42, True), (-42, -32, False), (-32, -27, True), (-27, -15, False), (-15, -10, True), (-10, -4, False)]
    for k, (z0, z1, stub) in enumerate(bands):
        if stub: box(f'end-{nm}-{k}', x0, x1, z0, z1, 3.8, 12, 'brick')   # over a tunnel mouth (the stub below is open to the shell)
        else: box(f'end-{nm}-{k}', x0, x1, z0, z1, TB, 12, 'brick')
    box(f'end-{nm}-corridor', x0, x1, Z0, -54, 0, 12, 'brick')
for k, (z0, z1) in enumerate(((-54, -45), (-43, -33), (-31, -21), (-19, -9), (-7, -4.4))): box(f'shed-roof-{k}', X0, X1, z0, z1, 12, 12.5, 'metal')
# The stalled train (T2, z -32..-27): 3.4 m wide cars (0.8 m gaps to the platform edges), floor at the platform's height.
CAR_Z = (-31.2, -27.8)
box('train-car1', -30, -12.5, CAR_Z[0], CAR_Z[1], TB, 3.2, 'metal'); box('train-car3', 7, 24.5, CAR_Z[0], CAR_Z[1], TB, 3.2, 'metal')
C2 = (-11.5, 6.0); C2_DOORS = ((-7.0, -5.4), (0.4, 2.0))
box('train-car2-floor', C2[0], C2[1], CAR_Z[0], CAR_Z[1], TB, 0, 'metal')
box('train-car2-roof', C2[0], C2[1], CAR_Z[0], CAR_Z[1], 2.9, 3.2, 'metal')
box('train-car2-end-w', C2[0], C2[0] + 0.15, CAR_Z[0], CAR_Z[1], 0, 2.9, 'metal'); box('train-car2-end-e', C2[1] - 0.15, C2[1], CAR_Z[0], CAR_Z[1], 0, 2.9, 'metal')
for side, (t0, t1) in (('n', (CAR_Z[1] - 0.15, CAR_Z[1])), ('s', (CAR_Z[0], CAR_Z[0] + 0.15))):
    wall(f'train-car2-side-{side}', 'x', t0, t1, C2[0] + 0.15, C2[1] - 0.15, 0, 2.9, [(a, b, 0, 2.1) for a, b in C2_DOORS])
    for k, (a, b) in enumerate(C2_DOORS):   # the door thresholds bridging the 0.8 m platform gap
        if side == 'n': box(f'train-car2-step-n{k}', a, b, CAR_Z[1], -27, -0.1, 0, 'metal')
        else: box(f'train-car2-step-s{k}', a, b, -32, CAR_Z[0], -0.1, 0, 'metal')
for k, (a, b) in enumerate(((-11.2, -7.2), (-5.2, 0.2), (2.2, 5.7))):   # seats along both walls between the doors
    box(f'train-car2-seats-n{k}', a, b, CAR_Z[1] - 0.65, CAR_Z[1] - 0.15, 0, 0.45, 'metal'); box(f'train-car2-seats-s{k}', a, b, CAR_Z[0] + 0.15, CAR_Z[0] + 0.65, 0, 0.45, 'metal')
for k, x in enumerate((-12.5, 6.0)): box(f'train-coupling-{k}', x, x + 1.0, -30.6, -28.4, TB, 1.2, 'metal')   # the couplings close the gaps between the cars
box('wagon', -30, -20, -46.5, -42.5, TB, 2.0, 'metal')             # a freight wagon off the rails in the flooded west end of T3
box('cabinet-p3', 30, 31.2, -33.2, -32.4, 0, 1.6, 'metal'); box('cabinet-p2', -14, -12.8, -16.2, -15.4, 0, 1.6, 'metal')   # signal cabinets (spark hazard)
box('bench-p2', 2, 6, -21.3, -20.7, 0, 0.5, 'wood'); box('bench-p3', -6, -2, -37.3, -36.7, 0, 0.5, 'wood')
box('kiosk-p2', 8, 12, -18.5, -15.5, 0, 2.6, 'wood')
# Platform end steps into the throats: P2 and P3 east and west (4 m), P1 into T1 at both ends (3 m).
for s, nm in ((-1, 'w'), (1, 'e')):
    for p, zc in (('p2', -21.0), ('p3', -37.0)): stair(f'steps-{p}-{nm}', (s * 41.0, TB, zc), (s * 38.0, 0, zc), 4.0, TB, step=0.3)
    stair(f'steps-p1-{nm}', (s * 36.5, TB, -13.0), (s * 36.5, 0, -10.0), 3.0, TB, step=0.3)
# Footbridges (y 6): the west from the west balcony to P4 (stairs to P2, P3, P4); the east from the east balcony into the
# signal box (stairs to P2, P3). Parapets 1.1 m, openings at the stair heads.
for s, nm, zend, heads in ((-1, 'w', -53.0, (-21.0, -37.0, -50.0)), (1, 'e', -50.0, (-21.0, -37.0))):
    xa, xb = (-36, -32) if s < 0 else (32, 36)
    box(f'bridge-{nm}', xa, xb, zend, -4, 5.5, L1, 'metal')
    outer, inner = ((xa, xa + 0.2), (xb - 0.2, xb)) if s < 0 else ((xb - 0.2, xb), (xa, xa + 0.2))
    box(f'bridge-{nm}-parapet-o', outer[0], outer[1], zend, -4, L1, L1 + 1.1, 'metal')
    wall(f'bridge-{nm}-parapet-i', 'z', inner[0], inner[1], zend, -4, L1, L1 + 1.1, [(z - 1.5, z + 1.5, L1, L1 + 1.1) for z in heads])
    if s < 0: box(f'bridge-{nm}-end', xa + 0.2, xb - 0.2, zend, zend + 0.2, L1, L1 + 1.1, 'metal')
    for z in heads: stair(f'bstair-{nm}{-int(z)}', (s * 20.0, 0, z), (s * 32.0, L1, z), 3.0, 0)
    for k, zp in enumerate((-7.3, -25.6, -16.6, -40.6, -33.6) + ((-47.6,) if s < 0 else ())):
        for j, (a, b) in enumerate(((xa, xa + 0.6), (xb - 0.6, xb))): box(f'pier-{nm}-{k}{j}', a, b, zp, zp + 0.6, 0, 5.5, 'metal')
# The south wall (P4 / corridor), full height but for two doors and the signal box's crossing (x 30..42 above 5.5).
wall('wall-s', 'x', -54, -53, -44, 44, TB, 12, [(-14, -11, 0, DOOR_H), (8, 11, 0, DOOR_H), (30, 42, 5.5, 12)], 'brick')

# =====================================================================================================================
# THE MAINTENANCE CORRIDOR (y 0, z -60..-54, ceiling 4.5): the pump room (west, behind a partition with a 3 m door), the
# generator (east), the back stair up to the signal box landing.
CEIL_C = (4.5, 5.0)
for k, (a, b, c, d) in enumerate(((-44, 16, Z0, -54), (16, 26, Z0, -58.5), (16, 26, -55.5, -54), (26, 44, Z0, -54))): box(f'corridor-ceiling-{k}', a, b, c, d, *CEIL_C, 'stone')
wall('pump-wall', 'z', -30.4, -30, Z0, -54, 0, CEIL_C[0], [(-58.5, -55.5, 0, DOOR_H)], 'stone')
box('pumps', -38, -34, -55.6, -54, 0, 1.6, 'metal'); box('generator', 34, 40, -59.5, -57, 0, 2.2, 'metal')
stair('stair-corridor', (14.0, 0, -57.0), (26.0, L1, -57.0), 3.0, 0)
box('corridor-rail-s', 16, 26, -58.7, -58.5, CEIL_C[1], CEIL_C[1] + 1.1, 'metal'); box('corridor-rail-n', 16, 26, -55.5, -55.3, CEIL_C[1], CEIL_C[1] + 1.1, 'metal')   # keep pilots off the ceiling
# THE SIGNAL BOX (y 6, x 30..42, z -59..-50): the east footbridge enters its north door; the corridor stair's landing
# reaches its west door. The lever frame inside (cover, the proposed objective).
box('signal-landing', 26, 30, Z0, -55, 5.5, L1, 'stone'); box('signal-landing-rail', 26, 30, -55.2, -55, L1, L1 + 1.1, 'metal')
box('signal-landing-rail-ws', 26, 26.2, Z0, -58.5, L1, L1 + 1.1, 'metal'); box('signal-landing-rail-wn', 26, 26.2, -55.5, -55.2, L1, L1 + 1.1, 'metal')
box('signal-floor', 30, 42, Z0, -50, 5.5, L1, 'stone'); box('signal-roof', 30, 42, Z0, -50, 9.5, 9.8, 'metal')
wall('signal-wall-n', 'x', -50.25, -50, 30, 42, L1, 9.5, [(32.4, 35.6, L1, L1 + 3.4)], 'metal')
box('signal-wall-s', 30, 42, Z0, -59.75, L1, 9.5, 'metal'); box('signal-wall-e', 41.75, 42, -59.75, -50.25, L1, 9.5, 'metal')
wall('signal-wall-w', 'z', 30, 30.25, -59.75, -50.25, L1, 9.5, [(-58.5, -56.6, L1, L1 + 2.2)], 'metal')
box('signal-levers', 33, 39, -56, -55.2, L1, L1 + 1.2, 'metal')

# =====================================================================================================================
# HULK BREACH PANELS (bricked arches 3.2 x 4 x 0.4 m proud of a solid face): the concourse's west and east walls, the pump
# room's end wall.
BREACH_W, BREACH_H, BREACH_T = 3.2, 4.0, 0.4
breaches = []
def breach_panel(name, face, u, sign, y=0.0, axis='x'):
    f0, f1 = sorted((face, face + sign * BREACH_T)); a, b = u - BREACH_W / 2, u + BREACH_W / 2
    blk = box('breach-' + name, f0, f1, a, b, y, y + BREACH_H, 'brick') if axis == 'x' else box('breach-' + name, a, b, f0, f1, y, y + BREACH_H, 'brick')
    breaches.append((name, face, axis, u, sign, blk))
breach_panel('concourse-w', -40.0, 10.6, 1); breach_panel('concourse-e', 40.0, 9.4, -1); breach_panel('pump-room', -44.0, -57.0, 1)

# =====================================================================================================================
# RECORDS. The exit door's anchor faces out (+z, yaw 0); an instance exit's interaction, approach and entries are inside.
ENTRIES_IN = [pt(x, 0, z, 180.0) for z in (32.8, 31.4) for x in (-3.6, -2.1, 2.1, 3.6)]
CACHES = [pt(-15.2, 0, 29.5, 90.0), pt(0.0, L1, 33.2, 180.0), pt(38.6, L1, -53.0, 270.0)]   # the ticket office, the station master, the signal box
BOSS_POINTS = [pt(0, 0, 3.0, 0.0), pt(-12.0, 0, 6.5, 20.0), pt(12.0, 0, 6.5, 340.0)]       # the concourse under the departure board
def door_record():
    fx, fz = forward(YAW); s = -1.0; cx, cz = DOOR
    return {'anchor': f'{AREA}:door:exit',
            'interact': vol(cx + s * fx * 1.1, 0, cz + s * fz * 1.1, 3.0, 3.0, 2.0, YAW),
            'closure': vol(cx + s * fx * 0.25, 0, cz + s * fz * 0.25, DOOR_W, DOOR_H, 0.4, YAW),
            'approach': pt(cx + s * fx * 1.6, 0, cz + s * fz * 1.6), 'entries': ENTRIES_IN}

# ENEMY SPAWNS (PveEntry shape plus `allow`; existing kinds only, so Astra's parser accepts them). `station` names the new
# station enemies each entrance proposes (docs/EXPEDITION-CENTRAL-STATION.md "Astra contract"); the parser ignores it.
entries = []
def entry(kind, name, frm, ext, facing, allow, surface='', height=0.0, clear=1.2, hulk=None, level=0, extra=None, station=()):
    d = dict(e=0, id=f'{AREA}:spawn:{kind}-{name}', kind=kind, surface=surface, **{'from': v(*frm), 'exit': v(*ext)}, yaw=yaw_rad(*facing),
             level=level, song=-1, pit=False, height=r4(height), clear=r4(clear))
    if hulk is not None: d['hulk'] = hulk
    if extra: d.update(extra)
    d['allow'] = list(allow)
    if station: d['station'] = list(station)
    entries.append(d)
def door(name, face, axis, u, sign, allow=SMALL, out=2.4, y=0.0, level=0, station=()):
    if axis == 'x': frm, ext, fac = (face - sign * 0.6, y, u), (face + sign * out, y, u), (sign, 0)
    else: frm, ext, fac = (u, y, face - sign * 0.6), (u, y, face + sign * out), (0, sign)
    entry('door', name, frm, ext, fac, allow, level=level, station=station)
def vent(name, x, z, dx, dz, y=0.0, station=()):
    n = math.hypot(dx, dz); entry('vent', name, (x, y, z), (x + dx / n * 1.2, y, z + dz / n * 1.2), (dx, dz), SMALL, surface='floor', station=station)
def drop(name, x, z, height, dx, dz, hulk=False):
    entry('drop', name, (x, 0, z), (x, 0, z), (dx, dz), ['hulk'] if hulk else ['brute'], height=height, clear=4.5 if hulk else 2.8, hulk=hulk)
# Train doors (the stalled train's sealed cars 1 and 3, both sides): out across the gap onto P2 (north) and P3 (south).
for car, xs_ in (('car1', (-26.0, -17.0)), ('car3', (11.0, 20.0))):
    for k, x in enumerate(xs_):
        entry('door', f'train-{car}-n{k}', (x, 0, -29.5), (x, 0, -24.8), (0, 1), BIG, station=('conductor', 'pileup'))
        entry('door', f'train-{car}-s{k}', (x, 0, -29.5), (x, 0, -34.2), (0, -1), BIG, station=('conductor', 'pileup'))
# Tunnel mouths: out of the dark stubs onto the track bed in the throats.
for s, nm in ((-1, 'w'), (1, 'e')):
    for t, (z0, z1) in TRACKS.items():
        zc = (z0 + z1) / 2; entry('door', f'tunnel-{t}-{nm}', (s * 47.0, TB, zc), (s * 41.6, TB, zc), (-s, 0), BIG, station=('linesman', 'undertow') if t == 't3' else ('linesman',))
# Staff doors: the concourse's side walls, the ticket hall's back corners, the corridor's east end; upstairs, the offices'
# end walls and the balconies' walls.
door('concourse-w-0', -40.0, 'x', 2.0, 1, BIG, station=('conductor',)); door('concourse-w-1', -40.0, 'x', 16.0, 1)
door('concourse-e-0', 40.0, 'x', 3.0, -1, BIG, station=('conductor',)); door('concourse-e-1', 40.0, 'x', 16.0, -1)
door('hall-w', -22.0, 'x', 23.5, 1); door('hall-e', 22.0, 'x', 23.5, -1)
door('corridor-e', 44.0, 'x', -57.0, -1, BIG, station=('linesman',))
door('office-w', -40.0, 'x', 30.0, 1, y=L1, level=1); door('office-e', 40.0, 'x', 29.0, -1, y=L1, level=1)
door('balcony-w', -40.0, 'x', 8.0, 1, y=L1, level=1); door('balcony-e', 40.0, 'x', 6.0, -1, y=L1, level=1)
# Floor grates and drains (the concourse, the platforms, the corridor) and sumps in the flooded track bed.
vent('concourse-w', -34.0, 6.0, 1, 0); vent('concourse-e', 34.0, 12.0, -1, 0); vent('concourse-s', -8.0, 1.0, 0, 1); vent('concourse-n', 8.0, 17.0, 0, -1)
vent('p2-w', -24.0, -18.0, 1, 0); vent('p2-e', 16.0, -24.0, -1, 0); vent('p3-w', -12.0, -36.0, 1, 0); vent('p3-e', 28.0, -41.0, -1, 0)
vent('p4', -7.0, -50.5, 1, 0); vent('corridor-w', -24.0, -57.0, 1, 0); vent('corridor-mid', 2.0, -57.0, -1, 0)
vent('sump-t3', -38.0, -44.5, 1, 0, y=TB, station=('undertow',)); vent('sump-throat-w', -42.5, -25.0, 1, 0, y=TB, station=('undertow',))
# Drops off the roof beams (concourse) and the shed roof (platforms); two Hulk drops through the glass.
drop('concourse-w', -9.0, 17.0, 17.0, 0, -1); drop('concourse-e', 22.0, 9.0, 17.0, -1, 0)
drop('p2', 4.0, -24.5, 11.5, 0, 1); drop('p3', -16.0, -37.5, 11.5, 0, -1)
drop('hulk-concourse-w', -19.0, 5.6, 17.0, 1, 0, hulk=True); drop('hulk-concourse-e', 19.5, 3.0, 17.0, -1, 0, hulk=True)
for name, face, axis, u, sign, blk in breaches:
    c, sz = blk['pos'], blk['size']; n = (sign, 0)
    frm = (face - sign * 1.0, 0, u); ext = (face + sign * (BREACH_T + 2.4), 0, u)
    entry('breach', name, frm, ext, n, ['hulk'], surface='wall', clear=HULK_CLEAR,
          extra=dict(block=blk['id'], center=v(c['x'], c['y'], c['z']), size=v(sz['x'], sz['y'], sz['z']), normal=v(n[0], 0, n[1])))
entries.sort(key=lambda e: e['id'])
for i, e in enumerate(entries): e['e'] = i
DROPS = [dict(id=f'{MAP}-drop-{nm}-{t}', a=v(x, 0, za), b=v(x, TB, zb)) for nm, x in (('w', -14.0), ('e', 14.0))
         for t, za, zb in (('t1-n', -9.6, -12.0), ('t1-s', -15.4, -13.0), ('t3-n', -41.6, -44.0), ('t3-s', -47.4, -45.0))]

# =====================================================================================================================
# THE TOWN SIDE (the hub's frame): the door record the town needs at its reserved transit doorway (0, 0, -84), yaw 0 (out of
# the doorway, north up Station Road), as Ward Zero's: the anchor 0.6 m out, interaction, closure on the boards, approach,
# eight returns past the canopy. Not live: the live manifest keeps exp-transit in hub.reserved.
TOWN_DOOR = (0.0, -84.0); TOWN_YAW = 0.0
TOWN_RETURNS = [pt(x, 0, -84.0 + dz, TOWN_YAW) for dz in (4.4, 6.0) for x in (-3.0, -1.0, 1.0, 3.0)]
def town_door():
    fx, fz = forward(TOWN_YAW); cx, cz = TOWN_DOOR
    return dict(anchor=dict(id=f'{TOWN}:door:{AREA}', kind='door', building=AREA, **pt(cx + fx * 0.6, 0, cz + fz * 0.6, TOWN_YAW)),
                door={'anchor': f'{TOWN}:door:{AREA}', 'interact': vol(cx + fx * 1.0, 0, cz + fz * 1.0, 3.0, 3.0, 2.0, TOWN_YAW),
                      'closure': vol(cx + fx * 0.2, 0, cz + fz * 0.2, DOOR_W, DOOR_H, 0.4, TOWN_YAW),
                      'approach': pt(cx + fx * 1.6, 0, cz + fz * 1.6), 'returns': TOWN_RETURNS})

# =====================================================================================================================
# PROPOSALS (Astra owns all authority; nothing here is read): PvE machines, the hazards, the station enemies' entrances.
def proposals():
    H = lambda id, kind, V, **kw: dict(id=f'{MAP}-{id}', kind=kind, volume=V, **kw)
    return {
        'machines': [dict(id='chip-0', kind='chip', **pt(-35.0, 0, 13.5, 90.0)), dict(id='med-0', kind='med', **pt(-6.0, 0, -16.2, 180.0)),
                     dict(id='recruit-0', kind='recruit', **pt(35.0, 0, 7.0, 270.0)), dict(id='chip-1', kind='chip', **pt(-30.0, L1, 33.0, 180.0))],
        'hazards': [
            H('express', 'train', vol(0, TB, -12.5, 95.8, 5.2, 5.0, 0), line='t1', proposal=True, note='the scheduled express: warn 4 s (signal red, rails sing, headlight in the tunnel), pass 2.2 s end to end, lethal in the pit'),
            H('rail-t1-w', 'third-rail', vol(-23.0, TB, -12.5, 42, 0.6, 1.2, 0), proposal=True, note='live rail strip along the pit centre, gapped at the barrow crossing; cut by the signal box levers'),
            H('rail-t1-e', 'third-rail', vol(23.0, TB, -12.5, 42, 0.6, 1.2, 0), proposal=True),
            H('rail-t2', 'third-rail', vol(-37.0, TB, -29.5, 14, 0.6, 1.2, 0), proposal=True, note='live only beyond the stalled train (the train shorted the middle)'),
            H('rail-t3-w', 'third-rail', vol(-11.0, TB, -44.5, 18, 0.6, 1.2, 0), proposal=True, note='arcs into the flood when live'),
            H('rail-t3-e', 'third-rail', vol(23.0, TB, -44.5, 42, 0.6, 1.2, 0), proposal=True),
            H('flood-t3', 'flood', vol(-32.0, TB, -44.5, 24, 0.45, 5.0, 0), proposal=True, note='0.35 m of water on the bed: slows, conducts the rail when it arcs'),
            H('flood-throat-w', 'flood', vol(-41.0, TB, -36.0, 6, 0.45, 22.0, 0), proposal=True, note='the west throat'),
            H('cabinet-p2', 'spark', vol(-13.4, 0, -15.8, 3.2, 2.0, 2.8, 0), proposal=True, note='a sparking signal cabinet: a 1 s fizz warning, then a 2 m arc'),
            H('cabinet-p3', 'spark', vol(30.6, 0, -32.8, 3.2, 2.0, 2.8, 0), proposal=True),
            H('steam-corridor', 'gas', vol(-6.0, 0, -57.0, 10, 3.0, 4.0, 0), proposal=True, note='a burst steam main (the Ward gas kind): 2 s hiss, 3 s scald'),
            H('glass-concourse', 'falling-glass', vol(0, 0, 8.0, 60, 18.0, 18, 0), proposal=True, note='boss encounter only: shard falls with a 1.2 s shadow telegraph')],
        'entranceKinds': {'train': 'a stalled carriage door sliding open (today: door)', 'tunnel': 'out of a tunnel mouth along the rails (today: door)',
                          'sump': 'surfacing from flood water (today: vent, floor)'},
    }

# =====================================================================================================================
# THE LOOK (a MapLooks.json-shaped entry; not loaded): night, failing power. Moonlight through the broken glass roof (the
# only key), sodium on the platforms, the teal of the station's signs, departure-board amber and cyan, emergency red.
def look():
    L = lambda id, x, y, z, color, intensity, rng, flicker='', rate=0, period=0, length=0, depth=0, phase=0: dict(
        id=id, flicker=flicker, pos=dict(x=x, y=y, z=z), color=color, intensity=intensity, range=rng, rate=rate, period=period, length=length, depth=depth, phase=phase)
    BL = lambda block, look_, color=0, note='': dict(block=block, look=look_, color=color, band=False, note=note)
    P = MAP + '-'
    return {'id': MAP, 'look': LOOK, 'farClip': 260, 'background': 0x070B0D,
            'key': dict(color=0x8FAEDC, intensity=0.42, elevation=62, bearing=205, **{'from': dict(x=0, y=0, z=0)}, shadows='soft', shadowStrength=0.9),
            'rim': dict(color=0x3E6A7A, intensity=0.14, elevation=18, bearing=25, **{'from': dict(x=0, y=0, z=0)}, shadows='none', shadowStrength=1),
            'ambient': dict(sky=0x2A3E58, skyScale=0.075, equator=0x24302E, equatorScale=0.06, ground=0x16120E, groundScale=0.055),
            'fog': dict(mode='linear', color=0x0B1517, skyLerp=0, start=14, end=120, density=0),
            'shadows': dict(cascades=2, distance=60, fallbackCascades=2, fallbackDistance=45),
            'sky': dict(variant='night', horizon=0x16222C, zenith=0x05080C, glow=0x6F8AB0, disc=0xDDE6F0, cloud=0x2A3440, discSize=0.035, halo=0.12,
                        stars=0.35, crater=0.3, cloudSpeed=0.3, elevation=62, bearing=205, dir=dict(x=0, y=0, z=0)),
            'lights': [L('departures', 0, 8.0, 11.0, 0xFFB45A, 1.5, 18, 'stutter', period=7, length=0.18, depth=0.5),
                       L('hall-tube', 0, 4.9, 28.0, 0xD8FFF0, 0.9, 14, 'stutter', period=11, length=0.3, depth=0.7, phase=3),
                       L('sodium-p2', -12.0, 5.2, -21.0, 0xFF9E3A, 1.2, 16), L('sodium-p3', 14.0, 5.2, -37.0, 0xFF9E3A, 1.1, 15, 'stutter', period=13, length=0.4, depth=0.8),
                       L('signal-box', 36.0, 8.4, -54.5, 0xFF2A22, 1.0, 12), L('tunnel-w-green', -44.5, 2.6, -12.5, 0x3AFF6A, 0.7, 9),
                       L('emergency-corridor', -10.0, 4.0, -57.0, 0xFF3A2A, 0.8, 12, 'stutter', period=5, length=0.15, depth=0.4),
                       L('cafe-fire', -17.0, 1.2, 10.8, 0xFF7A2E, 1.1, 9, 'fire', rate=5, depth=0.35)],
            'kitLooks': [], 'dressing': '', 'backdrop': '', 'atmosphere': '', 'ambience': '',
            'blocks': dict(dressed=True, dust=0.08, haze=0.42, hazeTop=2.6, hazeColor=0x1E2A2A),
            'blockLooks': [
                BL(P + 'floor-*', 'ward-tile', 0x7A7468, 'concourse and hall terrazzo (large worn tiles)'),
                BL(P + 'track-bed', 'tar', 0x1E1C1A, 'ballast'), BL(P + 'plat-*', 'concrete-board', 0x5A5852, 'platforms (edge stripe is kit)'),
                BL(P + 'mass-c*', 'brick-buff', 0x8A6A52, 'concourse side walls'), BL(P + 'mass-n*', 'ward-tile', 0xC8BCA0, 'hall walls: the facade cream tile'),
                BL(P + 'end-*', 'brick-red', 0x4A3028, 'shed end walls and tunnel mouths'), BL(P + 'wall-s*', 'ward-tile', 0xB8B0A0, 'the far platform wall'),
                BL(P + 'shell-*', 'brick-red', 0x3A2A24), BL(P + 'hall-ceiling', 'concrete-board', 0x4A4844), BL(P + 'upper-roof', 'concrete-board', 0x3A3836),
                BL(P + 'deck-*', 'cyber-metal', 0x34443E, 'balconies (painted iron decks; the green ironwork is kit)'), BL(P + 'landing-*', 'cyber-metal', 0x34443E),
                BL(P + 'bridge-*-parapet-*', 'cyber-metal', 0x2A3A34, 'footbridge parapets'), BL(P + 'bridge-*-end', 'cyber-metal', 0x2A3A34), BL(P + 'bridge-*', 'cyber-grate', 0x3A4440, 'footbridge grating decks'), BL(P + 'post-*', 'steel-green', 0x2E463C), BL(P + 'pier-*', 'steel-green', 0x2E463C),
                BL(P + 'rail-*', 'cyber-metal', 0x2E3A36), BL(P + 'gallery-rail', 'cyber-metal', 0x2E3A36), BL(P + 'barrier-*', 'cyber-metal', 0x3A4642),
                BL(P + 'roof-beam-*', 'steel-green', 0x24342E), BL(P + 'shed-roof-*', 'concrete-board', 0x24262A, 'the sooty shed roof (skylights between)'), BL(P + 'screen', 'cyber-glass', 0x1A2A34, 'the shed screen glazing'),
                BL(P + 'stair-*', 'concrete-board', 0x7A766C), BL(P + 'bstair-*', 'concrete-board', 0x6E6A62), BL(P + 'steps-*', 'concrete-board', 0x5E5A54), BL(P + 'crossing-*', 'timber', 0x4A3A2A, 'barrow crossings (boards over the ballast)'),
                BL(P + 'train-*', 'cyber-panel', 0x8A9AA0, 'stainless carriages (the red line and windows are kit)'), BL(P + 'wagon', 'corrugated', 0x5A3A2A),
                BL(P + 'booth', 'timber', 0x5A3E2A), BL(P + 'kiosk-*', 'timber', 0x5A3E2A), BL(P + 'benches-*', 'timber', 0x6A4A30), BL(P + 'bench-*', 'timber', 0x6A4A30),
                BL(P + 'hall-gates-*', 'cyber-metal', 0x4A5450), BL(P + 'hall-office', 'timber', 0x4A3424), BL(P + 'hall-counter', 'timber', 0x6A4A30),
                BL(P + 'hall-machines', 'cyber-panel', 0x2A3A44), BL(P + 'hall-column-*', 'ward-tile', 0xC8BCA0), BL(P + 'hall-sandbags-*', 'plain', 0x6A6248),
                BL(P + 'girder', 'steel-green', 0x24342E), BL(P + 'trolley-*', 'cyber-metal', 0x5A6460), BL(P + 'cabinet-*', 'steel-green', 0x2A3A30),
                BL(P + 'exit-*', 'cyber-metal', 0x3A4642),
                BL(P + 'corridor-ceiling-*', 'concrete-board', 0x3A3836), BL(P + 'corridor-rail-*', 'cyber-metal', 0x2E3A36), BL(P + 'pump-wall*', 'concrete-board', 0x4A4844), BL(P + 'pumps', 'steel-green', 0x2A4A3A),
                BL(P + 'generator', 'demolition', 0, 'the generator housing in hazard paint'),
                BL(P + 'signal-wall-*', 'ward-panel', 0x4A5A50, 'the signal box walls (glazing is kit)'), BL(P + 'signal-floor', 'cyber-metal', 0x34443E), BL(P + 'signal-landing', 'cyber-metal', 0x34443E), BL(P + 'signal-roof', 'corrugated', 0x3A3C3A), BL(P + 'signal-*', 'steel-green', 0x3E5A4E, 'the signal box rails and levers'), BL(P + 'office-*', 'ward-panel', 0x9A9484), BL(P + 'breach-*', 'brick-red', 0x6A3A2E, 'bricked arches')]}

# =====================================================================================================================
def game_map():
    sp = [dict(pos=v(p['x'], 1.1, p['z']), yaw=180.0) for p in ENTRIES_IN]
    return dict(id=MAP, name='Central Station (Expedition)', tag='Central Station. The last train left at 6:47.', theme='expedition', music=None,
                ambience='', look=LOOK, sky=0x16222C, floor=0x5C5850, accent=0x7AE8D0, rotation=False, bounds=dict(BOUNDS),
                spawns=dict(ffa=sp, teams=[dict(side=0, points=sp[:4]), dict(side=1, points=sp[4:])]),
                blocks=list(blocks), segments=[], hazards=[], pilotBlocks=[], nav=dict(nodes=[], links=[]),
                ramps=list(ramps), navHints=dict(drops=DROPS, safe=[], collisionOff=list(visual)))
def area():
    fx, fz = forward(YAW)
    anchors = [dict(id=f'{AREA}:door:exit', kind='door', building=AREA, **pt(DOOR[0] - fx * 0.6, 0, DOOR[1] - fz * 0.6, YAW))]
    anchors += [dict(id=f'{AREA}:cache:{i}', kind='chest', building='', **c) for i, c in enumerate(CACHES)]
    def front(p, d):
        fx, fz = forward(p['yaw']); return dict(x=r4(p['x'] + fx * d), y=p['y'], z=r4(p['z'] + fz * d))
    return {'id': AREA, 'role': 'instance', 'map': MAP, 'blockHash': block_hash(game_map()),
            'anchors': anchors, 'doors': [door_record()],
            'interactions': [dict(anchor=f'{AREA}:cache:{i}', **front(c, 0.8), range=2.0) for i, c in enumerate(CACHES)],
            'spawns': entries, 'boss': {'points': BOSS_POINTS}}
def draft():
    td = town_door()
    return dict(schema=1, draft=True, revision='exp-transit-draft-1',
                note='Central Station design draft from docs/arenas/scripts/expedition_central_station.py: NOT LIVE (nothing at runtime loads this file; '
                     'Resources/UnityMaps.json, ExpeditionLayout.json and MapLooks.json are unchanged). See docs/EXPEDITION-CENTRAL-STATION.md.',
                materials=[], maps=[game_map()], layout=dict(instance=AREA, area=area(), town=dict(area=TOWN, anchor=td['anchor'], door=td['door'])),
                look=look(), proposals=proposals())

# =====================================================================================================================
# CHECKS
def colliders(m):
    off = set(m['navHints']['collisionOff']); return [b for b in m['blocks'] if b['id'] not in off]
def overlaps(bl):
    """Pairs of colliding blocks whose volumes overlap by more than 1 cm in every axis (touching faces are fine). Yaw-rotated
    blocks use a separating-axis test on their footprints."""
    out = []
    def axes(b):
        a = math.radians(2 * math.degrees(math.atan2(b['rotation']['y'], b['rotation']['w']))); return [(math.cos(a), -math.sin(a)), (math.sin(a), math.cos(a))]
    def proj(b, ax):
        ps = [x * ax[0] + z * ax[1] for x, z in corners(b)]; return min(ps), max(ps)
    fps = [footprint(b) for b in bl]
    for i in range(len(bl)):
        for j in range(i + 1, len(bl)):
            a, b = bl[i], bl[j]; fa, fb = fps[i], fps[j]
            if fa[1] <= fb[0] + 0.01 or fb[1] <= fa[0] + 0.01 or fa[3] <= fb[2] + 0.01 or fb[3] <= fa[2] + 0.01: continue
            ya, yb = yspan(a), yspan(b)
            if ya[1] <= yb[0] + 0.01 or yb[1] <= ya[0] + 0.01: continue
            sep = False
            for ax in axes(a) + axes(b):
                p, r = proj(a, ax), proj(b, ax)
                if p[1] <= r[0] + 0.01 or r[1] <= p[0] + 0.01: sep = True; break
            if not sep: out.append((a['id'], b['id']))
    return out
def walk(col, starts):
    """Walking distances (m) over the collider's floor from `starts`, 8-connected (no corner cutting)."""
    import heapq
    dist = {s: 0.0 for s in starts}; heap = [(0.0, s) for s in starts]
    while heap:
        d, n = heapq.heappop(heap)
        if d > dist.get(n, 1e18): continue
        i, j, y = n
        for di in (-1, 0, 1):
            for dj in (-1, 0, 1):
                if not di and not dj: continue
                ms = [m for m in col.by_cell.get((i + di, j + dj), ()) if abs(m[2] - y) <= 0.36]
                if not ms: continue
                if di and dj and not (col.by_cell.get((i + di, j)) and col.by_cell.get((i, j + dj))): continue
                nd = d + col.cell * (1.41421356 if di and dj else 1.0)
                for m in ms:
                    if nd < dist.get(m, 1e18): dist[m] = nd; heapq.heappush(heap, (nd, m))
    return dist
FLOORS = (('ground', 0.0), ('upper', L1), ('track bed', TB))
def checks():
    out, ok = [], True
    m = game_map(); bl = colliders(m)
    keep = lambda y: -1.25 <= y <= m['bounds']['ceiling'] - 1
    col = Collider(bl, m['ramps'], m['bounds']); col.build(keep)
    starts = []
    for i, s in enumerate(ENTRIES_IN):
        n = col.node_at(s['x'], s['y'], s['z']); assert n, f'entry slot {i} not on walkable floor'; starts.append(n)
    reach = col.flood(starts)
    def floor(n, y): return abs(n[2] - y) < 0.05
    for lvl, y in FLOORS:
        tot = [n for n in col.graph if floor(n, y)]; got = [n for n in tot if n in reach]
        out.append(f'{lvl} floor (y {y:g}): {len(tot)} standable 0.5 m cells ({len(tot) * 0.25:.0f} m2), {len(got)} reached from the entry slots ({100 * len(got) / max(1, len(tot)):.1f}%)')
        if len(got) != len(tot):
            ok = False
            for g in components(col, [n for n in tot if n not in reach])[:8]: out.append('   UNREACHED %d cells round (%.1f, %.1f)' % g)
    other = sorted({n[2] for n in col.graph if not any(floor(n, y) for _, y in FLOORS)})
    out.append(f'other standable tops (cover, jumped onto, not walking floor): {len(other)} heights, {sum(1 for n in col.graph if n[2] in other)} cells, highest {max(other) if other else 0:g} m')
    ids = [b['id'] for b in m['blocks']]
    out.append(f'blocks {len(ids)} ({len(bl)} colliding, {len(visual)} visual steps), ramps {len(m["ramps"])}, all {MAP}-*: {all(i.startswith(MAP + "-") for i in ids)}; ids unique: {len(set(ids)) == len(ids)}; yaw-only colliders: {all(abs(b["rotation"]["x"]) < 1e-9 and abs(b["rotation"]["z"]) < 1e-9 for b in bl)}')
    ok &= len(set(ids)) == len(ids) and all(i.startswith(MAP + '-') for i in ids)
    ov = overlaps(bl)
    for r in m['ramps']:                                     # a block standing inside a stair's wedge (under its tread)
        rb_ = ramp_block(r); L = math.hypot(r['high']['x'] - r['low']['x'], r['high']['z'] - r['low']['z'])
        for b in bl:
            if dist_xz(b, rb_['pos']['x'], rb_['pos']['z']) > L: continue
            y0 = yspan(b)[0]; hit = False
            for gx in range(-4, 5):
                for gz in range(-4, 5):
                    x = b['pos']['x'] + gx / 4.0 * (b['size']['x'] / 2 - 0.01); z = b['pos']['z'] + gz / 4.0 * (b['size']['z'] / 2 - 0.01)
                    h = Collider.wedge(r, x, z)
                    if h is not None and y0 < h - 0.01 and yspan(b)[1] > r['bottom'] + 0.01 and dist_xz(rb_, x, z) < -0.0 + 1e-9:
                        lx, lz = (x - r['low']['x']), (z - r['low']['z'])
                        if abs(-(lx) * (r['high']['z'] - r['low']['z']) / L + lz * (r['high']['x'] - r['low']['x']) / L) < r['width'] / 2 - 0.01: hit = True
            if hit: ov.append((r['id'], b['id']))
    out.append(f'colliding blocks overlapping (more than 1 cm in every axis): {len(ov)}' + ''.join(f'\n   BAD {a} x {b}' for a, b in ov[:12])); ok &= not ov
    rb = all(r['width'] > 0.84 and r['bottom'] <= r['low']['y'] and r['high']['y'] > r['low']['y'] and (r['high']['y'] - r['low']['y']) / math.hypot(r['high']['x'] - r['low']['x'], r['high']['z'] - r['low']['z']) < 1
             and r['blocks'] and all(i in visual for i in r['blocks']) for r in m['ramps'])
    out.append(f'ramps: {len(m["ramps"])} (slope under 1, wider than a pilot, steps visual only): {rb}; steepest {max((r["high"]["y"] - r["low"]["y"]) / math.hypot(r["high"]["x"] - r["low"]["x"], r["high"]["z"] - r["low"]["z"]) for r in m["ramps"]):.2f}'); ok &= rb
    out.append(f'blockHash {block_hash(m)}')
    # Records on reached floor at their own level, with clearance.
    d = door_record(); a = area()
    def check_points(label, pts, need, yhi=HEIGHT + 0.05):
        bad = []; worst = 99
        for name, p in pts:
            n = col.node_at(p['x'], p['y'], p['z']); f = n is not None and n in reach and col.floor_under(p['x'], p['z'], p['y'])
            c = col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + yhi); worst = min(worst, c)
            if not f or c < need: bad.append(f"{name} ({p['x']:g}, {p['y']:g}, {p['z']:g}) floor {f} clear {c:.2f} ({col.nearest_block(p['x'], p['z'], p['y'] + 0.35, p['y'] + yhi)[1]})")
        out.append(f'{label}: {len(pts)} on reached floor at their own level with {need:g} m clear: {len(pts) - len(bad)} (tightest {worst:.2f} m)' + ''.join('\n   BAD ' + s for s in bad))
        return not bad
    ok &= check_points('entry slots', [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)], PILOT_CLEAR)
    ok &= check_points('entry slots (Hulk probe)', [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)], HULK_SLOT, yhi=HULK_HEIGHT + 0.05)
    ok &= check_points('door approach', [('approach', d['approach'])], PILOT_CLEAR)
    ok &= check_points('cache anchors (piece footprint)', [(c['id'], c) for c in a['anchors'] if c['kind'] == 'chest'], 1.0)
    ok &= check_points('interaction points', [(c['anchor'], c) for c in a['interactions']], PILOT_CLEAR)
    ok &= check_points('boss points (Hulk-sized)', [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)], HULK_CLEAR, yhi=HULK_BAND)
    pm = proposals()['machines']
    ok &= check_points('proposed machine spots (piece footprint)', [(x['id'], x) for x in pm], 1.0)
    ok_d = in_vol(d['interact'], d['approach']['x'], 0.0, d['approach']['z']) and not any(in_vol(d['interact'], p['x'], 0.0, p['z']) for p in ENTRIES_IN)
    ok_d &= col.floor_under(d['interact']['x'], d['interact']['z'], 0.0) and col.floor_under(d['closure']['x'], d['closure']['z'], 0.0)
    jam = [b for b in m['blocks'] if 'exit-jamb' in b['id']]; cl = vol_block('', d['closure'])
    gap = min(dist_xz(b, x, z) for b in jam for x, z in corners(cl)); ok_d &= gap < 0.05
    vb = all(vol_in_bounds(V, m['bounds']) for V in (d['interact'], d['closure']))
    out.append(f'exit door: doorway ({DOOR[0]:g}, 0, {DOOR[1]:g}) yaw {YAW:g}, anchor 0.6 m inside, closure {d["closure"]["sx"]:g} x {d["closure"]["sy"]:g} m between the jambs, approach inside the interaction volume, entries outside it, volumes inside the bounds: {ok_d and vb}'); ok &= ok_d and vb
    sl = min(math.hypot(p['x'] - q_['x'], p['z'] - q_['z']) for i, p in enumerate(ENTRIES_IN) for q_ in ENTRIES_IN[i + 1:])
    out.append(f'entry slots: closest pair {sl:.2f} m'); ok &= sl >= 1.4 - 1e-6
    # Routes from entry 0, pilot and Hulk (the loader's gate): every entry, boss point and the approach.
    hulk = Collider(bl, m['ramps'], m['bounds']); hulk.build(keep, HULK_RADIUS, HULK_HEIGHT)
    o = ENTRIES_IN[0]; rp = col.flood([col.node_at(o['x'], o['y'], o['z'])]); rh = hulk.flood([hulk.node_at(o['x'], o['y'], o['z'])])
    need = ENTRIES_IN + BOSS_POINTS + [d['approach']]
    miss = [p for p in need if hulk.node_at(p['x'], p['y'], p['z']) not in rh or col.node_at(p['x'], p['y'], p['z']) not in rp]
    out.append(f'Hulk-sized and pilot routes from entry 0 to every entry, boss point and the approach: {len(need) - len(miss)} of {len(need)}'); ok &= not miss
    hfloor = {lvl: [n for n in hulk.graph if floor(n, y)] for lvl, y in FLOORS}
    out.append('Hulk-standable floor reached from entry 0: ' + ', '.join(f'{lvl} {sum(1 for n in ns if n in rh)} of {len(ns)} cells' for lvl, ns in hfloor.items()))
    # Spawns: exits on reached floor at their level with their clear radius, routed back with their capsule; spacing.
    sp = []
    for e in entries:
        x = e['exit']; n = col.node_at(x['x'], x['y'], x['z'], 0.6); f = n is not None and n in reach and col.floor_under(x['x'], x['z'], x['y'])
        hk = 'hulk' in e['allow']; yhi = e['height'] if e['kind'] == 'drop' else (HULK_BAND if hk else HEIGHT + 0.05)
        skip = {e['block']} if e['kind'] == 'breach' else set()
        c = col.clearance(x['x'], x['z'], x['y'] + (0.05 if e['kind'] == 'drop' else 0.35), x['y'] + yhi - (0.05 if e['kind'] == 'drop' else 0), skip=skip)
        sp.append((e, f, c))
    bad = [f"{e['id']} floor {f} clear {c:.2f} < {e['clear']:g} ({col.nearest_block(e['exit']['x'], e['exit']['z'], e['exit']['y'] + 0.05, e['exit']['y'] + 4)[1]})" for e, f, c in sp if not f or c + 1e-6 < e['clear']]
    kinds = {}
    for e in entries: kinds[e['kind']] = kinds.get(e['kind'], 0) + 1
    out.append(f'spawn exits: {len(entries)} ({", ".join(f"{k} {n}" for k, n in sorted(kinds.items()))}) on reached floor at their own level with their clear radius: {len(entries) - len(bad)}' + ''.join('\n   BAD ' + s for s in bad)); ok &= not bad
    lv = all((e['level'] == 1) == (e['exit']['y'] > 3) for e in entries)
    out.append(f'spawn levels (1 on the upper floor, else 0): {lv}'); ok &= lv
    def allows(e, kind): return (e['kind'] == 'breach' or e['kind'] == 'drop' and e.get('hulk')) if kind == 'hulk' else e['kind'] != 'breach' and (e['kind'] != 'drop' or kind == 'brute' and not e.get('hulk'))
    rule = all(e['allow'] and all(allows(e, k) for k in e['allow']) for e in entries)
    out.append(f'allow lists within the entry rules (Hulk: breach or Hulk drop; drops: brute; doors and vents: shambler, runner, spitter, brute): {rule}'); ok &= rule
    miss = [e['id'] for e in entries if col.node_at(e['exit']['x'], e['exit']['y'], e['exit']['z']) not in rp]
    out.append(f'spawn exits routed to entry 0 for the pilot probe: {len(entries) - len(miss)} of {len(entries)}' + ''.join('\n   BAD ' + i for i in miss)); ok &= not miss
    hk = [e for e in entries if 'hulk' in e['allow']]
    missh = [e['id'] for e in hk if hulk.node_at(e['exit']['x'], e['exit']['y'], e['exit']['z'], 0.36) not in rh]
    out.append(f'Hulk spawn exits ({len(hk)}) Hulk-clear and routed to entry 0 for a Hulk: {len(hk) - len(missh)}' + ''.join('\n   BAD ' + i for i in missh)); ok &= not missh
    bok = all(any(b['id'] == e['block'] for b in m['blocks']) for e in entries if e['kind'] == 'breach')
    behind = []
    for name, face, axis, u, sign, blk in breaches:
        x = face - sign * 0.5; solid = any(dist_xz(b, x, u) <= 1e-6 and yspan(b)[0] <= 0.05 and yspan(b)[1] >= BREACH_H for b in bl if b['id'] != blk['id'])
        if not solid: behind.append(name)
    out.append(f'breach panels: {len(breaches)}, static, solid mass behind each: {bok and not behind}' + (f' (open behind: {behind})' if behind else '')); ok &= bok and not behind
    ex = [(e['id'], e['exit']['x'], e['exit']['y'], e['exit']['z']) for e in entries]
    mind = min((math.hypot(p[1] - r[1], p[3] - r[3]), p[0], r[0]) for i, p in enumerate(ex) for r in ex[i + 1:] if abs(p[2] - r[2]) < 1)
    out.append('closest two spawn exits on one level: %.1f m (%s, %s)' % (mind[0], mind[1].split(':')[-1], mind[2].split(':')[-1])); ok &= mind[0] >= 3.0
    recs = [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)] + [('approach', d['approach'])] + [(c['id'], c) for c in a['anchors']]
    md = min((math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']), e['id'], n) for e in entries for n, p in recs if abs(e['exit']['y'] - p['y']) < 1)
    out.append('nearest spawn exit to an entry slot, the door or a cache in a straight line: %.1f m (%s, %s)' % (md[0], md[1].split(':')[-1], md[2])); ok &= md[0] >= 4.0
    dist = walk(col, starts)
    wk = sorted((dist.get(col.node_at(e['exit']['x'], e['exit']['y'], e['exit']['z']), 1e9), e['id']) for e in entries)
    out.append('nearest spawn exits to the entry slots on foot: ' + ', '.join(f'{d_:.1f} m ({i.split(":")[-1]})' for d_, i in wk[:3])); ok &= wk[0][0] >= 12.0
    # Route lengths the doc quotes (walked from the entry slots).
    def at(p): return dist.get(col.node_at(p['x'], p['y'], p['z']), None)
    marks = [('the concourse (boss point 0)', BOSS_POINTS[0]), ('P1 at the crossing', pt(0, 0, -5.5)), ('the station master (cache 1)', CACHES[1]), ('P2, mid-platform', pt(0, 0, -21)),
             ('the stalled train (car 2 aisle)', pt(-3.0, 0, -29.5)), ('P4, mid-platform', pt(0, 0, -50.5)), ('the maintenance corridor', pt(0, 0, -57)),
             ('the signal box (cache 2)', CACHES[2]), ('the east tunnel mouth (T1)', pt(41.6, TB, -12.5))]
    out.append('walked from the entry slots: ' + '; '.join(f'{n} {at(p):.0f} m' if at(p) is not None else f'{n} UNREACHED' for n, p in marks))
    ok &= all(at(p) is not None for _, p in marks)
    # Proposed hazard volumes inside the bounds and 2 m clear of the entries, door, caches and boss points.
    hz = proposals()['hazards']; hb = [h['id'] for h in hz if not vol_in_bounds(h['volume'], m['bounds'])]
    near = []
    for h in hz:
        V = h['volume']
        for n, p in recs + [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)]:
            if h['kind'] == 'falling-glass': continue
            if V['y'] - V['sy'] / 2 < p['y'] + 2 and V['y'] + V['sy'] / 2 > p['y'] and math.hypot(max(abs(p['x'] - V['x']) - V['sx'] / 2, 0), max(abs(p['z'] - V['z']) - V['sz'] / 2, 0)) < 2.0: near.append((h['id'], n))
    out.append(f'proposed hazard volumes: {len(hz)}, inside the bounds: {not hb}; within 2 m of an entry, the door, a cache or a boss point: {len(near)}'); ok &= not hb and not near
    # Long sightlines (platform lanes) and the train lane, reported for the doc.
    out.append(f'sightlines: P2/P3 run {PLAT["p2"][1] - PLAT["p2"][0]:g} m end to end; the express lane (T1) {X1 - X0:g} m between the tunnel stubs; the concourse {80:g} x {24:g} m under the roof beams')
    # ids and the Expedition id rule (anchors, spawns).
    rec = [x['id'] for x in a['anchors']] + [e['id'] for e in entries]
    out.append(f'record ids unique {len(set(rec)) == len(rec)}, pass the Expedition id rule {all(id_ok(i) for i in rec)}'); ok &= len(set(rec)) == len(rec) and all(id_ok(i) for i in rec)
    lk = look(); ok_l = len(lk['lights']) <= 8 and all(0 <= l['depth'] <= 1 and l['range'] > 0 for l in lk['lights']) and lk['look'] == m['look']
    out.append(f'look {lk["id"]} ({lk["look"]}): {len(lk["lights"])} practical lights, {len(lk["blockLooks"])} block looks, every block listed: {all(any(glob(b["id"], e["block"]) for e in lk["blockLooks"]) for b in m["blocks"])}'); ok &= ok_l and all(any(glob(b['id'], e['block']) for e in lk['blockLooks']) for b in m['blocks'])
    return ok, out, (col, reach, hulk, rh, dist)
def glob(s, pat):
    import fnmatch; return fnmatch.fnmatchcase(s, pat)

def town_checks():
    """The hub door record against the live town (Resources/UnityMaps.json `expedition-city`, read only) in a window round the
    reserved doorway: floor and clearance for the pilot and the Hulk, and the live town spawns it would crowd (8 m rule)."""
    out, ok = [], True
    maps = json.load(open(os.path.join(RES, 'UnityMaps.json')))['maps']; town = next(m for m in maps if m['id'] == 'expedition-city')
    lay = json.load(open(os.path.join(RES, 'ExpeditionLayout.json'))); tarea = next(a for a in lay['areas'] if a['id'] == TOWN)
    res = next(r for r in tarea['hub']['reserved'] if r['building'] == AREA)
    out.append(f'live hub record: {AREA} reserved at ({res["x"]:g}, {res["y"]:g}, {res["z"]:g}) yaw {res["yaw"]:g}, {res["path"]:g} m walked from the {res["gate"].split(":")[-1]} gate ({res["run"]:.1f} s running)')
    ok &= (res['x'], res['z'], res['yaw']) == (TOWN_DOOR[0], TOWN_DOOR[1], TOWN_YAW)
    win = dict(minX=-20.0, maxX=20.0, minZ=-96.0, maxZ=-66.0, ceiling=40.0, killY=-8.0)
    tb = [b for b in town['blocks'] if b['id'] not in set(town['navHints'].get('collisionOff', []))]
    col = Collider(tb, town.get('ramps', []), win); col.build(lambda y: abs(y) < 0.05)
    hk = Collider(tb, town.get('ramps', []), win); hk.build(lambda y: abs(y) < 0.05, HULK_RADIUS, HULK_HEIGHT)
    td = town_door(); d = td['door']; ap = d['approach']; reach = col.flood([col.node_at(ap['x'], 0, ap['z'])]); rh = hk.flood([hk.node_at(d['returns'][0]['x'], 0, d['returns'][0]['z'])])
    out.append(f'window x {win["minX"]:g}..{win["maxX"]:g}, z {win["minZ"]:g}..{win["maxZ"]:g}: {len(col.graph)} town floor cells, {len(reach)} joined to the approach (the live hub record measures the route from the south gate)')
    pts = [('anchor', td['anchor']), ('approach', d['approach'])] + [(f'return{i}', p) for i, p in enumerate(d['returns'])]
    bad = []
    for n, p in pts:
        c = col.clearance(p['x'], p['z'], 0.35, HEIGHT + 0.05); f = col.node_at(p['x'], 0, p['z']) in reach and col.floor_under(p['x'], p['z'], 0)
        if not f or c < RADIUS: bad.append(f'{n} floor {f} clear {c:.2f}')
    for i, p in enumerate(d['returns']):
        c = col.clearance(p['x'], p['z'], 0.35, HULK_HEIGHT + 0.05)
        if c < HULK_SLOT or hk.node_at(p['x'], 0, p['z']) not in rh: bad.append(f'return{i} Hulk clear {c:.2f}')
    out.append(f'hub door record (town frame): anchor, approach and 8 returns on Station Road with pilot clearance, returns Hulk-clear and routed: {not bad}' + ''.join('\n   BAD ' + s for s in bad)); ok &= not bad
    ok &= in_vol(d['interact'], d['approach']['x'], 0, d['approach']['z']) and not any(in_vol(d['interact'], p['x'], 0, p['z']) for p in d['returns'])
    near = []
    for e in tarea['spawns']:
        dd = min(math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']) for _, p in pts)
        if dd < 8.0: near.append(f"{e['id'].split(':')[-1]} ({e['exit']['x']:g}, {e['exit']['z']:g}) {dd:.1f} m")
    out.append('live town spawns within 8 m of that door, its approach or its returns (to move when the station is imported): ' + (', '.join(near) or 'none'))
    return ok, out

# =====================================================================================================================
def plan(path, graphs, scale=7):
    col, reach, hulk, rh, dist = graphs; m = game_map()
    SX0, SX1, SZ0, SZ1 = X0 - 1, X1 + 1, Z0 - 1, Z1 + 1; PW, PH = int((SX1 - SX0) * scale), int((SZ1 - SZ0) * scale)
    W, H = 2 * PW + 30, PH + 170
    cv = Canvas(W, H)
    KC = {'vent': (190, 110, 255), 'door': (255, 150, 60), 'drop': (255, 70, 170), 'breach': (255, 235, 60)}
    HZ = {'train': (255, 60, 60), 'third-rail': (90, 200, 255), 'flood': (40, 120, 200), 'spark': (120, 230, 255), 'gas': (180, 200, 120), 'falling-glass': (200, 200, 230)}
    pr = proposals()
    for panel, yf in ((0, 0.0), (1, L1)):
        ox = 10 + panel * (PW + 10); oy = 46
        def X(x): return int(round((x - SX0) * scale)) + ox
        def Y(z): return int(round((SZ1 - z) * scale)) + oy
        cv.rect(X(SX0), Y(SZ1), X(SX1), Y(SZ0), (22, 22, 28))
        for n in col.graph:
            lvl = n[2]
            show = (panel == 0 and lvl < 3) or (panel == 1 and 3 < lvl < 9)
            if not show: continue
            x, z = col.cell_xz(n[0], n[1])
            if abs(lvl - yf) < 0.05: c = (70, 70, 78) if n in reach else (120, 50, 60)
            elif abs(lvl - TB) < 0.05: c = (44, 52, 62) if n in reach else (120, 50, 60)
            else: c = (88, 96, 110)
            cv.rect(X(x - 0.25), Y(z + 0.25), X(x + 0.25), Y(z - 0.25), c)
        if panel == 0:
            for h in pr['hazards']:
                V = h['volume']
                if h['kind'] in ('falling-glass',): continue
                cv.rect(X(V['x'] - V['sx'] / 2), Y(V['z'] + V['sz'] / 2), X(V['x'] + V['sx'] / 2), Y(V['z'] - V['sz'] / 2), HZ[h['kind']], 0.28 if h['kind'] != 'third-rail' else 0.6)
        for r in m['ramps']:
            if (panel == 1 and r['high']['y'] > 3) or panel == 0: cv.poly([(X(x), Y(z)) for x, z in corners(ramp_block(r))], (90, 170, 230), 0.35)
        for b in sorted(m['blocks'], key=lambda b: yspan(b)[1]):
            y0, y1 = yspan(b)
            if b['id'] in visual or 'floor-' in b['id'] or 'track-bed' in b['id'] or 'plat-' in b['id'] or 'roof' in b['id'] or 'ceiling' in b['id']: continue
            if 'shell' in b['id']: continue
            if panel == 0 and not (y0 < 3.0 and y1 > -1.0): continue
            if panel == 1 and not (y0 < L1 + 3 and y1 > L1 + 0.05): continue
            if panel == 1 and b['id'].split(MAP + '-')[1].startswith(('deck', 'landing', 'bridge-w', 'bridge-e', 'signal-floor', 'signal-landing')) and y1 <= L1 + 0.01: continue
            c = (230, 150, 40) if 'exit-' in b['id'] else (180, 70, 60) if 'breach' in b['id'] else (150, 156, 170) if y1 - max(yf, y0) > 2 else (150, 112, 74)
            if 'train' in b['id'] or 'wagon' in b['id']: c = (120, 150, 165)
            cv.poly([(X(x), Y(z)) for x, z in corners(b)], c)
        for e in entries:
            if (e['exit']['y'] > 3) != (panel == 1): continue
            x, z = e['exit']['x'], e['exit']['z']; c = KC[e['kind']]
            cv.line(X(e['from']['x']), Y(e['from']['z']), X(x), Y(z), c, 1); cv.disc(X(x), Y(z), 3, c)
            if e.get('station'): cv.ring(X(x), Y(z), 6, (80, 255, 220), 1)
        if panel == 0:
            d = door_record()
            for key, c in (('interact', (80, 220, 255)), ('closure', (255, 255, 120))): cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', d[key]))], c, 0.6)
            for p in ENTRIES_IN:
                cx, cy = X(p['x']), Y(p['z']); fx, fz = forward(p['yaw']); cv.disc(cx, cy, 3, (255, 255, 255)); cv.line(cx, cy, cx + int(fx * 7), cy - int(fz * 7), (255, 255, 255), 1)
            cv.disc(X(DOOR[0]), Y(DOOR[1] - 0.3), 5, (0, 160, 255))
            for p in BOSS_POINTS:
                cx, cy = X(p['x']), Y(p['z']); cv.line(cx - 6, cy - 6, cx + 6, cy + 6, (255, 90, 255), 3); cv.line(cx - 6, cy + 6, cx + 6, cy - 6, (255, 90, 255), 3)
        for i, c in enumerate(CACHES):
            if (c['y'] > 3) == (panel == 1): cv.rect(X(c['x']) - 5, Y(c['z']) - 5, X(c['x']) + 5, Y(c['z']) + 5, (255, 200, 40)); cv.text(X(c['x']) + 7, Y(c['z']) - 4, f'C{i}', (255, 200, 40))
        for mm in pr['machines']:
            if (mm['y'] > 3) == (panel == 1): cv.rect(X(mm['x']) - 4, Y(mm['z']) - 4, X(mm['x']) + 4, Y(mm['z']) + 4, (60, 255, 140)); cv.text(X(mm['x']) + 6, Y(mm['z']) + 2, mm['kind'].upper(), (60, 255, 140), 1)
        labels = ([('TICKET HALL', -10, 26.5), ('GRAND CONCOURSE', -15, 7.0), ('BOSS ARENA', -4.5, 0.0), ('P1', -2, -7), ('T1 EXPRESS', -9, -12.5), ('P2', -2, -21),
                   ('T2 STALLED TRAIN', 28.5, -29.5), ('P3', -2, -37), ('T3 FLOODED', -10, -44.5), ('P4', -2, -50), ('MAINTENANCE CORRIDOR', -22, -57.2),
                   ('PUMP', -42.5, -55.3), ('THROAT', -47.5, -24), ('THROAT', 38.2, -24), ('EXIT', 2.6, 34.6)] if panel == 0 else
                  [('STATION MASTER', -8.5, 33.5), ('LOST PROPERTY', -36, 29.5), ('PA ROOM', 20, 29.5), ('NORTH GALLERY', -11, 22.4), ('W BALCONY', -39.6, 15.5), ('E BALCONY', 30.4, 15.5),
                   ('W FOOTBRIDGE', -45.5, -28), ('E FOOTBRIDGE', 36.6, -28), ('SIGNAL BOX', 18.0, -51.5), ('CONCOURSE VOID', -12, 8)])
        for t, x, z in labels: cv.text(X(x), Y(z), t, (235, 240, 245), 2 if panel == 0 or len(t) < 14 else 1)
        cv.text(ox, oy - 16, 'GROUND Y 0 AND TRACK BED Y -1.2' if panel == 0 else 'UPPER FLOOR Y 6', (200, 220, 235))
    cv.text(10, 8, 'EXPEDITION CENTRAL STATION DESIGN DRAFT  96 X 96 M  NORTH UP  1 PX = %d CM  NOT LIVE' % (100 // scale), (232, 251, 255))
    lx, ly = 10, H - 110
    for label, c in [('EXIT DOOR', (0, 160, 255)), ('INTERACT', (80, 220, 255)), ('CLOSURE', (255, 255, 120)), ('ENTRY SLOT', (255, 255, 255)), ('BOSS POINT', (255, 90, 255)),
                     ('CACHE', (255, 200, 40)), ('MACHINE (PROPOSED)', (60, 255, 140)), ('DOOR SPAWN', KC['door']), ('VENT', KC['vent']), ('DROP', KC['drop']), ('BREACH', KC['breach']),
                     ('STATION ENEMY ENTRANCE', (80, 255, 220)), ('EXPRESS LANE', HZ['train']), ('THIRD RAIL', HZ['third-rail']), ('FLOOD', HZ['flood']), ('SPARK CABINET', HZ['spark']),
                     ('STEAM', HZ['gas']), ('STAIR / STEPS', (90, 170, 230)), ('TRACK BED', (44, 52, 62)), ('TRAIN', (120, 150, 165))]:
        cv.rect(lx, ly, lx + 12, ly + 10, c); lx = cv.text(lx + 18, ly, label, (210, 214, 220)) + 18
        if lx > W - 260: lx, ly = 10, ly + 22
    cv.save(path)

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--png'); ap.add_argument('--fixture', action='store_true'); a = ap.parse_args()
    if a.fixture and not a.json: a.json = FIXTURE
    ok, report, graphs = checks(); ok_t, rep_t = town_checks()
    print('== expedition-transit (exp-transit), Central Station draft (NOT LIVE)'); print('\n'.join(report)); print('station checks: ' + ('pass' if ok else 'FAIL'))
    print('== the hub door record (exp-city frame, not live)'); print('\n'.join(rep_t)); print('town door checks: ' + ('pass' if ok_t else 'FAIL'))
    if a.json:
        with open(a.json, 'w') as f: json.dump(draft(), f, indent=None, separators=(',', ':'), sort_keys=False)
        print('wrote', a.json)
    if a.png: plan(a.png, graphs); print('wrote', a.png)
    sys.exit(0 if ok and ok_t else 1)
