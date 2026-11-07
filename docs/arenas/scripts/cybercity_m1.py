# Cyber City M1 draft blockout (80x64 slab, sunken maglev cut, two 3-storey podiums with spires, an elevated station).
# A draft for Astra: it is NOT wired into the game. It emits draft map data in the UnityMaps.json shape (blocks with the
# id prefixes the presentation keys on, spawns, bounds, pilotBlocks, the two billboard segments and the hazard
# definitions of docs/arenas/CYBERCITY-SIMULATION-CONTRACT.md) and a top-down PNG, and prints its checks.
# Usage: python3 docs/arenas/scripts/cybercity_m1.py [--json out.json] [--png out.png] [--ascii]
# Coordinates: Unity frame, x east, z north, y up, metres. Point symmetric: (x, z) -> (-x, -z) unless noted.
import argparse, itertools, json, math, struct, sys, zlib

MAP = 'cybercity'
SX, SZ = 40, 32            # slab half extents (80 x 64)
BX, BZ = 38, 30            # play bounds (frontage 2 m deep)
CUT = 3.0                  # the Cut (maglev trench): |z| < 3
FLOOR = -4.0               # trench floor top
BAND = 4.2                 # trench wall band runs z 3.0..4.2 (alcoves are 1.2 m deep recesses in it)
STOREY = 3.6

blocks = []                # UnityMaps.json ContentBlock dicts (+ '_ch' painter char, '_los' blocks sight at eye height)
paint_only = []            # (x0, x1, z0, z1, char, mirror)
SWAP = {'n': 's', 's': 'n', 'e': 'w', 'w': 'e', 'ne': 'sw', 'sw': 'ne', 'nw': 'se', 'se': 'nw',
        'nova': 'vega', 'vega': 'nova', 'lantern': 'signal', 'signal': 'lantern', 'neon': 'rain', 'rain': 'neon', 'rank': 'dock', 'dock': 'rank'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def v(x=0, y=0, z=0): return dict(x=round(x, 4), y=round(y, 4), z=round(z, 4))
def q(): return dict(x=0, y=0, z=0, w=1)
HP = {'cover': 34, 'structural': 80, 'masonry': 120}
def box(id, x0, x1, z0, z1, y0, y1, role='static', material='stone', ch='#', mirror=True, segment=None, hp=None, los=None):
    if x1 - x0 < 1e-6 or z1 - z0 < 1e-6 or y1 - y0 < 1e-6: return
    bid = MAP + '-' + id
    b = dict(id=bid, material=material, pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0),
             rotation=q(), role=role, mass=26, segment=(MAP + '-' + segment) if segment else None, hp=hp if hp is not None else HP.get(role, 0), _ch=ch,
             _los=(y1 - max(y0, 0) >= 2.0 and y0 <= 1.2) if los is None else los)
    blocks.append(b)
    if mirror:
        m = dict(b); m['id'] = mirror_id(bid); m['pos'] = v(-b['pos']['x'], b['pos']['y'], -b['pos']['z'])
        if segment: m['segment'] = mirror_id(b['segment'])
        blocks.append(m)

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
def stair(name, a0, a1, w0, w1, y0, y1, axis, base, rise=0.3, run=0.6, tread=None, **kw):
    """Steps from a0 (at y0) to a1 along axis 'x' or 'z' between w0..w1 across; solid down to `base`, or `tread` m thick
    treads where a pilot can walk under them (underside 2.2 m or more above `base`)."""
    n = int(round(abs(y1 - y0) / rise)); d = 1 if a1 > a0 else -1; up = 1 if y1 > y0 else -1
    for k in range(n):
        s0, s1 = a0 + d * run * k, a0 + d * run * (k + 1); top = y0 + up * rise * (k + 1) if up > 0 else y0 - rise * (k + 1)
        bottom = base if tread is None or top - tread - base < 2.2 else top - tread
        lo, hi = min(s0, s1), max(s0, s1)
        if axis == 'x': box(f'{name}-{k}', lo, hi, w0, w1, bottom, top, los=False, **kw)
        else: box(f'{name}-{k}', w0, w1, lo, hi, bottom, top, los=False, **kw)

# ---------------- perimeter: indestructible frontage 2 m deep, 16 m tall, tops not standable ----------------
box('frontage-n', -SX, SX, BZ, SZ, 0, 16, ch='#')
box('frontage-w-n', -SX, -BX, CUT, BZ, 0, 16, ch='#')            # mirror: frontage-e-s
box('frontage-e-n', BX, SX, CUT, BZ, 0, 16, ch='#')              # mirror: frontage-w-s
box('portal-e', BX, SX, -CUT, CUT, -0.8, 16, ch='#')              # tunnel lintel over the Cut; mirror portal-w

