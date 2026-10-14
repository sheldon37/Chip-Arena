# Expedition town M1 draft blockout: `expedition-city`, a zombie city at dusk (120 x 120 m, one street level).
# A draft for Astra: it is NOT wired into the game. It emits a map entry in the UnityMaps.json shape (blocks, bounds,
# spawn tables, pilot exclusion columns) and the town's area record for ExpeditionLayout.json (docs/EXPEDITION-LAYOUT.md):
# door anchor, returns, interaction and closure volumes, the reprint machine and caches, the sanctuary volume and its
# eight slots, the final gate, the enemy spawns (PveEntry shape plus `allow`) and the final boss trigger and points.
# It prints its checks: floor reachability from the sanctuary with the final gate shut and open, every record on walkable
# floor at its own level with pilot clearance (Hulk clearance where Hulks are allowed), the final plaza reachable only
# through the gate volume, no spawn inside or near the sanctuary.
# Usage: python3 docs/arenas/scripts/expedition_city.py [--json out.json] [--png plan.png] [--look look.json] [--ascii]
# Plan: a ring-and-cross town. The sanctuary plaza (24 m) sits at the centre; four streets leave it: north up the avenue
# to the final gate, east to the Meridian Biocontainment frontage (Ward Zero), west to the foundry plot and south to the
# transit station plot (both reserved, sealed). A ring road links the four arms; alleys cut the inner blocks.
# Coordinates: Unity frame, x east, z north, y up, metres. The whole walkable town is at y 0.
# Deterministic: no clocks, no randomness, sorted outputs; the same run writes the same bytes.
import argparse, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from expedition_common import (Collider, Canvas, components, block_hash, id_ok, v, q, qyaw, vol, vol_floor, vol_in_bounds, pt, vol_block, in_vol, vol_dist_xz,
                               yaw_of, yaw_rad, forward, dist_xz, yspan, footprint, corners, r4, RADIUS, HEIGHT, PILOT_CLEAR, HULK_CLEAR, HULK_BAND,
                               HULK_RADIUS, HULK_HEIGHT, HULK_SLOT)

MAP = 'expedition-city'; AREA = 'exp-city'
B = 60.0                       # bounds half extent (120 x 120)
EDGE = 62.0                    # the perimeter masses run 2 m past the bounds
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
for i, (a, b, lo, hi) in enumerate(strips(-EDGE, EDGE, 2 * EDGE, [(h[0], h[1], h[2] + EDGE, h[3] + EDGE) for h in holes])):
    box(f'slab-{i}', a, b, lo - EDGE, hi - EDGE, -0.5, 0, material='foundation', ch='.')
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
    # east: the Meridian Biocontainment frontage (Ward Zero) between an annex and a depot
    ('e-annex', 34, 44, 10, 30, 11, 'stone'), ('e-tower', 44, EDGE, 20, 30, 17, 'brick'), ('wz-facility', 44, EDGE, -20, 20, 13, 'stone'),
    ('e-depot', 34, 44, -34, -10, 9, 'metal'), ('e-block', 44, EDGE, -34, -20, 15, 'brick'),
    # west: the foundry plot (exp-foundry, reserved and sealed)
    ('w-annex', -44, -34, 10, 30, 10, 'brick'), ('w-tower', -EDGE, -44, 20, 30, 15, 'brick'), ('foundry-hall', -EDGE, -44, -20, 20, 14, 'metal'),
    ('w-depot', -44, -34, -34, -10, 10, 'metal'), ('w-block', -EDGE, -44, -34, -20, 13, 'brick'),
    # south: the transit station plot (exp-transit, reserved and sealed) between two kiosks
    ('transit-hall', -20, 20, -EDGE, -44, 8, 'stone'), ('kiosk-e', 10, 20, -44, -34, 6, 'stone'), ('kiosk-w', -20, -10, -44, -34, 6, 'stone'),
    ('s-block-e', 20, 44, -EDGE, -34, 12, 'brick'), ('s-corner-e', 44, EDGE, -EDGE, -34, 18, 'brick'),
    ('s-block-w', -44, -20, -EDGE, -34, 11, 'brick'), ('s-corner-w', -EDGE, -44, -EDGE, -34, 16, 'brick'),
    # north: the final plaza between two flanks and the city hall; the corners close the forecourt
    ('nw-corner', -EDGE, -36, 30, EDGE, 20, 'brick'), ('ne-corner', 36, EDGE, 30, EDGE, 21, 'brick'),
    ('flank-w', -36, -24, 36, EDGE, 15, 'stone'), ('flank-e', 24, 36, 36, EDGE, 15, 'stone'), ('city-hall', -24, 24, 57, EDGE, 16, 'stone'),
]
HEIGHTS = {}
for name, x0, x1, z0, z1, h, mat in BUILDINGS:
    box('bldg-' + name, x0, x1, z0, z1, 0, h, material=mat, ch='#', pilot=True); HEIGHTS[name] = h
