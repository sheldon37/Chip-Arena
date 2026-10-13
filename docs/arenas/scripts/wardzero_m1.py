# Ward Zero M1 draft blockout: Meridian Biocontainment, Ward Zero, where the Siren breached (80 x 64 slab, two floors,
# an open Breach Atrium through both, the Cradle at its middle, wings, halls, galleries, gantries).
# A draft for Astra: it is NOT wired into the game. It emits draft map data in the UnityMaps.json shape (blocks with the id
# families the presentation keys on, spawns, bounds, the hazard definitions of docs/arenas/WARDZERO-SIMULATION-CONTRACT.md,
# the collision ramps that join the floors, nav hints) plus the PvE content the map needs (entries, machines, pads, boss
# points), and prints its checks (walkable reachability of both floors from the spawns, every entry, machine and pad on
# reachable floor, each route between the floors on its own, spawn sight lines, hazard clearances and timing tables).
# Usage: python3 docs/arenas/scripts/wardzero_m1.py [--json out.json] [--png out.png] [--ascii] [--sections]
# Coordinates: Unity frame, x east, z north, y up, metres. Point symmetric: (x, z) -> (-x, -z) unless noted.
# Deterministic: no clocks, no randomness, sorted outputs; the same run writes the same bytes.
import argparse, itertools, json, math, struct, sys, zlib

MAP = 'siren'                  # the id the game already uses for Ward Zero (PvE, PvP and the Dev Sandbox)
SX, SZ = 40, 32                # slab half extents (80 x 64)
BX, BZ = 38, 30                # play bounds (shell 2 m deep)
L1 = 6.0                       # upper floor top (deck 5.6..6.0)
DECK = 0.4
ROOF = 13.0                    # roof underside (the vault 13.0..13.6 is solid everywhere)
RAIL = 1.1                     # rail height (a 0.15 m top bar on posts over a 0.15 m kick plate: see-through, still a barrier)
STEP_RISE, STEP_RUN = 0.25, 0.5   # stair treads (visual; collision is a closed wedge, the Cut precedent)
RAMP_DEG = 24.0                # gurney ramps
DOOR = 3.4                     # doorway height (the Hulk zombie stands about 3.2 m)

blocks = []                    # ContentBlock dicts (+ '_ch' painter char, '_los' blocks sight, '_col' collides as authored)
SWAP = {'n': 's', 's': 'n', 'e': 'w', 'w': 'e', 'ne': 'sw', 'sw': 'ne', 'nw': 'se', 'se': 'nw'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def v(x=0, y=0, z=0): return dict(x=round(x, 4), y=round(y, 4), z=round(z, 4))
def q(x=0, y=0, z=0, w=1): return dict(x=round(x, 6), y=round(y, 6), z=round(z, 6), w=round(w, 6))
def qyaw(deg): a = math.radians(deg) / 2; return q(0, math.sin(a), 0, math.cos(a))
def qpitch(deg): a = math.radians(deg) / 2; return q(math.sin(a), 0, 0, math.cos(a))
def qroll(deg): a = math.radians(deg) / 2; return q(0, 0, math.sin(a), math.cos(a))
def mq(r): return q(-r['x'], r['y'], -r['z'], r['w'])          # the 180-degree turn about y (point mirror) of a rotation
HP = {'cover': 34, 'structural': 80}

def add(b, mirror):
    blocks.append(b)
    if mirror:
        m = dict(b); m['id'] = mirror_id(b['id']); assert m['id'] != b['id'], 'mirror keeps id ' + b['id']
        m['pos'] = v(-b['pos']['x'], b['pos']['y'], -b['pos']['z']); m['rotation'] = mq(b['rotation']); blocks.append(m)
def box(id, x0, x1, z0, z1, y0, y1, role='static', material='stone', ch='#', mirror=True, los=None, col=True, hp=None):
    if x1 - x0 < 1e-6 or z1 - z0 < 1e-6 or y1 - y0 < 1e-6: return
    b = dict(id=MAP + '-' + id, material=material, pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0),
             rotation=q(), role=role, mass=26, segment=None, hp=hp if hp is not None else HP.get(role, 0), _ch=ch, _col=col,
             _los=((y1 - y0) >= 1.9 and min(x1 - x0, z1 - z0) >= 0.25) if los is None else los)
    add(b, mirror)
def obox(id, cx, cy, cz, sx, sy, sz, rot, role='static', material='stone', ch='#', mirror=True, los=False, col=True, hp=None):
    """A rotated block (centre, full size, quaternion)."""
    b = dict(id=MAP + '-' + id, material=material, pos=v(cx, cy, cz), size=v(sx, sy, sz), rotation=rot, role=role, mass=26, segment=None,
             hp=hp if hp is not None else HP.get(role, 0), _ch=ch, _col=col, _los=los)
    add(b, mirror)

def strips(u0, u1, h, openings):
    """A face u0..u1 x 0..h minus opening rects (a, b, lo, hi): solid pieces (a, b, v0, v1), merged along u."""
    cuts = sorted({u0, u1, *[o[0] for o in openings], *[o[1] for o in openings]})
    out = []
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
def wall_x(name, x0, x1, z0, z1, y0, h, openings, **kw):
    for i, (a, b, lo, hi) in enumerate(strips(x0, x1, h, openings)): box(f'{name}-{i}', a, b, z0, z1, y0 + lo, y0 + hi, **kw)
def wall_z(name, z0, z1, x0, x1, y0, h, openings, **kw):
    for i, (a, b, lo, hi) in enumerate(strips(z0, z1, h, openings)): box(f'{name}-{i}', x0, x1, a, b, y0 + lo, y0 + hi, **kw)
def slab(name, x0, x1, z0, z1, y0, y1, holes, **kw):
    """A slab minus hole rects (x0, x1, z0, z1): strips along x, merged along z."""
    for i, (a, b, lo, hi) in enumerate(strips(x0, x1, z1 - z0, [(h[0], h[1], h[2] - z0, h[3] - z0) for h in holes])):
        box(f'{name}-{i}', a, b, z0 + lo, z0 + hi, y0, y1, **kw)

def rail(id, x0, x1, z0, z1, y, **kw):
    """A see-through rail: the top bar (0.95..1.1 m) and the kick plate (0..0.15 m); the kit draws the posts between them."""
    kw.setdefault('los', False); kw.setdefault('ch', 'r')
    box(id, x0, x1, z0, z1, y + RAIL - 0.15, y + RAIL, **kw); box(id + '-kick', x0, x1, z0, z1, y, y + 0.15, **kw)

ramps = []                     # collision ramps between the floors (contract section 4): closed wedges, their visual blocks
def ramp_entry(rid, kind, low, high, width, bottom, visual, mirror=True):
    r = dict(id=MAP + '-' + rid, kind=kind, low=v(*low), high=v(*high), width=round(width, 4), bottom=bottom, blocks=sorted(MAP + '-' + b for b in visual))
    ramps.append(r)
    if mirror:
        ramps.append(dict(id=mirror_id(r['id']), kind=kind, low=v(-low[0], low[1], -low[2]), high=v(-high[0], high[1], -high[2]), width=round(width, 4), bottom=bottom,
                          blocks=sorted(mirror_id(b) for b in r['blocks'])))
def stair(name, axis, a0, a1, w0, w1, y0, y1, ch='/', mirror=True):
    """A flight of solid treads (visual; .25 m risers on .5 m treads, 26.6 degrees) from a0 (floor y0) to a1 (floor y1)
    along 'x' or 'z' between w0..w1 across, and its collision wedge (Astra's Cut precedent: the treads stay as they are,
    a closed walkable wedge from the foot to the top edge replaces their collision)."""
    n = int(round((y1 - y0) / STEP_RISE)); d = 1 if a1 > a0 else -1; ids = []
    assert abs(abs(a1 - a0) - n * STEP_RUN) < 1e-6, name
    for k in range(n):
        s0, s1 = a0 + d * STEP_RUN * k, a0 + d * STEP_RUN * (k + 1); lo, hi = min(s0, s1), max(s0, s1); top = y0 + STEP_RISE * (k + 1)
        if axis == 'x': box(f'{name}-{k:02d}', lo, hi, w0, w1, y0, top, ch=ch, los=False, col=False, mirror=mirror)
        else: box(f'{name}-{k:02d}', w0, w1, lo, hi, y0, top, ch=ch, los=False, col=False, mirror=mirror)
        ids.append(f'{name}-{k:02d}')
    c = (w0 + w1) / 2
    low = (a0, y0, c) if axis == 'x' else (c, y0, a0); high = (a1, y1, c) if axis == 'x' else (c, y1, a1)
    ramp_entry(name, 'stair', low, high, w1 - w0, y0, ids, mirror)
def gurney_ramp(name, axis, a0, a1, w0, w1, y0, y1, mirror=True):
    """A gurney ramp: one pitched slab (a rotated block, 0.4 m thick, its top face from (a0, y0) to (a1, y1)) on a solid
    skirt; collision as a closed wedge like the stairs."""
    run = abs(a1 - a0); rise = y1 - y0; ang = math.degrees(math.atan2(rise, run)); L = math.hypot(run, rise); t = 0.4; d = 1 if a1 > a0 else -1
    mx, my = (a0 + a1) / 2, (y0 + y1) / 2
    # the slab's centre sits t/2 under the top face's middle, along the face normal
    nu, ny = -d * math.sin(math.radians(ang)), math.cos(math.radians(ang))
    cu, cy = mx - nu * t / 2, my - ny * t / 2; c = (w0 + w1) / 2
    if axis == 'z':
        obox(f'{name}-deck', c, cy, cu, w1 - w0, t, L, qpitch(-d * ang), ch='/', mirror=mirror, col=False)
        low, high = (c, y0, a0), (c, y1, a1)
    else:
        obox(f'{name}-deck', cu, cy, c, L, t, w1 - w0, qroll(d * ang), ch='/', mirror=mirror, col=False)
        low, high = (a0, y0, c), (a1, y1, c)
    ramp_entry(name, 'ramp', low, high, w1 - w0, y0, [f'{name}-deck'], mirror)
    return ang

# =====================================================================================================================
# SHELL: slab, outer walls to the vault, the vault (roof) over everything. Sealed: no jump or launch leaves the building.
box('slab', -SX, SX, -SZ, SZ, -0.5, 0, ch='.', mirror=False, los=False)
box('shell-n', -SX, SX, BZ, SZ, -0.5, ROOF + 0.6, ch='#')                         # mirror shell-s
box('shell-e', BX, SX, -BZ, BZ, -0.5, ROOF + 0.6, ch='#')                         # mirror shell-w
box('vault', -SX, SX, -SZ, SZ, ROOF, ROOF + 0.6, ch='V', mirror=False, los=True)