# ---------------- the Cut: maglev trench |z| < 3, floor -4, through tunnel portals at x +-40 ----------------
box('trench-floor', -SX, SX, -BAND, BAND, FLOOR - 0.5, FLOOR, ch='~', mirror=False, los=False)
box('guideway', -SX, SX, -0.9, 0.9, FLOOR, FLOOR + 0.3, ch='=', mirror=False, los=False)
# North wall band z 3.0..4.2 with safe alcoves (2.4 wide, 1.2 deep, 2.4 tall) and the two stair exits (2.0 wide).
ALCOVES = [-28, -18, -8, 2, 22, 32]
EXITS = [(-37, -35), (11, 13)]
openings = [(c - 1.2, c + 1.2, 0, 2.4) for c in ALCOVES] + [(a, b, 0, 2.4) for a, b in EXITS]
wall_x('trench-wall-n', -SX, SX, CUT, BAND, FLOOR, -FLOOR, openings, ch='|')
for c in ALCOVES: paint_only.append((c - 1.2, c + 1.2, CUT, BAND, 'a', True))
# North bank ground: the street slab (its top is the street), split around the two stair pits behind the band.
PITS = [(-37, -26), (11, 22)]
box('ground-n', -SX, SX, 6.0, SZ, FLOOR - 0.5, 0, ch='.', los=False)
for i, (a, b) in enumerate([(-SX, PITS[0][0]), (PITS[0][1], PITS[1][0]), (PITS[1][1], SX)]): box(f'ground-n-strip-{i}', a, b, BAND, 6.0, FLOOR - 0.5, 0, ch='.', los=False)
# Stair pits: 16 risers of .25 m on .55 m treads (24 deg) from the street down to a landing at the floor, then out through
# the band's exit. West pit descends westward from x -26; east pit descends westward from x 22.
stair('pit-nw-stair', -26, -34.8, BAND, 6.0, 0, FLOOR, 'x', FLOOR - 0.5, rise=0.25, run=0.55, ch='/')
box('pit-nw-landing', -37, -34.8, BAND, 6.0, FLOOR - 0.5, FLOOR, ch='/', los=False)
stair('pit-ne-stair', 22, 13.2, BAND, 6.0, 0, FLOOR, 'x', FLOOR - 0.5, rise=0.25, run=0.55, ch='/')
box('pit-ne-landing', 11, 13.2, BAND, 6.0, FLOOR - 0.5, FLOOR, ch='/', los=False)
for i, (a, b) in enumerate(PITS):
    box(f'pit-{("nw", "ne")[i]}-rail-n', a - 0.2, b, 6.0, 6.2, 0, 1.0, ch='r', los=False)
    box(f'pit-{("nw", "ne")[i]}-rail-s', a - 0.2, b, 4.0, 4.2, 0, 1.0, ch='r', los=False)
    box(f'pit-{("nw", "ne")[i]}-rail-end', a - 0.2, a, 4.2, 6.0, 0, 1.0, ch='r', los=False)

# ---------------- crossings over the Cut (deck tops at the street, undersides -0.8) ----------------
box('plaza-deck', -7, 7, -CUT, CUT, -0.8, 0, ch='=', mirror=False, los=False)                  # Helix Plaza bridge
box('plaza-planter-n', -5.4, -2.4, 1.2, 2.4, 0, 1.0, ch='p', los=False)                        # mirror plaza-planter-s
box('grid-w-deck', -26, -22, -CUT, CUT, -0.8, 0, ch='g', mirror=False, los=False)              # West Coupling (electrified)
box('grid-e-deck', 22, 26, -CUT, CUT, -0.8, 0, ch='g', mirror=False, los=False)                # East Coupling (electrified)
for g, (a, b) in (('w', (-26, -22)), ('e', (22, 26))):
    # Pylons on the north bank of each coupling and an overhead beam (mirror: the south bank pylons of the other coupling).
    box(f'grid-{g}-pylon-n-1', a - 0.7, a - 0.1, 3.5, 4.1, 0, 3.0, ch='P')
    box(f'grid-{g}-pylon-n-2', b + 0.1, b + 0.7, 3.5, 4.1, 0, 3.0, ch='P')
    box(f'grid-{g}-beam-n', a - 0.7, b + 0.7, 3.5, 4.1, 3.0, 3.4, ch='^', los=False)

# ---------------- Helix Station: concourse at 7.2 over the plaza bridge ----------------
box('station-deck', -9, 9, -4.5, 4.5, 6.6, 7.2, ch='S', mirror=False, los=False)
box('station-col-ne', 7.4, 8.6, 3.4, 4.6, 0, 6.6, ch='C')                                      # mirror station-col-sw
box('station-col-nw', -8.6, -7.4, 3.4, 4.6, 0, 6.6, ch='C')                                    # mirror station-col-se
box('station-rail-n-0', -6.4, 6.0, 4.3, 4.5, 7.2, 8.2, ch='r', los=False)
box('station-rail-n-1', 8.4, 9.0, 4.3, 4.5, 7.2, 8.2, ch='r', los=False)
box('station-rail-e', 8.8, 9.0, -4.5, 4.3, 7.2, 8.2, ch='r', los=False)
box('station-lift', -1.5, 1.5, -1.5, 1.5, 0, 6.6, ch='L', mirror=False)                            # lift core on the plaza bridge (holds the concourse)
box('station-lift-head', -1.5, 1.5, -1.5, 1.5, 7.2, 9.8, ch='L', mirror=False, los=False)          # lift head on the concourse
box('station-bench-n', 2.0, 4.0, 2.8, 3.3, 7.2, 7.7, ch='b', los=False)
# North access: a stair-bridge rising south through Kiosk Alley to a landing over the promenade (open under its high end).
stair('station-stair-n', 21.0, 6.6, -7.8, -6.0, 0, 7.2, 'z', 0, tread=0.4, ch='/')
box('station-landing-n', -9, -6.0, 4.5, 6.6, 6.6, 7.2, ch='/', los=False)
# Skybridge from the concourse to Nova's second floor (7.2).
box('skybridge-ne', 6.2, 8.2, 4.5, 10.0, 6.6, 7.2, ch='=', los=False)
box('skybridge-ne-rail-w', 6.0, 6.2, 4.5, 10.0, 7.2, 8.2, ch='r', los=False)
box('skybridge-ne-rail-e', 8.2, 8.4, 4.5, 10.0, 7.2, 8.2, ch='r', los=False)