# Skyline pieces on the roofs (out of reach under the exclusion columns; they read at dusk).
box('bldg-wz-tower', 50, EDGE, -8, 8, 13, 24, ch='#', pilot=True)                       # the Meridian tower
box('bldg-foundry-stack', -57.5, -54.5, -13.5, -10.5, 14, 32, material='brick', ch='#', pilot=True)
box('bldg-city-hall-clock', -5, 5, 57, EDGE, 16, 27, ch='#', pilot=True)
ROOF = [('ne-tenement-tank', 21, 23.5, 14, 16.5, 13, 15.8, 'metal'), ('nw-tenement-tank', -16, -13.5, 14.5, 17, 12, 14.8, 'metal'),
        ('se-loft-stair', 22, 25, -27, -24.5, 14, 16.6, 'stone'), ('sw-tenement-stair', -20, -17, -18.5, -16, 12, 14.6, 'stone'),
        ('ne-tower-plant', 8, 12, 25, 28.5, 18, 20.2, 'metal'), ('e-tower-tank', 50, 53, 23, 26, 17, 20.0, 'metal'),
        ('w-tower-stair', -55, -52, 22.5, 25.5, 15, 17.6, 'stone'), ('s-block-e-tank', 30, 33, -50, -47, 12, 15.0, 'metal'),
        ('nw-corner-tank', -50, -46, 44, 48, 20, 23.2, 'metal'), ('ne-corner-stair', 44, 47, 40, 43, 21, 23.6, 'stone'),
        ('transit-vent-0', -12, -8, -54, -50, 8, 9.6, 'metal'), ('transit-vent-1', 8, 12, -54, -50, 8, 9.6, 'metal')]
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
# INSTANCE FRONTAGES. Ward Zero (east): a portico on two pylons over the doorway in the facility's west face (x 44).
WZ_DOOR = (44.0, 0.0)          # doorway centre on the facade; the anchor faces out of the doorway (-x, yaw 270)
box('wz-portico-pylon-s', 40.6, 41.4, -4.6, -3.8, 0, 4.4, ch='P'); box('wz-portico-pylon-n', 40.6, 41.4, 3.8, 4.6, 0, 4.4, ch='P')
box('wz-portico-canopy', 40.4, 44, -4.8, 4.8, 4.4, 4.8, ch='^')
# Foundry (west) and transit (south): reserved plots with sealed facades; their doorways join the manifest later at
# these points without moving geometry.
RESERVED = {'exp-foundry': dict(x=-44.0, y=0.0, z=0.0, yaw=90.0), 'exp-transit': dict(x=0.0, y=0.0, z=-44.0, yaw=0.0)}
box('foundry-shutter', -44.4, -44, -3.5, 3.5, 0, 5.0, material='metal', ch='S')
box('transit-boards', -4, 4, -44.3, -44, 0, 3.6, material='wood', ch='S')
box('transit-canopy', -6, 6, -44, -41, 4.6, 5.0, ch='^')
box('transit-canopy-pylon-w', -6, -5.4, -41.6, -41, 0, 4.6, ch='P'); box('transit-canopy-pylon-e', 5.4, 6, -41.6, -41, 0, 4.6, ch='P')

