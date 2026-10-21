# Expedition town blockout: `expedition-city`, a zombie city at dusk (one street level), the hub of exp-layout-3, grown in
# exp-layout-7 (2026-10-20: the east, west and south arms 24 m further out, an outer quarter in each; "THE CITY GROWN" below).
# It emits a map entry in the UnityMaps.json shape (blocks, bounds, spawn tables, pilot exclusion columns) and the town's area
# record for ExpeditionLayout.json (docs/EXPEDITION-LAYOUT.md, docs/EXPEDITION-HUB.md): door anchor, returns, interaction and
# closure volumes, the reprint machine and caches, the sanctuary volume and its eight slots, the final gate, the enemy spawns
# (PveEntry shape plus `allow`), the final boss trigger and points, and (exp-layout-3) the `hub` record: the sanctuary fence, its
# four animated gates, the hub spawn zones by ring, the wilds, the instance routes (with their turning points, `via`) and the
# reserved doorways.
# It prints its checks: floor reachability from the sanctuary with the final gate shut and open, every record on walkable
# floor at its own level with pilot clearance (Hulk clearance where Hulks are allowed), the final plaza reachable only
# through the gate volume, no spawn inside or near the sanctuary; the fence closed except at its gates, every instance door
# reached from the sanctuary only through a gate and at least ROUTE_MIN m of running from it, the spawn zones outside the
# fence and clear of the gates.
# Usage: python3 docs/arenas/scripts/expedition_city.py [--json out.json] [--png plan.png] [--look look.json] [--ascii]
#        [--join-png door.png]   (the exp-layout-4 join circle draft's checks and door plan; the draft is not live)
# Plan: a ring-and-cross town with three districts of wilds. The fenced sanctuary plaza (24 m) sits at the centre; four streets
# leave it through four gates: north up the avenue to the final gate, east to the Meridian Approach and the Meridian
# Biocontainment frontage (Ward Zero) out at x 84, west to the Ironworks Yard and the foundry plot at x -84, south down Station
# Road to the transit station plot at z -84 (both reserved, sealed). A ring road links the four arms; alleys cut the inner blocks.
# Coordinates: Unity frame, x east, z north, y up, metres. The whole walkable town is at y 0.
# Deterministic: no clocks, no randomness, sorted outputs; the same run writes the same bytes.
import argparse, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from expedition_common import (Collider, Canvas, text_width, components, block_hash, id_ok, v, q, qyaw, vol, vol_floor, vol_in_bounds, pt, vol_block, in_vol, vol_dist_xz,
                               yaw_of, yaw_rad, forward, dist_xz, yspan, footprint, corners, r4, RADIUS, HEIGHT, PILOT_CLEAR, HULK_CLEAR, HULK_BAND,
                               HULK_RADIUS, HULK_HEIGHT, HULK_SLOT)

MAP = 'expedition-city'; AREA = 'exp-city'
BOUNDS = dict(minX=-96.0, maxX=96.0, minZ=-96.0, maxZ=60.0)   # 192 x 156 m: the 120 m city plus the three wilds districts
EX0, EX1, EZ0, EZ1 = -98.0, 98.0, -98.0, 62.0                  # the perimeter masses run 2 m past the bounds
B = 60.0                       # the old city's half extent (its north edge is still the town's)
EDGE = 62.0                    # the old city's perimeter line (the city's own masses still end there)
CEIL, KILL = 40.0, -8.0
SANCT = 12.0                   # sanctuary plaza half extent (24 m)
FP = (-24.0, 24.0, 38.0, 57.0)  # the final plaza (x0, x1, z0, z1)
GATE_W, GATE_H = 12.0, 6.0     # the final gate opening
DOOR_W, DOOR_H = 3.2, 3.4      # instance doorways (closure size)

blocks = []
def box(id, x0, x1, z0, z1, y0, y1, material='stone', role='static', ch='#', hp=0, pilot=False):
    """An axis-aligned static block (min/max corners). `pilot`: add an exclusion column over its top."""
    assert x1 > x0 and z1 > z0 and y1 > y0, id
    b = dict(id=MAP + '-' + id, material=material, pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0),
             rotation=q(), role=role, mass=26, segment=None, hp=hp, _ch=ch, _pilot=pilot)
    blocks.append(b); return b
def obox(id, cx, cz, sx, sz, y0, y1, yaw, material='metal', ch='o'):
    """A yaw-rotated static block (centre, full size along its own x and z)."""
    b = dict(id=MAP + '-' + id, material=material, pos=v(cx, (y0 + y1) / 2, cz), size=v(sx, y1 - y0, sz), rotation=qyaw(yaw),
             role='static', mass=26, segment=None, hp=0, _ch=ch, _pilot=False)
    blocks.append(b); return b
def strips(u0, u1, h, openings):
    """A face u0..u1 x 0..h minus opening rects (a, b, lo, hi): solid pieces (a, b, v0, v1), merged along u."""
    cuts = sorted({u0, u1, *[o[0] for o in openings], *[o[1] for o in openings]}); out = []
    for a, b in zip(cuts, cuts[1:]):
        mid = (a + b) / 2; y = 0
        for lo, hi in sorted((o[2], o[3]) for o in openings if o[0] <= mid <= o[1]):
            if lo > y: out.append([a, b, y, lo])
            y = max(y, hi)
        if y < h: out.append([a, b, y, h])
    merged = []
    for p in out:
        if merged and abs(merged[-1][1] - p[0]) < 1e-6 and merged[-1][2:] == p[2:]: merged[-1][1] = p[1]
        else: merged.append(p)
    return merged

# =====================================================================================================================
# GROUND: the street slab (asphalt) with the two plazas' paving set flush into it (all tops at y 0).
holes = [(-SANCT, SANCT, -SANCT, SANCT), (FP[0], FP[1], FP[2], FP[3])]
for i, (a, b, lo, hi) in enumerate(strips(EX0, EX1, EZ1 - EZ0, [(h[0], h[1], h[2] - EZ0, h[3] - EZ0) for h in holes])):
    box(f'slab-{i}', a, b, lo + EZ0, hi + EZ0, -0.5, 0, material='foundation', ch='.')
box('sanctuary-paving', -SANCT, SANCT, -SANCT, SANCT, -0.5, 0, ch=',')
box('square-paving', FP[0], FP[1], FP[2], FP[3], -0.5, 0, ch=',')

# =====================================================================================================================
# BUILDINGS: solid masses (facades, windows, doors, signage and roof clutter are kit). Each carries a pilot exclusion
# column over its roof, so no jump or launch leaves the street level (the cybercity frontage precedent).
# (name, x0, x1, z0, z1, height, material)
BUILDINGS = [
    # inner blocks round the sanctuary (L-shaped corners hold the plaza; alleys cut the rest)
    ('ne-shops', 12, 28, 5, 12, 8, 'brick'), ('ne-tenement', 6, 28, 12, 20, 13, 'brick'), ('ne-tower', 6, 15, 23.5, 30, 18, 'stone'), ('ne-hotel', 18.5, 28, 23.5, 30, 11, 'brick'),
    ('nw-shops', -28, -12, 5, 12, 7, 'brick'), ('nw-tenement', -28, -6, 12, 20, 12, 'brick'), ('nw-tower', -15, -6, 23.5, 30, 16, 'stone'), ('nw-block', -28, -18.5, 23.5, 30, 13, 'brick'),
    ('se-shops', 12, 28, -12, -5, 8, 'stone'), ('se-tenement', 5, 28, -20, -12, 11, 'brick'), ('se-row', 5, 15, -28, -23.5, 9, 'brick'), ('se-loft', 18.5, 28, -28, -23.5, 14, 'brick'),
    ('sw-shops', -28, -12, -12, -5, 9, 'brick'), ('sw-tenement', -28, -5, -20, -12, 12, 'brick'), ('sw-row', -15, -5, -28, -23.5, 10, 'stone'), ('sw-loft', -28, -18.5, -28, -23.5, 13, 'brick'),
    # east: the city's edge (an annex and a depot either side of the old forecourt), then the Meridian Approach (the east
    # wilds) out to the Meridian Biocontainment frontage (Ward Zero) at x 84 between its two annexes
    ('e-annex', 34, 44, 10, 30, 11, 'stone'), ('e-tower', 44, EDGE, 20, 30, 17, 'brick'),
    ('e-depot', 34, 44, -34, -10, 9, 'metal'), ('e-block', 44, EDGE, -34, -20, 15, 'brick'),
    ('e-ware-n', EDGE, EX1, 30, EZ1, 10, 'metal'), ('e-ware-s', EDGE, EX1, -EDGE, -30, 11, 'brick'),
    ('wz-facility', 84, EX1, -20, 20, 13, 'stone'), ('meridian-annex-n', 84, EX1, 20, 30, 9, 'stone'), ('meridian-annex-s', 84, EX1, -30, -20, 9, 'stone'),
    # west: the city's edge, then the Ironworks Yard (the west wilds) out to the foundry plot (exp-foundry, reserved and sealed) at x -84
    ('w-annex', -44, -34, 10, 30, 10, 'brick'), ('w-tower', -EDGE, -44, 20, 30, 15, 'brick'),
    ('w-depot', -44, -34, -34, -10, 10, 'metal'), ('w-block', -EDGE, -44, -34, -20, 13, 'brick'),
    ('w-ware-n', EX0, -EDGE, 30, EZ1, 11, 'brick'), ('w-ware-s', EX0, -EDGE, -EDGE, -30, 12, 'metal'),
    ('foundry-hall', EX0, -84, -20, 20, 14, 'metal'), ('foundry-annex-n', EX0, -84, 20, 30, 10, 'metal'), ('foundry-annex-s', EX0, -84, -30, -20, 10, 'metal'),
    # south: two kiosks at the city's edge, then Station Road (the south wilds) out to the transit station plot (exp-transit,
    # reserved and sealed) at z -84 between its two annexes
    ('kiosk-e', 10, 20, -44, -34, 6, 'stone'), ('kiosk-w', -20, -10, -44, -34, 6, 'stone'),
    ('s-block-e', 20, 44, -EDGE, -34, 12, 'brick'), ('s-corner-e', 44, EDGE, -EDGE, -34, 18, 'brick'),
    ('s-block-w', -44, -20, -EDGE, -34, 11, 'brick'), ('s-corner-w', -EDGE, -44, -EDGE, -34, 16, 'brick'),
    ('transit-hall', -20, 20, EZ0, -84, 8, 'stone'), ('transit-annex-e', 20, 30, EZ0, -84, 7, 'stone'), ('transit-annex-w', -30, -20, EZ0, -84, 7, 'stone'),
    # the outer corners behind the wilds (solid blocks of the dead city)
    ('se-outer', 30, EDGE, EZ0, -EDGE, 15, 'brick'), ('se-yard', EDGE, EX1, EZ0, -EDGE, 12, 'metal'),
    ('sw-outer', -EDGE, -30, EZ0, -EDGE, 14, 'brick'), ('sw-yard', EX0, -EDGE, EZ0, -EDGE, 13, 'brick'),
    # north: the final plaza between two flanks and the city hall; the corners close the forecourt
    ('nw-corner', -EDGE, -36, 30, EDGE, 20, 'brick'), ('ne-corner', 36, EDGE, 30, EDGE, 21, 'brick'),
    ('flank-w', -36, -24, 36, EDGE, 15, 'stone'), ('flank-e', 24, 36, 36, EDGE, 15, 'stone'), ('city-hall', -24, 24, 57, EDGE, 16, 'stone'),
]
HEIGHTS = {}
for name, x0, x1, z0, z1, h, mat in BUILDINGS:
    box('bldg-' + name, x0, x1, z0, z1, 0, h, material=mat, ch='#', pilot=True); HEIGHTS[name] = h
# Skyline pieces on the roofs (out of reach under the exclusion columns; they read at dusk).
box('bldg-wz-tower', 88, EX1, -8, 8, 13, 24, ch='#', pilot=True)                        # the Meridian tower
box('bldg-foundry-stack', -93.5, -90.5, -13.5, -10.5, 14, 32, material='brick', ch='#', pilot=True)
box('bldg-city-hall-clock', -5, 5, 57, EDGE, 16, 27, ch='#', pilot=True)
ROOF = [('ne-tenement-tank', 21, 23.5, 14, 16.5, 13, 15.8, 'metal'), ('nw-tenement-tank', -16, -13.5, 14.5, 17, 12, 14.8, 'metal'),
        ('se-loft-stair', 22, 25, -27, -24.5, 14, 16.6, 'stone'), ('sw-tenement-stair', -20, -17, -18.5, -16, 12, 14.6, 'stone'),
        ('ne-tower-plant', 8, 12, 25, 28.5, 18, 20.2, 'metal'), ('e-tower-tank', 50, 53, 23, 26, 17, 20.0, 'metal'),
        ('w-tower-stair', -55, -52, 22.5, 25.5, 15, 17.6, 'stone'), ('s-block-e-tank', 30, 33, -50, -47, 12, 15.0, 'metal'),
        ('nw-corner-tank', -50, -46, 44, 48, 20, 23.2, 'metal'), ('ne-corner-stair', 44, 47, 40, 43, 21, 23.6, 'stone'),
        ('transit-vent-0', -12, -8, -94, -90, 8, 9.6, 'metal'), ('transit-vent-1', 8, 12, -94, -90, 8, 9.6, 'metal'),
        ('e-ware-n-tank', 74, 78, 44, 48, 10, 13.6, 'metal'), ('w-ware-s-stair', -80, -77, -50, -47, 12, 14.6, 'stone'),
        ('se-outer-tank', 44, 48, -80, -76, 15, 18.0, 'metal'), ('sw-yard-tank', -80, -76, -80, -76, 13, 16.4, 'metal')]
for name, x0, x1, z0, z1, y0, y1, mat in ROOF: box('roof-' + name, x0, x1, z0, z1, y0, y1, material=mat, ch='^')

# =====================================================================================================================
# THE FINAL GATE: a 2 m gatehouse wall across the plaza's south side with one 12 x 6 m opening between two towers; the
# lintel and walls rise to 12 m, the towers to 15 m, all under exclusion columns. The gate volume closes the opening.
GZ0, GZ1 = 36.0, 38.0
box('gate-wall-w', -24, -9.5, GZ0, GZ1, 0, 12, ch='G', pilot=True); box('gate-wall-e', 9.5, 24, GZ0, GZ1, 0, 12, ch='G', pilot=True)
box('gate-tower-w', -9.5, -GATE_W / 2, 34.6, 38.6, 0, 15, ch='G', pilot=True); box('gate-tower-e', GATE_W / 2, 9.5, 34.6, 38.6, 0, 15, ch='G', pilot=True)
box('gate-lintel', -GATE_W / 2, GATE_W / 2, GZ0, GZ1, GATE_H, 12, ch='G', pilot=True)
GATE = vol(0, 0, (GZ0 + GZ1) / 2, GATE_W, GATE_H, 0.6, 0)

# =====================================================================================================================
# INSTANCE FRONTAGES, out at the far end of the wilds. Ward Zero (east): a portico on two pylons over the doorway in the
# facility's west face (x 84).
WZ_DOOR = (84.0, 0.0)          # doorway centre on the facade; the anchor faces out of the doorway (-x, yaw 270)
box('wz-portico-pylon-s', 80.6, 81.4, -4.6, -3.8, 0, 4.4, ch='P'); box('wz-portico-pylon-n', 80.6, 81.4, 3.8, 4.6, 0, 4.4, ch='P')
box('wz-portico-canopy', 80.4, 84, -4.8, 4.8, 4.4, 4.8, ch='^')
# Foundry (west) and transit (south): reserved plots with sealed facades; their doorways join the manifest later at
# these points without moving geometry (the hub record lists them).
RESERVED = {'exp-foundry': dict(x=-84.0, y=0.0, z=0.0, yaw=90.0), 'exp-transit': dict(x=0.0, y=0.0, z=-84.0, yaw=0.0)}
box('foundry-shutter', -84.4, -84, -3.5, 3.5, 0, 5.0, material='metal', ch='S')
box('transit-boards', -4, 4, -84.3, -84, 0, 3.6, material='wood', ch='S')
box('transit-canopy', -6, 6, -84, -81, 4.6, 5.0, ch='^')
box('transit-canopy-pylon-w', -6, -5.4, -81.6, -81, 0, 4.6, ch='P'); box('transit-canopy-pylon-e', 5.4, 6, -81.6, -81, 0, 4.6, ch='P')

# =====================================================================================================================
# THE SANCTUARY: the paved plaza (24 m), walled by the inner blocks and, across the four street mouths, a 3.2 m fence (under
# pilot exclusion columns) with a 4 m gate in each: wooden palisade on the north and west mouths (the civic and residential
# side), scrap-metal panels on the east and south (toward the Meridian and the station yards). A brazier at its centre, the
# reprint machine and a cache on its diagonals. Its volume is the whole plaza; the fence stands just outside it, so the
# sanctuary ends at the fence's inner face and a gate's threshold is outside it. The gate openings carry no collider in the
# map: a gate's collision is the director's (the `hub` record's closure volume), like the doors' and the final gate's.
FENCE_T, FENCE_H = 0.4, 3.2    # fence thickness and height (a Hulk is 3.06 m; the pilot columns start at 3.1 m)
HUB_GATE_W = 4.0               # the clear width of every hub gate opening (two pilots abreast; Hulk-clear)
GATE_LEAF_H = 3.0              # the drawn leaves' height (the closure volume is the fence's full 3.2 m)
# (side, the fence plane's axis ('z': a fence along x at z = +-12), outward sign, the mouth's span along the fence, kind,
#  outward yaw: the way a pilot walks to leave)
MOUTHS = [('north', 'z', 1, -6.0, 6.0, 'wood', 0.0), ('east', 'x', 1, -5.0, 5.0, 'metal', 90.0),
          ('south', 'z', -1, -5.0, 5.0, 'metal', 180.0), ('west', 'x', -1, -5.0, 5.0, 'wood', 270.0)]
FENCES, HUB_GATES = [], []
for side, ax, sg, a, b, kind, yaw in MOUTHS:
    p0, p1 = sorted((sg * SANCT, sg * (SANCT + FENCE_T)))
    for k, (u0, u1) in enumerate([(a, -HUB_GATE_W / 2), (HUB_GATE_W / 2, b)]):
        bid = f'sanctuary-fence-{kind}-{side}-{k}'
        blk = box(bid, u0, u1, p0, p1, 0, FENCE_H, material='wood' if kind == 'wood' else 'metal', ch='F', pilot=True) if ax == 'z' else \
              box(bid, p0, p1, u0, u1, 0, FENCE_H, material='wood' if kind == 'wood' else 'metal', ch='F', pilot=True)
        FENCES.append(dict(block=blk['id'], kind=kind, side=side, gate=f'{AREA}:hub-gate:{side}'))
    c = sg * (SANCT + FENCE_T / 2); cx, cz = (0.0, c) if ax == 'z' else (c, 0.0); fx, fz = forward(yaw)
    HUB_GATES.append({'id': f'{AREA}:hub-gate:{side}', 'kind': kind, 'motion': 'swing' if kind == 'wood' else 'slide', 'side': side,
                      'x': r4(cx), 'y': 0.0, 'z': r4(cz), 'yaw': yaw, 'width': HUB_GATE_W, 'height': GATE_LEAF_H,
                      # A slide gate's leaves run on the fence's inner side (d -0.38..-0.22), so its closure reaches 0.2 m inward.
                      'closure': vol(cx, 0, cz, HUB_GATE_W, FENCE_H, FENCE_T, yaw) if kind == 'wood' else vol(cx - fx * 0.09, 0, cz - fz * 0.09, HUB_GATE_W, FENCE_H, 0.62, yaw),
                      'interact': vol(cx, 0, cz, HUB_GATE_W + 0.4, 3.0, 4.4, yaw),
                      'sensor': vol(cx, 0, cz, HUB_GATE_W + 2.0, 3.4, 8.0, yaw),
                      'inside': pt(cx - fx * 1.6, 0, cz - fz * 1.6), 'outside': pt(cx + fx * 1.6, 0, cz + fz * 1.6),
                      'fence': [f['block'] for f in FENCES if f['side'] == side]})