# ---------------- Nova Arcade (NE podium, 3 storeys + roof 10.8) and the Nova spire ----------------
NX0, NX1, NZ0, NZ1, T = 6, 24, 10, 21, 0.3
SPIRE = (13.5, 18.5, 13, 18)
box('nova-spire', *SPIRE, 0, 24, ch='X')
S1, S2, S3 = (7.0, 8.6), (10.8, 12.4), (7.0, 8.6)                    # stair columns (x) running z 12.6..19.8
faces = {
    'south': [[(8, 10, 0, 2.7), (20, 22, 0, 2.7), (11.0, 13.6, 0.9, 2.8), (16.4, 19.0, 0.9, 2.8)],
              [(7, 12, 1.0, 2.6), (14, 18, 1.0, 2.6), (20, 23, 1.0, 2.6)],
              [(6.4, 8.4, 0, 2.6), (10, 16, 1.0, 2.6), (18, 23, 1.0, 2.6)]],
    'north': [[(9.2, 11, 0, 2.7), (19.6, 21.4, 0, 2.7), (14.2, 17.8, 0.9, 2.8)],
              [(9, 12, 1.0, 2.6), (14, 18, 1.0, 2.6), (20, 23, 1.0, 2.6)],
              [(9, 12, 1.0, 2.6), (14, 18, 1.0, 2.6), (20, 23, 1.0, 2.6)]],
    'west':  [[(12, 14, 0, 2.7), (16, 19.5, 0.9, 2.8)], [(11, 14, 1.0, 2.6), (16, 19.5, 1.0, 2.6)], [(11, 14, 1.0, 2.6), (16, 19.5, 1.0, 2.6)]],
    'east':  [[(15, 17, 0, 2.7), (11, 13.5, 0.9, 2.8), (18.5, 20.3, 0.9, 2.8)], [(11, 14, 1.0, 2.6), (16, 19.5, 1.0, 2.6)], [(11, 14, 1.0, 2.6), (16, 19.5, 1.0, 2.6)]],
}
for s in range(3):
    y0 = s * STOREY
    wall_x(f'nova-s{s}-south', NX0, NX1, NZ0, NZ0 + T, y0, STOREY, faces['south'][s], ch='H')
    wall_x(f'nova-s{s}-north', NX0, NX1, NZ1 - T, NZ1, y0, STOREY, faces['north'][s], ch='H')
    wall_z(f'nova-s{s}-west', NZ0 + T, NZ1 - T, NX0, NX0 + T, y0, STOREY, faces['west'][s], ch='H')
    wall_z(f'nova-s{s}-east', NZ0 + T, NZ1 - T, NX1 - T, NX1, y0, STOREY, faces['east'][s], ch='H')
inner = (NX0 + T, NX1 - T, NZ0 + T, NZ1 - T)
slab('nova-floor-1', *inner, STOREY - 0.3, STOREY, [SPIRE, (S1[0], S1[1], 12.6, 19.8)], ch='H')
slab('nova-floor-2', *inner, 2 * STOREY - 0.3, 2 * STOREY, [SPIRE, (S2[0], S2[1], 12.6, 19.8)], ch='H')
slab('nova-roof', *inner, 3 * STOREY - 0.3, 3 * STOREY, [SPIRE, (S3[0], S3[1], 12.6, 19.8)], ch='H')
stair('nova-stair-1', 19.8, 12.6, *S1, 0, STOREY, 'z', 0, ch='/')
stair('nova-stair-2', 12.6, 19.8, *S2, STOREY, 2 * STOREY, 'z', STOREY, ch='/')
stair('nova-stair-3', 19.8, 12.6, *S3, 2 * STOREY, 3 * STOREY, 'z', 2 * STOREY, ch='/')
R = 3 * STOREY
box('nova-parapet-s', NX0, NX1, NZ0, NZ0 + 0.25, R, R + 1, ch='H', los=False)
box('nova-parapet-n', NX0, NX1, NZ1 - 0.25, NZ1, R, R + 1, ch='H', los=False)
box('nova-parapet-w', NX0, NX0 + 0.25, NZ0 + 0.25, NZ1 - 0.25, R, R + 1, ch='H', los=False)
box('nova-parapet-e', NX1 - 0.25, NX1, NZ0 + 0.25, NZ1 - 0.25, R, R + 1, ch='H', los=False)
box('nova-roof-unit-1', 19.5, 22.5, 11.0, 12.8, R, R + 1.8, ch='u', los=False)
box('nova-roof-unit-2', 9.4, 11.6, 18.6, 20.2, R, R + 1.6, ch='u', los=False)
box('nova-lobby-desk', 19.4, 22.4, 13.6, 14.4, 0, 1.1, ch='o', los=False)

# ---------------- Lantern Market (NW): canopy (presentation lattice at 5.4 on posts), noodle bar, container perch ----------------
for i, (px, pz) in enumerate(((-35.5, 10.6), (-24.2, 10.6), (-12.5, 10.6), (-35.5, 21.2), (-24.2, 21.2), (-12.5, 21.2))):
    box(f'lantern-post-{i}', px - 0.15, px + 0.15, pz - 0.15, pz + 0.15, 0, 5.4, ch='i', los=False)
wall_x('lantern-wall', -36, -12, 9.6, 10.0, 0, 3.6, [(-31.2, -28.8, 0, 3.0), (-21.4, -19.0, 0, 3.0), (-15.6, -13.2, 0, 3.0)], ch='M')   # market hall's south wall, three arches
box('lantern-bar', -31, -25, 17.6, 21.6, 0, 3.2, ch='B')
box('lantern-counter', -31, -25, 16.4, 17.0, 0, 1.1, ch='o', los=False)
box('lantern-box-1', -21, -14.9, 17.8, 20.2, 0, 2.6, ch='K')
box('lantern-box-2', -21, -14.9, 15.4, 17.8, 0, 2.6, ch='K')
box('lantern-box-3', -20.4, -15.5, 18.0, 20.2, 2.6, 5.2, ch='K', los=False)
stair('lantern-stair', 10.6, 15.4, -22.9, -21.1, 0, 2.6, 'z', 0, rise=0.26, ch='/')
for i, (cx, cz) in enumerate(((-33.4, 12.6), (-28.6, 12.2), (-27.4, 14.9), (-17.6, 12.6), (-13.4, 14.4))):
    box(f'lantern-stall-{i}', cx - 1.2, cx + 1.2, cz - 0.6, cz + 0.6, 0, 1.1, role='cover', material='wood', ch='s', los=False)