# =====================================================================================================================
# BREACH ATRIUM: a 24 x 20 m void through both floors (x -12..12, z -10..10) inside the atrium room (walls at x +-16, z +-14).
# Ground: the arcade under the gallery, four portals (blast shutters), corner doors; the Cradle; two grand stairs.
# Upper: the gallery ring (4 m), two live-grating bridges across the void, rails with drop gaps.
VX, VZ = 12, 10
PORTAL = 3.0                   # portal half width (6 m), 4.5 m tall
wall_x('atrium-wall-n', -16.4, 16.4, 14.0, 14.4, 0, L1 - DECK, [(-PORTAL, PORTAL, 0, 4.5), (-13.4, -11.0, 0, DOOR), (11.0, 13.4, 0, DOOR)], ch='H')
wall_z('atrium-wall-e', -14.0, 14.0, 16.0, 16.4, 0, L1 - DECK, [(-PORTAL, PORTAL, 0, 4.5), (-12.4, -10.0, 0, DOOR), (9.6, 12.0, 0, DOOR)], ch='H')
# Gallery decks (the upper floor round the void) and the void-edge rails (gaps: bridges, stair landings, drop gaps).
box('gallery-n', -16.4, 16.4, VZ, 14.4, L1 - DECK, L1, ch='G', los=True)
box('gallery-e', VX, 16.4, -VZ, VZ, L1 - DECK, L1, ch='G', los=True)
for i, (a, b) in enumerate([(-VX, -7.5), (-4.5, -1.2), (1.2, 4.5), (7.5, VX)]):
    rail(f'gallery-n-rail-{i}', a, b, VZ, VZ + 0.15, L1, ch='r', los=False)
for i, (a, b) in enumerate([(-VZ, 3.0), (5.0, 6.5), (8.5, VZ)]):
    rail(f'gallery-e-rail-{i}', VX, VX + 0.15, a, b, L1, ch='r', los=False)
# Upper atrium walls (gallery to the wings' double-height halls): the specimen bridge door, observation windows (sill 1 m).
wall_z('atrium-upper-e', -14.4, 14.4, 16.0, 16.4, L1, ROOF - L1, [(4.3, 7.2, 0, DOOR), (-8.5, -4.5, 1.0, 3.2), (-1.5, 2.5, 1.0, 3.2)], ch='H')
# Live-grating bridges across the void (cybercity-style `grid` hazards, contract 5.2), rails both sides, a drop gap inboard.
box('bridge-e-deck', 4.5, 7.5, -VZ, VZ, L1 - 0.3, L1, ch='g', material='metal', los=False)
rail('bridge-e-rail-out', 7.35, 7.5, -VZ, VZ, L1, ch='r', los=False, material='metal')
rail('bridge-e-rail-in-0', 4.5, 4.65, -VZ, -1.2, L1, ch='r', los=False, material='metal')
rail('bridge-e-rail-in-1', 4.5, 4.65, 1.2, VZ, L1, ch='r', los=False, material='metal')
# Grand stairs in the void: east flight rises north along the gallery's edge (x 9..12, z -9..3) to a landing that joins
# the east gallery; its point mirror rises south on the west side.
stair('stair-atrium-e', 'z', -9.0, 3.0, 9.0, 12.0, 0, L1)
box('stair-atrium-e-landing', 9.0, 12.0, 3.0, 5.0, L1 - DECK, L1, ch='/', los=False)
rail('stair-atrium-e-rail', 9.0, 9.15, 3.0, 5.0, L1, ch='r', los=False)
rail('stair-atrium-e-rail-n', 9.0, 12.0, 4.85, 5.0, L1, ch='r', los=False)
# The Cradle: the Siren's shattered containment vessel. Six shards on a 5 m ring (tangential, 0.5 m thick) round a clear
# 4.7 m disc (the brute drop lands at its middle, the Siren is revealed there).
for i, (ang, hgt, wid) in enumerate([(20, 2.4, 2.2), (90, 1.3, 2.6), (160, 1.9, 2.0)]):
    r = 5.0; cx, cz = r * math.sin(math.radians(ang)), r * math.cos(math.radians(ang))
    obox(f'cradle-shard-n-{i}', cx, hgt / 2, cz, wid, hgt, 0.5, qyaw(ang), ch='c', material='metal', los=hgt >= 1.9)
# Atrium cover: gurneys and crash carts in the arcade (low, metal), two triage screens.
for name, (cx, cz, sx, sz) in (('n-0', (-9.5, 12.2, 2.2, 1.0)), ('e', (14.2, -6.5, 1.0, 2.2)), ('n-1', (2.5, 12.6, 1.6, 0.8))):
    box(f'atrium-cart-{name}', cx - sx / 2, cx + sx / 2, cz - sz / 2, cz + sz / 2, 0, 1.0, role='cover', material='metal', ch='o', los=False, hp=60)
box('atrium-screen-s', -8.0, -6.0, -12.8, -12.5, 0, 2.0, ch='s', material='metal')
box('atrium-screen-e', 13.4, 13.7, 4.5, 7.0, 0, 2.0, ch='s', material='metal')

# =====================================================================================================================
# NORTH BAND (z 14.4..30), point-mirrored to the south. Ground: the North Hall (z 14.4..19), the NW Morgue, the North Cell
# Block, the NE Intake (team B). Upper: the North Concourse (open to the gallery), the NW Decon Wing (decon lock: gas),
# the Observation Cells, the NE Control Room. Corner stairs: NW (rises east) and NE (rises west), against the shell.
HALL0, HALL1 = 14.4, 19.0
GROUND_DOORS_N = [(-30.0, -27.6, 0, DOOR), (-21.0, -18.6, 0, DOOR), (-9.5, -6.5, 0, DOOR), (6.5, 9.5, 0, DOOR), (18.6, 21.0, 0, DOOR), (27.6, 30.0, 0, DOOR)]
wall_x('hall-n-wall', -BX, BX, HALL1, HALL1 + 0.4, 0, L1 - DECK, GROUND_DOORS_N, ch='H')
UPPER_DOORS_N = [(-29.2, -26.8, 0, DOOR), (-20.2, -17.8, 0, DOOR), (-9.5, -6.5, 0, DOOR), (6.5, 9.5, 0, DOOR), (17.8, 20.2, 0, DOOR), (26.8, 29.2, 0, DOOR)]
wall_x('concourse-n-wall', -BX, BX, HALL1, HALL1 + 0.4, L1, ROOF - L1, UPPER_DOORS_N, ch='H')
# Wing separators (ground and upper) between the cell block and the corner rooms, with a door each.
for side, x0 in (('ne', 16.0), ('nw', -16.4)):
    wall_z(f'wing-wall-{side}', HALL1 + 0.4, BZ, x0, x0 + 0.4, 0, L1 - DECK, [(21.0, 23.4, 0, DOOR)], ch='H')
    wall_z(f'wing-upper-{side}', HALL1 + 0.4, BZ, x0, x0 + 0.4, L1, ROOF - L1, [(21.0, 23.4, 0, DOOR)], ch='H')
# The cell rows (ground cells and upper observation cells): solid masses behind the aisles; their doors are kit on the
# face (cell gates below, door leaves above) and the PvE entries walk out of them.
CELL_ROW = 25.0
box('cells-n-row', -16.0, 16.0, CELL_ROW, BZ, 0, L1 - DECK, ch='C')
box('obs-n-row', -16.0, 16.0, CELL_ROW, BZ, L1, ROOF, ch='C')
box('cells-n-station', -2.4, 2.4, 20.8, 23.2, 0, 2.4, ch='K')                               # the nurse booth (glazed above 1.1 m; solid in the sim)
box('cells-n-gurney-0', -12.6, -10.4, 20.4, 21.3, 0, 1.0, role='cover', material='metal', ch='o', los=False, hp=60)
box('obs-n-desk', 5.0, 7.6, 21.4, 22.4, L1, L1 + 1.0, ch='o', los=False)
# Breach panels (the Hulk breaks through them, contract 6.5): reinforced containment panels 3.2 m wide, 4 m tall, 0.4 m
# proud of a solid face (a cell row's or the shell's), so a broken panel opens onto a dark recess nobody can enter.
BREACH_W, BREACH_H, BREACH_T = 3.2, 4.0, 0.4
breaches = []
def breach_panel(id, axis, u, face, sign, y0, mirror=True):
    """A panel centred at u along the face (x for a z face, z for an x face), its back on the face plane `face`, standing
    out toward `sign` (+1: toward +z or +x)."""
    a, b = u - BREACH_W / 2, u + BREACH_W / 2; f0, f1 = sorted((face, face + sign * BREACH_T))
    if axis == 'z': box(id, a, b, f0, f1, y0, y0 + BREACH_H, ch='B', material='metal', mirror=mirror)
    else: box(id, f0, f1, a, b, y0, y0 + BREACH_H, ch='B', material='metal', mirror=mirror)
    breaches.append((id, axis, u, face, sign, y0))
breach_panel('breach-cells-n', 'z', 10.0, CELL_ROW, -1, 0)
breach_panel('breach-obs-n', 'z', -10.0, CELL_ROW, -1, L1)
# Upper deck over the north band (and the concourse): holes over the corner stairs.
STAIR_Z0 = 27.4
slab('deck-n', -BX, BX, HALL0, BZ, L1 - DECK, L1, [(-34.0, -22.0, STAIR_Z0, BZ), (22.0, 34.0, STAIR_Z0, BZ)], ch='D', los=True)
stair('stair-nw', 'x', -34.0, -22.0, STAIR_Z0, BZ, 0, L1)
stair('stair-ne', 'x', 34.0, 22.0, STAIR_Z0, BZ, 0, L1)
for side, a, b, end in (('nw', -34.0, -22.0, -34.0), ('ne', 22.0, 34.0, 34.0)):
    rail(f'stair-{side}-rail', a, b, STAIR_Z0 - 0.15, STAIR_Z0, L1, ch='r', los=False)
    rail(f'stair-{side}-rail-end', end - 0.15 if end > 0 else end, end + 0.15 if end < 0 else end, STAIR_Z0, BZ, L1, ch='r', los=False)