# =====================================================================================================================
# THE SANCTUARY: the paved plaza inside a ring of barricades (1.1 m, 6 m gaps on the four streets), a brazier at its
# centre, the reprint machine and a cache on its diagonals. Its volume is the whole plaza.
for side, (ax, sg) in {'n': ('z', 1), 's': ('z', -1), 'e': ('x', 1), 'w': ('x', -1)}.items():
    for k, (a, b) in enumerate([(-11.4 if ax == 'z' else -10.6, -3.0), (3.0, 11.4 if ax == 'z' else 10.6)]):
        if ax == 'z': box(f'sanctuary-barricade-{side}-{k}', a, b, *sorted((sg * 10.6, sg * 11.4)), 0, 1.1, ch='=')
        else: box(f'sanctuary-barricade-{side}-{k}', *sorted((sg * 10.6, sg * 11.4)), a, b, 0, 1.1, ch='=')
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
car('car-av-0', -3.4, 15.2, 8); car('car-av-1', 3.0, 16.4, -10)                                          # avenue (south half)
barrier('barrier-av-0', 1.5, 5.6, 29.4, 30.0)                                                            # avenue, police line at the forecourt
car('car-fc-0', -15.5, 31.3, 74); car('car-fc-1', 17.0, 33.0, -66); barrier('barrier-fc-0', -27.0, -23.0, 34.6, 35.2)   # forecourt police cars
obox('bus-es', 17.4, -1.6, 2.6, 9.0, 0, 3.0, 82)                                                          # E street: the crashed bus
car('car-es-0', 16.0, 2.8, 98); car('ambulance-wz', 37.5, -7.6, 70, length=5.6, width=2.3, h=2.5)
obox('truck-ws', -17.4, 2.8, 2.5, 7.5, 0, 3.0, 84); car('car-ws-0', -16.0, -2.8, 100)                      # W street
for i, (cx, cz) in enumerate([(-38.6, 8.0), (-40.4, 8.0), (-39.5, 6.2)]): box(f'crate-fd-{i}', cx - 0.75, cx + 0.75, cz - 0.75, cz + 0.75, 0, 1.4, material='wood', ch='x')
car('car-ss-0', -2.6, -19.0, 4); barrier('barrier-ss-0', 6.0, 9.5, -38.0, -37.4)   # S street, station square
car('taxi-tr', -6.8, -39.5, 28)
for i, (x0, z0, along) in enumerate([(28.0, 21.0, 'z'), (-29.3, -22.5, 'z'), (16.5, 20.0, 'x'), (-12.0, 20.0, 'x'), (25.5, -21.3, 'x'), (-17.5, -21.3, 'x'), (28.0, 8.0, 'z'), (-29.3, -10.0, 'z')]):
    if along == 'z': box(f'dumpster-{i}', x0, x0 + 1.3, z0, z0 + 2.0, 0, 1.3, material='metal', ch='u')
    else: box(f'dumpster-{i}', x0, x0 + 2.0, z0, z0 + 1.3, 0, 1.3, material='metal', ch='u')