box('lantern-vend-0', -36.4, -35.5, 14.6, 15.6, 0, 2.0, role='cover', material='metal', ch='v', hp=60)
box('lantern-vend-1', -13.1, -12.2, 16.6, 17.6, 0, 2.0, role='cover', material='metal', ch='v', hp=60)

box('lantern-booth', -37.6, -33.6, 10.0, 13.6, 0, 3.2, ch='B')                                  # pachinko booth at the market's west end

# ---------------- Kiosk Alley (x -12..6) ----------------
box('alley-n-kiosk', 0.5, 3.5, 15.5, 17.5, 0, 2.8, ch='k')
box('alley-n-bin-0', -11.4, -9.6, 12.0, 13.1, 0, 1.3, role='cover', material='metal', ch='o', los=False, hp=60)
box('alley-n-bin-1', 3.6, 5.4, 20.2, 21.3, 0, 1.3, role='cover', material='metal', ch='o', los=False, hp=60)
box('alley-n-bike', -3.5, -1.3, 11.0, 11.8, 0, 1.1, role='cover', material='metal', ch='o', los=False, hp=60)

# ---------------- Neon Avenue (z 22..30) and the north holo billboard (segment cybercity-billboard-n) ----------------
box('neon-van-0', -31, -25, 23.0, 25.4, 0, 2.6, ch='t')
box('neon-van-1', 10, 16, 25.2, 27.6, 0, 2.6, ch='t')
box('neon-kiosk', 18.5, 21, 22.8, 25.0, 0, 2.8, ch='k')
box('neon-pillar', -6.4, -5.6, 25.6, 26.4, 0, 2.6, ch='O')
for i, (lx, lz) in enumerate(((-34, 22.4), (-18, 22.4), (6, 22.4), (24.5, 29.5))):
    box(f'lamp-n-{i}', lx - 0.15, lx + 0.15, lz - 0.15, lz + 0.15, 0, 5.8, ch='l', los=False)
BB = dict(x0=-10.4, x1=4.4, z0=28.4, z1=29.2)
box('billboard-n-strut-w', -10.4, -9.6, 28.4, 29.2, 0, 9.0, role='structural', material='metal', ch='P', segment=None, hp=80)
box('billboard-n-strut-e', 3.6, 4.4, 28.4, 29.2, 0, 9.0, role='structural', material='metal', ch='P', segment=None, hp=80)
box('billboard-n-truss', -10.4, 4.4, 28.4, 29.2, 9.0, 9.6, role='carried', material='metal', ch='^', segment='billboard-n', los=False)
box('billboard-n-screen', -10.0, 4.0, 28.6, 29.0, 9.6, 15.0, role='carried', material='metal', ch='^', segment='billboard-n', los=False)
box('billboard-n-frame-w', -10.4, -10.0, 28.5, 29.1, 9.6, 15.4, role='carried', material='metal', ch='^', segment='billboard-n', los=False)
box('billboard-n-frame-e', 4.0, 4.4, 28.5, 29.1, 9.6, 15.4, role='carried', material='metal', ch='^', segment='billboard-n', los=False)
box('billboard-n-frame-top', -10.0, 4.0, 28.5, 29.1, 15.0, 15.4, role='carried', material='metal', ch='^', segment='billboard-n', los=False)
paint_only.append((-10.5, 4.5, 22.6, 28.4, 'f', True))       # fell zone (kept free of props)

# ---------------- Cab Rank (NE yard, team B spawn) ----------------
box('rank-canopy', 30, 37.4, 12, 17.5, 3.3, 3.6, ch='^', los=False)
for i, (px, pz) in enumerate(((30.15, 12.15), (37.25, 12.15), (30.15, 17.35), (37.25, 17.35))):
    box(f'rank-post-{i}', px - 0.15, px + 0.15, pz - 0.15, pz + 0.15, 0, 3.3, ch='i', los=False)
box('rank-cab-0', 27.5, 32, 25.6, 27.6, 0, 1.6, ch='c', los=False)
box('rank-cab-1', 32.6, 37.1, 20.8, 22.8, 0, 1.6, ch='c', los=False)
box('rank-pylon-0', 28.4, 29.2, 14.0, 14.8, 0, 2.2, ch='O')
box('rank-pylon-1', 29.0, 29.8, 20.6, 21.4, 0, 2.2, ch='O')
box('rank-barrier', 26.6, 29.6, 9.6, 10.2, 0, 1.0, ch='o', los=False)
box('rank-wall', 26.0, 26.4, 13.0, 20.0, 0, 2.4, ch='M')

# ---------------- promenade (z 3..9 north) ----------------
for i, (cx, cz) in enumerate(((-31, 7.6), (-15, 7.2), (17, 7.6), (31, 7.0))):
    box(f'prom-n-planter-{i}', cx - 1.5, cx + 1.5, cz - 0.6, cz + 0.6, 0, 1.0, ch='p', los=False)