# NW Morgue (ground x -38..-16.4): drawer walls (kit), autopsy slabs, cold chambers.
for i, (cx, cz) in enumerate([(-33.0, 21.6), (-27.0, 23.4), (-21.5, 21.0)]):
    box(f'morgue-nw-slab-{i}', cx - 1.1, cx + 1.1, cz - 0.5, cz + 0.5, 0, 1.0, ch='o', material='metal', los=False)
box('morgue-nw-chamber-0', -37.9, -36.4, 19.6, 22.6, 0, 2.6, ch='K', material='metal')
box('morgue-nw-chamber-1', -30.6, -28.8, 24.4, 25.6, 0, 2.4, ch='K', material='metal')
# NE Intake (ground x 16.4..38, team B spawn): lockers, decon showers, benches.
box('intake-ne-lockers', 24.0, 28.0, 25.6, 26.4, 0, 2.2, ch='K', material='metal')
box('intake-ne-shower-0', 17.0, 19.4, 25.6, 27.0, 0, 2.4, ch='K', material='metal')
box('intake-ne-bench', 30.5, 33.0, 23.0, 23.5, 0, 0.5, ch='o', los=False)
# NW Decon Wing (upper x -38..-16.4): the decon lock (gas, contract 5.4) between the concourse and the stair hall, a bypass
# vestibule to its east, the specimen freezers (dead end) to its west.
LOCK = (-31.6, -24.4, HALL1 + 0.4, CELL_ROW)
wall_z('decon-nw-wall-w', HALL1 + 0.4, CELL_ROW + 0.4, -32.0, -31.6, L1, ROOF - L1, [], ch='H')
wall_z('decon-nw-wall-e', HALL1 + 0.4, CELL_ROW + 0.4, -24.4, -24.0, L1, ROOF - L1, [], ch='H')
wall_x('decon-nw-wall-n', -BX, -24.0, CELL_ROW, CELL_ROW + 0.4, L1, ROOF - L1, [(-36.0, -33.6, 0, DOOR), (-29.2, -26.8, 0, DOOR)], ch='H')
box('decon-nw-freezer-0', -37.9, -36.6, 19.6, 24.6, L1, L1 + 2.4, ch='K', material='metal')
# NE Control Room (upper x 16.4..38): consoles facing the concourse windows, the supervisor desk.
for i, (cx, cz) in enumerate([(21.5, 21.0), (29.0, 21.0)]):
    box(f'control-ne-console-{i}', cx - 1.6, cx + 1.6, cz - 0.45, cz + 0.45, L1, L1 + 1.1, ch='o', material='metal', los=False)
box('control-ne-desk', 33.5, 36.5, 24.0, 25.0, L1, L1 + 1.1, ch='o', los=False)
box('control-ne-rack', 37.0, 37.9, 19.8, 23.8, L1, L1 + 2.4, ch='K', material='metal')
# North hall cover (ground): a gurney line and a crash cart; concourse (upper): benches and an observation console.
box('hall-n-gurney-0', -26.5, -24.3, 15.4, 16.3, 0, 1.0, role='cover', material='metal', ch='o', los=False, hp=60)
box('hall-n-cart-0', 12.0, 13.0, 17.6, 18.5, 0, 1.0, role='cover', material='metal', ch='o', los=False, hp=60)
box('concourse-n-bench-0', -27.0, -24.0, 15.0, 15.5, L1, L1 + 0.5, ch='o', los=False)
box('concourse-n-console', 22.5, 25.0, 15.0, 15.8, L1, L1 + 1.1, ch='o', material='metal', los=False)

# =====================================================================================================================
# EAST WING (x 16.4..38, z -14..14), point-mirrored to the west: the double-height Specimen Hall (x 16.4..30) with the
# gurney ramp up to the Specimen Bridge line (z 4..7.5 at the upper floor: atrium, platform, link, gantry), and the
# Incinerator Lane (x 30.6..38, burner rows, contract 5.3) under the East Gantry (x 34.4..38).
for side, z0, doors, windows in (('ne', 14.0, [(18.0, 20.4, 0, DOOR), (26.5, 28.9, 0, DOOR), (32.0, 35.0, 0, 4.0)], [(18.0, 22.0), (24.0, 28.5)]),
                                 ('se', -14.4, [(18.0, 20.4, 0, DOOR), (32.0, 35.0, 0, 4.0)], [(19.0, 23.0), (25.5, 29.5)])):
    ops = list(doors) + [(a, b, L1 + 1.0, L1 + 3.2) for a, b in windows] + [(34.4, BX, L1, L1 + 3.6)]
    wall_x(f'wing-tall-{side}', 16.4, BX, z0, z0 + 0.4, 0, ROOF, ops, ch='H')
LANE = 30.6
wall_z('lane-e-wall', -14.0, 14.0, LANE - 0.6, LANE, 0, 4.5, [(-7.5, -5.1, 0, DOOR), (5.1, 7.5, 0, DOOR), (-1.2, 1.2, 0, DOOR)], ch='H')
box('lane-e-furnace', 37.4, BX, -2.4, 2.4, 0, 3.6, ch='F', material='metal')                # the incinerator mouth (in the shell's face)
breach_panel('breach-lane-e', 'x', -12.0, BX, -1, 0)
breach_panel('breach-gantry-e', 'x', 2.0, BX, -1, L1)
# The East Gantry (upper floor along the shell, over the lane), its rail with the link gap and two drop gaps.
box('gantry-e-deck', 34.4, BX, -14.0, 14.0, L1 - 0.3, L1, ch='T', material='metal', los=False)
for i, (a, b) in enumerate([(-14.0, -6.5), (-4.5, 4.0), (7.5, 11.0), (13.0, 14.0)]):
    rail(f'gantry-e-rail-{i}', 34.4, 34.55, a, b, L1, ch='r', los=False, material='metal')
# Gurney ramp E: x 22.5..25.5, rises north from z -9.48 (floor) to z 4 (upper floor) at 24 degrees, onto the platform.
RZ0 = 4.0 - L1 / math.tan(math.radians(RAMP_DEG))
ramp_deg = gurney_ramp('ramp-e', 'z', RZ0, 4.0, 22.5, 25.5, 0, L1)
# Visual skirts closing the ramp's sides under the deck (collision is the wedge): stepped, each piece's top under the deck's
# underside at its low end, so nothing stands above the ramp.
for k in range(8):
    za, zb = (round(RZ0 + 0.6 + (4.0 - RZ0 - 0.6) * j / 8, 4) for j in (k, k + 1))
    top = round(max(0.3, (za - RZ0) * math.tan(math.radians(RAMP_DEG)) - 0.45), 3)
    box(f'ramp-e-skirt-w-{k}', 22.5, 22.65, za, zb, 0, top, ch='/', los=False, col=False)
    box(f'ramp-e-skirt-e-{k}', 25.35, 25.5, za, zb, 0, top, ch='/', los=False, col=False)
box('ramp-e-platform', 22.5, LANE, 4.0, 7.5, L1 - DECK, L1, ch='P', los=False)
box('bridge-e-specimen', 16.4, 22.5, 4.0, 7.5, L1 - DECK, L1, ch='P', los=False)
box('bridge-e-link', LANE, 34.4, 4.0, 7.5, L1 - 0.3, L1, ch='P', material='metal', los=False)
for i, (a, b) in enumerate([(16.4, 18.6), (20.6, 22.5)]):
    rail(f'bridge-e-specimen-rail-n-{i}', a, b, 7.35, 7.5, L1, ch='r', los=False)
rail('bridge-e-specimen-rail-s', 16.4, 22.5, 4.0, 4.15, L1, ch='r', los=False)
rail('ramp-e-platform-rail-n', 22.5, LANE, 7.35, 7.5, L1, ch='r', los=False)
rail('ramp-e-platform-rail-s', 25.5, LANE, 4.0, 4.15, L1, ch='r', los=False)
rail('bridge-e-link-rail-n', LANE, 34.4, 7.35, 7.5, L1, ch='r', los=False, material='metal')
rail('bridge-e-link-rail-s', LANE, 34.4, 4.0, 4.15, L1, ch='r', los=False, material='metal')
# Specimen tanks (tall cover), lab benches (low cover) and a sample cart.
for i, (cx, cz) in enumerate([(19.4, -7.0), (19.4, 10.8), (28.0, -3.0), (28.0, 11.0)]):
    box(f'lab-e-tank-{i}', cx - 1.0, cx + 1.0, cz - 1.0, cz + 1.0, 0, 3.0, ch='O', material='metal')
for i, (cx, cz, sx, sz) in enumerate([(19.5, -1.0, 1.0, 3.0), (27.8, -10.4, 2.6, 1.0), (19.6, 2.8, 1.0, 1.2)]):
    box(f'lab-e-bench-{i}', cx - sx / 2, cx + sx / 2, cz - sz / 2, cz + sz / 2, 0, 1.0, ch='o', material='metal', los=False)

# =====================================================================================================================
# PvE content for the map (contract section 6): entries (vent: floor / wall / ceiling; cell; door; drop), machines, pads, the
# boss's points. Every point carries y (the floor it stands on). Indices 0..7 stay the music-synced floor vents (`song`
# side 0/1, `pit`: they open into the shafts players fall into), as Ward Zero's eight are today.
entries = []
def entry(kind, id, frm, ext, surface=None, song=-1, pit=False, height=0.0, mirror=True, extra=None):
    def mk(i, f, e):
        dx, dz = e[0] - f[0], e[2] - f[2]
        d = dict(e=0, id=i, kind=kind, surface=surface or '', **{'from': v(*f), 'exit': v(*e)}, yaw=round(math.atan2(dx, dz), 6),
                 level=1 if e[1] > 3 else 0, song=song, pit=pit, height=height)
        if extra: d.update(json.loads(json.dumps(extra)))
        return d
    entries.append(mk(id, frm, ext))
    if mirror:
        m = mk(mirror_id(id), (-frm[0], frm[1], -frm[2]), (-ext[0], ext[1], -ext[2]))
        if song >= 0: m['song'] = 1 - song
        if 'block' in m: m['block'] = mirror_id(m['block']); m['center'] = v(-m['center']['x'], m['center']['y'], -m['center']['z']); m['normal'] = v(-m['normal']['x'], 0, -m['normal']['z'])
        entries.append(m)
def toward(f, t, dist):
    dx, dz = t[0] - f[0], t[1] - f[1]; n = math.hypot(dx, dz); return (f[0] + dx / n * dist, f[1] + dz / n * dist)