box('sanctuary-brazier', -0.8, 0.8, -0.8, 0.8, 0, 0.9, material='metal', ch='*')
SANCTUARY = vol(0, 0, 0, 2 * SANCT, 8, 2 * SANCT, 0)
SLOT_R = 5.5
SLOTS = []
for k in range(8):
    a = math.radians(22.5 + 45 * k); x, z = SLOT_R * math.sin(a), SLOT_R * math.cos(a)
    SLOTS.append(pt(x, 0, z, yaw_of(-x, -z)))                                              # facing the brazier
MACHINE = pt(5.3, 0, 5.3, 225.0)
CACHES = [pt(-5.3, 0, -5.3, 45.0), pt(31.5, 0, -31.5, 315.0), pt(-34.0, 0, 33.4, 90.0)]   # sanctuary, SE ring corner, NW forecourt nook

# =====================================================================================================================
# THE FINAL PLAZA: the city hall's steps (a 0.9 m dais) and four planters as cover round the boss's ground.
box('square-dais', -7, 7, 53.6, FP[3], 0, 0.9, ch='d')
for i, (cx, cz) in enumerate([(-16, 43), (16, 43), (-16, 51.5), (16, 51.5)]):
    box(f'square-planter-{i}', cx - 1.2, cx + 1.2, cz - 1.2, cz + 1.2, 0, 1.0, ch='o')
BOSS_TRIGGER = vol(0, 0, 47.5, 44, 8, 17, 0)
BOSS_POINTS = [pt(0, 0, 47.0, 180.0), pt(-11.0, 0, 46.5, 180.0), pt(11.0, 0, 46.5, 180.0)]

# =====================================================================================================================
# STREET COVER: wrecks, barriers, dumpsters and crates (static; nothing enters the prop frame).
def car(id, cx, cz, yaw, length=4.4, width=1.9, h=1.5, mat='metal'): obox(id, cx, cz, width, length, 0, h, yaw, material=mat, ch='c')
def barrier(id, x0, x1, z0, z1): box(id, x0, x1, z0, z1, 0, 1.0, ch='=')
car('car-av-0', -3.6, 17.4, 8); car('car-av-1', 3.2, 18.4, -10)                                          # avenue (south half)
barrier('barrier-av-0', 1.5, 5.6, 29.4, 30.0)                                                            # avenue, police line at the forecourt
car('car-fc-0', -15.5, 31.3, 74); car('car-fc-1', 17.0, 33.0, -66); barrier('barrier-fc-0', -27.0, -23.0, 34.6, 35.2)   # forecourt police cars
obox('bus-es', 19.0, -3.4, 2.6, 9.0, 0, 3.0, 88)                                                          # E street: the crashed bus
car('car-es-0', 19.0, 3.4, 96); car('ambulance-wz', WZ_DOOR[0] - 6.5, -7.6, 70, length=5.6, width=2.3, h=2.5)
obox('truck-ws', -17.6, 3.6, 2.5, 7.5, 0, 3.0, 86); car('car-ws-0', -17.0, -3.6, 94)                      # W street
for i, (cx, cz) in enumerate([(-38.6, 8.0), (-40.4, 8.0), (-39.5, 6.2)]): box(f'crate-fd-{i}', cx - 0.75, cx + 0.75, cz - 0.75, cz + 0.75, 0, 1.4, material='wood', ch='x')
car('car-ss-0', -2.6, -19.0, 4); barrier('barrier-ss-0', 6.0, 9.5, -38.0, -37.4)   # S street, station square
car('taxi-tr', -6.8, -39.5, 28)
for i, (x0, z0, along) in enumerate([(28.0, 21.0, 'z'), (-29.3, -22.5, 'z'), (16.5, 20.0, 'x'), (-12.0, 20.0, 'x'), (25.5, -21.3, 'x'), (-17.5, -21.3, 'x'), (28.0, 8.0, 'z'), (-29.3, -10.0, 'z')]):
    if along == 'z': box(f'dumpster-{i}', x0, x0 + 1.3, z0, z0 + 2.0, 0, 1.3, material='metal', ch='u')
    else: box(f'dumpster-{i}', x0, x0 + 2.0, z0, z0 + 1.3, 0, 1.3, material='metal', ch='u')
car('car-re-0', 32.6, -15.5, 4); car('car-rw-0', -32.6, 15.5, -6); car('car-rs-0', 14.5, -31.2, 86); car('car-rs-1', -17.0, -32.2, 92)

# =====================================================================================================================
# THE WILDS: three districts between the city's edge (|x| 44, z -44) and the instance plots, where the hub's zombies roam
# (docs/EXPEDITION-HUB.md). Darker and foggier than the city (the dressing's and the atmosphere's job); their blocks are low
# cover in the open: a checkpoint chicane at each district's mouth, then wrecks, containers, rubble heaps, ruined wall stubs,
# dead trees, a power pylon and floodlight bases. Each district is a volume of the hub record (`wilds`).
#   east  Meridian Approach: x 44..62 (z -20..20) and 62..84 (z -30..30), out to the Meridian frontage;
#   west  Ironworks Yard:    the mirror, out to the sealed foundry plot (rail yard: boxcars, a slag heap, a tank);
#   south Station Road:      x -20..20 (z -62..-44) and -30..30 (z -84..-62), out to the sealed station (bus terminal).
WILDS = [('meridian-approach', 'east', 44.0, 84.0, -30.0, 30.0), ('ironworks-yard', 'west', -84.0, -44.0, -30.0, 30.0), ('station-road', 'south', -30.0, 30.0, -84.0, -44.0)]
def wild(id, x0, x1, z0, z1, h, material='stone', y0=0.0): return box('wild-' + id, x0, x1, z0, z1, y0, y0 + h, material=material, ch='w')
def container(id, cx, cz, along, y0=0.0):
    hx, hz = (3.05, 1.2) if along == 'x' else (1.2, 3.05); return wild('container-' + id, cx - hx, cx + hx, cz - hz, cz + hz, 2.6, 'metal', y0)
def boxcar(id, cx, cz, along):
    hx, hz = (6.0, 1.5) if along == 'x' else (1.5, 6.0); return wild('boxcar-' + id, cx - hx, cx + hx, cz - hz, cz + hz, 4.2, 'metal')
def tree(id, x, z): return wild('tree-' + id, x - 0.3, x + 0.3, z - 0.3, z + 0.3, 3.2, 'wood')
# East: Meridian Approach.
barrier('barrier-e-0', 47.0, 47.6, -14.0, -4.5); barrier('barrier-e-1', 47.0, 47.6, 4.5, 14.0)
wild('booth-e-0', 49.6, 52.0, -10.6, -8.2, 2.7); wild('sandbag-e-0', 53.0, 56.0, 13.0, 14.2, 1.1, 'wood'); wild('sandbag-e-1', 53.0, 56.0, -15.2, -14.0, 1.1, 'wood')
obox('truck-e-0', 58.2, 7.4, 2.5, 7.5, 0, 3.0, 72); car('car-e-apc', 56.0, -6.0, 106, length=6.0, width=2.6, h=2.4)
tree('e-0', 45.8, 17.6); tree('e-1', 60.4, -17.4)
container('e-0', 70.2, 16.2, 'x'); container('e-1', 70.6, 16.2, 'x', 2.6); container('e-2', 69.0, -19.8, 'x'); container('e-3', 79.4, -19.0, 'x')
wild('rubble-e-0', 70.0, 75.0, -3.0, 2.6, 1.2); wild('wall-e-0', 62.4, 62.8, -12.0, -5.0, 2.6, 'brick'); wild('wall-e-1', 75.0, 80.0, 10.0, 10.4, 2.2, 'brick')
car('car-e-1', 66.0, 8.4, 32); car('car-e-2', 77.4, 17.4, -24)
wild('pylon-e-0', 73.4, 75.6, 23.4, 25.6, 1.0); wild('flood-e-0', 78.9, 80.1, 12.4, 13.6, 1.4, 'metal'); wild('flood-e-1', 78.9, 80.1, -13.6, -12.4, 1.4, 'metal')
tree('e-2', 63.8, 15.0); tree('e-3', 81.2, -14.8)
# West: Ironworks Yard (rails along x at z 7 and -7; their paint is the dressing's).
barrier('barrier-w-0', -47.6, -47.0, -14.0, -4.5); barrier('barrier-w-1', -47.6, -47.0, 4.5, 14.0)
boxcar('w-0', -57.0, 11.0, 'x'); boxcar('w-1', -77.0, 13.0, 'x'); boxcar('w-2', -69.0, -11.5, 'x')
wild('rubble-w-0', -75.0, -70.0, -3.0, 3.0, 1.4); wild('rubble-w-1', -55.0, -51.0, -16.0, -12.6, 1.0)
wild('tank-w-0', -72.0, -68.0, 22.0, 26.0, 5.5, 'metal'); wild('pylon-w-0', -79.6, -77.4, -25.6, -23.4, 1.0)
for i, (cx, cz) in enumerate([(-61.5, 4.5), (-60.0, 5.6), (-80.5, -5.0)]): box(f'crate-w-{i}', cx - 0.75, cx + 0.75, cz - 0.75, cz + 0.75, 0, 1.4, material='wood', ch='x')
container('w-0', -66.0, -22.6, 'x'); wild('wall-w-0', -64.0, -63.6, 4.0, 12.0, 2.4, 'brick'); car('car-w-0', -52.6, -6.0, 64)
tree('w-0', -45.8, -17.6); tree('w-1', -60.4, 17.4); tree('w-2', -81.2, 14.8)
wild('flood-w-0', -80.1, -78.9, 12.4, 13.6, 1.4, 'metal')
# South: Station Road (a bus terminal).
barrier('barrier-s-0', -14.0, -4.5, -47.6, -47.0); barrier('barrier-s-1', 4.5, 14.0, -47.6, -47.0)
obox('bus-s-0', -9.6, -56.0, 2.6, 11.0, 0, 3.0, 14); obox('bus-s-1', 13.4, -71.0, 2.6, 11.0, 0, 3.0, 96)
wild('booth-s-0', 4.0, 6.4, -63.2, -60.8, 2.7); wild('rubble-s-0', -5.0, 1.0, -70.0, -66.6, 1.1)
wild('shelter-s-0', -25.0, -21.4, -67.0, -66.4, 2.6, 'metal'); wild('shelter-s-1', 21.4, 25.0, -78.6, -78.0, 2.6, 'metal')
car('taxi-s-0', 8.8, -51.6, 140); car('car-s-1', -17.6, -76.0, 58); car('car-s-2', -16.4, -48.6, 12)
tree('s-0', -2.4, -62.6); tree('s-1', 6.6, -79.6); tree('s-2', -26.8, -80.2)
wild('wall-s-0', -14.0, -8.0, -66.4, -66.0, 2.2, 'brick'); wild('flood-s-0', -12.6, -11.4, -80.1, -78.9, 1.4, 'metal')

# =====================================================================================================================
# HULK BREACH PANELS: bricked-up shopfronts 3.2 m wide and 4 m tall, 0.4 m proud of a building face, so a broken panel
# opens onto the solid mass behind it (Ward Zero's rule; collision off at breakAt, back at restoreAt).
BREACH_W, BREACH_H, BREACH_T = 3.2, 4.0, 0.4
breaches = []
def breach_panel(name, face, axis, u, sign):
    """A panel on the face plane `face` (x for axis 'x', z for 'z'), centred at u along the face, standing out toward sign."""
    f0, f1 = sorted((face, face + sign * BREACH_T)); a, b = u - BREACH_W / 2, u + BREACH_W / 2
    blk = box('breach-' + name, f0, f1, a, b, 0, BREACH_H, ch='B') if axis == 'x' else box('breach-' + name, a, b, f0, f1, 0, BREACH_H, ch='B')
    breaches.append((name, face, axis, u, sign, blk))
breach_panel('ring-e', 34.0, 'x', -21.0, -1); breach_panel('ring-w', -34.0, 'x', 22.0, 1)
breach_panel('ring-s', -34.0, 'z', -28.0, 1); breach_panel('forecourt-e', 36.0, 'z', 30.0, -1)
breach_panel('mer-ware', -30.0, 'z', 76.5, 1); breach_panel('iron-ware', 30.0, 'z', -76.5, -1); breach_panel('station-outer', 30.0, 'x', -70.0, -1)

# =====================================================================================================================
# ENEMY SPAWNS (the existing PveEntry shape plus `allow`): shopfront and side doors, street grates and manholes, drops off
# roof edges, Hulk breaches. Each exit is on the street (y 0, level 0); `clear` is the free radius checked at the exit.
# yaw is PveEntry's: radians, atan2(dx, dz) of the facing (the existing horde code multiplies it by Rad2Deg).
SMALL = ['shambler', 'runner', 'spitter']; BIG = SMALL + ['brute']
entries = []
def entry(kind, name, frm, ext, facing, allow, surface='', height=0.0, clear=1.2, hulk=None, extra=None):
    d = dict(e=0, id=f'{AREA}:spawn:{kind}-{name}', kind=kind, surface=surface, **{'from': v(*frm), 'exit': v(*ext)}, yaw=yaw_rad(*facing),
             level=0, song=-1, pit=False, height=r4(height), clear=r4(clear))
    if hulk is not None: d['hulk'] = hulk
    if extra: d.update(extra)
    d['allow'] = list(allow); entries.append(d)
def door(name, face, axis, u, sign, allow=SMALL, out=2.4):
    """A door in the face plane (x = face for axis 'x', z = face for 'z'), at u along it, opening toward sign."""
    if axis == 'x': frm, ext, fac = (face - sign * 0.6, 0, u), (face + sign * out, 0, u), (sign, 0)
    else: frm, ext, fac = (u, 0, face - sign * 0.6), (u, 0, face + sign * out), (0, sign)
    entry('door', name, frm, ext, fac, allow)
def vent(name, x, z, dx, dz):
    n = math.hypot(dx, dz); entry('vent', name, (x, 0, z), (x + dx / n * 1.2, 0, z + dz / n * 1.2), (dx, dz), SMALL, surface='floor')
def drop(name, x, z, height, dx, dz, hulk=False):
    entry('drop', name, (x, 0, z), (x, 0, z), (dx, dz), ['hulk'] if hulk else ['brute'], height=height, clear=4.5 if hulk else 2.8, hulk=hulk)
# Doors: the four arms (beyond the sanctuary's 8 m buffer), the ring, the alleys, the forecourt and the final plaza.
door('avenue-e', 6.0, 'x', 28.0, -1); door('avenue-w', -6.0, 'x', 25.0, 1)
door('east-n', 5.0, 'z', 23.0, -1); door('east-s', -5.0, 'z', 25.0, 1, allow=BIG)
door('west-n', 5.0, 'z', -23.0, -1); door('west-s', -5.0, 'z', -26.0, 1)
door('south-e', 5.0, 'x', -27.0, -1); door('south-w', -5.0, 'x', -25.0, 1)
door('ring-e-0', 34.0, 'x', 20.0, -1, allow=BIG); door('ring-e-1', 28.0, 'x', -26.0, 1); door('ring-w-0', -34.0, 'x', -20.0, 1, allow=BIG); door('ring-w-1', -28.0, 'x', 27.0, -1)
door('ring-s-0', -34.0, 'z', 26.0, 1, allow=BIG); door('ring-s-1', -28.0, 'z', -23.0, -1)
door('alley-ne', 15.0, 'x', 27.0, 1, out=1.75); door('alley-nw', 20.0, 'z', -16.0, 1, out=1.75); door('alley-se', -20.0, 'z', 23.0, -1, out=1.75); door('alley-sw', -15.0, 'x', -26.0, -1, out=1.75)
door('forecourt-e', 30.0, 'z', 10.5, 1); door('forecourt-w', 30.0, 'z', -23.0, 1)
door('square-hall-w', FP[3], 'z', -10.0, -1, allow=BIG); door('square-hall-e', FP[3], 'z', 10.0, -1, allow=BIG)
door('square-flank-w', -24.0, 'x', 45.0, 1); door('square-flank-e', 24.0, 'x', 50.0, -1)
# Vents: storm grates and manholes on the avenue, the ring and the plazas.
vent('avenue', 0.0, 21.2, 0, 1)
vent('ring-ne', 31.0, 26.5, 0, -1); vent('ring-se', 31.0, -11.0, 0, 1); vent('ring-sw', -31.0, -12.0, 0, -1); vent('ring-nw', -30.5, 9.0, 0, 1)
vent('ring-s-e', 8.5, -30.5, 1, 0); vent('ring-s-w', -8.0, -31.0, 1, 0); vent('forecourt', -4.0, 32.8, 1, 0)
vent('square-w', -6.0, 41.5, 1, 0); vent('square-e', 6.0, 41.5, -1, 0)
# Drops off roof edges (brutes: 3.3 m out from the facade, a 2.8 m clear landing; Hulks: 4.5 m clear in the open).
drop('avenue-w', -2.7, 28.6, HEIGHTS['nw-tower'], 1, 0); drop('avenue-e', 2.7, 24.5, HEIGHTS['ne-tower'], -1, 0)
drop('east', 26.5, 1.7, HEIGHTS['ne-shops'], 0, -1); drop('west', -22.5, -1.7, HEIGHTS['sw-shops'], 0, 1); drop('south', 1.7, -24.0, HEIGHTS['se-row'], -1, 0)
drop('fd-forecourt', -39.0, -6.4, HEIGHTS['w-depot'], 0, 1)
drop('square-w', -20.7, 52.0, HEIGHTS['flank-w'], 1, 0); drop('square-e', 20.7, 42.0, HEIGHTS['flank-e'], -1, 0)
drop('hulk-east', 31.0, 0.0, HEIGHTS['ne-shops'], -1, 0, hulk=True); drop('hulk-west', -31.0, 0.0, HEIGHTS['nw-shops'], 1, 0, hulk=True)
drop('hulk-station', 0.0, -76.0, HEIGHTS['transit-hall'], 0, 1, hulk=True); drop('hulk-square', 0.0, 42.5, HEIGHTS['city-hall'], 0, -1, hulk=True)
# The wilds (the far ring): doors in the warehouses, annexes and blocks that line them, manholes, drops off their roofs.
door('mer-tower', 62.0, 'x', 25.5, 1, allow=BIG); door('mer-block', 62.0, 'x', -25.0, 1); door('mer-ware-n-0', 30.0, 'z', 71.0, -1, allow=BIG); door('mer-ware-n-1', 30.0, 'z', 77.0, -1)
door('mer-ware-s', -30.0, 'z', 71.5, 1); door('mer-annex-n', 84.0, 'x', 25.0, -1, allow=BIG); door('mer-annex-s', 84.0, 'x', -25.0, -1)
door('iron-tower', -62.0, 'x', 25.5, -1); door('iron-block', -62.0, 'x', -25.0, -1, allow=BIG); door('iron-ware-n', 30.0, 'z', -71.0, -1)
door('iron-ware-s-0', -30.0, 'z', -73.0, 1, allow=BIG); door('iron-ware-s-1', -30.0, 'z', -79.0, 1); door('iron-annex-n', -84.0, 'x', 25.0, 1); door('iron-annex-s', -84.0, 'x', -25.0, 1, allow=BIG)
door('station-block-e', -62.0, 'z', 25.0, -1); door('station-block-w', -62.0, 'z', -25.0, -1, allow=BIG); door('station-outer-w', -30.0, 'x', -71.0, 1)
door('station-outer-e', 30.0, 'x', -76.0, -1, allow=BIG); door('station-annex-e', -84.0, 'z', 25.0, 1); door('station-annex-w', -84.0, 'z', -25.0, 1)
vent('mer-0', 57.0, -12.5, 0, 1); vent('mer-1', 71.5, 9.0, 1, 0); vent('iron-0', -57.0, -3.0, 0, -1); vent('iron-1', -72.0, 20.5, 1, 0)
vent('station-0', -13.0, -52.0, 1, 0); vent('station-1', 13.0, -79.0, -1, 0)
drop('mer-tower', 65.3, 21.0, HEIGHTS['e-tower'], 1, 0); drop('iron-tower', -65.3, 21.0, HEIGHTS['w-tower'], -1, 0); drop('station-block', 16.7, -53.0, HEIGHTS['s-block-e'], -1, 0)
drop('hulk-meridian', 68.5, -8.5, HEIGHTS['e-block'], 0, 1, hulk=True); drop('hulk-ironworks', -62.0, -4.0, HEIGHTS['w-block'], 0, 1, hulk=True)
# Breaches: from 1 m behind the panel to 2.4 m in front of it.
for name, face, axis, u, sign, blk in breaches:
    c, sz = blk['pos'], blk['size']; n = (sign, 0) if axis == 'x' else (0, sign)
    frm = (face - sign * 1.0, 0, u) if axis == 'x' else (u, 0, face - sign * 1.0)
    ext = (face + sign * (BREACH_T + 2.4), 0, u) if axis == 'x' else (u, 0, face + sign * (BREACH_T + 2.4))
    entry('breach', name, frm, ext, n, ['hulk'], surface='wall', clear=HULK_CLEAR,
          extra=dict(block=blk['id'], center=v(c['x'], c['y'], c['z']), size=v(sz['x'], sz['y'], sz['z']), normal=v(n[0], 0, n[1])))