box('prom-n-pillar-0', -19.9, -19.1, 6.1, 6.9, 0, 2.6, ch='O')
box('prom-n-pillar-1', 2.1, 2.9, 7.6, 8.4, 0, 2.6, ch='O')
box('prom-n-bench-0', -12, -10, 8.2, 8.7, 0, 0.5, ch='b', los=False)
box('prom-n-shelter-0', -14.2, -10.2, 7.4, 8.8, 0, 2.6, ch='O')                                 # transit shelter (back wall and roof read as hard cover)
box('prom-n-shelter-1', 26.6, 30.6, 7.8, 9.0, 0, 2.6, ch='O')

# ---------------- spawns ----------------
teamB = [(34.8, 27.2), (32.4, 14.6), (35.6, 18.6), (28.6, 23.2)]
teamA = [(-x, -z) for x, z in teamB]
ffa = [(-34.5, 19.5), (34.5, -19.5), teamA[0], teamB[0], (10.5, 12.5), (-10.5, -12.5), (-16.5, 25.5), (16.5, -25.5)]
ffa_names = ['F1', 'F2', 'A1', 'B1', 'F5', 'F6', 'F7', 'F8']
def spawn(x, z): return dict(pos=v(x, 1.1, z), yaw=round(math.degrees(math.atan2(-x, -z)) % 360, 3))

# ---------------- segments (the two holo billboards; ARENA_PROPOSAL.md §5.2 shape, BRICKFALL contract fields) ----------------
def billboard_segment(sid, strut_w, strut_e, members, crush, rubble):
    # Tear-hinge (the Brickfall bridge mode): the screen swings forward about its bottom edge (the truss line, y 9.3) through
    # 90 degrees in 0.7 s while dropping 9.05 m from +0.3 s, so it lands flat across the avenue where its rubble swaps in.
    return dict(id=MAP + '-' + sid, building=MAP + '-' + sid, mode='tear-hinge', material='metal',
                supports=[dict(member=MAP + '-' + strut_w, w=1), dict(member=MAP + '-' + strut_e, w=1)], failBelow=2,
                telegraph=1.0, duration=0.8, maxAccel=9.81, mass=6.5, direction=v(0, 0, -1), pivot=v(-3.0, 9.3, 28.8), axis=v(1, 0, 0), angle=-90,
                blocks=[MAP + '-' + m for m in members],
                poses=[dict(block=MAP + '-' + m, pivot=v(-3.0, 9.3, 28.8), axis=v(1, 0, 0), angle=-90.0, drop=9.05, delay=0.0) for m in members],
                crush=dict(center=v(*crush[0]), size=v(*crush[1]), damage=45, push=6, cap=60, capHpFraction=0.6),
                sweeps=[dict(center=v(*crush[0]), size=v(*crush[1]))], rubble=rubble, nav=dict(remove=[], add=[]))
seg_members = ['billboard-n-truss', 'billboard-n-screen', 'billboard-n-frame-w', 'billboard-n-frame-e', 'billboard-n-frame-top']
def rubble_box(rid, x0, x1, z0, z1, y0, y1):
    return dict(id='rubble-' + MAP + '-' + rid, material='metal', pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0), rotation=q(), role='rubble', mass=26)
seg_n = billboard_segment('billboard-n', 'billboard-n-strut-w', 'billboard-n-strut-e', seg_members,
                          ((-3.0, 7.75, 25.95), (15.0, 15.5, 7.1)),
                          [rubble_box('billboard-n-0', -10.0, 4.0, 23.1, 28.5, 0, 0.5), rubble_box('billboard-n-1', -10.4, 4.4, 28.4, 29.2, 0, 0.6),
                           rubble_box('billboard-n-2', -10.0, 4.0, 22.7, 23.1, 0, 0.4)])
def mirror_seg(s):
    m = json.loads(json.dumps(s))
    m['id'] = mirror_id(s['id']); m['building'] = m['id']
    for p in m['supports']: p['member'] = mirror_id(p['member'])
    m['blocks'] = [mirror_id(b) for b in m['blocks']]; m['direction'] = v(0, 0, 1); m['axis'] = v(-1, 0, 0)
    m['pivot'] = v(-s['pivot']['x'], s['pivot']['y'], -s['pivot']['z'])
    for p in m['poses']: p['block'] = mirror_id(p['block']); p['pivot'] = v(-p['pivot']['x'], p['pivot']['y'], -p['pivot']['z']); p['axis'] = v(-1, 0, 0)
    for k in ('crush',): m[k]['center'] = v(-s[k]['center']['x'], s[k]['center']['y'], -s[k]['center']['z'])
    m['sweeps'] = [dict(center=v(-w['center']['x'], w['center']['y'], -w['center']['z']), size=w['size']) for w in s['sweeps']]
    for r in m['rubble']: r['id'] = mirror_id(r['id']); r['pos'] = v(-r['pos']['x'], r['pos']['y'], -r['pos']['z'])
    return m
segments = [seg_n, mirror_seg(seg_n)]