# Floor vents (ground, song + pit): the atrium four round the Cradle, the north hall, the specimen hall.
for name, at, song in (('vent-atrium-ne', (3.6, 6.4), 0), ('vent-atrium-nw', (-3.6, 6.4), 1), ('vent-hall-n', (-9.0, 16.7), 0), ('vent-lab-e', (21.0, -12.0), 1)):
    ex = toward(at, (0, 0) if 'atrium' in name else (at[0], 0) if 'hall' in name else (at[0], at[1] + 5), 1.2)
    entry('vent', name, (at[0], 0, at[1]), (round(ex[0], 4), 0, round(ex[1], 4)), surface='floor', song=song, pit=True)
# Floor vents (upper, duct grates: no pit, no song).
entry('vent', 'vent-concourse-n', (10.0, L1, 16.7), (10.0, L1, 15.5), surface='floor')
# Wall vents: grilles 0.5 m up in a wall; the add crawls out to a point 1.4 m in front.
entry('vent', 'vent-wall-morgue-nw', (-37.95, 0.5, 24.2), (-36.4, 0, 24.2), surface='wall')
entry('vent', 'vent-wall-lab-e', (29.95, 0.5, 3.0), (28.4, 0, 3.0), surface='wall')
entry('vent', 'vent-wall-control-ne', (37.95, L1 + 0.5, 26.0), (36.4, L1, 26.0), surface='wall')
entry('vent', 'vent-wall-concourse-n', (-14.0, L1 + 0.5, 18.95), (-14.0, L1, 17.4), surface='wall')
# Ceiling vents: the add drops out of a duct in the ceiling (height = ceiling over the floor).
entry('vent', 'vent-ceiling-hall-n', (24.0, L1 - DECK, 16.7), (24.0, 0, 16.7), surface='ceiling', height=L1 - DECK)
entry('vent', 'vent-ceiling-concourse-n', (-22.0, ROOF, 16.7), (-22.0, L1, 16.7), surface='ceiling', height=ROOF - L1)
# Cells (ground cell block: gates rise) and observation-cell doors (upper: leaves swing), four live of eight each.
for cx in (-14.0, -6.0, 6.0, 14.0):
    entry('cell', f'cell-n-{"w" if cx < 0 else "e"}-{abs(int(cx))}', (cx, 0, CELL_ROW + 0.6), (cx, 0, CELL_ROW - 1.6))
for cx in (-14.0, -2.0, 6.0, 14.0):
    entry('door', f'door-obs-n-{"w" if cx < 0 else "e"}-{abs(int(cx))}', (cx, L1, CELL_ROW + 0.6), (cx, L1, CELL_ROW - 1.6))
# Ward doors at the north hall's two ends (in the shell): leaves swing in.
entry('door', 'door-hall-ne', (BX + 0.6, 0, 16.7), (BX - 1.8, 0, 16.7))
entry('door', 'door-hall-nw', (-BX - 0.6, 0, 16.7), (-BX + 1.8, 0, 16.7))
# Drops: the Spine hatch over the Cradle (brutes and the Hulk, 13 m, a 4.5 m clear landing for the Hulk's shockwave) and the
# observation cells' roof hatches (brutes, 7 m), contract 6.4.
entry('drop', 'drop-cradle', (0, 0, 0), (0, 0, 0), height=ROOF, mirror=False, extra=dict(clear=4.5, hulk=True))
entry('drop', 'drop-obs-n', (0, L1, 22.2), (0, L1, 22.2), height=ROOF - L1, extra=dict(clear=2.8, hulk=False))
# Breaches (the Hulk): from behind the panel (inside the solid face), out to a point 2.4 m in front of it; the panel's box and
# its outward normal (into the play space). Collision off at breakAt, back at restoreAt (docs/SIREN-ZOMBIES.md).
for (bid, axis, u, face, sign, y0) in breaches:
    blk = next(b for b in blocks if b['id'] == MAP + '-' + bid); c, sz = blk['pos'], blk['size']
    n = (0, 0, sign) if axis == 'z' else (sign, 0, 0)
    frm = (u, y0, face - sign * 1.0) if axis == 'z' else (face - sign * 1.0, y0, u)
    ext = (u, y0, face + sign * (BREACH_T + 2.4)) if axis == 'z' else (face + sign * (BREACH_T + 2.4), y0, u)
    entry('breach', bid, frm, ext, surface='wall', extra=dict(block=MAP + '-' + bid, center=v(c['x'], c['y'], c['z']), size=v(sz['x'], sz['y'], sz['z']), normal=v(*n)))
# Order: the eight song vents first (0..7), then the rest by id; the index is the entry's `e`.
song = sorted([e for e in entries if e['song'] >= 0], key=lambda e: e['id']); rest = sorted([e for e in entries if e['song'] < 0], key=lambda e: e['id'])
entries = song + rest
for i, e in enumerate(entries): e['e'] = i
assert len({e['id'] for e in entries}) == len(entries), 'entry ids must be unique'

machines = []; machine_blocks = []
def machine(m, mid, kind, x, y, z, yaw_deg):
    a = math.radians(yaw_deg); s, c = math.sin(a), math.cos(a)
    def pt(d): return v(round(x + s * d, 4), y, round(z + c * d, 4))
    machines.append(dict(m=m, id=mid, kind=kind, x=x, y=y, z=z, yaw=round(a, 6), front=pt(0.7), dispense=pt(1.3), chipDrop=pt(2.7)))
    machine_blocks.append(dict(id=f'pve-machine-{m}', pos=v(round(x - s * 0.2, 4), y + 1.1, round(z - c * 0.2, 4)), size=v(1.3, 2.2, 0.9), material='foundation', rotation=qyaw(yaw_deg)))
MACH = [('chip', 29.45, 0, -11.6, -90), ('chip', -12.0, L1, 18.45, 180), ('recruit', -24.0, 0, 18.45, 180), ('med', 15.45, L1, -12.2, -90)]
# Ids as today: chip-0..3 (chips), chip-4/5 (recruits), med-0/1.
for i, (kind, x, y, z, yd) in enumerate(MACH):
    for k, (mx, mz, my) in enumerate(((x, z, yd), (-x, -z, yd + 180))):
        m = 2 * i + k; machine(m, f'med-{m - 6}' if kind == 'med' else f'chip-{m}', kind, mx, y, mz, (my + 180) % 360 - 180)
pads = [v(0, 0, -12.3), v(0, 0, 12.3), v(-14.2, L1, 0), v(14.2, L1, 0), v(-27.0, 0, -16.7), v(27.0, 0, 16.7), v(26.0, L1, -16.7), v(-26.0, L1, 16.7)]
boss = dict(spawn=v(0, 0, 0), yaw=180, rescue=v(0, 0, 0))
# Possession hide spots (contract 7.3): corners on both floors the possessed add prefers to drift to.
hide_spots = [v(-36.0, 0, 28.0), v(36.0, 0, -28.0), v(-34.0, L1, 21.0), v(34.0, L1, -21.0), v(35.0, 0, -12.5), v(-35.0, 0, 12.5),
              v(36.5, L1, 12.5), v(-36.5, L1, -12.5), v(10.5, 0, 4.0), v(-10.5, 0, -4.0), v(-14.5, L1, 23.0), v(14.5, L1, -23.0)]

# =====================================================================================================================
# Spawns (PvP / Sandbox). Team B: the NE Intake; team A: the SW Intake (mirror). FFA: both morgues, both intakes, both
# control rooms (upper), both specimen halls. y = floor + 1.1; yaw toward the Cradle (today's behaviour).
teamB = [(36.2, 0, 21.0), (29.5, 0, 21.4), (35.2, 0, 26.0), (21.5, 0, 23.6)]
teamA = [(-x, y, -z) for x, y, z in teamB]
ffa = [(-34.6, 0, 23.6), (34.6, 0, -23.6), teamA[0], teamB[0], (35.0, L1, 21.6), (-35.0, L1, -21.6), (24.0, 0, 10.6), (-24.0, 0, -10.6)]
ffa_names = ['F1', 'F2', 'A1', 'B1', 'F5', 'F6', 'F7', 'F8']
def spawn(x, y, z): return dict(pos=v(x, y + 1.1, z), yaw=round(math.degrees(math.atan2(-x, -z)) % 360, 3))

# =====================================================================================================================
# Hazards (contract section 5; MapHazard fields). All timing is a pure function of (round index, FightAt, time).
H = lambda **k: dict(dict(id='', kind='', center=v(), size=v(), axis=v(0, 0, 1), a=v(), b=v(), first=0, period=0, offset=0, warn=0, danger=0, speed=0, length=0,
                          charge=0, live=0, tick=0, damage=0, radius=0, start=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, push=0, lethal=False, alternate=False, zones=[]), **k)
def mv(p): return v(-p['x'], p['y'], -p['z'])
hazards = []
# 5.2 Live containment grating: the two atrium bridges (the existing `grid` kind: charge 2.5, live 2, 13 per .25 s tick).
hazards.append(H(id=MAP + '-grid-e', kind='grid', center=v(6.0, L1 + 0.5, 0), size=v(3.0, 1.0, 2 * VZ), first=8, period=16, offset=8, charge=2.5, live=2.0, tick=0.25, damage=13))
hazards.append(H(id=MAP + '-grid-w', kind='grid', center=v(-6.0, L1 + 0.5, 0), size=v(3.0, 1.0, 2 * VZ), first=16, period=16, offset=-8, charge=2.5, live=2.0, tick=0.25, damage=13))
# 5.3 Incinerator burners: four 2 m rows across each lane, alternate rows on two clocks (a, b) that never overlap.
def rows(zs, x0, x1): return [dict(center=v((x0 + x1) / 2, 1.75, z), size=v(x1 - x0, 3.5, 2.0)) for z in zs]
for side, sgn in (('e', 1), ('w', -1)):
    x0, x1 = (LANE, BX) if sgn > 0 else (-BX, -LANE)
    za, zb = ([-9.0, 3.0], [-3.0, 9.0]) if sgn > 0 else ([9.0, -3.0], [3.0, -9.0])
    hazards.append(H(id=f'{MAP}-burner-{side}-a', kind='burner', center=v((x0 + x1) / 2, 1.75, 0), size=v(x1 - x0, 3.5, 28.0), zones=rows(za, x0, x1), first=6, period=10, offset=5, warn=1.5, live=2.5, dwell=1.0, tick=0.25, damage=10))
    hazards.append(H(id=f'{MAP}-burner-{side}-b', kind='burner', center=v((x0 + x1) / 2, 1.75, 0), size=v(x1 - x0, 3.5, 28.0), zones=rows(zb, x0, x1), first=11, period=10, offset=-5, warn=1.5, live=2.5, dwell=1.0, tick=0.25, damage=10))