car('car-re-0', 32.6, -15.5, 4); car('car-rw-0', -32.6, 15.5, -6); car('car-rs-0', 14.5, -31.2, 86); car('car-rs-1', -17.0, -32.2, 92)

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
door('east-n', 5.0, 'z', 21.5, -1); door('east-s', -5.0, 'z', 25.0, 1, allow=BIG)
door('west-n', 5.0, 'z', -23.0, -1); door('west-s', -5.0, 'z', -26.0, 1)
door('south-e', 5.0, 'x', -26.5, -1); door('south-w', -5.0, 'x', -25.0, 1)
door('ring-e-0', 34.0, 'x', 20.0, -1, allow=BIG); door('ring-e-1', 28.0, 'x', -26.0, 1); door('ring-w-0', -34.0, 'x', -20.0, 1, allow=BIG); door('ring-w-1', -28.0, 'x', 27.0, -1)
door('ring-s-0', -34.0, 'z', 26.0, 1, allow=BIG); door('ring-s-1', -28.0, 'z', -23.0, -1)
door('alley-ne', 15.0, 'x', 27.0, 1, out=1.75); door('alley-nw', 20.0, 'z', -16.0, 1, out=1.75); door('alley-se', -20.0, 'z', 23.0, -1, out=1.75); door('alley-sw', -15.0, 'x', -26.0, -1, out=1.75)
door('forecourt-e', 30.0, 'z', 10.5, 1); door('forecourt-w', 30.0, 'z', -23.0, 1)
door('square-hall-w', FP[3], 'z', -10.0, -1, allow=BIG); door('square-hall-e', FP[3], 'z', 10.0, -1, allow=BIG)
door('square-flank-w', -24.0, 'x', 45.0, 1); door('square-flank-e', 24.0, 'x', 50.0, -1)
# Vents: storm grates and manholes on the avenue, the ring and the plazas.
vent('avenue', -1.5, 23.0, 0, -1)
vent('ring-ne', 31.0, 26.5, 0, -1); vent('ring-se', 31.0, -11.0, 0, 1); vent('ring-sw', -31.0, -12.0, 0, -1); vent('ring-nw', -30.5, 9.0, 0, 1)
vent('ring-s-e', 8.5, -30.5, 1, 0); vent('ring-s-w', -8.0, -31.0, 1, 0); vent('forecourt', -4.0, 32.8, 1, 0)
vent('square-w', -6.0, 41.5, 1, 0); vent('square-e', 6.0, 41.5, -1, 0)
# Drops off roof edges (brutes: 3.3 m out from the facade, a 2.8 m clear landing; Hulks: 4.5 m clear in the open).
drop('avenue-w', -2.7, 28.6, HEIGHTS['nw-tower'], 1, 0); drop('avenue-e', 2.7, 24.5, HEIGHTS['ne-tower'], -1, 0)
drop('east', 26.5, 1.7, HEIGHTS['ne-shops'], 0, -1); drop('west', -21.0, -1.7, HEIGHTS['sw-shops'], 0, 1); drop('south', 1.7, -20.0, HEIGHTS['se-tenement'], -1, 0)
drop('fd-forecourt', -39.0, -6.4, HEIGHTS['w-depot'], 0, 1)
drop('square-w', -20.7, 52.0, HEIGHTS['flank-w'], 1, 0); drop('square-e', 20.7, 42.0, HEIGHTS['flank-e'], -1, 0)
drop('hulk-east', 31.0, 0.0, HEIGHTS['ne-shops'], -1, 0, hulk=True); drop('hulk-west', -31.0, 0.0, HEIGHTS['nw-shops'], 1, 0, hulk=True)
drop('hulk-station', 0.0, -36.0, HEIGHTS['transit-hall'], 0, 1, hulk=True); drop('hulk-square', 0.0, 42.5, HEIGHTS['city-hall'], 0, -1, hulk=True)
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
# THE WARD ZERO DOORWAY (the town side). The door faces out of the doorway (-x, yaw 270). Its anchor is the clear floor
# point 0.6 m in front of the doorway (the loader probes every anchor with a pilot capsule); the closure fills the doorway
# just in front of the face; the interaction volume and the approach are in front; eight returns outside the portico,
# facing the town.
def door_record(anchor, cx, cz, yaw, slots_key, slots):
    fx, fz = forward(yaw)
    return {'anchor': anchor,
            'interact': vol(cx + fx * 1.0, 0, cz + fz * 1.0, 3.0, 3.0, 2.0, yaw),
            'closure': vol(cx + fx * 0.2, 0, cz + fz * 0.2, DOOR_W, DOOR_H, 0.4, yaw),
            'approach': pt(cx + fx * 1.6, 0, cz + fz * 1.6),
            slots_key: slots}
WZ_YAW = 270.0
RETURNS = [pt(x, 0, z, WZ_YAW) for x in (39.6, 38.0) for z in (-3.0, -1.0, 1.0, 3.0)]
DOOR_ANCHOR = f'{AREA}:door:exp-ward-zero'