# ---------------- hazards (CYBERCITY-SIMULATION-CONTRACT.md §3: one schema, pure timing) ----------------
hazards = [
    dict(id=MAP + '-maglev', kind='maglev', center=v(0, (FLOOR + -0.9) / 2, 0), size=v(2 * SX, -0.9 - FLOOR, 5.4), axis=v(1, 0, 0),
         first=18, period=32, offset=0, warn=6, danger=3, speed=44, length=42, charge=0, live=0, tick=0, damage=0, lethal=True),
    dict(id=MAP + '-grid-w', kind='grid', center=v(-24, 0.5, 0), size=v(4, 1.0, 6.0), axis=v(0, 0, 1),
         first=4, period=14, offset=7, warn=0, danger=0, speed=0, length=0, charge=2.5, live=2.0, tick=0.25, damage=13, lethal=False),
    dict(id=MAP + '-grid-e', kind='grid', center=v(24, 0.5, 0), size=v(4, 1.0, 6.0), axis=v(0, 0, 1),
         first=11, period=14, offset=-7, warn=0, danger=0, speed=0, length=0, charge=2.5, live=2.0, tick=0.25, damage=13, lethal=False),
]
pilot_blocks = [
    dict(id=MAP + '-frontage-top-n', center=v(0, 0, 31), size=v(80, 0, 2), bottom=15.9), dict(id=MAP + '-frontage-top-s', center=v(0, 0, -31), size=v(80, 0, 2), bottom=15.9),
    dict(id=MAP + '-frontage-top-w', center=v(-39, 0, 0), size=v(2, 0, 64), bottom=15.9), dict(id=MAP + '-frontage-top-e', center=v(39, 0, 0), size=v(2, 0, 64), bottom=15.9),
    dict(id=MAP + '-spire-top-nova', center=v(16, 0, 15.5), size=v(5, 0, 5), bottom=23.9), dict(id=MAP + '-spire-top-vega', center=v(-16, 0, -15.5), size=v(5, 0, 5), bottom=23.9),
    dict(id=MAP + '-tunnel-e', center=v(39, 0, 0), size=v(2, 0, 6), bottom=FLOOR), dict(id=MAP + '-tunnel-w', center=v(-39, 0, 0), size=v(2, 0, 6), bottom=FLOOR),
]

# ---------------- pure hazard timing (the reference the contract pins; presentation mirrors it in Core) ----------------
def maglev_pass(h, rnd, fight_at, t):
    """(k, direction, head x, phase) at match time t: phase 'idle' | 'warn' | 'danger' | 'pass'."""
    if t < fight_at: return None
    u = t - fight_at - h['first'] + h['warn']
    if u < 0: return (-1, 0, None, 'idle')
    k = int(u // h['period']); s = u - k * h['period'] - h['warn']      # s: seconds since this pass's head reached its portal
    d = 1 if (k + rnd) % 2 == 0 else -1; span = 2 * SX; dur = (span + h['length']) / h['speed']
    if s < -h['danger']: return (k, d, None, 'warn')
    if s < 0: return (k, d, None, 'danger')
    if s < dur: return (k, d, -d * SX + d * h['speed'] * s, 'pass')
    return (k, d, None, 'idle')
def grid_phase(h, rnd, fight_at, t):
    u = t - fight_at - h['first'] - (h['offset'] if rnd % 2 == 0 else 0)
    if u < 0: return 'idle'
    c = u % h['period']; idle = h['period'] - h['charge'] - h['live'] - 1.0
    return 'idle' if c < idle else 'charge' if c < idle + h['charge'] else 'live' if c < idle + h['charge'] + h['live'] else 'discharge'

# ---------------- checks ----------------
def footprint(b):
    p, s = b['pos'], b['size']; return (p['x'] - s['x'] / 2, p['x'] + s['x'] / 2, p['z'] - s['z'] / 2, p['z'] + s['z'] / 2, p['y'] - s['y'] / 2, p['y'] + s['y'] / 2)
def seg_hits(a, c, b, eye):
    x0, x1, z0, z1, y0, y1 = footprint(b)
    if not b['_los'] or not (y0 <= eye <= y1): return False
    t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((a[0], c[0] - a[0], x0, x1), (a[1], c[1] - a[1], z0, z1)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
def visible(a, c, eye): return not any(seg_hits(a, c, b, eye) for b in blocks)
def los_report(pairs, label, out):
    bad = []
    for (na, a), (nb, c) in pairs:
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9) if visible((a[0] + dx, a[1] + dz), (c[0] + ex, c[1] + ez), eye))
        if n: bad.append(f'{na}-{nb} ({n}/243 open, {math.dist(a, c):.1f} m)')
    out.append(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights' + ''.join('\n   OPEN ' + s for s in bad))
    return not bad
def dist_rect(p, r):
    dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)