entries.sort(key=lambda e: e['id'])
for i, e in enumerate(entries): e['e'] = i

# =====================================================================================================================
# THE CITY GROWN (exp-layout-7; docs/EXPEDITION-HUB.md "The city grown"). Owner, 2026-10-20: "make the expedition city still a
# little bigger (I want players to walk further still.)". Everything authored above that lies beyond the city's edge (the
# line |x| = CUT east and west, z = -CUT south) moves GROW m outward: the three wilds districts, the instance frontages and
# their doors, the corner masses; a block that straddles the edge is stretched across the gap. A face exactly on the edge
# belongs to the block on its side of it: a block's inner end stays, its outer end moves. The three bands this opens (x 44..68
# east and west, z -68..-44 south) are the new outer quarters, built below in their final coordinates. The sanctuary, the
# inner city, the ring road, the final plaza and the north edge do not move.
CUT, GROW = 44.0, 24.0
def grow_x(x, edge=0):
    """x after the inflation: `edge` -1 a block's min end, +1 its max end, 0 a point (a point on the edge stays)."""
    if x > CUT + 1e-6 or (edge < 0 and x > CUT - 1e-6): return x + GROW
    if x < -CUT - 1e-6 or (edge > 0 and x < -CUT + 1e-6): return x - GROW
    return x
def grow_z(z, edge=0):
    """z after the inflation (the south edge only; the north edge of the town stays)."""
    if z < -CUT - 1e-6 or (edge > 0 and z < -CUT + 1e-6): return z - GROW
    return z
def grow_pt(p):
    """A record point (x, y, z[, yaw]) after the inflation; none lies on the edge itself (checked)."""
    assert abs(abs(p['x']) - CUT) > 1e-6 and abs(p['z'] + CUT) > 1e-6, ('a record on the city edge', p)
    out = dict(p); out['x'] = r4(grow_x(p['x'])); out['z'] = r4(grow_z(p['z'])); return out
def grow_block(b):
    if b['rotation'] != q():
        # Turned blocks (wrecks, the buses, a truck) move whole with their centre; none straddles the edge.
        cs = corners(b); xs = [round(grow_x(x) - x, 6) for x, z in cs]; zs = [round(grow_z(z) - z, 6) for x, z in cs]
        assert len(set(xs)) == 1 and len(set(zs)) == 1, ('a turned block straddles the city edge', b['id'])
        b['pos'] = v(b['pos']['x'] + xs[0], b['pos']['y'], b['pos']['z'] + zs[0]); return
    x0, x1, z0, z1 = footprint(b); nx0, nx1, nz0, nz1 = grow_x(x0, -1), grow_x(x1, 1), grow_z(z0, -1), grow_z(z1, 1)
    b['pos'] = v((nx0 + nx1) / 2, b['pos']['y'], (nz0 + nz1) / 2); b['size'] = v(nx1 - nx0, b['size']['y'], nz1 - nz0)
for b in blocks: grow_block(b)
for e in entries:
    e['from'] = v(**{k: val for k, val in grow_pt(e['from']).items() if k in 'xyz'}); e['exit'] = v(**{k: val for k, val in grow_pt(e['exit']).items() if k in 'xyz'})
    if e['kind'] == 'breach':
        blk = next(b for b in blocks if b['id'] == e['block']); e['center'] = v(blk['pos']['x'], blk['pos']['y'], blk['pos']['z']); e['size'] = v(blk['size']['x'], blk['size']['y'], blk['size']['z'])
WZ_DOOR = (grow_x(WZ_DOOR[0]), WZ_DOOR[1])
RESERVED = {k: grow_pt(p) for k, p in RESERVED.items()}
WILDS = [(n, s, grow_x(x0, -1), grow_x(x1, 1), grow_z(z0, -1), grow_z(z1, 1)) for n, s, x0, x1, z0, z1 in WILDS]
OLD_BOUNDS = dict(BOUNDS)
BOUNDS = dict(minX=BOUNDS['minX'] - GROW, maxX=BOUNDS['maxX'] + GROW, minZ=BOUNDS['minZ'] - GROW, maxZ=BOUNDS['maxZ'])   # 240 x 180 m
EX0, EX1, EZ0, EZ1 = EX0 - GROW, EX1 + GROW, EZ0 - GROW, EZ1                                                       # the perimeter masses, 2 m past

# THE OUTER QUARTERS: one in each band, the city's last streets before the checkpoint and the wilds (lit like the city; the
# wilds' darkness starts at the old line, now CUT + GROW). Authored once in an arm's own frame and laid on all three: `a` m
# outward from the city's edge (0..GROW), `c` m across (+ to the left looking out: north on the east and west arms, east on
# the south arm). The forecourt or the station square meets the band at |c| < 10 and the wilds leave it at |c| < 20, so:
#   - a cross street (a 0..5) where the forecourt's axis ends against a landmark block's face (c -16..13, a 5..17.5): the
#     party has to turn, left up the main lane (c 13..20, 7 m) or right down the back alley (c -20..-16, 4 m);
#   - the lanes come out on a square (a 17.5..GROW) in front of the old checkpoint, where the instance's beacon shows down the
#     wilds again;
#   - the band beyond |c| 20 is closed by building rows (east and west; the south band's sides are the station blocks).
# Cover in the cross street, the lane and the square (wrecks, barriers, crates, a dumpster); spawns: a door in the landmark's
# front and in each row on its lane, a grate in the main lane, a brute drop off the landmark's roof into the cross street
# (zone `<arm>-quarter`, ring mid: every one nearer the gates than the wilds' nearest).
ARMS = {'east': (lambda a, c: (CUT + a, c)), 'west': (lambda a, c: (-CUT - a, c)), 'south': (lambda a, c: (c, -CUT - a))}
ARM_OUT = {'east': (1, 0), 'west': (-1, 0), 'south': (0, -1)}   # the outward direction of each arm
# (arm, prefix, landmark name and material, height, the rows' names and heights, the cars' yaws)
QUARTERS = [('east', 'xq', 'exchange', 'stone', 17), ('west', 'fq', 'union-hall', 'brick', 15), ('south', 'tq', 'tram-depot', 'stone', 12)]
LANE_IN, LANE_OUT, ALLEY_IN = 13.0, 20.0, -16.0        # the landmark's sides (c) and the lanes' outer side
LM_A0, LM_A1 = 5.0, GROW - 6.5                        # the landmark's front and back (a): a 5 m cross street before it, a 6.5 m square after
def qbox(arm, id, a0, a1, c0, c1, y0, y1, material='stone', ch='#', pilot=False):
    """A block given in the arm's frame (a0..a1 out, c0..c1 across)."""
    to = ARMS[arm]; (x0, z0), (x1, z1) = to(a0, c0), to(a1, c1)
    return box(id, min(x0, x1), max(x0, x1), min(z0, z1), max(z0, z1), y0, y1, material=material, ch=ch, pilot=pilot)
def qcar(arm, id, a, c, yaw, **kw):
    """A wreck at (a, c), its yaw measured from the arm's outward direction."""
    x, z = ARMS[arm](a, c); ox, oz = ARM_OUT[arm]; base = math.degrees(math.atan2(ox, oz)); car(id, x, z, base + yaw, **kw)
def qface(arm, a, c, da, dc):
    """World point and direction of an arm-frame point and direction."""
    x, z = ARMS[arm](a, c); x1, z1 = ARMS[arm](a + da, c + dc); return x, z, x1 - x, z1 - z
QUARTER_ZONES = []
OLD_ENTRIES = len(entries)
for arm, pre, landmark, mat, lh in QUARTERS:
    G = GROW
    # The landmark: its long front faces the forecourt across the cross street.
    qbox(arm, f'bldg-{pre}-{landmark}', LM_A0, LM_A1, ALLEY_IN, LANE_IN, 0, lh, material=mat, pilot=True); HEIGHTS[f'{pre}-{landmark}'] = lh
    if arm != 'south':
        # The rows closing the band beyond the lanes (the south band's sides are the stretched station blocks).
        qbox(arm, f'bldg-{pre}-row-l0', 0, 9.5, LANE_OUT, 30, 0, 12, material='brick', pilot=True); HEIGHTS[f'{pre}-row-l0'] = 12
        qbox(arm, f'bldg-{pre}-row-l1', 9.5, G, LANE_OUT, 30, 0, 14, material='brick', pilot=True); HEIGHTS[f'{pre}-row-l1'] = 14
        qbox(arm, f'bldg-{pre}-row-r0', 0, 10.5, -34, -LANE_OUT, 0, 11, material='brick', pilot=True); HEIGHTS[f'{pre}-row-r0'] = 11
        qbox(arm, f'bldg-{pre}-row-r1', 10.5, G, -34, -LANE_OUT, 0, 13, material='stone', pilot=True); HEIGHTS[f'{pre}-row-r1'] = 13
        qbox(arm, f'bldg-{pre}-back', 0, G, -62 - G, -34, 0, 14, material='brick', pilot=True); HEIGHTS[f'{pre}-back'] = 14
    # Cover: a wreck and a barrier line in the cross street, wrecks, crates and a dumpster on the square.
    qcar(arm, f'car-{pre}-0', 11.0, 18.9, 4); qcar(arm, f'car-{pre}-1', LM_A1 + 3.4, -12.6, -24); qcar(arm, f'car-{pre}-2', LM_A1 + 3.0, 10.4, 40)
    qbox(arm, f'barrier-{pre}-0', 1.6, 2.2, -14.5, -10.5, 0, 1.0, ch='=')
    for k, (a, c) in enumerate([(LM_A1 + 1.9, -1.6), (LM_A1 + 3.4, -2.2), (LM_A1 + 2.4, -3.2)]): qbox(arm, f'crate-{pre}-{k}', a - 0.75, a + 0.75, c - 0.75, c + 0.75, 0, 1.4, material='wood', ch='x')
    qbox(arm, f'dumpster-{pre}', 2.5, 4.5, LANE_OUT - 1.3, LANE_OUT, 0, 1.3, material='metal', ch='u')
    # Spawns: the landmark's front door on the cross street, a door in each row on its lane (the south arm's in the station
    # blocks' faces), a grate in the main lane, a brute drop off the landmark's roof into the cross street.
    def qdoor(name, a, c, da, dc, allow=SMALL, out=2.4):
        x, z, dx, dz = qface(arm, a, c, da, dc); entry('door', f'{pre}-{name}', (x - dx * 0.6, 0, z - dz * 0.6), (x + dx * out, 0, z + dz * out), (dx, dz), allow)
    qdoor('front', LM_A0, -6.0, -1, 0); qdoor('lane', 7.0, LANE_OUT, 0, -1, allow=BIG); qdoor('alley', 9.0, -LANE_OUT, 0, 1, out=1.75)
    x, z, dx, dz = qface(arm, 10.5, LANE_IN + 2.2, 1, 0); entry('vent', f'{pre}-lane', (x, 0, z), (x + dx * 1.2, 0, z + dz * 1.2), (dx, dz), SMALL, surface='floor')
    x, z, dx, dz = qface(arm, LM_A0 - 3.3, 4.0, -1, 0); entry('drop', f'{pre}-{landmark}', (x, 0, z), (x, 0, z), (dx, dz), ['brute'], height=lh, clear=2.8, hulk=False)
    (x0, z0), (x1, z1) = ARMS[arm](0, -LANE_OUT), ARMS[arm](G, LANE_OUT)
    QUARTER_ZONES.append((f'{arm}-quarter', 'mid', min(x0, x1), max(x0, x1), min(z0, z1), max(z0, z1)))
# The new spawns follow the earlier ones (whose indices stay as they were), in id order.
entries[OLD_ENTRIES:] = sorted(entries[OLD_ENTRIES:], key=lambda e: e['id'])
for i, e in enumerate(entries): e['e'] = i

# =====================================================================================================================
# THE WARD ZERO DOORWAY (the town side). The door faces out of the doorway (-x, yaw 270). Its anchor is the clear floor
# point 0.6 m in front of the doorway (the loader probes every anchor with a pilot capsule); the closure fills the doorway
# just in front of the face; the interaction volume and the approach are in front; eight returns outside the portico,
# facing the town.
def door_record(anchor, cx, cz, yaw, slots_key, slots, join=None):
    """A door record; with `join` (the exp-layout-4 draft only) its join cylinder follows the anchor and the approach is the
    cylinder's centre. Without it the record is exp-layout-3's, key for key."""
    fx, fz = forward(yaw)
    rec = {'anchor': anchor}
    if join: rec['join'] = dict(join)
    rec.update({'interact': vol(cx + fx * 1.0, 0, cz + fz * 1.0, 3.0, 3.0, 2.0, yaw),
                'closure': vol(cx + fx * 0.2, 0, cz + fz * 0.2, DOOR_W, DOOR_H, 0.4, yaw),
                'approach': pt(join['x'], join['y'], join['z']) if join else pt(cx + fx * 1.6, 0, cz + fz * 1.6),
                slots_key: slots})
    return rec
WZ_YAW = 270.0
RETURNS = [pt(WZ_DOOR[0] + dx, 0, z, WZ_YAW) for dx in (-4.4, -6.0) for z in (-3.0, -1.0, 1.0, 3.0)]
DOOR_ANCHOR = f'{AREA}:door:exp-ward-zero'

# =====================================================================================================================
# THE JOIN CIRCLE DRAFT (exp-layout-4; docs/EXPEDITION-HUB.md "Join circle at the instance doors"). NOT LIVE: only
# `expedition_layout.py --draft` (or --fixtures) writes it, to Tests/expedition-layout-4-join-draft.json, which nothing at
# runtime loads; Resources/ExpeditionLayout.json stays exp-layout-3. Astra's parser (NativeExpeditionLayout) reads an optional
# door `join` cylinder {x, y, z, r, h}: y is its base (the floor) and its top y + h; a member is inside when the authoritative
# capsule centre is, bounds inclusive. The draft gives every hub entry door (today Ward Zero's) one, moves that door's
# returns out of it and points the door's approach at its centre. The F interaction volume and closure stay exp-layout-3's
# (F entry stays live until the subset transfer exists); the hub record's route keeps measuring the run to the doorway.
# Ward Zero: centre 5 m out from the doorway on the approach line (z 0), between the portico pylons (inner faces z +-3.8),
# r 2.5 m (the removed door ring was 1.35 m round the anchor), its near edge 0.5 m short of the F interaction volume and
# 1.5 m clear of the rubble heap on the axis. h 4 m: a standing pilot's capsule centre (0.85 m), a ground jump's apex
# (2.42 m) and a ground plus air jump's (3.69 m) stay inside, and the top stays under the portico canopy (4.4 m), so no one
# on the canopy counts. Returns: 8 beside the portico, all outside the circle (capsule clear by 1.27 m or more), Hulk-clear,
# 8 m or more from every spawn exit, and their shortest walk to the east gate never needs the circle; the first four (a
# full party) stand at the pylons' town side and the portico's front corners.
JOINS = {'exp-ward-zero': dict(x=WZ_DOOR[0] - 5.0, y=0.0, z=0.0, r=2.5, h=4.0)}
DRAFT_RETURNS = [pt(WZ_DOOR[0] + dx, 0, z, WZ_YAW) for dx, z in ((-4.6, -4.2), (-4.6, 4.2), (-2.9, -6.0), (-2.9, 6.0), (-1.3, -6.0), (-1.3, 6.0), (-2.1, -7.6), (-2.1, 7.6))]
CAPSULE_R, CAPSULE_HALF = 0.42, 0.85          # NativeLocomotionRules.Radius, HalfHeight: the native pilot capsule
JUMP, GRAVITY, AIR_JUMP = 8.5, 23.0, 0.9       # Rules.JumpSpeed, Rules.Gravity, NativeJumpSimulation.AirMultiplier (jumpPower 1)
JOIN_SPAWN_CLEAR = 8.0                         # spawn exits stay this far from the circle's edge and the returns (the doc's door rule)