# 5.4 Decontamination gas: the two decon locks (upper NW, SE).
gas = H(id=MAP + '-gas-nw', kind='gas', center=v((LOCK[0] + LOCK[1]) / 2, L1 + 3.5, (LOCK[2] + LOCK[3]) / 2), size=v(LOCK[1] - LOCK[0], 7.0, LOCK[3] - LOCK[2]), first=15, period=24, offset=12, warn=2.5, live=4.0, dwell=3.0, tick=0.5, damage=6)
hazards.append(gas); hazards.append(dict(gas, id=MAP + '-gas-se', center=mv(gas['center']), first=27, offset=-12))
# 5.5 Blast shutters: the atrium's four portals, in pairs (N+S, E+W), never both pairs at once.
for side, c, s, first, off in (('n', v(0, 2.25, 14.2), v(2 * PORTAL, 4.5, 0.4), 20, 18), ('e', v(16.2, 2.25, 0), v(0.4, 4.5, 2 * PORTAL), 38, -18)):
    hazards.append(H(id=f'{MAP}-shutter-{side}', kind='shutter', center=c, size=s, first=first, period=36, offset=off, warn=3.0, travel=1.2, dwell=9.0, rise=1.5, push=6.0))
    hazards.append(H(id=f'{MAP}-shutter-{SWAP[side]}', kind='shutter', center=mv(c), size=s, first=first, period=36, offset=off, warn=3.0, travel=1.2, dwell=9.0, rise=1.5, push=6.0))
hazards.sort(key=lambda h: h['id'])

# Nav hints for Astra's generator (contract section 7): one-way drop links (an edge a pilot or add can step off to the floor
# below), and the safe pockets of each hazard.
drops = []
def drop_hint(id, at, to, mirror=True):
    drops.append(dict(id=MAP + '-' + id, a=v(*at), b=v(*to)))
    if mirror: drops.append(dict(id=MAP + '-' + mirror_id(id), a=v(-at[0], at[1], -at[2]), b=v(-to[0], to[1], -to[2])))
drop_hint('drop-gallery-n', (0, L1, 10.6), (0, 0, 8.6))
drop_hint('drop-gallery-e', (12.6, L1, 7.5), (10.4, 0, 7.5))
drop_hint('drop-bridge-e', (5.1, L1, 0), (3.2, 0, 0))
drop_hint('drop-gantry-e-s', (34.9, L1, -5.5), (33.0, 0, -5.5))
drop_hint('drop-gantry-e-n', (34.9, L1, 12.0), (33.0, 0, 12.0))
drop_hint('drop-specimen-e', (19.6, L1, 7.0), (19.6, 0, 8.6))
safe = []
def safe_hint(hz, at):
    safe.append(dict(hazard=MAP + '-' + hz, at=v(*at)))
for z in (-12.5, -6.0, 0.0, 6.0, 12.5): safe_hint('burner-e-a', (34.3, 0, z)); safe_hint('burner-e-b', (34.3, 0, z))
for z in (12.5, 6.0, 0.0, -6.0, -12.5): safe_hint('burner-w-a', (-34.3, 0, z)); safe_hint('burner-w-b', (-34.3, 0, z))