def checks():
    out, ok = [], True
    named = list(zip(ffa_names, ffa))
    ok &= los_report(list(itertools.combinations(named, 2)), 'FFA', out)
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]
    ok &= los_report([(p, c) for p in A for c in B], 'Teams A-B', out)
    sp = ffa + teamA[1:] + teamB[1:]
    out.append('min FFA spacing %.1f m; min A-B spacing %.1f m' % (min(math.dist(p, c) for p, c in itertools.combinations(ffa, 2)), min(math.dist(p, c) for p in teamA for c in teamB)))
    vols = [((-SX, SX, -CUT, CUT), 'the Cut (maglev envelope)'), ((-26, -22, -CUT - 0.6, CUT + 0.6), 'grid-w'), ((22, 26, -CUT - 0.6, CUT + 0.6), 'grid-e'),
            ((-10.5, 4.5, 22.5, 29.5), 'billboard-n crush'), ((-4.5, 10.5, -29.5, -22.5), 'billboard-s crush')]
    worst = min((dist_rect(p, r), n, p) for p in sp for r, n in vols)
    out.append('closest spawn to any hazard or crush volume: %.1f m (%s, spawn %s)' % worst); ok &= worst[0] >= 3
    clr = min((min(dist_rect(p, footprint(b)[:4]) for b in blocks if footprint(b)[5] > 0.3 and footprint(b)[4] < 2.2), p) for p in sp)
    out.append('closest spawn to any block it could touch: %.1f m at %s' % clr); ok &= clr[0] >= 1.5
    out.append('all spawns inside the play bounds less 2 m: %s' % all(abs(x) <= BX - 2 and abs(z) <= BZ - 2 for x, z in sp))
    # Safe pockets on the trench floor: alcoves and stair exits on both walls (the south wall's are the mirror).
    pockets = sorted([c for c in ALCOVES] + [-c for c in ALCOVES] + [(a + b) / 2 for a, b in EXITS] + [-(a + b) / 2 for a, b in EXITS])
    worst_pocket = max(min(abs(x - p) for p in pockets) for x in [i * 0.5 for i in range(-76, 77)])
    out.append('farthest trench-floor point from a safe pocket (alcove or stair exit, either wall): %.1f m along the Cut; %.2f s at 6.8 m/s against a %ds warning' % (worst_pocket, worst_pocket / 6.8, hazards[0]['warn']))
    ok &= worst_pocket / 6.8 < 1.5
    ids = [b['id'] for b in blocks]; dup = [i for i in set(ids) if ids.count(i) > 1]
    out.append('blocks %d (static %d, cover %d, structural %d, carried %d), unique ids: %s' % (len(blocks), *[sum(1 for b in blocks if b['role'] == r) for r in ('static', 'cover', 'structural', 'carried')], not dup)); ok &= not dup
    out.append('initial prop frame (non-static blocks): %d' % sum(1 for b in blocks if b['role'] != 'static'))
    # Hazard timing table for round 1 with the fight at 10 s.
    h = hazards[0]; rows = []
    for k in range(4):
        t = 10 + h['first'] + k * h['period']; d = maglev_pass(h, 1, 10, t + .01)[1]
        rows.append(f'  pass {k}: warn {t - h["warn"]:.0f} s, danger {t - h["danger"]:.0f} s, head at the {"west" if d > 0 else "east"} portal {t:.0f} s, clear {t + (2 * SX + h["length"]) / h["speed"]:.2f} s ({"W->E" if d > 0 else "E->W"})')
    out.append('maglev (round 1, fight at 10 s):\n' + '\n'.join(rows))
    for g in hazards[1:]:
        t = 10 + g['first']; idle = g['period'] - g['charge'] - g['live'] - 1
        out.append(f'{g["id"]} (round 1; even rounds {g["offset"]:+.0f} s): charge {t + idle:.1f}-{t + idle + g["charge"]:.1f} s, live {t + idle + g["charge"]:.1f}-{t + idle + g["charge"] + g["live"]:.1f} s, discharge to {t + g["period"]:.1f} s, every {g["period"]:.0f} s')
    return ok, out

# ---------------- outputs ----------------
def draft():
    clean = [{k: b[k] for k in b if not k.startswith('_')} for b in blocks]
    return dict(schema=1, draft=True, note='Draft blockout from docs/arenas/scripts/cybercity_m1.py for Astra; not game content.',
                materials=[dict(id='stone', color=0x5A5F6E, mass=26, durability=85, friction=0.85, indestructible=False)],
                maps=[dict(id=MAP, name='Cyber City', tag='A neon megacity interchange at 02:00. Mind the Helix Line.', theme=MAP, music='kinetic-city', ambience=MAP,
                           look='neon-rain', sky=0x1A1438, floor=0x24232B, accent=0xFF3DCF, rotation=False,
                           bounds=dict(minX=-BX, maxX=BX, minZ=-BZ, maxZ=BZ, ceiling=24, killY=-8),
                           spawns=dict(ffa=[spawn(*p) for p in ffa], teams=[dict(side=0, points=[spawn(*p) for p in teamA]), dict(side=1, points=[spawn(*p) for p in teamB])]),
                           blocks=clean, segments=segments, hazards=hazards, pilotBlocks=pilot_blocks)])
def section(axis, at, lo, hi, top=25.2, bottom=-4.8, step=1.2):
    """A vertical slice through the blocks at x = at (axis 'x': columns along z) or z = at (axis 'z': columns along x), 1 m
    columns from lo to hi, rows of `step` m from top down: each cell shows the block that fills its middle."""
    rows = []
    y = top
    while y > bottom - 1e-6:
        line = ''
        for c in range(lo, hi):
            u = c + .5; ch = ' '
            for b in blocks:
                x0, x1, z0, z1, y0, y1 = footprint(b)
                if (x0 <= at <= x1 and z0 < c + 1 and z1 > c) if axis == 'x' else (z0 <= at <= z1 and x0 < c + 1 and x1 > c):
                    if y0 < y and y1 > y - step and (ch == ' ' or ch == '.'): ch = b['_ch']
            line += ch
        rows.append(f'{y:5.1f} ' + line)
        y -= step
    return '\n'.join(rows)