# =====================================================================================================================
# THE HUB RECORD (exp-layout-3; docs/EXPEDITION-HUB.md). Director metadata for the director (Astra) and the presentation, no
# balance (the manifest's rule): the fence and its four gates (closure, interaction and auto-close sensor volumes, inside and
# outside threshold points), the hub spawn zones by ring (the ring's density, money and kinds are the doc's proposals), the
# wilds districts, the instance routes measured on the walkable floor, and the reserved doorways. The loader ignores the field;
# nothing here is read by today's runtime.
ROUTE_MIN = 100.0              # m of running from a hub gate's threshold to an instance door's approach (target; 70 before exp-layout-7)
RUN, SPRINT = 6.8, 6.8 * 1.35  # m/s: NativeArena's 6.8 x speed (1.0) and the 1.35 sprint multiplier
GATE_APRON, ZONE_APRON = 10.0, 8.0   # no spawn exit / zone volume within this of a hub gate's threshold
RINGS = ('near', 'mid', 'far')   # docs/EXPEDITION-HUB.md proposes each ring's density, money and kinds (balance stays out of the manifest)
# (name, ring, x0, x1, z0, z1): the inner streets beyond the gates' aprons, the ring road and the city's edge, the wilds.
ZONES = [('near-north', 'near', -28, 28, 20.5, 30), ('near-east', 'near', 20.5, 28, -20.5, 20.5), ('near-south', 'near', -28, 28, -30, -20.5), ('near-west', 'near', -28, -20.5, -20.5, 20.5),
         ('mid-north', 'mid', -36, 36, 30, 36), ('mid-east', 'mid', 28, 44, -34, 30), ('mid-south', 'mid', -28, 28, -44, -30), ('mid-west', 'mid', -44, -28, -34, 30),
         ('far-east', 'far', 44, 84, -30, 30), ('far-south', 'far', -30, 30, -84, -44), ('far-west', 'far', -84, -44, -30, 30)]
ZONES = [(n, r, grow_x(x0, -1), grow_x(x1, 1), grow_z(z0, -1), grow_z(z1, 1)) for n, r, x0, x1, z0, z1 in ZONES] + QUARTER_ZONES   # the far ring moved out with the wilds
_PATHS = {}
def zone_of(e):
    """The hub zone a spawn belongs to: the first listed whose volume holds its exit (none for the final plaza's)."""
    x, z = e['exit']['x'], e['exit']['z']
    if in_vol(BOSS_TRIGGER, x, 0, z): return None
    return next((name for name, ring, x0, x1, z0, z1 in ZONES if x0 - 1e-6 <= x <= x1 + 1e-6 and z0 - 1e-6 <= z <= z1 + 1e-6), None)
def gate_paths():
    """Walking distances (m) on the pilot's floor with every gate open: from each hub gate's threshold, 8-connected on the
    0.5 m grid (no corner cutting). Cached: {gate id: {node: metres}} and the collider."""
    if _PATHS: return _PATHS['by'], _PATHS['col']
    col = Collider(list(blocks), bounds=dict(BOUNDS)); col.build(keep=lambda y: abs(y) < 0.05)
    by = {g['id']: walk(col, col.node_at(g['x'], 0, g['z'])) for g in HUB_GATES}
    _PATHS['by'] = by; _PATHS['col'] = col; return by, col
def walk(col, start, forbid=None):
    """Walking distances (m) from `start` over the collider's floor, 8-connected (no corner cutting); `forbid(node)` keeps
    nodes out of the walk."""
    import heapq
    dist = {start: 0.0}; heap = [(0.0, start)]
    while heap:
        d, n = heapq.heappop(heap)
        if d > dist.get(n, 1e18): continue
        i, j, y = n
        for di in (-1, 0, 1):
            for dj in (-1, 0, 1):
                if not di and not dj: continue
                ms = [m for m in col.by_cell.get((i + di, j + dj), ()) if abs(m[2] - y) <= 0.36 and not (forbid and forbid(m))]
                if not ms: continue
                if di and dj and not (col.by_cell.get((i + di, j)) and col.by_cell.get((i, j + dj))): continue
                nd = d + col.cell * (1.41421356 if di and dj else 1.0)
                for m in ms:
                    if nd < dist.get(m, 1e18): dist[m] = nd; heapq.heappush(heap, (nd, m))
    return dist
def gate_distance(x, z, gate=None):
    """The walking distance from the nearest hub gate's threshold (or `gate`'s) to (x, z), and that gate's id."""
    by, col = gate_paths(); n = col.node_at(x, 0, z, 0.8)
    if n is None: return None, None
    best = min(((d.get(n, 1e18), gid) for gid, d in by.items() if gate is None or gid == gate))
    return (r4(best[0]) if best[0] < 1e17 else None), best[1]
def door_gate_route(target, gate_side):
    gid = f'{AREA}:hub-gate:{gate_side}'; d, _ = gate_distance(target['x'], target['z'], gid)
    g = next(x for x in HUB_GATES if x['id'] == gid); straight = math.hypot(target['x'] - g['x'], target['z'] - g['z'])
    return dict(gate=gid, path=d, straight=r4(straight), run=r4(d / RUN), sprint=r4(d / SPRINT), via=route_via(g, target))
# The route as the street shows it (exp-layout-7; the presentation's markers and signs follow it, nothing else reads it): the
# shortest walk from the gate's outer threshold to the target on a floor VIA_CLEAR m clear of every wall (down the middle of
# the streets, round the outer quarters' landmarks), pulled straight between points that see each other on that floor; `via`
# is its turning points, the gate and the target not repeated.
VIA_CLEAR = 1.5
_VIA = {}
def via_graph():
    if 'col' not in _VIA:
        col = Collider(list(blocks), bounds=dict(BOUNDS)); col.build(keep=lambda y: abs(y) < 0.05, radius=VIA_CLEAR); _VIA['col'] = col
    return _VIA['col']
def walk_path(col, start, goal):
    """The shortest 8-connected walk (no corner cutting) from `start` to `goal` over the collider's floor, as its nodes."""
    import heapq
    dist = {start: 0.0}; prev = {}; heap = [(0.0, start)]
    while heap:
        d, n = heapq.heappop(heap)
        if n == goal: break
        if d > dist.get(n, 1e18): continue
        i, j, y = n
        for di in (-1, 0, 1):
            for dj in (-1, 0, 1):
                if not di and not dj: continue
                if di and dj and not (col.by_cell.get((i + di, j)) and col.by_cell.get((i, j + dj))): continue
                for m in col.by_cell.get((i + di, j + dj), ()):
                    if abs(m[2] - y) > 0.36: continue
                    nd = d + col.cell * (1.41421356 if di and dj else 1.0)
                    if nd < dist.get(m, 1e18): dist[m] = nd; prev[m] = n; heapq.heappush(heap, (nd, m))
    assert goal in dist, 'no walk to the route target'
    out = [goal]
    while out[-1] != start: out.append(prev[out[-1]])
    return out[::-1]
def route_via(g, target):
    col = via_graph(); o = g['outside']; s = col.node_at(o['x'], 0, o['z'], 1.0); t = col.node_at(target['x'], 0, target['z'], 2.5)
    pts = [col.cell_xz(n[0], n[1]) for n in walk_path(col, s, t)]
    def sees(a, b):
        n = max(1, int(math.hypot(b[0] - a[0], b[1] - a[1]) / 0.25))
        return all((int((a[0] + (b[0] - a[0]) * k / n - col.bounds['minX']) / col.cell), int((a[1] + (b[1] - a[1]) * k / n - col.bounds['minZ']) / col.cell)) in col.by_cell for k in range(n + 1))
    keep = [0]
    while keep[-1] < len(pts) - 1:
        i = keep[-1]; j = i + 1
        while j + 1 < len(pts) and sees(pts[i], pts[j + 1]): j += 1
        keep.append(j)
    return [dict(x=r4(round(pts[k][0], 1)), y=0.0, z=r4(round(pts[k][1], 1))) for k in keep[1:-1]]
def hub():
    fx, fz = forward(WZ_YAW); approach = pt(WZ_DOOR[0] + fx * 1.6, 0, WZ_DOOR[1] + fz * 1.6)
    zones = []
    for name, ring, x0, x1, z0, z1 in ZONES:
        V = vol((x0 + x1) / 2, 0, (z0 + z1) / 2, x1 - x0, 8, z1 - z0, 0)
        inside = [e['e'] for e in entries if zone_of(e) == name]
        ds = [gate_distance(e['exit']['x'], e['exit']['z'])[0] for e in entries if e['e'] in inside]
        zones.append(dict(id=f'{AREA}:zone:{name}', ring=ring, volume=V, spawns=inside, gateDistance=[r4(min(ds)), r4(max(ds))] if ds else [0.0, 0.0]))
    wilds = [dict(id=f'{AREA}:wilds:{name}', side=side, volume=vol((x0 + x1) / 2, 0, (z0 + z1) / 2, x1 - x0, 8, z1 - z0, 0)) for name, side, x0, x1, z0, z1 in WILDS]
    routes = [dict(to='exp-ward-zero', approach=approach, **door_gate_route(approach, 'east'))]
    reserved = []
    for k, p_ in sorted(RESERVED.items()):
        fx2, fz2 = forward(p_['yaw']); ap = pt(p_['x'] + fx2 * 1.6, 0, p_['z'] + fz2 * 1.6)
        reserved.append(dict(building=k, x=p_['x'], y=p_['y'], z=p_['z'], yaw=p_['yaw'], approach=ap, **door_gate_route(ap, 'west' if k == 'exp-foundry' else 'south')))
    return {'fence': {'height': FENCE_H, 'thickness': FENCE_T, 'segments': [dict(f) for f in FENCES]},
            'gates': [dict(g) for g in HUB_GATES], 'apron': dict(spawn=GATE_APRON, zone=ZONE_APRON),
            'zones': zones, 'wilds': wilds, 'routes': routes, 'reserved': reserved}

def area(draft=False):
    """The town's area record: exp-layout-3's (live), or with `draft` the exp-layout-4 join circle draft (not live)."""
    fx, fz = forward(WZ_YAW)
    anchors = [dict(id=DOOR_ANCHOR, kind='door', building='exp-ward-zero', **pt(WZ_DOOR[0] + fx * 0.6, 0, WZ_DOOR[1] + fz * 0.6, WZ_YAW)),
               dict(id=f'{AREA}:machine:0', kind='machine', building='', **MACHINE)]
    anchors += [dict(id=f'{AREA}:cache:{i}', kind='chest', building='', **c) for i, c in enumerate(CACHES)]
    def front(p, d):
        fx, fz = forward(p['yaw']); return dict(x=r4(p['x'] + fx * d), y=p['y'], z=r4(p['z'] + fz * d))
    inter = [dict(anchor=f'{AREA}:machine:0', **front(MACHINE, 0.9), range=2.5)]
    inter += [dict(anchor=f'{AREA}:cache:{i}', **front(c, 0.8), range=2.0) for i, c in enumerate(CACHES)]
    return {'id': AREA, 'role': 'world', 'map': MAP, 'blockHash': block_hash(game_map()),
            'anchors': anchors, 'doors': [door_record(DOOR_ANCHOR, WZ_DOOR[0], WZ_DOOR[1], WZ_YAW, 'returns', DRAFT_RETURNS if draft else RETURNS, JOINS['exp-ward-zero'] if draft else None)], 'interactions': inter,
            'sanctuary': {'volumes': [SANCTUARY], 'slots': SLOTS},
            'gates': [{'id': f'{AREA}:gate:final', 'opens': 'final', 'closure': GATE}],
            'spawns': entries, 'boss': {'trigger': BOSS_TRIGGER, 'points': BOSS_POINTS}, 'hub': hub()}

# =====================================================================================================================
# THE MAP ENTRY (UnityMaps.json shape). The PvP spawn tables the map validator requires stand on the sanctuary slots
# (y = floor + 1.1); Expedition places pilots from the manifest's slots, never from these.
def clean(b): return {k: b[k] for k in ('id', 'material', 'pos', 'size', 'rotation', 'role', 'mass', 'segment', 'hp')}
def pilot_blocks():
    out = []
    for b in blocks:
        if not b['_pilot']: continue
        x0, x1, z0, z1 = footprint(b); top = yspan(b)[1]
        out.append(dict(id=b['id'] + '-top', center=v((x0 + x1) / 2, 0, (z0 + z1) / 2), size=v(x1 - x0, 0, z1 - z0), bottom=r4(top - 0.1)))
    return out
def game_map():
    sp = [dict(pos=v(s['x'], 1.1, s['z']), yaw=s['yaw']) for s in SLOTS]
    return dict(id=MAP, name='Expedition City', tag='A zombie city at dusk. The sanctuary holds; the streets do not.', theme='expedition', music=None,
                ambience='', look=MAP, sky=0x2A2448, floor=0x2C2A30, accent=0xFF8A4C, rotation=False,
                bounds=dict(**BOUNDS, ceiling=CEIL, killY=KILL),
                spawns=dict(ffa=sp, teams=[dict(side=0, points=sp[:4]), dict(side=1, points=sp[4:])]),
                blocks=[clean(b) for b in blocks], segments=[], hazards=[], pilotBlocks=pilot_blocks(), nav=dict(nodes=[], links=[]),
                ramps=[], navHints=dict(drops=[], safe=[], collisionOff=[]))
def draft():
    return dict(schema=1, draft=True, note='Draft blockout from docs/arenas/scripts/expedition_city.py; not game content. The Expedition town (exp-city); import steps in docs/EXPEDITION-LAYOUT.md.',
                materials=[], maps=[game_map()])