def area():
    fx, fz = forward(WZ_YAW)
    anchors = [dict(id=DOOR_ANCHOR, kind='door', building='exp-ward-zero', **pt(WZ_DOOR[0] + fx * 0.6, 0, WZ_DOOR[1] + fz * 0.6, WZ_YAW)),
               dict(id=f'{AREA}:machine:0', kind='machine', building='', **MACHINE)]
    anchors += [dict(id=f'{AREA}:cache:{i}', kind='chest', building='', **c) for i, c in enumerate(CACHES)]
    def front(p, d):
        fx, fz = forward(p['yaw']); return dict(x=r4(p['x'] + fx * d), y=p['y'], z=r4(p['z'] + fz * d))
    inter = [dict(anchor=f'{AREA}:machine:0', **front(MACHINE, 0.9), range=2.5)]
    inter += [dict(anchor=f'{AREA}:cache:{i}', **front(c, 0.8), range=2.0) for i, c in enumerate(CACHES)]
    return {'id': AREA, 'role': 'world', 'map': MAP, 'blockHash': block_hash(game_map()),
            'anchors': anchors, 'doors': [door_record(DOOR_ANCHOR, WZ_DOOR[0], WZ_DOOR[1], WZ_YAW, 'returns', RETURNS)], 'interactions': inter,
            'sanctuary': {'volumes': [SANCTUARY], 'slots': SLOTS},
            'gates': [{'id': f'{AREA}:gate:final', 'opens': 'final', 'closure': GATE}],
            'spawns': entries, 'boss': {'trigger': BOSS_TRIGGER, 'points': BOSS_POINTS}}

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
                bounds=dict(minX=-B, maxX=B, minZ=-B, maxZ=B, ceiling=CEIL, killY=KILL),
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
                       L('meridian-portico', 42.4, 4.0, 0, 0x9FE8FF, 1.4, 12, 'stutter', period=9, length=0.2, depth=0.6),
                       L('final-gate', 0, 9.0, 34.0, 0xFF3B30, 1.3, 16), L('wreck-fire-es', 21.5, 1.6, -2.7, 0xFF6A1E, 1.6, 9, 'fire', rate=6.5, depth=0.4, phase=0.4),
                       L('sodium-ring-e', 31.0, 5.5, -26.0, 0xFF9A3C, 1.1, 12), L('sodium-ring-w', -31.0, 5.5, 26.0, 0xFF9A3C, 1.1, 12)],
            'blockLooks': [BL('expedition-city-slab-*', 'asphalt', 0x3A3840, 'the street slab'),
                           BL('expedition-city-sanctuary-paving', 'cobble', 0x7A6E66, 'the sanctuary plaza'),
                           BL('expedition-city-square-paving', 'cobble', 0x6E6A70, 'the final plaza'),
                           BL('expedition-city-sanctuary-barricade-*', 'timber', 0x8A6A4A, 'sanctuary barricades'),
                           BL('expedition-city-sanctuary-brazier', 'tar', 0x2A2422, 'the brazier'),
                           BL('expedition-city-gate-*', 'concrete-board', 0x8C8478, 'the final gatehouse'),
                           BL('expedition-city-bldg-wz-*', 'concrete-board', 0xB8BEC4, 'Meridian Biocontainment (Ward Zero)'),
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
            'blocks': dict(dressed=True, dust=0.06, haze=0.3, hazeTop=3.2, hazeColor=0x5E4A62)}

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
    rows.append(f'   "blocks":{j(L["blocks"])}}}')
    return '\n'.join(rows)
def write_maplooks(path=MAPLOOKS):
    """Insert or replace this map's entry as the last entry of MapLooks.json's maps; every other byte stays as it is."""
    text = open(path, encoding='utf-8').read(); entry = look_text(); start = text.find('\n {"id":"' + MAP + '",')
    tail = '\n],\n"preview":['
    if start >= 0:
        end = text.index(tail, start); assert text.find('\n {"id":', start + 1, end) < 0, 'the expedition-city look must be the last entry'
        text = text[:start] + '\n' + entry + text[end:]
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
    shut = Collider(colliders + [gate_block], bounds=dict(minX=-B, maxX=B, minZ=-B, maxZ=B)); shut.build(keep=lambda y: abs(y) < 0.05)
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
    bnd = all(abs(p['x']) <= B - 1.5 and abs(p['z']) <= B - 1.5 for p in [e['exit'] for e in entries] + SLOTS + RETURNS + BOSS_POINTS + CACHES + [MACHINE])
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
    inb = all(abs(b['pos']['x']) + b['size']['x'] / 2 <= EDGE + 1e-6 and abs(b['pos']['z']) + b['size']['z'] / 2 <= EDGE + 1e-6 and yspan(b)[1] <= CEIL for b in blocks)
    out.append(f'every block within x, z +-{EDGE:g} and under the {CEIL:g} m ceiling: {inb}'); ok &= inb
    out.append(f'blockHash {a["blockHash"]}')
    out.append('reserved doorways (sealed facades, not in the manifest yet): ' + '; '.join(f'{k} ({p["x"]:g}, {p["y"]:g}, {p["z"]:g}) yaw {p["yaw"]:g}' for k, p in sorted(RESERVED.items())))
    return ok, out, (shut, reach, open_, reach_o)