def ascii_map():
    W, Rn = 2 * SX, SZ
    g = [['.'] * W for _ in range(Rn)]
    def put(x, z, ch):
        c, r = int(math.floor(x + SX)), int((SZ - z) // 2)
        if 0 <= c < W and 0 <= r < Rn: g[r][c] = ch
    def rect(x0, x1, z0, z1, ch):
        zz = z0
        while zz < z1:
            xx = x0
            while xx < x1: put(xx + .25, zz + .25, ch); xx += .5
            zz += .5
    rect(-SX, SX, -CUT, CUT, '~')
    order = sorted(blocks, key=lambda b: footprint(b)[5])
    for b in order:
        x0, x1, z0, z1, y0, y1 = footprint(b)
        if b['_ch'] in '.~': continue
        if y1 <= 0.05 and b['_ch'] not in '=g': continue
        rect(x0, x1, z0, z1, b['_ch'])
    for (x0, x1, z0, z1, ch, m) in paint_only:
        rect(x0, x1, z0, z1, ch)
        if m: rect(-x1, -x0, -z1, -z0, ch)
    for i, (x, z) in enumerate(teamA): put(x, z, str(i + 1)); put(-x, -z, str(i + 5))
    for nm, (x, z) in zip(ffa_names, ffa):
        if nm not in ('A1', 'B1'): put(x, z, nm)
    out = ['          x=-40' + ' ' * 34 + 'x=0' + ' ' * 35 + 'x=+39']
    for r in range(Rn):
        z = SZ - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

GLYPHS = dict(A='010101111101101', B='110101110101110', C='011100100100011', D='110101101101110', E='111100110100111', F='111100110100100',
              G='011100101101011', H='101101111101101', I='111010010010111', K='101101110101101', L='100100100100111', M='101111111101101',
              N='110101101101101', O='010101101101010', P='110101110100100', R='110101110101101', S='011100010001110', T='111010010010010',
              U='101101101101111', V='101101101101010', W='101101111111101', X='101101010101101', Y='101101010010010', Z='111001010100111')
GLYPHS.update({'0': '111101101101111', '1': '010110010010111', '2': '110001010100111', '3': '110001010001110', '4': '101101111001001',
               '5': '111100110001110', '6': '011100111101111', '7': '111001010010010', '8': '111101111101111', '9': '111101111001110',
               '-': '000000111000000', '+': '000010111010000', '.': '000000000000010', ':': '000010000010000', '/': '001001010100100', ' ': '0' * 15})
def png(path, scale=8):
    W, H = 2 * SX * scale + 20, 2 * SZ * scale + 60
    img = bytearray([18, 20, 26] * W * H)
    def px(x, y, c):
        if 0 <= x < W and 0 <= y < H: img[(y * W + x) * 3:(y * W + x) * 3 + 3] = bytes(c)
    def X(x): return int(round((x + SX) * scale)) + 10
    def Y(z): return int(round((SZ - z) * scale)) + 40
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        for yy in range(Y(z1), Y(z0)):
            for xx in range(X(x0), X(x1)):
                if 0 <= xx < W and 0 <= yy < H:
                    o = (yy * W + xx) * 3
                    for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    def text(x, y, s, c, k=2):
        for ch in s.upper():
            bits = GLYPHS.get(ch, '0' * 15)
            for r in range(5):
                for col in range(3):
                    if bits[r * 3 + col] == '1':
                        for a in range(k):
                            for b in range(k): px(x + col * k + a, y + r * k + b, c)
            x += 4 * k
    fill(-SX, SX, -SZ, SZ, (52, 54, 64))                                     # street
    fill(-SX, SX, -CUT, CUT, (22, 26, 44))                                   # the Cut
    def shade(top):
        t = max(0.0, min(1.0, (top + 4) / 28)); return (int(70 + 150 * t), int(78 + 140 * t), int(98 + 130 * t))
    pal = {'structural': (244, 179, 33), 'cover': (150, 110, 70), 'carried': (120, 210, 255)}
    for b in sorted(blocks, key=lambda b: footprint(b)[5]):
        x0, x1, z0, z1, y0, y1 = footprint(b)
        if b['_ch'] in '.~' or (y1 <= 0.05 and b['_ch'] not in '=g'): continue
        c = pal.get(b['role'], shade(y1))
        if b['_ch'] == 'g': c = (230, 200, 60)
        if b['_ch'] == '|': c = (90, 96, 120)
        fill(x0, x1, z0, z1, c)
    for (x0, x1, z0, z1, ch, m) in paint_only:
        col = (40, 220, 140) if ch == 'a' else (214, 51, 42)
        for (a, b2, c0, c1) in ([(x0, x1, z0, z1)] + ([(-x1, -x0, -z1, -z0)] if m else [])): fill(a, b2, c0, c1, col, 0.55)
    for x in range(-SX, SX + 1, 10):
        for zz in range(Y(SZ), Y(-SZ)): px(X(x), zz, (34, 36, 46)) if img[(zz * W + X(x)) * 3] == 52 else None
    for i, (x, z) in enumerate(teamA + teamB):
        c = (255, 77, 109) if i < 4 else (34, 230, 255)
        for a in range(-4, 5):
            for b2 in range(-4, 5):
                if a * a + b2 * b2 <= 16: px(X(x) + a, Y(z) + b2, c)
        text(X(x) + 6, Y(z) - 5, ('A' if i < 4 else 'B') + str(i % 4 + 1), c)
    for nm, (x, z) in zip(ffa_names, ffa):
        if nm in ('A1', 'B1'): continue
        for a in range(-3, 4):
            for b2 in range(-3, 4): px(X(x) + a, Y(z) + b2, (255, 255, 255))
        text(X(x) + 6, Y(z) - 5, nm, (255, 255, 255))
    text(10, 8, 'CYBER CITY M1 DRAFT  80 X 64 M  NORTH UP  1 PX = %d CM' % (100 // scale), (232, 251, 255))
    text(10, 22, 'CUT -4  STREET 0  FLOORS 3.6 7.2  ROOFS 10.8  SPIRES 24  STATION 7.2', (138, 166, 196))
    raw = b''.join(b'\x00' + bytes(img[y * W * 3:(y + 1) * W * 3]) for y in range(H))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--json'); ap.add_argument('--png'); ap.add_argument('--ascii', action='store_true'); ap.add_argument('--sections', action='store_true'); a = ap.parse_args()
    if a.ascii: print(ascii_map())
    if a.sections:
        print('SECTION A-A  x = 7, looking west (S left, N right; 1 column = 1 m of z):'); print(section('x', 7, -SZ, SZ))
        print('SECTION B-B  z = 15.5, looking north (W left, E right; 1 column = 1 m of x):'); print(section('z', 15.5, -SX, SX))
    ok, report = checks(); print('\n'.join(report))
    if a.json:
        with open(a.json, 'w') as f: json.dump(draft(), f, indent=None, separators=(',', ':'))
        print('wrote', a.json)
    if a.png: png(a.png); print('wrote', a.png)
    sys.exit(0 if ok else 1)