# =====================================================================================================================
# Pure hazard timing (the reference the contract pins; Core NativeWardZeroHazards mirrors it).
def cycle(h, rnd, fight_at, t):
    u = t - fight_at - h['first'] - (h['offset'] if rnd % 2 == 0 else 0)
    if u < 0: return -1, u
    k = int(u // h['period']); return k, u - k * h['period']
def phase(h, rnd, fight_at, t):
    if h['kind'] == 'grid':
        u = t - fight_at - h['first'] - (h['offset'] if rnd % 2 == 0 else 0)
        if u < 0: return 'idle'
        c = u % h['period']; idle = h['period'] - h['charge'] - h['live'] - 1.0
        return 'idle' if c < idle else 'warn' if c < idle + h['charge'] else 'active' if c < idle + h['charge'] + h['live'] else 'clear'
    k, u = cycle(h, rnd, fight_at, t)
    if k < 0: return 'idle'
    if h['kind'] == 'shutter':
        marks = [('warn', h['warn']), ('closing', h['travel']), ('closed', h['dwell']), ('opening', h['rise'])]
    else:
        marks = [('warn', h['warn']), ('active', h['live']), ('clear', h['dwell'])]
    for name, d in marks:
        if u < d: return name
        u -= d
    return 'idle'

# =====================================================================================================================
# CHECKS
def rect_xz(b):
    p, s = b['pos'], b['size']; return (p['x'] - s['x'] / 2, p['x'] + s['x'] / 2, p['z'] - s['z'] / 2, p['z'] + s['z'] / 2)
def yspan(b):
    if '_ys' in b: return b['_ys']
    b['_ys'] = _yspan(b); return b['_ys']
def _yspan(b):
    p, s = b['pos'], b['size']
    r = b['rotation']
    if abs(r['x']) > 1e-6 or abs(r['z']) > 1e-6:
        # a pitched block: its world y extent
        ext = abs(rot_vec(r, (s['x'] / 2, 0, 0))[1]) + abs(rot_vec(r, (0, s['y'] / 2, 0))[1]) + abs(rot_vec(r, (0, 0, s['z'] / 2))[1])
        return p['y'] - ext, p['y'] + ext
    return p['y'] - s['y'] / 2, p['y'] + s['y'] / 2
def rot_vec(r, p):
    x, y, z = p; qx, qy, qz, qw = r['x'], r['y'], r['z'], r['w']
    tx, ty, tz = 2 * (qy * z - qz * y), 2 * (qz * x - qx * z), 2 * (qx * y - qy * x)
    return (x + qw * tx + (qy * tz - qz * ty), y + qw * ty + (qz * tx - qx * tz), z + qw * tz + (qx * ty - qy * tx))
def yaw_only(b): r = b['rotation']; return abs(r['x']) < 1e-6 and abs(r['z']) < 1e-6
def local_xz(b, x, z):
    """(x, z) in a yaw-rotated block's frame (relative to its centre)."""
    r = b['rotation']; dx, dz = x - b['pos']['x'], z - b['pos']['z']
    ang = 2 * math.atan2(r['y'], r['w'])            # yaw (rad), +y rotation
    c, s = math.cos(-ang), math.sin(-ang)
    return (dx * c + dz * s, -dx * s + dz * c)        # inverse rotation about y (Unity: +yaw turns +z toward +x)
def dist_xz(b, x, z):
    """Distance from (x, z) to the block's footprint (yaw-rotated boxes too)."""
    lx, lz = local_xz(b, x, z); hx, hz = b['size']['x'] / 2, b['size']['z'] / 2
    return math.hypot(max(abs(lx) - hx, 0), max(abs(lz) - hz, 0))

# --- collision as Astra will build it: authored blocks (col True) plus the closed wedges of `ramps`.
solid = [b for b in blocks if b['_col'] and yaw_only(b)]
def wedge_height(r, x, z):
    """The wedge's top at (x, z), or None outside its footprint (along its run, within half its width)."""
    lx, lz = r['low']['x'], r['low']['z']; hx, hz = r['high']['x'], r['high']['z']
    dx, dz = hx - lx, hz - lz; L = math.hypot(dx, dz); ux, uz = dx / L, dz / L
    t = ((x - lx) * ux + (z - lz) * uz); w = (-(x - lx) * uz + (z - lz) * ux)
    if t < 0 or t > L or abs(w) > r['width'] / 2: return None
    return r['low']['y'] + (r['high']['y'] - r['low']['y']) * t / L
CELL = 0.5
NX, NZ = int(2 * BX / CELL), int(2 * BZ / CELL)
def cell_xz(i, j): return (-BX + (i + 0.5) * CELL, -BZ + (j + 0.5) * CELL)
BUCKET = 4.0
buckets = {}
for b in solid:
    x0, x1, z0, z1 = rect_xz(b) if yaw_only(b) and abs(b['rotation']['y']) < 1e-6 else (b['pos']['x'] - max(b['size']['x'], b['size']['z']), b['pos']['x'] + max(b['size']['x'], b['size']['z']), b['pos']['z'] - max(b['size']['x'], b['size']['z']), b['pos']['z'] + max(b['size']['x'], b['size']['z']))
    for bi in range(int(math.floor((x0 - 1) / BUCKET)), int(math.floor((x1 + 1) / BUCKET)) + 1):
        for bj in range(int(math.floor((z0 - 1) / BUCKET)), int(math.floor((z1 + 1) / BUCKET)) + 1):
            buckets.setdefault((bi, bj), []).append(b)
def near_blocks(x, z): return buckets.get((int(math.floor(x / BUCKET)), int(math.floor(z / BUCKET))), [])
RADIUS, HEIGHT, STEP = 0.42, 1.7, 0.35
def surfaces(x, z):
    """Candidate floor heights at (x, z): tops of solid blocks whose footprint holds the point, and wedge tops."""
    out = set()
    for b in near_blocks(x, z):
        if dist_xz(b, x, z) <= 1e-9:
            y0, y1 = yspan(b)
            if y1 - y0 > 0.02: out.add(round(y1, 3))
    for r in ramps:
        h = wedge_height(r, x, z)
        if h is not None: out.add(round(h, 3))
    return sorted(out)
def blocked(x, z, y):
    """Whether a pilot capsule standing at (x, y, z) overlaps anything (above a step's height off its feet)."""
    lo, hi = y + STEP + 0.01, y + HEIGHT + 0.05
    for b in near_blocks(x, z):
        if dist_xz(b, x, z) < RADIUS:
            y0, y1 = yspan(b)
            if y0 < hi and y1 > lo: return True
    for r in ramps:
        # the wedge under the capsule's rim: its top within the radius, above the feet by more than a step
        for ox, oz in ((RADIUS, 0), (-RADIUS, 0), (0, RADIUS), (0, -RADIUS)):
            h = wedge_height(r, x + ox, z + oz)
            if h is not None and h > lo: return True
    return False
graph = {}                     # (i, j, y) -> standable
def build_graph():
    for i in range(NX):
        for j in range(NZ):
            x, z = cell_xz(i, j)
            for y in surfaces(x, z):
                if y < -0.01 or y > ROOF - 1: continue
                if not blocked(x, z, y): graph[(i, j, y)] = True
def neighbours(n):
    i, j, y = n
    for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        for (a, b, yy) in by_cell.get((i + di, j + dj), ()):
            if abs(yy - y) <= STEP + 1e-6: yield (a, b, yy)
def flood(starts, allowed=None):
    seen = set(); stack = list(starts)
    while stack:
        n = stack.pop()
        if n in seen or n not in graph or (allowed is not None and not allowed(n)): continue
        seen.add(n); stack.extend(neighbours(n))
    return seen
def node_at(x, y, z, tol=0.6):
    """The standable node nearest (x, z) at floor y (within tol of its cell and 0.3 m of the height)."""
    best = None
    ci, cj = int((x + BX) / CELL), int((z + BZ) / CELL)
    for di in range(-2, 3):
        for dj in range(-2, 3):
            for n in by_cell.get((ci + di, cj + dj), ()):
                if abs(n[2] - y) > 0.3: continue
                cx, cz = cell_xz(n[0], n[1]); d = math.hypot(cx - x, cz - z)
                if d <= tol and (best is None or d < best[0]): best = (d, n)
    return best[1] if best else None

def seg_box(a, c, b):
    """Whether the 3D segment a->c passes through block b (sight blocker: _los blocks; yaw-rotated by their frame)."""
    if not b['_los']: return False
    y0, y1 = yspan(b)
    la = local_xz(b, a[0], a[2]); lc = local_xz(b, c[0], c[2])
    hx, hz = b['size']['x'] / 2, b['size']['z'] / 2
    t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((la[0], lc[0] - la[0], -hx, hx), (a[1], c[1] - a[1], y0, y1), (la[1], lc[1] - la[1], -hz, hz)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
los_blocks = [b for b in blocks if b['_los']]
def visible(a, c): return not any(seg_box(a, c, b) for b in los_blocks)
def los_report(pairs, label, out):
    bad = []
    for (na, a), (nb, c) in pairs:
        n = 0
        for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4):
            for eye in (1.2, 1.6, 1.9):
                if visible((a[0] + dx, a[1] + eye, a[2] + dz), (c[0] + ex, c[1] + eye, c[2] + ez)): n += 1
        if n: bad.append(f'{na}-{nb} ({n}/243 open, {math.dist(a, c):.1f} m)')
    out.append(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights' + ''.join('\n   OPEN ' + s for s in bad))
    return not bad

by_cell = {}
def checks():
    out, ok = [], True
    build_graph()
    for n in graph: by_cell.setdefault((n[0], n[1]), []).append(n)
    # --- reachability: flood from every spawn; every floor cell (y 0 and the upper floor) must be reached.
    starts = []
    for x, y, z in ffa + teamA + teamB:
        n = node_at(x, y, z); assert n, f'spawn {x},{z} not on walkable floor'; starts.append(n)
    reach = flood(starts)
    def is_floor(n, y): return abs(n[2] - y) < 0.05
    for lvl, y in (('ground', 0.0), ('upper', L1)):
        tot = [n for n in graph if is_floor(n, y)]; got = [n for n in tot if n in reach]
        out.append(f'{lvl} floor (y {y:g}): {len(tot)} standable 0.5 m cells, {len(got)} reached from the spawns ({100 * len(got) / max(1, len(tot)):.1f}%), {len(tot) * CELL * CELL:.0f} m2')
        if len(got) != len(tot):
            ok = False; miss = [n for n in tot if n not in reach]
            groups = []; left = set(miss)
            while left:
                s = left.pop(); comp = flood([s], allowed=lambda m: m in left or m == s); left -= comp
                xs = [cell_xz(m[0], m[1]) for m in comp]; groups.append((len(comp), sum(p[0] for p in xs) / len(xs), sum(p[1] for p in xs) / len(xs)))
            for g in sorted(groups, reverse=True)[:12]: out.append('   UNREACHED %d cells round (%.1f, %.1f)' % g)
    ramp_cells = [n for n in reach if not is_floor(n, 0) and not is_floor(n, L1)]
    out.append(f'stair and ramp cells reached: {sum(1 for n in ramp_cells if any(wedge_height(r, *cell_xz(n[0], n[1])) is not None for r in ramps))}; other raised surfaces walked onto (cover tops): {sum(1 for n in ramp_cells if all(wedge_height(r, *cell_xz(n[0], n[1])) is None for r in ramps))}')
    # --- each route between the floors on its own: from its foot, using only the wedge and 2 m round its ends, reach its top.
    for r in ramps:
        lo, hi = r['low'], r['high']; L = math.hypot(hi['x'] - lo['x'], hi['z'] - lo['z']); ux, uz = (hi['x'] - lo['x']) / L, (hi['z'] - lo['z']) / L
        foot = node_at(lo['x'] - ux * 1.0, lo['y'], lo['z'] - uz * 1.0, 0.8); top = node_at(hi['x'] + ux * 1.0, hi['y'], hi['z'] + uz * 1.0, 0.8)
        def near(n, r=r, ux=ux, uz=uz, L=L):
            x, z = cell_xz(n[0], n[1]); t = (x - r['low']['x']) * ux + (z - r['low']['z']) * uz; w = -(x - r['low']['x']) * uz + (z - r['low']['z']) * ux
            return -2.5 <= t <= L + 2.5 and abs(w) <= r['width'] / 2 + 1.0
        both = foot is not None and top is not None and top in flood([foot], allowed=near) and foot in reach and top in reach
        ang = math.degrees(math.atan2(hi['y'] - lo['y'], L))
        out.append(f"route {r['id']} ({r['kind']}, {r['width']:.1f} m wide, {L:.1f} m run, {ang:.1f} deg): foot to top on its own: {'yes' if both else 'NO'}")
        ok &= both and ang <= 30.0 + 1e-6
    # --- entries, machines, pads, hide spots and the boss on reached floor (each at its own level).
    def on_floor(label, pts):
        bad = [f'{n} ({x:.1f}, {y:g}, {z:.1f})' for n, (x, y, z) in pts if (lambda nd: nd is None or nd not in reach)(node_at(x, y, z))]
        out.append(f'{label}: {len(pts)} points on reached floor at their own level: {len(pts) - len(bad)}' + ''.join('\n   OFF ' + s for s in bad))
        return not bad
    ok &= on_floor('entry exits', [(e['id'], (e['exit']['x'], e['exit']['y'], e['exit']['z'])) for e in entries])
    ok &= on_floor('machine fronts', [(m['id'], (m['front']['x'], m['front']['y'], m['front']['z'])) for m in machines])
    ok &= on_floor('chip drops', [(m['id'], (m['chipDrop']['x'], m['chipDrop']['y'], m['chipDrop']['z'])) for m in machines if m['kind'] == 'chip'])
    ok &= on_floor('pads', [(f'pad{i}', (p['x'], p['y'], p['z'])) for i, p in enumerate(pads)])
    ok &= on_floor('hide spots', [(f'hide{i}', (p['x'], p['y'], p['z'])) for i, p in enumerate(hide_spots)])
    ok &= on_floor('boss spawn', [('boss', (boss['spawn']['x'], boss['spawn']['y'], boss['spawn']['z']))])
    ok &= on_floor('drop hint landings', [(d['id'], (d['b']['x'], d['b']['y'], d['b']['z'])) for d in drops]) and on_floor('drop hint edges', [(d['id'], (d['a']['x'], d['a']['y'], d['a']['z'])) for d in drops])
    ok &= on_floor('hazard safe pockets', [(s['hazard'], (s['at']['x'], s['at']['y'], s['at']['z'])) for s in safe])
    lv = [sum(1 for e in entries if e['level'] == l) for l in (0, 1)]
    kinds = {}
    for e in entries: kinds[(e['kind'], e['surface'])] = kinds.get((e['kind'], e['surface']), 0) + 1
    out.append(f'entries: {len(entries)} ({lv[0]} ground, {lv[1]} upper): ' + ', '.join(f'{k}{"/" + s if s else ""} {n}' for (k, s), n in sorted(kinds.items())) + f'; ids unique: {len({e["id"] for e in entries}) == len(entries)}; song vents 0..7: {[e["id"] for e in entries[:8]]}')
    ok &= all(e['song'] in (0, 1) and e['pit'] and e['surface'] == 'floor' and e['level'] == 0 for e in entries[:8]) and all(e['song'] < 0 for e in entries[8:])
    # Drop entries: a clear disc round each landing (2.4 m, the slam's radius) on reached floor, and headroom up to the hatch.
    for e in entries:
        if e['kind'] != 'drop': continue
        x, y, z = e['exit']['x'], e['exit']['y'], e['exit']['z']; clear = min((dist_xz(b, x, z) for b in solid if yspan(b)[1] > y + 0.05 and yspan(b)[0] < y + e['height'] - 0.05), default=99)
        out.append(f"drop {e['id']}: landing ({x:g}, {y:g}, {z:g}), fall {e['height']:g} m, clear radius {e['clear']:g} m{' (Hulk)' if e['hulk'] else ''}, nearest obstruction under the hatch {clear:.1f} m"); ok &= clear >= e['clear']
    # --- spawns: sight lines (3D), spacing, clearances, hazards.
    named = list(zip(ffa_names, ffa))
    ok &= los_report(list(itertools.combinations(named, 2)), 'FFA', out)
    A = [(f'A{i + 1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i + 1}', p) for i, p in enumerate(teamB)]
    ok &= los_report([(p, c) for p in A for c in B], 'Teams A-B', out)
    sp = ffa + teamA[1:] + teamB[1:]
    out.append('min FFA spacing on one floor %.1f m (across floors, through a deck, %.1f m); min A-B spacing %.1f m' % (min(math.dist(p, c) for p, c in itertools.combinations(ffa, 2) if p[1] == c[1]), min(math.dist(p, c) for p, c in itertools.combinations(ffa, 2) if p[1] != c[1]), min(math.dist(p, c) for p in teamA for c in teamB)))
    def haz_boxes():
        for h in hazards:
            vols = h['zones'] or [dict(center=h['center'], size=h['size'])]
            for vz in vols: yield h['id'], vz['center'], vz['size']
    def dist_box(p, c, s):
        return math.sqrt(sum(max(abs(p[k] - c[a]) - s[a] / 2, 0) ** 2 for k, a in ((0, 'x'), (1, 'y'), (2, 'z'))))
    same = [(math.hypot(max(abs(x - c['x']) - s['x'] / 2, 0), max(abs(z - c['z']) - s['z'] / 2, 0)), hid, (x, y, z)) for (x, y, z) in sp for hid, c, s in haz_boxes() if c['y'] - s['y'] / 2 < y + 2.0 and c['y'] + s['y'] / 2 > y]
    worst = min(same)
    out.append('closest spawn to a hazard volume on its own floor: %.1f m (%s, spawn %s)' % worst); ok &= worst[0] >= 6.0
    clr = min((min(dist_xz(b, x, z) for b in solid if yspan(b)[1] > y + 0.3 and yspan(b)[0] < y + 2.2), (x, y, z)) for (x, y, z) in sp)
    out.append('closest spawn to a block it could touch: %.1f m at %s' % clr); ok &= clr[0] >= 1.5
    out.append('all spawns inside the play bounds less 1.5 m: %s' % all(abs(x) <= BX - 1.5 and abs(z) <= BZ - 1.5 for x, _, z in sp))
    # --- hazards: shutters never close both pairs (two of four portals always open; corner doors never close); burners'
    # two clocks never overlap per lane; grids alternate; the gas lock always has its bypass (the vestibule door).
    for rnd in (1, 2):
        worst_closed = 0; overlap = 0; both_grids = 0
        for k in range(0, 2400):
            t = 10 + k * 0.1
            closed = sum(1 for h in hazards if h['kind'] == 'shutter' and phase(h, rnd, 10, t) in ('closing', 'closed', 'opening'))
            worst_closed = max(worst_closed, closed)
            for side in ('e', 'w'):
                pa, pb = [phase(h, rnd, 10, t) for h in hazards if h['id'] in (f'{MAP}-burner-{side}-a', f'{MAP}-burner-{side}-b')]
                if pa in ('warn', 'active') and pb in ('warn', 'active'): overlap += 1
            if all(phase(h, rnd, 10, t) == 'active' for h in hazards if h['kind'] == 'grid'): both_grids += 1
        out.append(f'round {rnd}: at most {worst_closed} of 4 portals shut at once; burner clocks overlapping {overlap} samples; both bridges live at once {both_grids} samples')
        ok &= worst_closed <= 2 and overlap == 0 and both_grids == 0
    # Burner lanes: the farthest lane point from a safe strip (between rows or at the ends), and its time at 6.8 m/s.
    edges = sorted({z for h in hazards if h['kind'] == 'burner' for zz in h['zones'] for z in (zz['center']['z'] - 1.0, zz['center']['z'] + 1.0)})
    worst_lane = max(min(abs(z - e) for e in edges) for z in [x * 0.1 for x in range(-140, 141)] if any(abs(z - zz['center']['z']) <= 1.0 for h in hazards if h['kind'] == 'burner' for zz in h['zones']))
    out.append(f'burner rows: 2 m strips; the farthest point inside a row from its edge {worst_lane:.1f} m ({worst_lane / 6.8:.2f} s at 6.8 m/s against a 1.5 s warning)')
    ok &= worst_lane / 6.8 < 1.5
    # Gas: the farthest point of the lock from its two doors (2.4 m) and the time out at 6.8 m/s against the 2.5 s warning.
    far = max(min(math.hypot(x - dx, z - dz) for dx, dz in ((-28.0, HALL1 + 0.4), (-28.0, CELL_ROW))) for x in [LOCK[0] + 0.4 + i * 0.2 for i in range(33)] for z in [LOCK[2] + 0.4 + j * 0.2 for j in range(24)])
    out.append(f'decon lock: farthest point {far:.1f} m from a door ({far / 6.8:.2f} s at 6.8 m/s against a 2.5 s warning); bypass: the vestibule door at x -20.2..-17.8')
    ok &= far / 6.8 < 2.5
    # Grid bridges: from the bridge's middle to an end or the drop gap, against the 2.5 s charge.
    out.append(f'grid bridges: 20 m; the farthest point from an end or the 2.4 m drop gap is 4.4 m ({4.4 / 6.8:.2f} s against a 2.5 s charge)')
    ids = [b['id'] for b in blocks]; dup = sorted({i for i in ids if ids.count(i) > 1})
    out.append('blocks %d (static %d, cover %d), unique ids: %s; collision as authored %d, visual-only (stairs and ramp slabs, collision by wedge) %d; rotated %d' % (
        len(blocks), sum(1 for b in blocks if b['role'] == 'static'), sum(1 for b in blocks if b['role'] == 'cover'), not dup, sum(1 for b in blocks if b['_col']), sum(1 for b in blocks if not b['_col']),
        sum(1 for b in blocks if b['rotation'] != q())))
    ok &= not dup
    legacy = {'siren-floor-1', 'siren-roof-6'} | {f'siren-{k}-{i}' for k in ('wall', 'column', 'pylon', 'partition', 'cell', 'cabinet', 'gurney', 'rubble') for i in range(2, 73)}
    clash = sorted(set(ids) & legacy); out.append(f'ids clear of the current Ward Zero blocks: {not clash}'); ok &= not clash
    out.append('initial prop frame (non-static blocks): %d' % sum(1 for b in blocks if b['role'] != 'static'))
    # Hazard timing tables (round 1, fight at 10 s).
    for h in hazards:
        if h['id'].endswith(('-w', '-w-a', '-w-b', '-se', '-s')): continue
        ts = []; last = None
        for k in range(0, 900):
            t = 10 + k * 0.1; p = phase(h, 1, 10, t)
            if p != last: ts.append(f'{p} {t:.1f}'); last = p
            if len(ts) > 7: break
        out.append(f"{h['id']} ({h['kind']}, round 1, fight at 10 s): " + ', '.join(ts))
    return ok, out

# =====================================================================================================================
# OUTPUTS
def clean_block(b): return {k: b[k] for k in ('id', 'material', 'pos', 'size', 'rotation', 'role', 'mass', 'segment', 'hp')}
def draft():
    cb = [clean_block(b) for b in blocks]
    return dict(schema=1, draft=True, note='Draft blockout from docs/arenas/scripts/wardzero_m1.py for Astra; not game content. Replaces the current Ward Zero (map id siren) once imported.',
                materials=[dict(id='stone', color=0x5A5F6E, mass=26, durability=85, friction=0.85, indestructible=False)],
                maps=[dict(id=MAP, name='Ward Zero', tag='Meridian Biocontainment, Ward Zero. Where she got out.', theme=MAP, music=None, ambience='wardzero',
                           look='quarantine', sky=0x040A10, floor=0x2A3036, accent=0xE9E4F2, rotation=False,
                           bounds=dict(minX=-BX, maxX=BX, minZ=-BZ, maxZ=BZ, ceiling=ROOF, killY=-6),
                           spawns=dict(ffa=[spawn(*p) for p in ffa], teams=[dict(side=0, points=[spawn(*p) for p in teamA]), dict(side=1, points=[spawn(*p) for p in teamB])]),
                           blocks=cb, segments=[], hazards=hazards, pilotBlocks=[], nav=dict(nodes=[], links=[]),
                           ramps=ramps, navHints=dict(drops=sorted(drops, key=lambda d: d['id']), safe=safe, collisionOff=sorted(b['id'] for b in blocks if not b['_col'])))],
                pve=dict(entries=entries, machines=machines, machineBlocks=machine_blocks, pads=pads, boss=boss, hideSpots=hide_spots))

def section(axis, at, lo, hi, top=14.4, bottom=-0.6, step=0.6):
    """A vertical slice at x = at (axis 'x': columns along z) or z = at (axis 'z': columns along x)."""
    rows = []; y = top
    while y > bottom - 1e-6:
        line = ''
        for c in range(lo, hi):
            ch = ' '
            for b in blocks:
                if not yaw_only(b) and b['_ch'] != '/': continue
                x0, x1, z0, z1 = rect_xz(b); y0, y1 = yspan(b)
                hit = (x0 <= at <= x1 and z0 < c + 1 and z1 > c) if axis == 'x' else (z0 <= at <= z1 and x0 < c + 1 and x1 > c)
                if hit and y0 < y and y1 > y - step and (ch == ' ' or ch == '.'): ch = b['_ch']
            for r in ramps:
                h = wedge_height(r, at, c + .5) if axis == 'x' else wedge_height(r, c + .5, at)
                if h is not None and r['bottom'] < y and h > y - step and ch in ' .': ch = '/'
            line += ch
        rows.append(f'{y:5.1f} ' + line); y -= step
    return '\n'.join(rows)
def ascii_map(level):
    """Top-down plan of one floor (1 char = 1 m of x, 2 m of z): what stands on that floor up to 3 m above it."""
    W, Rn = 2 * SX, SZ
    yf = 0.0 if level == 0 else L1
    g = [[' '] * W for _ in range(Rn)]
    def put(x, z, ch):
        c, r = int(math.floor(x + SX)), int((SZ - z) // 2)
        if 0 <= c < W and 0 <= r < Rn: g[r][c] = ch
    for r in range(Rn):
        for c in range(W):
            x, z = c - SX + .5, SZ - 2 * r - 1
            if level == 1:
                n = node_at(x, L1, z, 0.8)
                g[r][c] = '.' if n is not None else ' '
            else: g[r][c] = '.'
    for b in sorted(blocks, key=lambda b: yspan(b)[1]):
        y0, y1 = yspan(b)
        if b['id'] in (MAP + '-slab', MAP + '-vault'): continue
        if level == 0 and not (y0 < 3.0 and y1 > 0.05): continue
        if level == 1 and not (y0 < L1 + 3.0 and y1 > L1 + 0.05): continue
        ch = b['_ch']
        for zz in [k * 0.5 for k in range(-64, 64)]:
            for xx in [k * 0.5 for k in range(-80, 80)]:
                if dist_xz(b, xx + .25, zz + .25) <= 1e-9: put(xx + .25, zz + .25, ch)
    for rr in ramps:
        for zz in [k * 0.5 for k in range(-64, 64)]:
            for xx in [k * 0.5 for k in range(-80, 80)]:
                h = wedge_height(rr, xx + .25, zz + .25)
                if h is not None and (level == 0 and h < 3 or level == 1 and h >= 3): put(xx + .25, zz + .25, '/')
    if level == 1:
        for h in hazards:
            if h['kind'] in ('grid', 'gas'):
                c, s = h['center'], h['size']
                for zz in [k * 0.5 for k in range(-64, 64)]:
                    for xx in [k * 0.5 for k in range(-80, 80)]:
                        if abs(xx + .25 - c['x']) < s['x'] / 2 and abs(zz + .25 - c['z']) < s['z'] / 2 and g[int((SZ - zz - .25) // 2)][int(math.floor(xx + .25 + SX))] in '.g': put(xx + .25, zz + .25, 'g' if h['kind'] == 'grid' else '%')
    else:
        for h in hazards:
            for zv in h['zones']:
                c, s = zv['center'], zv['size']
                for zz in [k * 0.5 for k in range(-64, 64)]:
                    for xx in [k * 0.5 for k in range(-80, 80)]:
                        if abs(xx + .25 - c['x']) < s['x'] / 2 and abs(zz + .25 - c['z']) < s['z'] / 2: put(xx + .25, zz + .25, '~')
            if h['kind'] == 'shutter':
                c, s = h['center'], h['size']
                for zz in [k * 0.5 for k in range(-64, 64)]:
                    for xx in [k * 0.5 for k in range(-80, 80)]:
                        if abs(xx + .25 - c['x']) < s['x'] / 2 + .1 and abs(zz + .25 - c['z']) < s['z'] / 2 + .1: put(xx + .25, zz + .25, '=')
    for e in entries:
        if e['level'] != level: continue
        ch = {'vent': {'floor': 'v', 'wall': 'w', 'ceiling': 'u'}.get(e['surface'], 'v'), 'cell': 'c', 'door': 'd', 'drop': 'X', 'breach': 'b'}[e['kind']]
        put(e['from']['x'] if e['kind'] != 'door' or abs(e['from']['x']) < BX else e['exit']['x'], e['from']['z'], ch)
    for m in machines:
        if (m['y'] > 3) == (level == 1): put(m['x'], m['z'], {'chip': '$', 'recruit': 'R', 'med': '+'}[m['kind']])
    for p in pads:
        if (p['y'] > 3) == (level == 1): put(p['x'], p['z'], 'p')
    for i, (x, y, z) in enumerate(teamA):
        if (y > 3) == (level == 1): put(x, z, str(i + 1))
    for i, (x, y, z) in enumerate(teamB):
        if (y > 3) == (level == 1): put(x, z, str(i + 5))
    for nm, (x, y, z) in zip(ffa_names, ffa):
        if nm not in ('A1', 'B1') and (y > 3) == (level == 1): put(x, z, nm[1])
    out = ['          x=-40' + ' ' * 34 + 'x=0' + ' ' * 35 + 'x=+39']
    for r in range(Rn):
        z = SZ - 2 * r; out.append(f'{z - 2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

GLYPHS = dict(A='010101111101101', B='110101110101110', C='011100100100011', D='110101101101110', E='111100110100111', F='111100110100100',
              G='011100101101011', H='101101111101101', I='111010010010111', K='101101110101101', L='100100100100111', M='101111111101101',
              N='110101101101101', O='010101101101010', P='110101110100100', R='110101110101101', S='011100010001110', T='111010010010010',
              U='101101101101111', V='101101101101010', W='101101111111101', X='101101010101101', Y='101101010010010', Z='111001010100111')
GLYPHS.update({'0': '111101101101111', '1': '010110010010111', '2': '110001010100111', '3': '110001010001110', '4': '101101111001001',
               '5': '111100110001110', '6': '011100111101111', '7': '111001010010010', '8': '111101111101111', '9': '111101111001110',
               '-': '000000111000000', '+': '000010111010000', '.': '000000000000010', ':': '000010000010000', '/': '001001010100100', ' ': '0' * 15})
def png(path, scale=7):
    """Both floors side by side (ground left, upper right), north up."""
    PW, PH = 2 * SX * scale, 2 * SZ * scale; W, H = 2 * PW + 30, PH + 60
    img = bytearray([14, 16, 20] * W * H)
    def px(x, y, c):
        if 0 <= x < W and 0 <= y < H: img[(y * W + x) * 3:(y * W + x) * 3 + 3] = bytes(c)
    def text(x, y, s, c, k=2):
        for ch in s.upper():
            bits = GLYPHS.get(ch, '0' * 15)
            for r in range(5):
                for col in range(3):
                    if bits[r * 3 + col] == '1':
                        for a in range(k):
                            for b in range(k): px(x + col * k + a, y + r * k + b, c)
            x += 4 * k
    for panel, level in ((0, 0), (1, 1)):
        ox = 10 + panel * (PW + 10); oy = 40; yf = 0.0 if level == 0 else L1
        def X(x): return int(round((x + SX) * scale)) + ox
        def Y(z): return int(round((SZ - z) * scale)) + oy
        def fill(x0, x1, z0, z1, c, alpha=1.0):
            for yy in range(Y(z1), Y(z0)):
                for xx in range(X(x0), X(x1)):
                    if 0 <= xx < W and 0 <= yy < H:
                        o = (yy * W + xx) * 3
                        for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
        # walkable floor of this level
        for n in graph:
            if abs(n[2] - yf) < 0.05:
                x, z = cell_xz(n[0], n[1]); fill(x - .25, x + .25, z - .25, z + .25, (58, 66, 74))
            elif level == 0 and 0.05 < n[2] < 5.5 or level == 1 and n[2] > L1 + .05:
                x, z = cell_xz(n[0], n[1]); fill(x - .25, x + .25, z - .25, z + .25, (70, 76, 92))
        for rr in ramps:
            for zz in [k * 0.5 for k in range(-64, 64)]:
                for xx in [k * 0.5 for k in range(-80, 80)]:
                    h = wedge_height(rr, xx + .25, zz + .25)
                    if h is not None and ((level == 0) == (h < 3)): fill(xx, xx + .5, zz, zz + .5, (90, 170, 230), .7)
        for b in sorted(blocks, key=lambda b: yspan(b)[1]):
            y0, y1 = yspan(b)
            if b['id'] in (MAP + '-slab', MAP + '-vault') or not yaw_only(b): continue
            if not (y0 < yf + 3 and y1 > yf + 0.05) or not b['_col']: continue
            x0, x1, z0, z1 = rect_xz(b)
            t = max(0.0, min(1.0, (y1 - yf) / 7)); c = (int(110 + 120 * t), int(114 + 110 * t), int(124 + 100 * t))
            if b['role'] == 'cover': c = (150, 112, 74)
            if b['_ch'] == 'r': c = (200, 200, 210)
            if abs(b['rotation']['y']) > 1e-6:
                for zz in [k * 0.25 for k in range(int(z0 * 4) - 8, int(z1 * 4) + 8)]:
                    for xx in [k * 0.25 for k in range(int(x0 * 4) - 8, int(x1 * 4) + 8)]:
                        if dist_xz(b, xx + .125, zz + .125) <= 1e-9: fill(xx, xx + .25, zz, zz + .25, (205, 120, 230))
            else: fill(x0, x1, z0, z1, c)
        for h in hazards:
            col = {'grid': (60, 200, 255), 'burner': (255, 110, 40), 'gas': (130, 230, 120), 'shutter': (255, 200, 40)}[h['kind']]
            vols = h['zones'] or [dict(center=h['center'], size=h['size'])]
            for vz in vols:
                c, s = vz['center'], vz['size']
                if (c['y'] > 3) == (level == 1): fill(c['x'] - s['x'] / 2 - (.15 if h['kind'] == 'shutter' else 0), c['x'] + s['x'] / 2 + (.15 if h['kind'] == 'shutter' else 0), c['z'] - s['z'] / 2 - (.15 if h['kind'] == 'shutter' else 0), c['z'] + s['z'] / 2 + (.15 if h['kind'] == 'shutter' else 0), col, .45)
        for e in entries:
            if e['level'] != level: continue
            col = {'vent': (200, 90, 255), 'cell': (255, 80, 80), 'door': (255, 160, 80), 'drop': (255, 40, 200), 'breach': (255, 230, 40)}[e['kind']]
            x, z = e['exit']['x'], e['exit']['z']
            for a in range(-3, 4):
                for b2 in range(-3, 4):
                    if a * a + b2 * b2 <= 9: px(X(x) + a, Y(z) + b2, col)
        for m in machines:
            if (m['y'] > 3) == (level == 1): fill(m['x'] - .7, m['x'] + .7, m['z'] - .7, m['z'] + .7, (120, 255, 160))
        for i, (x, y, z) in enumerate(teamA + teamB):
            if (y > 3) != (level == 1): continue
            c = (255, 77, 109) if i < 4 else (34, 230, 255)
            for a in range(-4, 5):
                for b2 in range(-4, 5):
                    if a * a + b2 * b2 <= 16: px(X(x) + a, Y(z) + b2, c)
            text(X(x) + 6, Y(z) - 5, ('A' if i < 4 else 'B') + str(i % 4 + 1), c)
        for nm, (x, y, z) in zip(ffa_names, ffa):
            if nm in ('A1', 'B1') or (y > 3) != (level == 1): continue
            for a in range(-3, 4):
                for b2 in range(-3, 4): px(X(x) + a, Y(z) + b2, (255, 255, 255))
            text(X(x) + 6, Y(z) - 5, nm, (255, 255, 255))
        text(ox, 24, 'GROUND FLOOR  Y 0' if level == 0 else 'UPPER FLOOR  Y 6', (200, 220, 235))
    text(10, 8, 'WARD ZERO M1 DRAFT  80 X 64 M  TWO FLOORS  NORTH UP  1 PX = %d CM' % (100 // scale), (232, 251, 255))
    raw = b''.join(b'\x00' + bytes(img[y * W * 3:(y + 1) * W * 3]) for y in range(H))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--png'); ap.add_argument('--ascii', action='store_true'); ap.add_argument('--sections', action='store_true'); a = ap.parse_args()
    ok, report = checks()
    if a.ascii:
        print('GROUND FLOOR (y 0)'); print(ascii_map(0)); print('UPPER FLOOR (y 6)'); print(ascii_map(1))
    if a.sections:
        print('SECTION A-A  x = 0.5, looking west (S left, N right; 1 column = 1 m of z, 1 row = 0.6 m):'); print(section('x', 0.5, -SZ, SZ))
        print('SECTION B-B  z = 5.5, looking north (W left, E right; 1 column = 1 m of x):'); print(section('z', 5.5, -SX, SX))
    print('\n'.join(report))
    if a.json:
        with open(a.json, 'w') as f: json.dump(draft(), f, indent=None, separators=(',', ':'), sort_keys=False)
        print('wrote', a.json)
    if a.png: png(a.png); print('wrote', a.png)
    sys.exit(0 if ok else 1)