# =====================================================================================================================
# PLAN (top-down PNG): buildings shaded by height, walkable floor, the sanctuary and its slots, the gate, the boss
# trigger and points, the door with its volumes and returns, the machine and caches, the spawns by kind.
def plan(path, graphs, scale=8):
    shut, reach, open_, reach_o = graphs
    M = 40; W, H = int(2 * EDGE * scale) + 2 * M + 300, int(2 * EDGE * scale) + 2 * M + 30
    cv = Canvas(W, H)
    def X(x): return int(round((x + EDGE) * scale)) + M
    def Y(z): return int(round((EDGE - z) * scale)) + M + 20
    cv.rect(X(-EDGE), Y(EDGE), X(EDGE), Y(-EDGE), (24, 22, 28))
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
    # legend
    lx = X(EDGE) + 20; ly = 60
    cv.text(M, 10, 'EXPEDITION CITY M1 DRAFT  120 X 120 M  NORTH UP  1 PX = %d CM' % (100 // scale), (232, 251, 255))
    for label, c in [('SANCTUARY VOLUME', (90, 255, 140)), ('SLOT  (FACING)', (255, 255, 255)), ('WARD ZERO DOOR', (0, 160, 255)), ('INTERACT VOLUME', (80, 220, 255)),
                     ('CLOSURE', (255, 255, 120)), ('RETURNS, APPROACH', (80, 220, 255)), ('FINAL GATE', (255, 60, 50)), ('BOSS TRIGGER', (230, 80, 230)),
                     ('BOSS POINT', (255, 90, 255)), ('MACHINE', (120, 255, 160)), ('CACHE', (255, 200, 40)), ('DOOR SPAWN', KC['door']), ('VENT SPAWN', KC['vent']),
                     ('DROP (RING: CLEAR)', KC['drop']), ('BREACH (HULK)', KC['breach']), ('BIG DOT: HULK', (255, 255, 255)), ('BREACH PANEL', (230, 200, 60)),
                     ('COVER', (160, 112, 72)), ('STREET (REACHED, GATE SHUT)', (64, 60, 66)), ('FLOOR BEHIND THE GATE', (88, 58, 70)), ('BUILDING (LIGHTER = TALLER)', (160, 140, 150))]:
        cv.rect(lx, ly, lx + 14, ly + 10, c); cv.text(lx + 20, ly, label, (210, 214, 220)); ly += 22
    for label, x, z in (('SANCTUARY', -6.5, -9.0), ('FINAL PLAZA', -8.5, 55.5), ('FINAL GATE', -6.5, 34.2), ('AVENUE', -3.0, 18.0), ('RING ROAD', 28.6, -12.0),
                        ('MERIDIAN', 45.0, -22.5), ('(WARD ZERO)', 45.0, -25.0), ('FORECOURT', -10.0, 31.6)):
        cv.text(X(x), Y(z), label, (240, 236, 220))
    cv.save(path)

def ascii_map():
    out = []
    for r in range(60):
        z = B - 2 * r - 1; line = ''
        for c in range(120):
            x = -B + c + 0.5; ch = ' '
            for b in blocks:
                if yspan(b)[1] > 0.01 and dist_xz(b, x, z) <= 1e-9 and not b['id'].endswith('paving') and '-slab-' not in b['id']: ch = b['_ch']
            if ch == ' ': ch = '.'
            line += ch
        out.append(f'{z - 1:+4.0f}..{z + 1:+3.0f} ' + line)
    return '\n'.join(out)

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--png'); ap.add_argument('--look'); ap.add_argument('--maplooks', action='store_true', help='write the look into Resources/MapLooks.json'); ap.add_argument('--ascii', action='store_true'); a = ap.parse_args()
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
    sys.exit(0 if ok else 1)