# =====================================================================================================================
# THE LOOK (MapLooks.json entry `expedition-city`): dusk over a dead city. A low orange sun in the west-north-west under an
# indigo zenith, long soft shadows, mauve haze in the streets, the first stars; practical lights for the sanctuary's
# brazier and lamps, the Meridian frontage's cold light, the final gate's warning lamp and a burning wreck.
# The anime map style (docs/MAP-STYLE-ANIME.md, Core NativeMapStyle.MapLookToon): the city's `toon` block, written after
# `blocks` in key groups (MapLooks.json keeps it readable); presentation only.
TOON_GROUPS = [('on', 'softness', 'shadeEdge', 'lightEdge', 'halfTone', 'round', 'flatten', 'lift', 'points', 'rimIntensity', 'edgeLight', 'shade', 'lit', 'rimColor'), ('detail', 'gradient', 'wash', 'baseShadow'), ('plinths', 'copings', 'chips', 'posts', 'specular', 'specularEdge', 'occlusion', 'occlusionHeight'), ('line', 'lineFadeStart', 'lineFadeEnd', 'crease', 'lineColor'), ('saturation', 'contrast', 'gradeShadow', 'gradeLight'), ('skyToon', 'clouds', 'cloudScale', 'bandWidth', 'cloudLit', 'cloudShade', 'horizonBand'), ('key',), ('rim',), ('ambient',), ('sky',), ('fogColor', 'fogStart', 'background', 'hazeColor')]
TOON = {
    'on': True, 'softness': 0.025, 'shadeEdge': 0.03, 'lightEdge': 0.12, 'halfTone': 0.0, 'round': 0.1, 'flatten': 0.35, 'lift': 0.3, 'points': 0.7, 'rimIntensity': 0.6, 'edgeLight': 0.5, 'shade': dict(x=0.66, y=0.78, z=1.36), 'lit': dict(x=1.1, y=1.0, z=0.9), 'rimColor': 0x9db8ff,
    'detail': 0.3, 'gradient': 0.14, 'wash': 0.06, 'baseShadow': 0.0,
    'plinths': 0, 'copings': 1, 'chips': 1, 'posts': 1, 'specular': 0.55, 'specularEdge': 0.965, 'occlusion': 0.4, 'occlusionHeight': 0.22,
    'line': 1.0, 'lineFadeStart': 40, 'lineFadeEnd': 110, 'crease': 0.02, 'lineColor': 0x2a1e40,
    'saturation': 1.16, 'contrast': 1.07, 'gradeShadow': dict(x=0.94, y=0.96, z=1.08), 'gradeLight': dict(x=1.05, y=1.0, z=0.95),
    'skyToon': 1, 'clouds': 0.62, 'cloudScale': 1.0, 'bandWidth': 0.14, 'cloudLit': 0xffb89a, 'cloudShade': 0x7a70b0, 'horizonBand': 0xffc48a,
    'key': dict(color=16756354, intensity=0.95, elevation=9, bearing=288, **{'from': dict(x=0, y=0, z=0)}, shadows='soft', shadowStrength=0.85),
    'rim': dict(color=8366335, intensity=0.45, elevation=26, bearing=108, **{'from': dict(x=0, y=0, z=0)}, shadows='none', shadowStrength=1),
    'ambient': dict(sky=5926840, skyScale=0.85, equator=7236250, equatorScale=0.6, ground=2761792, groundScale=0.4),
    'sky': dict(variant='dusk', horizon=15901306, zenith=1977946, glow=16756872, disc=16769200, cloud=15771792, discSize=0.05, halo=0.3, stars=0.25, crater=0, cloudSpeed=0.25, elevation=9, bearing=288, dir=dict(x=0, y=0, z=0)),
    'fogColor': 0x7c6e9e, 'fogStart': 12, 'background': 0x2a2240, 'hazeColor': 0x6a6488,
}
def look():
    L = lambda id, x, y, z, color, intensity, rng, flicker='', rate=0, period=0, length=0, depth=0, phase=0: dict(
        id=id, flicker=flicker, pos=dict(x=x, y=y, z=z), color=color, intensity=intensity, range=rng, rate=rate, period=period, length=length, depth=depth, phase=phase)
    BL = lambda block, look_, color=0, note='': dict(block=block, look=look_, color=color, band=False, note=note)
    return {'id': MAP, 'look': MAP, 'farClip': 420, 'background': 0x2A2240,
            'key': dict(color=0xFF9C5C, intensity=0.95, elevation=9, bearing=288, **{'from': dict(x=0, y=0, z=0)}, shadows='soft', shadowStrength=0.85),
            'rim': dict(color=0x5A6AE0, intensity=0.42, elevation=26, bearing=108, **{'from': dict(x=0, y=0, z=0)}, shadows='none', shadowStrength=1),
            'ambient': dict(sky=0x4E4C80, skyScale=0.5, equator=0x7A5A60, equatorScale=0.3, ground=0x1E1820, groundScale=0.4),
            'fog': dict(mode='linear', color=0x5C4658, skyLerp=0, start=30, end=175, density=0),
            'shadows': dict(cascades=4, distance=90, fallbackCascades=2, fallbackDistance=65),
            'sky': dict(variant='dusk', horizon=0xD87A52, zenith=0x1C1A3A, glow=0xFF9A5A, disc=0xFFD8A8, cloud=0x9A6A7A, discSize=0.055, halo=0.28,
                        stars=0.22, crater=0, cloudSpeed=0.25, elevation=9, bearing=288, dir=dict(x=0, y=0, z=0)),
            'lights': [L('sanctuary-brazier', 0, 1.4, 0, 0xFF8A3C, 1.8, 13, 'fire', rate=5, depth=0.3),
                       L('sanctuary-lamp-ne', 9.5, 4.2, 9.5, 0xFFC27A, 1.2, 10), L('sanctuary-lamp-sw', -9.5, 4.2, -9.5, 0xFFC27A, 1.2, 10),
                       L('meridian-portico', WZ_DOOR[0] - 1.6, 4.0, 0, 0x9FE8FF, 1.4, 12, 'stutter', period=9, length=0.2, depth=0.6),
                       L('final-gate', 0, 9.0, 34.0, 0xFF3B30, 1.3, 16), L('wreck-fire-es', 22.6, 1.6, -3.9, 0xFF6A1E, 1.6, 9, 'fire', rate=6.5, depth=0.4, phase=0.4),
                       L('sodium-ring-e', 31.0, 5.5, -26.0, 0xFF9A3C, 1.1, 12), L('sodium-ring-w', -31.0, 5.5, 26.0, 0xFF9A3C, 1.1, 12)],
            'blockLooks': [BL('expedition-city-slab-*', 'asphalt', 0x3A3840, 'the street slab'),
                           BL('expedition-city-sanctuary-paving', 'cobble', 0x7A6E66, 'the sanctuary plaza'),
                           BL('expedition-city-square-paving', 'cobble', 0x6E6A70, 'the final plaza'),
                           BL('expedition-city-sanctuary-fence-wood-*', 'timber', 0x6E5236, 'the sanctuary palisade (north and west mouths)'),
                           BL('expedition-city-sanctuary-fence-metal-*', 'corrugated', 0x5E625E, 'the sanctuary scrap-metal fence (east and south mouths)'),
                           BL('expedition-city-sanctuary-brazier', 'tar', 0x2A2422, 'the brazier'),
                           BL('expedition-city-gate-*', 'concrete-board', 0x8C8478, 'the final gatehouse'),
                           BL('expedition-city-bldg-wz-*', 'concrete-board', 0xB8BEC4, 'Meridian Biocontainment (Ward Zero)'),
                           BL('expedition-city-bldg-meridian-*', 'concrete-board', 0xA8AEB2, 'the Meridian annexes'),
                           BL('expedition-city-wz-*', 'concrete-board', 0xD0D6DA, 'the Meridian portico'),
                           BL('expedition-city-bldg-foundry-*', 'corrugated', 0x6A5A50, 'the foundry (reserved)'),
                           BL('expedition-city-foundry-*', 'corrugated', 0x5A6062, 'the foundry shutter (sealed)'),
                           BL('expedition-city-transit-boards', 'timber', 0x7A6448, 'the station boards (sealed)'),
                           BL('expedition-city-bldg-transit-*', 'concrete-board', 0x9A948A, 'the transit station (reserved)'),
                           BL('expedition-city-transit-*', 'steel-green', 0x3E5A4E, 'the station canopy'),
                           BL('expedition-city-bldg-city-hall*', 'concrete-board', 0xA49A8A, 'the city hall'),
                           BL('expedition-city-breach-*', 'brick-buff', 0x9A8A70, 'bricked-up shopfronts (Hulk breaches)'),
                           BL('expedition-city-roof-*', 'tar', 0x2E2A2C, 'roof clutter'),
                           BL('expedition-city-square-*', 'concrete-board', 0x8A8478, 'the dais and planters'),
                           BL('expedition-city-crate-*', 'timber', 0, 'crates'),
                           BL('expedition-city-wild-container-*', 'corrugated', 0x6A4434, 'shipping containers (the wilds)'),
                           BL('expedition-city-wild-boxcar-*', 'corrugated', 0x5A3428, 'rail boxcars (Ironworks Yard)'),
                           BL('expedition-city-wild-rubble-*', 'concrete-board', 0x5E5854, 'rubble and slag heaps'),
                           BL('expedition-city-wild-wall-*', 'brick-red', 0x5E3A30, 'ruined wall stubs'),
                           BL('expedition-city-wild-booth-*', 'concrete-board', 0x8E8A80, 'checkpoint and ticket booths'),
                           BL('expedition-city-wild-sandbag-*', 'plain', 0x7E7252, 'sandbag nests'),
                           BL('expedition-city-wild-tree-*', 'timber', 0x2A221C, 'dead trees'),
                           BL('expedition-city-wild-pylon-*', 'concrete-board', 0x6A665E, 'power pylon footings'),
                           BL('expedition-city-wild-flood-*', 'steel-green', 0x3A3E3A, 'floodlight tower footings'),
                           BL('expedition-city-wild-tank-*', 'corrugated', 0x464A46, 'the coal hopper tank'),
                           BL('expedition-city-wild-shelter-*', 'steel-green', 0x3A4A44, 'bus shelters (their back panels)'),
                           BL('expedition-city-dumpster-*', 'steel-green', 0x3C4A3C, 'dumpsters'),
                           BL('expedition-city-barrier-*', 'concrete-board', 0xB0A898, 'jersey barriers'),
                           BL('expedition-city-car-fc-0', 'plain', 0xD4D4D0, 'police car (dark light bar)'), BL('expedition-city-car-fc-1', 'plain', 0xD4D4D0, 'police car (its light bar still flashing)'),
                           BL('expedition-city-car-av-0', 'plain', 0x3A4A62, 'abandoned sedan'), BL('expedition-city-car-es-0', 'plain', 0x7A2A24, 'abandoned sedan'),
                           BL('expedition-city-car-ws-0', 'plain', 0x3A5A44, 'abandoned sedan'), BL('expedition-city-car-rw-0', 'plain', 0x9A7A3A, 'overturned car'),
                           BL('expedition-city-car-rs-1', 'plain', 0x5A2A30, 'overturned car'),
                           BL('expedition-city-car-*', 'plain', 0x2E2826, 'wrecked cars (burnt out)'),
                           BL('expedition-city-bus-*', 'plain', 0xA89458, 'the crashed bus (its front burnt)'), BL('expedition-city-truck-*', 'plain', 0xA8A69E, 'the box truck'),
                           BL('expedition-city-ambulance-*', 'plain', 0xD8D8D0, 'the ambulance'), BL('expedition-city-taxi-*', 'plain', 0xD0A830, 'the taxi'),
                           BL('material:brick', 'brick-red', 0x7A4A3E, 'tenements, shops and corners'), BL('material:stone', 'concrete-board', 0x8A8478, 'towers and civic blocks'),
                           BL('material:metal', 'corrugated', 0x5A6062, 'depots'), BL('material:wood', 'timber', 0, 'anything else wood')],
            'kitLooks': [], 'dressing': MAP, 'backdrop': MAP, 'atmosphere': MAP, 'ambience': MAP,
            'blocks': dict(dressed=True, dust=0.06, haze=0.3, hazeTop=3.2, hazeColor=0x5E4A62), 'toon': TOON}

MAPLOOKS = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'unity', 'ChipArenaConversion', 'Assets', 'Conversion', 'Resources', 'MapLooks.json'))
def look_text():
    """The look as MapLooks.json writes its entries (one field per line, one light or block look per line)."""
    L = look(); j = lambda x: json.dumps(x, separators=(',', ':'))
    head = ',' .join(f'{j(k)}:{j(L[k])}' for k in ('id', 'look', 'farClip', 'background'))
    rows = [' {' + head + ',']
    for k in ('key', 'rim', 'ambient', 'fog', 'shadows', 'sky'): rows.append(f'   {j(k)}:{j(L[k])},')
    rows.append('   "lights":['); rows += ['    ' + j(x) + (',' if i < len(L['lights']) - 1 else '') for i, x in enumerate(L['lights'])]; rows.append('   ],')
    rows.append('   "blockLooks":['); rows += ['    ' + j(x) + (',' if i < len(L['blockLooks']) - 1 else '') for i, x in enumerate(L['blockLooks'])]; rows.append('   ],')
    rows.append(f'   "kitLooks":{j(L["kitLooks"])},')
    rows.append('   ' + ','.join(f'{j(k)}:{j(L[k])}' for k in ('dressing', 'backdrop', 'atmosphere', 'ambience')) + ',')
    rows.append(f'   "blocks":{j(L["blocks"])},')
    groups = [','.join(f'{j(k)}:{j(L["toon"][k])}' for k in g) for g in TOON_GROUPS]
    rows.append('   "toon":{' + groups[0] + ','); rows += ['     ' + g + ',' for g in groups[1:-1]]; rows.append('     ' + groups[-1] + '}}')
    return '\n'.join(rows)
def write_maplooks(path=MAPLOOKS):
    """Replace this map's entry in MapLooks.json's maps where it stands (the zone looks follow it since their activation), or
    insert it as the last entry; every other byte stays as it is."""
    text = open(path, encoding='utf-8').read(); entry = look_text(); start = text.find('\n {"id":"' + MAP + '",')
    tail = '\n],\n"preview":['
    if start >= 0:
        end = text.index(tail, start); after = text.find('\n {"id":', start + 1, end)
        text = text[:start] + '\n' + entry + (text[end:] if after < 0 else ',' + text[after:])
    else:
        end = text.index(tail); assert text[end - 2:end] == '}}', 'unexpected MapLooks.json layout'
        text = text[:end] + ',\n' + entry + text[end:]
    json.loads(text)
    with open(path, 'w', encoding='utf-8') as f: f.write(text)

# =====================================================================================================================
# CHECKS
def checks():
    out, ok = [], True
    colliders = [b for b in blocks]
    gate_block = vol_block(MAP + '-gate-closure', GATE)
    shut = Collider(colliders + [gate_block], bounds=dict(BOUNDS)); shut.build(keep=lambda y: abs(y) < 0.05)
    open_ = Collider(colliders, bounds=shut.bounds); open_.build(keep=lambda y: abs(y) < 0.05)
    def in_fp(n, col):
        x, z = col.cell_xz(n[0], n[1]); return FP[0] < x < FP[1] and GATE['z'] < z < FP[3]
    starts = []
    for i, s in enumerate(SLOTS):
        n = shut.node_at(s['x'], s['y'], s['z']); assert n, f'slot {i} not on walkable floor'; starts.append(n)
    reach = shut.flood(starts)
    town = [n for n in shut.graph if not in_fp(n, shut)]; plaza = [n for n in shut.graph if in_fp(n, shut)]
    got = [n for n in town if n in reach]; gotp = [n for n in plaza if n in reach]
    out.append(f'gate shut: town floor (y 0, outside the final plaza) {len(town)} standable 0.5 m cells, {len(got)} reached from the sanctuary slots ({100 * len(got) / max(1, len(town)):.1f}%), {len(town) * 0.25:.0f} m2')
    if len(got) != len(town):
        ok = False
        for g in components(shut, [n for n in town if n not in reach])[:12]: out.append('   UNREACHED %d cells round (%.1f, %.1f)' % g)
    out.append(f'gate shut: final plaza {len(plaza)} standable cells, {len(gotp)} reached'); ok &= len(plaza) > 0 and not gotp
    starts_o = [open_.node_at(s['x'], s['y'], s['z']) for s in SLOTS]; reach_o = open_.flood(starts_o)
    plaza_o = [n for n in open_.graph if in_fp(n, open_)]; gotpo = [n for n in plaza_o if n in reach_o]; all_o = [n for n in open_.graph if n in reach_o]
    out.append(f'gate open: final plaza {len(plaza_o)} standable cells, {len(gotpo)} reached ({100 * len(gotpo) / max(1, len(plaza_o)):.1f}%); whole town {len(all_o)} of {len(open_.graph)} cells')
    ok &= len(gotpo) == len(plaza_o) and len(all_o) == len(open_.graph)
    # Every walking edge from outside the plaza into it lies inside the gate volume's footprint (so the gate is the only way in).
    cross = []
    for n in reach_o:
        for m in open_.neighbours(n):
            if in_fp(n, open_) != in_fp(m, open_):
                (x0, z0), (x1, z1) = open_.cell_xz(n[0], n[1]), open_.cell_xz(m[0], m[1]); cross.append(((x0 + x1) / 2, (z0 + z1) / 2))
    inside = [c for c in cross if abs(c[0] - GATE['x']) <= GATE['sx'] / 2 + 1e-6 and abs(c[1] - GATE['z']) <= 4.0]
    xs = [c[0] for c in cross]
    out.append(f'walking edges into the final plaza: {len(cross)}, inside the gate volume\'s width (x {GATE["x"] - GATE["sx"] / 2:g}..{GATE["x"] + GATE["sx"] / 2:g}): {len(inside)}; crossings span x {min(xs):.2f}..{max(xs):.2f}')
    ok &= len(cross) > 0 and len(inside) == len(cross)
    # The plaza's enclosure: every boundary cell outside the gate opening is solid to 10 m or more under an exclusion column.
    pbs = pilot_blocks(); bad = 0; total = 0
    def column(x, z): return any(abs(x - p['center']['x']) <= p['size']['x'] / 2 + 1e-6 and abs(z - p['center']['z']) <= p['size']['z'] / 2 + 1e-6 for p in pbs)
    for k in range(int((FP[1] - FP[0]) / 0.5) + 2):
        for x, z in ((FP[0] - 0.25 + k * 0.5, FP[2] - 0.25), (FP[0] - 0.25 + k * 0.5, FP[3] + 0.25)):
            total += 1
            if abs(x - GATE['x']) <= GATE['sx'] / 2 and z < FP[2]: continue
            top = max([yspan(b)[1] for b in blocks if dist_xz(b, x, z) <= 1e-9 and yspan(b)[0] <= 0.01] + [0])
            if top < 10 or not column(x, z): bad += 1
    for k in range(int((FP[3] - FP[2]) / 0.5) + 2):
        for x, z in ((FP[0] - 0.25, FP[2] - 0.25 + k * 0.5), (FP[1] + 0.25, FP[2] - 0.25 + k * 0.5)):
            total += 1; top = max([yspan(b)[1] for b in blocks if dist_xz(b, x, z) <= 1e-9 and yspan(b)[0] <= 0.01] + [0])
            if top < 10 or not column(x, z): bad += 1
    out.append(f'final plaza enclosure: {total} boundary samples, {total - bad} solid to 10 m or more under an exclusion column or in the gate opening'); ok &= bad == 0
    lintel = next(b for b in blocks if b['id'] == MAP + '-gate-lintel')
    out.append(f'gate opening {GATE_W:g} x {GATE_H:g} m under the lintel (bottom {yspan(lintel)[0]:g} m); gate volume {GATE["sx"]:g} x {GATE["sy"]:g} x {GATE["sz"]:g} at z {GATE["z"]:g}')
    ok &= abs(vol_floor(GATE)) < 1e-6 and abs(yspan(lintel)[0] - GATE['sy']) < 1e-6 and abs(lintel['size']['x'] - GATE['sx']) < 1e-6
    # Records on reached floor at their own level, with clearance.
    col = open_
    def onfloor(x, y, z):
        n = shut.node_at(x, y, z, 0.6); return n is not None and n in reach and shut.floor_under(x, z, y)
    def onfloor_any(x, y, z):
        n = open_.node_at(x, y, z, 0.6); return n is not None and n in reach_o and open_.floor_under(x, z, y)
    def check_points(label, pts, need, ylo=0.35, yhi=HEIGHT + 0.05, plaza_ok=False):
        bad = []; worst = 99
        for name, p in pts:
            f = onfloor_any(p['x'], p['y'], p['z']) if plaza_ok else onfloor(p['x'], p['y'], p['z'])
            c = col.clearance(p['x'], p['z'], p['y'] + ylo, p['y'] + yhi); worst = min(worst, c)
            if not f or c < need: bad.append(f"{name} ({p['x']:g}, {p['y']:g}, {p['z']:g}) floor {f} clear {c:.2f} ({col.nearest_block(p['x'], p['z'], p['y'] + ylo, p['y'] + yhi)[1]})")
        out.append(f'{label}: {len(pts)} on reached floor at their own level with {need:g} m clear: {len(pts) - len(bad)} (tightest {worst:.2f} m)' + ''.join('\n   BAD ' + s for s in bad))
        return not bad
    ok &= check_points('sanctuary slots', [(f'slot{i}', s) for i, s in enumerate(SLOTS)], PILOT_CLEAR)
    ok &= check_points('Ward Zero returns', [(f'return{i}', s) for i, s in enumerate(RETURNS)], PILOT_CLEAR)
    a = area(); d = a['doors'][0]
    ok &= check_points('door approach', [('approach', d['approach'])], PILOT_CLEAR)
    ok &= check_points('machine and cache anchors (piece footprint)', [(x['id'], x) for x in a['anchors'] if x['kind'] != 'door'], 1.0)
    ok &= check_points('interaction points', [(x['anchor'], x) for x in a['interactions']], PILOT_CLEAR)
    ok &= check_points('boss points (Hulk-sized)', [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)], HULK_CLEAR, yhi=HULK_BAND, plaza_ok=True)
    sp = []
    for e in entries:
        x = e['exit']; fp = FP[0] < x['x'] < FP[1] and FP[2] < x['z'] < FP[3]
        f = onfloor_any(x['x'], x['y'], x['z']) if fp else onfloor(x['x'], x['y'], x['z'])
        hulk = 'hulk' in e['allow']; yhi = (e['height'] if e['kind'] == 'drop' else (HULK_BAND if hulk else HEIGHT + 0.05))
        skip = {e['block']} if e['kind'] == 'breach' else set()
        c = col.clearance(x['x'], x['z'], x['y'] + (0.05 if e['kind'] == 'drop' else 0.35), x['y'] + yhi - (0.05 if e['kind'] == 'drop' else 0), skip=skip)
        sp.append((e, f, c, fp))
    bad = [f"{e['id']} floor {f} clear {c:.2f} < {e['clear']:g}" for e, f, c, fp in sp if not f or c + 1e-6 < e['clear']]
    near_ids = {e['id']: col.nearest_block(e['exit']['x'], e['exit']['z'], 0.05, 4.0)[1] for e, f, c, fp in sp}
    bad = [b + f" ({near_ids[b.split(' ')[0]]})" for b in bad]
    kinds = {}
    for e in entries: kinds[e['kind']] = kinds.get(e['kind'], 0) + 1
    out.append(f'spawn exits: {len(entries)} ({", ".join(f"{k} {n}" for k, n in sorted(kinds.items()))}) on reached floor at their own level with their clear radius: {len(entries) - len(bad)}' + ''.join('\n   BAD ' + s for s in bad))
    ok &= not bad
    for e, f, c, fp in sp:
        if e['kind'] == 'drop': out.append(f"   drop {e['id']}: {'Hulk' if e['hulk'] else 'brute'}, fall {e['height']:g} m, landing clear {c:.2f} m (needs {e['clear']:g})")
    hk = [(e, c) for e, f, c, fp in sp if 'hulk' in e['allow']]
    out.append(f'Hulk spawns: {len(hk)} (breach {sum(1 for e, c in hk if e["kind"] == "breach")}, drop {sum(1 for e, c in hk if e["kind"] == "drop")}), every one {HULK_CLEAR:g} m clear to {HULK_BAND:g} m or its drop radius: {all(c >= max(HULK_CLEAR, e["clear"]) - 1e-6 for e, c in hk)}')
    ok &= all(c >= max(HULK_CLEAR, e['clear']) - 1e-6 for e, c in hk)
    # Allow lists follow the existing entry rules (NativeWardEntries.Allows): Hulks only from breaches and Hulk drops,
    # brutes the only other drop, nothing else from a breach.
    def allows(e, kind): return (e['kind'] == 'breach' or e['kind'] == 'drop' and e.get('hulk')) if kind == 'hulk' else e['kind'] != 'breach' and (e['kind'] != 'drop' or kind == 'brute' and not e.get('hulk'))
    rule = all(e['allow'] and all(allows(e, k) for k in e['allow']) for e in entries)
    out.append(f'allow lists within the entry rules (Hulk: breach or Hulk drop; drops: brute; doors and vents: shambler, runner, spitter, brute): {rule}'); ok &= rule
    # The sanctuary: no exit or origin inside its volume, every exit 8 m or more outside it.
    inside = [e['id'] for e in entries if in_vol(SANCTUARY, e['exit']['x'], e['exit']['y'], e['exit']['z']) or in_vol(SANCTUARY, e['from']['x'], e['from']['y'], e['from']['z'])]
    near = min((vol_dist_xz(SANCTUARY, e['exit']['x'], e['exit']['z']), e['id']) for e in entries)
    out.append(f'spawns inside the sanctuary volume: {len(inside)}; nearest exit {near[0]:.1f} m outside it ({near[1]})'); ok &= not inside and near[0] >= 8.0
    final = [e['id'] for e, f, c, fp in sp if in_vol(BOSS_TRIGGER, e['exit']['x'], 0, e['exit']['z'])]
    out.append(f'final-area spawns (exit inside the boss trigger; for the final encounter only): {len(final)}: ' + ', '.join(i.split(':')[-1] for i in final))
    ok &= all(fp == in_vol(BOSS_TRIGGER, e['exit']['x'], 0, e['exit']['z']) for e, f, c, fp in sp)
    # Spacing: exits 3 m or more apart; slots and returns 1.4 m or more apart; spawns 6 m or more from the slots, returns and the door.
    ex = [(e['id'], e['exit']['x'], e['exit']['z']) for e in entries]
    mind = min((math.hypot(a[1] - b[1], a[2] - b[2]), a[0], b[0]) for i, a in enumerate(ex) for b in ex[i + 1:])
    out.append('closest two spawn exits: %.1f m (%s, %s)' % (mind[0], mind[1].split(':')[-1], mind[2].split(':')[-1])); ok &= mind[0] >= 3.0
    for label, pts in (('sanctuary slots', SLOTS), ('returns', RETURNS)):
        m = min(math.hypot(a['x'] - b['x'], a['z'] - b['z']) for i, a in enumerate(pts) for b in pts[i + 1:]); out.append(f'{label}: closest pair {m:.2f} m'); ok &= m >= 1.4 - 1e-6
    door_pts = RETURNS + [d['approach']] + [pt(WZ_DOOR[0], 0, WZ_DOOR[1])]
    md = min((math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']), e['id']) for e in entries for p in door_pts)
    out.append('nearest spawn exit to the Ward Zero door, approach or returns: %.1f m (%s)' % (md[0], md[1].split(':')[-1])); ok &= md[0] >= 6.0
    # Door volumes: interaction and closure on the floor in front of the face, approach inside the interaction volume, returns outside it.
    ok_d = in_vol(d['interact'], d['approach']['x'], 0.0, d['approach']['z']) and not any(in_vol(d['interact'], p['x'], 0.0, p['z']) for p in RETURNS)
    ok_d &= shut.floor_under(d['interact']['x'], d['interact']['z'], 0.0) and shut.floor_under(d['closure']['x'], d['closure']['z'], 0.0)
    out.append(f'Ward Zero door: doorway ({WZ_DOOR[0]:g}, 0, {WZ_DOOR[1]:g}) yaw {WZ_YAW:g}, anchor 0.6 m out, closure {d["closure"]["sx"]:g} x {d["closure"]["sy"]:g} m on the facade, approach inside the interaction volume, returns outside it: {ok_d}'); ok &= ok_d
    # ---------------- the hub (exp-layout-3): fence, gates, routes, zones ----------------
    H = a['hub']; gates = H['gates']
    # The fence is closed except at its gates: with every gate's closure solid, the slots reach only the sanctuary's own floor.
    closed = Collider(colliders + [gate_block] + [vol_block(MAP + '-hub-closure-' + g['side'], g['closure']) for g in gates], bounds=dict(BOUNDS)); closed.build(keep=lambda y: abs(y) < 0.05)
    reach_c = closed.flood([closed.node_at(p_['x'], 0, p_['z']) for p_ in SLOTS])
    outside = [n for n in reach_c if not in_vol(SANCTUARY, closed.cell_xz(n[0], n[1])[0], 0, closed.cell_xz(n[0], n[1])[1], pad=0.3)]
    out.append(f'fence closed except at its gates: with the {len(gates)} gate closures solid the slots reach {len(reach_c)} cells, {len(outside)} of them outside the sanctuary volume'); ok &= len(reach_c) > 0 and not outside
    # With the gates open, every walking edge across the sanctuary's edge lies in a gate's opening.
    crossings = []; bad_cross = []
    for n in reach_o:
        for m in open_.neighbours(n):
            (ax0, az0), (ax1, az1) = open_.cell_xz(n[0], n[1]), open_.cell_xz(m[0], m[1])
            if in_vol(SANCTUARY, ax0, 0, az0) != in_vol(SANCTUARY, ax1, 0, az1):
                mx, mz = (ax0 + ax1) / 2, (az0 + az1) / 2; crossings.append((mx, mz))
                if not any(in_vol(g['closure'], mx, 1.0, mz, pad=0.5) for g in gates): bad_cross.append((mx, mz))
    out.append(f'walking edges across the sanctuary edge: {len(crossings)}, all inside a hub gate opening: {not bad_cross}' + ''.join(f'\n   BAD crossing ({x:.2f}, {z:.2f})' for x, z in bad_cross[:8])); ok &= len(crossings) > 0 and not bad_cross
    # Each gate: its closure fills exactly the opening between its two fence sections (nothing in the map collides there), on the
    # floor, the fence's full height; a Hulk passes it open; the interaction volume holds both threshold points; the sensor holds it.
    for g in gates:
        fb = [b for b in blocks if b['id'] in g['fence']]; cl = g['closure']; ax = 'x' if g['yaw'] % 180 == 0 else 'z'
        ends = sorted(abs((footprint(b)[1] if (footprint(b)[0] + footprint(b)[1]) / 2 < cl['x'] else footprint(b)[0]) - cl['x']) if ax == 'x' else abs((footprint(b)[3] if (footprint(b)[2] + footprint(b)[3]) / 2 < cl['z'] else footprint(b)[2]) - cl['z']) for b in fb)
        clear_open = open_.clearance(cl['x'], cl['z'], 0.05, FENCE_H - 0.05, maxr=3.0)
        hulk_ok = open_.blocked(cl['x'], cl['z'], 0.0, HULK_RADIUS, HULK_HEIGHT) is False
        ok_g = len(fb) == 2 and all(abs(e - cl['sx'] / 2) < 1e-3 for e in ends) and abs(vol_floor(cl)) < 1e-6 and abs(cl['sy'] - FENCE_H) < 1e-6 and clear_open >= cl['sx'] / 2 - 1e-3 and hulk_ok
        ok_g &= in_vol(g['interact'], g['inside']['x'], 1.0, g['inside']['z']) and in_vol(g['interact'], g['outside']['x'], 1.0, g['outside']['z']) and in_vol(SANCTUARY, g['inside']['x'], 0.0, g['inside']['z']) and not in_vol(SANCTUARY, g['outside']['x'], 0.0, g['outside']['z'])
        ok_g &= all(in_vol(g['sensor'], x_, 1.0, z_) for x_, z_ in corners(vol_block('', g['interact'])))
        out.append(f"hub gate {g['side']} ({g['kind']}, {g['motion']}): opening {cl['sx']:g} m between its fence sections (ends {', '.join(f'{e:.2f}' for e in ends)} m from the centre), closure on the floor {cl['sy']:g} m tall, open clearance {clear_open:.2f} m, Hulk passes: {hulk_ok}; thresholds in the interaction volume, inside in the sanctuary, outside not: {ok_g}"); ok &= ok_g
    pbs = pilot_blocks(); fence_cols = all(any(p_['id'] == f['block'] + '-top' for p_ in pbs) for f in H['fence']['segments'])
    out.append(f"fence: {len(H['fence']['segments'])} sections, {FENCE_H:g} m tall under pilot exclusion columns: {fence_cols}"); ok &= fence_cols
    # Routes: the instance door is reached only through a gate (closed: no; this gate alone open: yes), at least ROUTE_MIN m from it.
    for r in H['routes'] + H['reserved']:
        target = r['approach']; label = r.get('to', r.get('building'))
        only = Collider(colliders + [gate_block] + [vol_block('', g['closure']) for g in gates if g['id'] != r['gate']], bounds=dict(BOUNDS)); only.build(keep=lambda y: abs(y) < 0.05)
        rch = only.flood([only.node_at(SLOTS[0]['x'], 0, SLOTS[0]['z'])]); tn = only.node_at(target['x'], 0, target['z']); cn = closed.node_at(target['x'], 0, target['z'])
        ok_r = tn in rch and cn not in reach_c and r['path'] is not None and r['path'] >= ROUTE_MIN
        out.append(f"route to {label}: through {r['gate'].split(':')[-1]} only (all gates shut: unreached; that gate open: reached): {tn in rch and cn not in reach_c}; {r['path']:.1f} m walked from the gate's threshold ({r['straight']:.1f} m straight; target {ROUTE_MIN:g} m), {r['run']:.1f} s running, {r['sprint']:.1f} s sprinting"); ok &= ok_r
    # Zones: outside the sanctuary and clear of the gates; every spawn but the final plaza's in exactly one; rings ordered by distance.
    sv = vol_block('', SANCTUARY); bad_z = []
    for zn in H['zones']:
        zb = vol_block('', zn['volume']); zx0, zx1, zz0, zz1 = footprint(zb)
        if zx1 > -SANCT and zx0 < SANCT and zz1 > -SANCT and zz0 < SANCT: bad_z.append(zn['id'] + ' overlaps the sanctuary')
        for g in gates:
            dg = math.hypot(max(zx0 - g['x'], 0, g['x'] - zx1), max(zz0 - g['z'], 0, g['z'] - zz1))
            if dg < ZONE_APRON: bad_z.append(f"{zn['id']} {dg:.1f} m from {g['side']} gate")
    near_exit = min((math.hypot(e['exit']['x'] - g['x'], e['exit']['z'] - g['z']), e['id'], g['side']) for e in entries for g in gates)
    counted = sorted(e for zn in H['zones'] for e in zn['spawns']); want = sorted(e['e'] for e in entries if not in_vol(BOSS_TRIGGER, e['exit']['x'], 0, e['exit']['z']))
    rings = {k: [zn['gateDistance'] for zn in H['zones'] if zn['ring'] == k] for k in RINGS}
    lo = {k: min(d[0] for d in v) for k, v in rings.items()}; hi = {k: max(d[1] for d in v) for k, v in rings.items()}
    ordered = lo['near'] < lo['mid'] < lo['far'] and hi['near'] < hi['mid'] < hi['far'] and lo['far'] > hi['mid']
    out.append(f"hub zones: {len(H['zones'])} ({', '.join(f'{k} {len(v)}' for k, v in rings.items())}), outside the sanctuary and {ZONE_APRON:g} m or more from every gate: {not bad_z}" + ''.join('\n   BAD ' + b for b in bad_z)); ok &= not bad_z
    out.append(f"spawn exits {GATE_APRON:g} m or more from every gate's threshold: nearest {near_exit[0]:.1f} m ({near_exit[1].split(':')[-1]}, {near_exit[2]} gate)"); ok &= near_exit[0] >= GATE_APRON
    out.append(f"every spawn outside the final plaza in exactly one zone: {counted == want} ({len(counted)} of {len(entries)}; the final plaza's {len(entries) - len(want)} are the final encounter's)"); ok &= counted == want
    out.append('zone rings by walking distance from the gates: ' + ', '.join(f"{k} {lo[k]:.0f}-{hi[k]:.0f} m" for k in RINGS) + f': ordered {ordered}'); ok &= ordered
    wv = all(vol_in_bounds(w_['volume'], game_map()['bounds']) for w_ in H['wilds']) and all(vol_in_bounds(zn['volume'], game_map()['bounds']) for zn in H['zones'])
    out.append(f"wilds {[w_['id'].split(':')[-1] for w_ in H['wilds']]} and zones inside the bounds: {wv}"); ok &= wv
    # The layout loader's own gate (NativeExpeditionLayout.ValidateGeometry), mirrored: every slot and return clear for a
    # Hulk too; every anchor, interaction and approach clear for the pilot probe; from the first slot, a Hulk-sized route
    # to every slot, return, boss point and approach; every spawn's exit routed back to that slot with its own capsule;
    # every volume inside the bounds.
    hulk = Collider(colliders, bounds=shut.bounds); hulk.build(keep=lambda y: abs(y) < 0.05, radius=HULK_RADIUS, height=HULK_HEIGHT)
    hs = min((col.clearance(p['x'], p['z'], 0.35, HULK_HEIGHT + 0.05), n) for n, p in [(f'slot{i}', p) for i, p in enumerate(SLOTS)] + [(f'return{i}', p) for i, p in enumerate(RETURNS)])
    out.append('slots and returns clear for a Hulk (%.3f m radius, %.2f m tall): tightest %.2f m (%s)' % (HULK_SLOT, HULK_HEIGHT, hs[0], hs[1])); ok &= hs[0] >= HULK_SLOT
    pr = min((col.clearance(p['x'], p['z'], 0.35, HEIGHT + 0.05), n) for n, p in [(x['id'], x) for x in a['anchors']] + [(x['anchor'], x) for x in a['interactions']] + [('approach', d['approach'])])
    out.append('anchors (doors too), interactions and the approach clear for the pilot probe (%.2f m, %.1f m): tightest %.2f m (%s)' % (RADIUS, HEIGHT, pr[0], pr[1])); ok &= pr[0] >= RADIUS
    o_p = open_.node_at(SLOTS[0]['x'], 0, SLOTS[0]['z']); o_h = hulk.node_at(SLOTS[0]['x'], 0, SLOTS[0]['z'])
    rp = open_.flood([o_p]); rh = hulk.flood([o_h])
    need = SLOTS + RETURNS + BOSS_POINTS + [d['approach']]
    miss = [p for p in need if hulk.node_at(p['x'], 0, p['z']) not in rh or open_.node_at(p['x'], 0, p['z']) not in rp]
    out.append(f'Hulk-sized and pilot routes from slot 0 to every slot, return, boss point and approach: {len(need) - len(miss)} of {len(need)}; '
               f'street floor a Hulk reaches from slot 0: {100 * len(rh) / max(1, len(hulk.graph)):.1f}% of {len(hulk.graph)} Hulk-standable cells'); ok &= not miss
    miss = [e['id'] for e in entries if ('hulk' in e['allow'] and hulk.node_at(e['exit']['x'], 0, e['exit']['z']) not in rh) or open_.node_at(e['exit']['x'], 0, e['exit']['z']) not in rp]
    out.append(f'spawn exits routed to slot 0 with their capsule (Hulk where allowed): {len(entries) - len(miss)} of {len(entries)}' + ''.join('\n   BAD ' + i for i in miss)); ok &= not miss
    vols = [SANCTUARY, GATE, BOSS_TRIGGER, d['interact'], d['closure']]
    vb = all(vol_in_bounds(V, game_map()['bounds']) for V in vols) and all(in_vol(SANCTUARY, p['x'], p['y'], p['z']) for p in SLOTS)
    out.append(f'volumes inside the bounds (1 cm margin) and every sanctuary slot inside the sanctuary volume: {vb}'); ok &= vb
    bnd = all(BOUNDS['minX'] + 1.5 <= p['x'] <= BOUNDS['maxX'] - 1.5 and BOUNDS['minZ'] + 1.5 <= p['z'] <= BOUNDS['maxZ'] - 1.5 for p in [e['exit'] for e in entries] + SLOTS + RETURNS + BOSS_POINTS + CACHES + [MACHINE])
    out.append(f'every record inside the bounds less 1.5 m: {bnd}'); ok &= bnd
    # Ids and blocks.
    ids = [b['id'] for b in blocks]; mats = sorted({b['material'] for b in blocks})
    rec = [x['id'] for x in a['anchors']] + [e['id'] for e in entries] + [g['id'] for g in a['gates']]
    out.append(f'record ids: {len(rec)}, unique {len(set(rec)) == len(rec)}, pass the Expedition id rule {all(id_ok(i) for i in rec)}'); ok &= len(set(rec)) == len(rec) and all(id_ok(i) for i in rec)
    roles = {}
    for b in blocks: roles[b['role']] = roles.get(b['role'], 0) + 1
    out.append(f'blocks {len(blocks)} ({", ".join(f"{k} {n}" for k, n in sorted(roles.items()))}), unique ids {len(set(ids)) == len(ids)}; materials {mats}; rotated {sum(1 for b in blocks if b["rotation"] != q())}; '
               f'buildings {len(BUILDINGS)} (+3 skyline), cover {sum(1 for b in blocks if b["_ch"] in "c=uxod")}, pilot exclusion columns {len(pilot_blocks())}; initial prop frame (non-static) {sum(1 for b in blocks if b["role"] != "static")}')
    ok &= len(set(ids)) == len(ids) and set(mats) <= {'stone', 'brick', 'metal', 'wood', 'foundation'}
    inb = all(footprint(b)[0] >= EX0 - 1e-6 and footprint(b)[1] <= EX1 + 1e-6 and footprint(b)[2] >= EZ0 - 1e-6 and footprint(b)[3] <= EZ1 + 1e-6 and yspan(b)[1] <= CEIL for b in blocks)
    out.append(f'every block within x {EX0:g}..{EX1:g}, z {EZ0:g}..{EZ1:g} and under the {CEIL:g} m ceiling: {inb}'); ok &= inb
    out.append(f'blockHash {a["blockHash"]}')
    out.append('reserved doorways (sealed facades, not in the manifest yet): ' + '; '.join(f'{k} ({p["x"]:g}, {p["y"]:g}, {p["z"]:g}) yaw {p["yaw"]:g}' for k, p in sorted(RESERVED.items())))
    return ok, out, (shut, reach, open_, reach_o)

# =====================================================================================================================
# JOIN CIRCLE DRAFT CHECKS (exp-layout-4, not live): run by `expedition_layout.py --draft` on checks()'s graphs.
def in_circle(j, x, z): return (x - j['x']) ** 2 + (z - j['z']) ** 2 <= j['r'] ** 2 + 1e-9
def join_checks(graphs):
    """The draft's join circles against the town: Astra's parser rules mirrored (finite positive r and h, a hub entry door,
    inside the bounds, centre and 8 rim points on floor with the pilot probe), the whole circle on reached standable floor
    (sampled), no block in the cylinder, the centre routed for a pilot and a Hulk, clear of the door's volumes, the gates,
    the fence, the sanctuary, spawn exits, the machine and caches; the zones holding it; the height against a standing and a
    jumping capsule; its distance from the doorway and the east gate; the returns out of it; the F entry unchanged."""
    shut, reach, open_, reach_o = graphs
    out, ok = [], True
    live, a = area(), area(True); bounds = game_map()['bounds']; anc = {x['id']: x for x in a['anchors']}
    hulk = Collider(list(blocks), bounds=shut.bounds); hulk.build(keep=lambda y: abs(y) < 0.05, radius=HULK_RADIUS, height=HULK_HEIGHT)
    s0 = SLOTS[0]; rp = open_.flood([open_.node_at(s0['x'], 0, s0['z'])]); rh = hulk.flood([hulk.node_at(s0['x'], 0, s0['z'])])
    by, gcol = gate_paths(); east = f'{AREA}:hub-gate:east'; eg = next(g for g in HUB_GATES if g['id'] == east)
    floorish = lambda b: '-slab-' in b['id'] or b['id'].endswith('paving')
    for d, d3 in zip(a['doors'], live['doors']):
        j = d.get('join'); inst = anc[d['anchor']]['building']
        if not j: out.append(f"{d['anchor']}: no join circle  BAD"); ok = False; continue
        cx, cy, cz, r, h = j['x'], j['y'], j['z'], j['r'], j['h']; dx_, dz_ = forward(anc[d['anchor']]['yaw'])
        door_x, door_z = WZ_DOOR if inst == 'exp-ward-zero' else (anc[d['anchor']]['x'], anc[d['anchor']]['z'])
        off_axis = abs(-(cx - door_x) * dz_ + (cz - door_z) * dx_); out_m = (cx - door_x) * dx_ + (cz - door_z) * dz_
        out.append(f"join circle at the {inst} door (draft, not live): centre ({cx:g}, {cy:g}, {cz:g}), r {r:g} m, base y {cy:g}, top {cy + h:g} m; "
                   f"centre {out_m:.1f} m out from the doorway on the approach line (off it {off_axis:.2f} m), near edge {out_m - r:.1f} m from the doorway")
        ok &= off_axis < 1e-6 and out_m - r > 0
        # Astra's parser and geometry gate, mirrored (NativeExpeditionLayout.Join / ValidateGeometry).
        def probe(x, y, z):
            return (x - RADIUS >= bounds['minX'] and x + RADIUS <= bounds['maxX'] and z - RADIUS >= bounds['minZ'] and z + RADIUS <= bounds['maxZ']
                    and y > bounds['killY'] and y + HEIGHT <= bounds['ceiling'] and shut.floor_under(x, z, y) and not open_.blocked(x, z, y))
        rim = [(cx + math.cos(k * math.pi / 4) * r, cz + math.sin(k * math.pi / 4) * r) for k in range(8)]
        okp = all(math.isfinite(v_) for v_ in (cx, cy, cz, r, h)) and r > 0 and h > 0 and inst in ('exp-ward-zero',) and a['role'] == 'world'
        okp &= vol_in_bounds(vol(cx, cy, cz, 2 * r, h, 2 * r), bounds) and probe(cx, cy, cz) and all(probe(x, cy, z) for x, z in rim)
        out.append(f'parser rules (finite, r and h positive, at a hub entry door, inside the bounds, centre and 8 rim points on floor with the pilot probe): {okp}'); ok &= okp
        # The whole circle: a 0.25 m grid and 32 rim points on the street floor, reached from the sanctuary, standable.
        pts = [(cx + i * 0.25, cz + k * 0.25) for i in range(-int(r * 4), int(r * 4) + 1) for k in range(-int(r * 4), int(r * 4) + 1) if in_circle(j, cx + i * 0.25, cz + k * 0.25)]
        pts += [(cx + math.cos(k * math.pi / 16) * r * 0.999, cz + math.sin(k * math.pi / 16) * r * 0.999) for k in range(32)]
        fl = [p_ for p_ in pts if shut.floor_under(p_[0], p_[1], cy)]; rc = [p_ for p_ in fl if shut.node_at(p_[0], cy, p_[1], 0.36) in reach]
        st = [p_ for p_ in rc if not open_.blocked(p_[0], p_[1], cy)]
        out.append(f'floor in the circle: {len(pts)} samples (0.25 m grid and 32 rim points), on the street floor at y {cy:g}: {len(fl)}, reached from the sanctuary slots: {len(rc)}, standable for the pilot probe: {len(st)}')
        ok &= len(fl) == len(rc) == len(st) == len(pts)
        # Blocks: none in the cylinder; the nearest beside it and the nearest over it.
        band = [b for b in blocks if not floorish(b) and yspan(b)[0] < cy + h and yspan(b)[1] > cy + 0.01]
        inside = [b['id'] for b in band if dist_xz(b, cx, cz) < r]
        near = min((dist_xz(b, cx, cz) - r, b['id']) for b in band)
        over = min(((yspan(b)[0], b['id']) for b in blocks if dist_xz(b, cx, cz) < r and yspan(b)[0] >= cy + h - 1e-6), default=(99.0, '-'))
        pyl = min(dist_xz(b, cx, cz) - r for b in blocks if '-wz-portico-pylon-' in b['id'])
        out.append(f"blocks in the cylinder (y {cy:g}..{cy + h:g}): {len(inside)}{' BAD ' + ', '.join(inside) if inside else ''}; nearest beside it {near[0]:.2f} m beyond its edge ({near[1].replace(MAP + '-', '')}), "
                   f"the portico pylons {pyl:.2f} m; nearest over it: {over[1].replace(MAP + '-', '')} from {over[0]:g} m ({over[0] - cy - h:.1f} m over the top)")
        ok &= not inside and near[0] >= RADIUS
        # The centre is routed for a pilot and a Hulk (the approach is the centre); the walk from the east gate.
        cn_p, cn_h = open_.node_at(cx, cy, cz), hulk.node_at(cx, cy, cz)
        route = cn_p in rp and cn_h in rh and shut.node_at(cx, cy, cz) in reach
        dist = by[east]; cnode = gcol.node_at(cx, cy, cz, 0.8)
        cells = [n for n in dist if in_circle(j, *gcol.cell_xz(n[0], n[1]))]; edge = min(dist[n] for n in cells)
        out.append(f"centre navigable: pilot and Hulk routes from slot 0: {route}; walked from the east gate's threshold {dist[cnode]:.1f} m to the centre ({math.hypot(cx - eg['x'], cz - eg['z']):.1f} m straight), "
                   f"{edge:.1f} m to the first step inside the circle; exp-layout-3's approach (the doorway's front) {gate_distance(d3['approach']['x'], d3['approach']['z'], east)[0]:.1f} m (target {ROUTE_MIN:g} m)")
        ok &= route
        # The door's own volumes: the F interaction volume, the closure and the airlock unit drawn round it (Tests/ExpeditionDressingChecks).
        cl = d['closure']; unit = vol(cl['x'], 0, cl['z'], cl['sx'] + 3.8, 4.1, 1.5, cl['yaw'])
        dv = [(vol_dist_xz(d['interact'], cx, cz) - r, 'interaction volume (F)'), (vol_dist_xz(cl, cx, cz) - r, 'closure'), (vol_dist_xz(unit, cx, cz) - r, 'airlock unit')]
        out.append('clear of the door: ' + ', '.join(f'{n} {g_:.2f} m' for g_, n in dv) + f' beyond its edge: {all(g_ > 0 for g_, n in dv)}'); ok &= all(g_ > 0 for g_, n in dv)
        # The gates, the fence and the sanctuary.
        gv = [(vol_dist_xz(g[k], cx, cz) - r, g['side'] + ' gate ' + k) for g in HUB_GATES for k in ('closure', 'interact', 'sensor')] + [(vol_dist_xz(GATE, cx, cz) - r, 'final gate')]
        fv = min(dist_xz(b, cx, cz) - r for b in blocks if '-sanctuary-fence-' in b['id']); sv = vol_dist_xz(SANCTUARY, cx, cz) - r
        out.append(f"clear of the gates, the fence and the sanctuary: nearest gate volume {min(gv)[0]:.1f} m ({min(gv)[1]}), final gate {gv[-1][0]:.1f} m, fence {fv:.1f} m, sanctuary volume {sv:.1f} m beyond its edge: {min(gv)[0] > 0 and fv > 0 and sv > 0}")
        ok &= min(gv)[0] > 0 and fv > 0 and sv > 0
        # Hub zones and wilds holding it; spawn exits (with a drop's clear radius) and the machine and caches.
        H_ = hub(); zs = [z_['id'].split(':')[-1] for z_ in H_['zones'] if vol_dist_xz(z_['volume'], cx, cz) < r]
        whole = [z_['id'].split(':')[-1] for z_ in H_['zones'] if all(in_vol(z_['volume'], x, 1.0, z) for x, z in rim)]
        ws = [w_['id'].split(':')[-1] for w_ in H_['wilds'] if vol_dist_xz(w_['volume'], cx, cz) < r]
        dz3 = [z_['id'].split(':')[-1] for z_ in H_['zones'] if in_vol(z_['volume'], d3['approach']['x'], 1.0, d3['approach']['z'])]
        sp = min((math.hypot(e['exit']['x'] - cx, e['exit']['z'] - cz) - r, e['id'].split(':')[-1]) for e in entries)
        dp = min((math.hypot(e['exit']['x'] - cx, e['exit']['z'] - cz) - r - e['clear'], e['id'].split(':')[-1]) for e in entries if e['kind'] == 'drop')
        out.append(f"hub zones over it: {zs} (wholly inside {whole}; exp-layout-3's door approach is in {dz3}), wilds {ws}; nearest spawn exit {sp[0]:.1f} m beyond its edge ({sp[1]}; {JOIN_SPAWN_CLEAR:g} m or more), "
                   f"nearest drop landing disc {dp[0]:.1f} m ({dp[1]}): {sp[0] >= JOIN_SPAWN_CLEAR and dp[0] > 0}")
        ok &= sp[0] >= JOIN_SPAWN_CLEAR and dp[0] > 0 and zs == whole
        mc = min((math.hypot(x['x'] - cx, x['z'] - cz) - r - 1.0, x['id'].split(':', 1)[-1]) for x in a['anchors'] if x['kind'] != 'door')
        mi = min((math.hypot(x['x'] - cx, x['z'] - cz) - r - x['range'], x['anchor'].split(':', 1)[-1]) for x in a['interactions'])
        out.append(f'machine and caches: nearest footprint (1 m) {mc[0]:.1f} m beyond its edge ({mc[1]}), nearest interaction reach {mi[0]:.1f} m ({mi[1]}): {mc[0] > 0 and mi[0] > 0}'); ok &= mc[0] > 0 and mi[0] > 0
        # Height: the base on the floor; a standing capsule, a ground jump and a ground plus air jump (jumpPower 1) inside.
        g1 = JUMP ** 2 / (2 * GRAVITY); g2 = (JUMP * AIR_JUMP) ** 2 / (2 * GRAVITY)
        hs = [('standing capsule centre', CAPSULE_HALF), ('its top', 2 * CAPSULE_HALF), ('ground jump apex centre', CAPSULE_HALF + g1), ('ground + air jump apex centre', CAPSULE_HALF + g1 + g2)]
        okh = shut.floor_under(cx, cz, cy) and all(cy <= cy + v_ <= cy + h for _, v_ in hs) and over[0] >= cy + h
        out.append('height: base on the floor (y %g), ' % cy + ', '.join(f'{n} {v_:.2f} m' for n, v_ in hs) + f' inside 0..{h:g} m, top under the nearest block over it: {okh}'); ok &= okh
        # Returns: out of the circle, off the F volume, on reached floor, Hulk-clear, spaced, away from spawns.
        R_ = d['returns']; gap = min(math.hypot(p_['x'] - cx, p_['z'] - cz) - r for p_ in R_)
        old = [p_ for p_ in d3['returns'] if in_circle(j, p_['x'], p_['z'])]; oldgap = min(math.hypot(p_['x'] - cx, p_['z'] - cz) - r for p_ in d3['returns'] if not in_circle(j, p_['x'], p_['z']))
        onf = all(shut.node_at(p_['x'], 0, p_['z'], 0.6) in reach and shut.floor_under(p_['x'], p_['z'], p_['y']) for p_ in R_)
        hc = min(open_.clearance(p_['x'], p_['z'], 0.35, HULK_HEIGHT + 0.05) for p_ in R_); pc = min(open_.clearance(p_['x'], p_['z'], 0.35, HEIGHT + 0.05) for p_ in R_)
        pair = min(math.hypot(p_['x'] - q_['x'], p_['z'] - q_['z']) for i, p_ in enumerate(R_) for q_ in R_[i + 1:])
        rs = min(math.hypot(e['exit']['x'] - p_['x'], e['exit']['z'] - p_['z']) for e in entries for p_ in R_)
        routed = all(open_.node_at(p_['x'], 0, p_['z']) in rp and hulk.node_at(p_['x'], 0, p_['z']) in rh for p_ in R_)
        fvol = any(in_vol(d['interact'], p_['x'], 1.0, p_['z']) for p_ in R_)
        okr = gap >= RADIUS + 0.5 and onf and hc >= HULK_SLOT and pc >= PILOT_CLEAR and pair >= 1.4 - 1e-6 and rs >= JOIN_SPAWN_CLEAR and routed and not fvol and len(R_) == 8
        out.append(f'returns (draft): {len(R_)}, none in the circle: the closest stands {gap:.2f} m beyond its edge (its capsule {gap - CAPSULE_R:.2f} m); off the F volume {not fvol}; on reached floor {onf}; '
                   f'pilot and Hulk routes from slot 0 {routed}; clear {pc:.2f} m (pilot), {hc:.2f} m (Hulk); closest pair {pair:.2f} m; nearest spawn exit {rs:.1f} m: {okr}'); ok &= okr
        oz = ', '.join(sorted({'%g' % p_['z'] for p_ in old}, key=float))
        out.append(f'returns (exp-layout-3): {len(old)} of 8 would stand in the circle (z {oz}), the other {8 - len(old)} only {oldgap:.2f} m beyond its edge')
        # Their walk to the east gate never needs the circle.
        free = walk(gcol, gcol.node_at(eg['x'], 0, eg['z']), forbid=lambda n: in_circle(j, *gcol.cell_xz(n[0], n[1])))
        extra = [free.get(gcol.node_at(p_['x'], 0, p_['z']), 1e18) - dist[gcol.node_at(p_['x'], 0, p_['z'])] for p_ in R_]
        out.append(f"returns' walk to the east gate's threshold avoiding the circle is their shortest walk (extra metres {max(extra):.2f}): {max(extra) < 1e-6}"); ok &= max(extra) < 1e-6
        # The approach and the F entry (live until the subset transfer): approach at the centre, volumes and line of sight.
        a_ = d['approach']; same = d['interact'] == d3['interact'] and d['closure'] == d3['closure'] and d['anchor'] == d3['anchor']
        iv = d['interact']; yaw_ = math.radians(iv['yaw']); c_, s_ = math.cos(yaw_), math.sin(yaw_); los = []
        for u in (-0.4, 0.0, 0.4):
            for w in (-0.4, 0.0, 0.4):
                lx, lz = u * iv['sx'], w * iv['sz']; px, pz = iv['x'] + lx * c_ + lz * s_, iv['z'] - lx * s_ + lz * c_
                seg = [(px + (a_['x'] - px) * t / 50, 0.6, pz + (a_['z'] - pz) * t / 50) for t in range(51)]
                los.append(not any(dist_xz(b, x, z) <= 1e-9 and yspan(b)[0] < y < yspan(b)[1] for x, y, z in seg for b in shut.near(x, z)))
        okf = (a_['x'], a_['y'], a_['z']) == (cx, cy, cz) and same and all(los) and in_vol(iv, iv['x'], 1.0, iv['z'])
        out.append(f"approach at the circle's centre ({a_['x']:g}, {a_['y']:g}, {a_['z']:g}; exp-layout-3 ({d3['approach']['x']:g}, {d3['approach']['y']:g}, {d3['approach']['z']:g})); "
                   f"F entry intact: anchor, interaction volume and closure as exp-layout-3 {same}, line of sight from the interaction volume (9 points, 0.6 m up) to the approach {all(los)}: {okf}"); ok &= okf
    out.append('join draft checks: ' + ('pass' if ok else 'FAIL'))
    return ok, out

# =====================================================================================================================
# PLAN (top-down PNG): buildings shaded by height, walkable floor, the sanctuary and its slots, the gate, the boss
# trigger and points, the door with its volumes and returns, the machine and caches, the spawns by kind.
def plan(path, graphs, scale=6):
    shut, reach, open_, reach_o = graphs
    M = 40; W, H = int((EX1 - EX0) * scale) + 2 * M + 330, int((EZ1 - EZ0) * scale) + 2 * M + 30
    cv = Canvas(W, H)
    def X(x): return int(round((x - EX0) * scale)) + M
    def Y(z): return int(round((EZ1 - z) * scale)) + M + 20
    cv.rect(X(EX0), Y(EZ1), X(EX1), Y(EZ0), (24, 22, 28))
    H_ = area()['hub']
    for w_ in H_['wilds']:
        V = w_['volume']; cv.rect(X(V['x'] - V['sx'] / 2), Y(V['z'] + V['sz'] / 2), X(V['x'] + V['sx'] / 2), Y(V['z'] - V['sz'] / 2), (30, 26, 40))
    for n in open_.graph:
        x, z = open_.cell_xz(n[0], n[1]); c = (64, 60, 66) if n in reach else (88, 58, 70)
        cv.rect(X(x - 0.25), Y(z + 0.25), X(x + 0.25), Y(z - 0.25), c)
    cv.rect(X(-SANCT), Y(SANCT), X(SANCT), Y(-SANCT), (52, 92, 70), 0.55)
    for b in sorted(blocks, key=lambda b: yspan(b)[1]):
        y0, y1 = yspan(b)
        if y1 <= 0.01 or b['id'].endswith('paving') or '-slab-' in b['id']: continue
        if b['_ch'] in '#G^':
            t = max(0.0, min(1.0, y1 / 24)); c = (int(70 + 120 * t), int(64 + 100 * t), int(74 + 96 * t))
            if b['_ch'] == 'G': c = (176, 120, 96)
            if b['_ch'] == '^': c = (110, 104, 110)
        elif b['_ch'] == 'B': c = (230, 200, 60)
        elif b['_ch'] in 'PS': c = (150, 170, 190)
        elif b['_ch'] == 'F': c = (196, 140, 80) if '-wood-' in b['id'] else (150, 156, 164)
        elif b['_ch'] == 'w': c = (120, 104, 96)
        else: c = (160, 112, 72)
        cv.poly([(X(x), Y(z)) for x, z in corners(b)], c)
    for b in blocks:
        if b['_ch'] == '#' and b['id'].startswith(MAP + '-bldg-'):
            x0, x1, z0, z1 = footprint(b); cv.frame(X(x0), Y(z1), X(x1), Y(z0), (40, 36, 44))
    # the gate, the boss trigger and points
    gx0, gz0 = GATE['x'] - GATE['sx'] / 2, GATE['z'] - GATE['sz'] / 2
    cv.rect(X(gx0), Y(gz0 + GATE['sz']) - 2, X(gx0 + GATE['sx']), Y(gz0) + 2, (255, 60, 50))
    t = BOSS_TRIGGER; cv.frame(X(t['x'] - t['sx'] / 2), Y(t['z'] + t['sz'] / 2), X(t['x'] + t['sx'] / 2), Y(t['z'] - t['sz'] / 2), (230, 80, 230), 2)
    for p in BOSS_POINTS:
        cx, cy = X(p['x']), Y(p['z']); cv.line(cx - 7, cy - 7, cx + 7, cy + 7, (255, 90, 255), 3); cv.line(cx - 7, cy + 7, cx + 7, cy - 7, (255, 90, 255), 3)
    s = SANCTUARY; cv.frame(X(s['x'] - s['sx'] / 2), Y(s['z'] + s['sz'] / 2), X(s['x'] + s['sx'] / 2), Y(s['z'] - s['sz'] / 2), (90, 255, 140), 2)
    for i, p in enumerate(SLOTS):
        cx, cy = X(p['x']), Y(p['z']); fx, fz = forward(p['yaw']); cv.disc(cx, cy, 5, (255, 255, 255)); cv.line(cx, cy, cx + int(fx * 11), cy - int(fz * 11), (255, 255, 255), 2); cv.text(cx + 6, cy - 14, str(i), (255, 255, 255))
    a = area(); d = a['doors'][0]
    for key, col_ in (('interact', (80, 220, 255)), ('closure', (255, 255, 120))):
        cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', d[key]))], col_, 0.6)
    for p in RETURNS:
        cx, cy = X(p['x']), Y(p['z']); fx, fz = forward(p['yaw']); cv.disc(cx, cy, 4, (80, 220, 255)); cv.line(cx, cy, cx + int(fx * 9), cy - int(fz * 9), (80, 220, 255), 2)
    ap = d['approach']; cv.ring(X(ap['x']), Y(ap['z']), 5, (80, 220, 255), 2)
    cv.disc(X(WZ_DOOR[0]), Y(WZ_DOOR[1]), 6, (0, 160, 255))
    for k, p in RESERVED.items():
        cv.ring(X(p['x']), Y(p['z']), 8, (170, 170, 170), 2); cv.text(X(p['x']) + (12 if p['x'] < 0 else -70), Y(p['z']) + 12, k.split('-')[1].upper() + ' (RESERVED)', (190, 190, 190))
    cv.rect(X(MACHINE['x']) - 6, Y(MACHINE['z']) - 2, X(MACHINE['x']) + 6, Y(MACHINE['z']) + 2, (120, 255, 160)); cv.rect(X(MACHINE['x']) - 2, Y(MACHINE['z']) - 6, X(MACHINE['x']) + 2, Y(MACHINE['z']) + 6, (120, 255, 160))
    for i, c in enumerate(CACHES):
        cv.rect(X(c['x']) - 5, Y(c['z']) - 5, X(c['x']) + 5, Y(c['z']) + 5, (255, 200, 40)); cv.text(X(c['x']) + 7, Y(c['z']) - 4, f'C{i}', (255, 200, 40))
    KC = {'door': (255, 150, 60), 'vent': (190, 110, 255), 'drop': (255, 70, 170), 'breach': (255, 235, 60)}
    for e in entries:
        x, z = e['exit']['x'], e['exit']['z']; c = KC[e['kind']]; r = 6 if 'hulk' in e['allow'] else 4
        if e['kind'] == 'drop':
            cv.ring(X(x), Y(z), int(e['clear'] * scale), c, 1)
        fx, fz = e['from']['x'], e['from']['z']
        if e['kind'] in ('door', 'vent', 'breach'): cv.line(X(fx), Y(fz), X(x), Y(z), c, 2)
        cv.disc(X(x), Y(z), r, c)
    # the hub: zones by ring, the gates (closure, threshold points), the routes along their `via` with their walked length
    RC = {'near': (120, 220, 140), 'mid': (240, 200, 90), 'far': (255, 110, 90)}
    for zn in H_['zones']:
        V = zn['volume']; cv.frame(X(V['x'] - V['sx'] / 2) + 2, Y(V['z'] + V['sz'] / 2) + 2, X(V['x'] + V['sx'] / 2) - 2, Y(V['z'] - V['sz'] / 2) - 2, RC[zn['ring']], 1)
        cv.text(X(V['x'] - V['sx'] / 2) + 5, Y(V['z'] + V['sz'] / 2) + 5, zn['id'].split(':')[-1].upper(), RC[zn['ring']])
    for g in H_['gates']:
        cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', g['closure']))], (90, 255, 220) if g['kind'] == 'metal' else (255, 170, 60))
        for k_, c_ in (('inside', (90, 255, 140)), ('outside', (255, 120, 90))): cv.disc(X(g[k_]['x']), Y(g[k_]['z']), 3, c_)
    for r in H_['routes'] + H_['reserved']:
        g = next(x for x in H_['gates'] if x['id'] == r['gate']); ap = r['approach']; line = [(g['x'], g['z'])] + [(p_['x'], p_['z']) for p_ in r.get('via', [])] + [(ap['x'], ap['z'])]
        for (ax, az), (bx, bz) in zip(line, line[1:]):
            n_ = max(1, int(math.hypot(bx - ax, bz - az) / 1.0))
            for k_ in range(0, n_, 2): t0, t1 = k_ / n_, min(1.0, (k_ + 1) / n_); cv.line(X(ax + (bx - ax) * t0), Y(az + (bz - az) * t0), X(ax + (bx - ax) * t1), Y(az + (bz - az) * t1), (255, 255, 255), 2)
        mx, mz = line[len(line) // 2]; cv.text(X(mx) - 40, Y(mz) + 8, '%.0f M  %.1f S' % (r['path'], r['run']), (255, 255, 255))
    # legend
    lx = X(EX1) + 20; ly = 60
    cv.text(M, 10, 'EXPEDITION CITY HUB (EXP-LAYOUT-7)  %g X %g M  NORTH UP  1 PX = %d CM' % (BOUNDS['maxX'] - BOUNDS['minX'], BOUNDS['maxZ'] - BOUNDS['minZ'], 100 // scale), (232, 251, 255))
    for label, c in [('SANCTUARY VOLUME', (90, 255, 140)), ('SLOT  (FACING)', (255, 255, 255)), ('WARD ZERO DOOR', (0, 160, 255)), ('INTERACT VOLUME', (80, 220, 255)),
                     ('CLOSURE', (255, 255, 120)), ('RETURNS, APPROACH', (80, 220, 255)), ('FINAL GATE', (255, 60, 50)), ('BOSS TRIGGER', (230, 80, 230)),
                     ('BOSS POINT', (255, 90, 255)), ('MACHINE', (120, 255, 160)), ('CACHE', (255, 200, 40)), ('DOOR SPAWN', KC['door']), ('VENT SPAWN', KC['vent']),
                     ('DROP (RING: CLEAR)', KC['drop']), ('BREACH (HULK)', KC['breach']), ('BIG DOT: HULK', (255, 255, 255)), ('BREACH PANEL', (230, 200, 60)),
                     ('COVER', (160, 112, 72)), ('STREET (REACHED, GATE SHUT)', (64, 60, 66)), ('FLOOR BEHIND THE GATE', (88, 58, 70)), ('BUILDING (LIGHTER = TALLER)', (160, 140, 150)),
                     ('FENCE: WOOD PALISADE', (196, 140, 80)), ('FENCE: SCRAP METAL', (150, 156, 164)), ('HUB GATE (WOOD, SWING)', (255, 170, 60)), ('HUB GATE (METAL, SLIDE)', (90, 255, 220)),
                     ('WILDS COVER', (120, 104, 96)), ('WILDS DISTRICT', (30, 26, 40)), ('ZONE: NEAR', RC['near']), ('ZONE: MID', RC['mid']), ('ZONE: FAR', RC['far']), ('ROUTE: WALKED M, RUN S', (255, 255, 255))]:
        cv.rect(lx, ly, lx + 14, ly + 10, c); cv.text(lx + 20, ly, label, (210, 214, 220)); ly += 22
    for label, x, z in (('SANCTUARY', -6.5, -9.0), ('FINAL PLAZA', -8.5, 55.5), ('FINAL GATE', -6.5, 34.2), ('AVENUE', -3.0, 18.0), ('RING ROAD', 28.6, -12.0),
                        ('MERIDIAN', grow_x(85.0), -36.0), ('(WARD ZERO)', grow_x(85.0), -38.5), ('FORECOURT', -10.0, 31.6), ('MERIDIAN APPROACH', grow_x(50.0), -33.0), ('IRONWORKS YARD', grow_x(-80.0), -33.0),
                        ('STATION ROAD', -48.0, grow_z(-86.0)), ('EXCHANGE', CUT + 5.5, -1.0), ('UNION HALL', -CUT - 18.5, -1.0), ('TRAM DEPOT', -9.0, -CUT - 9.5)):
        cv.text(X(x), Y(z), label, (240, 236, 220))
    cv.save(path)

def join_plan(path, graphs, x0=66.0, x1=86.0, z0=-11.0, z1=11.0, scale=40):
    """Top-down PNG of an instance door with the exp-layout-4 draft's join circle (not live): floor, blocks (the portico
    canopy as an overlay), the door's F volume, closure and anchor, the approach line, exp-layout-3's approach and returns
    (hollow) against the draft's (filled), spawn exits with the 8 m apron. Offline only (not committed)."""
    shut, reach, open_, reach_o = graphs
    M, LW = 30, 380; W, H = int((x1 - x0) * scale) + 2 * M + LW, int((z1 - z0) * scale) + 2 * M + 40
    cv = Canvas(W, H)
    def X(x): return int(round((x - x0) * scale)) + M
    def Y(z): return int(round((z1 - z) * scale)) + M + 30
    live, dr = area()['doors'][0], area(True)['doors'][0]; j = dr['join']
    cv.rect(X(x0), Y(z1), X(x1), Y(z0), (24, 22, 28))
    for n in open_.graph:
        x, z = open_.cell_xz(n[0], n[1])
        if x0 - 1 < x < x1 + 1 and z0 - 1 < z < z1 + 1: cv.rect(X(x - 0.25), Y(z + 0.25), X(x + 0.25), Y(z - 0.25), (58, 56, 62) if n in reach else (88, 58, 70))
    for i in range(int(x0), int(x1) + 1):
        cv.rect(X(i), Y(z1), X(i) + 1, Y(z0), (44, 42, 50))
    for k in range(int(z0), int(z1) + 1):
        cv.rect(X(x0), Y(k), X(x1), Y(k) + 1, (44, 42, 50))
    for e in entries:
        ex, ez = e['exit']['x'], e['exit']['z']
        if x0 - 9 < ex < x1 + 9 and z0 - 9 < ez < z1 + 9:
            cv.ring(X(ex), Y(ez), int(JOIN_SPAWN_CLEAR * scale), (150, 90, 200), 1); cv.disc(X(ex), Y(ez), 7, (190, 110, 255))
            cv.text(X(ex) + 10, Y(ez) - 6, e['id'].split(':')[-1].upper(), (200, 150, 255))
    over = []
    for b in sorted(blocks, key=lambda b: yspan(b)[1]):
        if yspan(b)[1] <= 0.01 or b['id'].endswith('paving') or '-slab-' in b['id']: continue
        if yspan(b)[0] > 3.0: over.append(b); continue
        c = (150, 170, 190) if b['_ch'] == 'P' else (120, 104, 96) if b['_ch'] == 'w' else (110, 112, 120) if b['_ch'] == '#' else (160, 112, 72)
        cv.poly([(X(x), Y(z)) for x, z in corners(b)], c)
    for b in over:
        cv.poly([(X(x), Y(z)) for x, z in corners(b)], (200, 200, 210), 0.18); x_0, x_1, z_0, z_1 = footprint(b); cv.frame(X(x_0), Y(z_1), X(x_1), Y(z_0), (170, 170, 180), 1)
    # the circle: fill, rim, centre
    cx, cz, r = j['x'], j['z'], j['r']
    for py in range(Y(cz + r) - 1, Y(cz - r) + 2):
        for px in range(X(cx - r) - 1, X(cx + r) + 2):
            if ((px - X(cx)) ** 2 + (py - Y(cz)) ** 2) <= (r * scale) ** 2: cv.px(px, py, (40, 230, 190), 0.22)
    cv.ring(X(cx), Y(cz), int(r * scale), (60, 255, 210), 3)
    # the door: F volume, closure, anchor, doorway
    cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', live['interact']))], (80, 220, 255), 0.35); cv.text(X(live['interact']['x']) - 4, Y(0) + 14, 'F', (80, 220, 255), 3)
    cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', live['closure']))], (255, 255, 120))
    an = next(x for x in area()['anchors'] if x['id'] == live['anchor']); cv.disc(X(an['x']), Y(an['z']), 5, (0, 160, 255))
    # the approach line (the door's axis), exp-layout-3's approach (hollow) and the draft's (the centre, filled)
    for k in range(int(x0), int(WZ_DOOR[0]), 1): cv.line(X(k), Y(WZ_DOOR[1]), X(k + 0.5), Y(WZ_DOOR[1]), (255, 255, 255), 2)
    a3 = live['approach']; cv.ring(X(a3['x']), Y(a3['z']), 9, (255, 150, 60), 3); cv.disc(X(cx), Y(cz), 8, (255, 150, 60))
    for p in live['returns']:
        cv.ring(X(p['x']), Y(p['z']), int(CAPSULE_R * scale), (255, 90, 90), 2); cv.line(X(p['x']) - 6, Y(p['z']) - 6, X(p['x']) + 6, Y(p['z']) + 6, (255, 90, 90), 2); cv.line(X(p['x']) - 6, Y(p['z']) + 6, X(p['x']) + 6, Y(p['z']) - 6, (255, 90, 90), 2)
    for i, p in enumerate(dr['returns']):
        fx, fz = forward(p['yaw']); cv.disc(X(p['x']), Y(p['z']), int(CAPSULE_R * scale), (120, 255, 120)); cv.line(X(p['x']), Y(p['z']), X(p['x'] + fx * 0.8), Y(p['z'] + fz * 0.8), (20, 60, 20), 3)
        cv.text(X(p['x']) - 4, Y(p['z']) - 5, str(i), (10, 40, 10), 2)
    # the margins over anything drawn past the crop, then labels, scale bar, legend
    bg = (14, 16, 20); cv.rect(0, 0, W, Y(z1), bg); cv.rect(0, Y(z0), W, H, bg); cv.rect(0, 0, X(x0), H, bg); cv.rect(X(x1), 0, W, H, bg)
    cv.text(M, 8, 'WARD ZERO DOOR: JOIN CIRCLE DRAFT (EXP-LAYOUT-4, NOT LIVE)  NORTH UP  1 M = %d PX' % scale, (232, 251, 255))
    lab = 'JOIN CIRCLE R %g M, H %g M' % (r, j['h']); tx, ty = X(cx - r - 0.6) - text_width(lab), Y(cz + r + 1.6)
    cv.text(tx, ty, lab, (60, 255, 210)); cv.line(tx + text_width(lab), ty + 5, X(cx - r * 0.7071), Y(cz + r * 0.7071), (60, 255, 210), 1)
    cv.text(X(WZ_DOOR[0]) - 80, Y(z1) + 6, 'FACADE X 84', (200, 200, 210)) if x0 < WZ_DOOR[0] < x1 else None; cv.text(X(x0) + 6, Y(0) - 18, 'APPROACH LINE (FROM THE EAST GATE)', (255, 255, 255))
    cv.rect(X(x0) + 10, Y(z0) - 16, X(x0 + 5) + 10, Y(z0) - 12, (255, 255, 255)); cv.text(X(x0) + 10, Y(z0) - 32, '5 M', (255, 255, 255))
    lx, ly = X(x1) + 24, M + 40
    for label, c in [('JOIN CIRCLE (DRAFT)', (60, 255, 210)), ('APPROACH: DRAFT (CENTRE)', (255, 150, 60)), ('APPROACH: EXP-LAYOUT-3 (HOLLOW)', (255, 150, 60)),
                     ('RETURNS: DRAFT, ORDER, FACING', (120, 255, 120)), ('RETURNS: EXP-LAYOUT-3 (X)', (255, 90, 90)), ('F INTERACTION VOLUME', (80, 220, 255)),
                     ('DOOR CLOSURE', (255, 255, 120)), ('DOOR ANCHOR', (0, 160, 255)), ('PORTICO PYLONS', (150, 170, 190)), ('CANOPY / ROOF (OVERHEAD)', (200, 200, 210)),
                     ('WILDS COVER', (120, 104, 96)), ('BUILDING', (110, 112, 120)), ('VEHICLE / COVER', (160, 112, 72)), ('SPAWN EXIT, 8 M APRON', (190, 110, 255)), ('STREET (REACHED)', (58, 56, 62))]:
        cv.rect(lx, ly, lx + 14, ly + 10, c); cv.text(lx + 20, ly, label, (210, 214, 220)); ly += 22
    cv.save(path)

def ascii_map():
    out = []
    for r in range(int((BOUNDS['maxZ'] - BOUNDS['minZ']) / 2)):
        z = BOUNDS['maxZ'] - 2 * r - 1; line = ''
        for c in range(int(BOUNDS['maxX'] - BOUNDS['minX'])):
            x = BOUNDS['minX'] + c + 0.5; ch = ' '
            for b in blocks:
                if yspan(b)[1] > 0.01 and dist_xz(b, x, z) <= 1e-9 and not b['id'].endswith('paving') and '-slab-' not in b['id']: ch = b['_ch']
            if ch == ' ': ch = '.'
            line += ch
        out.append(f'{z - 1:+4.0f}..{z + 1:+3.0f} ' + line)
    return '\n'.join(out)

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--png'); ap.add_argument('--look'); ap.add_argument('--maplooks', action='store_true', help='write the look into Resources/MapLooks.json'); ap.add_argument('--ascii', action='store_true')
    ap.add_argument('--join-png', help='check the exp-layout-4 join circle draft (not live) and draw its door area'); a = ap.parse_args()
    ok, report, graphs = checks()
    if a.ascii: print(ascii_map())
    print('\n'.join(report))
    if a.json:
        with open(a.json, 'w') as f: json.dump(draft(), f, indent=None, separators=(',', ':'), sort_keys=False)
        print('wrote', a.json)
    if a.look:
        with open(a.look, 'w') as f: json.dump(look(), f, indent=None, separators=(',', ':'), sort_keys=False)
        print('wrote', a.look)
    if a.maplooks: write_maplooks(); print('wrote the', MAP, 'entry of', MAPLOOKS)
    if a.png: plan(a.png, graphs); print('wrote', a.png)
    if a.join_png:
        ok_j, rep_j = join_checks(graphs); print('\n'.join(rep_j)); join_plan(a.join_png, graphs); print('wrote', a.join_png); ok &= ok_j
    sys.exit(0 if ok else 1)
